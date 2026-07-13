using EngLoopKit.Components.DocumentValidation;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class DocumentValidationEdgeTests
{
    [Theory]
    [InlineData("")]
    [InlineData("plain markdown")]
    [InlineData("---\nname: incomplete")]
    public void FrontmatterParser_returnsNullForMissingOrMalformedFrontmatter(string text)
    {
        Assert.Null(SemanticProjection.ParseFrontmatter(text));
    }

    [Fact]
    public void FrontmatterParser_rejectsMultilineTextWithoutOpeningDelimiter()
    {
        Assert.Null(SemanticProjection.ParseFrontmatter("one\ntwo\nthree\n"));
    }

    [Fact]
    public void FrontmatterParser_returnsNullForInvalidYaml()
    {
        const string markdown = "---\nname: [unterminated\n---\n";
        Assert.Null(SemanticProjection.ParseFrontmatter(markdown));
    }

    [Fact]
    public void Canonicalize_sortsMapsAndPreservesSequenceOrder()
    {
        const string markdown = "---\nz: 1\na:\n  nested: yes\nitems: [second, first]\n---\n";
        var parsed = SemanticProjection.ParseFrontmatter(markdown);
        var canonical = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(SemanticProjection.Canonicalize(parsed));

        Assert.Equal(["a", "items", "z"], canonical.Keys);
        Assert.Equal(["second", "first"], Assert.IsAssignableFrom<object?[]>(canonical["items"]));
    }

    [Fact]
    public void Canonicalize_handlesNullScalarStringAndNestedObjectSequences()
    {
        Assert.Null(SemanticProjection.Canonicalize(null));
        Assert.Equal(42, SemanticProjection.Canonicalize(42));
        Assert.Equal("text", SemanticProjection.Canonicalize("text"));

        object value = new object[]
        {
            new Dictionary<object, object> { ["b"] = 2, ["a"] = null! },
            "tail",
        };
        var canonical = Assert.IsAssignableFrom<object?[]>(SemanticProjection.Canonicalize(value));
        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(canonical[0]);
        Assert.Equal(["a", "b"], map.Keys);
        Assert.Null(map["a"]);
        Assert.Equal("tail", canonical[1]);
    }

    [Fact]
    public void Canonicalize_handlesAParsedMapWithANullKeyDeterministically()
    {
        object map = new NullableKeyDictionary();
        var canonical = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(SemanticProjection.Canonicalize(map));
        Assert.Equal("null-key", canonical[string.Empty]);
        Assert.Equal(2, canonical["z"]);
    }

    [Fact]
    public void MarkdownLinks_handlesEmptyAndTrimsTargets()
    {
        Assert.Empty(MarkdownLinks.ExtractLinks(string.Empty));
        Assert.Equal(["a.md", "https://example.test/x"], MarkdownLinks.ExtractLinks("[a]( a.md ) [b](https://example.test/x)"));
    }

    [Fact]
    public void TextBudget_normalizesNewlinesAndRejectsExceededBudgets()
    {
        var text = "alpha\r\nbeta\r\n\r\ngamma";
        Assert.Equal((3, 3), TextBudget.Count(text));
        Assert.False(TextBudget.IsWithinBudget(text, maxWords: 2));
        Assert.False(TextBudget.IsWithinBudget(text, maxNonblankLines: 2));
        Assert.True(TextBudget.IsWithinBudget(string.Empty));
    }

    [Fact]
    public void SetCoverage_passesForEquivalentSetsDespiteDuplicates()
    {
        var result = SetCoverage.Compare(["a", "a", "b"], ["b", "a", "a"]);
        Assert.True(result.Passed);
        Assert.Empty(result.Missing);
        Assert.Empty(result.Extra);
    }
}

/// <summary>Read-only test fixture for the otherwise unreachable null-key YAML-map edge.</summary>
internal sealed class NullableKeyDictionary : IDictionary<object, object>
{
    private readonly List<KeyValuePair<object, object>> _items =
    [
        new KeyValuePair<object, object>(null!, "null-key"),
        new KeyValuePair<object, object>("z", 2),
    ];

    public object this[object key] { get => _items.First(pair => Equals(pair.Key, key)).Value; set => throw new NotSupportedException(); }
    public ICollection<object> Keys => _items.Select(pair => pair.Key).ToArray();
    public ICollection<object> Values => _items.Select(pair => pair.Value).ToArray();
    public int Count => _items.Count;
    public bool IsReadOnly => true;
    public void Add(object key, object value) => throw new NotSupportedException();
    public void Add(KeyValuePair<object, object> item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Contains(KeyValuePair<object, object> item) => _items.Contains(item);
    public bool ContainsKey(object key) => _items.Any(pair => Equals(pair.Key, key));
    public void CopyTo(KeyValuePair<object, object>[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => _items.GetEnumerator();
    public bool Remove(object key) => throw new NotSupportedException();
    public bool Remove(KeyValuePair<object, object> item) => throw new NotSupportedException();
    public bool TryGetValue(object key, out object value)
    {
        var match = _items.FirstOrDefault(pair => Equals(pair.Key, key));
        value = match.Value;
        return _items.Any(pair => Equals(pair.Key, key));
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
