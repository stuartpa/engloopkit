# Feature Specification: Ordered EngLoop v2

- **Feature ID:** SP001
- **Status:** READY FOR PLAN
- **Date:** 2026-07-10
- **Classification:** Maintainer-directed evolution refactor
- **Primary authority:** [REF001 — Ordered EngLoop v2 workflow](../../engloop/refactors/REF001_ordered-engloop-v2.md)

## Background and classification

EngLoopKit v1 works according to its documented contract. Dogfooding it while starting
TTHP and the EngLoopKit workshop exposed a better contract: the picker should teach the
workflow through lexical order, each repository should have one living direction,
functional behavior should be proven before unit tests influence coverage, scaffold
residue should be removed before it is protected, and accumulated learnings should be
cheap to retrieve without losing provenance.

This work is an **evolution refactor**, not an incident or direct patch:

- no operating system is disrupted and no emergency mitigation exists;
- manufacturing an incident would create false operational history;
- a direct rename is insufficient because command identity, artifact semantics, the
  executable stage machine, gate ownership, consumer installations, and learning
  retrieval all change together;
- delivery therefore follows the governed specification, architecture, implementation,
  model, exploration, validation, pruning, unit/property, and readiness sequence.

### Binding authority and carried-forward constraints

REF001 is binding wherever it changes the current workflow. The following existing
constraints remain binding:

- [ARC001](../../engloop/architecture/ARC001_bundle-composition.md): the EngLoopKit
  bundle composes capabilities; the single first-party extension owns EngLoopKit
  commands. Its old command count and namespace are superseded by this specification.
- [ARC002](../../engloop/architecture/ARC002_command-loop-contract.md): every command is
  a Trigger · Goal · Actions · Verification · Memory loop with an Artifact root note
  and a Done-when gate.
- [ARC003](../../engloop/architecture/ARC003_numbering-memory.md): numbered durable
  artifacts remain monotonic and never reused. Its SEED artifact rule is superseded;
  all other applicable numbering discipline remains.
- [ARC004](../../engloop/architecture/ARC004_executable-core.md): prose, executable
  stage rules, an independent behavioral model, exploration, generated conformance,
  and direct tests must agree.
- [ARC005](../../engloop/architecture/ARC005_component-pattern.md): generic,
  domain-free components remain physically separate from the domain vertical, and
  dependencies point from vertical to components.

The Learnings Pyramid was consulted as required:

- [Readiness is a gate](../../engloop/learnings/cards/readiness-is-a-gate.md), sourced
  from `PM001/LRN001`–`PM001/LRN003`: readiness is computed from a complete inventory,
  never narrated.
- [Verification follows artifact class](../../engloop/learnings/cards/verification-follows-artifact-class.md),
  sourced from `PM002/LRN001`–`PM002/LRN003`: components use unit/property evidence;
  the domain vertical uses behavioral evidence without lowering the common bar.
- [Model observable behavior, not internal shape](../../engloop/learnings/cards/model-observable-behavior.md),
  sourced from `PM002/LRN002` and `PM003/LRN001`–`PM003/LRN002`: model representative
  end-to-end behavior against the real SUT, not every internal assembly.
- [Adequate models prove rejection](../../engloop/learnings/cards/adequate-models-prove-rejection.md),
  sourced from `PM004/LRN001`–`PM004/LRN003`: require materially branching behavior and
  model-derived negative conformance, not positive-only or hand-coded theatre.

## Goals

1. Make the complete EngLoop workflow visible in process order through 13 exact command
   identities while retaining the EngLoopKit product and package identity.
2. Replace numbered SEED snapshots with one root-level living `NORTHSTAR.md` whose
   revisions are retained by Git.
3. Separate rapid scaffold learning, architecture derivation, governed final-form work,
   behavioral modelling, exploration, functional validation, dead-code disposition,
   unit/property verification, and final readiness ownership.
4. Preserve the strict incident → post-mortem → repair semantics and route permanent
   repairs through the full governed and verified loop.
5. Add a loss-aware Learnings Pyramid from chronological source learnings to living
   cards, a one-page index, and on-demand agent retrieval.
6. Migrate EngLoopKit and its three current consumers cleanly, with no compatibility
   aliases or stale generated command surfaces.
7. Preserve standalone repository usability while providing focused single-root
   workspace entry points for routine work.

## Command surface and lexical order

The product, repository, bundle, extension, and package remain **EngLoopKit** with
extension/package identifier **`engloopkit`**. The v2 command namespace is
**`speckit.engloop`**.

The shipped command set MUST contain exactly these 13 IDs. Ordinal lexical sorting of
full IDs MUST produce this exact order:

| Order | Exact command ID | Responsibility |
|---:|---|---|
| 01 | `speckit.engloop.01-northstar` | Create or evolve the repository's one living direction document. |
| 02 | `speckit.engloop.02-scaffold` | Create a thin working slice and prove the selected test runway. |
| 03 | `speckit.engloop.03-architect` | Derive and govern long-lived architecture from the running scaffold. |
| 04 | `speckit.engloop.04-refactor` | Run governed specify → plan → tasks → implement to reach architecture-conformant product code. |
| 05 | `speckit.engloop.05-model` | Define the domain vertical's behavioral model. |
| 06 | `speckit.engloop.06-explore` | Explore behavior and generate functional tests into the proven runway. |
| 07 | `speckit.engloop.07-validate` | Run generated functional tests against the real SUT and produce reachability evidence. |
| 08 | `speckit.engloop.08-unittest` | Classify/delete scaffold residue, revalidate, add unit/property tests, and compute readiness. |
| 20 | `speckit.engloop.20-incident` | Stabilize an operating disruption using mitigations only. |
| 21 | `speckit.engloop.21-postmortem` | Analyze stabilized incident sets into source learnings and repair items. |
| 22 | `speckit.engloop.22-repair` | Route permanent repairs through Stages 04 and 05–08. |
| 30 | `speckit.engloop.30-refactor-scan` | Select and record the single highest-value periodic evolution refactor. |
| 31 | `speckit.engloop.31-learnings-pyramid` | Condense accepted source learnings into traceable, retrievable living memory. |

No `speckit.engloopkit.*` command may remain in a current v2 manifest, command package,
registration, generated agent, generated prompt, or compatibility surface. There are
no aliases, redirects, deprecation shims, or silent translations. Historical records
may retain old IDs as historical evidence.

The number bands are distinct invocation lanes, not one mandatory automatic sequence:

- 01–08 is the ordered delivery/readiness lane;
- 20–22 is the on-demand operations lane, entered only for an incident, a warranted
  post-mortem over stabilized incidents, and resulting repair items;
- 30–31 is the spare-capacity stewardship lane, generally invoked when an engineer has
  spare time or spare agent tokens, especially near month-end.

Completing a lower-numbered lane does not manufacture work in a higher-numbered lane.

## User scenarios and independent acceptance

Each story can be tested from a prepared repository fixture that satisfies only its
stated preconditions; acceptance does not depend on running unrelated stage groups.

### User Story 1 — Ordered delivery and verification, Stages 01–08 (Priority: P1)

As a product maintainer, I want an explicit sequence from durable direction through a
thin scaffold, governed architecture, behavioral proof, pruning, and final readiness so
that fast learning cannot be mistaken for production readiness.

**Independent value:** A new standalone repository can demonstrate the complete
01–08 contract without any incident, post-mortem, refactor scan, or existing learning
history.

**Acceptance scenarios:**

1. **Northstar creation and evolution**
   - **Given** a governed repository has no `NORTHSTAR.md`, **when** Stage 01 completes,
     **then** exactly one root `NORTHSTAR.md` exists and states why/for whom, enduring
     outcomes and invariants, direction, more-of, less-of/stop, boundaries, and genuine
     unresolved questions.
   - **Given** the file already exists, **when** new evidence changes direction,
     **then** Stage 01 updates the same file and Git retains prior revisions.
   - **Given** only a routine feature or refactor completed, **when** Stage 01 is
     considered, **then** the Northstar is not churned.
2. **Thin scaffold and test runway**
   - **Given** a Northstar, **when** Stage 02 completes, **then** a thin behavior works
     end to end through the actual product boundary and the chosen test framework has
     demonstrably built, discovered, passed, and reported an intentional failure.
   - **Then** the intentional failure is removed, the same terse command passes, and
     the framework choice, command, evidence, and generated-test destination are
     durably recorded.
   - **When** any proof step fails, **then** Stage 02 fails visibly; it does not switch
     framework, command, project, data source, or success criterion silently.
3. **Architecture then governed refactor**
   - **Given** the scaffold runs, **when** Stage 03 completes, **then** architecture is
     derived from observed working evidence, the component/vertical boundary is
     governed, and scaffold compromises are recorded rather than blessed as final.
   - **When** Stage 04 runs, **then** accepted work passes through
     specify → plan → tasks → implement under that architecture before entering Stage
     05.
4. **Model, explore, and validate**
   - **Given** architecture-conformant product code, **when** Stages 05–07 complete,
     **then** SEK-generated functional tests include legal success, model-derived
     illegal-order rejection, and model-derived invalid-input rejection; explored
     paths are materially distinct; all tests run against the real SUT through the
     Stage 02 destination; and Stage 07 publishes generated-suite-only reachability.
   - **Then** Stage 07 reports functional evidence only and makes no final readiness
     claim.
5. **Delete before unit testing and compute readiness**
   - **Given** Stage 07 reachability, **when** Stage 08 finds an unreached path, **then**
     it classifies the path before any deletion or new unit test.
   - **When** the path has authoritative intent, **then** work returns through
     05 → 06 → 07.
   - **When** the path has neither authoritative intent nor a runtime entry mechanism,
     **then** it is removed, Stage 07 is rerun, and only a green revalidation permits
     Stage 08 to continue.
   - **Then** unit/property tests are added only after all paths are classified; every
     surviving module reaches at least 95% line and branch coverage; and Stage 08 alone
     emits the complete readiness inventory and PASS/FAIL verdict.

### User Story 2 — Strict operations and permanent repair, Stages 20–22 (Priority: P1)

As an operator and maintainer, I want stabilization, analysis, and permanent repair to
remain distinct so that emergency pressure cannot disguise a mitigation as a verified
fix.

**Independent value:** A fixture with a prior Stage 08 PASS and a simulated operating
disruption can demonstrate the complete 20–22 contract without running Stage 30 or 31.

**Acceptance scenarios:**

1. **Given** no incident exists, **when** a current Stage 08 PASS is produced, **then**
  no Stage 20, 21, or 22 work is created merely because the product is authorized to
  operate.
2. **Given** Stage 08 has not produced a current PASS, **when** entry to operations is
   requested, **then** the transition is rejected with the missing/failing gate rows.
3. **Given** an operating disruption after PASS, **when** Stage 20 runs, **then** it
   creates an incident record, numbers every stabilization action as a mitigation,
   restores and verifies service, and commits no permanent repair as incident work.
4. **Given** one or more stabilized incidents selected for review, **when** Stage 21
  runs, **then** it
   performs systemic analysis over the selected set and emits globally addressable
   `PMxxx/LRNxxx` source learnings plus concrete `RPIxxx` repair items.
5. **Given** a repair item, **when** Stage 22 runs, **then** the repair re-enters Stage
   04 and all applicable Stages 05–08; it cannot bypass the governed loop because it is
   small, and it remains open until source, release, target verification, and a current
   readiness PASS prove the permanent fix.
6. **Given** Stage 21 accepts new learnings, **then** it records a pending Stage 31
   refresh obligation without delaying stabilization or allowing that obligation to
   replace Stage 22.

### User Story 3 — Evolution and loss-aware learning, Stages 30–31 (Priority: P2)

As a long-term maintainer, I want periodic refactoring and learning condensation to
improve the product without manufacturing work, churning direction, or forgetting the
source evidence.

**Independent value:** A stable fixture with architecture, history, and accepted
post-mortem learnings can demonstrate Stages 30–31 without creating an incident or a
new product scaffold.

**Acceptance scenarios:**

1. **Given** useful evolution signals and spare engineering or agent-token capacity,
  especially near month-end, **when** Stage 30 completes, **then** it records
   exactly one numbered REF decision for the highest-value branch, or an explicit
   no-refactor decision, and creates no SEED.
2. **Given** the selected refactor does not change repository direction, **then** the
   Northstar remains unchanged and delivery normally enters Stage 04.
3. **Given** evidence genuinely changes direction, **then** Stage 30 requires Stage 01
   to update the same root Northstar before governed delivery continues.
4. **Given** an accepted-learning backlog and spare engineering or agent-token
  capacity, **when** Stage 31 completes, **then** every
   `PMxxx/LRNxxx` source is represented by at least one living subject card, every card
   has valid provenance and appears in root `LEARNINGS.md`, all links resolve, the page
   is within its ratified budget, and clean-context retrieval locates the correct card
   and source for the sampled questions.
5. **Given** an omission, broken link, oversize page, conflict hidden by condensation,
   or retrieval miss, **then** Stage 31 fails and repeats generation/condensation/
   recreation rather than claiming completion.

### User Story 4 — Ordered picker and standalone consumers (Priority: P1)

As a user working in one repository, I want the picker to show one ordered EngLoop v2
workflow and every command to work from that repository alone, without depending on a
parent mega-workspace.

**Independent value:** Each consumer can be opened and tested alone after a clean v2
installation.

**Acceptance scenarios:**

1. **Given** a clean single-root consumer workspace, **when** its picker is opened,
   **then** exactly 13 EngLoop commands appear once, in the lexical order listed above,
   and no old ID appears.
2. **Given** `tthp`, `engloop-workshop`, or `VerifyExtremeEdgeWithTpcc` is opened alone,
   **when** any applicable v2 command is invoked, **then** it resolves its local
   configuration, artifact root, memory, and generated-test destination without a
   sibling repository or parent-local installation.
3. **Given** the aggregate mega-workspace is opened, **then** independently installed
   commands may appear once per root; documentation identifies that view as an
   integration workspace and directs routine users to focused one-root entry points
   rather than coupling the repositories to deduplicate registrations.

## Stage ownership and transition contract

Every transition fails closed when its owned evidence is absent, stale, ambiguous, or
failing. Numeric order communicates the normal flow; it does not permit gate bypasses.

| Stage | Owned completion evidence | It does **not** own | Required next behavior |
|---|---|---|---|
| 01 Northstar | Singleton, complete living direction; justified create/update | Feature completion or architecture | New product → 02; existing direction change → 03 if architecture must be re-derived, otherwise 04 |
| 02 Scaffold | Thin working slice and fully proven test runway | Final architecture or readiness | 03 |
| 03 Architect | Architecture derived from running evidence; governed boundaries/contracts | Final-form implementation | 04 |
| 04 Refactor | Accepted specify/plan/tasks/implement outputs, green build, architecture conformance | Behavioral adequacy or readiness | 05 |
| 05 Model | Faithful behavior-level model with state, actions, outcomes, guards, invariants, and rejection semantics | Generated tests or SUT conformance | 06 |
| 06 Explore | Bounded, branching exploration and generated functional suite in the Stage 02 destination | Real-SUT pass or readiness | 07; model deficiency → 05 |
| 07 Validate | Positive and negative real-SUT conformance plus generated-suite-only functional reachability | Dead-code verdict, unit coverage, or final readiness | Functional gap → 05/06; SUT defect → 04; valid evidence → 08 |
| 08 Unit test | Reachability disposition, post-deletion revalidation, unit/property evidence, complete module inventory, final gate | Operational analysis | Intended gap → 05; deletion set → 07; design defect → 04; PASS enables 20/30 and steady-state 31 |
| 20 Incident | Verified stabilization and mitigation audit trail | Permanent fix | Another incident or 21 |
| 21 Post-mortem | Systemic cause analysis, accepted source learnings, actionable repair items | Repair implementation or readiness | 22; new learnings also create an independent 31 refresh obligation |
| 22 Repair | Traceable routing and closure evidence across 04 and applicable 05–08 plus target verification | A small-change bypass | 04; repair closes only after downstream evidence passes |
| 30 Refactor scan | One justified REF decision (including an explicit no-work result) | SEED or routine Northstar rewrite | Normally 04; genuine direction change → 01; no work → remain steady |
| 31 Learnings Pyramid | Complete, linked, bounded, retrieval-tested condensation | Source rewriting, repair, or readiness | Clear the refresh obligation and return to the invoking steady/operations context |

### Required transition behavior

1. The normal new-product path is
   `01 → 02 → 03 → 04 → 05 → 06 → 07 → 08`.
2. Stage 07 may return to 04 for a SUT defect, 05 for a model/fidelity gap, or 06 for an
   exploration/generation gap.
3. Stage 08 may return to 05 for intended but functionally unreached behavior and MUST
   return to 07 after deleting each coherent residue set. A returned Stage 07 run must
   complete before Stage 08 resumes.
4. A current Stage 08 PASS authorizes operations but invokes nothing by itself. When
  an incident actually occurs, the on-demand path is
  `20 → 21 → 22 → 04 → 05 → 06 → 07 → 08`; Stage 21 waits for a selected stabilized
  incident set, Stage 22 waits for repair items, and repeated Stage 20 incidents may
  accumulate before review.
5. Spare engineering or agent-token capacity, commonly near month-end, may invoke
  Stage 30. It normally enters 04. A real direction change inserts Stage 01 first; a
   resulting architecture impact also requires Stage 03. A no-work REF changes no
   product stage.
6. Stage 31 is an independent, opportunistic maintenance obligation over accepted
  learning memory. New source learnings make it pending; spare engineering or
  agent-token capacity normally services that backlog. It neither blocks incident
  stabilization nor bypasses repair, does not require Stage 30 to have run, and has a
  PASS/FAIL state separate from product readiness.
7. The executable core and independent model MUST reject every transition that skips
   an owned gate, including 02→04, 04→07, 07→20, 21→04, 22→08, and 30→08 as a claimed
   completed refactor.
8. Transition state MUST retain enough information to distinguish at least current
   stage, readiness evidence currency, pending repair verification, pending learning
   refresh, and Stage 08 reachability disposition; a stage-name-only covering script
   is not adequate v2 behavioral proof.

## Northstar contract

Every governed repository has exactly one root-level `NORTHSTAR.md`. It is living
memory, not a numbered snapshot. It contains:

- why the repository exists and for whom;
- enduring outcomes and non-negotiable invariants;
- current product direction;
- what the repository should do **more of** over time;
- what it should do **less of** or stop doing;
- current boundaries and meaningful unresolved direction questions.

Stage 01 creates it when absent and evolves the same file only when evidence changes
direction. Git is its revision history. v2 has no current SEED command, artifact class,
prefix, counter, template, required `seeds/` directory, or duplicate live seed file.
Migration moves and evolves useful seed content into the root Northstar; deletion from
the current tree does not erase prior forms from Git.

Multiple candidate Northstars, an untraceable direction rewrite, or uncertainty over
which document is authoritative causes Stage 01 to fail rather than merge or choose
silently.

## Scaffold and architecture contract

Stage 02 optimizes for executable learning, not final design. Its gate requires all of
the following evidence from one explicitly selected test framework:

1. installation/configuration is explicit;
2. product and test artifacts build;
3. at least one meaningful test crosses the actual product boundary;
4. the selected runner discovers that test;
5. the test passes for the expected behavior;
6. a controlled intentional defect or expectation proves that the same runner reports
   failure;
7. the intentional failure is reverted and the same terse command passes again;
8. the exact terse command and the contract for the Stage 06 generated-test project or
   location are recorded in durable repository memory.

A failed runway may be replaced only by an explicit, recorded framework decision and a
fresh proof of every step. There is no hidden fallback to a different runner, project,
command, provider, stale output, or weaker assertion.

Stage 03 derives durable boundaries and contracts from the running slice, including the
component/vertical classification. Stage 04, not Stage 02, owns the full governed
specify → plan → tasks → implement sequence and convergence from temporary scaffold
choices to architecture-conformant product code.

## Behavioral verification and readiness contract

### Stages 05–07: functional proof

- Stage 05 models representative observable behavior of the stateful domain vertical,
  including interacting state, actions, effects, legal guards, invalid input, expected
  success/rejection outcomes, invariants, and real ordering constraints.
- Generic/domain-free components are extracted before behavioral modelling and do not
  receive ceremonial models. Their unit/property verification occurs in Stage 08 only
  after reachability disposition. Internal pipeline assemblies do not each require a
  model when one representative end-to-end model exercises the real vertical pipeline.
- Stage 06 explores materially distinct paths and generates functional tests into the
  exact destination proven at Stage 02. Positive-only covering tours are insufficient.
- At minimum, the generated suite contains legal success, an illegal-order attempt with
  the modelled rejection, and an invalid-input attempt with the modelled rejection.
  Every modeled precondition class used by a required behavior is represented.
- Rejection expectations are generated from model semantics. A hand-written assertion
  hidden inside an always-enabled positive action is a gate failure.
- Stage 07 runs only the generated functional suite against the real SUT boundary and
  records green/red conformance, explored branching, and a map of reached and unreached
  production paths. Mocks or stateless replay that bypass the behavior under test do not
  satisfy this requirement.
- Stage 07 never pads reachability with hand-written unit tests and never emits a final
  readiness verdict.

### Stage 08: exact delete-before-unit-test algorithm

Stage 08 MUST execute this order without shortcuts:

1. **Observe Stage 07 reachability.** Use coverage/reachability produced by the current
   SEK-generated functional suite alone. Do not add new unit tests yet.
2. **Classify every unreached production path.** Non-reachability initiates
   investigation; it is not automatic proof of dead code.
3. **Return intended gaps.** A path is intended when supported by the Northstar,
   accepted specification, architecture, public contract, configured capability,
   authoritative platform entry point, serialization/reflection contract, or required
   error/recovery behavior. Return it through `05 → 06 → 07`; do not preserve it by
   writing a unit test around behavior that lacks functional proof.
4. **Delete unsupported residue.** A path with no authoritative requirement and no
   runtime entry mechanism is scaffold residue and is removed.
5. **Re-prove after deletion.** Build, run architecture checks, and rerun Stage 07 after
   each coherent deletion set. Any regression blocks further Stage 08 work and routes
   to the stage matching the defect.
6. **Repeat to full disposition.** Every surviving path is functionally justified or
   has an explicit reviewed reason why only direct unit/property verification can
   reach it.
7. **Only then add unit/property tests.** Test surviving units according to artifact
   class and close legitimate direct-only gaps. Unit tests may not create a reason to
   retain unsupported code or substitute for required functional behavior.
8. **Compute final readiness.** Build a complete per-module Readiness Inventory from
   current Stage 07 evidence, architecture conformance, dead-code disposition,
   unit/property results, measured line and branch coverage, and all regression gates.
   Stage 08 emits PASS only when every row passes.

The current ratified threshold is **at least 95% line and at least 95% branch coverage
for every surviving module**. A lower value, an aggregate that hides a weak module, or
a rationalized shortfall cannot PASS unless a later ratified policy explicitly changes
the threshold.

Verification method follows module class while the quality bar remains common:

- a generic component or pure value-type unit uses direct unit/property evidence;
- a stateful domain vertical requires the Stage 05–07 behavior-level, real-SUT,
  branching, positive-and-negative generated evidence, with direct tests only for
  justified surviving units;
- generic code left in the vertical is an architecture failure, not an alternative
  verification route.

A missing module, zero-evidence module, stale Stage 07 run, unclassified path, ambiguous
runtime entry, unproven negative outcome, hidden fallback, or failing regression row is
an explicit FAIL. The only honest status without a complete PASS is **NOT READY**.

## Operations and evolution contract

### Incident, post-mortem, and repair

- Stages 20–22 are invoked on demand, not scheduled after Stage 08. Stage 20 requires
  an actual incident; Stage 21 requires a deliberately selected set of stabilized
  incidents; Stage 22 requires one or more resulting repair items.
- Stage 20 follows the Golden Rule: a live action that stabilizes service is a
  mitigation, not a permanent fix. It is numbered and audited in the incident; source
  repair is deferred.
- Stage 21 analyzes one or more stabilized incidents to systemic cause depth. It emits
  class-level accepted learnings and concrete repair items capable of preventing the
  failure class mechanically.
- Stage 22 does not retain the v1 small-change escape route. Every permanent repair
  re-enters Stage 04 and all applicable Stages 05–08. It is complete only when the
  change is in source, represented in release artifacts, applied to the target, and
  verified by all required evidence including a current Stage 08 PASS.

### Refactor scan

When spare engineering or agent-token capacity is available—especially near
month-end—Stage 30 evaluates accumulated operational, architecture, reachability,
coverage, complexity, duplication, and component-leakage signals in priority order.
It records one numbered REF for the single highest-value warranted refactor, or records
that no refactor is warranted. It produces **REF only**, never a SEED. A selected
refactor normally enters Stage 04. `NORTHSTAR.md` changes only when the evidence
changes repository direction, not for routine cleanup or completion.

## Learnings Pyramid contract

The exact durable compression graph is:

`chronological PMxxx/LRNxxx sequence → living subject cards → root LEARNINGS.md → on-demand instruction/retrieval`

Retrieval drills back down as:

`.github/instructions/project-learnings.instructions.md → LEARNINGS.md → relevant card → PMxxx/LRNxxx source`

Stage 31 is normally invoked when accepted learnings have accumulated and spare
engineering or agent-token capacity is available, particularly near month-end. It is
not an automatic successor to Stage 30, Stage 21, or Stage 22; those stages only leave
a visible refresh obligation when relevant.

### Layer 1 — authoritative source-learning sequence

- The source set is every accepted learning in chronological post-mortems.
- A source identity is globally addressable as `PMxxx/LRNxxx`; `LRN` numbering remains
  local to its PM.
- Condensation never replaces, edits, deletes, or severs source learnings.

### Layer 2 — living subject cards

Cards live under `<ARTIFACT_ROOT>/learnings/cards/<subject>.md`. Each card focuses on
one coherent subject and contains a terse subject/recall cue, compressed plain-language
principle, applicability, operational checks, links to every contributing
`PMxxx/LRNxxx`, and visible conflicts, supersessions, or unresolved tension. Git retains
card evolution. Cards do not copy whole post-mortems.

The relationship is many-to-many: one card may use many sources, and one source may
inform multiple cards. Every accepted source appears on at least one card, and every
card cites at least one valid accepted source.

### Layer 3 — one-page root index

Root `LEARNINGS.md` is a genuinely compressed map, with one terse cue per current card,
organization that makes subject relationships visible, and a resolving link for every
cue. It does not duplicate card explanations or source narratives. Every current card
appears exactly as an index target, and every page → card → source link resolves.

Stage 31 cannot PASS unless the index contains no more than **500 words and 60
nonblank lines**. Both deterministic limits must pass.

### Layer 4 — on-demand retrieval instruction

Stage 31 creates or updates
`.github/instructions/project-learnings.instructions.md`. Its discovery description is
keyword-rich for architecture, scaffold, refactor, model, exploration, validation,
unit-test, readiness, incident, repair, and evolution decisions. Its body tells an
agent to read root `LEARNINGS.md`, follow only relevant cards, and inspect cited source
learnings before a consequential decision.

The instruction does not duplicate the pyramid, has no `applyTo: "**"`, and is not
registered as a command, custom agent, or picker item.

### Stage 31 gate

Deterministic checks MUST prove all of the following:

1. 100% of accepted source IDs are referenced by at least one card;
2. 100% of cards cite at least one valid source ID;
3. 100% of current cards are linked by root `LEARNINGS.md`;
4. 100% of page → card → source links resolve to the claimed content;
5. the index is within the ratified numeric one-page budget;
6. a clean-context retrieval sample covering every card and at least one source from
   every PM locates the correct card and source with no false provenance;
7. omissions, conflicts, or retrieval misses cause another condense/recreate cycle.

## Domain entities and artifacts

| Entity/artifact | Meaning and required relationships |
|---|---|
| EngLoop command | One exact ordered invocation surface conforming to ARC002's loop contract. |
| Northstar | The singleton root living direction; Git owns revision history. |
| Test runway record | Stage 02's selected framework, boundary test, build/discovery/pass/fail/re-pass evidence, terse command, and generated-test destination. |
| Scaffold | A thin executable learning slice; never evidence of final architecture or readiness. |
| ARC / architecture constitution | Long-lived boundary, ownership, and contract derived from running evidence. |
| SP delivery set | Stage 04's accepted specification, plan, tasks, and governed implementation evidence. |
| Module | A complete inventory unit classified as generic component, pure value-type unit, or domain vertical. |
| MDL | Human-readable and executable description of behavioral state, actions, outcomes, guards, and invariants. |
| CRD | Bounded exploration scenarios, constraints, goals, and graph/path evidence. |
| Generated functional suite | SEK-derived positive and negative tests written to the Stage 02 destination. |
| Functional validation record | Stage 07 real-SUT results, model/exploration identity, branching evidence, and generated-suite-only reachability map. |
| Reachability disposition | Stage 08 decision for every unreached path: intended gap, unsupported residue, or reviewed direct-only survivor, with authority/evidence. |
| Readiness Inventory | Stage 08 row for every surviving module: class, applicable behavioral/direct evidence, line/branch measures, architecture, regressions, and verdict. |
| Readiness Gate | Deterministic Stage 08 PASS/FAIL over the complete inventory; the sole source of readiness. |
| IN / MIT | Operating disruption and its temporary stabilization actions. |
| PM / LRN / RPI | Systemic analysis, accepted source learning identified as `PMxxx/LRNxxx`, and permanent repair item. |
| REF | Stage 30's single evolution decision; it never creates a SEED. |
| Subject card | Living subject condensation with many-to-many source provenance. |
| Learnings index | Root one-page cue map linking every current card. |
| Retrieval instruction | On-demand discovery entry point that loads the pyramid progressively without picker clutter. |
| Consumer installation | One root's self-contained v2 extension registration and generated invocation surfaces. |
| Focused workspace entry point | A routine-work view containing exactly one consumer root. |

## Functional requirements

### Command and architecture requirements

- **FR-CMD-001:** The product/bundle/extension/package identity MUST remain
  `engloopkit` / EngLoopKit.
- **FR-CMD-002:** The extension MUST expose exactly the 13 command IDs in the command
  table and no other `speckit.engloop.*` command.
- **FR-CMD-003:** Full-ID lexical ordering MUST equal numeric process ordering
  01–08, 20–22, 30–31.
- **FR-CMD-004:** Current v2 install and package surfaces MUST contain zero
  `speckit.engloopkit.*` registrations or aliases.
- **FR-CMD-005:** Every command MUST satisfy ARC002's frontmatter, Trigger, Goal,
  Actions, Verification, Memory, Artifact root, and Done-when contract.
- **FR-CMD-006:** The bundle MUST remain composition-only and all first-party EngLoop
  command logic MUST remain owned by the single `engloopkit` extension.
- **FR-CMD-007:** Prose, manifests, catalog/release metadata, executable stages,
  independent model, exploration, generated tests, and direct tests MUST agree on the
  same 13-stage contract.
- **FR-CMD-008:** Missing or ambiguous command identity or stage evidence MUST fail
  explicitly; no fallback identity, stale artifact, alternate path, or guessed default
  may mask it.

### Northstar requirements

- **FR-NS-001:** Every governed repository MUST have exactly one root
  `NORTHSTAR.md`.
- **FR-NS-002:** The Northstar MUST contain purpose/audience, enduring outcomes and
  invariants, direction, more-of, less-of/stop, boundaries, and unresolved direction
  questions.
- **FR-NS-003:** Stage 01 MUST create the file when absent and update the same file
  only when evidence changes direction.
- **FR-NS-004:** Git MUST be the history mechanism; prior current-tree snapshots MUST
  not be retained as duplicate live direction artifacts.
- **FR-NS-005:** Current v2 artifacts MUST contain no SEED command, prefix, counter,
  template, required directory, or live seed document.
- **FR-NS-006:** A routine feature, repair, or refactor that preserves direction MUST
  not rewrite the Northstar.
- **FR-NS-007:** Multiple or ambiguous direction documents MUST fail Stage 01 closed.

### Delivery requirements

- **FR-DEL-001:** Stage 02 MUST produce a thin, working end-to-end slice from the
  Northstar without claiming final architecture.
- **FR-DEL-002:** Stage 02 MUST explicitly select one repository-appropriate test
  framework and record the selection.
- **FR-DEL-003:** Stage 02 MUST prove build, discovery, pass, intentional-failure
  reporting, restoration, and re-pass using the same terse test command.
- **FR-DEL-004:** The runway proof MUST include a meaningful test through the actual
  product boundary.
- **FR-DEL-005:** Stage 02 MUST record the exact terse command and generated-test
  destination contract consumed by Stage 06.
- **FR-DEL-006:** A failed runway MUST fail visibly; changing the selection requires
  an explicit decision and complete re-proof.
- **FR-DEL-007:** Stage 03 MUST derive architecture from the running scaffold and
  govern the component/vertical boundary and other long-lived contracts.
- **FR-DEL-008:** Stage 04 MUST execute the governed
  specify → plan → tasks → implement sequence for final-form work.
- **FR-DEL-009:** Stage 04 MUST reject work that conflicts with accepted architecture
  or bypasses an accepted task/specification.
- **FR-DEL-010:** Scaffold shortcuts MUST be replaced, justified, or removed before
  Stage 04 completes; their mere operation is not architectural evidence.

### Verification and readiness requirements

- **FR-VER-001:** Stage 05 MUST model observable stateful domain behavior at
  representative end-to-end granularity against a real SUT boundary.
- **FR-VER-002:** The model MUST express interacting state, legal actions, invalid
  inputs, ordering guards, expected success/rejection outcomes, effects, and
  invariants sufficient for materially distinct paths.
- **FR-VER-003:** Generic/domain-free components MUST be extracted before behavioral
  modelling, excluded from ceremonial MDL/CRD artifacts, and verified by unit/property
  tests only in Stage 08 after full reachability disposition.
- **FR-VER-004:** Stage 06 MUST use the Stage 05 model to produce bounded exploration
  and SEK-generated functional tests in the Stage 02 destination.
- **FR-VER-005:** Generated evidence MUST include positive conformance plus
  model-derived illegal-order and invalid-input negative conformance.
- **FR-VER-006:** A human-authored error assertion embedded in an always-enabled
  positive action MUST fail model-adequacy review.
- **FR-VER-007:** Exploration MUST show non-trivial branching and materially distinct
  paths rather than a flat covering tour.
- **FR-VER-008:** Stage 07 MUST run generated tests against the real SUT and record
  functional conformance and generated-suite-only reachability.
- **FR-VER-009:** Stage 07 MUST NOT use hand-written unit tests to inflate functional
  reachability or emit a readiness verdict.
- **FR-VER-010:** Stage 08 MUST classify 100% of unreached production paths before
  adding unit/property tests.
- **FR-VER-011:** Functional non-reachability MUST trigger classification, not
  automatic deletion.
- **FR-VER-012:** Intended unreached behavior MUST return through 05 → 06 → 07 based
  on authoritative intent; a unit test cannot substitute.
- **FR-VER-013:** Unsupported scaffold residue with no authoritative requirement or
  runtime entry mechanism MUST be removed.
- **FR-VER-014:** Every coherent deletion set MUST be followed by build,
  architecture checks, and a complete Stage 07 rerun before Stage 08 resumes.
- **FR-VER-015:** Unit/property tests MUST be added only after full reachability
  disposition and MUST protect only surviving justified units.
- **FR-VER-016:** Every surviving module MUST measure at least 95% line and 95% branch
  coverage unless a later ratified policy changes the threshold.
- **FR-VER-017:** Stage 08 MUST inventory every module, including zero-evidence rows,
  and apply the verification method appropriate to its class.
- **FR-VER-018:** Stage 08 MUST combine current functional evidence, architecture,
  dead-code disposition, direct tests, measured per-module coverage, and green
  regression gates into the sole final PASS/FAIL verdict.
- **FR-VER-019:** Any missing, stale, ambiguous, below-threshold, non-conformant, or
  red row MUST produce FAIL/NOT READY and name the blocking evidence.
- **FR-VER-020:** The executable model and generated conformance suite MUST themselves
  demonstrate the v2 positive, negative, branching, real-SUT, and transition-gate
  requirements; hand-written tests alone are insufficient.

### Transition requirements

- **FR-TRN-001:** The executable transition contract MUST implement the normal
  01→02→03→04→05→06→07→08 path and all feedback paths defined above.
- **FR-TRN-002:** Operations entry MUST require a current Stage 08 PASS.
- **FR-TRN-003:** Repair MUST re-enter Stage 04 and applicable 05–08 gates and retain
  an open repair obligation until downstream and target evidence passes.
- **FR-TRN-004:** Refactor scan MUST normally enter Stage 04, inserting Stage 01 only
  for direction change and Stage 03 when architecture must be re-derived.
- **FR-TRN-005:** New accepted learnings MUST create a separately tracked Stage 31
  refresh obligation without satisfying or blocking repair.
- **FR-TRN-006:** Illegal ordering, invalid input, gate bypass, duplicate start, and
  stale-evidence transition attempts MUST be rejected with an actionable reason.
- **FR-TRN-007:** Stage/gate state MUST be rich enough to model readiness currency,
  repair obligations, learning-refresh obligations, and reachability disposition, not
  only the current stage label.
- **FR-TRN-008:** Numeric bands MUST be interpreted as separate invocation lanes;
  completion of 08 MUST NOT automatically invoke 20, and completion of any delivery or
  operations stage MUST NOT automatically invoke 30 or 31.

### Operations requirements

- **FR-OPS-001:** Stage 20 MUST distinguish temporary mitigation from permanent fix
  and MUST prohibit permanent source repair as incident stabilization work.
- **FR-OPS-002:** Stage 20 MUST retain an auditable incident timeline and locally
  numbered mitigations and close stabilization only on verified recovery.
- **FR-OPS-003:** Stage 21 MUST analyze a selected set of stabilized incidents and
  produce class-level `PMxxx/LRNxxx` learnings and actionable `RPIxxx` repairs.
- **FR-OPS-004:** Stage 22 MUST route every permanent repair through Stage 04 and all
  applicable 05–08 evidence, regardless of apparent size.
- **FR-OPS-005:** A repair MUST not be called fixed until source, built release,
  target application, target verification, and readiness evidence all pass.
- **FR-OPS-006:** Stage 20 MUST require an actual incident, Stage 21 MUST require a
  selected stabilized incident set, and Stage 22 MUST require resulting repair items;
  absent demand MUST create no placeholder operations work.

### Evolution and learning requirements

- **FR-EVO-001:** Stage 30 MUST evaluate evidence in a stable, documented priority
  order and select at most one highest-value refactor per run.
- **FR-EVO-002:** Every completed scan MUST create one numbered REF decision,
  including a justified no-refactor result, and MUST create no SEED.
- **FR-EVO-003:** A selected refactor MUST normally enter Stage 04; no-work leaves the
  product unchanged.
- **FR-EVO-004:** Stage 30 MUST request a Northstar update only for evidence-backed
  direction change.
- **FR-EVO-005:** Stages 30 and 31 MUST support opportunistic invocation when spare
  engineering or agent-token capacity is available, commonly near month-end, and MUST
  remain independently invocable rather than forming an automatic pair.
- **FR-LRN-001:** Stage 31 MUST treat the chronological accepted `PMxxx/LRNxxx`
  sequence as immutable source memory.
- **FR-LRN-002:** Every source MUST be globally identified by its PM/LRN pair even
  though LRN numbering resets per PM.
- **FR-LRN-003:** Cards MUST live by subject under
  `<ARTIFACT_ROOT>/learnings/cards/` and satisfy the card content contract.
- **FR-LRN-004:** One source MAY inform multiple cards and one card MAY cite multiple
  sources.
- **FR-LRN-005:** Every accepted source MUST appear on at least one card and every card
  MUST cite at least one valid source.
- **FR-LRN-006:** Cards MUST expose conflicts, supersessions, and unresolved tension
  instead of silently choosing a consensus.
- **FR-LRN-007:** Root `LEARNINGS.md` MUST contain one terse linked cue for every
  current card, show useful relationships, and avoid duplicating card/source detail.
- **FR-LRN-008:** All page → card → source links MUST resolve to the claimed IDs and
  content.
- **FR-LRN-009:** The root index MUST contain no more than 500 words and 60 nonblank
  lines; either limit being exceeded MUST fail Stage 31.
- **FR-LRN-010:** The on-demand instruction MUST exist at the exact required path,
  carry the required discovery concepts, and direct progressive page → card → source
  retrieval.
- **FR-LRN-011:** The instruction MUST have no `applyTo: "**"` and MUST not register or
  appear in the command/agent picker.
- **FR-LRN-012:** A clean-context retrieval exercise MUST cover every card and at least
  one source from every PM and MUST locate the correct card and source.
- **FR-LRN-013:** Any completeness, provenance, link, size, conflict, or retrieval
  failure MUST repeat condensation/recreation and prevent a completion claim.

### Migration requirements

- **FR-MIG-001:** EngLoopKit, `tthp`, `engloop-workshop`, and
  `VerifyExtremeEdgeWithTpcc` MUST each be migrated and verified as independently
  usable v2 roots.
- **FR-MIG-002:** EngLoopKit MUST evolve its current initial seed into root
  `NORTHSTAR.md`, remove the current seed file/template/counter/prefix, and retain its
  history through Git.
- **FR-MIG-003:** TTHP's `engloop/seeds/SEED001_tthp.md` MUST be moved and evolved into
  root `NORTHSTAR.md`; the old live seed and SEED counter/template MUST not remain.
- **FR-MIG-004:** The workshop and verification consumer MUST each end with exactly one
  reviewed root `NORTHSTAR.md` reflecting that repository's own direction, with no
  current SEED machinery.
- **FR-MIG-005:** Each consumer extension installation MUST be removed/reinstalled or
  otherwise refreshed cleanly so stale old command files, generated agents, generated
  prompts, registry entries, and cached install outputs are removed rather than
  accumulated.
- **FR-MIG-006:** Each migrated current install surface MUST register exactly the 13 v2
  IDs and zero old IDs; historical Git/doc evidence is exempt from destructive
  rewriting.
- **FR-MIG-007:** EngLoopKit's manifests, bundle/catalog/release metadata, commands,
  docs, standards, examples, skills, templates, state machine, model, exploration,
  generated functional tests, direct tests, changelog, and release evidence MUST be
  migrated coherently in one breaking major release.
- **FR-MIG-008:** A focused one-root workspace entry point MUST be supplied and
  documented for each routine consumer root so its picker has one EngLoop registration.
- **FR-MIG-009:** Opening any consumer folder directly, without a workspace file, MUST
  remain a supported standalone entry point.
- **FR-MIG-010:** The existing mega-workspace MUST remain available as the
  cross-repository integration view; duplicate rows there MUST be documented as the
  expected result of independent installations, not hidden by cross-root coupling.

## Migration acceptance by repository

| Repository | Required v2 end state |
|---|---|
| EngLoopKit | One root Northstar evolved from its initial seed; retained Learnings Pyramid prototype refined to the Stage 31 contract; one 13-command extension/package; executable and documentary contracts agree; no current SEED or old command surface. |
| tthp | `SEED001_tthp` content evolved into root `NORTHSTAR.md`; no duplicate live seed or SEED counter/template; clean 13-command local installation; standalone and focused picker proof. |
| engloop-workshop | Root Northstar for the workshop's own direction; curriculum/docs use the ordered v2 workflow; no current SEED machinery; clean 13-command local installation; standalone and focused picker proof. |
| VerifyExtremeEdgeWithTpcc | Root Northstar for verification-product direction; current model/test entry points remain independently usable; clean 13-command local installation; standalone and focused picker proof. |

## Failure modes and edge cases

| Condition | Required behavior |
|---|---|
| More than one candidate Northstar exists | Fail Stage 01 and require an explicit authoritative choice; do not merge by heuristic. |
| A routine change proposes a Northstar rewrite | Reject the rewrite unless evidence demonstrates direction change. |
| Test discovery passes but intentional failure is not observed | Fail Stage 02; a green-only runner proof is incomplete. |
| The selected test runway works only from a parent workspace or absolute local path | Fail standalone acceptance and repair the recorded contract; do not add a hidden alternate path. |
| A generated test fails | Preserve the finding and route to 04, 05, or 06 according to whether SUT, model, or exploration is wrong; never delete the test merely to turn green. |
| Stage 07 does not reach a required error/recovery/reflection/configuration/platform path | Classify it as intended and return to 05/06; do not delete it or unit-test around the gap. |
| An unreached path has no requirement but its runtime entry is ambiguous | Fail closed as unclassified until authoritative evidence resolves it; ambiguity is not permission to delete. |
| Deleting scaffold residue breaks architecture or generated behavior | Stage 08 stops and routes the defect; readiness remains FAIL. |
| Existing unit tests predate Stage 08 | They may remain regression assets, but Stage 07 reachability is measured with generated functional tests alone and no new unit test may influence classification. |
| Aggregate coverage exceeds 95% while one module is below threshold | Readiness is FAIL; aggregate coverage cannot hide a weak module. |
| A mitigation appears to fix the cause | It remains a MIT until a source repair traverses 04 and 05–08 and passes target verification. |
| No incident, selected incident set, or repair item exists | Create no Stage 20, 21, or 22 work respectively; readiness authorization is not demand. |
| No Stage 30 decision-tree branch fires | Record a no-refactor REF, create no work, and leave Northstar unchanged. |
| No spare stewardship capacity is available | Keep refactor/learning opportunities visible and defer Stages 30/31; do not steal capacity from incident stabilization or required repair. |
| One learning applies to two subjects | Link it from both cards; this is valid and counted once in source completeness. |
| Cards disagree or a later source supersedes an earlier principle | Record the conflict/supersession and provenance; do not silently flatten it. |
| A card or source link is broken, the index exceeds budget, or retrieval selects the wrong evidence | Stage 31 fails and iterates; no partial completion claim. |
| Mega-workspace shows duplicate EngLoop rows | Treat it as expected integration-view behavior; use the focused single-root entry point for routine work. |
| Old generated files survive a consumer upgrade | Migration fails even if new commands also exist; clean removal is required. |

## Non-goals

- Preserving old command aliases, dual namespaces, or compatibility redirects.
- Renaming the EngLoopKit product, bundle, extension, repository, or package ID.
- Deduplicating independently installed commands across an aggregate multi-root
  workspace by coupling consumers to a parent installation.
- Selecting one implementation language, application framework, or test framework for
  every consumer.
- Treating Stage 02 scaffold code as final architecture or running the full governed
  Stage 04 workflow before executable learning exists.
- Using unit tests to manufacture functional reachability, justify dead scaffold code,
  or let Stage 07 claim readiness.
- Requiring a bespoke behavioral model for every pure component or internal pipeline
  assembly.
- Making permanent source fixes during incident stabilization.
- Creating a SEED from Stage 30 or retaining SEED as a current artifact class.
- Updating Northstar for routine feature, repair, or refactor completion.
- Automatically running operations or stewardship stages merely because their numeric
  predecessors completed.
- Deleting or rewriting accepted PM/LRN sources during condensation.
- Preloading every learning into every agent turn or adding the retrieval instruction
  to the picker.
- Lowering the current per-module 95% line and branch threshold in this feature.

## Assumptions and dependencies

- Git is available as the authoritative revision history for living Northstars, cards,
  and the one-page index.
- Each repository has an explicit artifact root and discoverable module boundaries.
- Numeric/hyphenated command IDs are supported by the command host.
- Each consumer can select a test framework capable of deterministic build, discovery,
  pass, intentional-failure reporting, and a stable generated-test destination.
- SEK provides or gains the capability to derive negative tests from modelled illegal
  order and invalid input. If it cannot, Stage 07 and readiness fail; no hand-authored
  substitute is accepted.
- Architecture governance can evaluate the accepted boundaries and final tasks.
- Consumer extension installation can remove stale generated outputs before
  registering the v2 package.
- A project may ratify a stricter future readiness or Learnings index policy through a
  later governed decision; until then this specification's fail-closed rules apply.

## Measurable success criteria

- **SC-001:** A clean extension/package inspection finds exactly 13 current command
  registrations, all matching the table, and 0 old registrations or aliases.
- **SC-002:** In a clean single-root picker, all 13 commands appear exactly once and
  their displayed lexical sequence matches 01–08, 20–22, 30–31 with 0 inversions.
- **SC-003:** 100% of the 13 command files pass the ARC002 command-loop conformance
  checks and reference only existing required artifacts.
- **SC-004:** Each of the four migrated repositories has exactly 1 root
  `NORTHSTAR.md`, 0 current live SEED artifacts/templates/counters, and Git evidence of
  any migrated seed history.
- **SC-005:** Every Stage 02 acceptance run records all five observable runway outcomes
  (build, discovery, pass, intentional fail, restored pass), one terse command, and one
  generated-test destination; no intentional failing fixture remains afterward.
- **SC-006:** The v2 generated functional suite runs against the real SUT and contains
  at least one legal-success path, one model-derived illegal-order rejection, and one
  model-derived invalid-input rejection, with materially distinct explored branches.
- **SC-007:** Stage 07 reports reachability from generated tests only, and Stage 08
  assigns a reviewed disposition to 100% of unreached production paths before any new
  unit/property test is accepted.
- **SC-008:** After every residue deletion set, the rerun Stage 07 suite and architecture
  gates are 100% green before Stage 08 continues.
- **SC-009:** A readiness PASS contains one row for 100% of surviving modules; every
  row is architecture-conformant, regression-green, verified by the correct method,
  and measures at least 95% line and 95% branch coverage.
- **SC-010:** Executable transition tests exercise 100% of declared legal transitions
  and model-derived tests reject representative illegal order, invalid input, gate
  bypass, stale-evidence, and duplicate-start attempts.
- **SC-011:** `tthp`, `engloop-workshop`, and `VerifyExtremeEdgeWithTpcc` each pass a
  standalone invocation check and a focused single-root picker check with exactly 13
  v2 rows and 0 stale old rows.
- **SC-012:** The mega-workspace remains usable for cross-repository integration and
  its documentation explains focused-workspace mitigation without claiming global
  deduplication.
- **SC-013:** Stage 31 checks report 100% source-to-card coverage, 100% cards indexed,
  100% valid card provenance, 100% resolving page/card/source links, an index within
  500 words and 60 nonblank lines, and 100% correct retrieval for a clean-context
  sample covering every card and every PM.
- **SC-014:** Independent acceptance suites for the three stage groups (01–08, 20–22,
  and 30–31) all pass from their documented fixtures without relying on an unrelated
  group.
- **SC-015:** In a clean-context usability check, every evaluator can identify the
  next normal delivery command from picker order alone and can locate the command's
  Trigger, Goal, Verification, Memory, and Done-when gate without external workflow
  prose.

## Review disposition

The maintainer ratified the final open policy on 2026-07-10: root `LEARNINGS.md` is
bounded by both 500 words and 60 nonblank lines. No specification questions remain;
the feature is ready for planning.
