# Native Optimisation PR4: the Tas adapter for the Optimisation Definition

Record for the PR branch `feature/optimisation-definition-tas-adapter` (base `sow/2026-Q4` `7f2043ac`). It is kept on
the branch while the PR is open. `PROJECT_PROGRESS.md` gets the closeout only after the merge.

## Status
**Implemented and tested locally (257/257, licensed-run replay 11/11 + 16/16, 6/6 mutations); awaiting PR CI and owner review.** Do not merge without owner acceptance. PR5
(SAM_UI form ⇄ model, licensed A/B rerun) is not started and needs its own authorisation.

## Context
- Programme plan: "SAM Native Optimisation: declarative definition and engineering UX". PR1 SAM_UI#214, PR2 SAM#186
  and PR3 SAM#187 are merged and closed out (SAM `sow/2026-Q4` `721b5fae`, SAM_UI `492cf0b9`).
- PR3 added `SAM.Core.Optimisation` (schema `sam.optimisation/1`): the definition model, strict reader, canonical
  writer, L1–L5 diagnostics and the AI text. It runs nothing; an engine supplies its capabilities and executes.
- PR4 is that engine for Tas. It maps a definition onto the **existing, accepted** native route
  (`GenOptDocument.RunNative`, PR1–PR6 of the GenOpt migration). GenOpt objects stay the adapter's internal format.

## Work completed
`SAM.Analytical.Tas.GenOpt` (netstandard2.0) references `..\..\..\SAM\build\SAM.Core.Optimisation.dll` (HintPath, like
SAM.Math). New public API:

| API | What it does |
|---|---|
| `Query.TasOptimisationEngine` = `"tas-script"` | The engine id a definition names in `model.engine`. |
| `Query.TasOptimisationCapabilities()` | Golden section on exactly 1 variable, Hooke–Jeeves on 1 or more, minimise only, continuous only, no constraints. One shared instance. |
| `Convert.ToGenOptDocument(this OptimisationDefinition, string directory, string scriptText)` | Validates against the capabilities; any error throws `TasOptimisationDefinitionException` before anything is built. Otherwise builds the `GenOptDocument` (mapping below). |
| `Convert.ToGenOptDocument(this OptimisationDefinition, TasExecutionSettings)` | Same, with the workspace = `ProjectFolder` and the script read from `ScriptPath` (`File.ReadAllText`, as SAM_UI does). Missing script → `FileNotFoundException`. |
| `TasExecutionSettings` | Local, non-portable settings: `ProjectFolder`, `ScriptPath`, `RunsFolder`, `TasGenExecutePath`; copy constructor. Never part of the definition. |
| `TasOptimisationDefinitionException : GenOptCompatibilityException` | Carries every diagnostic; the message lists the errors. Existing `GenOptCompatibilityException` handlers (SAM_UI, Grasshopper) still catch it. |
| `Query.TasScriptCatalogue(scriptText)` | `OptimisationCatalogue` (source `"Tas script"`) of the names the script reads (`Variables["…"]`) and writes (`ScriptOutput.SetValue("…", …)`): literal scan, first-appearance order, once each, `//` and `/* */` comments ignored, string/verbatim/char literals respected, identifier boundary (`MyVariables[…]` is not a match). |
| `Query.TasScriptDiagnostics(this OptimisationDefinition, scriptText)` | L6 **warnings**: OPT501 design variable never read, OPT502 output never written; the hint names the case-differing match or the names the script uses. Never errors, so never blocks a run. |

**Mapping (definition → GenOptDocument), reproducing the PR5 SAM_UI form value for value:**
- Each design variable → `NumberParameter { Name, Initial = start, Min = minimum, Max = maximum, Step = step }`, in
  definition order. Golden section ignores start and step, but they are **passed through as given** so Variables.txt
  keeps its bytes (plan risk R2). Absent → start = minimum, step = 0 (what the form did for golden section).
- Outputs → `Objective(name)` (`name::` delimiter): **the objective output first**, then the recorded outputs in
  definition order.
- Golden section `tolerance` → `AbsDiffFunction`. Hooke–Jeeves `stepReductionFactor`, `initialStepExponent`,
  `stepExponentIncrement`, `stepReductions` → `MeshSizeDivider`, `InitialMeshSizeExponent`,
  `MeshSizeExponentIncrement`, `NumberOfStepReduction`. A setting the definition omits keeps the SAM_Tas default.
- `stopping.maximumSimulations` → `MaxIte` (omitted → 2000). `MaxEqualResults`, `WriteStepNumber`,
  `UnitsOfExecution` keep their SAM_Tas defaults.
- Objects are added in the form's order: script, objectives, parameters.

## Decisions and assumptions
- **Validation gate.** `ToGenOptDocument` runs `Diagnostics(TasOptimisationCapabilities())` (L3–L5, including PR3's
  OPT214 non-finite check). SAM_Tas' own `Convert.ToSAM_Optimiser`/`RunNative` checks remain the final gate.
- **Names.** Taken as they are in the definition (no trimming); the strict reader and L3 own name rules.
- **`TasScriptCatalogue` instead of the plan's `TasScriptNames`.** It returns the PR3 `OptimisationCatalogue`, which
  `AIExchangeText` already takes, rather than a new names type.
- **No run wrapper.** A `RunNative(definition, settings)` extension would need a `Modify` class in this namespace,
  which could make `Modify.RunNative` ambiguous for Grasshopper code. Callers use
  `definition.ToGenOptDocument(settings).RunNative(settings.RunsFolder, settings.TasGenExecutePath, …)`.
- **L6 scope.** Only the script-name warnings are here. Folder/script/TasGenExecute presence checks stay in SAM_UI
  until PR5 moves the window to the definition.
- Minimise only. Maximise is PR9 (V1.1).

## Files changed
- `SAM.Analytical.Tas.GenOpt/SAM.Analytical.Tas.GenOpt.csproj` (SAM.Core.Optimisation HintPath).
- New: `Convert/ToGenOptDocument.cs`, `Query/TasOptimisationCapabilities.cs`, `Query/TasScriptCatalogue.cs`,
  `Query/TasScriptDiagnostics.cs`, `Classes/TasExecutionSettings.cs`,
  `Classes/Native/TasOptimisationDefinitionException.cs`, this record.
- `SAM.Analytical.Tas.GenOpt.Tests/SAM.Analytical.Tas.GenOpt.Tests.csproj` (SAM.Core.Optimisation, SAM.Units
  HintPaths); new `OptimisationDefinitionAdapterTests.cs`, `TasScriptCatalogueTests.cs`.
- No SAM.Math, SAM, SAM_UI or Grasshopper change. Existing GenOpt code is untouched.

## Validation
Local, 2026-10-08, on a second workstation. Sibling repos are on the merged `sow/2026-Q4` heads: SAM `721b5fae`, which
includes PR2 and PR3, plus the six SAM_Tas dependency repos.
- **Build.** The owner's `BuildAlls_v4.bat` ran a full Debug rebuild. Then
  `MSBuild SAM.Analytical.Tas.GenOpt.csproj -restore -p:Configuration=Release -p:BuildProjectReferences=false`
  built with 0 errors.
- **`SAM.Analytical.Tas.GenOpt.Tests`: 257/257 passed** (216 before PR4, plus 41 new).
  - `OptimisationDefinitionAdapterTests`, 27 tests:
    - capabilities;
    - both fixtures runnable;
    - **parity with the PR5 form document** for both examples: algorithm, settings, command, output, parameter and
      script text; bit-equal doubles; objective order;
    - **stub runs evaluation for evaluation**: trace, best point or interval, and `Variables.txt` bytes;
    - mapping rules and defaults;
    - refusals OPT410, 412, 413, 414, 415, 214 and 406;
    - execution settings.
  - `TasScriptCatalogueTests`, 12 tests: the daylight example, the Systems Demo shape, comments and literals, and
    OPT501/502.
  - `OptimisationDeploymentTests`, 2 tests: in a load context that refuses SAM.Core.Optimisation, the `Query` and
    `Convert` type initialisers, `TasGenOptExecutePath` and a full stub `RunNative` still work. A control test shows
    that the new API does need the DLL.
- **Licensed-run replay** (an offline harness, not committed): the recorded PR5 acceptance runs
  (`C:\TasOut\pr5-acc`, A-ws and B-ws, Script.txt SHA-256 `d68a7e2e…`, recorded 2026-10-07) were replayed through
  this PR's path:
  - the path is fixture JSON → `Create.OptimisationDefinition` → `ToGenOptDocument(TasExecutionSettings)` →
    `RunNative`;
  - a stand-in TasGenExecute requires each evaluation's `Variables.txt` to be **byte-identical** to the licensed one,
    then returns the licensed `Output.txt`.
  - **A (golden section): 11/11 identical**, Success, final interval 4.767943850222925 to 5.093168600454254.
  - **B (Hooke–Jeeves): 16/16 identical**, Success, best Setpoint 5 at Result 7360.04370117188.
  - In both, the objectives were [Result, Cost, CO2] and the snapshot Script.txt was byte-identical.
- **Mutations:** 6 out of 6 caught by the PR4 test classes.

  | Mutation | Failing tests |
  |---|---|
  | M1: objective not moved first | 1 |
  | M2: golden-section start/step not passed through | 2 |
  | M3: validation gate removed | 8 |
  | M4: Hooke–Jeeves increment mapped from step reductions | 3 |
  | M5: eager capabilities field on `Query` | 1 |
  | M6: script comments not stripped | 1 |

  M5 first survived. On .NET 8, `Query` is `beforefieldinit`, so its initialiser never ran. The deployment test now
  forces the type initialisers, as a .NET Framework host may.
- `git diff --check` is clean, every new `.cs` file has its SPDX header, and no existing GenOpt or SAM.Math source
  changed.
- PR CI: pending.

## Unresolved issues and risks
- No new licensed Tas run was made. PR4 adds a construction path, not a runtime change, and the replay above proves
  byte-identical evaluation inputs against the licensed PR5 runs. A live licensed A/B rerun stays PR5's acceptance,
  when SAM_UI switches to this path.
- The replay harness and the recorded runs are local and not committed: the Systems Demo model and script are EDSL
  material.
- `SAM.Core.Optimisation.dll` (and `SAM.Units.dll`) must be deployed beside `SAM.Analytical.Tas.GenOpt.dll` wherever
  it ships (PR8, SAM_Deploy). Until PR5 nothing calls the new API at run time, so a missing DLL cannot break the
  current SAM_UI or Grasshopper paths (the CLR loads the reference lazily).
- The script scan is literal: a name built at run time is not found (warnings only).

## Next step
Owner review of this PR; after merge, the `PROJECT_PROGRESS.md` closeout on SAM_Tas `sow/2026-Q4`. Then PR5 (SAM_UI
form ⇄ model, licensed rerun of A and B) only when the owner authorises it.
