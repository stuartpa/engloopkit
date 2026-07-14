# Incident IN005: stale testhost locks ELK test assemblies

- **Started:** 2026-07-13
- **Status:** STABILIZED — no repair closure claim
- **Cause class:** test-process-lifecycle / validation reliability
- **Affected:** local direct-test and coverage reruns in the ELK working tree

## Symptom

A completed or aborted .NET test run left an `testhost.exe` process holding copied ELK
assemblies under `tests/EngLoopKit.Tests/bin/Debug/net8.0/`. Subsequent builds and
coverage runs failed while copying `engloopkit.dll` and component assemblies, producing
misleading build/coverage failure evidence.

## Scope and impact

- No published artifact was overwritten.
- No consumer repository was modified.
- No overlay archive, hook, or Git state was accepted as a result of the failure.
- Release/readiness remains blocked until normal deterministic gates pass again.

## Timeline

| Time (local) | Event | Evidence |
|---|---|---|
| 2026-07-13 | Coverage/test reruns failed copying ELK assemblies because `testhost` held files. | Build output named `testhost` PID 9260, then PID 3264. |
| 2026-07-13 | Verified the processes were ELK test hosts and terminated only those processes. | `Get-CimInstance Win32_Process` command line matched `EngLoopKit`; targeted `taskkill /T /F`. |
| 2026-07-13 | Reran direct suite after cleanup; tests completed cleanly. | Direct test output: 163 passing tests at stabilization checkpoint. |

## Mitigations applied

- **MIT001** — Stop only the identified ELK-owned stale `testhost.exe` process tree after
  confirming its command line references the ELK test project.
- **MIT002** — Remove only accidental transient diagnostic output
  `System.Xml.XmlDocument/`; it is not source, overlay state, or release evidence.
- **MIT003** — Continue to treat readiness/package results as invalid until a fresh green
  deterministic run produces replacement evidence.

## Verification

- [x] Selected incident-stage entry gate passed for the ELK root.
- [x] No ELK testhost process remained after targeted termination.
- [x] A subsequent direct test run completed green at the stabilization checkpoint.
- [ ] A later post-mortem may determine whether test process lifecycle needs a durable
  tooling change. This incident does not claim that repair is complete.

## Handoff

No permanent repair is authorized by this incident record. If recurrence demonstrates a
systemic lifecycle defect, select this stabilized incident explicitly for Stage 21
post-mortem analysis.
