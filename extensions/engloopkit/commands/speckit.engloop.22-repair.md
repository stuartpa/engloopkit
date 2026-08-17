---
name: speckit.engloop.22-repair
description: Route repair obligations through governed implementation and verification gates.
argument-hint: "--phase <route|close> --postmortem <path> --rpi <RPIxxx> --rules <RULE:id,...> --acceptance <.engloop/repairs/PMxxx-RPIxxx.<route|close>.json>"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.22-repair --root .
      timeout: 30
    - type: command
      command: dotnet tool run engloopkit -- operations-hook start repair
      timeout: 30
  UserPromptSubmit:
    - type: command
      command: dotnet tool run engloopkit -- operations-hook initialize repair
      timeout: 30
  Stop:
    - type: command
      command: dotnet tool run engloopkit -- operations-hook stop repair
      timeout: 30
handoffs:
  - label: Begin governed repair
    agent: speckit.engloop.04-refactor
    prompt: Implement the selected repair under its exact PM Rule IDs and executable gate. Preserve both in SPEC acceptance, run the gate after implementation, then complete applicable Stage 05–08 validation; do not claim closure from the code diff.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** at least one accepted repair item exists.
- **Goal:** full repair closure through 04 and applicable 05-08 gates.
- **Actions:** bind the PM/RPI to direction, rules, and its SEK escape disposition; when relevant repair the model/CORD scenario before regenerating; execute the exact gate and downstream readiness flow.
- **Verification:** no bypass; relevant SEK scenario/model/CORD fields match the PM; the SEK recurrence gate is the executable gate and passes from current generated output; current readiness passes.
- **Memory:** `.engloop/postmortems/`, `.engloop/repairs/`, and downstream repair evidence.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.22-repair --root .`

## Rule-bound repair acceptance

This agent requires the `OPERATIONS_LEARNING_GUARD_ACTIVE mode=repair` and
`OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair` hook markers. Inputs are explicit and
must match the selected PM's `RPIxxx learning contract` exactly.

### Route phase

1. Re-read current `NORTHSTAR.md`, `LEARNINGS.md`, each selected `RULE:<card-slug>` card,
  and cited `PMxxx/LEARNxxx` sources.
2. Re-read the PM's SEK test-escape analysis. If `RELEVANT`, require native .NET 10
  SEK v0.1.3, load its
  installed Cord/downstream skills, inspect the named model/Cord/generated paths, and keep
  the exact `SEK-SCENARIO:*` identity. If `NOT-RELEVANT`, preserve that explicit rationale.
3. Create immutable `.engloop/repairs/PMxxx-RPIxxx.route.json` from the route template with
  `phase: route`, `status: ROUTED`, current hashes, exact Rule IDs, executable gate, and
  all SEK applicability/scenario/repair fields.
4. The Stop hook validates the route record before showing the Stage 04 handoff.

### Close phase

1. Reinvoke Stage 22 with `--phase close` after Stage 04 and applicable 05–08 work.
2. When SEK is relevant, update the named model/CORD source, regenerate from current model
  and SUT binaries with v0.1.3, prove the incident scenario is now present with the right
  oracle/rejection, and reject bound hits, stale generation, or hand-written substitute tests.
3. Run the exact JSON argument-vector gate through the versioned tool (no shell,
   bounded timeout, content-sensitive worktree receipt):

  `dotnet tool run engloopkit -- repair-gate execute --root . --postmortem <path> --rpi <RPIxxx> --rules <RULE:id,...> --route <route.json> --receipt <.engloop/out/repair-gates/*.receipt.json>`

4. Create a separate immutable `.engloop/repairs/PMxxx-RPIxxx.close.json` referencing
  the validated route SHA-256 and tool-produced gate-receipt SHA-256. Never overwrite
  the route record or author `PASS` manually.
5. Completion requires the PM/hash/rules/gate/SEK scenario to remain current and Stage 08
  readiness record to be a current PASS. A code change or green unrelated suite is not
  repair closure.

The Stop hook runs `validate repair-learning` and fails closed on missing rule IDs,
changed direction/pyramid, substituted gate, missing gate evidence, or stale readiness.

## Done when

- [ ] Repair route includes required downstream gates
- [ ] Selected RPI acceptance carries exact PM Rule IDs and executable gate
- [ ] Relevant repair changes the named model/CORD scenario and regenerates with SEK v0.1.3
- [ ] Relevant executable gate is the PM's SEK verification gate and proves recurrence
- [ ] Close phase proves that exact gate with durable evidence
- [ ] Closure evidence is complete and current
