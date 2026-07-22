# Numbering Registry (EngLoopKit self-host)

Single source of truth for EngLoopKit's own numbered document counters. **Increment the
"Last used" value here before creating a new document.** Prefix definitions and rules are
in the bundle's [document standards](../docs/standards.md). Artifact root is `.engloop/`.

## Global counters

| Prefix | Scope | Last used | Notes |
|---|---|---|---|
| `SPEC` | Specifications | `SPEC002` | SPEC001 = ordered workflow; SPEC002 = private overlay |
| `SCAF` | Scaffold/test-runway records | `SCAF001` | SCAF001 = deterministic self-host test runway proof |
| `ARCH` | Architecture decisions | `ARCH007` | latest = private overlay boundary |
| `MODEL` | SEK model records | `MODEL001` | MODEL001 = engineering-loop state machine |
| `CORD` | CORD exploration records | `CORD001` | CORD001 = loop conformance exploration |
| `COV` | Coverage/validation/readiness records | `COV003` | COV001 = conformance; COV002 = functional; COV003 = readiness |
| `IN` | Incidents | `IN005` | latest = stale Windows testhost lock incident |
| `PM` | Post-mortems | `PM004` | PM001 = no readiness gate; PM002 = verification method by module class; PM003 = self-model criterion is behavior-level; PM004 = self-model must be behaviorally rich + prove negative conformance |
| `REFACT` | Refactor decisions | `REFACT001` | REFACT001 = ordered EngLoop v2 workflow, Northstar, verification split, and Learnings Pyramid |
| `DBG` | Debugger walkthrough ledgers | `DBG000` | engineer-attested per-chunk debugger walkthrough evidence |
| `POM` | Pomodoro session notes | `POM0000` | four-digit global counter; first note is POM0001 |

## Local counters

Reset inside each parent; tracked in the parent doc, not here.

| Prefix | Resets per | Recorded in |
|---|---|---|
| `MIT` | Incident | the incident's timeline table |
| `LEARN` | Post-mortem | the post-mortem's Learnings section |
| `RPI` | Post-mortem | the post-mortem's Repair Items section |
