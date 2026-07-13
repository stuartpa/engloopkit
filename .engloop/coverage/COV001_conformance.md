# COV001: Conformance / artifact coverage

- **Created:** 2026-07-06
- **Phase:** COMPLETE (conformance/artifact coverage — EngLoopKit has no runtime product code)
- **Status:** HISTORICAL BASELINE — not a current readiness verdict

## What "coverage" means here

EngLoopKit is a Spec Kit bundle — markdown, manifests, templates, plus a small executable
core of its load-bearing invariants. Line coverage of a large codebase does not apply, so
the Stage-5 goal is reinterpreted (with the maintainer) as **conformance/artifact
coverage**: every stage, prefix, command, template, and manifest field is exercised by a
passing test, and the executable core's behaviour is covered.

## Suite

| Suite | Tests | What it covers |
|---|---|---|
| SEK-generated ([CORD001](../cord/CORD001_loop-conformance.md)) | 1 | 15/15 loop transitions replayed against `EngLoopKit.Core.Loop` |
| `EngineeringLoopTests` | — | begin/advance guards, illegal-transition rejection, `LegalNext`/`IsLegalTransition`, all 11 stages |
| `NumberingRegistryTests` | — | zero-padding, unknown-prefix rejection, increment-before-create, monotonic never-reuse, all 12 prefixes |
| `BundleConformanceTests` | — | version consistency (bundle/extension/catalog), 9 commands declared + files exist, catalog count, **every command well-formed as a loop**, every referenced template exists, docs↔core prefix coupling |
| **Total** | **40** | all green (`dotnet test EngLoopKit.slnx`) |

## Artifact-coverage checklist

- [x] All 11 loop stages and 15 transitions exercised (SEK-generated + hand-written)
- [x] All 12 numbering prefixes exercised; monotonic/never-reuse enforced
- [x] All 9 commands validated against the loop contract (ARCH002)
- [x] Every template referenced by a command exists
- [x] Manifest versions consistent; command counts consistent
- [x] Docs coupled to the executable core (every core prefix documented)

## Findings closed while reaching coverage

- **SCAF** is represented in the registry and artifact map even while its current counter is zero.
- `coverage.md` uses `**Goal (ordered):**` → the loop-contract test accepts the annotated
  Goal heading.

## Decision

- [x] Historical conformance goal met for the recorded v1 surface.
- [ ] This record does **not** establish current readiness; PM001–PM004 and SPEC001 require
  a fresh complete inventory and the ratified gate.

## Related

- Model: [MODEL001](../models/MODEL001_engineering-loop.md) · Exploration: [CORD001](../cord/CORD001_loop-conformance.md)
