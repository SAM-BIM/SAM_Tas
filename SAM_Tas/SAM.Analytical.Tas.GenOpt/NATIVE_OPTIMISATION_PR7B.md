<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR7b-1: the "tas-model" engine (catalogue reader, script generator, glazing choice)

Branch `feature/optimisation-tas-model` → base `sow/2026-Q4` (cut from `4abad64b`). Record date: 2026-10-09.

Plan of record: SAM_UI `documentation/NativeOptimisation-Plan-ModelBindings.md` (PR7b row, "Follow-up: construction and
glazing choice"). Inputs settled before this PR:

- PR7a `NATIVE_OPTIMISATION_PR7A_SPIKE.md`: kinds, keys, units, chain rule, blocks, F1/F2, TasGenExecute code rules.
- PR7a-2 `NATIVE_OPTIMISATION_PR7A2_GLAZING.md`: the glazing swap and its owner decisions (`PROJECT_PROGRESS.md`):
  - glazing is a choice among real systems, ordered by g, run by "try every option";
  - the continuous g-value is not offered;
  - filter defaults as answer 4;
  - the model's frames are kept.
- SAM#190 (`documentation/OptimisationDefinition-TryEveryOption-PR.md` in SAM): the "try every option" method,
  `MaximumOptions`, and the "For PR7b" capabilities and kernel mapping.

**Split (as the hand-over allows):** this is **PR7b-1**: the engine, capabilities, catalogue, generator, run mapping,
stub tests, plus a licensed smoke of every block. The full licensed acceptance matrix is **PR7b-2** (see "Not done").
SAM_Tas only; no SAM, SAM_UI or Grasshopper change.

## Current status

PR open, **not merged**. Code, tests and the licensed smoke are complete; awaiting PR CI and **owner review**. Nothing is
wired into SAM_UI (PR8).

## What was built

All new code is in `SAM.Analytical.Tas.GenOpt` (netstandard2.0).

| API | What it does |
|---|---|
| `Query.TasModelEngine` = `"tas-model"`, `Query.TasModelCapabilities()` | The engine and what it runs (below). One shared instance in a holder class (as `tas-script`, so no other `Query` member loads SAM.Core.Optimisation). `tas-script` is unchanged. |
| `TasModelKind` | The kinds, reference keys, the `threshold` parameter (default 28 °C) and `MaximumGlazingOptions` (8). |
| `Query.TasModelInventory(projectFolder)` | Licensed, read-only COM read of the folder's single TBD/TSD/TPD into plain data (`TasModelInventory`): internal conditions with their thermostat profiles, the transparent constructions the building elements use (found through the elements, past the `GetConstruction(i)` null gaps), annual heating/cooling demand of the TSD, plant rooms and controllers, the TPD's stored annual results and their cost unit. |
| `Query.TasModelCatalogue(inventory, glazingPool, glazingFilter)` (+ an overload taking the folder) | The `OptimisationCatalogue`: what the model offers, with current values, units, suggested ranges and the glazing options. No COM, so it is unit-tested. |
| `Query.TasGlazingSystems(constructionManager, source)` | One pool source calculated with SAM_Tas' `ThermalTransmittanceCalculator.CalculateGlazing` (licensed TCD, STA). |
| `Query.TasGlazingOptions(current, pool, filter)`, `TasGlazingFilter`, `TasGlazingSystem`, `TasGlazingOption` | The PR7a-2 option filter with the owner's defaults. |
| `Create.TasScript(definition, inventory, glazingOptions)` | The TasGenExecute script, generated from PR7a/PR7a-2's proven blocks. |
| `Convert.ToSAM_TasModelKernel(definition)` → `TasModelKernel` | The kernel mapping: optimiser, problem, `Variables.txt` parameters, `Output.txt` objectives. |
| `TasModelRunner(definition, TasModelRunSettings)` with `.Run()` and `.Test()` | Checks the definition against the capabilities **and** the model's catalogue, resolves the glazing options and generates the script, all before any folder is created. `.Run()`: run folder, glazing systems written into the snapshot TBD, then the kernel. `.Test()`: "Test one simulation" (one evaluation, with its duration). |
| `TasGenExecuteObjectiveEvaluator.Evaluate(simulation, coordinates)` | New public single-evaluation entry. It shares the code of the kernel's calls, so the evaluator stays the only class that starts a process (PR6's IL scan still passes). |

### Capabilities (`tas-model`)

- Methods: golden section (1 variable), Hooke–Jeeves (1..∞), try every option (1). Minimise only. Continuous and
  discrete. No constraints.
- Targets:

  | Kind | Name in the window | Keys | Unit |
  |---|---|---|---|
  | `tbd.internal-condition.heating-setpoint` | Zone heating setpoint | `internalCondition` | °C |
  | `tbd.internal-condition.cooling-setpoint` | Zone cooling setpoint | `internalCondition` | °C |
  | `tbd.glazing-construction.choice` | Glazing system | `glazingConstruction` | — (accepts options, at most 8) |
  | `tpd.controller.setpoint` | Plant controller setpoint | `plantRoom`, `controller` | — (depends on the sensor) |

  `tbd.glazing-construction.g-value` is **not** offered.
- Measures:

  | Kind | Unit |
  |---|---|
  | `tsd.annual-heating-demand` | kWh |
  | `tsd.annual-cooling-demand` | kWh |
  | `tsd.overheating-hours` | h; `threshold` parameter, default 28 °C, 20–40 °C |
  | `tpd.annual-energy` | kWh |
  | `tpd.annual-cost` | GBP |
  | `tpd.annual-co2` | kgCO2e |

### Catalogue rules

- **Setpoints:** one target per internal condition (PR7a owner decision 1).
  - Offered for value and 24-hour profiles with factor 1. Heating is not offered at "no heating" (≤ −50 °C), cooling
    not at "no cooling" (≥ 150 °C).
  - Exact, untrimmed names (the Demo's `"Steady State Heating "`).
  - Suggested range: heating 16–24 °C, capped at the condition's cooling setpoint; cooling 21–28 °C. Both are widened
    to include the current value.
  - SAM's heating-design-day conditions (`"<space> - HDD"`) are not listed.
- **Glazing choice:** one entry per transparent construction the elements use, when the pool gives at least one option
  besides the current glazing.
  - The options are its `options`, in order: option 1 is the current glazing (its TBD construction name), the others
    follow in g order.
  - The entry's description lists every option with g, Ug, light and source, as the window will show them.
- **Controllers:** every controller of every plant room.
  - °C for a temperature sensor, otherwise no unit.
  - No suggested range.
- **Measures:**
  - Heating/cooling demand and overheating (threshold 28) when there is a TBD or TSD.
  - Plant energy and CO2 when there are a TPD and a TBD.
  - Plant cost only when the TPD's stored cost results are in `"£"`.

### Glazing options (PR7a-2 answer 4, owner defaults)

- Accepted systems:
  - the filter's aperture type ("Window") and transparent;
  - finite g, Ug and light;
  - g within the optional range;
  - Ug ≤ current U + 0.3 W/m²K and light ≥ current light − 0.1 (both compared to 0.001).
- Duplicates (g, Ug, light to 0.001) count once, and so does a system equal to the current glazing.
- At most 8 options, the current glazing included. When more pass, the kept ones are spread evenly over the g order;
  the lowest and highest are always kept.
- Option text: the system's name, with its 6-character short id when two systems of that aperture type share the name.
- Each pool system's TBD pane name is SAM_Tas' own naming of the system renamed `"<name> <short id>"`, for example
  `"Windows: SIM_EXT_GLZ (copy) a5191c -pane"`.
- `TasModelRunner.WriteGlazingSystems` writes the systems into the **run's snapshot TBD only**, with
  `Modify.UpdateConstructions`, and saves it.
  - It refuses a name the TBD already has, because that writer overwrites by name (PR7a-2).
  - It verifies each pane construction exists and is transparent.

### Generated script (C# 7.0)

| Rule | How |
|---|---|
| Language and references | C# 7.0. `using System.Linq;` is written out. Only the BCL and the TBD/TSD/TPD interops; no TCD, no `#r`. |
| Model item names | Escaped literals; the script is pure ASCII. |
| Internal names | Variables are `V1..Vn` and outputs `Y1..Ym`, objective first; `Result` = the objective. A label can then be any text: commas would break `Variables.txt`, and `::` would break `Output.txt`. |
| Copy | TBD, TSD (when no building simulation) and TPD are copied into the evaluation folder; the snapshot is never written. |
| Building | Runs when a variable targets the TBD, or when there is no TSD to read. Every setpoint and glazing block, read back, then `save`, `simulate(1, 365, 0, 1, 0, 0, tsd, 1, 0)`, wait for the TSD, `save`, `close`, release. |
| TSD | Read-only, and only the arrays the outputs need. Daily arrays are enumerated (1-based). Overheating counts occupied hours (occupant sensible gain > 0) with resultant temperature **strictly** above each threshold, per zone, and keeps the worst occupied zone. Several thresholds are read in one pass. |
| Plant | Without a building simulation, the TSD's `buildingPath` is pointed at the evaluation's TBD first (F1). Then `FixTSDPath`; the controller setpoint is written **as a double**; the Demo's `SimulateEx` runs for every plant room; every result is read; the result set is disposed; the TPD is released (no `Save`, F2). The TPD must use exactly one TSD. |
| Read-back outputs | Setpoints `Vk.Before/.Read/.Hours`, glazing `Vk.Option/.g/.U/.Light/.Elements`, controller `Vk.Before/.Read`, stage times `t.*`. A trace goes to `tas-model-trace.txt`. |

### Run mapping

- **Golden section and Hooke–Jeeves:** exactly the `tas-script` mapping (PR4). It uses the same GenOpt algorithm and
  settings objects, then `ToSAM_Optimiser`; a parameter is start or minimum, minimum, maximum, step or 0. A test pins
  the equality.
- **Try every option:** `SAM.Math.TryEveryOption` with the simulation limit (2000 by default) and one parameter
  `(V1, 1, 1, n, 1)`.
  - Option k is the definition's k-th option.
  - The kernel's table is one `OptionEvaluated` entry per option, then the `MinimumPoint`.
  - `NativeGenOptOutcome` gives the best option.
- `NativeGenOptRun.ParameterNames` and `ObjectiveNames` carry the definition's names.

## Owner decisions (chosen here; please confirm or redirect)

1. **Heating-design-day conditions are not offered.** SAM writes a `"<space> - HDD"` condition per space; the annual
   simulation does not use the design day.
2. **Every plant room is simulated**, and a TPD must use exactly one TSD.
   - The Systems Demo has one plant room, so it is unchanged.
   - A multi-room energy centre is unproven (no such model here).
3. **No suggested range for a controller setpoint** (the window or the AI asks the user). PR7a's Demo range was −5…35.
4. **A pool system equal to the current glazing** (g, U, light to 0.001) is not a separate option. This includes the
   model's own system coming from the library.
5. **Option names:** the short id is added when two systems *of the choice's aperture type* share a name. A library
   door named like a window does not make the window's name ambiguous. The PR7a-2 wording said "two pool systems".
6. **More than 7 candidates:** they are spread evenly over the g order, lowest and highest kept, rather than the 7 nearest
   the current g.
7. **Cost** is offered only when the TPD's stored annual cost results are in `"£"`. A TPD that was never simulated
   has no stored results, so it offers no cost.
8. **The evaluation's TPD is not saved** (the Demo script saves it). The copy is discarded, and F2's crash happens
   with or without the save.
9. **New dependency:** `SAM.Analytical.Tas.GenOpt` now references `SAM.Analytical.Tas` and the TBD/TSD/TPD interops
   (not embedded). They ship with SAM_Tas already; PR10 must deploy them beside the GenOpt DLL as today.

## Licensed smoke (this workstation, 2026-10-09)

- **Inputs:** copies only.
  - The Systems Demo `pr7a\base-local` (SHA-256 equal to PR7a's record).
  - PR7a's SAM-generated Part O model (`pr7a\inv-sam`: TBD and TSD).
  - The glazing pool: SAM's default library and "My glazing systems", calculated by `TasGlazingSystems` (TCD).
- **Driver:** a local console calling `TasModelInventory`, `TasModelCatalogue`, `TasModelRunner.Test`/`Run` with the
  installed `TasGenExecute.exe`.
- **Raw evidence** (catalogue dumps, every run folder): `C:\TasOut\pr7b\`.

| Block | Case | Result | Reference |
|---|---|---|---|
| Inventory | Demo: 7 internal conditions, 2 glazing constructions (Suncool Example on 3 elements, Rooflight), 6 controllers, cost unit £ | read in 1.1 s | PR7a inventory |
| | Demo stored TSD results | heating 15 227.840663709641, cooling 2 978.5325259896517 kWh | PR7a baseline |
| Heating setpoint (value profile) | Demo, Office Weekday, 22 °C | heating 20 909.4574699934, cooling 3 042.89228880054 kWh, 0 h; 10.2 s | PR7a: 20 909.46 / 3 042.89 |
| Cooling setpoint (value profile) | Demo, Office Weekday, 26 °C | cooling 1 644.4083095012 kWh, 22 h > 28 °C (B1_Circulation 3); 9.1 s | PR7a: 1 644.41 / 22 |
| Cooling setpoint (24-hour profile) | SAM model, Studio 1_0, 23 → 26 °C | cooling 1 123.32056516266 kWh, 12 hours rewritten, 33 h; 7.2 s | PR7a: 1 123.32 |
| Controller (plant only, F1 re-point) | Demo, HeatPumpController 3 | cost 7 363.16638183594, CO2 3 932.8885345459, energy 27 387.7943115234; 21.5 s | PR7a: 7 363.17 / 3 932.89 / 27 387.79 |
| | HeatPumpController −5 | cost 7 390.51611328125, CO2 3 685.87437438965, energy 26 358.7982177734; 25.5 s | PR7a plant-only: 7 390.53 / 3 685.88 / 26 358.83 (†) |
| Glazing choice (try every option, `Run`) | Demo: Suncool Example / SIM_EXT_GLZ (copy) / SIM_INT_GLZ, frames kept, threshold 25 | option 1: cooling 2 978.53252598965, heating 15 227.8406637096, 274 h; option 2: 3 219.90 / 14 728.92 / 355 h; option 3: 3 934.92031348047 / 21 166.1359825536 / 508 h; best = option 1; ~7 s each | option 1 = PR7a-2 baseline **bit for bit**; option 3 = PR7a-2 "frames kept" row (3 934.92 / 21 166.14 / 508) |
| | Applied values read back | g/U/light of each option equal the catalogue's (g 0.7917975 = 0.7917975187…) | PR7a-2 parity |
| | SAM model: current / SIM_EXT_GLZ (copy) | option 1: 32 h, 2 523.75187299657 kWh; option 2: 31 h, 2 496.39 kWh | option 1 = PR7a/PR7a-2 baseline bit for bit |
| Golden section (`Run`, plant) | Demo, HeatPumpController −5…35, tolerance 0.1 | 11 evaluations, ~22 s each; best **4.968943799848584**, cost 7 360.03466796875, CO2 4 076.69, energy 27 996.70; Success | the same best point and the same 11 evaluations as the PR3/PR5 GenOpt acceptance (cost 7 360.04370117188 there: the Demo wrote the setpoint as a float, and the TSD differs (†)) |

(†) PR7a's plant-only rows used the original Demo TSD (`pr7a\base`, SHA-256 `babe6c3c…`). `base-local`'s TSD
(`40f169af…`) was re-simulated on this workstation, and its plant results equal PR7a's building+plant rows. The
difference is the input TSD, not the block.

- **Clean-up:** every evaluation exited 0; no Tas process was left; no `Error.txt` content.
- **Registry:** the TasManager registry export was identical before and after (`695ccc5e…`, equal to PR7a's).
- **Two defects found by the smoke and fixed in this PR:**
  - The inventory reader passed a relative path to the TBD server. The COM server has its own working folder, so the
    open faulted; the reader now uses full paths.
  - TPD's `WrResultSet.Dispose()` once faulted (`RPC_E_SERVERFAULT`) after the values had been read, the F2 family.
    The reader now ignores faults on release.

## Tests (no licence; CI runs them)

`SAM.Analytical.Tas.GenOpt.Tests`: **299/299** (257 before, + 42). New suites:

- `TasModelCapabilitiesTests`:
  - the kinds, keys, units, methods and threshold; `tas-script` unchanged;
  - `glazing-choice.json` runnable with a catalogue offering its options, and OPT614 without one;
  - the g-value fixture is OPT601;
  - the zone-setpoint fixture without the g-value runs;
  - `systems-demo-bound-golden-section.json` against the Demo catalogue is OPT609 ("Plant Room 1" → hint "Plant
    Room") and runs with the Demo's name. **SAM is not edited**; a SAM follow-up can change the fixture;
  - the `tas-script` fixtures are OPT608;
  - the AI text offers the choice and `try-every-option`.
- `TasModelCatalogueTests`:
  - the Demo and SAM-model catalogues line by line (names, keys, current values, ranges, units);
  - the trailing-space name; the heating cap;
  - HDD, no-heating, no-cooling, factor and unsupported profiles skipped;
  - no TPD and TSD-only cases; cost only in £;
  - the glazing entry and description;
  - every filter rule at its boundary; 8 options spread over the g order; short ids.
- `TasScriptTests`:
  - one snapshot per chain shape: building setpoints with two thresholds, plant-only controller, glazing choice,
    building+plant;
  - every snapshot compiles at **C# 7.0** against the build-only interops and a TasGenComm stand-in (from the
    installed `TasGenComm.dll`'s public surface);
  - the compiler check itself rejects C# 7.1, TCD types and LINQ without `using`;
  - ASCII only; Result and every Y/V present; the chain-rule order (F1 before FixTSDPath; results before the release);
  - escaping of quotes, backslashes and non-ASCII names;
  - refusals: no TPD, no options, a detached option, no TBD;
  - no TSD → a building simulation.
- `TasModelRunnerTests`:
  - the choice → `TryEveryOption` (V1, 1, 1, n, 1);
  - golden section equal to PR4's mapping, and Hooke–Jeeves;
  - non-runnable definitions throw before any folder exists (OPT601, OPT609);
  - stub end to end:
    - the glazing choice tries 3 options; the writer gets the two pool systems;
    - `Variables.txt` = `V1,2,1,3,1,System.Double`;
    - the best is option 3;
    - Script.txt is the generated script;
  - a controller "Test one simulation" plus a golden-section run;
  - a failing option is retried once, then the run stops with no best.

Mutations (each applied, built, run against the new suites, reverted and touched): **18/18 caught**.

| Mutation | Failed |
|---|---|
| M1 glazing choice limit 9 instead of 8 | 2 |
| M2 g-value target offered | 3 |
| M3 heating design day conditions offered | 1 |
| M4 "no cooling" 150 offered | 2 |
| M5 Ug limit exclusive | 1 |
| M6 duplicates kept | 3 |
| M7 options not spread (first n by g) | 1 |
| M8 cost offered in any currency | 1 |
| M9 plant-only without the F1 re-point | 2 |
| M10 controller setpoint written as float | 3 |
| M11 overheating counts ≥ threshold | 3 |
| M12 Result is the last output | 5 |
| M13 TPD released before the results are read | 3 |
| M14 try every option over options + 1 | 2 |
| M15 glazing options in pool order, not the definition's | 1 |
| M16 short id never appended | 1 |
| M17 runner skips the catalogue check | 1 |
| M18 glazing writer not called | 1 |

After the last revert the unmutated build passed again.

## Files changed

- New in `SAM.Analytical.Tas.GenOpt/`:
  - `Classes/TasModel/`: `TasModelKind`, `TasModelInventory`, `TasInternalConditionInfo`, `TasSetpointProfile`,
    `TasGlazingConstructionInfo`, `TasPlantRoomInfo`, `TasGlazingSystem`, `TasGlazingFilter`, `TasGlazingOption`,
    `TasModelKernel`, `TasModelRunSettings`, `TasModelRunner`, `TasModelTest`;
  - `Enums/TasSetpointProfileType.cs`;
  - `Query/`: `TasModelCapabilities`, `TasModelInventory`, `TasModelCatalogue`, `TasGlazingSystems`,
    `TasGlazingOptions`;
  - `Create/TasScript.cs`, `Convert/ToSAM_TasModelKernel.cs`;
  - this record.
- Changed:
  - `SAM.Analytical.Tas.GenOpt.csproj`: references to SAM.Analytical.Tas, the interops and SAM.Core/Analytical/Geometry;
  - `Classes/Native/TasGenExecuteObjectiveEvaluator.cs`: the single-evaluation entry; the request path is unchanged.
- Tests:
  - the csproj: Roslyn 4.11.0, SAM.Analytical/Geometry/Architectural/Analytical.Tas, the snapshots;
  - `TESTING.md`;
  - the four new suites, `Helpers/{TasModelFixtures,TasGenCommStandIn,TasScriptCompiler}.cs`, `Golden/TasModel/*.csx`;
  - the stub's `Program.cs` (spec from `SAM_TAS_GENOPT_STUB_SPEC` when Script.txt is C#).

## Validation

- Step 0 on this workstation: SAM `288c9f57`, SAM_Tas `4abad64b`, SAM_UI `96e20c9e`, SAM_Systems `5404926`, all at
  `origin/sow/2026-Q4`, rebuilt in order (scratch profile, real `NUGET_PACKAGES`).
- `MSBuild SAM.Analytical.Tas.GenOpt.csproj -p:Configuration=Release`: 0 errors, no new warnings in the new files.
- Full `SAM.Analytical.Tas.GenOpt.Tests`: see Tests. The 257 existing tests pass unchanged, including PR6's IL scan
  and PR4's load-context test.
- The licensed smoke as above.
- `git diff --check` clean; SPDX headers on every new `.cs`.

## Not done (PR7b-2, licensed acceptance) and why

PR7b-1 proves every block once against the PR7a/PR7a-2 numbers. The full matrix is left for PR7b-2, so this PR stays
reviewable:

- the direction of every target on **both** models (heating on the SAM model needs a model with heating on);
- each measure compared with SAM_Tas' readers on these evaluation folders directly (here it is transitive, through
  PR7a's numbers, which equalled the readers);
- a glazing choice of 4–5 options on both models, and a larger "My glazing systems" pool;
- a Hooke–Jeeves run with setpoints and a controller together;
- repeat runs, durations and the F2 crash rate over many plant evaluations;
- a multi-plant-room TPD.

## Unresolved issues and risks

- SAM's fixture `systems-demo-bound-golden-section.json` still says `"Plant Room 1"` (SAM follow-up, test data only).
- The heating setpoint direction on a SAM-generated model is unproven (Part O model with heating off).
- `Modify.UpdateConstructions` still overwrites by name; the runner avoids it with unique names and a refusal.
- `SAM_ApertureConstructionLibrary.JSON`'s malformed `SIM_EXT_GLZ` Guid (PR7a-2) still gives that system a new Guid on
  every load. It is deduplicated by values, so it never becomes an option twice, but its short id is not stable across
  sessions.
- TPD.exe's shutdown fault (F2) can also hit the catalogue reader; it is ignored after the values are read.

## Next step

1. PR CI (`build`, `spdx`) green on the head.
2. **Owner review** of this PR and the owner decisions above. Merge (merge commit, `--match-head-commit`) only after
   approval, then the `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4`.
3. Then PR7b-2 (licensed acceptance matrix above), or PR8 (SAM_UI journey) if the owner accepts PR7b-1's smoke as
   enough.
