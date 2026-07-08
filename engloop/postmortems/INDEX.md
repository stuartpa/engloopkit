# Post-mortem Index (EngLoopKit self-host)

| PM | Title | Covers | Cause-class tags | Recurrence of |
|---|---|---|---|---|
| [PM001](PM001_no-readiness-gate.md) | No enforced readiness gate — "ready for incidents" was narrated, not proven | IN001 | process-gap, validation-gap | — |
| [PM002](PM002_gate-verification-method-by-class.md) | Readiness Gate must set the verification method by module class | IN002 | validation-gap, process-gap | refines PM001 |
| [PM003](PM003_selfmodel-behavior-granularity.md) | Self-model criterion is behavior-level, not per-internal-assembly | IN003 | validation-gap | refines PM002 |
| [PM004](PM004_selfmodel-negative-conformance-and-richness.md) | An adequate self-model must be behaviorally rich and prove negative conformance (model-derived illegal-sequence tests) | IN004 | validation-gap | refines PM003 |
