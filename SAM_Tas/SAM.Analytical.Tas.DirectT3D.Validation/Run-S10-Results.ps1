# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
#
# S10: Tas results round-trip validation, step by step, each step in its own process under a watchdog.
#
#   run      (licensed; owner-approved OD-1) preflight first, launching no engine: the committed fixtures
#            (ReadBack/Fixtures/SHA256SUMS.txt) and the weather EPW against their hashes, an empty run folder, and the
#            source provenance (below). Then per fixture: ONE benchmark-tas simulation on the unchanged producer route, the
#            independent reference read (results-read: TSD + TBD, fresh process, read-only), the TBD design-day coverage
#            (results-read-design, its own fresh process, read-only), the subject capture (results-subject: saved model,
#            production model-level converter on the same TSD, B1a; fresh process, read-only), the COM-free comparison and
#            the COM-free negative controls on copies. Two fixtures = at most two simulations; nothing else is executed.
#   offline  (COM-free) recompute the comparison and the negative controls of an existing root into compare-replay/ and
#            negatives-replay/, so a later commit can be checked against the same reads without Tas.
#
# Provenance (S10 revision-2 review R3): the binaries must come from Build-S10-Receipt.ps1, a controlled build of clean
# SAM and SAM_Tas checkouts that stamps each assembly with its commit and writes a receipt of the commits and the sha256
# of every .dll/.exe in the harness and benchmark-tas folders and of the SAM core libraries. Before any engine starts, the
# preflight here verifies, and never writes, that receipt:
#   - it exists, is s10-build-receipt/1 and names these checkouts and these harness/benchmark-tas paths;
#   - every file it lists has its recorded sha256, and no .dll/.exe it does not list sits beside the harness or
#     benchmark-tas (a copied or rebuilt artifact, however new, is refused);
#   - the SAM libraries the harness and benchmark-tas load equal SAM\build and the receipt's SAM build;
#   - each checkout is clean, at the receipt's commit or at a descendant whose difference from it is record-only (*.md):
#     a PR record commit after the tested build changes no build input. Any other change needs a rebuild.
# Timestamps stay as a freshness check: no artifact may be older than a tracked source file. benchmark-tas is given
# --sam-commit/--runner-commit = the receipt's build commits, which its stamps also carry. Anything missing, malformed or
# unproven stops the run before the first simulation.
#
# Failure control (review R4): a step is recorded as started before its process launches and as completed or
# terminated afterwards. A failed or timed-out simulation, a failed read or capture, a missing produced file, a file
# changed by a read, or any step exiting with an infrastructure code stops the run at once: no dependent step and no
# further fixture runs. The wrapper then exits 3 and status-run.json says why. A comparison verdict (FAIL 10, BLOCKED 11)
# or a negative matrix that is not all as intended (10) is a measured result, not an infrastructure failure: it is
# recorded and the run continues. A successful exit is never taken as a PASS by itself (the reports decide).
#
# Usage (Windows PowerShell 5.1, licensed Tas, Tas GUI closed, short output path):
#   powershell -ExecutionPolicy Bypass -File Build-S10-Receipt.ps1
#   powershell -ExecutionPolicy Bypass -File Run-S10-Results.ps1 -Phase run -Root C:\TasOut\s10\acc1 -WeatherEpw C:\TasOut\s08c\weather\budapest.epw
#   powershell -ExecutionPolicy Bypass -File Run-S10-Results.ps1 -Phase offline -Root C:\TasOut\s10\acc1
# -SamRepository, -RunnerRepository and -TasProcessNames default to this workspace and the Tas engine processes; the
# offline tests pass process doubles, temporary checkouts and a process name that matches nothing, so no Tas process is
# ever touched by a test.
param(
    [Parameter(Mandatory = $true)][ValidateSet('run', 'offline')][string]$Phase,
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$Exe = '',
    [string]$BenchmarkExe = '',
    [string]$EngineVersion = '2.0.0.1',
    [int]$TimeoutSeconds = 3600,
    [string]$WeatherEpw = '',
    [string]$WeatherEpwSha256 = '472ffb4e2f10867600949a392ce46891fc1bd05f2fc8d8917a99bca7d26209c3',
    [string]$SamRepository = '',
    [string]$RunnerRepository = '',
    [string]$BuildReceipt = '',
    [string[]]$TasProcessNames = @('ProfGate', 'TBD', 'TSD', 'TAS3D', 'TPD', 'TWD', 'TCD')
)

$ErrorActionPreference = 'Stop'
if (-not $Exe) { $Exe = Join-Path $PSScriptRoot 'bin\Release\direct-t3d-validation.exe' }
if (-not $BenchmarkExe) { $BenchmarkExe = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\build\benchmark-tas.exe')) }
if (-not $RunnerRepository) { $RunnerRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')) }
if (-not $SamRepository) { $SamRepository = [IO.Path]::GetFullPath((Join-Path $RunnerRepository '..\SAM')) }
if (-not $BuildReceipt) { $BuildReceipt = Join-Path (Split-Path $BenchmarkExe) 's10-build-receipt.json' }
$fixtures = 's08-fix-box', 's08-fix-two'
$fixtureDir = Join-Path $PSScriptRoot 'ReadBack\Fixtures'
$sourcePatterns = '*.cs', '*.csproj', '*.props', '*.targets', '*.resx', '*.xaml', '*.sln'

if (-not (Test-Path $Exe)) { throw "harness not built: $Exe" }
if ($Root.Length -gt 40) { throw "use a short output path (TAS fails on long paths): $Root" }
if ($Phase -eq 'run') {
    if (-not (Test-Path $BenchmarkExe)) { throw "benchmark-tas not built: $BenchmarkExe" }
    if ((Test-Path $Root) -and @(Get-ChildItem -Force $Root).Count -gt 0) { throw "refusing a non-empty run folder: $Root" }
}
elseif (-not (Test-Path $Root)) { throw "no run folder: $Root" }

New-Item -ItemType Directory -Force $Root | Out-Null
$log = Join-Path $Root "watchdog-$Phase.json"
$statusPath = Join-Path $Root "status-$Phase.json"
$steps = New-Object System.Collections.Generic.List[object]
$outcomes = New-Object System.Collections.Generic.List[object]

function Write-Status([string]$Infrastructure, [string]$Reason, [string]$FailedStep) {
    [ordered]@{
        phase = $Phase; utc = (Get-Date).ToUniversalTime().ToString('o'); infrastructure = $Infrastructure; reason = $Reason; failedStep = $FailedStep
        simulationsStarted = @($steps | Where-Object { $_.step -like 'simulate-*' }).Count
        outcomes = $outcomes; note = 'infrastructure = whether the run completed; measured verdicts are in outcomes and the reports, never in the wrapper exit'
    } | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 $statusPath
}

# Stops the run: nothing dependent and no further fixture is started. Exit 3 = infrastructure failure.
function Stop-Run([string]$Reason, [string]$FailedStep = '') {
    Write-Status 'failed' $Reason $FailedStep
    Write-Host "STOPPED (infrastructure): $Reason"
    Write-Host "status: $statusPath"
    exit 3
}

function Clear-Tas {
    if ($Phase -ne 'run') { return '' }
    $left = @(Get-Process -Name $TasProcessNames -ErrorAction SilentlyContinue)
    foreach ($p in $left) { try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch {} }
    return ($left | ForEach-Object { "$($_.Name):$($_.Id)" }) -join ','
}

function Save-Watchdog { $steps | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 $log }

# One step = one process. The entry is persisted as started before the launch, then completed (exit code, time) or
# terminated (timeout: the process tree is killed), so an interrupted run never loses a started step.
function Step([string]$Name, [string]$File, [string[]]$Arguments, [int[]]$Expect = @(0)) {
    $before = Clear-Tas
    $out = Join-Path $Root "logs\$Name.out.txt"
    $err = Join-Path $Root "logs\$Name.err.txt"
    New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
    $quoted = $Arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
    $entry = [ordered]@{
        step = $Name; state = 'started'; file = $File; arguments = ($quoted -join ' '); exitCode = $null; expected = ($Expect -join ','); asExpected = $false
        seconds = $null; timedOut = $false; strayBefore = $before; strayAfter = $null; pid = $null
        startedUtc = (Get-Date).ToUniversalTime().ToString('o'); stdout = $out
    }
    $steps.Add($entry)
    Save-Watchdog
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $File -ArgumentList $quoted -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
    $null = $p.Handle # Windows PowerShell 5.1 reports no ExitCode unless the handle is taken at once
    $entry.pid = $p.Id
    Save-Watchdog
    $timedOut = -not $p.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) { try { & taskkill /PID $p.Id /T /F | Out-Null } catch {} }
    $p.WaitForExit()
    $sw.Stop()
    $entry.strayAfter = Clear-Tas
    $entry.exitCode = if ($timedOut) { -1 } else { $p.ExitCode }
    $entry.state = if ($timedOut) { 'terminated' } else { 'completed' }
    $entry.timedOut = $timedOut
    $entry.asExpected = (-not $timedOut) -and ($Expect -contains $entry.exitCode)
    $entry.seconds = [Math]::Round($sw.Elapsed.TotalSeconds, 1)
    Save-Watchdog
    Write-Host ("{0,-34} exit {1,4} {2,7}s {3}{4}" -f $Name, $entry.exitCode, $entry.seconds, $(if ($entry.asExpected) { 'ok' } else { 'UNEXPECTED' }), $(if ($entry.strayAfter) { " strays: $($entry.strayAfter)" } else { '' }))
    if (Test-Path $out) { Get-Content $out -Tail 3 | ForEach-Object { Write-Host "    $_" } }
    return $entry
}

# A step whose exit is not one of its expected codes is an infrastructure failure: stop now.
function Require([string]$Name, [string]$File, [string[]]$Arguments, [int[]]$Expect = @(0)) {
    $entry = Step $Name $File $Arguments $Expect
    if (-not $entry.asExpected) {
        $how = if ($entry.timedOut) { "timed out after $TimeoutSeconds s and was terminated" } else { "exited $($entry.exitCode) (expected $($Expect -join ' or '))" }
        Stop-Run "$Name $how" $Name
    }
    return $entry
}

# SHA-256 through .NET (review R5): in Windows PowerShell 5.1 Get-FileHash is a script function of the Utility module's
# psm1, available only through module autoload along the PSModulePath inherited from the parent host (PowerShell 7 on
# the hosted runner, where every runner test failed on it). The stream digest needs no module.
function Sha([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream)) -replace '-', '').ToLowerInvariant() } finally { $sha.Dispose() }
    }
    finally { $stream.Dispose() }
}

$git = 'C:\Program Files\Git\cmd\git.exe'
if (-not (Test-Path $git)) { $git = 'git' }

# git with its stderr discarded; Windows PowerShell 5.1 would otherwise turn a stderr line into a terminating error.
function Git([string]$Directory) {
    $ErrorActionPreference = 'Continue'
    & $git -C $Directory @args 2>$null
}

# A clean checkout, bound to the commit the build receipt says was built (-Built): HEAD itself, or a descendant whose
# difference from it is record-only (*.md). Every artifact must also be at least as new as every tracked source file (a
# later checkout, edit or revert of a source file makes it newer than the build and is refused).
function Resolve-Source([string]$Name, [string]$Directory, [string[]]$Artifacts, [string]$Built) {
    $result = [ordered]@{ name = $Name; directory = $Directory; head = $null; branch = $null; dirty = $null; built = $null; recordOnly = @(); newestSource = $null; newestSourceUtc = $null; artifacts = @(); problems = @() }
    if (-not (Test-Path (Join-Path $Directory '.git'))) {
        $result.problems += "$Directory is not a git checkout"
        return $result
    }

    $head = (Git $Directory rev-parse HEAD | Out-String).Trim()
    if ($head -notmatch '^[0-9a-f]{40}$') { $result.problems += "HEAD of $Directory is not a full commit SHA ('$head')" } else { $result.head = $head }
    $result.branch = (Git $Directory rev-parse --abbrev-ref HEAD | Out-String).Trim()
    $status = @(Git $Directory status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { $result.problems += "git status failed in $Directory" }
    $result.dirty = $status.Count
    if ($status.Count -gt 0) { $result.problems += "$Directory has $($status.Count) uncommitted or untracked change(s), e.g. $($status[0]): the built artifacts cannot be bound to HEAD" }

    if ($Built -notmatch '^[0-9a-f]{40}$') { $result.problems += "the build receipt gives no $Name commit" }
    elseif ($result.head) {
        $result.built = $Built
        if ($Built -ne $result.head) {
            Git $Directory merge-base --is-ancestor $Built $result.head | Out-Null
            if ($LASTEXITCODE -ne 0) { $result.problems += "$Directory was built at $Built, which is not HEAD $($result.head) nor an ancestor of it: rebuild" }
            else {
                $changed = @(Git $Directory diff --name-only $Built $result.head)
                if ($LASTEXITCODE -ne 0) { $result.problems += "git diff $Built $($result.head) failed in $Directory" }
                $inputs = @($changed | Where-Object { $_ -notlike '*.md' })
                if ($inputs.Count -gt 0) { $result.problems += "$Directory changed $($inputs.Count) build input(s) since the receipt's build $Built, e.g. $($inputs[0]): rebuild" }
                else { $result.recordOnly = $changed }
            }
        }
    }

    $newest = $null
    foreach ($file in @(Git $Directory ls-files '--' @sourcePatterns)) {
        $item = Get-Item -LiteralPath (Join-Path $Directory $file) -ErrorAction SilentlyContinue
        if ($item -and ($newest -eq $null -or $item.LastWriteTimeUtc -gt $newest.LastWriteTimeUtc)) { $newest = $item }
    }

    if ($newest -eq $null) { $result.problems += "$Directory has no tracked source file" }
    else {
        $result.newestSource = $newest.FullName
        $result.newestSourceUtc = $newest.LastWriteTimeUtc.ToString('o')
    }

    foreach ($artifact in $Artifacts) {
        $item = Get-Item -LiteralPath $artifact -ErrorAction SilentlyContinue
        if (-not $item) {
            $result.problems += "artifact missing: $artifact"
            continue
        }

        $result.artifacts += [ordered]@{ path = $item.FullName; sha256 = (Sha $item.FullName); lastWriteUtc = $item.LastWriteTimeUtc.ToString('o') }
        if ($newest -ne $null -and $item.LastWriteTimeUtc -lt $newest.LastWriteTimeUtc) {
            $result.problems += "$artifact ($($item.LastWriteTimeUtc.ToString('o'))) is older than the source $($newest.FullName) ($($newest.LastWriteTimeUtc.ToString('o'))): rebuild"
        }
    }

    return $result
}

function Same-Path([string]$A, [string]$B) {
    if (-not $A -or -not $B) { return $false }
    return [string]::Equals([IO.Path]::GetFullPath($A).TrimEnd('\'), [IO.Path]::GetFullPath($B).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)
}

# Verifies the actual files against the receipt Build-S10-Receipt.ps1 wrote after its controlled build. Nothing here
# writes or amends the receipt: a file that differs from it, or a .dll/.exe it does not list, is refused.
function Test-Receipt([string]$SamBuild, [string]$BenchmarkDirectory) {
    $result = [ordered]@{ path = $BuildReceipt; sha256 = (Sha $BuildReceipt); samCommit = $null; runnerCommit = $null; filesChecked = 0; samLibraries = @(); problems = @() }
    if (-not $result.sha256) {
        $result.problems += "no build receipt at ${BuildReceipt}: build with Build-S10-Receipt.ps1"
        return $result
    }

    try { $receipt = [IO.File]::ReadAllText($BuildReceipt) | ConvertFrom-Json }
    catch {
        $result.problems += "the build receipt $BuildReceipt is not JSON: $($_.Exception.Message)"
        return $result
    }

    if ($receipt.schema -ne 's10-build-receipt/1') {
        $result.problems += "the build receipt $BuildReceipt is not s10-build-receipt/1 ('$($receipt.schema)')"
        return $result
    }

    $result.samCommit = $receipt.sam.commit
    $result.runnerCommit = $receipt.runner.commit
    if (-not (Same-Path $receipt.sam.repository $SamRepository)) { $result.problems += "the build receipt is for SAM at '$($receipt.sam.repository)', not $SamRepository" }
    if (-not (Same-Path $receipt.runner.repository $RunnerRepository)) { $result.problems += "the build receipt is for SAM_Tas at '$($receipt.runner.repository)', not $RunnerRepository" }
    foreach ($pair in @(@('exe', $Exe), @('harnessDll', [IO.Path]::ChangeExtension($Exe, '.dll')), @('benchmarkExe', $BenchmarkExe), @('benchmarkDll', [IO.Path]::ChangeExtension($BenchmarkExe, '.dll')))) {
        if (-not (Same-Path $receipt.($pair[0]) $pair[1])) { $result.problems += "$($pair[1]) is not the receipt's $($pair[0]) ('$($receipt.($pair[0]))'): build it with Build-S10-Receipt.ps1" }
    }

    # Every listed file is as built; nothing unlisted is beside the harness or benchmark-tas.
    $listed = @{}
    foreach ($file in @($receipt.files)) {
        $listed[[IO.Path]::GetFullPath($file.path).ToLowerInvariant()] = $true
        $actual = Sha $file.path
        $result.filesChecked++
        if (-not $actual) { $result.problems += "$($file.path) is in the build receipt but missing" }
        elseif ($actual -ne $file.sha256) { $result.problems += "$($file.path) ($actual) does not match the build receipt ($($file.sha256)): not the receipt's build" }
    }

    $present = @($Exe, [IO.Path]::ChangeExtension($Exe, '.dll'), $BenchmarkExe, [IO.Path]::ChangeExtension($BenchmarkExe, '.dll')) + @(Get-ChildItem -LiteralPath (Split-Path $Exe), $BenchmarkDirectory -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -in '.dll', '.exe' } | ForEach-Object { $_.FullName })
    foreach ($path in @($present | Sort-Object -Unique)) {
        if (-not $listed.ContainsKey([IO.Path]::GetFullPath($path).ToLowerInvariant())) { $result.problems += "$path is not in the build receipt" }
    }

    # The SAM libraries the harness and benchmark-tas load: equal to SAM\build, which is the receipt's SAM build.
    $libraries = @($receipt.samLibraries)
    foreach ($library in $libraries) {
        $built = Join-Path $SamBuild $library.name
        $entry = [ordered]@{ name = $library.name; receipt = $library.sha256; built = (Sha $built) }
        $result.samLibraries += $entry
        if ($entry.built -ne $library.sha256) { $result.problems += "$built ($($entry.built)) is not the receipt's SAM build ($($library.sha256))" }
    }

    foreach ($library in @(Get-ChildItem -LiteralPath @((Split-Path $Exe), $BenchmarkDirectory | Sort-Object -Unique) -Filter 'SAM.*.dll' -ErrorAction SilentlyContinue | Where-Object { Test-Path -LiteralPath (Join-Path $SamBuild $_.Name) })) {
        $built = Join-Path $SamBuild $library.Name
        if (-not ($libraries | Where-Object { $_.name -eq $library.Name })) { $result.problems += "$($library.FullName) is a SAM\build library the receipt's build did not rebuild" }
        elseif ((Sha $library.FullName) -ne (Sha $built)) { $result.problems += "$($library.FullName) differs from the SAM build output $built" }
    }

    if (-not (($libraries | Where-Object { $_.name -eq 'SAM.Analytical.dll' }) -and (Test-Path -LiteralPath (Join-Path $BenchmarkDirectory 'SAM.Analytical.dll')))) { $result.problems += "SAM.Analytical.dll is not both in the receipt's SAM build and beside benchmark-tas ($BenchmarkDirectory)" }
    return $result
}

function Provenance {
    $benchmarkDir = Split-Path $BenchmarkExe
    $samBuild = Join-Path $SamRepository 'build'
    $receipt = Test-Receipt $samBuild $benchmarkDir
    $runner = Resolve-Source 'runner' $RunnerRepository @($Exe, [IO.Path]::ChangeExtension($Exe, '.dll'), $BenchmarkExe, [IO.Path]::ChangeExtension($BenchmarkExe, '.dll'), (Join-Path $benchmarkDir 'SAM.Analytical.Tas.dll')) $receipt.runnerCommit
    $samArtifacts = @(Get-ChildItem -LiteralPath $samBuild -Filter 'SAM.*.dll' -ErrorAction SilentlyContinue | Where-Object { Test-Path -LiteralPath (Join-Path $benchmarkDir $_.Name) } | ForEach-Object { $_.FullName })
    if ($samArtifacts.Count -eq 0) { $samArtifacts = @((Join-Path $samBuild 'SAM.Analytical.dll')) }
    $sam = Resolve-Source 'sam' $SamRepository $samArtifacts $receipt.samCommit

    # Informational only (not passed to benchmark-tas): the other checkouts the build used.
    $others = [ordered]@{}
    foreach ($name in 'SAM_gbXML', 'SAM_Validation') {
        $dir = [IO.Path]::GetFullPath((Join-Path $RunnerRepository "..\$name"))
        if (Test-Path (Join-Path $dir '.git')) {
            $others[$name] = [ordered]@{ head = (Git $dir rev-parse HEAD); branch = (Git $dir rev-parse --abbrev-ref HEAD); dirty = @(Git $dir status --porcelain).Count }
        }
    }

    $rules = Join-Path (Split-Path $Exe) 'calibration-rules.json'
    $problems = @($receipt.problems) + @($runner.problems) + @($sam.problems)
    $record = [ordered]@{
        utc = (Get-Date).ToUniversalTime().ToString('o'); machine = $env:COMPUTERNAME; phase = $Phase
        exe = $Exe; exeSha256 = (Sha $Exe); harnessDll = [IO.Path]::ChangeExtension($Exe, '.dll'); harnessDllSha256 = (Sha ([IO.Path]::ChangeExtension($Exe, '.dll')))
        benchmarkExe = $BenchmarkExe; benchmarkExeSha256 = (Sha $BenchmarkExe); benchmarkDll = [IO.Path]::ChangeExtension($BenchmarkExe, '.dll'); benchmarkDllSha256 = (Sha ([IO.Path]::ChangeExtension($BenchmarkExe, '.dll')))
        rules = $rules; rulesSha256 = (Sha $rules); engineVersion = $EngineVersion
        samCommit = $sam.built; runnerCommit = $runner.built; samHead = $sam.head; runnerHead = $runner.head; proven = ($problems.Count -eq 0); problems = $problems
        receipt = $receipt; runner = $runner; sam = $sam; otherRepositories = $others; powershell = $PSVersionTable.PSVersion.ToString()
    }
    $record | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $Root "provenance-$Phase.json")
    return $record
}

$provenance = Provenance

if ($Phase -eq 'run') {
    # Preflight: nothing below launches an engine until every check has passed.
    if (-not $provenance.proven) { Stop-Run ("source provenance not proven: " + ($provenance.problems -join '; ')) 'preflight' }

    # The committed, owner-signed-off fixtures and the approved weather, hash-checked before anything runs.
    foreach ($line in (Get-Content (Join-Path $fixtureDir 'SHA256SUMS.txt') | Where-Object { $_.Trim() })) {
        $expected, $name = $line -split '\s+', 2
        $actual = Sha (Join-Path $fixtureDir $name.Trim())
        if ($actual -ne $expected.ToLowerInvariant()) { Stop-Run "fixture $name is $actual, expected $expected" 'preflight' }
    }
    if (-not $WeatherEpw -or -not (Test-Path $WeatherEpw)) { Stop-Run "-WeatherEpw <epw> is required for the run phase" 'preflight' }
    $actual = Sha $WeatherEpw
    if ($actual -ne $WeatherEpwSha256) { Stop-Run "weather $WeatherEpw is $actual, expected $WeatherEpwSha256" 'preflight' }
    [ordered]@{ fixtures = (Get-Content (Join-Path $fixtureDir 'SHA256SUMS.txt')); weather = $WeatherEpw; weatherSha256 = $actual; engineVersion = $EngineVersion; samCommit = $provenance.samCommit; runnerCommit = $provenance.runnerCommit } | ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8 (Join-Path $Root 'inputs.json')
    Write-Status 'running' $null $null

    foreach ($m in $fixtures) {
        $run = Join-Path $Root $m
        New-Item -ItemType Directory -Force $run | Out-Null
        $tbd = Join-Path $run 'fixture.tbd'
        $tsd = Join-Path $run 'fixture.tsd'
        $samJson = Join-Path $run 'fixture.json'
        $b1a = Join-Path $run 'b1a.json'

        # The ONE approved simulation of this fixture: the unchanged producer route (gbXML, Sizing, AddIZAMs, UnmetHours,
        # days 1-365), with the proven source revisions.
        Require "simulate-$m" $BenchmarkExe @('--model', (Join-Path $fixtureDir "$m.json"), '--weather', $WeatherEpw, '--tbd', $tbd, '--out', $b1a, '--engine-version', $EngineVersion, '--sam-commit', $provenance.samCommit, '--runner-commit', $provenance.runnerCommit) | Out-Null
        $produced = [ordered]@{ tbd = (Sha $tbd); tsd = (Sha $tsd); samJson = (Sha $samJson); b1a = (Sha $b1a) }
        $produced | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $run 'produced.json')
        $missing = @($produced.Keys | Where-Object { $produced[$_] -eq $null })
        if ($missing.Count -gt 0) { Stop-Run "simulate-$m exited 0 but produced no $($missing -join ', ')" "simulate-$m" }

        Require "read-$m" $Exe @('results-read', $tsd, $tbd, (Join-Path $run 'read')) | Out-Null
        Require "read-design-$m" $Exe @('results-read-design', $tbd, (Join-Path $run 'read')) | Out-Null
        Require "subject-$m" $Exe @('results-subject', $samJson, $tsd, $b1a, (Join-Path $run 'subject.json')) | Out-Null
        $compare = Require "compare-$m" $Exe @('results-compare', (Join-Path $run 'read'), (Join-Path $run 'subject.json'), (Join-Path $run 'compare')) @(0, 10, 11)
        $negatives = Require "negatives-$m" $Exe @('results-negatives', (Join-Path $run 'read'), (Join-Path $run 'subject.json'), (Join-Path $run 'negatives'), 'expect=PASS') @(0, 10)
        $verdict = switch ($compare.exitCode) { 0 { 'PASS' } 10 { 'FAIL' } default { 'BLOCKED' } }
        $outcomes.Add([ordered]@{ fixture = $m; compare = $verdict; negativesAllAsIntended = ($negatives.exitCode -eq 0) })

        # The produced files must be unchanged by every read.
        $after = [ordered]@{ tbd = (Sha $tbd); tsd = (Sha $tsd); samJson = (Sha $samJson); b1a = (Sha $b1a) }
        $changed = @($produced.Keys | Where-Object { $produced[$_] -ne $after[$_] })
        [ordered]@{ before = $produced; after = $after; changed = $changed } | ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8 (Join-Path $run 'produced-after-reads.json')
        if ($changed.Count -gt 0) { Stop-Run "produced files of $m changed during the reads: $($changed -join ', ')" "reads-$m" }
        Write-Status 'running' $null $null
    }

    # The fixtures themselves are unchanged.
    foreach ($line in (Get-Content (Join-Path $fixtureDir 'SHA256SUMS.txt') | Where-Object { $_.Trim() })) {
        $expected, $name = $line -split '\s+', 2
        if ((Sha (Join-Path $fixtureDir $name.Trim())) -ne $expected.ToLowerInvariant()) { Stop-Run "fixture $name changed during the run" 'postflight' }
    }
}

if ($Phase -eq 'offline') {
    foreach ($m in $fixtures) {
        $run = Join-Path $Root $m
        if (-not (Test-Path (Join-Path $run 'subject.json'))) { Write-Host "skip $m (no subject.json)"; continue }
        foreach ($d in 'compare-replay', 'negatives-replay') { if (Test-Path (Join-Path $run $d)) { throw "refusing to overwrite $run\$d" } }
        $compare = Require "compare-replay-$m" $Exe @('results-compare', (Join-Path $run 'read'), (Join-Path $run 'subject.json'), (Join-Path $run 'compare-replay')) @(0, 10, 11)
        $negatives = Require "negatives-replay-$m" $Exe @('results-negatives', (Join-Path $run 'read'), (Join-Path $run 'subject.json'), (Join-Path $run 'negatives-replay'), 'expect=PASS') @(0, 10)
        $verdict = switch ($compare.exitCode) { 0 { 'PASS' } 10 { 'FAIL' } default { 'BLOCKED' } }
        $outcomes.Add([ordered]@{ fixture = $m; compare = $verdict; negativesAllAsIntended = ($negatives.exitCode -eq 0) })
    }
}

Write-Status 'completed' $null $null
Write-Host "watchdog: $log"
Write-Host "status: $statusPath"
exit 0
