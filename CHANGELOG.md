# Changelog

All notable changes to EngLoopKit are documented here. This project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
