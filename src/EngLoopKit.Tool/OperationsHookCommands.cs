using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EngLoopKit.Core;

namespace EngLoopKit.Tool;

public static class OperationsHookCommands
{
    private sealed record GateRecord(
        string SchemaVersion,
        string Mode,
        string SessionHash,
        string Head,
        string ArgumentsHash,
        string AssemblyPath,
        string AssemblySha256,
        string ToolVersion,
        string ManifestPath,
        string ManifestSha256,
        string? Incident,
        string? Postmortem,
        string[]? Incidents,
        string? Phase,
        string? Rpi,
        string[]? Rules,
        string? Acceptance);

    public static int Execute(string[] args)
    {
        var action = args.Length > 0 ? args[0] : "unknown";
        var mode = args.Length > 1 ? args[1] : "unknown";
        try
        {
            Ensure(args.Length >= 2, "operations-hook-requires-action-and-mode");
            Ensure(mode is "incident" or "postmortem" or "repair", "operations-hook-mode-invalid");
            using var input = JsonDocument.Parse(Console.In.ReadToEnd());
            var root = ExactGitRoot(ReadString(input.RootElement, "cwd"));
            var sessionId = ReadString(input.RootElement, "session_id", "sessionId");
            Ensure(!string.IsNullOrWhiteSpace(sessionId), "operations-hook-session-id-missing");
            var sessionHash = Sha256(Encoding.UTF8.GetBytes(sessionId));
            var gatePath = GatePath(root, sessionHash, mode);

            return action switch
            {
                "start" => Start(mode, sessionHash),
                "initialize" => Initialize(root, mode, sessionHash, gatePath, ReadString(input.RootElement, "prompt")),
                "stop" => Stop(root, mode, sessionHash, gatePath),
                _ => throw new InvalidOperationException("operations-hook-action-invalid"),
            };
        }
        catch (Exception ex)
        {
            if (mode == "incident") WriteIncidentContextDeferred(action, IncidentDiagnosticCode(ex), ex.Message);
            else WriteResult(false, "Operations learning hook failed closed: " + ex.Message);
            return 0;
        }
    }

    private static int Start(string mode, string sessionHash)
    {
        WriteResult(true, systemMessage: $"OPERATIONS_LEARNING_GUARD_ACTIVE mode={mode} session={sessionHash[..12]}");
        return 0;
    }

    private static int Initialize(string root, string mode, string sessionHash, string gatePath, string prompt)
    {
        if (File.Exists(gatePath))
        {
            var existing = ReadGate(gatePath);
            Ensure(existing.SchemaVersion == "1.0" & existing.Mode == mode & existing.SessionHash == sessionHash & existing.Head == OperationsLearningPolicy.GitHead(root), "operations-hook-existing-gate-stale");
            ValidateToolIdentity(root, existing);
            Ensure(HashArguments(existing) == existing.ArgumentsHash, "operations-hook-gate-arguments-tampered");
            var suppliedArguments = ParseArguments(prompt, mode, allowMissing: true);
            Ensure(suppliedArguments is null || HashArguments(suppliedArguments) == existing.ArgumentsHash, "operations-hook-followup-arguments-changed");
            WriteResult(true, systemMessage: $"OPERATIONS_LEARNING_SCOPE_ACTIVE mode={mode} gate={gatePath}");
            return 0;
        }

        var arguments = ParseArguments(prompt, mode, allowMissing: false)!;
        var head = OperationsLearningPolicy.GitHead(root) ?? throw new InvalidOperationException("operations-hook-git-head-unavailable");
        if (mode == "postmortem") RequireCreateNewPostmortem(root, arguments.Postmortem!);
        if (mode == "repair") RequireCreateNewAcceptance(root, arguments.Acceptance!, arguments.Phase!);
        var identity = CurrentToolIdentity(root);
        var gate = arguments with
        {
            SchemaVersion = "1.0", Mode = mode, SessionHash = sessionHash, Head = head, ArgumentsHash = HashArguments(arguments),
            AssemblyPath = identity.AssemblyPath, AssemblySha256 = identity.AssemblySha256, ToolVersion = identity.ToolVersion,
            ManifestPath = identity.ManifestPath, ManifestSha256 = identity.ManifestSha256
        };
        Directory.CreateDirectory(Path.GetDirectoryName(gatePath)!);
        File.WriteAllText(gatePath, JsonSerializer.Serialize(gate));
        WriteResult(true, systemMessage: $"OPERATIONS_LEARNING_SCOPE_ACTIVE mode={mode} gate={gatePath}");
        return 0;
    }

    private static int Stop(string root, string mode, string sessionHash, string gatePath)
    {
        Ensure(File.Exists(gatePath), "operations-hook-gate-missing");
        var gate = ReadGate(gatePath);
        Ensure(gate.SchemaVersion == "1.0" & gate.Mode == mode & gate.SessionHash == sessionHash, "operations-hook-gate-identity-invalid");
        Ensure(gate.Head == OperationsLearningPolicy.GitHead(root), "operations-hook-head-changed");
        ValidateToolIdentity(root, gate);
        Ensure(HashArguments(gate) == gate.ArgumentsHash, "operations-hook-gate-arguments-tampered");
        int result;
        string marker;
        (result, var diagnostic) = CaptureValidation(() => mode switch
        {
            "incident" => ValidationCommands.ValidateIncidentContext(["--root", root, "--incident", gate.Incident!, "--allow-deferred", "true"]),
            "postmortem" => ValidationCommands.ValidatePostmortemLearning(["--root", root, "--incidents", string.Join(',', gate.Incidents!), "--postmortem", gate.Postmortem!]),
            _ => ValidationCommands.ValidateRepairLearning(["--root", root, "--phase", gate.Phase!, "--postmortem", gate.Postmortem!, "--rpi", gate.Rpi!, "--rules", string.Join(',', gate.Rules!), "--acceptance", gate.Acceptance!]),
        });
        marker = mode == "incident" ? "INCIDENT_CONTEXT_OK" : mode == "postmortem" ? "POSTMORTEM_LEARNING_OK" : "REPAIR_LEARNING_OK";
        if (result != 0)
        {
            if (mode == "incident") WriteIncidentContextDeferred("stop", "operations-hook-incident-context-validation-failed", diagnostic);
            else WriteResult(false, $"Operations learning validation failed for mode={mode}: {Bound(diagnostic)}");
            return 0;
        }
        File.Delete(gatePath);
        WriteResult(true, systemMessage: marker);
        return 0;
    }

    private static (int ExitCode, string Diagnostic) CaptureValidation(Func<int> action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var result = action();
            return (result, (output + Environment.NewLine + error).Trim());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static string Bound(string value)
    {
        if (value.Length <= 4096) return value;
        return value[..4096] + "...[truncated]";
    }

    private static string IncidentDiagnosticCode(Exception exception)
        => exception switch
        {
            JsonException => "operations-hook-json-invalid",
            IOException or UnauthorizedAccessException => "operations-hook-storage-unavailable",
            InvalidOperationException when exception.Message.StartsWith("operations-hook-", StringComparison.Ordinal) => Bound(exception.Message),
            InvalidOperationException => "operations-hook-invalid-state",
            _ => "operations-hook-unexpected-failure",
        };

    private static void WriteIncidentContextDeferred(string phase, string diagnosticCode, string diagnostic)
    {
        var missingOption = MissingOption(diagnosticCode + ":" + diagnostic);
        var expectedSource = phase switch
        {
            "start" => "SessionStart hook input fields cwd and session_id",
            "initialize" => "UserPromptSubmit.prompt containing --incident <.engloop/incidents/INxxx_title.md>",
            "stop" => "A validated incident gate created from UserPromptSubmit.prompt",
            _ => "A recognized incident lifecycle action with documented hook input",
        };
        var details = JsonSerializer.Serialize(new
        {
            status = "learning-context-deferred",
            mode = "incident",
            phase,
            command = $"operations-hook {phase} incident",
            diagnosticCode,
            diagnostic = Bound(diagnostic),
            missingOption,
            expectedSource,
            elkVersion = typeof(OperationsHookCommands).Assembly.GetName().Version?.ToString(3) ?? "unknown",
            remediation = "Continue incident mitigation. Resolve or create the incident artifact, then run validate incident-context before claiming stabilization; deferred context is not validated context.",
        });
        WriteResult(true, systemMessage: "OPERATIONS_LEARNING_CONTEXT_DEFERRED " + details);
    }

    private static string? MissingOption(string diagnostic)
    {
        var match = Regex.Match(diagnostic, @"operations-hook-option-missing:(?<option>--[a-z-]+)", RegexOptions.CultureInvariant);
        if (match.Success) return match.Groups["option"].Value;
        return diagnostic.Contains("operations-hook-prompt-missing", StringComparison.Ordinal) ? "--incident" : null;
    }

    private static GateRecord? ParseArguments(string prompt, string mode, bool allowMissing)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return allowMissing ? null : throw new InvalidOperationException("operations-hook-prompt-missing");
        if (allowMissing && !prompt.Contains("--", StringComparison.Ordinal)) return null;
        string Get(string name) => Argument(prompt, name);
        if (mode == "incident")
        {
            var incident = GovernedPath(Get("--incident"), ".engloop/incidents/");
            return New(incident: incident);
        }
        var postmortem = GovernedPath(Get("--postmortem"), ".engloop/postmortems/");
        if (mode == "postmortem")
        {
            var incidents = IdList(Get("--incidents"), @"^IN\d{3}$", "incident");
            return New(postmortem: postmortem, incidents: incidents);
        }
        var phase = Get("--phase").ToLowerInvariant();
        Ensure(phase is "route" or "close", "repair-phase-invalid");
        var rpi = Get("--rpi").ToUpperInvariant();
        Ensure(Regex.IsMatch(rpi, @"^RPI\d{3}$"), "repair-rpi-invalid");
        var rules = IdList(Get("--rules"), @"^RULE:[a-z0-9]+(?:-[a-z0-9]+)*$", "rule");
        var acceptance = GovernedPath(Get("--acceptance"), ".engloop/repairs/");
        return New(postmortem: postmortem, phase: phase, rpi: rpi, rules: rules, acceptance: acceptance);

        static GateRecord New(string? incident = null, string? postmortem = null, string[]? incidents = null, string? phase = null, string? rpi = null, string[]? rules = null, string? acceptance = null)
            => new("", "", "", "", "", "", "", "", "", "", incident, postmortem, incidents, phase, rpi, rules, acceptance);
    }

    private static void RequireCreateNewPostmortem(string root, string relative)
    {
        var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Ensure(!File.Exists(full), "postmortem-target-already-exists");
        var id = Regex.Match(Path.GetFileName(relative), @"^PM\d{3}").Value;
        Ensure(id.Length > 0, "postmortem-filename-invalid");
        Ensure(Directory.GetFiles(Path.Combine(root, ".engloop", "postmortems"), id + "*.md").Length == 0, "postmortem-number-already-used");
        var history = RunGit(root, "log", "--all", "--name-only", "--pretty=format:", "--", ".engloop/postmortems/" + id + "*");
        Ensure(history.ExitCode == 0 & string.IsNullOrWhiteSpace(history.Output), "postmortem-number-present-in-history");
    }

    private static void RequireCreateNewAcceptance(string root, string relative, string phase)
    {
        var suffix = phase == "route" ? ".route.json" : ".close.json";
        Ensure(relative.EndsWith(suffix, StringComparison.Ordinal), "repair-acceptance-phase-filename-mismatch");
        Ensure(!File.Exists(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))), "repair-acceptance-target-already-exists");
        var history = RunGit(root, "log", "--all", "--name-only", "--pretty=format:", "--", relative);
        Ensure(history.ExitCode == 0 & string.IsNullOrWhiteSpace(history.Output), "repair-acceptance-path-present-in-history");
    }

    private static string GatePath(string root, string sessionHash, string mode)
    {
        var directory = Path.Combine(root, ".engloop", "out", "operations-learning-gates");
        var ignored = RunGit(root, "check-ignore", "-q", "--no-index", "--", ".engloop/out/operations-learning-gates/.probe");
        Ensure(ignored.ExitCode == 0, "operations-hook-gate-root-not-ignored");
        return Path.Combine(directory, sessionHash + "." + mode + ".json");
    }

    private static GateRecord ReadGate(string path)
        => JsonSerializer.Deserialize<GateRecord>(File.ReadAllText(path)) ?? throw new InvalidOperationException("operations-hook-gate-json-invalid");

    private static string HashArguments(GateRecord value)
        => Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { value.Incident, value.Postmortem, value.Incidents, value.Phase, value.Rpi, value.Rules, value.Acceptance })));

    private static (string AssemblyPath, string AssemblySha256, string ToolVersion, string ManifestPath, string ManifestSha256) CurrentToolIdentity(string root)
    {
        var assemblyPath = Path.GetFullPath(typeof(OperationsHookCommands).Assembly.Location);
        var manifestPath = Path.Combine(root, ".config", "dotnet-tools.json");
        Ensure(File.Exists(manifestPath), "operations-hook-tool-manifest-missing");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var version = manifest.RootElement.GetProperty("tools").GetProperty("engloopkit").GetProperty("version").GetString() ?? string.Empty;
        Ensure(version.Length > 0, "operations-hook-tool-version-missing");
        var assemblyVersion = typeof(OperationsHookCommands).Assembly.GetName().Version?.ToString(3) ?? string.Empty;
        Ensure(string.Equals(version, assemblyVersion, StringComparison.Ordinal), "operations-hook-manifest-assembly-version-mismatch");
        return (assemblyPath, FileSha256(assemblyPath), version, manifestPath, FileSha256(manifestPath));
    }

    private static void ValidateToolIdentity(string root, GateRecord gate)
    {
        var current = CurrentToolIdentity(root);
        Ensure(gate.AssemblyPath == current.AssemblyPath & gate.AssemblySha256 == current.AssemblySha256 & gate.ToolVersion == current.ToolVersion
            & gate.ManifestPath == current.ManifestPath & gate.ManifestSha256 == current.ManifestSha256, "operations-hook-tool-identity-changed");
    }

    private static string Argument(string prompt, string name)
    {
        var match = Regex.Match(prompt, "(?:^|\\s)" + Regex.Escape(name) + "(?:=|\\s+)(?:\\\"(?<dq>[^\\\"]+)\\\"|'(?<sq>[^']+)'|(?<bare>[^\\s]+))");
        Ensure(match.Success, "operations-hook-option-missing:" + name);
        foreach (var group in new[] { "dq", "sq", "bare" }) if (match.Groups[group].Success) return match.Groups[group].Value;
        throw new InvalidOperationException("operations-hook-option-missing:" + name);
    }

    private static string GovernedPath(string value, string prefix)
    {
        var normalized = value.Trim().Replace('\\', '/');
        Ensure(!Path.IsPathRooted(normalized) & !normalized.Contains("../", StringComparison.Ordinal) & normalized.StartsWith(prefix, StringComparison.Ordinal), "operations-hook-path-invalid");
        return normalized;
    }

    private static string[] IdList(string value, string pattern, string identity)
    {
        var ids = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Ensure(ids.Length > 0 & ids.Length == ids.Distinct(StringComparer.Ordinal).Count() & !ids.Any(id => !Regex.IsMatch(id, pattern)), "operations-hook-" + identity + "-ids-invalid");
        return ids.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    private static string ExactGitRoot(string cwd)
    {
        Ensure(!string.IsNullOrWhiteSpace(cwd), "operations-hook-cwd-missing");
        var selected = Path.GetFullPath(cwd).TrimEnd(Path.DirectorySeparatorChar);
        var result = RunGit(selected, "rev-parse", "--show-toplevel");
        Ensure(result.ExitCode == 0, "operations-hook-git-root-unavailable");
        var root = Path.GetFullPath(result.Output.Trim()).TrimEnd(Path.DirectorySeparatorChar);
        Ensure(string.Equals(root, selected, StringComparison.OrdinalIgnoreCase), "operations-hook-cwd-not-exact-git-root");
        return root;
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names) if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static void WriteResult(bool continueValue, string reason = "", string systemMessage = "")
    {
        Console.WriteLine(JsonSerializer.Serialize(new { @continue = continueValue, stopReason = reason.Length == 0 ? null : reason, systemMessage = systemMessage.Length == 0 ? null : systemMessage }));
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string FileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static (int ExitCode, string Output) RunGit(string root, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git-start-failed");
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
