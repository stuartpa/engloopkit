# EngLoopKit ordered workflow extension

This directory is the first-party **ordered command extension** for the EngLoopKit
product. Its installed extension ID is **`engloop`**, producing the ordered
`/speckit.engloop.*` picker surface; the product, bundle, repository, .NET tool, and
release identity remain **`engloopkit`**.

> **Versioning clarification:** “Ordered EngLoop v2” is the *workflow generation /
> specification name*, not a SemVer major release. The product remains on the **1.x**
> maturity runway: this package is **v1.11.3**; “v2” remains workflow terminology only,
> not v2.0.

## Ordered command lanes

| Lane | Commands | Purpose |
|---|---|---|
| Delivery/readiness | `01-northstar` → `08-unittest` | Direction, runway, architecture, refactor, behavior model, exploration, functional validation, final readiness. |
| Review advisory | `09-debugger-walk-thru` | Prepare and track a recommended engineer-led walkthrough without blocking Stage 10. |
| Review | `10-codereview-prepare` | Minimize and validate the current PR after current Stage 08 readiness. |
| Operations | `20-incident` → `22-repair` | Stabilize a real disruption, analyze stabilized incident sets, and route permanent repair back through delivery gates. |
| Stewardship | `30-refactor-scan`, `31-learnings-pyramid` | Select one evidence-backed evolution decision or condense accepted source learnings when capacity exists. |
| Session memory | `40-pomodoro-create` | Capture a concise note about the just-completed work session. |
| Local utility | `50-overlay-pack`, `51-overlay-remove` | Pack or completely remove manifest-owned local overlay state. |
| Presentation | `60-powerpnt-create` | Create an evidence-backed Markdown-first PPTX with architecture/model/test-path graphics. |

Every command is a Trigger · Goal · Actions · Verification · Memory loop with a
versioned entry validator, exact least-privilege tools, and review-first handoffs.

## Install

Install the released extension archive through Spec Kit after installing the matching
root-local `engloopkit` .NET tool manifest:

```powershell
specify extension add engloop --from <release-dir>/engloopkit-extension-1.11.3.zip
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
