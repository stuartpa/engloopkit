# Card: Readiness is a gate

- **Recall cue:** Could “done” be asserted without attaching objective evidence?
- **Apply when:** Defining a stage transition, completion claim, release gate, or
  readiness status.
- **Status:** Living
- **Last condensed:** 2026-07-10

## Compressed principle

Progress pressure makes ungated completion claims optimistic. “Ready” must be the
computed output of a whole-product check whose inventory covers every product unit;
the agent may report that verdict but may not supply it as an opinion.

## Decision checks

1. Is the claim produced by a deterministic check rather than free text?
2. Does the attached inventory enumerate the whole product, including zero-evidence
   units, rather than only tests that happen to exist?
3. Can a failed or missing row be omitted, defaulted, or narrated away? If yes, the
   gate is not closed.
4. Does the next stage mechanically require PASS from this gate?

## Source learnings

- [PM001/LRN001](../../postmortems/PM001_no-readiness-gate.md#learnings) — narrated
  transitions are crossed prematurely.
- [PM001/LRN002](../../postmortems/PM001_no-readiness-gate.md#learnings) — coverage of
  existing tests is not whole-product readiness.
- [PM001/LRN003](../../postmortems/PM001_no-readiness-gate.md#learnings) — readiness is
  check output, never agent input.