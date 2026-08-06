# Utility evidence

This directory stores compact governed evidence that is neither a lifecycle transition
nor a numbered domain document.

Token-efficiency agents own:

- `token-efficiency-analysis-<session-or-date>.json` — Agent 30 read-only observations,
  data availability, findings, ranked repair IDs, estimates/proxies, and confidence;
- `token-efficiency-implementation-<revision>.json` — Agent 31 approved repair IDs,
  changed files, customization/toolchain decisions, focused validation, unavailable
  prerequisites, and residual risks.

Do not store raw conversations, complete terminal buffers, credentials, secrets, or
production logs here. Full validation output belongs under ignored
`.engloop/out/token-efficiency/`; evidence JSON references those paths with bounded
diagnostics.