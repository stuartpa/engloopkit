---
name: speckit.engloop.42-learnings-pyramid
description: Validate source/card/index/retrieval learnings completeness and provenance.
argument-hint: '[learning refresh scope]'
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
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.42-learnings-pyramid
      --root .
    timeout: 30
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

- **Trigger:** pending learning refresh and explicit stewardship capacity.
- **Goal:** deterministic static plus retrieval learnings validation.
- **Actions:** validate PM/HAPPY source-card-index links, budgets, and retrieval comparisons; optionally accept selected HAPPY records as positive provenance.
- **Verification:** complete provenance and exact retrieval-set pass.
- **Memory:** `.engloop/learnings/`, root `LEARNINGS.md`, and retrieval results.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.42-learnings-pyramid --root .`

## Positive provenance from Happy Minutes

- A HAPPY record is complete and historical even when it is never condensed.
- Review only a HAPPY record explicitly handed to this stage. Do not scan all Happy
  Minutes for mandatory work.
- When a genuinely reusable positive practice is accepted, change only its
  `Stage 42 candidate` field from `NOT-YET` to `YES`, cite the stable `HAPPY<NNN>` ID in
  the relevant card's `## Source learnings`, update the root index/retrieval case when
  needed, and run the full validator.
- `NO` or `NOT-YET` HAPPY records remain outside the mandatory source-coverage set.
- Preserve the original gratitude and observations. Never rewrite them as proven causes,
  universal rules, or mandatory process merely to make a card.

## Done when

- [ ] Source/card/index/retrieval validations pass together
- [ ] Any accepted HAPPY source is explicitly `YES`, cited as `HAPPY<NNN>`, and remains faithful to the original record
- [ ] Pending refresh is cleared only on full validation pass