# DBG<NNN> — <debugger walkthrough scope>

- **Created:** <UTC timestamp>
- **Base revision:** <full SHA>
- **Head revision:** <full SHA>
- **Engineer:** <explicitly supplied identity>
- **Status:** IN PROGRESS | COMPLETE | STALE | BLOCKED

## Scope inventory

| Chunk | File and lines | Changed symbols/behavior | Debugger | Breakpoint | Trigger test/command | Status |
|---|---|---|---|---|---|---|
| DBG-CHUNK-001 | `<path>:<start>-<end>` | <scope> | <explicit debugger> | `<path>:<line>` | `<exact command>` | pending |

Every changed executable line must belong to exactly one chunk. List non-executable
changed code separately for explicit line-by-line engineer-read attestation.

## Chunk records

### DBG-CHUNK-001 — <brief scope>

- **Debugger/config source:** <tool and authoritative repo config/skill>
- **Breakpoint reached:** <yes/no and evidence>
- **Trigger result:** <result>
- **Observed path/branches/values:** <engineer's observations>
- **Defects/questions:** <none or findings>
- **Engineer attestation:** <exact response; never agent-authored>
- **Attested at UTC:** <timestamp>
- **Attested HEAD:** <full SHA>

## Non-executable changed code

| File and lines | Reason debugger execution does not apply | Engineer line-by-line read attestation |
|---|---|---|
| <path/range> | <reason> | <exact response and UTC timestamp> |

## Debugger setup learning

- **Existing skill/config used:** <path or none>
- **Setup difficulty:** <none or bounded failed attempt>
- **Skill offered:** <yes/no; create only after explicit approval>

## Completion

- [ ] Base and HEAD remain current
- [ ] Every executable changed line is assigned exactly once
- [ ] Every chunk reached its breakpoint through the recorded trigger
- [ ] Every chunk has explicit engineer attestation at this HEAD
- [ ] Every non-executable change has explicit engineer-read attestation
- [ ] No chunk is pending, blocked, stale, or agent-certified
