using System.Text.RegularExpressions;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class AgentSurfaceValidationTests
{
    [Fact]
    public void AgentEntry_rejectsUnknownStageIdentity()
    {
        var result = EngLoopKit.Tool.ValidationCommands.ValidateAgentEntry(
            ["--stage", "invalid-stage", "--root", Root]);

        Assert.Equal(2, result);
    }

    private static readonly string Root = FindRepoRoot();
    private static readonly string CommandsDir = Path.Combine(Root, "extensions", "engloopkit", "commands");
    private static readonly string PromptsDir = Path.Combine(Root, ".github", "prompts");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "bundle.yml")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "could not locate repo root");
        return dir!.FullName;
    }

    [Fact]
    public void PromptFiles_selectExactAgents_andForbidTools()
    {
        var prompts = Directory.GetFiles(PromptsDir, "speckit.engloop.*.prompt.md", SearchOption.TopDirectoryOnly);
        Assert.Equal(17, prompts.Length);

        foreach (var prompt in prompts)
        {
            var content = File.ReadAllText(prompt);
            var id = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(prompt));
            Assert.Contains($"agent: {id}", content);
            Assert.DoesNotContain("tools:", content);
        }
    }

    [Fact]
    public void CommandFrontmatter_enforcesRequiredAndForbiddenFields()
    {
        foreach (var file in Directory.GetFiles(CommandsDir, "speckit.engloop.*.md", SearchOption.TopDirectoryOnly))
        {
            var text = File.ReadAllText(file);
            Assert.Contains("name:", text);
            Assert.Contains("description:", text);
            Assert.Contains("argument-hint:", text);
            Assert.Contains("target: vscode", text);
            Assert.Contains("user-invocable: true", text);
            Assert.Contains("disable-model-invocation: true", text);
            Assert.Contains("tools:", text);
            Assert.Contains("agents:", text);
            Assert.Contains("hooks:", text);
            Assert.DoesNotContain("infer:", text);
            Assert.DoesNotContain("model:", text);
        }
    }
}
