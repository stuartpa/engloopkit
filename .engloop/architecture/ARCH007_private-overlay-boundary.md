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

## Coexistence host contract (v1.8.1)

Some repositories already own an untracked or tracked agent surface and local hooks. An
operator may explicitly select `--host-mode coexist` only when a local `.specify/` host
already exists. Coexistence does not merge directories:

- existing `.github/agents/` and `.github/prompts/` files are snapshotted and must remain
  byte-identical;
- ELK owns only exact `speckit.engloop.*.agent.md` and `.prompt.md` names plus
  `.specify/extensions/engloop/`;
- tracked shared registration files fail closed because a local overlay must not modify
  them;
- an existing hook is renamed to `*.elk-prior`, then ELK installs a wrapper that executes
  the prior hook before `overlay verify`; rollback restores the prior bytes exactly.

The portable archive contains ELK-owned state only. The target coexistence host must
already exist when unpacking; its unrelated agent/hook files are never archived or
rewritten.

## Runtime ownership registry (v1.8.2)

Installation-time ownership cannot describe model projects or generated suites selected
later by a workflow. Overlay mode therefore exposes one explicit generic registration
operation:

`engloopkit overlay register --root . --directory <path> --file <path>`

The command normalizes repository-relative paths, rejects `.git`, tracked, staged, and
post-baseline history collisions, then updates the overlay manifest and local exclude
block as one rollback-protected transaction. Verification uses the resulting registry for
staged, tracked, and history checks with case-insensitive slash-normalized matching.

ELK does not infer ownership from module names, CORD field names, product layout, or
application conventions. Stage 05/06 must explicitly register model/generator-owned
outputs before creating them. Unregistered product source remains ordinary trackable
source.

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
