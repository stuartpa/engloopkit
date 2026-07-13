using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>The permanent real-boundary test selected by Stage 02 runway proof.</summary>
public sealed partial class RunwayBoundaryTests
{
    [Fact]
    public void RunwayBoundaryTest()
    {
        var loop = new Loop();
        loop.Northstar();
        loop.Scaffold();
        loop.Architect();
        loop.Refactor();
        loop.Model();
        loop.Explore();
        loop.Validate();
        loop.UnitTest();
        loop.UnitTest();

        Assert.True(loop.State.HasCurrentReadinessPass());
    }
}
