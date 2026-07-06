# SEED001: EngLoopKit — the engineering loop as a Spec Kit bundle

- **Created:** 2026-07-06
- **Author:** stuartpa
- **Origin:** new build
- **Status:** CONSUMED (bridging code complete; stages 0–5 complete)
- **Stage:** 0 · Seed → fed the bridging specify loop (Stage 1)

> The gathering document for building EngLoopKit: everything needed to turn one
> engineer's software-engineering methodology into a reusable, token-efficient Spec Kit
> bundle. Filed retroactively so EngLoopKit itself looks built-by-EngLoopKit.

## The ask

Build **EngLoopKit**: a Spec Kit **bundle** that encodes a repeatable way to build software
that lives for years and improves through incidents/post-mortems/repairs — while being
**deliberately efficient with tokens**. Use it to develop SEK, then dogfood it on itself.

## Why / for whom

For real engineers who **pay for tokens**. The bet: drive quality with deterministic
engines (SEK/Z3 exploration, tests, architecture checks) instead of LLM loops, and use
lightweight flows (tinyspec) for small work — so tokens are spent only on irreducible
judgement.

## Prior art / gathered material

- **Spec Kit** (github/spec-kit): bundles compose existing components via `bundle.yml`;
  new commands come from **extensions** (`extension.yml` + `commands/*.md`, namespaced
  `speckit.<id>.*`). `specify bundle validate|build`.
- **Loop Engineering** (Addy Osmani, June 2026; Boris Cherny; Peter Steinberger): design
  the *system* that prompts the agent. Five components — **Trigger · Goal · Actions ·
  Verification · Memory**. Cycle Observe→Reason→Act→Evaluate. Andrew Ng's nested loops
  (minutes/hours/days). Captured in [../../docs/loop-engineering.md](../../docs/loop-engineering.md).
- **architecture-guard** (DyanGalih): architecture constitutions, drift → refactor tasks;
  used for the post-bridging architecture stage.
- **tinyspec** (Quratulain-bilal): single-file flow for small repair items.
- **SEK** (stuartpa/sek): Z3 model explorer + xUnit test generator; powers the
  Verification stage (model → explore → generate) without spending tokens per test.

## Constraints

- Follow Spec Kit bundle/extension folder norms; package/release per Spec Kit.
- Every artifact has a **prefix + monotonic number + brief title** (generic; no K8s/Helm).
- **Token efficiency** woven throughout.
- When dogfooded on itself: **same dev platform as SEK** (.NET 8 / C# / xUnit, no new deps).

## Open questions (resolved during the build)

- Artifact root when `docs/` is a published site → configurable root; use `engloop/`.
- "Code coverage" for a bundle with no runtime code → **conformance/artifact coverage**.
- Model/explore = one or two commands → two commands, one Verification loop.

## Ready-for-specify checklist

- [x] What is being built is clear (a Spec Kit bundle, EngLoopKit)
- [x] Why and for whom is clear (token-efficient engineering loop for real engineers)
- [x] Constraints captured (Spec Kit norms, numbering, token efficiency, SEK platform)
