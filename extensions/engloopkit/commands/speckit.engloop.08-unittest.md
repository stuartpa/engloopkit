---
name: speckit.engloop.08-unittest
description: Classify unreached paths, enforce delete/revalidate ordering, then compute final readiness.
argument-hint: "[reachability disposition scope]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.08-unittest --root .
      timeout: 30
handoffs:
  - label: Correct design defect
    agent: speckit.engloop.04-refactor
    prompt: Route the design or architecture defect identified above through the governed SPEC implementation loop.
    send: false
  - label: Model intended gap
    agent: speckit.engloop.05-model
    prompt: Model the authoritative but functionally unreached behavior identified above before regenerating tests.
    send: false
  - label: Revalidate deletion
    agent: speckit.engloop.07-validate
    prompt: Rerun the complete generated functional validation after the coherent residue deletion set above.
    send: false
  - label: Walk through review code in debugger
    agent: speckit.engloop.09-debugger-walk-thru
    prompt: Use the current readiness PASS and exact base-to-HEAD diff to prepare an engineer-led debugger walkthrough ledger for every changed executable code chunk.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** current Stage 07 functional evidence exists.
- **Goal:** complete disposition then sole readiness PASS/FAIL emission.
- **Actions:** classify unreached paths, enforce delete/revalidate sequence, add direct tests only after disposition, and emit the generic HEAD-bound readiness record after PASS.
- **Verification:** per-module 95/95 with full inventory and current evidence.
- **Memory:** `.engloop/coverage/COV003_ordered-engloop-v2-readiness.md`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.08-unittest --root .`

## Readiness-state emission

After the Stage 08 evidence document has an explicit checked PASS verdict, emit the
generic current readiness record consumed by Stage 10:

`dotnet tool run engloopkit -- readiness emit --root . --evidence <.engloop/coverage/COVxxx-readiness.md> --verdict pass`

The command validates the evidence, hashes it, and binds `.engloop/readiness/current.json`
to the exact Git HEAD. A product edit makes that record stale automatically. Do not copy
the self-host `cov003-readiness.json` convention into consumer repositories.

## Done when

- [ ] Disposition precedes any new direct tests
- [ ] Final readiness verdict is emitted only by Stage 08
- [ ] A checked PASS emits `.engloop/readiness/current.json` for the exact current HEAD
- [ ] A readiness PASS is handed to Stage 09 before code-review preparation
