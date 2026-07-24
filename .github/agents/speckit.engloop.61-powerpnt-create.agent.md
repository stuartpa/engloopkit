---
name: speckit.engloop.61-powerpnt-create
description: Create a Markdown-first PowerPoint deck explaining the North Star, layered
  architecture, Spec Explorer behavior models, and generated test paths.
argument-hint: --audience <audience> --output <deck-name> [--model-evidence <paths>]
target: vscode
user-invocable: true
disable-model-invocation: true
tools:
- read
- search
- edit
- execute
agents: []
hooks:
  SessionStart:
  - type: command
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.61-powerpnt-create
      --root .
    timeout: 30
---


<!-- Extension: engloop -->
<!-- Config: .specify/extensions/engloop/ -->
## User Input

```text
$ARGUMENTS
```

## Artifact root

Reserve the next `PPT` number in `.engloop/numbering-registry.md`, then create:

- `.engloop/presentations/PPT<NNN>_<brief-title>.md` — authoritative Marp Markdown;
- `.engloop/presentations/PPT<NNN>_<brief-title>.pptx` — generated PowerPoint;
- `.engloop/presentations/PPT<NNN>_<brief-title>/assets/` — diagram source/rendered images;
- `.engloop/presentations/PPT<NNN>_<brief-title>/VALIDATION.md` — geometry/render evidence.

## Loop definition

- **Trigger:** the user wants a presentation explaining current direction, architecture, modeled behavior, and generated tests.
- **Goal:** one evidence-backed visual story whose Markdown source converts deterministically to PPTX.
- **Actions:** gather North Star/architecture/model/exploration evidence, select representative graph slices, create graphics, author Marp slides, convert, render the PPTX, and validate every visual output.
- **Verification:** slide order, graph, and collision constraints pass; every diagram traces to source evidence; Marp produces a non-empty PPTX; every graph slide rendered from that PPTX is visually inspected.
- **Memory:** numbered Markdown, PPTX, diagram source/assets, rendered slide images, and validation record under `.engloop/presentations/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.61-powerpnt-create --root .`

## Tooling contract

Use the MIT-licensed Marp CLI (`@marp-team/marp-cli`) to convert Markdown to PowerPoint and the MIT-licensed Mermaid CLI (`@mermaid-js/mermaid-cli`) for Mermaid graph assets. Require explicitly available executables or user-approved pinned invocations equivalent to `npx @marp-team/marp-cli@4.5.0` and `npx -p @mermaid-js/mermaid-cli@11.16.0 mmdc`. Do not download through `npx` without explicit user approval. Require a compatible browser. Do not silently install dependencies, use an online converter, or switch engines after failure.

Render trusted local Mermaid graph sources with an explicit command equivalent to:

`mmdc -i <graph.mmd> -o <graph.svg>`

Convert trusted local assets with an explicit command equivalent to:

`marp <deck.md> --pptx --allow-local-files --output <deck.pptx>`

Regular PPTX prioritizes visual fidelity. Do not use experimental editable PPTX unless the user explicitly requests it and accepts extra dependencies/lower fidelity.

## Required slide story

1. **Title and North Star.** Explain who the system serves, enduring outcomes, non-negotiable boundaries, and current direction.
2. **Top-level architecture.** Show one simple boxes-and-lines system-context graphic. Explain every box, line, ownership boundary, and external dependency with concise bullets.
3. **Progressive architecture drill-down.** Add successive boxes-and-lines slides at lower architectural levels. Do not replace graphics with prose walls or source screenshots.
4. **Spec Explorer model introduction.** Before behavior slides, explain what each model abstracts, its state/actions/invariants, why the abstraction matters, and what it intentionally omits.
5. **Behavior slices.** For each selected aspect, show one evidence-derived model graph with approximately **7 +/- 2 nodes** (5–9). Label transitions meaningfully and explain start state, choices, guards, outcomes, rejection paths, and implementation relationship. Split larger graphs rather than shrinking them.
6. **What was built.** Connect architecture/model behavior to implemented capabilities, validation evidence, and current limitations without overstating readiness.
7. **Appendix: generated tests.** Show generated test cases as straight-line path graphs from setup/actions to expected outcome, grouped and cited. Branching model graphs belong in the body; straight-line generated cases belong in the appendix.

## Graph evidence and rendering

- Use authoritative model/exploration/generated-test artifacts from config, MODEL/CORD records, SEK output, or explicit user input. Never invent states, transitions, architecture dependencies, generated cases, or coverage.
- Prefer deterministic local DOT/Mermaid/SVG sources and store graph source beside rendered assets.
- Every graph slide cites its source artifact in presenter notes or a small footer.
- Keep text readable at normal presentation size with high contrast, consistent direction, and legends for semantic color/symbol use.

### Connector-label geometry

- Treat nodes, node content, connector paths, and connector labels as separate layout objects with explicit bounding boxes.
- A connector-label bounding box must not intersect any node bounding box, node title/body content area, slide title/subtitle area, legend, another connector label, or the SVG view-box boundary.
- Do not place labels at edge midpoints by default. Midpoint placement is allowed only after proving the complete rendered label bounds fit available whitespace.
- For horizontal flows, reserve a dedicated label lane above or below the node row. Draw connectors through inter-node gaps and render labels separately in that lane.
- For diagonal, curved, feedback, or branching edges, require an explicit label coordinate or a deterministic collision-free routing result.
- Size compact label capsules from measured/estimated rendered text rather than a fixed oversized width. If text does not fit, shorten audience-facing wording, wrap within a bounded capsule, move it to a callout/legend, or increase diagram space. Never cover node content.
- Remove redundant edge labels when direction and adjacent node names already communicate the relationship.
- Keep long implementation identifiers in notes or supporting bullets when they cannot fit cleanly in graph nodes/edges. Use audience-readable display labels in the graphic without changing meaning.
- Programmatic graph generators must run a geometry preflight that rejects intersections among labels, nodes, reserved bands, prior labels, and view-box boundaries with a visible padding margin.

## Markdown/PPTX validation

- Include Marp frontmatter and separate slides with `---`.
- Verify no unresolved image references, placeholder tokens, or missing source citations.
- Verify each body model graph has 5–9 nodes; exceptions require explicit user approval and recorded rationale.
- Verify every appendix test graph is a straight line (each internal node has at most one outgoing displayed path).
- Generate the PPTX and require a non-zero file. Markdown/SVG validity and resolved assets are necessary but not sufficient.
- Export every generated graph slide to PNG using an actual PPTX renderer (Microsoft PowerPoint or explicitly approved compatible renderer), not directly from the Markdown source.
- Visually inspect every rendered graph slide at normal presentation size for node/label overlap, label/label overlap, clipping, unreadable wrapping, and collisions with title/subtitle/legend areas.
- Any text overlap is a conversion failure. Fix authoritative diagram source/geometry, regenerate assets and PPTX, rerender, and reinspect before completion.
- `VALIDATION.md` records renderer/version, inspected slide numbers, geometry-preflight result, defects/fixes, and final pass.

## Required regression scenarios for programmatic graph helpers

When reusable graph code is created or changed, validate fixtures for:

1. seven horizontal nodes with narrow gaps and labels of varied length;
2. diagonal branches with labels near separate targets;
3. curved feedback edges crossing above/below a main row;
4. labels near title/subtitle/legend reserved bands;
5. adjacent long labels that would collide;
6. narrow nodes containing multi-word titles;
7. labels too large for inline gaps, requiring relocation/wrapping; and
8. a redundant label whose correct resolution is removal.

Every fixture requires no node/label, label/label, reserved-area, or view-box collision and readable text in a PNG rendered from the generated PPTX.

## Naming policy

Compound command names use subject/action order. Therefore this command is `powerpnt-create`, consistent with `six-pager-create`, `academic-paper-create`, and `pomodoro-create`.

## Done when

- [ ] The PPT counter was advanced before artifact creation
- [ ] North Star opens the content story
- [ ] Architecture progresses from top-level to lower-level boxes-and-lines graphics
- [ ] Every model is explained before its behavior graphs
- [ ] Each body model graph focuses on one aspect and has 5–9 nodes
- [ ] Appendix generated-test graphs are straight-line cases with source citations
- [ ] Markdown and diagram sources/assets are reviewable and evidence-derived
- [ ] Geometry preflight found no connector-label collision with nodes, labels, reserved areas, or view-box bounds
- [ ] Marp generated a non-empty PPTX from the authoritative Markdown
- [ ] Every graph connector label is collision-free in slides rendered from the actual PPTX
- [ ] Every graph slide was visually inspected and recorded in `VALIDATION.md`