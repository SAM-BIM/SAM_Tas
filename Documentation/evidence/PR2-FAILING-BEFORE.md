# PR2 fail-first evidence

Recorded on the PR2 implementation branch `part-o/iteration3-tas-systems-route`, against unmodified
production source, before any fix was applied. Baselines: SAM `413215c`, SAM_Systems `89cf139`,
SAM_Tas `ec7f505`.

## D-1 - `Query.ZoneLoads(SystemComponent)` fixed-index defect

Source: `SAM.Analytical.Tas.TPD/Query/ZoneLoads.cs:26` declares `int index = 1`; the loop at `:33`
advances `i` but `:35` reads `GetZoneLoad(index)`, so `index` never moves. There is also no null skip,
unlike the two `TSDData` overloads at `:42` and `:64`.

`dotnet test --filter FullyQualifiedName~ZoneLoadEnumerationTests` on unmodified source:

```
Failed!  - Failed: 5, Passed: 1, Skipped: 0, Total: 6
```

(The first run of these six also failed for an unrelated reason - the managed fakes were `internal`,
so the C# runtime binder, resolving `dynamic` from the production assembly's context, could not see
their members and reported `'object' does not contain a definition for 'GetZoneLoadCount'`. Making
the fakes `public` removed that and exposed the real defect below.)

The three failures that are the defect:

```
Failed ZoneLoads_ThreeLoadZone_ReturnsThreeDistinctLoads
  Expected: < "Bedroom 1", "Bedroom 2", "Bathroom" >
  But was:  < "Bedroom 1", "Bedroom 1", "Bedroom 1" >

Failed ZoneLoads_ThreeLoadZone_RequestsEveryIndexOnce
  Expected and actual are both List<Int32> with 3 elements
  Expected: 2
  But was:  1            <- index 1 requested three times, never 2 or 3

Failed ZoneLoads_NullLoadInTheMiddle_IsSkippedNotReturned
  Expected: < "First", "Third" >
  But was:  < "First", "First", "First" >
```

A zone carrying three loads answers **the first load three times**. The index-traffic assertion is
made on the COM calls themselves, so the fix cannot be faked by de-duplicating the result afterwards.

`ZoneLoads_SingleLoadZone_IsUnchanged`, `ZoneLoads_EmptyZone_ReturnsEmptyNotNull` and
`ZoneLoads_NullComponent_ReturnsNull` passed before the fix and must keep passing after it - they are
the guard that the fix changes only the multi-load case.
