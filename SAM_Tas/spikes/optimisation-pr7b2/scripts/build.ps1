# PR7b-2: rebuild SAM -> SAM_Systems -> SAM_Tas -> SAM_UI (Release) in the documented order, then record hashes.
# APPDATA/USERPROFILE redirected (SAM_UI's post-build xcopies into %APPDATA%\SAM); NUGET_PACKAGES = the real cache.
param([string[]]$Repos = @('SAM','SAM_Systems','SAM_Tas','SAM_UI'))
$ErrorActionPreference = 'Stop'
$root = 'C:\Users\michal\Documents\GitHub\SAM-BIM'
$out = 'C:\TasOut\pr7b-2\build'
New-Item -ItemType Directory -Force $out | Out-Null
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$realNuget = Join-Path $env:USERPROFILE '.nuget\packages'
$scratch = 'C:\TasOut\pr7b-2\profile'
New-Item -ItemType Directory -Force "$scratch\AppData\Roaming", "$scratch\AppData\Local" | Out-Null
$env:NUGET_PACKAGES = $realNuget
$env:APPDATA = "$scratch\AppData\Roaming"
$env:USERPROFILE = $scratch
$env:Path = "C:\Users\michal\AppData\Local\GitHubDesktop\app-3.6.7\resources\app\git\cmd;" + $env:Path
$solutions = @{ 'SAM'='SAM\SAM.sln'; 'SAM_Systems'='SAM_Systems\SAM_Systems.sln'; 'SAM_Tas'='SAM_Tas\SAM_Tas.sln'; 'SAM_UI'='SAM_UI\SAM_UI.sln' }
foreach ($r in $Repos) {
  $head = git -C "$root\$r" rev-parse HEAD
  $dirty = git -C "$root\$r" status --porcelain
  "{0} {1} HEAD {2} dirty={3}" -f (Get-Date -Format s), $r, $head, [bool]$dirty | Tee-Object -Append "$out\build-summary.txt"
  $log = "$out\build-$r.log"
  $sw = [Diagnostics.Stopwatch]::StartNew()
  & $msbuild "$root\$($solutions[$r])" -restore -p:Configuration=Release -m:1 -nologo -v:minimal "-flp:logfile=$log;verbosity=minimal" | Out-Null
  $code = $LASTEXITCODE
  $errors = (Select-String -Path $log -Pattern ' error ' -SimpleMatch | Measure-Object).Count
  "{0} {1} exit {2} errors {3} in {4:0} s" -f (Get-Date -Format s), $r, $code, $errors, $sw.Elapsed.TotalSeconds | Tee-Object -Append "$out\build-summary.txt"
  if ($code -ne 0) { throw "$r build failed (exit $code); see $log" }
}
$dlls = 'SAM\build\SAM.Core.dll','SAM\build\SAM.Analytical.dll','SAM\build\SAM.Math.dll','SAM\build\SAM.Core.Optimisation.dll','SAM\build\SAM.Units.dll',
        'SAM_Systems\build\SAM.Analytical.Systems.dll','SAM_Tas\build\SAM.Core.Tas.dll','SAM_Tas\build\SAM.Analytical.Tas.dll','SAM_Tas\build\SAM.Analytical.Tas.GenOpt.dll',
        'SAM_UI\build\SAM.Analytical.UI.WPF.dll','SAM_UI\build\SAM Analytical.exe'
$dlls | ForEach-Object { $f = Get-Item "$root\$_"; "{0} {1:yyyy-MM-dd HH:mm:ss} {2}" -f (Get-FileHash $f.FullName).Hash, $f.LastWriteTime, $_ } | Tee-Object "$out\build-hashes.txt"
