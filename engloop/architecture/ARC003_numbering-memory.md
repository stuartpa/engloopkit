# ARC003: Numbered documents are the loop's Memory

- **Created:** 2026-07-06
- **Status:** ACCEPTED
- **Governs:** all artifacts, the document standards, and the numbering discipline

## Decision

The loop's **Memory** is a set of documents, each named `<PREFIX><NNN>_<title>.md` with a
fixed prefix, a monotonically increasing never-reused number, and a brief title, located
under a **configurable artifact root** (default `docs/`; override e.g. `engloop/`).

## Context (from the bridging code)

The bridging bundle already defined prefixes and a numbering registry. SEK (the first real
consumer) revealed that a hardcoded `docs/` root collides with a published documentation
site — motivating the configurable root and the `BRG` addition (shipped in v1.1.0).

## The rule

- Prefixes are fixed: SEED, SP, BRG, ARC, MDL, CRD, COV, IN, PM, REF (global) and MIT,
  LRN, RPI (local). No ad-hoc prefixes.
- Numbers are monotonic, never reused, zero-padded to three digits; increment the registry
  **before** creating the file.
- All artifact paths are relative to `<ARTIFACT_ROOT>/`.

## Enforcement

The rules are the **executable core** `EngLoopKit.Core.NumberingRegistry` (ARC004), tested
directly, and the conformance test `StandardsDoc_documentsEveryCorePrefix` couples the
prose standard to that core (every code prefix must be documented).

## Consequences

- Context persists as greppable, cross-referenced Memory — later loops reload decisions
  instead of re-deriving them (the core token saving).
- Projects with a published `docs/` keep process artifacts cleanly separate.
