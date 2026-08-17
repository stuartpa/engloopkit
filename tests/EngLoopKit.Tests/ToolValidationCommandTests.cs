using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>
/// Drives the public validation CLI and its fail-closed fixture paths in-process. These
/// tests deliberately exercise the same validators a released root-local tool invokes.
/// </summary>
public sealed class ToolValidationCommandTests : IDisposable
{
    private static readonly string Root = FindRepoRoot();
    private readonly string _fixture = Path.Combine(Path.GetTempPath(), "engloopkit-tool-" + Guid.NewGuid().ToString("N"));

    public ToolValidationCommandTests() => Directory.CreateDirectory(_fixture);

    [Fact]
    public void Program_dispatchesKnownAndRejectsMalformedCommands()
    {
        Assert.Equal(1, Program.Main([]));
        Assert.Equal(1, Program.Main(["unknown", "root"]));
        Assert.Equal(1, Program.Main(["validate", "unknown", "--root", Root]));
        Assert.Equal(0, Program.Main(["validate", "root", "--root", Root]));
    }

    [Fact]
    public void CurrentRoot_passesEveryDeterministicValidator()
    {
        Assert.Equal(0, ValidationCommands.ValidateRoot(["--root", Root]));
        Assert.Equal(0, ValidationCommands.ValidateConfig(["--root", Root]));
        Assert.Equal(0, ValidationCommands.ValidateCommands(["--root", Root]));
        Assert.Equal(0, ValidationCommands.ValidateAgentSurfaces(["--root", Root]));
        Assert.Equal(0, ValidationCommands.ValidateInstallation(["--root", Root]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.01-northstar", "--root", Root]));
    }

    [Fact]
    public void AgentEntry_failsClosedForMissingStageAndInvalidRoot()
    {
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--root", Root]));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.99-missing", "--root", Root]));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.01-northstar", "--root", _fixture]));
    }

    [Fact]
    public void AgentEntry_rejectsMalformedConfigAndEveryUnprovenRunwayStage()
    {
        CreateCanonicalFixture(modulePath: "module.csproj");
        File.WriteAllText(Path.Combine(_fixture, "module.csproj"), "<Project />");

        var configPath = Path.Combine(_fixture, ".engloop", "config.json");
        var valid = File.ReadAllText(configPath);
        File.WriteAllText(configPath, valid.Replace("\"productId\": \"engloopkit\"", "\"productId\": \"Invalid Product!\"", StringComparison.Ordinal));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.01-northstar", "--root", _fixture]));

        File.WriteAllText(configPath, valid.Replace("\"status\": \"proven\"", "\"status\": \"unproven\"", StringComparison.Ordinal));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.09-debugger-walk-thru", "--root", _fixture]));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.30-token-efficiency-analyze", "--root", _fixture]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.31-token-efficiency-implement", "--root", _fixture]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.50-handoff-create", "--root", _fixture]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.80-upgrade-elk", "--root", _fixture]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.60-overlay-pack", "--root", _fixture]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.61-overlay-remove", "--root", _fixture]));
        foreach (var stage in new[]
        {
            "speckit.engloop.05-model",
            "speckit.engloop.06-explore",
            "speckit.engloop.07-validate",
            "speckit.engloop.08-unittest",
        })
        {
            Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", stage, "--root", _fixture]));
        }

        File.WriteAllText(configPath, "{");
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.01-northstar", "--root", _fixture]));
    }

    [Fact]
    public void DebuggerEntry_requiresRunway_whileReviewRequiresOnlyCurrentReadiness()
    {
        CreateCanonicalFixture(modulePath: "module.csproj");
        File.WriteAllText(Path.Combine(_fixture, "module.csproj"), "<Project />");
        RunGit("init");
        RunGit("config", "user.email", "walkthrough@example.invalid");
        RunGit("config", "user.name", "Walkthrough Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "fixture");

        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.09-debugger-walk-thru", "--root", _fixture]));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));

        Assert.Equal(1, ValidationCommands.ExecuteReadiness([]));
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "fail", "--evidence", ".engloop/coverage/COV001_readiness.json"]));
        Directory.CreateDirectory(Path.Combine(_fixture, ".engloop", "coverage"));
        var evidenceRelative = ".engloop/coverage/COV001_readiness.json";
        var evidencePath = Path.Combine(_fixture, ".engloop", "coverage", "COV001_readiness.json");
        File.WriteAllText(evidencePath, "{}");
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", evidenceRelative]));
        WriteReadinessEvidence(evidencePath);
        Assert.Equal(0, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", evidenceRelative]));
        Assert.True(File.Exists(Path.Combine(_fixture, ".engloop", "readiness", "current.json")));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.20-incident", "--root", _fixture]));

        File.AppendAllText(evidencePath, "changed\n");
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        WriteReadinessEvidence(evidencePath);
        Assert.Equal(0, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", evidenceRelative]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));

        var directory = Path.Combine(_fixture, ".engloop", "debugger-walkthroughs");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "DBG001_incomplete.md"), "# incomplete\n- **Status:** BLOCKED\n- [ ] pending\n");
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));

        File.WriteAllText(Path.Combine(_fixture, "new-product-change.txt"), "new head");
        RunGit("add", "new-product-change.txt");
        RunGit("commit", "-m", "new head");
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.09-debugger-walk-thru", "--root", _fixture]));
    }

    [Fact]
    public void ReadinessEmissionAndRecordValidation_failClosedForEveryIdentityAndPathMismatch()
    {
        CreateCanonicalFixture(modulePath: "module.csproj");
        File.WriteAllText(Path.Combine(_fixture, "module.csproj"), "<Project />");
        RunGit("init");
        RunGit("config", "user.email", "readiness@example.invalid");
        RunGit("config", "user.name", "Readiness Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "fixture");

        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["unknown"]));
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", Path.Combine(_fixture, "missing-root"), "--verdict", "pass", "--evidence", ".engloop/coverage/COV001_readiness.json"]));
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture]));
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass"]));
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", Path.Combine(_fixture, "absolute.md")]));
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", "../escape.md"]));
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", "README.md"]));
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", ".engloop/coverage/missing.json"]));

        Directory.CreateDirectory(Path.Combine(_fixture, ".engloop", "coverage"));
        var evidenceRelative = ".engloop/coverage/COV001_readiness.json";
        var evidencePath = Path.Combine(_fixture, ".engloop", "coverage", "COV001_readiness.json");
        File.WriteAllText(evidencePath, "{}");
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", evidenceRelative]));
        WriteReadinessEvidence(evidencePath, line: 94.99);
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", evidenceRelative]));
        WriteReadinessEvidence(evidencePath, includeModule: false);
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", evidenceRelative]));
        WriteReadinessEvidence(evidencePath, modulePass: false);
        Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", evidenceRelative]));
        WriteReadinessEvidence(evidencePath);
        Assert.Equal(0, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", evidenceRelative]));

        var recordPath = Path.Combine(_fixture, ".engloop", "readiness", "current.json");
        var original = JsonNode.Parse(File.ReadAllText(recordPath))!.AsObject();
        void RejectMutation(Action<JsonObject> mutate)
        {
            var changed = JsonNode.Parse(original.ToJsonString())!.AsObject();
            mutate(changed);
            File.WriteAllText(recordPath, changed.ToJsonString());
            Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        }

        File.WriteAllText(recordPath, "{");
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        RejectMutation(record => record.Remove("schemaVersion"));
        RejectMutation(record => record["schemaVersion"] = "2.0");
        RejectMutation(record => record["stage"] = "07-validate");
        RejectMutation(record => record["verdict"] = "FAIL");
        RejectMutation(record => record["head"] = new string('0', 40));
        RejectMutation(record => record.Remove("evidencePath"));
        RejectMutation(record => record.Remove("evidenceSha256"));
        RejectMutation(record => record["evidencePath"] = "");
        RejectMutation(record => record["evidencePath"] = Path.Combine(_fixture, "absolute.md"));
        RejectMutation(record => record["evidenceSha256"] = null);
        RejectMutation(record => record["evidencePath"] = "README.md");
        RejectMutation(record => record["evidenceSha256"] = new string('0', 64));
        File.Delete(evidencePath);
        File.WriteAllText(recordPath, original.ToJsonString());
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));

        WriteReadinessEvidence(evidencePath);
        var gitPath = Path.Combine(_fixture, ".git");
        var hiddenGitPath = Path.Combine(_fixture, ".git-hidden");
        Directory.Move(gitPath, hiddenGitPath);
        try
        {
            Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", evidenceRelative]));
        }
        finally
        {
            Directory.Move(hiddenGitPath, gitPath);
        }
    }

    [Fact]
    public void StructuredReadiness_rejectsMalformedEvidenceMatrix()
    {
        CreateCanonicalFixture(modulePath: "module.csproj");
        File.WriteAllText(Path.Combine(_fixture, "module.csproj"), "<Project />");
        RunGit("init");
        RunGit("config", "user.email", "readiness-matrix@example.invalid");
        RunGit("config", "user.name", "Readiness Matrix");
        RunGit("add", ".");
        RunGit("commit", "-m", "fixture");
        Directory.CreateDirectory(Path.Combine(_fixture, ".engloop", "coverage"));
        var relative = ".engloop/coverage/COV001_readiness.json";
        var path = Path.Combine(_fixture, relative.Replace('/', Path.DirectorySeparatorChar));

        void Reject(Action<JsonObject> mutate)
        {
            WriteReadinessEvidence(path);
            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            mutate(json);
            File.WriteAllText(path, json.ToJsonString());
            Assert.Equal(1, ValidationCommands.ExecuteReadiness(["emit", "--root", _fixture, "--verdict", "pass", "--evidence", relative]));
        }

        Reject(json => json.Remove("schemaVersion"));
        Reject(json => json["artifactType"] = "wrong");
        Reject(json => json["verdict"] = "FAIL");
        Reject(json => json["generatedFunctionalPass"] = false);
        Reject(json => json["directSuitePass"] = false);
        Reject(json => json["architectureValidationPass"] = false);
        Reject(json => json["failures"] = new JsonArray("failure"));
        Reject(json => json["failures"] = "not-an-array");
        Reject(json => json.Remove("modules"));
        Reject(json => json["coberturaSha256"] = new string('0', 64));
        Reject(json => json["coberturaReport"] = 1);
        Reject(json => json["coberturaReport"] = null);
        Reject(json => json["coberturaSha256"] = null);
        Reject(json => json["coberturaReport"] = ".engloop/out/readiness-coverage/missing.xml");
        Reject(json => ((JsonArray)json["modules"]!).Clear());
        Reject(json => ((JsonArray)json["modules"]!).Add(JsonNode.Parse(((JsonArray)json["modules"]!)[0]!.ToJsonString())));
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!)["id"] = "other");
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!).Remove("id"));
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!)["id"] = null);
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!)["coverageIdentity"] = "missing-package");
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!).Remove("coverageIdentity"));
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!)["coverageIdentity"] = null);
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!).Remove("line"));
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!).Remove("branch"));
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!)["line"] = 99.0);
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!)["branch"] = 99.0);
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!)["functionalPass"] = false);
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!)["directPass"] = false);
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!)["architecturePass"] = false);
        Reject(json => ((JsonObject)((JsonArray)json["modules"]!)[0]!)["pass"] = false);
    }

    [Fact]
    public void RootAndInstallation_shortCircuitOnInvalidRootOrConfig()
    {
        Assert.Equal(1, ValidationCommands.ValidateRoot(["--root", _fixture]));
        Assert.Equal(1, ValidationCommands.ValidateConfig(["--root", _fixture]));
        Assert.Equal(1, ValidationCommands.ValidateInstallation(["--root", _fixture]));

        CreateCanonicalFixture(modulePath: "module.csproj");
        File.WriteAllText(Path.Combine(_fixture, "module.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_fixture, ".engloop", "config.json"), "not-json");
        Assert.Equal(1, ValidationCommands.ValidateConfig(["--root", _fixture]));
        Assert.Equal(1, ValidationCommands.ValidateInstallation(["--root", _fixture]));
    }

    [Fact]
    public void ConfigValidation_rejectsMissingAndEscapingModules()
    {
        CreateCanonicalFixture(modulePath: "missing.csproj");
        Assert.Equal(1, ValidationCommands.ValidateConfig(["--root", _fixture]));

        File.WriteAllText(Path.Combine(_fixture, "module.csproj"), "<Project />");
        WriteConfig("../escape.csproj");
        Assert.Equal(1, ValidationCommands.ValidateConfig(["--root", _fixture]));

        WriteConfig("module.csproj");
        Assert.Equal(0, ValidationCommands.ValidateConfig(["--root", _fixture]));
        Assert.Equal(1, ValidationCommands.ValidateInstallation(["--root", _fixture]));
    }

    [Fact]
    public void CommandAndAgentSurfaceValidators_rejectMissingDirectories()
    {
        CreateCanonicalFixture(modulePath: "module.csproj");
        File.WriteAllText(Path.Combine(_fixture, "module.csproj"), "<Project />");
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _fixture]));
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _fixture]));
    }

    [Fact]
    public void OperationsValidators_emitPolicyFailuresAndRejectUnsafeConfig()
    {
        CreateCanonicalFixture(modulePath: "module.csproj");
        File.WriteAllText(Path.Combine(_fixture, "module.csproj"), "<Project />");
        Directory.CreateDirectory(Path.Combine(_fixture, ".engloop", "postmortems"));
        Directory.CreateDirectory(Path.Combine(_fixture, ".engloop", "repairs"));
        File.WriteAllText(Path.Combine(_fixture, ".engloop", "postmortems", "PM001_invalid.md"), "# invalid\n");
        File.WriteAllText(Path.Combine(_fixture, ".engloop", "repairs", "PM001-RPI001.route.json"), "{}\n");
        Assert.Equal(1, ValidationCommands.ValidatePostmortemLearning(["--root", _fixture, "--incidents", "IN001", "--postmortem", ".engloop/postmortems/PM001_invalid.md"]));
        Assert.Equal(1, ValidationCommands.ValidateRepairLearning(["--root", _fixture, "--phase", "route", "--postmortem", ".engloop/postmortems/PM001_invalid.md", "--rpi", "RPI001", "--rules", "RULE:x", "--acceptance", ".engloop/repairs/PM001-RPI001.route.json"]));

        var config = Path.Combine(_fixture, ".engloop", "config.json");
        File.WriteAllText(config, File.ReadAllText(config).Replace("\"productId\": \"engloopkit\"", "\"productId\": \"bad product\"", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateIncidentContext(["--root", _fixture, "--incident", ".engloop/incidents/missing.md"]));
    }

    [Fact]
    public void LearningsAndIncidentValidators_emitEveryReturnedFailure()
    {
        CreateCanonicalFixture(modulePath: "module.csproj");
        File.WriteAllText(Path.Combine(_fixture, "module.csproj"), "<Project />");
        Directory.CreateDirectory(Path.Combine(_fixture, ".engloop", "postmortems"));
        Directory.CreateDirectory(Path.Combine(_fixture, ".engloop", "learnings", "cards"));
        File.WriteAllText(Path.Combine(_fixture, ".engloop", "postmortems", "PM001_bad.md"), "## Learnings\n- **LEARN001** — uncovered\n");
        Assert.Equal(1, ValidationCommands.ValidateLearnings(["--root", _fixture]));

        Directory.CreateDirectory(Path.Combine(_fixture, ".engloop", "incidents"));
        File.WriteAllText(Path.Combine(_fixture, ".engloop", "incidents", "IN001_bad.md"), "# invalid incident\n");
        Assert.Equal(1, ValidationCommands.ValidateIncidentContext(["--root", _fixture, "--incident", ".engloop/incidents/IN001_bad.md"]));
    }

    private void CreateCanonicalFixture(string modulePath)
    {
        Directory.CreateDirectory(Path.Combine(_fixture, ".engloop"));
        File.WriteAllText(Path.Combine(_fixture, "NORTHSTAR.md"), "# Direction");
        File.WriteAllText(Path.Combine(_fixture, "LEARNINGS.md"), "# Learnings");
        WriteConfig(modulePath);
    }

    private void WriteConfig(string modulePath)
    {
        File.WriteAllText(Path.Combine(_fixture, ".engloop", "config.json"), $$"""
        {
          "schemaVersion": "2.0",
          "productId": "engloopkit",
          "artifactRoot": ".engloop",
          "transientOutputRoot": ".engloop/out",
          "northstarPath": "NORTHSTAR.md",
                    "validatorCommand": ["dotnet", "tool", "run", "engloopkit", "--"],
                    "moduleDiscoveryCommand": ["pwsh", "-File", "scripts/discover-modules.ps1"],
                    "architectureCommand": ["dotnet", "tool", "run", "engloopkit", "--", "validate", "root", "--root", "."],
                    "regressionCommand": ["dotnet", "test", "tests/tests.csproj"],
                    "coverageInputs": { "wholeProduct": "tests/tests.csproj" },
                    "testRunway": {
                        "status": "proven",
                        "framework": "xunit",
                        "terseCommand": ["dotnet", "test", "tests/tests.csproj"],
                        "boundaryTest": "Fixture.Boundary",
                        "generatedDestination": "tests/generated",
                        "evidenceDigest": "fixture-digest",
                        "provenAtRevision": "content:fixture-digest"
                    },
          "moduleInventory": [{ "id": "fixture", "path": "{{modulePath}}" }]
        }
        """);
    }

    private static void WriteReadinessEvidence(string path, double line = 100, double branch = 100, bool includeModule = true, bool modulePass = true)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
        var report = Path.Combine(root, ".engloop", "out", "readiness-coverage", "fixture", "coverage.cobertura.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, $"<coverage><packages><package name=\"fixture\" line-rate=\"{(line / 100).ToString(System.Globalization.CultureInfo.InvariantCulture)}\" branch-rate=\"{(branch / 100).ToString(System.Globalization.CultureInfo.InvariantCulture)}\" /></packages></coverage>");
        object[] modules = includeModule
            ?
            [
                new
                {
                    id = "fixture",
                    coverageIdentity = "fixture",
                    line,
                    branch,
                    functionalPass = modulePass,
                    directPass = modulePass,
                    architecturePass = modulePass,
                    pass = modulePass,
                }
            ]
            : [];
        var evidence = new
        {
            schemaVersion = "1.0",
            artifactType = "whole-product-readiness",
            verdict = "PASS",
            generatedFunctionalPass = true,
            directSuitePass = true,
            architectureValidationPass = true,
            coberturaReport = Path.GetRelativePath(root, report).Replace('\\', '/'),
            coberturaSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(report))).ToLowerInvariant(),
            modules,
            failures = Array.Empty<string>(),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(evidence));
    }

    private void RunGit(params string[] args)
    {
        var result = RunGitProcess(args);
        if (result.ExitCode != 0) throw new Xunit.Sdk.XunitException(result.Error);
    }

    private string RunGitOutput(params string[] args)
    {
        var result = RunGitProcess(args);
        if (result.ExitCode != 0) throw new Xunit.Sdk.XunitException(result.Error);
        return result.Output;
    }

    private (int ExitCode, string Output, string Error) RunGitProcess(string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = _fixture,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "bundle.yml")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "could not locate repository root");
        return dir!.FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_fixture))
        {
            try { Directory.Delete(_fixture, recursive: true); }
            catch (IOException) { /* transient disposable Git file lock */ }
            catch (UnauthorizedAccessException) { /* transient disposable Git file lock */ }
        }
    }
}
