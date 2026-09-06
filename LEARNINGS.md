# EngLoopKit Learnings

> The one-page recall map. Start here, follow the relevant subject card, and inspect
> its cited `PMxxx/LEARNxxx` source before changing a consequential engineering rule.

## Owner priorities: user control and release time

- **Never block user-selected agent navigation**, especially Incident → Postmortem.
  Evidence gates govern claims and specific protected operations, not permission to
  enter another conversation. Carry gaps forward; never invent completion.
- **Small releases target under 90 minutes**, not days. Freeze scope, time every phase,
  preflight cheap contracts, prove the actual upgrade, reuse only valid bound evidence,
  and escalate forecast overruns instead of looping.
- These are explicit [owner directives](.engloop/learnings/owner-directives-release-and-navigation.md)
  dated 2026-09-06, not a claim that runtime enforcement has already been repaired.

## Evidence and readiness

- [Readiness is a gate](.engloop/learnings/cards/readiness-is-a-gate.md) — “ready” is
  emitted by whole-product evidence; users remain free to change agents regardless.
- [Verification follows artifact class](.engloop/learnings/cards/verification-follows-artifact-class.md)
  — use unit/property tests for generic components and SEK behavioral proof for the
  domain vertical; keep the quality bar common and releases bounded, timed, and resumable.

## Honest model-based validation

- [Model observable behavior, not internal shape](.engloop/learnings/cards/model-observable-behavior.md)
  — state verification granularity explicitly and model an end-to-end behavior, not
  every pipeline assembly.
- [Adequate models prove rejection](.engloop/learnings/cards/adequate-models-prove-rejection.md)
  — require real branching plus model-derived negative conformance; positive-only
  scripts and hand-coded error assertions are theatre.

## Recall path

1. Pick the cue that matches the decision.
2. Read that one subject card.
3. Follow its source links when applying, challenging, or changing the principle.
4. If no cue fits, do not invent a “learning”; return to the source sequence and run
   the Learnings Pyramid again.