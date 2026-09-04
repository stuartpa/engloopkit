# EngLoopKit v1.8 document standards

**Workflow generation:** Ordered EngLoop v2.
**Product SemVer:** EngLoopKit remains on the **1.x** line; this workflow ships as
v1.16.0. “v2” describes the workflow generation, not a v2.0 release.

Durable workflow memory lives in exactly one tracked hidden root:

```text
.engloop/
```

A governed root also has exactly one root `NORTHSTAR.md` and one root `LEARNINGS.md`.
Current `engloop/` and `.engloopkit/` roots are forbidden; the validator fails closed
rather than choosing or merging an alternate root.

## Private overlay mode

An explicit `engloopkit overlay install --mode overlay` creates this same process root
as **local-only** state. It writes local `.git/info/exclude` rules and ELK-owned local
Git hooks before creating managed files; it never edits tracked `.gitignore` or product
files. Overlay state is recorded in `.engloop-overlay/manifest.json` and can be packed
to a plain, hash-verified ZIP outside the repository. No secrets belong in an overlay
archive.

## Naming and counters

Numbered artifacts are monotonic, never reused, and incremented before creation. Most
use `<PREFIX><NNN>_<short-title>.md`; handoffs and Happy Minutes use
`HANDOFF<NNN>-<brief-kebab-description>.md` and
`HAPPY<NNN>-<brief-kebab-description>.md`. Increment the tracked
`.engloop/numbering-registry.md` counter **before** creating the artifact.

## Global prefixes

| Prefix | Produced by | Durable location | Meaning |
|---|---|---|---|
| `SPEC` | 04 Refactor | `specs/SPECxxx-*/` | Governed feature/refactor specification, plan, tasks, and implementation evidence. |
| `SCAF` | 02 Scaffold | `.engloop/scaffolds/` | Test-runway proof: same command pass → controlled failure → restored pass. |
| `ARCH` | 03 Architect | `.engloop/architecture/` | Long-lived architecture and component/vertical boundary decision. |
| `MODEL` | 05 Model | `.engloop/models/` | Independent stateful vertical behavior model. |
| `CORD` | 06 Explore | `.engloop/cord/` | Bounded exploration/generation evidence. |
| `COV` | 07 / 08 | `.engloop/coverage/` | Stage 07 functional evidence or Stage 08 whole-product readiness inventory. |
| `IN` | 20 Incident | `.engloop/incidents/` | Actual operating disruption, timeline, and local mitigations. |
| `PM` | 21 Postmortem | `.engloop/postmortems/` | Analysis of deliberately selected stabilized incident sets. |
| `HAPPY` | 23 Happy Minute | `.engloop/happy-minutes/` | A gratitude-first record of a wonderful outcome and readily available live/repository context. |
| `REFACT` | 40 Refactor Plan | `.engloop/refactors/` | One user-confirmed, North-Star-/architecture-aligned plan or `none-this-cycle`; Stage 04 implements its slices. |
| `DEADCODE` | 41 Dead code | `.engloop/deadcode/` | One high-certainty deletion proposal, its isolated proof, and explicit user decision. |
| `DBG` | 09 Debugger walk-through | `.engloop/debugger-walkthroughs/` | Per-chunk breakpoint/trigger evidence plus explicit engineer attestation at an exact HEAD. |
| `SIX` | 70 Six-pager create | `.engloop/six-pagers/` | Six-page narrative Markdown, generated DOCX, appendices, feedback, and rendered-page validation. |
| `PPT` | 71 PowerPoint create | `.engloop/presentations/` | Markdown-first visual deck, generated PPTX, evidence-derived graph assets, and rendered-slide validation. |
| `PAP` | 72 Academic paper create | `.engloop/academic-papers/` | Research-paper Markdown/BibTeX, generated PDF, figures/data, claim evidence, and rendered-page validation. |
| `HANDOFF` | 50 Handoff create | `.engloop/handoffs/` | Evidence-backed continuation packet for another chat window or engineering team. |

Compound utility command names use subject/action order (for example
`handoff-create`, `six-pager-create`, `powerpnt-create`, `academic-paper-create`, `overlay-pack`, `overlay-remove`, and `upgrade-elk`). Stable
single-word lifecycle command identities are not renamed merely to force a compound form.

## Local prefixes

| Prefix | Parent | Meaning |
|---|---|---|
| `MIT` | `IN` | Temporary stabilization action; never a permanent repair. |
| `LEARN` | `PM` | Accepted source learning retained with postmortem provenance. |
| `RPI` | `PM` | Concrete permanent repair item; must traverse 04 and applicable 05–08 gates. |

## Root direction and learning memory

- `NORTHSTAR.md` is one living, root-visible direction document. Git keeps its history;
  there is no numbered direction prefix.
- `LEARNINGS.md` is the one-page root recall map. It links to source-traceable cards under
  `.engloop/learnings/cards/`; Stage 42 clears a refresh obligation only after static and
  retrieval evidence pass.

## The readiness gate

> **READY is the output of Stage 08, never an agent claim.**

Stage 08 emits `PASS` only when every configured module has current architecture and
regression evidence, artifact-appropriate verification, and measured **95.00% line and
branch coverage**. A stateful domain vertical additionally requires an independent,
materially branching SEK model with model-derived legal and negative conformance. Any
missing, stale, or failing row produces **NOT READY** and blocks Stage 20 operations.

## Operations rule

> A live patch is a `MIT`, not a repair.

Stage 20 mitigates only. Its learning-context hook must keep recovery chat available on
missing/malformed metadata or unavailable hook state, emit an explicit structured
deferral, and leave failed evidence unaccepted. The standalone incident-context validator
still fails closed on unsupported stabilization claims. Stage 21 analyzes a deliberately
selected stabilized set. Plain-language postmortem requests enter read/search/question-only
collection when internal bindings are absent. Stage 21 presents ambiguous or active
incidents, proposes the next registry-backed PM path, and asks one confirmation; it never
asks the operator to reconstruct flags. No scope or completion is accepted until the
trusted binder validates the stabilized incident, next PM number, create-new path,
confirmation, HEAD, and tool identity.
Stage 21 must consult current `NORTHSTAR.md` and the progressive
`LEARNINGS.md → RULE:<card-slug> → PMxxx/LEARNxxx` path, classify reinforced,
contradicted, and missing rules, and deterministically validate pyramid provenance and
applicable retrieval before completion. `NO-CHANGE` means no new accepted source learning;
any new `PMxxx/LEARNxxx` requires immediate living provenance coverage. Stage 22 opens a permanent repair obligation,
carries exact Rule IDs plus an executable gate through Stage 04 and applicable Stage
05–08 gates, and closes only when that gate passes with durable evidence plus source,
immutable release artifact, exact target application/verification, and current readiness.
Route and close are separate immutable records. Gate PASS comes only from the versioned
tool's process receipt; readiness and receipts are invalidated by changed worktree bytes,
not merely by a changed Git status shape.

## Code-review response rule

CRB owns reviewer-side findings and publication; ELK owns the author's explicitly selected
response. Stage 11 may edit source and validate but cannot mutate provider state, commit,
or push, and it cannot edit Git/tool/config/adapter/response-authority control state. After
an accepted edit, an external workflow owns commit/push and Stage 11 must produce a new
clean refresh packet and trusted completion receipt at that head. Stage 12 may apply only one separately approved
reply/resolution from that clean packet through a tracked hash-bound provider adapter and
cannot edit source. The adapter must first complete a non-mutating exact provider-state
inspection, and the approval message binds the exact reply/principal/evidence plus packet
and inspection hashes. Resolution requires the validated fix on authoritative provider head.
Every provider attempt is recorded before mutation and revalidated before reuse;
ambiguous outcomes reconcile with the same marker and never retry blindly. Missing adapter,
principal, revision, thread, or read-back capability fails closed without a fallback.
