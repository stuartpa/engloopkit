# PM002: The Readiness Gate must set the verification method by module class

- **Date:** 2026-07-06
- **Duration:** short (design gap, caught in use)
- **Covers incidents:** IN002
- **Status:** COMPLETE

## Timeline

| Time | Event |
|---|---|
| T0 | v1.3.0 gate (PM001) requires MDL+CRD for every module, including pure components. |
| T1 | Applying it to SEK: pure components can't be meaningfully modelled; blanket rule invites fake models. |
| T2 | Maintainer's resolution: factor generics into `components/` (unit-tested); SEK models the residual domain vertical. |
| T3 | IN002 filed; this post-mortem refines the gate. |

## Root causes

### Primary cause: one verification method imposed on all module classes

- **What failed:** the gate treats "verified" as synonymous with "has an MDL+CRD." But *how* a module
  should be verified depends on *what kind* of module it is. A **pure, domain-free component** is
  verified by unit/property tests; a **stateful domain vertical** is verified by model-exploration.
  Imposing MDL+CRD on a pure function is ceremony that verifies nothing and tempts a fake model.
- **Why it failed:** PM001 (rightly) made "ready" a gated, evidence-backed verdict, but expressed the
  evidence as a single uniform checklist rather than one keyed to the ARC002 module classification
  that already exists.
- **Why we didn't catch it in PM001:** PM001's driving example was a whole product declared ready with
  *nothing* verified; the fix correctly demanded rigorous evidence everywhere. The subtlety that the
  *form* of evidence differs by module class only surfaced when the gate met a self-hosting tool whose
  generic parts are pure and whose domain parts are stateful.

### Contributing insight (the fix's foundation)

A self-hosting tool *can* validate itself — **once its generic code is factored into components**.
After ARC002 extraction, the residual vertical is domain behavior, which is exactly the stateful,
reactive surface model-based testing targets. So "SEK validates SEK" is achievable and honest,
provided the vertical is reduced to domain-only behavior first.

## Five whys

```
Symptom: The gate marks 100%-unit-covered pure components FAIL for "no MDL/CRD", and offers no honest pass.
Why #1: Q: Why FAIL despite full coverage?        A: The gate requires MDL+CRD for every module regardless of kind.
Why #2: Q: Why require MDL+CRD for pure code?     A: The gate specifies one verification method for all modules.
Why #3: Q: Why one method for all?                A: PM001 expressed evidence as a single uniform checklist.
Why #4: Q: Why uniform, ignoring ARC002 classes?  A: PM001's case never exercised the component/vertical distinction.
Why #5: Q: What's the systemic fix? (level)       A: The gate must key the verification METHOD to the module CLASS (component → unit/property; vertical → SEK self-model), and require the vertical to be domain-only (generics factored out) so the model-explored surface is real.
```

## ONE-AND-DONE analysis

- **Concrete bug:** pure components are FAILed for lacking a model that would be meaningless for them.
- **Bug class:** *a gate that fixes the verification method independently of the artifact's nature* —
  it will always either over-demand (blocking) or invite hollow compliance (fake models).
- **Structural fix (mechanical, class-preventing, verifiable):** the gate assigns the verification
  method **by module class**, using the ARC002 litmus test as the objective classifier, and adds a
  precondition that closes the escape hatch:
  - **Component** (`components/*`, passes the ARC002 domain-free litmus test): verified by
    **unit/property tests to ≥95% line & branch**. No MDL/CRD (a model of domain-free code is
    tautological).
  - **Vertical** (`src/*`, domain behavior): verified by **SEK self-modelling** — an `MDL` + `CRD` +
    `sek generate`d conformance tests — **plus ≥95% line & branch**.
  - **Precondition (anti-escape-hatch):** any **generic/domain-free** code still living in the
    vertical is an **ARC002 violation and a gate FAIL** — it must be extracted to a component first.
    This stops "it's just pure functions, we'll unit-test it in place" from dodging the model, and
    stops fake models being written for code that shouldn't be in the vertical at all.
  - All modules still require ≥95% line & branch, architecture conformance, and green suites.

## Learnings

- **LRN001** — Verification is not one-size-fits-all: the *method* (unit/property vs
  model-exploration) must follow the *class* of the artifact, or a gate will either block honest work
  or reward hollow compliance.
- **LRN002** — A self-hosting/model-based tool can validate itself **only after** its generic code is
  factored out; the residual domain vertical is the legitimate model-exploration surface. Component
  extraction (ARC002) is therefore a **precondition** of self-validation, not merely tidiness.
- **LRN003** — When a gate meets a genuinely different context (here, a self-hosting compiler), expect
  to refine *how* it measures — without lowering *what* it requires (still ≥95%, still conformant).

## Repair Items

| RPI | Description (ONE-AND-DONE) | Size | Spec/tinyspec | Status |
|---|---|---|---|---|
| RPI001 | Refine `docs/standards.md` Readiness Gate: verification method **by module class** (component → unit/property ≥95%; vertical → SEK self-model MDL+CRD + generated conformance + ≥95%), plus the "vertical must be domain-only; generic code in the vertical is an ARC002 violation & FAIL" precondition. Keep ≥95%/conformant/green for all. | full | (this PM) | DONE |
| RPI002 | Rework `commands/speckit.engloopkit.coverage.md`: the Readiness Inventory gains a **Class** column (component/vertical); PASS rules branch on class; a component needs no MDL/CRD but a vertical module does; add the domain-only precondition check. Update the `COV` template accordingly. | full | (this PM) | DONE |
| RPI003 | Note in `model`/`explore` commands + the `using-sek-to-generate-tests` skill: **don't model pure components** — extract them (ARC002) and unit/property-test them; model only the domain vertical. Cross-reference PM002. | tiny | (this PM) | DONE |

## Cause-class tags

validation-gap, process-gap

## References

- Incidents: engloop/incidents/IN002_gate-conflates-verification-method.md
- Architecture: ARC002 (component boundary, the classifier); builds on PM001 (the gate itself)
- Recurrence of: refines PM001 (not a regression — a sharpening of the same gate)

## Approvals

- [x] ONE-AND-DONE fixes reviewed for structural soundness
- [x] Learnings accepted
- [x] Repair Items implemented in this repair cycle (EngLoopKit v1.4.0)
- [ ] Closed when SEK is upgraded and the refined gate is exercised
