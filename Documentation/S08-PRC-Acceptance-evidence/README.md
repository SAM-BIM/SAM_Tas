# S08 PR-C licensed acceptance evidence

Run root `C:\TasOut\s08c\acc1` (machine-local; copy it before switching machines: it holds the TBD/T3D files whose hashes
are in each `export.json` and `snapshot.json`). Workstation LEGION5MD, Tas 2.0.0.1 (2025-06-04), 2026-10-21.

Harness `direct-t3d-validation.dll` sha256 `9cbdf5bb3f83ae276dd51a378cf95b648e90c4dd5d5f219f92022876631a1758`, rebuilt
(MSBuild `-t:Rebuild`, Debug) from the clean commit `62ce26cc`; `calibration-rules.json` sha256 `766a88cd…` (the frozen
PR-B table, unchanged). Inputs (`inputs-acceptance.json`): the committed fixtures (`ReadBack/Fixtures/SHA256SUMS.txt`)
and `HUN_BU_Budapest.Met.Center.128400_TMYx.epw` sha256 `472ffb4e…` (SAM `b721c5f4`), both hash-checked before any export.

```text
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Run-S08-ReadBack.ps1 -Phase acceptance -Root C:\TasOut\s08c\acc1 -WeatherEpw C:\TasOut\s08c\weather\budapest.epw
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Run-S08-ReadBack.ps1 -Phase acceptance-negative -Root C:\TasOut\s08c\acc1
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Pack-S08C-Evidence.ps1 -Root C:\TasOut\s08c\acc1
```

The EPW is `gzip -dc` of `SAM/SAM/SAM.Tests/Resources/Weather/HUN_BU_Budapest.Met.Center.128400_TMYx.epw.gz`.

## What ran (exactly the authorised bound)

- `acceptance` (17 steps, 0 not as expected; `watchdog-acceptance.json`): 3 exports on the contract route (Direct,
  UseWidths=false, Sizing=false, Simulate=false, AddIZAMs=false): `s08-fix-box`, `s08-fix-two`, `s08-fix-box-repeat`
  (writer control); 5 fresh-process reads (each fixture twice, the repeat once); 2 reader diffs and 1 writer diff;
  6 comparisons (3 runs x `approved`, `provisional`).
- `acceptance-negative` (35 steps, 0 not as expected; `watchdog-acceptance-negative.json`): 7 mutations on disposable
  copies, 7 reads, 14 comparisons; originals hash-identical before and after for every case.
- No simulation, no sizing, no recalibration. Every export's frozen inputs were byte-identical after the run.

## Results

| Run | approved (verdict) | provisional (pre-decision baseline) |
|---|---|---|
| `s08-fix-box` (V2) | **PASS** exit 0, 353/353, coverage complete | PASS |
| `s08-fix-two` (V1, V1) | **PASS** exit 0, 623/623, coverage complete | BLOCKED owner (exit 12): the 6 ticV rows, proposed G1 |
| `s08-fix-box-repeat` | PASS | PASS |

Controls: reader 0 of 77,748 (box) and 0 of 81,410 (two-zone) leaf values differ; writer 0 of 77,748.

Non-MATCH pass items, all listed in `report.json`: the box's unframed window frame area is a verified ACCEPTED-TRANSFORMATION
(T-FRAME 0 %, N-3.3); in the two-zone model rows 6.hourly, 6.eflh and 8b of `ticV` in both zones are ACCEPTED-TRANSFORMATION
under `T-VENT-INACTIVE` (native value profile, factor 1, value 0; 8760 schedule values and delivered flows exactly 0);
NOT-APPLICABLE are the 8a bases SAM does not state and the closed windows' 8d opening.

T3D field checks (writer-only identity, never gated): `framePerc` agrees but stays NOT RUN; `frameWidth` disagrees with the
writer rule as in PR-B (0.075 frame thickness expected, 0.065 saved = the construction's DefaultFrameWidth).

Negative checks (each FAIL, exit 10, under both rule sets): swapped `[SpaceGuid]` carriers (identity, 1a/2), flipped
partition `reversed` (3a), wall conductivity x 1.5 (3a/3b), lighting factor 0 (4a/4b), infiltration factor NaN (7a/7b; G3
EXPORT-DEFECT now approved), heating slots rotated 1 h (5), added IZAM (native object SAM never stated).

Watchdog: `TBD.exe` stayed resident after each export and was killed between steps; the Windows Application log has 12
`TAS3D.exe` faults, one per T3D read, the known shutdown crash on this workstation. Every read's hashes were unchanged.

## Files

`runs/<run>/`: `export.json`, the reader or writer diff, `snapshot.json.gz`, `compare-{approved,provisional}/report.{txt,json.gz}`,
and for the two fixtures `compare-approved/series-elements.csv.gz` (every series element) and the frozen `weather.json.gz`.
`negatives/neg-*/`: `export.json` and both reports. `logs/`: every step's stdout/stderr. `SHA256SUMS.txt`: every file and
the uncompressed original of each `.gz`. The frozen model of each run is the committed fixture itself (same sha256).
Manifests keep the workstation name and local repository paths as provenance; no credentials or binaries are included.

Offline replay: `AcceptanceEvidenceReplayTests` re-compares both fixture snapshots from these files and reproduces every
committed verdict item by item.
