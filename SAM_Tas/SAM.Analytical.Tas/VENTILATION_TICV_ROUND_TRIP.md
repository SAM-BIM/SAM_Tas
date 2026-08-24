# The ventilation `ticV` factor grew on every round trip

`TBD → FromTBD → new SAM model → ToGbXML → WorkflowgbXML → a NEW TBD`, repeated.

Reported from a real Grasshopper run of `A0-A1-A2.ghx`: the ventilation profile **values** were identical
across generations, but the **factor** kept climbing — a bedroom at `1.72 → 2.44 → 3.16` ACH, a studio at
`2.44 → 3.88 → 5.32`. Unbounded: each generation added the same constant again.

---

## What `ticV` is, and what carries it

A TAS internal condition states its ventilation as a `ticV` profile whose **factor** is the air change rate
and whose **values** are the schedule that rate is scaled by. `APERTURE_ROUND_TRIP_IDENTITY.md`'s sibling
concern, `VentilationAirflowMagnitudeTests`, already pinned the unit contract for that factor:

> A `ticV` rate carried on the ACH basis must round-trip unchanged, whatever the volume.

That invariant held — but only while nothing else was set alongside it.

---

## Root cause: one rate written to two places, then summed back in

`Query.CalculatedSupplyAirFlow(Space)` **sums** four bases:

| basis | unit | native TBD field? |
|---|---|---|
| `SupplyAirFlow` | m3/s | none |
| `SupplyAirFlowPerArea` | m3/s/m2 | none |
| `SupplyAirChangesPerHour` | ACH | `ticV.factor` |
| `SupplyAirFlowPerPerson` | m3/s/p | **`InternalGain.freshAirRate`** |

`Modify.UpdateInternalCondition` writes `SupplyAirFlowPerPerson` to `internalGain.freshAirRate` — TAS's own
per-person outside-air field — and then, 130 lines later, wrote the **whole summed total** (including that
same per-person rate) into `ticV.factor`. The occupants' fresh air was stated **twice**, in two different
TAS fields, on every export.

That alone is a double count. The growth comes from what the import does with it:

```
import:  SupplyAirChangesPerHour := ticV.factor          <-- the whole previous TOTAL, into ONE basis
export:  ticV.factor := sum(all four bases)              <-- which re-adds the per-person term
```

So the figure the export produced last time became one of the ingredients summed into the figure it produced
this time. **A feedback loop**, adding the per-person term once per generation, for ever.

### The arithmetic, measured on the reported model

`Bedroom 2_3`: volume 420 m3, area 105 m2, `AreaPerPerson` 10 → occupancy 10.5;
`freshAirRate` 8 l/s/p → `SupplyAirFlowPerPerson` 0.008 m3/s/p.

| | ACH basis | per-person term | total written |
|---|---|---|---|
| gen 1 | 1.72 ACH (0.2007 m3/s) | 0.084 m3/s = **0.72 ACH** | 2.44 ACH |
| gen 2 | 2.44 ACH | 0.72 ACH | 3.16 ACH |
| gen 3 | 3.16 ACH | 0.72 ACH | 3.88 ACH … |

`Studio 1_0` (occupancy 15, so a 1.44 ACH term) grew `2.44 → 3.88 → 5.32` — matching the report exactly.
The corridor, bathroom and ensuites state no `AreaPerPerson`, so their per-person term is `NaN`, they were
never inflated, and they sat at a stable 1.00 ACH throughout. That difference is itself the fingerprint.

---

## The fix

`Query.VentilationAirChangesPerHour(Space)` — the ACH that belongs in `ticV.factor`: **the volumetric supply
air TAS has no other field for, and only that.** It takes
`Analytical.Query.CalculatedSupplyAirFlow`'s total and subtracts the per-person summand, then converts over
the volume. `Modify.UpdateInternalCondition`'s `ticV` slot calls it instead of summing inline.

**Subtraction, not re-derivation.** The total still comes from the SAM query, so a basis added there later is
inherited rather than silently dropped; only the one term being removed is mirrored, and it mirrors that
query's own occupancy rule exactly so the subtraction is exact term-for-term.

### Why this is the right boundary

- `SupplyAirFlow` and `SupplyAirFlowPerArea` have **no** native TAS field, so they stay in the factor —
  dropping them would lose that ventilation outright rather than move it.
- `SupplyAirFlowPerPerson` **does** have one, written from the same parameter moments earlier. Excluding it
  from the factor is what stops it being stated twice.
- `Modify.UpdateInternalConditionTemplate` already did the right thing — it reads `SupplyAirChangesPerHour`
  verbatim rather than the summed total, and never compounded. The space path was the odd one out.

### What it makes true

`TBD → SAM → TBD` is now a **fixed point for both fields**:

```
ticV.factor  --import-->  SupplyAirChangesPerHour  --export-->  ticV.factor    (unchanged)
freshAirRate --import--> SupplyAirFlowPerPerson    --export-->  freshAirRate   (unchanged)
```

---

## Licensed acceptance

`A0.tbd` as the reported Grasshopper run produced it, driven through `Convert.ToSAM` → `TogbXML` →
`WorkflowCalculator.Calculate` (the engine `SAMAnalytical.WorkflowgbXML` calls), two generations:

| zone | volume | before: A0 → A1 → A2 | after: A0 → A1 → A2 |
|---|---|---|---|
| `Studio 1_0` | 300 | 2.44 → **3.88 → 5.32** | 2.44 → **2.44 → 2.44** |
| `Bedroom 2_3` | 420 | 1.72 → **2.44 → 3.16** | 1.72 → **1.72 → 1.72** |
| `Kitchen_4` | 300 | 1.72 → **2.44 → 3.16** | 1.72 → **1.72 → 1.72** |
| `Corridor_1` | 1464 | 1.00 → 1.00 → 1.00 | 1.00 → 1.00 → 1.00 |

`freshAirRate` holds at 8.0 l/s/p in every generation, before and after. The aperture-identity results from
PR #39 are unaffected: `40 considered / 40 rebound`, zero refusals, 3 aperture building elements, in both
generations.

---

## ⚠️ One behavioural implication, stated rather than buried

For a **round-tripped** model this is unambiguously a fix: it restores figures that were being inflated.

For a **SAM-authored** model exported for the first time, the occupants' fresh air now reaches TAS **only**
through `internalGain.freshAirRate`, where before it was also added into `ticV.factor`. If TAS simulates
`freshAirRate` itself, the previous behaviour was double-ventilating and this corrects it. If TAS treats
`freshAirRate` as sizing/reporting metadata only, then this reduces simulated ventilation for occupied
zones and the per-person air would need routing into the factor by some path that the import can tell apart
from the ACH basis — which the current single-slot import cannot.

**The round-trip growth is fixed either way**; that question only affects generation 1, and it is a TAS
semantics question rather than a SAM one. Worth confirming against a TAS reference before this is relied on
for a first-pass design simulation.

---

## Tests

`VentilationAirflowMagnitudeTests` (COM-free), alongside the existing magnitude contract:

- the per-person basis is excluded from the factor, and the excluded amount is exactly the per-person term;
- repeated round trips are a fixed point (the compounding, and its absence);
- a zone with no occupancy basis is completely unchanged by the correction;
- the area and flat bases are kept, because they have no other home;
- a condition stating no supply air at all still yields `NaN`, so the export's own "then use 1" fallback
  still fires rather than writing a zero.

Mutation check: removing the per-person exclusion fails the first two.
