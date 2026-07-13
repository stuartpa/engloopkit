# Card: Adequate models prove rejection

- **Recall cue:** Does the model prove what the SUT must reject, or only replay success?
- **Apply when:** Reviewing a state model, CORD exploration, generated conformance
  suite, or readiness evidence for a stateful vertical.
- **Status:** Living
- **Last condensed:** 2026-07-10

## Compressed principle

Model existence and positive conformance do not prove behavioral adequacy. A useful
state model has non-trivial branching and generates tests for both legal success and
illegal rejection. If a human embeds the expected error assertion inside an
always-enabled positive action, the human—not the model—is validating the behavior.

## Decision checks

1. Does reachable state contain interacting variables or real ordering constraints?
2. Do explored paths branch materially rather than form one flat covering tour?
3. Does the model express expected rejection outcomes for illegal order or input?
4. Does the tool generate and assert those negative cases from the model?
5. Are any “negative tests” actually hand-coded assertions hidden in positive actions?
   If yes, readiness fails.

## Source learnings

- [PM004/LEARN001](../../postmortems/PM004_selfmodel-negative-conformance-and-richness.md#learnings)
  — grade behavioral adequacy, not model presence.
- [PM004/LEARN002](../../postmortems/PM004_selfmodel-negative-conformance-and-richness.md#learnings)
  — model-derived negative evidence is load-bearing.
- [PM004/LEARN003](../../postmortems/PM004_selfmodel-negative-conformance-and-richness.md#learnings)
  — positive and negative conformance prove different guarantees.