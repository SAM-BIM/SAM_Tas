# Part O Iteration 3 — Candidate B0 / Reference A parity decomposition

**Classification: B — no B0 implementation defect. The residual is a representation difference
between the legacy TBD/IZAM route and the explicit TAS Systems route.**

No production change was made in any repository as a result of this study. Iteration 3
FOUNDATION remains FROZEN and Candidate B0 is unchanged.

This document decomposes the frozen Iteration 3 A/B residual
(bias **+0.373 K**, RMSE **0.834 K**, max **2.909 K**) into its measured causes, using the
frozen licensed artefacts plus targeted licensed TAS read-backs and two short controlled
experiments. It does not tune B0 towards A, and it does not restate the acceptance.

---

## 1. Provenance — exactly what was analysed

Repository state, all four repositories clean and in sync with `origin` at the time of the study:

| repository | branch | SHA |
| --- | --- | --- |
| SAM-BIM/SAM | `sow/2026-Q3` | `553957dc61db876926a5599154770d7aa22f959f` |
| SAM-BIM/SAM_Systems | `sow/2026-Q3` | `a0395dc96ecb085c9b74c350c68234e00e005371` |
| SAM-BIM/SAM_Tas | `sow/2026-Q3` | `c1cc91981efdcbe6735c415feaa3e04f8e704998` |
| SAM-BIM/SAM_UI | `sow/2026-Q3` | `95d200051bc228e624ca108d5391e774b0aa9b02` |

The pairing analysed is the **post-DHW-template-fix production run**, i.e. the most recent
validated annual pairing, not an older one that merely has compatible file names:

- pairing record `C:\TasOut\pr9out\Flat-Iteration3.json`, schema `PartOIteration3Record:v1`
- **attempt (run) GUID `a28813f0-1811-464f-ab2b-3f0b53d70a11`**, written `2026-09-12 05:41:00Z`
- Reference A design fingerprint `207dbb4ced02f556`, scenario fingerprint `008beb0b6213be29`
- thermal case: `weather=Leeds_TRY | solar=TAS | days 1-365 | unmetHours=True | sizing=True |
  useWidths=False | updateConstructionLayersByPanelType=True`
- canonical fixture SHA-256 `A7E09A25AE29C7DBB4C690D747A96FCD9F110CA27FB4DC2ABE816755368D7E4B`

Artefact hashes (SHA-256, uppercase) as read for this study:

| role | path | bytes | SHA-256 |
| --- | --- | --- | --- |
| Reference A model | `C:\TasOut\pr9out\Flat.sam` | 175472 | `A58815CCAC02D791E113BA7B29C69E757E4CA8DDC3433375D3BF2F387D71AC50` |
| Reference A TBD | `C:\TasOut\pr9out\Flat.tbd` | 424707 | `EFFC4CAC8F6EEEFB26F6479A16F724665D0D6BAA62C6DE3B31B399C1DFE7B758` |
| Reference A TSD | `C:\TasOut\pr9out\Flat.tsd` | 17864876 | `E42B48C54939A4C0BDE076C40C314AA065E69EEDEBBB3A83D60E2234E569E3F3` |
| B0 no-IZAM source TBD | `C:\TasOut\pr9out\Flat-It3B.tbd` | 402234 | `F5458E7A700BD8FDB7B5B250F9AFD9FC932336297FD04ECF6F56C16F2BA82E8A` |
| B0 no-IZAM source TSD | `C:\TasOut\pr9out\Flat-It3B.tsd` | 15987792 | `E6BAA4E2FB72F07A017BA8683E2EA7D63A85456FA4196B3DB3215EBE9A3BE55F` |
| B0 Systems TPD | `C:\TasOut\pr9out\Flat-It3B.tpd` | 120259 | `80067D2468B697B21226EECFCCB19C48777EA05239DEBBE765BCA3847633FED2` |
| B0 bridge TBD | `C:\TasOut\pr9out\Flat-It3B-Bridge.tbd` | 2325212 | `6C9D54F853179B55AFCAAFDD8EBA62674D395B06757661CECDEE3F255568DA27` |
| B0 bridge TSD | `C:\TasOut\pr9out\Flat-It3B-Bridge.tsd` | 16360279 | `0BE6B3B693287F936CE88217B7F04F60FDE8E2BDE990F61DF9C8051C02B1580C` |
| B0 model | `C:\TasOut\pr9out\Flat-It3B-Bridge.sam` | 178608 | `2C593D2A9446FEB60F704B9DCBEBED8BEA2029B5B8A67B89F012FD26ED9A4245` |
| pairing record | `C:\TasOut\pr9out\Flat-Iteration3.json` | 12767 | `E953B7EF32F1443FFE2C389CC9236CAB29B851AE356C4DDDB6AD2BBAB278BFE8` |
| review (structured) | `C:\TasOut\pr9out\Flat-Iteration3-Review.json` | 32871 | `80751C07EBCE0E0B10B4C0B94287124E31EEE7A5513996F47F4DB76246ABC822` |

Every file was copied to a working directory and the copies re-hashed to the same values before
any COM call; the frozen artefacts themselves were never opened read-write.

### Resolved resource paths (the shadowing trap)

`SAM.Core` resource resolution prefers a user-profile override over the repository's own
`files/resources`. Both were checked and are identical for the template this route uses, so
nothing stale was analysed:

| template | path | SHA-256 |
| --- | --- | --- |
| `MV.json` (user-profile override) | `C:\Users\michal.dengusiak\Documents\SAM\resources\Analytical\Systems\SystemEnergyCentre\MV.json` | `b0160e2b175779e6d270508feebc8821677841c997f12efbfcf95009ebf8f56e` |
| `MV.json` (repository) | `SAM_Systems\files\resources\Analytical\Systems\SystemEnergyCentre\MV.json` | `b0160e2b175779e6d270508feebc8821677841c997f12efbfcf95009ebf8f56e` |

### Were the annual simulations re-run?

**The annual TBD simulations were NOT re-run.** Both annual TSDs were read as frozen, and the
derived dataset reproduces every published statistic exactly (§3).

Two **short licensed TAS re-computations on copies** were needed, because TAS does **not**
persist plant/Systems results in the `.tpd` — reading a saved TPD answers
`NullReferenceException` for every component result series:

1. `ISystem.Simulate(1, 8760, 0)` per air system (≈2–4 s each) to recover B0's native
   `ZoneTemperature`, once as a control and once with one flag changed (§6).
2. `IPlantRoom.SimulateEx(1, 8760, 15, …, Load|Pipe|Duct|SimEvents|Cont, 1, 0)` (≈6 s) to
   recover the per-duct hourly temperature, flow, humidity and enthalpy that
   `ISystem.Simulate` does not populate (§5). This is the reader PR5A Phase 0 recorded as
   missing; it works, and the calls are recorded here.

Both run against the same frozen no-IZAM TSD, and both reproduce the frozen B0 room
temperatures:

| re-computation | vs frozen B0, pooled RMSE | max | at the six critical hours |
| --- | ---: | ---: | ---: |
| `ISystem.Simulate` per air system (the production call) | **0.000874 K** | 0.001001 K | ≤ 0.001 K |
| `IPlantRoom.SimulateEx` over the whole plant room | 0.010033 K | 0.688 K | ≤ 0.001 K |

The per-system control is within the bridge's own thermostat resolution everywhere. The
plant-room call, which additionally preconditions and couples the liquid side, agrees to within
0.01 K in 98.0 % of room-hours and to ≤ 0.001 K at every critical hour examined; the handful of
larger hours do not touch any conclusion drawn here, all of which rest on annual statistics and
on structural facts (duct temperature identity to 0.000000000 K, constant design flows,
stratification magnitude).

---

## 2. Identity — how A and B rooms were matched

Matching is by **SAM space GUID**, never by name. The two thermal models carry different TAS
zone GUIDs for the same room; the space GUID is the only common key:

| SAM space GUID | room | Reference A zone GUID | Candidate B0 zone GUID |
| --- | --- | --- | --- |
| `e2c8a683-410d-42fd-92cb-bcf1e87e5a3a` | Bathroom_2 | `{670A414D-…}` | `{D21605BA-…}` |
| `75b28088-2415-44c0-bea8-3179b3475df0` | Studio 1_0 | `{707AA364-…}` | `{E5783B26-…}` |
| `e4d79f26-bd86-4334-831a-fe4048c0e6cc` | Bedroom 2_3 | `{97FD2A81-…}` | `{60DD3287-…}` |
| `407a5b91-8248-4992-abeb-000fdced341d` | Ensuite_5 | `{847F6C37-…}` | `{300374AB-…}` |
| `cfd0fd57-5017-4003-9e6c-3d3d40468bd4` | Kitchen_4 | `{B011E45B-…}` | `{C05F1719-…}` |
| `6011108b-0d83-4602-ba4f-1ac620e6219e` | Bedroom 2_6 | `{1934DE14-…}` | `{4A194510-…}` |
| `b62f342b-f86a-4c46-8feb-616bd976e525` | Ensuite_8 | `{85268D3A-…}` | `{DDB75D19-…}` |
| `59709f58-9e71-4ad9-9ba1-a7b8c84c2fcc` | Kitchen_7 | `{FC8BA8EF-…}` | `{7DBFC58D-…}` |

Room names agree on both sides and were used only as a cross-check. Zone volumes and floor
areas are identical room-for-room in both TBDs (300/1464/100/420/300/120/420/300/120 m³).

Hour convention throughout: **0-based hour of year, 0 … 8759**, built from
`ZoneData.GetDailyZoneResult(day 1..365)` as `h = (day − 1) × 24 + hour_of_day`.

---

## 3. The dataset, and reproduction of the frozen result

`hourly-dataset.csv` carries **70 080 rows** (8 bound rooms × 8 760 h), all finite, with
per-row: hour, room, dwelling, space GUID, both zone GUIDs, outdoor dry bulb, and — for
Reference A, Candidate B0, and the free-running no-IZAM source — air temperature, mean radiant
temperature, resultant temperature, plus the ventilation/infiltration/air-movement/AHU gain
series and the internal-gain series.

It is 30 MB, so it is **not** committed here (this directory is text evidence only). It is
stored beside the acceptance artefacts and hashed in `PARTO-B0-PARITY-artefacts.csv`:

```
C:\TasOut\b0w\out\hourly-dataset.csv
30345875 bytes
sha256 9689CECB55AA5FDE2E0EB8F17A857B861E7A5A439A1B09126697A54300040B13
```

The generating harness and analysis scripts are committed as `PARTO-B0-harness-*.cs.txt`
and `PARTO-B0-analysis-*.py.txt`, so the dataset is reproducible from the frozen artefacts.

**Reproduction check.** Recomputed from the raw TSDs, the derived dataset gives

```
8 rooms, 70080 values:  bias +0.3726 K   RMSE 0.8335 K   max 2.9086 K in Ensuite_8 at hour 5491
```

and every per-room figure in the frozen acceptance table to the published precision, including
the `>26 °C` counts `4/16, 13/12, 3/3, 0/0, 4/4, 2/3, 0/0, 4/4`. Nothing in this study rests on
a re-derivation that disagrees with the frozen record.

**Control.** Re-simulating the Systems route on a copy reproduces the frozen B0 room
temperatures to **≤ 0.001001 K** (mean |diff| 0.00086 K) — which is exactly the bridge's own
thermostat resolution, so the experiment platform is sound and the frozen B0 is deterministic.

---

## 4. The two route topologies, read natively

### Reference A — legacy TBD/IZAM

`Flat.tbd` carries **12 zones**: the 9 real rooms plus three **MVHR plant zones**
(`MVHR-01/02/03`, 18 m³ / 9 m² each) that exist only in the thermal model, and **17 IZAMs**.

Those plant zones are not authored geometry — `Flat.sam` has exactly 9 spaces. They are written
by `SAM.Analytical.Tas.Modify.UpdateIZAMs`, which creates one 3 × 3 × 2 m box per
`AirHandlingUnit` (see `PlantZoneIdentity`, whose XML documentation names the same box) so that
the legacy route has somewhere to stage the `FROM OUTSIDE` air movement before distributing it.
That staging zone is a full TAS thermal zone with its own heat balance, which is why Reference A's
supply air is not outdoor air (§ gate 5). The IZAM chain is:

```
OUTSIDE -> MVHR-01 -> Studio 1_0 -> {extract to outside, transfer -> Bathroom_2 -> extract}
OUTSIDE -> MVHR-02 -> Bedroom 2_3 -> Kitchen_4 -> {extract, transfer -> Ensuite_5 -> extract}
OUTSIDE -> MVHR-03 -> Bedroom 2_6 -> Kitchen_7 -> {extract, transfer -> Ensuite_8 -> extract}
```

Full dump: `PARTO-B0-refA-izam-topology.txt`.

### Candidate B0 — explicit TAS Systems

`Flat-It3B.tbd` carries **9 zones and `IZAM count = 0`** (dump:
`PARTO-B0-noizam-source-topology.txt`). The ventilation lives in `Flat-It3B.tpd`, whose
component graph (dump: `PARTO-B0-tpd-topology.txt`) is:

```
Junction Fresh Air -> Fresh Air Fan -> Damper -> SystemZone(supply room)
   -> Damper -> SystemZone(kitchen) -> Part O Branch Junction
        -> Damper(extract) -> Junction Return -> Return Air Fan -> Junction Exhaust Air
        -> Damper(transfer) -> SystemZone(wet room) -> Damper -> Part O Branch Junction
```

Per system: 3 Junctions + 2 Fans + Dampers + SystemZones + 2 branch Junctions. **There is no
exchanger, no heating coil and no cooling coil anywhere in any of the three air systems.**
Both fans: `HeatGainFactor = 0`, `OverallEfficiency = 1`, `ControlType = FixedSpeed`,
`DesignFlowType = tpdFlowRateAllAttachedZonesFlowRate`.

**The directed graph is the same in both routes**: same three units, same eight rooms, same
three supply legs, six extract legs and five transfer legs, same source room on every transfer
leg. The reference and the candidate disagree about *thermodynamics*, not about topology.

---

## 5. Exit-gate answers, measured

### 1. Is there any remaining hourly alignment defect? — **No.**

A(t) was compared with B(t+k) for k ∈ {−3 … +3}, separately for air temperature and
resultant temperature, pooled and per room (no circular wrap; the overlapping window only).

| k | RMSE, A air vs B ZoneTemperature | RMSE, A vs B resultant | correlation (resultant) |
| ---: | ---: | ---: | ---: |
| −3 | 1.2531 | 1.0514 | 0.97373 |
| −2 | 1.1475 | 0.9600 | 0.98057 |
| −1 | 1.0407 | 0.8756 | 0.98634 |
| **0** | **0.9840** | **0.8335** | **0.98900** |
| +1 | 1.0795 | 0.9006 | 0.98466 |
| +2 | 1.2079 | 1.0003 | 0.97759 |
| +3 | 1.3193 | 1.0977 | 0.96997 |

`k = 0` is the minimum for **all 8 rooms and both quantities**, monotonically worse either
side, with correlation peaking at `k = 0`. The `k = ±1` penalty is small (0.834 → 0.876/0.901),
which is itself evidence that the residual is a level/dynamic difference and not a shift
artefact. The SAM_Tas #52 yearly-profile fix is the only alignment defect there was.
(`fig1-rmse-vs-shift.png`, `PARTO-B0-shift-analysis.csv`.)

### 2. How much of the difference exists at A-air vs B ZoneTemperature? — **All of it, and more.**

### 3. How much does the temporary ResultantTemperature bridge add? — **None; it subtracts.**

| comparison | bias | RMSE | MAE | max |
| --- | ---: | ---: | ---: | ---: |
| A air vs B0 `ZoneTemperature` (before the bridge) | +0.3853 | **0.9840** | 0.8010 | 4.1304 |
| A MRT vs B0 MRT | +0.3599 | 0.7101 | 0.6037 | 1.8805 |
| A resultant vs B0 resultant (after the bridge) | +0.3726 | **0.8335** | 0.6889 | 2.9086 |

Three measured facts settle this:

- `resultantTemp = (dryBulbTemp + MRTemp) / 2` holds to `max |diff| = 0.000001 K` in **all
  three** documents (Reference A, the no-IZAM source, and the bridge output). The bridge is not
  synthesising a resultant temperature; TAS is.
- The bridge's achieved air temperature equals the Systems `ZoneTemperature` to
  **≤ 0.001001 K** per room (mean 0.00086 K) — the declared thermostat tolerance.
- The RMSE **falls** from 0.9840 K to 0.8335 K across the bridge, because the second half of
  the resultant temperature (the MRT) is re-solved by TBD against the same fabric and damps the
  air-side difference (MRT RMSE 0.710 K vs air RMSE 0.984 K).

**The temporary bridge is not a material contributor to the residual** (gate 13). It faithfully
transports the Systems air temperature and lets TAS compute the radiant half. Replacing it with
a native Systems ResultantTemperature would remove at most the 0.001 K thermostat tolerance —
and would probably *increase* the reported difference, since the bridge's attenuation would go
with it.

### 4. Is B0's supply temperature effectively outdoor temperature? — **It is outdoor temperature, exactly.**

### 5. Does any hidden TAS component condition the B0 supply? — **No.**

Read natively off the fresh-air duct after `SimulateEx`, over all 8 760 h:

| unit | `max |T_freshair − T_outdoor|` |
| --- | ---: |
| MVHR-01 | **0.000000000 K** |
| MVHR-02 | **0.000000000 K** |
| MVHR-03 | **0.000000000 K** |

Ruled out individually: no `Exchanger` component exists in any of the three systems (so no
exchanger effect and nothing for `ExchCalcType`/`SetpointMethod`/`BypassFactor` to act on); both
fans state `HeatGainFactor = 0` so no fan heat; no `HeatingCoil` or `CoolingCoil` component
exists; no zone setpoint is active (`FlowRate`/`FreshAir` are the design values, no
`TemperatureSetpoint` profile drives anything); the plant room couples only through the DHW and
heating/cooling circuits, which touch no air system here. `PARTO-B0-duct-results.txt`.

**Reference A's supply is *not* outdoor air.** It is the air in the 18 m³ `MVHR-0n` plant zone:

| unit | mean (T_plantzone − ODB) | RMSE | max | Jan | Jul | Aug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| MVHR-01 | −0.0020 K | **1.468 K** | 6.32 K | +1.023 | −1.181 | −1.245 |
| MVHR-02 | −0.0017 K | **0.848 K** | 3.71 K | +0.539 | −0.625 | −0.658 |
| MVHR-03 | −0.0017 K | **0.848 K** | 3.71 K | +0.539 | −0.625 | −0.658 |

The annual mean offset is ~0.002 K, so this contributes essentially no annual bias — but it is a
±1.5 K seasonal and hourly modulation of A's supply air that B0, correctly supplying outdoor
air, does not have. It is the dominant driver at the worst hour of the year (§ gate 11).

### 6. Are the terminal design airflows truly equal? — **Yes.**

All **43** duct flow series are constant across the full 8 760 h at their design value, and each
unit is balanced:

| unit | fresh air in | exhaust out | balanced |
| --- | ---: | ---: | --- |
| MVHR-01 | 30.0 l/s | 30.0 l/s | yes |
| MVHR-02 | 63.0 l/s | 63.0 l/s | yes |
| MVHR-03 | 63.0 l/s | 63.0 l/s | yes |

Per leg: supply 30 / 63 / 63; transfer 8 (Studio→Bathroom), 63 (Bedroom 2_3→Kitchen_4),
8 (Kitchen_4→Ensuite_5), 63 (Bedroom 2_6→Kitchen_7), 8 (Kitchen_7→Ensuite_8); extract
22 / 8 / 55 / 8 / 55 / 8. Identical to the design terminals the frozen record states, and
identical to Reference A's IZAM profile flows (confirmed in gate 8 below by hand-calculation
against A's own native gain series).

### 7. Is there any duplicate ventilation or residual IZAM/ticV? — **No.**

| check | Reference A | B0 no-IZAM source | B0 bridge |
| --- | --- | --- | --- |
| `IZAM` objects in the TBD | 17 | **0** | (copy of source) |
| `airMovementGain`, all 8 rooms, all 8760 h | −812.5 … +116.8 W | **0.000000000 W** | **0.000000000 W** |
| `AHUGain`, all rooms | 0 | 0 | 0 |
| `ventilation` array, all rooms | 0 | 0 | 0 |
| `ticV`, all zones | 0 | 0 | 0 |
| `infiltration` (ach) | per room | identical | `max|A − B| = 0.000000000` |

Internal gains are identical between the two building models: `max |A − B|` is **0.000000** for
occupant sensible gain, equipment sensible gain and solar gain in every room. So is every
weather series (outdoor dry bulb, global and diffuse radiation, wind speed, external humidity):
`max |A − B| = 0.000000000`.

### 8. What thermally happens to transfer air in Candidate B0?

### 9. Does the explicit transfer graph explain the wet-room residuals? — **Yes, most of them.**

Air **entering** each room, annual mean, read off the native ducts and compared with the
Reference A equivalent (A's plant zone for a supply leg, A's source-room air for a transfer leg):

| room | leg | l/s | A inlet | B0 inlet | Δ inlet | B0 zone | B0 air **leaving** | B0 stratification |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Studio 1_0 | supply | 30 | 9.998 | 10.000 | **+0.002** | 15.973 | 18.225 | +2.25 K |
| Bedroom 2_3 | supply | 63 | 9.999 | 10.000 | **+0.002** | 14.939 | 15.110 | +0.17 K |
| Bedroom 2_6 | supply | 63 | 9.999 | 10.000 | **+0.002** | 14.879 | 15.051 | +0.17 K |
| Kitchen_4 | transfer ← Bedroom 2_3 | 63 | 14.416 | 15.110 | **+0.693** | 16.358 | 16.681 | +0.32 K |
| Kitchen_7 | transfer ← Bedroom 2_6 | 63 | 14.377 | 15.051 | **+0.674** | 16.367 | 16.682 | +0.32 K |
| Ensuite_5 | transfer ← Kitchen_4 | 8 | 15.353 | 16.681 | **+1.327** | 17.378 | **35.880** | +18.5 K |
| Ensuite_8 | transfer ← Kitchen_7 | 8 | 15.497 | 16.682 | **+1.186** | 17.446 | **36.028** | +18.6 K |
| Bathroom_2 | transfer ← Studio 1_0 | 8 | 15.682 | 18.225 | **+2.544** | 18.490 | **30.752** | +12.3 K |

The mechanism is `SystemZone.DisplacementVent`, which is **true (`-1`) on all eight B0 zones**,
inherited from the shipped `MV.json` zone prototype. TAS's displacement model treats the zone as
stratified: the reported `ZoneTemperature` is the occupied-level temperature and the air handed
to the next component is the upper-layer temperature. Because the extract/transfer flow is the
only carrier of the zone's whole sensible gain, an 8 l/s wet-room outlet reaches up to **79.8 °C**
while the room itself reports 17.4 °C.

Reference A's IZAM route has no such concept: air transfers at the source room's own fully-mixed
temperature. So on the three supply legs the two routes deliver **identical** air (to 0.002 K),
and on the five transfer legs B0 hands on air that is 0.67 – 2.54 K warmer in the annual mean
(and 15 – 18 K warmer at the worst hours). That, plus the compensating stratification in the
receiving room, is the whole wet-room and kitchen signature. `fig5-inlet-air.png`.

**Reference A's IZAM physics was independently verified**, so this is a real difference and not
a misread of A: hand-calculating `Q = ρ·c_p·V̇·(T_source − T_room)` from A's own hourly
temperatures reproduces A's native `airMovementGain` series with correlation **≥ 0.99979** in
every room, at an implied `ρ·c_p` of **1224.3 – 1224.9 J/m³K** — i.e. A supplies at the plant-zone
temperature, transfers at the source-room temperature, and extracts at room temperature, with
nothing else in between.

### 10. Why does Bathroom_2 have 12 additional `>26 °C` hours? — **Narrow-margin crossings on a 2.5 K warmer transfer inlet.**

All 16 of B0's hours are a **strict superset** of A's 4 (there are **0** hours where A exceeds
26 °C and B0 does not). For the 12 extra hours:

- B0 exceeds 26 °C by **0.076 – 0.850 K** (mean 0.466 K);
- Reference A is already at **25.38 – 25.99 °C** (mean 25.64 K) at those same hours;
- 11 of the 12 are July, one is June, all in the hours 4339 – 4748 band;
- the inlet at those hours is Studio 1_0's stratified outlet at **31.6 – 42.2 °C** in B0 versus
  Studio 1_0's mixed room air at **22.3 – 26.0 °C** in A, on 8 l/s.

Threshold sensitivity confirms the same picture: A has 10 hours in the 25.5 – 26.0 °C band, B0
has 15. The criterion limit is 262 h; both routes pass by a factor of 16 and the TM59 verdict is
unchanged. `fig4-bathroom2-threshold.png`, `PARTO-B0-bathroom2-extra-hours.csv`.

### 11. What happens at hour 2968? — **The annual worst hour, and it is A's plant zone lagging, not B0.**

Hour 2968 is **rank 1 of 8 760** for the pooled mean `|B − A|` resultant difference
(1.7895 K — the annual maximum). It is day 124 (early May), 16:00, outdoor 21.1 °C (only the
290th warmest hour of the year), global radiation 347 W/m².

At that hour A's plant zones are still discharging the heating season while outdoor air has jumped:

| | ODB | A plant zone | A supply deficit |
| --- | ---: | ---: | ---: |
| MVHR-01 | 21.10 | 17.43 | **−3.67 K** |
| MVHR-02 / 03 | 21.10 | 19.05 | **−2.05 K** |

So Reference A is supplying its bedrooms with 19.05 °C air at 63 l/s while B0 correctly supplies
21.10 °C outdoor air — into rooms sitting at 17.5 – 17.7 °C. The supply is *warming* both rooms,
and 2 K more so in B0. The four rooms on the 63 l/s legs (Bedroom 2_3, Bedroom 2_6, Kitchen_4,
Kitchen_7) therefore all record their annual maximum at exactly this hour, as the frozen record
states. `fig3-critical-windows.png`, `PARTO-B0-critical-hours.csv`.

### 12. Why were 5491 and 5299 both reported? — **A 0.0003 K tie.**

Exact values for Ensuite_8:

| hour | A resultant | B0 resultant | `|B − A|` |
| ---: | ---: | ---: | ---: |
| **5491** | 23.073898 | 20.165325 | **2.908573** |
| 5299 | 23.339245 | 20.430975 | 2.908270 |
| 4819 | 24.030121 | 21.126686 | 2.903435 |

`|Δ(5491)| − |Δ(5299)| = 0.000303 K`. **Both round to 2.909 K**, and 4819 rounds to 2.903 —
three near-tied maxima in the same recurring phenomenon (August/July evenings, hour-of-day 19,
a wet room on an 8 l/s transfer leg). It is a tie/presentation artefact, not a different
pairing, a different indexing convention, a timestamp conversion, or a stale artefact.

**Canonical convention going forward: 0-based hour of year 0 … 8759; the argmax of the frozen
pairing is Ensuite_8 at hour 5491 with 2.908573 K.** The 5299 report is the second-ranked hour
of the same room, 0.0003 K behind.

### 13. Is the temporary bridge a material contributor? — **No.** See gates 2/3.

### 14. Is there any legitimate B0 correction that brings it closer to A? — **No.**

Every candidate was tested against native evidence, not argued:

| candidate | verdict | evidence |
| --- | --- | --- |
| residual time shift | ruled out | `k = 0` is the minimum for all rooms and both quantities |
| supply-air conditioning | ruled out | fresh-air duct `= ODB` to 0.000000000 K |
| DesignAirFlow mismatch | ruled out | all 43 duct flows constant at design, all units balanced |
| duplicate ventilation / residual IZAM / `ticV` | ruled out | `airMovementGain ≡ 0`, `IZAM count = 0`, `ticV = 0`, infiltration byte-identical |
| wrong schedule | ruled out | fans operable 8760/8760 at factor 1.0; every duct flow constant over the year |
| identity / result mapping | ruled out | GUID-keyed both sides; control rerun reproduces to 0.001 K |
| bridge defect | ruled out | achieved air ≡ ZoneTemperature to 0.001 K; `res = (air+MRT)/2` to 1e-6 |
| weather / gains mismatch | ruled out | all weather and internal-gain series identical to 0.000000 |
| hidden TAS default | **found, and it is not correctable towards A** | `DisplacementVent` — below |

**The one inherited default, measured both ways.** `SystemZone.DisplacementVent = true` comes
from the `MV.json` zone prototype, not from the SAM analytical design and not from a decision in
the SAM_Tas route (the Review already states this). Clearing it on all eight zones and re-running
the Systems simulation on a copy:

| | pooled bias vs A air | pooled RMSE | Bathroom_2 | Ensuite_5 | Ensuite_8 | Studio 1_0 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `DisplacementVent = true` (**frozen B0**) | **+0.3856** | **0.9838** | +0.532 | −0.096 | −0.542 | +0.293 |
| `DisplacementVent = false` | +0.9856 | 1.2815 | +1.498 | +1.505 | +1.285 | +0.535 |

With the flag cleared, the air leaving each zone equals its `ZoneTemperature` exactly — i.e. the
fully-mixed behaviour Reference A has — and yet **parity gets substantially worse**: the pooled
bias more than doubles and the RMSE rises 30 %. Turning the flag off would therefore be tuning
*away* from A, not a correction. **There is no legitimate change to B0 that moves it closer to
Reference A.**

### 15. What residual is irreducible?

The frozen B0 statistics stand unchanged:

```
8 rooms, 70 080 hourly values
resultant temperature   bias +0.3726 K   RMSE 0.8335 K   max 2.9086 K (Ensuite_8, hour 5491)
air / ZoneTemperature   bias +0.3853 K   RMSE 0.9840 K   max 4.1304 K
TM59 criterion outcome differences: 0 of 8
```

Its structure, measured:

**(a) Season, not level.** Removing the pooled mean bias only takes the resultant RMSE from
0.834 K to 0.746 K; removing a per-room bias takes it to 0.659 K. The residual is dynamic. The
annual `+0.373 K` is almost entirely a heating-season effect:

| | Jan | Feb | Mar | Apr | May | Jun | Jul | Aug | Sep | Oct | Nov | Dec |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| bias | +0.744 | +0.817 | +1.068 | +0.823 | +0.503 | −0.296 | −0.392 | −0.392 | −0.378 | +0.174 | +0.900 | +0.929 |
| RMSE | 0.849 | 0.904 | 1.136 | 0.924 | 0.731 | 0.605 | 0.691 | 0.722 | 0.644 | 0.567 | 0.995 | 1.017 |

By outdoor condition:

| regime | room-hours (of 70 080) | bias | RMSE | max |
| --- | ---: | ---: | ---: | ---: |
| ODB ≥ 20 °C | 3 480 | **−0.048** | **0.490** | 2.493 |
| 10 ≤ ODB < 20 °C | 31 648 | +0.001 | 0.777 | 2.909 |
| ODB < 10 °C | 34 952 | **+0.751** | 0.907 | 2.344 |

**In the warm weather that Part O actually assesses, the pooled bias is −0.048 K and the RMSE is
0.490 K.** The zero TM59 outcome differences are not luck. Day/night is immaterial (+0.358 vs
+0.402 K).

**(b) Two mechanisms, both structural.**

1. **Reference A's MVHR plant zone** (§ gate 5). A conditions its supply air by passing it
   through an 18 m³ thermal zone; B0 supplies genuine outdoor air. Annual-mean effect ≈ 0, but
   ±1.5 K RMS hourly and a clear seasonal swing, and it is the whole story at hour 2968.
2. **TAS displacement zones on the transfer legs** (§ gate 8/9). B0's five transfer legs hand on
   stratified upper-layer air, 0.67 – 2.54 K warmer in the mean than A's fully-mixed transfer.
   This is the wet-room and kitchen signature and the Bathroom_2 threshold hours.

**(c) The residual that neither mechanism explains** is the seasonal one, and it is visible in
its cleanest form on the three supply rooms, which receive **identical** air (to 0.002 K) at
**identical** flow from **identical** fabric with **identical** gains and weather — and still sit
**+0.29 to +0.52 K** above Reference A (**+0.53 to +0.55 K** with the displacement flag cleared).
The measured signature points at the route's defining approximation: Candidate B0 solves the
building **first**, free-running with no ventilation at all, and replays those zone loads into
the TAS Systems zone solver, which then computes the achieved air temperature. The temperature
at which the loads were evaluated is not the temperature finally achieved:

| | Jan | Feb | Mar | Apr | May | Jun | Jul | Aug | Sep | Oct | Nov | Dec |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| free-running source − B0 achieved (K) | 1.883 | 1.940 | 1.858 | 1.811 | 1.503 | 1.150 | 1.020 | 0.936 | 1.345 | 1.738 | 1.988 | 1.912 |
| B0 − A, air (K) | 0.846 | 0.859 | 1.124 | 0.861 | 0.499 | −0.350 | −0.453 | −0.442 | −0.417 | 0.194 | 0.961 | 0.972 |

Across the twelve months these track each other at **r = 0.932**: the residual is largest exactly
when the free-running trajectory is furthest from the achieved one (winter, when ventilation
cooling is large relative to gains) and smallest when the two trajectories converge (high summer).
This is inherent to the two-pass TBD→TPD route, not to anything SAM_Tas chose; Reference A has no
equivalent because TBD co-solves fabric, room air and IZAM transfer in one time step.

### 16. Is that residual acceptable? — **Yes, on the evidence.**

- In the assessed regime (ODB ≥ 20 °C) the pooled bias is −0.048 K and RMSE 0.490 K.
- Correlation between the two routes is 0.989 on resultant temperature, hour for hour.
- The mechanisms are identified, native, and quantitatively reproducible from TAS's own results.
- Every `>26 °C` disagreement is a narrow-margin crossing in a room passing by 16×; **0 of 8**
  TM59 criterion outcomes differ.
- There is no configuration of B0 that is closer to A without abandoning either the shipped
  topology template or the explicit Systems route itself.

---

## 6. Root-cause ranking

Contribution estimated against the pooled resultant RMSE of 0.8335 K.

| # | cause | evidence for | evidence against | est. contribution | classification | confidence |
| ---: | --- | --- | --- | --- | --- | --- |
| 1 | Two-pass load evaluation: B0's zone loads come from a free-running no-IZAM TBD and are replayed into the Systems zone solver | supply rooms with identical inlet air, flow, fabric, gains and weather still differ by +0.29…+0.55 K; monthly (source − achieved) vs (B0 − A) correlate at r = 0.932; the whole seasonal signature follows it | TAS internals are not observable, so the coupling is inferred from its signature, not read | **≈ 0.4–0.5 K of the bias; most of the seasonal RMSE** | expected difference (route-defining) | high on the signature, medium on the exact mechanism |
| 2 | TAS displacement zones hand on stratified air on the five transfer legs | inlet Δ +0.67…+2.54 K measured on the native ducts; leaving air up to 79.8 °C at 8 l/s; clearing the flag makes leaving air ≡ zone temperature exactly | clearing it makes pooled parity *worse* (bias +0.386 → +0.986, RMSE 0.984 → 1.282), so it is not a correctable error | **most of the wet-room/kitchen per-room spread; all 12 Bathroom_2 hours** | expected difference (inherited template prototype) | high |
| 3 | Reference A's 18 m³ MVHR plant zone conditions A's supply air; B0 supplies true outdoor air | A plant zone − ODB: RMSE 1.468/0.848 K, max 6.32 K, +1.0 K Jan / −1.2 K Aug; B0 fresh-air duct = ODB to 0.000000000 K | annual-mean offset is only −0.002 K, so it adds no annual bias | **dominant at the annual worst hour (2968); ≈ 0.1–0.2 K of pooled RMSE** | expected difference (legacy route artefact) | high |
| 4 | Temporary ResultantTemperature bridge | — | achieved air ≡ ZoneTemperature to ≤0.001001 K; `res = (air+MRT)/2` to 1e-6 in all three documents; RMSE *falls* 0.984 → 0.834 across it | **≤ 0.001 K; net negative** | not a contributor | high |
| 5 | Residual time shift | — | `k = 0` minimal for all 8 rooms and both quantities, correlation peaks at `k = 0` | **0** | ruled out | high |
| 6 | Supply-air conditioning in B0 (exchanger / fan heat / coil / setpoint / plant coupling) | — | no such component exists in the graph; fresh-air duct = ODB to 0.000000000 K; HGF 0, η 1 | **0** | ruled out | high |
| 7 | DesignAirFlow mismatch | — | all 43 duct flow series constant at design over 8760 h; every unit balanced | **0** | ruled out | high |
| 8 | Duplicate ventilation / residual IZAM / `ticV` | — | `airMovementGain ≡ 0.000000000`, `IZAM count = 0`, `ticV = 0`, `AHUGain = 0`, infiltration identical to 1e-9 | **0** | ruled out | high |
| 9 | Schedule mismatch | — | fans operable 8760/8760 at factor 1.0; duct flows constant all year | **0** | ruled out | high |
| 10 | Weather / internal-gain mismatch | — | all five weather series and all three gain series identical to 0.000000 | **0** | ruled out | high |
| 11 | Identity / result mapping | — | GUID-keyed both sides, names agree as a cross-check; control rerun reproduces frozen B0 to 0.001 K | **0** | ruled out | high |
| 12 | Numerical / indexing effects | 5491 vs 5299 is a 0.000303 K tie | no other indexing artefact found; one convention (0-based 0…8759) throughout | **0.0003 K, presentation only** | ruled out as a cause | high |

---

## 7. Recorded for PR5A (not acted on here)

PR5A Phase 0 recorded that `MV.json`'s zone prototype states
`DisplacementVentilation = true` and `MVRE.json`'s states `false`, and that the PR5A
materialisation must normalise the flag explicitly. This study measures the consequence of the
normalisation *direction* on the frozen control, which Phase 0 could not:

- **Candidate B0 runs with the flag `true`.** That is the frozen control.
- Normalising to `false` moves B0's pooled air bias from **+0.386 K to +0.986 K** and its RMSE
  from **0.984 K to 1.282 K**, i.e. **away** from Reference A, with the wet rooms moving 1.6–1.8 K.
- So `MVRE` should be normalised **to `true`** to keep `MVRE@ε0 ≡ B0` (which is what Phase 0's
  Test A measured), and any proposal to normalise both to `false` would silently change the
  frozen B0 control and invalidate the comparison.

No production change was made for this, and nothing in PR #117 was touched.

---

## 8. Committed evidence

| file | content |
| --- | --- |
| `PARTO-ITERATION3-B0-PARITY-DECOMPOSITION.md` | this document |
| `PARTO-B0-PARITY-summary.csv` | machine-readable: pooled and per-room statistics, the full shift table, monthly statistics, weather/day-night regimes, supply-air measurements, the displacement experiment, the control rerun, the load-evaluation offset, and the argmax tie |
| `PARTO-B0-PARITY-artefacts.csv` | path, size and SHA-256 of every large generated artefact kept outside the repository, including the 70 080-row dataset and the five plots |
| `PARTO-B0-per-room-stats.csv` | per-room air / MRT / resultant statistics |
| `PARTO-B0-shift-analysis.csv` | every shift, pooled and per room, both quantities |
| `PARTO-B0-monthly-stats.csv` | per-room and pooled monthly bias / RMSE / max |
| `PARTO-B0-critical-hours.csv` | 1843, 2968, 4819, 5299, 5491, 6211 reconstructed for all 8 rooms |
| `PARTO-B0-bathroom2-extra-hours.csv` | the 12 B0-only `>26 °C` hours with inlet temperatures and margins |
| `PARTO-B0-top-hours.csv` | top-10 `|B − A|` hours per room |
| `PARTO-B0-refA-izam-topology.txt` | Reference A's 12 zones and 17 IZAMs, read natively |
| `PARTO-B0-noizam-source-topology.txt` | B0's 9 zones, `IZAM count = 0` |
| `PARTO-B0-tpd-topology.txt` | B0's full TPD component and duct graph with every stated property |
| `PARTO-B0-duct-results.txt` | native per-duct hourly flow/temperature/humidity/enthalpy after `SimulateEx` |
| `PARTO-B0-duct-results-displacement-off.txt` | the same with `DisplacementVent = 0` |
| `PARTO-B0-analysis-an1…an8.log` | the measured output of every analysis step |
| `PARTO-B0-harness-*.cs.txt`, `PARTO-B0-harness-B0P.csproj.txt` | the licensed-TAS read/simulate harness |
| `PARTO-B0-analysis-*.py.txt` | the analysis and plotting scripts |

Kept outside the repository (text-only convention here), hashed in
`PARTO-B0-PARITY-artefacts.csv`:

| file | bytes | SHA-256 |
| --- | ---: | --- |
| `C:\TasOut\b0w\out\hourly-dataset.csv` | 30345875 | `9689CECB55AA5FDE2E0EB8F17A857B861E7A5A439A1B09126697A54300040B13` |
| `C:\TasOut\b0r4\ducts.csv` | 47548435 | `5246048BE3CFDF870D9B02D5201A29E6A24EF52B78CCF9511BF9281835618A06` |
| `C:\TasOut\b0r1\zt-control.csv` | 1358828 | `01556EEF88417213F39FB092220CDB9F70890178C32CFEAEB59B209265B78C14` |
| `C:\TasOut\b0r2\zt-dv0.csv` | 1358304 | `C124CADF678B50FED09A514188745210E6C8F697431D9EC5028B470D0FEB656E` |
| `C:\TasOut\b0w\out\fig1-rmse-vs-shift.png` | 64769 | `13DA60B9F2299EBD2C0B6A186DD5134FD3357C3B0CD537163704D2DE8886F468` |
| `C:\TasOut\b0w\out\fig2-monthly.png` | 46772 | `78D3180E3AE82E711447D1F99C47AFFA7C095C935B70FE2A149CCB2976B9A145` |
| `C:\TasOut\b0w\out\fig3-critical-windows.png` | 211657 | `5BAA4B0846F6C906FB432267B12D6821815C94E3027C4610313AD6DC9DDBAA3C` |
| `C:\TasOut\b0w\out\fig4-bathroom2-threshold.png` | 106762 | `0D54D22DA7D7F0357C8F24DE36253B013D43E89E2CCBB649D6D8F8F08BDE326D` |
| `C:\TasOut\b0w\out\fig5-inlet-air.png` | 47656 | `999F1CF7A73D67E3C62391C2A318D7062A6D485DF2A55A41113F378D61E82C60` |

(The plots are regenerated by `PARTO-B0-analysis-plots.py.txt`; the dataset by the harness and
`PARTO-B0-analysis-an1.py.txt`.)

---

## 9. Outcome

**Classification B — no B0 implementation defect; the residual is a representation difference.**

- No production change in `SAM`, `SAM_Systems`, `SAM_Tas` or `SAM_UI`.
- Candidate B0 is byte-for-byte the frozen control; the frozen A/B statistics stand.
- Iteration 3 FOUNDATION remains FROZEN.
- SAM PR #117 was neither merged, modified, rebased nor built on; PR5A can resume from it.
