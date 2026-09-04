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
    public void HandoffGraph_hasExact31Edges_withHappyMinuteAndDeadCode()
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

            if (id is "speckit.engloop.09-debugger-walk-thru" or "speckit.engloop.31-token-efficiency-implement" or "speckit.engloop.41-deadcode" or "speckit.engloop.42-learnings-pyramid" or "speckit.engloop.50-handoff-create" or "speckit.engloop.61-overlay-remove" or "speckit.engloop.70-six-pager-create" or "speckit.engloop.71-powerpnt-create" or "speckit.engloop.72-academic-paper-create" or "speckit.engloop.80-upgrade-elk")
            {
                Assert.Empty(localEdges);
            }

            if (id == "speckit.engloop.08-unittest")
            {
                Assert.Contains("speckit.engloop.09-debugger-walk-thru", localEdges);
                Assert.DoesNotContain("speckit.engloop.20-incident", localEdges);
                Assert.DoesNotContain("speckit.engloop.40-refactor-plan", localEdges);
                Assert.DoesNotContain("speckit.engloop.41-deadcode", localEdges);
                Assert.DoesNotContain("speckit.engloop.42-learnings-pyramid", localEdges);
            }

            if (id == "speckit.engloop.02-scaffold")
            {
                Assert.Contains("speckit.engloop.09-debugger-walk-thru", localEdges);
            }

            if (id == "speckit.engloop.30-token-efficiency-analyze")
            {
                Assert.Equal(["speckit.engloop.31-token-efficiency-implement"], localEdges);
            }

            if (id == "speckit.engloop.40-refactor-plan")
            {
                Assert.Equal("speckit.engloop.41-deadcode", localEdges[^1]);
            }

            if (id == "speckit.engloop.23-happy-minute")
            {
                Assert.Equal(["speckit.engloop.42-learnings-pyramid"], localEdges);
            }

        }

        Assert.Equal(31, edges);
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
