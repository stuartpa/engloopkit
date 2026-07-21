using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>
/// Tests of the document-numbering discipline (the executable form of docs/standards.md):
/// fixed prefixes, zero-padded ids, increment-before-create, and monotonic never-reused
/// numbering.
/// </summary>
public sealed class NumberingRegistryTests
{
    [Theory]
    [InlineData("SPEC", 1, "SPEC001")]
    [InlineData("ARCH", 12, "ARCH012")]
    [InlineData("PM", 7, "PM007")]
    [InlineData("REFACT", 1000, "REFACT1000")]
    [InlineData("POM", 1, "POM0001")]
    public void Format_usesPrefixWidth(string prefix, int n, string expected)
    {
        Assert.Equal(expected, NumberingRegistry.Format(prefix, n));
    }

    [Fact]
    public void Format_unknownPrefix_throws()
    {
        Assert.Throws<ArgumentException>(() => NumberingRegistry.Format("XYZ", 1));
    }

    [Fact]
    public void Format_numberBelowOne_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberingRegistry.Format("SPEC", 0));
    }

    [Fact]
    public void KnownPrefixes_matchCurrentStandards()
    {
        string[] expected = ["SPEC", "SCAF", "ARCH", "MODEL", "CORD", "COV", "IN", "PM", "REFACT", "POM", "MIT", "LEARN", "RPI"];
        foreach (var p in expected)
        {
            Assert.True(NumberingRegistry.IsKnownPrefix(p), p);
        }

        Assert.Equal(expected.Length, NumberingRegistry.Prefixes.Count);
        Assert.False(NumberingRegistry.IsKnownPrefix("XYZ"));
    }

    [Fact]
    public void Next_incrementsFromZero()
    {
        var reg = new NumberingRegistry();
        Assert.Equal(0, reg.LastUsed("SPEC"));
        Assert.Equal(1, reg.Next("SPEC"));
        Assert.Equal(2, reg.Next("SPEC"));
        Assert.Equal(2, reg.LastUsed("SPEC"));
    }

    [Fact]
    public void NextId_formatsTheReservedNumber()
    {
        var reg = new NumberingRegistry();
        Assert.Equal("MODEL001", reg.NextId("MODEL"));
        Assert.Equal("MODEL002", reg.NextId("MODEL"));
        Assert.Equal("POM0001", reg.NextId("POM"));
    }

    [Fact]
    public void CountersAreIndependentPerPrefix()
    {
        var reg = new NumberingRegistry();
        reg.Next("SPEC");
        Assert.Equal(0, reg.LastUsed("ARCH"));
        Assert.Equal(1, reg.Next("ARCH"));
    }

    [Fact]
    public void Record_advancesAndRejectsReuse()
    {
        var reg = new NumberingRegistry();
        reg.Record("IN", 5);
        Assert.Equal(5, reg.LastUsed("IN"));

        // Reuse or precede is illegal (numbers are monotonic and never reused).
        Assert.Throws<InvalidOperationException>(() => reg.Record("IN", 5));
        Assert.Throws<InvalidOperationException>(() => reg.Record("IN", 3));

        reg.Record("IN", 6);
        Assert.Equal(6, reg.LastUsed("IN"));
    }

    [Fact]
    public void Record_belowOne_throws()
    {
        var reg = new NumberingRegistry();
        Assert.Throws<ArgumentOutOfRangeException>(() => reg.Record("IN", 0));
    }

    [Fact]
    public void LastUsed_unknownPrefix_throws()
    {
        var reg = new NumberingRegistry();
        Assert.Throws<ArgumentException>(() => reg.LastUsed("XYZ"));
    }
}
