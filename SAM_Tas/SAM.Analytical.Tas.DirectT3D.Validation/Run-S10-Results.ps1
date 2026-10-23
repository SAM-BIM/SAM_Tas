# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
#
# S10: Tas results round-trip validation, step by step, each step in its own process under a watchdog.
#
#   run      (licensed; owner-approved OD-1) check the committed fixtures (ReadBack/Fixtures/SHA256SUMS.txt) and the
#            weather EPW against their hashes; refuse a non-empty run folder; then per fixture: ONE benchmark-tas
#            simulation on the unchanged producer route, the independent reference read (results-read: TSD + TBD, fresh
#            process, read-only), the TBD design-day coverage (results-read-design, its own fresh process, read-only),
#            the subject capture (results-subject: saved model, production model-level converter on
#            the same TSD, B1a; fresh process, read-only), the COM-free comparison and the COM-free negative controls on
#            copies. Two fixtures = two simulations; nothing else is executed.
#   offline  (COM-free) recompute the comparison and the negative controls of an existing root into compare-replay/ and
#            negatives-replay/, so a later commit can be checked against the same reads without Tas.
#
# Usage (Windows PowerShell 5.1, licensed Tas, Tas GUI closed, short output path):
#   powershell -ExecutionPolicy Bypass -File Run-S10-Results.ps1 -Phase run -Root C:\TasOut\s10\acc1 -WeatherEpw C:\TasOut\s08c\weather\budapest.epw
#   powershell -ExecutionPolicy Bypass -File Run-S10-Results.ps1 -Phase offline -Root C:\TasOut\s10\acc1
param(
    [Parameter(Mandatory = $true)][ValidateSet('run', 'offline')][string]$Phase,
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$Exe = '',
    [string]$BenchmarkExe = '',
    [string]$EngineVersion = '2.0.0.1',
    [int]$TimeoutSeconds = 3600,
    [string]$WeatherEpw = '',
    [string]$WeatherEpwSha256 = '472ffb4e2f10867600949a392ce46891fc1bd05f2fc8d8917a99bca7d26209c3'
)

$ErrorActionPreference = 'Stop'
if (-not $Exe) { $Exe = Join-Path $PSScriptRoot 'bin\Release\direct-t3d-validation.exe' }
if (-not $BenchmarkExe) { $BenchmarkExe = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\build\benchmark-tas.exe')) }
$tasProcesses = 'ProfGate', 'TBD', 'TSD', 'TAS3D', 'TPD', 'TWD', 'TCD'
$fixtures = 's08-fix-box', 's08-fix-two'
$fixtureDir = Join-Path $PSScriptRoot 'ReadBack\Fixtures'

if (-not (Test-Path $Exe)) { throw "harness not built: $Exe" }
if ($Root.Length -gt 40) { throw "use a short output path (TAS fails on long paths): $Root" }
if ($Phase -eq 'run') {
    if (-not (Test-Path $BenchmarkExe)) { throw "benchmark-tas not built: $BenchmarkExe" }
    if ((Test-Path $Root) -and @(Get-ChildItem -Force $Root).Count -gt 0) { throw "refusing a non-empty run folder: $Root" }
}
elseif (-not (Test-Path $Root)) { throw "no run folder: $Root" }

New-Item -ItemType Directory -Force $Root | Out-Null
$log = Join-Path $Root "watchdog-$Phase.json"
$steps = New-Object System.Collections.Generic.List[object]

function Clear-Tas {
    $left = @(Get-Process -Name $tasProcesses -ErrorAction SilentlyContinue)
    foreach ($p in $left) { try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch {} }
    return ($left | ForEach-Object { "$($_.Name):$($_.Id)" }) -join ','
}

# One step = one process. The watchdog records the exit code, the time, a timeout (the process tree is killed) and any
# Tas process the step left behind; a successful exit is never taken as a PASS by itself (the reports decide).
function Step([string]$Name, [string]$File, [string[]]$Arguments, [int[]]$Expect = @(0)) {
    $before = Clear-Tas
    $out = Join-Path $Root "logs\$Name.out.txt"
    $err = Join-Path $Root "logs\$Name.err.txt"
    New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
    $quoted = $Arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $File -ArgumentList $quoted -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
    $null = $p.Handle # Windows PowerShell 5.1 reports no ExitCode unless the handle is taken at once
    $timedOut = -not $p.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) { try { & taskkill /PID $p.Id /T /F | Out-Null } catch {} }
    $p.WaitForExit()
    $sw.Stop()
    $strays = Clear-Tas
    $code = if ($timedOut) { -1 } else { $p.ExitCode }
    $entry = [ordered]@{
        step = $Name; file = $File; arguments = ($quoted -join ' '); exitCode = $code; expected = ($Expect -join ','); asExpected = ((-not $timedOut) -and ($Expect -contains $code))
        seconds = [Math]::Round($sw.Elapsed.TotalSeconds, 1); timedOut = $timedOut; strayBefore = $before; strayAfter = $strays; pid = $p.Id
        startedUtc = (Get-Date).ToUniversalTime().AddSeconds(-$sw.Elapsed.TotalSeconds).ToString('o'); stdout = $out
    }
    $steps.Add($entry)
    $steps | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 $log
    Write-Host ("{0,-34} exit {1,4} {2,7}s {3}{4}" -f $Name, $code, $entry.seconds, $(if ($entry.asExpected) { 'ok' } else { 'UNEXPECTED' }), $(if ($strays) { " strays: $strays" } else { '' }))
    Get-Content $out -Tail 3 | ForEach-Object { Write-Host "    $_" }
    return $entry
}

function Sha([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    return (Get-FileHash $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Provenance {
    $git = 'C:\Program Files\Git\cmd\git.exe'
    if (-not (Test-Path $git)) { $git = 'git' }
    $repos = [ordered]@{}
    foreach ($name in 'SAM_Tas', 'SAM', 'SAM_gbXML', 'SAM_Validation') {
        $dir = [IO.Path]::GetFullPath((Join-Path (Split-Path (Split-Path $PSScriptRoot)) "..\$name"))
        if (Test-Path $dir) {
            $repos[$name] = [ordered]@{ head = (& $git -C $dir rev-parse HEAD); branch = (& $git -C $dir rev-parse --abbrev-ref HEAD); dirty = @(& $git -C $dir status --porcelain).Count }
        }
    }
    $rules = Join-Path (Split-Path $Exe) 'calibration-rules.json'
    [ordered]@{
        utc = (Get-Date).ToUniversalTime().ToString('o'); machine = $env:COMPUTERNAME; phase = $Phase
        exe = $Exe; exeSha256 = (Sha $Exe); harnessDll = (Join-Path (Split-Path $Exe) 'direct-t3d-validation.dll'); harnessDllSha256 = (Sha (Join-Path (Split-Path $Exe) 'direct-t3d-validation.dll'))
        benchmarkExe = $BenchmarkExe; benchmarkExeSha256 = (Sha $BenchmarkExe); benchmarkDll = (Join-Path (Split-Path $BenchmarkExe) 'benchmark-tas.dll'); benchmarkDllSha256 = (Sha (Join-Path (Split-Path $BenchmarkExe) 'benchmark-tas.dll'))
        rules = $rules; rulesSha256 = (Sha $rules); engineVersion = $EngineVersion
        repositories = $repos; powershell = $PSVersionTable.PSVersion.ToString()
    } | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $Root "provenance-$Phase.json")
}

Provenance

if ($Phase -eq 'run') {
    # The committed, owner-signed-off fixtures and the approved weather, hash-checked before anything runs.
    foreach ($line in (Get-Content (Join-Path $fixtureDir 'SHA256SUMS.txt') | Where-Object { $_.Trim() })) {
        $expected, $name = $line -split '\s+', 2
        $actual = Sha (Join-Path $fixtureDir $name.Trim())
        if ($actual -ne $expected.ToLowerInvariant()) { throw "fixture $name is $actual, expected $expected" }
    }
    if (-not $WeatherEpw -or -not (Test-Path $WeatherEpw)) { throw "-WeatherEpw <epw> is required for the run phase" }
    $actual = Sha $WeatherEpw
    if ($actual -ne $WeatherEpwSha256) { throw "weather $WeatherEpw is $actual, expected $WeatherEpwSha256" }
    [ordered]@{ fixtures = (Get-Content (Join-Path $fixtureDir 'SHA256SUMS.txt')); weather = $WeatherEpw; weatherSha256 = $actual; engineVersion = $EngineVersion } | ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8 (Join-Path $Root 'inputs.json')

    foreach ($m in $fixtures) {
        $run = Join-Path $Root $m
        New-Item -ItemType Directory -Force $run | Out-Null
        $tbd = Join-Path $run 'fixture.tbd'
        $tsd = Join-Path $run 'fixture.tsd'
        $samJson = Join-Path $run 'fixture.json'
        $b1a = Join-Path $run 'b1a.json'

        # The ONE approved simulation of this fixture: the unchanged producer route (gbXML, Sizing, AddIZAMs, UnmetHours, days 1-365).
        Step "simulate-$m" $BenchmarkExe @('--model', (Join-Path $fixtureDir "$m.json"), '--weather', $WeatherEpw, '--tbd', $tbd, '--out', $b1a, '--engine-version', $EngineVersion) | Out-Null
        $produced = [ordered]@{ tbd = (Sha $tbd); tsd = (Sha $tsd); samJson = (Sha $samJson); b1a = (Sha $b1a) }
        $produced | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $run 'produced.json')

        Step "read-$m" $Exe @('results-read', $tsd, $tbd, (Join-Path $run 'read')) | Out-Null
        Step "read-design-$m" $Exe @('results-read-design', $tbd, (Join-Path $run 'read')) | Out-Null
        Step "subject-$m" $Exe @('results-subject', $samJson, $tsd, $b1a, (Join-Path $run 'subject.json')) | Out-Null
        Step "compare-$m" $Exe @('results-compare', (Join-Path $run 'read'), (Join-Path $run 'subject.json'), (Join-Path $run 'compare')) | Out-Null
        Step "negatives-$m" $Exe @('results-negatives', (Join-Path $run 'read'), (Join-Path $run 'subject.json'), (Join-Path $run 'negatives'), 'expect=PASS') | Out-Null

        # The produced files must be unchanged by every read.
        $after = [ordered]@{ tbd = (Sha $tbd); tsd = (Sha $tsd); samJson = (Sha $samJson); b1a = (Sha $b1a) }
        $changed = @($produced.Keys | Where-Object { $produced[$_] -ne $after[$_] })
        [ordered]@{ before = $produced; after = $after; changed = $changed } | ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8 (Join-Path $run 'produced-after-reads.json')
        if ($changed.Count -gt 0) { Write-Host "ERROR: produced files changed during the reads: $($changed -join ', ')" }
    }

    # The fixtures themselves are unchanged.
    foreach ($line in (Get-Content (Join-Path $fixtureDir 'SHA256SUMS.txt') | Where-Object { $_.Trim() })) {
        $expected, $name = $line -split '\s+', 2
        if ((Sha (Join-Path $fixtureDir $name.Trim())) -ne $expected.ToLowerInvariant()) { Write-Host "ERROR: fixture $name changed during the run" }
    }
}

if ($Phase -eq 'offline') {
    foreach ($m in $fixtures) {
        $run = Join-Path $Root $m
        if (-not (Test-Path (Join-Path $run 'subject.json'))) { Write-Host "skip $m (no subject.json)"; continue }
        foreach ($d in 'compare-replay', 'negatives-replay') { if (Test-Path (Join-Path $run $d)) { throw "refusing to overwrite $run\$d" } }
        Step "compare-replay-$m" $Exe @('results-compare', (Join-Path $run 'read'), (Join-Path $run 'subject.json'), (Join-Path $run 'compare-replay')) | Out-Null
        Step "negatives-replay-$m" $Exe @('results-negatives', (Join-Path $run 'read'), (Join-Path $run 'subject.json'), (Join-Path $run 'negatives-replay'), 'expect=PASS') | Out-Null
    }
}

Write-Host "watchdog: $log"
