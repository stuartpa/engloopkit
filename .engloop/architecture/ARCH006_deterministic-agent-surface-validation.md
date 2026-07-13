# ARCH006: Deterministic agent-surface validation — no UI validation

- **Created:** 2026-07-13
- **Status:** ACCEPTED
- **Governs:** command/agent/prompt release validation, package validation, and consumer migration
- **Consulted learnings:** [Readiness is a gate](../learnings/cards/readiness-is-a-gate.md)
  (`PM001/LEARN001`–`LEARN003`); [Verification follows artifact class](../learnings/cards/verification-follows-artifact-class.md)
  (`PM002/LEARN001`–`LEARN003`)

## Decision

EngLoopKit **never performs UI validation**. It must not launch, inspect, scrape,
automate, screenshot, or otherwise interact with an editor window, command picker,
Problems panel, agent UI, or other graphical surface as a release or consumer gate.

Agent-surface acceptance is exclusively deterministic and headless:

1. validate the exact source command set and frontmatter semantics;
2. build the immutable extension archive;
3. install that archive into a disposable root through Spec Kit's supported path;
4. compare source, installed command/agent, and generated prompt semantic projections;
5. validate exact tool/agent policies, 23 review-first handoff edges, absence rules,
   local-tool entry rejection, and extension removal behavior.

## Context

A prior attempt to obtain a real editor picker/diagnostics observation was intrusive,
not reliably automatable in this environment, and risked interacting with an unrelated
active workspace. That violates the product's token- and operator-efficiency goals.
A UI observation is neither the authoritative representation of a command surface nor a
necessary proof of archive/install semantics.

## Rule

- No ELK script may require or accept editor UI evidence.
- No ELK release gate may block on picker counts, Problems-panel diagnostics, screenshots,
  browser/Electron debugging, accessibility scraping, or editor automation.
- Command discoverability is proven by the exact ordered 13-ID source/archive/install set;
  semantic preservation is proven by parsed YAML projections.
- A deterministic install mismatch fails closed. No UI fallback is attempted.

## Consequences

- Release validation is reproducible in CI and safe on existing/internal repositories.
- Version/schema metadata may remain as compatibility provenance, but it is never an
  instruction to open or inspect a UI.
- Consumer migration validates installed files and tool behavior, not the editor UI.
