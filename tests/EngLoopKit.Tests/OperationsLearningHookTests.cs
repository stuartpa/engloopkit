using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EngLoopKit.Core;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

[CollectionDefinition("OperationsHookConsole", DisableParallelization = true)]
public sealed class OperationsHookConsoleCollection;

[Collection("OperationsHookConsole")]
public sealed class OperationsLearningHookTests : IDisposable
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string ToolDll = Path.Combine(Root, "src", "EngLoopKit.Tool", "bin", "Debug", "net10.0", "engloopkit.dll");
    private readonly string _work = Path.Combine(Path.GetTempPath(), "elk-operations-hooks-" + Guid.NewGuid().ToString("N"));

    public OperationsLearningHookTests() => Directory.CreateDirectory(_work);

    [Fact]
    public void Hook_requiresSessionIdentity_andExplicitPostmortemPath()
    {
        var repo = CreateRepository();
        var noSession = RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md", session: "");
        Assert.False(Continues(noSession));
        Assert.Contains("session-id-missing", noSession.Output);

        var missingPath = RunHook(repo, "postmortem", "initialize", "--incidents IN001", session: "pm-session");
        AssertPostmortemContextRequired(missingPath, "initialize", "--postmortem");
    }

    [Fact]
    public void PostmortemMissingContext_keepsChatUsableButDeniesToolsAndAcceptsNoCompletion()
    {
        var repo = CreateRepository();
        const string session = "postmortem-context-recovery";

        var initialized = RunHook(repo, "postmortem", "initialize", "Continue the retrospective for the stabilized incidents.", session);
        AssertPostmortemContextRequired(initialized, "initialize", "--postmortem");

        var guarded = RunHook(repo, "postmortem", "guard", string.Empty, session);
        Assert.True(Continues(guarded), guarded.Output + guarded.Error);
        using (var guardJson = JsonDocument.Parse(guarded.Output))
        {
            var specific = guardJson.RootElement.GetProperty("hookSpecificOutput");
            Assert.Equal("PreToolUse", specific.GetProperty("hookEventName").GetString());
            Assert.Equal("deny", specific.GetProperty("permissionDecision").GetString());
            Assert.Contains("--incidents", specific.GetProperty("permissionDecisionReason").GetString());
            Assert.Contains("--postmortem", specific.GetProperty("permissionDecisionReason").GetString());
        }

        var stopped = RunHook(repo, "postmortem", "stop", string.Empty, session);
        AssertPostmortemContextRequired(stopped, "stop", "operations-hook-gate-missing");
        Assert.DoesNotContain("POSTMORTEM_LEARNING_OK", stopped.Output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(repo, ".engloop", "out", "operations-learning-gates")));

        const string validPrompt = "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md";
        var rebound = RunHook(repo, "postmortem", "initialize", validPrompt, session);
        Assert.True(Continues(rebound), rebound.Output + rebound.Error);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", rebound.Output);

        var allowed = RunHook(repo, "postmortem", "guard", string.Empty, session);
        Assert.True(Continues(allowed), allowed.Output + allowed.Error);
        using var allowedJson = JsonDocument.Parse(allowed.Output);
        Assert.False(allowedJson.RootElement.TryGetProperty("hookSpecificOutput", out _));
    }

    [Theory]
    [InlineData("", "prompt-missing")]
    [InlineData("--incidents IN001", "--postmortem")]
    [InlineData("--postmortem .engloop/postmortems/PM005_example.md", "--incidents")]
    [InlineData("--incidents BAD --postmortem .engloop/postmortems/PM005_example.md", "incident-ids-invalid")]
    [InlineData("--incidents IN001 --postmortem C:/absolute.md", "path-invalid")]
    public void PostmortemMalformedContext_reportsActionableRecoveryWithoutCreatingGate(string prompt, string diagnostic)
    {
        var repo = CreateRepository();

        var result = RunHook(repo, "postmortem", "initialize", prompt, "postmortem-malformed");

        AssertPostmortemContextRequired(result, "initialize", diagnostic);
        Assert.False(Directory.Exists(Path.Combine(repo, ".engloop", "out", "operations-learning-gates")));
    }

    [Fact]
    public void PostmortemGate_isCreateNewHeadAndArgumentBound()
    {
        var repo = CreateRepository();
        const string prompt = "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md";
        var initialized = RunHook(repo, "postmortem", "initialize", prompt, "pm-session");
        Assert.True(Continues(initialized), initialized.Output);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", initialized.Output);

        var repeated = RunHook(repo, "postmortem", "initialize", prompt, "pm-session");
        Assert.True(Continues(repeated), repeated.Output);

        var followup = RunHook(repo, "postmortem", "initialize", "continue analysis", "pm-session");
        Assert.True(Continues(followup), followup.Output);

        var changed = RunHook(repo, "postmortem", "initialize", "--incidents IN002 --postmortem .engloop/postmortems/PM006_other.md", "pm-session");
        Assert.False(Continues(changed));
        Assert.Contains("arguments-changed", changed.Output);
    }

    [Fact]
    public void ExistingPostmortemGate_incompleteOrIrrelevantFollowupNeverDeadEndsChatOrAuthorizesWrongScope()
    {
        var repo = CreateRepository();
        const string session = "pm-existing-continuation";
        const string prompt = "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md";
        Assert.True(Continues(RunHook(repo, "postmortem", "initialize", prompt, session)));

        var unrelatedOption = RunHook(repo, "postmortem", "initialize", "Continue analysis --focus on the remaining repair item.", session);
        Assert.True(Continues(unrelatedOption), unrelatedOption.Output + unrelatedOption.Error);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", unrelatedOption.Output);

        var incompleteScope = RunHook(repo, "postmortem", "initialize", "--incidents IN001", session);
        AssertPostmortemContextRequired(incompleteScope, "initialize", "--postmortem");

        AssertPostmortemGuardDenied(RunHook(repo, "postmortem", "guard", string.Empty, session), "option-missing:--postmortem");
        var stopped = RunHook(repo, "postmortem", "stop", string.Empty, session);
        AssertPostmortemContextRequired(stopped, "stop", "option-missing:--postmortem");
        Assert.DoesNotContain("POSTMORTEM_LEARNING_OK", stopped.Output, StringComparison.Ordinal);

        var noArguments = RunHook(repo, "postmortem", "initialize", "Continue analysis without changing scope.", session);
        AssertPostmortemContextRequired(noArguments, "initialize", "option-missing:--postmortem");

        var rebound = RunHook(repo, "postmortem", "initialize", prompt, session);
        Assert.True(Continues(rebound), rebound.Output + rebound.Error);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", rebound.Output);
        var allowed = RunHook(repo, "postmortem", "guard", string.Empty, session);
        Assert.True(Continues(allowed), allowed.Output + allowed.Error);
        using var allowedJson = JsonDocument.Parse(allowed.Output);
        Assert.False(allowedJson.RootElement.TryGetProperty("hookSpecificOutput", out _));
    }

    [Fact]
    public void ExistingIncidentAndRepairGates_recognizeOnlyTheirOwnScopeOptions()
    {
        var incidentRepo = CreateRepository();
        const string incidentPrompt = "--incident .engloop/incidents/IN001_example.md";
        Assert.True(Continues(RunHook(incidentRepo, "incident", "initialize", incidentPrompt, "incident-continuation")));
        var incidentUnrelated = RunHook(incidentRepo, "incident", "initialize", "Continue --focus on mitigation verification.", "incident-continuation");
        Assert.True(Continues(incidentUnrelated), incidentUnrelated.Output + incidentUnrelated.Error);
        Assert.True(Continues(RunHook(incidentRepo, "incident", "initialize", incidentPrompt, "incident-continuation")));

        var repairRepo = CreateRepository();
        const string repairPrompt = "--phase route --postmortem .engloop/postmortems/PM005.md --rpi RPI001 --rules RULE:x --acceptance .engloop/repairs/PM005-RPI001.route.json";
        Assert.True(Continues(RunHook(repairRepo, "repair", "initialize", repairPrompt, "repair-continuation")));
        var repairUnrelated = RunHook(repairRepo, "repair", "initialize", "Continue --focus on the approved repair.", "repair-continuation");
        Assert.True(Continues(repairUnrelated), repairUnrelated.Output + repairUnrelated.Error);
        Assert.True(Continues(RunHook(repairRepo, "repair", "initialize", repairPrompt, "repair-continuation")));
    }

    [Fact]
    public void PostmortemGate_rejectsExistingOrPreviouslyUsedNumber()
    {
        var repo = CreateRepository();
        Directory.CreateDirectory(Path.Combine(repo, ".engloop", "postmortems"));
        File.WriteAllText(Path.Combine(repo, ".engloop", "postmortems", "PM005_existing.md"), "existing\n");
        var sameTarget = RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005_existing.md", "pm-same-target");
        Assert.False(Continues(sameTarget));
        Assert.Contains("postmortem-target-already-exists", sameTarget.Output);

        var result = RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005_new.md", "pm-existing");
        Assert.False(Continues(result));
        Assert.Contains("postmortem-number-already-used", result.Output);
    }

    [Fact]
    public void PostmortemGuard_deniesInvalidGateStateWithoutStoppingChatOrDeletingEvidence()
    {
        void DenyMutation(Action<JsonObject> mutate, string expected)
        {
            var repo = CreateRepository();
            const string session = "postmortem-guard-matrix";
            Assert.True(Continues(RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md", session)));
            var path = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json"));
            var gate = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            mutate(gate);
            File.WriteAllText(path, gate.ToJsonString());

            AssertPostmortemGuardDenied(RunHook(repo, "postmortem", "guard", string.Empty, session), expected);
            Assert.True(File.Exists(path));
        }

        DenyMutation(gate => gate["Head"] = new string('0', 40), "gate-identity-invalid");
        DenyMutation(gate => gate["Postmortem"] = ".engloop/postmortems/PM999_tampered.md", "arguments-tampered");

        var corrupt = CreateRepository();
        const string corruptSession = "postmortem-guard-corrupt";
        Assert.True(Continues(RunHook(corrupt, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md", corruptSession)));
        var corruptPath = Assert.Single(Directory.GetFiles(Path.Combine(corrupt, ".engloop", "out", "operations-learning-gates"), "*.json"));
        File.WriteAllText(corruptPath, "{");
        AssertPostmortemGuardDenied(RunHook(corrupt, "postmortem", "guard", string.Empty, corruptSession), "operations-hook-json-invalid");
        Assert.True(File.Exists(corruptPath));

        var identity = CreateRepository();
        const string identitySession = "postmortem-guard-tool-identity";
        Assert.True(Continues(RunHook(identity, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md", identitySession)));
        var identityPath = Assert.Single(Directory.GetFiles(Path.Combine(identity, ".engloop", "out", "operations-learning-gates"), "*.json"));
        File.AppendAllText(Path.Combine(identity, ".config", "dotnet-tools.json"), " ");
        AssertPostmortemGuardDenied(RunHook(identity, "postmortem", "guard", string.Empty, identitySession), "tool-identity-changed");
        Assert.True(File.Exists(identityPath));
    }

    [Fact]
    public void PostmortemGuard_deniesCorruptSuspensionStateUntilExactContextReactivatesGate()
    {
        var repo = CreateRepository();
        const string session = "postmortem-corrupt-suspension";
        const string prompt = "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md";
        Assert.True(Continues(RunHook(repo, "postmortem", "initialize", prompt, session)));
        var gatePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json"));
        var suspensionPath = gatePath + ".context-required";
        File.WriteAllText(suspensionPath, "not-a-context-diagnostic");

        AssertPostmortemGuardDenied(RunHook(repo, "postmortem", "guard", string.Empty, session), "postmortem-context-state-invalid");
        AssertPostmortemContextRequired(RunHook(repo, "postmortem", "stop", string.Empty, session), "stop", "postmortem-context-state-invalid");
        Assert.True(File.Exists(gatePath));
        Assert.True(File.Exists(suspensionPath));

        var rebound = RunHook(repo, "postmortem", "initialize", prompt, session);
        Assert.True(Continues(rebound), rebound.Output + rebound.Error);
        Assert.False(File.Exists(suspensionPath));
    }

    [Fact]
    public void RepairGate_requiresConcreteRulesAndPhaseSpecificCreateNewPath()
    {
        var repo = CreateRepository();
        var invalid = RunHook(repo, "repair", "initialize", "--phase route --postmortem .engloop/postmortems/PM005_example.md --rpi RPI001 --rules all --acceptance .engloop/repairs/PM005-RPI001.route.json", "repair-invalid");
        Assert.False(Continues(invalid));

        var valid = RunHook(repo, "repair", "initialize", "--phase route --postmortem .engloop/postmortems/PM005_example.md --rpi RPI001 --rules RULE:reliability --acceptance .engloop/repairs/PM005-RPI001.route.json", "repair-valid");
        Assert.True(Continues(valid), valid.Output);

        var wrongSuffix = RunHook(repo, "repair", "initialize", "--phase close --postmortem .engloop/postmortems/PM005_example.md --rpi RPI001 --rules RULE:reliability --acceptance .engloop/repairs/PM005-RPI001.json", "repair-close");
        Assert.False(Continues(wrongSuffix));
        Assert.Contains("phase-filename-mismatch", wrongSuffix.Output);
    }

    [Fact]
    public void ExistingHookGate_rejectsArgumentTamperingAndManifestChange()
    {
        var repo = CreateRepository();
        const string prompt = "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md";
        Assert.True(Continues(RunHook(repo, "postmortem", "initialize", prompt, "pm-tamper")));
        var gates = Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json");
        var gate = JsonDocument.Parse(File.ReadAllText(Assert.Single(gates))).RootElement;
        var map = JsonSerializer.Deserialize<Dictionary<string, object?>>(gate.GetRawText())!;
        map["Postmortem"] = ".engloop/postmortems/PM999_tampered.md";
        File.WriteAllText(gates[0], JsonSerializer.Serialize(map));
        var tampered = RunHook(repo, "postmortem", "initialize", "continue", "pm-tamper");
        Assert.False(Continues(tampered));
        Assert.Contains("arguments-tampered", tampered.Output);

        var repo2 = CreateRepository();
        Assert.True(Continues(RunHook(repo2, "postmortem", "initialize", prompt, "pm-manifest")));
        File.AppendAllText(Path.Combine(repo2, ".config", "dotnet-tools.json"), " ");
        var changed = RunHook(repo2, "postmortem", "initialize", "continue", "pm-manifest");
        Assert.False(Continues(changed));
        Assert.Contains("tool-identity-changed", changed.Output);
    }

    [Fact]
    public void PostmortemStop_invokesRealValidator_andBlocksInvalidArtifact()
    {
        var repo = CreateRepository();
        const string prompt = "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md";
        Assert.True(Continues(RunHook(repo, "postmortem", "initialize", prompt, "pm-stop")));
        File.WriteAllText(Path.Combine(repo, ".engloop", "postmortems", "PM005_example.md"), "# invalid PM\n");

        var stopped = RunHook(repo, "postmortem", "stop", string.Empty, "pm-stop");

        Assert.False(Continues(stopped));
        Assert.Contains("validation failed", stopped.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("route")]
    [InlineData("close")]
    public void RepairAcceptance_rejectsDeletedHistoricalPath(string phase)
    {
        var repo = CreateRepository();
        var relative = $".engloop/repairs/PM005-RPI001.{phase}.json";
        var full = Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(full, "historical\n");
        Git(repo, "add", relative);
        Git(repo, "commit", "-m", "historical acceptance");
        File.Delete(full);
        Git(repo, "add", "-u");
        Git(repo, "commit", "-m", "remove historical acceptance");

        var result = RunHook(repo, "repair", "initialize", $"--phase {phase} --postmortem .engloop/postmortems/PM005_example.md --rpi RPI001 --rules RULE:reliability --acceptance {relative}", "repair-history-" + phase);

        Assert.False(Continues(result));
        Assert.Contains("present-in-history", result.Output);
    }

    [Fact]
    public void SuccessfulIncidentStop_emitsExactlyOneJsonHookResponse()
    {
        var repo = CreateRepository();
        const string prompt = "--incident .engloop/incidents/IN001_example.md";
        Assert.True(Continues(RunHook(repo, "incident", "initialize", prompt, "incident-stop")));

        var result = RunHook(repo, "incident", "stop", string.Empty, "incident-stop");

        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Contains("INCIDENT_CONTEXT_OK", json.RootElement.GetProperty("systemMessage").GetString());
    }

    [Fact]
    public void IncidentHook_withoutIncidentOption_defersContextWithoutStoppingMitigation()
    {
        var repo = CreateRepository();
        const string session = "incident-without-metadata";

        var initialized = RunHook(repo, "incident", "initialize", "The production queue is blocked; restore service now.", session);

        Assert.True(Continues(initialized), initialized.Output);
        using (var json = JsonDocument.Parse(initialized.Output))
        {
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("stopReason").ValueKind);
            var diagnostic = json.RootElement.GetProperty("systemMessage").GetString();
            Assert.Contains("OPERATIONS_LEARNING_CONTEXT_DEFERRED", diagnostic);
            Assert.Contains("\"status\":\"learning-context-deferred\"", diagnostic);
            Assert.Contains("\"phase\":\"initialize\"", diagnostic);
            Assert.Contains("\"command\":\"operations-hook initialize incident\"", diagnostic);
            Assert.Contains("\"missingOption\":\"--incident\"", diagnostic);
            Assert.Contains("\"expectedSource\":", diagnostic);
            Assert.Contains("\"elkVersion\":", diagnostic);
            Assert.Contains("\"remediation\":", diagnostic);
        }

        var stopped = RunHook(repo, "incident", "stop", string.Empty, session);

        Assert.True(Continues(stopped), stopped.Output);
        using var stopJson = JsonDocument.Parse(stopped.Output);
        Assert.Contains("\"phase\":\"stop\"", stopJson.RootElement.GetProperty("systemMessage").GetString());
    }

    [Fact]
    public void SubprocessStart_emitsOneJsonResponseFromCompiledTool()
    {
        var repo = CreateRepository();
        var inProcess = RunHook(repo, "incident", "start", string.Empty, "in-process-start");
        Assert.True(Continues(inProcess));
        Assert.Contains("OPERATIONS_LEARNING_GUARD_ACTIVE", inProcess.Output);

        var result = RunHookSubprocess(repo, "incident", "start", string.Empty, "subprocess-session");
        Assert.True(Continues(result));
        Assert.Contains("OPERATIONS_LEARNING_GUARD_ACTIVE", result.Output);
    }

    [Fact]
    public void SubprocessIncidentInitialize_withoutOption_keepsCompiledHookNonBlocking()
    {
        var repo = CreateRepository();

        var result = RunHookSubprocess(repo, "incident", "initialize", "Restore the affected service immediately.", "subprocess-deferred");

        AssertIncidentDeferred(result, "initialize", "operations-hook-option-missing:--incident");
    }

    [Fact]
    public void SubprocessPostmortemInitialize_withoutOption_returnsActionableNonAuthorizingRecovery()
    {
        var repo = CreateRepository();

        var result = RunHookSubprocess(repo, "postmortem", "initialize", "Continue the retrospective.", "subprocess-postmortem-context");

        AssertPostmortemContextRequired(result, "initialize", "operations-hook-option-missing:--postmortem");
    }

    [Fact]
    public void Hook_defersRecoverableOperationsContextWhileRejectingInvalidDispatchAndRepairArguments()
    {
        var repo = CreateRepository();
        Assert.False(Continues(RunHookRaw(repo, [], "{}")));
        Assert.False(Continues(RunHookRaw(repo, ["start"], "{}")));
        Assert.False(Continues(RunHookRaw(repo, ["start", "unknown"], HookJson(repo, "s", string.Empty))));
        AssertIncidentDeferred(RunHookRaw(repo, ["unknown", "incident"], HookJson(repo, "s", string.Empty)), "unknown", "action-invalid");
        AssertIncidentDeferred(RunHookRaw(repo, ["start", "incident"], "not-json"), "start", "operations-hook-json-invalid");
        AssertIncidentDeferred(RunHookRaw(repo, ["start", "incident"], JsonSerializer.Serialize(new { session_id = "s" })), "start", "cwd-missing");

        var child = Path.Combine(repo, "child");
        Directory.CreateDirectory(child);
        AssertIncidentDeferred(RunHookRaw(repo, ["start", "incident"], HookJson(child, "s", string.Empty)), "start", "cwd-not-exact-git-root");
        AssertIncidentDeferred(RunHook(repo, "incident", "initialize", string.Empty, "empty-prompt"), "initialize", "prompt-missing");
        AssertIncidentDeferred(RunHook(repo, "incident", "initialize", "--incident C:/absolute.md", "absolute"), "initialize", "path-invalid");
        AssertIncidentDeferred(RunHook(repo, "incident", "initialize", "--incident .engloop/postmortems/PM001.md", "wrong-prefix"), "initialize", "path-invalid");
        AssertPostmortemContextRequired(RunHook(repo, "postmortem", "initialize", "--incidents IN001,IN001 --postmortem .engloop/postmortems/PM005.md", "duplicate-incidents"), "initialize", "incident-ids-invalid");
        AssertPostmortemContextRequired(RunHook(repo, "postmortem", "initialize", "--incidents BAD --postmortem .engloop/postmortems/PM005.md", "bad-incident"), "initialize", "incident-ids-invalid");
        Assert.False(Continues(RunHook(repo, "repair", "initialize", "--phase invalid --postmortem .engloop/postmortems/PM005.md --rpi RPI001 --rules RULE:x --acceptance .engloop/repairs/PM005-RPI001.route.json", "bad-phase")));
        Assert.False(Continues(RunHook(repo, "repair", "initialize", "--phase route --postmortem .engloop/postmortems/PM005.md --rpi BAD --rules RULE:x --acceptance .engloop/repairs/PM005-RPI001.route.json", "bad-rpi")));
        Assert.False(Continues(RunHook(repo, "repair", "initialize", "--phase route --postmortem .engloop/postmortems/PM005.md --rpi RPI001 --rules RULE:x,RULE:x --acceptance .engloop/repairs/PM005-RPI001.route.json", "duplicate-rules")));

        File.WriteAllText(Path.Combine(repo, ".engloop", "postmortems", "PM006_exists.md"), "existing");
        Assert.False(Continues(RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM006_exists.md", "existing-pm")));
        Assert.False(Continues(RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/notpm.md", "bad-pm-name")));
        File.WriteAllText(Path.Combine(repo, ".engloop", "repairs", "PM005-RPI001.route.json"), "existing");
        Assert.False(Continues(RunHook(repo, "repair", "initialize", "--phase route --postmortem .engloop/postmortems/PM005.md --rpi RPI001 --rules RULE:x --acceptance .engloop/repairs/PM005-RPI001.route.json", "existing-route")));

        var noIgnore = CreateRepository();
        File.WriteAllText(Path.Combine(noIgnore, ".gitignore"), string.Empty);
        AssertIncidentDeferred(RunHook(noIgnore, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "not-ignored"), "initialize", "gate-root-not-ignored");

        var noManifest = CreateRepository();
        File.Delete(Path.Combine(noManifest, ".config", "dotnet-tools.json"));
        AssertIncidentDeferred(RunHook(noManifest, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "missing-manifest"), "initialize", "tool-manifest-missing");
        var wrongVersion = CreateRepository();
        File.WriteAllText(Path.Combine(wrongVersion, ".config", "dotnet-tools.json"), "{\"version\":1,\"isRoot\":true,\"tools\":{\"engloopkit\":{\"version\":\"9.9.9\",\"commands\":[\"engloopkit\"]}}}");
        AssertIncidentDeferred(RunHook(wrongVersion, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "wrong-version"), "initialize", "manifest-assembly-version-mismatch");

        var nullVersion = CreateRepository();
        File.WriteAllText(Path.Combine(nullVersion, ".config", "dotnet-tools.json"), "{\"version\":1,\"isRoot\":true,\"tools\":{\"engloopkit\":{\"version\":null,\"commands\":[\"engloopkit\"]}}}");
        AssertIncidentDeferred(RunHook(nullVersion, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "null-version-incident"), "initialize", "tool-version-missing");
        var nullVersionPostmortem = RunHook(nullVersion, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005.md", "null-version-postmortem");
        Assert.False(Continues(nullVersionPostmortem));
        Assert.Contains("tool-version-missing", nullVersionPostmortem.Output);

        AssertIncidentDeferred(RunHook(repo, "incident", "stop", string.Empty, "missing-gate"), "stop", "gate-missing");

        var unborn = CreateRepository();
        Git(unborn, "checkout", "--orphan", "unborn");
        Git(unborn, "rm", "-rf", ".");
        File.WriteAllText(Path.Combine(unborn, ".gitignore"), ".engloop/out/\n");
        AssertIncidentDeferred(RunHook(unborn, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "unborn-head"), "initialize", "git-head-unavailable");
    }

    [Fact]
    public void ExistingIncidentGate_defersCorruptIdentityHeadAndJsonWithoutAcceptingIt()
    {
        void RejectMutation(Action<JsonObject> mutate, string expected)
        {
            var repo = CreateRepository();
            Assert.True(Continues(RunHook(repo, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "gate-matrix")));
            var path = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json"));
            var gate = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            mutate(gate);
            File.WriteAllText(path, gate.ToJsonString());
            var result = RunHook(repo, "incident", "initialize", "continue", "gate-matrix");
            AssertIncidentDeferred(result, "initialize", expected);
            Assert.True(File.Exists(path));
        }

        RejectMutation(gate => gate["SchemaVersion"] = "2.0", "existing-gate-stale");
        RejectMutation(gate => gate["Mode"] = "repair", "existing-gate-stale");
        RejectMutation(gate => gate["SessionHash"] = new string('0', 64), "existing-gate-stale");
        RejectMutation(gate => gate["Head"] = new string('0', 40), "existing-gate-stale");

        var corrupt = CreateRepository();
        Assert.True(Continues(RunHook(corrupt, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "corrupt-json")));
        var corruptPath = Assert.Single(Directory.GetFiles(Path.Combine(corrupt, ".engloop", "out", "operations-learning-gates"), "*.json"));
        File.WriteAllText(corruptPath, "{");
        AssertIncidentDeferred(RunHook(corrupt, "incident", "initialize", "continue", "corrupt-json"), "initialize", "operations-hook-json-invalid");
        Assert.True(File.Exists(corruptPath));

        var head = CreateRepository();
        Assert.True(Continues(RunHook(head, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "head-change")));
        File.WriteAllText(Path.Combine(head, "new.txt"), "new");
        Git(head, "add", "new.txt");
        Git(head, "commit", "-m", "new head");
        var headGate = Assert.Single(Directory.GetFiles(Path.Combine(head, ".engloop", "out", "operations-learning-gates"), "*.json"));
        AssertIncidentDeferred(RunHook(head, "incident", "stop", string.Empty, "head-change"), "stop", "head-changed");
        Assert.True(File.Exists(headGate));
    }

    [Fact]
    public void IncidentStop_defersNullGateWhilePostmortemAndRepairRemainFailClosed()
    {
        var nullGate = CreateRepository();
        Assert.True(Continues(RunHook(nullGate, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "null-gate")));
        var nullPath = Assert.Single(Directory.GetFiles(Path.Combine(nullGate, ".engloop", "out", "operations-learning-gates"), "*.json"));
        File.WriteAllText(nullPath, "null");
        AssertIncidentDeferred(RunHook(nullGate, "incident", "stop", string.Empty, "null-gate"), "stop", "gate-json-invalid");
        Assert.True(File.Exists(nullPath));

        var repair = CreateRepository();
        const string repairPrompt = "--phase route --postmortem .engloop/postmortems/PM005.md --rpi RPI001 --rules RULE:x --acceptance .engloop/repairs/PM005-RPI001.route.json";
        Assert.True(Continues(RunHook(repair, "repair", "initialize", repairPrompt, "repair-stop")));
        var repairStop = RunHook(repair, "repair", "stop", string.Empty, "repair-stop");
        Assert.False(Continues(repairStop));
        Assert.Contains("mode=repair", repairStop.Output);

        var postmortem = CreateRepository();
        Assert.True(Continues(RunHook(postmortem, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005.md", "pm-stop-mode")));
        var pmStop = RunHook(postmortem, "postmortem", "stop", string.Empty, "pm-stop-mode");
        Assert.False(Continues(pmStop));
        Assert.Contains("mode=postmortem", pmStop.Output);
    }

    [Fact]
    public void IncidentHook_defersMissingArtifactAndUnavailableGateStorage_withoutFalseAcceptance()
    {
        var missingArtifact = CreateRepository();
        const string missingPath = ".engloop/incidents/IN999_missing.md";
        Assert.True(Continues(RunHook(missingArtifact, "incident", "initialize", $"--incident {missingPath}", "missing-artifact")));
        var gate = Assert.Single(Directory.GetFiles(Path.Combine(missingArtifact, ".engloop", "out", "operations-learning-gates"), "*.json"));

        AssertIncidentDeferred(RunHook(missingArtifact, "incident", "stop", string.Empty, "missing-artifact"), "stop", "incident-context-validation-failed");
        Assert.True(File.Exists(gate));
        Assert.False(OperationsLearningPolicy.ValidateIncidentContext(missingArtifact, missingPath, requireConsulted: false).Passed);

        var unavailableStorage = CreateRepository();
        var outRoot = Path.Combine(unavailableStorage, ".engloop", "out");
        Directory.CreateDirectory(outRoot);
        File.WriteAllText(Path.Combine(outRoot, "operations-learning-gates"), "not a directory");

        AssertIncidentDeferred(
            RunHook(unavailableStorage, "incident", "initialize", "--incident .engloop/incidents/IN001_example.md", "unavailable-storage"),
            "initialize",
            "operations-hook-storage-unavailable");
    }

    private string CreateRepository()
    {
        var repo = Path.Combine(_work, "repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(repo, ".engloop", "postmortems"));
        Directory.CreateDirectory(Path.Combine(repo, ".engloop", "repairs"));
        Directory.CreateDirectory(Path.Combine(repo, ".engloop", "incidents"));
        Directory.CreateDirectory(Path.Combine(repo, ".config"));
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(Path.Combine(repo, ".gitignore"), ".engloop/out/\n");
        File.WriteAllText(Path.Combine(repo, "README.md"), "fixture\n");
        File.WriteAllText(Path.Combine(repo, "NORTHSTAR.md"), "# Direction\n");
        File.WriteAllText(Path.Combine(repo, "LEARNINGS.md"), "# Learnings\n");
        File.WriteAllText(Path.Combine(repo, "src", "fixture.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        File.WriteAllText(Path.Combine(repo, ".engloop", "config.json"), "{\"schemaVersion\":\"2.0\",\"productId\":\"fixture\",\"artifactRoot\":\".engloop\",\"transientOutputRoot\":\".engloop/out\",\"northstarPath\":\"NORTHSTAR.md\",\"validatorCommand\":[\"dotnet\",\"--version\"],\"moduleDiscoveryCommand\":[\"dotnet\",\"--version\"],\"architectureCommand\":[\"dotnet\",\"--version\"],\"regressionCommand\":[\"dotnet\",\"--version\"],\"coverageInputs\":{\"wholeProduct\":\"src/fixture.csproj\"},\"testRunway\":{\"status\":\"proven\",\"framework\":\"xunit\",\"terseCommand\":[\"dotnet\",\"--version\"],\"boundaryTest\":\"Fixture.Boundary\",\"generatedDestination\":\"tests/generated\",\"evidenceDigest\":\"fixture\",\"provenAtRevision\":\"content:fixture\"},\"moduleInventory\":[{\"id\":\"core\",\"path\":\"src/fixture.csproj\"}]}\n");
        File.WriteAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "{\"version\":1,\"isRoot\":true,\"tools\":{\"engloopkit\":{\"version\":\"1.15.4\",\"commands\":[\"engloopkit\"]}}}\n");
        var northstarHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(repo, "NORTHSTAR.md")))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(repo, ".engloop", "incidents", "IN001_example.md"), $"# IN001\n\n- **Status:** STABILIZED\n\n## Verification (stability, not root-cause fix)\n\n- [x] Health checks passing: service health remained continuously green for verification.\n- [x] User workflows unblocked: user workflow completed successfully without any errors.\n- [x] No fresh errors in the watch window: watch window reported zero additional errors.\n\n## Direction and learning context\n\n- **North Star SHA-256:** `{northstarHash}`\n- **Learning context:** `CONSULTED`\n- **Rule IDs:** `NONE`\n- **Source IDs:** `NONE`\n- **Deferral reason:** `NOT-REQUIRED`\n");
        Git(repo, "init");
        Git(repo, "config", "user.email", "operations@example.invalid");
        Git(repo, "config", "user.name", "Operations Test");
        Git(repo, "add", ".");
        Git(repo, "commit", "-m", "fixture");
        return repo;
    }

    private static (int ExitCode, string Output, string Error) RunHook(string repo, string mode, string action, string prompt, string session)
    {
        var input = HookJson(repo, session, prompt);
        return RunHookRaw(repo, [action, mode], input);
    }

    private static string HookJson(string repo, string session, string prompt)
        => JsonSerializer.Serialize(new { cwd = repo, session_id = session, prompt });

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
            var exitCode = OperationsHookCommands.Execute(args);
            return (exitCode, output.ToString().Trim(), error.ToString().Trim());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static (int ExitCode, string Output, string Error) RunHookSubprocess(string repo, string mode, string action, string prompt, string session)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repo,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(ToolDll);
        start.ArgumentList.Add("operations-hook");
        start.ArgumentList.Add(action);
        start.ArgumentList.Add(mode);
        using var process = Process.Start(start)!;
        process.StandardInput.Write(JsonSerializer.Serialize(new { cwd = repo, session_id = session, prompt }));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static bool Continues((int ExitCode, string Output, string Error) result)
    {
        Assert.Equal(0, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        return json.RootElement.GetProperty("continue").GetBoolean();
    }

    private static void AssertIncidentDeferred((int ExitCode, string Output, string Error) result, string phase, string expectedDiagnostic)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("stopReason").ValueKind);
        var message = json.RootElement.GetProperty("systemMessage").GetString();
        Assert.Contains("OPERATIONS_LEARNING_CONTEXT_DEFERRED", message);
        Assert.Contains("\"status\":\"learning-context-deferred\"", message);
        Assert.Contains($"\"phase\":\"{phase}\"", message);
        Assert.Contains($"\"command\":\"operations-hook {phase} incident\"", message);
        Assert.Contains(expectedDiagnostic, message);
        Assert.Contains("\"expectedSource\":", message);
        Assert.Contains("\"elkVersion\":", message);
        Assert.Contains("\"remediation\":", message);
    }

    private static void AssertPostmortemContextRequired((int ExitCode, string Output, string Error) result, string phase, string expectedDiagnostic)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("stopReason").ValueKind);
        var message = json.RootElement.GetProperty("systemMessage").GetString();
        Assert.Contains("OPERATIONS_LEARNING_CONTEXT_REQUIRED", message);
        Assert.Contains("\"status\":\"postmortem-context-required\"", message);
        Assert.Contains($"\"phase\":\"{phase}\"", message);
        Assert.Contains($"\"command\":\"operations-hook {phase} postmortem\"", message);
        Assert.Contains(expectedDiagnostic, message);
        Assert.Contains("\"expectedSource\":", message);
        Assert.Contains("\"elkVersion\":", message);
        Assert.Contains("\"remediation\":", message);
        Assert.Contains("\"completionAccepted\":false", message);
    }

    private static void AssertPostmortemGuardDenied((int ExitCode, string Output, string Error) result, string expectedDiagnostic)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("stopReason").ValueKind);
        Assert.Contains(expectedDiagnostic, json.RootElement.GetProperty("systemMessage").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("POSTMORTEM_LEARNING_OK", result.Output, StringComparison.Ordinal);
        var specific = json.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("PreToolUse", specific.GetProperty("hookEventName").GetString());
        Assert.Equal("deny", specific.GetProperty("permissionDecision").GetString());
    }

    private static void Git(string repo, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output + error);
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
