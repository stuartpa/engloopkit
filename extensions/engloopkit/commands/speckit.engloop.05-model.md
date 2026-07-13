---
name: speckit.engloop.05-model
description: Author and validate the independent behavior model including rejection semantics.
argument-hint: "[model scope or behavior delta]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.05-model --root .
      timeout: 30
handoffs:
  - label: Explore the model
    agent: speckit.engloop.06-explore
    prompt: Explore the accepted model above and generate the functional suite into the proven test runway.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** implementation and architecture are current.
- **Goal:** independent rich behavioral model updates.
- **Actions:** update model state/guards/invariants and validate model build.
- **Verification:** model compiles and captures required negative semantics.
- **Memory:** `.engloop/models/` and `model/EngLoopKit.Model/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.05-model --root .`

## Done when

- [ ] Model captures legal and rejection behavior requirements
- [ ] Model build/validation passes
