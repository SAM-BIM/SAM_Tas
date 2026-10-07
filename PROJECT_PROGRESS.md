# Project Progress - SAM_Tas (2026-Q4)

## Branch

`sow/2026-Q4` - bootstrapped 2026-10-06 from `master` `28ac11a7`. Frozen Q3 record: `sow/2026-Q3` @ `7d4ff52f` (not modified).

## Last updated

2026-10-07 (Java-free GenOpt PR1-T closeout, SAM_Tas#85).

## Current status

Q4 branch cut from `master` `28ac11a7`, which is the exact commit pinned in SAM_Deploy's frozen Q3 baseline (`v20261006.1`). Bootstrap added only internal docs (this file, `AGENTS.md`). First Q4 product work: the opt-in Direct SAM -> T3D route, merged as #84 (see the Direct T3D section below). The gbXML route remains the default. Second Q4 stream started: the Java-free GenOpt replacement (PR1-T evidence merged as #85; see the GenOpt section below).

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
