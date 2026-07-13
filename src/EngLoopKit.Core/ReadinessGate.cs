namespace EngLoopKit.Core;

public sealed record ReadinessRow(string ModuleId, double LineCoverage, double BranchCoverage, bool ArchitecturePass, bool RegressionPass);

public sealed record ReadinessResult(bool Passed, IReadOnlyList<string> Failures);

public static class ReadinessGate
{
    public static ReadinessResult Evaluate(IEnumerable<ReadinessRow> rows)
    {
        var failures = new List<string>();
        var materialized = rows.ToArray();
        if (materialized.Length == 0)
        {
            failures.Add("missing-module-inventory");
            return new ReadinessResult(false, failures);
        }

        foreach (var row in materialized)
        {
            if (!row.ArchitecturePass)
            {
                failures.Add($"architecture-fail:{row.ModuleId}");
            }

            if (!row.RegressionPass)
            {
                failures.Add($"regression-fail:{row.ModuleId}");
            }

            if (row.LineCoverage < 95.0)
            {
                failures.Add($"line-coverage-below-threshold:{row.ModuleId}");
            }

            if (row.BranchCoverage < 95.0)
            {
                failures.Add($"branch-coverage-below-threshold:{row.ModuleId}");
            }
        }

        return new ReadinessResult(failures.Count == 0, failures);
    }
}
