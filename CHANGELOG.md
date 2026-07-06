# Changelog

All notable changes to EngLoopKit are documented here. This project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
