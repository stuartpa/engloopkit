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
    prompt: After producing or updating the requested postmortem, automatically continue to Stage 22 with the exact marker ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION when same-session repair intent was explicitly captured. Do not construct or substitute phase, postmortem, RPI, rule, or acceptance arguments; Stage 22 derives scope only from the confirmed binding and validated postmortem contract. Carry any resumable evidence blocker unchanged, without claiming PASS or closure or asking the user to reconstruct internal flags.
    send: true
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

This agent must run as a user-invoked top-level agent for route collection and
binding. VS Code Local subagents cannot supply the top-level operator prompt or interactive
selection required for route authorization. Stage 20 exposes this agent through a user-visible handoff; use that handoff or
select Stage 21 directly. Do not invoke this agent as a subagent or reuse a parent
session's selection.

### Explicit operator intent without repeated confirmation

Ordinary unambiguous affirmative directives authorize routing without another question.
The recognizer accepts `postmortem`, `post mortem`, and `post-mortem`, polite prefixes,
start/create/write/continue/complete requests, and `and` or `then` implement/complete/execute
all repair items. An exact incident ID binds directly; an optional PM path remains exact.
The operator's affirmative `complete the plan...today FB first then fix live` continuation
also resolves a unique Facebook incident from current incident filenames. Multiple matching
incidents require a choice, not inference. Quoted, negated, planning, hypothetical, or
question forms do not authorize routing. Only the native current operator prompt is input;
history, unavailable-user text, and model-generated handoffs cannot substitute for it.

The native `UserPromptSubmit` is authoritative even when Stage 21 is selected in an
already-open chat without `SessionStart`; the hook initializes host identity only when
no saved postmortem or repair scope exists. Never require a new chat or window. The hook
checks current HEAD, tool/manifest identity, unique incident
identity, consulted learning context, registry-backed next PM and create-new scope. With
no explicit PM path it derives the filename from the unique incident filename and next
registry number. A separate hashed operator-intent record authorizes the binding; no
question confirmation receipt or tool-use selection is fabricated.

When the hook emits `POSTMORTEM_ROUTE_BOUND` and `OPERATIONS_LEARNING_SCOPE_ACTIVE`
with `authorization=explicit-operator-intent`, use the emitted exact route and do not
ask a confirmation question. Missing native session, stale host/manifest identity,
changed HEAD, ambiguous/absent intent,
or invalid evidence does not authorize binding. Keep any diagnostic as an evidence gap;
never reconstruct host JSON, internal tokens, or scope markers from a terminal.

Activation is delivered through native `UserPromptSubmit` additional context. If a
resumed conversation lost that context, use a successful read/search call: native
`PostToolUse` revalidates the saved same-session gate and binding, then re-emits the
exact incident, PM, receipt, authorization, and correlation. Do not refuse that trusted
scope solely because the earlier initialization message is absent, or ask for repeated
confirmation. A changed HEAD/tool identity, tampered evidence, different session, or
suspended scope remains unauthorized; preserve the reported recovery diagnostic.

For a tool/manifest upgrade in the same native session, a fresh affirmative operator
prompt can authorize a new current-identity generation. The hook checks the old record
chain and unchanged incident/HEAD/direction evidence, preserves every historical record,
and appends hashed lineage. It derives a create-new PM from the current registry unless
an explicit valid next-PM path was requested. The emitted `historicalPostmortem` and
`historicalScopeAccepted=false` preserve prior numbering mistakes. Never migrate a
historical IN023/PM060 reservation onto PM060 now owned by IN024, replace receipts, or
overwrite that PM. New authorization is not acceptance of stale identity. Changed HEAD,
tampering, suspended scope, and changed incident evidence remain rejected.

An explicit repair-items request is captured for the existing trusted Stage 22 handoff.
That continuation still requires the exact validated PM/RPI/rules/executable gate and
immutable route; operator intent alone permits neither source edits nor completion.
This distinction follows `RULE:readiness-is-a-gate` and `PM001/LEARN001-003`:
authorization to analyze is not evidence that a repair or readiness check passed.

When no explicit route was bound and the prompt lacks internal bindings, whether or not a hook emits
`OPERATIONS_POSTMORTEM_CONTEXT_COLLECTION_ACTIVE`:

1. For this active Stage 21 collector's protected operations, use `read`, `search`, and `vscode_askQuestions`; do not
  edit source/evidence, invoke a subagent, or run commands. Do not request
  `run_in_terminal`, even for a read-only shell command; use the VS Code read/search
  tools instead. `PostToolUse` owns trusted binding internally after confirmation; no
  terminal binder exception is needed. If a tool is accidentally blocked, no action ran:
  continue with an allowed read/search/question tool, do not retry the blocked call, and
  do not ask the operator to run a binder or provide tokens.
2. Inspect `.engloop/incidents/` and the `PM` row in
  `.engloop/numbering-registry.md`. Use an explicit incident ID when supplied. Otherwise,
  compare the user's words with incident titles/content and present candidates for human
  confirmation; never choose when more than one remains plausible.
3. The trusted binder permits analysis for an identity-valid incident with consulted
  learning context even when stabilization status or health/watch checks remain open.
  Carry each gap into the PM as an explicit limitation. Malformed identity/context or
  deferred learning consultation remains a binding failure. Stabilization is required for
  stabilization/closure claims and successful PM completion, never for starting requested
  analysis.
4. Propose the next registry-backed `PM<NNN>-<brief-kebab-description>.md` path without
  reserving or creating it. Only for unresolved incident/path choice, ask one concise question with
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
  incident identity and consulted learning context, the next registry PM number, a create-new path, and
  the confirmation receipt. On `POSTMORTEM_ROUTE_BIND_FAILED`, preserve the diagnostic
  and exact confirmed route. After correcting only the reported prerequisite, continue
  with a successful read/search tool call so `PostToolUse` retries the saved receipt.
  Never ask the operator to reconfirm an unchanged incident/PM pair; do not invent scope
  or leave cancellation cleanup to the operator.
6. Begin scope-authorized artifact writes only after `POSTMORTEM_ROUTE_BOUND` and
  `OPERATIONS_LEARNING_SCOPE_ACTIVE`. Increment the tracked `PM` registry row before
  creating the confirmed PM artifact. A proposal or confirmation is not analysis,
  routing, repair acceptance, or closure evidence.

Do not infer that this definition is the active agent merely because it was read.
Analysis and user-selected navigation are always available; evidence gates constrain
specific protected writes and claims. Separately explicit user authorization to maintain
ELK in its own repository does not require downstream repair-source scope. Perform that
bounded maintenance only in the authorized ELK files and commands under the actual active
agent's permissions. It creates no downstream PM, route, receipt, gate, live mutation, or
completion authority, and is not an exception to an active hook denial.

Protected postmortem operations require VS Code custom-agent hooks and the
`OPERATIONS_LEARNING_GUARD_ACTIVE mode=postmortem` and
`OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem` markers, not read-only recovery. The
initial prompt may carry exact internal bindings or may enter the plain-language collector
above. If `UserPromptSubmit` emits
`OPERATIONS_LEARNING_CONTEXT_REQUIRED` with status `postmortem-context-required`, no scope
or completion was accepted: follow the read-only collector, preserving explicit intent
and reported prerequisites without asking for repeated confirmation. Use a question only
for an unresolved choice; never ask the operator to reconstruct internal options. Local `read`, `search`,
and `vscode_askQuestions` remain available with missing, invalid, or stale scope. The
`PreToolUse` hook denies specific writes, execution, and arbitrary delegation until
bound; binding is performed internally by `UserPromptSubmit` for exact explicit intent
or by `PostToolUse` for a genuine question selection.
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
8. Bind the exact nonempty `--incidents` set to the PM's Selected incidents table using
  existing incident paths and SHA-256 values. Record open stabilization checks as
  limitations; they prevent completion, not analysis initiation. Deferred Stage 20
  learning context must be resolved before Stage 21 can complete.

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
