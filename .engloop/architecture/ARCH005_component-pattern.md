# ARCH005: The component pattern (and its recursive propagation)

- **Created:** 2026-07-06
- **Status:** ACCEPTED
- **Governs:** all code in every repository EngLoopKit builds or governs
- **Canonical principle:** [../../docs/component-pattern.md](../../docs/component-pattern.md)

## Decision

Code is split into two physically-separate kinds: the **vertical** (the domain the repo
exists to build) and **components** (small single-purpose building blocks that wrap the
language runtime / BCL to solve a *generic* problem, carrying no domain knowledge). The
vertical composes the components it needs. For C#/.NET the components live in a top-level
`components/` folder, one class-library project per component (`<Repo>.Components.<Name>`).

Crucially, this is a **methodology principle, not just EngLoopKit's own code choice**:
EngLoopKit *causes every repository it governs* to adopt the pattern.

## Context (from the bridging code)

EngLoopKit's bridging core (`EngLoopKit.Core`) mixed two generic mechanisms — monotonic
prefixed counters and a guarded state machine — into the engineering-loop vertical. Both pass
the litmus test (useful, unchanged, in an unrelated repo), so both were extracted.

## The rule

- Non-vertical code (litmus test: *useful unchanged in a different repo?*) MUST live as a
  component, not in the vertical.
- Components carry no domain knowledge; the vertical passes domain specifics in.
- Dependencies point one way: **vertical → components**, never the reverse.
- Folder is language-dependent (C# `components/`; Go `internal/`; see the principle doc).

## Enforcement

- **Stage 2 · Architect** establishes the boundary and records it as a governed rule
  (this ARCH record); architecture-guard governs it thereafter.
- **Stage 3 · Refactor-to-final** and **Stage 7 · Refactor-scan** converge toward it (a
  dedicated decision-tree branch extracts one leaked component per cycle).
- The conformance suite couples the principle to reality: the architect and refactor-scan
  commands must reference the pattern, and EngLoopKit itself must have a `components/` folder
  composed by its vertical.

## Applied to EngLoopKit itself

- `components/EngLoopKit.Components.Numbering` — monotonic, never-reused counters keyed by
  string (generic). The vertical supplies the EngLoopKit prefixes.
- `components/EngLoopKit.Components.StateMachine` — a generic `TransitionGraph<TState>` +
  `GuardedMachine<TState>`. The vertical supplies the `Stage` values and the loop's graph.
- `src/EngLoopKit.Core` (the vertical) composes both.

## Consequences

- The vertical shrinks to just the domain; generic machinery is reusable and independently
  testable.
- Every EngLoopKit-governed repo (e.g. SEK) is driven to the same shape — it cannot pass its
  architecture stage without the boundary, and its refactor cycles keep tightening it.
