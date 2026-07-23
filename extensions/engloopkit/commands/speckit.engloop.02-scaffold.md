---
name: speckit.engloop.02-scaffold
description: Prove the selected test runway and record deterministic Stage 02 evidence.
argument-hint: "[runway framework or proof request]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, web]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.02-scaffold --root .
      timeout: 30
handoffs:
  - label: Walk current slice in debugger
    agent: speckit.engloop.09-debugger-walk-thru
    prompt: Use the proven runway above to prepare an engineer-led debugger walkthrough of the current thin slice before more implementation accumulates.
    send: false
  - label: Derive architecture
    agent: speckit.engloop.03-architect
    prompt: Use the proven scaffold and test runway above to derive and govern the long-lived architecture.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** accepted Northstar exists.
- **Goal:** Stage 02 runway proof and durable scaffold evidence.
- **Actions:** execute deterministic proof runs and persist scaffold outputs.
- **Verification:** build/discovery/pass/fail/re-pass observations recorded.
- **Memory:** `.engloop/scaffolds/` and config runway fields.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.02-scaffold --root .`

## Done when

- [ ] Stage 02 runway observations are complete
- [ ] Durable scaffold evidence is current
