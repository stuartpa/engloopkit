---
name: speckit.engloop.01-northstar
description: Define or evolve the singleton root NORTHSTAR.md from evidence-backed direction.
argument-hint: "[direction evidence or REFACT input]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, web, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.01-northstar --root .
      timeout: 30
handoffs:
  - label: Create working scaffold
    agent: speckit.engloop.02-scaffold
    prompt: Use the accepted Northstar above to create the thin working slice and prove the test runway.
    send: false
  - label: Re-derive architecture
    agent: speckit.engloop.03-architect
    prompt: Use the revised Northstar and current working evidence above to re-derive the governed architecture.
    send: false
  - label: Refactor under existing architecture
    agent: speckit.engloop.04-refactor
    prompt: Use the accepted Northstar and existing governed architecture above to begin the SPEC-driven refactor.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** new repository direction or evidence-backed direction change.
- **Goal:** one complete root `NORTHSTAR.md`.
- **Actions:** gather evidence, preserve current source facts, edit/create singleton Northstar.
- **Verification:** singleton direction file and all required sections present.
- **Memory:** root `NORTHSTAR.md` and durable `.engloop/` records.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.01-northstar --root .`

## Done when

- [ ] Exactly one root `NORTHSTAR.md` is authoritative
- [ ] Required sections are complete and evidence-backed
- [ ] No legacy numbered direction artifacts are created
