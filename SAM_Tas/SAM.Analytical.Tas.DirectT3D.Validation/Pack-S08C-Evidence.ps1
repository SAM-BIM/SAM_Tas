# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
#
# S08 PR-C: copies the committed part of a Run-S08-ReadBack.ps1 acceptance root (phases acceptance and
# acceptance-negative) into Documentation/S08-PRC-Acceptance-evidence. Large JSON is gzip-compressed; SHA256SUMS.txt lists
# every committed file and the sha256 of each uncompressed original. TBD and T3D files stay machine-local (their hashes are
# in each export.json and snapshot.json). The frozen model is not repeated: it is the committed fixture (input.json hash in
# export.json). The frozen weather.json is packed, converted from the SAM-committed EPW (inputs-acceptance.json).
#
#   powershell -ExecutionPolicy Bypass -File Pack-S08C-Evidence.ps1 -Root C:\TasOut\s08c\acc1
param(
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$Target = ''
)

$ErrorActionPreference = 'Stop'
if (-not $Target) { $Target = Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'Documentation\S08-PRC-Acceptance-evidence' }
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

# Repacking replaces the run evidence but keeps the hand-written README and the offline replays of later revisions.
New-Item -ItemType Directory -Force $Target | Out-Null
Get-ChildItem $Target | Where-Object { $_.Name -ne 'README.md' -and $_.Name -notlike 'offline-replay-*' } | Remove-Item -Recurse -Force

foreach ($phase in 'acceptance', 'acceptance-negative') {
    Put (Join-Path $Root "provenance-$phase.json") "provenance-$phase.json"
    Put (Join-Path $Root "watchdog-$phase.json") "watchdog-$phase.json"
}

Put (Join-Path $Root 'inputs-acceptance.json') 'inputs-acceptance.json'

# The two fixtures carry the reader control and every series element of the verdict; the box repeat is the writer
# control (its own diff and reports; its elements equal the box's when the diff is empty).
foreach ($run in @{ 's08-fix-box' = 'diff-reader.json'; 's08-fix-two' = 'diff-reader.json'; 's08-fix-box-repeat' = 'diff-writer.json' }.GetEnumerator()) {
    Put (Join-Path $Root "$($run.Key)\export.json") "runs\$($run.Key)\export.json"
    Put (Join-Path $Root "$($run.Key)\$($run.Value)") "runs\$($run.Key)\$($run.Value)"
    Put (Join-Path $Root "$($run.Key)\snapshot.json") "runs\$($run.Key)\snapshot.json" -Gzip
    foreach ($set in 'approved', 'provisional') {
        Put (Join-Path $Root "$($run.Key)\compare-$set\report.txt") "runs\$($run.Key)\compare-$set\report.txt"
        Put (Join-Path $Root "$($run.Key)\compare-$set\report.json") "runs\$($run.Key)\compare-$set\report.json" -Gzip
    }

    if ($run.Key -ne 's08-fix-box-repeat') {
        Put (Join-Path $Root "$($run.Key)\compare-approved\series-elements.csv.gz") "runs\$($run.Key)\compare-approved\series-elements.csv.gz"
        # The frozen weather (converted from the EPW by the export), so the comparison replays offline beside the fixture.
        Put (Join-Path $Root "$($run.Key)\weather.json") "runs\$($run.Key)\weather.json" -Gzip
    }
}

foreach ($dir in Get-ChildItem $Root -Directory -Filter 'neg-*') {
    Put (Join-Path $dir.FullName 'export.json') "negatives\$($dir.Name)\export.json"
    foreach ($set in 'approved', 'provisional') {
        Put (Join-Path $dir.FullName "compare-$set\report.txt") "negatives\$($dir.Name)\compare-$set\report.txt"
        Put (Join-Path $dir.FullName "compare-$set\report.json") "negatives\$($dir.Name)\compare-$set\report.json" -Gzip
    }
}

Get-ChildItem (Join-Path $Root 'logs') -File -ErrorAction SilentlyContinue | ForEach-Object { Put $_.FullName "logs\$($_.Name)" }

Get-ChildItem $Target -Recurse -File | Where-Object { ($_.Name -ne 'SHA256SUMS.txt' -or $_.DirectoryName -ne $Target) -and $_.FullName -notlike (Join-Path $Target 'offline-replay-*') } | Sort-Object FullName | ForEach-Object {
    $sums.Add((Hash $_.FullName) + '  ' + $_.FullName.Substring($Target.Length + 1).Replace('\', '/'))
}

$lines = @('# sha256 of every committed evidence file', '') + $sums + @('', '# sha256 of the uncompressed original of each .gz file', '') + ($originals | ForEach-Object { $_.Replace('\', '/') })
[IO.File]::WriteAllLines((Join-Path $Target 'SHA256SUMS.txt'), $lines, (New-Object Text.UTF8Encoding($false)))
$size = (Get-ChildItem $Target -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ("packed {0} file(s), {1:N0} bytes into {2}" -f $sums.Count, $size, $Target)
