# Incident IN008: EngLoopKit v1.17 upgrade contract failure

- **Started:** 2026-09-06
- **Declared:** 2026-09-06
- **Reported by:** EngLoopKit maintainer
- **Affected:** Stable ELK upgrade discovery and supported v1.16.0 → v1.17.0 upgrade transactions
- **Status:** STABILIZED
- **Permanent repair:** OPEN — no closure claim
- **Cause-class (preliminary):** release-contract / missing-upgrade-gate / emergency-path divergence
- **North Star consulted:** `NORTHSTAR.md`, revision `Last revised: 2026-07-10`
- **Learning cues consulted:** `readiness-is-a-gate`, `PM001/LEARN001–003`;
  `verification-follows-artifact-class`, `PM002/LEARN001–003`

## Incident demand

After the emergency v1.17.0 publication, a supported updater starting from a valid
v1.16.0 installation downloaded hash-valid public assets, mutated managed ELK/SEK state,
then failed final surface validation because the published release manifest omitted
`commands.ids`. Rollback restored v1.16.0 in the observed reproduction. The maintainer
declared the unusable release and multi-day release cycle unacceptable and requested an
explanation, immediate containment, and a later post-mortem/permanent repair.

## Confirmed failure

- Published `engloopkit-release-manifest-1.17.0.json` contains
  `commands.count: 29` but no ordered `commands.ids`.
- The updater shipped in both v1.16.0 and v1.17.0 executes
  `$ids = @($Manifest.commands.ids)` under strict PowerShell mode.
- `Read-ReleaseManifest` validates only `schemaVersion` and `version`; it does not validate
  the complete contract before mutation.
- Release validation proved clean installation surfaces but did not run the final public
  assets through a disposable prior-stable v1.16.0 → v1.17.0 upgrade transaction.
- The emergency manual manifest path diverged from `validate-package.ps1`, whose normal
  manifest generator includes both command count and ordered IDs.

## Why this escaped after a multi-day release

The elapsed time was spent repeatedly executing broad build/test/coverage/package checks,
not validating the critical supported upgrade journey. The emergency mitigation then
bypassed the normal package-manifest generator to publish despite a NOT-READY verdict.
That produced a release artifact contract different from the one the updater consumes.
Neither schema validation nor check-only mode required all fields before mutation, and no
prior-stable upgrade test used the final immutable manifest and archives. More elapsed
validation therefore did not increase confidence in the missing end-to-end contract.

This is the same class identified by `PM001/LEARN001–003`: a required transition was not
represented by a deterministic gate, so completion was asserted from adjacent evidence.
`PM002/LEARN001–003` also applies: clean-install verification is not the same artifact
class as an upgrade transaction; the method must exercise the actual prior-stable upgrade
contract without lowering the common quality bar.

## Timeline and mitigations

| Time | Action | MIT | Evidence / result |
|---|---|---|---|
| T0 | Public v1.17.0 was published as stable/latest under IN007 emergency mitigation. | — | Release URL and four public hash-verified assets. |
| T1 | Supported v1.16.0 updater selected v1.17.0 and reproduced the final validation failure. | — | Strict-mode error: manifest property `commands.ids` absent; transaction reported `ELK_UPGRADE_FAILED_ROLLED_BACK`. |
| T2 | Rollback coherence was verified in the reproduction. | MIT001 | Tool/extension both v1.16.0; agent entry returned `AGENT_ENTRY_OK`. |
| T3 | Stage 20 entry validation was attempted in the engineering worktree. | MIT002 | `dotnet tool run engloopkit ...` unavailable because the root-local tool was not restored; context explicitly deferred, not treated as validated. |
| T4 | North Star and directly relevant learning provenance were consulted. | MIT003 | North Star 2026-07-10; `PM001/LEARN001–003`; `PM002/LEARN001–003`. |
| T5 | v1.17.0 was marked prerelease and a prominent known-upgrade-blocker warning was added. | MIT004 | Public release now `prerelease=true`; warning directs users to v1.16.0. |
| T6 | GitHub's explicit latest designation was cleared. | MIT005 | `GET /repos/stuartpa/engloopkit/releases/latest` returns stable v1.16.0, so installed stable updaters no longer select v1.17.0. |

## Stabilization evidence

- [x] Known-broken v1.17.0 is no longer returned by the exact stable `/releases/latest`
  endpoint used by the updater.
- [x] v1.17.0 remains public as a prerelease with a visible do-not-upgrade warning; no
  immutable asset was silently replaced.
- [x] Stable latest resolves to v1.16.0 (`draft=false`, `prerelease=false`).
- [x] One reproduced consumer was restored coherently to v1.16.0.
- [x] No claim is made that the updater contract or v1.17.0 assets are repaired.

## Direction and learning context

- **North Star SHA-256:** `7b6fb4b6df799630f0fc458b1c2169fd095e24b813b34fd83d0fa945a50e70a8`
- **Learning context:** CONSULTED
- **Rule IDs:** `RULE:readiness-is-a-gate`, `RULE:verification-follows-artifact-class`
- **Source IDs:** `PM001/LEARN001`, `PM001/LEARN002`, `PM001/LEARN003`, `PM002/LEARN001`, `PM002/LEARN002`, `PM002/LEARN003`
- **Deferral reason:** NOT-REQUIRED

## Verification (stability, not root-cause fix)

- [x] Health checks passing: GitHub stable `/releases/latest` returns v1.16.0 with
  `draft=false` and `prerelease=false`; v1.17.0 reports `prerelease=true`.
- [x] User workflows unblocked: supported stable updaters remain on coherent v1.16.0
  instead of selecting the known-broken v1.17.0 transaction.
- [x] No fresh errors in the watch window: repeated authoritative release API reads after
  clearing `make_latest` returned v1.16.0; no additional consumer mutation was initiated.

## Deferred permanent work for Stage 21/22

1. Publish a fixed 1.x successor rather than mutating the immutable v1.17.0 assets.
2. Require ordered, unique `commands.ids` and count equality in a versioned release-
   manifest schema and in `Read-ReleaseManifest` before any mutation, including check-only.
3. Add a disposable end-to-end prior-stable upgrade gate using the final four release
   assets; success requires `ELK_UPGRADE_PASS`, exact 29 command/agent/prompt surfaces,
   matching versions, and agent-entry validation.
4. Make the release orchestrator consume one manifest builder only; forbid emergency
   alternate manifest construction.
5. Add measured release critical-path evidence and an enforceable sub-90-minute budget,
   with fail-fast disk/tool/schema/upgrade preflight before long coverage partitions.

## Stage 21 handoff

The post-mortem must cover both IN007 (multi-day release cycle) and IN008 (published
upgrade-contract failure). It must distinguish excessive validation latency from missing
critical-path validation and produce rule-bound repair items. This incident records
containment only; it does not close the release-system repair.