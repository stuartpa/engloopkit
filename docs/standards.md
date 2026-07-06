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

The gate is computed by `/speckit.engloopkit.coverage` (Stage 5) over the **whole product**. It
**PASSES** iff, for **every** module — each `components/*` component **and** the vertical — all of:

1. **Modelled** — the module has an `MDL` (a SEK model of its behavior).
2. **Explored** — the module has a `CRD` (a CORD exploration driving its tests).
3. **Covered** — **measured** line coverage ≥95% **and** branch coverage ≥95% (from real
   coverage tooling, attached to the `COV`), or each shortfall line carries a written rationale.
4. **Architecture-conformant** — the module honors every applicable `ARC` / architecture-guard
   check (no boundary violations, no leaked components).
5. **Green** — the full unit-test suite and any exploration-regression gate pass.

If **any** module fails **any** criterion the gate is **FAIL** and the honest, required status is
**NOT READY** — the only statement allowed. The gate's evidence is a per-module **Readiness
Inventory** table in the `COV` document; a module with no tests is `Line 0% / FAIL` and may not be
omitted. No command, template, or agent may state readiness except by reporting a PASSing gate with
its inventory attached.
