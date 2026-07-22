using System.Text.Json;
using System.Text.RegularExpressions;
using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>Release-facing structural checks for the ordered v1.8 source surface.</summary>
public sealed class BundleConformanceTests
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string ExtensionRoot = Path.Combine(Root, "extensions", "engloopkit");

    private static readonly string[] ExpectedIds =
    [
        "speckit.engloop.01-northstar",
        "speckit.engloop.02-scaffold",
        "speckit.engloop.03-architect",
        "speckit.engloop.04-refactor",
        "speckit.engloop.05-model",
        "speckit.engloop.06-explore",
        "speckit.engloop.07-validate",
        "speckit.engloop.08-unittest",
        "speckit.engloop.09-codereview-prepare",
        "speckit.engloop.20-incident",
        "speckit.engloop.21-postmortem",
        "speckit.engloop.22-repair",
        "speckit.engloop.30-refactor-scan",
        "speckit.engloop.31-learnings-pyramid",
        "speckit.engloop.40-pomodoro-create",
        "speckit.engloop.50-overlay-pack",
        "speckit.engloop.51-overlay-remove",
    ];

    [Fact]
    public void VersionAndIdentity_areConsistentAcrossReleaseMetadata()
    {
        var extension = File.ReadAllText(Path.Combine(ExtensionRoot, "extension.yml"));
        var bundle = File.ReadAllText(Path.Combine(Root, "bundle.yml"));
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "catalog.json")));

        Assert.Contains("id: \"engloop\"", extension);
        Assert.Contains("version: \"1.9.1\"", extension);
        Assert.Contains("id: \"engloopkit\"", bundle);
        Assert.Contains("version: \"1.9.1\"", bundle);
        Assert.Equal("engloop", catalog.RootElement.GetProperty("extensions")[0].GetProperty("id").GetString());
        Assert.Equal("1.9.1", catalog.RootElement.GetProperty("extensions")[0].GetProperty("version").GetString());
        Assert.Equal(17, catalog.RootElement.GetProperty("extensions")[0].GetProperty("provides").GetProperty("commands").GetInt32());
    }

    [Fact]
    public void Extension_declaresExactOrderedSeventeenCommandSurface()
    {
        var manifest = File.ReadAllText(Path.Combine(ExtensionRoot, "extension.yml"));
        var ids = Regex.Matches(manifest, @"^\s*-\s*name:\s*""?(speckit\.engloop\.[\w-]+)""?", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToArray();
        Assert.Equal(ExpectedIds, ids);

        var commandDirectory = Path.Combine(ExtensionRoot, "commands");
        var files = Directory.GetFiles(commandDirectory, "speckit.engloop.*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedIds, files);
        Assert.Empty(Directory.GetFiles(commandDirectory, "speckit.engloopkit.*.md", SearchOption.TopDirectoryOnly));
    }

    [Theory]
    [MemberData(nameof(CommandIds))]
    public void EveryCommand_hasLoopContractAndMatchingPrompt(string id)
    {
        var command = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", id + ".md"));
        Assert.StartsWith("---", command.TrimStart());
        Assert.Contains("name: " + id, command);
        Assert.Contains("## User Input", command);
        Assert.Contains("## Artifact root", command);
        Assert.Contains("## Loop definition", command);
        Assert.Contains("**Trigger:**", command);
        Assert.Contains("**Goal:", command);
        Assert.Contains("**Actions:**", command);
        Assert.Contains("**Verification:**", command);
        Assert.Contains("**Memory:**", command);
        Assert.Contains("## Done when", command);

        var prompt = File.ReadAllText(Path.Combine(Root, ".github", "prompts", id + ".prompt.md"));
        Assert.Contains("agent: " + id, prompt);
        Assert.DoesNotContain("tools:", prompt);
    }

    [Fact]
    public void OverlayPackCommand_describesPrivateLocalOnlyContract()
    {
        var command = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.50-overlay-pack.md"));
        Assert.Contains(".git/info/exclude", command);
        Assert.Contains("overlay pack", command);
        Assert.Contains("unencrypted", command);
        Assert.Contains("never edits tracked `.gitignore`", command);
    }

    [Fact]
    public void NewUtilityCommands_haveTheirRequiredBoundaries()
    {
        var review = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.09-codereview-prepare.md"));
        Assert.Contains("github", review, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("azure-devops", review, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no persistent personal profile", review, StringComparison.OrdinalIgnoreCase);

        var pom = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.40-pomodoro-create.md"));
        Assert.Contains("POM0000", pom, StringComparison.Ordinal);
        Assert.Contains("30–60", pom, StringComparison.Ordinal);
        Assert.Contains("POM<NNNN>-<brief-kebab-description>.md", pom, StringComparison.Ordinal);

        var remove = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.51-overlay-remove.md"));
        Assert.Contains("REMOVE-OVERLAY:<repository-id>@<base-revision>", remove, StringComparison.Ordinal);
        Assert.Contains("restore", remove, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Standards_matchExecutableNumberingVocabulary()
    {
        var standards = File.ReadAllText(Path.Combine(Root, "docs", "standards.md"));
        foreach (var prefix in NumberingRegistry.Prefixes)
        {
            Assert.Contains("`" + prefix + "`", standards);
        }
        Assert.Contains(".engloop", standards);
    }

    [Fact]
    public void PublicPolicy_explicitlyForbidsUiValidation()
    {
        var architecture = File.ReadAllText(Path.Combine(Root, ".engloop", "architecture", "ARCH006_deterministic-agent-surface-validation.md"));
        Assert.Contains("never performs UI validation", architecture);
        var validator = File.ReadAllText(Path.Combine(Root, "scripts", "validate-agent-surfaces.ps1"));
        Assert.Contains("No UI validation", validator);
    }

    public static IEnumerable<object[]> CommandIds() => ExpectedIds.Select(id => new object[] { id });

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bundle.yml")))
        {
            directory = directory.Parent;
        }
        Assert.True(directory is not null, "could not locate repository root");
        return directory!.FullName;
    }
}
