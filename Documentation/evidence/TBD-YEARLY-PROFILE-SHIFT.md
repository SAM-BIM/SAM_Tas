# TBD yearly profiles were written one hour early

Licensed TAS, 2026-09-10 (convention probe, during SAM#111 PR3) and 2026-09-11 (production confirmation and
fix). Branch `fix/tbd-yearly-profile-one-hour-shift` off `sow/2026-Q3`.

## The TAS convention

`TBD.profile.SetYearlyValues(object)` given a `float[]` **ignores element 0**, stores element `i` in 1-based
slot `i`, and repeats the last element into any slot the array does not reach
([probe log](TBD-YEARLY-probe-yearly.log)):

| array | slot 1 | slot 8759 | slot 8760 |
|---|---|---|---|
| `float[8760]`, element i = 1000+i | 1001 | 9759 | **9759** (repeated) |
| `float[8761]`, element i = 1000+i | 1001 | 9759 | 9760 (exact) |

`yearlyValues[h] = v` for h = 1..8760 is unambiguous. `GetYearlyValues()` answers `Single[*]` bounded 1..8760.

## Confirmed in production

Probe modes `samupdate` / `samacci` ([source](TBD-YEARLY-probe-Program.cs.txt)) call the real
`SAM.Analytical.Tas.dll` on copies of the PR3 source TBD. A SAM yearly `Profile` whose 0-based hour k carries
1000+k is written through `Modify.Update` to a thermostat slot (ticUL) and a gain slot (ticOSG), saved,
reopened read-only and read back. `UpdateACCI` is compared hour by hour against
`Weather.Query.DryBulbTemperatureRange` of the building's own weather.

| check | before | after |
|---|---|---|
| `Modify.Update`: slots != SAM hour h-1 | **8759 / 8760** (slot 1 = 1001, slot 8760 = 9759) | 0 / 8760 |
| SAM import (`Core.Tas.Query.Values`) hour 0 | 1001 | 1000 |
| `UpdateACCI` ticUL, slots != range(hour h-1), every IC | **4232** (0 against hour h, i.e. one hour early) | 0 |

Logs: [samupdate before](TBD-YEARLY-probe-samupdate-before.log), [after](TBD-YEARLY-probe-samupdate-after.log);
[samacci before](TBD-YEARLY-probe-samacci-before.log), [after](TBD-YEARLY-probe-samacci-after.log).

The import is correct (slot k+1 -> hour k). Because the export was not, every export -> import generation
moved a yearly profile one more hour earlier.

## Fix

`Modify.UpdateYearlyValues(TBD.profile, IList<float>)` writes 0-based hour k into slot k+1 through an
8761-long array with element 0 unused. It is still one COM call, and it refuses anything that isn't exactly 8760
hours. `Modify.Update` (yearly branch) and both `UpdateACCI` paths (fast and splice) go through it.

## What is affected

- Every yearly TBD profile (a SAM `Profile` with more than 24 values) SAM exported through `Modify.Update`:
  internal-condition gains and thermostat/humidistat limits, AHU thermostat profiles (`UpdateIZAMs`, e.g.
  from *CreateIZAMBySetPoint*), and IZAM flow profiles (`UpdateIZAMProfile`).
- Every thermostat written by `Modify.UpdateACCI` (the *TasUpdateAdaptiveSetpointACCI* component).
- Not affected: constant (`Count == 1`) and daily 24-hour profiles, which take the value and hourly
  branches. That covers the TM59 schedules. Also unaffected: the PR3 thermostat bridge (`WriteThermostatBridge`,
  per-element writes) and the legacy `CalculateResultantTemperature` (already 8761).

## TPD plant schedules follow a different convention and are correct as written

`TPD.PlantSchedule.SetYearlyValues(int[])` is **0-based and exact** ([probe log](TBD-YEARLY-probe-tpdsched.log)).
`GetYearlyValue(h)` answers 0, so the base was read through `GetNumMonthlyOperableHours`:

| `int[8760]`, one element = 1 | operable hours | month counting it |
|---|---|---|
| element 0 | 1 | January |
| element 744 (0-based first hour of February) | 1 | February |
| element 8759 | 1 | December |

An `int[8761]` drops its element 8760. `Modify.Add(EnergyCentre, ISchedule)`'s `int[8760]` is therefore right
and must not get the TBD fix. The production constant schedule reads 8760 operable hours.

## Tests

`YearlyProfileAlignmentTests` runs the real `Modify.Update` and `Core.Tas.Query.Values` against
`TasProfileFakes.FakeProfile`, which now models the measured behaviour. The previous fake copied from element 0
and passed the shifted writer. Against the pre-fix DLL, 3 tests failed (8759/8760 slots misaligned; generation 1
off by one hour). With the fix, 869/869 pass on base `5bec3a6d`.

## Re-validated on `sow/2026-Q3` @ `81d78841` (after PR #51, the PR3 thermostat bridge)

PR #51 touches none of this fix's files. Its only TBD yearly writes are the bridge's per-slot
`yearlyValues[hour]` for hour = 1..8760, which aren't affected. After a clean MSBuild of `SAM_Tas.sln`:

- `dotnet test SAM.Analytical.Tas.TM59.Tests`: 890/890 pass, including PR #51's `ThermostatBridgeTests`.
- Licensed, same probes, fresh copies ([samupdate](TBD-YEARLY-probe-samupdate-rebased.log),
  [samacci](TBD-YEARLY-probe-samacci-rebased.log), [tpdsched](TBD-YEARLY-probe-tpdsched-rebased.log)):
  `Modify.Update` 0/8760 slots misaligned in both slots; `UpdateACCI` aligned in all 27 yearly ICs; TPD
  plant schedules still 0-based and exact.
