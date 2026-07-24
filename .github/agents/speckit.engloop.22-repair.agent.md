---
name: speckit.engloop.22-repair
description: Route repair obligations through governed implementation and verification
  gates.
argument-hint: '[repair obligation id]'
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
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.22-repair
      --root .
    timeout: 30
handoffs:
- label: Begin governed repair
  agent: speckit.engloop.04-refactor
  prompt: Implement the repair item above through the governed SPEC loop before downstream
    verification.
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

- **Trigger:** at least one accepted repair item exists.
- **Goal:** full repair closure through 04 and applicable 05-08 gates.
- **Actions:** execute governed repair flow and capture closure evidence.
- **Verification:** no bypass; closure requires downstream current readiness PASS.
- **Memory:** `.engloop/postmortems/` and repair evidence records.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.22-repair --root .`

## Done when

- [ ] Repair route includes required downstream gates
- [ ] Closure evidence is complete and current