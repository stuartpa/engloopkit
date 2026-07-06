namespace EngLoopKit.Core;

/// <summary>
/// The document-numbering discipline, as executable code. This is the enforceable form of
/// the rules in <c>docs/standards.md</c>: fixed prefixes, monotonic and never-reused
/// numbers, zero-padded three-digit ids, and increment-before-create.
/// </summary>
public sealed class NumberingRegistry
{
    // The canonical EngLoopKit prefixes (docs/standards.md). BRG was added in v1.1.0.
    private static readonly IReadOnlySet<string> KnownPrefixes = new HashSet<string>(StringComparer.Ordinal)
    {
        "SEED", "SP", "BRG", "ARC", "MDL", "CRD", "COV", "IN", "PM", "REF", "MIT", "LRN", "RPI",
    };

    private readonly Dictionary<string, int> _last = new(StringComparer.Ordinal);

    /// <summary>True if <paramref name="prefix"/> is a recognized EngLoopKit prefix.</summary>
    public static bool IsKnownPrefix(string prefix) => KnownPrefixes.Contains(prefix);

    /// <summary>Every recognized prefix.</summary>
    public static IReadOnlyCollection<string> Prefixes => (IReadOnlyCollection<string>)KnownPrefixes;

    /// <summary>Format a prefix + number as a zero-padded id, e.g. <c>SEED001</c>.</summary>
    public static string Format(string prefix, int n)
    {
        if (!IsKnownPrefix(prefix))
        {
            throw new ArgumentException($"unknown prefix '{prefix}'", nameof(prefix));
        }

        if (n < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(n), "numbers start at 1");
        }

        return $"{prefix}{n:D3}";
    }

    /// <summary>The last number used for <paramref name="prefix"/> (0 if none).</summary>
    public int LastUsed(string prefix)
    {
        if (!IsKnownPrefix(prefix))
        {
            throw new ArgumentException($"unknown prefix '{prefix}'", nameof(prefix));
        }

        return _last.TryGetValue(prefix, out var n) ? n : 0;
    }

    /// <summary>
    /// Increment-before-create: reserve and return the next number for <paramref name="prefix"/>.
    /// </summary>
    public int Next(string prefix)
    {
        var next = LastUsed(prefix) + 1;
        _last[prefix] = next;
        return next;
    }

    /// <summary>Reserve and return the next id (e.g. <c>SEED001</c>).</summary>
    public string NextId(string prefix) => Format(prefix, Next(prefix));

    /// <summary>
    /// Record an externally-created number (e.g. read from the registry file). Enforces
    /// monotonic, never-reused numbering: a number at or below the last used one is illegal.
    /// </summary>
    public void Record(string prefix, int n)
    {
        if (n < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(n), "numbers start at 1");
        }

        if (n <= LastUsed(prefix))
        {
            throw new InvalidOperationException(
                $"{Format(prefix, n)} reuses or precedes the last used number for {prefix}");
        }

        _last[prefix] = n;
    }
}
