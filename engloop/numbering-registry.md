# Numbering Registry (EngLoopKit self-host)

Single source of truth for EngLoopKit's own EngLoopKit document counters. **Increment the
"Last used" value here before creating a new document.** Prefix definitions and rules are
in the bundle's [../docs/standards.md](../docs/standards.md). Artifact root is `engloop/`.

## Global counters

| Prefix | Scope | Last used | Notes |
|---|---|---|---|
| `SEED` | Gathering docs | `SEED001` | SEED001 = EngLoopKit itself |
| `SP` | Specs | `SP000` | bridging predates a recorded specify loop |
| `BRG` | Bridging-stage records | `BRG000` | none (bridging = the markdown bundle) |
| `ARC` | Architecture decisions | `ARC005` | bundle-composition, command-loop-contract, numbering-memory, executable-core, component-pattern |
| `MDL` | SEK models | `MDL001` | MDL001 = engineering-loop state machine |
| `CRD` | CORD explorations | `CRD001` | CRD001 = loop conformance exploration |
| `COV` | Coverage reports | `COV001` | COV001 = conformance/artifact coverage |
| `IN` | Incidents | `IN004` | IN001 = consumer declared ready with no bar met; IN002 = gate conflates verification method with being verified; IN003 = self-model granularity ambiguous for a pipeline vertical; IN004 = gate accepts positive-only / thin self-models (no negative-path conformance, no behavioral-richness floor) |
| `PM` | Post-mortems | `PM004` | PM001 = no readiness gate; PM002 = verification method by module class; PM003 = self-model criterion is behavior-level; PM004 = self-model must be behaviorally rich + prove negative conformance |
| `REF` | Refactor decisions | `REF000` | none yet |

## Local counters

Reset inside each parent; tracked in the parent doc, not here.

| Prefix | Resets per | Recorded in |
|---|---|---|
| `MIT` | Incident | the incident's timeline table |
| `LRN` | Post-mortem | the post-mortem's Learnings section |
| `RPI` | Post-mortem | the post-mortem's Repair Items section |
