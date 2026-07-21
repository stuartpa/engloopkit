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
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--stage", "speckit.engloop.09-codereview-prepare", "--root", _fixture]));
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
            Directory.Delete(_fixture, recursive: true);
        }
    }
}
