---
name: speckit.engloop.40-refactor-plan
description: Work with the user to create one architecture- and North-Star-aligned, profile-bounded REFACT plan; point is the safe default and no product code is changed.
argument-hint: "--scope <path-or-topic> [--profile <point|bounded|deep>] [desired outcome and constraints]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.40-refactor-plan --root .
      timeout: 30
  UserPromptSubmit:
    - type: command
      command: dotnet tool run engloopkit -- refactor-profile bind
      timeout: 30
  Stop:
    - type: command
      command: dotnet tool run engloopkit -- refactor-profile clear
      timeout: 30
handoffs:
  - label: Update direction
    agent: speckit.engloop.01-northstar
    prompt: Update the living Northstar for the confirmed evidence-backed direction change in the REFACT plan above.
    send: false
  - label: Re-derive architecture
    agent: speckit.engloop.03-architect
    prompt: Re-derive governed architecture for the confirmed architecture-impacting REFACT plan above before implementation.
    send: false
  - label: Implement accepted refactor plan
    agent: speckit.engloop.04-refactor
    prompt: Implement only the confirmed REFACT plan and its next ordered slice. Treat the cited Northstar and architecture decisions as binding; return to planning if scope or boundaries must change.
    send: false
  - label: Inspect certain dead code
    agent: speckit.engloop.41-deadcode
    prompt: Search for the single highest-certainty dead-code candidate. Create a numbered DEADCODE proposal with deletion-proof evidence before asking whether to remove it.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`. Create at most one
`.engloop/refactors/REFACT<NNN>_<short-title>.md` plan after user confirmation.

## Loop definition

- **Trigger:** explicit stewardship capacity exists and the user wants to improve code structure or architecture conformity.
- **Goal:** work with the user to produce one confirmed REFACT plan (or no-work result) aligned with current direction and architecture, with special attention to extracting generic/non-vertical code into components.
- **Actions:** bind scope/profile, load governing direction and architecture, inspect the permitted code breadth, discuss evidence-backed options with the user, and record the confirmed plan only.
- **Verification:** the plan cites current guidance, classifies vertical/component boundaries, contains ordered implementation slices and acceptance checks, and changes no product source.
- **Memory:** `.engloop/refactors/` plus cited `NORTHSTAR.md`, `.engloop/architecture/`, learning, and code evidence.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.40-refactor-plan --root .`

## Mandatory guidance load

Before proposing a refactor:

1. Read `.engloop/config.json` and resolve its exact `northstarPath`; read that living North Star (normally root `NORTHSTAR.md`). Record its path and current Git/blob or SHA-256 identity in the plan.
2. Enumerate `.engloop/architecture/ARCH*.md`, then read the architecture decisions applicable to the declared scope. Do not preload unrelated decisions. Record every governing architecture path and identity used.
3. Read `docs/component-pattern.md` and apply its litmus test: code useful unchanged in an unrelated product is a component; vertical-specific code remains in the vertical.
4. Follow relevant `LEARNINGS.md` cues to their cards and source provenance. Do not invent a principle when no cue fits.
5. Inspect only the code/dependency/history/test evidence allowed by the active profile.

If the North Star or governing architecture is missing, ambiguous, or contradictory, do not invent a plan. Route direction questions to Stage 01 and architecture questions to Stage 03.

## Collaborative planning boundary

Stage 40 plans with the user; it does not implement.

1. Capture the user's desired outcome, pain point, constraints, exclusions, and appetite for change. Ask concise questions only for missing decisions that materially affect the plan.
2. Explain the observed architecture/component issue in plain language and show how it relates to the North Star and cited architecture decisions.
3. Present the best candidate within the active profile and concise alternatives/tradeoffs. Prefer moving generic/non-vertical responsibilities into one-purpose components with dependencies pointing vertical → component.
4. Define the proposed component API/responsibility, vertical composition changes, dependency direction, migration order, tests/model impact, rollback boundary, and done checks. Avoid speculative framework creation or generic components without a real user.
5. Ask the user to confirm or revise the plan. Do not write a `CHOSEN` REFACT artifact until the selected plan/scope is explicit. A user-requested no-work result may be recorded as `NONE-THIS-CYCLE`.
6. Never edit product source, tests, models, solution/project files, configuration, or deployment state. Only the REFACT plan/registry may change in this stage.

## Compute profile contract

Runtime model metadata—including selected model/tier/thinking level/token budget/price—is not authoritative. Never infer it. `--scope` is required; omitted profile means `point` and records `DEFAULT-POINT`; `bounded` and `deep` require explicit values; never silently promote or widen scope.

| Profile | Investigation boundary | Permitted REFACT plan | Subagent budget |
|---|---|---|---|
| `point` | One path/topic and nearest seam. No repository-wide survey. | One local simplification or one-component extraction. | None. |
| `bounded` | One subsystem plus direct dependencies and applicable architecture. | One cohesive subsystem/component-boundary plan in reviewable slices. | At most one read-only `Explore` survey. |
| `deep` | Repository-wide direction, architecture, dependencies, hotspots, incidents, and learnings. | One justified phased campaign, never an unbounded rewrite. | At most two read-only `Explore` surveys. |

Recommended only: MAI-Flash-1.1 or Luna/low-thinking → `point`; Tera/medium-thinking → `bounded`; deliberate SOL/frontier max-thinking → explicit `deep --scope repository`.

`point` is intentionally optimized for inexpensive/fast models. `deep` is appropriate
for deliberate frontier/high-thinking use. Stage 04 owns implementation after this plan
is confirmed.

## Required REFACT plan content

Use `REFACT-template.md`: confirmed outcome/scope/exclusions/profile; North Star and architecture identities/alignment; vertical/component classification; proposed component responsibilities/APIs/dependencies; alternatives; ordered Stage 04 slices; tests/model impact; rollback/done checks; routing and exact next slice.

## Quality invariants

Read `.engloop/learnings/cards/verification-follows-artifact-class.md` and `PM002/LEARN001–003`. Generic components receive unit/property evidence; the residual vertical receives applicable SEK behavior evidence; all retain 95% line/branch, conformance, and green-regression requirements.

## Done when

- [ ] Current North Star and applicable architecture decisions were read, identified, and cited
- [ ] User outcome, constraints, alternatives, and selected plan are explicit
- [ ] Vertical/component classification and extraction opportunities are explicit
- [ ] One confirmed REFACT plan or no-work result is recorded; no product source changed
- [ ] Ordered Stage 04 slices, acceptance checks, model/test impact, and routing are explicit
- [ ] Profile/scope stayed bound and no model capability was inferred
