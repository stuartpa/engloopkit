# Changelog

All notable changes to EngLoopKit are documented here. This project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.6.0] - 2026-07-09

### Changed

- **Readiness Gate now grades self-model *adequacy*, not just existence** (IN004/PM004). A vertical's
  self-model must (1) derive **negative conformance** from the model — illegal action sequences are
  driven against the SUT and must be **rejected** (not merely assert no-throw on happy paths); (2) use
  **no hand-coded error assertions** (error transitions must come from the model's guards, not bespoke
  test code — that is theatre per PM003); and (3) meet a **behavioral-richness floor** (more than a
  trivial one-bit state). The coverage command's Readiness Inventory gained **Neg-conf?** and
  **Branches?** columns, and the model/explore commands document the negative-conformance and
  richness expectations.

### Why

- A self-model that only replays happy paths and asserts "it didn't throw" can pass while the SUT
  silently accepts illegal behavior. Grading adequacy (negative conformance + richness, model-derived)
  closes that gap so "modelled + explored" is real evidence, not a checkbox.

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
