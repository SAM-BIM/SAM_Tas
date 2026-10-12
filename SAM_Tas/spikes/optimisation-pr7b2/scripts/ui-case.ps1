# One licensed SAM_UI harness run (TasModelApplyAcceptanceHarness) with a process monitor, a bounded wait, registry
# export before/after and restore of HKCU\Software\EDSL. Never two at once (lock).
param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Cases, [Parameter(Mandatory)][string]$Sam,
      [string]$Direct = 'off', [string]$Heating = '', [string]$Cooling = '', [string]$Sizing = 'on', [int]$TimeoutMin = 45)
$ErrorActionPreference = 'Stop'
$root = 'C:\TasOut\pr7b-2'
$lock = "$root\step.lock"
if (Test-Path $lock) { throw "another step is running: $(Get-Content $lock)" }
Set-Content $lock $Name
$out = "$root\ui\$Name"
try {
  if (Get-Process -Name TBD,TSD,TPD,TCD,TAS3D,TasGenExecute -ErrorAction SilentlyContinue) { throw "Tas processes running before $Name" }
  New-Item -ItemType Directory -Force $out | Out-Null
  reg export 'HKCU\Software\EDSL' "$out\reg-before.reg" /y | Out-Null
  $stop = "$out\monitor.stop"; Remove-Item $stop -ErrorAction SilentlyContinue
  $mon = Start-Process powershell -ArgumentList @('-NoProfile','-File','C:\TasOut\pr9\monitor.ps1','-Log',"$out\procs.log",'-Stop',$stop) -WindowStyle Hidden -PassThru
  $env:SAM_APPLY_ACCEPTANCE = $out
  $env:SAM_APPLY_ACCEPTANCE_SAM = $Sam
  $env:SAM_APPLY_ACCEPTANCE_CASES = $Cases
  $env:SAM_APPLY_ACCEPTANCE_DIRECT = $Direct
  if ($Heating) { $env:SAM_APPLY_ACCEPTANCE_HEATING = $Heating } else { Remove-Item Env:SAM_APPLY_ACCEPTANCE_HEATING -ErrorAction SilentlyContinue }
  if ($Cooling) { $env:SAM_APPLY_ACCEPTANCE_COOLING = $Cooling } else { Remove-Item Env:SAM_APPLY_ACCEPTANCE_COOLING -ErrorAction SilentlyContinue }
  $env:SAM_APPLY_ACCEPTANCE_SIZING = $Sizing
  $start = Get-Date
  "$((Get-Date).ToString('s')) START ui $Name cases=$Cases direct=$Direct heating=$Heating cooling=$Cooling sizing=$Sizing sam=$Sam (sha256 $((Get-FileHash $Sam).Hash))" | Tee-Object -Append "$root\steps.log"
  $proj = 'C:\Users\michal\Documents\GitHub\SAM-BIM\SAM_UI\WPF\SAM.Analytical.UI.WPF.Tests'
  $p = Start-Process dotnet -ArgumentList @('test', $proj, '-c', 'Release', '--no-build', '--filter', 'FullyQualifiedName~TasModelApplyAcceptanceHarness', '--logger', "trx;LogFileName=$out\harness.trx") -NoNewWindow -PassThru -RedirectStandardOutput "$out\dotnet.out.txt" -RedirectStandardError "$out\dotnet.err.txt"
  $null = $p.Handle
  $timedOut = -not $p.WaitForExit($TimeoutMin * 60000)
  if ($timedOut) { $p.Kill(); Start-Sleep 3 }
  $deadline = (Get-Date).AddSeconds(30)
  do { $left = Get-Process -Name TBD,TSD,TPD,TCD,TAS3D,TasGenExecute -ErrorAction SilentlyContinue; if (-not $left) { break }; Start-Sleep 1 } while ((Get-Date) -lt $deadline)
  Start-Sleep 5; Set-Content $stop 'stop'; $mon.WaitForExit(10000) | Out-Null; $left = Get-Process -Name TBD,TSD,TPD,TCD,TAS3D,TasGenExecute -ErrorAction SilentlyContinue
  reg export 'HKCU\Software\EDSL' "$out\reg-after.reg" /y | Out-Null
  # Restore the state of just before THIS run (not an older global baseline: the user may have used Tas since).
  & cmd /c "reg import `"$out\reg-before.reg`" 2>nul" | Out-Null
  reg export 'HKCU\Software\EDSL' "$out\reg-restored.reg" /y | Out-Null
  $restored = (Get-FileHash "$out\reg-restored.reg").Hash -eq (Get-FileHash "$out\reg-before.reg").Hash
  $crashes = @(Get-WinEvent -FilterHashtable @{LogName='Application'; Id=1000; StartTime=$start} -ErrorAction SilentlyContinue | Where-Object { $_.Message -match 'TPD\.exe|TSD\.exe|TBD\.exe|TasGenExecute|tas3d|TCD\.exe' })
  "{0} END ui {1} exit={2} timedOut={3} {4:0} s; Tas processes left: {5}; crash events: {6}; registry restored to its pre-run state: {7}" -f (Get-Date).ToString('s'), $Name, $(if ($timedOut) { 'killed' } else { $p.ExitCode }), $timedOut, ((Get-Date) - $start).TotalSeconds, $(if ($left) { ($left | % { "$($_.Name) $($_.Id)" }) -join ', ' } else { 'none' }), $crashes.Count, $restored | Tee-Object -Append "$root\steps.log"
  Get-Content "$out\dotnet.out.txt" | Select-String 'Passed!|Failed!|Total tests|error' | % Line
}
finally { Remove-Item $lock -ErrorAction SilentlyContinue }
