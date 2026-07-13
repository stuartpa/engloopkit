# EngLoopKit Northstar

- **Created:** 2026-07-06
- **Last revised:** 2026-07-10
- **Owner:** EngLoopKit maintainers
- **Status:** LIVING
- **Audience:** engineers building and operating software that must improve for years

> One living authority evolved from EngLoopKit's original gathered build brief. Git
> retains every prior revision.

## Purpose and audience

Build **EngLoopKit**: a Spec Kit **bundle** that encodes a repeatable way to build software
that lives for years and improves through incidents/post-mortems/repairs — while being
**deliberately efficient with tokens**. Use it to develop SEK, then dogfood it on itself.

For real engineers who **pay for tokens**. The bet: drive quality with deterministic
engines (SEK/Z3 exploration, tests, architecture checks) instead of LLM loops, and use
lightweight flows (tinyspec) for small work — so tokens are spent only on irreducible
judgement.

## Enduring outcomes and non-negotiable invariants

- Explicit loops retain traceable direction, architecture, evidence, learning, and repair.
- Deterministic engines decide every mechanically verifiable gate; missing or ambiguous
  evidence fails closed.
- Systems behavior comes from explicit generic contracts, never workload-shape heuristics.
- Final-vocabulary records are monotonic; root Northstar/Learnings stay living; numbered
  process memory lives only under tracked `.engloop/`.
- Components remain domain-free and dependencies point from the vertical to components.
- EngLoopKit remains on the 1.x maturity runway until a maintainer explicitly
  authorizes 2.x; internal workflow-generation labels never imply a package major.

## Current direction

Deliver [REFACT001](.engloop/refactors/REFACT001_ordered-engloop-v2.md) through
[SPEC001](specs/SPEC001-ordered-engloop-v2/spec.md): a proven scaffold runway,
architecture-governed code, SEK behavioral proof, delete-before-unit-test disposition,
computed readiness, strict operations, and loss-aware learning retrieval.

## More of

- Deterministic validators, SEK/Z3 positive-and-negative conformance, and reproducible commands.
- Thin executable learning slices followed by governance and residue removal.
- Progressive learning retrieval and standalone roots with explicit local configuration.

## Less of / stop

- Stop narrated readiness, unit tests that preserve residue, hidden fallbacks, numbered
  direction snapshots, and routine direction churn.
- Spend fewer tokens where a compiler, runner, solver, or architecture check can decide.

## Boundaries

- EngLoopKit owns its bundle, extension, contracts, reusable components, and verified core.
- Spec Kit owns its host/workflow; Architecture Guard, tinyspec, and SEK remain composed
  capabilities with their own contracts.
- Consumer application semantics stay in each consumer's model, configuration, and tests.

## Unresolved direction questions

- Which portable SEK release and measured gate budgets should v2 ratify first?

## Evidence for this revision

- TTHP/workshop dogfooding exposed lexical-order, living-direction, and focused-root needs.
- PM001–PM004 bind computed readiness, method by class, behavior-level modelling, and
  model-derived rejection.
- **Spec Kit** (github/spec-kit): bundles compose existing components via `bundle.yml`;
  new commands come from **extensions** (`extension.yml` + `commands/*.md`, namespaced
  `speckit.<id>.*`). `specify bundle validate|build`.
- **Loop Engineering** (Addy Osmani, June 2026; Boris Cherny; Peter Steinberger): design
  the *system* that prompts the agent. Five components — **Trigger · Goal · Actions ·
  Verification · Memory**. Cycle Observe→Reason→Act→Evaluate. Andrew Ng's nested loops
  (minutes/hours/days). Captured in [docs/loop-engineering.md](docs/loop-engineering.md).
- **architecture-guard** (DyanGalih): architecture constitutions, drift → refactor tasks;
  used for the post-scaffold architecture stage.
- **tinyspec** (Quratulain-bilal): single-file flow for small repair items.
- **SEK** (stuartpa/sek): Z3 model explorer + xUnit test generator; powers the
  Verification stage (model → explore → generate) without spending tokens per test.
- Follow Spec Kit bundle/extension folder norms; package/release per Spec Kit.
- Every numbered artifact has a **prefix + monotonic number + brief title**.
- **Token efficiency** is woven throughout.
- When dogfooded on itself: **same dev platform as SEK** (.NET 8 / C# / xUnit, no new deps).
- Process memory uses `.engloop/`; bundle verification includes conformance/artifact
  evidence; model and exploration remain separate responsibilities in one feedback loop.
