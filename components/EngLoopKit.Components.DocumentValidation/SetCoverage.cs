namespace EngLoopKit.Components.DocumentValidation;

public sealed record SetCoverageResult(
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Extra,
    bool Passed);

public static class SetCoverage
{
    public static SetCoverageResult Compare(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
        var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);

        var missing = expectedSet.Except(actualSet).OrderBy(v => v, StringComparer.Ordinal).ToArray();
        var extra = actualSet.Except(expectedSet).OrderBy(v => v, StringComparer.Ordinal).ToArray();

        return new SetCoverageResult(missing, extra, missing.Length == 0 && extra.Length == 0);
    }
}
