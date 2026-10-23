# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
#
# S10: copies the committed part of a Run-S10-Results.ps1 root into Documentation/S10-Results-evidence. Large JSON is
# gzip-compressed; SHA256SUMS.txt lists every committed file and the sha256 of each uncompressed original. TBD and TSD
# files stay machine-local (their hashes are in produced.json and the read snapshots). The fixtures are not repeated
# (they are committed under ReadBack/Fixtures and hash-checked by the run, inputs.json).
#
#   powershell -ExecutionPolicy Bypass -File Pack-S10-Evidence.ps1 -Root C:\TasOut\s10\acc1
param(
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$Target = ''
)

$ErrorActionPreference = 'Stop'
if (-not $Target) { $Target = Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'Documentation\S10-Results-evidence' }
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

foreach ($phase in 'run', 'offline') {
    Put (Join-Path $Root "provenance-$phase.json") "provenance-$phase.json"
    Put (Join-Path $Root "watchdog-$phase.json") "watchdog-$phase.json"
    Put (Join-Path $Root "status-$phase.json") "status-$phase.json"
}

Put (Join-Path $Root 'inputs.json') 'inputs.json'

foreach ($m in 's08-fix-box', 's08-fix-two') {
    $run = Join-Path $Root $m
    if (-not (Test-Path $run)) { continue }
    Put (Join-Path $run 'produced.json') "runs\$m\produced.json"
    Put (Join-Path $run 'produced-after-reads.json') "runs\$m\produced-after-reads.json"
    Put (Join-Path $run 'b1a.json') "runs\$m\b1a.json"
    Put (Join-Path $run 'fixture.json') "runs\$m\fixture.json" -Gzip
    Put (Join-Path $run 'fixture.timing.csv') "runs\$m\fixture.timing.csv"
    Put (Join-Path $run 'read\tsd-result.json') "runs\$m\read\tsd-result.json" -Gzip
    Put (Join-Path $run 'read\tbd-snapshot.json') "runs\$m\read\tbd-snapshot.json" -Gzip
    Put (Join-Path $run 'read\tbd-design-coverage.json') "runs\$m\read\tbd-design-coverage.json"
    Put (Join-Path $run 'subject.json') "runs\$m\subject.json" -Gzip
    foreach ($d in 'compare', 'compare-replay') {
        Put (Join-Path $run "$d\report.md") "runs\$m\$d\report.md"
        Put (Join-Path $run "$d\report.json") "runs\$m\$d\report.json" -Gzip
        Put (Join-Path $run "$d\reference.json") "runs\$m\$d\reference.json" -Gzip
    }

    foreach ($d in 'negatives', 'negatives-replay') {
        $negatives = Join-Path $run $d
        if (-not (Test-Path $negatives)) { continue }
        Put (Join-Path $negatives 'negatives.md') "runs\$m\$d\negatives.md"
        Put (Join-Path $negatives 'negatives.json') "runs\$m\$d\negatives.json"
        foreach ($case in Get-ChildItem $negatives -Directory) {
            Put (Join-Path $case.FullName 'report.md') "runs\$m\$d\$($case.Name)\report.md" -Gzip
            foreach ($file in Get-ChildItem $case.FullName -File -Filter '*.mutated.json') {
                Put $file.FullName "runs\$m\$d\$($case.Name)\$($file.Name)" -Gzip
            }
        }
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
