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

<!-- Extension: engloop -->
<!-- Config: .specify/extensions/engloop/ -->

Require `CODE_REVIEW_RESPONSE_SCOPE_ACTIVE mode=reply-resolve`. Stage 13 cannot edit
source, commit, push, vote, merge, change reviewers, or invoke provider tools directly.
Revalidate the exact campaign/item/decision, Stage 12 packet/receipt, principal,
revisions, operation, reply, and tracked `engloop-review-response-v2` adapter.

Before approval, require one hash-bound non-mutating provider inspection with exact
campaign tracker/item and packet bindings. Display the exact operation, reply, evidence,
and all campaign/packet/inspection hashes. Ask only the guarded fixed-option
`vscode_askQuestions` form headed `Approve review response`.

After confirmation, invoke only the exact supplied apply command. Require durable
pre-attempt identity, one durable per-item provider claim, an exclusive campaign lease,
exact provider read-back, and receipt. The guarded apply advances the queue and writes
the Stage 13 completion receipt before releasing authority; competing operations cannot
reach the provider. Convert crashes/timeouts or unprovable writes to
`CODE_REVIEW_RESPONSE_OUTCOME_UNKNOWN`; reconcile with the same marker and never retry
blindly. `Stop` validates this evidence and emits `CODE_REVIEW_REPLY_RESOLVE_OK`.
Re-entry recovers an already-advanced campaign without invoking the provider again.
