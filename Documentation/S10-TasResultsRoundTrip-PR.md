# S10: Tas results round-trip validation

Task S10 of the SAM simulation roadmap (`SAM/documentation/SIMULATION_ROADMAP_Q4_2026.md`, row S10): SAM-imported Tas
results and the B1a document must equal an independent read of the TSD; defects F4-F6 (SAM_UI#239) fixed or explicitly
accepted; hour and peak definitions documented. Branch `feature/s10-tas-results-roundtrip`, base `sow/2026-Q4`
(`c50e9169`). **Draft, for independent review. Not merged.** One SAM_Tas PR, two revisions.

## Status

- **Revision 1** (code and offline tests, no licensed step): implemented, built and tested locally. Tested content SHA
  and results: see "Tests and validation". Awaiting independent review before revision 2.
- **Revision 2** (licensed acceptance): not started. It runs only after revision 1 is accepted: exactly the two approved
  fixture simulations plus read-only reads and offline comparisons (OD-1). Nothing licensed was executed for revision 1.
- No merge, no deployment, no roadmap/handoff change (those need the normal workflow or a fresh owner exception).

## Owner decisions (2026-10-23, recorded for portability)

Owner instruction: "I authorise S10 implementation in SAM_Tas using the reviewed S10 plan, with the amendments below ...
I approve OD-1 through OD-3 as proposed". The reviewed plan is the local, non-Git file
`SAM-BIM/S10_IMPLEMENTATION_PLAN.md` (copy it when switching machines); its content is summarised here.

- **OD-1** Bounded SAM_Tas write scope (this PR). Licensed execution: two `benchmark-tas` simulations of the signed-off
  fixtures `s08-fix-box` (`45524172…`) and `s08-fix-two` (`20da9b58…`) with the Budapest EPW (`472ffb4e…`) on the
  unchanged producer route, plus read-only reopens. No other simulation.
- **OD-2** Semantics: B1a per-space peak from `AnnualPeak`, no legacy fallback; `designLoad` unchanged (TBD sizing); a
  zero peak is an available 0 with no hour (Tas producer now, OpenStudio at S11); SAM importer fields and the model's
  placeholder hour 0 for an all-zero profile accepted and documented as they are; B1a schema stays v1; earlier Tas B1a
  files are superseded, not migrated (distinguishable only by `runnerCommit`).
- **OD-3** Result tolerances R0/R1/R2 and the space tie rule (below).
- **Amendments**: validate native series bounds, expected annual and design-day lengths, finiteness and availability,
  never turning a missing or unreadable entry into a reference zero, with negative controls; establish native units,
  signs and interval from independent evidence, document every conversion, and block affected acceptance when unresolved;
  judge hours separately from loads, including tied maxima. No standing direct-Q4 documentation exception: later SAM
  roadmap/handoff updates need the normal workflow or a fresh explicit exception. Tolerances and gates are never relaxed
  to accommodate a failure.

## Result semantics (OD-2), the definitions B1a now follows

| Item | Definition |
|---|---|
| Hour | 0-based interval hour of the year: hour *h* is *h*:00-*h*+1:00 local standard time, `0..8759`. TSD CLR index *n* is hour *n*-1 (S08 N-5, P-ALIGN). |
| Space annual peak | Max of the zone's annual `heatingLoad`/`coolingLoad` (8760 h). SAM: `SpaceSimulationResult.AnnualPeak` (`Load`, `HourOfYear`, `HourOfDay`). B1a: `peakLoad`/`peakHour`. |
| Space design-day peak | Max over the role's design-day data sets; the first data set wins a tie (importer rule, documented). SAM-only `DesignDayPeak` (load, hour of day, name); never emitted by B1a. |
| Design load | TBD zone `maxHeatingLoad`/`maxCoolingLoad` (sizing). B1a `designLoad`, unchanged. |
| Model peak | Max of the building `heatingProfile`/`coolingProfile`; hour = earliest maximum (SAM `IndexOf`, unchanged). |
| Tie | Model: the earliest hour. Space: any tied maximum is accepted; the report states the tie count and whether the observed hour is the earliest. |
| Zero rule | Max = 0: load available 0, hour unavailable (space and model, B1a). SAM keeps its documented forms: a zero `AnnualPeak` has no hour; the model result stores hour 0; the legacy `Load` holds Tas's -1 at index 0. |
| Legacy fields (unchanged) | `Load`/`LoadIndex`/`SizingMethod` project the governing peak: the design day unless the annual peak is strictly larger; `LoadIndex` = raw 1-based Tas index. Verified, not changed. |
| Unmet hours | SAM's stated definition, re-derived: heating unmet when `T + 0.5 < LL`, cooling when `T - 0.5 > UL` (TSD `dryBulbTemp`, TBD `ticLL`/`ticUL` x factor via N-5/N-6). |

## Units, signs and interval: evidence and conversions (amendment)

Independent of the code under test:

1. **Hourly, 1 h per element** - Tas Theory Manual (`Documentation/TAS_Theory.pdf`, "Simulation Principles": "a series
   of hourly snapshots"); per run, check S10-C3: every annual array has 24 x 365 = 8760 elements at the calibrated lower
   bound 1 for days 1..365 (S10-C1), every element read and finite; S08 P-ALIGN TSD (committed, replayed by
   `S10UnitsEvidenceTests`): the one-hour equipment pulse is exactly one element holding its whole annual sum.
2. **Gains in W** - Theory Manual ("Sensible Load Breakdown", "Plant Operation: Sensible Load": powers in Watts); S08
   P-ALIGN: an authored 10 W/m2 x 20 m2 lighting gain reads back as 200 (`S10UnitsEvidenceTests`).
3. **Loads in W, roles and sign** - per run, check S10-C5: the Theory Manual defines the zone's sensible load breakdown as
   a balanced account (plant input positive for heating, negative for cooling), so every zone and hour must satisfy
   `heatingLoad - coolingLoad + sum(breakdown terms) = 0` within `0.01 x sum|terms| + 0.5 W` (terms: solar, lighting,
   occupant sensible, equipment sensible, infiltration/ventilation, air movement, building heat transfer, external
   conduction opaque and glazing, AHU). This ties the load arrays to the breakdown's W and fixes heating as plant input and
   cooling as its negative; a swapped or kW-scaled array does not close. S10-C4: load arrays are non-negative magnitudes.
4. **Building profiles** - per run, check S10-C6: each hour of `heatingProfile`/`coolingProfile` equals the sum of the zone
   loads within `1e-6 x sum + 1e-6 W`, so the model-level profiles carry the same W.
5. **Conversions** - zone loads W, unchanged (R0); model consumption kWh = sum(W x 1 h) / 1000 (R2); model peak kW =
   max / 1000 (R1); hour = CLR index - 1; hour of day = hour mod 24; floor area m2 and volume m3 unchanged (R1 sum).

Any unresolved check BLOCKS the rows that depend on it (never PASS): C1/C2 block everything; C3/C4/C5 block the zone's
peak, design-day and legacy rows (and C5/C6 the model rows); C7 (design-day shape, below) blocks the design-day and legacy
rows; C8 (identity) blocks the zone rows.

## Tolerances (OD-3)

| Class | Rule | Applies to |
|---|---|---|
| R0 | exact | hours (against the full tie set), indices, counts, copied float32 W values, TBD design loads, unmet hours |
| R1 | `|d| <= 1e-12 x max(1, |ref|)` | model peaks (one /1000), model floor area and volume (sums of < 100 values) |
| R2 | `|d| <= 1e-9 x |ref| + 1e-9` (kWh) | model consumption (sums of 8760 values) |

Hours are separate rows from loads (`annualPeak.hourOfYear`, `annualPeak.hourOfDay`, `peakHour`, `legacy.loadIndex`):
a right load at a wrong hour FAILs only the hour row (tested).

## Work completed (revision 1)

1. **B1a producer** (`benchmark/SAM.Analytical.Tas.Benchmark/Modify/ToBenchmark.cs`): `Condition()` reads `AnnualPeak`
   (load, `HourOfYear`; basis must be AnnualSimulation) with no legacy fallback; `Peak()` applies the zero rule for spaces
   and the model; doc comment states the definitions above. Fixes F4 (raw 1-based index as hour; last-hour peak lost),
   F5 (governing design-day load and series index emitted), F6 (zero peak lost; model hour 0 emitted as an hour).
2. **Producer tests**: `BenchmarkFixture` gains `AnnualPeak`/`DesignDayPeak` with the existing values (the golden
   `golden-tas-benchmark.json` is **byte-identical**, test unchanged); new `PeakSemanticsTests` (14 cases; 12 fail on the
   pre-S10 producer; the other 2, the validator check of a zero-peak document and the SAM JSON round trip of
   `AnnualPeak`, are guards the old producer also satisfies).
3. **Independent reference harness** (`SAM_Tas/SAM.Analytical.Tas.DirectT3D.Validation/Results/`):
   - `TsdResultRead.cs` (COM, fresh process, `openReadOnly`, sha256 before/after, stub refusal): building profiles; per
     zone name, GUID, number, floor area, volume and 13 annual arrays; every design-day data set (zones, load and air
     temperature series); `GetPeakZoneGains` tables as a cross-check only. Element conversion (`NativeConvert`, COM-free):
     a null or non-numeric element is listed as unread and held as NaN, **never 0** (the S08 P-ALIGN reader's
     `Convert.ToDouble(null)` = 0 is why it is not reused).
   - `ResultReference.cs` (COM-free): checks S10-C1..C8 and every expected value; fails closed (BLOCKED with the reason).
   - `ResultSubject.cs` (COM-free): the saved model's related results as stored (all sources, so a foreign or duplicate
     result is visible), the model-level result through SAM's units query, and the B1a document parsed as plain JSON
     (presence, availability, value and unit token of every metric).
   - `ResultCompare.cs` (COM-free): every reference row is required (complete coverage by construction); FAIL when the
     subject lacks a value, BLOCKED when the reference is blocked or the subject capture failed; verdict PASS only when
     every row passes; exit 0/10/11.
   - `ResultNegatives.cs` (COM-free): 28 negative controls on copies plus an unmutated control (list below).
   - `ResultReport.cs`, `ResultsRun.cs` (modes `results-read`, `results-subject`, `results-compare`, `results-negatives`;
     `results-subject` is the only caller of the production `ToSAM_AnalyticalModelSimulationResult`, tested), `Program.cs`
     dispatch, `Run-S10-Results.ps1` (phases `run` licensed / `offline` replay; fixture and EPW hashes checked first; a
     non-empty run folder refused; produced files hashed before and after the reads) and `Pack-S10-Evidence.ps1`.
4. **Offline tests** (`SAM.Analytical.Tas.DirectT3D.ReadBack.Tests`, already in CI; linked COM-free files, no workflow
   change): `S10ResultCompareTests`, `S10ResultReferenceTests`, `S10SourceScanTests`, `S10UnitsEvidenceTests`, and the
   synthetic triple `S10SyntheticTriple` (raw arrays, TBD read, SAM results as the importer defines them, the converter's
   model result, B1a from the real `ToBenchmark`) covering a last-hour peak, a later tied maximum, an all-zero load, a
   governing design day and a zero design day. The test project now references the COM-free producer and the B1a schema DLL.
5. `.gitattributes`: `Documentation/S10-Results-evidence/** -text` (hashed evidence, revision 2).

Negative controls (each must FAIL or be BLOCKED by its named rows; all as intended on the synthetic triple and, by the
same code, on each licensed fixture in revision 2):
F4 replay (B1a hour = raw index), F5 replay (B1a peak from the governing design day), F6 replay (zero peak with an
available hour 0), SAM hour +1 and -1, heating/cooling results swapped, B1a model consumption x1000, B1a space peak x1000,
consumption off by 1e-6 relative, captured model peak hour +1, a non-maximal "tie" hour, a missing, a duplicate and a
non-Tas-source space result, a missing B1a metric (all FAIL); part-year TSD, stub TSD, non-unique zone name, negative,
unread, NaN, truncated (8759), lower-bound-0 and absent load arrays, absent building profile, zone loads in kW, swapped
load arrays, a design-day series one hour short (all BLOCKED). The forbidden-API source scan has its own negative test.

## Decisions and assumptions (for review)

- **Reference independence**: `TsdResultRead.cs`, `ResultReference.cs`, `ResultCompare.cs` and `ResultSnapshot.cs` call
  only TSD's COM API and SAM core types; a source scan forbids SAM_Tas converters/queries (`Convert.ToSAM_*`,
  `ValueDictionary`, `MaxValueDictionary`, `DesignDataDictionary`, `ZeroBasedHour*`, `ZoneResultSeries`,
  `Query.AnnualBuildingResult`, `Query.UnmetHours`, `FullYearRefusal`, `Core.Tas.Query`, `SAMTSDDocument`,
  `SAMTBDDocument`, `TemperatureSetPointDictionary`, `DailyValues`, `using SAM.Analytical.Tas;`, ...). The TBD is read by
  the merged S08 `TbdReadBack` (unchanged); `TsdReadBack` is not modified, so S08 replays are untouched.
- **Identity**: SAM result <-> TSD zone by `Reference` = zone GUID (compared as GUIDs, not text); SAM space <-> zone by
  unique exact name with equal counts; B1a space <-> SAM space by GUID and name; TBD zone <-> TSD zone by GUID. Anything
  else is BLOCKED (reference) or FAIL (subject).
- **Design-day shape [U]**: a data set's series must be complete, finite, non-negative, equal in lower bound and length
  across zones, and of length 24 x (TBD design days of the same role and name) or 24 x (their repetitions); the hour of
  day is position mod 24 (series day-aligned). This is measured on the acceptance TSDs; another native shape BLOCKS the
  design-day and legacy rows (recorded as a blocker, never accepted by widening the rule).
- **Balance closure [U]**: the S10-C5 tolerance (1 % of the hour's absolute terms + 0.5 W) is fixed now, before any
  licensed read. If Tas's breakdown arrays do not close as the Theory Manual describes, the affected zone rows are BLOCKED
  and recorded with the largest residual; the tolerance is not relaxed.
- **Unmet hours** are a derived check of SAM's stated definition (setpoint = profile value x factor), not a TSD value.
- **Out of scope / recorded, not changed**: B1a `designDaySource` reports `None` for these fixtures although Tas derives
  design days from the weather (provenance label, follow-up); the legacy -1 sentinel; the OpenStudio producer's `Peak()`
  (S11); the importer, routes, defaults, schema, UI, CI.

## Files changed

`benchmark/SAM.Analytical.Tas.Benchmark/Modify/ToBenchmark.cs`;
`benchmark/SAM.Analytical.Tas.Benchmark.Tests/Fixtures/BenchmarkFixture.cs`, `PeakSemanticsTests.cs` (new);
`SAM_Tas/SAM.Analytical.Tas.DirectT3D.Validation/Program.cs`, `Results/*.cs` (7 new), `Run-S10-Results.ps1`,
`Pack-S10-Evidence.ps1` (new);
`SAM_Tas/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests.csproj`,
`S10ResultCompareTests.cs`, `S10ResultReferenceTests.cs`, `S10SourceScanTests.cs`, `S10UnitsEvidenceTests.cs`,
`S10SyntheticTriple.cs` (new); `.gitattributes`; this record. No importer, route, default, schema, workflow, SAM_UI,
SAM_Validation or SAM change. `PROJECT_PROGRESS.md` unchanged (post-merge closeout only).

## Tests and validation

Revision 1, local Windows workstation, no Tas licence used. Tested content SHA: **to be filled by the record-only commit
that follows the content commit** (see the PR description for the final head and hosted checks).

Commands (from the SAM-BIM root; VS MSBuild is required for the COM harness, MSB4803):

```text
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" SAM_Tas\SAM_Tas.sln /t:Restore;Rebuild /p:Configuration=Release /m /nr:false /v:m
dotnet test SAM_Tas/benchmark/SAM.Analytical.Tas.Benchmark.Tests/SAM.Analytical.Tas.Benchmark.Tests.csproj -c Release
dotnet test SAM_Tas/benchmark/SAM.Analytical.InputEquivalence.Tests/SAM.Analytical.InputEquivalence.Tests.csproj -c Release
dotnet test SAM_Tas/SAM_Tas/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests.csproj -c Release
```

## Revision 2 (licensed acceptance; after revision-1 acceptance only)

```text
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Run-S10-Results.ps1 -Phase run -Root C:\TasOut\s10\acc1 -WeatherEpw C:\TasOut\s08c\weather\budapest.epw
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Pack-S10-Evidence.ps1 -Root C:\TasOut\s10\acc1
```

Prerequisites: licensed Tas 2.0.0.1, Tas GUI closed, the revision-1 head built in Release from a clean tree, the Budapest
EPW at the given path (`472ffb4e…`). PASS = both fixtures PASS with complete coverage, every calibration check verified,
every negative as intended, produced files and fixtures unchanged by the reads. Evidence: `Documentation/S10-Results-evidence/`
(committed); run folders machine-local.

## Unresolved, risks, limits

- Revision 2 not run: S10 is not accepted. The design-day shape and the balance closure are fixed rules measured for the
  first time on the acceptance TSDs; either may BLOCK (see above). The Tas tie rule for `GetPeakZoneGains` is unknown and
  only reported (the subject is accepted on any tied maximum).
- B1a files produced before this change keep F4-F6 values (superseded, not migrated). SAM_Validation `METRICS.md` (Tas
  column) needs a docs-only follow-up; `designDaySource` label follow-up; OpenStudio zero rule at S11.
- Machine-local: `SAM-BIM/S10_IMPLEMENTATION_PLAN.md` (the reviewed plan) and, for revision 2, `C:\TasOut\s08c\weather\budapest.epw`.

## Next step

Independent review of revision 1 (this branch, the tested SHA above): GPT-6 or Claude Opus 5.5, effort High, new session,
read-only review mode, because the reference's independence and fail-closed semantics are where a false PASS would hide.
After acceptance: revision 2 in Build mode on the licensed workstation (the two approved runs only), then Ready, final-head
CI, owner merge and the post-merge closeout.
