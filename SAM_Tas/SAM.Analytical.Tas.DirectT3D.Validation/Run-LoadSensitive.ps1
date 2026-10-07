# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

<#
.SYNOPSIS
  Runs the load-sensitive three-zone fixture (LoadSensitiveModel) through BOTH SAM -> T3D routes with the production workflow
  settings (UseWidths = false), simulates a full year on each, and compares everything between the routes: the TBD inputs that
  decide the loads, every hourly zone series of the two TSDs, and the SAM model each route hands back.

.DESCRIPTION
  Needs a licensed Tas, a built harness (see DIRECT_T3D_ROUTE.md) and the TAS GUI CLOSED. Each TAS-driving step is its own process
  (a TAS COM server is not safely shared) with a watchdog, and TAS is swept before and after every one. Output goes under a SHORT
  path: a long one makes TAS show a modal save error.

  Models: 'loadsensitive' (spaces listed A, B, C) and 'loadsensitive-reversed' (listed C, B, A, which moves every partition's reversed
  side to the other zone and so makes the direct route's UpdateReversed repair do real work).

  -Control also runs the gbXML route a SECOND time and compares it with the first: the same TBD simulated twice. That is the noise
  floor; it must be bit-identical, or no difference between the routes could be told from noise.

  -OrderControl runs each route a second time with the panels added in the opposite order (same building, different TAS surface creation
  order) and judges the route difference against that: with natural ventilation active TAS's solve is order sensitive at about 1e-3 of a
  series' peak, so a route difference no larger than twice the same-route order sensitivity is not a modelling difference.

  Exit code 0 only when, for every model, the TBD inputs show no UNEXPECTED difference and the simulation passes: with -OrderControl,
  every series is explained by order sensitivity; without it, no series differs by more than $Tolerance of its own peak. That fixed
  tolerance came from a free-running model's 8.95e-5 and is too tight for a naturally ventilated one - which is why -OrderControl exists.
#>
param(
    [string]$OutDir = 'C:\t3dv\ls',
    [string]$Exe = (Join-Path $PSScriptRoot 'bin\Debug\direct-t3d-validation.exe'),
    [string]$WeatherFile = 'C:\Users\Public\Documents\Tas Data\Databases\cibseweather2005.twd',
    [string]$WeatherName = 'London TRY',
    [string[]]$Models = @('loadsensitive', 'loadsensitive-reversed'),
    [switch]$Control,
    [switch]$OrderControl,
    [int]$TimeoutSeconds = 540,
    [double]$Tolerance = 1e-4
)

$ErrorActionPreference = 'Stop'

function Clear-Tas {
    Get-Process -Name 'ProfGate', 'TBD', 'TSD', 'TAS3D', 'TPD', 'TWD', 'TCD', 'TIC', 'direct-t3d-validation' -ErrorAction SilentlyContinue |
        Where-Object { $_.Id -ne $PID } | Stop-Process -Force -ErrorAction SilentlyContinue
}

function Invoke-Harness {
    param([string[]]$Arguments, [string]$Log)
    Clear-Tas
    # Start-Process joins the argument list on spaces, so anything containing one is quoted here.
    $quoted = $Arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
    $process = Start-Process -FilePath $Exe -ArgumentList $quoted -RedirectStandardOutput $Log -RedirectStandardError ($Log + '.err') -NoNewWindow -PassThru
    $null = $process.Handle   # without this, ExitCode reads back empty for a redirected process
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $process.Id -Force
        Clear-Tas
        throw "TIMEOUT after $TimeoutSeconds s: $($Arguments -join ' ')  (is the TAS GUI open? did TAS refuse the model? see *_error_log.txt under $OutDir)"
    }

    $process.WaitForExit()
    Clear-Tas
    return $process.ExitCode
}

function Invoke-Workflow {
    param([string]$Model, [string]$Route, [string]$Dir, [string]$Stem)
    if (Test-Path $Dir) { Remove-Item $Dir -Recurse -Force }
    Write-Host "== workflow $Model / $Route ..."
    $log = Join-Path (Split-Path $Dir) "$(Split-Path $Dir -Leaf)-run.log"
    $code = Invoke-Harness -Arguments @('workflow', $Model, $Dir, $Route, 'simulate', "name=$Stem", "weather=$WeatherFile", "weathername=$WeatherName") -Log $log
    Get-Content $log | Select-String '^route=|^weather:' | ForEach-Object { Write-Host "   $($_.Line)" }
    $tsd = Join-Path $Dir "$Stem.tsd"
    if ($code -ne 0 -or -not (Test-Path $tsd) -or (Get-Item $tsd).Length -lt 1000) {
        $errorLog = Join-Path $Dir "$($Stem)_error_log.txt"
        if (Test-Path $errorLog) { Get-Content $errorLog | ForEach-Object { Write-Host "   TAS: $_" } }
        throw "the $Model / $Route run did not produce a simulated TSD (exit $code)"
    }
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
if (-not (Test-Path $Exe)) { throw "harness not built: $Exe" }

$stem = 'LoadSensitive'
$failed = $false

foreach ($model in $Models) {
    $root = Join-Path $OutDir $model
    New-Item -ItemType Directory -Force $root | Out-Null
    Write-Host "######## $model"

    Invoke-Workflow -Model $model -Route 'gbxml' -Dir (Join-Path $root 'gbxml') -Stem $stem
    Invoke-Workflow -Model $model -Route 'direct' -Dir (Join-Path $root 'direct') -Stem $stem

    $gbxml = Join-Path $root "gbxml\$stem"
    $direct = Join-Path $root "direct\$stem"

    Write-Host '== TBD inputs ...'
    $inputsCode = Invoke-Harness -Arguments @('inputs', "$gbxml.tbd", "$direct.tbd", (Join-Path $root 'inputs')) -Log (Join-Path $root 'inputs.log')
    Get-Content (Join-Path $root 'inputs.log') | Select-Object -First 8 | ForEach-Object { Write-Host "   $_" }

    Write-Host '== surface properties and shade proportions the simulation reads (deep) ...'
    $deepCode = Invoke-Harness -Arguments @('deep', "$gbxml.tbd", "$direct.tbd", (Join-Path $root 'deep.txt')) -Log (Join-Path $root 'deep.log')
    Get-Content (Join-Path $root 'deep.log') | Select-Object -First 8 | ForEach-Object { Write-Host "   $_" }

    Write-Host '== simulation: every hourly zone series, gbXML vs direct ...'
    $tsdCode = Invoke-Harness -Arguments @('tsd', "$gbxml.tsd", "$direct.tsd", (Join-Path $root 'tsd'), 'gbXML', 'direct', $Tolerance.ToString([Globalization.CultureInfo]::InvariantCulture)) -Log (Join-Path $root 'tsd.log')
    Get-Content (Join-Path $root 'tsd.log') | Select-String 'series differ|BEYOND TOLERANCE|largest hourly' | ForEach-Object { Write-Host "   $($_.Line)" }

    Write-Host '== SAM models handed back ...'
    $modelsCode = Invoke-Harness -Arguments @('models', "$gbxml.result.json", "$direct.result.json", (Join-Path $root 'models')) -Log (Join-Path $root 'models.log')
    Get-Content (Join-Path $root 'models.log') | Select-String 'differences:|UNRESOLVED|simulation outputs' | ForEach-Object { Write-Host "   $($_.Line)" }

    if ($Control) {
        Invoke-Workflow -Model $model -Route 'gbxml' -Dir (Join-Path $root 'gbxml-control') -Stem $stem
        Write-Host '== control: the gbXML route against itself (noise floor) ...'
        $controlCode = Invoke-Harness -Arguments @('tsd', "$gbxml.tsd", (Join-Path $root "gbxml-control\$stem.tsd"), (Join-Path $root 'control-tsd'), 'gbXML', 'gbXML-again', $Tolerance.ToString([Globalization.CultureInfo]::InvariantCulture)) -Log (Join-Path $root 'control-tsd.log')
        Get-Content (Join-Path $root 'control-tsd.log') | Select-String 'series differ|largest hourly' | ForEach-Object { Write-Host "   $($_.Line)" }
    }

    if ($OrderControl) {
        # The same building with the panels added in other orders: TAS creates its surfaces in another order. Through ONE route that is that
        # route's own order sensitivity - the yardstick for the difference between the routes above. Two further orderings per route.
        $tol = $Tolerance.ToString([Globalization.CultureInfo]::InvariantCulture)
        $orderArgs = @('tsd-order', (Join-Path $root 'order-noise'), "$gbxml.tsd", "$direct.tsd")
        foreach ($order in 'shuffled', 'rotated') {
            $variant = "$model-$order"
            Invoke-Workflow -Model $variant -Route 'gbxml' -Dir (Join-Path $root "gbxml-$order") -Stem $stem
            Invoke-Workflow -Model $variant -Route 'direct' -Dir (Join-Path $root "direct-$order") -Stem $stem
            $orderArgs += (Join-Path $root "gbxml-$order\$stem.tsd")
            $orderArgs += (Join-Path $root "direct-$order\$stem.tsd")
            foreach ($route in 'gbxml', 'direct') {
                Write-Host "== order control: $route with the panels $order, against $route as first run ..."
                $null = Invoke-Harness -Arguments @('tsd', (Join-Path $root "$route\$stem.tsd"), (Join-Path $root "$route-$order\$stem.tsd"), (Join-Path $root "order-$route-$order"), $route, "$route-$order", $tol) -Log (Join-Path $root "order-$route-$order.log")
                Get-Content (Join-Path $root "order-$route-$order.log") | Select-String 'series differ|largest hourly' | ForEach-Object { Write-Host "   $($_.Line)" }
            }
        }

        Write-Host '== route difference against order sensitivity ...'
        $noiseCode = Invoke-Harness -Arguments $orderArgs -Log (Join-Path $root 'order-noise.log')
        Get-Content (Join-Path $root 'order-noise.log') | Select-String 'series compared|largest route|UNEXPLAINED' | ForEach-Object { Write-Host "   $($_.Line)" }
    }

    Write-Host "$model : inputs exit=$inputsCode (0 = no UNEXPECTED difference)  tsd exit=$tsdCode (0 = within tolerance)  deep exit=$deepCode  models exit=$modelsCode"
    # With the order control the simulation verdict is the one against order sensitivity; without it, the fixed tolerance.
    $simulationOk = if ($OrderControl) { $noiseCode -eq 0 } else { $tsdCode -eq 0 }
    if ($inputsCode -ne 0 -or -not $simulationOk) { $failed = $true }
}

exit $(if ($failed) { 1 } else { 0 })
