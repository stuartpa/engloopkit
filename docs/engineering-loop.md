# The ordered EngLoop workflow

**Workflow generation:** Ordered EngLoop v2.
**Product versioning:** EngLoopKit remains on the **1.x** line; the ordered workflow
ships in v1.7.0 and the current author-response, active-chat postmortem, and distinct refactor-plan/refactor workflow in v1.16.0. “v2” is not a v2.0 product release.

EngLoopKit has independently invoked lifecycle, positive-history, and local utility lanes.
Command ordinals give the picker a
predictable order; they do **not** schedule work automatically. Every accepted stage is
an evidence-gated transition, not a narrated claim.

## Delivery, readiness, and review: 01–12

| Stage | Command | Gate and durable output |
|---:|---|---|
| 01 | `speckit.engloop.01-northstar` | One living root `NORTHSTAR.md`; do not create numbered direction snapshots. |
| 02 | `speckit.engloop.02-scaffold` | Thin real-boundary slice plus a proven test runway (`SCAFxxx`): same command pass → controlled named failure → restoration pass. |
| 03 | `speckit.engloop.03-architect` | Architecture and component/vertical boundary evidence (`ARCHxxx`). |
| 04 | `speckit.engloop.04-refactor` | Implement one accepted SPEC/REFACT/repair slice under binding North Star and architecture guidance; do not re-plan. |
| 05 | `speckit.engloop.05-model` | Independent behavior model with legal and rejection semantics (`MODELxxx`). |
| 06 | `speckit.engloop.06-explore` | Bounded CORD exploration and deterministic generated suite (`CORDxxx`). |
| 07 | `speckit.engloop.07-validate` | Fresh generated-suite-only functional validation and reachability (`COVxxx`); no readiness claim. |
| 08 | `speckit.engloop.08-unittest` | Disposition before direct tests, whole-product coverage, and the sole READY / NOT READY inventory verdict. |
| 09 | `speckit.engloop.09-debugger-walk-thru` | Recommend and track an engineer-led line-by-line walkthrough without gating Stage 10. |
| 10 | `speckit.engloop.10-codereview-prepare` | Minimize and validate the current PR after the current HEAD has Stage 08 readiness PASS. |
| 11 | `speckit.engloop.11-codereview-address` | Bind one selected provider thread, address accepted feedback in the author checkout, validate it, and prepare a private response packet without provider mutation, commit, or push. |
| 12 | `speckit.engloop.12-codereview-reply-resolve` | Revalidate a clean post-commit Stage 11 refresh packet through a non-mutating provider inspection, collect informed exact approval, and apply one reply/resolution through the same explicit adapter with reconciliation evidence. |

The Stage 08 PASS requires current evidence for every configured module: architecture,
regressions, artifact-appropriate verification, and measured **95% line + branch**
coverage. The stateful vertical additionally needs behavior-level SEK evidence with
model-derived negative conformance and materially branching paths.

CRB produces reviewer-side findings/comments. ELK's author-side response starts only when
an engineer explicitly selects one current provider thread. Stage 11 owns source edits,
repository-declared validation, and an ignored immutable response packet, but never posts,
resolves, commits, pushes, or edits ELK/provider control state. A separate workflow owns
commit/push, after which Stage 11 creates a new clean refresh packet. Stage 12 owns no
source edits; it requires that clean packet and the validated fix on authoritative provider
head for resolution, requires its trusted Stage 11 completion receipt and one exact
non-mutating provider inspection, displays the
exact reply/operation/principal/evidence in the fixed approval message,
collects one fixed-option approval, and invokes only a tracked provider adapter implementing
`engloop-review-response-v1`. Ambiguous mutation outcomes reconcile with the same attempt
identity and never retry blindly.

## Token efficiency: 30–31

| Stage | Command | Gate and durable output |
|---:|---|---|
| 30 | `speckit.engloop.30-token-efficiency-analyze` | Read-only VS Code Copilot session-speed/context analysis plus one compact `.engloop/evidence/token-efficiency-analysis-*.json`; no repairs or closure claim. |
| 31 | `speckit.engloop.31-token-efficiency-implement` | Only explicitly approved Agent 30 repair IDs; minimal customization/script changes, declared-tool preflight, focused validation, and one implementation JSON. |

Use this lane whenever a chat shows repeated polling, oversized output, tool guessing,
missing checkpoints, repeated context discovery, or slow serial work. Agent 30 hands off
with `send: false`; Agent 31 requires the user to approve stable repair IDs again. Both
agents reactivate their ordered entry/scope hooks on each submitted prompt, including
after switching agents in an existing chat; tool and completion guards remain fail closed.

Stage 21 accepts ordinary-language postmortem intent. The default agent—or Stage 20 after
an explicit operator request—may invoke it as a nested agent. When exact bindings are
absent, Stage 21 reads only incident/registry evidence, presents ambiguity instead of
guessing, proposes the next PM path, and asks one concise in-turn confirmation. The
versioned binder activates edits only after stabilized-incident, registry, create-new,
confirmation, HEAD, and tool-identity checks pass. Direct and delegated runs retain
equivalent start and completion hooks.

## Refactor compute profiles: 40

`/speckit.engloop.40-refactor-plan --scope <path-or-topic>` defaults to `--profile point`.
This intentionally supports frequent small component extractions with inexpensive/fast
models. Explicit `bounded` permits one subsystem; explicit `deep` permits repository-wide
analysis and one phased campaign for deliberate frontier/high-thinking runs. VS Code does
not provide authoritative selected-model, pricing, token-budget, or thinking-level metadata
to the agent/hook contract, so Stage 40 never guesses or silently promotes the profile.
Stage 40 works with the user to read/cite the current North Star and applicable architecture,
classify vertical versus component responsibilities, and record one confirmed REFACT plan.
Stage 04 performs only the accepted implementation slice.

Typical mapping: MAI-Flash-1.1/Luna or low thinking → explicit/default `point`; Tera or
medium thinking → explicit `bounded`; deliberately selected SOL/frontier max thinking →
explicit `deep`. This is operator guidance, not runtime detection, and a profile stays
bound for the session even if the selected model changes.

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

## Operations and positive history: 20–23

Operations is not created merely because a delivery lane completed.

1. **20 Incident** requires an actual operating disruption and a current Stage 08 PASS.
   It reads current North Star boundaries, consults only immediately relevant learning
   cues when safe, and captures mitigations/stabilization only; it does not close a repair.
   Hook context failures emit a structured non-blocking deferral so recovery remains
   available; the standalone incident-context validator still gates stabilization evidence.
2. **21 Post-mortem** requires a selected non-empty stabilized incident set. It emits
   PM/LEARN/RPI evidence only after current North Star/Learnings hashes, rule
   dispositions, pyramid update/no-change, source-card/history coverage, retrieval impact,
   and RPI Rule-ID/executable-gate contracts pass deterministic validation. Missing or
   malformed initial incident/PM context enters read-only conversational collection
   without authorizing tools or completion; one confirmed registry-backed proposal must
   pass the trusted binder before work.
3. **22 Repair** requires a concrete repair item and opens an obligation. It returns
   through Stage 04 and every applicable Stage 05–08 gate with exact PM Rule IDs and gate
   carried into immutable route acceptance. A separate close record requires the versioned
   tool's hashed process receipt for that gate, source, immutable release, exact target
   verification, and current readiness.
4. **23 Happy Minute** needs only the user's description of what worked wonderfully.
   It creates one gratitude-first `HAPPY<NNN>-<description>.md`, captures readily
   available live/runtime and explicitly supplied repository context, labels unknowns
   `NOT-PROVIDED`, and never blocks on provenance, cleanliness, readiness, or causal proof.
   Its optional review-first Stage 42 handoff may preserve reusable positive learnings.

## Stewardship: 40–42

- **40 Refactor Plan** requires explicit spare capacity. It works with the user to record
   exactly one architecture-aligned REFACT plan or `none-this-cycle`, emphasizing
   extraction of generic/non-vertical code into components. A selected direction or
   architecture change returns to 01 and/or 03 before 04 implementation.
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
