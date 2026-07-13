# ARCH003: Numbered documents are the loop's Memory

- **Created:** 2026-07-06
- **Status:** ACCEPTED
- **Governs:** all artifacts, the document standards, and the numbering discipline

## Decision

The loop's numbered **Memory** uses `<PREFIX><NNN>_<title>.md`: a fixed prefix, a
monotonically increasing never-reused number, and a brief title. Numbered process
artifacts live under the single tracked `.engloop/` root. Root `NORTHSTAR.md` and
`LEARNINGS.md` are explicit living, unnumbered entry points whose revision history is Git.

## Context (from the bridging code)

The original scaffold defined prefixes and a numbering registry. SEK (the first real
consumer) revealed that process memory must not collide with a published documentation
site. The v2 root decision resolves that conflict with one hidden, fixed process root and
an explicit scaffold/test-runway record class.

## The rule

- Prefixes are fixed: SPEC, SCAF, ARCH, MODEL, CORD, COV, IN, PM, REFACT (global) and
  MIT, LEARN, RPI (local). No ad-hoc prefixes or compatibility aliases.
- Numbers are monotonic, never reused, zero-padded to three digits; increment the registry
  **before** creating the file.
- All numbered process-artifact paths are relative to `.engloop/`; specifications remain
  under `specs/` by Spec Kit convention.

## Enforcement

The rules are the **executable core** `EngLoopKit.Core.NumberingRegistry` (ARCH004), tested
directly, and the conformance test `StandardsDoc_documentsEveryCorePrefix` couples the
prose standard to that core (every code prefix must be documented).

## Consequences

- Context persists as greppable, cross-referenced Memory — later loops reload decisions
  instead of re-deriving them (the core token saving).
- Projects with a published `docs/` keep process artifacts cleanly separate.
