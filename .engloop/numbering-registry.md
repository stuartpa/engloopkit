# Numbering Registry (EngLoopKit self-host)

Single source of truth for EngLoopKit's own numbered document counters. **Increment the
"Last used" value here before creating a new document.** Prefix definitions and rules are
in the bundle's [document standards](../docs/standards.md). Artifact root is `.engloop/`.

## Global counters

| Prefix | Scope | Last used | Notes |
|---|---|---|---|
| `SPEC` | Specifications | `SPEC002` | SPEC001 = ordered workflow; SPEC002 = private overlay |
| `SCAF` | Scaffold/test-runway records | `SCAF001` | SCAF001 = deterministic self-host test runway proof |
| `ARCH` | Architecture decisions | `ARCH009` | latest = direction/pyramid-bound operations learning gate |
| `MODEL` | SEK model records | `MODEL002` | latest = direction/pyramid-bound operations behavior |
| `CORD` | CORD exploration records | `CORD002` | latest = operations learning/gate conformance exploration |
| `COV` | Coverage/validation/readiness records | `COV003` | COV001 = conformance; COV002 = functional; COV003 = readiness |
| `IN` | Incidents | `IN006` | latest = tracked SpecKit registry blocks private overlay coexistence |
| `PM` | Post-mortems | `PM004` | PM001 = no readiness gate; PM002 = verification method by module class; PM003 = self-model criterion is behavior-level; PM004 = self-model must be behaviorally rich + prove negative conformance |
| `REFACT` | Refactor decisions | `REFACT001` | REFACT001 = ordered EngLoop v2 workflow, Northstar, verification split, and Learnings Pyramid |
| `DEADCODE` | High-certainty dead-code proposals | `DEADCODE000` | explicit user approval is required before current-source removal |
| `HAPPY` | Happy Minute records | `HAPPY000` | moments of gratitude and readily available conditions worth repeating |
| `DBG` | Debugger walkthrough ledgers | `DBG000` | engineer-attested per-chunk debugger walkthrough evidence |
| `SIX` | Six-page narrative memos | `SIX000` | authoritative Markdown, generated DOCX, appendices, and render validation |
| `PPT` | Presentation decks | `PPT000` | Markdown-first evidence-backed presentation and generated PPTX |
| `PAP` | Academic systems papers | `PAP000` | authoritative Markdown/BibTeX, generated PDF, figures/data, and review validation |
| `HANDOFF` | Chat/team continuation packets | `HANDOFF001` | latest = DsMainDev one-time ELK v1.14.0 bootstrap; future updates use Stage 80 |

## Local counters

Reset inside each parent; tracked in the parent doc, not here.

| Prefix | Resets per | Recorded in |
|---|---|---|
| `MIT` | Incident | the incident's timeline table |
| `LEARN` | Post-mortem | the post-mortem's Learnings section |
| `RPI` | Post-mortem | the post-mortem's Repair Items section |
