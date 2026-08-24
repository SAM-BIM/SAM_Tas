# Profile round-trip hardening

Two pre-existing defects left open by `PROFILE_DEFINITION_REUSE.md` (PR #37), fixed together because both
live in the one profile identity seam the round-trip invariant states:

> After a profile has been canonicalised once, repeated SAM ↔ TAS round trips preserve stable profile
> identity and references: no progressive name growth, no dangling profile references, and no
> simulation-effective change.

1. **Export-side (naming):** the HDD sizing writer stamped one SAM profile name onto two
   differently-valued TAS profiles, so every SAM → TAS → SAM generation accreted one `_<hash>` suffix on
   the two affected names.
2. **Import-side (reference integrity):** `VentilationProfileName` was written for every imported
   internal condition, but the `ticV` profile behind it never reached the `ProfileLibrary`, so the
   reference always dangled.

Function-profile semantics (`ticFunctionProfile` and the hourly/yearly variants) are deliberately **not**
in scope: they are a representation gap, not an identity defect, and remain excluded from dedup exactly as
PR #37 left them. See "Out of scope" below.

---

## 1. HDD naming: one name must never carry two definitions

### Root cause

`Convert.ToTBD` always exports HDD sizing conditions (`UpdateZones(…, includeHDD: true)` →
`UpdateZone_HDD` → `AddInternalCondition_HDD` → **`Modify.UpdateInternalCondition_HDD`**). That writer
synthesises the HDD condition from the *space's* SAM `InternalCondition` and flattens two of its profiles
to single-value `ticValueProfile`s:

- `ticI` (infiltration): `value = InfiltrationAirChangesPerHour`, `factor = 1`;
- `ticLL` (heating): `value = profile.MaxValue`, `factor = 1`.

Both were named `profile.Name` — the name of the **full schedule** the normal condition carries. The TBD
therefore held, per space, `Cell 1`[ticI] = (name X, 24 hourly values) and `Cell 1 - HDD`[ticI] = (name X,
one value): one identity naming two definitions.

On the next import the reuse index legitimately registers both definitions with the same preferred base,
and because `ProfileDefinition.CompareTo` orders by value count, the one-value HDD definition claims the
bare name and the full schedule is discriminated to `X_<hash>`. The following export copies the renamed
profile's name onto *both* again, so the next import discriminates `X_<hash>` into `X_<hash>_<hash>` —
one suffix per generation, deterministic, name-only. Exactly two categories were affected (Infiltration,
Heating) because those are the only two slots the HDD writer touches — matching the licensed observation
that **2 of 20** names grew on the feature model.

### Fix

`UpdateInternalCondition_HDD` names the flattened content after itself: `profile.Name + " - HDD"` — the
same convention the HDD condition itself already carries (`space.Name + " - HDD"`). The TBD then holds
`X` (full schedule) and `X - HDD` (flattened scalar): two names for two definitions. The import needs no
change — its discrimination stays as the safety net for genuinely same-named TAS-authored input (pinned
unchanged by `Naming_SameNameDifferentDefinitions_DiscriminatesDeterministically` and
`ModelA_TheTwoNameCollisionsAreDiscriminated`).

### Behaviour after the fix

- Import of a fixed export resolves `X` and `X - HDD` with **no** signature discriminator; a repeat
  export writes exactly those names back; the names are a **fixed point** across generations (pinned
  COM-free by `Naming_HDDFlattenedProfilesWithTheirOwnNames_ReachAStableFixedPoint`).
- A model already carrying discriminated names from a pre-fix export reshuffles **once**
  (`X_<hash>` → full schedule; `X_<hash> - HDD` → the flattened one) and is stable thereafter.
- Simulation-effective state is untouched: TAS profile names are diagnostic (PR #37's licensed A/B had
  52/432 profile-name differences with 0 simulation-effective differences), and the flattened HDD
  *values* (`InfiltrationAirChangesPerHour`, `MaxValue`) are byte-identical to before.

## 2. Ventilation: the reference and the library now agree

### Root cause

Commit `13c4284c` ("Ventilation Profile Implemented (WIP)", 2023-04-20) added both the import-side
reference (`Convert.ToSAM(TBD.InternalCondition)` writes `VentilationProfileName` +
`SupplyAirFlow`) and the export-side write (`UpdateInternalCondition` resolves
`ProfileType.Ventilation` and updates `ticV`) — but never added `ticV` to the library emitter
(`Convert.ToSAM_Profiles`). An unfinished WIP, not a design decision: `ticV` is an ordinary
internal-gain profile slot, read by the same `Core.Tas.Query.Values` flattening, with the category
mapping already in place (`ProfileType.Ventilation`, `InternalConditionParameter.VentilationProfileName`).
PR #37 pinned the dangling reference as baseline rather than change it.

The dangling reference meant the export's `internalCondition.GetProfile(ProfileType.Ventilation,
profileLibrary)` answered `null` for every imported model, so `Update(profile_TBD, …)` was skipped and
the exported `ticV` kept the fresh-`AddIC` TAS defaults — the source ventilation schedule was silently
dropped from the round trip. (PR #37's A/B could not see this: both sides dropped it identically.)

### Fix

- `Query.ProfileReuseIndex.ProfileSlots_InternalGain` gains `(ticV, ProfileType.Ventilation)`;
- `Convert.ToSAM_Profiles` (the legacy no-index collector the slot table mirrors) gains the same slot;
- `Convert.ToSAM(TBD.InternalCondition, …)` routes `VentilationProfileName` through the same
  `ProfileName(…)` helper as every other slot — canonical shared name with an index, legacy
  `"{IC} [{profile}]"` without one.

Ventilation definitions then dedupe by value exactly like the other seven gain slots, and the reference
resolves through the same `InternalCondition.GetProfile(ProfileType.Ventilation, profileLibrary)` lookup
the export uses (pinned COM-free by
`References_VentilationSlotIsCollected_SoItsReferenceResolves`).

### Behaviour after the fix

- The imported `ProfileLibrary` carries the ventilation profiles; `VentilationProfileName` resolves.
  On the licensed models this takes the unresolved-reference count from **4 (ModelA) / 36 (TM59 model)
  to 0**, and the library gains the distinct ventilation definitions those models carry (exact counts
  are a licensed-validation measurement — see below).
- The export now writes the imported ventilation schedule shape and the per-zone ACH factor instead of
  TAS defaults. That is a deliberate **simulation-effective restoration** for imported models — the one
  intended behavioural difference vs the pre-fix baseline — so the licensed acceptance compares the
  round-tripped TBD's `ticV` fields against the **source** TBD, not only baseline-vs-feature. (The same
  ticV field set — type/factor/value/setback/hourly/yearly — is already in the 852/5754-field dump.)
- Magnitude semantics are unchanged from every other slot: the export factor is recomputed per zone
  (`CalculatedSupplyAirFlow / volume * 3600`), exactly as native SAM models are already exported today.
- Native SAM models are unaffected (their ventilation references already resolved); the TIC import path
  is unchanged (it has no reuse index, like all its other slots).

## Out of scope (unchanged from PR #37, recorded so they are not rediscovered)

- **Function profiles.** Still excluded from dedup (a zero-value flattened form is an incomplete
  identity), still legacy-named per condition. The full semantics — importing `profile.function` into
  `LightingControlFunction`/`VentilationFunction`, reading hourly/yearly values behind the function
  *variants* in `Core.Tas.Query.Values`, and the re-export of an imported function profile (today:
  `Modify.Update(TBD.profile, …)` writes 24 NaN hourly values for a zero-count profile, its
  `Count == -1` guard being dead) — are deferred to their own task; neither licensed model exercises a
  non-empty function profile. SAM has no home for a function string outside Lighting/Ventilation, so a
  faithful general representation is a SAM-side question, not a SAM_Tas one.
- **Two adjacent export defects found during this investigation**, both pre-existing and both left for
  the function-profile task because they only bite on function-bearing models:
  `UpdateInternalCondition`'s `VentilationFunctionSetback`/`VentilationFunctionFactor` writes are guarded
  by `double.IsNaN(…)` instead of `!double.IsNaN(…)` and therefore never fire; and the template path
  (`UpdateInternalConditionTemplate`) never writes `LightingControlFunction`/`VentilationFunction` at all.
- The `InternalConditionParameter.SupplyAirFlow` unit muddle (the import stores the ticV peak **ACH**
  from `GetExtremeValue(true)` into a parameter documented as m³/s, and `CalculatedSupplyAirFlow` sums it
  as m³/s). Pre-existing for imported models, identical in kind to how every slot's magnitude is
  recomputed per zone on export, and changing it would alter simulation-effective values for models that
  set the parameter natively — outside this PR's invariant.
- TBD `InternalCondition` sharing, construction naming, and the occupancy ticOSG/ticOLG single-reference
  fold (SAM models one Occupancy profile reference; both TBD slots' definitions are in the library, but
  the condition references ticOLG's — a SAM representation limit, unchanged).

## Tests

`SAM.Analytical.Tas.TM59.Tests/ProfileDefinitionReuseTests.cs`:

- `References_VentilationSlotIsCollected_SoItsReferenceResolves` — replaces the PR #37 baseline pin.
  The production slot tables now carry `ticV` (twelve slots); a shared ventilation schedule dedupes to
  one definition; the resolved name resolves through the export's own `GetProfile` lookup.
- `Naming_HDDFlattenedProfilesWithTheirOwnNames_ReachAStableFixedPoint` — two-generation fixture over
  the post-fix export naming: no discriminator appears and generation 2 reproduces generation 1 exactly.
- The PR #37 legacy-input guarantees stand unchanged: a pre-fix (or TAS-authored) TBD whose HDD profiles
  share the design profile's name still discriminates deterministically
  (`ModelA_TheTwoNameCollisionsAreDiscriminated`,
  `Naming_SameNameDifferentDefinitions_DiscriminatesDeterministically`).

## Validation

- `SAM_Tas.sln` builds with **0 errors** in Debug and Release (Framework MSBuild; only the pre-existing
  MSB3270/MSB3277 warnings).
- `SAM.Analytical.Tas.TM59.Tests`: **498/498** in Debug and Release (497 inherited, +1 net: one
  replaced ventilation pin, one new fixed-point test).
- **Licensed TAS A/B: required before merge, not yet run.** Scenarios: `ModelA-Tas.sam` and the TM59
  project model from PR #37 — expect unresolved references 4/36 → 0, definition counts 20 → 20 + the
  distinct ventilation definitions, **zero** name growth across three full round-trip generations, all
  non-ticV simulation-effective fields identical to the PR #37 baseline, and ticV fields now equal to the
  **source** TBD.
