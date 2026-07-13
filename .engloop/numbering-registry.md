# Numbering Registry (EngLoopKit self-host)

Single source of truth for EngLoopKit's own numbered document counters. **Increment the
"Last used" value here before creating a new document.** Prefix definitions and rules are
in the bundle's [document standards](../docs/standards.md). Artifact root is `.engloop/`.

## Global counters

| Prefix | Scope | Last used | Notes |
|---|---|---|---|
| `SPEC` | Specifications | `SPEC001` | SPEC001 = ordered EngLoop v2 workflow |
| `SCAF` | Scaffold/test-runway records | `SCAF000` | none yet |
| `ARCH` | Architecture decisions | `ARCH006` | bundle-composition, command-loop-contract, numbering-memory, executable-core, component-pattern, deterministic agent-surface validation |
| `MODEL` | SEK model records | `MODEL001` | MODEL001 = engineering-loop state machine |
| `CORD` | CORD exploration records | `CORD001` | CORD001 = loop conformance exploration |
| `COV` | Coverage/validation/readiness records | `COV001` | COV001 = conformance/artifact coverage |
| `IN` | Incidents | `IN004` | IN001 = consumer declared ready with no bar met; IN002 = gate conflates verification method with being verified; IN003 = self-model granularity ambiguous for a pipeline vertical; IN004 = gate accepts positive-only / thin self-models (no negative-path conformance, no behavioral-richness floor) |
| `PM` | Post-mortems | `PM004` | PM001 = no readiness gate; PM002 = verification method by module class; PM003 = self-model criterion is behavior-level; PM004 = self-model must be behaviorally rich + prove negative conformance |
| `REFACT` | Refactor decisions | `REFACT001` | REFACT001 = ordered EngLoop v2 workflow, Northstar, verification split, and Learnings Pyramid |

## Local counters

Reset inside each parent; tracked in the parent doc, not here.

| Prefix | Resets per | Recorded in |
|---|---|---|
| `MIT` | Incident | the incident's timeline table |
| `LEARN` | Post-mortem | the post-mortem's Learnings section |
| `RPI` | Post-mortem | the post-mortem's Repair Items section |
