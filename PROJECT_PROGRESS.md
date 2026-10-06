# Project Progress - SAM_Tas (2026-Q4)

## Branch

`sow/2026-Q4` - bootstrapped 2026-10-06 from `master` `28ac11a7`. Frozen Q3 record: `sow/2026-Q3` @ `7d4ff52f` (not modified).

## Last updated

2026-10-06 (Q4 operational cleanup).

## Current status

Q4 branch cut from `master` `28ac11a7`, which is the exact commit pinned in SAM_Deploy's frozen Q3 baseline (`v20261006.1`). Bootstrap added only internal docs (this file, `AGENTS.md`). No product source changed. No Q4 product work has started.

## Q4 priorities

Not yet set by the owner. Record them here at the first Q4 planning pass. Known carry-over work is listed below.

## Known carry-over work

- Branch `codex/part-o-cooling-control-room` - Q3 complete: all commits already in sow/2026-Q3.
- Branch `codex/part-o-pr2-diagnostics` - Q3 complete: all commits already in sow/2026-Q3.
- Branch `codex/pr5b-generic-table-roundtrip` - owner decision: 6 commits not in Q3 (last 2026-09-15).

## Repository-specific next steps

- Await Q4 planning. Open PRs for Q4 work against `sow/2026-Q4`.
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
