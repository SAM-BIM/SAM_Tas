<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation: a failed plant simulation fails the evaluation (PR7b-2 defect D2)

Branch `feature/optimisation-plant-simulate-status` → base `sow/2026-Q4` (cut from `28b59fcb`). Record date:
2026-10-12. This is SAM-BIM/SAM_Tas#96. Found by the PR7b-2 licensed acceptance follow-up (record of record: `NATIVE_OPTIMISATION_PR7B2.md`,
SAM-BIM/SAM_Tas#95, whose follow-up section holds the evidence).

## Current status

Fixed, regression-tested, licensed-verified; tightened on review (2026-10-12) to a **success allowlist** (below).
SAM-BIM/SAM_Tas#96 open for owner review; not merged.

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
to it before the results are read. The answer is trimmed, then:

1. it contains one of `SAM.Core.Tas.SimulationDiagnostic`'s **measured failure fragments** → throws
   `Plant room "<name>" was not simulated: Tas answered "<answer>".`;
2. otherwise, unless it equals (case-insensitively) one of `SimulationDiagnostic`'s **measured success answers** ("Done") →
   throws `Plant room "<name>" did not report success: Tas answered "<answer>" | nothing; accepted: "Done".`;
3. every answer is written to `tas-model-trace.txt`.

A throw goes to Error.txt, the evaluation fails and the kernel's existing failure handling applies. Both lists are
generated from `SimulationDiagnostic.KnownSuccessAnswers`/`KnownFailureFragments`, so the script and SAM_Tas cannot drift
apart, and a failure fragment wins over a success word ("Done, sizing failed" is a failure), as in `Classify`. No other
block of the script changes; building-only scripts are unchanged.

**Decision (review, 2026-10-12): allowlist, not "refuse only measured failures".** The first version followed
`SimulationDiagnostic`'s own policy, which accepts silence and unknown answers — but that class states the policy is safe
only because "the decisive gate is the complete ZoneTemperature reconciliation". The tas-model script has no such gate:
whenever the plant did not run, the result set still holds the TPD's previous results, which look like valid numbers
(D2). So silence or an unknown answer must fail the evaluation. "Done" is validated as `SimulateEx`'s success answer:
Systems Demo, plant-only and building+plant chains, every argument set tried (below). A TAS version answering success
differently would be refused loudly with its answer in the message — visible and fixed by adding the measured answer to
`SimulationDiagnostic`; the opposite error (accepting a failure) would be silent.

**Non-finite objective (D1 owner review, 2026-10-12).** A successful status alone does not make a valid result. The
evaluator accepts `NaN`/`Infinity` as values; a NaN objective never wins (`NativeGenOptOutcome`) but golden section and
Hooke–Jeeves compare against it. The script now calls a generated `RefuseNonFinite(objectiveName, value)` after the trace
is written and before `Result`: a NaN or infinite **objective** throws `The objective "<name>" is NaN|Infinity|-Infinity,
not a number, so this simulation has no result to compare.` and the evaluation fails. Recorded outputs are written as they
are (a NaN there is shown, never optimised); the tas-model engine has no constraints (OPT414), so there is nothing else to
check. No measured fixture yields a non-finite objective after "Done", so this is covered by tests, not by a licensed case.

## Files changed

- `SAM.Analytical.Tas.GenOpt/Create/TasScript.cs` — the two helpers, their calls, the remarks.
- `SAM.Analytical.Tas.GenOpt.Tests/TasScriptTests.cs` — 26 cases (below); `Helpers/TasScriptCompiler.cs` — `Load`, which
  compiles and loads a generated helper so a test can run it.
- `SAM.Analytical.Tas.GenOpt.Tests/Golden/TasModel/*.csx` — the four snapshots (regenerated with
  `SAM_TAS_UPDATE_SNAPSHOTS=1`: the plant check in `controller`/`everything`, the objective check in all four).
- This record.

## Validation

- Regression tests run the script's **own generated** `PlantSimulated` (extracted, compiled at C# 7.0, invoked): the four
  measured failures refuse with the exact message; every `SimulationDiagnostic` fragment refuses; "Done", " Done ",
  "done", "DONE\r\n" and every `SimulationDiagnostic` success answer are accepted and traced; null, "", "   ",
  "Simulation finished", "Not done" and "Done." refuse with the exact "did not report success" message; "Done, sizing
  failed" is a failure; every plant room is checked before `GetResultSet`; building-only scripts carry no check.
- Mutation (first version): with `TasScript.cs` reverted to `28b59fcb`, 13 tests fail; restored, all pass.
- Mutation (allowlist): with the generator reverted to the fail-open `cf50b665`, the 6 silence/unknown cases and both
  plant snapshots fail (test bin verified to load the mutant DLL `D506FED0`; a first `--no-build` attempt still loaded
  the allowlist DLL and is void); restored (test bin `99D92EF7`), all pass.
- `SAM.Analytical.Tas.GenOpt.Tests` **370/370** (352 before + 18) on GenOpt `99D92EF7`; first version 363/363;
  re-verified 370/370 at head `9b6823c2` (2026-10-12, D1 owner review).
- Objective check: the script's own `RefuseNonFinite` refuses NaN, +Infinity and −Infinity with the exact message and
  accepts 0, −5.5, 1e300 and `double.Epsilon`; every chain shape checks its objective once, after the trace, before
  `Result`. **378/378** (+8) on GenOpt `6A1F74F0`. Mutation: call removed → 5 fail; NaN accepted → 5 fail (each mutant's
  test bin verified by hash; restored `6A1F74F0`).
- Licensed (Tas 2.0.0.1, this workstation, copies only, one Tas operation at a time, bounded): CS Test at 10 °C now
  fails with `Plant room "Main PlantRoom" was not simulated: Tas answered "Sizing Flow Failed".`; a golden-section Run on
  it stops with outcome **EvaluationFailed** after 2 evaluations (8 s), no best point, nothing to apply; **Systems Demo
  control** P1 (HeatPumpController 3): 27 387.7943115234 kWh / 3 932.8885345459 kgCO2e, **bit-identical** to the PR7b-2
  run before the fix, trace `plant room "Plant Room" simulated: Done`. No Tas process left; sources' hashes unchanged.
- Licensed, allowlist build (`99D92EF7`), targeted (the success path changed): Demo P1 again bit-identical
  (27 387.7943115234 / 3 932.8885345459, `Done`); Demo building + plant chain (HJ definition, Test at its start point
  20, 24, 3) **bit-identical on all four outputs** to PR7b-2's HJ first evaluation (27 387.8365478516, 15 227.8941594962,
  2 978.52016448265, 3 932.89582824707; `Done`); It3B still refused ("Sizing Flow Failed"). No Tas process left.
  Evidence (local): `C:\TasOut\pr7b-2\followup-2026-10-12` (`EXPECTATIONS.md`, `simulate\`, `runs\`, `runs-d2\`,
  `runs-d2b\`, `build\genopt-d2-final.trx`, `build\genopt-d2b-final.trx`, `build\mutation-d2*.log`) and
  `C:\TasOut\pr7b-2\steps.log`.
- Licensed, with the objective check (`6A1F74F0`; steps `D2N3-*`, separate driver `C:\TasOut\pr7b-2\driver-d2` from #95's
  `3e5182cc`, every run's `Script.txt` verified to hold `PlantSimulated` and `RefuseNonFinite`): Demo P1 and the HJ start
  point again **bit-identical**, `Done` traced; It3B refused. Two earlier attempts (`D2N-*`, `D2N2-*`) ran the D1 build's
  driver by mistake and are marked void in `steps.log`.

## Risks / not covered

- A recorded (non-objective) output can still be NaN after "Done": it is shown as NaN and never optimised.
- The building stage's `tbdDocument.simulate` returns an `int` that is not checked. It writes a fresh `model.tsd` (no TSD
  is copied in that chain), so a run that did not happen leaves no TSD and the evaluation fails when it is opened; a
  partially written TSD is the residual case. Measured since (D1 `capprobe`, four runs): `simulate` returned **−1** on
  success; one value is not yet an allowlist, so it is not checked here (a candidate follow-up).
- The It3B TPD's "Sizing Flow Failed" on this workstation is not explained (it simulated on the owner's machine when
  created). Not optimisation code.

## Next step

Owner review; CI; merge only with owner approval (merge commit, `--match-head-commit`); then the `PROJECT_PROGRESS.md`
closeout on `sow/2026-Q4`. Independent of SAM_Tas#95 and SAM_UI#223. The D1 guard PR (`feature/optimisation-sizing-guard`)
also changes the generated script: merge this one first, then that branch takes Q4 and regenerates its snapshots.
