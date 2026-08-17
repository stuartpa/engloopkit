# Incident IN<NNN>: <short title>

- **Started:** <timestamp>
- **Reported by:** <operator/monitor>
- **Affected:** <components>
- **Status:** INVESTIGATING
- **Resolved at:** (fill when resolved)
- **Duration:** (fill when resolved)
- **Cause-class (preliminary):** (fill during diagnosis)

At completion set `Status` exactly to `STABILIZED` or `RESOLVED`; ambiguous values such
as `NOT STABILIZED` are rejected.

## Symptom

<What is broken, in the reporter's terms.>

## Timeline of mitigation actions

> Number mitigations MIT001, MIT002, … within this incident. A mitigation is NOT a fix.

| Time | Action | MIT | Evidence / result |
|---|---|---|---|
| | | | |

## Snapshot bundle

<Logs, config dumps, system state captured at incident time. Path: <artifacts>.>

## Mitigations applied

- **MIT001** — <what was done, what improved>

## Verification (stability, not root-cause fix)

- [ ] Health checks passing: <evidence>
- [ ] User workflows unblocked: <evidence>
- [ ] No fresh errors in the watch window: <evidence>

## Direction and learning context

- **North Star SHA-256:** `<current-sha256>`
- **Learning context:** `<CONSULTED | DEFERRED>`
- **Rule IDs:** `<RULE:<card-slug>,... | NONE>`
- **Source IDs:** `<PMxxx/LEARNxxx,... | NONE>`
- **Deferral reason:** `<substantive emergency reason | NOT-REQUIRED>`

Read current `NORTHSTAR.md` before mitigation. Follow only immediately relevant
`LEARNINGS.md → card → source` cues when safe. `DEFERRED` is allowed to avoid delaying
stabilization, but Stage 21 cannot select this incident until the fields are resolved to
`CONSULTED` and the current North Star hash is recorded.

## Hand-off to Post-Mortem

- **Snapshot bundle:** <path>
- **Affected operations:** <what stopped working, for how long>
- **Cause-class hypothesis (preliminary):** <class>
- **Suggested PM title:** <specific, actionable>
