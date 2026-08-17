# MODEL002: Direction- and pyramid-bound operations behavior

- **Created:** 2026-08-15
- **Models:** the stateful Stage 20→21→22 operations vertical
- **Model source:** [`model/EngLoopKit.Model/Model.cs`](../../model/EngLoopKit.Model/Model.cs)
- **SUT binding:** `EngLoopKit.Core.Loop`
- **Status:** CURRENT

## Purpose

Prove that the dominant long-running ELK cycle cannot enter Incident without current
direction/learning context, cannot complete Postmortem without validated pyramid
disposition/provenance, and cannot enter Repair without current Rule-ID/executable-gate
acceptance.

## Operations state

- delivery/readiness cursor and incident active/stabilized state;
- `DirectionConsulted` and `LearningsConsulted`;
- `PostmortemLearningValidated` and `RepairLearningValidated`;
- learning refresh/repair demand plus direction/architecture branches.

## Evidence actions

- `Loop.ConsultDirection`
- `Loop.ConsultLearnings`
- `Loop.ValidatePostmortemLearning`
- `Loop.ValidateRepairLearning`

The actions are explicit because evidence acquisition/validation is real behavior, not a
Boolean guessed by a later stage. The model rejects duplicate consultation/validation and
validation outside its legal lifecycle context.

## Stage guards

- `Incident` requires actual demand, ready/incident context, direction consultation, and
  relevant learning consultation or an explicit deferral represented by that accepted
  evidence action.
- `Postmortem` requires stabilized selected incidents plus direction, pyramid consultation,
  and passing PM learning validation.
- `Repair` requires a concrete repair item plus passing current Rule-ID/gate acceptance.

## Granularity

This is behavior-level modelling of the end-to-end operations cycle. Markdown parsing,
hash algorithms, and individual validator branches receive direct tests; the model proves
legal ordering and model-derived rejection at the public `Loop` boundary.