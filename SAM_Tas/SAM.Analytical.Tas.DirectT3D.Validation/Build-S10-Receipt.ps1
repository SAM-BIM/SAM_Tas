# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
#
# S10: the controlled, engine-free Release build whose receipt `Run-S10-Results.ps1 -Phase run` requires (review R3 of
# the revision-2 correction). Nothing here starts Tas, opens a COM server or touches a Tas process.
#
#   1. SAM and SAM_Tas must each be a clean checkout (no change, no untracked file). Their HEADs are the build commits.
#   2. MSBuild rebuilds, one project at a time into SAM\build, the SAM core libraries that benchmark-tas loads. It then
#      rebuilds SAM_Tas.sln. Each build gets /p:SAMSourceRevision=<its checkout's HEAD>, the repositories' existing stamp
#      (Directory.Build.props), so every assembly built carries InformationalVersion <version>+<commit>; benchmark-tas
#      reads the same stamp. No BuildAll, no Revit/Grasshopper deployment, no installer.
#   3. Only after every build exits 0:
#      - both checkouts must still be clean at the same HEAD;
#      - each declared artifact (the harness and benchmark-tas exe/dll, SAM.Analytical.Tas.dll, the SAM core libraries)
#        must have been written by this build, and the compiled ones must carry their checkout's stamp;
#      - every SAM library beside benchmark-tas that SAM\build also has must be one of the libraries built here and
#        byte-identical to its SAM\build copy.
#   4. The receipt (default: s10-build-receipt.json beside benchmark-tas) records both commits, the commands, and the
#      sha256 of every .dll/.exe in the harness and benchmark-tas folders and of the SAM core libraries. It is removed
#      before the build, so a failed or refused build leaves no receipt, never a stale one.
#
# The run preflight verifies the files against this receipt and never writes one. -MSBuild exists so the offline tests can
# substitute a build double. It defaults to VS MSBuild, which the COM-referencing harness requires (MSB4803).
#
# Usage (Windows PowerShell 5.1, from anywhere; the defaults are this workspace's paths):
#   powershell -ExecutionPolicy Bypass -File Build-S10-Receipt.ps1
param(
    [string]$MSBuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe',
    [string]$Exe = '',
    [string]$BenchmarkExe = '',
    [string]$SamRepository = '',
    [string]$RunnerRepository = '',
    [string]$BuildReceipt = ''
)

$ErrorActionPreference = 'Stop'
if (-not $Exe) { $Exe = Join-Path $PSScriptRoot 'bin\Release\direct-t3d-validation.exe' }
if (-not $BenchmarkExe) { $BenchmarkExe = Join-Path $PSScriptRoot '..\..\build\benchmark-tas.exe' }
if (-not $RunnerRepository) { $RunnerRepository = Join-Path $PSScriptRoot '..\..' }
if (-not $SamRepository) { $SamRepository = Join-Path $RunnerRepository '..\SAM' }
$Exe = [IO.Path]::GetFullPath($Exe)
$BenchmarkExe = [IO.Path]::GetFullPath($BenchmarkExe)
$RunnerRepository = [IO.Path]::GetFullPath($RunnerRepository)
$SamRepository = [IO.Path]::GetFullPath($SamRepository)
if (-not $BuildReceipt) { $BuildReceipt = Join-Path (Split-Path $BenchmarkExe) 's10-build-receipt.json' }
$BuildReceipt = [IO.Path]::GetFullPath($BuildReceipt)

# The SAM core libraries benchmark-tas loads from SAM\build, in dependency order.
$samProjects = 'SAM.Core', 'SAM.Math', 'SAM.Units', 'SAM.Geometry', 'SAM.Weather', 'SAM.Architectural', 'SAM.Analytical', 'SAM.Core.Optimisation'

function Refuse([string]$Reason) {
    Write-Host "REFUSED (no receipt written): $Reason"
    exit 3
}

# SHA-256 through .NET, as in Run-S10-Results.ps1 (review R5): Get-FileHash is a Windows PowerShell 5.1 script function
# that only module autoload provides, along the PSModulePath inherited from the parent host.
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

# The HEAD of a clean checkout, or the reason there is none.
function Clean-Head([string]$Directory) {
    if (-not (Test-Path (Join-Path $Directory '.git'))) { Refuse "$Directory is not a git checkout" }
    $head = (Git $Directory rev-parse HEAD | Out-String).Trim()
    if ($head -notmatch '^[0-9a-f]{40}$') { Refuse "HEAD of $Directory is not a full commit SHA ('$head')" }
    $status = @(Git $Directory status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { Refuse "git status failed in $Directory" }
    if ($status.Count -gt 0) { Refuse "$Directory has $($status.Count) uncommitted or untracked change(s), e.g. $($status[0])" }
    return $head
}

# A compiled output carries its commit as the InformationalVersion '+<sha>' suffix (a UTF-8 string in the assembly).
function Test-Stamp([string]$Path, [string]$Commit) {
    $text = [Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($Path))
    return $text.Contains('+' + $Commit)
}

if (Test-Path -LiteralPath $BuildReceipt) { Remove-Item -LiteralPath $BuildReceipt -Force }
$samHead = Clean-Head $SamRepository
$runnerHead = Clean-Head $RunnerRepository
if (-not (Test-Path -LiteralPath $MSBuild)) { Refuse "MSBuild not found: $MSBuild" }

$samBuild = Join-Path $SamRepository 'build'
$harnessDll = [IO.Path]::ChangeExtension($Exe, '.dll')
$benchmarkDll = [IO.Path]::ChangeExtension($BenchmarkExe, '.dll')
$benchmarkDir = Split-Path $BenchmarkExe
$samTas = Join-Path $benchmarkDir 'SAM.Analytical.Tas.dll'
$started = (Get-Date).ToUniversalTime()
$commands = New-Object System.Collections.Generic.List[object]

function Build([string]$Project, [string]$Commit, [string[]]$Extra) {
    $arguments = @($Project, '-restore', '-t:Rebuild', '-p:Configuration=Release', "-p:SAMSourceRevision=$Commit", '-nr:false', '-v:m') + $Extra
    Write-Host "==> $Project"
    & $MSBuild @arguments | Out-Host
    $code = $LASTEXITCODE
    $commands.Add([ordered]@{ file = $MSBuild; arguments = ($arguments -join ' '); exitCode = $code })
    if ($code -ne 0) { Refuse "MSBuild exited $code for $Project" }
}

foreach ($name in $samProjects) { Build (Join-Path $SamRepository "SAM\$name\$name.csproj") $samHead @() }
Build (Join-Path $RunnerRepository 'SAM_Tas.sln') $runnerHead @('-m')

# The build must leave both checkouts as they were.
if ((Clean-Head $SamRepository) -ne $samHead) { Refuse "the HEAD of $SamRepository moved during the build" }
if ((Clean-Head $RunnerRepository) -ne $runnerHead) { Refuse "the HEAD of $RunnerRepository moved during the build" }

# Every declared artifact was written by this build; the compiled ones carry their checkout's stamp.
$stamped = New-Object System.Collections.Generic.List[object]
$declared = @(@($Exe, $harnessDll, $BenchmarkExe, $benchmarkDll, $samTas) | ForEach-Object { [ordered]@{ path = $_; commit = $runnerHead; compiled = ($_ -like '*.dll') } }) +
    @($samProjects | ForEach-Object { [ordered]@{ path = (Join-Path $samBuild "$_.dll"); commit = $samHead; compiled = $true } })
foreach ($artifact in $declared) {
    $item = Get-Item -LiteralPath $artifact.path -ErrorAction SilentlyContinue
    if (-not $item) { Refuse "the build did not produce $($artifact.path)" }
    if ($item.LastWriteTimeUtc -lt $started) { Refuse "$($artifact.path) was not written by this build ($($item.LastWriteTimeUtc.ToString('o')) < $($started.ToString('o')))" }
    if ($artifact.compiled) {
        if (-not (Test-Stamp $artifact.path $artifact.commit)) { Refuse "$($artifact.path) does not carry the stamp +$($artifact.commit)" }
        $stamped.Add([ordered]@{ path = $item.FullName; commit = $artifact.commit })
    }
}

# The SAM libraries the harness and benchmark-tas load are the ones built here, byte for byte.
$loadDirs = @((Split-Path $Exe), $benchmarkDir | Sort-Object -Unique)
foreach ($library in @(Get-ChildItem -LiteralPath $loadDirs -Filter 'SAM.*.dll' | Where-Object { Test-Path -LiteralPath (Join-Path $samBuild $_.Name) })) {
    if ($samProjects -notcontains $library.BaseName) { Refuse "$($library.FullName) is a SAM\build library this build does not rebuild" }
    if ((Sha $library.FullName) -ne (Sha (Join-Path $samBuild $library.Name))) { Refuse "$($library.FullName) differs from the SAM build output $(Join-Path $samBuild $library.Name)" }
}
if (-not (Test-Path -LiteralPath (Join-Path $benchmarkDir 'SAM.Analytical.dll'))) { Refuse "SAM.Analytical.dll is not beside benchmark-tas ($benchmarkDir)" }
$samLibraries = @($samProjects | ForEach-Object { [ordered]@{ name = "$_.dll"; built = (Join-Path $samBuild "$_.dll"); sha256 = (Sha (Join-Path $samBuild "$_.dll")) } })

# Every executable and library in the harness and benchmark-tas folders, by hash.
$paths = @($Exe, $harnessDll, $BenchmarkExe, $benchmarkDll) + @(Get-ChildItem -LiteralPath (Split-Path $Exe), $benchmarkDir -File | Where-Object { $_.Extension -in '.dll', '.exe' } | ForEach-Object { $_.FullName })
$files = @($paths | Sort-Object -Unique | ForEach-Object { [ordered]@{ path = $_; sha256 = (Sha $_) } })

[ordered]@{
    schema = 's10-build-receipt/1'
    note = 'Written by Build-S10-Receipt.ps1 after a successful controlled build of clean checkouts; Run-S10-Results.ps1 verifies it and never writes it.'
    startedUtc = $started.ToString('o'); completedUtc = (Get-Date).ToUniversalTime().ToString('o'); machine = $env:COMPUTERNAME
    msbuild = $MSBuild; commands = $commands
    sam = [ordered]@{ repository = $SamRepository; commit = $samHead; branch = (Git $SamRepository rev-parse --abbrev-ref HEAD | Out-String).Trim() }
    runner = [ordered]@{ repository = $RunnerRepository; commit = $runnerHead; branch = (Git $RunnerRepository rev-parse --abbrev-ref HEAD | Out-String).Trim() }
    exe = $Exe; harnessDll = $harnessDll; benchmarkExe = $BenchmarkExe; benchmarkDll = $benchmarkDll
    stamped = $stamped; samLibraries = $samLibraries; files = $files
} | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 $BuildReceipt

Write-Host "receipt: $BuildReceipt"
Write-Host "sam $samHead, runner $runnerHead, $($files.Count) files"
exit 0
