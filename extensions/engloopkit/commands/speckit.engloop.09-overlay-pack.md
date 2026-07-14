---
name: speckit.engloop.09-overlay-pack
description: Verify and pack an explicitly installed private ELK overlay into one portable local ZIP archive.
argument-hint: "--output <archive.zip>"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.09-overlay-pack --root .
      timeout: 30
handoffs:
  - label: Define local direction
    agent: speckit.engloop.01-northstar
    prompt: Define the overlay-local Northstar after the private overlay verifies cleanly.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Overlay mode owns a local-only managed set under `.engloop/`, `.engloop-overlay/`,
`.specify/`, generated ELK agents/prompts, the local tool manifest, and optional local
Northstar/Learnings files. It writes **only** `.git/info/exclude` and ELK-owned local Git
hooks; it never edits tracked `.gitignore`.

## Loop definition

- **Trigger:** an engineer has explicitly installed ELK in overlay mode and needs to move
  that private local state to another checkout.
- **Goal:** one plain, hash-verified ZIP outside the repository containing only the
  registered ELK-managed overlay paths.
- **Actions:** run overlay verification, reject tracked/history leakage and secret-like
  paths, refresh the local manifest hashes, and create the archive.
- **Verification:** archive manifest/file hashes match; the current checkout remains
  untracked and ignored; the archive is outside the repository.
- **Memory:** `.engloop-overlay/manifest.json` locally only; the user-selected ZIP.

Run before any action:

`dotnet tool run engloopkit overlay verify --root .`

## Required behavior

1. This agent **packs only**. Install and unpack are tool features because their target
   root may not yet have agents: `engloopkit overlay install ...` and
   `engloopkit overlay unpack ...`.
2. Run `engloopkit overlay pack --root . --output <zip-outside-repository>`.
3. The ZIP is deliberately unencrypted and must contain no secrets, credentials, `.env`,
   private keys, Git internals, or arbitrary untracked files.
4. Never create a tracked `.gitignore` rule or modify workload/application files.

## Done when

- [ ] `overlay verify` proves all managed files are untracked, locally ignored, and absent
      from commits since the overlay baseline.
- [ ] ZIP manifest and every archive entry hash verify.
- [ ] Archive is outside the repository and contains only registered ELK overlay state.
