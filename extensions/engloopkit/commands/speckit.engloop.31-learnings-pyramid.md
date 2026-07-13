---
name: speckit.engloop.31-learnings-pyramid
description: Validate source/card/index/retrieval learnings completeness and provenance.
argument-hint: "[learning refresh scope]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.31-learnings-pyramid --root .
      timeout: 30
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** pending learning refresh and explicit stewardship capacity.
- **Goal:** deterministic static plus retrieval learnings validation.
- **Actions:** validate source/card/index links, budgets, and retrieval comparisons.
- **Verification:** complete provenance and exact retrieval-set pass.
- **Memory:** `.engloop/learnings/`, root `LEARNINGS.md`, and retrieval results.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.31-learnings-pyramid --root .`

## Done when

- [ ] Source/card/index/retrieval validations pass together
- [ ] Pending refresh is cleared only on full validation pass
