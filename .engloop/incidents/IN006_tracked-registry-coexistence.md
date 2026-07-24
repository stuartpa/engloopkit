# Incident IN006: tracked registry blocks overlay coexistence

- **Started:** 2026-07-23T12:02:24-07:00
- **Reported by:** OLTP overlay operator
- **Affected:** ELK v1.11.1 overlay install into repositories with a tracked SpecKit extension registry
- **Status:** STABILIZED — target workflow verified; no post-mortem closure claim
- **Resolved at:** 2026-07-24T00:05:47-07:00
- **Duration:** 12h 03m
- **Cause-class (preliminary):** capability-contract gap plus unmet dependency version

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
| 21:44 | Target v1.11.2 passed artifact checks and tracked-registry preflight, then SpecKit exited 1 during extension materialization. |  | ELK reported only `overlay-command-failed:specify:1`; rollback left no manifest or generated ELK surface and preserved tracked registry. |
| 21:44 | Prepared a target-safe disposable-clone diagnostic for the exact SpecKit extension-add operation. | MIT004 | Requested SpecKit version, full stdout/stderr, registry/extensions hashes and content, generated counts, and clone Git status without modifying the target checkout. |
| 00:05 | Target diagnostic identified SpecKit 0.10.2 below ELK's declared minimum 0.12.0; upgraded only the machine-level CLI to 0.12.4. | MIT005 | Repository registry hashes and Git status remained unchanged during the CLI upgrade. |
| 00:05 | Retried ELK v1.11.2 installation on the tracked-registry host. |  | `OVERLAY_INSTALL_PASS` and `OVERLAY_VERIFY_PASS`; target reports SpecKit 0.12.4 and ELK 1.11.2 installed successfully. |

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
- **MIT004** — Diagnose the target-specific SpecKit exit in a disposable clone before
  authorizing another release or target mutation.
- **MIT005** — Upgrade the machine-level SpecKit CLI to verified compatible version
  0.12.4 without reinitializing or modifying the repository-owned SpecKit host.

## Verification (stability, not root-cause fix)

- [x] Health checks passing: final `OVERLAY_VERIFY_PASS`
- [x] User workflows unblocked: ELK v1.11.2 installed on the tracked-registry host
- [x] No fresh errors in the watch window: target reports final verification complete

## Hand-off to Post-Mortem

- **Snapshot bundle:** private target compatibility report and successful install response
- **Affected operations:** private coexist overlay installation into tracked SpecKit hosts
- **Cause-class hypothesis (preliminary):** tracked-registry coexistence gap compounded by
  hidden minimum-version incompatibility diagnostics
- **Suggested PM title:** Overlay dependency and tracked-host contracts failed in sequence
