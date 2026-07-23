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
| `DBG` | `DBG000` | Engineer-attested debugger walkthrough ledgers. |
| `PPT` | `PPT000` | Markdown-first presentation decks and generated PowerPoint artifacts. |
| `POM` | `POM0000` | Brief four-digit records of completed 30–60 minute work sessions. |

## Local counters

| Prefix | Resets per | Recorded in |
|---|---|---|
| `MIT` | `IN` | Incident timeline. |
| `LEARN` | `PM` | Postmortem learning section. |
| `RPI` | `PM` | Postmortem repair-item section. |

See [standards.md](standards.md) for the ordering, readiness, and operations rules.
