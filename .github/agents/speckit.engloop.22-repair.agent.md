---
name: speckit.engloop.22-repair
description: Route repair obligations through governed implementation and verification
  gates.
argument-hint: --phase <route|close> --postmortem <path> --rpi <RPIxxx> --rules <RULE:id,...>
  --acceptance <.engloop/repairs/PMxxx-RPIxxx.<route|close>.json>
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
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.22-repair
      --root .
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
  prompt: Implement the selected repair under its exact PM Rule IDs and executable
    gate. Preserve both in SPEC acceptance, run the gate after implementation, then
    complete applicable Stage 05–08 validation; do not claim closure from the code
    diff.
  send: false
---


<!-- Source: engloop -->
## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** user requests repair discussion; accepted routing requires at least one accepted repair item.
- **Goal:** full repair closure through 04 and applicable 05-08 gates.
- **Actions:** bind the PM/RPI to direction, rules, and its SEK escape disposition; when relevant repair the model/CORD scenario before regenerating; execute the exact gate and downstream readiness flow.
- **Verification:** no bypass; relevant SEK scenario/model/CORD fields match the PM; the SEK recurrence gate is the executable gate and passes from current generated output; current readiness passes.
- **Memory:** `.engloop/postmortems/`, `.engloop/repairs/`, and downstream repair evidence.

Run the entry diagnostic; a failure is unresolved context, not a navigation veto:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.22-repair --root .`

## Continuous Requested Work

Implement and verify every selected repair in this session, including authorized
push and deployment; routing alone is not task completion. Missing hooks or an ELK
identity fault are workflow defects, not reasons to ask the user to repeat intent.
Outside a bound native host, use the separately audited command:
`dotnet tool run engloopkit -- operations-hook authorize repair --root . --request "Implement all repair items for PMnnn."`
Use the exact selected PM identity and retain the emitted routes and authorization;
this path is not a native binding or completion evidence. Under explicit ELK
maintenance authority, repair workflow faults and resume, preserving historical
records. Never bypass an actual safety/authorization denial or invent PASS. Keep
readiness and stabilization gaps open while implementing the authorized repairs.

An unrelated dirty worktree is not a deployment blocker. When publication and live
deployment are explicitly authorized, isolate the selected repair in a clean worktree,
preserve unrelated edits, run its required gates, publish that exact revision, and use
the target repository's governed deployment procedure. Do not stop at local tests or
routing, and do not stage unrelated work or infer physical-effect authorization.

## Missing Native Hooks

If no native hook ran and no protected operation was denied, continue from the
exact validated PM through the audited operator-command path above. Missing native
receipts are an installation or host-integration diagnostic, not a request for the
user to change agents or repeat intent. Finish missing incident/PM prerequisites
under their existing authority before routing; never borrow a historical route.
Do not construct native phase, RPI, rule, or acceptance arguments: the trusted
marker or audited authorizer derives them from the actual validated contract.
Keep `nativeSession=false` for audited authorization, preserve all unresolved
evidence, and never bypass a real denial or claim closure from authorization.

## Rule-bound repair acceptance

An explicit operator request to implement repair items is authorization to begin
the named repair, not a demand to pretend the incident is healthy first. Native
Stage 22 accepts `Implement all repair items for PM060` (or a named RPI/PM path),
resolves the exact contracts, and binds one immutable route per selected item.
Do not ask for internal flags, binder tokens, or repeated authorization. If the
PM or selected items are genuinely ambiguous, ask one focused question and keep
working on the known context. Never stop or send the operator back to Stage 21
merely because incident stabilization, a watch window, or PM completion is open.

Routing and implementation require a structurally valid, hash-bound PM repair
contract, not a COMPLETE PM or STABILIZED incident. Closure still requires full
completion, measured recurrence-gate evidence, and current readiness. Obtain
canonical hashes using the read-only validator option `--show-current-hashes`;
correct only derived consultation fields without inventing health/PASS evidence.
Use `--for-repair-routing` to validate authorization separately from completion.

The user may select Stage21 (`speckit.engloop.21-postmortem`), Stage22
(`speckit.engloop.22-repair`), or Stage50 (`speckit.engloop.50-handoff-create`) at any
time. Missing flags, readiness, PM/RPI evidence, or failed hooks never veto navigation
or read-only discussion. Carry unresolved evidence and diagnostics forward. Direct
user selection does not authorize arbitrary delegation or a protected mutation.

Native accepted repair routing/closure requires the `OPERATIONS_LEARNING_GUARD_ACTIVE mode=repair`
and `OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair` hook markers. Collect missing context
without requiring the user to reconstruct internal options. Before protected operations,
bind explicit inputs matching the selected PM's `RPIxxx learning contract` exactly;
do not invent rule IDs, acceptance paths, or PASS evidence.

### Route phase

1. Re-read current `NORTHSTAR.md`, `LEARNINGS.md`, each selected `RULE:<card-slug>` card,
  and cited `PMxxx/LEARNxxx` sources.
2. Re-read the PM's SEK test-escape analysis. If `RELEVANT`, preserve its historical
  native .NET 10 SEK v0.1.3 contract; use that identity or the exact hash-bound numeric
  SEK identity in the structured supplement below. Load its
  installed Cord/downstream skills, inspect the named model/Cord/generated paths, and keep
  the exact `SEK-SCENARIO:*` identity. If `NOT-RELEVANT`, preserve that explicit rationale.
3. The native hook creates immutable `.engloop/repairs/PMxxx-RPIxxx.route.json` for
  every explicitly selected item; validate and retain those records. For the
  explicit argument route, create the record from the route template with
  `phase: route`, `status: ROUTED`, current hashes, exact Rule IDs, executable gate, and
  all SEK applicability/scenario/repair fields.
4. The Stop hook validates route acceptance, not permission to show or select the
  Stage 04 handoff. Carry any unaccepted route and missing evidence to the destination;
  navigation alone does not authorize implementation.
5. When implementation is requested and exact route scope is active, continue the
  implementation, tests, and applicable Stage 05-08 work under each bound item in
  this session; do not finish with only routing or require another user request.
  The Stage 04 handoff is available for user navigation, not a reason to stop.
  Preserve unrelated work, and honor authorized commit/push/deployment using the
  target repository's deployment procedure. Never substitute source changes for
  recurrence or live-readiness proof.

### Close phase

1. Reinvoke Stage 22 with `--phase close` after Stage 04 and applicable 05–08 work.
2. When SEK is relevant, update the named model/CORD source and regenerate from current
  model/SUT binaries under the bound native SEK identity. Follow SEK's authoritative skills
  for strict `config.proof`, attested `sek build`, COMPLETE artifacts and explicit validated
  checkpoint reuse. Prove the exact incident scenario and model-derived rejection contract;
  reject bound hits, missing obligations, stale generation, or hand-written substitute tests.
3. Run the exact JSON argument-vector gate through the versioned tool (no shell,
   bounded timeout, content-sensitive worktree receipt):

  `dotnet tool run engloopkit -- repair-gate execute --root . --postmortem <path> --rpi <RPIxxx> --rules <RULE:id,...> --route <route.json> --receipt <.engloop/out/repair-gates/*.receipt.json>`

4. Create a separate immutable `.engloop/repairs/PMxxx-RPIxxx.close.json` referencing
  the validated route SHA-256 and tool-produced gate-receipt SHA-256. Never overwrite
  the route record or author `PASS` manually.
5. Completion requires the PM/hash/rules/gate/SEK scenario to remain current and Stage 08
  readiness record to be a current PASS. A code change or green unrelated suite is not
  repair closure.

The Stop hook runs `validate repair-learning` and rejects acceptance on missing rule IDs,
changed direction/pyramid, substituted gate, missing gate evidence, or stale readiness.
Known valid lifecycle failures return `continue=true`, `completionAccepted=false`;
Stop never blocks navigation or manufactures PASS. These are evidence gates, not exits.

### Structured proof supplement

Use `repair-gate execute --proof-contract .engloop/repairs/<PM>-<RPI>.proof-contract.json`
with the exact primary gate above; see [the implemented descriptor contract](/docs/engineering-loop.md#structured-repair-proof-supplements).
The supplement binds accepted PM/route hashes, exact RPI/Rule IDs, unchanged gate digest,
declared inputs, native SEK reports and exact tool identity. For a different numeric `x.y.z`
SEK version, require hash-bound `sekVersionSupplement` with `priorVersion: "0.1.3"` and
the same PM/route hashes. Package metadata/bytes, manifest and installed assembly version/hash
must agree. Keep the original v0.1.3 requirement historical; never rewrite the PM, route,
scenario, or executable vector, and never infer permission for a global tool upgrade.

Receipts without this option are `structuredAdequacy: LEGACY`; exit zero alone is not
structured adequacy. Require fresh native reports with COMPLETE nonempty legal witnesses,
all expected stable case/guard IDs, exact modeled errors, model-derived rejection replay,
current build fingerprints and validated checkpoint/output hashes. Legal slices and direct
scoped negative models prove distinct contracts; follow SEK's skills rather than duplicating
its semantics here. Strict execution caps the primary gate at 300 seconds and supplemental
phases at a separate shared 300 seconds; reports must meet 120-second exploration, 25 MiB
graph and shared candidate ceilings. Close records and receipts carry `proofContractPath`
and `proofContractSha256`, and closure revalidates them.

Local structured proof does not waive immutable-release/live-target/conservation evidence
or Stage 08's whole-product 95% line and branch bar. A missing module inventory or missing
proof obligation fails readiness, even when a focused suite passes. Retain the blocker;
do not loop reruns, raise budgets, weaken inventories, hand-edit generated tests, or use a
physical print as proof. No deployment or print is authorized by this supplement.

## Done when

- [ ] Repair route includes required downstream gates
- [ ] Selected RPI acceptance carries exact PM Rule IDs and executable gate
- [ ] Relevant repair changes the named model/CORD scenario and regenerates under the bound native SEK identity
- [ ] Relevant executable gate is the PM's SEK verification gate and proves recurrence
- [ ] Close phase proves that exact gate with durable evidence
- [ ] Closure evidence is complete and current