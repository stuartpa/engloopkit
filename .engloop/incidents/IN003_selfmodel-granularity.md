# Incident IN003: the gate's per-module self-model rule is ambiguous for a pipeline vertical

- **Started:** 2026-07-06 (surfaced driving SEK's vertical toward the v1.4.0 gate)
- **Reported by:** SEK maintainer + the SEK build agent, applying the gate to SEK's compiler/engine
- **Affected:** the v1.4.0 Readiness Gate's **vertical** criterion ("verified by a SEK self-model —
  MODEL + CORD + generated conformance")
- **Status:** RESOLVED
- **Cause-class:** validation-gap (gate granularity under-specified)

## Symptom

v1.4.0 (PM002) says every **vertical** module is verified by a SEK self-model. Applied literally
to SEK, that reads as "each vertical *assembly* (`Sek.Cord`, `Sek.Core`, `Sek.Engine`, `Sek.Cli`)
needs its own `MODEL`+`CORD`." But SEK's vertical is a **single pipeline** — `parse Cord → semantic
analysis → build IR → explore → generate → conform` — whose assemblies are **stages**, not
independent products. Writing a bespoke SEK model of "the lexer" or "the IR renderer" would be a
tautological, low-value model — exactly the **theatre** PM002 itself warned against. Meanwhile a
single, genuine self-model of the tool's **observable behavior** (SEK modelling SEK's CLI workflow,
whose SUT drives the real `sek` CLI) already exercises the *entire* pipeline end-to-end, and it even
found a real bug (SEK IN002).

So the gate is ambiguous: taken one way it demands theatre; taken the other it is satisfied by one
end-to-end self-model. It needs to say which.

## Mitigations applied

- **MIT001** — Did **not** author bespoke per-assembly models of pure pipeline stages (would be
  theatre). Built one genuine end-to-end self-model (SEK's CLI workflow) that exercises the whole
  vertical pipeline, kept the ≥95%-coverage-per-module requirement, and filed this incident to
  clarify the gate rather than game it.

## Verification

- [x] No theatre models were created. Evidence: SEK has one behavior self-model (SelfHost) that
  drives the real pipeline; internal stages are covered by ≥95% coverage + transitively by that
  self-model and the sample conformance loops.

## Hand-off to Post-Mortem

- **Cause-class hypothesis:** validation-gap — the gate specifies *that* the vertical is
  self-modelled but not at *what granularity* (per-assembly vs per-observable-behavior).
- **Suggested PM title:** "Self-model criterion is behavior-level: one representative end-to-end
  self-model + conformance loops validates the vertical pipeline; ≥95% coverage stays per-module."
