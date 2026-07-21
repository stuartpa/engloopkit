# Loop Engineering, and how EngLoopKit aligns

This document records the definitions EngLoopKit is built on and shows, term by term,
how the bundle stays close to the vocabulary of *Loop Engineering* as it emerged in
2026. Sources are listed at the end.

## The one-line definition

> **Loop engineering is the practice of designing the *system* that prompts your AI
> agent, rather than typing each prompt yourself.**

Where *prompt engineering* asks "what should I say to get the best output?", *loop
engineering* asks "what system should I build so the agent finds the work, does it,
verifies it, and remembers what it did — without me in the loop at all?"

The term was popularized in June 2026 by **Addy Osmani**, synthesizing ideas from
**Boris Cherny** (creator of Claude Code at Anthropic) and **Peter Steinberger**.
Cherny's framing: *"I don't prompt Claude anymore. I have loops that are running.
They're the ones that are prompting Claude and figuring out what to do."*

## The five components of a loop

Every well-designed agent loop has the same five parts. EngLoopKit documents **every
command** against these five headings so each stage is a real loop, not a one-shot
prompt.

| Component | Loop Engineering meaning | How EngLoopKit uses it |
|---|---|---|
| **Trigger** | What starts the loop — a schedule, an event, a human instruction, another agent finishing. | A North Star, accepted specification, failing test, coverage gap, incident report, or explicit stewardship capacity. |
| **Goal** | A *verifiable* end state — "all tests pass", "bundle < 200KB" — not "make it better". | "95%+ line coverage then functional coverage", "all P1 incidents mitigated", "zero architecture-drift violations". |
| **Actions** | The tools the loop may use — read/write files, run commands, call MCP, spawn sub-agents. | Spec Kit commands, **SEK/Z3 exploration**, the test runner, coverage tooling, git, architecture-guard, tinyspec. |
| **Verification** | How the loop knows to stop — tests + exit codes, a supervisor, a diff review, CI. | Deterministic gates: test pass/fail, coverage thresholds, **Z3 exhaustiveness**, CI, `architecture-verify`. |
| **Memory** | What persists across iterations so work isn't repeated or lost. | Root North Star/Learnings plus `SPEC/SCAF/ARCH/MODEL/CORD/COV/IN/PM/REFACT/POM` records — see [standards.md](standards.md). |

## The agentic cycle

Within a loop, an iteration is **Observe → Reason → Act → Evaluate → loop-or-terminate**
(the ReAct pattern). EngLoopKit's Verification-loop commands (`model`, `explore`,
`coverage`) are the clearest example: observe current coverage, reason about the gap,
act by authoring/extending a CORD exploration, evaluate the regenerated tests'
coverage, and repeat until the Goal (95%+) is met.

## Andrew Ng's nested loops

On 30 June 2026, Andrew Ng framed 0→1 product building as **three nested clocks**:
an **inner agentic coding loop** (minutes), nested inside a **developer feedback loop**
(hours), nested inside an **external user feedback loop** (days). EngLoopKit adopts
this directly and adds a fourth, slower **evolution loop** (a month):

- **Inner agentic loop (minutes)** — `explore` / `coverage`: Z3 generates and re-checks
  tests in tight cycles with no human prompting.
- **Delivery loop (hours)** — North Star → scaffold → `architect` → refactor → `model`:
  the developer's feedback clock across a feature.
- **Operations loop (days)** — `incident` → `postmortem` → `repair`: the external
  feedback clock driven by real users and monitoring.
- **Evolution loop (monthly)** — `refactor-scan`: the slow structural-health clock.

## Why this matters for tokens

Loop engineering shifts leverage from *phrasing* to *system design*. EngLoopKit takes
the further step of insisting that the **Verification** component of the quality loops
be a **deterministic engine, not an LLM**: SEK explores CORD models with Z3 to prove
coverage and generate test cases without spending tokens per case. LLM tokens are
reserved for the components where judgement is irreducible — writing a spec, doing
root-cause analysis, choosing the next refactor. See [token-efficiency.md](token-efficiency.md).

## Guardrails EngLoopKit inherits from the discipline

- **Define exit conditions first.** Every command states its Goal before its Actions.
- **Set hard limits.** Exploration and refinement loops are bounded (max iterations /
  budget) so a loop can't run away.
- **Log everything.** Each loop writes a numbered Memory document; nothing loops
  silently.
- **Test routines independently.** Model, exploration, and coverage are separate
  commands that each work on their own before being wired together.

## Sources

- Osmani, A. — "What Is Loop Engineering?" (June 2026) and reference repository.
- explainx.ai — *What Is Loop Engineering? The New Paradigm Beyond Prompt Engineering*
  (five components: Trigger, Goal, Actions, Verification, Memory).
- MindStudio — *What Is Loop Engineering? The New Meta for Autonomous AI Agent
  Workflows* (`/loop`, `/goal`, `/routines`; Observe→Reason→Act→Evaluate).
- Ng, A. — *Three Loops for 0-to-1 Products* (June 2026).
- ReAct: Reasoning + Acting (Yao et al., 2022).
