# Changelog

All notable changes to EngLoopKit are documented here. This project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.11.3] - 2026-07-24

### Changed

- Stage 09 debugger walkthrough is advisory and never blocks Stage 10.
- Stage 10 entry requires only the current, HEAD-bound Stage 08 readiness PASS.
- Missing, stale, blocked, pending, or incomplete DBG evidence may be recorded as advisory
  context but cannot prevent code-review preparation.

## [1.11.2] - 2026-07-23

### Fixed

- Coexist overlay installation now supports repositories whose SpecKit `.registry` and
  `extensions.yml` are tracked. ELK uses SpecKit's supported extension materialization,
  then restores repository-owned host metadata byte-for-byte before success.
- Removal from tracked-registry hosts deletes only manifest-owned ELK paths and does not
  invoke SpecKit registry mutation.
- Direct and immutable integration tests prove tracked registry, extensions metadata,
  repository-owned agents/prompts, Git status, and prior hooks remain unchanged.

## [1.11.1] - 2026-07-23

### Fixed

- Overlay install no longer requires operators to understand or supply product/repository
  IDs. It uses the generic local product identity `engloop-overlay` and derives the stable
  archive identity from Git `origin`, or the root commit when no origin exists.
- Overlay unpack also derives the same identity when an explicit legacy ID is not supplied.

## [1.11.0] - 2026-07-23

### Added

- Generic `engloopkit readiness emit`: Stage 08 emits a checked, hashed readiness record
  bound to the exact Git HEAD and evidence path for Stage 10 consumption.
- `/speckit.engloop.60-powerpnt-create`: creates a Markdown-first Marp deck and PPTX that
  starts with the North Star, drills through boxes-and-lines architecture, explains
  focused 5–9-node model behavior graphs, and appends straight-line generated test paths.
- `PPTxxx` numbering and `.engloop/presentations/` artifact storage.

### Changed

- Stage 09 debugger walkthrough is available as soon as Stage 02 proves the runway and
  may be repeated at any later HEAD. Earlier ledgers remain historical and become stale
  when code changes.
- Stage 10 alone requires both the final current-HEAD walkthrough ledger and the generic
  current readiness record emitted by Stage 08.
- Expanded the exact surface from 18 to 19 commands and from 27 to 28 handoffs.

## [1.10.0] - 2026-07-22

### Added

- `/speckit.engloop.09-debugger-walk-thru`: requires an engineer-led, line-by-line
  debugger walkthrough for every executable changed-code chunk before review preparation.
- Numbered `DBGxxx` walkthrough ledgers with exact base/HEAD, chunk boundaries,
  debugger/breakpoint/trigger evidence, and non-delegable per-chunk engineer attestation.
- Debugger-neutral setup: use explicit repository/user authority; after one bounded setup
  failure, offer a reusable repo-local debugger `SKILL.md` and create it only with approval.

### Changed

- Moved code-review preparation from Stage 09 to Stage 10.
- Required workflow is Stage 08 readiness → Stage 09 debugger walkthrough → Stage 10
  review preparation. Product edits at Stage 10 invalidate evidence and return through
  Stages 08 and 09.
- Expanded the exact agent surface from 17 to 18 commands and from 25 to 27 handoffs.

## [1.9.1] - 2026-07-21

### Fixed

- Overlay removal now quarantines non-empty registered directories child-first before
  deleting their empty roots, with path/operation/exception diagnostics on access failure.
- Install/unpack now persist exact pre-install hook bytes or an explicit absence marker
  under overlay-owned state. Removal restores non-ELK hooks and pre-existing ELK wrappers
  byte-for-byte before reporting success.
- Legacy overlays without authoritative hook-baseline metadata preserve an ambiguous ELK
  wrapper rather than silently weakening repository protection.

## [1.9.0] - 2026-07-21

### Added

- `/speckit.engloop.09-codereview-prepare`: minimizes and validates an explicitly
  selected GitHub/Azure DevOps PR, identifies current reviewers, and records only
  current-PR, source-linked recurring technical review concerns.
- `/speckit.engloop.40-pomodoro-create`: records the just-completed 30–60 minute session
  as `.engloop/pomodoros/POM<NNNN>-<brief-description>.md`; POM numbering begins at
  `POM0001` and uses a four-digit monotonic counter.
- `/speckit.engloop.51-overlay-remove` and `engloopkit overlay remove`: require an exact
  target-bound confirmation token, remove manifest-owned installation and dynamically
  registered paths, remove the ELK exclude block/wrappers, restore prior hooks, and
  preserve unrelated coexist-host files.

### Changed

- Renumbered overlay pack from Stage 09 to `/speckit.engloop.50-overlay-pack`.
- Expanded the exact custom-agent surface from 14 to 17 commands and from 24 to 25
  review-first handoffs; Stages 31, 40, and 51 are terminal agents.

## [1.8.2] - 2026-07-19

### Fixed

- Added `engloopkit overlay register` as the single explicit runtime ownership registry
  for model directories and generated destinations selected after overlay installation.
- Registration atomically reconciles `.engloop-overlay/manifest.json` and the ELK block
  in `.git/info/exclude`, rejecting paths already tracked, staged, or present in history
  since the overlay baseline.
- Staged/push verification now applies registered ownership with case-insensitive,
  slash-normalized matching, while unregistered product source remains trackable.
- Stage 05 and Stage 06 now require explicit registration before creating overlay-local
  model or generated outputs outside `.engloop/`.

## [1.8.1] - 2026-07-15

### Fixed

- Added explicit `--host-mode coexist` for repositories that already own local agent
  directories, prompts, or hooks. Existing host agent/prompt files are preserved
  byte-for-byte; ELK owns only exact `speckit.engloop.*` entries.
- Existing local hooks are preserved as `*.elk-prior`; the ELK wrapper invokes the
  preserved hook before overlay verification.
- Coexist mode requires an existing local Spec Kit host and rejects tracked shared
  registration files or exact ELK-owned name collisions.

## [1.8.0] - 2026-07-13

### Added

- **Private overlay mode** for an existing Git repository. Explicit
  `engloopkit overlay install --mode overlay` uses local `.git/info/exclude` and
  ELK-owned local hooks so managed ELK files stay out of ordinary commits and pushes.
- `engloopkit overlay verify`, `pack`, and `unpack`: path-safe, hash-verified,
  repository-identity-bound transfer of registered overlay state in one plain ZIP.
  Overlay archives are deliberately unencrypted and reject secret-like paths.
- `/speckit.engloop.09-overlay-pack`, an explicit pack agent. Install and unpack remain
  tool features because their target root may not yet have agents.

### Security / isolation

- Overlay mode never edits tracked `.gitignore` or workload files, never merges with an
  existing ELK/Spec Kit surface, and fails closed on tracked-path, hook, origin, base
  revision, archive path, hash, or collision ambiguity.
- Normal Git hooks protect commits/pushes; deliberate hook bypass is documented as outside
  repository-local protection rather than claimed impossible.

## [1.7.0] - 2026-07-13

### Added

- Ordered EngLoop workflow generation: thirteen exact `speckit.engloop.*` commands
  spanning delivery/readiness (01–08), operations (20–22), and stewardship (30–31).
  The product remains on the **1.x** SemVer maturity line; “v2” names the workflow
  generation, not a v2.0 release.
- One canonical tracked `.engloop/` process root, root `NORTHSTAR.md`, root
  `LEARNINGS.md`, explicit v2 configuration, deterministic test-runway evidence, and
  evidence-gated ordered transition state.
- Independent stateful SEK model, bounded exploration, portable generated conformance
  suite with legal and model-derived rejection paths, and whole-product measured 95/95
  readiness inventory.
- Deterministic, non-UI agent-surface validation: source/archive/disposable-install
  semantic comparison, exact 13 command/agent/prompt identities, tool/subagent policy,
  review-first handoffs, and local-tool entry rejection. ELK never launches, reads, or
  automates an editor UI for validation.

### Changed

- Product/bundle/tool identity remains `engloopkit`; the installed ordered command
  extension is `engloop`, yielding the `speckit.engloop.*` namespace.

## [1.5.0] - 2026-07-06

### Changed

- **Readiness Gate: the vertical self-model criterion is now stated at behavior granularity**
  (PM003/IN003). A vertical is usually a pipeline whose assemblies are *stages*; the self-model
  criterion is satisfied by **one representative end-to-end self-model** (MDL + CRD) that exercises
  the vertical's observable behavior against a real SUT, plus the tool's sample conformance loops —
  internal stages are validated transitively. A bespoke model *per assembly* is **not** required and
  is discouraged as theatre (per PM002). Pure value-type vertical modules are verified like
  components. **The ≥95% line & branch coverage requirement stays per module** — this only fixes
  *where the "modelled + explored" evidence lives*, never the coverage bar.

### Why

- Post-mortem **PM003** (incident **IN003**): applying v1.4.0 to a pipeline vertical (a compiler +
  engine) read as "model each internal assembly," which would be tautological theatre. The fix states
  the granularity so neither theatre nor under-verification is expressible.

## [1.4.0] - 2026-07-06

### Changed

- **Readiness Gate now sets the verification method by module class** (refines v1.3.0). Every module
  still requires **≥95% line & branch**, architecture conformance, and green suites, but *how* it is
  verified follows the ARC002 litmus test:
  - **Component** (`components/*`, domain-free) → **unit / property tests**; **no `MDL`/`CRD`** (a
    model of pure code is tautological and inviting one is the PM001 failure in disguise).
  - **Vertical** (`src/*`, domain behavior) → a SEK **`MDL`** + **`CRD`** that **generates**
    conformance tests (SEK self-modelling).
  - **Precondition:** generic/domain-free code still in the vertical is an **ARC002 violation & a
    gate FAIL** — extract it to a component first. This is how a self-hosting tool becomes honestly
    self-validating: factor the generics out, and the residual vertical is the real domain surface
    to model.
- The Readiness Inventory (`coverage` command + `COV` template) gains a **Class** column; PASS rules
  branch on class. `model`/`explore` commands and the `using-sek-to-generate-tests` skill now say:
  **don't model pure components — extract and unit-test them; model only the domain vertical.**

### Why

- Post-mortem **PM002** (incident **IN002**): the v1.3.0 gate demanded `MDL`+`CRD` for *every* module
  including pure components, which is meaningless for domain-free code and invites fake models. The
  fix keys the verification *method* to the module *class* without lowering the bar.

## [1.3.0] - 2026-07-06

### Added

- **The Readiness Gate — a Definition of Ready-for-Incidents** (`docs/standards.md` + the
  `coverage` command). "Ready for incidents" / "ready to operate" is now the **output of a gate,
  never a claim an agent narrates.** A project is ready **iff** every module (each `components/*`
  component **and** the vertical) is modelled (`MDL`), explored (`CRD`), covered **≥95% line &
  branch** (measured with real tooling), architecture-conformant, and green. Until the gate PASSes,
  the required status is **NOT READY**.
- `/speckit.engloopkit.coverage` reworked to be **whole-product**: it builds a per-module
  **Readiness Inventory** table (no module omitted; an untested module is `0% / FAIL`), runs real
  coverage tooling, computes an explicit **PASS/FAIL gate verdict** (new Step 3.5), and emits a
  gated report — the PASS/"ready" template is unreachable unless every inventory row passes. The
  `COV` template carries the inventory + verdict.
- **Anti-narration rule** added to the `coverage`, `incident`, and `postmortem` commands, the
  extension README, and the `using-sek-to-generate-tests` skill: readiness may be stated only by
  reporting a PASSing gate.

### Why

- Post-mortem **PM001** (incident **IN001**): a consumer project was declared "ready for incidents"
  after only the architecture stages + one pilot component, with nothing else modelled/explored and
  coverage never measured. Root cause: the Stage 5 → Stage 6 transition was narrated, not gated.
  This release makes that class of failure mechanically impossible.

## [1.2.0] - 2026-07-06

### Added

- **The component pattern** as an enforced architectural principle
  (`docs/component-pattern.md`): non-vertical code (generic building blocks that wrap the
  language runtime/BCL) lives in a language-appropriate folder (C# `components/`, Go
  `internal/`, …) as components; the vertical composes them. EngLoopKit applies it to itself
  and *causes every governed repo to adopt it*.
- `/speckit.engloopkit.architect` now **establishes and enforces** the vertical/component
  boundary (mandatory step + governed rule); `/speckit.engloopkit.refactor-scan` gains a
  decision-tree branch that **converges** toward it (extract one leaked component per cycle).
- EngLoopKit's own core split into components: `EngLoopKit.Components.Numbering`
  (monotonic counters) and `EngLoopKit.Components.StateMachine` (generic guarded machine);
  `EngLoopKit.Core` (the vertical) composes them.
- Conformance tests couple the principle to reality (commands must enforce it; the repo must
  follow it). Suite now 42 tests.

## [1.1.0] - 2026-07-06

### Added

- **Configurable artifact root.** Artifact paths are now written `<ARTIFACT_ROOT>/`
  (default `docs/`). A project whose `docs/` is a published documentation site can
  override the root to a dedicated folder (recommended: `engloop/`) so process
  artifacts never leak into the published site. Documented in `docs/standards.md`;
  each command carries an artifact-root note.
- **`BRG` prefix** for bridging-stage records (implementation-state / parity / audit
  notes produced during Stage 1, before the architecture stage).

### Changed

- `docs/standards.md` and `docs/numbering-registry.md` parametrized to the artifact root
  and extended with `BRG`.

## [1.0.0] - 2026-07-06

### Added

- Initial EngLoopKit bundle (`bundle.yml`) composing the `engloopkit` extension with
  `architecture-guard` and `tinyspec`.
- `engloopkit` extension with nine loop-stage commands: `seed`, `architect`, `model`,
  `explore`, `coverage`, `incident`, `postmortem`, `repair`, `refactor-scan`.
- Document standards and numbering registry (SEED, SP, ARC, MDL, CRD, COV, IN, MIT,
  PM, LRN, RPI, REF).
- Templates for every produced artifact.
- Docs: the engineering loop, Loop Engineering alignment, token efficiency.
- SEK dogfood walkthrough.
