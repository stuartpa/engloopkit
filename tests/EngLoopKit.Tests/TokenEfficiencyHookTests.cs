using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

[Collection("OperationsHookConsole")]
public sealed class TokenEfficiencyHookTests : IDisposable
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string Scripts = Path.Combine(Root, "extensions", "engloopkit", "scripts");
    private static readonly string ToolDll = Path.Combine(Root, "src", "EngLoopKit.Tool", "bin", "Debug", "net10.0", "engloopkit.dll");
    private readonly string _work = Path.Combine(Path.GetTempPath(), "elk-token-hooks-" + Guid.NewGuid().ToString("N"));

    public TokenEfficiencyHookTests() => Directory.CreateDirectory(_work);

    [Fact]
    public void AnalysisGuard_allowsOneValidEvidenceArtifact_andBlocksOtherWrites()
    {
        var repo = CreateRepository();
        var session = "analysis-session";
        var start = RunAnalysisPrompt(repo, session, "--session current");
        Assert.True(Continue(start));
        Assert.Contains("TOKEN_EFFICIENCY_ANALYSIS_GUARD_ACTIVE", start.Output);

        var analysisId = "known-session-20260805T120000000Z";
        var relative = $".engloop/evidence/token-efficiency-analysis-{analysisId}.json";
        var content = Analysis(repo, analysisId, relative).ToJsonString();
        var create = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "PreToolUse"], Hook(repo, session, "create_file", new { filePath = Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar)), content }));
        Assert.True(Continue(create), create.Output + "\n" + create.Error);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(repo, relative))!);
        File.WriteAllText(Path.Combine(repo, relative), content);

        var resumed = RunAnalysisPrompt(repo, session, "continue analysis");
        Assert.True(Continue(resumed), resumed.Output + "\n" + resumed.Error);
        Assert.Contains("TOKEN_EFFICIENCY_ANALYSIS_GUARD_ACTIVE", resumed.Output);

        var second = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "PreToolUse"], Hook(repo, session, "create_file", new { filePath = Path.Combine(repo, ".engloop", "evidence", "token-efficiency-analysis-second.json"), content }));
        Assert.False(Continue(second));

        var patch = "*** Begin Patch\n*** Update File: " + Path.Combine(repo, "README.md") + "\n-old\n+new\n*** End Patch";
        var edit = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "PreToolUse"], Hook(repo, session, "apply_patch", new { input = patch }));
        Assert.False(Continue(edit));

        var stop = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "Stop"], Hook(repo, session));
        Assert.True(Continue(stop));
    }

    [Fact]
    public void ImplementationGate_enforcesApprovalPathsCommandsAndFinalEvidence()
    {
        var repo = CreateRepository();
        var analysisId = "approved-session-20260805T121500000Z";
        var relative = $".engloop/evidence/token-efficiency-analysis-{analysisId}.json";
        var analysisPath = Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(analysisPath)!);
        File.WriteAllText(analysisPath, Analysis(repo, analysisId, relative).ToJsonString());

        var session = "implementation-session";
        var prompt = $"--analysis {relative} --approve TE-R001";
        var init = RunImplementationPrompt(repo, session, prompt);
        Assert.True(Continue(init), init.Output + "\n" + init.Error);
        Assert.Contains("TOKEN_EFFICIENCY_IMPLEMENTATION_SCOPE_ACTIVE", init.Output);

        var followUp = RunImplementationPrompt(repo, session, "continue focused repair");
        Assert.True(Continue(followUp), followUp.Output + "\n" + followUp.Error);
        Assert.Contains("TOKEN_EFFICIENCY_IMPLEMENTATION_SCOPE_ACTIVE", followUp.Output);

        var allowedPatch = "*** Begin Patch\n*** Add File: " + Path.Combine(repo, "docs", "efficiency.md") + "\n+content\n*** End Patch";
        var allowed = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "implementation", "-Event", "PreToolUse"], Hook(repo, session, "apply_patch", new { input = allowedPatch }));
        Assert.True(Continue(allowed));

        var deniedPatch = "*** Begin Patch\n*** Update File: " + Path.Combine(repo, "README.md") + "\n-old\n+new\n*** End Patch";
        var denied = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "implementation", "-Event", "PreToolUse"], Hook(repo, session, "apply_patch", new { input = deniedPatch }));
        Assert.False(Continue(denied));

        var validation = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "implementation", "-Event", "PreToolUse"], Hook(repo, session, "run_in_terminal", new { command = "git diff --check" }));
        Assert.True(Continue(validation));
        var commit = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "implementation", "-Event", "PreToolUse"], Hook(repo, session, "run_in_terminal", new { command = "git commit -am forbidden" }));
        Assert.False(Continue(commit));

        var premature = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "implementation", "-Event", "Stop"], Hook(repo, session));
        Assert.False(Continue(premature));

        var gatePath = Path.Combine(repo, ".engloop", "out", "token-efficiency", "gates", session + ".json");
        var gate = JsonNode.Parse(File.ReadAllText(gatePath))!.AsObject();
        var evidencePath = Path.Combine(repo, gate["implementationEvidencePath"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar));
        var evidence = JsonNode.Parse(File.ReadAllText(evidencePath))!.AsObject();
        evidence["outcome"] = "passed";
        evidence["repairStatus"] = new JsonArray(new JsonObject { ["id"] = "TE-R001", ["status"] = "implemented", ["detail"] = "validated" });
        evidence["changedFiles"] = new JsonArray("docs/efficiency.md");
        evidence["sourceState"]!["finalStatusDigest"] = StatusDigest(repo,
            gate["analysisPath"]!.GetValue<string>(),
            gate["implementationEvidencePath"]!.GetValue<string>());
        File.WriteAllText(evidencePath, evidence.ToJsonString());

        var complete = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "implementation", "-Event", "Stop"], Hook(repo, session));
        Assert.True(Continue(complete));
        Assert.False(File.Exists(gatePath));
    }

    [Fact]
    public void ImplementationInitializer_rejectsUnknownRepairId()
    {
        var repo = CreateRepository();
        var analysisId = "unknown-id-20260805T123000000Z";
        var relative = $".engloop/evidence/token-efficiency-analysis-{analysisId}.json";
        var path = Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Analysis(repo, analysisId, relative).ToJsonString());

        var result = RunImplementationPrompt(repo, "bad-session", $"--analysis {relative} --approve TE-R999");
        Assert.False(Continue(result));
        Assert.Contains("repair", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImplementationInitializer_rejectsMissingWildcardRangeDuplicateAndMachineApprovals()
    {
        var repo = CreateRepository();
        var analysisId = "approval-matrix-20260831T010000000Z";
        var relative = $".engloop/evidence/token-efficiency-analysis-{analysisId}.json";
        var path = Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Analysis(repo, analysisId, relative).ToJsonString());

        var invalidPrompts = new[]
        {
            $"--analysis {relative}",
            "--approve TE-R001",
            $"--analysis {relative} --approve all",
            $"--analysis {relative} --approve *",
            $"--analysis {relative} --approve TE-R001..TE-R002",
            $"--analysis {relative} --approve TE-R001,TE-R001",
            $"--analysis {relative} --approve TE-M001",
        };

        for (var index = 0; index < invalidPrompts.Length; index++)
        {
            var result = RunImplementationPrompt(repo, "invalid-approval-" + index, invalidPrompts[index]);
            Assert.False(Continue(result), result.Output + "\n" + result.Error);
        }
    }

    [Fact]
    public void ImplementationInitializer_rejectsStaleSourceAndUnresolvedPrerequisite()
    {
        var staleRepo = CreateRepository();
        var staleId = "stale-source-20260831T011500000Z";
        var staleRelative = $".engloop/evidence/token-efficiency-analysis-{staleId}.json";
        var stalePath = Path.Combine(staleRepo, staleRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(stalePath)!);
        File.WriteAllText(stalePath, Analysis(staleRepo, staleId, staleRelative).ToJsonString());
        File.WriteAllText(Path.Combine(staleRepo, "changed-after-analysis.txt"), "changed\n");

        var stale = RunImplementationPrompt(staleRepo, "stale-source", $"--analysis {staleRelative} --approve TE-R001");
        Assert.False(Continue(stale));
        Assert.Contains("status digest is stale", stale.Output, StringComparison.OrdinalIgnoreCase);

        var unresolvedRepo = CreateRepository();
        var unresolvedId = "unresolved-prerequisite-20260831T013000000Z";
        var unresolvedRelative = $".engloop/evidence/token-efficiency-analysis-{unresolvedId}.json";
        var unresolvedPath = Path.Combine(unresolvedRepo, unresolvedRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(unresolvedPath)!);
        var unresolvedAnalysis = Analysis(unresolvedRepo, unresolvedId, unresolvedRelative);
        unresolvedAnalysis["recommendedRepoRepairs"]![0]!["prerequisites"]![0]!["status"] = "unresolved";
        File.WriteAllText(unresolvedPath, unresolvedAnalysis.ToJsonString());

        var unresolved = RunImplementationPrompt(unresolvedRepo, "unresolved-prerequisite", $"--analysis {unresolvedRelative} --approve TE-R001");
        Assert.False(Continue(unresolved));
        Assert.Contains("unresolved prerequisite", unresolved.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalysisPromptActivation_rejectsCorruptExistingStateWithoutReplacingIt()
    {
        var repo = CreateRepository();
        const string session = "corrupt-analysis-state";
        Assert.True(Continue(RunAnalysisPrompt(repo, session, "--session current")));
        var gatePath = Path.Combine(repo, ".engloop", "out", "token-efficiency", "gates", session + ".analysis.json");
        const string corrupt = "{\"schemaVersion\":\"2.0\",\"mode\":\"analysis\",\"sessionId\":\"corrupt-analysis-state\",\"artifactPath\":null}";
        File.WriteAllText(gatePath, corrupt);

        var input = Hook(repo, session, prompt: "continue analysis");
        var entry = RunTool(["validate", "agent-entry-hook", "--stage", "speckit.engloop.30-token-efficiency-analyze", "--root", repo], input);
        Assert.True(Continue(entry), entry.Output + "\n" + entry.Error);
        var resumed = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "UserPromptSubmit"], input);

        Assert.False(Continue(resumed));
        Assert.Contains("analysis-guard-state-invalid", resumed.Output);
        Assert.Equal(corrupt, File.ReadAllText(gatePath));
    }

    [Fact]
    public void PromptScripts_rejectInvalidEntryWithoutCreatingGateOrEvidenceState()
    {
        var repo = Path.Combine(_work, "invalid-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, ".gitignore"), ".engloop/out/\n");
        Git(repo, "init");
        Git(repo, "config", "user.email", "hooks@example.invalid");
        Git(repo, "config", "user.name", "Hook Test");
        Git(repo, "add", ".");
        Git(repo, "commit", "-m", "invalid fixture");

        var analysisInput = Hook(repo, "invalid-analysis", prompt: "--session current");
        var analysisEntry = RunTool(["validate", "agent-entry-hook", "--stage", "speckit.engloop.30-token-efficiency-analyze", "--root", repo], analysisInput);
        Assert.False(Continue(analysisEntry));
        var analysis = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "UserPromptSubmit"], analysisInput);
        Assert.False(Continue(analysis));
        Assert.Contains("prompt-entry-receipt-missing", analysis.Output);
        Assert.False(Directory.Exists(Path.Combine(repo, ".engloop", "out", "token-efficiency", "gates")));

        var implementationInput = Hook(repo, "invalid-implementation", prompt: "--analysis .engloop/evidence/token-efficiency-analysis-invalid.json --approve TE-R001");
        var implementationEntry = RunTool(["validate", "agent-entry-hook", "--stage", "speckit.engloop.31-token-efficiency-implement", "--root", repo], implementationInput);
        Assert.False(Continue(implementationEntry));
        var implementation = RunScript("Initialize-TokenEfficiencyImplementationGate.ps1", [], implementationInput);
        Assert.False(Continue(implementation));
        Assert.Contains("prompt-entry-receipt-missing", implementation.Output);
        Assert.False(Directory.Exists(Path.Combine(repo, ".engloop", "evidence")));
    }

    [Fact]
    public void SourceStateHelper_excludesPlannedAnalysisPath()
    {
        var repo = CreateRepository();
        var relative = ".engloop/evidence/token-efficiency-analysis-fixture.json";
        var before = RunScript("Get-TokenEfficiencySourceState.ps1", ["-RepositoryRoot", repo, "-ExcludePath", relative], string.Empty);
        Assert.Equal(0, before.ExitCode);
        using var beforeJson = JsonDocument.Parse(before.Output);

        var path = Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}\n");
        var after = RunScript("Get-TokenEfficiencySourceState.ps1", ["-RepositoryRoot", repo, "-ExcludePath", relative], string.Empty);
        Assert.Equal(0, after.ExitCode);
        using var afterJson = JsonDocument.Parse(after.Output);

        Assert.Equal(beforeJson.RootElement.GetProperty("head").GetString(), afterJson.RootElement.GetProperty("head").GetString());
        Assert.Equal(beforeJson.RootElement.GetProperty("gitStatusDigest").GetString(), afterJson.RootElement.GetProperty("gitStatusDigest").GetString());
    }

    [Fact]
    public void PromptEntryHook_emitsJsonMarkerAndShortCircuitingRejection()
    {
        var repo = CreateRepository();
        var acceptedInput = Hook(repo, "entry/accepted", prompt: "--session current");
        var accepted = RunTool(["validate", "agent-entry-hook", "--stage", "speckit.engloop.30-token-efficiency-analyze", "--root", repo], acceptedInput);
        Assert.Equal(0, accepted.ExitCode);
        using (var json = JsonDocument.Parse(accepted.Output))
        {
            Assert.True(json.RootElement.GetProperty("continue").GetBoolean());
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("stopReason").ValueKind);
            Assert.Contains("AGENT_ENTRY_OK", json.RootElement.GetProperty("systemMessage").GetString());
        }
        Assert.True(File.Exists(EntryReceipt(repo, "entry_accepted", "analysis")));

        var rejected = RunTool(["validate", "agent-entry-hook", "--stage", "speckit.engloop.99-missing", "--root", repo], Hook(repo, "entry-rejected", prompt: "--session current"));
        Assert.Equal(0, rejected.ExitCode);
        using var rejectedJson = JsonDocument.Parse(rejected.Output);
        Assert.False(rejectedJson.RootElement.GetProperty("continue").GetBoolean());
        Assert.Contains("invalid-stage", rejectedJson.RootElement.GetProperty("stopReason").GetString());
    }

    [Fact]
    public void CompiledPromptEntryHook_emitsJsonMarker()
    {
        var repo = CreateRepository();

        var result = RunToolSubprocess(["validate", "agent-entry-hook", "--stage", "speckit.engloop.30-token-efficiency-analyze", "--root", repo], Hook(repo, "compiled-entry", prompt: "--session current"));

        Assert.True(Continue(result), result.Output + "\n" + result.Error);
        Assert.Contains("AGENT_ENTRY_OK", result.Output);
    }

    [Fact]
    public void PromptEntryReceipt_isTimestampBoundAndSingleUse()
    {
        var repo = CreateRepository();
        const string session = "event-bound-entry";
        var eventTime = DateTimeOffset.Parse("2026-08-31T08:00:00.1234567Z");
        var input = Hook(repo, session, prompt: "--session current", timestamp: eventTime.ToString("o"));

        var entry = RunTool(["validate", "agent-entry-hook", "--stage", "speckit.engloop.30-token-efficiency-analyze", "--root", repo], input);
        Assert.True(Continue(entry), entry.Output + "\n" + entry.Error);

        var mismatchedEvent = Hook(repo, session, prompt: "--session current", timestamp: eventTime.AddTicks(1).ToString("o"));
        var stale = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "UserPromptSubmit"], mismatchedEvent);
        Assert.False(Continue(stale));
        Assert.Contains("prompt-entry-receipt-stale-or-invalid", stale.Output);

        var replacement = RunTool(["validate", "agent-entry-hook", "--stage", "speckit.engloop.30-token-efficiency-analyze", "--root", repo], mismatchedEvent);
        Assert.True(Continue(replacement), replacement.Output + "\n" + replacement.Error);
        var activated = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "UserPromptSubmit"], mismatchedEvent);
        Assert.True(Continue(activated), activated.Output + "\n" + activated.Error);
        Assert.False(File.Exists(EntryReceipt(repo, session, "analysis")));

        var replay = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "UserPromptSubmit"], mismatchedEvent);
        Assert.False(Continue(replay));
        Assert.Contains("prompt-entry-receipt-missing", replay.Output);
    }

    [Fact]
    public void PromptEntryHook_rejectsMalformedInputAndIdentityWithoutCreatingReceipts()
    {
        var repo = CreateRepository();
        var otherRepo = CreateRepository();
        const string stage = "speckit.engloop.30-token-efficiency-analyze";
        var timestamp = DateTimeOffset.Parse("2026-08-31T08:30:00.1234567Z").ToString("o");
        var cases = new[]
        {
            (Name: "malformed-json", Stage: stage, Input: "not-json", Diagnostic: "JSON"),
            (Name: "missing-cwd", Stage: stage, Input: JsonSerializer.Serialize(new { timestamp, session_id = "missing-cwd" }), Diagnostic: "hook-cwd-missing"),
            (Name: "missing-session", Stage: stage, Input: JsonSerializer.Serialize(new { timestamp, cwd = repo }), Diagnostic: "hook-session-id-missing"),
            (Name: "missing-timestamp", Stage: stage, Input: JsonSerializer.Serialize(new { cwd = repo, session_id = "missing-timestamp" }), Diagnostic: "hook-timestamp-missing"),
            (Name: "invalid-timestamp", Stage: stage, Input: JsonSerializer.Serialize(new { timestamp = "not-a-time", cwd = repo, session_id = "invalid-timestamp" }), Diagnostic: "hook-timestamp-invalid"),
            (Name: "cwd-mismatch", Stage: stage, Input: JsonSerializer.Serialize(new { timestamp, cwd = otherRepo, session_id = "cwd-mismatch" }), Diagnostic: "hook-cwd-root-mismatch"),
            (Name: "wrong-stage", Stage: "speckit.engloop.01-northstar", Input: JsonSerializer.Serialize(new { timestamp, cwd = repo, session_id = "wrong-stage" }), Diagnostic: "invalid-stage"),
        };

        foreach (var item in cases)
        {
            var result = RunTool(["validate", "agent-entry-hook", "--stage", item.Stage, "--root", repo], item.Input);
            Assert.Equal(0, result.ExitCode);
            Assert.False(Continue(result), item.Name + ": " + result.Output + "\n" + result.Error);
            using var json = JsonDocument.Parse(result.Output);
            Assert.Contains(item.Diagnostic, json.RootElement.GetProperty("stopReason").GetString(), StringComparison.OrdinalIgnoreCase);
        }

        Assert.False(Directory.Exists(Path.Combine(repo, ".engloop", "out", "token-efficiency", "gates")));

        var noIgnore = CreateRepository();
        File.WriteAllText(Path.Combine(noIgnore, ".gitignore"), string.Empty);
        var noIgnoreResult = RunTool(["validate", "agent-entry-hook", "--stage", stage, "--root", noIgnore], Hook(noIgnore, "not-ignored", prompt: "--session current", timestamp: timestamp));
        Assert.False(Continue(noIgnoreResult));
        Assert.Contains("token-efficiency-output-not-ignored", noIgnoreResult.Output);
        Assert.False(Directory.Exists(Path.Combine(noIgnore, ".engloop", "out", "token-efficiency", "gates")));

        var camelInput = JsonSerializer.Serialize(new { timestamp, cwd = repo, sessionId = "camel-session", prompt = "--session current" });
        var camel = RunTool(["validate", "agent-entry-hook", "--stage", stage, "--root", repo], camelInput);
        Assert.True(Continue(camel), camel.Output + "\n" + camel.Error);
        Assert.True(File.Exists(EntryReceipt(repo, "camel-session", "analysis")));
    }

    private string CreateRepository()
    {
        var repo = Path.Combine(_work, "repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(repo, ".engloop", "evidence"));
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(Path.Combine(repo, "README.md"), "old\n");
        File.WriteAllText(Path.Combine(repo, "NORTHSTAR.md"), "# Direction\n");
        File.WriteAllText(Path.Combine(repo, "LEARNINGS.md"), "# Learnings\n");
        File.WriteAllText(Path.Combine(repo, "docs", ".keep"), "keep\n");
        File.WriteAllText(Path.Combine(repo, "src", "fixture.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        File.WriteAllText(Path.Combine(repo, ".engloop", "config.json"), "{\"schemaVersion\":\"2.0\",\"productId\":\"fixture\",\"artifactRoot\":\".engloop\",\"transientOutputRoot\":\".engloop/out\",\"northstarPath\":\"NORTHSTAR.md\",\"validatorCommand\":[\"dotnet\",\"--version\"],\"moduleDiscoveryCommand\":[\"dotnet\",\"--version\"],\"architectureCommand\":[\"dotnet\",\"--version\"],\"regressionCommand\":[\"dotnet\",\"--version\"],\"coverageInputs\":{\"wholeProduct\":\"src/fixture.csproj\"},\"testRunway\":{\"status\":\"proven\",\"framework\":\"xunit\",\"terseCommand\":[\"dotnet\",\"--version\"],\"boundaryTest\":\"Fixture.Boundary\",\"generatedDestination\":\"tests/generated\",\"evidenceDigest\":\"fixture\",\"provenAtRevision\":\"content:fixture\"},\"moduleInventory\":[{\"id\":\"core\",\"path\":\"src/fixture.csproj\"}]}\n");
        File.WriteAllText(Path.Combine(repo, ".gitignore"), ".engloop/out/\n");
        Git(repo, "init");
        Git(repo, "config", "user.email", "hooks@example.invalid");
        Git(repo, "config", "user.name", "Hook Test");
        Git(repo, "add", ".");
        Git(repo, "commit", "-m", "fixture");
        return repo;
    }

    private static JsonObject Analysis(string repo, string analysisId, string excludedPath)
    {
        var head = GitOutput(repo, "rev-parse", "HEAD").Trim().ToLowerInvariant();
        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["artifactType"] = "token-efficiency-analysis",
            ["analysisId"] = analysisId,
            ["capturedAtUtc"] = DateTimeOffset.UtcNow.ToString("o"),
            ["scope"] = new JsonObject { ["session"] = analysisId, ["repository"] = "fixture", ["agentSurface"] = "VS Code Chat" },
            ["dataAvailability"] = new JsonObject { ["backend"] = "fixture-proxy", ["tokenData"] = "unavailable", ["limitations"] = new JsonArray("fixture") },
            ["evidence"] = new JsonArray(),
            ["findings"] = new JsonArray(),
            ["wasteEstimate"] = new JsonObject { ["basis"] = "unavailable", ["value"] = null, ["unit"] = null, ["range"] = null, ["limitations"] = new JsonArray("fixture") },
            ["recommendedRepoRepairs"] = new JsonArray(new JsonObject
            {
                ["id"] = "TE-R001",
                ["type"] = "script",
                ["summary"] = "Add a bounded helper",
                ["allowedPaths"] = new JsonArray("docs/efficiency.md"),
                ["prohibitedPaths"] = new JsonArray("src/deployment/"),
                ["prerequisites"] = new JsonArray(new JsonObject { ["id"] = "TE-P001", ["status"] = "resolved", ["evidence"] = "fixture" }),
                ["validationPlan"] = new JsonArray(new JsonObject { ["id"] = "TE-V001", ["command"] = new JsonArray("git", "diff", "--check"), ["scope"] = "focused", ["purpose"] = "validate diff" })
            }),
            ["recommendedMachineRepairs"] = new JsonArray(new JsonObject { ["id"] = "TE-M001", ["summary"] = "machine only", ["evidence"] = "fixture" }),
            ["confidence"] = "high",
            ["sourceState"] = new JsonObject { ["head"] = head, ["gitStatusDigest"] = StatusDigest(repo, excludedPath) }
        };
    }

    private static string StatusDigest(string repo, params string[] excludedPaths)
    {
        var excluded = excludedPaths.Select(path => path.Replace('\\', '/').ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var lines = GitOutput(repo, "status", "--porcelain=v1", "--untracked-files=all")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Length < 4 || !excluded.Contains(line[3..].Trim('"').Replace('\\', '/').ToLowerInvariant()))
            .OrderBy(line => line, StringComparer.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', lines));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string Hook(string repo, string session, string? toolName = null, object? toolInput = null, string? prompt = null, string? timestamp = null)
        => JsonSerializer.Serialize(new { timestamp = timestamp ?? DateTimeOffset.UtcNow.ToString("o"), cwd = repo, session_id = session, prompt, tool_name = toolName, tool_input = toolInput });

    private static (int ExitCode, string Output, string Error) RunAnalysisPrompt(string repo, string session, string prompt)
    {
        var input = Hook(repo, session, prompt: prompt);
        var entry = RunTool(["validate", "agent-entry-hook", "--stage", "speckit.engloop.30-token-efficiency-analyze", "--root", repo], input);
        Assert.True(Continue(entry), entry.Output + "\n" + entry.Error);
        var active = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "UserPromptSubmit"], input);
        Assert.False(File.Exists(EntryReceipt(repo, session, "analysis")));
        return active;
    }

    private static (int ExitCode, string Output, string Error) RunImplementationPrompt(string repo, string session, string prompt)
    {
        var input = Hook(repo, session, prompt: prompt);
        var entry = RunTool(["validate", "agent-entry-hook", "--stage", "speckit.engloop.31-token-efficiency-implement", "--root", repo], input);
        Assert.True(Continue(entry), entry.Output + "\n" + entry.Error);
        var loaded = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "implementation", "-Event", "UserPromptSubmit"], input);
        Assert.True(Continue(loaded), loaded.Output + "\n" + loaded.Error);
        Assert.Contains("TOKEN_EFFICIENCY_IMPLEMENTATION_GUARD_LOADED", loaded.Output);
        var scoped = RunScript("Initialize-TokenEfficiencyImplementationGate.ps1", [], input);
        Assert.False(File.Exists(EntryReceipt(repo, session, "implementation")));
        return scoped;
    }

    private static string EntryReceipt(string repo, string session, string mode)
        => Path.Combine(repo, ".engloop", "out", "token-efficiency", "gates", session + "." + mode + ".entry.json");

    private static bool Continue((int ExitCode, string Output, string Error) result)
    {
        Assert.Equal(0, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        return json.RootElement.GetProperty("continue").GetBoolean();
    }

    private static (int ExitCode, string Output, string Error) RunScript(string script, string[] args, string stdin)
    {
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = Root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(Path.Combine(Scripts, script));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        if (!string.IsNullOrEmpty(stdin)) process.StandardInput.Write(stdin);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static (int ExitCode, string Output, string Error) RunTool(string[] args, string stdin)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var input = new StringReader(stdin);
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetIn(input);
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = Program.Main(args);
            return (exitCode, output.ToString().Trim(), error.ToString().Trim());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static (int ExitCode, string Output, string Error) RunToolSubprocess(string[] args, string stdin)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(ToolDll);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        process.StandardInput.Write(stdin);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static void Git(string repo, params string[] args)
    {
        var result = RunGit(repo, args);
        Assert.True(result.ExitCode == 0, result.Error);
    }

    private static string GitOutput(string repo, params string[] args)
    {
        var result = RunGit(repo, args);
        Assert.True(result.ExitCode == 0, result.Error);
        return result.Output;
    }

    private static (int ExitCode, string Output, string Error) RunGit(string repo, string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bundle.yml"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            try { Directory.Delete(_work, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
