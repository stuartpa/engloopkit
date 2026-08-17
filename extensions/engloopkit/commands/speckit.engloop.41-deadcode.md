---
name: speckit.engloop.41-deadcode
description: Propose and, only after explicit user approval, remove the single highest-certainty dead-code candidate.
argument-hint: "[dead-code scan scope]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, agent]
agents: [Explore]
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.41-deadcode --root .
      timeout: 30
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Create one record at a time under `.engloop/deadcode/`, named exactly
`DEADCODE<NNN>-<brief-kebab-description>.md`. Reserve the next three-digit
`DEADCODE` number in `.engloop/numbering-registry.md` before creating the record.

## Loop definition

- **Trigger:** explicit stewardship capacity exists and the user asks for dead-code analysis.
- **Goal:** identify the single most certain dead-code candidate, prove the proposed deletion before touching current source, and ask the user whether to remove exactly that candidate.
- **Actions:** rank candidates by certainty; investigate references and runtime contracts; test the exact deletion in an isolated copy; write the DEADCODE record; ask for approval; remove only on explicit approval, or record rejection and inspect a new candidate.
- **Verification:** no current source changes before approval; every proposal has complete evidence and a reproducible deletion gate; accepted removal passes the declared validation and rejected code remains untouched.
- **Memory:** `.engloop/deadcode/DEADCODE<NNN>-<description>.md` plus the global numbering registry.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.41-deadcode --root .`

## Certainty gate

A candidate is eligible only when **all** checks below pass. If any check is missing,
ambiguous, inferred from naming, or contradicted, reject the candidate and continue
searching without asking the user.

1. Use symbol-aware reference analysis where available, then exact repository search.
   Record zero live compile-time callers outside the candidate itself.
2. Exclude dynamic use: reflection, serialization, dependency injection, configuration,
   command/route registration, source generation, native interop, plugin discovery,
   event subscription, framework convention, conditional compilation, platform-specific
   code, scripts, templates, tests/fixtures, documentation contracts, and public API use.
3. Check generated artifacts and authoritative external metadata. Absence from one build,
   one workload, code coverage, or text search alone is never proof of dead code.
4. Inspect relevant Git history, ownership, architecture, North Star, and Learnings cues.
   Do not delete code merely because its purpose is unclear or currently untested.
5. In a disposable isolated copy/worktree, apply only the proposed deletion and run the
   predeclared proof: `dotnet build EngLoopKit.slnx -c Debug`,
   `dotnet test EngLoopKit.slnx -c Debug --no-build`, plus all applicable repository,
   generated, platform, and behavior gates. Record commands, exit codes, and evidence.
6. Prefer a private, unreachable member or branch over a larger/public candidate. Public,
   protected, reflective, generated, serialized, or extension points are ineligible unless
   authoritative contract evidence proves they are not externally consumable.

Only `Confidence: HIGH` with every checklist item evidenced may reach user review.
“Probably unused,” analyzer output alone, zero coverage, or no text matches is insufficient.

## Proposal and approval protocol

1. Before changing current source, create the record from `DEADCODE-template.md` with
   `Status: PROPOSED`, exact symbol/path/lines, evidence, false-positive exclusions, the
   deletion diff, and the validation result from the isolated copy.
2. Ask exactly whether to proceed with removal of that numbered candidate. Accept only an
   explicit yes/approve/proceed response in the current conversation. Silence, a handoff,
   prior enthusiasm, or approval of another candidate is not approval.
3. If approved, set `User decision: APPROVED`, apply exactly the recorded deletion, and
   run the same validation against current source. Set `Status: REMOVED` only after PASS.
   On failure, restore the deleted bytes, record diagnostics, and set `Status: ROLLED-BACK`.
4. If the user disagrees, set `Status: REJECTED`, record a concise faithful rationale (or
   `declined without reason`), and do not alter the candidate. Then start a new
   `DEADCODE<NNN>` record and repeat the certainty gate for the next-best candidate.
5. If no candidate passes the certainty gate, create one `DEADCODE<NNN>-no-certain-candidate.md`
   with `Status: NO-CANDIDATE`, list what was checked, and stop without asking for removal.

## Prohibited behavior

- No source deletion, disabling, commenting-out, rename, API hiding, or generated-file edit before explicit approval.
- No bundling multiple candidates into one approval.
- No guessing application semantics, deleting compatibility surfaces, or weakening tests to make deletion pass.
- No commits, pushes, releases, deployments, package publication, or destructive history edits.

## Done when

- [ ] The registry was advanced before creating exactly one candidate record
- [ ] The proposal identifies the highest-certainty eligible candidate with complete evidence
- [ ] The exact deletion passed in an isolated copy before the user was asked
- [ ] Current source changed only after explicit candidate-specific approval
- [ ] Rejection is recorded and causes a new numbered search, or approved removal passes all gates
