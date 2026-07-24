---
name: speckit.engloop.21-postmortem
description: Analyze selected stabilized incidents into PM/LEARN/RPI outputs.
argument-hint: '[selected incident set]'
target: vscode
user-invocable: true
disable-model-invocation: true
tools:
- read
- search
- edit
- execute
- agent
agents:
- Explore
hooks:
  SessionStart:
  - type: command
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.21-postmortem
      --root .
    timeout: 30
handoffs:
- label: Repair selected item
  agent: speckit.engloop.22-repair
  prompt: Route the selected RPI above through Stage 04 and all applicable Stage 05–08
    gates.
  send: false
- label: Condense learnings when capacity exists
  agent: speckit.engloop.31-learnings-pyramid
  prompt: When spare stewardship capacity exists, condense the accepted learning backlog
    above and validate retrieval.
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

- **Trigger:** selected stabilized incident set exists.
- **Goal:** PM with source LEARN links and RPI repair items.
- **Actions:** perform systemic analysis and emit PM artifacts.
- **Verification:** selection is non-empty and outputs are complete.
- **Memory:** `.engloop/postmortems/` and `.engloop/learnings/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.21-postmortem --root .`

## Done when

- [ ] PM/LEARN/RPI outputs are complete and linked
- [ ] Repair routing is explicit