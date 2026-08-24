---
name: run-tas
description: Run a licensed EDSL Tas calculation headlessly over the normal Grasshopper route - AnalyticalModel -> ToGbXML -> WorkflowgbXML (WorkflowCalculator) - with sizing-only for a fast check or a full 1..365 simulation for a real run. Use whenever a task needs a real .tbd/.tsd produced from a .sam model, a licensed A/B gate, or any "does this actually run in Tas" question. Covers the environment traps that otherwise cost a session to rediscover.
---

# Run Tas headlessly (the Grasshopper route)

This is the same pipeline `SAMAnalytical.WorkflowgbXML` runs in Grasshopper, driven from a console
app so it works in CI, in an A/B gate, or from an agent session.

## Before anything else: three traps that waste whole sessions

1. **Close the Tas GUI.** If `TBD.exe`/`TSD.exe` is open as an application (the user working in Tas),
   COM calls block *silently* — the harness sits at near-zero CPU with **no dialog and no error**, and
   `ToSAM_WeatherDatas` returns empty. Check and clear first:
   ```bash
   powershell -NoProfile -Command "Get-Process -Name TBD,TSD,TAS3D,TPD,TWD,TCD -ErrorAction SilentlyContinue | Select Name,Id"
   ```
   Kill leftovers from a crashed run with `| Stop-Process -Force`. If the user has Tas open on purpose,
   **ask them to close it** — nothing else will make the run work.

2. **Pass every path as an ABSOLUTE path.** A relative path is resolved against the COM server's
   working directory, not yours. The signature failure is weather: `ToSAM_WeatherDatas("w.twd")`
   returns 0 years and the export dies with "no weather data read", while the identical file passed as
   `C:\P38\w.twd` reads fine. This applies to `.sam`, `.xml`, `.tbd`, `.tsd` and `.twd` alike.

3. **Keep output paths SHORT.** Write to `C:\TasOut`, never a nested
   `AppData\Local\Temp\...\scratchpad\...` path — a long path reproduces a modal
   "Fail to save to file" popup from `TBD.exe` followed by `RPC server is unavailable (0x800706BA)`.

## The two calls

```csharp
// 1. AnalyticalModel -> gbXML on disk
gbXMLSerializer.gbXML gbXML = analyticalModel.TogbXML(SAM.Core.Tolerance.MacroDistance, tolerance);
SAM.Core.gbXML.Create.gbXML(gbXML, path_gbXML);          // tolerance: 0.00001 is the GH default

// 2. gbXML -> T3D -> TBD -> sizing/simulation -> results written back onto the model
WorkflowSettings workflowSettings = new WorkflowSettings
{
    Path_gbXML   = path_gbXML,      // absolute
    Path_TBD     = path_TBD,        // absolute, short directory
    WeatherData  = weatherData,
    Sizing       = true,
    Simulate     = false,           // see modes below
    SimulateFrom = 1,
    SimulateTo   = 365,
    UpdateZones  = true,
    AddIZAMs     = true,
    UnmetHours   = false,
    RemoveExistingTBD = true,       // otherwise a stale .tbd is reused
};

WorkflowCalculator workflowCalculator = new WorkflowCalculator(workflowSettings);
AnalyticalModel result = workflowCalculator.Calculate(analyticalModel);
// workflowCalculator.Notes and .Timings carry the per-step log
```

**Use `WorkflowCalculator` directly, not `Modify.RunWorkflow`.** `RunWorkflow` lives in
`SAM.Analytical.Grasshopper.Tas` and opens a WPF progress window on its own UI thread — it cannot run
headless. `WorkflowCalculator` is the engine underneath it and takes the identical `WorkflowSettings`.

### Modes

| Mode | Settings | Use for |
| --- | --- | --- |
| **Fast check** (minutes) | `Sizing = true`, `Simulate = false` | "does the model run", design loads, geometry/IC sanity |
| **Full simulation** (much longer) | `Sizing = true`, `Simulate = true`, `SimulateFrom = 1`, `SimulateTo = 365` | hourly results, TM59, overheating, a real TSD |

Default to the fast check. Only run the full year when hourly results are actually needed, and say so
before starting — it is a long, blocking COM call that cannot be interrupted mid-step.

## Weather

`WeatherData` is required for anything simulatable; without it the TSD comes back ~22 bytes with
`result=True`.

```csharp
List<SAM.Weather.WeatherData> weatherDatas =
    SAM.Weather.Tas.Convert.ToSAM_WeatherDatas(@"C:\...\cibseweather2005.twd");
SAM.Weather.WeatherData weatherData = weatherDatas[0];   // [0] is Belfast TRY, 65 years in the file
```

Known-good libraries on this machine (nothing under the Tas install ships a `.twd`):

- `C:\Users\Public\Documents\Tas Data\Databases\CIBSE Weather 2021.twd`
- `C:\Users\michal.dengusiak\Documents\GitHub\Other\SAM_Legacy\4-MEPBridge\cibseweather2005.twd`

## Harness project

`net8.0-windows`, **never `net48`/`net481`** — on .NET Framework the first `SetValue` on any SAM object
throws `InvalidOperationException: Collection was modified` (a latent `SAM.Core` self-copy defect that
only Framework's `Dictionary` version counter trips). `UseWindowsForms` is needed too, or
`SAM.Analytical.Query.Color` throws `System.Drawing.Common is not supported on this platform`.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <NoWarn>$(NoWarn);CS0618;MSB3270;MSB3277</NoWarn>
    <SamLibs>C:\Users\michal.dengusiak\Documents\GitHub\SAM-BIM\SAM\build</SamLibs>
    <TasLibs>C:\Users\michal.dengusiak\Documents\GitHub\SAM-BIM\SAM_Tas\build</TasLibs>
    <GbXmlLibs>C:\Users\michal.dengusiak\Documents\GitHub\SAM-BIM\SAM_gbXML\build</GbXmlLibs>
  </PropertyGroup>
  <ItemGroup>
    <!-- SAM core -->
    <Reference Include="SAM.Core"><HintPath>$(SamLibs)\SAM.Core.dll</HintPath></Reference>
    <Reference Include="SAM.Geometry"><HintPath>$(SamLibs)\SAM.Geometry.dll</HintPath></Reference>
    <Reference Include="SAM.Architectural"><HintPath>$(SamLibs)\SAM.Architectural.dll</HintPath></Reference>
    <Reference Include="SAM.Analytical"><HintPath>$(SamLibs)\SAM.Analytical.dll</HintPath></Reference>
    <Reference Include="SAM.Weather"><HintPath>$(SamLibs)\SAM.Weather.dll</HintPath></Reference>
    <!-- Tas -->
    <Reference Include="SAM.Core.Tas"><HintPath>$(TasLibs)\SAM.Core.Tas.dll</HintPath></Reference>
    <Reference Include="SAM.Geometry.Tas"><HintPath>$(TasLibs)\SAM.Geometry.Tas.dll</HintPath></Reference>
    <Reference Include="SAM.Weather.Tas"><HintPath>$(TasLibs)\SAM.Weather.Tas.dll</HintPath></Reference>
    <Reference Include="SAM.Analytical.Tas"><HintPath>$(TasLibs)\SAM.Analytical.Tas.dll</HintPath></Reference>
    <!-- gbXML (only for the ToGbXML leg) -->
    <Reference Include="SAM.Core.gbXML"><HintPath>$(GbXmlLibs)\SAM.Core.gbXML.dll</HintPath></Reference>
    <Reference Include="SAM.Analytical.gbXML"><HintPath>$(GbXmlLibs)\SAM.Analytical.gbXML.dll</HintPath></Reference>
    <!-- Interop: EmbedInteropTypes MUST be false here -->
    <Reference Include="Interop.TBD"><HintPath>$(TasLibs)\Interop.TBD.dll</HintPath><EmbedInteropTypes>false</EmbedInteropTypes></Reference>
    <Reference Include="Interop.TAS3D"><HintPath>$(TasLibs)\Interop.TAS3D.dll</HintPath><EmbedInteropTypes>false</EmbedInteropTypes></Reference>
    <Reference Include="Interop.TSD"><HintPath>$(TasLibs)\Interop.TSD.dll</HintPath><EmbedInteropTypes>false</EmbedInteropTypes></Reference>
    <Reference Include="Interop.TCD"><HintPath>$(TasLibs)\Interop.TCD.dll</HintPath><EmbedInteropTypes>false</EmbedInteropTypes></Reference>
    <Reference Include="Interop.TCR"><HintPath>$(TasLibs)\Interop.TCR.dll</HintPath><EmbedInteropTypes>false</EmbedInteropTypes></Reference>
    <Reference Include="Interop.TIC"><HintPath>$(TasLibs)\Interop.TIC.dll</HintPath><EmbedInteropTypes>false</EmbedInteropTypes></Reference>
    <Reference Include="Interop.TPD"><HintPath>$(TasLibs)\Interop.TPD.dll</HintPath><EmbedInteropTypes>false</EmbedInteropTypes></Reference>
    <Reference Include="Interop.TWD"><HintPath>$(TasLibs)\Interop.TWD.dll</HintPath><EmbedInteropTypes>false</EmbedInteropTypes></Reference>
  </ItemGroup>
</Project>
```

`dotnet build` works for **this harness**. It does NOT work for `SAM.Analytical.Tas.csproj` itself —
that needs the .NET Framework MSBuild (`ResolveComReference` is missing from the dotnet SDK's MSBuild),
and Restore/Build must be **separate** invocations:

```bash
MSB=$("/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe" -latest -products '*' -find 'MSBuild\**\Bin\MSBuild.exe' | head -1)
"$MSB" SAM_Tas/SAM.Analytical.Tas/SAM.Analytical.Tas.csproj -t:Restore -v:m -nologo
"$MSB" SAM_Tas/SAM.Analytical.Tas/SAM.Analytical.Tas.csproj -t:Build -p:Configuration=Debug -v:m -nologo
```

Combining them as `-t:Restore,Build` fails with `CS0518 Predefined type 'System.String' is not defined`.

## One operation per process

`SAMTBDDocument.Dispose()` calls `tBDDocument.close()`, which tears down the shared Tas COM session
after a handful of open/close cycles in one process lifetime — a later `new SAMTBDDocument(...)` then
throws `RPC server is unavailable`. This is a Tas COM-server lifetime limit, not a code defect.

So give the harness **argv modes** and invoke it once per operation:

```bash
Harness.exe togbxml  C:\TasOut\model.sam  C:\TasOut\model.xml
Harness.exe workflow C:\TasOut\model.sam  C:\TasOut\model.xml C:\TasOut\model.tbd C:\...\weather.twd sizing
Harness.exe dumptbd  C:\TasOut\model.tbd  C:\TasOut\tbd.txt
```

Never loop several document cycles inside one process. Run `Modify.Simulate` in a child process if you
must do anything with a live COM session afterwards.

## Do not call these from an external assembly

`SAM.Core.Tas` embeds its interop types, so any public helper returning `List<TBD.zone>`,
`List<TBD.buildingElement>`, `List<TBD.IZoneSurface>` etc. fails to compile across the assembly
boundary with **CS1769**. That rules out `Query.Zones()`, `Query.BuildingElements()`,
`Query.ZoneSurfaces()`, `Query.Constructions()`, `Query.ApertureTypes()`, `Query.Schedules()`.

Walk Tas's own 0-based, null-terminated accessors instead — which is also the right call for a test
harness, since it observes the file independently of the code under test:

```csharp
List<TBD.zone> zones = new List<TBD.zone>();
TBD.zone zone = building.GetZone(0);
while (zone != null) { zones.Add(zone); zone = building.GetZone(zones.Count); }
```

Same shape for `building.GetIC(i)`, `building.GetConstruction(i)`, `zone.GetSurface(i)`,
`internalGain.GetProfile((int)TBD.Profiles.ticX)`, `thermostat.GetProfile(...)`.

## Confirm Tas is actually installed

```bash
powershell -NoProfile -Command "\$c=(Get-ItemProperty 'HKLM:\SOFTWARE\Classes\TBD.Document\CLSID').'(default)'; (Get-ItemProperty \"HKLM:\SOFTWARE\Classes\CLSID\\\$c\LocalServer32\").'(default)'"
```

Expected: `C:\PROGRA~1\ENVIRO~1\Tas\TBD.exe`.

## Models to test against

- `SAM_Deploy/SAM_SolarCalculator/SAM_SolarCalculator.Tests/Fixtures/ModelA-Tas.sam` — 2 spaces, normal
  **and** HDD internal conditions, small and fast. Good for naming/round-trip work.
- `OneDrive - Tetra Tech, Inc/Documents/SAM_daily/2027-08-03-HVAC/SAM_zoningAM_v2zonesisDomestic.sam` —
  real TM59 residential project, 9 spaces, 18 ICs. The de-facto production regression model; prefer it
  when the question needs scale.

A hand-built synthetic `AdjacencyCluster` does **not** survive the TBD->SAM import (hand-wound vertical
walls are lost), so any test touching the import leg needs a real `.sam` fixture.
