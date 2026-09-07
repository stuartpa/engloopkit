---
name: speckit.engloop.21-postmortem
description: "Use when a user asks in plain language to start, complete, continue, or analyze an incident postmortem; collect and confirm incident/PM context, then produce validated PM/LEARN/RPI outputs."
argument-hint: "Describe the incident postmortem to start or continue; exact incident IDs and PM path are optional internal bindings."
target: vscode
user-invocable: true
disable-model-invocation: false
tools: [read, search, edit, execute, agent, vscode_askQuestions]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.21-postmortem --root .
      timeout: 30
    - type: command
      command: dotnet tool run engloopkit -- operations-hook start postmortem
      timeout: 30
  SubagentStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.21-postmortem --root .
      timeout: 30
    - type: command
      command: dotnet tool run engloopkit -- operations-hook subagent-start postmortem
      timeout: 30
  UserPromptSubmit:
    - type: command
      command: dotnet tool run engloopkit -- operations-hook initialize postmortem
      timeout: 30
  PreToolUse:
    - type: command
      command: dotnet tool run engloopkit -- operations-hook guard postmortem
      timeout: 30
  PostToolUse:
    - type: command
      command: dotnet tool run engloopkit -- operations-hook post-tool postmortem
      timeout: 30
  Stop:
    - type: command
      command: dotnet tool run engloopkit -- operations-hook stop postmortem
      timeout: 30
  SubagentStop:
    - type: command
      command: dotnet tool run engloopkit -- operations-hook subagent-stop postmortem
      timeout: 30
handoffs:
  - label: Repair selected item
    agent: speckit.engloop.22-repair
    prompt: Invoke Stage 22 with --phase route, the exact --postmortem path, selected --rpi, concrete --rules from its RPI learning contract, and --acceptance path. Carry those rule IDs and the exact executable gate through Stage 04 and applicable Stage 05–08 gates; closure requires gate PASS plus current readiness.
    send: false
  - label: Condense learnings when capacity exists
    agent: speckit.engloop.42-learnings-pyramid
    prompt: When spare stewardship capacity exists, condense the accepted learning backlog above and validate retrieval.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** user requests postmortem discussion or analysis; validated completion requires a selected stabilized incident set.
- **Goal:** PM with source LEARN links and RPI repair items.
- **Actions:** consult direction/learnings, decide SEK applicability for every cause, prioritize why current SEK model/CORD/generated replay missed relevant incident behavior, classify pyramid-rule effects, and emit rule- and scenario-bound RPIs.
- **Verification:** selection is non-empty; direction/pyramid evidence passes; every PM has a valid SEK escape disposition; every relevant RPI carries the incident scenario, model/CORD correction, and SEK recurrence gate.
- **Memory:** `.engloop/postmortems/` and `.engloop/learnings/`.

Run the entry diagnostic; a failure is unresolved context, not a navigation veto:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.21-postmortem --root .`

For separate `validate commands` / `validate agent-surfaces` checks, source commands live
at `extensions/engloopkit/commands`, installed commands at `.specify/extensions/engloop/commands`.
If both exist, select the intended repo-relative directory with `--command-root`; an
invalid/missing explicit root is an error, never permission to substitute another root.

## Mandatory consultation and completion gate

### User navigation is not evidence acceptance

The user may select Stage21 (`speckit.engloop.21-postmortem`), Stage22
(`speckit.engloop.22-repair`), or Stage50 (`speckit.engloop.50-handoff-create`) at any
time. Missing readiness, active incidents, missing internal flags, or failed hooks do
not block entering, leaving, discussing, or drafting analysis in the conversation.
Carry unresolved evidence and diagnostics forward; do not label them PASS or closed.
Direct user selection is distinct from tool delegation and does not expand `agents`.

### Plain-language routing and context collection

This agent is eligible for model delegation when a user asks to start, complete,
continue, or analyze an incident postmortem in ordinary language. Delegation is a nested
Stage 21 run, not a visible agent-mode switch. Direct runs use `SessionStart`/`Stop`;
delegated runs use equivalent `SubagentStart`/`SubagentStop` hooks. In both cases,
`UserPromptSubmit`, `PreToolUse`, and the same trusted completion validator remain active.

When the prompt lacks internal bindings and the hook emits
`OPERATIONS_POSTMORTEM_CONTEXT_COLLECTION_ACTIVE`:

1. Stay in collection mode. Use only `read`, `search`, and `vscode_askQuestions`; do not
  edit source/evidence, invoke a subagent, or run commands. `PostToolUse` owns trusted
  binding internally after confirmation; no terminal binder exception is needed.
2. Inspect `.engloop/incidents/` and the `PM` row in
  `.engloop/numbering-registry.md`. Use an explicit incident ID when supplied. Otherwise,
  compare the user's words with incident titles/content and present candidates for human
  confirmation; never choose when more than one remains plausible.
3. The trusted binder and completion validator require a stabilized,
  learning-context-complete incident. If it is active, malformed, missing, or ambiguous,
  explain the gap and clarify the selection. Continue read-only discussion and provisional
  analysis; do not bind invalid evidence or claim stabilization/completion.
4. Propose the next registry-backed `PM<NNN>-<brief-kebab-description>.md` path without
  reserving or creating it. Ask exactly one concise question with
  `vscode_askQuestions`: header `Confirm postmortem`; question
  `Use incidents <INxxx,...> and create <path>?`; `multiSelect: false`;
  `allowFreeformInput: false`; and exactly the ordered options `Confirm`,
  `Choose different incident/path`, and `Cancel`.
5. `PostToolUse` validates the exact question schema, selected option, session, and
  `tool_use_id`. Confirm records the receipt and invokes the trusted binder internally;
  success emits `POSTMORTEM_ROUTE_BOUND` and `OPERATIONS_LEARNING_SCOPE_ACTIVE`.
  Cancel produces `POSTMORTEM_ROUTE_CANCELLED` and requires no command; Choose different
  keeps collection active. Never ask the operator to type or reconstruct binder tokens
  or flags. The internal `postmortem-route bind` / `--confirmation-receipt` interface
  is not a user prerequisite or an agent-executed recovery command.
  The binder independently requires exact collection/session/HEAD/tool identity,
  stabilized incident validation, the next registry PM number, a create-new path, and
  the confirmation receipt. On `POSTMORTEM_ROUTE_BIND_FAILED`, preserve the diagnostic,
  correct the evidence, and reconfirm; do not invent scope or leave cancellation cleanup
  to the operator.
6. Begin scope-authorized artifact writes only after `POSTMORTEM_ROUTE_BOUND` and
  `OPERATIONS_LEARNING_SCOPE_ACTIVE`. Increment the tracked `PM` registry row before
  creating the confirmed PM artifact. A proposal or confirmation is not analysis,
  routing, repair acceptance, or closure evidence.

Protected postmortem operations require VS Code custom-agent hooks and the
`OPERATIONS_LEARNING_GUARD_ACTIVE mode=postmortem` and
`OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem` markers, not read-only recovery. The
initial prompt may carry exact internal bindings or may enter the plain-language collector
above. If `UserPromptSubmit` emits
`OPERATIONS_LEARNING_CONTEXT_REQUIRED` with status `postmortem-context-required`, no scope
or completion was accepted: follow the read-only collector and one-question confirmation
flow; never ask the operator to reconstruct internal options. Local `read`, `search`,
and `vscode_askQuestions` remain available with missing, invalid, or stale scope. The
`PreToolUse` hook denies specific writes, execution, and arbitrary delegation until
bound; confirmation-controlled binding is performed internally by `PostToolUse`.
Known valid lifecycle failures return `continue=true`, `completionAccepted=false`.
Stop and SubagentStop never block navigation or manufacture PASS. For a valid gate,
the completion check runs:

`dotnet tool run engloopkit -- validate postmortem-learning --root . --incidents <INxxx,...> --postmortem <path>`

and rejects completion acceptance on missing/stale direction or pyramid evidence,
without vetoing the user's next conversation.

After scope activation, ordinary continuation text and unrelated command-style options do
not change the bound incident/PM identity. Supplying only `--incidents` or only
`--postmortem` suspends protected-operation authorization and emits context-required remediation; never
fill the missing value from the existing gate. Plain follow-up text cannot clear the
suspension. Recollect and confirm the complete original incident/PM identity through the
trusted internal binder; only an exact argument-hash/HEAD/tool-identity match reactivates
the preserved gate.

In prose, only a bare, unquoted `--postmortem` value ending in `.md.` loses its one
sentence-ending period. Quoted values, structured/CLI bindings, and confirmation/receipt
identities are exact: reject invalid paths rather than trimming or guessing them.

Before root-cause analysis:

1. Read current root `NORTHSTAR.md`; record its SHA-256, alignment
  (`ALIGNED|TENSION|GAP`), and the resulting direction decision.
2. Read root `LEARNINGS.md`, follow only relevant `RULE:<card-slug>` cards, then inspect
  their cited `PMxxx/LEARNxxx` sources. Record exact index SHA-256.
3. Classify each relevant rule as `REINFORCED`, `CONTRADICTED`, or `MISSING`, with
  incident evidence and an explicit pyramid action. Rule IDs are stable
  `RULE:<card-slug>` values; source IDs remain `PMxxx/LEARNxxx`.
4. Choose `Pyramid decision: UPDATED` or `NO-CHANGE`. `NO-CHANGE` is allowed only when
  the PM accepts no new `PMxxx/LEARNxxx`; record a substantive `No accepted source
  learning` decision. Any accepted learning requires `UPDATED` and immediate card/index/
  historical provenance coverage so the global Learnings validator remains green.
5. If updated, update `.engloop/learnings/README.md` historical coverage and card/source
  provenance. Static global `LEARNINGS.md → card → source` validation must pass.
6. If a rule meaning or retrieval query changes, update independent retrieval cases and
  observed results, run deterministic retrieval, and cite a PASS JSON. Provenance-only
  reinforcement may mark retrieval `UNCHANGED` with a reason.
7. Give every RPI a `### RPIxxx learning contract` containing concrete Rule IDs, one
  executable gate argument vector, and what that gate proves. A code diff is never the gate.
8. Bind the exact nonempty `--incidents` set to the PM's Selected stabilized incidents
  table using existing incident paths and SHA-256 values. Deferred Stage 20 learning
  context must be resolved before Stage 21 can complete.

## Mandatory SEK test-escape analysis

For **every primary and contributing cause**, decide whether the escaped behavior belongs
to the stateful vertical that SEK is intended to prove. Record one aggregate disposition
whose rationale covers all causes; split the PM if causes require incompatible repairs.

1. Set `SEK applicability` to `RELEVANT` or `NOT-RELEVANT`; never omit the decision.
2. When relevant, run `sek version` and require **0.1.3**. Load the installed
  `.specify/extensions/sek/skills/sek-cord-authoring/SKILL.md` and
  `using-sek-to-generate-tests/SKILL.md`; if either is absent, defer SEK execution and
  SEK-backed completion claims, not navigation or analysis. Do not reconstruct SEK
  semantics from ELK or legacy Spec Explorer guidance.
3. Inspect the unsliced model, applicable Cord configs/machines, finite parameter domains,
  slices, bounds/bound hits, binding/oracle, generated source freshness, and actual replay.
4. Name the incident behavior as `SEK-SCENARIO:<brief-kebab-id>` and classify the escape:
  model, Cord domain/slice/bound, binding, oracle, stale generation, or SEK engine gap.
5. Explain **why the existing generated suite did not contain or correctly assert that
  scenario**. “Insufficient tests” is not an explanation.
6. State the exact required model/CORD/generated-scenario repair. Every relevant RPI must
  carry the same scenario ID and a SEK verification gate that equals its executable gate.
7. When not relevant, identify the artifact class (for example a domain-free component,
  infrastructure, documentation, or external dependency) and explain why SEK should not
  be expanded to cover it. Do not force tautological model tests onto non-stateful code.

The objective of repeated Stages 20→21→22 is cumulative model quality: each relevant
escape becomes a durable model/CORD scenario and generated recurrence proof.

The generated PM must use `PMxxx/LEARNxxx`, not legacy `LRNxxx`, identities.

## Done when

- [ ] PM/LEARN/RPI outputs are complete and linked
- [ ] Current North Star and progressive Learnings Pyramid consultation are hash-bound
- [ ] Every relevant rule is reinforced, contradicted, or missing with an update/no-change decision
- [ ] Historical coverage/card provenance and applicable retrieval validation pass
- [ ] SEK applicability and escape class are explicit for the incident cause
- [ ] Relevant escapes identify the missing scenario and exact model/CORD repair under SEK v0.1.3
- [ ] Every RPI carries concrete Rule IDs and an executable verification gate
- [ ] Repair routing is explicit
