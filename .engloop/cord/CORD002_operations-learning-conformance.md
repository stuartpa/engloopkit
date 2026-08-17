# CORD002: Operations learning/gate conformance exploration

- **Created:** 2026-08-15
- **Targets model:** [MODEL002](../models/MODEL002_operations-learning-gates.md)
- **Script:** [`model/EngLoopKit.Model/Config.cord`](../../model/EngLoopKit.Model/Config.cord)
- **Status:** CURRENT

## Coverage goal

Exercise every legal ordered-v2 transition while generating illegal-action rejection
tests for direction consultation, learning consultation/deferral, PM pyramid validation,
and repair Rule-ID/executable-gate acceptance.

## Fresh generation contract

`scripts/generate-loop-tests.ps1` first builds the current SUT and model binaries, then
generates/replays a standalone portable suite. This prevents a stale DLL from being
reported as fresh source evidence.

## Exploration result

| Metric | Value |
|---|---:|
| States explored | 230 |
| Transitions | 635 |
| Positive covering paths | 11 |
| Model-derived negative tests | 3,311 |
| Transition coverage | 635 / 635 |

All **3,322** generated tests pass against the snapshotted real `EngLoopKit.Core.Loop`
binding. Generated rejection evidence includes:

- missing North Star consultation before Incident;
- missing relevant learning consultation/deferral before Incident;
- PM learning validation before a stabilized incident;
- Postmortem without validated pyramid disposition/provenance;
- repair learning validation before Postmortem; and
- Repair without current Rule IDs/executable-gate acceptance.

## SEK compatibility resolution

The initial ELK exploration exposed two SEK engine gaps: parameter-specific negative edges
and per-path stateful SUT reset. Both were repaired upstream in **SEK v0.1.2**.
ELK now pins native .NET 10 **SEK v0.1.3**, regenerates through its published tool/Modeling package,
and records the tool hash in freshness evidence. No unresolved SEK defect remains, so a
separate post-release engineering handoff is not required.