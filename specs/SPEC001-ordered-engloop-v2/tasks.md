# Tasks: SPEC001 Ordered EngLoop v2

Feature directory: `c:\boards\brd009\EngLoopKit\specs\SPEC001-ordered-engloop-v2`

Authoritative inputs used: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, all contracts under `contracts/`, and `c:\boards\brd009\EngLoopKit\.engloop\refactors\REFACT001_ordered-engloop-v2.md`.

Scope guard for this planning artifact:
- Only this `tasks.md` is generated/updated in this step.
- Do not modify `.specify/`.
- Implementation/consumer repo edits happen in future execution, not now.

## Phase P0 — Hard Blocking Canary (must pass before production 13-header authoring)

Goal: Fail closed on unsupported custom-agent generation/preservation before expensive implementation work.

- [X] T001 Create `scripts/test-spec-kit-agent-preservation.ps1` to run the disposable preservation canary under ignored `.engloop/out/spec-kit-agent-canary/` with deterministic cleanup/retain-on-fail behavior in `scripts/test-spec-kit-agent-preservation.ps1`
- [X] T002 Implement canary fixture generation and semantic comparison pipeline using YamlDotNet 18.1.0 in the disposable harness rooted at `.engloop/out/spec-kit-agent-canary/`
- [X] T003 Implement tracked schema projection loading and pin checks for VS Code Insiders `1.129.0-insider` commit `29d19ddd1af725baf537b6b328843bcdc2d29ba1` in `schemas/vscode-agent-surface.schema.json`
- [X] T004 Execute the canary against Spec Kit `0.12.4` and pinned VS Code projection, writing mismatch evidence to `.engloop/out/spec-kit-agent-canary/report.json`
- [X] T005 Enforce hard stop gating in `scripts/test-spec-kit-agent-preservation.ps1` that blocks all tasks touching 13 production command headers when any required semantic field/absence is dropped or rewritten
- [X] T006 Validate canary assertions for exact required fields: rich headers, least-privilege tools, explicit `agents`, hooks, and stage-specific handoffs in fixture source/installed/generated YAML under `.engloop/out/spec-kit-agent-canary/`
- [X] T007 Validate canary assertions for exact required absences: agent `infer` absent, agent `model` absent, prompt `tools` absent, handoff `model` absent in `.engloop/out/spec-kit-agent-canary/`
- [X] T008 Record and gate on explicit “no EngLoop-owned alternate generator / no post-processing fallback” policy in `specs/SPEC001-ordered-engloop-v2/contracts/command-surface.md`

## Phase 1 — Setup and Planning Guardrails

Goal: Freeze authoritative baseline and make execution reproducible.

- [X] T009 Capture immutable planning inputs and digests for SPEC001 authorities in `.engloop/out/spec001-planning-inputs.json`
- [X] T010 [P] Capture current command/package/install baseline snapshot for v1 surfaces in `.engloop/out/spec001-command-baseline.json`
- [X] T011 [P] Capture current root-layout/config/link baseline snapshot in `.engloop/out/spec001-root-baseline.json`
- [X] T012 Pin SDK to `8.0.422` for reproducible Stage 02 runway and CI behavior in `global.json`
- [X] T013 Add/verify transient-only ignore coverage for `.engloop/out/` while preserving durable tracked `.engloop/` content in `.gitignore`
- [X] T014 Add deterministic validation task orchestration script entrypoints for SPEC001 gates in `scripts/validate-v2.ps1`

## Phase 2 — Foundational Prerequisites (blocking for all user stories)

Goal: Establish shared architecture/config/tooling required by all stories.

- [X] T015 Define/validate explicit config schema contract for `.engloop/config.json` in `schemas/engloopkit-config.schema.json`
- [X] T016 Implement root-layout fail-closed validator (exact `.engloop/`, forbid `engloop/` and `.engloopkit/`) in `src/EngLoopKit.Tool/ValidationCommands.cs`
- [X] T017 [P] Implement config parsing and canonical path safety checks in `src/EngLoopKit.Core/Evidence.cs`
- [X] T018 [P] Implement authoritative module-inventory equality checks (configured vs discovered) in `src/EngLoopKit.Tool/ValidationCommands.cs`
- [X] T019 Create `components/EngLoopKit.Components.DocumentValidation` project skeleton and wire into solution in `components/EngLoopKit.Components.DocumentValidation/EngLoopKit.Components.DocumentValidation.csproj`
- [X] T020 [P] Implement semantic YAML canonical projection utilities in `components/EngLoopKit.Components.DocumentValidation/SemanticProjection.cs`
- [X] T021 [P] Implement Markdown link/content resolution validators in `components/EngLoopKit.Components.DocumentValidation/MarkdownLinks.cs`
- [X] T022 [P] Implement text-budget validators (500 words/60 nonblank lines) in `components/EngLoopKit.Components.DocumentValidation/TextBudget.cs`
- [X] T023 [P] Implement set-coverage and provenance validators in `components/EngLoopKit.Components.DocumentValidation/SetCoverage.cs`
- [X] T024 Wire tool-level validation commands (`root`, `config`, `commands`, `reachability`, `learnings`, `installation`, `agent-entry`, `agent-surfaces`) in `src/EngLoopKit.Tool/Program.cs`
- [X] T025 Add foundational tests for document-validation component utilities in `tests/EngLoopKit.Tests/DocumentValidationTests.cs`
- [X] T026 Add foundational tests for root/config fail-closed behavior in `tests/EngLoopKit.Tests/InstallationValidationTests.cs`

## Phase 3 — User Story 1 (P1): Ordered delivery/readiness lane 01–08

Story goal: Implement and prove the complete ordered delivery contract from Northstar through Stage 08 readiness gate ownership.

Independent test criteria:
- Normal path `01→02→03→04→05→06→07→08` accepted only with owned evidence.
- Stage 07 reports functional evidence only (no readiness claim).
- Stage 08 performs delete-before-unit-test ordering and emits sole final readiness verdict.

- [X] T027 [US1] Implement/refresh Stage 01 Northstar command contract in `extensions/engloopkit/commands/speckit.engloop.01-northstar.md`
- [X] T028 [US1] Implement Stage 02 scaffold/runway command contract in `extensions/engloopkit/commands/speckit.engloop.02-scaffold.md`
- [X] T029 [US1] Implement Stage 03 architecture command contract in `extensions/engloopkit/commands/speckit.engloop.03-architect.md`
- [X] T030 [US1] Implement Stage 04 governed refactor command contract in `extensions/engloopkit/commands/speckit.engloop.04-refactor.md`
- [X] T031 [US1] Implement Stage 05 model command contract in `extensions/engloopkit/commands/speckit.engloop.05-model.md`
- [X] T032 [US1] Implement Stage 06 explore command contract in `extensions/engloopkit/commands/speckit.engloop.06-explore.md`
- [X] T033 [US1] Implement Stage 07 functional validate command contract in `extensions/engloopkit/commands/speckit.engloop.07-validate.md`
- [X] T034 [US1] Implement Stage 08 unit/readiness command contract with strict ordering in `extensions/engloopkit/commands/speckit.engloop.08-unittest.md`
- [X] T035 [US1] Implement Stage 02 runway proof script and durable evidence emission in `scripts/prove-test-runway.ps1`
- [X] T036 [US1] Implement rich engineering-loop state model (readiness currency, obligations, disposition state) in `src/EngLoopKit.Core/EngineeringLoopState.cs`
- [ ] T037 [US1] Implement legal transition + rejection reason evaluation in `src/EngLoopKit.Core/EngineeringLoop.cs`
- [X] T038 [US1] Implement Stage 07 functional-only validation orchestration in `scripts/validate-functional.ps1`
- [X] T039 [US1] Implement Stage 08 reachability disposition + delete-before-unit-test orchestration in `scripts/validate-readiness.ps1`
- [X] T040 [US1] Implement Stage 08 readiness inventory computation (sole PASS/FAIL emitter) in `src/EngLoopKit.Core/ReadinessGate.cs`
- [X] T041 [US1] Implement independent model updates for rich state and required rejections in `model/EngLoopKit.Model/Model.cs`
- [ ] T042 [US1] Implement bounded CORD scenarios for legal/illegal/feedback routes in `model/EngLoopKit.Model/Config.cord`
- [X] T043 [US1] Implement deterministic generated-suite regeneration and freshness checks in `scripts/generate-loop-tests.ps1`
- [X] T044 [US1] Implement generated-suite-only Stage 07 coverage path and evidence output in `.engloop/coverage/COV002_ordered-engloop-v2-functional.md`
- [X] T045 [US1] Implement Stage 08 final readiness evidence output in `.engloop/coverage/COV003_ordered-engloop-v2-readiness.md`
- [ ] T046 [US1] Add transition rejection/feedback unit tests (including stale evidence and bypass cases) in `tests/EngLoopKit.Tests/EngineeringLoopTests.cs`
- [X] T047 [US1] Add readiness gate tests enforcing full module inventory and per-module 95/95 thresholds in `tests/EngLoopKit.Tests/ReadinessGateTests.cs`
- [X] T048 [US1] Add generated-conformance regression verification tests for required negative classes in `tests/EngLoopKit.Tests/EvidenceCurrencyTests.cs`

## Phase 4 — User Story 5 (P1): Guided least-privilege custom agents

Story goal: Ship exact rich custom-agent/prompt behavior with strict least privilege, exact handoffs, and validated hook-enabled and reduced-assurance behavior.

Independent test criteria:
- Exactly 13 visible EngLoop agents with valid rich headers and zero EngLoop-owned diagnostics.
- Exact least-privilege tools/subagent rows and exact 23 `send:false` handoff edges.
- Prompts select exact agents with no `tools` overrides.
- Hook-enabled blocking, hook-disabled observed body behavior, and trusted durable-stage gate rejections proven.

- [X] T049 [US5] Replace extension manifest command set with exact ordered 13 v2 IDs in `extensions/engloopkit/extension.yml`
- [X] T050 [US5] Delete all v1 command surfaces and keep only v2 command files in `extensions/engloopkit/commands/`
- [X] T051 [US5] Implement Stage 20 command rich header/body contract in `extensions/engloopkit/commands/speckit.engloop.20-incident.md`
- [X] T052 [US5] Implement Stage 21 command rich header/body contract in `extensions/engloopkit/commands/speckit.engloop.21-postmortem.md`
- [X] T053 [US5] Implement Stage 22 command rich header/body contract in `extensions/engloopkit/commands/speckit.engloop.22-repair.md`
- [X] T054 [US5] Implement Stage 30 command rich header/body contract in `extensions/engloopkit/commands/speckit.engloop.30-refactor-scan.md`
- [X] T055 [US5] Implement Stage 31 command rich header/body contract (no static handoff) in `extensions/engloopkit/commands/speckit.engloop.31-learnings-pyramid.md`
- [X] T056 [US5] Apply exact least-privilege tools + explicit subagent allowlists/empties across all 13 command headers in `extensions/engloopkit/commands/`
- [X] T057 [US5] Apply exact stage-specific SessionStart entry hooks across all 13 command headers in `extensions/engloopkit/commands/`
- [X] T058 [US5] Apply exact ordered 23-edge handoff graph with `send: false` and no handoff model overrides in `extensions/engloopkit/commands/`
- [X] T059 [US5] Generate/validate 13 matching prompt files selecting exact agents with no `tools` field in `.github/prompts/`
- [X] T060 [US5] Implement trusted durable-stage transition/evidence gate checks invoked by all state-accepting tool operations in `src/EngLoopKit.Tool/ValidationCommands.cs`
- [X] T061 [US5] Implement entry validator command behavior (`agent-entry`) with blocking exit semantics in `src/EngLoopKit.Tool/ValidationCommands.cs`
- [X] T062 [US5] Implement semantic source/archive/install agent/prompt comparison validator in `src/EngLoopKit.Tool/ValidationCommands.cs`
- [X] T063 [US5] **Superseded by ARCH006**: UI diagnostics/picker/handoff capture is forbidden; deterministic source/archive/disposable-install validation is the release gate.
- [X] T064 [US5] Validate deterministic invalid-entry rejection (exit code 2) in disposable-install fixture.
- [X] T065 [US5] Validate trusted local-tool durable gate rejection without UI hooks.
- [X] T066 [US5] Add tests for exact tools/subagents matrix and required/forbidden field presence/absence in `tests/EngLoopKit.Tests/AgentSurfaceValidationTests.cs`
- [X] T067 [US5] Add tests for exact 23-edge handoff graph, Stage31 no-handoff, and Stage08 no 20/30/31 edges in `tests/EngLoopKit.Tests/CommandSurfaceTests.cs`
- [X] T068 [US5] Add tests that prompt files preserve exact agent selection and forbid `tools` overrides in `tests/EngLoopKit.Tests/AgentSurfaceValidationTests.cs`

## Phase 5 — User Story 2 (P1): Operations lane 20–22 with permanent repair routing

Story goal: Keep incident stabilization, post-mortem analysis, and permanent repair semantically distinct and fail-closed.

Independent test criteria:
- No demand, no operations work.
- Stage 20 mitigation-only behavior preserved.
- Stage 22 must route repair through 04 and applicable 05–08 gates to close.

- [ ] T069 [US2] Implement operations demand guards (no incident/no selected set/no repair item rejection) in `src/EngLoopKit.Core/EngineeringLoop.cs`
- [ ] T070 [US2] Implement incident timeline/mitigation evidence data structures in `src/EngLoopKit.Core/Evidence.cs`
- [ ] T071 [US2] Implement postmortem output contracts for `PMxxx/LEARNxxx` and `RPIxxx` artifacts in `extensions/engloopkit/templates/PM-template.md`
- [ ] T072 [US2] Implement repair obligation lifecycle tracking through source/release/target/readiness evidence in `src/EngLoopKit.Core/Evidence.cs`
- [ ] T073 [US2] Implement Stage22 routing checks to prevent small-change bypass semantics in `src/EngLoopKit.Core/EngineeringLoop.cs`
- [ ] T074 [US2] Add operations lane conformance tests (including repeated incidents before selected review) in `tests/EngLoopKit.Tests/EngineeringLoopTests.cs`
- [ ] T075 [US2] Add repair closure tests requiring downstream evidence currency and current readiness PASS in `tests/EngLoopKit.Tests/EvidenceCurrencyTests.cs`

## Phase 6 — User Story 3 (P2): Evolution scan + Learnings Pyramid

Story goal: Support opportunistic evolution and loss-aware learnings condensation with deterministic provenance and retrieval.

Independent test criteria:
- Stage30 records exactly one REFACT decision or no-work result.
- Stage31 static and retrieval gates pass together to clear pending refresh.
- Source PM/LEARN immutability and provenance integrity maintained.

- [ ] T076 [US3] Implement Stage30 single-decision/no-work selection contract in `src/EngLoopKit.Core/Loop.cs`
- [ ] T077 [US3] Implement Stage30 branch routing logic for direction/architecture impacts in `src/EngLoopKit.Core/EngineeringLoop.cs`
- [ ] T078 [US3] Implement Stage31 source extraction and source/card/index completeness validation in `src/EngLoopKit.Core/LearningsPyramidPolicy.cs`
- [ ] T079 [US3] Implement retrieval-cases schema and parser in `schemas/retrieval-cases.schema.json`
- [ ] T080 [US3] Implement retrieval runner script for clean-context case execution in `scripts/run-learning-retrieval.ps1`
- [ ] T081 [US3] Implement Learnings validation command (static + retrieval-result comparison) in `src/EngLoopKit.Tool/ValidationCommands.cs`
- [ ] T082 [US3] Update cards to enforce explicit conflict/supersession/tension visibility in `.engloop/learnings/cards/`
- [ ] T083 [US3] Update root index and instruction path governance for progressive retrieval only in `LEARNINGS.md`
- [ ] T084 [US3] Add Learnings pyramid tests for source completeness, card provenance, link resolution, and page budget in `tests/EngLoopKit.Tests/LearningPyramidValidationTests.cs`
- [ ] T085 [US3] Add retrieval-case comparison tests (false provenance + missing coverage fails) in `tests/EngLoopKit.Tests/LearningPyramidValidationTests.cs`

## Phase 7 — Release Freeze and Migration Tooling

Goal: Build and validate immutable 1.7.0 bits before any consumer reset or install.

- [ ] T086 Implement immutable release manifest generation from validated 1.7.0 artifacts in `scripts/validate-package.ps1`
- [ ] T087 Implement consumer migration transaction helper with explicit `TrackingMode` and fail-closed preflight in `scripts/migrate-consumer.ps1`
- [ ] T088 Implement migration preflight stop conditions for forbidden/dual roots before mutation in `scripts/migrate-consumer.ps1`
- [ ] T089 Implement migration post-checks for exact root/config/layout/link invariants in `scripts/migrate-consumer.ps1`
- [ ] T090 Implement consumer install reset/removal + exact digest reinstall validation in `scripts/migrate-consumer.ps1`
- [ ] T091 Implement per-root 13/0 command/agent/prompt semantic surface checks in `scripts/migrate-consumer.ps1`
- [ ] T092 Implement focused one-root workspace generation and verification (`.` only) in `scripts/migrate-consumer.ps1`
- [ ] T093 Run the full pre-migration local gate (build/tests/model/explore/functional/readiness/learnings/agent-surface/package) and persist objective evidence in `scripts/validate-v2.ps1`
- [ ] T094 [P] Update migration and workflow docs to exact v2 semantics and standalone/focused usage guidance in `docs/v2-migration.md`
- [X] T095 [P] Update extension/bundle/catalog/changelog metadata to coherent immutable `1.7.0` release identity in `catalog.json`
- [X] T096 Enforce deterministic source/archive/disposable-install semantic parity before release pass in `scripts/validate-agent-surfaces.ps1`.
- [X] T097 **Superseded by ARCH006**: no UI diagnostics gate; deterministic semantic mismatch count must be zero.
- [ ] T098 Run FR/SC preservation audit proving 102 FR IDs and 20 SC IDs are fully covered by implemented checks/evidence in `.engloop/out/spec001-traceability-audit.json`
- [ ] T099 Confirm no EngLoop-owned alternate generator/post-processing path exists in release implementation in `tests/EngLoopKit.Tests/InstallationValidationTests.cs`
- [ ] T100 Produce three independently runnable acceptance results for delivery 01–08, operations 20–22, and stewardship 30–31 in `.engloop/out/stage-group-acceptance/`
- [X] T101 Validate lexical command ID order and command-loop required sections through deterministic source/archive/install projections.
- [ ] T102 Produce final immutable tool/extension/bundle artifacts and release manifest with digests and source/release evidence linkage in `.engloop/out/release-manifest.json`

## Phase 8 — User Story 4 (P1): Ordered picker and standalone consumer migration

Story goal: Migrate consumers to clean standalone v2 usage with exact 13-command surfaces; immediate deployment target is TTHP.

Independent test criteria:
- Each migrated root has exactly one tracked `.engloop/`, one `.engloop/config.json`, one root `NORTHSTAR.md`, one root `LEARNINGS.md`, zero current `engloop/`, zero `.engloopkit/`, and exact 13/0 command surface.
- TTHP migration executes first and uses installed `speckit.engloop.01-northstar` with preserved seed content.
- Workshop migration follows TTHP PASS; verification consumer follows workshop PASS.

- [ ] T103 [US4] Capture TTHP's current seed bytes, Git identity, and SHA-256 outside reset-owned paths, then evolve that exact content into root `NORTHSTAR.md` only after immutable 1.7.0 artifact PASS in `c:\boards\brd009\tthp\NORTHSTAR.md`
- [ ] T104 [US4] Execute the TTHP clean reset/install transaction and submit the preserved seed content to the installed `speckit.engloop.01-northstar` agent, retaining the reviewed prompt/result evidence in `c:\boards\brd009\tthp\.engloop\out\northstar-invocation.json`
- [ ] T105 [US4] Capture TTHP standalone and focused picker/handoff evidence on the pinned VS Code build in `c:\boards\brd009\tthp\.engloop\out\migration-evidence.json`
- [ ] T106 [US4] Execute workshop migration only after TTHP PASS with clean root/layout and reviewed workshop-specific Northstar in `c:\boards\brd009\engloop-workshop\NORTHSTAR.md`
- [ ] T107 [US4] Capture workshop standalone and focused picker/handoff evidence in `c:\boards\brd009\engloop-workshop\.engloop\out\migration-evidence.json`
- [ ] T108 [US4] Execute VerifyExtremeEdgeWithTpcc migration only after workshop PASS using explicit `InitializeGit` mode and full checksummed backup in `c:\boards\brd009\VerifyExtremeEdgeWithTpcc\.engloop\out\migration-evidence.json`
- [ ] T109 [US4] Capture VerifyExtremeEdgeWithTpcc standalone and focused picker/handoff evidence in `c:\boards\brd009\VerifyExtremeEdgeWithTpcc\.engloop\out\migration-evidence.json`
- [ ] T110 [US4] Verify the mega-workspace remains an integration view while focused one-root workspaces are recommended in `docs/workspaces.md`

## Final Phase — Post-Migration Integration Validation

Goal: Verify focused and aggregate workspace behavior without rebuilding accepted bits.

The post-migration integration gate is T110. It consumes the immutable artifacts from
T102 and all per-root migration evidence; it never rebuilds or rewrites 1.7.0.

## Dependency Order

### Hard blockers

- P0 (`T001`–`T008`) is a strict stop/go gate.
- No production 13-command rich-header implementation tasks may start before P0 PASS.

### Phase-level dependencies

- Phase 1 depends on P0 PASS.
- Phase 2 depends on Phase 1.
- US1 (Phase 3) depends on Phase 2.
- US5 (Phase 4) depends on Phase 2 and P0 PASS.
- US2 (Phase 5) depends on US1 core state/evidence contracts (`T036`–`T040`).
- US3 (Phase 6) depends on Phase 2 and operations/learning obligations availability from US2.
- Release Freeze (Phase 7) depends on US1, US2, US3, and US5 implementation/evidence.
- `T102` is the final pre-migration freeze and depends on `T086`, `T093`–`T101`; no
	consumer task may rebuild those accepted bits.
- US4 (Phase 8) depends on immutable artifact PASS from `T102` plus migration tooling
	`T087`–`T092`.
- Migration order inside US4 is strict: TTHP (`T103`–`T105`) → workshop
	(`T106`–`T107`) → Verify consumer (`T108`–`T109`).
- Post-migration integration `T110` depends on all three per-root PASS results.

### Task-level critical dependencies

- `T035` requires `T012`, `T015`–`T018`.
- All production command/header tasks `T027`–`T034` and `T051`–`T059` require P0
	canary PASS from `T004`/`T005`.
- `T039` requires `T038`, `T044`; unit/property test tasks `T046`–`T048` require T039's
	complete disposition plus a later green Stage 07 rerun and architecture check.
- `T063`–`T066` require `T062` semantic comparator readiness.
- `T093` runs only pre-migration product/release gates and excludes T103–T110.
- `T102` requires `T086`, `T093`–`T101` PASS.
- `T103` requires `T087`–`T092` and `T102` PASS.
- `T104` requires T103's verified seed backup and preserved-content digest.
- `T106` requires `T105` PASS.
- `T108` requires `T107` PASS.
- `T110` requires `T105`, `T107`, and `T109` PASS.

## Execution Gate Matrix

| Gate | Passing tasks | Blocks |
|---|---|---|
| P0 custom-agent preservation | T001–T008 | T027–T034, T049–T059, package/install work |
| Foundational root/config/tooling | T009–T026 | all story implementation |
| Functional reachability disposition | T038, T039, T044 | T046–T048 and readiness PASS |
| Agent surface release evidence | T062–T068, T096, T097 | T102 and all consumer migration |
| Independent lane/usability acceptance | T100, T101 | T102 |
| Immutable 1.7.0 freeze | T086, T093–T101, then T102 | T103–T110 |
| TTHP standalone PASS | T103–T105 | T106–T110 |
| Workshop standalone PASS | T106–T107 | T108–T110 |
| Verification consumer PASS | T108–T109 | T110 |

## Parallel Execution Opportunities (safe only)

- Setup/Foundational: `T010` and `T011`; `T017`/`T018`; `T020`/`T021`/`T022`/`T023`.
- US1: command docs (`T027`–`T034`) can be parallelized in pairs after shared header policy is fixed.
- US5: stage 20/21/22/30/31 docs (`T051`–`T055`) and prompt generation checks (`T059`) can run in parallel after manifest cutover.
- US3: static learnings validators (`T078`,`T079`,`T081`) can proceed in parallel with card/index content updates (`T082`,`T083`).
- Release freeze: docs/metadata updates (`T094`,`T095`) can run parallel to
	traceability audit prep (`T098`) after core gates pass.

## Requirement Preservation Coverage (must remain complete)

FR families preserved exactly as authoritative totals:

- `FR-CMD-001`…`FR-CMD-009` (9)
- `FR-AGT-001`…`FR-AGT-013` (13)
- `FR-NS-001`…`FR-NS-007` (7)
- `FR-DEL-001`…`FR-DEL-010` (10)
- `FR-VER-001`…`FR-VER-020` (20)
- `FR-TRN-001`…`FR-TRN-008` (8)
- `FR-OPS-001`…`FR-OPS-006` (6)
- `FR-EVO-001`…`FR-EVO-005` (5)
- `FR-LRN-001`…`FR-LRN-013` (13)
- `FR-NOM-001` (1)
- `FR-MIG-001`…`FR-MIG-010` (10)

Total preserved FR count: **102**.

Success criteria preserved exactly:

- `SC-001`…`SC-020`

Total preserved SC count: **20**.

Coverage enforcement tasks:

- `T093`, `T096`, `T097`, `T098`, `T100`, and `T101` are mandatory release blockers.

## MVP suggestion (implementation order)

Recommended minimal high-value first slice for rapid confidence:

1. P0 canary (`T001`–`T008`)
2. Foundational (`T015`–`T026`)
3. US1 core delivery/readiness path (`T027`–`T048`)
4. US5 agent-surface correctness (`T049`–`T068`)
5. Release freeze (`T086`–`T102`)
6. US4 immediate deployment target subset for TTHP only (`T103`–`T105`)

This MVP aligns with the requested immediate deployment target while preserving strict stop/go gates and no-fallback semantics.

