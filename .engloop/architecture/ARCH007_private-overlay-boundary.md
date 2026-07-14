# ARCH007: Private overlay boundary

- **Created:** 2026-07-13
- **Status:** ACCEPTED
- **Feature:** [SPEC002](../../specs/SPEC002-private-overlay/spec.md)
- **Consulted learning:** [Readiness is a gate](../learnings/cards/readiness-is-a-gate.md)
  (`PM001/LEARN001`–`LEARN003`)

## Decision

ELK overlay mode is a deterministic **local installation transaction**, selected
explicitly by `engloopkit overlay install`. It is not a normal extension installation
with a reminder to ignore files.

The transaction owns a closed path set, writes `.git/info/exclude` before creating
anything, records a hash manifest, and installs local Git hooks that invoke `overlay
verify` before ordinary commits/pushes. Any collision, tracked path, unignored path,
manifest mismatch, archive path escape, or foreign repository identity fails closed.

## Why local Git mechanisms

- `.git/info/exclude` is local to a checkout and is not pushed.
- `.git/hooks/` is local to a checkout and is not pushed.
- Both are appropriate for overlay state because the overlay itself must disappear from
  ordinary remote history.
- A tracked `.gitignore` rule would violate the feature’s central promise.

## Protected path set

The initial overlay transaction owns only paths created by official Spec Kit init/extension
installation and ELK itself:

- `.engloop/`, `.engloop-overlay/`, `.config/dotnet-tools.json`;
- `.specify/`;
- `.github/agents/`, `.github/prompts/`, `.vscode/settings.json` only when absent at
  preflight;
- root `NORTHSTAR.md` and `LEARNINGS.md` only when absent at preflight;
- local ELK hooks in `.git/hooks/pre-commit` and `.git/hooks/pre-push` only when absent
  or already ELK-owned.

Existing files in those locations are an actionable conflict, never a merge target.

## Consequences

- Overlay mode is suitable for large existing repositories because it does not infer or
  modify application semantics.
- It cannot coexist silently with an existing Spec Kit/ELK surface that shares managed
  paths; operators choose a normal installation or explicitly resolve the conflict.
- Pack/unpack is an artifact transfer mechanism, not security storage. Archives are
  unencrypted by design and must not contain secrets.
- Normal Git hooks provide ordinary-push protection; deliberate hook bypass is not
  something a repository-local tool can prevent, so ELK documents that limit rather than
  claiming absolute enforcement.
