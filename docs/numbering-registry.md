# Numbering Registry

This is the single source of truth for EngLoopKit document counters. **Increment the
"Last used" value here before creating a new document**, then create the file with the
next number. See [standards.md](standards.md) for prefix definitions and rules.

Global counters run across the whole project. Local counters reset inside their parent
document (mitigations reset per incident; learnings and repair items reset per
post-mortem).

## Global counters

| Prefix | Scope | Last used | Notes |
|---|---|---|---|
| `SEED` | Gathering docs | `SEED000` | none yet |
| `SP` | Specs | `SP000` | none yet |
| `ARC` | Architecture decisions | `ARC000` | none yet |
| `MDL` | SEK models | `MDL000` | none yet |
| `CRD` | CORD explorations | `CRD000` | none yet |
| `COV` | Coverage reports | `COV000` | none yet |
| `IN` | Incidents | `IN000` | none yet |
| `PM` | Post-mortems | `PM000` | none yet |
| `REF` | Refactor decisions | `REF000` | none yet |

## Local counters

These reset inside each parent. Track the highest used per parent in the parent
document itself, not here.

| Prefix | Resets per | Recorded in |
|---|---|---|
| `MIT` | Incident | the incident's timeline table |
| `LRN` | Post-mortem | the post-mortem's Learnings section |
| `RPI` | Post-mortem | the post-mortem's Repair Items section |
