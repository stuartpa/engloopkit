# REFACT<NNN>: <short title>

- **Date:** <date>
- **Cadence:** <e.g. month-end scan>
- **Compute profile:** <POINT | BOUNDED | DEEP>
- **Profile source:** <DEFAULT-POINT | EXPLICIT>
- **Declared scope:** <root-relative path or precise topic supplied by the user>
- **Scope class:** <LOCAL-SEAM | SUBSYSTEM | REPOSITORY>
- **Runtime model/thinking metadata:** UNAVAILABLE-NOT-INFERRED
- **Stewardship budget:** <time/compute available this cycle, if explicitly supplied>
- **Status:** CHOSEN | NONE-THIS-CYCLE
- **Northstar impact:** UNCHANGED | DIRECTION-CHANGE
- **North Star path:** <configured path, normally NORTHSTAR.md>
- **North Star identity:** <Git blob/hash or SHA-256>
- **North Star alignment:** <how the plan advances current direction, or explicit tension>
- **User-confirmed outcome:** <desired result>
- **User constraints/exclusions:** <must preserve / out of scope>
- **User plan decision:** CONFIRMED | REVISE | NONE-THIS-CYCLE

## Governing architecture

| Architecture decision | Identity | Constraint applied to this plan |
|---|---|---|
| `.engloop/architecture/ARCHxxx_<title>.md` | <Git blob/hash or SHA-256> | <boundary/dependency/rule> |

## Vertical and component boundary

| Responsibility | Current class/location | Litmus result | Planned class/location | Domain knowledge allowed |
|---|---|---|---|---|
| <responsibility> | <vertical/component path> | <useful unchanged elsewhere: YES/NO> | <component/vertical path> | <NONE or exact vertical knowledge> |

### Proposed component contract

- **Component responsibility/API:** <single purpose and proposed surface | NOT-APPLICABLE>
- **Consumers:** <vertical callers>
- **Dependency direction:** <vertical → component; component must not reference vertical>
- **Composition/migration:** <how the vertical will use it>

## Profile compliance

- **Inspection boundary:** <what was inspected and what was deliberately not inspected>
- **Subagent use:** <NONE | one focused Explore question | up to two distinct Explore questions>
- **Implementation envelope:** <point change, bounded subsystem work, or phased deep campaign>
- **Escalation decision:** <NONE | BOUNDED-RECOMMENDED | DEEP-RECOMMENDED>
- **Why this fits:** <evidence that the selected candidate stays inside the active profile>

## Signals gathered within declared scope

| Signal | Finding |
|---|---|
| Recurring cause-classes (POSTMORTEM INDEX) | |
| Architecture drift / boundary violations | |
| Duplicated business logic (DRY) | |
| Hot spots (change frequency × complexity) | |
| Test speed vs coverage | |

## Decision-tree branch taken

<Which of the ordered branches fired (1 recurring cause-class → 2 drift → 3 DRY →
4 hot spot → 5 test speed → 6 none), and why it fired before the others.>

## Chosen refactor

<The single refactor selected. Scope it tightly.>

For `POINT`, select one local simplification or one extraction into a single component.
For `BOUNDED`, select one cohesive subsystem refactor. For `DEEP`, select one campaign
and list its independently reviewable ordered slices below.

## Ordered implementation slices

<`NOT-REQUIRED` for POINT; a short ordered list for BOUNDED/DEEP. Stage 04 implements.>

## Expected long-term benefit

<Why this most improves the product's multi-year health.>

## Rationale for not choosing the others

<One line each on the branches that did not fire this cycle.>

## Hand-off

- Normally proceed through governed specification/refactor work. Update root
	`NORTHSTAR.md` only when the evidence above changes repository direction.
