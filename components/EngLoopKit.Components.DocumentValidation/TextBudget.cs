using System.Text.RegularExpressions;

namespace EngLoopKit.Components.DocumentValidation;

public static class TextBudget
{
    private static readonly Regex WordRegex = new(@"[\p{L}\p{N}]+(?:['’_-][\p{L}\p{N}]+)*", RegexOptions.Compiled);

    public static (int WordCount, int NonblankLineCount) Count(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return (0, 0);
        }

        var normalized = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n');
        var nonblank = lines.Count(line => !string.IsNullOrWhiteSpace(line));
        var words = WordRegex.Matches(markdown).Count;
        return (words, nonblank);
    }

    public static bool IsWithinBudget(string markdown, int maxWords = 500, int maxNonblankLines = 60)
    {
        var (words, lines) = Count(markdown);
        return words <= maxWords && lines <= maxNonblankLines;
    }
}
