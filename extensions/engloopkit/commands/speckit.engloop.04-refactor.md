---
name: speckit.engloop.04-refactor
description: Implement an accepted SPEC, REFACT plan, or repair scope under accepted architecture; do not select or redesign the refactor plan.
argument-hint: "[accepted SPEC task slice, REFACT plan, or repair scope]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.04-refactor --root .
      timeout: 30
handoffs:
  - label: Model current behavior
    agent: speckit.engloop.05-model
    prompt: Model the accepted architecture-conformant product behavior and its rejection semantics.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** accepted architecture plus one approved implementation scope from SPEC, Stage 40 REFACT planning, or Stage 22 repair routing.
- **Goal:** implement the exact accepted plan as coherent governed code/docs/tests updates.
- **Actions:** read the accepted plan and its ordered slice, implement only that scope, keep fail-closed behavior, and run objective checks.
- **Verification:** the accepted slice is implemented without unapproved scope growth; build/tests and required evidence are green.
- **Memory:** `.engloop/refactors/` plus impacted durable artifacts.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.04-refactor --root .`

## Mandatory implementation guidance

Before changing product code:

1. Read `.engloop/config.json`, resolve its exact `northstarPath`, and read the current
  living North Star (normally root `NORTHSTAR.md`).
2. Read the exact accepted SPEC task, REFACT plan/slice, or repair acceptance being
  implemented. For a REFACT slice, verify its recorded North Star identity/alignment.
3. Read every governing `.engloop/architecture/ARCH*.md` cited by the accepted plan and
  any architecture decisions directly applicable to the touched boundary. Treat those
  decisions as binding implementation guidance.
4. Read `docs/component-pattern.md` when the slice moves or introduces generic code.
  Preserve vertical → component dependency direction and keep domain knowledge out of
  components.
5. If current North Star or architecture differs materially from the accepted plan,
  stop and route back to Stage 40 Refactor Plan or Stages 01/03. Do not implement a
  stale plan.

## Implementation-only boundary

Stage 04 executes a plan; it does not choose among refactor candidates or invent a new
repository-wide strategy.

1. Identify the exact accepted SPEC task slice, REFACT plan/slice (`REFACTxxx`), or routed repair.
  If none is explicit, stop rather than inferring one.
2. Preserve the plan's declared scope, component/vertical boundaries, dependencies,
  acceptance checks, and ordered slices. Do not bundle adjacent cleanup.
3. If implementation discovers a direction decision, return to Stage 01. If it discovers
  an ungoverned architecture decision, return to Stage 03. If it needs candidate comparison,
  a different component boundary, or wider planning, return to
  `/speckit.engloop.40-refactor-plan`.
4. Product source, tests, models, and configuration may change only as required by the
  accepted implementation slice.
5. Apply verification by artifact class: direct unit/property evidence for generic
  components and applicable SEK behavior evidence for the stateful vertical.

## Done when

- [ ] The exact accepted plan and implementation slice are identified
- [ ] Required implementation changes are complete and validated
- [ ] No refactor candidate was selected or scope widened inside Stage 04
- [ ] Evidence stays coherent with code changes
