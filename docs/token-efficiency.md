# Token and Chat-Speed Efficiency

EngLoopKit is built for engineers who pay for tokens and time. The objective is not
merely shorter responses: it is faster verified work with less repeated context,
rediscovery, command guessing, output capture, and polling.

## Governed utility lane: 30 → 31

Switch to `speckit.engloop.30-token-efficiency-analyze` whenever an active or completed
VS Code GitHub Copilot chat shows likely waste: excessive turns, no compaction/checkpoint,
oversized tool output, repeated reads/commands, unavailable command guessing, duplicated
deployment polling, or slow serial work that could be parameterized.

Agent 30 is read-only with one exception: it writes one compact analysis JSON under
`.engloop/evidence/`. It uses the approved Chronicle/session-store path when available,
separates real cloud token measurements from local proxies, aggregates one-to-many tables
before joins, checks current customizations and declared/local toolchains, and returns
stable repair IDs. It never implements, installs, polls, commits, pushes, or claims
closure.

Agent 30 requires VS Code agent-scoped hooks (`chat.useCustomAgentHooks`) and the
`TOKEN_EFFICIENCY_ANALYSIS_GUARD_ACTIVE` session marker. Its `PreToolUse` guard permits
read/search/session-store work, bounded read-only probes, and exactly one create-new
schema-valid analysis JSON; every other write or command is denied. Local Chronicle
analysis requires `github.copilot.chat.localIndex.enabled`; without the index/tool, Agent
30 records visible-only limitations rather than inventing session data.

The review-first handoff (`send: false`) points to
`speckit.engloop.31-token-efficiency-implement`. Agent 31 requires the analysis path and
an explicit user-approved repair-ID list in the invocation. It rejects wildcards or
inferred approval, implements only approved paths, preflights one authoritative tool
command, validates the touched slice first, stores full logs under ignored
`.engloop/out/token-efficiency/`, and writes one compact implementation JSON. It does not
bypass Stages 04–08 or mutate deployments/global/user state.

Agent 31 also requires agent-scoped hooks. Its `UserPromptSubmit` initializer validates
the analysis identity/hash, exact HEAD and canonical Git-status digest, repository repair
IDs, resolved prerequisites, allowed/prohibited paths, and argument-array validation
commands. `PreToolUse` then denies paths/commands outside that scope and permanently
denies deployment/global mutation and commit/push. `Stop` requires a durable evidence
artifact finalized as `passed`, `blocked`, or `failed`.

## Evidence hierarchy

1. **Cloud session usage:** actual `assistant.usage` input/output token events, scoped to
	 `VS Code Chat` and aggregated by session before joins.
2. **Local session store:** turn count, session duration, checkpoints, oversized messages,
	 repeated files/tools, and polling/command-thrash proxies scoped to
	 `GitHub Copilot Chat`.
3. **Visible session/repository evidence:** only when the session-store tool/index is
	 unavailable; limitations must be explicit.

Never infer token counts from character counts or malformed joins. A proxy is reported as
a proxy, with range/uncertainty; unavailable token data stays unavailable.

## Faster, lower-context implementation patterns

- **Preflight once.** Read authoritative manifests/lockfiles/CI and test one declared
	command path. For pnpm workspaces, use direct `pnpm` when available or a single
	`corepack pnpm --version` check; signature failure is a prerequisite failure, not
	authority to disable verification or use npm/package-lock.
- **Parameterize repetition.** Prefer a terse script/task/skill with arguments and compact
	exit output over repeated ad hoc commands.
- **One monitor owns polling.** Use one long-running monitor with a compact final record;
	other agents consume that evidence rather than issuing status loops.
- **Keep logs out of chat.** Write full output to an ignored file, then return status,
	key metrics, bounded diagnostics, and path.
- **Validate by risk.** Run the narrowest deterministic check proving the touched slice;
	broaden only when shared behavior or risk warrants it.
- **Isolate heavy discovery.** Use a bounded read-only subagent or an explicitly supported
	forked skill when intermediate context does not belong in the parent session.
- **Compact or restart deliberately.** Long chats with growing input and no checkpoint
	should compact at a meaningful boundary or hand durable state to a fresh session.

## Choose the open customization primitive

Use existing authorities first. For new reusable multi-step capability, prefer the open
Agent Skills standard in `.github/skills/<name>/SKILL.md`: concise discoverable metadata,
stepwise instructions, and progressively loaded `scripts/`, `references/`, or `assets/`.
Use targeted instructions for scoped conventions, custom agents for persistent roles and
least-privilege tools, hooks for deterministic lifecycle enforcement, and prompt files for
small explicit invocations. Do not duplicate the same workflow across all of them.

## Existing deterministic savings

- SEK/Z3 explores behavior and generates tests without a token per test case.
- Compilers, tests, coverage, architecture checks, Git, and renderers own objective gates.
- The component pattern chooses unit/property versus model-based verification by artifact
	class rather than forcing expensive hollow ceremony.
- Durable numbered memory and the Learnings Pyramid avoid re-deriving accepted context.
- Bounded loops and review-first handoffs keep autonomous work and scope explicit.

## Rule of thumb

> If a compiler, test runner, solver, script, preflight, or monitor can decide it, do not
> spend repeated turns rediscovering or polling it. Spend tokens on the decision the
> deterministic evidence cannot make.
