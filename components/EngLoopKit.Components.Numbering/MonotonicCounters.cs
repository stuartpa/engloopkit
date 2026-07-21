namespace EngLoopKit.Components.Numbering;

/// <summary>
/// A domain-agnostic set of monotonic, never-reused counters keyed by string. It knows
/// nothing about any particular numbering scheme — it wraps a dictionary to solve the
/// generic "increment-before-create, never reuse, zero-pad" problem. A vertical supplies
/// the keys and any policy (which keys are valid, how they map to a scheme).
///
/// This is a <b>component</b>: reusable machinery over the base class library, not code
/// in the space of any repository's vertical.
/// </summary>
public sealed class MonotonicCounters
{
    private readonly Dictionary<string, int> _last = new(StringComparer.Ordinal);

    /// <summary>The last value used for <paramref name="key"/> (0 if none).</summary>
    public int Peek(string key) => _last.TryGetValue(key, out var n) ? n : 0;

    /// <summary>Reserve and return the next value for <paramref name="key"/> (increment-before-create).</summary>
    public int Next(string key)
    {
        var next = Peek(key) + 1;
        _last[key] = next;
        return next;
    }

    /// <summary>
    /// Record an externally-produced value, enforcing monotonic, never-reused counting: a
    /// value at or below the last used one is rejected.
    /// </summary>
    public void Record(string key, int n)
    {
        if (n < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(n), "counter values start at 1");
        }

        if (n <= Peek(key))
        {
            throw new InvalidOperationException($"{key} value {n} reuses or precedes the last used value");
        }

        _last[key] = n;
    }

    /// <summary>Format a key + value as a zero-padded token using an explicit width.</summary>
    public static string Pad(string key, int n, int width = 3)
    {
        if (n < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(n), "counter values start at 1");
        }

        return key + n.ToString("D" + width);
    }
}
