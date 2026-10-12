<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation: a failed plant simulation fails the evaluation (PR7b-2 defect D2)

Branch `feature/optimisation-plant-simulate-status` → base `sow/2026-Q4` (cut from `28b59fcb`). Record date:
2026-10-12. Found by the PR7b-2 licensed acceptance follow-up (record of record: `NATIVE_OPTIMISATION_PR7B2.md`,
SAM-BIM/SAM_Tas#95, whose follow-up section holds the evidence).

## Current status

Fixed, regression-tested, licensed-verified. Open for owner review; not merged.

## Defect (measured)

The generated "tas-model" script discarded the status that `IPlantRoom.SimulateEx` returns (a `string`). On the owner's
SAM-created TPD (`000000_SAM_AnalyticalModel-It1a-futureZ1-It3B`, Energy Simulation with Create TPD, 2026-09-11; copies)
TAS answers **"Sizing Flow Failed"** in 0.14 s on this workstation, and the script then read the result set as the
evaluation's result: annual plant energy **0.249599888920784 kWh** (the TPD's stored value is 3 176.26 kWh) and CO2
**NaN**, reported as a **successful** evaluation, identical at every controller value (10 and 30 °C). SAM_Tas's own reader
(`SystemEnergyCentreResults`) on a saved copy agrees with the script (0.2496 / NaN), so the readers are not at fault: the
simulation did not run. A golden-section run would have optimised over that constant and offered it to Apply.

Isolation (`spikes/optimisation-pr7b2` driver `simulate`, one argument changed at a time): "Sizing Flow Failed" under the
script's arguments `(1, 8760, 0, …, Load+Pipe, 0, 0)` and under SAM_Tas Create.TPD's own `(1, 8760, 15, …,
Load+Pipe+Duct+SimEvents+Cont, 1, 0)` alike; the Systems Demo answers **"Done"** under both (27 387.79 / 27 385.77 kWh).
So the arguments are not the cause; this TPD cannot size its plant here (a fixture condition, reported separately).

## Fix

`Create.TasScript` generates `PlantSimulated(plantRoomName, status)` and passes every plant room's `SimulateEx` answer
to it before the results are read. It applies SAM_Tas's existing, measured policy (`SAM.Core.Tas.SimulationDiagnostic`,
already used by SAM_Tas' own TPD simulation): an answer containing one of its **measured failure fragments** throws
`Plant room "<name>" was not simulated: Tas answered "<status>".` (TasGenExecute writes it to Error.txt, the evaluation
fails, the kernel's existing failure handling applies); every answer — success, silence, or one not yet measured — is
written to `tas-model-trace.txt`. The fragment list is generated from `SimulationDiagnostic.KnownFailureFragments`, so the
script and SAM_Tas cannot drift apart. No other block of the script changes; building-only scripts are unchanged.

**Decision (assumption for review):** an unmeasured answer is traced, not refused, exactly as `SimulationDiagnostic`
decides for SAM_Tas's own simulation route; only "Done" has been observed as success. Refusing anything but "Done" would be
stricter but could refuse valid runs on a TAS version answering differently.

## Files changed

- `SAM.Analytical.Tas.GenOpt/Create/TasScript.cs` — the helper, the call, the remarks.
- `SAM.Analytical.Tas.GenOpt.Tests/TasScriptTests.cs` — 11 cases (below); `Helpers/TasScriptCompiler.cs` — `Load`, which
  compiles and loads a generated helper so a test can run it.
- `SAM.Analytical.Tas.GenOpt.Tests/Golden/TasModel/controller.csx`, `everything.csx` — the two plant snapshots
  (regenerated with `SAM_TAS_UPDATE_SNAPSHOTS=1`; `setpoints.csx`/`glazing.csx` unchanged).
- This record.

## Validation

- Regression tests run the script's **own generated** `PlantSimulated` (extracted, compiled at C# 7.0, invoked): the four
  measured failures refuse with the exact message; every `SimulationDiagnostic` fragment refuses; "Done", " Done ",
  "", null and an unmeasured answer are traced, not refused; every plant room is checked before `GetResultSet`;
  building-only scripts carry no check.
- Mutation: with `TasScript.cs` reverted to `28b59fcb`, 13 of the new/affected tests fail (12, then the tightened
  fragment test); restored, all pass.
- `SAM.Analytical.Tas.GenOpt.Tests` **363/363** (352 before + 11), on GenOpt DLL `1C606D32` (deterministic rebuild).
- Licensed (Tas 2.0.0.1, this workstation, copies only, one Tas operation at a time, bounded): CS Test at 10 °C now
  fails with `Plant room "Main PlantRoom" was not simulated: Tas answered "Sizing Flow Failed".`; a golden-section Run on
  it stops with outcome **EvaluationFailed** after 2 evaluations (8 s), no best point, nothing to apply; **Systems Demo
  control** P1 (HeatPumpController 3): 27 387.7943115234 kWh / 3 932.8885345459 kgCO2e, **bit-identical** to the PR7b-2
  run before the fix, trace `plant room "Plant Room" simulated: Done`. No Tas process left; sources' hashes unchanged.
  Evidence (local): `C:\TasOut\pr7b-2\followup-2026-10-12` (`EXPECTATIONS.md`, `simulate\`, `runs\`, `runs-d2\`,
  `build\genopt-d2-final.trx`, `build\mutation-d2.log`) and `C:\TasOut\pr7b-2\steps.log`.

## Risks / not covered

- An answer TAS has not yet been seen to give on failure still passes (traced). The building stage's `tbdDocument.simulate`
  return is not checked either; it has never been observed to fail silently and is not changed here.
- The It3B TPD's "Sizing Flow Failed" on this workstation is not explained (it simulated on the owner's machine when
  created). Not optimisation code.

## Next step

Owner review; CI; merge (merge commit, `--match-head-commit`); then the `PROJECT_PROGRESS.md` closeout on
`sow/2026-Q4`. Independent of SAM_Tas#95 and SAM_UI#223 (either order).
