# Project Progress

## Branch
`feature/tas-profile-round-trip-hardening` (off `sow/2026-Q3` at `610696e9`, i.e. with **PR #37 merged** —
the profile definition reuse this work hardens). PR #36 (`UpdateIds` zone identity) and PR #37 (profile
definition reuse) are both merged; see the "Previously" sections below for their state at merge time.

The reusable-aperture programme is complete and merged on all three routes:
`feature/tas-aperture-hardening` was PR #35, merged 2026-08-23.
`feature/tas-aperture-definition-reuse-gbxml` was PR #34, merged 2026-08-23.
Stage 3 was `feature/tas-aperture-instance-identity` (PR #33, merged 2026-08-22).
Stage 3's S3-C1/S3-C2 were `sow/2026-Q3-instance-identity` (PR #32, merged 2026-08-21).

Stage 1 was `feature/tas-aperturetype-reuse` (PR #30, merged 2026-08-21).
Stage 2 was `feature/tas-aperture-definition-reuse` (PR #31, merged 2026-08-21).

## Last updated
2026-08-24 (profile round-trip hardening - LICENSED, and corrected) - **The two leftovers PR #37 pinned as
baseline are fixed, and the licensed run caught a third defect the fix itself activated: the imported
ventilation rate was inflated by 3600/volume.** Full detail in
`SAM_Tas/SAM.Analytical.Tas/PROFILE_ROUND_TRIP_HARDENING.md`.

**1. HDD naming (export-side).** `Modify/UpdateInternalCondition_HDD` stamped `profile.Name` onto the
flattened single-value `ticValueProfile`s it writes for the HDD sizing condition's `ticI` and `ticLL`
slots - one name carrying two differently-valued definitions, which the next import legitimately
discriminated, accreting one `_<hash>` suffix per SAM → TAS → SAM generation on exactly those two
categories (the licensed "2 of 20 names grow" residual). The flattened profiles are now named after
themselves: `profile.Name + " - HDD"` (the HDD condition's own naming convention). Import unchanged - its
discrimination stays as the safety net for genuinely same-named TAS-authored input.

**2. Ventilation ticV (import-side).** An unfinished WIP from `13c4284c` (2023): the import wrote
`VentilationProfileName` but `ticV` was never in the library emitter's slot set, so the reference always
dangled and the export silently kept TAS defaults for imported models' ventilation. `ticV` is now
collected like every other internal-gain slot (`Query.ProfileReuseIndex.ProfileSlots_InternalGain` +
the legacy `Convert.ToSAM_Profiles` mirror), and the reference is routed through the same index helper
(`Convert.ToSAM(TBD.InternalCondition, …)`). This is the one intended simulation-effective change vs the
pre-fix baseline: an imported model's ventilation schedule now round-trips instead of dropping to TAS
defaults, so licensed acceptance compares ticV fields against the SOURCE TBD, not only baseline-vs-feature.

**Files changed:** `SAM_Tas/SAM.Analytical.Tas/Modify/UpdateInternalCondition_HDD.cs`,
`SAM_Tas/SAM.Analytical.Tas/Query/ProfileReuseIndex.cs`,
`SAM_Tas/SAM.Analytical.Tas/Convert/ToSAM/Profiles.cs`,
`SAM_Tas/SAM.Analytical.Tas/Convert/ToSAM/InternalCondition.cs`,
`SAM_Tas/SAM.Analytical.Tas/Modify/UpdateInternalConditionTemplate.cs`,
`SAM_Tas/SAM.Analytical.Tas/Query/ProfileName.cs` (shared `ProfileName_HDD` naming rule),
`SAM_Tas/SAM.Analytical.Tas/Query/ProfileReuseIndex.cs`,
`SAM_Tas/SAM.Analytical.Tas/Classes/ProfileReuseIndex.cs` (`Reserve`),
`SAM_Tas/SAM.Analytical.Tas.TM59.Tests/ProfileDefinitionReuseTests.cs` (+1 net: the baseline-pin
`References_VentilationSlotIsNotCollected_…` became
`References_VentilationSlotIsCollected_SoItsReferenceResolves`; new
`Naming_HDDFlattenedProfilesWithTheirOwnNames_ReachAStableFixedPoint`),
`SAM_Tas/SAM.Analytical.Tas.TM59.Tests/VentilationAirflowMagnitudeTests.cs` (new, 11 tests - the COM-free
guard that would have caught the magnitude failure before TAS was run),
`SAM_Tas/SAM.Analytical.Tas/PROFILE_ROUND_TRIP_HARDENING.md` (new handover doc),
`SAM_Tas/SAM.Analytical.Tas/PROFILE_DEFINITION_REUSE.md` (two statements reworded where recorded), this
file.

**Validation:** `SAM_Tas.sln` builds 0 errors Debug AND Release (Framework MSBuild; pre-existing
MSB3270/MSB3277 warnings only). `SAM.Analytical.Tas.TM59.Tests`: **521/521 Debug and Release**
(497 inherited, +1 net, +17 ventilation-magnitude, +6 zero-length/reservation/slot-key guards). `SAM.Analytical.Tas.Benchmark.Tests`: **16/16** both. NOTE: the sibling dependency outputs were stale/missing on this machine and had
to be rebuilt first (`SAM_gbXML` Core+Analytical, all four `SAM_SolarCalculator` projects, three
`SAM_Systems` projects, `SAM_Validation/SAM.Analytical.Benchmark`) - build outputs only, no source changes
in those repos.

**Deferred (documented in the handover doc, not forgotten):** function-profile semantics end to end
(import of `profile.function`, `Core.Tas.Query.Values` reading the hourly/yearly function *variants*, the
zero-count re-export writing 24 NaNs, the inverted `double.IsNaN` guards on
`VentilationFunctionSetback`/`VentilationFunctionFactor` in `UpdateInternalCondition`, and the template
path never writing function strings). Neither PR #37 licensed model exercises a function profile; SAM has
no home for a function string outside Lighting/Ventilation. Separate task.

**3. Ventilation MAGNITUDE (the licensed correction).** The first licensed run of this branch failed.
Collecting `ticV` woke a dormant unit defect: `profile.GetExtremeValue(true)` on a `ticV` slot is a peak
**air change rate**, but the import stored it in `InternalConditionParameter.SupplyAirFlow`, declared
`[m3/s]`. `Query.CalculatedSupplyAirFlow` read it as m³/s and the export's `/ volume * 3600` inflated it
by 3600/volume. Neither licensed model could show this (both carry `ticV = factor 1.0, value 0.0`, so
their round trip is simulation-inert); an authored **2.0 ACH** source profile made it a
**40.8 ACH** round trip - a peak hourly heating error of 69,950 W against the source, 19.3× worse than the
baseline that dropped ventilation altogether. It was harmless for as long as the reference dangled,
because the export could not resolve the profile and never wrote the factor.

The correction is unit-only, at two sites: the import writes
`InternalConditionParameter.SupplyAirChangesPerHour` (`[ACH]`, whose `rate × volume / 3600` the export's
conversion exactly inverts) in **both** `Convert/ToSAM/InternalCondition.cs` overloads (TBD and TIC shared
the mis-mapping); and `Modify/UpdateInternalConditionTemplate.cs` prefers that parameter for its `ticV`
factor, falling back to `SupplyAirFlow` so SAM-authored templates keep the factor they have always been
given. `CalculatedSupplyAirFlow` itself is untouched - the rate is routed to the basis that already
inverts correctly instead of being compensated for downstream. Corrected result: **2.0 ACH → 2.0 ACH**,
`infVentGain` within 0.003 %, peak heating error 69,950 W → **326 W**.

**Licensed acceptance (2026-08-24).** One-DLL-swap isolation (67 files, exactly 1 differing) across three
builds - baseline `610696e9`, first attempt `e2e88ca4`, corrected head - each proving its own identity by
reflecting its production slot table, the corrected build reproducing byte-identically under `-t:Rebuild`.
Unresolved ventilation references **4 → 0** (ModelA) and **36 → 0** (TM59); library **20 → 21** and
**30 → 31** with every ticV slot deduped onto one definition and counts stable across generations; HDD
names reach a fixed point (generation 2 == 3 == 4 byte-identical, where the baseline accretes one
`_<hash>` per generation and never converges); non-ticV simulation-effective fields **0 differences** in
792 and 5346; both real models **0 differences** against baseline in 227,760 and 1,024,920 simulated
values, before and after the correction. Zone-GUID churn confirmed as noise by a same-DLL control.
Also measured directly: TAS's `internalGain.freshAirRate` is **inert** in a TBD simulation (40 vs
0 l/s/p → 0 differences in 227,760 values), which is why a source carrying both an ACH schedule and a
dormant per-person rate still round-trips to their additive sum - established export design, recorded but
not changed.

**4. Zero-length ticV guard (Codex P2).** Collecting `ticV` also gave zero-length TAS **function**
profiles a resolvable library entry for the first time: `Core.Tas.Query.Values` has no case for
`ticFunctionProfile`, so it flattens to zero values, PR #37's exclusion branch still emits a legacy-named
library entry, and `VentilationProfileName` then resolved to it - after which `Modify.Update`'s dead
`Count == -1` guard let a `Count == 0` profile fall into the `Count <= 24` branch and overwrite the function
profile with 24 hourly values. New `Query.IsCollectableSlot(int, IEnumerable<double>)` (true for every slot
except `ticV`, and for `ticV` only when its values are non-empty) is consulted by both collectors, so a
zero-length `ticV` is registered nowhere, its reference falls back to the legacy name and dangles exactly as
it did before PR #38 - the safe deferred behaviour. Deliberately `ticV`-scoped; the other eleven slots keep
PR #37's treatment, and no function support is attempted. Four COM-free tests pin it. Normal `ticV` is
unaffected - confirmed licensed as byte-identical to the accepted build (792 non-ticV + 56 ticV fields,
0 differences) with the 2.0 ACH oracle intact, so the full A/B was not rerun.

**5. Review round (Codex + Copilot).** Three further findings, all addressed. (a) The magnitude fix stored
`GetExtremeValue(true)` = `factor * max(values)`; because `Modify.Update` re-applies the raw values on top of
whatever basis it is given, that scaled the schedule twice (`factor * max^2`) - invisible for a profile
normalised to a peak of 1, which is what the first authored oracle used. The import now stores
`profile_TBD.factor`; a non-normalised source (factor 2.0, peak 0.5 = 1.0 ACH) round-tripped as 0.5 ACH
before and **1.0 ACH exactly** after, with the normalised 2.0 ACH oracle unchanged and TM59 source-factor
agreement improving 26/54 -> 44/54. (b) Skipping a zero-length `ticV` left its legacy name unclaimed, so
`Resolve` could hand that same string to an unrelated canonical definition and turn the intended dangling
reference into a live one; the skip path now calls the new `ProfileReuseIndex.Reserve(category, name)` -
a claim with no definition, no library entry and no answerable slot - and `Resolve` seeds its claim set from
it. (c) The legacy `Convert.ToSAM_Profiles` mirror repeated the twelve slots by hand; both collectors now
`foreach` over the shared slot tables behind the shared `Query.IsCollectableSlot` gate, so they cannot drift
and one assertion pins both. All three verified licensed as behaviour-neutral on the real models (0
differences across 792/5346 non-ticV fields, ModelA re-simulated at 0/227,760, library 21/31, 0 unresolved).
**6. Second review pass (Codex).** The `Reserve` fix in item 5(b) closes a coincidental STRING collision
between two different internal conditions, but Codex found it does not close a slot-KEY collision: two TBD
internal conditions can share the exact same name (a duplicate space name, a generic template) while
disagreeing on `ticV`, and `Reserve` never touches `definitionsBySlot`/`excludedNamesBySlot`. `Register`
gained an `bool suppressLibraryEntry` parameter so a skipped `ticV` still goes through the same ambiguity
tracking every other excluded slot already uses - only the final library-emission step is skipped. Verified
by first reverting to the Reserve-only behaviour and confirming a new test (`IC name = "Duplicate"`, one
`ticV` zero-length, one ordinary) genuinely failed - `GetProfileName("Duplicate", ticV)` answered the
ordinary profile's name instead of null - then restoring the fix and confirming it passes. 521/521 tests
Debug and Release; no full licensed A/B rerun (the cheap 2.0 ACH oracle and both real models' import
integrity were re-checked and are unchanged).

A fourth finding - a source declaring both an ACH schedule and a TAS-inert `freshAirRate` round-tripping to
their additive sum - is answered in the handover doc rather than changed: it is the established export design
and narrowing it would drop ventilation for native SAM models.

**Recommended next step:** [PR #38](https://github.com/SAM-BIM/SAM_Tas/pull/38) is OPEN against
`sow/2026-Q3` with the licensed gate **run and passed** after the magnitude correction and the review round. Remaining: human
review, then merge. Merging remains a human call - it was not done here.

---

## Previously
2026-08-23 - **PR #37 (profile definition reuse) merged at `610696e9`.** The section below is its state
at merge time; its two pinned leftovers (name growth, dangling ventilation reference) are what the
current branch fixes.

## Previously (PR #37, at merge time)
Branch `feature/tas-profile-definition-reuse` (off `sow/2026-Q3` at `2950b27c`, i.e. after PR #35 merged the
aperture hardening fixes). **PR #36 (`fix/tas-updateids-gbxml-zone-identity`) has since been merged into
`sow/2026-Q3` at `03f9757` and is merged into this branch**, so the branch now carries it; the two touch
disjoint code (PR #36: `UpdateIds`/`Match`/zone identity; this branch: profile definitions).
Stage 1 complete; **licensed A/B PASSED - see "Last updated". Final review finding fixed (see
"Post-review fix" below). PR #37 OPEN against `sow/2026-Q3`, Copilot review comments addressed (see
"Post-review fix (2)" below). Not yet merged.**

### PR #37 last entry
2026-08-23 (later still) - **Post-review fix (2): addressed the GitHub Copilot automated review on
[PR #37](https://github.com/SAM-BIM/SAM_Tas/pull/37).** Codex's review hit its usage limit and left no
comments; nothing from it to address. Six real findings, no behavioural or reusable-profile-dedup change:

**Binary compatibility (4 findings, all the same shape).** `InternalCondition.cs`, `Space.cs`,
`AdjacencyCluster.cs` and `AddUnusedInternalConditions.cs` each added the new `ProfileReuseIndex`
parameter as an *optional* parameter on the EXISTING public method, rather than as a genuinely new
overload. An optional parameter is a compile-time convenience only - it still changes the method's
CLR metadata arity, so a caller compiled against the previous signature (e.g. `ToSAM(TBD.InternalCondition, double)`)
throws `MissingMethodException` at runtime against the new DLL, and old-arity method-group conversions
stop compiling. This project already has an established, deliberate fix for exactly this shape -
`AnalyticalModel.ToSAM(string, bool)` forwards to `ToSAM(string, bool, bool)` - and Copilot correctly
spotted that this branch's four new call sites did not follow it. Fixed as a mechanical split: each
method's previous exact signature is now a separate forwarding overload (calling the new one with
`null`), and the indexed body moved to a new overload with the extra parameter non-optional. No
internal call site needed to change - they all already pass every parameter explicitly. Confirmed via
`git show <merge-base>:<file>` that all four really did have a narrower public signature before this
branch, so none of these was a pre-existing false positive.

**`ProfileReuseIndex.Profiles` returned excluded profiles before `Resolve()`.** The property's own doc
says "Empty until `Resolve` has run"; the getter unconditionally appended `excludedProfiles` regardless
of resolution state. The one production caller (`ProfileLibrary.cs`) already guards on `.Resolved`
first, so this was latent rather than a live bug, but it violated its own contract for any future or
test caller that trusted the doc. Fixed: the getter now returns empty before `Resolve`, matching the
doc, with no change to post-`Resolve` behaviour (verified no test reads `.Profiles` before calling
`Resolve()`).

**Stale "no second COM read" doc claim** in `Query/ProfileReuseIndex.cs`. The doc said the slot lookup
never re-reads TAS, but `Convert.ToSAM.ProfileName`'s ambiguous-slot fallback calls
`Core.Tas.Query.Values(profile_TBD)` again - confirmed by reading that fallback path. Fixed:
doc now states the guarantee applies to building the index, and separately documents the one
fallback exception and why it is still correct, not just cheap.

**Stale test count** in `PROFILE_DEFINITION_REUSE.md` ("27 COM-free tests"): the zero-length
ambiguity fix added two more, actual count is 29 (`grep -c '\[Test\]'` confirms). Fixed.

**Stale branch-status prose** in this file (this section and "Next step" above): both still said
"PR not opened" / "then open ONE PR", which stopped being true the moment PR #37 was opened. Fixed to
name the open PR and the actual remaining step (review, then merge).

**Files changed:** `InternalCondition.cs`, `Space.cs`, `AdjacencyCluster.cs`,
`AddUnusedInternalConditions.cs` (binary-compat overload split), `Classes/ProfileReuseIndex.cs`
(`Profiles` getter), `Query/ProfileReuseIndex.cs` (doc), `PROFILE_DEFINITION_REUSE.md` (stale count),
`PROJECT_PROGRESS.md` (this entry and the branch-status lines above).

**Validation:** `SAM.Analytical.Tas` rebuilt with VS Framework MSBuild (0 errors).
`SAM.Analytical.Tas.TM59.Tests` **497 passed / 0 failed**, unchanged - none of these six findings
touch a code path any existing or new test exercises differently; the binary-compat split is
metadata-only (same runtime behaviour, same call graph, since every internal call site already passed
every parameter explicitly) and was checked by full rebuild + full test re-run rather than by
reasoning alone. `SAM.Analytical.Tas.Benchmark.Tests` **16/16**.

**Licensed A/B NOT rerun, deliberately - same reasoning as the first post-review fix**: none of these
six findings touch reusable-profile dedup, canonical naming, or any TAS-simulation-effective behaviour;
four are pure API-surface/binary-compatibility fixes, one is a defensive contract fix on a path no
production caller reaches, and two are documentation-only.

---

2026-08-23 (later) - **Post-review fix: the zero-length exclusion path now marks a contested slot key
ambiguous, as the reusable path already did.**

**The defect.** In `ProfileReuseIndex.Register`, the `!profileDefinition.IsReusable` branch wrote the
excluded legacy name into `excludedNamesBySlot` first-wins. A slot key is
`(internal condition name, slot)` and a name is not an identity, so two TBD internal conditions can share
a name, share a slot, and still hold **different** zero-length (TAS function) profiles. Legacy import
writes `Duplicate [Daylight]` for one and `Duplicate [Dimmer]` for the other; the first-wins map answered
`Duplicate [Daylight]` for both. Because that name does exist in the library, the result was a **silent
misreference**, not a dangling reference - the harder of the two to notice. Low severity: it needs
duplicate TBD internal-condition names AND differing function profiles on the same slot.

**The fix** (narrow, and mirrors the reusable branch exactly): same key + same excluded name keeps the
mapping; same key + a different excluded name calls `MarkAmbiguous(key)`; once ambiguous the slot
fast-path answers nothing, permanently. The library entries themselves are untouched - both are still
added and deduped by `category::name` - so both conditions fall through
`Convert.ToSAM.ProfileName`'s chain (slot -> definitional -> legacy) to their own legacy name, which the
library carries. **The reusable path, dedup, canonical naming and `DefinitionCount` are all unchanged**;
the only behavioural delta is the same-key/different-excluded-name case, which previously answered wrongly
and now answers nothing.

**Files changed:** `SAM_Tas/SAM.Analytical.Tas/Classes/ProfileReuseIndex.cs` (the branch, plus the field
comment that still claimed first-wins); `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/ProfileDefinitionReuseTests.cs`
(+2 tests); `SAM_Tas/SAM.Analytical.Tas/PROFILE_DEFINITION_REUSE.md` (the ambiguity rule now lists all
three cases).

**Validation:** `SAM.Analytical.Tas` rebuilt with VS Framework MSBuild (0 errors; the test project
references `build/SAM.Analytical.Tas.dll`, not a `ProjectReference`, so this rebuild is required for the
tests to see the change). `SAM.Analytical.Tas.TM59.Tests` **497 passed / 0 failed** (495 previous + 2 new).
The new `References_SlotThatIsZeroLengthOnBothConditionsUnderDifferentNames_AnswersNothing` was confirmed
to FAIL against the pre-fix DLL with `Expected: null / But was: "Duplicate [Daylight]"`, so it pins the
defect rather than merely passing; `References_SlotRegisteredTwiceWithTheSameZeroLengthName_KeepsAnswering`
pins the agreement half so the fix cannot over-trigger.

**Licensed A/B NOT rerun, deliberately.** The change cannot alter reusable-profile behaviour, and the
licensed acceptance already records that **neither licensed model exercises zero-length function profiles
at all** - so an A/B could not observe this path. The earlier A/B result stands unchanged.

---

## This branch's main work: profile definition reuse
2026-08-23 - **Value-based deduplication of the SAM `Profile` definitions a TBD import creates.**
Full detail, invariants and the deliberate exclusions live in
`SAM_Tas/SAM.Analytical.Tas/PROFILE_DEFINITION_REUSE.md`.

**The problem.** A SAM `Profile` is a library-level REUSABLE DEFINITION - a native SAM model already shares
one `ProfileLibrary` entry across every `InternalCondition` that references it. The TBD import did not:
`Convert.ToSAM_Profiles` minted one profile per TBD internal-condition slot and named it
`"{internal condition} [{profile}]"`, so the name stated a PLACE rather than a SHAPE, which is what made
sharing impossible. `ModelA-Tas.sam`: 44 collected slots, 42 library entries, **20 distinct
`(Category, flattened Values)` definitions.**

**The rule now.** Reusable-definition equality is the SAM `Category` string (raw, ordinal) plus the complete
flattened values plus the value count, compared by exact IEEE-754 bit pattern with `-0.0` normalised to
`0.0` and every NaN canonicalised. No TAS internal-condition name, no space name, no profile Guid, no
encounter order. Zero-length (TAS function) profiles are **excluded** from dedup - their flattened form is
an incomplete representation of them - and keep today's per-internal-condition import verbatim.

**Deterministic naming.** Canonical name = the ordinal-smallest normalised source TAS profile name in the
equality group; on collision within a category, `_<signature hash>`; if even that is claimed,
`_<signature hash>_<k>`. It never refuses, never drops a profile and never overwrites one. Determinism
comes from claiming names in `ProfileDefinition.CompareTo` order (category, value count, value bits), not
in traversal order, so a reversed walk or a repeated import produces identical names. All ordering is
`StringComparer.Ordinal`.

**One index for the whole conversion.** `Query.ProfileReuseIndex(TBD.Building)` reads every slot once over
COM and is threaded through the library build, the zone/internal-condition conversion AND
`Modify.AddUnusedInternalConditions`. That last path was the gap the independent review found: with
`importUnused: true` it called `internalCondition_TBD.ToSAM()` with no index, which after dedup would have
left the unowned template conditions pointing at legacy names the library no longer carries.

**Deliberately unchanged** (all pre-existing, all confirmed still present): `ticV` is still not emitted into
the imported `ProfileLibrary`, so `VentilationProfileName` still dangles - the slot is NOT collected and the
reference keeps its legacy name, and a test pins that as baseline rather than as a regression of this work.
TBD `InternalCondition` sharing, opaque `BuildingElement` reuse, construction naming and the function-profile
import semantics are all untouched.

**Adjacent survey** (asked for alongside the change): no other TAS -> SAM import object is both a SAM
reusable library definition and cloned/renamed per space out of TAS provenance. Materials are keyed by TBD
material name building-wide; constructions by TBD construction GUID building-wide; aperture constructions by
`Query.ApertureConstructionPairKey` building-wide (the previous programme). SAM `InternalCondition` is the
one adjacent case and is NOT a pure library definition here: `Convert.ToSAM(TBD.InternalCondition, double)`
bakes the owning zone's floor area into `AreaPerPerson` and the per-person gains, so per-space instances are
semantically required, not provenance artefacts. Out of scope and unchanged.

**Validation this session:** `SAM_Tas.sln` builds with 0 errors in Debug AND Release (VS Framework MSBuild;
only the pre-existing MSB3270/MSB3277 and XML-doc warnings). `SAM.Analytical.Tas.TM59.Tests`
**495 passed / 0 failed** in both Debug and Release (457 pre-existing, unmodified, + 27 new in
`ProfileDefinitionReuseTests.cs`, + 11 arriving with PR #36); `SAM.Analytical.Tas.Benchmark.Tests` 16/16.

**LICENSED A/B: PASSED (2026-08-23, EDSL Tas).** Full evidence in
`SAM_Tas/SAM.Analytical.Tas/PROFILE_DEFINITION_REUSE.md` → "Licensed acceptance". One-DLL swap (67 files,
all hash-identical but `SAM.Analytical.Tas.dll`), input `.tbd` generated once with the baseline DLL so both
sides import identical TAS input. Two rounds, because PR #36 merged mid-validation: round 1 baseline
**`2950b27c`** vs **`d5ba1082`**; round 2 baseline **`03f97570`** (the current merge-base) vs **`95dabb6b`**.
Two real models — `ModelA-Tas.sam` (2 spaces, 4 ICs, 44 slots, normal + HDD) and the real TM59 residential
project `SAM_zoningAM_v2zonesisDomestic.sam` (**9 spaces, 27 ICs, 396 slots**, conditions genuinely shared
across spaces).

| | ModelA-Tas | TM59 project model |
|---|---:|---:|
| SAM `ProfileLibrary` entries, baseline → feature | **42 → 20** | **369 → 30** |
| SAM-side fields compared / semantic differences | 176 / **0** | 1584 / **0** |
| TAS simulation-effective fields compared / differences | 852 / **0** | 5754 / **0** |
| hourly TSD values compared / differing | 227 760 / **0** | 1 024 920 / **0** |

Every numeric field compared as its exact IEEE-754 bit pattern, the TBD read back with TAS's own
`Get*(index)` accessors rather than the helpers under test, and a full 1–365 day simulation run against a
real TAS weather year. The **only** differences are the three predicted diagnostic ones —
`profile_TBD.name`, `profile_TBD.description`, `thermostat.name` — plus the zone GUID, which a
same-DLL-twice control run shows TAS re-mints on every export regardless. `internalCondition_TBD.name`,
IC counts and per-zone assignment are unchanged. The known dangling `VentilationProfileName` is unchanged
(4 / 36 unresolved references, the same set on both sides, all `Ventilation`). Repeat import is
byte-identical. The one coverage gap is stated rather than implied: **zero-length TAS function profiles are
not exercised by either licensed model** (both carry only value/hourly profiles), so their exclusion from
dedup rests on the COM-free tests.

---

## Also this session (merged in from PR #36)
2026-08-23 - **PR #36 revalidated on the user's exact production file and Codex review addressed.** Exact input:
`C:\Users\michal.dengusiak\OneDrive - Tetra Tech, Inc\Documents\SAM_daily\2027-08-03-HVAC\SAM_zoningAM_v2zonesisDomestic.sam`
(SHA-256 `CF0C749D8148EC7433482528040B4E32EAC5E5B6A6B91042C6029FF17E19537F`). Route was exactly
`AnalyticalModel -> SAM.Analytical.gbXML.Convert.ToFile -> WorkflowCalculator`, the engine behind
`SAMAnalytical.WorkflowgbXML`, with `Simulate=false` as in the warning-producing run.

**Exact seam diagnostic, before any new behavioural edit.** The checkout's stale pre-PR build output
reproduced `0 considered / 40 carry no building element stamp` twice, while retaining 9 TBD zones, 110
zoneSurfaces (20 pane + 20 frame) and 20 SAM apertures. After rebuilding the actual PR head `b585e87`,
temporary `UpdateIds` instrumentation reported: **9/9 spaces -> zones; 110/110 TBD zoneSurfaces -> SAM
panels; 40/40 aperture zoneSurfaces -> SAM apertures; 20 pane + 20 frame identifications; 40/40
BuildingElementGuid writes; 20 unique aperture collectors.** The instrumentation was then removed.

The exact model does NOT expose a second translation algorithm. TAS all-panel, non-shade and
space-related bboxes are identical at `[-30.5,-8,0]-[30.5,8,4]`; the corresponding SAM subsets are all
`[0,0,0]-[61,16,4]`. Every subset therefore proves the same TBD->SAM translation `(30.5,8,0)`. There are
no shades/non-building outliers, no differing SAM/TBD subset and no extra TAS transform. The production
file's domestic-zone metadata does not alter the relevant 9-space/50-panel/20-aperture geometry. Thus the
previous `SAM_zoningAM_v2.sam` passed for the same geometric reason; the apparent difference here was the
DLL actually loaded, not the SAM file. No further translation code was added.

**Behavioural fix already in PR #36.** `UpdateIds` computes that TBD->SAM centroid translation once and
passes it only to translation-aware panel/aperture matches. ZoneGuid is captured before clearing and
resolved GUID-first with exact-name fallback. The exact model now reports **40 considered / 40 rebound / 0
already shared; 40 aperture building elements removed** and no no-stamp note. The 40 per-instance aperture
elements become 3 reusable definitions: frame x20 surfaces, pane x15 and pane x5. Both first and repeated
runs preserve 9 zones, 110 zoneSurfaces, 20 pane surfaces, 20 frame surfaces and 20 physical apertures;
the returned model has 20 pane + 20 frame BuildingElementGuid stamps and 20 + 20 physical-surface stamps.
Repeat run produces the identical summary and element-use multiset `{20,15,5}` with no added definition.

**Codex P2.** `Query.Match` now keeps the original public panel and aperture CLR signatures exactly and
forwards each to a separate translation-aware overload. Only `UpdateIds` calls the overloads with a
translation. A reflection regression pins the original and translation-aware signatures for both return
types; the test project explicitly references `Interop.TBD` for that metadata-only check.

**Later Codex P2 (unassigned-panel centroid).** No behavioural change was made because its premise is false
for `AdjacencyCluster`: `Shade(panel)` returns true exactly when the panel has no related `Space`. Therefore
the existing `GetPanels().FindAll(x => !Shade(x))` SAM centroid already is the space-related subset, and an
unassigned panel cannot be a non-shade outlier. This is also what the licensed seam trace measured: all
non-shade and space-related bboxes were identical on both sides. A COM-free regression now pins the invariant
with an orphan wall 1000 m away; it is classified as a shade and cannot move the non-shade centroid.

**Newest Codex P2 (SAM-only resolved-space subset) - now fully validated, including the exact licensed
rerun, and pushed.** A SAM space added after export (or absent because TAS failed to export it) has related
panels, so those panels are non-shades but have no TBD counterpart. `UpdateIds` now resolves all SAM spaces
against the TBD zones first, derives BOTH centroids only from panels belonging to those successfully shared
space/zone pairs, and reuses the same resolution map for stamping. The new COM-free regression has two
non-shade spaces 1000 m apart and passes only the shared space to the translation subset; the SAM-only panel
is excluded.

**Exact licensed two-pass rerun with this fix, same production file
(`SAM_zoningAM_v2zonesisDomestic.sam`, same SHA-256 as above), same route
(`AnalyticalModel -> SAM.Analytical.gbXML.Convert.ToFile -> WorkflowCalculator`, `Simulate=false`).**
Built with the .NET Framework MSBuild (Debug then Release, `-t:Restore`/`-t:Build` as separate invocations),
then a temporary `net8.0-windows`/x64 probe (`.scratch/PR36Probe`, removed after use per the discipline
below) drove the real route against `build/SAM.Analytical.Tas.dll`/`SAM.Core.Tas.dll` end to end - no
COM-crossing `List<T>` helper calls (see `[[tas-licensed-harness-troubleshooting]]`), output under the short
`C:\PR36Out` path, one stray `TBD.exe` from the run stopped afterwards.
- **Before** (pre-fix baseline, same file, reproduced from the stale pre-PR build as before): **0 aperture
  part(s) considered, 40 carry no building element stamp**, while still keeping 9 TBD zones, 50 SAM panels,
  110 TBD zoneSurfaces (20 pane + 20 frame) and 20 physical apertures.
- **After**, PASS 1: **40 aperture part(s) considered; 40 rebound onto a shared definition, 0 already on
  one; 40 aperture building elements removed afterwards.** SAM side: 9 spaces, 50 panels, 20 physical
  apertures, **20/20 pane and 20/20 frame `BuildingElementGuid` stamps, 20/20 pane and 20/20 frame physical
  zone-surface stamps** (100% both ways, no no-stamp note). TBD side unchanged: 9 zones, 110 zoneSurfaces
  (20 pane + 20 frame), 8 building elements, reduced to **3** reusable aperture definitions with element-use
  multiset **{20, 15, 5}**.
- **Repeat run (PASS 2, same process, TBD re-exported and re-solved)**: byte-identical summary line to PASS
  1 - same 40/40/0, same 20/20/20/20 stamps, same {20,15,5} multiset, no additional definition created. The
  fix is deterministic on this file.
- This exactly reproduces the already-validated fixed state recorded above from `b585e87` (before this
  newest SAM-only-subset refinement existed) - the newest fix is a generalisation for models with SAM
  spaces absent from the TBD and does not change behaviour on this file, as expected: TAS all-panel,
  non-shade and space-related bboxes were already identical for this file, so the new "shared-only" subset
  and the old "non-shade" subset select the same panels here. Confirms no regression from the newest change.

**Files changed in this follow-up:** `SAM_Tas/SAM.Analytical.Tas/Query/Match.cs`,
`SAM_Tas/SAM.Analytical.Tas/Modify/UpdateIds.cs` (SAM-only resolved-space subset),
`SAM_Tas/SAM.Analytical.Tas.TM59.Tests/UpdateIdsZoneResolutionTests.cs`,
`SAM_Tas/SAM.Analytical.Tas.TM59.Tests/SAM.Analytical.Tas.TM59.Tests.csproj`, this file.

**Validation:** focused Debug **11/11**, focused Release **11/11**; full TM59 Debug **468/468**, full TM59
Release **468/468**; solution Debug and Release build with **0 errors** (only the pre-existing
MSB3270/MSB3277 processor-architecture/System.Memory warnings). Exact licensed two-pass rerun on the
production file passes as described above, deterministically. The temporary `.scratch/PR36Probe` harness
was deleted before committing, matching the `APERTURE_TYPE_REUSE.md`-style discipline for scratch harnesses.

**Unresolved / out of scope:** the same recentring also afflicts any OTHER geometry-matching step on this
route that lacks the compensation (`CopyResults` matches apertures to solar surfaces by geometry; the
simulation/results legs were not run here - `Simulate=false`). Not touched; would need its own licensed
validation.

**Recommended next step:** all automated validation and the exact-model licensed acceptance are green;
reply to Codex comment `3839130533` confirming the SAM-only resolved-space subset fix is implemented and
licensed-validated, and await fresh CI checks on the pushed commit. Do not merge automatically - that
remains a human call.

---

## Previously
2026-08-23 - **Two aperture hardening fixes, both pre-existing defects deliberately kept out of PR #34.**
Full detail in `SAM_Tas/SAM.Analytical.Tas/APERTURE_HARDENING.md`; the two limitations they close are
reworded where they were recorded, in `APERTURE_DEFINITION_REUSE_GBXML.md`.

**1. A stated `ApertureParameter.FeatureShade` never reached the TBD pane.** Two causes, neither of them
the name decode (a licensed probe showed all 14 pane elements decoded their aperture GUID and found the
aperture). First, an `AdjacencyCluster` can hold one aperture BOTH on its panel and as a cluster object,
real models carry both - all 14 in `ModelA.sam` do, straight off disk - and
`AdjacencyCluster.GetAperture(guid)` answers from `GetObject<Aperture>` FIRST, so
`Modify.UpdateBuildingElements` read colour, opening controls and the shade off a stale copy the user's
edit never reached. New `AperturePanelIndex` (`Classes/` + `Query/`) answers from the panel walk only, and
is now shared with `Modify.UpdateApertureDefinitions`, which had built its own local dictionary for exactly
this hazard. Second, **licensed TAS silently drops the FIRST `AssignFeatureShade`** onto a building
`T3DDocument.ExportNew` has only just written; re-assigning the SAME object lands it. `Modify.SetFeatureShades`
now establishes the assignment by RE-READING the element and repeats up to three times, and reports honestly
when it never took. `UpdateBuildingElements` gained a `feature shade stated / written` summary note.

**2. The importer paired a window's pane and frame by construction NAME.** Stage 2 shares by value, so two
`ApertureConstruction`s with identical panes and different frames export as one shared pane construction plus
two frames; bucketing surfaces by the base name left after stripping `-pane`/`-frame` put the second family's
pane in the FIRST family's bucket and its frame in a bucket of its own. **The rule is now: physical grouping
is geometric (`Query.GroupAperturePolygons` over ALL of a zone's aperture surfaces at once), which half a
surface is comes from its element's `BEType`, and family identity is the PAIR of construction identities the
two halves carry (`Query.ApertureConstructionPairKey`, GUID first and name only as a fallback). The name is
chosen afterwards (`Query.ApertureConstructionName`) and labels the family; it never decides it.**

**Licensed A/B (2026-08-23, EDSL Tas, `ModelA.sam`, one-DLL swap against a `e9b5a3d0` worktree build):**

| | baseline | this branch |
|---|---:|---:|
| shaded gbXML run: feature shades on pane elements | 0 | **1** |
| shaded gbXML run: aperture building elements | 3 | 4 (the extra one is the shaded pane's own) |
| two-family round trip: physical apertures imported | **21** | **14** |
| two-family round trip: families | A x14, B x7 (B's panes lost) | **A x7, B x7** |
| two-family round trip: pane+frame stamps | both=7, paneOnly=7, frameOnly=7 | **both=14** |

The inverse case (shared FRAME, two panes) reconstructs the same way. `ModelA.sam` unmodified is unchanged
on both routes: 28 -> 3 aperture building elements, 2 aperture constructions, 14 apertures imported under one
`Windows: SIM_EXT_GLZ` with `both=14` stamps, and **a second full workflow run reproduces every count with
nothing added and no duplicated shade**. Tests: **457/457 in Debug and Release** (18 new in
`ApertureHardeningTests.cs`).

**Residual, all pre-existing and out of scope:** `Convert.ToTBD(analyticalModel, ...)` writes no aperture
`FeatureShade` at all (`Modify.Update`'s shade block has been commented out for years, and
`UpdateBuildingElements` is the only writer anywhere); the import never reads a shade back off a TBD element;
`Query.UpdateT3D` still resolves its aperture through `AdjacencyCluster.GetAperture`, the same stale-copy
defect, and changing it needs its own licensed T3D validation.

---

## Earlier
2026-08-22 - **The standard gbXML workflow now gets the same reusable aperture definitions the direct
`SAMAnalytical.TBD` route has.** Stage 2 scoped itself to the direct `Modify.Update` export and declared the
gbXML/T3D route out of scope; that gap is now closed. Full detail, invariants, the A/B table and the
deliberate limitations live in `SAM_Tas/SAM.Analytical.Tas/APERTURE_DEFINITION_REUSE_GBXML.md`.

**Root cause.** On this route SAM_Tas does not write the TBD - TAS's `T3DDocument.ExportNew` does, from a T3D
in which every aperture is its own `window`, because the gbXML opening name has to carry the aperture GUID for
`Query.UpdateT3D` to decode it back. TAS therefore writes one aperture building element per aperture per part,
named after that aperture, and nothing afterwards collapsed them (`UpdateBuildingElements` only ever SPLITS).
The T3D cannot be canonicalised first: `Interop.TAS3D` exposes no surface or opening object at all.

**Fix.** One gbXML-gated step, `Modify.UpdateApertureDefinitions`, placed AFTER `Modify.UpdateIds` so it reads
the physical stamps that step has just written instead of re-deriving them. It adds no new rules: definition
resolution is `Modify.ResolveApertureDefinition`, extracted verbatim from `Modify.Update` so both routes share
one resolver; physical resolution is Stage 3's `Query.AperturePhysicalIndex`/`ApertureRebindKeys`, unchanged.
Afterwards orphaned elements are swept (`markDelete` + `DeleteMarkedBuildingElements`; TBD has no
`RemoveBuildingElement`) and instance-named or superseded aperture constructions removed. Nothing named after
a physical aperture may be ADOPTED as a shared definition
(`BuildingReuseCache.RefuseSeededDefinitions` + `Query.NamesContainingApertureGuid`).

**Licensed A/B (2026-08-22, EDSL Tas, `ModelA.sam`, builds differing in `SAM.Analytical.Tas.dll` only):**
aperture building elements **28 -> 3**, with 14/14 pane/frame `zoneSurface`s unchanged, 2 aperture
constructions, 2 aperture types, and the 14 apertures resolving to **2 distinct pane bindings and 1 frame
binding**. The resulting definitions are identical to the direct route's name for name and surface count for
surface count. A repeated run adds nothing.

**Three defects the licensed run caught that no COM-free test could:** the re-stamp always refused
(`AdjacencyCluster.GetAperture(guid, out panel)` returns early when the aperture is also a cluster object and
leaves `panel` null); `DeleteMarkedBuildingElements` returns a STATUS (-1 on success), not a count; and
leaving one construction under its signature-qualified name broke the import's pane/frame pairing, so it
reported 28 apertures for 14 windows. All three are fixed, the last by reclaiming the plain name once the
sweep frees it.

**Known, out-of-scope:** `Modify.UpdateConstruction` sets `material.width` only for a TRANSPARENT material, so
the frame construction `Modify.UpdateConstructions` writes earlier in the workflow differs from the Stage 2
definition in that one field. The pass works around it rather than changing a writer used by every route.

**Validation:** COM-free suite **438/438** Debug and Release (419 pre-existing unchanged, 19 new); Debug and
Release solution builds **0 errors**; SPDX headers present on every new file; `git diff --check` clean.

**Immediate next step:** open the PR for `feature/tas-aperture-definition-reuse-gbxml`. Do NOT start
InternalCondition/profile work or opaque BuildingElement optimisation.

---

## Previous session

2026-08-22 - **PR #33 final review pass and focused licensed acceptance complete.** PR #32 (S3-C1 + S3-C2)
is merged;
`feature/tas-aperture-instance-identity` closes six further identity gaps it left, adds the handover doc
`SAM_Tas/SAM.Analytical.Tas/APERTURE_INSTANCE_IDENTITY.md`, takes the COM-free suite to **419/419**, and has
been through a full licensed-TAS A/B against the `0f66b11` baseline.

Physical aperture identity is now `{ ZoneGuid, SurfaceNumber }` and nothing else, held as one value type
(`ZoneSurfaceKey`) that every physical comparison goes through. A surface claimed by two apertures REFUSES
rather than resolving to whichever was enumerated first. The `_1`/`_2` slots are canonical - a slot is a SIDE
and a side is a ZONE - and all three write paths (export, import, `UpdateIds`) go through one mutator that
clears before it fills.

**Licensed headline (2026-08-22, EDSL Tas):** every scenario passes on this branch and FAILS on the baseline.
200 identical windows repeat-update with 0 stamps changed and 0 collisions where the baseline produces 400
collisions; a split rebinds exactly one surface and merges back onto the original element where the baseline
strands it; a real 2-zone model with 14 apertures sharing one construction round-trips with every stamp
0.0000 m from its own aperture where the baseline leaves all 28 unresolved; a two-zone aperture keeps exactly
one two-sided pane and one two-sided frame through export/update/save/reopen/import where the baseline
reports 13-14 spuriously two-sided and 0 after import. The exported TBD is **identical on all 61 dumped
facts** between the two builds, and a TAS run of both agrees on **173,376 result values with 0 differing,
max absolute and relative difference 0**. Full table in `APERTURE_INSTANCE_IDENTITY.md`.

The final PR review found both Codex behavioural comments valid. Multi-face aperture parts now preserve a
separate complete canonical surface set while `_1`/`_2` remain representative sides; a split/merge rebinds
that entire set or refuses. Complete-set validation now precedes replacement lookup/creation, cache
reservation, controls, schedules, shade and split counting, so an invalid/contested stamp creates no orphan
and moves nothing. Representative-only legacy stamps refuse until restamped rather than risk a partial move.
The two Copilot comments (XML return contract and `workk internla`) are also fixed.

**Validation:** focused regressions **4/4**; full COM-free suite Debug **419/419** and Release **419/419**;
Debug and Release solution builds **0 errors** (only existing MSB3270/MSB3277 and legacy compiler/XML-doc
warnings). The first post-review SPDX run found that the changed legacy `ApertureParameter.cs` had no required
header; the final commit adds that header, and `git diff --check` passes.

**Focused licensed acceptance (2026-08-22, EDSL Tas, exact `ed6d659` Release DLL): PASS, 0 failures.**

- **Multi-face split/merge-back:** aperture `353144a1-3d6a-4daf-b12b-24bf7556bbce`; complete pane set
  `{A67E0FA9-DC62-44EB-A1E0-9CB988807FC6, 5}` and `{A67E0FA9-DC62-44EB-A1E0-9CB988807FC6, 13}`; representative
  `_1` is surface 5 and `_2` is empty. Both faces moved from element
  `{D36F2CA6-79A4-40EC-A531-DED89D40C8AE}` to `{F719D1F4-A112-4AA3-9CE0-72FFDC59F0D9}` on divergence and both
  returned to the original element on merge. Old/new surface counts were **4/0 before**, **2/2 after split**,
  and **4/0 after merge**. Unrelated apertures and the original definition were byte-for-value unchanged;
  merge created no further element and left the split element unused.
- **Contested refusal/no orphan:** changed aperture `17c8ddfd-3f58-4ebc-89bf-0d9968c43aa6`, contestant
  `3f882c59-5f42-4cf5-be9a-304a43b33535`, contested key
  `{D72E02ED-3D5C-48F2-93C0-9DA38CA94695, 5}`. Both the first and repeated update refused before creation;
  binding `{8B6851B8-8F32-4271-8D23-89C66C4E3D85}` and every physical surface stayed unchanged, split count
  stayed zero, and counts stayed exactly **4 building elements / 2 aperture elements / 8 constructions /
  1 aperture type / 0 schedules / 0 feature shades**. Repetition accumulated nothing.

The scratch driver and generated `.tbd` files are deliberately uncommitted. The fixture first settles the
known one-time legacy construction reconciliation, then measures only the refused rebind, so its unchanged
construction count is specific to the P2 acceptance rather than obscured by that pre-existing behaviour.

**Immediate next step:** merge PR #33 once GitHub checks and re-review are green. Do not broaden into Stage 4.

## Current status
**The gbXML route is done and awaiting PR** - see the "Last updated" section above and
`SAM_Tas/SAM.Analytical.Tas/APERTURE_DEFINITION_REUSE_GBXML.md`. Both front ends inherit it: Grasshopper's
`SAMAnalytical.WorkflowgbXML` and SAM_UI's simulate-cases and multitasker flows all construct the same
`WorkflowCalculator`, so no change was needed in `SAM`, `SAM_UI` or `SAM_gbXML`.

**Stage 1 is merged.** The export shares one `TBD.ApertureType` across every building element stating the
same opening control. Full detail, the S1-C0 probe result and the licensed-TAS acceptance table live in
`SAM_Tas/SAM.Analytical.Tas/APERTURE_TYPE_REUSE.md`.

**Stage 2 is merged** (PR #31, 2026-08-21). The direct `Modify.Update` export shares one `TBD.Construction`
and one aperture `TBD.buildingElement` across every aperture stating the same content, instead of creating
one per aperture per part. 200 identical windows go from 400 constructions and 400 elements to 2 and 2,
while all 400 physical `zoneSurface`s remain. Full detail, invariants, seed gates, deliberate limitations
and the acceptance table live in `SAM_Tas/SAM.Analytical.Tas/APERTURE_DEFINITION_REUSE.md`.

**Stage 3 (physical-instance identity hardening) is in progress** on
`sow/2026-Q3-instance-identity` (PR #32 -> `sow/2026-Q3`):

- **S3-C1 done** (`11a856a1`) - `UpdateBuildingElements` resolves which SAM aperture(s) a TBD building
  element stands for via STAMPS, not name-decode: `Modify.UpdateIds` now also stamps each aperture's
  `Pane/FrameBuildingElementGuid` with the GUID of the TBD element its export bound it to; the update path
  resolves through a definition-membership map built from those stamps (many apertures may stamp one
  shared element), falling back to the ORIGINAL single-aperture GUID-in-name decode, byte-for-byte
  unchanged, for every element no aperture stamps (all TAS-authored/legacy TBDs). A shared element is never
  mutated; a divergent member is split onto its own element and only its own stamped surfaces are rebound.
  Also fixes `Query.Match`'s ZoneSurfaceReference overload comparing SurfaceNumber alone across zones
  (TAS numbers surfaces PER ZONE) - now requires ZoneGuid agreement when both sides state one, exposed as
  COM-free `Query.ZoneSurfaceReferencesMatch`.
- **S3-C2 done** (`c79be01d`) - the aperture import's inline polygon grouping extracted as COM-free
  `Query.GroupAperturePolygons`, fixing two real import bugs with one root cause (the seed's key half was
  read back off the shrunk tuple list): a seed with a coincident partner got a DIFFERENT aperture's
  surface key attached, and a lone pane with no coincident frame produced an EMPTY group - no
  ZoneSurfaceReference, no BuildingElementGuid, no imported OpeningProperties for that aperture at all.
- **S3-C3 DONE** on `feature/tas-aperture-instance-identity` - the handover doc
  (`APERTURE_INSTANCE_IDENTITY.md`) is written and seven further identity gaps PR #32 left are closed (export
  and `UpdateIds` never cleared their stamps; the import dropped every internal aperture's second side and
  read pane/frame off the construction name; two `Query.Match` overloads still ignored the zone; nothing
  detected a physical surface claimed by two apertures; and an element created by a SPLIT could never be
  updated again, so a split aperture could never merge back). 415/415 COM-free tests pass, and the
  **licensed-TAS A/B against the `0f66b11` baseline passes every scenario** - see the S3-C3 section below.

The frozen three-stage plan is
`C:\Users\Virtual Machine\.claude\plans\you-are-in-plan-lazy-pebble.md` (approved rev. 2, 2026-08-21).
Stage 3's first item was a known, already-planned consequence of Stage 2 that PR #31's Codex review caught
independently: `UpdateBuildingElements` degraded (note-based, not silent corruption) when fed a Stage-2
TBD, because Stage 2 element names no longer carry a single aperture's GUID by design - see
`APERTURE_DEFINITION_REUSE.md`, "Known limitation: `UpdateBuildingElements` on a Stage-2 TBD". S3-C1 is
the fix.

## Stage 3 - Codex review fixes (PR #32, this session)

The Codex review of PR #32 raised five findings; all are fixed:

- **P1 - legacy word-set construction fallback restored** (`Modify/UpdateBuildingElements.cs`). The Stage 3
  rewrite kept building `constructionWordSets` but stopped USING it, so a legacy element whose name carried
  all of a construction's words without either side being a literal suffix fell through to the null check
  and got no construction, colour, opening controls or schedules. The subset-of-words fallback is restored
  after the exact/suffix matches, before the null check.
- **P1 - feature shade joins the split decision** (`Query/ApertureMatchesExistingAssignment.cs`, new
  `Query/FeatureShadesMatch.cs`, call site). A stamped pane adding, removing or changing ONLY its
  `FeatureShade` still matched on colour and openings, so it stayed bound and never reached
  `SetFeatureShades`. The element's current shade (read once via `GetFeatureShade(1)`, converted to SAM) is
  now compared by CONTENT - float-precision, NaN-aware, name/description excluded (TAS auto-names shades) -
  and a mismatch splits exactly as a colour change does. Consequential invariant: a pane stating a shade
  never takes the reuse cache (a shade-carrying element is never shareable - the seed gate's own rule), is
  always created fresh, gets the shade written, and is NOT registered for reuse.
- **P1 - a lone pane is no longer stamped as its own frame** (`Convert/ToSAM/AdjacencyCluster.cs`). S3-C2's
  one-member groups newly fed singletons into a fallback that assigned `zoneSurfaces_Aperture[0]` to BOTH
  pane and frame, and frame-first reference matching then classified the pane as a frame. A singleton now
  keeps only the part its construction name (or, suffixless, its element's `BEType`) states; the `[0]`
  fabrication fallback is retained for multi-member groups only.
- **P2 - rebind validates the complete surface set before moving any** (`RebindMemberSurfaces`). A
  two-sided member whose second surface was missing/stale previously rebound the first and still advanced
  the BuildingElementGuid stamp, splitting the aperture across old and new elements. Now any resolution or
  stale-stamp failure rebinds NONE of the member's surfaces and leaves its stamp untouched.
- **P1 - this file updated** (it still read "Stage 3 not started" and recommended opening the
  already-merged Stage 2 PR).
- **P1 - collision-safe naming for repeated shade splits** (second review round; new
  `Query/ShadedBuildingElementName.cs`, call site). The plain two-name budget (preferred +
  signature-qualified) excludes the shade from the signature, so a second shade split of one definition -
  or a re-split after another shade change - derived a name that was already taken and `BuildingElementName`
  returned null, leaving the pane bound to the wrong-shaded element. The shade-aware variant falls back to
  a shade-content discriminator (FNV-1a over the definition signature plus the stored-float bit pattern of
  every shade field), then a counter, keeping the `Windows: <base>_<8 hex> -pane` convention shape so the
  name still decomposes.
- **CI (not Codex, same session):** `build.yml`'s dependency-clone fallback could not map a sow FEATURE
  branch (`sow/2026-Q3-instance-identity`) to a dependency-repo branch on PUSH events (empty `base_ref`) and
  fell through the stale hardcoded `sow/2026-Q2` to the default branch, which no longer carries
  `SAM.Analytical.Benchmark` - every push build failed in the SAM_Validation step while the PR build passed.
  Both clone steps now derive the quarter branch (`sow/2026-Q3`) from the ref name as a fallback.

Files changed: `SAM_Tas/SAM.Analytical.Tas/Modify/UpdateBuildingElements.cs`,
`SAM_Tas/SAM.Analytical.Tas/Query/ApertureMatchesExistingAssignment.cs`,
`SAM_Tas/SAM.Analytical.Tas/Query/FeatureShadesMatch.cs` (new),
`SAM_Tas/SAM.Analytical.Tas/Query/ShadedBuildingElementName.cs` (new),
`SAM_Tas/SAM.Analytical.Tas/Convert/ToSAM/AdjacencyCluster.cs`,
`SAM_Tas/SAM.Analytical.Tas.TM59.Tests/InstanceIdentityTests.cs` (+13 tests: shade add/remove/change,
float round-trip stability, frame-ignores-shade, `FeatureShadesMatch` null/text/NaN cases, and the four
shade-split naming cases), `.github/workflows/build.yml`, this file.

Validation: `SAM.Analytical.Tas.csproj` builds with 0 errors in Debug AND Release (Framework MSBuild; the
MSB3270 COM-architecture warnings are pre-existing). `SAM.Analytical.Tas.TM59.Tests`: **369/369 pass** in
both configurations (356 pre-existing, unchanged, + 13 new). CI on the final head: build (push AND
pull_request) + spdx all pass. Licensed TAS: not run - S3-C3 owns that gate.

---

## Stage 3 - S3-C3: physical instance identity completed (branch `feature/tas-aperture-instance-identity`)

Off `sow/2026-Q3` at `0f66b11` (i.e. after PR #32 merged S3-C1 and S3-C2). This branch finishes Stage 3:
the gaps PR #32 left, the handover doc, and the COM-free suite for both. **The licensed-TAS gate has NOT been
run and Stage 3 is not mergeable until it has.**

Full mechanism write-up: `SAM_Tas/SAM.Analytical.Tas/APERTURE_INSTANCE_IDENTITY.md`. It documents PR #32's
half as well as this one, so it is the single handover document for the stage.

### What PR #32 had already fixed (do not redo)

`UpdateBuildingElements` membership resolution + split/re-merge + atomic guarded rebind; the import's
seed-key and lone-pane grouping bugs (`Query.GroupAperturePolygons`); `UpdateIds` stamping
`Pane`/`FrameBuildingElementGuid`; `Query.Match`'s `ZoneSurfaceReference` overload made zone-aware
(`Query.ZoneSurfaceReferencesMatch`); feature-shade divergence in the split decision and collision-safe
shade-split naming.

### Gaps this branch closes

1. **The direct export never cleared its stamps.** `Modify.Update` filled
   `Pane`/`FrameZoneSurfaceReference_1` then `_2` in creation order and only where the slot was EMPTY. On a
   model already stamped by a previous export that left the old `_1` standing - pointing at a surface number
   TAS need not have reassigned to the same surface - and overwrote `_2`. An aperture whose pane is split
   into several faces filled BOTH slots from ONE side, losing the other side entirely.
2. **`UpdateIds` cleared the panel stamps but not the aperture ones**, so a second pass wrote side 1 into
   `_2` and then side 2 over the top of it.
3. **The import dropped the second side of every internal aperture.** The pass that meets an already-created
   aperture in the adjacent zone did `continue`, so a two-zone aperture came back stating one physical
   surface where the TBD holds two.
4. **The import read pane vs frame off the CONSTRUCTION NAME**, with `BEType` consulted only for singletons -
   so a multi-member group whose constructions were named unconventionally fell through to the `[0]`
   fabrication and stamped one surface as both halves.
5. **`Query.Match`'s other two overloads still compared SurfaceNumber alone**, ignoring the zone; the panel
   overload's `_2` branch also re-read `_1` (unreachable second slot, and an outright throw on a panel with
   `_2` but no `_1`); and the `ZoneSurfaceReference` overload RETURNED on a null list entry instead of
   skipping, so one null made every aperture after it unresolvable.
6. **Nothing detected a physical surface claimed by TWO apertures.** The membership map is keyed by
   building-element GUID (shared by design, so blind to it) and the surface index was last-wins. A contested
   surface would have been rebound to whichever aperture the enumeration reached first.
7. **An element created by a SPLIT could never be updated again** - found by the licensed gate, not by
   reading. `UpdateBuildingElements` resolves an element's construction from its NAME and skips the element
   entirely when none matches; a split-created element carries a collision-discriminated name
   (`Windows: SIM_EXT_GLZ_1F3A0C21 -pane`) that no construction is named and that the word-set test cannot
   match either. So every subsequent pass counted it under `count_GlazingWithoutConstruction` and skipped it
   before the aperture block - which meant a split aperture could never MERGE BACK. Fixed by falling back to
   the construction the element itself already carries; the name matches stay first, because re-deriving from
   the name is how an updated construction reaches an element at all.

### What was added

| File | Role |
|---|---|
| `Classes/ZoneSurfaceKey.cs` | `{ ZoneGuid, SurfaceNumber }` - immutable, value equality, normalised zone GUID. COM-free. |
| `Query/ZoneSurfaceKey.cs` | Zone-GUID normalisation and the factories. A half-populated stamp yields NO key rather than a wildcard. |
| `Classes/AperturePhysicalIdentity.cs` | One SAM aperture's four stamps plus its two definition bindings, as values. |
| `Query/AperturePhysicalIdentity.cs` | COM-free factory from an `Aperture`, and the whole-model index factory. |
| `Classes/AperturePhysicalIndex.cs` | Surface -> (aperture, part, side), REFUSING any key two apertures claim. Cannot be asked which aperture a building element is. |
| `Query/ApertureZoneSurfaceSides.cs` | The canonical slot rule - a slot is a SIDE and a side is a ZONE - the parameter maps, and the clear helper. |
| `Modify/SetApertureZoneSurfaceReferences.cs` | The ONE mutator for the stamps (plus `AddApertureZoneSurfaceReference` for the import's second side, and `Query.ApertureZoneSurfaceReferences` to read them back). |
| `Query/AperturePart_BuildingElementType.cs` | Pane/frame from `BEType`, deliberately NOT via `Query.AperturePart(int)` - that overload answers `Frame` for TAS's DOOR type, which is right where it is used and wrong as a statement about which half a physical surface is. |
| `APERTURE_INSTANCE_IDENTITY.md` | The Stage 3 handover doc, covering PR #32's half too. |
| `Tests/ApertureInstanceIdentityTests.cs` | 46 COM-free tests. |

### What was changed

- `Modify/Update.cs` - aperture surfaces collected with their ZONE and PANEL (`ApertureZoneSurfaceRecord`);
  the four inline stamp blocks replaced by one deferred canonical pass through the shared mutator. Stage 2
  definition resolution untouched.
- `Modify/UpdateIds.cs` - aperture stamps cleared alongside the panel ones; surfaces collected over the pass
  (`ApertureSurfaceCollector`) and written canonically at the end; zone GUID passed into both `Match` calls.
- `Convert/ToSAM/AdjacencyCluster.cs` - second side stamped instead of skipped; `BEType`-first pane/frame
  classification with the name convention as fallback and the `[0]` fabrication only for genuine multi-member
  groups; first-side stamps through the shared mutator.
- `Query/Match.cs` - zone-aware overloads for the panel and aperture-by-surface forms; `_2`-branch copy/paste
  fixed; null-entry abort fixed; `ZoneSurfaceReferencesMatch` normalises the GUID so it agrees with
  `ZoneSurfaceKey`.
- `Modify/UpdateBuildingElements.cs` - `AperturePhysicalIndex` built once and consulted by
  `RebindMemberSurfaces` as a contested-surface guard alongside the existing stale-stamp guard; the surface
  index re-keyed on `ZoneSurfaceKey` (replacing the raw `SurfaceKey` string format) so every physical
  comparison in the codebase uses one key type; ambiguities reported per surface and in the summary.

### Decisions worth not re-deriving

- **PR #32's design was kept, not replaced.** An earlier pass on this branch had built a parallel decision
  engine (`ApertureSurfaceActions`) and grouping helper (`ApertureSurfaceGroups`) plus an extraction of
  Stage 2's resolve-or-create; all were dropped on merging PR #32 rather than run alongside it. Two competing
  formulations of one decision is worse than either.
- **A slot is a SIDE and a side is a ZONE.** Several surfaces in one zone compete for one slot; the lowest
  surface number represents it. Three zones is a refusal, not a truncation.
- **Ordering normalises the zone GUID; writing preserves the caller's spelling**, so a re-exported model
  still diffs clean against its source.
- **`ZoneSurfaceReferencesMatch` falls back to the number when EITHER side states no zone**, which makes the
  whole zone-awareness change a strict tightening rather than a new class of refusal.
- **The import's construction-GUID bucketing was deliberately left alone** and documented instead: it only
  ever restricts grouping (so it cannot cross-bind), and the bucket key is also what supplies the aperture's
  `ApertureConstruction`, so changing it is a round-trip change rather than a grouping fix. Recorded in
  `APERTURE_INSTANCE_IDENTITY.md`, "Documented ambiguity".

### Validation performed

- `SAM.Analytical.Tas.csproj` and the full `SAM_Tas.sln` build with **0 errors** in Debug and Release
  (Framework MSBuild; the MSB3270/MSB3277 warnings are pre-existing).
- `SAM.Analytical.Tas.TM59.Tests`: **415/415 pass** (369 before this branch, all unchanged and green -
  including Stage 1 `ApertureTypeReuseTests` and Stage 2 `ApertureDefinitionReuseTests` with their sharing
  expectations intact - plus 46 new).
- **Licensed TAS A/B, 2026-08-22, every scenario PASSES on this branch and FAILS on the `0f66b11` baseline.**
  Full table in `APERTURE_INSTANCE_IDENTITY.md`, "Licensed TAS". Summary: A (200 identical windows, repeated
  update) 0 vs 400 collisions; B (split one) exactly 1 surface rebound, old element keeps the other 9;
  C (merge back) all 10 back on the ORIGINAL element vs baseline stranding it; D (real 2-zone model, 14
  apertures sharing one construction, `SAM -> TBD -> SAM`) every stamp 0.0000 m from its own aperture vs 28
  unresolved + 3 collisions; E (two-zone aperture through export/2x update/save/reopen/import) exactly 1
  two-sided pane and 1 two-sided frame with both link surfaces, vs 13-14 spuriously two-sided and 0 after
  import. Pre-simulation TBD **identical on all 61 dumped facts**; TAS/TSD A/B **173,376 values, 0 differing,
  max absolute and relative difference 0**.

### Licensed harness (not committed, same discipline as Stages 1 and 2)

`Gate3.exe`, a `net8.0-windows` console project (`Gate3.csproj`, `Program.cs`, `Model.cs`, `Tbd.cs`,
`Local.cs`, `Tsd.cs`) referencing the built DLL set by `HintPath` off `TasLibs`/`SamLibs` MSBuild properties,
so ONE binary runs against either branch's `SAM.Analytical.Tas.dll`. Commands: `probe`, `diag`, `diag2`,
`diag3`, `imp`, `inspect`, `dump`, `sim`, `cmp`, `results`, `a <n> <dir>`, `b <dir>`, `d <model> <dir>`,
`e <model> <dir>`. Three things worth not rediscovering:

- **Run it from a SHORT output path.** TAS shows a modal "Fail to save to file" dialog and then dies with
  "RPC server is unavailable" when the target path is long - the scratchpad path was long enough to trigger
  it. `C:\Gate3Out` works. Kill any stray `TBD.exe` first; a stuck one holds the file.
- **Never call a SAM_Tas helper that returns `List<TBD.*>`.** `SAM.Core.Tas` is built with
  `EmbedInteropTypes=True`, so those signatures cannot cross an assembly boundary at all (CS1769). `Tbd.cs`
  walks TAS's own 0-based `Get*(index)` accessors instead, which is also the right thing for a harness: what
  the TBD holds must be observed independently of the code being observed.
- **`SAMTBDDocument.Dispose` closes the shared TAS COM server**, so a handful of document open/close cycles
  in one process makes a later TSD read fail. Simulation is therefore run in a CHILD process, and the
  result-mapping leg still could not be driven to completion in-process.

Real models used: `SAM_Deploy/SAM_SolarCalculator/SAM_SolarCalculator.Tests/Fixtures/ModelA.sam` (2 spaces,
11 panels, 14 apertures, ALL sharing one `ApertureConstruction` - ideal for the collision scenario). A
synthetic hand-built box is used for A and B/C; note its hand-wound wall faces do NOT survive
`Convert.ToSAM` (only the two horizontal panels come back), which is why D and E use a real model.

### Unresolved

**Two legs of the licensed gate were NOT run and are not claimed:**

1. **The shading-specific chain** - `Simulate_Coverage`, `UpdateShading`, `Create.SolarModel`, `CopyResults`
   pane/frame/panel solar mapping. Not attempted. Mitigating evidence rather than a substitute:
   `CopyResults` matches apertures to solar surfaces by GEOMETRY, not by the stamps (recorded in
   `APERTURE_DEFINITION_REUSE.md`), and the TAS/TSD A/B is exactly identical.
2. **`Modify.AddResults` end to end.** Driven to the point of consuming the stamps, then blocked by the
   `SAMTBDDocument.Dispose` COM-server teardown described above - **identically on both builds**, so it is a
   harness limitation, not a Stage 3 behaviour. What IS established is that the stamp set `AddResults` keys on
   is exactly right here (28 stamps for 14 apertures) and was badly wrong before (54).

**Two pre-existing defects found and deliberately NOT fixed** (both confirmed identical on the baseline, both
recorded in `APERTURE_INSTANCE_IDENTITY.md`):

1. `Modify.UpdateConstructions` adds a duplicate, unused set of aperture constructions on a Stage-2 TBD
   (count 4 to 8 on the first `UpdateBuildingElements`, then stable) because its name derivation carries the
   `Windows: ` prefix where the Stage 2 export does not. Inert - nothing points at the duplicates.
2. **`Modify.Update`'s own `updateGuids` stamping never reaches the caller**, because the method opens by
   reassigning its parameter to `adjacencyCluster.UpdateNormals(...)`, which returns a NEW cluster. Every
   stamp that branch writes lands on a clone that is discarded on return. `Modify.UpdateIds` is the live path
   and is what the gate exercises. NOT fixed here: turning it on would start mutating caller models on two
   public entry points (`WorkflowCalculator`, `SAM.Analytical.Tas.TM59.Convert.ToTBD`) that pass
   `updateGuids: true` today and get nothing, which is well outside what Stage 3 was chartered to change.
   **Worth a decision before anyone relies on export-side stamping.**

### Exact recommended next step

One focused independent diff review of `0f66b11..HEAD`, then open the PR. The licensed gate is done.

---

## Stage 2 - blocking merge gate (ALL ROWS NOW PASS - see "licensed acceptance progress" below)

These were the merge conditions. All three are now satisfied on licensed TAS; the evidence is recorded in
the acceptance section below and summarised in `APERTURE_DEFINITION_REUSE.md`'s table.

1. **A/B simulation.** Export the same real model twice - once with the old per-aperture building-element
   behaviour (`sow/2026-Q3`) and once with shared elements (this branch) - run TAS/TSD on both, and compare
   zone results. They must be numerically equivalent within normal solver noise.
2. **One real shaded project**, checking `UpdateShading`, `CopyResults` and aperture solar-result mapping.
   `CopyResults` already matches apertures to solar surfaces by GEOMETRY (its own comment records that the
   stamped building-element GUID is really the shared *construction* GUID), so sharing should not affect it -
   but confirm rather than assume.
3. **Object counts and round trip**, per the table in `APERTURE_DEFINITION_REUSE.md`: 200 identical windows
   -> 400 surfaces / 2 constructions / 2 elements with Stage 1 counts unchanged; several constructions,
   several opening controls, sealed windows, doors; a repeated export adding nothing; and
   SAM -> TBD -> SAM preserving aperture count, geometry, pane/frame classification, construction layers and
   `OpeningProperties`.

**One thing only licensed TAS can settle:** what a freshly added `TBD.buildingElement` carries for `ground`,
`markDelete` and `width`. The building-element seed gate refuses anything non-zero there, because the export
writes none of them. If TBD's own default is non-zero, no seeded element is ever adopted - safe in itself
(under-reuse, never unsafe sharing), but the "repeated export adds nothing" row would fail and a third
export could then hit the double-name refusal. That row is what detects it; if it fails, relax those three
fields to "equal to what a freshly created element reports" rather than "zero".

**RESOLVED on licensed TAS (2026-08-21):** a freshly created `TBD.buildingElement` reports `ground=0`,
`markDelete=0`, `width=0`, `ghost=0` - live and after save/reopen. The seed gate's zero assumption was
already correct for these three fields; **no fix was needed there.**

**A genuine defect WAS found, one level down, by the exact same class of check** - see "Stage 2 - licensed
acceptance progress" below: `Query.ConstructionMaterialDefinition`'s opaque/transparent branches assumed a
fresh `TBD.material`'s untouched `dynamicViscosity`/`convectionCoefficient` read back as `0`; licensed TAS
reports `1E-05`/`0.001`. Fixed and verified - see that section.

---

## Stage 2 - licensed acceptance progress (this session, on this machine)

**Environment:** EDSL Tas build 17044, `HKLM\SOFTWARE\EDSL\TasManager`, COM confirmed live
(`New-Object -ComObject TBD.Document` succeeds). A standalone harness (`Gate.exe`, **not committed** - see
"Licensed harness" below, same discipline as Stage 1's) drives the real `Modify.Update` against real `.tbd`
files through TBD COM.

### 0. Starting-state confirmation - PASS
Branch `feature/tas-aperture-definition-reuse`; HEAD contains `a178124` and `a5a98ed`; base `f3f5802` is an
ancestor; `git status` clean; `git diff --stat f3f5802..HEAD` touches only
`SAM_Tas/SAM.Analytical.Tas/**`, `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/ApertureDefinitionReuseTests.cs`,
and the two docs (`APERTURE_DEFINITION_REUSE.md`, `PROJECT_PROGRESS.md`) - no `SAM`, `SAM_Tas_Grasshopper`,
`UpdateBuildingElements`/`UpdateIds`/import-grouping/gbXML/T3D files touched. `SAM.Analytical.Tas.csproj`
builds clean (0 errors, Debug) against the licensed interop.

### 1. Fresh-object defaults probe - PASS, no gate fix needed
`Gate.exe probe`: a fresh `TBD.buildingElement` -> `ground=0 markDelete=0 width=0 ghost=0 BEType=0
colour=0`, unchanged after save/reopen. Matches the seed gate's assumption exactly.

### 2A. 200 identical pane+frame windows, first export - PASS
`Gate.exe counts 200`: 400 aperture zoneSurfaces (200 pane + 200 frame) over 200 physical apertures; exactly
**2** aperture Constructions (`SIM_EXT_GLZ -pane`, `SIM_EXT_GLZ -frame`); exactly **2** aperture
BuildingElements (`Windows: SIM_EXT_GLZ -pane`, `Windows: SIM_EXT_GLZ -frame`); **1** Stage 1 ApertureType
(`Opening Cd0.395 F1`, identical opening control on all 200); **0** schedules (no schedule-bearing control
used); 2 distinct aperture BuildingElement guids; 0 part-mismatched surfaces (every element's own
construction is the same part as the element).

### 2B. Repeat export into the same store - FOUND A GENUINE DEFECT, THEN FIXED, THEN PASS
**First run (before the fix):** the second `Modify.Update` call into the SAME open building produced
`+2 Construction, +2 BuildingElement` (collision-suffixed names, e.g. `SIM_EXT_GLZ_5EFF1698 -pane`,
`Windows: SIM_EXT_GLZ_A47E0E09 -pane`) instead of `+0` - a real "repeated export adds nothing" **failure**.

**Diagnosis.** Neither the seed gate's own refusal checks were firing (`Gate.exe diagnose` showed both the
original and the new collision-suffixed objects individually passing their seed gates with `proven=true`) -
the mismatch was in field-by-field content EQUALITY between the fresh, factory-computed
`ConstructionDefinition` and the one read back off the just-created `TBD.Construction`. A targeted
field-by-field diff (`Gate.exe diffconstruction`) isolated it to exactly two fields on every opaque
(`Timber`) and transparent (`Glass 6mm`) material layer: `dynamicViscosity` and `convectionCoefficient`.
`Query.ConstructionMaterialDefinition` (the COM-free mirror of `Modify.UpdateMaterial`) assumed these read
back as `0` for opaque/transparent materials, because `UpdateMaterial` never writes them for those two
kinds (only for `GasMaterial`) - the code's own comment already said "a fresh TBD material's own values
stand", it just had the value wrong. Licensed TAS reports `dynamicViscosity = 1E-05` and
`convectionCoefficient = 0.001` on a material `construction.AddMaterial()` creates and the opaque/transparent
`UpdateMaterial` overload leaves those two fields untouched on - confirmed by reading back three separate
already-exported opaque/transparent materials (frame Timber, both pane Glass 6mm layers), all agreeing
exactly, while the one Gas layer (which IS written) matched the mirror already.

**Fix applied:** `SAM_Tas/SAM.Analytical.Tas/Query/ConstructionMaterialDefinition.cs` - the opaque and
transparent branches now use the confirmed TAS defaults (`1E-05f`/`0.001f`, named constants
`FreshOpaqueOrTransparentDynamicViscosity`/`FreshOpaqueOrTransparentConvectionCoefficient`) instead of `0`
for these two fields. This is exactly the same class of correction the merge-gate note already anticipated
for the BuildingElement seed gate's `ground`/`markDelete`/`width` (which turned out not to need it) - here
applied one level down, on the construction-material mirror, where it genuinely was needed. Nothing about
the conservative foreign-object refusal gates was touched or weakened; this is purely the "what does a
value we never write read back as" mirror.

**Verification after the fix:**
- `SAM.Analytical.Tas.csproj` rebuilds clean (Debug, 0 errors).
- `SAM.Analytical.Tas.TM59.Tests` (net8.0): **337/337 pass** (was already 337/337 before the fix - the fix
  only changes what a value the tests never assert on-the-nose reads back as on real TAS; no COM-free test
  regressed or needed updating).
- `Gate.exe counts 200` re-run end to end: repeat-export deltas are now **Construction +0, BuildingElement
  +0, ApertureType +0, schedule +0, distinct aperture BE guid +0, part-mismatched surface +0** - the gate
  row now passes. (Zone +1 and aperture zoneSurface +400 on the repeat are expected: a repeat export adds a
  second zone/set of physical surfaces, exactly as Stage 1's own repeat-export scenario does.)

### 3. Definition variants - PASS, all 18
`Gate.exe variants`: construction variants C1-C8 (identical pane+frame reuse; different material; different
width; different layer order; frame-shared-across-different-panes; different frame material; pane-never-
equals-frame-even-with-identical-layers; same preferred name + different content -> deterministic
hash-suffixed distinct names) and building-element variants B1-B8 (different construction; different
colour; different opening control; no-openings bare element; opening multiplicity; one-vs-two-identical-
openings; window != door; `ApertureType.Undefined` still gets an element) **all match expectations**, and no
generated name contains a physical aperture GUID or `aperture.UniqueName()` in any scenario.

**B2 (colour) needed the harness's own expectation corrected, not the code.** First run expected 4 elements
(2 colours x 2 parts) and got 3; diagnosed by reading `Query/Color.cs` (pre-Stage-2, untouched by this
branch's diff - confirmed via `git log`/`git diff f3f5802..HEAD`): `ApertureParameter.Color` only overrides
the **pane's** colour; the frame always takes the type-derived default regardless. So 2 panes (one per
colour) + 1 shared frame = 3 is the CORRECT expected count. Harness fixed, re-run, all 18 pass.

### 4. Round trip - PASS, both former failures diagnosed by a licensed A/B against `f3f5802`
The two rows previously recorded here as unexplained failures (geometry, construction layers) were
**over-strict harness assertions, not Stage 2 regressions.** Settled by running the SAME round trip twice on
this machine - once with `SAM.Analytical.Tas.dll` built from `f3f5802`, once from HEAD - over one small
deterministic model, and diffing field by field. Method and result:

**Method.** A minimal `net8.0-windows` console harness (`RT.exe`, not committed, same discipline as the rest)
builds one 5 x 4 x 3 m space with two 1.0 x 1.2 m `SIM_EXT_GLZ` windows (3-layer pane Glass/Air/Glass +
Timber frame; window 1 carries `PartOOpeningProperties`, window 2 none), then runs
`analyticalModel.ToTBD(path)` -> save/close -> `Convert.ToSAM(path, false)`, dumping ~520 named fields per
run: SAM-side aperture geometry, part faces, layers, materials, opening properties and identity stamps;
TBD-side constructions, every material property, both stored widths, building elements, colours, `BEType`
and aperture types. Only `SAM.Analytical.Tas.dll` differs between the two runs; every other assembly, the
model and the code path are identical. Both sides were run twice - fully deterministic apart from
TAS-assigned GUIDs, including the collision-suffix hash.

**Result.** Of the ~520 fields: **219 SAM-side fields (every geometry, area, coordinate, layer, material,
transparency, opening-property and count field) are identical between baseline and Stage 2, and all 186
`tbd.construction` material/width fields are identical too.** The only differences are the intended Stage 2
sharing effects - aperture building elements 4 -> 3 (the frame is now shared by both windows; the two panes
stay separate because they genuinely differ: openable vs fixed changes both `Modify.SetColor`'s colour and
the aperture-type count), the definition-derived names, and the `BuildingElementGuid` stamps that follow
from them - plus TAS's own per-run GUIDs.

- **PASS** - physical aperture count preserved (2 -> 2) on both sides; sharing a `BuildingElementGuid` does
  not collapse physical apertures (invariant 9).
- **PASS** - `OpeningProperties` preserved on both sides (1 -> 1 aperture carrying one).
- **PASS** - pane/frame classification preserved on both sides.
- **PASS (was FAIL)** - geometry. The physical geometry round-trips exactly: the exported pane surface is
  0.99 m2 and the frame surface 0.21 m2, which is exactly what `Aperture.GetFace3Ds(Pane/Frame)` derives
  from the 1.2 m2 source aperture, and the imported aperture's external edge is the same 1.2 m2 rectangle at
  the same coordinates. What differs is only the imported `Aperture`'s composite `Face3D`: the export writes
  frame and pane as two surfaces, and `Convert.ToSAM` reassembles them into one face with a hole, so
  `GetFace3D().GetArea()` reads 0.21 (ring) where the source read 1.2 (solid). Comparing that composite area
  to the source's is what the old assertion did, and it is the wrong comparison - the derived part faces,
  which are what TAS simulates, agree to the last digit. **Identical on `f3f5802`.**
- **PASS (was FAIL)** - construction layers. Every orig -> round-trip layer difference is float read-back or
  a field TBD does not store, and every one of them is byte-identical on baseline: layer thicknesses
  `0.016 -> 0.016000001` and `0.05 -> 0.050000001` (`Convert.ToSingle`), `ThermalConductivity
  0.13 -> 0.129999995`, `Material.Group -> empty` (TBD has no such field), and - the first differing field
  walking the pane layers in order - `Glass 6mm` `SpecificHeatCapacity 750 -> NaN` and `Density
  2500 -> NaN`, because the TBD transparent `UpdateMaterial` overload writes neither (already documented in
  `Query/ConstructionMaterialDefinition.cs`). Layer ORDER, names, count, `additionalHeatTransfer` (0),
  construction type (`tcdTransparentConstruction` pane / `tcdOpaqueConstruction` frame) and both stored
  widths all round-trip exactly. **Identical on `f3f5802`.**

Two further pre-existing behaviours the A/B surfaced, both identical on baseline and therefore out of scope
here (noted so they are not rediscovered): `Convert.ToSAM` groups a zone's aperture surfaces by
`ApertureConstruction` guid and pairs them by descending area, so with two identical windows the imported
`Frame`/`Pane` `ZoneSurfaceReference`s can cross-pair between windows; and a `PartOOpeningProperties`
returns as a `ProfileOpeningProperties`. Neither is caused or changed by Stage 2.

### 5. A/B TAS/TSD simulation - PASS, bit-identical
**Model:** `C:\Users\Virtual Machine\Documents\SAM_daily\2026-08-05-PartO\SAM_zoningAM_v2.sam` - a real
9-zone Part O flat: 9 spaces, 50 panels, **20 apertures, every one pane+frame and every one carrying
`PartOOpeningProperties`**, all on the single `SIM_EXT_GLZ` aperture construction. Exported through
`analyticalModel.ToTBD(path)` with the model's OWN embedded weather (`United Kingdom, London`, lat 51.48,
lon -0.45) so both sides get byte-identical weather, then `Modify.Simulate(tbd, tsd, 1, 365)` - the same
full-year run period, timestep, controls and shading on both sides. The only difference between the two
runs is which `SAM.Analytical.Tas.dll` sits next to the harness.

**Pre-simulation equivalence - confirmed BEFORE simulating, as the gate requires:**

| | baseline `f3f5802` | Stage 2 |
|---|---|---|
| zones | 9 | 9 |
| physical `zoneSurface`s | 110 | 110 |
| of which aperture surfaces | 40 | 40 |
| total surface area | 3379.999993533 m2 | 3379.999993533 m2 |
| total aperture surface area | 64.799998492 m2 | 64.799998492 m2 |
| Constructions (aperture) | 6 (2) | 6 (2) |
| `ApertureType`s | 2 | 2 |
| **aperture BuildingElements** | **40** | **3** |

All **110 per-surface rows are byte-identical** - area, `type`, orientation, inclination, altitude,
altitudeRange, room-surface count, the construction assigned, `BEType`, colour and aperture-type count -
and all **510 construction/material/width lines are byte-identical**. Only the element NAMES differ, by
design: baseline writes one element per aperture per part (`Windows: SIM_EXT_GLZ <aperture-guid> -pane`),
Stage 2 writes `Windows: SIM_EXT_GLZ -frame` shared by all 20 windows plus two panes,
`Windows: SIM_EXT_GLZ -pane` and `Windows: SIM_EXT_GLZ_AAF00869 -pane`, because the model states two
distinct opening controls (`Opening Cd0.411 F1` and `Opening Cd0.477 F1`) - the same two `ApertureType`s
both sides already carry.

**Numeric result comparison:** 22 zone variables x 9 zones x 8760 h = 1,734,480 values, plus 7 surface
variables x 47 TSD surface records x 8760 h = 2,882,040 values.

- **values compared: 4,616,520**
- **values differing: 0**
- **maximum absolute difference: 0** (no location - there is no differing value)
- **maximum relative difference: 0** (same)
- verdict: **exactly zero**, not floating/solver noise. The two TSDs agree bit for bit.

Zone variables compared: DryBulbTemperature, MeanRadiantTemperature, ResultantTemperature, SensibleLoad,
HeatingLoad, CoolingLoad, SolarGain, LightingGain, InfiltrationVentilationGain, AirMovementGain,
BuildingHeatTransfer, ExternalConductionOpaque, ExternalConductionGlazing, OccupantSensibleGain,
EquipmentSensibleGain, HumidityRatio, relativeHumidity, LatentLoad, Infiltration, Ventilation,
ZoneApertureFlowIn, ZoneApertureFlowOut. Surface variables: InternalSolarGain, ExternalSolarGain,
InternalConduction, ExternalConduction, ApertureFlowIn, ApertureFlowOut, ApertureOpening. Five of the zone
variables (SensibleLoad, HeatingLoad, CoolingLoad, Ventilation, AirMovementGain) are all-zero in this
unconditioned model - stated so the count is not read as 22 independently varying quantities; the other 17
carry real magnitudes (e.g. ExternalSolarGain up to 9956.4, InfiltrationVentilationGain up to 12345.1,
DryBulbTemperature up to 31.77).

### 6. Real shaded-project regression - PASS
**Model:** `C:\Users\Virtual Machine\Documents\SAM_daily\2026-08-13-Shading\test-file-kolobrzeg.sam` -
one room with a real shading context (56 panels for one space; location Kolobrzeg, lat 54.18, lon 15.58)
and 3 pane+frame apertures. The Part O model above was tried first and produced no SAM solar results at all
(see the note below), so this is the model the regression actually ran on.

**Chain exercised, end to end, on both sides:** `SAM.Analytical.SolarCalculator.Modify.Simulate_Coverage`
over TAS's own 25 representative shade days x 24 h = 600 timesteps (56 surfaces, 62 coverage results
including each aperture's own `-pane`/`-frame` surfaces) -> `analyticalModel.ToTBD` -> `Modify.UpdateShading`
-> `Modify.Simulate(1, 365)` -> `Create.SolarModel(building)` -> `Modify.CopyResults` -> aperture
solar-result mapping.

**Stage 2 sharing was really in effect here** - aperture BuildingElements 6 -> 2 - while the physical side
stayed identical (12 zoneSurfaces, 6 of them aperture, total area 76.947999999 m2, 5 constructions, both
sides).

| Field | baseline `f3f5802` | Stage 2 |
|---|---|---|
| SAM coverage results attached | 62 (56 panels + aperture parts) | 62 |
| `UpdateShading` returned | true | true |
| TAS shade-day calendar | 25 days, no fallback | 25 days, no fallback |
| shade-proportion values read back | 7200 | 7200 |
| surfaces carrying shade data | 12 | 12 |
| `Create.SolarModel` linked faces / coverage results / values | 12 / 12 / 3096 | 12 / 12 / 3096 |
| `CopyResults` apertures with results | 3 | 3 |
| `CopyResults` pane / frame / panel results | 5 / 5 / 62 | 5 / 5 / 62 |
| per-aperture result rows (name, count, sum, max) | 10 | 10, identical |

**114 comparable dumped fields, 0 differing** - including every per-surface shade-proportion value count,
sum and max, and every per-aperture coverage row. The TAS simulation of the two shaded exports was compared
the same way as step 5: **928,560 values, 0 differing, max absolute difference 0.**

Re-running the same regression on the Part O model gave **146 comparable fields, 0 differing** and
**4,616,520 TSD values, 0 differing** as well, but with the shading chain empty on both sides (see below),
so it corroborates rather than adds coverage.

**Two harness-side findings, neither a Stage 2 issue, recorded so they are not rediscovered:**
- The shade read-back **must reopen the TBD read-WRITE**. On a read-only reopen TAS reports no shade-day
  calendar at all and `GetShadeProportion` returns -1 for every hour, so the whole chain looks empty.
  `Modify.LogShadeRoundTrip` already documents this; the harness hit it first-hand. `SAMTBDDocument.Dispose`
  only `close()`s, so a read-write reopen does not modify the file.
- `SAM.Analytical.SolarCalculator`'s `Simulate` and `Simulate_Coverage` both return **zero results** for the
  Part O model (`SAM_zoningAM_v2.sam`) even though it has a Location, weather data and 21 sun-exposed faces
  (9 roofs + 12 external walls), returning in ~0.1 s. The same calls work on the Kolobrzeg model. This lives
  in the sibling `SAM_SolarCalculator` repo, is untouched by this branch's diff, and was **not** chased -
  it is out of scope for Stage 2. It is only why the shaded regression uses the Kolobrzeg model.

### A/B build recipe (proven)
`git worktree add
../SAM_Tas_baseline_f3f5802 f3f5802` next to this checkout so its `..\..\..\SAM\build` hint paths still
resolve, then run MSBuild's `-t:Restore` and `-t:Build` as SEPARATE invocations (a combined
`-t:Restore,Build` fails with `CS0518 Predefined type 'System.String' is not defined` because the build
half does not pick up the assets the restore half just wrote). Use the .NET Framework MSBuild
(`vswhere -latest -find MSBuild\**\Bin\MSBuild.exe`) - `dotnet build` cannot run `ResolveComReference`.
`git diff f3f5802..HEAD` touches only `SAM.Analytical.Tas`, so the A/B is a one-DLL swap in an otherwise
identical output folder.

### Licensed harness (not committed - same discipline as Stage 1's `APERTURE_TYPE_REUSE.md` harness)
A standalone `net8.0-windows` console project (`Gate.exe`), referencing the built
`SAM_Tas/build/*.dll` set (SAM.Core/SAM.Analytical/SAM.Geometry/SAM.Weather/SAM.Architectural,
SAM.*.Tas, the Interop.* PIAs) by `HintPath` off a `LibsDir` MSBuild property, so the SAME harness binary
can be pointed at either this branch's `SAM.Analytical.Tas.dll` or the `sow/2026-Q3` baseline's for the A/B.
Commands implemented: `probe`, `sanity`, `diagnose <tbd>`, `diffconstruction <tbd>`, `counts <n>`,
`variants`, `roundtrip`, `inspect <model>`, `export <model> <weather|-> <tbd>`, `simulate <tbd> <tsd>`,
`compare <tsdA> <tsdB>`, `shaded <model> <weather|-> <dir>`. Source lived in a scratchpad and **has since
been lost with that session** - if this needs re-running from a fresh checkout, re-derive it from this
description and `ApertureDefinitionReuseTests.cs`'s fixture builders (`Library()`, `Glazing()`, `PartO()`)
rather than trying to recover the scratchpad files.

Steps 4, 5 and 6 were done with a second, smaller harness (`RT.exe`, also scratchpad-only), which is the
simpler thing to re-derive: `Program.cs` + `Commands.cs`, referencing the same DLL set with
`<Private>true</Private>` so the whole dependency closure lands in `bin`, plus
`<UseWindowsForms>true</UseWindowsForms>` (without it `SAM.Analytical.Query.Color` dies on
`System.Drawing.Common is not supported on this platform`) and the `SAM_SolarCalculator/build` assemblies
for step 6. It prints one `key<TAB>value` line per observed field so two runs diff mechanically. Commands:

    RT.exe rt <label> <outdir>                          # step 4: synthetic 2-window round trip
    RT.exe inspect <model.sam> <out.txt>                # what a real model carries
    RT.exe export <model.sam> <weather|-> <tbd> <out>   # ToTBD + full pre-simulation dump
    RT.exe surfaces <tbd> <out.txt>                     # pre-simulation dump of an existing TBD
    RT.exe sim <tbd> <tsd> <dayFirst> <dayLast>         # Modify.Simulate
    RT.exe compare <tsdA> <tsdB> <out.txt>              # the numeric A/B over zone + surface variables
    RT.exe shaded <model.sam> <weather|-> <label> <dir> # Simulate_Coverage -> ToTBD -> UpdateShading
                                                        #   -> simulate -> SolarModel -> CopyResults

Run each from two folder copies that differ only in `SAM.Analytical.Tas.dll`. The pre-simulation dump
deliberately reports each surface's geometry and construction assignment on one line and its building-element
NAME on another, because Stage 2 changes the name on purpose and only the first line is an A/B assertion.

**Important environment note found while building the harness:** target **`net8.0-windows`**, matching
`benchmark/SAM.Analytical.Tas.Benchmark.Cli` (the repo's own licensed-TAS CLI), NOT `net48`/`net481`. A
`net48` console host reproduces a genuine, deterministic `SAM.Core` defect
(`SAM.Core.Modify.SetValue(ParameterizedSAMObject, Assembly, ...)` re-adds the very `ParameterSet` it just
populated via `parameterizedSAMObject.Add(parameterSet)`, which self-`Copy()`s onto itself) on the FIRST
ever `.SetValue(SomeEnumParameter, value)` call on any object, because .NET Framework's
`Dictionary<TKey,TValue>` bumps its mutation-version counter on every indexer write, including a same-key
overwrite, so the self-enumerate-while-write throws `InvalidOperationException: Collection was modified`.
.NET 8's `Dictionary` does not bump the version on a same-key overwrite, so the identical call sequence is
harmless there - matching why the committed `net8.0` test suite and the `net8.0-windows` benchmark CLI never
hit it. This is a **pre-existing `SAM.Core` defect, out of scope for Stage 2** (it lives in the sibling `SAM`
repo, is unrelated to aperture-definition reuse, and is dormant under every environment this codebase is
actually run in) - noted here only so it is not rediscovered from scratch; not fixed, not filed.
Confirmed empirically with an isolated `sanity` probe (single `Space`, three `SetValue` calls) before
retargeting.

Two more harmless SAM.Core/SAM.Analytical quirks the harness had to route around while building its own
synthetic test model (again, not Stage 2, not fixed): `Create.AdjacencyCluster(shells, spaces)` and
`Create.Panels(shell)` both eventually touch `SAM.Analytical.ActiveSetting.Setting`, whose cold-start
`GetDefault()` hits the exact same self-`Copy()` pattern above on its own second `SetValue` call in a
process with no prior SAM settings load (e.g. this bare console harness, never the NUnit test host or a
Revit/Grasshopper session, both of which warm this differently). Routed around by assembling the harness's
`AdjacencyCluster`/`Panel`s by hand from `Query.PanelType(Vector3D)` and
`Create.Panel(Construction, PanelType, Face3D)` (both pure, no `ActiveSetting` touch) instead.

## Stage 2 - what landed

- `Classes/ConstructionMaterialDefinition.cs`, `ConstructionLayerDefinition.cs`,
  `ConstructionDefinition.cs`, `ApertureTypeAssignment.cs`, `BuildingElementDefinition.cs`,
  `BuildingElementSeed.cs` - immutable, COM-free value equality over the whole of what the export writes.
- `Query/ConstructionMaterialDefinition.cs` - the COM-free MIRROR of `Modify.UpdateMaterial`, field for
  field and clamp for clamp. This is what lets a construction already in the TBD be proven equal to one
  about to be written.
- `Query/ConstructionDefinition.cs`, `Query/BuildingElementDefinition.cs` - COM-free factories from a SAM
  `ApertureConstruction` / `Aperture`.
- `Query/ConstructionDefinitionTBD.cs`, `Query/BuildingElementDefinitionTBD.cs` - seed READERS. They read
  and decide nothing.
- `Query/BuildingElementDefinitionSeed.cs` - both seed GATES, as pure functions of what was read, which is
  what makes them testable with no installed TAS.
- `Query/ConstructionSignature.cs`, `ConstructionName.cs`, `BuildingElementName.cs` - deterministic
  FNV-1a signatures over exact Single bit patterns, and definition-derived naming.
- `Classes/BuildingReuseCache.cs` - extended with constructions and aperture building elements. Purely
  additive: 378 lines added, 7 removed, and all 7 are doc rewording. Stage 1's schedules, aperture types,
  day types and assignment tracking are byte-for-byte unchanged.
- `Modify/Update.cs` - the aperture block of the direct export resolves DEFINITIONS instead of names. The
  physical-surface block, the panel block and the identity stamps are unchanged.

## Stage 2 - decisions worth not re-deriving

- **The bug this fixes is not just duplication.** Both objects were looked up BY NAME, and a name match was
  taken as a content match. That was harmless only while the name carried the aperture's GUID; once names
  are derived from the reusable SAM `ApertureConstruction`, a by-name lookup would hand one window another
  window's glazing. Hence full content equality, and a deterministic collision suffix rather than adoption.
- **`AperturePart` is construction identity even though TAS does not store it.** The aperture import pairs a
  window's two constructions by stripping the `-pane`/`-frame` suffix and reading each side's layers, so a
  pane and a frame with identical layers must not collapse - the round trip would lose half the window.
  For the same reason the collision discriminator goes on the BASE (`SIM_EXT_GLZ_1F3A0C21 -pane`), keeping
  the part suffix terminal.
- **Underscores are KEPT in the construction name base**, unlike Stage 1's aperture-type naming. Real names
  are full of them (`SIM_EXT_GLZ`) and this base is the round-trip identity of the `ApertureConstruction`;
  stripping them silently renamed it. Found by a test, not by inspection.
- **The new names match what the TCD route already writes** (`Convert.ToTCD_Constructions`:
  `apertureConstruction.Name + " -pane"`), so the two routes agree and the round-tripped
  `ApertureConstruction` now carries the model's own name instead of an aperture's unique name. That is a
  genuine round-trip improvement, not just a rename.
- **NaN compares equal to NaN in the Stage 2 definitions**, unlike Stage 1's `ApertureTypeDefinition`. A
  material that states no conductivity stores NaN; under `==` such a layer would never equal itself and
  every window would get its own construction. NaN is normalised to the canonical `float.NaN` on the way in
  so the bit-pattern signature stays in agreement with equality, as signed zero already was.
- **`ApertureType.Undefined` is a distinct value, not a missing one.** Refusing it would take a building
  element away from an aperture that used to get one (the `Windows: ` prefix has always covered everything
  that is not a door). It shares among its own kind and never merges with a real window.
- **Mirror bugs can only cause under-reuse.** Two layers created in one export run through the same mirror
  on the same input, so a mirror that disagreed with the writer would give both the same answer and both
  the same TBD material. What it would cost is recognising a SEEDED construction.
- **Refusals are discarded on this path.** `Modify.Update` returns `void` with no notes channel and adding
  one would change its signature and every caller. Every outcome is the conservative one, so what is lost is
  diagnosability. Stage 3 owns reporting.
- **`UpdateBuildingElements` is unaffected**, despite decoding aperture GUIDs out of element names: it runs
  only on the gbXML/T3D route in `WorkflowCalculator`, never after a direct `Modify.Update`.
- **Seed classification is deferred** to the first lookup that needs it. `Modify.UpdateIZAMs` re-enters
  `Modify.Update` once per air handling unit with a synthetic, aperture-less cluster, and classifying every
  seeded construction's layers on each pass would be a per-IZAM COM cost paid for nothing.

## Stage 2 - validation performed

- `SAM_Tas.sln` builds with 0 errors (Debug) after the change; `SAM.Analytical.Tas` alone also builds clean.
- `SAM.Analytical.Tas.TM59.Tests`: **337/337 pass**. 233 pre-existing (unchanged, including all 80 Stage 1
  aperture-type tests) plus **104 new** in `ApertureDefinitionReuseTests.cs`.
- The new tests found two genuine defects, both fixed before commit: the construction name base was
  stripping underscores (so `SIM_EXT_GLZ` became `SIMEXTGLZ`), and an `ApertureType.Undefined` aperture was
  being refused a building element altogether.
- Licensed TAS: **not run.** See the merge gate above.

---

## Stage 1 (merged as PR #30) - retained for continuity

The subsections below describe Stage 1 as it was developed on `feature/tas-aperturetype-reuse`; "same
branch" in them means that branch, not this one.

### Stage 1 correction pass (2026-08-21, on `feature/tas-aperturetype-reuse`, commits rewritten in place)

Three focused fixes, no architecture change:

- **Exact numeric collision identity.** Display names stay rounded (`Opening Cd0.62 F1`), but
  `Query.ApertureTypeSignature` now carries the **exact IEEE-754 Single bit pattern** of Cd and factor
  (`SingleBitsHex`), so two TAS float definitions like `0.6201`/`0.6202` can never share a deterministic
  collision identity. Equality was and stays exact float equality.
- **Name reservation vs reusable registration.** `BuildingReuseCache` now separates the two:
  `ReserveScheduleName` / `ScheduleNames()` and `ReserveApertureType` hold the namespace of a created
  object whose write later fails (no `RemoveSchedule`; a created aperture type is left in place by
  policy), without ever making it reusable. `RegisterApertureType` upgrades a reservation in place,
  identified by the same COM reference. `GetOrCreateSchedule` reserves on naming; the shared
  `SetApertureType` path reserves on naming.
- **Full read-back verification of newly created shared types.** After the complete write (Cd,
  description, profile value/factor/setback/type/function, schedule, day types), the new type is read
  back through the existing seed reader and must equal the requested definition; otherwise the write
  refuses, keeping the name reserved and the object non-reusable and unassigned. Runs only for newly
  created definitions.

Files changed: `Query/ApertureTypeSignature.cs`, `Classes/BuildingReuseCache.cs`,
`Create/GetOrCreateSchedule.cs`, `Modify/SetApertureType.cs`, plus tests and docs.

Validation: `SAM_Tas.sln` 0 errors Debug + Release; `SAM.Analytical.Tas.TM59.Tests` **230/230** both
configurations (77 in `ApertureTypeReuseTests.cs`, incl. close-float collision, late-failure
name-reservation and read-back mismatch/refusal; existing schedule tests unchanged). Licensed-TAS
acceptance re-run on this machine after the hardening: 200 identical windows -> 1 ApertureType
(`Opening Cd0.395 F1 S00FFFE`) + 1 schedule + 200 assignments; repeat export -> +0/+0, no second
openings; 5 variants -> 5 distinct types; 50 windows x 2 identical children -> exactly 2 ordinal types.
No issue notes anywhere. The acceptance harness remains uncommitted (rebuilt as a scratch net481 console
per `APERTURE_TYPE_REUSE.md`); the produced `.tbd` files live under `%TEMP%\aperture-accept`.

Next step: open the Stage 1 PR (two commits: `feat(tas): reuse equivalent aperture types`,
`test(tas): validate aperture type reuse`).

### Codex review fixes (2026-08-21, on `feature/tas-aperturetype-reuse`, one commit on top of the two Stage 1 commits)

The Codex review of PR #30 raised two genuine findings; both are fixed:

- **P1 - index-derived reuse ordinal for the compatibility overload.** The
  `SetApertureType(building, buildingElement, single, out refusal, name, index)` overload forwarded a
  fixed ordinal, so two calls for two identical indexed openings both resolved to occurrence 1 and the
  second collapsed into the first's assignment. The legacy 1-based `index` now doubles as the reuse
  ordinal via the new COM-free `Query.ApertureTypeOrdinal(int)` (position is the occurrence - exact for
  identical children, conservative for different ones). The multiple-opening entry point already
  computes the true per-definition occurrence and is unaffected.
- **P2 - signed-zero hashing.** `ApertureTypeDefinition.Equals` uses float equality under which `-0f`
  and `+0f` are equal, while the signature hashed their distinct IEEE-754 bit patterns, so an equal pair
  could produce different hash codes (the .NET dictionary contract). The constructor now normalises
  signed zero to positive zero for Cd and factor, keeping `Equals`, `GetHashCode` and the deterministic
  name signature in agreement.

Files changed: `Classes/ApertureTypeDefinition.cs`, `Query/ApertureTypeDefinition.cs`,
`Modify/SetApertureType.cs`, `APERTURE_TYPE_REUSE.md`, plus tests.

Validation: `SAM_Tas.sln` 0 errors Debug + Release (CI recipe: sibling deps at `sow/2026-Q3` rebuilt
first); `SAM.Analytical.Tas.TM59.Tests` **233/233** Debug + Release (was 230/230; +3 regression tests:
`Equality_SignedZero_NormalisesBeforeEqualityAndHashing`, `Ordinal_IndexDerived_IsThePosition`,
`Export_TwoIdenticalIndexedWrites_ProduceTwoTypesAndTwoAssignments`).

### Stage 1 - what landed

- `Classes/ApertureTypeDefinition.cs` - immutable value equality over Cd, factor (after the Part O
  `AlwaysClosed -> 0` override), profile mode, function text, the 24 schedule values, description and
  day-type membership. COM-free.
- `Query/ApertureType{Definition,DefinitionTBD,Signature,Index,Name,Reconciliation}.cs` - the COM-free
  factory and ordinal keying, the seed reader (existing type -> definition, or a refusal), deterministic
  FNV-1a signature/collision hash, first-equal lookup, name derivation/decomposition/legacy-name test, and
  the reconciliation decision (Create / Reuse / Legacy / Refuse).
- `Classes/BuildingReuseCache.cs` - one COM pass over schedules, aperture types and day types; lifetime is
  one open document. Replaces two full aperture-type scans per opening child and a per-child rebuild of
  every schedule's 24 values.
- `Create/GetOrCreateSchedule.cs` - cache-taking overload only; behaviour identical, `cache: null` is the
  original byte for byte.
- `Modify/SetApertureType.cs` - the reuse path, plus `SetApertureType_Named` holding the previous
  per-element write verbatim for the legacy fence.
- Cache threaded through `Modify/SetApertureTypes.cs`, `Modify/Update.cs`,
  `Modify/UpdateBuildingElements.cs`. Every new parameter is optional and defaults to null, so all
  pre-existing call sites compile and behave unchanged.

### Stage 1 - decisions worth not re-deriving

- **S1-C0 = Outcome A (day-type membership is readable).** `TBD.IApertureType.GetDayType(int)` exists in
  the Interop.TBD metadata and licensed TAS confirms faithful read-back, including across save/reopen.
  Membership is therefore an equality field - compared **as a set**, because TAS reports it in the order
  `SetDayType` was called in, not calendar order. The conservative Outcome B policy is NOT in force.
- **Reuse writes nothing.** A shared definition is immutable; anything short of full equality creates a new
  type under a deterministic, collision-suffixed name. Proven by write-log assertions on the fakes.
- **`sheltered` is a conservative seed gate** added beyond the plan's list: SAM never writes it, so
  adopting a sheltered type would apply a shelter the model does not state. Refusing to reuse is the safe
  direction.
- **The licensed-TAS acceptance harness is not committed.** `SAM.Analytical.Tas.TM59.Tests` deliberately
  carries no COM reference (`TESTING.md`), and adding a second COM-referencing project is out of Stage 1's
  scope. The scenarios and their results are recorded in `APERTURE_TYPE_REUSE.md` so the run can be
  reproduced.

---

## Previous branch (merged): `feature/partf-terminal-transfer-compliance` (SAM_Tas#29)

The Part O availability schedule export is **complete and accepted on licensed TAS**. The mapping fix
landed in `7ef2aff3f3f81949be8b15bf6a797848c2800bf2` ("fix: correct TAS availability schedule mapping")
and the acceptance run passed with no warnings. Two further Codex findings on the TPD-full preparation
are implemented and tested. The final review cleanup (below) fixed the WorkflowCalculator stale-notes gate
and made the MultipleOpeningProperties schedule diagnostic per-child. The schedule-removal transition (D3)
remains deferred.

## TAS availability schedule export - accepted
Accepted commit: `7ef2aff3f3f81949be8b15bf6a797848c2800bf2`.

The adapter crosses two independent COM conventions, and crosses them separately:

- `Query.ScheduleValueFromTBD` - COM TRUE comes back as the VARIANT_BOOL bit pattern, so **-1 maps to 1**.
  Every other value passes through unchanged, so an unexpected read-back still fails the caller's
  comparison rather than being taken for "true".
- `Query.ScheduleIndexTBD` - TBD's 24-slot hourly indexed properties are 1-based hour-ending, so
  **SAM hour `h` is TBD slot `h + 1`** and the slots written are 1..24, never 0. Note that TBD's
  COLLECTION getters (`GetSchedule`, `GetBuildingElement`, ...) are 0-based; it is the hourly indexed
  properties, and only those, that start at 1.
- `Modify.SetScheduleValues` is the only place that writes schedule values, and it reads every written
  slot straight back through the same COM object. A mismatch is a **refusal** naming the SAM hour, the TBD
  slot, the written value, the read value and the raw COM value - the schedule is left unassigned rather
  than exporting a control that does not match the model.

Licensed acceptance result:

- 20 schedules requested, **20 read back**, **0 assignment warnings**.
- `PartO_DayOpen_08_23` visually correct in TAS: 00:00-08:00 OFF, 08:00-23:00 ON, 23:00-24:00 OFF.
- **Slot 24** verified separately with `openingHour = 23`, `closingHour = 24`.

Writing 0-based against a 1-based convention had put every hour one slot early - a 15-hour ON block from
07:00 to 22:00 instead of 08:00 to 23:00 - and writing and reading at the same wrong index masked it
completely.

## Completed (this session)
Final review cleanup - two P2 findings from a Codex review of `7ef2aff3`, verified against `296882e7`
and both confirmed:

- **`WorkflowCalculator.Calculate` stale notes.** The validation gate (`analyticalModel == null ||
  WorkflowSettings == null`) returned BEFORE `notes.Clear()`, so a re-used calculator whose next run was
  rejected still exposed the previous run's notes. Fixed by clearing at the top of `Calculate`, ahead of
  the gate. Regression: `WorkflowCalculatorTests` (3 tests; the previous run is simulated by writing the
  private notes list, the only COM-free way to populate it).
- **`SetApertureTypes` / `UpdateBuildingElements` schedule diagnostics checked only `apertureTypes[0]`.**
  For `MultipleOpeningProperties` the returned list is compacted (refused children are absent), so child
  order does not survive partial failure - checking the first entry both falsely reported a missing
  schedule when child 0 was unrestricted and child 1 was scheduled-and-delivered, and hid a later child's
  failure behind child 0's schedule. Fix: the write overload now reports the correspondence
  (`out List<int> childIndices`, parallel to the returned list), each requesting child is read back against
  the aperture type ITS write returned (`ScheduleDeliveryByChild` + `ApertureTypeSchedule`), and the
  requested/written counters count per requesting child. New COM-free seams `Query.OpeningScheduleRequests`
  and `Query.UndeliveredOpeningScheduleRequests` carry the pairing decision; strict refusal behaviour of
  `SetApertureType` is untouched. Regression: `OpeningScheduleDeliveryTests` (14 tests) covering
  Unrestricted+NightClosed, NightClosed+Unrestricted, NightClosed+NightClosed and the child-0-only failure
  modes. Summary note wording adjusted ("opening(s) requested") since counts are now per child.

Earlier this branch (`296882e7` and before):
- `ResultantTemperaturePreparation.Transferred`: a non-null but EMPTY `IndexedDoubles` zone-temperature
  series is now skipped exactly as an absent one is. It counted towards the payload before, so
  `TryBeginSecondPass` considered the transfer usable and copied the TBD, and the COM write beyond that
  seam reads `values.Count` off it - either throwing out of a route whose contract is refusal, or writing
  a default series that is then reported as a systems-aware answer. (Codex 3820973150)
- `ResultantTemperaturePreparation.TryBeginSecondPass`: a failed `File.Copy` - a locked or read-only
  `_TPDThermostat.tbd`, the routine case being a previous run still open in TAS - is now a refusal with
  the payload cleared, not an escaping `IOException`. `Modify.CalculateResultantTemperature` does not
  catch, so it would otherwise surface as a bare exception on a port that reports every other failure as
  a refusal. The design TBD is still never touched. (Codex 3815817512)
- Regression tests for both, in `PreparationBoundaryTests`:
  `AnEmptyZoneTemperatureSeries_IsNotTakenAsAUsablePayload` and
  `ACopyTargetThatCannotBeWritten_IsRefusedRatherThanThrown`.

## Final pre-merge pass (this session) - backlog triage and fixes

The rounds 1-2 Codex backlog on PR #29 was re-triaged against the current head. Implemented:

- `ApproximateResultantTemperatureMap.Synthesise`: the zone-temperature series must now cover EXACTLY
  the radiant series - the count must equal the radiant length and the maximum index must be
  `radiantCount - 1`. A longer series was previously truncated silently; a gapped series (min 0, correct
  count, a missing key) was zero-filled by the bounded read - both fabricated plausible resultant
  temperatures. (Codex 3803884880, 3796840741)
- `PartODiagnosticLog.BuildHourlyRecords`: identity fields (`designSpaceGuid`, `simulatedSpaceGuid`,
  `designZoneGuidRaw`, `simulatedZoneGuidRaw`, `identityMode`, `series`) are now set on EVERY hourly
  record, including the non-extended refusals - previously those rows could not be attributed to a flat,
  which is exactly what this logger exists to diagnose. (Codex 3804809062)

**Confirmed already fixed by earlier commits:** 3795669836 (radiant value validation - `TryGetDouble`),
3796840735 (cluster clone semantics), 3815817499 (conflict refusals surfaced to the workflow),
3815817505 (conflict check precedes every profile-control write; the method's non-transactional
`description` write is documented), 3795669830 (empty-space refusal in ToTM59).

**Classified DEFERRED / legacy / next branch:** 3802556065 (legacy approximate route treats an empty
model as supported - contract of the compatibility route; the TPD-full route refuses empty payloads),
3804809050 (scenario overload for `ToTBD` - new API; the Part O production component routes scenarios
through `ToXml` with the map directly), 3803359698 (last-wins duplicate results - documented, deliberate),
3821601792 (D3 schedule-removal transition - parked).

## Workflow diagnostics
`SAM_Tas_Grasshopper` `f023594ef3f53a9d8d2411b6fe5bc21a9b363ad0` ("chore: clean TAS workflow schedule
diagnostics") removed the verbose per-aperture D1 success commentary. A successful run is quiet; problem
lines still reach the canvas.

## Deferred
- **D3 - schedule-removal transition (Codex 3821601792).** Clearing an obsolete TBD schedule when an
  opening restriction is removed. Not implemented: `TBD.Building` exposes no `RemoveSchedule`, and
  ownership of a schedule cannot be proven across processes because reuse-by-value deliberately adopts
  either a previous export's schedule or a user-authored one. Needs its own design pass. **No transition
  code exists in this repository** - there is no `Query/ScheduleTransition.cs` and no
  `TryResolveScheduleTransition`.
- **D2 - aperture matching.** Parked. `Query.Apertures(...)` already performs the relevant `Face3D.InRange`
  geometry check, and the D2 proposal introduced a tolerance inconsistency. No D2 code exists here.

## Files changed

Profile definition reuse (this session, branch `feature/tas-profile-definition-reuse`):
- `SAM_Tas/SAM.Analytical.Tas/Classes/ProfileDefinition.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Classes/ProfileReuseIndex.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Query/ProfileSignature.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Query/ProfileName.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Query/ProfileReuseIndex.cs` (new - the only COM read)
- `SAM_Tas/SAM.Analytical.Tas/Convert/ToSAM/ProfileLibrary.cs` (index overload)
- `SAM_Tas/SAM.Analytical.Tas/Convert/ToSAM/InternalCondition.cs` (index parameter + reference resolution)
- `SAM_Tas/SAM.Analytical.Tas/Convert/ToSAM/Space.cs` (index threaded)
- `SAM_Tas/SAM.Analytical.Tas/Convert/ToSAM/AdjacencyCluster.cs` (index threaded)
- `SAM_Tas/SAM.Analytical.Tas/Convert/ToSAM/AnalyticalModel.cs` (index built once, threaded to all three consumers)
- `SAM_Tas/SAM.Analytical.Tas/Modify/AddUnusedInternalConditions.cs` (index parameter - the review's uncovered path)
- `SAM_Tas/SAM.Analytical.Tas/Modify/AddUnusedConstructions.cs` (cref only)
- `SAM_Tas/SAM.Analytical.Tas/Modify/UpdateInternalConditionTemplate.cs` (crefs only)
- `SAM_Tas/SAM.Analytical.Tas/PROFILE_DEFINITION_REUSE.md` (new)
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/ProfileDefinitionReuseTests.cs` (new, +27)
- `PROJECT_PROGRESS.md` (this file)

`Convert/ToSAM/Profiles.cs` and `Convert/ToSAM/Profile.cs` are deliberately UNTOUCHED - they are the legacy
library build that `ToSAM_ProfileLibrary(TBD.Building)` still uses when no index is supplied.

Stage 1 - reusable aperture types (earlier session, branch `feature/tas-aperturetype-reuse`):
- `SAM_Tas/SAM.Analytical.Tas/Classes/ApertureTypeDefinition.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Classes/BuildingReuseCache.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Enums/ApertureTypeProfileMode.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Enums/ApertureTypeReconciliation.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Query/ApertureTypeDefinition.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Query/ApertureTypeDefinitionTBD.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Query/ApertureTypeSignature.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Query/ApertureTypeIndex.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Query/ApertureTypeName.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Query/ApertureTypeReconciliation.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas/Create/GetOrCreateSchedule.cs` (cache overload only)
- `SAM_Tas/SAM.Analytical.Tas/Modify/SetApertureType.cs`
- `SAM_Tas/SAM.Analytical.Tas/Modify/SetApertureTypes.cs`
- `SAM_Tas/SAM.Analytical.Tas/Modify/Update.cs` (cache construction + one call site)
- `SAM_Tas/SAM.Analytical.Tas/Modify/UpdateBuildingElements.cs` (cache construction + one call site)
- `SAM_Tas/SAM.Analytical.Tas/APERTURE_TYPE_REUSE.md` (new)
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/ApertureTypeReuseTests.cs` (new, +80)
- `PROJECT_PROGRESS.md` (this file)

Final pre-merge pass (previous branch):
- `SAM_Tas/SAM.Analytical.Tas.TPD/Classes/ApproximateResultantTemperatureMap.cs`
- `SAM_Tas/SAM.Analytical.Tas.TM59/Classes/PartODiagnosticLog.cs`
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/PreparationBoundaryTests.cs` (+2)
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/PartODiagnosticLogTests.cs` (extended hourly-refusal test)
- `PROJECT_PROGRESS.md` (this file)

Final review cleanup (committed 2026-08-20 as "fix: verify opening schedules per child and clear workflow
notes per run" + "docs: record final Part O review cleanup"):
- `SAM_Tas/SAM.Analytical.Tas/Classes/WorkflowCalculator.cs`
- `SAM_Tas/SAM.Analytical.Tas/Modify/SetApertureTypes.cs`
- `SAM_Tas/SAM.Analytical.Tas/Modify/UpdateBuildingElements.cs`
- `SAM_Tas/SAM.Analytical.Tas/Query/OpeningScheduleRequests.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/WorkflowCalculatorTests.cs` (new, +3)
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/OpeningScheduleDeliveryTests.cs` (new, +14)

Previous session:
- `SAM_Tas/SAM.Analytical.Tas.TPD/Classes/ResultantTemperaturePreparation.cs`
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/PreparationBoundaryTests.cs` (+2)
- `PROJECT_PROGRESS.md` (this file)

## Validation

This session (`feature/tas-profile-definition-reuse`):
- `SAM_Tas.sln` built with the VS Framework MSBuild in **Debug and Release**: 0 errors. Only the
  pre-existing MSB3270/MSB3277 and XML-doc warnings; the new files add none.
- `SAM.Analytical.Tas.TM59.Tests` Debug **484 passed / 0 failed**, Release **484 passed / 0 failed**
  (457 pre-existing and unmodified, + 27 new). `SAM.Analytical.Tas.Benchmark.Tests` 16/16 Release.
- `ModelA-Tas.sam` verified independently of the code, straight off the fixture: 42 `SAM.Analytical.Profile`
  entries, **20 distinct `(Category, flattened Values)`**, name collisions at exactly
  `Infiltration::Constant` and `Heating::HTG_7to19_21`, and no `Ventilation`-category profile at all (the
  known dangling `VentilationProfileName`). Those counts are reproduced behaviourally by
  `ModelA_FortyTwoProfilesCollapseToTwenty` / `ModelA_TheTwoNameCollisionsAreDiscriminated` from the fixture's
  own slot data, so the test does not depend on a file in a sibling repo.
- **Licensed TAS A/B run and PASSED** (2026-08-23) against baseline **`2950b27c`** (round 1) and
  **`03f97570`** (round 2, after PR #36 merged) — see "Last updated" for the table and
  `PROFILE_DEFINITION_REUSE.md` → "Licensed acceptance" for the full evidence. Note the baseline for this
  feature is `2950b27c` / `03f97570`, **not** `fff9984d`: that SHA belongs to the PR #34 gbXML aperture
  programme and is 16 commits too old here, so using it would fold the unrelated PR #34/#35 aperture
  changes into the comparison.

Earlier sessions:
- `SAM_Tas.sln` rebuilt with the VS Framework MSBuild in **Debug and Release**: 0 errors. Only the
  pre-existing MSB3270 (MSIL vs AMD64 interop) and MSB3277 (System.Memory unification) warnings.
- `SAM.Analytical.Tas.TM59.Tests` Debug: **233 passed, 0 failed**. Release: **233 passed, 0 failed**
  (153 pre-existing, unmodified, + 80 aperture-type-reuse tests - 77 Stage 1 + 3 for the Codex fixes:
  signed-zero equality/hashing, index-derived ordinal mapping, and the indexed-twins write regression).
- Licensed TAS acceptance (2026-08-21, before the Codex fixes; the fixes are COM-free seams exercised by
  the tests above - the licensed harness was not re-run for them), all pass, driven
  through the real `Modify.SetApertureTypes` -> `SetApertureType` -> `BuildingReuseCache` ->
  `Create.GetOrCreateSchedule` against a real `.tbd` created and reopened via `TBD.TBDDocument`:
  - 200 identical windows -> **1 ApertureType** (`Opening Cd0.395 F1 S00FFFE`), **1 schedule**, 200
    assignments, no issue notes;
  - repeat export into that TBD -> **0** additional types/schedules, no element gained a second opening;
  - 10 **new** elements added to the saved TBD -> **0** additional types/schedules, each adopts the
    **seeded** type (so the seed read survives save/reopen);
  - 5 control variants over 200 windows -> **5 ApertureTypes**, names distinct, none carrying aperture
    identity;
  - 50 windows x 2 identical children -> **exactly 2 ApertureTypes**, every element keeps both openings;
  - legacy per-element type -> written in place, no shared type created alongside;
  - stale shared type -> refused with the type named, Cd unchanged, no second opening, no replacement type.
- Note the documented build order: the test project references already-built assemblies by `HintPath`
  (the COM-referencing projects cannot be built by the .NET Core MSBuild), so the SAM libraries and
  `SAM_Tas.sln` must be built before `dotnet test`. See `SAM.Analytical.Tas.TM59.Tests/TESTING.md`.
  This session followed the CI recipe: sibling deps cloned at `sow/2026-Q3`, benchmark schema built,
  all dependency solutions rebuilt with the Framework MSBuild, then `SAM_Tas.sln` Debug + Release and
  `dotnet test` per configuration.

## Issues / blockers
- ~~Licensed TAS A/B outstanding for profile definition reuse~~ — **CLEARED 2026-08-23, PASSED.** The gate
  ran against baseline **`2950b27c`** (round 1) and **`03f97570`** (round 2, the current merge-base after
  PR #36), one-DLL swap, on two real models. Compared per zone / internal condition / profile slot:
  internal-condition count, internal-condition names, the profile slot, the TAS profile type, the complete
  values, `factor`, `function`, `setbackValue` — **852 and 5754 simulation-effective fields, 0 differences**
  — plus a full simulation (**227 760 and 1 024 920 hourly values, 0 differing**). SAM `Profile` counts
  **42 → 20** and **369 → 30**. The predicted diagnostic-only differences (`profile_TBD.name`,
  `profile_TBD.description`, `thermostat.name`) were the only ones. Detail in
  `PROFILE_DEFINITION_REUSE.md` → "Licensed acceptance".
  *The baseline for this feature is `2950b27c` / `03f97570`, NOT `fff9984d`* — that SHA is PR #34's gbXML
  aperture baseline, 16 commits too old, and would drag the unrelated PR #34/#35 aperture work into the diff.
- **Open, name-only, pre-existing:** the export writes one profile name onto two differently-valued TAS
  profiles (a zone's internal condition and its HDD sibling both take the space condition's profile name
  while keeping their own values). Across repeated full round trips that makes 2 of the 20 shared names
  accrete one `_<hash>` suffix per generation. The definition count never grows and no simulation value
  changes; the baseline is worse on the same measure (24 of 42 names re-nest per generation). Fixing the
  export's name/value mismatch is separate work.
- Pre-existing and deliberately not fixed *in PR #37*: `ticV` was never emitted into the imported
  `ProfileLibrary`, so `VentilationProfileName` dangled (pinned as baseline by a test, not silently
  changed). **PR #38 fixes this** - see "Last updated" above;
  the TBD function-profile import does not preserve complete function semantics, which is why zero-length
  profiles are excluded from dedup; TBD `InternalCondition` sharing is unchanged.
- Carried over from earlier branches: D3 (schedule-removal transition) and D2 (aperture matching)
  remain deferred - see **Deferred** above.

## Next step
- The licensed A/B is done and recorded, PR #36 is merged into this branch, and
  [PR #37](https://github.com/SAM-BIM/SAM_Tas/pull/37) is open against `sow/2026-Q3` with the Copilot
  automated review addressed (see "Post-review fix (2)" below). Remaining: human review of PR #37, then
  merge. Merging remains a human call - it was not done here.
- Stage 2 (`ConstructionDefinition` + `BuildingElementDefinition`, direct-export path only) follows the
  frozen plan section E on its own branch. Do not start it inside this one.
