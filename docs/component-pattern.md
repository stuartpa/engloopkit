# The Component Pattern

**Status:** Architectural principle — enforced by `/speckit.engloop.03-architect`, converged
toward by `/speckit.engloop.30-refactor-scan`.

EngLoopKit builds software in two kinds of code, and keeps them physically separate:

- **The vertical.** The code that *is* the thing this repository exists to build — the
  problem domain. It is specific to this repo and useful nowhere else.
- **Components.** Small, single-purpose building blocks that take the **language runtime /
  base class libraries** and solve a *generic* problem the vertical needs — but that are
  **not specific to this repo's vertical**. Each component could be lifted into a completely
  different repository, tackling a completely different problem, and still be useful.

The vertical **composes** the components it needs (plus, usually, one other piece of the
vertical) and solves the problem. This is the shape of
[go-sqlcmd](https://github.com/microsoft/go-sqlcmd/blob/main/cmd/modern/root/install/mssql.go):
each thing under `internal/` (`pal`, `localizer`, `cmdparser`, …) is a component; the
command code imports the components it needs and one other command, then solves the problem.

## The litmus test

> **Would this code be useful, unchanged, in a repo solving a totally different problem?**
> Yes → it is a **component**. No → it is **vertical**.

A component may know about the language and the BCL. It must **not** know about the vertical's
domain concepts. (Example from EngLoopKit: "monotonic, never-reused counters keyed by string"
is a component; "the EngLoopKit prefixes SEED/ARC/MDL…" is vertical. The vertical supplies the
prefixes *to* the component.)

## Where components live (language-dependent folder)

Each component is the language's natural unit of a reusable, dependency-scoped library, in a
dedicated top-level folder:

| Language | Folder | Unit per component |
|---|---|---|
| **C# / .NET** | **`components/`** | a class-library **project** `<Repo>.Components.<Name>` |
| Go | `internal/` | a package `internal/<name>` |
| TypeScript / JS | `packages/` (or `src/lib/`) | a package/module per component |
| Python | `components/` | a subpackage per component |
| Rust | `crates/` | a crate per component |

**Why `components/` (not `internal/`) for C#:** Go's `internal/` also carries *access-control*
semantics (packages under it can't be imported outside the module). C# gets encapsulation from
the `internal` **access modifier** and assembly boundaries, so a folder literally named
`internal` collides with that keyword's meaning. `components/` is unambiguous, and in .NET each
component is naturally its own **project/assembly** — the unit of reuse and dependency.

## The rules

1. **All non-vertical code is a component.** If it passes the litmus test, it does not belong
   in the vertical — extract it into `components/<Name>` (a project/package/crate).
2. **One folder per component**, each doing one thing.
3. **Components never depend on the vertical.** Dependencies point one way: vertical →
   components. A component may depend on another component; never the reverse of the vertical.
4. **The vertical composes.** Vertical code imports the components it needs (and usually one
   other vertical piece) and solves the domain problem.
5. **Components carry no domain knowledge.** Domain specifics are *passed in* by the vertical.

## When the pattern is applied (and to whom)

This is recursive — EngLoopKit applies it to **itself** and to **every repository it governs**:

- **Stage 03 · Architect** (`/speckit.engloop.03-architect`) **establishes and enforces** the
  boundary: it classifies existing code into vertical vs components, creates the language-
  appropriate `components/` folder, records the boundary as a governed architecture rule
  (an `ARC` that architecture-guard enforces), and files any non-vertical code still sitting
  in the vertical as refactor tasks.
- **Stage 04 · Refactor** and **Stage 30 · Stewardship** (`/speckit.engloop.30-refactor-scan`)
  **converge** toward the ideal: every refactor cycle pulls a little more non-vertical code out
  of the vertical into a component, iterating toward perfection of the pattern.

So a repo that adopts EngLoopKit is *caused* to adopt the component pattern: it cannot pass its
architecture stage without the boundary, and its refactor cycles keep tightening it.
