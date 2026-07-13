---
name: speckit.engloop.30-refactor-scan
description: Select one REFACT decision or no-work outcome under explicit stewardship capacity.
argument-hint: "[stewardship scan scope]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.30-refactor-scan --root .
      timeout: 30
handoffs:
  - label: Update direction
    agent: speckit.engloop.01-northstar
    prompt: Update the living Northstar for the evidence-backed direction change selected above.
    send: false
  - label: Re-derive architecture
    agent: speckit.engloop.03-architect
    prompt: Re-derive governed architecture for the architecture-impacting refactor selected above.
    send: false
  - label: Implement selected refactor
    agent: speckit.engloop.04-refactor
    prompt: Route the selected refactor above through the governed SPEC implementation loop.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** explicit stewardship capacity exists.
- **Goal:** one REFACT decision (or no-work) with evidence.
- **Actions:** evaluate signals, choose first valid branch, record decision.
- **Verification:** exactly one decision emitted.
- **Memory:** `.engloop/refactors/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.30-refactor-scan --root .`

## Done when

- [ ] One REFACT decision or no-work result is recorded
- [ ] Direction/architecture routing implications are explicit
