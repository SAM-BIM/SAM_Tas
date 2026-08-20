# Project Progress

## Branch
`feature/partf-terminal-transfer-compliance` (PR: SAM_Tas#29 against `sow/2026-Q3`)

## Last updated
2026-08-20 (second pass) - final review cleanup: two P2 Codex findings against `7ef2aff3` investigated
against `296882e7`, both confirmed and fixed; 151/151 tests green Debug + Release.

## Current status
The Part O availability schedule export is **complete and accepted on licensed TAS**. The mapping fix
landed in `7ef2aff3f3f81949be8b15bf6a797848c2800bf2` ("fix: correct TAS availability schedule mapping")
and the acceptance run passed with no warnings. Two further Codex findings on the TPD-full preparation
are implemented and tested. The final review cleanup (below) fixed the WorkflowCalculator stale-notes gate
and made the MultipleOpeningProperties schedule diagnostic per-child. The schedule-removal transition (D3)
remains deferred.

## TAS availability schedule export - accepted
Accepted commit: `7ef2aff3f3f81949be8b15bf6a797848c2800bf2`.

The adapter crosses two independent COM conventions, and crosses them separately:

- `Query.ScheduleValueFromTBD` - COM TRUE comes back as the VARIANT_BOOL bit pattern, so **-1 maps to 1**.
  Every other value passes through unchanged, so an unexpected read-back still fails the caller's
  comparison rather than being taken for "true".
- `Query.ScheduleIndexTBD` - TBD's 24-slot hourly indexed properties are 1-based hour-ending, so
  **SAM hour `h` is TBD slot `h + 1`** and the slots written are 1..24, never 0. Note that TBD's
  COLLECTION getters (`GetSchedule`, `GetBuildingElement`, ...) are 0-based; it is the hourly indexed
  properties, and only those, that start at 1.
- `Modify.SetScheduleValues` is the only place that writes schedule values, and it reads every written
  slot straight back through the same COM object. A mismatch is a **refusal** naming the SAM hour, the TBD
  slot, the written value, the read value and the raw COM value - the schedule is left unassigned rather
  than exporting a control that does not match the model.

Licensed acceptance result:

- 20 schedules requested, **20 read back**, **0 assignment warnings**.
- `PartO_DayOpen_08_23` visually correct in TAS: 00:00-08:00 OFF, 08:00-23:00 ON, 23:00-24:00 OFF.
- **Slot 24** verified separately with `openingHour = 23`, `closingHour = 24`.

Writing 0-based against a 1-based convention had put every hour one slot early - a 15-hour ON block from
07:00 to 22:00 instead of 08:00 to 23:00 - and writing and reading at the same wrong index masked it
completely.

## Completed (this session)
Final review cleanup - two P2 findings from a Codex review of `7ef2aff3`, verified against `296882e7`
and both confirmed:

- **`WorkflowCalculator.Calculate` stale notes.** The validation gate (`analyticalModel == null ||
  WorkflowSettings == null`) returned BEFORE `notes.Clear()`, so a re-used calculator whose next run was
  rejected still exposed the previous run's notes. Fixed by clearing at the top of `Calculate`, ahead of
  the gate. Regression: `WorkflowCalculatorTests` (3 tests; the previous run is simulated by writing the
  private notes list, the only COM-free way to populate it).
- **`SetApertureTypes` / `UpdateBuildingElements` schedule diagnostics checked only `apertureTypes[0]`.**
  For `MultipleOpeningProperties` the returned list is compacted (refused children are absent), so child
  order does not survive partial failure - checking the first entry both falsely reported a missing
  schedule when child 0 was unrestricted and child 1 was scheduled-and-delivered, and hid a later child's
  failure behind child 0's schedule. Fix: the write overload now reports the correspondence
  (`out List<int> childIndices`, parallel to the returned list), each requesting child is read back against
  the aperture type ITS write returned (`ScheduleDeliveryByChild` + `ApertureTypeSchedule`), and the
  requested/written counters count per requesting child. New COM-free seams `Query.OpeningScheduleRequests`
  and `Query.UndeliveredOpeningScheduleRequests` carry the pairing decision; strict refusal behaviour of
  `SetApertureType` is untouched. Regression: `OpeningScheduleDeliveryTests` (14 tests) covering
  Unrestricted+NightClosed, NightClosed+Unrestricted, NightClosed+NightClosed and the child-0-only failure
  modes. Summary note wording adjusted ("opening(s) requested") since counts are now per child.

Earlier this branch (`296882e7` and before):
- `ResultantTemperaturePreparation.Transferred`: a non-null but EMPTY `IndexedDoubles` zone-temperature
  series is now skipped exactly as an absent one is. It counted towards the payload before, so
  `TryBeginSecondPass` considered the transfer usable and copied the TBD, and the COM write beyond that
  seam reads `values.Count` off it - either throwing out of a route whose contract is refusal, or writing
  a default series that is then reported as a systems-aware answer. (Codex 3820973150)
- `ResultantTemperaturePreparation.TryBeginSecondPass`: a failed `File.Copy` - a locked or read-only
  `_TPDThermostat.tbd`, the routine case being a previous run still open in TAS - is now a refusal with
  the payload cleared, not an escaping `IOException`. `Modify.CalculateResultantTemperature` does not
  catch, so it would otherwise surface as a bare exception on a port that reports every other failure as
  a refusal. The design TBD is still never touched. (Codex 3815817512)
- Regression tests for both, in `PreparationBoundaryTests`:
  `AnEmptyZoneTemperatureSeries_IsNotTakenAsAUsablePayload` and
  `ACopyTargetThatCannotBeWritten_IsRefusedRatherThanThrown`.

## Workflow diagnostics
`SAM_Tas_Grasshopper` `f023594ef3f53a9d8d2411b6fe5bc21a9b363ad0` ("chore: clean TAS workflow schedule
diagnostics") removed the verbose per-aperture D1 success commentary. A successful run is quiet; problem
lines still reach the canvas.

## Deferred
- **D3 - schedule-removal transition (Codex 3821601792).** Clearing an obsolete TBD schedule when an
  opening restriction is removed. Not implemented: `TBD.Building` exposes no `RemoveSchedule`, and
  ownership of a schedule cannot be proven across processes because reuse-by-value deliberately adopts
  either a previous export's schedule or a user-authored one. Needs its own design pass. **No transition
  code exists in this repository** - there is no `Query/ScheduleTransition.cs` and no
  `TryResolveScheduleTransition`.
- **D2 - aperture matching.** Parked. `Query.Apertures(...)` already performs the relevant `Face3D.InRange`
  geometry check, and the D2 proposal introduced a tolerance inconsistency. No D2 code exists here.

## Files changed
Final review cleanup (committed 2026-08-20 as "fix: verify opening schedules per child and clear workflow
notes per run" + "docs: record final Part O review cleanup"):
- `SAM_Tas/SAM.Analytical.Tas/Classes/WorkflowCalculator.cs`
- `SAM_Tas/SAM.Analytical.Tas/Modify/SetApertureTypes.cs`
- `SAM_Tas/SAM.Analytical.Tas/Modify/UpdateBuildingElements.cs`
- `SAM_Tas/SAM.Analytical.Tas/Query/OpeningScheduleRequests.cs` (new)
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/WorkflowCalculatorTests.cs` (new, +3)
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/OpeningScheduleDeliveryTests.cs` (new, +14)

Previous session:
- `SAM_Tas/SAM.Analytical.Tas.TPD/Classes/ResultantTemperaturePreparation.cs`
- `SAM_Tas/SAM.Analytical.Tas.TM59.Tests/PreparationBoundaryTests.cs` (+2)
- `PROJECT_PROGRESS.md` (this file)

## Validation
- `SAM_Tas.sln` built with the VS Framework MSBuild in **Debug and Release**: 0 errors. Only the
  pre-existing MSB3270 (MSIL vs AMD64 interop) and MSB3277 (System.Memory unification) warnings.
- `SAM.Analytical.Tas.TM59.Tests` Debug: **151 passed, 0 failed**. Release: **151 passed, 0 failed**
  (was 134; +17 new for the two cleanup findings).
- Note the documented build order: the test project references already-built assemblies by `HintPath`
  (the COM-referencing projects cannot be built by the .NET Core MSBuild), so the SAM libraries and
  `SAM_Tas.sln` must be built before `dotnet test`. See `SAM.Analytical.Tas.TM59.Tests/TESTING.md`.

## Issues / blockers
- None blocking. The real TBD/COM write is not exercisable without an installed TAS; it is covered by the
  licensed acceptance run recorded above, which has now passed.

## Next step
- Cleanup committed and pushed on 2026-08-20; PR #29 description updated (validation counts 134 -> 151,
  licensed acceptance paragraph, superseded "schedules unwritten" statements).
- D3 transition design pass, if it is still wanted.
- The rounds 1-2 Codex backlog on PR #29 was not re-triaged in this pass; a fresh `@codex review` of the
  current head was requested on 2026-08-20 but the connector declined (usage limit / account connection) -
  re-request once the Codex account is usable.
