# Java-free GenOpt PR6 - retire the legacy Java route, share the result rules

Branch `feature/native-optimisation-pr6-retire-legacy` in SAM_Tas, SAM_Tas_Grasshopper and SAM_UI (same name, so each
PR's CI builds against the others; CI clones a dependency at the PR head branch first). Bases: each repository's
`sow/2026-Q4` (SAM_Tas `da891dd2`, SAM_Tas_Grasshopper `a01f6705`, SAM_UI `a74938ff`). Plus an independent comment-only
SAM PR (`docs/java8floattext-runtime-comment`, base SAM `f391e022`). Not merged. `PROJECT_PROGRESS.md` is not changed on
any PR branch.

PR order: **SAM_Tas first**, then SAM_Tas_Grasshopper and SAM_UI (both consume `NativeGenOptOutcome` and the
GPSCoordinateSearch refusal). The SAM PR is independent. SAM_Deploy is not changed (see "Deployment").

## Current status

Implementation, tests and evidence complete; PRs open for owner review. PR numbers, CI and review state are in the PR
descriptions.

## Inventory (B1) - every remaining legacy hit, classified

Searched SAM, SAM_Tas, SAM_Tas_Grasshopper, SAM_UI, SAM_Deploy for Java, java(w).exe, genopt.jar, `GenOptDocument.Run()`,
`cmd /c`, GenOpt process launching, the Tas Manager registry, Java configuration, GPSCoordinateSearch, route selectors,
compatibility wording and duplicated result logic.

- **No supported code calls `Run()`.** SAM_UI (`TasOptimisationWindow`) and Grasshopper (`Modify.RunNative`) call
  `RunNative` only; both already had static tests forbidding the Java members. No native/legacy selector exists anywhere.
- **3 - legacy Java execution (removed here):** `GenOptDocument.Run()`; `ExecutableFile` (writes `GenOpt.bat`:
  `java -classpath genopt.jar genopt.GenOpt`); `Query.TasGenOptJavaPath`; `Create.Command` (`cmd /c "start /WAIT /MIN"`);
  `SimulationConfigFile` + `SimulationStart` (default command `cmd /c ... C:\Users\DengusiakM\Desktop\...`),
  `SimulationError`, `IO`, `NumberFormat` (GenOpt's simulation-launch config, read only by Java GenOpt); the
  `GenOptDocument` members `ExecutableFile`, `SimulationConfigFile`, `Command`, `WriteInputFileExtension`, `ErrorMessage`,
  `NumberFormat`; the `Run()`-only call to `Core.Tas.Modify.SetProjectDirectory` (Tas Manager project registry);
  resources `files/resources/Analytical/Tas/GenOpt/GenOpt.bat` and `config.txt` (Java launcher, `cmd /c` config).
- **2 - native compatibility still required (kept):** `GenOptDocument` (definition + `RunNative`), all algorithm and
  parameter/objective classes, `OptimizationSettings`, `GenOptNumber` (GenOpt's own number reading, D1/D2),
  `Convert.*`, `NativeGenOptWorkspace`, `TasGenExecuteObjectiveEvaluator` (the only process start: TasGenExecute),
  `Query.TasGenOptDirectory`/`TasGenOptExecutePath`, and the GenOpt-format writers `CommandFile`, `ConfigFile`,
  `TemplateFile`, `ParameterFile`, `OutputFile`, `ScriptFile`, `Files`/`FileType`/`GetPath`... (`ScriptFile` feeds the
  run snapshot; `CommandFile`/`ConfigFile` are read by the Grasshopper progress text; the writers' text is what
  `GenOptNumber` models; SAM_UI tests compare them).
- **1 - shared production, unrelated (kept):** `SAM.Core.Tas.Modify.SetProjectDirectory` is also used by
  `WorkflowCalculator`; `SAM.Core.Tas` stays referenced (its resource deployment step runs from this reference).
- **4 - tests/oracle (kept, unchanged):** SAM `SAM.Tests.GenOptOracle` (local Java runner, explicit paths, nothing
  committed), `Java8FloatText` tests, the 31 GenOpt 3.1.1 golden traces; SAM_Tas golden replay (25 traces), protocol,
  workspace and run tests.
- **5 - documentation (history kept):** PR1-PR5 records; `NATIVE_GENOPT_ROUTE.md` (PR3 record, now points here).
- **6 - deployment:** SAM_Deploy tracks no Java/jar/GenOpt file. Its submodule pins (master and `sow/2026-Q4`) predate the
  native PRs, so the current installer still contains the Java route (Grasshopper calls `Run()`). The SAM_Tas resource
  folder is staged into the installer via `%USERPROFILE%\Documents\SAM\resources`.
- **7 - harmless naming (kept):** `GenOpt*` class names, `AlgorithmType.GPSCoordinateSearch`,
  `GPSCoordinateSearchAlgorithm`, the unsupported-algorithm Grasshopper components (refused at run), `Java8FloatText`
  (models GenOpt's arithmetic, which native reproduces), oracle wording ("Java GenOpt 3.1.1 would truncate ...").

## What PR6 changes

### SAM_Tas (this PR)

1. **Legacy route removed** (list above). `GenOptDocument` keeps its constructor, definition members, the GenOpt-format
   file objects and `RunNative`; its summary now says `RunNative` is the only way to run it.
2. **GPSCoordinateSearch (B4).** `Convert.NativeAlgorithmTypes` no longer lists it (it advertised a type that was always
   refused). `ToSAM_Optimiser` now refuses it with `NotSupportedException`, like every other unsupported algorithm, with
   the D3 reason ("its GenOpt definition always carries 'Seed' and 'NumberOfInitialPoint', which GenOpt 3.1.1 rejects
   without 'MultiStart', so it was never runnable"). The enum value and class stay for compatibility; no algorithm is
   invented or substituted; the SAM.Math CoordinateSearch kernel stays unexposed. Before PR6 the refusal was a
   `GenOptCompatibilityException` ("Invalid GenOpt settings"), which wrongly suggested other settings could make it run.
3. **Shared result rules (B5): `NativeGenOptOutcome`** (`Classes/Native`). The rules PR4 (Grasshopper
   `NativeGenOptReport`) and PR5 (SAM_UI `TasOptimisationReport`) each held, verbatim duplicates:
   `Completed` (Success / MaximumSimulationsReached / Nullspace), `Withheld` (completed but the user asked to stop),
   `Successful`, `BestEntry` (`Best`: the kernel minimum, else the first lowest non-NaN entry), `Interval` (only when
   successful), `IsLower` (the running "lowest so far" rule both progress displays use) and `RefusalMessage` (the wording
   of `RunNative`'s refusals by exception kind). Wording of outcome lines, number formatting, status colours and layout
   stay in each consumer.
4. **Wording (B6).** Messages and comments that said "the Java route would write ..." now speak of the GenOpt-format
   writer/text (the Java route no longer exists); oracle history is unchanged.

### SAM_Tas_Grasshopper PR

`NativeGenOptReport` uses `NativeGenOptOutcome` (its own `Best` and refusal mapping removed; log lines unchanged);
`RunNative`'s progress text uses `IsLower`. Tests: GPSCoordinateSearch refusal is now "Not supported by the native route:
GenOpt algorithm 'GPSCoordinateSearch' ..."; a metadata test pins the use of `NativeGenOptOutcome`.

### SAM_UI PR

`TasOptimisationReport` uses `NativeGenOptOutcome` (own `Best` and refusal mapping removed; the assembly-load check stays
first; lines unchanged); `TasOptimisationProgressState` uses `IsLower`. Metadata test pins the shared rule.

### SAM PR (independent)

`Java8FloatText`: the stale-looking ".NET Framework parser" sentence now says why the class avoids `double.Parse`
(netstandard2.0 inside .NET Framework hosts) without implying a requirement. Comment only. Not obsoleted by the Java
retirement: the class models GenOpt's arithmetic, which native reproduces.

## Decisions and assumptions

- `Run()` is removed, not repaired (its `cmd /c` launch was already broken under GenOpt 3.1.1, PR3 finding 1).
- The whole GenOpt simulation-launch configuration goes with it: it existed only to tell Java GenOpt how to start
  TasGenExecute, and its default held a `cmd /c` command with a personal path.
- The GenOpt-format input writers stay: they are the compatible definition, not execution, and are consumed.
- `GPSCoordinateSearch` -> `NotSupportedException` (see above); Grasshopper users see "Not supported by the native route"
  instead of "Invalid GenOpt settings"; SAM_UI never offered it.
- Rules move to SAM_Tas, not SAM.Math: no SAM.Math binary change (avoids the stale-SAM.Math shadowing risk) and the rules
  are about a Tas GenOpt run (cancellation withholding, refusal kinds).
- Native numerical behaviour is unchanged: `RunNative`, the mapping of supported algorithms, the evaluator and the kernel
  are untouched (golden replay and run tests green).

## Tests (SAM_Tas)

- `LegacyRouteRetiredTests` (new): `GenOptDocument` has `RunNative` and no other `Run*`; no Java launch property; the
  launch types are gone; no `TasGenOptJavaPath`/`Create.Command`; IL scan: **only `TasGenExecuteObjectiveEvaluator`
  calls `Process.Start`**; metadata: no `SetProjectDirectory`, no `Registry`/`RegistryKey`; no `java.exe`, `javaw`,
  `.jar`, `genopt.GenOpt`, `GenOpt.bat`, `cmd /c`, `cmd.exe`, `start /WAIT`, `SOFTWARE\EDSL` string.
- `NativeGenOptOutcomeTests` (new, real kernel results): Success, simulation limit, golden section (first-lowest on a
  tie, lowest, interval), NaN never best, cancel after run withheld (kernel outcome kept, no best/interval), Cancelled,
  EvaluationFailed, InitialPointInfeasible, exactly three normal ends, null guards, `IsLower` (tie, NaN, null) and its
  fold equals `Best`, refusal wording (5 kinds + null) incl. GPSCoordinateSearch.
- `MappingTests`/`NativeRunTests` updated for B4 (NotSupported, list without GPSCoordinateSearch, a
  GenOptCompatibility refusal still creates nothing).
- Unchanged and green: golden replay (25 traces bit for bit), protocol, workspace, cancellation and run tests.

## Validation

See the PR description of each repository for the exact numbers on the final heads.

## Real-system regression (B8)

**Licensed acceptance not rerun.** PR6 removes code that no supported route reaches and moves pure result-reading rules
without changing their outcome; `RunNative`, the evaluator, the supported mapping and the kernel are unchanged. Native
regression is the stub-driven `RunNative` suite (golden replay bit for bit, cancellation, failures) plus the consumers'
report tests; PR3 and PR5 licensed acceptance (GoldenSection 11/11 and GPSHookeJeeves 16/16 bit-identical, cancel, no
Java/cmd/registry) stand.

## Deployment (B10) - separate follow-up, not done here

- SAM_Deploy needs no change for PR6 to be correct. When its pins move to the native PRs: ship one current `SAM.Math.dll`
  everywhere, and `SAM.Analytical.Tas.GenOpt.dll` beside the SAM_UI app (carried from PR4/PR5).
- New, from this PR: installed machines keep the old `resources\Analytical\Tas\GenOpt\GenOpt.bat` and `config.txt`
  (the installer does not delete removed files); an `[InstallDelete]` entry in SAM_Deploy would remove them. Inert (no
  code reads them).

## Risks / unresolved

- Public API removal in `SAM.Analytical.Tas.GenOpt` (`Run`, the launch members/types, `TasGenOptJavaPath`,
  `Create.Command`). Searched every SAM-BIM repository on this machine: no consumer outside the removed route. A
  third-party script calling `Run()` would stop compiling (it could not run successfully anyway).
- GPSCoordinateSearch exception type changed (Compatibility -> NotSupported); both consumers updated in the same PR set.
- Order: Grasshopper and SAM_UI PRs need the SAM_Tas PR (CI resolves it by branch name until it merges).

## Next step

Owner review of the four PRs; merge SAM_Tas first, then SAM_Tas_Grasshopper and SAM_UI; then the post-merge
`PROJECT_PROGRESS.md` closeouts; then the SAM_Deploy shipping task.
