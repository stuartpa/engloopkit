# ARCH004: The executable core and its verification platform

- **Created:** 2026-07-06
- **Status:** ACCEPTED
- **Governs:** `src/EngLoopKit.Core`, `model/`, `tests/`, and the dev platform

## Decision

EngLoopKit's load-bearing invariants — the **engineering-loop state machine** and the
**document-numbering discipline** — are implemented as a small, verified C# core
(`EngLoopKit.Core`), the executable form of the prose in `docs/`. The core is verified on
**the current ELK platform** (.NET 10 / C# / xUnit, no extra application dependencies): a SEK model
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
- No dependency beyond .NET 10, xUnit, and SEK's published Modeling package (consumed as

  ## 2026-08-17 platform evolution

  ELK targets `net10.0`, pins SDK `10.0.303`, and uses `EngLoopKit.slnx` as the sole
  solution graph. SEK v0.1.3 remains independently versioned: its exact released tool,
  Modeling package, and generated ELK tests run natively on .NET 10, while
  are normalized to `net10.0`. This preserves the common readiness bar from
  `readiness-is-a-gate` (`PM001/LEARN001–003`) and changes verification mechanics without
  lowering them, per `verification-follows-artifact-class` (`PM002/LEARN001–003`).
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
