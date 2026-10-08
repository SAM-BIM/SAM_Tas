# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
#
# Native Optimisation PR7a spike driver (not production code).
#
# Runs ONE TasGenExecute evaluation the way the native evaluator does (Gate T protocol, SAM_Tas
# TASGENEXECUTE_PROTOCOL.md section 2.3): TasGenExecute.exe "<workspace>" with the working directory set to a
# fresh evaluation folder that holds Variables.txt. No Java, no cmd, no Tas Manager registry.
#
# The workspace is a snapshot: the Tas files of -Source plus the script given by -Script (written as Script.txt).
# Placeholders {{NAME}} in the script are replaced from -Substitute (the way PR7b's generator would write model
# item names into its blocks).
#
# One Tas simulation at a time: the driver refuses to start while TBD/TSD/TPD/TasGenExecute is running, kills the
# whole tree on timeout, and waits for the Tas COM servers this evaluation started to exit before it returns.

param(
    [Parameter(Mandatory = $true)] [string] $Script,
    [Parameter(Mandatory = $true)] [string] $Tag,
    [string] $Source = 'C:\TasOut\pr7a\base',
    [string] $Root = 'C:\TasOut\pr7a\runs',
    [string[]] $Variables = @('X=1'),
    [hashtable] $Substitute = @{},
    [string] $Workspace,
    [int] $TimeoutSeconds = 900,
    [string] $TasGenExecute = 'C:\Program Files\Environmental Design Solutions Ltd\Tas\TasGenOpt\TasGenExecute.exe'
)

$ErrorActionPreference = 'Stop'
$tasNames = @('TBD', 'TSD', 'TPD', 'TasGenExecute', 'TAS3D')

$busy = Get-Process -Name $tasNames -ErrorAction SilentlyContinue
if ($busy) { throw ('Tas is busy: ' + (($busy | ForEach-Object { $_.Name + ':' + $_.Id }) -join ', ')) }

$run = Join-Path $Root $Tag
if (Test-Path $run) { throw "Run folder exists: $run" }
New-Item -ItemType Directory -Force $run | Out-Null

# Workspace snapshot (shared by several evaluations when -Workspace is given).
if ([string]::IsNullOrWhiteSpace($Workspace)) { $Workspace = Join-Path $run 'ws' }
if (-not (Test-Path (Join-Path $Workspace 'Script.txt'))) {
    New-Item -ItemType Directory -Force $Workspace | Out-Null
    Get-ChildItem $Source -File | Where-Object { $_.Extension -in '.t3d', '.tbd', '.tsd', '.tpd', '.twd' } | Copy-Item -Destination $Workspace
    $text = [IO.File]::ReadAllText($Script)
    foreach ($key in $Substitute.Keys) { $text = $text.Replace('{{' + $key + '}}', [string]$Substitute[$key]) }
    if ($text -match '\{\{[A-Za-z0-9_]+\}\}') { throw "Unreplaced placeholder $($Matches[0]) in $Script" }
    [IO.File]::WriteAllText((Join-Path $Workspace 'Script.txt'), $text, (New-Object Text.UTF8Encoding $false))
}

$eval = Join-Path $run 'eval'
New-Item -ItemType Directory -Force $eval | Out-Null
$lines = foreach ($v in $Variables) {
    $name, $value = $v.Split('=', 2)
    $d = [double]::Parse($value, [Globalization.CultureInfo]::InvariantCulture)
    '{0},{1},{2},{3},{4},System.Double' -f $name, $d.ToString('R', [Globalization.CultureInfo]::InvariantCulture), '-1000', '1000', '0'
}
[IO.File]::WriteAllText((Join-Path $eval 'Variables.txt'), ($lines -join "`n"), (New-Object Text.UTF8Encoding $false))

$before = @(Get-Process | ForEach-Object { $_.Id })
$psi = New-Object Diagnostics.ProcessStartInfo $TasGenExecute
$psi.Arguments = '"' + $Workspace + '"'
$psi.WorkingDirectory = $eval
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$watch = [Diagnostics.Stopwatch]::StartNew()
$p = [Diagnostics.Process]::Start($psi)
$stdout = $p.StandardOutput.ReadToEndAsync()
$stderr = $p.StandardError.ReadToEndAsync()
$timedOut = -not $p.WaitForExit($TimeoutSeconds * 1000)
if ($timedOut) {
    & taskkill /PID $p.Id /T /F | Out-Null
    Get-Process -Name $tasNames -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id } | Stop-Process -Force
}
$p.WaitForExit()
$elapsed = $watch.Elapsed.TotalSeconds
[IO.File]::WriteAllText((Join-Path $run 'stdout.txt'), $stdout.Result)
[IO.File]::WriteAllText((Join-Path $run 'stderr.txt'), $stderr.Result)

# Wait for the Tas COM servers this evaluation started (TPD.exe lingers a few seconds, Gate T).
$lingerWatch = [Diagnostics.Stopwatch]::StartNew()
while ($true) {
    $left = @(Get-Process -Name $tasNames -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id })
    if ($left.Count -eq 0 -or $lingerWatch.Elapsed.TotalSeconds -gt 60) { break }
    Start-Sleep -Milliseconds 250
}
$linger = $lingerWatch.Elapsed.TotalSeconds
$leftNames = ($left | ForEach-Object { $_.Name + ':' + $_.Id }) -join ' '

# Which Tas files in the evaluation folder are still locked once the evaluation's processes have gone (or before
# the left-over ones are killed)?
$locked = @()
foreach ($file in Get-ChildItem $eval -File | Where-Object { $_.Extension -in '.tbd', '.tsd', '.tpd' }) {
    try { $s = [IO.File]::Open($file.FullName, 'Open', 'ReadWrite', 'None'); $s.Close() } catch { $locked += $file.Name }
}

if ($left.Count -gt 0) { $left | Stop-Process -Force }

$output = Join-Path $eval 'Output.txt'
$errorFile = Join-Path $eval 'Error.txt'
$outText = if (Test-Path $output) { [IO.File]::ReadAllText($output) } else { '' }
$errText = if (Test-Path $errorFile) { [IO.File]::ReadAllText($errorFile).Trim() } else { '' }

$summary = [ordered]@{
    tag = $Tag
    exit = $p.ExitCode
    timedOut = $timedOut
    seconds = [Math]::Round($elapsed, 2)
    lingerSeconds = [Math]::Round($linger, 2)
    leftOver = $leftNames
    locked = ($locked -join ' ')
    error = $errText
    output = ($outText -split "`r?`n" | Where-Object { $_ -ne '' })
}
$json = $summary | ConvertTo-Json -Compress -Depth 4
[IO.File]::WriteAllText((Join-Path $run 'summary.json'), $json)
Add-Content -Path (Join-Path $Root 'runs.jsonl') -Value $json
$json
