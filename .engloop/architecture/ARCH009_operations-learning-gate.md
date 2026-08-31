# ARCH009: Direction- and pyramid-bound operations learning gate

- **Created:** 2026-08-15
- **Amended:** 2026-08-31
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

Agent-scoped hooks execute only the root-local versioned tool. When Stage 20 receives a
resolvable explicit incident path, and for every Stage 21/22 operation, the tool binds
explicit artifact paths, identifiers, session hash, HEAD, argument hash, local manifest
hash, executing assembly hash, and package version into ignored session gates. `Stop`
revalidates those identities before invoking:

- `validate incident-context`
- `validate postmortem-learning`
- `validate repair-learning`

The Stage 20 lifecycle hook is a context-capture aid, not the incident-completion
authority. Any incident-hook setup, parsing, state, storage, identity, or validation
failure leaves chat available with `continue: true` and a structured
`learning-context-deferred` diagnostic. The response names the phase, command, diagnostic,
expected source, ELK version, and remediation. It does not create, accept, substitute, or
delete a gate after failure. A deferred hook result cannot support a stabilization claim;
the incident artifact must still pass the authoritative `incident-context` validator.
This keeps mitigation available while applying `PM001/LEARN001` and `PM001/LEARN003` at
the actual completion boundary rather than treating lifecycle metadata collection as the
readiness verdict.

Stage 21 separates correctable prompt context from postmortem authorization. When an
initial prompt omits `--incidents` or `--postmortem`, supplies malformed incident IDs, or
uses an invalid governed PM path, `UserPromptSubmit` returns `continue: true` with a
structured `postmortem-context-required` diagnostic. It creates no scope gate and accepts
no completion. A Stage 21 `PreToolUse` hook denies every tool until a valid session/HEAD/
argument/tool-identity-bound gate exists, so the agent can only report remediation and ask
the operator to resubmit exact values. A gate-less Stop may end that recovery response but
never emits `POSTMORTEM_LEARNING_OK`. Once valid context is rebound, the existing Stop
validator remains fail closed on every direction, pyramid, provenance, retrieval, SEK,
RPI, and artifact requirement. Unsupported existing/create-new PM identities and corrupt
or tampered gates are not downgraded to context recovery.

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

- Missing or malformed Stage 20 hook context can reduce learning capture but cannot make
  the recovery agent unavailable. The diagnostic is explicit; there is no silent fallback.
- Correctable Stage 21 invocation context cannot dead-end chat, but it also cannot
  authorize tools or completion. Scope and completion gates remain blocking because they
  accept durable retrospective evidence.
- Stage 22 scope and completion gates remain blocking because they accept durable repair
  evidence rather than performing urgent stabilization.
- Stage 41 remains useful for wider backlog condensation and sampled retrieval refresh,
  but it no longer excuses Stage 21 from considering/updating the living pyramid now.
- Repairs carry the rule they implement into specification and executable acceptance.
- Direction changes or learning changes after PM analysis make old repair acceptance stale
  and require revalidation.
- Historical PM001–PM004 are not rewritten; the new contract applies to PMs explicitly
  submitted to the Stage 21 completion validator.