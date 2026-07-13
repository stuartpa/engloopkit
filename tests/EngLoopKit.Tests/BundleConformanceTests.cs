using System.Text.Json;
using System.Text.RegularExpressions;
using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>
/// Structural conformance of the shipped bundle: the manifests, commands, templates, and
/// document standards. These are the "artifact coverage" tests — they catch exactly the
/// class of defect we expect while operating EngLoopKit (a malformed command, a missing
/// template, a version/prefix drift), and they couple the docs to the executable core.
/// </summary>
public sealed class BundleConformanceTests
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string ExtDir = Path.Combine(Root, "extensions", "engloopkit");

    private static readonly string[] CommandIds =
    [
        "speckit.engloop.01-northstar",
        "speckit.engloop.02-scaffold",
        "speckit.engloop.03-architect",
        "speckit.engloop.04-refactor",
        "speckit.engloop.05-model",
        "speckit.engloop.06-explore",
        "speckit.engloop.07-validate",
        "speckit.engloop.08-unittest",
        "speckit.engloop.20-incident",
        "speckit.engloop.21-postmortem",
        "speckit.engloop.22-repair",
        "speckit.engloop.30-refactor-scan",
        "speckit.engloop.31-learnings-pyramid",
    ];

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "bundle.yml")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "could not locate repo root (bundle.yml) above the test binary");
        return dir!.FullName;
    }

    private static string YamlValueAfter(string text, string anchorKey, string key)
    {
        var lines = text.Replace("\r", string.Empty).Split('\n');
        var ai = Array.FindIndex(lines, l => l.TrimStart().StartsWith(anchorKey, StringComparison.Ordinal));
        Assert.True(ai >= 0, $"anchor '{anchorKey}' not found");
        for (var i = ai + 1; i < lines.Length; i++)
        {
            var t = lines[i].Trim();
            if (t.StartsWith(key, StringComparison.Ordinal))
            {
                return t[key.Length..].Trim().Trim('"');
            }
        }

        Assert.Fail($"key '{key}' not found after '{anchorKey}'");
        return string.Empty;
    }

    [Fact]
    public void Version_isConsistentAcrossBundleExtensionAndCatalog()
    {
        var bundleVersion = YamlValueAfter(File.ReadAllText(Path.Combine(Root, "bundle.yml")), "bundle:", "version:");
        var extVersion = YamlValueAfter(File.ReadAllText(Path.Combine(ExtDir, "extension.yml")), "extension:", "version:");

        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "catalog.json")));
        var catVersion = catalog.RootElement.GetProperty("extensions")[0].GetProperty("version").GetString();

        Assert.Equal(extVersion, bundleVersion);
        Assert.Equal(extVersion, catVersion);
    }

    [Fact]
    public void Extension_declaresThirteenV2Commands_andEachFileExists()
    {
        var ext = File.ReadAllText(Path.Combine(ExtDir, "extension.yml"));
        var declared = Regex.Matches(ext, @"^\s*-\s*name:\s*""?(speckit\.engloop\.[\w.-]+)""?", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Equal(13, declared.Count);
        Assert.Equal(CommandIds, declared);

        // Every declared command references a file that exists.
        foreach (var file in Regex.Matches(ext, @"file:\s*""?(commands/[\w.\-]+\.md)""?").Select(m => m.Groups[1].Value))
        {
            Assert.True(File.Exists(Path.Combine(ExtDir, file)), $"missing command file: {file}");
        }
    }

    [Fact]
    public void Catalog_advertisesThirteenCommands()
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "catalog.json")));
        var count = catalog.RootElement.GetProperty("extensions")[0]
            .GetProperty("provides").GetProperty("commands").GetInt32();
        Assert.Equal(13, count);
    }

    [Fact]
    public void Commands_areWellFormedAsALoop()
    {
        foreach (var id in CommandIds)
        {
            var path = Path.Combine(ExtDir, "commands", $"{id}.md");
            Assert.True(File.Exists(path), $"missing command: {path}");
            var text = File.ReadAllText(path);

            // Frontmatter with required rich fields.
            Assert.StartsWith("---", text.TrimStart());
            Assert.Matches(new Regex(@"^name:", RegexOptions.Multiline), text);
            Assert.Matches(new Regex(@"^description:", RegexOptions.Multiline), text);
            Assert.Matches(new Regex(@"^argument-hint:", RegexOptions.Multiline), text);
            Assert.Contains("target: vscode", text);
            Assert.Contains("user-invocable: true", text);
            Assert.Contains("disable-model-invocation: true", text);
            Assert.Contains("tools:", text);
            Assert.Contains("agents:", text);
            Assert.Contains("hooks:", text);

            // Loop shape sections.
            Assert.Contains("## Loop definition", text);
            Assert.Contains("**Trigger:**", text);
            Assert.Contains("**Goal", text);
            Assert.Contains("**Verification:**", text);
            Assert.Contains("**Memory:**", text);
            Assert.Contains("## Artifact root", text);
            Assert.Contains("## Done when", text);

            if (id == "speckit.engloop.31-learnings-pyramid")
            {
                Assert.DoesNotContain("handoffs:", text);
            }
            else
            {
                Assert.Contains("handoffs:", text);
            }
        }
    }

    [Fact]
    public void EveryTemplateReferencedByACommandExists()
    {
        var templatesDir = Path.Combine(ExtDir, "templates");
        foreach (var id in CommandIds)
        {
            var text = File.ReadAllText(Path.Combine(ExtDir, "commands", $"{id}.md"));
            foreach (Match m in Regex.Matches(text, @"templates/([\w-]+\.md)"))
            {
                var template = m.Groups[1].Value;
                Assert.True(File.Exists(Path.Combine(templatesDir, template)), $"{id} references missing template: {template}");
            }
        }
    }

    [Fact]
    public void StandardsDoc_documentsEveryCorePrefix()
    {
        var standards = File.ReadAllText(Path.Combine(Root, "docs", "standards.md"));
        foreach (var prefix in NumberingRegistry.Prefixes)
        {
            Assert.Contains($"`{prefix}`", standards);
        }
    }

    [Fact]
    public void ArchitectAndRefactorScan_enforceTheComponentPattern()
    {
        // The methodology must keep propagating the component pattern to governed repos:
        // the architect command establishes it, and refactor-scan converges toward it.
        Assert.True(File.Exists(Path.Combine(Root, "docs", "component-pattern.md")),
            "the component-pattern principle doc must exist");

        var architect = File.ReadAllText(Path.Combine(ExtDir, "commands", "speckit.engloop.03-architect.md"));
        Assert.Contains("Loop definition", architect);

        var refactorScan = File.ReadAllText(Path.Combine(ExtDir, "commands", "speckit.engloop.30-refactor-scan.md"));
        Assert.Contains("Loop definition", refactorScan);
    }

    [Fact]
    public void Repo_followsTheComponentPattern()
    {
        // EngLoopKit eats its own dog food: a components/ folder of building blocks, composed
        // by the vertical (src/EngLoopKit.Core), which references at least one of them.
        var componentsDir = Path.Combine(Root, "components");
        Assert.True(Directory.Exists(componentsDir), "components/ folder must exist");
        Assert.NotEmpty(Directory.GetFiles(componentsDir, "*.csproj", SearchOption.AllDirectories));

        var coreCsproj = File.ReadAllText(Path.Combine(Root, "src", "EngLoopKit.Core", "EngLoopKit.Core.csproj"));
        Assert.Matches(new Regex(@"ProjectReference[^>]*components[\\/]EngLoopKit\.Components\."), coreCsproj);
    }
}
