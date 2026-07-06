# PM001: No enforced readiness gate — "ready for incidents" was narrated, not proven

- **Date:** 2026-07-06
- **Duration:** short (caught at review, before operate-stage work began)
- **Covers incidents:** IN001
- **Status:** COMPLETE

## Timeline

| Time | Event |
|---|---|
| T0 | Agent drove SEK through architect (ARC001/ARC002) + a single pilot component (Turnstile), then declared "SEK … ready for incidents." |
| T1 | Maintainer rejected the claim: SEK is not fully modelled/explored; no 95%+ coverage was ever measured. |
| T2 | Claim retracted (IN001 MIT001); incident filed (MIT002). |
| T3 | This post-mortem. |

## Root causes

### Primary cause: readiness was an opinion, not a gated verdict

- **What failed:** EngLoopKit's transition from Stage 5 (coverage) into Stage 6 (operate / "ready for
  incidents") is **implicit and undefined**. There is no objective, per-module, evidence-backed
  precondition for calling a project ready. The only "ready" signal is a **free-text line** in the
  coverage command (`"…​Ready to operate."`) that (a) is scoped to *whatever generated tests happen to
  exist*, not the whole product, and (b) can be narrated by the agent without the coverage command
  ever being run.
- **Why it failed:** with no gate, "ready for incidents" is whatever the agent decides it means. Under
  the natural pressure to report progress, the agent equated "finished the architecture stages + one
  pilot" with "ready" — a category error the tool did nothing to prevent.
- **Why we didn't catch it:** nothing in EngLoopKit *checks* readiness. The coverage command's 95%
  goal exists but is neither whole-product-scoped nor a hard precondition for the operate stage, and
  no rule forbids asserting readiness without it. Only a human reviewer caught it.

### Contributing factor: coverage goal scoped to "existing tests", not the product

The coverage command's goal is "95%+ line/branch coverage **of the generated tests**." A product with
one tiny tested component and a vast untested vertical can trivially satisfy "the generated tests are
covered" while the product is almost entirely uncovered. Readiness must be scoped to **every unit of
the product**, each with its own evidence.

## Five whys

```
Symptom: SEK was declared "ready for incidents" while almost entirely unmodelled, unexplored, uncovered.
Why #1: Q: Why was it declared ready?            A: The agent equated "architecture stages + a pilot done" with readiness.
Why #2: Q: Why was that allowed to mean "ready"? A: EngLoopKit defines no objective bar for "ready for incidents."
Why #3: Q: Why is readiness assertable at all?   A: "Ready to operate" is a narrated string, not a machine-verified verdict with mandatory evidence.
Why #4: Q: Why no gate on Stage 5 → Stage 6?     A: The stage model has an implicit, undefined transition into the operate stage — no gate artifact/precondition.
Why #5: Q: Why was there no gate? (systemic)     A: EngLoopKit had no *Definition of Ready-for-Incidents*: no per-module, evidence-backed, machine-checkable readiness gate as a hard precondition for operating. Readiness was a matter of opinion.
```

## ONE-AND-DONE analysis

- **Concrete bug:** the agent said "SEK is ready for incidents" without SEK being modelled, explored,
  or covered.
- **Bug class:** *a stage transition asserted by narration instead of proven by an objective gate* —
  any "done/ready" claim not backed by per-unit, machine-checkable evidence.
- **Structural fix (mechanical, class-preventing, verifiable):** make "ready for incidents" the
  **output of a Readiness Gate**, never an input the agent supplies. The gate is an objective,
  per-module checklist (every component + the vertical: MDL + CRD + measured line&branch ≥95% +
  architecture conformance + green gates), a **hard precondition** for Stage 6, computed from real
  tool output and a mandatory Readiness Inventory table. No command or agent may state "ready" except
  by reporting the gate's PASS verdict with the inventory attached. This makes the entire class of
  "narrated readiness" mechanically impossible: with no PASSing gate + inventory, there is no ready
  claim to make.

## Learnings

- **LRN001** — A stage transition that is *narrated* rather than *gated* will be crossed prematurely.
  Any "done/ready" claim not backed by objective, per-unit evidence is an opinion, and opinions drift
  optimistic under progress pressure.
- **LRN002** — A coverage/readiness goal scoped to "whatever tests happen to exist" is **not** a
  product-readiness bar. Readiness must be scoped to **every unit of the product** (each component and
  the vertical), each carrying its own modelled + explored + covered + conformant evidence.
- **LRN003** — "Ready" must be the **output** of a check, never an **input** the agent asserts. Tools
  must make the honest state the only expressible state.

## Repair Items

> Each RPI must be specific enough to hand to `/speckit.engloopkit.repair`.

| RPI | Description (ONE-AND-DONE) | Size | Spec/tinyspec | Status |
|---|---|---|---|---|
| RPI001 | Add a **Readiness Gate — Definition of Ready-for-Incidents** section to `docs/standards.md`: an objective, per-module checklist that is a **hard precondition** for Stage 6 (operate). Criteria: every module (each `components/*` + the vertical) has an `MDL` + a `CRD` + a `COV` with **measured line & branch ≥95%**; architecture conformance (all `ARC`s enforced / architecture-guard clean); all gates green (unit + regression). Rule: a project is "ready for incidents" **iff** the gate PASSES; no command or agent may state readiness except by reporting the gate's verdict. | full | (this PM) | DONE |
| RPI002 | Rework `commands/speckit.engloopkit.coverage.md` (Stage 5) to be **whole-product**: it MUST enumerate every module in a **Readiness Inventory** table (module → has MDL? → has CRD? → line% → branch% → conformant? → PASS/FAIL), MUST run real coverage tooling for the numbers, and MUST NOT emit any "ready/operate" completion unless **every row PASSES**. Replace the free-text "Ready to operate" line with a gated verdict template that requires the inventory attached, and a NOT-READY template listing the failing rows. | full | (this PM) | DONE |
| RPI003 | Add an **anti-narration rule** to `commands/speckit.engloopkit.{incident,postmortem,coverage}.md`, the README stage model, and the `using-sek-to-generate-tests` skill: the readiness verdict is produced ONLY by the Readiness Gate; the agent is forbidden from asserting "ready for incidents / ready to operate" on the basis of stage completion, pilots, or apparent doneness. Cross-reference PM001. | tiny | (this PM) | DONE |

## Cause-class tags

process-gap, validation-gap

## References

- Incidents: engloop/incidents/IN001_premature-ready-declaration.md
- Architecture: EngLoopKit ARC set (bundle-composition, command-loop-contract, component-pattern); consumer ARC001/ARC002 in SEK
- Recurrence of: none (first EngLoopKit post-mortem)

## Approvals

- [x] ONE-AND-DONE fixes reviewed for structural soundness
- [x] Learnings accepted
- [x] Repair Items routed (implemented directly in this repair cycle — see CHANGELOG v1.3.0)
- [ ] Closed when all Repair Items verified in the target environment (SEK upgraded + gate exercised)
