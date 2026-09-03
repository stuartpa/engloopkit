---
name: speckit.engloop.21-postmortem
description: "Use when a user asks in plain language to start, complete, continue, or analyze an incident postmortem; collect and confirm incident/PM context, then produce validated PM/LEARN/RPI outputs."
argument-hint: "Describe the incident postmortem to start or continue; exact incident IDs and PM path are optional internal bindings."
target: vscode
user-invocable: true
disable-model-invocation: false
tools:
- read
- search
- edit
- execute
- agent
- vscode_askQuestions
agents:
- Explore
hooks:
  SessionStart:
  - type: command
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.21-postmortem
      --root .
    timeout: 30
  - type: command
    command: dotnet tool run engloopkit -- operations-hook start postmortem
    timeout: 30
  SubagentStart:
  - type: command
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.21-postmortem
      --root .
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
  prompt: Invoke Stage 22 with --phase route, the exact --postmortem path, selected
    --rpi, concrete --rules from its RPI learning contract, and --acceptance path.
    Carry those rule IDs and the exact executable gate through Stage 04 and applicable
    Stage 05–08 gates; closure requires gate PASS plus current readiness.
  send: false
- label: Condense learnings when capacity exists
  agent: speckit.engloop.42-learnings-pyramid
  prompt: When spare stewardship capacity exists, condense the accepted learning backlog
    above and validate retrieval.
  send: false
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

- **Trigger:** selected stabilized incident set exists.
- **Goal:** PM with source LEARN links and RPI repair items.
- **Actions:** consult direction/learnings, decide SEK applicability for every cause, prioritize why current SEK model/CORD/generated replay missed relevant incident behavior, classify pyramid-rule effects, and emit rule- and scenario-bound RPIs.
- **Verification:** selection is non-empty; direction/pyramid evidence passes; every PM has a valid SEK escape disposition; every relevant RPI carries the incident scenario, model/CORD correction, and SEK recurrence gate.
- **Memory:** `.engloop/postmortems/` and `.engloop/learnings/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.21-postmortem --root .`

## Mandatory consultation and completion gate

### Plain-language routing and context collection

This agent is eligible for model delegation when a user asks to start, complete,
continue, or analyze an incident postmortem in ordinary language. Delegation is a nested
Stage 21 run, not a visible agent-mode switch. Direct runs use `SessionStart`/`Stop`;
delegated runs use equivalent `SubagentStart`/`SubagentStop` hooks. In both cases,
`UserPromptSubmit`, `PreToolUse`, and the same trusted completion validator remain active.

When the prompt lacks internal bindings and the hook emits
`OPERATIONS_POSTMORTEM_CONTEXT_COLLECTION_ACTIVE`:

1. Stay in collection mode. Use only `read`, `search`, and `vscode_askQuestions`; do not
  edit source/evidence, invoke a subagent, or run any command except the exact trusted
  binder after confirmation.
2. Inspect `.engloop/incidents/` and the `PM` row in
  `.engloop/numbering-registry.md`. Use an explicit incident ID when supplied. Otherwise,
  compare the user's words with incident titles/content and present candidates for human
  confirmation; never choose when more than one remains plausible.
3. Require a stabilized, learning-context-complete incident. If it is active, malformed,
  missing, or ambiguous, explain that condition and ask one concise clarification; do not
  bind, analyze, or claim completion.
4. Propose the next registry-backed `PM<NNN>-<brief-kebab-description>.md` path without
  reserving or creating it. Ask exactly one concise question with
  `vscode_askQuestions`: header `Confirm postmortem`; question
  `Use incidents <INxxx,...> and create <path>?`; `multiSelect: false`;
  `allowFreeformInput: false`; and exactly the ordered options `Confirm`,
  `Choose different incident/path`, and `Cancel`.
5. `PostToolUse` validates the exact question schema, selected option, session, and
  `tool_use_id`. Confirm produces `POSTMORTEM_ROUTE_CONFIRMED` plus a one-time receipt;
  Cancel produces `POSTMORTEM_ROUTE_CANCELLED` and requires no command; Choose different
  keeps collection active. Only after the confirmed receipt, invoke this exact internal
  shape with the hook-supplied collection path/token, receipt, and confirmed values:

  `dotnet tool run engloopkit -- postmortem-route bind --collection <hook-path> --token <hook-token> --incidents <INxxx,...> --postmortem <proposed-path> --confirmation-receipt <hook-receipt>`

  Never ask the operator to type or reconstruct that command or its flags. The trusted
  binder independently requires exact collection/session/HEAD/tool identity, stabilized
  incident validation, the next registry PM number, a create-new path, and the
  PostToolUse confirmation receipt. Never leave cancellation state for the operator to
  clean up.
6. Continue into postmortem work only after `POSTMORTEM_ROUTE_BOUND` and
  `OPERATIONS_LEARNING_SCOPE_ACTIVE`. Increment the tracked `PM` registry row before
  creating the confirmed PM artifact. A proposal or confirmation is not analysis,
  routing, repair acceptance, or closure evidence.

This agent requires VS Code custom-agent hooks. Require the
`OPERATIONS_LEARNING_GUARD_ACTIVE mode=postmortem` and
`OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem` markers before using any tool. The
initial prompt may carry exact internal bindings or may enter the plain-language collector
above. If `UserPromptSubmit` emits
`OPERATIONS_LEARNING_CONTEXT_REQUIRED` with status `postmortem-context-required`, no scope
or completion was accepted: follow the read-only collector and one-question confirmation
flow; never ask the operator to reconstruct internal options. The `PreToolUse` hook
mechanically denies non-collection tools until a valid scope gate exists;
a gate-less Stop response may end that recovery message but never emits a completion
marker. The Stop hook for a valid gate runs:

`dotnet tool run engloopkit -- validate postmortem-learning --root . --incidents <INxxx,...> --postmortem <path>`

and blocks completion on any missing/stale direction or pyramid evidence.

After scope activation, ordinary continuation text and unrelated command-style options do
not change the bound incident/PM identity. Supplying only `--incidents` or only
`--postmortem` suspends tool authorization and emits context-required remediation; never
fill the missing value from the existing gate. Plain follow-up text cannot clear the
suspension. Recollect and confirm the complete original incident/PM identity through the
trusted internal binder; only an exact argument-hash/HEAD/tool-identity match reactivates
the preserved gate.

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
  `using-sek-to-generate-tests/SKILL.md`; if either is absent, stop—do not reconstruct
  SEK semantics from ELK or legacy Spec Explorer guidance.
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