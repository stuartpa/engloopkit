using EngLoopKit.Components.StateMachine;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class StateMachineComponentTests
{
    [Fact]
    public void Graph_reportsLegalAndIllegalEdgesAndMissingNodes()
    {
        var graph = new TransitionGraph<string>(new Dictionary<string, string[]>
        {
            ["a"] = ["b", "c"],
            ["b"] = [],
        });

        Assert.True(graph.IsLegal("a", "b"));
        Assert.True(graph.IsLegal("a", "c"));
        Assert.False(graph.IsLegal("a", "a"));
        Assert.False(graph.IsLegal("missing", "a"));
        Assert.Equal(["b", "c"], graph.Next("a"));
        Assert.Empty(graph.Next("missing"));
    }

    [Fact]
    public void GuardedMachine_enforcesStartAndGraphEdges()
    {
        var graph = new TransitionGraph<int>(new Dictionary<int, int[]>
        {
            [1] = [2],
            [2] = [3],
            [3] = [],
        });
        var machine = new GuardedMachine<int>(graph, 1);

        Assert.False(machine.Started);
        Assert.Equal(1, machine.Current);
        Assert.Throws<InvalidOperationException>(() => machine.Advance(2));

        machine.Begin();
        Assert.True(machine.Started);
        Assert.Equal(1, machine.Current);
        Assert.Throws<InvalidOperationException>(() => machine.Begin());
        Assert.Throws<InvalidOperationException>(() => machine.Advance(3));
        Assert.Equal(1, machine.Current);

        machine.Advance(2);
        machine.Advance(3);
        Assert.Equal(3, machine.Current);
        Assert.Throws<InvalidOperationException>(() => machine.Advance(1));
    }
}
