# The Engineering Loop

This is the full narrative of the methodology EngLoopKit encodes: how a thing goes
from a rough idea to a hardened, model-checked, continuously-operated product that
lives for years. Each stage is expressed as a Loop Engineering loop (Trigger · Goal ·
Actions · Verification · Memory). Command details live in the extension's
`commands/` files; this document is the map.

---

## Stage 0 — Seed

**Command:** `/speckit.engloopkit.seed`

Before any specifying, gather *everything* you can find about the thing to build into a
single **SEED** document: the ask, prior art, constraints, links, code snippets,
sketches, screenshots, half-formed requirements. A SEED is not a spec — it is the raw
material a spec is distilled from, and it is the durable **Memory** that makes the
first `specify` run good instead of vague.

- **Trigger:** a new thing to build (or a `REF` from the evolution loop).
- **Goal:** one `SEEDxxx_<slug>.md` that a `specify` loop can start from with no other context.
- **Verification:** the SEED answers "what, why, for whom, and within what constraints" without hand-waving.
- **Memory:** `docs/seeds/SEEDxxx_<slug>.md`.

## Stage 1 — Bridge (bridging code)

**Commands:** core `/speckit.specify` → `/speckit.plan` → `/speckit.tasks` → `/speckit.implement`

Run the standard Spec-Driven Development loop on the SEED to get an initial **working**
implementation. This is deliberately *bridging code*: correct enough to run and to
learn the shape of the problem from, not yet its final architected form. Getting to
running code fast is the point — it is what the architecture stage will be derived from.

- **Trigger:** a `SEED`.
- **Goal:** working bridging code that satisfies the SEED's core scenarios.
- **Verification:** the code runs; the SEED's headline scenarios work end-to-end.
- **Memory:** `SPxxx` spec/plan/tasks under `specs/`.

## Stage 2 — Architect

**Command:** `/speckit.engloopkit.architect` (orchestrates architecture-guard)

Now use the bridging code as evidence to define the **long-lived architecture** —
boundaries, ownership, contracts, the rules the system will honor for years. This is
where [architecture-guard](https://github.com/DyanGalih/spec-kit-architecture-guard)
enters: `init-brownfield` maps the current code, then the architecture workflow turns
implicit structure into explicit, reviewable **ARC** constitutions. From here on,
architecture-guard governs every loop and surfaces drift as refactor tasks.

- **Trigger:** bridging code exists and runs.
- **Goal:** an explicit, governed architecture (`ARCxxx` + architecture-guard constitutions).
- **Verification:** `architecture-review` runs clean or with only accepted, tracked exceptions.
- **Memory:** `docs/architecture/ARCxxx_<slug>.md`.

## Stage 3 — Refactor to final form

**Commands:** core governed `/speckit.specify` loop (via architecture-guard's `governed-spec`)

Refactor the bridging code into its final form *against* the architecture. This is the
same `specify → plan → tasks → implement` loop, but every artifact is validated against
the ARC constitutions before code is written, and YAGNI/"lazy senior developer"
pragmatism keeps it from over-engineering. This is the stage the operations loop and
the evolution loop both return to.

- **Trigger:** an approved architecture, or an incoming Repair Item / refactor SEED.
- **Goal:** code that satisfies the spec *and* honors the architecture.
- **Verification:** tests pass **and** `architecture-verify` confirms the final work matches approved tasks.
- **Memory:** `SPxxx`.

## Stage 4 — Model

**Command:** `/speckit.engloopkit.model`

Build a **SEK** model of the implementation: its state fields, actions, and invariants,
expressed as an explorable state space. The model is *structural* — it says what states
exist and how actions move between them — and is the substrate the exploration stage
runs against.

- **Trigger:** final-form code (or changed code from a repair).
- **Goal:** a SEK model that faithfully abstracts the implementation's behavior.
- **Verification:** the model builds and its sanity explorations match known-good behavior.
- **Memory:** `docs/models/MDLxxx_<slug>.md` + model source.

### Why model and explore are two commands, one loop

A reasonable question is whether "define the model" and "define the CORD explorations"
are one stage or two. EngLoopKit makes them **two commands but one loop**, for a
concrete reason:

- **`model` (MDL)** answers a *structural* question — *what is the state space?* Its
  Goal is fidelity to the implementation. It changes rarely (only when the
  implementation's shape changes).
- **`explore` (CRD)** answers a *behavioral/coverage* question — *which scenarios,
  explored to what depth, generate the tests that cover the code?* Its Goal is coverage.
  It changes often as gaps are found.

They have different Goals and different Verification, so conflating them would blur two
distinct exit conditions. But they iterate **together** as the single coverage-driven
Verification loop: you rarely change the model without re-exploring, and you extend
explorations against a fixed model until coverage is met.

## Stage 5 — Explore & Coverage (the Verification loop)

**Commands:** `/speckit.engloopkit.explore`, `/speckit.engloopkit.coverage`

Author **CORD** models describing scenarios and constraints, run **Z3 exploration**
over the SEK model to enumerate behaviors, and **generate test cases** from the
explored paths. Then measure coverage and close the loop: drive **line/branch coverage
to 95%+ first**, then add models aimed at **functional completeness**. The emphasis is
tests that give **very good functional coverage but execute quickly** — and are
generated by the solver, not hand-written by an LLM.

- **Trigger:** a model (`MDL`) and a coverage gap (`COV`).
- **Goal:** 95%+ line/branch coverage, then functional coverage; all generated tests green and fast.
- **Actions:** write/extend CORD (`CRD`), Z3-explore, generate tests, run coverage.
- **Verification:** coverage thresholds met; test suite green and within a time budget.
- **Memory:** `docs/cord/CRDxxx_<slug>.md`, `docs/coverage/COVxxx_<slug>.md`.

At the end of Stage 5 the product is: implemented in final form, architecturally
governed, modeled, explored, and covered by fast passing tests. It is ready to operate.

## Stage 6 — Operate (the Operations loop)

**Commands:** `/speckit.engloopkit.incident`, `/speckit.engloopkit.postmortem`, `/speckit.engloopkit.repair`

Once operating, every issue becomes an **incident**. The order of priorities is
strict:

1. **Incident** — mitigate as fast as possible. Apply **Mitigations (MIT)** to restore
   service. **Do not commit a permanent fix**; the Golden Rule says a patch during an
   incident is a Mitigation, not a fix. Log everything in `INxxx`.
2. **Post-mortem** — once a *set* of incidents is stable, run a **5-whys** analysis
   across them, producing class-level **Learnings (LRN)** and concrete **Repair Items
   (RPI)** in `PMxxx`.
3. **Repair** — route each RPI: small ones to **tinyspec**, larger ones to full
   **specify**. Either way you re-enter the Delivery loop at Stage 3, **and update the
   SEK model, add CORD explorations, re-explore, and regenerate tests** so the fix is
   both shipped and permanently covered.

- **Trigger:** a bug reported by a user or surfaced by monitoring.
- **Goal:** system stable (incident), then recurrence made mechanically impossible (repair).
- **Verification:** incident closed when stable; repair done when it passes tests in the target environment.
- **Memory:** `INxxx`, `PMxxx` (with `LRN`/`RPI`).

## Stage 7 — Evolve (the Evolution loop)

**Command:** `/speckit.engloopkit.refactor-scan`

Code decays. Run this **periodically** — the natural cadence is month-end, when there
are Copilot tokens to "use or lose". After a month of incident/postmortem/repair loops,
the codebase has accumulated pressure. The refactor scan walks a **refactoring decision
tree**, picks the single highest-value refactor (`REF`), and emits a **SEED** — which
drops you back at Stage 0 for a clean Delivery loop.

- **Trigger:** a schedule (e.g. month-end) and available token budget.
- **Goal:** the one next refactor that most improves long-term health, expressed as a SEED.
- **Verification:** the chosen refactor is justified against the decision tree and scoped as a SEED.
- **Memory:** `docs/refactors/REFxxx_<slug>.md` → `SEEDxxx`.

---

## The whole loop, once more

```
SEED ─▶ specify/plan/tasks/implement ─▶ architect ─▶ refactor-to-final
   ▲                                                        │
   │                                                        ▼
refactor-scan (monthly)                          model ─▶ explore ⇄ coverage
   ▲                                                        │  (95%+, then functional)
   │                                                        ▼
   └──── repair ◀── postmortem ◀── incident ◀──────── OPERATE
```
