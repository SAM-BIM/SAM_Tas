# Aperture identity across a FULL gbXML round trip

`TBD → FromTBD → new SAM model → ToGbXML → WorkflowgbXML → a NEW TBD`.

The seam the earlier aperture programme (PR #34/#36) never pinned, and the one defect it left behind.
Stages 1–3 are unchanged here: `APERTURE_TYPE_REUSE.md`, `APERTURE_DEFINITION_REUSE.md`,
`APERTURE_DEFINITION_REUSE_GBXML.md` and `APERTURE_INSTANCE_IDENTITY.md` all stand exactly as written.
Nothing below relaxes a refusal, changes what physical identity is, or changes what a definition is.

---

## The workflow that was missing

Previous acceptance covered:

- `SAM → WorkflowgbXML → TBD`, and
- repeated operation against a **kept** TBD.

Both keep one TBD for the whole exercise. Neither exercises the extra reconstruction seam:

```
A0 → ToGbXML → WorkflowgbXML → TBD1
   → FromTBD → A1                      <-- a model rebuilt from one TBD ...
   → ToGbXML → WorkflowgbXML → TBD2    <-- ... and then used to write a DIFFERENT one
```

Across that seam a `Pane`/`FrameBuildingElementGuid` changes meaning, and that is the whole problem.

---

## What a BuildingElementGuid actually says

> **"This part was bound to definition X *in the TBD it was last stamped against*."**

It is a **binding**, never a physical identity — `APERTURE_INSTANCE_IDENTITY.md` establishes that, and it is
unchanged. What this document adds is the other half: a binding is also **scoped to one file**.

TAS mints its own aperture `buildingElement`s on every gbXML/T3D conversion — it must, because the gbXML
opening name carries the aperture GUID that `Query.UpdateT3D` decodes back. So the GUID an aperture imported
from TBD1 carries names an element **of TBD1**. In TBD2 that element does not exist, and the physical surface
the aperture claims really sits on a fresh TBD2 element.

A stale binding is therefore not harmlessly redundant — exactly the argument
`Query.RemoveApertureZoneSurfaceReferences` already made for the *physical* stamps, which TAS likewise does
not promise to renumber consistently.

---

## Root cause

`Modify.UpdateIds` refreshes an aperture against the newly written TBD. It:

1. **clears** every aperture's physical stamps — unconditionally, for every aperture on every panel; then
2. **refills** stamp and binding **only where the geometric match succeeded**.

Step 1 cleared the stamps and **not** the binding. `Pane`/`FrameBuildingElementGuid` was in fact never
cleared anywhere in the codebase. So a part that step 2 could not re-match kept the binding it was given
against the **previous** TBD, and every later pass read it as the current one.

Downstream, `Modify.UpdateApertureDefinitions`:

- counts the part as bound, because `buildingElementGuid_From` is non-empty — so it is **"considered"**;
- hands it to `Query.ApertureRebindKeys`, which refuses it.

Both observed refusal classes fall out of this one state:

| Refusal | Reached when |
|---|---|
| `SAM aperture '…' has representative Pane side stamps but no preserved complete physical surface set` | the stamps were cleared and not refilled, while the stale binding still makes the part look bound. **The message names representative stamps, but the gate only tests the complete-set flag — a fully cleared part reaches it too.** |
| `Physical surface {Zone}/{N} is not currently bound to the element the aperture stamp claims` | the stale TBD1 binding is compared against the surface's real, current TBD2 element |

Hence a whole model could report:

```
Aperture definitions: 40 aperture part(s) considered; 0 rebound onto a shared definition, 0 already on one.
```

The refusals were **correct** — they were simply being asked about state that was never current. That is why
the fix is upstream of them and the gates themselves do not move.

---

## The fix

One new mutator, `Modify.RemoveApertureTasIdentity` (`Modify/RemoveApertureTasIdentity.cs`), which forgets
everything an aperture states about the TBD it was last stamped against — **both parts' physical stamps and
both parts' definition bindings** — and one call site: the unconditional clearing pass in `Modify.UpdateIds`.

The per-part primitive `Query.RemoveApertureBuildingElementGuid` sits beside the existing
`Query.RemoveApertureZoneSurfaceReferences`.

**Deliberately not called from `Modify.SetApertureZoneSurfaceReferences`.** That mutator owns the physical
stamps alone; the direct export and the TBD import both write a binding through it and must keep theirs. Only
a pass that is about to re-resolve the binding against a new TBD may clear it.

### Why the safety semantics are preserved

- A part that **is** re-matched is restamped and rebound in the same pass — outcome unchanged. The licensed
  run below rebinds 40/40 in every generation.
- A part that is **not** re-matched now reads as **unstamped**, and `UpdateApertureDefinitions` records it as
  `count_NoStamp` — "no binding to move and nothing to create" — instead of counting it as bound and then
  refusing. That is the honest record of a refresh that could not resolve it.
- `Query.ApertureRebindKeys` is **untouched**: incomplete set, contested ownership and wrong-element bindings
  all still refuse.
- Nothing infers ownership from a `BuildingElementGuid`; no physical instance is merged; no geometric match
  is made more permissive; no identity is parsed out of an element name.

---

## Licensed acceptance

Fixture: `SAM_zoningAM_v2.sam` — the 9-zone / 20-aperture Flat1 model. Weather `UKWeather.twd`, sizing-only,
short scratch paths under `C:\TasOut\rt2`, run over the normal Grasshopper route
(`ToGbXML` → `WorkflowCalculator`) via the `run-tas` skill.

### Gate B — TBD1 vs TBD2, and Gate C — TBD3

| | zones | zoneSurfaces | aperture parts | aperture BEs | aperture Cons | pane / frame bindings |
|---|---|---|---|---|---|---|
| TBD1 | 9 | 110 | 40 | **3** | 2 | 2 / 1 |
| TBD2 | 9 | 110 | 40 | **3** | 2 | 2 / 1 |
| TBD3 | 9 | 110 | 40 | **3** | 2 | 2 / 1 |

Three aperture building elements in every generation: **one shared frame and two panes**, the two panes
differing because their aperture-type content genuinely differs. No instance-GUID-named element survives; no
progressive construction proliferation. Every generation reported:

```
Aperture definitions: 40 aperture part(s) considered; 40 rebound onto a shared definition, 0 already on one.
```

with **zero refusals**.

### Gate A — A0 vs A1 vs A2

Same 9 spaces, 50 panels, 20 apertures, one aperture family, identical pane/frame layer content and identical
geometry (total pane area 57.408 m², frame 7.392 m²) throughout. `A1` and `A2` are **identical** on the whole
structural aperture dump — the fixed point is reached at the first reconstruction.

Two A0 → A1 differences, both **pre-existing `FromTBD` reconstruction behaviour, unchanged by this fix**
(byte-identical before and after it):

- the aperture construction is renamed `SIM_EXT_GLZ` → `Windows: SIM_EXT_GLZ`;
- `PartOOpeningProperties` comes back as `ProfileOpeningProperties`, because TAS stores opening control as
  aperture types and profiles rather than as the Part O-specific SAM form. The **aperture-type semantics
  survive** — `Opening Cd0.411 F1` and `Opening Cd0.477 F1` are present and identical in TBD1, TBD2 and TBD3.

### Simulation safety

Comparing every simulation-effective aperture field between generations — zone, part, `BEType`, area,
**construction layer content** (materials and thicknesses, not names) and aperture types:

- TBD1 == TBD2: **identical**
- TBD2 == TBD3: **identical**

The only cross-generation delta is the frame construction's **name** (`SIM_EXT_GLZ -frame` →
`Windows: SIM_EXT_GLZ -frame`), applied once at generation 2 and stable thereafter. Names are diagnostic, not
simulation semantics, so no TSD comparison was required.

---

## Why PR #34/#36 acceptance did not catch it

Their acceptance never rebuilt a SAM model from one TBD and then used it to write another, so an aperture
never carried a *foreign* file's binding into `UpdateIds`. Within a single TBD the binding an aperture holds
is genuinely current, and the un-cleared binding is simply overwritten by a successful match — invisible.

---

## Remaining limitations

- **The originally reported symptom did not reproduce at base SHA `372518a6`.** On this fixture the full
  chain already produced the required fixed point before the fix (see "Licensed acceptance" — the numbers are
  the same pre- and post-fix). The defect fixed here is the mechanism behind both reported refusal classes,
  proven by a mutation check: disabling the binding clear makes three of the new tests fail, one of them with
  the exact reported message. The reported run's own artifacts predate the feature
  (`Flat1.tbd` is older than `Modify.UpdateApertureDefinitions`) or carry generation-1 aperture GUIDs while
  showing generation-2 breakage, which points at a Grasshopper install running older DLLs than this source.
- The `Windows: ` construction-name prefix added at the first round trip is pre-existing naming behaviour and
  is not addressed here. It applies once and does not accrete.
- `PartOOpeningProperties → ProfileOpeningProperties` is a lossy edge of TBD reconstruction, not of this
  seam.
