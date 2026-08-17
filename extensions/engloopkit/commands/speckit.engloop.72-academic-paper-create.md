---
name: speckit.engloop.72-academic-paper-create
description: Create an evidence-backed systems research paper in Markdown and convert it to a publication-quality PDF.
argument-hint: "--title <paper-title> --audience <research-or-engineering-audience> [--venue <target>] [--output <brief-title>]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute, web]
agents: []
hooks: { SessionStart: [{ type: "command", command: "dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.72-academic-paper-create --root .", timeout: 30 }] }
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Reserve the next `PAP` number in `.engloop/numbering-registry.md`, then create:

- `.engloop/academic-papers/PAP<NNN>_<brief-title>.md` — authoritative paper;
- `.engloop/academic-papers/PAP<NNN>_<brief-title>.pdf` — generated PDF;
- `.engloop/academic-papers/PAP<NNN>_<brief-title>.bib` — authoritative bibliography;
- `.engloop/academic-papers/PAP<NNN>_<brief-title>/assets/` — figures, tables, plot sources/data;
- `.engloop/academic-papers/PAP<NNN>_<brief-title>/VALIDATION.md` — claim/citation/render review.

## Loop definition

- **Trigger:** implemented systems work needs a rigorous research-style account of its problem, design, mechanisms, evaluation, tradeoffs, and contribution.
- **Goal:** one original, evidence-backed technical paper that a systems researcher or expert engineer can evaluate independently.
- **Actions:** define the research question/contributions, gather implementation and prior-work evidence, structure the argument, create traceable figures/tables, report reproducible experiments, author Markdown with citations, convert to PDF, and review the rendered paper.
- **Verification:** every material claim is supported by implementation/evidence/citation; architecture and mechanism figures are readable; experiments identify setup/baselines/metrics/results; bibliography resolves; Pandoc generates a non-empty PDF with no clipping or unresolved references.
- **Memory:** numbered Markdown/PDF/BibTeX, source figures/data, experiment provenance, and validation record under `.engloop/academic-papers/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.72-academic-paper-create --root .`

## Research integrity

- Do not invent novelty, measurements, workloads, baselines, deployments, citations, limitations, or causal conclusions.
- Distinguish measured results, derived calculations, implementation facts, design rationale, hypotheses, and future work.
- Cite primary/authoritative sources where available. Verify title, authors, venue/year, URL/DOI, and cited claim before adding bibliography entries.
- Use original prose and original figures. Do not imitate or reproduce copyrighted paper wording, diagrams, tables, or distinctive examples.
- Preserve raw experiment commands/config/data or cite the repository artifact that does. A chart without traceable data is not evidence.
- State negative results, uncertainty, threats to validity, scope limits, and tradeoffs plainly.

## Systems-paper organization

Use a structure inspired by strong systems papers such as Socrates and Hekaton, adapted to the actual contribution rather than copied mechanically:

1. **Title, authorship, and abstract.** In a compact abstract, state the problem/constraint, system or technique, principal contribution, evaluation basis, and strongest measured result without unsupported superlatives.
2. **Introduction.** Establish real-world/system pressure, why prior approaches are insufficient, design challenge, concise contributions, and paper roadmap.
3. **Background / state of the art.** Explain only the prior architecture/features/concepts needed to understand the design. Position related work fairly with citations and explicit differences.
4. **Design goals and considerations.** State measurable requirements and architectural principles before presenting the solution. Make tensions/tradeoffs explicit.
5. **High-level architecture.** Present an early boxes-and-lines architecture figure, component responsibilities, control/data paths, trust/failure boundaries, and integration with the surrounding system.
6. **Mechanism/subsystem deep dives.** Organize by actual contributions: storage/indexing, compilation/programming model, transaction/concurrency behavior, durability/recovery, distribution/logging, caching, lifecycle, or other relevant mechanisms. Use worked examples/flows where they clarify semantics.
7. **System at work / end-to-end operation.** Walk through representative legal and failure/rejection scenarios, tying mechanisms to observable behavior.
8. **Evaluation methodology.** Separate methodology from interpretation. Record hardware/software/service versions, configuration, datasets/workloads, baselines, metrics, repetitions, warmup, variability/statistics, and reproducible commands.
9. **Results.** Answer explicit evaluation questions with referenced figures/tables. Report absolute values and meaningful comparisons; avoid conclusions unsupported by the shown data.
10. **Discussion.** Analyze tradeoffs, deployments/operability, cost, scalability, failure modes, limitations, threats to validity, and when the design should not be used.
11. **Related work.** Compare contributions and assumptions precisely; do not use citations as decoration or mischaracterize alternatives.
12. **Conclusion.** Restate the problem, design contribution, and evidenced outcome concisely. Do not introduce new claims.
13. **References and appendices.** Include complete resolved citations. Put secondary proofs, extended results, algorithms, or operational detail in appendices when required by the target venue/page budget.

Record an explicit target audience/venue and page/format budget. If no venue is supplied, use a readable single-column technical-report layout and state that choice; do not pretend venue acceptance or compliance.

## Figure and table contract

- Introduce a high-level architecture figure early, then use subsystem/mechanism figures, worked examples, lifecycle/sequence diagrams, and experimental plots only where they advance the argument.
- Every figure/table has a numbered caption, is cited in the body before/near use, explains units/legend/baseline, and traces to authoritative source/data.
- Keep architecture diagrams simple enough to explain component boundaries and paths; use detail figures for complex internals.
- Experimental tables/plots display comparable configurations, units, sample counts/variability when available, and honest axes. Never truncate/scale axes deceptively.
- Run geometry/readability checks for diagrams and inspect the rendered PDF at normal size. No label may cover another label, node content, title, caption, legend, or plot data.
- Store plot/diagram generator source and input data beside the rendered asset.

## Markdown-to-PDF tooling

Use GPL-licensed Pandoc (validated baseline: `3.10.1`) with an explicitly selected supported PDF engine (for example Typst, LaTeX, or another Pandoc-supported engine). Require the tools to be installed/approved; do not silently install dependencies, use an online converter, or switch engines after failure.

Use an explicit command equivalent to:

`pandoc <paper.md> --from markdown --citeproc --bibliography <paper.bib> --pdf-engine <explicit-engine> --output <paper.pdf>`

Use an approved CSL style/template only when the target venue/audience requires it. Record Pandoc, PDF engine, CSL/template, and bibliography hashes/versions in `VALIDATION.md`. Conversion warnings about missing citations/references are failures.

## Paper validation and review

- Check every citation key resolves exactly once and every bibliography item is cited or explicitly retained for a documented reason.
- Maintain a claim-evidence table in `VALIDATION.md` for contributions, performance/reliability claims, and comparisons.
- Verify section order/roadmap consistency, figure/table numbering/cross-references, equation/symbol definitions, acronyms, units, and terminology.
- Render the final PDF and inspect every page for clipped columns, overfull text, missing glyphs, broken math/code, unreadable figures, orphaned captions/headings, blank pages, and unresolved placeholders.
- Ask at least one architecture/mechanism reviewer and one evaluation/evidence reviewer. Record comments, resolution, and unresolved disagreement; do not fabricate peer review or acceptance.
- Where experiments cannot be run, narrow the claims and label evaluation gaps/future work instead of substituting estimates.

## Naming policy

Compound command names use subject/action order. Therefore this command is `academic-paper-create`, consistent with `six-pager-create`, `powerpnt-create`, and `handoff-create`.

## Done when

- [ ] The PAP counter was advanced before artifact creation
- [ ] The paper states a real research question/problem and evidence-backed contributions
- [ ] Background/prior work is accurately cited and distinguished from the contribution
- [ ] Design goals precede architecture and mechanism detail
- [ ] Architecture/mechanism figures are original, sourced, numbered, cited, and readable
- [ ] Evaluation methodology, baselines, metrics, environment, and reproducibility are explicit
- [ ] Results support every quantitative/comparative claim and include negative/uncertain outcomes
- [ ] Discussion states tradeoffs, limitations, threats to validity, and deployment/operational implications
- [ ] Citation and claim-evidence validation passes
- [ ] Pandoc generated a non-empty PDF with the explicit engine and no unresolved references
- [ ] Every rendered PDF page was visually inspected and reviewer feedback was resolved/recorded
