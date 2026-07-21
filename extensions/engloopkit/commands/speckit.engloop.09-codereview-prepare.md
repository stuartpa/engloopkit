---
name: speckit.engloop.09-codereview-prepare
description: Minimize the current PR diff, validate it, and prepare evidence-backed reviewer-specific technical checks.
argument-hint: "--provider <github|azure-devops> --pr <url-or-id>"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, web]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.09-codereview-prepare --root .
      timeout: 30
handoffs:
  - label: Recompute readiness after review preparation
    agent: speckit.engloop.08-unittest
    prompt: Re-run direct evidence and the sole readiness gate after the code-review preparation changes above.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use `.engloop/out/codereview/current-pr.md` for the overwrite-only current-PR report.
It is transient evidence, not a persistent reviewer profile.

## Loop definition

- **Trigger:** an implementation is ready to be minimized and validated before requesting review.
- **Goal:** the smallest justified diff, objective validation evidence, and focused review guidance for the explicitly selected current PR.
- **Actions:** validate PR identity, remove unnecessary code, run applicable gates, identify reviewers, and research source-linked prior technical comments in changed files.
- **Verification:** HEAD matches the selected PR, no unexplained code remains, gates pass, and every reviewer pattern cites authoritative review evidence.
- **Memory:** product edits plus `.engloop/out/codereview/current-pr.md`; no durable personal profile.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.09-codereview-prepare --root .`

## Provider and identity rules

1. Require the explicit `--provider` and `--pr` input. Support only `github` and
   `azure-devops`; do not guess from branch names or choose the first open PR.
2. Use the authenticated provider CLI/API (`gh` for GitHub; Azure DevOps CLI/REST for
   Azure DevOps). Missing authentication or API capability fails closed.
3. Resolve PR base/head revisions and require the selected repository `HEAD` to match the
   authoritative PR head before editing.
4. Identify requested reviewers from PR metadata and repository ownership policy. Do not
   infer personal traits, motives, seniority, or availability.

## Preparation actions

- Inspect the complete base-to-head diff and delete code, tests, configuration, comments,
  generated files, compatibility paths, and abstractions that are not needed for the
  accepted behavior or evidence.
- Preserve required behavior and architecture; do not broaden scope merely to satisfy a
  speculative reviewer preference.
- Run formatting, build, tests, analyzers, readiness, and other repository gates applicable
  to the files changed.
- For each current reviewer, query prior review comments authored by that reviewer on the
  same changed files or directly related symbols. Record only recurring **technical review
  concerns** supported by links/IDs and quote no more than needed to identify the concern.
- Convert supported patterns into preemptive checks (for example error handling, tests,
  naming, ownership boundaries, or diagnostics). Mark sparse/conflicting evidence as
  inconclusive.

## Current-PR report

Overwrite `.engloop/out/codereview/current-pr.md` with:

- provider and authoritative PR/change ID;
- base revision, head revision, generated-at time, and dirty-state result;
- removed/unnecessary code and why removal was safe;
- validation commands and results;
- reviewers and source-linked recurring technical concerns;
- preemptive checks applied and unresolved questions.

Create no persistent personal profile or cross-PR personal dossier. A report whose PR ID
or head revision differs is stale and must not be reused.

## Done when

- [ ] Explicit provider/PR identity is authoritative and matches local HEAD
- [ ] Every remaining diff hunk is necessary and explainable
- [ ] Applicable deterministic gates pass
- [ ] Current reviewers and evidence-linked technical concerns are recorded
- [ ] No persistent personal reviewer profile was created
- [ ] Readiness is handed back to Stage 08 after any product edit
