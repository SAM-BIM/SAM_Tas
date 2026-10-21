# S08 PR-B revision 3: offline replay after the second review's fixes

**Offline only. No Tas process ran, no TBD/T3D/TSD file was opened, nothing was exported, simulated or sized.** This folder
re-compares the persisted snapshots of the licensed run `C:\TasOut\s08b\cal3` with the revision-3 comparator. The licensed
evidence one level up and `../offline-replay-r2/` are unchanged.

| Item | Value |
|---|---|
| Why | Independent review of revision 2 (head `cc414876`, 2026-10-21) found two false-PASS routes: an unusable native film input (inclination, surface type) replaced by an assumed Horizontal/External film in the derived U (3b), and a snapshot without the calendar or its days unmasking every annual schedule while the alignment rules kept it Verified |
| Code | `bc867f8ae15ec5c2b99d99234e8020edfe08a3a5` (clean tree) |
| Harness | `direct-t3d-validation.dll` Release, built from that tree by the full `SAM_Tas.sln` rebuild, sha256 `b9c3ea32422127ad5e4e82701c545371873892231ff04ae535bd2ad15155e128`; it contains the revision-3 messages. The `.exe` (sha256 `2ada4255...`) is the .NET apphost stub: it does not change with the code, so it identifies no revision (the r2 README's harness hash is that stub too) |
| Rules | committed `ReadBack/calibration-rules.json`, sha256 `766a88cdbf1294992c2b089502ad2027d6645674f514628c0a039d16a9e42c40` (unchanged) |
| Inputs | `export.json`, `input.json`, `weather.json`, `snapshot.json` copied from each `cal3` run folder to `C:\TasOut\s08b\replay-r3` (machine-local); sha256 in `inputs.sha256.txt`, all 44 equal `../offline-replay-r2/inputs.sha256.txt` |
| Date | 2026-10-21, this licensed Windows workstation (used offline) |

## Command (from the repository root, per run folder copy and rule set)

```text
SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\bin\Release\direct-t3d-validation.exe readback-compare --run <copy of C:\TasOut\s08b\cal3\<run>> --fixture-id <run> --rule-set interim|provisional --purpose "offline replay r3 at bc867f8a of the cal3 licensed snapshot (no Tas run)" --out <copy>\compare-<rule set>
```

Runs: `calib-box`, `calib-two`, `calib-box-repeat` (both rule sets), `calib-box-sizing` (interim), and the seven negative
checks (both rule sets): 21 comparisons.

## Results

**No difference from revision 2** (`checks/r2-vs-r3-diff.txt`, item by item): every status and exit code, every not-pass item
(none removed or added), every result's status, outcome, rank, named blocker, proposed case, owner decision, reason, expected
and native text and series counts, and every series-element file (sha256 equal to the r2 hashes, committed and
machine-local). This is expected: every cal3 snapshot has a complete calendar (days 1..365 typed), inclinations 0/90/180
only, and surface types tbdExposed, tbdGround and tbdLink only, so neither changed route is reached.

| Run | Interim | Provisional |
|---|---|---|
| calib-box, calib-box-repeat | PASS (0) | PASS (0) |
| calib-box-sizing | PASS (0) | - |
| calib-two | BLOCKED owner (12): proposed G1 (OD-G1) | BLOCKED owner (12) |
| neg-add-izam, neg-flip-partition, neg-layer-conductivity, neg-shift-heating-slot, neg-swap-space-guid, neg-zero-lighting-factor | FAIL (10) | FAIL (10) |
| neg-nan-infiltration | BLOCKED owner (12, G3 proposed) | FAIL (10) |

## Checks (`checks/`)

| File | What |
|---|---|
| `review-probe-on-cc414876.txt`, `review-probe-on-bc867f8a.txt` | The reviewer's COM-free probe (`.review-s08-prb-r2-20261021/Program.cs`, machine-local; copy with redirected output in `review-probe-Program.cs.txt`). Before: all 7 mutations PASS (exit 0), including through the full compare. After: each is BLOCKED technical (exit 11); baselines PASS |
| `regression-tests-on-cc414876.txt` | The 30 new `FalsePassRegressionTests` against the reviewed code: 26 fail, 4 pass (preserved behaviour) |
| `readback-tests-on-bc867f8a.txt`, `core-tests-on-bc867f8a.txt` | Read-back 104 passed, core 249 passed; 0 failed, 0 skipped |
| `r2-vs-r3-diff.txt` | The item-by-item comparison above |

`SHA256SUMS.txt` lists every file here, each compressed report's uncompressed original, and the 21 machine-local
series-element files (not committed again: each equals r2's).
