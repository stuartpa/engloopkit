# Incident IN009: operations evidence gates blocked user navigation

- **Recorded:** 2026-09-06; exact onset and elapsed duration not established.
- **Reported by:** EngLoopKit maintainer, HANDOFF012/013 repair request.
- **Affected:** EngLoopKit Stage20/21/22 operations hooks, agent guidance, and command-surface validation.
- **Status:** MITIGATING — source/docs repairs implemented; scoped compatibility verified; release blocked by unresolved whole-product readiness.
- **Cause class:** navigation/evidence coupling; source/installed contract mismatch.
- **Postmortem:** [PM005](../postmortems/PM005_navigation-is-not-evidence-acceptance.md), ANALYZED, not closed.

## Symptom and scope

The maintainer reported recovery blocked by lifecycle failures, missing internal scope,
and entry/surface assumptions. Stage21 guidance required scope markers before any tool
and barred analysis of active incidents; Stage22 conditioned its handoff on Stop
validation. These EngLoopKit defects conflict with the accepted owner navigation
directive. No private workload defect, identity, or application-specific cause is asserted.

## Chronology and mitigations

Sequence labels record order, not invented timestamps. HANDOFF012/013 are owner-supplied
report identifiers; their original packets are not present in this worktree's tracked
handoff directory, so no packet contents or execution results are inferred.

| Sequence | Event / mitigation | Evidence and limits |
|---|---|---|
| 1 | Owner required unconditional stage navigation while retaining strict evidence gates. | [Owner directive](../learnings/owner-directives-release-and-navigation.md); no waiver of PASS requirements. |
| 2 | HANDOFF012/013 repair request identified operations lifecycle and command-root defects. | Current request; existing source/test modifications were present before this documentation pass. |
| 3 | MIT001: retain source repair and continue recovery without accepting failed evidence. | `OperationsHookCommands.WriteLifecycleDeferred`, `WritePostmortemScopeDecision`, `PostTool`; inspected source, not a runtime result. |
| 4 | MIT002: remove contradictory Stage20/21/22 body instructions in both agent and command surfaces. | Six documentation files; hook/frontmatter metadata retained. |
| 5 | MIT003: reserve IN009/PM005 before creating records; keep stabilization and closure unaccepted. | [Registry](../numbering-registry.md); PM005 lists remaining tests and release work. |

## Direction and learning context

Consulted [NORTHSTAR.md](../../NORTHSTAR.md), [LEARNINGS.md](../../LEARNINGS.md),
[readiness-is-a-gate](../learnings/cards/readiness-is-a-gate.md) with
[PM001/LEARN001–003](../postmortems/PM001_no-readiness-gate.md#learnings), and
[verification-follows-artifact-class](../learnings/cards/verification-follows-artifact-class.md)
with [PM002/LEARN001–003](../postmortems/PM002_gate-verification-method-by-class.md#learnings).
Hashes and the owner-authority interpretation are recorded in PM005. Consultation is
not an `incident-context` validator PASS.

## Stabilization still required

- [ ] Validation owner proves navigation recovery and no false acceptance across direct,
  delegated, resumed, Stop and SubagentStop paths using deterministic CLI fixtures.
- [ ] Missing/invalid/stale scope permits local read/search/questions while denying
  protected writes, execution and arbitrary delegation until valid binding.
- [ ] Exact tested release assets pass installed-surface and supported-upgrade checks.
- [ ] Record authoritative stabilization evidence; do not close from source/doc edits.

## Release continuation — 2026-09-07

The owner explicitly authorized a new repair release. The saved compatibility TRX at
`.engloop/out/navigation-repair-tests/compatibility.trx` records 360 executed/passed,
zero failed and zero skipped. SHA-256:
`633200977397f2e1b2a48bb38b33bfd9e768cd20f126878331020339d5e310e1`.
Its start-to-finish interval is 518.79 seconds; the owner-supplied command elapsed time
is 530.69 seconds. These are distinct timing boundaries. The long suite was not rerun.

A bounded `InstalledCommandSurfaceTests` run against existing Debug binaries passed
59/59, zero skipped, in 41.06 seconds command elapsed. Evidence:
`.engloop/out/navigation-repair-tests/blocked-release-installed-surface.trx`.
This no-build run is not a fresh build or immutable-package upgrade proof.

Publication is BLOCKED: `README.md`, Development and release gates, requires product
readiness before release artifact hashes are emitted. The saved
`.engloop/out/cov003-readiness.json` (2026-09-05) says `NOT READY` with
`branch-coverage-below-threshold:tool:84.48`; it is historical evidence, not a current
measurement of this repair. No current whole-product PASS has been established.
The owner directive and verification card do not waive that prerequisite. A previous
corrective release is not a policy exception. No version bump, package generation,
upgrade proof, tag, push, or publication was performed in this continuation.

Next decision: satisfy the existing readiness prerequisite or obtain an explicit,
documented owner policy decision for corrective publication without a readiness claim.
Stabilization, PM acceptance, SEK escape proof and repair closure remain unaccepted.
