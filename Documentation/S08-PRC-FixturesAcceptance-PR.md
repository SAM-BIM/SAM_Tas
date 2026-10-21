# S08 PR-C: equivalence fixtures and licensed acceptance

Task S08 of the SAM simulation roadmap, PR-C (final) of the approved sequence: PR-A core SAM_Tas#104 (`d08916ca`), PR-B
read-back harness SAM_Tas#105 (`fb9041c1`). Contract: SAM `documentation/SIMULATION_INPUT_MAPPING_S07.md` (SAM#216).
Branch `feature/s08-prc-fixtures-acceptance`, base `sow/2026-Q4` (`989972ce`). **Draft, for independent review. Not merged.**

## Status

- Tested content SHA: `62ce26cc` (the harness of the licensed run was rebuilt from this clean commit). Later commits add
  the evidence packer, the offline replay tests and this record only; the final head and hosted checks are in the PR
  description.
- **Both fixtures PASS under the owner-approved rules**: `s08-fix-box` (V2) 353/353 and `s08-fix-two` (V1, V1) 623/623,
  coverage complete, no proposed case, no owner decision or technical blocker left. All 7 negative checks FAIL as required.
- On this evidence the roadmap S08 criterion is met ("for the box and the multizone model, every S07 quantity read back
  from the TBD equals SAM within the S07 tolerance or is listed as a mapping difference"), subject to independent review
  and merge. The roadmap row changes only through a separate reviewed SAM PR.

## Owner decisions (2026-10-21, recorded for portability)

Gate answer, quoted in substance:

1. **Fixtures signed off** at `05edbdde`: their documented synthetic provenance, the centre-line/PhysicalThickness
   convention, both multizone spaces V1, single-pane glazing. **Weather:** the SAM#212 TMYx Budapest EPW.
2. **OD-G1, OD-G2, OD-G3 approved as drafted in S07 §6.1.**
3. **Zero `ticV`:** "a narrowly scoped, verified inactive-profile transformation for V0/V1 only": SAM selects no
   ventilation-delivery profile; the native profile stays recorded as PRESENT; all 8760 expanded native `ticV` schedule
   values and delivered flows are read, finite and exactly zero; calendar and array-domain checks still hold; missing,
   unreadable, NaN or non-zero values get no exemption; V2 and other quantities unchanged; rows 6 and 8b only; no global
   reinterpretation of zero as absence; no tolerance relaxed.
4. SAM_Tas comparator changes and regression tests authorised to implement 2-3.
5. Licensed execution authorised and bounded: 3 exports, 5 fresh-process reads, 6 fixture comparisons; 7 disposable
   mutations, 7 reads, 14 negative comparisons. No simulation, sizing or recalibration.

## Work completed

| Commit | Change |
|---|---|
| `05edbdde` | `ReadBack/AcceptanceFixtures.cs` (COM-free factory, independent of the calibration models), `ReadBack/Fixtures/` (the two committed fixtures, `SHA256SUMS.txt`, provenance README), `readback-fixtures` mode, `Run-S08-ReadBack.ps1 -Phase acceptance / acceptance-negative` (fixture and EPW hashes checked first), `AcceptanceFixtureTests` (10), `.gitattributes` `-text` for fixtures and evidence |
| `62ce26cc` | Core: `GateRuleSet.Approved` (OD-G1..G3 as approved; Interim and ProvisionalProposed unchanged, so PR-B evidence replays as before) and `QuantityInput.InactiveNativeTransformation`, honoured only under Approved and only for S-UNSPEC against a native exact 0. Comparator: `T-VENT-INACTIVE`, set only for ticV rows 6.hourly, 6.eflh and 8b of a V0/V1 zone after checking every condition of decision 3; the reason for applying or refusing it is in `Supporting`. `--rule-set approved`. Tests: core `ApprovedRuleSetTests` (9) and the exhaustive sweep extended to both new dimensions; `InactiveVentilationTests` (16, full compare on the committed calib-two/calib-box snapshots) |
| later | `Pack-S08C-Evidence.ps1`, `Documentation/S08-PRC-Acceptance-evidence/`, `AcceptanceEvidenceReplayTests` (5), this record |

### Fixtures

| | `s08-fix-box` | `s08-fix-two` |
|---|---|---|
| sha256 | `45524172…2acd08` | `20da9b58…59baa0` |
| Geometry | one zone 6.4 x 4.2 x 2.85 m | A 4.5 x 5.4 m, B 3.6 x 5.4 m, 3.05 m, asymmetric partition |
| Ventilation | V2 (0.01 m³/s·p + Ventilation profile) | V1 / V1 (A per person + per area, B 0.75 1/h) |
| Openings | framed south, unframed west, closed | A framed south; B framed north, unframed east; closed |

Both: centre-line panels with `PhysicalThickness`, stored Area/Volume = centre-line geometry, every S07 gain, fraction,
setpoint, humidistat and infiltration authored, single-pane glazing, no exhaust, systems or IZAM data, no embedded weather.

## Licensed results (`C:\TasOut\s08c\acc1`, harness `9cbdf5bb…` from `62ce26cc`)

| Run | approved (verdict) | provisional (baseline) |
|---|---|---|
| s08-fix-box | PASS (exit 0) | PASS |
| s08-fix-two | PASS (exit 0) | BLOCKED owner (exit 12): proposed G1 on the 6 ticV rows |
| s08-fix-box-repeat | PASS | PASS |

Reader and writer controls: 0 differences (77,748 / 81,410 leaf values). Negatives: all FAIL (exit 10) under both rule
sets, originals unchanged. Details and file list: `Documentation/S08-PRC-Acceptance-evidence/README.md`.

**Mapping differences reported (none gated):** the T3D `frameWidth` (0.065 saved, the construction's DefaultFrameWidth,
vs 0.075 by the writer frame-thickness rule), as in PR-B; T3D `framePerc` agrees but is writer-only (NOT RUN). No other
quantity needed a transformation except T-FRAME 0 % (box unframed window, verified in PR-B) and T-VENT-INACTIVE.

## Decisions and assumptions (for review)

- The approved rules are a third rule set, not an edit of the provisional ones: historical verdicts stay reproducible.
- T-VENT-INACTIVE is decided by the comparator, which owns the snapshot and calendar, and only passed to the core as a
  named, verified transformation; the core still requires the approved rules, S-UNSPEC and a native exact 0 per element.
  A non-zero finite factor over an all-zero schedule qualifies (decision 3 constrains schedule values and flows).
- The acceptance compares under `approved` and `provisional` (the bound allowed two per run); `interim` was not rerun.
- `--rule-set approved` is the verdict; PR-B calibration evidence was not recompared under it (no licensed step needed).

## Tests and validation

Local (Release, sequential) at the final content: core 258/258 (exhaustive sweep included); read-back 135/135 (104 PR-B
+ 10 fixtures + 16 inactive ventilation + 5 replay). Mutation checks: weakening the exact-zero precondition fails 7
InactiveVentilationTests; removing the approved-rule guard fails 13. `SAM_Tas.sln` restore and rebuild: see the PR
description. Hosted Build (Windows) and SPDX: see the PR description.

```text
dotnet test benchmark/SAM.Analytical.InputEquivalence.Tests/SAM.Analytical.InputEquivalence.Tests.csproj -c Release
dotnet test SAM_Tas/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests.csproj -c Release
```

## Unresolved, risks, limits

- One workstation and Tas build (2.0.0.1, 2025-06-04); the 12 `TAS3D.exe` shutdown faults are the known local crash.
- Not exercised by the fixtures (unchanged PR-B limits): internal floors/ceilings, doors, rooflights, gas-filled glazing
  (gas-gap resistance and derived g are not computed), OpeningProperties, function profiles, multiple design days per role,
  leap-year weather, widths ON (a separate task under roadmap decision 7).
- The `frameWidth` writer rule versus the saved DefaultFrameWidth is reported, not fixed (T3D only, never gated).
- Machine-local, copy before switching machines: `C:\TasOut\s08c\acc1` (TBD/T3D files) and `C:\TasOut\s08c\weather\budapest.epw`.

## Next step

Independent review of this draft PR (GPT-6, effort High, new session, read-only review mode, because it adds an approved
rule set and a pass-enabling transformation whose scope must be checked for false-PASS routes). Then the owner's merge
decision, the known-SHA `PROJECT_PROGRESS.md` closeout and guarded branch cleanup, and a separate reviewed SAM PR for the
roadmap S08 row. S09 starts only on the owner's instruction.
