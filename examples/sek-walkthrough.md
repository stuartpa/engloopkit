# Walkthrough: taking SEK through EngLoopKit

SEK (the Z3 model explorer in this workspace, `stuartpa/sek`) is EngLoopKit's first
real consumer. This walkthrough shows the whole loop applied to it. SEK has **completed
the bridging code stage** (49 tests green, all Spec Explorer samples byte-identical, a
CI regression gate live), so it is entering **Stage 2 — Architect**.

The point of this document is to make the loop concrete, and to show that EngLoopKit
is self-hosting: SEK is both the tool that powers Stage 5 and a product built with the
loop.

---

## Where SEK is now (end of Stage 1)

- Bridging code implemented from the original SEED (the porting-from-spec-explorer
  effort).
- `specify → plan → tasks → implement` already run to get the CORD parser, engine,
  solver, and CLI working.
- A CI regression gate exists (`scripts/regression.ps1`, 59 baselines).

So Stages 0 and 1 are done. Begin at Stage 2.

## Stage 2 — Architect

```
/speckit.engloopkit.architect  SEK: define the long-lived architecture from the bridging code
```

- `init-brownfield` maps the current SEK modules: `Sek.Cord` (parser/AST/constraint
  extraction), `Sek.Engine` (exploration, automata, compiled scenarios), `Sek.Solver`
  (Z3 gate), `Sek.Modeling` (requirements, containers), `Sek.Core` (graph analysis),
  `Sek.Cli`.
- Record the boundaries as ARC decisions, e.g.:
  - **ARC001** — CORD parsing must not depend on the engine (one-way dependency
    Cord → Engine).
  - **ARC002** — the solver boundary: only `Sek.Solver` references Z3; everything else
    talks to an abstraction.
  - **ARC003** — generated tests are an output artifact, never hand-edited.
- `architecture-workflow` surfaces any current violations as refactor tasks for Stage 3.

## Stage 3 — Refactor to final form

Drive the bridging code to honor ARC001–ARC003 using the governed loop:

```
/speckit.architecture-guard.governed-spec  Enforce Cord→Engine one-way dependency and solver abstraction boundary
/speckit.plan
/speckit.tasks
/speckit.implement
```

`architecture-verify` must be clean before moving on.

## Stage 4 — Model

```
/speckit.engloopkit.model  Sek.Engine exploration state machine
```

- State fields: current DFA/product-DFA node, binding environment, visited set.
- Actions: `Explore(machine, startJson)`, `StepBinding`, subset/product construction.
- Invariants: a closed scenario never steps; goal states are always accepting.
- Produces **MDL001** pointing at the engine model source; sanity explorations
  reproduce the known Sailboat/atsvc behavior.

## Stage 5 — Explore & Coverage (the inner loop)

```
/speckit.engloopkit.explore  CRD for Sek.Engine goal-then-accepting paths
/speckit.engloopkit.coverage
```

- **CRD001** explores the compiled-scenario stepping paths; SEK/Z3 generates the test
  cases (this is the token-free quality engine — no LLM writes these tests).
- **COV001** measures coverage. Phase 1: drive `Sek.Engine`/`Sek.Cord` line/branch to
  95%+ by extending CRDs at the largest gaps. Phase 2: functional coverage — error
  paths in `ConstraintExtraction.StripComments`, unbounded-model guards, return-binding
  dataflow.
- Loop `explore ⇄ coverage` until COV declares COMPLETE, keeping the suite fast
  (PointAndShoot2 is the known slow outlier — a candidate for tighter bounds).

At this point SEK is implemented, governed, modeled, explored, and covered.

## Stage 6 — Operate

When a user hits a bug (e.g. a CORD model that hangs because an `id` is left unbounded
by dropped comments):

```
/speckit.engloopkit.incident  CORD model hangs: Z3 enumerates unbounded ints
```

- Mitigate fast (MIT001: bound the domain in the affected model to unblock the user).
  No permanent fix committed.
- Once stable, and once a few such incidents accumulate:

```
/speckit.engloopkit.postmortem  IN001 IN00x  comment/handling bugs in constraint extraction
```

- 5-whys → **LRN001**: "constraint extraction can silently drop bindings, leaving Z3
  variables unbounded." → **RPI001**: "strip comments before splitting on `;` so no
  binding is lost" (this is exactly the real `StripComments` fix).

```
/speckit.engloopkit.repair  PM001 RPI001
```

- tinyspec-sized → `/speckit.tinyspec`. Then **mandatory**: update MDL, add a CRD that
  explores the comment-in-constraint case, regenerate tests (a generated test now
  covers it), confirm COV didn't regress.

## Stage 7 — Evolve (monthly)

```
/speckit.engloopkit.refactor-scan  I have tokens left this month
```

- Signals: recurring "constraint-extraction" cause-class (branch 1 fires) → REF001:
  "consolidate all CORD text pre-processing behind one audited normalizer." Emits
  **SEED00x**, which re-enters the Delivery loop.

---

## The loop, closed

SEK now has a repeatable machine around it: features enter as SEEDs, are governed by
ARC, verified by SEK's own Z3 exploration, operated through incidents/post-mortems, and
kept healthy by monthly refactors — with LLM tokens spent only on judgement, and the
solver doing the coverage work for free.
