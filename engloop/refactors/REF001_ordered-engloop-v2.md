# REF001: Ordered EngLoop v2 workflow

- **Date:** 2026-07-10
- **Trigger:** Dogfooding EngLoopKit while starting the TTHP product and its workshop
- **Cadence:** Maintainer-directed evolution review
- **Status:** CHOSEN
- **Delivery route:** Full governed delivery loop; this document is the source for the specification
- **Breaking release:** Yes — clean v2 command migration, with no legacy command aliases

## Why this is a refactor, not an incident

No operating system required emergency stabilization, and no temporary mitigation was
applied. The current extension works according to its documented contract; using it in
TTHP exposed that the contract and workflow should evolve substantially. Treating this
as an incident would manufacture an outage and post-mortem rather than improve the
methodology honestly.

This is also too broad for an ungoverned direct edit. It changes command identity,
artifact semantics, the executable stage machine, verification ownership, consumer
migration, and the way project memory is exposed to agents. It therefore enters a full
specification, architecture-governed implementation, modelling, exploration,
validation, and unit-test loop.

## Signals gathered

| Signal | Finding |
|---|---|
| TTHP dogfooding | Starting a real product made the intended sequence and each stage's responsibility clearer. |
| Agent picker usability | `speckit.engloopkit.*` repeats “kit” and sorts by command name rather than workflow order. |
| Multi-root duplication | TTHP, the workshop, and the ExtremeEdge verification repo each install the extension so the aggregate workspace displays each agent three times. Renaming alone does not deduplicate them. |
| Seed semantics | A series of immutable numbered seeds does not represent the one enduring direction of a repository. Git already preserves revisions of a living document. |
| Scaffold purpose | The first implementation exists to make a thin end-to-end path work and, critically, prove that the chosen test framework can build and run tests before SEK generates into it. |
| Verification semantics | SEK-generated tests should prove functional behavior before unit tests are written. Functional validation and unit code coverage are different loops with different evidence. |
| Scaffold residue | Initial scaffold code may survive after the architecture and modeled behavior have converged. Unit-testing that residue rewards dead code rather than removing it. |
| Learning retention | Chronological post-mortem learnings become too numerous to keep active. They need loss-aware compression with traceable drill-down, inspired by Stuart's long-used index-card → one-page → exam-recall revision technique. |
| Existing guardrails | PM001–PM004 remain binding: readiness is a computed gate; module class determines verification method; vertical modelling is behavior-level; negative conformance and behavioral richness are mandatory. |

## Chosen refactor

### Product name versus command namespace

The product, bundle, extension package, and repository remain **EngLoopKit**. The
agent/command namespace becomes **`speckit.engloop`**, avoiding
`speckit.engloopkit` (“kit” twice) at the invocation surface.

Command names carry a zero-padded numeric workflow prefix so lexical order is process
order:

| Order | Command | Responsibility |
|---|---|---|
| 01 | `speckit.engloop.01-northstar` | Create or evolve the repository's one living direction document. |
| 02 | `speckit.engloop.02-scaffold` | Vibe a thin working implementation and prove the selected test framework. |
| 03 | `speckit.engloop.03-architect` | Derive and govern the long-lived architecture from working evidence. |
| 04 | `speckit.engloop.04-refactor` | Use the governed Spec Kit loop to turn scaffold code into architecture-conformant product code. |
| 05 | `speckit.engloop.05-model` | Build the SEK behavioral model. |
| 06 | `speckit.engloop.06-explore` | Explore the model and generate functional tests into the proven test framework. |
| 07 | `speckit.engloop.07-validate` | Run SEK-generated functional tests and produce behavior/reachability evidence. |
| 08 | `speckit.engloop.08-unittest` | Remove dead scaffold residue first, then unit-test surviving units and compute readiness. |
| 20 | `speckit.engloop.20-incident` | Stabilize an operating disruption with mitigations only. |
| 21 | `speckit.engloop.21-postmortem` | Analyze stabilized incident sets into class-level learnings and repair items. |
| 22 | `speckit.engloop.22-repair` | Route permanent repairs through the governed delivery and verification loops. |
| 30 | `speckit.engloop.30-refactor-scan` | Select the single highest-value periodic evolution refactor. |
| 31 | `speckit.engloop.31-learnings-pyramid` | Compress the full learning sequence into subject cards and an on-demand one-page index. |

The old `speckit.engloopkit.*` identities are removed in v2 rather than retained as
aliases. Aliases would preserve the long names and make the picker even noisier.

### Number bands are invocation lanes, not one automatic timeline

The numeric gaps intentionally separate three kinds of work:

- **01–08 · Delivery/readiness lane:** the normal ordered sequence for building or
  changing a product and proving it ready.
- **20–22 · On-demand operations lane:** invoked only by real operational demand.
  Stage 20 starts when an incident occurs; Stage 21 starts when stabilized incidents
  warrant a post-mortem; Stage 22 starts when that analysis produces repair items.
  A Stage 08 PASS authorizes operation but does not automatically invoke Stage 20.
- **30–31 · Spare-capacity stewardship lane:** generally invoked when the engineer has
  spare time or, especially, spare agent tokens near the end of the month. Stage 30
  spends that capacity on the highest-value evolution scan. Stage 31 spends it
  condensing an accumulated learning backlog and testing retrieval. Neither stage is
  automatically appended to every delivery, repair, or calendar event.

A pending post-mortem, repair, refactor opportunity, or learning refresh remains
visible until capacity and prerequisites permit its lane to run; it must not be marked
complete merely because another lane progressed. This cadence decision consulted the
[Readiness is a gate](../learnings/cards/readiness-is-a-gate.md) card and its
`PM001/LRN001` and `PM001/LRN003` sources: a gate controls whether a transition is
permitted, not whether unrelated work should be invented or scheduled.

### 01 — one living Northstar

Every governed repository has exactly one root-level **`NORTHSTAR.md`**.

It is living memory, not a numbered snapshot. It captures:

- why the repository exists and for whom;
- the enduring outcomes and non-negotiable invariants;
- the direction in which the product should move;
- what the repository should do more of over time;
- what it should deliberately do less of or stop doing;
- current boundaries and meaningful unresolved questions.

Git is the version history. v2 removes the `SEED` artifact class, seed counter,
`seeds/` requirement, and seed template. Existing initial seeds are moved and evolved
into `NORTHSTAR.md`; they are not retained as duplicate live files because their prior
forms remain in Git history.

`01-northstar` can create the document once or update it when evidence changes the
repository's direction. It must not rewrite direction merely because a feature or
refactor was completed.

### 02 — scaffold and prove the test runway

`02-scaffold` directly creates a deliberately thin working implementation from the
Northstar. It optimizes for learning and end-to-end execution, not final architecture.
It may “vibe” code, but it may not fake success, hide fallbacks, or weaken explicit
contracts.

Its most important durable result is a working test runway:

1. choose a test framework appropriate to the repository;
2. install and configure it explicitly;
3. create at least one meaningful test through the actual product boundary;
4. prove build, discovery, execution, pass, and intentional-failure reporting;
5. record the exact terse test command and generated-test project/location contract;
6. leave the runway ready for SEK-generated tests from Stage 06.

The scaffold is not required to run the full Spec Kit specify/plan/tasks workflow. Its
purpose is to obtain executable evidence quickly. Final design discipline begins after
that evidence exists.

### 03 and 04 — derive, then govern

`03-architect` derives long-lived boundaries and contracts from the working scaffold.
`04-refactor` then runs the full architecture-governed
specify → plan → tasks → implement loop. Stage 04 is where temporary scaffold choices
are replaced by final-form code, without pretending the initial implementation was
already architected.

### 05, 06, and 07 — behavioral proof

`05-model` defines the product's behavioral state, actions, outcomes, guards, and
invariants in SEK. `06-explore` uses CORD and the solver to enumerate behavior and
generate functional tests into the test runway proven by Stage 02.

`07-validate` runs those generated tests against the real SUT. It owns functional
evidence, including:

- positive conformance;
- model-derived negative conformance for illegal ordering and invalid input;
- behaviorally distinct explored paths rather than a flat covering script;
- green execution against the real system boundary;
- a functional reachability map showing which production paths the generated suite
  exercises.

Stage 07 does not pad line coverage with hand-written unit tests and does not emit the
final readiness verdict. A required behavior not reached by generated functional tests
is a model/exploration/validation gap and returns to Stage 05 or 06.

### 08 — remove dead code before writing unit tests

The order inside `08-unittest` is non-negotiable:

1. **Observe Stage 07 reachability.** Start from coverage produced only by the
   SEK-generated functional suite. Do not add unit tests yet.
2. **Classify every unreached production path.** Non-execution is evidence for
   investigation, not automatic proof of death.
3. **Return intended gaps.** If the path is required by the Northstar, accepted spec,
   architecture, public contract, configured capability, platform entry point,
   serialization/reflection contract, or required error/recovery behavior, return it
   to `05-model → 06-explore → 07-validate`. Do not preserve it by writing a unit test
   around an unproven functional requirement.
4. **Delete unsupported residue.** If the path is unreached and has no authoritative
   requirement or runtime entry mechanism, remove it as scaffold-era dead code.
5. **Re-prove behavior.** Build and rerun Stage 07 after each coherent deletion set.
   Architecture checks and generated functional tests must remain green.
6. **Repeat until classified.** Every surviving path is functionally justified or has
   an explicit, reviewed reason why only direct unit/property verification can reach
   it.
7. **Only then write unit/property tests.** Drive the surviving units to the ratified
   whole-product line and branch threshold (currently at least 95% per module).
8. **Compute the final Readiness Gate.** Stage 08 combines current Stage 07 evidence,
   architecture conformance, dead-code disposition, unit/property coverage, and all
   green regression gates into the per-module readiness inventory and PASS/FAIL
   verdict.

This prevents a common coverage pathology: writing tests solely to keep code that the
functional model never needs. Unit tests protect justified units; they do not grant
dead code a reason to exist.

The executable loop must permit `07-validate → 08-unittest → 07-validate` while pruning
changes are revalidated. Operations can start only after Stage 08 computes PASS.

### 20–22 — operations remain semantically strict

The operations stages are demand-driven and retain the existing Golden Rule:

- an actual incident invokes Stage 20, which mitigates and stabilizes but does not
  install a permanent fix;
- a deliberate review of stabilized incidents invokes Stage 21, which analyzes the
  selected set and emits source learnings plus
  concrete repair items;
- resulting repair items invoke Stage 22, which returns through Stage 04 and all
  applicable 05–08 gates.

If there is no incident, there is no Stage 20 work. If there is no selected stabilized
incident set, there is no Stage 21 work. If there is no repair item, there is no Stage
22 work.

Renumbering does not weaken PM001–PM004 or allow readiness to be narrated.

### 30 — evolution scan

`30-refactor-scan` continues to choose one highest-value long-term refactor and records
a numbered `REF`. It does **not** emit a new seed. It normally hands an existing product
to Stage 04. It proposes a `NORTHSTAR.md` update only when evidence genuinely changes
repository direction; routine refactors must not churn the Northstar. Its natural
trigger is available engineering or agent-token capacity—particularly use-it-or-lose-it
capacity near month-end—not completion of every delivery or repair.

### 31 — Learnings Pyramid

The Learnings Pyramid adapts Stuart's long-used exam revision practice to durable
engineering memory. The analogy is exact:

Its natural trigger is an accepted-learning backlog plus spare engineering or
agent-token capacity, commonly near month-end. A pending refresh is visible but does
not block urgent incident stabilization or repair, and Stage 31 does not run merely
because Stage 30 ran.

| Revision layer | Engineering layer | Role |
|---|---|---|
| Everything that may be examined | The chronological sequence of accepted learnings | Complete source syllabus; never discarded by compression. |
| Subject index cards | Living subject cards | Compress related learnings into one focused concept while preserving source links. |
| One-page revision sheet | Root `LEARNINGS.md` | Tiny map of every subject card and the cues for when it matters. |
| Exam recreation/application | On-demand agent retrieval | Load the page for a relevant task, reconstruct the applicable model through cards, and drill into source evidence when needed. |

#### Layer 1 — authoritative source-learning sequence

The complete set of accepted `PMxxx/LRNxxx` entries, in chronology, is the equivalent
of everything that must be learned for an exam. These source learnings remain in their
post-mortems. Compression must never replace, edit, or sever them.

Each source identity is globally addressable as the pair `PMxxx/LRNxxx`, even though
`LRN` numbering remains local to its post-mortem.

#### Layer 2 — subject index cards

Cards live under:

`<ARTIFACT_ROOT>/learnings/cards/<subject>.md`

Each living card focuses on one coherent subject, not one incident. A card contains:

- a terse subject name and recall cue;
- the compressed principle in plain language;
- when an agent should apply it;
- operational implications or decision checks;
- links to every contributing source learning (`PMxxx/LRNxxx`);
- conflicts, supersessions, or unresolved tension without silently choosing one;
- Git history for evolution of the subject's understanding.

Cards may group many chronological learnings and a learning may inform more than one
subject, but every accepted source learning must be referenced by at least one card.
Cards do not copy whole post-mortems.

#### Layer 3 — one-page index

Root **`LEARNINGS.md`** is a genuinely compressed navigation and recall page. It has
one terse cue per subject card, organized so relationships are visible, and links each
cue to its card. It does not duplicate card explanations or source narratives.

Every current card must appear on the page. Following a cue must reach a card, and
following that card's provenance must reach the original learning sequence:

`LEARNINGS.md → subject card → PMxxx/LRNxxx source`

This traceability makes condensation loss-aware: agents can start cheap and drill down
only when a decision needs evidence.

#### Layer 4 — make the page available when needed

Stage 31 creates a concise on-demand instruction at:

`.github/instructions/project-learnings.instructions.md`

Its keyword-rich discovery description covers architecture, scaffolding, refactoring,
modelling, validation, unit testing, incidents, repairs, and evolution decisions. The
instruction does not duplicate the pyramid. It tells the agent to read root
`LEARNINGS.md`, follow only the relevant subject cards, and inspect cited source
learnings before making a consequential decision.

It has no broad `applyTo: "**"` rule, so unrelated turns do not pay the context cost.
It is an instruction rather than another visible custom agent or slash command, so it
does not add more picker clutter. The prototype created while deciding this refactor is
the repository's root `LEARNINGS.md`, its four initial subject cards, and the discovery
instruction at that path; the v2 specification must retain or deliberately refine that
working evidence.

#### Pyramid verification

Stage 31 is complete only when deterministic checks prove:

- every accepted source learning is referenced by at least one subject card;
- every subject card cites at least one valid source learning;
- every current subject card is represented in `LEARNINGS.md`;
- all page → card → source links resolve;
- the root index contains no more than **500 words and 60 nonblank lines**; both
  deterministic limits must pass;
- a clean-context retrieval exercise given only the one-page entry point can locate
  the correct card and source for sampled learning-derived questions;
- omissions or poor recreation cause another generate/condense/recreate iteration,
  rather than a claim that the pyramid is complete.

This incorporates the useful warning from index-card guidance: cards are limited by
what their author selects. Completeness and provenance checks prevent compression from
quietly deleting inconvenient learning.

## External material gathered

These references support individual mechanics; the exact engineering workflow above is
Stuart's specified adaptation rather than a claim that one source defines it:

- [Kansas State University — The Index Card Study System](https://www.k-state.edu/counseling/services/resources/self_help/indexcardstudysystem.html): write focused questions/terms, explain in one's own words, self-test, and do not treat cards as the only source.
- [Open Study College — Active recall](https://www.openstudycollege.com/blog/5-study-techniques-to-help-you-ace-your-exams): recall without the source, then compare to identify missing areas.
- [SchoolHabits — index-card methods](https://schoolhabits.com/10-different-ways-to-use-index-cards-to-study/): group related cards, condense essential information, and recreate maps without the original.
- [SchoolHabits — mind mapping](https://schoolhabits.com/how-to-study-using-mind-mapping-a-study-method-for-visual-learners/): make relationships visible, use very short cues, attempt reconstruction first, then inspect sources to fill gaps.
- [True Coaching — one-page summaries](https://www.truecoaching.com.au/transform-your-revision-with-one-page-summaries/): group related ideas and reduce key concepts to a single navigable page.
- [True Coaching — Feynman technique](https://www.truecoaching.com.au/conquer-confusion-with-the-feynman-technique/): explain simply, identify weak understanding, return to source, and simplify again.

Some supplied video/PDF pages could not be meaningfully extracted in the available web
environment. They are supporting references, not required evidence for the maintainer's
explicitly stated process.

## Picker duplication and focused workspaces

The duplicate rows in the attached picker are not three registrations inside one
extension. They come from three independently usable consumer roots in the same
multi-root workspace, each with generated `.github/agents/` files.

v2 preserves standalone repository usability. It will add focused workspace entry
points for ordinary work (one consumer root per focused workspace). The existing mega
workspace remains useful for cross-repository integration and may display duplicate
locally installed agents. EngLoopKit will document that tradeoff rather than coupling
independent repositories to one parent-local installation.

## Binding constraints carried forward

1. Every command remains a Trigger · Goal · Actions · Verification · Memory loop with
   a Done-when gate (ARC002).
2. Architecture remains derived from working evidence and governs final code.
3. Generic/domain-free components are unit/property tested; the residual domain
   vertical is behaviorally self-modelled with SEK.
4. Vertical self-model evidence is behavior-level, includes model-derived negative
   conformance, and has non-trivial branching.
5. Whole-product line and branch coverage remains at least the ratified threshold per
   module after dead-code pruning.
6. Readiness remains the output of a machine-evidenced inventory and gate, never a
   narrated assertion.
7. Missing or ambiguous evidence fails closed; no compatibility alias or hidden
   fallback masks a migration or verification failure.

## Migration contract

- Release as a breaking major version.
- Keep extension/package identity `engloopkit`; replace only command namespace and
  identities with the 13 ordered `speckit.engloop.*` names.
- Do not ship old command aliases.
- Replace existing seed content with one root `NORTHSTAR.md`; remove seed artifacts,
  template, and counter from current consumers while relying on Git for history.
- Add root `LEARNINGS.md`, subject-card storage, and the on-demand discovery
  instruction.
- Reinstall the extension in TTHP, the workshop, and verification consumers so stale
  generated old agents/prompts are removed rather than accumulated.
- Update the executable state machine, independent SEK model, CORD exploration,
  generated conformance tests, hand-written unit tests, manifests, catalog, bundle,
  docs, examples, skills, templates, changelog, and release metadata together.
- Add focused workspaces and verify the picker ordering in a single-root consumer.

## Expected long-term benefit

- The agent picker teaches the workflow through its order instead of hiding it in
  prose.
- A repository has one durable direction rather than a pile of competing beginnings.
- Scaffold speed and architecture discipline coexist instead of being conflated.
- SEK proves functional behavior before hand-written tests can distort coverage.
- Dead scaffold code is deleted instead of fossilized behind unit tests.
- Readiness evidence has clear ownership and retains all hard-won PM001–PM004 rules.
- Learnings remain complete and auditable while their active context cost stays tiny.
- TTHP and workshop participants experience the actual intended loop rather than a
  historical command layout.

## Rationale for not choosing alternatives

- **Incident → post-mortem → repair:** rejected because there is no operating outage or
  mitigation; using incident machinery would create false operational history.
- **Direct rename only:** rejected because the requested semantic changes alter the
  state machine and readiness contract, not just labels.
- **Legacy aliases:** rejected because they preserve the bad picker experience and
  violate the clean, observable migration choice.
- **Delete everything Stage 07 does not execute:** rejected because reflection,
  configuration, platform hooks, required recovery paths, and missing model behavior
  can be legitimately unreached. Non-reachability initiates classification.
- **Unit-test before pruning:** rejected because tests would create artificial reasons
  to retain scaffold residue.
- **Always inject all learnings:** rejected because it burns context and turns memory
  into noise. Progressive page → card → source retrieval is cheaper and more precise.
- **One giant learning summary:** rejected because compression without provenance is
  silent forgetting.

## Hand-off

Create a full v2 specification from `REF001`; plan the clean migration; implement all
artifacts coherently; then update the model, explore, validate generated functional
tests, prune dead code, and complete unit-test/readiness evidence. Do not claim v2
complete from renamed files alone.