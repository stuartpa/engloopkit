# ARCH004: The executable core and its verification platform

- **Created:** 2026-07-06
- **Status:** ACCEPTED
- **Governs:** `src/EngLoopKit.Core`, `model/`, `tests/`, and the dev platform

## Decision

EngLoopKit's load-bearing invariants — the **engineering-loop state machine** and the
**document-numbering discipline** — are implemented as a small, verified C# core
(`EngLoopKit.Core`), the executable form of the prose in `docs/`. The core is verified on
**the same platform as SEK** (.NET 8 / C# / xUnit, no extra dependencies): a SEK model
generates a conformance suite, and hand-written xUnit tests cover the deep behaviour.

## Context (from the bridging code)

The bridging bundle was pure markdown with no machine-checkable invariants. To be
"ready to do incidents" the loop and numbering rules must be *enforceable*, and the
dogfood must run SEK for real. The constraint "no new deps beyond what SEK takes" fixes
the platform to SEK's.

## The rule

- The loop transition graph and numbering rules live in `EngLoopKit.Core`; docs describe,
  code enforces.
- The SEK model (`model/EngLoopKit.Model`) is an independent spec; `sek explore` +
  `sek generate` produce the committed conformance tests that drive the core.
- No dependency beyond .NET 8, xUnit, and SEK's `Sek.Modeling` (referenced by project, as
  SEK's own samples do). SEK is consumed via its v0.1.1 tool.
- All tests execute fast and must stay green.

## Enforcement

`dotnet test EngLoopKit.slnx` (40 tests: 1 SEK-generated + 39 hand-written) in CI;
`sek validate` on the model.

## Consequences

- The methodology's core is now machine-checked, not just documented.
- Coverage means **conformance/artifact coverage**: every stage, prefix, command,
  template, and manifest field is exercised. See [COV001](../coverage/COV001_conformance.md).
- A verification bug now surfaces as a failing test — the substrate the Operations loop
  (incidents/post-mortems) needs.
