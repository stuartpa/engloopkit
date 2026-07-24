---
name: speckit.engloop.10-codereview-prepare
description: Minimize the readiness-approved current PR diff, validate it, and prepare
  evidence-backed reviewer-specific technical checks.
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
    preparation changes above; a new Stage 09 debugger walkthrough is recommended
    but not required.
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

- **Trigger:** Stage 08 has emitted a current readiness PASS for the exact current HEAD.
- **Goal:** the smallest justified diff, objective validation evidence, and focused review guidance for the explicitly selected current PR.
- **Actions:** validate PR and readiness identity, optionally consume current debugger findings, remove unnecessary code, run applicable gates, identify reviewers, and research source-linked prior technical comments in changed files.
- **Verification:** HEAD matches the selected PR and current Stage 08 readiness record, no unexplained code remains, gates pass, and every reviewer pattern cites authoritative review evidence.
- **Memory:** product edits plus `.engloop/out/codereview/current-pr.md`; no durable personal profile.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.10-codereview-prepare --root .`

## Debugger-walkthrough evidence

If a numbered DBG ledger exists for the current HEAD, record it and use its observed
findings as advisory review evidence. Mark stale or incomplete evidence accurately.
Never convert a missing/incomplete walkthrough into a completion claim.

Do not reject Stage 10 because a DBG ledger is missing, stale, pending, blocked, or
incomplete. Stage 09 is recommended but non-blocking.

Any product edit during this stage invalidates readiness. Route the new HEAD through Stage
08 before treating it as review-ready; repeating Stage 09 is recommended but optional.

## Provider and identity rules

1. Require the explicit `--provider` and `--pr` input. Support only `github` and
   `azure-devops`; do not guess from branch names or choose the first open PR.
2. Use the authenticated provider CLI/API (`gh` for GitHub; Azure DevOps CLI/REST for
   Azure DevOps). Missing authentication or API capability fails closed.
3. Resolve PR base/head revisions and require the selected repository `HEAD` to match the
  authoritative PR head and current readiness record before editing.
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
optional DBG ledger ID/status, dirty-state result, removed code, validation results, current reviewers,
source-linked technical concerns, preemptive checks, and unresolved questions.

Create no persistent personal profile or cross-PR personal dossier. A report whose PR ID,
HEAD, or DBG ledger differs is stale and must not be reused.

## Done when

- [ ] Explicit provider/PR identity matches local HEAD
- [ ] Current debugger findings are recorded when available; missing/incomplete DBG evidence did not block preparation
- [ ] Every remaining diff hunk is necessary and explainable
- [ ] Applicable deterministic gates pass
- [ ] Current reviewers and evidence-linked technical concerns are recorded
- [ ] No persistent personal reviewer profile was created
- [ ] Any product edit is routed back through Stage 08; Stage 09 remains recommended and optional