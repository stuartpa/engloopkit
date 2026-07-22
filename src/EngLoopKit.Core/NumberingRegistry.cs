using EngLoopKit.Components.Numbering;

namespace EngLoopKit.Core;

/// <summary>
/// The document-numbering discipline, as executable code. This is the enforceable form of
/// the rules in <c>docs/standards.md</c>: fixed prefixes, monotonic and never-reused
/// numbers, prefix-specific zero-padded ids, and increment-before-create.
///
/// The vertical here supplies only the domain knowledge — the set of valid EngLoopKit
/// prefixes — and composes the generic counting machinery from the
/// <c>EngLoopKit.Components.Numbering</c> component.
/// </summary>
public sealed class NumberingRegistry
{
    private static readonly IReadOnlyDictionary<string, int> PrefixWidths = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["SPEC"] = 3, ["SCAF"] = 3, ["ARCH"] = 3, ["MODEL"] = 3,
        ["CORD"] = 3, ["COV"] = 3, ["IN"] = 3, ["PM"] = 3,
        ["REFACT"] = 3, ["DBG"] = 3, ["POM"] = 4,
        ["MIT"] = 3, ["LEARN"] = 3, ["RPI"] = 3,
    };

    // Generic monotonic-counter machinery (the component); the vertical supplies the keys.
    private readonly MonotonicCounters _counters = new();

    /// <summary>True if <paramref name="prefix"/> is a recognized EngLoopKit prefix.</summary>
    public static bool IsKnownPrefix(string prefix) => PrefixWidths.ContainsKey(prefix);

    /// <summary>Every recognized prefix.</summary>
    public static IReadOnlyCollection<string> Prefixes => PrefixWidths.Keys.ToArray();

    /// <summary>Format a prefix + number using its policy width, e.g. <c>POM0001</c>.</summary>
    public static string Format(string prefix, int n)
    {
        if (!IsKnownPrefix(prefix))
        {
            throw new ArgumentException($"unknown prefix '{prefix}'", nameof(prefix));
        }

        // The zero-padding is generic; the component owns it.
        return MonotonicCounters.Pad(prefix, n, PrefixWidths[prefix]);
    }

    /// <summary>The last number used for <paramref name="prefix"/> (0 if none).</summary>
    public int LastUsed(string prefix)
    {
        RequireKnown(prefix);
        return _counters.Peek(prefix);
    }

    /// <summary>
    /// Increment-before-create: reserve and return the next number for <paramref name="prefix"/>.
    /// </summary>
    public int Next(string prefix)
    {
        RequireKnown(prefix);
        return _counters.Next(prefix);
    }

    /// <summary>Reserve and return the next id (e.g. <c>POM0001</c>).</summary>
    public string NextId(string prefix) => Format(prefix, Next(prefix));

    /// <summary>
    /// Record an externally-created number (e.g. read from the registry file). Enforces
    /// monotonic, never-reused numbering: a number at or below the last used one is illegal.
    /// </summary>
    public void Record(string prefix, int n)
    {
        RequireKnown(prefix);
        _counters.Record(prefix, n);
    }

    private static void RequireKnown(string prefix)
    {
        if (!IsKnownPrefix(prefix))
        {
            throw new ArgumentException($"unknown prefix '{prefix}'", nameof(prefix));
        }
    }
}
