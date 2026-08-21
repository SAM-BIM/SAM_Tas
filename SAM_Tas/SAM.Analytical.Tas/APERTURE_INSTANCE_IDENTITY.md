# Physical aperture instance identity (Stage 3)

Stage 3 of the frozen three-stage aperture-definition plan, and the last. Stage 1 — reusable
`TBD.ApertureType` — is `APERTURE_TYPE_REUSE.md`. Stage 2 — reusable aperture `Construction` and
`buildingElement` — is `APERTURE_DEFINITION_REUSE.md`. **Both are unchanged here.** Stage 3 adds nothing to
what a definition is and takes nothing away from how definitions are shared; what it establishes is which
PHYSICAL window a shared definition is currently standing for, so that changing one of two hundred identical
windows changes one of them.

---

## The one rule

**Physical aperture identity is `{ ZoneGuid, SurfaceNumber }`.**

Held as `ZoneSurfaceKey` (`Classes/ZoneSurfaceKey.cs`), stamped on the SAM side as
`Pane`/`FrameZoneSurfaceReference_1`/`_2`.

Neither half is sufficient, which is why it is a type and not a convention. **TAS numbers surfaces per
zone**, so zone A's surface 5 and zone B's surface 5 are two different surfaces that happen to share a
number; and a zone GUID alone names a room. Every comparison of a physical surface in this codebase now goes
through either `ZoneSurfaceKey` or `Query.ZoneSurfaceReferencesMatch`, and the two agree — including that two
spellings of one GUID (braced, cased, padded) are one zone.

**What must never be used instead**, and what each of them actually is after Stage 2:

| Not identity | What it is | Why it pairs the wrong window |
|---|---|---|
| `Pane`/`FrameBuildingElementGuid` | a reusable DEFINITION binding | 200 identical windows legitimately stamp the same one |
| Construction GUID | a reusable definition | ditto, one per `ApertureConstruction` per part |
| Reusable `ApertureType` | a shared opening control | ditto, one per distinct control |
| Definition-derived name | a label on a shared object | carries no aperture GUID *by design* — that is Stage 2's invariant 4 |
| Surface area | a property of the shape | identical windows have identical areas |
| List position | nothing at all | TAS guarantees no ordering |

`AperturePhysicalIndex.ApertureGuids(buildingElementGuid)` exists and returns a **set**, deliberately: a
caller that has already resolved a surface physically may ask which apertures share the definition it points
at, and the answer being plural is the normal Stage 2 state, not a fault.

---

## What each mechanism does

### 1. `Modify.UpdateBuildingElements` — resolution, split and re-merge

Resolution order per TBD building element:

1. **The definition-membership map** — every SAM aperture whose `Pane`/`FrameBuildingElementGuid` names this
   element. Many apertures may; that is the point.
2. **Legacy GUID-in-name decoding** — for an element no aperture stamps. Byte-for-byte unchanged, so every
   TAS-authored or pre-stamping TBD behaves exactly as it always has.

On the membership path the element is compared, member by member, against what it **already carries** —
colour, opening-control assignments and feature shade, all read once before anything is written
(`Query.ApertureMatchesExistingAssignment`, `Query.FeatureShadesMatch`).

- **A member that still matches**: zero writes. Not even a rewrite to the same value — every other member
  would see it. This is the overwhelmingly common outcome of a repeated update, and what makes a repeated
  update a no-op.
- **A member that has diverged**: **split**. Its required `BuildingElementDefinition` resolves an existing
  equivalent element from the Stage 2 reuse cache, or creates one. Only **that member's own** physical
  surfaces — resolved from its own `ZoneSurfaceReference` stamps — are rebound to it. The element it left is
  never written to; if every member leaves, it is simply left in place, unused.
- **Re-merging** is the same mechanism with nothing added: a member that becomes equivalent again finds the
  common element in the cache (it is still in the building) and rebinds to it. No second equivalent
  definition is created, and the common element is not rewritten.

A pane stating a `FeatureShade` is a deliberate exception to reuse: a shade-carrying element can never be
shared (Stage 2's seed gate refuses one), so such a member always gets its own freshly created element, named
through `Query.ShadedBuildingElementName` so repeated shade splits do not collide, and is not registered for
reuse.

**Rebinding is atomic and doubly guarded** (`RebindMemberSurfaces`). The complete intended surface set is
resolved and validated before any one surface moves, because a two-sided member that moved one side and
failed on the other would be split across two elements with its stamp calling the new one authoritative. Two
independent guards must both hold:

- **the stale-stamp guard**, on the TBD side: the surface must currently point at the element the aperture
  claims it does;
- **the contested-surface guard**, on the SAM side: `AperturePhysicalIndex` must resolve that surface back to
  *this* aperture and *this* part.

Either failing rebinds **none** of the member's surfaces and leaves its stamp untouched.

### 2. `AperturePhysicalIndex` — resolve exactly one, or refuse

`Classes/AperturePhysicalIndex.cs`. `{ ZoneGuid, SurfaceNumber }` in, `(aperture, part, side)` out.

Two things must be true for an answer to be safe to write through: exactly one aperture claims the surface,
and it claims it in exactly one slot. Neither is guaranteed — a model round-tripped through the pre-Stage-3
import pairing, or hand edited, can carry the same stamp twice — and a scan returning the first hit turns
that into a silent cross-bind. **The collision is detected when the index is built, and that key refuses for
ever after, with a reason.** Refusing is correct: the alternative is updating a window the user did not
change.

Refusals are reported through `UpdateBuildingElements`'s existing `out List<string> notes` channel. No public
API was widened to carry them.

### 3. The `_1` / `_2` slots — a slot is a SIDE, and a side is a ZONE

One mutator, `Modify.SetApertureZoneSurfaceReferences`, used by **all three** write paths — the direct
export, the TBD import and `UpdateIds`. It **clears both slots and refills them** from
`Query.ApertureZoneSurfaceSides`, which orders by zone GUID (ordinal).

This replaces fill-the-first-empty-slot, which had three failure modes:

- on a model already stamped by a previous run, the old `_1` survived and `_2` was overwritten. A stale stamp
  is not harmlessly redundant — TAS does not promise to reassign the same surface numbers, so it points at a
  real surface belonging to something else;
- which side was `_1` depended on the order the zones happened to be walked, so a repeated update could swap
  them and an A/B against the source would report a swap that was not one;
- an aperture whose pane is split into several faces filled **both** slots from **one** side, losing the
  other side entirely. Several surfaces in one zone are one side, and compete for one slot; the lowest
  surface number represents it.

Three or more zones is a refusal, not a truncation. Ordering normalises the zone GUID; **writing preserves
the caller's spelling**, so a re-exported model still diffs clean against its source.

### 4. The TBD → SAM import

- **Grouping** is `Query.GroupAperturePolygons` — a pure function. Each group is seeded by its own largest
  remaining polygon and gathers every polygon whose internal point falls inside the seed's face. The seed's
  own (polygon, surface) pair is captured **before** anything is removed, which fixes two defects with one
  root cause: the seed used to be paired with whatever tuple was first in the *shrunk* list — a different
  window's surface entirely — and a lone pane with no coincident frame produced an **empty** group, so a
  frameless window came back with no `ZoneSurfaceReference`, no `BuildingElementGuid` and no imported
  `OpeningProperties`.
- **Pane vs frame comes from `BEType` first**, construction name second
  (`Query.AperturePart_BuildingElementType`). TAS keeps which half a surface is on the element; a
  construction name ending `-pane`/`-frame` is our own convention on a shared label, which a foreign TBD need
  not follow. The `[0]` fabrication fallback survives only for genuinely multi-member groups where neither
  the types nor the names say anything — a **singleton** is excluded, because a lone pane is not also its own
  frame.
- **The second side is stamped, not skipped.** The import walks each zone in turn, so an internal aperture is
  met twice; the first meeting creates it and the second used to `continue` outright, leaving the aperture
  stating one surface where the TBD holds two. It now adds the second side through
  `Modify.AddApertureZoneSurfaceReference`, which re-canonicalises the pair rather than appending, so which
  zone the import walked first does not decide which side is `_1`.

### 5. `UpdateIds`

- Clears the aperture stamps alongside the panel ones, then collects every matched surface over the whole
  pass and writes them canonically at the end. Previously only the panel stamps were cleared, so a second
  pass found `_1` occupied by the previous run, wrote side 1 into `_2`, and then wrote side 2 over the top.
- Passes the zone GUID into both `Query.Match` calls, so a surface number cannot match a panel or aperture in
  a different zone.
- Re-stamps `Pane`/`FrameBuildingElementGuid` every pass — unchanged, and correct, because that names a
  DEFINITION. It is what lets a later `UpdateBuildingElements` resolve an aperture's element without decoding
  a GUID out of a name that no longer carries one.

### 6. `Query.Match`

All three overloads that compare stamps now honour the zone:

- `Match(ZoneSurfaceReference, apertures, out part)` — via `Query.ZoneSurfaceReferencesMatch`;
- `Match(IZoneSurface, List<Panel>, zoneGuid, …)` and `Match(IZoneSurface, List<Aperture>, zoneGuid, out part, …)`
  — new overloads taking the surface's own zone; the parameterless forms remain and keep the old number-only
  behaviour for callers with no zone in hand.

Two further defects fixed in passing: the panel overload's `_2` branch re-read `_1` (a copy/paste that made
the second slot unreachable and threw outright on a panel carrying `_2` without `_1`), and the
`ZoneSurfaceReference` overload **returned** on a null list entry instead of skipping, so one null made every
aperture after it unresolvable.

`ZoneSurfaceReferencesMatch` falls back to the surface number whenever **either** side states no zone. That
makes the whole change a strict tightening: anything that matched before still matches, except a
same-numbered surface in a different, GUID-stated zone.

---

## Foreign and seeded TBDs

Stage 3 is conservative by construction, and the policy is uniform: **use SAM stamps deterministically where
they exist; never invent identity where they do not.**

| The TBD holds | What happens |
|---|---|
| valid `ZoneSurfaceReference` + `BuildingElementGuid` stamps | full membership resolution, split, re-merge, atomic guarded rebind |
| no stamps on an element (TAS-authored, or exported before the stamping existed) | the original single-aperture GUID-in-name decode, unchanged |
| a surface no aperture stamps | left alone; not an error — this is what a native TBD looks like |
| a surface two apertures stamp | **refused**, with a note; neither is rebound |
| an element carrying a feature shade | never adopted for reuse (Stage 2's seed gate); a shade-stated member always gets its own element |
| a construction name outside the `-pane`/`-frame` convention | part read from `BEType`; a singleton is not fabricated into two halves |

No arbitrary foreign TBD is required to carry SAM-specific identity, and our own exported-and-reopened models
are deterministic.

### Documented ambiguity, deliberately not resolved

**The import still buckets candidate surfaces by `ApertureConstruction` GUID before grouping them
geometrically.** For a model SAM exported this is harmless — Stage 2 names a window's two constructions from
one `ApertureConstruction`, so both halves land in one bucket. For a **foreign** TBD whose pane and frame
constructions have unrelated base names, the two halves land in different buckets and the window is imported
as two apertures. Bucketing only ever *restricts* grouping, so it cannot cross-bind; and the bucket key is
also what supplies the aperture's `ApertureConstruction`, so removing it is a round-trip change rather than a
grouping fix. Left as is, and recorded here.

---

## Invariants Stage 3 must not weaken

1. **A shared definition is never mutated on reuse.** Stage 3 changes which definition a physical surface
   points at. `zoneSurface.buildingElement = resolvedDefinition` is allowed; rewriting a shared element's
   construction, colour, `BEType` or aperture types is not, and neither is rewriting a shared `Construction`,
   `ApertureType` or schedule.
2. **No physical identity in a reusable name.** Stage 2's naming is untouched: `<ApertureConstruction.Name>
   -pane`/`-frame`, and definition-derived element names with a deterministic signature discriminator.
   `aperture.UniqueName()`, an aperture GUID and a surface number stay out of them. Physical identity lives
   in the physical references.
3. **Physical multiplicity is preserved.** 200 identical windows keep 200 pane and 200 frame `zoneSurface`s
   whatever the definitions resolve to.
4. **Refusing beats guessing.** Every ambiguity — a contested surface, a stale stamp, a part disagreement,
   three zones, an unprovable definition on a shared element — declines to write and says why.

---

## Acceptance

### COM-free (runs in CI)

`SAM.Analytical.Tas.TM59.Tests` — **415 tests, all passing** (369 before this change, all unchanged and
green, including the Stage 1 `ApertureTypeReuseTests` and Stage 2 `ApertureDefinitionReuseTests` with their
sharing expectations intact).

`ApertureInstanceIdentityTests.cs` (46 tests) covers:

- **the key**: same pair equal; same number different zone NOT equal; same zone different number not equal;
  GUID spelling (braced/cased/padded) is one zone; a non-GUID zone identifier is kept, not discarded; a
  half-populated stamp yields **no key at all** rather than a wildcard;
- **resolution**: a valid pair resolves exactly one aperture with its part and side; wrong zone → no match;
  wrong surface number → no match; side 1 vs side 2; pane vs frame;
- **ambiguity**: two apertures claiming one surface refuse, regardless of listing order; one aperture
  claiming one surface as both pane and frame refuses; a contested surface does not poison the rest;
- **identical instances**: 200 windows with identical geometry, construction and one shared building element
  remain 200 distinct physical instances, each surface resolving to its own aperture, with no ambiguity — and
  the shared binding reports all 200 members rather than a winner;
- **the slots**: two zones ordered by zone not arrival; several surfaces in one zone take one slot between
  them; three zones refuse; duplicates and unusable keys dropped; no surfaces is an empty answer, not a
  refusal;
- **the mutator**: two-sided stamping identical whichever order it is given; repeated writes change nothing;
  fewer surfaces than last time clears the stale slot; a refusal leaves no stamp standing; pane and frame do
  not disturb each other; the caller's GUID spelling is preserved;
- **the import's second side**: joins the first with both sides surviving; identical whichever zone was
  walked first; the same surface twice stays one side; a third zone refuses;
- **two-sided round trip**: three successive re-writes keep each side on its own surface with no inversion
  and no ambiguity; two internal apertures between the same zone pair do not cross-bind;
- **`ZoneSurfaceReferencesMatch`**: zone-aware, spelling-tolerant, and falling back to the number when
  either side states no zone.

`GroupAperturePolygonsTests.cs` and the membership/split half of `InstanceIdentityTests.cs` cover the import
grouping and the split decision.

### Licensed TAS — NOT YET RUN

**Stage 3 is not mergeable until this passes.** Scenarios A–E (200 identical windows; split one; merge back;
identical-geometry collision round trip; two-zone aperture through export → update → save/reopen → import),
the TAS/TSD A/B against post-Stage-2 `sow/2026-Q3`, and one shaded-project result-mapping regression
(`UpdateShading` → TAS → `CopyResults`, checking pane/frame/panel solar results stay attached to the correct
PHYSICAL aperture rather than merely the correct shared definition).

Stage 3 hardens identity without changing model state, so the A/B is expected to be exactly or
solver-noise equivalent; the actual maximum numerical differences are to be reported, not assumed.

---

## Out of scope

Deletion or garbage collection of definitions left unused by a split; profile, `InternalCondition` and
gbXML/T3D definition sharing; further Stage 1 or Stage 2 redesign; the construction-GUID import bucketing
(recorded above); refusal reporting beyond the existing `notes` channel; and any general model-identity work
outside aperture handling.
