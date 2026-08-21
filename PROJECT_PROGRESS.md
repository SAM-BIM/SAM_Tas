# Project Progress

## Branch
`feature/tas-aperture-definition-reuse` (off `sow/2026-Q3` at `f3f5802`, i.e. after PR #30 merged).
Stage 1 was `feature/tas-aperturetype-reuse` (PR #30, merged 2026-08-21).

## Last updated
2026-08-21 - **Licensed-TAS acceptance IN PROGRESS, session paused here to switch machines.** Object counts,
repeat-export (+ the one genuine defect it caught, now fixed) and all 18 definition-variant scenarios are
done and PASS - see "Stage 2 - licensed acceptance progress" below. Round trip is PARTWAY: aperture count
and `OpeningProperties` count PASS, but geometry (max area difference ~1.03 m2) and construction-layer
comparison FAIL on the very first run, **not yet diagnosed** - status unknown (likely a harness geometry
fidelity issue rather than a Stage 2 defect, since Stage 2 states physical surfaces/geometry are untouched,
but this is NOT confirmed - do not assume). The A/B TAS/TSD simulation and the shaded-project regression
have not been started. **The user is switching to another laptop with no TAS licence - the licensed run
must continue on a TAS-licensed machine** (this one, when back, or another one that also has a working EDSL
Tas licence).

**Immediate next step on resume:** diagnose the round-trip geometry/layer failures first (see "Round trip -
IN PROGRESS, 2 unexplained failures" below for exactly where to pick up: `Aperture.GetThickness`/
`GetFace3Ds(AperturePart)` were being read when the session paused, to check whether the harness's synthetic
apertures state a frame thickness/offset TAS-side reconstruction expects that the harness never set -
before concluding this is a real defect.**

## Current status
**Stage 1 is merged.** The export shares one `TBD.ApertureType` across every building element stating the
same opening control. Full detail, the S1-C0 probe result and the licensed-TAS acceptance table live in
`SAM_Tas/SAM.Analytical.Tas/APERTURE_TYPE_REUSE.md`.

**Stage 2 is implemented and awaiting the licensed-TAS gate.** The direct `Modify.Update` export now shares
one `TBD.Construction` and one aperture `TBD.buildingElement` across every aperture stating the same
content, instead of creating one per aperture per part. 200 identical windows go from 400 constructions and
400 elements to 2 and 2, while all 400 physical `zoneSurface`s remain. Full detail, invariants, seed gates,
deliberate limitations and the acceptance table live in
`SAM_Tas/SAM.Analytical.Tas/APERTURE_DEFINITION_REUSE.md`.

The frozen three-stage plan both implement is
`C:\Users\Virtual Machine\.claude\plans\you-are-in-plan-lazy-pebble.md` (approved rev. 2, 2026-08-21).
Stage 3 (physical-instance identity hardening on update/round-trip, the `UpdateBuildingElements`
name-decode replacement, import grouping, refusal reporting on `Modify.Update`) is **not started**.

---

## Stage 2 - blocking merge gate (NOT yet done)

Do not merge Stage 2 without these. They need licensed TAS on this machine.

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

### 4. Round trip - IN PROGRESS, 2 unexplained failures (session paused here)
`Gate.exe roundtrip` (25 windows, two distinct constructions, half with `OpeningProperties`, all through
`Modify.Update` -> `Convert.ToSAM(TBD.Building)`):

- **PASS** - physical aperture count preserved (25 -> 25): shared `BuildingElementGuid` does **not**
  collapse physical apertures, confirming invariant 9 (`BuildingElementGuid` is reuse-definition binding,
  not physical identity) round-trips correctly even though only 4 distinct BE guids cover 25 apertures.
- **PASS** - `OpeningProperties` preserved (13 -> 13 apertures carrying one).
- **PASS** - pane/frame classification preserved (every imported construction has both a pane and a frame
  layer list).
- **FAIL** - geometry: max aperture area difference ~1.03 m2 (source apertures are nominally 1.0 m wide x
  1.2 m tall = 1.2 m2, so this is not solver noise - something is materially different).
- **FAIL** - construction layers: 1 mismatch reported between a source `ApertureConstruction`'s
  pane/frame layers and the corresponding imported one.

**Not yet diagnosed - genuinely unknown whether this is a harness artifact or a real defect.** Was mid-way
through reading `Aperture.GetThickness(AperturePart)` / `Aperture.GetFace3Ds(AperturePart)`
(`SAM/SAM.Analytical/Classes/Aperture.cs:283` and `:119`) to check whether the harness's synthetic
apertures (`Gate/src/Runs.cs: RoundTrip()`, built via plain `new Aperture(apertureConstruction, polygon3D)`
with no explicit frame width/offset parameter set) state a frame geometry the way a real, Revit-derived
aperture would, or whether they degenerate to a zero-width/coincident frame that then reads back
differently once through TBD. **Stage 2 does not touch physical geometry or the layer WRITE path at all**
(see invariant 10 and "What each file does" in `APERTURE_DEFINITION_REUSE.md` - `Modify/Update.cs`'s
physical-surface block is unchanged), which is why a harness-geometry explanation is the leading hypothesis
- but this must be CONFIRMED, not assumed, before concluding the round-trip row passes. If it turns out to
be real, the geometry read (`Convert.ToSAM_AdjacencyCluster`/`ToSAM_ApertureConstruction`) is Stage-3-owned
territory per the doc's "Out of scope for Stage 2" section, and it may indicate Stage 2 exposed a
pre-existing issue there rather than caused one - re-run the SAME round trip against the `sow/2026-Q3`
baseline (see "A/B" below for how to build that side) to tell those apart before touching any code.

### 5-6. A/B TAS/TSD simulation and real shaded-project regression - NOT YET RUN
Harness commands are implemented (`export`, `simulate`, `compare`, `shaded`) but not yet exercised. For the
A/B: build the harness a second time with `LibsDir` pointed at a `SAM_Tas/build` produced from the
`sow/2026-Q3` baseline checkout (a separate worktree/clone, since this checkout is on
`feature/tas-aperture-definition-reuse`), export the SAME real model through both, simulate both, then
`Gate.exe compare <tsdA> <tsdB>`. For the shaded project: the user's real Part O model at
`C:\Users\michal.dengusiak\OneDrive - Tetra Tech, Inc\Documents\SAM_daily\2026-07-15 PartO\SAM_zoningAM.sam`
(confirmed to exist, 128 KB, itself a zip `AnalyticalModel`) plus a weather file (e.g.
`GBR_London_TRY.epw` in the same folder) is a good candidate - not yet tried.

Continue all of steps 3 (done) through 6 in a session on a TAS-licensed machine; do not attempt the licensed
rows on a machine without a working TAS licence.

### Licensed harness (not committed - same discipline as Stage 1's `APERTURE_TYPE_REUSE.md` harness)
A standalone `net8.0-windows` console project (`Gate.exe`), referencing the built
`SAM_Tas/build/*.dll` set (SAM.Core/SAM.Analytical/SAM.Geometry/SAM.Weather/SAM.Architectural,
SAM.*.Tas, the Interop.* PIAs) by `HintPath` off a `LibsDir` MSBuild property, so the SAME harness binary
can be pointed at either this branch's `SAM.Analytical.Tas.dll` or the `sow/2026-Q3` baseline's for the A/B.
Commands implemented: `probe`, `sanity`, `diagnose <tbd>`, `diffconstruction <tbd>`, `counts <n>`,
`variants`, `roundtrip`, `inspect <model>`, `export <model> <weather|-> <tbd>`, `simulate <tbd> <tsd>`,
`compare <tsdA> <tsdB>`, `shaded <model> <weather|-> <dir>`. Source lives in this session's scratchpad
(`.../scratchpad/gate/src/*.cs`) - not durable across machines/sessions; if this needs re-running from a
fresh checkout, re-derive it from this description and `ApertureDefinitionReuseTests.cs`'s fixture builders
(`Library()`, `Glazing()`, `PartO()`) rather than trying to recover the scratchpad files.

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

Stage 1 - reusable aperture types (this session, branch `feature/tas-aperturetype-reuse`):
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
- **None blocking Stage 1.**
- Open question for Stage 2, not Stage 1: whether one `buildingElement` shared across many openable
  windows' surfaces is simulator-equivalent. The frozen plan gates the Stage 2 merge on a manual
  licensed-TAS TSD A/B result-equality test; the panel path's existing behaviour is precedent, not proof.
- Carried over from the previous branch: D3 (schedule-removal transition) and D2 (aperture matching)
  remain deferred - see **Deferred** above.

## Next step
- PR #30 is open against `sow/2026-Q3`; the two Codex review findings are fixed, committed and pushed,
  and CI (build + SPDX) runs on the new head. Merge PR #30 once CI is green and the review approves.
- Stage 2 (`ConstructionDefinition` + `BuildingElementDefinition`, direct-export path only) follows the
  frozen plan section E on its own branch. Do not start it inside this one.
