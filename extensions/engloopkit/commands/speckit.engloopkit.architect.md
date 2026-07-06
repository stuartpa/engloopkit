---
description: Stage 2 — Derive the long-lived architecture from the working bridging code using architecture-guard, and record it as a numbered ARC decision that governs every later loop.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Prerequisite

The bridging code exists and runs (Stage 1 complete). The
[architecture-guard](https://github.com/DyanGalih/spec-kit-architecture-guard)
extension is installed (the `engloopkit` bundle pulls it in). This command
**orchestrates** architecture-guard; it does not replace it.

## Loop definition

- **Trigger:** bridging code exists and runs.
- **Goal:** an explicit, governed architecture — architecture-guard constitutions plus
  one or more human-readable `ARCxxx` decisions — that all later loops honor.
- **Actions:** run architecture-guard's brownfield mapping and workflow; capture
  boundaries, ownership, and contracts.
- **Verification:** `architecture-review` runs clean, or only with accepted, tracked
  exceptions.
- **Memory:** `docs/architecture/ARCxxx_<slug>.md`.

## Step 1 — Map the existing code (Observe)

Run architecture-guard's brownfield mapping so governance starts from reality, not
assumptions:

```
/speckit.architecture-guard.init-brownfield
```

Read what it produces. Identify the natural boundaries the bridging code already hints
at, the ownership of each module, and the contracts between them.

## Step 2 — Initialize constitutions (Act)

If constitutions do not yet exist, create them:

```
/speckit.architecture-guard.init
```

Encode the boundaries, ownership rules, and contracts you want to hold for years.
Apply the "lazy senior developer" / YAGNI stance: prefer the fewest boundaries that
keep the system honest. Do not invent structure the bridging code gives no evidence
for.

## Step 3 — Record ARC decisions (Memory)

For each significant architecture decision, create a numbered ARC document:

1. Read the `ARC` "Last used" value in
   [`docs/numbering-registry.md`](../../docs/numbering-registry.md); the new number is
   that + 1, zero-padded. **Increment the registry first.**
2. Create `docs/architecture/ARC<NNN>_<slug>.md` from
   [`templates/ARC-template.md`](templates/ARC-template.md): the decision, the context
   from the bridging code that motivated it, the rule it establishes, and how
   architecture-guard will enforce it.

## Step 4 — Review and verify

Run the architecture review over the current state:

```
/speckit.architecture-guard.architecture-workflow
```

Any violations become refactor tasks (they are handled in Stage 3, refactor-to-final).
The Goal here is not zero violations in the bridging code — it is a *clear, governed
target* the refactor stage will drive toward.

## Step 5 — Report

```
Architecture governed. Decisions recorded:
- docs/architecture/ARC<NNN>_<slug>.md ...
Constitutions: <architecture-guard location>
Open violations to resolve in Stage 3: <count>
Next: refactor bridging code to final form via /speckit.architecture-guard.governed-spec
      (Stage 3), then /speckit.engloopkit.model (Stage 4).
```

## Done when

- [ ] Brownfield mapping run
- [ ] Constitutions created/refined
- [ ] Each significant decision recorded as an `ARCxxx` (registry incremented)
- [ ] `architecture-workflow` run; violations captured as refactor tasks
- [ ] Next step (governed refactor) suggested
