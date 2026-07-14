using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class InstallationValidationTests
{
    [Fact]
    public void RootValidation_rejectsMissingProcessRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "engloopkit-root-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var result = Evidence.ValidateRootLayout(root);
            Assert.False(result.Passed);
            Assert.Equal("missing-process-root", result.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RootValidation_rejectsForbiddenOldRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), "engloopkit-root-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, ".engloop"));
        Directory.CreateDirectory(Path.Combine(root, "engloop"));
        File.WriteAllText(Path.Combine(root, ".engloop", "config.json"), "{}");
        File.WriteAllText(Path.Combine(root, "NORTHSTAR.md"), "# n");
        File.WriteAllText(Path.Combine(root, "LEARNINGS.md"), "# l");

        try
        {
            var result = Evidence.ValidateRootLayout(root);
            Assert.False(result.Passed);
            Assert.Equal("forbidden-root-present", result.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ConfigSafety_rejectsWrongFixedPathsAndDuplicateModules()
    {
        var config = new EngLoopConfiguration(
            "1.0",
            "Invalid Product!",
            "engloop",
            "out",
            "northstar.md",
            new[]
            {
                new ModuleInventoryItem("core", "src/A.csproj"),
                new ModuleInventoryItem("core", "src/A.csproj")
            });

        var errors = Evidence.ValidateConfigurationSafety(config);
        Assert.Contains("unsupported-schema-version", errors);
        Assert.Contains("invalid-product-id", errors);
        Assert.Contains("invalid-artifact-root", errors);
        Assert.Contains("invalid-transient-output-root", errors);
        Assert.Contains("invalid-northstar-path", errors);
        Assert.Contains("duplicate-module-id", errors);
        Assert.Contains("duplicate-module-path", errors);
    }
}
