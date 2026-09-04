# EngLoopKit ordered workflow extension

This directory is the first-party **ordered command extension** for the EngLoopKit
product. Its installed extension ID is **`engloop`**, producing the ordered
`/speckit.engloop.*` picker surface; the product, bundle, repository, .NET tool, and
release identity remain **`engloopkit`**.

Requires the **.NET 10 SDK/runtime**. ELK uses `EngLoopKit.slnx` as its
authoritative solution graph; SEK v0.1.3 is a native .NET 10 external dependency.

> **Versioning clarification:** “Ordered EngLoop v2” is the *workflow generation /
> specification name*, not a SemVer major release. The product remains on the **1.x**
> maturity runway: this package is **v1.16.0**; “v2” remains workflow terminology only,
> not v2.0.

## Ordered command lanes

| Lane | Commands | Purpose |
|---|---|---|
| Delivery/readiness | `01-northstar` → `08-unittest` | Direction, runway, architecture, accepted-plan implementation, behavior model, exploration, functional validation, final readiness. |
| Review advisory | `09-debugger-walk-thru` | Prepare and track a recommended engineer-led walkthrough without blocking Stage 10. |
| Review | `10-codereview-prepare` | Minimize and validate the current PR after current Stage 08 readiness. |
| Review response | `11-codereview-address` → `12-codereview-reply-resolve` | Address selected CRB/provider feedback, let an external workflow commit/push, refresh a clean Stage 11 packet, inspect provider state without mutation, then approve the exact payload and apply one reply/resolution through the same explicit adapter. |
| Operations | `20-incident` → `22-repair` | Stabilize under current direction, analyze incidents against pyramid rules/provenance/retrieval, and route exact Rule-ID/executable-gate repairs through delivery/readiness. |
| Positive history | `23-happy-minute` | Record gratitude for a wonderful outcome plus readily available live/runtime/repository context; never turn the moment into a gate. |
| Token efficiency | `30-token-efficiency-analyze` → `31-token-efficiency-implement` | Diagnose VS Code Copilot chat speed/context waste, then implement only approved repairs. |
| Stewardship | `40-refactor-plan`, `41-deadcode`, `42-learnings-pyramid` | Work with the user to plan one North-Star-/architecture-aligned component refactor (`point` is the safe default), propose proven dead code, or condense accepted learnings. |
| Continuation | `50-handoff-create` | Capture a numbered evidence-backed handoff for another chat window or engineering team. |
| Local utility | `60-overlay-pack`, `61-overlay-remove` | Pack or completely remove manifest-owned local overlay state. |
| Publication | `70-six-pager-create` | Create a six-page narrative Markdown memo and validated Word document. |
| Presentation | `71-powerpnt-create` | Create an evidence-backed Markdown-first PPTX with collision-checked architecture/model/test-path graphics. |
| Publication | `72-academic-paper-create` | Create an evidence-backed systems research paper in Markdown/BibTeX and validated PDF. |
| Local utility | `80-upgrade-elk` | Upgrade ELK and its pinned SEK dependency to the latest verified release, or report already current. |

Every command is a Trigger · Goal · Actions · Verification · Memory loop with a
versioned entry validator, exact least-privilege tools, and review-first handoffs.

Stage 21 is the sole model-invocable ELK agent. Plain-language postmortem requests can be
delegated from the default agent or explicitly from Stage 20. Missing bindings enter a
read/search/question-only collector that proposes the next registry-backed PM path and
asks one confirmation; a trusted binder—not operator-entered flags—must validate the
incident, registry, path, HEAD, and tool identity before edits or completion are possible.

CodeReview Bundle remains the reviewer-side product. ELK consumes an explicitly selected
current provider thread as external evidence: Stage 11 owns author-side source remediation
and private response packets without provider/commit/push or ELK/provider-control authority.
After an external commit/push, Stage 11 must create a clean refresh packet at the new head.
Stage 12 owns only an exact separately approved reply/resolution from that packet and its
trusted Stage 11 completion receipt through a
tracked provider adapter and has no source-edit authority. Its mandatory `inspect` phase
must prove current provider identity/state without mutation before the exact payload,
principal, evidence, packet hash, trusted Stage 11 receipt hash, and inspection hash are
eligible for approval.

## Install

Install the released extension archive through Spec Kit after installing the matching
root-local `engloopkit` .NET tool manifest:

```powershell
specify extension add engloop --from <release-dir>/engloopkit-extension-1.16.0.zip
```

The selected root must have exactly one tracked `.engloop/` root, root `NORTHSTAR.md`,
root `LEARNINGS.md`, and a valid `.engloop/config.json`. The entry hook and every command
body validate that state before accepting durable work.

## Readiness

Stage 08 alone produces READY / NOT READY. A PASS requires every configured module to
meet its artifact-appropriate verification method, current architecture/regression
evidence, and measured **95% line + branch coverage**. The domain vertical additionally
needs independent model-derived legal and rejection conformance.

See the root [README](../../README.md) and
[`SPEC001 Ordered EngLoop v2`](../../specs/SPEC001-ordered-engloop-v2/spec.md) for the
complete contract.

## License

MIT.
