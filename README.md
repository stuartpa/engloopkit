# EngLoopKit (ELK)

> Ordered, evidence-gated engineering loops for long-lived software.

EngLoopKit is a [Spec Kit](https://github.com/github/spec-kit) product and bundle for
building, validating, operating, and evolving software without narrating progress as
proof. It is **Loop Engineering** applied to the full lifecycle: every stage has an
explicit trigger, goal, actions, verification, and durable memory.

> **SemVer policy:** The phrase **“Ordered EngLoop v2”** refers to the second
> *workflow/specification generation*, not a product major version. EngLoopKit remains
> on the **1.x** maturity runway for the foreseeable future: this ordered release is
> **v1.7.0** established the ordered baseline; review, handoff memory, and complete
> reusable debugger walkthroughs, generic readiness handoff, presentation generation, and
> no-ID overlay installation, advisory debugger walkthroughs, publication/token-efficiency agents, and direction/pyramid-bound operations learning ship as **v1.14.0**. No v2.0 release is implied.

The v1.8 workflow separates delivery/readiness, operations, stewardship, and local
overlay utility work into
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

## The 25 commands

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
| Review advisory | `/speckit.engloop.09-debugger-walk-thru` | Prepare and track the engineer's recommended line-by-line debugger walkthrough; never block review preparation. |
| Review | `/speckit.engloop.10-codereview-prepare` | Minimize and validate the current PR after the current Stage 08 readiness PASS. |
| Operations | `/speckit.engloop.20-incident` | Stabilize a real disruption using mitigations under current North Star boundaries and immediately relevant learning cues. |
| Operations | `/speckit.engloop.21-postmortem` | Analyze incidents against current direction/pyramid rules; update provenance/retrieval or record explicit no-change; emit rule-bound RPIs. |
| Operations | `/speckit.engloop.22-repair` | Route/close permanent repair with exact Rule IDs, executable-gate evidence, Stage 04/applicable 05–08 gates, and current readiness. |
| Token efficiency | `/speckit.engloop.30-token-efficiency-analyze` | Read-only analysis of VS Code Copilot chat speed/context waste with compact Chronicle evidence and ranked repairs. |
| Token efficiency | `/speckit.engloop.31-token-efficiency-implement` | Implement only explicitly approved efficiency repairs with toolchain preflight and focused validation. |
| Stewardship | `/speckit.engloop.40-refactor` | Select one evidence-backed REFACT decision or record no work. |
| Stewardship | `/speckit.engloop.41-deadcode` | Propose one proven high-certainty DEADCODE candidate; remove it only after explicit user approval. |
| Stewardship | `/speckit.engloop.42-learnings-pyramid` | Condense source learnings into validated cards and retrieval evidence. |
| Continuation | `/speckit.engloop.50-handoff-create` | Create a numbered evidence-backed handoff for another chat window or engineering team. |
| Local utility | `/speckit.engloop.60-overlay-pack` | Pack a verified private local ELK overlay. |
| Local utility | `/speckit.engloop.61-overlay-remove` | Remove manifest-owned overlay state and restore prior hooks. |
| Publication | `/speckit.engloop.70-six-pager-create` | Build a six-page narrative Markdown memo and validated Word document. |
| Presentation | `/speckit.engloop.71-powerpnt-create` | Build a Markdown-first PPTX with collision-checked architecture/model graphs and rendered-slide validation. |
| Publication | `/speckit.engloop.72-academic-paper-create` | Build an evidence-backed systems paper in Markdown/BibTeX and a validated PDF. |
| Local utility | `/speckit.engloop.80-upgrade-elk` | Upgrade root-local ELK and its SEK dependency to the latest verified release, or report already current. |

Agents 30–31 require VS Code custom-agent hooks; the tracked workspace setting enables
them. Agent 30 uses the local Chronicle index when available; its absence is reported as a
measurement limitation. Organization policy that disables hooks makes both token agents
fail closed before analysis/implementation mutation.

Stages 21–22 also require agent-scoped hooks. Their completion validators bind the named
PM/repair acceptance to current `NORTHSTAR.md`, `LEARNINGS.md`, living rule cards,
historical `PMxxx/LEARNxxx` provenance, and the selected RPI's executable gate. Stage 42
still handles broader backlog condensation; it is not a fallback for skipping pyramid
consideration during the dominant incident/postmortem/repair loop.

## Readiness gate

Stage 08 is the only source of **READY / NOT READY**. Its PASS requires every explicit
module-inventory row to have current architecture and regression evidence plus at least
**95.00% measured line and branch coverage**. The stateful vertical additionally requires
an adequate independent model, material exploration, and model-derived negative
conformance. A green pilot, a test percentage from one suite, or an installed picker
never authorizes operations.

## Install a release

ELK v1.14.0 requires the **.NET 10 SDK/runtime**. The repository pins SDK `10.0.303`
and uses `EngLoopKit.slnx` as its only solution graph.

A released v1.14.0 artifact set contains three immutable pieces:

1. `engloopkit.<version>.nupkg` — the root-local .NET tool (`engloopkit`);
2. `engloopkit-extension-<version>.zip` — the ordered Spec Kit extension (`engloop`);
3. `engloopkit-<version>.zip` — the composition-only bundle.

Install the tool into the **selected repository root** using its local tool manifest; do
not point agent hooks at a sibling build output:

```powershell
# From the consumer root, after downloading the released nupkg to <release-dir>.
dotnet new tool-manifest --force
dotnet tool install engloopkit --version 1.14.0 --add-source <release-dir>

# Install the exact released ordered extension archive.
specify extension add engloop --from <release-dir>/engloopkit-extension-1.14.0.zip
```

The extension’s `SessionStart` hook and command body both run:

```powershell
dotnet tool run engloopkit validate agent-entry --stage <exact-command-id> --root .
```

A consumer installs companion extensions explicitly when its accepted architecture uses
them; no tool, provider, framework, or fallback is guessed by EngLoopKit.

### SEK dependency for model-based stages

Stages 05–07 and relevant Stage 21–22 escape/repair work require the exact independent
SEK **v0.1.3** release: native .NET 10 `SpecExplorerKit.Tool`, matching `SpecExplorerKit.Modeling`, and
the Spec Kit extension with ID `sek`. Those stages load SEK's installed
`sek-cord-authoring` and `using-sek-to-generate-tests` skills and fail closed when they
are absent. Cord, binding, and generation documentation belongs to SEK, not ELK.

```powershell
pwsh scripts/ensure-sek-v0.1.3.ps1 -Root . -InstallExtension
```

ELK verifies the published v0.1.3 SHA-256 values before restore/install and fails closed
on a missing or mismatched asset. See the
[SEK installation documentation](https://stuartpa.github.io/sek/install/) for the
authoritative release contract.

## Development and release gates

For self-host development:

```powershell
dotnet build EngLoopKit.slnx -c Debug
dotnet test EngLoopKit.slnx -c Debug --no-build
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

## Private overlay mode

Use overlay mode when ELK must remain local to an existing Git checkout. Overlay mode is
explicit at install time and does **not** modify tracked `.gitignore` or product files:

```powershell
# Do this in a private bootstrap directory OUTSIDE <git-root>.
$bootstrap = Join-Path $env:LOCALAPPDATA 'EngLoopKit\bootstrap\1.14.0'
New-Item -ItemType Directory -Force $bootstrap | Out-Null
Push-Location $bootstrap
dotnet new tool-manifest --force
dotnet tool install engloopkit --version 1.14.0 --add-source <release-dir>

# <release-dir> contains the downloaded .nupkg and extension .zip.
dotnet tool run engloopkit -- overlay install --mode overlay --root <git-root> `
  --tool-version 1.14.0 --tool-nupkg <release-dir>\engloopkit.1.14.0.nupkg `
  --extension-archive <release-dir>\engloopkit-extension-1.14.0.zip
Pop-Location
```

Do **not** first install the tool into `<git-root>` with `dotnet new tool-manifest`:
that would create `.config/dotnet-tools.json` before overlay mode can protect it. The
overlay transaction creates that root-local manifest itself, adds it to local Git excludes,
and installs the matching tool there. ELK derives the local archive identity from
authoritative Git metadata, so installation requires no operator-supplied IDs.

The transaction preflights collisions, writes `.git/info/exclude` before ELK files exist,
installs ELK-owned local `pre-commit`/`pre-push` hooks, and records every managed file in
`.engloop-overlay/manifest.json`. A normal commit/push then contains no overlay file.

Move local state between matching checkouts with:

```powershell
# After install, run these from <git-root>; its local tool is overlay-managed/ignored.
dotnet tool run engloopkit -- overlay verify --root .
dotnet tool run engloopkit -- overlay pack --root . --output <zip-outside-repository>

# Before another checkout has an overlay tool, run unpack from $bootstrap.
Push-Location $bootstrap
dotnet tool run engloopkit -- overlay unpack --root <other-checkout> `
  --input <zip-outside-repository>
Pop-Location
```

Download the published extension archive explicitly before installation; overlay install
does not fetch remote URLs. Archives are plain ZIPs by design and must not contain secrets. Pack/unpack reject
secret-like paths, tracked/colliding files, ZIP-slip entries, hash mismatches, base-revision
mismatches, and repository-origin mismatches. ELK manages ordinary Git-hook protection;
deliberately bypassing Git hooks is outside a repository-local tool’s protection.

For a repository that already owns local agent directories, prompts, or hooks, use the
explicit `--host-mode coexist` contract described in
[`docs/install-overlay.md`](docs/install-overlay.md). It preserves existing host files
byte-for-byte, adds only `speckit.engloop.*` entries, and chains existing local hooks.

## License

MIT — see [LICENSE](LICENSE).
