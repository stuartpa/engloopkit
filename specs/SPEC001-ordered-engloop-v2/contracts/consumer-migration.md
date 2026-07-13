# Contract: Clean v2 Consumer Migration and Release

- **Feature:** SPEC001
- **Release:** EngLoopKit 1.7.0 (ordered-workflow evolution on the 1.x maturity runway)
- **Consumers:** EngLoopKit self-host, `tthp`, `engloop-workshop`,
  `VerifyExtremeEdgeWithTpcc`

## Release artifacts

The migration uses immutable artifacts built from one accepted revision:

| Artifact | Identity | Required proof |
|---|---|---|
| EngLoopKit .NET tool | package/tool command `engloopkit`, version 1.7.0 | pack succeeds; command/version/config/validation smoke tests pass; SHA-256 recorded |
| EngLoop command extension | extension ID `engloop`, version 1.7.0 | exact 13-command and 13-agent source/archive/install semantic inspection, exact prompts/matrix/graph, zero EngLoop-owned diagnostic errors or warnings; SHA-256 recorded |
| EngLoopKit bundle | bundle ID `engloopkit`, version 1.7.0 | composition-only validation; exact pinned component versions; SHA-256 recorded |

Spec Kit 0.12.4 declares required tools but does not install arbitrary tool components.
Therefore every consumer has an explicit root-local .NET tool manifest/source policy or
an otherwise explicit tool installation step. Absence/version mismatch fails before
extension migration. It is never satisfied by searching a sibling build output.

Before the release artifact is authored, the disposable preservation experiment in
[`command-surface.md`](command-surface.md#early-spec-kit-0124-preservation-experiment)
must prove every required source field survives Spec Kit 0.12.4. If that experiment
fails, release work stops until the smallest supported upstream capability change or
an explicit generation mode implemented and documented by upstream Spec Kit passes the
same experiment. Consumer migration never uses an EngLoopKit-owned alternate generator,
post-processes a partial install, or copies hidden fallback agents.

The final catalog URL and checksum are written only after the final extension artifact
exists. Consumer acceptance uses those exact bits; accepted 1.7.0 artifacts are never
rebuilt in place.

## Per-root migration transaction

Run these phases from the selected root, one consumer at a time:

1. **Capture**
   - record root identity, Git HEAD/status or full checksummed backup;
   - inventory registry IDs, installed extension files, generated agents/prompts,
     current-source `engloop/`, target `.engloop/`, forbidden `.engloopkit/`, config,
    direction/Northstar/Learnings candidates, ignore rules, internal links, and focused
     entry points;
   - parse existing agent/prompt headers, local tool manifest/version, and focused-workspace
     approval settings; UI surfaces are intentionally not inspected;
   - stop on unreviewed unrelated changes that overlap migration paths.
2. **Prove an unambiguous source layout**
   - if a current tracked `engloop/` tree exists, require target `.engloop/` and
     `.engloopkit/` to be absent before mutation;
   - if no current process tree exists, require both old/target roots and
     `.engloopkit/` to be absent before direct target creation;
   - fail on both roots, a pre-existing forbidden config root, case ambiguity, path
     escape, or ownership uncertainty; never merge, copy over, prefer, or fall back.
   - require an explicit caller-selected tracking mode: `ExistingGit` or
     `InitializeGit`. Never infer or silently initialize source control. `InitializeGit`
     is permitted only after a complete checksummed backup and records that no
     pre-existing ancestry exists.
3. **Cut over the process root, direction, and config as one transaction**
   - in Git roots with a tracked current tree, run one history-preserving
     `git mv engloop .engloop` before editing moved content;
   - for explicit `InitializeGit`, operate only inside the complete checksummed backup
     boundary, establish the Git root as part of the transaction, and create/move
     directly to `.engloop/` without claiming pre-bootstrap ancestry;
   - after the root move, move any tracked initial direction file to root
     `NORTHSTAR.md`, then evolve exactly that file; otherwise create one reviewed
     Northstar from that repository's own evidence;
   - create config directly at `.engloop/config.json`, keep durable process memory
     under `.engloop/`, and ignore only transient `.engloop/out/`;
   - preserve or create visible root `LEARNINGS.md` as the learning entry point;
   - update all current-source `engloop/` links/path declarations to target
    `.engloop/` paths and remove legacy numbered-direction prefix/counter/template/directory references;
   - create no `.engloopkit/` directory and no live `engloop/` forwarding tree.
4. **Prove the root cutover before installing anything**
   - exactly one canonical `.engloop/` and one `.engloop/config.json` exist;
   - current `engloop/` count is zero and `.engloopkit/` count is zero;
   - root Northstar/Learnings exist, all internal links resolve, durable root files
     have repository ownership/tracking evidence, and `.engloop/out/` is ignored;
   - for `ExistingGit`, `git status` shows renames rather than unrelated delete/copy
     pairs; for both tracking modes, `git ls-files` contains every durable `.engloop/`
     file and excludes `.engloop/out/`.
5. **Remove v1 installation surfaces**
   - run explicit EngLoopKit extension removal (not `--keep-config` and not in-place
     force overwrite);
   - remove only residual files demonstrably owned by the old registry/package.
6. **Prove v1 installation absence**
   - no installed `engloopkit` directory/registry entry;
   - zero generated `speckit.engloopkit.*` agent/prompt files;
  - zero current legacy numbered-direction template/command/counter/live artifact;
   - core Spec Kit and unrelated extension files remain intact.
7. **Install exact v2 artifacts**
   - restore the root-local pinned tool;
   - install the exact extension/bundle artifact digest;
   - create/update the focused one-root workspace with explicit manual-edit protection for
     generated agents/prompts, the installed extension source, `.config/dotnet-tools.json`,
     and `.engloop/config.json`;
   - do not enable Bypass Approvals, Autopilot, wildcard edit approval, or broad
     terminal auto-approval;
   - do not use a sibling development install for final acceptance.
8. **Prove surface**
   - registry contains exactly the 13 ordered v2 IDs;
   - installed command files, generated agents, and generated prompts contain exactly
     one file per ID and zero old ID;
   - all 13 source/installed agent projections preserve every required common field,
     exact tool/subagent policy, exact stage hook, and exact ordered 23-edge graph;
   - Stage 31 has zero handoffs; every other agent has at least one; Stage 08 has no
     20/30/31 edge; every target resolves; all edges use `send: false` and omit model;
   - all 13 prompts select the exact matching agent and omit `tools`;
   - deterministic source/archive/install semantic comparison passes;
   - extension/product ID remains `engloopkit`.
9. **Prove standalone use**
   - resolve only `.engloop/config.json`, `.engloop/`, root Northstar/Learnings,
     validator, and applicable runway state without old/parent/sibling paths;
   - invoke representative deterministic tool validations for each applicable lane without
     cross-root resolution;
   - prove invalid agent-entry and durable validation attempts reject with the expected
     nonzero result. No editor window, picker, diagnostics panel, or UI hook observation is
     opened or required.
10. **Record/commit**
   - commit the coherent root rename/create, link/config/direction changes, and
     generated deletions/additions together in every accepted root;
   - require explicit caller opt-in to create the local commit; never push from the
     migration helper;
   - for a root that started without Git, also record pre/post-state checksums and
     retain the explicit backup until acceptance completes; the first Git history
     begins at the explicit bootstrap and makes no claim about earlier ancestry.

A failure stops the root. Do not continue to another root and do not combine old/new
files or restore an old config path to obtain a green deterministic validation result.

### Current-source versus target paths

The repository-specific `engloop/...` paths below identify files that exist **before**
implementation and therefore remain resolvable while this plan is reviewed. They are
migration inputs only. Their target paths after the atomic move are `.engloop/...`,
and the implementation change must update links to those targets before the old root
is removed from the accepted state.

## Owned stale-file sets

For each consumer, remove these old EngLoop-owned current surfaces:

- `.github/agents/speckit.engloopkit.*.agent.md` (currently nine);
- `.github/prompts/speckit.engloopkit.*.prompt.md` (currently nine);
- `.specify/extensions/engloopkit/commands/speckit.engloopkit.*.md`;
- `.specify/extensions/engloopkit/.specify-dev/agent-commands/**/speckit.engloopkit.*`;
- v1 installed numbered-direction template and old extension README/manifest payload;
- the `engloopkit` registry record containing the nine old IDs.

Do not delete core `speckit.*` generated files or unrelated extension data. After v2
installation, the corresponding new file stems are exactly the 13 IDs in
[`command-surface.md`](command-surface.md).

## Repository-specific end states

### EngLoopKit

- Current source: rename tracked `engloop/` to `.engloop/` with Git history, then move
  the tracked initial direction file to root `NORTHSTAR.md` and evolve it with Git
  rename history.
- Add only `.engloop/config.json`; ignore only `.engloop/out/`; preserve visible root
  `LEARNINGS.md`; update every moved-tree/reference link to `.engloop/`.
- Source extension, executable core/model/tests, Learnings Pyramid, docs, and package
  metadata all implement v2.
- Source, disposable install, and self-host install contain 13 semantically equivalent
  rich agents, 13 matching prompts without tool overrides, the exact handoff graph,
  deterministic source/archive/install semantic equivalence and passing local-tool
  invalid-entry fixtures.
- Root and self-host numbering registries contain no legacy numbered-direction row.
- No current `engloop/`, `.engloopkit/`, dual root, or config fallback remains.
- The v2 tool/extension/bundle artifacts are built from this root and validated before
  consumers move.

### TTHP

- Current source: rename tracked `engloop/` to `.engloop/`, then move/evolve
  its tracked initial direction file to root `NORTHSTAR.md`; do not retain a duplicate
  seed or old root.
- Update README, target `.engloop/README.md`, standards, numbering registry, and all
  moved links to v2 living-direction semantics; add visible root `LEARNINGS.md`.
- Add explicit `.engloop/config.json`; test runway remains explicitly `unproven` until
  TTHP Stage 02 chooses and proves a framework—no stack is guessed.
- Replace tracked v1 installed/generated surfaces with tracked v2 surfaces.
- Prove all 13 rich agent headers/prompts and exact graph/matrix from the immutable
  release artifact; retain no v1 agent/prompt/header residue.
- Add root `tthp.code-workspace` containing only `.`.

### EngLoopKit workshop

- Rename its current tracked `engloop/` tree to `.engloop/` with Git history; update
  moved links and place config directly at `.engloop/config.json`.
- Create one reviewed root `NORTHSTAR.md` for curriculum/workshop direction from its
  own README and evidence; do not reuse TTHP product direction.
- Create/preserve visible root `LEARNINGS.md`; update curriculum/docs to ordered v2 and
  remove planned legacy numbered-direction machinery/counter.
- Add config with an explicitly unproven runway; do not select a participant stack.
- Replace tracked v1 generated/install surfaces and add
  `engloop-workshop.code-workspace` containing only `.`.
- Prove all 13 rich agent headers/prompts, diagnostics, and both entry-validation
  modes against the immutable release artifact.

### VerifyExtremeEdgeWithTpcc

- Create one reviewed root `NORTHSTAR.md` for the verification product, not for DAB,
  SqlScriptoria, or TTHP.
- Because no current process root exists, create target `.engloop/` directly with
  `.engloop/config.json`, local README/standards/numbering metadata, and learning
  memory; create visible root `LEARNINGS.md` without changing existing model/SUT
  semantics. Never create an intermediate `engloop/` or `.engloopkit/` tree.
- Use explicit `InitializeGit` tracking mode after the complete checksummed backup;
  final acceptance requires `git ls-files` to own all durable `.engloop/` content.
  This establishes new tracking but does not claim history before the bootstrap.
- Preserve existing `.specexplorerkit/config.json`, model projects, and VS Code SEK
  tasks as independent entry points. Their presence is not retroactive Stage 02/07
  proof; runway state is explicit until re-proven.
- Clean/reinstall v2 surfaces and add
  `VerifyExtremeEdgeWithTpcc.code-workspace` containing only `.`.
- Prove all 13 rich agent headers/prompts, diagnostics, exact targets/edges, and both
  entry-validation modes without changing TPC-C/ExtremeEdge application semantics.
- Because the folder has no Git repository, capture a checksummed archive of all
  migration-owned paths before source-control bootstrap or removal. Rollback restores
  the entire captured set and removes the newly established tracking boundary, never
  selected old files into a partial v2 install.

## Focused and aggregate workspaces

Each focused `.code-workspace` has exactly one relative folder entry (`.`), no sibling
folders, and no absolute machine path. It carries this security posture semantically
(JSON ordering is immaterial):

```json
{
  "settings": {
    "chat.useCustomAgentHooks": true,
    "chat.tools.edits.autoApprove": {
      "**/.github/agents/**": false,
      "**/.github/prompts/**": false,
      "**/.specify/extensions/engloopkit/**": false,
      "**/.config/dotnet-tools.json": false,
      "**/.engloop/config.json": false
    }
  }
}
```

The workspace does not add `"**/*": true`, terminal auto-approval, Bypass Approvals,
or Autopilot. Acceptance runs under Default Approvals. These settings enable the
Preview hook and prevent silent edits to the hook/tool authority; they do not make the
hook the sole stage authority. Folder-open remains supported with hooks unavailable or
disabled in explicitly reduced-assurance mode: every agent body is required and tested
to run the same entry validator, and trusted tooling independently rejects invalid
durable state/evidence acceptance.

The existing mega-workspace is retained as the integration view. Because each root is
independently installed, it may display one 13-command EngLoop registration per root.
Documentation must explain this and direct routine users to the focused entry point.
No shared parent registry, symlink, sibling path, or deduplication extension is added.

## Acceptance assertions per consumer

```text
root NORTHSTAR count == 1
root LEARNINGS count == 1
canonical process root set == { .engloop }
source-control-tracked .engloop roots == 1
.engloop/config.json count == 1
current engloop root count == 0
current .engloopkit directory count == 0
.engloop/out is ignored and durable .engloop content is not ignored
all internal links resolve after the move
current legacy numbered-direction artifacts/templates/counters == 0
registry v2 ID set == exact expected 13
registry old ID count == 0
generated v2 agents == 13 distinct
generated v2 prompts == 13 distinct
generated old agents/prompts == 0
source/installed required agent fields == exact semantic match for 13/13
installed tools/subagent rows == exact ratified matrix for 13/13
installed handoff edges == exact ordered 23-edge graph
handoff targets resolved == 23/23
handoff send values false == 23/23
handoff model overrides == 0
Stage 31 handoffs == 0
Stage 08 edges to 20/30/31 == 0
prompt exact agent selectors == 13/13
prompt tools fields == 0
customization diagnostics attributable to EngLoop == 0
hook-enabled invalid entry blocked before stage action == PASS
hook-disabled assurance mode == reduced
hook-disabled body invalid entry rejection observed == PASS
hook-disabled invalid durable transition/evidence acceptance rejected == PASS
focused workspace chat.useCustomAgentHooks == true
focused workspace unsafe global auto-approval settings == 0
installed artifact digest == released artifact digest
focused workspace folder count == 1 and path == "."
folder-open standalone check == PASS
focused-workspace picker/order check == PASS
```

For unproven runway repositories, Stages 05–08 must fail with `missing-proven-runway`;
that is correct standalone behavior, not a failed installation.

## Rollback boundaries

- **Before a Git migration commit:** reverse/reset the complete captured root
  transaction, including rename, Northstar move, config, link, ignore, and generated
  surfaces. The rollback end state is the exact captured v1 layout, never both roots.
- **After a Git migration commit but before publication:** revert the whole coherent
  migration commit. Do not selectively recreate `engloop/` beside `.engloop/`.
- **Consumer that began without Git:** remove the entire partial target and newly
  initialized tracking boundary, restore the complete checksummed backup, then verify
  its checksum set. Never mix selected old files with a partial v2 root.
- **Prior package restoration:** reinstall exact 1.6.0 only as an explicit,
  operator-visible rollback after the root state has been restored consistently.
- **After publication, before all consumers pass:** halt rollout; do not mutate the
  released 1.7.0 bits. Fix forward under a new 1.x version or explicitly roll affected
  roots back.
- **Runtime behavior:** there is no automatic fallback to v1 commands, old config,
  stale generated files, `engloop/`, `.engloopkit/`, parent roots, or alternate tool
  versions.
