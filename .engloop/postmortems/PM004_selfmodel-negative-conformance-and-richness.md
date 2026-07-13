# PM004: An adequate self-model must be behaviorally rich and prove negative conformance

- **Date:** 2026-07-08
- **Duration:** short (design clarification, caught by maintainer review of SEK's self-tests)
- **Covers incidents:** IN004
- **Status:** COMPLETE

## Root cause

PM002/PM003 made the vertical bar concrete: a self-model (`MODEL` + `CORD`) that **generates
conformance**, at behavior granularity, plus per-module coverage. But every clause is satisfiable by
a **hollow** self-model:

- **Existence** is satisfied by any model, however thin — a single boolean of state passes.
- **"Generates conformance"** is satisfied by **positive** witness paths only. SEK's engine explores
  **enabled** transitions (a false guard makes an action *disabled*, hence absent), and
  `Conformance.Replay` only asserts *no exception* — so "conformance" means "every legal action ran",
  never "the SUT correctly **rejects** an illegal one".
- **Coverage** is a code metric orthogonal to whether the model exercises real behavior.

So the gate measures the **presence** of a self-model and its **positive** conformance, not its
**behavioral adequacy**. A consumer can pass with a toy model whose "error tests" are hand-coded
`assert failure` calls stuffed into always-enabled positive actions — model-based testing theatre.
That is the PM001 failure (readiness standing in for proof) one level deeper: *"has a self-model +
green tests"* stood in for *"is genuinely, adversarially self-validated."*

## Five whys

```
Symptom: SEK passed the gate with a 1-bit self-model whose "error" tests are hand-coded asserts, not model-derived.
Why #1: Why did that pass? A: The gate requires a self-model that GENERATES conformance + coverage — it does not grade the model.
Why #2: Why is "generates conformance" weak? A: Conformance is POSITIVE-only (explore enabled transitions; assert no-throw). Illegal sequences are never generated or checked.
Why #3: Why does negative behavior matter for a self-model? A: A model's value IS knowing legal vs illegal orderings + the SUT's error/rejection outcomes; positive-only tests leave error handling unproven.
Why #4: Why did behavioral thinness pass? A: The gate never floors the state space — a single boolean yields flat covering-tours indistinguishable from a script, so exploration adds nothing over hand-writing.
Why #5: Systemic fix? A: Grade the self-model: require (a) model-DERIVED negative conformance (illegal sequence -> asserted expected error), and (b) non-trivial behavioral state (genuine branching), and forbid hand-coded error asserts inside positive actions.
```

## ONE-AND-DONE analysis

- **Concrete bug:** the vertical self-model criterion accepts a positive-only, single-state-bit model
  with hand-coded error assertions.
- **Bug class:** *a verification criterion that checks a technique is PRESENT but not that it is
  EXERCISED adversarially* — invites hollow/positive-only self-validation (theatre).
- **Structural fix (mechanical, verifiable):** the Readiness Gate's **vertical self-model criterion**
  gains three testable clauses, all evidenced in the `COV` Readiness Inventory:
  1. **Negative conformance is required, and model-derived.** The self-model must express **expected
     outcomes**, including **error/rejection outcomes** for actions attempted **out of their legal
     order or with invalid input**, and the generated conformance MUST include **negative tests** that
     drive those illegal sequences and **assert the modelled error**. Positive-only conformance (every
     action ran) is **insufficient**.
  2. **No hand-coded error asserts.** An "error case" whose SUT body simply asserts a failure inside
     an otherwise-positive, always-enabled action is **theatre (PM002 class) and a gate FAIL**. Error
     behaviour must be **derived by the model** (guard/precondition + expected-error outcome), so the
     tool — not the author — produces the negative test.
  3. **Behavioral-richness floor.** The self-model must exercise **non-trivial behavioral state**
     (multiple interacting state variables / real ordering constraints) such that exploration yields
     **materially distinct paths**, not flat single-tour scripts. A model whose reachable graph is a
     single boolean is **not** an adequate behavior model of a stateful vertical.
  - **Unchanged:** coverage stays **per module** (≥ the project's ratified bar); behavior-level
    granularity (PM003) stands; pure value-type modules are still verified like components.

- **Honest immediate consequence (intended):** this **raises** the bar, so any consumer — **including
  SEK** — whose vertical self-model is positive-only / thin is now correctly **NOT READY** until it
  provides model-derived negative conformance and a richer model. This is the point: the gate should
  fail hollow self-validation, not bless it. It also **forces the tool capability** the criterion
  assumes — a model-based tool that cannot generate negative conformance must grow that feature (for
  SEK: explore/annotate illegal transitions + assert expected errors; this is a SEK spec, tracked in
  the SEK repo).

This closes the class: the criterion now grades the self-model's **behavioral adequacy** (branching +
model-derived negative conformance), so a hollow, positive-only self-model is no longer expressible as
"verified".

## Learnings

- **LEARN001** — A self-model criterion must grade the model's **behavioral adequacy** (does it branch;
  does it prove **negative** behavior), not merely its **existence** and **positive** conformance —
  else "has a self-model + green tests" becomes the new narrated-readiness (PM001).
- **LEARN002** — **Model-derived** is the load-bearing word: if a human hand-codes the error assertion,
  the model isn't validating error behaviour — the human is. The tool must generate the negative test
  from the model, or it's theatre.
- **LEARN003** — Positive conformance and negative conformance are **different guarantees**. "Every
  legal action succeeds" says nothing about "every illegal action is correctly rejected"; a gate that
  wants trustworthy self-validation must require both.

## Repair Items

| RPI | Description (ONE-AND-DONE) | Size | Status |
|---|---|---|---|
| RPI001 | `docs/standards.md` Readiness Gate: add the three clauses to the **vertical self-model** criterion — (1) required **model-derived negative conformance** (illegal sequence → asserted expected error), (2) **no hand-coded error asserts** inside positive actions (= FAIL/theatre), (3) **behavioral-richness floor** (non-trivial branching). Coverage/granularity unchanged. | full | DONE |
| RPI002 | `commands/speckit.engloopkit.coverage.md` + the `model`/`explore` guidance + skill: the Readiness Inventory must record, per vertical self-model, **Negative-conformance? (Y/N)** and **Branches? (Y/N)**; a vertical whose self-model is positive-only or single-bit is `FAIL`. Reflect in explore/model guidance that self-models must model expected error outcomes + illegal orderings. | full | DONE |

## Cause-class tags

validation-gap

## References

- Incidents: `.engloop/incidents/IN004_selfmodel-positive-only-and-thin.md`
- Refines: PM003 (behavior-level), PM002 (method-by-class), PM001 (the gate itself)
- Downstream (consumer): SEK must grow a **negative-conformance** capability (explore/annotate
  illegal transitions + assert expected errors) and rebuild `samples/SelfHost` as a rich model —
  tracked as a SEK incident/spec in the SEK repo.
