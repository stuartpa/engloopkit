---
description: Stage 7 (Evolution loop) — Run periodically (e.g. month-end). Walk a refactoring decision tree over the accumulated state of the codebase, pick the single highest-value refactor, record it as a REF, and emit a SEED that starts a clean Delivery loop.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). It may scope the
scan (a subsystem) or set a budget ("I have N tokens to use this month").

## When to run

Periodically — the natural cadence is **month-end, when there are Copilot tokens to use
or lose**. After a month of incident → post-mortem → repair loops, the codebase has
accumulated pressure (drift, duplication, hot spots, recurring cause-classes). This is
the slow **evolution loop** that keeps the product healthy for years.

## Loop definition

- **Trigger:** a schedule (month-end) and available token budget.
- **Goal:** the *one* next refactor that most improves long-term health, scoped as a
  SEED ready for a Delivery loop.
- **Actions:** gather signals, walk the decision tree, pick one refactor, emit a SEED.
- **Verification:** the choice is justified against the decision tree; the SEED is
  actionable.
- **Memory:** `docs/refactors/REFxxx_<slug>.md` → a new `SEEDxxx`.

## Step 1 — Gather signals (Observe)

Collect the evidence the decision tree needs:

1. **Recurring cause-classes.** Scan `docs/postmortems/INDEX.md` — which cause-class
   tags repeat? Repetition means a structural weakness the repair loop didn't fully
   close.
2. **Architecture drift.** Run architecture-guard's review and its DRY/duplication scan:
   ```
   /speckit.architecture-guard.architecture-workflow
   ```
   Note boundary violations and duplicated business logic.
3. **Coverage & test health.** Read recent `COVxxx` — files stuck below target, slow
   test outliers, brittle areas.
4. **Change hot spots.** Which files changed most this month (from git history)? Hot +
   complex = high refactor value.

## Step 2 — Walk the refactoring decision tree (Reason)

Evaluate in order; take the **first** branch that fires (highest leverage first):

1. **Recurring incident cause-class not structurally closed?**
   → Refactor to make that class mechanically impossible (strongest signal — real
   failures are recurring).
2. **Architecture drift / boundary violations from the guard?**
   → Refactor to restore the boundary (protects the long-lived architecture).
3. **Significant duplicated business logic (DRY)?**
   → Consolidate the duplication into one owner.
4. **Hot spot with low coverage or high complexity?**
   → Simplify/decompose the hot spot, then let the Verification loop cover it.
5. **Test suite too slow (over budget) despite adequate coverage?**
   → Refactor tests/model bounds for speed (tighter CORD explorations).
6. **None of the above fire?**
   → No refactor this cycle. Record that and stop — do not manufacture work (YAGNI).

Pick exactly **one** refactor — the first branch that fired. Concentrating the token
budget on one well-scoped refactor beats spreading it thin.

## Step 3 — Record the REF (Memory)

Read the `REF` "Last used" value in
[`docs/numbering-registry.md`](../../docs/numbering-registry.md); new number = +1,
zero-padded. **Increment the registry first.** Create
`docs/refactors/REF<NNN>_<slug>.md` from
[`templates/REF-template.md`](templates/REF-template.md): the signals gathered, the
decision-tree branch chosen, the rationale, the expected long-term benefit, and the
scope.

## Step 4 — Emit a SEED (Act)

Hand the chosen refactor to Stage 0 so it runs as a normal, governed Delivery loop:

```
/speckit.engloopkit.seed  REF<NNN>: <refactor title>
```

The `seed` command creates a `SEEDxxx` that carries the REF's rationale and scope. From
there the refactor follows `specify → plan → tasks → implement` (governed by
architecture-guard), then `model`/`explore`/`coverage` — the full loop.

## Step 5 — Report

```
Refactor scan complete.
Signals: recurring cause-classes <..>, drift <..>, DRY <..>, hot spots <..>
Chosen (decision-tree branch <n>): REF<NNN> — <title>
Emitted: SEED<NNN>. Next: /speckit.specify to run the refactor as a Delivery loop.
```

If no branch fired: `No refactor warranted this cycle (recorded in REF<NNN>).`

## Done when

- [ ] Signals gathered (post-mortem index, drift, coverage, hot spots)
- [ ] Decision tree walked; exactly one refactor chosen (or none, justified)
- [ ] `REF` counter incremented; `docs/refactors/REF<NNN>_<slug>.md` created
- [ ] A `SEEDxxx` emitted for the chosen refactor (unless none warranted)
- [ ] Next step (specify) suggested
