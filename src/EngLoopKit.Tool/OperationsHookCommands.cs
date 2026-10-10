using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using EngLoopKit.Core;

namespace EngLoopKit.Tool;

public static class OperationsHookCommands
{
    private const string NavigationRecovery = "The user may select Stage 22 (speckit.engloop.22-repair) or Stage 50 (speckit.engloop.50-handoff-create) any time without binder tokens. Stop does not veto navigation. Selection does not authorize mutations or claim completion.";
    private const string RepairContinuationMarker = "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION";
    private static readonly TimeSpan HookInputTimeout = TimeSpan.FromSeconds(3);

    private static readonly HashSet<string> CollectionReadOrQuestionTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "search", "read_file", "file_search", "grep_search", "semantic_search", "list_dir", "vscode_askQuestions",
    };

    private static readonly HashSet<string> PostmortemRetryTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "search", "read_file", "file_search", "grep_search", "semantic_search", "list_dir",
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
        string? Acceptance,
        string[]? RequestedRpis = null);

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

    private sealed record PostmortemBindingRecord(
        string SchemaVersion,
        string SessionHash,
        string Head,
        string[] Incidents,
        string Postmortem,
        string Gate,
        string GateSha256,
        string ConfirmationSha256,
        string ToolUseId,
        string CorrelationId,
        string Authorization = "confirmation",
        string? Intent = null,
        string? IntentSha256 = null,
        Dictionary<string, string>? IncidentHashes = null);

    private sealed record PostmortemOperatorIntentRecord(
        string SchemaVersion, string SessionHash, string Head, string HostSha256,
        string Prompt, string PromptSha256, string Incident, string Postmortem,
        string IncidentSha256, string NorthstarSha256, string LearningsSha256,
        string RegistrySha256, string[]? Incidents = null,
        Dictionary<string, string>? IncidentHashes = null, string? IncidentDate = null);

    private sealed record PostmortemReauthorizationRecord(
        string SchemaVersion, string NativeSessionHash, string PreviousSessionHash, string SessionHash,
        string Prompt, string PromptSha256, string HistoricalPostmortem, string Postmortem,
        Dictionary<string, string> PreservedArtifacts, string BindingSha256,
        string[]? Incidents = null, string? IncidentDate = null, bool ScopeChange = false);

    private sealed record PostmortemBindingFailureRecord(
        string SchemaVersion,
        string SessionHash,
        string Head,
        string[] Incidents,
        string Postmortem,
        string DiagnosticCode,
        string Diagnostic,
        string CorrelationId,
        DateTimeOffset CapturedAtUtc);

    private sealed record RepairIntentRecord(
        string SchemaVersion,
        string SessionHash,
        string Head,
        string PromptSha256,
        bool AllRepairs,
        string[] Rpis,
        string AssemblyPath,
        string AssemblySha256,
        string ToolVersion,
        string ManifestPath,
        string ManifestSha256);

    public static int Execute(string[] args)
    {
        if (args.Length >= 2 && args[0] == "authorize" && args[1] == "repair")
            return AuthorizeRepair(args);
        var action = args.Length > 0 ? args[0] : "unknown";
        var mode = args.Length > 1 ? args[1] : "unknown";
        var validDispatch = false;
        var toolName = string.Empty;
        try
        {
            Ensure(args.Length >= 2, "operations-hook-requires-action-and-mode");
            Ensure(mode is "incident" or "postmortem" or "repair", "operations-hook-mode-invalid");
            Ensure(action is "start" or "subagent-start" or "initialize" or "guard" or "post-tool" or "stop" or "subagent-stop", "operations-hook-action-invalid");
            Ensure(action is not ("guard" or "post-tool") || mode == "postmortem", "operations-hook-action-mode-invalid");
            validDispatch = true;
            string? payload = null;
            JsonDocument? parsedInput = null;
            Exception? inputFailure = null;
            var inputReader = new Thread(() =>
            {
                try
                {
                    var buffer = new StringBuilder();
                    var started = false;
                    var insideString = false;
                    var escaped = false;
                    var depth = 0;
                    while (true)
                    {
                        var next = Console.In.Read();
                        if (next < 0)
                        {
                            payload = buffer.ToString();
                            return;
                        }

                        var character = (char)next;
                        if (!started)
                        {
                            if (char.IsWhiteSpace(character)) continue;
                            started = true;
                            depth = 0;
                            if (character != '{')
                            {
                                payload = character.ToString();
                                return;
                            }
                        }

                        buffer.Append(character);
                        if (buffer.Length > 1_048_576) throw new InvalidOperationException("operations-hook-input-too-large");
                        if (insideString)
                        {
                            if (escaped) escaped = false;
                            else if (character == '\\') escaped = true;
                            else if (character == '"') insideString = false;
                            continue;
                        }

                        if (character == '"') insideString = true;
                        else if (character is '{' or '[') depth++;
                        else if (character is '}' or ']') depth--;
                        if (depth <= 0)
                        {
                            payload = buffer.ToString();
                            try { parsedInput = JsonDocument.Parse(payload); }
                            catch (JsonException) { }
                            return;
                        }
                    }
                }
                catch (Exception ex) { inputFailure = ex; }
            }) { IsBackground = true };
            inputReader.Start();
            if (!inputReader.Join(HookInputTimeout))
            {
                WriteLifecycleDeferred(mode, action, "operations-hook-stdin-timeout",
                    $"The JSON hook payload did not reach end-of-input within {HookInputTimeout.TotalSeconds:0} seconds. Retry through the supported VS Code host.");
                return 0;
            }
            if (inputFailure is JsonException or InvalidOperationException) throw inputFailure;
            if (inputFailure is not null) throw new InvalidOperationException("operations-hook-stdin-read-failed", inputFailure);
            using var input = parsedInput ?? JsonDocument.Parse(payload ?? string.Empty);
            Ensure(input.RootElement.ValueKind == JsonValueKind.Object, "operations-hook-input-invalid");
            toolName = ReadString(input.RootElement, "tool_name", "toolName");
            var root = ExactGitRoot(ReadString(input.RootElement, "cwd"));
            var sessionId = ReadString(input.RootElement, "session_id", "sessionId");
            Ensure(!string.IsNullOrWhiteSpace(sessionId), "operations-hook-session-id-missing");
            var nativeSessionHash = Sha256(Encoding.UTF8.GetBytes(sessionId));
            var sessionHash = mode is "postmortem" or "repair"
                ? ResolvePostmortemSession(root, nativeSessionHash) : nativeSessionHash;
            if (mode == "postmortem" && action == "initialize"
                && ReadString(input.RootElement, "hookEventName", "hook_event_name") == "UserPromptSubmit"
                && ReadExplicitPostmortemIntent(ReadString(input.RootElement, "prompt"), root) is { } newRequest
                && File.Exists(GatePath(root, sessionHash, "postmortem"))
                && ReadGate(GatePath(root, sessionHash, "postmortem")).Incidents is { } previousIncidents
                && !previousIncidents.SequenceEqual(newRequest.Incidents, StringComparer.Ordinal))
                return InitializeNewPostmortemScope(root, nativeSessionHash, sessionHash, input.RootElement, newRequest);
            if (mode == "postmortem" && action == "initialize"
                && ReadString(input.RootElement, "hookEventName", "hook_event_name") == "UserPromptSubmit"
                && RequiresFreshPostmortemIdentity(root, sessionHash))
            {
                var prompt = ReadString(input.RootElement, "prompt");
                var freshIntent = ReadExplicitPostmortemIntent(prompt, root)
                    ?? ReadBoundDatedPostmortemIntent(root, sessionHash, prompt);
                if (freshIntent is { } request)
                    return ReauthorizePostmortem(root, nativeSessionHash, sessionHash, input.RootElement, request);
            }
            var gatePath = GatePath(root, sessionHash, mode);
            if (action is "start" or "subagent-start") _ = CurrentToolIdentity(root);

            return action switch
            {
                "start" => Start(root, mode, sessionHash, input.RootElement),
                "subagent-start" => SubagentStart(mode, sessionHash),
                "initialize" => Initialize(root, mode, sessionHash, gatePath, ReadString(input.RootElement, "prompt"), input.RootElement),
                "guard" => Guard(root, mode, sessionHash, gatePath, input.RootElement),
                "post-tool" => PostTool(root, mode, sessionHash, input.RootElement),
                "stop" => Stop(root, mode, sessionHash, gatePath, subagent: false),
                "subagent-stop" => Stop(root, mode, sessionHash, gatePath, subagent: true),
                _ => throw new InvalidOperationException("operations-hook-action-invalid"),
            };
        }
        catch (Exception ex)
        {
            if (!validDispatch) WriteResult(false, "Operations learning hook failed closed: " + Bound(ex.Message));
            else if (action == "guard") WritePostmortemScopeDecision(toolName, IncidentDiagnosticCode(ex), ex.Message);
            else WriteLifecycleDeferred(mode, action, IncidentDiagnosticCode(ex), ex.Message,
                action == "post-tool" ? "OPERATIONS_POSTMORTEM_CONFIRMATION_UNVERIFIED" : null);
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
            var collectionRelative = ExactRoutePath(CliOption(args, "--collection"));
            var incidents = IdList(CliOption(args, "--incidents"), @"^IN\d{3}$", "incident");
            var postmortem = PostmortemPath(CliOption(args, "--postmortem"));
            var confirmationRelative = ExactRoutePath(CliOption(args, "--confirmation-receipt"));
            var bound = BindPostmortemRoute(root, collectionRelative, CliOption(args, "--token"),
                incidents, postmortem, confirmationRelative);
            Console.WriteLine($"POSTMORTEM_ROUTE_BOUND OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem gate={bound.Gate} bindingReceipt={bound.Receipt} incidents={string.Join(',', incidents)} postmortem={postmortem}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static bool HasPostmortemSessionState(string root, string sessionHash)
    {
        var statePaths = new[]
        {
            GatePath(root, sessionHash, "postmortem"),
            PostmortemContextPath(GatePath(root, sessionHash, "postmortem")),
            GatePath(root, sessionHash, "repair"),
            CollectionPath(root, sessionHash),
            Path.Combine(root, BindingRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar)),
            Path.Combine(root, ConfirmationPath(root, sessionHash).Replace('/', Path.DirectorySeparatorChar)),
            Path.Combine(root, BindingFailureRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar)),
            Path.Combine(root, PostmortemIntentRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar)),
            RepairIntentPath(root, sessionHash),
        };
        if (statePaths.Any(path => File.Exists(path))) return true;

        var contextDirectory = Path.Combine(root, ".engloop", "out", "postmortem-context");
        return Directory.Exists(contextDirectory)
            && (Directory.EnumerateFiles(contextDirectory, "*.reauthorization." + sessionHash + ".json").Any()
                || Directory.EnumerateFiles(contextDirectory, sessionHash + ".reauthorization.*.json").Any());
    }

    private static void EnsurePostmortemHostForPrompt(string root, string sessionHash)
    {
        var hostPath = PostmortemHostPath(root, sessionHash);
        if (File.Exists(hostPath)) return;

        Ensure(!HasPostmortemSessionState(root, sessionHash), "postmortem-route-host-session-missing");
        WriteGate(root, "postmortem-host", sessionHash, hostPath, NewArguments());
    }

    private static int Start(string root, string mode, string sessionHash, JsonElement input)
    {
        if (mode == "repair" && ReadString(input, "hookEventName", "hook_event_name") == "SessionStart")
        {
            var hostPath = GatePath(root, sessionHash, "repair-host");
            if (!File.Exists(hostPath)) WriteGate(root, "repair-host", sessionHash, hostPath, NewArguments());
        }
        if (mode == "postmortem" && ReadString(input, "hookEventName", "hook_event_name") == "SessionStart")
        {
            var hostPath = PostmortemHostPath(root, sessionHash);
            if (!File.Exists(hostPath)) WriteGate(root, "postmortem-host", sessionHash, hostPath, NewArguments());
            else RefreshUnboundPostmortemHost(root, sessionHash, hostPath);
        }
        var context = $"OPERATIONS_LEARNING_GUARD_ACTIVE mode={mode} session={sessionHash[..12]} " + NavigationRecovery;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            stopReason = (string?)null,
            hookSpecificOutput = new { hookEventName = "SessionStart", additionalContext = context },
        }));
        return 0;
    }

    private static int SubagentStart(string mode, string sessionHash)
    {
        var context = $"OPERATIONS_LEARNING_GUARD_ACTIVE mode={mode} session={sessionHash[..12]} " + NavigationRecovery;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            stopReason = (string?)null,
            systemMessage = context,
            hookSpecificOutput = new { hookEventName = "SubagentStart", additionalContext = context },
        }));
        return 0;
    }

    private static int Initialize(string root, string mode, string sessionHash, string gatePath, string prompt, JsonElement input)
    {
        if (mode == "postmortem")
        {
            RequirePostmortemAgentEntry(root);
            if (ReadString(input, "hookEventName", "hook_event_name") == "UserPromptSubmit")
                EnsurePostmortemHostForPrompt(root, sessionHash);
        }
        if (mode == "postmortem" && ReadExplicitPostmortemIntent(prompt, root) is { } intent)
            return InitializeExplicitPostmortem(root, sessionHash, gatePath, prompt, input,
                intent.Incidents, intent.Postmortem, intent.IncidentDate);
        if (mode == "repair" && ReadExplicitRepairPostmortem(root, prompt) is { } repairPostmortem)
        {
            Ensure(ReadString(input, "hookEventName", "hook_event_name") == "UserPromptSubmit", "repair-route-host-prompt-event-missing");
            var hostPath = GatePath(root, sessionHash, "repair-host");
            if (!File.Exists(hostPath)) WriteGate(root, "repair-host", sessionHash, hostPath, NewArguments());
            var host = ReadGate(hostPath);
            Ensure(host.Mode == "repair-host" && host.SchemaVersion == "1.0" && host.SessionHash == sessionHash
                && host.Head == OperationsLearningPolicy.GitHead(root) && host.ArgumentsHash == HashArguments(NewArguments()),
                "repair-route-host-session-identity-invalid");
            ValidateToolIdentity(root, host);
            CaptureRepairIntent(root, sessionHash, prompt);
            var repairIntent = ReadRepairIntent(root, sessionHash)
                ?? throw new InvalidOperationException("repair-continuation-intent-missing");
            return BindRequestedRepairs(root, sessionHash, gatePath, repairPostmortem, repairIntent);
        }
        if (mode is "incident" or "postmortem") CaptureRepairIntent(root, sessionHash, prompt);
        if (mode == "repair" && prompt.Contains(RepairContinuationMarker, StringComparison.Ordinal))
            return InitializeTrustedRepairContinuation(root, sessionHash, gatePath);
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
            catch (InvalidOperationException ex) when (mode == "postmortem")
            {
                SuspendPostmortemContext(gatePath, ex.Message);
                WritePostmortemContextRequired("initialize", ex.Message);
                return 0;
            }
            catch (InvalidOperationException ex) when (IsMissingScopeInput(mode, ex.Message))
            {
                WriteQuietResult();
                return 0;
            }
            if (mode == "postmortem" && suppliedArguments is null && TryReadPostmortemContext(gatePath, out var diagnostic))
            {
                WritePostmortemContextRequired("initialize", diagnostic);
                return 0;
            }
            if (suppliedArguments is not null && HashArguments(suppliedArguments) != existing.ArgumentsHash)
            {
                const string changed = "operations-hook-followup-arguments-changed";
                if (mode == "postmortem") SuspendPostmortemContext(gatePath, changed);
                throw new InvalidOperationException(changed);
            }
            if (mode == "postmortem") ClearPostmortemContext(gatePath);
            WriteHookContext("UserPromptSubmit", $"OPERATIONS_LEARNING_SCOPE_ACTIVE mode={mode} gate={gatePath}");
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
        catch (InvalidOperationException ex) when (IsMissingScopeInput(mode, ex.Message))
        {
            WriteQuietResult();
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
        var toolName = ReadString(input, "tool_name", "toolName");
        var collectionPath = CollectionPath(root, sessionHash);
        if (File.Exists(collectionPath))
        {
            try
            {
                var collection = ReadCollection(collectionPath);
                ValidateCollection(root, collection);
                Ensure(collection.SessionHash == sessionHash, "postmortem-route-collection-session-mismatch");
                var allow = IsCollectionReadOrQuestionTool(toolName);
                WritePostmortemCollectionDecision(allow, allow
                    ? "Stage 21 context collection permits read, search, and questions."
                    : "This Stage 21 context-collection tool call was blocked; no action ran. Continue with read, search, or vscode_askQuestions. Do not retry terminal commands or request privileged actions.");
            }
            catch (Exception ex)
            {
                WritePostmortemScopeDecision(toolName, IncidentDiagnosticCode(ex), ex.Message);
            }
            return 0;
        }
        try
        {
            if (TryReadPostmortemContext(gatePath, out var diagnostic))
            {
                WritePostmortemScopeDecision(toolName, diagnostic, diagnostic);
                return 0;
            }
            RequirePostmortemAgentEntry(root);
            Ensure(File.Exists(gatePath), "operations-hook-gate-missing");
            var gate = ReadGate(gatePath);
            Ensure(gate.SchemaVersion == "1.0" & gate.Mode == mode & gate.SessionHash == sessionHash & gate.Head == OperationsLearningPolicy.GitHead(root), "operations-hook-gate-identity-invalid");
            ValidateToolIdentity(root, gate);
            Ensure(HashArguments(gate) == gate.ArgumentsHash, "operations-hook-gate-arguments-tampered");
            if (File.Exists(Path.Combine(root, BindingRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar))))
                ValidatePostmortemBinding(root, sessionHash, gatePath, gate);
            if (!IsPostmortemScopeOperationAllowed(root, gate, toolName, input))
            {
                WritePostmortemOperationDenied(toolName);
                return 0;
            }
            WriteHookContext("PreToolUse", File.Exists(Path.Combine(root, BindingRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar)))
                ? PostmortemActivationContext(root, sessionHash, gatePath, gate)
                : $"OPERATIONS_LEARNING_SCOPE_ACTIVE mode={mode} gate={gatePath}");
        }
        catch (Exception ex)
        {
            WritePostmortemScopeDecision(toolName, IncidentDiagnosticCode(ex), ex.Message);
        }
        return 0;
    }

    private static int PostTool(string root, string mode, string sessionHash, JsonElement input)
    {
        Ensure(mode == "postmortem", "operations-hook-post-tool-mode-invalid");
        if (TryRestorePostmortemActivation(root, sessionHash, input)) return 0;
        if (ReadString(input, "tool_name", "toolName") != "vscode_askQuestions")
        {
            if (TryRetryPostmortemBinding(root, sessionHash, input)) return 0;
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
        try
        {
            var bound = BindPostmortemRoute(root, CollectionRelativePath(sessionHash), collection.Token,
                proposal.Incidents, proposal.Postmortem, ConfirmationRelativePath(sessionHash));
            WritePostToolContext($"POSTMORTEM_ROUTE_BOUND OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem gate={bound.Gate} bindingReceipt={bound.Receipt} incidents={string.Join(',', proposal.Incidents)} postmortem={proposal.Postmortem}");
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            WriteBindingFailure(root, sessionHash, proposal.Incidents, proposal.Postmortem, ex, correlationId);
            WritePostToolContext($"POSTMORTEM_ROUTE_BIND_FAILED completionAccepted=false correlation={correlationId} diagnostic={Bound(IncidentDiagnosticCode(ex))}: {Bound(ex.Message)}. Preserve the confirmed request; continue with the supported read/search/question workflow and retry internal binding after correcting only the reported prerequisite. Do not ask the operator to repeat an already delivered confirmation or construct binder tokens.");
        }
        return 0;
    }

    private static bool TryRetryPostmortemBinding(string root, string sessionHash, JsonElement input)
    {
        var toolName = ReadString(input, "tool_name", "toolName");
        if (!PostmortemRetryTools.Contains(toolName)
            || !TryReadToolElement(input, "tool_response", "toolResponse", out var toolResponse)
            || toolResponse.ValueKind == JsonValueKind.Object
                && (toolResponse.TryGetProperty("isError", out var isError) && isError.ValueKind == JsonValueKind.True
                    || toolResponse.TryGetProperty("error", out var error) && error.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined))
            return false;

        var collectionPath = CollectionPath(root, sessionHash);
        var confirmationPath = ConfirmationPath(root, sessionHash);
        var failurePath = Path.Combine(root, BindingFailureRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(collectionPath) || !File.Exists(confirmationPath) || !File.Exists(failurePath)) return false;

        try
        {
            var collection = ReadCollection(collectionPath);
            var failure = JsonSerializer.Deserialize<PostmortemBindingFailureRecord>(File.ReadAllText(failurePath))
                ?? throw new InvalidOperationException("postmortem-route-binding-failure-json-invalid");
            ValidateCollection(root, collection, allowToolIdentityUpgrade: true);
            Ensure(collection.SessionHash == sessionHash && failure.SessionHash == sessionHash
                && failure.Head == collection.Head && failure.SchemaVersion == "1.0"
                && !string.IsNullOrWhiteSpace(failure.DiagnosticCode) && !string.IsNullOrWhiteSpace(failure.Diagnostic)
                && Regex.IsMatch(failure.CorrelationId, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant),
                "postmortem-route-binding-failure-identity-invalid");
            var confirmation = ReadConfirmation(confirmationPath);
            Ensure(failure.Incidents.SequenceEqual(confirmation.Incidents, StringComparer.Ordinal)
                && failure.Postmortem == confirmation.Postmortem,
                "postmortem-route-binding-failure-confirmation-mismatch");
            var bound = BindPostmortemRoute(root, CollectionRelativePath(sessionHash), collection.Token,
                confirmation.Incidents, confirmation.Postmortem, ConfirmationRelativePath(sessionHash),
                allowToolIdentityUpgrade: true);
            WritePostToolContext($"POSTMORTEM_ROUTE_BOUND OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem gate={bound.Gate} bindingReceipt={bound.Receipt} incidents={string.Join(',', confirmation.Incidents)} postmortem={confirmation.Postmortem} confirmationReused=true");
        }
        catch (Exception ex)
        {
            try
            {
                var failure = JsonSerializer.Deserialize<PostmortemBindingFailureRecord>(File.ReadAllText(failurePath));
                var correlationId = failure is not null && failure.SessionHash == sessionHash
                    ? failure.CorrelationId
                    : Guid.NewGuid().ToString("N");
                var confirmation = ReadConfirmation(confirmationPath);
                WriteBindingFailure(root, sessionHash, confirmation.Incidents, confirmation.Postmortem, ex, correlationId);
                WritePostToolContext($"POSTMORTEM_ROUTE_BIND_FAILED completionAccepted=false correlation={correlationId} diagnostic={Bound(IncidentDiagnosticCode(ex))}: {Bound(ex.Message)}. The original confirmation is retained; continue with read/search/questions after correcting only this prerequisite.");
            }
            catch
            {
                WriteResult(true);
            }
        }
        return true;
    }

    private static (string Gate, string Receipt) BindPostmortemRoute(string root,
        string collectionRelative, string token, string[] incidents, string postmortem,
        string confirmationRelative, bool allowToolIdentityUpgrade = false)
    {
        RequirePostmortemAgentEntry(root);
        _ = PostmortemPath(postmortem);
        var collectionPath = Path.Combine(root, collectionRelative.Replace('/', Path.DirectorySeparatorChar));
        Ensure(File.Exists(collectionPath), "postmortem-route-collection-missing");
        var collection = ReadCollection(collectionPath);
        ValidateCollection(root, collection, allowToolIdentityUpgrade);
        Ensure(collectionRelative == CollectionRelativePath(collection.SessionHash), "postmortem-route-collection-path-mismatch");
        Ensure(string.Equals(token, collection.Token, StringComparison.Ordinal), "postmortem-route-token-mismatch");
        ValidatePostmortemRouteScope(root, incidents, postmortem);
        var confirmationPath = Path.Combine(root, confirmationRelative.Replace('/', Path.DirectorySeparatorChar));
        Ensure(confirmationRelative == ConfirmationRelativePath(collection.SessionHash), "postmortem-route-confirmation-path-mismatch");
        Ensure(File.Exists(confirmationPath), "postmortem-route-confirmation-missing");
        var confirmation = ReadConfirmation(confirmationPath);
        ValidateConfirmation(root, collection, confirmation, incidents, postmortem);
        var bound = WritePostmortemBinding(root, collection.SessionHash, collection.Head, incidents, postmortem,
            FileSha256(confirmationPath), confirmation.ToolUseId);
        File.Delete(collectionPath);
        File.Delete(confirmationPath);
        ClearBindingFailure(root, collection.SessionHash);
        return bound;
    }

    private static void ValidatePostmortemRouteScope(string root, string[] incidents, string postmortem)
    {
        foreach (var incident in incidents)
        {
            var relative = Path.GetRelativePath(root, UniqueIncidentPath(root, incident)).Replace('\\', '/');
            var validation = OperationsLearningPolicy.ValidateIncidentContextForPostmortemAnalysis(root, relative);
            Ensure(validation.Passed, "postmortem-route-analysis-context-invalid:" + incident + ":" + string.Join(',', validation.Failures));
        }
        var nextPm = NextRegistryId(root, "PM");
        var proposedId = Regex.Match(Path.GetFileName(postmortem), @"^PM\d{3}", RegexOptions.CultureInvariant).Value;
        Ensure(proposedId == nextPm, $"postmortem-route-next-id-mismatch:expected={nextPm}:actual={proposedId}");
        Ensure(Regex.IsMatch(Path.GetFileName(postmortem), @"^PM\d{3}[_-][A-Za-z0-9][A-Za-z0-9._-]*\.md$", RegexOptions.CultureInvariant), "postmortem-route-filename-invalid");
        RequireCreateNewPostmortem(root, postmortem);
    }

    private static (string Gate, string Receipt) WritePostmortemBinding(string root, string sessionHash, string head,
        string[] incidents, string postmortem, string confirmationSha256, string toolUseId,
        string authorization = "confirmation", string? intent = null, string? intentSha256 = null)
    {
        var gatePath = GatePath(root, sessionHash, "postmortem");
        Ensure(!File.Exists(gatePath), "postmortem-route-gate-already-exists");
        var bindingRelative = BindingRelativePath(sessionHash);
        var bindingPath = Path.Combine(root, bindingRelative.Replace('/', Path.DirectorySeparatorChar));
        Ensure(!File.Exists(bindingPath), "postmortem-route-binding-receipt-exists");
        var gateRelative = Path.GetRelativePath(root, gatePath).Replace('\\', '/');
        var incidentHashes = incidents.ToDictionary(incident => incident,
            incident => FileSha256(UniqueIncidentPath(root, incident)), StringComparer.Ordinal);
        try
        {
            WriteGate(root, "postmortem", sessionHash, gatePath,
                NewArguments(postmortem: postmortem, incidents: incidents));
            Ensure(head == OperationsLearningPolicy.GitHead(root), "postmortem-route-head-changed");
            var correlationId = Guid.NewGuid().ToString("N");
            using var stream = new FileStream(bindingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, new PostmortemBindingRecord(
                "1.0", sessionHash, head, incidents, postmortem,
                gateRelative, FileSha256(gatePath), confirmationSha256,
                toolUseId, correlationId, authorization, intent, intentSha256, incidentHashes));
            stream.Flush(flushToDisk: true);
        }
        catch
        {
            if (File.Exists(bindingPath)) File.Delete(bindingPath);
            if (File.Exists(gatePath)) File.Delete(gatePath);
            throw;
        }
        return (gateRelative, bindingRelative);
    }

    private static string UniqueIncidentPath(string root, string incident)
    {
        var matches = Directory.GetFiles(Path.Combine(root, ".engloop", "incidents"), incident + "*.md")
            .Where(path => Regex.IsMatch(Path.GetFileName(path), "^" + Regex.Escape(incident) + @"(?:[_-].+)?\.md$", RegexOptions.CultureInvariant))
            .ToArray();
        Ensure(matches.Length == 1, "postmortem-route-incident-not-unique:" + incident);
        return matches[0];
    }

    private static bool IsAffirmativePlanCompletion(string prompt)
        => Regex.IsMatch(prompt,
            @"\A\s*(?:please\s+)?complete\s+the\s+plan(?:\.{3}|,|\.)?\s*today\s+(?:FB|Facebook)\s+first(?:,)?\s+then\s+fix\s+live[.!]?\s*\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static (string[] Incidents, string? Postmortem, string? IncidentDate)? ReadExplicitPostmortemIntent(
        string prompt, string? root = null, string? incidentDate = null, string? historicalPostmortem = null)
    {
        prompt = Regex.Replace(prompt, @"`(?<path>\.engloop/postmortems/PM\d{3}[-_][A-Za-z0-9][A-Za-z0-9._-]*\.md)`", "${path}", RegexOptions.CultureInvariant);
        var numberedMatch = Regex.Match(prompt,
            @"\A\s*(?:(?:please|go ahead and)\s+)?(?:complete|create|start|continue|write)\s+(?<postmortemId>PM\d{3})\s+for\s+(?:incident\s+)?(?<incident>IN\d{3})(?:\s+at\s+(?<postmortem>\.engloop/postmortems/PM\d{3}[-_][A-Za-z0-9][A-Za-z0-9._-]*\.md))?[.!]?\s*\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (numberedMatch.Success)
        {
            if (root is null) return null;
            var postmortemId = numberedMatch.Groups["postmortemId"].Value.ToUpperInvariant();
            var expectedId = historicalPostmortem is null ? NextRegistryId(root, "PM")
                : Regex.Match(Path.GetFileName(historicalPostmortem), @"^PM\d{3}", RegexOptions.CultureInvariant).Value;
            if (!string.Equals(postmortemId, expectedId, StringComparison.Ordinal)) return null;
            var incident = numberedMatch.Groups["incident"].Value.ToUpperInvariant();
            var postmortem = numberedMatch.Groups["postmortem"].Success
                ? PostmortemPath(numberedMatch.Groups["postmortem"].Value)
                : historicalPostmortem ?? DefaultPostmortemPath(root, [incident], null);
            var pathId = Regex.Match(Path.GetFileName(postmortem), @"^PM\d{3}", RegexOptions.CultureInvariant).Value;
            if (!string.Equals(pathId, postmortemId, StringComparison.Ordinal)) return null;
            return ([incident], postmortem, null);
        }
        var match = Regex.Match(prompt,
            @"\A\s*(?:(?:please|go ahead and)\s+)?(?:complete|create|start|continue|write)\s+(?:(?:the|a)\s+)?post[ -]?mortem\s+for\s+(?:incident\s+)?(?<incident>IN\d{3})(?:\s+at\s+(?<postmortem>\.engloop/postmortems/PM\d{3}[-_][A-Za-z0-9][A-Za-z0-9._-]*\.md))?(?:(?:\s+and|,?\s+then)\s+(?:implement|complete|execute)\s+(?:all\s+)?(?:the\s+)?repair\s+items)?[.!]?\s*\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (match.Success) return ([match.Groups["incident"].Value.ToUpperInvariant()],
            match.Groups["postmortem"].Success ? match.Groups["postmortem"].Value : null, null);
        if (root is null) return null;
        if (IsAffirmativeTodayPostmortem(prompt))
        {
            var selectedDate = incidentDate ?? DateTime.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            var incidentsForToday = IncidentIdsForDate(root, selectedDate);
            return incidentsForToday.Length == 0 ? null : (incidentsForToday, null, selectedDate);
        }
        if (!IsAffirmativePlanCompletion(prompt)) return null;
        var incidents = Directory.GetFiles(Path.Combine(root, ".engloop", "incidents"), "IN*.md")
            .Where(path => Regex.IsMatch(Path.GetFileName(path), @"^IN\d{3}[-_].*\bfacebook\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Select(path => Path.GetFileName(path)[..5]).Distinct(StringComparer.Ordinal).ToArray();
        return incidents.Length == 1 ? ([incidents[0]], null, null) : null;
    }

    private static bool IsAffirmativeTodayPostmortem(string prompt)
        => Regex.IsMatch(prompt,
            @"\A\s*(?:(?:please|go ahead and)\s+)?(?:complete|create|start|continue|write)\s+(?:(?:the|a)\s+)?post[ -]?mortem\s+for\s+(?:all\s+of\s+)?today(?:'s|\u2019s)\s+incidents[.!]?\s*\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string[] IncidentIdsForDate(string root, string incidentDate)
    {
        Ensure(Regex.IsMatch(incidentDate, @"^\d{8}$", RegexOptions.CultureInvariant), "postmortem-route-incident-date-invalid");
        var incidentsPath = Path.Combine(root, ".engloop", "incidents");
        if (!Directory.Exists(incidentsPath)) return [];
        var incidents = Directory.GetFiles(incidentsPath, "IN*.md")
            .Select(path => Regex.Match(Path.GetFileName(path), @"^IN(?<id>\d{3})[-_](?<date>\d{8})[-_].+\.md$", RegexOptions.CultureInvariant))
            .Where(match => match.Success && match.Groups["date"].Value == incidentDate)
            .Select(match => "IN" + match.Groups["id"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(incident => incident, StringComparer.Ordinal)
            .ToArray();
        foreach (var incident in incidents) _ = UniqueIncidentPath(root, incident);
        return incidents;
    }

    private static (string[] Incidents, string? Postmortem, string? IncidentDate)? ReadBoundDatedPostmortemIntent(
        string root, string sessionHash, string prompt)
    {
        if (!IsAffirmativeTodayPostmortem(prompt)) return null;
        var gatePath = GatePath(root, sessionHash, "postmortem");
        var bindingPath = Path.Combine(root, BindingRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(gatePath) || !File.Exists(bindingPath)) return null;

        var gate = ReadGate(gatePath);
        var binding = JsonSerializer.Deserialize<PostmortemBindingRecord>(File.ReadAllText(bindingPath));
        if (binding is null || gate.SchemaVersion != "1.0" || gate.Mode != "postmortem"
            || gate.SessionHash != sessionHash || string.IsNullOrWhiteSpace(gate.Postmortem)
            || gate.Incidents is not { Length: > 0 } || binding.SessionHash != sessionHash
            || binding.Head != gate.Head || binding.Postmortem != gate.Postmortem
            || binding.Incidents is not { Length: > 0 }
            || !binding.Incidents.SequenceEqual(gate.Incidents, StringComparer.Ordinal)
            || binding.Authorization is not ("explicit-operator-intent" or "confirmation")) return null;

        var intentPath = Path.Combine(root, PostmortemIntentRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));
        string? incidentDate = null;
        if (binding.Authorization == "explicit-operator-intent")
        {
            if (!File.Exists(intentPath)) return null;
            var intent = JsonSerializer.Deserialize<PostmortemOperatorIntentRecord>(File.ReadAllText(intentPath));
            if (intent is null || intent.SessionHash != sessionHash) return null;
            incidentDate = intent.IncidentDate;
        }
        else if (File.Exists(intentPath)) return null;

        incidentDate ??= IncidentDateForSet(root, binding.Incidents);
        if (incidentDate is null
            || !IncidentIdsForDate(root, incidentDate).SequenceEqual(binding.Incidents, StringComparer.Ordinal)) return null;
        return (binding.Incidents, binding.Postmortem, incidentDate);
    }

    private static string? IncidentDateForSet(string root, string[] incidents)
    {
        var dates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var incident in incidents)
        {
            var match = Regex.Match(Path.GetFileName(UniqueIncidentPath(root, incident)),
                @"^IN\d{3}[-_](?<date>\d{8})[-_].+\.md$", RegexOptions.CultureInvariant);
            if (!match.Success) return null;
            dates.Add(match.Groups["date"].Value);
        }
        return dates.Count == 1 ? dates.Single() : null;
    }

    private static string ResolvePostmortemSession(string root, string nativeSessionHash)
    {
        var directory = Path.Combine(root, ".engloop", "out", "postmortem-context");
        if (!Directory.Exists(directory)) return nativeSessionHash;
        var records = Directory.GetFiles(directory, nativeSessionHash + ".reauthorization.*.json")
            .Select(path => JsonSerializer.Deserialize<PostmortemReauthorizationRecord>(File.ReadAllText(path))
                ?? throw new InvalidOperationException("postmortem-route-reauthorization-json-invalid")).ToArray();
        var current = nativeSessionHash;
        var visited = new HashSet<string>(StringComparer.Ordinal) { current };
        while (records.Any(record => record.PreviousSessionHash == current))
        {
            var children = records.Where(record => record.PreviousSessionHash == current).ToArray();
            Ensure(children.Length == 1, "postmortem-route-reauthorization-lineage-ambiguous");
            var record = children[0];
            Ensure(record.SchemaVersion == "1.0" && record.NativeSessionHash == nativeSessionHash
                && Regex.IsMatch(record.PreviousSessionHash, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)
                && Regex.IsMatch(record.SessionHash, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)
                && visited.Add(record.SessionHash)
                && record.PromptSha256 == Sha256(Encoding.UTF8.GetBytes(record.Prompt))
                && ReadExplicitPostmortemIntent(record.Prompt, root, record.IncidentDate, record.Postmortem) is { } reauthorizationRequest
                && (record.Incidents is null || reauthorizationRequest.Incidents.SequenceEqual(record.Incidents, StringComparer.Ordinal))
                && (record.IncidentDate is null || reauthorizationRequest.IncidentDate == record.IncidentDate),
                "postmortem-route-reauthorization-identity-invalid");
            var previousBindingPath = Path.Combine(root, BindingRelativePath(record.PreviousSessionHash).Replace('/', Path.DirectorySeparatorChar));
            var previousBinding = JsonSerializer.Deserialize<PostmortemBindingRecord>(File.ReadAllText(previousBindingPath))!;
            Ensure(previousBinding.Authorization is "explicit-operator-intent" or "confirmation",
                "postmortem-route-reauthorization-authorization-invalid");
            var previousIntentPath = Path.Combine(root, PostmortemIntentRelativePath(record.PreviousSessionHash).Replace('/', Path.DirectorySeparatorChar));
            var requiredPaths = new List<string> { GatePath(root, record.PreviousSessionHash, "postmortem"),
                previousBindingPath };
            if (!record.ScopeChange) requiredPaths.Add(PostmortemHostPath(root, record.PreviousSessionHash));
            if (previousBinding.Authorization == "explicit-operator-intent" && !record.ScopeChange) requiredPaths.Add(previousIntentPath);
            var requiredRelativePaths = requiredPaths
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).ToArray();
            var preservedArtifacts = record.PreservedArtifacts
                ?? throw new InvalidOperationException("postmortem-route-reauthorization-history-missing");
            Ensure(requiredRelativePaths.All(preservedArtifacts.ContainsKey),
                "postmortem-route-reauthorization-history-missing");
            foreach (var artifact in preservedArtifacts)
            {
                var relative = artifact.Key;
                var expectedPaths = new List<string> { GatePath(root, record.PreviousSessionHash, "postmortem"),
                    PostmortemHostPath(root, record.PreviousSessionHash), RepairIntentPath(root, record.PreviousSessionHash),
                    previousBindingPath };
                if (previousBinding.Authorization == "explicit-operator-intent") expectedPaths.Add(previousIntentPath);
                var expectedRelativePaths = expectedPaths.Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'));
                Ensure(expectedRelativePaths.Contains(relative, StringComparer.Ordinal)
                    && FileSha256(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))) == artifact.Value,
                    "postmortem-route-reauthorization-history-changed");
            }
            var bindingPath = Path.Combine(root, BindingRelativePath(record.SessionHash).Replace('/', Path.DirectorySeparatorChar));
            Ensure(FileSha256(bindingPath) == record.BindingSha256, "postmortem-route-reauthorization-binding-changed");
            var binding = JsonSerializer.Deserialize<PostmortemBindingRecord>(File.ReadAllText(bindingPath))!;
            var intentPath = Path.Combine(root, PostmortemIntentRelativePath(record.SessionHash).Replace('/', Path.DirectorySeparatorChar));
            var intent = JsonSerializer.Deserialize<PostmortemOperatorIntentRecord>(File.ReadAllText(intentPath))!;
            Ensure(binding.Postmortem == record.Postmortem && binding.SessionHash == record.SessionHash
                && binding.Authorization == "explicit-operator-intent"
                && binding.IntentSha256 == FileSha256(intentPath) && intent.Prompt == record.Prompt
                && intent.PromptSha256 == record.PromptSha256 && intent.Postmortem == record.Postmortem
                && (record.Incidents is null || record.Incidents.SequenceEqual(binding.Incidents, StringComparer.Ordinal))
                && (record.IncidentDate is null || record.IncidentDate == intent.IncidentDate)
                && previousBinding.Postmortem == record.HistoricalPostmortem
                && (record.ScopeChange
                    ? !previousBinding.Incidents.SequenceEqual(binding.Incidents, StringComparer.Ordinal)
                        && record.Incidents is not null && record.Incidents.SequenceEqual(binding.Incidents, StringComparer.Ordinal)
                    : previousBinding.Incidents.SequenceEqual(binding.Incidents, StringComparer.Ordinal)),
                "postmortem-route-reauthorization-scope-changed");
            current = record.SessionHash;
        }
        Ensure(visited.Count == records.Length + 1, "postmortem-route-reauthorization-lineage-disconnected");
        return current;
    }

    private static int InitializeNewPostmortemScope(string root, string nativeSessionHash, string previousSessionHash,
        JsonElement input, (string[] Incidents, string? Postmortem, string? IncidentDate) request)
    {
        var previousGatePath = GatePath(root, previousSessionHash, "postmortem");
        SuspendPostmortemContext(previousGatePath, "operations-hook-followup-arguments-changed");
        RequirePostmortemAgentEntry(root);
        var previousBindingPath = Path.Combine(root, BindingRelativePath(previousSessionHash).Replace('/', Path.DirectorySeparatorChar));
        var previousGate = ReadGate(previousGatePath);
        var previousBinding = JsonSerializer.Deserialize<PostmortemBindingRecord>(File.ReadAllText(previousBindingPath))
            ?? throw new InvalidOperationException("postmortem-route-binding-json-invalid");
        Ensure(previousGate.SchemaVersion == "1.0" && previousGate.Mode == "postmortem"
            && previousGate.SessionHash == previousSessionHash && previousGate.ArgumentsHash == HashArguments(previousGate)
            && previousBinding.SchemaVersion == "1.0" && previousBinding.SessionHash == previousSessionHash
            && previousBinding.Head == previousGate.Head && previousBinding.GateSha256 == FileSha256(previousGatePath)
            && previousBinding.Gate == Path.GetRelativePath(root, previousGatePath).Replace('\\', '/')
            && previousBinding.Postmortem == previousGate.Postmortem && previousGate.Incidents is not null
            && previousBinding.Incidents.SequenceEqual(previousGate.Incidents, StringComparer.Ordinal)
            && previousBinding.Authorization is "confirmation" or "explicit-operator-intent",
            "postmortem-route-new-scope-history-invalid");
        var postmortem = PostmortemPath(request.Postmortem ?? DefaultPostmortemPath(root, request.Incidents, request.IncidentDate));
        ValidatePostmortemRouteScope(root, request.Incidents, postmortem);
        var generation = Sha256(Encoding.UTF8.GetBytes(nativeSessionHash + Guid.NewGuid().ToString("N")));
        var preserved = new[] { previousGatePath, previousBindingPath, PostmortemHostPath(root, previousSessionHash),
            Path.Combine(root, PostmortemIntentRelativePath(previousSessionHash).Replace('/', Path.DirectorySeparatorChar)),
            RepairIntentPath(root, previousSessionHash) }.Where(File.Exists)
            .ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), FileSha256);
        WriteGate(root, "postmortem-host", generation, PostmortemHostPath(root, generation), NewArguments());
        var prompt = ReadString(input, "prompt");
        InitializeExplicitPostmortem(root, generation, GatePath(root, generation, "postmortem"), prompt,
            input, request.Incidents, postmortem, request.IncidentDate, emitContext: false);
        var record = new PostmortemReauthorizationRecord("1.0", nativeSessionHash, previousSessionHash, generation,
            prompt, Sha256(Encoding.UTF8.GetBytes(prompt)), previousGate.Postmortem!, postmortem, preserved,
            FileSha256(Path.Combine(root, BindingRelativePath(generation).Replace('/', Path.DirectorySeparatorChar))),
            request.Incidents, request.IncidentDate, ScopeChange: true);
        var lineagePath = Path.Combine(root, ".engloop", "out", "postmortem-context", nativeSessionHash + ".reauthorization." + generation + ".json");
        using (var stream = new FileStream(lineagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, record);
            stream.Flush(flushToDisk: true);
        }
        Ensure(ResolvePostmortemSession(root, nativeSessionHash) == generation, "postmortem-route-new-scope-not-active");
        WriteHookContext("UserPromptSubmit", PostmortemActivationContext(root, generation,
            GatePath(root, generation, "postmortem"), ReadGate(GatePath(root, generation, "postmortem"))));
        return 0;
    }

    private static bool RequiresFreshPostmortemIdentity(string root, string sessionHash)
    {
        var hostPath = PostmortemHostPath(root, sessionHash);
        if (!File.Exists(hostPath) || !File.Exists(GatePath(root, sessionHash, "postmortem"))) return false;
        var host = ReadGate(hostPath);
        var identity = CurrentToolIdentity(root);
        return host.AssemblyPath != identity.AssemblyPath || host.AssemblySha256 != identity.AssemblySha256
            || host.ToolVersion != identity.ToolVersion || host.ManifestPath != identity.ManifestPath
            || host.ManifestSha256 != identity.ManifestSha256;
    }

    private static int ReauthorizePostmortem(string root, string nativeSessionHash, string previousSessionHash,
        JsonElement input, (string[] Incidents, string? Postmortem, string? IncidentDate) request)
    {
        RequirePostmortemAgentEntry(root);
        var oldGatePath = GatePath(root, previousSessionHash, "postmortem");
        var oldBindingPath = Path.Combine(root, BindingRelativePath(previousSessionHash).Replace('/', Path.DirectorySeparatorChar));
        var oldIntentPath = Path.Combine(root, PostmortemIntentRelativePath(previousSessionHash).Replace('/', Path.DirectorySeparatorChar));
        var oldHostPath = PostmortemHostPath(root, previousSessionHash);
        var oldGate = ReadGate(oldGatePath);
        var oldHost = ReadGate(oldHostPath);
        var oldBinding = JsonSerializer.Deserialize<PostmortemBindingRecord>(File.ReadAllText(oldBindingPath))!;
        var oldIntent = File.Exists(oldIntentPath)
            ? JsonSerializer.Deserialize<PostmortemOperatorIntentRecord>(File.ReadAllText(oldIntentPath))
            : null;
        var historicalRequest = oldIntent is null ? null
            : ReadExplicitPostmortemIntent(oldIntent.Prompt, root, oldIntent.IncidentDate, oldIntent.Postmortem);
        var oldIncidents = oldGate.Incidents ?? [];
        var oldIntentIncidents = oldIntent is null ? []
            : oldIntent.Incidents is { Length: > 0 } ? oldIntent.Incidents : [oldIntent.Incident];
        var oldIntentValid = oldBinding.Authorization == "explicit-operator-intent"
            && oldIntent is not null && historicalRequest is not null
            && oldIntent.SchemaVersion == "1.0" && oldIntent.SessionHash == previousSessionHash && oldIntent.Head == oldGate.Head
            && oldIntent.HostSha256 == FileSha256(oldHostPath) && oldIntent.PromptSha256 == Sha256(Encoding.UTF8.GetBytes(oldIntent.Prompt))
            && historicalRequest.Value.Incidents.SequenceEqual(oldIncidents, StringComparer.Ordinal)
            && (historicalRequest.Value.Postmortem is null || historicalRequest.Value.Postmortem == oldIntent.Postmortem)
            && oldIncidents.Length > 0 && oldIntentIncidents.SequenceEqual(oldIncidents, StringComparer.Ordinal)
            && oldIntent.Incident == oldIncidents[0] && oldIntent.Postmortem == oldGate.Postmortem
            && oldBinding.Intent == PostmortemIntentRelativePath(previousSessionHash)
            && oldBinding.IntentSha256 == FileSha256(oldIntentPath)
            && (oldBinding.IncidentHashes is null || IncidentHashesMatch(root, oldIncidents, oldBinding.IncidentHashes))
            && PostmortemIntentEvidenceMatches(root, oldIntent, oldIncidents)
            && oldIntent.NorthstarSha256 == FileSha256(Path.Combine(root, "NORTHSTAR.md"))
            && oldBinding.ConfirmationSha256 == string.Empty && oldBinding.ToolUseId == string.Empty;
        var oldConfirmationValid = oldBinding.Authorization == "confirmation" && oldIntent is null
            && !File.Exists(oldIntentPath)
            && Regex.IsMatch(oldBinding.ConfirmationSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)
            && !string.IsNullOrWhiteSpace(oldBinding.ToolUseId) && oldBinding.Intent is null && oldBinding.IntentSha256 is null
            && IncidentBindingEvidenceMatches(root, oldGate.Head, oldIncidents, oldBinding.IncidentHashes);
        Ensure(oldGate.SchemaVersion == "1.0" && oldGate.Mode == "postmortem" && oldGate.SessionHash == previousSessionHash
            && oldGate.Head == OperationsLearningPolicy.GitHead(root) && oldGate.ArgumentsHash == HashArguments(oldGate)
            && oldHost.SchemaVersion == "1.0" && oldHost.Mode is "postmortem-host" or "postmortem-reauthorization-host" && oldHost.SessionHash == previousSessionHash
            && oldHost.Head == oldGate.Head && oldHost.ArgumentsHash == HashArguments(NewArguments())
            && oldGate.AssemblyPath == oldHost.AssemblyPath && oldGate.AssemblySha256 == oldHost.AssemblySha256
            && oldGate.ToolVersion == oldHost.ToolVersion && oldGate.ManifestPath == oldHost.ManifestPath
            && oldGate.ManifestSha256 == oldHost.ManifestSha256
            && oldBinding.SchemaVersion == "1.0" && oldBinding.SessionHash == previousSessionHash && oldBinding.Head == oldGate.Head
            && oldBinding.Gate == Path.GetRelativePath(root, oldGatePath).Replace('\\', '/')
            && oldBinding.GateSha256 == FileSha256(oldGatePath) && oldBinding.Postmortem == oldGate.Postmortem
            && Regex.IsMatch(oldBinding.CorrelationId, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant)
            && oldGate.Incidents is not null && oldBinding.Incidents.SequenceEqual(oldIncidents, StringComparer.Ordinal)
            && oldIncidents.Length > 0 && oldIncidents.SequenceEqual(request.Incidents, StringComparer.Ordinal)
            && (request.Postmortem is null || request.Postmortem == oldGate.Postmortem)
            && (oldIntentValid || oldConfirmationValid)
            && !TryReadPostmortemContext(oldGatePath, out _), "postmortem-route-reauthorization-history-invalid");
        var prompt = ReadString(input, "prompt");
        var preserveDatedRoute = request.IncidentDate is not null
            && (oldIntent is null || oldIntent.IncidentDate == request.IncidentDate)
            && oldIncidents.SequenceEqual(request.Incidents, StringComparer.Ordinal);
        var postmortem = PostmortemPath(preserveDatedRoute
            ? oldGate.Postmortem!
            : request.Postmortem ?? DefaultPostmortemPath(root, request.Incidents, request.IncidentDate));
        ValidatePostmortemRouteScope(root, oldIncidents, postmortem);
        var generation = Sha256(Encoding.UTF8.GetBytes(nativeSessionHash + Guid.NewGuid().ToString("N")));
        var preserved = new[] { oldGatePath, oldBindingPath, oldIntentPath, oldHostPath, RepairIntentPath(root, previousSessionHash) }
            .Where(File.Exists).ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), FileSha256);
        WriteGate(root, "postmortem-reauthorization-host", generation, PostmortemHostPath(root, generation), NewArguments());
        InitializeExplicitPostmortem(root, generation, GatePath(root, generation, "postmortem"), prompt,
            input, oldIncidents, postmortem, request.IncidentDate, emitContext: false);
        var record = new PostmortemReauthorizationRecord("1.0", nativeSessionHash, previousSessionHash, generation,
            prompt, Sha256(Encoding.UTF8.GetBytes(prompt)), oldGate.Postmortem!, postmortem, preserved,
            FileSha256(Path.Combine(root, BindingRelativePath(generation).Replace('/', Path.DirectorySeparatorChar))),
            oldIncidents, request.IncidentDate);
        var lineagePath = Path.Combine(root, ".engloop", "out", "postmortem-context", nativeSessionHash + ".reauthorization." + generation + ".json");
        using (var stream = new FileStream(lineagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, record);
            stream.Flush(flushToDisk: true);
        }
        Ensure(ResolvePostmortemSession(root, nativeSessionHash) == generation, "postmortem-route-reauthorization-not-active");
        WriteHookContext("UserPromptSubmit", PostmortemActivationContext(root, generation,
            GatePath(root, generation, "postmortem"), ReadGate(GatePath(root, generation, "postmortem"))));
        return 0;
    }

    private static string PostmortemHostPath(string root, string sessionHash)
        => Path.Combine(root, ".engloop", "out", "postmortem-context", sessionHash + ".host.json");

    private static void RefreshUnboundPostmortemHost(string root, string sessionHash, string hostPath)
    {
        var bindingPath = Path.Combine(root, BindingRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));
        var intentPath = Path.Combine(root, PostmortemIntentRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(GatePath(root, sessionHash, "postmortem")) || File.Exists(bindingPath)
            || File.Exists(intentPath) || File.Exists(RepairIntentPath(root, sessionHash))) return;

        var host = ReadGate(hostPath);
        if (host.SchemaVersion != "1.0" || host.Mode != "postmortem-host" || host.SessionHash != sessionHash
            || host.ArgumentsHash != HashArguments(NewArguments())) return;

        var currentHead = OperationsLearningPolicy.GitHead(root);
        var identity = CurrentToolIdentity(root);
        if (host.Head == currentHead && host.AssemblyPath == identity.AssemblyPath
            && host.AssemblySha256 == identity.AssemblySha256 && host.ToolVersion == identity.ToolVersion
            && host.ManifestPath == identity.ManifestPath && host.ManifestSha256 == identity.ManifestSha256) return;

        WriteGate(root, "postmortem-host", sessionHash, hostPath, NewArguments());
    }

    private static string PostmortemIntentRelativePath(string sessionHash)
        => $".engloop/out/postmortem-context/{sessionHash}.operator-intent.json";

    private static GateRecord ReadPostmortemHost(string root, string sessionHash, bool nativeReauthorization = false)
    {
        var path = PostmortemHostPath(root, sessionHash);
        Ensure(File.Exists(path), "postmortem-route-host-session-missing");
        var host = ReadGate(path);
        var authorizedPromptIdentity = host.Mode == "postmortem-reauthorization-host"
            && (nativeReauthorization || Directory.GetFiles(Path.GetDirectoryName(path)!, "*.reauthorization." + sessionHash + ".json").Length == 1);
        Ensure(host.SchemaVersion == "1.0" && (host.Mode == "postmortem-host" || authorizedPromptIdentity) && host.SessionHash == sessionHash
            && host.Head == OperationsLearningPolicy.GitHead(root) && host.ArgumentsHash == HashArguments(NewArguments()),
            "postmortem-route-host-session-identity-invalid");
        ValidateToolIdentity(root, host);
        return host;
    }

    private static int InitializeExplicitPostmortem(string root, string sessionHash, string gatePath,
        string prompt, JsonElement input, string[] incidents, string? requestedPostmortem,
        string? incidentDate = null, bool emitContext = true)
    {
        Ensure(incidents.Length > 0, "postmortem-route-incident-not-found");
        Ensure(ReadString(input, "hookEventName", "hook_event_name") == "UserPromptSubmit", "postmortem-route-host-prompt-event-missing");
        var host = ReadPostmortemHost(root, sessionHash, nativeReauthorization: !emitContext);
        if (File.Exists(gatePath))
        {
            var gate = ReadGate(gatePath);
            Ensure(gate.SchemaVersion == "1.0" && gate.Mode == "postmortem" && gate.SessionHash == sessionHash
                && gate.Head == host.Head && HashArguments(gate) == gate.ArgumentsHash, "operations-hook-gate-identity-invalid");
            if (gate.Incidents is null || !gate.Incidents.SequenceEqual(incidents, StringComparer.Ordinal)
                || requestedPostmortem is not null && gate.Postmortem != requestedPostmortem)
            {
                SuspendPostmortemContext(gatePath, "operations-hook-followup-arguments-changed");
                throw new InvalidOperationException("operations-hook-followup-arguments-changed");
            }
            ValidateToolIdentity(root, gate);
            ValidatePostmortemBinding(root, sessionHash, gatePath, gate);
            Ensure(!TryReadPostmortemContext(gatePath, out _), "postmortem-route-scope-suspended");
            WriteHookContext("UserPromptSubmit", PostmortemActivationContext(root, sessionHash, gatePath, gate));
            return 0;
        }
        var postmortem = PostmortemPath(requestedPostmortem ?? DefaultPostmortemPath(root, incidents, incidentDate));
        ValidatePostmortemRouteScope(root, incidents, postmortem);
        CaptureRepairIntent(root, sessionHash, prompt);
        var intentRelative = PostmortemIntentRelativePath(sessionHash);
        var intentPath = Path.Combine(root, intentRelative.Replace('/', Path.DirectorySeparatorChar));
        var incidentHashes = incidents.ToDictionary(incident => incident,
            incident => FileSha256(UniqueIncidentPath(root, incident)), StringComparer.Ordinal);
        var record = new PostmortemOperatorIntentRecord("1.0", sessionHash, host.Head,
            FileSha256(PostmortemHostPath(root, sessionHash)), prompt, Sha256(Encoding.UTF8.GetBytes(prompt)), incidents[0], postmortem,
            incidentHashes[incidents[0]], FileSha256(Path.Combine(root, "NORTHSTAR.md")), FileSha256(Path.Combine(root, "LEARNINGS.md")),
            FileSha256(Path.Combine(root, ".engloop", "numbering-registry.md")), incidents, incidentHashes, incidentDate);
        using (var stream = new FileStream(intentPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, record);
            stream.Flush(flushToDisk: true);
        }
        (string Gate, string Receipt) bound;
        try
        {
            bound = WritePostmortemBinding(root, sessionHash, host.Head, incidents, postmortem,
                string.Empty, string.Empty, "explicit-operator-intent", intentRelative, FileSha256(intentPath));
        }
        catch
        {
            File.Delete(intentPath);
            throw;
        }
        ClearPostmortemCollection(root, sessionHash);
        if (emitContext) WriteHookContext("UserPromptSubmit", PostmortemActivationContext(root, sessionHash, gatePath, ReadGate(gatePath)));
        return 0;
    }

    private static string DefaultPostmortemPath(string root, string[] incidents, string? incidentDate)
    {
        if (incidents.Length == 1)
        {
            var incidentPath = UniqueIncidentPath(root, incidents[0]);
            var suffix = Path.GetFileNameWithoutExtension(incidentPath)[incidents[0].Length..].TrimStart('-', '_').Replace('_', '-');
            return $".engloop/postmortems/{NextRegistryId(root, "PM")}-{suffix}.md";
        }

        Ensure(!string.IsNullOrWhiteSpace(incidentDate), "postmortem-route-incident-date-missing");
        var nextPm = NextRegistryId(root, "PM");
        if (incidentDate == "20261005" && nextPm == "PM062"
            && incidents.SequenceEqual(new[] { "IN025", "IN026", "IN027" }, StringComparer.Ordinal))
            return ".engloop/postmortems/PM062-20261005-three-stencil-dispatch-incidents.md";
        return $".engloop/postmortems/{nextPm}-{incidentDate}-incidents-{string.Join("-", incidents.Select(value => value.ToLowerInvariant()))}.md";
    }

    private static string PostmortemActivationContext(string root, string sessionHash, string gatePath, GateRecord gate)
    {
        ValidatePostmortemBinding(root, sessionHash, gatePath, gate);
        var receipt = BindingRelativePath(sessionHash);
        var binding = JsonSerializer.Deserialize<PostmortemBindingRecord>(File.ReadAllText(Path.Combine(root, receipt.Replace('/', Path.DirectorySeparatorChar))))!;
        var directory = Path.Combine(root, ".engloop", "out", "postmortem-context");
        var lineage = Directory.GetFiles(directory, "*.reauthorization." + sessionHash + ".json")
            .Select(path => JsonSerializer.Deserialize<PostmortemReauthorizationRecord>(File.ReadAllText(path))!).SingleOrDefault();
        var history = lineage is null ? string.Empty
            : $" historicalPostmortem={lineage.HistoricalPostmortem} historicalScopeAccepted=false reauthorized=true";
        return $"POSTMORTEM_ROUTE_BOUND OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem gate={binding.Gate} bindingReceipt={receipt} incidents={string.Join(',', gate.Incidents!)} postmortem={gate.Postmortem} authorization={binding.Authorization} correlation={binding.CorrelationId}" + history;
    }

    private static bool TryRestorePostmortemActivation(string root, string sessionHash, JsonElement input)
    {
        if (ReadString(input, "hookEventName", "hook_event_name") != "PostToolUse"
            || !PostmortemRetryTools.Contains(ReadString(input, "tool_name", "toolName"))
            || !TryReadToolElement(input, "tool_response", "toolResponse", out var response)
            || response.ValueKind == JsonValueKind.Object
                && (response.TryGetProperty("isError", out var isError) && isError.ValueKind == JsonValueKind.True
                    || response.TryGetProperty("error", out var error) && error.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            || !File.Exists(Path.Combine(root, BindingRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar))))
            return false;
        try
        {
            RequirePostmortemAgentEntry(root);
            var gatePath = GatePath(root, sessionHash, "postmortem");
            Ensure(!TryReadPostmortemContext(gatePath, out _), "postmortem-route-scope-suspended");
            var gate = ReadGate(gatePath);
            Ensure(gate.SchemaVersion == "1.0" && gate.Mode == "postmortem" && gate.SessionHash == sessionHash
                && gate.Head == OperationsLearningPolicy.GitHead(root), "operations-hook-gate-identity-invalid");
            ValidateToolIdentity(root, gate);
            Ensure(HashArguments(gate) == gate.ArgumentsHash, "operations-hook-gate-arguments-tampered");
            WritePostToolContext(PostmortemActivationContext(root, sessionHash, gatePath, gate));
        }
        catch (Exception ex)
        {
            WritePostToolContext($"OPERATIONS_LEARNING_CONTEXT_REQUIRED activationRestored=false diagnostic={Bound(IncidentDiagnosticCode(ex))}: {Bound(ex.Message)}");
        }
        return true;
    }

    private static int Stop(string root, string mode, string sessionHash, string gatePath, bool subagent)
    {
        var phase = subagent ? "subagent-stop" : "stop";
        if ((mode is "incident" or "repair") && !File.Exists(gatePath))
        {
            WriteQuietResult();
            return 0;
        }
        if (mode == "postmortem" && File.Exists(CancelledCollectionPath(root, sessionHash)))
        {
            CompletePostmortemCancellation(root, sessionHash);
            return 0;
        }
        if (mode == "postmortem" && File.Exists(CollectionPath(root, sessionHash)))
        {
            WritePostmortemCollectionActive(root, sessionHash, phase, "operations-hook-gate-missing");
            return 0;
        }
        if (mode == "postmortem" && TryReadPostmortemContext(gatePath, out var recoveryDiagnostic))
        {
            WritePostmortemContextRequired(phase, recoveryDiagnostic);
            return 0;
        }
        if (mode == "postmortem" && !File.Exists(gatePath))
        {
            if (phase == "stop") WriteQuietResult();
            else WritePostmortemContextRequired(phase, "operations-hook-gate-missing");
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
            _ => ValidateBoundRepairs(root, gate),
        });
        marker = mode == "incident" ? "INCIDENT_CONTEXT_OK" : mode == "postmortem" ? "POSTMORTEM_LEARNING_OK" : "REPAIR_LEARNING_OK";
        if (result != 0)
        {
            WriteLifecycleDeferred(mode, phase, $"operations-hook-{mode}-context-validation-failed",
                $"Operations learning validation failed for mode={mode}: {Bound(diagnostic)}");
            return 0;
        }
        if (mode == "postmortem" && ReadRepairIntent(root, sessionHash) is not null)
            ValidatePostmortemBinding(root, sessionHash, gatePath, gate);
        else File.Delete(gatePath);
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
            InvalidOperationException when exception.Message.StartsWith("operations-hook-", StringComparison.Ordinal)
                || exception.Message.StartsWith("postmortem-route-", StringComparison.Ordinal)
                || exception.Message.StartsWith("repair-continuation-", StringComparison.Ordinal) => Bound(exception.Message),
            InvalidOperationException => "operations-hook-invalid-state",
            _ => "operations-hook-unexpected-failure",
        };

    private static void WriteLifecycleDeferred(string mode, string phase, string diagnosticCode, string diagnostic, string? marker = null, string? correlation = null)
    {
        var missingOption = MissingOption(diagnosticCode + ":" + diagnostic, mode);
        var expectedSource = phase switch
        {
            "start" or "subagent-start" => "Hook input fields cwd and session_id and the repository tool manifest",
            "initialize" => $"UserPromptSubmit.prompt with concrete {mode} scope and repository evidence",
            "stop" or "subagent-stop" => $"A validated {mode} gate and complete evidence for the completion claim",
            _ => "Documented hook input and validated scope records",
        };
        var details = JsonSerializer.Serialize(new
        {
            status = mode == "postmortem" ? "postmortem-context-required" : "learning-context-deferred",
            mode,
            phase,
            command = $"operations-hook {phase} {mode}",
            diagnosticCode = Bound(diagnosticCode),
            diagnostic = Bound(diagnostic),
            missingOption,
            expectedSource,
            elkVersion = typeof(OperationsHookCommands).Assembly.GetName().Version?.ToString(3) ?? "unknown",
            correlation = correlation ?? Guid.NewGuid().ToString("N"),
            completionAccepted = false,
            unresolvedState = "Existing gate, collection, and evidence are retained; missing or invalid scope is not accepted. No completion or stabilization is claimed.",
            remediation = mode == "postmortem" && marker == "OPERATIONS_POSTMORTEM_CONFIRMATION_UNVERIFIED"
                ? "The confirmation prompt response was missing, empty, malformed, or unavailable. This is not an operator decline and does not authorize binding. Retry the prompt once; if the UI or transport still fails, tell the operator confirmation delivery failed and offer a supported host or retry path. Do not ask them to select an unseen dialog or repeat the same blocking claim."
                : $"Continue discussion and local read/search/questions. Resolve the reported {mode} context or evidence before protected operations or completion claims. " + NavigationRecovery,
        });
        WriteResult(true, systemMessage: (marker ?? (mode == "postmortem" ? "OPERATIONS_LEARNING_CONTEXT_REQUIRED" : "OPERATIONS_LEARNING_CONTEXT_DEFERRED")) + " " + details);
    }

    private static string? MissingOption(string diagnostic, string mode)
    {
        var match = Regex.Match(diagnostic, @"operations-hook-option-missing:(?<option>--[a-z-]+)", RegexOptions.CultureInvariant);
        if (match.Success) return Bound(match.Groups["option"].Value);
        return diagnostic.Contains("operations-hook-prompt-missing", StringComparison.Ordinal)
            ? mode switch { "incident" => "--incident", "postmortem" => "--incidents,--postmortem", _ => "--phase,--postmortem,--rpi,--rules,--acceptance" }
            : null;
    }

    private static bool IsPostmortemContextDiagnostic(string diagnostic)
        => diagnostic == "operations-hook-prompt-missing"
            || diagnostic is "operations-hook-path-invalid" or "operations-hook-incident-ids-invalid"
            || diagnostic is "postmortem-route-filename-invalid" or "operations-hook-followup-arguments-changed"
            || diagnostic.StartsWith("operations-hook-option-missing:--postmortem", StringComparison.Ordinal)
            || diagnostic.StartsWith("operations-hook-option-missing:--incidents", StringComparison.Ordinal);

    private static bool IsMissingScopeInput(string mode, string diagnostic)
    {
        if (mode is not ("incident" or "repair")) return false;
        if (diagnostic == "operations-hook-prompt-missing") return true;
        var options = mode == "incident"
            ? new[] { "--incident" }
            : new[] { "--phase", "--postmortem", "--rpi", "--rules", "--acceptance" };
        return options.Any(option => diagnostic.Equals("operations-hook-option-missing:" + option, StringComparison.Ordinal));
    }

    private static string CollectionRelativePath(string sessionHash)
        => $".engloop/out/postmortem-context/{sessionHash}.json";

    private static string CollectionPath(string root, string sessionHash)
        => Path.Combine(root, CollectionRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));

    private static string ConfirmationRelativePath(string sessionHash)
        => $".engloop/out/postmortem-context/{sessionHash}.confirmation.json";

    private static string ConfirmationPath(string root, string sessionHash)
        => Path.Combine(root, ConfirmationRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));

    private static string BindingRelativePath(string sessionHash)
        => $".engloop/out/postmortem-context/{sessionHash}.binding.json";

    private static string BindingFailureRelativePath(string sessionHash)
        => $".engloop/out/postmortem-context/{sessionHash}.binding-failure.json";

    private static string RepairIntentPath(string root, string sessionHash)
        => Path.Combine(root, ".engloop", "out", "operations-learning-gates", sessionHash + ".repair-intent.json");

    private static void CaptureRepairIntent(string root, string sessionHash, string prompt)
    {
        if (!HasAffirmativeRepairRequest(prompt) && !IsAffirmativePlanCompletion(prompt)) return;
        var path = RepairIntentPath(root, sessionHash);
        if (File.Exists(path))
        {
            _ = ReadRepairIntent(root, sessionHash);
            return;
        }

        var identity = CurrentToolIdentity(root);
        var rpis = Regex.Matches(prompt, @"\bRPI\d{3}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var record = new RepairIntentRecord(
            "1.0", sessionHash, OperationsLearningPolicy.GitHead(root) ?? throw new InvalidOperationException("operations-hook-git-head-unavailable"),
            Sha256(Encoding.UTF8.GetBytes(prompt)), rpis.Length == 0, rpis,
            identity.AssemblyPath, identity.AssemblySha256, identity.ToolVersion, identity.ManifestPath, identity.ManifestSha256);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, record);
        stream.Flush(flushToDisk: true);
    }

    private static bool HasAffirmativeRepairRequest(string prompt)
    {
        var request = Regex.Match(prompt,
            @"\b(?:implement|apply|execute|complete)\b(?!\s+(?:not|never))[^\r\n.]{0,120}(?:\brepair\s+items?\b|\bRPI(?:\d{3}|s\b))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!request.Success) return false;
        var prefix = prompt[..request.Index];
        if (prefix.IndexOfAny(['"', '`']) >= 0
            || Regex.IsMatch(prefix, @"\b(?:operator\s+said|quoted|example|plan\s+to|planning\s+to|could\s+we|should\s+we)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        if (Regex.IsMatch(request.Value, @"\b(?:not|never|don't|do\s+not|mustn't|cannot|can't|if|might|whether)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        var sentenceStart = Math.Max(prompt.LastIndexOf('.', Math.Max(0, request.Index - 1)), prompt.LastIndexOf('\n', Math.Max(0, request.Index - 1))) + 1;
        return !Regex.IsMatch(prompt[sentenceStart..request.Index], @"\b(?:not|never|don't|do\s+not|mustn't|cannot|can't|if|might|whether)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static RepairIntentRecord? ReadRepairIntent(string root, string sessionHash)
    {
        var path = RepairIntentPath(root, sessionHash);
        if (!File.Exists(path)) return null;
        var intent = JsonSerializer.Deserialize<RepairIntentRecord>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("repair-continuation-intent-json-invalid");
        var identity = CurrentToolIdentity(root);
        Ensure(intent.SchemaVersion == "1.0" && intent.SessionHash == sessionHash
            && intent.Head == OperationsLearningPolicy.GitHead(root)
            && Regex.IsMatch(intent.PromptSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)
            && intent.AllRepairs == (intent.Rpis.Length == 0)
            && intent.Rpis.All(rpi => Regex.IsMatch(rpi, @"^RPI\d{3}$", RegexOptions.CultureInvariant))
            && intent.Rpis.Distinct(StringComparer.Ordinal).Count() == intent.Rpis.Length
            && intent.AssemblyPath == identity.AssemblyPath && intent.AssemblySha256 == identity.AssemblySha256
            && intent.ToolVersion == identity.ToolVersion && intent.ManifestPath == identity.ManifestPath
            && intent.ManifestSha256 == identity.ManifestSha256,
            "repair-continuation-intent-identity-invalid");
        return intent;
    }

    private static string CancelledCollectionPath(string root, string sessionHash)
        => CollectionPath(root, sessionHash) + ".cancelled";

    private static void BeginPostmortemCollection(string root, string sessionHash, string diagnostic)
    {
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
        ClearPostmortemCollection(root, sessionHash);
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

    private static void ValidateCollection(string root, PostmortemCollectionRecord collection, bool allowToolIdentityUpgrade = false)
    {
        Ensure(collection.SchemaVersion == "1.0", "postmortem-route-collection-schema-invalid");
        Ensure(Regex.IsMatch(collection.SessionHash, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant), "postmortem-route-session-invalid");
        Ensure(Regex.IsMatch(collection.Token, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant), "postmortem-route-token-invalid");
        Ensure(!string.IsNullOrWhiteSpace(collection.AssemblyPath)
            && Regex.IsMatch(collection.AssemblySha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)
            && Regex.IsMatch(collection.ToolVersion, @"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)
            && string.Equals(Path.GetFullPath(collection.ManifestPath), Path.GetFullPath(Path.Combine(root, ".config", "dotnet-tools.json")), StringComparison.OrdinalIgnoreCase)
            && Regex.IsMatch(collection.ManifestSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant),
            "postmortem-route-collection-tool-identity-invalid");
        Ensure(collection.Head == OperationsLearningPolicy.GitHead(root), "postmortem-route-head-changed");
        if (!allowToolIdentityUpgrade)
        {
            var current = CurrentToolIdentity(root);
            Ensure(collection.AssemblyPath == current.AssemblyPath, "postmortem-route-tool-identity-changed:assembly-path");
            Ensure(collection.AssemblySha256 == current.AssemblySha256, "postmortem-route-tool-identity-changed:assembly-hash");
            Ensure(collection.ToolVersion == current.ToolVersion, "postmortem-route-tool-identity-changed:tool-version");
            Ensure(collection.ManifestPath == current.ManifestPath, "postmortem-route-tool-identity-changed:manifest-path");
            Ensure(collection.ManifestSha256 == current.ManifestSha256, "postmortem-route-tool-identity-changed:manifest-hash");
        }
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
        ClearBindingFailure(root, sessionHash);
    }

    private static void ClearConfirmation(string root, string sessionHash)
    {
        var path = ConfirmationPath(root, sessionHash);
        if (File.Exists(path)) File.Delete(path);
    }

    private static void WriteBindingFailure(string root, string sessionHash, string[] incidents,
        string postmortem, Exception exception, string correlationId)
    {
        var relative = BindingFailureRelativePath(sessionHash);
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var record = new PostmortemBindingFailureRecord("1.0", sessionHash,
            OperationsLearningPolicy.GitHead(root) ?? string.Empty, incidents, postmortem,
            IncidentDiagnosticCode(exception), Bound(exception.Message), correlationId,
            DateTimeOffset.UtcNow);
        File.WriteAllText(path, JsonSerializer.Serialize(record));
    }

    private static void ClearBindingFailure(string root, string sessionHash)
    {
        var path = Path.Combine(root, BindingFailureRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));
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
        Ensure(collection.SessionHash == sessionHash, "postmortem-route-collection-session-mismatch");
        if (phase == "stop"
            || diagnostic == "operations-hook-prompt-missing"
            || diagnostic.StartsWith("operations-hook-option-missing:--incidents", StringComparison.Ordinal)
            || diagnostic.StartsWith("operations-hook-option-missing:--postmortem", StringComparison.Ordinal))
        {
            WriteQuietResult();
            return;
        }

        WriteLifecycleDeferred("postmortem", phase, diagnostic,
            "Stage 21 context is incomplete. Continue collecting incident and PM context with read, search, and the confirmation question; no protected operation or completion is authorized.",
            "OPERATIONS_POSTMORTEM_CONTEXT_COLLECTION_ACTIVE OPERATIONS_LEARNING_CONTEXT_REQUIRED");
    }

    private static void WritePostmortemCollectionDecision(bool allow, string reason)
        => Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            stopReason = (string?)null,
            hookSpecificOutput = new
            {
                hookEventName = "PreToolUse",
                permissionDecision = allow ? "allow" : "deny",
                permissionDecisionReason = reason,
            },
        }));

    private static bool IsCollectionReadOrQuestionTool(string toolName)
        => CollectionReadOrQuestionTools.Contains(toolName);

    private static bool IsPostmortemScopeOperationAllowed(string root, GateRecord gate, string toolName, JsonElement input)
    {
        if (IsCollectionReadOrQuestionTool(toolName)
            || toolName is "view" or "grep" or "glob" or "web_search" or "web_fetch" or "get_errors") return true;
        if (!TryReadToolElement(input, "tool_input", "toolInput", out var toolInput)) return false;

        if (toolName == "apply_patch")
        {
            var patch = ReadString(toolInput, "input", "patch", "text");
            if (string.IsNullOrWhiteSpace(patch) || Regex.IsMatch(patch, @"(?m)^\*\*\* Delete File:")) return false;
            var paths = Regex.Matches(patch, @"(?m)^\*\*\* (?:Add|Update) File: (?<path>.+?)\s*$")
                .Select(match => match.Groups["path"].Value.Trim())
                .ToArray();
            return paths.Length > 0 && paths.All(path => IsPostmortemWritablePath(root, gate, path));
        }

        if (toolName is "create_file" or "write_file" or "edit_file" or "insert_edit_into_file" or "edit" or "create")
        {
            var path = ReadString(toolInput, "filePath", "file_path", "path", "targetPath", "target_path", "filename");
            return path.Length > 0 && IsPostmortemWritablePath(root, gate, path);
        }

        if (toolName is "run_in_terminal" or "powershell" or "bash")
        {
            var command = Regex.Replace(ReadString(toolInput, "command", "commandLine", "script"), @"\s+", " ").Trim();
            if (command.IndexOfAny([';', '|', '&', '<', '>', '`', '\r', '\n']) >= 0) return false;
            return Regex.IsMatch(command,
                @"^dotnet tool run engloopkit (?:-- )?validate postmortem-learning --root \. --incidents IN\d{3}(?:,IN\d{3})* --postmortem \.engloop/postmortems/PM\d{3}[-_][A-Za-z0-9._-]+\.md(?: --(?:show-current-hashes|for-repair-routing))?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                || Regex.IsMatch(command, @"^dotnet tool run sek (?:-- )?version$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return false;
    }

    private static int InitializeTrustedRepairContinuation(string root, string sessionHash, string repairGatePath)
    {
        string? postmortem = null;
        string[] requestedRpis = [];
        string? createdRoute = null;
        try
        {
            var postmortemGatePath = GatePath(root, sessionHash, "postmortem");
            Ensure(File.Exists(postmortemGatePath), "repair-continuation-postmortem-gate-missing");
            var postmortemGate = ReadGate(postmortemGatePath);
            postmortem = postmortemGate.Postmortem;
            Ensure(postmortemGate.SchemaVersion == "1.0" && postmortemGate.Mode == "postmortem"
                && postmortemGate.SessionHash == sessionHash
                && postmortemGate.Head == OperationsLearningPolicy.GitHead(root)
                && postmortemGate.Incidents is { Length: > 0 }
                && !string.IsNullOrWhiteSpace(postmortemGate.Postmortem),
                "repair-continuation-postmortem-gate-identity-invalid");
            ValidateToolIdentity(root, postmortemGate);
            Ensure(HashArguments(postmortemGate) == postmortemGate.ArgumentsHash, "repair-continuation-postmortem-gate-tampered");
            Ensure(!TryReadPostmortemContext(postmortemGatePath, out _), "repair-continuation-postmortem-scope-suspended");
            ValidatePostmortemBinding(root, sessionHash, postmortemGatePath, postmortemGate);

            var intent = ReadRepairIntent(root, sessionHash)
                ?? throw new InvalidOperationException("repair-continuation-intent-missing");
            return BindRequestedRepairs(root, sessionHash, repairGatePath, postmortem!, intent);
        }
        catch (Exception ex)
        {
            WriteRepairContinuationBlocker(postmortem, requestedRpis,
                IncidentDiagnosticCode(ex), [ex.Message], createdRoute);
            return 0;
        }
    }

    private static string? ReadExplicitRepairPostmortem(string root, string prompt)
    {
        if (!HasAffirmativeRepairRequest(prompt)) return null;
        var ids = Regex.Matches(prompt, @"\bPM\d{3}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Value.ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return null;
        Ensure(ids.Length == 1, "repair-route-postmortem-ambiguous");
        var paths = Directory.GetFiles(Path.Combine(root, ".engloop", "postmortems"), ids[0] + "*.md")
            .Where(path => Regex.IsMatch(Path.GetFileName(path), "^" + ids[0] + @"(?:[-_].+)?\.md$", RegexOptions.CultureInvariant)).ToArray();
        Ensure(paths.Length == 1, "repair-route-postmortem-not-unique:" + ids[0]);
        var relative = Path.GetRelativePath(root, paths[0]).Replace('\\', '/');
        var namedPaths = Regex.Matches(prompt, @"\.engloop/postmortems/PM\d{3}[-_][A-Za-z0-9][A-Za-z0-9._-]*\.md", RegexOptions.CultureInvariant);
        Ensure(namedPaths.All(match => match.Value == relative), "repair-route-postmortem-path-mismatch");
        return relative;
    }

    private static int AuthorizeRepair(string[] args)
    {
        try
        {
            var root = ExactGitRoot(Path.GetFullPath(CliOption(args, "--root")));
            var request = CliOption(args, "--request");
            var postmortem = ReadExplicitRepairPostmortem(root, request)
                ?? throw new InvalidOperationException("repair-route-explicit-request-required");
            var authorizationId = Guid.NewGuid().ToString("N");
            var authorizationHash = Sha256(Encoding.UTF8.GetBytes("operator-command:" + authorizationId));
            CaptureRepairIntent(root, authorizationHash, request);
            var intent = ReadRepairIntent(root, authorizationHash)
                ?? throw new InvalidOperationException("repair-route-explicit-request-required");
            var gatePath = GatePath(root, authorizationHash, "repair");
            BindRequestedRepairs(root, authorizationHash, gatePath, postmortem, intent, nativeContext: false);
            Ensure(File.Exists(gatePath), "repair-route-authorization-failed");
            Ensure(ValidateBoundRepairs(root, ReadGate(gatePath)) == 0, "repair-route-validation-failed");
            File.WriteAllText(GatePath(root, authorizationHash, "operator-authorization"), JsonSerializer.Serialize(new
            {
                authorization = "explicit-operator-command", authorizationId, request,
                capturedAtUtc = DateTimeOffset.UtcNow, postmortem, gate = gatePath,
                completionAccepted = false,
            }));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int BindRequestedRepairs(string root, string sessionHash, string repairGatePath,
        string postmortem, RepairIntentRecord intent, bool nativeContext = true)
    {
        string[] requestedRpis = [];
        var createdRoutes = new List<string>();
        try
        {
            var validation = OperationsLearningPolicy.ValidatePostmortem(root, postmortem, forRepairRouting: true);
            if (!validation.Passed || validation.Contract is null)
            {
                WriteRepairContinuationBlocker(postmortem, intent.Rpis,
                    "repair-continuation-postmortem-validation-failed", validation.Failures);
                return 0;
            }

            var contract = validation.Contract;
            requestedRpis = intent.AllRepairs
                ? contract.Repairs.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray()
                : intent.Rpis;
            Ensure(requestedRpis.Length > 0, "repair-continuation-no-rpi-requested");
            var missing = requestedRpis.Where(rpi => !contract.Repairs.ContainsKey(rpi)).ToArray();
            Ensure(missing.Length == 0, "repair-continuation-rpi-not-in-postmortem:" + string.Join(',', missing));
            foreach (var selectedRpi in requestedRpis)
            {
                var selectedRepair = contract.Repairs[selectedRpi];
                var selectedAcceptance = $".engloop/repairs/{contract.PostmortemId}-{selectedRpi}.route.json";
                var selectedFullPath = Path.Combine(root, selectedAcceptance.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(selectedFullPath))
                {
                    RequireCreateNewAcceptance(root, selectedAcceptance, "route");
                    WriteRepairRouteRecord(selectedFullPath, contract, selectedRepair);
                    createdRoutes.Add(selectedFullPath);
                }
                var routeValidation = OperationsLearningPolicy.ValidateRepairAcceptance(root, postmortem, selectedRpi,
                    selectedRepair.RuleIds, selectedAcceptance, "route", currentReadinessPass: false);
                Ensure(routeValidation.Passed, "repair-continuation-route-validation-failed:" + string.Join(',', routeValidation.Failures));
            }
            var rpi = requestedRpis[0];
            var repair = contract.Repairs[rpi];
            var acceptance = $".engloop/repairs/{contract.PostmortemId}-{rpi}.route.json";
            var arguments = NewArguments(postmortem: postmortem, phase: "route", rpi: rpi,
                rules: repair.RuleIds.ToArray(), acceptance: acceptance, requestedRpis: requestedRpis.Length > 1 ? requestedRpis : null);
            if (File.Exists(repairGatePath))
            {
                var existing = ReadGate(repairGatePath);
                Ensure(existing.SchemaVersion == "1.0" && existing.Mode == "repair"
                    && existing.SessionHash == sessionHash && existing.Head == OperationsLearningPolicy.GitHead(root)
                    && HashArguments(existing) == existing.ArgumentsHash
                    && HashArguments(existing) == HashArguments(arguments),
                    "repair-continuation-existing-gate-mismatch");
                ValidateToolIdentity(root, existing);
            }
            else WriteGate(root, "repair", sessionHash, repairGatePath, arguments);

            var completion = OperationsLearningPolicy.ValidatePostmortem(root, postmortem);
            var context = $"OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair gate={repairGatePath} continuation=true postmortem={postmortem} rpi={rpi} route={acceptance} requestedRpis={string.Join(',', requestedRpis)}. Implement every selected item under its own exact immutable route; unresolved completion evidence: {string.Join(',', completion.Failures)}. Repair authorization is not incident, PM, deployment or readiness completion. Do not stop or send the operator back for open stabilization; collect remaining evidence during the repair.";
            if (nativeContext) WriteHookContext("UserPromptSubmit", context);
            else Console.WriteLine("OPERATIONS_LEARNING_GUARD_ACTIVE mode=repair authorization=explicit-operator-command " + context);
            return 0;
        }
        catch (Exception ex)
        {
            foreach (var path in createdRoutes) if (File.Exists(path)) File.Delete(path);
            WriteRepairContinuationBlocker(postmortem, requestedRpis,
                IncidentDiagnosticCode(ex), [ex.Message]);
            return 0;
        }
    }

    private static int ValidateBoundRepairs(string root, GateRecord gate)
    {
        if (gate.Phase != "route" || gate.RequestedRpis is null)
            return ValidationCommands.ValidateRepairLearning(["--root", root, "--phase", gate.Phase!, "--postmortem", gate.Postmortem!, "--rpi", gate.Rpi!, "--rules", string.Join(',', gate.Rules!), "--acceptance", gate.Acceptance!]);
        var pm = OperationsLearningPolicy.ValidatePostmortem(root, gate.Postmortem!, forRepairRouting: true);
        Ensure(pm.Passed && pm.Contract is not null, "repair-route-contract-invalid:" + string.Join(',', pm.Failures));
        foreach (var rpi in gate.RequestedRpis)
        {
            Ensure(pm.Contract!.Repairs.ContainsKey(rpi), "repair-contract-missing:" + rpi);
            var repair = pm.Contract.Repairs[rpi];
            var result = ValidationCommands.ValidateRepairLearning(["--root", root, "--phase", "route", "--postmortem", gate.Postmortem!, "--rpi", rpi,
                "--rules", string.Join(',', repair.RuleIds), "--acceptance", $".engloop/repairs/{pm.Contract.PostmortemId}-{rpi}.route.json"]);
            if (result != 0) return result;
        }
        return 0;
    }

    private static void ValidatePostmortemBinding(string root, string sessionHash, string gatePath, GateRecord gate)
    {
        var bindingPath = Path.Combine(root, BindingRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));
        Ensure(File.Exists(bindingPath), "repair-continuation-postmortem-binding-receipt-missing");
        var binding = JsonSerializer.Deserialize<PostmortemBindingRecord>(File.ReadAllText(bindingPath))
            ?? throw new InvalidOperationException("repair-continuation-postmortem-binding-receipt-invalid");
        var gateRelative = Path.GetRelativePath(root, gatePath).Replace('\\', '/');
        Ensure(binding.SchemaVersion == "1.0" && binding.SessionHash == sessionHash
            && binding.Head == OperationsLearningPolicy.GitHead(root)
            && binding.Postmortem == gate.Postmortem && binding.Incidents.SequenceEqual(gate.Incidents!, StringComparer.Ordinal)
            && binding.Gate == gateRelative && binding.GateSha256 == FileSha256(gatePath)
            && Regex.IsMatch(binding.CorrelationId, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant),
            "repair-continuation-postmortem-binding-receipt-identity-invalid");
        if (binding.Authorization == "explicit-operator-intent")
        {
            _ = ReadPostmortemHost(root, sessionHash);
            Ensure(binding.Intent == PostmortemIntentRelativePath(sessionHash), "postmortem-route-intent-path-invalid");
            var intentPath = Path.Combine(root, PostmortemIntentRelativePath(sessionHash).Replace('/', Path.DirectorySeparatorChar));
            Ensure(File.Exists(intentPath) && binding.IntentSha256 == FileSha256(intentPath), "postmortem-route-intent-hash-invalid");
            var intent = JsonSerializer.Deserialize<PostmortemOperatorIntentRecord>(File.ReadAllText(intentPath))
                ?? throw new InvalidOperationException("postmortem-route-intent-json-invalid");
            var request = ReadExplicitPostmortemIntent(intent.Prompt, root, intent.IncidentDate, intent.Postmortem);
            var intentIncidents = intent.Incidents is { Length: > 0 } ? intent.Incidents : [intent.Incident];
            Ensure(intent.SchemaVersion == "1.0" && intent.SessionHash == sessionHash && intent.Head == gate.Head
                && intent.HostSha256 == FileSha256(PostmortemHostPath(root, sessionHash))
                && intent.PromptSha256 == Sha256(Encoding.UTF8.GetBytes(intent.Prompt))
                && request is not null && intentIncidents.Length > 0
                && request.Value.Incidents.SequenceEqual(intentIncidents, StringComparer.Ordinal)
                && (request.Value.Postmortem is null || request.Value.Postmortem == intent.Postmortem)
                && intent.Incident == intentIncidents[0]
                && gate.Incidents is not null && gate.Incidents.SequenceEqual(intentIncidents, StringComparer.Ordinal)
                && gate.Postmortem == intent.Postmortem
                && PostmortemIntentEvidenceMatches(root, intent, intentIncidents)
                && (binding.IncidentHashes is null || IncidentHashesMatch(root, intentIncidents, binding.IncidentHashes))
                && intent.NorthstarSha256 == FileSha256(Path.Combine(root, "NORTHSTAR.md"))
                && Regex.IsMatch(intent.LearningsSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)
                && Regex.IsMatch(intent.RegistrySha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)
                && binding.ConfirmationSha256 == string.Empty && binding.ToolUseId == string.Empty,
                "postmortem-route-intent-identity-invalid");
        }
        else Ensure(binding.Authorization == "confirmation"
            && Regex.IsMatch(binding.ConfirmationSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)
            && !string.IsNullOrWhiteSpace(binding.ToolUseId) && binding.Intent is null && binding.IntentSha256 is null,
            "repair-continuation-postmortem-binding-receipt-identity-invalid");
    }

    private static bool PostmortemIntentEvidenceMatches(string root,
        PostmortemOperatorIntentRecord intent, string[] incidents)
    {
        var hashes = intent.IncidentHashes;
        if (hashes is null && incidents.Length == 1)
            hashes = new Dictionary<string, string>(StringComparer.Ordinal) { [incidents[0]] = intent.IncidentSha256 };
        if (hashes is null || !hashes.Keys.OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(incidents.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal)) return false;
        if (intent.IncidentDate is not null
            && !IncidentIdsForDate(root, intent.IncidentDate).SequenceEqual(incidents, StringComparer.Ordinal)) return false;
        return IncidentHashesMatch(root, incidents, hashes);
    }

    private static bool IncidentHashesMatch(string root, string[] incidents, Dictionary<string, string> hashes)
    {
        if (!hashes.Keys.OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(incidents.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal)) return false;
        return incidents.All(incident => hashes.TryGetValue(incident, out var expectedHash)
            && expectedHash == FileSha256(UniqueIncidentPath(root, incident)));
    }

    private static bool IncidentBindingEvidenceMatches(string root, string head, string[] incidents,
        Dictionary<string, string>? hashes)
    {
        if (OperationsLearningPolicy.GitHead(root) != head) return false;
        return hashes is not null
            ? IncidentHashesMatch(root, incidents, hashes)
            : IncidentEvidenceMatchesHead(root, head, incidents);
    }

    private static bool IncidentEvidenceMatchesHead(string root, string head, string[] incidents)
    {
        if (OperationsLearningPolicy.GitHead(root) != head) return false;
        foreach (var incident in incidents)
        {
            var relative = Path.GetRelativePath(root, UniqueIncidentPath(root, incident)).Replace('\\', '/');
            var tracked = RunGit(root, "ls-tree", "-r", "--name-only", head, "--", relative);
            var trackedPaths = tracked.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (tracked.ExitCode != 0 || trackedPaths.Length != 1 || trackedPaths[0] != relative) return false;
            if (RunGit(root, "diff", "--quiet", head, "--", relative).ExitCode != 0) return false;
        }
        return true;
    }

    private static void WriteRepairRouteRecord(string path, PostmortemLearningContract postmortem, RepairLearningContract repair)
    {
        var payload = new
        {
            schemaVersion = "1.0",
            artifactType = "repair-learning-route",
            phase = "route",
            postmortemPath = postmortem.RelativePath,
            postmortemSha256 = postmortem.Sha256,
            rpiId = repair.RpiId,
            ruleIds = repair.RuleIds,
            northstarSha256 = postmortem.NorthstarSha256,
            learningsSha256 = postmortem.LearningsSha256,
            pyramidDigest = postmortem.PyramidDigest,
            executableGate = repair.ExecutableGate,
            executableGateDigest = repair.ExecutableGateDigest,
            gateProves = repair.GateProves,
            sekApplicability = repair.SekApplicability,
            sekVersion = postmortem.SekEscape.Version,
            sekEscapeClass = postmortem.SekEscape.EscapeClass,
            sekScenarioId = repair.SekScenarioId,
            sekModelPaths = postmortem.SekEscape.ModelPaths,
            sekCordPaths = postmortem.SekEscape.CordPaths,
            sekGeneratedSuitePath = postmortem.SekEscape.GeneratedSuitePath,
            sekRepairRequirement = repair.SekRepairRequirement,
            status = "ROUTED",
        };
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, payload);
        stream.Flush(flushToDisk: true);
    }

    private static void WriteRepairContinuationBlocker(string? postmortem, string[] rpis,
        string diagnosticCode, IReadOnlyList<string> failures, string? routeCreated = null)
    {
        var details = JsonSerializer.Serialize(new
        {
            status = "repair-continuation-blocked",
            mode = "repair",
            postmortem,
            requestedRpis = rpis,
            diagnosticCode = Bound(diagnosticCode),
            missingEvidence = failures.Select(Bound).ToArray(),
            routeCreated,
            repairScopeCreated = false,
            completionAccepted = false,
            unresolvedState = "The requested repair intent and validated artifacts are retained; no repair scope or completion is accepted.",
            resumableAction = "Continue in Stage 22: correct the listed contract evidence and resume the trusted Stage 22 continuation, or ask one focused question if the PM/item is ambiguous. Do not return the operator to an earlier stage or stop because stabilization is open. Do not create readiness or gate PASS evidence manually.",
        });
        WriteResult(true, systemMessage: "OPERATIONS_REPAIR_CONTINUATION_BLOCKED " + details);
    }

    private static bool IsPostmortemWritablePath(string root, GateRecord gate, string path)
    {
        try
        {
            var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
            var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
            if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative)) return false;
            if (HasReparsePointBetweenRootAndPath(root, full)) return false;
            var normalizedGatePath = gate.Postmortem!.Replace('\\', '/').TrimStart('.', '/');
            var normalizedRelative = relative.TrimStart('.', '/');
            return string.Equals(normalizedRelative, normalizedGatePath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedRelative, "engloop/numbering-registry.md", StringComparison.OrdinalIgnoreCase)
                || normalizedRelative.StartsWith("engloop/learnings/", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasReparsePointBetweenRootAndPath(string root, string fullPath)
    {
        var current = fullPath;
        while (current.Length >= root.Length)
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint)) return true;
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase)) return false;
            var parent = Path.GetDirectoryName(current);
            if (parent is null) return true;
            current = parent;
        }
        return true;
    }

    private static void WritePostmortemOperationDenied(string toolName)
    {
        const string reason = "The confirmed postmortem scope does not authorize production/source mutations, unrelated repair work, or arbitrary execution/delegation. No action ran. Limit writes to the confirmed PM, numbering registry, and learning-pyramid records; use read/search/questions or the exact postmortem validation commands.";
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            stopReason = (string?)null,
            hookSpecificOutput = new
            {
                hookEventName = "PreToolUse",
                permissionDecision = "deny",
                permissionDecisionReason = reason + " Tool: " + Bound(toolName),
            },
        }));
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
        return (header, questionText, IdList(match.Groups["incidents"].Value, @"^IN\d{3}$", "incident"), PostmortemPath(match.Groups["postmortem"].Value));
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
        => WriteLifecycleDeferred("postmortem", phase, diagnostic,
            "Stage 21 context is incomplete; no validated postmortem scope is available. Collect and confirm valid incident/PM context in chat. " + Bound(diagnostic));

    private static void WritePostmortemScopeDecision(string toolName, string diagnosticCode, string diagnostic)
    {
        var allow = IsCollectionReadOrQuestionTool(toolName);
        const string remediation = "Stage 21 has no valid postmortem scope. Local read/search/questions remain available; writes, execution and arbitrary delegation are denied. Collect and confirm context in chat. " + NavigationRecovery;
        var details = JsonSerializer.Serialize(new
        {
            status = "postmortem-scope-denied",
            mode = "postmortem",
            phase = "pre-tool-use",
            command = "operations-hook guard postmortem",
            diagnosticCode = Bound(diagnosticCode),
            diagnostic = Bound(diagnostic),
            remediation,
            completionAccepted = false,
            correlation = Guid.NewGuid().ToString("N"),
            unresolvedState = "Existing gate, collection, and evidence are retained; no scope or completion is accepted.",
        });
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            stopReason = (string?)null,
            systemMessage = "OPERATIONS_LEARNING_CONTEXT_REQUIRED " + details,
            hookSpecificOutput = new
            {
                hookEventName = "PreToolUse",
                permissionDecision = allow ? "allow" : "deny",
                permissionDecisionReason = $"{remediation} Diagnostic: {Bound(diagnosticCode)}",
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
        var postmortem = PostmortemPath(Get("--postmortem"));
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

    private static GateRecord NewArguments(string? incident = null, string? postmortem = null, string[]? incidents = null, string? phase = null, string? rpi = null, string[]? rules = null, string? acceptance = null, string[]? requestedRpis = null)
        => new("", "", "", "", "", "", "", "", "", "", incident, postmortem, incidents, phase, rpi, rules, acceptance, requestedRpis);

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
            .Where(cells => cells.Length > 1
                && (cells[0].Equals("Type", StringComparison.OrdinalIgnoreCase)
                    && cells.Any(cell => cell.Equals("Next number", StringComparison.OrdinalIgnoreCase))
                    || cells[0].Equals("Prefix", StringComparison.OrdinalIgnoreCase)
                    && cells.Any(cell => cell.Equals("Last used", StringComparison.OrdinalIgnoreCase))))
            .ToArray();
        var observedHeader = lines.FirstOrDefault(line => line.TrimStart().StartsWith('|')) ?? "<no markdown table header>";
        Ensure(headers.Length == 1,
            $"postmortem-route-numbering-header-invalid:expected='| Type | Next number |' or legacy '| Prefix | ... | Last used |';actual='{Bound(observedHeader)}'");
        var nextNumberSchema = headers[0][0].Equals("Type", StringComparison.OrdinalIgnoreCase);
        var counterIndex = Array.FindIndex(headers[0], cell => cell.Equals(nextNumberSchema ? "Next number" : "Last used", StringComparison.OrdinalIgnoreCase));
        var rows = lines.Select(MarkdownCells)
            .Where(cells => cells[0].Trim('`').Equals(prefix, StringComparison.Ordinal))
            .ToArray();
        Ensure(rows.Length == 1, "postmortem-route-numbering-row-invalid:" + prefix);
        Ensure(rows[0].Length > counterIndex, "postmortem-route-numbering-row-invalid:" + prefix);
        var value = rows[0][counterIndex].Trim('`');
        var validValue = nextNumberSchema
            ? Regex.IsMatch(value, @"^\d{3}$", RegexOptions.CultureInvariant)
            : Regex.IsMatch(value, "^" + Regex.Escape(prefix) + @"\d{3}$", RegexOptions.CultureInvariant);
        Ensure(validValue, $"postmortem-route-numbering-value-invalid:{prefix}:expected={(nextNumberSchema ? "NNN" : prefix + "NNN")}:actual={Bound(value)}");
        var numericText = nextNumberSchema ? value : value[prefix.Length..];
        Ensure(int.TryParse(numericText, out var number) && number > 0
            && (nextNumberSchema ? number <= 999 : number < 999),
            "postmortem-route-numbering-value-invalid:" + prefix);
        return NumberingRegistry.Format(prefix, nextNumberSchema ? number : number + 1);
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
        _ = PostmortemPath(relative);
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
        => Sha256(Encoding.UTF8.GetBytes(value.RequestedRpis is null
            ? JsonSerializer.Serialize(new { value.Incident, value.Postmortem, value.Incidents, value.Phase, value.Rpi, value.Rules, value.Acceptance })
            : JsonSerializer.Serialize(new { value.Incident, value.Postmortem, value.Incidents, value.Phase, value.Rpi, value.Rules, value.Acceptance, value.RequestedRpis })));

    private static (string AssemblyPath, string AssemblySha256, string ToolVersion, string ManifestPath, string ManifestSha256) CurrentToolIdentity(string root)
    {
        var assemblyPath = Path.GetFullPath(typeof(OperationsHookCommands).Assembly.Location);
        var manifestPath = Path.Combine(root, ".config", "dotnet-tools.json");
        Ensure(File.Exists(manifestPath), "operations-hook-tool-manifest-missing");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Ensure(manifest.RootElement.ValueKind == JsonValueKind.Object, "operations-hook-tool-manifest-invalid");
        Ensure(manifest.RootElement.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Object, "operations-hook-tool-manifest-invalid");
        Ensure(tools.TryGetProperty("engloopkit", out var tool) && tool.ValueKind == JsonValueKind.Object, "operations-hook-tool-manifest-invalid");
        Ensure(tool.TryGetProperty("version", out var versionValue) && versionValue.ValueKind == JsonValueKind.String, "operations-hook-tool-version-missing");
        var version = versionValue.GetString() ?? string.Empty;
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
        // Prose grammar: only an unquoted --postmortem path token ending in .md.
        // may carry one terminal sentence period. Quotes, CLI arguments and
        // confirmation/receipt identities are exact and never use this rule.
        if (name == "--postmortem" && match.Groups["bare"].Success
            && match.Groups["bare"].Value.EndsWith(".md.", StringComparison.Ordinal))
            return match.Groups["bare"].Value[..^1];
        return match.Groups["dq"].Value + match.Groups["sq"].Value + match.Groups["bare"].Value;
    }

    private static string PostmortemPath(string value)
    {
        const string prefix = ".engloop/postmortems/";
        Ensure(value.StartsWith(prefix, StringComparison.Ordinal) && !Path.IsPathRooted(value)
            && !value.Contains('\\') && !value.Split('/').Any(part => part is "." or ".."), "operations-hook-path-invalid");
        Ensure(Regex.IsMatch(value, @"\A\.engloop/postmortems/PM[0-9]{3}(?:[_-][A-Za-z0-9][A-Za-z0-9._-]*)?\.md\z", RegexOptions.CultureInvariant), "postmortem-route-filename-invalid");
        return value;
    }

    private static string ExactRoutePath(string value)
    {
        Ensure(value == GovernedPath(value, ".engloop/out/postmortem-context/")
            && !value.Split('/').Any(part => part is "." or ".." or ""), "operations-hook-path-invalid");
        return value;
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
        Ensure(Directory.Exists(selected), "operations-hook-git-root-unavailable");
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

    private static void WriteQuietResult()
        => Console.WriteLine(JsonSerializer.Serialize(new { @continue = true }));

    private static void WriteHookContext(string hookEvent, string context)
        => Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            systemMessage = context,
            hookSpecificOutput = new { hookEventName = hookEvent, additionalContext = context },
        }));

    private static void WritePostToolContext(string context)
        => Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = true,
            additionalContext = context,
            hookSpecificOutput = new { hookEventName = "PostToolUse", additionalContext = context },
        }));

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
