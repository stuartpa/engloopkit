using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EngLoopKit.Core;

namespace EngLoopKit.Tool;

public static class OperationsHookCommands
{
    private static readonly HashSet<string> CollectionReadOrQuestionTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "search", "read_file", "file_search", "grep_search", "semantic_search", "list_dir", "vscode_askQuestions",
    };

    private static readonly HashSet<string> CollectionCommandTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "execute", "run_in_terminal",
    };

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

    private sealed record PostmortemCollectionRecord(
        string SchemaVersion,
        string SessionHash,
        string Head,
        string AssemblyPath,
        string AssemblySha256,
        string ToolVersion,
        string ManifestPath,
        string ManifestSha256,
        string Token);

    private sealed record PostmortemConfirmationRecord(
        string SchemaVersion,
        string SessionHash,
        string Head,
        string AssemblySha256,
        string ManifestSha256,
        string CollectionToken,
        string ToolUseId,
        string[] Incidents,
        string Postmortem,
        string QuestionSha256,
        string ResponseSha256);

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
            if (mode == "postmortem") RequirePostmortemAgentEntry(root);
            var sessionId = ReadString(input.RootElement, "session_id", "sessionId");
            Ensure(!string.IsNullOrWhiteSpace(sessionId), "operations-hook-session-id-missing");
            var sessionHash = Sha256(Encoding.UTF8.GetBytes(sessionId));
            var gatePath = GatePath(root, sessionHash, mode);

            return action switch
            {
                "start" => Start(mode, sessionHash),
                "subagent-start" => SubagentStart(mode, sessionHash),
                "initialize" => Initialize(root, mode, sessionHash, gatePath, ReadString(input.RootElement, "prompt")),
                "guard" => Guard(root, mode, sessionHash, gatePath, input.RootElement),
                "post-tool" => PostTool(root, mode, sessionHash, input.RootElement),
                "stop" => Stop(root, mode, sessionHash, gatePath, subagent: false),
                "subagent-stop" => Stop(root, mode, sessionHash, gatePath, subagent: true),
                _ => throw new InvalidOperationException("operations-hook-action-invalid"),
            };
        }
        catch (Exception ex)
        {
            if (mode == "incident") WriteIncidentContextDeferred(action, IncidentDiagnosticCode(ex), ex.Message);
            else if (mode == "postmortem" && action == "subagent-stop") WriteSubagentStopBlocked(ex.Message);
            else if (mode == "postmortem" && action == "post-tool") WriteResult(true, systemMessage: "OPERATIONS_POSTMORTEM_CONFIRMATION_REJECTED completionAccepted=false diagnostic=" + IncidentDiagnosticCode(ex));
            else WriteResult(false, "Operations learning hook failed closed: " + ex.Message);
            return 0;
        }
    }

    public static int ExecutePostmortemRoute(string[] args)
    {
        if (args.Length == 0 || args[0] != "bind")
        {
            Console.Error.WriteLine("Usage: engloopkit postmortem-route bind --collection <path> --token <token> --incidents <INxxx,...> --postmortem <path> --confirmation-receipt <path>");
            return 1;
        }

        try
        {
            var root = ExactGitRoot(Environment.CurrentDirectory);
            RequirePostmortemAgentEntry(root);
            var collectionRelative = GovernedPath(CliOption(args, "--collection"), ".engloop/out/postmortem-context/");
            var collectionPath = Path.Combine(root, collectionRelative.Replace('/', Path.DirectorySeparatorChar));
            Ensure(File.Exists(collectionPath), "postmortem-route-collection-missing");
            var collection = ReadCollection(collectionPath);
            ValidateCollection(root, collection);
            Ensure(collectionRelative == CollectionRelativePath(collection.SessionHash), "postmortem-route-collection-path-mismatch");
            Ensure(string.Equals(CliOption(args, "--token"), collection.Token, StringComparison.Ordinal), "postmortem-route-token-mismatch");

            var incidents = IdList(CliOption(args, "--incidents"), @"^IN\d{3}$", "incident");
            foreach (var incident in incidents)
            {
                var matches = Directory.GetFiles(Path.Combine(root, ".engloop", "incidents"), incident + "*.md")
                    .Where(path => Regex.IsMatch(Path.GetFileName(path), "^" + Regex.Escape(incident) + @"(?:[_-].+)?\.md$", RegexOptions.CultureInvariant))
                    .ToArray();
                Ensure(matches.Length == 1, "postmortem-route-incident-not-unique:" + incident);
                var relative = Path.GetRelativePath(root, matches[0]).Replace('\\', '/');
                var validation = OperationsLearningPolicy.ValidateIncidentContext(root, relative, requireConsulted: true);
                Ensure(validation.Passed, "postmortem-route-incident-not-ready:" + incident + ":" + string.Join(',', validation.Failures));
            }

            var postmortem = GovernedPath(CliOption(args, "--postmortem"), ".engloop/postmortems/");
            var confirmationRelative = GovernedPath(CliOption(args, "--confirmation-receipt"), ".engloop/out/postmortem-context/");
            var confirmationPath = Path.Combine(root, confirmationRelative.Replace('/', Path.DirectorySeparatorChar));
            Ensure(confirmationRelative == ConfirmationRelativePath(collection.SessionHash), "postmortem-route-confirmation-path-mismatch");
            Ensure(File.Exists(confirmationPath), "postmortem-route-confirmation-missing");
            var confirmation = ReadConfirmation(confirmationPath);
            ValidateConfirmation(root, collection, confirmation, incidents, postmortem);
            var nextPm = NextRegistryId(root, "PM");
            var proposedId = Regex.Match(Path.GetFileName(postmortem), @"^PM\d{3}", RegexOptions.CultureInvariant).Value;
            Ensure(proposedId == nextPm, $"postmortem-route-next-id-mismatch:expected={nextPm}:actual={proposedId}");
            Ensure(Regex.IsMatch(Path.GetFileName(postmortem), @"^PM\d{3}[_-][A-Za-z0-9][A-Za-z0-9._-]*\.md$", RegexOptions.CultureInvariant), "postmortem-route-filename-invalid");
            RequireCreateNewPostmortem(root, postmortem);

            var gatePath = GatePath(root, collection.SessionHash, "postmortem");
            Ensure(!File.Exists(gatePath), "postmortem-route-gate-already-exists");
            var arguments = NewArguments(postmortem: postmortem, incidents: incidents);
            WriteGate(root, "postmortem", collection.SessionHash, gatePath, arguments);
            File.Delete(collectionPath);
            File.Delete(confirmationPath);
            Console.WriteLine($"POSTMORTEM_ROUTE_BOUND OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem gate={gatePath} incidents={string.Join(',', incidents)} postmortem={postmortem}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Start(string mode, string sessionHash)
    {
        WriteResult(true, systemMessage: $"OPERATIONS_LEARNING_GUARD_ACTIVE mode={mode} session={sessionHash[..12]}");
        return 0;
    }

    private static int SubagentStart(string mode, string sessionHash)
    {
        var context = $"OPERATIONS_LEARNING_GUARD_ACTIVE mode={mode} session={sessionHash[..12]}";
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            stopReason = (string?)null,
            systemMessage = context,
            hookSpecificOutput = new { hookEventName = "SubagentStart", additionalContext = context },
        }));
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
            GateRecord? suppliedArguments;
            try
            {
                suppliedArguments = ParseArguments(prompt, mode, allowMissing: true);
            }
            catch (InvalidOperationException ex) when (mode == "postmortem" && IsPostmortemContextDiagnostic(ex.Message))
            {
                SuspendPostmortemContext(gatePath, ex.Message);
                WritePostmortemContextRequired("initialize", ex.Message);
                return 0;
            }
            if (mode == "postmortem" && suppliedArguments is null && TryReadPostmortemContext(gatePath, out var diagnostic))
            {
                WritePostmortemContextRequired("initialize", diagnostic);
                return 0;
            }
            Ensure(suppliedArguments is null || HashArguments(suppliedArguments) == existing.ArgumentsHash, "operations-hook-followup-arguments-changed");
            if (mode == "postmortem") ClearPostmortemContext(gatePath);
            WriteResult(true, systemMessage: $"OPERATIONS_LEARNING_SCOPE_ACTIVE mode={mode} gate={gatePath}");
            return 0;
        }

        GateRecord arguments;
        try
        {
            arguments = ParseArguments(prompt, mode, allowMissing: false)!;
        }
        catch (InvalidOperationException ex) when (mode == "postmortem" && IsPostmortemContextDiagnostic(ex.Message))
        {
            BeginPostmortemCollection(root, sessionHash, ex.Message);
            return 0;
        }
        if (mode == "postmortem") RequireCreateNewPostmortem(root, arguments.Postmortem!);
        if (mode == "repair") RequireCreateNewAcceptance(root, arguments.Acceptance!, arguments.Phase!);
        WriteGate(root, mode, sessionHash, gatePath, arguments);
        if (mode == "postmortem") ClearPostmortemCollection(root, sessionHash);
        if (mode == "postmortem") ClearPostmortemContext(gatePath);
        WriteResult(true, systemMessage: $"OPERATIONS_LEARNING_SCOPE_ACTIVE mode={mode} gate={gatePath}");
        return 0;
    }

    private static int Guard(string root, string mode, string sessionHash, string gatePath, JsonElement input)
    {
        Ensure(mode == "postmortem", "operations-hook-guard-mode-invalid");
        var collectionPath = CollectionPath(root, sessionHash);
        if (File.Exists(collectionPath))
        {
            try
            {
                var collection = ReadCollection(collectionPath);
                ValidateCollection(root, collection);
                var toolName = ReadString(input, "tool_name", "toolName");
                var command = ReadToolCommand(input);
                var allow = IsCollectionReadOrQuestionTool(toolName)
                    || IsCollectionRouteCommand(toolName, command, collection, CollectionRelativePath(sessionHash));
                WritePostmortemCollectionDecision(allow, allow
                    ? "Stage 21 context collection permits this read, search, question, or exact trusted bind operation."
                    : "Stage 21 context collection is read-only. Inspect incident/registry evidence, ask one concise confirmation, then use only the exact trusted binder.");
            }
            catch (Exception ex)
            {
                WritePostmortemCollectionDecision(false, "Stage 21 collection state is invalid: " + IncidentDiagnosticCode(ex));
            }
            return 0;
        }
        try
        {
            if (TryReadPostmortemContext(gatePath, out var diagnostic))
            {
                WritePostmortemPreToolDenied(diagnostic, diagnostic);
                return 0;
            }
            Ensure(File.Exists(gatePath), "operations-hook-gate-missing");
            var gate = ReadGate(gatePath);
            Ensure(gate.SchemaVersion == "1.0" & gate.Mode == mode & gate.SessionHash == sessionHash & gate.Head == OperationsLearningPolicy.GitHead(root), "operations-hook-gate-identity-invalid");
            ValidateToolIdentity(root, gate);
            Ensure(HashArguments(gate) == gate.ArgumentsHash, "operations-hook-gate-arguments-tampered");
            WriteResult(true, systemMessage: $"OPERATIONS_LEARNING_SCOPE_ACTIVE mode={mode} gate={gatePath}");
        }
        catch (Exception ex)
        {
            WritePostmortemPreToolDenied(IncidentDiagnosticCode(ex), ex.Message);
        }
        return 0;
    }

    private static int PostTool(string root, string mode, string sessionHash, JsonElement input)
    {
        Ensure(mode == "postmortem", "operations-hook-post-tool-mode-invalid");
        if (ReadString(input, "tool_name", "toolName") != "vscode_askQuestions")
        {
            WriteResult(true);
            return 0;
        }
        var collectionPath = CollectionPath(root, sessionHash);
        if (!File.Exists(collectionPath))
        {
            WriteResult(true);
            return 0;
        }
        var collection = ReadCollection(collectionPath);
        ValidateCollection(root, collection);
        var toolUseId = ReadString(input, "tool_use_id", "toolUseId");
        Ensure(!string.IsNullOrWhiteSpace(toolUseId), "postmortem-route-confirmation-tool-use-id-missing");
        Ensure(TryReadToolElement(input, "tool_input", "toolInput", out var toolInput), "postmortem-route-confirmation-input-missing");
        Ensure(TryReadToolElement(input, "tool_response", "toolResponse", out var toolResponse), "postmortem-route-confirmation-response-missing");
        var proposal = ReadConfirmationQuestion(toolInput);
        var decision = ReadConfirmationDecision(toolResponse, proposal.Header, proposal.Question);

        if (decision == "Cancel")
        {
            var cancelledPath = CancelledCollectionPath(root, sessionHash);
            File.Move(collectionPath, cancelledPath, overwrite: true);
            ClearConfirmation(root, sessionHash);
            WriteResult(true, systemMessage: "POSTMORTEM_ROUTE_CANCELLED completionAccepted=false");
            return 0;
        }
        if (decision == "Choose different incident/path")
        {
            ClearConfirmation(root, sessionHash);
            WriteResult(true, systemMessage: "POSTMORTEM_ROUTE_REVISION_REQUESTED completionAccepted=false");
            return 0;
        }
        Ensure(decision == "Confirm", "postmortem-route-confirmation-decision-invalid");

        var receipt = new PostmortemConfirmationRecord(
            "1.0",
            sessionHash,
            collection.Head,
            collection.AssemblySha256,
            collection.ManifestSha256,
            collection.Token,
            toolUseId,
            proposal.Incidents,
            proposal.Postmortem,
            Sha256(Encoding.UTF8.GetBytes(toolInput.GetRawText())),
            Sha256(Encoding.UTF8.GetBytes(toolResponse.GetRawText())));
        var confirmationPath = ConfirmationPath(root, sessionHash);
        File.WriteAllText(confirmationPath, JsonSerializer.Serialize(receipt));
        WriteResult(true, systemMessage: $"POSTMORTEM_ROUTE_CONFIRMED receipt={ConfirmationRelativePath(sessionHash)} incidents={string.Join(',', proposal.Incidents)} postmortem={proposal.Postmortem}");
        return 0;
    }

    private static int Stop(string root, string mode, string sessionHash, string gatePath, bool subagent)
    {
        if (mode == "postmortem" && File.Exists(CancelledCollectionPath(root, sessionHash)))
        {
            CompletePostmortemCancellation(root, sessionHash);
            return 0;
        }
        if (mode == "postmortem" && File.Exists(CollectionPath(root, sessionHash)))
        {
            if (subagent)
                WriteSubagentStopBlocked("Stage 21 requires incident/path confirmation or explicit cancellation before delegated completion.");
            else
                WritePostmortemCollectionActive(root, sessionHash, "stop", "operations-hook-gate-missing");
            return 0;
        }
        if (mode == "postmortem" && TryReadPostmortemContext(gatePath, out var recoveryDiagnostic))
        {
            if (subagent)
                WriteSubagentStopBlocked("Stage 21 context is incomplete: " + recoveryDiagnostic);
            else
                WritePostmortemContextRequired("stop", recoveryDiagnostic);
            return 0;
        }
        if (mode == "postmortem" && !File.Exists(gatePath))
        {
            if (subagent)
                WriteSubagentStopBlocked("Stage 21 has no validated postmortem scope.");
            else
                WritePostmortemContextRequired("stop", "operations-hook-gate-missing");
            return 0;
        }
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
            else if (mode == "postmortem" && subagent) WriteSubagentStopBlocked("Postmortem validation failed: " + Bound(diagnostic));
            else WriteResult(false, $"Operations learning validation failed for mode={mode}: {Bound(diagnostic)}");
            return 0;
        }
        File.Delete(gatePath);
        WriteResult(true, systemMessage: marker);
        return 0;
    }

    private static void WriteSubagentStopBlocked(string reason)
        => Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            stopReason = (string?)null,
            systemMessage = "OPERATIONS_POSTMORTEM_SUBAGENT_COMPLETION_BLOCKED",
            hookSpecificOutput = new { hookEventName = "SubagentStop", decision = "block", reason = Bound(reason) },
        }));

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
            InvalidOperationException when exception.Message.StartsWith("operations-hook-", StringComparison.Ordinal)
                || exception.Message.StartsWith("postmortem-route-", StringComparison.Ordinal) => Bound(exception.Message),
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

    private static bool IsPostmortemContextDiagnostic(string diagnostic)
        => diagnostic == "operations-hook-prompt-missing"
            || diagnostic is "operations-hook-path-invalid" or "operations-hook-incident-ids-invalid"
            || diagnostic.StartsWith("operations-hook-option-missing:--postmortem", StringComparison.Ordinal)
            || diagnostic.StartsWith("operations-hook-option-missing:--incidents", StringComparison.Ordinal);

    private static string CollectionRelativePath(string sessionHash)
        => $".engloop/out/postmortem-context/{sessionHash}.json";

    private static string CollectionPath(string root, string sessionHash)
        => Path.Combine(root, CollectionRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));

    private static string ConfirmationRelativePath(string sessionHash)
        => $".engloop/out/postmortem-context/{sessionHash}.confirmation.json";

    private static string ConfirmationPath(string root, string sessionHash)
        => Path.Combine(root, ConfirmationRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));

    private static string CancelledCollectionPath(string root, string sessionHash)
        => CollectionPath(root, sessionHash) + ".cancelled";

    private static void BeginPostmortemCollection(string root, string sessionHash, string diagnostic)
    {
        ClearPostmortemCollection(root, sessionHash);
        var identity = CurrentToolIdentity(root);
        var collection = new PostmortemCollectionRecord(
            "1.0",
            sessionHash,
            OperationsLearningPolicy.GitHead(root) ?? throw new InvalidOperationException("operations-hook-git-head-unavailable"),
            identity.AssemblyPath,
            identity.AssemblySha256,
            identity.ToolVersion,
            identity.ManifestPath,
            identity.ManifestSha256,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant());
        var path = CollectionPath(root, sessionHash);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(collection));
        WritePostmortemCollectionActive(root, sessionHash, "initialize", diagnostic);
    }

    private static PostmortemCollectionRecord ReadCollection(string path)
        => JsonSerializer.Deserialize<PostmortemCollectionRecord>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("postmortem-route-collection-json-invalid");

    private static PostmortemConfirmationRecord ReadConfirmation(string path)
        => JsonSerializer.Deserialize<PostmortemConfirmationRecord>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("postmortem-route-confirmation-json-invalid");

    private static void ValidateCollection(string root, PostmortemCollectionRecord collection)
    {
        Ensure(collection.SchemaVersion == "1.0", "postmortem-route-collection-schema-invalid");
        Ensure(Regex.IsMatch(collection.SessionHash, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant), "postmortem-route-session-invalid");
        Ensure(Regex.IsMatch(collection.Token, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant), "postmortem-route-token-invalid");
        Ensure(collection.Head == OperationsLearningPolicy.GitHead(root), "postmortem-route-head-changed");
        var current = CurrentToolIdentity(root);
        Ensure(collection.AssemblyPath == current.AssemblyPath, "postmortem-route-tool-identity-changed:assembly-path");
        Ensure(collection.AssemblySha256 == current.AssemblySha256, "postmortem-route-tool-identity-changed:assembly-hash");
        Ensure(collection.ToolVersion == current.ToolVersion, "postmortem-route-tool-identity-changed:tool-version");
        Ensure(collection.ManifestPath == current.ManifestPath, "postmortem-route-tool-identity-changed:manifest-path");
        Ensure(collection.ManifestSha256 == current.ManifestSha256, "postmortem-route-tool-identity-changed:manifest-hash");
    }

    private static void ValidateConfirmation(string root, PostmortemCollectionRecord collection, PostmortemConfirmationRecord confirmation, string[] incidents, string postmortem)
    {
        Ensure(confirmation.SchemaVersion == "1.0", "postmortem-route-confirmation-schema-invalid");
        Ensure(confirmation.SessionHash == collection.SessionHash, "postmortem-route-confirmation-session-mismatch");
        Ensure(confirmation.Head == collection.Head, "postmortem-route-confirmation-head-mismatch");
        Ensure(confirmation.Head == OperationsLearningPolicy.GitHead(root), "postmortem-route-confirmation-head-changed");
        Ensure(confirmation.AssemblySha256 == collection.AssemblySha256, "postmortem-route-confirmation-assembly-changed");
        Ensure(confirmation.ManifestSha256 == collection.ManifestSha256, "postmortem-route-confirmation-manifest-changed");
        Ensure(confirmation.CollectionToken == collection.Token, "postmortem-route-confirmation-token-mismatch");
        Ensure(!string.IsNullOrWhiteSpace(confirmation.ToolUseId), "postmortem-route-confirmation-tool-use-id-missing");
        Ensure(Regex.IsMatch(confirmation.QuestionSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant), "postmortem-route-confirmation-question-hash-invalid");
        Ensure(Regex.IsMatch(confirmation.ResponseSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant), "postmortem-route-confirmation-response-hash-invalid");
        Ensure(confirmation.Incidents.SequenceEqual(incidents, StringComparer.Ordinal), "postmortem-route-confirmation-incidents-mismatch");
        Ensure(confirmation.Postmortem == postmortem, "postmortem-route-confirmation-postmortem-mismatch");
    }

    private static void ClearPostmortemCollection(string root, string sessionHash)
    {
        var path = CollectionPath(root, sessionHash);
        if (File.Exists(path)) File.Delete(path);
        var cancelled = CancelledCollectionPath(root, sessionHash);
        if (File.Exists(cancelled)) File.Delete(cancelled);
        ClearConfirmation(root, sessionHash);
    }

    private static void ClearConfirmation(string root, string sessionHash)
    {
        var path = ConfirmationPath(root, sessionHash);
        if (File.Exists(path)) File.Delete(path);
    }

    private static void CompletePostmortemCancellation(string root, string sessionHash)
    {
        var path = CancelledCollectionPath(root, sessionHash);
        var collection = ReadCollection(path);
        ValidateCollection(root, collection);
        Ensure(collection.SessionHash == sessionHash, "postmortem-route-cancellation-session-mismatch");
        File.Delete(path);
        WriteResult(true, systemMessage: "POSTMORTEM_ROUTE_CANCELLED completionAccepted=false");
    }

    private static void WritePostmortemCollectionActive(string root, string sessionHash, string phase, string diagnostic)
    {
        var path = CollectionPath(root, sessionHash);
        var collection = ReadCollection(path);
        ValidateCollection(root, collection);
        var details = JsonSerializer.Serialize(new
        {
            status = "postmortem-context-required",
            mode = "postmortem",
            phase,
            command = $"operations-hook {phase} postmortem",
            diagnosticCode = diagnostic,
            collectionPath = CollectionRelativePath(sessionHash),
            token = collection.Token,
            allowedTools = new[] { "read", "search", "vscode_askQuestions", "exact-postmortem-route-bind-or-cancel" },
            expectedSource = "A plain-language incident/postmortem request plus repository incident and numbering evidence",
            elkVersion = typeof(OperationsHookCommands).Assembly.GetName().Version?.ToString(3) ?? "unknown",
            remediation = "Collect candidate incident and next registry-backed PM path in chat, ask one concise confirmation, and invoke the trusted binder internally. Never ask the operator to reconstruct command-line options.",
            completionAccepted = false,
        });
        WriteResult(true, systemMessage: "OPERATIONS_POSTMORTEM_CONTEXT_COLLECTION_ACTIVE OPERATIONS_LEARNING_CONTEXT_REQUIRED " + details);
    }

    private static void WritePostmortemCollectionDecision(bool allow, string reason)
        => Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            stopReason = (string?)null,
            systemMessage = allow ? "OPERATIONS_POSTMORTEM_CONTEXT_COLLECTION_ACTIVE" : "OPERATIONS_POSTMORTEM_CONTEXT_COLLECTION_DENIED",
            hookSpecificOutput = new
            {
                hookEventName = "PreToolUse",
                permissionDecision = allow ? "allow" : "deny",
                permissionDecisionReason = reason,
            },
        }));

    private static bool IsCollectionReadOrQuestionTool(string toolName)
        => CollectionReadOrQuestionTools.Contains(toolName);

    private static bool IsCollectionRouteCommand(string toolName, string command, PostmortemCollectionRecord collection, string collectionRelative)
    {
        if (!CollectionCommandTools.Contains(toolName)) return false;
        if (command.IndexOfAny([';', '&', '|', '>', '<', '\r', '\n']) >= 0) return false;
        var bindPattern = "^dotnet tool run engloopkit -- postmortem-route bind --collection " + Regex.Escape(collectionRelative)
            + " --token " + Regex.Escape(collection.Token)
            + @" --incidents IN\d{3}(?:,IN\d{3})* --postmortem \.engloop/postmortems/PM\d{3}[_-][A-Za-z0-9][A-Za-z0-9._-]*\.md --confirmation-receipt "
            + Regex.Escape(ConfirmationRelativePath(collection.SessionHash)) + "$";
        return Regex.IsMatch(command, bindPattern, RegexOptions.CultureInvariant);
    }

    private static string ReadToolCommand(JsonElement input)
    {
        foreach (var name in new[] { "tool_input", "toolInput" })
            if (input.TryGetProperty(name, out var toolInput) && toolInput.ValueKind == JsonValueKind.Object)
                return ReadString(toolInput, "command", "commandLine");
        return string.Empty;
    }

    private static bool TryReadToolElement(JsonElement input, string snakeName, string camelName, out JsonElement value)
    {
        foreach (var name in new[] { snakeName, camelName })
        {
            if (!input.TryGetProperty(name, out var element)) continue;
            if (element.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                value = element;
                return true;
            }
            if (element.ValueKind == JsonValueKind.String)
            {
                try
                {
                    using var parsed = JsonDocument.Parse(element.GetString()!);
                    value = parsed.RootElement.Clone();
                    return true;
                }
                catch (JsonException)
                {
                    break;
                }
            }
        }
        value = default;
        return false;
    }

    private static (string Header, string Question, string[] Incidents, string Postmortem) ReadConfirmationQuestion(JsonElement input)
    {
        Ensure(input.ValueKind == JsonValueKind.Object, "postmortem-route-confirmation-questions-invalid");
        Ensure(input.TryGetProperty("questions", out var questions), "postmortem-route-confirmation-questions-invalid");
        Ensure(questions.ValueKind == JsonValueKind.Array, "postmortem-route-confirmation-questions-invalid");
        var items = questions.EnumerateArray().ToArray();
        Ensure(items.Length == 1, "postmortem-route-confirmation-question-count-invalid");
        var question = items[0];
        var header = ReadString(question, "header");
        Ensure(header == "Confirm postmortem", "postmortem-route-confirmation-header-invalid");
        Ensure(question.TryGetProperty("allowFreeformInput", out var freeform), "postmortem-route-confirmation-freeform-missing");
        Ensure(freeform.ValueKind == JsonValueKind.False, "postmortem-route-confirmation-freeform-forbidden");
        Ensure(question.TryGetProperty("multiSelect", out var multi), "postmortem-route-confirmation-multiselect-missing");
        Ensure(multi.ValueKind == JsonValueKind.False, "postmortem-route-confirmation-multiselect-forbidden");
        Ensure(question.TryGetProperty("options", out var options), "postmortem-route-confirmation-options-missing");
        Ensure(options.ValueKind == JsonValueKind.Array, "postmortem-route-confirmation-options-invalid");
        var labels = options.EnumerateArray().Select(option => ReadString(option, "label")).ToArray();
        Ensure(labels.SequenceEqual(new[] { "Confirm", "Choose different incident/path", "Cancel" }, StringComparer.Ordinal), "postmortem-route-confirmation-options-invalid");
        var questionText = ReadString(question, "question");
        var match = Regex.Match(questionText, @"^Use incidents (?<incidents>IN\d{3}(?:,IN\d{3})*) and create (?<postmortem>\.engloop/postmortems/PM\d{3}[_-][A-Za-z0-9][A-Za-z0-9._-]*\.md)\?$", RegexOptions.CultureInvariant);
        Ensure(match.Success, "postmortem-route-confirmation-question-invalid");
        return (header, questionText, IdList(match.Groups["incidents"].Value, @"^IN\d{3}$", "incident"), GovernedPath(match.Groups["postmortem"].Value, ".engloop/postmortems/"));
    }

    private static string ReadConfirmationDecision(JsonElement response, string header, string question)
    {
        Ensure(response.ValueKind == JsonValueKind.Object, "postmortem-route-confirmation-answer-invalid");
        Ensure(response.TryGetProperty("answers", out var answers), "postmortem-route-confirmation-answer-invalid");
        Ensure(answers.ValueKind == JsonValueKind.Object, "postmortem-route-confirmation-answer-invalid");
        var hasAnswer = answers.TryGetProperty(question, out var answer) || answers.TryGetProperty(header, out answer);
        Ensure(hasAnswer, "postmortem-route-confirmation-answer-missing");
        Ensure(answer.ValueKind == JsonValueKind.Object, "postmortem-route-confirmation-answer-invalid");
        Ensure(answer.TryGetProperty("selected", out var selected), "postmortem-route-confirmation-selection-missing");
        Ensure(selected.ValueKind == JsonValueKind.Array, "postmortem-route-confirmation-selection-invalid");
        var values = selected.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Ensure(values.Length == 1, "postmortem-route-confirmation-selection-invalid");
        return values[0];
    }

    private static string PostmortemContextPath(string gatePath) => gatePath + ".context-required";

    private static void SuspendPostmortemContext(string gatePath, string diagnostic)
        => File.WriteAllText(PostmortemContextPath(gatePath), diagnostic);

    private static bool TryReadPostmortemContext(string gatePath, out string diagnostic)
    {
        var path = PostmortemContextPath(gatePath);
        if (!File.Exists(path))
        {
            diagnostic = string.Empty;
            return false;
        }
        var stored = File.ReadAllText(path).Trim();
        diagnostic = IsPostmortemContextDiagnostic(stored) ? stored : "operations-hook-postmortem-context-state-invalid";
        return true;
    }

    private static void ClearPostmortemContext(string gatePath)
    {
        var path = PostmortemContextPath(gatePath);
        if (File.Exists(path)) File.Delete(path);
    }

    private static void WritePostmortemContextRequired(string phase, string diagnostic)
    {
        var missingOption = Regex.Match(diagnostic, @"operations-hook-option-missing:(?<option>--[a-z-]+)", RegexOptions.CultureInvariant) is { Success: true } match
            ? match.Groups["option"].Value
            : diagnostic == "operations-hook-prompt-missing" ? "--incidents,--postmortem" : null;
        var details = JsonSerializer.Serialize(new
        {
            status = "postmortem-context-required",
            mode = "postmortem",
            phase,
            command = $"operations-hook {phase} postmortem",
            diagnosticCode = diagnostic,
            missingOption,
            expectedSource = "A plain-language incident/postmortem request plus repository incident and numbering evidence",
            elkVersion = typeof(OperationsHookCommands).Assembly.GetName().Version?.ToString(3) ?? "unknown",
            remediation = "No postmortem scope or completion was accepted. Collect and confirm valid incident/PM context in chat, then use the trusted internal binder; never ask the operator to type hook flags.",
            completionAccepted = false,
        });
        WriteResult(true, systemMessage: "OPERATIONS_LEARNING_CONTEXT_REQUIRED " + details);
    }

    private static void WritePostmortemPreToolDenied(string diagnosticCode, string diagnostic)
    {
        const string remediation = "Stage 21 has no valid postmortem scope. Resubmit with --incidents <INxxx,...> --postmortem <.engloop/postmortems/PMxxx_title.md> before using tools.";
        var details = JsonSerializer.Serialize(new
        {
            status = "postmortem-scope-denied",
            mode = "postmortem",
            phase = "pre-tool-use",
            command = "operations-hook guard postmortem",
            diagnosticCode,
            diagnostic = Bound(diagnostic),
            remediation,
            completionAccepted = false,
        });
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            stopReason = (string?)null,
            systemMessage = "OPERATIONS_LEARNING_CONTEXT_REQUIRED " + details,
            hookSpecificOutput = new
            {
                hookEventName = "PreToolUse",
                permissionDecision = "deny",
                permissionDecisionReason = $"{remediation} Diagnostic: {diagnosticCode}",
            },
        }));
    }

    private static GateRecord? ParseArguments(string prompt, string mode, bool allowMissing)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return allowMissing ? null : throw new InvalidOperationException("operations-hook-prompt-missing");
        if (allowMissing && !ContainsRelevantArgument(prompt, mode)) return null;
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

    private static GateRecord NewArguments(string? incident = null, string? postmortem = null, string[]? incidents = null, string? phase = null, string? rpi = null, string[]? rules = null, string? acceptance = null)
        => new("", "", "", "", "", "", "", "", "", "", incident, postmortem, incidents, phase, rpi, rules, acceptance);

    private static void WriteGate(string root, string mode, string sessionHash, string gatePath, GateRecord arguments)
    {
        var head = OperationsLearningPolicy.GitHead(root) ?? throw new InvalidOperationException("operations-hook-git-head-unavailable");
        var identity = CurrentToolIdentity(root);
        var gate = arguments with
        {
            SchemaVersion = "1.0",
            Mode = mode,
            SessionHash = sessionHash,
            Head = head,
            ArgumentsHash = HashArguments(arguments),
            AssemblyPath = identity.AssemblyPath,
            AssemblySha256 = identity.AssemblySha256,
            ToolVersion = identity.ToolVersion,
            ManifestPath = identity.ManifestPath,
            ManifestSha256 = identity.ManifestSha256,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(gatePath)!);
        File.WriteAllText(gatePath, JsonSerializer.Serialize(gate));
    }

    private static string CliOption(string[] args, string name)
    {
        var index = Array.FindIndex(args, value => value == name);
        Ensure(index >= 0, "postmortem-route-option-missing:" + name);
        Ensure(index + 1 < args.Length, "postmortem-route-option-missing:" + name);
        Ensure(!args[index + 1].StartsWith("--", StringComparison.Ordinal), "postmortem-route-option-missing:" + name);
        return args[index + 1];
    }

    private static void RequirePostmortemAgentEntry(string root)
    {
        var result = ValidationCommands.EvaluateAgentEntry(["--stage", "speckit.engloop.21-postmortem", "--root", root]);
        Ensure(result.Passed, "operations-hook-agent-entry-rejected:" + result.Reason);
    }

    private static string NextRegistryId(string root, string prefix)
    {
        var registryPath = Path.Combine(root, ".engloop", "numbering-registry.md");
        Ensure(File.Exists(registryPath), "postmortem-route-numbering-registry-missing");
        var lines = File.ReadAllLines(registryPath);
        var headers = lines.Select(MarkdownCells)
            .Where(cells => cells.Length > 1 && cells[0].Equals("Prefix", StringComparison.OrdinalIgnoreCase)
                && cells.Any(cell => cell.Equals("Last used", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        Ensure(headers.Length == 1, "postmortem-route-numbering-header-invalid");
        var lastUsedIndex = Array.FindIndex(headers[0], cell => cell.Equals("Last used", StringComparison.OrdinalIgnoreCase));
        var rows = lines.Select(MarkdownCells)
            .Where(cells => cells[0].Trim('`').Equals(prefix, StringComparison.Ordinal))
            .ToArray();
        Ensure(rows.Length == 1, "postmortem-route-numbering-row-invalid:" + prefix);
        Ensure(rows[0].Length > lastUsedIndex, "postmortem-route-numbering-row-invalid:" + prefix);
        var value = rows[0][lastUsedIndex].Trim('`');
        Ensure(Regex.IsMatch(value, "^" + Regex.Escape(prefix) + @"\d{3}$", RegexOptions.CultureInvariant), "postmortem-route-numbering-value-invalid:" + prefix);
        Ensure(int.TryParse(value[prefix.Length..], out var last), "postmortem-route-numbering-value-invalid:" + prefix);
        Ensure(last < 999, "postmortem-route-numbering-value-invalid:" + prefix);
        return NumberingRegistry.Format(prefix, last + 1);
    }

    private static string[] MarkdownCells(string line)
        => line.Trim().Trim('|').Split('|', StringSplitOptions.TrimEntries);

    private static bool ContainsRelevantArgument(string prompt, string mode)
    {
        var names = mode switch
        {
            "incident" => new[] { "--incident" },
            "postmortem" => new[] { "--incidents", "--postmortem" },
            _ => new[] { "--phase", "--postmortem", "--rpi", "--rules", "--acceptance" },
        };
        return names.Any(name => Regex.IsMatch(prompt, "(?:^|\\s)" + Regex.Escape(name) + "(?:=|\\s|$)", RegexOptions.CultureInvariant));
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
        return match.Groups["dq"].Value + match.Groups["sq"].Value + match.Groups["bare"].Value;
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
        foreach (var name in names) if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString()!;
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
