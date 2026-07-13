# Learnings Pyramid

This is the working prototype behind
[`REFACT001`](../refactors/REFACT001_ordered-engloop-v2.md). It compresses EngLoopKit's
accepted post-mortem learnings without replacing them:

```text
post-mortem PMxxx/LEARNxxx source
    → living subject card
        → root LEARNINGS.md recall/index page
            → on-demand agent instruction
```

## Source set

The authoritative source sequence is every accepted `LEARNxxx` in the post-mortems
listed by [`postmortems/INDEX.md`](../postmortems/INDEX.md), addressed globally as
`PMxxx/LEARNxxx`. Source post-mortems remain immutable historical analysis; cards and the
one-page index are living condensations whose changes are retained by Git.

## Card contract

Each file in `cards/`:

- focuses on one subject;
- states one terse recall cue;
- explains when the subject applies;
- compresses related source learnings into a usable principle;
- gives concrete decision checks;
- links every contributing `PMxxx/LEARNxxx` source;
- exposes conflicts or supersession instead of silently reconciling them.

Every accepted source learning must be represented by at least one card. Every card
must be linked from root [`LEARNINGS.md`](../../LEARNINGS.md). The root page stays
short: no more than **500 words and 60 nonblank lines**. Both limits must pass; detail
belongs on cards and evidence belongs in post-mortems.

## Current coverage

| Source sequence | Accepted learnings | Cards |
|---|---:|---|
| PM001 | 3 | readiness-is-a-gate |
| PM002 | 3 | verification-follows-artifact-class; model-observable-behavior |
| PM003 | 2 | model-observable-behavior |
| PM004 | 3 | adequate-models-prove-rejection |

Current source-learning coverage: **11 of 11**. The root index is **172 words and 24
nonblank lines**, below both limits. This is prototype evidence, not yet the automated
Stage 31 gate; v2 must make completeness, links, size, and retrieval checks
deterministic.