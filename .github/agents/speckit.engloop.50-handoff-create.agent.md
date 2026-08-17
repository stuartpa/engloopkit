---
name: speckit.engloop.50-handoff-create
description: Capture a concise evidence-backed handoff from the just-completed chat
  work so another chat window or engineering team can continue without reconstructing
  context.
argument-hint: --recipient <team|next-chat> [--title <brief-description>]
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
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.50-handoff-create
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

Create exactly one Markdown artifact under `.engloop/handoffs/` named
`HANDOFF<NNN>-<brief-kebab-description>.md`.

## Loop definition

- **Trigger:** the user needs to continue recent chat work in another chat window or hand a newly discovered issue to another engineering team.
- **Goal:** one self-contained, evidence-backed continuation packet about what just happened.
- **Actions:** inspect only the relevant recent conversation, current Git state/diff, touched files, terminal results, and explicit user statements; reserve the next HANDOFF number; write the handoff.
- **Verification:** the registry was incremented before creation, the filename is unique, paths/commands/results are factual, unresolved work and recipient are explicit, and no secret or unsupported reconstruction is included.
- **Memory:** `.engloop/numbering-registry.md` plus one `.engloop/handoffs/HANDOFF<NNN>-<description>.md`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.50-handoff-create --root .`

## Required behavior

1. Read the `HANDOFF` row in `.engloop/numbering-registry.md`, increment its three-digit
   monotonic counter before creating the artifact, and never reuse an ID.
2. Derive a brief lowercase kebab-case description. The exact filename shape is
   `HANDOFF001-brief-description.md`.
3. Scope the artifact to the recent event the user wants transferred. Do not summarize
   the whole project or invent chat history, intent, elapsed time, ownership, or approval.
4. Prefer current evidence: explicit conversation statements, `git status/diff/log`,
   touched files, command output, test results, diagnostics, and durable artifacts.
5. Use the HANDOFF template. Include recipient, objective, observed problem/outcome,
   reproduction or completed actions, evidence, current repository state, constraints,
   unresolved decisions, and an exact recommended first action for the receiving chat/team.
6. If handing an ELK defect to ELK engineering, distinguish the downstream symptom from
   the generic ELK product issue; remove private workload names, credentials, paths, and
   logs unless the user explicitly authorizes them.
7. A handoff records and transfers work; it does not implement the fix, open a network
   issue, commit, push, deploy, or claim that the recipient accepted it.

## Done when

- [ ] The `HANDOFF` counter was advanced before artifact creation
- [ ] Exactly one `HANDOFF<NNN>-<brief-kebab-description>.md` was created
- [ ] The handoff is self-contained, recipient-bound, factual, and safe to transfer
- [ ] Reproduction/evidence, current state, unresolved work, and first next action are explicit