<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR7b-2: licensed acceptance matrix of the "tas-model" engine (and PR9's open cases)

Branch `feature/optimisation-pr7b2-acceptance` â†’ base `sow/2026-Q4` (cut from `28b59fcb`, the Sizing closeout). Record
date: 2026-10-11. **This is the record of record.** Companion: SAM-BIM/SAM_UI, same branch name (the opt-in harness's
new cases; short record `documentation/NativeOptimisation-PR7b2-Acceptance.md`). The two PRs are independent: no product
code changes in either, so either can merge first.

## Current status

Acceptance run and recorded. **No product code changed.** One defect found (D1 below) that needs an **owner decision**
before any fix, because every fix option changes behaviour the owner approved on 2026-10-11 (Sizing option (a)). Four
cases could not run for lack of an input (listed with the exact input). Open, not merged; CI and owner review next.

## Coverage matrix (reconciled)

Builds under test: SAM `c080dd08`, SAM_Systems `c0dc428`, SAM_Tas `28b59fcb`, SAM_UI `18d59010` (= `origin/sow/2026-Q4`
after SAM_Tas#93/#94 and SAM_UI#220/#222), rebuilt in that order on this workstation (Tas 2.0.0.1, 2025-06-04); GenOpt
352/352 and UI 2795/2795 on those builds, the test bins loading the rebuilt DLLs (hashes in the evidence). "Before" is
the earlier evidence and the commits it tested; "now" is this run. Inputs are copies; originals' hashes unchanged.

| # | Requirement (exact behaviour) | Before (evidence, commits) | Now | Status |
|---|---|---|---|---|
| 1 | Target effect, **Demo** (Systems Demo: TBD `bf3c0c7d`, TSD `40f169af`, TPD `c7178b65`) | PR7b-1 smoke (`581853a8`, other workstation): one point per block; PR8 A/B/C/F (SAM_UI#219) | heating 18/20/22 Â°C â†’ 10 971.6 / 15 227.9 / 20 909.5 kWh; cooling 24â†’26 Â°C â†’ 2 978.5 â†’ 1 644.4 kWh, 0 â†’ 22 h > 28 Â°C; controller 3 â†’ â’5 â†’ plant energy 27 387.79 â†’ 26 358.80 kWh; 5 glazing options | **covered** |
| 2 | Target effect, **SAM model** | cooling 24-h profile (PR7b, PR8 E, PR9 S1, Sizing S1); glazing (PR8 D, PR9 S2, Sizing S2); heating never (model had heating off) | heating on two SAM models whose authors set heating (below, row 3); cooling on `ModelA.sam` (Cell 1, own 25 Â°C profile): 24.3 â†’ 27.9 Â°C gives 657.1 â†’ 365.3 kWh, monotonic | **covered**; controller on a SAM model **not run** (no SAM model here has a TPD) |
| 3 | **Heating on a SAM model with heating on** | untested | H1 `SAM_zoningAM-test.sam` (`04ef4798`, owner's TM59 variant, Bathroom_2 16 Â°C): demand â‰ 0 (0 / 0 / 0.0248 kWh at 14.9/15.5/16.5 Â°C), journey PASS but weak. H2 `ModelA.sam` (`eac750d1`, committed SolarCalculator fixture, Cell 1 21 Â°C 07â€“19, setback 16): monotonic 6 263 â†’ 11 899 kWh over 18.3â€“21.7 Â°C; **Sizing off: Apply + Energy Simulation 2 = best within 1.9e-15**; **Sizing on: D1** | **covered, defect D1** |
| 4 | **Measures vs SAM_Tas' readers on the evaluation folders** | transitive only (through PR7a numbers) | 14 evaluation folders: heating, cooling (reader Wh Ă· 1000), overheating 25/28 (`Query.Overheating`, worst occupied zone) and plant energy/CO2 (`SystemEnergyCentreResults`, re-simulated and saved copy) â€” all PASS, â‰¤ 3.5e-15 relative, hours exact | **covered** (cost: see F1) |
| 5 | **4â€“5 glazing choices**; larger "My glazing systems" pool | PR8 C (Demo, 5) and D (SAM, 4), 13-system pool on the other workstation | G5 Demo, 5 options (default library, widened filter), each pane read back at the catalogue's g/U/light, readers PASS; DR-S2 on the direct route | 4â€“5 **covered**; larger pool **not run** (no user library here) |
| 6 | **Hookeâ€“Jeeves with setpoints and a plant controller** | PR8 B: two setpoints, no controller | Demo heating + cooling + HeatPumpController, min plant energy: Success, 38 simulations, 725 s; best (18.4375, 28, â’5), 27 387.84 â†’ 25 635.29 kWh; every evaluation building then plant; readers PASS on the best | **covered** |
| 7 | **Repeatability, durations, plant shutdown** | PR8 repeats A/C/E; PR9 S1Ă—3, D1Ă—2 (same session) | in-session repeats bit-identical (B1, P1); other workstation: plant bit-identical, building â‰ 4e-6 relative (F2). Durations: Demo building test 9.5â€“11.6 s, plant test 13â€“15 s, HJ mean 16.0 s (building 5.0, plant 7.7); SAM model 6.9â€“8.3 s; Energy Simulation 29â€“40 s. TPD.exe shutdown crash (0xc0000005, Application log) 9 times in 73 TPD sessions, 4 of 39 in HJ: **0 failed evaluations, 0 retries, 0 Error.txt**; no Tas process left after any step | **covered** |
| 8 | **Multi-plant-room TPD** | unproven (PR7b-1 decision 2) | all 9 TPDs on this workstation have one plant room (probe) | **not run** â€” input missing |
| 9 | PR9: **internal glazing between spaces** | not run (PR9 decision 15) | none of 20 local SAM models (incl. repository fixtures) has an aperture on a panel between two spaces | **not run** â€” input missing |
| 10 | PR9: **direct export route through optimisation, Apply, Energy Simulation** | the route itself validated (SAM_Tas#84); Apply on it never | DR (`SAM_zoningAM.sam`, DirectT3D on, Sizing on; no gbXML written, direct pipeline in the timings): S1 setpoint Apply bit-exact, ES 2 = best within 6.8e-16; S2 glazing: the direct-route TBD passed the pane/frame/placement checks, applied, overheating 19 h = 19 h | **covered** |

## D1 â€” defect: with Sizing on, setpoint results do not survive Energy Simulation after Apply (owner decision)

**Reproduced** (H2 heating, C1 cooling; `ModelA.sam`; Energy Simulation dialog defaults, Sizing on):

| Case | Best simulation of the run | Energy Simulation 2 after Apply | Same case, Sizing off |
|---|---|---|---|
| H2 Cell 1 heating 18â€“24 Â°C | 18.334 Â°C â†’ **6 263.4 kWh** | **11 393.8 kWh** (+82%) | 101 833.6 vs 101 833.6 kWh (1.9e-15) |
| C1 Cell 1 cooling 22â€“28 Â°C | 27.872 Â°C â†’ **365.3 kWh** | **521.8 kWh** (+43%) | â€” |

Apply itself is exact in both (TBD hours = (float)best, setback kept; SAM profile bit-exact; sizing copies untouched).

**Cause, measured** (driver `zones`, copies): Sizing leaves every zone with **fixed capacities** (`sizeHeating`/`sizeCooling`
= no sizing, `maxHeatingLoad`/`maxCoolingLoad` set) sized on the design days for the setpoints of that moment. Each
evaluation edits the setpoint but keeps those capacities; Energy Simulation after Apply sizes again for the new setpoint,
and the sizing of neighbouring zones is coupled:

| TBD | Cell 1 heating / cooling capacity | Cell 2 heating / cooling capacity |
|---|---|---|
| ES 1 (21 Â°C / 25 Â°C) | 1 586.5 W / 2 947.1 W | 1 525.4 W / 2 671.6 W |
| ES 2 after H2 (Cell 1 heating 18.33) | 1 416.6 W / 2 947.1 W | **3 076.4 W** / 2 671.6 W |
| ES 2 after C1 (Cell 1 cooling 27.87) | 1 586.5 W / **588.5 W** | 1 525.4 W / **5 452.5 W** |

Controls: the evaluation script reproduces Energy Simulation exactly on unchanged files (ES 1 TBD at 21 Â°C: 11 615.7322635474
= ES 1's TSD; ES 2 TBD at 18.33 Â°C = ES 2's TSD), and the readers agree with every evaluation. So the generated script and
the readers are right; the optimisation evaluates a different design problem (capacities frozen) from the one Energy
Simulation then solves (capacities re-sized). Where capacities bind, the optimiser can be rewarded for unmet load. PR9's
note "the heating design-day condition follows a heating setpoint at the next Energy Simulationâ€¦ annual results are
unaffected" held for its Sizing-off runs only; with Sizing on it is false. Sizing S1 (`SAM_zoningAM.sam`, cooling) held
because its capacities do not bind. Glazing on a sized model with active heating/cooling is expected to behave the same
way (loads change the sizing) but was **not measured**.

**Options (owner):**

- (a) **Refuse setpoint targets on a sized TBD** (zones with Sizing's fixed capacities), saying why and that Energy
  Simulation with Sizing off makes them available. Safe and small; it removes setpoint optimisation from default folders,
  including cases that hold (Sizing S1).
- (b) **Re-size in every evaluation** as Energy Simulation does (the SAM_Tas sizing chain, with the model's oversizing
  factors). Faithful; a new evaluation stage (design-day sizing per evaluation, longer runs), and the generated script
  cannot call SAM_Tas, so it needs an architecture change.
- (c) **Keep and warn**: a âš  check line, the AI text and the Apply plan/outcome say capacities are fixed during the run and
  re-sized by Energy Simulation; compare after Energy Simulation.
- (d) Remove the capacities for the evaluations: then it matches neither Energy Simulation with Sizing on nor off.

**Recommendation:** (a) now as a small PR (with a COM-free regression test on an inventory that reports fixed capacities,
and glazing measured first to decide whether it joins the refusal); (b) later only if sized setpoint studies are wanted.
No code was changed here because each option reverses part of an approved decision.

## Other findings (no change made)

- **F1 Cost unit is the workstation's, not the file's.** The same Demo TPD (`c7178b65`) reports its cost unit as `ÂŁ` on the
  PR7b workstation and `$` (U+0024) here, so PR7b decision 7 ("cost only in ÂŁ") correctly does not offer Annual plant cost
  on this workstation; the values are the same numbers. Plant cost was therefore not compared with the readers here.
- **F2 Building results differ between workstations by â‰ 4e-6 relative** (Demo baseline heating 15 227.8941594962 here vs
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
- **Controller target on a SAM model:** a SAM model's folder after Energy Simulation with "Create TPD".

## Files changed

- `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7B2.md` (this record).
- `SAM_Tas/spikes/optimisation-pr7b2/` (driver, definitions, scripts, README) â€” evidence tooling, not in `SAM_Tas.sln`.

## Validation

- Rebuild in order: SAM, SAM_Systems, SAM_Tas, SAM_UI Release, 0 errors each (profile redirected, real `NUGET_PACKAGES`).
- `SAM.Analytical.Tas.GenOpt.Tests` **352/352**; test-bin `SAM.Analytical.Tas.GenOpt.dll` = `build\` (`D523E245â€¦`).
- `SAM.Analytical.UI.WPF.Tests` **2795/2795** with the companion harness change.
- Licensed: 45 driver steps, 9 TPD probes and 5 harness runs (H1, H2, H2 Sizing off, C1, DR = S1+S2), one Tas operation at a time, each bounded (driver 300â€“3000 s, harness
  40â€“60 min, run watchdog 30 min); no timeout hit; no Tas process left after any step; `HKCU\Software\EDSL` exported before,
  restored after every harness run and at the end (byte-identical to the first export).
- The committed driver builds from its folder (relative root); `git diff --check` clean.
- Evidence (local, not committed): `C:\TasOut\pr7b-2` â€” `EXPECTATIONS.md` (written before the runs, with the cost
  deviation), `steps.log`, `logs\`, `runs\`, `ui\`, `investigate\`, `plant-check\`, `inputs\tpd-probe\`, `inspect\`,
  `build\`, `reg\`.

## Next step

1. Owner review of this record and **a decision on D1** (options above).
2. Merge this PR and the SAM_UI companion (independent; merge commits with `--match-head-commit`), then the
   `PROJECT_PROGRESS.md` closeouts on both `sow/2026-Q4` branches.
3. Then the D1 fix PR as decided, and the owner's order: Tas units correction, SAM_Deploy PR10. Provide the missing inputs
   above to close rows 2 (controller), 5 (pool), 8 and 9.
