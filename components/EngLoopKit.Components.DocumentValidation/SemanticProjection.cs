using YamlDotNet.Serialization;
using YamlDotNet.Core;

namespace EngLoopKit.Components.DocumentValidation;

public static class SemanticProjection
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    public static object? ParseFrontmatter(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        var normalized = markdown.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        if (lines.Length < 3 || lines[0].Trim() != "---")
        {
            return null;
        }

        var end = Array.FindIndex(lines, 1, line => line.Trim() == "---");
        if (end < 0)
        {
            return null;
        }

        var yaml = string.Join("\n", lines[1..end]);
        try
        {
            return Deserializer.Deserialize<object>(yaml);
        }
        catch (YamlException)
        {
            // Validation callers treat a null projection as malformed frontmatter and
            // reject the artifact deterministically; never leak a parser exception into
            // an agent/tool execution path.
            return null;
        }
    }

    public static object? Canonicalize(object? value)
    {
        return value switch
        {
            null => null,
            IDictionary<object, object> map => map
                .OrderBy(k => k.Key?.ToString(), StringComparer.Ordinal)
                .ToDictionary(k => k.Key?.ToString() ?? string.Empty, k => Canonicalize(k.Value), StringComparer.Ordinal),
            IEnumerable<object> sequence when value is not string => sequence.Select(Canonicalize).ToArray(),
            _ => value
        };
    }
}
