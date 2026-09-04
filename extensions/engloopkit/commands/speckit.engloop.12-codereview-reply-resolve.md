---
name: speckit.engloop.12-codereview-reply-resolve
description: Publish one exact approved reply and/or resolve one exact current review thread through an explicit tracked provider adapter, with revision/principal revalidation and durable reconciliation evidence.
argument-hint: "--packet <.engloop/out/code-review-response/address/*.json> --operation <reply|resolve|reply-and-resolve> --adapter <.engloop/provider-adapters/*.json>"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, execute, vscode_askQuestions]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.12-codereview-reply-resolve --root .
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

Consume one exact Stage 11 packet under `.engloop/out/code-review-response/address/` and
its trusted hash-bound Stage 11 completion receipt.
Approval, pre-attempt, reconciliation, and provider receipts remain ignored under
`.engloop/out/code-review-response/`. The adapter manifest is a tracked explicit product
capability under `.engloop/provider-adapters/`.

## Loop definition

- **Trigger:** Stage 11 produced a clean refreshed response packet after the authoritative PR head contains the validated fix revision, or a clean factual no-code packet is ready.
- **Goal:** apply exactly one separately approved `reply`, `resolve`, or `reply-and-resolve` operation to one current thread, or stop in a factual rejected/outcome-unknown state without duplicate mutation.
- **Actions:** revalidate packet/provider/principal/revisions/thread/adapter, display exact content and operation, collect one confirmation, invoke one guarded adapter attempt, reconcile ambiguous outcomes, and record the result.
- **Verification:** the adapter reports exactly one matching reply and required resolution state with a provider receipt, or the operation remains rejected/outcome-unknown and cannot retry blindly.
- **Memory:** ignored packet, one-time approval, pre-attempt/reconciliation state, and provider receipt.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.12-codereview-reply-resolve --root .`

Require `CODE_REVIEW_RESPONSE_SCOPE_ACTIVE mode=reply-resolve`. This agent cannot edit
source, commit, push, vote, merge, change reviewers, or invoke provider tools directly.
Stage 12 cannot edit source under any operation.
PreToolUse permits local workspace reads only (no web/network read tool), one exact
fixed-option `vscode_askQuestions` call, and—only
after approval—one exact versioned `code-review-response apply` command. All provider
mutation is isolated behind the selected tracked adapter contract.
An arbitrary question with the same tool is denied, and an existing approval cannot be
replaced by a second PostTool event.

## Revalidation and approval

1. Revalidate the Stage 11 packet digest, authenticated acting principal, provider,
   repository, PR, exact thread, source/target revisions, iteration, classification,
   exact reply text, allowed operations, and current thread state through the explicit
   adapter capability. Never infer an adapter/provider mapping.
   Require the engine-created `schemas/code-review-address-receipt.schema.json` receipt;
   a merely schema-correct or subsequently altered packet is ineligible. Require an exact
   clean repository root and a clean refreshed packet (`changedFiles: []` and the
   canonical clean status digest); a dirty pre-commit Stage 11 packet is ineligible.
   Before approval, the engine invokes the adapter's mandatory `inspect` capability. It
   must return `schemas/code-review-provider-inspection.schema.json` with exact identities,
   principal, revisions, thread status, provider head, operation, one match, a receipt,
   and `mutationPerformed: false`. Rejection, malformed output, drift, ambiguity, timeout,
   or any claimed mutation fails closed before an approval exists.
2. For `resolve` or `reply-and-resolve`, require the packet's `requiredFixRevision` to
   equal both current local HEAD and authoritative `providerHead`. Reply-only does not
   imply resolution.
3. Display the exact reply text, operation, thread identity, evidence summary, packet
   SHA-256, and acting principal.
4. Ask exactly one question with `vscode_askQuestions`: header
   `Approve review response`; question
   `Apply <operation> to thread <thread> with packet <12-char-packet-hash>?`;
    `message` equal to the engine's fenced, canonically escaped JSON projection of the exact provider,
    repository, PR/thread, revisions, iteration, acting principal, thread status,
    classification, provider head, operation, reply text, evidence, unresolved risks,
    packet SHA-256, Stage 11 receipt SHA-256, and provider-inspection SHA-256;
   `multiSelect: false`; `allowFreeformInput: false`; exact ordered options `Confirm`
   and `Cancel`.
5. `PostToolUse` binds the actual fixed-option answer and host `tool_use_id` into a
   one-time approval. On Cancel, no provider operation is authorized.

## Provider adapter and ambiguous outcomes

The tracked adapter manifest uses `templates/CODE-REVIEW-PROVIDER-ADAPTER-template.json`
and protocol `engloop-review-response-v1`. It declares one provider, exact no-shell
argument-vector command, mandatory `inspect`, and explicit mutation capabilities.
The manifest and command artifact must byte-match their committed `HEAD` objects even
when index flags hide working-tree changes.
The artifact is self-contained and execution-location independent because ELK executes a
hash-verified private copy. Missing/mismatched capability fails closed; ELK never tries
another CLI, provider, identity, or data source.

After `CODE_REVIEW_RESPONSE_APPROVED`, invoke exactly:

`dotnet tool run engloopkit -- code-review-response apply --gate <hook-gate> --approval <hook-approval>`

The engine writes pre-attempt identity before adapter execution. The adapter must re-read
authoritative provider state and return canonical result JSON. Success requires exactly
one matching target/result, an observed matching reply only when the operation includes
`reply`, observed resolved state only when it includes `resolve`, and a provider receipt
ID according to `schemas/code-review-provider-result.schema.json`. A crash, timeout,
malformed result, or unprovable write becomes
`CODE_REVIEW_RESPONSE_OUTCOME_UNKNOWN`; re-entry invokes reconciliation with the same
attempt ID/marker and never blindly repeats mutation. Multiple matches are integrity
failure. A successful one-action approval is deleted after use.
Every existing attempt is revalidated against the exact packet, operation, adapter hash,
attempt ID, marker, state, and non-empty success receipt before idempotent completion.

`Stop` emits `CODE_REVIEW_REPLY_RESOLVE_OK` only after a durable success receipt exists.
No packet, approval, adapter exit code, or locally authored PASS text is sufficient.

## Done when

- [ ] Exact current packet, principal, PR/thread, revisions, iteration, operation, and adapter are validated
- [ ] Trusted Stage 11 receipt proves the exact packet passed Stage 11 completion
- [ ] One hash-bound adapter inspection proved the current exact target and no provider mutation before approval
- [ ] Resolution is rejected unless the validated fix revision is on authoritative provider head
- [ ] Exact reply/operation/principal/evidence and packet/Stage 11 receipt/inspection hashes were displayed and separately confirmed through the fixed question
- [ ] One pre-attempt identity exists before provider mutation
- [ ] Exactly one matching provider result and required status were read back
- [ ] Durable success or outcome-unknown receipt exists and blind retry is impossible
- [ ] No source edit, commit, push, vote, merge, reviewer change, or unapproved provider action occurred
