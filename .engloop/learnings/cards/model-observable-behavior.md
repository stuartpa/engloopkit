# Card: Model observable behavior, not internal shape

- **Recall cue:** Are we modelling product behavior or merely restating each assembly?
- **Apply when:** Choosing a SEK model boundary or deciding whether a pipeline stage
  requires its own model.
- **Status:** Living
- **Last condensed:** 2026-07-10

## Compressed principle

A verification rule must say at what granularity it is satisfied. Model a vertical's
representative observable behavior end to end against the real SUT. Internal pipeline
stages receive transitive behavioral exercise plus per-module coverage; modelling each
stage independently is usually tautological theatre.

## Decision checks

1. What externally meaningful state and behavior does this model prove?
2. Does the path exercise the real vertical pipeline rather than a mock of one stage?
3. Are internal modules still measured individually for line and branch coverage?
4. Is a pure value-type or domain-free module being modelled only to tick a box? If so,
   classify and unit/property test it instead.

## Source learnings

- [PM002/LEARN002](../../postmortems/PM002_gate-verification-method-by-class.md#learnings)
  — factor generic code before self-modelling the residual vertical.
- [PM003/LEARN001](../../postmortems/PM003_selfmodel-behavior-granularity.md#learnings)
  — verification criteria must state their granularity.
- [PM003/LEARN002](../../postmortems/PM003_selfmodel-behavior-granularity.md#learnings)
  — model observable end-to-end behavior, not each internal pipeline stage.