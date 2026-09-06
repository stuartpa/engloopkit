# Card: Verification follows artifact class

- **Recall cue:** Is a small release becoming a multi-day validation cycle, or using the wrong proof?
- **Apply when:** Classifying modules, selecting unit/property versus SEK verification,
  or designing a whole-product readiness inventory.
- **Status:** Living
- **Last condensed:** 2026-09-06

## Compressed principle

Keep one quality bar but choose the verification method from the artifact's nature.
Generic, domain-free components earn evidence through unit/property tests. The
stateful domain vertical earns behavioral evidence through SEK. Generic code left in
the vertical is a boundary failure, not permission to evade either method.

## Decision checks

1. Does the module pass the domain-free component litmus test?
2. If yes, is it extracted and directly unit/property tested rather than given a
   tautological model?
3. If no, does the residual vertical receive real behavioral model/exploration proof?
4. Did changing the method accidentally lower line/branch, architecture, or green-test
   requirements? The bar must remain common.

## Owner-directed release operating rules

The [2026-09-06 directive](../owner-directives-release-and-navigation.md), informed by
IN007/IN008, requires a **90-minute small-release target**, measured from release request
through public asset and supported-upgrade verification. Multi-day rerun cycles are a
process incident, not acceptable routine work. This is a target with escalation, not a
guarantee that an arbitrary defective change can safely ship in 90 minutes.

1. Freeze scope and identify the actual changed contracts; defer unrelated reviews,
  refactors, new agents, and documentation expansion. Version size is not risk evidence.
2. Preflight disk, SDK, package sources, credentials, manifest schema and complete ordered
  command IDs before expensive tests. Prior-stable upgrade is distinct from fresh install.
3. Time planning, edits, restore/build, tests, coverage, packaging, upload, and verification.
  Record wall time, exit status, inputs, and artifact digests. Report measured progress;
  do not invent timings for reasoning or promise “final run” without evidence.
4. Use one resumable orchestrator and one manifest builder. Run tests with coverage once
  per validated input set. Reuse evidence only through an explicit supported contract
  binding source, dependencies, toolchain, configuration, tests, and instrumentation.
  Never merge branch hits across changed binaries or guess mappings from line numbers.
5. Prioritize cheap contract checks and the real prior-stable updater using final artifacts.
  Hash equality proves integrity, not installability. Promote tested bytes; do not rebuild.
6. Forecast using measured prior runs. Escalate as soon as the forecast exceeds 90 minutes;
  present the blocker and user choices, and stop automatic rerun churn. A budget breach
  never fabricates PASS, silently waives a gate, or blocks the user's next agent.
7. Never change boolean evaluation, exclude code, or lower thresholds merely to improve a
  coverage number. Add meaningful missing tests or obtain an explicit policy decision.

## Source learnings

- [PM002/LEARN001](../../postmortems/PM002_gate-verification-method-by-class.md#learnings)
  — verification method follows artifact class.
- [PM002/LEARN002](../../postmortems/PM002_gate-verification-method-by-class.md#learnings)
  — extracting generics is a precondition for honest self-validation.
- [PM002/LEARN003](../../postmortems/PM002_gate-verification-method-by-class.md#learnings)
  — refine how evidence is measured without lowering what is required.

## Tensions

Artifact-specific verification may evolve, but it must not lower the shared
coverage, architecture, and green-regression bar.

The release budget adds a latency requirement; it does not establish a new coverage
waiver. IN007/IN008 are incident evidence, not completed postmortem provenance. Their
permanent repair and executable timing/upgrade gates remain open until independently proven.