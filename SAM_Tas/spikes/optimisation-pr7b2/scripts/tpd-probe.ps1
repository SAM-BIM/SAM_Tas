# Probe candidate TPDs for plant rooms / TSD references. Copies only; one Tas operation at a time; bounded; cleanup checked.
$ErrorActionPreference = 'Stop'
$d = 'C:\TasOut\pr7b-2\driver\bin\Release\net8.0-windows\Pr7b2Driver.exe'
$out = 'C:\TasOut\pr7b-2\inputs\tpd-probe'
New-Item -ItemType Directory -Force $out | Out-Null
$candidates = @(
  'C:\TasOut\pr7b\demo\Systems Training.tpd',
  'C:\Users\michal\Documents\SAM_daily\2026-04-16-MVHR_TPD\Block-B\000000_SAM_AnalyticalModel.tpd',
  'C:\Users\michal\Documents\SAM_daily\2026-04-16-MVHR_TPD\MVRE-SEandNV.tpd',
  'C:\Users\michal\Documents\SAM_daily\2026-04-16-MVHR_TPD\MVRE-SEandNV1.tpd',
  'C:\Users\michal\Documents\SAM_daily\2026-04-16-MVHR_TPD\MVHR-delte.tpd',
  'C:\Users\michal\Documents\SAM_daily\2026-04-16-MVHR_TPD\OneLevel08-Ventilation_Sys.tpd',
  'C:\Users\michal\Documents\SAM_daily\2026-04-16-MVHR_TPD\OneLevel08-Ventilation_SysMD.tpd',
  'C:\Users\michal\Documents\SAM_daily\2026-07-15 PartO\000000_SAM_AnalyticalModel-It1a-futureZ1-It3B.tpd',
  'C:\Users\michal\Documents\SAM_daily\2026-07-15 PartO\Simulation\Flat1.tpd')
$i = 0
foreach ($c in $candidates) {
  $i++
  $dir = Join-Path $out ("{0:00}" -f $i); New-Item -ItemType Directory -Force $dir | Out-Null
  $copy = Join-Path $dir (Split-Path $c -Leaf)
  Copy-Item $c $copy -Force
  $src = (Get-FileHash $c).Hash; $dst = (Get-FileHash $copy).Hash
  "== $c (source sha256 $src, copy equal: $($src -eq $dst))" | Tee-Object -Append "$out\probe.log"
  $p = Start-Process -FilePath $d -ArgumentList @('tpd', "`"$copy`"") -NoNewWindow -PassThru -RedirectStandardOutput "$dir\out.txt" -RedirectStandardError "$dir\err.txt"
  if (-not $p.WaitForExit(180000)) { $p.Kill(); "TIMEOUT 180 s" | Tee-Object -Append "$out\probe.log" }
  Get-Content "$dir\out.txt","$dir\err.txt" | Tee-Object -Append "$out\probe.log"
  Start-Sleep 3
  $left = Get-Process -Name TBD,TSD,TPD,TCD,TAS3D,TasGenExecute -ErrorAction SilentlyContinue
  "Tas processes left: " + ($(if ($left) { ($left | % { "$($_.Name) $($_.Id)" }) -join ', ' } else { 'none' })) | Tee-Object -Append "$out\probe.log"
  "source unchanged: " + ((Get-FileHash $c).Hash -eq $src) | Tee-Object -Append "$out\probe.log"
}
