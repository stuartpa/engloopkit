# ARC001: Bundle composition boundary

- **Created:** 2026-07-06
- **Status:** ACCEPTED
- **Governs:** the EngLoopKit bundle and its components
- **Constitution ref:** architecture-guard constitutions would live under `.specify/` (not yet wired)

## Decision

EngLoopKit is a **bundle** (`bundle.yml`) that *composes* components; it never
re-implements them. New commands are provided by exactly one first-party **extension**
(`engloopkit`); governance and lightweight-spec capabilities are composed from the
external **architecture-guard** and **tinyspec** extensions.

## Context (from the bridging code)

The bridging bundle already separated these concerns: `bundle.yml` lists three extensions;
`extensions/engloopkit/` holds the nine commands; architecture-guard and tinyspec are
referenced, not vendored. Spec Kit's own model (bundles compose; extensions provide
commands) makes this the natural long-lived boundary.

## The rule

- The bundle MUST only compose; it MUST NOT contain command logic of its own.
- All first-party commands live in the single `engloopkit` extension, namespaced
  `speckit.engloopkit.*`.
- Companion capabilities (architecture governance, tiny specs) are **composed** external
  extensions, pinned by exact version in `bundle.yml`.

## Enforcement

`specify bundle validate` (structural) + the conformance test
`Version_isConsistentAcrossBundleExtensionAndCatalog` and
`Extension_declaresNineCommands_andEachFileExists`.

## Consequences

- Easy to add/upgrade a companion extension without touching command logic.
- The bundle stays thin and portable; a consumer installs one bundle, gets the whole loop.
