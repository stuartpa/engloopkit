---
name: speckit.engloop.70-six-pager-create
description: Create a self-contained six-page narrative decision memo in Markdown and convert it to a validated Word document.
argument-hint: "--subject <decision-or-strategy> --audience <decision-makers> --output <brief-title>"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks: { SessionStart: [{ type: "command", command: "dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.70-six-pager-create --root .", timeout: 30 }] }
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Reserve the next `SIX` number in `.engloop/numbering-registry.md`, then create:

- `.engloop/six-pagers/SIX<NNN>_<brief-title>.md` — authoritative narrative;
- `.engloop/six-pagers/SIX<NNN>_<brief-title>.docx` — generated Word document;
- `.engloop/six-pagers/SIX<NNN>_<brief-title>/assets/` — evidence-backed figures/tables;
- `.engloop/six-pagers/SIX<NNN>_<brief-title>/VALIDATION.md` — page/render/review evidence.

## Loop definition

- **Trigger:** a consequential decision, strategy, roadmap, policy, risk assessment, or cross-team change needs deep shared context and explicit decision criteria.
- **Goal:** one crisp, self-contained six-page narrative whose facts, goals, lessons, and priorities support a high-quality decision.
- **Actions:** test whether a six-pager is warranted, gather stakeholder/evidence input, draft and iterate the six-section narrative, move detail to appendices, convert to DOCX, and validate the rendered document.
- **Verification:** the narrative body is exactly six rendered pages; every material fact/metric is sourced; appendices are separable; reviewer comments are resolved or recorded; the generated DOCX opens and renders without clipping or unresolved assets.
- **Memory:** numbered Markdown/DOCX, supporting assets, appendices, and validation record under `.engloop/six-pagers/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.70-six-pager-create --root .`

## Six-pager suitability check

Before writing, record why a six-pager is worth the reader/author investment:

1. **Audience:** multiple decision-makers or impacted teams need the same context.
2. **Purpose:** the decision is too complex or consequential for a short note or slide deck.
3. **Longevity:** the narrative should stand alone weeks or months later.
4. **Time:** evidence gathering, rewriting, and review can be completed responsibly.
5. **Complexity:** missing context could create ambiguity, conflict, or a poor decision.

If these conditions are not met, explain why a shorter artifact is appropriate and stop; do not pad prose to six pages.

## Evidence and authorship rules

- Start from repository evidence, current metrics, authoritative external sources, and explicit stakeholder input. Never invent baselines, targets, causes, customer claims, costs, or commitments.
- Make the memo self-contained for a reader unfamiliar with the subject. External links are sparse; essential detail belongs in the narrative or appendix.
- Write complete sentences and coherent paragraphs with topic sentences. Narrative is the default; bullets are allowed only where the contract explicitly permits them.
- Clearly distinguish current facts, historical lessons, assumptions, forecasts, proposals, and decisions required.
- Cite appendix items inline (for example, `Appendix A`) and keep source/provenance with each supporting table or figure.
- Do not copy proprietary Amazon wording or source text. Apply the narrative method and section purpose in original language.

## Required six-page narrative

The body is exactly six rendered pages, excluding cover metadata and appendices. Use these sections in order unless the user explicitly approves a topic-specific adaptation and the validation record explains it:

1. **Introduction (approximately 1/2–1 page).** State the subject, decision, reader context, scope, urgency, and narrative direction. Write this last if that produces a clearer opening.
2. **Goals (approximately 1/2 page).** Bullets are allowed. Define a small set of measurable outcomes using baseline, target, delta, and date/decision horizon. A goal without an authoritative baseline is explicitly marked unresolved.
3. **Tenets (approximately 1/2 page).** Bullets are allowed. State durable decision principles/North Star boundaries that resolve real tradeoffs; omit decorative slogans.
4. **State of the business/system (approximately 1 page).** Give a current-tense, data-backed snapshot of structure, execution, health, customer/system behavior, and progress against goals.
5. **Lessons learned (approximately 1/2–1 page).** Use past tense and evidence. State successes, failures, surprises, causal limits, and what changed; do not polish failures or mix future proposals into this section.
6. **Strategic priorities (approximately 2–3 pages).** This is the majority of the memo. For each priority, state the prediction/thesis, evidence from prior sections, execution approach, owner/decision boundary, measurable effect on a goal, risks, dependencies, and validation plan.

Do not add a repetitive conclusion or executive-summary ending. End on a concrete forward action, decision, or priority. The appendix has no fixed page limit and may contain detailed tables, graphs, calculations, alternatives, risks, source notes, or implementation detail that would interrupt the six-page narrative.

## Review process

- Gather early input from the accountable leader and critical subject-matter/impacted stakeholders before polishing.
- Share an early draft with at least 2–3 reviewers. Incorporate clarifications into the narrative rather than leaving repeated questions only in comments.
- Categorize feedback as affirmation, question, disagreement, missing evidence, or deferred meeting discussion. Resolve or explicitly record every substantive item.
- Include a review-session note recommending 15–30 minutes of silent reading followed by discussion of comments, unresolved questions, decisions, and actions. Do not fabricate attendance or approval.

## Markdown-to-Word tooling

Use the GPL-licensed Pandoc CLI to convert authoritative Markdown to DOCX. Require an explicitly available, approved Pandoc version (validated baseline: `3.10.1`). Do not silently install Pandoc, switch converters, or use an online service.

Use an explicit command equivalent to:

`pandoc <memo.md> --from markdown --to docx --output <memo.docx> [--reference-doc <approved-reference.docx>]`

Record the exact Pandoc version and reference DOCX identity/hash (or the explicit use of Pandoc's default reference document). Do not hand-edit the generated DOCX without applying the same change to the authoritative Markdown and regenerating.

## DOCX and page validation

- Open the generated DOCX with an actual Word-compatible renderer (Microsoft Word or explicitly approved LibreOffice). Export/render it to PDF or page images for inspection.
- Validate the six-page narrative body separately from appendices using the same styles/reference document. Require exactly six rendered body pages; do not estimate from Markdown length.
- Inspect every rendered page at normal reading size for widows/orphans that destroy meaning, clipped text, broken tables, missing figures, unreadable captions, unresolved placeholders, and accidental blank pages.
- Verify headings, page numbers, footnotes/endnotes, cross-references, appendix labels, and figure/table captions.
- A non-empty DOCX is necessary but not sufficient. If no compatible renderer is available, report the missing prerequisite and do not claim completion.

## Naming policy

Compound command names use subject/action order. Therefore this command is `six-pager-create`, consistent with `handoff-create`, `powerpnt-create`, and `academic-paper-create`.

## Done when

- [ ] The SIX counter was advanced before artifact creation
- [ ] The suitability check justifies a six-pager rather than a shorter artifact
- [ ] The body uses the required six-section narrative in order
- [ ] Goals have authoritative baselines/targets or explicit unresolved markers
- [ ] Facts, metrics, assumptions, forecasts, and proposals are distinguishable and sourced
- [ ] Strategic priorities occupy the narrative majority and trace to goals/evidence
- [ ] Supporting detail is in cited appendices, not hidden external links
- [ ] At least 2–3 reviewer perspectives were requested and substantive feedback is resolved/recorded
- [ ] Pandoc generated a non-empty DOCX from the authoritative Markdown
- [ ] The rendered narrative body is exactly six pages and every page was visually inspected
