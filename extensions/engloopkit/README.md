# EngLoopKit ordered workflow extension

This directory is the first-party **ordered command extension** for the EngLoopKit
product. Its installed extension ID is **`engloop`**, producing the ordered
`/speckit.engloop.*` picker surface; the product, bundle, repository, .NET tool, and
release identity remain **`engloopkit`**.

Requires the **.NET 10 SDK/runtime**. ELK uses `EngLoopKit.slnx` as its
authoritative solution graph; SEK v0.1.3 is a native .NET 10 external dependency.

> **Versioning clarification:** “Ordered EngLoop v2” is the *workflow generation /
> specification name*, not a SemVer major release. The product remains on the **1.x**
> maturity runway: this package is **v1.15.3**; “v2” remains workflow terminology only,
> not v2.0.

## Ordered command lanes

| Lane | Commands | Purpose |
|---|---|---|
| Delivery/readiness | `01-northstar` → `08-unittest` | Direction, runway, architecture, accepted-plan implementation, behavior model, exploration, functional validation, final readiness. |
| Review advisory | `09-debugger-walk-thru` | Prepare and track a recommended engineer-led walkthrough without blocking Stage 10. |
| Review | `10-codereview-prepare` | Minimize and validate the current PR after current Stage 08 readiness. |
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

## Install

Install the released extension archive through Spec Kit after installing the matching
root-local `engloopkit` .NET tool manifest:

```powershell
specify extension add engloop --from <release-dir>/engloopkit-extension-1.15.3.zip
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
