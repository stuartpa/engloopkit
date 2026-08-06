# Token-efficiency agents v1.13.0 acceptance

- **Date:** 2026-08-05
- **Status:** PASS
- **Scope:** generic VS Code GitHub Copilot token/chat-speed utility lane
- **Private handoff boundary:** no private repository path, product/workload name, session body, credential, or production log is recorded here

## Final stage registration

| Stage | Source command | Generated agent | Output |
|---:|---|---|---|
| 30 | `extensions/engloopkit/commands/speckit.engloop.30-token-efficiency-analyze.md` | `.github/agents/speckit.engloop.30-token-efficiency-analyze.agent.md` | `.engloop/evidence/token-efficiency-analysis-<session-and-full-UTC-timestamp>.json` |
| 31 | `extensions/engloopkit/commands/speckit.engloop.31-token-efficiency-implement.md` | `.github/agents/speckit.engloop.31-token-efficiency-implement.agent.md` | `.engloop/evidence/token-efficiency-implementation-<short-revision>-<full-UTC-attempt>.json` |

Agent 30 has one review-first handoff (`send: false`) to Agent 31. Agent 31 is terminal.
The exact installed surface is 23 agents/prompts and 28 ordered handoff edges.

## Renumbering

- `30-refactor-scan` → `40-refactor-scan`
- `31-learnings-pyramid` → `41-learnings-pyramid`
- `40-pomodoro-create` → `50-pomodoro-create`
- `50-overlay-pack` → `60-overlay-pack`
- `51-overlay-remove` → `61-overlay-remove`
- `60-six-pager-create` → `70-six-pager-create`
- `61-powerpnt-create` → `71-powerpnt-create`
- `62-academic-paper-create` → `72-academic-paper-create`

No compatibility aliases remain in current source/generated/install/package scopes.
Versioned historical records retain their original identities.

## Enforcement and resources

- Agent-scoped `SessionStart`, `UserPromptSubmit`, `PreToolUse`, and `Stop` hooks fail
  closed when guard/scope/evidence requirements are absent.
- Agent 30 can create exactly one schema-valid analysis JSON and cannot edit other paths.
- Agent 31 requires exact analysis HEAD/status/hash, explicit `TE-Rddd` approvals,
  resolved prerequisites, exact allowed/prohibited paths, and exact validation commands.
- Agent 31 permanently forbids deployment/global mutation and commit/push.
- Full logs are required under ignored `.engloop/out/token-efficiency/`; durable JSON is compact.
- Shipped schemas define analysis and implementation evidence.
- Shipped open-standard `SKILL.md` template uses progressive `scripts`, `references`, and
  `assets` resources.
- Shipped declared-toolchain preflight is bounded, no-network, strict-project, integrity-
  preserving, and chooses verified `corepack pnpm` only when direct pnpm is absent.

## Acceptance evidence

Known-session proxy fixture (no token billing events were supplied):

- `.engloop/out/token-efficiency-v1130-acceptance/token-efficiency-analysis-known-session-fixture.json`
- `.engloop/out/token-efficiency-v1130-acceptance/token-efficiency-implementation-fixture.json`
- `.engloop/out/token-efficiency-v1130-acceptance/toolchain-tests.log`

The fixture records turn/output-size/polling/toolchain facts as proxies, never converts
characters into token counts, and keeps complete logs outside chat/durable evidence.

## Validation results

- Exact command/frontmatter/tool/agent/terminal policy: PASS
- Exact ordered handoff graph (including same-count wrong-target rejection): PASS
- Spec Kit source→installed agent/prompt projection: PASS, 23/23, no stale old IDs
- Installed token-agent body guard markers and skill-path integrity: PASS
- Agent 30 one-artifact/out-of-scope write hook tests: PASS
- Agent 31 approval/path/command/Stop-evidence hook tests: PASS
- Declared pnpm toolchain: direct-absent → verified `corepack pnpm`: PASS
- Corepack signature failure → blocked, no npm/package-lock/global/integrity fallback: PASS
- Full direct suite: PASS, 222 tests
- Generated model suite: PASS, 667 tests
- Whole-product readiness: PASS; Tool line 96.96%, branch 95.34%
- Immutable package + clean/coexist/tracked-registry install/verify/remove transactions: PASS
- Public/private-boundary audit: PASS

Final immutable SHA-256:

- `engloopkit.1.13.0.nupkg`: `1ff488cab959d768dfda816c0934c1173b10380ce1caea8dfac845eea3608c4a`
- `engloopkit-extension-1.13.0.zip`: `39e6d050263e23c58bb4e10bb5a037e5242a223936abb917bf87397b0fee91cd`
- `engloopkit-1.13.0.zip`: `276ad3c04e324a5740b6bb391362c40f84765ab7ad3a0c2068c2b0e3880829fc`

## Deviations and explicit choices

1. Agent 30 retains the VS Code `edit` capability only because custom agents do not expose
   a portable path-scoped JSON writer; its mandatory `PreToolUse` guard restricts it to
   one create-new analysis artifact. If hooks are unavailable, Agent 30 stops.
2. Both agents allow one bounded read-only `Explore` subagent to isolate large repository
   surveys. Session queries, edits, and validation ownership cannot be delegated.
3. Token-efficiency evidence uses stable non-numbered filenames from the supplied contract;
   no new global document prefix was invented.
4. The known-session acceptance uses supplied aggregate facts as labeled proxies because
   no token-level event data was provided.
