---
name: speckit.engloop.40-pomodoro-create
description: Capture concise evidence-backed notes about the work completed in the last 30 to 60 minutes.
argument-hint: "[brief session description]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.40-pomodoro-create --root .
      timeout: 30
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Create one note under `.engloop/pomodoros/` named
`POM<NNNN>-<brief-kebab-description>.md`.

## Loop definition

- **Trigger:** the user wants a factual record of the just-completed 30–60 minute work session.
- **Goal:** one concise POM note describing what just happened—nothing more.
- **Actions:** inspect current conversation/session evidence, Git changes, recent commands, and validation output; reserve the next POM number; write the note.
- **Verification:** the counter was incremented before creation, the filename is unique, and every claim is supported by current-session evidence.
- **Memory:** `.engloop/numbering-registry.md` plus one `.engloop/pomodoros/POM<NNNN>-<description>.md` note.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.40-pomodoro-create --root .`

## Required behavior

1. Read the `POM` row in `.engloop/numbering-registry.md`. The initial value is
   `POM0000`; increment it before creating the note. Numbers are four digits, monotonic,
   and never reused.
2. Derive a brief lowercase kebab-case description from the main completed work. The exact
   filename shape is `POM0001-desc-of-what-is-in-the-pom.md`.
3. Capture only the last session (normally 30–60 minutes). Do not summarize the project,
   invent elapsed time, start a timer, or turn the note into a future plan.
4. Prefer evidence from the current conversation, `git diff/status/log`, terminal output,
   and files touched. Clearly mark uncertainty instead of reconstructing unsupported facts.
5. Keep the note brief using the POM template: intent, what happened, decisions, touched
   artifacts, verification, and unresolved handoff (if any).
6. In overlay mode, the note remains local because `.engloop/` is overlay-managed and is
   included by overlay pack unless removed before packing.

## Done when

- [ ] The registry was advanced before file creation
- [ ] Exactly one unique `POM<NNNN>-<brief-description>.md` note was created
- [ ] The note covers only the just-completed session and contains no invented facts
- [ ] Verification evidence and any unresolved handoff are concise and explicit
