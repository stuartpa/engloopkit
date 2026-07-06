# EngLoopKit

> The engineering loop, as a Spec Kit bundle.

EngLoopKit is a [Spec Kit](https://github.com/github/spec-kit) **bundle** that
encodes one opinionated, repeatable way to build software that has to *live for
years* — improving in quality and features over time and responding to bugs and
incidents — while being **deliberately efficient with tokens**.

It is *Loop Engineering* applied to the whole software lifecycle: you don't hand-prompt
each step, you set up **loops** — each with a **Trigger**, a **Goal**, a set of
**Actions**, a **Verification** gate, and durable **Memory** — and let the loop
drive the agent from a rough idea to a hardened, model-checked, operated product.

EngLoopKit composes three things:

| Component | Role in the loop |
|---|---|
| **`engloopkit`** extension (this repo) | The stage commands that don't exist in core Spec Kit: `seed`, `architect`, `model`, `explore`, `coverage`, `incident`, `postmortem`, `repair`, `refactor-scan`. |
| **[architecture-guard](https://github.com/DyanGalih/spec-kit-architecture-guard)** | Defines and *governs* the long-lived architecture after bridging code; keeps every later loop honest against it (YAGNI, drift detection, refactor tasks). |
| **[tinyspec](https://github.com/Quratulain-bilal/spec-kit-tinyspec)** | A single-file lightweight flow for small repair items that don't warrant the full `specify → plan → tasks → implement` loop. |

---

## Why Loop Engineering (and why tokens matter)

*Loop engineering* — coined June 2026 by Addy Osmani, drawing on Boris Cherny
(Anthropic) and Peter Steinberger — is **the practice of designing the system that
prompts your agent, instead of typing every prompt yourself.** Every well-designed
loop has five parts:

1. **Trigger** — what starts the loop (a SEED, a failing test, an incident, a monthly token budget).
2. **Goal** — a *verifiable* end state ("95%+ line coverage", "all P1 incidents mitigated", "zero architecture-drift violations").
3. **Actions** — the tools the loop may use (Spec Kit commands, SEK/Z3 exploration, the test runner, git).
4. **Verification** — how the loop knows it's done (tests, coverage thresholds, Z3 exhaustiveness, CI, architecture-verify).
5. **Memory** — what persists between iterations (numbered SEED/spec/model/incident/postmortem docs — see [Standards](docs/standards.md)).

EngLoopKit's central bet on **token efficiency**: the loops that determine *quality*
should be driven by **deterministic engines, not LLMs**. SEK explores CORD models
with **Z3** to generate high-coverage test cases without spending a token per test;
`tinyspec` collapses small changes from 30+ files to one; architecture-guard's
"lazy senior developer" YAGNI stance prevents over-engineered plans. LLM tokens are
spent where judgement is actually needed (specs, root-cause analysis, refactor
decisions) — not on work a solver or a compiler can verify for free.
See [Token Efficiency](docs/token-efficiency.md).

---

## The nested loops

EngLoopKit is structured as Andrew Ng's three nested clocks — an inner agentic loop
(minutes), a developer feedback loop (hours), and an external feedback loop (days) —
plus a slow **evolution** loop (a month) for refactoring.

```
                         ┌──────────────── EVOLUTION LOOP (monthly) ────────────────┐
                         │  refactor-scan → SEED → specify loop (back into Delivery) │
                         └───────────────────────────▲──────────────────────────────┘
                                                      │
   ┌──────────────────── DELIVERY LOOP (hours) ───────┴──────────────────────────┐
   │  0. seed        gather everything into one SEED doc                          │
   │  1. bridge      specify → plan → tasks → implement   (bridging code)         │
   │  2. architect   define the long-lived architecture (architecture-guard)      │
   │  3. refactor     governed specify loop → bridging code becomes final form    │
   │  4. model       build the SEK model of the implementation                    │
   │  5. explore     CORD models + Z3 exploration → generated test cases          │  ◄─ inner
   │     coverage    measure coverage; drive 95%+ line, then functional           │     agentic
   └───────────────────────────────────▲──────────────────────────────────────────┘     loop
                                        │ (bugs found by users / monitoring)             (minutes)
   ┌──────────────── OPERATIONS LOOP (days) ─┴───────────────────────────────────┐
   │  incident     mitigate fast (no permanent fix), log everything               │
   │  postmortem   5-whys over a SET of incidents → Learnings + Repair Items       │
   │  repair       each Repair Item → tinyspec (small) or specify (large) ────────┼─► back to step 3
   └──────────────────────────────────────────────────────────────────────────────┘
```

Full narrative: [The Engineering Loop](docs/engineering-loop.md).

---

## Install

EngLoopKit references two community extensions that are not in the default catalog,
so add their catalogs (or install from release archives) before installing the bundle.

```bash
# 1. Make the companion extensions resolvable
specify extension add architecture-guard --from \
  https://github.com/DyanGalih/spec-kit-architecture-guard/archive/refs/tags/v1.11.0.zip
specify extension add tinyspec --from \
  https://github.com/Quratulain-bilal/spec-kit-tinyspec/archive/refs/tags/v1.0.0.zip

# 2. Validate and build this bundle
specify bundle validate --path ./EngLoopKit
specify bundle build --path ./EngLoopKit        # produces engloopkit-1.0.0.zip

# 3. Install the bundle into your project
specify bundle install ./engloopkit-1.0.0.zip
```

To develop the `engloopkit` extension on its own:

```bash
specify extension add --dev ./EngLoopKit/extensions/engloopkit
```

Or install the released `engloopkit` extension directly from this repo's catalog:

```bash
specify extension add engloopkit --from \
  https://github.com/stuartpa/engloopkit/releases/download/v1.0.0/engloopkit-extension-1.0.0.zip
```

---

## The commands

All commands are namespaced `speckit.engloopkit.*`. Each is documented as a Trigger /
Goal / Actions / Verification / Memory loop.

| Stage | Command | What it does |
|---|---|---|
| 0 · Seed | `/speckit.engloopkit.seed` | Gather everything known about the thing to build into one numbered **SEED** doc. |
| 1 · Bridge | *core* `/speckit.specify` → `.plan` → `.tasks` → `.implement` | Turn the SEED into working bridging code. |
| 2 · Architect | `/speckit.engloopkit.architect` | Derive the long-lived architecture from the bridging code via architecture-guard; produce an **ARC** constitution. |
| 3 · Refactor to final | *core* governed `specify` loop | Refactor bridging code into its final, architecture-honoring form. |
| 4 · Model | `/speckit.engloopkit.model` | Build the **SEK** model (MDL) of the implementation's state space. |
| 5 · Explore | `/speckit.engloopkit.explore` | Author **CORD** models (CRD), run Z3 exploration, generate test cases. |
| 5 · Coverage | `/speckit.engloopkit.coverage` | Measure coverage (**COV**), close the SEK↔coverage loop to 95%+ line, then functional. |
| 6 · Incident | `/speckit.engloopkit.incident` | Live mitigation, no permanent fix; log an **IN** doc with **MIT** actions. |
| 6 · Post-mortem | `/speckit.engloopkit.postmortem` | 5-whys over a set of incidents → **PM** with **LRN** learnings and **RPI** repair items. |
| 6 · Repair | `/speckit.engloopkit.repair` | Route each **RPI** to `tinyspec` (small) or `specify` (large), re-entering the Delivery loop. |
| 7 · Refactor scan | `/speckit.engloopkit.refactor-scan` | Monthly: walk a refactoring decision tree, pick the next refactor (**REF**), emit a SEED. |

---

## Document standards

Every artifact EngLoopKit produces has a **prefix + monotonically increasing number +
short title** — `SEED042_photo-albums.md`, `PM007_config-drift.md`. Numbers are the
loop's **Memory**: stable, greppable, and cross-referenceable. Prefixes and the
counter registry are defined in [docs/standards.md](docs/standards.md) and
[docs/numbering-registry.md](docs/numbering-registry.md).

---

## Using EngLoopKit on SEK (dogfood)

SEK has completed its bridging code stage. The next step is `architect`. A worked
walkthrough of taking SEK through the whole loop is in
[examples/sek-walkthrough.md](examples/sek-walkthrough.md).

## License

MIT — see [LICENSE](LICENSE).
