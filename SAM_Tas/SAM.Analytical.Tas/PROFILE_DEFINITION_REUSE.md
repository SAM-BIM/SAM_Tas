# Profile definition reuse (TBD → SAM import)

Value-based deduplication of the SAM `Profile` definitions a TBD import creates.

## The problem

A SAM `Profile` is a **library-level reusable definition**. A native SAM model already shares one
`ProfileLibrary` entry across every `InternalCondition` that references it.

The TBD import did not. `Convert.ToSAM_Profiles` minted one SAM `Profile` per TBD
internal-condition profile slot and named it `"{internal condition} [{profile}]"`, and
`Convert.ToSAM(TBD.InternalCondition, double)` wrote the matching reference. A two-zone building
carrying one activity therefore produced two copies of every schedule, and the name of each copy
stated a **place** rather than a **shape** — which is what made sharing impossible: a name carrying
a zone cannot be found again by the next zone that needs the same profile.

`ModelA-Tas.sam` shows the shape of it: 44 collected slots, 42 library entries (two pairs silently
overwrote one another by library key), **20 distinct `(Category, flattened Values)` definitions**.

## The architecture

```
TBD zone-local IC / profile slots        (as many as TAS states — unchanged)
        ↓
imported SAM InternalConditions          (one per TBD internal condition — unchanged)
        ↓
shared SAM ProfileLibrary definitions    (one per distinct definition — this change)
```

Physical or zone identity never becomes part of reusable profile-definition identity.

## Equality — `Classes/ProfileDefinition.cs`

Two import-produced profiles are the same reusable definition when they agree on:

* the SAM **`Category`** string, compared ordinally — the raw category, not merely the resolved
  `ProfileType`, so two categories that resolve to one profile type but read differently stay two
  definitions;
* the **complete flattened values**, read through `Core.Tas.Query.Values` (the same flattening
  `Profile.GetValues()` performs, so the equality stays correct even for a range-encoded profile);
* the **value count**, which is part of identity in its own right — a one-value profile and a
  24-value profile of the same number are different shapes and TAS writes them back as different
  profile types.

Values are compared by **exact IEEE-754 bit pattern**, with two normalisations applied on the way in:

* `-0.0` → `0.0`. The simulation cannot tell them apart, and leaving the sign in would let two
  equal definitions hash differently.
* every NaN → the one canonical `double.NaN` pattern, so a definition carrying a NaN equals itself
  and signs deterministically. Raw IEEE NaN semantics give neither.

No tolerance is applied: both sides come from the same TAS read, so a tolerance could only ever
merge two profiles the model states as different.

### Zero-length (TAS function) profiles are out of scope

`Core.Tas.Query.Values` returns no values at all for a function profile, so its flattened form is an
**incomplete representation** of it and merging by that would be unsafe. Those keep today's
per-internal-condition name and today's library entry, unchanged. Their names are claimed *before*
any canonical name is assigned, so a canonical name can never displace one.

Fixing the function-profile import limitation is separate work and is not attempted here.

## Deterministic naming — `Query/ProfileName.cs`

> Every distinct reusable profile definition receives a deterministic unique SAM library name,
> independent of traversal order and of physical/zone identity.

1. **Preferred base** — the ordinal-smallest normalised source TAS profile name in the definition's
   equality group. Normalisation trims, collapses internal whitespace and drops control characters;
   it **keeps underscores**, unlike `Query.ApertureTypeNameBase`, because real TAS profile names
   carry them (`HTG_7to19_21`). A generated name is therefore not required to be decomposable —
   uniqueness comes from the claim set, not from a grammar.
2. **`<base>_<signature hash>`** when the base is already claimed within the same category by a
   different definition. The hash is `Query.ProfileSignatureHash`, i.e. FNV-1a over
   `Query.ProfileSignature`, which carries the exact value bit patterns. Never a UI-style `(1)`/`(2)`
   counter.
3. **`<base>_<signature hash>_<k>`**, `k` counting from 2, when even that is claimed.

It **never refuses, never drops a valid profile and never overwrites an existing definition** —
unlike the aperture-type case, where refusing was right because the alternative was writing over an
object the export did not author. Here every candidate is a fresh library entry.

Determinism therefore rests on the order definitions are offered in, so `ProfileReuseIndex.Resolve`
claims them in `ProfileDefinition.CompareTo` order — category ordinally, then value count, then
value bit patterns — a genuine total order derived from the definitions alone. Reverse the building
walk, or import twice, and the names are identical. Every comparison and ordering that affects
output uses `StringComparer.Ordinal` / `string.CompareOrdinal`; nothing depends on the current
culture.

`Query.ProfileSignature` is a **bounded fingerprint** (`C<category hash> N<count> V<value hash>`),
deliberately not injective — a yearly profile carries 8760 values and a name discriminator has to
stay short. Rule 3 is what a fingerprint collision falls through to. Nothing about **reuse** rests
on it; reuse is full definitional equality.

## One index for the whole conversion — `Classes/ProfileReuseIndex.cs`

`Query.ProfileReuseIndex(TBD.Building)` reads every collected slot **once** over COM and resolves
the definitions and names. The same instance is then threaded through:

* `Convert.ToSAM_ProfileLibrary(TBD.Building, ProfileReuseIndex)`
* `Convert.ToSAM(TBD.Building, Dictionary<string, Polygon3D>, ProfileReuseIndex)` →
  `Convert.ToSAM(TBD.zone, out …, ProfileReuseIndex)` →
  `Convert.ToSAM(TBD.InternalCondition, double, ProfileReuseIndex)`
* **`Modify.AddUnusedInternalConditions(AdjacencyCluster, TBD.Building, ProfileReuseIndex)`**

The last one matters. With `importUnused: true` that path converts the building-level internal
conditions no zone owns. Before this change it called `internalCondition_TBD.ToSAM()` with no index
at all, so those templates would have kept legacy `"{IC} [{profile}]"` references while the library
carried canonical names — dangling references on exactly the conditions least likely to be noticed.

Lookup is `(internal condition name, TBD profile slot) → resolved name`, so the conversion pays no
second COM read. A name is not an identity, though: if one slot key would have to stand for two
different things — two TBD internal conditions sharing a name and disagreeing on that slot, or a slot
that is a shared definition on one condition and a zero-length passthrough on another — the key is
marked **ambiguous and answers nothing at all**. Answering *either* would be a wrong reference on the
other side; answering nothing sends both callers to the definitional lookup, which is right for both.

`ProfileReuseIndex` and `ProfileDefinition` **touch no COM type**, which is what makes the whole
reuse and naming decision testable without an installed TAS.

## Backward compatibility

Every new parameter is optional and defaults to `null`, which reproduces today's behaviour exactly:

* `index != null` → canonical shared profile references;
* `index == null` → the legacy `"{IC} [{profile}]"` naming.

`Convert.ToSAM_ProfileLibrary(TBD.Building)` is untouched and still builds the legacy library.

## Deliberately unchanged

* **`ticV` / `VentilationProfileName`.** The import writes the reference but has never emitted the
  ventilation profile behind it. The slot is therefore *not* collected, and the reference keeps its
  legacy name — the pre-existing dangling reference stays exactly as it was, visible rather than
  quietly altered. `References_VentilationSlotIsNotCollected_…` pins that as baseline so a future
  reader does not mistake it for a regression of this work.
* **TBD `InternalCondition` sharing**, opaque `BuildingElement` reuse, `Construction` naming, and
  the function-profile import semantics.
* **Native SAM library semantics.** After this change, editing one shared `ProfileLibrary`
  definition affects every `InternalCondition` referencing it. That is what a SAM library is, and it
  is what a native SAM model already does. No aperture-style split/rebind is introduced.

## Export equivalence

`Modify.UpdateInternalCondition` derives the TAS profile's **type, factor and values** from the SAM
profile's values and the zone's own area/gains — the shared SAM profile supplies reusable shape and
values only, and zone/IC-local TAS state stays independently derived. The only export-visible
consequences of a renamed shared profile are diagnostic:

* `profile_TBD.name` and `profile_TBD.description` (both set from `Profile.Name` in `Modify.Update`);
* `thermostat.name`, which is the four thermostat profile names joined with `" & "`.

The required invariant is **simulation-effective TAS state unchanged**, not byte-identical TBD
output.

## Tests

`SAM.Analytical.Tas.TM59.Tests/ProfileDefinitionReuseTests.cs` — 27 COM-free tests: equality
(category, values, bit stability, signed zero, NaN determinism, value count, zero-length exclusion),
naming (canonical base, ordinal-smallest source name, first and extended discriminators, reversed
order, repeated build, ordinal not culture-aware), reference integrity (every slot resolves to
exactly one library entry with the right category and complete values, resolution through the same
`InternalCondition.GetProfile` lookup the export uses, the template path, and the ventilation
baseline), plus the `ModelA-Tas` 42 → 20 regression and its two known name collisions
(`Infiltration::Constant`, `Heating::HTG_7to19_21`).
