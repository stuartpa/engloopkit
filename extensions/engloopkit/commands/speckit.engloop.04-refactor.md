---
name: speckit.engloop.04-refactor
description: Execute governed implementation/refactor work under accepted architecture.
argument-hint: "[SPEC task slice or repair scope]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.04-refactor --root .
      timeout: 30
handoffs:
  - label: Model current behavior
    agent: speckit.engloop.05-model
    prompt: Model the accepted architecture-conformant product behavior and its rejection semantics.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** accepted architecture and approved implementation scope.
- **Goal:** coherent governed code/docs/tests updates.
- **Actions:** implement tasks, keep fail-closed behavior, run objective checks.
- **Verification:** build/tests green with required evidence updates.
- **Memory:** `.engloop/refactors/` plus impacted durable artifacts.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.04-refactor --root .`

## Done when

- [ ] Required implementation changes are complete and validated
- [ ] Evidence stays coherent with code changes
