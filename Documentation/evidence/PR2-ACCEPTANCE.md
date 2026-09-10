# PR2 licensed TAS acceptance - items 1 to 15, individually

Run on the licensed machine, `inv.exe route-source` then `inv.exe route-acceptance`, one COM document
cycle per process. Model `C:\TasOut\po2\f2.sam` (9 spaces), weather
`CIBSE Weather 2021.twd`, full-year TBD simulation (days 1..365), Systems simulation over hours
**0..8759**.

Every step is a **production** call:

```
SAM.Analytical.Tas.Create.NoIzamThermalSource            the thermal source
SAM.Analytical.Systems.Create.MechanicalVentilation      the PR1 graph
SAM.Analytical.Tas.TPD.Create.SystemVentilationRoute     the PR2 route
```

The document is then read back **independently**, late-bound through TAS's own accessors, to check that
what the route says is what TAS holds.

Full transcripts: `PR2-acceptance-source.log` (items 1-3), `PR2-acceptance-asdesigned.log` (the
as-designed refusal), `PR2-acceptance-simulable.log` (items 4-15). Harness archived as
`PR2-harness-Route.cs.txt`, `PR2-harness-Ident.cs.txt`, `PR2-harness-FanOut.cs.txt`.

---

## The design under test

Two physical air handling units. Per unit: a supply-only room, a room carrying **both** supply and
extract, a hall with no terminals at all reached only by transfer, and one or two extract-only rooms
fed by a **branching** transfer out of that hall. Duties are deliberately asymmetric and not round, so
a value that was substituted, rebalanced or conflated shows up as a mismatch rather than a
coincidence.

Materialised by PR1 as:

```
note: Air handling unit 'AHU Two' materialised as one air system serving 5 space(s).
note: Air handling unit 'AHU One' materialised as one air system serving 4 space(s).
note: 2 air system(s), 9 room(s) and 27 lineage binding(s) materialised and reconciled against the design.
```

## Two runs, and why both are here

**As designed** (`PR2-acceptance-asdesigned.log`). One unit states 44 l/s of supply against 34 l/s of
extract. The conversion reconciles **completely** and TAS then refuses to simulate it:

```
note: 12 duty-carrying damper(s) materialised - one per extract and transfer leg.
      Supply duties ride on the room's own zone and no damper was invented for them.
note: Conversion reconciled against the source graph: 2 air system(s), 9 room(s),
      4 supply / 5 extract / 7 transfer leg(s), every design flow matched on its native carrier.
refusal: TAS reported a failure: "Sizing Flow Failed".
IsComplete: False
```

That is the correct outcome twice over. An unbalanced MVHR design is a defect in the design;
**nothing in production rebalances, repairs or completes it**, and the route hands back no payload at
all - no bindings, no results, not even the document path.

**Simulable** (`PR2-acceptance-simulable.log`). The **harness** corrects the fixture so that the
duties satisfy per-zone air continuity, which is what TAS requires before it will simulate anything.
That correction is in the licensed harness and in no production path:

```
diagnostic variant: balanced MVHR from supply 44 / extract 34 l/s
diagnostic variant: transfer duties changed to exact per-zone continuity
```

Items 4-9 hold on **both** runs - the conversion note above states them on the as-designed one. Items
10-15 need a simulated document and are recorded on the simulable one.

---

## Items 1 to 3 - the no-IZAM thermal source

From `PR2-acceptance-source.log`, read by walking TAS's own `GetIZAM(i)` / `GetIC(i)` /
`GetProfile(ticV|ticI)` accessors, independently of the code under test.

| # | item | verdict | evidence |
| --- | --- | --- | --- |
| 1 | the thermal source carries no IZAM | **PASS** | IZAM count 0 on `acc.tbd` |
| 2 | the mechanical ventilation gain is zero on every internal condition | **PASS** | 36 internal condition(s), 0 with a non-zero `ticV` |
| 3 | the infiltration term is untouched | **PASS** | `ticI` present on 36 of 36 internal condition(s) |

The stronger control for these - a real TBD seeded with 3 inherited IZAMs and a genuinely non-zero
`ticV` summing to 180, cleaned to 0 with `ticI` and the aperture types byte-identical - is recorded in
`PR2-CHECKPOINT-1B.md` section E and is not repeated here.

## Items 4 to 15

| # | item | verdict | evidence |
| --- | --- | --- | --- |
| 4 | separate analytical AHUs remain separate TAS systems | **PASS** | 2 analytical unit(s), 2 native TAS system(s), 2 distinct system(s) named by the room bindings |
| 5 | every intended room belongs to the correct system | **PASS** | 9 room(s) bound, 9 materialised by PR1; 0 native zone(s) missing, 0 in the wrong system |
| 6 | supply / extract / both / transfer-only topology correct | **PASS** | found/designed - legs 4/4 supply, 5/5 extract, 7/7 transfer; rooms 2/2 supply-only, 3/3 extract-only, 2/2 both, 2/2 transfer-only |
| 7 | supply DesignFlowRate preserved | **PASS** | 4 supply room(s) checked against the native zone, 0 mismatch(es) - value, `FreshAir` and `Type = tpdSizedVariableValue` |
| 8 | extract DesignFlowRate preserved | **PASS** | 5 extract leg(s) checked against the native damper, 0 mismatch(es) - value, `DesignFlowRate.Type` and `DesignFlowType = tpdFlowRateValue` |
| 9 | transfer DesignFlowRate preserved | **PASS** | 7 transfer leg(s) checked against the native damper, 0 mismatch(es) |
| 10 | no duplicated ventilation / System Modelled semantics | **PASS** | 9 native zone(s), 0 still modelling their own ventilation (bit 4) or interzone flow (bit 2); flags seen `Flags=1` |
| 11 | fan + System Modelled configuration correct | **PASS** | every fan `DesignFlowType = tpdFlowRateAllAttachedZonesFlowRate`, value 44 - derived from the attached zones, never authored |
| 12 | complete ZoneTemperature | **PASS** | 9 of 9 room(s) complete over hours 0..8759, 8760 finite values each |
| 13 | API / native sampled agreement | **PASS** | 9 of 9 room(s) agreed value for value over 8760 hour(s), each read after a **raw late-bound** simulate of its own air system |
| 14 | duplicate display names do not alter identity | **PASS** | 23 native zone(s) and damper(s) renamed to one shared string; results still validate, and 9 of 9 room(s) returned **identical** values |
| 15 | native SystemZone / ZoneLoad identity chain proven end to end | **PASS** | 9/9 zone guids round-trip through `System.GetComponentByGUID`, 9/9 zone loads resolve through `GetSystemZoneZoneLoad` to the bound load, all identities distinct |

### Notes on three of them

**Item 10.** `Flags=1` is `tpdSystemZoneFlagDisplacementVent`, which is what the template's prototype
room states about how air is delivered - not a second ventilation model. The two bits that would
duplicate the ventilation, `ModelVentFlow` (4) and `ModelInterzoneFlow` (2), are clear on every zone.
The explicit route stops setting them from the template, because the graph already carries every
supply, extract and transfer leg explicitly.

**Item 13 is a genuinely independent read, and getting there mattered.** A first attempt read every
zone once, after both systems had been simulated, and reported "4 of 4 room(s) agreed" while listing
three rooms as "Failed to get the results series" - a pass with five of nine rooms silently outside
the denominator. The cause is native: **TAS replaces an air system's in-memory result surface when
`ISystem.Simulate` is called for the next system in the same document.** The check now simulates each
system with a raw late-bound call on a copy of the document and reads that system's own rooms straight
afterwards, and the denominator is every bound room.

The same native fact is why `Modify.SimulateSystems` captures each system's series before advancing.

**Item 14 mutates the document to prove the point.** Every native zone and damper in a copy is renamed
to one shared string, and the results are read again through the same bindings. They not only still
validate - they come back **identical**, room for room. A name-keyed lookup would have aliased them.

---

## What these runs do not show

* **One model, nine rooms, two units.** The scaling behaviour of the mapping and extraction is
  evidenced structurally at 100, 1,000 and 5,000 rooms in `PR2-BUILD.md` section 5, not by a licensed
  run at that size.
* **The branch-junction topology, the orphan skip and the nearest-zone switch are covered by this
  licensed acceptance and not by unit tests.** They are decisions about native TAS behaviour taken
  inside COM-touching code; the 840 COM-free tests cover the identity chain, the reconciliation, the
  duty carriers, the results gate and the route's fail-closed contract, and they cover the unchanged
  replicated conversion.
* **`SimulateTo = 1` is not a usable thermal source on this route.** A one-day TSD made the TBD
  workflow die in its post-simulation results step with
  `COMException: The RPC server is unavailable`. The full-year configuration is the one proven here.
