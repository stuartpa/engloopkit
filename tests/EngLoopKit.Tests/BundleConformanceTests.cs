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

    private static readonly string[] CommandNames =
    [
        "seed", "architect", "model", "explore", "coverage",
        "incident", "postmortem", "repair", "refactor-scan",
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
    public void Extension_declaresNineCommands_andEachFileExists()
    {
        var ext = File.ReadAllText(Path.Combine(ExtDir, "extension.yml"));
        var declared = Regex.Matches(ext, @"^\s*-\s*name:\s*""?(speckit\.engloopkit\.[\w-]+)""?", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Equal(9, declared.Count);

        // Every declared command references a file that exists.
        foreach (var file in Regex.Matches(ext, @"file:\s*""?(commands/[\w.\-]+\.md)""?").Select(m => m.Groups[1].Value))
        {
            Assert.True(File.Exists(Path.Combine(ExtDir, file)), $"missing command file: {file}");
        }
    }

    [Fact]
    public void Catalog_advertisesNineCommands()
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "catalog.json")));
        var count = catalog.RootElement.GetProperty("extensions")[0]
            .GetProperty("provides").GetProperty("commands").GetInt32();
        Assert.Equal(9, count);
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("architect")]
    [InlineData("model")]
    [InlineData("explore")]
    [InlineData("coverage")]
    [InlineData("incident")]
    [InlineData("postmortem")]
    [InlineData("repair")]
    [InlineData("refactor-scan")]
    public void Command_isWellFormedAsALoop(string name)
    {
        var path = Path.Combine(ExtDir, "commands", $"speckit.engloopkit.{name}.md");
        Assert.True(File.Exists(path), $"missing command: {path}");
        var text = File.ReadAllText(path);

        // Frontmatter with a description.
        Assert.StartsWith("---", text.TrimStart());
        Assert.Matches(new Regex(@"^description:", RegexOptions.Multiline), text);

        // Every command is written as a Loop with the five Loop-Engineering components,
        // an artifact-root note, and a Done-when checklist.
        Assert.Contains("## Loop definition", text);
        Assert.Contains("**Trigger:**", text);
        Assert.Contains("**Goal", text);
        Assert.Contains("**Verification:**", text);
        Assert.Contains("**Memory:**", text);
        Assert.Contains("## Artifact root", text);
        Assert.Contains("## Done when", text);
    }

    [Fact]
    public void EveryTemplateReferencedByACommandExists()
    {
        var templatesDir = Path.Combine(ExtDir, "templates");
        foreach (var name in CommandNames)
        {
            var text = File.ReadAllText(Path.Combine(ExtDir, "commands", $"speckit.engloopkit.{name}.md"));
            foreach (Match m in Regex.Matches(text, @"templates/([\w-]+\.md)"))
            {
                var template = m.Groups[1].Value;
                Assert.True(File.Exists(Path.Combine(templatesDir, template)), $"{name} references missing template: {template}");
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
}
