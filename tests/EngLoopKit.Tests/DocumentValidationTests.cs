using EngLoopKit.Components.DocumentValidation;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class DocumentValidationTests
{
    [Fact]
    public void ExtractLinks_findsMarkdownTargets()
    {
        const string markdown = "See [A](./a.md) and [B](../b.md#frag).";
        var links = MarkdownLinks.ExtractLinks(markdown);
        Assert.Equal(new[] { "./a.md", "../b.md#frag" }, links);
    }

    [Fact]
    public void TextBudget_countsWordsAndNonblankLines()
    {
        const string markdown = "# Title\n\nAlpha beta.\nGamma.";
        var (words, lines) = TextBudget.Count(markdown);
        Assert.Equal(4, words);
        Assert.Equal(3, lines);
        Assert.True(TextBudget.IsWithinBudget(markdown));
    }

    [Fact]
    public void SetCoverage_reportsMissingAndExtra()
    {
        var result = SetCoverage.Compare(new[] { "a", "b" }, new[] { "b", "c" });
        Assert.False(result.Passed);
        Assert.Equal(new[] { "a" }, result.Missing);
        Assert.Equal(new[] { "c" }, result.Extra);
    }

    [Fact]
    public void SemanticProjection_parsesFrontmatter()
    {
        const string markdown = "---\nname: sample\ntools: [read, search]\n---\n\nBody";
        var parsed = SemanticProjection.ParseFrontmatter(markdown);
        Assert.NotNull(parsed);

        var canonical = SemanticProjection.Canonicalize(parsed);
        Assert.NotNull(canonical);
    }
}
