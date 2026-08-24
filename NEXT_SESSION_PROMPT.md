# Continue: investigate the ~29% cooling-load drop between generation 1 and 2

## Setup

Repo: `SAM-BIM/SAM_Tas`. Checkout branch `fix/tas-ventilation-ticv-factor-growth` (already pushed,
tracks `origin/fix/tas-ventilation-ticv-factor-growth`; PR #40 is open against `sow/2026-Q3`, which
already has PR #39 merged). Pull latest before starting - this file's own instructions live at the HEAD
of that branch.

Read `PROJECT_PROGRESS.md`'s top "Last updated" entry first - it has the measured numbers below and is the
authoritative record. Also read `SAM.Analytical.Tas/VENTILATION_TICV_ROUND_TRIP.md` and
`SAM.Analytical.Tas/APERTURE_ROUND_TRIP_IDENTITY.md` for the two defects already fixed this session, both
with the exact same symptom shape (stable gen0->gen1, a jump at gen1->gen2, then a fixed point) - whatever
this is, it likely shares a root cause pattern with those two, even though ticV/freshAirRate are ruled out
as the direct cause here.

## The finding, exactly as measured

A 3-generation chain built with BOTH already-fixed defects in place (aperture door-typed-pane fix +
ventilation ticV fix), driven through `Convert.ToSAM -> TogbXML -> WorkflowCalculator.Calculate` (the same
engine `SAMAnalytical.WorkflowgbXML` calls), `_importUnused_`/`_importSurfaceShades_` both false, sizing on:

| generation | heating (W) | cooling (W) |
|---|---|---|
| RA0 (seed - the user's real Grasshopper A0.tbd) | 11,037 | 115,192 |
| gen 1 | 11,289 (+2.3%) | 115,168 (~same) |
| **gen 2** | 11,726 (+3.9%) | **81,883 (-28.9%)** |
| gen 3 | 11,726 (same) | 81,883 (same) |

Read back via `Modify.UpdateDesignLoads` (`SpaceParameter.DesignHeatingLoad`/`DesignCoolingLoad`) - the same
call `WorkflowCalculator` itself makes at the end of a sizing run.

**Ruled out:** `ticV.factor` and `freshAirRate` - confirmed byte-identical (Studio 2.44 ACH / Bedroom
1.72 ACH / 8.0 l/s/p) across all three generations by direct COM dump. This is NOT the bug PR #40 fixes,
and NOT the bug PR #39 fixed either (aperture rebind counts were clean 40/40 throughout this same run).

**Leading suspects**, since a ~29% cooling swing with no ventilation change points at solar gain or
shading, not airflow:

- Glazing g-value / SHGC - does `ApertureConstruction`'s solar properties change value (not just name)
  between generation 1 and 2? `APERTURE_ROUND_TRIP_IDENTITY.md` already documents a NAME change
  (`SIM_EXT_GLZ` -> `Windows: SIM_EXT_GLZ`) at gen 1 that's stable and harmless - check whether anything
  about the construction's actual layer content or type changes at gen 2 specifically.
- Shading / blinds - `FeatureShade`, `Modify.SetBlinds`, or the `SolarModel`/shade-proportion import
  (`_importSurfaceShades_` was OFF for this run, so this may not apply - but check anyway).
- Aperture-type control - `Modify.SetApertureTypes`, opening restriction/schedule, or anything that
  changes how much of the window is "glazed vs open" for solar purposes between generations.
- Something in `Modify.UpdateBuildingElements` or `Modify.UpdateApertureDefinitions` that changes which
  physical surfaces end up on which construction between gen 1 and gen 2, even though the REBIND COUNTS
  look identical (40/40 both times) - a correct rebind count doesn't guarantee identical construction
  *content* was assigned.

## How to reproduce (the harness pattern used for both prior fixes)

A throwaway console harness lives in this session's scratch history, not in the repo - rebuild it fresh.
Reference `SAM.Analytical.Tas`, `SAM.Analytical`, `SAM.Core`, `SAM.Analytical.gbXML` (aliased `gbx`),
`SAM.Core.gbXML`, and the `Interop.*` TAS COM libraries from `SAM_Tas/build` and `SAM/build` and
`SAM_gbXML/build`. `net8.0-windows`, `UseWindowsForms=true` - see the `run-tas` skill
(`.claude/skills/run-tas/SKILL.md`) for the full csproj and every environment trap (close the TAS GUI
first, absolute paths only, short output directories).

```csharp
// One generation:
AnalyticalModel analyticalModel = SAM.Analytical.Tas.Convert.ToSAM(path_TBD_In, false, false);
gbXMLSerializer.gbXML gbXML = gbx::SAM.Analytical.gbXML.Convert.TogbXML(analyticalModel, SAM.Core.Tolerance.MacroDistance, 0.00001);
SAM.Core.gbXML.Create.gbXML(gbXML, path_gbXML);
WorkflowSettings workflowSettings = new WorkflowSettings {
    Path_TBD = path_TBD_Out, Path_gbXML = path_gbXML, WeatherData = weatherData,
    Sizing = true, Simulate = false, UpdateZones = true, AddIZAMs = true,
    SimulateFrom = 1, SimulateTo = 365, RemoveExistingTBD = false, UpdateWindowPositionType = false
};
AnalyticalModel result = new WorkflowCalculator(workflowSettings).Calculate(analyticalModel);

// Read loads back exactly as WorkflowCalculator does:
AdjacencyCluster adjacencyCluster = SAM.Analytical.Tas.Modify.UpdateDesignLoads(path_TBD_Out, result.AdjacencyCluster);
foreach (Space space in adjacencyCluster.GetSpaces())
{
    space.TryGetValue(SAM.Analytical.SpaceParameter.DesignHeatingLoad, out double heating);
    space.TryGetValue(SAM.Analytical.SpaceParameter.DesignCoolingLoad, out double cooling);
}
```

Weather: `C:\Users\Public\Documents\Tas Data\Databases\CIBSE Weather 2021.twd`, `[0]` element.

Seed TBD: the user's real `A0.tbd` from
`C:\Users\michal.dengusiak\OneDrive - Tetra Tech, Inc\Documents\SAM_daily\2026-07-15 PartO\Simulation\A0.tbd`
(copy to a short path first, e.g. `C:\P39\RA0.tbd` - TAS COM fails on long/nested paths, see the run-tas
skill). Run 3 generations chained (gen1's output TBD becomes gen2's input, etc.) and dump loads after each.

## What "done" looks like

Same standard as the other two fixes in this branch:

1. Reproduce the drop independently (confirm it's not an artifact of my harness).
2. Find the exact code path and the exact value that changes between generation 1 and 2 - not just "loads
   changed" but "construction X's g-value went from A to B" or equivalent.
3. Explain WHY generation 1 doesn't show it but generation 2 does (matches the pattern: TAS's own
   gbXML/T3D conversion does something on re-export that the first export doesn't trigger).
4. Fix it, keeping every existing safety invariant from the two prior fixes (see their .md files) -
   don't loosen a refusal, don't infer identity from names, etc.
5. Add COM-free unit tests pinning the mechanism, with a mutation check.
6. Re-run the full 3-generation chain and confirm heating/cooling reach a fixed point at generation 1
   (or state clearly if a fixed point at generation 2 is actually correct/expected, with reasoning).
7. Run `SAM.Analytical.Tas.TM59.Tests` and `SAM.Analytical.Tas.Benchmark.Tests` in Debug and Release -
   must stay 100% green (561/561 and 16/16 as of this branch's HEAD, or whatever the current count is).
8. Update `PROJECT_PROGRESS.md` and either extend `VENTILATION_TICV_ROUND_TRIP.md` or add a new doc,
   following the existing docs' style (root cause, fix, licensed acceptance table, tests).
9. Commit on this same branch, push, and update PR #40's description with the new finding - do not open
   a separate PR unless the fix turns out to be unrelated in scope once found.

## Guardrails carried over from this session

- Never trust "it didn't reproduce" without measuring the actual loaded DLL / actual code path. Both
  prior "false negatives" in this project happened because a harness left an input at its default that
  the real Grasshopper canvas didn't.
- Prefer reproducing through the REAL engine (`WorkflowCalculator.Calculate`, the same one
  `SAMAnalytical.WorkflowgbXML` calls) over reimplementing the pipeline by hand - a hand-rolled repro
  diverged from the real gbXML export once already this session and cost time to notice.
- Test in every combination of `_importUnused_` and `_importSurfaceShades_` before declaring a fix
  licensed-verified - the aperture bug only showed up with one flag on.
