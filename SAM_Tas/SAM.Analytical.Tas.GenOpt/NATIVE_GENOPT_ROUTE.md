# Native GenOpt route — PR3 (TasGenExecute evaluator over the SAM.Math kernel)

Branch `feature/native-genopt-tas-evaluator`, base `sow/2026-Q4` @ `60d56621`. This is the third PR of the Java-free
GenOpt replacement:
- SAM#182: PR1, oracle and specification.
- SAM_Tas#85: PR1-T, the TasGenExecute protocol and Gate T.
- SAM#183: PR2, the `SAM.Math` kernel, merged `0989ad81`.

## 1. Status

Implementation, automated validation and licensed acceptance complete. Awaiting owner review. Not merged.

## 2. Work completed

`GenOptDocument.RunNative(runsDirectory, tasGenExecutePath, progress, cancellationToken)` maps the existing GenOpt
objects onto the merged SAM.Math kernel. Evaluation runs `TasGenExecute.exe` directly, following the Gate T protocol
(`TASGENEXECUTE_PROTOCOL.md` §2.3). Java, GenOpt, cmd.exe and the Tas Manager registry are not used. The Java route
`Run()` is unchanged (until PR6).

| Type (SAM.Analytical.Tas.GenOpt) | Role |
|---|---|
| `GenOptNumber` | The doubles Java GenOpt reads from the Java writer's command-file text. The writer produces .NET `ToString()` text; GenOpt reads it with `StreamTokenizer` arithmetic: inexact digit accumulation, double rounding of exponents, `-0` → `0` (D1). Exponent text in algorithm keywords is refused (D2). |
| `Convert.ToSAM_Optimiser` | `GPSHookeJeevesAlgorithm` → `HookeJeeves`; `GoldenSectionAlgorithm` → `GoldenSection` (AbsDiffFunction mode, exactly one parameter). `GPSCoordinateSearchAlgorithm` is refused with a clear message (D3). Every other algorithm type or class throws `NotSupportedException`, naming the type and the Phase-1 list; nothing is substituted. GPS mesh values must be integers in GenOpt's domain (divider > 1, s0 ≥ 0, t > 0, m > 0) and are never rounded. OptimizationSettings: `MaxIte` → simulation limit; `MaxEqualResults` ≥ 2; `WriteStepNumber = true` refused; `UnitsOfExecution` accepted, never parallel. |
| `Convert.ToSAM_OptimisationProblem` | NumberParameters only. Names must be present, unique and free of `,` or line breaks. One kernel output per objective; the first is minimised. |
| `NativeGenOptWorkspace` | One unique folder per run, holding `project` (the immutable snapshot passed as `args[0]`) and `evaluations`. The snapshot is an **allow-list**: `Script.txt` (written by the same `ScriptFile` writer as the Java route) plus the workspace's top-level T3D/TBD/TPD/TSD/TWD files. Those are exactly TasGenComm's `TasFiles.TasExtension`, the types TasGenExecute itself discovers (read by reflection from the installed `TasGenComm.dll`). No stale runtime or GenOpt artifacts are carried over. |
| `TasGenExecuteObjectiveEvaluator` | One fresh working folder per attempt: `evaluations\NNNN`, and `NNNN-retry` for the kernel's single retry. It writes `Variables.txt` as `Name,<R value>,Min,Max,Step,System.Double`, invariant, joined with `\n`. **Failure** (`ObjectiveEvaluation.Failure`) on a non-zero exit, `Error.txt` with any non-whitespace content (Tas leaves a 0-byte `Error.txt` on success), or a missing or unparseable objective. Parsing takes the last line containing the delimiter, then the text after the delimiter's last occurrence, with invariant numbers; comma decimals are refused. The kernel owns retry-once-then-stop; the evaluator never retries. **Cancellation is cooperative**: a running TasGenExecute is never killed, and the kernel stops before the next simulation. |
| `NativeGenOptRun`, `GenOptCompatibilityException` | Run result (kernel `OptimisationResult` plus run folders and names); refusal type. |
| `GenOptDocument` | `RunNative` and an `OptimizationSettings` accessor were added. Both are additive; `Run()` is byte-for-byte unchanged. |

## 3. Owner decisions and the PR3 Java-only oracle

The oracle ran real `genopt.jar` 3.1.1 under an explicit Temurin JRE 8, with the oracle's fake simulator. `Command.txt`
was produced by the **existing SAM_Tas `CommandFile` writer**. Tas was not involved. The harness lived in `C:\TasOut\pr3-oracle`
and was not committed.

| Question | Observed (GenOpt 3.1.1) | Implemented |
|---|---|---|
| D3: GPSCoordinateSearch with Tas defaults; with Seed 0 / NumberOfInitialPoint 0 and a valid mesh; with Seed 7 / NumberOfInitialPoint 5 | **All rejected**: "Unknown or unexpected keyword 'Seed'" and "… 'NumberOfInitialPoint'" (exit 1). The writer always emits both, and there is no `MultiStart`, so the Java route can never run GPSCoordinateSearch. | Mirrored: refused with that reason. |
| D2: `AbsDiffFunction = 1e-5` (written as `1E-05`) | "Expected ';', got 'E-05'", then "Missing specification of 'AbsDiffFunction'" | Refused. `0.0001` and `1` are accepted; `= 1` replays golden `e10-gs-absdiff` exactly. |
| Mesh domain (via HJ) | Divider 0 and 1 rejected ("> 1"); s0 −1 rejected ("≥ 0"); t 0 and m 0 rejected ("> 0"). **Fractions are silently truncated**: divider 2.5 and s0 0.5 replay identical to 2 and 0. | Domain enforced. Fractions are **refused**, not truncated (owner: never repair); this is a documented strictness beyond Java. |
| `MaxEqualResults = 1` | Rejected (GenOpt's message misnames it as `WriteStepNumber`) | Refused. |
| Java writer number formats (D1) | Accepted: `E+20`, `E+25`, `E-05`, `-0`, 17 digits. A GoldenSection probe (bounds are not rounded) matched the `GenOptNumber` model **bit for bit** on 5/5 cases, e.g. `1.5E-30` is read as `1.5000000000000001E-30`. | `GenOptNumber`, pinned by tests with these values. |
| HJ through the Tas writer (`UnitsOfExecution = 0`) | Identical to golden `e1-hj-quad-2d` | — |

## 4. Validation

### Automated (no Tas, COM, licence or Java)

- New `SAM.Analytical.Tas.GenOpt.Tests` (NUnit, net8.0, HintPath pattern as TM59) with the stub
  `SAM.Analytical.Tas.GenOpt.Tests.StubTasGenExecute`, a real child process following the Gate T protocol: **173/173**.
  It covers:
  - Java-number parity, using the oracle values;
  - mapping of all supported algorithms, every non-Phase-1 `AlgorithmType` (9 of 12, enumerated from the enum) and
    every non-Phase-1 algorithm class (including `MultiStartGPSAlgorithm`);
  - GoldenSection's one-parameter rule, invalid meshes, exponent keywords, the D3 refusal and the settings;
  - `Variables.txt` (ordinary values, ±0, large and small values, Java-mapped values, a comma culture);
  - the snapshot allow-list and isolation, and unique evaluation and retry folders;
  - `Output.txt` parsing (last line wins, multiple outputs, NaN/exponent, malformed values, comma decimals, missing
    objectives);
  - `Error.txt` (non-empty, lowercase compile error, whitespace-only) and non-zero exits;
  - each failure class through a real process;
  - retry-once-then-stop through the kernel;
  - **cancellation that lets the running stub (2.5 s) finish and starts no further evaluation**;
  - an unchanged HKCU `EDSL\TasManager`;
  - **25 merged SAM golden traces replayed end to end through `RunNative` + stub, bit for bit**. The other 6 cannot be
    expressed with the SAM_Tas objects (unbounded, IntervalReduction/no-keyword/nullspace golden section), or are
    refused (`e13` D3, `ec-gs-collapse` D2), as asserted.
- Mutation check: 5 adapter mutations, each caught by targeted tests:
  - no Java-text emulation;
  - `Error.txt` ignored;
  - first `Result` line wins;
  - retry reuses the folder;
  - the snapshot copies every file.
- `MSBuild SAM_Tas.sln /t:Rebuild` Release (VS 18): 0 errors.
- Existing suites: TM59 1128/1128 and Benchmark 16/16. TM59 shows 12 environmental failures only when
  APPDATA/USERPROFILE are redirected; with the normal profile it is 1128/1128.
- PR CI: see the PR.

### Licensed acceptance (Systems Demo, local; nothing licensed committed)

Source: the Gate T project copy (`Systems Training` TBD/TPD/T3D plus the simulated TSD, and the Demo script with
outputs Result, Cost, CO2). Each run used its own copied workspace. The Java controls were fresh real Java GenOpt
3.1.1 runs under the explicit JRE 8, with no PATH change, no `GenOpt.bat` and no registry. All files came from the
existing SAM_Tas writers, except the simulation command (see §5, finding 1).

**A. GoldenSection** (Setpoint −5…35, AbsDiffFunction 0.1, MaxIte 2000)

| | Java A | Java B | Native |
|---|---|---|---|
| Candidate sequence (11) | reference | identical (bit for bit) | identical (bit for bit) |
| Simulation numbers / sub counters | 1…11 | identical | identical |
| Simulation count | 11 | 11 | 11 |
| Termination | completed successfully | same | `Success` |
| Final interval [lower, upper] | [4.767943850222925, 5.093168600454254] | identical | identical |
| Best point | Setpoint 4.968943799848584, Result 7360.04370117188 | identical | identical |
| Objectives (Result, Cost, CO2), every row | — | bit-identical | bit-identical |

Java-A-vs-Java-B noise floor: **0**, all objectives bit-identical. The first four points equal the 2025-12-05 Tas
Java run (Gate T T6).

**B. GPSHookeJeeves** (Setpoint Ini 10, −5…35, Step 2, mesh 2/0/1/4, MaxIte 2000)

| | Java A | Java B | Native |
|---|---|---|---|
| Candidate sequence: OutputListingAll (41 rows) / Main (10 rows) | reference | identical (bit for bit) | identical (bit for bit) |
| Simulation numbers, main/sub counters, cache-hit rows | reference | identical | identical |
| Simulation count | 16 | 16 | 16 |
| Termination | completed successfully (step reductions exhausted) | same | `Success` |
| Best point (reported minimum) | Setpoint 5.0, Result 7360.04370117188 | identical | identical |
| Objectives (Result, Cost, CO2), every row | — | bit-identical | bit-identical |

Java-A-vs-Java-B noise floor: **0**. The trace exercises the pattern move, direction memory, 4 step reductions
(Δ 2 → 0.125), cache hits and the minimum report.

Run times: GoldenSection 3.4–3.8 min per run, Hooke-Jeeves 5.4–5.7 min per run. Native and Java are equivalent: each
evaluation is a full TasGenExecute TPD simulation. No retries or failures occurred.

## 5. Findings, decisions and deviations

1. **The legacy Java route's simulation command hangs under GenOpt 3.1.1 (pre-existing, not changed).** Through
   `Create.Command`, `GenOptDocument.Run()` writes `cmd /c "start /WAIT /MIN "" "…TasGenExecute.exe" "<ws>"`. GenOpt
   rewrites the `/c` token to a path (`C:\c`), so it launches `cmd C:\c "start …"`. That opens an interactive cmd that
   never returns: the first evaluation hung for more than 30 minutes and TasGenExecute never started. The Java
   controls therefore used a direct command instead: `C:\PROGRA~1\…\TASGEN~1.EXE <workspace>`, with an 8.3 path
   because GenOpt splits on whitespace. That is the same executable, the same `args[0]` and the working directory
   `tmp-genopt-run-N`, i.e. the Gate T protocol, still driven by real Java GenOpt. Per the PR3 instructions, `Run()`
   is not fixed here. This is input for PR6, the Java retirement.
2. GPSCoordinateSearch is refused (D3). It is supported by the kernel but unreachable with the SAM_Tas writer objects.
   Making it usable would need an owner decision: either a native-only acceptance that the Java route cannot match,
   or a writer change in a later PR.
3. Non-integer GPS mesh values are refused, where Java truncates them silently. This is the owner rule against
   repairing settings.
4. `GenOptNumber` uses invariant text. The Java writer uses the current culture and only works with a '.' decimal
   separator.
5. `Variables.txt` writes .NET round-trip text. Java GenOpt writes Java's `Double.toString`. Both parse to the same
   double, as the bit-identical acceptance objectives confirm.
6. The kernel counts a simulation number that was assigned before a cancellation (PR2 contract). After a cancel during
   evaluation N, `Simulations` is N + 1 while folder N + 1 is never created.

## 6. Files changed

- `SAM_Tas/SAM.Analytical.Tas.GenOpt/`:
  - `Classes/Native/{GenOptNumber, GenOptCompatibilityException, NativeGenOptWorkspace, TasGenExecuteObjectiveEvaluator, NativeGenOptRun}.cs`
  - `Convert/{ToSAM_Optimiser, ToSAM_OptimisationProblem}.cs`
  - `Classes/GenOptDocument.cs` (additive), `SAM.Analytical.Tas.GenOpt.csproj` (SAM.Math HintPath)
  - this record
- `SAM_Tas/SAM.Analytical.Tas.GenOpt.Tests/` (new): 7 test files, 2 helpers, `TESTING.md`, csproj.
- `SAM_Tas/SAM.Analytical.Tas.GenOpt.Tests.StubTasGenExecute/` (new).
- `SAM_Tas.sln` (2 projects appended, plus a build dependency), `.github/workflows/build.yml` (test step).
- **Not changed:** SAM / SAM.Math, SAM_UI, Grasshopper, SAM_Deploy, and the legacy Java-route behaviour.

## 7. Unresolved issues and risks

- Finding 1, the legacy command hang: owner awareness for PR6.
- Finding 2, GPSCoordinateSearch: owner decision.
- Acceptance covers the Systems Demo, one parameter. Multi-parameter Tas runs are covered by the stub and golden tests,
  not by licensed runs.
- TasGenExecute's own number parsing and culture behaviour is EDSL's. Under a comma culture, native refuses the result
  (Gate T guidance).

## 8. Next step

Owner review of this PR (findings 1 and 2), then PR CI, then merge, then the `PROJECT_PROGRESS.md` closeout. PR4/PR5
(Grasshopper, SAM_UI) and PR6 (Java retirement) only when requested.
