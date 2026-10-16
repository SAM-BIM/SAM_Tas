# Phase 4B final Windows / EDSL TAS acceptance - SAM_Tas PR #101

Date: 2026-10-16. Machine: LEGION5MD, Windows 11 Pro 10.0.26200 (native, not WSL), user session.
Validation only: no production code changed, PR #101 not merged, branch not deleted, nothing committed or pushed.

## Verdict
**Merge recommendation: ACCEPT for merge (owner decision).** No regression found. Every legacy output, every generated TAS file (semantically), and every simulation result is identical to baseline; the structured diagnostics behave as specified on real TAS runs. Two gates are partial and are stated as such: Grasshopper definitions were opened and structurally compared but NOT solved (so runtime messages / `RunWorkflow` outputs are NOT TESTED), and one saved definition needed a third-party-object dialog dismissed (details below).

## 1. Environment
| Item | Value |
|---|---|
| OS | Windows 11 Pro 10.0.26200, direct on the local machine (`WSL_DISTRO_NAME` empty) |
| EDSL Tas | `C:\Program Files\Environmental Design Solutions Ltd\Tas` (TAS3D/TBD/TSD/TPD 2.0.0.1), licensed runs succeeded |
| Rhino | 8.35.26251.13001 (also Rhino 9 WIP installed, not used) |
| .NET / MSBuild | SDK 10.0.401 (net8.0 targets), VS 18 Community MSBuild |
| Repos | all SAM-BIM repos local, `origin` = `https://github.com/SAM-BIM/SAM_Tas.git` (fetch and push); no HoareLea remote present |
| Baseline | `0589f0da7e2633478c26defdb237ceb0821ebdf9` (worktree `SAM_Tas-wt-base`, detached) |
| Tested head | `c1bf097f0e3ece0fb89941b7e8ac97f290c784b1` (worktree `SAM_Tas-wt-pr`, detached) = latest PR head |
| Dependencies | SAM `6167d474`, SAM_gbXML `43b5d01`, SAM_Systems `f0838ca`, SAM_Mollier `7f61916`, SAM_SolarCalculator `277a3a4`, all `sow/2026-Q4`, prebuilt, identical for both sides |

## 2. Gates
| # | Gate | Result | Evidence |
|---|---|---|---|
| 1 | Environment is local Windows with TAS, Rhino 8, .NET, MSBuild | **PASS** | section 1 |
| 2 | PR head is `c1bf097f`; hosted Windows `build` and `spdx` green | **PASS** | `gh pr view 101`: build SUCCESS (07:12:57Z), spdx SUCCESS; PR is a draft |
| 3 | `AddNotes(null)` correction and regression test present | **PASS** | commit `98db0994`; test `ANullStepNoteList_ThrowsWhatNotesAddRangeAlwaysThrew_BeforeAnythingIsAdded` passed; probe confirms ArgumentNullException, ParamName `collection`, nothing modified |
| 4 | Release x64 build, baseline and head | **PASS** | `logs/*-build.log`, both exit 0 (solution platform x64 maps to AnyCPU projects) |
| 5 | TM59 + GenOpt + Diagnostics tests, no inference from compilation | **PASS** | below |
| 6 | Legacy diagnostics unchanged (text, order, success/failure, `Notes`) | **PASS** | 8 route scenarios + 3 refusal gates |
| 7 | Generated T3D / TBD identical (semantic), TSD / TPD results identical | **PASS** | sections 4-5 |
| 8 | Structured diagnostics correct on real runs | **PASS** | section 6 |
| 9 | Public API only added, never changed | **PASS** | 0 removed / 26 added across 10 assemblies |
| 10 | Grasshopper saved definitions load, structure unchanged | **PASS (load/structure only)** | section 7 |
| 11 | Grasshopper runtime messages / solved outputs | **NOT TESTED** | definitions deliberately not solved (RunWorkflow writes TAS files) |

### Tests (Release, Windows, `dotnet test`)
| Suite | Baseline | PR head | Delta |
|---|---|---|---|
| `SAM.Analytical.Tas.TM59.Tests` | 1166 / 1166 passed | **1207 / 1207 passed** | +41 (`DiagnosticsAdapterTests`) |
| `SAM.Analytical.Tas.GenOpt.Tests` | 426 / 426 passed | **432 / 432 passed** | +6 |
Failed 0, skipped 0, not run 0 on both sides. The Linux-record environmental failures (registry `SetProjectDirectory`, `TasGenExecute.exe`, file-sharing tests) do not occur on Windows, including the three `ARefusedRun_...` gate tests. Files: `baseline|pr-head/*.trx`.

## 3. Real-TAS A/B method
Identical inputs to both sides; separate process per TAS operation; TAS processes swept before and after each; short output paths; TAS GUI closed. Drivers: the repository's own `direct-t3d-validation.exe` (`workflow`, `dump`, `t3d`, `deep`, `inputs`, `tsd`), built from each worktree; one reader binary (PR build) used to dump both sides. **Control**: baseline run twice (`baseline-control`) to measure the noise floor.
Inputs (`inputs/`, SHA-256 in `logs/input-hashes.txt`): `ModelA-Tas.sam` (2 spaces, 14 apertures), synthetic `loadsensitive` (3 zones, naturally ventilated, 1 opening availability schedule), `ModelA-PT.sam` (ModelA with `PanelSourceParameter.PhysicalThickness` stamped on 10 panels), weather `CIBSE Weather 2021.twd` / `London_TRY`, Systems Training TPD fixture (`inputs/tpd-fixture`).
Isolation proof: the two harness folders differ only in the 10 SAM_Tas assemblies (`SAM.Analytical.Tas`, `SAM.Core.Tas`, `.UKBR`, `SAM.Geometry.Tas`, `SAM.Weather.Tas` and pdbs); all 57 other files are byte-identical (`logs/harness-bin-hashes-*.txt`).

## 4. Generated files and the normalisation applied
Every raw `.tbd/.t3d/.tsd/.xml/.sam/.json` differs byte-for-byte between ANY two runs, including baseline vs its own control (TAS mints zone / surface GUIDs and timestamps every run). Byte comparison is therefore meaningless for these files, and the only normalisation used is: **GUIDs masked** in text dumps (and for the PartO definition, wire-source GUIDs reduced to counts). No numeric value, name, count, area, volume, width, construction or schedule is normalised.
Semantic comparison used instead (all passed, each also equal to baseline-vs-control):
| Scenario | Route | Spaces/zones | Volume m3 | Notes baseline == head | `.t3d` dump | `.tbd` dump | TBD property compare (`deep`) | TBD inputs (`inputs`) | TSD series |
|---|---|---|---|---|---|---|---|---|---|
| S1 ModelA | gbXML, simulated | 2 | 400.0 | byte-identical (9) | identical | identical | 0 differing | 0 unexpected (1614 props; 48 expected = TAS-minted GUIDs, same 48 in control) | 0 of 64 series differ, bit-identical 8760 h |
| S2 ModelA | direct T3D, simulated | 2 | 400.0 | identical (10) | identical | identical | 0 | 0 unexpected (1621) | 0 of 64 |
| S3 LoadSens | gbXML, simulated | 3 | 276.0 | identical (5) | identical | identical | 0 | 0 unexpected (2136) | 0 of 96 |
| S4 LoadSens | direct, simulated | 3 | 276.0 | identical (6) | identical | identical | 0 | 0 unexpected (2136) | 0 of 96 |
| S5 ModelA | direct, UseWidths ON (UseBEWidths) | 2 | 347.78 | identical (11) | identical | identical | 0 | 0 unexpected | 0 of 64 |
| S6 ModelA | gbXML, no simulation | 2 | - | identical (9) | identical | identical | 0 | 0 unexpected | n/a |
| S7 ModelA-PT | direct, simulated | 2 | 400.0 | identical (10) | identical | identical | 0 | 0 unexpected | 0 of 64 |
| S8 ModelA-PT | direct, UseWidths ON, simulated | 2 | - | identical (11) | identical | identical | 0 | 0 unexpected | 0 of 64 |
Sensitivity check: the same compare tool reports many differences between S1 and S5 (different routes), so "0 differing" is not a vacuous result.
Results are non-trivial (not zero loads): e.g. S1 first zone heating peak 1512.95 W / 5544.05 kWh, cooling peak 3063.36 W / 304.46 kWh; S3 heating peak 1029.67 W, cooling 1729.84 W. Absolute and relative deltas are exactly 0 (bit-identical) for every series, so no tolerance was needed (comparison tolerance fixed in advance: 1e-9 of each series' own peak).
**T3D PhysicalThickness vs TBD ConstructionThickness, independently (S7/S8):** with PhysicalThickness stamped, the T3D element widths are 0.30 (wall, floor) and 0.35 m (roof); without it (S2) they are the layer totals 0.2077 / 0.4127 / 0.3335 m. The TBD construction layers are unchanged in both (e.g. roof layers 0.0125 + 0.05 + 0.1 + 0.001 + 0.17 = 0.3335 m, compared property by property in `inputs`). Baseline and head agree on all of them.
Aperture geometry/assignments, zone volumes, panel (building-element) counts, aperture types and opening schedules (S3/S4: 1 schedule requested) are all inside the dumps/compares above. Fixture limitation: ModelA has no opening availability schedules or controls (0 requested); only the LoadSens fixture exercises them.

## 5. TPD simulation (Systems Training fixture, `Modify.Simulate` in place, hours 0..8759)
TPD must be re-linked to its TSD/TBD after copying (done identically on both sides before the call). Baseline, head and baseline-control: return `true`, `CallReturned=True`, `NativeDiagnosticKind=KnownSuccess`, native answer "Done", 0 refusals, 3 notes; `SimulationEvidence` text identical after masking only timestamps, byte counts and the root path. Resulting TSD: 0 of 832 series differ (bit-identical). TPD annual `SystemEnergyCentreResults`: 19 results, identical to full double precision on all three (only the result DateTime/Guid ignored); e.g. heating 8370.7266 kWh, cooling 2951.3599 kWh, auxiliary 5668.1826 kWh. The TPD file itself differs in bytes (as in the control).
Two earlier attempts of this scenario failed for harness reasons and are archived (`tpd-attempt*`): hours 1..8760 (TAS: "End Hour is after end of TSD data" - the API is 0-based) and no relink ("Failed to load TBD File"). Both were genuine native TAS refusals, handled identically by baseline and head, and the head's adapter classified the first as Error `TAS-SIM-001` (see 6).

## 6. Diagnostics compatibility and structured APIs
Legacy (baseline vs head, same scenarios): `WorkflowCalculator.Notes` text and order byte-identical in S1-S8 and in the three refusal gates (gbXML+canonical, canonical missing, canonical == target; only the output-root path normalised there); results null/non-null and success states identical. Existing Warning/ISSUE lines are present on both (ModelA direct has 4 ISSUE notes, identical).
Structured (`diag-probe` on real routes S1, S2, S5 + gates; `probe-gates.log`, `probe-S*.log`): all checks PASS - `GetDiagnostics` does not change `Notes`; default detail == Detailed; exactly one diagnostic per note; `SourceIndex` == index in `Notes` (order preserved); `Message` == legacy text (only the ISSUE marker removed); Summary subset of Standard subset of Detailed; all Warnings and Errors (S1: 4, gates: 1 Error each) survive Summary, Standard and Detailed; ISSUE notes never Info and unmarked notes never Warning; a null result carries an Error and a successful result carries none; re-run clears; `AddNotes(null)` throws ArgumentNullException(`collection`) before modifying anything (identical to `List.AddRange(null)`).
Producer severity on real TAS evidence (TPD): refusal "End Hour is after end of TSD data" -> Error `TAS-SIM-001` kept at every level, Info notes dropped at Summary; successful-but-unreconciled in-place run -> Warning `TAS-SIM-004` kept at every level; `SimulationEvidence` unchanged by the adapter.
Unit evidence: 41 + 6 new tests pass on Windows (section 2). API surface (`apidiff/out`): 0 members removed or changed, 26 added: 3 `ToDiagnostics` adapters (`SimulationEvidence`, `Log`, `T3DImportReport`), `StepNotesDiagnostics`, `IsIssueNote`, `WorkflowCalculator.GetDiagnostics`, the `TasDiagnosticCodes` type with its 17 codes, and 2 GenOpt adapters.

## 7. Grasshopper (Rhino 8.35, real GUI, `gh/`)
Method: copies (never originals) opened inside Rhino 8 through the GH archive reader with the document disabled and never solved. The three changed SAM_Tas assemblies (`SAM.Analytical.Tas`, `SAM.Core.Tas`, `SAM.Analytical.Tas.GenOpt`, built at the install's own version 1.0.0.0, `APPDATA` redirected) were swapped into BOTH `%APPDATA%\SAM` and the Rhino package folder, Rhino closed completely between each of baseline / head / baseline-control, then the originals restored and hash-verified (0 mismatches, `gh/gh-driver.log`). The installed Tas GHAs (`SAM_Tas_Grasshopper`, unchanged by the PR) were not touched.
| Definition | Objects | Placeholders | Result |
|---|---|---|---|
| `SAM_daily\2026-10-13-House\test3-acceptance.gh` (Tas `WorkflowgbXML`, 12 inputs incl. `T3DRoute_`) | 43 | 0 | baseline == head: every object's instance and component GUID, name, nickname, parameter names, access, optional flags, wires and obsolete flags identical (only the 5 `GH_Group` instance GUIDs are re-minted by GH on every load, also between baseline and control) |
| `SAM_daily\2026-07-15 PartO\2Falts-Model4.ghx` (4 Tas components: `WorkflowgbXML`, `CreateTBDByTM59`, `TSDQueryTM59Results`, `LogPartODiagnostics`) | 46 | 0 | baseline == head == untouched install after reducing wire-source GUIDs to counts (the document contains one third-party object, Woods Bagot "False Start Toggle", not installed on this machine; it is dropped on load on every side, which re-mints source GUIDs) |
No Tas component is a placeholder or newly obsolete; the only obsolete flags (`WorkflowgbXML` in the PartO file, `CreatePanelsBySolids`, `CreateAperturesBySolids`, `OCCT.CreateAdjacencyCluster` in the House file) are identical on baseline, head and the untouched install, i.e. pre-existing and unrelated to this PR. NOT TESTED: solving either definition, runtime balloons, `RunWorkflow` outputs; the guarantee for those rests on the unchanged GHA + IL/API evidence + the identical `Notes` above.

## 8. Differences found and likely cause
None in behaviour. Run-to-run differences exist only in TAS-minted GUIDs/timestamps (identical magnitude in baseline vs control), GH group GUIDs, and result-object timestamps.

## 9. Side effects on this machine (disclosure)
1. Building the baseline worktree re-copied 13 resource files into `%APPDATA%\SAM\resources\Analytical\Tas` (mtime 09:07-09:09). Content verified identical to the repository sources; no DLL in `%APPDATA%\SAM` was changed by any build (hash `61CC0DCE...` for `SAM.Analytical.Tas.dll` before and after).
2. The Rhino test replaced three DLLs in `%APPDATA%\SAM` and the Rhino package folder three times and restored them; final hashes equal the originals.
3. The final version-1.0.0.0 rebuild overwrote `build\` in both worktrees; the 2026.4.9001 builds used for the A/B are identified by `logs/harness-bin-hashes-*.txt`. Worktrees `SAM_Tas-wt-base` and `SAM_Tas-wt-pr` were left in place (not deleted, per AGENTS.md). The main checkout is untouched (clean, `sow/2026-Q4` at `0589f0da`).
4. Windows Application log shows APPCRASH entries (tas3d/TAS3D shutdown crashes, TPD.exe, and my own harness exceptions during failed attempts); the shutdown crashes are the documented workstation behaviour and all results were complete.
5. An initial Rhino swap using the 2026.4.9001 builds, and the first attempts with `GH_DocumentIO.Open`, hit modal "Unrecognized Objects"/"IO messages" dialogs; both were harness artefacts (version mismatch / third-party object), with the untouched install showing the same dialog for the PartO file under `GH_DocumentIO`. Switched to the archive reader and version-matched builds.

## 10. Reproduce
Scripts and sources under `C:\TasOut\diagnostics-phase4b`: `build.ps1`, `runtests.ps1`, `runroutes.ps1`, `runpt.ps1`, `rundumps.ps1`, `rundeep.ps1`, `runtpd.ps1`, `runtpdcmp.ps1`, `probe\` (diag-probe), `probe-legacy\` (legacy-gates), `apidiff\`, `gh\` (`ghprobe.py`, `rungh.ps1`, `buildv1.ps1`). Per-side outputs: `baseline\`, `pr-head\`, `baseline-control\`. Machine-local prerequisites: licensed Tas with GUI closed; short paths; the SAM_Tas dependency repos prebuilt.

