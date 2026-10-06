# The direct SAM -> T3D route (`T3DRoute.Direct`)

An opt-in way to build the TAS3D `.t3d` for a workflow run straight from a SAM `AnalyticalModel`, through TAS's own geometry
importer (`T3DDocument.CreateIDFImport()` -> `TAS3D.WrImportIDF`), **with no gbXML anywhere**. The established route
(`SAM -> gbXML -> ImportGBXML -> UpdateT3D -> TBD`) is unchanged and stays the default.

* Branch `feature/t3d-direct-import`, cut from `origin/sow/2026-Q4` (`f624ebc`). No pull request has been opened.
* Switch: `WorkflowSettings.T3DRoute` (`GbXML` default, `Direct`). Only the exact serialized value `"Direct"` (or the number 1)
  selects it; a missing, unknown or corrupt value reads as `GbXML`. Nothing selects the direct route by omission.
* Everything below was **measured on licensed Tas 9.5.7** with the harness in
  `SAM_Tas/SAM.Analytical.Tas.DirectT3D.Validation` (see "How to re-run"). Where a measurement contradicted the implementation
  specification, the measurement won and this document says so.

## Contents

1. [Architecture](#architecture)
2. [Workflow integration and what was gated](#workflow-integration-and-what-was-gated)
3. [Where the specification was wrong, and what was done](#where-the-specification-was-wrong-and-what-was-done)
4. [The four open questions](#the-four-open-questions)
5. [Validation](#validation)
6. [Real-model comparison, direct vs gbXML](#real-model-comparison-direct-vs-gbxml)
7. [Performance](#performance)
8. [Open issues and manual acceptance](#open-issues-and-manual-acceptance)
9. [How to re-run](#how-to-re-run)

## Architecture

The conversion is two halves, on purpose.

```
AnalyticalModel --Query.T3DImportPlan--> T3DImportPlan (no TAS, no COM) --Convert.ToT3D--> T3DDocument
                                              |                                                 |
                      every decision, testable offline                  replayed in the one order WrImportIDF allows
```

| Piece | File | Role |
| --- | --- | --- |
| `T3DRoute` | `Enums/T3DRoute.cs` | `GbXML = 0` (default), `Direct = 1` |
| `ToT3DOptions` | `Classes/ToT3DOptions.cs` | `UseWidths` (false), `ElementPerPanel` (false), `SharedWindowTypes` (false), `ImportShades` (true), `ZoneSetName`, `Tolerance`, `SnapTolerance` |
| `T3DImportPlan` + specs | `Classes/T3DImportPlan.cs` | COM-free plan: zones, elements, window types, surfaces (each followed by its openings), shades, report |
| `T3DImportReport` | `Classes/T3DImportReport.cs` | counts, the SAM space -> zone map, every skipped item with the reason, every caveat |
| `Query.T3DImportPlan` | `Query/T3DImportPlan.cs` | reads the model and makes every decision |
| `Query.TasPolygon`, `ToTasCoordinates`, `NewellNormal`, `ProjectedOnPlane` | `Query/TasCoordinates.cs` | the **one** polygon helper (closing vertex, near-duplicates, collinear points, tolerance, orientation, `double[3,n]`) |
| `Convert.ToT3D(model, T3DDocument \| path, options, out report)` | `Convert/ToT3D/T3DDocument.cs` | the COM replay; releases every COM object it created |
| `Query.ZoneDescription`, `TryGetSpaceGuid` | `Query/ZoneDescription.cs` | the SAM space GUID carried in the zone description (`[SpaceGuid]=...`) |
| `Query.UpdateSpaces` | `Query/UpdateSpaces.cs` | the zone loop `UpdateT3D` runs on the gbXML route, for the direct route |
| `Query.InternalSurfaceReversals`, `Modify.UpdateReversed` | `Query/`, `Modify/` | sets the reversed side of internal walls (see below) |

The signature differs from the specification's sketch on purpose: `ToT3D(model, T3DDocument, ToT3DOptions, out report)` has no default
for `options`, because the existing two-argument `ToT3D(AnalyticalModel, T3DDocument)` returns a `Building` and an optional
parameter would have let a new overload capture existing callers.

### What the plan decides

* **Zones** - one per `Space` / `ExternalSpace` that bounds at least one importable panel, created in SAM's space order. Name = space name,
  description = `[SpaceGuid]=<guid>` (TAS's own `Zone.GUID` is read-only through COM). The zone is `external` for an `ExternalSpace`. A space whose panels
  could not be imported gets no zone (reported).
* **Elements** - one per SAM `Construction` (plus a split where the same construction is used on and off the ground, or as air, so ground is never
  wrong), named after it; width = `DefaultThickness` else the layers' thickness; colour, transparency, internal shadows, BE type, ground, ghost,
  floor-area flag follow the rules `Query.UpdateT3D` applies on the gbXML route; the construction GUID rides in the element description. A generic
  `Wall` / `Floor` construction gets its BE type from how the panel is used (the gbXML route inherits TAS's choice from the gbXML surface type).
* **Surfaces** - shade (`PanelType.Shade` or no space) -> `AddShadeSurface`; one space -> `AddSurface` (adiabatic per
  `Analytical.Query.Adiabatic`); two spaces -> `AddInternalSurface`, or, when adiabatic, one adiabatic `AddSurface` per zone (what the gbXML
  route's `UpdateAdiabatic` ends up with). The polygon normal is made to point **out of the first zone**, taken from that space's own closed
  shell; an open shell cannot say which way is out, so the panel's own normal is used and the report says so.
* **Openings** - each aperture is emitted immediately after its host (`AddOpening` attaches to the surface added last), snapped onto the host
  plane and wound the same way. **Window types are one window object per aperture** (see "Where the specification was wrong").
* Polygons with holes: `AddSurface` takes one outer loop; the outer loop is imported and the hole is reported.

## Workflow integration and what was gated

`WorkflowCalculator` branches only inside the T3D conversion block. The gbXML path is unchanged, including every `Step(...)` name, so
`.timing.csv` output stays comparable. The direct route adds `Converting SAM to T3D` (replacing `Importing gbXML` + `Updating T3D file`) and
`Aligning Reversed Surfaces`.

`Direct` together with `Path_TBD_Canonical` is refused up front, exactly as `Path_gbXML` + canonical already is.

| Step | gbXML | Direct | Why (evidence) |
| --- | --- | --- | --- |
| `Query.UpdateT3D` | yes | **no** | the converter states every element, window and zone itself; the space stamping it did is `UpdateSpaces` |
| Updating Facing External Elements | yes | yes | reads building element **names**, which are the same on both routes |
| Assigning Adiabatic Constructions | yes | **no** | it re-assigns building elements whose names end `-unzoned`/`-internal`/`-exposed` - names only TAS's gbXML import makes. Measured: **0** matches on a direct TBD (real model and synthetic). Left on, it would re-assign a SAM construction that happened to be called `...-internal` |
| Setting Adiabatic (`UpdateAdiabatic`) | yes | **no** | the importer states `isAdiabatic` itself. Real model: running it over a direct TBD changes nothing (14 -> 14 null links). Synthetic adiabatic wall **with a window**: TAS already null-links the opening's pane and frame natively (3 of 3). And it is **harmful** on a direct TBD: it aligns SAM geometry to the TBD by the footprint recentring the gbXML import does, but a direct TBD keeps SAM coordinates (gbXML TBD min = (-30.5,-8,-6), direct TBD min = (0,0,0) = SAM), so it flagged a false null link (4 vs 3) |
| Updating Building Elements | yes | yes | constructions, colours, aperture control. Per-aperture window names decode exactly as on the gbXML route |
| Updating Ids | yes | yes | resolves each space's zone by the `[SpaceGuid]` in the zone description first, then the stamped GUID, then the name |
| **Aligning Reversed Surfaces** | n/a | **yes (new)** | see "Reversed side" |
| Reusing Aperture Definitions | yes | yes | one window object per aperture -> TAS builds one element and construction per aperture on both routes, and they must be collapsed. Real model: 40 aperture parts rebound onto shared definitions, identical to the gbXML route |
| Updating Aperture Types | yes | yes | real model: 20 of 20 apertures updated on both routes |

### Reversed side of internal walls (found by the real-model simulation)

Simulating the real model through both routes gave results that differed by up to 14 % on some outputs. Two runs of the **same** route are
bit-identical (noise floor = 0), so the difference was real. Cause: for a partition between two zones TAS decides which side is *reversed*
(its layers run the other way round for that zone). `WrImportIDF.AddInternalSurface`'s `reverseElement` flag has **no effect** on this (identical TBD
for `false` and `true`, on synthetic and real models); TAS decides from its own geometry, and on the real model half the partitions disagreed
with SAM's convention - the panel's first space sees the layers as listed, the second sees them reversed, which is what `Modify.Update`
(SAM's own direct TBD export) writes and what the gbXML route produces. The partition's first layer is a paint film on one face only, so the
side matters. `Modify.UpdateReversed` now sets it from the identities `UpdateIds` has just stamped. Horizontal surfaces are left to TAS
(identical to the gbXML route in every case measured, both space orders). After the fix the two routes' full-year results agree to **9e-5
relative**.

## Where the specification was wrong, and what was done

| Specification said | Measured / found | Decision |
| --- | --- | --- |
| One window type per `ApertureConstruction`, not per aperture | **TAS folds every opening of one window object on one host surface into one TBD zone surface**: three 3.33 m2 windows on one wall became one 10 m2 window (frame 1.12 + pane 8.88). That keeps the area but loses "N apertures keep N pane and N frame surfaces" - the identity Stage 3 (`APERTURE_*.md`) is built on - and a per-aperture frame percentage, which is an attribute of the window object. Also the window-per-construction building elements carried hash-suffixed names no construction matched (3 GLAZING elements got no construction) | **One window object per aperture by default**, named `Windows: <name> <aperture GUID> ` (trailing space: TAS appends `-pane`/`-frame` directly) so the existing name decoding and the shared-definition collapse work unchanged. `ToT3DOptions.SharedWindowTypes = true` gives the specification's design and its consequences; it is covered by a licensed test that shows the fold |
| `Query.OpeningType(aperture)` gives the opening type | it throws `NotImplementedException` (the legacy `ToT3D(Aperture)` is dead code) | own mapping: door 2, roof 1, else 0 for `AddWindow`; `positionType` from the host (wall window 0 / door 2, roof 1, floor 4) as `UpdateT3D` does. `AddWindow` accepts 0-2 only (3 returns null; `positionType` 4 is settable afterwards, 3 is not) |
| Widths default ON (TAS importer default) | with widths ON TAS treats polygons as centre lines and shrinks every zone 10-12 % | default `UseWidths = false`, the same default as `WorkflowSettings.UseWidths`, so the SAM polygons are the geometry (see Q1) |
| `doc.Create()`'s default `Zone` zone set "doesn't reach the TBD" | it reaches the TBD as an empty zone **group** called `Zone` | deleted before the SAM zone set is made |
| Put the space GUID in the zone description | confirmed - it survives T3D -> TBD and `SAMZoneMetadata.Compose` (which `UpdateZone` runs) preserves it | `[SpaceGuid]=<guid>` segment; `UpdateIds` resolves by it first |
| Remaining `UpdateIds`/`UpdateAdiabatic`/... "re-check" | see the gating table | gated, each with evidence |
| The spec's "T3D -> TBD" keeps widths as `doc.SetUseBEWidths` | also needed after `CreateImportedModel` | set on the importer before the first `Add*` and on the document after the import |

Differences from the Q3 baseline the specification was written against: `sow/2026-Q4` is `master` `28ac11a7` plus documentation and CI
files only, so the workflow, converter and step code the specification names are identical; line numbers in the specification are obsolete.

## The four open questions

**A. Are SAM panel polygons centre lines?** No. For every one of the 9 spaces of the real model, widths OFF reproduces the volume of SAM's own
shell for that space **exactly** (3544.0 m3 in total) and widths ON shrinks it (3121.4 m3, -11.9 %; floor 886.0 -> 860.6 m2). No geometry offset is
introduced. The synthetic reference box (5 x 4 x 3 m; walls 0.30, roof 0.35, floor 0.40 m): widths ON floor **17.39 m2**, volume **45.649 m3**; widths OFF
**20 m2**, **60 m3** - exactly the specification's numbers.

**B. Horizontal internal surfaces.** Verified, both space orders (`StackedZones(upperFirst)`): one `AddInternalSurface` gives one `tbdLink` pair, the
lower zone sees a ceiling (inclination 0), the upper a floor (180), areas 20 m2 each, floor areas 20 and volumes 60 for both zones, and TAS assigns the
reversed side identically to the gbXML route. The plan test pins the normal pointing out of whichever zone comes first.

**C. Shades.** `AddShadeSurface` **works and is equivalent to the gbXML route**. A south window with and without a 2 m canopy (London): the pane's
hourly shade proportion (TBD stores the *sunlit* fraction; -1 = no direct sun) falls from 239.0 to 136.7 sunlit window-hours over the representative days,
to 40.5 with a lower, deeper canopy, and the hourly arrays for the canopy case are **identical** to what the gbXML route gives for the same model
(15 Jun: `-1 ... 0.47 0.11 -1 ... 0.12 0.43 -1 ...`). Shades are therefore imported directly; `ToT3DOptions.ImportShades = false` lists
them in the report instead of dropping them silently. Not exercised through the real-model workflow (the real model has no shade panels).

**D. `AddGroupOpening`.** Not used. The only reference to `WindowGroup` in SAM and SAM_Tas is a one-line enumerator, `Query.WindowGroups`, that nothing calls;
SAM has two aperture types (window, door) and an aperture is one polygon plus a frame.

## Validation

Everything here ran on this machine (Tas 9.5.7, licensed) on 2026-10-07; the artifacts are preserved (see "How to re-run").

| Check | Result |
| --- | --- |
| `MSBuild SAM_Tas.sln` (Debug, .NET Framework MSBuild) | succeeds, 0 errors; the harness is part of the solution |
| `dotnet test SAM.Analytical.Tas.TM59.Tests` | **1113 passed**, 0 failed (1055 existing + 58 new in `DirectT3DRouteTests`) |
| `dotnet test benchmark/SAM.Analytical.Tas.Benchmark.Tests` | **16 passed** (unchanged) |
| Licensed `synthetic` (box on/off, window, three windows, door + rooflight, partition, adiabatic, ground, stacked x 2 orders) | **104 checks, 0 failed** |
| Licensed `shade` | 8 checks, 0 failed |
| Licensed `widths` (real model) | 10 checks, 0 failed |
| Licensed `gating` | 7 checks, 0 failed |

**Synthetic geometry** (TBD read back through TAS's own accessors, independent of the code under test):

| Case | Result |
| --- | --- |
| Box 5 x 4 x 3 (walls 0.30 / roof 0.35 / floor 0.40), widths ON | floor **17.39 m2**, volume **45.649 m3**, 6 surfaces: 5 `tbdExposed` + 1 `tbdGround` |
| same, widths OFF | **20.0 m2**, **60.0 m3** |
| One window 2 x 1 m in the south wall | pane 1.71 + frame 0.29 = 2.0 m2 (the polygon, not the window type, sets the size); the south wall is 15 - 2 = 13 m2; building elements `Windows: EXT_GLZ <aperture guid> -pane` / `-frame` |
| Three windows on one wall | default: 3 window objects, **3 pane + 3 frame surfaces**; `SharedWindowTypes`: 1 window object, **1 pane + 1 frame surface** (the fold), same 3 m2 |
| Door + rooflight | the door is `Doors: ... -pane`, BEType 14 (2 m2); the opening in the roof is BEType **13 (rooflight)**, 1 m2 on a horizontal surface |
| Two zones with one partition | the partition is **one** `AddInternalSurface` -> 2 `tbdLink` surfaces, both linked, on `INT_PARTITION`; 8 exposed, 2 ground; floor 40, volume 120 |
| Horizontal internal floor, either space order | one `AddInternalSurface` -> 2 `tbdLink`, inclination 0 (lower zone) and 180 (upper), 20 m2 each; zone floor 20 / volume 60 each |
| Adiabatic north wall | 1 `tbdNullLink` (A's north wall, 15 m2), 7 exposed, partition still `tbdLink` |
| Adiabatic wall with a window in it | wall + pane + frame are all `tbdNullLink` as imported (TAS carries the flag to the openings) |
| Slab on grade | 1 `tbdGround` of 20 m2 on `GRD_FLOOR`; only that element is flagged ground |
| Zone identity | zone description is `[SpaceGuid]=<guid>`; the zone groups are exactly `[SAM]` (TAS's seeded empty `Zone` set removed) |

**Skipped / invalid geometry** is never silent: degenerate polygons (fewer than 3 distinct, non-collinear vertices, or no area), panels bounding more
than two spaces, spaces none of whose panels import, holes, open shells and apertures off their host plane are counted in `T3DImportReport.Skipped` /
`.Notes`; the workflow turns `Skipped` into `ISSUE:` notes (raised as warnings by the Grasshopper component) and refuses the run only when TAS itself
rejects a call or `CreateImportedModel` fails.

## Real-model comparison, direct vs gbXML

Model: `000000_SAM_AnalyticalModel.sam` (9 spaces, 50 panels, 20 apertures, 4 constructions, 1 aperture construction), run through the real
`WorkflowCalculator` on each route into its own folder, with full-year simulation (the `final-*` runs; the files already in the `2026-09-29 partOi` folder were
never touched). The comparison is `compare` (TBD structure), `deep` (every property the simulation reads) and `models` (the SAM model each route hands back).

**Same, number for number** (19 totals): TBD zones 12, floor area 913.000 m2, volume 3598.000 m3, 128 zone surfaces - `tbdExposed` 47 (272 m2),
`tbdGround` 27 (1012 m2), `tbdLink` 40 (992 m2), `tbdNullLink` 14 (1230 m2) - 40 aperture surfaces (64.8 m2: pane 57.39 + frame 7.41), 11 building elements,
11 constructions, 2 aperture types; every building element's BE type, width, ground flag, construction and aperture types; **every zone's surface multiset**
(type, orientation, inclination, area, building element); and the SAM model each route hands back (9 spaces, 4 constructions, 50 panels, 20 apertures).

| Difference | Class | Explanation |
| --- | --- | --- |
| Zone description carries `[SpaceGuid]=...` (9 zones) | expected improvement | the gbXML TBD carries no SAM space identity; this is what `UpdateIds` resolves by first |
| Element `SIM_EXT_GRD_FLR FLR01` colour 808040 vs 004080 | equivalent representation | cosmetic: no construction colour is stated, so gbXML keeps TAS's default for the gbXML surface type and direct uses SAM's colour for the panel type |
| Zone group `gbXml Spaces` vs `SAM` | equivalent representation | the name of the T3D zone set holding every zone |
| TAS surface numbers in `ZoneSurfaceReference` stamps and surface results' `Reference` | equivalent representation | TAS numbers surfaces in creation order; consistent within each TBD |
| `zone.exposedPerimeter` / `facadeLength` of Corridor_1 (82 vs 6 m), Kitchen_7 (15.5 vs 10.5), Ensuite_8 (5 vs 0) | expected improvement - **MANUAL ACCEPTANCE REQUIRED** | the direct route states adiabatic walls at import so TAS leaves them out of the exposed perimeter; the gbXML route computes the perimeter before its adiabatic repair, so it includes them. The differences equal the adiabatic wall lengths exactly. Effect on results: none measurable here (see below), but whether TAS's ground model ever reads the field should be confirmed by a person |
| Window `level` in the T3D (0 vs 0.85 m) | equivalent representation | the opening polygon, not the window type, positions the opening; identical TBD |
| Internal-wall `reversed` side | **likely defect, fixed** | see "Reversed side of internal walls" |

**Full-year results**: 18 result sets (9 spaces x heating/cooling), 537 simulation outputs. **471 are bit-identical; the other 66 agree within
8.95e-5 relative** (opaque/glazing external conduction, building heat transfer, a few gains). Two runs of the **same** route are bit-identical in all
537, so this residual is real but tiny; its cause was not isolated (the only other measured differences are the exposed perimeter above and the float
precision of recentred vs absolute coordinates). Before the reversed-side fix the same comparison showed 121 differing outputs, up to 14 %.
Excluded as non-outputs, on both routes and in same-route reruns: `Zone Guid` (minted by TAS) and `Dry Bulb Temperature` (the result import reads it
from a field TAS does not fill, so it is garbage and differs between any two runs).

**Aperture steps**: "Reusing Aperture Definitions" rebound 40 aperture parts onto shared definitions and removed 40 per-aperture elements; "Updating Aperture Types"
updated 20 of 20 - the same notes on both routes. The `ZoneSurfaceReference` / `BuildingElementGuid` stamps are on the same objects on both routes.

**Preserved artifacts** (both sets, plus every comparison file) are in the `2026-09-29 partOi` folder under `gbxml-q4-reference` and `direct-t3d-q4`.

## Performance

Cold TAS start for every run (TAS processes killed before each, nothing else running), three runs per route, the same model, simulation off
(the settings the 2026-10-01 baseline used). `.timing.csv` step names are unchanged, so the runs compare directly.

| Step (median of 3) | gbXML ms | direct ms | change |
| --- | ---: | ---: | ---: |
| Opening T3D file | 10 708 | 731 | -9 976 |
| Importing gbXML | 52 | - | removed |
| Updating T3D file (`UpdateT3D`) | 401 | - | removed |
| Converting SAM to T3D | - | 438 | new |
| T3D to TBD -> Shading | 32 630 | 26 966 | -5 664 |
| Assigning Adiabatic Constructions | 42 | - | removed |
| Setting Adiabatic | 1 750 | - | removed |
| Updating Building Elements | 944 | 733 | -211 |
| Updating Ids | 3 675 | 3 288 | -387 |
| Aligning Reversed Surfaces | - | 113 | new |
| Reusing Aperture Definitions | 885 | 811 | -74 |
| Updating Aperture Types | 2 739 | 2 142 | -597 |
| Updating Zones + Add IZAMs + Sizing | 4 860 | 3 770 | -1 090 |
| **TOTAL** | **60 904** (57 066 - 61 731) | **41 368** (33 848 - 41 884) | **-19 536 (-32 %)** |

Against the baseline in the specification (58.2 s on 2026-10-01: T3D -> TBD shading 25.0 s, opening T3D 11.0 s, Updating Ids 4.5 s, Updating Aperture
Types 2.8 s, Reusing Aperture Definitions 1.4 s) the gbXML route measured here is in line (60.9 s; the shading step, 32.6 s, is the main difference and
varies with machine load). **Where the time goes:** nearly all the gain is `Opening T3D file` (a T3D opened by path versus one created: 10.7 s vs 0.7 s) and
the T3D -> TBD export (-5.7 s). The repair stages the direct route drops (`UpdateT3D`, the gbXML import, both adiabatic steps) are only about 2.2 s together -
removing them alone is a ~4 % gain, not the headline. A first batch that overlapped other work on the machine was discarded and repeated (it had gbXML at 51-84 s);
the spread within each route above is what to expect.

**Per-call COM cost is not a bottleneck.** The converter makes one COM call per surface or opening (plus one per element, window type and zone):

| Synthetic building | Zones / surfaces / openings | Plan | COM replay | T3D -> TBD export |
| --- | --- | ---: | ---: | ---: |
| 10 x 10 grid | 100 / 420 / 40 | 376 ms | 471 ms (1.0 ms per call) | 86.7 s (gbXML route end to end for the same model: 83.6 s) |
| 20 x 15 grid | 300 / 1235 / 80 | 560 ms | 866 ms (0.66 ms per call) | 160.7 s |

TAS's own shading export is the cost at scale on either route; the direct conversion is under 1 % of it. The two routes' TBDs for the 10 x 10 grid match
(100 zones, 2000 m2, 6000 m3, 680 surfaces, 85 building elements).

## Open issues and manual acceptance

| # | Item | Class |
| --- | --- | --- |
| 1 | **A person has not opened a produced T3D in TAS3D.** All geometry was checked through TAS's own accessors and the TBD it exports; no visual inspection took place. Preserved for it: `direct-t3d-q4\000000_SAM_AnalyticalModel.t3d` / `.tbd` and the synthetic and shade `.t3d` files. **MANUAL ACCEPTANCE REQUIRED** | manual acceptance only |
| 2 | Zone `exposedPerimeter` / `facadeLength` differ from the gbXML route on three zones (adiabatic walls excluded on the direct route). Likely more correct; confirm TAS's ground model, and that no downstream reader depends on the gbXML value. **MANUAL ACCEPTANCE REQUIRED** | manual acceptance only |
| 3 | The residual 66 simulation outputs at <= 8.95e-5 relative are not explained | follow-up (negligible) |
| 4 | `reverseElement` is inert in TAS, so the reversed side is set in the TBD (`UpdateReversed`) from stamps `UpdateIds` writes. A panel that cannot be stamped (unmatched geometry) keeps TAS's choice; horizontal panels are always left to TAS | follow-up |
| 5 | Door/window apertures in **internal** (two-sided) walls were validated only on one synthetic adiabatic case; no real model here has one | follow-up |
| 6 | `ExternalSpace` zones (`zone.external = true`) are built but no model with an `ExternalSpace` was available to run through TAS | follow-up |
| 7 | `WorkflowSettings.UpdateWindowPositionType` is not consulted: the direct route always sets the position type from the host (the gbXML route skips non-rectangular apertures unless the flag is on) | follow-up |
| 8 | A model with skipped panels continues with `ISSUE:` notes (it does not stop); only a TAS rejection stops the run. Whether a skipped panel should also stop it is a policy choice for the owner | follow-up |
| 9 | Simulation parity was measured on one real model (9 spaces); no larger real model was available, only synthetic grids up to 300 zones (TBD identical to the gbXML route at 100 zones) | follow-up |
| 10 | `SAM_Tas_Grasshopper` and SAM_UI are untouched: nothing there can select `T3DRoute.Direct` yet | follow-up (by design) |
| 11 | Panels with holes import the outer loop only (reported); shades with holes likewise | known limitation |
| 12 | `AddInternalSurface`'s `reverseElement` cannot be used (inert); the layer direction of horizontal internal surfaces is TAS's geometric choice, equal to the gbXML route's in every measured case. Sloped internal panels are not given SAM's rule either (not measured) and keep TAS's side | known limitation |
| 13 | The element / window attribute rules in `Query.T3DImportPlan` (thickness, colour, transparency, BE type, frame width) are re-implemented from `Query.UpdateT3D`; a change to one must be made in the other. Sharing one decision helper would remove the drift risk but means touching `UpdateT3D` (not done: no unrelated refactors) | follow-up |
| 14 | `Modify.UpdateIds` now reads each TBD zone's description (one extra COM read per zone) on the gbXML route too, to build the description-GUID index; it finds none there | follow-up (minor) |
| 15 | No automated test asserts which workflow steps run per route, or that `Modify.UpdateReversed` writes to a TBD (COM): the licensed harness covers both and is not part of `dotnet test` / CI | follow-up |
| 16 | A curved (non-polygonal) panel or aperture boundary is skipped and reported; it is not discretised | known limitation |

There are no blockers. The branch is ready for review; items 1 and 2 are the ones that need a person with TAS3D open.

## How to re-run

All of it needs a licensed Tas and the .NET Framework MSBuild (COM references); `dotnet build` cannot build the harness.

```bash
MSBuild.exe SAM_Tas.sln -restore -p:Configuration=Debug          # builds the library, the harness and the tests
dotnet test SAM_Tas/SAM.Analytical.Tas.TM59.Tests -c Debug         # COM-free; includes DirectT3DRouteTests
```

The harness is `SAM_Tas/SAM.Analytical.Tas.DirectT3D.Validation/bin/Debug/direct-t3d-validation.exe`. Write its output under a **short** path (a long path
makes TAS show a modal save error), close TAS3D/TBD first, and run one mode at a time:

| Mode | What it does |
| --- | --- |
| `synthetic <dir>` | the synthetic buildings, each read back from the TBD TAS exports (104 checks) |
| `shade <dir>` | with/without canopy, direct vs gbXML, hourly shade proportions |
| `widths <model.sam> <dir>` | widths ON/OFF per zone against SAM's shell volume |
| `gating <model.sam> <dir> [gbxml.tbd]` | what each gbXML-era repair would do to a direct TBD; where TAS puts the geometry |
| `reversed <dir>` / `reversed-real <model.sam> <dir>` | which side of an internal wall TAS reverses, `reverseElement` false vs true |
| `workflow <model.sam> <dir> <gbxml or direct> [simulate] [widths] [name=<stem>]` | the real `WorkflowCalculator` on either route; writes `.t3d/.tbd/.timing.csv/.notes.txt/.result.json` |
| `compare <gbxml.tbd> <direct.tbd> <prefix>` | structural comparison with every difference classified (`.compare.txt` / `.json`) |
| `deep <a.tbd> <b.tbd> [out]` | the properties the simulation reads (perimeter, altitude, shade proportions, reversed ...) |
| `models <a.result.json> <b.result.json> <prefix>` | the SAM models each route hands back, outputs compared numerically |
| `scale <dir> <nx> <ny> [gbxml]` | grid of zones: plan, COM replay and export timings |
| `inspect <model.sam>` / `dump <tbd>` / `t3d <file>` | describe a model and its plan / a TBD / a T3D |

Do not rebuild the harness while a timing run is in progress, and never run two TAS-driving processes at once.
