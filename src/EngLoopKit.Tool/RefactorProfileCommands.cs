using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EngLoopKit.Tool;

public static class RefactorProfileCommands
{
    private sealed record ProfileGate(
        string SchemaVersion,
        string SessionHash,
        string Head,
        string Profile,
        string ProfileSource,
        string Scope);

    public static int Execute(string[] args)
    {
        try
        {
            Ensure(args.Length == 1, "refactor-profile-requires-action");
            using var input = JsonDocument.Parse(Console.In.ReadToEnd());
            var root = ExactGitRoot(ReadString(input.RootElement, "cwd"));
            var sessionId = ReadString(input.RootElement, "session_id", "sessionId");
            Ensure(!string.IsNullOrWhiteSpace(sessionId), "refactor-profile-session-id-missing");
            var sessionHash = Sha256(Encoding.UTF8.GetBytes(sessionId));
            var path = GatePath(root, sessionHash);

            return args[0] switch
            {
                "bind" => Bind(root, path, sessionHash, ReadString(input.RootElement, "prompt")),
                "clear" => Clear(path),
                _ => throw new InvalidOperationException("refactor-profile-action-invalid"),
            };
        }
        catch (Exception ex)
        {
            WriteResult(false, "Refactor profile failed closed: " + ex.Message);
            return 0;
        }
    }

    private static int Bind(string root, string path, string sessionHash, string prompt)
    {
        if (File.Exists(path))
        {
            var existing = ReadGate(path);
            Ensure(existing.SchemaVersion == "1.0" & existing.SessionHash == sessionHash, "refactor-profile-gate-identity-invalid");
            Ensure(existing.Head == GitHead(root), "refactor-profile-head-changed");
            var supplied = ParseOptional(prompt);
            Ensure(supplied is null || (supplied.Profile == existing.Profile && supplied.Scope == existing.Scope), "refactor-profile-scope-or-profile-changed");
            WriteActive(existing);
            return 0;
        }

        var selected = ParseRequired(prompt);
        var head = GitHead(root) ?? throw new InvalidOperationException("refactor-profile-git-head-unavailable");
        var gate = selected with { SchemaVersion = "1.0", SessionHash = sessionHash, Head = head };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(gate));
        WriteActive(gate);
        return 0;
    }

    private static int Clear(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        WriteResult(true, systemMessage: "REFACTOR_PROFILE_CLEARED");
        return 0;
    }

    private static ProfileGate ParseRequired(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new InvalidOperationException("refactor-profile-prompt-missing");

        return ParsePresent(prompt);
    }

    private static ProfileGate? ParseOptional(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return null;

        var hasScope = ContainsOption(prompt, "--scope");
        var hasProfile = ContainsOption(prompt, "--profile");
        if (!hasScope && !hasProfile) return null;

        return ParsePresent(prompt);
    }

    private static ProfileGate ParsePresent(string prompt)
    {
        var hasProfile = ContainsOption(prompt, "--profile");

        Ensure(OptionCount(prompt, "--scope") == 1, "refactor-profile-scope-option-count-invalid");
        Ensure(OptionCount(prompt, "--profile") <= 1, "refactor-profile-profile-option-count-invalid");

        var scope = Argument(prompt, "--scope");
        Ensure(scope.Length is >= 1 and <= 256, "refactor-profile-scope-length-invalid");
        Ensure(!scope.Contains('<') & !scope.Contains('>') & !scope.StartsWith("--", StringComparison.Ordinal), "refactor-profile-scope-invalid");

        var profileValue = hasProfile ? Argument(prompt, "--profile").ToLowerInvariant() : "point";
        Ensure(profileValue is "point" or "bounded" or "deep", "refactor-profile-value-invalid");
        var repositoryWide = scope is "." or "*"
            || scope.Equals("all", StringComparison.OrdinalIgnoreCase)
            || scope.Equals("repository", StringComparison.OrdinalIgnoreCase);
        Ensure(!repositoryWide || profileValue == "deep", "refactor-profile-repository-scope-requires-deep");
        var source = hasProfile ? "EXPLICIT" : "DEFAULT-POINT";
        return new ProfileGate("", "", "", profileValue.ToUpperInvariant(), source, scope);
    }

    private static bool ContainsOption(string prompt, string name)
        => Regex.IsMatch(prompt, "(?:^|\\s)" + Regex.Escape(name) + "(?:=|\\s|$)", RegexOptions.CultureInvariant);

    private static int OptionCount(string prompt, string name)
        => Regex.Matches(prompt, "(?:^|\\s)" + Regex.Escape(name) + "(?:=|\\s|$)", RegexOptions.CultureInvariant).Count;

    private static string Argument(string prompt, string name)
    {
        var match = Regex.Match(
            prompt,
            "(?:^|\\s)" + Regex.Escape(name) + "(?:=|\\s+)(?:\\\"(?<dq>[^\\\"]+)\\\"|'(?<sq>[^']+)'|(?<bare>[^\\s]+))",
            RegexOptions.CultureInvariant);
        Ensure(match.Success, "refactor-profile-option-missing:" + name);
        foreach (var group in new[] { "dq", "sq", "bare" })
            if (match.Groups[group].Success) return match.Groups[group].Value.Trim();
        throw new InvalidOperationException("refactor-profile-option-missing:" + name);
    }

    private static string GatePath(string root, string sessionHash)
    {
        var ignored = RunGit(root, "check-ignore", "-q", "--no-index", "--", ".engloop/out/refactor-profile-gates/.probe");
        Ensure(ignored.ExitCode == 0, "refactor-profile-gate-root-not-ignored");
        return Path.Combine(root, ".engloop", "out", "refactor-profile-gates", sessionHash + ".json");
    }

    private static ProfileGate ReadGate(string path)
        => JsonSerializer.Deserialize<ProfileGate>(File.ReadAllText(path))
           ?? throw new InvalidOperationException("refactor-profile-gate-json-invalid");

    private static string ExactGitRoot(string cwd)
    {
        Ensure(!string.IsNullOrWhiteSpace(cwd), "refactor-profile-cwd-missing");
        var selected = Path.GetFullPath(cwd).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var result = RunGit(selected, "rev-parse", "--show-toplevel");
        Ensure(result.ExitCode == 0, "refactor-profile-git-root-unavailable");
        var root = Path.GetFullPath(result.Output.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Ensure(string.Equals(root, selected, StringComparison.OrdinalIgnoreCase), "refactor-profile-cwd-not-exact-git-root");
        return root;
    }

    private static string? GitHead(string root)
    {
        var result = RunGit(root, "rev-parse", "HEAD");
        return result.ExitCode == 0 && result.Output.Trim().Length > 0 ? result.Output.Trim() : null;
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static void WriteActive(ProfileGate gate)
        => WriteResult(true, systemMessage:
            $"REFACTOR_PROFILE_ACTIVE profile={gate.Profile} source={gate.ProfileSource} scope={gate.Scope} " +
            "modelMetadata=UNAVAILABLE-NOT-INFERRED; obey the matching investigation, subagent, and implementation envelope");

    private static void WriteResult(bool continueValue, string reason = "", string systemMessage = "")
        => Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = continueValue,
            stopReason = reason.Length == 0 ? null : reason,
            systemMessage = systemMessage.Length == 0 ? null : systemMessage,
        }));

    private static string Sha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static (int ExitCode, string Output) RunGit(string root, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git-start-failed");
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
