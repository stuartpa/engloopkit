---
name: speckit.engloop.23-happy-minute
description: Record a moment when a live system, repository workflow, or engineering experience worked wonderfully, preserving gratitude and the readily available conditions worth repeating.
argument-hint: "[what worked perfectly] [--title <brief-description>] [--repos <path,...>]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.23-happy-minute --root .
      timeout: 30
handoffs:
  - label: Preserve reusable positive learnings
    agent: speckit.engloop.42-learnings-pyramid
    prompt: Review the HAPPY record above only when stewardship capacity exists. Condense genuinely reusable positive practices into the Learnings Pyramid with provenance; do not turn gratitude into a mandatory process or infer causation beyond the record.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Create exactly one Markdown artifact under `.engloop/happy-minutes/` named
`HAPPY<NNN>-<brief-kebab-description>.md`.

## Loop definition

- **Trigger:** the user says something worked wonderfully and wants to preserve the moment and the conditions worth repeating.
- **Goal:** one warm, factual Happy Minute record that celebrates the outcome and captures readily available live/runtime/repository context for future decisions.
- **Actions:** accept the user's description as sufficient; reserve the next HAPPY number; record the glorious outcome; inspect only readily available evidence and explicitly supplied repositories; write one concise record.
- **Verification:** the counter advanced before creation, the filename is unique, gratitude remains prominent, available facts are labeled by source, missing details say `NOT-PROVIDED`, and sensitive data is redacted.
- **Memory:** `.engloop/numbering-registry.md` plus one `.engloop/happy-minutes/HAPPY<NNN>-<description>.md`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.23-happy-minute --root .`

## Happy-first behavior

1. The user's description is enough to create the Happy Minute. Do not require readiness,
   deployment provenance, a clean working tree, complete commit attribution, production
   authentication, or causal proof. Give the person a break—they are recording gratitude.
2. Start with what was wonderful, why it felt exceptional, and who/what deserves thanks.
   Preserve the user's tone without exaggerating facts they did not state.
3. Read the `HAPPY` row in `.engloop/numbering-registry.md`, increment its three-digit
   monotonic counter before creation, and never reuse an ID.
4. Derive a brief lowercase kebab-case title. The exact filename shape is
   `HAPPY001-everything-worked-perfectly.md`.

## Context worth preserving

Capture details only when they are readily available from the current conversation,
explicit user input, safe read-only commands, or supplied evidence:

- **Live/runtime context:** environment label, release/deployment ID, artifact or image
  digest, service/version/config identity, and observed time/window.
- **Repository context:** always inspect the exact current Git root as investigation
  context. Inspect additional repositories only when explicitly supplied by `--repos`.
  For each inspected repo, record safe remote identity, branch, HEAD, upstream, tags at
  HEAD, and clean/dirty status.
- **Attribution:** distinguish `LIVE/DEPLOYED`, `USER-DESCRIBED`, `LOCAL-CONTEXT`, and
  `NOT-PROVIDED`. A local checkout is not automatically the code running live.
- **What aligned:** tests, model/CORD evidence, agent/profile choices, tooling, deployment,
  observability, team decisions, or other conditions the user says contributed.
- **Repeatable clues:** practices worth trying again, clearly labeled as observations—not
  universal rules or proven causes.

Do not auto-discover parent/sibling repositories, mutate live systems, interrogate
credentials, block on unavailable information, or manufacture deployment-to-commit links.
Use `NOT-PROVIDED` without apology when a detail is unknown.

For each supplied repository, use safe read-only Git observations where available:
`git rev-parse --show-toplevel`, `git remote get-url origin` (redact before recording),
`git branch --show-current`, `git rev-parse HEAD`, `git rev-parse --abbrev-ref
--symbolic-full-name @{upstream}`, `git tag --points-at HEAD`, and
`git status --porcelain=v1 --untracked-files=all`. A failed optional observation becomes
`NOT-PROVIDED`; it does not fail the Happy Minute.

## Safety and boundaries

- Redact credentials, tokens, customer data, private hostnames, and unnecessary private
  absolute paths. Prefer safe hashes/IDs and root-relative paths.
- Do not change source, configuration, deployments, Git state, or learning cards.
- Do not turn a Happy Minute into an incident, audit, release gate, mandatory checklist,
  or claim that every recorded condition caused the success.
- The optional Stage 42 handoff is review-first. It may later preserve reusable positive
  learnings, but the HAPPY record is complete without that handoff.

## Done when

- [ ] The `HAPPY` counter was advanced before artifact creation
- [ ] Exactly one `HAPPY<NNN>-<brief-kebab-description>.md` was created
- [ ] The user's happy description and gratitude are the center of the record
- [ ] Readily available live/repository details are captured with source labels
- [ ] Missing details are `NOT-PROVIDED`, sensitive details are redacted, and no causal proof is invented
