# Why the Iteration 3 TPD will not size its plant room, and where its London design conditions come from

Licensed TAS, 2026-09-11, post-freeze follow-up to SAM#111. No production change in this repository: both
findings are properties of the **shipped `SystemEnergyCentre` template data in `SAM_Systems`**, faithfully
carried into TAS, and neither touches anything the Approved Document O Iteration 3 route reads.

Document under test: `Flat-It3B.tpd`, the Candidate B TPD the merged-state Iteration 3 acceptance produced.
Every variant below is a fresh copy of that same file, driven through `Interop.TPD` out of process.

---

## 1. `PlantRoom.Simulate` answers "Sizing Flow Failed"

Running the plant room by hand in TAS - which the Part O route never does, see
`Modify.SimulateSystems` - fails with:

```text
Sizing Flow Failed
Unknown design flow rate into component Multi Boiler 1, unable to calculate design pressure drop.
```

### What is actually in the document

| object | DesignDeltaT | DesignPressureDrop | rest |
| --- | --- | --- | --- |
| DHW group `DHW Circuit Group` | **0** | 19.5 | MinimumReturnTemp 0 |
| Heating group `Heating Circuit Group` | 0 | 19.5 | |
| Cooling group `Cooling Circuit Group` | 0 | 19.5 | |
| MultiBoiler `Heating Circuit Boiler` | 11 | 25 | Flags = 1 (LossesInSizing, **not** DHW), setpoint 71, duty 1700.68 sized on `Annual Design Condition` |
| MultiBoiler `Multi Boiler 1` | **0** | 25 | Flags = 1 (LossesInSizing, **not** DHW), setpoint 60, duty **0** sized on `London_LHR_DSY1 ANN CLG 0% CONDS DB=>GRad` |

`Multi Boiler 1` is the DHW circuit's boiler. From the document's own pipe list, that circuit is an **open**
loop, unlike the heating and cooling circuits which both close:

```text
DHW Junction In -> Multi Boiler 1 -> DHW Circuit Pump -> DHW Circuit Group -> DHW Junction Out
                (nothing returns DHW Junction Out to DHW Junction In)
```

### The measured matrix

`IPlantRoom.Simulate(1, 24, 0)`, one fresh copy of the same TPD per row:

| variant | DHW group dT | Multi Boiler 1 dT | Multi Boiler 1 IsDHW | answer |
| --- | --- | --- | --- | --- |
| as produced | 0 | 0 | false | **"Sizing Flow Failed"** |
| the DHW duty flag only | 0 | 0 | **true** | **"Sizing Flow Failed"** |
| DHW group dT = 11 | **11** | 0 | false | **"Done"** |
| DHW group dT = 5 | **5** | 0 | false | **"Done"** |
| Multi Boiler 1 dT = 11 | 0 | **11** | false | **"Done"** |

So:

- the missing property is a **non-zero design flow delta-T on the DHW circuit**, and TAS takes it from either
  the circuit group or the component;
- the DHW duty flag (`tpdMultiBoilerIsDHW`) is neither necessary nor sufficient;
- any non-zero value unblocks sizing, so the **magnitude is engineering data, not a switch**, and must not be
  invented.

### Where the value comes from, and why nothing is changed here

The conversion is faithful. `Convert.ToTPD(DisplayDomesticHotWaterSystemCollection, ...)` writes
`DesignDeltaT` from `DisplayDomesticHotWaterSystemCollection.DesignTemperatureDifference`, and
`Convert.ToTPD(DisplaySystemMultiBoiler, ...)` writes both `DesignDeltaT` and the DHW flag. The TPD carries
exactly what the SAM graph states, and the SAM graph carries exactly what the shipped template states -
**nothing is lost in materialisation or conversion**.

What the shipped templates state, read directly out of
`SAM_Systems/files/resources/Analytical/Systems/SystemEnergyCentre`:

| template | DHW group dT | Heating boiler dT | `Multi Boiler 1` dT |
| --- | --- | --- | --- |
| CAV, DISP, EOC, EOL, **MV**, NV, UV, VAV | **0** | 11 | 0 |
| MVRE (refreshed in `SAM_Systems` a571080) | **11** | 11 | 0 |
| Plantroom-Only (added in `SAM_Systems` 375e109) | **11** | 11 | 0 |

Before that refresh, MVRE also carried 0. So **11 K is SAM's own shipped DHW circuit design delta-T**: it is
what the two templates that have been round-tripped through a working TAS plant room carry, it matches the
71 °C / 60 °C flow-and-return the same templates state, and it is the value manual testing found to work. It
is not a number this investigation invented.

Applying it means editing the shipped `MV.json` (and the six other templates with the same gap), which is a
change to data every SAM workflow shares, not to the Part O route. That decision is deliberately left to the
template owner: this note records the exact one-property correction and its in-repo authority.

### It cannot affect Candidate B

`ISystem.Simulate(1, 168, 0)` on the same document, then `GetResultsData(hourly, max, 9 /* ZoneTemperature */)`,
with and without `DHW Circuit Group.DesignDeltaT = 11`:

| room | sum(ZT) over 168 h, as produced | sum(ZT) over 168 h, with the DHW delta-T |
| --- | --- | --- |
| Kitchen_7 | 2123.290275 | 2123.290275 |
| Bedroom 2_6 | 1757.493796 | 1757.493796 |
| Ensuite_8 | 2410.493202 | 2410.493202 |
| Ensuite_5 | 2368.264459 | 2368.264459 |
| Kitchen_4 | 2108.817826 | 2108.817826 |
| Bedroom 2_3 | 1759.963980 | 1759.963980 |
| Studio 1_0 | 1988.185446 | 1988.185446 |
| Bathroom_2 | 2423.627146 | 2423.627146 |

All three air systems answered `"Done"` in both runs and every room's hourly values are identical to six
decimals. The DHW correction is on a disjoint liquid circuit; the B0 parity configuration is untouched by it,
and applying it would require no annual rerun.

---

## 2. Both Leeds and London design conditions are present

### What TAS holds

| # | name | type | design days |
| --- | --- | --- | --- |
| 1 | `Leeds_TRY ANN HTG 100% CONDS DB` | `tpdHeatingDesignCondition` | **1** |
| 2 | `Leeds_TRY ANN CLG 0% CONDS DB=>GRad` | `tpdCoolingDesignCondition` | **1** |
| 3 | `London_LHR_DSY1 ANN HTG 100% CONDS DB` | `tpdSystemsDesignCondition` | **0** |
| 4 | `London_LHR_DSY1 ANN CLG 0% CONDS DB=>GRad` | `tpdSystemsDesignCondition` | **0** |
| 5 | `Annual Design Condition` | `tpdSystemsDesignCondition` | 0 |

### Where each comes from

- **Leeds** is TAS's own. `Convert.ToTPD` calls `EnergyCentre.AddTSDData(path_TSD, 1)` before anything else
  is written, and TAS derives the heating and cooling design conditions from the attached results file. They
  are the only two carrying an actual design day, and they name the canonical fixture's annual weather.
- **London** is shipped template data. `MV.json` declares them in its own
  `AnalyticalSystemsProperties.DesignConditions`, and `Convert.ToTPD` re-creates each by name through
  `Modify.Add(EnergyCentre, DesignCondition)`. They arrive as bare `tpdSystemsDesignCondition` entries with
  **no design day at all** - named hour windows carrying no weather. Every shipped template except
  `Plantroom-Only` declares the same London pair, and `MVRE` declares two more.

There is **no accumulation**. `Convert.ToTPD` deletes any existing TPD before writing, `Modify.Add` is an
upsert by name, and each run therefore produces exactly this list once. The duplication is between two
*sources* - the model's weather and the template's declared list - not between runs.

### What actually references them

| carrier | TAS type | design conditions attached |
| --- | --- | --- |
| `Extract Damper <room>` / `Transfer Damper <room>` - every duty carrier Part O writes (10 of them) | `tpdSizedVariableValue` | **0** |
| prototype `Fresh Air Fan` / `Return Air Fan` / `Damper 1` (9 of them) | `tpdSizedVariableSize` | **0** |
| `SystemZone.FlowRate` / `.FreshAir`, per room | `tpdSizedVariableValue` | 3 - both London conditions and `Annual Design Condition` |
| `Multi Boiler 1.Duty` | sized | 1 - `London_LHR_DSY1 ANN CLG 0% CONDS DB=>GRad` |

The zone flow and fresh-air variables are `Value`-typed, so TAS uses the stated number and the attached list
cannot change it - which is why the acceptance measured a zero design-airflow delta against the prepared
design's own terminals. **The two Leeds conditions are referenced by nothing in the document.** The only
variable in the whole TPD sized against a London condition is the unused DHW boiler's duty, and because that
condition carries no design day its duty reads 0 - the same template gap as finding 1, seen from the other end.

### Which conditions each stage uses

| stage | what it uses |
| --- | --- |
| annual thermal simulation (TBD/TSD) | the building's own weather - Leeds_TRY, `CIBSE Weather 2021.twd` for the canonical fixture. TPD design conditions play no part. |
| TAS Systems simulation (`ISystem.Simulate`, the Part O route) | the simulated hour range over the attached TSD. Every ventilation duty carrier is `Value`-typed with no design condition, so no design condition is consulted. |
| plant room sizing (`IPlantRoom.Simulate`, not used by Part O) | whatever each `SizedVariable` names. In this document that is `Annual Design Condition` for the heating boiler and the London cooling condition for the DHW boiler. |

### Conclusion

The London entries are **authoring residue in the shipped templates**, not a statement by the analytical
model, and not accumulated conversion state. They cannot reach any Candidate B number, which is why nothing
filters them here: removing them would mean either editing shipped template data or making
`Convert.ToTPD` drop design conditions a template deliberately declared - and the second would break the
template's own plant, whose duties reference them by name.

---

## How to reproduce

`Interop.TPD` from `references_buildonly`, a `net8.0-windows` console, and a copy of a Candidate B TPD:

```csharp
TPDDoc doc = new TPDDocClass();
doc.Open(path);                                   // -1 on success

EnergyCentre ec = doc.EnergyCentre;

for (int i = 1; i <= ec.GetDesignConditionCount(); i++)
{
    DesignConditionLoad dc = ec.GetDesignCondition(i);
    // dc.Name, dc.Type, dc.GetDesignDayCount()
}

PlantRoom pr = ec.GetPlantRoom(1);

dynamic g = pr.GetDHWGroup(1);
g.DesignDeltaT = 11.0;                            // the one property under test

string answer = pr.Simulate(1, 24, 0);            // "Sizing Flow Failed" / "Done"
```

Two environment notes, both measured:

- `IDHWGroup` does not expose `DesignDeltaT` or `DesignPressureDrop` on the typed interface - they are
  late-bound, which is why `Convert.ToTPD` uses `dynamic` for exactly those two.
- `GetComponentType()`, `GetInputPortCount()` and several typed accessors throw
  `COMException 0x80020003` on group and junction components while the `dynamic` `Name` read works. Guard
  every accessor, or one component ends the enumeration.
