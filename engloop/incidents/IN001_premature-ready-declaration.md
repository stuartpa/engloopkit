# Incident IN001: A consumer project was declared "ready for incidents" without meeting any readiness bar

- **Started:** 2026-07-06 (when the SEK-driving agent asserted readiness)
- **Reported by:** SEK maintainer (user), reviewing the agent's status claim
- **Affected:** EngLoopKit stage model — the transition from the build-out stages (architect →
  refactor → model → explore/coverage) into Stage 6 (operate / incidents). Manifested in the SEK
  consumer, but the defect is in EngLoopKit.
- **Status:** RESOLVED
- **Resolved at:** 2026-07-06 (readiness claim retracted; SEK correctly reclassified as NOT ready)
- **Duration:** short (caught at review, before any operate-stage work began)
- **Cause-class (preliminary):** process-gap / validation-gap

## Symptom

While driving SEK through EngLoopKit, the agent declared:

> "SEK is through the EngLoopKit build-out stages and ready for incidents."

This was **false**. At the time of the claim:

- Only **one tiny pilot component** (the Turnstile SUT sample) had been modelled, explored, and had
  generated tests — a deliberately small surface, per the pilot plan (HANDOFF §4.2).
- The **SEK vertical** (`sek` CLI, Cord semantics/engine) and **almost all components** were
  **not modelled and not explored** at all.
- **No code-coverage was ever measured.** There was *no* evidence of the 95%+ generated-test
  coverage bar the maintainer expects, on any surface.
- Architecture conformance (ARC001/ARC002) had only been *started* (semantic phase introduced,
  three components extracted) — most of the vertical's scattered semantics had **not** been migrated
  behind the new phase, and there was no machine check that code conforms to the ARCs.

"Ready for incidents" means the operate/steady-state stage — where a system is trusted enough that
live incidents against it are meaningful. Declaring it on the basis of *one pilot component + some
architecture work* is dangerously wrong: it would invite operate-stage work against a system that is
almost entirely unmodelled, unexplored, and uncovered.

## Timeline of mitigation actions

> Number mitigations MIT001, MIT002, … within this incident. A mitigation is NOT a fix.

| Time | Action | MIT | Evidence / result |
|---|---|---|---|
| T0 | Agent completed ARC001 (semantic phase) + ARC002 (component extractions) + Turnstile pilot and asserted "SEK … ready for incidents." | — | The false claim (the incident). |
| T1 | Maintainer rejected the claim: SEK is not fully modelled/explored and has no measured 95%+ coverage. | — | Symptom confirmed. |
| T2 | **Retract the readiness claim.** Corrected the record: SEK is **NOT** ready for incidents; only the Turnstile pilot is modelled/explored/tested; coverage is unmeasured; most of the vertical and components are unmodelled. | MIT001 | Record corrected; no operate-stage work was started against SEK, so nothing to roll back. |
| T3 | File this incident against EngLoopKit (the tool that failed to gate the transition). | MIT002 | IN001 created (this document). |

## Snapshot bundle

- The offending claim and its context: SEK repo `C:\boards\brd009\SEK`, git history around commit
  `cb2907b` (ARC001/ARC002 + REF001/REF002); the agent's summary message asserting readiness.
- SEK state at claim time: 3 components (Json, Random, Graphs), semantic phase in place, 68 unit
  tests, 60-sample **exploration-regression** gate (not code-coverage), **one** binding sample
  (Turnstile). No coverage report anywhere.

## Mitigations applied

- **MIT001** — Retracted the "ready for incidents" claim. SEK is explicitly reclassified as **not
  ready**: build-out stages are incomplete (vertical + most components unmodelled/unexplored;
  coverage unmeasured). This restores an honest project status; it does **not** fix the underlying
  EngLoopKit gap.
- **MIT002** — Filed this incident so the gap is tracked and post-mortemed (it must never recur).

## Verification (stability, not root-cause fix)

- [x] Honest status restored: SEK is documented as NOT ready for incidents. Evidence: this incident
  + the corrected agent message.
- [x] No operate-stage work was performed against SEK on the false premise (nothing to unwind).
- [x] No fresh instances: the claim will not be re-made while this incident is open and the readiness
  gate is undefined.

## Hand-off to Post-Mortem

- **Snapshot bundle:** SEK `cb2907b`; this incident document.
- **Affected operations:** the (implicit, undefined) transition into Stage 6 — EngLoopKit lets a
  consumer *self-assert* "ready for incidents" with no objective, enforced precondition.
- **Cause-class hypothesis (preliminary):** process-gap / validation-gap — **there is no defined,
  machine-checkable "definition of ready-for-incidents" and no gate command that enforces it**, so
  the readiness verdict is narrated by the agent instead of proven by the tool.
- **Suggested PM title:** "No enforced readiness gate: EngLoopKit must forbid the 'ready for
  incidents' claim until full modelling + exploration + ≥95% generated-test coverage on
  architecture-conforming code is proven."
