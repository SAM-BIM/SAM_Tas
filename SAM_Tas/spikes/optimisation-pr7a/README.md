<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR7a spike files (evidence only)

These files back the record `../../SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7A_SPIKE.md`. **Nothing here is
product code.** No project in `SAM_Tas.sln` compiles this folder: the TasGenExecute scripts are `.csx` (an SDK project
only globs `*.cs`), and the probe has its own `.csproj` that is not in the solution and is built by hand.

| File | What it is |
|---|---|
| `chain.csx` | The whole `tas-model` chain in one TasGenExecute script (C# 7.0): copy the workspace files into the evaluation folder; edit the TBD (internal condition heating/cooling setpoint, glazing g-value); simulate TBD → TSD; read heating/cooling demand and overheating from the TSD; point the TPD at that TSD, set a controller setpoint, simulate the plant and read annual energy, cost and CO2. `{{NAME}}` placeholders stand for the model item names a generator would write in. |
| `compiler/*.csx` | One-feature scripts that find what TasGenExecute's compiler accepts (language level, references, `#r`). |
| `Invoke-Pr7aEvaluation.ps1` | Runs one evaluation the way the native evaluator does (Gate T protocol): `TasGenExecute.exe "<workspace>"` with a fresh evaluation folder holding `Variables.txt` as working directory. Refuses to start while Tas is busy, kills the tree on timeout, waits for the Tas COM servers to exit, reports file locks, and appends a summary line to `runs.jsonl`. |
| `probe/` | `Pr7aProbe`, a small console that reads Tas files the way SAM_Tas does (`inventory`, `readers`) and explores the glazing g-value (`glazing`). |

## Rerunning

Licensed Tas, Tas closed, one simulation at a time. Work on **copies** of the Tas files only.

1. Copy a Tas project (TBD, TSD, TPD, T3D) to a scratch folder, for example `C:\TasOut\pr7a\base`.
2. Build the probe against a sibling checkout's `SAM`, `SAM_Systems` and `SAM_Tas` build folders (built first):

   ```powershell
   dotnet build probe\Pr7aProbe.csproj -c Release -o C:\TasOut\pr7a\probe-bin
   ```

   (Copy the `probe` folder elsewhere and pass `-p:Root=<folder holding SAM, SAM_Systems, SAM_Tas>` to keep build
   output out of the repository.)
3. `Pr7aProbe inventory <folder>` lists internal conditions, thermostat profiles, constructions, controllers.
4. Run an evaluation, for example the whole chain with a heating setpoint of 22 °C:

   ```powershell
   $sub = @{ TBD='<name>.tbd'; TSD='<name>.tsd'; TPD='<name>.tpd'; IC='<internal condition>'; GLAZING='<glazing construction>';
             PANE_INDEX='0'; PLANTROOM='<plant room>'; CONTROLLER='<controller>'; THRESHOLD='28'; OBJECTIVE='HeatingDemand' }
   .\Invoke-Pr7aEvaluation.ps1 -Script .\chain.csx -Tag heat-22 -Source C:\TasOut\pr7a\base -Substitute $sub `
       -Variables @('Mode=3', 'HeatingSetpoint=22')
   ```

5. `Pr7aProbe readers <eval>\model.tsd <eval>\model.tpd [threshold]` prints what SAM_Tas' readers report for the same
   files, to compare with the evaluation's `Output.txt`.

Do not commit Tas models, EDSL scripts or run output; keep them in the scratch folder.
