# The ordered EngLoop workflow

**Workflow generation:** Ordered EngLoop v2.
**Product versioning:** EngLoopKit remains on the **1.x** line; the ordered workflow
ships in v1.7.0 and reusable debugger/readiness/publication/token-efficiency plus direction/pyramid-bound operations support in v1.14.0. “v2” is not a v2.0 product release.

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
| 09 | `speckit.engloop.09-debugger-walk-thru` | Recommend and track an engineer-led line-by-line walkthrough without gating Stage 10. |
| 10 | `speckit.engloop.10-codereview-prepare` | Minimize and validate the current PR after the current HEAD has Stage 08 readiness PASS. |

The Stage 08 PASS requires current evidence for every configured module: architecture,
regressions, artifact-appropriate verification, and measured **95% line + branch**
coverage. The stateful vertical additionally needs behavior-level SEK evidence with
model-derived negative conformance and materially branching paths.

## Token efficiency: 30–31

| Stage | Command | Gate and durable output |
|---:|---|---|
| 30 | `speckit.engloop.30-token-efficiency-analyze` | Read-only VS Code Copilot session-speed/context analysis plus one compact `.engloop/evidence/token-efficiency-analysis-*.json`; no repairs or closure claim. |
| 31 | `speckit.engloop.31-token-efficiency-implement` | Only explicitly approved Agent 30 repair IDs; minimal customization/script changes, declared-tool preflight, focused validation, and one implementation JSON. |

Use this lane whenever a chat shows repeated polling, oversized output, tool guessing,
missing checkpoints, repeated context discovery, or slow serial work. Agent 30 hands off
with `send: false`; Agent 31 requires the user to approve stable repair IDs again.

## Continuation, local utilities, and publications: 50, 60–61, 70–72, 80

| Stage | Command | Gate and durable output |
|---:|---|---|
| 50 | `speckit.engloop.50-handoff-create` | One `HANDOFF<NNN>-<description>.md` continuation packet for another chat or engineering team. |
| 60 | `speckit.engloop.60-overlay-pack` | Pack the verified registered private overlay into one portable archive. |
| 61 | `speckit.engloop.61-overlay-remove` | Confirm and remove every manifest-owned local path, restore prior hooks, and preserve unrelated host files. |
| 70 | `speckit.engloop.70-six-pager-create` | Create a self-contained six-page narrative decision memo and validated Word document. |
| 71 | `speckit.engloop.71-powerpnt-create` | Create a Markdown-first presentation with layered architecture, focused model graphs, collision-free labels, and rendered-PPTX validation. |
| 72 | `speckit.engloop.72-academic-paper-create` | Create a rigorous systems research paper with citations, figures, reproducible evaluation, and validated PDF. |
| 80 | `speckit.engloop.80-upgrade-elk` | Upgrade root-local ELK plus its pinned SEK dependency to the latest verified release, or report already current. |

## Operations: 20–22

Operations is not created merely because a delivery lane completed.

1. **20 Incident** requires an actual operating disruption and a current Stage 08 PASS.
   It reads current North Star boundaries, consults only immediately relevant learning
   cues when safe, and captures mitigations/stabilization only; it does not close a repair.
2. **21 Post-mortem** requires a selected non-empty stabilized incident set. It emits
   PM/LEARN/RPI evidence only after current North Star/Learnings hashes, rule
   dispositions, pyramid update/no-change, source-card/history coverage, retrieval impact,
   and RPI Rule-ID/executable-gate contracts pass deterministic validation.
3. **22 Repair** requires a concrete repair item and opens an obligation. It returns
   through Stage 04 and every applicable Stage 05–08 gate with exact PM Rule IDs and gate
   carried into immutable route acceptance. A separate close record requires the versioned
   tool's hashed process receipt for that gate, source, immutable release, exact target
   verification, and current readiness.

## Stewardship: 40–42

- **40 Refactor** requires explicit spare capacity. It records exactly one REFACT
  decision or `none-this-cycle`; a selected direction/architecture change returns to
  01 and/or 03 before 04.
- **41 Dead code** records the single highest-certainty DEADCODE proposal only after
   symbol, dynamic-use, public-contract, history, and isolated-deletion proof. It changes
   no current source before explicit candidate-specific approval. Rejection is recorded
   and starts a newly numbered candidate search.
- **42 Learnings Pyramid** requires capacity and an accepted-learning refresh demand. It
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
