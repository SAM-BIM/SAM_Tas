# S10 licensed acceptance evidence (revision 2, attempt 2)

**Status: S10 accepted by the owner (2026-10-23) with one declared validation limitation. This is not an unqualified
full PASS.** The limitation is `SAM|FixB|Heating|designDayPeak.hourOfDay` on `s08-fix-two`, which stays **BLOCKED** in
these files (see below). Record: `Documentation/S10-TasResultsRoundTrip-PR.md`.

## Run

- Run root `C:\TasOut\s10\acc2`, machine-local; copy it before switching machines. It holds the TBD/TSD files whose
  hashes are in `produced.json` and the read snapshots.
- Workstation LEGION5MD, Tas 2.0.0.1, 2026-10-23 18:33:26Z–18:34:48Z.
- Code: content `9871f437`, run at record-only head `a170cdf3`; SAM `2cfd6909`.
- The binaries came from `Build-S10-Receipt.ps1`, receipt sha256 `c36bb2066d8168b1c4a154ff2b1c139b64b485d6f80f07c9d60ef9f97a81f822`.
- The preflight in `provenance-run.json` is proven. It recorded:
  - `samCommit` `2cfd6909`;
  - `runnerCommit` `9871f437`, the built commit, which benchmark-tas also received;
  - `runnerHead` `a170cdf3`.
- Inputs in `inputs.json`: the committed fixtures (`ReadBack/Fixtures/SHA256SUMS.txt`) and the Budapest EPW `472ffb4e…`.

```text
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Build-S10-Receipt.ps1
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Run-S10-Results.ps1 -Phase run -Root C:\TasOut\s10\acc2 -WeatherEpw C:\TasOut\s08c\weather\budapest.epw
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Pack-S10-Evidence.ps1 -Root C:\TasOut\s10\acc2 -Target <folder>
```

The pack was made into a machine-local folder, independently checked and copied here unchanged.

## What ran (the authorised bound)

- Exactly 2 `benchmark-tas` simulations, one per approved fixture.
- 12 steps in `watchdog-run.json`, all completed, none timed out.
- `status-run.json`: `infrastructure: completed`. No retry.
- Every read left the produced TBD/TSD/JSON/B1a unchanged (`produced-after-reads.json`, `changed: []`). The fixtures were
  unchanged afterwards.

## Results

| Fixture | Comparison (`runs/*/compare/`) | Negatives (`runs/*/negatives/`, `expect=PASS`) |
|---|---|---|
| `s08-fix-box` | **PASS 59/59**, exit 0 | exit 0, all 52 outcomes as intended |
| `s08-fix-two` | **BLOCKED: 94 PASS, 0 FAIL, 1 BLOCKED**, exit 11 | exit 10, 51 of 52 as intended; the unmutated `control` is BLOCKED |

These outcomes are kept as measured. The acceptance decision does not change any status, tolerance or file here.

## Declared validation limitation (the one BLOCKED row)

`SAM|{D267F93B-CB70-4981-9095-8298DEF76FBB}|Heating|designDayPeak.hourOfDay` (zone FixB, `s08-fix-two`):

- **What is checked:** FixB's heating design-day load (622.7639770507812 W), the native peak index and load, the annual
  heating load and hour, and every B1a row all pass.
- **What is not checked:** the clock hour of that design-day peak. Nothing independent places the heating design day in
  time:
  - the captured heating lighting and equipment gains are constant zero;
  - the TBD heating weather dry bulb is constant.
- **Why it cannot be derived from the series alone:** FixB has 12 exact maxima at alternating raw slots. Its two load
  values differ by only 0.00042724609375 W, which is consistent with numerical noise in an effectively steady load. That
  is an engineering inference, not proof of the clock origin.
- **The other zone:** FixA's heating load is constant over all 24 hours, so its hour row passes for any origin.
- **What the decision covers:** the guard correctly keeps the row BLOCKED, and S10-C9 stays unresolved for heating. The
  owner accepted this one row as a declared limitation of this attempt. This is **not** a general acceptance of
  unsupported heating design-day hours.
- **For later stages:** the limitation must stay visible in later roadmap, Phase B and release evidence. Closing it would
  need independent heating timing evidence and separately authorised work.

For the same reason, the two-zone `negatives.md` header reads "NOT all as intended". The only miss is `control`, expected
PASS and blocked by that same row. All 51 mutations FAIL or BLOCK as intended.

## N/A negative controls

"As intended" counts include cases that do not apply to an input, so they do not mean every mutation was exercised
natively:
- **Box:** 46 applicable, 6 N/A: `f4-b1a-hour-is-raw-index`, `f6-zero-peak-hour-0-available`, `sam-hour-plus-1`,
  `sam-hour-minus-1`, `sam-non-maximal-hour`, `tsd-non-unique-zone-name`.
- **Two-zone:** 51 applicable, 1 N/A: `f6-zero-peak-hour-0-available`.

F6, the all-zero mutation, has no native case on either fixture. Its evidence remains the offline synthetic and producer
tests.

A diagnostic replay without `expect=PASS` uses the measured baseline and would accept the BLOCKED control. That is not
acceptance evidence. The independent review reproduced exit 10 with `expect=PASS`.

## Files and hashes

- `SHA256SUMS.txt` lists every packed file. Large JSON is stored gzip-compressed (`*.gz`) and listed by the sha256 of
  the uncompressed original.
- This README is not listed, because the reviewed `SHA256SUMS.txt` is kept byte-for-byte.
- Produced-file hashes:

| Fixture | TBD | TSD | SAM JSON | B1a |
|---|---|---|---|---|
| `s08-fix-box` | `fcc945cb…` | `047a51e9…` | `c4080fa9…` | `82ecfa5b…` |
| `s08-fix-two` | `85216403…` | `8109d47c…` | `510b8b09…` | `90354820…` |

Attempt 1 (`C:\TasOut\s10\acc1`) is preserved and is not part of this pack.
