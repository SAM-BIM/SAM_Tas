# PR2 no-IZAM fail-closed - the Codex P2 finding and its fix

## The finding

Codex's PR-level review of `76d95e8` found that `WorkflowCalculator.Calculate`, in the
`WorkflowSettings.RemoveIZAMs` step, verified whether an IZAM survived the removal sweep but on
survival only recorded a note -

```
Removing IZAMs: an IZAM survived the sweep, so this TBD is NOT IZAM-free.
```

- and continued to save, size and simulate. A building that refused to give up an IZAM would
therefore have been handed to the explicit Systems route as its "no-IZAM" thermal source, and the
ventilation would have been modelled twice: once by the surviving IZAM, once explicitly in TAS
Systems. No downstream result check can see that double-count, which is why the frozen #111
contract makes it a refusal rather than a warning.

## The fix - the smallest change that fails closed

The established failure mechanism in `WorkflowCalculator` is the refusal convention already used by
the contradictory-instructions gate and the warm-start guards: add the refusal to `Notes`, invoke
`Ended`, and return `null`. `Create.NoIzamThermalSource` already records a null return as a failed
call (`RecordCallFailed("the workflow produced no model.")`), so a refused run cannot come back as
an accepted source.

Two production changes, nothing else:

* `Query.IzamSurvivorRefusal(removeIZAMs, izamSurvived)` - the COM-free decision, following the
  `Query.CancelNote` precedent: returns the refusal sentence when the sweep was requested **and** an
  IZAM survived it, null otherwise. The COM half (`Building.GetIZAM(0)`) stays in the calculator,
  which owns the document session; the helper carries only what a test can pin without licensed TAS.
* `WorkflowCalculator` - the surviving-IZAM branch now adds that refusal and returns `null`
  **before** the save, so the surviving-IZAM state is never persisted as this run's output and
  sizing/simulation never run on it. The zero-IZAM path is unchanged: same sweep, same
  verification, same "none remain" note.

No settings, signatures, architecture or caller behaviour change. A run that never asked for the
sweep (`RemoveIZAMs = false`, every existing caller) is untouched by the decision.

## The regression test

`NoIzamSurvivorRefusalTests` (COM-free) pins the decision the defect lived in:

| case | expected |
| --- | --- |
| sweep requested, IZAM survived | refusal, stating the TBD is NOT IZAM-free |
| sweep requested, building clean | null - success preserved |
| sweep not requested, either answer | null - ordinary IZAM-bearing runs unaffected |

The behavioural half - that a surviving IZAM now stops the run before save/size/simulate on a real
`TBD.Building` - follows from the wiring: the calculator returns `null` exactly when the helper
answers non-null, on the same lines as the other refusals, and `NoIzamThermalSource` treats `null`
as failure. Native re-acceptance of the full route is unchanged by this fix: the accepted fixture
sweeps clean, which is the null path.

## Verification

* Release build: SAM (`413215c`), SAM_gbXML, SAM_Systems (`89cf139`), SAM_Tas - all clean.
* `SAM.Analytical.Tas.TM59.Tests`: **861 passed, 0 failed** (includes the 3 new tests).
* `SAM.Analytical.Tas.Benchmark.Tests`: **16 passed, 0 failed**.
* `git diff --check`: clean.
