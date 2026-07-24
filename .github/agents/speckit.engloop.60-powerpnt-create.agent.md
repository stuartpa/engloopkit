---
name: speckit.engloop.60-powerpnt-create
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
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.60-powerpnt-create
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
- `.engloop/presentations/PPT<NNN>_<brief-title>/assets/` — generated diagram images.

## Loop definition

- **Trigger:** the user wants a presentation explaining current direction, architecture, modeled behavior, and generated tests.
- **Goal:** one evidence-backed visual story whose Markdown source converts deterministically to PPTX.
- **Actions:** gather North Star/architecture/model/exploration evidence, select representative graph slices, create graphics, author Marp slides, convert, and validate outputs.
- **Verification:** slide order and graph constraints pass; every diagram traces to source evidence; Marp produces a non-empty PPTX without unresolved assets.
- **Memory:** numbered Markdown, PPTX, and asset directory under `.engloop/presentations/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.60-powerpnt-create --root .`

## Tooling contract

Use the MIT-licensed Marp CLI (`@marp-team/marp-cli`) to convert Markdown to PowerPoint
and the MIT-licensed Mermaid CLI (`@mermaid-js/mermaid-cli`) for Mermaid graph assets.
Require explicitly available executables or user-approved pinned invocations equivalent to
`npx @marp-team/marp-cli@4.5.0` and
`npx -p @mermaid-js/mermaid-cli@11.16.0 mmdc`. Do not download through `npx` without
explicit user approval. Require a compatible browser for both renderers.
and a compatible browser. Do not silently install dependencies, use an online converter,
or switch presentation engines after failure. Fail with prerequisites when conversion is
unavailable.

Render trusted local Mermaid graph sources with an explicit command equivalent to:

`mmdc -i <graph.mmd> -o <graph.svg>`

Convert trusted local assets with an explicit command equivalent to:

`marp <deck.md> --pptx --allow-local-files --output <deck.pptx>`

The regular PPTX format prioritizes visual fidelity; do not use experimental editable PPTX
unless the user explicitly asks and accepts its extra dependencies/lower fidelity.

## Required slide story

1. **Title and North Star.** Begin by explaining what the living North Star is, who the
   system serves, enduring outcomes, non-negotiable boundaries, and current direction.
2. **Top-level architecture.** Show one simple boxes-and-lines graphic of the complete
   system boundary and major components. Add concise bullets explaining every box, line,
   ownership boundary, and external dependency.
3. **Progressive architecture drill-down.** Add successive slides that zoom into lower
   architectural levels. Every slide uses boxes and lines plus audience-oriented bullets;
   do not replace architecture graphics with prose walls or source-code screenshots.
4. **Spec Explorer model introduction.** Before behavior slides, explain what each model
   abstracts, its state/actions/invariants, why that abstraction is useful, and what was
   intentionally omitted. Do not assume the audience already understands model-based
   testing.
5. **Behavior slices.** For each selected system aspect, show one evidence-derived model
   graph with approximately **7 +/- 2 nodes** (5–9 nodes). Each graph tells one coherent
   behavior story, labels transitions meaningfully, and has bullets explaining start state,
   choices, guards, important outcomes, rejection paths, and relation to implementation.
   Split larger graphs into multiple slides; never shrink a large unreadable graph to fit.
6. **What was built.** Connect architecture and model behavior to implemented capabilities,
   evidence, validation, and current limitations without overstating readiness.
7. **Appendix: generated tests.** Include generated test cases as graph images, each a
   straight-line path from setup/actions to expected outcome. Group related paths, label
   calls/returns/results, and cite the generated test/exploration source. Branching model
   graphs belong in the body; straight-line generated cases belong in the appendix.

## Graph evidence and rendering

- Use authoritative model/exploration/generated-test artifacts supplied by the repository
  configuration, MODEL/CORD records, SEK exploration output, or explicit user input.
- Never invent states, transitions, architecture dependencies, generated cases, or graph
  coverage to make a better-looking presentation. Missing graph evidence is a blocker.
- Prefer deterministic local graph sources (for example DOT/Mermaid text generated from
  authoritative evidence) rendered to SVG/PNG. Store graph source beside rendered assets
  when practical so diagrams are reviewable.
- Every slide containing a graph must cite its source artifact in presenter notes or a
  small source footer.
- Keep text readable at normal presentation size; use high contrast, consistent direction,
  and a legend when symbols/colors carry meaning.

## Markdown/PPTX validation

- Include Marp frontmatter and separate slides with `---`.
- Verify no unresolved image references, placeholder tokens, or missing source citations.
- Verify each body model graph has 5–9 nodes; any exception requires explicit user approval
  and a recorded rationale.
- Verify every appendix test graph is a straight line (each internal node has at most one
  outgoing path in the displayed case).
- Generate the PPTX and require a non-zero file. If conversion fails, report the exact
  Marp/browser prerequisite or conversion diagnostic; do not claim success from Markdown
  alone.

## Naming policy

Compound command names use **subject/action** order. Therefore this command is
`powerpnt-create`, consistent with `pomodoro-create`, `overlay-pack`, `overlay-remove`,
and `codereview-prepare`. Established single-word lifecycle commands retain their stable
public identities.

## Done when

- [ ] The PPT counter was advanced before creating artifacts
- [ ] North Star opens the content story
- [ ] Architecture progresses from top-level to lower-level boxes-and-lines graphics
- [ ] Every model is explained before its behavior graphs
- [ ] Each body model graph focuses on one aspect and has 5–9 nodes
- [ ] Appendix generated-test graphs are straight-line cases with source citations
- [ ] Markdown and all diagram assets are reviewable and evidence-derived
- [ ] Marp generated a non-empty `.pptx` from the authoritative Markdown