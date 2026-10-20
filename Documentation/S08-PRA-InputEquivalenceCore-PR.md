# S08 PR-A: input-equivalence evaluation core

Task S08 of the SAM simulation roadmap (`SAM` repository, `documentation/SIMULATION_ROADMAP_Q4_2026.md`), first of the
planned PR-A / PR-B / PR-C slices. Contract: `documentation/SIMULATION_INPUT_MAPPING_S07.md` (SAM#216, merged
`68b60257`, closed out `c817804f`).

## Status

* **PR:** [SAM_Tas#104](https://github.com/SAM-BIM/SAM_Tas/pull/104), draft, into `sow/2026-Q4`. Not merged; merge needs
  explicit owner authorisation. It stays draft for independent re-review.
* **Branch:** `feature/s08-input-equivalence-core`, base `sow/2026-Q4` at `e84aeff6`.
* **Authority:** the owner authorised PR-A only, restricted to SAM_Tas ("I authorize PR-A only"). PR-B (native reader),
  PR-C (fixtures and acceptance) and every other repository are not started.
* **Revision history:**
  * Revision 1: content `da0f5529545eca519782e9f4e310b7f44cf3a1de`, record-only head `7d7f2964e0985aff34e377f9739a77ca0b06c12f`
    (the head the independent review examined).
  * Revision 2 (this record): the four independent-review findings, section "Revision 2" below. **Validated content SHA:
    `ab5754483587fada9e8fc84e084436f24e3ffd05`.** A record-only commit (this file) follows it; the local results below were
    obtained on that content. The final PR head and its GitHub checks are identified in the PR description.

## Work completed

New COM-free library `benchmark/SAM.Analytical.InputEquivalence` (net8.0, references `SAM.Core` and `SAM.Geometry` from
`SAM/build` only) and its test project `benchmark/SAM.Analytical.InputEquivalence.Tests` (NUnit 4.2.2):

1. **Reader-result and quantity DTOs.** `ReaderResult` keeps `Absent`, `Unreadable`, `NaN`, `Zero` and `Finite` distinct;
   `ExpectedValue` carries the S07 input state (S-ZERO, S-UNSPEC, S-INVALID, S-VALUE); `QuantityInput`,
   `QuantityResult`, `GateReport`, `SeriesResult`.
2. **The S07 section 5 numeric rule.** `Query.Compare`: within when `d <= abs` or `d / max(|n|,|s|) <= rel`; a criterion
   S07 leaves blank does not apply; `d == 0` is identical without forming 0/0; NaN, infinities and overflow are not
   comparable and can never be within. `QuantityCatalog` holds every Tas-applicable row as data; a test locks each
   number to the S07 table.
3. **Outcomes and the rank gate.** All S07 section 6 outcome categories, the state table (every SAM state against every
   reader state), the six ranks (Fail, Blocked technical, Blocked owner decision, Not run, Not pass accepted
   limitation, Pass) and `Query.Gate` (lowest rank decides; PASS is a strict allow-list; a PASS needs an explicit,
   complete list of the required quantity-on-object comparisons, see Revision 2; per fixture).
4. **Independent expected values that need no TAS.** Floor area, net/gross area, closed-shell volume (`Shell.Volume`),
   T-FRAME percentage and width (writer rule), gain and people densities, infiltration flow, D-F2 ventilation state,
   requirement and delivery as separate quantities, opaque and glazing U with the approved films, design-day year
   day, EFLH.
5. **Schedules and mathematics.** `ExpandSchedule` (1, 24 or 8760 values; 8784 reported as an unsupported leap year,
   never truncated), hourly series evaluation, `FindHourShifts` for alignment diagnosis, reader-state propagation
   through products (`Query.Multiply`).
6. **Provisional OD-G1 to OD-G3 rules.** `GateRuleSet.Interim` (default) and `GateRuleSet.ProvisionalProposed`.
7. **Tests.** 229 licence-free tests (177 from revision 1 plus the revision 2 regression cases), including an
   exhaustive sweep of 663,552 input combinations asserting the PASS allow-list invariants, and a check that the
   provisional rule set is never more lenient than the interim one.

CI: one step added to `.github/workflows/build.yml` runs the new tests next to the offline benchmark producer tests.

## Important decisions and assumptions

These are mine where S07 is silent; each is fail-closed and visible, and each needs owner review.

* **Identity safeguard.** `QuantityInput.Identity` defaults to `NotEstablished`. Writer-generated names, order and
  GUIDs reach at most `WriterOnly`; only a one-to-one match of vertex geometry (`Query.MatchPolygons`) reaches
  `Verified`. `NotEstablished` and `WriterOnly` give UNVERIFIED-unresolved (rank 4, NOT RUN); a mismatch, an
  unmatched or a many-to-one pairing is an EXPORT-DEFECT (rank 1). I mapped "UNVERIFIED/BLOCKED" to rank 4 because that
  is S07's rank for a readable but unresolved item; if the owner wants rank 2 or 3 for identity it is one line.
* **Rule sets.** The S07 interim treatment is the default and both rule sets are fail-closed. A G case is at best
  BLOCKED (owner decision named) under either; `ProvisionalProposed` only applies the proposed EXPORT-DEFECT rank 1
  for G3 and G1-NaN. No owner acceptance lifts a proposed case, and UNLISTED-DEFAULT cannot be accepted (it is not an
  `AcceptableLimitation`) until OD-G1 is approved. Nothing is labelled approved.
* **"Gated" flags** in the catalogue: since revision 2 `Gated == false` has exactly three members (`3b.ground`,
  `3c.g`, `9.ground`), each an exclusion S07 states in its own text (F-OPQ ground/adiabatic "not gated", derived g
  "informational", ground temperature "not gated"). A numeric mismatch on them is informational; the general
  OD-G2 definition of "gated" is still the owner's to approve (revision 1 wording that the flag only names an owner
  decision no longer holds).
* **S-INVALID** is used for a stored area that is missing, NaN or not positive when a per-area value is needed, for
  negative gains, and for undefined schedule lengths (only 1, 24 and 8760 values are expanded). S07 names the area
  case; the others are my reading of "out of range".
* **8784-hour schedules** become UNSUPPORTED-MAPPING (rank 3 until accepted), matching the plan's leap-year
  recommendation. The owner has not yet decided the leap-year policy.
* **D-F2.** A Ventilation profile without a stated requirement is `Indeterminate` and S-INVALID; D-F2 does not define it.
* **S-ZERO with a native object omitted** is not rescued by a limitation listing (S07 lists only a verified T-* for it).
* **Exact rows** (design-day `yearDay`) are an absolute criterion of 0. EFLH is the sum of the hourly fraction values.
* **Inputs this core does not derive** because S07 gives no rule: gas-gap resistance, glazing g, heat-flow direction
  from geometry, selection of floor polygons from a frozen model.
* The projects are **not added to `SAM_Tas.sln`**: CI runs `dotnet test` on the test project directly, like the other
  offline test steps, and the production solution build is untouched.
* Row 3a of S07 lists "absorptance"; SAM stores and the writer passes *reflectance* (`MA/Modify/UpdateMaterial.cs`).
  The catalogue row is generic ("layer property"), so this is left for the PR-B reader and a wording correction in
  the roadmap follow-up.

## Revision 2: the four independent-review findings

Each finding was reproduced on the reviewed head `7d7f2964` before any fix (temporary probes against the revision 1
API; deleted afterwards, replaced by `ReviewFindingTests.cs`). Before:

| # | Reproduction on `7d7f2964` | Before |
| --- | --- | --- |
| 1 | row 7a, SAM 0.00005 ACH, native 0 | `WithinTolerance` / Pass (abs 1e-4) |
| 2 | one matching row 1a, default configuration | gate Pass, `RequiredListSupplied == false` |
| 3 | rows 1a (match) and `3b.ground` 0.2 vs 0.4 (`Gated == false`), both required | gate Fail |
| 4 | polygons A-B-C-D-E (area 10) and A-B-D-C-E (area 12), same five vertices | `IsBijection`, identity Verified |

Fixes (file:line at the validated content):

1. **Zeroed native object** (`Query/Evaluate.cs:165-172`, in `ValueState`). A native that reads exactly 0 against a
   non-zero S-VALUE is judged like a missing one: EXPORT-DEFECT, or UNSUPPORTED-MAPPING / MAPPING-DIFFERENCE when the
   contract lists it, via the existing `ListedOrDefect`. The numeric working is kept on the result but is not the
   verdict. `Query.Compare` (`Query/Compare.cs`) is untouched: the abs-or-rel mathematics, every tolerance
   (`QuantityCatalog`) and the S-ZERO branch (`Evaluate.cs:184-`) are unchanged. The basis is S07 section 6: "`abs`
   never turns a missing or zeroed native object into a pass."
2. **Coverage** (`Query/Evaluate.cs:351-405`, `Classes/QuantityResult.cs:75-` `RequiredComparison` and
   `CoverageStatus`, `Classes/GateConfiguration.cs:59`). `RequiredQuantityIds` is removed. A fixture PASS now needs
   `GateConfiguration.RequiredComparisons`: explicit quantity-on-object pairs (`RequiredComparison.Cross` builds
   all-object coverage). No list (`NotSupplied`), an empty or all-null list (`Empty`) and a missing pair (`Incomplete`) cap
   the status at NOT RUN; quantity presence alone is not coverage, a result without an object key cannot satisfy a pair,
   and a gate with no gating result is NOT RUN. Rank 1 to 3 outcomes are never hidden by the cap. A series is covered
   through `SeriesResult.GateResult`, keyed by the series object rather than by the decisive hour
   (`Query/Series.cs:12-29`). Results nobody asked for are listed (`GateReport.Unrequested`) and still count.
3. **Informational mismatches** (`Query/Evaluate.cs:67-80,106-115,194,277`, `Classes/QuantityResult.cs:58-61`).
   `IsInformational` is set only for a numeric out-of-tolerance verdict on a quantity with `Gated == false`
   (`3b.ground`, `3c.g`, `9.ground`: the S07 statements for F-OPQ ground/adiabatic, derived g and ground temperature).
   Such a result keeps its own verdict and numeric working, is listed in `GateReport.InformationalMismatches`, is
   counted in `CountByOutcome`, counts as coverage, and does not decide the status. It never reads as a pass
   (`IsPass == false`). Absent, zeroed, NaN, unreadable, invalid values, the proposed G cases (including the **G2
   DOCUMENTED-DEFAULT**, which still blocks), listed limitations, and the identity, alignment and interpretation
   prerequisites still gate on a non-gated quantity.
4. **Boundary identity** (`Query/Identity.cs:86,159-`). `SameBoundary` requires the vertices to follow each other in the
   same cyclic order, from any start vertex and in either winding, each within the tolerance. The tolerance, one-to-one
   matching, ambiguity rejection (`AmbiguousExpected/Native`), the winding report (`SameFacing`) and the carrier
   contradiction check in `AssessIdentity` are unchanged. Of the 120 vertex orders of the review polygon exactly the 10
   rotations and reversals match (SAM's `Polygon3D` builds no plane for 4 further orders; they are unusable and never
   match).

Intentional expectation changes to revision 1 tests (the 177 original cases remain; 128 of the 131 original test
methods are unchanged by name; three are changed):

* `SValue_Native_Zero_Against_A_Value_Is_Out_Of_Tolerance_Not_A_Match` is now `..._Is_A_Zeroed_Object_Not_A_Match`:
  the outcome is EXPORT-DEFECT instead of OUT-OF-TOLERANCE (finding 1; the status stays FAIL).
* `An_Empty_Result_Set_Is_Never_A_Pass` is now `..._Even_With_A_Coverage_List` and also checks a coverage list.
* `A_Required_Quantity_That_Was_Never_Evaluated_Makes_The_Gate_Not_Run` is replaced by the `CoverageTests` fixture
  (finding 2: quantity presence is not coverage).
* The rank tests in `GateTests` now state their coverage explicitly through a test helper (`Covered`); before, they
  enshrined a pass with no coverage list, which is the incomplete fixture PASS of finding 2. The exhaustive sweep also
  gained the 7a quantity and a sub-tolerance SAM value, plus the zeroed-native and informational invariants.

New regression cases (`ReviewFindingTests.cs`): zero/non-zero boundaries (7a 0.00005 vs 0, vs 0.00006, vs 1e-300; S-ZERO
unchanged; a zeroed native under every catalogued absolute tolerance; listed and accepted limitations; a zeroed hour in a
series), coverage (none, empty, null-only, complete, per-zone incomplete, same object different quantity, blank object
key, duplicates, every cap against FAIL/BLOCKED/NOT RUN, accepted limitation, unrequested results, `Cross`, series,
several objects), informational mismatches (the 1a + 3b.ground reproduction, the same mismatch gated, a real failure
beside it, each approved exclusion, the exact non-gated set, S-ZERO, every non-numeric defect still gating, listing,
prerequisites, G2 beside an informational mismatch, accepted G2, only-informational, many informational), and boundary
identity (the review polygons, rotations, reversals, all 120 orders, adjacent swaps, bow-tie versus square, triangles,
tolerance along the boundary, set matching, ambiguity, carrier contradiction, facing, degenerate polygons).

Decisions I made where S07 is silent (all fail-closed, for owner review):

* A zeroed native for a non-zero S-VALUE is an EXPORT-DEFECT (rank 1), as the nearest S07 wording ("missing or zeroed
  native object") and the S-VALUE row point to; S07 does not name the outcome.
* Informational is limited to the numeric mismatch. Whether an absent or unreadable diagnostic on a non-gated quantity
  should also be informational is not decided by S07 (OD-G2 territory) and stays gating.
* A gate with no gating result (only informational ones) is NOT RUN.
* `RequiredQuantityIds` was replaced rather than extended because the review showed quantity presence cannot express
  all-object coverage. The new API is unmerged, so nothing outside this PR depended on it.

## Files changed

Revision 1 (against `e84aeff6`):

* New: `benchmark/SAM.Analytical.InputEquivalence/` (csproj, `README.md`, `Classes/` 8 files, `Query/` 9 files).
* New: `benchmark/SAM.Analytical.InputEquivalence.Tests/` (csproj and 8 test files).
* Changed: `.github/workflows/build.yml` (+18 lines, one test step).
* New: this record.

Revision 2 (against `7d7f2964`, 11 files, all inside the two benchmark projects; `git diff --cached --name-only` outside
them: none):

* Library: `Classes/GateConfiguration.cs`, `Classes/QuantityDefinition.cs`, `Classes/QuantityResult.cs`,
  `Query/Evaluate.cs`, `Query/Identity.cs`, `Query/Series.cs`, `README.md`.
* Tests: `EvaluateStateTableTests.cs`, `GateTests.cs`, `NeverSilentlyPassTests.cs`, new `ReviewFindingTests.cs`.
* This record. `git diff --check` is clean. No existing source, project, solution, setting, default or public API file
  outside the new library is touched; `PROJECT_PROGRESS.md` is untouched.

## Tests, builds and validation

Local, this workstation, no TAS and no licence used (`dotnet 10.0.401`, projects target net8.0). Results below are on the
validated content `ab575448`:

| Check | Command | Result |
| --- | --- | --- |
| New library build | `dotnet build benchmark/SAM.Analytical.InputEquivalence/SAM.Analytical.InputEquivalence.csproj -c Debug` | 0 warnings, 0 errors |
| Core suite (Debug) | `dotnet test benchmark/SAM.Analytical.InputEquivalence.Tests/SAM.Analytical.InputEquivalence.Tests.csproj -c Debug` | **229 passed, 0 failed, 0 skipped** |
| Core suite (Release, CI form) | same, `-c Release -p:SAMVersion=2026.4.999.0` | **229 passed, 0 failed, 0 skipped** |
| Exhaustive PASS sweep | included in the 229 | 663,552 combinations evaluated, 7,344 pass, every pass satisfies the allow-list invariants (revision 1: 414,720 / 5,184) |
| Review-finding reproductions | four probes on `7d7f2964`, revision 1 API | 4 of 4 failed as the review said (table above); now covered by `ReviewFindingTests` and passing |
| Mutation check | each revision 2 fix removed in turn, then restored byte-identical | finding 1: 8 failures; finding 2: 11; finding 3: 11; finding 4: 5 (revision 1 mutations: 7, 1, 3, 1) |
| Existing TM59 tests (rebuilt with `--no-incremental`) | `dotnet build SAM_Tas/SAM.Analytical.Tas.TM59.Tests/... -c Release --no-incremental`, then `dotnet test ... --no-build` | **1249 passed, 0 failed, 0 skipped** |
| Existing benchmark producer tests | `dotnet test benchmark/SAM.Analytical.Tas.Benchmark.Tests/... -c Release --filter "TestCategory!=Simulation"` | **26 passed, 0 failed, 0 skipped** |
| Whitespace and scope | `git diff --cached --check`; `git diff --cached --name-only` | clean; only the two benchmark projects |
| SPDX and copyright header | literal check of every `.cs` in both projects | 0 missing |

GenOpt tests (432 in CI) were not run locally: nothing they exercise changed. Hosted results for each head are in the
PR description, kept separate from these local results. For the reviewed head `7d7f2964` the hosted `build` (run
39451347105) and `spdx` (run 39451346641) checks had completed SUCCESS.

Not done, by instruction: licensed TAS, native reader, fixtures, conversion, sizing, simulation.

## Regression assessment

* Revision 1 changed no tracked source, project or solution file other than the workflow step; revision 2 changes only
  the new library and its tests. `WorkflowSettings` defaults (`T3DRoute = GbXML`, `UseWidths = false`, `Sizing`,
  `Simulate`, `AddIZAMs`), SAM_UI defaults and the Both producer are unchanged because no file that defines them
  changed.
* No public API of any existing assembly changed. In the new, unmerged assembly the revision 2 API changes are:
  `GateConfiguration.RequiredQuantityIds` replaced by `RequiredComparisons`; `GateReport.RequiredListSupplied` and
  `MissingRequiredQuantityIds` replaced by `Coverage`, `MissingComparisons`, `Unrequested`; added `QuantityResult.IsInformational`,
  `GatesFixture`, `GateReport.InformationalMismatches`, `RequiredComparison`, `CoverageStatus`, `SeriesResult.GateResult`.
  Nothing outside the PR consumes them.
* The new project loads `SAM.Core.dll` and `SAM.Geometry.dll` from the sibling `SAM/build` like the benchmark projects.
  A stale `SAM/build` is the one environmental risk; CI builds it first.

## Unresolved issues and decisions

* Owner decisions still open (S08 plan section 8): fixtures (D-FIX); OD-G1 to OD-G3 approval; OD-W1 and OD-W2; the
  alignment probe and Sizing diff inside S08; the leap-year policy. This PR implements none of them as approved.
* New from revision 2, for the owner: (a) whether an absent or unreadable value on a non-gated diagnostic should also be informational (it still gates); (b) the coverage list is the caller's responsibility, so PR-B must build it from the frozen model for every object (`RequiredComparison.Cross`), and a list that omits an object is not detected by the gate; (c) the general OD-G2 definition of "gated" is still unapproved.
* Native questions N-1 to N-13 of the plan stay open for PR-B (host-surface area gross or net, layer order versus
  `reversed`, T-FRAME native uncertainties, hour-ending slot and yearly alignment, calendar day types, and so on).
* Recorded in passing, not fixed: `Convert/ToSAM/Material.cs:50-51,70-71` passes `externalLightReflectance` in both
  light slots; `Convert/ToSAM/InternalCondition.cs:220` guards on the wrong object; `Convert/ToSAM/DesignDay.cs:21-23`
  uses a 2018 reference year (leap-year shift); `SAMTBDDocument` creates a missing file and ignores the `open` result.

## Exact recommended next step

1. Independent re-review of revision 2 (the four findings) on the final head, then the owner's decision to mark the draft ready and merge it. The PR stays draft until then.
2. After merge: the known-SHA `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4` and guarded branch cleanup.
3. Before PR-B: the owner answers the S08 plan decision gate (SAM_Tas write scope for PR-B, fixtures, OD-G1..G3,
   OD-W1/W2, probes, leap year).
4. PR-B (licensed native reader, calibration, alignment probe): recommended as a **new** session on the licensed
   Windows workstation, **Claude Opus 5.5, High effort, Build mode** (Plan mode first if the decisions change), because
   reader independence, T-FRAME and layer-order calibration are still open and a wrong rule yields plausible passes.
   This is a recommendation; no setting is changed automatically.
