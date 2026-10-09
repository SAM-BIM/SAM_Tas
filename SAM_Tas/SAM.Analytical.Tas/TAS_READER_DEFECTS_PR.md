<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# SAM_Tas reader defects seen in PR7a: construction gaps and consumption units (evidence; no behaviour change)

Branch `fix/tas-reader-constructions-consumption` → base `sow/2026-Q4` (cut from `4abad64b`). Record date: 2026-10-09.

PR7a's record listed two out-of-scope SAM_Tas defects (`NATIVE_OPTIMISATION_PR7A_SPIKE.md`, "Out-of-scope SAM_Tas defects
seen"; PR7a entry of `PROJECT_PROGRESS.md`):

1. `Query.Constructions` stops at the first null, so constructions after a null gap of `Building.GetConstruction(i)` are
   missed.
2. `ConsumptionHeating` / `ConsumptionCooling` hold Wh under a kWh label.

This PR establishes both with licensed evidence. **Result: no product code changes.**

- Defect 1 does not exist: the gap was a misreading.
- Defect 2 is real, but it cannot be fixed at a single source without breaking three consumers that already
  compensate. It needs an owner decision.

## Current status

PR open, **not merged**: evidence and record only (no product file changed). Awaiting PR CI and **owner review**.

## 1. `Query.Constructions`: no defect

`SAM.Analytical.Tas/Query/Constructions.cs` reads `Building.GetConstruction(i)` from 0 until the first null. The TBD
interop exposes no construction count (`Building` has `AddConstruction`, `GetConstruction`, `GetConstructionByName`,
`RemoveConstruction`, `GetBECount` only), so "until the first null" is the only end marker.

Licensed evidence (this workstation, Tas TBD, **copies** of the Systems Demo TBD, SHA-256 `bf3c0c7d…`; local
`C:\TasOut\reader-defects\`):

| Case | `GetConstruction(i)` for i = 0..24 | `Query.Constructions` |
|---|---|---|
| Systems Demo as shipped (PR7a's `base` and `base-local`, same file) | 0–19 non-null (`Suncool Example` is 19), 20–24 null | 20, the last is `Suncool Example` |
| After `Building.RemoveConstruction(5)` (`Glazing`, unused) | 0–18 non-null, **compacted** (`Internal Ceiling` moved from 6 to 5), 19+ null | 19 |
| After SAM_Tas `Modify.RemoveConstructions(Raised Floor, Roof)` | 0–16 non-null, compacted | 17 |
| After save and re-open | the same, compacted | 17 |
| PR7a-2's TBDs after 8 constructions were written (`UpdateConstructions`) | 0–32 / 0–22 contiguous | — |

- **Where the "gap" came from.** PR7a's glazing probe counted non-null constructions in `GetConstruction(0..199)`:
  `non-null=20 listed=True`. That is 20 contiguous constructions followed by the end of the list, not a gap.
- PR7a's own inventory (a stop-at-first-null loop, like `Query.Constructions`) printed `construction[19] 'Suncool
  Example'`, so the reader did reach it.
- No TBD seen so far has a gap; removal compacts.
- **Decision: `Query.Constructions` is not changed.** A scan past nulls would need an arbitrary bound, since there is
  no count, and would change nothing observable. If a TBD with a gap is ever found, the fix is a bounded scan with a
  test on that recorded sequence.
- The PR7a and PR7a-2 records (merged) still say "null gaps"; this record supersedes that statement. PR7b's catalogue
  reads glazing through the building elements anyway.

## 2. `ConsumptionHeating` / `ConsumptionCooling` (and the peaks): Wh and W under kWh and kW labels

**Source:** `SAM.Analytical.Tas/Convert/ToSAM/AnalyticalModelSimulationResult.cs`. It sums the TSD building
`heatingProfile` / `coolingProfile` (8 760 hourly values) into `ConsumptionHeating` / `ConsumptionCooling`, and stores
their maxima as `PeakHeatingLoad` / `PeakCoolingLoad`. SAM's parameter labels
(`SAM.Analytical/Enums/Parameter/AnalyticalModelSimulationResultParameter.cs`) say `[kWh]` and `[kW]`.

**Evidence (licensed, the Systems Demo TSD copy, SHA-256 `40f169af…`):**

| Quantity | Value |
|---|---|
| SAM_Tas `ConsumptionHeating` | 15 227 840.66370964 |
| SAM_Tas `ConsumptionCooling` | 2 978 532.5259896517 |
| SAM_Tas `PeakHeatingLoad` | 38 727.40625 |
| TSD building `heatingProfile`, 8 760 values summed | 15 227 840.66370964 (the same) |
| Tas' own annual sums, `ZoneData.GetAnnualSumZoneResult(heatingLoad, 1, 365, Annual)` over all zones | 15 227 840.06 (heating), 2 978 532.46 (cooling); hourly sum / Tas sum = 1.00000004 |
| Largest zone annual peak `heatingLoad` (Tas) | 3 749.69 |
| SAM-generated Part O model (PR7a, SHA-256 `16812cfc…`): `ConsumptionCooling` | 2 523 751.8729965687 = PR7a's measured 2 523.75 kWh × 1000; Tas' annual cooling sum 2 523 751.91 (heating is off in that model: 0) |

- The hourly arrays are **power in W**:
  - Tas' zone loads are W, the same arrays SAM_Tas reads into space `Load` values in W;
  - the building peak is 38.7 kW, and the largest office zone 3.7 kW.
  A sum of hourly W over hours is **Wh**, and Tas' own annual sum returns that number without conversion.
- **Conclusive bound:** at 38 727 W all year the building could use at most 38 727 × 8 760 = 339 MWh. A value of
  15 227 840 can therefore only be Wh (15.2 MWh, about 393 full-load hours), never kWh (15.2 GWh).
- The same holds for the peaks: 38 727 is W, labelled kW.
- PR7a's measured heating demand (15 227.84 kWh) is this value ÷ 1000.

**Every consumer, in all SAM-BIM repos (`grep` of the parameter names and labels):**

| Consumer | What it does today | Shown today | After a source fix (÷ 1000) alone |
|---|---|---|---|
| SAM_Tas_Grasshopper `TasTSDAddBuildingResults` | divides consumption and peaks by 1000 | correct kWh / kW | **1000× too small** |
| SAM `Convert.ToDesignExplorer` (`ConsumptionHTG[kWh]`, per m², per m³, `PeakHTGLoad[kW]`) | divides by 1000 | correct | **1000× too small** |
| SAM_Tas benchmark `ToBenchmark` (B2 producer) | documents "the SAM_Tas convention (Wh, W)" and converts with `Kilo()` | correct | **1000× too small**; the offline goldens would fail |
| SAM_OpenStudio `Convert.ToSAM` (`SimulationResults.cs`) | writes **kWh** (`TotalAnnualHeating`) into the same parameter | correct for OpenStudio | unchanged; the two engines would then agree |
| SAM_Validation benchmark schema | carries the kWh value the producers convert | correct | unchanged |
| A user reading the parameter directly (SAM UI properties, Excel, a script) | sees the label `[kWh]` | **1000× too large for Tas results** | correct |
| Persisted `.json` / `.sam` results | Wh values stored under the parameter | — | keep their old values: a mixed model unless migrated |

## Owner decision needed

The instruction was to fix at the single source when the evidence is conclusive. The units are conclusive, but there is
**no single source to fix**: three consumers already divide by 1000. A source-only fix makes all of them report
values 1000× too small. Behaviour is therefore **not changed** here.

| Option | What changes | Pros | Cons |
|---|---|---|---|
| **A (recommended).** Fix at the source in a coordinated set | SAM_Tas source ÷ 1000 (consumption and peaks), the SAM_Tas benchmark `Kilo()` removed, the Grasshopper component's ÷ 1000 removed, SAM DesignExplorer's ÷ 1000 removed. One PR per repo, merged together. A result written after the fix gets a marker; for example the `Source` text or a version parameter. | The label is true. Tas and OpenStudio results agree. | Persisted Tas results stay in Wh. Consumers must tell old from new (marker), or a one-off migration divides old Tas results (only those with a Tas source and no marker). |
| B. Change the labels to Wh / W for Tas | Parameter labels only | No number changes. | The same parameter means kWh for OpenStudio and Wh for Tas: worse than today. |
| C. Leave as is and document | Nothing | No risk. | The label stays wrong for anyone reading the parameter directly. |

- **Persisted results** (any option that changes numbers): do not migrate silently. The proposal is a marker on new
  results and a reader rule: a Tas result without the marker is in Wh/W.
- No migration is done in this PR.

## Files changed

- `SAM_Tas/SAM.Analytical.Tas/TAS_READER_DEFECTS_PR.md` (this record). No product file.

## Validation

- Licensed probe (local, `C:\TasOut\reader-defects\probe`): built against the `sow/2026-Q4` build folders; run on copies.
  - `gap`: listing, removal, SAM_Tas `RemoveConstructions`, save and re-open.
  - `units`: SAM_Tas' converter, the hourly sums, Tas' own annual and peak sums.
- Every Tas process exited after each probe.
- The consumer list comes from a grep of every SAM-BIM repository for the parameter names and their labels.
- PR CI (`build`, `spdx`).

## Unresolved issues and risks

- The consumption and peak labels stay wrong for direct readers until the owner chooses an option.
- The native Optimisation measures (PR7a; PR7b, SAM_Tas#91, open) divide by 1000 in their own generated script, so they are in kWh and do not depend on this parameter.

## Next step

1. PR CI green; **owner review** and a choice of A, B or C.
2. If A: four small PRs (SAM_Tas source + benchmark, SAM_Tas_Grasshopper, SAM DesignExplorer, and the marker in SAM
   if it is a new parameter), merged together, then the closeouts.
