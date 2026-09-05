# Incident IN007: EngLoopKit v1.17 release cycle exceeded two days

- **Started:** 2026-09-03
- **Declared:** 2026-09-05
- **Reported by:** EngLoopKit maintainer
- **Affected:** EngLoopKit v1.17.0 release process and self-hosting release workflow
- **Status:** MITIGATING
- **Cause-class (preliminary):** release-process / validation-latency / evidence-reuse
- **North Star consulted:** `NORTHSTAR.md`, revision `Last revised: 2026-07-10`
- **Learning cue consulted:** `readiness-is-a-gate`, `PM001/LEARN001` and
  `PM001/LEARN003`; readiness remains check output and MUST NOT be fabricated.

## Incident demand

The maintainer explicitly declared the nearly three-day v1.17.0 release cycle a new
incident and required immediate mitigation: publish the already-tested release, then run
a separate post-mortem on why ELK self-hosting expanded a minor release into a multi-day
cycle and how to enforce a release budget below 90 minutes.

## Symptom and impact

- The release remained unpublished for nearly three days despite the product and review
  workflow test suites passing.
- The release gate ran a 33m49s core coverage partition and a 1h35m overlay coverage
  partition before reporting the Tool branch row at 84.48%.
- Repeated focused coverage attempts added delay because Coverlet merged stale branch
  identities after source refactoring; one health-check command also canceled an active
  redundant coverage process.
- C: temporary storage filled during an earlier whole-suite run, causing cascading
  environmental test failures and additional reruns.
- User trust and release predictability were materially harmed.

## Timeline and mitigations

| Time | Action | MIT | Evidence / result |
|---|---|---|---|
| T0 | v1.17.0 implementation and release validation began. | — | Release target `1.17.0`; exact 29-command/32-edge surface. |
| T1 | Disk-safe complete main test inventory and generated model suite completed. | MIT001 | 3,720 tests passed across 3,322 generated and 398 direct/overlay tests; later exact changed workflow suite passed 49/49. |
| T2 | Whole-product readiness ran from committed source. | — | Generated 3,322/3,322, core 334/334, overlay 64/64, build and architecture gates PASS; final readiness verdict NOT READY because Tool branch coverage was 84.48%. |
| T3 | Readiness claim withheld. | MIT002 | `.engloop/out/cov003-readiness.json` recorded `branch-coverage-below-threshold:tool:84.48`; no PASS was emitted or fabricated. |
| T4 | Maintainer ordered emergency publication as incident mitigation, with failed readiness disclosed and permanent repair deferred to post-mortem. | MIT003 | Owner decision in active incident conversation, 2026-09-05. |
| T5 | Incident entry command was attempted but could not bind because current readiness was absent. Context was explicitly deferred rather than treated as validated. | MIT004 | `dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.20-incident --root .` → `missing-current-readiness`. |

## Current mitigation contract

1. Publish exactly the committed v1.17.0 source and four locally validated release assets.
2. Release notes MUST disclose that the 95% Tool branch readiness gate did not pass and
   that publication is an owner-authorized incident mitigation, not a readiness verdict.
3. Do not claim repair closure. The release-time budget, evidence reuse, coverage model,
   disk preflight, cancellation behavior, and release-agent design belong to Stage 21/22.
4. After GitHub publication, record the tag, commit, asset hashes, URL, and stabilization
   evidence in this incident.

## Stabilization criteria

- [ ] GitHub release `v1.17.0` is public and non-draft.
- [ ] Exactly four expected assets are attached and their SHA-256 values are recorded.
- [ ] `origin/master` and tag `v1.17.0` point to the intended release commit.
- [ ] Release notes disclose the NOT-READY Tool branch result and emergency mitigation.
- [ ] No permanent repair or post-mortem closure is claimed.

## Stage 21 handoff

Post-mortem must measure the actual release critical path and design an enforceable
sub-90-minute release system. Candidate topics include a dedicated release agent,
preflight coverage impact before version freeze, content-addressed immutable evidence,
parallel bounded partitions, disk-capacity preflight/cleanup, cancellation-safe execution,
single-attempt orchestration, and a policy for changed-code versus whole-product gates
that preserves the non-negotiable quality bar without stale branch-identity artifacts.