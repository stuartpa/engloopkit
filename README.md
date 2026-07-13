# EngLoopKit

> Ordered, evidence-gated engineering loops for long-lived software.

EngLoopKit is a [Spec Kit](https://github.com/github/spec-kit) product and bundle for
building, validating, operating, and evolving software without narrating progress as
proof. It is **Loop Engineering** applied to the full lifecycle: every stage has an
explicit trigger, goal, actions, verification, and durable memory.

> **SemVer policy:** The phrase **“Ordered EngLoop v2”** refers to the second
> *workflow/specification generation*, not a product major version. EngLoopKit remains
> on the **1.x** maturity runway for the foreseeable future: this ordered release is
> **v1.7.0** and the planned overlay feature is **v1.8.0**. No v2.0 release is implied.

The v1.7 workflow separates delivery/readiness, operations, and stewardship into
ordered command lanes. A command number is a picker identity—not an automatic scheduler.
A handoff is review-first (`send: false`), not a state transition.

## Core principles

- **One process root:** tracked `.engloop/`, root `NORTHSTAR.md`, and root `LEARNINGS.md`.
  Legacy `engloop/` and `.engloopkit/` roots are rejected rather than merged.
- **Evidence, not narration:** only Stage 08 emits a readiness verdict. A product enters
  operations only after a current whole-product PASS inventory.
- **Verification follows artifact class:** generic components earn unit/property evidence;
  the stateful domain vertical earns independent SEK model/exploration/functional evidence.
- **Model-derived rejection:** a useful model proves both legal behavior and what the SUT
  refuses. Hand-written error assertions are not a substitute for model-derived negatives.
- **Components are generic:** non-domain runtime/BCL helpers live under language-appropriate
  component boundaries; the vertical composes them.

## The 13 commands

The released extension ID is **`engloop`**; product, bundle, and tool identity remain
**`engloopkit`**. Lexical picker order is the normal workflow order.

| Lane | Command | Responsibility |
|---|---|---|
| Delivery | `/speckit.engloop.01-northstar` | Create/evolve the one root living direction. |
| Delivery | `/speckit.engloop.02-scaffold` | Create a thin working slice and prove the test runway. |
| Delivery | `/speckit.engloop.03-architect` | Derive/govern long-lived architecture and component boundary. |
| Delivery | `/speckit.engloop.04-refactor` | Governed implementation/refactor to final form. |
| Delivery | `/speckit.engloop.05-model` | Define independent stateful vertical behavior and rejection semantics. |
| Delivery | `/speckit.engloop.06-explore` | Explore bounded behavior and regenerate functional tests. |
| Delivery | `/speckit.engloop.07-validate` | Run generated-only functional validation and reachability. |
| Delivery | `/speckit.engloop.08-unittest` | Classify residue, add direct tests after disposition, compute sole readiness verdict. |
| Operations | `/speckit.engloop.20-incident` | Stabilize a real operating disruption using mitigations only. |
| Operations | `/speckit.engloop.21-postmortem` | Turn selected stabilized incidents into PM/LEARN/RPI evidence. |
| Operations | `/speckit.engloop.22-repair` | Route permanent repair through Stage 04 and applicable 05–08 gates. |
| Stewardship | `/speckit.engloop.30-refactor-scan` | Select one evidence-backed REFACT decision or record no work. |
| Stewardship | `/speckit.engloop.31-learnings-pyramid` | Condense source learnings into validated cards and retrieval evidence. |

## Readiness gate

Stage 08 is the only source of **READY / NOT READY**. Its PASS requires every explicit
module-inventory row to have current architecture and regression evidence plus at least
**95.00% measured line and branch coverage**. The stateful vertical additionally requires
an adequate independent model, material exploration, and model-derived negative
conformance. A green pilot, a test percentage from one suite, or an installed picker
never authorizes operations.

## Install a release

A released v1.7 artifact set contains three immutable pieces:

1. `engloopkit.<version>.nupkg` — the root-local .NET tool (`engloopkit`);
2. `engloop-extension-<version>.zip` — the ordered Spec Kit extension (`engloop`);
3. `engloopkit-<version>.zip` — the composition-only bundle.

Install the tool into the **selected repository root** using its local tool manifest; do
not point agent hooks at a sibling build output:

```powershell
# From the consumer root, after downloading the released nupkg to <release-dir>.
dotnet new tool-manifest --force
dotnet tool install engloopkit --version 1.7.0 --add-source <release-dir>

# Install the exact released ordered extension archive.
specify extension add engloop --from <release-dir>/engloop-extension-1.7.0.zip
```

The extension’s `SessionStart` hook and command body both run:

```powershell
dotnet tool run engloopkit validate agent-entry --stage <exact-command-id> --root .
```

A consumer installs companion extensions explicitly when its accepted architecture uses
them; no tool, provider, framework, or fallback is guessed by EngLoopKit.

## Development and release gates

For self-host development:

```powershell
pwsh scripts/prove-test-runway.ps1
pwsh scripts/generate-loop-tests.ps1
pwsh scripts/validate-functional.ps1
pwsh scripts/validate-readiness.ps1
pwsh scripts/validate-v2.ps1
pwsh scripts/validate-package.ps1
```

`validate-package.ps1` emits immutable artifact hashes only after the product readiness,
command/agent surface, package, and disposable install gates pass. See
[`specs/SPEC001-ordered-engloop-v2/`](specs/SPEC001-ordered-engloop-v2/) for the v2
contract and [`LEARNINGS.md`](LEARNINGS.md) for traceable operational lessons.

## License

MIT — see [LICENSE](LICENSE).
