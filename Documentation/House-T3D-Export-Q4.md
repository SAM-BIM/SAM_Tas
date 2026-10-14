# House model: direct T3D export and physical thickness — Q4 (2026-10-14)

Status: implemented on `feature/panels-by-solid-q4`; draft PR, owner review required. Not merged.
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

## Files

- `SAM.Analytical.Tas/Query/T3DImportPlan.cs` — level ≥ 0, hole split, physical-thickness width and element split, notes.
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

## Limitations / open

- **Widths ON** is not the workflow default (`UseWidths = false`, `DIRECT_T3D_ROUTE.md` Q1). It gives net volumes from these mid-plane panels: GF Living 17.39 m² × 2.8 m = 48.692 m³, against 18.891 m² × 3.0 m between mid-planes. But:
  - the existing post-export aperture steps then miss every opening ("gate 3: no SAM aperture face contains its internal point"), because the T3D geometry is offset;
  - the workflow already warns about this for widths ON.

  Making mid-plane models default to widths ON is an **owner decision** and is not changed here.
- TAS GUI: the T3D and TBD were created, re-opened and exported through TAS COM on this machine. They were not opened interactively in the TAS3D window.
- Not run: hosted CI. The GenOpt tests are untouched.

## Next

Owner review. Merge order: SAM#194 → SAM_OCCT#76 → SAM_OCCT#77 → this PR (re-run CI after SAM#194 merges).
