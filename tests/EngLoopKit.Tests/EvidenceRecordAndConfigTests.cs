using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class EvidenceRecordAndConfigTests
{
    [Fact]
    public void IncidentEvidence_reportsTimelineAndMitigationPresence()
    {
        var empty = new IncidentEvidence("IN001", [], [], false);
        Assert.False(empty.HasTimeline);
        Assert.False(empty.HasMitigations);

        var populated = new IncidentEvidence(
            "IN001",
            [new IncidentTimelineEntry(DateTimeOffset.UtcNow, "observed", "log")],
            [new MitigationEvidence("MIT001", "restart", DateTimeOffset.UtcNow)],
            true);
        Assert.True(populated.HasTimeline);
        Assert.True(populated.HasMitigations);

        var repair = new RepairLifecycle("RPI001", true, true, true, true, true);
        Assert.True(repair.IsClosed);
        Assert.Equal("closed", Evidence.ClassifyRepairLifecycle(repair));
    }

    [Fact]
    public void ConfigurationSafety_coversMissingCommandsRunwayAndInvalidProduct()
    {
        var missing = new EngLoopConfiguration(
            "2.0", "Valid Product!", ".engloop", ".engloop/out", "NORTHSTAR.md", [],
            TestRunway: null,
            ValidatorCommand: null,
            ModuleDiscoveryCommand: null,
            ArchitectureCommand: null,
            RegressionCommand: null,
            CoverageInputs: null);
        var errors = Evidence.ValidateConfigurationSafety(missing);
        Assert.Contains("invalid-product-id", errors);
        Assert.Contains("missing-validator-command", errors);
        Assert.Contains("missing-module-discovery-command", errors);
        Assert.Contains("missing-architecture-command", errors);
        Assert.Contains("missing-regression-command", errors);
        Assert.Contains("missing-coverage-inputs", errors);
        Assert.Contains("missing-test-runway", errors);

        var incomplete = missing with
        {
            ProductId = "valid-product",
            ValidatorCommand = ["dotnet"],
            ModuleDiscoveryCommand = ["tool"],
            ArchitectureCommand = ["architecture"],
            RegressionCommand = ["tests"],
            CoverageInputs = new Dictionary<string, string> { ["coverage"] = "report" },
            TestRunway = new TestRunwayConfiguration("proven", null, null, null, null, null, null),
        };
        var incompleteErrors = Evidence.ValidateConfigurationSafety(incomplete);
        Assert.Contains("missing-runway-framework", incompleteErrors);
        Assert.Contains("missing-runway-command", incompleteErrors);
        Assert.Contains("missing-runway-boundary-test", incompleteErrors);
        Assert.Contains("missing-runway-generated-destination", incompleteErrors);
        Assert.Contains("missing-runway-evidence-digest", incompleteErrors);
        Assert.Contains("missing-runway-revision", incompleteErrors);
        Assert.False(Evidence.IsTestRunwayProven(incomplete));

        var proven = incomplete with
        {
            TestRunway = new TestRunwayConfiguration("proven", "xunit", ["dotnet", "test"], "Boundary", "tests/generated", "digest", "revision"),
        };
        Assert.True(Evidence.IsTestRunwayProven(proven));
        Assert.Empty(Evidence.ValidateConfigurationSafety(proven));

        var invalidStatus = proven with
        {
            TestRunway = new TestRunwayConfiguration("unknown", null, null, null, null, null, null),
        };
        Assert.Contains("invalid-runway-status", Evidence.ValidateConfigurationSafety(invalidStatus));
        Assert.Equal("missing-current-readiness", Evidence.ClassifyRepairLifecycle(new RepairLifecycle("RPI002", true, true, true, true, false)));
    }

    [Fact]
    public void EngineeringLoop_rejectsUnknownEnumWithoutMutation()
    {
        var state = EngineeringLoopState.Initial;
        var result = EngineeringLoop.Evaluate(state, (Stage)999, new TransitionEvidence());
        Assert.False(result.Accepted);
        Assert.Equal(TransitionReasons.InvalidCommand, result.Reason);
        Assert.Same(state, result.State);
    }

    [Fact]
    public void LoadConfiguration_handlesNonArrayCommandsNullFieldsAndNonStringCoverageValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "elk-config-branch-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".engloop"));
            File.WriteAllText(Path.Combine(root, ".engloop", "config.json"), """
            {
              "schemaVersion": "2.0",
              "productId": "valid-product",
              "artifactRoot": ".engloop",
              "transientOutputRoot": ".engloop/out",
              "northstarPath": "NORTHSTAR.md",
              "moduleInventory": [],
              "validatorCommand": "not-an-array",
              "moduleDiscoveryCommand": [null],
              "architectureCommand": [],
              "regressionCommand": [],
              "coverageInputs": { "numeric": 1 },
              "testRunway": {
                "status": "unproven",
                "framework": null,
                "terseCommand": "not-an-array",
                "boundaryTest": null,
                "generatedDestination": null,
                "evidenceDigest": null,
                "provenAtRevision": null
              }
            }
            """);

            var config = Evidence.LoadConfiguration(root);
            Assert.Null(config.ValidatorCommand);
            Assert.Equal(string.Empty, Assert.Single(config.ModuleDiscoveryCommand!));
            Assert.Null(config.TestRunway!.TerseCommand);
            Assert.Equal(string.Empty, config.CoverageInputs!["numeric"]);
            var errors = Evidence.ValidateConfigurationSafety(config);
            Assert.Contains("missing-validator-command", errors);
            Assert.Contains("missing-module-discovery-command", errors);
            Assert.Contains("missing-architecture-command", errors);
            Assert.Contains("missing-regression-command", errors);
            Assert.Contains("invalid-coverage-inputs", errors);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
