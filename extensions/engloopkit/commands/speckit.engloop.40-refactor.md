---
name: speckit.engloop.40-refactor
description: Select one REFACT decision or no-work outcome under explicit stewardship capacity.
argument-hint: "[stewardship scope]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.40-refactor --root .
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
  - label: Inspect certain dead code
    agent: speckit.engloop.41-deadcode
    prompt: Search for the single highest-certainty dead-code candidate. Create a numbered DEADCODE proposal with deletion-proof evidence before asking whether to remove it.
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
- **Actions:** evaluate signals, choose the first valid branch, and record one decision.
- **Verification:** exactly one decision is emitted and its routing is explicit.
- **Memory:** `.engloop/refactors/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.40-refactor --root .`

## Done when

- [ ] One REFACT decision or no-work result is recorded
- [ ] Direction, architecture, implementation, or dead-code routing is explicit
