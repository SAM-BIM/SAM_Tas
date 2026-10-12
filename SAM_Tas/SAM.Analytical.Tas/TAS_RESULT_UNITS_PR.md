<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Tas consumption/peak units at the source, option A of SAM_Tas#92 (SAM_Tas)

Branch `fix/tas-result-units-kwh` → base `sow/2026-Q4` (cut from `f16b5d2c`). Record date: 2026-10-12. Part of a coordinated
set of four PRs; the design, compatibility table and merge order are recorded in SAM's
`documentation/TasResultUnits-PR.md` (record of record). **This PR merges last.**

## Current status

PRs of the set: SAM-BIM/SAM#192 (1, marker and query), SAM-BIM/SAM_Tas_Grasshopper#13 (2), SAM-BIM/SAM_OpenStudio#26 (2), SAM-BIM/SAM_Tas#98 (3, last).

PR open, **not merged**. Depends on the SAM PR (`fix/tas-result-units-kwh`: `ResultUnitsVersion`,
`Query.TryGetConsumptionOrPeakLoad`, `Modify.SetResultUnitsVersion`); its CI builds against SAM's same-named branch until
that merges. Awaiting final-head CI and **owner review**.

## Work completed

- **Source** (`Convert/ToSAM/AnalyticalModelSimulationResult.cs`): the TSD building `heatingProfile`/`coolingProfile` are
  hourly loads in W (SAM_Tas#92). The converter now stores `ConsumptionHeating/Cooling = Σ / 1000` (kWh) and
  `PeakHeating/CoolingLoad = max / 1000` (kW), the hours unchanged, and stamps `ResultUnitsVersion = 1`. The arithmetic
  moved into a COM-free overload `ToSAM_AnalyticalModelSimulationResult(name, reference, heatingProfile, coolingProfile,
  floorArea, volume)` so it is tested without Tas; the TSD overload only reads and delegates. `Source` stays the assembly
  name `SAM.Analytical.Tas`, which SAM uses to recognise legacy (unmarked, Wh/W) results.
- **Benchmark** (`benchmark/.../Modify/ToBenchmark.cs`): the `Kilo()` ÷ 1000 on model consumption and peaks is replaced by
  SAM's `TryGetConsumptionOrPeakLoad`, which returns kWh/kW for a marked result as stored and converts a legacy
  result exactly once; unknown units → `available:false`. Per-space loads (W) unchanged. Docs updated
  (`TasBenchmarkContext`, the CLI comment, `benchmark/README.md` "Units"). `Producer.HasModelAnnualEnergy` checks
  finiteness only and is unchanged.
- **Not changed, by instruction:** `SAM.Analytical.Tas.GenOpt` (PR7a/7b measures divide the raw TSD by 1000 in their own
  generated script; they never read these parameters: grep-verified); `Create.TPD`'s plant result (different parameters:
  `AnnualTotalConsumption` etc.); the spike drivers under `spikes/` (historical evidence code; the PR7b-2 driver's text
  "labelled kWh, holds Wh" describes the pre-correction build it was run against).
- Persisted results are not migrated.

## Owner decisions

As in SAM's record (marker, provenance by exact `Source`, unknown units refused, merge order SAM → (SAM_Tas_Grasshopper,
SAM_OpenStudio) → **SAM_Tas**). Specific here: the benchmark publishes the same kWh/kW for legacy and new results; the
committed golden is unchanged.

## Files changed

- `SAM_Tas/SAM.Analytical.Tas/Convert/ToSAM/AnalyticalModelSimulationResult.cs` (+ SPDX header).
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/BuildingResultUnitsTests.cs` (new; that project is the existing home of the
  COM-free `SAM.Analytical.Tas` tests and runs in CI).
- `benchmark/SAM.Analytical.Tas.Benchmark/Modify/ToBenchmark.cs`, `.../Classes/TasBenchmarkContext.cs`,
  `benchmark/SAM.Analytical.Tas.Benchmark.Cli/Program.cs` (comment), `benchmark/README.md`.
- `benchmark/SAM.Analytical.Tas.Benchmark.Tests/Fixtures/BenchmarkFixture.cs` (`MarkedModelResult`), `OfflineGoldenTests.cs`
  (comments), `ResultUnitsTests.cs` (new). `Fixtures/golden-tas-benchmark.json` **unchanged**.
- `SAM_Tas/SAM.Analytical.Tas/TAS_RESULT_UNITS_PR.md` (this record).

## Validation

- `SAM_Tas.sln` Release (VS 18 MSBuild) on the SAM branch build: 0 errors. Test bins load the rebuilt DLLs
  (`SAM.Analytical.Tas.dll` SHA-256 `5e380082…`, `SAM.Analytical.dll` `6e5658aa…`, identical in `build\` and the bins).
- `SAM.Analytical.Tas.TM59.Tests` **1132/1132** (4 new: kWh/kW stored, marker, hours; `Source` = assembly name = SAM's
  constant; new result reads bit-identical to the legacy result from the same profiles; recorded #92 numbers).
- `SAM.Analytical.Tas.Benchmark.Tests` **23/23** (7 new): the marked result produces the committed golden byte for byte,
  equal to the legacy result's bytes; marked values are not divided again; unknown producer and unsupported marker →
  consumption/peaks unavailable, floor area still published. The existing golden test passes unchanged.
- `SAM.Analytical.Tas.GenOpt.Tests` **426/426** (unchanged; confirms the optimisation route is untouched).
- Mutation: 5/5 valid mutants killed (converter stores raw Wh but marks; divides but does not mark; peak not divided;
  benchmark divides again; benchmark reads the raw parameter). One mutant that did not compile was discarded and redone.
- **Licensed confirmation** (this workstation, Tas; **copies** of the #92 TSDs, SHA-256 `40f169af…` Systems Demo and
  `16812cfc…` SAM-generated model; probe and logs `C:\TasOut\units-fix`, local): the corrected converter stores
  15 227.840663709641 kWh heating, 2 978.5325259896517 kWh cooling, 38.72740625 kW peak heating (Demo) and
  2 523.7518729965686 kWh cooling (SAM model), each **bit-identical** to the TSD's hourly sum/max ÷ 1000; the hourly sums
  equal the #92 recorded values bit for bit; marker 1, `Source = SAM.Analytical.Tas`; the legacy reading of the same run
  is bit-identical; DesignExplorer rows equal (Demo `ConsumptionHTG[kWh] 15227.84`, `PeakHTGLoad[kW] 38.73`,
  `PeakHTGLoad[W/m2] 42.93`). Both **RESULT PASS**; no Tas process left.
- PR CI (`build`, `spdx`): see the PR.

## Tested content, local versus GitHub checks, reproduction

- PR SAM-BIM/SAM_Tas#98, base `sow/2026-Q4`. **Validated content SHA `36fb21d4c632d65a543a31ab32d905c6cbadea52`**, built
  against SAM `b28daa77` (SAM-BIM/SAM#192); later commits are record-only. The final PR head and its GitHub checks are named in
  the PR description.
- "Validation" above is **local** (this laptop, VS 18 MSBuild, .NET 8, licensed Tas) unless it says "PR CI"; the GitHub
  checks are `build` (which runs the benchmark, TM59 and GenOpt suites offline) and `spdx`.
- Reproduce (SAM built first from its PR branch; MSBuild = `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`):

  ```
  MSBuild SAM_Tas.sln -restore -p:Configuration=Release -m
  dotnet test SAM_Tas/SAM.Analytical.Tas.TM59.Tests/SAM.Analytical.Tas.TM59.Tests.csproj -c Release            # 1132/1132
  dotnet test benchmark/SAM.Analytical.Tas.Benchmark.Tests/SAM.Analytical.Tas.Benchmark.Tests.csproj -c Release --filter "TestCategory!=Simulation"   # 23/23
  dotnet test SAM_Tas/SAM.Analytical.Tas.GenOpt.Tests/SAM.Analytical.Tas.GenOpt.Tests.csproj -c Release --filter "TestCategory!=Simulation"          # 426/426
  ```

- Licensed confirmation (machine-local, not committed; prerequisites: licensed Tas, the two #92 TSD copies). Probe source
  `C:\TasOut\units-fix\probe` (references this checkout's `build\`), built with `dotnet build -c Release -o ..\bin`, run on
  copies in `C:\TasOut\units-fix\demo` and `\sam`:

  ```
  UnitsProbe.exe confirm "C:\TasOut\units-fix\demo\Systems Training.tsd" 15227840.66370964 2978532.5259896517 38727.40625
  UnitsProbe.exe confirm "C:\TasOut\units-fix\sam\000000_SAM_AnalyticalModel.tsd" 0 2523751.8729965687 -
  ```

  Logs: `C:\TasOut\units-fix\demo-confirm.log`, `sam-confirm.log` (both `RESULT PASS`).

## Unresolved issues and risks

- Merge **after** SAM_Tas_Grasshopper: its pre-change component would divide the new kWh by 1000 again.
- Any external script that divided SAM_Tas consumption/peaks by 1000 itself becomes 1000× too small for new results
  (inherent to option A); old persisted results remain Wh/W, read correctly through the SAM query.

## Next step

Owner review. Merge SAM, then SAM_Tas_Grasshopper and SAM_OpenStudio, rerun this PR's CI against merged Q4, merge this
PR; closeout on `sow/2026-Q4` (docs-only, `[skip ci]`).
