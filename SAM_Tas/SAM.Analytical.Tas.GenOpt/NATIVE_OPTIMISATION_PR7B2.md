<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR7b-2: licensed acceptance matrix of the "tas-model" engine (and PR9's open cases)

Branch `feature/optimisation-pr7b2-acceptance` → base `sow/2026-Q4` (cut from `28b59fcb`, the Sizing closeout). Record
date: 2026-10-11. **This is the record of record** (SAM-BIM/SAM_Tas#95). Companion: SAM-BIM/SAM_UI#223, same branch name (the opt-in harness's
new cases; short record `documentation/NativeOptimisation-PR7b2-Acceptance.md`). The two PRs are independent: no product
code changes in either, so either can merge first.

## Current status

Acceptance run and recorded; follow-up 2026-10-12 below. **No product code changed in this PR.** Two defects:

- **D1**: with Sizing on, building-side results do not survive Energy Simulation after Apply. The follow-up **measured
  glazing too** (G1: heating +14.3%), so D1 covers every TBD target on a sized TBD, not only setpoints. **Owner decision
  2026-10-12: option (a) for V1** — implemented in SAM-BIM/SAM_Tas#97 (four SAM_Tas layers) and SAM-BIM/SAM_UI#224 (the
  window's explanation); option (b) deferred to V2.
- **D2** (fixed in its own PR, SAM-BIM/SAM_Tas#96): the generated script ignored `SimulateEx`'s status, so a plant
  simulation that failed ("Sizing Flow Failed", on a SAM-created TPD) was a successful evaluation with stale numbers.
  #96 accepts only the measured success ("Done") and also fails a NaN or infinite objective.

Not runnable for lack of an input: multi-plant-room TPD (no production route makes one), internal glazing, larger user
glazing pool; controller on a SAM model's own TPD reached the engine but that TPD cannot size on this workstation.
SAM-BIM/SAM_Tas#95, SAM-BIM/SAM_UI#223, SAM-BIM/SAM_Tas#96, SAM-BIM/SAM_Tas#97 and SAM-BIM/SAM_UI#224 open, not merged.

## Coverage matrix (reconciled)

Builds under test: SAM `c080dd08`, SAM_Systems `c0dc428`, SAM_Tas `28b59fcb`, SAM_UI `18d59010` (= `origin/sow/2026-Q4`
after SAM_Tas#93/#94 and SAM_UI#220/#222), rebuilt in that order on this workstation (Tas 2.0.0.1, 2025-06-04); GenOpt
352/352 and UI 2795/2795 on those builds, the test bins loading the rebuilt DLLs (hashes in the evidence). "Before" is
the earlier evidence and the commits it tested; "now" is this run. Inputs are copies; originals' hashes unchanged.

| # | Requirement (exact behaviour) | Before (evidence, commits) | Now | Status |
|---|---|---|---|---|
| 1 | Target effect, **Demo** (Systems Demo: TBD `bf3c0c7d`, TSD `40f169af`, TPD `c7178b65`) | PR7b-1 smoke (`581853a8`, other workstation): one point per block; PR8 A/B/C/F (SAM_UI#219) | heating 18/20/22 °C → 10 971.6 / 15 227.9 / 20 909.5 kWh; cooling 24→26 °C → 2 978.5 → 1 644.4 kWh, 0 → 22 h > 28 °C; controller 3 → −5 → plant energy 27 387.79 → 26 358.80 kWh; 5 glazing options | **covered** |
| 2 | Target effect, **SAM model** | cooling 24-h profile (PR7b, PR8 E, PR9 S1, Sizing S1); glazing (PR8 D, PR9 S2, Sizing S2); heating never (model had heating off) | heating on two SAM models whose authors set heating (below, row 3); cooling on `ModelA.sam` (Cell 1, own 25 °C profile): 24.3 → 27.9 °C gives 657.1 → 365.3 kWh, monotonic | **covered**; controller on a SAM model: **follow-up CS** — its own TPD fails to size here (fixture), which exposed **D2** |
| 3 | **Heating on a SAM model with heating on** | untested | H1 `SAM_zoningAM-test.sam` (`04ef4798`, owner's TM59 variant, Bathroom_2 16 °C): demand ≈ 0 (0 / 0 / 0.0248 kWh at 14.9/15.5/16.5 °C), journey PASS but weak. H2 `ModelA.sam` (`eac750d1`, committed SolarCalculator fixture, Cell 1 21 °C 07–19, setback 16): monotonic 6 263 → 11 899 kWh over 18.3–21.7 °C; **Sizing off: Apply + Energy Simulation 2 = best within 1.9e-15**; **Sizing on: D1** | **covered, defect D1** |
| 4 | **Measures vs SAM_Tas' readers on the evaluation folders** | transitive only (through PR7a numbers) | 14 evaluation folders: heating, cooling (reader Wh ÷ 1000), overheating 25/28 (`Query.Overheating`, worst occupied zone) and plant energy/CO2 (`SystemEnergyCentreResults`, re-simulated and saved copy) — all PASS, ≤ 3.5e-15 relative, hours exact | **covered** (cost: see F1) |
| 5 | **4–5 glazing choices**; larger "My glazing systems" pool | PR8 C (Demo, 5) and D (SAM, 4), 13-system pool on the other workstation | G5 Demo, 5 options (default library, widened filter), each pane read back at the catalogue's g/U/light, readers PASS; DR-S2 on the direct route | 4–5 **covered**; larger pool **not run** (no user library here) |
| 6 | **Hooke–Jeeves with setpoints and a plant controller** | PR8 B: two setpoints, no controller | Demo heating + cooling + HeatPumpController, min plant energy: Success, 38 simulations, 725 s; best (18.4375, 28, −5), 27 387.84 → 25 635.29 kWh; every evaluation building then plant; readers PASS on the best | **covered** |
| 7 | **Repeatability, durations, plant shutdown** | PR8 repeats A/C/E; PR9 S1×3, D1×2 (same session) | in-session repeats bit-identical (B1, P1); other workstation: plant bit-identical, building ≈ 4e-6 relative (F2). Durations: Demo building test 9.5–11.6 s, plant test 13–15 s, HJ mean 16.0 s (building 5.0, plant 7.7); SAM model 6.9–8.3 s; Energy Simulation 29–40 s. TPD.exe shutdown crash (0xc0000005, Application log) 9 times in 73 TPD sessions, 4 of 39 in HJ: **0 failed evaluations, 0 retries, 0 Error.txt**; no Tas process left after any step | **covered** |
| 8 | **Multi-plant-room TPD** | unproven (PR7b-1 decision 2) | all 9 TPDs on this workstation have one plant room (probe); SAM_Tas `Create.TPD` always writes one "Main PlantRoom", so no production route makes one | **not run** — owner-supplied TPD needed |
| 9 | PR9: **internal glazing between spaces** | not run (PR9 decision 15) | none of 20 local SAM models (incl. repository fixtures) has an aperture on a panel between two spaces | **not run** — input missing |
| 10 | PR9: **direct export route through optimisation, Apply, Energy Simulation** | the route itself validated (SAM_Tas#84); Apply on it never | DR (`SAM_zoningAM.sam`, DirectT3D on, Sizing on; no gbXML written, direct pipeline in the timings): S1 setpoint Apply bit-exact, ES 2 = best within 6.8e-16; S2 glazing: the direct-route TBD passed the pane/frame/placement checks, applied, overheating 19 h = 19 h | **covered** |
| 11 | **Sizing-enabled folders as default inputs** (follow-up) | Sizing S1/S2 (non-binding: S2 cooling 0 in every simulation, no heating) | H2, C1 (setpoints) and **G1 glazing** on sized `ModelA.sam`: Apply exact, sizing copies unchanged, but ES 2 ≠ best (G1 heating +14.3%); Sizing off exact (≤ 1.6e-15) | **defect D1** (owner decision) |

## D1 — defect: with Sizing on, setpoint results do not survive Energy Simulation after Apply (owner decision)

**Reproduced** (H2 heating, C1 cooling; `ModelA.sam`; Energy Simulation dialog defaults, Sizing on):

| Case | Best simulation of the run | Energy Simulation 2 after Apply | Same case, Sizing off |
|---|---|---|---|
| H2 Cell 1 heating 18–24 °C | 18.334 °C → **6 263.4 kWh** | **11 393.8 kWh** (+82%) | 101 833.6 vs 101 833.6 kWh (1.9e-15) |
| C1 Cell 1 cooling 22–28 °C | 27.872 °C → **365.3 kWh** | **521.8 kWh** (+43%) | — |

Apply itself is exact in both (TBD hours = (float)best, setback kept; SAM profile bit-exact; sizing copies untouched).

**Cause, measured** (driver `zones`, copies): Sizing leaves every zone with **fixed capacities** (`sizeHeating`/`sizeCooling`
= no sizing, `maxHeatingLoad`/`maxCoolingLoad` set) sized on the design days for the setpoints of that moment. Each
evaluation edits the setpoint but keeps those capacities; Energy Simulation after Apply sizes again for the new setpoint,
and the sizing of neighbouring zones is coupled:

| TBD | Cell 1 heating / cooling capacity | Cell 2 heating / cooling capacity |
|---|---|---|
| ES 1 (21 °C / 25 °C) | 1 586.5 W / 2 947.1 W | 1 525.4 W / 2 671.6 W |
| ES 2 after H2 (Cell 1 heating 18.33) | 1 416.6 W / 2 947.1 W | **3 076.4 W** / 2 671.6 W |
| ES 2 after C1 (Cell 1 cooling 27.87) | 1 586.5 W / **588.5 W** | 1 525.4 W / **5 452.5 W** |

Controls: the evaluation script reproduces Energy Simulation exactly on unchanged files (ES 1 TBD at 21 °C: 11 615.7322635474
= ES 1's TSD; ES 2 TBD at 18.33 °C = ES 2's TSD), and the readers agree with every evaluation. So the generated script and
the readers are right; the optimisation evaluates a different design problem (capacities frozen) from the one Energy
Simulation then solves (capacities re-sized). Where capacities bind, the optimiser can be rewarded for unmet load. PR9's
note "the heating design-day condition follows a heating setpoint at the next Energy Simulation… annual results are
unaffected" held for its Sizing-off runs only; with Sizing on it is false. Sizing S1 (`SAM_zoningAM.sam`, cooling) held
because its capacities do not bind. **Glazing behaves the same way (measured in the follow-up, G1 below):** a glazing
choice re-sizes the capacities at Energy Simulation 2 too (Cell 1 heating 1 586.5 → 1 879.1 W), so the result of the run
does not hold (heating +14.3%). D1 therefore covers **every TBD target** (setpoints and glazing) on a sized TBD; plant
controllers change only the TPD and are not affected.

**Options (owner):**

- (a) **Refuse TBD targets (setpoints and glazing) on a sized TBD** (zones with Sizing's fixed capacities), saying why and
  that Energy Simulation with Sizing off makes them available. Safe and small; it removes building-side optimisation from
  default folders, including cases that hold (Sizing S1/S2, non-binding).
- (b) **Re-size in every evaluation** as Energy Simulation does (the SAM_Tas sizing chain, with the model's oversizing
  factors). Faithful; a new evaluation stage (design-day sizing per evaluation, longer runs), and the generated script
  cannot call SAM_Tas, so it needs an architecture change.
- (c) **Keep and warn**: a ⚠ check line, the AI text and the Apply plan/outcome say capacities are fixed during the run and
  re-sized by Energy Simulation; compare after Energy Simulation.
- (d) Remove the capacities for the evaluations: then it matches neither Energy Simulation with Sizing on nor off.

**Recommendation:** (a) now as a small PR, covering setpoints **and glazing** (glazing is now measured), with a COM-free
regression test on an inventory that reports fixed capacities; (b) later only if sized building studies are wanted. No
code was changed here because each option reverses part of an approved decision. Until decided, the defaults (Sizing on)
can mislead: G1's ranking held but its values did not; H2 rewarded unmet heating load (+82% after Energy Simulation).

**Architecture review (2026-10-12): (a) vs (b), with the detector measured.**

- **Detector for (a), measured on copies** (driver `zones`, `followup-2026-10-12\d1-detector`): SAM Energy Simulation with
  Sizing **on** leaves every zone `sizeHeating/sizeCooling = 0` (`tbdNoSizing`) with fixed capacities (Cell 1 1 586.5 W);
  Sizing **off** leaves `2` (`tbdSizing`: Tas sizes inside every simulation; Cell 1 209 632 W, hence the ~17× heating);
  the Tas-authored Systems Demo has `1` (`tbdDesignSizingOnly`). Rule: **a TBD has frozen capacities when any zone has
  `tbdNoSizing` with a capacity > 0** — read-only, folder-only, it leaves the Demo and Sizing-off projects untouched.
  Over-refusal: a Tas-authored TBD whose engineer fixed capacities on purpose is refused too (documented limitation).
- **(a) placement:** SAM_Tas only (all engine and Apply callers are SAM_Tas GenOpt and SAM_UI): `TasModelInventory`
  reports the fixed-capacity zones; `TasModelCatalogue` does not offer TBD targets (setpoints, glazing) with that note;
  `TasModelRunner`/capabilities refuse a definition with a TBD target on such an inventory (definitions can be reused
  or hand-written); the Apply plan refuses a TBD change when the folder's TBD has fixed capacities (re-read at plan time).
  Apply already takes only the current window run's best point, bound to the source hashes, so no earlier run can reach
  it through the UI; the plan-time check is defence in depth for any other caller. SAM_UI: a check line and the AI text.
  Plant controllers stay offered. About 1–1.5 days; low regression risk (additive refusal, COM-free tests, 3 short
  licensed reads plus one window journey).
- **(b) cost:** `Query.Sizing` is several `tBDDocument.sizing(0)` passes (prepare; one per distinct " - HDD" lower limit
  when positive gains are excluded; Air_Glass on `_AIR` elements), per-zone oversizing factors from the **SAM model**,
  the optional system sizing method from its adjacency cluster, and settings from the Energy Simulation **dialog** that
  are **not stored in the Tas folder**. Re-sizing each candidate therefore needs those inputs captured and persisted with
  the run, the " - HDD" conditions kept consistent with every setpoint, and either a second implementation of the chain
  inside the C# 7 script (drift risk) or a SAM-side evaluation stage before TasGenExecute (architecture change; Tas COM
  in the SAM process). Several design-day passes per evaluation (estimate 2–5× evaluation time; not measured), and a new
  licensed matrix to show Energy Simulation 2 = best on both routes and every sizing setting. About 2–3 weeks; medium-high
  regression risk (every TBD evaluation changes).
- **User impact of (a):** on default folders (Sizing on) building-side optimisation is not offered; plant controllers are.
  "Run Energy Simulation with Sizing off" makes it available but is **not equivalent** (Tas then sizes plant without
  limit: ModelA heating ~17×); results found that way hold for Sizing-off simulations only. This is the documented V1
  limitation.

**Owner decision (2026-10-12): option (a) for V1 approved**; option (b) deferred to V2. Implemented in SAM-BIM/SAM_Tas#97
and SAM-BIM/SAM_UI#224. Validating the detector there narrowed the rule (driver `capprobe`, copies): `tbdDesignSizingOnly`
and `tbdSizing` zones are sized again by every simulation (a 50 W cap is restored) and stay allowed — so a Sizing-on
project whose zones are unheated (`tbdDesignSizingOnly`, e.g. `SAM_zoningAM`) is safe — while `tbdNoSizing` is frozen
**at any capacity**, and unmeasured types (`tbdMixed`, `tbdLoadPerArea`) or an unread sizing refuse too.

## Follow-up 2026-10-12 (same workstation, same builds)

Expectations written before the runs: `C:\TasOut\pr7b-2\followup-2026-10-12\EXPECTATIONS.md`. Builds: the PR7b-2
DLLs above (hashes re-verified equal at the start) plus SAM_UI rebuilt with the opt-in harness case **G1** (SAM_UI#223,
test-only; UI DLL `4498B91C`, GenOpt `D523E245` loaded by the test bin). `HKCU\Software\EDSL` was byte-identical to
`reg\edsl-before.reg` before, and was restored to it after every harness run.

**G1: glazing on a sized model with heating and cooling on** (`ModelA.sam` `eac750d1` as authored; filter widened in the
window: Ug +5, light +1; try every option; 4 options: current g 0.400/Ug 1.24, SIM_EXT_GLZ 230343 g 0.406/Ug 2.24,
SIM_EXT_GLZ_SKY g 0.548/Ug 1.69, SIM_INT_GLZ g 0.792/Ug 5.56):

| Run | Best option | Best simulation (heating / cooling kWh) | Energy Simulation 2 after Apply | Relative |
|---|---|---|---|---|
| G1, Sizing on, min heating | 1 (current) | 11 615.73 / 551.30 | nothing to apply; ES 2 = 11 615.73 / 551.30 | 1.9e-15 (non-binding) |
| G1-cooling, Sizing on, min cooling | 2 (Ug 2.24) | 12 128.03 / 540.85 | **13 856.67 / 537.24** | **+14.3% / −0.67%** |
| G1-cooling, Sizing off | 2 | 203 901.70 / 1 453.06 | 203 901.70 / 1 453.06 | 7.1e-16 / 1.6e-15 |

Apply was exact (TBD pane read back at the option's g/U/light; SAM aperture construction set; sizing copies unchanged).
Control: "Test one simulation" on Energy Simulation 2's files equals Energy Simulation 2's TSD exactly (13 856.6731808889,
537.239269579411), so the script and readers are right. Zone capacities (driver `zones`, ES 1 = run snapshot vs ES 2):
Cell 1 heating 1 586.5 → **1 879.1 W**, Cell 2 1 525.4 → **1 763.6 W**, cooling 2 947.1 → 2 919.1 W and 2 671.6 → 2 650.7 W.
The evaluations kept the ES 1 capacities and so under-reported heating by 14% (unmet load counted as saving). Overheating
equal (24 h). Note: with Sizing off this model's heating is ~17× larger (unlimited capacities), so "Sizing off" is a
different design problem, not a neutral workaround.

**CS: controller on a SAM model's own TPD** (owner's `000000_SAM_AnalyticalModel-It1a-futureZ1-It3B` .tbd/.tsd/.tpd, one
Energy Simulation with Create TPD, 2026-09-11; copies; sources' hashes unchanged). The catalogue offers its 5 controllers
(Main PlantRoom; cost withheld, `$`, F1); stored plant result 3 176.26 kWh / 1 648.48 kgCO2e. Test at Controller 4
(DHW temperature, 10 °C) and 30 °C: **0.249599888920784 kWh, CO2 NaN, both "successful"**, `t.PlantSimulation` 0.13 s.
SAM_Tas `SystemEnergyCentreResults` on a saved copy agrees (0.2496 / NaN). The driver's `simulate` (one argument
changed at a time) shows `SimulateEx` answering **"Sizing Flow Failed"** in 0.14 s under the script's arguments and under
SAM_Tas Create.TPD's own; the Systems Demo answers "Done" under both (27 387.79 / 27 385.77 kWh). So: the TPD cannot size
its plant on this workstation (fixture; it simulated when created on the owner's machine), and the script ignored the
failure → **D2**.

**D2** (fixed in SAM-BIM/SAM_Tas#96, record `NATIVE_OPTIMISATION_PLANT_SIMULATE_STATUS.md`): the script now accepts a
plant room's status only when it is SAM_Tas `SimulationDiagnostic`'s measured success ("Done"); a measured failure,
silence or an unknown answer fails the evaluation (tightened on review from "refuse measured failures only", because the
script has no results reconciliation to catch a silent failure); every status is traced. Licensed on #96: CS refused
(`… was not simulated: Tas answered "Sizing Flow Failed".`); a golden-section Run stops with EvaluationFailed after 2
evaluations (8 s), no best point; Systems Demo P1 and the building + plant chain **bit-identical** to before. GenOpt
370/370 there; mutation caught.

**MPR:** SAM_Tas `Create.TPD` always writes a single "Main PlantRoom" (`Create/TPD.cs`), so no production route makes a
multi-plant-room TPD; it needs an owner-supplied file.

Workstation build after the follow-up: `SAM_Tas\build` was rebuilt from this branch (product code = `28b59fcb`) after
#96's build; the DLL hashes differ from the ones above (GenOpt `976C49D0`, Tas `7F196727`, Core.Tas `F8C339EF`: these
rebuilds are not byte-reproducible); none of the follow-up runs used them. Rebuild in order before further evidence.

## Other findings (no change made)

- **F1 Cost unit is the workstation's, not the file's.** The same Demo TPD (`c7178b65`) reports its cost unit as `£` on the
  PR7b workstation and `$` (U+0024) here, so PR7b decision 7 ("cost only in £") correctly does not offer Annual plant cost
  on this workstation; the values are the same numbers. Plant cost was therefore not compared with the readers here.
- **F2 Building results differ between workstations by ≈ 4e-6 relative** (Demo baseline heating 15 227.8941594962 here vs
  15 227.8406637096 on the PR7b/PR8 workstation; cooling 2 978.52016448265 vs 2 978.53252598965; overheating equal); plant
  results on the same TSD are bit-identical. The other workstation's Tas version is not recorded. Within a workstation runs
  are bit-identical, so comparisons must use same-workstation baselines.
- **F3 `tas3d.exe` crashes at shutdown in every gbXML-route Energy Simulation** (8 of 8; 0 of 4 on the direct route); every
  run completed and produced results. Not optimisation code; recorded for the Energy Simulation owner.
- **F4 Heating fixtures:** `SAM_zoningAM-test.sam`'s heated rooms barely need heating (London TRY, TM59
  gains), so it is a weak heating fixture; `ModelA.sam` is the strong one.

## Not run, and the exact input needed

- **Multi-plant-room TPD:** a TPD with two or more plant rooms, with its TBD and the **one** TSD it uses (the engine refuses
  more), all from the same Energy Simulation. None of the 9 TPDs here qualifies.
- **Internal glazing:** a SAM model with at least one window hosted on an internal panel between two conditioned spaces,
  simulated through Energy Simulation (gbXML route first). Not authored by the harness, so that the placement check is
  tested against an engineer's model.
- **Larger glazing pool:** `Documents\SAM\User Libraries\Glazing Systems.json` with eight or more window systems that pass
  the filter (to exercise the "spread over the g order" rule licensed). This workstation has no user library.
- **Controller target on a SAM model (positive case):** a SAM model's folder after Energy Simulation with "Create TPD"
  **whose TPD simulates on this workstation** (the It3B TPD answers "Sizing Flow Failed" here; with #96 that is now a clear
  refusal, as it should be).

## Files changed

- `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7B2.md` (this record; 2026-10-12 also re-encoded: the
  committed text was UTF-8 read as Windows-1250 and saved again, so GitHub showed `â†’` for `→`; reversed exactly, 66
  sequences, nothing lost).
- `SAM_Tas/spikes/optimisation-pr7b2/` (driver, definitions, scripts, README) — evidence tooling, not in `SAM_Tas.sln`;
  2026-10-12 the driver gained `simulate` (SimulateEx status per argument set).

## Validation

- Rebuild in order: SAM, SAM_Systems, SAM_Tas, SAM_UI Release, 0 errors each (profile redirected, real `NUGET_PACKAGES`).
- `SAM.Analytical.Tas.GenOpt.Tests` **352/352**; test-bin `SAM.Analytical.Tas.GenOpt.dll` = `build\` (`D523E245…`).
- `SAM.Analytical.UI.WPF.Tests` **2795/2795** with the companion harness change.
- Licensed: 45 driver steps, 9 TPD probes and 5 harness runs (H1, H2, H2 Sizing off, C1, DR = S1+S2), one Tas operation at a time, each bounded (driver 300–3000 s, harness
  40–60 min, run watchdog 30 min); no timeout hit; no Tas process left after any step; `HKCU\Software\EDSL` exported before,
  restored after every harness run and at the end (byte-identical to the first export).
- The committed driver builds from its folder (relative root); `git diff --check` clean.
- Evidence (local, not committed): `C:\TasOut\pr7b-2` — `EXPECTATIONS.md` (written before the runs, with the cost
  deviation), `steps.log`, `logs\`, `runs\`, `ui\`, `investigate\`, `plant-check\`, `inputs\tpd-probe\`, `inspect\`,
  `build\`, `reg\`.
- Follow-up 2026-10-12: 3 G1 harness runs (G1, G1-cooling, G1-cooling-sizing-off; 112–127 s each, exit 0, no Tas process
  left, registry restored), 16 driver steps (catalogue, tests, plant reader, `simulate` ×7, zones ×2, D2 verification ×3);
  every step bounded and logged in `steps.log`; no timeout hit, no Tas process left. Non-zero exits, both explained: the
  CS plant-reader step (exit 1: CO2 NaN = NaN is a FAIL by design) and `simulate` arg3=15 (TPD RPC_E_SERVERFAULT in the
  result set's `Dispose`, after its sums printed; diagnostic only). Application-log shutdown crashes (F2/F3 kind) in 9 of
  19 steps, none during a simulation. Evidence: `C:\TasOut\pr7b-2\followup-2026-10-12`
  (`EXPECTATIONS.md`, `build\`, `defs\`, `inputs\`, `investigate\glazing\`, `simulate\`, `plant-check\`, `runs\`,
  `runs-d2\`) and `C:\TasOut\pr7b-2\ui\G1*`.

## Next step

1. D1 decided (option (a)); owner review of SAM-BIM/SAM_Tas#97 + SAM-BIM/SAM_UI#224 and of SAM-BIM/SAM_Tas#96.
2. Merge with owner approval only (merge commits, `--match-head-commit`): SAM_UI#223 and this PR (records/tests, any time),
   SAM_Tas#96, SAM_Tas#97, then SAM_UI#224 after its CI is rerun against the merged SAM_Tas Q4; then the
   `PROJECT_PROGRESS.md` closeouts on `sow/2026-Q4`.
3. Then the owner's order: Tas units correction, SAM_Deploy PR10. Inputs still needed:
   a multi-plant-room TPD (row 8), a model with internal glazing (row 9), a user glazing library of 8+ systems (row 5), a
   SAM-created TPD that simulates here (row 2 positive case).
