# The ordered EngLoop workflow

**Workflow generation:** Ordered EngLoop v2.
**Product versioning:** EngLoopKit remains on the **1.x** line; the ordered workflow
ships in v1.7.0 and private overlay coexistence support in v1.8.1. “v2” is not a v2.0 product release.

EngLoopKit has three independently invoked lifecycle lanes plus one local utility.
Command ordinals give the picker a
predictable order; they do **not** schedule work automatically. Every accepted stage is
an evidence-gated transition, not a narrated claim.

## Delivery and readiness: 01–08

| Stage | Command | Gate and durable output |
|---:|---|---|
| 01 | `speckit.engloop.01-northstar` | One living root `NORTHSTAR.md`; do not create numbered direction snapshots. |
| 02 | `speckit.engloop.02-scaffold` | Thin real-boundary slice plus a proven test runway (`SCAFxxx`): same command pass → controlled named failure → restoration pass. |
| 03 | `speckit.engloop.03-architect` | Architecture and component/vertical boundary evidence (`ARCHxxx`). |
| 04 | `speckit.engloop.04-refactor` | Governed specification/plan/tasks/implementation under accepted architecture. |
| 05 | `speckit.engloop.05-model` | Independent behavior model with legal and rejection semantics (`MODELxxx`). |
| 06 | `speckit.engloop.06-explore` | Bounded CORD exploration and deterministic generated suite (`CORDxxx`). |
| 07 | `speckit.engloop.07-validate` | Fresh generated-suite-only functional validation and reachability (`COVxxx`); no readiness claim. |
| 08 | `speckit.engloop.08-unittest` | Disposition before direct tests, whole-product coverage, and the sole READY / NOT READY inventory verdict. |
| 09 | `speckit.engloop.09-overlay-pack` | Pack a verified private local overlay; install/unpack use the tool because a target may have no agent surface yet. |

The Stage 08 PASS requires current evidence for every configured module: architecture,
regressions, artifact-appropriate verification, and measured **95% line + branch**
coverage. The stateful vertical additionally needs behavior-level SEK evidence with
model-derived negative conformance and materially branching paths.

## Operations: 20–22

Operations is not created merely because a delivery lane completed.

1. **20 Incident** requires an actual operating disruption and a current Stage 08 PASS.
   It captures mitigations and stabilization only; it does not close a permanent repair.
2. **21 Post-mortem** requires a selected non-empty stabilized incident set. It emits
   PM/LEARN/RPI evidence and may create a pending learning refresh.
3. **22 Repair** requires a concrete repair item and opens an obligation. It returns
   through Stage 04 and every applicable Stage 05–08 gate. It closes only after source,
   immutable release, exact target verification, and current readiness agree.

## Stewardship: 30–31

- **30 Refactor scan** requires explicit spare capacity. It records exactly one REFACT
  decision or `none-this-cycle`; a selected direction/architecture change returns to
  01 and/or 03 before 04.
- **31 Learnings Pyramid** requires capacity and an accepted-learning refresh demand. It
  validates source/card/index/retrieval evidence, then returns to its invoking context.

## Handoffs

Handoffs are review-first UI suggestions with `send: false`. Opening or clicking a
handoff does not mutate state, satisfy evidence, or schedule another lane. Submission
at the target command re-runs the root-local versioned entry validator.

## Private overlay utility: install / pack / unpack

Overlay is selected at installation time, not inferred from a repository:

```text
engloopkit overlay install --mode overlay --root <git-root> ...
```

It owns a closed local path set and proves every managed file is untracked and ignored
before normal work begins. `overlay pack` produces one plain hash-verified ZIP outside
the repository. `overlay unpack` accepts only a matching repository identity/base
revision and rejects collisions, tracked paths, archive path escapes, hash failures, and
secret-like files. It is deterministic tool validation; ELK performs no UI validation.

## Why this is Loop Engineering

Every stage has a concrete **Trigger**, **Goal**, **Actions**, **Verification**, and
**Memory**. The feedback loop is not an agent improvisation: failed functional validation
routes to implementation/model/exploration according to evidence; failed readiness lists
module blockers; operations separates mitigation from permanent repair; stewardship work
requires explicit capacity.
