using Xunit;

namespace EngLoopKit.Tests;

public sealed class SekProductBoundaryTests
{
    private static readonly string Root = FindRepoRoot();

    [Fact]
    public void Elk_consumesSekAsAnExternalProductWithoutVendoredManuals()
    {
        var extensionSkills = Path.Combine(Root, "extensions", "engloopkit", "skills");
        Assert.True(!Directory.Exists(extensionSkills) ||
                Directory.GetFiles(extensionSkills, "*", SearchOption.AllDirectories).Length == 0);
        Assert.False(File.Exists(Path.Combine(Root, "examples", "sek-walkthrough.md")));

        var installedSek = Path.Combine(Root, ".specify", "extensions", "sek");
        Assert.Contains("version: \"0.1.3\"", File.ReadAllText(Path.Combine(installedSek, "extension.yml")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(installedSek, "skills", "sek-cord-authoring", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(installedSek, "skills", "using-sek-to-generate-tests", "SKILL.md")));
        Assert.Contains("SpecExplorerKit.Modeling", File.ReadAllText(Path.Combine(installedSek, "skills", "using-sek-to-generate-tests", "SKILL.md")), StringComparison.Ordinal);

        foreach (var stage in new[] { "speckit.engloop.05-model", "speckit.engloop.06-explore" })
        {
            var command = File.ReadAllText(Path.Combine(Root, "extensions", "engloopkit", "commands", stage + ".md"));
            Assert.Contains(".specify/extensions/sek/skills/sek-cord-authoring/SKILL.md", command, StringComparison.Ordinal);
            Assert.Contains(".specify/extensions/sek/skills/using-sek-to-generate-tests/SKILL.md", command, StringComparison.Ordinal);
            Assert.Contains("Do not copy SEK documentation into ELK", command, StringComparison.Ordinal);
            Assert.DoesNotContain("Behavior precedence", command, StringComparison.Ordinal);
            Assert.DoesNotContain("Condition.In", command, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ElkGeneratedTests_useOnlyTheirSnapshottedBindingAssets()
    {
        var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "ci.yml"));
        Assert.DoesNotContain("SEK_BINDING", workflow, StringComparison.Ordinal);

        var generated = File.ReadAllText(Path.Combine(Root, "tests", "EngLoopKit.Loop.Generated", "ModelProgramTests.cs"));
        Assert.Contains("BindingAssets", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("SEK_BINDING", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultBinding", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ElkPinsExactReleasedSekV013AcrossToolModelGenerationAndCi()
    {
        var toolManifest = File.ReadAllText(Path.Combine(Root, ".config", "dotnet-tools.json"));
        Assert.Contains("specexplorerkit.tool", toolManifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"0.1.3\"", toolManifest, StringComparison.Ordinal);

        var modelProject = File.ReadAllText(Path.Combine(Root, "model", "EngLoopKit.Model", "EngLoopKit.Model.csproj"));
        Assert.Contains("SpecExplorerKit.Modeling", modelProject, StringComparison.Ordinal);
        Assert.Contains("Version=\"0.1.3\"", modelProject, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectReference", modelProject, StringComparison.Ordinal);

        var generation = File.ReadAllText(Path.Combine(Root, "scripts", "generate-loop-tests.ps1"));
        Assert.Contains("ensure-sek-v0.1.3.ps1", generation, StringComparison.Ordinal);
        Assert.Contains("dotnet tool run sek", generation, StringComparison.Ordinal);
        Assert.DoesNotContain("SEK/src", generation, StringComparison.OrdinalIgnoreCase);

        var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "ci.yml"));
        Assert.Contains("ensure-sek-v0.1.3.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("specify-cli==0.12.4", workflow, StringComparison.Ordinal);
        Assert.Contains("specify 0.12.4", workflow, StringComparison.Ordinal);
        Assert.Contains("specify init --here --force --integration copilot --script sh --ignore-agent-tools", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet-version: \"10.0.303\"", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("DOTNET_ROLL_FORWARD", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet build EngLoopKit.slnx", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test EngLoopKit.slnx", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Checkout SEK", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void ElkUsesOnlyNet10AndTheCompleteSlnxGraph()
    {
        using var global = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "global.json")));
        Assert.Equal("10.0.303", global.RootElement.GetProperty("sdk").GetProperty("version").GetString());

        var projects = Directory.GetFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".engloop"))
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.True(projects.Length == 9, $"Expected 9 projects in the .slnx graph; found {projects.Length}.");
        foreach (var project in projects)
            Assert.Contains("<TargetFramework>net10.0</TargetFramework>", File.ReadAllText(Path.Combine(Root, project)), StringComparison.Ordinal);

        var solution = File.ReadAllText(Path.Combine(Root, "EngLoopKit.slnx"));
        foreach (var project in projects) Assert.Contains($"Project Path=\"{project}\"", solution.Replace('\\', '/'), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Root, "EngLoopKit.sln")));

        var extension = File.ReadAllText(Path.Combine(Root, "extensions", "engloopkit", "extension.yml"));
        Assert.Contains("name: \"dotnet\"", extension, StringComparison.Ordinal);
        Assert.Contains("version: \">=10.0\"", extension, StringComparison.Ordinal);

        using var catalog = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "catalog.json")));
        var tools = catalog.RootElement.GetProperty("extensions")[0].GetProperty("requires").GetProperty("tools");
        Assert.Contains(tools.EnumerateArray(), tool => tool.GetProperty("name").GetString() == "dotnet" && tool.GetProperty("version").GetString() == ">=10.0");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bundle.yml")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
