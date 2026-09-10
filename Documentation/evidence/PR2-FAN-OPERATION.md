# PR2 fan operation - the frozen continuous factor 1.0, measured, fixed and re-accepted

> **Re-proved on a real design, 2026-09-10.** The correction this document records - the frozen
> constant 1.0 supplied through PR1's settings, `HeatGainFactor = 0`, and the native hourly proof that
> the fan actually runs - was re-established from scratch on
> `SAM_zoningAM-CIBSEfutureZ1.sam`'s real Iteration 1a design: **6 of 6 fans across 3 air systems
> delivered their derived design duty in 8760 of 8760 hours**, `GetNumOperableHours = 8760`,
> `HeatGainFactor = 0` on all six. Transcript `PR2-reacc-fan-operation.txt`; verdict in section 8 of
> `PR2-REACCEPTANCE.md`.
>
> The mechanism, the measurements and the diagnosis below are unchanged - they are facts about TAS, not
> about the design that was under test. The **flow-sizing order sensitivity** of section 7
> (SAM-BIM/SAM #113) was **not encountered** on the real fixture: the canonical route completed on the
> first attempt, on all three systems, with no sizing refusal. It is neither fixed nor hidden.


SAM-BIM/SAM #111 freezes the PR2 parity operating configuration at **continuous operation factor
1.0** and **fan Heat Gain Factor 0**. The independent re-review found the fans exposing
`tpdScheduleFunctionAllZonesLoad` and challenged the claim that they run continuously. This is the
native answer. Every figure below was measured on licensed TAS; the harness is archived as
`PR2-closeout-harness-*.txt` and every transcript named here is in this folder.

**Verdict: the challenge was right. It was a real PR2 defect, and it is fixed.** The reviewed
configuration happened to run continuously on the acceptance fixture, but its operation carrier was
demand-driven, and production refused the frozen constant-1.0 schedule outright.

---

## 1. The schedule chain, traced

| stage | what it carries |
| --- | --- |
| #111 | "The Part O parity fixture supplies an 8760-hour constant 1.0 schedule", through PR1's generic API |
| PR1 `MechanicalVentilationSettings.Schedule` | any `ISchedule`; PR1 names it on every materialised `SystemFan` (`ScheduleName`) and adds a copy to the energy centre's `AnalyticalSystemsProperties`. **Null leaves the template's own fan schedule in place.** |
| SAM_Tas `Modify.Add(EnergyCentre, ISchedule)` | a `YearlySchedule` becomes `tpdScheduleYearly` via `SetYearlyValues(int[8760])` |
| SAM_Tas `Convert.ToTPD(SystemFan, …)` | attaches the energy-centre schedule to the native fan **by name** |
| TAS | a plant schedule is an **on/off table**; the fan runs in the hours it is on |

So factor 1.0 is natively **a yearly table on in all 8760 hours**, which TAS reports as
`GetNumOperableHours() == 8760`. (`GetYearlyValue(hour)` answers 0 in every hour on every schedule
type - it is not a usable read.)

## 2. What the reviewed configuration actually carried

The acceptance harness called PR1 with **no settings**, so every fan kept the shipped `MV.json`
template's `"Occupancy Schedule"`: `tpdScheduleFunction`, `tpdScheduleFunctionAllZonesLoad`,
`FunctionLoads = 1024` (occupant sensible). `GetNumOperableHours()` throws `"Not a Yearly Schedule"`
on it. And `Modify.GroundVentilationFans` **refused any yearly or hourly schedule** - so had the
frozen constant-1.0 schedule been supplied, production would have refused it.

## 3. Native hourly fan results exist - one series

A `Fan` answers exactly one of `GetResultsData(Hourly, Max, variable, 1, 8760)` variables `0..24`:
**variable 9, its Load in W = Q x dp / eta**. Every other variable answers "Failed to get the results
series". Delivered flow is therefore `Load x eta / dp`: 44 W on the 1000 Pa fresh air fan and 26.4 W
on the 600 Pa return fan are 44 l/s each, at `OverallEfficiency = 1`.

That variable 9 tracks **operation** rather than restating a design value is proved by the `gap`
control below.

## 4. The evidence, per fan

Expected period 8760 hours (0..8759) on every run; hours available 8760 on every fan read.

| run | carrier | fans | hours running (of 8760) | delivered flow min..max | transcript |
| --- | --- | --- | --- | --- | --- |
| **reviewed configuration** (no schedule supplied) | "Occupancy Schedule", function AllZonesLoad, occupant sensible | 4 | **8760** each | 44.0..44.0 l/s | `PR2-fanop-before-reviewed-config.txt` |
| control `gap` - yearly table off hours 0..23 | yearly, `GetNumOperableHours = 8736` | 4 | **8736** each - off exactly hours 0..23 | 0..44 l/s | `PR2-fanop-control-gap.txt` |
| control `funcload 4` - same function, heating load only | function AllZonesLoad, heating | 4 | **0** each | 0..0 l/s | `PR2-fanop-control-funcload4.txt` |
| control `funcload 8` - same function, cooling load only | function AllZonesLoad, cooling | 4 | **356** (AHU One), **470** (AHU Two) | 0..44 l/s | `PR2-fanop-control-funcload8.txt` |
| **the corrected configuration** - frozen constant 1.0 | yearly "PartO Constant 1.0", `GetNumOperableHours = 8760` | 4 | **8760** each | 44.0..44.0 l/s | `PR2-fanop-final-accepted.txt` |

Per fan on that `f2.sam` document - fan, schedule/function, expected duty, hours available,
hours running, delivered flow. **These four fans are not the accepted PR2 fans**: the accepted run has
six, across three MVHR systems, at 63 / 63 / 63 / 63 / 30 / 30 l/s - `PR2-reacc-fan-operation.txt`.

| system | fan | schedule | expected duty | available | running | min..max l/s |
| --- | --- | --- | --- | --- | --- | --- |
| AHU One | Return Air Fan | "PartO Constant 1.0", yearly, 8760 operable | 44 l/s (derived, `tpdFlowRateAllAttachedZonesFlowRate`) | 8760 | 8760 | 43.99999..43.99999 |
| AHU One | Fresh Air Fan | same | 44 l/s | 8760 | 8760 | 43.99999..44 |
| AHU Two | Return Air Fan | same | 44 l/s | 8760 | 8760 | 43.99999..43.99999 |
| AHU Two | Fresh Air Fan | same | 44 l/s | 8760 | 8760 | 44..44 |

## 5. Why the reviewed configuration is a defect even though it ran every hour

The occupant-sensible function switches the fan on whenever **any** attached zone carries occupant
load. On this dwelling some room is occupied in every hour, so it happened to run continuously. The
controls prove the carrier is **demand-driven**: the same schedule with heating as its load ran the
fans in 0 hours, with cooling in 356 and 470. On a dwelling or an AHU whose rooms are all unoccupied
for some hours, it would switch the fan off. Continuity owed to one fixture's occupancy is not the
frozen factor 1.0.

## 6. The correction - the smallest one

* `Query.ContinuousOperationRefusal` states the rule once: a fan's operation carrier must be a
  **yearly schedule operable in 8760 of 8760 hours**. A function schedule (demand-driven), an hourly
  day-type schedule (its operable hours cannot be read back), no schedule, or a yearly table off in
  any hour is refused with the reason. Pinned COM-free by `ContinuousOperationRefusalTests`.
* `Modify.GroundVentilationFans` reads the fan's schedule late-bound and applies it, and its note now
  states the carrier and its operable hours. `HeatGainFactor = 0` is kept, unchanged.
* **Nothing is authored by the route.** The frozen constant-1.0 schedule is supplied by the caller
  through PR1's generic settings, as #111 states; the route verifies it natively and refuses anything
  else. Nothing in SAM or SAM_Systems changed.

Measured fail-closed: the same run with no schedule supplied is refused at conversion, before TAS is
asked to simulate, with all four fans named - `C:\TasOut\pr2z\nosched\acceptance.console.txt`.

## 7. A separate native finding - TAS flow sizing is order-sensitive

Supplying the schedule changes PR1's deterministic identities (its energy-centre key hashes the
schedule's type and name), so it changes the order the **identical** network reaches TAS in. Under
the name `"Part O Continuous Operation"` AHU Two reproducibly answered `"Flow Sizing Failed"` (twice,
`C:\TasOut\pr2z\final` and `final2`), while three other names and the reviewed no-schedule order all
simulate. The canonical duty network of the refused and the passing documents is edge-for-edge
identical; only creation order differs. The route fails closed on it - no payload - so it is an
availability limit, not a correctness one. It is **not fixed here** (it would touch topology/creation
order, which this closeout forbids) and is tracked as **SAM-BIM/SAM#113** (filed there because
SAM-BIM/SAM_Tas has issues disabled).

The same finding also settled that the yearly carrier itself is sound: on the refused document AHU
One sized and ran on it for 8760 hours, and swapping AHU Two's carrier for no schedule, the template
function, an hourly all-on table or a function of type None did not rescue AHU Two's sizing.
