# Contract: Executable Ordered EngLoop v2

- **Feature:** SPEC001
- **Applies to:** `src/EngLoopKit.Core`, independent SEK model, CORD, generated tests,
  and direct tests

## Principle

A stage number identifies an invocation surface. It is not an automatic scheduler and
is not sufficient evidence for a transition. Every accepted attempt is evaluated over
rich state and current evidence; every rejected attempt leaves state unchanged and
returns a stable actionable reason.

Every attempt also has a repository-layout precondition: the explicitly selected root
contains exactly one tracked `.engloop/`, exactly one `.engloop/config.json`, ignored
transient `.engloop/out/`, and visible root `NORTHSTAR.md` / `LEARNINGS.md`; it contains
no current `engloop/` compatibility tree or `.engloopkit/` directory. Layout validation
precedes config/evidence evaluation. Missing or ambiguous layout is rejected without
probing another root or mutating workflow state.

## Handoffs are a UI projection, not executable transitions

The exact handoff graph in
[`command-surface.md`](command-surface.md#exact-review-first-handoff-graph) projects
reviewable legal branch choices into VS Code. It is related to this contract but is a
different artifact:

- a handoff row may exist only for a branch this contract can legally request after
   the source stage reports the corresponding finding;
- selecting a handoff switches to the named agent with conversation context and an
   exact prefilled prompt;
- all handoffs use `send: false`, so selection alone does not submit the prompt;
- selection or display creates no `TransitionAttempt`, changes no
   `EngineeringLoopState`, stamps no evidence, clears no obligation, and satisfies no
   gate;
- when the user submits, the target agent's versioned `agent-entry` hook and mandatory
   body check re-evaluate the current root/state/evidence; every accepted durable
   transition/evidence operation independently enforces that gate;
- absence of a button does not erase an independently demand-driven invocation, and
   presence of a conditional branch button does not schedule it.

Therefore the handoff edge set is neither the state machine's transition table nor a
numeric-adjacency scheduler. Stage 08 exposes only its 04/05/07 corrective branches and
never offers 20/30/31; a readiness PASS authorizes operation but creates no incident or
stewardship work. Stage 21's 31 button is a capacity-conditioned suggestion, not an
automatic successor. Stage 31 has no static handoff and returns to its saved invoking
context. Any implementation that mutates state on button rendering/click or submits a
handoff automatically is non-conformant.

## State required for every implementation/model

The SUT and independent model MUST distinguish at least:

- selected repository identity, accepted root-layout/config digest, and whether its
   durable `.engloop/` files are tracked;
- last accepted stage and delivery cursor;
- current product/model/exploration/validation revisions;
- readiness PASS/FAIL and the product revision it proves;
- pending repair obligations and whether release/target proof is complete;
- pending Learnings Pyramid refresh and its accepted-source digest;
- Stage 08 reached/unreached path set and disposition status;
- actual incident, stabilization, selected post-mortem set, and repair-item demand;
- spare stewardship capacity;
- Stage 30 no-work/selected-work, direction-change, and architecture-impact outcomes;
- return context for independent Stage 31 work.

A model whose only durable state is a stage enum is non-conformant.

## Normal delivery lane

| From completed evidence | Requested stage | Additional guard | Accepted effect |
|---|---|---|---|
| none / no direction | 01 | exactly zero or one authoritative root Northstar candidate | create/evolve singleton Northstar |
| 01 | 02 | complete Northstar | enter scaffold/runway proof |
| 02 | 03 | complete current runway proof | derive/govern architecture |
| 03 | 04 | accepted architecture | run governed specify→plan→tasks→implement |
| 04 | 05 | accepted implementation, green build, architecture conformant | model current product revision |
| 05 | 06 | adequate current model | explore/generate into Stage 02 destination |
| 06 | 07 | complete fresh generation | run generated suite against real SUT |
| 07 | 08 | current functional PASS and reachability report | begin disposition; no direct tests yet |
| 08 disposition complete | 08 readiness computation | deletion revalidation current; direct tests/coverage/architecture/regressions current | emit PASS or FAIL inventory |

A Stage 08 FAIL records blockers but does not authorize operations.

## Feedback paths

| Finding | Required route | Forbidden shortcut |
|---|---|---|
| Stage 07 identifies a SUT defect | 07→04 | editing/deleting generated test to pass |
| Stage 07 identifies model/fidelity gap | 07→05 | weakening invariant or hand-writing expected error |
| Stage 07 identifies exploration/generation gap | 07→06 | substituting unit coverage |
| Stage 08 finds intended unreached behavior | 08→05→06→07 | retaining via a new unit test |
| Stage 08 deletes unsupported residue | 08→07, then resume 08 only after full green revalidation | continuing on a stale Stage 07 report |
| Stage 08 finds design/architecture defect | 08→04 | local patch outside governed work |

Non-reachability is evidence requiring classification. It is never an automatic
`unsupported-residue` verdict.

## Operations lane

1. Operations entry requires a current Stage 08 PASS for the current product revision.
2. Stage 20 additionally requires an actual operating disruption.
3. Stage 20 may repeat for additional real incidents; it records mitigations and verified
   stabilization only.
4. Stage 21 requires a deliberately selected non-empty set of stabilized incidents.
5. Stage 21 may create accepted source learnings and repair items. New learnings set the
   independent Stage 31 refresh obligation; they do not close repair.
6. Stage 22 requires one or more repair items and creates/retains an open repair
   obligation.
7. Stage 22 routes to Stage 04 and every applicable Stage 05–08 gate.
8. A repair closes only after source, immutable release artifact, exact target
   application, target verification, and a current Stage 08 PASS all agree.

There is no small-change/tinyspec bypass in this lane.

## Stewardship lane

### Stage 30

Requires explicit spare engineering or agent-token capacity. It evaluates the ratified
priority tree and records exactly one REFACT result:

- `none-this-cycle`: return to steady context; no product stage changes;
- selected refactor without direction change: route to 04;
- selected refactor with direction change: route to 01, then 03 if architecture impact
  exists, otherwise 04 after the direction update;
- selected refactor with architecture impact but no direction change: route through 03
  then 04.

It never creates a numbered direction snapshot and never routes directly to 08.

### Stage 31

Requires explicit spare capacity and an accepted-learning backlog/pending source-set
change. It may run independently from Stage 30 and returns to its saved invoking
context. It clears the pending obligation only when static pyramid validation and the
clean-context retrieval suite pass against identical input digests. It neither blocks
incident stabilization nor substitutes for Stage 22.

## Required representative rejections

The executable core and generated conformance MUST reject, with state unchanged:

| Attempt | Stable reason category |
|---|---|
| missing `.engloop/` or `.engloop/config.json` | `missing-process-root` |
| current `engloop/`, `.engloopkit/`, duplicate, or case-ambiguous root | `ambiguous-process-root` |
| unknown/malformed command ID | `invalid-command` |
| duplicate initial start | `duplicate-start` |
| 02→04 | `missing-architecture` |
| 04→07 | `missing-model-or-exploration` |
| 07→20 | `missing-current-readiness` |
| 21→04 | `missing-repair-routing` |
| 22→08 | `repair-gate-bypass` |
| 30→08 | `refactor-gate-bypass` |
| Stage 20 with no incident | `no-incident-demand` |
| Stage 21 with no selected stabilized set | `no-postmortem-selection` |
| Stage 22 with no repair item | `no-repair-demand` |
| Stage 30/31 with no explicit capacity | `no-stewardship-capacity` |
| operation after product revision invalidated readiness | `stale-readiness` |
| Stage 08 before all paths are classified | `unclassified-reachability` |
| direct test addition before disposition completion | `unit-tests-too-early` |
| deletion when requirement/runtime entry is ambiguous | `ambiguous-reachability` |

Generated negatives MUST include at least one illegal-order rejection and one invalid
input rejection derived from model guards. Direct tests may deepen diagnostics but do
not satisfy this generated-evidence requirement.

## Readiness currency

Let `P` be current product revision and `R.productRevision` the revision in the latest
readiness evidence.

```text
CurrentReadiness =
    R.verdict == PASS
    AND R.productRevision == P
    AND all referenced evidence digests still match
```

Any governed source/config/module/model/exploration change invalidates the dependent
evidence. Time alone does not make evidence current; a new timestamp cannot mask a
digest mismatch.

## Independent model requirements

The SEK model MUST be authored separately from the SUT graph and MUST NOT call
`EngineeringLoop.IsLegalTransition` or import its transition table. It must include
bounded interacting state for readiness currency, repair, learning refresh, incident
demand, capacity, and reachability. CORD MUST produce multiple materially distinct
paths and generated tests for:

- normal 01–08 PASS;
- 08 authorization with no automatic operation/stewardship action;
- actual 20→21→22 repair route back through 04 and 05–08;
- repeated incidents before a selected post-mortem set;
- Stage 30 no-work and direction/architecture branches;
- Stage 31 independent pending/clear behavior;
- intended-gap return and residue-delete→07 revalidation;
- representative illegal ordering, invalid command/input, duplicate start, stale
  readiness, and absent-demand attempts.

Exploration bounds are expressed with `RequireBound`; product illegality uses real
`Require` guards so negatives remain model-derived.

Root-layout rejection is exercised independently from stage-order rejection. A layout
failure cannot be converted into a valid model path by reading legacy config or stale
evidence from another directory.

## Conformance completion

This contract passes only when:

- every declared legal transition class has generated positive evidence;
- required rejection classes have generated negative evidence;
- generated tests drive one stateful SUT instance per path at the real boundary;
- direct tests cover full finite transition/reason tables and invariants;
- the installed 23-edge handoff graph equals the command-surface UI projection, every
   edge is review-first, and handoff display/click tests prove zero state mutation or
   lane scheduling before target submission and validation;
- prose, command package, SUT, model, CORD, generated suite, and tests contain the same
  13-stage vocabulary and lane semantics.
