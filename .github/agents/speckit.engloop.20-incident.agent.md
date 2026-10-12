---
name: speckit.engloop.20-incident
description: Stabilize real incidents and record mitigation-only operational evidence.
argument-hint: --incident <.engloop/incidents/INxxx_title.md> [incident demand and
  mitigation scope]
target: vscode
user-invocable: true
disable-model-invocation: true
tools:
- read
- search
- edit
- execute
agents: []
hooks:
  SessionStart:
  - type: command
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.20-incident
      --root .
    timeout: 30
  - type: command
    command: dotnet tool run engloopkit -- operations-hook start incident
    timeout: 30
  UserPromptSubmit:
  - type: command
    command: dotnet tool run engloopkit -- operations-hook initialize incident
    timeout: 30
  Stop:
  - type: command
    command: dotnet tool run engloopkit -- operations-hook stop incident
    timeout: 30
  PostToolUse:
    - type: command
      command: dotnet tool run engloopkit -- operations-hook post-tool incident
      timeout: 30
handoffs:
- label: Analyze incidents for postmortem
  agent: speckit.engloop.21-postmortem
  prompt: ELK_TRUSTED_AUTOPILOT_POSTMORTEM_CONTINUATION Continue the requested incident postmortem in this native session, retaining the incident evidence and explicit operator repair intent above. Use the trusted context collection and binding path; do not ask the user to reconstruct internal flags. Consult current NORTHSTAR.md and the relevant LEARNINGS.md card/source path, classify rule effects, and produce validated learning-bound repair items. Carry unresolved stabilization evidence without claiming completion. After producing or updating the requested postmortem, continue the trusted Stage 22 handoff with ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION when same-session repair intent was captured.
  send: true
---


<!-- Extension: engloop -->
<!-- Config: .specify/extensions/engloop/ -->
## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** actual incident demand exists.
- **Goal:** mitigation/stabilization evidence, not permanent fix.
- **Actions:** read current direction, capture IN/MIT evidence, consult only immediately relevant learning cues when safe, and preserve state.
- **Verification:** incident demand and stabilization proof present.
- **Memory:** `.engloop/incidents/`.

Run the entry diagnostic; a failure is unresolved context, not a navigation veto:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.20-incident --root .`

Stage 20 does not require current readiness. The user may select Stage21
(`speckit.engloop.21-postmortem`), Stage22 (`speckit.engloop.22-repair`), or Stage50
(`speckit.engloop.50-handoff-create`) at any time, including during an active incident.
Carry unresolved readiness, stabilization, scope, and hook diagnostics to the destination;
navigation neither authorizes a protected mutation nor proves completion.

For bound incident evidence, expect `OPERATIONS_LEARNING_GUARD_ACTIVE mode=incident` and
`OPERATIONS_LEARNING_SCOPE_ACTIVE mode=incident`. The prompt names the incident artifact
with `--incident`; a bound Stop hook runs `validate incident-context --allow-deferred true`.
If any incident hook instead reports `OPERATIONS_LEARNING_CONTEXT_DEFERRED` with status
`learning-context-deferred`, keep mitigating immediately. Record the diagnostic, do not
treat deferred context as validated context, and resolve or create the incident artifact
without delaying urgent stabilization. Before claiming stabilization, run the authoritative
`validate incident-context --allow-deferred true` command against that artifact. Missing,
malformed, or unavailable learning metadata never disables the recovery conversation.

Known valid lifecycle failures return `continue=true`, `completionAccepted=false`:
Stop must not block navigation or manufacture PASS. Keep specific mutation authorization
and stabilization claims strict; deferred context is not permission for arbitrary writes
or execution.

If the operator explicitly asks in plain language to start, continue, complete, or
analyze a postmortem, use the existing user-visible Stage 21 handoff; do not invoke
Stage 21 through the `agent` tool. VS Code Local subagents cannot use the interactive
question flow required to obtain a human selection, so delegated Stage 21 runs cannot
produce the selected-option `PostToolUse` receipt. Run Stage 21 as a user-invoked
top-level agent. Do not synthesize internal flags or treat an empty selection as a
decline or confirmation. This preserves user-owned navigation under
`RULE:readiness-is-a-gate` (PM001/LEARN001–003; 2026-09-06 owner directive); it does not
prove stabilization, postmortem completion, or repair closure.

The declared handoff is a validated-evidence workflow, not the user's only exit. If a
requested destination is not an allowed subagent, honor direct user selection instead
of attempting arbitrary delegation or demanding missing internal flags.

## Direction and learning context during stabilization

1. Read root `NORTHSTAR.md` before choosing a mitigation so emergency action does not
  violate a non-negotiable product boundary.
2. Read root `LEARNINGS.md` and follow only a directly relevant card/source cue when that
  can be done without delaying stabilization. Do not preload the pyramid during an
  outage.
3. Record the consulted North Star revision and any consulted `RULE:<card-slug>` /
  `PMxxx/LEARNxxx` evidence in the incident timeline. If urgency requires deferring the
  learning lookup, record that deferral explicitly for Stage 21; never fabricate a cue.
4. Direction/learning consultation never authorizes a permanent fix in Stage 20.

## Done when

- [ ] Incident stabilization evidence is captured
- [ ] North Star was consulted; relevant learning cues were consulted or explicitly deferred
- [ ] No repair closure claim is made