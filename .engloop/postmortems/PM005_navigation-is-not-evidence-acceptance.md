# PM005: User navigation is not evidence acceptance

- **Recorded:** 2026-09-06; onset/duration not established, no synthetic timestamps.
- **Covers incident:** [IN009](../incidents/IN009_operations-navigation-evidence-coupling.md), HANDOFF012/013 report identifiers.
- **Status:** ANALYZED — source/documentation repairs implemented; scoped compatibility verified; release blocked by unresolved whole-product readiness.
- **Closure:** OPEN. No stabilization, validated PM completion, repair acceptance, readiness, or release PASS is claimed.
- **Authority:** Maintainer explicitly authorized PM repair and a new release on 2026-09-07, conditional on required checks. This is not an authorization to waive readiness or claim repair closure.

## Factual chronology

| Sequence | Observation / action | Evidence boundary |
|---|---|---|
| 1 | PM001 required computed readiness; PM002 refined proof by artifact class. | Historical source learnings below, not permission to gate user navigation. |
| 2 | Owner explicitly separated navigation from claims and required bounded release work. | Accepted 2026-09-06 owner directive below. |
| 3 | HANDOFF012/013 repair request reported operations-hook and entry/surface failures. | Owner report; original handoff packets are not in this tracked worktree. |
| 4 | Source owner implemented lifecycle recovery, narrow scope denial, internal confirmation binding, exact path parsing and command-root selection. | Current uncommitted source inspected; no build/test execution by this documentation pass. |
| 5 | Documentation inspection found no-tools/active-incident prohibitions and Stop-controlled handoff wording still present. | Previous Stage20/21/22 bodies in Git; six matching bodies edited without metadata/hook changes. |
| 6 | Reserved IN009 and PM005, then recorded mitigation and this analysis. | Only IN/PM registry counters advanced. Validation and release remain open. |

## Causes and five whys

**Primary cause:** completion acceptance and permission to continue a conversation were
treated as the same control. Strict evidence validation is correct; broad lifecycle/tool
stops and body-level navigation prerequisites are not.

**Contributors:** internal binding mechanics leaked into recovery instructions; prose
punctuation and exact identity needed distinct parsing contracts; surface validation
assumed a source checkout rather than explicitly selecting source or installed commands.

1. **Why was recovery blocked?** Missing/invalid evidence was treated as a reason to stop
   the conversation or all tools, rather than reject the particular claim or operation.
2. **Why did changing agents depend on evidence?** Stage guidance required valid scope
   before any tool and tied handoff availability to successful completion validation.
3. **Why did that seem consistent with governance?** PM001's historical stage-transition
   language was applied beyond truthful readiness claims; the owner directive explicitly
   corrects that interpretation, without changing the quality bar.
4. **Why did ordinary recovery inputs compound the failure?** Binding instructions exposed
   internal flags, and path/layout assumptions conflated prose, exact identities, source
   surfaces and installed surfaces. Each needs its own explicit contract.
5. **Why was this not prevented by existing assurance?** The owner directive and runnable
   hook/body/layout contracts diverged. Static text and adjacent green evidence do not
   prove their combined recovery behavior. The exact generated-test escape remains to
   be audited; this analysis does not invent a model omission or a passing replay.

## Direction, provenance and authority

- [North Star](../../NORTHSTAR.md) SHA-256: `7b6fb4b6df799630f0fc458b1c2169fd095e24b813b34fd83d0fa945a50e70a8`.
- [Learning index](../../LEARNINGS.md) SHA-256: `946c30f062a110dc3089b0fd9f3b16434bd3a382653fe543525ae48f94a5641b`.
- **Alignment:** ALIGNED — generic contracts, efficient bounded work and honest evidence.
- **Owner authority:** [release/navigation directive](../learnings/owner-directives-release-and-navigation.md)
  supersedes historical navigation prerequisites, not mutation authorization or evidence gates.
- **REINFORCED:** [RULE:readiness-is-a-gate](../learnings/cards/readiness-is-a-gate.md),
  [PM001/LEARN001–003](PM001_no-readiness-gate.md#learnings): PASS is check output.
- **REINFORCED:** [RULE:verification-follows-artifact-class](../learnings/cards/verification-follows-artifact-class.md),
  [PM002/LEARN001–003](PM002_gate-verification-method-by-class.md#learnings): static docs,
  pure parsing/layout checks and stateful recovery require different proof.
- **CONTRADICTED interpretation:** evidence gates as permission to enter Stage21/22/50.
  The existing readiness card already records this owner override; the accepted evidence
  principle is not contradicted.
- **Pyramid decision:** NO-CHANGE. **No accepted source learning:** observations below
  restate existing owner requirements and remain candidates until validation/acceptance.
  No new accepted PM005/LEARN identities or historical-coverage updates are claimed.
- **Retrieval:** UNCHANGED — no rule meaning, card, index, or retrieval query edited.

## Learnings (candidate observations, not newly accepted provenance)

- Separate user-selected navigation, specific protected operations, and completion
  acceptance. `continue=true` with `completionAccepted=false` is truthful recovery.
- A confirmation-controlled internal binder must not become a user token-copying task.
  Unresolved scope permits local read/search/questions, not arbitrary execution/delegation.
- Normalize only the documented bare `.md.` prose ending. Quoted, structured/CLI and
  receipt identities stay exact; invalid inputs must not create authority.
- Test the selected artifact layout and lifecycle behavior, not merely neighboring
  contracts or elapsed validation time. Freeze scope and escalate release-budget overruns.

## Repair items: implementation is not acceptance

| RPI | Completed implementation / documentation | Owner and pending acceptance |
|---|---|---|
| RPI001 | Source `WriteLifecycleDeferred` preserves navigation with rejected completion; `WritePostmortemScopeDecision` allows local recovery tools and denies protected operations; `PostTool` binds internally after confirmation. | Runtime/validation owner: run current `OperationsLearningHookTests`, including lifecycle faults, failed Stop/SubagentStop, active incidents, exact rebind, cancellation and invalid dispatch. Prove unchanged evidence and no false PASS. |
| RPI002 | Source `Option`/`PostmortemPath` distinguish bare sentence punctuation from exact identities; `ValidationCommands.ResolveCommandDirectory` selects source/installed roots or explicit `--command-root`; Stage20 no longer requires readiness. | Validation owner: run current parsing cases and `InstalledCommandSurfaceTests`; prove invalid/ambiguous/partial roots never fall back, and root/config checks plus Stage10 readiness remain strict. |
| RPI003 | Six Stage20/21/22 agent/command bodies now preserve navigation, explain narrow scope denial/internal binding, and keep claims strict; metadata/hooks retained. | Documentation/validation owner: static parity/link checks, then fresh CLI command/agent-surface checks on source and installed artifacts. `docs/standards.md` still contains historical Stage20 blocking wording; reconcile in separately authorized scope. |
| RPI004 | IN009/PM005 record bounded repair and open evidence honestly; no release/version mutation performed. | Model/release owners: finish escape analysis and exact gate contracts, run required regression/readiness and immutable installed/upgrade validation, then publish an authorized new 1.x release. Measure phases against the under-90-minute target; escalate instead of rerun churn. |

These are tracked obligations, not accepted Stage22 routes. Exact executable gate vectors,
rule/scenario-bound learning contracts, route/close receipts and current readiness are
pending with their owners; no placeholder receipt or manually authored PASS is supplied.

## SEK escape disposition and remaining proof

**SEK applicability: RELEVANT** for the stateful recovery/binding/acceptance vertical.
Proposed scenario: `SEK-SCENARIO:operations-navigation-with-unaccepted-evidence` — missing,
partial, invalid and stale scopes across direct/delegated/resumed lifecycles must preserve
navigation and reject protected operations/false completion; exact rebind restores only
the authorized scope. Inspect current model/Cord domains, slices/bounds, binding/oracle,
generated freshness and replay before assigning a verified escape class or repair path.
That audit and generated recurrence gate are **pending**, not inferred from direct tests.
Documentation parity and domain-free path/layout checks use static/unit proof under PM002;
do not add tautological models for them.

## Verification record and release prerequisites

### 2026-09-07 continuation (supersedes pending-test wording below)

- Inspected the saved scoped compatibility TRX: **360/360 passed, zero failed/skipped**.
  Evidence: `.engloop/out/navigation-repair-tests/compatibility.trx`, SHA-256
  `633200977397f2e1b2a48bb38b33bfd9e768cd20f126878331020339d5e310e1`.
  TRX start-to-finish: **518.79 s**; separately, owner-reported command elapsed:
  **530.69 s**. No unchanged long-suite rerun was performed.
- Executed only `InstalledCommandSurfaceTests` using existing Debug binaries,
  `--no-build --no-restore`: **59/59 passed, zero skipped; 41.06 s** command elapsed.
  TRX: `.engloop/out/navigation-repair-tests/blocked-release-installed-surface.trx`.
  This supports the installed-root regression checks, not a fresh build, final artifact
  identity, supported-upgrade proof, or whole-product readiness.
- Release policy blocker: `README.md`, **Development and release gates**, requires
  product readiness before immutable artifact hashes are emitted. The packaging script
  does not itself enforce that prerequisite; invoking it cannot substitute for the gate.
  Saved `.engloop/out/cov003-readiness.json`, captured 2026-09-05, reports **NOT READY**:
  `branch-coverage-below-threshold:tool:84.48` versus required 95%. This is an older
  baseline, not a current coverage measurement. A current whole-product PASS is absent.
- Consulted the readiness and verification cards and PM001/LEARN001–003 and
  PM002/LEARN001–003 above. The owner release/navigation directive explicitly does
  not establish a coverage waiver. Prior corrective publication is not authority to
  invent one. The release therefore remains **BLOCKED**, not PASS.
- Remote tag query found no `v1.19.*`; **1.19.0 remains the planned successor**.
  No version pins or historical changelog were changed; no final assets, manifest,
  catalog checksum, upgrade proof, tag, push or public release were produced.
- Resume after current readiness is satisfied, or an explicit documented owner policy
  decision changes the corrective-publication prerequisite. Then build once from the
  source commit, validate the canonical manifest schema/ordered IDs/hashes, prove the
  old 1.18 updater against exact final 1.19 assets in a disposable consumer, and promote
  those tested bytes. PM/repair closure additionally retains the evidence obligations
  below; publication would not automatically satisfy them.

### Earlier documentation-pass record (historical scope)

- Source inspected: [OperationsHookCommands.cs](../../src/EngLoopKit.Tool/OperationsHookCommands.cs)
  and [ValidationCommands.cs](../../src/EngLoopKit.Tool/ValidationCommands.cs).
- Existing source/test edits belong to another owner and were not changed by this pass.
- Static documentation checks **PASS**: YAML metadata parity and unchanged hooks against
  this worktree's HEAD; all three agent/command bodies match excluding trailing
  whitespace; 19 local links/anchors resolve across the eight stage/incident/PM files;
  29 commands and 32 manual handoff edges retained; `git diff --check` passes. Only
  IN/PM registry rows changed, and the four pre-existing source/test files remained
  SHA-256-identical during this pass. These are static checks, not runtime or PM acceptance.
- No `dotnet build`, `dotnet test`, build-triggering validation script, UI action,
  commit, push, version edit or release was performed by this documentation pass.
- No prior test-output directory or session-memory PASS is adopted as current evidence.
- Before closure: bind current incident/direction hashes, resolve stabilization and SEK
  escape evidence, accept learnings if warranted, validate the PM/RPI contracts, and
  prove the exact tested installed/release artifacts. Owner authorization to release
  does not itself satisfy any of those gates.
