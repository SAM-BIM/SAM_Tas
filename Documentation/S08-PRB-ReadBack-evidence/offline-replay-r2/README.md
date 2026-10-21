# S08 PR-B revision 2: offline replay after the review fixes

**Offline only. No Tas process ran, no TBD/T3D/TSD file was opened, nothing was exported, simulated or sized.** This folder
re-compares the persisted snapshots of the licensed run `C:\TasOut\s08b\cal3` (the committed evidence of revision 1) with the
corrected comparator. The licensed evidence one level up is unchanged and keeps its own provenance and `SHA256SUMS.txt`.

| Item | Value |
|---|---|
| Why | Independent review of SAM_Tas#105 (2026-10-21) found false-PASS routes: unstated SAM schedules and a SAM space without an internal condition left out of coverage; native annual arrays compared as an 8760-element prefix; an extra native design day compared against the one SAM day; provenance that did not require valid timing or unchanged required-file hashes |
| Code | `87f6ecf389d73f70923d8aca518ea3fa5138e483` (clean tree) |
| Harness | `direct-t3d-validation.exe` Release, built from that tree by the full `SAM_Tas.sln` rebuild, sha256 `2ada4255ed4b23ce2fe538e008a35e1ea890d7512661ac8f280d43825d7fbe17` |
| Rules | committed `ReadBack/calibration-rules.json`, sha256 `766a88cdbf1294992c2b089502ad2027d6645674f514628c0a039d16a9e42c40` (unchanged) |
| Inputs | `export.json`, `input.json`, `weather.json`, `snapshot.json` copied from each `cal3` run folder; sha256 in `inputs.sha256.txt`. The four calibration runs' inputs equal the committed evidence (checked against `../SHA256SUMS.txt`), except the Sizing run's `input.json`, which is not committed: its sha256 `f6ad16f2...` equals `FrozenModelSha256` in the committed `runs/calib-box-sizing/export.json`. The negative-check snapshots were never committed; they stay machine-local (sha256 listed) |
| Date | 2026-10-21, this licensed Windows workstation (used offline) |

## Command (PowerShell, from the repository root, per run folder copy and rule set)

```text
SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\bin\Release\direct-t3d-validation.exe readback-compare --run <copy of C:\TasOut\s08b\cal3\<run>> --fixture-id <run> --rule-set interim|provisional --purpose "offline replay r2 at 87f6ecf3 of the cal3 licensed snapshot (no Tas run)" --out <copy>\compare-<rule set>
```

Runs: `calib-box`, `calib-two`, `calib-box-repeat` (both rule sets), `calib-box-sizing` (interim, as in revision 1), and the
seven negative checks (both rule sets).

## Results (`replay-summary.txt`; each `<run>/compare-<set>/report.txt`)

Every status and exit code equals revision 1's licensed compare. Only the coverage changed:

| Run | Interim | Provisional | Change from revision 1 |
|---|---|---|---|
| calib-box, calib-box-repeat | PASS (0) | PASS (0) | none |
| calib-box-sizing | PASS (0) | - | none |
| calib-two | BLOCKED owner (12) | BLOCKED owner (12) | not-pass items 2 -> 6: the unstated `ticV` slot's 6.hourly and 6.eflh in both zones are now compared, native present and zero = proposed **G1** (OD-G1), never absence |
| neg-flip-partition | FAIL (10) | FAIL (10) | 22 -> 26, the same four `ticV` G1 items |
| neg-swap-space-guid | FAIL (10) | FAIL (10) | 480 -> 484, the same four items, FAIL by identity mismatch |
| neg-layer-conductivity, neg-zero-lighting-factor, neg-shift-heating-slot, neg-add-izam | FAIL (10) | FAIL (10) | none |
| neg-nan-infiltration | BLOCKED owner (12, G3 proposed) | FAIL (10) | none |

No item was removed from any not-pass list; `checks/notpass-diff.txt` compares every list item by item.

## Checks (`checks/`)

| File | What |
|---|---|
| `review-probe-on-057d24c7.txt`, `review-probe-on-87f6ecf3.txt` | The reviewer's COM-free probe (`.review-s08-prb-20261021/Program.cs`, machine-local) against the test assembly before and after. Before: 9 mutations PASS and the missing-timestamp preconditions are empty. After: each is FAIL, BLOCKED or infrastructure (exit 70) with a named reason, and the preconditions name both missing times. The probe's in-memory "T3D changed" line still shows PASS because it calls the comparison without the preconditions; the full `readback-compare` path on the same snapshot returns exit 70 |
| `regression-tests-on-057d24c7.txt` | The 35 new `FalsePassRegressionTests` against the unfixed code: 29 fail, 6 pass (preserved behaviour: valid baseline, supported encodings, existing ordering and hash-mismatch checks) |
| `readback-tests-on-87f6ecf3.txt` | The whole read-back test project at the fix: 74 passed, 0 failed, 0 skipped |
| `library-replay-on-*.txt` | The same replay through `ReadBackCompare.Run` from a scratch console (before: the reviewed code; after: the fix), with the first not-pass items |

`SHA256SUMS.txt` lists every file here, each compressed report's uncompressed original, and the machine-local series-element
files that were not committed (only `calib-box` and `calib-two` interim are committed).
