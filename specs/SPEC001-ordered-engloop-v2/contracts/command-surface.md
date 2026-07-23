# Contract: Ordered v2 Command Surface

- **Features:** SPEC001 ordered workflow + SPEC002 overlay utilities + POM session memory
- **Contract version:** 1.11.0
- **Owner:** first-party extension `engloopkit`

## Identity

The product, repository, bundle, extension, package, and release remain EngLoopKit with
identifier `engloopkit`. The picker namespace is `speckit.engloop`.

The extension manifest MUST declare exactly the following entries, in this order. Each
ID maps to exactly one same-named Markdown file.

| Manifest order | Exact ID | Exact file | Lane | Durable memory/evidence |
|---:|---|---|---|---|
| 1 | `speckit.engloop.01-northstar` | `commands/speckit.engloop.01-northstar.md` | delivery | root `NORTHSTAR.md` |
| 2 | `speckit.engloop.02-scaffold` | `commands/speckit.engloop.02-scaffold.md` | delivery | thin slice + `SCAFxxx` runway evidence/config |
| 3 | `speckit.engloop.03-architect` | `commands/speckit.engloop.03-architect.md` | delivery | `ARCHxxx` + architecture governance |
| 4 | `speckit.engloop.04-refactor` | `commands/speckit.engloop.04-refactor.md` | delivery | accepted SPEC/spec/plan/tasks/implementation evidence |
| 5 | `speckit.engloop.05-model` | `commands/speckit.engloop.05-model.md` | delivery | `MODELxxx` + independent model source |
| 6 | `speckit.engloop.06-explore` | `commands/speckit.engloop.06-explore.md` | delivery | `CORDxxx` + generated suite at proven destination |
| 7 | `speckit.engloop.07-validate` | `commands/speckit.engloop.07-validate.md` | delivery | functional-only `COVxxx` and reachability |
| 8 | `speckit.engloop.08-unittest` | `commands/speckit.engloop.08-unittest.md` | delivery | disposition + readiness `COVxxx` and verdict |
| 9 | `speckit.engloop.09-debugger-walk-thru` | `commands/speckit.engloop.09-debugger-walk-thru.md` | review gate | numbered engineer-attested `DBGxxx` walkthrough ledger |
| 10 | `speckit.engloop.10-codereview-prepare` | `commands/speckit.engloop.10-codereview-prepare.md` | review | minimized current PR + transient evidence-linked review report |
| 11 | `speckit.engloop.20-incident` | `commands/speckit.engloop.20-incident.md` | operations | `INxxx` + local `MITxxx` |
| 12 | `speckit.engloop.21-postmortem` | `commands/speckit.engloop.21-postmortem.md` | operations | `PMxxx` + `PMxxx/LEARNxxx` + `RPIxxx` |
| 13 | `speckit.engloop.22-repair` | `commands/speckit.engloop.22-repair.md` | operations | repair obligation + Stage 04/05–08/release/target evidence |
| 14 | `speckit.engloop.30-refactor-scan` | `commands/speckit.engloop.30-refactor-scan.md` | stewardship | one `REFACTxxx`, including no-work decisions |
| 15 | `speckit.engloop.31-learnings-pyramid` | `commands/speckit.engloop.31-learnings-pyramid.md` | stewardship | source-linked cards, root index, instruction, validation/retrieval result |
| 16 | `speckit.engloop.40-pomodoro-create` | `commands/speckit.engloop.40-pomodoro-create.md` | session memory | one `POM<NNNN>-<description>.md` note |
| 17 | `speckit.engloop.50-overlay-pack` | `commands/speckit.engloop.50-overlay-pack.md` | local utility | portable registered overlay archive |
| 18 | `speckit.engloop.51-overlay-remove` | `commands/speckit.engloop.51-overlay-remove.md` | local utility | complete manifest-owned overlay removal |
| 19 | `speckit.engloop.60-powerpnt-create` | `commands/speckit.engloop.60-powerpnt-create.md` | presentation | `PPTxxx` Markdown/PPTX and evidence-derived graph assets |

Ordinal lexical sort of the full IDs MUST equal table order. The manifest order MUST
also equal table order so integrations that preserve declaration order remain correct.

## Exact-set invariant

For every current v2 surface:

```text
actual IDs == expected 19-ID set
count(actual IDs) == 19
count(distinct actual IDs) == 19
actual IDs sorted ordinal == expected order
```

The following current scopes MUST contain zero matches for
`speckit.engloopkit.`:

- extension manifest and command package;
- extension README and current product docs/examples/skills;
- built extension/bundle archive current payload;
- installed extension registry;
- installed current command files;
- generated agent files and generated prompt files;
- focused-workspace picker rows.

Historical PM/incident/refactor/spec/changelog evidence may quote an old ID when it is
clearly historical. Tests MUST scope the exemption to durable historical paths; there
is no blanket repository-wide ignore.

## Repository-root precondition

Before any command reads config or stage evidence, it MUST validate the explicitly
selected repository root:

- exactly one canonical tracked `.engloop/` directory exists;
- exactly one `.engloop/config.json` regular file exists;
- `.engloop/out/` is the only configured transient output root and is ignored;
- root `NORTHSTAR.md` and root `LEARNINGS.md` remain visible entry points;
- no current `engloop/` compatibility tree or `.engloopkit/` directory exists.

Missing, dual, case-ambiguous, or forbidden roots fail with an actionable root-layout
result before config parsing. Commands MUST NOT merge roots, prefer one root, search a
parent/sibling, or read an old config as a fallback.

## Command-loop shape

Every command file MUST satisfy ARCH002 and contain:

1. YAML frontmatter with explicit `name`, non-empty `description`, stage-specific
   `argument-hint`, `target: vscode`, `user-invocable: true`,
   `disable-model-invocation: true`, exact least-privilege `tools`, explicit `agents`,
   the versioned `SessionStart` entry hook, and exact applicable `handoffs`;
2. `## User Input` and the `$ARGUMENTS` contract;
3. `## Artifact root` that resolves only through `.engloop/config.json` at the
   selected repository root;
4. `## Loop definition` with `**Trigger:**`, `**Goal:**` (or an annotated Goal),
   `**Actions:**`, `**Verification:**`, and `**Memory:**`;
5. stage-specific prerequisites and fail-closed rejection behavior;
6. `## Done when` with mechanically observable checks;
7. the correct next behavior without implying that a different invocation lane is
   scheduled.

The common header and stage-specific matrices are normative in SPEC001's
**Custom-agent UX contract**. `infer` and `model` are absent. Every handoff uses
`send: false` and no model override. Stages 31, 40, 51, and 60 have no handoffs. Generated prompt
files identify the exact matching agent and contain no `tools` field.

The source command frontmatter and installed `.agent.md` frontmatter must be
semantically equivalent after YAML parsing. Silent field loss, target loss, handoff
rewriting, tool widening, unresolved agent targets, or a generated prompt tool override
is an install failure.

Missing configuration, identity, prerequisite evidence, or tool capability MUST emit
an actionable failure and leave state unchanged. A command MUST NOT search a parent or
sibling root, choose a provider/framework/project by convention, reuse stale output,
or switch to a weaker gate.

## Exact custom-agent header contract

Every source command and generated agent has this common semantic projection:

| Field | Required value |
|---|---|
| `name` | Exact command ID from the Identity table. |
| `description` | Non-empty stage-specific trigger and outcome. |
| `argument-hint` | Non-empty stage-specific required input. |
| `target` | `vscode`. |
| `user-invocable` | `true`. |
| `disable-model-invocation` | `true`. |
| `tools` | Exact row in the matrix below, with no duplicate/extra/missing value. |
| `agents` | Exact row in the matrix below; `[Explore]` or `[]`, never `*`. |
| `hooks.SessionStart` | One exact `EntryHook` below. |
| `handoffs` | Exact ordered outgoing rows in the graph below; omitted only for Stages 31, 40, 51, and 60. |
| `infer` | Absent. |
| `model` | Absent. |

YAML mapping order and quoting style are not significant. Scalar types and values are
significant. `tools` and `agents` compare as duplicate-free exact policy sets;
handoffs compare as an ordered sequence because button order is visible UX.

### Exact tools and subagents

| Exact agent ID | Exact tools | Exact `agents` |
|---|---|---|
| `speckit.engloop.01-northstar` | `read, search, edit, execute, web, agent` | `[Explore]` |
| `speckit.engloop.02-scaffold` | `read, search, edit, execute, web` | `[]` |
| `speckit.engloop.03-architect` | `read, search, edit, execute, agent` | `[Explore]` |
| `speckit.engloop.04-refactor` | `read, search, edit, execute` | `[]` |
| `speckit.engloop.05-model` | `read, search, edit, execute, agent` | `[Explore]` |
| `speckit.engloop.06-explore` | `read, search, edit, execute` | `[]` |
| `speckit.engloop.07-validate` | `read, search, edit, execute` | `[]` |
| `speckit.engloop.08-unittest` | `read, search, edit, execute, agent` | `[Explore]` |
| `speckit.engloop.09-debugger-walk-thru` | `read, search, edit, execute` | `[]` |
| `speckit.engloop.10-codereview-prepare` | `read, search, edit, execute, web` | `[]` |
| `speckit.engloop.20-incident` | `read, search, edit, execute` | `[]` |
| `speckit.engloop.21-postmortem` | `read, search, edit, execute, agent` | `[Explore]` |
| `speckit.engloop.22-repair` | `read, search, edit, execute` | `[]` |
| `speckit.engloop.30-refactor-scan` | `read, search, edit, execute, agent` | `[Explore]` |
| `speckit.engloop.31-learnings-pyramid` | `read, search, edit, execute, agent` | `[Explore]` |
| `speckit.engloop.40-pomodoro-create` | `read, search, edit, execute` | `[]` |
| `speckit.engloop.50-overlay-pack` | `read, search, edit, execute` | `[]` |
| `speckit.engloop.51-overlay-remove` | `read, search, edit, execute` | `[]` |
| `speckit.engloop.60-powerpnt-create` | `read, search, edit, execute` | `[]` |

For every nonempty `agents` list, `tools` contains `agent`; for every empty list, it
does not. The supported VS Code build must resolve every named tool and the `Explore`
agent. VS Code's documented behavior of ignoring an unavailable tool is not an
EngLoopKit success path. Nested subagents are not required and acceptance does not
enable `chat.subagents.allowInvocationsFromSubagents`.

### Capability justification

Every stage receives `read` and `search` to inspect its prerequisites and evidence,
`edit` only because it owns a durable artifact/state update, and `execute` only to run
the versioned validator and stage-specific objective gates. Additional capabilities
are justified narrowly:

| Stage(s) | Additional capability | Required use; forbidden widening |
|---|---|---|
| 01 | `web` | Research external product/user evidence only when repository evidence is insufficient; never infer authority from a convention. |
| 01 | `agent` → `Explore` | Isolated read-only synthesis of broad direction evidence with a focused question and bounded findings. |
| 02 | `web` | Consult authoritative framework/package documentation for the explicitly selected runway; never switch frameworks silently. |
| 03 | `agent` → `Explore` | Independent read-only boundary/dependency survey before recording architecture. |
| 05 | `agent` → `Explore` | Independent behavior/state inventory; no implementation delegation. |
| 08 | `agent` → `Explore` | Independent reachability/authority classification review; no deletion delegation. |
| 10 | `web` | Query the explicitly selected GitHub/Azure DevOps PR and source-linked reviewer comments; never infer PR identity. |
| 21 | `agent` → `Explore` | Clean-context cross-incident pattern analysis; no repair implementation. |
| 30 | `agent` → `Explore` | Independent signal scan before selecting at most one refactor. |
| 31 | `agent` → `Explore` | Clean-context retrieval cases and subject grouping; no source-learning rewrite. |

Every allowed `Explore` call must pass one focused subtask, the minimum relevant paths
or evidence, and an explicit bounded result shape. Structural acceptance rejects an
unfocused delegation instruction, a nested delegation requirement, or any body use of
a capability absent from its row. No stage receives `todo`, MCP, wildcard tools, or a
second implementation agent.

### Exact entry hook and body gate

Each agent has exactly one agent-scoped Preview hook:

```yaml
hooks: { SessionStart: [{ type: "command", command: "dotnet tool run engloopkit validate agent-entry --stage <exact-command-id> --root .", timeout: 30 }] }
```

`<exact-command-id>` is replaced by that row's full ID; it is not a runtime placeholder
in a shipped file. The command resolves the accepted EngLoopKit package from the
selected root's local .NET tool manifest, uses no repository-editable script or secret,
validates every argument/root/stage/evidence input, and returns exit code 2 when entry
is rejected so VS Code blocks the Preview hook.

The body unconditionally runs the same fully expanded command before any other stage
action and treats any nonzero exit as rejection. This is mandatory agent behavior and
is acceptance-tested, but it is not falsely described as a VS Code pre-tool
interceptor. When `chat.useCustomAgentHooks` is false, unsupported, or disabled by
policy, the session is reduced-assurance. Every EngLoopKit operation that accepts a
durable transition or evidence record independently re-runs the trusted entry gate, so
skipping a body instruction cannot produce accepted stage state, readiness, or release
evidence. Hook-enabled focused workspaces provide the strict mechanical pre-action
boundary; neither hook nor body observation alone is sufficient stage-completion
evidence.

### Pinned VS Code/schema baseline

The 1.7.0 release accepts custom-agent behavior against exactly:

- VS Code Insiders `1.129.0-insider`;
- commit `29d19ddd1af725baf537b6b328843bcdc2d29ba1`;
- build date 2026-07-09;
- official custom-agent, prompt, subagent, and hook documentation retrieved
   2026-07-10 (pages updated 2026-07-08); and
- tracked `schemas/vscode-agent-surface.schema.json`, whose digest is recorded in the
   release evidence.

The tracked schema is the immutable expected projection for all fields used here; live
documentation is provenance, not a mutable runtime authority. Any other VS Code build
must pass the identical preservation/schema-load/semantic-projection/archive-install/handoff/hook suite
before it is added to the supported set.

### Generated prompt contract

For each exact ID, the corresponding `.prompt.md` frontmatter selects
`agent: <exact-id>`. It MUST omit `tools`; official VS Code priority gives prompt tools
precedence over custom-agent tools. A generated prompt tool list—even if textually equal
today—is a contract failure because it creates a second policy authority. The prompt
does not select a different agent or silently use the current/default agent.

## Exact review-first handoff graph

Handoff buttons switch to the target agent with conversation context and the exact
prefilled prompt. They do not mutate executable state, satisfy evidence, submit work,
or schedule a lane. Every row has `send: false`; the user reviews and submits. Every
handoff-level `model` field is absent. No other edge is allowed.

| From | Exact target agent | Exact label | Exact prefilled prompt | `send` | handoff `model` |
|---|---|---|---|---:|---|
| `speckit.engloop.01-northstar` | `speckit.engloop.02-scaffold` | Create working scaffold | Use the accepted Northstar above to create the thin working slice and prove the test runway. | `false` | absent |
| `speckit.engloop.01-northstar` | `speckit.engloop.03-architect` | Re-derive architecture | Use the revised Northstar and current working evidence above to re-derive the governed architecture. | `false` | absent |
| `speckit.engloop.01-northstar` | `speckit.engloop.04-refactor` | Refactor under existing architecture | Use the accepted Northstar and existing governed architecture above to begin the SPEC-driven refactor. | `false` | absent |
| `speckit.engloop.02-scaffold` | `speckit.engloop.03-architect` | Derive architecture | Use the proven scaffold and test runway above to derive and govern the long-lived architecture. | `false` | absent |
| `speckit.engloop.02-scaffold` | `speckit.engloop.09-debugger-walk-thru` | Walk current slice in debugger | Use the proven runway above to prepare an engineer-led debugger walkthrough of the current thin slice before more implementation accumulates. | `false` | absent |
| `speckit.engloop.03-architect` | `speckit.engloop.04-refactor` | Start governed refactor | Use the accepted architecture above to run the governed specify → plan → tasks → implement loop. | `false` | absent |
| `speckit.engloop.04-refactor` | `speckit.engloop.05-model` | Model current behavior | Model the accepted architecture-conformant product behavior and its rejection semantics. | `false` | absent |
| `speckit.engloop.05-model` | `speckit.engloop.06-explore` | Explore the model | Explore the accepted model above and generate the functional suite into the proven test runway. | `false` | absent |
| `speckit.engloop.06-explore` | `speckit.engloop.05-model` | Repair model deficiency | Revise the behavioral model to address the exploration deficiency identified above before regenerating. | `false` | absent |
| `speckit.engloop.06-explore` | `speckit.engloop.07-validate` | Validate generated suite | Run the freshly generated suite above against the real SUT and publish functional reachability. | `false` | absent |
| `speckit.engloop.07-validate` | `speckit.engloop.04-refactor` | Fix SUT defect | Route the SUT defect identified above through the governed SPEC implementation loop. | `false` | absent |
| `speckit.engloop.07-validate` | `speckit.engloop.05-model` | Fix model gap | Revise the model to address the fidelity or behavior gap identified above. | `false` | absent |
| `speckit.engloop.07-validate` | `speckit.engloop.06-explore` | Fix exploration gap | Regenerate exploration and tests to address the coverage or generation gap identified above. | `false` | absent |
| `speckit.engloop.07-validate` | `speckit.engloop.08-unittest` | Classify reachability | Use the valid Stage 07 evidence above to classify every unreached path before adding unit tests. | `false` | absent |
| `speckit.engloop.08-unittest` | `speckit.engloop.04-refactor` | Correct design defect | Route the design or architecture defect identified above through the governed SPEC implementation loop. | `false` | absent |
| `speckit.engloop.08-unittest` | `speckit.engloop.05-model` | Model intended gap | Model the authoritative but functionally unreached behavior identified above before regenerating tests. | `false` | absent |
| `speckit.engloop.08-unittest` | `speckit.engloop.07-validate` | Revalidate deletion | Rerun the complete generated functional validation after the coherent residue deletion set above. | `false` | absent |
| `speckit.engloop.08-unittest` | `speckit.engloop.09-debugger-walk-thru` | Walk through review code in debugger | Use the current readiness PASS and exact base-to-HEAD diff to prepare an engineer-led debugger walkthrough ledger for every changed executable code chunk. | `false` | absent |
| `speckit.engloop.09-debugger-walk-thru` | `speckit.engloop.10-codereview-prepare` | Prepare code review | Use the current fully attested debugger-walkthrough ledger above to minimize and prepare the exact same HEAD for code review. | `false` | absent |
| `speckit.engloop.10-codereview-prepare` | `speckit.engloop.08-unittest` | Recompute readiness after review preparation | Re-run direct evidence and the sole readiness gate after the code-review preparation changes above; then repeat Stage 09 debugger walkthrough for the new HEAD. | `false` | absent |
| `speckit.engloop.20-incident` | `speckit.engloop.21-postmortem` | Analyze stabilized incidents | Analyze the selected stabilized incident set above and produce source learnings and repair items. | `false` | absent |
| `speckit.engloop.21-postmortem` | `speckit.engloop.22-repair` | Repair selected item | Route the selected RPI above through Stage 04 and all applicable Stage 05–08 gates. | `false` | absent |
| `speckit.engloop.21-postmortem` | `speckit.engloop.31-learnings-pyramid` | Condense learnings when capacity exists | When spare stewardship capacity exists, condense the accepted learning backlog above and validate retrieval. | `false` | absent |
| `speckit.engloop.22-repair` | `speckit.engloop.04-refactor` | Begin governed repair | Implement the repair item above through the governed SPEC loop before downstream verification. | `false` | absent |
| `speckit.engloop.30-refactor-scan` | `speckit.engloop.01-northstar` | Update direction | Update the living Northstar for the evidence-backed direction change selected above. | `false` | absent |
| `speckit.engloop.30-refactor-scan` | `speckit.engloop.03-architect` | Re-derive architecture | Re-derive governed architecture for the architecture-impacting refactor selected above. | `false` | absent |
| `speckit.engloop.30-refactor-scan` | `speckit.engloop.04-refactor` | Implement selected refactor | Route the selected refactor above through the governed SPEC implementation loop. | `false` | absent |
| `speckit.engloop.50-overlay-pack` | `speckit.engloop.01-northstar` | Define local direction | Define the overlay-local North Star after the private overlay verifies cleanly. | `false` | absent |

Stages 31, 40, 51, and 60 have no `handoffs` field and are natural terminals.
Stage 09 may be invoked after Stage 02 and repeatedly at later HEADs. Earlier DBG ledgers
remain historical observations but do not satisfy Stage 10 after HEAD changes. Stage 10
requires a final complete current-HEAD DBG ledger plus the generic current readiness record
emitted by Stage 08.
Stages 08 and 30 retain their listed conditional branch buttons even though PASS/no-work
can terminate naturally without a click. In particular, there is no 08→20, 08→30, or
08→31 edge, and no numeric adjacency creates an implicit edge.

## Ownership constraints

- The bundle composes; it contains no command logic.
- All 19 commands are owned by the one `engloop` extension of the `engloopkit` product.
- Architecture-guard and tinyspec remain external, pinned capabilities. Stage 22 never
  routes through tinyspec; its presence is not a repair fallback.
- Stage 07 owns functional conformance/reachability only.
- Stage 08 alone owns final readiness.
- Stage 08 PASS authorizes operation but creates no Stage 20, 30, or 31 invocation.
- Stages 20–22 and 30–31 require their own explicit demand/capacity inputs.

## Package and generated-surface checks

### Early Spec Kit 0.12.4 preservation experiment

Before accepting the production headers, a disposable fixture MUST:

1. pin and verify Spec Kit CLI 0.12.4;
2. create a temporary single-root fixture outside the source/install trees with a
   minimal extension whose command headers collectively cover every required field,
   nested hook value, scalar/list/map shape, nonempty and empty `agents`, one or more
   handoffs, and a no-handoff terminal case;
3. omit `infer` and `model` deliberately and include a generated-prompt expectation
   that selects the exact agent while omitting `tools`;
4. install only through Spec Kit's supported extension path, without copying or
   rewriting installed agents/prompts;
5. parse source commands, installed commands, generated agents, and prompts using
   YamlDotNet 18.1.0 and compare the canonical semantic projection;
6. assert every required field/subfield survives, every required absence remains
   absent, handoff targets resolve, and prompt precedence is not overridden;
7. run deterministic source/archive/disposable-install semantic validation; and
8. delete a passing fixture, but retain a failing fixture path and complete mismatch
   report for diagnosis.

Any dropped, rewritten, unresolved, or injected field is a hard prerequisite failure.
Implementation then stops for the smallest generic upstream Spec Kit capability change
or an explicit generation mode implemented and documented by upstream Spec Kit itself.
The identical experiment must pass against the newly pinned version/revision before the
production headers are released. An EngLoopKit-owned alternate generator,
post-processing a partial installation, copying hidden fallback agents, or weakening
this contract is forbidden.

### Full 19-agent package/install acceptance

After the preservation experiment passes, a clean package test MUST:

1. parse the source manifest and assert the exact ordered set;
2. assert every declared file exists and every undeclared command file is absent;
3. validate all 19 files against the command-loop shape, common header, exact
   tool/subagent matrix, entry hook/body gate, and 28-edge handoff table;
4. build the release archive and inspect its payload for the same set;
5. initialize a disposable single-root Spec Kit fixture;
6. create its sole tracked `.engloop/config.json` contract and prove forbidden roots
   are absent;
7. install the exact archive digest;
8. inspect `.specify/extensions/.registry`, installed commands, generated agents, and
   generated prompts;
9. assert 19 exact current entries, each once, and zero old entries;
10. parse source and installed YAML into a `GeneratedSurfaceSemanticComparison` and
   prove all 19 common fields, absence rules, exact tool/subagent policies, exact hook
    commands, and the exact ordered handoff edge/value set;
11. prove all handoff and `Explore` targets resolve and every generated prompt selects
    its matching exact ID while omitting `tools`;
12. record zero deterministic semantic mismatches from source, archive, and disposable
   installation projections;
13. exercise a valid and controlled invalid entry with
    `chat.useCustomAgentHooks: true`, proving the hook runs the exact versioned command
    and invalid entry blocks;
14. repeat the invalid fixture with agent-scoped hooks disabled, record the session as
   reduced-assurance, prove the body unconditionally invokes and obeys the validator,
   and prove the trusted tool rejects any invalid durable transition/evidence attempt;
15. verify the focused workspace enables only `chat.useCustomAgentHooks` plus explicit
    manual-edit protection for agent/prompt/tool-manifest/config files, with no blanket
    edit/terminal approval, Bypass Approvals, or Autopilot setting; and
16. remove the extension and prove all EngLoop-owned current/generated entries are gone.

The test fails on first mismatch and retains the disposable fixture path for diagnosis;
it does not install a development source as an alternate success path.

## Version rule

The current command-surface target is 1.11.0. “Ordered EngLoop v2” identifies this workflow
generation only and provides no authority for a 2.x product release. Bundle, extension,
tool, catalog, archive names, and release notes MUST agree on 1.11.0. Catalog SHA-256
values are computed from final immutable artifacts. Rebuilding different bits under
1.11.0 is forbidden. Any 2.x value is a release-blocking error unless a later explicit
maintainer decision supersedes this contract.
