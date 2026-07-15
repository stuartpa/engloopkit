---
name: speckit.engloop.01-northstar
description: Define or evolve the singleton root NORTHSTAR.md from evidence-backed direction.
argument-hint: "[direction evidence or REFACT input]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, web, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.01-northstar --root .
      timeout: 30
handoffs:
  - label: Create working scaffold
    agent: speckit.engloop.02-scaffold
    prompt: Use the accepted Northstar above to create the thin working slice and prove the test runway.
    send: false
  - label: Re-derive architecture
    agent: speckit.engloop.03-architect
    prompt: Use the revised Northstar and current working evidence above to re-derive the governed architecture.
    send: false
  - label: Refactor under existing architecture
    agent: speckit.engloop.04-refactor
    prompt: Use the accepted Northstar and existing governed architecture above to begin the SPEC-driven refactor.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** new repository direction or evidence-backed direction change.
- **Goal:** one complete root `NORTHSTAR.md` that expresses a timeless North Star.
- **Actions:** gather evidence, preserve current source facts, edit/create the singleton North Star.
- **Verification:** singleton direction file and all required sections present.
- **Memory:** root `NORTHSTAR.md` and durable `.engloop/` records.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.01-northstar --root .`

## North Star authoring rules

- Write enduring direction: outcomes, invariants, boundaries, evidence, and desired
  capability direction—not calendar commitments or an execution roadmap.
- Do not introduce `Phase`, `quarter`, `sprint`, `milestone date`, `release date`, or
  `week` language unless the user explicitly asks for date-based scheduling.
- If ordered dependency boundaries are genuinely needed, use a `## Staged capability
  sequence` heading and label entries `Stage N — <name>`.
- A Stage is a capability, evidence, governance, or safety prerequisite. It is not elapsed
  time, a sprint, a delivery promise, or a release commitment.
- Put task breakdowns, implementation plans, schedules, and date-based commitments in
  separate planning artifacts; do not make them part of the living North Star.

## Done when

- [ ] Exactly one root `NORTHSTAR.md` is authoritative
- [ ] Required sections are complete and evidence-backed
- [ ] Direction is timeless unless the user explicitly requested scheduling
- [ ] Any ordered capability sequence uses Stage labels and prerequisite language
- [ ] No legacy numbered direction artifacts are created
