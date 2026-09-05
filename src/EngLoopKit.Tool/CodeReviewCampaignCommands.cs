using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EngLoopKit.Core;

namespace EngLoopKit.Tool;

internal sealed record ReviewCampaignComment(
    string Comment,
    string ParentComment,
    string Author,
    string Content,
    string CreatedAt,
    string UpdatedAt,
    bool? IsDeleted);

internal sealed record ReviewCampaignItem(
    string ItemId,
    string Thread,
    string RootComment,
    string Status,
    string ObservationState,
    string AnchorPath,
    int? Line,
    string Summary,
    string UpdatedAt,
    string ContentSha256,
    ReviewCampaignComment[] Comments,
    JsonElement ProviderState,
    string ProposedClassification,
    string HumanDecision,
    string Priority,
    bool DecisionStale,
    string DecisionReceiptId,
    string DecisionToolUseId,
    string DecisionQuestionSha256,
    string DecisionResponseSha256,
    string SourceState,
    string ResponseState,
    string RequiredFixRevision,
    string ResponsePacket,
    string ResponsePacketSha256,
    string ProviderReceiptId);

internal sealed record ReviewCampaignTracker(
    string SchemaVersion,
    string ArtifactType,
    string CampaignId,
    string Provider,
    string Repository,
    string PullRequest,
    string Adapter,
    string AdapterSha256,
    string AdapterArtifactSha256,
    string ActingPrincipal,
    string SourceRevision,
    string TargetRevision,
    string Iteration,
    string ProviderHead,
    int RefreshNumber,
    string Snapshot,
    string SnapshotSha256,
    string DiscoveryReceiptId,
    int PageCount,
    bool? PaginationComplete,
    bool? ProviderMutationPerformed,
    string SelectedItemId,
    ReviewCampaignItem[] Items,
    string TrackerSha256);

internal sealed record ReviewCampaignBinding(
    ReviewCampaignTracker Tracker,
    ReviewCampaignItem Item,
    string TrackerPath,
    string TrackerSha256,
    string CampaignReceiptPath,
    string CampaignReceiptSha256);

internal sealed class ReviewCampaignMutationLease : IDisposable
{
    private readonly FileStream _lock;

    internal ReviewCampaignMutationLease(FileStream @lock, ReviewCampaignBinding binding)
    {
        _lock = @lock;
        Binding = binding;
    }

    internal ReviewCampaignBinding Binding { get; }

    public void Dispose() => _lock.Dispose();
}

public static class CodeReviewCampaignCommands
{
    private const string StageId = "speckit.engloop.11-codereview-campaign";
    private const string Protocol = "engloop-review-response-v2";
    private static readonly string CurrentToolVersion = typeof(CodeReviewCampaignCommands).Assembly.GetName().Version!.ToString(3);
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
    };

    private static readonly HashSet<string> ReadTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "read_file", "file_search", "grep_search", "semantic_search", "list_dir",
    };

    internal static readonly HashSet<string> Classifications = new(StringComparer.Ordinal)
    {
        "accepted-actionable", "already-addressed", "stale", "disputed", "clarification-required", "unsupported-out-of-scope",
    };

    private static readonly string[] ClassificationOrder =
    [
        "accepted-actionable", "already-addressed", "stale", "disputed", "clarification-required", "unsupported-out-of-scope",
    ];

    private static readonly string[] PriorityOrder = ["urgent", "high", "normal", "low"];
    private static readonly HashSet<string> ThreadStatuses = new(StringComparer.Ordinal) { "active", "resolved" };
    private static readonly HashSet<string> TrackerStatuses = new(StringComparer.Ordinal) { "active", "resolved", "not-returned" };
    private static readonly HashSet<string> SourceStates = new(StringComparer.Ordinal)
    {
        "pending", "addressing", "validated-local", "branch-update-required", "fix-on-provider-head", "not-applicable",
    };
    private static readonly HashSet<string> ResponseStates = new(StringComparer.Ordinal)
    {
        "pending", "response-ready", "replied", "resolved", "outcome-unknown",
    };
    private static readonly HashSet<string> AdapterCapabilities = new(StringComparer.Ordinal)
    {
        "discover", "inspect", "reply", "resolve", "reply-and-resolve",
    };
    private static readonly HashSet<string> InlineEvaluationFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "-Command", "-EncodedCommand", "--command", "-c", "/c", "--eval", "-e",
    };

    private sealed record CampaignGate(
        string SchemaVersion,
        string SessionHash,
        string Head,
        string StatusDigest,
        string ToolVersion,
        string ManifestSha256,
        string Provider,
        string Repository,
        string PullRequest,
        string Adapter,
        string AdapterSha256,
        string AdapterArtifactSha256,
        string Tracker,
        string TrackerSha256,
        string CampaignId,
        int RefreshNumber);

    private sealed record CampaignReceipt(
        string SchemaVersion,
        string Tracker,
        string TrackerSha256,
        string CampaignId,
        string Provider,
        string Repository,
        string PullRequest,
        int RefreshNumber,
        string GateSha256,
        string GateJson);

    private sealed record CampaignCompletionReceipt(
        string SchemaVersion,
        string SessionHash,
        string Tracker,
        string TrackerSha256,
        string TrackerJson,
        string CampaignReceipt,
        string CampaignReceiptSha256,
        string CampaignId,
        int Active,
        int Undecided,
        string SelectedItemId);

    private sealed record CampaignTransaction(
        string SchemaVersion,
        string Kind,
        string CampaignId,
        string SessionHash,
        string PreviousTrackerSha256,
        string TrackerPath,
        ReviewCampaignTracker Tracker,
        string GatePath,
        CampaignGate Gate,
        string SnapshotJson);

    private sealed record CampaignAdapterManifest(
        string SchemaVersion,
        string AdapterId,
        string Provider,
        string Protocol,
        string[] Command,
        string CommandArtifact,
        string CommandArtifactSha256,
        int TimeoutSeconds,
        string[] Capabilities,
        JsonElement DiscoveryRequest);

    private sealed record DiscoveryComment(
        string Comment,
        string ParentComment,
        string Author,
        string Content,
        string CreatedAt,
        string UpdatedAt,
        bool? IsDeleted);

    private sealed record DiscoveryThread(
        string Thread,
        string RootComment,
        string Status,
        string AnchorPath,
        int? Line,
        string Summary,
        string UpdatedAt,
        DiscoveryComment[] Comments,
        JsonElement ProviderState);

    private sealed record DiscoveryResult(
        string SchemaVersion,
        string Status,
        string Provider,
        string Repository,
        string PullRequest,
        string ActingPrincipal,
        string SourceRevision,
        string TargetRevision,
        string Iteration,
        string ProviderHead,
        int? PageCount,
        bool? PaginationComplete,
        string ContinuationToken,
        int? ThreadCount,
        DiscoveryThread[] Threads,
        string DiscoveryReceiptId,
        bool? MutationPerformed);

    public static int ExecuteHook(string[] args)
    {
        var action = args.Length > 0 ? args[0] : "unknown";
        try
        {
            Ensure(action is "initialize" or "guard" or "post-tool" or "stop",
                "review-campaign-action-invalid");
            using var input = JsonDocument.Parse(Console.In.ReadToEnd());
            var root = ExactGitRoot(ReadString(input.RootElement, "cwd"));
            RequireAgentEntry(root);
            var session = ReadString(input.RootElement, "session_id", "sessionId");
            Ensure(session.Length > 0, "review-campaign-session-missing");
            var sessionHash = Sha256Text(session);
            var gatePath = GatePath(root, sessionHash);
            using var sessionLock = action is "initialize" or "post-tool" or "stop"
                ? AcquireExclusiveLock(root, gatePath + ".lock", "review-campaign-session-operation-in-progress")
                : null;
            var prompt = ReadString(input.RootElement, "prompt");
            var campaignId = CampaignIdForHook(root, action, sessionHash, gatePath, prompt);
            using var campaignLock = AcquireExclusiveLock(root, CampaignLockPath(root, campaignId),
                "review-campaign-operation-in-progress");
            RecoverCampaignTransaction(root, campaignId);
            return action switch
            {
                "initialize" => Initialize(root, sessionHash, gatePath, prompt),
                "guard" => Guard(root, gatePath, input.RootElement),
                "post-tool" => PostTool(root, gatePath, input.RootElement),
                "stop" => Stop(root, sessionHash, gatePath),
                _ => throw new UnreachableException(),
            };
        }
        catch (Exception ex)
        {
            WriteHook(false, "Code-review campaign hook failed closed: " + ex.Message);
            return 0;
        }
    }

    private static int Initialize(string root, string sessionHash, string gatePath, string prompt)
    {
        Ensure(!File.Exists(CampaignCompletionPath(root, sessionHash)),
            "review-campaign-session-already-completed");
        var provider = Identity(Argument(prompt, "--provider"), "provider");
        var repository = Identity(Argument(prompt, "--repository"), "repository");
        var pullRequest = Identity(Argument(prompt, "--pr"), "pr");
        var adapterRelative = GovernedPath(root, Argument(prompt, "--adapter"), ".engloop/provider-adapters/");
        if (File.Exists(gatePath))
        {
            var existingGate = ReadGate(root, gatePath);
            Ensure(existingGate.SessionHash == sessionHash & existingGate.Provider == provider
                & existingGate.Repository == repository & existingGate.PullRequest == pullRequest
                & existingGate.Adapter == adapterRelative, "review-campaign-gate-already-exists");
            var existingTracker = ReadTracker(root, existingGate.Tracker);
            WriteCampaignScopeActive(existingTracker);
            return 0;
        }
        var adapter = ReadAdapter(root, adapterRelative);
        ValidateAdapter(root, adapter, adapterRelative, provider);

        var campaignId = Sha256Text(provider + "\n" + repository + "\n" + pullRequest);
        EnsureNoProviderClaims(root, campaignId);
        var trackerRelative = TrackerRelative(campaignId);
        ReviewCampaignTracker? previous = null;
        if (File.Exists(Full(root, trackerRelative)))
        {
            previous = ReadTracker(root, trackerRelative);
            Ensure(previous.Provider == provider & previous.Repository == repository & previous.PullRequest == pullRequest,
                "review-campaign-tracker-identity-mismatch");
            _ = ValidateCampaignReceipt(root, trackerRelative, previous);
        }

        var refreshNumber = (previous?.RefreshNumber ?? 0) + 1;
        var request = JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            phase = "discover",
            adapterId = adapter.AdapterId,
            provider,
            repository,
            pullRequest,
            campaignId,
            previousTrackerSha256 = previous?.TrackerSha256 ?? string.Empty,
            previousRefreshNumber = previous?.RefreshNumber ?? 0,
            discoveryRequest = adapter.DiscoveryRequest,
        }, JsonOptions);
        var invoked = InvokeAdapter(root, adapter, sessionHash, request);
        Ensure(invoked.ExitCode == 0, "review-campaign-provider-discovery-exit-failed");
        Ensure(TryParseDiscovery(invoked.Output, out var discovery), "review-campaign-provider-discovery-invalid");
        ValidateDiscovery(discovery!, provider, repository, pullRequest);
        Ensure(discovery!.Status == "success", "review-campaign-provider-discovery-rejected");

        var snapshotRelative = SnapshotRelative(campaignId, refreshNumber);
        var snapshotJson = JsonSerializer.Serialize(discovery, JsonOptions);
        var tracker = MergeTracker(previous, discovery, campaignId, adapterRelative,
            FileSha256(Full(root, adapterRelative)), FileSha256(Full(root, adapter.CommandArtifact)),
            refreshNumber, snapshotRelative, Sha256Text(snapshotJson));

        var manifest = Path.Combine(root, ".config", "dotnet-tools.json");
        var gate = new CampaignGate("1.0", sessionHash, GitHead(root), StatusDigest(root), CurrentToolVersion,
            FileSha256(manifest), provider, repository, pullRequest, adapterRelative,
            FileSha256(Full(root, adapterRelative)), FileSha256(Full(root, adapter.CommandArtifact)),
            trackerRelative, tracker.TrackerSha256, campaignId, refreshNumber);
        CommitCampaignTransaction(root, new CampaignTransaction("1.0", "initialize",
            campaignId, sessionHash, previous?.TrackerSha256 ?? string.Empty,
            trackerRelative, tracker, Path.GetRelativePath(root, gatePath).Replace('\\', '/'),
            gate, snapshotJson));
        WriteCampaignScopeActive(tracker);
        return 0;
    }

    private static void WriteCampaignScopeActive(ReviewCampaignTracker tracker)
    {
        var active = tracker.Items.Count(item => item.Status == "active" && item.ObservationState == "current");
        var undecided = tracker.Items.Count(item => item.Status == "active" && item.ObservationState == "current"
            && (item.HumanDecision == "pending" || item.DecisionStale));
        WriteHook(true, systemMessage: $"CODE_REVIEW_CAMPAIGN_SCOPE_ACTIVE tracker={TrackerRelative(tracker.CampaignId)} campaign={tracker.CampaignId} active={active} undecided={undecided}");
    }

    private static int Guard(string root, string gatePath, JsonElement input)
    {
        var gate = ReadGate(root, gatePath);
        var tracker = ReadTracker(root, gate.Tracker);
        var tool = ReadString(input, "tool_name", "toolName");
        var command = ToolCommand(input);
        var allow = ReadTools.Contains(tool)
            || IsEntryValidationCommand(tool, command)
            || tool.Equals("vscode_askQuestions", StringComparison.OrdinalIgnoreCase)
                && IsExactCampaignQuestion(ToolJson(input, "tool_input", "toolInput"), tracker);
        WriteDecision(allow, allow ? "Allowed by the exact review-campaign policy." : "Denied by the review-campaign authority boundary.");
        return 0;
    }

    private static int PostTool(string root, string gatePath, JsonElement input)
    {
        if (!ReadString(input, "tool_name", "toolName").Equals("vscode_askQuestions", StringComparison.OrdinalIgnoreCase))
        {
            WriteHook(true);
            return 0;
        }
        var gate = ReadGate(root, gatePath);
        var tracker = ReadTracker(root, gate.Tracker);
        EnsureNoProviderClaims(root, gate.CampaignId);
        var toolUseId = ReadString(input, "tool_use_id", "toolUseId");
        Ensure(toolUseId.Length is > 0 and <= 512, "review-campaign-question-tool-use-id-invalid");
        var question = ToolJson(input, "tool_input", "toolInput");
        var response = ToolJson(input, "tool_response", "toolResponse");
        var questionJson = question.GetRawText();
        var responseJson = response.GetRawText();
        var previousTrackerSha256 = tracker.TrackerSha256;

        if (TryValidateSelectionQuestion(question, tracker, out var selectionQuestion))
        {
            var selected = QuestionDecision(response, selectionQuestion, "Select review thread");
            Ensure(tracker.Items.Any(item => item.ItemId == selected & item.Status == "active" & item.ObservationState == "current"),
                "review-campaign-selected-item-invalid");
            if (tracker.SelectedItemId == selected)
            {
                WriteHook(true, systemMessage: $"CODE_REVIEW_CAMPAIGN_UPDATED tracker={gate.Tracker} selected={selected}");
                return 0;
            }
            tracker = Rehash(tracker with { SelectedItemId = selected });
        }
        else
        {
            Ensure(TryValidateTriageQuestion(question, tracker, out var proposal, out var classificationQuestion, out var priorityQuestion),
                "review-campaign-question-invalid");
            var decision = QuestionDecision(response, classificationQuestion, "Classify review thread");
            var priority = QuestionDecision(response, priorityQuestion, "Prioritize review thread");
            Ensure(Classifications.Contains(decision), "review-campaign-decision-invalid");
            Ensure(PriorityOrder.Contains(priority, StringComparer.Ordinal), "review-campaign-priority-invalid");
            var itemIndex = Array.FindIndex(tracker.Items, item => item.ItemId == tracker.SelectedItemId);
            Ensure(itemIndex >= 0, "review-campaign-selected-item-invalid");
            var items = tracker.Items.ToArray();
            var existing = items[itemIndex];
            if (existing.DecisionToolUseId == toolUseId)
            {
                Ensure(existing.ProposedClassification == proposal
                    & existing.HumanDecision == decision & existing.Priority == priority
                    & !existing.DecisionStale
                    & existing.DecisionQuestionSha256 == Sha256Text(questionJson)
                    & existing.DecisionResponseSha256 == Sha256Text(responseJson),
                    "review-campaign-question-tool-use-id-reused");
                WriteHook(true, systemMessage: $"CODE_REVIEW_CAMPAIGN_UPDATED tracker={gate.Tracker} selected={tracker.SelectedItemId}");
                return 0;
            }
            Ensure(!tracker.Items.Any(item => item.DecisionToolUseId == toolUseId),
                "review-campaign-question-tool-use-id-reused");
            items[itemIndex] = items[itemIndex] with
            {
                ProposedClassification = proposal,
                HumanDecision = decision,
                Priority = priority,
                DecisionStale = false,
                DecisionReceiptId = Guid.NewGuid().ToString("N"),
                DecisionToolUseId = toolUseId,
                DecisionQuestionSha256 = Sha256Text(questionJson),
                DecisionResponseSha256 = Sha256Text(responseJson),
                SourceState = decision == "accepted-actionable" ? "pending" : "not-applicable",
            };
            tracker = Rehash(tracker with { Items = items });
        }

        gate = gate with { TrackerSha256 = tracker.TrackerSha256 };
        CommitCampaignTransaction(root, new CampaignTransaction("1.0", "post-tool",
            gate.CampaignId, gate.SessionHash, previousTrackerSha256, gate.Tracker,
            tracker, Path.GetRelativePath(root, gatePath).Replace('\\', '/'), gate,
            string.Empty));
        WriteHook(true, systemMessage: $"CODE_REVIEW_CAMPAIGN_UPDATED tracker={gate.Tracker} selected={tracker.SelectedItemId}");
        return 0;
    }

    private static int Stop(string root, string sessionHash, string gatePath)
    {
        var completionRelative = CampaignCompletionRelative(sessionHash);
        var completionPath = Full(root, completionRelative);
        if (!File.Exists(gatePath))
        {
            var existing = Read<CampaignCompletionReceipt>(completionPath,
                "review-campaign-completion-receipt-missing-or-invalid");
            ValidateCampaignCompletionReceipt(root, sessionHash, existing);
            WriteCampaignCompleted(existing, completionRelative, alreadyCompleted: true);
            return 0;
        }
        if (File.Exists(completionPath))
        {
            var existing = Read<CampaignCompletionReceipt>(completionPath,
                "review-campaign-completion-receipt-missing-or-invalid");
            ValidateCampaignCompletionReceipt(root, sessionHash, existing);
            File.Delete(gatePath);
            WriteCampaignCompleted(existing, completionRelative, alreadyCompleted: true);
            return 0;
        }
        var gate = ReadGate(root, gatePath);
        var tracker = ReadTracker(root, gate.Tracker);
        var active = tracker.Items.Count(item => item.Status == "active" & item.ObservationState == "current");
        if (active > 0)
        {
            Ensure(IsHash(tracker.SelectedItemId), "review-campaign-selection-required");
            _ = RequireItemForStage(tracker, tracker.SelectedItemId);
        }
        else
            Ensure(tracker.SelectedItemId.Length == 0, "review-campaign-selected-item-invalid");
        var undecided = tracker.Items.Count(item => item.Status == "active" & item.ObservationState == "current"
            & (item.HumanDecision == "pending" | item.DecisionStale));
        var gateJson = File.ReadAllText(gatePath);
        var receiptRelative = ReceiptRelative(gate.Tracker, tracker.TrackerSha256);
        var receiptPath = Full(root, receiptRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        var receipt = new CampaignReceipt("1.0", gate.Tracker, tracker.TrackerSha256,
            tracker.CampaignId, tracker.Provider, tracker.Repository, tracker.PullRequest,
            tracker.RefreshNumber, FileSha256(gatePath), gateJson);
        WriteJsonCreateNewOrExact(receiptPath, receipt, "review-campaign-receipt-conflict");
        var trackerJson = File.ReadAllText(Full(root, gate.Tracker));
        var completion = new CampaignCompletionReceipt("1.0", sessionHash, gate.Tracker,
            tracker.TrackerSha256, trackerJson, receiptRelative, FileSha256(receiptPath),
            tracker.CampaignId, active, undecided, tracker.SelectedItemId);
        Directory.CreateDirectory(Path.GetDirectoryName(completionPath)!);
        WriteJsonCreateNewOrExact(completionPath, completion,
            "review-campaign-completion-receipt-conflict");
        ValidateCampaignCompletionReceipt(root, sessionHash, completion);
        File.Delete(gatePath);
        WriteCampaignCompleted(completion, completionRelative, alreadyCompleted: false);
        return 0;
    }

    private static void WriteCampaignCompleted(CampaignCompletionReceipt completion,
        string completionRelative, bool alreadyCompleted)
        => WriteHook(true, systemMessage: $"CODE_REVIEW_CAMPAIGN_OK tracker={completion.Tracker} receipt={completion.CampaignReceipt} completionReceipt={completionRelative} campaign={completion.CampaignId} active={completion.Active} undecided={completion.Undecided} selected={completion.SelectedItemId} alreadyCompleted={alreadyCompleted.ToString().ToLowerInvariant()}");

    private static void ValidateCampaignCompletionReceipt(string root, string sessionHash,
        CampaignCompletionReceipt completion)
    {
        Ensure(AllPresent(completion.SchemaVersion, completion.SessionHash,
            completion.Tracker, completion.TrackerSha256, completion.TrackerJson,
            completion.CampaignReceipt, completion.CampaignReceiptSha256,
            completion.CampaignId, completion.SelectedItemId),
            "review-campaign-completion-receipt-fields-missing");
        Ensure(completion.SchemaVersion == "1.0"
            & completion.SessionHash == sessionHash & IsHash(completion.SessionHash)
            & IsHash(completion.TrackerSha256)
            & IsHash(completion.CampaignReceiptSha256)
            & IsHash(completion.CampaignId)
            & completion.Active >= 0 & completion.Undecided >= 0
            & (completion.SelectedItemId.Length == 0
                | IsHash(completion.SelectedItemId)),
            "review-campaign-completion-receipt-invalid");
        var trackerRelative = NormalizeTrackerPath(root, completion.Tracker);
        Ensure(trackerRelative == TrackerRelative(completion.CampaignId),
            "review-campaign-completion-receipt-invalid");
        ReviewCampaignTracker tracker;
        try { tracker = JsonSerializer.Deserialize<ReviewCampaignTracker>(
            completion.TrackerJson, JsonOptions)
            ?? throw new InvalidOperationException(
                "review-campaign-completion-receipt-invalid"); }
        catch (JsonException) { throw new InvalidOperationException(
            "review-campaign-completion-receipt-invalid"); }
        ValidateTracker(root, tracker, trackerRelative);
        Ensure(tracker.TrackerSha256 == completion.TrackerSha256
            & tracker.CampaignId == completion.CampaignId
            & tracker.SelectedItemId == completion.SelectedItemId
            & tracker.Items.Count(item => item.Status == "active"
                & item.ObservationState == "current") == completion.Active
            & tracker.Items.Count(item => item.Status == "active"
                & item.ObservationState == "current"
                & (item.HumanDecision == "pending" | item.DecisionStale))
                == completion.Undecided,
            "review-campaign-completion-tracker-changed");
        var receiptRelative = GovernedPath(root, completion.CampaignReceipt,
            ".engloop/out/code-review-response/campaign-receipts/");
        Ensure(receiptRelative == ReceiptRelative(trackerRelative,
            completion.TrackerSha256),
            "review-campaign-completion-receipt-invalid");
        var receiptPath = Full(root, receiptRelative);
        Ensure(FileSha256(receiptPath) == completion.CampaignReceiptSha256,
            "review-campaign-completion-receipt-changed");
        var validated = ValidateCampaignReceipt(root, trackerRelative, tracker);
        Ensure(validated.Relative == receiptRelative
            & validated.Sha256 == completion.CampaignReceiptSha256,
            "review-campaign-completion-receipt-invalid");
        var receipt = Read<CampaignReceipt>(receiptPath,
            "review-campaign-receipt-missing-or-invalid");
        CampaignGate gate;
        try { gate = JsonSerializer.Deserialize<CampaignGate>(receipt.GateJson, JsonOptions)
            ?? throw new InvalidOperationException(
                "review-campaign-completion-receipt-invalid"); }
        catch (JsonException) { throw new InvalidOperationException(
            "review-campaign-completion-receipt-invalid"); }
        Ensure(gate.SessionHash == sessionHash,
            "review-campaign-completion-receipt-invalid");
    }

    internal static ReviewCampaignTracker ReadTrackerForStage(string root, string relative)
    {
        var normalized = NormalizeTrackerPath(root, relative);
        using var campaignLock = AcquireExclusiveLock(root,
            CampaignLockPath(root, CampaignIdFromTrackerPath(normalized)),
            "review-campaign-operation-in-progress");
        return ReadTracker(root, normalized);
    }

    internal static ReviewCampaignItem RequireItemForStage(ReviewCampaignTracker tracker, string itemId)
    {
        Ensure(IsHash(itemId), "review-campaign-item-id-invalid");
        var matches = tracker.Items.Where(item => item.ItemId == itemId).ToArray();
        Ensure(matches.Length == 1, "review-campaign-item-match-invalid");
        var item = matches[0];
        Ensure(item.Status == "active" & item.ObservationState == "current", "review-campaign-item-not-active");
        Ensure(item.HumanDecision != "pending" & Classifications.Contains(item.HumanDecision), "review-campaign-item-decision-missing");
        Ensure(!item.DecisionStale, "review-campaign-item-decision-stale");
        Ensure(!string.IsNullOrWhiteSpace(item.DecisionReceiptId), "review-campaign-item-decision-receipt-missing");
        return item;
    }

    internal static ReviewCampaignBinding BindItemForStage(string root, string trackerRelative, string itemId)
    {
        var normalized = NormalizeTrackerPath(root, trackerRelative);
        using var campaignLock = AcquireExclusiveLock(root,
            CampaignLockPath(root, CampaignIdFromTrackerPath(normalized)),
            "review-campaign-operation-in-progress");
        return BindItemForStageLocked(root, normalized, itemId);
    }

    private static ReviewCampaignBinding BindItemForStageLocked(string root, string normalized, string itemId)
    {
        var tracker = ReadTracker(root, normalized);
        var item = RequireItemForStage(tracker, itemId);
        Ensure(tracker.SelectedItemId == itemId, "review-campaign-item-not-selected");
        var validatedReceipt = ValidateCampaignReceipt(root, normalized, tracker);
        return new ReviewCampaignBinding(tracker, item, normalized, tracker.TrackerSha256,
            validatedReceipt.Relative, validatedReceipt.Sha256);
    }

    private static (string Relative, string Sha256) ValidateCampaignReceipt(
        string root, string trackerRelative, ReviewCampaignTracker tracker)
    {
        var receiptRelative = ReceiptRelative(trackerRelative, tracker.TrackerSha256);
        var receiptPath = Full(root, receiptRelative);
        var receipt = Read<CampaignReceipt>(receiptPath, "review-campaign-receipt-missing-or-invalid");
        Ensure(receipt.SchemaVersion == "1.0" & receipt.Tracker == trackerRelative
            & receipt.TrackerSha256 == tracker.TrackerSha256 & receipt.CampaignId == tracker.CampaignId
            & receipt.Provider == tracker.Provider & receipt.Repository == tracker.Repository
            & receipt.PullRequest == tracker.PullRequest & receipt.RefreshNumber == tracker.RefreshNumber
            & IsHash(receipt.GateSha256) & Sha256Text(receipt.GateJson) == receipt.GateSha256,
            "review-campaign-receipt-invalid");
        CampaignGate gate;
        try { gate = JsonSerializer.Deserialize<CampaignGate>(receipt.GateJson, JsonOptions)
            ?? throw new InvalidOperationException("review-campaign-receipt-invalid"); }
        catch (JsonException) { throw new InvalidOperationException("review-campaign-receipt-invalid"); }
        Ensure(gate.SchemaVersion == "1.0" & gate.ToolVersion == CurrentToolVersion
            & gate.Tracker == trackerRelative & gate.TrackerSha256 == tracker.TrackerSha256
            & gate.CampaignId == tracker.CampaignId & gate.RefreshNumber == tracker.RefreshNumber
            & gate.Provider == tracker.Provider & gate.Repository == tracker.Repository
            & gate.PullRequest == tracker.PullRequest & gate.Adapter == tracker.Adapter
            & gate.AdapterSha256 == tracker.AdapterSha256
            & gate.AdapterArtifactSha256 == tracker.AdapterArtifactSha256,
            "review-campaign-receipt-invalid");
        return (receiptRelative, FileSha256(receiptPath));
    }

    internal static ReviewCampaignBinding CompleteItemForStage(string root, string trackerRelative,
        string itemId, string expectedTrackerSha256, string packet, string packetSha256,
        string requiredFixRevision, string operation, string providerReceiptId)
    {
        var normalized = NormalizeTrackerPath(root, trackerRelative);
        using var campaignLock = AcquireExclusiveLock(root,
            CampaignLockPath(root, CampaignIdFromTrackerPath(normalized)),
            "review-campaign-operation-in-progress");
        var binding = BindItemForStageLocked(root, normalized, itemId);
        return CompleteItemForStageLocked(root, binding, expectedTrackerSha256, packet,
            packetSha256, requiredFixRevision, operation, providerReceiptId);
    }

    internal static ReviewCampaignMutationLease AcquireItemMutationLease(string root,
        string trackerRelative, string itemId, string expectedTrackerSha256)
    {
        var normalized = NormalizeTrackerPath(root, trackerRelative);
        var campaignLock = AcquireExclusiveLock(root,
            CampaignLockPath(root, CampaignIdFromTrackerPath(normalized)),
            "review-campaign-operation-in-progress");
        try
        {
            var binding = BindItemForStageLocked(root, normalized, itemId);
            Ensure(binding.TrackerSha256 == expectedTrackerSha256,
                "review-campaign-completion-tracker-changed");
            return new ReviewCampaignMutationLease(campaignLock, binding);
        }
        catch
        {
            campaignLock.Dispose();
            throw;
        }
    }

    internal static ReviewCampaignBinding CompleteItemForStageUnderLease(string root,
        ReviewCampaignMutationLease lease, string expectedTrackerSha256, string packet,
        string packetSha256, string requiredFixRevision, string operation,
        string providerReceiptId)
        => CompleteItemForStageLocked(root, lease.Binding, expectedTrackerSha256,
            packet, packetSha256, requiredFixRevision, operation, providerReceiptId);

    private static ReviewCampaignBinding CompleteItemForStageLocked(string root,
        ReviewCampaignBinding binding, string expectedTrackerSha256, string packet,
        string packetSha256, string requiredFixRevision, string operation,
        string providerReceiptId)
    {
        Ensure(binding.TrackerSha256 == expectedTrackerSha256,
            "review-campaign-completion-tracker-changed");
        Ensure(IsHash(packetSha256) & !string.IsNullOrWhiteSpace(packet)
            & !string.IsNullOrWhiteSpace(providerReceiptId), "review-campaign-completion-evidence-invalid");
        Ensure(operation is "reply" or "resolve" or "reply-and-resolve", "review-campaign-completion-operation-invalid");
        var index = Array.FindIndex(binding.Tracker.Items,
            item => item.ItemId == binding.Item.ItemId);
        Ensure(index >= 0, "review-campaign-selected-item-invalid");
        var items = binding.Tracker.Items.ToArray();
        var selected = items[index];
        var completed = selected with
        {
            Status = operation.Contains("resolve", StringComparison.Ordinal) ? "resolved" : selected.Status,
            SourceState = selected.HumanDecision == "accepted-actionable" ? "fix-on-provider-head" : "not-applicable",
            ResponseState = operation.Contains("resolve", StringComparison.Ordinal) ? "resolved" : "replied",
            RequiredFixRevision = requiredFixRevision,
            ResponsePacket = packet,
            ResponsePacketSha256 = packetSha256,
            ProviderReceiptId = providerReceiptId,
        };
        items[index] = completed with { ContentSha256 = ItemContentHash(completed) };
        var updated = Rehash(binding.Tracker with { Items = items, SelectedItemId = string.Empty });
        var oldReceipt = Read<CampaignReceipt>(Full(root, binding.CampaignReceiptPath),
            "review-campaign-receipt-missing-or-invalid");
        CampaignGate oldGate;
        try { oldGate = JsonSerializer.Deserialize<CampaignGate>(oldReceipt.GateJson, JsonOptions)
            ?? throw new InvalidOperationException("review-campaign-receipt-invalid"); }
        catch (JsonException) { throw new InvalidOperationException("review-campaign-receipt-invalid"); }
        var updatedGate = oldGate with { TrackerSha256 = updated.TrackerSha256 };
        var gateJson = JsonSerializer.Serialize(updatedGate, JsonOptions);
        var receiptRelative = ReceiptRelative(binding.TrackerPath, updated.TrackerSha256);
        var receiptPath = Full(root, receiptRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        WriteJsonCreateNewOrExact(receiptPath, new CampaignReceipt("1.0", binding.TrackerPath,
            updated.TrackerSha256, updated.CampaignId, updated.Provider, updated.Repository,
            updated.PullRequest, updated.RefreshNumber, Sha256Text(gateJson), gateJson),
            "review-campaign-receipt-conflict");
        WriteJson(Full(root, binding.TrackerPath), updated);
        var verified = ReadTracker(root, binding.TrackerPath);
        var verifiedItem = verified.Items.Single(item => item.ItemId == binding.Item.ItemId);
        return new ReviewCampaignBinding(verified, verifiedItem, binding.TrackerPath,
            verified.TrackerSha256, receiptRelative, FileSha256(receiptPath));
    }

    internal static bool TryGetCompletedItemForStage(string root, string trackerRelative,
        string itemId, string packet, string packetSha256, string requiredFixRevision,
        string operation, string providerReceiptId, out ReviewCampaignBinding? binding)
    {
        var normalized = NormalizeTrackerPath(root, trackerRelative);
        using var campaignLock = AcquireExclusiveLock(root,
            CampaignLockPath(root, CampaignIdFromTrackerPath(normalized)),
            "review-campaign-operation-in-progress");
        var tracker = ReadTracker(root, normalized);
        var matches = tracker.Items.Where(item => item.ItemId == itemId).ToArray();
        if (matches.Length != 1)
        {
            binding = null;
            return false;
        }

        var item = matches[0];
        var expectedResponseState = operation.Contains("resolve", StringComparison.Ordinal)
            ? "resolved"
            : "replied";
        var completed = item.ResponsePacket == packet
            & item.ResponsePacketSha256 == packetSha256
            & item.RequiredFixRevision == requiredFixRevision
            & item.ResponseState == expectedResponseState
            & item.ProviderReceiptId == providerReceiptId
            & (!operation.Contains("resolve", StringComparison.Ordinal) | item.Status == "resolved");
        if (!completed)
        {
            binding = null;
            return false;
        }

        var validatedReceipt = ValidateCampaignReceipt(root, normalized, tracker);
        binding = new ReviewCampaignBinding(tracker, item, normalized, tracker.TrackerSha256,
            validatedReceipt.Relative, validatedReceipt.Sha256);
        return true;
    }

    internal static void ValidateHistoricalCampaignReceipt(string root, string trackerRelative,
        string trackerSha256, string receiptRelative, string receiptSha256)
    {
        var normalized = NormalizeTrackerPath(root, trackerRelative);
        using var campaignLock = AcquireExclusiveLock(root,
            CampaignLockPath(root, CampaignIdFromTrackerPath(normalized)),
            "review-campaign-operation-in-progress");
        var current = ReadTracker(root, normalized);
        var normalizedReceipt = GovernedPath(root, receiptRelative,
            ".engloop/out/code-review-response/campaign-receipts/");
        Ensure(normalizedReceipt == ReceiptRelative(normalized, trackerSha256),
            "review-campaign-receipt-path-mismatch");
        var receiptPath = Full(root, normalizedReceipt);
        Ensure(FileSha256(receiptPath) == receiptSha256, "review-campaign-receipt-changed");
        var receipt = Read<CampaignReceipt>(receiptPath, "review-campaign-receipt-missing-or-invalid");
        Ensure(receipt.SchemaVersion == "1.0" & receipt.Tracker == normalized
            & receipt.TrackerSha256 == trackerSha256 & receipt.CampaignId == current.CampaignId
            & receipt.Provider == current.Provider & receipt.Repository == current.Repository
            & receipt.PullRequest == current.PullRequest & receipt.RefreshNumber >= 1
            & receipt.RefreshNumber <= current.RefreshNumber & IsHash(receipt.GateSha256)
            & Sha256Text(receipt.GateJson) == receipt.GateSha256,
            "review-campaign-receipt-invalid");
        CampaignGate gate;
        try { gate = JsonSerializer.Deserialize<CampaignGate>(receipt.GateJson, JsonOptions)
            ?? throw new InvalidOperationException("review-campaign-receipt-invalid"); }
        catch (JsonException) { throw new InvalidOperationException("review-campaign-receipt-invalid"); }
        Ensure(gate.SchemaVersion == "1.0" & gate.ToolVersion == CurrentToolVersion
            & gate.Tracker == normalized & gate.TrackerSha256 == trackerSha256
            & gate.CampaignId == current.CampaignId & gate.RefreshNumber == receipt.RefreshNumber
            & gate.Provider == current.Provider & gate.Repository == current.Repository
            & gate.PullRequest == current.PullRequest,
            "review-campaign-receipt-invalid");
    }

    internal static string NormalizeTrackerPath(string root, string relative)
        => GovernedPath(root, relative, ".engloop/out/code-review-response/campaigns/");

    internal static string ComputeFileSha256(string path) => FileSha256(path);

    private static CampaignGate ReadGate(string root, string gatePath)
    {
        var gate = Read<CampaignGate>(gatePath, "review-campaign-gate-missing-or-invalid");
        ValidateGateStatic(root, gate);
        var tracker = ReadTracker(root, gate.Tracker);
        Ensure(tracker.TrackerSha256 == gate.TrackerSha256 & tracker.CampaignId == gate.CampaignId
            & tracker.RefreshNumber == gate.RefreshNumber, "review-campaign-tracker-changed");
        return gate;
    }

    private static void ValidateGateStatic(string root, CampaignGate gate)
    {
        Ensure(gate.SchemaVersion == "1.0" & IsHash(gate.SessionHash) & IsRevision(gate.Head)
            & IsHash(gate.StatusDigest) & gate.ToolVersion == CurrentToolVersion & IsHash(gate.ManifestSha256)
            & IsIdentity(gate.Provider) & IsIdentity(gate.Repository) & IsIdentity(gate.PullRequest)
            & IsHash(gate.AdapterSha256) & IsHash(gate.AdapterArtifactSha256) & IsHash(gate.TrackerSha256)
            & IsHash(gate.CampaignId) & gate.RefreshNumber >= 1, "review-campaign-gate-invalid");
        _ = GovernedPath(root, gate.Adapter, ".engloop/provider-adapters/");
        _ = NormalizeTrackerPath(root, gate.Tracker);
        Ensure(gate.Head == GitHead(root), "review-campaign-head-changed");
        Ensure(gate.StatusDigest == StatusDigest(root), "review-campaign-worktree-changed");
        var manifest = Path.Combine(root, ".config", "dotnet-tools.json");
        Ensure(FileSha256(manifest) == gate.ManifestSha256, "review-campaign-tool-manifest-changed");
        Ensure(FileSha256(Full(root, gate.Adapter)) == gate.AdapterSha256, "review-campaign-adapter-changed");
        var adapter = ReadAdapter(root, gate.Adapter);
        Ensure(FileSha256(Full(root, adapter.CommandArtifact)) == gate.AdapterArtifactSha256, "review-campaign-adapter-artifact-changed");
    }

    private static ReviewCampaignTracker ReadTracker(string root, string relative)
    {
        var normalized = NormalizeTrackerPath(root, relative);
        var tracker = Read<ReviewCampaignTracker>(Full(root, normalized), "review-campaign-tracker-invalid");
        ValidateTracker(root, tracker, normalized);
        return tracker;
    }

    private static void ValidateTracker(string root, ReviewCampaignTracker tracker, string relative,
        bool requireSnapshotFile = true)
    {
        Ensure(tracker.SchemaVersion == "1.0" & tracker.ArtifactType == "code-review-campaign"
            & IsHash(tracker.CampaignId) & IsIdentity(tracker.Provider) & IsIdentity(tracker.Repository)
            & IsIdentity(tracker.PullRequest) & IsRevision(tracker.SourceRevision) & IsRevision(tracker.TargetRevision)
            & IsRevision(tracker.ProviderHead) & tracker.RefreshNumber >= 1 & IsHash(tracker.SnapshotSha256)
            & !string.IsNullOrWhiteSpace(tracker.DiscoveryReceiptId) & tracker.PageCount >= 1
            & tracker.PaginationComplete is true & tracker.ProviderMutationPerformed is false
            & IsHash(tracker.TrackerSha256), "review-campaign-tracker-invalid");
        Ensure(tracker.CampaignId == Sha256Text(tracker.Provider + "\n" + tracker.Repository + "\n" + tracker.PullRequest),
            "review-campaign-id-mismatch");
        Ensure(relative == TrackerRelative(tracker.CampaignId), "review-campaign-tracker-path-mismatch");
        Ensure(tracker.TrackerSha256 == TrackerHash(tracker), "review-campaign-tracker-hash-mismatch");
        var adapterRelative = GovernedPath(root, tracker.Adapter, ".engloop/provider-adapters/");
        var adapter = ReadAdapter(root, adapterRelative);
        ValidateAdapter(root, adapter, adapterRelative, tracker.Provider);
        Ensure(FileSha256(Full(root, adapterRelative)) == tracker.AdapterSha256
            & FileSha256(Full(root, adapter.CommandArtifact)) == tracker.AdapterArtifactSha256,
            "review-campaign-tracker-adapter-mismatch");
        var snapshotRelative = GovernedPath(root, tracker.Snapshot,
            ".engloop/out/code-review-response/campaign-snapshots/");
        Ensure(snapshotRelative == SnapshotRelative(tracker.CampaignId, tracker.RefreshNumber),
            "review-campaign-tracker-snapshot-mismatch");
        if (requireSnapshotFile)
            Ensure(File.Exists(Full(root, snapshotRelative))
                & FileSha256(Full(root, snapshotRelative)) == tracker.SnapshotSha256,
                "review-campaign-tracker-snapshot-mismatch");
        Ensure(tracker.Items.Select(item => item.ItemId).Distinct(StringComparer.Ordinal).Count() == tracker.Items.Length,
            "review-campaign-item-duplicate");
        foreach (var item in tracker.Items)
        {
            Ensure(IsHash(item.ItemId) & IsIdentity(item.Thread) & IsIdentity(item.RootComment)
                & TrackerStatuses.Contains(item.Status) & item.ObservationState is "current" or "not-returned"
                & IsHash(item.ContentSha256) & item.Comments.Length > 0 & item.ProviderState.ValueKind == JsonValueKind.Object
                & (item.ProposedClassification == "pending-analysis" | Classifications.Contains(item.ProposedClassification))
                & (item.HumanDecision == "pending" | Classifications.Contains(item.HumanDecision))
                & (item.Priority == "unassigned" | PriorityOrder.Contains(item.Priority, StringComparer.Ordinal))
                & SourceStates.Contains(item.SourceState) & ResponseStates.Contains(item.ResponseState),
                "review-campaign-item-invalid");
            Ensure(item.ItemId == ItemId(tracker.Provider, tracker.Repository, tracker.PullRequest,
                item.Thread, item.RootComment), "review-campaign-item-id-mismatch");
            Ensure(item.ContentSha256 == ItemContentHash(item), "review-campaign-item-content-hash-mismatch");
            Ensure(item.Comments.Select(comment => comment.Comment).Distinct(StringComparer.Ordinal).Count() == item.Comments.Length
                & item.Comments.Any(comment => comment.Comment == item.RootComment), "review-campaign-comment-duplicate");
            Ensure(item.Comments.All(comment => IsIdentity(comment.Comment)
                & !string.IsNullOrWhiteSpace(comment.Author)
                & !string.IsNullOrWhiteSpace(comment.CreatedAt)
                & !string.IsNullOrWhiteSpace(comment.UpdatedAt)
                & comment.IsDeleted is not null), "review-campaign-comment-invalid");
            Ensure(item.ObservationState == "not-returned" == (item.Status == "not-returned"),
                "review-campaign-item-observation-mismatch");
            if (item.HumanDecision == "pending")
            {
                Ensure(item.Priority == "unassigned" & item.DecisionReceiptId.Length == 0
                    & item.DecisionToolUseId.Length == 0 & item.DecisionQuestionSha256.Length == 0
                    & item.DecisionResponseSha256.Length == 0, "review-campaign-pending-decision-evidence-forbidden");
            }
            else
            {
                Ensure(item.Priority != "unassigned" & Regex.IsMatch(item.DecisionReceiptId, "^[a-f0-9]{32}$")
                    & item.DecisionToolUseId.Length is > 0 and <= 512
                    & IsHash(item.DecisionQuestionSha256) & IsHash(item.DecisionResponseSha256),
                    "review-campaign-decision-evidence-invalid");
            }
            Ensure(item.RequiredFixRevision.Length == 0 || IsRevision(item.RequiredFixRevision),
                "review-campaign-fix-revision-invalid");
            Ensure((item.ResponsePacket.Length == 0) == (item.ResponsePacketSha256.Length == 0),
                "review-campaign-response-packet-evidence-invalid");
            if (item.ResponsePacketSha256.Length > 0)
                Ensure(IsHash(item.ResponsePacketSha256), "review-campaign-response-packet-evidence-invalid");
            if (item.ProviderReceiptId.Length > 0)
                Ensure(item.ResponseState is "replied" or "resolved", "review-campaign-provider-receipt-state-invalid");
        }
        Ensure(tracker.SelectedItemId.Length == 0
            | tracker.Items.Count(item => item.ItemId == tracker.SelectedItemId
                & item.Status == "active" & item.ObservationState == "current") == 1,
            "review-campaign-selected-item-invalid");
    }

    private static ReviewCampaignTracker MergeTracker(ReviewCampaignTracker? previous, DiscoveryResult result,
        string campaignId, string adapter, string adapterSha256, string adapterArtifactSha256,
        int refreshNumber, string snapshot, string snapshotSha256)
    {
        var previousById = previous?.Items.ToDictionary(item => item.ItemId, StringComparer.Ordinal)
            ?? new Dictionary<string, ReviewCampaignItem>(StringComparer.Ordinal);
        var currentIds = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<ReviewCampaignItem>();
        foreach (var thread in result.Threads)
        {
            var itemId = ItemId(result.Provider, result.Repository, result.PullRequest, thread.Thread, thread.RootComment);
            Ensure(currentIds.Add(itemId), "review-campaign-item-duplicate");
            var comments = thread.Comments.Select(comment => new ReviewCampaignComment(comment.Comment,
                comment.ParentComment, comment.Author, comment.Content, comment.CreatedAt, comment.UpdatedAt,
                comment.IsDeleted)).ToArray();
            var contentSha = ItemContentHash(thread, comments);
            if (previousById.TryGetValue(itemId, out var old))
            {
                var decisionStale = old.DecisionStale || old.HumanDecision != "pending"
                    && (old.ContentSha256 != contentSha || old.Status != thread.Status);
                items.Add(old with
                {
                    Status = thread.Status,
                    ObservationState = "current",
                    AnchorPath = thread.AnchorPath,
                    Line = thread.Line,
                    Summary = thread.Summary,
                    UpdatedAt = thread.UpdatedAt,
                    ContentSha256 = contentSha,
                    Comments = comments,
                    ProviderState = thread.ProviderState.Clone(),
                    DecisionStale = decisionStale,
                });
            }
            else
            {
                items.Add(new ReviewCampaignItem(itemId, thread.Thread, thread.RootComment, thread.Status, "current",
                    thread.AnchorPath, thread.Line, thread.Summary, thread.UpdatedAt, contentSha, comments,
                    thread.ProviderState.Clone(), "pending-analysis", "pending", "unassigned", false,
                    string.Empty, string.Empty, string.Empty, string.Empty, "pending", "pending",
                    string.Empty, string.Empty, string.Empty, string.Empty));
            }
        }

        foreach (var old in previousById.Values.Where(item => !currentIds.Contains(item.ItemId)))
            items.Add(old with { Status = "not-returned", ObservationState = "not-returned" });

        var selected = previous?.SelectedItemId ?? string.Empty;
        if (!items.Any(item => item.ItemId == selected && item.Status == "active" && item.ObservationState == "current"))
            selected = string.Empty;
        var tracker = new ReviewCampaignTracker("1.0", "code-review-campaign", campaignId, result.Provider,
            result.Repository, result.PullRequest, adapter, adapterSha256, adapterArtifactSha256,
            result.ActingPrincipal, result.SourceRevision, result.TargetRevision, result.Iteration,
            result.ProviderHead, refreshNumber, snapshot, snapshotSha256, result.DiscoveryReceiptId,
            result.PageCount!.Value, result.PaginationComplete, false, selected,
            items.OrderBy(item => item.ItemId, StringComparer.Ordinal).ToArray(), string.Empty);
        return Rehash(tracker);
    }

    private static ReviewCampaignTracker Rehash(ReviewCampaignTracker tracker)
        => tracker with { TrackerSha256 = TrackerHash(tracker) };

    private static string TrackerHash(ReviewCampaignTracker tracker)
        => Sha256Text(JsonSerializer.Serialize(tracker with { TrackerSha256 = string.Empty }, JsonOptions));

    private static string ItemId(string provider, string repository, string pullRequest, string thread, string rootComment)
        => Sha256Text(provider + "\n" + repository + "\n" + pullRequest + "\n" + thread + "\n" + rootComment);

    private static string ItemContentHash(DiscoveryThread thread, ReviewCampaignComment[] comments)
        => Sha256Text(JsonSerializer.Serialize(new
        {
            thread.Thread,
            thread.RootComment,
            thread.AnchorPath,
            thread.Line,
            thread.Summary,
            thread.UpdatedAt,
            comments,
            providerState = thread.ProviderState,
        }, JsonOptions));

    private static string ItemContentHash(ReviewCampaignItem item)
        => Sha256Text(JsonSerializer.Serialize(new
        {
            item.Thread,
            item.RootComment,
            item.AnchorPath,
            item.Line,
            item.Summary,
            item.UpdatedAt,
            item.Comments,
            item.ProviderState,
        }, JsonOptions));

    private static bool IsExactCampaignQuestion(JsonElement question, ReviewCampaignTracker tracker)
        => TryValidateSelectionQuestion(question, tracker, out _)
            || TryValidateTriageQuestion(question, tracker, out _, out _, out _);

    private static bool TryValidateSelectionQuestion(JsonElement input, ReviewCampaignTracker tracker, out string questionText)
    {
        questionText = $"Select one active review thread from campaign {tracker.CampaignId[..12]}.";
        try
        {
            Ensure(HasExactProperties(input, "questions"), "review-campaign-question-invalid");
            var questions = input.GetProperty("questions").EnumerateArray().ToArray();
            Ensure(questions.Length == 1, "review-campaign-question-invalid");
            var item = questions[0];
            Ensure(HasExactProperties(item, "header", "question", "message", "multiSelect", "allowFreeformInput", "options"),
                "review-campaign-question-invalid");
            Ensure(ReadString(item, "header") == "Select review thread" & ReadString(item, "question") == questionText,
                "review-campaign-question-invalid");
            Ensure(ReadString(item, "message") == SelectionMessage(tracker), "review-campaign-question-invalid");
            Ensure(item.GetProperty("multiSelect").ValueKind == JsonValueKind.False
                & item.GetProperty("allowFreeformInput").ValueKind == JsonValueKind.False, "review-campaign-question-invalid");
            var options = item.GetProperty("options").EnumerateArray().ToArray();
            var expected = tracker.Items.Where(candidate => candidate.Status == "active" & candidate.ObservationState == "current")
                .OrderBy(candidate => candidate.ItemId, StringComparer.Ordinal).Select(candidate => candidate.ItemId).ToArray();
            Ensure(expected.Length > 0 & options.Length == expected.Length, "review-campaign-question-invalid");
            Ensure(options.All(option => HasExactProperties(option, "label")), "review-campaign-question-invalid");
            Ensure(options.Select(option => ReadString(option, "label")).SequenceEqual(expected, StringComparer.Ordinal),
                "review-campaign-question-invalid");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryValidateTriageQuestion(JsonElement input, ReviewCampaignTracker tracker,
        out string proposal, out string classificationQuestion, out string priorityQuestion)
    {
        proposal = string.Empty;
        classificationQuestion = $"Classify item {Short(tracker.SelectedItemId)}.";
        priorityQuestion = $"Prioritize item {Short(tracker.SelectedItemId)}.";
        try
        {
            Ensure(tracker.SelectedItemId.Length == 64, "review-campaign-selected-item-invalid");
            var selected = RequireCurrentSelected(tracker);
            Ensure(HasExactProperties(input, "questions"), "review-campaign-question-invalid");
            var questions = input.GetProperty("questions").EnumerateArray().ToArray();
            Ensure(questions.Length == 2, "review-campaign-question-invalid");
            var classification = questions[0];
            var priority = questions[1];
            foreach (var candidate in ClassificationOrder)
            {
                if (ReadString(classification, "message") == TriageMessage(tracker, selected, candidate))
                {
                    proposal = candidate;
                    break;
                }
            }
            Ensure(proposal.Length > 0, "review-campaign-proposal-invalid");
            ValidateFixedQuestion(classification, "Classify review thread", classificationQuestion,
                ClassificationOrder);
            ValidateFixedQuestion(priority, "Prioritize review thread", priorityQuestion, PriorityOrder);
            Ensure(ReadString(priority, "message") == $"Set the author priority for campaign item {selected.ItemId}.",
                "review-campaign-question-invalid");
            return true;
        }
        catch (Exception)
        {
            proposal = string.Empty;
            return false;
        }
    }

    private static void ValidateFixedQuestion(JsonElement item, string header, string question, string[] labels)
    {
        Ensure(HasExactProperties(item, "header", "question", "message", "multiSelect", "allowFreeformInput", "options"),
            "review-campaign-question-invalid");
        Ensure(ReadString(item, "header") == header & ReadString(item, "question") == question,
            "review-campaign-question-invalid");
        Ensure(item.GetProperty("multiSelect").ValueKind == JsonValueKind.False
            & item.GetProperty("allowFreeformInput").ValueKind == JsonValueKind.False,
            "review-campaign-question-invalid");
        var options = item.GetProperty("options").EnumerateArray().ToArray();
        Ensure(options.All(option => HasExactProperties(option, "label"))
            & options.Select(option => ReadString(option, "label")).SequenceEqual(labels, StringComparer.Ordinal),
            "review-campaign-question-invalid");
    }

    private static ReviewCampaignItem RequireCurrentSelected(ReviewCampaignTracker tracker)
    {
        var matches = tracker.Items.Where(item => item.ItemId == tracker.SelectedItemId
            & item.Status == "active" & item.ObservationState == "current").ToArray();
        Ensure(matches.Length == 1, "review-campaign-selected-item-invalid");
        return matches[0];
    }

    private static string SelectionMessage(ReviewCampaignTracker tracker)
    {
        var projection = tracker.Items.Where(item => item.Status == "active" && item.ObservationState == "current")
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .Select(item => new { item.ItemId, item.AnchorPath, item.Line, item.Summary, item.HumanDecision, item.Priority, item.DecisionStale });
        return "Select one exact current item; no item is selected automatically.\n\n```json\n"
            + JsonSerializer.Serialize(projection, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }) + "\n```";
    }

    private static string TriageMessage(ReviewCampaignTracker tracker, ReviewCampaignItem item, string proposal)
    {
        var projection = new
        {
            tracker.CampaignId,
            item.ItemId,
            item.Thread,
            item.RootComment,
            item.AnchorPath,
            item.Line,
            item.Summary,
            item.Comments,
            proposedClassification = proposal,
            item.ContentSha256,
        };
        return "Review the exact current provider item and the non-authoritative proposal before deciding.\n\n```json\n"
            + JsonSerializer.Serialize(projection, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }) + "\n```";
    }

    private static string QuestionDecision(JsonElement response, string question, string header)
    {
        Ensure(HasExactProperties(response, "answers"), "review-campaign-answer-invalid");
        var answers = response.GetProperty("answers");
        Ensure(answers.ValueKind == JsonValueKind.Object, "review-campaign-answer-invalid");
        var properties = answers.EnumerateObject().ToArray();
        var match = properties.Where(property => property.Name == question || property.Name == header).ToArray();
        Ensure(match.Length == 1, "review-campaign-answer-invalid");
        var answer = match[0].Value;
        Ensure(HasExactProperties(answer, "selected"), "review-campaign-answer-invalid");
        var values = answer.GetProperty("selected").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
        Ensure(values.Length == 1, "review-campaign-answer-invalid");
        return values[0];
    }

    private static CampaignAdapterManifest ReadAdapter(string root, string relative)
    {
        Ensure(IsTracked(root, relative), "review-campaign-adapter-must-be-tracked");
        Ensure(IsTrackedAtHead(root, relative), "review-campaign-adapter-must-match-head");
        return Read<CampaignAdapterManifest>(Full(root, relative), "review-campaign-adapter-invalid");
    }

    private static void ValidateAdapter(string root, CampaignAdapterManifest adapter, string relative, string provider)
    {
        Ensure(AllPresent(adapter.SchemaVersion, adapter.AdapterId, adapter.Provider, adapter.Protocol,
            adapter.Command, adapter.CommandArtifact, adapter.CommandArtifactSha256,
            adapter.Capabilities), "review-campaign-adapter-fields-missing");
        Ensure(adapter.SchemaVersion == "1.0", "review-campaign-adapter-schema-invalid");
        Ensure(Regex.IsMatch(adapter.AdapterId, "^[a-z0-9][a-z0-9._-]{0,127}$", RegexOptions.CultureInvariant),
            "review-campaign-adapter-id-invalid");
        Ensure(adapter.Provider.Length is > 0 and <= 128 & !adapter.Provider.Any(char.IsWhiteSpace),
            "review-campaign-adapter-provider-invalid");
        Ensure(adapter.Provider == provider, "review-campaign-adapter-provider-mismatch");
        Ensure(adapter.Protocol == Protocol, "review-campaign-adapter-protocol-invalid");
        Ensure(adapter.Command.Length > 0
            & adapter.Command.All(value => !string.IsNullOrWhiteSpace(value)
                & value.IndexOfAny(['\0', '\r', '\n']) < 0), "review-campaign-adapter-command-invalid");
        Ensure(!adapter.Command.Any(InlineEvaluationFlags.Contains), "review-campaign-adapter-shell-command-forbidden");
        var artifact = GovernedPath(root, adapter.CommandArtifact, ".engloop/provider-adapters/");
        Ensure(IsTracked(root, artifact), "review-campaign-adapter-artifact-must-be-tracked");
        Ensure(IsTrackedAtHead(root, artifact), "review-campaign-adapter-artifact-must-match-head");
        Ensure(IsHash(adapter.CommandArtifactSha256)
            & FileSha256(Full(root, artifact)) == adapter.CommandArtifactSha256,
            "review-campaign-adapter-artifact-hash-mismatch");
        Ensure(adapter.Command.Count(value => value == artifact) == 1
            & relative.EndsWith(".json", StringComparison.Ordinal), "review-campaign-adapter-command-artifact-invalid");
        Ensure(adapter.TimeoutSeconds is >= 1 and <= 300, "review-campaign-adapter-timeout-invalid");
        Ensure(adapter.Capabilities.Length > 0, "review-campaign-adapter-capabilities-empty");
        Ensure(adapter.Capabilities.Distinct(StringComparer.Ordinal).Count() == adapter.Capabilities.Length,
            "review-campaign-adapter-capabilities-duplicate");
        Ensure(adapter.Capabilities.All(AdapterCapabilities.Contains), "review-campaign-adapter-capabilities-invalid");
        Ensure(adapter.Capabilities.Contains("discover", StringComparer.Ordinal),
            "review-campaign-adapter-discovery-capability-missing");
        Ensure(adapter.Capabilities.Contains("inspect", StringComparer.Ordinal),
            "review-campaign-adapter-inspection-capability-missing");
        Ensure(adapter.DiscoveryRequest.ValueKind == JsonValueKind.Object,
            "review-campaign-adapter-discovery-request-invalid");
    }

    private static void ValidateDiscovery(DiscoveryResult result, string provider, string repository, string pullRequest)
    {
        var threads = result.Threads ?? throw new InvalidOperationException("review-campaign-provider-discovery-invalid");
        Ensure(result.SchemaVersion == "1.0" & result.Status is "success" or "rejected"
            & result.Provider == provider & result.Repository == repository & result.PullRequest == pullRequest
            & !string.IsNullOrWhiteSpace(result.ActingPrincipal) & IsRevision(result.SourceRevision)
            & IsRevision(result.TargetRevision) & IsIdentity(result.Iteration) & IsRevision(result.ProviderHead)
            & result.PageCount is >= 1 & result.PaginationComplete is true & result.ContinuationToken == string.Empty
            & result.ThreadCount is >= 0 & result.ThreadCount == threads.Length
            & !string.IsNullOrWhiteSpace(result.DiscoveryReceiptId) & result.MutationPerformed is false,
            "review-campaign-provider-discovery-invalid");
        Ensure(threads.Select(thread => thread.Thread).Distinct(StringComparer.Ordinal).Count() == threads.Length,
            "review-campaign-thread-duplicate");
        foreach (var thread in threads)
        {
            Ensure(IsIdentity(thread.Thread) & IsIdentity(thread.RootComment) & ThreadStatuses.Contains(thread.Status)
                & !string.IsNullOrWhiteSpace(thread.Summary) & !string.IsNullOrWhiteSpace(thread.UpdatedAt)
                & thread.Comments is { Length: > 0 } & thread.ProviderState.ValueKind == JsonValueKind.Object
                & thread.Comments.Any(comment => comment.Comment == thread.RootComment), "review-campaign-thread-invalid");
            Ensure(thread.Comments.Select(comment => comment.Comment).Distinct(StringComparer.Ordinal).Count() == thread.Comments.Length,
                "review-campaign-comment-duplicate");
            foreach (var comment in thread.Comments)
                Ensure(IsIdentity(comment.Comment) & !string.IsNullOrWhiteSpace(comment.Author)
                    & !string.IsNullOrWhiteSpace(comment.CreatedAt) & !string.IsNullOrWhiteSpace(comment.UpdatedAt)
                    & comment.IsDeleted is not null, "review-campaign-comment-invalid");
        }
    }

    private static bool TryParseDiscovery(string value, out DiscoveryResult? result)
    {
        try { result = JsonSerializer.Deserialize<DiscoveryResult>(value, JsonOptions); return result is not null; }
        catch (JsonException) { result = null; return false; }
    }

    private static (int ExitCode, string Output, string Error) InvokeAdapter(string root, CampaignAdapterManifest adapter,
        string sessionHash, string input)
    {
        var command = adapter.Command.ToArray();
        var artifact = Full(root, adapter.CommandArtifact);
        var runDirectory = Full(root, $".engloop/out/code-review-response/adapter-runs/{sessionHash}");
        Directory.CreateDirectory(runDirectory);
        EnsureNoReparsePoints(root, runDirectory);
        var artifactName = Path.GetFileName(adapter.CommandArtifact);
        var runArtifact = Path.Combine(runDirectory,
            Path.GetFileNameWithoutExtension(artifactName) + "." + Guid.NewGuid().ToString("N") + Path.GetExtension(artifactName));
        using (var source = new FileStream(artifact, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var destination = new FileStream(runArtifact, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            source.CopyTo(destination);
            destination.Flush(flushToDisk: true);
        }
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(runArtifact, File.GetUnixFileMode(artifact));
        Ensure(FileSha256(runArtifact) == adapter.CommandArtifactSha256, "review-campaign-adapter-artifact-changed");
        var index = Array.FindIndex(command, value => value == adapter.CommandArtifact);
        Ensure(index >= 0, "review-campaign-adapter-command-invalid");
        command[index] = runArtifact;
        var start = new ProcessStartInfo(command[0])
        {
            WorkingDirectory = root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        foreach (var argument in command.Skip(1)) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("review-campaign-adapter-start-failed");
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(adapter.TimeoutSeconds * 1000))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (!process.WaitForExit(5000)) throw new InvalidOperationException("review-campaign-adapter-timeout-kill-failed");
            Ensure(Task.WaitAll([outputTask, errorTask], 5000), "review-campaign-adapter-timeout-drain-failed");
            return (-1, outputTask.Result.Trim(), "adapter-timeout:" + errorTask.Result.Trim());
        }
        return (process.ExitCode, outputTask.GetAwaiter().GetResult().Trim(), errorTask.GetAwaiter().GetResult().Trim());
    }

    private static bool IsEntryValidationCommand(string tool, string command)
        => (tool.Equals("run_in_terminal", StringComparison.OrdinalIgnoreCase) | tool.Equals("execute", StringComparison.OrdinalIgnoreCase))
            & command == $"dotnet tool run engloopkit validate agent-entry --stage {StageId} --root .";

    private static string TrackerRelative(string campaignId)
        => $".engloop/out/code-review-response/campaigns/{campaignId}/tracker.json";

    private static string CampaignLockPath(string root, string campaignId)
        => Full(root, $".engloop/out/code-review-response/campaign-locks/{campaignId}.lock");

    private static void EnsureNoProviderClaims(string root, string campaignId)
    {
        var providerClaims = Full(root,
            $".engloop/out/code-review-response/provider-claims/{campaignId}");
        Ensure(!Directory.Exists(providerClaims)
            || Directory.GetFiles(providerClaims, "*.json", SearchOption.TopDirectoryOnly).Length == 0,
            "review-campaign-provider-operation-in-progress");
    }

    private static string CampaignTransactionRelative(string campaignId, string sessionHash)
        => $".engloop/out/code-review-response/campaign-transactions/{campaignId}.{sessionHash}.json";

    private static string[] PendingTransactionsForCampaign(string root, string campaignId)
    {
        var directory = Full(root, ".engloop/out/code-review-response/campaign-transactions");
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, campaignId + ".*.json", SearchOption.TopDirectoryOnly)
            : [];
    }

    private static string[] PendingTransactionsForSession(string root, string sessionHash)
    {
        var directory = Full(root, ".engloop/out/code-review-response/campaign-transactions");
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*." + sessionHash + ".json", SearchOption.TopDirectoryOnly)
            : [];
    }

    private static void CommitCampaignTransaction(string root, CampaignTransaction transaction)
    {
        Ensure(PendingTransactionsForCampaign(root, transaction.CampaignId).Length == 0,
            "review-campaign-transaction-in-progress");
        var relative = CampaignTransactionRelative(transaction.CampaignId,
            transaction.SessionHash);
        var path = Full(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteJsonCreateNew(path, transaction, "review-campaign-transaction-in-progress");
        ApplyCampaignTransaction(root, relative, transaction);
    }

    private static void RecoverCampaignTransaction(string root, string campaignId)
    {
        var pending = PendingTransactionsForCampaign(root, campaignId);
        Ensure(pending.Length <= 1, "review-campaign-transaction-ambiguous");
        if (pending.Length == 0) return;
        var relative = Path.GetRelativePath(root, pending[0]).Replace('\\', '/');
        var transaction = Read<CampaignTransaction>(pending[0],
            "review-campaign-transaction-invalid");
        ApplyCampaignTransaction(root, relative, transaction);
    }

    private static void ApplyCampaignTransaction(string root, string relative,
        CampaignTransaction transaction)
    {
        Ensure(transaction.SchemaVersion == "1.0"
            & transaction.Kind is "initialize" or "post-tool"
            & IsHash(transaction.CampaignId) & IsHash(transaction.SessionHash)
            & (transaction.PreviousTrackerSha256.Length == 0
                | IsHash(transaction.PreviousTrackerSha256))
            & relative == CampaignTransactionRelative(transaction.CampaignId,
                transaction.SessionHash), "review-campaign-transaction-invalid");
        var trackerRelative = NormalizeTrackerPath(root, transaction.TrackerPath);
        Ensure(trackerRelative == TrackerRelative(transaction.CampaignId)
            & transaction.Tracker.CampaignId == transaction.CampaignId,
            "review-campaign-transaction-invalid");
        ValidateTracker(root, transaction.Tracker, trackerRelative,
            requireSnapshotFile: transaction.Kind != "initialize");
        var gateRelative = GovernedPath(root, transaction.GatePath,
            ".engloop/out/code-review-response/campaign-gates/");
        Ensure(gateRelative == Path.GetRelativePath(root,
            GatePath(root, transaction.SessionHash)).Replace('\\', '/'),
            "review-campaign-transaction-invalid");
        ValidateGateStatic(root, transaction.Gate);
        Ensure(transaction.Gate.SessionHash == transaction.SessionHash
            & transaction.Gate.CampaignId == transaction.CampaignId
            & transaction.Gate.Tracker == trackerRelative
            & transaction.Gate.TrackerSha256 == transaction.Tracker.TrackerSha256
            & transaction.Gate.RefreshNumber == transaction.Tracker.RefreshNumber
            & transaction.Gate.Provider == transaction.Tracker.Provider
            & transaction.Gate.Repository == transaction.Tracker.Repository
            & transaction.Gate.PullRequest == transaction.Tracker.PullRequest
            & transaction.Gate.Adapter == transaction.Tracker.Adapter
            & transaction.Gate.AdapterSha256 == transaction.Tracker.AdapterSha256
            & transaction.Gate.AdapterArtifactSha256
                == transaction.Tracker.AdapterArtifactSha256,
            "review-campaign-transaction-invalid");

        if (transaction.Kind == "initialize")
        {
            Ensure(transaction.SnapshotJson.Length > 0
                & Sha256Text(transaction.SnapshotJson)
                    == transaction.Tracker.SnapshotSha256,
                "review-campaign-transaction-snapshot-invalid");
            DiscoveryResult discovery;
            try { discovery = JsonSerializer.Deserialize<DiscoveryResult>(
                transaction.SnapshotJson, JsonOptions)
                ?? throw new InvalidOperationException(
                    "review-campaign-transaction-snapshot-invalid"); }
            catch (JsonException) { throw new InvalidOperationException(
                "review-campaign-transaction-snapshot-invalid"); }
            ValidateDiscovery(discovery, transaction.Tracker.Provider,
                transaction.Tracker.Repository, transaction.Tracker.PullRequest);
            Ensure(discovery.Status == "success"
                & transaction.Tracker.Snapshot == SnapshotRelative(
                    transaction.CampaignId, transaction.Tracker.RefreshNumber),
                "review-campaign-transaction-snapshot-invalid");
        }
        else
        {
            Ensure(transaction.SnapshotJson.Length == 0
                & IsHash(transaction.PreviousTrackerSha256),
                "review-campaign-transaction-invalid");
        }

        var trackerPath = Full(root, trackerRelative);
        if (File.Exists(trackerPath))
        {
            var current = ReadTracker(root, trackerRelative);
            Ensure(current.TrackerSha256 == transaction.PreviousTrackerSha256
                | current.TrackerSha256 == transaction.Tracker.TrackerSha256,
                "review-campaign-transaction-tracker-conflict");
        }
        else
        {
            Ensure(transaction.PreviousTrackerSha256.Length == 0,
                "review-campaign-transaction-tracker-missing");
        }

        var gatePath = Full(root, gateRelative);
        if (File.Exists(gatePath))
        {
            var currentGate = Read<CampaignGate>(gatePath,
                "review-campaign-gate-missing-or-invalid");
            ValidateGateStatic(root, currentGate);
            Ensure(currentGate.SessionHash == transaction.SessionHash
                & currentGate.CampaignId == transaction.CampaignId
                & currentGate.Tracker == trackerRelative
                & (currentGate.TrackerSha256 == transaction.PreviousTrackerSha256
                    | currentGate.TrackerSha256
                        == transaction.Tracker.TrackerSha256),
                "review-campaign-transaction-gate-conflict");
        }
        else
        {
            Ensure(transaction.Kind == "initialize",
                "review-campaign-transaction-gate-missing");
        }

        if (transaction.Kind == "initialize")
        {
            var snapshotPath = Full(root, transaction.Tracker.Snapshot);
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            WriteTextCreateNewOrExact(snapshotPath, transaction.SnapshotJson,
                "review-campaign-snapshot-conflict");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(trackerPath)!);
        WriteJson(trackerPath, transaction.Tracker);
        Directory.CreateDirectory(Path.GetDirectoryName(gatePath)!);
        WriteJson(gatePath, transaction.Gate);
        _ = ReadGate(root, gatePath);
        File.Delete(Full(root, relative));
    }

    private static string CampaignIdFromTrackerPath(string tracker)
    {
        var match = Regex.Match(tracker,
            "^\\.engloop/out/code-review-response/campaigns/(?<id>[a-f0-9]{64})/tracker\\.json$",
            RegexOptions.CultureInvariant);
        Ensure(match.Success, "review-campaign-tracker-path-invalid");
        return match.Groups["id"].Value;
    }

    private static string CampaignIdForHook(string root, string action, string sessionHash,
        string gatePath, string prompt)
    {
        if (action == "initialize")
        {
            var provider = Identity(Argument(prompt, "--provider"), "provider");
            var repository = Identity(Argument(prompt, "--repository"), "repository");
            var pullRequest = Identity(Argument(prompt, "--pr"), "pr");
            return Sha256Text(provider + "\n" + repository + "\n" + pullRequest);
        }

        if (File.Exists(gatePath))
        {
            var gate = Read<CampaignGate>(gatePath, "review-campaign-gate-missing-or-invalid");
            Ensure(gate.SchemaVersion == "1.0" & IsHash(gate.CampaignId)
                & gate.Tracker == TrackerRelative(gate.CampaignId), "review-campaign-gate-invalid");
            return gate.CampaignId;
        }

        var completionPath = CampaignCompletionPath(root, sessionHash);
        if (File.Exists(completionPath))
        {
            var completion = Read<CampaignCompletionReceipt>(completionPath,
                "review-campaign-completion-receipt-missing-or-invalid");
            Ensure(completion.SchemaVersion == "1.0"
                & completion.SessionHash == sessionHash
                & IsHash(completion.CampaignId),
                "review-campaign-completion-receipt-invalid");
            return completion.CampaignId;
        }

        var pending = PendingTransactionsForSession(root, sessionHash);
        Ensure(pending.Length == 1, "review-campaign-gate-missing-or-invalid");
        var transaction = Read<CampaignTransaction>(pending[0],
            "review-campaign-transaction-invalid");
        Ensure(transaction.SchemaVersion == "1.0" & transaction.SessionHash == sessionHash
            & IsHash(transaction.CampaignId), "review-campaign-transaction-invalid");
        return transaction.CampaignId;
    }

    private static string SnapshotRelative(string campaignId, int refresh)
        => $".engloop/out/code-review-response/campaign-snapshots/{campaignId}/{refresh:D6}.json";

    private static string GatePath(string root, string sessionHash)
        => Full(root, $".engloop/out/code-review-response/campaign-gates/{sessionHash}.json");

    private static string CampaignCompletionRelative(string sessionHash)
        => $".engloop/out/code-review-response/campaign-completions/{sessionHash}.json";

    private static string CampaignCompletionPath(string root, string sessionHash)
        => Full(root, CampaignCompletionRelative(sessionHash));

    private static string ReceiptRelative(string tracker, string trackerSha256)
        => $".engloop/out/code-review-response/campaign-receipts/{Sha256Text(tracker + "\n" + trackerSha256)}.json";

    private static string Argument(string prompt, string name)
    {
        var match = Regex.Match(prompt, "(?:^|\\s)" + Regex.Escape(name) + "(?:=|\\s+)(?:\\\"(?<dq>[^\\\"]+)\\\"|'(?<sq>[^']+)'|(?<bare>[^\\s]+))");
        Ensure(match.Success, "review-campaign-option-missing:" + name);
        return match.Groups["dq"].Value + match.Groups["sq"].Value + match.Groups["bare"].Value;
    }

    private static string Identity(string value, string name)
    {
        Ensure(IsIdentity(value), "review-campaign-" + name + "-invalid");
        return value;
    }

    private static bool IsIdentity(string value) => value.Length is > 0 and <= 512 & !value.Any(char.IsWhiteSpace);
    private static bool IsRevision(string value) => Regex.IsMatch(value, "^[a-fA-F0-9]{40,64}$", RegexOptions.CultureInvariant);
    private static bool IsHash(string value) => Regex.IsMatch(value, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant);
    private static string Short(string value) => value.Length >= 12 ? value[..12] : value;

    private static string GovernedPath(string root, string value, string prefix)
    {
        var normalized = value.Replace('\\', '/');
        Ensure(normalized.Length > prefix.Length & !Path.IsPathRooted(normalized) & !normalized.Contains('\0'), "review-campaign-path-invalid");
        var full = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefixFull = Path.GetFullPath(Path.Combine(root, prefix.Replace('/', Path.DirectorySeparatorChar))).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Ensure(full.StartsWith(prefixFull, PathComparison), "review-campaign-path-invalid");
        var canonical = Path.GetRelativePath(root, full).Replace('\\', '/');
        Ensure(canonical.Equals(normalized, PathComparison), "review-campaign-path-invalid");
        EnsureNoReparsePoints(root, full);
        return canonical;
    }

    private static string ExactGitRoot(string cwd)
    {
        Ensure(cwd.Length > 0, "review-campaign-cwd-missing");
        var selected = Path.GetFullPath(cwd).TrimEnd(Path.DirectorySeparatorChar);
        var result = Git(selected, "rev-parse", "--show-toplevel");
        Ensure(result.ExitCode == 0, "review-campaign-git-root-unavailable");
        var root = Path.GetFullPath(result.Output.Trim()).TrimEnd(Path.DirectorySeparatorChar);
        Ensure(root.Equals(selected, PathComparison), "review-campaign-cwd-not-exact-root");
        EnsureNoReparsePoints(root, Full(root, ".engloop/out/code-review-response"));
        Ensure(Git(root, "check-ignore", "-q", "--no-index", "--", ".engloop/out/code-review-response/.probe").ExitCode == 0,
            "review-campaign-output-not-ignored");
        return root;
    }

    private static void RequireAgentEntry(string root)
    {
        var result = ValidationCommands.EvaluateAgentEntry(["--stage", StageId, "--root", root]);
        Ensure(result.Passed, "review-campaign-agent-entry-rejected:" + result.Reason);
    }

    private static string GitHead(string root)
    {
        var result = Git(root, "rev-parse", "HEAD");
        Ensure(result.ExitCode == 0 & IsRevision(result.Output.Trim()), "review-campaign-git-head-unavailable");
        return result.Output.Trim().ToLowerInvariant();
    }

    private static string StatusDigest(string root)
    {
        var result = Git(root, "status", "--porcelain=v1", "--untracked-files=all", "--", ":(exclude).engloop/out");
        Ensure(result.ExitCode == 0, "review-campaign-status-unavailable");
        return Sha256Text(result.Output.Replace("\r\n", "\n").TrimEnd('\n'));
    }

    private static bool IsTracked(string root, string relative)
        => Git(root, "ls-files", "--error-unmatch", "--", relative).ExitCode == 0;

    private static bool IsTrackedAtHead(string root, string relative)
    {
        var canonical = relative.Replace('\\', '/');
        var current = Git(root, "hash-object", "--path=" + canonical, "--", canonical);
        var committed = Git(root, "rev-parse", "HEAD:" + canonical);
        return (current.ExitCode == 0) & (committed.ExitCode == 0)
            & current.Output.Trim().Equals(committed.Output.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static (int ExitCode, string Output) Git(string root, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("review-campaign-git-start-failed");
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static void EnsureNoReparsePoints(string root, string full)
    {
        var current = root;
        foreach (var segment in Path.GetRelativePath(root, full).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            Ensure((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "review-campaign-path-reparse-forbidden");
        }
    }

    private static T Read<T>(string path, string error)
    {
        Ensure(File.Exists(path), error);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new InvalidOperationException(error);
        }
        catch (JsonException) { throw new InvalidOperationException(error); }
        catch (IOException) { throw new InvalidOperationException(error); }
        catch (UnauthorizedAccessException) { throw new InvalidOperationException(error); }
    }

    private static void WriteJson<T>(string path, T value) => WriteJsonAtomic(path, value, true, string.Empty);
    private static void WriteJsonCreateNew<T>(string path, T value, string existsError) => WriteJsonAtomic(path, value, false, existsError);

    private static void WriteJsonCreateNewOrExact<T>(string path, T value, string existsError)
    {
        if (!File.Exists(path))
        {
            try
            {
                WriteJsonCreateNew(path, value, existsError);
                return;
            }
            catch (InvalidOperationException) when (File.Exists(path)) { }
        }
        var existing = Read<T>(path, existsError);
        Ensure(JsonSerializer.Serialize(existing, JsonOptions) == JsonSerializer.Serialize(value, JsonOptions), existsError);
    }

    private static void WriteJsonAtomic<T>(string path, T value, bool overwrite, string existsError)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, JsonOptions);
                stream.Flush(true);
            }
            try { File.Move(temporary, path, overwrite); }
            catch (IOException) when (!overwrite && File.Exists(path)) { throw new InvalidOperationException(existsError); }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void WriteTextCreateNewOrExact(string path, string value, string error)
    {
        if (File.Exists(path))
        {
            Ensure(File.ReadAllText(path) == value, error);
            return;
        }
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(value);
        writer.Flush();
        stream.Flush(true);
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
        throw new InvalidOperationException("review-campaign-hook-tool-json-missing");
    }

    private static string ToolCommand(JsonElement input)
    {
        foreach (var name in new[] { "tool_input", "toolInput" })
            if (input.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object)
                return ReadString(value, "command", "commandLine");
        return string.Empty;
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString()!;
        return string.Empty;
    }

    private static bool HasExactProperties(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        var actual = value.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        return actual.SequenceEqual(expected.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private static string Full(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    private static string FileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Sha256Text(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void WriteDecision(bool allow, string reason)
        => Console.WriteLine(JsonSerializer.Serialize(new { @continue = true, stopReason = (string?)null, hookSpecificOutput = new { hookEventName = "PreToolUse", permissionDecision = allow ? "allow" : "deny", permissionDecisionReason = reason } }));

    private static void WriteHook(bool continueValue, string reason = "", string systemMessage = "")
        => Console.WriteLine(JsonSerializer.Serialize(new { @continue = continueValue, stopReason = reason.Length == 0 ? null : reason, systemMessage = systemMessage.Length == 0 ? null : systemMessage }));

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool AllPresent(params object?[] values) => values.All(value => value is not null);
}
