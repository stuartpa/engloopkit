# ARCH011: Complete PR review campaigns span Stages 11–13

- **Created:** 2026-09-04
- **Status:** ACCEPTED
- **Supersedes:** [ARCH010](ARCH010_code-review-response-authority.md)
- **Governs:** PR-wide review discovery, persistent private campaign queues, human
  triage, author remediation, provider replies/resolutions, adapters, receipts, and
  ambiguous-outcome reconciliation
- **Consulted learnings:** [Readiness is a gate](../learnings/cards/readiness-is-a-gate.md)
  (`PM001/LEARN001` and `PM001/LEARN003`)

## Decision

EngLoopKit owns an author-response **campaign**, not an inferred batch action. One
explicit provider/repository/PR identity flows through three independent authority
stages:

1. **Stage 11 `codereview-campaign`** uses one explicit tracked
   `engloop-review-response-v2` adapter to retrieve the complete current review surface.
   Discovery must report complete pagination, an empty continuation token, exact PR and
   revision identity, every thread's complete current comments, one stable discovery
   receipt, and `mutationPerformed: false`. Missing pages, ambiguous identity, malformed
   comments, or adapter failure stops the campaign. ELK never substitutes cached state,
   another CLI, another provider, or partial data.
2. Stage 11 stores each discovery as an immutable private snapshot and atomically merges
   it into one current tracker keyed by provider/repository/PR. Items have stable IDs from
   provider/repository/PR/thread/root-comment identity. Unchanged human decisions persist;
   changed content marks a decision stale; absent items remain `not-returned`; new items
   remain undecided. A human must explicitly select one active item, then decide its
  classification and priority through fixed host questions. Only that currently selected
  item may bind a downstream stage; a previously triaged but unselected item has no source
  or provider authority. Model proposals are never authority.

  Snapshot/tracker/gate installation and every selection or triage tracker/gate update
  use a validated write-ahead campaign transaction under the campaign lock. Re-entry
  accepts only the exact previous-or-target hashes, finishes the transaction, validates
  the resulting gate/tracker pair, and then deletes the transaction. A crash cannot make
  a discovered inventory or human decision inaccessible, and recovery never rediscovers
  or invents an answer. Successful Stage 11 Stop also writes a session-indexed completion
  receipt binding the tracker/campaign receipt, selection, and queue counts before gate
  deletion. It embeds the exact completed tracker bytes, so post-cleanup re-entry reports
  the same success without rediscovery even after a later refresh.
3. **Stage 12 `codereview-address`** consumes only one exact current tracker item whose
   human decision and campaign receipt validate. It may edit source only for
   `accepted-actionable`, runs repository-declared validation, and emits one private
   campaign-bound packet plus a trusted completion receipt. It cannot refetch provider
   state, post, resolve, vote, commit, push, or modify adapter/control state. An external
   workflow owns branch mutation; a code-changing response becomes provider-eligible only
   through a clean packet bound to the exact fix on local/provider head.
4. **Stage 13 `codereview-reply-resolve`** has no source authority. It revalidates the
   campaign/item/decision and Stage 12 packet/receipt, performs a non-mutating inspection,
   displays exact campaign and response evidence, and obtains one non-replaceable fixed
   approval. Provider mutation occurs only through the same tracked v2 adapter. Before
   mutation ELK records the attempt identity; exact read-back must include the campaign
  tracker hash/item ID and provider receipt. Unknown outcomes reconcile with the same
  marker and never blindly retry. Before provider invocation, guarded apply creates one
  durable claim for the exact campaign item and holds the campaign lease through
  authoritative read-back and queue advancement. Thus two separately approved operations
  cannot both reach the provider. Verified completion atomically advances that campaign
  item and emits a new campaign receipt so the queue remains durable and actionable.

All Stage 11 tracker/receipt operations and all Stage 12/13 tracker bindings or transitions
serialize through one campaign-scoped exclusive lock. Guarded Stage 13 apply re-reads the
selected item, decision, tracker hash, and trusted receipt while holding that lock, retains
the lease across provider read-back, and writes the new receipt and tracker before release.
Apply also shares the Stage 13 session-operation lock with initialize, approval binding,
and Stop; lock order is session, campaign, then attempt. Stop therefore cannot delete a
gate while apply is completing durable success.
It then writes an immutable session completion receipt
binding the original gate/provider attempt, prior/new tracker hashes, campaign/item/packet,
operation, and provider receipt before deleting the transient gate. Re-entry can therefore
report or finish an already-advanced transition without invoking the provider again.
Contention fails closed without mutation; a stale second completion cannot overwrite an
accepted transition.

CRB remains the reviewer-side product and external evidence source. Provider text and
adapter output remain untrusted inputs. Application/provider-specific semantics stay in
explicit adapters and opaque metadata; core ELK contains no provider-name, schema,
comment-shape, or workload heuristic.

The administrative trust boundary remains the repository owner account and local
filesystem. Hashes, create-new writes, atomic replacements, locks, hooks, and receipts
protect against model/tool mistakes, stale state, corruption, and concurrent ELK
processes; they do not claim resistance to a malicious same-user process that can replace
the running binary or arbitrary files.

## Why

ARCH010 correctly split source authority from provider authority, but required the user
to preselect one provider thread before ELK could establish completeness, durable queue
state, or refresh history. That made "all feedback was handled" narratable without a
mechanical inventory.

Applying `PM001/LEARN001` and `PM001/LEARN003`, campaign completeness, human decisions,
publication approval, provider success, and queue completion are outputs of deterministic
gates and receipts—not model narration. Complete discovery does **not** authorize batch
mutation; each item still crosses Stage 12 and Stage 13 independently.

## Consequences

- Authors can retrieve every current review conversation once, refresh it repeatedly,
  prioritize it, and preserve completed/disappeared history without losing provenance.
- Partial pagination and stale decisions are explicit failures, never silent omissions.
- Human selection/classification/priority remain separate from model analysis.
- One item can be remediated and published without granting source/provider authority for
  any other item.
- Reply approval never implies resolution, and a fix/test/commit/push never implies
  provider publication.
- Provider adapters must implement v2 complete discovery plus inspection and whichever
  explicit mutation capabilities the repository chooses. V1 adapters fail closed.
- Successful Stage 13 completion updates the queue only after durable provider success;
  ambiguous outcomes retain reconciliation state and do not fabricate campaign closure.
- Concurrent refresh, triage, or completion attempts cannot lose an accepted queue update;
  one campaign transition wins and every stale writer must re-enter through fresh discovery
  and human selection.