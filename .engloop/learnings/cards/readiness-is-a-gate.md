# Card: Readiness is a gate

- **Recall cue:** Is an evidence gate being mistaken for permission to select another agent?
- **Apply when:** Defining agent navigation, Incident → Postmortem handoffs, completion
  claims, release gates, or readiness status.
- **Status:** Living
- **Last condensed:** 2026-09-06

## Compressed principle

Progress pressure makes ungated completion claims optimistic. “Ready” must be the
computed output of a whole-product check whose inventory covers every product unit;
the agent may report that verdict but may not supply it as an opinion.

**The user owns agent navigation, always.** Choosing, leaving, or entering an agent is
not an evidence-backed completion claim. Under the owner's explicit directive below,
ELK MUST NOT deny a user-selected next agent, especially Incident → Postmortem.
Missing readiness, incident IDs, stabilization evidence, or a failed hook must travel
as visible context; they must not trap the user in the current agent. The destination
can discuss, collect evidence, and draft analysis without pretending the prior stage passed.

## Decision checks

1. Is the claim produced by a deterministic check rather than free text?
2. Does the attached inventory enumerate the whole product, including zero-evidence
   units, rather than only tests that happen to exist?
3. Can a failed or missing row be omitted, defaulted, or narrated away? If yes, the
   gate is not closed.
4. Is PASS required only for the particular success claim or protected operation,
  never for choosing the next agent or beginning its conversation?
5. Can the user enter Postmortem during an active incident, with unresolved stabilization
  explicitly carried forward and no false closure? Failed Stop hooks must not veto this.
6. Do direct selection, handoff, delegation, and resumed-chat paths all preserve this
  freedom? Limit only the specific unsafe mutation, not all tools or the conversation.

## Source learnings

- [PM001/LEARN001](../../postmortems/PM001_no-readiness-gate.md#learnings) — narrated
  transitions are crossed prematurely.
- [PM001/LEARN002](../../postmortems/PM001_no-readiness-gate.md#learnings) — coverage of
  existing tests is not whole-product readiness.
- [PM001/LEARN003](../../postmortems/PM001_no-readiness-gate.md#learnings) — readiness is
  check output, never agent input.

## Tensions

PM001's historical “stage transition” wording was applied too broadly. The owner's
[2026-09-06 directive](../owner-directives-release-and-navigation.md) supersedes that
interpretation for navigation, not the requirement for truthful readiness evidence.
This is an explicit owner decision, not a retroactive PM001 conclusion. Runtime hooks
may still violate it; this card does not claim that those implementations are repaired.