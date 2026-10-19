# S01a - TAS silent-failure detection (SAM_Tas) - PR record

Roadmap: SAM `documentation/SIMULATION_ROADMAP_Q4_2026.md`, task S01 (Phase A). S01a is the SAM_Tas half; SAM_UI
propagation is S01b (not started here). Owner approved SAM_Tas write access for S01a and the revised S01 plan (D1-D4).

Base: `sow/2026-Q4` (`a34a298d`). Branch: `feature/s01a-tas-silent-failure`. Dependencies: SAM `sow/2026-Q4`
(`62b54156`), unchanged. No SAM, SAM_UI, SAM_Tas_Grasshopper or HVAC worktree/branch change.
Validated content SHA: `a3be6931` (all code and tests). Code commits: `6ccc2d53` (S01a), `0b0257ee` (document-level
overload, GPT-6 review 1), `a3be6931` (separate-output error-log path, GPT-6 review 2, plus a test-fixture timestamp fix
for the hosted D4 failure on `6ef9b3d0`). Record-only commits follow; the final PR head and hosted checks are in the PR
description. `PROJECT_PROGRESS.md` is not touched (post-merge closeout only).

## Status
Implementation, unit tests and licensed checks complete on 2026-10-19 (this laptop, TAS licensed). GPT-6 reviews: main
solution correct; the document-level overload gap (`0b0257ee`) and the separate-output error-log path (`a3be6931`) are
corrected and re-validated below. Awaiting owner review and hosted CI on the final head. Not merged.

## Problem (measured)
`TBDDocument.simulate` (interop signature `Int32 simulate(...)`) returns `-1` when the simulation ran and `0` when
TAS refused it. Measured here with the box model and one unbalanced inter-zone air movement (outside -> zone,
which TAS refuses per SAM `PartO-TAS-VALIDATION.md`, Iteration 1a root cause):

| Input | Return | TSD | Error log |
|---|---|---|---|
| `box-izam.tbd` (one unbalanced IZAM) | `0` | 22 bytes | `izam_error_log.txt` = `Simulation Failed` |
| `box-ok.tbd` | `-1` | 1 034 856 bytes | none |

Before this change (baseline `a34a298d`):

- the legacy `Modify.Simulate(path_TBD, path_TSD, ...)` (Grasshopper `TasSimulate`'s `Successful`) returned **True**
  for the refused run (`L1-baseline-legacy.txt`);
- `WorkflowCalculator` ignored the result of its simulate call and read the 22-byte stub TSD: the run **hung** in
  "Adding Results" (TSD.exe idle, 0.3 s CPU) and was killed after about 12 minutes (`L1-baseline-workflow-HUNG.txt`);
- the evidence overload already refused (error log + size), but recorded no return code, and a refusal stub larger
  than the 1024-byte threshold with no error log would have passed it.

## Work completed
1. `SAM.Core.Tas/Classes/SimulationEvidence.cs`
   - `NativeReturnCode` (`int?`), `NativeReturnCodeRecorded`, constants `NativeReturnCode_Ran = -1`,
     `NativeReturnCode_Refused = 0`.
   - `RecordCallReturned(object nativeReturnCode)`: `-1` notes "ran" and returns true (still judged by `Conclude`);
     `0` refuses ("TAS refused the simulation: TBDDocument.simulate returned 0."); any other integer, `null`, or a
     non-integer (e.g. `-1.0`, `"-1"`) refuses and keeps the raw value and its type in the text. Integral types other
     than `int` are read by value when they fit in `Int32`.
   - Every refusal of a run with a recorded code carries ` TAS return code: <c>; TAS error log: <path>.`; the error-log
     refusal carries ` (TAS return code: <c>)`. Runs that record no code (TPD, legacy callers) keep their refusal text
     byte-for-byte (`DiagnosticsAdapterTests` pins this and passes).
   - `Path_ErrorLog` for `SeparateOutputFile` is derived from the TSD (`a3be6931`; see Decisions); `InPlaceDocument`
     keeps the document.
   - Unchanged: `Prepare`, `Conclude` logic, `Completed`, `TrivialOutputLengthThreshold` (1024), the string
     `RecordCallReturned(string)` TPD path, `Query.ToDiagnostics` mapping and codes. An attributable error log still
     refuses when TAS returned `-1` (test A5).
2. `SAM.Analytical.Tas/Modify/Simulate.cs`
   - New `Simulate(path_TBD, path_TSD, first, last, SizingType, IEnumerable<SurfaceOutputSpec>, out SimulationEvidence)`
     with the legacy preparation moved in unchanged (sizing types, surface output specs, save when dirty). It records
     the return code and saves the TBD after the call only when TAS returned `-1` (the successful-run save is kept).
   - The existing 5-argument evidence overload delegates to it (`SizingType.Undefined`, no specs).
   - The legacy 6-argument path overload calls it and returns `simulationEvidence.Completed`; the 4-argument overload is
     unchanged and still delegates to it. Signatures preserved, so `SAM_Tas_Grasshopper` `TasSimulate`'s `Successful`
     now reflects evidence without a Grasshopper change.
   - Document-level overloads `Simulate(TBDDocument, path_TSD, ...)` and `Simulate(SAMTBDDocument, ...)` (which
     delegates): no caller in any SAM-BIM repository (only `_ext_validation/edl-ost-pilot`, outside the repos). At
     `6ccc2d53` they answered `code == -1 && WaitToUnlock(path_TSD)`, which still accepted a leftover, empty or stub TSD
     and ignored an error log (GPT-6 review). `0b0257ee` routes them through a private `SimulateToTSD(Func<int>,
     path_TSD, out SimulationEvidence)`: the same `SimulationEvidence` rules (leftover TSD and error log cleared first;
     success needs -1, a fresh non-trivial TSD, no error log for this run, and the file unlocked). A `TBDDocument`
     carries no path, so the record is keyed on the TSD, which is where TAS writes the log (see Decisions). Public
     signatures unchanged; a throw from the COM call still propagates, as before.
3. `SAM.Analytical.Tas/Classes/WorkflowCalculator.cs`
   - `SimulationEvidence` property (private set), reset to null with the notes at the start of every `Calculate`.
   - "Simulating Model" uses the evidence overload; when not evidenced, each refusal becomes an Error note with code
     `TAS-SIM-001` (`AddSimulationRefusals`), `Ended` is raised and `null` is returned (the established refusal
     convention) before the TSD is read, so no result and no `<tbd>.json` is written.
   - After `AddResults`: `Query.ZoneResultsRefusal(spaceCount, results)`; a refusal is added to the evidence and as a
     `TAS-SIM-001` Error, then the same return-null path.
4. `SAM.Analytical.Tas/Query/ZoneResultsRefusal.cs` (new, pure): refuses when the model has spaces and the returned
   results hold no `SpaceSimulationResult`; null otherwise. Predicate confirmed against the real successful structure:
   the box model's `AddResults` returned 2 `SpaceSimulationResult` (heating, cooling) + 26 `SurfaceSimulationResult`
   (`L4-candidate-addresults-control.txt`); the persisted model also holds 2 `AdjacencyClusterSimulationResult`
   (unmet hours).
5. `benchmark/` - `Producer.AddWorkflowFailure(context, diagnostics)` (COM-free core) writes every Error diagnostic as a
   B1a warning `"<code>: <message>"` and one note ("The TAS workflow refused the run ..."; or "returned no model and
   reported no error" when there are none). `Program.Execute` keeps the `WorkflowCalculator` instance and calls it when
   `Calculate` returned null without timeout or exception, instead of the misleading "No TSD was produced" note.
   Exit code 5, `RunState.Failure` and the schema are unchanged.

## Decisions and assumptions
- The approved plan text (D1-D4, A1-A11, L1-L4) is not in any repository; the test list below maps the brief's
  enumerated cases onto A1-A11. Reviewers holding the plan should check that mapping.
- The refusal stub measured here is **22 bytes**, not ~22 KB. Both are tested; a ~22 KB stub is caught by the return
  code alone (A4), because it is above the unchanged 1024-byte threshold.
- `RecordCallReturned(object)` is an overload beside `RecordCallReturned(string)`; a `string` argument still binds to
  the TPD text path (test A10). The TBD caller passes `(object)int`.
- `TAS-SIM-001` is used for the zone-results refusal too (it is a reason the simulation is not evidence), and the
  refusal is also recorded on `SimulationEvidence` so S01b can read one object.
- On a refused run the TBD is no longer saved after the call (previously it was, because `WaitToUnlock` answered true);
  the pre-run preparation save is unchanged.
- The legacy path overload now also deletes a leftover TSD/error log before the run (the evidence `Prepare`) and
  returns false instead of throwing when the COM call throws; both are the evidence overload's existing behaviour.

- **TAS writes the error log beside the TSD, with the TSD's basename** (measured: `tbddir\alpha.tbd` simulated to
  `tsddir\beta.tsd` gave `tsddir\beta_error_log.txt` and nothing beside the TBD; `errorlog-location-probe.txt`).
  Until `a3be6931`, `SimulationEvidence` derived the log from the document (`<tbd basename>_error_log.txt` beside the
  TBD), which missed TAS's log whenever the TSD was named differently. `a3be6931`: for `SeparateOutputFile`,
  `Path_ErrorLog` is derived from `path_Output` (the TSD); `InPlaceDocument` keeps the document. `Prepare` (stale-log
  deletion or timestamping), `Conclude` (reading), and the refusal text all read `Path_ErrorLog`, so they follow; no
  signature, return-code logic or threshold changed. Compatibility: every production caller (`Modify.Simulate` path
  overloads via `WorkflowCalculator`/`benchmark-tas`, `Create.NoIzamThermalSource` via `Query.Path_TSD`,
  `ThermostatBridge` via `Path.ChangeExtension`, the document-level `SimulateToTSD`) uses a TSD with the TBD's folder
  and basename, so its log path is unchanged; only a differently named pair (e.g. a user-supplied Grasshopper
  `TasSimulate` TSD) changes, which is the fix. Licensed confirmation: `errorlog-path-licensed.txt`.

## Files changed
`SAM_Tas/SAM.Core.Tas/Classes/SimulationEvidence.cs`, `SAM_Tas/SAM.Analytical.Tas/Modify/Simulate.cs`,
`SAM_Tas/SAM.Analytical.Tas/Classes/WorkflowCalculator.cs`, `SAM_Tas/SAM.Analytical.Tas/Query/ZoneResultsRefusal.cs`
(new), `benchmark/SAM.Analytical.Tas.Benchmark/Producer.cs`, `benchmark/SAM.Analytical.Tas.Benchmark.Cli/Program.cs`;
tests `SimulationReturnCodeTests.cs` (new), `ZoneResultsRefusalTests.cs` (new), `SimulateDocumentLevelTests.cs` (new,
`0b0257ee`; log timestamp in `a3be6931`), `ErrorLogLocationTests.cs` (new, `a3be6931`), `WorkflowCalculatorTests.cs`,
`benchmark/.../WorkflowFailureTests.cs` (new); this record and `Documentation/S01a-SilentFailure-evidence/`.

## Tests (local, Windows, on the `a3be6931` working tree, final build)
All rows are **simulated evidence**: real files on disk, with the TAS COM response stood in for (no licence needed).
Licensed end-to-end evidence is the next section.
| ID | Case | Test(s) | Result |
|---|---|---|---|
| A1 | return 0 refuses, names code and log path | `A1_ReturnCodeZero_...`, `A1_RecordCallReturned_AnswersTheVerdict` | PASS |
| A2 | -1 with a fresh TSD completes | `A2_ReturnCodeMinusOne_WithAFreshTSD_Completes` | PASS |
| A3 | unexpected int (1, -2, 2, MinValue), null, non-integer (-1.0, "-1"), other integral types | `A3_*` (8 cases) | PASS |
| A4 | 22-byte and ~22 KB refusal stubs | `A4_*` | PASS |
| A5 | attributable error log despite -1 | `A5_AttributableErrorLog_...` | PASS |
| A6 | empty, missing, stale, leftover TSD | `A6_*` | PASS |
| A7 | zone-results refusal | `ZoneResultsRefusalTests` (4) | PASS |
| A8 | code and log path reach diagnostics (evidence and workflow) | `A8_Refusal_ReachesTheDiagnostics_...`, `A8_ARefusedSimulation_ReachesNotesAndDiagnostics_...`, `Calculate_ResetsTheLastRunsSimulationEvidence` | PASS |
| A9 | benchmark failure handling, valid Failure document, exit 5 | `WorkflowFailureTests` (3) | PASS |
| A10 | unchanged recording paths (no-code text, string overload) | `A10_*` | PASS |
| D1-D4 | document-level overload: 0 fails (stub and ~22 KB); -1 with a fresh valid TSD succeeds; -1 with missing, empty, 22-byte, stale or leftover TSD fails; an attributable error log fails; an earlier run's log is cleared, not charged; public signatures and null inputs | `SimulateDocumentLevelTests` (11), driving the private `SimulateToTSD` with a delegate in place of `TBDDocument.simulate`; 5 consecutive runs green | PASS |
| E1-E4 | error-log location: `alpha.tbd` + `tsddir\beta.tsd` keys on `tsddir\beta_error_log.txt`; that log refuses despite -1 and a 1 MB TSD (refusal and diagnostics name it); a stale TSD-keyed log is cleared before a successful run; a log beside the TBD is not charged and not deleted; in-place (TPD) keeps the document-keyed log and its refusal | `ErrorLogLocationTests` (5); focused S01a/evidence/diagnostics fixtures green 5 consecutive runs | PASS |
| A11 | existing regression suites | TM59 **1249/1249**, GenOpt **432/432**, Benchmark **26/26** | PASS |

Commands (Windows; build with `APPDATA` redirected so `%APPDATA%\SAM` is not touched):
`MSBuild SAM_Tas.sln /t:Restore` then `/t:Build /p:Configuration=Release`;
`dotnet build` then `dotnet test --no-build SAM_Tas/SAM.Analytical.Tas.TM59.Tests/...csproj -c Release` (test-bin
`SAM.Core.Tas`/`SAM.Analytical.Tas` hashes checked equal to `build\`); `dotnet test SAM_Tas/SAM.Analytical.Tas.GenOpt.Tests/...`;
`dotnet test benchmark/SAM.Analytical.Tas.Benchmark.Tests/... --filter TestCategory!=Simulation`.

## Licensed checks (TAS on this laptop, 2026-10-19)
Machine-local folder `C:\TasOut\s01a` (inputs, both build snapshots, runs; not committed). Committed:
`Documentation/S01a-SilentFailure-evidence/` (harness source, comparer, outputs, hashes). Inputs: `box-aperture.json` and
`Budapest.epw` from `C:\TasOut\licensed-e2e-20261019\inputs-box` (hashes in `hashes.txt`); `box-ok.tbd` is the baseline
benchmark run's TBD and `box-izam.tbd` is it plus one unbalanced IZAM (harness `mkizam`, baseline DLLs). One harness
binary (only pre-existing APIs, new members by reflection) was copied into a copy of each build folder.

| ID | Check | Result | Evidence |
|---|---|---|---|
| L1 | Unbalanced-IZAM refusal (SAM#92 class) | **PASS**. Legacy path: baseline True -> candidate False. Evidence: `NativeReturnCode=0`, three refusals with code 0 and log path. Workflow: returns null in 1.9 s, three `TAS-SIM-001` Errors, no `run.json` written (baseline hung). | `L1-*.txt` |
| L1b | Refusal through the real `benchmark-tas` CLI | **NOT RUN**: the CLI converts the model itself and IZAMs come only from the per-room-balanced Part O route, so a refusal cannot be injected without changing the producer. Covered by A9 + L1 workflow. | - |
| L2 | Successful box regression (#237 method) | **PASS**, on `6ccc2d53`, `0b0257ee` and again on the final `a3be6931` build. benchmark-tas Success on both; B1a identical apart from timestamp/duration; persisted results exact on all **574** numbers (26 surface, 2 space, 2 cluster results). Workflow-route control (canonical TBD, no sizing): exact on **570** numbers. Legacy path on `box-ok.tbd`: True. | `L2-*.json` (final build), `benchmark-TAS-base1.json`, `benchmark-TAS-cand4.json`, `L2-candidate-legacy-success.txt` |
| LP | Separate-output error-log path, real TAS (`a3be6931`), `tbddir\alpha.tbd` -> `tsddir\beta.tsd` | **PASS**. Refusal: three refusals, the error-log one reading TAS's own `tsddir\beta_error_log.txt` ("Simulation Failed"), every refusal naming that path. Success with a stale `beta_error_log.txt` placed beside the TSD: cleared before the run, `Completed=True`. Agrees with the location probe (`errorlog-location-probe.txt`). | `errorlog-path-licensed.txt` |
| LD | Document-level overload, real TAS (`0b0257ee`) | **PASS**. Unbalanced IZAM: baseline True -> candidate False. Refusal over a 1 MB leftover TSD: False. `box-ok.tbd`: True, both with the TSD beside the TBD and with a differently named TSD in another folder. The -1-with-error-log case cannot be produced with real TAS; covered by D4 only. | `doclevel-licensed.txt` |
| L3 | Folder reuse | **PASS**. (a) after a failed run (stub TSD + error log left): success, log removed, persisted values exact vs fresh baseline. (b) refusal after a success in the same folder: refused, the 1 MB TSD not inherited. | `L3a-*`, `L3b-*`, `L1-L3b-*` |
| L4 | Result-less non-trivial TSD through the real reader | **PASS** (helper level). A 3 499-byte TSD written by TSD (`create`+`save`, no zones): `AddResults` returns null on both builds; candidate `ZoneResultsRefusal` refuses; control (real 1 MB TSD) accepted. Not end to end: TAS cannot be made to return -1 with this file. | `L4-*.txt` |

Noise floor: two baseline runs are exact (`L2-control-base1-vs-base2.json`). Comparator sensitivity: one changed last
digit gives 1 difference (`comparator-tamper-check.json`). Build isolation: between the snapshots only SAM_Tas's own
assemblies differ; all SAM dependencies are byte-identical. Baseline `SAM.Analytical.Tas.dll` `de942e40...`, candidate
`78757bb8...` (`6ccc2d53`), `6fe9bc41...` (`0b0257ee`) and `84fb3e61...` (`a3be6931`) (`hashes.txt`).

## Unresolved issues and risks
- **A failed workflow can leave a previous run's `<tbd>.json` in a reused directory.** `WorkflowCalculator` does not
  delete it, and a refused run leaves it untouched (L3b: older timestamp). The JSON retention policy is deliberately
  unchanged. **S01b must not treat the existence of that file as current-run success**; it must decide on
  `Calculate`'s return value and `WorkflowCalculator.SimulationEvidence` / `GetDiagnostics()`.
- The error-log location rests on one measured refusal (plus the same-name runs). A log TAS wrote within the same
  file-system clock tick as `Prepare`'s start line would be dated before it and read as an earlier run's; real TAS
  writes out of process after the model load, and the hosted D4 failure was this effect in a test stand-in only.
- `ZoneResultsRefusal` judges the returned results, not attachment: results whose names match no SAM space are still
  accepted (pre-existing behaviour).
- Return codes other than -1/0 have never been observed; they fail closed by decision.
- Exceptions inside the legacy path overload are now caught into a false result rather than thrown to Grasshopper.
- S01b must surface `WorkflowCalculator.GetDiagnostics()` / `SimulationEvidence` in Energy Simulation and Run
  Simulation; until then the UI sees a null model with its existing generic handling.

## Recommended next step
Owner (with GPT-6) reviews this PR and its CI; on approval, merge into `sow/2026-Q4`, then add the
`PROJECT_PROGRESS.md` closeout with the merge SHA. Next task S01b (SAM_UI propagation): new session, Claude Opus 5.5,
effort High, Build mode, because it is a separate repository and task.
