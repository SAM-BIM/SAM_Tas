# SAM-BIM runtime URLs (Q4) - SAM_Tas

Branch `fix/sam-bim-runtime-urls-q4` -> base `sow/2026-Q4`. Record date: 2026-10-06.

## Current status

PR open, **not merged**. Source-only, minimum change: the text SAM writes into the TBD building description links to
SAM-BIM/SAM instead of HoareLea/SAM. No `.gitmodules`, gitlink, workflow, `master`, `sow/2026-Q3` or icon-redesign change.

## Work completed

`SAM_Tas/SAM.Analytical.Tas/Modify/UpdateZones.cs`: `building.description` = `Delivered by SAM https://github.com/HoareLea/SAM [yyyy/MM/dd]`
-> `Delivered by SAM https://github.com/SAM-BIM/SAM [yyyy/MM/dd]` (same text the SAM Grasshopper model components now write).

## Decisions and assumptions

- SAM-BIM is the authoritative ecosystem; HoareLea is no longer the synchronised operational source.
- The string is only written, never parsed or asserted anywhere in this repository (searched all sources and tests).
- Left unchanged: the `//TODO ... https://github.com/HoareLea/...` code comment in `UpdateSurfaceShades.cs` (historical issue reference, not runtime) and `Kernel/AssemblyInfo.cs` provenance strings.

## Files changed

`UpdateZones.cs` plus this record. One product line changed.

## Validation

- `git diff --check` clean; diff reviewed (1 insertion, 1 deletion in product code).
- `msbuild SAM_Tas.sln -p:Configuration=Release` (.NET Framework MSBuild, `APPDATA`/`USERPROFILE` redirected): 0 errors;
  `SAM.Analytical.Tas.dll` contains `Delivered by SAM https://github.com/SAM-BIM/SAM [` and no `HoareLea/SAM [`.
- TAS-dependent tests were not run locally (they need the installed TAS COM environment); no test references this string. PR CI results are recorded in the PR description.

## Unresolved issues, risks

- None introduced by this change.

## Exact next step

Maintainer review and merge into `sow/2026-Q4` once CI is green (not merged automatically). After merge, the
`PROJECT_PROGRESS.md` closeout is a direct docs commit on `sow/2026-Q4` (never on this branch).
