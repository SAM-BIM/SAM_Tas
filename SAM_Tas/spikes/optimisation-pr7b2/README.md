<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR7b-2 acceptance files (evidence only)

These files back the record `../../SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7B2.md`. **Nothing here is product
code.** No project in `SAM_Tas.sln` compiles this folder; the driver has its own `.csproj`, built by hand.

| File | What it is |
|---|---|
| `driver/` | `Pr7b2Driver` (net8.0-windows), the PR7b smoke driver extended. `catalogue`, `test`, `run` call `Query.TasModelInventory`, `Query.TasModelCatalogue`, `TasModelRunner.Test`/`Run` with the installed TasGenExecute. `readers` compares an evaluation folder's `Output.txt` with SAM_Tas' own readers on its `model.tsd` (`Convert.ToSAM_AnalyticalModelSimulationResult`: Wh, compared ÷ 1000; `Query.Overheating`: worst occupied zone, 25/28 °C). `plant` copies an evaluation's files, writes the controllers, runs the generated script's `SimulateEx` call for every plant room, **saves** and reads the saved TPD with SAM_Tas `SystemEnergyCentreResults(Annual)` (the evaluation TPD itself is never saved, PR7b decision 8). `sam` summarises a `.sam` (setpoints, internal/external apertures); `tpd` lists plant rooms, controllers and TSD links; `zones` lists each TBD zone's sizing flags and capacities. |
| `defs/` | The definitions run: `B-building` (Office Weekday heating/cooling setpoints, four measures), `P-plant` (HeatPumpController), `HJ-mixed` (both setpoints + the controller, Hooke–Jeeves), `G5-glazing` (five options), `X-heating-investigation` (Cell 1 heating setpoint, the Sizing finding). |
| `scripts/` | `build.ps1` (SAM → SAM_Systems → SAM_Tas → SAM_UI, Release, profile redirected), `step.ps1` (one driver call: bounded, lock against concurrency, Tas processes and Application-log crashes after), `tpd-probe.ps1`, `ui-case.ps1` (one SAM_UI `TasModelApplyAcceptanceHarness` run with a process monitor and the EDSL registry exported and restored). Paths are this workstation's (`C:\TasOut\pr7b-2`); change them for another. |

## Rerunning

Licensed Tas, Tas closed, one Tas operation at a time, **copies** of the Tas files only.

1. Rebuild the four repositories in order (`scripts/build.ps1`), then build the driver against them:
   `MSBuild driver\Pr7b2Driver.csproj -t:Restore` and `-t:Build -p:Configuration=Release` (`-p:Root=<folder holding SAM,
   SAM_Systems, SAM_Tas>` if this checkout is not in that folder).
2. Systems Demo cases (inputs: PR7a/PR8's Demo copy, TBD `bf3c0c7d…`, TSD `40f169af…`, TPD `c7178b65…`):
   `step.ps1 -Name B1 -DriverArgs test,<demo>,defs\B-building.json,20,24,<runs>`, then
   `step.ps1 -Name B1-readers -DriverArgs readers,<evaluation folder>,heating,cooling,oh28,oh25`; plant:
   `test … defs\P-plant.json 3 …` then `plant <evaluation> <work> "Plant Room|HeatPumpController=3" energy,co2`;
   `run … defs\HJ-mixed.json …`; `run … defs\G5-glazing.json … pool permissive`.
3. SAM-model journeys (Energy Simulation, Run, Apply, Energy Simulation): SAM_UI's opt-in
   `TasModelApplyAcceptanceHarness` through `scripts/ui-case.ps1` (cases `S1,S2` with `-Direct on`; `H1`/`C1` with
   `-Heating`/`-Cooling "space,min,max"` and `-Sizing on|off`).
