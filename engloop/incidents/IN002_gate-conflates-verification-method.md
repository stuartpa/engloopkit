# Incident IN002: the Readiness Gate conflates verification *method* with *being verified*

- **Started:** 2026-07-06 (surfaced while driving SEK's own coverage under the v1.3.0 gate)
- **Reported by:** SEK maintainer (user), on reviewing what "modelled + explored" should mean for SEK's own code
- **Affected:** the v1.3.0 Readiness Gate (`coverage` command + `docs/standards.md`) — its per-module criteria
- **Status:** RESOLVED
- **Resolved at:** 2026-07-06 (criteria clarified; gate refined in v1.4.0)
- **Duration:** short (design gap caught before it forced bad work)
- **Cause-class (preliminary):** validation-gap / process-gap

## Symptom

The v1.3.0 gate (from PM001) requires **every** module — including pure `components/*` — to have an
`MDL` (SEK model) **and** a `CRD` (CORD exploration) before a project can be "ready for incidents".
While using it on SEK, two problems appeared:

1. **It demands model-based testing of code that isn't stateful.** SEK's extracted components
   (`Components.Json` canonicalization, `Components.Graphs` reachability, `Components.Random`
   probability gate) are **pure, domain-free algorithms**. A SEK "model" of a pure function is
   trivial/tautological — it would just restate the function and explore a degenerate state space.
   Producing an `MDL`/`CRD` for them is **ceremony, not verification**, and it actively invites the
   PM001 failure in a new disguise: a *fake model* checked in only to turn a box green.
2. **It offers no honest path for a self-hosting tool.** SEK is a compiler + solver + engine. The
   question "can SEK validate SEK?" has a real answer — **yes, once the generic pieces are factored
   out** — but the blanket rule doesn't express it, so the gate is either permanently unsatisfiable
   or satisfied dishonestly.

The trigger was a direct question from the maintainer: *are the coverage tests SEK-generated from a
model of SEK, or hand-written?* The honest answer was **hand-written** — which exposed that the gate
never distinguished the two, and treated 97.5%-unit-covered pure code as `FAIL` for "no MDL/CRD"
while providing no sensible way to earn the pass.

## The maintainer's resolution (the correct model)

> Factor **all** generic/pure code out into `components/` (ARC002) and unit/property-test each to
> ≥95%. What remains in the **vertical** is the **domain-specific behavior** — and *that* is stateful
> and reactive, so **SEK can model and explore it**. Therefore **SEK validates SEK once the
> components are factored out and independently tested.**

This reconciles ARC002 (the component boundary) with the Readiness Gate: the *method* of verification
follows the *class* of the module.

## Timeline of mitigation actions

| Time | Action | MIT | Evidence / result |
|---|---|---|---|
| T0 | v1.3.0 gate marks SEK `FAIL` for every module lacking MDL/CRD, incl. 100%-covered pure components. | — | The design gap. |
| T1 | Maintainer asks whether coverage tests are SEK-generated; answer is hand-written. | — | Gap made explicit. |
| T2 | **Stop treating hand-written unit coverage as if it were model-exploration**, and stop planning MDL/CRD for pure components. | MIT001 | Prevents fake models being authored to satisfy the gate. |
| T3 | File this incident; hand to PM002 to refine the gate's criteria by module class. | MIT002 | IN002 (this doc). |

## Mitigations applied

- **MIT001** — Paused authoring any `MDL`/`CRD` for pure/domain-free modules; kept them on
  unit/property coverage only, pending the gate refinement. (No fake models were created.)
- **MIT002** — Filed this incident so the gate is corrected structurally rather than worked around.

## Verification (stability, not root-cause fix)

- [x] No fake models were checked in to satisfy the gate. Evidence: SEK has only the Turnstile
  `MDL`/`CRD`; components remain unit-tested.
- [x] SEK's status is still honestly `NOT READY` (COV002); nothing was declared ready.

## Hand-off to Post-Mortem

- **Snapshot bundle:** EngLoopKit `6d61388` (v1.3.0 gate); SEK `COV002` baseline.
- **Affected operations:** the gate's per-module criteria in `coverage` + `standards.md`.
- **Cause-class hypothesis (preliminary):** validation-gap — the gate specifies *one* verification
  method for all modules instead of the method appropriate to each module's **class** (component vs
  vertical), and omits the precondition that the vertical must be reduced to domain-only behavior.
- **Suggested PM title:** "Readiness Gate must set the verification method by module class
  (component → unit/property; vertical → SEK self-model), with 'vertical is domain-only' as a
  precondition."
