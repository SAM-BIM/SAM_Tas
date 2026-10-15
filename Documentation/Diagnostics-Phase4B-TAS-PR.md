# Unified Diagnostics, Phase 4B (SAM_Tas integration) - PR record

Base: `sow/2026-Q4` (`0589f0d`). Branch: `feature/tas-diagnostics-phase4b-q4`. Dependencies: SAM `sow/2026-Q4` (`6167d47`, `SAM.Core.Diagnostic`, `Diagnostics`, `DiagnosticSeverity`, `DiagnosticDetail`); no SAM, SAM_OCCT, SAM_OpenStudio or SAM_Tas_Grasshopper change.
Validated content SHA: `920257bacfe6dfb4553b5aa1ed0bdd5515e34259` (all code and tests). Record-only commits follow; the final PR head and hosted checks are in the PR description.
Draft. Merge needs owner approval (AGENTS.md). `PROJECT_PROGRESS.md` is not touched (post-merge closeout only).

## Status
Implementation complete. Local Linux build, tests, IL comparison and plan comparison done (below). **Hosted Windows CI, Windows-only native tests and licensed EDSL TAS acceptance are not run by this record** - see "Not run" and "Windows / EDSL acceptance".

## Investigation: what the TAS diagnostic producers really are
TAS does **not** have OpenStudio's single typed diagnostic. It has eight different mechanisms; only some carry a structured severity. Read from the source of `SAM.Core.Tas`, `SAM.Analytical.Tas`, `.TPD`, `.TM59`, `.GenOpt`.

| # | Producer (entry points) | Form | Authoritative severity? | Decision |
|---|---|---|---|---|
| 1 | `SimulationEvidence` - the one funnel for TBD to TSD (`Modify.Simulate`), TPD in place (`Simulate`, `SimulateSystems`), `Create.NoIzamThermalSource`, `ThermostatBridge`, `SystemVentilationRoute` | two string lists, `Refusals` and `Notes`, plus `Completed` and `NativeDiagnosticKind` | **Yes, by structure**: a refusal is by definition a reason the run is not evidence of a simulation | adapter |
| 2 | `SimulationDiagnostic` (raw `ITPD/IPlantRoom/ISystem.Simulate` return text) | text, classified into `KnownSuccess/KnownFailure/Unrecognised/Silent` by the production gate (a measured vocabulary, existing code) | `KnownFailure` already becomes a refusal; `Unrecognised` is "neither success nor failure" | not re-classified; consumed through #1 |
| 3 | `WorkflowCalculator.Notes` (what `Modify.RunWorkflow` hands the Grasshopper `RunWorkflow` helper as runtime messages) | `IReadOnlyList<string>`; its own refusals are **unprefixed** ("... Nothing was run.") | Only at the writing site | funnel edit: severity chosen at each site |
| 4 | Step notes `Modify.UpdateBuildingElements`, `UpdateApertureDefinitions`, `SetApertureTypes` (`out List<string> notes`) | strings; the producers document "problems carry `Modify.NotePrefix_Issue`" and the steps themselves test for it | **Yes, by the producers' own marker constant** (not by wording) | adapter + funnel |
| 5 | `T3DImportReport` (`Convert.ToT3D(..., out report)`) | `Skipped`, `Notes` lists, `Success`, `ZoneNames` | **Yes, by list**: the report defines Skipped as "a hole in the thermal model" and Notes as "a caveat that does not lose geometry" | adapter |
| 6 | `SAM.Core.Log` from `Create.Log(...)` (thermal transmittance calculation data) | `LogRecord` with `LogRecordType` (Message/Warning/Error/**Undefined**) | **Yes**, except `Undefined` | adapter |
| 7 | `OptimisationDiagnostic` (GenOpt: `TasScriptDiagnostics`, `TasModelRunner` sizing refusal, `TasOptimisationDefinitionException.Diagnostics`) | typed, severity + stable `OPT###` code + path | **Yes** | adapter |
| 8 | About 30 `out string refusal` queries (`ToSAM(TSD)`, `SetApertureType`, `GetOrCreateSchedule`, `BuildingElementDefinition`, ...) | one reason string per call, non-null means refused | **No**: the severity is the caller's decision (`UpdateBuildingElements` turns them into `ISSUE` notes) | reach users through #4; not adapted |
| - | `PartODiagnosticLog` (TM59) | a JSON Lines run record with its own schema (`PartODiagnostic:v1`) | n/a - a data artefact, not a message list | out of scope; its file output is unchanged |
| - | `CopyResults` / `LogShadeRoundTrip` debug logs (`debugLog` output) | free text | none | not adapted; legacy path kept |

Exceptions: the COM calls are already caught into `SimulationEvidence.RecordCallFailed` (a refusal, #1) and, in the T3D conversion, into `Skipped` entries (#5). Uncaught exceptions propagate exactly as before.

## Architecture
Additive adapters in the `Query` partial classes, one edit at the existing funnel; the same pattern as the OpenStudio phase (SAM_OpenStudio#28). Nothing in SAM.Core was needed. No producer algorithm, export, simulation or numeric path was changed.

- `SAM.Core.Tas`: `TasDiagnosticCodes` (17 released codes); `Query.ToDiagnostics(SimulationEvidence, detail)`; `Query.ToDiagnostics(Log, subject, detail)`.
- `SAM.Analytical.Tas`: `Query.ToDiagnostics(T3DImportReport, detail)`; `Query.StepNotesDiagnostics(notes, subject, detail)` and `Query.IsIssueNote`; `WorkflowCalculator.GetDiagnostics(detail)`. The 14 `notes.Add(...)` and 3 `notes.AddRange(...)` sites of `Calculate` now go through two private methods (`AddNote`, `AddNotes`) that write the **same text, in the same order** to `Notes` and a structured record beside it. The IL change of `Calculate` is exactly those 17 call sites (below).
- `SAM.Analytical.Tas.GenOpt`: `Query.ToDiagnostic(OptimisationDiagnostic)` and `Query.ToDiagnostics(IEnumerable<OptimisationDiagnostic>, detail)`.

Default detail of every adapter is `Detailed` (drops nothing, matches what the legacy lists show); a narrower level only narrows Information.

## Mappings and the Information-detail policy
Severity is taken from what the producer recorded; no message is read. The only text-prefix rule is the producers' own `Modify.NotePrefix_Issue` constant (#4), documented in their XML comments and tested by the steps themselves.

| Source record | Severity | Detail | Code |
|---|---|---|---|
| `SimulationEvidence.Refusals[i]` | Error | Summary (always kept) | `TAS-SIM-001` |
| `SimulationEvidence.Notes[i]` | Information | Standard | `TAS-SIM-002` |
| `NativeDiagnosticKind == Unrecognised` | **Warning** (kept at every level; never Information, never an invented Error) | Summary | `TAS-SIM-003` |
| not `Completed` and no refusal (for example TPD in place, results not yet reconciled) | Warning | Summary | `TAS-SIM-004` |
| `Completed` | Information (`ToString()`) | Summary | `TAS-SIM-005` |
| `Log` Error / Warning / Message | Error / Warning / Information | Summary / Summary / Standard | `TAS-LOG-003/002/001` |
| `Log` `Undefined` type | **Warning** (not dropped at any level, does not claim a failure the producer never stated; OpenStudio maps an out-of-range enum to Error, here `Undefined` is a legitimate "not stated") | Summary | `TAS-LOG-004` |
| `T3DImportReport.Success == false` | Error | Summary | `TAS-T3D-001` |
| `Skipped[i]` | Warning | Summary | `TAS-T3D-002` |
| `Notes[i]` (includes the preserved physical thickness note) | Information | Standard | `TAS-T3D-003` |
| totals line / each created zone (`ZoneNames`, subject `Space <guid>`) | Information | Summary / Detailed | `TAS-T3D-004/005` |
| workflow refusal sites (both contradictory-instruction gates, canonical TBD missing / same file / copy failed, surviving IZAM, T3D conversion refused) | **Error** | Summary | `TAS-WF-001` |
| workflow decision / assumption / totals (canonical start, UseWidths caveat, T3D totals, reversed surfaces, ventilation gains) | Information | Summary | `TAS-WF-002` |
| workflow "Removing IZAMs: none remain." | Information | Standard | `TAS-WF-002` |
| step note with the `ISSUE` marker (incl. each T3D skipped entry) | Warning, marker dropped from `Message` | Summary | `TAS-WF-003` |
| step note without the marker (one-line outcomes: counts, "nothing to reuse") | Information | Summary | `TAS-WF-002` |
| `OptimisationDiagnostic` Info / Warning / Error / out-of-range | Information / Warning / Error / **Error** | Standard / Summary / Summary / Summary | its own `OPT###` |

Every step-note site was read: all unprefixed lines of the three step producers are one-line outcomes (`notes_Summary`) or "nothing to do" lines; every problem carries the marker. Steps never produce an Error (they report issues and carry on); only the calculator's own refusal sites do.

Identity: `Subject` is the simulated document path (evidence), the workflow step that wrote the note (workflow; `Workflow` for the pre-run gates), the JSON path (GenOpt), `Space <guid>` (created zones), `Direct T3D conversion` (report), or the caller's `subject` (Log). `SourceIndex` is the index in the producer's own list (`Notes`, `Refusals`, `Skipped`, ...), so a diagnostic maps back to its legacy line; for the workflow it is the index in `Notes`.

## Engineering-invariant evidence (diagnostics change no engineering behaviour)
Independent of the unit tests. Tools and outputs are committed under `Documentation/Diagnostics-Phase4B-evidence/`.

1. **IL comparison, base `0589f0d` vs head, same toolchain and shim** (`il-compare-summary.txt`; Mono.Cecil, per method the sequence of opcodes and resolved operands, compiler-generated numbering normalised). Across 7 assemblies (`SAM.Core.Tas`, `SAM.Analytical.Tas`, `.TPD`, `.TM59`, `.GenOpt`, `SAM.Weather.Tas`, `SAM.Geometry.Tas`): **4,519 base methods, 4,515 identical IL, 4 changed, 0 removed, 11 added** (the new adapters and the two funnel methods). The 4 changed are `WorkflowCalculator.Calculate` and its three constructors (field initialiser). The IL difference of `Calculate` is exactly: 14 `notes.Add` replaced by `AddNote`, 3 `notes.AddRange` replaced by `AddNotes`, one `diagnostics.Clear()`, plus the severity/code constants. Nothing in `Convert.ToT3D`, `Convert.ToTBD`, `Convert.ToSAM`, any `Modify.*`, `Create.*`, `Query.T3DImportPlan`, the TPD assembly or the TM59 assembly changed. Because no public method disappeared or changed signature, every public member the Grasshopper components call is still there.
2. **T3D plan byte comparison** (`plan-dump-sha256.txt`, `plandump-Program.cs.txt`): the complete `T3DImportPlan` (zones, elements and their widths, surfaces, openings, window types, shades, and the full report with Skipped and Notes) was dumped for 15 synthetic scenarios of the direct-route test harness (box, windows, shared window types, two zones, adiabatic, stacked, stair void, door and rooflight, grid, shade on/off, widths off) with the base and the head build: **byte-identical** (SHA-256 `863ecde1...c8aadb9`). Only normalisation: GUIDs renumbered by order of first appearance, because the synthetic models create fresh random GUIDs on every run (two runs of the base itself differ before this, after it they are identical).
3. Physical thickness: a test asserts the T3D element width stays the source `PhysicalThickness` (0.25 m) while the layered `ConstructionThickness` stays 0.30 m, both unchanged by the adapter; the preserved-thickness line is a Standard Information note, not a warning. Missing optional opening controls: a model with three openings and no opening properties yields no Warning and no Error. `UseBEWidths`, space volumes, panel assignments, apertures, adjacency, schedules, TPD, ventilation, internal conditions, HVAC and simulation inputs: code identical per item 1.

Not possible here: byte comparison of the generated `.t3d/.tbd/.tpd/.tsd` files and numerical results. They need TAS (Windows COM) - see "Windows / EDSL acceptance". Item 1 shows the code that writes them is unchanged.

## Tests
New: `SAM.Analytical.Tas.TM59.Tests/DiagnosticsAdapterTests.cs` (40 test cases, 5 fixtures) and `SAM.Analytical.Tas.GenOpt.Tests/DiagnosticsAdapterTests.cs` (6 tests). They use real objects: `SimulationEvidence` runs over real files (completed, missing output, TAS error log, call failed, in place, every native-answer kind), real `Create.Log`, `T3DImportReport`s from the real `T3DImportPlan` over the synthetic models, the real `WorkflowCalculator.Calculate` refusal gates, the real `TasScriptDiagnostics` and a real `TasOptimisationDefinitionException`. They cover: severity preservation and unknown severity; code, message, subject, index and order; Summary within Standard within Detailed (Information) and Warning/Error retained at every level; legacy strings pinned as literals and unchanged by the adapters; null, empty, malformed and failed inputs; non-mutation and determinism; public-surface and code-stability checks; "no severity from text" (a step note that says "failed ... error" without the marker stays Information; an `ISSUE` note that says "all fine" is a Warning).

**Mutation checks** (`mutation-results.txt`): 16 deliberate faults (a refusal downgraded to Warning, later refusals dropped, an unrecognised answer made Information, Log undefined made Information, skipped entries made Information, an unsuccessful conversion not reported, the issue marker ignored, severity inferred from text, a workflow refusal downgraded, the funnel altering the legacy text, diagnostics not cleared, a released code changed, GenOpt warning/undefined downgraded) were each applied, built and run; **all 16 are caught** by at least one test that is executable on Linux. The sources were restored and verified by SHA-256.

## Validation actually performed
### Local (Ubuntu 24.04, .NET SDK 8.0.131, Debug; machine-local, not hosted CI)
There is no Windows or .NET Framework MSBuild here, so a **local build shim outside the repository** was used (`Directory.Build.targets` in the parent folder, removing the `COMReference` items, linking instead of embedding the `Interop.*` assemblies - embedded interop types need Windows type equivalence - and skipping `PreparationBoundaryTests.cs`, which the Ubuntu Roslyn mis-compiles). SAM and the sibling repositories (`SAM_gbXML`, `SAM_Systems`, `SAM_Mollier`, `SAM_SolarCalculator`, all `sow/2026-Q4`) were built as in CI; SAM needed one local, uncommitted line rewrite in a scratch copy (`new([...])` in `AddTransferAirDoorsByPartF.cs`, same Roslyn issue). No repository file was changed for this.

| Check | Base `0589f0d` | Head `920257b` |
|---|---|---|
| Build (7 SAM_Tas libraries, Debug) | 0 errors | 0 errors |
| `SAM.Analytical.Tas.TM59.Tests` | 1139 total, 1125 passed, 14 failed | **1179 total, 1162 passed, 17 failed** = +40 new |
| `SAM.Analytical.Tas.GenOpt.Tests` | 426 total, 375 passed, 51 failed | **432 total, 381 passed, 51 failed** = +6 new |

The failures are **environmental, identical on base**: 14 TM59 (4 `NullReferenceException` in `SetProjectDirectory`, which needs the Windows registry; 1 `NullReferenceException` in `GbXMLRouteIdentityAcceptanceTests`; 8 `SAM_Systems` air-handling fixtures, "no cooling coil could be drawn"; 1 undeletable-file test that does not hold when running as root) and 51 GenOpt (48 need `TasGenExecute.exe`, which is not installed; 3 are Windows file-sharing and timing tests). The head's failing set is the base's 14 plus **3 new gate tests** (`ARefusedRun_...("canonical-missing" | "canonical-is-target" | "canonical-copy-fails")`) that fail on this machine for the same registry reason as the 4 existing `WorkflowCanonicalTBDTests` (`SetProjectDirectory` runs before those gates). To still execute them, a scratch copy had that one registry call commented out: there all `WorkflowDiagnosticsTests` (including these 3) passed and the existing canonical tests behave as on base except `Calculate_WhereTheTwoPathsDifferOnlyInSpelling...` (Windows path semantics). That scratch result is evidence, not a substitute for the Windows run. Lists: `test-results-summary.txt`.

### Hosted CI
See the PR description for the checks on the final head (`build` = Windows 2022 rebuild of all repositories plus `SAM.Analytical.Tas.TM59.Tests` and `SAM.Analytical.Tas.GenOpt.Tests`; `spdx`). Those run the 3 gate tests for real.

### Not run
- Windows-only native TAS tests and any licensed EDSL TAS run (BLOCKED here: no Windows, no TAS).
- Byte comparison of generated `.t3d/.tbd/.tpd/.tsd` and numerical results (BLOCKED, same reason; replaced by the IL and plan comparisons above).
- Interactive Grasshopper/Rhino canvas, saved `.gh/.ghx` loading. SAM_Tas has no Grasshopper project; `SAM_Tas_Grasshopper` was only read (`d827409`).

## Grasshopper and compatibility
- **No Grasshopper contract change.** Nothing in `SAM_Tas_Grasshopper` or any component, GUID, name, parameter, order, access, default, output or version changed; no `diagnosticDetail_` input, no new balloon. The adapters are not consumed by any component yet.
- The only consumer of the changed class in `SAM_Tas_Grasshopper` is `Modify/RunWorkflow.cs`, which does `notes.AddRange(workflowCalculator.Notes)`: `Notes` is the same read-only text list with identical content and order (pinned by tests and by the IL comparison).
- The one deliberate API addition on an existing class is `WorkflowCalculator.GetDiagnostics`. One tolerated difference: `AddNotes` ignores a `null` step list, whereas `notes.AddRange(null)` threw; the three step producers always return a non-null list (`notes = []` on entry).
- Library filtering: `GetDiagnostics(DiagnosticDetail)`, `ToDiagnostics(..., DiagnosticDetail)`.

## Decisions and assumptions
- The workflow's own refusals are **Errors by site**, chosen where they are written; the "ISSUE" marker is only used for the step notes, whose producers document it. The T3D conversion refusal carries the marker in the legacy text but stops the run, so it is an Error.
- `Unrecognised` TAS text is a Warning, not Information: it is unknown and might be an error; the existing production decision (not a refusal) is unchanged.
- Evidence and T3D notes are Standard (per-stage / per-item outcomes), not Summary: Summary shows totals and everything that was not imported. The text of notes carries no structure to split them further, so no note is classified by wording.
- `Log` `Undefined` is a Warning (see table). `OptimisationDiagnostic` Info has no detail dimension and is Standard.
- Messages are the legacy text (marker removed for issues); the legacy strings stay in `Notes`, `Refusals`, `Skipped`, `ToString()` and `Log`.

## Limitations (candidates for a later phase, each its own decision)
- `T3DImportReport` entries and step notes carry panel/aperture/space identity only inside the sentence; structured identity (SAM Guid, TAS object name) needs a change at the producers (about 20 report sites, many step-note sites) and is not done. `Subject`/`SourceIndex` locate the entry instead.
- `SimulationEvidence` refusals share one code (the evidence stores strings only).
- The adapters are not wired into Grasshopper or the benchmark; no `diagnosticDetail_` input.
- `out string refusal` queries, `CopyResults`/`LogShadeRoundTrip` debug logs and `PartODiagnosticLog` are not adapted (no authoritative severity / data artefact).
- `OptimisationDiagnostic` to `Diagnostic` is generic and would belong in `SAM.Core.Optimisation`; SAM was not changed, so it lives in `SAM.Analytical.Tas.GenOpt`.
- `SimulationEvidence.Notes` contain timestamps of the run, so their text is not reproducible between runs (unchanged legacy behaviour).

## Files changed
New: `SAM_Tas/SAM.Core.Tas/Classes/TasDiagnosticCodes.cs`, `SAM_Tas/SAM.Core.Tas/Query/ToDiagnostics.cs`, `SAM_Tas/SAM.Analytical.Tas/Query/ToDiagnostics.cs`, `SAM_Tas/SAM.Analytical.Tas.GenOpt/Query/ToDiagnostics.cs`, `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/DiagnosticsAdapterTests.cs`, `SAM_Tas/SAM.Analytical.Tas.GenOpt.Tests/DiagnosticsAdapterTests.cs`, this record and `Documentation/Diagnostics-Phase4B-evidence/*`.
Modified: `SAM_Tas/SAM.Analytical.Tas/Classes/WorkflowCalculator.cs` (private field and two private methods, `GetDiagnostics`, 17 call sites).
Untouched: every exporter, converter, TPD, TM59 and Weather/Geometry source, all `.csproj`, workflows, `AGENTS.md`, `PROJECT_PROGRESS.md`.

## Windows / EDSL acceptance (exact requirements, none done)
1. Hosted `build` green on the final head, in particular `SAM.Analytical.Tas.TM59.Tests` (1179 test cases; the Linux-only environmental failures above are not expected on Windows) and `GenOpt.Tests` (432).
2. Licensed TAS, same model, base vs this branch, direct and gbXML routes: the generated `.t3d`, `.tbd`, `.tpd` byte-identical after normalising only the known run metadata (timestamps, GUID assignment), `.tsd` results equal, `WorkflowCalculator.Notes` text and order identical.
3. Grasshopper: open a saved definition with the Tas components - loads, no update mark, `RunWorkflow` runtime messages unchanged.

## Next step
Owner review of the draft PR; after merge a docs-only `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4` (`[skip ci]`). Separate, owner-decided follow-ups: wire the adapters into the Tas Grasshopper components (optional hidden `diagnosticDetail_`, canvas balloons), structured identity at the T3D report and step-note producers. Recommended for the next piece: Opus, high effort, new session, Plan mode.
