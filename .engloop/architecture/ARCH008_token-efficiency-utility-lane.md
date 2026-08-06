# ARCH008: Governed token-efficiency utility lane

- **Created:** 2026-08-05
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

Both agents require VS Code agent-scoped hooks. Agent 30's guard mechanically allows one
schema-valid create-new analysis JSON and no other write. Agent 31's prompt hook creates a
session/HEAD-bound scope gate and in-progress evidence record; its tool hook enforces exact
approved paths/commands, and its Stop hook requires finalized evidence. If hooks are
disabled or their guard markers are absent, the agents stop rather than operate in a
reduced-assurance mode.

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
- Agent 31 receives a reviewable bounded contract rather than a vague “optimize” prompt.
- Recommendations become measurable artifacts with confidence and data limitations.
- Repeated solutions can become portable skills/scripts instead of growing every chat or
  always-on instruction file.
- Token efficiency cannot be claimed by malformed joins, hidden fallbacks, or broad
  validation rituals.