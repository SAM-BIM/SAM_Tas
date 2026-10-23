# S10: Tas results round-trip validation

Task S10 of the SAM simulation roadmap (`SAM/documentation/SIMULATION_ROADMAP_Q4_2026.md`, row S10): SAM-imported Tas
results and the B1a document must equal an independent read of the TSD; defects F4-F6 (SAM_UI#239) fixed or explicitly
accepted; hour and peak definitions documented. Branch `feature/s10-tas-results-roundtrip`, base `sow/2026-Q4`
(`c50e9169`). **Draft, for independent review. Not merged.** One SAM_Tas PR, two revisions.

## Status

- **Revision 1** (code and offline tests, no licensed step): first tested at content `77ac1cda` (record head `b607d529`);
  the independent review returned **REVISE** with two P1 findings (R1, R2). **Revision 1 correction** (this commit series)
  closes both offline; see "Revision 1 correction". Tested content SHA and results: "Tests and validation". Awaiting the
  independent re-review of revision 1 before revision 2.
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
   - `ResultNegatives.cs` (COM-free): 28 negative controls on copies plus an unmutated control (list below); the correction adds 12 design-day controls (40 in all).
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
Correction (all BLOCKED through only the dependent rows): design-day series at lower bound 0, 2 and -23, a role's
design-day `GetPeakZoneGains` absent, a role's design-day gain arrays one hour late, every data set missing, one role
missing, a duplicate data set, an expected TBD group without a data set, a data set the TBD does not hold, an unreadable
TBD group, no coverage read.

## Revision 1 correction (independent review R1/R2, 2026-10-23)

Review: `SAM-BIM/.review-s10-r1-20261023/REVIEW.md` (machine-local, with its probe). Both findings were reproduced by the
review probe at `77ac1cda`: the unmodified control and three design-day lower bounds (0, 2, -23) all PASS 95/95 with C7
verified (R1), and deleting every TSD design-day data set while the TBD keeps its groups, with SAM omitting the same
outputs, PASS 95/95 with C7 not-exercised (R2).

**R2, design-day coverage (new check S10-C10).** The expectation is now independent of the TSD:
- `results-read-design` (new COM reader `Results/TbdDesignCoverageRead.cs`, its own fresh process, read-only, sha256
  before/after) reads the TBD design-day groups to `tbd-design-coverage.json`.
- A null ends a list. An exception, or reaching the enumeration bound, is recorded as unreadable, never as the end of the
  list. The merged S08 `TbdReadBack` (unchanged) takes an exception as the end of the list, which is why it is not used for
  coverage.
- Per role, every TBD group needs exactly one TSD data set of the same role and exact name, and no TSD data set may be
  unexpected.
- The following BLOCK that role's design-day rows (`designDayPeak.*`) and legacy rows (`legacy.*`), whatever the subject
  holds: missing groups, extra data sets, duplicate names, unnamed data sets, unreadable groups or days, read errors, and a
  coverage that was never read.
- No design days is accepted only when the coverage was read completely and holds no group of the role.
- The name correspondence is required and checked, not assumed. It is first observed on the acceptance TSDs, and a
  mismatch stays BLOCKED.
- C7 (series shape and expected length) now takes the expected length from the matched coverage group.

**R1, design-day index and day origin (new check S10-C9, per role).** The convention is established only from evidence:
- **Index**:
  - Every design-day series must be at the N-5 lower bound 1. Another bound is never taken as evidence of another
    convention.
  - The (value, index) rows of Tas's own `GetPeakZoneGains` are located on the annual tables. There N-5 fixes the index as
    the CLR index, and exactly one row pair must fit every positive annual zone peak.
  - Each design data set's table must then hold every positive design-day maximum and the CLR index of one of its tied
    elements.
- **Day origin**: an hour-varying TBD hourly gain schedule (`ticLG`/`ticESG`, x factor, N-5 hourly slot) of the condition
  carrying the design days' day type must match the TSD design-day `lightingGain`/`equipmentSensibleGain` at offset 0 only
  (index 1 = 00:00-01:00, `ResultReference.Offsets`). Constant schedules are no evidence. Any other set of fitting offsets
  is a contradiction.
- **Blocking, hours kept separate from loads**:
  - Without index evidence, the design-day hour of day and the design-governed `legacy.loadIndex` are BLOCKED.
  - Without a time signal, only the design-day hour of day is BLOCKED.
  - Design-day loads, names, the sizing method, the legacy load, annual-governed indices and a zero peak's absent hour do
    not depend on the convention and keep their verdict.
- The importer and agreement with it are never used.
- `TsdResultRead` now also reads the design-day `lightingGain` and `equipmentSensibleGain` arrays.

**Evidence that the regressions catch the reviewed code.** The new scenarios, written against the `77ac1cda` API and run
on an extracted copy of `77ac1cda` (machine-local:
`SAM-BIM/.s10-r1-correction-20261023/old-code-regressions/`), fail on 7 of 9 checks there:

| Scenario | `77ac1cda` | Corrected |
|---|---|---|
| Lower bound 0, 2, -23 | PASS 95/95 each | BLOCKED |
| All data sets missing, subject omits them | PASS | BLOCKED |
| Extra expected group | PASS | BLOCKED |
| Duplicate data set | PASS | BLOCKED |
| One role missing | FAIL, via the subject only | BLOCKED |
| Unexpected data set | already BLOCKED (name match) | BLOCKED |
| Justified no-design-day control | PASS | PASS |

The review probe, re-run with only the reference's new coverage argument added
(`SAM-BIM/.s10-r1-correction-20261023/probe-rerun/`):
- control PASS 95/95, with C7, C9 and C10 verified;
- lower bounds 0, 2 and -23: BLOCKED 91/95 each, C9 unresolved, exactly the four hour/index rows;
- all data sets missing: BLOCKED 71/95, C10 unresolved, all 24 design-day and legacy rows.

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
- **Design-day shape, coverage and convention [U]**: a data set's series must be complete, finite, non-negative, equal in
  lower bound and length across zones, and of length 24 x (the matched TBD group's design days) or 24 x (their
  repetitions) (C7); the data sets must correspond to the TBD groups (C10); index and hour of day follow only the
  evidence-backed convention (C9). All three are measured for the first time on the acceptance TSDs; anything else
  BLOCKS the dependent rows (recorded as a blocker, never accepted by widening a rule).
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
`SAM_Tas/SAM.Analytical.Tas.DirectT3D.Validation/Program.cs`, `Results/*.cs` (8 new, incl. the correction's
`TbdDesignCoverageRead.cs`), `Run-S10-Results.ps1`, `Pack-S10-Evidence.ps1` (new);
`SAM_Tas/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests.csproj`,
`S10ResultCompareTests.cs`, `S10ResultReferenceTests.cs`, `S10SourceScanTests.cs`, `S10UnitsEvidenceTests.cs`,
`S10SyntheticTriple.cs`, `S10DesignDayTests.cs` (correction) (new); `.gitattributes`; this record. The correction
changes only `Results/` (`ResultReference.cs`, `ResultSnapshot.cs`, `ResultNegatives.cs`, `ResultsRun.cs`,
`ResultReport.cs`, `TsdResultRead.cs`, new `TbdDesignCoverageRead.cs`), the two scripts, the S10 tests and this record. No importer, route, default, schema, workflow, SAM_UI,
SAM_Validation or SAM change. `PROJECT_PROGRESS.md` unchanged (post-merge closeout only).

## Tests and validation

### Revision 1 correction (current)

CORRECTION-RESULTS-PENDING

### Revision 1 as first reviewed (superseded by the correction)

Local Windows workstation (2026-10-23), no Tas licence used. Tested content SHA `77ac1cda`
(`77ac1cda7d79866183ee672323073981d1a62d04`), clean tree; record head `b607d529`. Hosted at `b607d529`: Build
`39957785234` and SPDX `39957785222` SUCCESS (testing merge `810734c8`, all eight dependencies at Q4, totals
40/258/178/1249/432), as recorded by the independent review.

| Check (local, at `77ac1cda`) | Result |
|---|---|
| `SAM_Tas.sln` Restore + Rebuild, Release, VS MSBuild 18 | exit 0 (only pre-existing CS8604 warnings in `SAM.Core.Tas.UKBR`) |
| Producer tests `SAM.Analytical.Tas.Benchmark.Tests` | 40/40 (26 existing + 14 new); golden byte-identical |
| Core `SAM.Analytical.InputEquivalence.Tests` | 258/258 (unchanged) |
| Read-back `SAM.Analytical.Tas.DirectT3D.ReadBack.Tests` | 178/178 (141 existing, incl. every S08 PR-B/PR-C offline replay, + 37 S10) |
| New producer tests against the pre-S10 `ToBenchmark` | 12/14 fail as intended (F4/F5/F6 replays) |
| Built harness, COM-free modes on the synthetic triple's files | `results-compare` PASS 95/95, exit 0; `results-negatives` control + 28 cases as intended, exit 0 |
| Artefacts | `direct-t3d-validation.dll` sha256 `86c77490b735fd2c…`, `benchmark-tas.dll` `a929cfea628e05f6…`, rules `766a88cdbf129499…` (the frozen S08 table, unchanged) |

Not run locally: TM59 (1249) and GenOpt (432) suites (projects untouched; hosted CI runs them). No licensed step.

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

Per fixture the run executes: one `benchmark-tas` simulation, then `results-read` (TSD + TBD), `results-read-design` (TBD
design-day coverage), `results-subject`, `results-compare` and `results-negatives`, each in its own process.
Prerequisites: licensed Tas 2.0.0.1, Tas GUI closed, the accepted revision-1 head built in Release from a clean tree, the Budapest
EPW at the given path (`472ffb4e…`). PASS = both fixtures PASS with complete coverage, every calibration check verified,
every negative as intended, produced files and fixtures unchanged by the reads. Evidence: `Documentation/S10-Results-evidence/`
(committed); run folders machine-local.

## Unresolved, risks, limits

- Revision 2 not run: S10 is not accepted. Licensed-only questions, first measured on the acceptance TSDs, any of which
  may BLOCK the dependent rows (never relaxed):
  - **Design-day series**:
    - Length: 24 or 24 x the 30 repetitions the S08 PR-C fixture TBDs hold.
    - Lower bound: must be 1.
    - Name correspondence: TSD data set names must equal TBD group names.
  - **`GetPeakZoneGains` layout**:
    - The annual tables must give exactly one fitting (value, index) row pair.
    - The design tables must hold the CLR index of the maximum.
    - The Tas tie rule is unknown and only reported; the subject is accepted on any tied maximum.
  - **Heating design-day hour of day is expected to stay BLOCKED.** The committed S08 PR-C fixture reads show:
    - the heating design day's condition (`FixBox - HDD`, `FixA - HDD`, `FixB - HDD`) holds only zero `ticValueProfile`
      gains, so nothing places that day in time;
    - the cooling design day uses the annual condition with an hour-varying `ticLG`, which is a time signal.

    Unless the owner provides another independent time signal, `designDayPeak.hourOfDay` for heating will be BLOCKED in
    revision 2. The heating legacy index needs only the native index evidence.
  - **Balance closure**: the S10-C5 assumption.
- B1a files produced before this change keep F4-F6 values (superseded, not migrated). SAM_Validation `METRICS.md` (Tas
  column) needs a docs-only follow-up; `designDaySource` label follow-up; OpenStudio zero rule at S11.
- Machine-local: `SAM-BIM/S10_IMPLEMENTATION_PLAN.md` (the reviewed plan) and, for revision 2, `C:\TasOut\s08c\weather\budapest.epw`.

## Next step

Independent re-review of revision 1 including the correction (this branch, the correction's tested SHA above): GPT-6 or
Claude Opus 5.5, effort High, new session, read-only review mode, because the reference's independence and fail-closed
semantics are where a false PASS would hide. The owner also decides whether the expected BLOCKED heating design-day hour
of day (no independent time signal) is acceptable for S10 or needs further evidence. Revision 2 does not start before
that acceptance.
After acceptance: revision 2 in Build mode on the licensed workstation (the two approved runs only), then Ready, final-head
CI, owner merge and the post-merge closeout.
