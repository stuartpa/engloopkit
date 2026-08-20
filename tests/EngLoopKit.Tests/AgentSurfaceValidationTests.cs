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
        Assert.Equal(26, prompts.Length);

        foreach (var prompt in prompts)
        {
            var content = File.ReadAllText(prompt);
            var id = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(prompt));
            Assert.Contains($"agent: {id}", content);
            Assert.DoesNotContain("tools:", content);
        }
    }

    [Fact]
    public void TokenEfficiencyPromptFiles_exposeRequiredArguments()
    {
        var analysis = File.ReadAllText(Path.Combine(PromptsDir, "speckit.engloop.30-token-efficiency-analyze.prompt.md"));
        Assert.Contains("description:", analysis, StringComparison.Ordinal);
        Assert.Contains("argument-hint:", analysis, StringComparison.Ordinal);
        Assert.Contains("--session", analysis, StringComparison.Ordinal);

        var implementation = File.ReadAllText(Path.Combine(PromptsDir, "speckit.engloop.31-token-efficiency-implement.prompt.md"));
        Assert.Contains("description:", implementation, StringComparison.Ordinal);
        Assert.Contains("argument-hint:", implementation, StringComparison.Ordinal);
        Assert.Contains("--analysis", implementation, StringComparison.Ordinal);
        Assert.Contains("--approve", implementation, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationsPromptFiles_exposeLearningBoundRepairArguments()
    {
        var postmortem = File.ReadAllText(Path.Combine(PromptsDir, "speckit.engloop.21-postmortem.prompt.md"));
        Assert.Contains("description:", postmortem, StringComparison.Ordinal);
        Assert.Contains("argument-hint:", postmortem, StringComparison.Ordinal);
        Assert.Contains("--postmortem", postmortem, StringComparison.Ordinal);

        var repair = File.ReadAllText(Path.Combine(PromptsDir, "speckit.engloop.22-repair.prompt.md"));
        Assert.Contains("description:", repair, StringComparison.Ordinal);
        Assert.Contains("argument-hint:", repair, StringComparison.Ordinal);
        Assert.Contains("--postmortem", repair, StringComparison.Ordinal);
        Assert.Contains("--rpi", repair, StringComparison.Ordinal);
        Assert.Contains("--rules", repair, StringComparison.Ordinal);
        Assert.Contains("verification requirement", repair, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefactorPrompt_exposesExplicitProfilesAndSafeDefault()
    {
        var refactor = File.ReadAllText(Path.Combine(PromptsDir, "speckit.engloop.40-refactor-plan.prompt.md"));
        Assert.Contains("description:", refactor, StringComparison.Ordinal);
        Assert.Contains("argument-hint:", refactor, StringComparison.Ordinal);
        Assert.Contains("--scope", refactor, StringComparison.Ordinal);
        Assert.Contains("point|bounded|deep", refactor, StringComparison.Ordinal);
        Assert.Contains("omitted means `point`", refactor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never infer model", refactor, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tools:", refactor, StringComparison.Ordinal);

        var implementation = File.ReadAllText(Path.Combine(PromptsDir, "speckit.engloop.04-refactor.prompt.md"));
        Assert.Contains("accepted SPEC, REFACT, or repair slice", implementation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without re-planning", implementation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stage 40 Refactor Plan", implementation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HappyMinutePrompt_isGratitudeFirstAndLightweight()
    {
        var happy = File.ReadAllText(Path.Combine(PromptsDir, "speckit.engloop.23-happy-minute.prompt.md"));
        Assert.Contains("description:", happy, StringComparison.Ordinal);
        Assert.Contains("argument-hint:", happy, StringComparison.Ordinal);
        Assert.Contains("what worked perfectly", happy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("description is sufficient", happy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT-PROVIDED", happy, StringComparison.Ordinal);
        Assert.DoesNotContain("tools:", happy, StringComparison.Ordinal);
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
