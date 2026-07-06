using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>
/// Deep behavioural tests of the engineering-loop state machine — the guarded sequencing
/// and rejection of illegal transitions that the SEK-generated conformance suite (which
/// only replays legal sequences) does not exercise.
/// </summary>
public sealed class EngineeringLoopTests
{
    [Fact]
    public void Begin_startsAtSeed()
    {
        var loop = new EngineeringLoop();
        Assert.False(loop.Started);
        loop.Begin();
        Assert.True(loop.Started);
        Assert.Equal(Stage.Seed, loop.Current);
    }

    [Fact]
    public void Begin_twice_throws()
    {
        var loop = new EngineeringLoop();
        loop.Begin();
        Assert.Throws<InvalidOperationException>(() => loop.Begin());
    }

    [Fact]
    public void Advance_beforeBegin_throws()
    {
        var loop = new EngineeringLoop();
        Assert.Throws<InvalidOperationException>(() => loop.Advance(Stage.Bridge));
    }

    [Fact]
    public void AllStages_hasEleven()
    {
        Assert.Equal(11, EngineeringLoop.AllStages.Count);
    }

    // The canonical happy path through a full delivery + verification cycle.
    [Fact]
    public void Advance_walksTheDeliveryPath()
    {
        var loop = new EngineeringLoop();
        loop.Begin();
        loop.Advance(Stage.Bridge);
        loop.Advance(Stage.Architect);
        loop.Advance(Stage.RefactorToFinal);
        loop.Advance(Stage.Model);
        loop.Advance(Stage.Explore);
        loop.Advance(Stage.Coverage);
        Assert.Equal(Stage.Coverage, loop.Current);
    }

    // The Verification loop (explore <-> coverage) and Operations loop are reachable.
    [Fact]
    public void Coverage_canLoopAndOperateAndEvolve()
    {
        Assert.True(EngineeringLoop.IsLegalTransition(Stage.Coverage, Stage.Explore));
        Assert.True(EngineeringLoop.IsLegalTransition(Stage.Coverage, Stage.Incident));
        Assert.True(EngineeringLoop.IsLegalTransition(Stage.Coverage, Stage.RefactorScan));
        Assert.True(EngineeringLoop.IsLegalTransition(Stage.Incident, Stage.Incident));
        Assert.True(EngineeringLoop.IsLegalTransition(Stage.Repair, Stage.RefactorToFinal));
        Assert.True(EngineeringLoop.IsLegalTransition(Stage.RefactorScan, Stage.Seed));
    }

    [Theory]
    [InlineData(Stage.Seed, Stage.Architect)]     // must go through Bridge
    [InlineData(Stage.Seed, Stage.Seed)]          // no self-loop at Seed
    [InlineData(Stage.Bridge, Stage.Model)]       // must go through Architect + RefactorToFinal
    [InlineData(Stage.Coverage, Stage.Postmortem)] // Operations opens with an Incident
    [InlineData(Stage.Postmortem, Stage.RefactorToFinal)] // must go through Repair
    public void Advance_illegalTransition_throws(Stage from, Stage to)
    {
        Assert.False(EngineeringLoop.IsLegalTransition(from, to));

        var loop = DriveTo(from);
        Assert.Throws<InvalidOperationException>(() => loop.Advance(to));
    }

    [Fact]
    public void LegalNext_matchesIsLegalTransition()
    {
        foreach (var from in EngineeringLoop.AllStages)
        {
            foreach (var to in EngineeringLoop.LegalNext(from))
            {
                Assert.True(EngineeringLoop.IsLegalTransition(from, to));
            }
        }
    }

    // Drive a fresh loop to an arbitrary stage over legal transitions (BFS shortest path).
    private static EngineeringLoop DriveTo(Stage target)
    {
        var loop = new EngineeringLoop();
        loop.Begin();
        if (target == Stage.Seed)
        {
            return loop;
        }

        var path = ShortestPath(Stage.Seed, target);
        foreach (var step in path)
        {
            loop.Advance(step);
        }

        return loop;
    }

    private static List<Stage> ShortestPath(Stage from, Stage to)
    {
        var queue = new Queue<Stage>();
        var prev = new Dictionary<Stage, Stage>();
        var seen = new HashSet<Stage> { from };
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur.Equals(to))
            {
                break;
            }

            foreach (var next in EngineeringLoop.LegalNext(cur))
            {
                if (seen.Add(next))
                {
                    prev[next] = cur;
                    queue.Enqueue(next);
                }
            }
        }

        var steps = new List<Stage>();
        var node = to;
        while (!node.Equals(from))
        {
            steps.Add(node);
            node = prev[node];
        }

        steps.Reverse();
        return steps;
    }
}
