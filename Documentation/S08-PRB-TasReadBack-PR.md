# S08 PR-B: independent Tas native-input read-back harness

Task S08 of the SAM simulation roadmap, PR-B of the approved sequence (PR-A core SAM_Tas#104, merged `d08916ca`;
PR-C fixtures and acceptance, not started). Contract: SAM `documentation/SIMULATION_INPUT_MAPPING_S07.md` (SAM#216).
Branch `feature/s08-tas-readback-harness`, base `sow/2026-Q4` (`3d16b5c7`). **Draft, for independent review. Not merged.
S08 is not accepted: no fixture was chosen or compared.**

## Status

- PR: SAM-BIM/SAM_Tas#105 (draft) into `sow/2026-Q4`. The final head and hosted checks are in the PR description.
- Validated content SHA: `b9975571532612454baa6f07fbd4fa9f3be16a63` (code identical to `3d20d0fc`, from which the
  licensed harness was built; this record-only commit follows it).
- Dependency selection: no SAM-BIM dependency repository has a `feature/s08-tas-readback-harness` branch (checked with
  `git ls-remote` on SAM, SAM_gbXML, SAM_Systems, SAM_Windows, SAM_SolarCalculator, SAM_Psychrometrics, SAM_Mollier), so
  CI clones each one at `sow/2026-Q4`. Confirm in the job log of the final head.
- Offline (local, Release, on the code of `3d20d0fc`): core 249/249, read-back 39/39 (also on the final evidence), TM59
  1249/1249, benchmark producer 26/26 (Simulation excluded), GenOpt 432/432. Full `SAM_Tas.sln` Release restore and rebuild
  succeeded.
- Licensed (this workstation, Tas 2.0.0.1 dated 2025-06-04): the calibration, compare and negative phases all ran as
  expected. Details below.

## Launch decisions (owner, 2026-10-20, recorded here for portability)

The owner authorised PR-B implementation in SAM_Tas only and approved launch items 1-7 as listed, with these
clarifications:

1. **Write scope:** SAM_Tas only. No production converter or default change, no SAM_UI/SAM/SAM_Validation/SAM_OpenStudio
   change, no roadmap update.
2. **D-FIX direction approved:** the box fixture is V2, centre-line, with PhysicalThickness. The two-zone fixture is V1.
   Both are factory-authored with a declared convention, hashes and provenance. **Fixture creation, provenance sign-off
   and final acceptance stay in PR-C.**
3. **OD-G1..G3 stay provisional.** Both rule sets floor every G case at BLOCKED and label it "proposed".
4. **OD-W1:** the §4.1 reference is approved. **Widths-ON verification is a separate later task.**
5. **OD-W2:** stored volume is compared with the authored-shell geometric volume. The offset volume is a separate
   quantity (widths ON only, not exercised).
6. **P-ALIGN and Sizing:** exactly **one** small synthetic 365-day alignment simulation and **one** informational
   Sizing=true comparison, both on disposable PR-B calibration models. Originals unchanged; settings, hashes and results
   recorded.
7. **Leap year:** the 8760-hour requirement applies to annual weather and expanded annual series. Constant and 24-hour
   profiles stay supported. An 8784-hour input is UNSUPPORTED-MAPPING/BLOCKED and is never truncated.

The owner also asked that all series-element evidence be kept, including non-decisive proposed cases and blockers.

## Work completed

Added a `readback-*` family of modes to `SAM.Analytical.Tas.DirectT3D.Validation` (the existing harness; `Program.cs`
dispatches them), and a COM-free test project:

| File (`SAM_Tas/SAM.Analytical.Tas.DirectT3D.Validation/`) | Role |
|---|---|
| `ReadBack/TbdReadBack.cs` | Raw TBD/T3D reader (COM). `openReadOnly` with its result checked, an existence check first, no `SAMTBDDocument`, no `ToSAM`, no SAM_Tas query, no save. Every field is Absent, Unreadable, NaN, Zero or Finite. Hashes the file before and after the read. |
| `ReadBack/TsdReadBack.cs` | P-ALIGN TSD reader (COM). Read-only, refuses a stub TSD under 1024 bytes. |
| `ReadBack/TbdMutation.cs` | Licensed negative checks, applied to disposable copies (COM). |
| `ReadBack/ReadBackRun.cs` | Modes `readback-models`, `-export`, `-read`, `-tsd`, `-compare`, `-diff`, `-calibrate`, `-negative`. |
| `ReadBack/NativeSnapshot.cs` | COM-free snapshot DTOs and JSON. |
| `ReadBack/ReadBackExpected.cs` | Expected values from a fresh load of the frozen model and weather (raw parameters, S07 formulas, PR-A core). |
| `ReadBack/ReadBackIdentity.cs` | Geometry-only one-to-one identity. |
| `ReadBack/ReadBackComparison.cs` | Every S07 row to `Query.Evaluate`/`EvaluateSeries`/`Gate`. Coverage is built from the frozen model. |
| `ReadBack/ReadBackReport.cs`, `ReadBackCompare.cs` | Report, all-element tallies, every series element (CSV.gz), preconditions, exit codes. |
| `ReadBack/Calibration.cs`, `CalibrationRules.cs`, `calibration-rules.json` | Measured conventions and the committed rule table. |
| `ReadBack/CalibrationModels.cs`, `ReadBack/SnapshotDiff.cs` | Synthetic calibration models. Snapshot diff for the reproducibility controls and the Sizing diff. |
| `Run-S08-ReadBack.ps1`, `Pack-S08-Evidence.ps1` | One process per step under a watchdog; evidence packing. |

Also added: `SAM_Tas/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests` (39 tests), one CI step in
`.github/workflows/build.yml`, two `-text` lines in `.gitattributes` (byte-exact evidence and rule table), the evidence
folder `Documentation/S08-PRB-ReadBack-evidence/` and this record.

### Contract route actually run

`readback-export` builds its own `WorkflowSettings` and records every property in `export.json`: `T3DRoute=Direct`,
`UseWidths=false`, `Sizing=false`, `Simulate=false`, `AddIZAMs=false`, `RemoveIZAMs=false`,
`RemoveMechanicalVentilationGains=false`, `UpdateZones=true`, `UnmetHours=false`, `RemoveExistingTBD=true`. It refuses a
run directory that is not empty, so the TBD delete only ever happens in a fresh, disposable directory. `WeatherData` is the
frozen `weather.json`. The 17 post-export steps that ran are listed in `export.json`. `WorkflowSettings` defaults, SAM_UI
and the Both producer are untouched. Exceptions: only the approved P-ALIGN run used `Simulate=true` (365 days), and only
the approved Sizing export used `Sizing=true`.

### Freeze, hash, fresh process

The source model and weather are hashed, then frozen as `input.json` and `weather.json` (read-only) and hashed again. The
workflow converts a fresh load of the frozen files, and the frozen files are re-hashed after the run. The read runs in a
separate process after the export has exited. `readback-compare` refuses the comparison as **infrastructure** when any of
these fail: the reader has the export's process id, it started before the export ended, the TBD or T3D hash differs from
what the export wrote, or a frozen input changed. Each comparison builds the expected values from fresh loads.

### What each S07 row reads (persisted file -> raw field)

| S07 | Native (TBD unless stated) | Expected (frozen SAM, raw) |
|---|---|---|
| 1a | `zone.floorArea` | Σ floor-group panel polygons at the shell bottom |
| 1b host | `zoneSurface.area` (net of openings, N-1) | panel outline area − aperture outlines |
| 1b pane/frame | Σ areas of BEType 12/13/14/20 and 15 surfaces inside the aperture outline | T-FRAME split recomputed (convex inward offset; holes; no-frame cases) |
| 1b T3D (not gated) | T3D `window.framePerc`, `isPercFrame` (unreadable), `frameWidth` | T-FRAME percentage; writer frame-width rule |
| 2 | `zone.volume`; and D-F9 C: stored `SpaceParameter.Volume` against the shell | `shell.Volume()` of the authored panel polygons |
| 3a | per zone side: the surface's `buildingElement.GetConstruction().materials(1..n)` read in zone-facing order by `reversed` (N-2); `materialWidth[j]`, λ, ρ, c, 4 reflectances, 2 emissivities | SAM layers in zone-facing order (first space as listed, a later space reversed) and their library material |
| 3b / 3b.ground | U from the native layers, films from native `inclination`/surface type | U from the SAM layers, films from panel group and space geometry (F-OPQ) |
| 3c | pane-element layers (τ, ρ, ε, λ, thickness); derived U (F-GLZ-U) | aperture pane layers; derived U. g not computed (no S07 gas or g rule in the core) |
| 4a/4b/4c | annual IC `ticOSG/OLG/LG/ESG/ELG.factor`; × `zone.floorArea`; `(OSG+OLG)/personGain`; `lighting/occupant/equipmentRadProp` | raw gains ÷ stored Area; totals; people per m²; authored fractions |
| 5 | `ticLL/UL/HLL/HUL` factor × shape(h), 8760 elements | expanded SAM profile |
| 6 | shape(h) per gain/infiltration/ventilation slot, 8760 elements; EFLH | expanded SAM profile; Σ |
| 7a/7b | `ticI.factor`; × `zone.volume`/3600 | ACH; ACH × shell volume/3600 |
| 8a | `[SAM_META_V1]` carrier bases (crossing only). `freshAirRate` recorded as supporting evidence, not gated | the four raw bases |
| 8b | V2: `ticV.factor` × `zone.volume`/3600 × shape(h); V0/V1: peak of the same | D-F2 state from the frozen model |
| 8d | aperture type on the pane element (`dischargeCoefficient`) | closed window: S-UNSPEC |
| 8e/IZAM | `GetIZAM(i)`: any IZAM makes the run at least FAIL | none |
| 9 | weather year lat/lon/altitude/time zone, building lat/lon/time zone, `groundTemperature` (9.ground), `GetAnnualParameter(0..6)` at CLR index h+1, 8760 elements | frozen weather (2 m ground series mean) |
| 10 | `GetCooling/HeatingDesignDay(i).GetDesignDay(j)`: integer `yearDay`; `GetWeatherDay()` slots h+1 | §3 precedence re-stated in the harness; `DayOfYear` in the day's own year |

Day-type coverage: calendar `days(1..365)` against the annual internal condition's day types. A day the condition does
not carry reads as Absent. The HDD internal condition is read and recorded, not gated (S07 states no HDD expectation).

### Identity

A SAM space is a native zone when every panel outline pairs one to one with an opaque room-surface outline
(`Query.MatchPolygons`; collinear points dropped on both sides). A zone claimed by two spaces is ambiguous. The writer's
`[SpaceGuid]` carrier can only contradict a geometric match, never create one. A native surface or zone that no SAM
object claims is an EXPORT-DEFECT. Apertures own the pane and frame surfaces that lie in their outline.

Weather and design days pair structurally: one item on each side per role. More than one design day per role could only
pair by writer name and would be WriterOnly (NOT RUN).

T3D windows tie to their aperture only through the GUID the writer put in the window description. That is writer-only
identity, so T3D field checks are reported and **never gated**.

## Calibration (rules fixed before any fixture)

Calibration models: `calib-box` (V2, 2 windows: framed 2 × 1 m and unframed 1 × 1 m), `calib-two` (asymmetric partition,
V1 and V0), `calib-align` (P-ALIGN pulses). Synthetic marker weather with ground temperatures. Every authored value is
distinct. `readback-calibrate` marks a rule verified only when every observation agrees and the answer is unique. The
committed `ReadBack/calibration-rules.json` (sha256 in every report) has these rules:

| Rule | Measured answer |
|---|---|
| N-1 host area | **net** of openings (3/3 sides) |
| N-2 layer orientation | `reversed` 0: `materials(1)` faces the zone. Evidence: 16/16 single-space surfaces have reversed 0; TAS's construction absorptances for 7/7 elements use `materials(n)` external and `materials(1)` internal reflectance, consistent with SAM's inside-to-outside order; the partition is unreversed on the first space and reversed (-1) on the second |
| N-3.1 frame surfaces | pane and frame areas equal the T-FRAME split |
| N-3.2 frameWidth | no independent geometric effect: the saved T3D `frameWidth` is the ring width TAS derives from `framePerc` and the opening size (0.052), not the writer's frame thickness (0.068) |
| N-3.3 0 % frame | no frame surface (the 0 % frame area is then a verified ACCEPTED-TRANSFORMATION). 100 % is not reachable from SAM geometry; not exercised |
| N-3.4 frame fields | T3D only. `isPercFrame` is **unreadable** (the TAS3D getter fails with RPC_E_SERVERFAULT) |
| N-5 hourly / yearly / weather / design day | slot or CLR index = SAM hour + 1 (lower bound 1, length 8760) |
| N-5 alignment (P-ALIGN) | **aligned:** weather shift 0. Lighting (24 h) at SAM hour 10 on all 365 days, sensible equipment (8760 h) only at hour 2390 (day 100, 14:00), infiltration at hour 20, the heating-setpoint step at hour 4 |
| N-6 calendar | `days(1..365)`; day types Weekday/Saturday/Sunday; the annual condition carries them all, plus CDD |
| N-8 winding | outlines reproduce the panel boundary; 7 same and 11 opposite winding (identity is winding-independent) |

Reproducibility controls. The reader read the same TBD in two fresh processes, and the writer exported the same frozen
box twice: both gave **0 differences** over 77,550 (box) and 89,905 (two-zone) leaf values, with only TAS-minted GUIDs
and reader metadata ignored. The informational Sizing=true export changed only zone `sizeHeating`/`sizeCooling` (2 -> 0)
and `maxHeatingLoad` (0 -> 751.77 W). **No S07 input changed.**

### Licensed run sequence

| Root (machine-local) | Code | What |
|---|---|---|
| `C:\TasOut\s08b\dry` | working tree | Dry run without the approved executions. Not evidence. |
| `C:\TasOut\s08b\cal1` | working tree on `3d16b5c7` (dirty 4) | **The one P-ALIGN simulation and the one Sizing=true export** (`approved-executions/`), and the first derivation of the committed rules. |
| `C:\TasOut\s08b\cal2` | `3d20d0fc` (clean) | Re-run, reusing cal1's approved outputs. Same results. Its Sizing diff compared a different model generation, so it was superseded. |
| `C:\TasOut\s08b\cal3` | harness `direct-t3d-validation.dll` sha256 `a9182e81…`, built from clean `3d20d0fc`; only `Run-S08-ReadBack.ps1` uncommitted (the same-generation Sizing baseline, committed afterwards unchanged) | **The committed evidence.** cal1's approved outputs (P-ALIGN TBD/TSD, Sizing TBD and their own Sizing=false box) were re-read in fresh processes; neither was executed again. |

The calibration models are regenerated on each run, with new SAM GUIDs (`Create.Aperture` mints them). Each run's frozen
inputs are therefore committed with that run. The rules re-derived at cal2 and cal3 equal the committed table: 0 of 13
differ.

## Licensed results (calibration models only)

| Run | Interim | Provisional |
|---|---|---|
| calib-box, calib-box-repeat, calib-box-sizing | PASS (exit 0): self-consistency of the model the rules were measured on, **not a fixture result** | PASS |
| calib-two | BLOCKED owner decision (exit 12): V1 and V0 `ticV` is present with zero delivery = proposed **G1** UNLISTED-DEFAULT (OD-G1) on 8b | BLOCKED |

T3D field checks, not gated: `frameWidth` disagrees with the writer rule (0.068 written, 0.052 saved); `framePerc` is
NOT RUN (writer-only identity).

Negative checks, each on a disposable copy, originals hash-identical before and after (`negatives/`):

| Case | Interim | Provisional |
|---|---|---|
| swap `[SpaceGuid]` carriers | FAIL (identity) | FAIL |
| flip partition `reversed` | FAIL (3a) | FAIL |
| wall conductivity × 1.5 | FAIL (3a, 3b) | FAIL |
| lighting factor 0 | FAIL (4a zeroed, 4b) | FAIL |
| infiltration factor NaN | BLOCKED (G3 proposed) | FAIL |
| heating slots rotated 1 h | FAIL (5) | FAIL |
| added IZAM | FAIL (native object SAM never stated) | FAIL |

Watchdog: no step timed out. `TBD.exe` (and once `TSD.exe`) stayed resident after a step; the script killed it between
steps. The Windows Application log shows `TAS3D.exe` faulting at shutdown after T3D opens. This is the known
shutdown crash on this workstation; every read's file hashes were unchanged.

## Tests

`SAM.Analytical.Tas.DirectT3D.ReadBack.Tests` (offline, CI step added) replays the committed real snapshots. It covers:
the box PASS and no-rules NOT RUN; the two-zone G1 under both rule sets; end-to-end exit codes; Absent, Unreadable, NaN
(G3 under both rule sets) and Zero reader states; an unreadable profile; coverage built from the model and an
incomplete-coverage gate; swapped carriers; geometry without the carrier; swapped boundaries; an unstated native surface;
a duplicated boundary; swapped layers; a flipped `reversed`; an 8784-hour profile (UNSUPPORTED, not truncated). It also
tests mixed series with an informational element beside every gating state in both orders, non-decisive G1 and G3 kept
beside a decisive FAIL, the serialized report and the full elements file, exit codes, infrastructure preconditions, EPW
leap-year refusal, the snapshot diff, the committed rules, reproduction of every consumed rule from the committed
evidence, a shifted P-ALIGN pulse, the T-FRAME geometry and a D-FIX criterion.

## Commands

```text
dotnet test benchmark/SAM.Analytical.InputEquivalence.Tests/SAM.Analytical.InputEquivalence.Tests.csproj -c Release
dotnet test SAM_Tas/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests.csproj -c Release
msbuild SAM_Tas.sln -t:Restore -p:Configuration=Release ; msbuild SAM_Tas.sln -t:Rebuild -p:Configuration=Release
```

Licensed: see `Documentation/S08-PRB-ReadBack-evidence/README.md`. Machine-local prerequisites (copy or provision on
another machine): licensed Tas, `C:\TasOut\s08b\*` run roots holding the TBD/T3D/TSD files (their hashes are committed).

## Decisions and assumptions (for review)

- **Calibration self-consistency is not acceptance.** The rules come from the same synthetic models they are then
  compared on. Fixture results come in PR-C, under the frozen rules.
- **N-2's flip** for `reversed` ≠ 0 rests on TAS's definition of the flag, checked as exactly one reversed side per
  partition. A native thermal effect of the flip was not measured.
- **Tolerance mapping for T3D checks** (not gated): `framePerc` uses the 1b definition, `frameWidth` the 3a thickness
  definition.
- **4a people density** has no TAS field. It is derived from native fields only: (OSG + OLG factor) / `personGain`.
- **8b for V0/V1** compares one scalar: the native peak delivered flow.
- **Heat-flow direction:** the native films follow the measured inclination (0 roof, 90 wall, 180 floor). The SAM side
  uses the panel group and the shell geometry.
- **The approved executions ran from the uncommitted PR-B working tree** on base `3d16b5c7` (provenance and harness exe
  sha256 in `approved-executions/`). Their persisted outputs were re-read in fresh processes at the committed code;
  neither was executed again.

## Unresolved, risks, owner questions

- **OD-G1 for V0/V1 delivery.** TAS always holds a `ticV` profile; with no delivery it reads as present and zero.
  D-F2 states "no `ticV` = MATCH with intent", but the core treats a zero native against S-UNSPEC as G1 (BLOCKED). The
  owner decides whether a zero-delivery `ticV` is native absence for 8b. Until then, every V0 and V1 zone is BLOCKED.
- **4c Tas defaults** are not listed in S07. When the radiant fractions are not authored, the TAS defaults
  (HDD condition 0.3 / 0.2 / 0.1) are EXPORT-DEFECTs. The fixtures must author them (D-FIX).
- **T-FRAME:** the persisted T3D `frameWidth` differs from the writer's value. It was reported, not changed.
  `isPercFrame` cannot be read.
- **Not computed:** derived g (3c.g) and gas-gap resistance. Glazing U with a gas layer is BLOCKED technical.
  OpeningProperties (8d) are BLOCKED technical. Non-convex aperture frame offsets are S-INVALID.
- **Not exercised:** internal floors and ceilings, doors and rooflights, multiple design days per role, function
  profiles, and widths ON (a separate task).
- **Recorded in passing, not fixed (as instructed):** ToSAM reflectance slot, thermostat guard, the 2018 design-day year,
  `SAMTBDDocument` creating a missing file. The independent reader uses none of them.
- Owner reference for S09: `C:\Users\michal\Documents\SAM_daily\OpenStudio` (Building Energy Modeling with OpenStudio,
  PDF; not reviewed here).

## Next step

Independent review of this draft PR (Claude Opus 5.5, effort High, new session, Plan mode, because reader independence
and the calibration rules are the risk). Then the owner's merge decision. After merge: the known-SHA `PROJECT_PROGRESS.md`
closeout and guarded branch cleanup. Then PR-C (fixtures and acceptance) as a new session on the licensed workstation,
Claude Opus 5.5, effort High, Build mode after the owner answers OD-G1 for V0/V1 delivery and signs the fixture
provenance.
