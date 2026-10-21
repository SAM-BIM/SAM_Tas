# S08 PR-C revision 2: offline replay of the acc1 comparisons

The 20 persisted comparisons of `C:\TasOut\s08c\acc1` (3 fixture runs and 7 negatives, each under `approved` and
`provisional`) re-run with `ReadBackCompare.Run` built from `5d7c1314` (the unresolved-Ventilation-selection guard), and
checked against the committed reports in `../runs` and `../negatives`. No licensed export or read, simulation, sizing or
recalibration: the persisted snapshots, frozen inputs and TBD/T3D hashes of the licensed run were reused.

**Result: 20 of 20 equal** to the committed reports in status, exit code, the not-pass list and every serialized result
(`replay-results.txt`). The guard changes only the `Supporting` refusal text of the V2 box ("SAM selects a Ventilation
profile"), which the comparison of results does not include.

`checks/`: the six `SelectedVentilationProfileTests` on the reviewed head plus the new test file (4 failed: approved PASS
exit 0 and provisional BLOCKED owner for a missing and a wrong-type selection) and after the fix (6 passed).

```text
dotnet test SAM_Tas/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests/SAM.Analytical.Tas.DirectT3D.ReadBack.Tests.csproj -c Release
dotnet run -c Release --project <copy of Replay.csproj> -- <empty output folder>
```

`Program.cs` carries the repository licence header (added after the replay run; no other change). `Replay.csproj` references the read-back test project's Release output; it needs the machine-local `C:\TasOut\s08c\acc1`.
