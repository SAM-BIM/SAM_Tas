# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
#
# S08 PR-B: the independent Tas native-input read-back, step by step, each step in its own process under a watchdog.
#
#   calibrate  write the calibration models; export each on the contract route (Direct, UseWidths=false, Sizing=false,
#              Simulate=false, AddIZAMs=false); reopen every TBD/T3D in a fresh process (twice, as the reader control);
#              export the box twice (writer control); run the ONE approved P-ALIGN 365-day simulation and the ONE approved
#              informational Sizing=true export; derive the calibration findings and a candidate rule table.
#   compare    compare every calibration run under the committed rule table (calibration-rules.json beside the exe).
#   negative   apply each licensed negative case to a DISPOSABLE copy, reopen it in a fresh process and compare;
#              every case must end non-PASS, and the originals must hash the same before and after.
#
# Usage (Windows PowerShell 5.1, licensed Tas, Tas GUI closed, short output path):
#   powershell -ExecutionPolicy Bypass -File Run-S08-ReadBack.ps1 -Phase calibrate -Root C:\TasOut\s08b\run1
#   powershell -ExecutionPolicy Bypass -File Run-S08-ReadBack.ps1 -Phase compare   -Root C:\TasOut\s08b\run1
#   powershell -ExecutionPolicy Bypass -File Run-S08-ReadBack.ps1 -Phase negative  -Root C:\TasOut\s08b\run1
param(
    [Parameter(Mandatory = $true)][ValidateSet('calibrate', 'compare', 'negative')][string]$Phase,
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$Exe = '',
    [int]$TimeoutSeconds = 1200,
    # Dry run of the calibrate phase without the two owner-approved native executions (P-ALIGN, Sizing=true), so a
    # script problem can never cost a second approved simulation.
    [switch]$SkipApprovedExecutions,
    # With -SkipApprovedExecutions: copy the persisted outputs of the earlier approved executions (calib-align with its
    # TSD, calib-box-sizing) from this root and only READ them again here, so a re-run at a later commit needs no second
    # simulation or sizing.
    [string]$ReuseApprovedFrom = ''
)

$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 has no $PSScriptRoot in parameter defaults, so the harness path is resolved here.
if (-not $Exe) { $Exe = Join-Path $PSScriptRoot 'bin\Debug\direct-t3d-validation.exe' }
$tasProcesses = 'ProfGate', 'TBD', 'TSD', 'TAS3D', 'TPD', 'TWD', 'TCD'

if (-not (Test-Path $Exe)) { throw "harness not built: $Exe" }
if ($Root.Length -gt 40) { throw "use a short output path (TAS fails on long paths): $Root" }
New-Item -ItemType Directory -Force $Root | Out-Null
$log = Join-Path $Root "watchdog-$Phase.json"
$steps = New-Object System.Collections.Generic.List[object]

function Clear-Tas {
    $left = @(Get-Process -Name $tasProcesses -ErrorAction SilentlyContinue)
    foreach ($p in $left) { try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch {} }
    return ($left | ForEach-Object { "$($_.Name):$($_.Id)" }) -join ','
}

# One step = one process. The watchdog records the exit code, the time, a timeout (the process tree is killed) and any
# Tas process the step left behind; a successful exit is never taken as a PASS by itself.
function Step([string]$Name, [string[]]$Arguments, [int[]]$Expect = @(0)) {
    $before = Clear-Tas
    $out = Join-Path $Root "logs\$Name.out.txt"
    $err = Join-Path $Root "logs\$Name.err.txt"
    New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
    $quoted = $Arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $Exe -ArgumentList $quoted -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
    $null = $p.Handle # Windows PowerShell 5.1 reports no ExitCode unless the handle is taken at once
    $timedOut = -not $p.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) { try { & taskkill /PID $p.Id /T /F | Out-Null } catch {} }
    $p.WaitForExit()
    $sw.Stop()
    $strays = Clear-Tas
    $code = if ($timedOut) { -1 } else { $p.ExitCode }
    $entry = [ordered]@{
        step = $Name; arguments = ($quoted -join ' '); exitCode = $code; expected = ($Expect -join ','); asExpected = ((-not $timedOut) -and ($Expect -contains $code))
        seconds = [Math]::Round($sw.Elapsed.TotalSeconds, 1); timedOut = $timedOut; strayBefore = $before; strayAfter = $strays; pid = $p.Id
        startedUtc = (Get-Date).ToUniversalTime().AddSeconds(-$sw.Elapsed.TotalSeconds).ToString('o'); stdout = $out
    }
    $steps.Add($entry)
    $steps | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 $log
    Write-Host ("{0,-34} exit {1,4} {2,7}s {3}{4}" -f $Name, $code, $entry.seconds, $(if ($entry.asExpected) { 'ok' } else { 'UNEXPECTED' }), $(if ($strays) { " strays: $strays" } else { '' }))
    Get-Content $out -Tail 3 | ForEach-Object { Write-Host "    $_" }
    return $entry
}

function Provenance {
    $git = 'C:\Program Files\Git\cmd\git.exe'
    if (-not (Test-Path $git)) { $git = 'git' }
    $repos = [ordered]@{}
    foreach ($name in 'SAM_Tas', 'SAM', 'SAM_gbXML') {
        $dir = Join-Path (Split-Path (Split-Path $PSScriptRoot)) "..\$name"
        $dir = [IO.Path]::GetFullPath($dir)
        if (Test-Path $dir) {
            $repos[$name] = [ordered]@{ head = (& $git -C $dir rev-parse HEAD); branch = (& $git -C $dir rev-parse --abbrev-ref HEAD); dirty = @(& $git -C $dir status --porcelain).Count }
        }
    }
    [ordered]@{
        utc = (Get-Date).ToUniversalTime().ToString('o'); machine = $env:COMPUTERNAME; phase = $Phase
        exe = $Exe; exeSha256 = (Get-FileHash $Exe -Algorithm SHA256).Hash.ToLowerInvariant()
        rules = (Join-Path (Split-Path $Exe) 'calibration-rules.json'); rulesSha256 = (Get-FileHash (Join-Path (Split-Path $Exe) 'calibration-rules.json') -Algorithm SHA256).Hash.ToLowerInvariant()
        repositories = $repos; powershell = $PSVersionTable.PSVersion.ToString()
    } | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $Root "provenance-$Phase.json")
}

Provenance
$models = Join-Path $Root 'models'
$weather = Join-Path $models 'calib-weather.json'

if ($Phase -eq 'calibrate') {
    Step 'models' @('readback-models', '--out', $models) | Out-Null
    foreach ($m in 'calib-box', 'calib-two') {
        Step "export-$m" @('readback-export', '--model', (Join-Path $models "$m.json"), '--weather', $weather, '--out', (Join-Path $Root $m), '--label', $m) | Out-Null
        Step "read-$m" @('readback-read', '--tbd', (Join-Path $Root "$m\model.tbd"), '--t3d', (Join-Path $Root "$m\model.t3d"), '--out', (Join-Path $Root "$m\snapshot.json")) | Out-Null
        Step "reread-$m" @('readback-read', '--tbd', (Join-Path $Root "$m\model.tbd"), '--t3d', (Join-Path $Root "$m\model.t3d"), '--out', (Join-Path $Root "$m\snapshot.reread.json")) | Out-Null
        Step "diff-reader-$m" @('readback-diff', '--a', (Join-Path $Root "$m\snapshot.json"), '--b', (Join-Path $Root "$m\snapshot.reread.json"), '--purpose', 'reader control: the same TBD read in two fresh processes', '--out', (Join-Path $Root "$m\diff-reader.json")) | Out-Null
    }

    # Writer control: the same frozen model exported again into a fresh directory.
    Step 'export-calib-box-repeat' @('readback-export', '--model', (Join-Path $models 'calib-box.json'), '--weather', $weather, '--out', (Join-Path $Root 'calib-box-repeat'), '--label', 'calib-box-repeat') | Out-Null
    Step 'read-calib-box-repeat' @('readback-read', '--tbd', (Join-Path $Root 'calib-box-repeat\model.tbd'), '--t3d', (Join-Path $Root 'calib-box-repeat\model.t3d'), '--out', (Join-Path $Root 'calib-box-repeat\snapshot.json')) | Out-Null
    Step 'diff-writer-calib-box' @('readback-diff', '--a', (Join-Path $Root 'calib-box\snapshot.json'), '--b', (Join-Path $Root 'calib-box-repeat\snapshot.json'), '--purpose', 'writer control: the same frozen model exported twice', '--out', (Join-Path $Root 'calib-box-repeat\diff-writer.json')) | Out-Null

    if (-not $SkipApprovedExecutions) {
        # P-ALIGN: the ONE owner-approved small synthetic 365-day simulation.
        Step 'export-calib-align-simulate' @('readback-export', '--model', (Join-Path $models 'calib-align.json'), '--weather', $weather, '--out', (Join-Path $Root 'calib-align'), '--label', 'P-ALIGN', '--simulate') | Out-Null
        Step 'read-calib-align' @('readback-read', '--tbd', (Join-Path $Root 'calib-align\model.tbd'), '--t3d', (Join-Path $Root 'calib-align\model.t3d'), '--out', (Join-Path $Root 'calib-align\snapshot.json')) | Out-Null
        $tsd = Join-Path $Root 'calib-align\model.tsd'
        if ((Test-Path $tsd) -and ((Get-Item $tsd).Length -ge 1024)) {
            Step 'tsd-calib-align' @('readback-tsd', '--tsd', $tsd, '--out', (Join-Path $Root 'calib-align\tsd.json')) | Out-Null
        } else {
            Write-Host "P-ALIGN TSD missing or a stub; it is not opened"
        }

        # The ONE owner-approved informational Sizing=true export, compared with the Sizing=false box.
        Step 'export-calib-box-sizing' @('readback-export', '--model', (Join-Path $models 'calib-box.json'), '--weather', $weather, '--out', (Join-Path $Root 'calib-box-sizing'), '--label', 'calib-box Sizing=true (informational)', '--sizing') | Out-Null
        Step 'read-calib-box-sizing' @('readback-read', '--tbd', (Join-Path $Root 'calib-box-sizing\model.tbd'), '--t3d', (Join-Path $Root 'calib-box-sizing\model.t3d'), '--out', (Join-Path $Root 'calib-box-sizing\snapshot.json')) | Out-Null
        Step 'diff-sizing-calib-box' @('readback-diff', '--a', (Join-Path $Root 'calib-box\snapshot.json'), '--b', (Join-Path $Root 'calib-box-sizing\snapshot.json'), '--purpose', 'informational: what Sizing=true changes in the persisted inputs', '--out', (Join-Path $Root 'calib-box-sizing\diff-sizing.json')) | Out-Null
    } elseif ($ReuseApprovedFrom) {
        foreach ($run in 'calib-align', 'calib-box-sizing') {
            $from = Join-Path $ReuseApprovedFrom $run
            if (-not (Test-Path (Join-Path $from 'model.tbd'))) { throw "no approved $run output in $ReuseApprovedFrom" }
            New-Item -ItemType Directory -Force (Join-Path $Root $run) | Out-Null
            foreach ($f in 'input.json', 'weather.json', 'export.json', 'model.tbd', 'model.t3d', 'model.tsd') {
                if (Test-Path (Join-Path $from $f)) { Copy-Item (Join-Path $from $f) (Join-Path $Root "$run\$f") }
            }
        }

        Step 'reread-approved-calib-align' @('readback-read', '--tbd', (Join-Path $Root 'calib-align\model.tbd'), '--t3d', (Join-Path $Root 'calib-align\model.t3d'), '--out', (Join-Path $Root 'calib-align\snapshot.json')) | Out-Null
        Step 'reread-approved-tsd-calib-align' @('readback-tsd', '--tsd', (Join-Path $Root 'calib-align\model.tsd'), '--out', (Join-Path $Root 'calib-align\tsd.json')) | Out-Null
        Step 'reread-approved-calib-box-sizing' @('readback-read', '--tbd', (Join-Path $Root 'calib-box-sizing\model.tbd'), '--t3d', (Join-Path $Root 'calib-box-sizing\model.t3d'), '--out', (Join-Path $Root 'calib-box-sizing\snapshot.json')) | Out-Null
        Step 'diff-sizing-calib-box' @('readback-diff', '--a', (Join-Path $Root 'calib-box\snapshot.json'), '--b', (Join-Path $Root 'calib-box-sizing\snapshot.json'), '--purpose', 'informational: what Sizing=true changes in the persisted inputs (the approved run, re-read)', '--out', (Join-Path $Root 'calib-box-sizing\diff-sizing.json')) | Out-Null
    }

    $alignArgs = @()
    if (Test-Path (Join-Path $Root 'calib-align\tsd.json')) { $alignArgs = @('--align-tsd', (Join-Path $Root 'calib-align\tsd.json')) }
    Step 'calibrate' (@('readback-calibrate', '--runs', ("calib-box=" + (Join-Path $Root 'calib-box') + ";calib-two=" + (Join-Path $Root 'calib-two')), '--out', (Join-Path $Root 'calibration')) + $alignArgs) | Out-Null
}

if ($Phase -eq 'compare') {
    # 0 is PASS only; every other status code is a legitimate, recorded outcome for a calibration model.
    $any = @(0, 10, 11, 12, 13, 14)
    foreach ($m in 'calib-box', 'calib-two', 'calib-box-repeat') {
        Step "compare-$m" @('readback-compare', '--run', (Join-Path $Root $m), '--fixture-id', $m, '--out', (Join-Path $Root "$m\compare")) $any | Out-Null
        Step "compare-provisional-$m" @('readback-compare', '--run', (Join-Path $Root $m), '--fixture-id', $m, '--rule-set', 'provisional', '--out', (Join-Path $Root "$m\compare-provisional")) $any | Out-Null
    }

    Step 'compare-calib-box-sizing' @('readback-compare', '--run', (Join-Path $Root 'calib-box-sizing'), '--fixture-id', 'calib-box-sizing', '--purpose', 'informational Sizing=true run (not the contract route)', '--out', (Join-Path $Root 'calib-box-sizing\compare')) $any | Out-Null
}

if ($Phase -eq 'negative') {
    $cases = [ordered]@{
        'swap-space-guid' = 'calib-two'; 'flip-partition' = 'calib-two'; 'layer-conductivity' = 'calib-box'; 'zero-lighting-factor' = 'calib-box'
        'nan-infiltration' = 'calib-box'; 'shift-heating-slot' = 'calib-box'; 'add-izam' = 'calib-box'
    }

    foreach ($case in $cases.Keys) {
        $source = Join-Path $Root $cases[$case]
        $target = Join-Path $Root "neg-$case"
        $originals = Get-ChildItem $source -File | Where-Object { $_.Name -in 'input.json', 'weather.json', 'export.json', 'model.tbd', 'model.t3d' } | ForEach-Object { "$($_.Name)=$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }
        Step "negative-$case" @('readback-negative', '--run', $source, '--case', $case, '--out', $target) | Out-Null
        Step "read-neg-$case" @('readback-read', '--tbd', (Join-Path $target 'model.tbd'), '--t3d', (Join-Path $target 'model.t3d'), '--out', (Join-Path $target 'snapshot.json')) | Out-Null
        foreach ($set in 'interim', 'provisional') {
            # A negative case must NOT pass: 0 is the only unexpected exit.
            Step "compare-neg-$case-$set" @('readback-compare', '--run', $target, '--fixture-id', "neg-$case", '--rule-set', $set, '--purpose', "negative check '$case' on a disposable copy", '--out', (Join-Path $target "compare-$set")) @(10, 11, 12, 13, 14) | Out-Null
        }

        $after = Get-ChildItem $source -File | Where-Object { $_.Name -in 'input.json', 'weather.json', 'export.json', 'model.tbd', 'model.t3d' } | ForEach-Object { "$($_.Name)=$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }
        $same = (($originals -join ';') -eq ($after -join ';'))
        Write-Host ("  originals of {0} unchanged: {1}" -f $cases[$case], $same)
        $steps.Add([ordered]@{ step = "originals-$case"; source = $source; before = $originals; after = $after; unchanged = $same; asExpected = $same })
        $steps | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 $log
    }
}

$bad = @($steps | Where-Object { -not $_.asExpected })
Write-Host ("{0}: {1} step(s), {2} not as expected; log {3}" -f $Phase, $steps.Count, $bad.Count, $log)
if ($bad.Count -gt 0) { exit 1 } else { exit 0 }
