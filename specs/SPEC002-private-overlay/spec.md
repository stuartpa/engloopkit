# SPEC002: Private ELK Overlay

- **Feature ID:** SPEC002
- **Status:** IMPLEMENTING
- **Target release:** EngLoopKit **v1.9.1**
- **Classification:** additive product feature / consumer-installation capability
- **SemVer policy:** v1.9.1 remains on the 1.x maturity line.

## Purpose

Allow an engineer to use EngLoopKit inside an existing Git repository while keeping all
ELK-created or ELK-installed artifacts local. A normal commit and push of the working
branch must contain no overlay-managed ELK paths, so the remote repository is
indistinguishable from one where overlay ELK was never used.

The overlay is portable: `overlay pack` creates one plain ZIP archive of the managed
local state; `overlay unpack` restores it to another checkout of the same repository
only after path, hash, Git identity, and collision checks pass.

## Explicit non-goals

- Encryption, secret storage, credential transport, or source-control bypass.
- UI validation, editor automation, picker inspection, diagnostics scraping, or screenshots.
- Guessing product identity, module discovery, framework, repository layout, or a Git
  remote. Ambiguous/missing configuration fails closed.
- Modifying tracked files, an existing ELK/Spec Kit installation, existing hooks, or
  existing generated agent/prompt paths during overlay installation.

## User stories

### US1 — Install an overlay into an existing Git repository

An engineer invokes the released tool explicitly:

```text
dotnet tool run engloopkit -- overlay install --mode overlay --root <repo> --product-id <explicit-id>
   --tool-nupkg <released-nupkg> --extension-archive <downloaded-release-archive.zip>
```

The `dotnet tool run` invocation is made from a private bootstrap manifest outside the
selected repository. The transaction creates the target root's `.config/dotnet-tools.json`
only after installing local Git exclusions; it does not require a pre-existing
target-local tool manifest.

The installer must:

1. require the selected path to be the Git root;
2. reject tracked or pre-existing collisions with every path it would own;
3. write local-only `.git/info/exclude` entries **before** creating ELK files;
4. install local pre-commit and pre-push hooks only if those hooks are absent or already
   ELK-owned;
5. install the root-local tool manifest and official Spec Kit extension through supported
   commands;
6. create local `.engloop/`, `NORTHSTAR.md`, and `LEARNINGS.md` placeholders with an
   explicit unproven runway/configuration state;
7. record every owned path, hash, exclude rule, hook identity, archive identity, Git
   remote identity, and base revision in `.engloop-overlay/manifest.json`;
8. prove `git check-ignore` covers every managed file and `git ls-files` contains none.

A failure rolls back only files created by the transaction and preserves pre-existing
repository state.

### US1b — Coexist with a repository-owned agent host

An engineer whose repository already owns `.github/agents/`, `.github/prompts/`, or an
existing local hook invokes:

```text
dotnet tool run engloopkit -- overlay install --mode overlay --host-mode coexist ...
```

Coexistence requires an existing local `.specify/` host. It must preserve every
pre-existing agent/prompt file byte-for-byte, add only exact namespaced ELK agent/prompt
files, and chain an existing local hook by preserving it as `*.elk-prior` before running
ELK verification. It rejects tracked shared Spec Kit registration files and any exact ELK
name collision; it never requires moving, deleting, or renaming repository-owned files.

### US2 — Prevent managed paths entering ordinary commits or pushes

Overlay install writes local `.git/hooks/pre-commit` and `.git/hooks/pre-push` hooks.
Both invoke `engloopkit overlay verify`.

`overlay verify` fails if:

- any managed path is tracked, staged, or appears in the current branch history;
- an owned file is not ignored by the local Git exclude rules;
- the overlay manifest is malformed, has a path escape, or records a hash mismatch.

Hooks protect normal Git commits/pushes. A user who deliberately bypasses Git hooks or
uses low-level Git plumbing is outside the tool’s protection and is reported in docs;
ELK never claims it can defeat an intentional bypass.

### US2b — Register outputs selected after installation

Before an overlay workflow creates a model project or generated destination outside
`.engloop/`, it invokes `overlay register` with an explicit repository-relative file or
directory. Registration atomically reconciles the manifest and local exclude block and
fails if the path is already tracked, staged, or present in history since the overlay
baseline. Verification and hooks immediately enforce the newly registered ownership.

Ownership is never inferred from application/module names, path conventions, CORD field
names, or product layout. Files not explicitly registered remain ordinary product source.

### US3 — Pack local overlay state

`engloopkit overlay pack --root <repo> --output <outside-managed-root>.zip`:

- runs `overlay verify` first;
- writes a deterministic ZIP containing a top-level `overlay-manifest.json` and all
  registered managed files under `files/<relative-path>`;
- includes SHA-256 and length for every entry;
- excludes secrets by policy: no `.env`, private keys, credential files, Git internals,
  arbitrary untracked files, or paths outside the registered overlay set;
- refuses an output inside a managed overlay root.

### US4 — Restore onto another checkout

`engloopkit overlay unpack --root <repo> --input <overlay.zip>`:

- requires selected Git root and matching repository identity (origin URL when present,
  otherwise explicit repository ID); no implicit override;
- rejects ZIP-slip paths, duplicate entries, malformed manifests, unknown paths, hash
  mismatch, tracked collisions, existing non-overlay collisions, and hook conflicts;
- writes local excludes before restoring files;
- recreates only registered files and ELK-owned local hooks;
- verifies no restored managed path is tracked or unignored.

## Acceptance criteria

1. Overlay install on a clean existing Git repo creates managed local artifacts, while
   `git status --short` reports none of them and `git check-ignore` confirms each.
2. Adding a managed file with `git add -f` makes `overlay verify` and the installed hook
   fail before commit/push.
3. Pack then unpack into a second checkout of the same repository restores byte-identical
   managed files and local excludes; no overlay file is tracked.
4. Unpack rejects a different repository origin, path traversal, corrupted archive entry,
   tracked collision, and existing hook conflict without partial restoration.
5. Source/archive/disposable-install tests cover the component and tool paths; no UI
   validation is performed.
6. Coexist mode preserves pre-existing tracked and local agent files byte-for-byte and
   chains an existing LFS-style pre-push hook without modifying its contents.
7. Registering a model directory and generated file after installation makes both locally
   ignored; forced staging and post-baseline commits are rejected by staged/push verify.
8. Registration is case/slash normalized, rollback-protected, rejects prior leakage, and
   does not block unrelated unregistered product source.
9. Removal of a non-empty registered directory quarantines children before deleting the
   root and reports path/operation/exception details on failure.
10. Install/unpack persist exact pre-install hook bytes (or absence); removal restores
    non-ELK and pre-existing ELK wrappers byte-for-byte before reporting success.
