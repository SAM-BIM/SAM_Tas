<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR9 (SAM_Tas part): apply the best design to the Tas files

Branch `feature/optimisation-apply-best-design` → base `sow/2026-Q4` (cut from `a4cd6d32`). Record date: 2026-10-10.

Plan of record: SAM_UI `documentation/NativeOptimisation-Plan-ModelBindings.md` (PR9 row, D2). The window, the plan shown
to the engineer and the SAM model change are SAM-BIM/SAM_UI#220 (same branch name), record
`documentation/NativeOptimisation-PR9-ApplyBestDesign.md`. **Merge this PR first**: the SAM_UI PR builds against it (CI
resolves the dependency by the same branch name) and needs the new API.

## Current status

SAM-BIM/SAM_Tas#93 open, **not merged**. The 2026-10-10 architecture review (`PR9_ARCHITECTURE_ADVICE_2026-10-10.md`,
local to the SAM-BIM workspace) raised three source findings; all three were **reproduced with failing tests and fixed**
(heads `194a1545` / SAM_UI `60828d31`, CI green). The follow-up review (`PR9_FOLLOWUP_REVIEW_2026-10-10.md`, local) found
that the hold does not exclude **atomic saves**, so an external save could still be lost; that was **reproduced through
the applier and fixed** here (see "Follow-up review" below). Heads before it are superseded: **final-head CI** and
**owner review** of the decisions below and in the SAM_UI record (the PR9 record of record) are the blockers for merge.

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
| `TasMaterialLayer`, `Query.TasMaterialLayer(s)`, `Query.TasMaterialLayerDifferences` | (Safety review.) A construction layer as the thermal calculation sees it: material name, kind (opaque/transparent/gas), the layer's thickness and the properties of that kind, as the TBD's floats. `TasMaterialLayer(s)` describes a SAM layer exactly as SAM_Tas writes it (`Modify.UpdateConstruction` + the TBD `UpdateMaterial` of the material's kind; pure); the differences are in words ("layer 3 “Clear6” conductivity 0.9, not 1"), to float precision, two NaNs equal. A material's own width and description are not compared (the gbXML route stores the layer thickness as the frame material's width). |
| `TasGlazingConstructionInfo.PaneLayers` / `.Frames` / `.ZoneSurfaces` | (Safety review; optional constructor arguments, null = not read.) The inventory now reads, for each glazing construction, its layers, the layers of each different construction of the frame elements that pair with its pane elements (`Windows: <base>[_<hash>] -pane` ↔ `-frame`, by `Analytical.Tas.Query.TryDecomposeBuildingElementName`; in the gbXML route the frame element's construction is `<base> -frame`, not the unused `Windows: <base> -frame`), and the number of zone surfaces of its elements in each zone. SAM_UI compares the open model with them. |
| `TasModelDesignApplier.ReplacingNoteName`, `Query.FileHash(Stream)` | (Safety review.) The recovery note left in the work folder while the project files are replaced; a hash through an open handle. |
| `TasModelDesignApplier.FileReplacer` `(source, destination, displaced)` | (Follow-up review; was `(source, destination)`.) The replacement takes a third path that receives whatever was at the destination, in the same OS operation (`File.Replace` with a backup name). Test-only injection point; SAM_UI does not set it. |

### How the originals are protected (`Apply`)

1. The project's Tas files must still have the run's hashes (`SourceHashes`); a changed, missing or new Tas file refuses
   everything ("The Tas files changed after the optimisation ran (… changed)").
2. Only the files that change are copied, into `<project>\SAM_ApplyBestDesign\<yyyyMMdd-HHmmss>-<id>\staging` and
   `…\original` (the backup); both copies are hash-checked.
3. The staging copies are written, then read back with `Query.TasModelInventory`: every changed item must read as the
   best value (a 24-hour profile hour by hour; glazing: exactly the target's elements on the option's pane, its g, U and
   light each within one rounding step (0.001) of the option's, **and the pane layer by layer the system's**; a controller
   exactly), and every other internal condition, glazing assignment and controller exactly as before. Otherwise nothing
   in the project is changed and the attempt is kept for diagnosis.
4. **(Safety review.)** The project files to replace are held (`FileStream`, shared for **delete only**, so the replacement
   can rename them), and **every** top-level Tas file is hashed again under that hold and compared with the run's: a file
   saved or simulated by another program while the licensed writer and read-back ran refuses everything and is **kept**.
   What the hold does and does not do: it refuses a file another program has open for writing ("… is open in another
   program") and keeps out programs that *open* the file to read or write it; it does **not** keep out an **atomic save**
   (a new file renamed over the path, as editors and Tas may save) — delete sharing permits exactly that, and the held
   handle then still reads the old file. Step 5 is what catches those (follow-up review).
5. **(Follow-up review.)** The project files are replaced from staging (TBD, then TPD) with the operating system's
   replacement `File.Replace(staged, project file, displaced)`, which in the **same operation** moves whatever is at the
   path into `<work>\displaced\NN-<file>`. That displaced file must be the run's original:
   - it is → the file is replaced (the displaced original, a copy of the backup, is deleted after success);
   - it is not → another program saved after the final check: its save is **put back** with the same capturing
     replacement (what that moves away, Apply's written copy, is kept), nothing more is replaced, already-replaced files
     are rolled back, and Apply fails with "<file> was saved by another program while Apply was replacing it" — never
     success. If yet another save arrives during the put-back, both saves are kept and named.
   A note (`REPLACING-PROJECT-FILES.txt`: files, original and written hashes, backup, what to do) is in the work folder
   meanwhile. An OS replacement that fails after moving the project file away is undone (the file is moved back).
6. **Rollback restores only what Apply owns**: a replaced file is restored from the backup only while it still holds
   Apply's written copy, and the restore again moves away what it replaces and checks it: a save another program made
   after Apply replaced the file (before or during the rollback) is **left as the project file** (put back if the restore
   met it) and reported, with `ProjectChanged` and the note rewritten to say which files to check and "do not overwrite it
   with the original". The note's manual instructions likewise only restore a file that still has the best-design hash.
7. After success the note is removed and the staging folder deleted (licensed: an empty `staging` folder can stay when Tas
   still holds it — pre-existing, the deletion error is ignored); the backup is kept.

**The guarantee, exactly.** No version of a project file is lost: every version a replacement displaces is kept in the
work folder or is the project file again. Apply never reports success over another program's save made before its
replacement of that file, and never restores the backup over a save made after it. A program opening the file to write
between the final check and the replacement is refused; an atomic save in that window is detected, kept and reported (a
**conflict**: Apply stops, the other program's save is the project file, nothing of the best design is left, recovery
by the engineer as reported). Not guaranteed: the files are replaced one after another, **not as one transaction**; a
save another program makes to a file after Apply replaced it, while Apply goes on, is that program's and Apply reports
success for its own work; if the process or the computer stops during steps 5–6, the note stays with the backup, the
displaced files and the hashes, and the files are recovered by hand; nothing is recovered automatically (owner decision 13
in the SAM_UI record). Whether a file is "the run's" is decided by SHA-256 content: another save with identical bytes is
indistinguishable and harmless. This is about the Tas files only; Undo in SAM restores the model only.

## Decisions (owner review; also in the SAM_UI record)

1. **Same rules as the evaluation.** Apply runs the generated script's blocks, so the files hold exactly what was
   evaluated (the setpoint as the TBD float; the glazing pane on every element of the target, frames kept; a controller as
   a double). The heating design-day condition (`"<space> - HDD"`) is not changed, as in the evaluation.
2. **The run's file hashes decide "changed source model".** Any change to any top-level Tas file after the run refuses
   the application; nothing is repaired.
3. **Backups are kept** under the project's `SAM_ApplyBestDesign` folder and never deleted automatically.
4. **Glazing read-back tolerance: one rounding step per quantity** (g, U and light each 0.001, named separately). Source:
   the option's values are the pool's, which SAM_Tas' glazing calculation reads from TCD rounded to
   `Core.Tolerance.MacroDistance` (`Query.GlazingValues`, `Query.ThermalTransmittance`), while the TBD read-back is not
   rounded (licensed acceptance: the model's `SIM_EXT_GLZ` listed g 0.4 / light 0.804 reads back 0.40016 / 0.80356).
   Equal values are not the same glazing: the written pane is also read back **layer by layer** against the system's
   materials (safety review).
5. **(Safety review.) The final check is under a hold**: the files to replace are held (delete sharing only) and every
   Tas file is compared with the run's again; a change by another program refuses everything and is kept. The hold does
   not exclude atomic saves (follow-up review); decision 6 catches them.
6. **(Safety review, follow-up.) Capturing OS replacement; conflicts reported; owned-only rollback; no automatic
   recovery**: `File.Replace` with a displaced-file capture per file, the displaced file checked, another program's save
   put back and reported; rollback only of files still holding Apply's copy; a recovery note while replacing; not one
   transaction (the guarantee above).

## Files changed

- New: `Classes/TasModel/TasModelDesignChange.cs`, `TasModelDesignApplier.cs`, `TasModelApplyResult.cs`
  (`TasModelAppliedValue`, `TasModelApplyResult`, `TasModelApplyException`); `Query/TasModelDesignChanges.cs`,
  `Query/TasFileHashes.cs`; this record.
- Changed: `Classes/TasModel/TasModelRunner.cs` (`SourceHashes`), `Classes/TasModel/TasSetpointProfile.cs` (`Hours`),
  `Query/TasModelInventory.cs` (records the hours; `SingleFile`, `IgnoreServerFault`, `Floats` internal).
- Safety review: `TasModelDesignApplier.cs` (hold + final check, `File.Replace`, recovery note, restore only what is not
  original, pane layers in the read-back, per-quantity tolerances); new `Classes/TasModel/TasMaterialLayer.cs`,
  `Query/TasMaterialLayers.cs`; `TasGlazingConstructionInfo.cs` (`PaneLayers`, `Frames`, `ZoneSurfaces`);
  `Query/TasModelInventory.cs` (reads them); `Query/TasFileHashes.cs` (`FileHash(Stream)`);
  `SAM.Analytical.Tas.GenOpt.csproj` (references `SAM.Architectural`, the base of `ConstructionLayer`).
- Follow-up review: `TasModelDesignApplier.cs` only (capturing replacement, conflict handling, owned-only rollback, the
  `FileReplacer` signature, the note's wording).
- Tests: `SAM.Analytical.Tas.GenOpt.Tests/TasModelApplyTests.cs` (new), `TasMaterialLayerTests.cs` (new, safety review).

## Safety review (2026-10-10)

Review: `PR9_ARCHITECTURE_ADVICE_2026-10-10.md` (SAM-BIM workspace, local, not in Git). Findings 2 and 3 are fixed in
SAM_UI with this PR's new inventory detail and layer comparison; the reproduction/fix table for all three is in the SAM_UI
record. Finding 1 here:

- **Reproduced** on `aad85e39` (new tests failing): another program saving the project TBD while the licensed writer ran,
  or simulating its TSD during the read-back, was not detected — with the default `File.Copy` replacer Apply
  **succeeded and overwrote that save**; with a stand-in replacer the post-copy hash check "restored" the backup over it.
  A TBD held open by another program, and a writer between the check and the replacement, were not refused.
- **Fixed** then (`194a1545`): hold, final check under it, `File.Replace`, recovery note, restore only what is not
  original. That fix was **incomplete**: its record claimed no other program could write between the check and the
  replacement, which is false for atomic saves — see the follow-up review below, which replaces steps 5–6.

Validation on the final code: Release `MSBuild SAM_Tas.sln -restore` **0 errors**, no warning in a changed file;
`SAM.Analytical.Tas.GenOpt.Tests` **330/330** (323 + 7: four for finding 1, the pane read back layer by layer, two for the
layer description/comparison, whose TBD side is the licensed S2 TBD's values); **mutations 7/7 caught** (T8 no final
check, T9 a hold that lets others write, T10 the old copy-over replacer, T11 no layer read-back, T12 a property left out
of SAM's description, T13 NaNs unequal, T14 thickness not compared; script and log `C:\TasOut\pr9\fix\mutate-fix.ps1`,
`mutations-fix.log`; the SAM_UI ones in its record). **Licensed rerun on the final build** (`C:\TasOut\pr9\fix`, local):
S1, S2 and D1 all PASS. The new inventory reader read the real TBDs, and the genuine S2 model passed the new
pane/frame/placement checks. The written S2 pane read back layer by layer as the system's, and the D1 TPD was replaced
with `File.Replace` (bit-exact). Best points were identical to the first acceptance. The real-application smoke also
passed; details in the SAM_UI record.

## Follow-up review (2026-10-10): atomic saves

Review: `PR9_FOLLOWUP_REVIEW_2026-10-10.md` (SAM-BIM workspace, local). The hold opened with `FileShare.Delete` lets
another program atomically replace the live path; `LiveHashes` then reads the old held file, the check passes, and the
replacement overwrites the external save, which the backup does not contain either.

- **Reproduced** on `194a1545` through `TasModelDesignApplier`, using the `FileReplacer` injection to atomically replace
  the live TBD with external content (real `File.Replace` on NTFS, the hold open) just before calling the captured default
  replacer: **Apply reported success and the external save was gone** (the TBD held the best design). And with TBD + TPD,
  an external atomic save on the already-replaced TBD followed by a failing TPD replacement: **the rollback copied the
  pre-run backup over the external save**. (Both tests failed on `194a1545`; log `C:\TasOut\pr9\fix\race-red.log`.)
- **Fixed**: steps 5–6 above — capturing replacement (displaced file checked against the run's original), a conflicting
  save put back and reported, rollback only of files still holding Apply's copy (each restore capturing and checking what
  it moves away), a half-done OS replacement undone, the note's manual instructions never overwriting another save. The
  hold and the final check are kept, with their limit stated. `FileReplacer` takes the displaced path.
- **Tests** (5 new, 2 changed): an atomic save just before the replacement (kept as the project file, Apply fails with
  the conflict, the written copy kept in `displaced`, `ProjectChanged` false); a later save on a replaced file before a
  failing replacement (not restored over, reported, `ProjectChanged` true); an atomic save just before the rollback's
  restore (put back, note says do not overwrite it); two saves racing one replacement (both kept, the later named); an OS
  replacement failing after moving the file away (moved back). The writer-exclusion test now says what it proves (opening
  to write is refused); the failed-replacement test fails the OS replacement before anything moves, as the OS does.
- **Validation (final code)**: Release `MSBuild SAM_Tas.sln -restore` and `SAM_UI.sln` 0 errors, no warning in a changed
  file; `SAM.Analytical.Tas.GenOpt.Tests` **335/335** (330 + 5); SAM_UI `SAM.Analytical.UI.WPF.Tests` **2793/2793** (one
  unrelated Part O window test failed once in the first full run, passed 3/3 alone and in a full rerun); **mutations 6/6
  caught** (R1 displaced file not checked, R2 the default replacer keeps nothing, R3 a conflicting save not put back, R4
  rollback restores another program's save, R5 the restore does not check what it moved away, R6 a half-done replacement
  not undone; `C:\TasOut\pr9\fix\mutate-race.ps1`, `mutations-race.log`; unmutated 36/36 after). **Licensed rerun** on
  this build (`C:\TasOut\pr9\fix\race`): S1, S2, D1 PASS with identical best points and bit-exact read-backs, no displaced
  file or note left after success; the real-application smoke PASS. The conflict paths are proven with real NTFS
  replacements in the tests, not against a running Tas (an atomic save by Tas itself was not provoked).

## Validation (before the safety review; counts superseded above)

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

- Replacement is per file, not transactional; an interrupted replacement, or a conflict with another program's save, is
  resolved by the engineer with the backup, the displaced files and the note (nothing automatic). Undo in SAM restores
  the model only, never the Tas files (SAM_UI record).
- The hold does not exclude atomic saves; they are detected after the fact by the displaced-file check (a conflict, not
  a loss). A save after a file was replaced, while Apply replaces the next one, is accepted as the other program's.
- `File.Replace` needs the work folder on the project's volume (it is under the project) and a file system that supports
  it (NTFS; not verified on network shares). Where it is refused, Apply fails and changes nothing.
- The conflict paths were exercised with real NTFS replacements in tests, not by provoking Tas itself to save mid-Apply.
- The inventory now walks every zone surface of the TBD once (glazing placement): more COM calls on a large model; not
  measured on one.
- TBD building element names are assumed unique when zone surfaces are counted per element name.
- A COM call that hangs (TBD/TPD) cannot be interrupted; Apply runs one document at a time on its own STA thread.
- `Modify.UpdateConstructions` still overwrites by name; `WriteGlazingSystems` refuses a pane name the TBD already has
  (a second Apply of the same system to a re-optimised TBD would be refused; the run's hashes refuse it first anyway).
- Finding, not changed here: Energy Simulation with Sizing (the dialog's default) leaves `<name>_HDDCDD.tbd` and
  `<name>_Uncapped.tbd` beside the TBD, and `Query.TasModelInventory` refuses a folder with more than one TBD, so the
  Optimisation window cannot read a default Energy Simulation folder (owner decision in the SAM_UI record).

## Next step

1. Confirm PR CI (`build`, `spdx`) green on the **final** head; earlier green runs do not count.
2. Owner review with SAM-BIM/SAM_UI#220 (hand-over steps in its record). Merge this PR first (merge commit,
   `--match-head-commit <final head>`), confirm the `sow/2026-Q4` build, then validate and merge the SAM_UI PR against the
   merged SAM_Tas (its record says how), then the `PROJECT_PROGRESS.md` closeouts on both `sow/2026-Q4` branches
   (`[skip ci]`, with the merge SHAs).
