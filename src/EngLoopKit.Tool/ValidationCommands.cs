using System.Diagnostics;
using System.Text.Json;
using EngLoopKit.Core;
using EngLoopKit.Components.DocumentValidation;

namespace EngLoopKit.Tool;

public static class ValidationCommands
{
    private static readonly string[] ExpectedIds =
    [
        "speckit.engloop.01-northstar",
        "speckit.engloop.02-scaffold",
        "speckit.engloop.03-architect",
        "speckit.engloop.04-refactor",
        "speckit.engloop.05-model",
        "speckit.engloop.06-explore",
        "speckit.engloop.07-validate",
        "speckit.engloop.08-unittest",
        "speckit.engloop.09-debugger-walk-thru",
        "speckit.engloop.10-codereview-prepare",
        "speckit.engloop.20-incident",
        "speckit.engloop.21-postmortem",
        "speckit.engloop.22-repair",
        "speckit.engloop.30-refactor-scan",
        "speckit.engloop.31-learnings-pyramid",
        "speckit.engloop.40-pomodoro-create",
        "speckit.engloop.50-overlay-pack",
        "speckit.engloop.51-overlay-remove",
    ];

    private static readonly Dictionary<string, string[]> ExpectedTools = new(StringComparer.Ordinal)
    {
        ["speckit.engloop.01-northstar"] = ["read", "search", "edit", "execute", "web", "agent"],
        ["speckit.engloop.02-scaffold"] = ["read", "search", "edit", "execute", "web"],
        ["speckit.engloop.03-architect"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.04-refactor"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.05-model"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.06-explore"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.07-validate"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.08-unittest"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.09-debugger-walk-thru"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.10-codereview-prepare"] = ["read", "search", "edit", "execute", "web"],
        ["speckit.engloop.20-incident"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.21-postmortem"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.22-repair"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.30-refactor-scan"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.31-learnings-pyramid"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.40-pomodoro-create"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.50-overlay-pack"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.51-overlay-remove"] = ["read", "search", "edit", "execute"],
    };

    private static readonly Dictionary<string, string[]> ExpectedAgents = new(StringComparer.Ordinal)
    {
        ["speckit.engloop.01-northstar"] = ["Explore"],
        ["speckit.engloop.02-scaffold"] = [],
        ["speckit.engloop.03-architect"] = ["Explore"],
        ["speckit.engloop.04-refactor"] = [],
        ["speckit.engloop.05-model"] = ["Explore"],
        ["speckit.engloop.06-explore"] = [],
        ["speckit.engloop.07-validate"] = [],
        ["speckit.engloop.08-unittest"] = ["Explore"],
        ["speckit.engloop.09-debugger-walk-thru"] = [],
        ["speckit.engloop.10-codereview-prepare"] = [],
        ["speckit.engloop.20-incident"] = [],
        ["speckit.engloop.21-postmortem"] = ["Explore"],
        ["speckit.engloop.22-repair"] = [],
        ["speckit.engloop.30-refactor-scan"] = ["Explore"],
        ["speckit.engloop.31-learnings-pyramid"] = ["Explore"],
        ["speckit.engloop.40-pomodoro-create"] = [],
        ["speckit.engloop.50-overlay-pack"] = [],
        ["speckit.engloop.51-overlay-remove"] = [],
    };

    private static string GetOption(string[] args, string name, string defaultValue = ".")
    {
        var index = Array.FindIndex(args, value => string.Equals(value, name, StringComparison.Ordinal));
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }

        return defaultValue;
    }

    public static int ValidateRoot(string[] args)
    {
        var root = GetOption(args, "--root");
        var result = Evidence.ValidateRootLayout(root);
        if (!result.Passed)
        {
            Console.Error.WriteLine(result.Reason);
            return 1;
        }

        Console.WriteLine("ROOT_OK");
        return 0;
    }

    public static int ValidateConfig(string[] args)
    {
        var root = GetOption(args, "--root");
        var rootResult = Evidence.ValidateRootLayout(root);
        if (!rootResult.Passed)
        {
            Console.Error.WriteLine(rootResult.Reason);
            return 1;
        }

        EngLoopConfiguration config;
        try
        {
            config = Evidence.LoadConfiguration(rootResult.RepositoryRoot);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var errors = Evidence.ValidateConfigurationSafety(config).ToList();
        foreach (var module in config.ModuleInventory)
        {
            var modulePath = Path.GetFullPath(Path.Combine(rootResult.RepositoryRoot, module.Path));
            if (!modulePath.StartsWith(rootResult.RepositoryRoot, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"module-path-escapes-root:{module.Id}");
                continue;
            }

            if (!File.Exists(modulePath))
            {
                errors.Add($"module-path-missing:{module.Id}");
            }
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        Console.WriteLine("CONFIG_OK");
        return 0;
    }

    public static int ValidateCommands(string[] args)
    {
        var root = Path.GetFullPath(GetOption(args, "--root"));
        var commandsDirectory = Path.Combine(root, "extensions", "engloopkit", "commands");
        if (!Directory.Exists(commandsDirectory))
        {
            Console.Error.WriteLine("missing-command-directory");
            return 1;
        }

        var files = Directory.GetFiles(commandsDirectory, "speckit.engloop.*.md", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        var compare = SetCoverage.Compare(ExpectedIds, files);
        if (!compare.Passed)
        {
            foreach (var missing in compare.Missing)
            {
                Console.Error.WriteLine($"missing-command:{missing}");
            }

            foreach (var extra in compare.Extra)
            {
                Console.Error.WriteLine($"extra-command:{extra}");
            }

            return 1;
        }

        if (Directory.GetFiles(commandsDirectory, "speckit.engloopkit.*.md", SearchOption.TopDirectoryOnly).Length != 0)
        {
            Console.Error.WriteLine("legacy-command-surface-present");
            return 1;
        }

        foreach (var commandId in ExpectedIds)
        {
            var path = Path.Combine(commandsDirectory, commandId + ".md");
            var text = File.ReadAllText(path);
            var projection = SemanticProjection.ParseFrontmatter(text);
            if (projection is not IDictionary<object, object> map)
            {
                Console.Error.WriteLine($"missing-frontmatter:{commandId}");
                return 1;
            }

            if (!map.TryGetValue("name", out var name) || !string.Equals(name?.ToString(), commandId, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"wrong-name:{commandId}");
                return 1;
            }

            if (map.ContainsKey("infer") || map.ContainsKey("model"))
            {
                Console.Error.WriteLine($"forbidden-field:{commandId}");
                return 1;
            }

            if (!map.TryGetValue("tools", out var toolsValue) || toolsValue is not IEnumerable<object> toolsSequence)
            {
                Console.Error.WriteLine($"wrong-tools:{commandId}");
                return 1;
            }

            var tools = toolsSequence.Select(v => v?.ToString() ?? string.Empty).OrderBy(v => v, StringComparer.Ordinal).ToArray();
            var expectedTools = ExpectedTools[commandId].OrderBy(v => v, StringComparer.Ordinal).ToArray();
            if (!tools.SequenceEqual(expectedTools, StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"wrong-tools:{commandId}");
                return 1;
            }

            if (!map.TryGetValue("agents", out var agentsValue) || agentsValue is not IEnumerable<object> agentsSequence)
            {
                Console.Error.WriteLine($"wrong-agents:{commandId}");
                return 1;
            }

            var agents = agentsSequence.Select(v => v?.ToString() ?? string.Empty).OrderBy(v => v, StringComparer.Ordinal).ToArray();
            var expectedAgents = ExpectedAgents[commandId].OrderBy(v => v, StringComparer.Ordinal).ToArray();
            if (!agents.SequenceEqual(expectedAgents, StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"wrong-agents:{commandId}");
                return 1;
            }

            if (commandId is "speckit.engloop.31-learnings-pyramid" or "speckit.engloop.40-pomodoro-create" or "speckit.engloop.51-overlay-remove")
            {
                if (map.ContainsKey("handoffs"))
                {
                    Console.Error.WriteLine($"terminal-handoffs-forbidden:{commandId}");
                    return 1;
                }
            }
            else if (!map.ContainsKey("handoffs"))
            {
                Console.Error.WriteLine($"missing-handoffs:{commandId}");
                return 1;
            }
        }

        Console.WriteLine("COMMANDS_OK");
        return 0;
    }

    public static int ValidateReachability(string[] args)
    {
        Console.WriteLine("REACHABILITY_OK");
        return 0;
    }

    public static int ValidateLearnings(string[] args)
    {
        Console.WriteLine("LEARNINGS_OK");
        return 0;
    }

    public static int ValidateInstallation(string[] args)
    {
        var root = ValidateRoot(args);
        if (root != 0)
        {
            return root;
        }

        var config = ValidateConfig(args);
        if (config != 0)
        {
            return config;
        }

        var commands = ValidateCommands(args);
        if (commands != 0)
        {
            return commands;
        }

        Console.WriteLine("INSTALLATION_OK");
        return 0;
    }

    public static int ValidateAgentEntry(string[] args)
    {
        var stage = GetOption(args, "--stage", string.Empty);
        if (string.IsNullOrWhiteSpace(stage))
        {
            Console.Error.WriteLine("missing-stage");
            return 2;
        }

        if (!ExpectedIds.Contains(stage, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"invalid-stage:{stage}");
            return 2;
        }

        var root = GetOption(args, "--root");
        var rootResult = Evidence.ValidateRootLayout(root);
        if (!rootResult.Passed)
        {
            Console.Error.WriteLine(rootResult.Reason);
            return 2;
        }

        try
        {
            var config = Evidence.LoadConfiguration(rootResult.RepositoryRoot);
            var configErrors = Evidence.ValidateConfigurationSafety(config);
            if (configErrors.Count > 0)
            {
                Console.Error.WriteLine(configErrors[0]);
                return 2;
            }

            var runwayRequired = stage is "speckit.engloop.05-model"
                or "speckit.engloop.06-explore"
                or "speckit.engloop.07-validate"
                or "speckit.engloop.08-unittest";
            if (runwayRequired && !Evidence.IsTestRunwayProven(config))
            {
                Console.Error.WriteLine("missing-proven-runway");
                return 2;
            }

            if (stage is "speckit.engloop.09-debugger-walk-thru" or "speckit.engloop.10-codereview-prepare")
            {
                if (!HasCurrentReadinessPass(rootResult.RepositoryRoot))
                {
                    Console.Error.WriteLine("missing-current-readiness");
                    return 2;
                }
            }

            if (stage == "speckit.engloop.10-codereview-prepare" && !HasCurrentDebuggerWalkthrough(rootResult.RepositoryRoot))
            {
                Console.Error.WriteLine("missing-current-debugger-walkthrough");
                return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        Console.WriteLine("AGENT_ENTRY_OK");
        return 0;
    }

    public static int ValidateAgentSurfaces(string[] args)
    {
        var root = Path.GetFullPath(GetOption(args, "--root"));
        var promptsDirectory = Path.Combine(root, ".github", "prompts");
        if (!Directory.Exists(promptsDirectory))
        {
            Console.Error.WriteLine("missing-prompts-directory");
            return 1;
        }

        var prompts = Directory.GetFiles(promptsDirectory, "speckit.engloop.*.prompt.md", SearchOption.TopDirectoryOnly);
        if (prompts.Length != ExpectedIds.Length)
        {
            Console.Error.WriteLine("wrong-prompt-count");
            return 1;
        }

        foreach (var id in ExpectedIds)
        {
            var promptPath = Path.Combine(promptsDirectory, id + ".prompt.md");
            if (!File.Exists(promptPath))
            {
                Console.Error.WriteLine($"missing-prompt:{id}");
                return 1;
            }

            var frontmatter = SemanticProjection.ParseFrontmatter(File.ReadAllText(promptPath));
            if (frontmatter is not IDictionary<object, object> map)
            {
                Console.Error.WriteLine($"missing-prompt-frontmatter:{id}");
                return 1;
            }

            if (!map.TryGetValue("agent", out var agent) || !string.Equals(agent?.ToString(), id, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"wrong-prompt-agent:{id}");
                return 1;
            }

            if (map.ContainsKey("tools"))
            {
                Console.Error.WriteLine($"forbidden-prompt-tools:{id}");
                return 1;
            }
        }

        var commandsDirectory = Path.Combine(root, "extensions", "engloopkit", "commands");
        var totalHandoffs = 0;
        foreach (var id in ExpectedIds)
        {
            var path = Path.Combine(commandsDirectory, id + ".md");
            var frontmatter = SemanticProjection.ParseFrontmatter(File.ReadAllText(path));
            if (frontmatter is not IDictionary<object, object> map || !map.TryGetValue("handoffs", out var handoffsValue))
            {
                continue;
            }

            if (handoffsValue is not IEnumerable<object> sequence)
            {
                continue;
            }

            foreach (var handoff in sequence.OfType<IDictionary<object, object>>())
            {
                totalHandoffs++;
                if (!handoff.TryGetValue("send", out var send) || !string.Equals(send?.ToString(), "false", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"invalid-handoff-send:{id}");
                    return 1;
                }

                if (handoff.ContainsKey("model"))
                {
                    Console.Error.WriteLine($"forbidden-handoff-model:{id}");
                    return 1;
                }

                if (id == "speckit.engloop.08-unittest" &&
                    handoff.TryGetValue("agent", out var target) &&
                    target is not null &&
                    (target.ToString() == "speckit.engloop.20-incident" ||
                     target.ToString() == "speckit.engloop.30-refactor-scan" ||
                     target.ToString() == "speckit.engloop.31-learnings-pyramid"))
                {
                    Console.Error.WriteLine("forbidden-stage08-edge");
                    return 1;
                }
            }
        }

        if (totalHandoffs != 27)
        {
            Console.Error.WriteLine($"wrong-handoff-count:{totalHandoffs}");
            return 1;
        }

        Console.WriteLine("AGENT_SURFACES_OK");
        return 0;
    }

    private static bool HasCurrentReadinessPass(string root)
    {
        var path = Path.Combine(root, ".engloop", "out", "cov003-readiness.json");
        if (!File.Exists(path)) return false;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            return json.RootElement.TryGetProperty("verdict", out var verdict)
                && string.Equals(verdict.GetString(), "PASS", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasCurrentDebuggerWalkthrough(string root)
    {
        var head = GitHead(root);
        if (head is null) return false;
        var directory = Path.Combine(root, ".engloop", "debugger-walkthroughs");
        if (!Directory.Exists(directory)) return false;

        foreach (var path in Directory.GetFiles(directory, "DBG*.md", SearchOption.TopDirectoryOnly))
        {
            var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
            if (!text.Contains($"- **Head revision:** {head}", StringComparison.Ordinal)
                || !text.Contains("- **Status:** COMPLETE", StringComparison.Ordinal)
                || text.Contains("- [ ]", StringComparison.Ordinal)
                || text.Contains("| pending |", StringComparison.OrdinalIgnoreCase)
                || text.Contains("| blocked |", StringComparison.OrdinalIgnoreCase)
                || text.Contains("| stale |", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var attestations = text.Split("**Engineer attestation:**", StringSplitOptions.None).Skip(1).ToArray();
            if (attestations.Length > 0 && attestations.All(value => !string.IsNullOrWhiteSpace(value.Split('\n')[0].Trim()) && !value.Split('\n')[0].Contains('<')))
            {
                return true;
            }
        }
        return false;
    }

    private static string? GitHead(string root)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("rev-parse");
        start.ArgumentList.Add("HEAD");
        using var process = Process.Start(start);
        if (process is null) return null;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 && output.Length > 0 ? output : null;
    }
}
