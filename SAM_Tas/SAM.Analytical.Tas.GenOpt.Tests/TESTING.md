# SAM.Analytical.Tas.GenOpt.Tests

Tests for the native GenOpt route (`GenOptDocument.RunNative`): the SAM_Tas compatibility mapping onto the SAM.Math
optimisation kernel and `TasGenExecuteObjectiveEvaluator` (Gate T protocol, `../SAM.Analytical.Tas.GenOpt/TASGENEXECUTE_PROTOCOL.md`).
They need no Tas, COM, licence or Java.

## Build order (required before `dotnet test`)

Same pattern as `SAM.Analytical.Tas.TM59.Tests` (see its `TESTING.md`). This project references the already-built
assemblies through `HintPath`, not `ProjectReference`, because `SAM.Analytical.Tas.GenOpt` references `SAM.Core.Tas`,
whose `<COMReference>` needs the .NET Framework MSBuild:

- `..\..\build\SAM.Analytical.Tas.GenOpt.dll`, `..\..\build\SAM.Core.Tas.dll` (SAM_Tas.sln)
- `..\..\..\SAM\build\SAM.Math.dll`, `..\..\..\SAM\build\SAM.Core.dll`, `..\..\..\SAM\build\SAM.Core.Optimisation.dll`,
  `..\..\..\SAM\build\SAM.Units.dll` (the SAM repository)

Build SAM, then `MSBuild.exe SAM_Tas.sln -restore -p:Configuration=Release`, then:

```bash
dotnet test SAM_Tas/SAM.Analytical.Tas.GenOpt.Tests -c Release
```

## What runs

- **Stub TasGenExecute** (`../SAM.Analytical.Tas.GenOpt.Tests.StubTasGenExecute`), a real child process that follows
  the Gate T protocol. Its project `Script.txt` is a JSON spec: a synthetic objective plus failure, sleep and
  formatting behaviours keyed by simulation number.
- **Golden replays.** The SAM GenOpt 3.1.1 golden traces (recorded from real Java GenOpt) are read from the sibling SAM
  checkout, `../../../SAM/SAM/SAM.Tests/Golden/GenOpt`, which CI also clones. Every case the SAM_Tas GenOpt objects can
  express is replayed end to end through `RunNative` and the stub, bit for bit.
- The full run takes about 3 minutes, because each evaluation is a real process.
- **PR6.** `LegacyRouteRetiredTests` reads the built `SAM.Analytical.Tas.GenOpt.dll` (reflection, metadata, an IL scan)
  to prove the Java route is gone and only `TasGenExecuteObjectiveEvaluator` starts a process.
  `NativeGenOptOutcomeTests` pins the shared result rules with real kernel results (no process).
- **Optimisation Definition (PR4).** `OptimisationDefinitionAdapterTests` read the SAM.Core.Optimisation fixtures from
  the sibling SAM checkout (`../../../SAM/SAM/SAM.Tests/Golden/Optimisation`). For both Systems Demo examples, the
  document built from the definition equals the one the SAM_UI form built (PR5), and a stub run of each is
  evaluation-for-evaluation identical, `Variables.txt` bytes included. `TasScriptCatalogueTests` cover the script scan
  and its warnings. `OptimisationDeploymentTests` load the built assemblies in a load context that refuses
  SAM.Core.Optimisation, to prove the existing route never needs it.
