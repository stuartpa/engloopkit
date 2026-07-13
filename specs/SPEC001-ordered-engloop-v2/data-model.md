# SPEC001 Data Model: Ordered EngLoop v2

- **Status:** DESIGN COMPLETE
- **Feature:** [SPEC001](spec.md)
- **Contracts:** [`contracts/`](contracts/)

This is the conceptual and executable-domain model for the v2 workflow. It does not
prescribe application/workload semantics. Consumer-specific paths and commands enter
through explicit configuration; no object name, repository shape, or sibling layout is
inferred.

## 1. Repository root and configuration

### `RepositoryRootLayout`

The fail-closed physical layout for every governed v2 repository.

| Field | Type | Rules |
|---|---|---|
| `repositoryRoot` | canonical directory | Explicitly selected root; never discovered by walking to a parent or sibling. |
| `processRoot` | root-relative path | Exactly `.engloop`; one directory, tracked as durable repository content. |
| `configPath` | root-relative path | Exactly `.engloop/config.json`; exactly one regular file. |
| `transientOutputRoot` | root-relative path | Exactly `.engloop/out`; ignored and never accepted as durable evidence by existence alone. |
| `northstarPath` | root-relative path | Exactly `NORTHSTAR.md`; root-visible. |
| `learningsIndexPath` | root-relative path | Exactly `LEARNINGS.md`; root-visible. |
| `forbiddenRootFindings` | set | Presence of current `engloop/`, `.engloopkit/`, duplicate/case-ambiguous canonical roots, or an escaping link. Must be empty. |
| `trackingEvidence` | path set/revision | Every durable `.engloop/` file is returned by the selected repository source-control inventory; `.engloop/out/` is excluded. A root with no source-control boundary cannot pass final v2 acceptance. |

**Invariant:** root-layout validation precedes config parsing. Exactly one canonical
`.engloop/` root and one config file must exist, both visible entry-point files must
resolve, and no forbidden old root may exist. A missing, dual, case-ambiguous, or
forbidden root returns a non-success result without selecting, merging, copying, or
falling back to another path.

### `EngLoopConfiguration`

One tracked configuration at the exact path `.engloop/config.json` for a governed
repository.

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | string | Exactly the supported v2 schema. Unknown versions fail closed. |
| `productId` | string | `engloopkit` for this product installation. |
| `artifactRoot` | root-relative path | Required and exactly `.engloop`; no alternate value or default. |
| `transientOutputRoot` | root-relative path | Required and exactly `.engloop/out`; must be ignored. |
| `northstarPath` | root-relative path | Exactly `NORTHSTAR.md`; must resolve to one file. |
| `validatorCommand` | non-empty command vector | Explicit deterministic validator invocation; no PATH guessing or alternate command. |
| `testRunway` | `TestRunwayConfiguration` | Explicitly `unproven`, `proving`, or `proven`. |
| `moduleInventory` | list of `ModuleDescriptor` | Required before Stage 08; IDs and paths unique. |
| `moduleDiscoveryCommand` | command vector | Authoritative command that emits the current module set for completeness comparison. |
| `architectureCommand` | command vector | Required before architecture/readiness checks. |
| `regressionCommand` | command vector | Required before readiness. |
| `coverageInputs` | list of named coverage contracts | Separates generated-functional and whole-product reports. |

**Discovery and validation:** The caller supplies the selected repository root. The
validator checks `RepositoryRootLayout`, opens only `.engloop/config.json`, and requires
the fixed root/output values before resolving any remaining path. It never probes
`engloop/`, `.engloopkit/`, a parent, a sibling, or a cached config as an alternate.
All paths are canonicalized relative to the selected repository root and must remain
within it. Missing commands, duplicate module IDs, unknown classes, stale evidence
references, or a path that escapes the root reject the config. A field marked
`unproven` is an explicit state, not permission to guess.

### `TestRunwayConfiguration`

| Field | Type | Rules |
|---|---|---|
| `status` | enum | `unproven`, `proving`, `proven`; only `proven` enables Stage 06. |
| `framework` | string/null | Required when proven; exact selected framework and version family. |
| `terseCommand` | command vector/null | Required when proven; one command reused for pass/fail/re-pass. |
| `boundaryTest` | test identity/null | Must cross the actual product boundary. |
| `generatedDestination` | root-relative directory/null | Required when proven; stable, unique, and generator-owned. |
| `evidenceDigest` | digest/null | Digest of the current Stage 02 evidence. |
| `provenAtRevision` | Git revision or content revision/null | Must match the relevant configuration/product revision. |

A repository with `status=unproven` may run Stages 01–02. Stages 05–08 reject it with an
actionable missing-runway result.

## 2. Command surface

### `CommandDescriptor`

| Field | Type | Rules |
|---|---|---|
| `ordinal` | integer | One of 01–08, 20–22, 30–31. |
| `id` | string | One exact `speckit.engloop.<NN>-<name>` ID from the command contract. |
| `file` | root-relative path | Exactly one corresponding Markdown command file. |
| `lane` | enum | `delivery`, `operations`, or `stewardship`. |
| `responsibility` | string | Matches SPEC001 ownership; cannot claim another stage's gate. |
| `loopContract` | value object | Frontmatter description plus Trigger, Goal, Actions, Verification, Memory, Artifact root, Done when. |

**Invariants:** Exactly 13 descriptors; IDs and files unique; ordinal sort equals ordinal
lexical ID sort; no current descriptor, registry, generated agent, or generated prompt
contains `speckit.engloopkit.`.

## 2A. Custom-agent UX and generated surfaces

### `AgentDescriptor`

The one authoritative custom-agent projection associated with a
`CommandDescriptor`.

| Field | Type | Rules |
|---|---|---|
| `id` / `name` | exact command ID | Both equal the associated `CommandDescriptor.id`; all 13 are distinct. |
| `sourceCommandPath` | root-relative path | The one authoritative source Markdown command. |
| `installedAgentPath` | root-relative path | Exactly `.github/agents/<id>.agent.md` in a disposable/consumer installation. |
| `generatedPromptPath` | root-relative path | Exactly `.github/prompts/<id>.prompt.md`. |
| `description` | non-empty string | Stage-specific trigger and outcome; preserved semantically. |
| `argumentHint` | non-empty string | Stage-specific required input shown by VS Code. |
| `target` | enum | Exactly `vscode`. |
| `userInvocable` | bool | Exactly true; all 13 are visible. |
| `disableModelInvocation` | bool | Exactly true; another model cannot select a stage implicitly. |
| `toolPolicy` | `ToolPolicy` | Exact row from SPEC001; no missing, extra, or duplicate tool. |
| `subagentPolicy` | `SubagentPolicy` | Explicit `Explore` allowlist or explicit empty list. |
| `entryHook` | `EntryHook` | Exact stage-bound `SessionStart` validator. |
| `handoffs` | ordered list of `HandoffEdge` | Exact outgoing graph order; empty only for Stage 31. |
| `bodyEntryValidation` | command contract | Body unconditionally invokes the same validator before any action; compliance is observed behavior, not a platform interceptor. |
| `durableStageGate` | trusted command contract | Every operation that accepts durable transition/evidence state independently enforces current entry prerequisites. |
| `modelPresent` / `inferPresent` | bool | Both false. Absence is validated, not defaulted. |

Exactly 13 `AgentDescriptor` values exist. Source and installed headers have equivalent
canonical projections for every field above. Generated prompts select `id` through
their exact `agent` field and have no `tools` field. A missing source, generated agent,
generated prompt, or semantic value is not represented as a partial descriptor; it is
a failed installation result.

### `ToolPolicy`

| Field | Type | Rules |
|---|---|---|
| `tools` | duplicate-free exact set | One ratified stage row using only `read`, `search`, `edit`, `execute`, `web`, and `agent`. |
| `resolvedTools` | set | Must equal `tools` on the supported VS Code build; official silent-ignore behavior is not accepted by EngLoopKit validation. |
| `source` | authority reference | SPEC001 least-privilege matrix. |

The set is least privilege and cannot be widened by a prompt. Tool sequence formatting
does not affect semantic equality, but duplicates, omissions, or extras fail.

### `SubagentPolicy`

| Field | Type | Rules |
|---|---|---|
| `allowedAgents` | exact list | Either `[Explore]` for the seven ratified research-capable stages or `[]`. `*` is invalid. |
| `agentToolRequired` | bool | True iff `allowedAgents` is nonempty; must agree with `ToolPolicy`. |
| `nestedSubagentsRequired` | bool | Always false; no self-reference or nested setting dependency. |
| `delegationContract` | text/policy | Pass only the focused subtask/context and return a bounded result. |
| `targetsResolved` | bool | `Explore` must resolve when listed; absence fails installation. |

### `EntryHook`

| Field | Type | Rules |
|---|---|---|
| `event` | enum | Exactly `SessionStart`. |
| `type` | string | Exactly `command`. |
| `command` | string | `dotnet tool run engloopkit validate agent-entry --stage <exact-id> --root .`. |
| `timeoutSeconds` | integer | Exactly 30. |
| `toolVersion` / `toolDigest` | release identity | Resolve through the selected root's local tool manifest to the accepted EngLoopKit release; never a sibling/dev binary. |
| `usesRepositoryEditableScript` | bool | False. |
| `usesSecrets` | bool | False. |
| `rejectionExitCode` | integer | Exactly 2 for hook blocking; body invocation treats any nonzero as rejection. |
| `previewSetting` | string | `chat.useCustomAgentHooks`; enabled only in focused acceptance workspaces. |

The hook supplies strict platform-enforced pre-action blocking when enabled. The body
check always runs as mandatory defense-in-depth behavior, but is not treated as a
platform interceptor. With Preview hooks unavailable or disabled, the session records
reduced assurance; `durableStageGate` remains the authority that prevents invalid
accepted transition/evidence state.

### `HandoffEdge`

| Field | Type | Rules |
|---|---|---|
| `fromAgentId` | exact ID | Existing source/installed agent. |
| `targetAgentId` | exact ID | Existing member of the same 13-agent installed set. |
| `label` | non-empty string | Exact branch-specific button label from the command-surface contract. |
| `prompt` | non-empty string | Exact branch-specific prefilled prompt from the contract. |
| `send` | bool | Always false. |
| `modelPresent` | bool | Always false. |
| `branchMeaning` | enum/text | Corresponding legal branch projection, never numeric adjacency. |

A handoff is a UI suggestion that switches agents with context and a prefilled prompt.
It creates no `TransitionAttempt`, mutates no `EngineeringLoopState`, satisfies no gate,
and schedules no lane. User submission at the target creates a new entry-validation
attempt. Stage 31 has zero outgoing edges; Stage 08 has no edge to 20, 30, or 31.

### `GeneratedSurfaceSemanticComparison`

| Field | Type | Rules |
|---|---|---|
| `specKitVersion` | exact version/revision | 0.12.4 for the initial experiment, or the explicitly pinned upstream replacement after a failed experiment. |
| `sourceDigest` / `installedDigest` | digest sets | Cover all compared source commands, installed commands/agents, and prompts. |
| `requiredFieldProjection` | canonical YAML value tree | Mapping order ignored; scalar types/values exact; policy sets exact; handoff sequence exact. |
| `requiredPresence` | field set | All required fields present; `handoffs` omitted only for Stage 31. |
| `requiredAbsence` | field set | `infer` and `model` absent from agents; `tools` absent from prompts; handoff `model` absent. |
| `mismatches` | list | Field path, source value, generated value, and affected agent ID; empty for PASS. |
| `targetResolution` | exact-set result | All handoff and allowed-subagent targets resolve; no extra target. |
| `promptProjection` | exact-set result | 13 prompts select 13 matching agent IDs and contain no tool override. |
| `verdict` | PASS/FAIL | PASS only when all 13 source/install projections and absence constraints agree. |

The comparison is produced from YamlDotNet 18.1.0 parsing, not line equality or regex
matching. A failed early canary comparison blocks production header authoring and
routes to the smallest supported Spec Kit capability change; a failed full comparison
blocks package/release/consumer migration.

### `CustomizationDiagnosticsResult`

| Field | Type | Rules |
|---|---|---|
| `vscodeVersion` / `target` | identity | Exactly `1.129.0-insider` / commit `29d19ddd1af725baf537b6b328843bcdc2d29ba1` and `vscode` for 1.7.0 acceptance; another build requires the identical canary. |
| `schemaProjectionDigest` | digest | Tracked `schemas/vscode-agent-surface.schema.json` plus official-document provenance retrieved 2026-07-10. |
| `workspaceRoot` | canonical path | Disposable or focused single-root consumer only. |
| `loadedAgentIds` | set | Exactly the expected 13 visible IDs. |
| `errors` / `warnings` | diagnostic lists | Empty for EngLoop-owned agents/prompts/hooks; every finding retains file/field/message. |
| `hookSetting` / `assuranceMode` | bool / enum | Recorded explicitly as strict hook-enabled or reduced-assurance hook-disabled; both fixtures are tested without equating their enforcement. |
| `deterministicParserResult` | comparison digest | References the matching `GeneratedSurfaceSemanticComparison`. |
| `verdict` | PASS/FAIL | PASS only on exact source/archive/install semantic projections, target resolution, and matching deterministic semantics. |

The deterministic parser plus disposable archive installation is the complete
release/install evidence. UI validation is intentionally not performed. This evidence is
not a product-readiness verdict.

## 3. Living direction

### `Northstar`

A singleton living file at repository root.

| Field/section | Rules |
|---|---|
| Purpose and audience | Required and repository-specific. |
| Enduring outcomes | At least one durable outcome. |
| Non-negotiable invariants | Explicit and reviewable. |
| Current direction | One authoritative direction. |
| More of | Explicit desired trend. |
| Less of / stop | Explicit disinvestment or stop signal. |
| Boundaries | Current scope and external boundaries. |
| Unresolved direction questions | Genuine questions only; may state none. |
| Evidence for revision | Required when an existing Northstar changes. |

**Invariants:** Exactly one root file; root `LEARNINGS.md` remains a separate visible
learning entry point; no live numbered-direction file/template/counter/prefix; routine
feature/refactor/repair completion does not mutate the Northstar. Multiple candidates,
uncertain authority, or a forbidden/dual process root reject Stage 01.

## 4. Executable workflow state

### `Stage`

The 13 domain stage values:

`Northstar`, `Scaffold`, `Architect`, `Refactor`, `Model`, `Explore`, `Validate`,
`UnitTest`, `Incident`, `Postmortem`, `Repair`, `RefactorScan`, `LearningsPyramid`.

Numeric command ordinals are presentation/identity data, not enum adjacency or an
automatic scheduler.

### `InvocationLane`

- `Delivery`: Stages 01–08.
- `Operations`: Stages 20–22, entered only by demand.
- `Stewardship`: Stages 30–31, entered independently with explicit capacity and demand.

### `EngineeringLoopState`

| Field | Type | Meaning |
|---|---|---|
| `lastAcceptedStage` | `Stage?` | Audit location only; never sufficient to authorize a transition. |
| `deliveryCursor` | enum | `not-started`, `northstar`, `scaffold`, `architecture`, `refactor`, `model`, `explored`, `validated`, `disposition`, `ready`. |
| `productRevision` | monotonic revision | Changes whenever governed product behavior/configuration changes. |
| `modelRevision` | revision/null | Product revision represented by the current model. |
| `explorationRevision` | revision/null | Model revision represented by current exploration/generated tests. |
| `validationRevision` | revision/null | Product/exploration revision proven by Stage 07. |
| `readiness` | `ReadinessEvidence` | PASS/FAIL plus revisions and inventory digest. |
| `reachability` | `ReachabilitySet` | Every currently reached/unreached path and its disposition. |
| `repairObligations` | set of `RepairObligation` | Remain open through delivery, release, target verification, and current PASS. |
| `learningRefresh` | `LearningRefreshObligation` | Independent pending/current state keyed to accepted source-set digest. |
| `incidentState` | `IncidentDemandState` | Actual incident, stabilization, selected set, and resulting items. |
| `refactorDecision` | `RefactorDecision?` | Selected/no-work/direction and architecture impact. |
| `returnContext` | lane/context token/null | Restores the invoking steady/operations context after independent Stage 31 work. |

### `EvidenceStamp`

| Field | Type | Rules |
|---|---|---|
| `kind` | enum | Runway, architecture, model, exploration, functional validation, deletion revalidation, direct tests, coverage, target verification, learning validation. |
| `producer` | string | Exact command/tool identity and version. |
| `inputDigest` | digest | Covers authoritative inputs. |
| `outputDigest` | digest | Covers the evidence/report. |
| `productRevision` | revision | Must equal the revision the evidence claims. |
| `createdAtUtc` | timestamp | Informational; freshness is revision/digest based, not age guessed from time. |
| `passed` | bool | False evidence cannot authorize a transition. |

**Currency rule:** Evidence is current only when its declared input/product/model
digests equal current authoritative state. A newer timestamp cannot rescue a mismatched
digest. Product changes invalidate downstream model, exploration, validation, and
readiness in dependency order.

### `TransitionAttempt`

| Field | Type | Rules |
|---|---|---|
| `requestedStage` | `Stage` or raw command input | Unknown input is retained for actionable rejection. |
| `preStateDigest` | digest | Captures state before evaluation. |
| `outcome` | enum | `accepted` or `rejected`; no warning-success state. |
| `reasonCode` | string/null | Required for rejection; stable code plus human detail. |
| `missingEvidence` | list | Exact absent/stale/failing rows. |
| `postStateDigest` | digest | Equals pre-state on rejection. |

Rejected attempts leave all state unchanged.

## 5. Stage 02 evidence

### `TestRunwayEvidence`

| Field | Rules |
|---|---|
| Framework/package versions | Exact and repository-selected. |
| Terse command | One command vector, identical in all five observations. |
| Boundary test identity | Must exercise the real product boundary. |
| Build observation | Exit 0 plus product and test artifacts identified. |
| Discovery observation | Selected boundary test appears in runner output. |
| Passing observation | Boundary test passes. |
| Intentional failure observation | Same test/runner command returns non-zero and names the controlled failure. |
| Restoration observation | Temporary defect source absent; same command returns zero. |
| Generated destination | Stable root-relative directory. |
| Source/config digests | Bind evidence to the current runway definition. |

State transitions are `unproven → proving → proven`; any failed/missing observation
returns to `unproven` with visible evidence. Changing framework, command, project, or
destination creates a new proof; evidence is never carried across silently.

## 6. Module classification and verification

### `ModuleDescriptor`

| Field | Type | Rules |
|---|---|---|
| `id` | stable string | Unique, explicit, independent of test discovery. |
| `path` | root-relative path | Existing production module/project. |
| `class` | enum | `component`, `domain-vertical`, `pure-value`. |
| `coverageIdentity` | string | Exact report module/assembly identity. |
| `runtimeEntryEvidence` | list | Public/configuration/reflection/platform entry contracts where applicable. |
| `verificationMethod` | enum | Derived from class: unit/property or behavior-level SEK plus justified direct evidence. |

The authoritative discovery command's result set must equal the configured descriptor
set. An omitted or extra module fails readiness; generic code in a domain vertical is
an architecture failure.

## 7. Stages 05–07 functional evidence

### `BehaviorModelRecord`

Fields: model ID/revision, SUT boundary, interacting state fields, action set, legal
guards, invalid-input classes, expected success/rejection outcomes, effects,
invariants, bounded domains, and abstraction rationale.

**Adequacy:** Multiple interacting state facts or real ordering constraints; no flat
stage-name-only model. Rejection semantics are represented by guards/model outcomes so
SEK generates the attempt and assertion.

### `ExplorationRecord`

Fields: model digest, CORD digest, bounds, deterministic seed/options, states,
transitions, accepting/goal states, branches, negative edges by class, generated test
count, destination, generator revision, and whether a bound was hit.

**Validation:** A hit bound without an explicitly accepted complete scope fails. Legal
success, illegal order, and invalid input must each have generated witnesses. Paths
must be materially distinct rather than one flat tour.

### `FunctionalValidationRecord`

| Field | Rules |
|---|---|
| Generated suite identity | Freshly generated from the recorded model/CORD digests. |
| SUT binding identity | Real stateful EngLoopKit boundary; one portable binding path. |
| Positive results | All required legal witnesses pass. |
| Negative results | Generated illegal-order and invalid-input attempts are rejected. |
| Branch evidence | Distinct explored paths and transition coverage. |
| Reachability report | Produced by generated tests only. |
| Verdict | `functional-pass` or `functional-fail`; never `ready`. |

## 8. Stage 08 reachability and readiness

### `ProductionPath`

A stable path identity from current generated-suite coverage: module, source identity,
line/branch or semantic path, reached flag, and runtime-entry evidence.

### `ReachabilityDisposition`

| Value | Meaning | Required next action |
|---|---|---|
| `unclassified` | Generated suite did not reach the path. | Investigation; blocks all new direct tests. |
| `intended-gap` | Authoritative product/platform/config/error intent exists. | Return through 05→06→07. |
| `unsupported-residue` | No authoritative requirement and no runtime entry mechanism. | Delete in a coherent set. |
| `deletion-awaiting-revalidation` | Residue removed but current Stage 07 proof absent. | Build + architecture + complete Stage 07. |
| `functionally-justified` | Current generated evidence reaches and justifies it. | Eligible for later direct tests if needed. |
| `reviewed-direct-only` | Survives for an explicit authoritative reason not functionally invocable. | Direct unit/property evidence permitted after all paths are classified. |

Ambiguous runtime entry remains `unclassified`; ambiguity is neither deletion authority
nor a direct-test justification.

### `ReachabilitySet`

Contains every `ProductionPath`, its current disposition, authority links, reviewer,
and evidence revision. It is complete only when there are zero unclassified,
intended-gap, deletion-awaiting-revalidation, or stale entries.

### `ReadinessInventoryRow`

| Field | Rules |
|---|---|
| Module/class | Must match the authoritative module inventory. |
| Architecture | Current PASS required. |
| Functional evidence | Required for domain behavior; n/a only by class/explicit direct-only rationale. |
| Negative conformance | Required for stateful domain vertical. |
| Branching | Required for stateful domain vertical. |
| Reachability disposition | Complete and current. |
| Unit/property evidence | Added only after disposition; required by class/survivor rationale. |
| Line coverage | Measured per module; at least 95.00%. |
| Branch coverage | Measured per module; at least 95.00%. |
| Regressions | Current PASS required. |
| Verdict/reasons | PASS only if every applicable field passes; all failures named. |

### `ReadinessEvidence`

Fields: verdict, product revision, Stage 07 validation digest, disposition digest,
module-inventory digest, architecture digest, direct-test digest, coverage digest,
regression digest, and rows.

**Computed invariant:** PASS iff every authoritative module has exactly one passing row.
No aggregate, exception rationale, missing row, or stale evidence can produce PASS.

## 9. Operations

### `IncidentRecord` / `Mitigation`

An incident exists only for an actual operating disruption after current readiness.
It has a timeline, status, recovery evidence, and locally numbered mitigations. A
mitigation never closes a repair obligation or mutates source as a permanent fix.

### `PostmortemRecord`

References a deliberately selected non-empty set of stabilized incidents, systemic
analysis, globally addressable accepted source learnings (`PMxxx/LEARNxxx`), and one or
more concrete repair items when warranted. Acceptance of a new learning updates the
learning source-set digest and makes `LearningRefreshObligation` pending.

### `RepairObligation`

| Field | Rules |
|---|---|
| `id` | `PMxxx/RPIxxx`; globally unambiguous pair. |
| `deliveryRevision` | Revision created through Stage 04. |
| `model/explore/validate/readiness` | Applicable current evidence required. |
| `releaseArtifactDigest` | Exact built release containing the repair. |
| `targetIdentity` and verification | Exact target and passing proof. |
| `status` | Remains open until all above pass. |

There is no small-change bypass or tinyspec route within Stage 22.

## 10. Evolution and learning

### `RefactorDecision`

Fields: REFACT ID, ordered signal evaluation, first branch fired, selected work or explicit
no-work, Northstar impact (`unchanged` or evidence-backed direction change), architecture
impact, capacity evidence, and next route. Exactly one decision per completed scan;
never a numbered direction snapshot.

### `LearningSource`

Identity is the pair `PMxxx/LEARNxxx`; fields include PM chronology, exact Learnings
section anchor/content digest, acceptance state, and source file. Source content is
immutable to condensation.

### `SubjectCard`

| Field | Rules |
|---|---|
| Slug/path | Unique file directly under `.engloop/learnings/cards/`. |
| Subject/recall cue | Terse and non-empty. |
| Principle | Compressed, plain language. |
| Applicability | Explicit decisions/tasks where it applies. |
| Operational checks | Actionable list. |
| Sources | Non-empty set of valid `PMxxx/LEARNxxx` links. |
| Tensions | Explicit conflicts, supersessions, unresolved tension, or “none known.” |

Many-to-many source/card relationships are valid. Every accepted source has degree at
least one; every card has source degree at least one.

### `LearningsIndex`

Root `LEARNINGS.md`, containing exactly one resolving target per current card and terse
relationship-oriented cues. Maximum 500 words and 60 nonblank lines under the exact
counting algorithm in the learning contract.

### `LearningRefreshObligation`

Fields: accepted source-set digest, last validated digest, pending bool, and last Stage
31 result. New accepted sources make it pending. Stage 31 clears it only when static
validation and clean-context retrieval both pass for the same source/card/index digests.
It neither blocks stabilization nor satisfies repair.

### `RetrievalCase` / `RetrievalResult`

A case contains question, expected card IDs, expected source IDs, and coverage tags.
The case set covers every card and at least one source from every PM. A fresh isolated
run receives only the on-demand instruction entry point plus the question. Results list
actual card/source IDs; exact comparison rejects omissions and false provenance.

## 11. Consumer installation and release

### `ReleaseArtifact`

Fields: kind (`tool`, `extension`, `bundle`), product ID `engloopkit`, release version
1.7.0, path/URL, SHA-256, build revision, and acceptance result. Artifacts are immutable
after consumer acceptance; a changed bitstream receives a new version.

### `ConsumerInstallation`

| Field | Rules |
|---|---|
| Root identity | Exact selected root; no parent/sibling lookup. |
| Pre-migration snapshot | Git revision or explicit checksummed filesystem backup. |
| Root migration | One coherent old-root rename or direct target-root creation; no merge/copy compatibility state. |
| Removal proof | No installed EngLoopKit directory/registry ownership/current old generated files. |
| Installed artifact | Exact 1.7.0 artifact digest. |
| Registry surface | Exactly 13 v2 IDs, zero old IDs. |
| Generated surface | Exactly one rich agent/prompt per v2 ID, zero old files, and a passing `GeneratedSurfaceSemanticComparison`. |
| Customization diagnostics | Passing `CustomizationDiagnosticsResult` for the supported VS Code build. |
| Entry validation | Invalid entry is mechanically blocked with Preview hooks enabled; hook-disabled use records observed unconditional body rejection and trusted-tool rejection of all invalid durable transition/evidence attempts. |
| Local root/config | Exactly one tracked `.engloop/`, one `.engloop/config.json`, ignored `.engloop/out/`, and no current `engloop/` or `.engloopkit/`. |
| Direction/learning entry points | Exactly one reviewed root Northstar and one root `LEARNINGS.md`; no current numbered-direction machinery. |
| Entry points | Folder-open plus one root-local focused `.code-workspace`. |
| Standalone result | Applicable commands resolve and run without siblings. |

Installation states are `captured → removed → absence-proven → installed →
surface-proven → standalone-proven`. Any failure stops that root. Rollback is an
explicit restore/reinstall operation; the product never falls back at runtime.

### `RootMigrationRecord`

| Field | Rules |
|---|---|
| Source layout | Records whether the current-source `engloop/` tree exists, whether Git owns it, and the exact pre-state digest/revision. |
| Ambiguity check | Must prove target `.engloop/` and forbidden `.engloopkit/` are absent before a visible-root rename; any dual/forbidden state stops before mutation. |
| Root operation | Existing Git root: one history-preserving `git mv engloop .engloop`; no-source-root consumer: create `.engloop/` directly under a complete backup boundary and explicitly establish source-control tracking without claiming prior ancestry. |
| Direction operation | After the root move, move the tracked initial direction file to root `NORTHSTAR.md` where one exists; otherwise create one reviewed Northstar. |
| Config/output operation | Create `.engloop/config.json` directly and ensure only `.engloop/out/` is ignored. |
| Link rewrite | Update current-source links and path declarations to target `.engloop/` paths in the same coherent change. |
| Post-state | Exactly one process root/config, zero forbidden roots, visible Northstar/Learnings, source-control tracking proof for every durable `.engloop/` file, and resolving links. |
| Rollback | Reverse/restore the entire captured root transaction. Never restore `engloop/` beside a live `.engloop/`, and never use old config as runtime fallback. |

Migration states are `captured → unambiguous → tracking-boundary-confirmed-or-
explicitly-bootstrapped → root-moved-or-created → direction-moved-or-created →
links-and-config-updated → old-roots-absent → tracking-proven → validated`.
A rejected transition preserves the last complete state; a partial state cannot be an
accepted consumer installation.

## Relationship summary

```text
RepositoryRootLayout 1 ── 1 EngLoopConfiguration
RepositoryRootLayout 1 ── 1 Northstar
EngLoopConfiguration 1 ── 1 Northstar
EngLoopConfiguration 1 ── * ModuleDescriptor
CommandDescriptor 1 ── 1 AgentDescriptor
AgentDescriptor 1 ── 1 ToolPolicy
AgentDescriptor 1 ── 1 SubagentPolicy
AgentDescriptor 1 ── 1 EntryHook
AgentDescriptor 1 ── * HandoffEdge
GeneratedSurfaceSemanticComparison 1 ── 13 AgentDescriptor
CustomizationDiagnosticsResult 1 ── 1 GeneratedSurfaceSemanticComparison
EngineeringLoopState 1 ── * EvidenceStamp
EngineeringLoopState 1 ── * RepairObligation
EngineeringLoopState 1 ── 1 LearningRefreshObligation
BehaviorModelRecord 1 ── * ExplorationRecord
ExplorationRecord 1 ── 1 Generated functional suite
FunctionalValidationRecord 1 ── 1 ReachabilitySet
ReachabilitySet 1 ── * ProductionPath / ReachabilityDisposition
ReadinessEvidence 1 ── * ReadinessInventoryRow
PostmortemRecord 1 ── * LearningSource
PostmortemRecord 1 ── * RepairObligation
LearningSource * ── * SubjectCard
LearningsIndex 1 ── * SubjectCard
ReleaseArtifact 1 ── * ConsumerInstallation
ConsumerInstallation 1 ── 1 RootMigrationRecord
ConsumerInstallation 1 ── 1 GeneratedSurfaceSemanticComparison
ConsumerInstallation 1 ── 1 CustomizationDiagnosticsResult
```

## Model validation exit

The model is sufficient for implementation planning because it explicitly represents:

- legal and illegal stage attempts;
- separate invocation lanes;
- current/stale readiness evidence;
- pending repair and pending learning refresh;
- Stage 08 path disposition and deletion revalidation;
- model-derived legal, illegal-order, and invalid-input outcomes;
- deterministic learning completeness and retrieval evidence;
- exact hidden-root/config discovery and forbidden dual-root rejection;
- exact custom-agent tool/subagent/hook/handoff semantics and UI-only handoff effects;
- semantic source-to-installed comparison, prompt precedence protection, and
	customization diagnostics;
- clean, standalone, fail-closed consumer installation.
