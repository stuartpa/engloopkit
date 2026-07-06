# COV001: Conformance / artifact coverage

- **Created:** 2026-07-06
- **Phase:** COMPLETE (conformance/artifact coverage — EngLoopKit has no runtime product code)
- **Status:** COMPLETE

## What "coverage" means here

EngLoopKit is a Spec Kit bundle — markdown, manifests, templates, plus a small executable
core of its load-bearing invariants. Line coverage of a large codebase does not apply, so
the Stage-5 goal is reinterpreted (with the maintainer) as **conformance/artifact
coverage**: every stage, prefix, command, template, and manifest field is exercised by a
passing test, and the executable core's behaviour is covered.

## Suite

| Suite | Tests | What it covers |
|---|---|---|
| SEK-generated ([CRD001](../cord/CRD001_loop-conformance.md)) | 1 | 15/15 loop transitions replayed against `EngLoopKit.Core.Loop` |
| `EngineeringLoopTests` | — | begin/advance guards, illegal-transition rejection, `LegalNext`/`IsLegalTransition`, all 11 stages |
| `NumberingRegistryTests` | — | zero-padding, unknown-prefix rejection, increment-before-create, monotonic never-reuse, all 13 prefixes |
| `BundleConformanceTests` | — | version consistency (bundle/extension/catalog), 9 commands declared + files exist, catalog count, **every command well-formed as a loop**, every referenced template exists, docs↔core prefix coupling |
| **Total** | **40** | all green (`dotnet test EngLoopKit.slnx`) |

## Artifact-coverage checklist

- [x] All 11 loop stages and 15 transitions exercised (SEK-generated + hand-written)
- [x] All 13 numbering prefixes exercised; monotonic/never-reuse enforced
- [x] All 9 commands validated against the loop contract (ARC002)
- [x] Every template referenced by a command exists
- [x] Manifest versions consistent; command counts consistent
- [x] Docs coupled to the executable core (every core prefix documented)

## Findings closed while reaching coverage

- **BRG** was defined in `docs/standards.md` but missing from its artifact map → added.
- `coverage.md` uses `**Goal (ordered):**` → the loop-contract test accepts the annotated
  Goal heading.

## Decision

- [x] Goal met → Verification loop **COMPLETE**. EngLoopKit is ready to operate (Stage 6:
  incidents / post-mortems).

## Related

- Model: [MDL001](../models/MDL001_engineering-loop.md) · Exploration: [CRD001](../cord/CRD001_loop-conformance.md)
