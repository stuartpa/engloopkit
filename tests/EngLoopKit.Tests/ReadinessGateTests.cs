using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class ReadinessGateTests
{
    [Fact]
    public void Evaluate_requiresAtLeastOneModule()
    {
        var result = ReadinessGate.Evaluate([]);
        Assert.False(result.Passed);
        Assert.Contains("missing-module-inventory", result.Failures);
    }

    [Fact]
    public void Evaluate_failsIfAnyModuleBelow95()
    {
        var rows = new[]
        {
            new ReadinessRow("core", 96, 96, true, true),
            new ReadinessRow("tool", 94.9, 95, true, true),
        };

        var result = ReadinessGate.Evaluate(rows);
        Assert.False(result.Passed);
        Assert.Contains(result.Failures, failure => failure.StartsWith("line-coverage-below-threshold:tool", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_passesWhenAllRowsMeetThreshold()
    {
        var rows = new[]
        {
            new ReadinessRow("core", 95, 95, true, true),
            new ReadinessRow("tool", 98.4, 97.2, true, true),
        };

        var result = ReadinessGate.Evaluate(rows);
        Assert.True(result.Passed);
        Assert.Empty(result.Failures);
    }
}
