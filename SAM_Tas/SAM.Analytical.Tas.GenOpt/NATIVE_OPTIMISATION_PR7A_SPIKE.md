<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR7a: licensed spike for the `tas-model` blocks (evidence only)

Branch `spike/optimisation-tas-model-blocks` → base `sow/2026-Q4` (cut from `861d3e75`). Record date: 2026-10-08.

Plan of record: SAM_UI `documentation/NativeOptimisation-Plan-ModelBindings.md` (PR7a row, risks R1–R4, decisions D1/D2,
"Follow-up: construction and glazing choice"). Schema served: PR6, SAM#188 (merge `6e5727f6`). Evaluation route: PR4
(`NATIVE_OPTIMISATION_DEFINITION_PR4.md`) and the Gate T protocol (`TASGENEXECUTE_PROTOCOL.md`).

## Current status

PR open, **not merged**. Evidence complete; awaiting PR CI and **owner review**. The owner decides PR7b's blocks from
this record. Nothing here is product code, nothing is wired into `RunNative`, and PR7b is not started.

## What was done

All runs: licensed Tas on this workstation, one simulation at a time, through `TasGenExecute.exe "<workspace>"` with a
fresh evaluation folder as working directory (Gate T), driven by `spikes/optimisation-pr7a/Invoke-Pr7aEvaluation.ps1`.
Inputs: **copies** of the Systems Demo (`Systems Training` T3D/TBD/TSD/TPD, script SHA-256 `d68a7e2e…`), verified equal
to the PR5 parity anchor before use; the anchor folder was only read. A SAM-generated model (a Part O dwelling
project, 10 zones, no TPD) was used for the internal-condition questions. Raw evidence (77 evaluation folders,
`runs.jsonl`, traces, reader output, registry exports) stays local in `C:\TasOut\pr7a\`.

The spike's scripts: `chain.csx` (the whole chain in one TasGenExecute script), `compiler/*.csx` (21 one-feature
probes) and the reader/inventory probe `probe/` (reads the same files the way SAM_Tas does). See
`spikes/optimisation-pr7a/README.md`.

## Answers

### 1. One script, whole chain: **yes**

One TasGenExecute script edits the TBD, runs the building simulation, reads the TSD, runs the plant simulation on that
TSD and writes the outputs (`chain.csx`, `Mode = 3`). Order and calls (the order is the one that left no lock and no
process behind in any run):

| Stage | COM calls | Notes |
|---|---|---|
| Copy | `File.Copy` of the workspace's TBD (and TSD when there is no building simulation, TPD when there is a plant simulation) into the evaluation folder | The workspace snapshot is never written. Files are found with `TasFiles.getFiles(TasExtension.X)["<name>"]`. |
| TBD edit | `new TBD.TBDDocument()`, `open(tbd)`, `Building.GetIC(i)` / `GetConstructionByName`, thermostat `GetProfile(ticLL / ticUL)`, `material.solarTransmittance` | Read-back of every written value is reported as an output. |
| Building simulation | `save()`, `simulate(1, 365, 0, 1, 0, 0, tsd, 1, 0)`, `save()`, `close()`, `Marshal.FinalReleaseComObject` | Same arguments as SAM_Tas `Modify.Simulate`. `simulate` returns synchronously: the TSD was unlocked within 0.1 ms in every run. |
| TSD read | `new TSD.TSDDocument()`, `openReadOnly(tsd)`, `GetAnnualBuildingResult`, `GetZoneData(z).GetDailyZoneResult(day, …)`, `close()`, `FinalReleaseComObject` | **The daily arrays are 1-based SAFEARRAYs** (`GetValue(0)` throws "Index was outside the bounds"); enumerate them, as SAM_Tas does. Close the TSD before the TPD opens it. |
| TSD building path (plant only) | `TSD.open(tsd)`, `SimulationData.buildingPath = <evaluation TBD>`, `save()`, `close()` | Needed when there is no building simulation; see finding F1. 0.22 s. |
| Plant | `new TPD.TPDDoc()`, `Open(tpd)`, `EnergyCentre.GetTSDData(1).FixTSDPath(tsd)`, plant room / controller by name, `Setpoint = …`, `PlantRoom.SimulateEx(1, 8760, 0, ExternalPollutant.Value, 10, Load + Pipe, 0, 0)` | Exactly the Systems Demo's simulation call. |
| Plant results | `GetResultSet(Annual, 0, 0, 0, null)`, `GetVectorSize` / `GetResultItem(type, i).GetValues()[0]` summed, `Dispose()` | 0-based arrays here (as the Demo). |
| TPD close | `Save()` (Demo order; optional: 0.12 s with it, 0.005 s without), `FinalReleaseComObject` (the Demo has no `Close`) | See finding F2. |

File locks and processes: after every successful evaluation no TBD/TSD/TPD in the evaluation folder was locked and no
Tas process was left; TasGenExecute's COM servers exited within 0.3 s. A script exception (unknown internal condition,
unreachable g-value) also left no lock and no process: the servers exit when TasGenExecute exits, even with a document
still open. The TasManager registry export was identical (hash `695CCC5E…`) before and after every batch.

**Findings that change the blocks:**

- **F1. `FixTSDPath` fails when the TBD named inside the TSD does not exist.** `TSDData.FixTSDPath` throws
  `RPC_E_SERVERFAULT` when `SimulationData.buildingPath` (an absolute path stored in the TSD) points at a missing TBD.
  The Systems Demo's TSD names a folder of the other workstation, so the unchanged Demo script fails on this workstation
  (3/3); with that TBD hidden, a TSD that ran before fails the same way (1/1). Re-pointing `buildingPath` at the
  evaluation's own TBD copy first fixes it (19/19) and reproduces the PR3/PR5 acceptance value exactly (below). Any
  project moved or copied after its last building simulation (a SAM model folder, the run's workspace snapshot) is
  affected. **PR7b: a plant evaluation without a building simulation must re-point `buildingPath` first.**
- **F2. TPD.exe intermittently crashes when it shuts down** (Application Error, access violation `0xc0000005`, then
  `0xc000041d`, in `TPD.exe` 2.0.0.1): **6 of 36 plant evaluations** (17 %), including 1 of 5 unchanged Demo-script
  runs and 2 of 10 runs that skipped `Save()`, so the save is not the cause. It happens after the results were read and
  after the document was released: the release then took 3.9–5.5 s instead of 0.005–0.12 s, TasGenExecute still exited 0, and the
  outputs were complete and equal to the readers. The same crash followed SAM_Tas' own read-only TPD reader once. No
  dialog appeared. **PR7b: read every plant result before releasing the TPD, and do not treat the crash as a failure
  (it is invisible to the evaluator: exit 0, no `Error.txt`); allow ~5 s more per plant evaluation in the estimate.**
- **F3. Names are exact.** One Demo internal condition is `"Steady State Heating "` (trailing space). Blocks match
  ordinal, untrimmed, and require exactly one match (an unknown name fails with "found 0 times").

### 2. Duration (R2)

Systems Demo, 5 runs each, interleaved; wall time = the whole TasGenExecute process (compilation included).

| Evaluation | Wall time (s) mean ± sd | Simulation | TSD read | Other |
|---|---|---|---|---|
| Building only (TBD edit → TSD → measures) | **11.5 ± 0.3** (11.3–12.1) | 4.0–4.5 s | 4.5–4.7 s | compile/start ≈ 2.5 s |
| Plant only, chain block (TSD re-pointed) | **23.9 ± 0.4** (23.5–24.6) | 20.3–21.3 s | — | |
| Plant only, the Demo script itself | **24.1 ± 2.1** (22.8–28.2; 28.2 is a run with the F2 crash) | | | |
| Both | **32.2 ± 0.2** (31.9–32.5) | 4.0 + 20.4 s | 4.6 s | |

Repeats were **bit-identical** (all outputs, all 5 repeats of each kind, and the g-value repeat): the evaluation noise
floor is 0. On the SAM-generated model a building-only evaluation took 8.3–8.5 s. The TSD read (three zone arrays,
day by day, all zones) costs more than this small building's simulation; PR7b should read only the arrays the bound
measures need. The plan's "20 s per building evaluation" is pessimistic for the Demo; a real building will be
dominated by its simulation, which the test simulation (PR8) measures.

### 3. Each V1 target changes the result in the expected direction: **yes**

Systems Demo, internal condition `"Office Weekday"` (17 office zones, weekdays only), plant room `"Plant Room"`,
controller `"HeatPumpController"`:

| Target | Value | Heating demand (kWh) | Cooling demand (kWh) | Overheating >28 °C (h) | Plant energy (kWh) | Plant cost (GBP) | Plant CO2 (kg) |
|---|---|---|---|---|---|---|---|
| Heating setpoint | 18 | 10 971.60 | 2 964.18 | 0 | 27 172.73 | 7 332.48 | 3 894.43 |
| (unchanged) | 20 | 15 227.84 | 2 978.53 | 0 | 27 387.79 | 7 363.17 | 3 932.89 |
| Heating setpoint | 22 | 20 909.46 | 3 042.89 | 0 | 28 109.17 | 7 521.26 | 4 050.08 |
| Cooling setpoint | 22 | 15 299.33 | 4 999.73 | 0 | 28 010.85 | 7 530.77 | 4 004.98 |
| (unchanged) | 24 | 15 227.84 | 2 978.53 | 0 | 27 387.79 | 7 363.17 | 3 932.89 |
| Cooling setpoint | 26 | 15 218.18 | 1 644.41 | 22 | 27 025.65 | 7 265.75 | 3 891.96 |
| Cooling setpoint | 30 | 15 215.92 | 469.85 | 162 | 26 682.41 | 7 173.42 | 3 853.45 |
| Controller setpoint (plant only) | −5 | | | | 26 358.83 | 7 390.53 | 3 685.88 |
| Controller setpoint (plant only) | 3 (unchanged) | | | | 27 387.84 | 7 363.17 | 3 932.90 |
| Controller setpoint (plant only) | 35 | | | | 32 002.85 | 7 436.63 | 5 004.57 |

**Controller parity with the PR3/PR5 acceptance:** the chain's plant block at Setpoint 4.968943799848584 on the
original files gives cost **7360.04370117188**, bit-identical to the Java/native GenOpt acceptance best point. The
reference is plant room `"Plant Room"` (PR6's fixture says `"Plant Room 1"`), controller `"HeatPumpController"`
(`tpdControlOutdoor`, `tpdTempSensor`, current setpoint 3; its unit is °C because the sensor is a temperature sensor;
the other Demo controllers are named "Controller 2" … "Controller 6" and include a load sensor).

**Where the zone setpoint lives.** A TBD internal condition's thermostat: heating = lower-limit profile `ticLL`,
cooling = upper-limit profile `ticUL`. An internal condition applies to its day types only (the Demo has a weekday, a
weekend and a heating-design-day condition per zone). Two shapes were met:

| Model | Profile | Current value for the catalogue | What the block writes | Licensed proof |
|---|---|---|---|---|
| Systems Demo (EDSL) | `ticValueProfile`, factor 1, `value` 20/24, `setbackValue` 12/150 outside a schedule ("8am to 6pm") | `value` | `value`; setback and schedule kept | table above |
| SAM-generated | `ticHourlyProfile`, factor 1, 24 values, e.g. 21 for 07–19 h and 16 otherwise | heating: the highest hour; cooling: the lowest hour | those hours (12 of 24); the other (setback) hours kept | heating 21→23 rewrote 12 h, read back; cooling 23→26: 2 523.75 → 1 123.32 kWh |

The SAM-generated model has **one internal condition per space**, named after the space, with the description
`"<SAM internal condition> - <space>"`, plus a `"<space> - HDD"` condition for the heating design day; 150 and −50 mean
"no cooling" / "no heating". Its heating is off (Part O model), so its heating demand was 0 in every run; the
heating direction is shown on the Demo. `InternalCondition.GetLowerLimit/GetUpperLimit` follow the written values and
are not a substitute for the profile.

### 4. Glazing g-value (R1): **exact and repeatable, by solving one pane's solar transmittance**

`Construction.GetGlazingValues()` returns 9 values: light transmittance, light reflectance, direct solar
transmittance, solar reflectance, solar absorptance, **total solar energy transmittance g (index 5)**, and the short-wave,
long-wave and total shading coefficients (`T/0.87`, `(g−T)/0.87`, `g/0.87`). It follows a layer change immediately, in
memory, without a save. The Demo's glazed elements use `"Suncool Example"` (g 0.3367; two panes and an argon gap).

- **Which layer.** Scanning each pane's solar transmittance from 0 to `1 − max(reflectance)`: the solar-control pane
  spans **g 0.037–0.482**, the clear pane only 0.300–0.344. Rule for PR7b: solve the pane with the widest g span; the
  catalogue's suggested range is that span.
- **How.** Bisection on that pane's `solarTransmittance` until `|g − target| ≤ 1e-5`: 10–13 steps, sub-millisecond each.
  The same target gives the bit-identical `solarTransmittance` and g on repeat, and the simulation repeated bit for bit.
  An unreachable target (0.6) is refused with the reachable maximum.
- **Effect** (building only): g 0.20 / 0.337 / 0.45 → cooling 2 334.11 / 2 978.53 / 3 661.19 kWh, heating
  16 617.44 / 15 227.84 / 14 140.72 kWh, overheating 0 / 0 / 14 h.
- **TBD's own search** `Construction.UValueSearch(flow, targetU, targetG, material, tcdSolarTransmittance, standard)`
  also reaches the target (code 1, |error| 3e-5, or no change with code 0 for one flag combination); its flags are
  undocumented. Not recommended over the bisection.
- **What it does not do.** Light transmittance, reflectances and emissivities are left unchanged, so the result is
  one of many glass combinations with that g; the window should say "g-value (solar transmittance of the coated pane
  adjusted)".
- **Catalogue trap.** `Building.GetConstruction(i)` has null gaps: `"Suncool Example"` comes after a null slot (indices 0–199 hold 20 constructions),
  and SAM_Tas' `Query.Constructions` (stop at the first null) never reaches it. List glazing constructions from the
  building elements (`GetBuildingElement(i).GetConstruction()`, transparent), or scan past nulls. Two transparent
  constructions in the Demo are used by no element.

**Recommendation:** keep the continuous target `tbd.glazing-construction.g-value` for V1, implemented as above (exact
to 1e-5, deterministic), and keep the owner's glazing choice as the better long-term answer (it swaps real glass).

### 5. Each V1 measure equals SAM_Tas' own readers: **yes**

For 9 evaluations (Demo baseline, heating 18/22, cooling 22/26/30, threshold 25 at baseline and at cooling 30, SAM
model cooling 26) the probe read the evaluation's own TSD/TPD with SAM_Tas' code; every value is equal to the 15
significant digits TasGenExecute writes (largest relative difference 3.3e-15; counts exact).

| Measure | TSD/TPD field | Unit | SAM_Tas reader it equals |
|---|---|---|---|
| Annual heating demand | TSD `BuildingData.GetAnnualBuildingResult(tsdBuildingArray.heatingProfile)`, 8760 values summed | W each hour → sum in Wh, **÷ 1000 = kWh** | `Weather.Tas.Query.AnnualBuildingResult` summed, as `Convert.ToSAM_AnalyticalModelSimulationResult` (`ConsumptionHeating`) |
| Annual cooling demand | `tsdBuildingArray.coolingProfile`, same | kWh | same (`ConsumptionCooling`) |
| Overheating hours | zone `occupantSensibleGain` and `resultantTemp` (see 6) | h | `Analytical.Tas.Query.Overheating` (`OccupiedHours28`, `OccupiedHours25`), worst occupied zone |
| Plant energy | TPD annual result set, `tpdConsumption`, first value of every item summed (Heating, Cooling, Auxiliary, Equipment, Lighting) | "kW·h" | `Core.Tas.TPD.Query.SystemEnergyCentreResults(…, Annual, Consumption)` = `Modify.CopyResults` `AnnualTotalConsumption` |
| Plant cost | `tpdCost`, one item per fuel source | "£" | same (`Cost`, `AnnualCost`) |
| Plant CO2 | `tpdCo2`, one item per category | "kg" | same (`Co2`, `AnnualCO2Emission`) |

The building heating profile equals the sum of the zone `heatingLoad` series (checked to 2e-9). **SAM labels
`ConsumptionHeating`/`ConsumptionCooling` "[kWh]" but stores the Wh sum** (Demo: 15 227 840.66 "kWh"); the measure
divides by 1000. Plant energy includes unregulated equipment and lighting; the Demo script itself reports cost and CO2
only. Cost is in the TPD's currency (the Demo's fuel sources give "£").

### 6. Overheating (D1)

- **Occupied:** zone `tsdZoneArray.occupantSensibleGain > 0` in that hour. **Room temperature:** zone
  `tsdZoneArray.resultantTemp` (°C). Read day by day for days 1–365 (8 760 h).
- **Count:** occupied hours with resultant temperature **strictly above** the threshold (default 28 °C), per zone;
  zones with no occupied hour are not rooms that count; the measure is the **worst occupied zone's** count.
- **Cross-check:** equal to SAM_Tas `Query.Overheating` on every run (Demo: 0 h at 28 °C until cooling 26 °C → 22 h, 30
  °C → 162 h; at 25 °C: 274 h baseline, 394 h at cooling 30 °C; SAM model: 32–33 h in a kitchen). Demo occupied hours:
  2 277 per occupied zone (24 of 26 zones); SAM model: 5 of 10 zones occupied.
- **How it differs from SAM's TM59/TM52 code** (`TMOverheatingCalculator`, wrapped by SAM_Tas `OverheatingCalculator`):
  the occupancy rule and the temperature are the same (occupancy gain > 0; resultant = TM59's operative temperature);
  but TM59 uses fixed criteria, not a free threshold (26 °C for mechanically ventilated homes and bedroom nights
  22:00–07:00; adaptive ΔT ≥ 1 K for natural ventilation), judges a **percentage** of occupied hours (3 %, or 32 night
  hours), and TM52 assesses May–September only (hours 2 880–6 528). D1 is a whole-year count against one threshold,
  for ranking designs, not a compliance verdict; that is why it is the worst room's hours and why a TM59-style measure
  stays a later addition.

### 7. Generated code in Tas (R4)

From TasGenExecute's IL and 21 licensed probes:

- **Compiler:** Roslyn scripting 2.4 (`Microsoft.CodeAnalysis.CSharp.Scripting`), `ScriptOptions.Default`, no language
  version set: **C# 7.0 exactly.** Out variables, tuples, local functions, pattern `is`, `nameof`, interpolation,
  top-level `await`, and class declarations work. C# 7.1+ fails (`default` literal: "CS8107 … not available in C# 7"),
  as do `in` parameters (7.2), tuple `==` (7.3) and `??=`/switch expressions (8.0).
- **Imports:** `System`, `System.IO`, `System.Collections.Generic`, `TasGenComm` only. **LINQ needs
  `using System.Linq;`** in the script.
- **References:** the default script references, `TasGenComm.dll`, `Microsoft.CSharp` (so `dynamic` works), and the Tas
  interops of the install: **TBD, TSD, TPD, TAS3D, TWD; not TCD** (a TCD type does not compile). `System.Xml` works;
  `System.Xml.Linq` needs `#r "System.Xml.Linq"`. **`#r "<absolute path>"` works**, even for a netstandard2.0 SAM
  assembly.
- **Globals:** `TasFiles`, `Variables` (`Dictionary<string, TasVariable>`: `VariableValue`, `MinValue`, `MaxValue`,
  `Step`), `ScriptOutput.SetValue(name, double)`. TasGenExecute appends `ScriptOutput.Instance` to the script and writes
  its `Name::value` lines in the process culture (setting a culture inside the script has no effect); `NaN` is written
  as `NaN`; 15 significant digits.
- **Errors:** compile errors and exceptions keep exit code 0 and go to `Error.txt` with a line number counted from the
  script's first line.
- Compilation and start cost about 2.5 s per evaluation.

**Rule for PR7b's blocks:** C# 7.0, `using System.Linq;` written out, only the BCL and the TBD/TSD/TPD interops, no `#r`
(a path to a SAM assembly would tie the generated script to one installation), every COM document closed and released
in the order of question 1.

### 8. Glazing swap (optional): **not done**

Only with the owner's agreement first; not asked in this session. Evidence that bears on it: TCD is not referenced in
TasGenExecute (a swap must use the TBD's own constructions: `Building.AddConstruction(copy)`,
`buildingElement.AssignConstruction`), and glazing constructions must be found past the `GetConstruction(i)` null gaps.

## PR7b proposal

Engine `tas-model`. Kinds and reference keys: **PR6's placeholders stand**, except the fixture's plant room name.

| Kind | Name in the window | Reference keys | Unit (quantity) | Parameters / suggested range | Needs |
|---|---|---|---|---|---|
| `tbd.internal-condition.heating-setpoint` | Zone heating setpoint | `internalCondition` | °C (temperature) | current value from `ticLL` (value profile: `value`; 24-hour profile: highest hour); range 16–24, below the cooling setpoint | building simulation |
| `tbd.internal-condition.cooling-setpoint` | Zone cooling setpoint | `internalCondition` | °C (temperature) | `ticUL` (value or lowest hour); range 21–28; not offered when the profile is the "no cooling" 150 | building simulation |
| `tbd.glazing-construction.g-value` | Glazing g-value | `glazingConstruction` | – (dimensionless) | current `GetGlazingValues()[5]`; range = the solved pane's reachable span | building simulation |
| `tpd.controller.setpoint` | Plant controller setpoint | `plantRoom`, `controller` | from the sensor: temperature sensor → °C; otherwise none (PR6 allows a null unit) | current `Setpoint`; range from the owner (Demo −5…35) | plant simulation |
| `tsd.annual-heating-demand` | Annual heating demand | — | kWh (energy) | — | building results |
| `tsd.annual-cooling-demand` | Annual cooling demand | — | kWh (energy) | — | building results |
| `tsd.overheating-hours` | Overheating hours | — | h (time) | `threshold`, default 28 °C, 20–40 °C | building results |
| `tpd.annual-energy` | Annual plant energy | — | kWh (energy) | — | plant simulation |
| `tpd.annual-cost` | Annual plant cost | — | GBP (currency) when the TPD's cost unit is "£"; otherwise not offered | — | plant simulation |
| `tpd.annual-co2` | Annual plant CO2 | — | kgCO2e (carbon; the TPD writes "kg", which OPT606 accepts) | — | plant simulation |

**Chain rule** (confirmed): any `tbd.*` target → building simulation (TBD → TSD); any `tpd.*` target or measure → plant
simulation on that TSD; `tsd.*` measures without a `tbd.*` target read the existing TSD (no simulation); a plant
evaluation without a building simulation re-points the TSD's `buildingPath` first (F1).

**Blocks** (each proven above): copy; internal-condition setpoint (value and 24-hour profiles, factor 1, else refuse);
g-value solve; building simulation; TSD measures (only the arrays needed); TSD re-point; TPD controller setpoint; plant
simulation (the Demo's `SimulateEx`); TPD measures; close. Write the controller setpoint as a `double` (the Demo casts
to `float`, which is why the read-back of 4.968943799848584 is 4.96894359588623).

**Changes to PR6 placeholders:** only the fixture `systems-demo-bound-golden-section.json`'s `"plantRoom": "Plant Room 1"`
→ `"Plant Room"` (test data, PR7b or later). No kind, key, unit or parameter range must change.

**Owner decisions for PR7b:**

1. **Grouping on SAM models.** A SAM-generated TBD has one internal condition per space. Offer them one by one (as
   now), or add a target for every internal condition sharing a SAM internal condition (the description before
   `" - "`)? Recommended: one by one in PR7b, grouping as a follow-up.
2. **F2.** Whether to report EDSL the TPD.exe shutdown crash (seen with the unchanged Systems Demo script too). The
   evaluation itself needs no change; saving the per-evaluation TPD is optional (only Apply best design needs a saved
   TPD).
3. Whether the window describes the g-value target as "coated pane solar transmittance adjusted" (recommended).

## Files changed

- `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7A_SPIKE.md` (this record).
- `SAM_Tas/spikes/optimisation-pr7a/`: `README.md`, `chain.csx`, `Invoke-Pr7aEvaluation.ps1`, `compiler/*.csx` (21),
  `probe/{Pr7aProbe.csproj, Program.cs, Glazing.cs}`. Not in `SAM_Tas.sln`; no product file changed.

## Validation

- 77 TasGenExecute evaluations (21 compiler probes, 56 on Tas models) plus probe reads (2026-10-08, this workstation, Tas TPD.exe 2.0.0.1). Summaries:
  `C:\TasOut\pr7a\runs\runs.jsonl`.
- Inputs verified by SHA-256 against the PR5 anchor before use; the anchor folder was not written.
- Registry (`HKCU\SOFTWARE\EDSL\TasManager`) export identical before and after every batch; no java, cmd or Tas process
  left by the spike.
- Probe built with `dotnet build` against the sibling SAM/SAM_Systems/SAM_Tas build folders (0 warnings, 0 errors).
- `git diff --check` clean; SPDX headers on every new `.cs`.

## Unresolved issues and risks

- **F1** affects any plant-only run of a moved project (also today's `tas-script` route with the Demo script). Worth a
  separate SAM_Tas note for the Systems Demo users.
- **F2** is EDSL's (TPD.exe); the cause inside `Save()` is unknown.
- **Out-of-scope SAM_Tas defects seen:** `Query.Constructions` stops at the first null (misses constructions);
  `ConsumptionHeating`/`ConsumptionCooling` hold Wh under a kWh label.
- The SAM-generated model had heating off and no TPD: heating and plant blocks are proven on the Demo only. PR8's
  acceptance on a SAM-generated model with a TPD covers them.
- Timings are for a 26-zone Demo; a real building's evaluation is dominated by its simulation (R2 stays).

## Next step

1. PR CI (`build`, `spdx`) green on the head.
2. **Owner review**: the PR7b proposal and the three decisions above. Merge (merge commit, `--match-head-commit`) only
   after approval.
3. `PROJECT_PROGRESS.md` closeout on SAM_Tas `sow/2026-Q4` with the merge SHA.
4. Then, only with the owner: PR7b (catalogue reader, script generator, engine `tas-model`).
