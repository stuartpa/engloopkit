using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>Failure fixtures for the exact v2 command/prompt surface validator.</summary>
public sealed class ToolSurfaceFailureTests : IDisposable
{
    private static readonly string SourceRoot = FindRepoRoot();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "engloopkit-surface-" + Guid.NewGuid().ToString("N"));

    public ToolSurfaceFailureTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ValidateCommands_rejectsMissingDirectoryAndCommandSetDrift()
    {
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        CopyCommandSurface();
        File.Delete(Path.Combine(_root, "extensions", "engloopkit", "commands", "speckit.engloop.01-northstar.md"));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        CopyCommandSurface();
        File.WriteAllText(Path.Combine(_root, "extensions", "engloopkit", "commands", "speckit.engloop.99-extra.md"), "---\nname: speckit.engloop.99-extra\ntools: []\nagents: []\nhandoffs: []\n---\n");
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));
    }

    [Theory]
    [InlineData("speckit.engloop.01-northstar.md", "---\nname: wrong\ntools: [read]\nagents: [Explore]\nhandoffs: []\n---\n")]
    [InlineData("speckit.engloop.02-scaffold.md", "---\nname: speckit.engloop.02-scaffold\ninfer: true\ntools: [read, search, edit, execute, web]\nagents: []\nhandoffs: []\n---\n")]
    [InlineData("speckit.engloop.03-architect.md", "no frontmatter")]
    public void ValidateCommands_rejectsMalformedIdentityOrForbiddenFields(string file, string replacement)
    {
        CopyCommandSurface();
        File.WriteAllText(Path.Combine(_root, "extensions", "engloopkit", "commands", file), replacement);
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));
    }

    [Fact]
    public void ValidateCommands_rejectsMissingNameAndNullPolicyItems()
    {
        CopyCommandSurface();
        var commands = Path.Combine(_root, "extensions", "engloopkit", "commands");
        var stage02 = Path.Combine(commands, "speckit.engloop.02-scaffold.md");
        File.WriteAllText(stage02, File.ReadAllText(stage02).Replace("name: speckit.engloop.02-scaffold", "# name removed", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        CopyCommandSurface();
        File.WriteAllText(stage02, File.ReadAllText(stage02).Replace("tools: [read, search, edit, execute, web]", "tools: [null]", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        CopyCommandSurface();
        File.WriteAllText(stage02, File.ReadAllText(stage02).Replace("agents: []", "agents: [null]", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        CopyCommandSurface();
        File.WriteAllText(stage02, File.ReadAllText(stage02).Replace("agents: []", "# agents absent", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        CopyCommandSurface();
        File.WriteAllText(stage02, File.ReadAllText(stage02).Replace("name: speckit.engloop.02-scaffold", "name:", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));
    }

    [Fact]
    public void ValidateCommands_rejectsLegacySurfaceAndMissingRequiredHandoffs()
    {
        CopyCommandSurface();
        var commands = Path.Combine(_root, "extensions", "engloopkit", "commands");
        File.WriteAllText(Path.Combine(commands, "speckit.engloopkit.legacy.md"), "legacy");
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        File.Delete(Path.Combine(commands, "speckit.engloopkit.legacy.md"));
        var stage20 = Path.Combine(commands, "speckit.engloop.20-incident.md");
        var stageText = File.ReadAllText(stage20);
        var headerEnd = stageText.IndexOf("\n---", 4, StringComparison.Ordinal);
        var header = stageText[..headerEnd];
        var body = stageText[headerEnd..];
        header = System.Text.RegularExpressions.Regex.Replace(header, "(?ms)^handoffs:\r?\n.*$", string.Empty);
        File.WriteAllText(stage20, header + body);
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));
    }

    [Fact]
    public void ValidateCommands_rejectsToolsAgentsAndStage31HandoffPolicy()
    {
        CopyCommandSurface();
        var commands = Path.Combine(_root, "extensions", "engloopkit", "commands");
        var stage02 = Path.Combine(commands, "speckit.engloop.02-scaffold.md");
        File.WriteAllText(stage02, File.ReadAllText(stage02).Replace("tools: [read, search, edit, execute, web]", "tools: [read]", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        CopyCommandSurface();
        File.WriteAllText(stage02, File.ReadAllText(stage02).Replace("agents: []", "agents: [Explore]", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        CopyCommandSurface();
        var stage31 = Path.Combine(commands, "speckit.engloop.31-learnings-pyramid.md");
        File.WriteAllText(stage31, File.ReadAllText(stage31).Replace("\n---", "\nhandoffs: []\n---", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        CopyCommandSurface();
        File.WriteAllText(stage02, File.ReadAllText(stage02).Replace("tools: [read, search, edit, execute, web]", "# tools absent", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));

        CopyCommandSurface();
        File.WriteAllText(stage02, File.ReadAllText(stage02).Replace("agents: []", "agents: invalid", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateCommands(["--root", _root]));
    }

    [Fact]
    public void ValidateAgentSurfaces_rejectsPromptAndHandoffViolations()
    {
        CopyCommandSurface();
        CopyPromptSurface();
        var prompts = Path.Combine(_root, ".github", "prompts");
        File.Delete(Path.Combine(prompts, "speckit.engloop.01-northstar.prompt.md"));
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));

        CopyPromptSurface(overwrite: true);
        var prompt = Path.Combine(prompts, "speckit.engloop.02-scaffold.prompt.md");
        File.WriteAllText(prompt, "---\nagent: speckit.engloop.02-scaffold\ntools: [execute]\n---\n");
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));

        CopyPromptSurface(overwrite: true);
        var stage20 = Path.Combine(_root, "extensions", "engloopkit", "commands", "speckit.engloop.20-incident.md");
        File.WriteAllText(stage20, File.ReadAllText(stage20).Replace("send: false", "send: true", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));
    }

    [Fact]
    public void ValidateAgentSurfaces_rejectsMalformedWrongAgentAndModelOverride()
    {
        CopyCommandSurface();
        CopyPromptSurface();
        var prompts = Path.Combine(_root, ".github", "prompts");
        var prompt = Path.Combine(prompts, "speckit.engloop.03-architect.prompt.md");
        File.WriteAllText(prompt, "not frontmatter");
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));

        CopyPromptSurface(overwrite: true);
        File.WriteAllText(prompt, "---\nagent: speckit.engloop.02-scaffold\n---\n");
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));

        CopyPromptSurface(overwrite: true);
        var stage20 = Path.Combine(_root, "extensions", "engloopkit", "commands", "speckit.engloop.20-incident.md");
        File.WriteAllText(stage20, File.ReadAllText(stage20).Replace("send: false", "send: false\n    model: forbidden", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));
    }

    [Fact]
    public void ValidateAgentSurfaces_rejectsMissingPromptAgentAndWrongHandoffCount()
    {
        CopyCommandSurface();
        CopyPromptSurface();
        var prompt = Path.Combine(_root, ".github", "prompts", "speckit.engloop.04-refactor.prompt.md");
        File.WriteAllText(prompt, "---\n# agent absent\n---\n");
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));

        CopyPromptSurface(overwrite: true);
        File.WriteAllText(prompt, "---\nagent:\n---\n");
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));

        CopyPromptSurface(overwrite: true);
        var stage20 = Path.Combine(_root, "extensions", "engloopkit", "commands", "speckit.engloop.20-incident.md");
        var text = File.ReadAllText(stage20);
        var headerEnd = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        var header = text[..headerEnd];
        header = System.Text.RegularExpressions.Regex.Replace(header, "(?ms)^handoffs:\r?\n.*$", string.Empty);
        File.WriteAllText(stage20, header + text[headerEnd..]);
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));

        CopyPromptSurface(overwrite: true);
        text = File.ReadAllText(stage20);
        File.WriteAllText(stage20, text.Replace("send: false", "# send absent", StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));
    }

    [Fact]
    public void ValidateAgentSurfaces_rejectsMissingSpecificPromptAndMalformedHandoffShape()
    {
        CopyCommandSurface();
        CopyPromptSurface();
        var prompts = Path.Combine(_root, ".github", "prompts");
        File.Delete(Path.Combine(prompts, "speckit.engloop.01-northstar.prompt.md"));
        File.WriteAllText(Path.Combine(prompts, "speckit.engloop.99-extra.prompt.md"), "---\nagent: speckit.engloop.99-extra\n---\n");
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));

        CopyPromptSurface(overwrite: true);
        var stage20 = Path.Combine(_root, "extensions", "engloopkit", "commands", "speckit.engloop.20-incident.md");
        var text = File.ReadAllText(stage20);
        var headerEnd = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        File.WriteAllText(stage20, text[..headerEnd].Replace("handoffs:", "handoffs: invalid", StringComparison.Ordinal) + text[headerEnd..]);
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));
    }

    [Fact]
    public void Program_dispatchesEveryValidatorEndpoint()
    {
        Assert.Equal(0, Program.Main(["validate", "root", "--root", SourceRoot]));
        Assert.Equal(0, Program.Main(["validate", "config", "--root", SourceRoot]));
        Assert.Equal(0, Program.Main(["validate", "commands", "--root", SourceRoot]));
        Assert.Equal(0, Program.Main(["validate", "reachability", "--root", SourceRoot]));
        Assert.Equal(0, Program.Main(["validate", "learnings", "--root", SourceRoot]));
        Assert.Equal(0, Program.Main(["validate", "installation", "--root", SourceRoot]));
        Assert.Equal(0, Program.Main(["validate", "agent-surfaces", "--root", SourceRoot]));
        Assert.Equal(2, Program.Main(["validate", "agent-entry", "--root", SourceRoot]));
        Assert.Equal(0, Program.Main(["validate", "agent-entry", "--stage", "speckit.engloop.20-incident", "--root", SourceRoot]));
    }

    [Fact]
    public void ValidateAgentSurfaces_rejectsStageEightOperationsHandoff()
    {
        CopyCommandSurface();
        CopyPromptSurface();
        var stage08 = Path.Combine(_root, "extensions", "engloopkit", "commands", "speckit.engloop.08-unittest.md");
        var text = File.ReadAllText(stage08);
        var marker = "handoffs:";
        var insertion = "handoffs:\n  - label: Forbidden\n    agent: speckit.engloop.20-incident\n    prompt: forbidden\n    send: false";
        File.WriteAllText(stage08, text.Replace(marker, insertion, StringComparison.Ordinal));
        Assert.Equal(1, ValidationCommands.ValidateAgentSurfaces(["--root", _root]));
    }

    private void CopyCommandSurface()
    {
        CopyDirectory(Path.Combine(SourceRoot, "extensions"), Path.Combine(_root, "extensions"), overwrite: true);
    }

    private void CopyPromptSurface(bool overwrite = false)
    {
        CopyDirectory(Path.Combine(SourceRoot, ".github", "prompts"), Path.Combine(_root, ".github", "prompts"), overwrite);
    }

    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite);
        }
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
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
