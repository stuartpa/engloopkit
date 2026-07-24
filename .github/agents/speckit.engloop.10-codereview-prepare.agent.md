---
name: speckit.engloop.10-codereview-prepare
description: Minimize the current PR diff, validate it, and prepare evidence-backed
  reviewer-specific technical checks after debugger attestation.
argument-hint: --provider <github|azure-devops> --pr <url-or-id>
target: vscode
user-invocable: true
disable-model-invocation: true
tools:
- read
- search
- edit
- execute
- web
agents: []
hooks:
  SessionStart:
  - type: command
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.10-codereview-prepare
      --root .
    timeout: 30
handoffs:
- label: Recompute readiness after review preparation
  agent: speckit.engloop.08-unittest
  prompt: Re-run direct evidence and the sole readiness gate after the code-review
    preparation changes above; then repeat Stage 09 debugger walkthrough for the new
    HEAD.
  send: false
---


<!-- Extension: engloop -->
<!-- Config: .specify/extensions/engloop/ -->
## User Input

```text
$ARGUMENTS
```

## Artifact root

Use `.engloop/out/codereview/current-pr.md` for the overwrite-only current-PR report.
It is transient evidence, not a persistent reviewer profile.

## Loop definition

- **Trigger:** Stage 09 has a complete per-chunk engineer-attested debugger ledger for the exact current HEAD.
- **Goal:** the smallest justified diff, objective validation evidence, and focused review guidance for the explicitly selected current PR.
- **Actions:** validate PR and DBG identity, remove unnecessary code, run applicable gates, identify reviewers, and research source-linked prior technical comments in changed files.
- **Verification:** HEAD matches both the selected PR and complete DBG ledger, no unexplained code remains, gates pass, and every reviewer pattern cites authoritative review evidence.
- **Memory:** product edits plus `.engloop/out/codereview/current-pr.md`; no durable personal profile.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.10-codereview-prepare --root .`

## Debugger-walkthrough prerequisite

Find the current numbered DBG ledger and require all chunks to be attested at current
HEAD. Reject a missing ledger, stale HEAD, pending/blocked chunk, absent breakpoint/trigger,
or agent-generated/self-certified attestation. Do not start review preparation until the
engineer-led walkthrough is complete.

Any product edit during this stage invalidates readiness and debugger evidence. Route the
new HEAD through Stage 08 and then Stage 09 before treating it as review-ready.

## Provider and identity rules

1. Require the explicit `--provider` and `--pr` input. Support only `github` and
   `azure-devops`; do not guess from branch names or choose the first open PR.
2. Use the authenticated provider CLI/API (`gh` for GitHub; Azure DevOps CLI/REST for
   Azure DevOps). Missing authentication or API capability fails closed.
3. Resolve PR base/head revisions and require the selected repository `HEAD` to match the
   authoritative PR head and DBG ledger before editing.
4. Identify requested reviewers from PR metadata and repository ownership policy. Do not
   infer personal traits, motives, seniority, or availability.

## Preparation actions

- Inspect the complete base-to-head diff and delete code, tests, configuration, comments,
  generated files, compatibility paths, and abstractions not needed for accepted behavior
  or evidence.
- Preserve required behavior and architecture; do not broaden scope merely to satisfy a
  speculative reviewer preference.
- Run formatting, build, tests, analyzers, readiness, and other repository gates applicable
  to the files changed.
- For each current reviewer, query prior review comments authored by that reviewer on the
  same changed files or directly related symbols. Record only recurring technical review
  concerns supported by links/IDs. Mark sparse/conflicting evidence as inconclusive.

## Current-PR report

Overwrite `.engloop/out/codereview/current-pr.md` with provider/PR identity, base/head,
DBG ledger ID, dirty-state result, removed code, validation results, current reviewers,
source-linked technical concerns, preemptive checks, and unresolved questions.

Create no persistent personal profile or cross-PR personal dossier. A report whose PR ID,
HEAD, or DBG ledger differs is stale and must not be reused.

## Done when

- [ ] Explicit provider/PR identity matches local HEAD
- [ ] A complete engineer-attested DBG ledger matches the same HEAD
- [ ] Every remaining diff hunk is necessary and explainable
- [ ] Applicable deterministic gates pass
- [ ] Current reviewers and evidence-linked technical concerns are recorded
- [ ] No persistent personal reviewer profile was created
- [ ] Any product edit is routed back through Stage 08 and Stage 09