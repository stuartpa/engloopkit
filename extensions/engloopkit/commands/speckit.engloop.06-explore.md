---
name: speckit.engloop.06-explore
description: Explore model scenarios, regenerate deterministic suites, and capture CORD evidence.
argument-hint: "[exploration scenario set]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.06-explore --root .
      timeout: 30
handoffs:
  - label: Repair model deficiency
    agent: speckit.engloop.05-model
    prompt: Revise the behavioral model to address the exploration deficiency identified above before regenerating.
    send: false
  - label: Validate generated suite
    agent: speckit.engloop.07-validate
    prompt: Run the freshly generated suite above against the real SUT and publish functional reachability.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** current accepted model exists.
- **Goal:** bounded exploration and generated suite refresh.
- **Actions:** run explore/generate commands and persist CORD evidence.
- **Verification:** generated suite is fresh and exploration is bounded.
- **Memory:** `.engloop/cord/` and generated test destination.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.06-explore --root .`

## Done when

- [ ] Exploration evidence is fresh and bounded
- [ ] Generated suite is regenerated deterministically
