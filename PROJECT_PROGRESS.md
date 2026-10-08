# Project Progress - SAM_Tas (2026-Q4)

## Branch

`sow/2026-Q4` - bootstrapped 2026-10-06 from `master` `28ac11a7`. Frozen Q3 record: `sow/2026-Q3` @ `7d4ff52f` (not modified).

## Last updated

2026-10-08 (native Optimisation PR4 closeout, SAM_Tas#88).

## Current status

Q4 branch cut from `master` `28ac11a7`, which is the exact commit pinned in SAM_Deploy's frozen Q3 baseline (`v20261006.1`). Bootstrap added only internal docs (this file, `AGENTS.md`). First Q4 product work: the opt-in Direct SAM -> T3D route, merged as #84 (see the Direct T3D section below). The gbXML route remains the default. Second Q4 stream started: the Java-free GenOpt replacement (PR1-T evidence merged as #85; native Tas GenOpt route merged as #86; legacy Java route retired and result rules shared as #87; see the GenOpt sections below). Native Optimisation definition stream: the Tas adapter for the SAM.Core.Optimisation definition merged as #88 (see the PR4 section below).

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
