---
name: speckit.engloop.20-incident
description: Stabilize real incidents and record mitigation-only operational evidence.
argument-hint: "[incident demand and mitigation scope]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.20-incident --root .
      timeout: 30
handoffs:
  - label: Analyze stabilized incidents
    agent: speckit.engloop.21-postmortem
    prompt: Analyze the selected stabilized incident set above and produce source learnings and repair items.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** actual incident demand exists.
- **Goal:** mitigation/stabilization evidence, not permanent fix.
- **Actions:** capture IN/MIT evidence and preserve state.
- **Verification:** incident demand and stabilization proof present.
- **Memory:** `.engloop/incidents/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.20-incident --root .`

## Done when

- [ ] Incident stabilization evidence is captured
- [ ] No repair closure claim is made
