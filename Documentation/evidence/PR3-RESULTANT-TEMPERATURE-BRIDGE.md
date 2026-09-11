# PR3 - the ResultantTemperature thermostat bridge (SAM-BIM/SAM#111, Iteration 3)

PR3 of SAM-BIM/SAM#111. SAM_Tas only. The route this PR owns:

```
TAS Systems (TPD) ZoneTemperature
  -> COPY of the route's own, explicitly paired no-IZAM TBD
  -> heating (ticLL) AND cooling (ticUL) thermostat := achieved room-air temperature, hour by hour
  -> second TSD
  -> ResultantTemperature, per analytical room guid
```

behind a replaceable seam, `IResultantTemperatureProvider`, so that when TAS Systems exposes a native
resultant temperature only the bridge (`ThermostatBridgeResultantTemperatureProvider`,
`Create.ThermostatBridge` and the types only they use) is deleted. SAM, SAM_Systems, PR1, PR2 and the TM59
authority do not change.

**Measured facts and assumptions are kept apart below. Everything under "measured" was observed on the
licensed machine in this session; everything under "not measured" was not.**

---

## 1. Verdict

| gate | result |
| --- | --- |
| A. input transfer - Systems `ZoneTemperature[h]` == heating[h] == cooling[h], every room, every hour, every internal condition | **PASS** - 8 of 8 rooms, 8760/8760 heating and 8760/8760 cooling slots on both ICs; **MAX ABSOLUTE TRANSFER DELTA = 0** |
| B. completeness - 8760 finite `ResultantTemperature` per room, no missing or duplicate room | **PASS** - 8 of 8, 8760 finite each |
| C. identity - every TBD zone renamed to one shared string; results unchanged | **PASS** - 8 of 8 rooms bit-identical over 8760 hours (max abs diff 0) |
| D. source immutability - no-IZAM TBD (and TSD) hash before == after | **PASS** - `FC5C677F...` before and after; TSD `16CED2B6...` before and after |
| E. real execution - the second simulation proven by its output, not by a bool | **PASS** - stale TSD deleted first; fresh 4,953,965-byte TSD written 71 s after the run started; no error log; results read back and reconciled |
| achieved air follows the imposed series (#111 PR3 exit gate) | **PASS** - max 0.0010 K, mean 0.0008-0.0009 K per room, at 0 h shift |
| hours are not shifted | **PASS** - see section 5 |
| PR1 / PR2 behaviour unchanged | **PASS** - PR2 acceptance items 4-15 PASS and directed topology 14 PASS / 0 FAIL, re-run on this build |

---

## 2. Source state

| repository | branch | commit | working tree |
| --- | --- | --- | --- |
| SAM-BIM/SAM_Tas | `part-o/iteration3-resultant-temperature-bridge` off `sow/2026-Q3` | `5bec3a6d` (PR2 merge) + this PR | the PR3 delta |
| SAM-BIM/SAM | `sow/2026-Q3` | `413215cca722a70b660c4ef367f6faab1d6d9357` | clean, untouched |
| SAM-BIM/SAM_Systems | `sow/2026-Q3` | `89cf139966f4fe426459851d09f052834551792f` (PR1 merged) | clean, untouched |
| SAM-BIM/SAM_gbXML | `sow/2026-Q3` | `3b96001f6075f1eb6aea341eafab6762528aeaed` | clean, untouched |
| SAM-BIM/SAM_SolarCalculator | `sow/2026-Q3` | `3a36b73cde257d7fcfdd3d866177b419f29af998` | clean, untouched |
| SAM-BIM/SAM_UI | `sow/2026-Q3` | `af7535db7746825e4132369b557939831b27ef5c` | clean, untouched |

Every dependency was rebuilt in **Release** from exactly those states before any licensed run (SAM ->
SAM_SolarCalculator -> SAM_gbXML -> SAM_Systems -> SAM_Tas). The local SAM checkout had been on
`docs/capability-presentation-p0`; it was switched to `sow/2026-Q3` (clean tree) for this, so no DLL from
that branch reached the acceptance.

---

## 3. The chain, regenerated from the real fixture on this machine

| phase | production call | artifact | SHA-256 |
| --- | --- | --- | --- |
| fixture | none - the real SAM_UI model, unmodified | `SAM_zoningAM-CIBSEfutureZ1.sam`, 172,542 bytes | `A7E09A25AE29C7DBB4C690D747A96FCD9F110CA27FB4DC2ABE816755368D7E4B` |
| weather | shipped | `CIBSE Weather 2021.twd`, year `[0]` Leeds_TRY | `20D3DBE3E7DAFD1497A52FB2BFC86EE5B1C9B7A4CC2ECF653C7A56021D85B14C` |
| A. Iteration 1a design | `PartFCalculator.Calculate("Flats")`, `PartFDwellingZones`, `PreparePartOIteration(BasePassive, ..., "MVHR")` | `prepared.sam` | `5DC91DB46E67363E528404CB727CB93564BAB4063A4CFBFE41A963D69959034B` |
| A. intent oracle | measured off the prepared model | `intent.tsv` | `4795BF4A4E98DBB23B320B191D0770F72DE2C4328B1E4E98724A7B803E51660B` - **byte-identical to PR2's committed `PR2-reacc-intent.tsv`** |
| C. no-IZAM source | `Create.NoIzamThermalSource`, days 1..365 | `src.tbd` 401,573 bytes | `FC5C677F7A6A13AAB6D11607BB62BBC07B97DC16A3A64923BBDEEB2AD2063537` |
| C. its paired TSD | same call | `src.tsd` 4,594,686 bytes | `16CED2B62541EA77F078EA84D54B690EF1EBC77FC6A74FE1F76FB1919F94F1C1` |
| D. PR1 + PR2 | `Systems.Create.MechanicalVentilation`, `TPD.Create.SystemVentilationRoute`, hours 0..8759 | `acc.tpd` | `FE46B2830327B4038AE09B2FD9551120FB80EA88AAA296812FB3195BE0E0D0CE` |
| **PR3** | **`TPD.Create.ThermostatBridge`** | `bridge.tbd` (thermostats written, simulated, saved) | `3FD9F2AE0830E8CE2808C6630BAB8F7CBDF47880BA7C4F6953456528E20A6EFB` |
| **PR3** | same call | **`bridge.tsd`** 4,953,965 bytes | `9881487F8D065885390D4B774B5C5F56B098118EB2E7B6F7AE006AF2A2AD2028` |
| PR3 | the route's ZoneTemperature, persisted before the bridge ran | `zt.tsv` | `204C182A21E6A27CD59E223EE167E19D4BEBF8F73AC9930EECA4131752A49C12` |
| PR3 | the bridge's ResultantTemperature | `resultant.tsv` | `CC20A8ADEA64AAA99A44F0C90BD987E6E8D927B94D3F3911199B2D97EFBA2D6D` |

Phase C read back independently (`PR3-phase-c.log`): IZAM count 0; 30 internal conditions, 0 with a
non-zero `ticV`; `ticI` present on 30 of 30, non-zero on 9. The source TBD/TSD hashes differ from PR2's
`C:\TasOut\pr2r` run because this is a separate generation on a different machine; the intent they were
generated from is byte-identical, and every lineage statement below is against this run's hashes.

The design (identical to PR2's): three physical MVHR systems, eight mechanically served rooms,
`Corridor_1` not a ventilation-system member.

| system | rooms |
| --- | --- |
| MVHR-01 (Flat 1) | Studio 1_0, Bathroom_2 |
| MVHR-02 (Flat 2) | Bedroom 2_3, Kitchen_4, Ensuite_5 |
| MVHR-03 (Flat 3) | Bedroom 2_6, Kitchen_7, Ensuite_8 |

---

## 4. The bridge, room by room (measured)

Transcript `PR3-bridge.log`; table `PR3-bridge-rooms.tsv`. Names are for reading only - nothing on the path
reads one.

| room guid | name | TBD / TSD zone guid | ZT | IC | heating | cooling | max transfer delta | RT | finite | air dev max / mean K |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `407a5b91-8248-4992-abeb-000fdced341d` | Ensuite_5 | `{147C4969-043F-476E-A38A-E77113F55654}` | 8760 | 2 | 8760 | 8760 | 0 | 8760 | 8760 | 0.0010 / 0.0009 |
| `59709f58-9e71-4ad9-9ba1-a7b8c84c2fcc` | Kitchen_7 | `{C18844D4-F835-456F-9E39-C958F9A148EB}` | 8760 | 2 | 8760 | 8760 | 0 | 8760 | 8760 | 0.0010 / 0.0009 |
| `6011108b-0d83-4602-ba4f-1ac620e6219e` | Bedroom 2_6 | `{DCB2741B-F088-411D-9D49-BE8A49E5BD5E}` | 8760 | 2 | 8760 | 8760 | 0 | 8760 | 8760 | 0.0010 / 0.0008 |
| `75b28088-2415-44c0-bea8-3179b3475df0` | Studio 1_0 | `{4A05E23B-38DF-4636-AC4C-EAB26628341B}` | 8760 | 2 | 8760 | 8760 | 0 | 8760 | 8760 | 0.0010 / 0.0009 |
| `b62f342b-f86a-4c46-8feb-616bd976e525` | Ensuite_8 | `{E390237D-B1BB-4E81-8FD6-116019019178}` | 8760 | 2 | 8760 | 8760 | 0 | 8760 | 8760 | 0.0010 / 0.0008 |
| `cfd0fd57-5017-4003-9e6c-3d3d40468bd4` | Kitchen_4 | `{4ADC0A50-6E5F-4CB4-A2F4-D220A63188F0}` | 8760 | 2 | 8760 | 8760 | 0 | 8760 | 8760 | 0.0010 / 0.0009 |
| `e2c8a683-410d-42fd-92cb-bcf1e87e5a3a` | Bathroom_2 | `{C314BEA7-5214-4A13-85AF-F33E9FF7D695}` | 8760 | 2 | 8760 | 8760 | 0 | 8760 | 8760 | 0.0010 / 0.0009 |
| `e4d79f26-bd86-4334-831a-fe4048c0e6cc` | Bedroom 2_3 | `{9BCC98E0-5C1A-4E49-909D-34CDE64D9A21}` | 8760 | 2 | 8760 | 8760 | 0 | 8760 | 8760 | 0.0010 / 0.0008 |

`IC 2` = the zone's normal internal condition (day types Weekday/Saturday/Sunday/CDD) and its HDD one; both
are assigned to that zone alone, and both carry the series. The ninth TSD zone
(`7cd4bd07-3c41-4bde-89c6-5c8c427264cd`, Corridor_1) is not bridged: it is free-running, as in the source.

### The identity chain

```
Space.Guid -> NoIzamThermalSource.ZoneReference (SpaceParameter.ZoneGuid)  == TBD zone.GUID
           -> must equal the route's SystemVentilationBinding.Reference_ZoneLoad (TPD ZoneLoad.GUID)
           -> TBD copy: zone found in a one-pass guid index -> its internal conditions -> ticLL / ticUL
           -> second TSD: ZoneData.zoneGUID in a one-pass guid index -> resultantTemp
           -> ResultantTemperatureResult keyed by the same Space.Guid
```

Two independent statements of each room's zone (the source's zone map and the route's zone-load binding)
must agree or the bridge refuses. Guids are compared canonically (`Query.ZoneReferenceKey`): TAS answers
braced upper-case, the zone map carries bare.

---

## 5. Hour alignment and the one TAS convention the bridge depends on (measured)

**`TBD.profile.SetYearlyValues` silently shifts a 0-based array by one hour** (`PR3-probe-yearly.log`):

| write | slot 1 | slot 2 | slot 8759 | slot 8760 |
| --- | --- | --- | --- | --- |
| `SetYearlyValues(float[8760])`, element i = 1000+i | 1001 | 1002 | 9759 | **9759** (repeated) |
| `SetYearlyValues(float[8761])` | 1001 | 1002 | 9759 | 9760 |
| `yearlyValues[h] = 5000+h`, h = 1..8760 | 5001 | 5002 | - | 13760 |

TAS ignores element 0, and `GetYearlyValues()` answers `Single[*]` bounded 1..8760. The bridge therefore
writes slot by slot, `yearlyValues[h] = ZoneTemperature[h - 1]`, and reads every slot back.

**TSD annual index k is slot k + 1** - measured twice:

* self-imposition control (`PR3-probe-selfimpose.txt`, an older no-IZAM model, not the acceptance fixture):
  imposing a model's own free-running dry bulb on a copy of itself, the copy's air matched at 0 h shift with
  mean |d| 0.0010-0.0054 K per zone and at +-1 h with 0.07-0.52 K; its resultant reproduced the model's own
  within 0.07-0.15 K in the eight room zones (1.47 K in the corridor zone);
* acceptance run (`PR3-bridgecheck.log` section B): bridge air vs imposed series, mean |d| 0.0008-0.0009 K at
  0 h and 0.20-0.44 K at +-1 h, every room.

**Systems hours align with TBD hours** (`PR3-bridgecheck.log` section C): the Systems `ZoneTemperature` is
closest to the no-IZAM source's own free-running dry bulb at 0 h shift in all 8 rooms (e.g. Bathroom_2 1.3708
K at 0 h vs 1.4959 / 1.4642 at -1 / +1 h). The quantities differ physically - the MVHR route is not the
free-running building - so this is corroboration of alignment, not an equality.

---

## 6. The independent read-back (measured)

`bridgecheck` (`PR3-harness-Bridge.cs.txt`) opens the artifacts through TAS's own late-bound accessors,
sharing no code with the production bridge, and compares against the route's persisted `zt.tsv`:

* every slot of `ticLL` and `ticUL` on every internal condition of every bridged zone, 8 of 8 rooms
  8760/8760 exactly `(float)ZoneTemperature[h - 1]`; **MAX ABSOLUTE TRANSFER DELTA = 0**. The TAS Systems
  `ZoneTemperature` is itself single precision, so storing it in a single-precision TBD profile loses
  nothing: there is no measured precision limitation;
* the production resultant series equal the raw TSD `resultantTemp` read, max |d| 0;
* 8 of 8 rooms have 8760 finite `ResultantTemperature`.

---

## 7. Identity challenge (measured)

`rename` (`PR3-rename.log`): a copy of the source had **all 9 TBD zones renamed to `"Zone"`**; the route was
rebuilt through its public constructors from its **own persisted output** (`bindings.tsv`, `zt.tsv` - nothing
authored), pointing at the renamed copy; the production bridge ran again. Every room's resultant
temperature: **8 of 8 bit-identical, 8760/8760 hours, max |d| 0**; the whole `resultant.tsv` hashes
identically (`CC20A8AD...`). Renamed source `38A3F0CD...`, its bridge TSD `5B71CBCD...`.

---

## 8. Source immutability and simulation evidence (measured)

| | before | after |
| --- | --- | --- |
| source TBD | `FC5C677F7A6A13AAB6D11607BB62BBC07B97DC16A3A64923BBDEEB2AD2063537` | `FC5C677F7A6A13AAB6D11607BB62BBC07B97DC16A3A64923BBDEEB2AD2063537` |
| source TSD | `16CED2B62541EA77F078EA84D54B690EF1EBC77FC6A74FE1F76FB1919F94F1C1` | `16CED2B62541EA77F078EA84D54B690EF1EBC77FC6A74FE1F76FB1919F94F1C1` |
| bridge TBD as copied | `FC5C677F...` (== source) | `3FD9F2AE...` after thermostats + simulation |

The production bridge computes and gates on these itself (refuses if the copy is not byte-identical to the
source, or if the source moved while it ran). The harness hashes them again independently.

Second simulation, by PR2's `SimulationEvidence` (`SeparateOutputFile`): pre-existing TSD and error log
cleared before the call; run started `20:25:24.70Z`; TSD written `20:26:36.06Z`, 4,953,965 bytes (far above
the 1,024-byte stub threshold); no error log attributable to the run; `Completed = True`,
`ResultsReconciled = True` only after every room's series was read back and every gate passed.

---

## 9. Fail-closed contract (unit tests, COM-free)

`SAM.Analytical.Tas.TM59.Tests/ThermostatBridgeTests.cs`, 21 tests. The production plan, TBD writer and
TSD reader run for real against managed stand-ins; the one TAS behaviour a stand-in imitates
(`MeasuredYearlyProfile`) is the measured `SetYearlyValues` shift above, so the alignment test fails for a
writer that takes the bulk shortcut.

| refusal | where |
| --- | --- |
| missing / incomplete / non-annual route, source not the complete no-IZAM building | `ThermostatBridgePlan` |
| source and route disagree on a room's zone (ambiguous identity); two rooms on one zone; room with no zone | `ThermostatBridgePlan` |
| missing series, hole, NaN, infinity, value beyond single range | route gate + `ThermostatBridgePlan` |
| period other than hours 0..8759 (no pad / truncate / repeat) | `ThermostatBridgePlan` |
| copy path onto the source, non-`.tbd`, missing directory | `Create.ThermostatBridge` (before any copy) |
| copy not byte-identical to source; IZAM present in the copy | `Create.ThermostatBridge` |
| zone missing from / duplicated in the copy; IC shared with another zone; no IC / thermostat; radiant-sensing or proportional thermostat | `Modify.WriteThermostatBridge` |
| any thermostat slot not read back exactly as written | `Modify.WriteThermostatBridge` |
| simulation not evidenced (stale output, stub, error log, call threw) | `SimulationEvidence` |
| zone missing from / duplicated in the second TSD; short or non-finite resultant; air not held within tolerance | `Query.ReadThermostatBridge` |
| any room missing, extra or duplicated; any refusal upstream | `ResultantTemperatureResults` - payload empty |

No partial success: one room that cannot be bridged refuses the set, and a refused
`ResultantTemperatureResults` carries no series and no result path.

**Achieved-air tolerance** `ThermostatBridge.DefaultAchievedAirTemperatureTolerance = 0.5 K`: the canonical
run held every bridged room within 0.0010 K. In the self-imposition control (different model) the eight room
zones held within 0.22 K, while the corridor zone - also imposed there, heating emitter 10 % radiant, cooling
peaking at 6 kW - stood up to 2.23 K off in its worst hour (mean 0.0045 K). 0.5 K keeps a factor of two over
the room zones and stays inside TM59's 1 K criteria resolution; an excursion like the corridor's is exactly
what the gate exists to refuse. It is a judgement set against those measurements, stated as one.

**Scaling**: each TAS document is walked once into a guid index (5,000-zone stand-in: `GetZone` and
`GetZoneData` each called exactly 5,001 times for 12 bridged rooms); every room is then a dictionary probe.

---

## 10. PR1 / PR2 unchanged (measured on this build)

No PR1 or PR2 file is modified. On this build, PR2's own acceptance re-run on the same design and source
(`PR3-pr2-unchanged-acceptance.log`): items 4-15 **PASS**, TAS `"Done"`; directed topology
(`PR3-pr2-unchanged-topology.txt`) **14 legs PASS, 0 FAIL**, every native duty equal to the design duty -
**max absolute airflow delta 0 l/s**. The only production file touched outside the new bridge types is the
`SAM.Analytical.Tas.TPD.csproj`, which gains a non-embedded `Interop.TSD` reference.

---

## 11. Native TAS observations

1. `SetYearlyValues(float[])` ignores element 0 and repeats the last element (section 5). **Out of scope
   here, but it bears on existing code**: `SAM.Analytical.Tas/Modify/Update.cs` and `UpdateACCI.cs` write
   yearly profiles through it with 0-based 8760-element arrays, which by this measurement shifts those
   profiles one hour. Not changed in PR3; raised as a separate follow-up.
2. SAM's TBD export gives every zone its own internal conditions (normal + HDD), each assigned to that zone
   alone, with thermostats `radiantProportion = 0`, `proportionalControl = 0`, `controlRange = 0`
   (`PR3-probe-dump.log`) - the air-temperature control the bridge requires. The bridge checks it and
   refuses rather than reconfigures if it is not so.
3. Zones sized `tbdDesignSizingOnly` with `maxCoolingLoad = 0` still delivered cooling in simulation
   (unbounded simulation capacity); `tbdNoSizing` wet rooms held the imposed series on the real chain too.
   The bridge does not rely on this: it measures the achieved air instead.
4. The bridge's COM session (write 8 rooms x 2 ICs x 2 profiles x 8760 slots with read-back, simulate) took
   ~77 s; the second simulation itself ~71 s.
5. Orphaned `TBD.exe` / `TSD.exe` COM servers remain after each harness process; they were cleared between
   runs.

---

## 12. Not measured / assumptions

* TM59 was **not** run on the bridge TSD here - that is PR4's (orchestration, Iteration 1a vs Iteration 3
  comparison). The bridge TSD's zones carry the source's zone guids, which is what the existing
  `SimulationSpaceMap` matches on, so the unchanged TM59 authority can read it; that consumption is PR4's to
  prove.
* Only the canonical full-year period was exercised (SAM-BIM/SAM#115: a 1-day TSD crashes `TSD.exe`; the
  bridge refuses any non-annual period anyway).
* The acceptance ran on one fixture. SAM-BIM/SAM#113 (TAS flow-sizing creation-order sensitivity) and
  SAM-BIM/SAM#114 (PR1 system scope on authored NV/UV/legacy MV models; the harness's recorded scoping step
  is unchanged from PR2) remain open and untouched.
* The legacy name-keyed `Modify.CalculateResultantTemperature` (Grasshopper's TPD-full route) is left as it
  was; the Iteration 3 route does not use it.

---

## 13. Artifact manifest

| purpose | absolute path | SHA-256 | tracked in git |
| --- | --- | --- | --- |
| fixture copy | `C:\TasOut\pr3\src\fixture.sam` | `A7E09A25...7E4B` | no |
| prepared design | `C:\TasOut\pr3\a2\prepared.sam` | `5DC91DB4...034B` | no |
| intent oracle | `C:\TasOut\pr3\a2\intent.tsv` | `4795BF4A...660B` | yes, as `PR2-reacc-intent.tsv` (identical) |
| no-IZAM source | `C:\TasOut\pr3\c1\src.tbd`, `src.tsd` | `FC5C677F...`, `16CED2B6...` | no |
| Systems document | `C:\TasOut\pr3\final\acc.tpd` | `FE46B283...D0CE` | no |
| bridge TBD / TSD | `C:\TasOut\pr3\final\bridge.tbd`, `bridge.tsd` | `3FD9F2AE...`, `9881487F...` | no |
| identity challenge | `C:\TasOut\pr3\rename\*` | see section 7 | no |
| bridge transcript | `C:\TasOut\pr3\final\bridge.log` | - | yes, `PR3-bridge.log` |
| room table | `C:\TasOut\pr3\final\bridge-rooms.tsv` | - | yes, `PR3-bridge-rooms.tsv` |
| independent read-back | `C:\TasOut\pr3\final\bridgecheck.log` | - | yes, `PR3-bridgecheck.log` |
| identity challenge | `C:\TasOut\pr3\rename\rename.log` | - | yes, `PR3-rename.log` |
| PR2 unchanged | `C:\TasOut\pr3\pr2check\acceptance.log`, `topology.txt` | - | yes, `PR3-pr2-unchanged-*` |
| no-IZAM read-back | `C:\TasOut\pr3\c1\phase-c.log` | - | yes, `PR3-phase-c.log` |
| convention probes | `C:\TasOut\pr3\probe\data\*` | - | yes, `PR3-probe-*` |
| harness | `C:\TasOut\pr3\h\*.cs` (PR2 re-acceptance harness + `Bridge.cs`) | - | `Bridge.cs`, `Program.cs` as `PR3-harness-*.txt`; the rest as the PR2 `*.cs.txt` archives |

No `.tbd`, `.tsd` or `.tpd` binary is tracked.

### Reproducing

```
pr3h prep   C:\TasOut\pr3\src\fixture.sam C:\TasOut\pr3\a2 Flats sizepartf
pr3h source C:\TasOut\pr3\a2\prepared.sam C:\TasOut\pr3\c1 "C:\Users\Public\Documents\Tas Data\Databases\CIBSE Weather 2021.twd" full
pr3h bridge C:\TasOut\pr3\a2 C:\TasOut\pr3\c1 C:\TasOut\pr3\final
pr3h bridgecheck C:\TasOut\pr3\final
pr3h rename C:\TasOut\pr3\final C:\TasOut\pr3\rename        (needs src-zones.txt copied into final\)
pr3h run    C:\TasOut\pr3\a2 C:\TasOut\pr3\c1 C:\TasOut\pr3\pr2check full
pr3h topo   C:\TasOut\pr3\pr2check\acc.tpd C:\TasOut\pr3\pr2check C:\TasOut\pr3\pr2check\topology.txt
```

One TAS operation per process; clear orphaned TAS COM servers between them.

---

## 14. Tests and build

| gate | result |
| --- | --- |
| `SAM_Tas.sln` Release, full `-t:Rebuild` | **0 errors**. Warning profile identical, code by code, to a clean rebuild of `5bec3a6d` except **one** extra `MSB3270` (processor-architecture mismatch): the new `Interop.TSD` reference in `SAM.Analytical.Tas.TPD.csproj`, the same warning that project already raises for `Interop.TBD` and `Interop.TPD`. No warning names a PR3 source file. |
| `ThermostatBridgeTests` (new, focused) | **21 / 21** |
| full `SAM.Analytical.Tas.TM59.Tests` | **882 / 882** (PR2's 861 + 21) |
| full `SAM.Analytical.Tas.Benchmark.Tests` | **16 / 16** |
| `git diff --check` | clean |
| SPDX + copyright header on every new `.cs` | 14 / 14 |

The tests were run against the final Release rebuild. One focused-test defect was found and fixed before
that: a guard-test case whose path legitimately passed the guards made the provider open a real TAS COM
session on the licensed machine; the case now uses a path refused before any copy, so the suite creates no
COM object.
