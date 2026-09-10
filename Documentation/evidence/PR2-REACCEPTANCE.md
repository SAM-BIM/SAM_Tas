# PR2 corrective re-acceptance, on a real Iteration 1a design

## THE ACCEPTANCE CRITERION

```
analytical Iteration 1a intent  ==  PR1 materialised intent  ==  native TAS directed graph
```

**All three, per leg, on direction as well as on duty.** Direction is proved from TAS's own connectors
and from nothing else:

* `IDuct.GetUpstreamComponent` / `GetUpstreamComponentPort`
* `IDuct.GetDownstreamComponent` / `GetDownstreamComponentPort`
* `ISystemZone.GetSystemZoneInputDuct()` / `GetSystemZoneOutputDuct()`

**No display-name inference. No schematic-coordinate inference. No creation-order inference.** The
defect this document exists to close was invisible to every check that did not ask TAS which way round
its own ducts are connected - so this is the criterion, not a section of one.

The final engineering, native and in the design's own direction (section 7 has the full table):

| dwelling | air system | the directed path, as TAS holds it |
| --- | --- | --- |
| Flat 1 | `MVHR-01` | `Fresh Air Fan` -> **Studio 1_0** (supply 30) --transfer 8--> **Bathroom_2**; extract 22 (Studio 1_0) + 8 (Bathroom_2) -> `Return Air Fan` |
| Flat 2 | `MVHR-02` | `Fresh Air Fan` -> **Bedroom 2_3** (supply 63) --transfer 63--> **Kitchen_4** --transfer 8--> **Ensuite_5**; extract 55 (Kitchen_4) + 8 (Ensuite_5) -> `Return Air Fan` |
| Flat 3 | `MVHR-03` | `Fresh Air Fan` -> **Bedroom 2_6** (supply 63) --transfer 63--> **Kitchen_7** --transfer 8--> **Ensuite_8**; extract 55 (Kitchen_7) + 8 (Ensuite_8) -> `Return Air Fan` |

Each transfer duty above is the duty the materialised design states, not a share of the supply: Flat 2
and Flat 3 pass the **whole** 63 l/s out of the bedroom into the kitchen, and the kitchen passes **8**
l/s of it on to the ensuite while extracting the other 55 itself. Every node conserves air.

**Manual inspection in the TAS GUI: PASS** (2026-09-10). One MVHR per dwelling; bedroom/living supply;
transfer toward kitchen / ensuite / bathroom extract; no cross-dwelling ventilation; `Corridor_1` not
part of any dwelling ventilation system. The schematic has scope for future visual polish, which is
deliberately **out of scope for PR2**: correct engineering topology takes precedence over schematic
cosmetics.

---

**This document supersedes the engineering acceptance in `PR2-ACCEPTANCE.md`.** That acceptance ran on
`f2.sam` through a harness that authored its own ventilation design, so what it proved was that the
route converts a synthetic design faithfully - which it does, and which is not what PR2 has to prove.
The native API discoveries it recorded remain independently true and are cited from here where they
still apply; its **engineering** verdicts do not.

Everything below was run on the licensed machine, one COM document cycle per process, against a real
SAM model prepared by the **production** Approved Document O Iteration 1a route.

---

## 1. Why the previous acceptance was invalid

A manual inspection of the previously accepted `.tpd` found transfer relationships running the
opposite way round to the design Iteration 1a produces:

| Iteration 1a intent | the old PR2 acceptance |
| --- | --- |
| `Bedroom 2_3 -> Kitchen_4` | `Kitchen_4 -> Bedroom 2_3` |
| `Bedroom 2_6 -> Kitchen_7` | `Kitchen_7 -> Bedroom 2_6` |

**This was not a conversion defect.** The old acceptance harness's `Route.Design()` did three things
that between them made the acceptance meaningless:

* it **cleared** whatever ventilation the model already carried - `VentilationSystem`,
  `VentilationTerminal`, `SpaceAirMovement` and `AirHandlingUnit` alike;
* it then **authored** supply / extract / transfer roles by taking the spaces in **ascending guid**
  (`spaces.Sort((x, y) => x.Guid.CompareTo(y.Guid))`) and assigning offsets 0..4 of each half to
  supply-only, both, hall, extract-1, extract-2;
* it generated **two air handling units** by splitting that same guid-ordered list in half, which on a
  block of flats crosses dwellings.

It then corrected the duties it had just authored, through `balanced` and `continuity` modes, because
the design it invented did not conserve air.

The route converted all of that correctly. So the reversals in the accepted document were the
harness's design showing up in TAS exactly as stated - a true answer to the wrong question.

**All of that logic is gone.** It was acceptance-harness code and nowhere else; no production path ever
contained it. The corrected harness is archived as `PR2-reacc-harness-*.cs.txt`, and
`Acc.cs`'s own header records what was removed. The regression that would have caught the class of
defect is `SystemVentilationDesignDirectionTests` - COM-free, and stated deliberately **against**
ascending guid order.

---

## 2. The fixture, and its provenance

| what | value |
| --- | --- |
| path | `C:\Users\michal.dengusiak\OneDrive - Tetra Tech, Inc\Documents\SAM_daily\2026-07-15 PartO\SAM_zoningAM-CIBSEfutureZ1.sam` |
| size | 172,542 bytes |
| SHA-256 | `A7E09A25AE29C7DBB4C690D747A96FCD9F110CA27FB4DC2ABE816755368D7E4B` |
| working copy | `C:\TasOut\pr2r\src\fixture.sam`, same hash |

A real SAM model used through SAM_UI. **It was not modified to make acceptance pass**, and the hash
above is the hash of the file as found.

What it carries as it arrives: 9 spaces, 4 zones (`Flat 1`, `Flat 2`, `Flat 3`, `Corridor`, all in the
`Flats` zone category, with `Corridor` explicitly `IsDwelling = False`), 8 of 9 spaces already carrying
`PartFSpaceData`, and six authored `VentilationSystem` objects - `NV 1`, `UV 1`, `MV 1` and
`MVHR 1..3`. Full inventory: `PR2-reacc-model-inventory.txt`.

---

## 3. Phase A - the fixture proved against the production route

Reproduced with the production calls SAM_UI itself makes, and no others:

```
SAM.Analytical.Query.DefaultPartFCalculator / PartFCalculator.Calculate("Flats")   the Part F sizing
SAM.Analytical.Query.PartFDwellingZones                                            the dwelling scope
SAM.Analytical.Modify.PreparePartOIteration(BasePassive, zones, {zone -> "MVHR"}, null, false)
```

The Part F sizing is `WPF Modify.AddVentilationByPartF` without its two dialogs: the shipped default
rule set, the shipped default setback factor (0.3), and `Calculate(zoneCategoryName)` - which is the
only call that applies the explicit dwelling filter. `"MVHR"` is the canonical word SAM_UI's own picker
hands over for `BasePassive` (`SAM.Analytical.UI.PartOVentilationStrategyOption`), and the null
catalogue **is** Iteration 1a.

`Query.PartFDwellingZones` answered 3 of the 4 zones. `PreparePartOIteration` returned
`VentilationMode = MVHR`, `AirflowApplication = Apply`, `Successful = True`, no refusal, and a design
duty of 156 l/s supply against 156 l/s extract across three systems.

**The design, read back off the prepared model** (`PR2-reacc-phase-a.log`, `PR2-reacc-intent.tsv`):

| dwelling | system | space | design supply l/s | design extract l/s |
| --- | --- | --- | --- | --- |
| Flat 1 | MVHR 1 | Studio 1_0 | 30 | 22 |
| Flat 1 | MVHR 1 | Bathroom_2 | 0 | 8 |
| Flat 2 | MVHR 2 | Bedroom 2_3 | 63 | 0 |
| Flat 2 | MVHR 2 | Kitchen_4 | 0 | 55 |
| Flat 2 | MVHR 2 | Ensuite_5 | 0 | 8 |
| Flat 3 | MVHR 3 | Bedroom 2_6 | 63 | 0 |
| Flat 3 | MVHR 3 | Kitchen_7 | 0 | 55 |
| Flat 3 | MVHR 3 | Ensuite_8 | 0 | 8 |

| transfer From | transfer To | l/s | dwelling |
| --- | --- | --- | --- |
| Studio 1_0 | Bathroom_2 | 8 | Flat 1 |
| Bedroom 2_3 | Kitchen_4 | 63 | Flat 2 |
| Kitchen_4 | Ensuite_5 | 8 | Flat 2 |
| Bedroom 2_6 | Kitchen_7 | 63 | Flat 3 |
| Kitchen_7 | Ensuite_8 | 8 | Flat 3 |

Which is the engineering the review described: **bedrooms are supply rooms, kitchens / bathrooms /
ensuites are extract rooms, transfer runs supply room -> extract room, and each system belongs to one
dwelling.**

The seven Phase A questions:

| # | question | answer |
| --- | --- | --- |
| 1 | does Iteration 1a succeed with no engineering refusal? | **yes** - `Successful = True`, `Refusal` null, `Refusals` empty |
| 2 | is dwelling/system membership coherent? | **yes** - three systems, one per dwelling zone, each with its own air handling unit `MVHR-01/02/03` |
| 3 | do supply and extract spaces match the analytical design? | **yes** - every duty is the sum of that space's own design terminals |
| 4 | are transfer directions explicit and sensible? | **yes** - five legs, each `supply room -> extract room` or `extract room -> further extract room` down one dwelling's path |
| 5 | does any transfer cross a dwelling boundary? | **no** - 0 cross-dwelling legs |
| 6 | is airflow continuity valid where TAS requires it? | **yes** - **0 of 8 rooms discontinuous**; every room's inflow equals its outflow exactly |
| 7 | is the design synthetically altered by the harness? | **no** - see section 5 |

Item 6 is the one the old acceptance had to fake. Production `PrepareBaseMVHR` routes the dwelling's
transfer air through `Modify.AddPartFTransferAirMovements` and then **refuses** an unbalanced node
itself (`RefuseUnbalancedAirMovement`), so a design that reaches TAS from this route already conserves
air at every node. **Neither the `balanced` nor the `continuity` correction exists in this harness, and
neither is needed.**

**Reproducibility.** The route was run twice - once reading the model's own carried `PartFSpaceData`,
once re-running the production Part F sizing over it - and the two intent tables are byte-identical.
The sizing the fixture carries is therefore genuine, and the whole route reproduces from the `.sam`
alone. The canonical Phase A output is the run that **re-ran** the sizing.

| artifact | SHA-256 |
| --- | --- |
| `prepared.sam` - the model Iteration 1a produced | `5D4756F45956D5F91D01B1EA55CCC3D516762D54756231F8884F928F3332CE18` |
| `intent.tsv` - the engineering oracle | `4795BF4A4E98DBB23B320B191D0770F72DE2C4328B1E4E98724A7B803E51660B` |

---

## 4. Phase B - the frozen PR1 route, and the one thing it could not be told

PR1 refuses the real Iteration 1a model as it stands. **Three staged licensed runs establish exactly
why**, and none of the three is a guess:

| run | what was in scope | PR1's answer |
| --- | --- | --- |
| the model unchanged | all six authored ventilation systems | refusal: *"Ventilation system 'UV' names no air handling unit, so the physical unit it serves from could not be resolved."* |
| systems naming no unit removed | `NV 1`, `UV 1` out; `MV 1` still in | refusal: *"Space 'Bedroom 2_3' is served by air handling units 'MVHR-02' and 'AHU1'."* |
| systems carrying **no design terminal** removed | `NV 1`, `UV 1`, `MV 1` out | **materialised**: 3 air systems, 8 rooms, 25 lineage bindings |

Transcripts: `PR2-reacc-pr1-unscoped.log`, `PR2-reacc-pr1-nounit.log`,
`PR2-reacc-pr1-noterminal.log`.

Read across the rows. `Create.MechanicalVentilation` walks **every** `VentilationSystem` the cluster
carries and requires each to name a resolvable air handling unit - while production
`Modify.PreparePartOIteration` deliberately **preserves** the model's authored `NV` / `UV` / `MV`
systems and says so in its own notes (*"that relation is left as the model authored it"*). On any real
model the two are in genuine contradiction, and PR1 gives the caller a **space** scope but no **system**
scope with which to say which ventilation design is being materialised.

The second row shows the conflict is not only about unnamed units: `MV 1` names `AHU1`, which resolves,
and PR1's own cross-unit membership guard then correctly refuses because `AHU1` would claim six spaces
that `MVHR-02` and `MVHR-03` already serve.

### The harness's scoping step, and what it is not

The harness therefore takes the ventilation systems that carry **no design ventilation terminal** out of
its working copy before calling PR1 (`Pr1.ScopeSystems`, rule `noterminal`). The rule is stated in terms
of the design itself - *a `VentilationSystem` with no design terminal materialises no duty, so it is not
part of the mechanical ventilation being materialised* - and no name, type, guid order or enumeration
index decides it.

**It is not the old defect, and the difference is asserted rather than argued.** The intent table is
recomputed off the scoped cluster and compared with the one Phase A wrote off the unscoped prepared
model; the run refuses to continue unless they are **byte-identical**, which they are. No terminal, air
movement, air handling unit, space, duty, role or transfer direction moves.

**This is reported as PR1 debt, not resolved here, and it is tracked: SAM-BIM/SAM#114.** PR1 is frozen
and `SAM_Systems` is untouched by this work. #114 records the fixture, both exact refusals, why the
authored NV / UV / MV systems are there at all, the harness-only scoping used for this acceptance, and
the two candidate ownerships - a `SAM_Systems` filtering API, or PR4 / SAM_UI caller scoping - **without
prescribing either**, because which is right needs investigation first. **#114 must be considered before
PR4 is declared complete**: a production Iteration 3 orchestration that cannot materialise a real Part O
model is not a complete PR4.

### What PR1 produced

```
note: Air handling unit 'MVHR-03' materialised as one air system serving 3 space(s).
note: Air handling unit 'MVHR-02' materialised as one air system serving 3 space(s).
note: Air handling unit 'MVHR-01' materialised as one air system serving 2 space(s).
note: 3 air system(s), 8 room(s) and 25 lineage binding(s) materialised and reconciled against the design.
```

25 bindings: 3 `AirSystem`, 8 `SystemSpace`, 3 `SupplyConnection`, 6 `ExtractConnection`,
5 `TransferConnection` - one per row of the intent table above, and none besides.

The frozen invariant holds by construction: PR1 reads design terminal duty and reads no Part F
requirement, no selected capacity and no operating airflow.

```
PartFRequiredAirFlow != DesignAirFlow != SelectedEquipmentCapacity != OperatingAirFlow
```

---

## 5. Phase C - one fresh no-IZAM thermal source, from this fixture

Generated once, by the production call, from `prepared.sam` and nothing else:

```
SAM.Analytical.gbXML.Convert.TogbXML                    the geometry export
SAM.Analytical.Tas.Create.NoIzamThermalSource           the thermal source, both cleanups forced on
```

Full year, days 1..365. Weather `C:\Users\Public\Documents\Tas Data\Databases\CIBSE Weather 2021.twd`
(SHA-256 `20D3DBE3E7DAFD1497A52FB2BFC86EE5B1C9B7A4CC2ECF653C7A56021D85B14C`), year **`Leeds_TRY`** -
`weatherDatas[0]`, the same selection rule the superseded acceptance used, recorded explicitly here
because it was not recorded there.

| artifact | SHA-256 |
| --- | --- |
| `C:\TasOut\pr2r\c1\src.tbd` - the no-IZAM thermal source | `A01CFBB6B8CD9EB6F7B103696A1DE2A1697C4396708C70561D45C0A18D3C9654` |
| `C:\TasOut\pr2r\c1\src.tsd` - its paired TSD, 4,586,194 bytes | `7239B8D139F7703F6EDDA4B13EDD9D86FACBB039E470EA243A61C44BDE5A5277` |

The Phase D run reads **byte-identical copies** of both and hashes them before and after: they are
`UNCHANGED` in `PR2-reacc-provenance.txt`. The route reads its thermal source and never writes it.

Read back through TBD's own 0-based null-terminated accessors, independently of the code that wrote
the file (`PR2-reacc-phase-c.log`):

| # | item | verdict | evidence |
| --- | --- | --- | --- |
| 1 | no IZAM | **PASS** | IZAM count 0 |
| 2 | mechanical `ticV` zero on every internal condition | **PASS** | 30 internal conditions, 0 with a non-zero `ticV` |
| 3 | infiltration `ticI` untouched and non-zero where the model states one | **PASS** | `ticI` present on 30 of 30, non-zero on 9 |

The production note states the cleanup in full: `ticV` zeroed on all 30 internal conditions by name,
and *"Infiltration (ticI) and natural ventilation (aperture types and opening schedules) are untouched -
they are separate carriers."* 9 rooms carry a TAS zone identity, which is every space in the model.

Geometry, fabric, weather, occupancy and internal gains reach the TBD through the ordinary gbXML/T3D
workflow and are not touched by either cleanup; the workflow's own step log is in the transcript.

### One environment finding, recorded because it cost a run

A **1-day** simulation period (`SimulateTo = 1`) crashes `TSD.exe` natively - Windows Application Error
`0xc0000005` in `TSD.exe 2.0.0.1` - inside `Modify.AddResults`, surfacing as
`COMException: The RPC server is unavailable (0x800706BA)` from `SAMTSDDocument.Dispose`. The results
have already been read by then; it is the teardown that fails.

It is **not** a property of this fixture: the same 1-day run on the old `f2.sam` fails identically
(`PR2-reacc-oneday-tsd-crash.log` holds the step-by-step timings and the stack). The full-year run -
which is what the acceptance needs and uses - completes normally.

Diagnosed narrowly, left alone, and tracked separately as **SAM-BIM/SAM#115**. No production code was
changed for it in this closeout, and PR2 does not broaden into solving it.

---

## 6. Phase D - the explicit Systems / TPD route, items 4 to 15

One process: the real design, the frozen PR1 materialisation, the hashed no-IZAM source, the production
`Create.SystemVentilationRoute`, hours **0..8759**, then the document read back **independently**,
late-bound, through TAS's own accessors.

The frozen #111 operating configuration is supplied through PR1's own generic settings: a
`YearlySchedule` named `"Part O continuous operation"` holding 8760 values, all `1.0`.

| # | item | verdict | evidence |
| --- | --- | --- | --- |
| 4 | separate analytical units remain separate TAS systems | **PASS** | 3 materialised units, 3 native TAS systems, 3 distinct systems named by the room bindings |
| 5 | every intended room belongs to the correct TAS system | **PASS** | 8 rooms bound, 8 materialised; 0 native zones missing, 0 in the wrong system |
| 6 | supply / extract / both / transfer-only topology | **PASS** | 3/3 supply, 6/6 extract, 5/5 transfer legs; 2/2 supply-only, 5/5 extract-only, 1/1 both, 0/0 transfer-only rooms |
| 7 | supply `DesignFlowRate` on the room's own zone, absolutely | **PASS** | 3 supply rooms, 0 mismatches |
| 8 | extract `DesignFlowRate` on its own in-line damper | **PASS** | 6 extract legs, 0 mismatches |
| 9 | transfer `DesignFlowRate` on its own in-line damper | **PASS** | 5 transfer legs, 0 mismatches |
| 10 | no duplicated ventilation | **PASS** | 8 native zones, 0 still modelling their own ventilation (bit 4) or interzone flow (bit 2); `Flags=1` throughout |
| 11 | fan duty derived from the attached zones, never authored | **PASS** | 6 fans, all `tpdFlowRateAllAttachedZonesFlowRate`, values 63 / 63 / 63 / 63 / 30 / 30 |
| 12 | complete finite `ZoneTemperature` for the period | **PASS** | 8 of 8 rooms, 8760 values each over hours 0..8759 |
| 13 | the route's values agree with TAS's own, read independently | **PASS** | 8 of 8 rooms agreed value for value over 8760 hours, each after a raw late-bound simulate of its own air system |
| 14 | duplicate display names do not alter identity | **PASS** | 22 native zones and dampers renamed to one shared string; results still validate, 8 of 8 identical |
| 15 | the native `SystemZone` / `ZoneLoad` identity chain | **PASS** | 8/8 zone guids round-trip, 8/8 zone loads resolve to the bound load, all identities distinct |

TAS answered `"Done"` to each of the three air systems, taken one at a time with the results captured
before the next was simulated. `NativeDiagnostic: [Done] kind=KnownSuccess`.

Items 13 and 14 each work on their own **copy** of the document; the accepted `acc.tpd` is not the
renamed or re-simulated one. Full transcript: `PR2-reacc-acceptance.log`.

The frozen native rules are all still in force and all still hold: guid identity and never display
name, one `AirSystem` per physical unit, l/s as the absolute unit, supply on `SystemZone`
`FlowRate`/`FreshAir`, extract and transfer on a typed `Damper.DesignFlowRate`, junctions carrying
topology and no duty, per-air-system `ISystem.Simulate`, results taken per system, `"Done"` as the
native success word, `HeatGainFactor = 0`, constant `1.0` operation, and **no `ComponentGroup`**.

---

## 7. Directed topology - the acceptance the previous one failed

Every edge below comes from `IDuct.GetUpstreamComponent` / `GetUpstreamComponentPort` /
`GetDownstreamComponent` / `GetDownstreamComponentPort`. **No screen coordinate, display name, creation
order or drawing is read.** Three independent statements are compared per leg: the analytical intent
(`intent.tsv`, written before any conversion), the route's own claim (`claim-legs.tsv`, written before
the read-back) and the native directed graph. Transcript: `PR2-reacc-topology.txt`.

| leg | intended from | intended to | duty l/s | native upstream | native downstream | native duty | verdict |
| --- | --- | --- | --- | --- | --- | --- | --- |
| transfer | Bedroom 2_3 | Kitchen_4 | 63 | SystemZone "Bedroom 2_3" | SystemZone "Kitchen_4" | 63 | **PASS** |
| transfer | Bedroom 2_6 | Kitchen_7 | 63 | SystemZone "Bedroom 2_6" | SystemZone "Kitchen_7" | 63 | **PASS** |
| transfer | Kitchen_4 | Ensuite_5 | 8 | SystemZone "Kitchen_4" | SystemZone "Ensuite_5" | 8 | **PASS** |
| transfer | Kitchen_7 | Ensuite_8 | 8 | SystemZone "Kitchen_7" | SystemZone "Ensuite_8" | 8 | **PASS** |
| transfer | Studio 1_0 | Bathroom_2 | 8 | SystemZone "Studio 1_0" | SystemZone "Bathroom_2" | 8 | **PASS** |
| supply | the unit | Studio 1_0 | 30 | Fan "Fresh Air Fan" | SystemZone "Studio 1_0" | 30 | **PASS** |
| supply | the unit | Bedroom 2_3 | 63 | Fan "Fresh Air Fan" | SystemZone "Bedroom 2_3" | 63 | **PASS** |
| supply | the unit | Bedroom 2_6 | 63 | Fan "Fresh Air Fan" | SystemZone "Bedroom 2_6" | 63 | **PASS** |
| extract | Bathroom_2 | the unit | 8 | SystemZone "Bathroom_2" | Fan "Return Air Fan" | 8 | **PASS** |
| extract | Studio 1_0 | the unit | 22 | SystemZone "Studio 1_0" | Fan "Return Air Fan" | 22 | **PASS** |
| extract | Ensuite_5 | the unit | 8 | SystemZone "Ensuite_5" | Fan "Return Air Fan" | 8 | **PASS** |
| extract | Kitchen_4 | the unit | 55 | SystemZone "Kitchen_4" | Fan "Return Air Fan" | 55 | **PASS** |
| extract | Ensuite_8 | the unit | 8 | SystemZone "Ensuite_8" | Fan "Return Air Fan" | 8 | **PASS** |
| extract | Kitchen_7 | the unit | 55 | SystemZone "Kitchen_7" | Fan "Return Air Fan" | 55 | **PASS** |

**14 legs, 14 PASS, 0 FAIL.** And what must not be there:

* **no reversal.** `Bedroom 2_3 -> Kitchen_4` and `Bedroom 2_6 -> Kitchen_7` - the two legs the review
  found reversed - are native, in that direction, with the design's own duty on a typed damper;
* **no extract-room-to-supply-room reversal** anywhere;
* **no cross-dwelling membership**: 0 native edges cross an air system boundary, and each system's
  rooms are exactly one flat's;
* **no invented Corridor path.** `Corridor_1` belongs to no dwelling (`IsDwelling = False`), carries no
  design terminal and no transfer, and appears in **no** air system. It is in the thermal model, where
  it belongs, and nowhere else;
* **no missing transfer**: all 5 stated legs are present;
* **no additional transfer**: 14 native dampers, of which 3 are the template's own derived-duty trunk
  dampers (`tpdFlowRateAllAttachedZonesFlowRate`, one per system, sized at 63 / 63 / 30 l/s = each
  system's own supply total) and **11 carry an authored duty, every one of them a leg the design
  states**. None is unaccounted for.

Two things about the trunk damper, since a checker that did not know about it reported six false
failures on the first pass and both facts are needed to read the table above. It sits between the
`Fresh Air Fan` and the branch junction; it **derives** its flow from the zones attached downstream
rather than carrying a duty of its own, which is why it is transparent to the question *"what supplies
this room"*; and being derived rather than authored is exactly what distinguishes it from the eleven
design dampers. `tpdFlowRateValue` is the authored carrier the frozen rules name for every extract and
transfer duty, and nothing else in the document uses it.

Per-node arithmetic off the canonical network (`PR2-reacc-network.txt`), raw ducts and ports in
`PR2-reacc-graph.txt`. Each dwelling balances:

```
Flat 2 / MVHR-02:  supply 63 -> Bedroom 2_3
                   Bedroom 2_3 --63--> Kitchen_4 --8--> Ensuite_5
                   extract 55 (Kitchen_4) + 8 (Ensuite_5) = 63
Flat 3 / MVHR-03:  identical shape, Bedroom 2_6 / Kitchen_7 / Ensuite_8
Flat 1 / MVHR-01:  supply 30 -> Studio 1_0
                   Studio 1_0 --8--> Bathroom_2
                   extract 22 (Studio 1_0) + 8 (Bathroom_2) = 30
```

---

## 8. Fan operation, re-proved on the real design

Read off native hourly TAS result data on a copy of the accepted document
(`PR2-reacc-fan-operation.txt`), 6 fans across 3 air systems:

* **the schedule is the frozen constant 1.0 representation** - `"Part O continuous operation"`,
  `Type=1 FunctionType=1 FunctionLoads=0`, and TAS's own `GetNumOperableHours` answers **8760**;
  `GetYearlyValue(1..8760)` reads 8760 values, and the out-of-range probes `GetYearlyValue(0)` and
  `GetYearlyValue(8761)` answer 0, which is how the 8760 in range are known to be the whole year;
* **8760 of 8760 operable hours**, every fan;
* **delivered airflow equals the intended duty throughout.** Derived from the fan's own hourly Load
  result and its `Pressure` / `OverallEfficiency`: `n=8760`, `min = max = the derived duty`,
  `nonZeroHours = 8760`, `hoursAtDesignDuty = 8760`, `nonFinite = 0`, `firstZeroHour = none`;
* **`HeatGainFactor` remains 0** on all six.

```
SUMMARY 6 of 6 fan(s) delivered their derived design duty in every hour of 0..8759
```

Duties: 63 / 63 l/s on `MVHR-02` and `MVHR-03` (both fans of each), 30 / 30 on `MVHR-01` - each
system's own supply total, derived and never authored.

**SAM-BIM/SAM#113** - TAS flow sizing being sensitive to deterministic object creation order and
schedule identity - **was not encountered on this fixture**. The canonical route completed on the first
attempt with no sizing refusal, on all three systems. It is neither fixed nor hidden: the route still
fails closed on it, and nothing here reorders anything to avoid it. **#113 stays open**, its description
unchanged; one fixture drawing an order TAS accepts is not evidence the mechanism is gone, and the
observation is recorded there as a comment rather than as a resolution.

---

## 9. The presentation-only layout

`0 overlapping component pairs` and `0 duct passes through a non-endpoint box`, across all three air
systems (`PR2-reacc-layout.txt`). Supply enters from the left, rooms are one row each in air-path
order, transfer dampers stack beneath their room and extract dampers stand to its right, so the extract
side reads separately. **The drawing follows the real engineering direction**: the row order down each
system is the order the air actually travels.

**Positions are inert to the results, and this is measured, not asserted.** Every top-level component
of every air system was moved to a fresh grid on a copy, both documents were then simulated with a raw
late-bound `System.Simulate(1, 8760, 0)`, and all 8 zones' whole 8760-hour `ZoneTemperature` series
came back **byte-identical** - `PR2-reacc-layout-inert.txt`,
`CAE40BBEACD9845289B07DE5A6B49AA847ABFDC97B805916CB1ED89B12190B5D` both times.

### One layout rule the real design corrected

The layout classified a room by its duties alone, so a room with extract and no supply went to the
terminal extract-only column - **past** the transfer column. The synthetic fixture never produced a room
that extracts *and* passes air on, so this never showed. A real dwelling is full of them: a kitchen
extracting 55 l/s that still transfers 8 l/s to an ensuite. Standing that kitchen past the transfer
column forced its own outgoing duct to double back through the kitchen's own box - **2 duct passes on
the first real-design run**.

The rule now reads what it always meant: the extract-only column is for a room the air path **ends**
at. A room that extracts and still passes air on is in the middle of the path and stays in the room
column, which also puts the rows in true air-path order (bedroom, then kitchen, then ensuite). One
production file, presentation only:
`SAM.Analytical.Tas.TPD/Query/VentilationLayout.cs`. Pinned by
`VentilationLayoutTests.ARoomThatExtractsAndStillPassesAirOnStaysInTheRoomColumn`.

The safe presentation surface is unchanged - `SetPosition`, `SetDirection`, duct `AddNode` bend points,
**no `ComponentGroup`** - and no creation order was changed for appearance.

### FROZEN for PR2

The layout implementation is **frozen**. No further aesthetic change is to be made in PR2, by direction
after the manual inspection. What is recorded and fixed:

| | |
| --- | --- |
| positioning, direction, duct bend nodes | **presentation only** - and measured inert, above |
| `ComponentGroup` | **not used**, anywhere |
| directed topology | read from native connections, never from screen placement |
| overlapping component boxes | **0** |
| ducts through a non-endpoint box | **0** |
| moving every component | **0 K** ZoneTemperature change, all 8 rooms x 8760 hours |

Further schematic polish is a later task if it is wanted at all.

---

## 10. Artifact manifest

| purpose | absolute path | SHA-256 | generating step | tracked in git |
| --- | --- | --- | --- | --- |
| the fixture, as found | `C:\Users\michal.dengusiak\OneDrive - Tetra Tech, Inc\Documents\SAM_daily\2026-07-15 PartO\SAM_zoningAM-CIBSEfutureZ1.sam` | `A7E09A25AE29C7DBB4C690D747A96FCD9F110CA27FB4DC2ABE816755368D7E4B` | none - a real SAM_UI model | no |
| weather | `C:\Users\Public\Documents\Tas Data\Databases\CIBSE Weather 2021.twd` | `20D3DBE3E7DAFD1497A52FB2BFC86EE5B1C9B7A4CC2ECF653C7A56021D85B14C` | shipped Tas data | no |
| the prepared model | `C:\TasOut\pr2r\a2\prepared.sam` | `5D4756F45956D5F91D01B1EA55CCC3D516762D54756231F8884F928F3332CE18` | `prep` - production Iteration 1a | no |
| the engineering oracle | `C:\TasOut\pr2r\a2\intent.tsv` | `4795BF4A4E98DBB23B320B191D0770F72DE2C4328B1E4E98724A7B803E51660B` | `prep` | yes, `PR2-reacc-intent.tsv` |
| **the source TBD** | `C:\TasOut\pr2r\c1\src.tbd` | `A01CFBB6B8CD9EB6F7B103696A1DE2A1697C4396708C70561D45C0A18D3C9654` | `source` - `Create.NoIzamThermalSource`, days 1..365 | no |
| **the paired TSD** | `C:\TasOut\pr2r\c1\src.tsd` | `7239B8D139F7703F6EDDA4B13EDD9D86FACBB039E470EA243A61C44BDE5A5277` | same call, same run | no |
| **the candidate FINAL TPD** | `C:\TasOut\pr2r\final\acc.tpd` | `553C9E1C364E6F528417247DCE0FE1D31B8884939EDA8A5217B46BDBF5B755FE` | `run full` - `Create.SystemVentilationRoute`, hours 0..8759 | no |
| analytical / PR1 intent transcript | `C:\TasOut\pr2r\a2\phase-a.log` | see log | `prep` | yes, `PR2-reacc-phase-a.log` |
| PR1 staged diagnosis | `C:\TasOut\pr2r\b-*/pr1.log` | see logs | `pr1 none / nounit / noterminal` | yes, `PR2-reacc-pr1-*.log` |
| no-IZAM transcript | `C:\TasOut\pr2r\c1\phase-c.log` | see log | `source` | yes, `PR2-reacc-phase-c.log` |
| acceptance log, items 4-15 | `C:\TasOut\pr2r\final\acceptance.log` | see log | `run full` | yes, `PR2-reacc-acceptance.log` |
| directed topology transcript | `C:\TasOut\pr2r\final\topology.txt` | see log | `topo` | yes, `PR2-reacc-topology.txt` |
| canonical network | `C:\TasOut\pr2r\final\network.txt` | see log | `net` | yes, `PR2-reacc-network.txt` |
| raw native graph | `C:\TasOut\pr2r\final\graph.txt` | see log | `graph` | yes, `PR2-reacc-graph.txt` |
| fan-operation transcript | `C:\TasOut\pr2r\final\fan-operation.txt` | see log | `fanprobe 0 8759` | yes, `PR2-reacc-fan-operation.txt` |
| layout evidence | `C:\TasOut\pr2r\final\layout.txt` | see log | `layout` | yes, `PR2-reacc-layout.txt` |
| layout inertness | `C:\TasOut\pr2r\final\zt-*.txt` | `CAE40BBE…12190B5D` both | `zt` / `spread` / `zt` | yes, `PR2-reacc-layout-inert.txt` |
| provenance / hash log | `C:\TasOut\pr2r\final\provenance.txt` | see log | `run full` | yes, `PR2-reacc-provenance.txt` |
| the route's own claim | `C:\TasOut\pr2r\final\claim-*.tsv` | see files | `run full` | yes, `PR2-reacc-claim-*.tsv` |
| TAS error log | the 1-day `TSD.exe` crash | see log | `diag sizing` | yes, `PR2-reacc-oneday-tsd-crash.log` |
| the corrected harness | `C:\TasOut\pr2r\*.cs`, `pr2r.csproj` | see files | hand-written | yes, `PR2-reacc-harness-*.txt` |

No `.tbd`, `.tsd` or `.tpd` binary is tracked in git.

## 10b. Follow-ups this acceptance opened or touched

| issue | state | what it holds | gate |
| --- | --- | --- | --- |
| SAM-BIM/SAM#114 | **open**, new | PR1 `Create.MechanicalVentilation` has no system scope and refuses any real Part O model carrying authored NV / UV / legacy MV systems. Fixture, both exact refusals, the harness-only scoping, and the two candidate ownerships - no fix prescribed. | **must be considered before PR4 is declared complete** |
| SAM-BIM/SAM#115 | **open**, new | a 1-day simulation period crashes `TSD.exe` (`0xc0000005`) inside `Modify.AddResults`; reproduces on both fixtures; full-year route unaffected. | none on PR2 - explicitly out of scope |
| SAM-BIM/SAM#113 | **open**, unchanged | TAS flow sizing sensitive to creation order. Not encountered on this route; commented, not closed. | none - do not close on one passing fixture |

Nothing here changes `SAM_Systems` or `SAM` production source.

## 11. What is in the candidate document

`C:\TasOut\pr2r\final\acc.tpd` - one energy centre, one plant room, **three** air systems:

| air system | rooms | supply | extract | transfers |
| --- | --- | --- | --- | --- |
| `MVHR-01` (Flat 1) | Studio 1_0, Bathroom_2 | Studio 1_0 30 | Studio 1_0 22, Bathroom_2 8 | Studio 1_0 -> Bathroom_2 8 |
| `MVHR-02` (Flat 2) | Bedroom 2_3, Kitchen_4, Ensuite_5 | Bedroom 2_3 63 | Kitchen_4 55, Ensuite_5 8 | Bedroom 2_3 -> Kitchen_4 63, Kitchen_4 -> Ensuite_5 8 |
| `MVHR-03` (Flat 3) | Bedroom 2_6, Kitchen_7, Ensuite_8 | Bedroom 2_6 63 | Kitchen_7 55, Ensuite_8 8 | Bedroom 2_6 -> Kitchen_7 63, Kitchen_7 -> Ensuite_8 8 |

`Corridor_1` - the ninth space, in the non-dwelling `Corridor` zone - is deliberately **not** in the
document. It has no design terminal, no transfer and no dwelling, and inventing a path through it is one
of the failures this acceptance had to exclude.

## 12. Repository state this ran against

| repository | branch | head | working tree |
| --- | --- | --- | --- |
| SAM-BIM/SAM_Tas | `part-o/iteration3-tas-systems-route` | `fc32261` plus this delta | the delta, uncommitted |
| SAM-BIM/SAM | `sow/2026-Q3` | `413215cc` | clean, untouched |
| SAM-BIM/SAM_Systems | `sow/2026-Q3` | `89cf139` (PR1, `75bc8aa`) | clean, untouched |

All four dependency repositories - SAM, SAM_gbXML, SAM_Systems, SAM_Tas - were rebuilt in **Release**
from those states before any engineering was run, so no stale DLL from the previously checked-out
`docs/capability-presentation-p0` state could reach the acceptance.

---

## 13. Operator notes, kept for the future `sam-tas-systems-operator` skill

Not a skill, and not to be built in this closeout. This section is the operating knowledge the future
portable skill has to capture, written down here so it survives the session. The harness itself is
archived beside it as `PR2-reacc-harness-*.txt` and `PR2-reacc-harness-pr2r.csproj.txt` - **the corrected
one**, with no design-authoring path in it.

### The lesson that matters more than any of the mechanics

**Never manufacture an acceptance airflow design by guid or enumeration order.** Always establish, and
then explicitly compare, three things:

```
real SAM analytical intent  ->  real PR1 materialisation  ->  native directed TAS graph
```

A harness that authors its own design will pass every native check and still accept a reversed network.
The tell is a harness that has to *repair* what it authored: if a fixture needs correcting to simulate,
the fixture is wrong.

### Environment and licence discovery

```powershell
$c = (Get-ItemProperty 'HKLM:\SOFTWARE\Classes\TBD.Document\CLSID').'(default)'
(Get-ItemProperty "HKLM:\SOFTWARE\Classes\CLSID\$c\LocalServer32").'(default)'
# also TPD.Document and TSD.Document -> C:\PROGRA~1\ENVIRO~1\Tas\{TBD,TPD,TSD}.exe
Get-Process -Name TBD,TSD,TAS3D,TPD,TWD,TCD -ErrorAction SilentlyContinue | Select Name,Id
```

Discover, never hard-code. Orphan COM processes block silently at near-zero CPU with no dialog - sweep
them between phases, not only at the start. A TAS GUI open as an application holds the licence and makes
every call block.

### Portable work directory

Short paths only, under `C:\TasOut\<tag>`, never a nested temp/scratchpad path. Every path handed to TAS
absolute. One subdirectory per phase, so each phase's inputs are immutable to the next:
`src/` the fixture, `a2/` the prepared model and intent oracle, `c1/` the thermal source, `final/` the
document and its transcripts.

### Repository and SHA verification, before any engineering

Confirm each repository's branch, head and clean working tree; confirm the PR head is at or descended
from the expected SHA; then **rebuild every dependency in Release from those states** so no stale DLL
from a previously checked-out branch can reach the run. Order: SAM -> SAM_gbXML -> SAM_Systems ->
SAM_Tas. The `.sln` needs .NET Framework MSBuild with Restore and Build as **separate** invocations; a
`dotnet build` of the test project immediately before `dotnet test --no-build`, or the test runs against
the previous DLL.

### The route, one COM document cycle per process

| step | mode | production call |
| --- | --- | --- |
| the design | `prep` | `Query.DefaultPartFCalculator` / `PartFCalculator.Calculate(category)`, `Query.PartFDwellingZones`, `Modify.PreparePartOIteration` |
| the thermal source | `source` | `gbXML.Convert.TogbXML`, `Create.NoIzamThermalSource` (full year - never 1 day, #115) |
| the graph and the document | `run` | `Systems.Create.MechanicalVentilation`, `TPD.Create.SystemVentilationRoute` |
| the read-backs | `topo`, `net`, `graph`, `layout`, `fanprobe`, `zt`, `spread`, `source-check` | none - late-bound reads only |

### Provenance and hashing

Hash the fixture, the weather file, the TBD and the TSD. Each run reads **byte-identical copies** of the
thermal source and hashes them before *and* after, so "the route never writes its source" is measured.
Write the machine-readable intent oracle **before** any conversion and make every later check compare
against it rather than against a literal.

### The checks that have to be native

* **no-IZAM / `ticV`** - read the TBD back through TBD's own 0-based null-terminated accessors
  (`building.GetIZAM(i)`, `building.GetIC(i)`, `internalGain.GetProfile((int)Profiles.ticV|ticI)`),
  never through the code that wrote it. `SAM.Core.Tas` embeds its interop types, so any helper returning
  `List<TBD.something>` cannot even be called across the assembly boundary (CS1769) - which is the right
  constraint for a harness.
* **directed topology** - `IDuct.GetUpstreamComponent` / `GetDownstreamComponent` and
  `ISystemZone.GetSystemZoneInputDuct()` / `GetSystemZoneOutputDuct()`. Collapse `Junction`,
  `GroupJunction` and `ComponentGroup` to transparent nodes, and **also** see through a damper whose
  `DesignFlowType` is derived (`tpdFlowRateAllAttachedZonesFlowRate`) rather than authored
  (`tpdFlowRateValue`) - the template's trunk damper is one, and a checker that does not know that
  reports false failures on every supply leg.
* **per-air-system simulation** - `ISystem.Simulate`, one system at a time, capturing each system's
  results **before** advancing: the next `Simulate` replaces the previous system's in-memory result
  surface. Native success word is `"Done"`.
* **continuous operation** - `GetNumOperableHours() == 8760` on a yearly schedule, plus the fan's own
  hourly Load series (`GetResultsData` variable 9; every other variable throws) converted through
  `Pressure` / `OverallEfficiency` to a delivered flow, and the out-of-range probes
  `GetYearlyValue(0)` / `GetYearlyValue(8761)` to prove the 8760 in range are the whole year.
* **identity** - guids everywhere; prove it by renaming every zone and damper to one shared string on a
  copy and re-reading the results.

### Presentation

`SetPosition`, `SetDirection`, duct `AddNode` bend points. **No `ComponentGroup`.** Bend nodes can only
be given at creation - there is no removal. Prove positions inert by moving every component on a copy and
comparing whole hourly series, rather than asserting it.

### Known order sensitivity

TAS flow sizing can refuse an identical network depending on creation order (#113). Fail closed; never
reorder to get past it.

### Artifact manifest

Every run ends with one table: purpose, absolute path, SHA-256, generating step, and whether it is
tracked in git. **No project `.tbd` / `.tsd` / `.tpd` binary is ever bundled into a skill.**
