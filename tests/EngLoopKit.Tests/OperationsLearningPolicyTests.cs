using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using EngLoopKit.Core;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class OperationsLearningPolicyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "elk-operations-learning-" + Guid.NewGuid().ToString("N"));
    private const string PmPath = ".engloop/postmortems/PM005_synthetic.md";
    private const string IncidentPath = ".engloop/incidents/IN005_synthetic.md";
    private const string RoutePath = ".engloop/repairs/PM005-RPI001.route.json";
    private const string ClosePath = ".engloop/repairs/PM005-RPI001.close.json";
    private const string ReceiptPath = ".engloop/out/repair-gates/PM005-RPI001.receipt.json";

    public OperationsLearningPolicyTests()
    {
        foreach (var path in new[]
        {
            ".engloop/postmortems", ".engloop/learnings/cards", ".engloop/out", ".engloop/repairs",
            ".engloop/incidents", ".engloop/coverage", "src", "model", "tests/generated"
        }) Directory.CreateDirectory(Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar)));
        File.WriteAllText(Path.Combine(_root, "NORTHSTAR.md"), "# North Star\n\nNever hide fallback behavior.\n");
        File.WriteAllText(Path.Combine(_root, "LEARNINGS.md"), "# Learnings\n\n[reliability](.engloop/learnings/cards/reliability.md)\n");
        File.WriteAllText(Path.Combine(_root, ".engloop", "postmortems", "PM001_prior.md"), "## Learnings\n- **LEARN001** — fail closed\n");
        File.WriteAllText(Path.Combine(_root, ".engloop", "learnings", "cards", "reliability.md"), "# Reliability\n\n## Source learnings\n\nPM001/LEARN001\n\n## Tensions\nnone known\n");
        File.WriteAllText(Path.Combine(_root, ".engloop", "learnings", "README.md"), "# Coverage\n\n| PM001 | 1 | reliability |\n");
        File.WriteAllText(Path.Combine(_root, "src", "fixture.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        File.WriteAllText(Path.Combine(_root, "model", "Model.cs"), "// model fixture\n");
        File.WriteAllText(Path.Combine(_root, "model", "Config.cord"), "config Main { action all Loop; }\n");
        File.WriteAllText(Path.Combine(_root, ".engloop", "config.json"), """
        {
          "schemaVersion":"2.0",
          "productId":"fixture",
          "artifactRoot":".engloop",
          "transientOutputRoot":".engloop/out",
          "northstarPath":"NORTHSTAR.md",
          "validatorCommand":["dotnet","tool","run","engloopkit","--"],
          "moduleDiscoveryCommand":["dotnet","--version"],
          "architectureCommand":["dotnet","--version"],
          "regressionCommand":["dotnet","--version"],
          "coverageInputs":{"wholeProduct":"src/fixture.csproj"},
                    "testRunway":{
                        "status":"proven",
                        "framework":"xunit",
                        "terseCommand":["dotnet","--version"],
                        "boundaryTest":"Fixture.Boundary",
                        "generatedDestination":"tests/generated",
                        "evidenceDigest":"fixture-digest",
                        "provenAtRevision":"content:fixture-digest"
                    },
          "moduleInventory":[{"id":"core","path":"src/fixture.csproj"}]
        }
        """);
        File.WriteAllText(Path.Combine(_root, ".gitignore"), ".engloop/out/\n.engloop/readiness/\n");
        WriteIncident(consulted: true);
        WriteGateScript(pass: true);
        Git("init");
        Git("config", "user.email", "operations@example.invalid");
        Git("config", "user.name", "Operations Test");
        Git("add", ".");
        Git("commit", "-m", "fixture baseline");
    }

    [Fact]
    public void ContradictedRule_cannotCompleteWithoutPyramidDecision()
    {
        var path = WritePostmortem(PyramidMode.MissingDecision);
        var result = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
        Assert.False(result.Passed);
        Assert.Contains(result.Failures, failure => failure.Contains("pyramid", StringComparison.Ordinal));
    }

    [Fact]
    public void ContradictedRule_canRecordExplicitNoChangeWithRealIncidentBinding()
    {
        var path = WritePostmortem(PyramidMode.NoChange);
        var result = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Failures));
        Assert.Equal("NO-CHANGE", result.Contract!.PyramidDecision);
        Assert.Single(result.Contract.Incidents);
        Assert.Equal(0, ValidationCommands.ValidatePostmortemLearning(["--root", _root, "--incidents", "IN005", "--postmortem", path]));
        Assert.Equal(0, ValidationCommands.ValidateLearnings(["--root", _root]));
    }

    [Fact]
    public void DeferredIncident_cannotBeSelectedForPostmortem()
    {
        WriteIncident(consulted: false);
        var path = WritePostmortem(PyramidMode.NoChange);
        var result = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
        Assert.Contains(result.Failures, failure => failure.Contains("incident-learning-consultation-still-deferred", StringComparison.Ordinal));
    }

    [Fact]
    public void UpdatedContradictedRule_recomputesRetrievalAgainstCurrentInputs()
    {
        var cardPath = Path.Combine(_root, ".engloop", "learnings", "cards", "reliability.md");
        File.WriteAllText(cardPath, File.ReadAllText(cardPath).Replace(
            "PM001/LEARN001\n\n## Tensions",
            "PM001/LEARN001\nPM005/LEARN001\n\n## Tensions",
            StringComparison.Ordinal));
        File.AppendAllText(Path.Combine(_root, ".engloop", "learnings", "README.md"), "| PM005 | 1 | reliability |\n");
        var path = WritePostmortem(PyramidMode.Updated);
        WriteRetrievalEvidence(actualSource: "PM005/LEARN001");
        var result = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Failures));
        Assert.Equal(0, ValidationCommands.ValidateLearnings(["--root", _root]));

        WriteRetrievalEvidence(actualSource: "PM001/LEARN001");
        var falsePass = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
        Assert.Contains(falsePass.Failures, failure => failure.Contains("retrieval-source-set-mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryListedRepairItem_requiresItsOwnLearningContract()
    {
        var path = WritePostmortem(PyramidMode.NoChange);
        var full = Full(path);
        File.WriteAllText(full, File.ReadAllText(full).Replace(
            "| RPI001 | Add the rejection gate. | OPEN |",
            "| RPI001 | Add the rejection gate. | OPEN |\n| RPI002 | Add another gate. | OPEN |",
            StringComparison.Ordinal));
        var result = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
        Assert.Contains("repair-item-learning-contract-missing:RPI002", result.Failures);
    }

    [Fact]
    public void RelevantSekEscape_carriesScenarioCordRepairAndSekGateIntoRoute()
    {
        var path = WritePostmortem(PyramidMode.NoChange, sekRelevant: true);
        var result = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Failures));
        Assert.Equal("RELEVANT", result.Contract!.SekEscape.Applicability);
        Assert.Equal("CORD-DOMAIN-GAP", result.Contract.SekEscape.EscapeClass);
        Assert.Equal("SEK-SCENARIO:silent-fallback", result.Contract.SekEscape.ScenarioId);
        WriteRoute(result.Contract);
        var route = OperationsLearningPolicy.ValidateRepairAcceptance(_root, path, "RPI001", ["RULE:reliability"], RoutePath, "route", currentReadinessPass: false);
        Assert.True(route.Passed, string.Join(Environment.NewLine, route.Failures));

        File.Delete(Full("model/Config.cord"));
        var missingCord = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
        Assert.Contains(missingCord.Failures, failure => failure.StartsWith("sek-cord-path-invalid:", StringComparison.Ordinal));
    }

    [Fact]
    public void SekEscapeAnalysis_rejectsApplicabilityAndRepairMismatchMatrix()
    {
        void Reject(bool relevant, string oldValue, string newValue, string expected)
        {
            var path = WritePostmortem(PyramidMode.NoChange, relevant);
            File.WriteAllText(Full(path), File.ReadAllText(Full(path)).Replace(oldValue, newValue, StringComparison.Ordinal));
            var result = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
            Assert.Contains(result.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
        }

        void RejectLast(bool relevant, string oldValue, string newValue, string expected)
        {
            var path = WritePostmortem(PyramidMode.NoChange, relevant);
            var text = File.ReadAllText(Full(path));
            var index = text.LastIndexOf(oldValue, StringComparison.Ordinal);
            Assert.True(index >= 0, "Expected mutation text was not found.");
            text = text.Remove(index, oldValue.Length).Insert(index, newValue);
            File.WriteAllText(Full(path), text);
            var result = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
            Assert.Contains(result.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
        }

        Reject(true, "- **SEK version:** `0.1.3`", "- **SEK version:** `0.1.2`", "sek-version-must-be-0.1.3");
        Reject(true, "- **SEK verification class:** `STATEFUL-VERTICAL`", "- **SEK verification class:** `INFRASTRUCTURE`", "sek-relevant-requires-stateful-vertical");
        Reject(true, "- **SEK escape class:** `CORD-DOMAIN-GAP`", "- **SEK escape class:** `NOT-RELEVANT`", "sek-relevant-escape-class-invalid");
        Reject(true, "- **SEK scenario ID:** `SEK-SCENARIO:silent-fallback`", "- **SEK scenario ID:** `bad`", "sek-scenario-id-invalid");
        Reject(true, "- **SEK model paths:** `model/Model.cs`", "- **SEK model paths:** `model/Missing.cs`", "sek-model-path-invalid");
        Reject(true, "- **SEK model paths:** `model/Model.cs`", "- **SEK model paths:** `model/Config.cord`", "sek-model-path-extension-invalid");
        Reject(true, "- **SEK CORD paths:** `model/Config.cord`", "- **SEK CORD paths:** `model/Missing.cord`", "sek-cord-path-invalid");
        Reject(true, "- **SEK generated suite path:** `tests/generated`", "- **SEK generated suite path:** `tests/missing`", "sek-generated-suite-invalid");
        Reject(true, "Add the fallback decision to the finite Cord domain and regenerate a rejection scenario with the real binding.", "weak", "sek-required-repair-missing");
        Reject(true, "- **SEK applicability:** `RELEVANT`", "- **SEK applicability:** `NOT-RELEVANT`", "sek-not-relevant");
        RejectLast(true, "- **SEK scenario ID:** `SEK-SCENARIO:silent-fallback`", "- **SEK scenario ID:** `SEK-SCENARIO:other`", "repair-contract-sek-scenario-mismatch");
        Reject(true, "- **SEK repair requirement:** Correct the Cord domain, regenerate the suite, and prove the silent fallback scenario is rejected by the real binding.", "- **SEK repair requirement:** weak", "repair-contract-sek-repair-missing");
        Reject(true, "- **SEK verification gate:** `[\"dotnet\", \"tool\", \"run\", \"sek\", \"version\"]`", "- **SEK verification gate:** `[\"dotnet\", \"test\"]`", "repair-contract-executable-gate-must-equal-sek-gate");
        Reject(true, "- **Executable gate:** `[\"dotnet\", \"tool\", \"run\", \"sek\", \"version\"]`", "- **Executable gate:** `[\"dotnet\", \"test\"]`", "repair-contract-sek-gate-does-not-invoke-sek");

        Reject(false, "- **SEK version:** `NOT-REQUIRED`", "- **SEK version:** `0.1.3`", "sek-not-relevant-version-must-be-not-required");
        Reject(false, "- **SEK verification class:** `INFRASTRUCTURE`", "- **SEK verification class:** `STATEFUL-VERTICAL`", "sek-not-relevant-class-invalid");
        Reject(false, "- **SEK escape class:** `NOT-RELEVANT`", "- **SEK escape class:** `MODEL-GAP`", "sek-not-relevant-escape-class-invalid");
        Reject(false, "- **SEK scenario ID:** `NOT-REQUIRED`", "- **SEK scenario ID:** `SEK-SCENARIO:unexpected`", "sek-not-relevant-fields-must-be-not-required");
        RejectLast(false, "- **SEK applicability:** `NOT-RELEVANT`", "- **SEK applicability:** `RELEVANT`", "repair-contract-sek-applicability-mismatch");
        Reject(false, "- **SEK scenario ID:** `NOT-REQUIRED`", "- **SEK scenario ID:** `SEK-SCENARIO:unexpected`", "repair-contract-sek-fields-must-be-not-required");
    }

    [Fact]
    public void ToolExecutesExactGate_andCloseRequiresImmutableRouteReceiptAndReadiness()
    {
        var pmPath = WritePostmortem(PyramidMode.NoChange);
        var pm = OperationsLearningPolicy.ValidatePostmortem(_root, pmPath, ["IN005"]);
        Assert.True(pm.Passed, string.Join(Environment.NewLine, pm.Failures));
        WriteRoute(pm.Contract!);
        Assert.Equal(0, ValidationCommands.ValidateRepairLearning(["--root", _root, "--phase", "route", "--postmortem", pmPath, "--rpi", "RPI001", "--rules", "RULE:reliability", "--acceptance", RoutePath]));
        WriteReadiness();

        Assert.Equal(0, Program.Main(["repair-gate", "execute", "--root", _root, "--postmortem", pmPath, "--rpi", "RPI001", "--rules", "RULE:reliability", "--route", RoutePath, "--receipt", ReceiptPath]));
        WriteClose(pm.Contract!);
        Assert.Equal(0, ValidationCommands.ValidateRepairLearning(["--root", _root, "--phase", "close", "--postmortem", pmPath, "--rpi", "RPI001", "--rules", "RULE:reliability", "--acceptance", ClosePath]));

        var routeText = File.ReadAllText(Full(RoutePath));
        File.WriteAllText(Full(RoutePath), routeText + " ");
        Assert.NotEqual(0, ValidationCommands.ValidateRepairLearning(["--root", _root, "--phase", "close", "--postmortem", pmPath, "--rpi", "RPI001", "--rules", "RULE:reliability", "--acceptance", ClosePath]));
    }

    [Fact]
    public void FailedGateProducesFailReceipt_andCannotClose()
    {
        WriteGateScript(pass: false);
        var pmPath = WritePostmortem(PyramidMode.NoChange);
        var pm = OperationsLearningPolicy.ValidatePostmortem(_root, pmPath, ["IN005"]);
        WriteRoute(pm.Contract!);
        WriteReadiness();
        Assert.Equal(1, Program.Main(["repair-gate", "execute", "--root", _root, "--postmortem", pmPath, "--rpi", "RPI001", "--rules", "RULE:reliability", "--route", RoutePath, "--receipt", ReceiptPath]));
        using var receipt = JsonDocument.Parse(File.ReadAllText(Full(ReceiptPath)));
        Assert.Equal("FAIL", receipt.RootElement.GetProperty("verdict").GetString());
    }

    [Fact]
    public void RepairGate_rejectsTimeoutRpiAndExistingOutputMatrix()
    {
        var pmPath = WritePostmortem(PyramidMode.NoChange);
        var pm = OperationsLearningPolicy.ValidatePostmortem(_root, pmPath, ["IN005"]).Contract!;
        WriteRoute(pm);
        Assert.Equal(1, ValidationCommands.ExecuteRepairGate(["execute", "--root", _root, "--postmortem", pmPath, "--rpi", "RPI001", "--rules", "RULE:reliability", "--route", RoutePath, "--receipt", ReceiptPath, "--timeout-seconds", "bad"]));
        Assert.Equal(1, ValidationCommands.ExecuteRepairGate(["execute", "--root", _root, "--postmortem", pmPath, "--rpi", "RPI999", "--rules", "RULE:reliability", "--route", RoutePath, "--receipt", ReceiptPath]));

        Directory.CreateDirectory(Path.GetDirectoryName(Full(ReceiptPath))!);
        File.WriteAllText(Full(ReceiptPath), "existing");
        Assert.Equal(1, ValidationCommands.ExecuteRepairGate(["execute", "--root", _root, "--postmortem", pmPath, "--rpi", "RPI001", "--rules", "RULE:reliability", "--route", RoutePath, "--receipt", ReceiptPath]));
        File.Delete(Full(ReceiptPath));

        File.WriteAllText(Full("gate.ps1"), "[Threading.Thread]::Sleep(5000); exit 0\n");
        Assert.Equal(1, ValidationCommands.ExecuteRepairGate(["execute", "--root", _root, "--postmortem", pmPath, "--rpi", "RPI001", "--rules", "RULE:reliability", "--route", RoutePath, "--receipt", ReceiptPath, "--timeout-seconds", "1"]));
        using var receipt = JsonDocument.Parse(File.ReadAllText(Full(ReceiptPath)));
        Assert.False(receipt.RootElement.GetProperty("completed").GetBoolean());
        Assert.Equal(-1, receipt.RootElement.GetProperty("exitCode").GetInt32());
    }

    [Theory]
    [InlineData("head")]
    [InlineData("index")]
    public void GateThatMutatesSourceIdentity_isRejected(string mutation)
    {
        var pmPath = WritePostmortem(PyramidMode.NoChange);
        var pm = OperationsLearningPolicy.ValidatePostmortem(_root, pmPath, ["IN005"]);
        WriteRoute(pm.Contract!);
        WriteReadiness();
        File.WriteAllText(Full("gate.ps1"), mutation == "head"
            ? "git commit --allow-empty -m gate-head-mutation; exit 0\n"
            : "Set-Content index-only.txt changed; git add index-only.txt; exit 0\n");

        Assert.Equal(1, Program.Main(["repair-gate", "execute", "--root", _root, "--postmortem", pmPath, "--rpi", "RPI001", "--rules", "RULE:reliability", "--route", RoutePath, "--receipt", ReceiptPath]));
        using var receipt = JsonDocument.Parse(File.ReadAllText(Full(ReceiptPath)));
        Assert.Equal("FAIL", receipt.RootElement.GetProperty("verdict").GetString());
    }

    [Fact]
    public void WorktreeDigests_changeWhenDirtyFileBytesChangeWithoutStatusShapeChange()
    {
        var tracked = Full("src/tracked.txt");
        var untracked = Full("src/untracked.txt");
        File.WriteAllText(tracked, "baseline\n");
        Git("add", "src/tracked.txt");
        Git("commit", "-m", "tracked digest fixture");
        File.WriteAllText(tracked, "dirty-one\n");
        File.WriteAllText(untracked, "untracked-one\n");
        var readiness1 = OperationsLearningPolicy.ComputeReadinessWorktreeDigest(_root);
        var receipt1 = OperationsLearningPolicy.ComputeGitStatusDigest(_root, []);
        File.WriteAllText(tracked, "dirty-two-with-different-bytes\n");
        File.WriteAllText(untracked, "untracked-two-with-different-bytes\n");
        var readiness2 = OperationsLearningPolicy.ComputeReadinessWorktreeDigest(_root);
        var receipt2 = OperationsLearningPolicy.ComputeGitStatusDigest(_root, []);
        Assert.NotEqual(readiness1, readiness2);
        Assert.NotEqual(receipt1, receipt2);
    }

    [Fact]
    public void IncidentValidation_rejectsNotStabilizedAndUnrelatedCheckedBox()
    {
        var path = Full(IncidentPath);
        var text = File.ReadAllText(path)
            .Replace("**Status:** STABILIZED", "**Status:** NOT STABILIZED", StringComparison.Ordinal)
            .Replace("- [x] User workflows unblocked: synthetic user workflow completed successfully without errors.", "- [ ] User workflows unblocked: pending.\n\n## Unrelated\n\n- [x] unrelated: irrelevant evidence.", StringComparison.Ordinal);
        File.WriteAllText(path, text);
        var result = OperationsLearningPolicy.ValidateIncidentContext(_root, IncidentPath, requireConsulted: true);
        Assert.Contains("incident-not-stabilized", result.Failures);
        Assert.Contains(result.Failures, failure => failure.StartsWith("incident-stability-check-invalid:", StringComparison.Ordinal));
    }

    [Fact]
    public void Retrieval_rejectsNonexistentCardAndSource_evenWhenExpectedEqualsObserved()
    {
        var path = WritePostmortem(PyramidMode.Updated);
        var casesPath = Full(".engloop/learnings/retrieval-cases.json");
        var observedPath = Full(".engloop/learnings/retrieval-observed.json");
        File.WriteAllText(casesPath, "{\"cases\":[{\"id\":\"fake\",\"expectedCardIds\":[\"does-not-exist\"],\"expectedSourceIds\":[\"PM999/LEARN999\"],\"expectGap\":false}]}\n");
        File.WriteAllText(observedPath, "{\"results\":[{\"id\":\"fake\",\"actualCardIds\":[\"does-not-exist\"],\"actualSourceIds\":[\"PM999/LEARN999\"],\"verdict\":\"PASS\"}]}\n");
        var receipt = new
        {
            casesPath = ".engloop/learnings/retrieval-cases.json", casesSha256 = Sha(casesPath),
            observedResultsPath = ".engloop/learnings/retrieval-observed.json", observedResultsSha256 = Sha(observedPath),
            learningsSha256 = Sha(Full("LEARNINGS.md")), cardsDigest = OperationsLearningPolicy.ComputeCardsDigest(Full(".engloop/learnings/cards"))
        };
        File.WriteAllText(Full(".engloop/out/retrieval.json"), JsonSerializer.Serialize(receipt));
        var result = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
        Assert.Contains(result.Failures, failure => failure.Contains("unknown-card", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("source-not-cited", StringComparison.Ordinal));
    }

    [Fact]
    public void PostmortemValidation_rejectsMalformedContractMatrix()
    {
        void Reject(string oldValue, string newValue, string expected)
        {
            var path = WritePostmortem(PyramidMode.NoChange);
            File.WriteAllText(Full(path), File.ReadAllText(Full(path)).Replace(oldValue, newValue, StringComparison.Ordinal));
            var result = OperationsLearningPolicy.ValidatePostmortem(_root, path, ["IN005"]);
            Assert.Contains(result.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
        }

        Reject("**Status:** COMPLETE", "**Status:** OPEN", "postmortem-status-not-complete");
        Reject("## Root causes", "## Cause analysis", "heading-count-invalid:## Root causes");
        Reject("## Learnings", "## Learning notes", "heading-count-invalid:## Learnings");
        Reject("## Repair Items", "## Repairs", "heading-count-invalid:## Repair Items");
        Reject("## Direction and Learning-Pyramid Consultation", "## Direction only", "heading-count-invalid:## Direction and Learning-Pyramid Consultation");
        Reject("- **Direction alignment:** `ALIGNED`", "- **Direction alignment:** `UNKNOWN`", "invalid-field:direction-alignment");
        Reject("Preserve the fail closed reliability boundary for every repair.", "weak", "missing-or-weak-field:direction-decision");
        Reject("- **Pyramid decision:** `NO-CHANGE`", "- **Pyramid decision:** `UNKNOWN`", "invalid-field:pyramid-decision");
        Reject("- **Historical coverage decision:** `NO-CHANGE`", "- **Historical coverage decision:** `UPDATED`", "historical-coverage-decision-mismatch");
        Reject("- **Historical coverage path:** `NOT-REQUIRED`", "- **Historical coverage path:** `.engloop/learnings/README.md`", "historical-coverage-path-must-be-not-required");
        Reject("- **Changed pyramid paths:** `NOT-REQUIRED`", "- **Changed pyramid paths:** `.engloop/learnings/cards/reliability.md`", "changed-pyramid-paths-must-be-not-required");
        Reject("- **Retrieval impact:** `UNCHANGED`", "- **Retrieval impact:** `UNKNOWN`", "invalid-field:retrieval-impact");
        Reject("- **Retrieval evidence:** `NOT-REQUIRED`", "- **Retrieval evidence:** `.engloop/out/fake.json`", "retrieval-evidence-must-be-not-required");
        Reject("`RULE:reliability`", "`RULE:Bad`", "invalid-rule-id");
        Reject("| `CONTRADICTED` |", "| `UNKNOWN` |", "invalid-rule-disposition");
        Reject("The implementation silently used a fallback in the selected incident.", "weak", "missing-rule-incident-evidence");
        Reject("NO-CHANGE: retain the existing stronger rule and repair its executable gate.", "weak", "missing-rule-pyramid-action");
        Reject("| `reliability` |", "| `other-card` |", "rule-card-id-mismatch");
        Reject("- **Rule IDs:** `RULE:reliability`", "- **Rule IDs:** `RULE:missing`", "repair-contract-rule-not-dispositioned");
        Reject("[\"pwsh\", \"-NoProfile\", \"-File\", \"gate.ps1\"]", "not-json", "repair-contract-gate-invalid");
        Reject("The forbidden fallback is rejected and state remains consistent for users.", "weak", "repair-contract-gate-proof-missing");

        var valid = WritePostmortem(PyramidMode.NoChange);
        Assert.False(OperationsLearningPolicy.ValidatePostmortem(_root, Path.GetFullPath(Full(valid)), ["IN005"]).Passed);
        Assert.False(OperationsLearningPolicy.ValidatePostmortem(_root, ".engloop/postmortems/missing.md", ["IN005"]).Passed);
        var invalidName = ".engloop/postmortems/not-a-pm.md";
        File.Copy(Full(valid), Full(invalidName));
        Assert.Contains("postmortem-filename-invalid", OperationsLearningPolicy.ValidatePostmortem(_root, invalidName, ["IN005"]).Failures);
    }

    [Fact]
    public void IncidentValidation_rejectsMalformedContextMatrix()
    {
        void Reject(string oldValue, string newValue, string expected, bool requireConsulted = true)
        {
            WriteIncident(consulted: true);
            File.WriteAllText(Full(IncidentPath), File.ReadAllText(Full(IncidentPath)).Replace(oldValue, newValue, StringComparison.Ordinal));
            var result = OperationsLearningPolicy.ValidateIncidentContext(_root, IncidentPath, requireConsulted);
            Assert.Contains(result.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
        }

        Reject("## Verification (stability, not root-cause fix)", "## Verification", "heading-count-invalid");
        Reject("## Direction and learning context", "## Context", "heading-count-invalid");
        Reject("- **Learning context:** `CONSULTED`", "- **Learning context:** `UNKNOWN`", "invalid-field:learning-context");
        Reject("- **Rule IDs:** `RULE:reliability`", "- **Rule IDs:** `bad`", "incident-rule-ids-invalid");
        Reject("- **Source IDs:** `PM001/LEARN001`", "- **Source IDs:** `bad`", "incident-source-ids-invalid");
        Reject("- **Deferral reason:** NOT-REQUIRED", "- **Deferral reason:** emergency reason that should not remain", "incident-deferral-reason-must-be-not-required");
        Reject("Health checks passing: synthetic service health checks remained continuously green.", "Health checks passing: weak", "incident-stability-check-invalid");
        Reject("No fresh errors in the watch window: synthetic watch window reported zero errors.", "No fresh errors in the watch window: weak", "incident-stability-check-invalid");

        WriteIncident(consulted: false);
        Assert.Contains("incident-learning-consultation-still-deferred", OperationsLearningPolicy.ValidateIncidentContext(_root, IncidentPath, true).Failures);
        var invalidName = ".engloop/incidents/not-an-incident.md";
        File.Copy(Full(IncidentPath), Full(invalidName));
        Assert.Contains("incident-filename-invalid", OperationsLearningPolicy.ValidateIncidentContext(_root, invalidName, false).Failures);
        Assert.False(OperationsLearningPolicy.ValidateIncidentContext(_root, Path.GetFullPath(Full(IncidentPath)), false).Passed);
    }

    [Fact]
    public void RepairClose_rejectsRouteReceiptAndCloseMutationMatrix()
    {
        var pmPath = WritePostmortem(PyramidMode.NoChange);
        var pm = OperationsLearningPolicy.ValidatePostmortem(_root, pmPath, ["IN005"]).Contract!;
        WriteRoute(pm);
        WriteReadiness();
        Assert.Equal(0, Program.Main(["repair-gate", "execute", "--root", _root, "--postmortem", pmPath, "--rpi", "RPI001", "--rules", "RULE:reliability", "--route", RoutePath, "--receipt", ReceiptPath]));
        WriteClose(pm);
        var routeOriginal = File.ReadAllText(Full(RoutePath));
        var receiptOriginal = File.ReadAllText(Full(ReceiptPath));
        var closeOriginal = File.ReadAllText(Full(ClosePath));

        void Restore()
        {
            File.WriteAllText(Full(RoutePath), routeOriginal);
            File.WriteAllText(Full(ReceiptPath), receiptOriginal);
            File.WriteAllText(Full(ClosePath), closeOriginal);
        }

        void RejectClose(Action<JsonObject> mutate, string expected)
        {
            Restore();
            var close = JsonNode.Parse(closeOriginal)!.AsObject();
            mutate(close);
            File.WriteAllText(Full(ClosePath), close.ToJsonString());
            var result = OperationsLearningPolicy.ValidateRepairAcceptance(_root, pmPath, "RPI001", ["RULE:reliability"], ClosePath, "close", true);
            Assert.Contains(result.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
        }

        void RejectReceipt(Action<JsonObject> mutate, string expected)
        {
            Restore();
            var receipt = JsonNode.Parse(receiptOriginal)!.AsObject();
            mutate(receipt);
            File.WriteAllText(Full(ReceiptPath), receipt.ToJsonString());
            var close = JsonNode.Parse(closeOriginal)!.AsObject();
            close["gateReceiptSha256"] = Sha(Full(ReceiptPath));
            File.WriteAllText(Full(ClosePath), close.ToJsonString());
            var result = OperationsLearningPolicy.ValidateRepairAcceptance(_root, pmPath, "RPI001", ["RULE:reliability"], ClosePath, "close", true);
            Assert.Contains(result.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
        }

        void RejectRoute(Action<JsonObject> mutate, string expected)
        {
            Restore();
            var route = JsonNode.Parse(routeOriginal)!.AsObject();
            mutate(route);
            File.WriteAllText(Full(RoutePath), route.ToJsonString());
            var routeHash = Sha(Full(RoutePath));
            var receipt = JsonNode.Parse(receiptOriginal)!.AsObject();
            receipt["routeSha256"] = routeHash;
            File.WriteAllText(Full(ReceiptPath), receipt.ToJsonString());
            var close = JsonNode.Parse(closeOriginal)!.AsObject();
            close["routeSha256"] = routeHash;
            close["gateReceiptSha256"] = Sha(Full(ReceiptPath));
            File.WriteAllText(Full(ClosePath), close.ToJsonString());
            var result = OperationsLearningPolicy.ValidateRepairAcceptance(_root, pmPath, "RPI001", ["RULE:reliability"], ClosePath, "close", true);
            Assert.Contains(result.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
        }

        RejectClose(close => close["schemaVersion"] = "2.0", "schemaVersion");
        RejectClose(close => close["artifactType"] = "wrong", "artifactType");
        RejectClose(close => close["postmortemSha256"] = new string('0', 64), "postmortemSha256");
        RejectClose(close => close["northstarSha256"] = new string('0', 64), "northstarSha256");
        RejectClose(close => close["learningsSha256"] = new string('0', 64), "learningsSha256");
        RejectClose(close => close["pyramidDigest"] = new string('0', 64), "pyramidDigest");
        RejectClose(close => close["ruleIds"] = new JsonArray("RULE:reliability", "RULE:reliability"), "json-array-duplicate:ruleIds");
        RejectClose(close => close["routeSha256"] = new string('0', 64), "route-reference-hash-mismatch");
        RejectClose(close => close["gateReceiptSha256"] = new string('0', 64), "gate-receipt-hash-mismatch");
        RejectClose(close => close["routePath"] = ClosePath, "repair-close-must-not-overwrite-route");

        RejectRoute(route => route["status"] = "OPEN", "status");
        RejectRoute(route => route["executableGateDigest"] = new string('0', 64), "executableGateDigest");
        RejectRoute(route => route["executableGate"] = new JsonArray("dotnet", "--version"), "acceptance-executable-gate-mismatch");
        RejectRoute(route => route["ruleIds"] = new JsonArray("RULE:wrong"), "acceptance-rule-ids-mismatch");

        RejectReceipt(receipt => receipt["verdict"] = "FAIL", "verdict");
        RejectReceipt(receipt => receipt["exitCode"] = 7, "gate-receipt-exit-not-zero");
        RejectReceipt(receipt => receipt["completed"] = false, "gate-receipt-not-completed");
        RejectReceipt(receipt => receipt["preGateStatusDigest"] = new string('0', 64), "gate-receipt-worktree-mutated");
        RejectReceipt(receipt => receipt["preGateHead"] = new string('0', 40), "gate-receipt-head-mutated");
        RejectReceipt(receipt => receipt["preGateIndexDigest"] = new string('0', 64), "gate-receipt-index-mutated");
        RejectReceipt(receipt => receipt["sourceHead"] = new string('0', 40), "gate-receipt-head-stale");
        RejectReceipt(receipt => receipt["sourceIndexDigest"] = new string('0', 64), "gate-receipt-index-stale");
        RejectReceipt(receipt => receipt["executableGate"] = new JsonArray("dotnet", "--version"), "gate-receipt-command-mismatch");
        RejectReceipt(receipt => receipt["ruleIds"] = new JsonArray("RULE:wrong"), "gate-receipt-rule-ids-mismatch");

        Restore();
    }

    [Fact]
    public void RetrievalValidation_rejectsMalformedAndStaleMatrix()
    {
        var cardPath = Full(".engloop/learnings/cards/reliability.md");
        File.WriteAllText(cardPath, File.ReadAllText(cardPath).Replace(
            "PM001/LEARN001\n\n## Tensions",
            "PM001/LEARN001\nPM005/LEARN001\n\n## Tensions",
            StringComparison.Ordinal));
        File.AppendAllText(Full(".engloop/learnings/README.md"), "| PM005 | 1 | reliability |\n");
        var pmPath = WritePostmortem(PyramidMode.Updated);
        WriteRetrievalEvidence("PM005/LEARN001");
        var casesPath = Full(".engloop/learnings/retrieval-cases.json");
        var observedPath = Full(".engloop/learnings/retrieval-observed.json");
        var receiptPath = Full(".engloop/out/retrieval.json");
        var baseCases = File.ReadAllText(casesPath);
        var baseObserved = File.ReadAllText(observedPath);
        var baseReceipt = File.ReadAllText(receiptPath);

        void Restore()
        {
            File.WriteAllText(casesPath, baseCases);
            File.WriteAllText(observedPath, baseObserved);
            File.WriteAllText(receiptPath, baseReceipt);
        }

        void RejectReceipt(Action<JsonObject> mutate, string expected)
        {
            Restore();
            var receipt = JsonNode.Parse(baseReceipt)!.AsObject();
            mutate(receipt);
            File.WriteAllText(receiptPath, receipt.ToJsonString());
            var result = OperationsLearningPolicy.ValidatePostmortem(_root, pmPath, ["IN005"]);
            Assert.Contains(result.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
        }

        void RejectCases(Action<JsonObject> mutate, string expected)
        {
            Restore();
            var cases = JsonNode.Parse(baseCases)!.AsObject();
            mutate(cases);
            File.WriteAllText(casesPath, cases.ToJsonString());
            var receipt = JsonNode.Parse(baseReceipt)!.AsObject();
            receipt["casesSha256"] = Sha(casesPath);
            File.WriteAllText(receiptPath, receipt.ToJsonString());
            var result = OperationsLearningPolicy.ValidatePostmortem(_root, pmPath, ["IN005"]);
            Assert.Contains(result.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
        }

        void RejectObserved(Action<JsonObject> mutate, string expected)
        {
            Restore();
            var observed = JsonNode.Parse(baseObserved)!.AsObject();
            mutate(observed);
            File.WriteAllText(observedPath, observed.ToJsonString());
            var receipt = JsonNode.Parse(baseReceipt)!.AsObject();
            receipt["observedResultsSha256"] = Sha(observedPath);
            File.WriteAllText(receiptPath, receipt.ToJsonString());
            var result = OperationsLearningPolicy.ValidatePostmortem(_root, pmPath, ["IN005"]);
            Assert.Contains(result.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
        }

        RejectReceipt(receipt => receipt["casesSha256"] = new string('0', 64), "retrieval-input-hash-stale:casesPath");
        RejectReceipt(receipt => receipt["observedResultsSha256"] = new string('0', 64), "retrieval-input-hash-stale:observedResultsPath");
        RejectReceipt(receipt => receipt["learningsSha256"] = new string('0', 64), "retrieval-learnings-hash-stale");
        RejectReceipt(receipt => receipt["cardsDigest"] = new string('0', 64), "retrieval-cards-digest-stale");
        RejectCases(cases => cases.Remove("cases"), "retrieval-cases-missing");
        RejectObserved(observed => observed.Remove("results"), "retrieval-observed-results-missing");
        RejectObserved(observed => ((JsonArray)observed["results"]!).Add(JsonNode.Parse(((JsonArray)observed["results"]!)[0]!.ToJsonString())), "observed-id-invalid-or-duplicate");
        RejectCases(cases => ((JsonArray)cases["cases"]!).Add(JsonNode.Parse(((JsonArray)cases["cases"]!)[0]!.ToJsonString())), "case-id-invalid-or-duplicate");
        RejectObserved(observed => ((JsonArray)observed["results"]!).Clear(), "missing-observed-case");
        RejectObserved(observed => ((JsonObject)((JsonArray)observed["results"]!)[0]!)["verdict"] = "GAP", "verdict-mismatch");
        RejectObserved(observed => ((JsonObject)((JsonArray)observed["results"]!)[0]!)["actualCardIds"] = new JsonArray(), "card-set-mismatch");
        RejectObserved(observed => ((JsonObject)((JsonArray)observed["results"]!)[0]!)["actualSourceIds"] = new JsonArray("PM001/LEARN001"), "source-set-mismatch");
        RejectObserved(observed => ((JsonArray)observed["results"]!).Add(new JsonObject { ["id"] = "extra", ["actualCardIds"] = new JsonArray(), ["actualSourceIds"] = new JsonArray(), ["verdict"] = "GAP" }), "unexpected-observed-case");

        RejectCases(cases =>
        {
            var item = (JsonObject)((JsonArray)cases["cases"]!)[0]!;
            item["expectGap"] = true;
            item["expectedCardIds"] = new JsonArray();
            item["expectedSourceIds"] = new JsonArray();
        }, "verdict-mismatch");

        Restore();
    }

    private string WritePostmortem(PyramidMode mode, bool sekRelevant = false)
    {
        var northstarHash = Sha(Full("NORTHSTAR.md"));
        var learningsHash = Sha(Full("LEARNINGS.md"));
        var pyramidDigest = OperationsLearningPolicy.ComputePyramidDigest(_root, "PM005");
        var incidentHash = Sha(Full(IncidentPath));
        var decision = mode switch { PyramidMode.Updated => "UPDATED", PyramidMode.NoChange => "NO-CHANGE", _ => "<UPDATED | NO-CHANGE>" };
        var historicalDecision = mode == PyramidMode.Updated ? "UPDATED" : "NO-CHANGE";
        var historicalPath = mode == PyramidMode.Updated ? ".engloop/learnings/README.md" : "NOT-REQUIRED";
        var changedPaths = mode == PyramidMode.Updated ? ".engloop/learnings/cards/reliability.md, .engloop/learnings/README.md" : "NOT-REQUIRED";
        var retrievalImpact = mode == PyramidMode.Updated ? "CHANGED" : "UNCHANGED";
        var retrievalEvidence = mode == PyramidMode.Updated ? ".engloop/out/retrieval.json" : "NOT-REQUIRED";
        var gate = sekRelevant
            ? "[\"dotnet\", \"tool\", \"run\", \"sek\", \"version\"]"
            : "[\"pwsh\", \"-NoProfile\", \"-File\", \"gate.ps1\"]";
        var text = $$"""
        # PM005: Synthetic contradiction

        - **Status:** COMPLETE

        ## Selected stabilized incidents

        | Incident ID | Path | SHA-256 |
        |---|---|---|
        | `IN005` | `{{IncidentPath}}` | `{{incidentHash}}` |

        ## Root causes

        The implementation silently used a fallback.

        ## SEK Test-Escape Analysis

        - **SEK applicability:** `{{(sekRelevant ? "RELEVANT" : "NOT-RELEVANT")}}`
        - **SEK applicability rationale:** {{(sekRelevant ? "The silent fallback is stateful vertical behavior that the workflow model and generated scenarios are intended to reject." : "The synthetic fixture treats this repair as infrastructure enforcement outside the stateful model boundary for this test.")}}
        - **SEK version:** `{{(sekRelevant ? "0.1.3" : "NOT-REQUIRED")}}`
        - **SEK verification class:** `{{(sekRelevant ? "STATEFUL-VERTICAL" : "INFRASTRUCTURE")}}`
        - **SEK escape class:** `{{(sekRelevant ? "CORD-DOMAIN-GAP" : "NOT-RELEVANT")}}`
        - **SEK scenario ID:** `{{(sekRelevant ? "SEK-SCENARIO:silent-fallback" : "NOT-REQUIRED")}}`
        - **SEK model paths:** `{{(sekRelevant ? "model/Model.cs" : "NOT-REQUIRED")}}`
        - **SEK CORD paths:** `{{(sekRelevant ? "model/Config.cord" : "NOT-REQUIRED")}}`
        - **SEK generated suite path:** `{{(sekRelevant ? "tests/generated" : "NOT-REQUIRED")}}`
        - **Why SEK tests missed the incident:** {{(sekRelevant ? "The Cord parameter domain omitted the fallback decision, so exploration never generated the incident behavior or its rejection oracle." : "The affected infrastructure operation is not represented by the stateful vertical and expanding the model would create tautological tests.")}}
        - **Required model/CORD repair:** {{(sekRelevant ? "Add the fallback decision to the finite Cord domain and regenerate a rejection scenario with the real binding." : "NOT-REQUIRED")}}

        ## Direction and Learning-Pyramid Consultation

        - **North Star path:** `NORTHSTAR.md`
        - **North Star SHA-256:** `{{northstarHash}}`
        - **Direction alignment:** `ALIGNED`
        - **Direction decision:** Preserve the fail closed reliability boundary for every repair.
        - **Learnings index path:** `LEARNINGS.md`
        - **Learnings index SHA-256:** `{{learningsHash}}`
        - **Pyramid digest:** `{{pyramidDigest}}`
        - **Pyramid decision:** `{{decision}}`
        - **Pyramid rationale:** The incident contradicts the reliability rule; {{(mode == PyramidMode.NoChange ? "no semantic change is appropriate because the existing rule already forbids this failure and only executable enforcement is repaired." : "the living rule provenance and retrieval query are updated for this newly observed contradiction.")}}
        - **Historical coverage decision:** `{{historicalDecision}}`
        - **Historical coverage path:** `{{historicalPath}}`
        - **Changed pyramid paths:** `{{changedPaths}}`
        - **Retrieval impact:** `{{retrievalImpact}}`
        - **Retrieval evidence:** `{{retrievalEvidence}}`
        - **Retrieval rationale:** {{(mode == PyramidMode.Updated ? "The contradicted rule query changed and independent retrieval results must match." : "The existing query already retrieves the governing reliability rule and only its executable enforcement changes.")}}

        ### Rule dispositions

        | Rule ID | Card ID | Source IDs | Disposition | Incident evidence | Pyramid action |
        |---|---|---|---|---|---|
        | `RULE:reliability` | `reliability` | {{(mode == PyramidMode.Updated ? "`PM001/LEARN001`, `PM005/LEARN001`" : "`PM001/LEARN001`")}} | `CONTRADICTED` | The implementation silently used a fallback in the selected incident. | {{(mode == PyramidMode.Updated ? "Update living rule provenance and the independent retrieval query." : "NO-CHANGE: retain the existing stronger rule and repair its executable gate.")}} |

        ## Learnings

        {{(mode == PyramidMode.Updated ? "- **LEARN001 (`PM005/LEARN001`)** — A declared fail closed rule needs an executable rejection gate." : "- **No accepted source learning:** The existing PM001 reliability rule already captures the class; this PM adds only repair enforcement evidence.")}}

        ## Repair Items

        | RPI | Description | Status |
        |---|---|---|
        | RPI001 | Add the rejection gate. | OPEN |

        ### RPI001 learning contract

        - **Rule IDs:** `RULE:reliability`
        - **Executable gate:** `{{gate}}`
        - **Gate proves:** The forbidden fallback is rejected and state remains consistent for users.
        - **SEK applicability:** `{{(sekRelevant ? "RELEVANT" : "NOT-RELEVANT")}}`
        - **SEK scenario ID:** `{{(sekRelevant ? "SEK-SCENARIO:silent-fallback" : "NOT-REQUIRED")}}`
        - **SEK repair requirement:** {{(sekRelevant ? "Correct the Cord domain, regenerate the suite, and prove the silent fallback scenario is rejected by the real binding." : "NOT-REQUIRED")}}
        - **SEK verification gate:** `{{(sekRelevant ? gate : "NOT-REQUIRED")}}`
        """.Replace("\n+", "\n", StringComparison.Ordinal);
        File.WriteAllText(Full(PmPath), text);
        return PmPath;
    }

    private void WriteIncident(bool consulted)
    {
        var northstarHash = Sha(Full("NORTHSTAR.md"));
        File.WriteAllText(Full(IncidentPath), $$"""
        # Incident IN005: Synthetic

        - **Status:** STABILIZED

        ## Verification (stability, not root-cause fix)

        - [x] Health checks passing: synthetic service health checks remained continuously green.
        - [x] User workflows unblocked: synthetic user workflow completed successfully without errors.
        - [x] No fresh errors in the watch window: synthetic watch window reported zero errors.

        ## Direction and learning context

        - **North Star SHA-256:** `{{northstarHash}}`
        - **Learning context:** `{{(consulted ? "CONSULTED" : "DEFERRED")}}`
        - **Rule IDs:** `{{(consulted ? "RULE:reliability" : "NONE")}}`
        - **Source IDs:** `{{(consulted ? "PM001/LEARN001" : "NONE")}}`
        - **Deferral reason:** {{(consulted ? "NOT-REQUIRED" : "Immediate stabilization took priority and the learning lookup remains pending.")}}
        """);
    }

    private void WriteRoute(PostmortemLearningContract pm)
    {
        var repair = pm.Repairs["RPI001"];
        var payload = new
        {
            schemaVersion = "1.0", artifactType = "repair-learning-route", phase = "route",
            postmortemPath = pm.RelativePath, postmortemSha256 = pm.Sha256, rpiId = "RPI001",
            ruleIds = repair.RuleIds, northstarSha256 = pm.NorthstarSha256, learningsSha256 = pm.LearningsSha256,
            pyramidDigest = pm.PyramidDigest, executableGate = repair.ExecutableGate,
            executableGateDigest = repair.ExecutableGateDigest, gateProves = repair.GateProves,
            sekApplicability = repair.SekApplicability, sekVersion = pm.SekEscape.Version,
            sekEscapeClass = pm.SekEscape.EscapeClass, sekScenarioId = repair.SekScenarioId,
            sekModelPaths = pm.SekEscape.ModelPaths, sekCordPaths = pm.SekEscape.CordPaths,
            sekGeneratedSuitePath = pm.SekEscape.GeneratedSuitePath,
            sekRepairRequirement = repair.SekRepairRequirement, status = "ROUTED"
        };
        File.WriteAllText(Full(RoutePath), JsonSerializer.Serialize(payload, JsonOptions));
    }

    private void WriteClose(PostmortemLearningContract pm)
    {
        var repair = pm.Repairs["RPI001"];
        var payload = new
        {
            schemaVersion = "1.0", artifactType = "repair-learning-close", phase = "close",
            postmortemPath = pm.RelativePath, postmortemSha256 = pm.Sha256, rpiId = "RPI001",
            ruleIds = repair.RuleIds, northstarSha256 = pm.NorthstarSha256, learningsSha256 = pm.LearningsSha256,
            pyramidDigest = pm.PyramidDigest, gateProves = repair.GateProves,
            sekApplicability = repair.SekApplicability, sekVersion = pm.SekEscape.Version,
            sekEscapeClass = pm.SekEscape.EscapeClass, sekScenarioId = repair.SekScenarioId,
            sekModelPaths = pm.SekEscape.ModelPaths, sekCordPaths = pm.SekEscape.CordPaths,
            sekGeneratedSuitePath = pm.SekEscape.GeneratedSuitePath,
            sekRepairRequirement = repair.SekRepairRequirement,
            routePath = RoutePath, routeSha256 = Sha(Full(RoutePath)),
            gateReceiptPath = ReceiptPath, gateReceiptSha256 = Sha(Full(ReceiptPath)), status = "CLOSED"
        };
        File.WriteAllText(Full(ClosePath), JsonSerializer.Serialize(payload, JsonOptions));
    }

    private void WriteReadiness()
    {
        var evidence = ".engloop/coverage/COV999_fixture.json";
        var report = ".engloop/out/readiness-coverage/fixture/coverage.cobertura.xml";
        Directory.CreateDirectory(Path.GetDirectoryName(Full(report))!);
        File.WriteAllText(Full(report), "<coverage><packages><package name=\"core\" line-rate=\"1\" branch-rate=\"1\" /></packages></coverage>");
        var payload = new
        {
            schemaVersion = "1.0",
            artifactType = "whole-product-readiness",
            verdict = "PASS",
            generatedFunctionalPass = true,
            directSuitePass = true,
            architectureValidationPass = true,
            coberturaReport = report,
            coberturaSha256 = Sha(Full(report)),
            modules = new[] { new { id = "core", coverageIdentity = "core", line = 100, branch = 100, functionalPass = true, directPass = true, architecturePass = true, pass = true } },
            failures = Array.Empty<string>(),
        };
        File.WriteAllText(Full(evidence), JsonSerializer.Serialize(payload));
        Assert.Equal(0, ValidationCommands.ExecuteReadiness(["emit", "--root", _root, "--evidence", evidence, "--verdict", "pass"]));
    }

    private void WriteGateScript(bool pass)
    {
        File.WriteAllText(Full("gate.ps1"), pass ? "Write-Output 'gate pass'; exit 0\n" : "Write-Error 'gate fail'; exit 7\n");
    }

    private void WriteRetrievalEvidence(string actualSource)
    {
        var casesPath = Full(".engloop/learnings/retrieval-cases.json");
        var observedPath = Full(".engloop/learnings/retrieval-observed.json");
        File.WriteAllText(casesPath, "{\"cases\":[{\"id\":\"reliability\",\"expectedCardIds\":[\"reliability\"],\"expectedSourceIds\":[\"PM001/LEARN001\",\"PM005/LEARN001\"],\"expectGap\":false}]}\n");
        File.WriteAllText(observedPath, $$"""{"results":[{"id":"reliability","actualCardIds":["reliability"],"actualSourceIds":["PM001/LEARN001","{{actualSource}}"],"verdict":"PASS"}]}""");
        var payload = new
        {
            casesPath = ".engloop/learnings/retrieval-cases.json", casesSha256 = Sha(casesPath),
            observedResultsPath = ".engloop/learnings/retrieval-observed.json", observedResultsSha256 = Sha(observedPath),
            learningsSha256 = Sha(Full("LEARNINGS.md")),
            cardsDigest = OperationsLearningPolicy.ComputeCardsDigest(Full(".engloop/learnings/cards"))
        };
        File.WriteAllText(Full(".engloop/out/retrieval.json"), JsonSerializer.Serialize(payload));
    }

    private string Full(string relative) => Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private void Git(params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = _root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output + error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private enum PyramidMode { MissingDecision, NoChange, Updated }
}
