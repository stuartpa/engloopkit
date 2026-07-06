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
    [InlineData("SEED", 1, "SEED001")]
    [InlineData("ARC", 12, "ARC012")]
    [InlineData("PM", 7, "PM007")]
    [InlineData("REF", 1000, "REF1000")]
    public void Format_zeroPadsToThreeDigits(string prefix, int n, string expected)
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
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberingRegistry.Format("SEED", 0));
    }

    [Fact]
    public void KnownPrefixes_areTheThirteenStandardOnes()
    {
        string[] expected = ["SEED", "SP", "BRG", "ARC", "MDL", "CRD", "COV", "IN", "PM", "REF", "MIT", "LRN", "RPI"];
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
        Assert.Equal(0, reg.LastUsed("SEED"));
        Assert.Equal(1, reg.Next("SEED"));
        Assert.Equal(2, reg.Next("SEED"));
        Assert.Equal(2, reg.LastUsed("SEED"));
    }

    [Fact]
    public void NextId_formatsTheReservedNumber()
    {
        var reg = new NumberingRegistry();
        Assert.Equal("MDL001", reg.NextId("MDL"));
        Assert.Equal("MDL002", reg.NextId("MDL"));
    }

    [Fact]
    public void CountersAreIndependentPerPrefix()
    {
        var reg = new NumberingRegistry();
        reg.Next("SEED");
        Assert.Equal(0, reg.LastUsed("ARC"));
        Assert.Equal(1, reg.Next("ARC"));
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
