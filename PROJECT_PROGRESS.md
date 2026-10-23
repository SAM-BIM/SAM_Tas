# Project Progress - SAM_Tas (2026-Q4)

## Q4 S10 Tas results round-trip validation closeout - 2026-10-23

- **PR**: SAM-BIM/SAM_Tas#107 -> `sow/2026-Q4`, **merged 2026-10-23T20:33:53Z**.
  - Owner instruction: "I approve marking SAM-BIM/SAM_Tas#107 Ready and merging exactly 662977a6ace1fa0ca27e189c66080739ad575daa, provided the head is unchanged and checks remain green".
  - Merge commit `1d61ab052157fac868ef7beeae53054ff6c75c66`, made as a merge commit with `--match-head-commit`. Parents: base `c50e9169` and the approved head `662977a6`. The merge tree equals that head's tree (checked).
  - The PR was marked ready from draft immediately before the merge. No GitHub review or comment existed; the owner's instruction was the approval.
- **Status: S10 ACCEPTED WITH ONE DECLARED VALIDATION LIMITATION. This is not an unqualified full PASS.**
  - The limitation is `SAM|FixB|Heating|designDayPeak.hourOfDay` on `s08-fix-two`. It stays BLOCKED: comparison 94 PASS / 0 FAIL / 1 BLOCKED. The two-zone negative control keeps its measured BLOCKED / exit-10 outcome, and S10-C9 stays unresolved for heating.
  - Cause: nothing independent places the heating design day in time. The heating gains are constant zero and the TBD heating dry bulb is constant. FixB has 12 alternating exact maxima that differ by 0.00042724609375 W, which is consistent with numerical noise in a steady load (an inference, not proof).
  - Still checked and passing: FixB's heating design-day load, the native peak index and load, the annual load and hour, and every B1a row.
  - Owner accepted this one row (2026-10-23), as the independent attempt-2 review recommended. No code, tolerance, fixture or comparison status was changed. Unsupported heating design-day hours still BLOCK in general.
  - **The limitation must stay visible in the roadmap, Phase B and release evidence.** Phase B is not accepted by this decision. The roadmap row is unchanged; it changes only through a separate reviewed SAM PR. S11 not started.
- **Work**:
  - B1a annual peaks (F4-F6) in `benchmark/SAM.Analytical.Tas.Benchmark/Modify/ToBenchmark.cs`.
  - An independent TSD/TBD reference, comparison and negatives in `SAM.Analytical.Tas.DirectT3D.Validation/Results/` (calendar design-day mask from the TBD `yearDay`; C9 in-day peaks and the full-day-tie hour rule).
  - `Run-S10-Results.ps1`: receipt-verified provenance, stop on infrastructure failure, watchdog.
  - `Build-S10-Receipt.ps1`: the controlled stamped build and receipt.
  - The S10 offline tests, the record `Documentation/S10-TasResultsRoundTrip-PR.md`, and the evidence `Documentation/S10-Results-evidence/`: the attempt-2 pack, 248 files with `SHA256SUMS.txt` re-verified, plus a README stating the limitation and the N/A negative controls.
- **Review history**: revision 1 `77ac1cda` REVISE → `3387915b` accepted for measurement. Licensed attempt 1 (`C:\TasOut\s10\acc1`) stopped (B1a refused; R3/R4; design-day layout disproved) → correction `f29adaed`: calendar method ACCEPTED, runner REVISE (R3 timestamp binding, R5 `Get-FileHash` in the test child) → `9871f437` ACCEPTED. Licensed attempt 2 execution and evidence ACCEPTED. All reviews are local `.review-s10-*` folders.
- **Licensed results, attempt 2** (`C:\TasOut\s10\acc2`, Tas 2.0.0.1, 2026-10-23):
  - The receipt preflight was proven (receipt `c36bb206…`; SAM `2cfd6909`, runner built at `9871f437`).
  - 2 simulations, 12 steps, no infrastructure failure, no retry.
  - Box: PASS 59/59; negatives 52/52 as intended (6 N/A).
  - Two-zone: 94/0/1; negatives 51/52 as intended. The only miss is the control, blocked by the limited row.
  - The reads left the produced files unchanged. Fixtures, EPW and attempt 1 are preserved.
- **Validation**: local at `9871f437`: build exit 0; producer 40/40, core 258/258, read-back 249/249. Hosted at the approved head `662977a6`: Build (Windows) run `40010831380` and SPDX run `40010831293` SUCCESS, testing merge `0997f449`; all dependencies cloned at `sow/2026-Q4`; totals producer 40, core 258, read-back 249, TM59 1249, GenOpt 432. For the post-merge Build on `1d61ab05`, see the Actions tab. `sow/2026-Q4` has no branch protection or ruleset; none was bypassed.
- **Unresolved**:
  - The declared limitation above. Closing it needs independent heating timing evidence and separately authorised work; repeating the unchanged steady fixture would add no time signal.
  - The calendar design-day layout is measured on these two fixtures only.
  - Libraries from other repositories and third parties are pinned by hash in the build receipt, not source-bound.
  - The hosted R5 module-path cause is not proven (the fix is confirmed by CI).
  - `Pack-S10-Evidence.ps1` still uses `Get-FileHash`.
  - Follow-ups: the SAM_Validation `METRICS.md` Tas column, the `designDaySource` label, and the OpenStudio zero rule at S11.
  - Machine-local, to copy before switching machines: `C:\TasOut\s10\acc1`, `C:\TasOut\s10\acc2`, `C:\TasOut\s08c\weather\budapest.epw`, the `SAM-BIM/.s10-*` and `.review-s10-*` folders, and `S10_IMPLEMENTATION_PLAN.md`.
- **Cleanup**: remote `feature/s10-tas-results-roundtrip` was deleted with a lease on `662977a6` and confirmed gone. Before deleting, I confirmed that:
  - its remote head was that SHA;
  - it was an ancestor of `origin/sow/2026-Q4`, with 0 unmerged commits;
  - no open PR used it as head or base;
  - it was unprotected.

  The local branch was deleted with `git branch -d` (fully merged, no worktree used it), and the checkout is on `sow/2026-Q4`. No dependency repository has a same-named branch.
- **Next step**: a separate reviewed SAM PR records S10 in the roadmap as "accepted with one declared validation limitation", carrying the limitation. Then S11 only on the owner's start, as a new session.

## Q4 S08 PR-C equivalence fixtures and licensed acceptance closeout - 2026-10-21

- **PR**: SAM-BIM/SAM_Tas#106 -> `sow/2026-Q4`, **merged 2026-10-21T09:53:41Z** on the owner's instruction ("I approve merging SAM-BIM/SAM_Tas#106 at exactly 89345a84… conditional on successful final-head CI"), merge commit `61eb0d37af62dc2142248ddea30f0b043797e75f` (merge commit, `--match-head-commit`). Parents: base `989972ce` and approved head `89345a84a724870d69b0b7ef3220aee98245ecb8`; the merge tree equals that head's tree (checked). Marked ready from draft immediately before the merge. No GitHub review or comment existed; the owner's instruction was the approval. Independent reviews outside GitHub: revision 1 (`9badb9b4`) REVISE with one P1, fixed in revision 2; the owner approved the merge after revision 2.
- **Status**: S08 PR-C complete, the last S08 task. **On this evidence the roadmap S08 criterion is met**: both owner-signed-off fixtures PASS on the contract route under the owner-approved rules. The roadmap row is unchanged (it changes only through a separate reviewed SAM PR). S09 not started.
- **Owner decisions (2026-10-21 gate)**: fixtures signed off at `05edbdde` (synthetic provenance, centre-line/PhysicalThickness, both multizone spaces V1, single-pane glazing); Budapest TMYx weather (SAM `b721c5f4`, sha256 `472ffb4e…`); OD-G1, OD-G2, OD-G3 approved as drafted in S07 §6.1; zero `ticV`: the verified inactive-profile transformation T-VENT-INACTIVE for V0/V1 only, rows 6 and 8b only, when SAM selects no delivery profile, the native profile is present and all 8760 schedule values and delivered flows are read, finite and exactly 0; bounded licensed execution (3 exports, 5 reads, 6 comparisons; 7 mutations, 7 reads, 14 negative comparisons).
- **Work**: `ReadBack/AcceptanceFixtures.cs` (COM-free factory independent of the calibration models) and the committed fixtures `ReadBack/Fixtures/` (`s08-fix-box` V2 `45524172…`, `s08-fix-two` V1/V1 `20da9b58…`, provenance README); `readback-fixtures` mode; `Run-S08-ReadBack.ps1 -Phase acceptance / acceptance-negative` (fixture and EPW hashes checked first); core `GateRuleSet.Approved` and `QuantityInput.InactiveNativeTransformation` (Interim and ProvisionalProposed unchanged, so PR-B evidence replays as before); comparator T-VENT-INACTIVE; `--rule-set approved`; `Pack-S08C-Evidence.ps1`. Revision 2 (`5d7c1314`): an authored Ventilation selection is kept apart from its resolution; a selection that does not resolve (missing or wrong type) is S-INVALID on the ticV rows 6/8b (INPUT-INVALID, BLOCKED technical) and never inactive. Record: `Documentation/S08-PRC-FixturesAcceptance-PR.md`; evidence `Documentation/S08-PRC-Acceptance-evidence/` (with `offline-replay-r2/`).
- **Licensed results** (root `C:\TasOut\s08c\acc1`, harness `direct-t3d-validation.dll` `9cbdf5bb…` rebuilt from clean `62ce26cc`, frozen PR-B rules `766a88cd…`, Tas 2.0.0.1, Direct/UseWidths=false/Sizing=false/Simulate=false): `s08-fix-box` PASS 353/353, `s08-fix-two` PASS 623/623, coverage complete, no proposed case; provisional baseline BLOCKED only on the two-zone model's six ticV rows; reader/writer controls 0 differences; all 7 negatives FAIL under both rule sets, originals unchanged. Not gated, reported: T3D `frameWidth` 0.065 saved (DefaultFrameWidth) vs 0.075 writer rule. No simulation, sizing or recalibration.
- **Validation**: local Release at `5d7c1314`: core 258/258, read-back 141/141, `SAM_Tas.sln` restore and rebuild exit 0; offline replay of all 20 acc1 comparisons equal to the committed reports; the 6 revision-2 regressions failed 4/6 on `9badb9b4`. Hosted at the approved head `89345a84`: Build (Windows) run `39579013907` and SPDX run `39579013798` SUCCESS, testing merge `5788d1b5`; all eight dependencies cloned at `sow/2026-Q4`; totals producer 26, core 258, read-back 141, TM59 1249, GenOpt 432. (SPDX had failed on `14eb2e1a` for a missing header on the replay program, fixed in `89345a84`.) Post-merge Build (Windows) on `61eb0d37`: see the next entry or the Actions tab. `sow/2026-Q4` has no branch protection; none bypassed.
- **Unresolved**: one workstation and Tas build; 12 known `TAS3D.exe` shutdown faults (one per T3D read, hashes unchanged); not exercised: internal floors/ceilings, doors, rooflights, gas-filled glazing (gas-gap resistance, derived g), OpeningProperties, function profiles, multiple design days per role, leap-year weather, widths ON (separate task, roadmap decision 7); the production writer also skips the `ticV` update for an unresolved selection (recorded, not changed); other profile slots keep their existing handling of an unresolved selection. Machine-local: `C:\TasOut\s08c\acc1`, `C:\TasOut\s08c\weather\budapest.epw`, review folder `.review-s08-prc-20261021` (copy before switching machines).
- **Cleanup**: remote `feature/s08-prc-fixtures-acceptance` deleted with a lease on `89345a84` after confirming its remote head was that SHA, it was an ancestor of `origin/sow/2026-Q4` with 0 unmerged commits, no open PR used it as head or base, and it was unprotected; confirmed gone. The local branch was deleted with `git branch -d` (fully merged, no worktree used it); the checkout is on `sow/2026-Q4`. No dependency repository has a same-named branch. Other branches, worktrees and the frozen quarters were not touched.
- **Next step**: a separate reviewed SAM PR records S08 Done (SAM_Tas#104 `d08916ca`, #105 `fb9041c1`, #106 `61eb0d37`) in the roadmap. Then S09 (OpenStudio read-back) only on the owner's start, as a new session: Claude Opus 5.5, effort High, Plan then Build mode, because S09 needs its own fixture route, OpenStudio reader and owner gate.

## Q4 S08 PR-B independent Tas native-input read-back harness closeout - 2026-10-21

- **PR**: SAM-BIM/SAM_Tas#105 -> `sow/2026-Q4`, **merged 2026-10-21T08:01:43Z** on the owner's instruction ("merge at the reviewed head ad521528"), merge commit `fb9041c18299a23d30b4443501fc47a2ee8ba8d1` (merge commit, `--match-head-commit`). Parents: base `3d16b5c7` and head `ad521528437b4bee836f8d82cc6bb0099616571b`; the merge tree equals that head's tree (checked). Marked ready from draft immediately before the merge. No GitHub review, review comment or issue comment existed; the owner's instruction was the approval. Independent reviews were held outside GitHub for revisions 1 and 2 (both REVISE); an independent re-review of revision 3 was recommended but is not recorded.
- **Status**: PR-B of S08 complete. **S08 is not accepted:** PR-C (fixtures, provenance sign-off, acceptance) has not started; the calibration-model PASS results are self-consistency, not fixture results. The roadmap row is unchanged (it changes only through a reviewed SAM PR).
- **Work**: `readback-*` modes in `SAM.Analytical.Tas.DirectT3D.Validation` (export on the S07 contract route with frozen, hashed inputs; raw TBD/T3D reader in a fresh process; COM-free comparison through the PR-A core with coverage from the frozen model and every series element kept; calibration, P-ALIGN TSD, diff and negative modes), `Run-S08-ReadBack.ps1`/`Pack-S08-Evidence.ps1`, the committed `calibration-rules.json`, and the COM-free test project `SAM.Analytical.Tas.DirectT3D.ReadBack.Tests` (104 tests, one CI step). Record: `Documentation/S08-PRB-TasReadBack-PR.md`; evidence `Documentation/S08-PRB-ReadBack-evidence/`.
- **Revisions** (tested content SHAs): 1 `b9975571` (licensed harness built from `3d20d0fc`: calibration, the one approved P-ALIGN simulation and the one Sizing=true export, compare and negative checks; cal3 is the committed evidence); 2 `87f6ecf3` (unstated SAM schedules and IC-less spaces now required; native annual arrays outside the calibrated domain BLOCKED technical; an extra native design day FAIL; provenance fails closed with exit 70); 3 `bc867f8a` (a derived U whose native inclination or surface type is unusable is Unreadable/BLOCKED technical instead of an assumed Horizontal/External film; a snapshot without calendar or day evidence blocks the unread days' schedule hours instead of unmasking them under verified alignment).
- **Decisions and assumptions**: unusable derived-quantity inputs are Unreadable, never Absent (S-UNSPEC vs Absent passes in the core) or NaN (would mislabel OD-G3); out-of-domain native arrays are BLOCKED technical, never truncated or padded; a read day type the annual condition does not carry stays masked as absence; OD-G1..G3 and the zero-`ticV` interpretation stay provisional and floored at BLOCKED; T3D field checks are writer-only identity and never gated.
- **Files changed**: the read-back harness files under `SAM_Tas/SAM.Analytical.Tas.DirectT3D.Validation/ReadBack/`, `Program.cs` dispatch, the two scripts, the new test project, `.github/workflows/build.yml` (+1 test step), `.gitattributes` (2 `-text` lines), the record and the evidence folder. No production converter, `WorkflowSettings` default, public API, SAM_UI or other-repository change.
- **Validation**: local Release at `bc867f8a` (sequential): core 249/249, read-back 104/104, `SAM_Tas.sln` restore and rebuild 0 errors; the 30 revision-3 regression tests failed 26/30 on `cc414876`; offline replay of the 21 cal3 comparisons equal to revision 2 item by item (`offline-replay-r3/`, DLL sha256 `b9c3ea32…`; the `.exe` hash is the apphost stub). Hosted at `ad521528`: Build (Windows) run `39557873683` and SPDX run `39557873709` SUCCESS, testing merge `ac8cb108`; all eight dependencies cloned at `sow/2026-Q4`; totals producer 26, core 249, read-back 104, TM59 1249, GenOpt 432. Post-merge Build (Windows) run `39566201925` on `fb9041c1` was in progress at closeout. `sow/2026-Q4` has no branch protection; none bypassed.
- **Unresolved**: OD-G1 (is a zero-delivery `ticV` native absence for V0/V1 8b?) blocks every V0/V1 zone; T-FRAME `frameWidth` differs from the writer value and `isPercFrame` is unreadable; derived g, gas-gap resistance and OpeningProperties are not computed; only tbdExposed/tbdGround/tbdLink and complete calendars were ever read (the revision-3 states are exercised by mutated snapshots only); a native zone without a non-HDD condition is BLOCKED technical (unexercised); Tas radiant-fraction defaults are not in S07, so fixtures must author them. Machine-local: `C:\TasOut\s08b\cal1..cal3`, `C:\TasOut\s08b\replay-r3`, the review folders `.review-s08-prb-20261021` and `.review-s08-prb-r2-20261021` (copy before switching machines).
- **Cleanup**: remote `feature/s08-tas-readback-harness` deleted with a lease on `ad521528` after confirming its remote head was that SHA, it was an ancestor of `origin/sow/2026-Q4` with 0 unmerged commits, no open PR used it as head or base, and it was unprotected; confirmed gone. The local branch was deleted with `git branch -d` (fully merged, no worktree used it); the checkout is on `sow/2026-Q4`. No dependency repository has a same-named branch. Other branches, worktrees and the frozen quarters were not touched.
- **Next step**: the owner answers OD-G1 for V0/V1 delivery and signs the fixture provenance (D-FIX), then PR-C (fixtures and acceptance under the frozen rules) as a new session on the licensed Windows workstation, Claude Opus 5.5, effort High, Plan then Build mode, because fixture creation and the first fixture verdicts depend on those decisions. Optionally first, the independent re-review of revision 3's two fixes (GPT-6, High, new session, read-only review mode). A separate reviewed SAM PR updates the roadmap row (S08 PR-B merged `fb9041c1`).

## Q4 S08 PR-A input-equivalence evaluation core closeout - 2026-10-20

- **PR**: SAM-BIM/SAM_Tas#104 -> `sow/2026-Q4`, **merged 2026-10-20T19:02:34Z by the owner**, merge commit `d08916ca578cf4ab13ec64503cde43cf118ffbbf`. Parents: base `e84aeff6` and approved head `f91dc6a7f2a97b69d2e7287f679d9b6ff3cb4313`; the merge tree equals that head's tree (checked). The owner merged and deleted the remote branch themselves, after an independent review of three revisions; there were no review or comment threads on GitHub.
- **Status**: PR-A of S08 complete. **S08 is not complete:** PR-B (licensed native reader, calibration, alignment probe) and PR-C (fixtures and acceptance) are not started. The roadmap row still reads as before (it changes only through a reviewed SAM PR).
- **Work**: new COM-free library `benchmark/SAM.Analytical.InputEquivalence` (references `SAM.Core` and `SAM.Geometry` only) and `benchmark/SAM.Analytical.InputEquivalence.Tests`: reader-result DTOs that keep absent, unreadable, NaN, zero and finite distinct; the S07 section 5 abs-or-rel rule and tolerance catalogue (locked to the S07 table by tests); the section 6 outcome categories, state table and six-rank gate with a strict PASS allow-list; independent expected values that need no TAS (areas, shell volume, T-FRAME, gain and people densities, infiltration, D-F2 states, U-values with the approved films, design-day year day); schedule expansion and series evaluation; boundary-geometry identity matching; OD-G1 to OD-G3 only as an explicitly provisional, fail-closed rule set. One CI step runs the new tests.
- **Revisions** (validated content SHAs): 1 `da0f5529`; 2 `ab575448` (first review: zeroed native objects no longer pass through abs; no fixture PASS without an explicit complete coverage list `RequiredComparison`; informational numeric mismatches on non-gated quantities stay visible and do not gate; polygon identity compares boundaries, not vertex sets); 3 `6e0b55e4` (second review, P1: a mixed series chose its decisive element over informational mismatches too, so a gating FAIL/BLOCKED was hidden and the verdict depended on element order; the decisive element is now chosen among gating elements only and informational elements stay in the evidence). Record: `Documentation/S08-PRA-InputEquivalenceCore-PR.md`.
- **Decisions and assumptions** (all fail-closed, owner-visible in the record): identity defaults to not established and reaches at most writer-only without independent geometry (NOT RUN); a zeroed native for a non-zero SAM value is an EXPORT-DEFECT; only a numeric mismatch on `3b.ground`, `3c.g` or `9.ground` is informational; only schedule lengths 1, 24 and 8760 are expanded and 8784 is an unsupported leap year; a Ventilation profile without a requirement is indeterminate; both OD-G rule sets floor the undecided G cases at BLOCKED and nothing is labelled approved.
- **Files changed**: the two new benchmark projects, `.github/workflows/build.yml` (+1 test step) and the PR record. No existing source, solution, default or public API.
- **Validation**: local, no TAS or licence, on `6e0b55e4`: core suite 249/249 (Debug and Release), exhaustive PASS sweep 663,552 combinations with 7,344 passes all satisfying the allow-list invariants, mutation checks fail the intended tests for every fix, TM59 1249/1249 (rebuilt `--no-incremental`), benchmark producer 26/26. Hosted on the approved head `f91dc6a7`: `build` (run `39474363081`) and `spdx` (run `39474363055`) SUCCESS, merge state CLEAN, job-log totals 26 / 249 / 1249 / 432 (GenOpt); every dependency repo was cloned at `sow/2026-Q4`, so none was shadowed. Post-merge Build (Windows) run `39480031940` on `d08916ca` was in progress at closeout. `sow/2026-Q4` has no branch protection; none bypassed.
- **Unresolved**: owner decisions still open (S08 plan section 8): fixtures (D-FIX), OD-G1 to OD-G3, OD-W1 and OD-W2, the alignment probe and Sizing diff inside S08, the leap-year policy, and SAM_Tas write scope for PR-B. Native questions N-1 to N-13 (host-surface area gross or net, layer order versus `reversed`, T-FRAME, hour-ending slot and yearly alignment, calendar day types and others) stay for PR-B. Open design points: whether an absent or unreadable diagnostic on a non-gated quantity should also be informational (it gates today); PR-B must build the coverage list for every object from the frozen model, since the gate cannot detect an object left off the list; a series result carries the proposed/owner metadata of its decisive element only. Recorded in passing, not fixed: `Convert/ToSAM/Material.cs` passes `externalLightReflectance` in both light slots; `Convert/ToSAM/InternalCondition.cs:220` guards on the wrong object; `Convert/ToSAM/DesignDay.cs` uses a 2018 reference year; `SAMTBDDocument` creates a missing file and ignores the `open` result; S07 row 3a says "absorptance" while SAM and the writer use reflectance.
- **Cleanup**: remote `feature/s08-input-equivalence-core` was deleted by the owner (confirmed gone on the remote; `git fetch --prune` removed the tracking ref); it was fully merged, with head `f91dc6a7`, and no open PR used it. The local branch was deleted with `git branch -d` after confirming it was fully merged and no worktree used it. Other branches, worktrees and the frozen quarters were not touched.
- **Next step**: the owner answers the S08 decision gate, then PR-B (licensed native reader, calibration and alignment probe) as a new session on the licensed Windows workstation, Claude Opus 5.5, effort High, Build mode (Plan first if the decisions change), because reader independence, T-FRAME and layer-order calibration are open and a wrong rule yields plausible passes. A separate reviewed SAM PR updates the roadmap rows (S07 Done `68b60257`; S08 PR-A merged `d08916ca`).

## Q4 S01a TAS silent-failure detection closeout - 2026-10-19

- **PR**: SAM-BIM/SAM_Tas#103 -> `sow/2026-Q4`, **merged 2026-10-19**, merge commit `e483877cc517c3e0daf29884c9a401501bd77ce2` pinned to the verified head `1e9301fa0250f99f3bbe1f6427f02f06b0297621` (`--match-head-commit`; base before merge `a34a298d`). Validated content SHA `a3be6931`; `1e9301fa` is record-only. Roadmap task S01 (SAM `documentation/SIMULATION_ROADMAP_Q4_2026.md`), SAM_Tas half; owner approved SAM_Tas write access, two GPT-6 reviews, and the merge.
- **Status**: complete. PR-head build and spdx green on `1e9301fa`; post-merge Build (Windows) run `39298314016` on `e483877c` was in progress at closeout. `sow/2026-Q4` has no branch protection; none bypassed.
- **Work**: `TBDDocument.simulate` returns -1 (ran) / 0 (refused; TAS still writes a 22-byte stub TSD and `<tsd basename>_error_log.txt` beside the TSD, measured). `SimulationEvidence` gained `NativeReturnCode` and `RecordCallReturned(object)` (-1 ran, 0 refuses, anything else fails closed; refusals name code and log path) and keys a separate-output run's error log on the TSD. All `Modify.Simulate` overloads (legacy path, evidence, document-level via private `SimulateToTSD`) are decided by evidence; signatures preserved, so Grasshopper `TasSimulate.Successful` follows it. `WorkflowCalculator` stores `SimulationEvidence` per run, returns null with `TAS-SIM-001` Errors before reading a refused TSD, and refuses a model with spaces but no `SpaceSimulationResult` after `AddResults` (`Query.ZoneResultsRefusal`). `benchmark-tas` keeps those Errors as B1a warnings (`Producer.AddWorkflowFailure`); exit 5 / `RunState.Failure` unchanged.
- **Files changed**: `SAM.Core.Tas/Classes/SimulationEvidence.cs`, `SAM.Analytical.Tas/Modify/Simulate.cs`, `SAM.Analytical.Tas/Classes/WorkflowCalculator.cs`, `SAM.Analytical.Tas/Query/ZoneResultsRefusal.cs` (new), `benchmark/SAM.Analytical.Tas.Benchmark/Producer.cs`, `benchmark/SAM.Analytical.Tas.Benchmark.Cli/Program.cs`; tests `SimulationReturnCodeTests`, `ZoneResultsRefusalTests`, `SimulateDocumentLevelTests`, `ErrorLogLocationTests`, `WorkflowFailureTests` (new), `WorkflowCalculatorTests`; record `Documentation/S01a-Tas-SilentFailure-PR.md` and `Documentation/S01a-SilentFailure-evidence/`.
- **Validation**: local Windows TM59 1249/1249, GenOpt 432/432, Benchmark 26/26 (42 new simulated-evidence tests). Licensed TAS (machine-local `C:\TasOut\s01a`): unbalanced-IZAM refusal now False/null on legacy, evidence, document-level and workflow paths (baseline True, workflow hung); folder reuse both directions; result-less 3.5 KB TSD refused (helper level); differently named TBD/TSD reads TAS's TSD-side log. Successful box regression by the #237 method on the final build: B1a identical, persisted results exact 574/574 (workflow route 570/570), zero-noise control and tamper-checked comparator.
- **Unresolved**: a refusal through the `benchmark-tas` CLI end to end was not run (cannot be injected without changing the producer; covered by unit A9 + licensed workflow). A failed workflow can leave a previous run's `<tbd>.json` in a reused directory (retention unchanged): S01b must not treat that file as current-run success. `ZoneResultsRefusal` judges returned results, not attachment to SAM spaces.
- **Cleanup**: remote `feature/s01a-tas-silent-failure` deleted with a lease on `1e9301fa` and local branch deleted, after confirming it was fully merged into `sow/2026-Q4`, had no unmerged commits, and no open PR or worktree used it.
- **Next step**: S01b (SAM_UI propagation of `WorkflowCalculator.GetDiagnostics()` / `SimulationEvidence` to Energy Simulation and Run Simulation): new session, Claude Opus 5.5, effort High, Build mode, because it is a separate repository and task.

## Q4 engineering standards references closeout - 2026-10-16

- **PR**: SAM-BIM/SAM_Tas#102 -> `sow/2026-Q4`, **merged 2026-10-16**, merge commit `46783cf1fc36a4647c65532072d8c71d206d4111` pinned to approved head `0bdd92d21fe232f6613a147c823789902cd86a8d` (`--match-head-commit`). Part of a five-repository rollout (SAM_OCCT#79, SAM_OpenStudio#29, SAM_Tas#102, SAM_Tas_Grasshopper#15, SAM_Psychrometrics#13).
- **Status**: complete. PR-head build and spdx green on `0bdd92d2`; post-merge Build (Windows) run `38886664884` succeeded on `46783cf1`. `sow/2026-Q4` has no branch protection or required reviews; none bypassed. No review findings were posted.
- **Work**: added a concise "Engineering Standards" section to `AGENTS.md` (+10 lines) linking SAM's canonical `docs/DIAGNOSTICS_STANDARD.md` and `docs/GH_COMPONENT_STANDARDS.md` on `sow/2026-Q4` (SAM PR #205) by stable GitHub URL; standards not copied. Diagnostics changes follow the first; Grasshopper changes follow the second where applicable. Existing rules unchanged.
- **Files changed**: `AGENTS.md` only. No source, tests, GH definitions or build files.
- **Validation**: both links return HTTP 200 and both files exist on SAM `sow/2026-Q4` (GitHub API). Diff scope checked as AGENTS.md only; CRLF preserved. Hosted checks as above.
- **Attribution**: owner-approved wording "Generated by Michal Dengusiak and CodeClaude" used in the PR description.
- **Cleanup**: remote and local `docs/sam-tas-engineering-standards-refs` deleted after confirming it was fully merged into `sow/2026-Q4` and no open PR used it.
- **Unresolved**: none.
- **Next step**: none required.

## Q4 Unified Diagnostics phase 4B closeout - 2026-10-16

- **PR**: SAM-BIM/SAM_Tas#101 -> `sow/2026-Q4`, **merged 2026-10-16**, merge commit `ba9c95c8d620ca213cc538674fa9ed77e17eefda`, merge commit pinned to the verified head `d24821c6634636f76702bfddc28daf4617c788f4` (base before merge `0589f0da7e2633478c26defdb237ceb0821ebdf9`). Code head `c1bf097f0e3ece0fb89941b7e8ac97f290c784b1`; validated content SHA `98db09943d51653023688e269ca3e9f08f8b0623`; the last commit `d24821c6` is documentation only. Owner-approved acceptance and merge 2026-10-16.
- **Status**: complete. Post-merge Build (Windows) succeeded on `ba9c95c8` (run 38879593576): benchmark 23/23, TM59 1207/1207, GenOpt 432/432; dependency clones all `@ sow/2026-Q4`. PR-head checks were green on both `c1bf097f` (build 38864714847, spdx 38864714848) and `d24821c6` (build 38878275474, spdx 38878275406). `sow/2026-Q4` has no branch protection or required reviews; none bypassed. The PR was a draft, marked ready immediately before merge.
- **Work**: additive structured diagnostics for TAS on `SAM.Core.Diagnostic`/`Diagnostics`: `TasDiagnosticCodes` (17 codes), `Query.ToDiagnostics` for `SimulationEvidence`, `Log` and `T3DImportReport`, `Query.StepNotesDiagnostics`/`IsIssueNote`, `WorkflowCalculator.GetDiagnostics(detail)` (notes funnel `AddNote`/`AddNotes`, `AddNotes(null)` throws `ArgumentNullException("collection")` as `AddRange(null)` did), GenOpt `ToDiagnostic(s)`. No exporter, converter, simulation or numeric change; no Grasshopper contract change. Record: `Documentation/Diagnostics-Phase4B-TAS-PR.md`; evidence `Documentation/Diagnostics-Phase4B-evidence/` (including the Windows report).
- **Windows / licensed-TAS acceptance (PASSED 2026-10-16, owner's workstation, machine-local generated files not committed, kept in `C:\TasOut\diagnostics-phase4b`)**: Windows tests baseline TM59 1166/1166, GenOpt 426/426; head 1207/1207 and 432/432 (0 failed/skipped). Licensed TAS A/B vs baseline `0589f0da`, 8 scenarios plus baseline-vs-baseline control: `WorkflowCalculator.Notes` byte-identical, `.t3d`/`.tbd` dumps identical, TBD property compare 0 differences (only TAS-minted GUIDs, as in the control), all TSD series bit-identical over 8760 h; PhysicalThickness (T3D) and layer thicknesses (TBD) checked independently; TPD in place: same evidence, 832 TSD series and 19 annual results identical; public API 0 removed/changed, 26 added; structured-diagnostics probe passed on real runs; Grasshopper definitions (`test3-acceptance.gh`, `2Falts-Model4.ghx`) load identically on baseline and head in Rhino 8.35.
- **Remaining limitation**: the Grasshopper definitions were opened and compared but **not solved**, so component runtime messages and solved outputs are not covered by evidence (`RunWorkflow` writes TAS files). ModelA requests no opening schedules; only the 3-zone synthetic model exercises them. Workstation side effects of the acceptance (resource files re-copied into `%APPDATA%\SAM\resources`, temporary DLL swap in the Rhino install, restored and hash-verified) are in the Windows report.
- **Cleanup**: remote branch `feature/tas-diagnostics-phase4b-q4` deleted with an expected-SHA lease after confirming it equals the approved head, is fully merged into `sow/2026-Q4` and no open PR uses it. Local worktrees `SAM_Tas-wt-base` and `SAM_Tas-wt-pr` and the acceptance evidence are preserved (not deleted).
- **Next step**: none required for this stream. Separate owner-decided follow-ups: wire the adapters into the Tas Grasshopper components (optional hidden `diagnosticDetail_`, canvas balloons) and give T3D report and step-note entries structured identity at the producers. If solved-output Grasshopper evidence is wanted, run a saved definition with a real TAS model and compare its runtime messages and outputs on baseline and head.

## Q4 direct T3D export and BEWidths matching closeout - 2026-10-14

- **PR**: SAM-BIM/SAM_Tas#100 -> `sow/2026-Q4`, **merged 2026-10-14**, merge commit `4b253dc7c1304a8580e956dbe8a8c11acffc66d8`, head `e6888ebbd97e090bc803300991e5b0cba4383bc9`.
- **Status**: complete. Post-merge Build (Windows) succeeded on `4b253dc7` (GitHub). PR-head CI (build, spdx) was green on `e6888ebb` before merge; its CI used the same-named SAM branch whose head was the content merged as SAM#194.
- **Work**: direct T3D export of the house model with physical thickness preserved; apertures and panels matched with UseBEWidths ON (`Query.Match`, `PaneStampMatch`, `ApertureByElementName`, `ApertureTypesSummary`, `UpdateIds`, `UpdateBuildingElements`, `SetApertureTypes`, `T3DImportPlan`); OpeningProperties optional. Tests: `BuildingElementWidthsMatchTests`, `DirectT3DRouteTests`. Records: `Documentation/House-T3D-Export-Q4.md`, `DIRECT_T3D_ROUTE.md`.
- Manual Rhino/Grasshopper/TAS acceptance PASSED on 2026-10-14 (owner-run, machine-local, not committed): 11 correctly named TAS zones, 57 surfaces, 25/25 apertures matched, 50 aperture parts rebound, UseBEWidths ON volume 481.784 m³, no TAS validation errors, no unnecessary OpeningProperties warnings, PhysicalThickness preserved independently of TBD constructions.
- Merge order (sequential, merge commits, each pinned to its verified head SHA): SAM#194 `755bc514` → SAM_OCCT#76 `79d0332e` → SAM_OCCT#77 `3e688afc` → SAM_Tas#100 `4b253dc7`. `sow/2026-Q4` has no branch protection or required reviews; none bypassed. No review or codex comments existed on any of the four PRs. PRs were drafts and were marked ready immediately before merge.
- Remote task branches deleted with lease guards after verifying head SHA, full merge and no open PRs: SAM `feature/panels-by-solid-q4`, SAM_OCCT `fix/sew-under-split-q4` and `feature/air-panels-attic-q4`, SAM_Tas `feature/panels-by-solid-q4`. Local branches left in place (upstream gone).
- Unresolved (non-blocking): the informational wording "SAM panels are the physical inner surfaces" (SAM_Tas `WorkflowCalculator.cs` UseWidths note and `DIRECT_T3D_ROUTE.md`) is misleading for mid-surface panels; deliberately not changed during the merge. Small follow-up PR. The 19 pre-existing SAM_OCCT native integration failures remain documented and were not reinvestigated.
- **Next step**: none required for this stream. Optional wording follow-up PR (see above).

## Q4 agent instructions rollout closeout - 2026-10-12

- SAM-BIM/SAM_Tas#99 merged as `c6dfe516593d4590cd20b78b6d01975b66327dc9` (merge commit pinned to approved head `297bd7c155bda7b208902026c9c80e96711889a7`; base `sow/2026-Q4` `347e9efe521db6e985a92bf1f9bd2a404ce2850d`; validated content `9fa32480942563b26542f93d7176bde6252adf50`). Owner-authorized 2026-10-12 after an independent review (`AGENT_ROLLOUT_REVIEW_2026-10-12.md`, local to the owner's workspace, not committed).
- Docs only: AGENTS.md, CLAUDE.md, plus the rollout record `Documentation/Agent-Continuity-Rollout-Q4.md`. No product code, build, workflow, dependency pin or tool changed; this closeout is the only `PROJECT_PROGRESS.md` change.
- Validation: local documentation checks and commands are in `Documentation/Agent-Continuity-Rollout-Q4.md` (`git diff --check`, file scope, UTF-8/LF/no BOM, import). Hosted checks at the approved head: build success (run 38257834039); spdx success (run 38257834052). Dependency selection: dependency clones in the build log: SAM @ sow/2026-Q4, SAM_gbXML @ sow/2026-Q4, SAM_Mollier @ sow/2026-Q4, SAM_Psychrometrics @ sow/2026-Q4, SAM_SolarCalculator @ sow/2026-Q4, SAM_Systems @ sow/2026-Q4, SAM_Validation @ sow/2026-Q4, SAM_Windows @ sow/2026-Q4.
- Cleanup: remote docs/agent-instructions-rollout-q4 deleted with an expected-SHA lease on 297bd7c155bda7b208902026c9c80e96711889a7 after confirming it is contained in sow/2026-Q4 and no open PR uses it as head or base; the local branch and its worktree are preserved (worktree in use). Other branches, worktrees and the frozen quarters were not touched.
- Remaining gaps: no agent-client behaviour or token benchmark has been run.
- Exact next step: Continue from the shared `AGENTS.md`. Open item carried from the pilots: verify on each installed agent client, in a fresh Q4 session, that it follows the instructions without loading the full progress history, and record the result.

## Q4 Tas result units correction closeout - 2026-10-12

- SAM-BIM/SAM_Tas#98 merged as `92bfad33c9812d0be4190fc18dfc1bb18d63adaa`, approved head `72c4f3feb3e20702db4fd9232b5ba241b2c76303` (validated content SHA `36fb21d4`; later commits record-only). Owner-approved; merged last. The TSD converter now stores building consumption in kWh and peaks in kW and stamps `ResultUnitsVersion = 1`; the benchmark reads through SAM's helper so legacy and new results publish identical kWh/kW (golden unchanged). GenOpt untouched.
- Coordinated set (option A of SAM_Tas#92, owner-approved 2026-10-12): SAM#192 -> `be9c00cadb237cdc5a64351dce90dd7731be438c` (head `9268ccb7`), SAM_Tas_Grasshopper#13 -> `8054867fc317135e30792defe9796addfd12cef2` (head `d57b150c`), SAM_OpenStudio#26 -> `e0f9d17cc392a7217694d3ed8805c187c9d4a58b` (head `310bcffc`), SAM_Tas#98 -> `92bfad33c9812d0be4190fc18dfc1bb18d63adaa` (head `72c4f3fe`). Merge commits, each pinned to its approved full head SHA, in the approved order SAM -> Grasshopper/OpenStudio -> SAM_Tas the same day.
- Validation (recorded in the PR record): TM59 1132/1132, benchmark 23/23, GenOpt 426/426, mutation 5/5 killed; licensed converter confirmation 2/2 on copies of the #92 TSDs, bit-identical to the hourly sum/max / 1000. Merged-Q4 CI: Build `38251433825` success at `92bfad33`. PR checks build/spdx green on the exact head; no review findings.
- Dependency CI: each downstream PR CI was rerun after SAM#192 merged and its task branch was deleted; the logs show `Cloning SAM-BIM/SAM @ sow/2026-Q4` (Grasshopper run `38247874467` attempt 2, OpenStudio run `38247876785` attempt 2, SAM_Tas run `38247884982` attempt 3, all success on the unchanged approved heads).
- Compatibility: results carry `ResultUnitsVersion = 1` (consumption kWh, peaks kW). Unmarked results are read by exact recorded `Source` (`SAM.Analytical.Tas` = legacy Wh/W, divided once; `SAM.Analytical.OpenStudio` = kWh/kW); any other producer or marker gives unknown units, which DesignExplorer shows as empty cells. Persisted results are not migrated. Limitation: generic parameter views and external scripts reading the raw parameter see Wh/W on old unmarked Tas results and kWh/kW on new ones; scripts that applied their own /1000 to Tas values must stop doing so for newly corrected results. Details: SAM `documentation/TasResultUnits-PR.md`.
- Cleanup: remote task branch `fix/tas-result-units-kwh` deleted with a lease on the approved head after confirming it was in `sow/2026-Q4` and no open PR used it; local branch deleted and the checkout returned to `sow/2026-Q4` (clean, equal to origin). No other branch touched.
- Remaining: none. Next: PR10 packaging and installed-build validation; not started.

## Branch

`sow/2026-Q4` - bootstrapped 2026-10-06 from `master` `28ac11a7`. Frozen Q3 record: `sow/2026-Q3` @ `7d4ff52f` (not modified).

## Last updated

2026-10-11 (native Optimisation PR9 "Apply best design" closeout, SAM_Tas#93; sizing-copies fix closeout, SAM_Tas#94); 2026-10-12 (PR7b-2 closeout, SAM_Tas#95/#96/#97).

## Current status

Q4 branch cut from `master` `28ac11a7`, which is the exact commit pinned in SAM_Deploy's frozen Q3 baseline (`v20261006.1`). Bootstrap added only internal docs (this file, `AGENTS.md`). First Q4 product work: the opt-in Direct SAM -> T3D route, merged as #84 (see the Direct T3D section below). The gbXML route remains the default. Second Q4 stream started: the Java-free GenOpt replacement (PR1-T evidence merged as #85; native Tas GenOpt route merged as #86; legacy Java route retired and result rules shared as #87; see the GenOpt sections below). Native Optimisation definition stream: the Tas adapter for the SAM.Core.Optimisation definition merged as #88 (see the PR4 section below). The licensed spike for the `tas-model` blocks (PR7a, evidence only) merged as #89 and the glazing-swap spike (PR7a-2, evidence only) as #90. SAM's "try every option" PR merged as SAM#190. The `tas-model` engine (PR7b-1: catalogue reader, script generator, glazing choice, run mapping) merged as #91; next: PR7b-2 (licensed acceptance matrix) or PR8 (SAM_UI journey), owner's choice (see the PR7b-1 section below). Reader-defect evidence merged as #92 (no construction defect; consumption/peak units are Wh/W under kWh/kW labels; owner chose option A, a coordinated source fix, not started). PR8 (the SAM_UI journey) merged as SAM_UI#219; PR9 ("Apply best design": the Tas file writers and their protection) merged as #93 with SAM_UI#220 (see the PR9 section below). The Sizing-copies fix (owner option (a)) merged as #94 with SAM_UI#222: default Energy Simulation folders (Sizing on) can now be optimised. Next: PR7b-2, the Tas units correction (option A) and SAM_Deploy PR10, in the owner's order (not started).

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
  - **Update:** D1–D4 were resolved and SAM PR2 merged as SAM#183 (`0989ad81`). PR3 merged as #86 (see below).

## Native Optimisation PR7b-2: licensed acceptance, D1 guard and D2 protections (2026-10-12)

- **Status:** SAM_Tas side complete, closed. Merged into `sow/2026-Q4`, each as a merge commit pinned to its approved head (`--match-head-commit`), in this order:
  - SAM-BIM/SAM_Tas#95 (`feature/optimisation-pr7b2-acceptance`, head `d95702bf`): merge `e59e21dafdcfde48a3e155a41528e22129be482c`. Acceptance record `NATIVE_OPTIMISATION_PR7B2.md` and the spike driver; no product code.
  - SAM-BIM/SAM_Tas#96 (`feature/optimisation-plant-simulate-status`, head `6a4aac27`): merge `f42405e34b16e94c9034cddc5e3a68ad95fea688`. Q4 build green.
  - SAM-BIM/SAM_Tas#97 (`feature/optimisation-sizing-guard`, validated head `a793ac1a`, which is the approved `0c9284ab` plus a routine merge of Q4 containing #96): merge `ba9d97a30e3f28ae6b90cb29f296ec3c35a5033b`. Q4 build green.
- **Owner decisions (approved 2026-10-12):**
  - **D1 (guarded V1 limitation):** zone setpoint and glazing optimisation are refused when the main TBD has frozen or unverified sizing states (enforced at discovery, validation OPT617, the generated script and Apply). Sizing off is not an equivalent workaround. Proper support for frozen-capacity models (option b, per-candidate re-sizing) is deferred to V2.
  - **D2:** a plant simulation is accepted only on a measured success status ("Done"; failures, silence and unknown answers fail the evaluation), and a NaN or infinite objective fails the evaluation. The non-finite-objective case is unit-tested only; no licensed coverage is claimed for it.
  - **Deferred acceptance (kept explicit, no support claimed beyond evidence):** multiple plant rooms, internal glazing, an 8+ glazing library and a working SAM-created TPD (the owner's It3B TPD cannot size its plant on this workstation).
- **Integrated validation (the actual combined D1+D2 code):** #97's branch was merged with Q4 containing #96 (clean, no conflicts; the diff against the approved head is only #95/#96 content), `SAM_Tas.sln` rebuilt in Release (GenOpt DLL `581DA53E...`), then `SAM.Analytical.Tas.GenOpt.Tests` **426/426**; the test bin was verified to load that DLL hash. PR CI `build` and `spdx` green on `a793ac1a`; Q4 `build` green after each of `e59e21da`, `f42405e3` and `ba9d97a3`. The earlier trial merge (426/426) was historical evidence only.
- **Files changed (product):** `Create/TasScript.cs` (status allowlist, objective check, `RefuseFixedCapacities`), the catalogue/validation/Apply guard layers, golden snapshots, tests; records `NATIVE_OPTIMISATION_PR7B2.md`, `NATIVE_OPTIMISATION_PLANT_SIMULATE_STATUS.md`, `NATIVE_OPTIMISATION_SIZING_GUARD.md`.
- **Unresolved issues, risks:**
  - V1 limitations above: folders from Energy Simulation with Sizing on (heated models) offer no zone setpoint or glazing optimisation; a deliberately fixed `tbdNoSizing` TBD is refused too.
  - The building stage's `tbdDocument.simulate` return value is unchecked (a run that does not happen leaves no TSD, so the evaluation fails anyway). A NaN in a recorded non-objective output is shown, never optimised.
  - The SAM_UI companion SAM_UI#224 cannot be validated against Q4 by CI while this PR's remote branch `feature/optimisation-sizing-guard` exists: the workflow clones a dependency branch of the same name first. Its tree equals Q4's (`e5f14d7f`).
- **Next step:** the SAM_UI#224 CI rerun against merged Q4 and merge; then the separate coordinated Tas units correction, then SAM_Deploy PR10 (not started).

## Native Optimisation: Energy Simulation's sizing copies (2026-10-11)

- **Status:** complete, closed. SAM-BIM/SAM_Tas#94 (`feature/optimisation-sizing-derivatives`) merged into `sow/2026-Q4` as merge commit `481a77f6b2fdb7513b3e40ef034d7b5c05db276f` (parents: `94acd49a` + reviewed PR head `c57d3a276a91a0586a84efb798ceb20460b82a04`); merge commit pinned to the approved head, after explicit owner approval. PR CI (`build`, `spdx`) green on the head; no review findings; post-merge `sow/2026-Q4` build of `481a77f6` green. Remote feature branch deleted (lease-guarded; no unmerged commits, no open PR on it); local branch kept. Companion SAM-BIM/SAM_UI#222 merged after it as `57e14df9`, its CI rerun against this merged branch (`Cloning SAM-BIM/SAM_Tas @ sow/2026-Q4`). Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_SIZING_COPIES.md`.
- **Work completed:** Energy Simulation with Sizing (the default) leaves `<name>_Uncapped.tbd` and `<name>_HDDCDD.tbd`, intermediate sizing snapshots, beside the project's `<name>.tbd`, which holds the final sized model. One shared split, `Query.TasProjectFiles`, now decides the project's files for the inventory (`SingleFile`), the run snapshot (`NativeGenOptWorkspace.Create`), the run's hashes (`TasFileHashes`) and Apply's final check (`LiveHashes`): a snapshot whose primary is present is never opened, copied, hashed or changed; a sizing-named TBD without its primary, or two project TBDs, is refused.
- **Owner decisions (approved 2026-10-11):** name-based snapshot recognition (primary must be present); exclusion for both optimisation engines; snapshot changes do not block Apply; after Apply the snapshots describe the previous design until Energy Simulation regenerates them.
- **Files changed:** new `Query/TasProjectFiles.cs`, `Classes/Native/TasProjectFiles.cs`, the record; changed `Query/TasModelInventory.cs`, `Query/TasFileHashes.cs`, `Classes/Native/NativeGenOptWorkspace.cs`, `Classes/TasModel/TasModelDesignApplier.cs`; tests `SizingCopyTests.cs` (17).
- **Validation:** `SAM.Analytical.Tas.GenOpt.Tests` 352/352; mutation (recognition disabled) 11/17 caught; SAM_UI full suite 2795/2795 against this build; licensed journey with Sizing on (S1 setpoint bit-exact in SAM, ES 2 within 2.3e-13 kWh; S2 glazing 19 h = 19 h; snapshots absent from run copies and byte-identical after Apply) and real-application smoke PASS, no Tas process left, registry restored (local evidence `C:\TasOut\sizing`).
- **Unresolved issues, risks:** recognition is by name (an unrelated `X_Uncapped.tbd` beside `X.tbd` would be treated as a snapshot; it is listed in the UI line, so visible); after Apply the snapshots are stale until the next Energy Simulation.
- **Next step:** in the owner's order, PR7b-2 (licensed acceptance matrix), the Tas units correction (option A) and SAM_Deploy PR10 (shipping); none started.

## Native Optimisation PR9, "Apply best design" to the Tas files (2026-10-11)

- **Status:** complete, closed. SAM-BIM/SAM_Tas#93 (`feature/optimisation-apply-best-design`) merged into `sow/2026-Q4` as merge commit `818eee507317c1c74bc1c36b319e74b862c16665` (parents: `a4cd6d32` + reviewed PR head `85b4af4327ba8bec9b50ac6517d6d7afb27659c6`); merge method: merge commit pinned to the reviewed head, after explicit owner approval. Final-head CI (`build`, `spdx`) green; the post-merge `sow/2026-Q4` build of `818eee50` green. Remote feature branch deleted (lease-guarded, no unmerged commits); local branch kept. Companion SAM-BIM/SAM_UI#220 merged after it as `e94094ba`, its CI rerun against this merged branch. Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR9.md`; the PR9 record of record is SAM_UI `documentation/NativeOptimisation-PR9-ApplyBestDesign.md`.
- **Work completed:** `Query.TasModelDesignChanges` (best point → one change per variable, exact values, refusals for wrong counts, NaN, out-of-range values, invalid option numbers, unknown options or references); `TasModelDesignApplier` (run hashes checked, staging copies written and read back layer by layer, originals backed up under `<project>\SAM_ApplyBestDesign`, final check under a hold, capturing `File.Replace`, conflicting saves kept and reported, owned-only rollback, recovery note); `Query.TasFileHashes`; TBD inventory detail for glazing (pane layers, paired frames, per-zone surfaces); the licensed TBD/TPD writers.
- **Owner decisions (2026-10-11):** decisions 1–15 approved as implemented; manual recovery for V1, conflicting saves preserved and reported, a crash during replacement recovered from the backups, Undo restores SAM only, never the Tas files; a same-named different glazing material is refused (unique-name import later); strict pane/frame/placement checks kept, licensed coverage accepted for external glazing through the gbXML route only; the Sizing finding is option (a), a separate small PR.
- **Validation:** `SAM.Analytical.Tas.GenOpt.Tests` 335/335 on a fresh local Release rebuild of the final code (2026-10-11); mutations 14/14 (safety review) and 6/6 (atomic-save follow-up); licensed S1/S2/D1 and real-application smoke PASS (local evidence `C:\TasOut\pr9\fix`, `C:\TasOut\pr9\fix\race`).
- **Unresolved issues, risks:** not one transaction (manual recovery); internal glazing and the direct export route not run licensed (not claimed); default Energy Simulation folders (Sizing on) cannot yet be optimised: the Sizing copies fix is next; an empty `staging` folder can stay after a licensed Apply (Tas holds it).
- **Next step:** the Sizing-copies fix (owner option (a)): one shared identification of the project's TBD, sizing copies excluded from inventory, run snapshot, hashes and Apply, kept untouched; ambiguous folders refused; licensed journey with Sizing on. Then, in the owner's order, PR7b-2, the Tas units correction and SAM_Deploy PR10.

## Reader defects: construction gaps and consumption units, evidence (2026-10-09)

- **Status:** complete, closed (evidence only; no product file changed). SAM-BIM/SAM_Tas#92 (`fix/tas-reader-constructions-consumption`) merged into `sow/2026-Q4` as merge commit `cff2371dcce3d1018539d9660704ee23256e0236` (parents: `e07c7c20` + reviewed PR head `ac33fe7ddd17489ca977ebd5bb3c995749452891`); merge method: merge commit with `--match-head-commit`, after the owner's approval and choice of option A. PR CI (`build`, `spdx`) green on the head. Branch deleted on origin. Record: `SAM_Tas/SAM.Analytical.Tas/TAS_READER_DEFECTS_PR.md`.
- **Work completed:** (1) `Query.Constructions` has no defect: PR7a's "null gap" was a misreading (20 contiguous constructions; removal compacts); not changed, and the PR7a/PR7a-2 "null gaps" statements are superseded. (2) `ConsumptionHeating`/`ConsumptionCooling` and `PeakHeatingLoad`/`PeakCoolingLoad` hold Wh and W under kWh/kW labels (Systems Demo: 15 227 840.66 and 38 727.41; a year at that peak could use at most 339 MWh, so the value can only be Wh). Three consumers already divide by 1000 (SAM_Tas_Grasshopper `TasTSDAddBuildingResults`, SAM `Convert.ToDesignExplorer`, the SAM_Tas benchmark `ToBenchmark`), so a source-only fix would make them 1000x too small.
- **Owner decision:** option A, fix at the source in a coordinated set of PRs (SAM marker + DesignExplorer, SAM_Tas source + benchmark, SAM_Tas_Grasshopper), merged back to back the same day; new results carry a marker and a Tas result without it is read as Wh/W; no silent migration. Options B (relabel Wh/W) and C (document only) rejected.
- **Files changed (1):** the record above.
- **Validation:** licensed probes on copies (listing, removal, save and re-open; hourly sums vs Tas' own annual and peak sums, ratio 1.00000004); consumer list from a grep of all SAM-BIM repos; PR CI green.
- **Unresolved issues, risks:** until option A lands, the parameter labels are wrong for direct readers of Tas results; persisted Tas results stay in Wh/W after it (handled by the marker rule). The marker's exact form is still to be chosen in the first PR.
- **Next step:** the option A programme, from hand-over prompt `SAM-BIM/NEXT_SESSION_PROMPT_TAS_UNITS_FIX.md` (local file, not in git; it points only to committed files). It does not block PR8 (the native Optimisation measures divide by 1000 in their own script). Other open choice: PR7b-2 (licensed acceptance) or PR8, owner's order.

## Native Optimisation PR7b-1, the `tas-model` engine (2026-10-09)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#91 (`feature/optimisation-tas-model`) merged into `sow/2026-Q4` as merge commit
    `0e3a2aa0a8cbaecdc57d98f824aeaa0bf146c442`. Its parents are the Q4 base `4abad64b` and the reviewed PR head
    `581853a850375429fb57fe3dd7450bae35543a55`.
  - Merge method: merge commit with `--match-head-commit`, after the owner's approval on that head. CI on the head:
    `build` (it runs the GenOpt tests) and `spdx` green. No review comments. Remote and local branch deleted.
  - Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7B.md`. This is PR7b-1 of a split allowed by the
    hand-over; PR7b-2 (full licensed acceptance) is not started.
- **Work completed** (`SAM.Analytical.Tas.GenOpt`):
  - engine `tas-model` (`Query.TasModelCapabilities`):
    - methods: golden section (1 variable), Hooke-Jeeves (1..n), try every option (1); minimise; continuous and
      discrete;
    - targets: heating/cooling setpoint per TBD internal condition, `tbd.glazing-construction.choice` (display "Glazing
      system", at most 8 options), TPD controller setpoint;
    - measures: annual heating/cooling demand (kWh), overheating hours (threshold 28 °C, 20-40), plant energy (kWh),
      cost (GBP), CO2 (kgCO2e);
    - the g-value target is not offered; `tas-script` is unchanged;
  - `Query.TasModelInventory(folder)`: a licensed read-only COM reader; `Query.TasModelCatalogue`: pure, unit-tested;
  - `Query.TasGlazingSystems` (TCD calculator); `Query.TasGlazingOptions` (the PR7a-2 filter with the owner defaults);
  - `Create.TasScript`: the C# 7.0 TasGenExecute script from the PR7a/PR7a-2 blocks, with positional names `V1..`/`Y1..`
    and `Result`;
  - `Convert.ToSAM_TasModelKernel`: golden section and Hooke-Jeeves exactly as PR4; try every option `(V1, 1, 1, n, 1)`;
  - `TasModelRunner`:
    - checks the definition against the capabilities and the model's catalogue (OPT609) before any folder exists;
    - writes the glazing systems under unique names into the run's snapshot TBD only;
    - `Run()` and `Test()` (one evaluation);
  - `TasGenExecuteObjectiveEvaluator.Evaluate(simulation, coordinates)`: a single evaluation through the same code.
- **Owner decisions taken by the agent and accepted with the approval of #91:**
  1. heating-design-day conditions (`"<space> - HDD"`) are not offered;
  2. every plant room is simulated, and a TPD must use exactly one TSD;
  3. no suggested range for a controller setpoint;
  4. a pool system equal to the current glazing is not a separate option;
  5. the short id is added only when names clash within the choice's aperture type;
  6. more than 7 candidates are spread evenly over the g order;
  7. cost is offered only when the TPD's stored cost results are in "£";
  8. the evaluation's TPD is not saved;
  9. `SAM.Analytical.Tas.GenOpt` now references `SAM.Analytical.Tas` and the TBD/TSD/TPD interops (not embedded).
     PR10 must deploy them beside it, as today.
- **Files changed:** 38 (+4783/-5).
  - 13 classes under `Classes/TasModel/`, 5 `Query` files, `Create/TasScript.cs`, `Convert/ToSAM_TasModelKernel.cs`,
    the enum, the csproj, the evaluator entry, the record;
  - tests: 4 suites, 3 helpers, 4 snapshots `Golden/TasModel/*.csx`, the csproj (Roslyn 4.11.0), `TESTING.md`, and the
    stub's spec from `SAM_TAS_GENOPT_STUB_SPEC`.
- **Validation:**
  - `SAM.Analytical.Tas.GenOpt.Tests` 299/299 (257 + 42), including every snapshot compiled at C# 7.0 against the
    build-only interops and a TasGenComm stand-in;
  - 18/18 mutations caught; full `SAM_Tas.sln` Release 0 errors;
  - licensed smoke on copies (`C:\TasOut\pr7b`, local): every block reproduced PR7a/PR7a-2:
    - Demo heating 22 °C: 20 909.46 kWh; cooling 26 °C: 1 644.41 kWh / 22 h;
    - SAM model, 24-hour cooling 23→26 °C: 1 123.32 kWh;
    - controller 3: cost 7 363.17;
    - glazing choice: option 1 = baseline bit for bit; SIM_INT_GLZ with frames kept = 3 934.92 / 21 166.14 / 508 h;
    - a full golden-section `Run` converged to 4.968943799848584 in 11 evaluations (the PR3/PR5 point);
    - TasManager registry unchanged; no leftover Tas process.
- **Defects found by the smoke and fixed in #91:**
  - Tas COM servers need full paths: a relative folder gave `RPC_E_SERVERFAULT`;
  - the TPD `WrResultSet.Dispose()`/`Close()` fault after the values were read (the F2 family) is ignored.
- **Unresolved issues, risks:**
  - PR7b-2 not done:
    - every target's direction on both models (heating on a SAM model needs heating on);
    - measures compared with SAM_Tas' readers on the evaluation folders;
    - a 4-5 option choice on both models;
    - a mixed Hooke-Jeeves run;
    - repeats and the F2 rate;
    - a multi-plant-room TPD;
  - SAM fixture `systems-demo-bound-golden-section.json` still says `"Plant Room 1"` (tests prove it is OPT609 on the
    Demo; SAM follow-up);
  - `Modify.UpdateConstructions` still overwrites by name (the runner avoids it with unique names and a refusal);
  - the malformed `SIM_EXT_GLZ` Guid in SAM's library (its short id is not stable across sessions).
- **Next step:** owner's choice between PR7b-2 (licensed acceptance, SAM_Tas) and PR8 (SAM_UI journey on `tas-model`).
  Hand-over prompt: `NEXT_SESSION_PROMPTS_2026-10-09.md` in the SAM-BIM folder.

## Native Optimisation PR7a-2, glazing swap and g-value from available glazing systems (2026-10-08)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#90 (`spike/optimisation-glazing-swap`) merged into `sow/2026-Q4` as merge commit
    `02a06b6fdc6b697a3d74f9a38a936e6e2832420b`. Its parents are the Q4 base `13d86686` and the reviewed PR head
    `d928f97c01c64a0bf756e91d0ff3ff1f3cf5ee5c`.
  - Merge method: merge commit with `--match-head-commit`, after the owner's approval. CI on the head: `build` and
    `spdx` green. No review comments. Remote and local branch deleted.
  - Evidence only; no product code. Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7A2_GLAZING.md`.
    Spike files (not in `SAM_Tas.sln`): `SAM_Tas/spikes/optimisation-pr7a2/` (`swap.csx`, the probe `probe/`
    with `pool`/`write`/`constructions`, `README.md`). Raw evidence stays local in `C:\TasOut\pr7a2\`.
- **Work completed (licensed, Systems Demo copy plus a SAM-generated Part O model):**
  - SAM's glazing flow read end to end: `ThermalTransmittanceCalculator.CalculateGlazing` (TCD, STA, g and light
    rounded to 0.001, U = `GetUValue()[6]`; 171 systems in 2.94 s), the Glazing window's pools/filters/sort orders, the
    legacy assign-by-g flow, `SetGlazing`, and SAM_Tas' TBD writer `Modify.UpdateConstructions(building,
    apertureConstructions, materialLibrary)` (`"Windows: <name> -pane"/"-frame"`).
  - g parity: the calculator's g equals the TBD's `GetGlazingValues()[5]` bit for bit for 8 systems (g 0.118–0.792) on
    both TBDs; U and frame U too.
  - Swap in TasGenExecute (C# 7.0, TBD only): `buildingElement.AssignConstruction` on every element using the target
    glazing (and the frame element paired by name), read back, simulated. 23 evaluations: exit 0, no lock, no leftover
    process, registry unchanged; repeats bit-identical; the SAM model's own system written under a new name reproduced
    the baseline exactly. Solar gain rises strictly with g; cooling/overheating do not (U and light change with the
    system).
  - Snapping a continuous g to the closest system gives a step function (Hooke–Jeeves stuck on a plateau when the
    measured results are replayed); trying every option is exact in n simulations.
- **Owner decisions (2026-10-08, on approving #90):**
  1. **Method:** a choice among real systems, ordered by g, run by **"try every option"** (kind
     `tbd.glazing-construction.choice`, `"discrete"` 1..n). The continuous `tbd.glazing-construction.g-value` is not
     offered (no pane solve, no snap).
  2. **Option filter:** not changed by the owner, so the recommended defaults stand: same aperture type and
     transparency, g in the requested range, Ug ≤ current + 0.3 W/m²K, light ≥ current − 0.1, duplicates (g, U, light to
     0.001) once, at most 8 options, the current glazing as option 1.
  3. **Frames: keep the model's frames** (swap the pane construction only).
  4. **Order (owner asked the agent to choose):** the SAM work first. SAM's schema has only golden section and
     Hooke–Jeeves, `"discrete"` is reserved (OPT415) and the SAM.Math kernel has no exhaustive method, while SAM_Tas
     already models GenOpt's `Mesh` algorithm (every grid point), so a SAM PR adds the method (schema value,
     capabilities, diagnostics, kernel) before PR7b uses it. Chosen from code reading, not from a run.
- **Findings for PR7b:**
  - `UpdateConstructions` matches by name and overwrites: a library system named like the model's own glazing replaced
    the model's in-use construction. Write every candidate under a unique name (`"<name> <last 6 of Guid>"` worked).
  - Frames must be found through the elements (`"<base>-pane"` → `"<base>-frame"`); SAM-generated TBDs use `"X -frame"`
    on the element and leave `"Windows: X -frame"` unused; one pane element had no frame element.
  - Half of the 171-system TCD pool is single glazing; with the U filter only ~5–10 systems remain, so useful options
    come mainly from "My glazing systems".
- **Files changed:** the record and `SAM_Tas/spikes/optimisation-pr7a2/` (4 files). No product file.
- **Validation:** 23 TasGenExecute evaluations, 2 pool runs (179 systems), 4 TBD writes, 2 listings; PR CI green.
- **Unresolved issues, risks:**
  - SAM's default `SAM_ApertureConstructionLibrary.JSON` has a `SIM_EXT_GLZ` window with a malformed Guid
    (`4d00dd0-…`): a new random Guid on every load (separate SAM fix, suggested as its own task).
  - The `UpdateConstructions` same-name overwrite is a product behaviour worth its own SAM_Tas issue.
  - Legacy `Tas.Query.Score` is biased when only g is targeted (not used by the plan).
  - Heating effects are shown on the Demo only (the SAM model has heating off).
- **Next step:** the SAM PR "try every option" (method value in `sam.optimisation/1`, kernel exhaustive search over a
  `"discrete"` variable, capabilities and diagnostics), then PR7b (SAM_Tas catalogue reader, script generator, engine
  `tas-model`, glazing choice). Hand-over prompt: `NEXT_SESSION_PROMPT_TRY_EVERY_OPTION.md` in the SAM-BIM folder.

## Native Optimisation PR7a, licensed spike for the `tas-model` blocks (2026-10-08)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#89 (`spike/optimisation-tas-model-blocks`) merged into `sow/2026-Q4` as merge commit
    `d393ffbacd5073f63a4540e45390f9224beba3f8`. Its parents are the Q4 base `861d3e75` and the reviewed PR head
    `6fd69fbdd22310d7841fcfc976cc3a912d2ed290`.
  - Merge method: merge commit with `--match-head-commit`, after the owner's approval. CI on the head: `build` and
    `spdx` green. No review comments. Remote and local branch deleted.
  - Evidence only; no product code. Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7A_SPIKE.md`.
    Spike files (not in `SAM_Tas.sln`): `SAM_Tas/spikes/optimisation-pr7a/` (`chain.csx`, 21 compiler probes, the
    driver `Invoke-Pr7aEvaluation.ps1`, the reader probe `probe/`). Raw evidence stays local in `C:\TasOut\pr7a\`.
  - Programme: plan of record SAM_UI `documentation/NativeOptimisation-Plan-ModelBindings.md`; PR6 SAM#188 merged.
- **Work completed (licensed, Systems Demo copies plus one SAM-generated Part O model):**
  - One TasGenExecute script runs the whole chain: TBD edit → `simulate(1,365,0,1,0,0,tsd,1,0)` → TSD read → TPD
    `FixTSDPath` → controller setpoint → the Demo's `SimulateEx` → annual result sets. No lock or leftover process in any
    run; TasManager registry unchanged.
  - Durations (5 repeats, bit-identical): building only 11.5 s, plant only 23.9 s, both 32.2 s, Demo script 24.1 s.
  - Every V1 target moved the result in the expected direction. Controller parity: Setpoint 4.968943799848584 →
    cost 7360.04370117188, bit-identical to the PR3/PR5 acceptance (plant room `Plant Room`, `HeatPumpController`).
  - Every measure equals SAM_Tas' own readers to 15 digits; overheating = `Query.Overheating` (`OccupiedHours28/25`)
    for the worst occupied zone.
  - TasGenExecute compiles **C# 7.0 exactly**; LINQ needs `using System.Linq;`; TBD/TSD/TPD/TAS3D/TWD interops are
    referenced, **TCD is not**; `#r` works.
- **Findings for PR7b:**
  - F1: `TSDData.FixTSDPath` throws `RPC_E_SERVERFAULT` when the TBD named in the TSD's absolute
    `SimulationData.buildingPath` is missing (the Demo's TSD names a folder on the other workstation). Re-pointing
    `buildingPath` to the evaluation's TBD first fixes it (19/19).
  - F2: TPD.exe crashes at shutdown in ~17 % of plant evaluations (with or without `Save`); results already read, exit 0,
    +4–5 s.
  - F3: names are matched exactly (a Demo internal condition has a trailing space).
  - SAM-generated TBDs: one internal condition per space (description `"<SAM internal condition> - <space>"`) plus
    `"- HDD"`; thermostat setpoints are 24-hour profiles (setpoint = highest heating / lowest cooling hour).
  - TSD daily arrays are 1-based SAFEARRAYs; `Building.GetConstruction(i)` has null gaps.
- **Owner decisions (2026-10-08, on approving #89):**
  1. Setpoint targets are offered **per internal condition** (no grouping by SAM internal condition in PR7b).
  2. The TPD.exe shutdown crash (F2) is **not** reported to EDSL; blocks read results before release and ignore it.
  3. **Glazing g-value: no pane-property solve.** The g-value target uses the g that SAM already calculates for real
     glazing systems (`ThermalTransmittanceCalculator.CalculateGlazing`, the Tas TCD calculation behind the Glazing
     window), picks the **closest match among the glazing systems available to the project** (model, My glazing
     systems, default library), and swaps that construction in. The next stage proposes the exact method (snapping
     vs a g-ordered choice) after understanding how SAM's glazing flow works. PR7a's pane-solve is evidence only.
  4. The optional **glazing-swap test is approved** (next stage).
- **Files changed:** the record and `SAM_Tas/spikes/optimisation-pr7a/` (27 files). No product file.
- **Validation:** 77 TasGenExecute evaluations (21 compiler probes, 56 on Tas models); 9 cross-checked against SAM_Tas
  readers; registry export identical before/after every batch; PR CI green.
- **Unresolved issues, risks:**
  - F1 also affects today's `tas-script` route when a Systems Demo project is moved.
  - Out-of-scope SAM_Tas defects seen: `Query.Constructions` stops at the first null; `ConsumptionHeating/Cooling`
    hold Wh under a kWh label.
  - PR6 fixture `systems-demo-bound-golden-section.json` says `"Plant Room 1"`; the Demo's plant room is `"Plant Room"`.
  - Heating and plant blocks are proven on the Demo only (the SAM model had heating off and no TPD).
- **Next step:** PR7a-2, a licensed spike for the glazing swap and the g-value from available glazing systems
  (decisions 3 and 4), then PR7b (catalogue reader, script generator, engine `tas-model`), each after owner review.

## Native Optimisation PR4, the Optimisation Definition runs on Tas (2026-10-08)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#88 (`feature/optimisation-definition-tas-adapter`) merged into `sow/2026-Q4` as merge commit
    `a3837acbebb1bdfcf1d68fa519305e67f640fdb8`. Its parents are the Q4 base `7f2043ac` and the reviewed PR head
    `b5c93fc9ebfc1fa19a0f03a14e5ad04cf30e6624`; the merge tree `98a0e34` is identical to the head tree.
  - Merge method: merge commit with `--match-head-commit`.
  - CI: the PR's `build` (it runs the GenOpt tests) and `spdx` were green on the head. The post-merge
    `Build (Windows)` on `a3837acb` was green (run 37774014140). No review comments.
  - The remote branch was deleted. Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_DEFINITION_PR4.md`.
  - Programme: native Optimisation definition and UX. PR1 SAM_UI#214, PR2 SAM#186 and PR3 SAM#187 had already
    merged.
- **Work completed.** `SAM.Analytical.Tas.GenOpt` now references `SAM.Core.Optimisation` (HintPath) and adds:
  - `Query.TasOptimisationCapabilities()` and `Query.TasOptimisationEngine` (`"tas-script"`): golden section 1..1,
    Hooke–Jeeves 1..∞, minimise only, continuous only, no constraints.
  - `Convert.ToGenOptDocument(definition, directory, scriptText)` and an overload taking `TasExecutionSettings`.
    - It validates the definition against the capabilities. Any error throws `TasOptimisationDefinitionException`,
      a `GenOptCompatibilityException` that carries the diagnostics.
    - It maps onto the existing GenOpt objects exactly as the SAM_UI form did: objective first; golden-section
      start and step passed through (absent → minimum and 0); omitted settings keep the SAM_Tas defaults;
      `MaxEqualResults` at its default.
  - `TasExecutionSettings`: local, non-portable settings.
  - `Query.TasScriptCatalogue`: a literal scan of `Variables["…"]` and `ScriptOutput.SetValue("…"`, with comments
    and literals handled.
  - `Query.TasScriptDiagnostics`: OPT501/OPT502 warnings, never errors.
- **Decisions:**
  - `TasScriptCatalogue` returns the PR3 `OptimisationCatalogue`; it replaces the plan's `TasScriptNames`.
  - No `RunNative` wrapper. A `Modify` class here could clash with Grasshopper's `Modify.RunNative`.
  - L6 file and engine checks stay in SAM_UI until PR5.
  - The capabilities instance lives in a nested holder class. That way neither `Query` nor `Convert` loads
    `SAM.Core.Optimisation.dll` for the existing route before PR8 deploys it.
- **Files changed:** 13 (+1297/−1).
  - Six new source files, the csproj and the record.
  - Tests: the csproj, `TESTING.md`, `OptimisationDefinitionAdapterTests`, `TasScriptCatalogueTests` and
    `OptimisationDeploymentTests`.
  - No SAM.Math, SAM, SAM_UI or Grasshopper change, and no existing GenOpt source changed.
- **Validation.** Done on the second workstation, with all sibling repos on the Q4 heads.
  - `SAM.Analytical.Tas.GenOpt.Tests` 257/257 (216 + 41).
  - The definition document equals the PR5 form document for both Systems Demo fixtures, and the stub runs match
    evaluation for evaluation, `Variables.txt` bytes included.
  - **Licensed-run replay:** the recorded PR5 acceptance runs (`C:\TasOut\pr5-acc`) were replayed through this path.
    A 11/11 and B 16/16 `Variables.txt` were byte-identical. Harness: `C:\TasOut\pr4-replay`, not committed.
  - Mutations 6/6 caught.
  - A load context that refuses SAM.Core.Optimisation proves the existing route still runs without it.
- **Unresolved issues, risks:**
  - `SAM.Core.Optimisation.dll` (and `SAM.Units.dll`) must ship beside `SAM.Analytical.Tas.GenOpt.dll` before PR5
    reaches users (PR8, SAM_Deploy).
  - The script scan is literal.
  - No new live licensed run; the PR5a acceptance does that through SAM_UI.
- **Next step:** PR5a in SAM_UI. The form becomes an `OptimisationDefinition`, the examples become JSON, and the run
  goes through `ToGenOptDocument`, with a licensed A/B rerun. Then PR5b (formatting), each after owner review. The
  hand-over prompt is `NEXT_SESSION_PROMPT_PR5.md` in the SAM-BIM folder.

## Java-free GenOpt replacement — PR6, legacy Java route retired, shared result rules (2026-10-08)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#87 (`feature/native-optimisation-pr6-retire-legacy`) merged into `sow/2026-Q4` as merge commit
    `8dffaa3dabfedb45e43330a8a54fb1ba4a17c34e` (parents: Q4 base `da891dd2` + reviewed PR head
    `860c5a55a3a7a85d430e2845f1e73563ae0676fa`; merge tree `9fd7e76` identical to the head tree).
  - Merge method: merge commit with `--match-head-commit`. PR CI (`build`, `spdx`) green on the head; post-merge
    `Build (Windows)` on `8dffaa3d` green. Codex: no findings on this PR.
  - Remote and local branch removed. Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_GENOPT_PR6.md`.
  - Coordinated PR set (owner-approved order): this PR first, then SAM_Tas_Grasshopper#12 and SAM_UI#212 (consumers of
    `NativeGenOptOutcome`), then SAM#185 (comment only).
- **Work completed:**
  - Removed the legacy Java execution route: `GenOptDocument.Run()` (incl. its `Core.Tas.Modify.SetProjectDirectory`
    call), `ExecutableFile` (GenOpt.bat / `java -classpath genopt.jar`), `Query.TasGenOptJavaPath`, `Create.Command`
    (`cmd /c start`), the Java-only simulation-launch configuration (`SimulationConfigFile`, `SimulationStart` with a
    personal-path `cmd /c` default, `SimulationError`, `IO`, `NumberFormat`) and the matching document members; resources
    `files/resources/Analytical/Tas/GenOpt/GenOpt.bat` and `config.txt`.
  - GPSCoordinateSearch: removed from `Convert.NativeAlgorithmTypes`; refused with `NotSupportedException` and the D3
    reason (was `GenOptCompatibilityException`). Enum value and class kept for compatibility; nothing substituted.
  - New `NativeGenOptOutcome`: Completed / Withheld / Successful / BestEntry (`Best`) / Interval / `IsLower` /
    `RefusalMessage` — the rules the Grasshopper (PR4) and SAM_UI (PR5) reports duplicated. Wording stays in consumers.
  - "Java route" wording in messages/comments replaced by "GenOpt-format writer/text"; oracle history kept.
- **Kept deliberately:** `GenOptDocument` (definition + `RunNative`), algorithm/parameter/objective classes,
  `GenOptNumber`, `Convert.*`, workspace, evaluator, GenOpt-format input writers, `SetProjectDirectory` (used by
  `WorkflowCalculator`), the `SAM.Core.Tas` reference, the inert rest of the GenOpt resource folder.
- **Files changed:** 25 (+816/−425): `SAM.Analytical.Tas.GenOpt` (8 files deleted; `GenOptDocument`, `ToSAM_Optimiser`,
  4 native-class comment/message edits; new `Classes/Native/NativeGenOptOutcome.cs`; `NATIVE_GENOPT_PR6.md`;
  `NATIVE_GENOPT_ROUTE.md` pointer), tests (`LegacyRouteRetiredTests`, `NativeGenOptOutcomeTests` new; `MappingTests`,
  `NativeRunTests`, `TESTING.md`), 2 resource files deleted.
- **Validation:** `MSBuild SAM_Tas.sln /t:Rebuild` Release 0 errors (APPDATA/USERPROFILE redirected, real
  `NUGET_PACKAGES`). GenOpt.Tests 216/216 (173 + 43; 25 golden traces bit for bit through `RunNative`), TM59 1128/1128,
  Benchmark 16/16. IL/metadata test: only `TasGenExecuteObjectiveEvaluator` starts a process; no registry, java or cmd.
  Mutations on `NativeGenOptOutcome` (tie->last, no withholding, Nullspace not completed) all caught.
  Licensed acceptance not rerun: native execution unchanged; PR3 acceptance stands.
- **Unresolved issues, risks:**
  - Public API removal (`Run`, launch members/types, `TasGenOptJavaPath`, `Create.Command`); no consumer in any SAM-BIM
    repository; external scripts calling `Run()` would stop compiling.
  - SAM_Deploy follow-ups (separate task): ship one current `SAM.Math.dll` everywhere and `SAM.Analytical.Tas.GenOpt.dll`
    beside SAM_UI; delete old `GenOpt.bat`/`config.txt` from existing installations (installer does not remove files).
- **Next step:** merge SAM_Tas_Grasshopper#12 and SAM_UI#212, then SAM#185 (owner-approved); then the SAM_Deploy shipping
  task; then the AI-friendly objective model phase when requested.

## Java-free GenOpt replacement — PR3, native Tas GenOpt route (2026-10-07)

- **Status:** complete, closed.
  - SAM-BIM/SAM_Tas#86 (`feature/native-genopt-tas-evaluator`) merged into `sow/2026-Q4` as merge commit
    `63a5fec7f1681662f56f89e3c84c2a8824bff7bb` (PR head `229142e36b2b22bee8c4d7c69dedd117734a2085`, Q4 base `60d56621`).
  - Merge method: merge commit, head-commit protected.
  - Code-owner review: @ZiolkowskiJakub approved.
  - Remote and local branch removed.
  - Record: `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_GENOPT_ROUTE.md`.
- **Work completed:** `GenOptDocument.RunNative` maps the existing GenOpt objects onto the SAM.Math kernel (SAM#183)
  and evaluates with `TasGenExecute.exe` directly (Gate T protocol). It uses no Java, GenOpt, cmd.exe or registry.
  - `GenOptNumber`: the Java writer's text, read with GenOpt's StreamTokenizer arithmetic (D1). Exponent algorithm
    keywords are refused (D2).
  - `Convert.ToSAM_Optimiser` / `ToSAM_OptimisationProblem`:
    - GPSHookeJeeves and GoldenSection are supported. GoldenSection takes AbsDiffFunction and exactly one parameter.
    - GPSCoordinateSearch is refused (D3).
    - Every other algorithm type or class throws `NotSupportedException`.
    - GPS mesh values must be integers in GenOpt's domain. MaxEqualResults ≥ 2. WriteStepNumber = true is refused.
      UnitsOfExecution never makes the run parallel.
  - `NativeGenOptWorkspace`: one folder per run. The project snapshot is an allow-list: `Script.txt` plus
    TasGenComm's T3D/TBD/TPD/TSD/TWD.
  - `TasGenExecuteObjectiveEvaluator`:
    - one fresh folder per attempt (`NNNN`, `NNNN-retry`) and `Variables.txt`;
    - failure on a non-zero exit, a non-empty `Error.txt`, or a missing or unparseable objective (comma decimals are
      refused); the kernel owns the retry;
    - cooperative cancellation between evaluations, with no process kill.
  - The legacy `Run()` is unchanged.
- **Owner decisions:**
  - Legacy Java route: `Run()` writes `cmd /c "start …"`, which hangs under GenOpt 3.1.1 because GenOpt rewrites `/c`
    to `C:\c`. Accepted as a pre-existing defect. Not fixed; it is input for PR6.
  - GPSCoordinateSearch: the D3 Java-only oracle showed the SAM_Tas writer always emits `Seed`/`NumberOfInitialPoint`,
    which GenOpt rejects, so the Java route cannot run it. The adapter refuses it. A future native configuration layer
    may expose CoordinateSearch, but not as legacy compatibility.
  - Cancellation count: the PR2 kernel contract (a cancelled run counts the assigned but unlaunched simulation) is
    accepted. It may be revisited for presentation before SAM_UI (PR5).
  - Non-integer mesh values are refused, where Java truncates them (no silent repair).
- **PR3 Java-only oracle** (real genopt.jar 3.1.1, JRE 8, existing SAM_Tas writer, no Tas):
  - GPSCoordinateSearch is rejected for all Seed/NumberOfInitialPoint values.
  - `AbsDiffFunction = 1E-05` gives "Expected ';', got 'E-05'".
  - Mesh domain: divider > 1, s0 ≥ 0, t > 0, m > 0; fractions are silently truncated.
  - MaxEqualResults < 2 is rejected.
  - The writer's `E+`, `E-`, `-0` and 17-digit texts are read bit for bit as `GenOptNumber` predicts.
- **Files changed:** 24 (+2957/−1).
  - `SAM.Analytical.Tas.GenOpt`: `Classes/Native/*` (5), `Convert/*` (2), `GenOptDocument.cs` (additive), csproj
    (SAM.Math HintPath), `NATIVE_GENOPT_ROUTE.md`.
  - New `SAM.Analytical.Tas.GenOpt.Tests` (NUnit, HintPath pattern) and `SAM.Analytical.Tas.GenOpt.Tests.StubTasGenExecute`.
  - `SAM_Tas.sln` (2 projects appended), `.github/workflows/build.yml` (test step).
- **Validation:**
  - `SAM.Analytical.Tas.GenOpt.Tests`: 173/173. It includes 25 SAM golden GenOpt traces replayed bit for bit through
    `RunNative` plus the stub.
  - 5/5 adapter mutations caught.
  - `MSBuild SAM_Tas.sln /t:Rebuild` Release: 0 errors. TM59 1128/1128, Benchmark 16/16.
  - Licensed Systems Demo: Java GenOpt A, Java GenOpt B and native were **bit-identical**, with a Java-vs-Java noise
    floor of 0. That covers candidates, numbering, objectives (Result, Cost, CO2), termination and best point.
    - GoldenSection: 11 simulations, best Setpoint 4.968943799848584.
    - GPSHookeJeeves: 16 simulations, 41/10 listing rows, best Setpoint 5.
    - The Java controls invoked TasGenExecute directly because of the legacy `cmd /c` hang (accepted deviation).
  - PR CI `build` and `spdx` green on the head.
- **Unresolved issues, risks:**
  - The legacy `Run()` command hang (PR6).
  - Licensed acceptance covers one parameter (Systems Demo); multi-parameter runs are covered by the stub and golden
    tests.
  - TasGenExecute culture: native refuses comma-decimal output.
  - `SAM.Math`'s `Java8FloatText` keeps a stale .NET Framework sentence in a comment. It is deferred to a separate
    comment-only SAM PR; there is no .NET Framework requirement.
- **Next step:** PR3 is frozen. PR4 (Grasshopper), then PR5 (SAM_UI), then PR6 (Java retirement), each only when the
  owner requests it.

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
