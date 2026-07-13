using System.Text.RegularExpressions;

namespace EngLoopKit.Components.DocumentValidation;

public static class MarkdownLinks
{
    private static readonly Regex LinkRegex = new(@"\[[^\]]+\]\(([^)]+)\)", RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtractLinks(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return [];
        }

        var links = new List<string>();
        foreach (Match match in LinkRegex.Matches(markdown))
        {
            links.Add(match.Groups[1].Value.Trim());
        }

        return links;
    }
}
