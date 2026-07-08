# EngLoopKit Document Standards

**Status:** Ratified 2026-07-06
**Authority:** EngLoopKit Constitution — *documents are the loop's Memory*

Every document EngLoopKit produces is a unit of **Memory** in a Loop Engineering
sense: it is what persists between iterations so the agent (and the human) never
re-derive context they already paid for. To be usable as memory, every document has:

> **`<PREFIX><NNN>_<short-title>.md`**
> a fixed **prefix**, a **monotonically increasing number**, and a **brief title**.

Examples: `SEED042_photo-albums.md`, `MDL003_queue-state.md`, `PM007_config-drift.md`.

This document defines the canonical prefixes, where each artifact lives, and the
workflow that connects them. It is **generic** — it prescribes no specific cloud,
orchestrator, package manager, or deployment tool. Substitute your own stack wherever
"build output", "artifact", and "target environment" appear.

---

## Artifact root

All paths below are written relative to an **artifact root**, written `<ARTIFACT_ROOT>/`.

- **Default:** `docs/`. In a project with no published documentation site, artifacts
  live under `docs/seeds/`, `docs/models/`, and so on.
- **Override:** a project whose `docs/` is already a *published documentation site*
  (e.g. a DocFX/MkDocs build that globs `**/*.md`) should set the artifact root to a
  dedicated top-level folder — `engloop/` is the recommended name — so process
  artifacts never leak into the published site. Then `<ARTIFACT_ROOT>/seeds/` means
  `engloop/seeds/`, etc.

Record the chosen root once in the project's local `standards.md` (a copy of this file
with the override noted). Every command resolves `<ARTIFACT_ROOT>` from that local file;
if absent, it defaults to `docs/`.

---

## Numbering rules

1. **Prefixes are fixed** (below). Do not invent ad-hoc prefixes; propose additions
   via a spec if a genuinely new artifact class appears.
2. **Numbers are monotonic and never reused** within a prefix. Deleting a document
   does not free its number.
3. **Numbers are zero-padded to three digits** (`001`) until a prefix exceeds 999.
4. **Increment happens in the registry first** ([numbering-registry.md](numbering-registry.md)),
   then the file is created. This makes the registry the single source of truth and
   avoids two loops racing to the same number.
5. Some prefixes are **global** (one counter across the whole project); some are
   **local** (reset inside a parent document). The table marks which.

---

## Prefixes

### Delivery loop

#### SEED — `SEED001`, `SEED002`, … *(global)*

A gathering document. Everything currently known about a thing to be built —
requirements, prior art, constraints, links, snippets — collected into one place so a
`specify` loop can start from a single source of truth.

- Path: `<ARTIFACT_ROOT>/seeds/SEEDxxx_<slug>.md`
- Produced by `/speckit.engloopkit.seed` and by `/speckit.engloopkit.refactor-scan`.
- A SEED is the **Trigger** for a Delivery loop.

#### SP — `SP001`, `SP002`, … *(global)*

A specification produced by `/speckit.specify` (optionally via architecture-guard's
governed flow). One feature or repair per spec.

- Path: Spec Kit's own `specs/SPxxx-<slug>/` (spec.md, plan.md, tasks.md).
- The **Goal** and plan of a Delivery loop.

#### BRG — `BRG001`, `BRG002`, … *(global)*

A **bridging-stage record**: implementation-state, parity, or audit notes produced while
getting the bridging code working (Stage 1), before the architecture stage formalizes
things. Optional — use it when the bridging stage generates status/audit docs worth
keeping as Memory rather than discarding.

- Path: `<ARTIFACT_ROOT>/bridging/BRGxxx_<slug>.md`
- Produced during Stage 1 (the bridging specify loop); not tied to a single command.

#### ARC — `ARC001`, `ARC002`, … *(global)*

A long-lived architecture decision or constitution article — boundaries, ownership,
contracts. Derived from bridging code, then **governed** on every later loop by
architecture-guard.

- Path: `<ARTIFACT_ROOT>/architecture/ARCxxx_<slug>.md` (architecture-guard's constitutions may
  also live in its own location; ARC docs are the human-readable decisions).
- Produced by `/speckit.engloopkit.architect`.

### Verification loop (SEK)

#### MDL — `MDL001`, `MDL002`, … *(global)*

A SEK **model** definition: the state fields, actions, and invariants that describe
the implementation's behavior as an explorable state space.

- Path: `<ARTIFACT_ROOT>/models/MDLxxx_<slug>.md` (the human description) alongside the SEK model
  source it points to.
- Produced by `/speckit.engloopkit.model`.

#### CRD — `CRD001`, `CRD002`, … *(global)*

A CORD exploration: the scenarios/constraints explored against a model to generate
test cases and the coverage goal each targets.

- Path: `<ARTIFACT_ROOT>/cord/CRDxxx_<slug>.md` alongside its `.cord` script.
- Produced by `/speckit.engloopkit.explore`.

#### COV — `COV001`, `COV002`, … *(global)*

A coverage report closing the SEK↔coverage loop: line/branch coverage achieved, gaps,
and which CRD explorations to extend next.

- Path: `<ARTIFACT_ROOT>/coverage/COVxxx_<slug>.md`
- Produced by `/speckit.engloopkit.coverage`.

### Operations loop

#### IN — `IN001`, `IN002`, … *(global)*

An unplanned disruption that required intervention. Contains symptom, timeline, and
mitigations — **not** repair items (those belong to the post-mortem). Closed when the
system is stable, **not** when the root cause is fixed.

- Path: `<ARTIFACT_ROOT>/incidents/INxxx_<slug>.md`
- Produced by `/speckit.engloopkit.incident`.

#### MIT — `MIT001`, `MIT002`, … *(local to an incident)*

A temporary stabilization applied during an incident (restart, roll back, fail over,
scale up). A mitigation is **not a fix**. Recorded in the incident timeline.

- Numbered sequentially within an incident.

#### PM — `PM001`, `PM002`, … *(global)*

A structured analysis of **one or more** incidents, written after the system is
stable. Contains timeline, 5-whys, ONE-AND-DONE analysis, Learnings, and Repair Items.

- Path: `<ARTIFACT_ROOT>/postmortems/PMxxx_<slug>.md`; indexed in `<ARTIFACT_ROOT>/postmortems/INDEX.md`.
- Produced by `/speckit.engloopkit.postmortem`.

#### LRN — `LRN001`, `LRN002`, … *(local to a post-mortem)*

A class-level structural insight from a post-mortem. Written about the *class* of
failure ("config can silently diverge from source"), never the instance.

#### RPI — `RPI001`, `RPI002`, … *(local to a post-mortem)*

A concrete, shippable fix that prevents a class of failure from recurring — the
primary output of a post-mortem. Must be specific enough to hand to `/speckit.specify`
or `/speckit.tinyspec`. Belongs in source, not as a live patch. Not done until it
passes verification in the target environment.

### Evolution loop

#### REF — `REF001`, `REF002`, … *(global)*

A refactor decision from the monthly refactor scan: the strategy chosen from the
decision tree, the rationale, and the SEED it emits.

- Path: `<ARTIFACT_ROOT>/refactors/REFxxx_<slug>.md`
- Produced by `/speckit.engloopkit.refactor-scan`.

---

## The Golden Rule

> **A patch applied during an incident is a Mitigation (MIT), not a fix.**
> A fix is not done until it is committed to source, built into release artifacts,
> deployed to the target environment, and has passed all verification tests.

---

## Loop → artifact map

| Loop | Trigger | Produces | Verification |
|---|---|---|---|
| Delivery | `SEED` | `SP`, `BRG`, code, `ARC` | tests pass, architecture-verify clean |
| Verification | new/changed code | `MDL`, `CRD`, `COV` | 95%+ line coverage, then functional coverage |
| Operations | bug / monitoring | `IN` + `MIT`, then `PM` + `LRN` + `RPI` | system stable; RPIs verified in target env |
| Evolution | monthly token budget | `REF` → `SEED` | refactor lands via a Delivery loop |

---

## Workflow

```
┌─────────────────────────────────────────────────────────────┐
│  INCIDENT (INxxx)                                            │
│  Something breaks. Apply Mitigations (MITxxx) to stabilize.  │
│  Log <ARTIFACT_ROOT>/incidents/INxxx.md. Close when stable.  │
└───────────────────────────────┬─────────────────────────────┘
                                 │ system stable; a SET of incidents chosen
                                 ▼
┌─────────────────────────────────────────────────────────────┐
│  POST-MORTEM (PMxxx) over one or more incidents              │
│  5-whys → Learnings (LRNxxx) → Repair Items (RPIxxx)          │
└───────────────────────────────┬─────────────────────────────┘
                                 │ for each RPI
                                 ▼
┌─────────────────────────────────────────────────────────────┐
│  REPAIR ROUTING (/speckit.engloopkit.repair)                 │
│   small  → /speckit.tinyspec → /speckit.tinyspec.implement   │
│   large  → /speckit.specify → plan → tasks → implement (SPxxx)│
│  In both cases: update the SEK model (MDL), add CORD (CRD),   │
│  re-explore, regenerate tests, re-check coverage (COV).       │
└───────────────────────────────┬─────────────────────────────┘
                                 │ merged to source
                                 ▼
┌─────────────────────────────────────────────────────────────┐
│  RELEASE (generic)                                           │
│  1. Rebuild affected build output from source                │
│  2. Publish new artifacts                                    │
│  3. Update the target environment's references               │
│  4. Deploy to the target environment                         │
│  5. Run verification tests — ALL must pass                   │
│  6. If any test fails → roll back immediately                │
└─────────────────────────────────────────────────────────────┘
```

---

## Verification gate (post-deployment)

Every deployment to the target environment must pass all tests listed in the relevant
incident/post-mortem before the incident can be closed.

---

## Readiness Gate — Definition of Ready-for-Incidents

> **"Ready for incidents" / "ready to operate" is the OUTPUT of a gate, never an input an agent
> asserts.** A project may NOT be described as ready, and MUST NOT be treated as being in Stage 6
> (operate), until the Readiness Gate returns **PASS**. This is a hard precondition, not a
> guideline. (Ratified after `PM001`: a consumer was declared "ready for incidents" with almost
> nothing modelled, explored, or covered, because readiness was narrated instead of proven.)

The gate is computed by `/speckit.engloopkit.coverage` (Stage 5) over the **whole product**. Every
module — each `components/*` component **and** the vertical — must be **≥95% line & branch covered**,
**architecture-conformant**, and **green** (unit suite + any regression). *How* a module earns its
coverage depends on its **class** (per the ARC002 litmus test — *is it generic, domain-free code
useful unchanged in an unrelated repo?*):

- **Component** (`components/*`, passes the litmus test): verified by **unit / property tests** to
  ≥95% line & branch. A component needs **no** `MDL`/`CRD` — it carries no domain behavior, so a
  model of it would be tautological (and authoring one to tick a box is the PM001 failure in
  disguise).
- **Vertical** (`src/*`, domain-specific behavior): verified by **SEK self-modelling** — an `MDL`
  (a SEK model of the module's behavior) **and** a `CRD` (a CORD exploration) that **generates** the
  conformance tests — **plus** ≥95% line & branch.

**Self-model granularity is behavior-level (PM003).** A vertical is usually a **pipeline** whose
assemblies are *stages*, not standalone products. The self-model criterion is satisfied by **at least
one representative end-to-end self-model** (`MDL` + `CRD`) whose exploration/conformance exercises the
vertical's **observable behavior against a real SUT**, together with the tool's conformance loops over
its samples. Internal pipeline stages are then validated **transitively** by that end-to-end flow — a
bespoke model *per assembly* is **not** required and is discouraged as tautological theatre (PM002). A
**pure value-type** vertical module (no observable stateful behavior) is verified like a component
(unit/property). **The ≥95% line & branch coverage requirement remains PER module** regardless —
behavior-level self-modelling fixes only *where the "modelled + explored" evidence lives*, never the
coverage bar.

**Self-model adequacy is graded, not assumed (PM004).** A self-model must be *worth something* — the
gate grades its **behavioral adequacy**, not merely its existence and positive conformance. A vertical
self-model MUST satisfy all three:

1. **Model-derived negative conformance (required).** The model must express **expected outcomes**,
   including **error / rejection outcomes** for actions attempted **out of their legal order or with
   invalid input**, and the generated conformance MUST include **negative tests** that drive those
   **illegal** sequences and **assert the modelled error**. Positive conformance alone ("every legal
   action executed without throwing") is **insufficient** — it says nothing about whether the SUT
   correctly *rejects* what it should. *(Positive and negative conformance are different guarantees;
   the gate requires both.)*
2. **No hand-coded error asserts (theatre = FAIL).** The negative test must be **derived by the tool
   from the model** (guard/precondition + expected-error outcome). An "error case" implemented as an
   always-enabled positive action whose SUT body simply asserts a failure is **hand-coded, not
   model-derived** — it is the PM002 theatre class relocated to error-testing, and it is a **gate
   FAIL**. If a human writes the error assertion, the human — not the model — is validating error
   behavior.
3. **Behavioral-richness floor.** The model must exercise **non-trivial behavioral state** (multiple
   interacting state variables / real ordering constraints) such that exploration yields **materially
   distinct paths**, not a single flat covering-tour. A self-model whose reachable graph is one
   boolean is **not** an adequate behavior model of a stateful vertical (its generated suite is a
   script, not a behavior space) and is a **gate FAIL**.

The `COV` Readiness Inventory records, per vertical self-model, **Negative-conformance? (Y/N)** and
**Branches? (Y/N)**; either `N` on a stateful vertical is a **FAIL**. *(Raising this bar can move a
previously-"ready" project — including a self-hosting tool validating itself — back to **NOT READY**
until it provides model-derived negative conformance and a richer model. That is intended: the gate
must fail hollow self-validation, and this criterion also forces a model-based tool to grow the
negative-conformance capability it assumes.)*

**Precondition (not an escape hatch):** the vertical must contain **only** domain-specific behavior.
Any **generic / domain-free** code still living in the vertical is an **ARC002 violation and a gate
FAIL** — it must be extracted into a `components/` component first (and then unit/property-tested).
This is what makes a self-hosting/model-based tool honestly self-validating: *factor the generic code
out, and the residual vertical is the real domain surface the tool can model and explore.*

If **any** module fails **any** applicable criterion the gate is **FAIL** and the honest, required
status is **NOT READY** — the only statement allowed. The gate's evidence is a per-module **Readiness
Inventory** table (with a **Class** column) in the `COV` document; a module with no tests is
`Line 0% / FAIL` and may not be omitted. No command, template, or agent may state readiness except by
reporting a PASSing gate with its inventory attached.
