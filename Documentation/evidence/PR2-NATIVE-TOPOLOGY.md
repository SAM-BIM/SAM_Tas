# PR2 native topology - is the generated connector graph the TAS-native one?

> **Re-grounded on a real design, 2026-09-10.** Sections 1 to 6 were established on `f2.sam`, whose
> design the acceptance harness authored rather than read. Everything they measure about **TAS** -
> what a `ComponentGroup` is, that the grouping experiment changes nothing a simulation can see, the
> two native limits, and that positions are inert - is independent of which design was under test and
> stands unchanged. What does **not** stand is any claim that the *engineering* in those tables is the
> engineering Approved Document O Iteration 1a produces.
>
> The **directed topology acceptance on the real design** - every leg's upstream and downstream port
> compared with the analytical intent, 14 of 14 PASS - is section 7 of `PR2-REACCEPTANCE.md`, with the
> transcript in `PR2-reacc-topology.txt`. Read that for the engineering; read this for the API.


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

What *would* improve is the graphical layout, which is a separate and purely presentational concern -
and the closeout did it. See section 6.

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
(constant 150) - and none of them is a delivered airflow.

**Correction (closeout): a fan does answer an hourly series, and it settles delivered flow.** The sweep
above never probed a `Fan`. Done in the closeout, a fan answers exactly one of variables `0..24`:
variable 9, its Load in W, `Q x dp / eta` - 44 W on a 1000 Pa fresh air fan and 26.4 W on a 600 Pa
return fan at 44 l/s and `OverallEfficiency = 1`. Delivered flow is `Load x eta / dp`, and a control
that switches the fans off for hours 0..23 drives exactly those hours to 0. The earlier conclusion -
that continuous operation could only be grounded by exhausting the carriers - was wrong, and it hid a
real defect: the fans were running on a demand-driven function schedule, not the frozen constant 1.0.
Measured, fixed and re-accepted in `PR2-FAN-OPERATION.md`.

## 6. The layout - presentation only, measured before it was trusted

Manual inspection of the accepted document in the TAS GUI found the schematic unreadable. Measured
off the native document (`PR2-layout-before-fix.txt`): every native zone of a system at one point
(630, 80), every extract damper at one point, every transfer damper at another, all five branch
junctions at (0, 0), and no duct bend nodes - **45 overlapping box pairs and 225 duct passes through a
box that is neither of the duct's ends**. The network was right; the drawing was not.

**TAS exposes a presentation-only mechanism, with no `ComponentGroup` in it:**
`ISystemComponent.SetPosition(x, y)` / `SetDirection(tpdDirection)`, and `IDuct.AddNode(x, y)` for
bend nodes - which TAS accepts **only when the duct is created** (there is no node removal).

**It was proved presentation only before any production code used it.** Every one of the 42
components of an accepted document was moved to an arbitrary grid position and direction on a copy
(`PR2-layout-spread-control.txt`), and all 9 zones x 8760 hours of ZoneTemperature came back
**bit-identical - maximum difference exactly 0 K**, both systems `"Done"`.

**What PR2 now draws** (`Query.VentilationLayout`, `Modify.LayOutVentilationSystem`, called before
`Create.Ducts` on the explicit route only):

* every room its own row, in air-path order - supply-only, supply and extract, transfer-only,
  extract-only - by identity, never by name;
* each room's transfer dampers stacked beneath it; extract-only rooms in their own column past the
  transfer dampers, so a transfer into one drops straight down to it; extract dampers level with their
  room in the right-most column, clear of the occupied rooms;
* each branch junction beside the component it branches, facing the same way;
* every duct reaching a room, a duty carrier or a branch junction routed orthogonally, turning just
  before its target or - when it runs back - travelling in the free lane above its target's row;
* the template trunk (fans, supply damper, the template's own junctions) left where the template
  draws it; the replicated conversion untouched.

The first layout was inspected in the TAS GUI and approved (`PR2-layout-approved-first.txt`); the
refinement then moved the extract-only rooms into their own column (x 820 -> 1060) and the extract
dampers further right (x 1100 -> 1200), coordinates only.

**Result on that `f2.sam` run's final document** (`PR2-layout-after-fix.txt`). The accepted PR2 layout is
the real-design one in section 9 of `PR2-REACCEPTANCE.md` - 3 systems, 0 overlaps, 0 duct passes:

| | before | first layout (approved) | that run's refined layout |
| --- | --- | --- | --- |
| overlapping box pairs | 45 | 0 | **0** |
| duct passes through a non-endpoint box | 225 | 0 | **0** |
| canonical network (components, names, duties, edges; guids masked) | - | identical | identical |
| ZoneTemperature, 9 zones x 8760 h, keyed by ZoneLoad guid | - | identical, max \|dT\| 0 K | identical, max \|dT\| 0 K |
| acceptance items 4-15 | pass | pass | pass |

Native component guids cannot be compared across regenerated documents: two documents from identical
code and settings, with no layout at all, differ in every native guid and in nothing else. The
identity that must not change - PR1's derived guids and the room -> zone -> zone-load chain - is
item 15, which passes. Creation order is unchanged: the layout calls no `Add…`, and positions and bend
nodes are set on components and ducts the conversion creates exactly as before.
