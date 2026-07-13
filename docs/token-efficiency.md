# Token Efficiency

EngLoopKit is built for engineers who **pay for tokens**. Its design goal is not "use
an LLM everywhere" — it is "use the LLM only where human-grade judgement is
irreducible, and let deterministic engines do everything they can verify for free."

This document states the principle, then lists the concrete mechanisms.

## Principle: put deterministic engines on the Verification component

In Loop Engineering, every loop has a **Verification** step. The cheapest possible
Verification is one a machine can decide with an exit code. EngLoopKit deliberately
routes the *quality-determining* loops through engines instead of models:

| Loop | Verification engine | Tokens spent |
|---|---|---|
| Explore / Coverage | **SEK + Z3** enumerate behaviors and generate tests | ~0 per test case |
| Architecture | `architecture-verify` (rule check) | ~0 per check |
| CI regression | test runner + coverage thresholds | ~0 per run |
| Repair (small) | `tinyspec` single-file flow | a fraction of full SDD |

The LLM's tokens are then concentrated where they actually add value: distilling a
the Northstar, writing a spec, doing 5-whys root-cause analysis, and choosing the next refactor.

## Mechanisms

### 1. Z3-driven test generation instead of LLM-written tests

The single largest saving. Instead of asking a model to imagine test cases (expensive,
and coverage is unverifiable), SEK explores CORD models with Z3 and **generates** test
cases from the explored state space. Coverage becomes a *proof*, not a guess, and it
costs solver time, not tokens. This is why Stage 5 targets "very good functional
coverage but fast execution" — the solver finds the minimal, high-coverage set.

### 2. tinyspec for small repairs

Most repair items are small. Running the full `specify → plan → tasks → implement`
loop on a five-line change produces "30+ files with < 20 lines of actual code". The
`repair` command classifies each RPI and routes small ones to `tinyspec` — one file,
under 80 lines, two commands. The full loop is reserved for changes that earn it.

### 3. YAGNI / "lazy senior developer" pragmatism

architecture-guard's built-in Ponytail Pragmatism actively prevents over-engineered
plans and needless dependencies during specification and implementation. Less
generated scope means fewer tokens generating it and fewer tokens maintaining it.

### 4. Numbered documents as durable Memory

Durable stages write final-vocabulary records (`SPEC/SCAF/ARCH/MODEL/CORD/COV/IN/PM/REFACT`),
while root Northstar/Learnings stay living. Because context
persists as greppable, cross-referenced Memory, later loops **re-load** decisions
instead of **re-deriving** them. Re-derivation is the silent token sink loop
engineering exists to remove.

### 5. Bounded loops

Exploration and refinement loops carry hard iteration/budget caps. A loop that can't
run away can't quietly burn a budget — a core loop-engineering guardrail.

### 6. Right loop for the right clock

The nested-loop structure (minutes / hours / days / month) means the frequent inner
loops are the *cheap* ones (Z3, tests), and the expensive LLM-heavy work (specs,
post-mortems, refactor decisions) happens on the slower clocks where it's amortized.

## A rule of thumb

> If a compiler, a test runner, or a solver can verify it, don't spend a token proving
> it. Spend tokens on the decisions those engines can't make for you.
