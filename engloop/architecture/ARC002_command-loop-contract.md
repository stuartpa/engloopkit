# ARC002: The command–loop contract

- **Created:** 2026-07-06
- **Status:** ACCEPTED
- **Governs:** every command in the `engloopkit` extension

## Decision

Every EngLoopKit command is a **Loop Engineering loop**: it declares a **Trigger**, a
**Goal**, **Actions**, a **Verification**, and **Memory**, plus a **Done-when** checklist
and an **Artifact root** note. This is the uniform contract that makes the commands
composable stages of one engineering loop.

## Context (from the bridging code)

The bridging commands were already written to this shape. Making it an explicit,
enforced contract turns a convention into an invariant that later loops (and contributors)
cannot silently break.

## The rule

- A command file MUST have YAML frontmatter with a `description`.
- It MUST contain a `## Loop definition` with `**Trigger:**`, a Goal (`**Goal…`),
  `**Verification:**`, and `**Memory:**`.
- It MUST contain an `## Artifact root` note and a `## Done when` checklist.
- Paths are written under `<ARTIFACT_ROOT>/` (default `docs/`; overridable).

## Enforcement

The conformance test `Command_isWellFormedAsALoop` (a `[Theory]` over all nine commands)
checks each of these mechanically. A malformed command fails CI.

## Consequences

- Every stage is a real, self-describing loop — not a one-shot prompt.
- New commands are added by conforming to the same contract; the test guarantees it.
