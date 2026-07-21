---
name: speckit.engloop.51-overlay-remove
description: Remove an installed private ELK overlay and restore prior local hooks so the checkout appears never overlaid.
argument-hint: "--confirm REMOVE-OVERLAY:<repository-id>@<base-revision>"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.51-overlay-remove --root .
      timeout: 30
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Removal authority comes only from `.engloop-overlay/manifest.json`. Never derive deletion
scope from current product defaults, path names, or repository conventions.

## Loop definition

- **Trigger:** the user explicitly requests complete removal of the local ELK overlay.
- **Goal:** remove every manifest-owned installation/runtime path, remove ELK local excludes and wrappers, restore prior hooks, and preserve unrelated coexist-host files.
- **Actions:** verify clean ownership, present the exact deletion plan/token, obtain confirmation, execute `overlay remove`, and verify absence.
- **Verification:** manifest-owned paths are absent, the ELK exclude block is gone, prior hooks are restored byte-for-byte, unrelated host files remain, and no tracked/history leak was hidden.
- **Memory:** none in the checkout; removal deletes its own ELK agent and local process memory after the current invocation has loaded.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.51-overlay-remove --root .`

## Required behavior

1. Run `overlay status` and `overlay verify --mode all`. Stop if manifest identity,
   tracked/staged/history checks, wrappers, or local ignores are ambiguous.
2. Build deletion scope only from manifest `ManagedRoots`; dynamically registered paths
   are included. In coexist mode, unrelated pre-existing agents/prompts and host files are
   not manifest-owned and must remain untouched.
3. Require the exact confirmation token
   `REMOVE-OVERLAY:<repository-id>@<base-revision>`. No `--force`, guessed token, partial
   removal, or weaker fallback is allowed.
4. Run `dotnet tool run engloopkit -- overlay remove --root . --confirm <exact-token>`.
5. The tool removes ELK wrappers, restores `*.elk-prior` hooks, removes exactly the ELK
   `.git/info/exclude` block, and deletes manifest-owned paths child-first. The overlay
   root/manifest is deleted last.
6. Do not interpret removal as erasing a prior remote leak. If registered paths are in Git
   history, removal fails and repository history must be repaired separately.

## Done when

- [ ] Exact confirmation token was supplied by the user
- [ ] `OVERLAY_REMOVE_PASS` was emitted
- [ ] `.engloop-overlay/manifest.json` and every manifest-owned path are absent
- [ ] The ELK exclude block and wrappers are absent; prior hooks are restored
- [ ] Pre-existing coexist-host files and unrelated product files are unchanged
