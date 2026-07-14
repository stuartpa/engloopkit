using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class CoreNegativeContractTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "engloopkit-core-negative-" + Guid.NewGuid().ToString("N"));

    public CoreNegativeContractTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("missing-process-root")]
    [InlineData("forbidden-root-present")]
    [InlineData("missing-config")]
    [InlineData("missing-northstar")]
    [InlineData("missing-learnings")]
    public void RootLayout_reportsEachMissingOrForbiddenBoundary(string expected)
    {
        if (expected == "missing-process-root")
        {
            Assert.Equal(expected, Evidence.ValidateRootLayout(_root).Reason);
            return;
        }

        Directory.CreateDirectory(Path.Combine(_root, ".engloop"));
        if (expected == "forbidden-root-present")
        {
            Directory.CreateDirectory(Path.Combine(_root, "engloop"));
            Assert.Equal(expected, Evidence.ValidateRootLayout(_root).Reason);
            return;
        }

        if (expected != "missing-config")
        {
            File.WriteAllText(Path.Combine(_root, ".engloop", "config.json"), "{}");
        }
        if (expected == "missing-config")
        {
            Assert.Equal(expected, Evidence.ValidateRootLayout(_root).Reason);
            return;
        }

        if (expected == "missing-learnings")
        {
            File.WriteAllText(Path.Combine(_root, "NORTHSTAR.md"), "# n");
            Assert.Equal(expected, Evidence.ValidateRootLayout(_root).Reason);
            return;
        }

        Assert.Equal(expected, Evidence.ValidateRootLayout(_root).Reason);
    }

    [Fact]
    public void LoadConfiguration_rejectsMissingConfigAndInvalidJson()
    {
        Assert.Throws<InvalidOperationException>(() => Evidence.LoadConfiguration(_root));
        Directory.CreateDirectory(Path.Combine(_root, ".engloop"));
        File.WriteAllText(Path.Combine(_root, ".engloop", "config.json"), "not-json");
        Assert.ThrowsAny<Exception>(() => Evidence.LoadConfiguration(_root));
    }

    [Fact]
    public void ConfigurationSafety_rejectsNullRequiredStringAndModuleFields()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".engloop"));
        File.WriteAllText(Path.Combine(_root, ".engloop", "config.json"), """
        {
          "schemaVersion": null,
          "productId": null,
          "artifactRoot": null,
          "transientOutputRoot": null,
          "northstarPath": null,
          "moduleInventory": [{ "id": null, "path": null }]
        }
        """);
        var config = Evidence.LoadConfiguration(_root);
        var errors = Evidence.ValidateConfigurationSafety(config);
        Assert.Contains("unsupported-schema-version", errors);
        Assert.Contains("invalid-product-id", errors);
        Assert.Contains("invalid-artifact-root", errors);
        Assert.Contains("invalid-transient-output-root", errors);
        Assert.Contains("invalid-northstar-path", errors);
    }

    [Fact]
    public void ReadinessGate_reportsEveryIndependentFailure()
    {
        var result = ReadinessGate.Evaluate(
        [
            new ReadinessRow("bad", 94.9, 94.9, false, false),
        ]);
        Assert.False(result.Passed);
        Assert.Contains("architecture-fail:bad", result.Failures);
        Assert.Contains("regression-fail:bad", result.Failures);
        Assert.Contains(result.Failures, f => f.StartsWith("line-coverage-below-threshold:bad", StringComparison.Ordinal));
        Assert.Contains(result.Failures, f => f.StartsWith("branch-coverage-below-threshold:bad", StringComparison.Ordinal));
    }

    [Fact]
    public void RepairObligation_requiresEveryClosureFactAndCurrentReadiness()
    {
        var notReady = EngineeringLoopState.Initial;
        var complete = new RepairObligation("r", true, true, true, false);
        Assert.False(complete.CanClose(notReady));

        var ready = notReady with { ProductRevision = "4", Readiness = new ReadinessEvidence(true, "4", DateTimeOffset.UtcNow) };
        Assert.True(complete.CanClose(ready));
        Assert.False(new RepairObligation("r", false, true, true, false).CanClose(ready));
        Assert.False(new RepairObligation("r", true, false, true, false).CanClose(ready));
        Assert.False(new RepairObligation("r", true, true, false, false).CanClose(ready));
    }

    [Fact]
    public void LearningsPolicy_rejectsMissingIndexSourcesCardsAndBadCards()
    {
        var emptySources = Array.Empty<LearningSource>();
        var emptyCards = Array.Empty<LearningCard>();
        var missing = LearningsPyramidPolicy.Validate(Path.Combine(_root, "none.md"), emptySources, emptyCards);
        Assert.False(missing.Passed);
        Assert.Contains("missing-learnings-index", missing.Failures);

        var index = Path.Combine(_root, "LEARNINGS.md");
        File.WriteAllText(index, "# index\n");
        var absent = LearningsPyramidPolicy.Validate(index, emptySources, emptyCards);
        Assert.Contains("missing-learning-sources", absent.Failures);
        Assert.Contains("missing-learning-cards", absent.Failures);

        var cards = Path.Combine(_root, "cards");
        Directory.CreateDirectory(cards);
        File.WriteAllText(Path.Combine(cards, "bad.md"), "# no provenance\n");
        LearningSource[] sources = [new LearningSource("PM001/LEARN001", "pm.md", "pm")];
        var result = LearningsPyramidPolicy.Validate(index, sources, LearningsPyramidPolicy.ExtractCards(cards));
        Assert.False(result.Passed);
        Assert.Contains(result.Failures, f => f.StartsWith("card-without-source:bad", StringComparison.Ordinal));
        Assert.Contains(result.Failures, f => f.StartsWith("card-missing-tension:bad", StringComparison.Ordinal));
        Assert.Contains(result.Failures, f => f.StartsWith("uncovered-source:PM001/LEARN001", StringComparison.Ordinal));
    }

    [Fact]
    public void LearningsPolicy_handlesAbsentRootsBudgetsAndRetrievalCaseMismatches()
    {
        Assert.Empty(LearningsPyramidPolicy.ExtractSources(Path.Combine(_root, "no-postmortems")));
        Assert.Empty(LearningsPyramidPolicy.ExtractCards(Path.Combine(_root, "no-cards")));

        var postmortems = Path.Combine(_root, "postmortems");
        var cards = Path.Combine(_root, "cards2");
        Directory.CreateDirectory(postmortems);
        Directory.CreateDirectory(cards);
        File.WriteAllText(Path.Combine(postmortems, "PM002.md"), "PM002/LEARN001\n");
        File.WriteAllText(Path.Combine(cards, "card.md"), "## Tensions\nnone known\nPM002/LEARN001\n");
        var index = Path.Combine(_root, "big.md");
        File.WriteAllText(index, string.Join("\n", Enumerable.Repeat("word", 501)) + "\n[card](.engloop/learnings/cards/card.md)");
        var sources = LearningsPyramidPolicy.ExtractSources(postmortems);
        var extracted = LearningsPyramidPolicy.ExtractCards(cards);

        var result = LearningsPyramidPolicy.Validate(
            index,
            sources,
            extracted,
            new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["unexpected"] = ["PM002/LEARN001"],
            },
            new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["missing"] = ["PM002/LEARN001"],
            });

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, value => value.StartsWith("index-word-budget-exceeded", StringComparison.Ordinal));
        Assert.Contains(result.Failures, value => string.Equals(value, "retrieval-missing-case:missing", StringComparison.Ordinal));
        Assert.Contains(result.Failures, value => string.Equals(value, "retrieval-unexpected-case:unexpected", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
