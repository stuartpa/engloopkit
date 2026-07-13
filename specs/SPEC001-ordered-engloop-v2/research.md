# SPEC001 Research: Ordered EngLoop v2

- **Status:** COMPLETE — AMENDED FOR CUSTOM-AGENT UX AND HIDDEN ROOT
- **Date:** 2026-07-10
- **Feature:** [SPEC001](spec.md)
- **Primary evolution decision:** [REFACT001](../../.engloop/refactors/REFACT001_ordered-engloop-v2.md)
- **Unresolved clarifications:** None

## Ratified hidden-root amendment

SPEC001 and REFACT001 bind v2 to one tracked hidden `.engloop/` root in EngLoopKit
and every consumer. Required configuration is `.engloop/config.json`; transient output
is ignored under `.engloop/out/`; root `NORTHSTAR.md` and root `LEARNINGS.md` remain
visible. V2 has no `.engloopkit/` directory, no live `engloop/` compatibility tree,
and no discovery fallback among those names. A dual-root or forbidden-root state fails
before config is read or migration mutates files.

The atomic nomenclature checkpoint has now moved EngLoopKit's tracked process tree to
`.engloop/`, moved/evolved its initial direction content into root `NORTHSTAR.md`, and
updated current links. Consumer root migrations remain future implementation work.

## Method and authoritative evidence

This research resolves implementation choices without reopening SPEC001's ratified policy. It used the progressive Learnings Pyramid path required by
`.github/instructions/project-learnings.instructions.md`:

1. root [`LEARNINGS.md`](../../LEARNINGS.md);
2. the four relevant living subject cards;
3. their cited `PMxxx/LEARNxxx` source sections in PM001–PM004.

The following were also inspected in full:

- ARCH001–ARCH005;
- the v1.6.0 extension manifest, all nine commands, and all eight templates;
- `src/EngLoopKit.Core`, both generic components, the independent SEK model, CORD,
  `.specexplorerkit/config.json`, generated tests, and direct tests;
- bundle, catalog, CI, changelog, product docs, example, and the local SEK-consumer skill;
- the local SEK source at commit
  `2bf8d3dc7993d9bd93fc167108f5f7de3c8d2196`;
- the current read-only install structures of `tthp`, `engloop-workshop`, and
  `VerifyExtremeEdgeWithTpcc`.

The custom-agent amendment also uses the current official VS Code documentation:

- [Custom agents](https://code.visualstudio.com/docs/agent-customization/custom-agents)
  for supported frontmatter and review-first handoffs;
- [Prompt files](https://code.visualstudio.com/docs/agent-customization/prompt-files)
  for the tool-priority rule;
- [Subagents](https://code.visualstudio.com/docs/agents/subagents) for the `agent`
  tool, explicit allowlists, and default-disabled nesting;
- [Agent hooks](https://code.visualstudio.com/docs/agent-customization/hooks) and
  the [hooks reference](https://code.visualstudio.com/docs/agents/reference/hooks-reference)
  for Preview enablement, `SessionStart`, command shape, timeout, and blocking exit
  behavior;
- [Review AI-generated edits](https://code.visualstudio.com/docs/chat/review-code-edits#_edit-sensitive-files)
  for path-scoped edit approval rather than broad auto-approval.

The repository-local `.specify/` directory was inspected only for hook/setup presence.
It contains cache state only: no `extensions.yml`, setup-plan script, plan template, or
constitution/update-agent-context script exists there. Per SPEC001's planning boundary,
it remains untouched. The
standard plan structure was read from a current Spec Kit 0.12.4 installation in a
read-only consumer and applied manually inside this feature directory.

## Verified baseline

| Area | Verified current state |
|---|---|
| Branch / source | `master`; SPEC001 and REFACT001 derive from EngLoopKit HEAD `9e2618dd9b2c50e878736b19e0385ea92f416c95`; this nomenclature checkpoint is intentionally uncommitted. |
| Product version | Bundle, extension, and catalog are `1.6.0`. |
| Command surface | Nine `speckit.engloopkit.*` commands; no v2 command exists. |
| Direction memory | EngLoopKit has one root `NORTHSTAR.md`, moved/evolved from its tracked initial direction content. |
| Root/config layout | EngLoopKit has the tracked hidden `.engloop/` tree and no `.engloop/config.json` yet; config remains planned implementation work. |
| Executable loop | Eleven stage labels in a graph-only `EngineeringLoop`; state is effectively current stage plus started/not-started. |
| Independent model | One `Current` property; legal-only exploration; 12 states and 15 positive transitions in the committed record. |
| Generated tests | One stale positive covering tour in `tests/EngLoopKit.Loop.Generated`; its generated source contains an absolute workstation binding path and an environment-variable alternate. |
| Direct tests | xUnit tests carry duplicate-start and illegal-transition rejection that the committed generated suite does not prove. |
| Learnings Pyramid | Four cards cover the 11 accepted learnings in PM001–PM004; coverage and page size are manually stated, not deterministically validated. |
| Consumer installs | Each consumer registry pins local EngLoopKit 1.6.0 with nine old IDs and has nine old generated agents plus nine old generated prompts. |
| Consumer direction | TTHP retains its v1 initial-direction layout; workshop and verification consumer have no Northstar; consumer migration is intentionally unchanged in this checkpoint. |
| Consumer source control | TTHP and workshop track generated install surfaces. `VerifyExtremeEdgeWithTpcc` is not a Git repository. |
| Current custom-agent evidence | Existing install evidence proves the legacy `description` projection and exact prompt `agent` selection; a prior focused probe proves handoff projection. It does not prove preservation of the complete required header, nested hook values, absence constraints, exact tool/subagent policies, or all 13 installed surfaces. |

## Verified technology context

| Technology | Repository or machine evidence | Planning decision |
|---|---|---|
| .NET target | Every EngLoopKit project targets `net8.0`; CI requests `8.0.x`. | Remain on .NET 8 and add an explicit SDK pin before implementation. The installed compatible SDK selected for the pin is 8.0.422. |
| C# | `Directory.Build.props` sets `LangVersion=latest`. | Retain current language policy; no language migration. |
| xUnit | Projects pin xUnit 2.9.2, runner 2.8.2, and Microsoft.NET.Test.Sdk 17.11.1. | Retain these versions for the initial v2 cut unless the Stage 02 runway explicitly selects and re-proves a change. |
| Coverage | No coverage tool is installed or declared. The local NuGet cache contains `coverlet.collector` 6.0.2. | Pin `coverlet.collector` 6.0.2 and prove it in Stage 02; consume Cobertura output through deterministic EngLoopKit validation. No unpinned global tool. |
| Spec Kit | Installed CLI is 0.12.4; manifests require `>=0.12.0`. | Test/package against 0.12.4 and retain the public minimum only if an install fixture proves it; otherwise raise the minimum explicitly. |
| VS Code custom agents | Official documentation supports `description`, `name`, `argument-hint`, `tools`, `agents`, `model`, `user-invocable`, `disable-model-invocation`, `target`, `handoffs`, and Preview `hooks`. Handoffs carry `label`, `agent`, optional `prompt`, optional `send` (default false), and optional `model`. The release machine has VS Code Insiders 1.129.0-insider, commit `29d19ddd1af725baf537b6b328843bcdc2d29ba1`, built 2026-07-09. | Bind all 13 agents to `target: vscode`, visible/protected invocation, exact tools/subagents, review-first handoffs, no `model`, and no deprecated `infer`. Pin this build plus a tracked schema projection for 1.7.0 acceptance; another build must pass the identical canary before support. Treat handoffs as UI navigation only. |
| VS Code hooks | Agent-scoped hooks are Preview, require `chat.useCustomAgentHooks`, run with VS Code's permissions, and can be edited if repository files are not protected. `SessionStart` runs on the first submitted prompt; exit code 2 blocks, while other nonzero codes are warnings. A prompt body is instruction, not a platform pre-tool interceptor. | Use the exact versioned local-tool validator and return 2 on rejected hook entry. Enable Preview in focused workspaces for strict pre-action enforcement. Run the same body check unconditionally, label hook-disabled use reduced-assurance, and independently gate every accepted durable transition/evidence operation in trusted tooling. Require manual approval for agent/tool/config surfaces. |
| YAML semantic validation | Required frontmatter now contains nested mappings and sequences. YamlDotNet 18.1.0 is MIT-licensed and explicitly targets `net8.0`. | Pin YamlDotNet 18.1.0 in the domain-free document-validation component and compare parsed semantic projections; do not use line regexes or a handwritten partial YAML parser for release acceptance. |
| SEK | No global tool is installed. Local source declares CLI package 0.1.0, while ARCH004 and the skill describe 0.1.1. Current source HEAD includes model-derived negative edges. | Pin an authoritative SEK Git revision in CI and developer setup. Do not infer a version from conflicting labels. Regeneration uses the pinned source command until a matching release is published. |
| Git | Installed Git is 2.55.0.windows.2. | Git remains Northstar/card history. Migration uses explicit moves/deletions; no duplicate current snapshots. |
| Companion extensions | Bundle pins architecture-guard 1.11.0 and tinyspec 1.0.0. | Preserve ARCH001 composition. Tinyspec remains an independently composed capability but is not a Stage 22 route, compatibility path, or fallback. |

## Decisions

### Decision 1 — One atomic 13-command cutover

**Decision:** Replace the nine v1 command files and manifest registrations atomically
with the exact 13 SPEC001 IDs. Keep bundle/extension/product/package identity
`engloopkit`; ship no old alias, redirect, translation, or dual registration.

**Rationale:** This is the only design that satisfies FR-CMD-001–004 and makes lexical
order teach the process. A mixed manifest would make stale output indistinguishable
from compatibility behavior.

**Alternatives considered:**

- Keep v1 aliases for transition: rejected by SPEC001 and picker-noise goals.
- Rename files while retaining old registration IDs: rejected because installed
  registries and generated prompts derive from IDs.
- Migrate consumers incrementally before package conformance: rejected because it can
  strand a consumer with a mixed surface.

### Decision 2 — Model three invocation lanes, not a 13-step conveyor

**Decision:** The executable state records delivery/readiness, operations demand, and
stewardship obligations independently. Stage 08 PASS updates authorization evidence
only; it emits no Stage 20, 30, or 31 work. Stage 20 requires an actual incident;
Stage 21 a selected stabilized set; Stage 22 repair items; Stages 30/31 explicit spare
capacity and their own demand.

**Rationale:** This directly implements FR-TRN-008, FR-OPS-006, and FR-EVO-005. A single
`CurrentStage` cannot preserve readiness currency, repair closure, learning refresh,
or return context.

**Alternatives considered:**

- Preserve a graph keyed only by stage: rejected as behaviorally too thin.
- Add edges from 08 to 20/30/31: rejected because an allowed transition is not a
  scheduler and would reproduce the conveyor interpretation.
- Encode month-end as an automatic timer: rejected; spare capacity is explicit input,
  and month-end is guidance rather than demand.

### Decision 3 — Use one hidden root and explicit local configuration

**Decision:** V2 has exactly one tracked process root, `.engloop/`. Add the required
configuration at `.engloop/config.json`; store durable numbered memory, learning cards,
and retrieval cases below that same root; and write only transient ignored reports to
`.engloop/out/`. The configuration explicitly identifies the fixed artifact root,
Northstar, test-runway status and command, generated-test destination, module
inventory/discovery command, architecture command, coverage reports, and validator
command. Discovery examines only the selected repository root and exact config path.
If `engloop/`, `.engloopkit/`, both old and new roots, or contradictory config is
present, validation fails before acting. There is no compatibility tree or lookup
fallback to another root, `docs/`, a sibling repository, an old destination, or a
guessed test framework.

**Rationale:** Current commands infer a default artifact root, while SPEC001 requires
standalone roots and fail-closed evidence. One physical root removes split authority
between process memory, config, and output. An explicit generic contract also respects
the systems/component boundary: consumer-specific meaning stays in consumer config.

**Alternatives considered:**

- Continue parsing prose in `standards.md`: rejected as ambiguous and not a stable
  machine contract.
- Keep config/output in `.engloopkit/` beside durable `engloop/` memory: rejected as
  two process authorities and directly superseded by the ratified hidden-root choice.
- Retain `engloop/` as a compatibility tree or search it after `.engloop/` fails:
  rejected because dual authority and fallback hide incomplete migration.
- Discover a test project or module by name/shape: rejected as a workload heuristic.
- Let commands search parent/sibling roots: rejected as cross-root coupling.

### Decision 4 — Migrate initial direction history into one living Northstar

**Decision:** In each Git repository, first rename the complete current tracked
`engloop/` tree to `.engloop/` with `git mv`; then move the useful initial seed from
its new `.engloop/seeds/` location to root `NORTHSTAR.md` and evolve that same file to
the SPEC001 content contract. Create config directly at `.engloop/config.json`, update
all moved-tree links in the same transaction, and delete the duplicate live direction
path plus legacy command/template/prefix/counter rows and empty directory requirement. Do not
create a copied archival seed or old-root forwarding tree. In a non-Git consumer,
create `.engloop/` and one reviewed Northstar directly from its own product evidence
inside an explicit checksummed filesystem backup boundary, then establish explicit
source-control tracking before acceptance. This creates future tracking but does not
invent pre-migration Git ancestry.

**Rationale:** Git provides rename history for the existing Git roots. A duplicate seed
would create two current authorities. The verification consumer cannot claim history
that predates its tracking boundary, so its migration records provenance and a backup
checksum, establishes tracking explicitly, and makes no false ancestry claim.

**Alternatives considered:**

- Copy seed content and retain the old file: rejected as duplicate live direction.
- Copy the visible artifact tree into `.engloop/` and leave `engloop/`: rejected
  because it loses rename ancestry and produces a forbidden dual-root state.
- Convert every historical seed to a Northstar: rejected; only current direction is
  migrated, while Git history remains historical evidence.
- Keep a dormant legacy direction counter: rejected because SPEC001 removes the artifact class.

### Decision 5 — Stage 02 proves one stable runway, including controlled failure

**Decision:** For EngLoopKit, retain xUnit and use the one terse command recorded by
Stage 02. A proof script temporarily adds a uniquely named failing test to the selected
project, runs the same command, requires the expected test and non-zero exit, removes
the temporary source in a guaranteed cleanup block, and reruns the same command green.
The evidence record captures build, discovery, pass, intentional fail, restored pass,
framework versions, command, and destination.

The stable generated destination is
`tests/EngLoopKit.Loop.Generated/`. It is generated-only and committed. Generation must
produce a portable project with exactly one binding resolution path, preferably a
project reference/copy-to-output contract. Absolute workstation paths plus
`SEK_BINDING ?? default` are forbidden.

**Rationale:** This proves that failure can be observed, not merely that green tests can
run. A fixed destination lets Stage 06 replace generated output deterministically.

**Alternatives considered:**

- Keep the existing absolute path with an environment override: rejected as a hidden
  alternate path and not standalone.
- Hand-edit generated tests after each run: rejected; generated output is replaced by
  the generator.
- Change framework when a proof step fails: rejected unless a new explicit decision
  repeats every proof step.

### Decision 6 — Pin a portable SEK generator capability as a prerequisite

**Decision:** Pin a SEK revision that supports stateful per-path SUT instances,
model-derived negative edges, `RequireBound`, and a single portable binding contract.
Current HEAD proves the first three but not the last. If the portable output contract
is not available, land and verify that generic SEK capability before generating SPEC001
v2 tests; EngLoopKit does not post-process or silently patch generated output.

**Rationale:** SPEC001 explicitly allows SEK to “provide or gain” required capability.
Failing at the dependency boundary is safer than accepting stale or workstation-bound
tests.

**Alternatives considered:**

- Rely on whichever sibling SEK checkout happens to exist: rejected as unpinned.
- Install the conflicting documented 0.1.1 label blindly: rejected because local
  package metadata and source behavior do not establish equivalence.
- Replace model-derived negatives with hand-written xUnit cases: rejected by PM004.

### Decision 7 — Rich state stays in the vertical; generic machinery stays in components

**Decision:** EngLoop-specific stages, evidence rules, lanes, repair/learning
obligations, and readiness semantics live in `src/EngLoopKit.Core`. Generic graph/guard
machinery remains in `EngLoopKit.Components.StateMachine`. Add a domain-free
`EngLoopKit.Components.DocumentValidation` component for Markdown link extraction,
text budgets, set coverage, and validation reports. A thin `EngLoopKit.Tool` vertical
provides deterministic config, command, learnings, installation, and readiness checks.

**Rationale:** This satisfies ARCH005 while making Stage 31 and Stage 08 machine gates
usable in standalone repositories. Spec Kit 0.12.4 can declare required tools but does
not install arbitrary tools as bundle components, so the tool is a separately verified
`engloopkit` 1.7.0 release prerequisite/local tool entry.

**Alternatives considered:**

- Put PM/LEARN semantics into the generic component: rejected as domain leakage.
- Implement validation only in xUnit: rejected because consumers need a runnable gate.
- Assume a binary embedded in the extension is installed by Spec Kit: rejected; the
  installed bundler supports extensions/presets/steps/workflows, not arbitrary tool
  installation.

### Decision 8 — Separate Stage 07 functional reachability from Stage 08 readiness

**Decision:** Stage 07 runs only the freshly generated project against the real
stateful SUT and collects a dedicated coverage report. Stage 08 consumes that report,
classifies every unreached path, returns intended gaps through 05–07, deletes only
unsupported/no-entry residue, and reruns build + architecture + Stage 07 after each
coherent deletion set. Only after full disposition may direct tests be added and a
separate whole-product coverage run be used for the final inventory.

**Rationale:** This preserves PM001–PM004 and prevents unit tests from manufacturing
functional reachability or reasons to retain residue.

**Alternatives considered:**

- Merge all tests into one coverage run: rejected because origin of reachability is
  lost.
- Delete every unreached line: rejected; reflection/configuration/recovery paths can be
  authoritative.
- Allow a rationale for below-95% modules: rejected by SPEC001's ratified threshold.

### Decision 9 — Deterministically validate the Learnings Pyramid, then test retrieval

**Decision:** The tool derives accepted source IDs from PM Learnings sections; validates
source↔card, card↔index, exact links/content, required card sections, conflicts, and both
page budgets; and exits non-zero on any defect. Clean-context retrieval uses a committed
case manifest whose expected card/source IDs cover every card and at least one source
from every PM. A fresh isolated agent run produces results, and the deterministic tool
compares exact expected versus actual IDs, including false provenance.

**Rationale:** Static graph completeness and clean-context usability are different
claims and both are required. The instruction remains on-demand and has no broad
`applyTo`.

**Alternatives considered:**

- Trust the current manual “11 of 11” statement: rejected as stale-able narration.
- Put full learning content in the instruction: rejected as context-heavy duplication.
- Accept retrieval that names a right card plus a false source: rejected as false
  provenance.

### Decision 10 — Release and migrate coherently on the 1.x maturity runway

**Decision:** Ship version 1.7.0. “Ordered EngLoop v2” is an internal workflow-
generation label, not a package-version instruction. EngLoopKit remains on 1.x until
the maintainer explicitly authorizes 2.x; compatibility analysis alone cannot make
that decision. Build immutable extension, bundle, and tool artifacts;
validate their contents and hashes; then migrate one consumer root at a time through
capture → validate unambiguous roots → atomically rename/create `.engloop/` and create
its config → prove forbidden-root absence → remove v1 install → prove absence → install
exact artifact → prove exact surface → standalone acceptance. TTHP and workshop commit
the root rename and generated replacements coherently. The verification consumer uses
a checksummed backup/restore boundary because it starts without Git, creates the target
root directly, and uses an explicit caller-selected source-control bootstrap so the
accepted `.engloop/` is tracked. Focused one-root `.code-workspace` files are added;
the existing mega-workspace remains an integration view and may display one independent
registration per root.

**Rationale:** This avoids mixed generated surfaces and gives each root an observable
rollback boundary. The release notes and clean-install contract communicate removal of
the prior command IDs; the maintainer-governed 1.x maturity policy deliberately reserves
2.x for a later explicit maturity decision rather than deriving it automatically.

**Alternatives considered:**

- In-place `--force` overwrite: rejected because stale files can survive.
- Cross-root shared installation: rejected because opening a consumer alone would fail.
- Suppress duplicates in the mega-workspace by coupling roots: rejected by FR-MIG-010.
- Copy/migrate roots opportunistically while continuing after a dual-root finding:
  rejected because partial root authority has no safe fallback semantics.

### Decision 11 — Bind the official custom-agent and handoff semantics exactly

**Decision:** Every source command and generated `.agent.md` uses explicit
`name`, `description`, `argument-hint`, `target`, `user-invocable`,
`disable-model-invocation`, `tools`, `agents`, `hooks`, and applicable `handoffs`.
The name is the exact command ID, the target is `vscode`, the agent is visible, and
model invocation is disabled. `infer` and `model` are absent. Every handoff has the
contracted target, label, and prefilled prompt, with `send: false` and no model
override.

Selecting a handoff button switches to the target agent with existing context and a
prefilled prompt for human review. It does not submit, mutate EngLoop state, satisfy a
gate, or schedule another lane. The target agent reruns authoritative entry validation
when the user submits the reviewed prompt.

**Rationale:** These are the supported VS Code fields and handoff behaviors. Making
visibility, invocation protection, and review explicit avoids relying on defaults;
keeping `send: false` preserves approval at every branch.

**Alternatives considered:**

- Use numeric adjacency to synthesize handoffs: rejected because the ratified branch
  graph is not a conveyor.
- Use `send: true`: rejected because it bypasses review.
- Pin a model or handoff model: rejected because no stage-specific benefit or tested
  availability/fallback order is ratified.

### Decision 12 — Preserve the exact tool/subagent matrix and prompt priority

**Decision:** Use the 13-row tools/agents matrix in SPEC001 without widening. Only the
seven agents that list `Explore` include the `agent` tool; every other agent declares
`agents: []` and omits that tool. Wildcards are forbidden. Delegation passes a focused
subtask/context and does not require `chat.subagents.allowInvocationsFromSubagents`;
nested subagents remain off.

Each generated `.prompt.md` selects the exact matching custom agent and omits `tools`.
This is mandatory because official VS Code precedence makes prompt-file tools override
the referenced custom agent's tools. Installation also resolves the `Explore` agent and
all named tools on the supported VS Code build; unavailable capabilities fail rather
than being silently ignored.

**Rationale:** Explicit allowlists prevent selection of an unintended agent, and prompt
omission preserves the custom agent's least-privilege boundary.

**Alternatives considered:**

- `agents: '*'`: rejected as unbounded delegation.
- Repeat the tool list in prompts: rejected because prompt priority could replace the
  authoritative agent policy and drift independently.
- Enable nested delegation: rejected because no SPEC001 scenario requires it.

### Decision 13 — Treat agent-scoped hooks as Preview defense-in-depth

**Decision:** Every agent carries this exact `SessionStart` command, with its own exact
stage ID and `timeout: 30`:

`dotnet tool run engloopkit validate agent-entry --stage <exact-command-id> --root .`

The tool is restored from the root-local manifest at the accepted EngLoopKit version;
the hook invokes no repository-editable script, accepts no secret, validates all input,
and returns exit code 2 for rejection so VS Code blocks the session. The command body
unconditionally runs the same validator before any stage action. Because body text is
model instruction rather than a platform interceptor, hook-disabled operation is
explicitly reduced-assurance; every trusted command that accepts durable stage state or
evidence independently repeats the gate, making an invalid accepted transition
unrepresentable even if a model disobeys the body.

Focused workspaces set `chat.useCustomAgentHooks: true` and deny automatic edits to
installed agent/prompt files, the local tool manifest, and `.engloop/config.json`.
They do not enable Bypass Approvals, Autopilot, blanket edit approval, or broad terminal
auto-approval. Folder-open operation remains valid with hooks disabled, but reports
reduced assurance; body compliance is observed and trusted durable operations remain
mechanically gated.

**Rationale:** Official hooks are Preview and run commands with VS Code permissions.
Versioned immutable tooling, strict hook-enabled focused workspaces, unconditional body
instructions, and independent durable-operation gates preserve honest enforcement
without pretending a prompt body has platform interception powers.

**Alternatives considered:**

- Hook-only validation: rejected because Preview can be disabled or unavailable.
- Repository script hook: rejected because an editing agent could alter what it later
  executes.
- Global approval changes: rejected as unrelated and unsafe.

### Decision 14 — Run a disposable Spec Kit preservation gate before 13 headers

**Decision:** Immediately after baseline freeze, run a disposable Spec Kit 0.12.4
installation experiment before authoring the 13 production headers. A small fixture
covers every required scalar, sequence, mapping, empty/nonempty `agents` policy,
terminal/nonterminal handoff shape, nested `SessionStart` command, and required absence
of `infer`/`model`. It installs only through Spec Kit's supported path, with no copied
agent, hidden fallback, or post-processing.

The P0 task owns `scripts/test-spec-kit-agent-preservation.ps1`. It creates an ignored
disposable .NET harness below `.engloop/out/spec-kit-agent-canary/`, pins YamlDotNet
18.1.0 locally, parses source commands, installed commands, generated agents, and
generated prompts, and removes a passing fixture while retaining a failing fixture and
report. This harness is research/test infrastructure, not production parser code; P4
implements the governed reusable projection in the document-validation component.

Parse source commands, installed commands, generated agents, and generated prompts;
compare canonical semantic projections; resolve targets; and run customization
diagnostics or the deterministic equivalent. The experiment must prove all required
fields, handoff subfields, exact prompt agent selection, and prompt `tools` absence.

If 0.12.4 drops or rewrites any value, stop. Make the smallest generic upstream Spec
Kit capability change and pin its resulting supported version/revision, or use an
explicit generation mode implemented and documented by upstream Spec Kit itself. Then
rerun the identical experiment. Do not introduce an EngLoopKit-owned alternate
generator, write all 13 headers, post-process a partial installation, copy private
fallback agents, or weaken SPEC001 while the gate is red.

**Rationale:** Existing evidence proves only a subset. A cheap capability spike avoids
duplicating 13 rich headers against an unproven generator and makes FR-AGT-011
mechanical.

**Alternatives considered:**

- Assume unknown fields pass through: rejected as a release-critical guess.
- Write all 13 first and repair generated output: rejected as high-churn and a hidden
  second source of truth.
- Accept description/handoff-only output: rejected by FR-AGT-001–013.

### Decision 15 — Compare YAML semantically and keep UX evidence release-scoped

**Decision:** Pin YamlDotNet 18.1.0 in
`EngLoopKit.Components.DocumentValidation`. Parse frontmatter into a canonical value
tree: mapping order is irrelevant, scalar type/value is exact, tools and subagents are
duplicate-free exact sets, and handoff order plus values are exact because order is a
visible UX choice. Compare required field presence/absence separately from values.

Record source/installed digests, Spec Kit and VS Code versions, semantic mismatches,
target resolution, prompt policy, and customization diagnostics in an agent-surface
validation result. This result gates package/release/install acceptance. It is not
product readiness unless code implementing an inventoried product module is also
covered by that module's normal Stage 08 row; picker/installation evidence by itself
cannot emit READY.

**Rationale:** Text equality rejects harmless YAML formatting while regexes miss nested
semantic loss. Separating release evidence from readiness preserves PM001/LEARN001–003.

**Alternatives considered:**

- Raw text comparison: rejected because serializers may reorder mappings or normalize
  quoting without changing meaning.
- Regex-only checks: rejected because nested hooks/handoffs and absence constraints can
  be misread.
- Count package acceptance as product readiness: rejected as a narrated category error.

## Research exit gate

- All technical-context unknowns have either a verified version or an explicit chosen
  contract.
- No unresolved clarification remains.
- The portable SEK binding capability is a sequenced dependency, not an open policy
  question: absence causes a visible prerequisite failure.
- The hidden-root design has no unresolved choice: `.engloop/config.json` is the sole
  discovery path, `.engloop/out/` is the sole transient output root, and old/dual roots
  are hard failures.
- Official custom-agent, handoff, prompt priority, subagent, and hook semantics are
  resolved into Decisions 11–15.
- Spec Kit frontmatter preservation is a fail-closed prerequisite experiment with a
  complete pass branch and an explicit smallest-upstream-change branch; it is not an
  unresolved product-policy clarification.
- YamlDotNet 18.1.0 is the pinned semantic parser; no partial-parser decision remains.
- PM001–PM004 remain binding and are traced into design and validation.
- Phase 1 design may proceed.
