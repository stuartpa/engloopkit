# ARCH010: Author-side code-review response authority is split across Stages 11 and 12

- **Created:** 2026-09-03
- **Status:** ACCEPTED
- **Governs:** Stages 11–12, CodeReview Bundle interoperability, private response packets,
  provider adapters, reply/resolution approval, mutation attempts, reconciliation, and receipts
- **Consulted learnings:** [Readiness is a gate](../learnings/cards/readiness-is-a-gate.md)
  (`PM001/LEARN001` and `PM001/LEARN003`); [Verification follows artifact class](../learnings/cards/verification-follows-artifact-class.md)
  (`PM002/LEARN001`–`LEARN003`)

## Decision

CodeReview Bundle (CRB) and EngLoopKit have complementary, non-overlapping authority.
CRB reviews pull requests and may publish reviewer-owned feedback under its own approval
contract. ELK owns the author's engineering response after one current provider thread is
explicitly selected. CRB output is untrusted external evidence; it never grants ELK source
or provider authority and is not an ELK runtime dependency.

The author response is split into two agents:

1. **Stage 11 `codereview-address`** binds one exact provider/repository/PR/thread plus
   source/target revisions, iteration, acting principal, local HEAD, and checkout status.
   It classifies the feedback, may make the smallest accepted source/test edit, runs only
   repository-declared validation, and creates one ignored private response packet. It
  atomically records a trusted hash-bound completion receipt only after all Stage 11
  checks pass. A packet without that receipt has no Stage 12 authority. It
   cannot post/reply/resolve/vote/change PR state, commit, push, switch, reset, stash,
   merge, rebase, or clean unrelated work.
2. **Stage 12 `codereview-reply-resolve`** cannot edit source. It consumes one immutable
  clean Stage 11 refresh packet plus its trusted completion receipt only after an external
  commit/push workflow and after revalidating provider/principal/thread/revision identity.
  A dirty actionable packet is never provider-ready.
   `reply`, `resolve`, and `reply-and-resolve` are separate exact operations. Resolution
   requires the packet's validated fix revision to equal local HEAD and authoritative
  provider head. Before approval, the adapter's mandatory read-only `inspect` capability
    must prove exact provider/principal/thread/revision/head state, one match, a receipt,
    and no mutation. One fixed-option question includes a fenced, canonically escaped JSON
    message binding the exact packet, Stage 11 receipt, reply text, evidence, principal,
    inspection hash, thread, and operation. The trusted post-tool handler then binds the
    host-issued tool-use identity plus exact question and answer JSON into the one-time approval.

Provider mutation exists only behind an explicit tracked adapter manifest implementing
`engloop-review-response-v1`. The manifest binds one provider, exact no-shell argument
vector, tracked adapter artifact/hash, bounded timeout, and explicit capabilities. Missing
or mismatched inspection/mutation capability fails closed; ELK never guesses a CLI, converts Azure
DevOps thread state into GitHub semantics, calls CRB as a provider proxy, or switches to a
fallback adapter.

Before invoking an adapter, the versioned ELK tool records an immutable attempt ID,
packet/gate/adapter hashes, operation, and reconciliation marker. Success requires
provider read-back of exactly one matching target/result, matching reply content only for
reply-bearing operations, the resolved thread state only for resolution-bearing
operations, plus a provider receipt ID. Exit failure, timeout, malformed output, or unprovable
mutation becomes `outcome-unknown`; re-entry may only reconcile using the same attempt and
marker. It never blindly repeats the mutation. The one-action approval is consumed only
after confirmed or reconciled success.

Private packet, approval, attempt, and receipt state lives under ignored
`.engloop/out/code-review-response/`. Adapter manifests/artifacts live under tracked
`.engloop/provider-adapters/` because they are repository-authorized capabilities, not
model-selected tools. Real comments, identities, credentials, and customer/source payloads
remain outside tracked ELK product artifacts.

The repository owner account and local filesystem are the administrative trust boundary.
Hooks, hashes, create-new writes, locks, and receipts protect against model/tool mistakes,
stale state, corruption, and concurrent ELK processes; they do not claim to withstand a
malicious same-user process that can rewrite arbitrary repository files or replace the
running tool binary. Provider/review text and adapter output remain untrusted inputs.

## Why this boundary

One agent with unrestricted source edits and provider mutation would allow a compromised
or mistaken review comment to both change code and speak for the author. Splitting roles
makes human authority and provider effects observable. Applying `PM001/LEARN001` and
`PM001/LEARN003`, an agent's narrative or test result never authorizes publication; the
approval and provider read-back are deterministic gate outputs. Applying
`PM002/LEARN001`–`LEARN003`, the generic adapter/packet machinery receives direct
unit/property coverage, while independent provider state machines belong to each adapter's
own product tests rather than a fake ELK model of one provider.

## Consequences

- Authors get a direct Stage 11 → Stage 12 journey without collapsing review and response
  products or authority.
- Reply approval never implies resolution; resolution never follows automatically from a
  fix, test, commit, push, or reply.
- A dirty checkout is preserved. If safe isolation is unavailable, Stage 11 stops and asks
  for an explicitly approved worktree instead of rearranging user work.
- Stage 11 cannot edit ELK/provider control state. After an accepted source edit, an
  external workflow owns commit/push, and a clean Stage 11 refresh packet at the new head
  is mandatory before Stage 12.
- A provider timeout cannot duplicate a reply or resolution; outcome remains unknown until
  exact reconciliation proves one result.
- V1 operates on one explicitly selected thread. Batches, long-running monitoring, and
  automatic branch updates remain out of scope until separately specified and proven.
