# PM<NNN>: <short title>

- **Date:** <date>
- **Duration:** <HH:MM total across incidents>
- **Covers incidents:** IN<..>, IN<..>
- **Status:** COMPLETE

## Timeline

| Time | Event |
|---|---|
| | |

## Selected stabilized incidents

| Incident ID | Path | SHA-256 |
|---|---|---|
| `IN<NNN>` | `.engloop/incidents/IN<NNN>_<title>.md` | `<sha256>` |

## Root causes

### Primary cause: <title>

- **What failed:** <system/component>
- **Why it failed:** <mechanism>
- **Why we didn't catch it:** <why verification/testing missed it>

### Contributing factor: <title, if any>

## Five whys

```
Symptom: <what users saw>
Why #1: Q: … A: …
Why #2: Q: … A: …
Why #3: Q: … A: …
Why #4: Q: … A: …
Why #5: Q: … A: …   (systemic level)
```

## ONE-AND-DONE analysis

For each root cause: abstract the concrete bug to its class, then a structural fix that
makes the class mechanically impossible.

- **Concrete bug:** …
- **Bug class:** …
- **Structural fix (mechanical, class-preventing, verifiable):** …

## SEK Test-Escape Analysis

- **SEK applicability:** `<RELEVANT | NOT-RELEVANT>`
- **SEK applicability rationale:** <why the incident cause should or should not have been captured by model-based testing>
- **SEK version:** `<0.1.3 | NOT-REQUIRED>`
- **SEK verification class:** `<STATEFUL-VERTICAL | NON-STATEFUL-COMPONENT | INFRASTRUCTURE | DOCUMENTATION | EXTERNAL-DEPENDENCY>`
- **SEK escape class:** `<MODEL-GAP | CORD-DOMAIN-GAP | CORD-SLICE-GAP | CORD-BOUND-GAP | BINDING-GAP | ORACLE-GAP | STALE-GENERATION | SEK-ENGINE-GAP | NOT-RELEVANT>`
- **SEK scenario ID:** `<SEK-SCENARIO:brief-kebab-id | NOT-REQUIRED>`
- **SEK model paths:** `<comma-separated root-relative .cs paths | NOT-REQUIRED>`
- **SEK CORD paths:** `<comma-separated root-relative .cord paths | NOT-REQUIRED>`
- **SEK generated suite path:** `<root-relative generated test directory | NOT-REQUIRED>`
- **Why SEK tests missed the incident:** <specific model, Cord, binding, oracle, freshness, engine, or artifact-class explanation>
- **Required model/CORD repair:** <specific scenario/domain/slice/bound/oracle change | NOT-REQUIRED>

Always decide applicability. When `RELEVANT`, inspect the installed SEK v0.1.3 skills,
current model, Cord, exploration, generated source, and replay evidence. Identify the
missing incident scenario and why the existing proof admitted the escape. Do not blame
"coverage" generically. When `NOT-RELEVANT`, give the exact artifact-class reason.

## Direction and Learning-Pyramid Consultation

- **North Star path:** `NORTHSTAR.md`
- **North Star SHA-256:** `<current-sha256>`
- **Direction alignment:** `<ALIGNED | TENSION | GAP>`
- **Direction decision:** <how the current North Star constrains this analysis/repair>
- **Learnings index path:** `LEARNINGS.md`
- **Learnings index SHA-256:** `<current-sha256>`
- **Pyramid digest:** `<sha256 of current index, cards, and prior source PMs>`
- **Pyramid decision:** `<UPDATED | NO-CHANGE>`
- **Pyramid rationale:** <why the living pyramid changes or explicitly does not change>
- **Historical coverage decision:** `<UPDATED | NO-CHANGE>`
- **Historical coverage path:** `<.engloop/learnings/README.md | NOT-REQUIRED>`
- **Changed pyramid paths:** `<comma-separated paths | NOT-REQUIRED>`
- **Retrieval impact:** `<CHANGED | UNCHANGED>`
- **Retrieval evidence:** `<.engloop/out/...json | NOT-REQUIRED>`
- **Retrieval rationale:** <why retrieval changed or why provenance-only/no-change leaves queries stable>

### Rule dispositions

| Rule ID | Card ID | Source IDs | Disposition | Incident evidence | Pyramid action |
|---|---|---|---|---|---|
| `RULE:<card-slug>` | `<card-slug>` | `PMxxx/LEARNxxx` | `<REINFORCED | CONTRADICTED | MISSING>` | | |

Use stable `RULE:<card-slug>` identities. Follow `LEARNINGS.md → card → PMxxx/LEARNxxx`.
If a rule was missing and `NO-CHANGE` is explicitly chosen, Card ID may be `-`; otherwise
updated cards/provenance must exist and deterministic pyramid validation must pass.

## Learnings

Choose exactly one form:

- **LEARN001 (`PM<NNN>/LEARN001`)** — <class-level insight; requires Pyramid decision UPDATED and immediate living provenance coverage>

or

- **No accepted source learning:** <substantive reason the existing rule already captures the class; Pyramid decision must be NO-CHANGE>

## Repair Items

> Each RPI must be specific enough to hand to `/speckit.engloop.22-repair`.

| RPI | Description (ONE-AND-DONE) | Size (tiny/full) | Spec/tinyspec | Status |
|---|---|---|---|---|
| RPI001 | | | (pending) | OPEN |

### RPI001 learning contract

- **Rule IDs:** `RULE:<card-slug>`
- **Executable gate:** `["<executable>", "<arg1>", "<arg2>"]`
- **Gate proves:** <observable invariant/rejection/outcome established by the command>
- **SEK applicability:** `<RELEVANT | NOT-RELEVANT>`
- **SEK scenario ID:** `<same SEK-SCENARIO:id | NOT-REQUIRED>`
- **SEK repair requirement:** <specific model/CORD/generated-scenario correction | NOT-REQUIRED>
- **SEK verification gate:** `<same JSON argument vector as Executable gate when relevant | NOT-REQUIRED>`

## Cause-class tags

<state-drift | dependency-failure | resource-exhaustion | bug-regression | deployment-incomplete | process-gap | validation-gap>

## References

- Incidents: docs/incidents/IN<..>.md
- Architecture: ARC<..>
- Recurrence of: <prior PM, if any>

## Approvals

- [ ] ONE-AND-DONE fixes reviewed for structural soundness
- [ ] Learnings accepted
- [ ] Repair Items routed via `/speckit.engloop.22-repair`
- [ ] Closed when all Repair Items verified in the target environment
