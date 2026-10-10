<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR9 (SAM_Tas part): apply the best design to the Tas files

Branch `feature/optimisation-apply-best-design` → base `sow/2026-Q4` (cut from `a4cd6d32`). Record date: 2026-10-10.

Plan of record: SAM_UI `documentation/NativeOptimisation-Plan-ModelBindings.md` (PR9 row, D2). The window, the plan shown
to the engineer and the SAM model change are SAM_UI#(see the SAM_UI PR, same branch name), record
`documentation/NativeOptimisation-PR9-ApplyBestDesign.md`. **Merge this PR first**: the SAM_UI PR builds against it (CI
resolves the dependency by the same branch name) and needs the new API.

## Current status

PR open, **not merged**. Code, tests and licensed acceptance are complete; awaiting PR CI and **owner review** (decisions
in the SAM_UI record, which is the PR9 record of record; the SAM_Tas ones are repeated below).

## What was built (`SAM.Analytical.Tas.GenOpt`)

| API | What it does |
|---|---|
| `Query.TasModelDesignChanges(definition, point, glazingOptions)` → `TasModelDesignChange` | The best point of a "tas-model" run as one change per design variable, in the definition's order. Pure. Refuses (with the variable and why) a wrong number of values, NaN/∞, a value outside the variable's range, a glazing value that is not a whole option number 1..n, options that are not the definition's, an option with no system to write, a missing target or reference key. Values are kept exactly (no rounding). |
| `TasModelDesignApplier(projectFolder, inventory, sourceHashes).Apply(changes)` → `TasModelApplyResult` | Writes the changes into the project's TBD/TPD (licensed Tas, COM) and protects the originals (below). Writers, reader and file replacement are replaceable for tests. |
| `TasModelDesignApplier.WriteTbd` / `WriteTpd` | The writers: the generated script's blocks run in process on a staging copy. Setpoint: exact unique internal condition name, factor 1, the value written as the TBD's float (`(float)value`, as the script); a value profile's value, or in a 24-hour profile the hours at the setpoint (heating: highest, cooling: lowest), the setback kept. Glazing: the option's system written under its unique name (`TasModelRunner.WriteGlazingSystems`), then assigned to every element using the target glazing construction; the frames are kept. Controller: exact unique plant room and controller, the value as a double, read back, `Save`; a TPD shutdown fault after the save is ignored (PR7a F2: the file is read back anyway). |
| `TasModelDesignApplier.IsUnchanged` / `CurrentText` | An item that already holds the best value (glazing option 1, a setpoint equal as the TBD float, a controller equal as a double) writes nothing. |
| `TasModelRunner.SourceHashes` | SHA-256 of each Tas file of the snapshot the last `Run`/`Test` evaluated, taken before the glazing systems are written into it. |
| `Query.TasFileHashes(folder)`, `Query.FileHash(path)` | SHA-256 of the top-level Tas files (T3D, TBD, TPD, TSD, TWD). |
| `TasSetpointProfile.Hours` | The inventory now keeps a 24-hour profile's 24 values (floats), so Apply checks a profile hour by hour and SAM_UI can compare the SAM profile with the TBD. Additive. |

### How the originals are protected (`Apply`)

1. The project's Tas files must still have the run's hashes (`SourceHashes`); a changed, missing or new Tas file refuses
   everything ("The Tas files changed after the optimisation ran (… changed)").
2. Only the files that change are copied, into `<project>\SAM_ApplyBestDesign\<yyyyMMdd-HHmmss>-<id>\staging` and
   `…\original` (the backup); both copies are hash-checked.
3. The staging copies are written, then read back with `Query.TasModelInventory`: every changed item must read as the
   best value (a 24-hour profile hour by hour; glazing: exactly the target's elements on the option's pane, its g, U and
   light within 0.001 of the option's; a controller exactly), and every other internal condition, glazing assignment and
   controller exactly as before. Otherwise nothing in the project is changed and the attempt is kept for diagnosis.
4. The project files are replaced from staging (TBD, then TPD), each hash-checked. If one fails, every file already
   replaced is restored from the backup; if a restore fails, the exception (`ProjectChanged`) names the file and the backup.
5. The staging copies are deleted; the backup is kept.

## Decisions (owner review; also in the SAM_UI record)

1. **Same rules as the evaluation.** Apply runs the generated script's blocks, so the files hold exactly what was
   evaluated (the setpoint as the TBD float; the glazing pane on every element of the target, frames kept; a controller as
   a double). The heating design-day condition (`"<space> - HDD"`) is not changed, as in the evaluation.
2. **The run's file hashes decide "changed source model".** Any change to any top-level Tas file after the run refuses
   the application; nothing is repaired.
3. **Backups are kept** under the project's `SAM_ApplyBestDesign` folder and never deleted automatically.
4. **Glazing read-back tolerance 0.001** (the option filter's precision): a pool source may hold a system's values rounded
   (licensed acceptance: the model's `SIM_EXT_GLZ` listed g 0.4 / light 0.804 reads back 0.40016 / 0.80356).

## Files changed

- New: `Classes/TasModel/TasModelDesignChange.cs`, `TasModelDesignApplier.cs`, `TasModelApplyResult.cs`
  (`TasModelAppliedValue`, `TasModelApplyResult`, `TasModelApplyException`); `Query/TasModelDesignChanges.cs`,
  `Query/TasFileHashes.cs`; this record.
- Changed: `Classes/TasModel/TasModelRunner.cs` (`SourceHashes`), `Classes/TasModel/TasSetpointProfile.cs` (`Hours`),
  `Query/TasModelInventory.cs` (records the hours; `SingleFile`, `IgnoreServerFault`, `Floats` internal).
- Tests: `SAM.Analytical.Tas.GenOpt.Tests/TasModelApplyTests.cs` (new).

## Validation

- Step 0: SAM `288c9f57`, SAM_Tas `a4cd6d32`, SAM_UI `828ed76a`, SAM_Systems `5404926`, all at `origin/sow/2026-Q4`, clean.
- `SAM.Analytical.Tas.GenOpt.Tests`: **24 new**, full suite **323/323** (299 before). They cover: one change per variable
  at full precision (bit-equal), a glazing best option resolved to the run's option, ten refusals of a point that does
  not fit; the applier with stand-in writers: staging → read-back → backup → replace, refusal of changed/new/missing files
  and of a run with no hashes, a writer failure and a read-back failure leaving the project untouched, a failed second
  replacement restoring the first, a TPD value written as a double (a float read-back refused), unchanged items writing
  nothing, a 24-hour profile checked hour by hour (setback kept), a glazing choice moving exactly the target's elements
  (another element moving, or another system's values, refused; values within 0.001 accepted), and `SourceHashes` of a
  stub run.
- Release `MSBuild SAM_Tas.sln` (scratch profile, real `NUGET_PACKAGES`): **0 errors**; no warning in a changed file.
- **Mutations** (each applied alone, rebuilt, `TasModelApplyTests` + `TasModelRunnerTests` run, reverted; the unmutated build
  passed 30/30 after the last): **7/7 caught** — the run's hashes not checked; the read-back ignoring items outside the
  design; no restore after a failed replacement; glazing tolerance 0.01; a value outside its range accepted; an item at the
  best value written anyway; a 24-hour profile checked by its setpoint only. (The SAM_UI ones are in its record.)
- **Licensed acceptance** (this workstation, Tas installed and licensed; driven from SAM_UI's opt-in harness, evidence
  `C:\TasOut\pr9`, local): the licensed writers on staging copies, read back and replacing copies of the project files —
  S1 (SAM model, 24-hour cooling setpoint), S2 (SAM model, glazing choice from the open model), D1 (Systems Demo, TPD
  controller). Every value read back as the best point (TBD float, TPD double bit-exact), backups equal the originals, no
  Tas process left. Details and numbers: the SAM_UI record.
- `git diff --check` clean; SPDX headers on the new files.

## Unresolved issues and risks

- A COM call that hangs (TBD/TPD) cannot be interrupted; Apply runs one document at a time on its own STA thread.
- `Modify.UpdateConstructions` still overwrites by name; `WriteGlazingSystems` refuses a pane name the TBD already has
  (a second Apply of the same system to a re-optimised TBD would be refused; the run's hashes refuse it first anyway).
- Finding, not changed here: Energy Simulation with Sizing (the dialog's default) leaves `<name>_HDDCDD.tbd` and
  `<name>_Uncapped.tbd` beside the TBD, and `Query.TasModelInventory` refuses a folder with more than one TBD, so the
  Optimisation window cannot read a default Energy Simulation folder (owner decision in the SAM_UI record).

## Next step

1. PR CI (`build`, `spdx`) green on the head.
2. Owner review with the SAM_UI PR. Merge this PR first (merge commit, `--match-head-commit`), then the SAM_UI PR, then
   the `PROJECT_PROGRESS.md` closeouts on both `sow/2026-Q4` branches (`[skip ci]`).
