# S08 PR-B evidence: Tas native-input read-back calibration

Licensed, machine-local runs of the S08 PR-B read-back harness
(`SAM_Tas/SAM.Analytical.Tas.DirectT3D.Validation`, modes `readback-*`) on the **synthetic calibration models only**
(`ReadBack/CalibrationModels.cs`). These models are not equivalence fixtures. Fixtures and S08 acceptance are PR-C.
Nothing here is a fixture PASS or an S08 acceptance.

The record is `Documentation/S08-PRB-TasReadBack-PR.md`. The contract is S07, SAM#216
(`documentation/SIMULATION_INPUT_MAPPING_S07.md`).

## What is here

| Path | What |
|---|---|
| `provenance-<phase>.json` | Machine, repository heads and dirty state, harness exe and rule-table sha256, per phase |
| `watchdog-<phase>.json` | Every step: arguments, exit code, expected codes, seconds, timeout, Tas processes left behind |
| `approved-executions/` | The run that performed the **one** owner-approved P-ALIGN 365-day simulation and the **one** owner-approved Sizing=true export (logs, watchdog, provenance) |
| `models/SHA256SUMS.txt` | Hashes of the calibration models and weather as the run wrote them |
| `calibration/calibration-findings.json` | Each native question (N-1 .. N-8), the measured answer, consistency and every observation |
| `calibration/calibration-rules.candidate.json` | The rule table `readback-calibrate` derived; the committed `ReadBack/calibration-rules.json` has the same rules (plus a provenance note) |
| `runs/calib-box`, `runs/calib-two` | `export.json` (frozen-input hashes, actual settings, steps, notes, assemblies, outputs), frozen `input.json`/`weather.json`, the fresh-process `snapshot.json`, the reader-control diff, `compare/report.*` and every series element (`series-elements.csv.gz`) |
| `runs/calib-box-repeat` | The writer control: the same frozen model exported again, and its diff |
| `runs/calib-box-sizing` | The informational Sizing=true export and its diff against the Sizing=false box |
| `runs/calib-align` | The P-ALIGN model, its persisted inputs and the fresh-process TSD read (`tsd.json`) |
| `negatives/neg-<case>` | Each licensed negative check on a disposable copy: the mutation (with the original hashes) and both rule-set reports |
| `logs/` | stdout/stderr of every step |
| `SHA256SUMS.txt` | sha256 of every committed file, and of the uncompressed original of each `.gz` |

Large JSON is gzip-compressed (`.gz`). TBD, T3D and TSD files are **not committed**: they are machine-local, and their
sha256 is in each `export.json` (`Outputs`) and `snapshot.json` (`Reader`). The offline tests
(`SAM_Tas/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests`) replay the committed snapshots without TAS.

## Reproduce (licensed Windows workstation)

Prerequisites: licensed EDSL Tas (TBD/TAS3D/TSD COM servers registered), the Tas GUI closed, .NET 8 SDK,
Visual Studio MSBuild, the sibling repositories built (`SAM/build`, `SAM_gbXML/build`), and a short output path.

```text
msbuild SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\SAM.Analytical.Tas.DirectT3D.Validation.csproj -t:Restore -p:Configuration=Debug
msbuild SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\SAM.Analytical.Tas.DirectT3D.Validation.csproj -t:Build   -p:Configuration=Debug
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Run-S08-ReadBack.ps1 -Phase calibrate -Root C:\TasOut\s08b\<run> [-SkipApprovedExecutions -ReuseApprovedFrom <earlier run>]
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Run-S08-ReadBack.ps1 -Phase compare  -Root C:\TasOut\s08b\<run>
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Run-S08-ReadBack.ps1 -Phase negative -Root C:\TasOut\s08b\<run>
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Pack-S08-Evidence.ps1 -Root C:\TasOut\s08b\<run>
```

`-SkipApprovedExecutions -ReuseApprovedFrom` re-reads the persisted outputs of an earlier approved simulation and
sizing in fresh processes **without executing them again**. A new P-ALIGN simulation or Sizing=true run needs a new
owner approval.
