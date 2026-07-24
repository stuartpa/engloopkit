---
marp: true
theme: default
paginate: true
title: <presentation title>
description: <audience and purpose>
---

# <System> — North Star and behavior

<One-sentence enduring direction>

<!-- Source: NORTHSTAR.md -->

---

## North Star

- **Who it serves:** <audience>
- **Enduring outcomes:** <outcomes>
- **Non-negotiable boundaries:** <boundaries>
- **Current direction:** <direction>

<!-- Source: NORTHSTAR.md -->

---

## Architecture — system context

![Top-level boxes-and-lines architecture](./<deck-assets>/architecture-context.svg)

- <Explain each box and external boundary.>
- <Explain each line and dependency direction.>

<!-- Source: ARCHxxx / authoritative architecture artifact -->
<!-- Geometry: connector-label bounds do not intersect nodes, title/subtitle, legend, prior labels, or view-box bounds. -->

---

## Architecture — <drill-down scope>

![Drill-down boxes-and-lines architecture](./<deck-assets>/architecture-detail.svg)

- <Explain the lower-level boxes.>
- <Explain interfaces, ownership, and important data/control flow.>

<!-- Source: ARCHxxx / authoritative architecture artifact -->

---

## What the <model name> Spec Explorer model represents

- **System aspect:** <one coherent behavior aspect>
- **State:** <modeled state>
- **Actions:** <modeled actions>
- **Invariants/guards:** <important rules>
- **Intentionally omitted:** <abstraction boundary>
- **Why this model matters:** <audience value>

<!-- Source: MODELxxx / model source / CORD evidence -->

---

## <Model behavior slice>

![Focused 5–9-node behavior graph](./<deck-assets>/model-slice.svg)

- **Start:** <state>
- **Choices/guards:** <meaningful choices>
- **Outcome/rejection:** <important behavior>
- **Implementation relationship:** <what this demonstrates>

<!-- Source: MODELxxx / CORDxxx / exploration output -->
<!-- Geometry: use a dedicated horizontal label lane or explicit collision-checked label coordinates. -->

---

## What was built

- <Capability tied to architecture/model evidence>
- <Validation evidence>
- <Current limitations without overstating readiness>

<!-- Source: implementation + COV/validation evidence -->

---

# Appendix — generated test paths

---

## Generated case <case ID>

![Straight-line generated test path](./<deck-assets>/test-case-001.svg)

- **Setup:** <setup>
- **Actions:** <ordered actions>
- **Expected outcome:** <result/rejection>

<!-- Source: generated test / CORD exploration case -->

<!--
Validation requirements:
- Run deterministic geometry preflight for all graph assets.
- Generate the PPTX from this Markdown.
- Export every graph slide from the actual PPTX to PNG using an approved renderer.
- Inspect rendered PNGs at normal presentation size; any text overlap/clipping fails.
- Record renderer/version, inspected slides, defects/fixes, and pass in VALIDATION.md.
-->
