using System.Text.RegularExpressions;

namespace EngLoopKit.Core;

public sealed record LearningSource(string Id, string Path, string Title);

public sealed record LearningCard(string Slug, string Path, IReadOnlyList<string> SourceIds, bool HasTensionSection);

public sealed record LearningsValidationResult(bool Passed, IReadOnlyList<string> Failures, int WordCount, int NonblankLines);

public static class LearningsPyramidPolicy
{
    private static readonly Regex SourceIdRegex = new(@"PM\d{3}/LEARN\d{3}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<LearningSource> ExtractSources(string postmortemsRoot)
    {
        if (!Directory.Exists(postmortemsRoot))
        {
            return [];
        }

        var results = new List<LearningSource>();
        var files = Directory.GetFiles(postmortemsRoot, "PM*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match match in SourceIdRegex.Matches(text))
            {
                var id = match.Value;
                results.Add(new LearningSource(id, file, Path.GetFileName(file)));
            }
        }

        return results
            .GroupBy(source => source.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(source => source.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<LearningCard> ExtractCards(string cardsRoot)
    {
        if (!Directory.Exists(cardsRoot))
        {
            return [];
        }

        var cards = new List<LearningCard>();
        foreach (var path in Directory.GetFiles(cardsRoot, "*.md", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            var text = File.ReadAllText(path);
            var sourceIds = SourceIdRegex.Matches(text)
                .Select(match => match.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            var hasTension = text.Contains("## Tensions", StringComparison.Ordinal)
                             || text.Contains("## Conflict", StringComparison.Ordinal)
                             || text.Contains("none known", StringComparison.OrdinalIgnoreCase);

            cards.Add(new LearningCard(Path.GetFileNameWithoutExtension(path), path, sourceIds, hasTension));
        }

        return cards;
    }

    public static LearningsValidationResult Validate(
        string learningsIndexPath,
        IReadOnlyList<LearningSource> sources,
        IReadOnlyList<LearningCard> cards,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? retrievalActual = null,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? retrievalExpected = null)
    {
        var failures = new List<string>();
        if (!File.Exists(learningsIndexPath))
        {
            failures.Add("missing-learnings-index");
            return new LearningsValidationResult(false, failures, 0, 0);
        }

        var index = File.ReadAllText(learningsIndexPath);
        var lines = index.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var nonblankLines = lines.Count(line => !string.IsNullOrWhiteSpace(line));
        var words = Regex.Matches(index, @"[\p{L}\p{N}]+(?:['’_-][\p{L}\p{N}]+)*").Count;

        if (words > 500)
        {
            failures.Add($"index-word-budget-exceeded:{words}");
        }

        if (nonblankLines > 60)
        {
            failures.Add($"index-line-budget-exceeded:{nonblankLines}");
        }

        if (sources.Count == 0)
        {
            failures.Add("missing-learning-sources");
        }

        if (cards.Count == 0)
        {
            failures.Add("missing-learning-cards");
        }

        foreach (var card in cards)
        {
            if (card.SourceIds.Count == 0)
            {
                failures.Add($"card-without-source:{card.Slug}");
            }

            if (!card.HasTensionSection)
            {
                failures.Add($"card-missing-tension:{card.Slug}");
            }

            var cardBody = File.ReadAllText(card.Path);
            if (!cardBody.Contains("supersession", StringComparison.OrdinalIgnoreCase)
                && !cardBody.Contains("conflict", StringComparison.OrdinalIgnoreCase)
                && !cardBody.Contains("tension", StringComparison.OrdinalIgnoreCase)
                && !cardBody.Contains("none known", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"card-missing-conflict-or-supersession:{card.Slug}");
            }

            var expectedLink = $".engloop/learnings/cards/{card.Slug}.md";
            if (!index.Contains(expectedLink, StringComparison.Ordinal))
            {
                failures.Add($"index-missing-card-link:{card.Slug}");
            }
        }

        var coveredSourceIds = new HashSet<string>(cards.SelectMany(card => card.SourceIds), StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (!coveredSourceIds.Contains(source.Id))
            {
                failures.Add($"uncovered-source:{source.Id}");
            }
        }

        if (retrievalActual is not null && retrievalExpected is not null)
        {
            foreach (var (caseId, expectedIds) in retrievalExpected)
            {
                if (!retrievalActual.TryGetValue(caseId, out var actualIds))
                {
                    failures.Add($"retrieval-missing-case:{caseId}");
                    continue;
                }

                var expectedSet = new HashSet<string>(expectedIds, StringComparer.Ordinal);
                var actualSet = new HashSet<string>(actualIds, StringComparer.Ordinal);
                foreach (var missing in expectedSet.Except(actualSet))
                {
                    failures.Add($"retrieval-missing-id:{caseId}:{missing}");
                }

                foreach (var extra in actualSet.Except(expectedSet))
                {
                    failures.Add($"retrieval-false-provenance:{caseId}:{extra}");
                }
            }

            foreach (var unknownCase in retrievalActual.Keys.Except(retrievalExpected.Keys, StringComparer.Ordinal))
            {
                failures.Add($"retrieval-unexpected-case:{unknownCase}");
            }
        }

        return new LearningsValidationResult(failures.Count == 0, failures, words, nonblankLines);
    }
}
