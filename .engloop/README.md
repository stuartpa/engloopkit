# `.engloop/` — EngLoopKit's own EngLoopKit artifact root

EngLoopKit is developed **with EngLoopKit** (it dogfoods itself). This folder is its
engineering-loop **artifact root** — separate from `docs/`, because EngLoopKit's
`docs/` holds the bundle's *product* documentation. V2 fixes this as the one tracked,
hidden process root; there is no visible compatibility tree or alternate config root.

> Artifact root = `.engloop/`. Document standards = the bundle's own
> [../docs/standards.md](../docs/standards.md). Counters =
> [numbering-registry.md](numbering-registry.md). Living entry points are
> [NORTHSTAR.md](../NORTHSTAR.md) and [LEARNINGS.md](../LEARNINGS.md).

The planned required config is `.engloop/config.json`; ignored transient output is
`.engloop/out/`. Neither path has a fallback.

## Layout

```text
.engloop/
├── numbering-registry.md   # the counters (increment before creating a doc)
├── scaffolds/              # SCAFxxx — scaffold/test-runway records (none yet)
├── architecture/           # ARCHxxx — architecture decisions
├── models/                 # MODELxxx — SEK model records
├── cord/                   # CORDxxx — CORD exploration records
├── coverage/               # COVxxx — coverage/validation/readiness records
├── incidents/              # INxxx — incidents with local MIT actions
├── postmortems/            # PMxxx — post-mortems with local LEARN/RPI entries
├── repairs/                # PMxxx-RPIxxx — direction/rule/gate-bound route and close acceptance
├── refactors/              # REFACTxxx — refactor decisions
├── deadcode/               # DEADCODExxx — proven deletion proposals and user decisions
├── debugger-walkthroughs/  # DBGxxx — engineer-attested debugger walkthrough ledgers
├── evidence/               # compact utility evidence (token-efficiency analysis/implementation JSON)
├── six-pagers/             # SIXxxx — six-page narrative Markdown/DOCX and validation
├── presentations/          # PPTxxx — Markdown decks, PPTX files, and graph assets
├── academic-papers/        # PAPxxx — research Markdown/PDF, bibliography, figures/data
├── handoffs/               # HANDOFFxxx — chat/team continuation packets
└── learnings/cards/         # living cards with PM/LEARN provenance
```

The executable code lives at repo root (`src/`, `model/`, `tests/`); the model/coverage
docs here point at it. Spec Kit's own `specify` outputs (`SPECxxx`) live at `specs/`.

## Current record status (2026-07-10 checkpoint)

| Class | State | Artifacts |
|---|---|---|
| Direction | living | [NORTHSTAR.md](../NORTHSTAR.md) |
| Specification | ready for tasks | [SPEC001](../specs/SPEC001-ordered-engloop-v2/spec.md) |
| Scaffold/test runway | pending | none; registry remains `SCAF000` |
| Architecture | current baseline | [ARCH001](architecture/ARCH001_bundle-composition.md) through [ARCH009](architecture/ARCH009_operations-learning-gate.md) |
| Model | historical baseline | [MODEL001](models/MODEL001_engineering-loop.md) |
| Explore / Coverage | historical baseline | [CORD001](cord/CORD001_loop-conformance.md), [COV001](coverage/COV001_conformance.md) |
| Operations | historical evidence | [IN001–IN004](incidents/), [PM001–PM004](postmortems/INDEX.md) |
| Evolution | current decision | [REFACT001](refactors/REFACT001_ordered-engloop-v2.md) |
| Utility evidence | current contract | [evidence/README.md](evidence/README.md) defines compact Agent 30/31 JSON and ignored detailed logs. |

`COV001` preserves prior conformance evidence; it is not a current v2 readiness
verdict. Only the ratified complete readiness gate may authorize operation.
