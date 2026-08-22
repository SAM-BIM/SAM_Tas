# Reusable aperture definitions on the gbXML/T3D route

The standard Grasshopper and SAM_UI workflow — `ToGbXML → WorkflowgbXML` — now gets the same reusable
aperture definitions the direct `SAMAnalytical.TBD` export has had since Stage 2. Stage 2
(`APERTURE_DEFINITION_REUSE.md`) explicitly scoped itself to the direct `Modify.Update` export and declared
this route out of scope, so this closes a **known gap, not a regression**.

**The direct route is the behavioural reference throughout.** Success is defined as the two routes producing
the same aperture definition structure for the same analytical model, and that is what the licensed
acceptance below measures.

---

## Why the gap existed

On this route **SAM_Tas does not write the TBD.** TAS's own `T3DDocument.ExportNew` does, from a T3D in
which every aperture is its own `window`:

```text
SAMAnalytical.ToGbXML          SAM_gbXML   Convert/TogbXML/Opening.cs:38
                               opening.Name = "{name} [{aperture.Guid}]"   ← the physical GUID enters here
SAMAnalytical.WorkflowgbXML    SAM_Tas     WorkflowCalculator.Calculate
  Importing gbXML                t3DDocument.TogbXML(...)          → one T3D `window` per aperture
  Updating T3D file              Query.UpdateT3D(...)              → decodes that GUID back to the aperture
  T3D to TBD                     t3DDocument.ExportNew(...)        → TAS writes the TBD
```

Three facts make it unavoidable upstream:

- **The gbXML opening name has to carry the aperture GUID.** `Query.UpdateT3D` decodes it back to the SAM
  aperture to write colour, frame percentage and window position type. Removing it would break that.
- **The T3D cannot be canonicalised first.** `Interop.TAS3D` exposes no surface or opening object at all —
  `TAS3D.Building` offers only `GetWindow`/`AddWindow`/`GetElement`/… and `TAS3D.window` only `Delete()` plus
  scalar properties. There is no way to repoint a surface's opening at a shared window.
- **Nothing afterwards collapsed the result.** `Modify.UpdateBuildingElements` only ever SPLITS a diverged
  aperture off a shared element; it never merges. `Modify.SetApertureTypes` reuses `ApertureType`s by value,
  which is why aperture types on this route were already correct.

Measured on the licensed fixture, the raw TAS conversion produces **28 aperture building elements for 14
windows and no constructions at all**:

```text
Windows: SIM_EXT_GLZ 05f4bc0b-8f7b-4a84-b884-a5715883dda6 -pane
Windows: SIM_EXT_GLZ 05f4bc0b-8f7b-4a84-b884-a5715883dda6 -frame
… one pair per aperture, one surface each
```

Constructions were already *partly* shared by accident: `Modify.UpdateConstructions` (called at the top of
`UpdateBuildingElements`) creates `SIM_EXT_GLZ -pane`/`-frame` from the SAM `ApertureConstruction`, and the
word-set fallback in `UpdateBuildingElements` then assigns those to the per-aperture elements. **The
definitional gap was the building elements.**

---

## What was added

One gbXML-gated step in `WorkflowCalculator`, **after `Modify.UpdateIds`**:

```text
Updating Building Elements        Modify.UpdateBuildingElements     (unchanged)
Updating Ids                      Modify.UpdateIds                  (unchanged)
Reusing Aperture Definitions      Modify.UpdateApertureDefinitions  ← new, gbXML route only
Updating Thermal Parameters       …
Updating Aperture Types           Modify.SetApertureTypes           (unchanged)
```

**Running after `UpdateIds` is what keeps it small.** That step has just stamped every aperture's
`Pane/FrameZoneSurfaceReference` and its current `Pane/FrameBuildingElementGuid`, so the pass reads which
surfaces an aperture owns and which element they are on rather than re-deriving either from geometry. A
later `UpdateIds` re-derives the same stamps from the actual bindings, so the order stays idempotent.

Per aperture, per part:

1. **`Query.ApertureRebindKeys`** — Stage 3, unchanged — yields the COMPLETE physical surface set or a
   refusal. A two-sided aperture moves as one set or not at all; a surface claimed by two apertures refuses
   rather than being guessed at.
2. **`Modify.ResolveApertureDefinition`** — the Stage 2 resolve-or-create block, **extracted verbatim from
   `Modify.Update`** so both routes resolve through one resolver rather than two copies of the same equality,
   naming and reserve/register discipline.
3. If the resolved element is already the stamped one, nothing is written. Otherwise every surface in the set
   is rebound and `Pane/FrameBuildingElementGuid` re-stamped.
4. The leftovers are swept.

### It introduces no new equality rules

`ConstructionDefinition`, `BuildingElementDefinition`, their signatures, the seed gates and the two naming
functions are all Stage 2's, untouched. `Query.ApertureDefinitionBindings` is a COM-free mirror that asks
what each aperture part wants **through those same factories**, so the test suite can assert definition
counts without an installed TAS and cannot drift from what the resolver does.

---

## Invariants

1. **Physical instances are never merged.** N apertures keep N pane and N frame `zoneSurface`s.
   `{ZoneGuid, SurfaceNumber}` remains physical identity and is not touched by this pass;
   `BuildingElementGuid` remains a reusable-definition binding that many apertures legitimately share.

2. **Nothing named after a physical aperture is ever ADOPTED as a shared definition.**
   `Query.NamesContainingApertureGuid` tests every seeded construction and element name against the model's
   own aperture GUIDs — an exact test, not a pattern guess — and
   `BuildingReuseCache.RefuseSeededDefinitions` refuses the matches while keeping their NAMES reserved. TAS's
   per-aperture objects can pass every content gate, so without this the cache would hand one over and twenty
   windows would share a definition named after whichever came first. **Sharing and instance-naming are
   mutually exclusive:** an instance-named definition can never be found again by anything but itself.

3. **Pane and frame can never collapse**, however identical their layer content: `AperturePart` is a field of
   both definitions and `Query.BEType` differs (`GLAZING` vs `FRAMEELEMENT`).

4. **A refusal creates no orphan and moves no surface.** The complete surface set is validated before any
   definition is created, reserved or written.

5. **The sweep is conservative in three ways.** `Query.UnusedApertureBuildingElementGuids` marks an element
   only when it is an APERTURE element, holds NO surface, and is not one of the canonical elements this pass
   resolved onto — so an aperture whose rebind was refused never loses the definition that refusal was
   conservative about. `TBD.Building` has no `RemoveBuildingElement`, so removal is `markDelete` plus
   `Building.DeleteMarkedBuildingElements`.

6. **An unreferenced aperture construction is removed only on two grounds** — it names a physical aperture,
   or this pass superseded it (`Query.OrphanApertureConstructionNames`). Anything else unreferenced is a
   library definition with no window using it right now, and the export has always kept those; those are
   reported rather than removed.

7. **A freed plain construction name is reclaimed** (`Query.SupersededConstructionRenames`). This is not
   cosmetic: the aperture import pairs a window's two halves by the base name left after stripping
   `-pane`/`-frame` (`Convert.ToSAM_AdjacencyCluster`), so a qualified frame beside a plain pane makes the
   import produce **one aperture per surface instead of one per window**. Renaming is safe only here — the
   construction is one this pass created moments earlier, elements reference it by COM identity rather than
   by name, and the document has not been saved. Two guards: the plain name must actually have been removed,
   and exactly one construction must have wanted it.

---

## Why `DeleteMarkedBuildingElements`'s return value is ignored

Licensed TAS returns **-1** after successfully sweeping 28 elements. It is a status, not a count. The pass
therefore establishes what happened by re-reading the building and seeing which of the marked GUIDs are gone.

## Why the re-stamp does not use `AdjacencyCluster.GetAperture(guid, out panel)`

That overload returns as soon as it finds the aperture as a cluster OBJECT and leaves its `out panel` null —
which on this route is every aperture, so every re-stamp refused and every aperture kept a stamp naming an
element the sweep had just deleted. The pass builds its own aperture → panel index and writes **both** shapes
an aperture can be held in, because a stale copy is a stamp that names a deleted element.

## The one content disagreement this pass works around

`Modify.UpdateConstruction` sets `material.width` **only for a TRANSPARENT material**, so the frame
construction `Modify.UpdateConstructions` writes earlier in the workflow keeps the material-library default
there (0.001 m) while `construction.materialWidth` carries the real thickness (0.05 m). A
`ConstructionLayerDefinition` compares BOTH widths, so the resolver correctly declines to adopt it and
creates its own — whose content matches the direct route field for field. Fixing the shared writer instead
would change opaque material content on every route and is out of scope here; the consequence is handled by
invariants 6 and 7 above, and the end state matches the direct route.

---

## What each file does

| File | Role |
|---|---|
| `Modify/ResolveApertureDefinition.cs` | **The one aperture-definition resolver**, extracted from `Modify.Update`. Both routes call it. |
| `Modify/UpdateApertureDefinitions.cs` | The new COM pass: rebind, re-stamp, sweep, reclaim. Reports through `notes`. |
| `Query/ApertureDefinitionBindings.cs` | COM-free mirror of what each aperture part asks for, via the Stage 2 factories. |
| `Classes/ApertureDefinitionBinding.cs` | One (aperture, part) pairing with the definitions it states. |
| `Query/NamesContainingApertureGuid.cs` | The instance-named test, exact against the model's aperture GUIDs. |
| `Query/UnusedApertureBuildingElements.cs` | The element sweep decision, pure. |
| `Classes/ApertureBuildingElementUsage.cs` | An element and its post-rebind surface count, read out of COM into a value. |
| `Query/OrphanApertureConstructionNames.cs` | The construction sweep decision, pure. |
| `Query/SupersededConstructionRenames.cs` | Which freed plain names may be reclaimed, pure. |
| `Query/ZoneSurfaceIndex.cs` | `{ZoneGuid, SurfaceNumber}` → `zoneSurface`, hoisted out of `UpdateBuildingElements` so both rebinding passes resolve a stamp to the same object. |
| `Classes/BuildingReuseCache.cs` | **Extended** with `RefuseSeededDefinitions`. Purely additive. |
| `Classes/WorkflowCalculator.cs` | One gated step, and the step count. |

---

## Front-end reachability

Both front ends construct the same `WorkflowCalculator`, so **both inherit this automatically** and neither
`SAM`, `SAM_UI` nor `SAM_gbXML` needed changing:

```text
Grasshopper  SAMAnalytical.WorkflowgbXML   SAM_Tas_Grasshopper/…/SAMAnalyticalWorkflowgbXML.cs:354
  → Modify.RunWorkflow → SAM.Analytical.Tas.WorkflowCalculator.Calculate → TAS/TBD

SAM_UI       simulate cases                SAM.Analytical.UI.WPF/Modify/CreateSimulateCases.cs:75
  → Modify.RunWorkflow  (writes the gbXML itself: RunWorkflow.cs:187-205)
  → SAM.Analytical.Tas.WorkflowCalculator.Calculate → TAS/TBD
SAM_UI       multitasker workflow          SAM.Analytical.UI.WPF.Grasshopper/…/SAMAnalyticalMultitaskerWorkflow.cs:356
  → the same
```

SAM_UI's *other* TAS export (`Modify/Export.cs:46` → `Tas.Convert.ToTBD`) is the direct route and was already
correct.

---

## Acceptance

### COM-free (`SAM.Analytical.Tas.TM59.Tests/ApertureDefinitionReuseGbXMLTests.cs`, runs in CI)

19 tests, inside a suite of **438/438** passing in Debug and Release:

- 20 identical windows → 40 physical bindings, **2** element definitions and **2** construction definitions,
  and no generated name carries a physical aperture GUID;
- one diverging opening control → exactly **3** definitions, the new one bound by that aperture alone, the
  other 19 still on the shared original, the frame definition untouched;
- restoring it → back to **2**, resolving to the ORIGINAL definition, with no duplicate equivalent left;
- a frame given the pane's own layers → still 2 definitions, distinct `BEType`s, distinct names;
- a repeated run → the same definitions by value and the same names, so the second run creates nothing;
- instance-named detection in both GUID spellings and either case, and *not* on definition-named ones;
- the element sweep: only surface-less, non-canonical aperture elements; a canonical element left empty by a
  refusal is kept; a panel element is never a candidate;
- the construction sweep: instance-named and superseded removed, library definitions kept and reported, a
  still-referenced construction never touched;
- the reclaim: a freed plain name taken by the qualified construction and the bases matching afterwards; a
  name still in use never taken; a contested name given to neither;
- part selection: a frameless window asks for a pane definition only, matching the direct route's own rule.

### Licensed (EDSL Tas, `ModelA.sam` — 2 spaces, 14 apertures, one shared `ApertureConstruction`)

A/B against the `fff9984` baseline. The two builds differ in `SAM.Analytical.Tas.dll` and nothing else —
every other DLL verified hash-identical. One `WorkflowgbXML`-shaped run each.

| | A baseline | B this branch | direct route |
|---|---:|---:|---:|
| zones / total surfaces | 2 / 40 | 2 / 40 | 2 / 40 |
| aperture pane / frame surfaces | 14 / 14 | 14 / 14 | 14 / 14 |
| **aperture buildingElements** | **28** | **3** | **3** |
| aperture constructions | 2 | 2 | 2 |
| aperture types | 2 | 2 | 2 |
| distinct pane bindings (14 apertures) | 14 | **2** | — |
| distinct frame bindings | 14 | **1** | — |
| distinct physical surfaces / collisions | 28 / 0 | 28 / 0 | — |
| TBD apertures the import reports | 14 | 14 | — |

**B's aperture definitions are identical to the direct route's, name for name and surface count for surface
count:**

```text
Windows: Windows: SIM_EXT_GLZ -frame            BEType 15   14 surfaces   0 aperture types
Windows: Windows: SIM_EXT_GLZ -pane             BEType 12   11 surfaces   1 aperture type
Windows: Windows: SIM_EXT_GLZ_6364C1DA -pane    BEType 12    3 surfaces   1 aperture type
constructions   Windows: SIM_EXT_GLZ -pane      Windows: SIM_EXT_GLZ -frame
apertureTypes   Opening Cd0.374 F1              Opening Cd0.448 F1
```

The `_6364C1DA` pane is the legitimate split: the model states two different opening discharge
coefficients, so 11 surfaces take one pane definition and 3 take another — exactly as the direct route does.
No name carries a physical aperture GUID; `_6364C1DA` is a signature hash and is present identically on the
direct route.

Both aperture constructions match the direct route's content field for field, including both widths TBD
stores per layer.

**A repeated `WorkflowgbXML` run over the same `.tbd` reproduces every count above with nothing added.**

Two residual observations, both measured on the baseline too and therefore not from this work: 8 total
building elements against the direct route's 7 (the extra one is a panel element), and a "3 physical surfaces
are claimed by more than one SAM aperture" note from `UpdateBuildingElements` reading the fixture's own
pre-existing stamps, reported identically by A and B.

Stage 3's previously accepted scenarios were **not** re-run: this change touches none of their mechanics.

---

## Deliberate limitations

- **The pass is gated on the gbXML route.** A direct-route or TAS-authored TBD fed through `WorkflowTBD` is
  left completely untouched, so no existing behaviour on those paths can change.
- **`Modify.UpdateConstruction`'s opaque `material.width` write is left alone** — see above. The workaround
  costs one construction created and one removed per affected `ApertureConstruction`, and the end state
  matches the direct route.
- **The doubled `Windows: Windows: ` element prefix is pre-existing** and identical on both routes: on this
  model the SAM `ApertureConstruction` is itself named `Windows: SIM_EXT_GLZ`, and the element naming adds
  its own prefix. Out of scope here.
