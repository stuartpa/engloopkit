using System.Diagnostics;
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
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.40-pomodoro-create", "--root", _fixture]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.50-overlay-pack", "--root", _fixture]));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.51-overlay-remove", "--root", _fixture]));
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
    public void DebuggerAndReviewEntry_requireReadinessAndCurrentEngineerAttestation()
    {
        CreateCanonicalFixture(modulePath: "module.csproj");
        File.WriteAllText(Path.Combine(_fixture, "module.csproj"), "<Project />");
        RunGit("init");
        RunGit("config", "user.email", "walkthrough@example.invalid");
        RunGit("config", "user.name", "Walkthrough Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "fixture");

        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.09-debugger-walk-thru", "--root", _fixture]));
        Directory.CreateDirectory(Path.Combine(_fixture, ".engloop", "out"));
        var readinessPath = Path.Combine(_fixture, ".engloop", "out", "cov003-readiness.json");
        File.WriteAllText(readinessPath, "{");
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.09-debugger-walk-thru", "--root", _fixture]));
        File.WriteAllText(readinessPath, "{}");
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.09-debugger-walk-thru", "--root", _fixture]));
        File.WriteAllText(readinessPath, "{\"verdict\":\"NOT READY\"}");
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.09-debugger-walk-thru", "--root", _fixture]));
        File.WriteAllText(readinessPath, "{\"verdict\":\"PASS\"}");
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.09-debugger-walk-thru", "--root", _fixture]));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));

        var head = RunGitOutput("rev-parse", "HEAD").Trim();
        var directory = Path.Combine(_fixture, ".engloop", "debugger-walkthroughs");
        Directory.CreateDirectory(directory);
        var ledgerPath = Path.Combine(directory, "DBG001_fixture.md");
        string Ledger(string ledgerHead, string status, string checklist, string chunkStatus, string attestation) => $$"""
        # DBG001 — fixture
        - **Head revision:** {{ledgerHead}}
        - **Status:** {{status}}
        | DBG-CHUNK-001 | path | {{chunkStatus}} |
        - **Engineer attestation:** {{attestation}}
        {{checklist}}
        """;

        File.WriteAllText(ledgerPath, Ledger("0000000000000000000000000000000000000000", "COMPLETE", "- [x] Every chunk is attested", "attested", "I personally stepped through it."));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        File.WriteAllText(ledgerPath, Ledger(head, "IN PROGRESS", "- [x] Every chunk is attested", "attested", "I personally stepped through it."));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        File.WriteAllText(ledgerPath, Ledger(head, "COMPLETE", "- [ ] Every chunk is attested", "attested", "I personally stepped through it."));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        foreach (var incomplete in new[] { "pending", "blocked", "stale" })
        {
            File.WriteAllText(ledgerPath, Ledger(head, "COMPLETE", "- [x] Every chunk is attested", incomplete, "I personally stepped through it."));
            Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        }
        File.WriteAllText(ledgerPath, Ledger(head, "COMPLETE", "- [x] Every chunk is attested", "attested", "<exact response>"));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
        File.WriteAllText(ledgerPath, Ledger(head, "COMPLETE", "- [x] Every chunk is attested", "attested", ""));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));

        File.WriteAllText(ledgerPath, Ledger(head, "COMPLETE", "- [x] Every chunk is attested", "attested", "I personally stepped through this chunk line by line in the recorded debugger."));
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));

        File.AppendAllText(ledgerPath, "\n| DBG-CHUNK-002 | path | pending |\n");
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));

        Directory.Move(Path.Combine(_fixture, ".git"), Path.Combine(_fixture, ".git-hidden"));
        Assert.Equal(2, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.10-codereview-prepare", "--root", _fixture]));
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
