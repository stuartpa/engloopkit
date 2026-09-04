using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EngLoopKit.Core;

namespace EngLoopKit.Tool;

public static class CodeReviewResponseCommands
{
    private static readonly string CurrentToolVersion = typeof(CodeReviewResponseCommands).Assembly.GetName().Version!.ToString(3);
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly RegexOptions PathRegexOptions = RegexOptions.Multiline | RegexOptions.CultureInvariant
        | (OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
    };

    private sealed record Gate(
        string SchemaVersion,
        string Mode,
        string SessionHash,
        string Head,
        string ToolVersion,
        string ManifestSha256,
        string Packet,
        string PacketSha256,
        string? AddressReceipt,
        string? AddressReceiptSha256,
        string? Adapter,
        string? AdapterSha256,
        string? AdapterArtifactSha256,
        string? Inspection,
        string? InspectionSha256,
        string? Operation,
        string Provider,
        string Repository,
        string PullRequest,
        string Thread,
        string SourceRevision,
        string TargetRevision,
        string Iteration,
        string InitialStatusDigest);

    private sealed record AddressReceipt(
        string SchemaVersion,
        string Packet,
        string PacketSha256,
        string GateSha256,
        string GateJson,
        string Head,
        string Provider,
        string Repository,
        string PullRequest,
        string Thread,
        string SourceRevision,
        string TargetRevision,
        string Iteration,
        string FinalStatusDigest);

    private sealed record AddressPacket(
        string SchemaVersion,
        string ArtifactType,
        string Provider,
        string Repository,
        string PullRequest,
        string Thread,
        string SourceRevision,
        string TargetRevision,
        string Iteration,
        string ActingPrincipal,
        string ThreadStatus,
        string Classification,
        string InitialHead,
        string FinalHead,
        string InitialStatusDigest,
        string FinalStatusDigest,
        string[] ChangedFiles,
        string[] ValidationResults,
        string[] Evidence,
        string[] UnresolvedRisks,
        string ReplyText,
        string[] AllowedOperations,
        string RequiredFixRevision,
        string ProviderHead,
        bool? ProviderMutationPerformed,
        bool? CommitPushPerformed,
        JsonElement AdapterRequest);

    private sealed record AdapterManifest(
        string SchemaVersion,
        string AdapterId,
        string Provider,
        string Protocol,
        string[] Command,
        string CommandArtifact,
        string CommandArtifactSha256,
        int TimeoutSeconds,
        string[] Capabilities);

    private sealed record Approval(
        string SchemaVersion,
        string SessionHash,
        string GateSha256,
        string PacketSha256,
        string Operation,
        string ReplySha256,
        string ToolUseId,
        string QuestionSha256,
        string ResponseSha256,
        string QuestionJson,
        string ResponseJson);

    private sealed record Attempt(
        string SchemaVersion,
        string State,
        string AttemptId,
        string GateSha256,
        string PacketSha256,
        string AdapterSha256,
        string Operation,
        string Marker,
        string ApprovalSha256,
        string ApprovalJson,
        string ApprovalToolUseId,
        string ApprovalQuestionSha256,
        string ApprovalResponseSha256,
        string ApprovalQuestionJson,
        string ApprovalResponseJson,
        string? Diagnostic,
        string? ProviderReceiptId);

    private sealed record AdapterResult(
        string SchemaVersion,
        string Status,
        string Provider,
        string Repository,
        string PullRequest,
        string Thread,
        string SourceRevision,
        string TargetRevision,
        string Iteration,
        string ActingPrincipal,
        string ThreadStatus,
        string ProviderHead,
        string Operation,
        string PacketSha256,
        string AddressReceiptSha256,
        string InspectionSha256,
        string InspectionReceiptId,
        string Marker,
        string ReplySha256,
        bool? ReplyObserved,
        bool? ResolveObserved,
        int? MatchCount,
        string ProviderReceiptId);

    private sealed record AdapterInspection(
        string SchemaVersion,
        string Status,
        string Provider,
        string Repository,
        string PullRequest,
        string Thread,
        string SourceRevision,
        string TargetRevision,
        string Iteration,
        string ActingPrincipal,
        string ThreadStatus,
        string ProviderHead,
        string Operation,
        string PacketSha256,
        string AddressReceiptSha256,
        int? MatchCount,
        bool? MutationPerformed,
        string InspectionReceiptId);

    private static readonly HashSet<string> LocalReadTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "read_file", "file_search", "grep_search", "semantic_search", "list_dir",
    };

    private static readonly HashSet<string> AddressReadTools = new(LocalReadTools, StringComparer.OrdinalIgnoreCase)
    {
        "search", "fetch_webpage", "web",
    };

    private static readonly HashSet<string> EditTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "edit", "apply_patch", "create_file", "replace_string_in_file", "insert_edit_into_file",
    };

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal) { "address", "reply-resolve" };
    private static readonly HashSet<string> Operations = new(StringComparer.Ordinal) { "reply", "resolve", "reply-and-resolve" };
    private static readonly HashSet<string> Classifications = new(StringComparer.Ordinal)
    {
        "accepted-actionable", "already-addressed", "stale", "disputed", "clarification-required", "unsupported-out-of-scope",
    };
    private static readonly HashSet<string> ThreadStatuses = new(StringComparer.Ordinal) { "active", "resolved" };
    private static readonly HashSet<string> AdapterStatuses = new(StringComparer.Ordinal) { "success", "rejected", "outcome-unknown" };
    private static readonly HashSet<string> InspectionStatuses = new(StringComparer.Ordinal) { "ready", "rejected" };
    private static readonly HashSet<string> AdapterCapabilities = new(Operations, StringComparer.Ordinal) { "inspect" };
    private static readonly HashSet<string> AttemptStates = new(StringComparer.Ordinal) { "started", "outcome-unknown", "rejected", "success" };
    private static readonly HashSet<string> InlineEvaluationFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "-Command", "-EncodedCommand", "--command", "-c", "/c", "--eval", "-e",
    };

    public static int ExecuteHook(string[] args)
    {
        var action = args.Length > 0 ? args[0] : "unknown";
        var mode = args.Length > 1 ? args[1] : "unknown";
        try
        {
            Ensure(Modes.Contains(mode), "review-response-mode-invalid");
            using var input = JsonDocument.Parse(Console.In.ReadToEnd());
            var root = ExactGitRoot(ReadString(input.RootElement, "cwd"));
            RequireAgentEntry(root, mode);
            var session = ReadString(input.RootElement, "session_id", "sessionId");
            Ensure(session.Length > 0, "review-response-session-missing");
            var sessionHash = Sha256Text(session);
            var gatePath = GatePath(root, sessionHash, mode);
            using var sessionLock = action is "initialize" or "post-tool" or "stop" ? AcquireExclusiveLock(root, gatePath + ".lock", "review-response-session-operation-in-progress") : null;
            return action switch
            {
                "initialize" => Initialize(root, mode, sessionHash, gatePath, ReadString(input.RootElement, "prompt")),
                "guard" => Guard(root, mode, gatePath, input.RootElement),
                "post-tool" => PostTool(root, mode, gatePath, input.RootElement),
                "stop" => Stop(root, mode, gatePath),
                _ => throw new InvalidOperationException("review-response-action-invalid"),
            };
        }
        catch (Exception ex)
        {
            WriteHook(false, "Code-review response hook failed closed: " + ex.Message);
            return 0;
        }
    }

    public static int Execute(string[] args)
    {
        if (args.Length == 0 || args[0] != "apply")
        {
            Console.Error.WriteLine("Usage: engloopkit code-review-response apply --gate <path> --approval <path>");
            return 1;
        }
        try
        {
            var root = ExactGitRoot(Environment.CurrentDirectory);
            var gateRelative = GovernedPath(root, Option(args, "--gate"), ".engloop/out/code-review-response/gates/");
            var approvalRelative = GovernedPath(root, Option(args, "--approval"), ".engloop/out/code-review-response/approvals/");
            var gatePath = Full(root, gateRelative);
            var approvalPath = Full(root, approvalRelative);
            var gate = Read<Gate>(gatePath, "review-response-gate-invalid");
            Ensure(gate.Mode == "reply-resolve", "review-response-gate-mode-invalid");
            ValidateGate(root, gate);
            Ensure(approvalRelative == ApprovalRelative(gate.SessionHash), "review-response-approval-path-mismatch");
            var packet = ReadPacket(root, gate.Packet);
            ValidatePacket(packet, gate, requireReadyForProvider: true);
            var adapter = ReadAdapter(root, gate.Adapter!);
            ValidateAdapter(root, adapter, gate, packet);
            var inspection = Read<AdapterInspection>(Full(root, gate.Inspection!), "review-response-provider-inspection-invalid");

            var attemptPath = AttemptPath(root, gate.PacketSha256, gate.Operation!);
            using var attemptLock = AcquireExclusiveLock(root, attemptPath + ".lock", "review-response-attempt-in-progress");
            var existing = File.Exists(attemptPath) ? Read<Attempt>(attemptPath, "review-response-attempt-invalid") : null;
            if (existing is not null) ValidateAttempt(existing, gate, gatePath);
            if (existing is { State: "success" })
            {
                if (File.Exists(approvalPath)) File.Delete(approvalPath);
                Console.WriteLine($"CODE_REVIEW_RESPONSE_ALREADY_APPLIED receipt={Path.GetRelativePath(root, attemptPath).Replace('\\', '/')}");
                return 0;
            }
            var approval = Read<Approval>(approvalPath, "review-response-approval-invalid");
            ValidateApproval(gatePath, gate, packet, approval);
            Ensure(approval.ReplySha256 == Sha256Text(packet.ReplyText), "review-response-approved-reply-changed");
            var approvalJson = File.ReadAllText(approvalPath);
            var approvalSha256 = Sha256Text(approvalJson);
            if (existing is not null) Ensure(existing.ApprovalSha256 == approvalSha256, "review-response-attempt-approval-mismatch");
            var phase = existing is null ? "apply" : "reconcile";
            if (existing is not null) Ensure(existing.State is "started" or "outcome-unknown", "review-response-attempt-retry-forbidden");
            var attemptId = existing?.AttemptId ?? Guid.NewGuid().ToString("N");
            var marker = existing?.Marker ?? $"elk-review-response:{gate.PacketSha256[..16]}:{attemptId}";
            var started = new Attempt("1.0", "started", attemptId, FileSha256(gatePath), gate.PacketSha256, gate.AdapterSha256!, gate.Operation!, marker,
                approvalSha256, approvalJson, approval.ToolUseId, approval.QuestionSha256, approval.ResponseSha256, approval.QuestionJson, approval.ResponseJson, null, null);
            Directory.CreateDirectory(Path.GetDirectoryName(attemptPath)!);
            if (existing is null) WriteJsonCreateNew(attemptPath, started, "review-response-attempt-already-exists"); else WriteJson(attemptPath, started);

            var request = JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0",
                phase,
                adapterId = adapter.AdapterId,
                packet.Provider,
                packet.Repository,
                packet.PullRequest,
                packet.Thread,
                packet.SourceRevision,
                packet.TargetRevision,
                packet.Iteration,
                packet.ActingPrincipal,
                operation = gate.Operation,
                replyText = packet.ReplyText,
                replySha256 = Sha256Text(packet.ReplyText),
                packetSha256 = gate.PacketSha256,
                addressReceiptSha256 = gate.AddressReceiptSha256,
                inspectionSha256 = gate.InspectionSha256,
                inspectionReceiptId = inspection.InspectionReceiptId,
                requiredFixRevision = packet.RequiredFixRevision,
                expectedProviderHead = packet.ProviderHead,
                expectedThreadStatus = packet.ThreadStatus,
                marker,
                adapterRequest = packet.AdapterRequest,
            }, JsonOptions);
            (int ExitCode, string Output, string Error) invoked;
            try
            {
                invoked = InvokeAdapter(root, adapter, gate, request);
            }
            catch (Exception ex)
            {
                WriteJson(attemptPath, started with { State = "outcome-unknown", Diagnostic = "adapter-invocation=" + ex.GetType().Name });
                Console.Error.WriteLine("CODE_REVIEW_RESPONSE_OUTCOME_UNKNOWN reconcile-before-retry");
                return 2;
            }
            if (invoked.ExitCode != 0 || !TryParseAdapterResult(invoked.Output, out var result))
            {
                WriteJson(attemptPath, started with { State = "outcome-unknown", Diagnostic = $"adapter-exit={invoked.ExitCode};stdout={Sha256Text(invoked.Output)};stderr={Sha256Text(invoked.Error)}" });
                Console.Error.WriteLine("CODE_REVIEW_RESPONSE_OUTCOME_UNKNOWN reconcile-before-retry");
                return 2;
            }
            try
            {
                ValidateAdapterResult(result!, gate, packet, inspection, marker);
            }
            catch (Exception ex)
            {
                WriteJson(attemptPath, started with { State = "outcome-unknown", Diagnostic = ex.Message });
                Console.Error.WriteLine("CODE_REVIEW_RESPONSE_OUTCOME_UNKNOWN reconcile-before-retry");
                return 2;
            }
            if (result!.Status != "success")
            {
                var state = result.Status == "outcome-unknown" ? "outcome-unknown" : "rejected";
                WriteJson(attemptPath, started with { State = state, Diagnostic = result.Status, ProviderReceiptId = result.ProviderReceiptId });
                Console.Error.WriteLine(state == "outcome-unknown" ? "CODE_REVIEW_RESPONSE_OUTCOME_UNKNOWN reconcile-before-retry" : "CODE_REVIEW_RESPONSE_REJECTED");
                return state == "outcome-unknown" ? 2 : 1;
            }
            WriteJson(attemptPath, started with { State = "success", ProviderReceiptId = result.ProviderReceiptId });
            File.Delete(approvalPath);
            Console.WriteLine($"CODE_REVIEW_RESPONSE_APPLIED receipt={Path.GetRelativePath(root, attemptPath).Replace('\\', '/')} providerReceipt={result.ProviderReceiptId}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Initialize(string root, string mode, string sessionHash, string gatePath, string prompt)
    {
        Ensure(!File.Exists(gatePath), "review-response-gate-already-exists");
        var packet = PacketPath(root, Argument(prompt, "--packet"));
        Gate gate;
        if (mode == "address")
        {
            Ensure(!File.Exists(Full(root, packet)), "review-response-packet-already-exists");
            Ensure(StatusText(root).Length == 0, "review-response-checkout-not-clean-use-isolated-worktree");
            var provider = Identity(Argument(prompt, "--provider"), "provider");
            var repository = Identity(Argument(prompt, "--repository"), "repository");
            var pr = Identity(Argument(prompt, "--pr"), "pr");
            var thread = Identity(Argument(prompt, "--thread"), "thread");
            var source = Revision(Argument(prompt, "--source"), "source");
            Ensure(source == GitHead(root), "review-response-source-not-local-head");
            var target = Revision(Argument(prompt, "--target"), "target");
            var iteration = Identity(Argument(prompt, "--iteration"), "iteration");
            gate = NewGate(root, mode, sessionHash, packet, null, null, provider, repository, pr, thread, source, target, iteration);
        }
        else
        {
            Ensure(StatusText(root).Length == 0, "review-response-stage12-checkout-not-clean");
            Ensure(File.Exists(Full(root, packet)), "review-response-packet-missing");
            var loaded = ReadPacket(root, packet);
            var operation = Operation(Argument(prompt, "--operation"));
            var adapterPath = GovernedPath(root, Argument(prompt, "--adapter"), ".engloop/provider-adapters/");
            var adapter = ReadAdapter(root, adapterPath);
            gate = NewGate(root, mode, sessionHash, packet, adapterPath, operation, loaded.Provider, loaded.Repository, loaded.PullRequest, loaded.Thread, loaded.SourceRevision, loaded.TargetRevision, loaded.Iteration);
            ValidatePacket(loaded, gate, requireReadyForProvider: true);
            ValidateAddressReceipt(root, gate);
            ValidateAdapter(root, adapter, gate, loaded);
            var inspection = InspectAdapter(root, adapter, gate, loaded);
            var inspectionRelative = InspectionRelative(gate.PacketSha256, operation, sessionHash);
            var inspectionPath = Full(root, inspectionRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(inspectionPath)!);
            WriteJson(inspectionPath, inspection);
            Ensure(inspection.Status == "ready", "review-response-provider-inspection-rejected");
            gate = gate with { Inspection = inspectionRelative, InspectionSha256 = FileSha256(inspectionPath) };
        }
        Directory.CreateDirectory(Path.GetDirectoryName(gatePath)!);
        WriteJsonCreateNew(gatePath, gate, "review-response-gate-already-exists");
        WriteHook(true, systemMessage: $"CODE_REVIEW_RESPONSE_SCOPE_ACTIVE mode={mode} gate={Path.GetRelativePath(root, gatePath).Replace('\\', '/')}");
        return 0;
    }

    private static int Guard(string root, string mode, string gatePath, JsonElement input)
    {
        var gate = Read<Gate>(gatePath, "review-response-gate-missing-or-invalid");
        ValidateGate(root, gate);
        Ensure(gate.Mode == mode, "review-response-gate-mode-mismatch");
        var tool = ReadString(input, "tool_name", "toolName");
        var command = ToolCommand(input);
        bool allow;
        if (mode == "address")
        {
            allow = AddressReadTools.Contains(tool) || EditTools.Contains(tool) && IsAllowedAuthorEdit(input, gate) || IsDeclaredValidation(root, tool, command);
        }
        else
        {
            var approvalPath = Full(root, ApprovalRelative(gate.SessionHash));
            var packet = ReadPacket(root, gate.Packet);
            var succeeded = HasSuccessfulAttempt(root, gate, gatePath);
            allow = LocalReadTools.Contains(tool)
                || !succeeded && !File.Exists(approvalPath) && tool.Equals("vscode_askQuestions", StringComparison.OrdinalIgnoreCase) && IsExactApprovalQuestion(input, gate, packet)
                || File.Exists(approvalPath) && IsApplyCommand(tool, command, Path.GetRelativePath(root, gatePath).Replace('\\', '/'), ApprovalRelative(gate.SessionHash));
        }
        WriteDecision(allow, allow ? "Allowed by the exact review-response stage policy." : "Denied by the review-response authority boundary.");
        return 0;
    }

    private static int PostTool(string root, string mode, string gatePath, JsonElement input)
    {
        if (mode != "reply-resolve" || ReadString(input, "tool_name", "toolName") != "vscode_askQuestions")
        {
            WriteHook(true);
            return 0;
        }
        var gate = Read<Gate>(gatePath, "review-response-gate-missing-or-invalid");
        ValidateGate(root, gate);
        Ensure(gate.Mode == mode, "review-response-gate-mode-mismatch");
        Ensure(!HasSuccessfulAttempt(root, gate, gatePath), "review-response-already-applied");
        var packet = ReadPacket(root, gate.Packet);
        ValidatePacket(packet, gate, requireReadyForProvider: true);
        var toolUseId = ReadString(input, "tool_use_id", "toolUseId");
        Ensure(toolUseId.Length > 0, "review-response-approval-tool-use-id-missing");
        var questionText = $"Apply {gate.Operation} to thread {gate.Thread} with packet {gate.PacketSha256[..12]}?";
        var questionMessage = ApprovalMessage(packet, gate);
        var inputJson = ToolJson(input, "tool_input", "toolInput");
        var responseJson = ToolJson(input, "tool_response", "toolResponse");
        ValidateQuestion(inputJson, questionText, questionMessage);
        var approvalPath = Full(root, ApprovalRelative(gate.SessionHash));
        Ensure(!File.Exists(approvalPath), "review-response-approval-already-exists");
        var decision = QuestionDecision(responseJson, questionText, "Approve review response");
        if (decision == "Cancel")
        {
            File.Delete(gatePath);
            WriteHook(true, systemMessage: "CODE_REVIEW_RESPONSE_CANCELLED completionAccepted=false");
            return 0;
        }
        Ensure(decision == "Confirm", "review-response-approval-decision-invalid");
        Directory.CreateDirectory(Path.GetDirectoryName(approvalPath)!);
        var questionJson = inputJson.GetRawText();
        var responseJsonText = responseJson.GetRawText();
        WriteJsonCreateNew(approvalPath, new Approval("1.0", gate.SessionHash, FileSha256(gatePath), gate.PacketSha256, gate.Operation!, Sha256Text(packet.ReplyText), toolUseId, Sha256Text(questionJson), Sha256Text(responseJsonText), questionJson, responseJsonText), "review-response-approval-already-exists");
        WriteHook(true, systemMessage: $"CODE_REVIEW_RESPONSE_APPROVED approval={ApprovalRelative(gate.SessionHash)} packet={gate.PacketSha256}");
        return 0;
    }

    private static int Stop(string root, string mode, string gatePath)
    {
        if (!File.Exists(gatePath))
        {
            WriteHook(true, systemMessage: "CODE_REVIEW_RESPONSE_CANCELLED completionAccepted=false");
            return 0;
        }
        var gate = Read<Gate>(gatePath, "review-response-gate-missing-or-invalid");
        ValidateGate(root, gate);
        Ensure(gate.Mode == mode, "review-response-gate-mode-mismatch");
        var packet = ReadPacket(root, gate.Packet);
        ValidatePacket(packet, gate, requireReadyForProvider: mode == "reply-resolve");
        if (mode == "address")
        {
            Ensure(packet.FinalStatusDigest == StatusDigest(root), "review-response-final-status-mismatch");
            var statusPaths = StatusPaths(root);
            Ensure(packet.ChangedFiles.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(statusPaths, StringComparer.Ordinal), "review-response-changed-files-mismatch");
            Ensure(!statusPaths.Any(IsProtectedControlPath), "review-response-stage11-control-path-changed");
            var packetHash = FileSha256(Full(root, gate.Packet));
            var receiptRelative = AddressReceiptRelative(gate.Packet, packetHash);
            var receiptPath = Full(root, receiptRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
            var gateJson = File.ReadAllText(gatePath);
            var receipt = new AddressReceipt("1.0", gate.Packet, packetHash, FileSha256(gatePath), gateJson, gate.Head,
                gate.Provider, gate.Repository, gate.PullRequest, gate.Thread, gate.SourceRevision,
                gate.TargetRevision, gate.Iteration, packet.FinalStatusDigest);
            if (File.Exists(receiptPath))
            {
                var existingReceipt = Read<AddressReceipt>(receiptPath, "review-response-address-receipt-invalid");
                ValidateAddressReceiptValue(existingReceipt, gate, packet, packetHash, "review-response-address-receipt-conflict");
            }
            else
            {
                try { WriteJsonCreateNew(receiptPath, receipt, "review-response-address-receipt-conflict"); }
                catch (InvalidOperationException) when (File.Exists(receiptPath))
                {
                    var existingReceipt = Read<AddressReceipt>(receiptPath, "review-response-address-receipt-invalid");
                    ValidateAddressReceiptValue(existingReceipt, gate, packet, packetHash, "review-response-address-receipt-conflict");
                }
            }
            File.Delete(gatePath);
            WriteHook(true, systemMessage: $"CODE_REVIEW_ADDRESS_OK packet={gate.Packet} sha256={packetHash} receipt={receiptRelative}");
            return 0;
        }
        var attemptPath = AttemptPath(root, gate.PacketSha256, gate.Operation!);
        Ensure(File.Exists(attemptPath), "review-response-provider-receipt-missing");
        var attempt = Read<Attempt>(attemptPath, "review-response-attempt-invalid");
        ValidateAttempt(attempt, gate, gatePath);
        Ensure(attempt.State == "success", "review-response-provider-outcome-not-success:" + attempt.State);
        File.Delete(gatePath);
        WriteHook(true, systemMessage: $"CODE_REVIEW_REPLY_RESOLVE_OK receipt={Path.GetRelativePath(root, attemptPath).Replace('\\', '/')}");
        return 0;
    }

    private static Gate NewGate(string root, string mode, string sessionHash, string packet, string? adapter, string? operation, string provider, string repository, string pr, string thread, string source, string target, string iteration)
    {
        var manifest = Path.Combine(root, ".config", "dotnet-tools.json");
        Ensure(File.Exists(manifest), "review-response-tool-manifest-missing");
        EnsureNoReparsePoints(root, manifest);
        Ensure(IsTracked(root, ".config/dotnet-tools.json"), "review-response-tool-manifest-must-match-head");
        Ensure(IsTrackedAtHead(root, ".config/dotnet-tools.json"), "review-response-tool-manifest-must-match-head");
        using var json = JsonDocument.Parse(File.ReadAllText(manifest));
        var version = json.RootElement.GetProperty("tools").GetProperty("engloopkit").GetProperty("version").GetString();
        Ensure(version == CurrentToolVersion, "review-response-tool-version-mismatch");
        var packetFull = Full(root, packet);
        string? adapterArtifactSha = null;
        if (adapter is not null)
        {
            var adapterManifest = ReadAdapter(root, adapter);
            var artifact = GovernedPath(root, adapterManifest.CommandArtifact, ".engloop/provider-adapters/");
            adapterArtifactSha = FileSha256(Full(root, artifact));
        }
        var packetSha256 = File.Exists(packetFull) ? FileSha256(packetFull) : string.Empty;
        string? addressReceipt = null;
        string? addressReceiptSha256 = null;
        if (mode == "reply-resolve")
        {
            addressReceipt = AddressReceiptRelative(packet, packetSha256);
            var addressReceiptPath = Full(root, addressReceipt);
            Ensure(File.Exists(addressReceiptPath), "review-response-address-receipt-missing");
            addressReceiptSha256 = FileSha256(addressReceiptPath);
        }
        return new Gate("1.0", mode, sessionHash, GitHead(root), version!, FileSha256(manifest), packet,
            packetSha256, addressReceipt, addressReceiptSha256, adapter,
            adapter is null ? null : FileSha256(Full(root, adapter)), adapterArtifactSha, null, null, operation,
            provider, repository, pr, thread, source, target, iteration, StatusDigest(root));
    }

    private static void ValidateGate(string root, Gate gate)
    {
        Ensure(AllPresent(gate.SchemaVersion, gate.Mode, gate.SessionHash, gate.Head, gate.ToolVersion,
            gate.ManifestSha256, gate.Packet, gate.PacketSha256, gate.Provider, gate.Repository,
            gate.PullRequest, gate.Thread, gate.SourceRevision, gate.TargetRevision, gate.Iteration,
            gate.InitialStatusDigest), "review-response-gate-fields-missing");
        Ensure(gate.SchemaVersion == "1.0", "review-response-gate-schema-invalid");
        Ensure(Modes.Contains(gate.Mode), "review-response-gate-mode-invalid");
        Ensure(IsHash(gate.SessionHash), "review-response-gate-session-invalid");
        _ = PacketPath(root, gate.Packet);
        Ensure(IsRevision(gate.Head), "review-response-gate-head-invalid");
        Ensure(IsIdentity(gate.Provider), "review-response-gate-provider-invalid");
        Ensure(IsIdentity(gate.Repository), "review-response-gate-repository-invalid");
        Ensure(IsIdentity(gate.PullRequest), "review-response-gate-pr-invalid");
        Ensure(IsIdentity(gate.Thread), "review-response-gate-thread-invalid");
        Ensure(IsRevision(gate.SourceRevision), "review-response-gate-source-invalid");
        Ensure(IsRevision(gate.TargetRevision), "review-response-gate-target-invalid");
        Ensure(IsIdentity(gate.Iteration), "review-response-gate-iteration-invalid");
        Ensure(IsHash(gate.InitialStatusDigest), "review-response-gate-status-invalid");
        if (gate.Mode == "address")
        {
            Ensure(gate.PacketSha256.Length == 0, "review-response-address-gate-packet-hash-invalid");
            Ensure(gate.AddressReceipt is null, "review-response-address-gate-authority-invalid");
            Ensure(gate.AddressReceiptSha256 is null, "review-response-address-gate-authority-invalid");
            Ensure(gate.Adapter is null, "review-response-address-gate-authority-invalid");
            Ensure(gate.AdapterSha256 is null, "review-response-address-gate-authority-invalid");
            Ensure(gate.AdapterArtifactSha256 is null, "review-response-address-gate-authority-invalid");
            Ensure(gate.Inspection is null, "review-response-address-gate-authority-invalid");
            Ensure(gate.InspectionSha256 is null, "review-response-address-gate-authority-invalid");
            Ensure(gate.Operation is null, "review-response-address-gate-authority-invalid");
        }
        else
        {
            Ensure(gate.AddressReceipt is not null, "review-response-gate-address-receipt-missing");
            Ensure(IsHash(gate.AddressReceiptSha256 ?? string.Empty), "review-response-gate-address-receipt-hash-invalid");
            Ensure(gate.Adapter is not null, "review-response-gate-adapter-missing");
            Ensure(IsHash(gate.AdapterSha256 ?? string.Empty), "review-response-gate-adapter-hash-invalid");
            Ensure(IsHash(gate.AdapterArtifactSha256 ?? string.Empty), "review-response-gate-adapter-artifact-hash-invalid");
            Ensure(gate.Inspection is not null, "review-response-gate-inspection-missing");
            Ensure(IsHash(gate.InspectionSha256 ?? string.Empty), "review-response-gate-inspection-hash-invalid");
            Ensure(gate.Operation is not null, "review-response-gate-operation-invalid");
            Ensure(Operations.Contains(gate.Operation!), "review-response-gate-operation-invalid");
            Ensure(IsHash(gate.PacketSha256), "review-response-gate-packet-hash-invalid");
        }
        Ensure(gate.Head == GitHead(root), "review-response-head-changed");
        if (gate.Mode == "reply-resolve") Ensure(StatusText(root).Length == 0, "review-response-stage12-checkout-not-clean");
        var manifest = Path.Combine(root, ".config", "dotnet-tools.json");
        EnsureNoReparsePoints(root, manifest);
        Ensure(gate.ManifestSha256 == FileSha256(manifest), "review-response-tool-manifest-changed");
        Ensure(gate.ToolVersion == CurrentToolVersion, "review-response-tool-version-changed");
        if (gate.PacketSha256.Length > 0) Ensure(FileSha256(Full(root, gate.Packet)) == gate.PacketSha256, "review-response-packet-changed");
        if (gate.AddressReceipt is not null) ValidateAddressReceipt(root, gate);
        if (gate.Adapter is not null)
        {
            _ = GovernedPath(root, gate.Adapter, ".engloop/provider-adapters/");
            Ensure(FileSha256(Full(root, gate.Adapter)) == gate.AdapterSha256, "review-response-adapter-changed");
            var adapter = ReadAdapter(root, gate.Adapter);
            var artifact = GovernedPath(root, adapter.CommandArtifact, ".engloop/provider-adapters/");
            Ensure(FileSha256(Full(root, artifact)) == gate.AdapterArtifactSha256, "review-response-adapter-artifact-changed");
            var inspectionRelative = GovernedPath(root, gate.Inspection!, ".engloop/out/code-review-response/inspections/");
            var inspectionPath = Full(root, inspectionRelative);
            Ensure(FileSha256(inspectionPath) == gate.InspectionSha256, "review-response-provider-inspection-changed");
            var packet = ReadPacket(root, gate.Packet);
            var inspection = Read<AdapterInspection>(inspectionPath, "review-response-provider-inspection-invalid");
            ValidateAdapterInspection(inspection, gate, packet);
            Ensure(inspection.Status == "ready", "review-response-provider-inspection-rejected");
        }
    }

    private static AddressPacket ReadPacket(string root, string relative)
        => Read<AddressPacket>(Full(root, relative), "review-response-packet-invalid");

    private static void ValidateAddressReceipt(string root, Gate gate)
    {
        var relative = GovernedPath(root, gate.AddressReceipt!, ".engloop/out/code-review-response/address-receipts/");
        var path = Full(root, relative);
        Ensure(File.Exists(path), "review-response-address-receipt-missing");
        Ensure(FileSha256(Full(root, gate.Packet)) == gate.PacketSha256, "review-response-packet-changed");
        Ensure(FileSha256(path) == gate.AddressReceiptSha256, "review-response-address-receipt-changed");
        var actual = Read<AddressReceipt>(path, "review-response-address-receipt-invalid");
        var packet = ReadPacket(root, gate.Packet);
        ValidateAddressReceiptValue(actual, gate, packet, gate.PacketSha256, "review-response-address-receipt-identity-mismatch");
    }

    private static void ValidateAddressReceiptValue(AddressReceipt actual, Gate currentGate, AddressPacket packet, string packetSha256, string error)
    {
        Ensure(AllPresent(actual.SchemaVersion, actual.Packet, actual.PacketSha256, actual.GateSha256,
            actual.GateJson, actual.Head, actual.Provider, actual.Repository, actual.PullRequest,
            actual.Thread, actual.SourceRevision, actual.TargetRevision, actual.Iteration,
            actual.FinalStatusDigest), "review-response-address-receipt-fields-missing");
        Ensure(actual.SchemaVersion == "1.0", "review-response-address-receipt-invalid");
        Ensure(IsHash(actual.GateSha256) && Sha256Text(actual.GateJson) == actual.GateSha256, "review-response-address-receipt-gate-invalid");
        Gate acceptedGate;
        try { acceptedGate = JsonSerializer.Deserialize<Gate>(actual.GateJson, JsonOptions) ?? throw new InvalidOperationException("review-response-address-receipt-gate-invalid"); }
        catch (JsonException) { throw new InvalidOperationException("review-response-address-receipt-gate-invalid"); }
        Ensure(acceptedGate.SchemaVersion == "1.0" && acceptedGate.Mode == "address" && IsHash(acceptedGate.SessionHash)
            && acceptedGate.Packet == actual.Packet && acceptedGate.PacketSha256.Length == 0,
            "review-response-address-receipt-gate-invalid");
        Ensure(acceptedGate.AddressReceipt is null && acceptedGate.AddressReceiptSha256 is null
            && acceptedGate.Adapter is null && acceptedGate.AdapterSha256 is null && acceptedGate.AdapterArtifactSha256 is null
            && acceptedGate.Inspection is null && acceptedGate.InspectionSha256 is null && acceptedGate.Operation is null,
            "review-response-address-receipt-gate-invalid");
        Ensure(acceptedGate.Head == actual.Head && acceptedGate.Provider == actual.Provider && acceptedGate.Repository == actual.Repository
            && acceptedGate.PullRequest == actual.PullRequest && acceptedGate.Thread == actual.Thread && acceptedGate.SourceRevision == actual.SourceRevision
            && acceptedGate.TargetRevision == actual.TargetRevision && acceptedGate.Iteration == actual.Iteration, "review-response-address-receipt-gate-invalid");
        Ensure(actual.Packet == currentGate.Packet && actual.PacketSha256 == packetSha256 && actual.Head == currentGate.Head
            && actual.Provider == currentGate.Provider && actual.Repository == currentGate.Repository && actual.PullRequest == currentGate.PullRequest
            && actual.Thread == currentGate.Thread && actual.SourceRevision == currentGate.SourceRevision && actual.TargetRevision == currentGate.TargetRevision
            && actual.Iteration == currentGate.Iteration && actual.FinalStatusDigest == packet.FinalStatusDigest
            && acceptedGate.InitialStatusDigest == packet.InitialStatusDigest && acceptedGate.ToolVersion == currentGate.ToolVersion
            && acceptedGate.ManifestSha256 == currentGate.ManifestSha256, error);
    }

    private static void ValidatePacket(AddressPacket packet, Gate gate, bool requireReadyForProvider)
    {
        Ensure(AllPresent(packet.SchemaVersion, packet.ArtifactType, packet.Provider, packet.Repository,
            packet.PullRequest, packet.Thread, packet.SourceRevision, packet.TargetRevision,
            packet.Iteration, packet.ActingPrincipal, packet.ThreadStatus, packet.Classification,
            packet.InitialHead, packet.FinalHead, packet.InitialStatusDigest, packet.FinalStatusDigest,
            packet.ChangedFiles, packet.ValidationResults, packet.Evidence, packet.UnresolvedRisks,
            packet.ReplyText, packet.AllowedOperations, packet.RequiredFixRevision, packet.ProviderHead),
            "review-response-packet-fields-missing");
        Ensure(packet.SchemaVersion == "1.0", "review-response-packet-schema-invalid");
        Ensure(packet.ArtifactType == "code-review-address", "review-response-packet-type-invalid");
        Ensure(packet.Provider == gate.Provider, "review-response-provider-identity-mismatch");
        Ensure(packet.Repository == gate.Repository, "review-response-provider-identity-mismatch");
        Ensure(packet.PullRequest == gate.PullRequest, "review-response-provider-identity-mismatch");
        Ensure(packet.Thread == gate.Thread, "review-response-provider-identity-mismatch");
        Ensure(packet.SourceRevision == gate.SourceRevision, "review-response-revision-identity-mismatch");
        Ensure(packet.TargetRevision == gate.TargetRevision, "review-response-revision-identity-mismatch");
        Ensure(packet.Iteration == gate.Iteration, "review-response-revision-identity-mismatch");
        Ensure(packet.InitialHead == gate.Head, "review-response-local-head-mismatch");
        Ensure(packet.FinalHead == gate.Head, "review-response-local-head-mismatch");
        Ensure(packet.InitialStatusDigest == gate.InitialStatusDigest, "review-response-initial-status-mismatch");
        Ensure(IsHash(packet.InitialStatusDigest), "review-response-initial-status-invalid");
        Ensure(IsHash(packet.FinalStatusDigest), "review-response-final-status-invalid");
        Ensure(packet.ProviderMutationPerformed is false, "review-response-stage11-provider-mutation-forbidden");
        Ensure(packet.CommitPushPerformed is false, "review-response-stage11-commit-push-forbidden");
        Ensure(ThreadStatuses.Contains(packet.ThreadStatus), "review-response-thread-status-invalid");
        Ensure(Classifications.Contains(packet.Classification), "review-response-classification-invalid");
        Ensure(packet.AllowedOperations.Length > 0, "review-response-operations-empty");
        Ensure(packet.AllowedOperations.Distinct(StringComparer.Ordinal).Count() == packet.AllowedOperations.Length, "review-response-operations-duplicate");
        Ensure(packet.AllowedOperations.All(Operations.Contains), "review-response-operations-invalid");
        Ensure(!string.IsNullOrWhiteSpace(packet.ReplyText), "review-response-reply-empty");
        Ensure(!string.IsNullOrWhiteSpace(packet.ActingPrincipal) && packet.ActingPrincipal.Length <= 512, "review-response-principal-missing");
        Ensure(packet.ChangedFiles.Distinct(StringComparer.Ordinal).Count() == packet.ChangedFiles.Length && packet.ChangedFiles.All(value => !string.IsNullOrWhiteSpace(value)), "review-response-changed-files-invalid");
        Ensure(packet.ValidationResults.All(value => !string.IsNullOrWhiteSpace(value)), "review-response-validation-invalid");
        Ensure(packet.Evidence.Length > 0, "review-response-evidence-missing");
        Ensure(packet.Evidence.All(value => !string.IsNullOrWhiteSpace(value)), "review-response-evidence-missing");
        Ensure(packet.UnresolvedRisks.All(value => !string.IsNullOrWhiteSpace(value)), "review-response-risks-invalid");
        Ensure(IsRevision(packet.ProviderHead), "review-response-provider-head-invalid");
        if (packet.RequiredFixRevision.Length > 0) Ensure(IsRevision(packet.RequiredFixRevision), "review-response-fix-revision-invalid");
        Ensure(packet.AdapterRequest.ValueKind == JsonValueKind.Object, "review-response-adapter-request-invalid");
        if (packet.Classification == "accepted-actionable")
        {
            Ensure(packet.ChangedFiles.Length > 0, "review-response-changed-files-missing");
            Ensure(packet.ValidationResults.Length > 0, "review-response-validation-missing");
        }
        if (!requireReadyForProvider) return;
        Ensure(packet.FinalStatusDigest == Sha256Text(string.Empty), "review-response-packet-not-refreshed-after-commit");
        Ensure(packet.ChangedFiles.Length == 0, "review-response-packet-not-refreshed-after-commit");
        var operation = gate.Operation!;
        Ensure(packet.AllowedOperations.Contains(operation, StringComparer.Ordinal), "review-response-operation-not-allowed");
        Ensure(packet.ProviderHead == gate.Head, "review-response-provider-head-not-local-head");
        if (operation.Contains("resolve", StringComparison.Ordinal))
        {
            Ensure(packet.ThreadStatus == "active", "review-response-thread-not-active");
            Ensure(IsRevision(packet.RequiredFixRevision), "review-response-fix-revision-required");
            Ensure(packet.ProviderHead == packet.RequiredFixRevision, "review-response-fix-not-on-provider-head");
            Ensure(packet.RequiredFixRevision == gate.Head, "review-response-fix-not-local-head");
        }
    }

    private static AdapterManifest ReadAdapter(string root, string relative)
    {
        var full = Full(root, relative);
        Ensure(IsTracked(root, relative), "review-response-adapter-must-be-tracked");
        Ensure(IsTrackedAtHead(root, relative), "review-response-adapter-must-match-head");
        return ReadLocked<AdapterManifest>(full, "review-response-adapter-invalid");
    }

    private static void ValidateAdapter(string root, AdapterManifest adapter, Gate gate, AddressPacket packet)
    {
        Ensure(AllPresent(adapter.SchemaVersion, adapter.AdapterId, adapter.Provider, adapter.Protocol,
            adapter.Command, adapter.CommandArtifact, adapter.CommandArtifactSha256, adapter.Capabilities),
            "review-response-adapter-fields-missing");
        Ensure(adapter.SchemaVersion == "1.0", "review-response-adapter-schema-invalid");
        Ensure(Regex.IsMatch(adapter.AdapterId, "^[a-z0-9][a-z0-9._-]{0,127}$", RegexOptions.CultureInvariant), "review-response-adapter-id-invalid");
        Ensure(adapter.Provider.Length is > 0 and <= 128 && !adapter.Provider.Any(char.IsWhiteSpace), "review-response-adapter-provider-invalid");
        Ensure(adapter.Protocol == "engloop-review-response-v1", "review-response-adapter-protocol-invalid");
        Ensure(adapter.Provider == packet.Provider, "review-response-adapter-provider-mismatch");
        Ensure(adapter.Command.Length > 0, "review-response-adapter-command-invalid");
        Ensure(adapter.Command.All(value => !string.IsNullOrWhiteSpace(value)), "review-response-adapter-command-invalid");
        Ensure(!adapter.Command.Any(value => value.IndexOfAny(['\0', '\r', '\n']) >= 0), "review-response-adapter-command-invalid");
        Ensure(!adapter.Command.Any(InlineEvaluationFlags.Contains), "review-response-adapter-shell-command-forbidden");
        var artifact = GovernedPath(root, adapter.CommandArtifact, ".engloop/provider-adapters/");
        Ensure(IsTracked(root, artifact), "review-response-adapter-artifact-must-be-tracked");
        Ensure(IsTrackedAtHead(root, artifact), "review-response-adapter-artifact-must-match-head");
        Ensure(FileSha256(Full(root, artifact)) == adapter.CommandArtifactSha256, "review-response-adapter-artifact-hash-mismatch");
        Ensure(adapter.Command.Count(value => value.Equals(artifact, StringComparison.Ordinal)) == 1, "review-response-adapter-command-artifact-invalid");
        Ensure(adapter.TimeoutSeconds is >= 1 and <= 300, "review-response-adapter-timeout-invalid");
        Ensure(adapter.Capabilities.Length > 0, "review-response-adapter-capabilities-empty");
        Ensure(adapter.Capabilities.Distinct(StringComparer.Ordinal).Count() == adapter.Capabilities.Length, "review-response-adapter-capabilities-duplicate");
        Ensure(adapter.Capabilities.All(AdapterCapabilities.Contains), "review-response-adapter-capabilities-invalid");
        Ensure(adapter.Capabilities.Contains("inspect", StringComparer.Ordinal), "review-response-adapter-inspection-capability-missing");
        Ensure(adapter.Capabilities.Contains(gate.Operation!, StringComparer.Ordinal), "review-response-adapter-capability-missing");
    }

    private static void ValidateApproval(string gatePath, Gate gate, AddressPacket packet, Approval approval)
    {
        Ensure(AllPresent(approval.SchemaVersion, approval.SessionHash, approval.GateSha256,
            approval.PacketSha256, approval.Operation, approval.ReplySha256, approval.ToolUseId,
            approval.QuestionSha256, approval.ResponseSha256, approval.QuestionJson,
            approval.ResponseJson), "review-response-approval-fields-missing");
        Ensure(approval.SchemaVersion == "1.0", "review-response-approval-identity-invalid");
        Ensure(approval.SessionHash == gate.SessionHash, "review-response-approval-identity-invalid");
        Ensure(approval.GateSha256 == FileSha256(gatePath), "review-response-approval-stale");
        Ensure(approval.PacketSha256 == gate.PacketSha256, "review-response-approval-stale");
        Ensure(approval.Operation == gate.Operation, "review-response-approval-stale");
        Ensure(approval.ReplySha256 == Sha256Text(packet.ReplyText), "review-response-approval-invalid");
        Ensure(approval.ToolUseId.Length is > 0 and <= 512, "review-response-approval-invalid");
        Ensure(IsHash(approval.QuestionSha256), "review-response-approval-invalid");
        Ensure(IsHash(approval.ResponseSha256), "review-response-approval-invalid");
        Ensure(Sha256Text(approval.QuestionJson) == approval.QuestionSha256, "review-response-approval-question-changed");
        Ensure(Sha256Text(approval.ResponseJson) == approval.ResponseSha256, "review-response-approval-response-changed");
        using var question = JsonDocument.Parse(approval.QuestionJson);
        using var response = JsonDocument.Parse(approval.ResponseJson);
        var questionText = $"Apply {gate.Operation} to thread {gate.Thread} with packet {gate.PacketSha256[..12]}?";
        ValidateQuestion(question.RootElement, questionText, ApprovalMessage(packet, gate));
        Ensure(QuestionDecision(response.RootElement, questionText, "Approve review response") == "Confirm", "review-response-approval-decision-invalid");
    }

    private static AdapterInspection InspectAdapter(string root, AdapterManifest adapter, Gate gate, AddressPacket packet)
    {
        var request = JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            phase = "inspect",
            adapterId = adapter.AdapterId,
            packet.Provider,
            packet.Repository,
            packet.PullRequest,
            packet.Thread,
            packet.SourceRevision,
            packet.TargetRevision,
            packet.Iteration,
            packet.ActingPrincipal,
            operation = gate.Operation,
            replySha256 = Sha256Text(packet.ReplyText),
            packetSha256 = gate.PacketSha256,
            addressReceiptSha256 = gate.AddressReceiptSha256,
            requiredFixRevision = packet.RequiredFixRevision,
            expectedProviderHead = packet.ProviderHead,
            expectedThreadStatus = packet.ThreadStatus,
            adapterRequest = packet.AdapterRequest,
        }, JsonOptions);
        var invoked = InvokeAdapter(root, adapter, gate, request);
        Ensure(invoked.ExitCode == 0, "review-response-provider-inspection-exit-failed");
        Ensure(TryParseAdapterInspection(invoked.Output, out var inspection), "review-response-provider-inspection-invalid");
        ValidateAdapterInspection(inspection!, gate, packet);
        return inspection!;
    }

    private static void ValidateAdapterInspection(AdapterInspection inspection, Gate gate, AddressPacket packet)
    {
        Ensure(AllPresent(inspection.SchemaVersion, inspection.Status, inspection.Provider,
            inspection.Repository, inspection.PullRequest, inspection.Thread, inspection.SourceRevision,
            inspection.TargetRevision, inspection.Iteration, inspection.ActingPrincipal,
            inspection.ThreadStatus, inspection.ProviderHead, inspection.Operation,
            inspection.PacketSha256, inspection.AddressReceiptSha256, inspection.InspectionReceiptId),
            "review-response-provider-inspection-fields-missing");
        Ensure(inspection.SchemaVersion == "1.0", "review-response-provider-inspection-schema-invalid");
        Ensure(InspectionStatuses.Contains(inspection.Status), "review-response-provider-inspection-status-invalid");
        Ensure(inspection.Provider == packet.Provider, "review-response-provider-inspection-identity-mismatch");
        Ensure(inspection.Repository == packet.Repository, "review-response-provider-inspection-identity-mismatch");
        Ensure(inspection.PullRequest == packet.PullRequest, "review-response-provider-inspection-identity-mismatch");
        Ensure(inspection.Thread == packet.Thread, "review-response-provider-inspection-identity-mismatch");
        Ensure(inspection.SourceRevision == packet.SourceRevision, "review-response-provider-inspection-revision-mismatch");
        Ensure(inspection.TargetRevision == packet.TargetRevision, "review-response-provider-inspection-revision-mismatch");
        Ensure(inspection.Iteration == packet.Iteration, "review-response-provider-inspection-revision-mismatch");
        Ensure(inspection.ActingPrincipal == packet.ActingPrincipal, "review-response-provider-inspection-principal-mismatch");
        Ensure(ThreadStatuses.Contains(inspection.ThreadStatus), "review-response-provider-inspection-thread-status-invalid");
        Ensure(inspection.ThreadStatus == packet.ThreadStatus, "review-response-provider-inspection-thread-changed");
        Ensure(inspection.ProviderHead == packet.ProviderHead, "review-response-provider-inspection-head-changed");
        Ensure(inspection.Operation == gate.Operation, "review-response-provider-inspection-operation-mismatch");
        Ensure(inspection.PacketSha256 == gate.PacketSha256, "review-response-provider-inspection-packet-mismatch");
        Ensure(inspection.AddressReceiptSha256 == gate.AddressReceiptSha256, "review-response-provider-inspection-address-receipt-mismatch");
        Ensure(inspection.MatchCount is not null, "review-response-provider-inspection-match-count-invalid");
        Ensure(inspection.MatchCount >= 0, "review-response-provider-inspection-match-count-invalid");
        Ensure(inspection.MutationPerformed is false, "review-response-provider-inspection-mutation-forbidden");
        Ensure(!string.IsNullOrWhiteSpace(inspection.InspectionReceiptId), "review-response-provider-inspection-receipt-missing");
        if (inspection.Status == "ready") Ensure(inspection.MatchCount == 1, "review-response-provider-inspection-match-count-invalid");
    }

    private static void ValidateAdapterResult(AdapterResult result, Gate gate, AddressPacket packet, AdapterInspection inspection, string marker)
    {
        Ensure(AllPresent(result.SchemaVersion, result.Status, result.Provider, result.Repository,
            result.PullRequest, result.Thread, result.SourceRevision, result.TargetRevision,
            result.Iteration, result.ActingPrincipal, result.ThreadStatus, result.ProviderHead,
            result.Operation, result.PacketSha256, result.AddressReceiptSha256, result.InspectionSha256,
            result.InspectionReceiptId, result.Marker, result.ReplySha256),
            "review-response-adapter-result-fields-missing");
        Ensure(result.SchemaVersion == "1.0", "review-response-adapter-result-schema-invalid");
        Ensure(AdapterStatuses.Contains(result.Status), "review-response-adapter-result-status-invalid");
        Ensure(result.Provider == packet.Provider && result.Repository == packet.Repository && result.PullRequest == packet.PullRequest && result.Thread == packet.Thread, "review-response-adapter-result-identity-mismatch");
        Ensure(result.SourceRevision == packet.SourceRevision && result.TargetRevision == packet.TargetRevision && result.Iteration == packet.Iteration, "review-response-adapter-result-revision-mismatch");
        Ensure(result.ActingPrincipal == packet.ActingPrincipal && result.Operation == gate.Operation && result.PacketSha256 == gate.PacketSha256 && result.Marker == marker, "review-response-adapter-result-authorization-mismatch");
        Ensure(result.AddressReceiptSha256 == gate.AddressReceiptSha256, "review-response-adapter-result-address-receipt-mismatch");
        Ensure(result.InspectionSha256 == gate.InspectionSha256 && result.InspectionReceiptId == inspection.InspectionReceiptId, "review-response-adapter-result-inspection-mismatch");
        Ensure(result.ReplyObserved is not null && result.ResolveObserved is not null && result.MatchCount is not null && result.ProviderReceiptId is not null, "review-response-adapter-result-fields-missing");
        Ensure(result.MatchCount >= 0, "review-response-adapter-result-match-count-invalid");
        Ensure(ThreadStatuses.Contains(result.ThreadStatus), "review-response-adapter-result-thread-status-invalid");
        Ensure(result.ProviderHead == packet.ProviderHead, "review-response-adapter-result-provider-head-mismatch");
        Ensure(result.ReplySha256 == Sha256Text(packet.ReplyText), "review-response-adapter-result-reply-mismatch");
        if (result.Status == "success")
        {
            var replyObserved = result.ReplyObserved.GetValueOrDefault();
            var resolveObserved = result.ResolveObserved.GetValueOrDefault();
            Ensure(result.MatchCount == 1, "review-response-adapter-result-match-count-invalid");
            Ensure(!gate.Operation!.Contains("reply", StringComparison.Ordinal) || replyObserved, "review-response-adapter-result-reply-not-observed");
            Ensure(!gate.Operation!.Contains("resolve", StringComparison.Ordinal) || resolveObserved, "review-response-adapter-result-resolution-not-observed");
            Ensure(!gate.Operation.Contains("resolve", StringComparison.Ordinal) || result.ThreadStatus == "resolved", "review-response-adapter-result-thread-not-resolved");
            Ensure(!string.IsNullOrWhiteSpace(result.ProviderReceiptId), "review-response-adapter-result-receipt-missing");
        }
    }

    private static void ValidateAttempt(Attempt attempt, Gate gate, string gatePath)
    {
        Ensure(AllPresent(attempt.SchemaVersion, attempt.State, attempt.AttemptId, attempt.GateSha256,
            attempt.PacketSha256, attempt.AdapterSha256, attempt.Operation, attempt.Marker,
            attempt.ApprovalSha256, attempt.ApprovalJson, attempt.ApprovalToolUseId, attempt.ApprovalQuestionSha256,
            attempt.ApprovalResponseSha256, attempt.ApprovalQuestionJson, attempt.ApprovalResponseJson),
            "review-response-attempt-fields-missing");
        Ensure(attempt.SchemaVersion == "1.0", "review-response-attempt-schema-invalid");
        Ensure(AttemptStates.Contains(attempt.State), "review-response-attempt-state-invalid");
        Ensure(Regex.IsMatch(attempt.AttemptId, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant), "review-response-attempt-id-invalid");
        Ensure(attempt.GateSha256 == FileSha256(gatePath), "review-response-attempt-gate-hash-invalid");
        Ensure(attempt.PacketSha256 == gate.PacketSha256 && attempt.AdapterSha256 == gate.AdapterSha256 && attempt.Operation == gate.Operation, "review-response-attempt-identity-mismatch");
        Ensure(attempt.Marker == $"elk-review-response:{gate.PacketSha256[..16]}:{attempt.AttemptId}", "review-response-attempt-marker-invalid");
        Ensure(IsHash(attempt.ApprovalSha256), "review-response-attempt-approval-invalid");
        Ensure(Sha256Text(attempt.ApprovalJson) == attempt.ApprovalSha256, "review-response-attempt-approval-invalid");
        Approval approval;
        try { approval = JsonSerializer.Deserialize<Approval>(attempt.ApprovalJson, JsonOptions) ?? throw new InvalidOperationException("review-response-attempt-approval-invalid"); }
        catch (JsonException) { throw new InvalidOperationException("review-response-attempt-approval-invalid"); }
        Ensure(attempt.ApprovalToolUseId.Length is > 0 and <= 512, "review-response-attempt-approval-invalid");
        Ensure(IsHash(attempt.ApprovalQuestionSha256), "review-response-attempt-approval-invalid");
        Ensure(IsHash(attempt.ApprovalResponseSha256), "review-response-attempt-approval-invalid");
        Ensure(Sha256Text(attempt.ApprovalQuestionJson) == attempt.ApprovalQuestionSha256, "review-response-attempt-approval-invalid");
        Ensure(Sha256Text(attempt.ApprovalResponseJson) == attempt.ApprovalResponseSha256, "review-response-attempt-approval-invalid");
        Ensure(approval.SchemaVersion == "1.0" && approval.SessionHash == gate.SessionHash
            && approval.GateSha256 == attempt.GateSha256 && approval.PacketSha256 == attempt.PacketSha256
            && approval.Operation == attempt.Operation && approval.ToolUseId == attempt.ApprovalToolUseId
            && approval.QuestionSha256 == attempt.ApprovalQuestionSha256 && approval.ResponseSha256 == attempt.ApprovalResponseSha256
            && approval.QuestionJson == attempt.ApprovalQuestionJson && approval.ResponseJson == attempt.ApprovalResponseJson,
            "review-response-attempt-approval-invalid");
        if (attempt.State == "success") Ensure(!string.IsNullOrWhiteSpace(attempt.ProviderReceiptId), "review-response-attempt-success-receipt-missing");
    }

    private static bool HasSuccessfulAttempt(string root, Gate gate, string gatePath)
    {
        var path = AttemptPath(root, gate.PacketSha256, gate.Operation!);
        if (!File.Exists(path)) return false;
        var attempt = Read<Attempt>(path, "review-response-attempt-invalid");
        ValidateAttempt(attempt, gate, gatePath);
        return attempt.State == "success";
    }

    private static bool TryParseAdapterResult(string value, out AdapterResult? result)
    {
        try { result = JsonSerializer.Deserialize<AdapterResult>(value, JsonOptions); return result is not null; }
        catch (JsonException) { result = null; return false; }
    }

    private static bool TryParseAdapterInspection(string value, out AdapterInspection? inspection)
    {
        try { inspection = JsonSerializer.Deserialize<AdapterInspection>(value, JsonOptions); return inspection is not null; }
        catch (JsonException) { inspection = null; return false; }
    }

    private static (int ExitCode, string Output, string Error) InvokeAdapter(string root, AdapterManifest adapter, Gate gate, string input)
    {
        var command = adapter.Command.ToArray();
        var artifact = Full(root, adapter.CommandArtifact);
        var runDirectory = Full(root, $".engloop/out/code-review-response/adapter-runs/{gate.SessionHash}");
        Directory.CreateDirectory(runDirectory);
        EnsureNoReparsePoints(root, runDirectory);
        var artifactName = Path.GetFileName(adapter.CommandArtifact);
        var runArtifact = Path.Combine(runDirectory,
            Path.GetFileNameWithoutExtension(artifactName) + "." + Guid.NewGuid().ToString("N") + Path.GetExtension(artifactName));
        using (var source = new FileStream(artifact, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using var destination = new FileStream(runArtifact, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            source.CopyTo(destination);
            destination.Flush(flushToDisk: true);
        }
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(runArtifact, File.GetUnixFileMode(artifact));
        Ensure(FileSha256(runArtifact) == adapter.CommandArtifactSha256, "review-response-adapter-artifact-changed");
        var artifactIndex = Array.FindIndex(command, value => value.Equals(adapter.CommandArtifact, StringComparison.Ordinal));
        Ensure(artifactIndex >= 0, "review-response-adapter-command-artifact-invalid");
        command[artifactIndex] = runArtifact;
        var start = new ProcessStartInfo(command[0])
        {
            WorkingDirectory = root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        foreach (var arg in command.Skip(1)) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("review-response-adapter-start-failed");
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(adapter.TimeoutSeconds * 1000))
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            if (!process.WaitForExit(5000)) throw new InvalidOperationException("review-response-adapter-timeout-kill-failed");
            Ensure(Task.WaitAll([outputTask, errorTask], 5000), "review-response-adapter-timeout-drain-failed");
            return (-1, outputTask.Result.Trim(), "adapter-timeout:" + errorTask.Result.Trim());
        }
        var output = outputTask.GetAwaiter().GetResult().Trim();
        var error = errorTask.GetAwaiter().GetResult().Trim();
        return (process.ExitCode, output, error);
    }

    private static bool IsDeclaredValidation(string root, string tool, string command)
    {
        if (!tool.Equals("run_in_terminal", StringComparison.OrdinalIgnoreCase)
            && !tool.Equals("execute", StringComparison.OrdinalIgnoreCase)) return false;
        if (HasShellComposition(command)) return false;
        var config = Evidence.LoadConfiguration(root);
        var allowed = new[] { config.ModuleDiscoveryCommand, config.ArchitectureCommand, config.RegressionCommand }
            .Select(value => string.Join(' ', value!));
        return allowed.Contains(command, StringComparer.Ordinal) || Regex.IsMatch(command, @"^git (status --short(?: --branch)?|diff(?: --check| --stat)?|rev-parse HEAD)$", RegexOptions.CultureInvariant);
    }

    private static bool IsAllowedAuthorEdit(JsonElement input, Gate gate)
    {
        var raw = string.Join('\n', StringValues(input)).Replace('\\', '/');
        var exactPacketPattern = "(?<![A-Za-z0-9._/-])" + Regex.Escape(gate.Packet) + "(?![A-Za-z0-9._/-])";
        var withoutSelectedPacket = Regex.Replace(raw, exactPacketPattern, string.Empty, PathRegexOptions);
        if (Regex.IsMatch(withoutSelectedPacket, "(?:^|[\\s/'\"=:])\\.(?:git|engloop)(?:/|$)", PathRegexOptions)) return false;
        return !(withoutSelectedPacket.Contains(".config", PathComparison)
            && withoutSelectedPacket.Contains("dotnet-tools.json", PathComparison));
    }

    private static IEnumerable<string> StringValues(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            yield return value.GetString()!;
            yield break;
        }
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
                foreach (var item in StringValues(property.Value))
                    yield return item;
            yield break;
        }
        if (value.ValueKind != JsonValueKind.Array) yield break;
        foreach (var element in value.EnumerateArray())
            foreach (var item in StringValues(element))
                yield return item;
    }

    private static bool IsProtectedControlPath(string path)
        => path.Equals(".config/dotnet-tools.json", StringComparison.OrdinalIgnoreCase)
            || path.Equals(".engloop/config.json", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(".engloop/provider-adapters/", StringComparison.OrdinalIgnoreCase);

    private static bool IsExactApprovalQuestion(JsonElement input, Gate gate, AddressPacket packet)
    {
        try
        {
            ValidateQuestion(ToolJson(input, "tool_input", "toolInput"), $"Apply {gate.Operation} to thread {gate.Thread} with packet {gate.PacketSha256[..12]}?", ApprovalMessage(packet, gate));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsApplyCommand(string tool, string command, string gate, string approval)
    {
        if (!tool.Equals("run_in_terminal", StringComparison.OrdinalIgnoreCase)
            && !tool.Equals("execute", StringComparison.OrdinalIgnoreCase)) return false;
        if (HasShellComposition(command)) return false;
        return command == $"dotnet tool run engloopkit -- code-review-response apply --gate {gate} --approval {approval}";
    }

    private static bool HasShellComposition(string command) => command.IndexOfAny([';', '&', '|', '>', '<', '\r', '\n']) >= 0;

    private static void ValidateQuestion(JsonElement input, string expected, string expectedMessage)
    {
        Ensure(HasExactProperties(input, "questions"), "review-response-approval-question-invalid");
        Ensure(input.TryGetProperty("questions", out var questions), "review-response-approval-question-invalid");
        Ensure(questions.ValueKind == JsonValueKind.Array, "review-response-approval-question-invalid");
        var items = questions.EnumerateArray().ToArray();
        Ensure(items.Length == 1, "review-response-approval-question-count-invalid");
        var item = items[0];
        Ensure(ReadString(item, "header") == "Approve review response", "review-response-approval-question-invalid");
        Ensure(ReadString(item, "question") == expected, "review-response-approval-question-invalid");
        Ensure(ReadString(item, "message") == expectedMessage, "review-response-approval-message-invalid");
        Ensure(item.TryGetProperty("multiSelect", out var multi), "review-response-approval-multiselect-forbidden");
        Ensure(multi.ValueKind == JsonValueKind.False, "review-response-approval-multiselect-forbidden");
        Ensure(item.TryGetProperty("allowFreeformInput", out var freeform), "review-response-approval-freeform-forbidden");
        Ensure(freeform.ValueKind == JsonValueKind.False, "review-response-approval-freeform-forbidden");
        Ensure(item.TryGetProperty("options", out var options), "review-response-approval-options-invalid");
        Ensure(options.ValueKind == JsonValueKind.Array, "review-response-approval-options-invalid");
        Ensure(options.EnumerateArray().All(option => HasExactProperties(option, "label")), "review-response-approval-options-invalid");
        Ensure(options.EnumerateArray().Select(option => ReadString(option, "label")).SequenceEqual(new[] { "Confirm", "Cancel" }, StringComparer.Ordinal), "review-response-approval-options-invalid");
        Ensure(HasExactProperties(item, "header", "question", "message", "multiSelect", "allowFreeformInput", "options"), "review-response-approval-question-invalid");
    }

    private static string ApprovalMessage(AddressPacket packet, Gate gate)
    {
        var json = JsonSerializer.Serialize(new
        {
            packet.Provider,
            packet.Repository,
            packet.PullRequest,
            packet.Thread,
            packet.SourceRevision,
            packet.TargetRevision,
            packet.Iteration,
            packet.ActingPrincipal,
            packet.ThreadStatus,
            packet.Classification,
            packet.ProviderHead,
            operation = gate.Operation,
            packet.ReplyText,
            packet.Evidence,
            packet.UnresolvedRisks,
            packetSha256 = gate.PacketSha256,
            addressReceiptSha256 = gate.AddressReceiptSha256,
            providerInspectionSha256 = gate.InspectionSha256,
        }, new JsonSerializerOptions(JsonOptions) { WriteIndented = true });
        return "Review this exact provider operation before confirming:\n\n```json\n" + json + "\n```";
    }

    private static string QuestionDecision(JsonElement response, string question, string header)
    {
        Ensure(HasExactProperties(response, "answers"), "review-response-approval-answer-invalid");
        Ensure(response.TryGetProperty("answers", out var answers), "review-response-approval-answer-invalid");
        Ensure(answers.ValueKind == JsonValueKind.Object, "review-response-approval-answer-invalid");
        var answerProperties = answers.EnumerateObject().ToArray();
        Ensure(answerProperties.Length == 1, "review-response-approval-selection-invalid");
        var found = answers.TryGetProperty(question, out var answer) || answers.TryGetProperty(header, out answer);
        Ensure(found, "review-response-approval-selection-invalid");
        Ensure(HasExactProperties(answer, "selected"), "review-response-approval-selection-invalid");
        Ensure(answer.TryGetProperty("selected", out var selected), "review-response-approval-selection-invalid");
        Ensure(selected.ValueKind == JsonValueKind.Array, "review-response-approval-selection-invalid");
        var values = selected.EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
        Ensure(values.Length == 1, "review-response-approval-selection-invalid");
        return values[0];
    }

    private static JsonElement ToolJson(JsonElement input, string snake, string camel)
    {
        foreach (var name in new[] { snake, camel })
        {
            if (!input.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Object) return value;
            if (value.ValueKind == JsonValueKind.String)
            {
                using var parsed = JsonDocument.Parse(value.GetString()!);
                return parsed.RootElement.Clone();
            }
        }
        throw new InvalidOperationException("review-response-hook-tool-json-missing");
    }

    private static string ToolCommand(JsonElement input)
    {
        foreach (var name in new[] { "tool_input", "toolInput" })
            if (input.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object)
                return ReadString(value, "command", "commandLine");
        return string.Empty;
    }

    private static string Argument(string prompt, string name)
    {
        var match = Regex.Match(prompt, "(?:^|\\s)" + Regex.Escape(name) + "(?:=|\\s+)(?:\\\"(?<dq>[^\\\"]+)\\\"|'(?<sq>[^']+)'|(?<bare>[^\\s]+))");
        Ensure(match.Success, "review-response-option-missing:" + name);
        return match.Groups["dq"].Value + match.Groups["sq"].Value + match.Groups["bare"].Value;
    }

    private static string Option(string[] args, string name)
    {
        var index = Array.FindIndex(args, value => value == name);
        Ensure(index >= 0, "review-response-option-missing:" + name);
        Ensure(index + 1 < args.Length, "review-response-option-missing:" + name);
        Ensure(!args[index + 1].StartsWith("--", StringComparison.Ordinal), "review-response-option-missing:" + name);
        return args[index + 1];
    }

    private static string Operation(string value)
    {
        Ensure(Operations.Contains(value), "review-response-operation-invalid");
        return value;
    }

    private static string Identity(string value, string name)
    {
        Ensure(value.Length > 0, "review-response-" + name + "-invalid");
        Ensure(value.Length <= 512, "review-response-" + name + "-invalid");
        Ensure(!value.Any(char.IsWhiteSpace), "review-response-" + name + "-invalid");
        return value;
    }

    private static string Revision(string value, string name)
    {
        Ensure(IsRevision(value), "review-response-" + name + "-revision-invalid");
        return value.ToLowerInvariant();
    }

    private static bool IsRevision(string value) => Regex.IsMatch(value, "^[a-fA-F0-9]{40,64}$", RegexOptions.CultureInvariant);
    private static bool IsHash(string value) => Regex.IsMatch(value, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant);
    private static bool IsIdentity(string value) => value.Length is > 0 and <= 512 && !value.Any(char.IsWhiteSpace);

    private static string GovernedPath(string root, string value, string prefix)
    {
        var normalized = value.Replace('\\', '/');
        Ensure(normalized.Length > prefix.Length && !Path.IsPathRooted(normalized) && !normalized.Contains('\0'), "review-response-path-invalid");
        var full = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefixFull = Path.GetFullPath(Path.Combine(root, prefix.Replace('/', Path.DirectorySeparatorChar))).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Ensure(full.StartsWith(prefixFull, PathComparison), "review-response-path-invalid");
        var canonical = Path.GetRelativePath(root, full).Replace('\\', '/');
        Ensure(canonical.Equals(normalized, PathComparison), "review-response-path-invalid");
        EnsureNoReparsePoints(root, full);
        return canonical;
    }

    private static string PacketPath(string root, string value)
    {
        var canonical = GovernedPath(root, value, ".engloop/out/code-review-response/address/");
        Ensure(Regex.IsMatch(canonical, @"^\.engloop/out/code-review-response/address/[A-Za-z0-9._-]+\.json$", RegexOptions.CultureInvariant), "review-response-packet-path-invalid");
        return canonical;
    }

    private static string GatePath(string root, string sessionHash, string mode)
        => Full(root, $".engloop/out/code-review-response/gates/{sessionHash}.{mode}.json");

    private static string ApprovalRelative(string sessionHash)
        => $".engloop/out/code-review-response/approvals/{sessionHash}.json";

    private static string AttemptPath(string root, string packetHash, string operation)
        => Full(root, $".engloop/out/code-review-response/attempts/{packetHash}.{operation}.json");

    private static string InspectionRelative(string packetHash, string operation, string sessionHash)
        => $".engloop/out/code-review-response/inspections/{packetHash}.{operation}.{sessionHash}.json";

    private static string AddressReceiptRelative(string packet, string packetHash)
        => $".engloop/out/code-review-response/address-receipts/{Sha256Text(packet + "\n" + packetHash)}.json";

    private static string Full(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static T Read<T>(string path, string error)
    {
        Ensure(File.Exists(path), error);
        return ReadLocked<T>(path, error);
    }

    private static T ReadLocked<T>(string path, string error)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new InvalidOperationException(error);
        }
        catch (JsonException) { throw new InvalidOperationException(error); }
        catch (IOException) { throw new InvalidOperationException(error); }
        catch (UnauthorizedAccessException) { throw new InvalidOperationException(error); }
    }

    private static void WriteJson<T>(string path, T value) => WriteJsonAtomic(path, value, overwrite: true, string.Empty);

    private static void WriteJsonCreateNew<T>(string path, T value, string existsError)
        => WriteJsonAtomic(path, value, overwrite: false, existsError);

    private static void WriteJsonAtomic<T>(string path, T value, bool overwrite, string existsError)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            try { File.Move(temporary, path, overwrite); }
            catch (IOException) when (!overwrite && File.Exists(path)) { throw new InvalidOperationException(existsError); }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static FileStream AcquireExclusiveLock(string root, string path, string error)
    {
        EnsureNoReparsePoints(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        EnsureNoReparsePoints(root, Path.GetDirectoryName(path)!);
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.WriteThrough); }
        catch (IOException) { throw new InvalidOperationException(error); }
        catch (UnauthorizedAccessException) { throw new InvalidOperationException(error); }
    }

    private static void RequireAgentEntry(string root, string mode)
    {
        var stage = mode == "address" ? "speckit.engloop.11-codereview-address" : "speckit.engloop.12-codereview-reply-resolve";
        var result = ValidationCommands.EvaluateAgentEntry(["--stage", stage, "--root", root]);
        Ensure(result.Passed, "review-response-agent-entry-rejected:" + result.Reason);
    }

    private static string ExactGitRoot(string cwd)
    {
        Ensure(cwd.Length > 0, "review-response-cwd-missing");
        var selected = Path.GetFullPath(cwd).TrimEnd(Path.DirectorySeparatorChar);
        var result = Git(selected, "rev-parse", "--show-toplevel");
        Ensure(result.ExitCode == 0, "review-response-git-root-unavailable");
        var root = Path.GetFullPath(result.Output.Trim()).TrimEnd(Path.DirectorySeparatorChar);
        Ensure(root.Equals(selected, PathComparison), "review-response-cwd-not-exact-root");
        EnsureNoReparsePoints(root, Full(root, ".engloop/out/code-review-response"));
        var ignored = Git(root, "check-ignore", "-q", "--no-index", "--", ".engloop/out/code-review-response/.probe");
        Ensure(ignored.ExitCode == 0, "review-response-output-not-ignored");
        return root;
    }

    private static string GitHead(string root)
    {
        var result = Git(root, "rev-parse", "HEAD");
        Ensure(result.ExitCode == 0 && IsRevision(result.Output.Trim()), "review-response-git-head-unavailable");
        return result.Output.Trim().ToLowerInvariant();
    }

    private static bool IsTracked(string root, string relative) => Git(root, "ls-files", "--error-unmatch", "--", relative).ExitCode == 0;

    private static bool IsTrackedAtHead(string root, string relative)
    {
        var canonical = relative.Replace('\\', '/');
        var current = Git(root, "hash-object", "--path=" + canonical, "--", canonical);
        var committed = Git(root, "rev-parse", "HEAD:" + canonical);
        return (current.ExitCode == 0) & (committed.ExitCode == 0)
            & current.Output.Trim().Equals(committed.Output.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string StatusDigest(string root)
    {
        return Sha256Text(StatusText(root));
    }

    private static string StatusText(string root)
    {
        var result = Git(root, "status", "--porcelain=v1", "--untracked-files=all", "--", ":(exclude).engloop/out");
        Ensure(result.ExitCode == 0, "review-response-status-unavailable");
        return result.Output.Replace("\r\n", "\n").TrimEnd('\n');
    }

    private static string[] StatusPaths(string root)
    {
        var result = Git(root, "status", "--porcelain=v1", "-z", "--untracked-files=all", "--", ":(exclude).engloop/out");
        Ensure(result.ExitCode == 0, "review-response-status-unavailable");
        var tokens = result.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var paths = new List<string>();
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            Ensure(token.Length >= 3, "review-response-status-invalid");
            var status = token[..2];
            paths.Add(token[3..].Replace('\\', '/'));
            if (status.IndexOfAny(['R', 'C']) >= 0)
            {
                Ensure(++index < tokens.Length, "review-response-status-invalid");
                paths.Add(tokens[index].Replace('\\', '/'));
            }
        }
        return paths.Where(path => path.Length > 0).OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private static void EnsureNoReparsePoints(string root, string full)
    {
        var relative = Path.GetRelativePath(root, full);
        var current = root;
        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            Ensure((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "review-response-path-reparse-forbidden");
        }
    }

    private static (int ExitCode, string Output) Git(string root, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("review-response-git-start-failed");
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static string FileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Sha256Text(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names) if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString()!;
        return string.Empty;
    }

    private static void WriteDecision(bool allow, string reason)
        => Console.WriteLine(JsonSerializer.Serialize(new { @continue = true, stopReason = (string?)null, hookSpecificOutput = new { hookEventName = "PreToolUse", permissionDecision = allow ? "allow" : "deny", permissionDecisionReason = reason } }));

    private static void WriteHook(bool continueValue, string reason = "", string systemMessage = "")
        => Console.WriteLine(JsonSerializer.Serialize(new { @continue = continueValue, stopReason = reason.Length == 0 ? null : reason, systemMessage = systemMessage.Length == 0 ? null : systemMessage }));

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool AllPresent(params object?[] values) => values.All(value => value is not null);

    private static bool HasExactProperties(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        var actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        return actual.Length == expected.Length
            && actual.OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
