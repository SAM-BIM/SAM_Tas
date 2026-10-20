# Input-equivalence evaluation core (S08 PR-A)

The licence-free, COM-free core that decides whether a native model received what SAM intended, as defined by the
S07 mapping contract (`SAM` repository, `documentation/SIMULATION_INPUT_MAPPING_S07.md`, merged in SAM#216). It is
the evaluation half of the Tas independent native-input read-back (roadmap task S08). The native reader, the fixtures
and the licensed runs are **not** part of this project and are not in this pull request.

It references SAM core and SAM geometry only: no `SAM.Analytical.Tas`, no TAS COM interop, no SAM_UI, SAM_Validation or
SAM_OpenStudio. It builds and tests with the .NET CLI, and the OpenStudio read-back (S09) can lift it unchanged.

## What it contains

| Area | Types / entry points | S07 |
| --- | --- | --- |
| Reader results | `ReaderResult` (`Absent`, `Unreadable`, `NaN`, `Zero`, `Finite`, kept distinct), `ExpectedValue` (S-ZERO, S-UNSPEC, S-INVALID, S-VALUE) | section 6, 6.1 reader precondition |
| Numeric rule | `ToleranceRule`, `Query.Compare`: within when `d <= abs` or `d / max(\|n\|,\|s\|) <= rel`; zero and invalid inputs handled | section 5 |
| Tolerances as data | `QuantityCatalog` (every Tas-applicable row, locked by a test) | section 5 |
| Outcomes and gate | `EquivalenceOutcome`, `GateStatus` (rank 1-6), `Query.Evaluate`, `Query.Gate`, `GateReport`, `OwnerAcceptance` | section 6 |
| Prerequisites | identity (`IdentityStatus`), hour alignment (`AlignmentStatus`), model interpretation (`ModelInterpretation`) | sections 6, 7, D-F9 |
| Expected values | `Query.ExpectedFloorArea`, `ExpectedShellVolume`, `ExpectedNetArea`, `ExpectedFramePercentage`, `ExpectedFrameWidth`, `ExpectedGainDensity`, `ExpectedPeopleDensity`, `ExpectedInfiltrationFlow`, `ClassifyVentilation`, `ExpectedRequiredSupplyFlow`, `ExpectedDeliveredOutdoorAir`, `ExpectedOpaqueU`, `ExpectedGlazingU`, `ExpectedYearDay` | sections 3, 4, 5, D-F2 |
| Schedules | `ExpandSchedule` (1, 24 or 8760 values; 8784 is an unsupported leap year), `ExpectedEquivalentFullLoadHours`, `EvaluateSeries`, `FindHourShifts` | sections 5 row 6, 7 |
| Identity | `MatchPolygons` (vertex geometry only), `AssessIdentity` | S08 plan R5 |
| Provisional rules | `GateRuleSet.Interim` (default) and `GateRuleSet.ProvisionalProposed` for the undecided OD-G1 to OD-G3 cases | section 6.1 |

## Fail-closed by construction

* A result passes only for MATCH, WITHIN-TOLERANCE, a **verified** ACCEPTED-TRANSFORMATION, or a proven
  NOT-APPLICABLE; every other outcome is below rank 6. An owner acceptance moves a limitation from rank 3 to rank 5
  and never to a pass. An empty result set, or a required quantity that was never evaluated, is NOT RUN.
* `QuantityInput.Identity` defaults to `NotEstablished` and `Alignment` to `Unverified`. Writer-generated names, array
  order and GUIDs can only reach `WriterOnly`; only independent geometry evidence reaches `Verified`. Hourly
  quantities are capped at NOT RUN until the alignment is verified.
* The three cases S07 section 6.1 leaves undecided (G1, G1-NaN/G3, G2) are labelled "proposed" with the open owner
  decision named, and are at best BLOCKED under **both** rule sets. `ProvisionalProposed` only adds the proposed
  EXPORT-DEFECT rank where it is more severe. No owner acceptance lifts a proposed case. Neither rule set is approved.
* No tolerance is changed or invented. A tolerance criterion S07 leaves blank does not apply; a rule with none is
  rejected.

## Not included

The native reader, fixtures, licensed TAS runs, hour-alignment probe, widths-ON checks, production route or default
changes, the SAM_UI/SAM_Validation/SAM_OpenStudio changes, and any roadmap status update. Gas-gap resistance, glazing g,
heat-flow direction from geometry, and the selection of floor polygons from a frozen model are inputs to this core, not
derived by it, because S07 gives no rule for them yet.

## Build and test

```text
dotnet test benchmark/SAM.Analytical.InputEquivalence.Tests/SAM.Analytical.InputEquivalence.Tests.csproj
```

It needs the built `SAM.Core.dll` and `SAM.Geometry.dll` in the sibling `SAM/build` folder (the same HintPath
convention as the benchmark projects), and nothing else.
