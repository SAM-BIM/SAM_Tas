# PR2 licensed TAS checkpoint 1B - how TAS Systems actually represents ventilation airflow

Measured on the licensed machine through the harness at `C:\TasOut\inv`, one operation per process,
short absolute paths, TAS GUI closed. Two independent sources were used:

* **observation of a TAS-authored system** - `AirTemp.tpd`, authored in the TAS UI, not by SAM;
* **a differential COM probe** - a reference system built with deliberately asymmetric, non-round
  values, saved, reopened, and read back, so a value TAS refuses to keep shows up as a mismatch rather
  than as an assumption.

Every read is late-bound through `IDispatch`, because the typed `ISystemComponent` accessors throw on a
`SystemZone` (checkpoint 1). The property *names* come from the interop type library, so what is
dumped is what TAS declares, not a guess at what might be there.

---

## A. The carriers - answer per flow type

### SUPPLY

**Carrier:** `SystemZone.FlowRate` (total supply air) and `SystemZone.FreshAir` (outside-air portion),
both `SizedFlowVariable`, with `Type = tpdSizedVariableValue (1)` and the duty in `.Value`.

**Measured, exact round trip** through save and reopen:

```
Component[1] Type="SystemZone"   FlowRate Value=17 Type=1   FreshAir Value=17 Type=1
Component[3] Type="SystemZone"   FlowRate Value=31 Type=1   FreshAir Value=31 Type=1
```

The complete declared property set of a `SystemZone` is `DisplacementVent`, `Flags`, `FlowRate`,
`FreshAir`, `MinimumFlowFraction`, `PollutantSetpoint`, `RHSetpoint`, `TemperatureSetpoint` plus the
`ISystemComponent` identity members. **There are exactly two flow carriers and no extract or return
design flow on a zone** - that is enumerated, not assumed.

### EXTRACT

**Carrier:** an in-line **`Damper`** on the extract leg - `Damper.DesignFlowRate` (`SizedFlowVariable`,
`Type = tpdSizedVariableValue`) with `Damper.DesignFlowType = tpdFlowRateValue (1)`.

`IDamper` declares `DesignFlowRate` and `DesignFlowType`, the same pair a `Fan` uses. `IDuct` declares
**no GUID and no design-flow property at all** - its only flow members are `GetFlowRate(int hour)` /
`SetFlowRate(int hour, float)`, hourly *results*, which answer `Hour out of range` on an unsimulated
document. `IJunction` declares **no members whatsoever** - it is pure topology and carries nothing.

So a leg's duty cannot live on the duct or the junction; it lives on a flow-controlling component
placed in the leg. Measured, exact round trip:

```
Component[9]  Type="Damper"  DesignFlowType=1  DesignFlowRate Value=23 Type=1     <- extract-only room
```

### SUPPLY + EXTRACT ON THE SAME ROOM, WITH DIFFERENT VALUES

This is the case that decides whether TAS can carry PR1's legs independently. Authored as supply 31 and
extract 19 on one room, then read back after a save/reopen:

```
Component[3]  Type="SystemZone"  FlowRate Value=31 Type=1     <- the room's supply
Component[10] Type="Damper"      DesignFlowType=1  DesignFlowRate Value=31 Type=1  <- supply leg
Component[11] Type="Damper"      DesignFlowType=1  DesignFlowRate Value=19 Type=1  <- extract leg
```

**31 and 19 coexist, on the same room, independently.** Nothing was dropped, substituted or rebalanced.

### TRANSFER, AND BRANCHING TRANSFER

**Carrier:** the same `Damper.DesignFlowRate` mechanism, on a leg that runs room to room. A duct may be
run directly from one `SystemZone`'s outlet port to another's inlet port, and a `Junction` branches one
source into several legs, each with its own damper:

```
duct zone_Both -> junction: ok
duct junction -> damper_T1: ok      Component[13] Damper DesignFlowType=1 DesignFlowRate Value=11 Type=1
duct damper_T1 -> zone_T1: ok
duct junction -> damper_T2: ok      Component[14] Damper DesignFlowType=1 DesignFlowRate Value=7  Type=1
duct damper_T2 -> zone_T2: ok
```

**Both transfer legs (11 and 7) survive off one source, each with its own independent duty.**

### Fan duty is DERIVED, never authored

Measured on the **TAS-authored** system, to the last significant digit:

| system | `Fan.DesignFlowType` | fan `DesignFlowRate.Value` | equals |
| --- | --- | --- | --- |
| Fancoil - Zone 1 | `3` = `tpdFlowRateAllAttachedZonesFreshAir` | `37.9420166015625` | that zone's `FreshAir.Value` **exactly** |
| CAV - Zone 2 | `2` = `tpdFlowRateAllAttachedZonesFlowRate` | `3517.4283114346595` | that zone's `FlowRate.Value` **exactly** |

So a fan's duty follows the attached zones. PR2 must **assert** the reconciliation rather than write a
fan flow, exactly as the plan assumed.

### Verdict on representability

**TAS Systems CAN faithfully represent every explicit PR1 design leg - supply, extract, transfer and
branching transfer - independently and simultaneously.** This is NOT a blocker. The representation is:

* the room's own supply duty on the `SystemZone`;
* every other leg's duty on an in-line `Damper` with `DesignFlowType = tpdFlowRateValue`;
* topology by `Duct`, branching by `Junction`;
* fan duty derived from the attached zones and reconciled, not authored.

---

## B. Units - what is proven and what is not

**Stated answer: litres per second (l/s).** The basis, and the limit of it, precisely:

What is **measured**:

* TAS's own defaults on a newly created zone are `FlowRate SizeValue1 = 8` with `Method = tpdSizeFlowACH`
  (8 air changes per hour - dimensionless, so it fixes nothing) and `FreshAir SizeValue1 = 1.6` with
  `Method = tpdSizeFlowPerMeterSquared`. **1.6 l/s/m2 is the standard fresh-air rate**; 1.6 m3/s/m2 or
  1.6 m3/h/m2 are not defaults anyone ships.
* On the TAS-authored system, `FreshAir / FloorArea` is `1.11000` for **both** zones
  (`37.9420166015625 / 34.18199920654297` and `61.76816940307617 / 55.64699935913086`) - one consistent
  linear rate, and 1.11 l/s/m2 is 10 l/s/person at 9 m2/person.
* `Fan.Pressure` reads `1000` and `600`, i.e. Pa - the model is SI with litres for flow, which is the
  normal building-services convention.
* **There is no unit conversion anywhere on the SAM to TPD flow path.** `Modify.Update` writes
  `sizedFlowVariable.Value = sizedFlowValue.Value` and `Convert/ToSAM/SizedFlowValue.cs` reads it
  straight back. PR1 authors l/s and refuses in l/s, so whatever SAM states arrives verbatim.

What is **not** proven: a sizing round trip that would let TAS compute a flow from a known volume and
a stated ACH, so the unit could be read off TAS's own arithmetic. It was attempted three ways and all
three were blocked, each time with TAS's own diagnostic:

```
own reference system, no plant      -> "Plant room has no components"
own reference system, plus a boiler -> "Plant Room Has Errors"
TAS-authored system, TSD repointed  -> "Failed to open the TSD file"   (3 locations tried)
```

The authored model's TSD dates from 2025 and this TAS build will not open it; the reference systems
have no valid plant circuit, which TAS requires before it will size anything.

**Consequence for PR2, stated plainly.** The carriers are proven and the path is a pass-through, so if
the unit were ever shown to differ it is a single constant at one seam, not a design change. The
confirming measurement is deferred to step 7, where a production-converted TPD will exist and can be
simulated; **until it is made, PR2 must not silently rely on the unit** - the conversion writes PR1's
l/s verbatim, which is what the pass-through guarantees, and the acceptance record says so.

---

## C. `Simulate` returns a diagnostic STRING - and production throws it away

`ITPD.Simulate(int start, int end, int hWnd)` is declared as returning **`String`**, not `void`.
`SAM.Analytical.Tas.TPD/Modify/Simulate.cs` calls it as a statement and discards the result.

Observed return values, each a precise and different diagnosis of why nothing ran:

```
"Plant room has no components"
"Plant Room Has Errors"
"Failed to open the TSD file"
```

This is far better evidence than any structural check, and it is free. **The TPD side of
`SimulationEvidence` must capture and evaluate this string.**

---

## D. Room and load identity - settled on a real TSD

Measured by attaching the fixture-1 **no-IZAM** TSD (`C:\TasOut\po2\f1.tsd`) to an energy centre and
binding each zone load to its own zone with `SystemComponent.AddZoneLoad(zoneLoad)` - which is an
explicit object-to-object pairing with **no name involved**:

```
TSDDataCount = 1
TBDPath = "C:\TasOut\po2\f1.tbd"   TSDPath = "C:\TasOut\po2\f1.tsd"
ZoneLoadCount = 2

[1] load Name="Cell 1"
    ZoneLoad.GUID        = "{37FA3D5C-27E0-41D5-8825-366F8DBD66AD}"
    SystemZone.GUID      = "{09EDFD23-6F7A-41A0-A261-72C460997A6A}"
    EQUAL?               = False
    GetSystemZoneZoneLoad -> "{37FA3D5C-27E0-41D5-8825-366F8DBD66AD}"    <- exact, explicit
    GetZoneLoadForGuid    -> "Cell 1"                                    <- resolves
    GetComponentByGUID    -> "{09EDFD23-6F7A-41A0-A261-72C460997A6A}"    <- round trips
```

Findings, each of which the binding depends on:

1. **`SystemZone` component GUID and `ZoneLoad.GUID` are DISTINCT.** They are two different
   identifiers for two different objects. Neither substitutes for the other.
2. **`SystemComponent.AddZoneLoad(zoneLoad)` is the pairing point.** The binding must be captured
   there, at materialisation, not reconstructed afterwards.
3. **`SystemZone.GetSystemZoneZoneLoad()` returns exactly the bound load** - the zone-to-load direction,
   explicit and singular.
4. **`TSDData.GetZoneLoadForGuid(ZoneLoad.GUID)` resolves the load** - the load-side bridge to results.
5. **`System.GetComponentByGUID(SystemZone.GUID)` round-trips the component** - the component-side check.
6. The same is visible in the TAS-authored file, where `ZoneLoad "Zone 1" {2D4AE3A4-...}` maps to
   component `{53B52DF0-...}` - and where **both zones are named "System Zone 1"**, a live demonstration
   that display names alias in real TAS files.

**The chain PR2 must build, all identity, no names:**

```
Space.Guid -> SystemSpace.Guid -> SystemZone.GUID (component, late-bound read)
                               -> ZoneLoad.GUID  (via AddZoneLoad / GetSystemZoneZoneLoad)
                               -> results (via TSDData.GetZoneLoadForGuid)
```

Because the typed accessors throw, **the late-bound read must be encapsulated in one place** in
SAM_Tas rather than spread as `dynamic` reads through the conversion.

---

## E. Strengthened no-IZAM acceptance - a control that actually has something to remove

The earlier fixtures were **weak controls** and were reported as such: neither SAM model carries
ventilation systems, so even the control TBD answered `IZAM COUNT: 0`, and its `ticV` read `v=0 f=1` -
a TBD profile is *factor x value*, so the effective contribution was already zero. Nothing non-zero was
ever removed, and that evidence must not be read as full acceptance.

This is the missing control, built by **controlled mutation of a real TBD** - the fixture-2 control
`f2c.tbd`, with genuine inherited IZAMs and a genuinely non-zero mechanical `ticV` injected into the
file. No production code was changed to make it: the mutation is applied to a document, and the code
under test is then run against it unmodified.

**Seeded:** 3 IZAMs named `INHERITED_IZAM_1..3`, and `ticV value=5, factor=1, setbackValue=2.5` on
every one of the 36 internal conditions - effective contribution 5 each, 180 in total.

**Then the production cleanup, exactly as `WorkflowCalculator` calls it:**

```
Modify.RemoveIZAMs returned True
Modify.RemoveVentilationGains touched 36 internal condition(s)
```

**Before and after, read by walking TAS's own accessors:**

| measure | BEFORE | AFTER |
| --- | --- | --- |
| IZAM count | **3** | **0** |
| internal conditions | 36 | 36 |
| `ticV` effective non-zero count | **36** | **0** |
| `ticV` effective **sum** | **180** | **0** |
| `ticI` (infiltration) | 36 rows | **IDENTICAL, all 36** |
| aperture types (name, Cd, sheltered, profile, schedule) | 2 | **IDENTICAL** |

```
ticI IDENTICAL across all internal conditions
APERTURE TYPES IDENTICAL
  APERTURETYPE[0] name=Opening Cd0.411 F1 Cd=0.41059074 sheltered=0 profile(v=1 f=1 sched=<none>)
  APERTURETYPE[1] name=Opening Cd0.477 F1 Cd=0.477327 sheltered=0 profile(v=1 f=1 sched=<none>)
```

**This is the acceptance the weak fixtures could not give.** A real inherited IZAM was present and is
gone; a real non-zero mechanical ventilation contribution was present and is gone; and the infiltration
term in the adjacent profile slot and the natural-ventilation aperture types are byte-for-byte what
they were. Mechanical ventilation was removed and nothing else was.

Reproduce with:

```
inv.exe seed  C:\TasOut\po2\f2c.tbd C:\TasOut\po2\f2seed.tbd 3 5.0
inv.exe clean C:\TasOut\po2\f2seed.tbd
```


---

# Checkpoint 1B closure - the two remaining native semantics

## A. ABSOLUTE FLOW UNIT = **litres per second (l/s)** - PROVEN

Proven by **TAS's own arithmetic**, read back through the **exact COM property PR2 writes**
(`SystemZone.FlowRate.Value`), on a topologically valid current-TAS file.

**Method.** The shipped Part O template `MV.json` was converted by the **production**
`SAM.Analytical.Tas.TPD.Convert.ToTPD` against the real fixture-1 no-IZAM TSD, producing a TPD with a
complete plant circuit (19 plant components) and a live air system. Each zone was then bound to a real
`ZoneLoad` by explicit `AddZoneLoad`, so TAS itself reports the zone volume. Every zone's `FlowRate`
was set to **TAS's own ACH sizing rule at 8 ACH** and the document simulated, so TAS - not the harness -
computed the flow.

```
zone[1] bound -> load "Cell 1" guid "{37FA3D5C-...}" volume 200
zone[2] bound -> load "Cell 2" guid "{211ECCA2-...}" volume 200
      Method=2 (tpdSizeFlowACH)  SizeValue1=8  Type=2 (Size)

== AFTER ==
  zone[1] volume=200 sized=444.44444444444446 Type=3 (SizeDone)
      candidates  m3/s=0.44444  l/s=444.44444  m3/h=1600.00000   -> MATCHES l/s
  zone[2] volume=200 sized=444.44444444444446 Type=3 (SizeDone)
      candidates  m3/s=0.44444  l/s=444.44444  m3/h=1600.00000   -> MATCHES l/s
```

200 m3 x 8 ACH = 1600 m3/h = 0.4444 m3/s = **444.444 l/s**. TAS answered **444.44444444444446** and
marked the variable `tpdSizedVariableSizeDone`. The other two candidates are wrong by factors of 1000
and 3.6 respectively, so this is unambiguous.

**Independently corroborated on the same file**: with the template's own `PeakInternalCondition` method
TAS sized the same 200 m3 zones to `266.66668701171875`, i.e. 4.8 ACH - a sensible ventilation rate in
l/s and absurd in m3/s.

**And PR2's write mode was confirmed on the same file.** Forcing `Type = tpdSizedVariableValue` held the
authored values exactly through save, simulate and reopen:

```
      forced absolute: Value=128.00001525878906 Type=1
      forced absolute: Value=149.33334350585938 Type=1
  ...
  zone[1] sized=128.00001525878906 Type=1
  zone[2] sized=149.33334350585938 Type=1
```

> **ABSOLUTE FLOW UNIT = l/s**, for `SystemZone.FlowRate`, `SystemZone.FreshAir` and
> `Damper.DesignFlowRate` alike - they are all `SizedFlowVariable.Value`.

## B. `ITPD.Simulate` success semantics - NOT established, and the code says so

Four distinct returns have now been measured, **every one of them accompanying a run that produced
nothing**:

```
"Plant room has no components"     bare system, no plant side
"Plant Room Has Errors"            plant side present but not a valid circuit
"Failed to open the TSD file"      TSD unreadable (3 locations tried)
"Sizing Flow Failed"               production-converted MV template
```

**No successful run was obtained.** The production-converted MV template still answers
`"Sizing Flow Failed"` even when the zones are bound to real loads and their flows are absolute with
nothing left to size - so the remaining failure is elsewhere in the template's plant or fan
configuration, and chasing it is step 7 work, not a checkpoint question.

**Therefore what TAS returns on success is unknown**, and the earlier implementation -
`if (!string.IsNullOrWhiteSpace(returned)) fail` - was an assumption. It has been **removed**.

`SimulationDiagnostic` now classifies the return against the **measured** vocabulary:

* a measured failure -> **refuses**;
* silence -> decides nothing;
* anything unrecognised -> **preserved verbatim, recorded as a note, and decides nothing**.

`SimulationEvidence.NativeDiagnostic` and `.NativeDiagnosticKind` keep the raw answer as evidence
whatever it says. This is safe precisely because the string is **not** the success gate: the decisive
gate remains the complete `ZoneTemperature` reconciliation, which refuses a run that produced no
results regardless of what TAS said. When a successful return is finally observed,
`SimulationDiagnostic` is the one place that needs to learn it.

## C. Two production defects observed live during this closure

1. **Zone loads were not bound at all.** After the production conversion the zones read
   `GetSystemZoneZoneLoad -> <no object>` and `volume=NaN`, because `Query.ZoneLoads(TSDData,
   systemSpaces)` matches `SystemSpaceParameter.SpaceName` against `ZoneLoad.Name` - and the template's
   spaces are "System Zone 1" while the TSD's loads are "Cell 1"/"Cell 2". That is the N2 name-matching
   defect, reproduced end to end. Binding by explicit `AddZoneLoad` fixed it immediately.
2. **Duplicate display names in the production output.** Both converted zones are named
   `"System Zone 1"` - so any name-keyed result lookup would alias them onto one another.

## D. What the production conversion actually builds, and why it matters for step 7

```
Component[1..3]  Junction  "Junction Fresh Air", "Junction Exhaust Air", "Junction Return"
Component[4]     Fan       "Return Air Fan"   DesignFlowType=2  (AllAttachedZonesFlowRate)
Component[5]     Fan       "Fresh Air Fan"    DesignFlowType=2
Component[6]     ComponentGroup "MV 1_AHU1_AHU1"  Multiplicity=2  BaseComponentCount=2 -> 6
                   GroupJunction -> Damper 1 -> System Zone 1 -> GroupJunction     (x2 replicas)
Component[9,11]  Damper    DesignFlowType=4  (NearestZoneFlowRate)
Component[10,12] SystemZone  ZoneLoadCount=1
```

Three things follow directly:

* **The template already places one in-line `Damper` per room.** The damper is not something PR2 has to
  invent as a carrier - it is already there, currently deriving its duty from the nearest zone
  (`DesignFlowType=4`). PR2 sets it to `tpdFlowRateValue` with PR1's authored duty for the legs that
  carry one.
* **The multiplicity path is live** - `Multiplicity=2`, two base components replicated to six. This is
  exactly the branch the source-order hardening protects.
* **Fan duty is derived** (`DesignFlowType=2`), consistent with the authored-file measurement, so PR2
  reconciles it rather than writing it.
