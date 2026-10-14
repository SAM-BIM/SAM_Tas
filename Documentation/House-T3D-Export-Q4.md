# House model: direct T3D export and physical thickness — Q4 (2026-10-14)

Status: implemented on `feature/panels-by-solid-q4` in two commits: the direct export and the physical thickness, then UseBEWidths ON matching and the OpeningProperties diagnostics. Draft PR, owner review required. Not merged.
Base: `sow/2026-Q4` at `4cb3094cc7c8efea168f37d5f04897be2e9415bc`.
PR: the live draft PR for this branch in `SAM-BIM/SAM_Tas`. Its description holds the tested SHA, the final head and the hosted checks.

**Dependency (build):** SAM-BIM/SAM#194 (`feature/panels-by-solid-q4`) adds `PanelSourceParameter.PhysicalThickness`, which this branch reads.
- The branch has the same name on purpose. SAM_Tas CI clones a dependency at the PR's head-branch name first, so it builds against SAM#194.
- **Merge SAM#194 first**, then re-run this PR's CI against `sow/2026-Q4`.

Related:
- SAM-BIM/SAM_OCCT#77 carries the physical thickness and source names through `CreateAdjacencyCluster`.
- SAM-BIM/SAM_OCCT#76.

## Why

On the real house (`SAM_daily\2026-10-13-House\test3.gh`) the direct T3D route failed.

Log: `zones=11 elements=6 windows=25 surfaces=56 ... skipped=4 success=False`.

TAS refused two door window types (`Doors: SIM_EXT_SLD <guid>`), and their openings on `SIM_INT_SLD_Partition` were then lost.

## Root causes and changes

1. **Window level below zero (the failure).** TAS `AddWindow` refuses any negative `level`. Measured on this machine:
   - refused at -1e-17, -1e-12, -1e-9, -1e-6, -0.001, -0.5 and -3, for both window and door types;
   - accepted at 0, -0.0 and 1e-17;
   - width, height, colour, type and name make no difference.

   The two refused doors were ground-floor internal doors off the GF Hall (0.89 × 2.095 m). Their opening polygons came out a rounding error below z = 0 (level printed `-0`). The other ground-floor doors happened to land on 0 exactly.
   - `Query.T3DImportPlan` now never writes a level below 0. A real drop below zero (more than `Tolerance`) is reported as a note.
   - The opening polygon still places the opening.
   - Construction naming was **not** the cause.
2. **Slab with a stair void imported whole (found once the export ran).** `AddSurface` takes one outer loop. So the UF slab (hole = the stair void, filled by the Air panel) was imported over the Air panel:
   - UF Landing got 18.825 m² of floor;
   - Hall and Landing each got about 5 m³ too much;
   - the TBD total was 574.1 m³ against SAM's 564.1 m³.

   The plan now splits panels with non-aperture holes with `AdjacencyCluster.SplitByInternalEdges`, exactly as the gbXML route does (`Campus`), and notes it. The first piece keeps the GUID.
3. **Physical thickness as the T3D element width (owner-approved design).**
   - Width = `PanelSourceParameter.PhysicalThickness` when the panels carry it.
   - Otherwise `DefaultThickness`, else the layers' total, as before.
   - Constructions and materials are never touched.
   - One construction used at two physical thicknesses becomes two elements, because TAS holds the width on the element. The second gets the plan's existing stable `_XXXXXXXX` suffix, as the ground/air split already does.
   - Notes: each element's physical width against its construction's layer total, and the elements with no physical thickness when others have one.
4. **Split-off elements had no TBD construction.** `Modify.UpdateBuildingElements` finds a TBD construction by element name. `SIM_INT_SLD_FLR_D45F6E1D` matched none, so it was left with no construction.
   - When nothing else matches, the `_XXXXXXXX` suffix is stripped and the base name is looked up.
   - This also fixes the existing ground/air split, which had the same latent gap.
   - Every name that matched before still matches first, unchanged.

5. **Widths ON: every post-export match missed (2026-10-14, second commit).** With `UseBEWidths` ON, TAS treats each SAM polygon as its element's centre line and moves every zone surface half the element width into the zone. For example, an external wall 0.25 m wide lands 0.125 m in from the panel.
   - `Modify.UpdateIds` matched TBD surfaces to SAM panels, then apertures, by point-in-face within 1 mm. So nothing matched: 0 panel and 0 aperture stamps.
   - Then `UpdateApertureDefinitions` skipped all 50 parts ("carry no building element stamp"), and `SetApertureTypes` stopped all 25 at gate 3.
   - Identity was never lost. The per-aperture TBD elements keep the SAM aperture GUID in their names until the rebind, and the TBD apertures carry their zone-surface references. Only the geometric matcher could not see them.
   - Fixes:
     - **Aperture surface by name** (`Query.ApertureByElementName`). `UpdateIds` first decodes the SAM aperture GUID from the pane/frame element name (`Windows: SIM_EXT_GLZ <guid> -pane`), within the space's own apertures. Its host is then the panel holding it. Geometry is the fallback, unchanged.
     - **TBD aperture by stamp** (`Query.PaneStampMatch`). `SetApertureTypes` first matches the pane surface a TBD aperture was read from against the SAM apertures' pane stamps. Geometry is the fallback, unchanged.
     - **Opaque surface off its panel by its half width** (`Query.OffsetMatch`). This applies only when the exact test finds nothing: parallel (2°), at most half the element's width plus tolerance away, inside the panel once projected. The nearest one wins. A zero-width element (Air) is bounded by the widest half width in its zone, because TAS moves it with the surfaces round it.
     - **No centroid translation on a direct TBD.** Its zones carry `[SpaceGuid]`, and it keeps SAM coordinates. With widths ON, its shrunken shell gave a false few-cm shift.
   - No tolerance was raised globally. Nothing changes for a surface the exact test matches.
6. **Stair-void Air surface stamped onto the slab, both modes.** `Face3D.InRange` tests the outer boundary only, so the GF Hall's Air surface (#5) was stamped onto the holed UF slab, and the Air panel was never stamped. `Query.InRangeOutsideHoles` excludes a point inside a hole and is used by both panel tests.
   - Widths OFF stamps change only here: slab = UF Landing #18 / GF Hall #4; Air = UF Landing #19 / GF Hall #5.
   - TBD geometry, volumes and constructions are unchanged.
7. **OpeningProperties are optional.** An aperture without them (a fixed window, an ordinary door) requests no control and is valid.
   - Gate 4 is no longer an `ISSUE:` line per aperture (the house gave 25 warnings). It is counted in the one summary line: `25 TBD apertures seen, 25 matched to their SAM aperture, 25 with no OpeningProperties (...)`.
   - Unmatched apertures, a missing building element, a refused write and an undelivered schedule stay `ISSUE:` lines.
   - The summary is `Query.ApertureTypesSummary` (COM-free). No OpeningProperties are invented, and no aperture geometry, type or construction changes.

## Files

- `SAM.Analytical.Tas/Query/T3DImportPlan.cs` — level ≥ 0, hole split, physical-thickness width and element split, notes.
- `SAM.Analytical.Tas/Modify/UpdateIds.cs` — aperture surface by element-name identity first; no centroid translation on a direct TBD.
- `SAM.Analytical.Tas/Modify/SetApertureTypes.cs` — TBD aperture by pane stamp first; no per-aperture note for missing OpeningProperties.
- `SAM.Analytical.Tas/Query/Match.cs` — `OffsetMatch` fallback for building-element widths; `InRangeOutsideHoles`.
- `SAM.Analytical.Tas/Query/ApertureByElementName.cs`, `PaneStampMatch.cs`, `ApertureTypesSummary.cs` — new, COM-free.
- `SAM.Analytical.Tas.TM59.Tests/BuildingElementWidthsMatchTests.cs` — 22 new tests.
- `SAM.Analytical.Tas/Classes/T3DImportPlan.cs` — `T3DElementSpec.PhysicalThickness`.
- `SAM.Analytical.Tas/Modify/UpdateBuildingElements.cs` — base-name fallback for discriminated element names.
- `SAM.Analytical.Tas/DIRECT_T3D_ROUTE.md` — mapping rules updated.
- `SAM.Analytical.Tas.DirectT3D.Validation/SyntheticModels.cs` — `StackedZonesWithStairVoid` fixture (shared with the tests).
- `SAM.Analytical.Tas.TM59.Tests/DirectT3DRouteTests.cs` — 12 new tests: window level ×5, stair void ×1, physical thickness ×6.

## Validation (local, this machine)

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" SAM_Tas\SAM.Analytical.Tas\SAM.Analytical.Tas.csproj -p:Configuration=Release
dotnet test SAM_Tas\SAM.Analytical.Tas.TM59.Tests\SAM.Analytical.Tas.TM59.Tests.csproj -c Release
```

- `DirectT3DRouteTests`: **75 / 0** (63 before, plus 12 new).
- The full TM59 suite result is in the PR description.

**Licensed TAS (machine-local)**
- Harness: `SAM.Analytical.Tas.DirectT3D.Validation`, `workflow <model.sam> <dir> direct`, then `dump <tbd>` plus a TBD construction probe.
- House model: rebuilt through the test3.gh chain by a machine-local Rhino 8 harness from `morning-test\Untitled2_meters.3dm`.

| Run | Result |
|---|---|
| Before (sow code) | `success=False`, 2 door types refused, 2 openings lost. Reproduced exactly as reported. |
| Level fix only | Success: 11 zones, 56 surfaces, 25 openings. TBD 574.102 m³ / 230.013 m² (slab over the Air panel). |
| gbXML route, same model | 564.169 m³ / 226.688 m². Landing 15.5 m² / 46.112 m³, Hall 46.5 m³. |
| + hole split | **Identical to the gbXML route**: 564.169 m³ / 226.688 m², 11 zones, 57 surfaces (the holed slab is 2 pieces), 25 openings, 0 skipped. |
| + physical thickness, widths OFF (default) | 7 elements. Volumes unchanged (564.169 m³). Every element and its width below; every construction unchanged. |
| same, widths ON | 481.784 m³ / 209.270 m², the net room volumes from the mid-planes. See "Limitations". |

Element widths before and after, house model (the TBD constructions are byte-for-byte the same layers and U-values):

| T3D/TBD element | Before (construction) | After (Rhino physical) | TBD construction layers total / U |
|---|---:|---:|---|
| SIM_EXT_SLD (walls, gables) | 0.2077 | **0.25** | 0.2077 / 0.26 |
| SIM_INT_SLD_Partition | 0.0752 | **0.10** | 0.0752 / 1.775 |
| SIM_INT_SLD_FLR (UF ceiling) | 0.2925 | **0.15** | 0.2925 / 0.81 |
| SIM_INT_SLD_FLR_D45F6E1D (UF slab) | (same element) | **0.20** | `Floor: SIM_INT_SLD_FLR` |
| SIM_EXT_SLD_Roof | 0.3335 | **0.20** | 0.3335 / 0.164 |
| SIM_EXT_GRD_FLR | 0.3729 | **0.20** | 1.3729 / 0.219 |

- Zones are named after the SAM spaces (`GF Hall`, ..., `Attic Unconditioned`) when seed spaces are supplied. Without seeds they are `Cell 1..11`.
- With seeds and `SetDefaultApertureConstruction`: 8 internal doors are `Doors: SIM_INT_SLD`, and the front door `Doors: SIM_EXT_SLD`.

### Widths ON matching and OpeningProperties (second commit, 2026-10-14)

Same house input as above (`house.input.sam` from the Rhino harness, SHA-256 prefix `11FFF70C131B`), run through the real `WorkflowCalculator` with the harness: `workflow <house.json> <dir> direct [widths] name=house`. Output is under `C:\t3dv\h1014` (machine-local).

| | Widths OFF | Widths ON, before | Widths ON, after |
|---|---|---|---|
| Conversion | success, 11 zones, 57 surfaces, 25 openings, 0 skipped | same | same |
| TBD floor / volume | 226.688 m² / 564.169 m³ | 209.270 / 481.784 | 209.270 m² / **481.784 m³** |
| Panel + aperture stamps (`UpdateIds`) | 81 + 66 | **0 + 0** | 81 + 66, **identical to OFF** (zone names, surface numbers) |
| Aperture parts rebound to shared definitions | 50 / 50 | **0** (50 unstamped) | 50 / 50 |
| TBD apertures matched (`SetApertureTypes`) | 25 / 25 | **0** (25 at gate 3) | 25 / 25 |
| `ISSUE:` lines from aperture types | 0 (was 25 × gate 4) | 26 | **0** |
| Elements / widths | 14 after rebind; 0.25 / 0.10 / 0.15 / 0.20 / 0.20 / 0.20 | — | identical |

Per-zone volumes, OFF → ON (m³):
- GF Living 56.672 → 48.692; GF Kitchen/Dining 56.672 → 48.692; GF Hall 46.500 → 39.900; GF Study 33.422 → 27.972; GF Bathroom 33.422 → 27.972.
- UF Bedroom 1 50.436 → 43.512; UF Bedroom 3 50.436 → 43.512; UF Landing 46.112 → 39.900; UF Bedroom 2 38.907 → 33.152; UF Bathroom 38.907 → 33.152.
- Attic Unconditioned 112.683 → 95.328.

Further checks:
- **Constructions independent of widths.** `inputs` OFF vs ON TBD: every construction and material property is identical (1184 construction lines; only TAS-minted GUIDs differ). The new OFF TBD against yesterday's accepted OFF TBD (`run4`): 5476 identical, 174 expected (GUIDs), **0 unexpected**.
- **Opening controls, both modes** (`workflow loadsensitive <dir> direct [widths]`: 2 operable windows, 1 with a night-closed schedule):
  - 5/5 matched, 2 updated, 1 schedule requested and 1 read back;
  - the same elements carry the same controls, `Opening Cd0.448 F1` and `Opening Cd0.395 F1 S00FFFE`.
- **gbXML route unaffected:** house 25/25 matched and 50/50 rebound; load-sensitive 5/5, 2 updated, schedule read back.
- Licensed `synthetic`: 104 / 0.
- Tests:
  - `BuildingElementWidthsMatchTests` 22 / 0;
  - full `SAM.Analytical.Tas.TM59.Tests` **1166 / 0**;
  - `benchmark/SAM.Analytical.Tas.Benchmark.Tests` 23 / 0.
- Stamp comparison: a machine-local PowerShell extractor over `house.result.json`, with zone GUIDs mapped to space names and slot order ignored. Not committed.

## Limitations / open

- **Widths ON** is not the workflow default (`UseWidths = false`, `DIRECT_T3D_ROUTE.md` Q1).
  - It gives net volumes from these mid-plane panels: GF Living 17.39 m² × 2.8 m = 48.692 m³, against 18.891 m² × 3.0 m between mid-planes.
  - Its aperture and host matching now works (above). Making mid-plane models default to widths ON is an **owner decision** and is not changed here.
  - The widths-ON workflow note still describes SAM panels as inner surfaces. That is true of older models, not of PhysicalThickness mid-plane models. It is informational (no `ISSUE:`), and its wording is left for that decision.
- `OffsetMatch` assumes TAS offsets a surface by at most half its element's width, which is what was measured here. A surface pair closer together than that and parallel would resolve to the nearer panel.
- The Grasshopper acceptance definition was not re-run in Rhino and no working copy with a widths switch was made. The validation used the workflow harness on the same model. No assemblies were deployed.
- TAS GUI: the T3D and TBD were created, re-opened and exported through TAS COM on this machine. They were not opened interactively in the TAS3D window.
- Not run: hosted CI. The GenOpt tests are untouched.

## Next

Owner review. Merge order: SAM#194 → SAM_OCCT#76 → SAM_OCCT#77 → this PR (re-run CI after SAM#194 merges).
