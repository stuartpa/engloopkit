# Presentation artifacts

Stage 71 creates evidence-backed Markdown-first PowerPoint decks.

- Reserve the next `PPT` number in `../numbering-registry.md` before creating artifacts.
- Keep authoritative Markdown, generated PPTX, diagram sources, and rendered assets together.
- Start with the North Star, then progressively drill through boxes-and-lines architecture.
- Explain each Spec Explorer model before showing focused 5-9-node behavior graph slices.
- Put generated straight-line test-case graphs in the appendix.
- Never invent states, transitions, architecture edges, or generated tests for presentation aesthetics.
- Treat connector labels as independent geometry: reserve label lanes or explicit
	collision-checked positions and reject intersections with nodes, labels, reserved slide
	areas, or view-box boundaries.
- Render every graph slide from the actual PPTX to PNG and visually inspect it; valid
	Markdown/SVG and a non-empty PPTX are not sufficient completion evidence.
