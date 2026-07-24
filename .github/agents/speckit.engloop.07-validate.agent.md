---
name: speckit.engloop.07-validate
description: Execute generated-only functional validation and produce reachability
  evidence.
argument-hint: '[functional validation run]'
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
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.07-validate
      --root .
    timeout: 30
handoffs:
- label: Fix SUT defect
  agent: speckit.engloop.04-refactor
  prompt: Route the SUT defect identified above through the governed SPEC implementation
    loop.
  send: false
- label: Fix model gap
  agent: speckit.engloop.05-model
  prompt: Revise the model to address the fidelity or behavior gap identified above.
  send: false
- label: Fix exploration gap
  agent: speckit.engloop.06-explore
  prompt: Regenerate exploration and tests to address the coverage or generation gap
    identified above.
  send: false
- label: Classify reachability
  agent: speckit.engloop.08-unittest
  prompt: Use the valid Stage 07 evidence above to classify every unreached path before
    adding unit tests.
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

- **Trigger:** fresh generated suite exists.
- **Goal:** functional-only validation verdict and reachability map.
- **Actions:** run generated suite against real SUT and collect functional evidence.
- **Verification:** generated-only functional checks pass and evidence is current.
- **Memory:** `.engloop/coverage/COV002_ordered-engloop-v2-functional.md`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.07-validate --root .`

## Done when

- [ ] Functional-only evidence is published
- [ ] No readiness verdict is emitted by Stage 07