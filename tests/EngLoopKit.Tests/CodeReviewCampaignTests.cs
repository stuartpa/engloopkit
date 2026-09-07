using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

[Collection("OperationsHookConsole")]
public sealed class CodeReviewCampaignTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "elk-review-campaign-" + Guid.NewGuid().ToString("N"));

    public CodeReviewCampaignTests() => Directory.CreateDirectory(_work);

    [Fact]
    public void CampaignDiscoversEveryThreadWithoutSourceOrProviderMutationAndRequiresExplicitSelection()
    {
        var repo = CreateRepository("three");
        const string session = "discover-three";
        var beforeHead = GitOutput(repo, "rev-parse", "HEAD");
        var beforeStatus = GitRawOutput(repo, "status", "--porcelain=v1", "--untracked-files=all", "--", ":(exclude).engloop/out");

        var initialized = RunHook(repo, "initialize", CampaignPrompt(), session);

        Assert.True(Continues(initialized), initialized.Output + initialized.Error);
        Assert.Contains("CODE_REVIEW_CAMPAIGN_SCOPE_ACTIVE", initialized.Output);
        var trackerRelative = MarkerValue(initialized.Output, "tracker=");
        using var tracker = JsonDocument.Parse(File.ReadAllText(Full(repo, trackerRelative)));
        var root = tracker.RootElement;
        Assert.Equal("code-review-campaign", root.GetProperty("artifactType").GetString());
        Assert.Equal(2, root.GetProperty("pageCount").GetInt32());
        Assert.True(root.GetProperty("paginationComplete").GetBoolean());
        Assert.False(root.GetProperty("providerMutationPerformed").GetBoolean());
        Assert.Equal(string.Empty, root.GetProperty("selectedItemId").GetString());
        var items = root.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(3, items.Length);
        Assert.Equal(3, items.Select(item => item.GetProperty("itemId").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.All(items, item =>
        {
            Assert.Equal("active", item.GetProperty("status").GetString());
            Assert.Equal("current", item.GetProperty("observationState").GetString());
            Assert.Equal("pending", item.GetProperty("humanDecision").GetString());
            Assert.Equal("unassigned", item.GetProperty("priority").GetString());
        });
        Assert.Equal(beforeHead, GitOutput(repo, "rev-parse", "HEAD"));
        Assert.Equal(beforeStatus, GitRawOutput(repo, "status", "--porcelain=v1", "--untracked-files=all", "--", ":(exclude).engloop/out"));
        Assert.Single(ReadAdapterCalls(repo));
        Assert.Equal("discover", ReadAdapterCalls(repo)[0].GetProperty("phase").GetString());
        Assert.Equal("three", ReadAdapterCalls(repo)[0].GetProperty("discoveryRequest").GetProperty("behavior").GetString());
        Assert.Equal("three", ReadAdapterCalls(repo)[0].GetProperty("adapterObservedBehavior").GetString());
        AssertDecision(RunGuard(repo, session, "apply_patch", new { input = "*** Update File: src/fixture.cs" }), "deny");
        AssertDecision(RunGuard(repo, session, "provider_reply", new { }), "deny");
        AssertDecision(RunGuard(repo, session, "read_file", new { filePath = trackerRelative }), "allow");

        var stopped = RunHook(repo, "stop", string.Empty, session);
        Assert.False(Continues(stopped));
        Assert.Contains("review-campaign-selection-required", stopped.Output);
    }

    [Fact]
    public void CampaignBindsExactSelectionClassificationAndPriorityFromHostAnswers()
    {
        var repo = CreateRepository("three");
        const string session = "triage";
        var initialized = RunHook(repo, "initialize", CampaignPrompt(), session);
        var trackerRelative = MarkerValue(initialized.Output, "tracker=");
        var selection = SelectionQuestion(repo, trackerRelative);
        AssertDecision(RunGuard(repo, session, "vscode_askQuestions", selection), "allow");
        var itemId = TrackerItems(repo, trackerRelative)[1].GetProperty("itemId").GetString()!;

        var selected = RunPostTool(repo, session, selection,
            new { answers = new Dictionary<string, object> { [SelectionQuestionText(repo, trackerRelative)] = new { selected = new[] { itemId } } } },
            "select-tool-use");
        Assert.True(Continues(selected), selected.Output + selected.Error);
        var triage = TriageQuestion(repo, trackerRelative, "accepted-actionable");
        AssertDecision(RunGuard(repo, session, "vscode_askQuestions", triage), "allow");
        var triaged = RunPostTool(repo, session, triage, new
        {
            answers = new Dictionary<string, object>
            {
                [$"Classify item {itemId[..12]}."] = new { selected = new[] { "accepted-actionable" } },
                [$"Prioritize item {itemId[..12]}."] = new { selected = new[] { "urgent" } },
            },
        }, "triage-tool-use");

        Assert.True(Continues(triaged), triaged.Output + triaged.Error);
        using var tracker = JsonDocument.Parse(File.ReadAllText(Full(repo, trackerRelative)));
        var item = tracker.RootElement.GetProperty("items").EnumerateArray()
            .Single(value => value.GetProperty("itemId").GetString() == itemId);
        Assert.Equal(itemId, tracker.RootElement.GetProperty("selectedItemId").GetString());
        Assert.Equal("accepted-actionable", item.GetProperty("proposedClassification").GetString());
        Assert.Equal("accepted-actionable", item.GetProperty("humanDecision").GetString());
        Assert.Equal("urgent", item.GetProperty("priority").GetString());
        Assert.False(item.GetProperty("decisionStale").GetBoolean());
        Assert.Equal("triage-tool-use", item.GetProperty("decisionToolUseId").GetString());
        Assert.Matches("^[a-f0-9]{32}$", item.GetProperty("decisionReceiptId").GetString());
        Assert.Matches("^[a-f0-9]{64}$", item.GetProperty("decisionQuestionSha256").GetString());
        Assert.Matches("^[a-f0-9]{64}$", item.GetProperty("decisionResponseSha256").GetString());
        Assert.Equal("pending", item.GetProperty("sourceState").GetString());
    }

    [Fact]
    public void CampaignRefreshPreservesStableDecisionsAndMarksChangedOrMissingItems()
    {
        var repo = CreateRepository("three");
        const string firstSession = "refresh-first";
        var first = RunHook(repo, "initialize", CampaignPrompt(), firstSession);
        var trackerRelative = MarkerValue(first.Output, "tracker=");
        var selection = SelectionQuestion(repo, trackerRelative);
        var itemId = TrackerItems(repo, trackerRelative)[0].GetProperty("itemId").GetString()!;
        Assert.True(Continues(RunPostTool(repo, firstSession, selection,
            new { answers = new Dictionary<string, object> { [SelectionQuestionText(repo, trackerRelative)] = new { selected = new[] { itemId } } } }, "select")));
        var triage = TriageQuestion(repo, trackerRelative, "disputed");
        Assert.True(Continues(RunPostTool(repo, firstSession, triage, new
        {
            answers = new Dictionary<string, object>
            {
                [$"Classify item {itemId[..12]}."] = new { selected = new[] { "disputed" } },
                [$"Prioritize item {itemId[..12]}."] = new { selected = new[] { "high" } },
            },
        }, "triage")));
        Assert.True(Continues(RunHook(repo, "stop", string.Empty, firstSession)));

        SetAdapterBehavior(repo, "refresh");
        var refreshed = RunHook(repo, "initialize", CampaignPrompt(), "refresh-second");

        Assert.True(Continues(refreshed), refreshed.Output + refreshed.Error);
        using var tracker = JsonDocument.Parse(File.ReadAllText(Full(repo, trackerRelative)));
        Assert.Equal(2, tracker.RootElement.GetProperty("refreshNumber").GetInt32());
        var items = tracker.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(4, items.Length);
        var changed = items.Single(value => value.GetProperty("itemId").GetString() == itemId);
        Assert.Equal("disputed", changed.GetProperty("humanDecision").GetString());
        Assert.Equal("high", changed.GetProperty("priority").GetString());
        Assert.True(changed.GetProperty("decisionStale").GetBoolean());
        Assert.Contains(items, value => value.GetProperty("status").GetString() == "resolved");
        Assert.Contains(items, value => value.GetProperty("observationState").GetString() == "not-returned");
        Assert.Contains(items, value => value.GetProperty("thread").GetString() == "thread-4"
            && value.GetProperty("humanDecision").GetString() == "pending");
    }

    [Fact]
    public void CampaignRefreshRequiresTheTrustedReceiptForThePreviousTracker()
    {
        var repo = CreateRepository("three");
        const string session = "receipt-first";
        var initialized = RunHook(repo, "initialize", CampaignPrompt(), session);
        var trackerRelative = MarkerValue(initialized.Output, "tracker=");
        var selection = SelectionQuestion(repo, trackerRelative);
        var itemId = TrackerItems(repo, trackerRelative)[0].GetProperty("itemId").GetString()!;
        Assert.True(Continues(RunPostTool(repo, session, selection,
            new { answers = new Dictionary<string, object> { [SelectionQuestionText(repo, trackerRelative)] = new { selected = new[] { itemId } } } }, "select")));
        var triage = TriageQuestion(repo, trackerRelative, "already-addressed");
        Assert.True(Continues(RunPostTool(repo, session, triage, new
        {
            answers = new Dictionary<string, object>
            {
                [$"Classify item {itemId[..12]}."] = new { selected = new[] { "already-addressed" } },
                [$"Prioritize item {itemId[..12]}."] = new { selected = new[] { "normal" } },
            },
        }, "triage")));
        var stopped = RunHook(repo, "stop", string.Empty, session);
        Assert.True(Continues(stopped), stopped.Output + stopped.Error);
        var receipt = MarkerValue(stopped.Output, "receipt=");
        File.Delete(Full(repo, receipt));

        var refresh = RunHook(repo, "initialize", CampaignPrompt(), "receipt-second");

        Assert.False(Continues(refresh));
        Assert.Contains("review-campaign-receipt-missing-or-invalid", refresh.Output);
        Assert.Single(ReadAdapterCalls(repo));
    }

    [Fact]
    public void CampaignInitializationRecoversWriteAheadStateWithoutRediscovery()
    {
        var repo = CreateRepository("three");
        const string session = "initialize-crash";
        var initialized = RunHook(repo, "initialize", CampaignPrompt(), session);
        Assert.True(Continues(initialized), initialized.Output + initialized.Error);
        var trackerRelative = MarkerValue(initialized.Output, "tracker=");
        var trackerPath = Full(repo, trackerRelative);
        var gateRelative = CampaignGateRelative(session);
        var gatePath = Full(repo, gateRelative);
        var trackerText = File.ReadAllText(trackerPath);
        var gateText = File.ReadAllText(gatePath);
        using var tracker = JsonDocument.Parse(trackerText);
        var campaignId = tracker.RootElement.GetProperty("campaignId").GetString()!;
        var snapshotRelative = tracker.RootElement.GetProperty("snapshot").GetString()!;
        var snapshotPath = Full(repo, snapshotRelative);
        var snapshotText = File.ReadAllText(snapshotPath);
        WriteCampaignTransaction(repo, "initialize", campaignId, session, string.Empty,
            trackerRelative, trackerText, gateRelative, gateText, snapshotText);
        File.Delete(gatePath);
        File.Delete(trackerPath);
        File.Delete(snapshotPath);

        var recovered = RunHook(repo, "initialize", CampaignPrompt(), session);

        Assert.True(Continues(recovered), recovered.Output + recovered.Error);
        Assert.Contains("CODE_REVIEW_CAMPAIGN_SCOPE_ACTIVE", recovered.Output);
        Assert.Equal(trackerText, File.ReadAllText(trackerPath));
        Assert.Equal(gateText, File.ReadAllText(gatePath));
        Assert.Equal(snapshotText, File.ReadAllText(snapshotPath));
        Assert.False(File.Exists(CampaignTransactionPath(repo, campaignId, session)));
        Assert.Single(ReadAdapterCalls(repo));
    }

    [Fact]
    public void CampaignQuestionRecoversTrackerWrittenBeforeGateUpdate()
    {
        var repo = CreateRepository("three");
        const string session = "question-crash";
        var initialized = RunHook(repo, "initialize", CampaignPrompt(), session);
        var trackerRelative = MarkerValue(initialized.Output, "tracker=");
        var trackerPath = Full(repo, trackerRelative);
        var gateRelative = CampaignGateRelative(session);
        var gatePath = Full(repo, gateRelative);
        var oldTrackerText = File.ReadAllText(trackerPath);
        var oldGateText = File.ReadAllText(gatePath);
        using var oldTracker = JsonDocument.Parse(oldTrackerText);
        var oldTrackerSha = oldTracker.RootElement.GetProperty("trackerSha256").GetString()!;
        var campaignId = oldTracker.RootElement.GetProperty("campaignId").GetString()!;
        var selection = SelectionQuestion(repo, trackerRelative);
        var itemId = TrackerItems(repo, trackerRelative)[0].GetProperty("itemId").GetString()!;
        var selected = RunPostTool(repo, session, selection,
            new { answers = new Dictionary<string, object> { [SelectionQuestionText(repo, trackerRelative)] = new { selected = new[] { itemId } } } },
            "selection-crash-tool");
        Assert.True(Continues(selected), selected.Output + selected.Error);
        var newTrackerText = File.ReadAllText(trackerPath);
        var newGateText = File.ReadAllText(gatePath);
        WriteCampaignTransaction(repo, "post-tool", campaignId, session, oldTrackerSha,
            trackerRelative, newTrackerText, gateRelative, newGateText, string.Empty);
        File.WriteAllText(gatePath, oldGateText);

        var recovered = RunGuard(repo, session, "read_file", new { filePath = trackerRelative });

        AssertDecision(recovered, "allow");
        Assert.Equal(newTrackerText, File.ReadAllText(trackerPath));
        Assert.Equal(newGateText, File.ReadAllText(gatePath));
        Assert.False(File.Exists(CampaignTransactionPath(repo, campaignId, session)));
        using var recoveredTracker = JsonDocument.Parse(File.ReadAllText(trackerPath));
        Assert.Equal(itemId, recoveredTracker.RootElement.GetProperty("selectedItemId").GetString());
    }

    [Fact]
    public void CampaignStopReplaysItsDurableCompletionAfterGateCleanupAndRejectsTampering()
    {
        var repo = CreateRepository("three");
        const string session = "campaign-stop-replay";
        var initialized = RunHook(repo, "initialize", CampaignPrompt(), session);
        var trackerRelative = MarkerValue(initialized.Output, "tracker=");
        var selection = SelectionQuestion(repo, trackerRelative);
        var itemId = TrackerItems(repo, trackerRelative)[0].GetProperty("itemId").GetString()!;
        Assert.True(Continues(RunPostTool(repo, session, selection,
            new { answers = new Dictionary<string, object> { [SelectionQuestionText(repo, trackerRelative)] = new { selected = new[] { itemId } } } },
            "stop-replay-select")));
        var triage = TriageQuestion(repo, trackerRelative, "already-addressed");
        Assert.True(Continues(RunPostTool(repo, session, triage, new
        {
            answers = new Dictionary<string, object>
            {
                [$"Classify item {itemId[..12]}."] = new { selected = new[] { "already-addressed" } },
                [$"Prioritize item {itemId[..12]}."] = new { selected = new[] { "normal" } },
            },
        }, "stop-replay-triage")));

        var completed = RunHook(repo, "stop", string.Empty, session);
        Assert.True(Continues(completed), completed.Output + completed.Error);
        Assert.Contains("CODE_REVIEW_CAMPAIGN_OK", completed.Output);
        Assert.Contains("alreadyCompleted=false", completed.Output);
        Assert.False(File.Exists(Full(repo, CampaignGateRelative(session))));
        var completionRelative = $".engloop/out/code-review-response/campaign-completions/{Sha256Text(session)}.json";
        var completionPath = Full(repo, completionRelative);
        Assert.True(File.Exists(completionPath));
        var original = File.ReadAllText(completionPath);

        var replay = RunHook(repo, "stop", string.Empty, session);
        Assert.True(Continues(replay), replay.Output + replay.Error);
        Assert.Contains("alreadyCompleted=true", replay.Output);
        Assert.Single(ReadAdapterCalls(repo));

        SetAdapterBehavior(repo, "refresh");
        var refreshed = RunHook(repo, "initialize", CampaignPrompt(), "later-refresh");
        Assert.True(Continues(refreshed), refreshed.Output + refreshed.Error);
        var historicalReplay = RunHook(repo, "stop", string.Empty, session);
        Assert.True(Continues(historicalReplay), historicalReplay.Output + historicalReplay.Error);
        Assert.Contains("alreadyCompleted=true", historicalReplay.Output);
        Assert.Equal(2, ReadAdapterCalls(repo).Length);

        var tampered = JsonNode.Parse(original)!.AsObject();
        tampered["campaignReceiptSha256"] = new string('0', 64);
        File.WriteAllText(completionPath, tampered.ToJsonString());
        var rejected = RunHook(repo, "stop", string.Empty, session);
        Assert.False(Continues(rejected));
        Assert.Contains("review-campaign-completion-receipt-changed", rejected.Output);
        Assert.Equal(2, ReadAdapterCalls(repo).Length);

        File.WriteAllText(completionPath, original);
        Assert.True(Continues(RunHook(repo, "stop", string.Empty, session)));
        Assert.Equal(2, ReadAdapterCalls(repo).Length);
    }

    [Theory]
    [InlineData("incomplete", "provider-discovery-invalid")]
    [InlineData("continuation", "provider-discovery-invalid")]
    [InlineData("wrong-count", "provider-discovery-invalid")]
    [InlineData("duplicate-thread", "thread-duplicate")]
    [InlineData("mutated", "provider-discovery-invalid")]
    [InlineData("wrong-provider", "provider-discovery-invalid")]
    [InlineData("no-receipt", "provider-discovery-invalid")]
    [InlineData("invalid-json", "provider-discovery-invalid")]
    [InlineData("exit-failure", "provider-discovery-exit-failed")]
    public void CampaignFailsClosedOnIncompleteOrMalformedProviderDiscovery(string behavior, string expected)
    {
        var repo = CreateRepository(behavior);

        var initialized = RunHook(repo, "initialize", CampaignPrompt(), "invalid-" + behavior);

        var calls = File.Exists(Path.Combine(repo, ".engloop", "out", "code-review-response", "campaign-adapter-calls.jsonl"))
            ? ReadAdapterCalls(repo)
            : [];
        Assert.False(Continues(initialized), initialized.Output + initialized.Error + " requests=" + JsonSerializer.Serialize(calls));
        Assert.Contains(expected, initialized.Output, StringComparison.OrdinalIgnoreCase);
        var campaigns = Path.Combine(repo, ".engloop", "out", "code-review-response", "campaigns");
        var snapshots = Path.Combine(repo, ".engloop", "out", "code-review-response", "campaign-snapshots");
        var gates = Path.Combine(repo, ".engloop", "out", "code-review-response", "campaign-gates");
        Assert.Empty(Directory.Exists(campaigns) ? Directory.GetFiles(campaigns, "*", SearchOption.AllDirectories) : []);
        Assert.Empty(Directory.Exists(snapshots) ? Directory.GetFiles(snapshots, "*", SearchOption.AllDirectories) : []);
        Assert.Empty(Directory.Exists(gates) ? Directory.GetFiles(gates, "*.json", SearchOption.AllDirectories) : []);
    }

    private string CreateRepository(string behavior)
    {
        var repo = Path.Combine(_work, "repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(repo, ".engloop", "provider-adapters"));
        Directory.CreateDirectory(Path.Combine(repo, ".config"));
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(Path.Combine(repo, ".gitignore"), ".engloop/out/\n");
        File.WriteAllText(Path.Combine(repo, "README.md"), "fixture\n");
        File.WriteAllText(Path.Combine(repo, "NORTHSTAR.md"), "# Direction\n");
        File.WriteAllText(Path.Combine(repo, "LEARNINGS.md"), "# Learnings\n");
        File.WriteAllText(Path.Combine(repo, "src", "fixture.cs"), "// fixture\n");
        File.WriteAllText(Path.Combine(repo, "src", "fixture.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        File.WriteAllText(Path.Combine(repo, ".engloop", "config.json"), "{\"schemaVersion\":\"2.0\",\"productId\":\"fixture\",\"artifactRoot\":\".engloop\",\"transientOutputRoot\":\".engloop/out\",\"northstarPath\":\"NORTHSTAR.md\",\"validatorCommand\":[\"dotnet\",\"--version\"],\"moduleDiscoveryCommand\":[\"dotnet\",\"--version\"],\"architectureCommand\":[\"dotnet\",\"--version\"],\"regressionCommand\":[\"dotnet\",\"--version\"],\"coverageInputs\":{\"wholeProduct\":\"src/fixture.csproj\"},\"testRunway\":{\"status\":\"proven\",\"framework\":\"xunit\",\"terseCommand\":[\"dotnet\",\"--version\"],\"boundaryTest\":\"Fixture.Boundary\",\"generatedDestination\":\"tests/generated\",\"evidenceDigest\":\"fixture\",\"provenAtRevision\":\"content:fixture\"},\"moduleInventory\":[{\"id\":\"core\",\"path\":\"src/fixture.csproj\"}]}\n");
        File.WriteAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "{\"version\":1,\"isRoot\":true,\"tools\":{\"engloopkit\":{\"version\":\"1.19.0\",\"commands\":[\"engloopkit\"]}}}\n");
        var script = Path.Combine(repo, ".engloop", "provider-adapters", "fake-campaign.ps1");
        File.WriteAllText(script, FakeAdapterScript);
        WriteAdapterManifest(repo, behavior);
        Git(repo, "init");
        Git(repo, "config", "user.email", "campaign@example.invalid");
        Git(repo, "config", "user.name", "Campaign Test");
        Git(repo, "add", ".");
        Git(repo, "commit", "-m", "fixture");
        return repo;
    }

    private static void SetAdapterBehavior(string repo, string behavior)
    {
        var path = Path.Combine(repo, ".engloop", "out", "code-review-response", "campaign-test-behavior.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, behavior);
    }

    private static void WriteAdapterManifest(string repo, string behavior)
    {
        var script = Path.Combine(repo, ".engloop", "provider-adapters", "fake-campaign.ps1");
        File.WriteAllText(Path.Combine(repo, ".engloop", "provider-adapters", "fake-campaign.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            adapterId = "fixture-campaign-adapter",
            provider = "fixture-provider",
            protocol = "engloop-review-response-v2",
            command = new[] { "pwsh", "-NoProfile", "-File", ".engloop/provider-adapters/fake-campaign.ps1" },
            commandArtifact = ".engloop/provider-adapters/fake-campaign.ps1",
            commandArtifactSha256 = FileHash(script),
            timeoutSeconds = 30,
            capabilities = new[] { "discover", "inspect", "reply", "resolve", "reply-and-resolve" },
            discoveryRequest = new { behavior },
        }));
    }

    private static string CampaignPrompt()
        => "--provider fixture-provider --repository fixture-repo --pr pr-1 --adapter .engloop/provider-adapters/fake-campaign.json";

    private static string CampaignGateRelative(string session)
        => $".engloop/out/code-review-response/campaign-gates/{Sha256Text(session)}.json";

    private static string CampaignTransactionPath(string repo, string campaignId, string session)
        => Full(repo, $".engloop/out/code-review-response/campaign-transactions/{campaignId}.{Sha256Text(session)}.json");

    private static void WriteCampaignTransaction(string repo, string kind, string campaignId,
        string session, string previousTrackerSha256, string trackerPath, string trackerJson,
        string gatePath, string gateJson, string snapshotJson)
    {
        var path = CampaignTransactionPath(repo, campaignId, session);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var tracker = JsonDocument.Parse(trackerJson);
        using var gate = JsonDocument.Parse(gateJson);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            kind,
            campaignId,
            sessionHash = Sha256Text(session),
            previousTrackerSha256,
            trackerPath,
            tracker = tracker.RootElement,
            gatePath,
            gate = gate.RootElement,
            snapshotJson,
        }));
    }

    private static object SelectionQuestion(string repo, string trackerRelative)
    {
        using var tracker = JsonDocument.Parse(File.ReadAllText(Full(repo, trackerRelative)));
        var root = tracker.RootElement;
        var items = root.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("status").GetString() == "active" && item.GetProperty("observationState").GetString() == "current")
            .OrderBy(item => item.GetProperty("itemId").GetString(), StringComparer.Ordinal)
            .ToArray();
        var projection = items.Select(item => new
        {
            itemId = item.GetProperty("itemId").GetString(),
            anchorPath = item.GetProperty("anchorPath").GetString(),
            line = item.GetProperty("line").ValueKind == JsonValueKind.Null ? (int?)null : item.GetProperty("line").GetInt32(),
            summary = item.GetProperty("summary").GetString(),
            humanDecision = item.GetProperty("humanDecision").GetString(),
            priority = item.GetProperty("priority").GetString(),
            decisionStale = item.GetProperty("decisionStale").GetBoolean(),
        });
        var message = "Select one exact current item; no item is selected automatically.\n\n```json\n"
            + JsonSerializer.Serialize(projection, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }) + "\n```";
        return new
        {
            questions = new[]
            {
                new
                {
                    header = "Select review thread",
                    question = SelectionQuestionText(repo, trackerRelative),
                    message,
                    multiSelect = false,
                    allowFreeformInput = false,
                    options = items.Select(item => new { label = item.GetProperty("itemId").GetString() }).ToArray(),
                },
            },
        };
    }

    private static string SelectionQuestionText(string repo, string trackerRelative)
    {
        using var tracker = JsonDocument.Parse(File.ReadAllText(Full(repo, trackerRelative)));
        return $"Select one active review thread from campaign {tracker.RootElement.GetProperty("campaignId").GetString()![..12]}.";
    }

    private static object TriageQuestion(string repo, string trackerRelative, string proposal)
    {
        using var tracker = JsonDocument.Parse(File.ReadAllText(Full(repo, trackerRelative)));
        var root = tracker.RootElement;
        var selectedId = root.GetProperty("selectedItemId").GetString()!;
        var item = root.GetProperty("items").EnumerateArray().Single(value => value.GetProperty("itemId").GetString() == selectedId);
        var projection = new
        {
            campaignId = root.GetProperty("campaignId").GetString(),
            itemId = selectedId,
            thread = item.GetProperty("thread").GetString(),
            rootComment = item.GetProperty("rootComment").GetString(),
            anchorPath = item.GetProperty("anchorPath").GetString(),
            line = item.GetProperty("line").ValueKind == JsonValueKind.Null ? (int?)null : item.GetProperty("line").GetInt32(),
            summary = item.GetProperty("summary").GetString(),
            comments = item.GetProperty("comments").Deserialize<object[]>(),
            proposedClassification = proposal,
            contentSha256 = item.GetProperty("contentSha256").GetString(),
        };
        var message = "Review the exact current provider item and the non-authoritative proposal before deciding.\n\n```json\n"
            + JsonSerializer.Serialize(projection, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }) + "\n```";
        return new
        {
            questions = new object[]
            {
                new
                {
                    header = "Classify review thread",
                    question = $"Classify item {selectedId[..12]}.",
                    message,
                    multiSelect = false,
                    allowFreeformInput = false,
                    options = new[] { "accepted-actionable", "already-addressed", "stale", "disputed", "clarification-required", "unsupported-out-of-scope" }.Select(label => new { label }).ToArray(),
                },
                new
                {
                    header = "Prioritize review thread",
                    question = $"Prioritize item {selectedId[..12]}.",
                    message = $"Set the author priority for campaign item {selectedId}.",
                    multiSelect = false,
                    allowFreeformInput = false,
                    options = new[] { "urgent", "high", "normal", "low" }.Select(label => new { label }).ToArray(),
                },
            },
        };
    }

    private static JsonElement[] TrackerItems(string repo, string tracker)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Full(repo, tracker)));
        return json.RootElement.GetProperty("items").EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private static (int ExitCode, string Output, string Error) RunGuard(string repo, string session, string toolName, object toolInput)
        => RunHookRaw(repo, ["guard"], JsonSerializer.Serialize(new { cwd = repo, session_id = session, tool_name = toolName, tool_input = toolInput }));

    private static (int ExitCode, string Output, string Error) RunPostTool(string repo, string session, object input, object response, string toolUseId)
        => RunHookRaw(repo, ["post-tool"], JsonSerializer.Serialize(new
        {
            cwd = repo,
            session_id = session,
            tool_name = "vscode_askQuestions",
            tool_input = input,
            tool_response = response,
            tool_use_id = toolUseId,
        }));

    private static (int ExitCode, string Output, string Error) RunHook(string repo, string action, string prompt, string session)
        => RunHookRaw(repo, [action], JsonSerializer.Serialize(new { cwd = repo, session_id = session, prompt }));

    private static (int ExitCode, string Output, string Error) RunHookRaw(string repo, string[] args, string input)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var reader = new StringReader(input);
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetIn(reader);
            Console.SetOut(output);
            Console.SetError(error);
            var exit = CodeReviewCampaignCommands.ExecuteHook(args);
            return (exit, output.ToString().Trim(), error.ToString().Trim());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static bool Continues((int ExitCode, string Output, string Error) result)
    {
        Assert.Equal(0, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        return json.RootElement.GetProperty("continue").GetBoolean();
    }

    private static void AssertDecision((int ExitCode, string Output, string Error) result, string expected)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal(expected, json.RootElement.GetProperty("hookSpecificOutput").GetProperty("permissionDecision").GetString());
    }

    private static string MarkerValue(string output, string marker)
    {
        using var json = JsonDocument.Parse(output);
        var message = json.RootElement.GetProperty("systemMessage").GetString()!;
        return message[(message.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private static JsonElement[] ReadAdapterCalls(string repo)
    {
        var path = Path.Combine(repo, ".engloop", "out", "code-review-response", "campaign-adapter-calls.jsonl");
        return File.ReadAllLines(path).Where(line => line.Length > 0)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
    }

    private static string Full(string repo, string relative) => Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar));
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Sha256Text(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string GitOutput(string repo, params string[] args) => GitRawOutput(repo, args).Trim();

    private static string GitRawOutput(string repo, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output + error);
        return output;
    }

    private static void Git(string repo, params string[] args) => _ = GitOutput(repo, args);

    public void Dispose()
    {
        if (!Directory.Exists(_work)) return;
        try { Directory.Delete(_work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private const string FakeAdapterScript = """
$ErrorActionPreference = 'Stop'
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$log = Join-Path (Get-Location) '.engloop/out/code-review-response/campaign-adapter-calls.jsonl'
New-Item -ItemType Directory -Force (Split-Path $log -Parent) | Out-Null
$behaviorProperty = $payload.discoveryRequest.PSObject.Properties['behavior']
$behavior = if ($null -eq $behaviorProperty) { '' } else { [string]$behaviorProperty.Value }
$behaviorOverride = Join-Path (Get-Location) '.engloop/out/code-review-response/campaign-test-behavior.txt'
if (Test-Path -LiteralPath $behaviorOverride) { $behavior = (Get-Content -LiteralPath $behaviorOverride -Raw).Trim() }
$payload | Add-Member -NotePropertyName adapterObservedBehavior -NotePropertyValue $behavior
Add-Content -LiteralPath $log -Value ($payload | ConvertTo-Json -Depth 64 -Compress)
if ($behavior -eq 'exit-failure') { exit 9 }
if ($behavior -eq 'invalid-json') { 'not-json'; exit 0 }
function Comment([string]$id,[string]$content) {
    [ordered]@{ comment=$id; parentComment=''; author='reviewer'; content=$content; createdAt='2026-09-04T00:00:00Z'; updatedAt='2026-09-04T00:00:00Z'; isDeleted=$false }
}
function Thread([string]$id,[string]$root,[string]$status,[string]$content,[string]$path,[object]$line) {
    [ordered]@{ thread=$id; rootComment=$root; status=$status; anchorPath=$path; line=$line; summary='Summary ' + $id; updatedAt='2026-09-04T00:00:00Z'; comments=@(Comment $root $content); providerState=[ordered]@{ rawStatus=$status } }
}
$threads = switch ($behavior) {
    'refresh' { @(
        (Thread 'thread-1' 'comment-1' 'active' 'changed-one' 'src/a.cs' 10),
        (Thread 'thread-2' 'comment-2' 'resolved' 'two' '' $null),
        (Thread 'thread-4' 'comment-4' 'active' 'four' 'src/d.cs' 40)
    ); break }
    'duplicate-thread' { @((Thread 'thread-1' 'comment-1' 'active' 'one' 'src/a.cs' 10),(Thread 'thread-1' 'comment-x' 'active' 'duplicate' 'src/a.cs' 10)); break }
    default { @(
        (Thread 'thread-1' 'comment-1' 'active' 'one' 'src/a.cs' 10),
        (Thread 'thread-2' 'comment-2' 'active' 'two' '' $null),
        (Thread 'thread-3' 'comment-3' 'active' 'three' 'src/c.cs' 30)
    ); break }
}
$result = [ordered]@{
    schemaVersion='1.0'; status='success'; provider=[string]$payload.provider; repository=[string]$payload.repository; pullRequest=[string]$payload.pullRequest
    actingPrincipal='fixture-principal'; sourceRevision=('a' * 40); targetRevision=('b' * 40); iteration='iteration-1'; providerHead=('a' * 40)
    pageCount=2; paginationComplete=$true; continuationToken=''; threadCount=$threads.Count; threads=$threads
    discoveryReceiptId='discovery-' + [string]$payload.campaignId; mutationPerformed=$false
}
switch ($behavior) {
    'incomplete' { $result.paginationComplete=$false }
    'continuation' { $result.continuationToken='next' }
    'wrong-count' { $result.threadCount=99 }
    'mutated' { $result.mutationPerformed=$true }
    'wrong-provider' { $result.provider='other' }
    'no-receipt' { $result.discoveryReceiptId='' }
}
$result | ConvertTo-Json -Depth 64 -Compress
""";
}
