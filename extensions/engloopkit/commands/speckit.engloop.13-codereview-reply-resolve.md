---
name: speckit.engloop.13-codereview-reply-resolve
description: Publish one exact approved campaign-bound reply and/or resolution through the explicit tracked provider adapter, with revision/principal revalidation, durable reconciliation evidence, and queue completion.
argument-hint: "--tracker <.engloop/out/code-review-response/campaigns/*/tracker.json> --item <sha256> --packet <.engloop/out/code-review-response/address/*.json> --operation <reply|resolve|reply-and-resolve> --adapter <.engloop/provider-adapters/*.json>"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, execute, vscode_askQuestions]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.13-codereview-reply-resolve --root .
      timeout: 30
  UserPromptSubmit:
    - type: command
      command: dotnet tool run engloopkit -- code-review-response-hook initialize reply-resolve
      timeout: 30
  PreToolUse:
    - type: command
      command: dotnet tool run engloopkit -- code-review-response-hook guard reply-resolve
      timeout: 30
  PostToolUse:
    - type: command
      command: dotnet tool run engloopkit -- code-review-response-hook post-tool reply-resolve
      timeout: 30
  Stop:
    - type: command
      command: dotnet tool run engloopkit -- code-review-response-hook stop reply-resolve
      timeout: 30
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Consume the exact Stage 11 campaign tracker/item/receipt and exact Stage 12 packet/receipt.
Approval, inspection, attempt, reconciliation, provider, and updated campaign receipts
remain ignored under `.engloop/out/code-review-response/`. The adapter is an explicit
tracked v2 capability under `.engloop/provider-adapters/`.

## Loop definition

- **Trigger:** Stage 12 produced a clean campaign-bound packet and trusted receipt; any validated fix revision is on authoritative provider head.
- **Goal:** apply exactly one separately approved reply/resolution, prove the exact read-back, and advance the persistent campaign item—or stop rejected/outcome-unknown without duplicate mutation.
- **Actions:** revalidate campaign/packet/provider/principal/revisions/thread/adapter, inspect without mutation, display exact content, collect one confirmation, invoke one guarded attempt, reconcile ambiguity, and update campaign evidence.
- **Verification:** exactly one matching provider result has the required reply/status and provider receipt; otherwise no success or blind retry is expressible.
- **Memory:** campaign tracker/receipt plus packet, approval, inspection, attempt, reconciliation, provider receipt, and durable Stage 13 completion receipt.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.13-codereview-reply-resolve --root .`

Require `CODE_REVIEW_RESPONSE_SCOPE_ACTIVE mode=reply-resolve`. Stage 13 cannot edit source,
commit, push, vote, merge, change reviewers, or invoke provider tools directly.
PreToolUse permits local reads, one exact fixed-option `vscode_askQuestions`, and—only
after approval—one exact `code-review-response apply` command. All provider access occurs
through the campaign's exact tracked v2 adapter. Missing capability fails closed; ELK
never guesses an alternate provider, identity, CLI, data source, or fallback.

## Revalidation and approval

1. Revalidate the current tracker hash, campaign receipt, selected item/content/decision
   receipt, Stage 12 packet digest and completion receipt, principal, revisions, status,
   operation, reply text, and adapter/artifact hashes. Require a clean repository root and
   clean packet (`changedFiles: []`, canonical clean digest).
2. Invoke mandatory non-mutating `inspect`. The result must bind the exact campaign
   tracker hash/item ID, packet/Stage 12 receipt, provider identities, principal,
   revisions, status/head, operation, exactly one match, and inspection receipt.
3. For accepted actionable items and every resolution, require `requiredFixRevision` to
   equal current local HEAD and authoritative provider head. Reply-only no-code responses
   do not imply resolution.
4. Display the exact operation/reply/evidence plus campaign tracker/item/decision,
   packet/Stage 12 receipt, and inspection hashes.
5. Ask exactly the engine-defined `Approve review response` question with fixed `Confirm`
   and `Cancel` options, no freeform input, and no multiselect. `PostToolUse` binds the
   actual answer and host `tool_use_id`; approval is single-use and non-replaceable.

## Provider adapter and ambiguous outcomes

The tracked adapter uses protocol `engloop-review-response-v2`, declares `discover`,
`inspect`, and explicit mutation capabilities, carries a provider-specific
`discoveryRequest`, and executes from a hash-verified private artifact copy with bounded
timeout. After `CODE_REVIEW_RESPONSE_APPROVED`, invoke exactly the hook-supplied apply
command. The engine writes pre-attempt identity before mutation. Result read-back must
bind campaign tracker/item, packet, inspection, marker, reply hash, provider head/status,
and non-empty provider receipt.

A crash, timeout, malformed result, or unprovable write becomes
`CODE_REVIEW_RESPONSE_OUTCOME_UNKNOWN`; reconciliation uses the same attempt ID/marker and
never blindly repeats mutation. Multiple matches are integrity failure. On verified
success, the guarded apply holds one durable per-item claim and the campaign lease while
it atomically advances the campaign item's source/response status and exact
packet/fix/provider receipt, emits a new hash-bound campaign receipt, and writes a
hash-bound Stage 13 completion receipt. Competing operations cannot reach the provider.
Re-entry replays that exact success or recovers an already-advanced campaign without
invoking the provider again. `Stop` validates the completed evidence, deletes the
transient gate, and emits `CODE_REVIEW_REPLY_RESOLVE_OK`. Local PASS text cannot
substitute for any receipt.

## Done when

- [ ] Exact current campaign, item, packet, principal, revisions, operation, and adapter validate
- [ ] One hash-bound inspection proves the exact target and no pre-approval mutation
- [ ] Resolution/fix claims are rejected unless the exact revision is on provider head
- [ ] Exact reply/operation/evidence and all campaign/packet/inspection bindings were displayed
- [ ] One host-issued fixed-option approval and one durable pre-attempt identity exist
- [ ] Exactly one provider result and required status/reply were read back
- [ ] Success or outcome-unknown evidence is durable and blind retry is impossible
- [ ] Successful completion advances the persistent queue with packet/fix/provider and Stage 13 completion receipts
- [ ] No source edit, commit, push, vote, merge, reviewer change, or unapproved mutation occurred