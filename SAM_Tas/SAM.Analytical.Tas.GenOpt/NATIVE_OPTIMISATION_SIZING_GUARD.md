<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation: no zone setpoint or glazing optimisation on frozen Sizing capacities (PR7b-2 D1, option (a))

Branch `feature/optimisation-sizing-guard` → base `sow/2026-Q4` (cut from `28b59fcb`). Record date: 2026-10-12. This is SAM-BIM/SAM_Tas#97; companion SAM-BIM/SAM_UI#224. Owner
decision (2026-10-12): **D1 option (a) for V1**; per-candidate re-sizing (option (b)) deferred to V2. Defect and options:
`NATIVE_OPTIMISATION_PR7B2.md` (SAM-BIM/SAM_Tas#95). Companion: SAM-BIM/SAM_UI#224 (window text; depends on this PR).

## Current status

Implemented, regression- and mutation-tested, licensed-checked. SAM-BIM/SAM_Tas#97 open for owner review; not merged.

## The rule (measured, not assumed)

Zone heating/cooling setpoints and glazing (the "TBD targets") are optimised and applied only when **every TBD zone's
heating and cooling sizing is `tbdSizing` or `tbdDesignSizingOnly`**. Measured on copies (driver `capprobe`): a zone
capacity set to 50 W before `simulate` is sized again by it for those two types (Systems Demo: 1 593.76 W restored, heating
identical; Sizing off: 209 632 W restored), and stays 50 W for `tbdNoSizing` (Sizing on: heating 11 615.7 → 6 410.4 kWh) —
the D1 mechanism. Everything else refuses (fail-safe):

| `TasCapacitySizing` | When | TBD targets |
|---|---|---|
| `SizedEverySimulation` | every zone 1 or 2 (Systems Demo; Energy Simulation with Sizing off; production Sizing on leaves unheated zones 1, e.g. `SAM_zoningAM` — why PR7b-2's Sizing S1/S2/DR held) | offered |
| `Fixed` | any zone `tbdNoSizing` (0), **whatever its capacity** (the system sizing method writes 0) | refused |
| `Unrecognised` | any other type (`tbdMixed` 3, `tbdLoadPerArea` 4, unknown values: not measured) | refused |
| `Unknown` | not read, or no zone | refused |

The proposed "`tbdNoSizing` and capacity > 0" detector was validated against production code and narrowed the other way:
production Energy Simulation (`WorkflowCalculator.cs:757`, ExcludePositiveInternalGains) leaves **heated** zones
`tbdNoSizing` with capacities and **unheated** zones `tbdDesignSizingOnly`; the latter are sized again by every simulation,
so they are safe; `tbdNoSizing` with 0 is still frozen, so capacity is not part of the rule.

## Where it is enforced (no caller can bypass it)

1. **Target discovery** — `Query.TasModelCatalogue` lists no TBD target unless sized every simulation; its `Source` carries
   the reason (the AI assistant's text prints it: `AVAILABLE (…)`). Plant controllers and every measure stay.
2. **Definition validation** — `TasModelRunner` refuses each TBD variable as **OPT617** (path `variables[i].target`, the
   reason, hint "… or run Energy Simulation with Sizing off first"), before the catalogue check's generic OPT609.
3. **Execution** — the generated script calls `RefuseFixedCapacities(tbdBuilding)` before its first TBD edit: the **TBD file
   itself**, in every evaluation, whatever inventory a caller passed (licensed L3: a forged inventory is refused there).
4. **Apply** — `TasModelDesignApplier.Apply` refuses a TBD change on the run's inventory before anything is copied;
   `WriteTbd` checks the **TBD file itself** before writing (glazing systems included). A mixed change (TBD + controller)
   is refused whole; a controller-only change is applied; a TBD target already at its best value needs no write.

Unchanged: plant-controller-only scripts (`controller.csx` identical), Energy Simulation's sizing copies (`_Uncapped`,
`_HDDCDD`) still excluded from inventory, snapshot, hashes and Apply (`SizingCopyTests` pass; L1 counts the primary only).

## V1 limitations (documented for users)

- On folders from Energy Simulation with **Sizing on** (the default), heated models offer **no zone setpoint or glazing
  optimisation**; plant controllers are offered.
- "Run Energy Simulation with **Sizing off** first" makes them available but is **not equivalent**: Tas then sizes plant
  within every simulation without a design-day limit (ModelA heating ~17× the Sizing-on value). Results found that way hold
  for Sizing-off simulations only.
- A Tas-authored TBD whose engineer fixed capacities on purpose (`tbdNoSizing`) is refused too.
- Per-candidate re-sizing (option (b)) is V2: it needs the model's oversizing factors and the Energy Simulation dialog's
  sizing settings, which the Tas folder does not hold.

## Files changed

- New: `Classes/TasModel/TasZoneSizing.cs` (zone sizing, `TasCapacitySizing`, the measured constants),
  `Query/TasCapacitySizing.cs` (`SizedEverySimulation`, `CapacitySizing`, `TasModelSizingRefusal`, `SizingTypeText`, the
  TBD reader and file check).
- `Classes/TasModel/TasModelInventory.cs` (`ZoneSizing`, `CapacitySizing`), `Query/TasModelInventory.cs` (reads it),
  `Query/TasModelCatalogue.cs`, `Classes/TasModel/TasModelRunner.cs` (OPT617), `Create/TasScript.cs` (script check),
  `Classes/TasModel/TasModelDesignApplier.cs`.
- Tests: `SizingGuardTests.cs` (new), `Helpers/StandInTbdProbe.cs` (new: runs the script's check on stand-in zones), Apply
  tests in `TasModelApplyTests.cs`; fixtures with measured zone sizing (`TasModelFixtures`: Demo, `ModelASizingOn/Off`,
  `Sized()`); 3 snapshots (`setpoints`, `glazing`, `everything`).

## Validation

- `SAM.Analytical.Tas.GenOpt.Tests` **400/400** (352 + 48) on GenOpt `E4A6DFC7`, test bin verified.
- Mutation (`C:\TasOut\pr7b-2\d1-guard\mutate*.ps1`, each mutant's build verified by hash, source restored and verified):
  7 of 7 caught — catalogue offers regardless (5 fail), no OPT617 (4), no script call (5), script accepts every heating
  type (5), no Apply check (4), unmeasured types accepted (9), "not read" allowed (5).
- Licensed (Tas 2.0.0.1, copies, bounded, one Tas operation at a time; `C:\TasOut\pr7b-2\d1-guard\EXPECTATIONS.md` written
  before; `steps.log` D1-L*): L1 ModelA Sizing on → `Fixed` (Cell 1/2 `0`/1 879.1 W), no setpoint/glazing, reason in the
  source, sizing copies not counted; L2 → OPT617, no run folder; **L3 forged inventory → the evaluation fails in
  TasGenExecute on the TBD's own zones**; L4 `WriteTbd` refused, TBD byte-identical; L5 ModelA Sizing off → offered, Test
  = its Energy Simulation TSD (203 901.699810553 kWh); L6 Systems Demo → offered, B1 15 227.8941594962 and P1
  27 387.7943115234 / 3 932.8885345459 bit-identical to PR7b-2; L7 Demo with all 26 zones `tbdNoSizing` → only its 6
  controllers, P1 still 27 387.7943115234, setpoints OPT617. No Tas process left.
- Evidence (local): `C:\TasOut\pr7b-2\d1-guard` (`EXPECTATIONS.md`, `inputs\` with hashes, `runs\`, `apply\`, `build\`
  with the trx files and mutation logs, the mutation scripts) and `C:\TasOut\pr7b-2\steps.log` (`capprobe-*`, `d1-zones-*`,
  `D1-L*`). The checks used the PR7b-2 spike driver (`capprobe`, committed in SAM_Tas#95) extended locally with
  `forge-sized`, `applytbd` and `setsizing` and the sizing lines in `catalogue`; that source (`C:\TasOut\pr7b-2\driver\Program.cs`)
  uses this PR's API, so it goes into the spike folder only after this PR merges.
- Trial merge with SAM-BIM/SAM_Tas#96 (local branch, discarded): no conflict; the merged result passes **426/426**.

## Next step

Owner review; CI; merge only with owner approval (merge commit, `--match-head-commit`): this PR first, then the SAM_UI
companion (SAM_UI#224) after its CI is rerun against the merged SAM_Tas Q4; then the `PROJECT_PROGRESS.md` closeouts. If
SAM-BIM/SAM_Tas#96 merges first, this branch takes Q4 and regenerates the plant snapshots (both change `everything.csx`).
