using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class EvidenceAndLearningTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "engloopkit-evidence-" + Guid.NewGuid().ToString("N"));

    public EvidenceAndLearningTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void RootAndConfiguration_validation_acceptsCanonicalLayoutAndRejectsBadConfig()
    {
        CreateCanonicalRoot();
        var root = Evidence.ValidateRootLayout(_root);
        Assert.True(root.Passed);

        var config = Evidence.LoadConfiguration(_root);
        Assert.Empty(Evidence.ValidateConfigurationSafety(config));

        var bad = config with
        {
            SchemaVersion = "9.0",
            ProductId = "other",
            ArtifactRoot = "engloop",
            TransientOutputRoot = "out",
            NorthstarPath = "northstar.md",
            ModuleInventory = [new ModuleInventoryItem("a", "x"), new ModuleInventoryItem("a", "x")],
        };
        var errors = Evidence.ValidateConfigurationSafety(bad);
        Assert.Contains("unsupported-schema-version", errors);
        Assert.Contains("unsupported-product-id", errors);
        Assert.Contains("invalid-artifact-root", errors);
        Assert.Contains("invalid-transient-output-root", errors);
        Assert.Contains("invalid-northstar-path", errors);
        Assert.Contains("duplicate-module-id", errors);
        Assert.Contains("duplicate-module-path", errors);
    }

    [Fact]
    public void RepairLifecycle_classifiesEveryOpenGateAndClosure()
    {
        Assert.Equal("missing-source", Evidence.ClassifyRepairLifecycle(new RepairLifecycle("r", false, false, false, false, false)));
        Assert.Equal("missing-release-artifact", Evidence.ClassifyRepairLifecycle(new RepairLifecycle("r", true, false, false, false, false)));
        Assert.Equal("missing-target-apply", Evidence.ClassifyRepairLifecycle(new RepairLifecycle("r", true, true, false, false, false)));
        Assert.Equal("missing-target-verification", Evidence.ClassifyRepairLifecycle(new RepairLifecycle("r", true, true, true, false, false)));
        Assert.Equal("missing-current-readiness", Evidence.ClassifyRepairLifecycle(new RepairLifecycle("r", true, true, true, true, false)));
        Assert.Equal("closed", Evidence.ClassifyRepairLifecycle(new RepairLifecycle("r", true, true, true, true, true)));
    }

    [Fact]
    public void LearningsPolicy_extractsAndValidatesProvenanceAndRetrieval()
    {
        var postmortems = Path.Combine(_root, ".engloop", "postmortems");
        var cards = Path.Combine(_root, ".engloop", "learnings", "cards");
        Directory.CreateDirectory(postmortems);
        Directory.CreateDirectory(cards);
        File.WriteAllText(Path.Combine(postmortems, "PM001_example.md"), "## Learnings\n- PM001/LEARN001\n- PM001/LEARN002\n");
        File.WriteAllText(Path.Combine(cards, "example.md"), "# Card\n## Tensions\nnone known\nPM001/LEARN001\nPM001/LEARN002\n");
        var index = Path.Combine(_root, "LEARNINGS.md");
        File.WriteAllText(index, "[example](.engloop/learnings/cards/example.md)\n");

        var sources = LearningsPyramidPolicy.ExtractSources(postmortems);
        var extractedCards = LearningsPyramidPolicy.ExtractCards(cards);
        var good = LearningsPyramidPolicy.Validate(
            index,
            sources,
            extractedCards,
            new Dictionary<string, IReadOnlyCollection<string>> { ["case"] = ["PM001/LEARN001"] },
            new Dictionary<string, IReadOnlyCollection<string>> { ["case"] = ["PM001/LEARN001"] });
        Assert.True(good.Passed);

        var bad = LearningsPyramidPolicy.Validate(
            index,
            sources,
            extractedCards,
            new Dictionary<string, IReadOnlyCollection<string>> { ["case"] = ["PM001/LEARN002"] },
            new Dictionary<string, IReadOnlyCollection<string>> { ["case"] = ["PM001/LEARN001"] });
        Assert.False(bad.Passed);
        Assert.Contains(bad.Failures, value => value.StartsWith("retrieval-missing-id:case:PM001/LEARN001", StringComparison.Ordinal));
        Assert.Contains(bad.Failures, value => value.StartsWith("retrieval-false-provenance:case:PM001/LEARN002", StringComparison.Ordinal));
    }

    private void CreateCanonicalRoot()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".engloop"));
        File.WriteAllText(Path.Combine(_root, "NORTHSTAR.md"), "# Direction");
        File.WriteAllText(Path.Combine(_root, "LEARNINGS.md"), "# Learnings");
        File.WriteAllText(Path.Combine(_root, ".engloop", "config.json"), """
        {
          "schemaVersion":"2.0",
          "productId":"engloopkit",
          "artifactRoot":".engloop",
          "transientOutputRoot":".engloop/out",
          "northstarPath":"NORTHSTAR.md",
                    "validatorCommand":["dotnet","tool","run","engloopkit","--"],
                    "moduleDiscoveryCommand":["pwsh","-File","scripts/discover-modules.ps1"],
                    "architectureCommand":["dotnet","tool","run","engloopkit","--","validate","root","--root","."],
                    "regressionCommand":["dotnet","test","tests/tests.csproj"],
                    "coverageInputs":{"wholeProduct":"tests/tests.csproj"},
                    "testRunway":{
                        "status":"proven",
                        "framework":"xunit",
                        "terseCommand":["dotnet","test","tests/tests.csproj"],
                        "boundaryTest":"Fixture.Boundary",
                        "generatedDestination":"tests/generated",
                        "evidenceDigest":"fixture-digest",
                        "provenAtRevision":"content:fixture-digest"
                    },
                    "moduleInventory":[{"id":"core","path":"src/core.csproj"}]
        }
        """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
