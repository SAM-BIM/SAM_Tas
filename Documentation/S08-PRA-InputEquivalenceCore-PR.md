# S08 PR-A: input-equivalence evaluation core

Task S08 of the SAM simulation roadmap (`SAM` repository, `documentation/SIMULATION_ROADMAP_Q4_2026.md`), first of the
planned PR-A / PR-B / PR-C slices. Contract: `documentation/SIMULATION_INPUT_MAPPING_S07.md` (SAM#216, merged
`68b60257`, closed out `c817804f`).

## Status

* **PR:** draft pull request into `sow/2026-Q4` (number and final head in the PR description). Not merged; merge needs
  explicit owner authorisation.
* **Branch:** `feature/s08-input-equivalence-core`, base `sow/2026-Q4` at `e84aeff6`.
* **Authority:** the owner authorised PR-A only, restricted to SAM_Tas ("I authorize PR-A only"). PR-B (native reader),
  PR-C (fixtures and acceptance) and every other repository are not started.
* **Validated content SHA:** `da0f5529545eca519782e9f4e310b7f44cf3a1de`. A record-only commit (this file) follows it;
  the local results below were obtained on that content. The final PR head and its GitHub checks are identified in the
  PR description.

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
   limitation, Pass) and `Query.Gate` (lowest rank decides; PASS is a strict allow-list; empty set or a missing
   required quantity is NOT RUN; per fixture).
4. **Independent expected values that need no TAS.** Floor area, net/gross area, closed-shell volume (`Shell.Volume`),
   T-FRAME percentage and width (writer rule), gain and people densities, infiltration flow, D-F2 ventilation state,
   requirement and delivery as separate quantities, opaque and glazing U with the approved films, design-day year
   day, EFLH.
5. **Schedules and mathematics.** `ExpandSchedule` (1, 24 or 8760 values; 8784 reported as an unsupported leap year,
   never truncated), hourly series evaluation, `FindHourShifts` for alignment diagnosis, reader-state propagation
   through products (`Query.Multiply`).
6. **Provisional OD-G1 to OD-G3 rules.** `GateRuleSet.Interim` (default) and `GateRuleSet.ProvisionalProposed`.
7. **Tests.** 177 licence-free tests, including an exhaustive sweep of 414,720 input combinations asserting the PASS
   allow-list invariants, and a check that the provisional rule set is never more lenient than the interim one.

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
* **"Gated" flags** in the catalogue follow the proposed OD-G2 definition. They only change which owner decision a
  rank 3 item names.
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

## Files changed

* New: `benchmark/SAM.Analytical.InputEquivalence/` (csproj, `README.md`, `Classes/` 8 files, `Query/` 9 files).
* New: `benchmark/SAM.Analytical.InputEquivalence.Tests/` (csproj and 8 test files).
* Changed: `.github/workflows/build.yml` (+18 lines, one test step).
* New: this record.
* No existing source, project, solution, setting, default or public API file is touched.

## Tests, builds and validation

Local, this workstation, no TAS and no licence used (`dotnet 10.0.401`, projects target net8.0):

| Check | Command | Result |
| --- | --- | --- |
| New library build | `dotnet build benchmark/SAM.Analytical.InputEquivalence/SAM.Analytical.InputEquivalence.csproj -c Debug` | 0 warnings, 0 errors |
| New tests (Debug) | `dotnet test benchmark/SAM.Analytical.InputEquivalence.Tests/SAM.Analytical.InputEquivalence.Tests.csproj -c Debug` | **177 passed, 0 failed, 0 skipped** |
| New tests (Release, CI form) | same, `-c Release -p:SAMVersion=2026.4.999.0` | **177 passed, 0 failed, 0 skipped** |
| Exhaustive PASS sweep | included in the 177 | 414,720 combinations evaluated, 5,184 pass, every pass satisfies the allow-list invariants |
| Mutation check (negative tests bite) | four deliberate source mutations, then restored byte-identical | accepted limitation to Pass: 7 failures; empty gate to Pass: 1; writer-only identity not capped: 3; relative denominator min instead of max: 1 |
| Existing TM59 tests (rebuilt with `--no-incremental`) | `dotnet build SAM_Tas/SAM.Analytical.Tas.TM59.Tests/... -c Release --no-incremental`, then `dotnet test ... --no-build` | **1249 passed, 0 failed, 0 skipped** |
| Existing benchmark producer tests | `dotnet test benchmark/SAM.Analytical.Tas.Benchmark.Tests/... -c Release --filter "TestCategory!=Simulation"` | **26 passed, 0 failed, 0 skipped** |
| SPDX and copyright header | literal check of all 25 new `.cs` files | 0 missing |

GenOpt tests (432 in CI) were not run locally: nothing they exercise changed. GitHub checks (build, test, spdx) on the
final head are reported in the PR description, separately from these local results.

Not done, by instruction: licensed TAS, native reader, fixtures, conversion, simulation.

## Regression assessment

* No tracked source, project or solution file changed; the only tracked file touched is the workflow, with an added
  step. `WorkflowSettings` defaults (`T3DRoute = GbXML`, `UseWidths = false`, `Sizing`, `Simulate`, `AddIZAMs`), SAM_UI
  defaults and the Both producer are unchanged because no file that defines them changed.
* No public API of any existing assembly changed; the new assembly is referenced by nothing but its test project.
* The new project loads `SAM.Core.dll` and `SAM.Geometry.dll` from the sibling `SAM/build` like the benchmark projects.
  A stale `SAM/build` is the one environmental risk; CI builds it first.

## Unresolved issues and decisions

* Owner decisions still open (S08 plan section 8): fixtures (D-FIX); OD-G1 to OD-G3 approval; OD-W1 and OD-W2; the
  alignment probe and Sizing diff inside S08; the leap-year policy. This PR implements none of them as approved.
* Native questions N-1 to N-13 of the plan stay open for PR-B (host-surface area gross or net, layer order versus
  `reversed`, T-FRAME native uncertainties, hour-ending slot and yearly alignment, calendar day types, and so on).
* Recorded in passing, not fixed: `Convert/ToSAM/Material.cs:50-51,70-71` passes `externalLightReflectance` in both
  light slots; `Convert/ToSAM/InternalCondition.cs:220` guards on the wrong object; `Convert/ToSAM/DesignDay.cs:21-23`
  uses a 2018 reference year (leap-year shift); `SAMTBDDocument` creates a missing file and ignores the `open` result.

## Exact recommended next step

1. Independent review of this draft and the owner's decision to mark it ready and merge it.
2. After merge: the known-SHA `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4` and guarded branch cleanup.
3. Before PR-B: the owner answers the S08 plan decision gate (SAM_Tas write scope for PR-B, fixtures, OD-G1..G3,
   OD-W1/W2, probes, leap year).
4. PR-B (licensed native reader, calibration, alignment probe): recommended as a **new** session on the licensed
   Windows workstation, **Claude Opus 5.5, High effort, Build mode** (Plan mode first if the decisions change), because
   reader independence, T-FRAME and layer-order calibration are still open and a wrong rule yields plausible passes.
   This is a recommendation; no setting is changed automatically.
