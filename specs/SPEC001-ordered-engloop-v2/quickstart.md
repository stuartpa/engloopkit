# Quickstart: Validate Ordered EngLoop v2

This guide is the post-implementation acceptance path for SPEC001. It proves behavior;
it does not contain implementation code or replace the contracts in [`contracts/`](contracts/).
Run from `C:\boards\brd009\EngLoopKit` on Windows PowerShell unless a step explicitly
says it runs in CI as well.

## Prerequisites

- clean implementation worktree on the SPEC001 implementation branch;
- `.specify/` remains local cache state and is not staged or used as a deliverable;
- .NET SDK selected by root `global.json` (planned pin: 8.0.422);
- Git;
- Spec Kit CLI 0.12.4;
- VS Code Insiders `1.129.0-insider`, commit
  `29d19ddd1af725baf537b6b328843bcdc2d29ba1`, for the 1.7.0 agent-surface acceptance;
- sibling SEK checkout at the exact revision recorded by the implementation, containing
  model-derived negative conformance and the portable single-path generated binding;
- local package source containing the just-built EngLoopKit 1.7.0 tool for pre-release
  consumer validation.

The post-implementation EngLoopKit root must already be in the v2 layout: one tracked
`.engloop/`, config at `.engloop/config.json`, ignored `.engloop/out/`, visible root
`NORTHSTAR.md` and `LEARNINGS.md`, and no current `engloop/` or `.engloopkit/`.

Verify the selected tools:

```powershell
dotnet --version
specify --version
git status --short --branch
git -C ..\SEK rev-parse HEAD
```

Expected: .NET reports the pinned 8.0 SDK; Spec Kit reports 0.12.4; SEK HEAD equals the
recorded pin; only intended SPEC001 implementation changes are present.

Prove the physical root before any config-driven command:

```powershell
$forbiddenRoots = @('.\engloop', '.\.engloopkit') | Where-Object { Test-Path $_ }
if ($forbiddenRoots.Count -ne 0) { throw "Forbidden v2 roots: $($forbiddenRoots -join ', ')" }
if (-not (Test-Path '.\.engloop\config.json' -PathType Leaf)) { throw 'Missing .engloop/config.json' }
if (-not (Test-Path '.\NORTHSTAR.md' -PathType Leaf)) { throw 'Missing root NORTHSTAR.md' }
if (-not (Test-Path '.\LEARNINGS.md' -PathType Leaf)) { throw 'Missing root LEARNINGS.md' }
git ls-files --error-unmatch '.engloop/config.json'
git check-ignore --quiet '.engloop/out/probe.json'
if ($LASTEXITCODE -ne 0) { throw '.engloop/out/ is not ignored' }
```

Expected: no forbidden root is found, config is tracked, both visible entry points
exist, and the transient output probe is ignored. The validator must fail rather than
continue if either old root is introduced alongside or instead of `.engloop/`.

## 0. Prove Spec Kit and VS Code preserve the custom-agent contract

```powershell
pwsh .\scripts\test-spec-kit-agent-preservation.ps1 -SpecKitVersion 0.12.4 -VsCodeVersion 1.129.0-insider -VsCodeCommit 29d19ddd1af725baf537b6b328843bcdc2d29ba1
```

Expected: an ignored disposable harness below
`.engloop/out/spec-kit-agent-canary/` installs only through Spec Kit, loads the tracked
VS Code schema projection, parses source/installed command/agent/prompt YAML with
YamlDotNet 18.1.0, and proves every required scalar/list/map value and absence. It must
cover empty and nonempty `agents`, nested valid `SessionStart` YAML, branching
handoffs, Stage 31's absent `handoffs`, exact prompt agent selection, and prompt
`tools` absence. A passing fixture is removed; a failing fixture/report is retained.
Any mismatch blocks all 13 production headers and routes only to an upstream Spec Kit
capability—never an EngLoopKit-owned generator or post-processing path.

## 1. Restore and build

```powershell
dotnet restore EngLoopKit.slnx
dotnet build EngLoopKit.slnx -c Debug --no-restore
```

Expected: all component, vertical, tool, model, generated-test, and direct-test projects
build with no missing sibling/runtime binding and no generated absolute workstation path.

## 2. Prove the Stage 02 test runway

```powershell
pwsh .\scripts\prove-test-runway.ps1
```

Expected evidence in `.engloop/scaffolds/SCAF001_test-runway.md` (and its machine-readable
fresh report) shows, for one identical terse command:

1. build;
2. exact boundary-test discovery;
3. pass;
4. controlled expected failure with non-zero exit;
5. temporary failure source removed;
6. restored pass;
7. xUnit/Test SDK/collector versions;
8. generated destination `tests/EngLoopKit.Loop.Generated/`.

The script must fail if a different test fails, the intentional run is green, cleanup is
incomplete, or the final run is red.

## 3. Validate source command/package contracts

```powershell
dotnet run --project .\src\EngLoopKit.Tool -- validate config --root .
dotnet run --project .\src\EngLoopKit.Tool -- validate commands --root .
pwsh .\scripts\validate-package.ps1 -Version 1.7.0
```

Expected:

- exactly 13 source/archive IDs in the order 01–08, 20–22, 30–31;
- all 13 command-loop shapes pass;
- zero old IDs in current package surfaces;
- bundle remains composition-only;
- extension, bundle, tool, and catalog versions agree;
- disposable install generates 13 agents and 13 prompts, then removal leaves zero
  EngLoop-owned generated files.

## 3A. Validate exemplary agent surfaces and interaction

```powershell
pwsh .\scripts\validate-agent-surfaces.ps1 -Version 1.7.0 -VsCodeVersion 1.129.0-insider -VsCodeCommit 29d19ddd1af725baf537b6b328843bcdc2d29ba1
```

Expected deterministic source/archive/install rows:

- installed agents: 13/13 in exact lexical command-ID order;
- common source→installed fields: 13/13 semantic matches;
- justified tools/subagent policies: 13/13 exact, with `Explore` resolved 7/7;
- handoffs: exact ordered graph 23/23, targets 23/23, `send: false` 23/23,
  handoff model overrides 0, Stage 31 handoffs 0, Stage 08 operations/stewardship edges 0;
- prompts: exact agent selectors 13/13 and `tools` fields 0;
- deterministic semantic mismatches: 0;
- handoff contract: 23/23 `send: false`, exact targets/prompts, no model override;
- invalid entry: deterministically rejected by exit code 2;
- sensitive generated-agent/prompt/tool-manifest/config paths require manual edit
  approval, with zero broad approval/Autopilot/Bypass settings.

The script emits the deterministic semantic source/archive/disposable-install report for
the same installed digests. UI validation is intentionally not performed or required.

## 4. Validate the independent model and regenerate functional tests

```powershell
dotnet build .\model\EngLoopKit.Model\EngLoopKit.Model.csproj -c Debug
dotnet run --project ..\SEK\src\Sek.Cli -- validate --project .
dotnet run --project ..\SEK\src\Sek.Cli -- explore OrderedEngLoopV2 --project .
pwsh .\scripts\generate-loop-tests.ps1
```

Expected:

- exploration is bounded without silently hitting a bound;
- paths materially branch across delivery, demand-driven operations, and independent
  stewardship;
- model state includes readiness currency, pending repair, pending learning refresh,
  and reachability disposition;
- generated output is replaced only under
  `tests/EngLoopKit.Loop.Generated/`;
- the generated project has one portable SUT binding path and no `SEK_BINDING ?? default`
  or absolute machine path;
- regenerated output matches the committed generated suite byte-for-byte after
  normalization limited to declared deterministic metadata.

## 5. Run Stage 07 generated functional validation only

```powershell
pwsh .\scripts\validate-functional.ps1
```

Expected:

- only `tests/EngLoopKit.Loop.Generated` runs;
- tests drive the real stateful EngLoopKit SUT;
- legal success, model-derived illegal-order rejection, and model-derived invalid-input
  rejection pass;
- required bypass/stale-demand cases are rejected;
- a generated-only Cobertura report and fresh functional `COV002` evidence are produced;
- transient reports are written only below ignored `.engloop/out/`, while durable
  `COV002` evidence is written below `.engloop/coverage/`;
- no readiness verdict appears.

If this step fails, route by evidence to Stage 04, 05, or 06. Do not run Stage 08 to
cover over the failure.

## 6. Validate Stage 08 disposition and readiness

```powershell
dotnet run --project .\src\EngLoopKit.Tool -- validate reachability --root .
pwsh .\scripts\validate-readiness.ps1
```

Expected:

- every Stage 07-unreached path has a reviewed disposition;
- intended gaps have returned through 05–07;
- each deleted coherent residue set has a later green build, architecture check, and
  complete Stage 07 run;
- no new direct test predates full disposition;
- authoritative discovered modules equal configured modules;
- each surviving module has one row with the correct class-specific method, green
  regressions, and at least 95.00% line and 95.00% branch coverage;
- `COV003` reports the sole final PASS/FAIL verdict.

Any missing, stale, ambiguous, zero-evidence, or below-threshold row must yield
`FAIL — NOT READY` and a non-zero exit.

## 7. Validate the Learnings Pyramid

Run fresh clean-context retrieval cases first, using only the on-demand instruction as
the entry point, then validate their recorded IDs:

```powershell
pwsh .\scripts\run-learning-retrieval.ps1
dotnet run --project .\src\EngLoopKit.Tool -- validate learnings --root . --retrieval-results .\.engloop\out\learning-retrieval-results.json
```

Expected:

- all 11 current accepted PM001–PM004 learning IDs are represented (and future accepted
  sources are discovered automatically);
- every card has valid source provenance and an explicit tension state;
- every card appears exactly once in root `LEARNINGS.md`;
- all page→card→source links resolve to the claimed content;
- root index is at most 500 words and 60 nonblank lines;
- retrieval cases cover every card and at least one source from every PM;
- exact expected card/source sets match, with no false provenance;
- instruction has no broad `applyTo: "**"` and no picker registration.

## 8. Run all local tests and freshness gates

```powershell
dotnet test EngLoopKit.slnx -c Debug --no-build
pwsh .\scripts\validate-v2.ps1
```

Expected: direct component/property tests, vertical tests, generated functional tests,
manifest/docs/install contracts, learning validation, evidence freshness, and package
checks all pass. `validate-v2.ps1` must regenerate to a temporary location or compare
fresh digests; it may not accept stale committed output merely because files exist.

## 9. Migrate consumers one at a time

Build final immutable 1.7.0 artifacts first. Record their SHA-256 values, then run the
migration helper for exactly one root per invocation:

```powershell
pwsh .\scripts\validate-package.ps1 -Version 1.7.0 -EmitReleaseManifest .\.engloop\out\release-manifest.json
$release = Get-Content .\.engloop\out\release-manifest.json -Raw | ConvertFrom-Json
$extensionArtifact = (Resolve-Path $release.extension.path).Path
$toolSource = (Resolve-Path $release.tool.source).Path
$consumerMigrations = @(
  [pscustomobject]@{ Root = 'C:\boards\brd009\tthp'; TrackingMode = 'ExistingGit' },
  [pscustomobject]@{ Root = 'C:\boards\brd009\engloop-workshop'; TrackingMode = 'ExistingGit' },
  [pscustomobject]@{ Root = 'C:\boards\brd009\VerifyExtremeEdgeWithTpcc'; TrackingMode = 'InitializeGit' }
)
foreach ($migration in $consumerMigrations) {
  pwsh .\scripts\migrate-consumer.ps1 -Root $migration.Root -TrackingMode $migration.TrackingMode -ExtensionArtifact $extensionArtifact -ToolSource $toolSource -Commit
  if ($LASTEXITCODE -ne 0) { throw "Consumer migration failed: $($migration.Root)" }
}
```

`-Commit` is explicit and local: the helper must not push. It returns success only
after the selected root's coherent migration commit/record and all per-root acceptance
checks complete, so the loop cannot start the next root after a partial or uncommitted
cutover. For each root, inspect the migration report and independently verify:

```powershell
foreach ($migration in $consumerMigrations) {
  $consumerRoot = $migration.Root
  Push-Location $consumerRoot
  try {
    dotnet tool restore
    dotnet tool run engloopkit validate root --root .
    dotnet tool run engloopkit validate config --root .
    dotnet tool run engloopkit validate installation --root .
    if (Test-Path '.\engloop') { throw "Live compatibility root: $consumerRoot" }
    if (Test-Path '.\.engloopkit') { throw "Forbidden config root: $consumerRoot" }
    if (-not (Test-Path '.\.engloop\config.json' -PathType Leaf)) { throw "Missing v2 config: $consumerRoot" }
    if (-not (Test-Path '.\NORTHSTAR.md' -PathType Leaf)) { throw "Missing Northstar: $consumerRoot" }
    if (-not (Test-Path '.\LEARNINGS.md' -PathType Leaf)) { throw "Missing Learnings index: $consumerRoot" }
    git ls-files --error-unmatch '.engloop/config.json'
    if ($LASTEXITCODE -ne 0) { throw "Untracked v2 config: $consumerRoot" }
  }
  finally {
    Pop-Location
  }
}
```

Expected per root:

- one root `NORTHSTAR.md` and no current legacy numbered-direction machinery;
- one source-control-tracked `.engloop/`, one `.engloop/config.json`, ignored
  `.engloop/out/`, one visible root `LEARNINGS.md`, zero current `engloop/`, and zero
  `.engloopkit/` directories;
- existing Git consumers show the visible-tree migration as a rename and all moved
  internal links resolve; the consumer that began without Git retains a full checksum
  backup, explicitly bootstraps tracking, and makes no claim to pre-bootstrap history;
- exact v2 registry set, 13 generated agents, 13 generated prompts, zero old files;
- 13/13 semantic headers, exact 23-edge graph, exact prompt policy, and zero EngLoop-
  owned customization errors or warnings on the pinned VS Code build;
- installed extension/tool digests equal release artifacts;
- folder-open operation succeeds without sibling resolution;
- focused root-local `.code-workspace` contains only `.` and shows one ordered EngLoop
  registration;
- focused hook-enabled invalid entry is mechanically blocked; direct folder-open with
  hooks disabled reports reduced assurance, observes the body check, and proves trusted
  tooling rejects invalid durable state/evidence;
- stages whose runway is explicitly unproven fail with `missing-proven-runway` rather
  than choosing a framework/destination.

For TTHP and the workshop, review the coherent local commit containing the tracked
generated-file replacement together with `git mv engloop .engloop`. For
`VerifyExtremeEdgeWithTpcc`, confirm the local bootstrap commit creates `.engloop/`
directly (never via an intermediate visible root) under explicit `InitializeGit` mode.
No helper invocation pushes. Retain and verify the checksummed pre-migration backup
until tracked standalone acceptance passes.

Failure injection for migration acceptance must prove that a pre-existing target,
forbidden config root, or dual-root state stops before mutation. Rollback validation
must restore the complete captured root transaction and then prove that it did not
leave `engloop/` and `.engloop/` live together.

## 10. Verify the integration workspace and release

Open the existing mega-workspace only after all three standalone checks pass. Confirm it
remains useful as an integration view and may show one independent 13-command set per
root. Confirm documentation points routine work to focused one-root entry points; do
not add cross-root deduplication.

Before publishing:

```powershell
pwsh .\scripts\validate-v2.ps1 -Release
$release = Get-Content .\.engloop\out\release-manifest.json -Raw | ConvertFrom-Json
Get-FileHash (Resolve-Path $release.tool.path) -Algorithm SHA256
Get-FileHash (Resolve-Path $release.extension.path) -Algorithm SHA256
Get-FileHash (Resolve-Path $release.bundle.path) -Algorithm SHA256
```

Publish exactly the validated bits. If any final check fails, do not publish or rebuild
under the same 1.7.0 identity.

## Acceptance result

SPEC001 is implementation-complete only when all local gates, all three standalone
consumer migrations, focused picker checks, and the integration-view check pass. A
passing plan/design review alone does not confer product readiness.
