# One licensed step: run the driver with a bounded timeout, log stdout/stderr, then wait for Tas processes to exit
# (up to 30 s) and record what is left. Never runs two steps at once (a lock file refuses a concurrent call).
param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string[]]$DriverArgs, [int]$TimeoutSec = 600)
$ErrorActionPreference = 'Stop'
$root = 'C:\TasOut\pr7b-2'
$lock = "$root\step.lock"
if (Test-Path $lock) { throw "another step is running: $(Get-Content $lock)" }
Set-Content $lock $Name
try {
  $logs = "$root\logs"; New-Item -ItemType Directory -Force $logs | Out-Null
  $before = Get-Process -Name TBD,TSD,TPD,TCD,TAS3D,TasGenExecute -ErrorAction SilentlyContinue
  if ($before) { throw "Tas processes running before $Name : $(($before | % { "$($_.Name) $($_.Id)" }) -join ', ')" }
  $d = "$root\driver\bin\Release\net8.0-windows\Pr7b2Driver.exe"
  $quoted = $DriverArgs | % { if ($_ -match '\s|;|\|') { '"' + $_ + '"' } else { $_ } }
  $start = Get-Date
  "$((Get-Date).ToString('s')) START $Name : $($quoted -join ' ')" | Tee-Object -Append "$root\steps.log"
  $p = Start-Process -FilePath $d -ArgumentList $quoted -NoNewWindow -PassThru -RedirectStandardOutput "$logs\$Name.out.txt" -RedirectStandardError "$logs\$Name.err.txt"; $null = $p.Handle
  $timedOut = -not $p.WaitForExit($TimeoutSec * 1000)
  if ($timedOut) { $p.Kill(); Start-Sleep 2 }
  $p.WaitForExit()
  $deadline = (Get-Date).AddSeconds(30)
  do { $left = Get-Process -Name TBD,TSD,TPD,TCD,TAS3D,TasGenExecute -ErrorAction SilentlyContinue; if (-not $left) { break }; Start-Sleep 1 } while ((Get-Date) -lt $deadline)
  $crashes = Get-WinEvent -FilterHashtable @{LogName='Application'; Id=1000; StartTime=$start} -ErrorAction SilentlyContinue | Where-Object { $_.Message -match 'TPD\.exe|TSD\.exe|TBD\.exe|TasGenExecute|tas3d' }
  $summary = "{0} END {1} exit={2} timedOut={3} {4:0.0} s; Tas processes left: {5}; Application-log crashes (Tas): {6}" -f (Get-Date).ToString('s'), $Name, $(if ($timedOut) { 'killed' } else { $p.ExitCode }), $timedOut, ((Get-Date) - $start).TotalSeconds, $(if ($left) { ($left | % { "$($_.Name) $($_.Id)" }) -join ', ' } else { 'none' }), (@($crashes).Count)
  foreach ($c in @($crashes)) { "   crash: " + ($c.Message -split "`n")[0] | Tee-Object -Append "$root\steps.log" }
  $summary | Tee-Object -Append "$root\steps.log"
  Get-Content "$logs\$Name.out.txt" | Select-String -Pattern 'PASS|FAIL|ok=|best |run outcome|  Y|^Y|test |sim ' | Select-Object -First 80 | % Line
  Get-Content "$logs\$Name.err.txt" | Select-Object -First 20
}
finally { Remove-Item $lock -ErrorAction SilentlyContinue }
