# PR2 BUILD - the explicit Part O ventilation route through TAS Systems

What was built, what it rests on, and what was measured. Everything below that says "measured" was
measured on the licensed machine through the harness at `C:\TasOut\inv`, one operation per process,
TAS GUI closed. Everything that says "pinned" is a test in
`SAM_Tas/SAM.Analytical.Tas.TM59.Tests` that runs without a TAS licence.

---

## 1. The identity chain, and the measurement that settles it

The conversion binds a room's TAS zone load by **identity**. The measurement that makes that possible
was taken in this session, with `inv.exe zoneload-identity`:

```
zoneload-identity
  TBD C:\TasOut\po2\f1.tbd
  TSD C:\TasOut\po2\f1.tsd
  TBD zone[0] name="Cell 1" GUID={37FA3D5C-27E0-41D5-8825-366F8DBD66AD}
  TBD zone[1] name="Cell 2" GUID={211ECCA2-5F2C-4260-8B3C-4A14F19362C2}
  zone loads: 2
  load[1] name="Cell 1" GUID={37FA3D5C-27E0-41D5-8825-366F8DBD66AD}
        GetZoneLoadForGuid(own guid)  -> Cell 1
        GetZoneLoadForGuid(bare guid) -> Cell 1
  load[2] name="Cell 2" GUID={211ECCA2-5F2C-4260-8B3C-4A14F19362C2}
        GetZoneLoadForGuid(own guid)  -> Cell 2
        GetZoneLoadForGuid(bare guid) -> Cell 2

VERDICT: 2 of 2 ZoneLoad GUIDs are also TBD zone GUIDs (TBD zones: 2)
```

**A TSD zone load's `GUID` is the TBD zone's `GUID`.** SAM stamps that same guid onto the analytical
space as `SpaceParameter.ZoneGuid` during the TBD workflow (`Modify.UpdateIds`, from `zone.GUID`). So
the whole chain is identity, with no display name at any step:

```
Space.Guid -> SpaceParameter.ZoneGuid == ZoneLoad.GUID -> TSDData.GetZoneLoadForGuid -> AddZoneLoad
           -> SystemZone.GUID (a DIFFERENT identifier) -> System.GetComponentByGUID -> results
```

The defect this replaces compared `SystemSpaceParameter.SpaceName` with `ZoneLoad.Name`. On the
shipped `MV.json` that compares `"System Zone 1"` with `"Cell 1"`, so **every converted zone came out
with no zone load at all** - observed live in the previous session - and where it did match it would
alias two rooms that share a name, which a real TAS-authored file was observed doing.

---

## 2. Why the explicit route does not use `ComponentGroup` replication

`SetMultiplicity(n)` copies **one** base subgraph n times. PR1 gives every room of a unit the same
group index, so the base holds one zone and one of the template's dampers, and TAS replicates the
pair. Two consequences make a per-leg duty impossible:

* every replica carries the **same** damper properties, so there is nowhere to put room 2's extract
  duty that is not also room 1's;
* a room-to-room **transfer** runs between two replicas, and the ducts are built before replication,
  when only the base zone exists - so both ends collapse onto the same component and
  `Create.Ducts` skips the leg silently.

So the route materialises every room, every leg and every duty as an object of its own.

### 2a. `AddDuct` accepting a duct is not TAS accepting the graph

The first measurement of the explicit shape, `inv.exe fanout`, showed every duct being accepted:

```
== one DAMPER output port -> N zones (the supply-side fan-out) ==
  duct damperSupply[1] -> zone1[1]: ok
  duct damperSupply[1] -> zone2[1]: ok
  duct damperSupply[1] -> zone3[1]: ok

== N zones -> one JUNCTION input port (the extract-side fan-in) ==
  duct zone1[1] -> junctionExtract[1]: ok
  duct zone2[1] -> junctionExtract[1]: ok
  duct zone3[1] -> junctionExtract[1]: ok

== a DAMPER inserted in one room's extract leg, and a room-to-room transfer damper ==
  duct zone1[1] -> damperExtract[1]: ok
  duct damperExtract[1] -> junctionExtract[1]: ok
  duct zone1[1] -> damperTransfer[1]: ok
  duct damperTransfer[1] -> zone2[1]: ok

  damperSupply GetOutputDuctCount(1)  = 3
  junctionExtract GetInputDuctCount(1)= 4

VERDICT: damper output fan-out 3/3, junction input fan-in 3/3
```

**That measurement was necessary and not sufficient, and taking it as sufficient cost a run.** The
first licensed route on the real model converted and reconciled perfectly and then answered

```
refusal: TAS reported a failure: "AHU Two Has Errors"
```

`System.AddDuct` will attach several ducts to one ordinary component port and report success; TAS
rejects that air system when it is asked to simulate it. **A `Junction` is the native branch carrier**,
which is what the shipped template itself uses. `Create.Ducts` therefore inserts one native junction at
every connector end that carries more than one leg, on the explicit route only - the junction owns no
duty, and each extract or transfer leg keeps its own in-line damper. That is also precisely the shape
the plan asked for in words: *"Junction provides topology only; each transfer leg owns its own Damper
duty."*

Two further native facts came out of the same run, each with TAS's own diagnostic:

* **Orphaned prototype components.** Disabling replication exposes the group's unused prototype
  components as ordinary members of the air system. They participate in no PR1 connection, so
  materialising them creates a native component with no ducts at all, which TAS rejects. The explicit
  route converts only components that participate in the graph. Rooms are retained regardless, so a
  room with no connection is caught by the reconciliation rather than hidden here.
* **`tpdFlowRateNearestZoneFlowRate` is ambiguous behind a branch.** The template's supply damper used
  the nearest-zone rule because a replicated group put exactly one zone behind each copy. With one real
  damper in front of a branch junction, "nearest" has no answer and TAS says so. It is switched to
  `tpdFlowRateAllAttachedZonesFlowRate`: it remains a derived topology component, and the rooms' own
  `SystemZone` values remain the supply design-flow authority. PR2's own leg dampers are excluded from
  that switch by their duty-carrier identity, so an absolute duty is never turned into a derived one.

**Disabling replication is a deviation from the frozen plan and is recorded as one.** The plan did not
say to disable it; it is the only shape in which room-to-room transfers, branching transfers and
per-leg duties can all be expressed at once. It is confined to the explicit route: without a
`SystemVentilationConversionContext`, `Convert.ToTPD` groups and replicates exactly as before, and the
838 COM-free tests cover that unchanged path.

### 2b. TAS replaces the result surface between systems

Measured: calling `ISystem.Simulate` for the next air system in the same document **replaces the
previous system's in-memory result surface**. Reading every zone after the loop therefore returns
results for the last system only - on the two-unit model that silently produced four complete series
out of nine, with the other five answering "Failed to get the results series".

`Modify.SimulateSystems` now captures each system's zone temperatures *before* it advances, through a
per-system overload of `Convert.ToSAM_SystemZoneTemperatureResults` so the capture stays linear in the
rooms of that unit rather than re-indexing the whole document per system. The arrays it keeps are
ordinary managed values and remain valid after the next native call.

### 2c. TAS will not simulate an unbalanced design, and production does not repair one

The acceptance design as first written stated 44 l/s of supply against 34 l/s of extract on one unit.
It converts and reconciles **completely** - and then:

```
note: Conversion reconciled against the source graph: 2 air system(s), 9 room(s),
      4 supply / 5 extract / 7 transfer leg(s), every design flow matched on its native carrier.
refusal: TAS reported a failure: "Sizing Flow Failed".
IsComplete: False
```

That is the correct outcome twice over: an unbalanced MVHR design is a defect in the design, and the
route refuses rather than producing a payload. **Nothing in production rebalances, repairs or completes
a design** - PR1 refuses to, and PR2 does not either. The acceptance fixture is corrected in the
harness so that items 10-15, which need a simulated document, have one to work on; that correction is
in the licensed harness and in no production path. See `PR2-ACCEPTANCE.md` for both runs.

---

## 3. Where each duty lives

| leg | native carrier | how |
| --- | --- | --- |
| supply | the room's own `SystemZone` | `FlowRate.Value` and `FreshAir.Value` in l/s, `Type = tpdSizedVariableValue` |
| extract | an in-line `Damper` PR2 materialises for the leg | `DesignFlowRate.Value` l/s, `DesignFlowRate.Type = tpdSizedVariableValue`, `DesignFlowType = tpdFlowRateValue` |
| transfer | one `Damper` per leg, likewise | as extract; a branching transfer is two legs, two dampers, two duties |
| fan | derived, never authored | `DesignFlowType` reconciled and reported, never written |

`Type = tpdSizedVariableValue` is not optional. Left alone, TAS sizes a zone by its own rule - measured
at `444.44444444444446` l/s for a 200 m3 zone at 8 ACH - and the litres per second are discarded.

PR1's graph carries no extract or transfer damper: its extract legs run straight into the unit's
junction and its transfer legs room to room. So PR2 materialises one per leg, into a **working copy**
of the graph, with a guid derived from the PR1 connection it belongs to. PR1's graph is never touched,
and that is pinned by a byte-for-byte JSON comparison.

---

## 4. The gate

A run is accepted only when **both** hold:

1. the conversion **reconciles** against the source graph - every unit its own TAS system with no
   collapse and no extra, every room bound once into the right system to the right zone and the right
   zone load, every leg present exactly once with its design airflow read back off its own native
   carrier;
2. every room returns a **complete finite `ZoneTemperature` series** for the requested period, read
   off the exact native zone and zone load its own binding names.

TAS answering `"Done"` is recorded as positive evidence and decides nothing on its own. A saved file,
a returned string and a finished call are all things a failed run also produces.

---

## 5. Scaling

Structural rather than a stopwatch race. `SystemVentilationConversionContext` counts every indexed
lookup it makes, so "one probe per room" is asserted directly:

```
100 rooms / 5 air systems / 160 legs:    220 indexed lookups,  178 ms (intent 35,  carriers 143,  reconcile 0)
1000 rooms / 50 air systems / 1600 legs: 2200 indexed lookups, 2259 ms (intent 365, carriers 1890, reconcile 4)
5000 rooms / 250 air systems / 8000 legs: 11000 indexed lookups, 7364 ms (intent 914, carriers 6428, reconcile 22)

indexed lookups per room: 2.20 / 2.20 / 2.20
milliseconds per room: 1.7837 at 100, 1.4728 at 5000, ratio 0.83
```

Lookups per room are **exactly constant** across a fifty-fold size increase, and the per-room wall
clock falls slightly rather than rising. A per-room scan of the rooms, the legs, the connections or
the document would show as a fifty-fold growth in the first row.

Two quadratic paths were found by this review and removed before the measurement was recorded:
`Modify.BindVentilationLegs` filtered the whole leg collection once per air system (250 x 8000 on a
five thousand room scheme), and `SystemVentilationRoute.Binding` scanned its bindings linearly, which
would have made a caller's walk over its own rooms quadratic. Both are indexed now.

---

## 6. Licensed acceptance

See `PR2-ACCEPTANCE.md`.
