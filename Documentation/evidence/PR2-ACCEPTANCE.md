# PR2 licensed TAS acceptance - items 1 to 15, individually

Run on the licensed machine, `route-source` then `route-acceptance`, one COM document cycle per
process. Model `C:\TasOut\po2\f2.sam` (9 spaces), weather `CIBSE Weather 2021.twd`, full-year TBD
simulation (days 1..365), Systems simulation over hours **0..8759**.

**Closeout re-run (2026-09-10).** Every item below was re-established by the closeout on **one**
regenerated thermal source, with the frozen #111 operating configuration - the constant 1.0 yearly
schedule supplied through PR1's settings (`PR2-FAN-OPERATION.md`) - and the presentation-only layout
(`PR2-NATIVE-TOPOLOGY.md` section 6). Harness `PR2-closeout-harness-*.txt`; verdicts unchanged.

### Provenance - one thermal source, byte-identical everywhere

The original evidence cited `C:\TasOut\acc\acc.tbd` for items 1-3 while the accepted simulation used
`C:\TasOut\rev4\acc.tbd`. Those files were on the other licensed machine and could not be hashed from
this one, so their identity is **unverified and superseded**: the closeout regenerated the source once
and ran every check against byte-identical copies of it, hashing each copy before and after each run.

| artifact | SHA-256 |
| --- | --- |
| `C:\TasOut\pr2z\src\acc.tbd` - the no-IZAM thermal source | `054ED2376EC8687D859CF444B3A3DAE0032C9E840FA68276DAAF04BFA56C8C45` |
| `C:\TasOut\pr2z\src\acc.tsd` - its paired TSD | `44D8DB7B7F822D8820C17B9347DA86CA2E7BB47DC9CC20C8942580252CB65013` |

The copies in `before`, `final`, `final2`, `nosched`, `rename-n1..n3`, `final-layout` and
`final-layout2` hash to exactly these values before **and** after every run (`provenance.txt` in each
folder): the route reads its thermal source and never writes it. Items 1-3 and the accepted Systems
run therefore stand on the identical thermal source, by construction and by hash.

Every step is a **production** call:

```
SAM.Analytical.Tas.Create.NoIzamThermalSource            the thermal source
SAM.Analytical.Systems.Create.MechanicalVentilation      the PR1 graph
SAM.Analytical.Tas.TPD.Create.SystemVentilationRoute     the PR2 route
```

The document is then read back **independently**, late-bound through TAS's own accessors, to check that
what the route says is what TAS holds.

Full transcripts: `PR2-acceptance-source.log` (items 1-3), `PR2-acceptance-simulable.log` (items
4-15), and the three refusal runs that settle the diagnosis - `PR2-acceptance-asdesigned.log`,
`PR2-acceptance-balancedonly.log`, `PR2-acceptance-continuityonly.log`. Harness archived as
`PR2-harness-Route.cs.txt`, `PR2-harness-Ident.cs.txt`, `PR2-harness-FanOut.cs.txt`; the read-only
review harness as `PR2-revharness-*.txt`.

The native connector-graph evidence - including whether the absence of `ComponentGroup` is a defect -
is in `PR2-NATIVE-TOPOLOGY.md`.

---

## The design under test, with its duties persisted

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

### As designed

The fixture as first authored, in l/s (`SpaceAirMovement` values converted from m3/s):

| room | role | supply | extract | transfer out |
| --- | --- | --- | --- | --- |
| Studio 1_0 / Ensuite_5 | supply-only | 13 | - | 6.5 to the hall |
| Ensuite_8 / Kitchen_7 | both | 31 | 19 | 6.0 to the hall |
| Kitchen_4 / Bedroom 2_6 | transfer-only (hall) | - | - | 7.5 to extract-1, 5.0 to extract-2 |
| Bathroom_2 / Corridor_1 | extract-only | - | 15 | - |
| Bedroom 2_3 | extract-only (AHU Two only) | - | 10 | - |

### Corrected for simulability - the duties items 10 to 15 were actually produced from

**These are persisted here rather than left implied by a harness mode string**, so the run is
reproducible from this repository. The corrections are `balanced` (raise the last extract terminal of
an unbalanced unit) and `continuity` (restate every transfer duty), both in the licensed harness and
in **no production path**.

| room | AHU | role | supply | extract | transfer out |
| --- | --- | --- | --- | --- | --- |
| Studio 1_0 | Two | supply-only | 13 | - | **13** to Kitchen_4 |
| Ensuite_8 | Two | both | 31 | 19 | **12** to Kitchen_4 |
| Kitchen_4 | Two | transfer-only | - | - | **15** to Bathroom_2, **10** to Bedroom 2_3 |
| Bathroom_2 | Two | extract-only | - | 15 | - |
| Bedroom 2_3 | Two | extract-only | - | 10 | - |
| Ensuite_5 | One | supply-only | 13 | - | **13** to Bedroom 2_6 |
| Kitchen_7 | One | both | 31 | 19 | **12** to Bedroom 2_6 |
| Bedroom 2_6 | One | transfer-only | - | - | **25** to Corridor_1 |
| Corridor_1 | One | extract-only | - | **25** (raised from 15) | - |

Every room then balances exactly: `13 + 31 = 44` supplied per unit, `19 + 15 + 10 = 44` and
`19 + 25 = 44` extracted, and each room's inflow equals its outflow. The per-room arithmetic, read
back off the produced document rather than off this table, is `PR2-native-item6.txt`.

## Why TAS refused the design as written - the corrected diagnosis

**The earlier diagnosis in this document was wrong.** It attributed `"Sizing Flow Failed"` to one unit
stating 44 l/s of supply against 34 l/s of extract. That is not the cause, and three licensed runs
settle it. Each converts and reconciles **completely** first, so the refusal is TAS's judgement of the
design and never a conversion fault.

| run | AHU Two unit balance | AHU One unit balance | rooms failing per-zone continuity | TAS |
| --- | --- | --- | --- | --- |
| as designed | 44 / 44 - **already balanced** | 44 / 34 | **8 of 9** | refusal: `"Sizing Flow Failed"` |
| unit-balanced only | 44 / 44 | 44 / 44 - **both balanced** | **8 of 9** | refusal: `"Sizing Flow Failed"` |
| per-zone continuity only | 44 / 44 | 44 / 34 | **1 of 9** (Corridor_1, +10 l/s) | refusal: `"Sizing Flow Failed"` |
| both corrections | 44 / 44 | 44 / 44 | **0 of 9** | simulates; 9 of 9 rooms x 8760 hours |

Read across the rows:

* **unit-level balance is not sufficient.** AHU Two balanced at 44/44 in the *original* fixture and
  TAS refused anyway.
* **unit-level balance is not necessary either.** Balancing both units changed nothing: still 8 rooms
  discontinuous, still refused.
* **per-zone continuity is what TAS requires.** Restating the transfer duties alone took the failure
  count from 8 rooms to 1, and that last room - Corridor_1, taking 25 l/s of transfer while its
  extract terminal still stated 15 - refused on its own.
* the `balanced` step earns its place only as the **mechanism** that raises Corridor_1's extract
  terminal to the 25 l/s its own continuity requires. Unit balance is a *consequence* of per-zone
  continuity, not the condition.

That is the correct outcome twice over. A design whose rooms do not conserve air is a defect in the
design; **nothing in production rebalances, repairs or completes it**, and on a refusal the route hands
back no payload at all - no bindings, no results, not even the document path.

Items 4-9 hold on **every** run above, including the refused ones - the conversion note states them
before TAS is asked to simulate. Items 10-15 need a simulated document and are recorded on the
corrected one.

---

## Items 1 to 3 - the no-IZAM thermal source

From `PR2-acceptance-source.log`, read by walking TAS's own `GetIZAM(i)` / `GetIC(i)` /
`GetProfile(ticV|ticI)` accessors, independently of the code under test.

| # | item | verdict | evidence |
| --- | --- | --- | --- |
| 1 | the thermal source carries no IZAM | **PASS** | IZAM count 0 on `C:\TasOut\pr2z\src\acc.tbd` (SHA-256 `054ED237…`), and again on its byte-identical copies `before\acc.tbd` and `final\acc.tbd` |
| 2 | the mechanical ventilation gain is zero on every internal condition | **PASS** | 36 internal condition(s), 0 with a non-zero `ticV` - on the same three files |
| 3 | the infiltration term is untouched | **PASS** | `ticI` present and non-zero on 36 of 36 internal condition(s) (`value=0.15 factor=1` / `value=0 factor=0.15`) - `PR2-fanop-before-source-check.txt` |

The stronger control for these - a real TBD seeded with 3 inherited IZAMs and a genuinely non-zero
`ticV` summing to 180, cleaned to 0 with `ticI` and the aperture types byte-identical - is recorded in
`PR2-CHECKPOINT-1B.md` section E and is not repeated here.

## Items 4 to 15

| # | item | verdict | evidence |
| --- | --- | --- | --- |
| 4 | separate analytical AHUs remain separate TAS systems | **PASS** | 2 analytical unit(s), 2 native TAS system(s), 2 distinct system(s) named by the room bindings; and **0 components shared between the two systems** in the native graph |
| 5 | every intended room belongs to the correct system | **PASS** | 9 room(s) bound, 9 materialised by PR1; 0 native zone(s) missing, 0 in the wrong system |
| 6 | supply / extract / both / transfer-only topology correct | **PASS** | re-evaluated from the connector graph, not from counts - see below |
| 7 | supply DesignFlowRate preserved | **PASS** | 4 supply room(s) checked against the native zone, 0 mismatch(es) - value, `FreshAir` and `Type = tpdSizedVariableValue` |
| 8 | extract DesignFlowRate preserved | **PASS** | 5 extract leg(s) checked against the native damper, 0 mismatch(es) - value, `DesignFlowRate.Type` and `DesignFlowType = tpdFlowRateValue` |
| 9 | transfer DesignFlowRate preserved | **PASS** | 7 transfer leg(s) checked against the native damper, 0 mismatch(es) |
| 10 | no duplicated ventilation / System Modelled semantics | **PASS** | grounded on the source, not on the flags: (1) the accepted run's thermal source is the no-IZAM TBD hashed above - 0 IZAM; (2) 0 of 36 internal conditions carry mechanical `ticV`; (3) all ventilation is explicit in TAS Systems - 4 supply, 5 extract, 7 transfer legs on their own carriers (items 6-9). Zone flags corroborate only: 9 of 9 zones have `ModelVentFlow` (4) and `ModelInterzoneFlow` (2) clear |
| 11 | fan + System Modelled configuration correct | **PASS** | every fan `DesignFlowType = tpdFlowRateAllAttachedZonesFlowRate`, value 44 - derived, never authored; `HeatGainFactor = 0`; operation carrier the frozen constant 1.0 yearly schedule, operable 8760 of 8760 hours; native hourly fan Load shows all 4 fans at 44 l/s in 8760 of 8760 hours |
| 12 | complete ZoneTemperature | **PASS** | 9 of 9 room(s) complete over hours 0..8759, 8760 finite values each |
| 13 | API / native sampled agreement | **PASS** | 9 of 9 room(s) agreed value for value over 8760 hour(s), each read after a **raw late-bound** simulate of its own air system |
| 14 | duplicate display names do not alter identity | **PASS** | 23 native zone(s) and damper(s) renamed to one shared string; results still validate, and 9 of 9 room(s) returned **identical** values |
| 15 | native SystemZone / ZoneLoad identity chain proven end to end | **PASS** | 9/9 zone guids round-trip through `System.GetComponentByGUID`, 9/9 zone loads resolve through `GetSystemZoneZoneLoad` to the bound load, all identities distinct |

### Item 6, re-evaluated explicitly

The harness reports `found/designed - legs: 4/4 supply, 5/5 extract, 7/7 transfer. rooms: 2/2
supply-only, 3/3 extract-only, 2/2 both, 2/2 transfer-only.` **Matching counts are not why this item
passes.** Counts can match while the wrong room holds the wrong leg, so item 6 is decided from the
native connector graph instead, in `PR2-native-item6.txt`: every duty carrier is located by walking
TAS's own `IDuct` endpoint accessors, each room's role is *derived* from which carriers reach it, and
every edge in the graph must be a leg the design states or the unit's own backbone.

What that produces, per room, on the accepted document:

```
AHU Two   zones=5 dampers=8 fans=2 edges=17
  room           role           supply  transfIn  extract transfOut   net
  Bathroom_2     extract-only      0.0      15.0     15.0       0.0   0.0
  Bedroom 2_3    extract-only      0.0      10.0     10.0       0.0   0.0
  Ensuite_8      both             31.0       0.0     19.0      12.0   0.0
  Kitchen_4      transfer-only     0.0      25.0      0.0      25.0   0.0
  Studio 1_0     supply-only      13.0       0.0      0.0      13.0   0.0
  edges: 17 total = 16 design leg(s) + 1 backbone (fan -> supply damper) + 0 unexplained

AHU One   zones=4 dampers=6 fans=2 edges=13
  Bedroom 2_6    transfer-only     0.0      25.0      0.0      25.0   0.0
  Corridor_1     extract-only      0.0      25.0     25.0       0.0   0.0
  Ensuite_5      supply-only      13.0       0.0      0.0      13.0   0.0
  Kitchen_7      both             31.0       0.0     19.0      12.0   0.0
  edges: 13 total = 12 design leg(s) + 1 backbone (fan -> supply damper) + 0 unexplained

components shared between any two systems: 0
unexplained edges anywhere: 0
rooms failing per-zone air continuity: 0
```

Every role is the designed one, every duty is on its own carrier, **every edge is accounted for**, and
there is no cross-room connection that is not a designed transfer and no cross-unit connection at all.
The same analyser applied to the three refused variants is what produced the diagnosis table above -
so it is known to report a defect when there is one, rather than only ever agreeing.

The analyser also makes visible that AHU Two carries **two** dampers both named
`"Transfer Damper Kitchen_4"`, one at 15 l/s into Bathroom_2 and one at 10 l/s into Bedroom 2_3 - a
branching transfer out of one hall. They share a display name and are distinct objects with distinct
duties, which is what item 14 exists to prove is safe.

### Notes on four of them

**Item 10 - why nothing can be counted twice, and why the flags are not the proof.** Ventilation could
reach a zone twice only if the thermal source still carried it alongside the Systems network. It does
not, and that is established on the exact bytes the accepted run read:

1. **no IZAM** - interzone air movement is where the reference route states ventilation; the accepted
   source has none (item 1, hashed);
2. **zero mechanical `ticV`** - the building model's own mechanical ventilation gain is zero on every
   internal condition (item 2); infiltration and natural ventilation are left as they were (item 3),
   because they are not the MVHR's air;
3. **explicit Systems ventilation** - every supply, extract and transfer leg of the design is a native
   carrier with its own duty (items 6-9), so the MVHR's air exists in exactly one place.

The zone flags come **after** that, as structural corroboration - not as the proof, which would be
circular: a flag says what TAS would do with building-model ventilation, and the source has none.

**What the zone flags are and are not worth.** `Flags=1` is
`tpdSystemZoneFlagDisplacementVent`, which is what the template's prototype room states about how air
is delivered - not a second ventilation model. It is **inherited from the template and not decided by
this route**, which the route now says in a note per room rather than leaving a reader to infer it
from a flag value. The two bits that would duplicate the ventilation, `ModelVentFlow` (4) and
`ModelInterzoneFlow` (2), are clear on every zone, because the graph already carries every supply,
extract and transfer leg explicitly.

How much that suppression is worth was then **measured** rather than asserted: re-enabling both bits
on the accepted document and re-simulating moved `ZoneTemperature` by at most **4.0e-05 K** across 9
rooms x 8760 hours - exactly the bound two runs of the *same* document differ by, so it is the
solver's noise floor and not a signal. The reason is that this route's thermal source is the no-IZAM
TBD: `ticV` is zeroed on every internal condition and no interzone air movement is authored, so there
is nothing for those bits to re-apply. Clearing them is **structural correctness** - it makes double
counting unreachable if a future source did carry those terms - and **not** a demonstrated numerical
correction. The claim in item 10 is that the bits are clear, which is what was checked; it is not a
claim that leaving them set would have changed this run's results.

**Item 11, and the fan heat gain.** Fan duty is derived (`tpdFlowRateAllAttachedZonesFlowRate`), never
authored. Fan **heat gain** is now cleared by production: `Modify.GroundVentilationFans` sets
`HeatGainFactor = 0` and reads it back, where the shipped `MV.json` prototype states `1.0`. That is a
real thermal change and is recorded as one - on this fixture, zeroing it moved `ZoneTemperature` by up
to **2.80 K** in the worst hour (the transfer-fed corridor) and 0.13 K in the annual mean, which is
four orders of magnitude larger than the zone-flag effect above. SAM's own replicated routes are split
on the value: `TPD_CAV`, `TPD_EOL` and `TPD_EOC` zero it, `TPD_MV`, `TPD_VAV` and `TPD_MVRE` leave it
at 1. **This route deviates from `TPD_MV` deliberately.**

**Item 11, and operation - corrected in the closeout.** The earlier text here claimed the fans ran
continuously because their only carrier was a `tpdScheduleFunctionAllZonesLoad` schedule. **That was
wrong, and it hid a defect**: the reviewed configuration supplied no schedule, so every fan kept the
template's occupant-sensible function schedule - demand-driven - and production refused the frozen
constant 1.0 yearly schedule outright. A fan *does* answer an hourly series (variable 9, its Load), and
controls on it show the same function switching the fans off (0 hours with a heating load, 356 and
470 with a cooling load). Production now accepts only a yearly schedule operable in 8760 of 8760
hours, and the accepted run carries the frozen one: all four fans deliver 44 l/s in 8760 of 8760
hours. The full record is `PR2-FAN-OPERATION.md`.

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
  evidenced **structurally and COM-free** at 100, 1,000 and 5,000 rooms in `PR2-BUILD.md` section 5 -
  which bounds the conversion context's lookup count and nothing about native TAS. Nothing licensed
  has been run above 9 rooms and 2 units.
* **The branch-junction topology, the orphan skip and the nearest-zone switch are covered by this
  licensed acceptance and not by unit tests.** They are decisions about native TAS behaviour taken
  inside COM-touching code; the 840 COM-free tests cover the identity chain, the reconciliation, the
  duty carriers, the results gate and the route's fail-closed contract, and they cover the unchanged
  replicated conversion.
* **`SimulateTo = 1` is not a usable thermal source on this route.** A one-day TSD made the TBD
  workflow die in its post-simulation results step with
  `COMException: The RPC server is unavailable`. The full-year configuration is the one proven here.
* **The fan heat gain and zone flag magnitudes above are one fixture.** 2.80 K and 4.0e-05 K are
  measured on this 9-room design with this weather file. They establish the *order* of each effect,
  not a general figure.

---

## Artifact register (closeout, licensed machine)

Everything under `C:\TasOut` is generated and **not tracked in git**; the transcripts and harness copied
into this folder **are**. Every run folder holds a byte-identical copy of the one thermal source and a
`provenance.txt` with its hashes before and after the run.

| role | path | produced by | tracked |
| --- | --- | --- | --- |
| **FINAL ACCEPTED PR2 TPD** | `C:\TasOut\pr2z\final-layout2\acc.tpd` | `route-acceptance full balanced continuity const1`, schedule "PartO Constant 1.0", refined layout | no |
| FINAL's source TBD | `C:\TasOut\pr2z\final-layout2\acc.tbd` (= `src\acc.tbd`) | copied from `src` | no |
| FINAL's paired TSD | `C:\TasOut\pr2z\final-layout2\acc.tsd` (= `src\acc.tsd`) | copied from `src` | no |
| the one thermal source | `C:\TasOut\pr2z\src\acc.tbd`, `acc.tsd`, `acc-design.sam`, `acc-zones.txt` | `route-source … full` (production `Create.NoIzamThermalSource`) | no |
| manually inspected and approved layout (superseded only by the refinement) | `C:\TasOut\pr2z\final-layout\acc.tpd` | as FINAL, first layout | no |
| diagnostic: accepted network before the layout fix | `C:\TasOut\pr2z\rename-n2\acc.tpd` | as FINAL, no layout | no |
| diagnostic: reviewed configuration reproduced (no schedule, old code) | `C:\TasOut\pr2z\before\acc.tpd` | `route-acceptance full balanced continuity` at `0fefca76` | no |
| control: fan operation carriers | `C:\TasOut\pr2z\controls\ctl.{gap,funcload4,funcload8}.tpd`, `C:\TasOut\pr2z\carrier\car.*.tpd` | `fancontrol` on copies | no |
| control: refused by the fix (no schedule) | `C:\TasOut\pr2z\nosched\` | `route-acceptance`, fixed code | no |
| control: order sensitivity, refused | `C:\TasOut\pr2z\final\acc.tpd`, `C:\TasOut\pr2z\final2\acc.tpd` | const1, schedule "Part O Continuous Operation" | no |
| control: order sensitivity, passing | `C:\TasOut\pr2z\rename-n1\`, `rename-n3\` | const1, other schedule names | no |
| control: position is presentation only | `C:\TasOut\pr2z\layout\spread.tpd` | `spread` on a copy | no |
| harness copies (never the accepted document) | `acc.agree.tpd`, `acc.renamed.tpd`, `acc.fanprobe.tpd`, `acc.zt.tpd` beside each `acc.tpd` | items 13, 14, `fanprobe`, `zt` | no |
| fan operation evidence | `PR2-FAN-OPERATION.md`, `PR2-fanop-*.txt` | `fanprobe`, `fancontrol` | **yes** |
| item 10 / no-IZAM evidence | `PR2-fanop-before-source-check.txt`; section "Provenance" above | `source-check` on the exact bytes | **yes** |
| native connector / topology evidence | `PR2-NATIVE-TOPOLOGY.md`, `PR2-native-*.txt`, `PR2-layout-*.txt` | `graph`, `net`, `layout` | **yes** |
| acceptance logs | `PR2-acceptance-*.log`; per run `acceptance.console.txt` under `C:\TasOut\pr2z\*` | `route-acceptance` | logs **yes**, run folders no |
| error logs | `C:\TasOut\inv3\last-exception.log` (written only on a harness exception; none occurred in the accepted runs) | harness | no |
| harness | `PR2-closeout-harness-*.txt` (source at `C:\TasOut\inv3`) | - | **yes** |
