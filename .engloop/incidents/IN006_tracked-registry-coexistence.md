# Incident IN006: tracked registry blocks overlay coexistence

- **Started:** 2026-07-23T12:02:24-07:00
- **Reported by:** OLTP overlay operator
- **Affected:** ELK v1.11.1 overlay install into repositories with a tracked SpecKit extension registry
- **Status:** STABILIZED — no repair closure claim
- **Resolved at:** 2026-07-23T15:54:50-07:00
- **Duration:** 3h 52m
- **Cause-class (preliminary):** capability-contract gap

## Symptom

A repository-only ELK v1.11.1 coexist installation derives identity correctly but stops
before mutation with:

`overlay-coexist-tracked-host-config:.specify/extensions/.registry`

The target repository already owns a tracked SpecKit host registry. The OLTP team is
blocked from using ELK while the repository remains unmodified.

## Timeline of mitigation actions

> Number mitigations MIT001, MIT002, … within this incident. A mitigation is NOT a fix.

| Time | Action | MIT | Evidence / result |
|---|---|---|---|
| 12:02 | Reserved IN006 and opened incident before further compatibility experiments. |  | Registry advanced from IN005 to IN006. |
| 12:02 | Preserved fail-closed install behavior; no tracked-registry workaround authorized. | MIT001 | Target report confirms no overlay manifest, no partial ELK install, and unchanged repository-owned files. |
| 13:05 | Inspected SpecKit CLI source and ran a disposable tracked-registry lifecycle probe. |  | SpecKit `extension add` materialized all 19 ELK agents; restoring `.registry` and `extensions.yml` byte-for-byte produced zero tracked diff. |
| 13:05 | Implemented transaction-owned restoration of shared host metadata and skipped SpecKit registry removal for tracked hosts. | MIT002 | Focused coexist install/verify/remove test passes while registry, extensions.yml, agents, and prior hook remain byte-identical. |
| 15:54 | Completed full direct/readiness/immutable package gates against tracked and untracked SpecKit hosts. | MIT003 | 205 direct tests pass; readiness PASS; package integration installs/removes with tracked registry and extensions.yml unchanged. |

## Snapshot bundle

Private target report and read-only probe output. Public incident records must contain no
private repository paths, registry contents, origin URLs, or product identities.

## Mitigations applied

- **MIT001** — Keep v1.11.1 preflight fail-closed while determining whether the installed
  SpecKit CLI exposes an authoritative tracked-registry-preserving extension mechanism.
- **MIT002** — Use SpecKit's supported extension materialization, then atomically restore
  repository-owned tracked host metadata before transaction success; removal deletes only
  manifest-owned ELK paths and never invokes registry mutation for a tracked host.
- **MIT003** — Publish the verified dot release and provide a target-only installation
  runbook after downloaded asset hashes are confirmed.

## Verification (stability, not root-cause fix)

- [x] Health checks passing: target repository remains on its pre-install SpecKit state
- [x] User workflows unblocked: v1.11.2 artifact transaction validates the target host shape
- [x] No fresh errors in the watch window: no partial overlay state was created

## Hand-off to Post-Mortem

- **Snapshot bundle:** private target probe report (pending)
- **Affected operations:** private coexist overlay installation into tracked SpecKit hosts
- **Cause-class hypothesis (preliminary):** missing authoritative coexistence capability
- **Suggested PM title:** SpecKit tracked-registry coexistence was absent from overlay host contracts
