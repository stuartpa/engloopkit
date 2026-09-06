# Owner directives: bounded releases and unconditional agent navigation

- **Recorded:** 2026-09-06
- **Authority:** Explicit maintainer instruction in the current Stage 42 conversation.
- **Scope:** ELK self-hosting and generic guidance for downstream products.
- **Status:** Accepted owner requirements; implementation and postmortem repair remain open.

## User direction

The owner requires that small releases never routinely expand into multi-day cycles and
that users MUST remain in control of moving to the next agent. In particular, ELK MUST
NEVER deny Incident → Postmortem navigation. The preceding release conversation established
an under-90-minute release target and requested timing for every phase.

## Interpretation

Agent selection is user intent, not evidence of stage completion or authorization for a
destructive operation. Enter the selected agent, acknowledge outstanding evidence, and
continue discussion or evidence collection. Do not require readiness, stabilized status,
internal IDs, successful Stop, or completed repair before allowing that navigation.
Retain narrowly scoped authorization checks for actual mutations and truthful success
claims. An active incident may be analyzed in Postmortem without claiming stabilization.

Budget overruns require early visibility and a user decision, not endless autonomous
retries. Preserve evidence and resumability; never convert timeout or missing evidence
into PASS. Do not start another release merely to ship these learning edits.

## Evidence and provenance boundary

- [IN007](../incidents/IN007_v117-release-cycle-exceeded-two-days.md) records the multi-day
  release delay, 33m49s core and 95-minute overlay coverage phases, disk exhaustion, and
  repeated incompatible coverage merges.
- [IN008](../incidents/IN008_v117-upgrade-contract-failure.md) records the count-only
  manifest, missing ordered IDs, late updater validation, and missing prior-stable proof.
- The conversation also records repeated scope expansion, unsupported time estimates,
  interrupted/repeated tests, and coverage-metric-driven boolean rewrites. These are
  observable actions, not a completed causal analysis.

Existing PM001 and PM002 remain historical sources. The cards explicitly distinguish
their accepted principles from these new owner requirements. No new PM/LEARN IDs or
positive-history sources are invented here.

## Required executable follow-up (not implemented by this learning refresh)

- Test user navigation across direct selection, handoff, delegation, and resumed chats
  with missing readiness/IDs, active incidents, failed hooks, and incomplete evidence.
  Incident → Postmortem must remain available in every case; false completion must not.
- Introduce measured, bounded, resumable release orchestration and a canonical manifest
  contract. Prove prior-stable upgrade against exact final assets before promotion.
- Measure p50/p95 request-to-verified-release latency and repeat work. Alert before the
  90-minute target is exceeded. Investigate slow operations rather than inferring progress
  from a responsive process or cumulative CPU time.
- A new agent is optional UX, not the fix by itself. Enforce these properties in the
  runtime/orchestrator and test them; do not add another mandatory navigation gate.