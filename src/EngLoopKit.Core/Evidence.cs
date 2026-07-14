using System.Text.Json;

namespace EngLoopKit.Core;

public sealed record ModuleInventoryItem(string Id, string Path);

public sealed record TestRunwayConfiguration(
    string Status,
    string? Framework,
    IReadOnlyList<string>? TerseCommand,
    string? BoundaryTest,
    string? GeneratedDestination,
    string? EvidenceDigest,
    string? ProvenAtRevision);

public sealed record MitigationEvidence(string Id, string Description, DateTimeOffset CapturedAtUtc);

public sealed record IncidentTimelineEntry(DateTimeOffset AtUtc, string Event, string EvidenceRef);

public sealed record IncidentEvidence(
    string IncidentId,
    IReadOnlyList<IncidentTimelineEntry> Timeline,
    IReadOnlyList<MitigationEvidence> Mitigations,
    bool Stabilized)
{
    public bool HasTimeline => Timeline.Count > 0;

    public bool HasMitigations => Mitigations.Count > 0;
}

public sealed record RepairLifecycle(
    string RepairId,
    bool SourceImplemented,
    bool IncludedInReleaseArtifact,
    bool AppliedToTarget,
    bool TargetVerified,
    bool ReadinessPassAtTarget)
{
    public bool IsClosed =>
        SourceImplemented
        && IncludedInReleaseArtifact
        && AppliedToTarget
        && TargetVerified
        && ReadinessPassAtTarget;
}

public sealed record EngLoopConfiguration(
    string SchemaVersion,
    string ProductId,
    string ArtifactRoot,
    string TransientOutputRoot,
    string NorthstarPath,
    IReadOnlyList<ModuleInventoryItem> ModuleInventory,
    TestRunwayConfiguration? TestRunway = null,
    IReadOnlyList<string>? ValidatorCommand = null,
    IReadOnlyList<string>? ModuleDiscoveryCommand = null,
    IReadOnlyList<string>? ArchitectureCommand = null,
    IReadOnlyList<string>? RegressionCommand = null,
    IReadOnlyDictionary<string, string>? CoverageInputs = null);

public sealed record RootValidationResult(bool Passed, string Reason, string RepositoryRoot);

public static class Evidence
{
    public static RootValidationResult ValidateRootLayout(string repositoryRoot)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var targetRoot = Path.Combine(root, ".engloop");
        var forbiddenRoots = new[]
        {
            Path.Combine(root, "engloop"),
            Path.Combine(root, ".engloopkit")
        };

        if (!Directory.Exists(targetRoot))
        {
            return new RootValidationResult(false, "missing-process-root", root);
        }

        if (forbiddenRoots.Any(Directory.Exists))
        {
            return new RootValidationResult(false, "forbidden-root-present", root);
        }

        var configPath = Path.Combine(targetRoot, "config.json");
        if (!File.Exists(configPath))
        {
            return new RootValidationResult(false, "missing-config", root);
        }

        if (!File.Exists(Path.Combine(root, "NORTHSTAR.md")))
        {
            return new RootValidationResult(false, "missing-northstar", root);
        }

        if (!File.Exists(Path.Combine(root, "LEARNINGS.md")))
        {
            return new RootValidationResult(false, "missing-learnings", root);
        }

        return new RootValidationResult(true, "ok", root);
    }

    public static EngLoopConfiguration LoadConfiguration(string repositoryRoot)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var path = Path.Combine(root, ".engloop", "config.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("missing-config");
        }

        using var stream = File.OpenRead(path);
        var json = JsonDocument.Parse(stream);
        var obj = json.RootElement;

        static string StringOrEmpty(JsonElement value)
            => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

        var schemaVersion = StringOrEmpty(obj.GetProperty("schemaVersion"));
        var productId = StringOrEmpty(obj.GetProperty("productId"));
        var artifactRoot = StringOrEmpty(obj.GetProperty("artifactRoot"));
        var transientOutputRoot = StringOrEmpty(obj.GetProperty("transientOutputRoot"));
        var northstarPath = StringOrEmpty(obj.GetProperty("northstarPath"));

        var modules = new List<ModuleInventoryItem>();
        foreach (var module in obj.GetProperty("moduleInventory").EnumerateArray())
        {
            modules.Add(new ModuleInventoryItem(
                StringOrEmpty(module.GetProperty("id")),
                StringOrEmpty(module.GetProperty("path"))));
        }

        IReadOnlyList<string>? ReadCommand(string property)
        {
            if (!obj.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return value.EnumerateArray().Select(StringOrEmpty).ToArray();
        }

        TestRunwayConfiguration? runway = null;
        if (obj.TryGetProperty("testRunway", out var runwayValue) && runwayValue.ValueKind == JsonValueKind.Object)
        {
            string? StringValue(string name) => runwayValue.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

            IReadOnlyList<string>? runwayCommand = runwayValue.TryGetProperty("terseCommand", out var commandValue) && commandValue.ValueKind == JsonValueKind.Array
                ? commandValue.EnumerateArray().Select(StringOrEmpty).ToArray()
                : null;

            runway = new TestRunwayConfiguration(
                StringValue("status") ?? string.Empty,
                StringValue("framework"),
                runwayCommand,
                StringValue("boundaryTest"),
                StringValue("generatedDestination"),
                StringValue("evidenceDigest"),
                StringValue("provenAtRevision"));
        }

        var coverageInputs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (obj.TryGetProperty("coverageInputs", out var coverageValue) && coverageValue.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in coverageValue.EnumerateObject())
            {
                coverageInputs[property.Name] = StringOrEmpty(property.Value);
            }
        }

        return new EngLoopConfiguration(
            schemaVersion,
            productId,
            artifactRoot,
            transientOutputRoot,
            northstarPath,
            modules,
            runway,
            ReadCommand("validatorCommand"),
            ReadCommand("moduleDiscoveryCommand"),
            ReadCommand("architectureCommand"),
            ReadCommand("regressionCommand"),
            coverageInputs);
    }

    public static IReadOnlyList<string> ValidateConfigurationSafety(EngLoopConfiguration config)
    {
        var errors = new List<string>();

        if (!string.Equals(config.SchemaVersion, "2.0", StringComparison.Ordinal))
        {
            errors.Add("unsupported-schema-version");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(config.ProductId, "^[a-z0-9][a-z0-9-]{0,63}$"))
        {
            errors.Add("invalid-product-id");
        }

        if (!string.Equals(config.ArtifactRoot, ".engloop", StringComparison.Ordinal))
        {
            errors.Add("invalid-artifact-root");
        }

        if (!string.Equals(config.TransientOutputRoot, ".engloop/out", StringComparison.Ordinal))
        {
            errors.Add("invalid-transient-output-root");
        }

        if (!string.Equals(config.NorthstarPath, "NORTHSTAR.md", StringComparison.Ordinal))
        {
            errors.Add("invalid-northstar-path");
        }

        var duplicateId = config.ModuleInventory
            .GroupBy(m => m.Id, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        if (duplicateId)
        {
            errors.Add("duplicate-module-id");
        }

        var duplicatePath = config.ModuleInventory
            .GroupBy(m => m.Path, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        if (duplicatePath)
        {
            errors.Add("duplicate-module-path");
        }

        static bool MissingOrInvalidCommand(IReadOnlyList<string>? command)
            => command is null || command.Count == 0 || command.Any(string.IsNullOrWhiteSpace);

        if (MissingOrInvalidCommand(config.ValidatorCommand))
        {
            errors.Add("missing-validator-command");
        }

        if (MissingOrInvalidCommand(config.ModuleDiscoveryCommand))
        {
            errors.Add("missing-module-discovery-command");
        }

        if (MissingOrInvalidCommand(config.ArchitectureCommand))
        {
            errors.Add("missing-architecture-command");
        }

        if (MissingOrInvalidCommand(config.RegressionCommand))
        {
            errors.Add("missing-regression-command");
        }

        if (config.CoverageInputs is null || config.CoverageInputs.Count == 0)
        {
            errors.Add("missing-coverage-inputs");
        }
        else if (config.CoverageInputs.Any(input => string.IsNullOrWhiteSpace(input.Key) || string.IsNullOrWhiteSpace(input.Value)))
        {
            errors.Add("invalid-coverage-inputs");
        }

        if (config.TestRunway is null)
        {
            errors.Add("missing-test-runway");
        }
        else
        {
            var runway = config.TestRunway;
            if (runway.Status is not ("unproven" or "proving" or "proven"))
            {
                errors.Add("invalid-runway-status");
            }
            else if (runway.Status == "proven")
            {
                if (string.IsNullOrWhiteSpace(runway.Framework)) errors.Add("missing-runway-framework");
                if (runway.TerseCommand is null || runway.TerseCommand.Count == 0) errors.Add("missing-runway-command");
                if (string.IsNullOrWhiteSpace(runway.BoundaryTest)) errors.Add("missing-runway-boundary-test");
                if (string.IsNullOrWhiteSpace(runway.GeneratedDestination)) errors.Add("missing-runway-generated-destination");
                if (string.IsNullOrWhiteSpace(runway.EvidenceDigest)) errors.Add("missing-runway-evidence-digest");
                if (string.IsNullOrWhiteSpace(runway.ProvenAtRevision)) errors.Add("missing-runway-revision");
            }
        }

        return errors;
    }

    public static bool IsTestRunwayProven(EngLoopConfiguration config)
        => config.TestRunway is { Status: "proven", Framework: not null, TerseCommand: { Count: > 0 }, BoundaryTest: not null, GeneratedDestination: not null, EvidenceDigest: not null, ProvenAtRevision: not null };

    public static string ClassifyRepairLifecycle(RepairLifecycle lifecycle)
    {
        if (lifecycle.IsClosed)
        {
            return "closed";
        }

        if (!lifecycle.SourceImplemented)
        {
            return "missing-source";
        }

        if (!lifecycle.IncludedInReleaseArtifact)
        {
            return "missing-release-artifact";
        }

        if (!lifecycle.AppliedToTarget)
        {
            return "missing-target-apply";
        }

        if (!lifecycle.TargetVerified)
        {
            return "missing-target-verification";
        }

        return "missing-current-readiness";
    }
}
