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
handoffs:
- label: Analyze stabilized incidents
  agent: speckit.engloop.21-postmortem
  prompt: Analyze the selected stabilized incident set above with --postmortem <.engloop/postmortems/PMxxx_title.md>;
    consult current NORTHSTAR.md and the relevant LEARNINGS.md card/source path, classify
    rule effects, and produce validated learning-bound repair items.
  send: false
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

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.20-incident --root .`

Normally require `OPERATIONS_LEARNING_GUARD_ACTIVE mode=incident` and
`OPERATIONS_LEARNING_SCOPE_ACTIVE mode=incident`. The prompt names the incident artifact
with `--incident`; a bound Stop hook runs `validate incident-context --allow-deferred true`.
If any incident hook instead reports `OPERATIONS_LEARNING_CONTEXT_DEFERRED` with status
`learning-context-deferred`, keep mitigating immediately. Record the diagnostic, do not
treat deferred context as validated context, and resolve or create the incident artifact
without delaying urgent stabilization. Before claiming stabilization, run the authoritative
`validate incident-context --allow-deferred true` command against that artifact. Missing,
malformed, or unavailable learning metadata never disables the recovery conversation.

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