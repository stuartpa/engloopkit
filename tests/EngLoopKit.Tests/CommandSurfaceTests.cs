using Xunit;

namespace EngLoopKit.Tests;

public sealed class CommandSurfaceTests
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string CommandsDir = Path.Combine(Root, "extensions", "engloopkit", "commands");

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
    public void HandoffGraph_hasExact25Edges_withTerminalAgentsAndNoForbiddenStage08Targets()
    {
        var edges = 0;

        foreach (var file in Directory.GetFiles(CommandsDir, "speckit.engloop.*.md", SearchOption.TopDirectoryOnly))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var lines = File.ReadAllLines(file);

            var inHandoffs = false;
            var localEdges = new List<string>();
            foreach (var line in lines)
            {
                if (line.Trim() == "handoffs:")
                {
                    inHandoffs = true;
                    continue;
                }

                if (inHandoffs && line.Trim() == "---")
                {
                    break;
                }

                if (inHandoffs && line.TrimStart().StartsWith("agent:"))
                {
                    var target = line.Split(':', 2)[1].Trim();
                    localEdges.Add(target);
                    edges++;
                }

                if (inHandoffs && line.TrimStart().StartsWith("send:"))
                {
                    Assert.Equal("send: false", line.Trim());
                }

                if (inHandoffs)
                {
                    Assert.DoesNotContain("model:", line.Trim());
                }
            }

            if (id is "speckit.engloop.31-learnings-pyramid" or "speckit.engloop.40-pomodoro-create" or "speckit.engloop.51-overlay-remove")
            {
                Assert.Empty(localEdges);
            }

            if (id == "speckit.engloop.08-unittest")
            {
                Assert.DoesNotContain("speckit.engloop.20-incident", localEdges);
                Assert.DoesNotContain("speckit.engloop.30-refactor-scan", localEdges);
                Assert.DoesNotContain("speckit.engloop.31-learnings-pyramid", localEdges);
            }
        }

        Assert.Equal(25, edges);
    }

    [Fact]
    public void NorthStar_authoringSurface_usesTimelessDirectionAndStagePrerequisites()
    {
        var command = File.ReadAllText(Path.Combine(CommandsDir, "speckit.engloop.01-northstar.md"));
        var template = File.ReadAllText(Path.Combine(Root, "extensions", "engloopkit", "templates", "NORTHSTAR-template.md"));
        var prompt = File.ReadAllText(Path.Combine(Root, ".github", "prompts", "speckit.engloop.01-northstar.prompt.md"));

        Assert.Contains("timeless", command, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## Staged capability", command, StringComparison.Ordinal);
        Assert.Contains("Stage N", command, StringComparison.Ordinal);
        Assert.Contains("Phase", command, StringComparison.Ordinal);
        Assert.Contains("separate planning artifacts", command, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("# <Repository> North Star", template, StringComparison.Ordinal);
        Assert.Contains("## Staged capability sequence", template, StringComparison.Ordinal);
        Assert.Contains("### Stage 1", template, StringComparison.Ordinal);
        Assert.Contains("Do not use `Phase`", template, StringComparison.Ordinal);
        Assert.Contains("schedules, tasks, milestones", template, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("timeless North Star", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stage N", prompt, StringComparison.Ordinal);
    }
}
