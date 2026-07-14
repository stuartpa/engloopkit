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
    public void HandoffGraph_hasExact24Edges_withStage31None_andNoForbiddenStage08Targets()
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

            if (id == "speckit.engloop.31-learnings-pyramid")
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

        Assert.Equal(24, edges);
    }
}
