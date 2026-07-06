using EngLoopKit.Components.Numbering;

namespace EngLoopKit.Core;

/// <summary>
/// The document-numbering discipline, as executable code. This is the enforceable form of
/// the rules in <c>docs/standards.md</c>: fixed prefixes, monotonic and never-reused
/// numbers, zero-padded three-digit ids, and increment-before-create.
///
/// The vertical here supplies only the domain knowledge — the set of valid EngLoopKit
/// prefixes — and composes the generic counting machinery from the
/// <c>EngLoopKit.Components.Numbering</c> component.
/// </summary>
public sealed class NumberingRegistry
{
    // The canonical EngLoopKit prefixes (docs/standards.md). BRG was added in v1.1.0.
    private static readonly IReadOnlySet<string> KnownPrefixes = new HashSet<string>(StringComparer.Ordinal)
    {
        "SEED", "SP", "BRG", "ARC", "MDL", "CRD", "COV", "IN", "PM", "REF", "MIT", "LRN", "RPI",
    };

    // Generic monotonic-counter machinery (the component); the vertical supplies the keys.
    private readonly MonotonicCounters _counters = new();

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

        // The zero-padding is generic; the component owns it.
        return MonotonicCounters.Pad(prefix, n);
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

    /// <summary>Reserve and return the next id (e.g. <c>SEED001</c>).</summary>
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
