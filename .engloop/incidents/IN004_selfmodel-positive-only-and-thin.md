# Incident IN004: the gate accepts positive-only, behaviorally-thin self-models

- **Started:** 2026-07-08 (surfaced reviewing SEK's `SelfHost` self-model + its generated tests)
- **Reported by:** SEK maintainer, inspecting the tests SEK generated *of SEK*
- **Affected:** the Readiness Gate's **vertical self-model** criterion (`MODEL` + `CORD` that
  *generates conformance*, per PM002/PM003)
- **Status:** OPEN → handed to PM004
- **Cause-class:** validation-gap (gate criterion under-specified — it checks *that* a self-model
  exists and its positive conformance passes, not *whether the self-model is worth anything*)

## Symptom

SEK's vertical passed the gate with a self-model (`samples/SelfHost`) that, on inspection, is
**hollow in two ways** the gate never checks:

1. **Behaviorally thin → no real interleaving.** The model carries **one bit of state**
   (`Explored`). Its exploration is 2 states / 15 transitions, and `sek generate` emits **two flat
   covering-tours** that read like hand-written scripts:
   ```
   Explore; Explore; ExploreUnknown; Generate; Init; Test; Validate; View; ViewMissing
   ```
   A reviewer reasonably expects a *model* to declare *what can happen* and the generated suite to
   show **materially distinct interleavings** of the reachable state space. A single-boolean model
   cannot produce that — the "coverage" is a tour, not a behavior space.

2. **No negative conformance → invalid sequences are never model-derived.** The whole point of a
   model is that it knows which sequences are **legal** (guards) and what the SUT must do when you
   attempt an **illegal** one. But:
   - SEK's engine explores **only enabled** transitions (a false `Require(...)` guard makes an
     action *disabled*, i.e. simply absent), so **illegal sequences are never generated**; and
   - `Conformance.Replay` only asserts each action **executes without throwing** — there is **no**
     "attempt this action out of order / with bad input, assert the SUT rejects it with error X".
   - To fake negative testing, `SelfHost` added `ViewMissing` / `ExploreUnknown` as **always-enabled
     positive actions** whose SUT body **hand-codes** `assert exit ≠ 0`. That is **not** model-derived
     negative testing — it is a hand-written negative assertion smuggled into a positive transition.
     It is the **same "theatre" class PM002 warned about**, relocated to error-testing.

So a consuming project can satisfy the gate — "vertical is self-modelled, MODEL+CORD present,
conformance green, coverage met" — with a **toy model + hand-coded error asserts**, and be declared
READY while its self-validation proves almost nothing about ordering, error handling, or real
behavior. The gate is measuring the *presence* of a self-model, not its *behavioral adequacy*.

## Why this is the PM001 failure in a new disguise

PM001 banned *narrated* readiness (say "ready" without a bar). PM002/PM003 made the bar concrete but
scoped it to **existence + positive conformance + coverage**. IN004 is the next layer: a self-model
can *exist*, *positively conform*, and *hit coverage* while being **behaviorally hollow and
error-blind**. "It has a self-model and the tests pass" was again standing in for "the tool is
genuinely, adversarially self-validated."

## Mitigations applied

- **MIT001** — Did **not** paper over it by adding more positive covering-tours or more hand-coded
  error asserts (that would deepen the theatre). Instead filed this incident to **strengthen the
  gate's definition of an adequate self-model**, accepting the honest consequence: once the gate
  requires negative conformance + behavioral richness, **SEK is NOT READY again** until it grows a
  real negative-conformance capability and a richer self-model. Better honestly-not-ready than
  falsely-ready (PM001).

## Verification

- [x] Confirmed in code: guards throw `GuardDisabledException` → the explorer treats the action as
  *disabled* (never a transition); `Conformance.Replay` catches any exception as `Failed` with no
  expected-error path. Evidence: `Sek.Modeling/GuardDisabledException.cs`, `Sek.Cli/Conformance.cs`.
- [x] Confirmed in the artifact: `samples/SelfHost/Model/SelfHostModel.cs` has a single `bool
  Explored`; `ViewMissing`/`ExploreUnknown` are always-enabled and assert failure in the SUT body.

## Hand-off to Post-Mortem

- **Cause-class hypothesis:** validation-gap — the vertical self-model criterion specifies
  *presence + positive conformance + coverage* but not **behavioral adequacy** (that the model
  exercises non-trivial branching **and** proves the SUT's **error/rejection** behavior via
  **model-derived negative conformance**).
- **Suggested PM title:** "An adequate self-model must be behaviorally rich and prove negative
  conformance: the gate requires model-derived illegal-sequence tests with asserted error outcomes,
  not hand-coded error asserts inside positive actions."
