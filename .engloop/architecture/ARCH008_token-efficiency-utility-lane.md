# ARCH008: Governed token-efficiency utility lane

- **Created:** 2026-08-05
- **Amended:** 2026-08-31
- **Status:** ACCEPTED
- **Governs:** Stages 30–31, token-efficiency evidence, customization recommendations,
  declared-toolchain preflight, and compact validation output
- **Consulted learning:** [Verification follows artifact class](../learnings/cards/verification-follows-artifact-class.md)
  (`PM002/LEARN001`–`LEARN003`)

## Decision

Token and chat-speed efficiency is an independently invoked two-agent utility lane, not
a product-state transition and not an alternate implementation lifecycle:

1. **30 Analyze** observes VS Code GitHub Copilot session/customization/toolchain
   evidence, writes one compact analysis JSON, proposes stable repair IDs, and cannot
   modify repository/configuration state beyond that evidence artifact.
2. **31 Implement** requires the analysis hash plus a fresh explicit approved repair-ID
   list, changes only approved paths, preflights one authoritative command path, validates
   the touched slice first, writes compact evidence, and cannot bypass Stages 04–08.

The handoff is review-first (`send: false`). No analysis recommendation implies approval.
Agent 31 is terminal and never commits or pushes. A separately invoked workflow may do
so only after Agent 31 stops and the user reviews its diff/evidence.

Both agents require VS Code agent-scoped hooks. The common `SessionStart` entry check is
retained, but it is not an activation authority for these user-switchable agents: VS Code
defines `SessionStart` as the first prompt of a new session, so it does not cover selecting
Agent 30 or 31 in an existing chat. Each submitted token-agent prompt therefore runs an
ordered `UserPromptSubmit` chain. A root-local JSON entry hook runs first and returns
`continue: false` on rejection, which short-circuits all later hooks without creating
scope state. Agent 30 then idempotently creates or revalidates its per-session analysis
gate and emits `TOKEN_EFFICIENCY_ANALYSIS_GUARD_ACTIVE`. Agent 31 loads its implementation
guard and then validates the exact analysis/approval contract before emitting
`TOKEN_EFFICIENCY_IMPLEMENTATION_SCOPE_ACTIVE`.

Agent 30's `PreToolUse` guard mechanically allows one schema-valid create-new analysis
JSON and no other write; prompt reactivation never resets an accepted artifact path or
replaces corrupt state. Agent 31's prompt hook creates a session/HEAD-bound scope gate and
in-progress evidence record; its tool hook enforces exact approved paths/commands, and its
Stop hook requires finalized evidence. If hooks are disabled, entry is rejected, or any
required marker is absent, the agents stop with phase-specific remediation rather than
operate in a reduced-assurance mode. This applies `PM002/LEARN001–003`: prompt lifecycle,
tool mutation, and completion are different artifact classes and retain distinct entry,
`PreToolUse`, and `Stop` verification instead of weakening one gate to compensate for
another lifecycle's absence.

## Host hook provenance

The prompt-chain contract was source-verified on 2026-08-31 against installed VS Code
Insiders `1.134.0-insider` (application build directory `d1d996042b`, bundled Copilot
package `1.0.81-0`). In the bundled Copilot hook executor, one base input object—including
one ISO timestamp—is created before the ordered command loop; each command is awaited
sequentially; and a result with `continue: false` produces a stop reason and breaks that
loop. The prompt execution path invokes `UserPromptSubmit` for submitted prompts, while
`SessionStart` is gated to conversation turn count one. Therefore the transient entry
receipt binds stage, safe session identity, event UTC ticks, and HEAD to one submitted
prompt and is consumed by that prompt's activation chain. A later host/runtime version
must preserve these semantics or be revalidated before it can claim strict token-agent
activation compatibility. Agent switching without submitting a prompt is not treated as
activation and requires no marker.

## Evidence method by artifact class

- Session behavior uses Chronicle/session-store token events when cloud data exists; local
  storage uses labeled proxies such as turns, checkpoints, message sizes, repeated files,
  commands, and polling. Unavailable token data is never estimated as measured.
- Customization repair uses the VS Code role matrix: concise instructions for broad rules,
  open-standard Agent Skills for reusable workflows/scripts/resources, custom agents for
  least-privilege roles, hooks for deterministic lifecycle enforcement, and prompts for
  explicit invocations.
- Toolchain repair follows declared manifests/lockfiles/CI plus one cheap executable
  preflight. It never tries multiple likely commands or switches package managers.
- Validation stores full logs under ignored `.engloop/out/token-efficiency/` and emits
  bounded status/diagnostics/path evidence.

## Open-standard preference

Reusable multi-step capabilities prefer `.github/skills/<name>/SKILL.md` following the
Agent Skills specification: discoverable metadata, concise instructions, progressive
loading, and optional directly referenced `scripts/`, `references/`, and `assets/`.
VS Code-only options such as forked skill context are additive and require explicit
compatibility/setting checks; they do not replace the portable skill contract.

## Tool and polling boundary

The shipped `Resolve-DeclaredToolchain.ps1` helper returns compact JSON and chooses direct
pnpm when available or `corepack pnpm` when the declared pnpm workspace lacks a direct
command. Corepack signature/resolution failure blocks; npm/package-lock and integrity
bypass are forbidden fallbacks.

Only one authoritative monitor owns long-running deployment/status polling. These agents
may consume its final evidence but never initiate deployment mutation or duplicate a
running monitor loop.

## Consequences

- Engineers can switch to Agent 30 at the first sign of thrash without authorizing edits.
- New chats, active-chat agent switches, and the first prompt after compaction all use the
  same prompt activation contract; no stale SessionStart marker is accepted as proof.
- Agent 31 receives a reviewable bounded contract rather than a vague “optimize” prompt.
- Recommendations become measurable artifacts with confidence and data limitations.
- Repeated solutions can become portable skills/scripts instead of growing every chat or
  always-on instruction file.
- Token efficiency cannot be claimed by malformed joins, hidden fallbacks, or broad
  validation rituals.