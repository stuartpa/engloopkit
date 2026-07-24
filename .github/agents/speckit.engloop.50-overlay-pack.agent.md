---
name: speckit.engloop.50-overlay-pack
description: Verify and pack an explicitly installed private ELK overlay into one
  portable local ZIP archive.
argument-hint: --output <archive.zip>
target: vscode
user-invocable: true
disable-model-invocation: true
tools:
- read
- search
- edit
- execute
agents: []
hooks:
  SessionStart:
  - type: command
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.50-overlay-pack
      --root .
    timeout: 30
handoffs:
- label: Define local direction
  agent: speckit.engloop.01-northstar
  prompt: Define the overlay-local North Star after the private overlay verifies cleanly.
  send: false
---


<!-- Extension: engloop -->
<!-- Config: .specify/extensions/engloop/ -->
## User Input

```text
$ARGUMENTS
```

## Artifact root

Overlay mode owns the explicit paths in `.engloop-overlay/manifest.json`, including
installation paths and runtime paths registered through `overlay register`. It writes
**only** `.git/info/exclude` and ELK-owned local Git hooks; it never edits tracked
`.gitignore`.

## Loop definition

- **Trigger:** an engineer has explicitly installed ELK in overlay mode and needs to move that private local state to another checkout.
- **Goal:** one plain, hash-verified ZIP outside the repository containing only registered ELK-managed overlay paths.
- **Actions:** run overlay verification, reject tracked/history leakage and secret-like paths, refresh manifest hashes, and create the archive.
- **Verification:** archive manifest/file hashes match; the current checkout remains untracked and ignored; the archive is outside the repository.
- **Memory:** `.engloop-overlay/manifest.json` locally only; the user-selected ZIP.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.50-overlay-pack --root .`

## Required behavior

1. This agent **packs only**. Install and unpack are tool features because their target root may not yet have agents.
2. Confirm every runtime model/generated output was explicitly registered before creation.
3. Run `dotnet tool run engloopkit -- overlay pack --root . --output <zip-outside-repository>`.
4. The ZIP is deliberately unencrypted and must contain no secrets, credentials, `.env`, private keys, Git internals, or arbitrary untracked files.
5. The command never edits tracked `.gitignore`; never create a tracked ignore rule or
  modify workload/application files.
6. After intentionally restoring private content over a managed file, treat
  `overlay-manifest-file-mismatch` as expected stale provenance. Register any new paths,
  then run this pack command to atomically refresh hashes before requiring all-mode
  verification. Never edit manifest hashes by hand.

## Done when

- [ ] `overlay verify` proves every registered path is untracked, locally ignored, and absent from commits since the overlay baseline
- [ ] ZIP manifest and every archive entry hash verify
- [ ] Archive is outside the repository and contains only registered ELK overlay state