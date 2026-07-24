# EngLoopKit v1.8 document standards

**Workflow generation:** Ordered EngLoop v2.
**Product SemVer:** EngLoopKit remains on the **1.x** line; this workflow ships as
v1.11.3. “v2” describes the workflow generation, not a v2.0 release.

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
use `<PREFIX><NNN>_<short-title>.md`; Pomodoro notes use
`POM<NNNN>-<brief-kebab-description>.md`. Increment the tracked
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
| `REFACT` | 30 Refactor scan | `.engloop/refactors/` | One selected evolution decision or `none-this-cycle`. |
| `DBG` | 09 Debugger walk-through | `.engloop/debugger-walkthroughs/` | Per-chunk breakpoint/trigger evidence plus explicit engineer attestation at an exact HEAD. |
| `PPT` | 60 PowerPoint create | `.engloop/presentations/` | Markdown-first visual deck, generated PPTX, and evidence-derived graph assets. |
| `POM` | 40 Pomodoro create | `.engloop/pomodoros/` | Brief evidence-backed note about the just-completed 30–60 minute session. |

Compound utility command names use subject/action order (for example
`pomodoro-create`, `powerpnt-create`, `overlay-pack`, and `overlay-remove`). Stable
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
  `.engloop/learnings/cards/`; Stage 31 clears a refresh obligation only after static and
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

Stage 20 mitigates only. Stage 21 analyzes a deliberately selected stabilized set. Stage
22 opens a permanent repair obligation and returns through Stage 04 plus all applicable
Stage 05–08 gates. Repair closure requires source, immutable release artifact, exact
target application, target verification, and a current Stage 08 PASS.
