<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR7a-2: glazing swap and g-value from available glazing systems (evidence only)

Branch `spike/optimisation-glazing-swap` → base `sow/2026-Q4` (cut from `13d86686`). Record date: 2026-10-08.

Inputs of record: PR7a (`NATIVE_OPTIMISATION_PR7A_SPIKE.md`, SAM_Tas#89, merge `d393ffba`) and the **owner decisions of
2026-10-08** in its `PROJECT_PROGRESS.md` closeout (`13d86686`): no pane-property solve; the g-value comes from real
glazing systems as SAM calculates them; the glazing-swap test is approved. Plan of record: SAM_UI
`documentation/NativeOptimisation-Plan-ModelBindings.md` ("Follow-up: construction and glazing choice"). Choice shape:
PR6, SAM `documentation/OptimisationDefinition-Bindings-PR.md` (`options`, OPT610–OPT615).

## Current status

PR open, **not merged**. Evidence complete; awaiting PR CI and **owner review**: the owner decides the glazing method for
PR7b from this record. Nothing here is product code, nothing is wired into `RunNative`, and PR7b is not started.

## What was done

Licensed Tas on this workstation, one simulation at a time; every evaluation through `TasGenExecute.exe "<workspace>"`
with a fresh evaluation folder (Gate T), driven by PR7a's unchanged `Invoke-Pr7aEvaluation.ps1`. Inputs were **copies**:
the Systems Demo TBD (`pr7a\base-local`) and a SAM-generated Part O model (`000000_SAM_AnalyticalModel` `.sam`/`.tbd`,
10 zones, heating off). The pools were read as the SAM UI Glazing window builds them: the model, the default library,
"My glazing systems" (one system) and a `.tcd` loaded through "Load more glazing…" (SAM UI's cached conversion, 171
systems). Raw evidence (23 evaluation folders, `runs.jsonl`, pool tables, probe output, registry exports) stays local in
`C:\TasOut\pr7a2\`.

Spike files: `spikes/optimisation-pr7a2/swap.csx` (the swap in one TasGenExecute script) and the probe
`spikes/optimisation-pr7a2/probe/` (`pool`, `write`, `constructions`). See that folder's `README.md`.

## Answers

### 1. How SAM's glazing flow works (code reading)

**The calculator.** `ThermalTransmittanceCalculator.CalculateGlazing(IEnumerable<Guid>)`
(`SAM.Analytical.Tas/Classes/ThermalTransmittanceCalculator.cs:630`) runs one action in one temporary TCD document
(`Modify.Run(Action<TCD.Document>)`: `%TEMP%\SAM\<guid>.tcd`, created, used, deleted; needs an STA thread). Per system
(`:407`):

- It writes the system into TCD with `ApertureConstruction.ToTCD_Constructions` (`Convert/ToTCD/Constructions.cs`): a
  `"<name> -pane"` construction and, when the system has frame layers, a `"<name> -frame"` construction; each layer gets
  `Modify.UpdateMaterial(TCD.material, IMaterial)` and `width = layer thickness`; the pane/frame additional heat
  transfer is carried over. A panel `Construction` goes through `Construction.ToTCD`.
- It reads back, for a **transparent pane**: `GetGlazingValues()` through `Query.GlazingValues` → light transmittance
  `[0]`, **g = `[5]`**, both **rounded to 0.001** (`Core.Query.Round(…, Tolerance.MacroDistance)`); U = `GetUValue()[6]`
  (unrounded; for a transparent construction TCD fills only index 6). For an opaque pane: `Query.ThermalTransmittance`
  (horizontal flow, external).
- The **frame** is calculated the same way and returned as `ApertureGlazingCalculationResult` (frame U, and g/light if
  the frame is transparent).
- Timing on this workstation: one system 0.21–0.49 s (TCD start-up dominates), 6 systems 0.39 s, **171 systems
  2.94 s**, matching the hand-over.

**The Glazing window** (SAM_UI `WPF/SAM.Analytical.UI.WPF/Classes/Glazing/`):

- `GlazingSource`: one pool source as a `ConstructionManager` (systems + the materials they name): `FromModel`
  (`AdjacencyCluster.GetApertureConstructions`, the model's materials), `FromDefaultLibrary`
  (`DefaultApertureConstructionLibrary` + `DefaultMaterialLibrary`), `FromUserLibrary` ("My glazing systems",
  `Documents\SAM\User Libraries\Glazing Systems.json`), and `Query.ReadGlazingSource` for a loaded `.tcd`/`.json` (a TCD's
  transparent constructions become pane-only window systems, Guid kept, cached as JSON). Pool order `Rank`: model,
  library, user, loaded; **the first source of a Guid wins**.
- `GlazingCandidate`: one system; **identity is the Guid** (names repeat: the default library has several
  `SIM_EXT_GLZ`); `MaterialsToAdd` = the system's materials the model lacks; a same-named material with another
  definition blocks it (`MaterialIssue`).
- `TasGlazingEvaluator`: the calculator above on one STA worker, one batch per source; `GlazingValues` = Ug, g, light,
  Uf.
- `GlazingViewModel`: candidates of the current aperture type and the same transparency; filters **target Uw**
  (`Query.GlazingOverallThermalTransmittance`: area-weighted Ug/Uf, or 80/20 without geometry), **min/max g**, **min
  light**; sources library/loaded switchable; sort orders `OverallU` (default), `GLowest`, `GHighest`, `LightHighest`,
  `Name`, then Uw, Ug, current first; automatic choice only against a target Uw the current system misses (best Uw
  that meets it).

**The legacy "assign by g-value" flow** (`Modify.CalculateGlazing(UIAnalyticalModel)`):
`GlazingCalculationDataWindow` asks for a target g and light transmittance (`GlazingCalculationData`: ranges
`−0.02…0` around each, i.e. "at most the target, down to 0.02 below", thickness 0.001–1 m); the calculator runs on
**every** construction and aperture construction of the model's `ConstructionManager`; `Query.Criteria` marks each result
All/NotAll/None against the ranges; `DisplayGlazingCalculationResult.GetScore` = |score(target) − score(result)| with
`Tas.Query.Score` = 0.6·g + 0.4·light (each ×10 when both target and result have it); `AssignIndexes` orders All, NotAll,
None, then by score. The user picks one; `Tas.Modify.Update(cm, source, destination, replace)` copies it over the
destination (keeping the destination's Guid when replacing), then `UpdateConstructions`/`UpdateApertureConstructions`/
`UpdateThermalParameters`. **Quirk:** with only a g target, `GetScore` compares 10·g_target with 6·g + 0.4·light (the
light term is not dropped on the result side), so the ranking is biased by light transmittance. Do not reuse `Score` for
a g match; use |Δg|.

**`Modify.SetGlazing`** (SAM_UI `WPF/…/Modify/SetGlazing.cs`): one Undo step on a model clone: only the chosen
system and the materials it needs that the model lacks; the chosen system keeps its Guid and gets a unique name (`"<name>
2"`, …) when another aperture construction of the model has its name; scope AllUsing / SelectedOnly / DontAssign; the
apertures' parameters (U, g, light, solar, Pilkington) are refreshed from a Tas run of the chosen system
(`ThermalTransmittanceCalculator.Calculate`, rounded to 0.001); no pane material is ever created or edited.

**How a system reaches a TBD** (SAM_Tas): `Modify.UpdateConstructions(TBD.Building, IEnumerable<ApertureConstruction>,
MaterialLibrary)` (`Modify/UpdateConstructions.cs:118`), which `Modify.AddUnusedConstructions(building, cluster, …)`
calls for the systems held in a cluster. Names: `Query.Name(UniqueName(), …)` → **`"Windows: <name> -pane"`** /
**`"Windows: <name> -frame"`** (prefix from the aperture type; the Guid is dropped). An existing construction of that
name is **reused and overwritten**. Layers are written by `UpdateConstruction` (`RemoveMaterials`, `AddMaterial`,
`UpdateMaterial(TBD.material, …)`, `materialWidth[i]`).

### 2. g parity: **exact (bit for bit)** for every system, once written under its own name

`Pr7a2Probe write` on TBD copies (SAM model and Systems Demo), 8 systems spanning g 0.118–0.792: model `SIM_EXT_GLZ`,
library `SIM_EXT_GLZ` (7 mm air), `SIM_EXT_GLZ_SKY`, `SIM_INT_GLZ`, the "My glazing systems" triple, and three systems
of the loaded `.tcd` (called TCD-A/B/C here; their EDSL names are not recorded).

| System | g calculator (0.001) | g TCD (raw) | g TBD `"… -pane"` | U TCD = U TBD | light |
|---|---|---|---|---|---|
| TCD-A (6/12 air/6, solar control) | 0.118 | 0.11761055886745453 | identical | 2.3429 | 0.073 |
| TCD-B (6/12 air/6) | 0.284 | 0.28368183970451355 | identical | 1.6994 | 0.797 |
| My glazing systems triple | 0.369 | 0.36886733770370483 | identical | 0.9976 | 0.728 |
| Model `SIM_EXT_GLZ` | 0.400 | 0.4001609981060028 | identical | 1.2434 | 0.804 |
| Library `SIM_EXT_GLZ` (7 mm air) | 0.406 | 0.40626540780067444 | identical | 2.2377 | 0.804 |
| TCD-C (6/12 air/6) | 0.495 | 0.49532291293144226 | identical | 2.8312 | 0.438 |
| Library `SIM_EXT_GLZ_SKY` | 0.548 | 0.5478138327598572 | identical | 1.6901 | 0.610 |
| Library `SIM_INT_GLZ` (single 10 mm) | 0.792 | 0.7917975187301636 | identical | 5.5556 | 0.870 |

All nine `GetGlazingValues()` and all `GetUValue()` values (pane and frame) are bit-identical between TCD and TBD, on
both TBDs, also after save and re-open. The calculator's g differs from the raw TCD g only by its 0.001 rounding (max
0.0005 over all 179 pool systems). Layer differences found and harmless: TBD sets `materialWidth[i]` (TCD leaves 0);
transparent layers get density/specific heat 0 in TBD vs NaN in TCD; a **gas layer laid thicker than its material's
default thickness keeps the default `width` in TBD** (`UpdateMaterial(TBD.material, GasMaterial)` writes
`DefaultThickness`; TCD writes the layer thickness): tested with a 16 mm gap of a 12 mm argon material, g and U still
identical in TCD and TBD (0.36886733770370483 / 0.997646152973175) because the gap uses its fixed heat-transfer
coefficient (1.403 W/m²K). **Conclusion: the g SAM shows in the Glazing window is the g the TBD simulates.**

**Trap found: a same-named system overwrites.** Without a unique name, writing the library `SIM_EXT_GLZ` (7 mm air) into
the SAM model's TBD reused `"Windows: SIM_EXT_GLZ -pane"`, the construction **used by the model's two glazing
elements**, and replaced its layers: the baseline's g silently became 0.4063 (from 0.4002) and U 2.24 (from 1.24).
`UpdateConstructions` matches by name only. **PR7b must write every candidate under a name no construction of the TBD
has**; the probe's `--rename` (`"<name> <last 6 of Guid>"`, SAM_UI's `GlazingCandidate.ShortId`) worked for all 16
writes.

**Trap found: an unstable library Guid.** SAM's default `SAM_ApertureConstructionLibrary.JSON` (SAM
`files/resources/Analytical/`, also the installed copy) has a `SIM_EXT_GLZ` window with the malformed Guid
`4d00dd0-f646-…` (7 hex digits in the first group); it is parsed as a **new random Guid on every load**, so that system
has no stable identity across sessions (a choice option or the window's pinned choice cannot refer to it next time).

### 3. Swap mechanics (licensed): **AssignConstruction on the elements works, exact and repeatable**

**Before the run** (outside TasGenExecute, which cannot reference TCD): the candidates were written into the workspace
TBD as unused constructions with `Modify.UpdateConstructions(building, apertureConstructions, materialLibrary)` under
unique names, the TBD saved (0.15 s for 8 systems). Names created, e.g. `"Windows: SIM_EXT_GLZ b9d885 -pane"` /
`"… -frame"`; pane-only systems (TCD) get no frame construction. The unused constructions change nothing: the Demo
baseline in that TBD equals PR7a's exactly (cooling 2 978.53252598965 kWh, heating 15 227.8406637096 kWh).

**In the script** (`swap.csx`, C# 7.0, TBD interop only):

1. Elements: every `GetBuildingElement(i)` whose `GetConstruction().name` equals the target exactly (ordinal) — the
   Demo: `Lower Window-pane`, `Upper Window-pane`, `Curtain Wall-pane` → `"Suncool Example"`; the SAM model: two
   `GLAZING` elements → `"Windows: SIM_EXT_GLZ -pane"`. Reading through the elements avoids the `GetConstruction(i)`
   null gaps; the candidates are found by `GetConstructionByName` (which finds constructions past the gaps).
2. Frames paired by element name, `"<base>-pane"` → `"<base>-frame"` (Demo: `Lower Window-frame` etc., construction
   `"Frame"`, shared with the rooflight, door and shade frames, which stay untouched). On the SAM model one pane element
   (`"Windows: SIM_EXT_GLZ_284F92C9 -pane"`) has **no frame element of its own**; the frame element present uses
   `"SIM_EXT_GLZ -frame"` (no `"Windows: "` prefix), while the `"Windows: SIM_EXT_GLZ -frame"` written by the export is
   unused. So frames must be found through the elements, never by construction name.
3. `buildingElement.AssignConstruction(candidate pane)` on every pane element, and (when the candidate has a frame and
   `SwapFrame` ≠ 0) `AssignConstruction(candidate frame)` on the paired frame elements; read back through
   `GetConstruction().name`; the applied g/U/light read from the assigned construction (`GetGlazingValues()[5]`,
   `GetUValue()[6]`) equal the catalogue's to the float.
4. `save`, `simulate(1, 365, 0, 1, 0, 0, tsd, 1, 0)`, TSD read as PR7a, plus the zones' annual `solarGain` and
   `externalConductionGlazing`.

Results, Systems Demo, building only, 8 options ordered by g (the `"Suncool Example"` baseline first):

| # | System | g | U pane | light | Solar gain (kWh) | Glazing conduction (kWh) | Heating (kWh) | Cooling (kWh) | > 25 °C (h) |
|---|---|---|---|---|---|---|---|---|---|
| – | Suncool Example (current) | 0.3367 | 1.055 | 0.642 | 15 241.46 | −6 986.60 | 15 227.84 | 2 978.53 | 274 |
| 1 | TCD-A | 0.1176 | 2.343 | 0.073 | 8 805.92 | −9 993.74 | 20 696.32 | **1 846.73** | 172 |
| 2 | TCD-B | 0.2837 | 1.699 | 0.797 | 13 624.34 | −8 694.05 | 17 394.40 | 2 554.79 | 242 |
| 3 | My glazing systems triple | 0.3689 | 0.998 | 0.728 | 16 399.68 | −7 067.58 | 14 581.64 | 3 328.99 | 360 |
| 4 | Model `SIM_EXT_GLZ` | 0.4002 | 1.243 | 0.804 | 16 941.95 | −8 045.52 | 15 100.66 | 3 251.20 | 378 |
| 5 | Library `SIM_EXT_GLZ` 7 mm air | 0.4063 | 2.238 | 0.804 | 16 987.16 | −11 755.18 | 17 480.72 | 2 951.65 | 270 |
| 6 | TCD-C | 0.4953 | 2.831 | 0.438 | 19 201.17 | −13 599.70 | 18 026.31 | 3 221.12 | 310 |
| 7 | Library `SIM_EXT_GLZ_SKY` | 0.5478 | 1.690 | 0.610 | 21 237.02 | −10 528.55 | 14 848.40 | 3 946.02 | 618 |
| 8 | Library `SIM_INT_GLZ` (single) | 0.7918 | 5.556 | 0.870 | 29 223.24 | −25 733.66 | 21 515.82 | 3 897.00 | 508 |
| 8 | same, frames kept (`SwapFrame=0`) | 0.7918 | 5.556 | 0.870 | 29 223.24 | −25 873.50 | 21 166.14 | 3 934.92 | 508 |

- **Expected direction: yes.** Solar gain rises strictly with g (8 806 → 29 223 kWh); glazing conduction follows U.
  Cooling and overheating are **not monotonic in g**, because a whole-system swap also changes U and light: options 4
  and 5 have the same g (0.400 / 0.406) but U 1.24 / 2.24 → cooling 3 251 / 2 952 kWh (−9 %), heating 15 101 / 17 481 kWh
  (+16 %).
- **Repeatable bit for bit:** baseline ×2, option 4 ×2 (Demo), option 8 ×2 (SAM model): every output identical.
- **SAM model** (`Windows: SIM_EXT_GLZ -pane`, threshold 28 °C): baseline cooling 2 523.75187299657 kWh, 32 h (= PR7a);
  option 4 — the model's **own** system re-written under a new name — gives **exactly** the baseline (every output),
  which proves the swap equals the original assignment; option 1: solar gain 13 664 → 1 925 kWh, cooling 2 069 kWh, 8 h;
  option 8: 31 218 kWh, 3 060 kWh, 94 h.
- **Frame:** swapping the frame with the pane (SAM's Set glazing applies the whole system) changed heating by 350 kWh
  for option 8 (frame U 2.94 vs the Demo's 2.20); solar gain and overheating unchanged.
- **Cost:** edit 0.21–0.24 s; Demo evaluation 11.9–12.4 s, SAM model 8.3–8.5 s (as PR7a building only).
- **Errors:** an option number outside 1..n is refused in the script (`Error.txt`: "GlazingOption 9 is not an option
  number 1..8.", exit 0, no lock, 2.5 s).
- Every evaluation: exit 0, no locked file, no Tas process left (COM servers gone within 0.3 s), TasManager registry
  export identical (`695CCC5E…`) before and after both batches (and equal to PR7a's).

**A better TBD-only way?** The alternative, copying the candidate's layers into the existing target construction, would
change the model's construction in place (the owner ruled out editing pane properties), needs material-by-material
copying in C# 7.0 and loses the original. Assigning a prepared construction to the elements is simpler, exact, leaves
every construction intact, and is what Tas itself does when a user picks another construction for an element.
**Recommended.**

### 4. The g-value target method (proposal, with the licensed mini-run above)

**(A) Snap** (continuous g; each evaluation applies the closest available system, ties by the U closest to the current
system, then the lower option number; the applied g reported). Proven in the script: `GlazingG` 0.30 → option 2,
0.403 → option 4, 0.45 → option 5, each bit-identical to running that option directly. But the objective an optimiser
sees is a **step function**: plateaus between the midpoints of neighbouring options (0.2006, 0.3263, 0.3845, 0.4032,
0.4508, 0.5216, 0.6698 for the table above), flat inside each, with jumps that are not ordered by g (U and light jump
too). Replaying the measured cooling demand through the kernel's methods:

| Method on the snapped g in 0.1–0.8 | Evaluations | Distinct systems simulated | Result |
|---|---|---|---|
| Golden section (20 evaluations) | 20 | 4 | option 1 (happens to be the best; unimodality does not hold) |
| Hooke–Jeeves from 0.45, step 0.05 | 9 | 3 | **option 5 (2 951.65 kWh), a local plateau**; the best is option 1 (1 846.73 kWh) |
| Try every option | 8 | 8 | option 1, exact, with the whole table |

Snap wastes simulations on repeats of the same system, can stop on a plateau, and reports an "optimum g" that is not
what was simulated. The repeats could be cached, but the plateaus and the U/light jumps remain.

**(B) g-ordered choice** (a `"discrete"` variable over the systems whose g lies in the requested range, numbered 1..n in
g order, PR6 `options`; run by "try every option"): n simulations for n options, exact, no plateau, the whole table
(g, U, light, objective per system) is the result, and the best is a real system that Apply best design can apply as
it is. **Recommended for PR7b; the window and the AI text offer only (B).** (A) is not offered (the owner's decision 3
removes the pane solve that made a continuous g exact; snapping over a few real systems adds nothing over trying them).

**U and light transmittance change with the system.** A g range alone lets a single glazing (U 5.56) or a dark glass
(light 0.07) in. Proposed filter for building the options (defaults the user can change; all are already filters of the
Glazing window):

- the current system's aperture type and transparency (the window's rule);
- g within the requested range;
- **Ug ≤ Ug(current) + 0.3 W/m²K** (one-sided: never much worse insulation; use the window's Uw when apertures carry
  geometry);
- **light ≥ light(current) − 0.1** (the window's "min light");
- systems with the same g, U and light to 0.001 counted once (the default library has two identical `SIM_EXT_GLZ`);
- at most **8 options** (each costs one simulation); the current system always included as option 1, so the table
  compares with it.

Effect on the pools (all four sources, transparent windows): half of the 171 loaded TCD systems are single glazing
(U ≥ 5.68). With Ug ≤ current + 0.3, only 5 systems remain for the Demo (current U 1.06; g 0.341–0.426) and 10 for the
SAM model (current U 1.24; g 0.206–0.461); with no allowance (Ug ≤ current) 1 and 3.
**A useful g range needs the user's own systems** ("My glazing systems", built with the Glazing System Builder), which is
where the owner's 8 Oct follow-up already points the choice.

### 5. Catalogue and kinds (proposal)

- **Kind:** `tbd.glazing-construction.choice` (PR6 placeholder, accepts options), display "Glazing system", reference
  key **`glazingConstruction`** = the exact name of the TBD transparent construction used by building elements (the
  catalogue lists those through the elements: Demo `"Suncool Example"`, SAM model `"Windows: SIM_EXT_GLZ -pane"`).
  Variable `"discrete"`, numbered 1..n, no unit/quantity (the number is an option index). Needs a building simulation
  (chain rule as PR7a).
- **`tbd.glazing-construction.g-value`:** not offered in PR7b's capabilities (no solve, no snap). The key stays
  reserved. **No PR6 placeholder must change**: the PR6 fixture `zone-setpoints-glazing-hooke-jeeves.json` stays valid
  as shape (it is checked against test capabilities); against SAM_Tas' capabilities that kind is simply unknown.
- **Options** are text (PR6). Proposed text: the system's name, with the 6-character short id appended only when two
  pool systems share the name (`"SIM_EXT_GLZ b9d885"`); option 1 = the current glazing (its TBD construction name). The
  catalogue entry for each option carries what the window shows: g, Ug, light, Uf, source (Model / Default library /
  My glazing systems / file), frame or not.
- **Who supplies what:** SAM_UI passes the pool (`GlazingSource` → `ConstructionManager` per source, as the Glazing
  window has it) and the chosen systems' Guids; SAM_Tas calculates g/U/light with `ThermalTransmittanceCalculator.
  CalculateGlazing` (STA; ~3 s for 171) when SAM_UI does not pass the values it already has (`TasGlazingEvaluator`
  gives the same numbers); before the run SAM_Tas writes the chosen systems into the workspace TBD under unique names
  (`UpdateConstructions`), and the generator writes the option table (TBD pane/frame names, g, U, light) into the
  script; the script applies the option and reports the applied g/U/light read back from the TBD.
- **Units/ranges:** option index 1..n; per option g 0–1, U W/m²K, light 0–1 (display only). Default filter as in 4.
- **Still to build (outside SAM_Tas):** PR6 keeps `"discrete"` reserved (OPT415) and there is no "try every option"
  method yet; a choice needs both in SAM (kernel method + capability) before PR7b can run one end to end.
- **PR9 (Apply best design):** the best option is a real system with a Guid and a source, so applying it is exactly the
  Glazing window's Apply: `Modify.SetGlazing` with `SourceApertureConstructionGuid` = the current system, the chosen
  system and its source's `MaterialsToAdd`, scope AllUsing. Nothing new is needed for SAM models. (A TBD-only project
  like the Systems Demo has no SAM system to replace; applying there would mean writing the swap into its TBD, out of
  scope.)

## Carried from PR7a for PR7b (settled, so PR7b can start from this record)

- **Setpoint blocks:** internal condition heating = thermostat `ticLL`, cooling = `ticUL`; value profile → `value`
  (setback and schedule kept); 24-hour profile → the highest (heating) / lowest (cooling) hours rewritten, the setback
  hours kept; factor ≠ 1 or another profile type refused. Targets per internal condition (owner decision 1); names exact
  (a Demo name has a trailing space).
- **F1:** a plant evaluation without a building simulation re-points the TSD's `SimulationData.buildingPath` at the
  evaluation's TBD before `FixTSDPath`.
- **F2:** read every TPD result before releasing the TPD; the TPD.exe shutdown crash (~17 %) is ignored (owner decision
  2); allow ~5 s more per plant evaluation.
- **TasGenExecute code rules:** C# 7.0 exactly; `using System.Linq;` written out; BCL + TBD/TSD/TPD interops only (no
  TCD, no `#r`); TSD daily arrays are 1-based SAFEARRAYs (enumerate them); `GetConstruction(i)` has null gaps; every COM
  document closed and released in PR7a's order.
- **Measures** as PR7a question 5–6 (heating/cooling demand ÷ 1000 = kWh; overheating = worst occupied zone, resultant
  > threshold; plant energy/cost/CO2 from the TPD annual result set).
- **PR6 fixture:** `systems-demo-bound-golden-section.json` `"Plant Room 1"` → `"Plant Room"` (test data).

## PR7b proposal (glazing part; the rest as PR7a's proposal)

| Kind | Name in the window | Reference keys | Options | Needs |
|---|---|---|---|---|
| `tbd.glazing-construction.choice` | Glazing system | `glazingConstruction` | systems from the pool after the filter (≤ 8), option 1 = current, numbered in g order | building simulation; SAM: `"discrete"` + "try every option" |

Blocks: (outside the script) write the chosen systems into the workspace TBD under unique names; (script) find the target
elements and their paired frames, assign the option's pane/frame constructions, read back, report applied g/U/light.

**Owner decisions for PR7b:**

1. **Method:** (B) g-ordered choice run by "try every option" (recommended), not (A) snap; `g-value` not offered.
2. **Filter defaults:** Ug ≤ current + 0.3 W/m²K, light ≥ current − 0.1, at most 8 options, current as option 1.
3. **Frames:** swap pane and frame together when the system has a frame (recommended, as Set glazing), or keep the
   model's frames to isolate the glazing.
4. **Order:** the SAM work ("try every option" method, `"discrete"` runnable) before or with PR7b.

## Files changed

- `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7A2_GLAZING.md` (this record).
- `SAM_Tas/spikes/optimisation-pr7a2/`: `README.md`, `swap.csx`, `probe/{Pr7a2Probe.csproj, Program.cs}`. Not in
  `SAM_Tas.sln`; no product file changed; PR7a's files unchanged.

## Validation

- 23 TasGenExecute evaluations (Demo 17, SAM model 6), all exit 0 except the intended invalid-option refusal (exit 0
  with `Error.txt`); no lock, no leftover process; registry export identical before/after each batch.
- Probe: 2 pool runs (179 systems), 4 TBD writes (3 parity + the 16 mm gap variant), 2 construction listings; TCD
  processes exited after each run.
- Repeats bit-identical (Demo baseline, Demo option 4, SAM option 8); snap targets equal the directly chosen options.
- Inputs were copies; `pr5-acc` and `pr7a\base` were only read. Probe built with `dotnet build` from a copy outside the
  repository against the sibling build folders (0 warnings, 0 errors).
- `git diff --check` clean; SPDX headers on the new files.

## Unresolved issues and risks

- **Name collision** in `UpdateConstructions` (by name, overwrites): PR7b must rename; a product fix in SAM_Tas
  (refuse or rename on a name clash) is worth a separate issue.
- **Malformed Guid** in SAM's default aperture construction library (`4d00dd0-…`): unstable identity; a one-line SAM
  resource fix, separate PR.
- `Tas.Query.Score` bias when only g is targeted (legacy window); not used by the proposal.
- The 171-system TCD pool is mostly old glazing; the useful options will come from "My glazing systems".
- A choice is not runnable until SAM implements `"discrete"` and "try every option".
- The SAM-generated model has heating off (Part O): heating effects are shown on the Demo only.

## Next step

1. PR CI (`build`, `spdx`) green on the head.
2. **Owner review**: the method (B vs A) and the four decisions above. Merge (merge commit, `--match-head-commit`) only
   after approval.
3. `PROJECT_PROGRESS.md` closeout on SAM_Tas `sow/2026-Q4` with the merge SHA.
4. Then, only with the owner: the PR7b prompt (catalogue reader, script generator, engine `tas-model`, glazing choice).
