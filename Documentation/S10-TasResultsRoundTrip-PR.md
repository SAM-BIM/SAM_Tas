# S10: Tas results round-trip validation

Task S10 of the SAM simulation roadmap (`SAM/documentation/SIMULATION_ROADMAP_Q4_2026.md`, row S10): SAM-imported Tas
results and the B1a document must equal an independent read of the TSD; defects F4-F6 (SAM_UI#239) fixed or explicitly
accepted; hour and peak definitions documented. Branch `feature/s10-tas-results-roundtrip`, base `sow/2026-Q4`
(`c50e9169`). **Draft, for independent review. Not merged.** One SAM_Tas PR, two revisions.

## Status

- **Revision 1** (code and offline tests): tested at `77ac1cda`; the review returned REVISE (R1, R2). The correction was
  tested at `3387915b` (record head `223c49dd`) and independently **accepted for the bounded licensed measurement only**
  (`SAM-BIM/.review-s10-r1-correction-20261023/REVIEW.md`, machine-local).
- **Revision 2, attempt 1** (licensed, 2026-10-23): **STOPPED on an infrastructure failure.** Both OD-1 invocations count
  as attempted. The box simulation completed, but B1a was refused (no SAM commit). The script then started the two-zone
  invocation, which was terminated before it wrote a TSD. The review of that outcome returned **REVISE**: R3 (runner
  provenance), R4 (no stop on failure), and a design-day representation that contradicts the S10-C7 assumption
  (`SAM-BIM/.review-s10-r2-20261023/REVIEW.md`). See "Revision 2 attempt 1".
- **Revision 2 correction** (this commit series, offline only): closes R3/R4 and replaces the disproved design-day
  assumption with the measured calendar representation. See "Revision 2 correction". Tested content SHA and results:
  "Tests and validation". No simulation, COM read or Tas process was run for it.
- **S10 is not accepted.** Next: independent review of this correction. Any further licensed execution needs a fresh
  owner decision; none is authorised by this record.
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
Revision-2 correction (11 more, 51 in all; all BLOCKED):
- the design day moved one hour later than its year day's block;
- a load hour inside the day holding −1;
- a value outside the day;
- an unread or NaN load inside the day;
- the series compacted to 24 elements;
- `yearDay` unread, +1, or 0;
- Tas's design-day peak index outside the day, or at a non-maximum.

The time-signal control now rotates the gains within the TBD day. Lower-bound controls now take the role off the calendar
shape.

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

## Revision 2 attempt 1 (licensed, 2026-10-23): stopped

The run used the committed `Run-S10-Results.ps1 -Phase run -Root C:\TasOut\s10\acc1` at `223c49dd` (content `3387915b`).
- **Build:** fresh Release rebuild, byte-identical to the build the correction review tested: `direct-t3d-validation.dll`
  `c055753c…`, `benchmark-tas.dll` `6f6aff19…`.
- **Inputs:** Tas 2.0.0.1; fixtures `45524172…`/`20da9b58…` and EPW `472ffb4e…`, all unchanged afterwards.

**Counted attempts: both OD-1 invocations.**
1. **`s08-fix-box`:** the workflow ran to completion (Sizing, Simulating, Saving Model). It saved the native TBD
   `d868316b…`, TSD `3d80a135…` and SAM JSON `204062f6…`. Then `benchmark-tas` exited **4**: "provenance.commit.malformed
   at $.provenance.samCommit". The script passed no `--sam-commit`, and local builds carry no stamp
   (`benchmark/D10-CHECKPOINT.md`). The reads ran: TSD and TBD unchanged by them, 1 zone, 2 data sets. The subject capture
   recorded "B1a missing" (exit 70). Compare: BLOCKED 59/59 (exit 11). Negatives: not run (exit 70).
2. **`s08-fix-two`:** the script did not stop after the failure (R4), so it started this invocation automatically about
   5 s later. The agent terminated it during the T3D/TBD build. No TSD was written, and the watchdog has no entry because
   the script was killed inside the step.
   - It left an orphan `TBD.exe` (PID 50008). It is no longer running as of the correction session. This branch's code
     and tests did not stop it: the runner tests bind `-TasProcessNames` to a name that matches nothing (verified). Who
     ended it is not recorded.

**Native observations, box (independent reads, unchanged by reading):**
- **Model:** heating 2037.1340381522775 kWh, 1.4284979248046874 kW at hour 7 (354 tied hours). Cooling 994.3502346024811
  kWh, 1.60656640625 kW at hour 3877 (17 tied hours).
- **Checks:** C1–C6, C8 and C10 verified. C5 closes in every hour; the largest residual is −0.0307 W.
- **Annual `GetPeakZoneGains`:** value row 1, index row 2, index = the CLR index of the earliest tied maximum.
- **Design-day series (contradicting the S10-C7 assumption):** 8760 elements at lower bound 1, populated only at the 24
  CLR indices of the TBD design day's `yearDay`, Tas's −1 elsewhere. Heating `yearDay` 39 → CLR 913..936; cooling 182 →
  CLR 4345..4368. All five series of each data set follow this.
- **Design-day peak table:** Tas's index is the CLR index of an in-day maximum: cooling 4358, heating 913.

**Evidence (machine-local):**
- the run root `C:\TasOut\s10\acc1`;
- `SAM-BIM/.s10-r2-acceptance-20261023/`: `REPORT.md`, and a `pack/` made with the committed packer (`-Target` set there),
  28 files.

No evidence from this attempt is committed: the acceptance pack belongs to a successful run.

## Revision 2 correction (review R3/R4 and the design-day evidence method, 2026-10-23)

**R3, provenance** (`Run-S10-Results.ps1`). Before any engine starts, the script resolves `samCommit` and
`runnerCommit` and passes them to `benchmark-tas` as `--sam-commit`/`--runner-commit`. Each is the HEAD (40 hex) of a
clean checkout (`git status --porcelain --untracked-files=all` empty), bound to its build:
- every artifact (the harness and `benchmark-tas` exe/dll, `SAM.Analytical.Tas.dll`; the SAM build's `SAM.*.dll`) is no
  older than every tracked source file (`*.cs`, `*.csproj`, `*.props`, `*.targets`, `*.resx`, `*.xaml`, `*.sln`) of its
  checkout;
- every `SAM.*.dll` beside `benchmark-tas` is byte-identical to `SAM/build`, and `SAM.Analytical.dll` must be among them.

Any problem, recorded in `provenance-run.json`, stops the run in preflight. The fixture and EPW checks also stop in
preflight. The production CLI is unchanged.

**R4, failure control.**
- Each step is written to the watchdog as `started` before its launch, then as `completed` or `terminated` (timeout).
- The run stops at once (exit 3, `status-run.json` with the reason and the failed step) on any of: a simulation that is
  non-zero or timed out; an exit 0 without the TBD/TSD/JSON/B1a; a read, coverage or subject capture that is non-zero; an
  infrastructure exit from compare (not 0/10/11) or from negatives (not 0/10); a file changed by the reads; a fixture
  changed. Nothing dependent runs and no further fixture starts.
- Compare FAIL/BLOCKED and a negative matrix that is not all as intended are measured results: they are recorded in
  `status-run.json` and the run continues.
- `Clear-Tas` runs only in the run phase, and the process names are a parameter. The tests drive the real script with
  `.cmd` process doubles, temporary git checkouts and a non-matching process name (`S10RunnerScriptTests`), so no Tas
  process or COM server is touched.

**Design-day evidence method (amends S10-C7 and C9).** This implements only the representation measured on the acc1 box.
- **The expected mask comes from the TBD, never from the series.** The data set's coverage group must hold exactly one
  design day.
  - Its `yearDay` is taken from the raw TBD read (`tbd-snapshot.json`, `TbdReadBack` `d.yearDay`): the group is matched by
    role and exact name, the snapshot and coverage must be reads of the same TBD (equal sha256), and they must agree on the
    day type and repetitions.
  - `yearDay` must be a readable integer 1..365. Mask = CLR (d−1)·24+1 .. d·24.
  - Calendar basis: N-5 (CLR = hour + 1, days 1..365). The 1-based `yearDay` reading of that basis is checked on every run,
    because any other basis puts the expected mask off the populated elements and BLOCKS.
- **Every series of the data set that was read must:**
  - be 8760 elements at the N-5 lower bound, every element read and finite;
  - be exactly −1 outside the mask;
  - hold a non-negative load inside the mask (so never the −1 sentinel). Other series may hold any finite value inside,
    e.g. −1 °C.
- **Kept as they are:** raw positions, original CLR indices, the legacy index semantics and R0/R1/R2. Annual arrays are
  unchanged: no sentinel exemption.
- **Not measured, so BLOCKED:** the earlier 24 / 24 × repetitions layout, a group of several design days, two data sets on
  one year day, and any other bound or length.
- **S10-C9:**
  - Design-day peaks and ties are taken over the mask only.
  - Tas's design index must be the CLR index of an in-day maximum, so an index at a −1 element or at a non-maximum
    contradicts it.
  - The time-signal fit uses the 24 in-day gains, whose first element is hour 0 at offset 0.

**C9 assessment (established / uncertain), box:**
- **Day block:** established from the TBD `yearDay` and the exact mask, both roles.
- **Index:** established; Tas's design index is the CLR index of an in-day maximum, both roles.
- **Cooling within-day origin:** established. The TBD `ticLG` hourly schedule of the CDD condition fits the in-day
  lighting gain at offset 0 only, so the cooling hour of day is derived.
- **Heating within-day origin:** NOT established. The HDD condition has no gains, and C9 stays unresolved for heating.
  However, the heating design load is a 24-way tie over the whole day. Any within-day rotation maps that tie set onto
  itself, so the hour-of-day row is derived with all 24 hours accepted. This does not depend on the importer, on a waiver
  or on relaxed tolerances.
  - A role with a narrower tie set and no time signal stays BLOCKED.
  - An absent hour, or one outside 0..23, still FAILs.
- **Still uncertain:**
  - universality beyond this box: the layout is measured on one fixture, both roles; the two-zone fixture has no TSD;
  - the meaning of the native `yearDay` beyond its exact match here;
  - multi-day, repeated-day or overlapping variants (BLOCKED).

**Offline replay of the acc1 box.** Read-only, written to `SAM-BIM/.s10-r3-correction-20261023/replay/`; acc1 is not
written.
- **Built harness `results-compare`:** BLOCKED 59/59 (exit 11), the original "B1a missing" error preserved. C7 is now
  verified (year days 182 and 39); C9 is unresolved only for the heating origin. All 59 reference rows are derived
  (`3387915b`: 47, with 12 BLOCKED by C7).
- **`results-negatives`:** exit 70 (no B1a file), as before.
- **Diagnostic only** (machine-local probe, derived, not the producer's acceptance): the partial SAM capture checked
  against the corrected reference.
  - 39 SAM/identity rows match, including both design-day loads, the cooling hour of day 13, the heating hour of day,
    legacy and annual peaks, design loads and unmet hours (heating 292, cooling 9).
  - 20 B1a-dependent rows are not checkable.

The missing B1a remains a blocker, and no B1a was recovered or fabricated.

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
- **Design-day shape, coverage and convention**: the data sets must correspond to the TBD groups (C10), and index and hour
  of day follow only the evidence-backed convention (C9). C7's original assumption (24 x days or repetitions) was
  disproved by the acc1 box TSD and replaced by the measured calendar representation ("Revision 2 correction"). Anything
  unmeasured BLOCKS the dependent rows (recorded as a blocker, never accepted by widening a rule).
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
`S10SyntheticTriple.cs`, `S10DesignDayTests.cs` (correction), `S10RunnerScriptTests.cs` (revision-2 correction) (new);
`.gitattributes`; this record. The revision-2 correction changes:
- `Results/ResultReference.cs` (C7 calendar shape, C9 in-day peaks/offsets, full-day-tie hour rows);
- `Results/ResultNegatives.cs` (11 controls, a TBD-mutation flag);
- `Results/ResultsRun.cs` (writes a mutated TBD snapshot);
- `Run-S10-Results.ps1` (R3/R4), `Pack-S10-Evidence.ps1` (packs `status-*.json`);
- `S10SyntheticTriple.cs` (calendar-shaped design days, year days 182/39, an optional 24-way heating tie);
- `S10DesignDayTests.cs`, and the new `S10RunnerScriptTests.cs`. The correction
changes only `Results/` (`ResultReference.cs`, `ResultSnapshot.cs`, `ResultNegatives.cs`, `ResultsRun.cs`,
`ResultReport.cs`, `TsdResultRead.cs`, new `TbdDesignCoverageRead.cs`), the two scripts, the S10 tests and this record. No importer, route, default, schema, workflow, SAM_UI,
SAM_Validation or SAM change. `PROJECT_PROGRESS.md` unchanged (post-merge closeout only).

## Tests and validation

### Revision 2 correction (current)

Run on the local Windows workstation, 2026-10-23. No simulation, sizing, COM read or Tas process. **Tested content SHA
`f29adaed`** (`f29adaedb3c48c7ee3dfc895b3616f9701b01f75`), clean tree. The record-only commit after it changes this file
only. Final head and hosted checks: PR description.

| Check (local, at `f29adaed`) | Result |
|---|---|
| `SAM_Tas.sln` Rebuild, Release, VS MSBuild 18, existing restore assets | exit 0, 0 errors |
| Producer tests | 40/40 |
| Core `SAM.Analytical.InputEquivalence.Tests` | 258/258 |
| Read-back tests | 232/232 (196 + 23 design-day + 13 runner-script doubles), 0 failed, 0 skipped |
| Negative matrix | synthetic control PASS, 51 cases as intended; on the box-like input (no heating signal, 24-way heating tie), the control PASSes and every case is as intended |
| Runner doubles (`S10RunnerScriptTests`, the committed script) | see below |
| Offline replay of acc1 box (built harness) | `results-compare` BLOCKED 59/59, exit 11 (B1a missing kept), C7 verified; `results-negatives` exit 70 (no B1a) |
| Regression probe, same inputs on both builds (below) | the reviewed build blocks the measured layout and accepts the unmeasured one; the correction does the reverse |
| Preservation | fixtures, EPW, acc1 native files and all 28 pack hashes unchanged; no write under `C:\TasOut\s10\acc1` since 16:15:32Z |
| Artefacts | `direct-t3d-validation.dll` `b16fba39bc9fcc9d…`, `benchmark-tas.dll` `d2561556728…`, `SAM.Analytical.Tas.dll` `c4de5d7db9f8…`, rules `766a88cdbf129499…` (frozen) |

The runner doubles show:
- simulation exit 4 starts no capture and no second fixture: 1 invocation, exit 3;
- a timeout is terminated and the run stops;
- an exit 0 without its files stops;
- a failed read, coverage read or subject capture stops before the dependents;
- a compare exit of 70 stops, but compare/negatives exit 10/11 complete (measured);
- a wrong weather sha256, a dirty or untracked checkout, a source newer than the build, a SAM library that is not the SAM
  build output, or no git checkout each launch nothing;
- a complete run passes `--sam-commit`/`--runner-commit` = the checkouts' HEADs.

Regression probe, the same inputs on both builds:

| Scenario | Reviewed `3387915b` | Corrected `f29adaed` |
|---|---|---|
| Calendar-shaped control | BLOCKED 71/95 (C7 rejects 8760) | PASS 95/95 |
| Box-like (no heating signal, 24-way tie) | BLOCKED 71/95 | PASS 95/95 |
| Earlier compact 24-element layout | **PASS 95/95** (unmeasured layout accepted) | BLOCKED 71/95 (C7) |
| Day shifted / `yearDay` +1 | BLOCKED (length) | BLOCKED 83/95 (C7 mask) |
| Native acc1 box reference | 47 rows derived, 12 BLOCKED (C7) | 59 rows derived |

Reviewed-script R3/R4 evidence is static only: that script calls the real `Clear-Tas`, so it was not executed. It shows:
- the simulation step has no `--sam-commit`/`--runner-commit`;
- every `Step` return value is discarded.

Machine-local evidence, `SAM-BIM/.s10-r3-correction-20261023/`:
- `rebuild.txt`, `*-tests.txt`, `test-results/` (TRX), `binaries-sha256.txt`, `tested-head.txt`;
- `replay/`;
- `probe/` (source, generated scenarios, `results-*.json`, consoles);
- `reviewed-build/` (the reviewed test build and script);
- `reviewed-script-static.txt`, `preservation-check.txt`.

Commands (from the SAM-BIM root):

```text
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" SAM_Tas\SAM_Tas.sln -t:Rebuild -p:Configuration=Release -m -nr:false -v:m
dotnet test SAM_Tas/benchmark/SAM.Analytical.Tas.Benchmark.Tests/SAM.Analytical.Tas.Benchmark.Tests.csproj -c Release --no-build
dotnet test SAM_Tas/benchmark/SAM.Analytical.InputEquivalence.Tests/SAM.Analytical.InputEquivalence.Tests.csproj -c Release --no-build
dotnet test SAM_Tas/SAM_Tas/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests.csproj -c Release --no-build
SAM_Tas\SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\bin\Release\direct-t3d-validation.exe results-compare C:\TasOut\s10\acc1\s08-fix-box\read C:\TasOut\s10\acc1\s08-fix-box\subject.json <new empty folder>
```

### Revision 1 correction (accepted for measurement; superseded by the revision-2 correction)

Local Windows workstation (2026-10-23), no Tas licence used, no COM read, no simulation. **Tested content SHA `3387915b`**
(`3387915ba63c10ab5723ac73f625074e0cdbf942`), clean tree. The record-only commit after it changes this file only. Final
head and hosted checks: PR description. Diff `77ac1cda..3387915b` touches no producer, golden, importer, S08 reader,
calibration table, fixture or workflow file (checked with `git diff --quiet`).

| Check (local, at `3387915b`) | Result |
|---|---|
| `SAM_Tas.sln` Restore + Rebuild, Release, VS MSBuild 18 | exit 0, 0 errors; the CS warnings are all in untouched pre-existing projects (UKBR, SAP, TPD, GenOpt, Tas), none in S10 files |
| Producer tests | 40/40 (unchanged; golden byte-identical) |
| Core `SAM.Analytical.InputEquivalence.Tests` | 258/258 |
| Read-back tests | 196/196 = the 178 of revision 1 + 18 new in `S10DesignDayTests`, 0 failed, 0 skipped (includes every S08 offline replay and the full negative matrix) |
| Negative matrix (synthetic triple, also through the built harness `results-negatives`) | control PASS + 40 cases as intended, exit 0 |
| Built harness `results-compare` on the synthetic triple's files | PASS 95/95, exit 0 |
| Review probe, re-run on this build | control PASS 95/95 (C7/C9/C10 verified); lower bounds 0, 2, -23 BLOCKED 91/95; all data sets missing BLOCKED 71/95 |
| New regressions on the reviewed code `77ac1cda` | 7 of 9 fail as intended (see "Revision 1 correction") |
| Artefacts (build-specific; builds are not byte-reproducible) | `direct-t3d-validation.dll` sha256 `888348d337586cb7…`, `benchmark-tas.dll` `5a73217d4727db30…`, rules `766a88cdbf129499…` (frozen S08 table, unchanged) |

Not run locally: TM59 (1249) and GenOpt (432) suites; those projects are untouched and hosted CI runs them. Machine-local
evidence: `SAM-BIM/.s10-r1-correction-20261023/` holds:
- `rebuild.txt` and `*-tests.txt`;
- `harness-smoke/`;
- `probe-rerun/` (review probe with only the coverage argument added; `probe-results.json`, `console.txt`);
- `old-code-regressions/` (the old-API scenarios and their run on `77ac1cda`).

Copy it, with the review folder and `S10_IMPLEMENTATION_PLAN.md`, before changing machines.

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

## Revision 2 (licensed acceptance; only on a fresh owner decision)

Attempt 1 (`acc1`) spent both OD-1 invocations and is preserved; the script refuses its non-empty folder. A further
licensed run needs a fresh owner decision on its scope (which fixtures, how many invocations) and an empty, new root. The
form of the command is:

```text
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Run-S10-Results.ps1 -Phase run -Root <new empty root> -WeatherEpw C:\TasOut\s08c\weather\budapest.epw
powershell -ExecutionPolicy Bypass -File SAM_Tas\SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\Pack-S10-Evidence.ps1 -Root <that root>
```

Per fixture the run executes: one `benchmark-tas` simulation, then `results-read` (TSD + TBD), `results-read-design` (TBD
design-day coverage), `results-subject`, `results-compare` and `results-negatives`, each in its own process. Any
infrastructure failure stops the run (exit 3, `status-run.json`).

Prerequisites:
- licensed Tas 2.0.0.1, Tas GUI closed;
- clean SAM_Tas and SAM checkouts with Release builds no older than their sources (the preflight checks this);
- the Budapest EPW at the given path (`472ffb4e…`).

PASS = both fixtures PASS with complete coverage, every calibration check verified, every negative as intended, and the
produced files and fixtures unchanged by the reads. Evidence: `Documentation/S10-Results-evidence/` (committed); run
folders machine-local.

## Unresolved, risks, limits

- **Revision-2 blockers (current):**
  - No B1a document has been produced by a run, so every B1a row and the negative matrix are unexercised natively.
  - The two-zone fixture has no TSD.
  - The calendar design-day representation is measured on one fixture only.
  - The heating within-day origin is unresolved. Its hour row is derived only through the box's 24-way tie; a fixture
    without that tie would BLOCK it.
  - The new runner preflight has not met a live engine.
  - Both OD-1 invocations are spent; further licensed execution needs a fresh owner decision.
- The list below is the original revision-1 statement of the licensed-only questions. Its design-day series item is
  superseded by "Revision 2 attempt 1" and "Revision 2 correction". Any of these may BLOCK the dependent rows (never
  relaxed):
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
- Machine-local, to copy before changing machines:
  - `SAM-BIM/S10_IMPLEMENTATION_PLAN.md` (the reviewed plan);
  - `C:\TasOut\s08c\weather\budapest.epw`;
  - the attempt-1 run root `C:\TasOut\s10\acc1`;
  - `SAM-BIM/.s10-r2-acceptance-20261023/`, `SAM-BIM/.review-s10-r2-20261023/`, `SAM-BIM/.s10-r3-correction-20261023/`.

## Next step

Independent review of the revision-2 correction (this branch, tested content `f29adaed`), recommended as GPT-6 or Claude
Opus 5.5, effort High, new session, read-only review mode. The calendar evidence method, the full-day-tie hour rule and
the runner preflight/stop control are where a false PASS or a wasted licensed invocation would hide. The review should
cover:
- R3 and R4;
- the evidence-method amendment and the C9 assessment;
- the regression probe and the replay.

Only after that review does the owner decide the minimum further licensed execution. A fresh successful wrapper run is
still needed for end-to-end acceptance, because no B1a and no two-zone TSD exist. Then: Ready, final-head CI, owner merge
and the post-merge closeout.
