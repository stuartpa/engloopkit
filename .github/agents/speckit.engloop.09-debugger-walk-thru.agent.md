---
name: speckit.engloop.09-debugger-walk-thru
description: Recommend, prepare, and track an engineer-led line-by-line debugger walkthrough
  without blocking review preparation.
argument-hint: --base <revision> [--debugger <explicit-choice>] [--trigger <test-command>]
target: vscode
user-invocable: true
disable-model-invocation: true
tools:
- read
- search
- edit
- execute
agents: []
hooks:
  SessionStart:
  - type: command
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.09-debugger-walk-thru
      --root .
    timeout: 30
---


<!-- Extension: engloop -->
<!-- Config: .specify/extensions/engloop/ -->
## User Input

```text
$ARGUMENTS
```

## Artifact root

Create one numbered ledger under `.engloop/debugger-walkthroughs/` named
`DBG<NNN>_<brief-scope>.md`. Reserve the next global `DBG` number in
`.engloop/numbering-registry.md` before creating the ledger.

## Loop definition

- **Trigger:** Stage 02 has proven the test runway; the engineer may invoke this stage at any later point to inspect the current implementation early.
- **Goal:** the engineer personally steps line by line through every executable changed-code chunk under a real debugger and explicitly attests each completed chunk.
- **Actions:** bind the diff to base/HEAD, inventory chunks, select/configure a debugger, place a breakpoint, run a triggering test, pause for engineer stepping, and record attestation.
- **Verification:** every executable changed-code chunk has a reached breakpoint, deterministic trigger, observed path, exact engineer attestation, and the ledger HEAD equals current HEAD.
- **Memory:** one `.engloop/debugger-walkthroughs/DBG<NNN>_<scope>.md` ledger.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.09-debugger-walk-thru --root .`

## Non-delegable engineering responsibility

The agent prepares the debugger and evidence; the engineer performs the walkthrough.
Never infer, simulate, auto-check, or self-certify that a chunk was stepped through. For
each chunk, pause and require the engineer to explicitly attest that they personally
stepped through it line by line. Silence, a passing test, a reached breakpoint, trace
output, or agent observation is not attestation.

Any product-code edit changes HEAD and makes prior completion evidence stale. Preserve
earlier ledgers as historical observations, but never carry their attestations forward as
current. The engineer may immediately invoke Stage 09 again at the new HEAD.

Stage 09 is recommended engineering practice, not a transition gate. Missing, incomplete,
blocked, or stale DBG evidence must never prevent Stage 10 from starting. Stage 10 relies
on the current Stage 08 readiness PASS; it may record and use debugger findings when they
exist without claiming that an incomplete walkthrough was completed.

## Scope inventory

1. Require an explicit base revision. Record base and current HEAD before setup.
2. Inventory every changed executable file and partition it into reviewable chunks by
   symbol/behavior boundary. Record exact file and line range, changed symbols, and why
   the chunk boundary is complete.
3. Every changed executable line must belong to exactly one chunk. Generated or
   non-executable changes must be identified separately and receive an explicit
   line-by-line engineer-read attestation; do not falsely claim debugger execution.
4. Do not omit a chunk because tests pass, the code was AI-generated, or the change seems
   mechanical.

## Debugger and trigger setup

1. Search repository instructions, existing launch/attach configurations, tasks, tests,
   and repo-local debugger skills before changing setup.
2. Do not infer a debugger from language, product name, operating system, or convention.
   Use the explicit `--debugger` choice or ask the engineer when multiple authoritative
   options exist.
3. For the current chunk, identify the narrowest deterministic test/command that executes
   it. Place a breakpoint at the first changed executable line or nearest authoritative
   entry boundary, configure launch/attach, and run/prepare that trigger until the
   breakpoint is reached.
4. The agent may create local debugger configuration only when it is explicitly local or
   repository-approved. Do not commit machine-specific paths, process IDs, credentials,
   or local launch state.
5. After one bounded, documented setup/trigger attempt fails or requires repeated ad hoc
   discovery, stop and offer to create a repo-local debugger setup `SKILL.md`. Create it
   only after explicit user approval. The skill must capture prerequisites, launch/attach,
   breakpoint placement, trigger command, and verification without secrets or
   machine-specific identity.

## Per-chunk walkthrough

For each chunk, record:

- chunk ID and status (`pending`, `breakpoint-reached`, `attested`, `blocked`);
- file/line range and changed symbols;
- explicit debugger and configuration source;
- breakpoint location;
- exact trigger test/command and result;
- paths/branches/values the engineer observed while stepping;
- defects/questions discovered;
- engineer identity as explicitly supplied, UTC timestamp, HEAD SHA, and exact
  attestation statement.

Ask the engineer to confirm a statement equivalent to:

> I personally stepped through this chunk line by line in the named debugger at the
> recorded HEAD and observed the recorded execution path.

Record the engineer's actual response; do not rewrite a refusal or qualified response as
completion.

## Done when

- [ ] Base and HEAD are explicit and HEAD still matches the ledger
- [ ] Every changed executable line belongs to exactly one chunk
- [ ] Every executable chunk reached its recorded breakpoint through its recorded test
- [ ] Every chunk has explicit per-chunk engineer attestation
- [ ] Non-executable changed code has explicit line-by-line engineer-read attestation
- [ ] No chunk is pending, blocked, stale, or self-certified by the agent
- [ ] Any current findings are offered to Stage 10 as advisory evidence; absence or incompleteness does not block the handoff