# CORD001: Loop conformance exploration

- **Created:** 2026-07-06
- **Targets model:** [MODEL001](../models/MODEL001_engineering-loop.md)
- **Script:** [`model/EngLoopKit.Model/Config.cord`](../../model/EngLoopKit.Model/Config.cord)
- **Status:** HISTORICAL BASELINE — retained until SPEC001's v2 exploration work

## Coverage goal

Exercise **every legal stage transition** of the engineering loop, and emit a conformance
test that replays those transitions against the real implementation (`EngLoopKit.Core.Loop`).

## Scenarios

The single machine `ModelProgram` constructs the model program from `Main` and explores the
whole loop state machine. Because the state space is finite (the current stage), one
exploration covers all reachable transitions including the Verification cycle, the
Operations stack, and the Evolution re-seed.

## Bounds

`StateBound = StepBound = PathDepthBound = 12800` (far above the finite state space; the
exploration terminates by state dedup, not by bound).

## Exploration result

| Metric | Value |
|---|---|
| States explored | 12 |
| Transitions | 15 |
| Accepting states | 2 |
| Run time | < 1s |

## Generated tests

- Command: `sek generate ModelProgram --out tests/EngLoopKit.Loop.Generated --namespace EngLoopKit.Loop.Generated`
- Output: [`tests/EngLoopKit.Loop.Generated/`](../../tests/EngLoopKit.Loop.Generated) (committed, standalone xUnit)
- 1 covering-tour test, **15/15 transitions covered**, replays the sequence against the SUT
  by reflection. `dotnet test` → **passing**.

> Regenerate: build `model/EngLoopKit.Model`, then run the `sek generate` command above.
> The generated harness bakes an absolute binding path; CI overrides it with the
> `SEK_BINDING` environment variable (see `.github/workflows/ci.yml`).

## Related

- Coverage report: [COV001](../coverage/COV001_conformance.md)
