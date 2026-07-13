# MODEL001: The engineering-loop state machine

- **Created:** 2026-07-06
- **Models:** `EngLoopKit.Core` — the engineering loop's stage transitions
- **Model source:** [`model/EngLoopKit.Model/Model.cs`](../../model/EngLoopKit.Model/Model.cs) (`EngLoopKit.Model.LoopModel`)
- **SUT binding:** `EngLoopKit.Core.Loop` (namespace `EngLoopKit.Core`)
- **Status:** HISTORICAL BASELINE — retained until SPEC001's v2 model work

## Purpose

A faithful SEK model of the engineering loop as a state machine, so the loop's legal stage
transitions can be explored exhaustively and turned into conformance tests. It abstracts
`EngLoopKit.Core.EngineeringLoop` — the executable transition graph.

## State fields

| Field | Type / domain | Meaning |
|---|---|---|
| `Current` | `S` (None, Seed, Bridge, Architect, RefactorToFinal, Model, Explore, Coverage, Incident, Postmortem, Repair, RefactorScan) | the stage the loop is in |

> `Current` is a **property** (`{ get; set; }`), not a field — SEK's introspector captures
> state from properties. As a field the exploration collapses to a single state.

## Actions (rules)

Eleven rules `Loop.<Stage>`, each guarded by `Require(...)` so exploration enumerates only
legal transitions (e.g. `Bridge` requires `Current == Seed`; `Explore` requires `Model` or
`Coverage`; `Incident` requires `Coverage` or `Incident`). Rule names map to the SUT class
`Loop` and its methods.

## Invariants

- A loop begins only at `Seed` (or re-seeds after `RefactorScan`).
- The Verification loop is the `Explore ⇄ Coverage` cycle.
- The Operations loop stacks `Incident*` then `Postmortem → Repair → RefactorToFinal`.

## Accepting condition

Stable resting points: `Current == Coverage` (product covered) or `Current == RefactorScan`
(a fresh refactor scanned).

## Abstraction choices

State is exactly the current stage (finite → exploration terminates and dedups cycles). The
model is an *independent* re-encoding of the loop graph; the SUT (`EngineeringLoop`) is the
implementation, so the generated tests genuinely compare spec against impl.

## Exploration

`sek explore ModelProgram` → **12 states, 15 transitions, 2 accepting**. See
[CORD001](../cord/CORD001_loop-conformance.md).
