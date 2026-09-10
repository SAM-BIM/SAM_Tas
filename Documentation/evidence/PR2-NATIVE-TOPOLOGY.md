# PR2 native topology - is the generated connector graph the TAS-native one?

A manual inspection of a generated `.tpd` in the TAS GUI raised a specific finding: native TAS
templates represent a room terminal as a **`ComponentGroup`** holding the `SystemZone` and its
damper, connect the *group* into the main duct, and use **`Junction`** components for the branch
topology - whereas the generated document appeared to connect zones, dampers and fans individually.

The question that had to be settled was not what the schematic looks like. It was whether PR2 has

* **A** - correct native connectivity and only a poor graphical layout, or
* **B** - an incorrect native connector/group graph.

**The answer is A**, and this document is the native evidence, none of it inferred from a drawing.

Everything below was measured on the licensed machine through a read-only harness
(`C:\TasOut\rev`, modes `graph`, `net`, `groupexp`, `scan`, `schedprobe`, `oper`, `flowprobe`),
archived as `PR2-revharness-*.txt`. Every read is late-bound, and every graph edge comes from TAS's
own `IDuct.GetUpstreamComponent` / `GetUpstreamComponentPort` / `GetDownstreamComponent` /
`GetDownstreamComponentPort`, so what is reported is the connector graph and never the layout.

---

## 1. The finding is right about the idiom - and the idiom is TAS multiplicity

A TAS-authored file on this machine contains a system named, in its own author's words,
**"Fancoil with Grouped Mech Vent - VAV"**. Its native graph:

```
duct 7  [Fan "Supply Fan"]:1              ->  [ComponentGroup {9228D32B…} "Group 1"]:1
duct 8  [ComponentGroup {9228D32B…}]:1    ->  [Fan "Extract Fan"]:1

GROUP {9228D32B…} name="Group 1"  multiplicity=3  components=8  baseComponents=2
                                  ducts=9         baseDucts=3   inPorts=1 outPorts=1
      member GroupJunction {8E6277FB…}    <- the group's inlet boundary port
      member GroupJunction {A184AC41…}    <- the group's outlet boundary port
      member Damper {01C09831…} "Damper 1"   + SystemZone {BEBDCF15…} "System Zone 1"
      member Damper {0B30E3F2…} "Damper 1"   + SystemZone {6B408BD0…} "System Zone 1"
      member Damper {21D1CDDC…} "Damper 1"   + SystemZone {C17F143E…} "System Zone 1"
```

Three things are settled by this, and they matter in opposite directions.

**A `ComponentGroup` is a genuine connectivity participant, not a drawing box.** `IDuct` answers the
group itself as the duct's upstream or downstream component. The main duct terminates on the group,
and `GroupJunction` members carry the branch inside it. So the finding's description of the native
idiom is accurate.

**But `baseComponents = 2` with `multiplicity = 3` says what the idiom *is*.** The author drew **one**
damper and **one** zone and set multiplicity to 3; TAS replicated the pair. `components = 8` is
2 group junctions + 3 x (damper + zone); `ducts = 9` is 3 x (junction->damper, damper->zone,
zone->junction). This is the TAS **multiplicity** mechanism - explicitly out of scope for this route,
and `PR2-BUILD.md` section 2 already records why per-leg duties are impossible under it.

**And in that idiom the group becomes display-name authority.** All three replicated zones answer
`"System Zone 1"` and all three dampers `"Damper 1"`, because a replica inherits the base component's
name. Their guids differ, so identity survives - but the room names do not. That is precisely the
hazard this route is required to avoid.

A second authored file uses `ComponentGroup` in a completely different way: `multiplicity = 1`,
`baseComponents = components` (6 and 10 on its two systems), **`inPorts = 0 outPorts = 0`**, wrapping
an entire system - fans, coils, exchanger, junctions and the zone - with **zero ducts left at system
level**. Pure visual containment, no connectivity role at all.

So across the two authored references the construct appears as a replicator and as a wrapper. Neither
is a connectivity requirement, and neither is the shape the finding sketched (one `Junction` feeding
several separate `[Damper + Zone]` groups).

## 2. What PR2 actually generates - the canonical flow network

A `Junction`, a `GroupJunction` and a `ComponentGroup` all carry topology only: no duty, and no duct
of their own that a design flow rides on. Collapsing all three into transparent nodes leaves the
**canonical network** - the adjacency of the components that do carry a duty - which is the object to
compare, because two documents with equal canonical networks are the same ventilation system however
they are drawn or grouped.

For the four-room unit, with each edge's duty as TAS holds it:

```
Fresh Air Fan ── Damper 1 (44 l/s, tpdFlowRateAllAttachedZonesFlowRate)
                    │
                    ├── Kitchen_7  (31 l/s supply) ──┬── Transfer 12 ── Bedroom 2_6 ──┐
                    │                                └── Extract  19 ─────────────────┼──┐
                    └── Ensuite_5  (13 l/s supply) ───── Transfer 13 ── Bedroom 2_6 ──┘  │
                                                                             │            │
                                          Bedroom 2_6 (0 supply) ── Transfer 25 ──┐       │
                                                                                  │       │
                                                       Corridor_1 (0 supply) ── Extract 25 ┤
                                                                                           │
                                                                            Return Air Fan ┘
```

and it balances at every node: `31 + 13 = 44` supplied; `12 + 13 = 25` transferred into Bedroom 2_6;
`25` on to Corridor_1; `19 + 25 = 44` extracted. The five-room unit balances the same way -
`13 + 31 = 44` supplied, `13 + 12 = 25` into Kitchen_4, `10 + 15 = 25` out of it,
`19 + 10 + 15 = 44` extracted.

Read off the native graph rather than the picture:

* **five branch junctions in the five-room unit, four in the four-room unit**, and they do exactly
  what the finding expects a junction to do - the supply junction has one duct in and **two** out (to
  the unit's two supply rooms), the return junction **three** in and one out (three extract legs into
  the return fan);
* **every zone has exactly one input duct and exactly one output duct**, from
  `ISystemZone.GetSystemZoneInputDuct()` / `GetSystemZoneOutputDuct()`, 9 of 9;
* **no unintended cross-room edge**: every duty-node edge in the canonical network is a leg the design
  states, and there is none that is not;
* **no cross-unit edge at all**: the two systems share no component and no duct.

It is not star-connected, and the junction branching the finding asks for is already there. The full
dumps are `PR2-native-graph.txt` (raw ducts and ports) and `PR2-native-network.txt` (canonical).

## 3. The decisive experiment - grouping the terminals changes nothing a simulation can see

The native idiom was then **applied** to a copy of the working document: for every room,
`System.AddGroup([SystemZone, its terminal Damper], null)`, one group per room, no multiplicity.

`AddGroup(components, controllers)` is accepted only as a **typed `SystemComponent[]` with `null`
controllers**. Every other marshalling was refused - `object[]` with `object[0]`, with `null`, with
`Missing.Value`, a typed array with `object[0]` or `Controller[0]`, and the typed
`ISystem.AddGroup(…)` call - so the working form is measured, not assumed.

| measurement | result |
| --- | --- |
| groups created | **9 of 9**, each `multiplicity=1`, `baseComponents=1..2`, `inPorts=1 outPorts=1` |
| raw duct list | 17 edges removed, 35 added, 18 new `GroupJunction` components |
| **canonical network** | **byte-identical to ungrouped** - `diff` differs only on the filename line |
| duties and guids | `DUTY: 0 removed, 0 added, 23 unchanged` |
| simulation | both systems answered `"Done"`; 9 of 9 zones, 8760 values each |
| ZoneTemperature vs ungrouped | worst 4.00543212890625e-05 K over 9 rooms x 8760 hours |
| **control: the same document simulated twice** | worst **4.00543212890625e-05 K** |

The control is the row that settles it. Re-simulating the **same** document differs from itself by
exactly the same bound, to the last bit, so the grouped-versus-ungrouped difference is TAS's own
run-to-run noise floor and not a signal. (The harness read path is deterministic: the control's first
run against the archived earlier run differed by exactly 0.)

**Conclusion: at multiplicity 1 a `ComponentGroup` is a topology-neutral container.** It re-parents
ducts and inserts boundary junctions - so it is not literally a no-op on the raw duct list - but it
does not change the order of duty-carrying components along any flow path, any duty, any guid, or any
result.

## 4. Verdict, and why no production change follows

The generated connector graph is correct: **A**. Adding `ComponentGroup`s would change the schematic's
appearance and nothing else that TAS computes.

Three further reasons not to add them, beyond "it buys no correctness":

1. **A group has one inlet and one outlet port.** Kitchen_4 and Kitchen_7 each fan out to **two**
   downstream dampers - a transfer leg and an extract leg - so `[zone + its dampers]` is not
   expressible as a single one-in-one-out group for those rooms. The grouping would have to be
   inconsistent across rooms, which is worse for a reader than no grouping.
2. **The only authored multi-room grouped MV template reaches the arrangement through multiplicity**,
   which is ruled out for this route and which flattens each room's display name onto the base
   component's.
3. **Grouping must not become display-name authority.** Section 1 shows that in the authored idiom it
   does.

What *would* improve is the graphical layout, which is a separate and purely presentational concern:
`Create.Ducts` places components from PR1's own `SystemGeometry` and adds no layout of its own for
the junctions it introduces, which is why the schematic reads as a star even though the graph does
not. That is worth doing and is not a correctness item.

## 5. Two native limits found while establishing this, both recorded rather than worked around

**`ISystemComponent.GetSchedule()` throws through the typed interface.** Measured on one fan object,
four ways:

```
(a) raw __ComObject InvokeMember "GetSchedule"        -> OK  "Occupancy Schedule"  Type=3
(b) raw __ComObject ((dynamic)x).GetSchedule()        -> OK  "Occupancy Schedule"  Type=3
(c) ((TPD.SystemComponent)fan).GetSchedule()          -> COMException DISP_E_MEMBERNOTFOUND
(d) ((dynamic)fan).GetSchedule()                      -> OK  "Occupancy Schedule"  Type=3
(e) fan.GetType().InvokeMember("GetSchedule", …)      -> OK  "Occupancy Schedule"  Type=3
(f) ((TPD.ISystemComponent)fan).GetSchedule()         -> COMException DISP_E_MEMBERNOTFOUND
```

This is the same split checkpoint 1 recorded for `ISystemComponent.GUID` and `.Name`, now on a third
member, and it cost a refused acceptance run before it was found. `Modify.GroundVentilationFans` uses
path (d).

**There is no hourly flow result for a duct or a zone on this route.** On a document that simulated to
`"Done"`, `IDuct.GetFlowRate(hour)` answers `COMException: Hour out of range` for every hour swept in
`-1..8761`, and `IDuct.GetResultsData(Hourly, Max, variable, 1, 8760)` answers "Failed to get the
results series" for every variable `0..24`. A `SystemZone` exposes exactly five series - variables
9 (ZoneTemperature, 9.13..25.65 degC), 10 (constant 0), 11 (0..60), 12 (constant 16) and 13
(constant 150) - and none of them is a delivered airflow. So a per-hour "the design duty was actually
delivered" proof is **not available** through this API, which is why the continuous-operation claim in
`Modify.GroundVentilationFans` is grounded by exhausting the carriers that could hold a factor other
than 1.0 rather than by reading a delivered flow back.
