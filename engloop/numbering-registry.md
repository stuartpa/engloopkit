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
| `IN` | Incidents | `IN000` | none yet — Stage 6 is next |
| `PM` | Post-mortems | `PM000` | none yet |
| `REF` | Refactor decisions | `REF000` | none yet |

## Local counters

Reset inside each parent; tracked in the parent doc, not here.

| Prefix | Resets per | Recorded in |
|---|---|---|
| `MIT` | Incident | the incident's timeline table |
| `LRN` | Post-mortem | the post-mortem's Learnings section |
| `RPI` | Post-mortem | the post-mortem's Repair Items section |
