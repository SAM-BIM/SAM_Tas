<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation: Energy Simulation's sizing copies are not the project

Branch `feature/optimisation-sizing-derivatives` → base `sow/2026-Q4` (cut from `94acd49a`, after the PR9 merge
`818eee50` and its closeout). Record date: 2026-10-11. **This is the record of record**; the companion SAM_UI PR (same
branch name) has a short record, `documentation/NativeOptimisation-SizingCopies.md`.

**Merge order:** this PR first (it adds `Query.TasProjectFiles`, which the SAM_UI PR calls), then the SAM_UI PR, its CI
rerun against the merged `sow/2026-Q4` (the UI workflow clones a dependency by the PR's head-branch name first).

## Current status

Implemented and validated locally, including the licensed journey with Sizing on (see Validation). PRs open, **not
merged**; PR CI and owner review next.

## The finding (PR9) and the owner decision

Energy Simulation with **Sizing** (the dialog's default, `SimulateOptions.Sizing = true`) runs SAM_Tas
`Query.Sizing`, which leaves two copies beside the project's TBD:

- `<name>_Uncapped.tbd`: the TBD after sizing, before the air glass (copied, then the project TBD changes);
- `<name>_HDDCDD.tbd`: the TBD before the oversizing factors (copied, then the project TBD changes).

They are snapshots of intermediate sizing stages; the project's `<name>.tbd` holds the final sized model and is the
one simulated into `<name>.tsd` (owner, 2026-10-11: "if you are using sizing the real value is already written in the
main TBD"). Before this PR, the "tas-model" reader refused such a folder ("holds 3 .tbd files"), so a default Energy
Simulation folder could not be optimised.

**Owner decision (2026-10-11): option (a), a separate small PR.** Identify the project's TBD unambiguously; exclude the
sizing copies consistently from the inventory and the run snapshots; keep them untouched; refuse ambiguous folders.

## What changed

One split, `Query.TasProjectFiles(folder)` → `TasProjectFiles` (`Files`, `SizingCopies`, `UnmatchedSizingNames`), used
by every reader of the project folder:

| Consumer | Before | Now |
|---|---|---|
| `Query.TasModelInventory` (via `Query.SingleFile`) | every `*.tbd` counted; 3 → refused | sizing copies not counted, never opened; two project TBDs refused (the message names the copies "not counted"); a sizing-named TBD without its primary refused ("cannot be identified; the tas-model engine does not guess") |
| `NativeGenOptWorkspace.Create` (the run snapshot, both engines) | every top-level Tas file copied | sizing copies not copied |
| `Query.TasFileHashes` (the run's `SourceHashes`; Apply's first check) | every top-level Tas file | sizing copies left out, so the run's record and Apply compare the same set |
| `TasModelDesignApplier.LiveHashes` (Apply's final check under the hold) | every top-level Tas file | the same set as `TasFileHashes` |
| `TasModelDesignApplier.ProjectFile` (via `SingleFile`) | refused with copies present | finds the project's TBD/TPD |

**A sizing copy** is a top-level `.tbd` named `<base>_Uncapped` or `<base>_HDDCDD` (case-insensitive) **whose
`<base>.tbd` is in the folder too**. Only TBDs: Sizing copies only the TBD; a `<base>_HDDCDD.tsd` is a second TSD and
stays ambiguous (refused). A chain (`A_Uncapped_HDDCDD.tbd` beside `A_Uncapped.tbd` beside `A.tbd`) is recognised link by
link.

## Decisions (for owner review)

1. **Recognised by name and the primary's presence, not by content.** The copies are byte copies of the project TBD at
   earlier stages; nothing in them marks them. Requiring `<base>.tbd` beside them means a copy is never taken for the
   project, and a lone sizing-named TBD is refused, not guessed.
2. **Excluded everywhere, both engines.** The "Tas script" engine's snapshot leaves them out too (one rule; TasGenExecute
   would otherwise see three TBDs). A script that needed a sizing copy would have to be given it another way; none known.
3. **Ignored by the change check.** A sizing copy that changes after the run does not refuse Apply (it is not the model);
   the project's TBD changes whenever Energy Simulation runs again, so a re-simulated folder is still refused.
4. **Never touched.** Not opened, copied, hashed, backed up or replaced; after Apply they are snapshots of the model
   *before* the applied design until the next Energy Simulation with Sizing regenerates them (Apply already says the
   TSD is stale until the next simulation and offers Energy Simulation; the copies are not named there).
5. **No other folder rule changed.** A folder without sizing copies behaves exactly as before; no TBD/TSD name matching is
   added (pre-existing folders whose files have different names keep working).

## Files changed

- New: `Query/TasProjectFiles.cs` (`TasProjectFiles`, `SizingCopySuffixes`, `SizingCopyPrimary`),
  `Classes/Native/TasProjectFiles.cs`; this record.
- Changed: `Query/TasModelInventory.cs` (`SingleFile`, doc), `Query/TasFileHashes.cs`,
  `Classes/Native/NativeGenOptWorkspace.cs` (`Create`, doc), `Classes/TasModel/TasModelDesignApplier.cs` (`LiveHashes`).
- Tests: `SAM.Analytical.Tas.GenOpt.Tests/SizingCopyTests.cs` (17).

## Validation

- Release `MSBuild SAM_Tas.sln -restore`: 0 errors. `SAM.Analytical.Tas.GenOpt.Tests` **352/352** (335 existing + 17
  new `SizingCopyTests`: split, case-insensitivity, chain, non-TBD suffix, snapshot, hashes, inventory refusals, Apply on
  a sized folder: only the project TBD staged/backed up/replaced, copies byte- and time-identical; Apply refusing an
  added second TBD). Local evidence `C:\TasOut\sizing` (logs, TRX).
- Mutation: `SizingCopyPrimary` → null (the old behaviour; GenOpt rebuilt before the tests): **11/17 fail**; the other 6
  are the no-copy / refusal cases that hold either way. Restored byte for byte: 17/17.
- SAM_UI against this build: full `SAM.Analytical.UI.WPF.Tests` **2795/2795**.
- **Licensed journey with Sizing on** (details in the SAM_UI record; `C:\TasOut\sizing\acc`): a SAM model
  (`SAM_zoningAM.sam`) simulated with the Energy Simulation dialog's defaults (Sizing on → `_HDDCDD.tbd`,
  `_Uncapped.tbd`); read, checks, run, apply and Energy Simulation again with Sizing on, for a setpoint (S1: best
  25.8966744387541 bit-exact in SAM, ES 2 within 2.3e-13 kWh of the best simulation) and a glazing choice (S2: 19 h =
  19 h). Run snapshots held no sizing copy; Apply left both copies byte-identical; ES 2 regenerated them. Real
  application (UI Automation) on a sized folder: read, run, apply, Run Energy Simulation… PASS; no Tas process left;
  registry restored.

## Unresolved issues, risks

- Recognition is by name. A project the engineer named `X_Uncapped.tbd` beside an unrelated `X.tbd` would be taken as a
  sizing copy; it is listed in the UI line, so it is visible, not silent.
- After Apply, the sizing copies describe the design before Apply until Energy Simulation runs again (Undo restores SAM
  only, PR9 decision 13).

## Next step

Owner review of this PR and the SAM_UI companion; merge this first, rerun the UI PR's CI against the merged
`sow/2026-Q4`, merge it, then the `PROJECT_PROGRESS.md` closeouts. Then PR7b-2, the Tas units correction and SAM_Deploy
PR10 in the owner's order (not started).
