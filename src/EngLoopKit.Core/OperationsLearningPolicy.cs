using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EngLoopKit.Core;

public sealed record PyramidRuleDisposition(
    string RuleId,
    string CardId,
    IReadOnlyList<string> SourceIds,
    string Disposition,
    string IncidentEvidence,
    string PyramidAction);

public sealed record SekEscapeAnalysis(
    string Applicability,
    string ApplicabilityRationale,
    string Version,
    string VerificationClass,
    string EscapeClass,
    string ScenarioId,
    IReadOnlyList<string> ModelPaths,
    IReadOnlyList<string> CordPaths,
    string GeneratedSuitePath,
    string WhyEscaped,
    string RequiredRepair);

public sealed record RepairLearningContract(
    string RpiId,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> ExecutableGate,
    string ExecutableGateDigest,
    string GateProves,
    string SekApplicability,
    string SekScenarioId,
    string SekRepairRequirement);

public sealed record IncidentContextContract(
    string IncidentId,
    string RelativePath,
    string Sha256,
    string NorthstarSha256,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> SourceIds);

public sealed record PostmortemLearningContract(
    string PostmortemId,
    string RelativePath,
    string Sha256,
    string NorthstarSha256,
    string LearningsSha256,
    string PyramidDigest,
    IReadOnlyList<IncidentContextContract> Incidents,
    string DirectionAlignment,
    string PyramidDecision,
    string RetrievalImpact,
    SekEscapeAnalysis SekEscape,
    IReadOnlyList<PyramidRuleDisposition> RuleDispositions,
    IReadOnlyDictionary<string, RepairLearningContract> Repairs);

public sealed record OperationsLearningValidationResult(
    bool Passed,
    IReadOnlyList<string> Failures,
    PostmortemLearningContract? Contract = null);

public sealed record IncidentContextValidationResult(
    bool Passed,
    IReadOnlyList<string> Failures,
    IncidentContextContract? Contract = null);

public static class OperationsLearningPolicy
{
    private static readonly Regex PostmortemFileRegex = new(@"^PM(?<number>\d{3})(?:[-_].+)?\.md$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SourceIdRegex = new(@"^(?:PM\d{3}/LEARN\d{3}|HAPPY\d{3})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SourceIdFindRegex = new(@"PM\d{3}/LEARN\d{3}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RuleIdRegex = new(@"^RULE:(?<slug>[a-z0-9]+(?:-[a-z0-9]+)*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RpiRegex = new(@"^RPI\d{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static OperationsLearningValidationResult ValidatePostmortem(string repositoryRoot, string postmortemPath)
    {
        try
        {
            var root = NormalizeRoot(repositoryRoot);
            var (_, full) = ResolveGovernedPath(root, postmortemPath, ".engloop/postmortems/", true);
            var incidentIds = ParseSelectedIncidentTable(File.ReadAllText(full)).Keys.ToArray();
            return ValidatePostmortem(root, postmortemPath, incidentIds);
        }
        catch (Exception ex)
        {
            return new OperationsLearningValidationResult(false, [$"postmortem-path-invalid:{ex.Message}"]);
        }
    }

    public static OperationsLearningValidationResult ValidatePostmortem(
        string repositoryRoot,
        string postmortemPath,
        IReadOnlyCollection<string> selectedIncidentIds)
    {
        var failures = new List<string>();
        string root;
        string relative;
        string full;
        try
        {
            root = NormalizeRoot(repositoryRoot);
            (relative, full) = ResolveGovernedPath(root, postmortemPath, ".engloop/postmortems/", true);
        }
        catch (Exception ex)
        {
            return new OperationsLearningValidationResult(false, [$"postmortem-path-invalid:{ex.Message}"]);
        }

        ValidateRootAndConfig(root, failures);
        var fileMatch = PostmortemFileRegex.Match(Path.GetFileName(full));
        if (!fileMatch.Success) return new OperationsLearningValidationResult(false, ["postmortem-filename-invalid"]);
        var postmortemId = "PM" + fileMatch.Groups["number"].Value;
        var sameNumber = Directory.GetFiles(Path.Combine(root, ".engloop", "postmortems"), postmortemId + "*.md", SearchOption.TopDirectoryOnly);
        RecordFailure(failures, sameNumber.Length != 1, $"duplicate-postmortem-number:{postmortemId}");

        var text = NormalizeText(File.ReadAllText(full));
        foreach (var heading in new[] { "## Selected stabilized incidents", "## Root causes", "## SEK Test-Escape Analysis", "## Direction and Learning-Pyramid Consultation", "### Rule dispositions", "## Learnings", "## Repair Items" })
            RequireUniqueHeading(text, heading, failures);
        var firstHeading = Regex.Match(text, @"(?m)^## ").Index;
        var prologue = firstHeading > 0 ? text[..firstHeading] : text;
        RecordFailure(failures, ReadField(prologue, "Status").ToUpperInvariant() != "COMPLETE", "postmortem-status-not-complete");

        var northstarPath = ResolveFixedRegularFile(root, "NORTHSTAR.md", failures, "northstar");
        var learningsPath = ResolveFixedRegularFile(root, "LEARNINGS.md", failures, "learnings-index");
        var northstarSha = northstarPath is null ? string.Empty : Sha256(northstarPath);
        var learningsSha = learningsPath is null ? string.Empty : Sha256(learningsPath);
        var consultation = ExtractSection(text, "## Direction and Learning-Pyramid Consultation", ["## "]);
        RequireField(consultation, "North Star path", "NORTHSTAR.md", failures);
        RequireHash(consultation, "North Star SHA-256", northstarSha, failures);
        RequireField(consultation, "Learnings index path", "LEARNINGS.md", failures);
        RequireHash(consultation, "Learnings index SHA-256", learningsSha, failures);
        var pyramidDigest = ComputePyramidDigest(root, postmortemId, failures);
        RequireHash(consultation, "Pyramid digest", pyramidDigest, failures);

        var incidents = ValidateSelectedIncidents(root, text, selectedIncidentIds, failures);
        var directionAlignment = RequireEnumField(consultation, "Direction alignment", ["ALIGNED", "TENSION", "GAP"], failures);
        RequireSubstantiveField(consultation, "Direction decision", failures);
        var pyramidDecision = RequireEnumField(consultation, "Pyramid decision", ["UPDATED", "NO-CHANGE"], failures);
        RequireSubstantiveField(consultation, "Pyramid rationale", failures);
        var historicalDecision = RequireEnumField(consultation, "Historical coverage decision", ["UPDATED", "NO-CHANGE"], failures);
        var historicalPath = ReadField(consultation, "Historical coverage path");
        var changedPyramidPaths = ReadField(consultation, "Changed pyramid paths");
        var retrievalImpact = RequireEnumField(consultation, "Retrieval impact", ["CHANGED", "UNCHANGED"], failures);
        var retrievalEvidence = ReadField(consultation, "Retrieval evidence");
        RequireSubstantiveField(consultation, "Retrieval rationale", failures);
        var sekEscape = ParseSekEscapeAnalysis(root, ExtractSection(text, "## SEK Test-Escape Analysis", ["## "]), failures);

        var cardsRoot = Path.Combine(root, ".engloop", "learnings", "cards");
        var cards = LearningsPyramidPolicy.ExtractCards(cardsRoot);
        var cardsBySlug = cards.ToDictionary(card => card.Slug, StringComparer.Ordinal);
        var dispositions = ParseRuleDispositions(consultation, failures);
        RecordFailure(failures, dispositions.Count == 0, "missing-rule-disposition");
        RecordFailure(failures, dispositions.Select(item => item.RuleId).Distinct(StringComparer.Ordinal).Count() != dispositions.Count, "duplicate-rule-disposition");

        var currentSourceIds = ParseCurrentPostmortemSources(text, postmortemId, failures);
        var noAcceptedLearning = ReadField(ExtractSection(text, "## Learnings", ["## "]), "No accepted source learning");
        RecordFailure(failures, currentSourceIds.Count == 0 & !IsSubstantive(noAcceptedLearning), "missing-current-learning-or-explicit-no-learning-decision");
        RecordFailure(failures, currentSourceIds.Count > 0 & !string.IsNullOrWhiteSpace(noAcceptedLearning), "current-learning-and-no-learning-decision-conflict");
        RecordFailure(failures, pyramidDecision == "NO-CHANGE" & currentSourceIds.Count > 0, "no-change-cannot-leave-accepted-learning-uncovered");
        RecordFailure(failures, pyramidDecision == "UPDATED" & currentSourceIds.Count == 0, "updated-pyramid-requires-current-learning");
        var dispositionSources = dispositions.SelectMany(item => item.SourceIds).ToHashSet(StringComparer.Ordinal);
        foreach (var sourceId in currentSourceIds)
        {
            RecordFailure(failures, !dispositionSources.Contains(sourceId), $"current-learning-not-dispositioned:{sourceId}");
        }

        foreach (var disposition in dispositions)
        {
            ValidateRuleDisposition(disposition, cardsBySlug, currentSourceIds, pyramidDecision, failures);
        }

        var sources = LearningsPyramidPolicy.ExtractSources(
            Path.Combine(root, ".engloop", "postmortems"),
            Path.Combine(root, ".engloop", "happy-minutes"));
        var pyramid = LearningsPyramidPolicy.Validate(learningsPath ?? Path.Combine(root, "LEARNINGS.md"), sources, cards);
        failures.AddRange(pyramid.Failures.Select(failure => "pyramid-" + failure));

        if (pyramidDecision == "UPDATED")
        {
            RecordFailure(failures, historicalDecision != "UPDATED", "historical-coverage-not-updated");
            RecordFailure(failures, string.IsNullOrWhiteSpace(changedPyramidPaths) | changedPyramidPaths == "NOT-REQUIRED" | !IsSubstantive(changedPyramidPaths), "missing-changed-pyramid-paths");
            ValidateHistoricalCoverage(root, historicalPath, postmortemId, failures);
        }
        else
        {
            RecordFailure(failures, historicalDecision != "NO-CHANGE", "historical-coverage-decision-mismatch");
            RecordFailure(failures, historicalPath != "NOT-REQUIRED", "historical-coverage-path-must-be-not-required");
            RecordFailure(failures, changedPyramidPaths != "NOT-REQUIRED", "changed-pyramid-paths-must-be-not-required");
        }

        var semanticRuleChange = dispositions.Any(item => item.Disposition is "CONTRADICTED" or "MISSING") && pyramidDecision == "UPDATED";
        RecordFailure(failures, semanticRuleChange & retrievalImpact != "CHANGED", "retrieval-required-for-rule-change");
        if (retrievalImpact == "CHANGED") ValidateRetrievalEvidence(root, retrievalEvidence, failures);
        else RecordFailure(failures, retrievalEvidence != "NOT-REQUIRED", "retrieval-evidence-must-be-not-required");

        var repairSection = ExtractSection(text, "## Repair Items", ["## "]);
        var repairs = ParseRepairContracts(repairSection, dispositions.Select(item => item.RuleId).ToHashSet(StringComparer.Ordinal), sekEscape, failures);
        var repairItems = ParseRepairItemIds(repairSection, failures);
        RecordFailure(failures, repairItems.Count == 0, "repair-item-required");
        foreach (var repairItem in repairItems) RecordFailure(failures, !repairs.ContainsKey(repairItem), $"repair-item-learning-contract-missing:{repairItem}");
        foreach (var repairContract in repairs.Keys) RecordFailure(failures, !repairItems.Contains(repairContract, StringComparer.Ordinal), $"repair-contract-without-item:{repairContract}");

        var contract = new PostmortemLearningContract(
            postmortemId,
            relative,
            Sha256(full),
            northstarSha,
            learningsSha,
            pyramidDigest,
            incidents,
            directionAlignment,
            pyramidDecision,
            retrievalImpact,
            sekEscape,
            dispositions,
            repairs);
        return Result(failures, contract);
    }

    public static IncidentContextValidationResult ValidateIncidentContext(string repositoryRoot, string incidentPath, bool requireConsulted)
    {
        var failures = new List<string>();
        try
        {
            var root = NormalizeRoot(repositoryRoot);
            var (relative, full) = ResolveGovernedPath(root, incidentPath, ".engloop/incidents/", true);
            var idMatch = Regex.Match(Path.GetFileName(full), @"^(IN\d{3})", RegexOptions.CultureInvariant);
            if (!idMatch.Success) return new IncidentContextValidationResult(false, ["incident-filename-invalid"]);
            var incidentId = idMatch.Groups[1].Value;
            var text = NormalizeText(File.ReadAllText(full));
            var firstHeading = Regex.Match(text, @"(?m)^## ").Index;
            var prologue = firstHeading > 0 ? text[..firstHeading] : text;
            var status = ReadField(prologue, "Status").ToUpperInvariant();
            RecordFailure(failures, status is not ("STABILIZED" or "RESOLVED"), "incident-not-stabilized");
            RequireUniqueHeading(text, "## Verification (stability, not root-cause fix)", failures);
            RequireUniqueHeading(text, "## Direction and learning context", failures);
            var verification = ExtractSection(text, "## Verification (stability, not root-cause fix)", ["## "]);
            foreach (var check in new[] { "Health checks passing", "User workflows unblocked", "No fresh errors in the watch window" })
                RequireCheckedEvidence(verification, check, failures);
            var directionSection = ExtractSection(text, "## Direction and learning context", ["## "]);
            RecordFailure(failures, string.IsNullOrWhiteSpace(directionSection), "incident-direction-learning-section-missing");
            var northstarPath = ResolveFixedRegularFile(root, "NORTHSTAR.md", failures, "northstar");
            var northstarSha = northstarPath is null ? string.Empty : Sha256(northstarPath);
            RequireHash(directionSection, "North Star SHA-256", northstarSha, failures);
            var context = RequireEnumField(directionSection, "Learning context", ["CONSULTED", "DEFERRED"], failures);
            var rules = ParseRuleList(ReadField(directionSection, "Rule IDs"), true, "incident-rule-ids-invalid", failures);
            var sources = ParseSourceList(ReadField(directionSection, "Source IDs"), true, "incident-source-ids-invalid", failures);
            var deferralReason = ReadField(directionSection, "Deferral reason");
            if (context == "DEFERRED")
            {
                RecordFailure(failures, !IsSubstantive(deferralReason) | deferralReason == "NOT-REQUIRED", "incident-deferral-reason-missing");
                RecordFailure(failures, requireConsulted, "incident-learning-consultation-still-deferred");
            }
            else RecordFailure(failures, deferralReason != "NOT-REQUIRED", "incident-deferral-reason-must-be-not-required");
            var contract = new IncidentContextContract(incidentId, relative, Sha256(full), northstarSha, rules, sources);
            return new IncidentContextValidationResult(failures.Count == 0, Sorted(failures), contract);
        }
        catch (Exception ex)
        {
            failures.Add("incident-context-invalid:" + ex.Message);
            return new IncidentContextValidationResult(false, Sorted(failures));
        }
    }

    public static OperationsLearningValidationResult ValidateRepairAcceptance(
        string repositoryRoot,
        string postmortemPath,
        string rpiId,
        IReadOnlyCollection<string> suppliedRuleIds,
        string acceptancePath,
        string phase,
        bool currentReadinessPass)
    {
        var pmResult = ValidatePostmortem(repositoryRoot, postmortemPath);
        var failures = new List<string>(pmResult.Failures);
        if (pmResult.Contract is null) return Result(failures);
        if (!pmResult.Contract.Repairs.TryGetValue(rpiId, out var repair))
        {
            failures.Add($"repair-contract-missing:{rpiId}");
            return Result(failures, pmResult.Contract);
        }
        var expectedRules = repair.RuleIds.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var actualRules = suppliedRuleIds.ToArray();
        RecordFailure(failures, actualRules.Length != actualRules.Distinct(StringComparer.Ordinal).Count(), "repair-rule-ids-duplicate");
        RecordFailure(failures, !expectedRules.SequenceEqual(actualRules.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal), "repair-rule-ids-mismatch");
        RecordFailure(failures, phase is not ("route" or "close"), "repair-phase-invalid");

        try
        {
            var root = NormalizeRoot(repositoryRoot);
            var (relativeAcceptance, fullAcceptance) = ResolveGovernedPath(root, acceptancePath, ".engloop/repairs/", true);
            using var json = JsonDocument.Parse(File.ReadAllText(fullAcceptance));
            var element = json.RootElement;
            ValidateAcceptanceCommon(element, phase, pmResult.Contract, repair, expectedRules, failures);
            if (phase == "route") ValidateRouteRecord(element, relativeAcceptance, repair, failures);
            else ValidateCloseRecord(root, element, relativeAcceptance, pmResult.Contract, repair, expectedRules, currentReadinessPass, failures);
        }
        catch (Exception ex) { failures.Add("repair-acceptance-invalid:" + ex.Message); }
        return Result(failures, pmResult.Contract);
    }

    public static string Sha256(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    public static string ComputeArgumentVectorDigest(IReadOnlyList<string> arguments)
        => Sha256Bytes(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(arguments)));

    public static string ComputeCardsDigest(string cardsRoot)
    {
        if (!Directory.Exists(cardsRoot)) return Sha256Bytes([]);
        var builder = new StringBuilder();
        foreach (var path in Directory.GetFiles(cardsRoot, "*.md", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
            builder.Append(Path.GetFileName(path)).Append(':').Append(Sha256(path)).Append('\n');
        return Sha256Bytes(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    public static string ComputePyramidDigest(string root, string currentPostmortemId, List<string>? failures = null)
    {
        try
        {
            var entries = new List<string>();
            var localFailures = failures ?? [];
            var learnings = ResolveFixedRegularFile(root, "LEARNINGS.md", localFailures, "learnings-index");
            if (learnings is not null) entries.Add("LEARNINGS.md:" + Sha256(learnings));
            var cardsRoot = Path.Combine(root, ".engloop", "learnings", "cards");
            foreach (var path in Directory.GetFiles(cardsRoot, "*.md", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
            {
                RejectReparse(path, "card");
                entries.Add(Path.GetRelativePath(root, path).Replace('\\', '/') + ":" + Sha256(path));
            }
            var postmortemsRoot = Path.Combine(root, ".engloop", "postmortems");
            foreach (var path in Directory.GetFiles(postmortemsRoot, "PM*.md", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
            {
                var id = Regex.Match(Path.GetFileName(path), @"^PM\d{3}").Value;
                if (id == currentPostmortemId) continue;
                RejectReparse(path, "postmortem");
                entries.Add(Path.GetRelativePath(root, path).Replace('\\', '/') + ":" + Sha256(path));
            }
            var happyRoot = Path.Combine(root, ".engloop", "happy-minutes");
            foreach (var source in LearningsPyramidPolicy.ExtractSources(postmortemsRoot, happyRoot)
                         .Where(source => source.Id.StartsWith("HAPPY", StringComparison.Ordinal)))
            {
                RejectReparse(source.Path, "happy-minute");
                entries.Add(Path.GetRelativePath(root, source.Path).Replace('\\', '/') + ":" + Sha256(source.Path));
            }
            return Sha256Bytes(Encoding.UTF8.GetBytes(string.Join("\n", entries) + "\n"));
        }
        catch (Exception ex)
        {
            failures?.Add("pyramid-digest-invalid:" + ex.Message);
            return string.Empty;
        }
    }

    public static string? GitHead(string root)
    {
        var result = RunGit(root, "rev-parse", "HEAD");
        return result.ExitCode == 0 && Regex.IsMatch(result.Output.Trim(), "^[0-9a-fA-F]{40,64}$") ? result.Output.Trim().ToLowerInvariant() : null;
    }

    public static string ComputeGitStatusDigest(string root, IReadOnlyCollection<string> excludedPaths)
    {
        var exclusions = excludedPaths.Select(path => NormalizeRelative(path).ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var result = RunGit(root, "status", "--porcelain=v1", "--untracked-files=all");
        if (result.ExitCode != 0) throw new InvalidOperationException("git-status-unavailable");
        var entries = NormalizeText(result.Output).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => SnapshotStatusLine(root, line))
            .Where(entry => !exclusions.Contains(entry.Path.ToLowerInvariant()))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.Status, StringComparer.Ordinal)
            .Select(entry => $"{entry.Status}|{entry.Path}|{entry.ContentIdentity}");
        return Sha256Bytes(Encoding.UTF8.GetBytes(string.Join("\n", entries)));
    }

    public static string ComputeReadinessWorktreeDigest(string root)
    {
        var result = RunGit(root, "status", "--porcelain=v1", "--untracked-files=all");
        if (result.ExitCode != 0) throw new InvalidOperationException("git-status-unavailable");
        var entries = NormalizeText(result.Output).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => SnapshotStatusLine(root, line))
            .Where(entry =>
            {
                var path = entry.Path;
                if (!path.StartsWith(".engloop/", StringComparison.OrdinalIgnoreCase)) return true;
                return string.Equals(path, ".engloop/config.json", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.Status, StringComparer.Ordinal)
            .Select(entry => $"{entry.Status}|{entry.Path}|{entry.ContentIdentity}");
        return Sha256Bytes(Encoding.UTF8.GetBytes(string.Join("\n", entries)));
    }

    public static string ComputeGitIndexDigest(string root)
    {
        var result = RunGit(root, "ls-files", "-s", "-z");
        if (result.ExitCode != 0) throw new InvalidOperationException("git-index-unavailable");
        return Sha256Bytes(Encoding.UTF8.GetBytes(result.Output));
    }

    private static (string Status, string Path, string ContentIdentity) SnapshotStatusLine(string root, string line)
    {
        if (line.Length < 4) return (line, string.Empty, "malformed");
        var status = line[..2];
        var rawPath = line[3..].Trim('"');
        var arrow = rawPath.IndexOf(" -> ", StringComparison.Ordinal);
        var path = (arrow >= 0 ? rawPath[(arrow + 4)..] : rawPath).Replace('\\', '/');
        var full = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        var identity = File.Exists(full) ? "file:" + Sha256(full) : Directory.Exists(full) ? "directory" : "missing";
        return (status, path, identity);
    }

    private static void ValidateRootAndConfig(string root, List<string> failures)
    {
        var rootResult = Evidence.ValidateRootLayout(root);
        if (!rootResult.Passed) { failures.Add(rootResult.Reason); return; }
        try
        {
            failures.AddRange(Evidence.ValidateConfigurationSafety(Evidence.LoadConfiguration(root)));
        }
        catch (Exception ex) { failures.Add("config-invalid:" + ex.Message); }
    }

    private static IReadOnlyList<IncidentContextContract> ValidateSelectedIncidents(string root, string text, IReadOnlyCollection<string> selectedIncidentIds, List<string> failures)
    {
        var requested = selectedIncidentIds.Select(value => value.Trim().ToUpperInvariant()).Where(value => value.Length > 0).ToArray();
        RecordFailure(failures, requested.Length == 0, "selected-incident-set-empty");
        RecordFailure(failures, requested.Length != requested.Distinct(StringComparer.Ordinal).Count(), "selected-incident-set-duplicate");
        foreach (var id in requested) RecordFailure(failures, !Regex.IsMatch(id, @"^IN\d{3}$", RegexOptions.CultureInvariant), $"selected-incident-id-invalid:{id}");
        var rows = ParseSelectedIncidentTable(text, failures);
        var expected = requested.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        RecordFailure(failures, !rows.Keys.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal), "selected-incident-table-mismatch");
        var contracts = new List<IncidentContextContract>();
        foreach (var id in expected)
        {
            if (!rows.TryGetValue(id, out var row)) continue;
            try
            {
                var (relative, full) = ResolveGovernedPath(root, row.Path, ".engloop/incidents/", true);
                RecordFailure(failures, !Path.GetFileName(full).StartsWith(id, StringComparison.Ordinal), $"incident-filename-id-mismatch:{id}");
                RecordFailure(failures, !string.Equals(Sha256(full), row.Hash, StringComparison.Ordinal), $"incident-hash-mismatch:{id}");
                var result = ValidateIncidentContext(root, relative, true);
                failures.AddRange(result.Failures.Select(failure => $"incident-{id}:{failure}"));
                if (result.Contract is not null) contracts.Add(result.Contract);
            }
            catch (Exception ex) { failures.Add($"incident-path-invalid:{id}:{ex.Message}"); }
        }
        return contracts;
    }

    private static Dictionary<string, (string Path, string Hash)> ParseSelectedIncidentTable(string text, List<string>? failures = null)
    {
        var rows = new Dictionary<string, (string Path, string Hash)>(StringComparer.Ordinal);
        var section = ExtractSection(NormalizeText(text), "## Selected stabilized incidents", ["## "]);
        foreach (var line in section.Split('\n'))
        {
            if (!line.TrimStart().StartsWith('|') || line.Contains("---", StringComparison.Ordinal)) continue;
            var cells = SplitTableRow(line);
            if (cells.Length > 0 && cells[0] == "Incident ID") continue;
            if (cells.Length != 3) { failures?.Add("selected-incident-row-shape-invalid"); continue; }
            var id = cells[0].ToUpperInvariant();
            if (!rows.TryAdd(id, (cells[1], cells[2].ToLowerInvariant()))) failures?.Add($"selected-incident-row-duplicate:{id}");
        }
        return rows;
    }

    private static IReadOnlyList<string> ParseCurrentPostmortemSources(string text, string postmortemId, List<string> failures)
    {
        var ids = LearningsPyramidPolicy.ExtractSourceIds(postmortemId, text);
        var section = ExtractSection(text, "## Learnings", ["## "]);
        var declarations = Regex.Matches(section, @"(?m)^-\s+\*\*(?<id>LEARN\d{3})\b", RegexOptions.CultureInvariant)
            .Select(match => postmortemId + "/" + match.Groups["id"].Value).ToArray();
        RecordFailure(failures, declarations.Length != declarations.Distinct(StringComparer.Ordinal).Count(), "duplicate-current-postmortem-learning-id");
        foreach (Match qualified in SourceIdFindRegex.Matches(section))
            RecordFailure(failures, !qualified.Value.StartsWith(postmortemId + "/", StringComparison.Ordinal), "current-learning-id-postmortem-mismatch:" + qualified.Value);
        return ids;
    }

    private static List<PyramidRuleDisposition> ParseRuleDispositions(string text, List<string> failures)
    {
        var results = new List<PyramidRuleDisposition>();
        var section = ExtractSection(text, "### Rule dispositions", ["### ", "## "]);
        foreach (var line in section.Split('\n'))
        {
            if (!line.TrimStart().StartsWith('|') || line.Contains("---", StringComparison.Ordinal)) continue;
            var cells = SplitTableRow(line);
            if (cells.Length > 0 && cells[0] == "Rule ID") continue;
            if (cells.Length != 6) { failures.Add("rule-disposition-row-shape-invalid"); continue; }
            var sources = cells[2] == "NONE" ? [] : ParseSourceList(cells[2], false, $"rule-source-ids-invalid:{cells[0]}", failures);
            results.Add(new PyramidRuleDisposition(cells[0], cells[1], sources, cells[3].ToUpperInvariant(), cells[4], cells[5]));
        }
        return results;
    }

    private static void ValidateRuleDisposition(PyramidRuleDisposition item, IReadOnlyDictionary<string, LearningCard> cards, IReadOnlyCollection<string> currentSources, string pyramidDecision, List<string> failures)
    {
        var match = RuleIdRegex.Match(item.RuleId);
        if (!match.Success) { failures.Add($"invalid-rule-id:{item.RuleId}"); return; }
        RecordFailure(failures, item.Disposition is not ("REINFORCED" or "CONTRADICTED" or "MISSING"), $"invalid-rule-disposition:{item.RuleId}:{item.Disposition}");
        RecordFailure(failures, !IsSubstantive(item.IncidentEvidence), $"missing-rule-incident-evidence:{item.RuleId}");
        RecordFailure(failures, !IsSubstantive(item.PyramidAction), $"missing-rule-pyramid-action:{item.RuleId}");
        var slug = match.Groups["slug"].Value;
        RecordFailure(failures, item.CardId != slug & !(item.Disposition == "MISSING" & pyramidDecision == "NO-CHANGE" & item.CardId == "-"), $"rule-card-id-mismatch:{item.RuleId}:{item.CardId}");
        if (cards.TryGetValue(slug, out var card))
        {
            foreach (var source in item.SourceIds)
            {
                RecordFailure(failures, !card.SourceIds.Contains(source, StringComparer.Ordinal), $"rule-source-not-cited-by-card:{item.RuleId}:{source}");
            }
        }
        else RecordFailure(failures, !(item.Disposition == "MISSING" & pyramidDecision == "NO-CHANGE"), $"rule-card-missing:{item.RuleId}");
    }

    private static SekEscapeAnalysis ParseSekEscapeAnalysis(string root, string section, List<string> failures)
    {
        var applicability = RequireEnumField(section, "SEK applicability", ["RELEVANT", "NOT-RELEVANT"], failures);
        var rationale = RequireSubstantiveField(section, "SEK applicability rationale", failures);
        var version = ReadField(section, "SEK version");
        var verificationClass = RequireEnumField(section, "SEK verification class",
            ["STATEFUL-VERTICAL", "NON-STATEFUL-COMPONENT", "INFRASTRUCTURE", "DOCUMENTATION", "EXTERNAL-DEPENDENCY"], failures);
        var escapeClass = RequireEnumField(section, "SEK escape class",
            ["MODEL-GAP", "CORD-DOMAIN-GAP", "CORD-SLICE-GAP", "CORD-BOUND-GAP", "BINDING-GAP", "ORACLE-GAP", "STALE-GENERATION", "SEK-ENGINE-GAP", "NOT-RELEVANT"], failures);
        var scenarioId = ReadField(section, "SEK scenario ID");
        var modelValue = ReadField(section, "SEK model paths");
        var cordValue = ReadField(section, "SEK CORD paths");
        var generatedSuite = ReadField(section, "SEK generated suite path");
        var whyEscaped = RequireSubstantiveField(section, "Why SEK tests missed the incident", failures);
        var requiredRepair = ReadField(section, "Required model/CORD repair");

        IReadOnlyList<string> modelPaths = [];
        IReadOnlyList<string> cordPaths = [];
        if (applicability == "RELEVANT")
        {
            RecordFailure(failures, version != "0.1.3", "sek-version-must-be-0.1.3");
            RecordFailure(failures, verificationClass != "STATEFUL-VERTICAL", "sek-relevant-requires-stateful-vertical");
            RecordFailure(failures, escapeClass == "NOT-RELEVANT", "sek-relevant-escape-class-invalid");
            RecordFailure(failures, !Regex.IsMatch(scenarioId, @"^SEK-SCENARIO:[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant), "sek-scenario-id-invalid");
            modelPaths = ParseExistingFilePaths(root, modelValue, ".cs", "sek-model-path", failures);
            cordPaths = ParseExistingFilePaths(root, cordValue, ".cord", "sek-cord-path", failures);
            ValidateExistingDirectoryPath(root, generatedSuite, "sek-generated-suite", failures);
            RecordFailure(failures, !IsSubstantive(requiredRepair), "sek-required-repair-missing");
        }
        else
        {
            RecordFailure(failures, version != "NOT-REQUIRED", "sek-not-relevant-version-must-be-not-required");
            RecordFailure(failures, verificationClass == "STATEFUL-VERTICAL", "sek-not-relevant-class-invalid");
            RecordFailure(failures, escapeClass != "NOT-RELEVANT", "sek-not-relevant-escape-class-invalid");
            foreach (var value in new[] { scenarioId, modelValue, cordValue, generatedSuite, requiredRepair })
                RecordFailure(failures, value != "NOT-REQUIRED", "sek-not-relevant-fields-must-be-not-required");
        }

        return new SekEscapeAnalysis(applicability, rationale, version, verificationClass, escapeClass,
            scenarioId, modelPaths, cordPaths, generatedSuite, whyEscaped, requiredRepair);
    }

    private static IReadOnlyList<string> ParseExistingFilePaths(string root, string value, string extension, string identity, List<string> failures)
    {
        var paths = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => path.Trim().Trim('`').Replace('\\', '/')).ToArray();
        RecordFailure(failures, paths.Length == 0 | paths.Length != paths.Distinct(StringComparer.Ordinal).Count(), identity + "-set-invalid");
        foreach (var path in paths)
        {
            RecordFailure(failures, !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase), identity + "-extension-invalid:" + path);
            try { _ = ResolveGovernedPath(root, path, string.Empty, true); }
            catch (Exception ex) { failures.Add(identity + "-invalid:" + path + ":" + ex.Message); }
        }
        return paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateExistingDirectoryPath(string root, string value, string identity, List<string> failures)
    {
        try
        {
            var relative = value.Trim().Trim('`').Replace('\\', '/');
            if (Path.IsPathRooted(relative) || relative.Contains("../", StringComparison.Ordinal)) throw new InvalidOperationException("path-must-be-root-relative");
            var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(full)) throw new InvalidOperationException("directory-missing-or-outside-root");
            var cursor = full;
            while (cursor.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                RejectReparse(cursor, identity);
                if (string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase)) break;
                cursor = Path.GetDirectoryName(cursor) ?? root;
            }
        }
        catch (Exception ex) { failures.Add(identity + "-invalid:" + ex.Message); }
    }

    private static Dictionary<string, RepairLearningContract> ParseRepairContracts(string text, IReadOnlySet<string> dispositionRules, SekEscapeAnalysis sekEscape, List<string> failures)
    {
        var results = new Dictionary<string, RepairLearningContract>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(text, @"(?ms)^### (?<id>RPI\d{3}) learning contract\s*$\n(?<body>.*?)(?=^### |^## |\z)"))
        {
            var id = match.Groups["id"].Value;
            if (!RpiRegex.IsMatch(id) || results.ContainsKey(id)) { failures.Add($"repair-contract-id-invalid:{id}"); continue; }
            var body = match.Groups["body"].Value;
            var rules = ParseRuleList(ReadField(body, "Rule IDs"), false, $"repair-contract-rule-ids-invalid:{id}", failures);
            foreach (var rule in rules) RecordFailure(failures, !dispositionRules.Contains(rule), $"repair-contract-rule-not-dispositioned:{id}:{rule}");
            var gate = ParseArgumentVector(ReadField(body, "Executable gate"), $"repair-contract-gate-invalid:{id}", failures);
            var proves = ReadField(body, "Gate proves");
            RecordFailure(failures, !IsSubstantive(proves), $"repair-contract-gate-proof-missing:{id}");
            var sekApplicability = RequireEnumField(body, "SEK applicability", ["RELEVANT", "NOT-RELEVANT"], failures);
            var sekScenarioId = ReadField(body, "SEK scenario ID");
            var sekRepairRequirement = ReadField(body, "SEK repair requirement");
            var sekGateValue = ReadField(body, "SEK verification gate");
            if (sekEscape.Applicability == "RELEVANT")
            {
                RecordFailure(failures, sekApplicability != "RELEVANT", $"repair-contract-sek-applicability-mismatch:{id}");
                RecordFailure(failures, sekScenarioId != sekEscape.ScenarioId, $"repair-contract-sek-scenario-mismatch:{id}");
                RecordFailure(failures, !IsSubstantive(sekRepairRequirement), $"repair-contract-sek-repair-missing:{id}");
                var sekGate = ParseArgumentVector(sekGateValue, $"repair-contract-sek-gate-invalid:{id}", failures);
                RecordFailure(failures, !gate.SequenceEqual(sekGate, StringComparer.Ordinal), $"repair-contract-executable-gate-must-equal-sek-gate:{id}");
                RecordFailure(failures, !IsSekGate(gate), $"repair-contract-sek-gate-does-not-invoke-sek:{id}");
            }
            else
            {
                RecordFailure(failures, sekApplicability != "NOT-RELEVANT", $"repair-contract-sek-applicability-mismatch:{id}");
                RecordFailure(failures, sekScenarioId != "NOT-REQUIRED" | sekRepairRequirement != "NOT-REQUIRED" | sekGateValue != "NOT-REQUIRED", $"repair-contract-sek-fields-must-be-not-required:{id}");
            }
            results[id] = new RepairLearningContract(id, rules, gate, ComputeArgumentVectorDigest(gate), proves,
                sekApplicability, sekScenarioId, sekRepairRequirement);
        }
        return results;
    }

    private static bool IsSekGate(IReadOnlyList<string> gate)
    {
        if (gate.Count == 0) return false;
        if (string.Equals(Path.GetFileNameWithoutExtension(gate[0]), "sek", StringComparison.OrdinalIgnoreCase)) return true;
        if (gate.Count >= 4 && string.Equals(Path.GetFileNameWithoutExtension(gate[0]), "dotnet", StringComparison.OrdinalIgnoreCase)
            && gate[1] == "tool" && gate[2] == "run" && gate[3] == "sek") return true;
        return gate.Any(argument => argument.Contains("sek", StringComparison.OrdinalIgnoreCase)
            && (argument.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) || argument.EndsWith(".sh", StringComparison.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<string> ParseRepairItemIds(string repairSection, List<string> failures)
    {
        var ids = new List<string>();
        foreach (var line in repairSection.Split('\n'))
        {
            if (!line.TrimStart().StartsWith('|') || line.Contains("---", StringComparison.Ordinal)) continue;
            var cells = SplitTableRow(line);
            if (cells.Length > 0 && cells[0] == "RPI") continue;
            if (cells.Length < 2 || !RpiRegex.IsMatch(cells[0])) { failures.Add("repair-item-row-invalid"); continue; }
            ids.Add(cells[0]);
        }
        RecordFailure(failures, ids.Count != ids.Distinct(StringComparer.Ordinal).Count(), "repair-item-id-duplicate");
        return ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateAcceptanceCommon(JsonElement element, string phase, PostmortemLearningContract pm, RepairLearningContract repair, IReadOnlyList<string> expectedRules, List<string> failures)
    {
        RequireJsonString(element, "schemaVersion", "1.0", failures);
        RequireJsonString(element, "artifactType", phase == "route" ? "repair-learning-route" : "repair-learning-close", failures);
        RequireJsonString(element, "phase", phase, failures);
        RequireJsonString(element, "postmortemPath", pm.RelativePath, failures);
        RequireJsonString(element, "postmortemSha256", pm.Sha256, failures);
        RequireJsonString(element, "rpiId", repair.RpiId, failures);
        RequireJsonString(element, "northstarSha256", pm.NorthstarSha256, failures);
        RequireJsonString(element, "learningsSha256", pm.LearningsSha256, failures);
        RequireJsonString(element, "pyramidDigest", pm.PyramidDigest, failures);
        RequireJsonString(element, "gateProves", repair.GateProves, failures);
        RequireJsonString(element, "sekApplicability", repair.SekApplicability, failures);
        RequireJsonString(element, "sekScenarioId", repair.SekScenarioId, failures);
        RequireJsonString(element, "sekRepairRequirement", repair.SekRepairRequirement, failures);
        RequireJsonString(element, "sekVersion", pm.SekEscape.Version, failures);
        RequireJsonString(element, "sekEscapeClass", pm.SekEscape.EscapeClass, failures);
        RequireJsonString(element, "sekGeneratedSuitePath", pm.SekEscape.GeneratedSuitePath, failures);
        var modelPaths = ReadJsonStringArrayStrict(element, "sekModelPaths", failures).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var cordPaths = ReadJsonStringArrayStrict(element, "sekCordPaths", failures).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        RecordFailure(failures, !pm.SekEscape.ModelPaths.SequenceEqual(modelPaths, StringComparer.Ordinal), "acceptance-sek-model-paths-mismatch");
        RecordFailure(failures, !pm.SekEscape.CordPaths.SequenceEqual(cordPaths, StringComparer.Ordinal), "acceptance-sek-cord-paths-mismatch");
        var rules = ReadJsonStringArrayStrict(element, "ruleIds", failures).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        RecordFailure(failures, !expectedRules.SequenceEqual(rules, StringComparer.Ordinal), "acceptance-rule-ids-mismatch");
    }

    private static void ValidateRouteRecord(JsonElement element, string relativePath, RepairLearningContract repair, List<string> failures)
    {
        RequireJsonString(element, "status", "ROUTED", failures);
        RecordFailure(failures, !relativePath.EndsWith(".route.json", StringComparison.Ordinal), "repair-route-filename-invalid");
        var gate = ReadJsonStringArrayStrict(element, "executableGate", failures);
        RecordFailure(failures, !repair.ExecutableGate.SequenceEqual(gate, StringComparer.Ordinal), "acceptance-executable-gate-mismatch");
        RequireJsonString(element, "executableGateDigest", repair.ExecutableGateDigest, failures);
    }

    private static void ValidateCloseRecord(string root, JsonElement element, string closePath, PostmortemLearningContract pm, RepairLearningContract repair, IReadOnlyList<string> expectedRules, bool currentReadinessPass, List<string> failures)
    {
        RequireJsonString(element, "status", "CLOSED", failures);
        RecordFailure(failures, !closePath.EndsWith(".close.json", StringComparison.Ordinal), "repair-close-filename-invalid");
        RecordFailure(failures, !currentReadinessPass, "repair-close-missing-current-readiness");
        var routePath = ReadJsonString(element, "routePath");
        var routeHash = ReadJsonString(element, "routeSha256");
        RecordFailure(failures, routePath == closePath, "repair-close-must-not-overwrite-route");
        ValidateRouteContinuity(root, routePath, routeHash, pm, repair, expectedRules, failures);
        ValidateGateReceipt(root, ReadJsonString(element, "gateReceiptPath"), ReadJsonString(element, "gateReceiptSha256"), routePath, routeHash, closePath, pm, repair, expectedRules, failures);
    }

    private static void ValidateRouteContinuity(string root, string routePath, string routeHash, PostmortemLearningContract pm, RepairLearningContract repair, IReadOnlyList<string> expectedRules, List<string> failures)
    {
        try
        {
            var (_, full) = ResolveGovernedPath(root, routePath, ".engloop/repairs/", true);
            if (!routePath.EndsWith(".route.json", StringComparison.Ordinal)) failures.Add("route-reference-filename-invalid");
            if (Sha256(full) != routeHash) failures.Add("route-reference-hash-mismatch");
            using var json = JsonDocument.Parse(File.ReadAllText(full));
            var element = json.RootElement;
            ValidateAcceptanceCommon(element, "route", pm, repair, expectedRules, failures);
            ValidateRouteRecord(element, routePath, repair, failures);
        }
        catch (Exception ex) { failures.Add("route-reference-invalid:" + ex.Message); }
    }

    private static void ValidateGateReceipt(string root, string receiptPath, string receiptHash, string routePath, string routeHash, string closePath, PostmortemLearningContract pm, RepairLearningContract repair, IReadOnlyList<string> expectedRules, List<string> failures)
    {
        try
        {
            var (_, full) = ResolveGovernedPath(root, receiptPath, ".engloop/out/repair-gates/", true);
            RecordFailure(failures, Sha256(full) != receiptHash, "gate-receipt-hash-mismatch");
            using var json = JsonDocument.Parse(File.ReadAllText(full));
            var element = json.RootElement;
            RequireJsonString(element, "schemaVersion", "1.0", failures, "gate-receipt");
            RequireJsonString(element, "artifactType", "repair-gate-receipt", failures, "gate-receipt");
            RequireJsonString(element, "verdict", "PASS", failures, "gate-receipt");
            RequireJsonString(element, "postmortemPath", pm.RelativePath, failures, "gate-receipt");
            RequireJsonString(element, "postmortemSha256", pm.Sha256, failures, "gate-receipt");
            RequireJsonString(element, "rpiId", repair.RpiId, failures, "gate-receipt");
            RequireJsonString(element, "pyramidDigest", pm.PyramidDigest, failures, "gate-receipt");
            RequireJsonString(element, "routePath", routePath, failures, "gate-receipt");
            RequireJsonString(element, "routeSha256", routeHash, failures, "gate-receipt");
            RequireJsonString(element, "executableGateDigest", repair.ExecutableGateDigest, failures, "gate-receipt");
            RequireJsonString(element, "sekApplicability", repair.SekApplicability, failures, "gate-receipt");
            RequireJsonString(element, "sekScenarioId", repair.SekScenarioId, failures, "gate-receipt");
            var rules = ReadJsonStringArrayStrict(element, "ruleIds", failures).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            RecordFailure(failures, !expectedRules.SequenceEqual(rules, StringComparer.Ordinal), "gate-receipt-rule-ids-mismatch");
            var gate = ReadJsonStringArrayStrict(element, "executableGate", failures);
            RecordFailure(failures, !repair.ExecutableGate.SequenceEqual(gate, StringComparer.Ordinal), "gate-receipt-command-mismatch");
            RecordFailure(failures, !element.TryGetProperty("exitCode", out var exitCode) | exitCode.ValueKind != JsonValueKind.Number | (exitCode.ValueKind == JsonValueKind.Number && exitCode.GetInt32() != 0), "gate-receipt-exit-not-zero");
            RecordFailure(failures, !element.TryGetProperty("completed", out var completed) | completed.ValueKind != JsonValueKind.True, "gate-receipt-not-completed");
            RecordFailure(failures, ReadJsonString(element, "preGateStatusDigest") != ReadJsonString(element, "sourceStatusDigest"), "gate-receipt-worktree-mutated");
            RecordFailure(failures, ReadJsonString(element, "preGateHead") != ReadJsonString(element, "sourceHead"), "gate-receipt-head-mutated");
            RecordFailure(failures, ReadJsonString(element, "preGateIndexDigest") != ReadJsonString(element, "sourceIndexDigest"), "gate-receipt-index-mutated");
            var stdoutPath = ReadJsonString(element, "stdoutPath");
            var stderrPath = ReadJsonString(element, "stderrPath");
            ValidateHashedOutput(root, stdoutPath, ReadJsonString(element, "stdoutSha256"), failures, "stdout", false);
            ValidateHashedOutput(root, stderrPath, ReadJsonString(element, "stderrSha256"), failures, "stderr", true);
            var excluded = new[] { receiptPath, stdoutPath, stderrPath, closePath };
            RecordFailure(failures, ReadJsonString(element, "sourceHead") != GitHead(root), "gate-receipt-head-stale");
            RecordFailure(failures, ReadJsonString(element, "sourceIndexDigest") != ComputeGitIndexDigest(root), "gate-receipt-index-stale");
            RecordFailure(failures, ReadJsonString(element, "sourceStatusDigest") != ComputeGitStatusDigest(root, excluded), "gate-receipt-status-stale");
        }
        catch (Exception ex) { failures.Add("gate-receipt-invalid:" + ex.Message); }
    }

    private static void ValidateHashedOutput(string root, string path, string hash, List<string> failures, string identity, bool allowEmpty)
    {
        try
        {
            var (_, full) = ResolveGovernedPath(root, path, ".engloop/out/repair-gates/", true);
            RecordFailure(failures, !allowEmpty & new FileInfo(full).Length == 0, $"gate-receipt-{identity}-empty");
            RecordFailure(failures, Sha256(full) != hash, $"gate-receipt-{identity}-hash-mismatch");
        }
        catch (Exception ex) { failures.Add($"gate-receipt-{identity}-invalid:{ex.Message}"); }
    }

    private static void ValidateHistoricalCoverage(string root, string relativePath, string postmortemId, List<string> failures)
    {
        try
        {
            var (_, full) = ResolveGovernedPath(root, relativePath, ".engloop/learnings/", true);
            if (!File.ReadAllText(full).Contains(postmortemId, StringComparison.Ordinal)) failures.Add($"historical-coverage-missing-postmortem:{postmortemId}");
        }
        catch (Exception ex) { failures.Add("historical-coverage-path-invalid:" + ex.Message); }
    }

    private static void ValidateRetrievalEvidence(string root, string relativePath, List<string> failures)
    {
        try
        {
            var (_, receiptPath) = ResolveGovernedPath(root, relativePath, ".engloop/out/", true);
            using var receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
            var element = receipt.RootElement;
            var casesPath = ResolveReceiptInput(root, element, "casesPath", "casesSha256", failures);
            var observedPath = ResolveReceiptInput(root, element, "observedResultsPath", "observedResultsSha256", failures);
            if (casesPath is null || observedPath is null) return;
            RecordFailure(failures, ReadJsonString(element, "learningsSha256") != Sha256(Path.Combine(root, "LEARNINGS.md")), "retrieval-learnings-hash-stale");
            RecordFailure(failures, ReadJsonString(element, "cardsDigest") != ComputeCardsDigest(Path.Combine(root, ".engloop", "learnings", "cards")), "retrieval-cards-digest-stale");
            failures.AddRange(RecomputeRetrieval(root, casesPath, observedPath).Select(failure => "retrieval-" + failure));
        }
        catch (Exception ex) { failures.Add("retrieval-evidence-invalid:" + ex.Message); }
    }

    private static string? ResolveReceiptInput(string root, JsonElement element, string pathField, string hashField, List<string> failures)
    {
        try
        {
            var (_, full) = ResolveGovernedPath(root, ReadJsonString(element, pathField), ".engloop/learnings/", true);
            if (ReadJsonString(element, hashField) != Sha256(full)) failures.Add($"retrieval-input-hash-stale:{pathField}");
            return full;
        }
        catch (Exception ex) { failures.Add($"retrieval-input-invalid:{pathField}:{ex.Message}"); return null; }
    }

    private static IReadOnlyList<string> RecomputeRetrieval(string root, string casesPath, string observedPath)
    {
        var failures = new List<string>();
        var cards = LearningsPyramidPolicy.ExtractCards(Path.Combine(root, ".engloop", "learnings", "cards"))
            .ToDictionary(card => card.Slug, StringComparer.Ordinal);
        using var casesJson = JsonDocument.Parse(File.ReadAllText(casesPath));
        using var observedJson = JsonDocument.Parse(File.ReadAllText(observedPath));
        if (!casesJson.RootElement.TryGetProperty("cases", out var cases) || cases.ValueKind != JsonValueKind.Array) return ["cases-missing"];
        if (!observedJson.RootElement.TryGetProperty("results", out var observed) || observed.ValueKind != JsonValueKind.Array) return ["observed-results-missing"];
        var observedById = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var result in observed.EnumerateArray())
        {
            var id = ReadJsonString(result, "id");
            if (id.Length == 0 || !observedById.TryAdd(id, result.Clone())) failures.Add($"observed-id-invalid-or-duplicate:{id}");
        }
        var expectedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in cases.EnumerateArray())
        {
            var id = ReadJsonString(item, "id");
            if (id.Length == 0 || !expectedIds.Add(id)) { failures.Add($"case-id-invalid-or-duplicate:{id}"); continue; }
            if (!observedById.TryGetValue(id, out var actual)) { failures.Add($"missing-observed-case:{id}"); continue; }
            var expectedCards = ReadJsonStringArrayStrict(item, "expectedCardIds", failures).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var expectedSources = ReadJsonStringArrayStrict(item, "expectedSourceIds", failures).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var actualCards = ReadJsonStringArrayStrict(actual, "actualCardIds", failures).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var actualSources = ReadJsonStringArrayStrict(actual, "actualSourceIds", failures).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var expectGap = item.TryGetProperty("expectGap", out var gap) && gap.ValueKind == JsonValueKind.True;
            var expectedVerdict = expectGap ? "GAP" : "PASS";
            if (ReadJsonString(actual, "verdict") != expectedVerdict) failures.Add($"verdict-mismatch:{id}");
            if (!expectedCards.SequenceEqual(actualCards, StringComparer.Ordinal)) failures.Add($"card-set-mismatch:{id}");
            if (!expectedSources.SequenceEqual(actualSources, StringComparer.Ordinal)) failures.Add($"source-set-mismatch:{id}");
            ValidateRetrievedProvenance(id, expectedCards, expectedSources, cards, failures, "expected");
            ValidateRetrievedProvenance(id, actualCards, actualSources, cards, failures, "actual");
            if (expectGap && (actualCards.Length != 0 || actualSources.Length != 0)) failures.Add($"gap-case-not-empty:{id}");
        }
        foreach (var id in observedById.Keys) if (!expectedIds.Contains(id)) failures.Add($"unexpected-observed-case:{id}");
        return Sorted(failures);
    }

    private static void ValidateRetrievedProvenance(
        string caseId,
        IReadOnlyList<string> cardIds,
        IReadOnlyList<string> sourceIds,
        IReadOnlyDictionary<string, LearningCard> cards,
        List<string> failures,
        string identity)
    {
        var cited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cardId in cardIds)
        {
            if (!cards.TryGetValue(cardId, out var card))
            {
                failures.Add($"{identity}-unknown-card:{caseId}:{cardId}");
                continue;
            }
            cited.UnionWith(card.SourceIds);
        }
        foreach (var sourceId in sourceIds)
        {
            if (!SourceIdRegex.IsMatch(sourceId)) failures.Add($"{identity}-source-id-invalid:{caseId}:{sourceId}");
            else if (!cited.Contains(sourceId)) failures.Add($"{identity}-source-not-cited:{caseId}:{sourceId}");
        }
    }

    private static IReadOnlyList<string> ParseRuleList(string value, bool allowNone, string failure, List<string> failures)
        => ParseIdList(value, allowNone, RuleIdRegex, failure, failures);

    private static IReadOnlyList<string> ParseSourceList(string value, bool allowNone, string failure, List<string> failures)
        => ParseIdList(value, allowNone, SourceIdRegex, failure, failures);

    private static IReadOnlyList<string> ParseIdList(string value, bool allowNone, Regex regex, string failure, List<string> failures)
    {
        if (allowNone && value == "NONE") return [];
        var tokens = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim().Trim('`')).ToArray();
        if (tokens.Length == 0 || tokens.Length != tokens.Distinct(StringComparer.Ordinal).Count() || tokens.Any(token => !regex.IsMatch(token))) failures.Add(failure);
        return tokens.Distinct(StringComparer.Ordinal).OrderBy(token => token, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> ParseArgumentVector(string value, string failure, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('<') || value.Contains('>')) { failures.Add(failure + ":missing"); return []; }
        try
        {
            using var json = JsonDocument.Parse(value);
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("not-array");
            var result = new List<string>();
            foreach (var item in json.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())) throw new InvalidOperationException("non-string-or-empty");
                result.Add(item.GetString()!);
            }
            if (result.Count == 0) throw new InvalidOperationException("empty");
            return result;
        }
        catch (Exception ex) { failures.Add(failure + ":" + ex.Message); return []; }
    }

    private static IReadOnlyList<string> ReadJsonStringArrayStrict(JsonElement element, string name, List<string> failures)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            failures.Add($"json-array-missing:{name}");
            return [];
        }
        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())) { failures.Add($"json-array-item-invalid:{name}"); continue; }
            result.Add(item.GetString()!);
        }
        if (result.Count != result.Distinct(StringComparer.Ordinal).Count()) failures.Add($"json-array-duplicate:{name}");
        return result;
    }

    private static string? ResolveFixedRegularFile(string root, string relativePath, List<string> failures, string identity)
    {
        try
        {
            var (_, full) = ResolveGovernedPath(root, relativePath, string.Empty, true);
            var attributes = File.GetAttributes(full);
            if ((attributes & FileAttributes.Directory) != 0 || (attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("not-regular-file");
            return full;
        }
        catch (Exception ex) { failures.Add($"{identity}-invalid:{ex.Message}"); return null; }
    }

    private static (string Relative, string Full) ResolveGovernedPath(string root, string candidate, string requiredPrefix, bool requireExisting)
    {
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate)) throw new InvalidOperationException("path-must-be-relative");
        var normalized = NormalizeRelative(candidate);
        var full = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || (requiredPrefix.Length > 0 && !relative.StartsWith(requiredPrefix, StringComparison.Ordinal))) throw new InvalidOperationException("path-outside-governed-root");
        if (requireExisting && !File.Exists(full)) throw new FileNotFoundException("file-missing", full);
        var cursor = requireExisting ? full : Path.GetDirectoryName(full);
        while (!string.IsNullOrWhiteSpace(cursor) && cursor.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(cursor) || Directory.Exists(cursor)) RejectReparse(cursor, "path");
            if (string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase)) break;
            cursor = Path.GetDirectoryName(cursor);
        }
        return (relative, full);
    }

    private static void RejectReparse(string path, string identity)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException(identity + "-reparse-point-forbidden");
    }

    private static string NormalizeRoot(string root) => Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    private static string NormalizeRelative(string value) => value.Trim().Trim('`').Replace('\\', '/');
    private static string NormalizeText(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    private static string[] SplitTableRow(string line) => line.Trim().Trim('|').Split('|').Select(value => value.Trim().Trim('`')).ToArray();
    private static string ExtractSection(string text, string heading, IReadOnlyList<string> nextPrefixes)
    {
        var matches = Regex.Matches(text, @"(?m)^" + Regex.Escape(heading) + @"\s*$", RegexOptions.CultureInvariant);
        if (matches.Count != 1) return string.Empty;
        var start = matches[0].Index + matches[0].Length;
        var end = text.Length;
        foreach (var prefix in nextPrefixes)
        {
            var match = Regex.Match(text[start..], @"(?m)^" + Regex.Escape(prefix) + @".+$", RegexOptions.CultureInvariant);
            if (match.Success && start + match.Index < end) end = start + match.Index;
        }
        return text[start..end];
    }

    private static void RequireUniqueHeading(string text, string heading, List<string> failures)
    {
        var count = Regex.Matches(text, @"(?m)^" + Regex.Escape(heading) + @"\s*$", RegexOptions.CultureInvariant).Count;
        if (count != 1) failures.Add($"heading-count-invalid:{heading}:{count}");
    }

    private static void RequireCheckedEvidence(string section, string label, List<string> failures)
    {
        var matches = Regex.Matches(section, @"(?m)^- \[x\] " + Regex.Escape(label) + @":\s*(?<evidence>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (matches.Count != 1 || !IsSubstantive(matches.Count == 1 ? matches[0].Groups["evidence"].Value : string.Empty))
            failures.Add("incident-stability-check-invalid:" + ToKebab(label));
    }

    private static string ReadField(string text, string name)
    {
        var match = Regex.Match(text, @"(?m)^- \*\*" + Regex.Escape(name) + @":\*\*\s*(?<value>.+?)\s*$", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim().Trim('`') : string.Empty;
    }

    private static string RequireSubstantiveField(string text, string name, List<string> failures)
    {
        var value = ReadField(text, name);
        if (!IsSubstantive(value)) failures.Add("missing-or-weak-field:" + ToKebab(name));
        return value;
    }

    private static string RequireEnumField(string text, string name, IReadOnlyCollection<string> allowed, List<string> failures)
    {
        var value = ReadField(text, name).ToUpperInvariant();
        if (!allowed.Contains(value, StringComparer.Ordinal)) failures.Add("invalid-field:" + ToKebab(name));
        return value;
    }

    private static void RequireField(string text, string name, string expected, List<string> failures)
    {
        if (ReadField(text, name) != expected) failures.Add("invalid-field:" + ToKebab(name));
    }

    private static void RequireHash(string text, string name, string expected, List<string> failures)
    {
        var value = ReadField(text, name).ToLowerInvariant();
        if (value.Length != 64 || value != expected) failures.Add("stale-or-invalid-hash:" + ToKebab(name));
    }

    private static bool IsSubstantive(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('<') || value.Contains('>') || value.Contains("TODO", StringComparison.OrdinalIgnoreCase) || value.Contains("TBD", StringComparison.OrdinalIgnoreCase)) return false;
        return Regex.Matches(value, @"[\p{L}\p{N}]+", RegexOptions.CultureInvariant).Count >= 6;
    }

    private static string ToKebab(string value) => Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    private static string ReadJsonString(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static void RequireJsonString(JsonElement element, string name, string expected, List<string> failures, string prefix = "acceptance")
    {
        if (ReadJsonString(element, name) != expected) failures.Add($"{prefix}-field-mismatch:{name}");
    }

    private static OperationsLearningValidationResult Result(List<string> failures, PostmortemLearningContract? contract = null)
        => new(failures.Count == 0, Sorted(failures), contract);
    private static IReadOnlyList<string> Sorted(IEnumerable<string> failures) => failures.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    private static string Sha256Bytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RecordFailure(List<string> failures, bool condition, string failure)
    {
        if (condition) failures.Add(failure);
    }

    private static (int ExitCode, string Output) RunGit(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git-start-failed");
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
