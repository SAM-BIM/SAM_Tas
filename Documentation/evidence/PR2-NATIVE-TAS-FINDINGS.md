# PR2 licensed TAS checkpoint - measured native findings

Measured on the licensed machine through the headless harness at `C:\TasOut\inv`, one operation per
process, short absolute paths, TAS GUI closed. Nothing below is inferred from a COM name or an old
comment; each row states the call that produced it.

Environment: `EDSL Tas for Engineers`, `C:\Program Files\Environmental Design Solutions Ltd\Tas`.
`TBD.Document`, `TPD.Document`, `TSD.Document`, `TAS3D.T3DDocument` all registered; `TPD.Document`
resolves to `C:\PROGRA~1\ENVIRO~1\Tas\TPD.exe`.

---

## 1. E1 reproduced: `Modify.Simulate` returns `true` having simulated nothing

`inv.exe make-empty-tpd` then, in a **separate process**, `inv.exe simulate-tpd ... 0 23`:

```
make-empty-tpd C:\TasOut\inv\e1.tpd
EnergyCentre after Create: present
PlantRoomCount: 0
exists after save: True, length 4068

simulate-tpd C:\TasOut\inv\e1.tpd 0..23
before: 4068 bytes, written 2026-09-09T16:31:50.0446362Z
UtcStarted 2026-09-09T16:31:59.2716063Z
PRODUCTION Modify.Simulate RETURNED: True
after: 4069 bytes, written 2026-09-09T16:31:59.9005420Z
error log C:\TasOut\inv\e1_error_log.txt: absent
```

The document holds **zero plant rooms, zero systems, zero zones**. Nothing was simulated and no result
of any kind exists, yet the production entry point answered `True`. The single byte of growth is
`Save()`, which is exactly why a changed file is not evidence for the in-place shape.

## 2. Which GUID does a `SystemZone` answer? (hard-stop item 4)

Measured on a zone created by `System.AddSystemZone()`:

| read | result |
| --- | --- |
| `ISystemComponent.GUID` through the **typed** interface | **throws** `InvalidCastException: OleAut reported a type mismatch` |
| `(systemZone as dynamic).GUID` - **what production does** | `{444DCD8E-18E7-44AF-8470-05676B3CAC7D}`, runtime type `System.String` |
| `ISystemComponent.Name` through the typed interface | **throws** the same |
| `GetComponentType()` | `4` for a system zone |

The same split appears on a `Fan`: the typed `.GUID` returned the string `"0"` while the dynamic read
returned `{27A758F0-973A-47D9-97CD-768CB0F7E46D}`.

**Consequence, and it is not optional.** On this interop the **late-bound read is the correct one**;
the typed `ISystemComponent` accessors are unreliable and in the zone's case throw outright. Any PR2
code that reads a component identity must go through the dynamic path that
`Convert/ToSAM/SystemSpaceResult.cs:20` already uses. Replacing it with a typed read - which would look
like a tidy-up - would break the route.

`ISystemZone` itself declares no `GUID` (confirmed against the type library), so the value above is the
**TPD component guid**, not a zone-load guid.

## 3. Zone loads and the load-side bridge

- `ISystemZone.GetSystemZoneZoneLoad()` exists and is the **explicit** zone-to-load accessor. On a zone
  with no TSD attached it returns `NULL`, and `GetZoneLoadCount()` on the zone answers
  `DISP_E_MEMBERNOTFOUND` - so a zone carries no loads until an energy centre has a TSD.
- `ITSDData.GetZoneLoadForGuid(string)` exists, so a zone load **is** addressable by guid.
- `IZoneLoad.GUID` exists as a property distinct from `ISystemComponent.GUID`.

Whether the two are equal for a materialised zone is **still open**: it cannot be measured on a zone
with no TSD, and needs the fixture-1 run that attaches a real no-IZAM TSD.

## 4. Flow carriers and units (hard-stop items 1, 2, 3)

A `SystemZone` has exactly **two** flow carriers - `FlowRate` and `FreshAir`, both `SizedFlowVariable`.
There is **no separate extract or return design-flow property on a zone**.

`IDuct` exposes **no GUID and no design-flow property**. Its only flow member is
`GetFlowRate(int hour)` / `SetFlowRate(int hour, float)`, an **hourly result** accessor - calling it on
an unsimulated document answers `COMException: Hour out of range`. So a duct is topology plus results,
never a design duty, and the plan's "do not invent a native duct GUID" is confirmed at the type level.

Measured defaults on a newly added zone:

```
FlowRate.Value  before: 0        after setting 42: 42
FlowRate.Method        : tpdSizeFlowACH
FlowRate.Type          : tpdSizedVariableSize
```

`tpdSizeFlowMethod` has **no absolute-flow member at all** - every member is a sizing rule
(`PerMeterSquared`, `PerMeterCubed`, `ACH`, `PeakPerson`, `PeakInternalCondition`, `DeltaT`,
`PeakPersonAndArea`, the four hourly variants, `PeakVentilation`, `HourlyVentilation`). An absolute
duty therefore travels through `tpdSizedVariable`:

```
tpdSizedVariableNone / tpdSizedVariableValue / tpdSizedVariableSize / tpdSizedVariableSizeDone
```

**The trap, and why PR1 does not fall into it.** `Modify.Update(SizedFlowVariable, SizedFlowValue,
EnergyCentre)` at `Modify/Update.cs:101-113` always writes `Value`, but writes `Type` and `Method`
**only when the SAM value is a `DesignConditionSizedFlowValue`**. A plain `SizedFlowValue` would leave
the TAS defaults in place - `Type = tpdSizedVariableSize`, `Method = tpdSizeFlowACH` - and the litres
per second would be ignored in favour of sizing the zone by air changes per hour.

PR1 does the right thing: `MechanicalVentilationAirSystem.cs:238-252` constructs a
`DesignConditionSizedFlowValue` with `SizingType.Value` **explicitly**, overriding the template's own
`"SizingType": "Sized"` from `MV.json`. So on the PR2 path `Type` lands as `tpdSizedVariableValue` and
`Value` is the authored duty. **This is measured, not assumed, and it is the reason the supply figure
survives.**

**Still open:** the absolute flow *unit* when `Type = tpdSizedVariableValue` - l/s, m3/s or m3/h. PR1
writes l/s. `MV.json`'s prototype carries `Value = 128.0` alongside `SizeValue1 = 8.0` ACH, which is
consistent with l/s for a room of that size and wildly inconsistent with m3/s, but that is
circumstantial. It needs the fixture-1 round trip against a TAS-authored reference.

`Fan.DesignFlowType` (`tpdFlowRateType`) is the derivation switch, and its members settle how a fan
gets its duty:

```
tpdFlowRateNone = 0
tpdFlowRateValue = 1                        (default on a new fan)
tpdFlowRateAllAttachedZonesFlowRate = 2
tpdFlowRateAllAttachedZonesFreshAir = 3
tpdFlowRateNearestZoneFlowRate = 4
tpdFlowRateNearestZoneFreshAir = 5
tpdFlowRateSized = 6
tpdFlowRateAllAttachedZonesSized = 7
```

So fan duty **is** derivable from the attached zones rather than authored, which is what the plan
assumed and what `MV.json` relies on.

## 5. Zone flags (hard-stop item 6)

Measured bit values, and the default state of a newly added zone:

```
tpdSystemZoneFlags.tpdSystemZoneFlagDisplacementVent   = 1
tpdSystemZoneFlags.tpdSystemZoneFlagModelInterzoneFlow = 2
tpdSystemZoneFlags.tpdSystemZoneFlagModelVentFlow      = 4

Flags on a new zone : 4      -> ModelVentFlow is ON by default
after OR-ing ModelVentFlow|ModelInterzoneFlow : 6
```

The clear-then-OR code in `Convert/ToTPD/SystemZone.cs:26-59` writes these bits correctly. What the
flags *mean* executably - that the building model's own ventilation and interzone flows are ignored -
is asserted by #111 and is **not** settled by this probe; it remains acceptance item 10.

## 6. Round trip by guid

`System.GetComponentByGUID(string)` exists and is the round-trip check the binding needs. It could not
be exercised against the zone in this probe because the **typed** guid read throws (item 2); it must be
driven from the dynamic read instead. That is a harness correction, not a TAS limitation.

## 7. COM lifetime

Two orphaned `TPD.exe` servers were left behind by a probe that threw before `Close()`, and they locked
the output file (`Device or resource busy`). The harness now closes in a `finally`. This is the known
one-operation-per-process constraint and is why every mode above is invoked in its own process.

---

## What remains to be measured

These need the fixture-1 run, which attaches a real no-IZAM TSD to the energy centre:

1. whether `ZoneLoad.GUID` equals the `SystemZone` component guid, and whether
   `TSDData.GetZoneLoadForGuid` resolves either;
2. the absolute flow unit when `Type = tpdSizedVariableValue`;
3. how an extract-only room and a room-to-room transfer are actually expressed once a TSD is present;
4. `ZoneTemperature` unit and API agreement.
