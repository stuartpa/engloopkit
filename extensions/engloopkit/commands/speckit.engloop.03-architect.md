---
name: speckit.engloop.03-architect
description: Derive and govern architecture from current direction and scaffold evidence.
argument-hint: "[architecture scope or change request]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.03-architect --root .
      timeout: 30
handoffs:
  - label: Start governed refactor
    agent: speckit.engloop.04-refactor
    prompt: Use the accepted architecture above to run the governed specify → plan → tasks → implement loop.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** Stage 02 runway proof is current.
- **Goal:** governed architecture decision artifact(s).
- **Actions:** evaluate boundaries, update architecture artifacts, run architecture checks.
- **Verification:** architecture evidence passes and references current direction.
- **Memory:** `.engloop/architecture/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.03-architect --root .`

## Done when

- [ ] Architecture artifacts are updated and validated
- [ ] Stage ownership and boundaries are explicit
