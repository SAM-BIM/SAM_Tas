# Project Progress - SAM_Tas (2026-Q4)

## Branch

`sow/2026-Q4` - bootstrapped 2026-10-06 from `master` `28ac11a7`. Frozen Q3 record: `sow/2026-Q3` @ `7d4ff52f` (not modified).

## Last updated

2026-10-09 (native Optimisation PR7b-1 closeout, SAM_Tas#91).

## Current status

Q4 branch cut from `master` `28ac11a7`, which is the exact commit pinned in SAM_Deploy's frozen Q3 baseline (`v20261006.1`). Bootstrap added only internal docs (this file, `AGENTS.md`). First Q4 product work: the opt-in Direct SAM -> T3D route, merged as #84 (see the Direct T3D section below). The gbXML route remains the default. Second Q4 stream started: the Java-free GenOpt replacement (PR1-T evidence merged as #85; native Tas GenOpt route merged as #86; legacy Java route retired and result rules shared as #87; see the GenOpt sections below). Native Optimisation definition stream: the Tas adapter for the SAM.Core.Optimisation definition merged as #88 (see the PR4 section below). The licensed spike for the `tas-model` blocks (PR7a, evidence only) merged as #89 and the glazing-swap spike (PR7a-2, evidence only) as #90. SAM's "try every option" PR merged as SAM#190. The `tas-model` engine (PR7b-1: catalogue reader, script generator, glazing choice, run mapping) merged as #91; next: PR7b-2 (licensed acceptance matrix) or PR8 (SAM_UI journey), owner's choice (see the PR7b-1 section below). Open: #92 (reader-defect evidence; owner decision on the consumption units).

## Q4 priorities

Not yet set by the owner. Record them here at the first Q4 planning pass. Known carry-over work is listed below.

## Known carry-over work

- Branch `codex/part-o-cooling-control-room` - Q3 complete: all commits already in sow/2026-Q3.
- Branch `codex/part-o-pr2-diagnostics` - Q3 complete: all commits already in sow/2026-Q3.
- Branch `codex/pr5b-generic-table-roundtrip` - owner decision: 6 commits not in Q3 (last 2026-09-15).

## Repository-specific next steps

- Await Q4 planning; first candidates are the Direct T3D follow-ups below. Open PRs for Q4 work against `sow/2026-Q4`.
- Follow the continuity convention in `AGENTS.md` for every PR and closeout.

## Decisions / assumptions

- Q4 base is `master` `28ac11a7`; the internal files were recovered from `sow/2026-Q3` into this branch only, never onto `master`.
- Q4 history intentionally does not contain the Q3 branch history (the maintained `master` is the promoted Q3 line, which is not a descendant of `sow/2026-Q3`); the frozen `sow/2026-Q3` branch is the permanent record.
- Historical Q2/Q3 content below is kept as evidence; its branch names, SHAs and next steps describe Q3 and are not current instructions.

## Validation

- Bootstrap verified 2026-10-06: `sow/2026-Q4` was created at exactly `28ac11a7` and the push was a normal (non-forced) branch creation.

## Issues / blockers

- None at bootstrap.

## Next step

- Owner to set Q4 priorities; then start the first Q4 task from this branch.

## Q4 operational cleanup (2026-10-06)

- Reviewed every active Q2/Q3 reference in this repository on `sow/2026-Q4` (workflow branch filters, dependency-branch resolution, `.gitmodules`/validation, docs). Historical Q2/Q3 mentions (feature documentation records, the frozen Q3 section below) are intentionally unchanged.
- Changed (`dd69cac`): removed the dead `$candidates += 'sow/2026-Q2'` fallback from the dependency-branch resolution in `.github/workflows/build.yml`. No dependency repository has a `sow/2026-Q2` branch, so the entry never matched and resolution already fell through to the default branch; behaviour is unchanged (PR head ref, current sow ref, then the dependency's default branch) and no per-quarter edit is needed.
- Checked, no action: the `github.repository_owner == 'SAM-BIM'` build guard (intentional; its comment names HoareLea only to explain why the guard exists), CODEOWNERS (SAM-BIM owners), and workflow secrets (no HoareLea-named secret). The local `upstream` (HoareLea) remote is preserved.
- Full cross-repository record, migration table and owner decisions: `SAM_Deploy:sow/2026-Q4` `PROJECT_PROGRESS.md`.

## Q4 runtime-URL cleanup (2026-10-06)

- **Status:** complete. SAM-BIM/SAM_Tas#83 merged into `sow/2026-Q4` as merge commit `3e918cfa6534e190b670b748e79353224a66e2d4` (PR head `00ced2b8e1caaedf19d4002ad34d24895f3d3e86`, Q4 base `f624ebc`); merge method: merge commit (repository convention). Remote and local `fix/sam-bim-runtime-urls-q4` removed.
- **Work completed:** The text written into the TBD building description is now `Delivered by SAM https://github.com/SAM-BIM/SAM [date]` instead of the `HoareLea/SAM` URL (the string is only written, never parsed). SAM-BIM is the authoritative ecosystem; HoareLea is no longer the synchronised operational source. Record: the PR's `SAM-BIM-RuntimeUrls-Q4.md` document.
- **Decisions / owner classifications:** The `//TODO ... github.com/HoareLea/...` comment in `UpdateSurfaceShades.cs` is a historical issue reference: KEEP. Assembly author/contact strings (`Hoare Lea`, `@hoarelea.com` in `Kernel/AssemblyInfo.cs`) are provenance/metadata, not repository ownership: KEEP unchanged.
- **Files changed:** `SAM_Tas/SAM.Analytical.Tas/Modify/UpdateZones.cs`, `Documentation/SAM-BIM-RuntimeUrls-Q4.md` (1 product line).
- **Validation:** `msbuild SAM_Tas.sln -p:Configuration=Release` (.NET Framework MSBuild, APPDATA/USERPROFILE redirected): 0 errors; `SAM.Analytical.Tas.dll` contains the new text and not the old. TAS-COM-dependent tests not run locally (none references the string). PR CI build and spdx green.
- **Unresolved issues, risks:** None introduced.
- **Next step:** None for this change.

## Direct T3D import route (2026-10-07)

- **Status:** complete. SAM-BIM/SAM_Tas#84 (`feature/t3d-direct-import`) merged into `sow/2026-Q4` as merge commit `f9202503e6142d1a19cde964cbe833de30973ea2`. Design record: `SAM_Tas/SAM.Analytical.Tas/DIRECT_T3D_ROUTE.md`.
- **Work completed:** opt-in route `SAM AnalyticalModel -> TAS direct importer (WrImportIDF) -> T3D -> TBD`, selected by `WorkflowSettings.T3DRoute` (`GbXML` default, `Direct` explicit opt-in; a missing/old serialized value resolves to `GbXML`). COM-free `T3DImportPlan` describes zones/surfaces/apertures/shades and is unit-testable; `Convert.ToT3D` replays it into TAS. SAM space GUID is kept in the zone description, SAM space name in the zone name.
- **Decisions / assumptions:**
  - Widths OFF is intentional: SAM polygons already represent the physical shell, and both routes then reproduce SAM area/volume exactly. Widths ON differs only through TAS's two importers treating element thickness differently.
  - Storey name is taken from the SAM level name (fixed).
  - One TAS window per aperture is intentional: one window per `ApertureConstruction` makes TAS collapse openings sharing a window object into one TBD opening.
  - `UpdateReversed` is required for asymmetric internal constructions (`reverseElement` is not reliable); on Direct, `UpdateT3D` and the adiabatic assign/set stages are skipped.
  - `exposedPerimeter`/`facadeLength` differ on adiabatic boundaries (Direct excludes adiabatic wall lengths). Known, documented, non-blocking: no effect on any simulated series.
- **Files changed:** 46 files (+8641/-17): `T3DImportPlan`/`TasCoordinates`/`UpdateSpaces`/`ZoneDescription` and related Query/Convert/Modify code, `WorkflowSettings`, tests, the licensed validation harness `SAM.Analytical.Tas.DirectT3D.Validation`, `DIRECT_T3D_ROUTE.md`.
- **Validation:** geometry and TBD validation, real-model validation (9 zones; Direct and gbXML equivalent) and a load-sensitive three-zone validation through full-year simulation all completed; Direct and gbXML thermal inputs and results accepted as equivalent for the tested models. Final: 1128 tests passed, licensed synthetic suite passed, full solution build passed. The licensed harness is not part of CI.
- **Unresolved issues, risks:** one substantial real model plus the synthetic fixtures only; no real project exercises `ExternalSpace`; curved boundaries are skipped, not discretised; some mapping logic duplicates legacy `UpdateT3D`.
- **Next steps (follow-ups, each its own feature branch + PR):** expose `T3DRoute` in SAM_Tas_Grasshopper; expose `T3DRoute` in SAM_UI; broader real-project validation; `ExternalSpace` validation; curved-boundary handling; consider reducing the duplicated mapping logic.

## Java-free GenOpt replacement — PR1-T, TasGenExecute protocol evidence (2026-10-07)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#85 (`feature/tasgenexecute-protocol-evidence`) merged into `sow/2026-Q4` as merge commit
    `1873e5786a6b29c216dfbdf401df4b45e2ddfa6a` (PR head `a353487c4b3ac33a1bcd3e4909aa52772e35942e`, Q4 base `f213f50a`).
  - Merge method: merge commit, head-commit protected. Remote and local branch removed.
  - Docs only. Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/TASGENEXECUTE_PROTOCOL.md`.
  - Companion of SAM-BIM/SAM#182 (GenOpt semantic oracle, PR1; see SAM's `PROJECT_PROGRESS.md`).
- **Work completed:**
  - Reconstructed how Tas Generic Optimisation drives Java GenOpt 3.1.1 (IL of `TasGenOpt.exe`, `TasGenExecute.exe`,
    `TasGenComm.dll`) and which layers exist only because GenOpt is external Java.
  - Gate T, licensed Tas: `TasGenExecute.exe "<workspace>"` runs **directly, without Java or GenOpt**, with the working
    directory set to a fresh evaluation folder holding `Variables.txt`.
- **Findings:**
  - The Tas Manager registry (`Modify.SetProjectDirectory`) is not needed.
  - `TasOutputs.txt` is not read.
  - `Output.txt` is appended to: the first line is `<date>::Result::<v>`, values have 15 significant digits.
  - Error classes: script exception and compile error give exit 0 with `Error.txt`; a missing `Script.txt` or
    `Variables.txt` gives exit `0xE0434352` and no files.
  - Results were bit-identical to the 2025-12-05 Java GenOpt run on 4 distinct points.
  - Two concurrent runs in separate folders succeeded.
- **Decisions / assumptions:**
  - The future evaluator (PR3) invokes `TasGenExecute.exe` directly in a unique per-evaluation folder.
  - Failure = non-zero exit code, non-empty `Error.txt`, or no `Result::` line.
  - Phase 1 stays sequential.
  - No EDSL model, script or binary committed; the scratch Tas copies stayed local.
- **Files changed:** `SAM_Tas/SAM.Analytical.Tas.GenOpt/TASGENEXECUTE_PROTOCOL.md` (new).
- **Validation:** licensed Tas run, T1–T7 in the record. PR CI: `build` and `spdx` green.
- **Unresolved issues, risks:**
  - TasGenExecute is an external EDSL binary (path hard-coded in `Query.TasGenOptDirectory`, exit code not meaningful,
    culture-dependent output).
  - Owner decisions D1–D4 are tracked in SAM's PR1 record and progress file.
- **Next step:** after D1–D4, SAM PR2 (`feature/native-optimiser-kernel`, kernel in `SAM.Math`), then SAM_Tas PR3
  (`feature/native-genopt-tas-evaluator`: GenOpt compatibility adapter + `TasGenExecuteObjectiveEvaluator` + workspace
  isolation + licensed acceptance).
  - **Update:** D1–D4 were resolved and SAM PR2 merged as SAM#183 (`0989ad81`). PR3 merged as #86 (see below).

## Native Optimisation PR7b-1, the `tas-model` engine (2026-10-09)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#91 (`feature/optimisation-tas-model`) merged into `sow/2026-Q4` as merge commit
    `0e3a2aa0a8cbaecdc57d98f824aeaa0bf146c442`. Its parents are the Q4 base `4abad64b` and the reviewed PR head
    `581853a850375429fb57fe3dd7450bae35543a55`.
  - Merge method: merge commit with `--match-head-commit`, after the owner's approval on that head. CI on the head:
    `build` (it runs the GenOpt tests) and `spdx` green. No review comments. Remote and local branch deleted.
  - Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7B.md`. This is PR7b-1 of a split allowed by the
    hand-over; PR7b-2 (full licensed acceptance) is not started.
- **Work completed** (`SAM.Analytical.Tas.GenOpt`):
  - engine `tas-model` (`Query.TasModelCapabilities`):
    - methods: golden section (1 variable), Hooke-Jeeves (1..n), try every option (1); minimise; continuous and
      discrete;
    - targets: heating/cooling setpoint per TBD internal condition, `tbd.glazing-construction.choice` (display "Glazing
      system", at most 8 options), TPD controller setpoint;
    - measures: annual heating/cooling demand (kWh), overheating hours (threshold 28 °C, 20-40), plant energy (kWh),
      cost (GBP), CO2 (kgCO2e);
    - the g-value target is not offered; `tas-script` is unchanged;
  - `Query.TasModelInventory(folder)`: a licensed read-only COM reader; `Query.TasModelCatalogue`: pure, unit-tested;
  - `Query.TasGlazingSystems` (TCD calculator); `Query.TasGlazingOptions` (the PR7a-2 filter with the owner defaults);
  - `Create.TasScript`: the C# 7.0 TasGenExecute script from the PR7a/PR7a-2 blocks, with positional names `V1..`/`Y1..`
    and `Result`;
  - `Convert.ToSAM_TasModelKernel`: golden section and Hooke-Jeeves exactly as PR4; try every option `(V1, 1, 1, n, 1)`;
  - `TasModelRunner`:
    - checks the definition against the capabilities and the model's catalogue (OPT609) before any folder exists;
    - writes the glazing systems under unique names into the run's snapshot TBD only;
    - `Run()` and `Test()` (one evaluation);
  - `TasGenExecuteObjectiveEvaluator.Evaluate(simulation, coordinates)`: a single evaluation through the same code.
- **Owner decisions taken by the agent and accepted with the approval of #91:**
  1. heating-design-day conditions (`"<space> - HDD"`) are not offered;
  2. every plant room is simulated, and a TPD must use exactly one TSD;
  3. no suggested range for a controller setpoint;
  4. a pool system equal to the current glazing is not a separate option;
  5. the short id is added only when names clash within the choice's aperture type;
  6. more than 7 candidates are spread evenly over the g order;
  7. cost is offered only when the TPD's stored cost results are in "£";
  8. the evaluation's TPD is not saved;
  9. `SAM.Analytical.Tas.GenOpt` now references `SAM.Analytical.Tas` and the TBD/TSD/TPD interops (not embedded).
     PR10 must deploy them beside it, as today.
- **Files changed:** 38 (+4783/-5).
  - 13 classes under `Classes/TasModel/`, 5 `Query` files, `Create/TasScript.cs`, `Convert/ToSAM_TasModelKernel.cs`,
    the enum, the csproj, the evaluator entry, the record;
  - tests: 4 suites, 3 helpers, 4 snapshots `Golden/TasModel/*.csx`, the csproj (Roslyn 4.11.0), `TESTING.md`, and the
    stub's spec from `SAM_TAS_GENOPT_STUB_SPEC`.
- **Validation:**
  - `SAM.Analytical.Tas.GenOpt.Tests` 299/299 (257 + 42), including every snapshot compiled at C# 7.0 against the
    build-only interops and a TasGenComm stand-in;
  - 18/18 mutations caught; full `SAM_Tas.sln` Release 0 errors;
  - licensed smoke on copies (`C:\TasOut\pr7b`, local): every block reproduced PR7a/PR7a-2:
    - Demo heating 22 °C: 20 909.46 kWh; cooling 26 °C: 1 644.41 kWh / 22 h;
    - SAM model, 24-hour cooling 23→26 °C: 1 123.32 kWh;
    - controller 3: cost 7 363.17;
    - glazing choice: option 1 = baseline bit for bit; SIM_INT_GLZ with frames kept = 3 934.92 / 21 166.14 / 508 h;
    - a full golden-section `Run` converged to 4.968943799848584 in 11 evaluations (the PR3/PR5 point);
    - TasManager registry unchanged; no leftover Tas process.
- **Defects found by the smoke and fixed in #91:**
  - Tas COM servers need full paths: a relative folder gave `RPC_E_SERVERFAULT`;
  - the TPD `WrResultSet.Dispose()`/`Close()` fault after the values were read (the F2 family) is ignored.
- **Unresolved issues, risks:**
  - PR7b-2 not done:
    - every target's direction on both models (heating on a SAM model needs heating on);
    - measures compared with SAM_Tas' readers on the evaluation folders;
    - a 4-5 option choice on both models;
    - a mixed Hooke-Jeeves run;
    - repeats and the F2 rate;
    - a multi-plant-room TPD;
  - SAM fixture `systems-demo-bound-golden-section.json` still says `"Plant Room 1"` (tests prove it is OPT609 on the
    Demo; SAM follow-up);
  - `Modify.UpdateConstructions` still overwrites by name (the runner avoids it with unique names and a refusal);
  - the malformed `SIM_EXT_GLZ` Guid in SAM's library (its short id is not stable across sessions).
- **Next step:** owner's choice between PR7b-2 (licensed acceptance, SAM_Tas) and PR8 (SAM_UI journey on `tas-model`).
  Hand-over prompt: `NEXT_SESSION_PROMPTS_2026-10-09.md` in the SAM-BIM folder.

## Native Optimisation PR7a-2, glazing swap and g-value from available glazing systems (2026-10-08)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#90 (`spike/optimisation-glazing-swap`) merged into `sow/2026-Q4` as merge commit
    `02a06b6fdc6b697a3d74f9a38a936e6e2832420b`. Its parents are the Q4 base `13d86686` and the reviewed PR head
    `d928f97c01c64a0bf756e91d0ff3ff1f3cf5ee5c`.
  - Merge method: merge commit with `--match-head-commit`, after the owner's approval. CI on the head: `build` and
    `spdx` green. No review comments. Remote and local branch deleted.
  - Evidence only; no product code. Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7A2_GLAZING.md`.
    Spike files (not in `SAM_Tas.sln`): `SAM_Tas/spikes/optimisation-pr7a2/` (`swap.csx`, the probe `probe/`
    with `pool`/`write`/`constructions`, `README.md`). Raw evidence stays local in `C:\TasOut\pr7a2\`.
- **Work completed (licensed, Systems Demo copy plus a SAM-generated Part O model):**
  - SAM's glazing flow read end to end: `ThermalTransmittanceCalculator.CalculateGlazing` (TCD, STA, g and light
    rounded to 0.001, U = `GetUValue()[6]`; 171 systems in 2.94 s), the Glazing window's pools/filters/sort orders, the
    legacy assign-by-g flow, `SetGlazing`, and SAM_Tas' TBD writer `Modify.UpdateConstructions(building,
    apertureConstructions, materialLibrary)` (`"Windows: <name> -pane"/"-frame"`).
  - g parity: the calculator's g equals the TBD's `GetGlazingValues()[5]` bit for bit for 8 systems (g 0.118–0.792) on
    both TBDs; U and frame U too.
  - Swap in TasGenExecute (C# 7.0, TBD only): `buildingElement.AssignConstruction` on every element using the target
    glazing (and the frame element paired by name), read back, simulated. 23 evaluations: exit 0, no lock, no leftover
    process, registry unchanged; repeats bit-identical; the SAM model's own system written under a new name reproduced
    the baseline exactly. Solar gain rises strictly with g; cooling/overheating do not (U and light change with the
    system).
  - Snapping a continuous g to the closest system gives a step function (Hooke–Jeeves stuck on a plateau when the
    measured results are replayed); trying every option is exact in n simulations.
- **Owner decisions (2026-10-08, on approving #90):**
  1. **Method:** a choice among real systems, ordered by g, run by **"try every option"** (kind
     `tbd.glazing-construction.choice`, `"discrete"` 1..n). The continuous `tbd.glazing-construction.g-value` is not
     offered (no pane solve, no snap).
  2. **Option filter:** not changed by the owner, so the recommended defaults stand: same aperture type and
     transparency, g in the requested range, Ug ≤ current + 0.3 W/m²K, light ≥ current − 0.1, duplicates (g, U, light to
     0.001) once, at most 8 options, the current glazing as option 1.
  3. **Frames: keep the model's frames** (swap the pane construction only).
  4. **Order (owner asked the agent to choose):** the SAM work first. SAM's schema has only golden section and
     Hooke–Jeeves, `"discrete"` is reserved (OPT415) and the SAM.Math kernel has no exhaustive method, while SAM_Tas
     already models GenOpt's `Mesh` algorithm (every grid point), so a SAM PR adds the method (schema value,
     capabilities, diagnostics, kernel) before PR7b uses it. Chosen from code reading, not from a run.
- **Findings for PR7b:**
  - `UpdateConstructions` matches by name and overwrites: a library system named like the model's own glazing replaced
    the model's in-use construction. Write every candidate under a unique name (`"<name> <last 6 of Guid>"` worked).
  - Frames must be found through the elements (`"<base>-pane"` → `"<base>-frame"`); SAM-generated TBDs use `"X -frame"`
    on the element and leave `"Windows: X -frame"` unused; one pane element had no frame element.
  - Half of the 171-system TCD pool is single glazing; with the U filter only ~5–10 systems remain, so useful options
    come mainly from "My glazing systems".
- **Files changed:** the record and `SAM_Tas/spikes/optimisation-pr7a2/` (4 files). No product file.
- **Validation:** 23 TasGenExecute evaluations, 2 pool runs (179 systems), 4 TBD writes, 2 listings; PR CI green.
- **Unresolved issues, risks:**
  - SAM's default `SAM_ApertureConstructionLibrary.JSON` has a `SIM_EXT_GLZ` window with a malformed Guid
    (`4d00dd0-…`): a new random Guid on every load (separate SAM fix, suggested as its own task).
  - The `UpdateConstructions` same-name overwrite is a product behaviour worth its own SAM_Tas issue.
  - Legacy `Tas.Query.Score` is biased when only g is targeted (not used by the plan).
  - Heating effects are shown on the Demo only (the SAM model has heating off).
- **Next step:** the SAM PR "try every option" (method value in `sam.optimisation/1`, kernel exhaustive search over a
  `"discrete"` variable, capabilities and diagnostics), then PR7b (SAM_Tas catalogue reader, script generator, engine
  `tas-model`, glazing choice). Hand-over prompt: `NEXT_SESSION_PROMPT_TRY_EVERY_OPTION.md` in the SAM-BIM folder.

## Native Optimisation PR7a, licensed spike for the `tas-model` blocks (2026-10-08)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#89 (`spike/optimisation-tas-model-blocks`) merged into `sow/2026-Q4` as merge commit
    `d393ffbacd5073f63a4540e45390f9224beba3f8`. Its parents are the Q4 base `861d3e75` and the reviewed PR head
    `6fd69fbdd22310d7841fcfc976cc3a912d2ed290`.
  - Merge method: merge commit with `--match-head-commit`, after the owner's approval. CI on the head: `build` and
    `spdx` green. No review comments. Remote and local branch deleted.
  - Evidence only; no product code. Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7A_SPIKE.md`.
    Spike files (not in `SAM_Tas.sln`): `SAM_Tas/spikes/optimisation-pr7a/` (`chain.csx`, 21 compiler probes, the
    driver `Invoke-Pr7aEvaluation.ps1`, the reader probe `probe/`). Raw evidence stays local in `C:\TasOut\pr7a\`.
  - Programme: plan of record SAM_UI `documentation/NativeOptimisation-Plan-ModelBindings.md`; PR6 SAM#188 merged.
- **Work completed (licensed, Systems Demo copies plus one SAM-generated Part O model):**
  - One TasGenExecute script runs the whole chain: TBD edit → `simulate(1,365,0,1,0,0,tsd,1,0)` → TSD read → TPD
    `FixTSDPath` → controller setpoint → the Demo's `SimulateEx` → annual result sets. No lock or leftover process in any
    run; TasManager registry unchanged.
  - Durations (5 repeats, bit-identical): building only 11.5 s, plant only 23.9 s, both 32.2 s, Demo script 24.1 s.
  - Every V1 target moved the result in the expected direction. Controller parity: Setpoint 4.968943799848584 →
    cost 7360.04370117188, bit-identical to the PR3/PR5 acceptance (plant room `Plant Room`, `HeatPumpController`).
  - Every measure equals SAM_Tas' own readers to 15 digits; overheating = `Query.Overheating` (`OccupiedHours28/25`)
    for the worst occupied zone.
  - TasGenExecute compiles **C# 7.0 exactly**; LINQ needs `using System.Linq;`; TBD/TSD/TPD/TAS3D/TWD interops are
    referenced, **TCD is not**; `#r` works.
- **Findings for PR7b:**
  - F1: `TSDData.FixTSDPath` throws `RPC_E_SERVERFAULT` when the TBD named in the TSD's absolute
    `SimulationData.buildingPath` is missing (the Demo's TSD names a folder on the other workstation). Re-pointing
    `buildingPath` to the evaluation's TBD first fixes it (19/19).
  - F2: TPD.exe crashes at shutdown in ~17 % of plant evaluations (with or without `Save`); results already read, exit 0,
    +4–5 s.
  - F3: names are matched exactly (a Demo internal condition has a trailing space).
  - SAM-generated TBDs: one internal condition per space (description `"<SAM internal condition> - <space>"`) plus
    `"- HDD"`; thermostat setpoints are 24-hour profiles (setpoint = highest heating / lowest cooling hour).
  - TSD daily arrays are 1-based SAFEARRAYs; `Building.GetConstruction(i)` has null gaps.
- **Owner decisions (2026-10-08, on approving #89):**
  1. Setpoint targets are offered **per internal condition** (no grouping by SAM internal condition in PR7b).
  2. The TPD.exe shutdown crash (F2) is **not** reported to EDSL; blocks read results before release and ignore it.
  3. **Glazing g-value: no pane-property solve.** The g-value target uses the g that SAM already calculates for real
     glazing systems (`ThermalTransmittanceCalculator.CalculateGlazing`, the Tas TCD calculation behind the Glazing
     window), picks the **closest match among the glazing systems available to the project** (model, My glazing
     systems, default library), and swaps that construction in. The next stage proposes the exact method (snapping
     vs a g-ordered choice) after understanding how SAM's glazing flow works. PR7a's pane-solve is evidence only.
  4. The optional **glazing-swap test is approved** (next stage).
- **Files changed:** the record and `SAM_Tas/spikes/optimisation-pr7a/` (27 files). No product file.
- **Validation:** 77 TasGenExecute evaluations (21 compiler probes, 56 on Tas models); 9 cross-checked against SAM_Tas
  readers; registry export identical before/after every batch; PR CI green.
- **Unresolved issues, risks:**
  - F1 also affects today's `tas-script` route when a Systems Demo project is moved.
  - Out-of-scope SAM_Tas defects seen: `Query.Constructions` stops at the first null; `ConsumptionHeating/Cooling`
    hold Wh under a kWh label.
  - PR6 fixture `systems-demo-bound-golden-section.json` says `"Plant Room 1"`; the Demo's plant room is `"Plant Room"`.
  - Heating and plant blocks are proven on the Demo only (the SAM model had heating off and no TPD).
- **Next step:** PR7a-2, a licensed spike for the glazing swap and the g-value from available glazing systems
  (decisions 3 and 4), then PR7b (catalogue reader, script generator, engine `tas-model`), each after owner review.

## Native Optimisation PR4, the Optimisation Definition runs on Tas (2026-10-08)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#88 (`feature/optimisation-definition-tas-adapter`) merged into `sow/2026-Q4` as merge commit
    `a3837acbebb1bdfcf1d68fa519305e67f640fdb8`. Its parents are the Q4 base `7f2043ac` and the reviewed PR head
    `b5c93fc9ebfc1fa19a0f03a14e5ad04cf30e6624`; the merge tree `98a0e34` is identical to the head tree.
  - Merge method: merge commit with `--match-head-commit`.
  - CI: the PR's `build` (it runs the GenOpt tests) and `spdx` were green on the head. The post-merge
    `Build (Windows)` on `a3837acb` was green (run 37774014140). No review comments.
  - The remote branch was deleted. Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_DEFINITION_PR4.md`.
  - Programme: native Optimisation definition and UX. PR1 SAM_UI#214, PR2 SAM#186 and PR3 SAM#187 had already
    merged.
- **Work completed.** `SAM.Analytical.Tas.GenOpt` now references `SAM.Core.Optimisation` (HintPath) and adds:
  - `Query.TasOptimisationCapabilities()` and `Query.TasOptimisationEngine` (`"tas-script"`): golden section 1..1,
    Hooke–Jeeves 1..∞, minimise only, continuous only, no constraints.
  - `Convert.ToGenOptDocument(definition, directory, scriptText)` and an overload taking `TasExecutionSettings`.
    - It validates the definition against the capabilities. Any error throws `TasOptimisationDefinitionException`,
      a `GenOptCompatibilityException` that carries the diagnostics.
    - It maps onto the existing GenOpt objects exactly as the SAM_UI form did: objective first; golden-section
      start and step passed through (absent → minimum and 0); omitted settings keep the SAM_Tas defaults;
      `MaxEqualResults` at its default.
  - `TasExecutionSettings`: local, non-portable settings.
  - `Query.TasScriptCatalogue`: a literal scan of `Variables["…"]` and `ScriptOutput.SetValue("…"`, with comments
    and literals handled.
  - `Query.TasScriptDiagnostics`: OPT501/OPT502 warnings, never errors.
- **Decisions:**
  - `TasScriptCatalogue` returns the PR3 `OptimisationCatalogue`; it replaces the plan's `TasScriptNames`.
  - No `RunNative` wrapper. A `Modify` class here could clash with Grasshopper's `Modify.RunNative`.
  - L6 file and engine checks stay in SAM_UI until PR5.
  - The capabilities instance lives in a nested holder class. That way neither `Query` nor `Convert` loads
    `SAM.Core.Optimisation.dll` for the existing route before PR8 deploys it.
- **Files changed:** 13 (+1297/−1).
  - Six new source files, the csproj and the record.
  - Tests: the csproj, `TESTING.md`, `OptimisationDefinitionAdapterTests`, `TasScriptCatalogueTests` and
    `OptimisationDeploymentTests`.
  - No SAM.Math, SAM, SAM_UI or Grasshopper change, and no existing GenOpt source changed.
- **Validation.** Done on the second workstation, with all sibling repos on the Q4 heads.
  - `SAM.Analytical.Tas.GenOpt.Tests` 257/257 (216 + 41).
  - The definition document equals the PR5 form document for both Systems Demo fixtures, and the stub runs match
    evaluation for evaluation, `Variables.txt` bytes included.
  - **Licensed-run replay:** the recorded PR5 acceptance runs (`C:\TasOut\pr5-acc`) were replayed through this path.
    A 11/11 and B 16/16 `Variables.txt` were byte-identical. Harness: `C:\TasOut\pr4-replay`, not committed.
  - Mutations 6/6 caught.
  - A load context that refuses SAM.Core.Optimisation proves the existing route still runs without it.
- **Unresolved issues, risks:**
  - `SAM.Core.Optimisation.dll` (and `SAM.Units.dll`) must ship beside `SAM.Analytical.Tas.GenOpt.dll` before PR5
    reaches users (PR8, SAM_Deploy).
  - The script scan is literal.
  - No new live licensed run; the PR5a acceptance does that through SAM_UI.
- **Next step:** PR5a in SAM_UI. The form becomes an `OptimisationDefinition`, the examples become JSON, and the run
  goes through `ToGenOptDocument`, with a licensed A/B rerun. Then PR5b (formatting), each after owner review. The
  hand-over prompt is `NEXT_SESSION_PROMPT_PR5.md` in the SAM-BIM folder.

## Java-free GenOpt replacement — PR6, legacy Java route retired, shared result rules (2026-10-08)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#87 (`feature/native-optimisation-pr6-retire-legacy`) merged into `sow/2026-Q4` as merge commit
    `8dffaa3dabfedb45e43330a8a54fb1ba4a17c34e` (parents: Q4 base `da891dd2` + reviewed PR head
    `860c5a55a3a7a85d430e2845f1e73563ae0676fa`; merge tree `9fd7e76` identical to the head tree).
  - Merge method: merge commit with `--match-head-commit`. PR CI (`build`, `spdx`) green on the head; post-merge
    `Build (Windows)` on `8dffaa3d` green. Codex: no findings on this PR.
  - Remote and local branch removed. Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_GENOPT_PR6.md`.
  - Coordinated PR set (owner-approved order): this PR first, then SAM_Tas_Grasshopper#12 and SAM_UI#212 (consumers of
    `NativeGenOptOutcome`), then SAM#185 (comment only).
- **Work completed:**
  - Removed the legacy Java execution route: `GenOptDocument.Run()` (incl. its `Core.Tas.Modify.SetProjectDirectory`
    call), `ExecutableFile` (GenOpt.bat / `java -classpath genopt.jar`), `Query.TasGenOptJavaPath`, `Create.Command`
    (`cmd /c start`), the Java-only simulation-launch configuration (`SimulationConfigFile`, `SimulationStart` with a
    personal-path `cmd /c` default, `SimulationError`, `IO`, `NumberFormat`) and the matching document members; resources
    `files/resources/Analytical/Tas/GenOpt/GenOpt.bat` and `config.txt`.
  - GPSCoordinateSearch: removed from `Convert.NativeAlgorithmTypes`; refused with `NotSupportedException` and the D3
    reason (was `GenOptCompatibilityException`). Enum value and class kept for compatibility; nothing substituted.
  - New `NativeGenOptOutcome`: Completed / Withheld / Successful / BestEntry (`Best`) / Interval / `IsLower` /
    `RefusalMessage` — the rules the Grasshopper (PR4) and SAM_UI (PR5) reports duplicated. Wording stays in consumers.
  - "Java route" wording in messages/comments replaced by "GenOpt-format writer/text"; oracle history kept.
- **Kept deliberately:** `GenOptDocument` (definition + `RunNative`), algorithm/parameter/objective classes,
  `GenOptNumber`, `Convert.*`, workspace, evaluator, GenOpt-format input writers, `SetProjectDirectory` (used by
  `WorkflowCalculator`), the `SAM.Core.Tas` reference, the inert rest of the GenOpt resource folder.
- **Files changed:** 25 (+816/−425): `SAM.Analytical.Tas.GenOpt` (8 files deleted; `GenOptDocument`, `ToSAM_Optimiser`,
  4 native-class comment/message edits; new `Classes/Native/NativeGenOptOutcome.cs`; `NATIVE_GENOPT_PR6.md`;
  `NATIVE_GENOPT_ROUTE.md` pointer), tests (`LegacyRouteRetiredTests`, `NativeGenOptOutcomeTests` new; `MappingTests`,
  `NativeRunTests`, `TESTING.md`), 2 resource files deleted.
- **Validation:** `MSBuild SAM_Tas.sln /t:Rebuild` Release 0 errors (APPDATA/USERPROFILE redirected, real
  `NUGET_PACKAGES`). GenOpt.Tests 216/216 (173 + 43; 25 golden traces bit for bit through `RunNative`), TM59 1128/1128,
  Benchmark 16/16. IL/metadata test: only `TasGenExecuteObjectiveEvaluator` starts a process; no registry, java or cmd.
  Mutations on `NativeGenOptOutcome` (tie->last, no withholding, Nullspace not completed) all caught.
  Licensed acceptance not rerun: native execution unchanged; PR3 acceptance stands.
- **Unresolved issues, risks:**
  - Public API removal (`Run`, launch members/types, `TasGenOptJavaPath`, `Create.Command`); no consumer in any SAM-BIM
    repository; external scripts calling `Run()` would stop compiling.
  - SAM_Deploy follow-ups (separate task): ship one current `SAM.Math.dll` everywhere and `SAM.Analytical.Tas.GenOpt.dll`
    beside SAM_UI; delete old `GenOpt.bat`/`config.txt` from existing installations (installer does not remove files).
- **Next step:** merge SAM_Tas_Grasshopper#12 and SAM_UI#212, then SAM#185 (owner-approved); then the SAM_Deploy shipping
  task; then the AI-friendly objective model phase when requested.

## Java-free GenOpt replacement — PR3, native Tas GenOpt route (2026-10-07)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#86 (`feature/native-genopt-tas-evaluator`) merged into `sow/2026-Q4` as merge commit
    `63a5fec7f1681662f56f89e3c84c2a8824bff7bb` (PR head `229142e36b2b22bee8c4d7c69dedd117734a2085`, Q4 base `60d56621`).
  - Merge method: merge commit, head-commit protected.
  - Code-owner review: @ZiolkowskiJakub approved.
  - Remote and local branch removed.
  - Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_GENOPT_ROUTE.md`.
- **Work completed:** `GenOptDocument.RunNative` maps the existing GenOpt objects onto the SAM.Math kernel (SAM#183)
  and evaluates with `TasGenExecute.exe` directly (Gate T protocol). It uses no Java, GenOpt, cmd.exe or registry.
  - `GenOptNumber`: the Java writer's text, read with GenOpt's StreamTokenizer arithmetic (D1). Exponent algorithm
    keywords are refused (D2).
  - `Convert.ToSAM_Optimiser` / `ToSAM_OptimisationProblem`:
    - GPSHookeJeeves and GoldenSection are supported. GoldenSection takes AbsDiffFunction and exactly one parameter.
    - GPSCoordinateSearch is refused (D3).
    - Every other algorithm type or class throws `NotSupportedException`.
    - GPS mesh values must be integers in GenOpt's domain. MaxEqualResults ≥ 2. WriteStepNumber = true is refused.
      UnitsOfExecution never makes the run parallel.
  - `NativeGenOptWorkspace`: one folder per run. The project snapshot is an allow-list: `Script.txt` plus
    TasGenComm's T3D/TBD/TPD/TSD/TWD.
  - `TasGenExecuteObjectiveEvaluator`:
    - one fresh folder per attempt (`NNNN`, `NNNN-retry`) and `Variables.txt`;
    - failure on a non-zero exit, a non-empty `Error.txt`, or a missing or unparseable objective (comma decimals are
      refused); the kernel owns the retry;
    - cooperative cancellation between evaluations, with no process kill.
  - The legacy `Run()` is unchanged.
- **Owner decisions:**
  - Legacy Java route: `Run()` writes `cmd /c "start …"`, which hangs under GenOpt 3.1.1 because GenOpt rewrites `/c`
    to `C:\c`. Accepted as a pre-existing defect. Not fixed; it is input for PR6.
  - GPSCoordinateSearch: the D3 Java-only oracle showed the SAM_Tas writer always emits `Seed`/`NumberOfInitialPoint`,
    which GenOpt rejects, so the Java route cannot run it. The adapter refuses it. A future native configuration layer
    may expose CoordinateSearch, but not as legacy compatibility.
  - Cancellation count: the PR2 kernel contract (a cancelled run counts the assigned but unlaunched simulation) is
    accepted. It may be revisited for presentation before SAM_UI (PR5).
  - Non-integer mesh values are refused, where Java truncates them (no silent repair).
- **PR3 Java-only oracle** (real genopt.jar 3.1.1, JRE 8, existing SAM_Tas writer, no Tas):
  - GPSCoordinateSearch is rejected for all Seed/NumberOfInitialPoint values.
  - `AbsDiffFunction = 1E-05` gives "Expected ';', got 'E-05'".
  - Mesh domain: divider > 1, s0 ≥ 0, t > 0, m > 0; fractions are silently truncated.
  - MaxEqualResults < 2 is rejected.
  - The writer's `E+`, `E-`, `-0` and 17-digit texts are read bit for bit as `GenOptNumber` predicts.
- **Files changed:** 24 (+2957/−1).
  - `SAM.Analytical.Tas.GenOpt`: `Classes/Native/*` (5), `Convert/*` (2), `GenOptDocument.cs` (additive), csproj
    (SAM.Math HintPath), `NATIVE_GENOPT_ROUTE.md`.
  - New `SAM.Analytical.Tas.GenOpt.Tests` (NUnit, HintPath pattern) and `SAM.Analytical.Tas.GenOpt.Tests.StubTasGenExecute`.
  - `SAM_Tas.sln` (2 projects appended), `.github/workflows/build.yml` (test step).
- **Validation:**
  - `SAM.Analytical.Tas.GenOpt.Tests`: 173/173. It includes 25 SAM golden GenOpt traces replayed bit for bit through
    `RunNative` plus the stub.
  - 5/5 adapter mutations caught.
  - `MSBuild SAM_Tas.sln /t:Rebuild` Release: 0 errors. TM59 1128/1128, Benchmark 16/16.
  - Licensed Systems Demo: Java GenOpt A, Java GenOpt B and native were **bit-identical**, with a Java-vs-Java noise
    floor of 0. That covers candidates, numbering, objectives (Result, Cost, CO2), termination and best point.
    - GoldenSection: 11 simulations, best Setpoint 4.968943799848584.
    - GPSHookeJeeves: 16 simulations, 41/10 listing rows, best Setpoint 5.
    - The Java controls invoked TasGenExecute directly because of the legacy `cmd /c` hang (accepted deviation).
  - PR CI `build` and `spdx` green on the head.
- **Unresolved issues, risks:**
  - The legacy `Run()` command hang (PR6).
  - Licensed acceptance covers one parameter (Systems Demo); multi-parameter runs are covered by the stub and golden
    tests.
  - TasGenExecute culture: native refuses comma-decimal output.
  - `SAM.Math`'s `Java8FloatText` keeps a stale .NET Framework sentence in a comment. It is deferred to a separate
    comment-only SAM PR; there is no .NET Framework requirement.
- **Next step:** PR3 is frozen. PR4 (Grasshopper), then PR5 (SAM_UI), then PR6 (Java retirement), each only when the
  owner requests it.

---

# Historical record - 2026-Q3 (frozen)

Source: last revision of the file on `sow/2026-Q3`, commit `1ea4b5d` (the file was removed from the Q3 tip by `9a2e4e6`; `sow/2026-Q3` tip is `7d4ff52f`). Preserved verbatim except that heading levels are shifted down one. Everything below describes Q3 and is not a current instruction.

## SAM_Tas Part O PR2 progress

Base: `sow/2026-Q3`. PR2 merged as SAM-BIM/SAM_Tas#81 at `adfaa42caf762444a4e1e6913eb1b42d886a408f` on 2026-10-04.

### Completed
Added observational NORMAL/COOLING and BYPASS/RECOVERY labels to the existing guidance cooling hourly read-back. Ambiguous or missing values are UNAVAILABLE. Summary states first observed cooling hour. No control or simulation physics changed.

### Files changed
GuidanceCoolingResults class and read-back writer; GuidanceCoolingRecipeTests; this file.

### Validation
TPD project builds with VS MSBuild. Guidance and mixed cooling tests: 40 passed; focused recipe tests: 15 passed. PR Windows build and SPDX passed. Existing compiler warnings only. Diff review found no physics edits.

### Next step
PR2 is complete. Native TAS COM remains unverified by deterministic tests. Next task, only when requested: real end-to-end acceptance through SAM_UI → Part O → Prepare & Run → Iteration 3 using the prepared Nuaire sample. Do not start PR3.
