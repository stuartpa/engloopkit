# ARCH009: Direction- and pyramid-bound operations learning gate

- **Created:** 2026-08-15
- **Status:** ACCEPTED
- **Governs:** Stages 20 Incident, 21 Postmortem, 22 Repair, PM/RPI templates,
  operation-agent completion hooks, and repair acceptance evidence
- **Consulted learning:** [Readiness is a gate](../learnings/cards/readiness-is-a-gate.md)
  (`PM001/LEARN001` and `PM001/LEARN003`)

## Decision

The incident→postmortem→repair cycle is the steady-state operating loop of a successful
long-running ELK product. It therefore consumes current direction and learning memory
directly rather than treating the Learnings Pyramid as optional month-end stewardship.

- **Stage 20** reads current `NORTHSTAR.md` before mitigation and follows only immediately
  relevant learning cues when that does not delay stabilization. It records consultation
  or explicit emergency deferral; it still cannot implement a permanent repair.
- **Stage 21** cannot complete until a named PM records current North Star/Learnings hashes,
  direction alignment, stable `RULE:<card-slug>` dispositions, an `UPDATED` or substantive
  `NO-CHANGE` pyramid decision, historical provenance coverage, retrieval impact/evidence,
  and RPI learning contracts.
- **Stage 22** routes and closes repairs using exact PM path/hash, selected RPI, Rule IDs,
  and executable gate. Route acceptance proves the contract was carried forward. Close
  acceptance requires that exact gate to PASS with durable evidence plus current Stage 08
  readiness; a code diff is never repair proof.

## Stable identities

- Living rule identity: `RULE:<learning-card-slug>`.
- Historical source identity: `PMxxx/LEARNxxx`.
- Repair identity: `RPIxxx` local to its source PM.

Cards remain living and Git-versioned; source PM learnings remain immutable historical
evidence. A rule can be `REINFORCED`, `CONTRADICTED`, or `MISSING`. A contradiction or
missing rule may explicitly choose `NO-CHANGE`, but only with a reason; new source
learnings cannot silently disappear from provenance.

## Completion enforcement

Agent-scoped hooks execute only the root-local versioned tool. The tool binds explicit
PM/repair paths, identifiers, session hash, HEAD, argument hash, local manifest hash,
executing assembly hash, and package version into ignored session gates. `Stop` revalidates
those identities before invoking:

- `validate postmortem-learning`
- `validate repair-learning`

The validators fail closed on missing/stale North Star or Learnings hashes, absent rule
dispositions, uncovered source provenance, missing historical coverage, required retrieval
without PASS evidence, substituted Rule IDs/gates, missing gate evidence, or stale readiness.

Executable repair gates are JSON argument vectors. Only `engloopkit repair-gate execute`
runs them, without a shell, under a bounded timeout. It emits a receipt with exit code,
stdout/stderr hashes, route hash, Rule IDs, gate/pyramid digests, HEAD, and a
content-sensitive worktree digest. Close uses a separate immutable record referencing the
original route and receipt. Caller-authored `PASS` text is never acceptance evidence.

This applies PM001's lesson: consultation and repair proof are outputs of deterministic
checks, not prose claims supplied by the agent.

## Consequences

- Stage 41 remains useful for wider backlog condensation and sampled retrieval refresh,
  but it no longer excuses Stage 21 from considering/updating the living pyramid now.
- Repairs carry the rule they implement into specification and executable acceptance.
- Direction changes or learning changes after PM analysis make old repair acceptance stale
  and require revalidation.
- Historical PM001–PM004 are not rewritten; the new contract applies to PMs explicitly
  submitted to the Stage 21 completion validator.