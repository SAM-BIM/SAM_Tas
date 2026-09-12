# Part O Iteration 3 PR5A — SAM_Tas slice licensed conversion evidence

Licensed TAS confirmation of the two new conversion behaviours this slice adds:
`ExchCalcType` writing/read-back on the air-side exchanger, and the
`SystemVentilationFanHeatGainPolicy` route option (`ClearToZero`/`FromSystemsGraph`) on
`Modify.GroundVentilationFans`. **No production model, template or B0 behaviour was changed by
this check** - it exercises the new conversion code directly against real TAS, on a small
purpose-built model, independent of the frozen canonical acceptance fixture.

---

## 1. Baseline

| repository | branch | SHA (at implementation) |
| --- | --- | --- |
| SAM-BIM/SAM | `sow/2026-Q3` | `b4a1283f8f5a805b637943fedc0b2a7f493d7b9a` |
| SAM-BIM/SAM_Systems | `sow/2026-Q3` | `5213ba9c0c7003bc38044ab83ae378e238bcb6ed` |
| SAM-BIM/SAM_Tas | this PR, branched from `sow/2026-Q3` `6c622309f6e368cf0116dfabbc20deec45bd2cba` | — |

TSD source reused (not regenerated): `C:\TasOut\b0w\Flat-It3B.tsd`, confirmed by hash to be the
canonical no-IZAM source already on record -

- bytes: 15,987,792
- SHA-256: `E6BAA4E2FB72F07A017BA8683E2EA7D63A85456FA4196B3DB3215EBE9A3BE55F`
- matches `PARTO-ITERATION3-B0-PARITY-DECOMPOSITION.md`'s recorded `Flat-It3B.tsd` exactly.

This TSD is used only to satisfy `Convert.ToTPD`'s `path_TSD` parameter (it loads TSD data onto
the `EnergyCentre` before the ventilation conversion runs); the check below does not depend on any
particular zone existing in it, and does not read simulation results from it.

## 2. What was tested, and why not the full A-D matrix

This is a **conversion-correctness check**, not the licensed operating-point matrix (A-D) or an
annual acceptance. Those need a fully zone-identity-matched model (`SpaceParameter.ZoneGuid`
stamped against a real TSD, a schedule threaded through Part O's own orchestration) and are SAM_UI
(Phase 5) / full-pipeline territory. What can be, and was, checked in isolation here: does the new
production code actually write and correctly read back the two properties this slice adds/changes,
against real TAS, in a real saved TPD document - not against a fake, and not merely "the code
compiles".

A small model was built directly against the real production entry points, in a throwaway harness
(`C:\TasOut\pr5a_h`, not part of any repository):

- one physical `AirHandlingUnit`, one MVHR `VentilationSystem`, two rooms (one supply-only, one
  extract-only) - built with `SAM.Analytical.Create` the same way `MechanicalVentilationTestModel`
  does in the SAM_Systems tests, just in an external process;
- the shipped, unmodified `MVRE.json` template;
- `MechanicalVentilationUnitSettings` on the one AHU: `HeatRecoverySensibleEfficiency = 0.83`,
  `SupplyFanPressure_Pa = 500`, `ExtractFanPressure_Pa = 480`, `FanOverallEfficiency = 0.6`,
  `SupplyFanHeatGainFactor = 1.0`, `ExtractFanHeatGainFactor = 0.5` - two **different** fan heat
  gain factors, deliberately, so a policy that silently ignored one fan or conflated the two would
  show up immediately;
- a constant 8760-hour 1.0 `YearlySchedule` (Part O's own continuous-operation convention) so the
  run reaches the fan/exchanger grounding steps rather than refusing earlier on schedule shape;
- `SAM.Analytical.Systems.Create.MechanicalVentilation(...)` (PR1/PR5A, unmodified by this check)
  materialised the graph;
- `SAM.Analytical.Tas.TPD.Create.SystemVentilationConversionContext(...)` built the intent, with
  `fanHeatGainPolicy` set explicitly per run;
- `SAM.Analytical.Tas.TPD.Convert.ToTPD(systemEnergyCentre, path_TPD, path_TSD, settings, context)`
  - this PR's own production entry point - ran the conversion, including
    `Modify.GroundVentilationFans` and the new `Modify.GroundVentilationExchangers`.

The run's zone identity was intentionally left unstamped (`SpaceParameter.ZoneGuid` never set), so
the overall `SystemVentilationConversionContext.IsReconciled` is `false` and `Converted` is
`false` - **expected and irrelevant here**: `Notes` and `Refusals` are populated by
`GroundVentilationFans`/`GroundVentilationExchangers` before the later zone-reconciliation check
runs, so the two properties under test are fully exercised and reported regardless of that later,
unrelated refusal.

## 3. Result 1 — `ExchCalcType` write + native read-back

Native TAS answered, immediately after `Convert.ToTPD(DisplaySystemExchanger, system)` wrote it and
before the document was saved (three independent runs, three different exchanger guids):

```text
NOTE: Exchanger {DCA21A77-7FB1-47CE-905D-0498D648675A} states calculation method tpdExchangerCalcSimple.
NOTE: Exchanger {A16BE1A8-9BB1-44DF-9618-018D3C2D23ED} states calculation method tpdExchangerCalcSimple.
NOTE: Exchanger {43E69C5E-2AD8-4D0F-8209-33860E5648D7} states calculation method tpdExchangerCalcSimple.
NOTE: Exchanger {A74A0BDE-2724-412A-B260-573FC94A10EC} states calculation method tpdExchangerCalcSimple.
```

`Modify.GroundVentilationExchangers` reads this back via `(dynamic)exchanger.ExchCalcType` (the
same late-bound pattern the sibling `Convert.ToSAM` liquid-exchanger/chiller conversions already
use for this exact native property) and refuses if it is not `tpdExchangerCalcSimple`. It was
`tpdExchangerCalcSimple` in every run. **`ExchCalcType` is a genuine, working, typed write and a
genuine, working, late-bound read** on the air-side `Exchanger` - not an assumption carried over
from the different native type (`WaterSourceChiller`/`SystemLiquidExchanger`) that already used
this pattern.

## 4. Result 2 — `SystemVentilationFanHeatGainPolicy`

Same model, same two fans (`SupplyFanHeatGainFactor = 1.0`, `ExtractFanHeatGainFactor = 0.5`),
converted twice, once per policy:

**`FromSystemsGraph`** - nothing forced; both figures preserved and read back distinctly:

```text
NOTE: Fan {03006338-B34D-455D-B6D3-504A4E67FDB7} … carries the systems graph's own heat gain factor (HeatGainFactor 0.5) …
NOTE: Fan {78F52DAE-B9E4-4900-94C8-32EBD5D74EC6} … carries the systems graph's own heat gain factor (HeatGainFactor 1) …
```

**`ClearToZero`** - the same model, same `ExtractFanHeatGainFactor = 0.5` in the source settings,
converted again with the B0 policy: both fans forced to, and read back as, exactly 0:

```text
NOTE: Fan {44A42D03-22B8-4F21-ADBD-ABB91F4A21D3} … adds no heat to the air stream (HeatGainFactor 0) …
NOTE: Fan {F8ABDC7A-5C1A-41F2-B4B7-F88741472C41} … adds no heat to the air stream (HeatGainFactor 0) …
```

**This is the regression check that matters most**: `ClearToZero` (the default, and what every
existing caller gets with no code change on their part) still forces every fan to 0 and still
reads it back to confirm - identical to the pre-PR5A behaviour - even though the very same source
settings this time stated a non-zero `ExtractFanHeatGainFactor`. The policy is a genuine gate, not
a no-op.

## 5. What this does not prove, and remains gated

- **Not the operating-point evidence A-D.** No simulation ran; no `ε`/setpoint/bypass semantics were
  exercised; no hand-calculation was checked against a simulated result. Those need a
  zone-identity-matched model and the full-year TSD/TPD hour-window route, and are the next step.
- **Not an annual acceptance.** No B-variant TM59 comparison, no per-layer attribution.
- **E1/E2 remain unsourced.** `0.83` above is a fixture value for this conversion check, exactly as
  every other PR5A fixture value in SAM/SAM_Systems has been - never presented as, or derived from,
  real MRXBOX data.
- The duplicate `ExchLatType` write this PR also removes (`Convert/ToTPD/Exchanger.cs`, previously
  written twice, identically, on lines that are now one) has no observable native effect either way
  - it is a code-quality fix, not a behaviour change, and is not separately evidenced here.

## 6. Reproduction

Harness source: `C:\TasOut\pr5a_h` (not part of any repository - a throwaway console app,
`net8.0-windows`, referencing the built `SAM`/`SAM_Systems`/`SAM_Tas` DLLs by `HintPath`, following
this repository's own `run-tas` skill conventions for a headless TAS harness). Built and run with
no TAS GUI process open, absolute paths throughout, one conversion per process invocation:

```text
PR5AH.exe convert <path_tpd> FromSystemsGraph 0.5
PR5AH.exe convert <path_tpd> ClearToZero      0.5
```
