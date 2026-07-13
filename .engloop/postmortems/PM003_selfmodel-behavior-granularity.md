# PM003: Self-model criterion is behavior-level, not per-internal-assembly

- **Date:** 2026-07-06
- **Duration:** short (design clarification, caught applying the gate to a pipeline vertical)
- **Covers incidents:** IN003
- **Status:** COMPLETE

## Root cause

v1.4.0 (PM002) rightly split verification method by module class — components → unit/property;
**vertical → SEK self-model (MODEL + CORD + generated conformance)**. But it left the *granularity* of
"vertical self-model" unstated. A vertical is often a **pipeline** (SEK: `parse → semantic → IR →
explore → generate → conform`) whose assemblies are **stages**, not independent products. Read
literally, the rule demands a bespoke SEK model of each stage (e.g. "a model of the lexer") — which
is tautological **theatre**, the very thing PM002 warned against. Read sensibly, one self-model of
the vertical's **observable behavior** exercises the whole pipeline. The gate didn't say which, so a
consumer either builds theatre or is unsure it has satisfied the criterion.

## Five whys

```
Symptom: "verified by a SEK self-model" is unclear for a multi-stage vertical (per-assembly vs per-behavior?).
Why #1: Q: Why unclear?                         A: PM002 named the METHOD (self-model) but not the GRANULARITY.
Why #2: Q: Why does granularity matter here?    A: A pipeline's assemblies are stages, not standalone SUTs.
Why #3: Q: Why not model each stage?            A: A model of a pure transformation stage just restates it — theatre (PM002).
Why #4: Q: What does model-based testing validate? A: Observable BEHAVIOR end-to-end, not internal stages in isolation (like a compiler's conformance suite, not a "model of the register allocator").
Why #5: Q: Systemic fix?                         A: State the self-model criterion at behavior granularity: ONE representative end-to-end self-model + the tool's conformance loops over real SUTs; keep ≥95% coverage PER module.
```

## ONE-AND-DONE analysis

- **Concrete bug:** unclear whether SEK needs a model per vertical assembly or one behavior model.
- **Bug class:** *a verification criterion stated without the granularity at which it is satisfied* —
  invites either theatre (over-modelling) or under-verification.
- **Structural fix (mechanical, verifiable):** the gate's **vertical self-model criterion is
  behavior-level**:
  - The vertical is verified by **at least one representative SEK self-model** (`MODEL` + `CORD`) whose
    exploration/conformance **exercises the vertical's end-to-end pipeline against a real SUT**, PLUS
    the tool's conformance loops over its samples. Internal pipeline stages are validated
    transitively by that end-to-end flow — a bespoke model per stage is **not** required (and is
    discouraged as theatre).
  - **Unchanged and non-negotiable:** every module (component *and* vertical) still needs **≥95%
    line & branch coverage** (or documented shortfall). Behavior-level self-modelling does **not**
    lower the coverage bar; it only fixes *where* the "modelled + explored" evidence lives.
  - A **pure value-type** vertical module (no observable stateful behavior — e.g. immutable
    containers) is verified like a component (unit/property), not modelled.

This closes the class: the criterion now says exactly what satisfies it, so neither theatre nor
under-verification is expressible.

## Learnings

- **LEARN001** — A verification criterion must state the **granularity** at which it is satisfied, or
  it invites theatre on one side and gaps on the other.
- **LEARN002** — Model-based testing validates a vertical's **observable behavior end-to-end**, not
  each internal pipeline stage; internal stages are covered by coverage + transitive exercise.

## Repair Items

| RPI | Description (ONE-AND-DONE) | Size | Status |
|---|---|---|---|
| RPI001 | `docs/standards.md` Readiness Gate: state the vertical self-model criterion at **behavior granularity** (≥1 representative end-to-end self-model + conformance loops; internal stages transitive; ≥95% coverage stays per-module; pure value-type modules verified like components). | full | DONE |
| RPI002 | `commands/speckit.engloopkit.coverage.md` + the `model`/`explore` guidance + skill: reflect behavior-level self-model granularity; keep the per-module ≥95% inventory. | full | DONE |

## Cause-class tags

validation-gap

## References

- Incidents: `.engloop/incidents/IN003_selfmodel-granularity.md`
- Refines: PM002 (method-by-class) and PM001 (the gate itself)
