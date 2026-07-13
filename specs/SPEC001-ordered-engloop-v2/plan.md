# Implementation Plan: Ordered EngLoop v2

**Branch:** `master` (nomenclature checkpoint; implementation uses a dedicated SPEC001 branch)

**Date:** 2026-07-10

**Spec:** [`spec.md`](spec.md)

**Primary authority:** [`REFACT001`](../../.engloop/refactors/REFACT001_ordered-engloop-v2.md)
**Plan status:** AMENDED — READY FOR TASKS WITH BLOCKING P0 CANARY

**Input:** Authoritative feature specification at
`specs/SPEC001-ordered-engloop-v2/spec.md`.

**Nomenclature-checkpoint boundary:** This atomic pre-task change updates only
EngLoopKit's root/artifact names, final prefix vocabulary, current links, source prefix
set/tests, and SPEC001 planning bundle. It creates no `tasks.md`, makes no consumer
change, and does not edit/stage the untracked local `.specify/` cache.

**Hidden-root amendment:** The 2026-07-10 ratification replaces the formerly planned
split visible-memory/config layout with exactly one tracked `.engloop/` process root.
Configuration is `.engloop/config.json`, transient output is ignored under
`.engloop/out/`, and visible root `NORTHSTAR.md` / `LEARNINGS.md` remain entry points.
V2 contains no `.engloopkit/` directory, no live `engloop/` compatibility tree, and no
root-discovery fallback. In this plan, resolvable links and baseline references under
`engloop/` are explicitly current-source paths; implementation updates them after the
history-preserving move.

**Custom-agent amendment:** All 13 command sources and generated `.agent.md` files use
the exact rich VS Code header, least-privilege tool/subagent policy, versioned entry
hook, and review-first handoff graph in SPEC001 and
[`contracts/command-surface.md`](contracts/command-surface.md). Generated prompt files
select the matching agent and omit `tools`. A disposable Spec Kit preservation
experiment is a prerequisite to authoring all production headers; any dropped or
rewritten field fails closed and routes to the smallest supported upstream capability
change rather than post-processing installed output.

## Summary

EngLoopKit 1.7.0 will atomically replace the nine `speckit.engloopkit.*` commands with
the exact 13 ordered `speckit.engloop.*` commands, while retaining product/extension
identity `engloopkit`. Each stage becomes a visible, model-protected VS Code custom
agent with an explicit argument hint, least-privilege tools, an explicit subagent
policy, defense-in-depth entry validation, and only the review-first handoffs that
represent real workflow branches. It replaces numbered direction semantics with one
root living `NORTHSTAR.md`; separates the 01–08 delivery/readiness lane from
demand-driven 20–22 operations and independently opportunistic 30–31 stewardship; and
implements those rules in a rich C# vertical, an independent bounded SEK model, CORD
exploration, generated positive/negative real-SUT conformance, and post-disposition
direct tests.

The design preserves PM001–PM004: readiness is a computed whole-product gate;
verification method follows artifact class; the vertical model is behavior-level; and
generated proof includes model-derived rejection plus non-trivial branching. Stage 07
owns generated functional evidence only. Stage 08 classifies every unreached path,
returns intended gaps, deletes only unsupported/no-entry residue, revalidates each
deletion set, then and only then adds direct unit/property tests and computes a
per-module 95% line/branch PASS/FAIL.

The v2 physical contract consolidates all tracked process memory and required config
under `.engloop/`. Git repositories rename the current tracked `engloop/` tree to
`.engloop/` atomically, then move/evolve the seed to visible root `NORTHSTAR.md`.
Root validation rejects old, forbidden, dual, or case-ambiguous layouts before parsing
config; it never selects or merges a root. Root `LEARNINGS.md` stays visible while its
cards and source history move with the hidden tree.

A domain-free document-validation component and a thin EngLoopKit tool make config,
command, learning, install, and readiness gates deterministic. The Learnings Pyramid
remains `PMxxx/LEARNxxx → cards → LEARNINGS.md → on-demand instruction`, with complete
provenance/link/content checks, both page limits, and clean-context retrieval result
comparison. Final immutable 1.7.0 artifacts are clean-installed into TTHP, the workshop,
and the ExtremeEdge verification consumer one root at a time; no old alias, stale file,
parent lookup, or fallback path survives.

## Technical Context

**Language/Version:** C# with repository `LangVersion=latest`; all executable/model/test
projects target .NET 8 (`net8.0`). PowerShell 7 scripts orchestrate reproducible local/CI
validation. Markdown, YAML, and JSON remain product/config artifact formats.

**SDK:** Repository currently has no `global.json`; CI requests `8.0.x`; this machine has
8.0.128 and 8.0.422. Implementation pins SDK 8.0.422 in `global.json` and CI so Stage 02
uses one selected build runway rather than the machine-default 10.0.301 SDK.

**Primary dependencies:**

- .NET BCL and existing domain-free
  `EngLoopKit.Components.Numbering` / `EngLoopKit.Components.StateMachine`;
- new domain-free `EngLoopKit.Components.DocumentValidation`;
- Spec Kit CLI 0.12.4 for package/install fixtures (manifest minimum remains
  `>=0.12.0` only if compatibility is proven);
- architecture-guard 1.11.0 and tinyspec 1.0.0 remain pinned external bundle
  capabilities; tinyspec is not a Stage 22 route;
- SEK source pinned by exact Git revision, with `Sek.Modeling` project reference,
  stateful path replay, model-derived negative edges, `RequireBound`, and a portable
  single-path generated binding;
- YamlDotNet 18.1.0, pinned in the physically separate domain-free document-validation
  component, parses nested source/generated custom-agent frontmatter into canonical
  semantic projections; generic Markdown links, budgets, and set checks otherwise use
  the BCL.

**Current SEK evidence:** local HEAD
`2bf8d3dc7993d9bd93fc167108f5f7de3c8d2196` contains model-derived negative edges and
stateful path replay. Its CLI project declares 0.1.0 while repository guidance says
0.1.1, and no global `sek` tool is installed. Implementation therefore pins a
capability-bearing commit rather than inferring equivalence from a label. Portable
single-path generated binding is a prerequisite gate; if absent, it lands in SEK first
and the resulting exact revision is pinned.

**Storage:** Git-tracked durable process/config files under `.engloop/`; visible root
Northstar/Learnings; JSON schemas and retrieval cases; C# source; CORD; generated xUnit
source; and package metadata. Required config is `.engloop/config.json`. Fresh reports
live under ignored `.engloop/out/` or test result directories and carry input digests;
stale reports cannot authorize transitions. No database or service.

**Testing:** xUnit 2.9.2, xUnit runner 2.8.2, Microsoft.NET.Test.Sdk 17.11.1 (current
pins); `coverlet.collector` 6.0.2 (verified in local package cache, to be pinned and
proved in Stage 02); SEK-generated xUnit conformance; direct finite/property-style xUnit
checks after reachability disposition; semantic YAML source/install comparisons;
disposable Spec Kit install fixtures; VS Code customization diagnostics and picker/
handoff observations; clean consumer acceptance.

**Target platforms:** Windows developer/consumer roots and `ubuntu-latest` CI. Custom-
agent release acceptance is pinned to VS Code Insiders `1.129.0-insider`, commit
`29d19ddd1af725baf537b6b328843bcdc2d29ba1`; another build is unsupported until the
identical canary passes. Generated projects and focused workspaces contain no absolute
workstation path. PowerShell scripts use `pwsh` on both platforms.

**Project type:** Spec Kit bundle + one first-party extension + small .NET domain
vertical + domain-free components + separately packaged `engloopkit` .NET tool +
independent SEK model/generated tests.

**Performance goals:** No numeric wall-clock SLO is ratified. All loops must be bounded,
record actual duration, avoid a token/test relationship, and keep the existing fast-test
intent. A bound hit, timeout, or unexplained slow outlier is a visible failure or
recorded optimization input, never silent truncation.

**Constraints:** Exact 13 IDs/order; no alias/fallback; exactly one tracked `.engloop/`
with exact local config and ignored output; no `.engloopkit/` or live `engloop/`; one
root Northstar and one visible root Learnings index; numbered direction machinery absent from current semantics;
per-module line and branch coverage each at least 95%; generated negatives and
branching; source/card/index provenance; index at most 500 words and 60 nonblank lines;
no broad learning instruction `applyTo`; exact 13-agent header/tool/subagent/hook
policies and ordered 23-edge `send: false` handoff graph; no agent or handoff model
override; no prompt-level tool override; strict pre-action enforcement requires the
pinned hook-enabled focused workspace, while hook-disabled use is reduced-assurance
with an unconditional body check and independent trusted durable-stage gates; no
cross-root install coupling;
`.specify/` local cache excluded from delivery.

**Scale/Scope:** 13 commands/custom agents and 13 matching prompts in 3 invocation
lanes; 23 exact review-first handoff edges; 4 migrated roots; 3 release artifact kinds;
current 6-project solution grows by one component and one tool; current 11 accepted
learning sources/4 cards, with algorithms designed for future PM/card growth; module/
card/source counts are discovered and compared to explicit authoritative config, not
hard-coded.

**Environment variables/secrets:** None required by the feature. The generated test
binding must not use an environment/default fallback. No `.env` file is needed.

## Constitution, Architecture, and Learning Gate — Pre-Design

The local cache contains no `.specify/memory/constitution.md`; this plan does not invent
one or edit cache state. The governing constitution for this feature is the ratified
SPEC001/REFACT001 contract, ARCH001–ARCH005, the repository architecture/reliability
instructions, and PM001–PM004 through their living cards.

| Gate | Authority | Pre-design result | Evidence/rule carried forward |
|---|---|---|---|
| Scope integrity | User checkpoint boundary | PASS | EngLoopKit nomenclature/root only; no tasks, consumer, or cache edit. |
| Bundle composition | ARCH001 | PASS | Bundle composes; one `engloopkit` extension owns all first-party commands. |
| Command loop | ARCH002 | PASS | Every one of 13 commands retains Trigger/Goal/Actions/Verification/Memory, artifact root, Done when. |
| Numbered memory / Northstar | ARCH003 as superseded by SPEC001 | PASS | Exact final prefix set; monotonic discipline retained; Git owns living Northstar history. |
| Executable agreement | ARCH004 | PASS | Prose, vertical, independent model, CORD, generated tests, and direct tests must agree. |
| Component boundary | ARCH005 | PASS | Generic state/document validation in components; EngLoop semantics in vertical; dependencies one-way. |
| Whole-product readiness | Readiness card; PM001/LEARN001–003 | PASS | Complete inventory; PASS is deterministic output; missing/zero evidence rows cannot disappear. |
| Method by class | Verification-class card; PM002/LEARN001–003 | PASS | Components/pure values direct; stateful vertical behavior-level SEK; same quality bar. |
| Behavior granularity | Observable-behavior card; PM002/LEARN002, PM003/LEARN001–002 | PASS | One representative real-SUT vertical model; no ceremonial model per assembly. |
| Adversarial adequacy | Rejection card; PM004/LEARN001–003 | PASS | Non-trivial branches and generated model-derived illegal/invalid rejection. |
| Systems/reliability | Repository instructions | PASS | Explicit generic metadata/config; unknowns fail closed; no workload heuristic or hidden fallback. |
| Invocation lanes | SPEC001 FR-TRN-008/FR-OPS-006/FR-EVO-005 | PASS | Stage 08 authorizes only; operations and stewardship require independent demand/capacity. |
| Hidden process root | SPEC001 FR-CMD-009/FR-MIG-002–004/SC-016 | PASS | Sole `.engloop/`; exact config/output; visible entry points; old/forbidden/dual roots fail before config discovery. |
| Nomenclature | SPEC001 FR-NOM-001/SC-017 | PASS | Exact nine global plus three local prefixes; living Northstar/Learnings; no allocation alias or fallback. |
| Guided custom agents | SPEC001 FR-AGT-001–013/SC-018–020 | PASS | Exact supported header semantics, justified least privilege, prompt precedence protection, review-first handoffs, strict hook-enabled blocking, honest reduced-assurance behavior, and trusted durable-stage gates. |

No gate violation requires an exception.

## Design Artifacts

- [`research.md`](research.md) — verified baseline, technology versions, decisions, and
  rejected alternatives.
- [`data-model.md`](data-model.md) — rich state/evidence/config/learning/install entities
  and invariants.
- [`contracts/command-surface.md`](contracts/command-surface.md) — exact 13 IDs/files,
  loop shape, rich agent header, tool/subagent matrix, entry hook/body gate, exact
  23-edge handoff graph, prompt policy, and package/install assertions.
- [`contracts/engineering-loop.md`](contracts/engineering-loop.md) — lanes, legal and
  illegal transitions, readiness currency, model requirements.
- [`contracts/evidence-and-readiness.md`](contracts/evidence-and-readiness.md) — explicit
  config, Stage 02 proof, Stage 05–08 evidence and gate formulas.
- [`contracts/learnings-pyramid.md`](contracts/learnings-pyramid.md) — deterministic
  source/card/index/link/content/page validation and retrieval protocol.
- [`contracts/consumer-migration.md`](contracts/consumer-migration.md) — immutable
  artifacts, per-root clean transaction, focused/aggregate workspace behavior.
- [`quickstart.md`](quickstart.md) — runnable post-implementation validation sequence.

## Target Project Structure

```text
EngLoopKit/
├── .engloop/
│   ├── config.json                         # exact explicit root/runway/module/gate contract
│   ├── out/                                # ignored transient reports only
│   ├── README.md                           # moved current process-memory entry point
│   ├── numbering-registry.md               # exact 9 global + 3 local prefixes
│   ├── architecture/
│   │   ├── ARCH001* … ARCH005*              # current authorities/history
│   │   └── ARCH006_ordered-engloop-v2.md
│   ├── scaffolds/
│   │   └── SCAF001_test-runway.md
│   ├── cord/
│   │   ├── CORD001*                         # historical baseline evidence
│   │   └── CORD002_ordered-engloop-v2.md
│   ├── coverage/
│   │   ├── COV001*                          # moved historical evidence
│   │   ├── COV002_ordered-engloop-v2-functional.md
│   │   └── COV003_ordered-engloop-v2-readiness.md
│   ├── incidents/IN001* … IN004*            # moved immutable operational history
│   ├── learnings/
│   │   ├── README.md
│   │   ├── cards/*.md
│   │   └── retrieval-cases.json
│   ├── models/
│   │   ├── MODEL001*                        # historical baseline evidence
│   │   └── MODEL002_ordered-engloop-v2.md
│   ├── postmortems/PM001* … PM004*          # immutable PM/LEARN sources during implementation
│   └── refactors/REFACT001_ordered-engloop-v2.md
├── .github/
│   ├── instructions/
│   │   └── project-learnings.instructions.md
│   └── workflows/ci.yml
├── components/
│   ├── EngLoopKit.Components.Numbering/
│   ├── EngLoopKit.Components.StateMachine/
│   └── EngLoopKit.Components.DocumentValidation/
│       ├── EngLoopKit.Components.DocumentValidation.csproj
│       ├── FrontmatterYaml.cs
│       ├── SemanticProjection.cs
│       ├── MarkdownLinks.cs
│       ├── TextBudget.cs
│       ├── SetCoverage.cs
│       └── ValidationReport.cs
├── docs/
│   ├── component-pattern.md
│   ├── engineering-loop.md
│   ├── loop-engineering.md
│   ├── numbering-registry.md
│   ├── standards.md
│   ├── token-efficiency.md
│   ├── v2-migration.md
│   └── workspaces.md
├── extensions/engloopkit/
│   ├── commands/
│   │   ├── speckit.engloop.01-northstar.md
│   │   ├── speckit.engloop.02-scaffold.md
│   │   ├── speckit.engloop.03-architect.md
│   │   ├── speckit.engloop.04-refactor.md
│   │   ├── speckit.engloop.05-model.md
│   │   ├── speckit.engloop.06-explore.md
│   │   ├── speckit.engloop.07-validate.md
│   │   ├── speckit.engloop.08-unittest.md
│   │   ├── speckit.engloop.20-incident.md
│   │   ├── speckit.engloop.21-postmortem.md
│   │   ├── speckit.engloop.22-repair.md
│   │   ├── speckit.engloop.30-refactor-scan.md
│   │   └── speckit.engloop.31-learnings-pyramid.md
│   ├── templates/
│   │   ├── ARCH-template.md
│   │   ├── SCAF-test-runway-template.md
│   │   ├── COV-functional-template.md
│   │   ├── COV-readiness-template.md
│   │   ├── CORD-template.md
│   │   ├── IN-template.md
│   │   ├── LEARNING-CARD-template.md
│   │   ├── MODEL-template.md
│   │   ├── NORTHSTAR-template.md
│   │   ├── PM-template.md
│   │   ├── REFACT-template.md
│   │   └── engloopkit-config-template.json
│   ├── extension.yml
│   └── README.md
├── model/EngLoopKit.Model/
│   ├── Model.cs                            # independent rich model; no Core reference
│   ├── Config.cord                         # bounded branching scenarios
│   └── EngLoopKit.Model.csproj
├── schemas/
│   ├── engloopkit-config.schema.json
│   ├── retrieval-cases.schema.json
│   └── vscode-agent-surface.schema.json
├── scripts/
│   ├── test-spec-kit-agent-preservation.ps1
│   ├── prove-test-runway.ps1
│   ├── generate-loop-tests.ps1
│   ├── validate-functional.ps1
│   ├── validate-readiness.ps1
│   ├── run-learning-retrieval.ps1
│   ├── validate-package.ps1
│   ├── validate-agent-surfaces.ps1
│   ├── migrate-consumer.ps1
│   └── validate-v2.ps1
├── src/
│   ├── EngLoopKit.Core/
│   │   ├── CommandSurface.cs
│   │   ├── EngineeringLoop.cs
│   │   ├── EngineeringLoopState.cs
│   │   ├── Evidence.cs
│   │   ├── LearningsPyramidPolicy.cs
│   │   ├── Loop.cs
│   │   ├── NumberingRegistry.cs
│   │   ├── ReadinessGate.cs
│   │   └── EngLoopKit.Core.csproj
│   └── EngLoopKit.Tool/
│       ├── Program.cs
│       ├── ValidationCommands.cs
│       └── EngLoopKit.Tool.csproj
├── tests/
│   ├── EngLoopKit.Loop.Generated/          # generator-owned stable destination
│   └── EngLoopKit.Tests/
│       ├── BundleConformanceTests.cs
│       ├── CommandSurfaceTests.cs
│       ├── DocumentValidationTests.cs
│       ├── EngineeringLoopTests.cs
│       ├── EvidenceCurrencyTests.cs
│       ├── AgentSurfaceValidationTests.cs
│       ├── InstallationValidationTests.cs
│       ├── LearningPyramidValidationTests.cs
│       ├── NumberingRegistryTests.cs
│       └── ReadinessGateTests.cs
├── global.json
├── NORTHSTAR.md                            # living direction; Git retains prior revisions
├── LEARNINGS.md                            # visible one-page learning entry point
├── EngLoopKit.slnx
├── bundle.yml
├── catalog.json
└── CHANGELOG.md
```

**Structure decision:** Keep one EngLoopKit domain vertical and the existing independent
SEK model. Extend the existing generic state component only with domain-free atomic
guard/result primitives; add one domain-free document-validation component. The new
thin tool depends on Core/components and packages deterministic gates for consumers.
The model references only `Sek.Modeling`, never Core. Generated tests bind to the real
Core SUT through one portable generated contract. Dependencies remain
`vertical/tool → components`, never the reverse.

## File-Impact Map

The nomenclature checkpoint already established `.engloop/`, root Northstar, final
active artifact/template filenames, exact prefix sets, current links, and SPEC001
identity. Paths below therefore start from that accepted pre-task baseline. Consumer
root migrations remain future, repository-local transactions.

### Completed before task decomposition

| Path(s) | Checkpoint result |
|---|---|
| `.engloop/`, `NORTHSTAR.md`, `LEARNINGS.md` | History-preserving root and direction moves complete; current links resolve; transient `.engloop/out/` is ignored. |
| `.engloop/{architecture,models,cord,refactors}/`, `extensions/engloopkit/templates/` | Active files use ARCH/MODEL/CORD/REFACT and Northstar names. |
| Registries, `NumberingRegistry`, tests, standards, commands | Exact `SPEC,SCAF,ARCH,MODEL,CORD,COV,IN,PM,REFACT,MIT,LEARN,RPI` set; no aliases or fallback. |
| `specs/SPEC001-ordered-engloop-v2/` | Feature and all planning artifacts use final vocabulary; requirements total 102 and criteria total 20. |

### Add during v2 implementation

| Path(s) | Purpose |
|---|---|
| `global.json` | Pin .NET SDK 8.0.422 used by Stage 02 and CI. |
| `.engloop/config.json`, `schemas/engloopkit-config.schema.json` | Sole explicit local artifact/runway/module/evidence config and schema; fixed root/output values; fail-closed validation. |
| `schemas/retrieval-cases.schema.json` | Stable clean-context case/result input contract. |
| `schemas/vscode-agent-surface.schema.json` | Immutable expected frontmatter/prompt/hook projection and pinned VS Code/doc provenance for 1.7.0 acceptance. |
| `.engloop/architecture/ARCH006_ordered-engloop-v2.md` | Governing v2 lanes, state/evidence, Northstar, tool/component boundaries; supersedes only named v1 clauses. |
| `.engloop/scaffolds/SCAF001_test-runway.md` | Durable five-observation Stage 02 runway proof and generated destination. |
| `.engloop/models/MODEL002_ordered-engloop-v2.md` | Rich independent v2 model record. |
| `.engloop/cord/CORD002_ordered-engloop-v2.md` | Bounded branching/positive-negative exploration record. |
| `.engloop/coverage/COV002_ordered-engloop-v2-functional.md` | Stage 07 generated-only functional/reachability evidence, no readiness claim. |
| `.engloop/coverage/COV003_ordered-engloop-v2-readiness.md` | Stage 08 disposition and complete per-module final gate. |
| `.engloop/learnings/retrieval-cases.json` | Cases covering every current card and at least one source per PM. |
| 13 exact `extensions/engloopkit/commands/speckit.engloop.*.md` files | Clean v2 picker surface. |
| New templates listed in target tree | Northstar, runway, split functional/readiness, card, and config contracts. |
| `components/EngLoopKit.Components.DocumentValidation/*` | Domain-free semantic YAML projection plus link/text-budget/set-coverage/report machinery; pin YamlDotNet 18.1.0 here only. |
| `src/EngLoopKit.Core/{CommandSurface,EngineeringLoopState,Evidence,LearningsPyramidPolicy,ReadinessGate}.cs` | EngLoop-specific v2 vertical semantics. |
| `src/EngLoopKit.Tool/*` | Deterministic standalone config/command/learning/install/reachability/readiness CLI; package/tool ID `engloopkit`. |
| Ten `scripts/*.ps1` files listed above | Reproducible Spec Kit canary/runway/generation/coverage/agent-surface/package/migration orchestration with no hidden alternate path; fresh reports/release manifest go only to `.engloop/out/`. |
| New direct test files listed above | Added only after Stage 08 disposition; contract/property/deep behavior coverage. |
| `docs/v2-migration.md`, `docs/workspaces.md` | Breaking migration and focused-vs-integration workspace guidance. |

### Modify in EngLoopKit

| Path(s) | Required change |
|---|---|
| `Directory.Build.props`, `EngLoopKit.slnx` | Add projects/pins and consistent build settings; no reverse component dependency. |
| `.gitignore` | Ignore fresh `.engloop/out/`, coverage, and package scratch output; keep `.engloop/config.json` and durable memory/retrieval cases tracked. Do not ignore all of `.engloop/`. |
| `.github/workflows/ci.yml` | Pin SDK, Spec Kit, SEK revision; prove runway; regenerate/freshness-check; separate Stage 07/08; validate learning/package/install. Remove environment binding fallback. |
| `components/EngLoopKit.Components.StateMachine/StateMachine.cs` | Add only generic atomic guarded-transition/result support required by rich vertical; retain zero EngLoop names. |
| `src/EngLoopKit.Core/{EngineeringLoop,Loop,NumberingRegistry}.cs` and csproj | Replace graph-only v1 semantics with rich stateful v2 SUT; preserve the checkpoint's exact prefix set; compose new generic component. |
| `model/EngLoopKit.Model/{Model.cs,Config.cord,csproj}` | Replace single-stage legal-only model with independent rich bounded v2 model and branch scenarios; keep no Core project reference. |
| `.specexplorerkit/config.json` | Point to v2 machine/binding contract while retaining root-relative paths. |
| `tests/EngLoopKit.Loop.Generated/*` | Delete/regenerate atomically from pinned SEK into same destination; no hand edits/absolute path/fallback. |
| Existing direct tests | Preserve as regression assets, minimally compile during Stage 04, then update/expand only after Stage 08 disposition. |
| `extensions/engloopkit/{extension.yml,README.md}` | Version 1.7.0, exact ordered 13 entries, v2 workflow ownership/lanes, no old current IDs. |
| Retained ARCH/MODEL/CORD/IN/PM/REFACT templates | Stage numbers, evidence ownership, model rejection, repair full-loop closure, no numbered-direction/tinyspec repair route. |
| `bundle.yml`, `catalog.json` | Version/release text/counts/checksums; bundle remains composition-only. Move tool requirement under supported `requires.tools`; do not claim it is auto-installed. |
| `README.md`, `CHANGELOG.md`, all six current `docs/*.md`, `examples/sek-walkthrough.md` | Coherent v2 narrative, exact lanes/order, Northstar, Stage 02 proof, Stage 05–08 split, learning/release/workspace guidance. Preserve clearly historical changelog evidence. |
| `.github/skills/using-sek-to-generate-tests/SKILL.md` | Pinned/source usage, portable binding, stable destination, Stage 07-only reachability, PM001–PM004. |
| `.engloop/README.md`, `.engloop/numbering-registry.md` | Preserve checkpoint root/links; increment SCAF/ARCH/MODEL/CORD/COV exactly before future files are created. |
| `docs/numbering-registry.md`, `docs/standards.md` | Preserve exact final prefix set and living-document exception while adding executable config/gate detail. |
| ARCH001–ARCH005 | Add minimal explicit “refined/superseded in part by ARCH006/SPEC001” references where current rules changed; retain rationale. |
| `.engloop/models/MODEL001*`, `.engloop/cord/CORD001*`, `.engloop/coverage/COV001*` | Preserve historical-baseline status and supersede with v2 records; do not rewrite their evidence as current readiness. |
| Four learning cards | Add explicit conflicts/supersessions/tension state while preserving PM citations. |
| `.engloop/learnings/README.md` | Replace manual-only gate description with deterministic validator/retrieval process. |
| Root `LEARNINGS.md` and learning instruction | Change only if deterministic validation requires it; preserve concise prototype when it already passes. Never add broad `applyTo`. |

### Remaining delete/replace work during implementation

| Path(s) | Disposition |
|---|---|
| Nine `extensions/engloopkit/commands/speckit.engloopkit.*.md` files | Delete in the same atomic change that adds the 13 v2 files/manifest. No aliases. |
| `extensions/engloopkit/templates/COV-template.md` | Delete; replaced by separate functional and readiness templates. |
| Nine retained v1 command registrations and generated install surfaces | Remove only when the exact 13 v2 files and manifest entries are added atomically; the nomenclature checkpoint creates no new command files. |

### Future implementation changes in consumer roots

These are intentionally not made in this planning run.

| Root | Add/move | Modify | Delete/replace |
|---|---|---|---|
| `tthp` | `git mv engloop .engloop`; then move/evolve its tracked initial direction file to `NORTHSTAR.md`; add `.engloop/config.json`, visible `LEARNINGS.md`, root-local tool manifest, `tthp.code-workspace` | README, target `.engloop/` README/standards/registry/links, tracked registry/install outputs | Current `engloop/`, legacy direction path/row, forbidden config root if found, nine old agents/prompts, old installed commands/templates/dev outputs; replace atomically with exact 13 v2 outputs |
| `engloop-workshop` | `git mv engloop .engloop`; add reviewed `NORTHSTAR.md`, `.engloop/config.json`, visible `LEARNINGS.md`, tool manifest, `engloop-workshop.code-workspace` | Curriculum README and target `.engloop/` memory/registry/links; tracked registry/install outputs | Current `engloop/`, planned legacy direction row/path semantics, forbidden config root if found, and all tracked v1 generated/install outputs; replace atomically with v2 |
| `VerifyExtremeEdgeWithTpcc` | Under a complete checksummed backup and explicit `InitializeGit` tracking mode, create target `.engloop/{config.json,README.md,standards.md,numbering-registry.md}` directly plus reviewed `NORTHSTAR.md`, visible `LEARNINGS.md`, tool manifest, focused workspace | New source-control ownership, registry/install/generated outputs, and documentation without changing TPC-C/ExtremeEdge semantics | Any current/forbidden root causes pre-mutation failure; replace all v1 EngLoop generated/install files after backup; no pre-bootstrap history claim or intermediate compatibility tree |

### Explicit no-change historical/source set

- `.engloop/postmortems/PM001`–`PM004` and their final `LEARN` source text remain
  immutable during implementation after this requested identity migration.
- `.engloop/incidents/IN001`–`IN004` remain historical operational evidence.
- REFACT001 and SPEC001 remain current planning authorities.
- Consumer application/model behavior is not rewritten merely to install EngLoopKit.
- Local `EngLoopKit/.specify/` remains untracked cache and outside all tasks/artifacts.

## Dependency Order

```text
Ratified SPEC001/REFACT001 + ARCH/PM gates
    ↓
Nomenclature checkpoint PASS: `.engloop/`, Northstar, exact prefix set, final active filenames
    ↓
Exact `.engloop/config.json` schema + SDK/Spec Kit/SEK pins
    ↓
Stage 01 Northstar/config validation
    ↓
Stage 02 xUnit/coverage runway proof + stable generated destination
    ↓
ARCH006 / component and vertical boundaries
    ↓
Stage 04 atomic command/tool/core/docs implementation
    ↓
Stage 05 independent rich model
    ↓
Stage 06 bounded CORD + portable generated suite
    ↓
Stage 07 generated-only real-SUT validation/reachability
    ↓
Stage 08 classify → return/delete → revalidate → direct tests → readiness
    ↓
Learning/package/release gates
    ↓
Immutable 1.7.0 artifacts
    ↓
TTHP → workshop → verification consumer clean migrations
    ↓
Focused-workspace acceptance + mega-workspace integration view
```

No downstream phase may consume a stale predecessor. The implementation does not
parallelize distributed consumer migrations; only read-only preflight may run in
parallel.

## Phased Implementation Strategy

### Phase 0 — Freeze authority and create the implementation branch

**Actions**

- Create a dedicated SPEC001 implementation branch from the accepted checkpoint; record source and
  sibling SEK revisions.
- Snapshot current IDs/files/versions, module projects, learning graph, and each
  consumer's install ownership.
- Snapshot current/target/forbidden process-root names, config candidates, Git tracking,
  links, ignore rules, and direction/Northstar/Learnings entry points for all four roots.
- Add contract-first checks for exact IDs, sole `.engloop/`, exact config/output paths,
  and explicitly scoped current-source/historical-path exemptions.
- Add `scripts/test-spec-kit-agent-preservation.ps1`; it generates an ignored temporary
  .NET/YamlDotNet 18.1.0 harness only below `.engloop/out/spec-kit-agent-canary/` and
  owns cleanup/retention and a complete mismatch report without adding production code.
- Build the minimal disposable Spec Kit 0.12.4 preservation fixture that exercises all
  required agent scalar/list/map fields, empty/nonempty subagent policies, nested
  `SessionStart`, branching and terminal handoffs, required absences, and matching
  prompt selection without prompt `tools`.
- Install the fixture only through the supported Spec Kit path and compare source,
  installed command, generated agent, and generated prompt YAML semantically. If any
  field is dropped or rewritten, stop and route the smallest generic upstream Spec Kit
  capability change or upstream-implemented/documented generation mode; do not author
  13 production headers, add an EngLoopKit-owned generator, or post-process output.
- Verify `.specify/` remains unstaged before and after every phase.

**Checkpoint P0:** Baseline report is reproducible; root migration mode and rollback
boundary are explicit per repository; any dual/forbidden root is a blocking finding;
the Spec Kit canary preserves every required agent/prompt semantic field and absence or
has produced a blocking upstream-capability item; no ratified choice is reopened and no
unrelated consumer change is present.

### Phase 1 — Stage 01: validate direction and establish config

**Actions**

- Consume the checkpoint's sole tracked `.engloop/` root and root `NORTHSTAR.md`; stop
  if a visible compatibility root, `.engloopkit/`, dual root, or ambiguous direction appears.
- Verify Git rename ancestry for the process tree and Northstar rather than repeating
  or copying either move.
- Increment no counter: Northstar is living, not numbered.
- Validate every required Northstar section and preserve its evidence-backed revision.
- Add explicit `.engloop/config.json` in an initially honest state; add the ignore rule
  only if needed for `.engloop/out/`, never for the durable root.
- Revalidate current links and exact nomenclature while keeping root `LEARNINGS.md`
  visible and its card/source links resolving.
- Prove current `engloop/` and `.engloopkit/` counts are zero before accepting the phase.

**Checkpoint P1:** Exactly one tracked `.engloop/`, one `.engloop/config.json`, ignored
`.engloop/out/`, visible root Northstar/Learnings, zero current `engloop/` and
`.engloopkit/`, zero legacy numbered-direction artifact/template/counter semantics, resolving links,
and Git ancestry for both the root tree and Northstar. Routine historical records
remain intact.

### Phase 2 — Stage 02: pin and prove the test runway

**Actions**

- Add `global.json` and pin existing xUnit/Test SDK plus `coverlet.collector` 6.0.2.
- Establish the exact terse EngLoopKit test command and one real boundary test.
- Implement/run controlled failure proof; require discovery, pass, expected non-zero
  failure, cleanup, and restored pass from the same command.
- Record `SCAF001` and set config runway status/destination only after all observations.
- Establish the pinned SEK prerequisite. If current generator cannot emit one portable
  binding path, stop and land the generic SEK capability before continuing.

**Checkpoint P2:** Runway evidence is complete/current; no intentional failure remains;
`tests/EngLoopKit.Loop.Generated/` is the one proven destination; no absolute/fallback
binding contract is accepted.

### Phase 3 — Stage 03: ratify v2 architecture

**Actions**

- Increment ARCH to ARCH006 before creating it.
- Record the rich-state vertical, explicit config, tool packaging, generic document
  component, one-way dependencies, generated destination, and evidence currency.
- Add minimal supersession links to ARCH001–ARCH005; do not rewrite historical decisions.
- Run architecture governance and capture Stage 04 violations/tasks.

**Checkpoint P3:** Architecture review has a governed target; generic code has a
component destination; no EngLoop stage/PM semantics appear in components.

### Phase 4 — Stage 04: implement the atomic v2 product contract

**Actions**

- Add generic state/document machinery, rich Core state/evidence/gates, thin tool, config
  schema, command package, templates, scripts, and docs.
- Promote the P0 canonical YAML projection into the governed document-validation
  component and validate it against the tracked pinned VS Code schema snapshot.
- Replace manifest/package registrations and nine command files atomically with 13.
- Author all 13 source headers only after P0 preservation passes. Apply the exact
  stage-specific descriptions, argument hints, tool/subagent rows, expanded entry hook,
  unconditional body validator, trusted durable-operation gate, and ordered handoffs;
  omit `infer`, agent `model`, and handoff `model`.
- Ensure every generated prompt selects the exact matching agent and omits `tools`, so
  prompt precedence cannot replace the agent's least-privilege policy.
- Preserve and validate the checkpoint's exact 9-global/3-local prefix set across code,
  registries, templates, and docs.
- Keep existing direct tests only as compile/regression assets; do not add direct tests
  to justify newly unreached production paths.
- Make command/tool/config failures explicit and state-preserving.
- Update model-independent package/install validation and build all projects.

**Checkpoint P4:** Green build and architecture gate; source/archive/disposable install
contains exact 13 v2 IDs and zero old current IDs; source headers pass the exact common
field, policy, entry-validation, and 23-edge graph checks; generated prompts have zero
tool overrides; no readiness claim is made.

### Phase 5 — Stage 05: build the richer independent model

**Actions**

- Increment MODEL to MODEL002 before creating the record.
- Replace the stage-only model with bounded interacting state for delivery cursor,
  revisions/readiness, repair, learning refresh, incident demand/capacity, and
  reachability disposition.
- Encode legal guards and expected rejection for unknown input, ordering, duplicate
  start, stale evidence, absent demand, and bypasses.
- Keep model project independent of Core and separate product guards (`Require`) from
  exploration bounds (`RequireBound`).

**Checkpoint P5:** Model builds/validates, has materially interacting state, reproduces
ratified paths, and cannot satisfy adequacy with a flat positive tour.

### Phase 6 — Stage 06: explore and generate

**Actions**

- Increment CORD to CORD002.
- Author bounded CORD scenarios for 01–08, 20–22 demand, 30 no-work/direction branches,
  independent 31 refresh, Stage 08 feedback, and required negatives.
- Explore with deterministic settings; fail on unexplained bound hit.
- Generate atomically to the Stage 02 destination using pinned SEK; compare fresh output
  to committed generated source; never hand-edit it.

**Checkpoint P6:** Distinct paths/branches and model-derived legal, illegal-order, and
invalid-input tests exist; generated project is portable and builds without SEK.

### Phase 7 — Stage 07: validate the real SUT and publish functional reachability

**Actions**

- Increment COV to COV002.
- Run only generated tests against the stateful Core SUT.
- Collect generated-only coverage/reachability and positive/negative branch metrics.
- Route any failure to Stage 04, 05, or 06 by evidence; do not delete tests or merge unit
  coverage.
- Emit functional PASS/FAIL only.

**Checkpoint P7:** All generated functional tests are green; required negative classes
and branching are proven; every production path is reached/unreached in a current
Stage 07 map; no readiness verdict exists.

### Phase 8 — Stage 08: disposition, prune, direct tests, readiness

**Actions (strict order)**

1. Consume COV002 generated-only reachability.
2. Classify every unreached path without adding direct tests.
3. Return intended gaps through 05→06→07.
4. Delete only unsupported/no-entry residue in coherent sets.
5. After every set, rebuild, run architecture checks, rerun complete Stage 07, and stop
   on red.
6. Repeat until all surviving paths are functionally justified or reviewed direct-only.
7. Add/update direct unit/property/contract tests for surviving components, value types,
   vertical units, tool, command/package/install structures, and learning validator.
8. Run whole-product coverage separately and compute one row per authoritative module.
9. Increment COV to COV003 and emit sole PASS/FAIL.

Existing pre-v2 tests may remain regression assets, but Stage 07 never consumes them and
new direct evidence is not accepted before disposition.

**Checkpoint P8:** 100% path disposition; green post-deletion generated proof; every
surviving module architecture/regression green and at least 95% line and 95% branch;
complete inventory PASS. Anything else is NOT READY and blocks release/migration.

### Phase 9 — Stage 31/product coherence and release candidate

**Actions**

- Implement static source/card/index/link/content/budget validation in the generic
  component + vertical policy/tool.
- Add explicit tension sections to all four current cards; keep PM sources immutable.
- Add retrieval cases covering every card and every PM; run isolated retrieval and
  exact-set comparison.
- Update all current v2 docs, skill, templates, example, bundle/catalog/changelog, and
  migration/workspace guidance.
- Run the full 13-agent semantic source/install comparison, target resolution, exact
  prompt-policy checks, supported VS Code customization diagnostics, picker ordering,
  and handoff display/click observations. Prove invalid entry is mechanically blocked
  with `chat.useCustomAgentHooks: true`; with hooks disabled, record reduced assurance,
  observe unconditional body rejection, and prove trusted-tool rejection of every
  invalid durable transition/evidence attempt.
- Verify focused workspaces protect generated agent/prompt files, the local tool
  manifest, and `.engloop/config.json` from automatic edits without enabling broad
  approvals, Bypass Approvals, Autopilot, or nested subagents.
- Pack the 1.7.0 tool, extension, and bundle once; record SHA-256; run source/archive/
  disposable-install gates against exact bits.
- Emit a fresh `.engloop/out/release-manifest.json` naming the exact immutable artifact
  paths and digests used by the migration quickstart; its existence alone is not proof.

**Checkpoint P9:** Learning static/retrieval PASS; all 13 installed agents and prompts
match source semantics, all 23 handoffs and seven `Explore` references resolve, strict-
hook and reduced-assurance body/durable-gate entry evidence pass, and EngLoop-owned
customization diagnostics are empty; docs/manifests/executable/model/tests agree;
immutable release candidate passes all local gates.

### Phase 10 — Clean consumer migrations

Migrate sequentially: TTHP, workshop, then verification consumer. For each, execute the
transaction in `contracts/consumer-migration.md`: capture, prove an unambiguous source
layout/tracking mode, rename or create the hidden root, update direction/config/links,
prove forbidden-root absence, remove v1, install exact artifacts, prove 13/0 surface,
prove standalone/focused use, then commit/record.

- TTHP and workshop first use `git mv engloop .engloop`; TTHP then moves/evolves its
  tracked initial direction file to Northstar. Both update moved links/config in the same coherent commit.
- Workshop creates its own reviewed Northstar and visible Learnings index; it does not
  borrow TTHP direction.
- Verification consumer uses explicit `InitializeGit` mode after a complete checksummed
  filesystem backup, creates `.engloop/` directly, and preserves current TPC-C
  model/tasks. It creates no intermediate visible/forbidden root, tracks all durable
  target files before acceptance, and claims no ancestry before the bootstrap.
- Unproven consumer runways remain explicitly unproven; later stages fail closed rather
  than choosing a stack.

**Checkpoint P10 (per root):** one source-control-tracked `.engloop/`, one exact config,
ignored output, visible Northstar/Learnings, no current `engloop/`, `.engloopkit/`, or
legacy numbered-direction machinery, resolving links, exact 13/0 install, standalone folder/focused workspace PASS,
13 semantically conformant agents/prompts, exact handoffs and prompt policy, empty
EngLoop customization diagnostics, strict-hook and reduced-assurance body/durable-gate
invalid-entry evidence, and no parent/sibling dependency. Do not start the next root
before PASS.

### Phase 11 — Final integration and publish

**Actions**

- Open the mega-workspace as integration view; verify expected independent
  registrations and documentation, not global deduplication.
- Run final release gate against the exact P9 artifacts and all P10 reports.
- Publish 1.7.0 artifacts/catalog/checksums without rebuilding accepted bits.
- Record release evidence and leave follow-up work only through a new governed version.

**Checkpoint P11:** All SC-001–SC-020 pass; release artifacts and catalog hashes agree;
no fallback/alias/stale surface exists.

## Testing and Evidence Strategy

### 1. Stage 02 runway evidence

Purpose: prove the chosen framework can build, discover, pass, fail observably, clean
up, and repass through the real product boundary. It selects the stable Stage 06
destination. It is not architecture, functional adequacy, coverage, or readiness.

### 2. Generated functional validation (Stages 05–07)

- Independent model and CORD are the source of tests.
- Generated suite alone runs against the real stateful SUT.
- Required evidence: legal success, model-derived illegal order, model-derived invalid
  input, materially distinct branches, all declared legal transition classes, and
  representative bypass/stale/duplicate/absent-demand rejection.
- Coverage from this run is functional reachability/classification input only.
- A generated failure is preserved and routed; no hand-authored substitute or deleted
  test can turn it green.

### 3. Dead-code/reachability disposition (Stage 08 first half)

- Compare generated reachability to the authoritative module/source map.
- Treat non-reachability as an investigation signal.
- Attach requirement/runtime-entry authority to intended paths.
- Delete only paths with neither authority nor entry mechanism.
- Rebuild + architecture + complete Stage 07 after each coherent deletion set.
- Record all dispositions and their evidence revision.

### 4. Direct unit/property and structural validation (Stage 08 second half)

After disposition only:

- `Numbering` and generic state/document components receive exhaustive finite,
  property-style, boundary, malformed-input, and path-safety tests.
- Core receives deep rich-state/evidence-currency/reason-code tests; these deepen but do
  not replace generated vertical proof.
- Tool/config/command/package/install validators receive direct and disposable-fixture
  tests.
- Historical/current-surface scoping is tested so old IDs in PM/changelog evidence do
  not become aliases and current old IDs cannot hide behind an over-broad exclusion.

### 5. Whole-product readiness

The explicit module discovery output must equal config inventory. Every surviving
module gets exactly one row. At minimum the inventory covers both existing components,
the new document component, Core, and Tool; model/test assemblies are classified as
test/spec artifacts rather than omitted production modules. Each production row must
meet its class method, architecture, regressions, disposition, and measured 95% line /
95% branch. Zero evidence and missing rows are explicit FAIL.

### 6. Learnings Pyramid

Static deterministic tests cover source/card/index/instruction graph and budgets.
Clean-context cases separately prove retrieval; exact result-set comparison rejects
missing and false provenance. Input digests prevent a prior run validating changed
memory.

### 7. Package and consumer acceptance

Source manifest, archive payload, disposable clean install/remove, three consumer roots,
folder-open, focused workspace, and aggregate workspace are distinct gates. A dev install
may support development but never substitutes for final exact-artifact acceptance.
Every root-specific gate first proves one source-control-tracked `.engloop/`, exact config,
ignored output, visible Northstar/Learnings, zero old/forbidden roots, and resolving
post-move links; package success cannot hide a failed root migration.

### 8. Custom-agent UX and entry protection

- Parse source commands, installed commands, generated agents, and generated prompts
  with YamlDotNet into canonical semantic projections; do not compare raw formatting or
  use regex as the authority for nested values.
- Prove all 13 required common headers, exact tool/subagent policies, exact expanded
  hooks, required field absences, and the ordered 23-edge handoff graph.
- Resolve every handoff target and every allowed `Explore` target. An unavailable tool
  or agent is failure even if VS Code would ignore it.
- Prove each prompt selects its exact agent and has no `tools` override.
- Record zero EngLoop-owned customization errors/warnings and exactly 13 visible picker
  rows in a supported VS Code build.
- Verify selecting every handoff only changes agent/context and pre-fills the exact
  prompt: it does not submit, mutate loop state, satisfy evidence, or schedule a lane.
- Exercise controlled invalid entry with Preview hooks enabled (exit 2 mechanically
  blocks). With hooks disabled, record reduced assurance, observe the mandatory body
  check's rejection, and prove trusted tooling rejects every invalid durable
  transition/evidence acceptance; do not claim platform-equivalent pre-action blocking.

## Migration Sequencing and Failure Boundaries

### Atomic EngLoopKit cutovers

- Treat the completed root/Northstar/nomenclature checkpoint as the accepted task
  baseline. Do not repeat, copy, reverse, or create a compatibility tree for those moves.
- Before config or implementation work, require sole `.engloop/`, no `.engloopkit/` or
  current `engloop/`, tracked durable files, final active filenames, and exact prefix sets.
- Add config and its schema coherently on top of that baseline; do not introduce another
  discovery root or fallback.
- Do not commit a manifest that references both namespaces or references missing files.
- Add 13/delete 9/update manifest/templates/docs/tests in one coherent cutover commit
  after source checks pass.
- Preserve the checkpoint's Northstar ancestry and zero legacy direction machinery
  throughout implementation.
- Generated output replacement is atomic: generate to temporary, validate, then replace
  destination; failed generation makes Stage 07 unavailable rather than reusing old
  files.

### Evidence rollback

A failed phase leaves its gate FAIL and does not mutate the accepted evidence pointer.
A retry repeats the same authoritative operation after correction; it never switches
framework, source, provider, binding, destination, or test set silently.

Root migration rollback is whole-transaction only. Before commit, reset/reverse the
captured rename, Northstar, config, links, ignore, and generated surfaces. After a
coherent Git commit, revert that commit as a unit. Rollback for the consumer that began
without Git deletes the full partial target and newly initialized tracking boundary,
then restores the complete checksummed backup. No rollback may leave both
`engloop/` and `.engloop/`, recreate `.engloopkit/` as a config source, or make runtime
fallback part of v2.

### Release rollback

- Before publish: revert the coherent implementation/migration commit or restore the
  complete consumer snapshot.
- After publish: do not alter 1.7.0 bits. Roll back explicitly to exact 1.6.0 in affected
  roots or fix forward under a new version.
- Rollback is an operator-visible version operation, not runtime compatibility or
  fallback.

### Distributed consumer boundary

There is no global transaction across repositories. Each root has its own capture,
commit/record, and PASS before the next starts. TTHP/workshop use existing Git history;
verification uses full checksummed backup/restore plus explicit Git bootstrap. No root
consumes another root's installation during acceptance.

## Versioning and Release Implications

- Maintainer-governed evolution release: **1.7.0**. EngLoopKit remains on the 1.x
  maturity runway even though this release deliberately replaces pre-2 command
  identities and generated surfaces. The internal “Ordered EngLoop v2” label does not
  authorize product version 2.x; only a later explicit maintainer decision can do so.
- Preserve extension/bundle/tool/product ID `engloopkit`; do not rename the product.
- Keep architecture-guard 1.11.0 and tinyspec 1.0.0 pinned unless a separate verified
  compatibility need changes them. Stage 22 docs/code contain no tinyspec route.
- Catalog command count changes 9→13; extension/bundle/tool/catalog versions agree.
- Release notes explicitly list every removed old command and exact new ID; “no aliases”
  is prominent.
- Extension/bundle/tool archives receive SHA-256 and build revision. Catalog checksum is
  computed from final artifact.
- CI pins SDK, Spec Kit, and SEK revision; generated-source freshness is a release gate.
- Historical v1 docs/changelog remain evidence; current product docs expose only v2.

## Complexity Tracking

The constitution has no violation, but these deliberate complexities require explicit
rationale.

| Complexity | Why needed | Simpler alternative rejected because |
|---|---|---|
| Rich state/evidence model beyond a transition graph | Readiness currency, repair, learning refresh, demand lanes, and reachability are binding behavior. | A stage enum cannot reject stale evidence or distinguish authorization from scheduling. |
| New domain-free document-validation component | Link/content/budget/set checks are reusable mechanics and ARCH005 forbids placing them in the EngLoop vertical. | Ad-hoc regexes inside Stage 31 mix generic parsing with PM/LEARN semantics and are hard to test. |
| Separately packaged thin `engloopkit` tool | Consumers need deterministic gates; Spec Kit 0.12.4 bundles do not install arbitrary tools. | xUnit-only validation is unavailable in consumers; pretending an extension embeds/install tools is unsupported fallback behavior. |
| Exact hidden root/config/schema | Standalone roots require one authoritative `.engloop/` with local artifact/runway/module commands and ignored transient output. | Split `.engloopkit/` config, visible-root compatibility, defaults, name discovery, or parent lookup violate the ratified root and fail-closed rules. |
| Two coverage runs and two COV records | Generated reachability and final direct+generated coverage answer different questions. | Merging them lets unit tests inflate functional proof and erase Stage 08 ordering. |
| Pinned upstream SEK capability | Model-derived negatives and portable generated binding are load-bearing. | Global/latest or post-processed generated output is stale/non-reproducible and can hide capability gaps. |
| One-root-at-a-time migration | Generated surfaces are tracked in two existing Git roots and the third requires explicit tracking bootstrap. | Force-overwrite/multi-root shared install can leave stale mixed files with no safe rollback. |

## Risk Register

| Risk | Likelihood / impact | Prevention and detection | Failure response |
|---|---|---|---|
| SEK pin lacks portable binding | Medium / High | Capability preflight before P2; generated-source scan rejects absolute/default alternate paths. | Stop; land generic SEK capability, pin new SHA, repeat P2. |
| Rich model state explodes exploration | Medium / High | Bounded abstractions, finite booleans/small enums, scenario slicing, `RequireBound`, recorded bound metrics. | Return to Stage 05/06; never accept truncated coverage. |
| Negative edges are numerous/noisy | Medium / Medium | CORD scopes required rejection classes; distinguish product guard from bound; inspect generated negative classes. | Refine model/scenarios, not hand-write substitute assertions. |
| Existing unit tests influence Stage 07 | Medium / High | Dedicated generated project/coverage invocation and report provenance/digests. | Invalidate Stage 07 evidence and rerun isolated suite. |
| Dead code is deleted from non-reachability alone | Low / High | Mandatory authority/entry fields and reviewed disposition; ambiguity remains blocking. | Restore deletion set, classify, route intended path through 05–07. |
| Learning parser misidentifies sources/anchors | Medium / High | Generic parser tests, exact Learnings-section/source/content checks, malformed fixtures. | Validator FAIL; do not clear refresh obligation. |
| Clean-context retrieval is contaminated | Medium / Medium | Fresh context per case, no expected IDs in prompt, input/result digests. | Invalidate run and repeat clean; no partial PASS. |
| Root index budget algorithm differs by platform | Low / Medium | Unicode/LF algorithm in contract and cross-platform golden tests. | Tool FAIL until deterministic parity restored. |
| Old generated files survive uninstall | High / High (verified current surfaces) | Registry-owned inventory, remove→absence proof before install, exact post-set comparison. | Stop root; remove only proven owned residue; rerun absence. |
| Consumer config guesses an unchosen stack | Medium / High | Explicit `unproven` runway state; no destination/framework default. | Later stages reject `missing-proven-runway`; Stage 02 must decide/prove. |
| Verification consumer begins without Git history | High / High | Full checksummed backup, explicit `InitializeGit`, tracked-root proof, and post-state checksum report. | Remove the bootstrap/partial target and restore the complete set; never mix selected v1 files with v2. |
| Package versions/hashes drift after acceptance | Low / High | Immutable artifacts, one build, SHA verification, no rebuild under 1.7.0. | New 1.x version or explicit rollback; never overwrite release. |
| Mega-workspace duplication is mistaken for install defect | High / Low | Focused workspace docs/tests and explicit integration-view expectation. | Use one-root entry point; do not cross-root-couple. |
| Historical old IDs trigger false alias failure or are accidentally rewritten | Medium / Medium | Narrow historical path exemptions and immutable PM checks. | Fix scope; preserve evidence while requiring zero old IDs in current surfaces. |
| Root copy loses Git ancestry or leaves two authorities | Medium / High | Preflight exact root set; require `git mv`; inspect rename/tracking evidence and zero forbidden roots before config parse. | Stop and restore the complete captured transaction; never merge roots or continue from copied files. |
| Moved internal links still target current-source `engloop/` | Medium / High | Inventory links before move; rewrite and resolve-check in the same transaction; Stage 31 content-aware link gate. | Roll back or repair links before P1/P10; never keep a compatibility tree to make stale links pass. |
| `.gitignore` hides durable `.engloop/` content | Low / High | Assert every durable target file is tracked and only `.engloop/out/` probes are ignored. | Gate FAIL; fix ignore rules and explicitly add durable files before migration acceptance. |
| Spec Kit drops or rewrites rich frontmatter | Medium / High | P0 minimal preservation fixture covers every field/absence before 13 headers; semantic comparison after package/install. | Stop; land/pin the smallest supported upstream capability and rerun the same fixture; no post-processing fallback. |
| Prompt `tools` silently replaces agent policy | Medium / High | Require exact prompt agent selection and structural absence of `tools`; test official precedence contract. | Installation FAIL; regenerate from corrected source/generator rather than accepting equal-looking duplicate policy. |
| Preview hook is unavailable, disabled, or returns a warning code | Medium / High | Strict acceptance pins hook-enabled VS Code; body runs the same validator; trusted durable operations revalidate; setting/version/assurance mode are recorded. | Mark reduced assurance, reject invalid accepted state through trusted tooling, and never claim hook-equivalent blocking from a body or warning. |
| Editable hook/tool/config weakens entry validation | Low / High | Hook invokes versioned local tool, no editable script/secret; focused workspaces require manual approval for sensitive paths. | Stop acceptance, restore accepted bits/config, and rerun semantic/entry tests. |
| YAML text checks miss nested semantic drift | Medium / High | Pin YamlDotNet 18.1.0; compare canonical maps/scalars, exact policy sets, and ordered handoffs. | Report exact field-path mismatch and fail package/install; do not add regex exceptions. |

## Constitution, Architecture, and Learning Gate — Post-Design

| Gate | Post-design result | Design evidence |
|---|---|---|
| Scope integrity | PASS | Checkpoint writes are limited to EngLoopKit nomenclature/root/current links/planning; implementation and consumers remain future. |
| ARCH001 composition | PASS | Bundle remains thin; extension owns commands; tool is explicit prerequisite/artifact, not hidden bundle install. |
| ARCH002 loop shape | PASS | Command contract validates all required sections for exact 13 files. |
| ARCH003 memory | PASS | Final numbered prefixes stay monotonic; Northstar/cards/index are explicit living exceptions; prior revisions remain in Git. |
| ARCH004 executable agreement | PASS | Target tree and phases couple prose/Core/model/CORD/generated/direct tests/package. |
| ARCH005 component boundary | PASS | Generic state/document mechanics in components; PM/stage/readiness policy in Core/tool; model independent. |
| PM001 readiness | PASS | COV003 computed from exact full inventory; zero/missing/stale rows fail. |
| PM002 method by class | PASS | Module descriptor and row rules choose direct vs behavior evidence without lowering 95% bar. |
| PM003 behavior granularity | PASS | One rich real-SUT vertical model, not one model per assembly. |
| PM004 rejection/richness | PASS | P5/P6 require interacting state, branches, and generated guard-derived illegal/invalid rejection. |
| Independent lanes | PASS | Engineering-loop contract requires demand/capacity; Stage 08 emits no work. |
| Fail closed | PASS | Config/evidence/package/migration contracts name missing/ambiguous/stale failure; no fallback/alias. |
| Northstar migration | PASS | Existing Git roots move/evolve with ancestry and no duplicate direction file; the root that starts without Git gets explicit reviewed provenance/backup plus a new tracked boundary without false prior ancestry. |
| Consumer isolation | PASS | Local config/tool/install/focused workspace; mega-workspace only integration view. |
| Hidden process root | PASS | Target tree, config discovery, migration transaction, rollback, and quickstart require sole tracked `.engloop/`; exact config/output; visible entry points; zero old/forbidden roots. |
| Guided agents and handoffs | PASS | Official supported header fields, semantic preservation canary, least-privilege policy, 23 review-first edges, prompt precedence protection, and no automatic lane scheduling. |
| Hook reliability/security | PASS | Versioned `SessionStart` supplies strict Preview blocking; body behavior is tested honestly; trusted durable operations remain authoritative in both modes. |

No justified violation remains. Design is ready to decompose into `tasks.md` in a later
workflow.

## Requirement-to-Plan Traceability

### Functional requirements

| Requirement(s) | Planned ownership | Objective proof |
|---|---|---|
| FR-CMD-001, FR-CMD-006 | P4/P9; command contract; ARCH006 | IDs/product remain `engloopkit`; bundle has no command logic; one extension owns 13. |
| FR-CMD-002, FR-CMD-003, FR-CMD-004 | P4/P9/P10; command contract | Exact ordered set in source/archive/registry/agents/prompts; zero current old IDs. |
| FR-CMD-005 | P4/P8 | 13-way loop-shape theory/validator over frontmatter and required sections. |
| FR-CMD-007, FR-CMD-008 | P4–P9 | Cross-artifact vocabulary tests; missing/ambiguous/stale inputs return non-zero/actionable rejection. |
| FR-CMD-009 | P0/P1/P4/P10; root/config contracts | Exact tracked `.engloop/` and `.engloop/config.json`; ignored `.engloop/out/`; zero current `engloop/`/`.engloopkit/`; dual-root fixture fails before config parsing with no fallback. |
| FR-AGT-001, FR-AGT-002 | P0/P4/P9 | Canary then all 13 source/installed semantic projections prove explicit names, descriptions, hints, VS Code targets, visibility, and model-invocation protection. |
| FR-AGT-003, FR-AGT-013 | P0/P4/P9 | Exact per-stage tool/subagent sets, `agent` iff `Explore`, all targets resolved, focused context, and nesting disabled. |
| FR-AGT-004 | P0/P4/P9 | All 13 prompts select the matching exact agent and structurally omit `tools`; official precedence cannot widen policy. |
| FR-AGT-005, FR-AGT-006 | P0/P4/P9 | Stage 31 alone omits handoffs; installed ordered edge set equals all 23 exact target/label/prompt/`send: false` rows with no model. |
| FR-AGT-007 | P4/P5/P9 | UI and model tests prove no 08→20/30/31 edge, Stage 21 capacity wording, conditional natural ends, and zero handoff state mutation/scheduling. |
| FR-AGT-008, FR-AGT-009 | P4/P9/P10 | Exact stage-expanded 30-second `SessionStart`, unconditional body check, and trusted durable gate; versioned local tool, validated input, no secret/editable script, cross-platform strict/reduced-assurance evidence. |
| FR-AGT-010 | P0/P4/P9 | Semantic absence assertions find zero `infer`, agent `model`, or handoff `model`. |
| FR-AGT-011 | P0 | Spec Kit preservation fixture either passes unchanged semantics or blocks production headers pending the smallest supported upstream capability. |
| FR-AGT-012 | P0/P9/P10 | Disposable and consumer acceptance parse source/generated YAML, resolve targets, inspect picker/diagnostics, and retain exact mismatch evidence. |
| FR-NS-001, FR-NS-002, FR-NS-003 | P1/P10 | Singleton/content validator and create/evolve scenarios. |
| FR-NS-004, FR-NS-005 | P1/P4/P10 | Git rename ancestry; zero live numbered-direction file/template/counter/prefix/command. |
| FR-NS-006, FR-NS-007 | P3/P5/P8 | Transition/model tests reject routine churn and ambiguous/multiple direction. |
| FR-DEL-001, FR-DEL-002 | P2 | Thin real-boundary slice and explicit framework/config evidence. |
| FR-DEL-003, FR-DEL-004, FR-DEL-005 | P2; evidence contract | Same-command build/discovery/pass/fail/re-pass plus boundary identity/destination. |
| FR-DEL-006 | P2 | Failure-path fixtures prove no framework/command/project fallback; fresh proof required after change. |
| FR-DEL-007 | P3 | ARCH006 plus architecture review and component/vertical classification. |
| FR-DEL-008, FR-DEL-009, FR-DEL-010 | P4 | Governed SPEC plan/tasks/implementation evidence, architecture check, scaffold compromise disposition. |
| FR-VER-001, FR-VER-002 | P5–P7 | Rich behavior-level model and generated real-SUT branch/guard evidence. |
| FR-VER-003 | P3/P8 | ARCH component classification; no component MODEL/CORD; direct tests only after disposition. |
| FR-VER-004, FR-VER-005 | P6 | Pinned SEK generation to proven destination with legal/illegal/invalid generated tests. |
| FR-VER-006, FR-VER-007 | P5/P6/P8 | Adequacy validator rejects always-enabled hand assertion and flat tour. |
| FR-VER-008, FR-VER-009 | P7 | Generated-only project/report against real SUT; COV002 contains no readiness/direct test data. |
| FR-VER-010, FR-VER-011 | P8 | 100% disposition report before new direct-test acceptance; non-reachability starts classification. |
| FR-VER-012, FR-VER-013 | P8 | Authority-linked intended return vs no-authority/no-entry residue deletion. |
| FR-VER-014 | P8 | One green build/architecture/Stage07 stamp after every coherent deletion set. |
| FR-VER-015 | P8 | Git/evidence ordering check proves direct test additions follow complete disposition. |
| FR-VER-016, FR-VER-017 | P8 | Exact discovered module set and per-row measured line/branch ≥95.00. |
| FR-VER-018, FR-VER-019 | P8 | Deterministic COV003 sole gate; any absent/stale/red/below row yields NOT READY. |
| FR-VER-020 | P5–P8 | Generated suite itself proves positive/negative/branch/real-SUT/transition rules; direct tests supplemental. |
| FR-TRN-001 | P4–P8; engineering-loop contract | Generated positive coverage of normal and feedback routes plus direct finite table checks. |
| FR-TRN-002, FR-TRN-003 | P5–P8 | Current-readiness operation guard and open repair obligation through target/PASS. |
| FR-TRN-004, FR-TRN-005 | P5/P6 | Generated Stage30 branches and independent pending Stage31 obligation. |
| FR-TRN-006 | P5–P8 | Generated/model-derived ordering/input/bypass/stale/duplicate rejections with reason codes. |
| FR-TRN-007 | P4/P5 | Rich Core/model state equality tests; stage-only model rejected. |
| FR-TRN-008 | P4–P8 | No scheduler edges/effects from 08; demand/capacity generated scenarios. |
| FR-OPS-001, FR-OPS-002 | P4–P8 | Incident command/template/model distinguishes MIT; timeline and recovery gate tests. |
| FR-OPS-003 | P4/P6/P9 | Selected-set guard; PM template/source IDs/RPI/pending-learning behavior. |
| FR-OPS-004, FR-OPS-005 | P4–P8/P10 | Stage22 full route; closure requires source/release/target/verification/current PASS. |
| FR-OPS-006 | P5/P6 | Generated absent-demand rejection for 20/21/22 and no placeholder effects. |
| FR-EVO-001, FR-EVO-002 | P4/P6 | Ordered first-fired branch, one REFACT including no-work, zero numbered direction snapshots. |
| FR-EVO-003, FR-EVO-004 | P5/P6 | Generated no-work/04 and evidence-backed direction→01 paths. |
| FR-EVO-005 | P5/P6 | Independent capacity guards/scenarios for 30 and 31; no automatic pair. |
| FR-LRN-001, FR-LRN-002 | P9; learning contract | PM Learnings-section extraction and immutable `PMxxx/LEARNxxx` IDs. |
| FR-LRN-003, FR-LRN-004 | P9 | Card schema/path and many-to-many graph fixtures. |
| FR-LRN-005, FR-LRN-006 | P9 | 100% source/card degree and required explicit tension state. |
| FR-LRN-007, FR-LRN-008 | P9 | Exactly-one card index target and content-aware page/card/source link resolution. |
| FR-LRN-009 | P9 | Cross-platform deterministic word/nonblank counts both within limits. |
| FR-LRN-010, FR-LRN-011 | P9 | Exact instruction path/keywords/progressive body; no broad applyTo/picker entry. |
| FR-LRN-012 | P9 | Clean cases cover every card and PM; exact expected IDs. |
| FR-LRN-013 | P9 | Every static/retrieval defect exits non-zero and keeps refresh pending. |
| FR-MIG-001 | P1/P10 | Four root-specific end-state reports. |
| FR-NOM-001 | Checkpoint/P4/P9/P10 | Exact 9 global + 3 local prefix sets, final active filenames, living root documents, and zero allocation/alias/fallback outside the set. |
| FR-MIG-002, FR-MIG-003 | P1/P10 | EngLoopKit/TTHP `git mv engloop .engloop`, initial-direction→Northstar ancestry, exact hidden config/output, moved links, and no old/forbidden root or legacy direction machinery. |
| FR-MIG-004 | P10 | Workshop history-preserving root rename; verification direct target-root creation under backup plus explicit source-control bootstrap; each has tracked `.engloop/`, reviewed visible Northstar/Learnings, exact config, and no old/forbidden root or legacy direction machinery. |
| FR-MIG-005, FR-MIG-006 | P10; migration contract | Remove→absence→exact install; 13 v2/0 old in every current surface. |
| FR-MIG-007 | P4–P11 | Atomic major-release coherence gate over all named product artifacts. |
| FR-MIG-008, FR-MIG-009 | P10 | One-folder workspace files and direct folder-open acceptance. |
| FR-MIG-010 | P11 | Mega-workspace retained/documented; expected independent duplicates; no coupling. |

### Success criteria

| Criterion | Phase/gate | Measured result |
|---|---|---|
| SC-001 | P4/P9 | Source/archive inspection: 13 exact current registrations, 0 old/aliases. |
| SC-002 | P10 | Clean one-root generated/picker order: 13 once, 0 inversions, 0 old. |
| SC-003 | P4/P8 | 13/13 command-loop conformance and referenced artifact existence. |
| SC-004 | P1/P10 | Each root: Northstar=1, live numbered-direction machinery=0, Git ancestry where Git exists. |
| SC-005 | P2/P10 | Five runway observations + one command + one destination; no failure source remains. |
| SC-006 | P6/P7 | Generated legal, illegal-order, invalid-input real-SUT tests and distinct branches. |
| SC-007 | P7/P8 | Generated-only reachability; 100% reviewed unreached paths before new direct tests. |
| SC-008 | P8 | Every deletion set has later green architecture + complete Stage07 evidence. |
| SC-009 | P8 | 100% surviving modules represented; every row correct method and ≥95/≥95 PASS. |
| SC-010 | P5–P8 | All declared legal transition classes plus representative generated rejection classes. |
| SC-011 | P10 | Three consumers pass folder/focused standalone, exact 13/0. |
| SC-012 | P11 | Mega-workspace integration use and focused-workspace documentation verified. |
| SC-013 | P9 | 100% source/card/index/provenance/links, both budgets, 100% retrieval cases. |
| SC-014 | P6–P10 | Independent fixture suites for 01–08, 20–22, and 30–31 pass without unrelated lane. |
| SC-015 | P4/P10 | Clean evaluators derive next normal stage from order and locate all loop sections. |
| SC-016 | P1/P4/P10 | Each of four roots reports process-root set `{.engloop}`, source-control-tracked root count 1, config count 1, ignored output, current `engloop/` count 0, `.engloopkit/` count 0, and visible Northstar/Learnings count 1 each. |
| SC-017 | Checkpoint/P4/P9/P10 | Case-sensitive current-surface scan returns only the exact FR-NOM-001 vocabulary and living docs; no alias, translation, fallback, or active old-prefixed filename. |
| SC-018 | P0/P4/P9/P10 | Disposable/focused install: 13/13 visible agents preserve every common field; Stage 31 has zero handoffs, all others have at least one, and EngLoop-owned diagnostic errors/warnings are both zero. |
| SC-019 | P4/P9/P10 | Installed ordered graph equals all 23 ratified edges; 23/23 targets resolve, 23/23 use `send: false`, zero model overrides, and Stage 08 has zero operations/stewardship edges. |
| SC-020 | P4/P9/P10 | All 13 justified tool/subagent rows and hooks match; 13 prompts have zero tool overrides; strict hook blocking, reduced-assurance body rejection, and trusted durable-gate rejection all pass. |

## Plan Completion Gate

- Phase 0 is task-owned and begins with the explicit blocking preservation/schema
  canary; no production header task may start until it passes.
- Planning data model, contracts, and quickstart are complete.
- Pre/post architecture and learning gates pass without exception.
- Exact target structure, file impacts, dependency order, migration sequence, rollback,
  release, testing, complexity, risks, FR traceability, and SC traceability are present.
- Requirement extraction from authoritative `spec.md` yields **102 distinct functional
  requirements** (CMD 9, AGT 13, NS 7, DEL 10, VER 20, TRN 8, OPS 6, EVO 5, LEARN 13,
  NOM 1, MIG 10), and this trace table references all 102 with no missing/extra FR ID.
- Success-criterion extraction yields **20 distinct criteria**, and this trace table
  references SC-001 through SC-020 with no missing/extra SC ID.
- Normative target config/artifact/output paths use `.engloop/`; `.engloopkit/` and
  current `engloop/` appear only as forbidden/removal or explicitly labeled
  current-source/history references.
- No `tasks.md` or consumer change was created; checkpoint code changes are limited to
  the exact prefix set and its tests.
- `.specify/` remains local untracked cache state and is not a deliverable.

**Disposition:** AMENDED — READY FOR TASKS WITH BLOCKING P0 CANARY. The hidden-root,
atomic nomenclature, and custom-agent UX decisions are fully absorbed; task execution
must stop at P0 if the supported Spec Kit/VS Code projection does not pass exactly.
