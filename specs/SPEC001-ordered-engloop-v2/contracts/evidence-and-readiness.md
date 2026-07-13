# Contract: Runway, Functional Evidence, Reachability, and Readiness

- **Feature:** SPEC001
- **Owners:** Stage 02 (runway), Stages 05–07 (functional proof), Stage 08 (disposition,
  direct evidence, final readiness)

## Root configuration boundary

Every governed repository MUST have exactly one tracked `.engloop/` process root and
exactly one `.engloop/config.json`. The implementation will publish a JSON schema and
validate the physical root before parsing config or acting. Required conceptual fields
are:

```json
{
  "schemaVersion": "2.0",
  "artifactRoot": ".engloop",
  "transientOutputRoot": ".engloop/out",
  "northstarPath": "NORTHSTAR.md",
  "validatorCommand": ["dotnet", "tool", "run", "engloopkit"],
  "testRunway": {
    "status": "unproven|proving|proven",
    "framework": null,
    "terseCommand": null,
    "generatedDestination": null,
    "evidenceDigest": null
  },
  "moduleInventory": [],
  "moduleDiscoveryCommand": [],
  "architectureCommand": [],
  "regressionCommand": [],
  "coverageInputs": {}
}
```

The example is shape documentation, not permission to use null values after a stage
requires them. No omitted field has a guessed default. Command vectors are executed
exactly as configured from the selected root; they are not shell-concatenated with
untrusted input.

### Exact discovery algorithm

1. Canonicalize the caller-supplied repository root without walking upward or probing
  siblings.
2. Inspect that root for `.engloop/`, current `engloop/`, and `.engloopkit/` before
  reading any config.
3. Require exactly one canonical `.engloop/` directory and reject either forbidden
  old root, a dual/case-ambiguous state, a symlink escape, or duplicate config.
4. Open only `.engloop/config.json`; require `artifactRoot == ".engloop"` and
  `transientOutputRoot == ".engloop/out"`.
5. Require root-visible `NORTHSTAR.md` and `LEARNINGS.md`, durable `.engloop/` tracking
  evidence, and an ignore rule covering `.engloop/out/` but not durable memory.
6. Resolve all configured paths relative to the selected root and reject escape,
  contradiction, missing identity, or stale evidence.

There is no `.engloopkit/` directory in a conforming v2 root and no live `engloop/`
compatibility tree. Their presence is an error to repair explicitly, never another
discovery candidate.

## Stage 02 runway proof

### Required observations

One proof run records all of the following for the same framework, test project, terse
command, and product revision:

1. selected framework/tool versions and explicit installation/configuration;
2. product and test artifacts build;
3. a meaningful test crosses the actual product boundary;
4. the runner discovers that exact test;
5. the boundary test passes;
6. a controlled temporary defect/expectation makes the same command return non-zero
   and names the expected failing test;
7. the temporary defect is removed;
8. the same command returns zero again;
9. the generated-test destination is created/reserved and recorded.

### Intentional-failure safety

The proof helper MUST use a uniquely named temporary source file, reject a pre-existing
file with that name, and remove it in a guaranteed cleanup path. It may continue after
the expected non-zero test run only if the selected test identity and expected failure
are observed. Build failure, discovery failure, a different test failure, timeout, or
zero exit during the intentional-failure step fails the proof. After cleanup, absence
of the temporary source and restored green execution are both required.

The durable `SCAFxxx` evidence records command vectors, exit codes, selected output
excerpts, file/destination identities, tool versions, source revision, and digests. It
must be tracked below `.engloop/scaffolds/` and must not contain secrets or transient
absolute user paths. Machine-readable scratch/report output goes only below ignored
`.engloop/out/` and cannot authorize a stage unless its digest is promoted into the
required durable evidence record.

## Stable generated-test destination

For EngLoopKit the destination is exactly:

`tests/EngLoopKit.Loop.Generated/`

Contract:

- Stage 06 replaces generator-owned files in that directory atomically.
- Human-authored helpers do not live in the generated directory.
- Generated source is not manually patched.
- The generated project has one portable SUT binding path (project-reference/copy to
  output or another single explicit generated contract).
- Absolute workstation paths, `environment ?? default`, stale-DLL search, or sibling
  lookup are forbidden.
- Generation records the pinned SEK revision and input graph/model/CORD digests.
- A failed generation leaves the prior directory unavailable for validation; Stage 07
  cannot reuse it as stale evidence.

## Stage 05 model gate

PASS requires:

- current final-form product and architecture evidence;
- a behavior-level independent model of the stateful vertical;
- interacting state and real ordering constraints;
- actions/effects/invariants plus legal guards;
- explicit illegal-order and invalid-input rejection semantics;
- finite domains/bounds separated from product illegality;
- no model of pure components and no imported SUT transition table.

## Stage 06 exploration/generation gate

PASS requires:

- model and CORD validate under the pinned SEK revision;
- exploration does not silently truncate at a bound;
- materially distinct branches are present;
- positive legal, illegal-order negative, and invalid-input negative witnesses exist;
- every required modeled precondition class is represented;
- generated files are written only to the Stage 02 destination;
- generated project builds independently of the SEK source tree once generated.

## Stage 07 functional validation gate

Stage 07 runs only the freshly generated project against the real stateful SUT. Its
coverage invocation excludes hand-written/direct test projects. It records:

- model, CORD, graph, generator, suite, and SUT digests;
- positive/negative test identities and results;
- explored state/transition/negative-edge/branch counts;
- generated-suite-only line/branch reachability by production module/path;
- duration and deterministic options;
- `functional-pass` or `functional-fail`.

It MUST NOT emit `READY`, include direct unit results, or merge a prior coverage report.

## Stage 08 exact algorithm

The implementation and command MUST execute this sequence:

1. Load current Stage 07 generated-only reachability.
2. Enumerate every unreached production path from the authoritative module/source map.
3. Classify every path; do not add a new direct test.
4. For an intended gap, attach authoritative intent and return through 05→06→07.
5. For unsupported residue, attach proof of no requirement and no runtime entry, then
   delete one coherent set.
6. After each deletion set, run build, architecture command, and complete Stage 07.
7. Stop on any red result; do not continue classifying against stale source.
8. Repeat until every surviving path is functionally justified or explicitly reviewed
   as direct-only.
9. Only then add/adjust unit/property tests for surviving units.
10. Run whole-product coverage and compute the complete Readiness Inventory.

A path with ambiguous reflection, serialization, configuration, recovery, platform, or
public-contract entry remains unclassified and blocks Stage 08.

## Coverage separation

Two independently named reports are required:

| Report | Test source | Purpose | May authorize readiness? |
|---|---|---|---|
| Functional reachability | Generated project only | Stage 07 conformance and classification input | No |
| Whole-product coverage | Generated + allowed direct/property/regression suites after disposition | Stage 08 per-module line/branch and regression evidence | Only as one input to the final gate |

Reports MUST carry input/test/source digests. The readiness tool rejects report mixing,
missing modules, duplicate module identities, and generated-only evidence contaminated
by direct tests.

## Module inventory and class method

The configured module set and the authoritative discovery command's emitted set MUST be
identical. Each row is classified:

- `component`: domain-free; direct unit/property evidence;
- `pure-value`: direct unit/property evidence;
- `domain-vertical`: representative real-SUT Stage 05–07 evidence plus justified direct
  tests after disposition.

All classes require current architecture, regressions, and at least 95.00% measured
line and 95.00% measured branch coverage per surviving module. No rounding up and no
aggregate substitution.

## Readiness computation

For each authoritative module `m`:

```text
rowPass(m) =
    architectureCurrent(m)
    AND regressionsGreen(m)
    AND reachabilityComplete(m)
    AND verificationMethodSatisfied(m.class)
    AND lineCoverage(m) >= 95.00
    AND branchCoverage(m) >= 95.00
```

```text
Readiness PASS =
    moduleSetsEqual
    AND inventory has exactly one row per module
    AND every rowPass(m)
    AND Stage07 evidence current
    AND deletion revalidation complete
```

Any unknown/missing/stale/failing value is false. A PASS report includes every row and
all evidence digests. A FAIL report says `NOT READY` and lists exact blockers.

## Agent UX, package, and installation evidence

Custom-agent evidence is release/install evidence with its own typed result. It MUST
cover:

- the passing early Spec Kit preservation experiment and pinned generator identity;
- 13/13 source command headers and 13/13 installed agent headers with semantic field
  preservation;
- the exact tools/subagent matrix and 23-edge handoff graph;
- exact versioned `SessionStart` hooks, unconditional body checks, assurance-mode
  reporting, and independent trusted durable-stage gates;
- 13 generated prompts selecting their exact agent with zero `tools` overrides;
- resolved handoff/`Explore` targets and exact ordered source/archive/install command
  identities;
- controlled invalid-entry mechanical rejection with Preview hooks enabled; with hooks
  disabled, observed body rejection plus trusted-tool rejection of every invalid
  durable transition/evidence attempt;
- source/archive/install digests, Spec Kit version, VS Code version, and focused
  workspace settings/approval posture.

This evidence can block packaging, publication, or consumer migration. It does not by
itself make the product ready. In particular, installed command identities or a passing
hook are not a `ReadinessInventoryRow` and cannot replace
architecture, behavioral, reachability, direct-test, coverage, or regression evidence.

If an authoritative `.engloop/config.json` explicitly inventories a production module
that implements agent parsing/validation, that module's code and tests contribute only
through its ordinary Stage 08 row and applicable structural gate. This does not turn
external package/install observations into coverage or readiness. No unconfigured
agent-UX evidence is silently added to or omitted from the product gate.

Release acceptance is therefore a separate conjunction:

```text
ReleaseCandidate PASS =
    Readiness PASS
    AND agentSourceSemantics PASS
    AND packageArchiveSemantics PASS
    AND disposableInstallSemantics PASS
    AND customizationDiagnostics PASS
    AND strictHookEntryFixture PASS
    AND reducedAssuranceBodyAndDurableGateFixtures PASS
    AND releaseArtifactDigestsCurrent
```

A release-evidence failure reports `RELEASE BLOCKED`; it does not rewrite a current
product-readiness result. Conversely, a Stage 08 PASS cannot waive a red release row.
This separation applies PM001/LEARN001–003: each claim is the output of its own complete
inventory, never narration from nearby progress.

## Required validation commands

The final implementation MUST expose objective commands for these checkpoints (exact
CLI spelling may be finalized with the tool's help text but cannot have alternate
semantics):

- validate the exact hidden-root/config/output layout and reject forbidden roots;
- validate root config and authoritative module equality;
- prove/run Stage 02 runway;
- validate model/CORD and regenerate to the configured destination;
- run generated-only functional validation and coverage;
- validate reachability disposition completeness;
- run whole-product tests/coverage;
- compute and verify readiness;
- run `validate agent-entry --stage <exact-command-id> --root <selected-root>` through
  the accepted root-local tool, returning 2 for a rejected hook entry and nonzero for a
  rejected body entry;
- run `validate agent-surfaces --root <source-root> --installed-root <fixture-root>` to
  emit the semantic source/install/prompt/target comparison; and
- run deterministic semantic projection and disposable-install validation for the same
  installed digests. UI validation is intentionally not performed.

Each command exits non-zero on failure and writes no PASS state when evidence is
incomplete.
