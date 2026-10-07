# Tas Generic Optimisation ↔ GenOpt protocol, and direct `TasGenExecute.exe` evidence (Gate T)

Status: **evidence for the Java-free GenOpt replacement** (PR1-T; companion of SAM `feature/genopt-oracle-traces`,
whose `documentation/GenOpt-3.1.1-Behaviour.md` specifies the optimiser side). Docs only, no code change.

## 1. How Tas Generic Optimisation drives GenOpt today

Reconstructed from the IL of EDSL `TasGenOpt.exe`, `TasGenExecute.exe` and `TasGenComm.dll` (Tas install folder
`TasGenOpt\`), and checked against a real 2025-12-05 run of EDSL's "GenOpt Systems Demo".

1. **Workspace.** The workspace is a folder holding:
   - `Script.txt` (user C#);
   - `Variables.txt`;
   - `Template.txt`;
   - `TasOutputs.txt` (output names; `Result` is always first);
   - `Algorithm.txt` (UI state, JSON);
   - the Tas files.
2. **Java.** `TasGenOpt.exe` checks for Java with `java -version` on `PATH`. It then writes three GenOpt files:
   - `Config.ini`, which declares the template, input, `Error.txt`, `Output.txt`, `config.txt`, the objective
     delimiters `Name::` and the command file;
   - `Command.txt`, which holds the parameters and `OptimizationSettings` (`MaxIte = 2000`, `MaxEqualResults = 100`,
     `WriteStepNumber = false`, `UnitsOfExecution = 0`, all hard-coded), plus the algorithm keywords;
   - `config.txt`, whose `SimulationStart` command is
     `cmd /c "start /WAIT /MIN "" "<TasGenOpt>\TasGenExecute.exe" "<workspace>"`.
3. **GenOpt launch.** It runs `java -classpath genopt.jar genopt.GenOpt <workspace>\Config.ini`, and watches
   `OutputListingAll.txt` and `tmp-genopt-run-N\Error.txt` with a FileSystemWatcher. The Stop button kills Java only.
4. **Per evaluation**, GenOpt does the following:
   - substitutes `%Name%` in `Template.txt` (CSV `Name,%Name%,Min,Max,Step,System.Double`);
   - writes `tmp-genopt-run-N\Variables.txt`;
   - runs the command with that folder as working directory;
   - reads the last `Result::` line of `Output.txt`;
   - treats `"Error"` in `Error.txt` as a failure.
5. **`TasGenExecute.exe <workspace>`** then:
   - reads `<workspace>\Script.txt` and `<cwd>\Variables.txt`;
   - compiles the script with Roslyn, every time;
   - runs it with `TasFiles` (the workspace's Tas files), `Variables` and `ScriptOutput` globals;
   - appends `Name::value` lines to `<cwd>\Output.txt`, and errors to `<cwd>\Error.txt`.

**What exists only because GenOpt is an external Java program:**
- Java detection and the embedded jar;
- `Config.ini`, `Command.txt` and `config.txt`;
- `%Name%` template substitution;
- the `cmd start` wrapper;
- output-listing scraping and the file watcher.

**What a native optimiser keeps:** a per-evaluation working folder with `Variables.txt`, the `TasGenExecute.exe`
invocation, and parsing of `Output.txt` and `Error.txt`.

## 2. Gate T — direct invocation without Java or GenOpt (2026-10-07, licensed Tas)

### 2.1 Set-up

1. Copy the Systems Demo's `Systems Training.tbd/.tpd/.t3d`, `Script.txt` and `TasOutputs.txt` to a short scratch path
   `<project>`.
2. The Demo ships no TSD, and its script copies `Systems Training.tsd`, so produce one by simulating the copied TBD:
   `TBD.Document.open` then `simulate(1, 365, 0, 1, 0, 0, <tsd>, 1, 0)`. This takes 20 s.
3. For each evaluation, write `Variables.txt` into a fresh `<eval>` folder, then run
   `TasGenExecute.exe "<project>"` with working directory `<eval>`. Do not use `cmd`, Java or GenOpt.
4. The Tas Manager registry keys (`HKCU\SOFTWARE\EDSL\TasManager\CurrentProject`, `TasData`) were **not changed**. They
   pointed at an unrelated folder throughout.

### 2.2 Results

| ID | Question | Result |
|---|---|---|
| T1 | Working directory / project path | The working directory must be the evaluation folder; it receives `Variables.txt`, `Output.txt`, `Error.txt`, `Info.txt`, `Standard.txt` and the script's file copies. `args[0]` = workspace (`Script.txt` and Tas files). With no argument, the working directory is used as the workspace. |
| T2 | Required files | Workspace: `Script.txt` plus the Tas files the script names. Working directory: `Variables.txt`. `TasOutputs.txt` is **not** read by TasGenExecute (same result without it). |
| T3 | Registry / `Modify.SetProjectDirectory` | **Not needed.** It succeeded with `CurrentProject` pointing elsewhere. |
| T4 | Output format | The first line is `<date>::Result::<v>`; later outputs are `Name::<v>` without a date. `double.ToString()` gives 15 significant digits. The file is **appended to** across runs in the same folder; the last `Result::` line is the newest. |
| T5 | Error paths | See below. |
| T6 | Same value as the Java-driven run | **Bit-identical** to the 2025-12-05 Java GenOpt run on all 4 distinct points tried (5 runs). |
| T7 | Two concurrent invocations (separate folders, same workspace) | Both succeeded with the expected values (informational; Phase 1 stays sequential). |

T5 error paths:

| Case | Exit code | `Error.txt` | `Output.txt` |
|---|---|---|---|
| Script throws | 0 | `<date>::Error: (Line n) -> "<message>"` | empty |
| Compile error | 0 | `(l,c): error CSxxxx …` (lowercase "error" only) | empty |
| Missing `Script.txt`, `Variables.txt`, or no argument | `0xE0434352` (unhandled `IOException`) | nothing written | nothing written |

T6 values:

| Setpoint | Result (2025-12-05 Java run) | Result (direct, 2026-10-07) |
|---|---|---|
| 10.278640450004207 | 7392.29388427734 | 7392.29388427734 |
| 19.721359549995796 | 7436.61492919922 | 7436.61492919922 |
| 4.44271909999159 | 7360.33587646484 | 7360.33587646484 |
| 0.8359213500126206 | 7374.48030090332 | 7374.48030090332 |

Each evaluation took about 20–30 s. After an evaluation, `TPD.exe` stays alive for under 10 s.

### 2.3 Consequences for the native `TasGenExecuteObjectiveEvaluator` (PR3)

- Invoke `TasGenExecute.exe "<workspace snapshot>"` directly, with working directory = a fresh, unique evaluation folder.
- Write `Variables.txt` as `Name,<round-trip value>,Min,Max,Step,System.Double` (invariant culture).
- **Failure** = non-zero exit code, **or** non-empty `Error.txt`, **or** no `Result::` line. This is stricter than
  GenOpt's case-sensitive `"Error"` test, but the outcome is the same.
- Read the **last** `Result::` line, taking the text after the last delimiter. Parse it with the invariant culture and
  refuse a comma decimal: TasGenExecute writes with the current culture, and GenOpt would cut `7392,29` at `,`.
- Retry rule as GenOpt (spec §2.4).
- The objective has only 15 significant digits. That is what GenOpt saw too, so parity holds.
- Do not set the Tas Manager registry keys.
- Use short paths (Tas COM refuses very long ones). Before the next evaluation, allow the Tas COM servers to exit or
  wait on them.
- Workspace snapshot and per-evaluation folders are unique per run (plan §5). Concurrency is possible but stays off in
  Phase 1.

## 3. Reproducing

PowerShell, licensed Tas, Tas GUI closed:

```powershell
$exe = 'C:\Program Files\Environmental Design Solutions Ltd\Tas\TasGenOpt\TasGenExecute.exe'
$psi = New-Object Diagnostics.ProcessStartInfo $exe
$psi.Arguments = '"' + $project + '"'; $psi.WorkingDirectory = $eval; $psi.UseShellExecute = $false
$p = [Diagnostics.Process]::Start($psi); $p.WaitForExit(); $p.ExitCode
Get-Content "$eval\Output.txt"; Get-Content "$eval\Error.txt"
```

Do not commit the Demo's Tas files or its script; they are EDSL example material.
