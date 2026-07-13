---
name: speckit.engloop.08-unittest
description: Classify unreached paths, enforce delete/revalidate ordering, then compute final readiness.
argument-hint: "[reachability disposition scope]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.08-unittest --root .
      timeout: 30
handoffs:
  - label: Correct design defect
    agent: speckit.engloop.04-refactor
    prompt: Route the design or architecture defect identified above through the governed SPEC implementation loop.
    send: false
  - label: Model intended gap
    agent: speckit.engloop.05-model
    prompt: Model the authoritative but functionally unreached behavior identified above before regenerating tests.
    send: false
  - label: Revalidate deletion
    agent: speckit.engloop.07-validate
    prompt: Rerun the complete generated functional validation after the coherent residue deletion set above.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** current Stage 07 functional evidence exists.
- **Goal:** complete disposition then sole readiness PASS/FAIL emission.
- **Actions:** classify unreached paths, enforce delete/revalidate sequence, add direct tests only after disposition.
- **Verification:** per-module 95/95 with full inventory and current evidence.
- **Memory:** `.engloop/coverage/COV003_ordered-engloop-v2-readiness.md`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.08-unittest --root .`

## Done when

- [ ] Disposition precedes any new direct tests
- [ ] Final readiness verdict is emitted only by Stage 08
