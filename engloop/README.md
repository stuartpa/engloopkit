# `engloop/` — EngLoopKit's own EngLoopKit artifact root

EngLoopKit is developed **with EngLoopKit** (it dogfoods itself). This folder is its
engineering-loop **artifact root** — an override of the default `docs/`, because
EngLoopKit's `docs/` holds the bundle's *product* documentation. Keeping process
artifacts here mirrors the choice SEK made and exercises the v1.1.0 configurable root.

> Artifact root = `engloop/`. Document standards = the bundle's own
> [../docs/standards.md](../docs/standards.md). Counters = [numbering-registry.md](numbering-registry.md).

## Layout

```
engloop/
├── numbering-registry.md   # the counters (increment before creating a doc)
├── seeds/                  # SEEDxxx — gathering docs (Stage 0)
├── architecture/           # ARCxxx — architecture decisions (Stage 2)
├── models/                 # MDLxxx — SEK models (Stage 4)
├── cord/                   # CRDxxx — CORD explorations (Stage 5)
├── coverage/               # COVxxx — coverage reports (Stage 5)
├── incidents/              # INxxx  — incidents (Stage 6)   ← next
├── postmortems/            # PMxxx  — post-mortems (Stage 6)
└── refactors/              # REFxxx — refactor decisions (Stage 7)
```

The executable code lives at repo root (`src/`, `model/`, `tests/`); the model/coverage
docs here point at it. Spec Kit's own `specify` outputs (SPxxx) would live at `specs/`.

## Stage status (as of retroactive filing, 2026-07-06)

| Stage | State | Artifacts |
|---|---|---|
| 0 · Seed | ✅ | [SEED001](seeds/SEED001_engloopkit.md) |
| 1 · Bridge | ✅ | the v1.0.0 bundle (manifests, 9 commands, templates, docs) |
| 2 · Architect | ✅ | [ARC001](architecture/ARC001_bundle-composition.md), [ARC002](architecture/ARC002_command-loop-contract.md), [ARC003](architecture/ARC003_numbering-memory.md), [ARC004](architecture/ARC004_executable-core.md) |
| 3 · Refactor to final | ✅ | configurable artifact root + BRG (v1.1.0); executable core added |
| 4 · Model | ✅ | [MDL001](models/MDL001_engineering-loop.md) — SEK model of the loop |
| 5 · Explore / Coverage | ✅ | [CRD001](cord/CRD001_loop-conformance.md), [COV001](coverage/COV001_conformance.md) — 40 tests green |
| 6 · Operate | ⏭ next | incidents/post-mortems (this is why the loop was completed) |
| 7 · Evolve | pending | — |

EngLoopKit is now **ready to do incidents**: every stage before Operate is complete, the
SEK model is built, tests are generated, and all pass.
