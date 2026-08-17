---
name: speckit.engloop.80-upgrade-elk
description: Upgrade the selected repository's root-local ELK tool and installed ELK
  extension to the latest verified non-prerelease release, or report that both are
  already current.
argument-hint: '[--check-only]'
target: vscode
user-invocable: true
disable-model-invocation: true
tools:
- read
- search
- execute
agents: []
hooks:
  SessionStart:
  - type: command
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.80-upgrade-elk
      --root .
    timeout: 30
---


<!-- Extension: engloop -->
<!-- Config: .specify/extensions/engloop/ -->
## User Input

```text
$ARGUMENTS
```

## Artifact root

Transient download, rollback, and result evidence lives only under
`.engloop/out/upgrade-elk/` and must remain ignored.

## Loop definition

- **Trigger:** the user asks whether ELK is current or asks to upgrade ELK.
- **Goal:** root-local ELK tool and installed `engloop` extension exactly match the latest verified non-draft/non-prerelease GitHub release, or an evidence-backed already-current result.
- **Actions:** run the shipped updater in check-only or upgrade mode; report its exact result.
- **Verification:** release manifest and artifact SHA-256 values are verified; local tool and installed extension versions agree; exact generated command surface validates; rollback restores the prior verified release on failed mutation.
- **Memory:** ignored `.engloop/out/upgrade-elk/` result and diagnostics only.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.80-upgrade-elk --root .`

## Required behavior

1. Use only the shipped updater:

   `pwsh -NoProfile -File .specify/extensions/engloop/scripts/Update-EngLoopKit.ps1 -Root .`

   Add `-CheckOnly` only when explicitly requested.
2. The updater queries GitHub's latest **non-draft, non-prerelease** release for
   `stuartpa/engloopkit`. It does not infer versions from branches, tags, catalogs, or
   filenames alone.
3. Report **already current** only when both `.config/dotnet-tools.json` and installed
   `.specify/extensions/engloop/extension.yml` equal the latest release version and the
   generated agent/prompt surface matches that release manifest.
4. An upgrade requires the release's machine-readable manifest plus exact tool and
   extension assets. Verify every SHA-256 before mutation. Missing/ambiguous assets or
   mismatched hashes fail closed.
5. The updater snapshots managed local state and downloads the currently installed
   verified release artifacts before mutation. If tool/extension installation or final
   validation fails, it restores that exact prior release and reports failure; it never
   switches to a different provider, package source, or development checkout.
6. Never edit product source, install globally, choose another repository root, disable
   integrity checks, commit, push, deploy, or claim success from download alone.
7. If organization/network policy blocks GitHub, report the actionable blocker. Do not
   use stale cached release metadata as “latest.”

## Done when

- [ ] Result is exactly `ELK_UPGRADE_CURRENT` or `ELK_UPGRADE_PASS`, with version evidence
- [ ] Tool and installed extension versions are equal
- [ ] Release manifest and artifact hashes were verified
- [ ] Exact installed agent/prompt surface passed
- [ ] Any failed mutation was rolled back to the exact prior verified release