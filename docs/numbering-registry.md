# Ordered EngLoop numbering registry

This is the public template for a governed repository’s tracked
`.engloop/numbering-registry.md`. The selected repository root owns its own counters;
no parent, sibling, or prior process root is consulted.

Increment a global counter before creating its artifact. Local counters reset in the
parent artifact and are tracked there.

## Global counters

| Prefix | Last used | Notes |
|---|---:|---|
| `SPEC` | `SPEC000` | Governed specification/refactor records. |
| `SCAF` | `SCAF000` | Scaffold/test-runway proof records. |
| `ARCH` | `ARCH000` | Architecture decisions. |
| `MODEL` | `MODEL000` | Independent behavior models. |
| `CORD` | `CORD000` | Bounded exploration records. |
| `COV` | `COV000` | Functional/readiness validation evidence. |
| `IN` | `IN000` | Actual incidents only. |
| `PM` | `PM000` | Selected stabilized incident-set postmortems. |
| `REFACT` | `REFACT000` | Stewardship refactor decisions or no-work records. |
| `DEADCODE` | `DEADCODE000` | High-certainty dead-code proposals and user decisions. |
| `HAPPY` | `HAPPY000` | Moments when things worked wonderfully and the readily available context worth remembering. |
| `DBG` | `DBG000` | Engineer-attested debugger walkthrough ledgers. |
| `SIX` | `SIX000` | Six-page narrative memos, generated Word documents, appendices, and render evidence. |
| `PPT` | `PPT000` | Markdown-first presentation decks and generated PowerPoint artifacts. |
| `PAP` | `PAP000` | Academic systems papers, bibliography/figures/data, generated PDF, and validation. |
| `HANDOFF` | `HANDOFF000` | Evidence-backed continuation packets for another chat window or engineering team. |

## Local counters

| Prefix | Resets per | Recorded in |
|---|---|---|
| `MIT` | `IN` | Incident timeline. |
| `LEARN` | `PM` | Postmortem learning section. |
| `RPI` | `PM` | Postmortem repair-item section. |

See [standards.md](standards.md) for the ordering, readiness, and operations rules.
