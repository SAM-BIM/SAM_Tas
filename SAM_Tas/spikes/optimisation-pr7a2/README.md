<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR7a-2 spike files (glazing swap, evidence only)

These files back the record `../../SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7A2_GLAZING.md`. **Nothing here is
product code.** No project in `SAM_Tas.sln` compiles this folder: the TasGenExecute script is `.csx`, and the probe
has its own `.csproj` that is not in the solution and is built by hand. PR7a's files (`../optimisation-pr7a/`) are
reused unchanged: its driver `Invoke-Pr7aEvaluation.ps1` runs every evaluation here, and its probe's `inventory` and
`readers` commands still apply.

| File | What it is |
|---|---|
| `swap.csx` | One TasGenExecute script (C# 7.0, TBD interop only) that swaps a whole glazing system: every building element using the target glazing construction, and the frame element paired with each by name, are given a candidate's `-pane` / `-frame` constructions (`buildingElement.AssignConstruction`), read back, simulated, and the building results read. The candidate is chosen by `GlazingOption` (1..n, PR6's options) or by `GlazingG` (the closest g). `{{OPTIONS}}` is the catalogue a generator writes in. |
| `probe/` | `Pr7a2Probe`: `pool` builds the glazing pools as the SAM UI Glazing window does and calculates them with SAM_Tas' `ThermalTransmittanceCalculator.CalculateGlazing` (Tas TCD); `write` writes chosen systems into a TBD copy with SAM_Tas' `Modify.UpdateConstructions(building, apertureConstructions, materialLibrary)` and compares the TBD's g with the TCD's, layer by layer; `constructions` lists a TBD's constructions past the `GetConstruction(i)` null gaps. |

## Rerunning

Licensed Tas, Tas closed, one simulation at a time, **from PowerShell** (TCD needs an STA thread, and a mis-quoted
path leaves TCD.exe waiting on a "File not found." box). Work on **copies** of the Tas files only.

1. Build the probe outside the repository against a sibling checkout's `SAM`, `SAM_Systems` and `SAM_Tas` build
   folders (built first):

   ```powershell
   Copy-Item -Recurse probe C:\TasOut\pr7a2\probe-src
   dotnet build C:\TasOut\pr7a2\probe-src\Pr7a2Probe.csproj -c Release -o C:\TasOut\pr7a2\probe-bin -p:Root=<folder holding SAM, SAM_Systems, SAM_Tas>
   ```

2. The pool (sources in SAM UI order; `--loaded` is SAM UI's JSON cache of a `.tcd` read by "Load more glazing…",
   under `%LOCALAPPDATA%\SAM\cache\tcd`):

   ```powershell
   Pr7a2Probe pool pool.tsv --model <model.sam> --library --user "<Documents>\SAM\User Libraries\Glazing Systems.json" --loaded <cache.json>
   ```

3. Parity and the workspace: copy the project's TBD, then write the candidates into it under unique names and save:

   ```powershell
   Pr7a2Probe write <copy.tbd> <sources> --guids <g1,g2,...> --rename --save
   ```

   Without `--rename`, a candidate that shares its name with a system already in the TBD **overwrites** it (record,
   question 3).

4. Write the option table (one `new GlazingOption(label, pane, frame, g, U, light),` line per option, ordered by g)
   and run evaluations with PR7a's driver, for example option 3:

   ```powershell
   $sub = @{ TBD='<name>.tbd'; GLAZING='<current glazing construction>'; THRESHOLD='25'; OBJECTIVE='CoolingDemand';
             OPTIONS=[IO.File]::ReadAllText('options.txt') }
   ..\optimisation-pr7a\Invoke-Pr7aEvaluation.ps1 -Script .\swap.csx -Tag opt-3 -Source <workspace folder> `
       -Root C:\TasOut\pr7a2\runs -Substitute $sub -Variables @('GlazingOption=3')
   ```

Do not commit Tas models, EDSL construction databases, their system names or run output; keep them in the scratch
folder.
