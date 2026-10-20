# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
#
# S08 PR-B: copies the committed part of a Run-S08-ReadBack.ps1 output root into Documentation/S08-PRB-ReadBack-evidence.
# Large JSON is gzip-compressed; SHA256SUMS.txt lists every committed file AND the sha256 of each uncompressed original,
# so a reader can check the files against the run's own export.json and snapshot hashes. TBD, T3D and TSD files stay
# machine-local (their hashes are in each export.json and snapshot.json).
#
#   powershell -ExecutionPolicy Bypass -File Pack-S08-Evidence.ps1 -Root C:\TasOut\s08b\cal2
param(
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$Target = ''
)

$ErrorActionPreference = 'Stop'
if (-not $Target) { $Target = Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'Documentation\S08-PRB-ReadBack-evidence' }
$sums = New-Object System.Collections.Generic.List[string]
$originals = New-Object System.Collections.Generic.List[string]

function Hash([string]$Path) { (Get-FileHash $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

function Put([string]$Source, [string]$Relative, [switch]$Gzip) {
    if (-not (Test-Path $Source)) { Write-Host "  (missing) $Source"; return }
    $dest = Join-Path $Target $Relative
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    if ($Gzip) {
        $dest = $dest + '.gz'
        $in = [IO.File]::OpenRead($Source)
        $out = [IO.File]::Create($dest)
        $gz = New-Object IO.Compression.GZipStream($out, [IO.Compression.CompressionLevel]::Optimal)
        $in.CopyTo($gz); $gz.Close(); $out.Close(); $in.Close()
        $originals.Add((Hash $Source) + '  ' + $Relative)
    } else {
        Copy-Item $Source $dest -Force
    }
}

# Repacking replaces the run evidence but keeps the hand-written README and the record of the approved executions.
New-Item -ItemType Directory -Force $Target | Out-Null
Get-ChildItem $Target | Where-Object { $_.Name -notin 'README.md', 'approved-executions' } | Remove-Item -Recurse -Force

foreach ($phase in 'calibrate', 'compare', 'negative') {
    Put (Join-Path $Root "provenance-$phase.json") "provenance-$phase.json"
    Put (Join-Path $Root "watchdog-$phase.json") "watchdog-$phase.json"
}

Put (Join-Path $Root 'models\SHA256SUMS.txt') 'models\SHA256SUMS.txt'
Put (Join-Path $Root 'calibration\calibration-findings.json') 'calibration\calibration-findings.json'
Put (Join-Path $Root 'calibration\calibration-rules.candidate.json') 'calibration\calibration-rules.candidate.json'

foreach ($run in 'calib-box', 'calib-two') {
    foreach ($f in 'export.json', 'diff-reader.json') { Put (Join-Path $Root "$run\$f") "runs\$run\$f" }
    foreach ($f in 'input.json', 'weather.json', 'snapshot.json') { Put (Join-Path $Root "$run\$f") "runs\$run\$f" -Gzip }
    Put (Join-Path $Root "$run\compare\report.json") "runs\$run\compare\report.json" -Gzip
    Put (Join-Path $Root "$run\compare\report.txt") "runs\$run\compare\report.txt"
    Put (Join-Path $Root "$run\compare\series-elements.csv.gz") "runs\$run\compare\series-elements.csv.gz"
    Put (Join-Path $Root "$run\compare-provisional\report.txt") "runs\$run\compare-provisional\report.txt"
}

foreach ($run in @{ 'calib-box-repeat' = 'diff-writer.json'; 'calib-box-sizing' = 'diff-sizing.json' }.GetEnumerator()) {
    Put (Join-Path $Root "$($run.Key)\export.json") "runs\$($run.Key)\export.json"
    Put (Join-Path $Root "$($run.Key)\snapshot.json") "runs\$($run.Key)\snapshot.json" -Gzip
    Put (Join-Path $Root "$($run.Key)\compare\report.txt") "runs\$($run.Key)\compare\report.txt"
    Put (Join-Path $Root "$($run.Key)\$($run.Value)") "runs\$($run.Key)\$($run.Value)"
}

Put (Join-Path $Root 'calib-align\export.json') 'runs\calib-align\export.json'
foreach ($f in 'input.json', 'weather.json', 'snapshot.json', 'tsd.json') { Put (Join-Path $Root "calib-align\$f") "runs\calib-align\$f" -Gzip }

foreach ($dir in Get-ChildItem $Root -Directory -Filter 'neg-*') {
    Put (Join-Path $dir.FullName 'export.json') "negatives\$($dir.Name)\export.json"
    foreach ($set in 'interim', 'provisional') {
        Put (Join-Path $dir.FullName "compare-$set\report.txt") "negatives\$($dir.Name)\compare-$set\report.txt"
        Put (Join-Path $dir.FullName "compare-$set\report.json") "negatives\$($dir.Name)\compare-$set\report.json" -Gzip
    }
}

Get-ChildItem (Join-Path $Root 'logs') -File -ErrorAction SilentlyContinue | ForEach-Object { Put $_.FullName "logs\$($_.Name)" }

Get-ChildItem $Target -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' -or $_.DirectoryName -ne $Target } | Sort-Object FullName | ForEach-Object {
    $sums.Add((Hash $_.FullName) + '  ' + $_.FullName.Substring($Target.Length + 1).Replace('\', '/'))
}

$lines = @('# sha256 of every committed evidence file', '') + $sums + @('', '# sha256 of the uncompressed original of each .gz file', '') + ($originals | ForEach-Object { $_.Replace('\', '/') })
[IO.File]::WriteAllLines((Join-Path $Target 'SHA256SUMS.txt'), $lines, (New-Object Text.UTF8Encoding($false)))
$size = (Get-ChildItem $Target -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ("packed {0} file(s), {1:N0} bytes into {2}" -f $sums.Count, $size, $Target)
