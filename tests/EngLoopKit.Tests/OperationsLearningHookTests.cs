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
    private static readonly string ToolVersion = typeof(OperationsHookCommands).Assembly.GetName().Version!.ToString(3);
    private readonly string _work = Path.Combine(Path.GetTempPath(), "elk-operations-hooks-" + Guid.NewGuid().ToString("N"));

    public OperationsLearningHookTests() => Directory.CreateDirectory(_work);

    [Fact]
    public void Hook_requiresSessionIdentity_andExplicitPostmortemPath()
    {
        var repo = CreateRepository();
        var noSession = RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md", session: "");
        AssertLifecycleDeferred(noSession, "postmortem", "initialize");
        Assert.Contains("session-id-missing", noSession.Output);

        var missingPath = RunHook(repo, "postmortem", "initialize", "--incidents IN001", session: "pm-session");
        AssertNoLifecycleWarning(missingPath);
    }

    [Fact]
    public void PostmortemMissingContext_keepsChatUsableButDeniesToolsAndAcceptsNoCompletion()
    {
        var repo = CreateRepository();
        const string session = "postmortem-context-recovery";

        var started = RunHook(repo, "postmortem", "start", string.Empty, session);
        Assert.True(Continues(started), started.Output + started.Error);
        using (var startJson = JsonDocument.Parse(started.Output))
        {
            Assert.False(startJson.RootElement.TryGetProperty("systemMessage", out var warning) && warning.ValueKind == JsonValueKind.String);
            var specific = startJson.RootElement.GetProperty("hookSpecificOutput");
            Assert.Equal("SessionStart", specific.GetProperty("hookEventName").GetString());
            Assert.Contains("OPERATIONS_LEARNING_GUARD_ACTIVE", specific.GetProperty("additionalContext").GetString());
        }

        var initialized = RunHook(repo, "postmortem", "initialize", "Continue the retrospective for the stabilized incidents.", session);
        AssertNoLifecycleWarning(initialized);
        var collectionPath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".json");
        Assert.True(File.Exists(collectionPath));

        var guarded = RunHook(repo, "postmortem", "guard", string.Empty, session);
        Assert.True(Continues(guarded), guarded.Output + guarded.Error);
        using (var guardJson = JsonDocument.Parse(guarded.Output))
        {
            var specific = guardJson.RootElement.GetProperty("hookSpecificOutput");
            Assert.Equal("PreToolUse", specific.GetProperty("hookEventName").GetString());
            Assert.Equal("deny", specific.GetProperty("permissionDecision").GetString());
            Assert.Contains("read, search, or vscode_askQuestions", specific.GetProperty("permissionDecisionReason").GetString(), StringComparison.Ordinal);
        }

        var stopped = RunHook(repo, "postmortem", "stop", string.Empty, session);
        AssertNoLifecycleWarning(stopped);
        Assert.DoesNotContain("POSTMORTEM_LEARNING_OK", stopped.Output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(repo, ".engloop", "out", "operations-learning-gates")));
        Assert.True(File.Exists(collectionPath));

        const string validPrompt = "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md";
        var rebound = RunHook(repo, "postmortem", "initialize", validPrompt, session);
        Assert.True(Continues(rebound), rebound.Output + rebound.Error);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", rebound.Output);

        var denied = RunHook(repo, "postmortem", "guard", string.Empty, session);
        AssertPostmortemScopeDenied(denied);
    }

    [Fact]
    public void NaturalLanguagePostmortem_collectsReadOnlyContextAndBindsRegistryBackedProposalAfterConfirmation()
    {
        var repo = CreateRepository();
        const string session = "natural-language-postmortem";

        var initialized = RunHook(repo, "postmortem", "initialize", "Complete the postmortem for the known stencil incident.", session);
        Assert.True(Continues(initialized), initialized.Output + initialized.Error);
        using var initializedJson = JsonDocument.Parse(initialized.Output);
        AssertNoLifecycleWarning(initialized);
        var collectionPath = $".engloop/out/postmortem-context/{Sha256ForTest(session)}.json";
        Assert.True(File.Exists(Path.Combine(repo, collectionPath.Replace('/', Path.DirectorySeparatorChar))));

        AssertPostmortemCollectionDecision(RunGuard(repo, session, "read_file", new { filePath = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md") }), "allow");
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "vscode_askQuestions", new { questions = Array.Empty<object>() }), "allow");
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "apply_patch", new { input = "*** Begin Patch" }), "deny");
        var terminal = RunGuard(repo, session, "run_in_terminal", new { command = "git status --short" });
        AssertPostmortemCollectionDecision(terminal, "deny");
        using (var terminalJson = JsonDocument.Parse(terminal.Output))
        {
            var reason = terminalJson.RootElement.GetProperty("hookSpecificOutput").GetProperty("permissionDecisionReason").GetString()!;
            Assert.Contains("no action ran", reason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Continue with read, search, or vscode_askQuestions", reason, StringComparison.Ordinal);
            Assert.DoesNotContain("provide one confirmation", reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("binder", reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Stage 22", reason, StringComparison.OrdinalIgnoreCase);
        }

        var postmortem = ".engloop/postmortems/PM005-known-incident.md";
        var confirmation = AnswerCollection(repo, session, ["IN001"], postmortem, "Confirm", answerByQuestion: true, autoBind: true);
        AssertPostToolContext(confirmation.Result, "POSTMORTEM_ROUTE_BOUND", "OPERATIONS_LEARNING_SCOPE_ACTIVE");
        Assert.True(File.Exists(Path.Combine(repo, confirmation.ReceiptPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(repo, collectionPath.Replace('/', Path.DirectorySeparatorChar))));

        var editAllowed = RunGuard(repo, session, "apply_patch", new { input = "*** Begin Patch\n*** Add File: " + postmortem + "\n+draft\n*** End Patch" });
        Assert.True(Continues(editAllowed), editAllowed.Output + editAllowed.Error);
        AssertBoundGuardContext(editAllowed.Output, postmortem);

        AssertPostmortemScopeDenied(RunGuard(repo, session, "apply_patch", new
        {
            input = "*** Begin Patch\n*** Update File: verticals/pottery-shop/db/production.sql\n@@\n+production mutation\n*** End Patch",
        }));
        AssertPostmortemScopeDenied(RunGuard(repo, session, "run_in_terminal", new { command = "kubectl apply -f production.yaml" }));

        var stop = RunHook(repo, "postmortem", "stop", string.Empty, session);
        AssertLifecycleDeferred(stop, "postmortem", "stop");
        Assert.Contains("validation failed", stop.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NaturalLanguagePostmortem_IN011Pm055ConfirmBindsAndAuthorizesWorkInSameTurn()
    {
        var repo = CreateRepository();
        var incidentDirectory = Path.Combine(repo, ".engloop", "incidents");
        var oldIncident = Path.Combine(incidentDirectory, "IN001_example.md");
        var incident = Path.Combine(incidentDirectory, "IN011-benji-front-middle-placement-reliability.md");
        File.Move(oldIncident, incident);
        File.WriteAllText(incident, File.ReadAllText(incident).Replace("# IN001", "# IN011", StringComparison.Ordinal));
        var registryPath = Path.Combine(repo, ".engloop", "numbering-registry.md");
        File.WriteAllText(registryPath, File.ReadAllText(registryPath)
            .Replace("`IN001`", "`IN011`", StringComparison.Ordinal)
            .Replace("`PM004`", "`PM054`", StringComparison.Ordinal));
        Git(repo, "add", ".");
        Git(repo, "commit", "-m", "benji incident fixture");
        const string session = "benji-in011-pm055";
        var collection = BeginCollection(repo, session);
        const string postmortem = ".engloop/postmortems/PM055-benji-front-middle-placement-reliability.md";

        var confirmed = AnswerCollection(repo, session, ["IN011"], postmortem,
            "Confirm", answerByQuestion: true, autoBind: true);

        Assert.Contains("POSTMORTEM_ROUTE_BOUND", confirmed.Result.Output);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", confirmed.Result.Output);
        Assert.Contains("incidents=IN011", confirmed.Result.Output);
        Assert.Contains("postmortem=" + postmortem, confirmed.Result.Output);
        Assert.True(File.Exists(Path.Combine(repo, confirmed.ReceiptPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(repo, collection.Path.Replace('/', Path.DirectorySeparatorChar))));
        var allowed = RunGuard(repo, session, "apply_patch", new { input = "*** Add File: " + postmortem });
        Assert.True(Continues(allowed), allowed.Output + allowed.Error);
        AssertBoundGuardContext(allowed.Output, postmortem);
    }

    [Fact]
    public void NaturalLanguagePostmortem_bindingFailureIsCorrelatedRetryableAndStage50Escalatable()
    {
        var repo = CreateRepository();
        const string session = "binding-recovery";
        var collection = BeginCollection(repo, session);
        const string postmortem = ".engloop/postmortems/PM005-binding-recovery.md";
        var registryPath = Path.Combine(repo, ".engloop", "numbering-registry.md");
        var registry = File.ReadAllText(registryPath);
        File.WriteAllText(registryPath, "# invalid registry\n");

        var failed = AnswerCollection(repo, session, ["IN001"], postmortem,
            "Confirm", autoBind: true);

        AssertPostToolContext(failed.Result, "POSTMORTEM_ROUTE_BIND_FAILED", "completionAccepted=false");
        Assert.Contains("supported read/search/question workflow", failed.Result.Output);
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", failed.Result.Output);
        Assert.True(File.Exists(Path.Combine(repo, collection.Path.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Empty(Directory.Exists(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"))
            ? Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json")
            : []);
        var failurePath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".binding-failure.json");
        Assert.True(File.Exists(failurePath));
        var confirmationPath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".confirmation.json");
        Assert.True(File.Exists(confirmationPath));

        File.WriteAllText(registryPath, registry);
        var unrelated = RunPostTool(repo, session, "apply_patch", new { input = "*** Begin Patch" }, new { success = true });
        Assert.True(Continues(unrelated), unrelated.Output + unrelated.Error);
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", unrelated.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(failurePath));
        Assert.True(File.Exists(confirmationPath));

        var readInput = new { filePath = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md") };
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "read_file", readInput), "allow");
        var retried = RunPostTool(repo, session, "read_file", readInput, new { content = "incident read" });
        AssertPostToolContext(retried, "POSTMORTEM_ROUTE_BOUND", "OPERATIONS_LEARNING_SCOPE_ACTIVE");
        Assert.Contains("confirmationReused=true", retried.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(failurePath));
        Assert.False(File.Exists(confirmationPath));
        Assert.False(File.Exists(Path.Combine(repo, collection.Path.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void NaturalLanguagePostmortem_canonicalNumberRegistryBindsIN021ToPM058WithoutStabilizationClaims()
    {
        var repo = CreateRepository();
        var incidentDirectory = Path.Combine(repo, ".engloop", "incidents");
        var oldIncidentPath = Path.Combine(incidentDirectory, "IN001_example.md");
        var incidentPath = Path.Combine(incidentDirectory, "IN021-burnett-text-reprint-size-regression-20260927.md");
        var incidentText = File.ReadAllText(oldIncidentPath)
            .Replace("# IN001", "# IN021", StringComparison.Ordinal)
            .Replace("**Status:** STABILIZED", "**Status:** ACTIVE", StringComparison.Ordinal)
            .Replace("- [x] Health checks passing:", "- [ ] Health checks passing:", StringComparison.Ordinal)
            .Replace("- [x] User workflows unblocked:", "- [ ] User workflows unblocked:", StringComparison.Ordinal)
            .Replace("- [x] No fresh errors in the watch window:", "- [ ] No fresh errors in the watch window:", StringComparison.Ordinal);
        File.Delete(oldIncidentPath);
        File.WriteAllText(incidentPath, incidentText);
        File.WriteAllText(Path.Combine(repo, ".engloop", "numbering-registry.md"),
            "# EngLoop Numbering Registry\n\n| Type | Next number |\n|---|---:|\n| IN | 021 |\n| PM | 058 |\n");
        Git(repo, "add", ".");
        Git(repo, "commit", "-m", "canonical numbering registry fixture");

        const string session = "canonical-registry-in021-pm058";
        var postmortem = ".engloop/postmortems/PM058-20260928-burnett-text-reprint-size-regression.md";
        _ = BeginCollection(repo, session);
        var confirmed = AnswerCollection(repo, session, ["IN021"], postmortem, "Confirm", autoBind: true);

        AssertPostToolContext(confirmed.Result, "POSTMORTEM_ROUTE_BOUND", "OPERATIONS_LEARNING_SCOPE_ACTIVE");
        Assert.Contains("incidents=IN021", confirmed.Result.Output, StringComparison.Ordinal);
        Assert.Contains("postmortem=" + postmortem, confirmed.Result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(repo, confirmed.ReceiptPath.Replace('/', Path.DirectorySeparatorChar))));
        var gatePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.postmortem.json"));
        using (var gate = JsonDocument.Parse(File.ReadAllText(gatePath)))
        {
            Assert.Equal("IN021", gate.RootElement.GetProperty("Incidents")[0].GetString());
            Assert.Equal(postmortem, gate.RootElement.GetProperty("Postmortem").GetString());
        }

        var createAllowed = RunGuard(repo, session, "apply_patch", new
        {
            input = "*** Begin Patch\n*** Add File: " + postmortem + "\n+# PM058: IN021 analysis\n*** End Patch",
        });
        Assert.True(Continues(createAllowed), createAllowed.Output + createAllowed.Error);
        AssertBoundGuardContext(createAllowed.Output, postmortem);
        Assert.False(File.Exists(Path.Combine(repo, postmortem.Replace('/', Path.DirectorySeparatorChar))));

        var strict = OperationsLearningPolicy.ValidateIncidentContext(repo,
            Path.GetRelativePath(repo, incidentPath).Replace('\\', '/'), requireConsulted: true);
        Assert.False(strict.Passed);
        Assert.Contains("incident-not-stabilized", strict.Failures);
        var stopped = RunHook(repo, "postmortem", "stop", string.Empty, session);
        Assert.Contains("validation failed", stopped.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("POSTMORTEM_LEARNING_OK", stopped.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void NaturalLanguagePostmortem_canonicalHeaderFailureResumesSavedConfirmationAfterManifestIdentityChanges()
    {
        var repo = CreateRepository();
        var registryPath = Path.Combine(repo, ".engloop", "numbering-registry.md");
        File.WriteAllText(registryPath,
            "# EngLoop Numbering Registry\n\n| Type | Next counter |\n|---|---:|\n| IN | 001 |\n| PM | 005 |\n");
        Git(repo, "add", ".engloop/numbering-registry.md");
        Git(repo, "commit", "-m", "malformed canonical registry header fixture");
        const string session = "canonical-header-recovery-upgrade";
        var postmortem = ".engloop/postmortems/PM005-header-recovery.md";
        var collection = BeginCollection(repo, session);

        var failed = AnswerCollection(repo, session, ["IN001"], postmortem, "Confirm", autoBind: true);

        AssertPostToolContext(failed.Result, "POSTMORTEM_ROUTE_BIND_FAILED", "postmortem-route-numbering-header-invalid");
        using (var failedJson = JsonDocument.Parse(failed.Result.Output))
        {
            var context = failedJson.RootElement.GetProperty("additionalContext").GetString()!;
            Assert.Contains("expected='| Type | Next number |'", context, StringComparison.Ordinal);
            Assert.Contains("actual='| Type | Next counter |'", context, StringComparison.Ordinal);
        }
        var confirmationPath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".confirmation.json");
        var failurePath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".binding-failure.json");
        Assert.True(File.Exists(confirmationPath));
        Assert.True(File.Exists(failurePath));

        File.WriteAllText(registryPath,
            "# EngLoop Numbering Registry\n\n| Type | Next number |\n|---|---:|\n| IN | 001 |\n| PM | 005 |\n");
        File.WriteAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), JsonSerializer.Serialize(new
        {
            version = 1,
            isRoot = true,
            tools = new { engloopkit = new { version = ToolVersion, commands = new[] { "engloopkit" } } },
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        var readInput = new { filePath = Path.Combine(repo, ".engloop", "numbering-registry.md") };
        var resumed = RunPostTool(repo, session, "read_file", readInput, new { content = File.ReadAllText(registryPath) });

        AssertPostToolContext(resumed, "POSTMORTEM_ROUTE_BOUND", "OPERATIONS_LEARNING_SCOPE_ACTIVE", "confirmationReused=true");
        Assert.False(File.Exists(confirmationPath));
        Assert.False(File.Exists(failurePath));
        Assert.False(File.Exists(Path.Combine(repo, collection.Path.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.postmortem.json"));
    }

    [Fact]
    public void NaturalLanguagePostmortem_delegatedLifecycleRetainsUnresolvedStateWithoutBlockingNavigation()
    {
        var repo = CreateRepository();
        const string session = "delegated-natural-language";
        var started = RunHook(repo, "postmortem", "subagent-start", string.Empty, session);
        Assert.True(Continues(started), started.Output + started.Error);
        using (var startedJson = JsonDocument.Parse(started.Output))
        {
            Assert.Contains("OPERATIONS_LEARNING_GUARD_ACTIVE", startedJson.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString());
        }

        var collection = BeginCollection(repo, session);
        AssertSubagentStopDeferred(RunHook(repo, "postmortem", "subagent-stop", string.Empty, session), "confirmation");

        var cancelled = AnswerCollection(repo, session, ["IN001"], ".engloop/postmortems/PM005-cancelled.md", "Cancel");
        Assert.Contains("POSTMORTEM_ROUTE_CANCELLED", cancelled.Result.Output);
        var completedCancellation = RunHook(repo, "postmortem", "subagent-stop", string.Empty, session);
        Assert.True(Continues(completedCancellation), completedCancellation.Output + completedCancellation.Error);
        using var cancellationJson = JsonDocument.Parse(completedCancellation.Output);
        Assert.Contains("POSTMORTEM_ROUTE_CANCELLED", cancellationJson.RootElement.GetProperty("systemMessage").GetString());
        Assert.False(cancellationJson.RootElement.TryGetProperty("hookSpecificOutput", out _));

        var invalidRepo = CreateRepository();
        const string invalidSession = "delegated-invalid-postmortem";
        _ = BeginCollection(invalidRepo, invalidSession);
        var invalidPostmortem = ".engloop/postmortems/PM005-invalid.md";
        var invalidConfirmation = AnswerCollection(invalidRepo, invalidSession, ["IN001"], invalidPostmortem, "Confirm", autoBind: true);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", invalidConfirmation.Result.Output);
        File.WriteAllText(Path.Combine(invalidRepo, ".engloop", "postmortems", "PM005-invalid.md"), "# invalid PM\n");
        AssertSubagentStopDeferred(RunHook(invalidRepo, "postmortem", "subagent-stop", string.Empty, invalidSession), "validation failed");
    }

    [Fact]
    public void NaturalLanguagePostmortemBinder_allowsAnalysisForUnstabilizedIncidentButCompletionRemainsGated()
    {
        var activeRepo = CreateRepository();
        var activeIncidentPath = Path.Combine(activeRepo, ".engloop", "incidents", "IN001_example.md");
        File.WriteAllText(activeIncidentPath, File.ReadAllText(activeIncidentPath)
            .Replace("**Status:** STABILIZED", "**Status:** ACTIVE", StringComparison.Ordinal)
            .Replace("- [x] Health checks passing:", "- [ ] Health checks passing:", StringComparison.Ordinal)
            .Replace("- [x] User workflows unblocked:", "- [ ] User workflows unblocked:", StringComparison.Ordinal)
            .Replace("- [x] No fresh errors in the watch window:", "- [ ] No fresh errors in the watch window:", StringComparison.Ordinal));
        var active = BeginCollection(activeRepo, "active-natural-language");
        var activePostmortem = ".engloop/postmortems/PM005-active.md";
        var activeConfirmation = AnswerCollection(activeRepo, "active-natural-language", ["IN001"], activePostmortem, "Confirm", autoBind: true);
        AssertPostToolContext(activeConfirmation.Result, "POSTMORTEM_ROUTE_BOUND", "OPERATIONS_LEARNING_SCOPE_ACTIVE");
        Assert.True(File.Exists(Path.Combine(activeRepo, ".engloop", "out", "operations-learning-gates", Sha256ForTest("active-natural-language") + ".postmortem.json"))
            || Directory.GetFiles(Path.Combine(activeRepo, ".engloop", "out", "operations-learning-gates"), "*.json").Length == 1);
        var strictIncident = OperationsLearningPolicy.ValidateIncidentContext(activeRepo, ".engloop/incidents/IN001_example.md", requireConsulted: true);
        Assert.False(strictIncident.Passed);
        Assert.Contains("incident-not-stabilized", strictIncident.Failures);
        Assert.Contains(strictIncident.Failures, failure => failure.StartsWith("incident-stability-check-invalid:", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(activeRepo, activePostmortem.Replace('/', Path.DirectorySeparatorChar))));
        var stopped = RunHook(activeRepo, "postmortem", "stop", string.Empty, "active-natural-language");
        Assert.Contains("validation failed", stopped.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("POSTMORTEM_LEARNING_OK", stopped.Output, StringComparison.Ordinal);

        var wrongNumberRepo = CreateRepository();
        var wrongNumber = BeginCollection(wrongNumberRepo, "wrong-number-natural-language");
        var wrongNumberPostmortem = ".engloop/postmortems/PM006-wrong.md";
        var wrongNumberConfirmation = AnswerCollection(wrongNumberRepo, "wrong-number-natural-language", ["IN001"], wrongNumberPostmortem, "Confirm");
        var wrongNumberResult = RunPostmortemRoute(wrongNumberRepo, ["bind", "--collection", wrongNumber.Path, "--token", wrongNumber.Token, "--incidents", "IN001", "--postmortem", wrongNumberPostmortem, "--confirmation-receipt", wrongNumberConfirmation.ReceiptPath]);
        Assert.NotEqual(0, wrongNumberResult.ExitCode);
        Assert.Contains("next", wrongNumberResult.Error, StringComparison.OrdinalIgnoreCase);

        var wrongTokenRepo = CreateRepository();
        var wrongToken = BeginCollection(wrongTokenRepo, "wrong-token-natural-language");
        var wrongTokenPostmortem = ".engloop/postmortems/PM005-token.md";
        var wrongTokenConfirmation = AnswerCollection(wrongTokenRepo, "wrong-token-natural-language", ["IN001"], wrongTokenPostmortem, "Confirm");
        var wrongTokenResult = RunPostmortemRoute(wrongTokenRepo, ["bind", "--collection", wrongToken.Path, "--token", "wrong", "--incidents", "IN001", "--postmortem", wrongTokenPostmortem, "--confirmation-receipt", wrongTokenConfirmation.ReceiptPath]);
        Assert.NotEqual(0, wrongTokenResult.ExitCode);
        Assert.Contains("token", wrongTokenResult.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NaturalLanguagePostmortem_confirmationRevisionAndMalformedResponseNeverAuthorize()
    {
        var repo = CreateRepository();
        const string session = "revision-natural-language";
        var collection = BeginCollection(repo, session);
        var revised = AnswerCollection(repo, session, ["IN001"], ".engloop/postmortems/PM005-revised.md", "Choose different incident/path");
        Assert.Contains("POSTMORTEM_ROUTE_REVISION_REQUESTED", revised.Result.Output);
        Assert.True(File.Exists(Path.Combine(repo, collection.Path.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(string.Empty, revised.ReceiptPath);
        AssertSubagentStopDeferred(RunHook(repo, "postmortem", "subagent-stop", string.Empty, session), "confirmation");

        var malformed = RunHookRaw(repo, ["post-tool", "postmortem"], JsonSerializer.Serialize(new
        {
            cwd = repo,
            session_id = session,
            tool_name = "vscode_askQuestions",
            tool_input = new { questions = Array.Empty<object>() },
            tool_response = new { answers = new { } },
            tool_use_id = "malformed-question",
        }));
        Assert.True(Continues(malformed), malformed.Output + malformed.Error);
        AssertLifecycleDeferred(malformed, "postmortem", "post-tool");
        Assert.Contains("OPERATIONS_POSTMORTEM_CONFIRMATION_UNVERIFIED", malformed.Output);
        Assert.False(File.Exists(Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".confirmation.json")));
    }

    [Fact]
    public void NaturalLanguagePostmortem_collectionIdentityCorruptionDeniesAndPostToolIgnoresUnrelatedTools()
    {
        var noCollection = CreateRepository();
        var noCollectionRead = RunPostTool(noCollection, "no-collection", "read_file", new { }, new { });
        Assert.True(Continues(noCollectionRead), noCollectionRead.Output + noCollectionRead.Error);
        var noCollectionQuestion = RunPostTool(noCollection, "no-collection", "vscode_askQuestions", new { }, new { });
        Assert.True(Continues(noCollectionQuestion), noCollectionQuestion.Output + noCollectionQuestion.Error);

        var readRepo = CreateRepository();
        _ = BeginCollection(readRepo, "post-tool-read");
        var readPostTool = RunPostTool(readRepo, "post-tool-read", "read_file", new { }, new { });
        Assert.True(Continues(readPostTool), readPostTool.Output + readPostTool.Error);

        void DenyMutation(string field, JsonNode? value, string expected)
        {
            var repo = CreateRepository();
            var session = "collection-mutation-" + field.ToLowerInvariant();
            var collection = BeginCollection(repo, session);
            var full = Path.Combine(repo, collection.Path.Replace('/', Path.DirectorySeparatorChar));
            var state = JsonNode.Parse(File.ReadAllText(full))!.AsObject();
            state[field] = value;
            File.WriteAllText(full, state.ToJsonString());
            var guarded = RunGuard(repo, session, "read_file", new { filePath = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md") });
            AssertPostmortemCollectionDecision(guarded, "allow", allowDiagnostic: true);
            Assert.Contains(expected, guarded.Output, StringComparison.OrdinalIgnoreCase);
            AssertPostmortemCollectionDecision(RunGuard(repo, session, "apply_patch", new { }), "deny", allowDiagnostic: true);
            Assert.True(File.Exists(full));
        }

        DenyMutation("SchemaVersion", "2.0", "schema-invalid");
        DenyMutation("SessionHash", "bad", "session-invalid");
        DenyMutation("Token", "bad", "token-invalid");
        DenyMutation("Head", new string('0', 40), "head-changed");
        DenyMutation("AssemblyPath", "wrong", "assembly-path");
        DenyMutation("AssemblySha256", new string('0', 64), "assembly-hash");
        DenyMutation("ToolVersion", "0.0.0", "tool-version");
        DenyMutation("ManifestPath", "wrong", "collection-tool-identity-invalid");
        DenyMutation("ManifestSha256", new string('0', 64), "manifest-hash");

        var corruptRepo = CreateRepository();
        var corrupt = BeginCollection(corruptRepo, "corrupt-collection");
        var corruptFull = Path.Combine(corruptRepo, corrupt.Path.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(corruptFull, "{");
        var corruptRead = RunGuard(corruptRepo, "corrupt-collection", "read_file", new { });
        AssertPostmortemCollectionDecision(corruptRead, "allow", allowDiagnostic: true);
        Assert.Contains("operations-hook-json-invalid", corruptRead.Output, StringComparison.Ordinal);

        var noScopeDirect = CreateRepository();
        AssertNoLifecycleWarning(RunHook(noScopeDirect, "postmortem", "stop", string.Empty, "no-scope-direct"));
        AssertSubagentStopDeferred(RunHook(noScopeDirect, "postmortem", "subagent-stop", string.Empty, "no-scope-subagent"), "no validated postmortem scope");
        AssertSubagentStopDeferred(RunHookRaw(noScopeDirect, ["subagent-stop", "postmortem"], "not-json"), "operations-hook-json-invalid");
    }

    [Fact]
    public void NaturalLanguagePostmortem_confirmationHookRejectsMalformedEnvelopesAndAcceptsFlattenedHostJson()
    {
        var repo = CreateRepository();
        const string session = "malformed-confirmation";
        _ = BeginCollection(repo, session);
        var receiptPath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".confirmation.json");
        var validQuestion = ConfirmationQuestion(["IN001"], ".engloop/postmortems/PM005-confirm.md");
        var questionText = validQuestion["questions"]![0]!["question"]!.GetValue<string>();
        var validResponse = ConfirmationResponse(questionText, "Confirm");

        void Reject(object input, object response, string expected, string? toolUseId = null)
        {
            if (File.Exists(receiptPath)) File.Delete(receiptPath);
            var result = RunPostTool(repo, session, "vscode_askQuestions", input, response, toolUseId);
            Assert.True(Continues(result), result.Output + result.Error);
            Assert.Contains("OPERATIONS_POSTMORTEM_CONFIRMATION_UNVERIFIED", result.Output);
            Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(receiptPath));
        }

        Reject(validQuestion, validResponse, "tool-use-id-missing", string.Empty);
        Reject("not-json", validResponse, "input-missing");
        Reject(new JsonArray(), validResponse, "questions-invalid");
        Reject(new JsonObject { ["questions"] = new JsonArray() }, validResponse, "question-count-invalid");

        var freeform = ConfirmationQuestion(["IN001"], ".engloop/postmortems/PM005-confirm.md");
        freeform["questions"]![0]!["allowFreeformInput"] = true;
        Reject(freeform, validResponse, "freeform-forbidden");
        var multi = ConfirmationQuestion(["IN001"], ".engloop/postmortems/PM005-confirm.md");
        multi["questions"]![0]!["multiSelect"] = true;
        Reject(multi, validResponse, "multiselect-forbidden");
        var labels = ConfirmationQuestion(["IN001"], ".engloop/postmortems/PM005-confirm.md");
        labels["questions"]![0]!["options"]![0]!["label"] = "Proceed";
        Reject(labels, validResponse, "options-invalid");
        var question = ConfirmationQuestion(["IN001"], ".engloop/postmortems/PM005-confirm.md");
        question["questions"]![0]!["question"] = "Continue?";
        Reject(question, validResponse, "question-invalid");

        Reject(validQuestion, new JsonObject(), "answer-invalid");
        Reject(validQuestion, new JsonObject { ["answers"] = new JsonObject() }, "answer-missing");
        Reject(validQuestion, ConfirmationResponse(questionText, "Confirm", "Cancel"), "selection-invalid");
        Reject(validQuestion, ConfirmationResponse(questionText, "Other"), "decision-invalid");
        Reject(42, validResponse, "input-missing");
        Reject(validQuestion, 42, "response-missing");
        Reject(validQuestion, new JsonObject
        {
            ["answers"] = new JsonObject
            {
                [questionText] = new JsonObject { ["selected"] = new JsonArray((JsonNode?)null) },
            },
        }, "decision-invalid");

        var flattened = RunPostTool(repo, session, "vscode_askQuestions", JsonSerializer.Serialize(validQuestion), JsonSerializer.Serialize(validResponse));
        Assert.True(Continues(flattened), flattened.Output + flattened.Error);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", flattened.Output);
        Assert.False(File.Exists(receiptPath));
        Assert.True(File.Exists(Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".binding.json")));
    }

    [Fact]
    public void NaturalLanguagePostmortem_emptyConfirmationResponseIsNotTreatedAsDeclineAndOffersRetry()
    {
        var repo = CreateRepository();
        const string session = "empty-confirmation-response";
        _ = BeginCollection(repo, session);
        var question = ConfirmationQuestion(["IN001"], ".engloop/postmortems/PM005-empty-response.md");
        var questionText = question["questions"]![0]!["question"]!.GetValue<string>();
        var response = new
        {
            answers = new Dictionary<string, object>
            {
                [questionText] = new { selected = Array.Empty<string>(), fallback = "User unavailable; will review later." },
            },
        };

        var result = RunPostTool(repo, session, "vscode_askQuestions", question, response);

        Assert.True(Continues(result), result.Output + result.Error);
        Assert.Contains("OPERATIONS_POSTMORTEM_CONFIRMATION_UNVERIFIED", result.Output);
        using var json = JsonDocument.Parse(result.Output);
        var message = json.RootElement.GetProperty("systemMessage").GetString()!;
        using var details = JsonDocument.Parse(message[message.IndexOf('{')..]);
        var diagnostic = details.RootElement;
        Assert.False(diagnostic.GetProperty("completionAccepted").GetBoolean());
        Assert.Contains("not an operator decline", diagnostic.GetProperty("remediation").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operator declined", diagnostic.GetProperty("remediation").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retry the prompt once", diagnostic.GetProperty("remediation").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".confirmation.json")));
        Assert.False(File.Exists(Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".binding.json")));

        var stopped = RunHook(repo, "postmortem", "stop", string.Empty, session);
        Assert.True(Continues(stopped), stopped.Output + stopped.Error);
        using var stoppedJson = JsonDocument.Parse(stopped.Output);
        Assert.Single(stoppedJson.RootElement.EnumerateObject());
        Assert.True(stoppedJson.RootElement.GetProperty("continue").GetBoolean());
    }

    [Fact]
    public void NaturalLanguagePostmortemBinder_rejectsAmbiguousMissingRegistryTamperedReceiptAndReplay()
    {
        var ambiguousRepo = CreateRepository();
        File.Copy(Path.Combine(ambiguousRepo, ".engloop", "incidents", "IN001_example.md"), Path.Combine(ambiguousRepo, ".engloop", "incidents", "IN001_duplicate.md"));
        var ambiguous = BeginCollection(ambiguousRepo, "ambiguous-natural-language");
        var ambiguousPm = ".engloop/postmortems/PM005-ambiguous.md";
        var ambiguousConfirmation = AnswerCollection(ambiguousRepo, "ambiguous-natural-language", ["IN001"], ambiguousPm, "Confirm");
        var ambiguousResult = RunPostmortemRoute(ambiguousRepo, ["bind", "--collection", ambiguous.Path, "--token", ambiguous.Token, "--incidents", "IN001", "--postmortem", ambiguousPm, "--confirmation-receipt", ambiguousConfirmation.ReceiptPath]);
        Assert.NotEqual(0, ambiguousResult.ExitCode);
        Assert.Contains("not-unique", ambiguousResult.Error);

        var registryRepo = CreateRepository();
        var registry = BeginCollection(registryRepo, "registry-natural-language");
        var registryPm = ".engloop/postmortems/PM005-registry.md";
        var registryConfirmation = AnswerCollection(registryRepo, "registry-natural-language", ["IN001"], registryPm, "Confirm");
        File.Delete(Path.Combine(registryRepo, ".engloop", "numbering-registry.md"));
        var registryResult = RunPostmortemRoute(registryRepo, ["bind", "--collection", registry.Path, "--token", registry.Token, "--incidents", "IN001", "--postmortem", registryPm, "--confirmation-receipt", registryConfirmation.ReceiptPath]);
        Assert.NotEqual(0, registryResult.ExitCode);
        Assert.Contains("registry-missing", registryResult.Error);

        var tamperedRepo = CreateRepository();
        var tampered = BeginCollection(tamperedRepo, "tampered-natural-language");
        var confirmedPm = ".engloop/postmortems/PM005-confirmed.md";
        var tamperedConfirmation = AnswerCollection(tamperedRepo, "tampered-natural-language", ["IN001"], confirmedPm, "Confirm");
        var mismatchResult = RunPostmortemRoute(tamperedRepo, ["bind", "--collection", tampered.Path, "--token", tampered.Token, "--incidents", "IN001", "--postmortem", ".engloop/postmortems/PM005-different.md", "--confirmation-receipt", tamperedConfirmation.ReceiptPath]);
        Assert.NotEqual(0, mismatchResult.ExitCode);
        Assert.Contains("postmortem-mismatch", mismatchResult.Error);

        var receiptFull = Path.Combine(tamperedRepo, tamperedConfirmation.ReceiptPath.Replace('/', Path.DirectorySeparatorChar));
        var receipt = JsonNode.Parse(File.ReadAllText(receiptFull))!.AsObject();
        receipt["ToolUseId"] = string.Empty;
        File.WriteAllText(receiptFull, receipt.ToJsonString());
        var tamperedResult = RunPostmortemRoute(tamperedRepo, ["bind", "--collection", tampered.Path, "--token", tampered.Token, "--incidents", "IN001", "--postmortem", confirmedPm, "--confirmation-receipt", tamperedConfirmation.ReceiptPath]);
        Assert.NotEqual(0, tamperedResult.ExitCode);
        Assert.Contains("tool-use-id", tamperedResult.Error);

        var replayRepo = CreateRepository();
        var replay = BeginCollection(replayRepo, "replay-natural-language");
        var replayPm = ".engloop/postmortems/PM005-replay.md";
        var replayConfirmation = AnswerCollection(replayRepo, "replay-natural-language", ["IN001"], replayPm, "Confirm");
        var first = RunPostmortemRoute(replayRepo, ["bind", "--collection", replay.Path, "--token", replay.Token, "--incidents", "IN001", "--postmortem", replayPm, "--confirmation-receipt", replayConfirmation.ReceiptPath]);
        Assert.Equal(0, first.ExitCode);
        var second = RunPostmortemRoute(replayRepo, ["bind", "--collection", replay.Path, "--token", replay.Token, "--incidents", "IN001", "--postmortem", replayPm, "--confirmation-receipt", replayConfirmation.ReceiptPath]);
        Assert.NotEqual(0, second.ExitCode);
        Assert.Contains("collection-missing", second.Error);
    }

    [Fact]
    public void NaturalLanguagePostmortemBinder_rejectsEveryTamperedConfirmationIdentityField()
    {
        var repo = CreateRepository();
        const string session = "confirmation-identity-matrix";
        var collection = BeginCollection(repo, session);
        var postmortem = ".engloop/postmortems/PM005-confirmation-matrix.md";
        var confirmation = AnswerCollection(repo, session, ["IN001"], postmortem, "Confirm");
        var receiptFull = Path.Combine(repo, confirmation.ReceiptPath.Replace('/', Path.DirectorySeparatorChar));
        var original = JsonNode.Parse(File.ReadAllText(receiptFull))!.AsObject();

        void Reject(string field, JsonNode? value, string expected)
        {
            var mutated = original.DeepClone().AsObject();
            mutated[field] = value;
            File.WriteAllText(receiptFull, mutated.ToJsonString());
            var result = RunPostmortemRoute(repo, ["bind", "--collection", collection.Path, "--token", collection.Token, "--incidents", "IN001", "--postmortem", postmortem, "--confirmation-receipt", confirmation.ReceiptPath]);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(expected, result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(receiptFull));
        }

        Reject("SchemaVersion", "2.0", "schema-invalid");
        Reject("SessionHash", new string('0', 64), "session-mismatch");
        Reject("Head", new string('0', 40), "head-mismatch");
        Reject("AssemblySha256", new string('0', 64), "assembly-changed");
        Reject("ManifestSha256", new string('0', 64), "manifest-changed");
        Reject("CollectionToken", "wrong", "token-mismatch");
        Reject("ToolUseId", string.Empty, "tool-use-id-missing");
        Reject("QuestionSha256", "bad", "question-hash-invalid");
        Reject("ResponseSha256", "bad", "response-hash-invalid");
        Reject("Incidents", new JsonArray("IN002"), "incidents-mismatch");
        Reject("Postmortem", ".engloop/postmortems/PM005-other.md", "postmortem-mismatch");

        File.WriteAllText(receiptFull, "null");
        var nullResult = RunPostmortemRoute(repo, ["bind", "--collection", collection.Path, "--token", collection.Token, "--incidents", "IN001", "--postmortem", postmortem, "--confirmation-receipt", confirmation.ReceiptPath]);
        Assert.NotEqual(0, nullResult.ExitCode);
        Assert.Contains("confirmation-json-invalid", nullResult.Error);
    }

    [Fact]
    public void NaturalLanguagePostmortemBinder_rejectsMalformedRegistryMissingOptionsAndShellComposition()
    {
        (string Repo, (string Path, string Token) Collection, (int ExitCode, string Output, string Error) Confirmation, string Receipt, string Postmortem) Setup(string session)
        {
            var repo = CreateRepository();
            var collection = BeginCollection(repo, session);
            var postmortem = ".engloop/postmortems/PM005-" + session + ".md";
            var confirmation = AnswerCollection(repo, session, ["IN001"], postmortem, "Confirm");
            return (repo, collection, confirmation.Result, confirmation.ReceiptPath, postmortem);
        }

        void RejectRegistry(string registry, string expected, string session)
        {
            var setup = Setup(session);
            File.WriteAllText(Path.Combine(setup.Repo, ".engloop", "numbering-registry.md"), registry);
            var result = RunPostmortemRoute(setup.Repo, ["bind", "--collection", setup.Collection.Path, "--token", setup.Collection.Token, "--incidents", "IN001", "--postmortem", setup.Postmortem, "--confirmation-receipt", setup.Receipt]);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(expected, result.Error, StringComparison.OrdinalIgnoreCase);
        }

        RejectRegistry("# none\n", "header-invalid", "registry-header");
        RejectRegistry("| Prefix | Last used |\n|---|---:|\n| `IN` | `IN001` |\n", "row-invalid", "registry-row");
        RejectRegistry("| Prefix | Last used |\n|---|---:|\n| `PM` | `BAD` |\n", "value-invalid", "registry-value");
        RejectRegistry("| Prefix | Last used |\n|---|---:|\n| `PM` | `PM999` |\n", "value-invalid", "registry-exhausted");
        RejectRegistry("| Prefix | Last used |\n|---|---:|\n| `PM` | `PM004` |\n| `PM` | `PM004` |\n", "row-invalid", "registry-duplicate");

        var options = Setup("missing-options");
        foreach (var missing in new[] { "--collection", "--token", "--incidents", "--postmortem", "--confirmation-receipt" })
        {
            var args = new List<string> { "bind", "--collection", options.Collection.Path, "--token", options.Collection.Token, "--incidents", "IN001", "--postmortem", options.Postmortem, "--confirmation-receipt", options.Receipt };
            var index = args.IndexOf(missing);
            args.RemoveAt(index + 1);
            args.RemoveAt(index);
            var result = RunPostmortemRoute(options.Repo, args.ToArray());
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("option-missing:" + missing, result.Error);
        }
        var missingValue = RunPostmortemRoute(options.Repo, ["bind", "--collection", "--token", options.Collection.Token]);
        Assert.NotEqual(0, missingValue.ExitCode);
        Assert.Contains("option-missing:--collection", missingValue.Error);

        var unknown = RunPostmortemRoute(options.Repo, []);
        Assert.NotEqual(0, unknown.ExitCode);
        Assert.Contains("Usage:", unknown.Error);
        var unknownName = RunPostmortemRoute(options.Repo, ["unknown"]);
        Assert.NotEqual(0, unknownName.ExitCode);
        Assert.Contains("Usage:", unknownName.Error);

        var injection = BeginCollection(options.Repo, "shell-injection");
        var command = $"dotnet tool run engloopkit -- postmortem-route bind --collection {injection.Path} --token {injection.Token} --incidents IN001 --postmortem .engloop/postmortems/PM005-injection.md --confirmation-receipt .engloop/out/postmortem-context/{Sha256ForTest("shell-injection")}.confirmation.json; git status";
        AssertPostmortemCollectionDecision(RunGuard(options.Repo, "shell-injection", "run_in_terminal", new { command }), "deny");
        AssertPostmortemCollectionDecision(RunGuard(options.Repo, "shell-injection", "custom_run_anything", new { command = "anything" }), "deny");
    }

    [Fact]
    public void NaturalLanguagePostmortem_collectionLifecycleCleansStaleStateAndSupportsCamelCaseHostEnvelope()
    {
        var repo = CreateRepository();
        const string session = "collection-lifecycle";
        var first = BeginCollection(repo, session);
        var postmortem = ".engloop/postmortems/PM005-lifecycle.md";
        var confirmation = AnswerCollection(repo, session, ["IN001"], postmortem, "Confirm");
        var confirmationFull = Path.Combine(repo, confirmation.ReceiptPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(confirmationFull));

        var second = BeginCollection(repo, session);
        Assert.NotEqual(first.Token, second.Token);
        Assert.False(File.Exists(confirmationFull));

        _ = AnswerCollection(repo, session, ["IN001"], postmortem, "Cancel");
        var cancelledFull = Path.Combine(repo, second.Path.Replace('/', Path.DirectorySeparatorChar)) + ".cancelled";
        Assert.True(File.Exists(cancelledFull));
        var third = BeginCollection(repo, session);
        Assert.False(File.Exists(cancelledFull));
        Assert.NotEqual(second.Token, third.Token);

        var question = ConfirmationQuestion(["IN001"], postmortem);
        var questionText = question["questions"]![0]!["question"]!.GetValue<string>();
        var response = ConfirmationResponse(questionText, "Confirm");
        var camel = RunHookRaw(repo, ["post-tool", "postmortem"], JsonSerializer.Serialize(new
        {
            cwd = repo,
            sessionId = session,
            toolName = "vscode_askQuestions",
            toolInput = JsonSerializer.Serialize(question),
            toolResponse = JsonSerializer.Serialize(response),
            toolUseId = "camel-question",
        }));
        Assert.True(Continues(camel), camel.Output + camel.Error);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", camel.Output);
    }

    [Fact]
    public void NaturalLanguagePostmortem_routeGuardsMissingReceiptWrongPathExistingGateAndSuspendedSubagent()
    {
        var receiptRepo = CreateRepository();
        const string receiptSession = "route-guard-receipt";
        var collection = BeginCollection(receiptRepo, receiptSession);
        var postmortem = ".engloop/postmortems/PM005-receipt.md";
        var confirmation = AnswerCollection(receiptRepo, receiptSession, ["IN001"], postmortem, "Confirm");
        var missingReceipt = RunPostmortemRoute(receiptRepo, ["bind", "--collection", collection.Path, "--token", collection.Token, "--incidents", "IN001", "--postmortem", postmortem, "--confirmation-receipt", ".engloop/out/postmortem-context/" + new string('0', 64) + ".confirmation.json"]);
        Assert.NotEqual(0, missingReceipt.ExitCode);
        Assert.Contains("confirmation-path-mismatch", missingReceipt.Error);
        File.Delete(Path.Combine(receiptRepo, confirmation.ReceiptPath.Replace('/', Path.DirectorySeparatorChar)));
        var absentReceipt = RunPostmortemRoute(receiptRepo, ["bind", "--collection", collection.Path, "--token", collection.Token, "--incidents", "IN001", "--postmortem", postmortem, "--confirmation-receipt", confirmation.ReceiptPath]);
        Assert.NotEqual(0, absentReceipt.ExitCode);
        Assert.Contains("confirmation-missing", absentReceipt.Error);

        var collisionRepo = CreateRepository();
        const string collisionSession = "route-guard-collision";
        var collisionCollection = BeginCollection(collisionRepo, collisionSession);
        var collisionPm = ".engloop/postmortems/PM005-collision.md";
        var collisionConfirmation = AnswerCollection(collisionRepo, collisionSession, ["IN001"], collisionPm, "Confirm");
        var collisionCollectionFull = Path.Combine(collisionRepo, collisionCollection.Path.Replace('/', Path.DirectorySeparatorChar));
        var collisionReceiptFull = Path.Combine(collisionRepo, collisionConfirmation.ReceiptPath.Replace('/', Path.DirectorySeparatorChar));
        var savedCollection = File.ReadAllText(collisionCollectionFull);
        var savedReceipt = File.ReadAllText(collisionReceiptFull);
        var firstBind = RunPostmortemRoute(collisionRepo, ["bind", "--collection", collisionCollection.Path, "--token", collisionCollection.Token, "--incidents", "IN001", "--postmortem", collisionPm, "--confirmation-receipt", collisionConfirmation.ReceiptPath]);
        Assert.Equal(0, firstBind.ExitCode);
        File.WriteAllText(collisionCollectionFull, savedCollection);
        File.WriteAllText(collisionReceiptFull, savedReceipt);
        var collision = RunPostmortemRoute(collisionRepo, ["bind", "--collection", collisionCollection.Path, "--token", collisionCollection.Token, "--incidents", "IN001", "--postmortem", collisionPm, "--confirmation-receipt", collisionConfirmation.ReceiptPath]);
        Assert.NotEqual(0, collision.ExitCode);
        Assert.Contains("gate-already-exists", collision.Error);

        var suspendedRepo = CreateRepository();
        const string suspendedSession = "subagent-suspended-context";
        Assert.True(Continues(RunHook(suspendedRepo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005-suspended.md", suspendedSession)));
        AssertPostmortemContextRequired(RunHook(suspendedRepo, "postmortem", "initialize", "--incidents IN001", suspendedSession), "initialize", "--postmortem");
        AssertSubagentStopDeferred(RunHook(suspendedRepo, "postmortem", "subagent-stop", string.Empty, suspendedSession), "context is incomplete");
    }

    [Fact]
    public void NaturalLanguagePostmortem_collectionReadFailuresAllowRecoveryWithBoundedDiagnostics()
    {
        var nullRepo = CreateRepository();
        var nullCollection = BeginCollection(nullRepo, "null-collection");
        File.WriteAllText(Path.Combine(nullRepo, nullCollection.Path.Replace('/', Path.DirectorySeparatorChar)), "null");
        var nullCollectionRead = RunGuard(nullRepo, "null-collection", "read_file", new { });
        AssertPostmortemCollectionDecision(nullCollectionRead, "allow", allowDiagnostic: true);
        Assert.Contains("OPERATIONS_LEARNING_CONTEXT_REQUIRED", nullCollectionRead.Output);

        if (OperatingSystem.IsWindows())
        {
            var lockedRepo = CreateRepository();
            var lockedCollection = BeginCollection(lockedRepo, "locked-collection");
            var lockedPath = Path.Combine(lockedRepo, lockedCollection.Path.Replace('/', Path.DirectorySeparatorChar));
            using var stream = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var guarded = RunGuard(lockedRepo, "locked-collection", "read_file", new { });
            AssertPostmortemCollectionDecision(guarded, "allow", allowDiagnostic: true);
            Assert.Contains("storage-unavailable", guarded.Output);
        }

        var longRepo = CreateRepository();
        const string longSession = "long-subagent-diagnostic";
        Assert.True(Continues(RunHook(longRepo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005-long.md", longSession)));
        var gate = Assert.Single(Directory.GetFiles(Path.Combine(longRepo, ".engloop", "out", "operations-learning-gates"), "*.json"));
        File.WriteAllText(gate + ".context-required", "operations-hook-option-missing:--postmortem" + new string('a', 5000));
        var blocked = RunHook(longRepo, "postmortem", "subagent-stop", string.Empty, longSession);
        AssertSubagentStopDeferred(blocked, "context is incomplete");
        Assert.Contains("...[truncated]", blocked.Output);
    }

    [Theory]
    [InlineData("", "prompt-missing")]
    [InlineData("--incidents IN001", "--postmortem")]
    [InlineData("--postmortem .engloop/postmortems/PM005_example.md", "--incidents")]
    [InlineData("--incidents BAD --postmortem .engloop/postmortems/PM005_example.md", "incident-ids-invalid")]
    [InlineData("--incidents IN001 --postmortem C:/absolute.md", "path-invalid")]
    public void PostmortemMalformedContext_retainsRecoveryStateWithoutCreatingGate(string prompt, string diagnostic)
    {
        var repo = CreateRepository();
        const string session = "postmortem-malformed";

        var result = RunHook(repo, "postmortem", "initialize", prompt, session);

        if (diagnostic is "prompt-missing" or "--postmortem" or "--incidents")
        {
            AssertNoLifecycleWarning(result);
        }
        else
        {
            AssertPostmortemContextRequired(result, "initialize", diagnostic);
        }

        var collectionPath = Path.Combine(repo, $".engloop/out/postmortem-context/{Sha256ForTest(session)}.json"
            .Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(collectionPath));
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

        var emptyFollowup = RunHook(repo, "postmortem", "initialize", string.Empty, "pm-session");
        Assert.True(Continues(emptyFollowup), emptyFollowup.Output);

        var changed = RunHook(repo, "postmortem", "initialize", "--incidents IN002 --postmortem .engloop/postmortems/PM006_other.md", "pm-session");
        AssertLifecycleDeferred(changed, "postmortem", "initialize");
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
        var denied = RunHook(repo, "postmortem", "guard", string.Empty, session);
        AssertPostmortemScopeDenied(denied);
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
        var repairPartial = RunHook(repairRepo, "repair", "initialize", "--phase route", "repair-continuation");
        AssertNoLifecycleWarning(repairPartial);
        Assert.Single(Directory.GetFiles(Path.Combine(repairRepo, ".engloop", "out", "operations-learning-gates"), "*.repair.json"));
        Assert.True(Continues(RunHook(repairRepo, "repair", "initialize", repairPrompt, "repair-continuation")));
    }

    [Fact]
    public void PostmortemGate_rejectsExistingOrPreviouslyUsedNumber()
    {
        var repo = CreateRepository();
        Directory.CreateDirectory(Path.Combine(repo, ".engloop", "postmortems"));
        File.WriteAllText(Path.Combine(repo, ".engloop", "postmortems", "PM005_existing.md"), "existing\n");
        var sameTarget = RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005_existing.md", "pm-same-target");
        AssertLifecycleDeferred(sameTarget, "postmortem", "initialize");
        Assert.Contains("postmortem-target-already-exists", sameTarget.Output);

        var result = RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005_new.md", "pm-existing");
        AssertLifecycleDeferred(result, "postmortem", "initialize");
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
        AssertLifecycleDeferred(invalid, "repair", "initialize");

        var valid = RunHook(repo, "repair", "initialize", "--phase route --postmortem .engloop/postmortems/PM005_example.md --rpi RPI001 --rules RULE:reliability --acceptance .engloop/repairs/PM005-RPI001.route.json", "repair-valid");
        Assert.True(Continues(valid), valid.Output);

        var wrongSuffix = RunHook(repo, "repair", "initialize", "--phase close --postmortem .engloop/postmortems/PM005_example.md --rpi RPI001 --rules RULE:reliability --acceptance .engloop/repairs/PM005-RPI001.json", "repair-close");
        AssertLifecycleDeferred(wrongSuffix, "repair", "initialize");
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
        AssertLifecycleDeferred(tampered, "postmortem", "initialize");
        Assert.Contains("arguments-tampered", tampered.Output);

        var repo2 = CreateRepository();
        Assert.True(Continues(RunHook(repo2, "postmortem", "initialize", prompt, "pm-manifest")));
        File.AppendAllText(Path.Combine(repo2, ".config", "dotnet-tools.json"), " ");
        var changed = RunHook(repo2, "postmortem", "initialize", "continue", "pm-manifest");
        AssertLifecycleDeferred(changed, "postmortem", "initialize");
        Assert.Contains("tool-identity-changed", changed.Output);
    }

    [Fact]
    public void PostmortemStop_invokesRealValidator_andRejectsCompletionWithoutBlockingNavigation()
    {
        var repo = CreateRepository();
        const string prompt = "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md";
        Assert.True(Continues(RunHook(repo, "postmortem", "initialize", prompt, "pm-stop")));
        File.WriteAllText(Path.Combine(repo, ".engloop", "postmortems", "PM005_example.md"), "# invalid PM\n");

        var stopped = RunHook(repo, "postmortem", "stop", string.Empty, "pm-stop");

        AssertLifecycleDeferred(stopped, "postmortem", "stop");
        Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json"));
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

        AssertLifecycleDeferred(result, "repair", "initialize");
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
    public void IncidentHook_withoutIncidentOption_keepsInitializeAndStopQuiet()
    {
        var repo = CreateRepository();
        const string session = "incident-without-metadata";

        var initialized = RunHook(repo, "incident", "initialize", "The production queue is blocked; restore service now.", session);

        AssertNoLifecycleWarning(initialized);

        var stopped = RunHook(repo, "incident", "stop", string.Empty, session);

        AssertNoLifecycleWarning(stopped);
        AssertNoScopeAuthority(repo);
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
    public void SubprocessStart_repairWithStalledStdinReturnsActionableDiagnostic()
    {
        var repo = CreateRepository();
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
        start.ArgumentList.Add("start");
        start.ArgumentList.Add("repair");

        using var process = Process.Start(start)!;
        try
        {
            Assert.True(process.WaitForExit(10_000), "The hook hung instead of timing out its incomplete stdin payload.");
            var output = process.StandardOutput.ReadToEnd();
            Assert.Empty(process.StandardError.ReadToEnd());
            Assert.Equal(0, process.ExitCode);
            using var json = JsonDocument.Parse(output);
            Assert.True(json.RootElement.GetProperty("continue").GetBoolean());
            var message = json.RootElement.GetProperty("systemMessage").GetString();
            Assert.Contains("operations-hook-stdin-timeout", message);
            Assert.Contains("supported VS Code host", message);
            Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE", output, StringComparison.Ordinal);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    [Theory]
    [InlineData("postmortem")]
    [InlineData("repair")]
    public void SubprocessStart_acceptsCompleteJsonWithoutWaitingForNewlineOrStdinEof(string mode)
    {
        var repo = CreateRepository();
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
        start.ArgumentList.Add("start");
        start.ArgumentList.Add(mode);

        using var process = Process.Start(start)!;
        try
        {
            process.StandardInput.Write(HookJson(repo, "open-stdin-session", string.Empty));
            process.StandardInput.Flush();
            Assert.True(process.WaitForExit(10_000), "The hook waited for stdin EOF after receiving a complete JSON document.");
            var output = process.StandardOutput.ReadToEnd();
            Assert.Empty(process.StandardError.ReadToEnd());
            Assert.Equal(0, process.ExitCode);
            using var json = JsonDocument.Parse(output);
            Assert.True(json.RootElement.GetProperty("continue").GetBoolean());
            var context = json.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString();
            Assert.Contains($"OPERATIONS_LEARNING_GUARD_ACTIVE mode={mode}", context);
            Assert.DoesNotContain("operations-hook-stdin-timeout", output, StringComparison.Ordinal);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public void SubprocessIncidentInitialize_withoutOptionStaysQuiet()
    {
        var repo = CreateRepository();

        var result = RunHookSubprocess(repo, "incident", "initialize", "Restore the affected service immediately.", "subprocess-deferred");

        AssertNoLifecycleWarning(result);
    }

    [Fact]
    public void SubprocessPostmortemInitialize_withoutOption_returnsActionableNonAuthorizingRecovery()
    {
        var repo = CreateRepository();

        var result = RunHookSubprocess(repo, "postmortem", "initialize", "Continue the retrospective.", "subprocess-postmortem-context");

        AssertNoLifecycleWarning(result);
    }

    [Fact]
    public void Hook_defersRecoverableOperationsContextWhileRejectingInvalidDispatchAndRepairArguments()
    {
        var repo = CreateRepository();
        Assert.False(Continues(RunHookRaw(repo, [], "{}")));
        Assert.False(Continues(RunHookRaw(repo, ["start"], "{}")));
        Assert.False(Continues(RunHookRaw(repo, ["start", "unknown"], HookJson(repo, "s", string.Empty))));
        Assert.False(Continues(RunHookRaw(repo, ["unknown", "incident"], HookJson(repo, "s", string.Empty))));
        AssertIncidentDeferred(RunHookRaw(repo, ["start", "incident"], "not-json"), "start", "operations-hook-json-invalid");
        AssertIncidentDeferred(RunHookRaw(repo, ["start", "incident"], JsonSerializer.Serialize(new { session_id = "s" })), "start", "cwd-missing");

        var child = Path.Combine(repo, "child");
        Directory.CreateDirectory(child);
        AssertIncidentDeferred(RunHookRaw(repo, ["start", "incident"], HookJson(child, "s", string.Empty)), "start", "cwd-not-exact-git-root");
        AssertNoLifecycleWarning(RunHook(repo, "incident", "initialize", string.Empty, "empty-prompt"));
        AssertIncidentDeferred(RunHook(repo, "incident", "initialize", "--incident C:/absolute.md", "absolute"), "initialize", "path-invalid");
        AssertIncidentDeferred(RunHook(repo, "incident", "initialize", "--incident .engloop/postmortems/PM001.md", "wrong-prefix"), "initialize", "path-invalid");
        AssertPostmortemContextRequired(RunHook(repo, "postmortem", "initialize", "--incidents IN001,IN001 --postmortem .engloop/postmortems/PM005.md", "duplicate-incidents"), "initialize", "incident-ids-invalid");
        AssertPostmortemContextRequired(RunHook(repo, "postmortem", "initialize", "--incidents BAD --postmortem .engloop/postmortems/PM005.md", "bad-incident"), "initialize", "incident-ids-invalid");
        AssertLifecycleDeferred(RunHook(repo, "repair", "initialize", "--phase invalid --postmortem .engloop/postmortems/PM005.md --rpi RPI001 --rules RULE:x --acceptance .engloop/repairs/PM005-RPI001.route.json", "bad-phase"), "repair", "initialize");
        AssertLifecycleDeferred(RunHook(repo, "repair", "initialize", "--phase route --postmortem .engloop/postmortems/PM005.md --rpi BAD --rules RULE:x --acceptance .engloop/repairs/PM005-RPI001.route.json", "bad-rpi"), "repair", "initialize");
        AssertLifecycleDeferred(RunHook(repo, "repair", "initialize", "--phase route --postmortem .engloop/postmortems/PM005.md --rpi RPI001 --rules RULE:x,RULE:x --acceptance .engloop/repairs/PM005-RPI001.route.json", "duplicate-rules"), "repair", "initialize");

        File.WriteAllText(Path.Combine(repo, ".engloop", "postmortems", "PM006_exists.md"), "existing");
        AssertLifecycleDeferred(RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM006_exists.md", "existing-pm"), "postmortem", "initialize");
        AssertLifecycleDeferred(RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/notpm.md", "bad-pm-name"), "postmortem", "initialize");
        File.WriteAllText(Path.Combine(repo, ".engloop", "repairs", "PM005-RPI001.route.json"), "existing");
        AssertLifecycleDeferred(RunHook(repo, "repair", "initialize", "--phase route --postmortem .engloop/postmortems/PM005.md --rpi RPI001 --rules RULE:x --acceptance .engloop/repairs/PM005-RPI001.route.json", "existing-route"), "repair", "initialize");

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
        AssertLifecycleDeferred(nullVersionPostmortem, "postmortem", "initialize");
        Assert.Contains("tool-version-missing", nullVersionPostmortem.Output);

        AssertNoLifecycleWarning(RunHook(repo, "incident", "stop", string.Empty, "missing-gate"));

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
    public void OperationsStop_defersInvalidEvidenceForEveryModeWithoutFalseCompletion()
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
        AssertLifecycleDeferred(repairStop, "repair", "stop");
        Assert.Contains("mode=repair", repairStop.Output);

        var postmortem = CreateRepository();
        Assert.True(Continues(RunHook(postmortem, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005.md", "pm-stop-mode")));
        var pmStop = RunHook(postmortem, "postmortem", "stop", string.Empty, "pm-stop-mode");
        AssertLifecycleDeferred(pmStop, "postmortem", "stop");
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

    [Theory]
    [InlineData("--postmortem .engloop/postmortems/PM005-analysis.md.", ".engloop/postmortems/PM005-analysis.md")]
    [InlineData("--postmortem=.engloop/postmortems/PM005-analysis.md.", ".engloop/postmortems/PM005-analysis.md")]
    [InlineData("--postmortem '.engloop/postmortems/PM005-analysis.v1.md'", ".engloop/postmortems/PM005-analysis.v1.md")]
    [InlineData("--postmortem \".engloop/postmortems/PM005-analysis.v1.md\"", ".engloop/postmortems/PM005-analysis.v1.md")]
    [InlineData("--postmortem '.engloop/postmortems/PM005-analysis..v1.md'", ".engloop/postmortems/PM005-analysis..v1.md")]
    public void PostmortemProse_normalizesOnlyOneBareSentencePeriodAndPreservesInternalDots(string argument, string expected)
    {
        var repo = CreateRepository();
        const string session = "prose-path";
        var result = RunHook(repo, "postmortem", "initialize", "Please analyze --incidents IN001 " + argument, session);
        Assert.True(Continues(result), result.Output);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", result.Output);
        var gatePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json"));
        using var gate = JsonDocument.Parse(File.ReadAllText(gatePath));
        Assert.Equal(expected, gate.RootElement.GetProperty("Postmortem").GetString());
        var gateBytes = File.ReadAllBytes(gatePath);
        var followup = RunHook(repo, "postmortem", "initialize", $"--incidents IN001 --postmortem '{expected}'", session);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", followup.Output);
        Assert.Equal(gateBytes, File.ReadAllBytes(gatePath));
    }

    [Theory]
    [InlineData("'.engloop/postmortems/PM005-analysis.md.'")]
    [InlineData("\".engloop/postmortems/PM005-analysis.md.\"")]
    [InlineData(".engloop/postmortems/PM005-analysis.md..")]
    [InlineData(".engloop/postmortems/PM005-analysis.md,")]
    [InlineData(".engloop/postmortems/PM005-analysis.md;")]
    [InlineData(".engloop/postmortems/PM005-analysis.MD")]
    [InlineData("'.engloop/postmortems/PM005-analysis.md '")]
    [InlineData("'.engloop/postmortems/PM005-analysis.md\n'")]
    [InlineData(".engloop/postmortems/../PM005-analysis.md")]
    [InlineData(".engloop/postmortems/./PM005-analysis.md")]
    [InlineData(".engloop/postmortems/nested/PM005-analysis.md")]
    [InlineData(".engloop/postmortems/PM005-analysis.md/..")]
    [InlineData(".engloop/postmortems/PM005-analysis.md:stream")]
    [InlineData(".engloop/postmortems/PM005-analysis\\file.md")]
    public void PostmortemProse_invalidExactFilenameNeverWritesScopeAuthority(string argument)
    {
        var repo = CreateRepository();
        var result = RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem " + argument, "invalid-path");
        AssertLifecycleDeferred(result, "postmortem", "initialize");
        AssertNoScopeAuthority(repo);
    }

    [Theory]
    [InlineData(".engloop/postmortems/PM005-analysis.md.")]
    [InlineData(".engloop/postmortems/PM005-analysis.md ")]
    [InlineData(".engloop/postmortems/PM005-analysis.md\n")]
    [InlineData(".engloop/postmortems/../PM005-analysis.md")]
    public void PostmortemCliAndStructuredConfirmation_doNotApplyProseNormalization(string path)
    {
        var repo = CreateRepository();
        const string session = "exact-transport";
        var collection = BeginCollection(repo, session);
        var collectionFull = Path.Combine(repo, collection.Path.Replace('/', Path.DirectorySeparatorChar));
        var original = File.ReadAllBytes(collectionFull);
        var rejected = AnswerCollection(repo, session, ["IN001"], path, "Confirm", autoBind: true);
        AssertLifecycleDeferred(rejected.Result, "postmortem", "post-tool");
        Assert.Contains("OPERATIONS_POSTMORTEM_CONFIRMATION_UNVERIFIED", rejected.Result.Output);
        AssertNoScopeAuthority(repo);
        Assert.Equal(original, File.ReadAllBytes(collectionFull));

        // Supply an exact receipt to the CLI: an invalid name must not be
        // normalized to a different identity, even when the receipt matches it.
        var confirmation = AnswerCollection(repo, session, ["IN001"], path, "Confirm");
        var receiptFull = Path.Combine(repo, confirmation.ReceiptPath.Replace('/', Path.DirectorySeparatorChar));
        var receiptBytes = File.ReadAllBytes(receiptFull);
        var cli = RunPostmortemRoute(repo, ["bind", "--collection", collection.Path, "--token", collection.Token,
            "--incidents", "IN001", "--postmortem", path, "--confirmation-receipt", confirmation.ReceiptPath]);
        Assert.Equal(1, cli.ExitCode);
        Assert.Contains("invalid", cli.Error);
        Assert.Equal(original, File.ReadAllBytes(collectionFull));
        Assert.Equal(receiptBytes, File.ReadAllBytes(receiptFull));
        AssertNoScopeAuthority(repo, allowConfirmation: true);
    }

    public static IEnumerable<object[]> LifecycleFailureCases()
    {
        foreach (var mode in new[] { "incident", "postmortem", "repair" })
        foreach (var phase in new[] { "start", "subagent-start", "initialize", "stop", "subagent-stop" })
        foreach (var fault in new[] { "malformed-json", "missing-root", "child-root", "unavailable-root", "non-git-root", "missing-session", "missing-manifest", "malformed-manifest", "invalid-manifest" })
            yield return [mode, phase, fault];
    }

    [Theory]
    [MemberData(nameof(LifecycleFailureCases))]
    public void LifecycleFailures_continueWithBoundedActionableDiagnosticsAndRetainGate(string mode, string phase, string fault)
    {
        var repo = CreateRepository();
        const string session = "lifecycle-fault";
        var prompt = ScopePrompt(mode);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", RunHook(repo, mode, "initialize", prompt, session).Output);
        var gatePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json"));
        var original = File.ReadAllBytes(gatePath);
        var input = JsonNode.Parse(HookJson(repo, session, prompt))!.AsObject();
        var manifestPath = Path.Combine(repo, ".config", "dotnet-tools.json");
        switch (fault)
        {
            case "missing-root": input.Remove("cwd"); break;
            case "child-root": input["cwd"] = Directory.CreateDirectory(Path.Combine(repo, "child")).FullName; break;
            case "unavailable-root": input["cwd"] = Path.Combine(repo, "absent"); break;
            case "non-git-root": input["cwd"] = _work; break;
            case "missing-session": input.Remove("session_id"); break;
            case "missing-manifest": File.Delete(manifestPath); break;
            case "malformed-manifest": File.WriteAllText(manifestPath, "{"); break;
            case "invalid-manifest": File.WriteAllText(manifestPath, "{}"); break;
        }
        var result = RunHookRaw(repo, [phase, mode], fault == "malformed-json" ? "{" : input.ToJsonString());
        AssertLifecycleDeferred(result, mode, phase);
        Assert.Equal(original, File.ReadAllBytes(gatePath));
    }

    [Theory]
    [InlineData("incident", "stop")]
    [InlineData("incident", "subagent-stop")]
    [InlineData("postmortem", "stop")]
    [InlineData("postmortem", "subagent-stop")]
    [InlineData("repair", "stop")]
    [InlineData("repair", "subagent-stop")]
    public void StopFailures_retainExactGateForMissingEvidenceMalformedStateAndChangedHead(string mode, string phase)
    {
        var repo = CreateRepository();
        const string session = "stop-failure";
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", RunHook(repo, mode, "initialize", ScopePrompt(mode), session).Output);
        var gatePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json"));
        File.Delete(Path.Combine(repo, ".engloop", "incidents", "IN001_example.md"));
        var original = File.ReadAllText(gatePath);
        foreach (var contents in new[] { original, "{", "null" })
        {
            File.WriteAllText(gatePath, contents);
            AssertLifecycleDeferred(RunHook(repo, mode, phase, string.Empty, session), mode, phase);
            Assert.Equal(contents, File.ReadAllText(gatePath));
        }
        File.WriteAllText(gatePath, original);
        Git(repo, "add", "-u");
        Git(repo, "commit", "-m", "changed head");
        var stale = RunHook(repo, mode, phase, string.Empty, session);
        AssertLifecycleDeferred(stale, mode, phase);
        Assert.Contains("head-changed", stale.Output);
        Assert.Equal(original, File.ReadAllText(gatePath));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("collection")]
    [InlineData("corrupt-collection")]
    [InlineData("suspended")]
    [InlineData("corrupt-suspension")]
    [InlineData("corrupt-gate")]
    [InlineData("invalid-entry")]
    [InlineData("missing-manifest")]
    [InlineData("missing-root")]
    [InlineData("missing-session")]
    public void InvalidScope_allowsRecoveryReadsAndQuestionsButDeniesMutationExecutionAndDelegation(string state)
    {
        var repo = CreateRepository();
        const string session = "guard-recovery";
        if (state.Contains("collection", StringComparison.Ordinal))
        {
            var collection = BeginCollection(repo, session);
            if (state == "corrupt-collection") File.WriteAllText(Path.Combine(repo, collection.Path.Replace('/', Path.DirectorySeparatorChar)), "{");
        }
        else if (state != "missing")
        {
            Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", RunHook(repo, "postmortem", "initialize", ScopePrompt("postmortem"), session).Output);
            var gate = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json"));
            if (state == "corrupt-gate") File.WriteAllText(gate, "{");
            if (state == "suspended") File.WriteAllText(gate + ".context-required", "operations-hook-option-missing:--postmortem");
            if (state == "corrupt-suspension") File.WriteAllText(gate + ".context-required", "corrupt");
            if (state == "invalid-entry") File.WriteAllText(Path.Combine(repo, ".engloop", "config.json"), "{");
            if (state == "missing-manifest") File.Delete(Path.Combine(repo, ".config", "dotnet-tools.json"));
        }
        var outRoot = Path.Combine(repo, ".engloop", "out");
        var retained = Directory.Exists(outRoot)
            ? Directory.GetFiles(outRoot, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes)
            : new Dictionary<string, byte[]>();
        foreach (var tool in new[] { "read", "search", "read_file", "file_search", "grep_search", "semantic_search", "list_dir", "vscode_askQuestions",
            "apply_patch", "create_file", "run_in_terminal", "runSubagent", "agent", "custom_run_anything", "" })
        {
            var input = JsonNode.Parse(HookJson(repo, session, string.Empty))!.AsObject();
            input["tool_name"] = tool;
            input["tool_input"] = new JsonObject { ["prompt"] = "Select Stage 50 and execute a file mutation" };
            if (state == "missing-root") input.Remove("cwd");
            if (state == "missing-session") input.Remove("session_id");
            var guarded = RunHookRaw(repo, ["guard", "postmortem"], input.ToJsonString());
            var allow = tool is "read" or "search" or "read_file" or "file_search" or "grep_search" or "semantic_search" or "list_dir" or "vscode_askQuestions";
            AssertPostmortemCollectionDecision(guarded, allow ? "allow" : "deny", allowDiagnostic: state != "collection");
            if (state == "collection")
            {
                using var response = JsonDocument.Parse(guarded.Output);
                var reason = response.RootElement.GetProperty("hookSpecificOutput").GetProperty("permissionDecisionReason").GetString()!;
                Assert.Contains(allow ? "Stage 21 context collection permits" : "tool call was blocked; no action ran", reason, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("Stage 22", guarded.Output);
                Assert.Contains("Stage 50", guarded.Output);
            }
            Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE", guarded.Output);
        }
        foreach (var (path, bytes) in retained) Assert.Equal(bytes, File.ReadAllBytes(path));
        AssertPostmortemCollectionDecision(RunHookRaw(repo, ["guard", "postmortem"], "{"), "deny", allowDiagnostic: true);
    }

    [Fact]
    public void MalformedActiveIncidentBindingRetainsEvidenceAndPermitsNavigationWithoutClaimingStabilization()
    {
        var repo = CreateRepository();
        const string session = "active-incident-navigation";
        var incidentPath = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md");
        File.WriteAllText(incidentPath, "# IN001\n\n- **Status:** ACTIVE\n");
        var incidentBytes = File.ReadAllBytes(incidentPath);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", RunHook(repo, "incident", "initialize", ScopePrompt("incident"), session).Output);
        foreach (var phase in new[] { "stop", "subagent-stop" })
            AssertLifecycleDeferred(RunHook(repo, "incident", phase, string.Empty, session), "incident", phase);

        foreach (var phase in new[] { "start", "subagent-start" })
            Assert.True(Continues(RunHook(repo, "postmortem", phase, string.Empty, session)));
        var collection = BeginCollection(repo, session);
        var collectionFull = Path.Combine(repo, collection.Path.Replace('/', Path.DirectorySeparatorChar));
        var collectionBytes = File.ReadAllBytes(collectionFull);
        var confirmation = AnswerCollection(repo, session, ["IN001"], ".engloop/postmortems/PM005-active.md", "Confirm", autoBind: true);
        AssertPostToolContext(confirmation.Result, "POSTMORTEM_ROUTE_BIND_FAILED", "postmortem-route-analysis-context-invalid");
        var failurePath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".binding-failure.json");
        var failureBytes = File.ReadAllBytes(failurePath);
        using var failure = JsonDocument.Parse(failureBytes);
        Assert.Contains(failure.RootElement.GetProperty("CorrelationId").GetString()!, confirmation.Result.Output, StringComparison.Ordinal);
        AssertNoLifecycleWarning(RunHook(repo, "postmortem", "stop", string.Empty, session));
        AssertLifecycleDeferred(RunHook(repo, "postmortem", "subagent-stop", string.Empty, session), "postmortem", "subagent-stop");
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "read_file", new { }), "allow");
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "runSubagent", new { prompt = "Stage 50: mutate files" }), "deny");
        Assert.Equal(collectionBytes, File.ReadAllBytes(collectionFull));
        Assert.Equal(failureBytes, File.ReadAllBytes(failurePath));
        Assert.Equal(incidentBytes, File.ReadAllBytes(incidentPath));
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.postmortem.json"));
        Assert.False(OperationsLearningPolicy.ValidateIncidentContext(repo, ".engloop/incidents/IN001_example.md", requireConsulted: true).Passed);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("subagent-start")]
    public void PostmortemEntry_doesNotRequireAgentEntryEvidenceForNavigation(string phase)
    {
        var repo = CreateRepository();
        File.Delete(Path.Combine(repo, ".engloop", "config.json"));
        var result = RunHook(repo, "postmortem", phase, string.Empty, "entry-without-config");
        Assert.True(Continues(result), result.Output);
        Assert.Contains("OPERATIONS_LEARNING_GUARD_ACTIVE", result.Output);
        AssertNoScopeAuthority(repo);
        AssertLifecycleDeferred(RunHook(repo, "postmortem", "initialize", ScopePrompt("postmortem"), "entry-without-config"), "postmortem", "initialize");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Please repair this and let me select Stage 50.")]
    [InlineData("--phase route")]
    public void RepairMissingArguments_continueWithoutScopeOrCompletion(string prompt)
    {
        var repo = CreateRepository();
        var result = RunHook(repo, "repair", "initialize", prompt, "repair-missing-args");
        AssertNoLifecycleWarning(result);
        AssertNoScopeAuthority(repo);
        AssertNoLifecycleWarning(RunHook(repo, "repair", "stop", string.Empty, "repair-missing-args"));
    }

    [Theory]
    [InlineData("Please implement repair items after the postmortem.", true, true, "")]
    [InlineData("Please deploy a fix to livesite. This is a new incident.", true, true, "")]
    [InlineData("Yes, please do 21 now, and go straight into 22 and deploy the fix to livesite.", true, true, "")]
    [InlineData("If needed, deploy a fix to livesite.", false, false, "")]
    [InlineData("Please do not deploy a fix to livesite.", false, false, "")]
    [InlineData("Operator said \"deploy a fix to livesite\".", false, false, "")]
    [InlineData("Please implement RPI001 after the postmortem.", true, false, "RPI001")]
    [InlineData("Please do not implement repair items.", false, false, "")]
    [InlineData("Please analyze the postmortem and repair items.", false, false, "")]
    [InlineData("Please implement repair items. Do not print twice.", true, true, "")]
    [InlineData("Please implement not repair items.", false, false, "")]
    [InlineData("If needed, implement repair items.", false, false, "")]
    [InlineData("we don't neeed to mitigate by printing teh stencils, we just need to mitigate this incident by understand why it has failed so badly. then perform the PM and impl all RPIs", true, true, "")]
    [InlineData("so now it is up to to work out why a bunch of the stencils failed, and why the alerts were raised, please then do the PM, and implemented the RPIs", true, true, "")]
    [InlineData("Do not impl the RPIs", false, false, "")]
    [InlineData("We implemented all RPIs yesterday", false, false, "")]
    public void RepairIntentCapture_requiresExplicitAffirmativeRequest(string prompt, bool expected, bool allRepairs, string rpi)
    {
        var repo = CreateRepository();
        const string session = "repair-intent-capture";

        AssertNoLifecycleWarning(RunHook(repo, "incident", "initialize", prompt, session));

        var intentPath = Path.Combine(repo, ".engloop", "out", "operations-learning-gates", Sha256ForTest(session) + ".repair-intent.json");
        Assert.Empty(Directory.Exists(Path.GetDirectoryName(intentPath)!)
            ? Directory.GetFiles(Path.GetDirectoryName(intentPath)!, "*.repair.json")
            : []);
        Assert.Equal(expected, File.Exists(intentPath));
        if (!expected) return;

        using var intent = JsonDocument.Parse(File.ReadAllText(intentPath));
        Assert.Equal(Sha256ForTest(session), intent.RootElement.GetProperty("SessionHash").GetString());
        Assert.Equal(allRepairs, intent.RootElement.GetProperty("AllRepairs").GetBoolean());
        Assert.Matches("^[a-f0-9]{64}$", intent.RootElement.GetProperty("PromptSha256").GetString()!);
        Assert.Equal(rpi.Length == 0 ? Array.Empty<string>() : new[] { rpi }, intent.RootElement.GetProperty("Rpis").EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    [Fact]
    public void RepairIntentCapture_freshRequestAfterHeadChangePreservesHistoricalIntent()
    {
        var repo = CreateRepository();
        const string session = "repair-intent-next-incident";
        RunHook(repo, "incident", "initialize", "Please implement RPI001 after the postmortem.", session);
        var intentPath = Path.Combine(repo, ".engloop", "out", "operations-learning-gates", Sha256ForTest(session) + ".repair-intent.json");
        var historicalBytes = File.ReadAllBytes(intentPath);
        File.WriteAllText(Path.Combine(repo, "next-incident.txt"), "next incident");
        Git(repo, "add", "next-incident.txt");
        Git(repo, "commit", "-m", "next incident");

        var result = RunHook(repo, "incident", "initialize", "Please deploy the fix to livesite.", session);

        AssertNoLifecycleWarning(result);
        using var intent = JsonDocument.Parse(File.ReadAllBytes(intentPath));
        Assert.True(intent.RootElement.GetProperty("AllRepairs").GetBoolean());
        Assert.Equal(OperationsLearningPolicy.GitHead(repo), intent.RootElement.GetProperty("Head").GetString());
        var history = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(intentPath)!, Path.GetFileName(intentPath) + ".history.*.json"));
        Assert.Equal(historicalBytes, File.ReadAllBytes(history));
    }

    [Fact]
    public void TrustedRepairContinuation_invalidPostmortemReturnsResumableBlockerWithoutRouteOrScope()
    {
        var repo = CreateRepository();
        const string session = "repair-continuation-invalid-pm";
        AssertNoLifecycleWarning(RunHook(repo, "incident", "initialize", "Please implement RPI001 after the postmortem.", session));

        const string postmortem = ".engloop/postmortems/PM005_example.md";
        _ = BeginCollection(repo, session);
        var confirmation = AnswerCollection(repo, session, ["IN001"], postmortem, "Confirm", autoBind: true);
        AssertPostToolContext(confirmation.Result, "POSTMORTEM_ROUTE_BOUND", "OPERATIONS_LEARNING_SCOPE_ACTIVE");
        File.WriteAllText(Path.Combine(repo, postmortem.Replace('/', Path.DirectorySeparatorChar)), "# incomplete PM005\n");

        var result = RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", session);

        Assert.True(Continues(result), result.Output + result.Error);
        using var response = JsonDocument.Parse(result.Output);
        var message = response.RootElement.GetProperty("systemMessage").GetString()!;
        Assert.StartsWith("OPERATIONS_REPAIR_CONTINUATION_BLOCKED ", message, StringComparison.Ordinal);
        using var details = JsonDocument.Parse(message[message.IndexOf('{')..]);
        Assert.Equal("repair-continuation-postmortem-validation-failed", details.RootElement.GetProperty("diagnosticCode").GetString());
        Assert.False(details.RootElement.GetProperty("repairScopeCreated").GetBoolean());
        Assert.False(details.RootElement.GetProperty("completionAccepted").GetBoolean());
        Assert.NotEmpty(details.RootElement.GetProperty("missingEvidence").EnumerateArray());
        Assert.Contains("resume the trusted Stage 22 continuation", details.RootElement.GetProperty("resumableAction").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(repo, ".engloop", "out", "operations-learning-gates", Sha256ForTest(session) + ".postmortem.json")));
        Assert.True(File.Exists(Path.Combine(repo, ".engloop", "out", "operations-learning-gates", Sha256ForTest(session) + ".repair-intent.json")));
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "repairs"), "*.route.json"));
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.repair.json"));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("valid-object")]
    [InlineData("failed-object")]
    [InlineData("failed-write")]
    [InlineData("multiple-incidents")]
    [InlineData("unrelated-file")]
    [InlineData("no-intent")]
    [InlineData("wrong-event")]
    [InlineData("other-session")]
    public void TrustedPostmortemContinuation_bindsIncidentWrittenAfterPlainLanguageRequest(string fault)
    {
        var repo = CreateRepository();
        const string session = "plain-language-incident-autopilot";
        RunHook(repo, "incident", "initialize", fault == "no-intent" ? "This is a new incident." : "This is a new incident. Please deploy a fix to livesite.", session);
        const string incident = ".engloop/incidents/IN001_example.md";
        var patch = "*** Begin Patch\n*** Update File: " + incident + "\n*** End Patch";
        if (fault == "multiple-incidents") patch += "\n*** Update File: .engloop/incidents/IN002_other.md\n";
        if (fault == "unrelated-file") patch = "*** Begin Patch\n*** Update File: README.md\n*** End Patch";
        var captured = RunHookRaw(repo, ["post-tool", "incident"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = fault == "other-session" ? "unrelated-session" : session,
            hookEventName = fault == "wrong-event" ? "PreToolUse" : "PostToolUse",
            tool_name = "apply_patch", tool_use_id = "incident-evidence-write",
            tool_input = new { input = patch },
            tool_response = fault == "valid-object"
                ? (object)new { content = "The following files were successfully edited: " + incident, isError = false }
                : fault == "failed-object"
                    ? new { content = "The following files were successfully edited: " + incident, isError = true }
                : fault == "failed-write" ? "Failed to edit files" : "The following files were successfully edited: " + incident,
        }));
        if (fault is not ("valid" or "valid-object"))
        {
            Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=incident", captured.Output);
            Assert.False(File.Exists(Path.Combine(repo, ".engloop", "out", "operations-learning-gates", Sha256ForTest(session) + ".incident.json")));
            return;
        }
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=incident", captured.Output);
        var continued = RunHookRaw(repo, ["initialize", "postmortem"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = session, hookEventName = "UserPromptSubmit",
            prompt = "ELK_TRUSTED_AUTOPILOT_POSTMORTEM_CONTINUATION",
        }));
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", continued.Output);
        Assert.Contains("authorization=trusted-incident-continuation", continued.Output);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("active")]
    [InlineData("other-session")]
    [InlineData("no-intent")]
    [InlineData("changed-head")]
    [InlineData("changed-tool")]
    [InlineData("handoff-prompt")]
    public void TrustedPostmortemContinuation_requiresSameSessionIncidentAndRepairIntent(string fault)
    {
        var repo = CreateRepository();
        const string session = "incident-autopilot";
        if (fault == "active")
        {
            var incidentPath = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md");
            File.WriteAllText(incidentPath, File.ReadAllText(incidentPath).Replace("STABILIZED", "ACTIVE", StringComparison.Ordinal));
        }
        var prompt = ScopePrompt("incident") + (fault == "no-intent" ? "" : " Please deploy a fix to livesite.");
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=incident", RunHook(repo, "incident", "initialize", prompt, session).Output);
        var stopped = RunHook(repo, "incident", "stop", string.Empty, session);
        if (fault == "active") Assert.DoesNotContain("INCIDENT_CONTEXT_OK", stopped.Output);
        else Assert.Contains("INCIDENT_CONTEXT_OK", stopped.Output);
        if (fault == "changed-head")
        {
            File.WriteAllText(Path.Combine(repo, "advance.txt"), "advance");
            Git(repo, "add", "advance.txt");
            Git(repo, "commit", "-m", "advance");
        }
        if (fault == "changed-tool") File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");
        var result = RunHookRaw(repo, ["initialize", "postmortem"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = fault == "other-session" ? "other-session" : session,
            hookEventName = "UserPromptSubmit", prompt = fault == "handoff-prompt"
                ? "ELK_TRUSTED_AUTOPILOT_POSTMORTEM_CONTINUATION Continue the requested incident postmortem in this native session, retaining the incident evidence and explicit operator repair intent above. Use the trusted context collection and binding path; do not ask the user to reconstruct internal flags."
                : "ELK_TRUSTED_AUTOPILOT_POSTMORTEM_CONTINUATION",
        }));
        Assert.True(Continues(result), result.Output + result.Error);
        if (fault is not ("valid" or "active" or "handoff-prompt"))
        {
            Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", result.Output);
            Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.postmortem.json"));
            return;
        }
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", result.Output);
        Assert.Contains("authorization=trusted-incident-continuation", result.Output);
        const string postmortem = ".engloop/postmortems/PM005-example.md";
        WriteValidRepairContinuationPostmortem(repo, postmortem);
        var postmortemStopped = RunHook(repo, "postmortem", "stop", string.Empty, session);
        if (fault == "active") Assert.DoesNotContain("POSTMORTEM_LEARNING_OK", postmortemStopped.Output);
        else Assert.Contains("POSTMORTEM_LEARNING_OK", postmortemStopped.Output);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair",
            RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", session).Output);
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.confirmation.json"));
    }

    [Fact]
    public void TrustedRepairContinuation_rejectsReceiptFromAnotherSession()
    {
        var repo = CreateRepository();
        AssertNoLifecycleWarning(RunHook(repo, "incident", "initialize", "Please implement RPI001 after the postmortem.", "repair-source-session"));

        var result = RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", "repair-other-session");

        Assert.True(Continues(result), result.Output + result.Error);
        using var response = JsonDocument.Parse(result.Output);
        var message = response.RootElement.GetProperty("systemMessage").GetString()!;
        Assert.StartsWith("OPERATIONS_REPAIR_CONTINUATION_BLOCKED ", message, StringComparison.Ordinal);
        using var details = JsonDocument.Parse(message[message.IndexOf('{')..]);
        Assert.Equal("repair-continuation-postmortem-gate-missing", details.RootElement.GetProperty("diagnosticCode").GetString());
        Assert.False(details.RootElement.GetProperty("repairScopeCreated").GetBoolean());
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.repair.json"));
    }

    [Fact]
    public void TrustedRepairContinuation_validPostmortemCreatesExactImmutableRouteAndRepairScope()
    {
        var repo = CreateRepository();
        const string session = "repair-continuation-valid-pm";
        AssertNoLifecycleWarning(RunHook(repo, "incident", "initialize", "Please implement RPI001 after the postmortem.", session));
        const string postmortem = ".engloop/postmortems/PM005-example.md";
        _ = BeginCollection(repo, session);
        var confirmation = AnswerCollection(repo, session, ["IN001"], postmortem, "Confirm", autoBind: true);
        AssertPostToolContext(confirmation.Result, "POSTMORTEM_ROUTE_BOUND", "OPERATIONS_LEARNING_SCOPE_ACTIVE");

        WriteValidRepairContinuationPostmortem(repo, postmortem);
        var postmortemValidation = OperationsLearningPolicy.ValidatePostmortem(repo, postmortem, ["IN001"]);
        Assert.True(postmortemValidation.Passed, string.Join(',', postmortemValidation.Failures));

        var result = RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", session);

        Assert.True(Continues(result), result.Output + result.Error);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", result.Output);
        Assert.Contains("rpi=RPI001", result.Output);
        var routePath = Path.Combine(repo, ".engloop", "repairs", "PM005-RPI001.route.json");
        var routeBytes = File.ReadAllBytes(routePath);
        using (var route = JsonDocument.Parse(routeBytes))
        {
            var payload = route.RootElement;
            Assert.Equal("repair-learning-route", payload.GetProperty("artifactType").GetString());
            Assert.Equal(postmortem, payload.GetProperty("postmortemPath").GetString());
            Assert.Equal(postmortemValidation.Contract!.Sha256, payload.GetProperty("postmortemSha256").GetString());
            Assert.Equal("RPI001", payload.GetProperty("rpiId").GetString());
            Assert.Equal("RULE:reliability", payload.GetProperty("ruleIds")[0].GetString());
            Assert.Equal(new[] { "dotnet", "--version" }, payload.GetProperty("executableGate").EnumerateArray().Select(item => item.GetString()).ToArray());
        }
        Assert.True(OperationsLearningPolicy.ValidateRepairAcceptance(repo, postmortem, "RPI001",
            ["RULE:reliability"], ".engloop/repairs/PM005-RPI001.route.json", "route", false).Passed);

        var repeated = RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", session);

        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", repeated.Output);
        Assert.Equal(routeBytes, File.ReadAllBytes(routePath));
        Assert.Contains("REPAIR_LEARNING_OK", RunHook(repo, "repair", "stop", string.Empty, session).Output);
    }

    [Theory]
    [InlineData("Please implement RPI001 after the postmortem.")]
    [InlineData("Please deploy a fix to livesite. This is a new incident.")]
    [InlineData("Yes, please do 21 now, and go straight into 22 and deploy the fix to livesite.")]
    public void TrustedRepairContinuation_activeIncidentRoutesRepairWithoutClaimingCompletion(string prompt)
    {
        var repo = CreateRepository();
        const string session = "repair-active-incident";
        var postmortem = PrepareValidRepairContinuation(repo, session, prompt);
        var incidentPath = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md");
        var oldHash = OperationsLearningPolicy.Sha256(incidentPath);
        var incident = File.ReadAllText(incidentPath).Replace("STABILIZED", "ACTIVE", StringComparison.Ordinal)
            .Replace("[x] Health checks passing", "[ ] Health checks passing", StringComparison.Ordinal)
            .Replace("[x] No fresh errors in the watch window", "[ ] No fresh errors in the watch window", StringComparison.Ordinal);
        File.WriteAllText(incidentPath, incident);
        var pmPath = Path.Combine(repo, postmortem.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(pmPath, File.ReadAllText(pmPath)
            .Replace(oldHash, OperationsLearningPolicy.Sha256(incidentPath), StringComparison.Ordinal)
            .Replace("**Status:** COMPLETE", "**Status:** ANALYSIS RECORDED", StringComparison.Ordinal));
        Assert.False(OperationsLearningPolicy.ValidatePostmortem(repo, postmortem, ["IN001"]).Passed);

        var result = RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", session);

        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", result.Output);
        Assert.True(OperationsLearningPolicy.ValidateRepairAcceptance(repo, postmortem, "RPI001",
            ["RULE:reliability"], ".engloop/repairs/PM005-RPI001.route.json", "route", false).Passed);
        Assert.False(OperationsLearningPolicy.ValidatePostmortem(repo, postmortem, ["IN001"]).Passed);
        Assert.False(OperationsLearningPolicy.ValidateRepairAcceptance(repo, postmortem, "RPI001",
            ["RULE:reliability"], ".engloop/repairs/PM005-RPI001.route.json", "close", false).Passed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitRepairRequest_newNativeSessionBindsNamedPostmortemAndAllRequestedItems(bool multipleItems)
    {
        var repo = CreateRepository();
        const string postmortem = ".engloop/postmortems/PM005-example.md";
        WriteValidRepairContinuationPostmortem(repo, postmortem);
        if (multipleItems)
        {
            var full = Path.Combine(repo, postmortem.Replace('/', Path.DirectorySeparatorChar));
            var text = File.ReadAllText(full);
            var contract = text[text.IndexOf("### RPI001 learning contract", StringComparison.Ordinal)..];
            File.WriteAllText(full, text.Replace("| RPI001 | Preserve the exact route contract. | OPEN |",
                "| RPI001 | Preserve the exact route contract. | OPEN |\n| RPI002 | Preserve the second exact route contract. | OPEN |", StringComparison.Ordinal)
                + "\n\n" + contract.Replace("RPI001", "RPI002", StringComparison.Ordinal));
        }
        const string session = "direct-native-repair";
        var started = RunHookRaw(repo, ["start", "repair"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = session, hookEventName = "SessionStart",
        }));
        Assert.Contains("OPERATIONS_LEARNING_GUARD_ACTIVE mode=repair", started.Output);

        var result = RunHookRaw(repo, ["initialize", "repair"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = session, hookEventName = "UserPromptSubmit",
            prompt = "Implement all repair items for PM005 at " + postmortem + ". Do not print twice.",
        }));

        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", result.Output);
        using var response = JsonDocument.Parse(result.Output);
        Assert.Equal("UserPromptSubmit", response.RootElement.GetProperty("hookSpecificOutput").GetProperty("hookEventName").GetString());
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", response.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString());
        Assert.Equal(multipleItems ? 2 : 1, Directory.GetFiles(Path.Combine(repo, ".engloop", "repairs"), "*.route.json").Length);
        Assert.Contains("REPAIR_LEARNING_OK", RunHook(repo, "repair", "stop", string.Empty, session).Output);
    }

    [Fact]
    public void ExplicitRepairRequest_sameSessionCanSelectAnotherPostmortemWithoutChangingPriorRoute()
    {
        var repo = CreateRepository();
        const string session = "direct-repair-scope-change";
        const string firstPostmortem = ".engloop/postmortems/PM005-example.md";
        const string secondPostmortem = ".engloop/postmortems/PM006-example.md";
        WriteValidRepairContinuationPostmortem(repo, firstPostmortem);
        var firstPath = Path.Combine(repo, firstPostmortem.Replace('/', Path.DirectorySeparatorChar));
        var secondPath = Path.Combine(repo, secondPostmortem.Replace('/', Path.DirectorySeparatorChar));
        var firstDigest = OperationsLearningPolicy.ComputePyramidDigest(repo, "PM005");
        var firstResult = RunHookRaw(repo, ["initialize", "repair"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = session, hookEventName = "UserPromptSubmit",
            prompt = "Implement all repair items for PM005.",
        }));
        Assert.True(firstResult.Output.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", StringComparison.Ordinal), firstResult.Output + firstResult.Error);
        var routePath = Path.Combine(repo, ".engloop", "repairs", "PM005-RPI001.route.json");
        var originalRoute = File.ReadAllBytes(routePath);
        File.WriteAllText(secondPath, File.ReadAllText(firstPath)
            .Replace("PM005", "PM006", StringComparison.Ordinal)
            .Replace(firstDigest, OperationsLearningPolicy.ComputePyramidDigest(repo, "PM006"), StringComparison.Ordinal));

        var result = RunHookRaw(repo, ["initialize", "repair"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = session, hookEventName = "UserPromptSubmit",
            prompt = "Implement all repair items for PM006.",
        }));

        Assert.True(result.Output.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", StringComparison.Ordinal), result.Output + result.Error);
        Assert.Contains(secondPostmortem, result.Output);
        Assert.Equal(originalRoute, File.ReadAllBytes(routePath));
        Assert.Contains("REPAIR_LEARNING_OK", RunHook(repo, "repair", "stop", string.Empty, session).Output);
    }

    [Theory]
    [InlineData("missing-event")]
    [InlineData("ambiguous-pm")]
    [InlineData("wrong-path")]
    [InlineData("changed-head")]
    [InlineData("changed-tool")]
    [InlineData("negated-request")]
    public void ExplicitRepairRequest_invalidNativeContextNeverCreatesRoute(string fault)
    {
        var repo = CreateRepository();
        const string session = "direct-repair-invalid";
        const string postmortem = ".engloop/postmortems/PM005-example.md";
        WriteValidRepairContinuationPostmortem(repo, postmortem);
        if (fault != "missing-host")
            _ = RunHookRaw(repo, ["start", "repair"], JsonSerializer.Serialize(new
            {
                cwd = repo, session_id = session, hookEventName = "SessionStart",
            }));
        if (fault == "changed-head")
        {
            File.WriteAllText(Path.Combine(repo, "advance.txt"), "advance");
            Git(repo, "add", "advance.txt");
            Git(repo, "commit", "-m", "advance native repair head");
        }
        if (fault == "changed-tool") File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");
        var prompt = fault switch
        {
            "ambiguous-pm" => "Implement all repair items for PM005 and PM006.",
            "wrong-path" => "Implement all repair items for PM005 at .engloop/postmortems/PM005-other.md.",
            "negated-request" => "Do not implement repair items for PM005.",
            _ => "Implement all repair items for PM005.",
        };
        var result = RunHookRaw(repo, ["initialize", "repair"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = fault == "another-session" ? "other-session" : session,
            hookEventName = fault == "missing-event" ? "" : "UserPromptSubmit", prompt,
        }));

        Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", result.Output);
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "repairs"), "*.route.json"));
        var gateRoot = Path.Combine(repo, ".engloop", "out", "operations-learning-gates");
        Assert.Empty(Directory.Exists(gateRoot) ? Directory.GetFiles(gateRoot, "*.repair.json") : []);
    }

    [Fact]
    public void ExplicitRepairRequest_existingChatDoesNotRequireSessionStart()
    {
        var repo = CreateRepository();
        WriteValidRepairContinuationPostmortem(repo, ".engloop/postmortems/PM005-example.md");
        var result = RunHookRaw(repo, ["initialize", "repair"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = "existing-chat", hookEventName = "UserPromptSubmit",
            prompt = "Implement all repair items for PM005.",
        }));
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", result.Output);
    }

    [Theory]
    [InlineData("Implement all repair items for PM005.", true)]
    [InlineData("Do not implement repair items for PM005.", false)]
    [InlineData("Implement all repair items for PM005 and PM006.", false)]
    public void ExplicitRepairAuthorization_withoutLifecycleUsesSeparateAuditableAuthority(string request, bool authorized)
    {
        var repo = CreateRepository();
        WriteValidRepairContinuationPostmortem(repo, ".engloop/postmortems/PM005-example.md");
        var result = RunHookRaw(repo, ["authorize", "repair", "--root", repo, "--request", request], "");
        Assert.Equal(authorized, result.Output.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", StringComparison.Ordinal));
        var gateRoot = Path.Combine(repo, ".engloop", "out", "operations-learning-gates");
        var records = Directory.Exists(gateRoot) ? Directory.GetFiles(gateRoot, "*.operator-authorization.json") : [];
        Assert.Equal(authorized ? 1 : 0, records.Length);
        if (authorized)
        {
            using var record = JsonDocument.Parse(File.ReadAllText(records[0]));
            Assert.Equal(request, record.RootElement.GetProperty("request").GetString());
            Assert.False(record.RootElement.GetProperty("completionAccepted").GetBoolean());
            Assert.DoesNotContain("hookSpecificOutput", result.Output);
        }
    }

    [Theory]
    [InlineData("Create the postmortem for IN001.", true)]
    [InlineData("Do not create the postmortem for IN001.", false)]
    [InlineData("Could we create the postmortem for IN001?", false)]
    [InlineData("Create PM004 for IN001.", false)]
    public void ExplicitPostmortemAuthorization_withoutLifecycleUsesSeparateAuditableAuthority(string request, bool authorized)
    {
        var repo = CreateRepository();
        var result = RunHookRaw(repo, ["authorize", "postmortem", "--root", repo, "--request", request], "");
        Assert.Equal(authorized, result.Output.Contains("POSTMORTEM_ROUTE_BOUND", StringComparison.Ordinal));
        var gateRoot = Path.Combine(repo, ".engloop", "out", "operations-learning-gates");
        var records = Directory.Exists(gateRoot) ? Directory.GetFiles(gateRoot, "*.operator-authorization.json") : [];
        Assert.Equal(authorized ? 1 : 0, records.Length);
        if (authorized)
        {
            using var record = JsonDocument.Parse(File.ReadAllText(records[0]));
            Assert.Equal(request, record.RootElement.GetProperty("request").GetString());
            Assert.False(record.RootElement.GetProperty("completionAccepted").GetBoolean());
            Assert.Contains("nativeSession=false", result.Output);
            Assert.DoesNotContain("hookSpecificOutput", result.Output);
            Assert.False(File.Exists(Path.Combine(repo, ".engloop", "postmortems", "PM005-example.md")));
        }
    }

    [Fact]
    public void PostmortemHashInspection_isCanonicalReadOnlyAndDoesNotClaimCompletion()
    {
        var repo = CreateRepository();
        const string postmortem = ".engloop/postmortems/PM005-example.md";
        WriteValidRepairContinuationPostmortem(repo, postmortem);
        var full = Path.Combine(repo, postmortem.Replace('/', Path.DirectorySeparatorChar));
        var digest = OperationsLearningPolicy.ComputePyramidDigest(repo, "PM005");
        File.WriteAllText(full, File.ReadAllText(full).Replace(digest, "PENDING", StringComparison.Ordinal)
            .Replace("**Status:** COMPLETE", "**Status:** ANALYSIS RECORDED", StringComparison.Ordinal));
        var before = File.ReadAllBytes(full);
        var originalOut = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            Assert.Equal(0, ValidationCommands.ValidatePostmortemLearning(["--root", repo, "--incidents", "IN001",
                "--postmortem", postmortem, "--show-current-hashes"]));
        }
        finally { Console.SetOut(originalOut); }
        using var context = JsonDocument.Parse(output.ToString());
        Assert.Equal(digest, context.RootElement.GetProperty("pyramidDigest").GetString());
        Assert.False(context.RootElement.GetProperty("completionAccepted").GetBoolean());
        Assert.Equal(before, File.ReadAllBytes(full));
        Assert.False(OperationsLearningPolicy.ValidatePostmortem(repo, postmortem).Passed);
        AssertPostmortemCollectionDecision(RunGuard(repo, "hash-context", "run_in_terminal", new
        {
            command = "dotnet tool run engloopkit -- validate postmortem-learning --root . --incidents IN001 --postmortem " + postmortem + " --show-current-hashes",
        }), "deny", allowDiagnostic: true);
    }

    [Theory]
    [InlineData("tampered-intent")]
    [InlineData("tampered-binding")]
    [InlineData("wrong-rpi")]
    [InlineData("changed-head")]
    [InlineData("changed-tool-identity")]
    public void TrustedRepairContinuation_rejectsInvalidIdentityOrRpiWithoutCreatingScope(string change)
    {
        var repo = CreateRepository();
        var session = "repair-continuation-" + change;
        var requestedRpi = change == "wrong-rpi" ? "RPI999" : "RPI001";
        var postmortem = PrepareValidRepairContinuation(repo, session,
            $"Please implement {requestedRpi} after the postmortem.");
        var sessionHash = Sha256ForTest(session);

        switch (change)
        {
            case "tampered-intent":
            {
                var path = Path.Combine(repo, ".engloop", "out", "operations-learning-gates", sessionHash + ".repair-intent.json");
                var record = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                record["PromptSha256"] = "invalid-digest";
                File.WriteAllText(path, record.ToJsonString());
                break;
            }
            case "tampered-binding":
            {
                var path = Path.Combine(repo, ".engloop", "out", "postmortem-context", sessionHash + ".binding.json");
                var record = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                record["GateSha256"] = new string('0', 64);
                File.WriteAllText(path, record.ToJsonString());
                break;
            }
            case "changed-head":
                File.WriteAllText(Path.Combine(repo, "new-head.txt"), "changed\n");
                Git(repo, "add", "new-head.txt");
                Git(repo, "commit", "-m", "advance continuation head");
                break;
            case "changed-tool-identity":
                File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");
                break;
        }

        var result = RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", session);

        Assert.True(Continues(result), result.Output + result.Error);
        using var response = JsonDocument.Parse(result.Output);
        var message = response.RootElement.GetProperty("systemMessage").GetString()!;
        Assert.StartsWith("OPERATIONS_REPAIR_CONTINUATION_BLOCKED ", message, StringComparison.Ordinal);
        using var details = JsonDocument.Parse(message[message.IndexOf('{')..]);
        Assert.False(details.RootElement.GetProperty("repairScopeCreated").GetBoolean());
        Assert.False(details.RootElement.GetProperty("completionAccepted").GetBoolean());
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "repairs"), "*.route.json"));
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.repair.json"));
        Assert.True(File.Exists(Path.Combine(repo, postmortem.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Theory]
    [InlineData("incident")]
    [InlineData("postmortem")]
    [InlineData("repair")]
    public void InvalidAction_remainsStrictEvenWithMalformedInput(string mode)
    {
        var repo = CreateRepository();
        var result = RunHookRaw(repo, ["unknown", mode], "{");
        Assert.False(Continues(result));
        Assert.Contains("action-invalid", result.Output);
        Assert.DoesNotContain("CONTEXT_DEFERRED", result.Output);
        AssertNoScopeAuthority(repo);
    }

    [Fact]
    public void ExplicitOperatorPostmortem_nativeActivationIsDeliveredAndRestoredWithoutRebinding()
    {
        var repo = CreateRepository();
        const string session = "explicit-native-activation";
        const string prompt = "Complete the postmortem for IN001.";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        var initialized = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);
        AssertNativeActivation(initialized.Output, "UserPromptSubmit");
        var bindingPath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".binding.json");
        var bindingBytes = File.ReadAllBytes(bindingPath);
        AssertNativeActivation(RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt).Output, "UserPromptSubmit");
        var restored = RunHookRaw(repo, ["post-tool", "postmortem"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = session, hookEventName = "PostToolUse",
            tool_name = "read_file", tool_response = new { content = "read succeeded" },
        }));
        AssertNativeActivation(restored.Output, "PostToolUse");
        Assert.Equal(bindingBytes, File.ReadAllBytes(bindingPath));
        AssertNativeActivation(RunGuard(repo, session, "read_file", new { }).Output, "PreToolUse");
    }

    [Fact]
    public void ExplicitOperatorPostmortem_todayRequestBindsAllDatedIncidentsWithoutQuestion()
    {
        var repo = CreateRepository();
        var incidentDirectory = Path.Combine(repo, ".engloop", "incidents");
        var templatePath = Path.Combine(incidentDirectory, "IN001_example.md");
        var template = File.ReadAllText(templatePath);
        File.Delete(templatePath);
        var today = DateTime.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var incidents = new[] { "IN025", "IN026", "IN027" };
        foreach (var incident in incidents)
            File.WriteAllText(Path.Combine(incidentDirectory, $"{incident}-{today}-fixture.md"),
                template.Replace("IN001", incident, StringComparison.Ordinal));

        const string session = "today-postmortem-all-incidents";
        const string prompt = "Complete post mortem for today's incidents.";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        var initialized = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);

        Assert.Contains("POSTMORTEM_ROUTE_BOUND", initialized.Output);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem", initialized.Output);
        Assert.Contains("incidents=IN025,IN026,IN027", initialized.Output);
        Assert.Contains($"postmortem=.engloop/postmortems/PM005-{today}-incidents-in025-in026-in027.md", initialized.Output);
        Assert.DoesNotContain("vscode_askQuestions", initialized.Output);
    }

    [Fact]
    public void ExplicitOperatorPostmortem_todayScopeSurvivesToolIdentityUpgradeInSameChat()
    {
        var repo = CreateRepository();
        var incidentDirectory = Path.Combine(repo, ".engloop", "incidents");
        var templatePath = Path.Combine(incidentDirectory, "IN001_example.md");
        var template = File.ReadAllText(templatePath);
        File.Delete(templatePath);
        var today = DateTime.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var incidents = new[] { "IN025", "IN026", "IN027" };
        foreach (var incident in incidents)
            File.WriteAllText(Path.Combine(incidentDirectory, $"{incident}-{today}-fixture.md"),
                template.Replace("IN001", incident, StringComparison.Ordinal));

        File.WriteAllText(Path.Combine(repo, ".engloop", "numbering-registry.md"),
            "| Type | Next number |\n|---|---|\n| PM | 062 |\n");
        const string session = "today-postmortem-identity-upgrade";
        const string prompt = "Complete post mortem for today's incidents.";
        var postmortem = today == "20261005"
            ? ".engloop/postmortems/PM062-20261005-three-stencil-dispatch-incidents.md"
            : $".engloop/postmortems/PM062-{today}-incidents-in025-in026-in027.md";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt).Output);

        File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");
        var reauthorized = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", reauthorized.Output);
        Assert.Contains("incidents=IN025,IN026,IN027", reauthorized.Output);
        Assert.Contains("postmortem=" + postmortem, reauthorized.Output);
        Assert.Contains("historicalPostmortem=" + postmortem, reauthorized.Output);
        Assert.DoesNotContain("vscode_askQuestions", reauthorized.Output);

        var lineagePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.reauthorization.*.json"));
        var lineage = JsonNode.Parse(File.ReadAllText(lineagePath))!;
        Assert.Equal(postmortem, lineage["Postmortem"]!.GetValue<string>());
        Assert.Equal(today, lineage["IncidentDate"]!.GetValue<string>());
        Assert.Equal(incidents, lineage["Incidents"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray());

        var resumed = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", resumed.Output);
        Assert.Contains("postmortem=" + postmortem, resumed.Output);
        Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.reauthorization.*.json"));
    }

    [Fact]
    public void ConfirmationPostmortem_todayScopeSurvivesToolIdentityUpgradeWithoutOldIntentFile()
    {
        var repo = CreateRepository();
        var incidentDirectory = Path.Combine(repo, ".engloop", "incidents");
        var templatePath = Path.Combine(incidentDirectory, "IN001_example.md");
        var template = File.ReadAllText(templatePath);
        File.Delete(templatePath);
        var today = DateTime.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var incidents = new[] { "IN025", "IN026", "IN027" };
        foreach (var incident in incidents)
            File.WriteAllText(Path.Combine(incidentDirectory, $"{incident}-{today}-fixture.md"),
                template.Replace("IN001", incident, StringComparison.Ordinal));

        File.WriteAllText(Path.Combine(repo, ".engloop", "numbering-registry.md"),
            "| Type | Next number |\n|---|---|\n| PM | 062 |\n");
        const string session = "today-postmortem-confirmation-upgrade";
        const string prompt = "Complete post mortem for today's incidents.";
        var postmortem = today == "20261005"
            ? ".engloop/postmortems/PM062-20261005-three-stencil-dispatch-incidents.md"
            : $".engloop/postmortems/PM062-{today}-incidents-in025-in026-in027.md";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        var collection = RunHook(repo, "postmortem", "initialize", "Complete the postmortem for the known stencil incident.", session);
        Assert.True(Continues(collection), collection.Output + collection.Error);
        var confirmation = AnswerCollection(repo, session, incidents, postmortem, "Confirm", answerByQuestion: true, autoBind: true);
        AssertPostToolContext(confirmation.Result, "POSTMORTEM_ROUTE_BOUND", "OPERATIONS_LEARNING_SCOPE_ACTIVE");

        File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");
        var reauthorized = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", reauthorized.Output);
        Assert.Contains("incidents=IN025,IN026,IN027", reauthorized.Output);
        Assert.Contains("postmortem=" + postmortem, reauthorized.Output);

        var lineagePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.reauthorization.*.json"));
        var lineage = JsonNode.Parse(File.ReadAllText(lineagePath))!;
        var previousSessionHash = lineage["PreviousSessionHash"]!.GetValue<string>();
        var preservedArtifacts = lineage["PreservedArtifacts"]!.AsObject().Select(entry => entry.Key).ToArray();
        Assert.DoesNotContain(preservedArtifacts, path => path.EndsWith(previousSessionHash + ".operator-intent.json", StringComparison.Ordinal));
        var resumed = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", resumed.Output);
        Assert.Contains("postmortem=" + postmortem, resumed.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmationPostmortem_yesterdaysScopeSurvivesSameChatUpgradeOnlyWithUnchangedEvidence(bool changeEvidence)
    {
        var repo = CreateRepository();
        var incidentDirectory = Path.Combine(repo, ".engloop", "incidents");
        var templatePath = Path.Combine(incidentDirectory, "IN001_example.md");
        var template = File.ReadAllText(templatePath);
        File.Delete(templatePath);
        var priorDate = DateTime.Today.AddDays(-1).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var incidents = new[] { "IN025", "IN026", "IN027" };
        foreach (var incident in incidents)
            File.WriteAllText(Path.Combine(incidentDirectory, $"{incident}-{priorDate}-fixture.md"),
                template.Replace("IN001", incident, StringComparison.Ordinal));

        File.WriteAllText(Path.Combine(repo, ".engloop", "numbering-registry.md"),
            "| Type | Next number |\n|---|---|\n| PM | 062 |\n");
        const string session = "yesterday-postmortem-confirmation-upgrade";
        var postmortem = priorDate == "20261005"
            ? ".engloop/postmortems/PM062-20261005-three-stencil-dispatch-incidents.md"
            : $".engloop/postmortems/PM062-{priorDate}-incidents-in025-in026-in027.md";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        var collection = RunHook(repo, "postmortem", "initialize",
            "Complete the postmortem for the known stencil incident.", session);
        Assert.True(Continues(collection), collection.Output + collection.Error);
        var confirmation = AnswerCollection(repo, session, incidents, postmortem, "Confirm",
            answerByQuestion: true, autoBind: true);
        AssertPostToolContext(confirmation.Result, "POSTMORTEM_ROUTE_BOUND", "OPERATIONS_LEARNING_SCOPE_ACTIVE");

        if (changeEvidence)
            File.AppendAllText(Path.Combine(incidentDirectory, $"IN026-{priorDate}-fixture.md"), "\nChanged evidence\n");
        File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");

        var reauthorized = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Complete post mortem for today's incidents.");
        Assert.True(Continues(reauthorized), reauthorized.Output + reauthorized.Error);
        if (changeEvidence)
        {
            Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", reauthorized.Output);
            Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem", reauthorized.Output);
            return;
        }

        Assert.Contains("POSTMORTEM_ROUTE_BOUND", reauthorized.Output);
        Assert.Contains("incidents=IN025,IN026,IN027", reauthorized.Output);
        Assert.Contains("postmortem=" + postmortem, reauthorized.Output);
        Assert.DoesNotContain("vscode_askQuestions", reauthorized.Output);

        var lineagePath = Assert.Single(Directory.GetFiles(
            Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.reauthorization.*.json"));
        var lineage = JsonNode.Parse(File.ReadAllText(lineagePath))!;
        Assert.Equal(priorDate, lineage["IncidentDate"]!.GetValue<string>());
        Assert.Equal(incidents, lineage["Incidents"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray());
    }

    [Theory]
    [InlineData("wrong-session")]
    [InlineData("changed-set")]
    [InlineData("changed-evidence")]
    public void ExplicitOperatorPostmortem_todayScopeRequestsKeepFreshAuthorizationDistinctFromChangedEvidence(string fault)
    {
        var repo = CreateRepository();
        var incidentDirectory = Path.Combine(repo, ".engloop", "incidents");
        var templatePath = Path.Combine(incidentDirectory, "IN001_example.md");
        var template = File.ReadAllText(templatePath);
        File.Delete(templatePath);
        var today = DateTime.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var incidents = new[] { "IN025", "IN026", "IN027" };
        foreach (var incident in incidents)
            File.WriteAllText(Path.Combine(incidentDirectory, $"{incident}-{today}-fixture.md"),
                template.Replace("IN001", incident, StringComparison.Ordinal));

        const string session = "today-postmortem-invalid-reauthorization";
        const string prompt = "Complete post mortem for today's incidents.";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt).Output);

        if (fault == "wrong-session")
        {
            var wrongSession = RunHostPostmortem(repo, session + "-other", "initialize", "UserPromptSubmit", prompt);
            Assert.Contains("POSTMORTEM_ROUTE_BOUND", wrongSession.Output);
            Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem", wrongSession.Output);
            Assert.Contains("bindingReceipt=.engloop/out/postmortem-context/" + Sha256ForTest(session + "-other")
                + ".binding.json", wrongSession.Output);
            Assert.DoesNotContain("bindingReceipt=.engloop/out/postmortem-context/" + Sha256ForTest(session)
                + ".binding.json", wrongSession.Output);
            return;
        }

        if (fault == "changed-set")
            File.WriteAllText(Path.Combine(incidentDirectory, $"IN028-{today}-fixture.md"),
                template.Replace("IN001", "IN028", StringComparison.Ordinal));
        if (fault == "changed-evidence")
            File.AppendAllText(Path.Combine(incidentDirectory, $"IN026-{today}-fixture.md"), "\nChanged evidence\n");
        File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");

        var rejected = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);
        if (fault == "changed-set")
        {
            Assert.Contains("POSTMORTEM_ROUTE_BOUND", rejected.Output);
            Assert.Contains("incidents=IN025,IN026,IN027,IN028", rejected.Output);
            Assert.DoesNotContain("bindingReceipt=.engloop/out/postmortem-context/" + Sha256ForTest(session)
                + ".binding.json", rejected.Output);
            var lineagePath = Assert.Single(Directory.GetFiles(
                Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.reauthorization.*.json"));
            using var lineage = JsonDocument.Parse(File.ReadAllText(lineagePath));
            Assert.True(lineage.RootElement.GetProperty("ScopeChange").GetBoolean());
            return;
        }
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", rejected.Output);
        Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem", rejected.Output);
    }

    [Fact]
    public void ExplicitOperatorPostmortem_tamperedDatedReauthorizationLineageCannotRestoreBinding()
    {
        var repo = CreateRepository();
        var incidentDirectory = Path.Combine(repo, ".engloop", "incidents");
        var templatePath = Path.Combine(incidentDirectory, "IN001_example.md");
        var template = File.ReadAllText(templatePath);
        File.Delete(templatePath);
        var today = DateTime.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var incidents = new[] { "IN025", "IN026", "IN027" };
        foreach (var incident in incidents)
            File.WriteAllText(Path.Combine(incidentDirectory, $"{incident}-{today}-fixture.md"),
                template.Replace("IN001", incident, StringComparison.Ordinal));

        const string session = "today-postmortem-tampered-lineage";
        const string prompt = "Complete post mortem for today's incidents.";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt).Output);
        File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt).Output);

        var lineagePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.reauthorization.*.json"));
        var lineage = JsonNode.Parse(File.ReadAllText(lineagePath))!;
        lineage["Incidents"]![0] = "IN028";
        File.WriteAllText(lineagePath, lineage.ToJsonString());

        var rejected = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", rejected.Output);
        Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem", rejected.Output);
    }

    [Fact]
    public void ExplicitOperatorPostmortem_repeatedSessionStartRefreshesUnboundHostAfterHeadChange()
    {
        var repo = CreateRepository();
        const string session = "same-chat-stale-host";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");

        File.WriteAllText(Path.Combine(repo, "new-head.txt"), "advance host head");
        Git(repo, "add", "new-head.txt");
        Git(repo, "commit", "-m", "advance host head");
        RunHostPostmortem(repo, session, "start", "SessionStart", "");

        var initialized = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Complete the postmortem for IN001.");
        AssertNativeActivation(initialized.Output, "UserPromptSubmit");
    }

    [Fact]
    public void ExplicitOperatorPostmortem_repeatedSessionStartPreservesBoundHostAfterHeadChange()
    {
        var repo = CreateRepository();
        const string session = "same-chat-bound-host";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        var initialized = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Complete the postmortem for IN001.");
        AssertNativeActivation(initialized.Output, "UserPromptSubmit");

        var hostPath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".host.json");
        var boundHost = File.ReadAllBytes(hostPath);
        File.WriteAllText(Path.Combine(repo, "new-head.txt"), "advance bound host head");
        Git(repo, "add", "new-head.txt");
        Git(repo, "commit", "-m", "advance bound host head");
        RunHostPostmortem(repo, session, "start", "SessionStart", "");

        Assert.Equal(boundHost, File.ReadAllBytes(hostPath));
        var stale = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Complete the postmortem for IN001.");
        AssertLifecycleDeferred(stale, "postmortem", "initialize");
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", stale.Output);
    }

    [Theory]
    [InlineData("Complete the postmortem for IN001.")]
    [InlineData("Please complete PM005 for IN001.")]
    public void ExplicitOperatorPostmortem_existingChatWithoutSessionStartBindsCurrentPrompt(string prompt)
    {
        var repo = CreateRepository();
        const string session = "existing-chat-postmortem";
        var result = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);

        AssertNativeActivation(result.Output, "UserPromptSubmit");
        var sessionHash = Sha256ForTest(session);
        Assert.True(File.Exists(Path.Combine(repo, ".engloop", "out", "postmortem-context", sessionHash + ".host.json")));
        Assert.True(File.Exists(Path.Combine(repo, ".engloop", "out", "postmortem-context", sessionHash + ".binding.json")));
    }

    [Fact]
    public void ExplicitOperatorPostmortem_missingHostWithExistingScopeIsNotRecreated()
    {
        var repo = CreateRepository();
        const string session = "missing-postmortem-host-with-scope";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Complete the postmortem for IN001.").Output);

        var hostPath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".host.json");
        File.Delete(hostPath);
        var resumed = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", "Complete the postmortem for IN001.");

        AssertLifecycleDeferred(resumed, "postmortem", "initialize");
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", resumed.Output);
        Assert.False(File.Exists(hostPath));
    }

    [Fact]
    public void ExplicitOperatorPostmortem_newIncidentInLegacyChatPreservesHistoryAndBindsFreshScope()
    {
        var repo = CreateRepository();
        const string session = "legacy-chat-new-incident";
        const string oldPostmortem = ".engloop/postmortems/PM005-example.md";
        RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", "Complete the postmortem for IN001.");
        var sessionHash = Sha256ForTest(session);
        var oldGatePath = Path.Combine(repo, ".engloop", "out", "operations-learning-gates", sessionHash + ".postmortem.json");
        var oldBindingPath = Path.Combine(repo, ".engloop", "out", "postmortem-context", sessionHash + ".binding.json");
        var oldGate = File.ReadAllBytes(oldGatePath);
        var oldBinding = File.ReadAllBytes(oldBindingPath);
        File.Delete(Path.Combine(repo, ".engloop", "out", "postmortem-context", sessionHash + ".host.json"));
        var incidentPath = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md");
        File.WriteAllText(Path.Combine(repo, ".engloop", "incidents", "IN002-next.md"),
            File.ReadAllText(incidentPath).Replace("IN001", "IN002", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(repo, oldPostmortem.Replace('/', Path.DirectorySeparatorChar)), "# PM005: Earlier analysis\n");
        var registryPath = Path.Combine(repo, ".engloop", "numbering-registry.md");
        File.WriteAllText(registryPath, File.ReadAllText(registryPath).Replace("`PM004`", "`PM005`", StringComparison.Ordinal));
        Git(repo, "add", ".");
        Git(repo, "commit", "-m", "next incident");

        var result = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Complete PM006 for IN002 at `.engloop/postmortems/PM006-next.md`.");

        Assert.True(result.Output.Contains("POSTMORTEM_ROUTE_BOUND", StringComparison.Ordinal), result.Output);
        Assert.Contains("incidents=IN002", result.Output);
        Assert.Contains("postmortem=.engloop/postmortems/PM006-next.md", result.Output);
        Assert.Equal(oldGate, File.ReadAllBytes(oldGatePath));
        Assert.Equal(oldBinding, File.ReadAllBytes(oldBindingPath));
        File.WriteAllText(registryPath, File.ReadAllText(registryPath).Replace("`PM005`", "`PM006`", StringComparison.Ordinal));
        var resumed = RunHookRaw(repo, ["post-tool", "postmortem"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = session, hookEventName = "PostToolUse",
            tool_name = "read_file", tool_response = new { content = "read succeeded" },
        }));
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", resumed.Output);
        Assert.Contains("incidents=IN002", resumed.Output);
    }

    [Theory]
    [InlineData("Ok, please complete the Post Mortem and RPIs for the last incident (wrt Text Stencils failing to print)")]
    [InlineData("Please complete the PM and RPIs for the last incident.")]
    [InlineData("Ive moved you to agent 21, complete the PM and RPIs for IN002.")]
    public void ExplicitOperatorPostmortem_lastIncidentAndRpisBindsWithoutQuestion(string prompt)
    {
        var repo = CreateRepository();
        const string session = "last-incident-autopilot";
        var original = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md");
        File.WriteAllText(Path.Combine(repo, ".engloop", "incidents", "IN002-text-generation-failure.md"),
            File.ReadAllText(original).Replace("IN001", "IN002", StringComparison.Ordinal));

        var result = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);

        Assert.Contains("POSTMORTEM_ROUTE_BOUND", result.Output);
        Assert.Contains("incidents=IN002", result.Output);
        Assert.Contains("postmortem=.engloop/postmortems/PM005-text-generation-failure.md", result.Output);
        Assert.Contains("authorization=explicit-operator-intent", result.Output);
        Assert.True(File.Exists(Path.Combine(repo, ".engloop", "out", "operations-learning-gates",
            Sha256ForTest(session) + ".repair-intent.json")));
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.confirmation.json"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitOperatorPostmortem_unboundRepairIntentSurvivesToolUpgradeWithoutSessionRestart(bool mixedGeneration)
    {
        var repo = CreateRepository();
        const string session = "unbound-autopilot-upgrade";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", "Please deploy a fix to livesite.");
        var directory = Path.Combine(repo, ".engloop", "out", "postmortem-context");
        var hostPath = Path.Combine(directory, Sha256ForTest(session) + ".host.json");
        var intentPath = Path.Combine(repo, ".engloop", "out", "operations-learning-gates", Sha256ForTest(session) + ".repair-intent.json");
        if (mixedGeneration)
        {
            var host = JsonNode.Parse(File.ReadAllText(hostPath))!;
            host["ManifestSha256"] = new string('a', 64);
            File.WriteAllText(hostPath, host.ToJsonString());
        }
        var oldHost = File.ReadAllBytes(hostPath);
        var oldIntent = File.ReadAllBytes(intentPath);
        File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");

        var result = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Please complete the PM and RPIs for the last incident.");

        Assert.Contains("POSTMORTEM_ROUTE_BOUND", result.Output);
        Assert.Contains("authorization=explicit-operator-intent", result.Output);
        var history = Assert.Single(Directory.GetDirectories(Path.Combine(directory, "unbound-history")));
        Assert.Equal(oldHost, File.ReadAllBytes(Path.Combine(history, Path.GetFileName(hostPath))));
        Assert.Equal(oldIntent, File.ReadAllBytes(Path.Combine(history, Path.GetFileName(intentPath))));
    }

    [Theory]
    [InlineData("Do not complete the PM and RPIs for the last incident.")]
    [InlineData("Could we complete the PM and RPIs for the last incident?")]
    [InlineData("\"Please complete the PM and RPIs for the last incident.\"")]
    public void ExplicitOperatorPostmortem_lastIncidentRejectsNonDirective(string prompt)
    {
        var repo = CreateRepository();
        var result = RunHostPostmortem(repo, "last-incident-negative", "initialize", "UserPromptSubmit", prompt);
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", result.Output);
        AssertNoScopeAuthority(repo);
    }

    [Fact]
    public void ExplicitOperatorPostmortem_lastIncidentRejectsDuplicateLatestIdentity()
    {
        var repo = CreateRepository();
        var original = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md");
        File.Copy(original, Path.Combine(repo, ".engloop", "incidents", "IN001-duplicate.md"));
        var result = RunHostPostmortem(repo, "last-incident-duplicate", "initialize", "UserPromptSubmit",
            "Please complete the PM and RPIs for the last incident.");
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", result.Output);
        AssertNoScopeAuthority(repo);
    }

    [Theory]
    [InlineData("changed-head")]
    [InlineData("tampered-intent")]
    [InlineData("host-assembly")]
    [InlineData("intent-assembly")]
    public void ExplicitOperatorPostmortem_unboundUpgradeRejectsInvalidHistory(string fault)
    {
        var repo = CreateRepository();
        const string session = "unbound-upgrade-negative";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", "Please deploy a fix to livesite.");
        File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");
        if (fault == "changed-head")
        {
            File.WriteAllText(Path.Combine(repo, "changed-head.txt"), "new head");
            Git(repo, "add", ".");
            Git(repo, "commit", "-m", "change head");
        }
        else
        {
            var intentPath = fault == "host-assembly"
                ? Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".host.json")
                : Path.Combine(repo, ".engloop", "out", "operations-learning-gates", Sha256ForTest(session) + ".repair-intent.json");
            var intent = JsonNode.Parse(File.ReadAllText(intentPath))!;
            if (fault == "tampered-intent") intent["SessionHash"] = "another-session";
            else intent["AssemblySha256"] = new string('0', 64);
            File.WriteAllText(intentPath, intent.ToJsonString());
        }
        var result = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Please complete the PM and RPIs for the last incident.");
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", result.Output);
        Assert.False(File.Exists(Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".binding.json")));
    }

    private static void AssertBoundGuardContext(string output, string postmortem)
    {
        using var json = JsonDocument.Parse(output);
        var specific = json.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("PreToolUse", specific.GetProperty("hookEventName").GetString());
        Assert.False(specific.TryGetProperty("permissionDecision", out _));
        var context = specific.GetProperty("additionalContext").GetString()!;
        Assert.Contains("POSTMORTEM_ROUTE_BOUND OPERATIONS_LEARNING_SCOPE_ACTIVE", context);
        Assert.Contains("bindingReceipt=", context);
        Assert.Contains("postmortem=" + postmortem, context);
        Assert.Contains("authorization=confirmation", context);
    }

    private static void AssertNativeActivation(string output, string hookEvent)
    {
        using var json = JsonDocument.Parse(output);
        var specific = json.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal(hookEvent, specific.GetProperty("hookEventName").GetString());
        var context = specific.GetProperty("additionalContext").GetString()!;
        Assert.Contains("POSTMORTEM_ROUTE_BOUND OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem", context);
        Assert.Contains("bindingReceipt=", context);
        Assert.Contains("incidents=IN001", context);
        Assert.Contains("postmortem=.engloop/postmortems/PM005-example.md", context);
        Assert.Contains("authorization=explicit-operator-intent", context);
    }

    [Theory]
    [InlineData("intent")]
    [InlineData("binding")]
    [InlineData("gate")]
    [InlineData("host")]
    [InlineData("changed-head")]
    [InlineData("changed-manifest")]
    [InlineData("changed-incident")]
    [InlineData("suspended")]
    [InlineData("different-session")]
    [InlineData("failed-tool")]
    [InlineData("wrong-event")]
    [InlineData("terminal")]
    public void ExplicitOperatorPostmortem_nativeRecoveryNeverAuthorizesInvalidScope(string fault)
    {
        var repo = CreateRepository();
        const string session = "explicit-native-recovery-negative";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", "Complete the postmortem for IN001.");
        var sessionHash = Sha256ForTest(session);
        var gatePath = Path.Combine(repo, ".engloop", "out", "operations-learning-gates", sessionHash + ".postmortem.json");
        if (fault is "intent" or "binding")
            File.AppendAllText(Path.Combine(repo, ".engloop", "out", "postmortem-context", sessionHash
                + (fault == "intent" ? ".operator-intent.json" : ".binding.json")), "tampered");
        if (fault == "gate") File.AppendAllText(gatePath, "tampered");
        if (fault == "host")
        {
            var intentPath = Path.Combine(repo, ".engloop", "out", "postmortem-context", sessionHash + ".operator-intent.json");
            var intent = JsonNode.Parse(File.ReadAllText(intentPath))!;
            intent["HostSha256"] = new string('0', 64);
            File.WriteAllText(intentPath, intent.ToJsonString());
        }
        if (fault == "changed-head")
        {
            File.WriteAllText(Path.Combine(repo, "head.txt"), "advance");
            Git(repo, "add", "head.txt");
            Git(repo, "commit", "-m", "advance bound head");
        }
        if (fault == "changed-manifest") File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");
        if (fault == "changed-incident") File.AppendAllText(Path.Combine(repo, ".engloop", "incidents", "IN001_example.md"), "\nchanged evidence");
        if (fault == "suspended")
            RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", "Complete the postmortem for IN002.");
        var result = RunHookRaw(repo, ["post-tool", "postmortem"], JsonSerializer.Serialize(new
        {
            cwd = repo, session_id = fault == "different-session" ? "another-session" : session,
            hookEventName = fault == "wrong-event" ? "PreToolUse" : "PostToolUse",
            tool_name = fault == "terminal" ? "run_in_terminal" : "read_file",
            tool_response = new { isError = fault == "failed-tool" },
        }));
        Assert.True(Continues(result));
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", result.Output);
        Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE", result.Output);
    }

    [Fact]
    public void ExplicitOperatorPostmortem_bindsWithoutQuestionAndContinuesThroughValidatedRepairContract()
    {
        var repo = CreateRepository();
        const string session = "explicit-operator-postmortem";
        const string postmortem = ".engloop/postmortems/PM005-example.md";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        var bound = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Complete the postmortem for IN001 and implement repair items.");
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", bound.Output);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=postmortem", bound.Output);
        Assert.Contains("authorization=explicit-operator-intent", bound.Output);
        Assert.Contains("postmortem=" + postmortem, bound.Output);
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "out"), "*.confirmation.json", SearchOption.AllDirectories));
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", RunGuard(repo, session, "apply_patch", new { input = "*** Add File: " + postmortem }).Output);
        AssertPostmortemScopeDenied(RunGuard(repo, session, "apply_patch", new { input = "*** Update File: src/fixture.csproj" }));
        var blocked = RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", session);
        Assert.Contains("OPERATIONS_REPAIR_CONTINUATION_BLOCKED", blocked.Output);
        Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", blocked.Output);
        WriteValidRepairContinuationPostmortem(repo, postmortem);
        Assert.Contains("POSTMORTEM_LEARNING_OK", RunHook(repo, "postmortem", "stop", string.Empty, session).Output);
        var repair = RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", session);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", repair.Output);
        Assert.True(OperationsLearningPolicy.ValidateRepairAcceptance(repo, postmortem, "RPI001",
            ["RULE:reliability"], ".engloop/repairs/PM005-RPI001.route.json", "route", false).Passed);
    }

    [Theory]
    [InlineData("Please complete the post mortem for IN001 and implement all repair items.")]
    [InlineData("Complete the post-mortem for incident IN001, then complete all repair items.")]
    [InlineData("Go ahead and complete the postmortem for IN001 and implement repair items.")]
    public void ExplicitOperatorPostmortem_ordinaryAffirmativeRequestContinuesAllRepairs(string prompt)
    {
        var repo = CreateRepository();
        const string session = "ordinary-affirmative-request";
        const string postmortem = ".engloop/postmortems/PM005-example.md";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        AssertNativeActivation(RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt).Output, "UserPromptSubmit");
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "out"), "*.confirmation.json", SearchOption.AllDirectories));
        WriteValidRepairContinuationPostmortem(repo, postmortem);
        Assert.Contains("POSTMORTEM_LEARNING_OK", RunHook(repo, "postmortem", "stop", string.Empty, session).Output);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair",
            RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", session).Output);
    }

    [Theory]
    [InlineData("Please complete PM005 for IN001.", ".engloop/postmortems/PM005-example.md")]
    [InlineData("Please complete PM005 for IN001 at .engloop/postmortems/PM005-explicit-route.md.", ".engloop/postmortems/PM005-explicit-route.md")]
    public void ExplicitOperatorPostmortem_pmFirstRequestUsesCurrentRegistryAndPreservesPath(string prompt, string expectedPostmortem)
    {
        var repo = CreateRepository();
        const string session = "explicit-postmortem-pm-first";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");

        var result = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);

        Assert.Contains("POSTMORTEM_ROUTE_BOUND", result.Output);
        Assert.Contains("authorization=explicit-operator-intent", result.Output);
        Assert.Contains("postmortem=" + expectedPostmortem, result.Output);
    }

    [Theory]
    [InlineData("complete the plan...today FB first then fix live", "")]
    [InlineData("Complete the plan, today Facebook first then fix live.", "")]
    [InlineData("complete the plan...today FB first then fix live", "missing-history")]
    [InlineData("complete the plan...today FB first then fix live", "changed-prompt")]
    [InlineData("complete the plan...today FB first then fix live", "changed-historical-pm")]
    public void ExplicitOperatorPostmortem_freshNativeRequestPreservesHistoricalMismatchAndUsesCurrentRegistry(string prompt, string fault)
    {
        var repo = CreateRepository();
        const string session = "fresh-facebook-authorization";
        var incidentPath = Path.Combine(repo, ".engloop", "incidents", "IN023-facebook-daily-opening.md");
        var oldIncident = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md");
        File.WriteAllText(incidentPath, File.ReadAllText(oldIncident).Replace("IN001", "IN023", StringComparison.Ordinal));
        File.Delete(oldIncident);
        var registry = Path.Combine(repo, ".engloop", "numbering-registry.md");
        File.WriteAllText(registry, "| Type | Next number |\n|---|---|\n| PM | 060 |\n");
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Complete the postmortem for IN023 and implement repair items.").Output);
        var historical = Directory.GetFiles(Path.Combine(repo, ".engloop", "out"), "*.json", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);
        var lesleyPath = Path.Combine(repo, ".engloop", "postmortems", "PM060-lesley.md");
        File.WriteAllText(lesleyPath, "# PM060\n\nCovers incidents: IN024\n");
        var lesleyBytes = File.ReadAllBytes(lesleyPath);
        File.WriteAllText(registry, "| Type | Next number |\n|---|---|\n| PM | 061 |\n");
        File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "apply_patch",
            new { input = "*** Add File: .engloop/postmortems/PM061-facebook-daily-opening.md" }), "deny", allowDiagnostic: true);
        var fresh = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", fresh.Output);
        Assert.Contains("postmortem=.engloop/postmortems/PM061-facebook-daily-opening.md", fresh.Output);
        Assert.Contains("historicalPostmortem=.engloop/postmortems/PM060-facebook-daily-opening.md", fresh.Output);
        Assert.Contains("historicalScopeAccepted=false", fresh.Output);
        var freshLineagePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.reauthorization.*.json"));
        var freshLineage = JsonNode.Parse(File.ReadAllText(freshLineagePath))!;
        var freshSessionHash = freshLineage["SessionHash"]!.GetValue<string>();
        var freshHost = JsonNode.Parse(File.ReadAllText(Path.Combine(repo, ".engloop", "out", "postmortem-context", freshSessionHash + ".host.json")))!;
        Assert.Equal("postmortem-reauthorization-host", freshHost["Mode"]!.GetValue<string>());
        foreach (var entry in historical) Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key));
        Assert.Equal(lesleyBytes, File.ReadAllBytes(lesleyPath));
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", RunGuard(repo, session, "apply_patch",
            new { input = "*** Add File: .engloop/postmortems/PM061-facebook-daily-opening.md" }).Output);
        AssertPostmortemScopeDenied(RunGuard(repo, session, "apply_patch", new { input = "*** Update File: .engloop/postmortems/PM060-lesley.md" }));
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt).Output);
        Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.reauthorization.*.json"));
        if (fault.Length == 0) return;
        var lineagePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "postmortem-context"), "*.reauthorization.*.json"));
        var lineage = JsonNode.Parse(File.ReadAllText(lineagePath))!;
        if (fault == "missing-history") lineage["PreservedArtifacts"] = new JsonObject();
        if (fault == "changed-prompt")
        {
            const string changed = "Complete the postmortem for IN023.";
            lineage["Prompt"] = changed;
            lineage["PromptSha256"] = Sha256ForTest(changed);
        }
        if (fault == "changed-historical-pm") lineage["HistoricalPostmortem"] = ".engloop/postmortems/PM060-lesley.md";
        File.WriteAllText(lineagePath, lineage.ToJsonString());
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "apply_patch",
            new { input = "*** Add File: .engloop/postmortems/PM061-facebook-daily-opening.md" }), "deny", allowDiagnostic: true);
        Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE", RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt).Output);
    }

    [Theory]
    [InlineData("Complete the postmortem for IN001 or IN002.")]
    [InlineData("Complete the postmortem for the Facebook incident.")]
    [InlineData("Please complete PM006 for IN001.")]
    [InlineData("Please complete PM005 for IN001 at .engloop/postmortems/PM006-wrong.md.")]
    [InlineData("Do not complete the postmortem for IN001.")]
    [InlineData("Could we complete the postmortem for IN001?")]
    [InlineData("The operator said: Complete the postmortem for IN001.")]
    [InlineData("\"Please complete the post mortem for IN001 and implement all repair items.\"")]
    [InlineData("Plan to complete the post mortem for IN001 and implement all repair items.")]
    [InlineData("Do not complete the plan...today FB first then fix live")]
    [InlineData("Could we complete the plan, today Facebook first then fix live?")]
    [InlineData("The operator said: complete the plan...today FB first then fix live")]
    [InlineData("Complete the post-mortem for IN001, then do not implement repair items.")]
    [InlineData("")]
    public void ExplicitOperatorPostmortem_refusesAmbiguousAbsentNegatedOrQuotedIntent(string prompt)
    {
        var repo = CreateRepository();
        const string session = "explicit-operator-invalid-intent";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        var result = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", result.Output);
        AssertNoScopeAuthority(repo);
    }

    [Theory]
    [InlineData("missing-session")]
    [InlineData("missing-event")]
    [InlineData("missing-root")]
    [InlineData("subagent-event")]
    [InlineData("changed-head")]
    [InlineData("changed-manifest")]
    [InlineData("ambiguous-artifact")]
    [InlineData("missing-learning")]
    public void ExplicitOperatorPostmortem_refusesMissingSessionStaleIdentityOrInvalidEvidence(string fault)
    {
        var repo = CreateRepository();
        const string session = "explicit-operator-host-negative";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        if (fault == "changed-head")
        {
            File.WriteAllText(Path.Combine(repo, "new-head.txt"), "new head");
            Git(repo, "add", "new-head.txt");
            Git(repo, "commit", "-m", "advance host head");
        }
        if (fault == "changed-manifest") File.AppendAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "\n");
        if (fault == "ambiguous-artifact") File.Copy(Path.Combine(repo, ".engloop", "incidents", "IN001_example.md"), Path.Combine(repo, ".engloop", "incidents", "IN001-duplicate.md"));
        if (fault == "missing-learning")
        {
            var path = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md");
            File.WriteAllText(path, File.ReadAllText(path).Replace("CONSULTED", "DEFERRED", StringComparison.Ordinal));
        }
        var input = JsonNode.Parse(JsonSerializer.Serialize(new
        {
            cwd = repo,
            session_id = fault == "wrong-session" ? "another-session" : fault == "missing-session" ? "" : session,
            hookEventName = fault == "missing-event" ? "" : fault == "subagent-event" ? "SubagentStart" : "UserPromptSubmit",
            prompt = "Complete the postmortem for IN001.",
        }))!.AsObject();
        if (fault == "missing-root") input.Remove("cwd");
        var result = RunHookRaw(repo, ["initialize", "postmortem"], input.ToJsonString());
        AssertLifecycleDeferred(result, "postmortem", "initialize");
        Assert.DoesNotContain("POSTMORTEM_ROUTE_BOUND", result.Output);
        AssertNoScopeAuthority(repo);
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "apply_patch", new { input = "*** Add File: .engloop/postmortems/PM005-example.md" }), "deny", allowDiagnostic: true);
    }

    [Fact]
    public void ExplicitOperatorPostmortem_exactIN023PM060RetainsAuditEvidenceAndRegistryScope()
    {
        var repo = CreateRepository();
        const string session = "explicit-in023-pm060";
        var incidentPath = Path.Combine(repo, ".engloop", "incidents", "IN023-facebook-daily-opening-post-failure-20260929.md");
        var originalIncident = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md");
        File.WriteAllText(incidentPath, File.ReadAllText(originalIncident).Replace("IN001", "IN023", StringComparison.Ordinal));
        File.Delete(originalIncident);
        var registry = Path.Combine(repo, ".engloop", "numbering-registry.md");
        File.WriteAllText(registry, "| Type | Next number |\n|---|---|\n| IN | 024 |\n| PM | 060 |\n");
        const string postmortem = ".engloop/postmortems/PM060-20260929-facebook-daily-opening-post-failure.md";
        const string prompt = "Complete the postmortem for IN023 at " + postmortem + " and implement repair items.";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        var result = RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", result.Output);
        Assert.Contains("postmortem=" + postmortem, result.Output);
        var intentPath = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + ".operator-intent.json");
        using var intent = JsonDocument.Parse(File.ReadAllText(intentPath));
        Assert.Equal(prompt, intent.RootElement.GetProperty("Prompt").GetString());
        Assert.Equal(Sha256ForTest(prompt), intent.RootElement.GetProperty("PromptSha256").GetString());
        Assert.Equal(OperationsLearningPolicy.Sha256(incidentPath), intent.RootElement.GetProperty("IncidentSha256").GetString());
        Assert.Equal(OperationsLearningPolicy.Sha256(registry), intent.RootElement.GetProperty("RegistrySha256").GetString());
        Assert.Contains("060", File.ReadAllText(registry));
        Assert.False(File.Exists(Path.Combine(repo, postmortem.Replace('/', Path.DirectorySeparatorChar))));
        var gatePath = Path.Combine(repo, ".engloop", "out", "operations-learning-gates", Sha256ForTest(session) + ".postmortem.json");
        var gateBytes = File.ReadAllBytes(gatePath);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", prompt).Output);
        Assert.Equal(gateBytes, File.ReadAllBytes(gatePath));
        AssertPostmortemScopeDenied(RunGuard(repo, session, "apply_patch", new { input = "*** Add File: .engloop/postmortems/PM061-unrelated.md" }));
    }

    [Theory]
    [InlineData("intent")]
    [InlineData("binding")]
    [InlineData("changed-head")]
    [InlineData("changed-incident")]
    [InlineData("changed-request")]
    public void ExplicitOperatorPostmortem_tamperedOrChangedBindingDeniesMutationAndRepair(string fault)
    {
        var repo = CreateRepository();
        const string session = "explicit-bound-negative";
        RunHostPostmortem(repo, session, "start", "SessionStart", "");
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit",
            "Complete the postmortem for IN001 and implement repair items.").Output);
        if (fault == "intent" || fault == "binding")
        {
            var suffix = fault == "intent" ? ".operator-intent.json" : ".binding.json";
            var path = Path.Combine(repo, ".engloop", "out", "postmortem-context", Sha256ForTest(session) + suffix);
            File.AppendAllText(path, "\n");
            if (fault == "binding")
            {
                var binding = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                binding["Authorization"] = "unverified";
                File.WriteAllText(path, binding.ToJsonString());
            }
        }
        if (fault == "changed-head")
        {
            File.WriteAllText(Path.Combine(repo, "head.txt"), "advance");
            Git(repo, "add", "head.txt");
            Git(repo, "commit", "-m", "advance bound head");
        }
        if (fault == "changed-incident") File.AppendAllText(Path.Combine(repo, ".engloop", "incidents", "IN001_example.md"), "\nChanged incident evidence\n");
        if (fault == "changed-request")
            AssertLifecycleDeferred(RunHostPostmortem(repo, session, "initialize", "UserPromptSubmit", "Complete the postmortem for IN002."), "postmortem", "initialize");
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "apply_patch", new { input = "*** Add File: .engloop/postmortems/PM005-example.md" }), "deny", allowDiagnostic: true);
        var repair = RunHook(repo, "repair", "initialize", "ELK_TRUSTED_AUTOPILOT_REPAIR_CONTINUATION", session);
        Assert.Contains("OPERATIONS_REPAIR_CONTINUATION_BLOCKED", repair.Output);
        Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE mode=repair", repair.Output);
    }

    private static (int ExitCode, string Output, string Error) RunHostPostmortem(string repo, string session, string action, string hookEvent, string prompt)
        => RunHookRaw(repo, [action, "postmortem"], JsonSerializer.Serialize(new { cwd = repo, session_id = session, hookEventName = hookEvent, prompt }));

    private static string ScopePrompt(string mode) => mode switch
    {
        "incident" => "--incident .engloop/incidents/IN001_example.md",
        "postmortem" => "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md",
        _ => "--phase route --postmortem .engloop/postmortems/PM005_example.md --rpi RPI001 --rules RULE:reliability --acceptance .engloop/repairs/PM005-RPI001.route.json",
    };

    [Theory]
    [InlineData("--incidents IN001")]
    [InlineData("--incidents IN001 --postmortem '.engloop/postmortems/PM005_example.md.'")]
    [InlineData("--incidents IN002 --postmortem .engloop/postmortems/PM006_changed.md")]
    public void InvalidFollowup_retainsOriginalGateButSuspendsMutationsUntilExactScopeReturns(string prompt)
    {
        var repo = CreateRepository();
        const string session = "invalid-followup";
        var originalPrompt = ScopePrompt("postmortem");
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", RunHook(repo, "postmortem", "initialize", originalPrompt, session).Output);
        var gatePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json"));
        var gateBytes = File.ReadAllBytes(gatePath);
        AssertLifecycleDeferred(RunHook(repo, "postmortem", "initialize", prompt, session), "postmortem", "initialize");
        Assert.Equal(gateBytes, File.ReadAllBytes(gatePath));
        Assert.True(File.Exists(gatePath + ".context-required"));
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "apply_patch", new { }), "deny", allowDiagnostic: true);
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "read_file", new { }), "allow", allowDiagnostic: true);
        AssertLifecycleDeferred(RunHook(repo, "postmortem", "subagent-stop", string.Empty, session), "postmortem", "subagent-stop");
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", RunHook(repo, "postmortem", "initialize", originalPrompt, session).Output);
        Assert.False(File.Exists(gatePath + ".context-required"));
        Assert.Equal(gateBytes, File.ReadAllBytes(gatePath));
    }

    [Theory]
    [InlineData("--collection")]
    [InlineData("--confirmation-receipt")]
    public void BinderTransport_pathsAreExactAndNeverTrimReceiptIdentities(string option)
    {
        var repo = CreateRepository();
        const string session = "receipt-exact-path";
        const string postmortem = ".engloop/postmortems/PM005-exact.v1.md";
        var collection = BeginCollection(repo, session);
        var confirmation = AnswerCollection(repo, session, ["IN001"], postmortem, "Confirm");
        var args = new[] { "bind", "--collection", collection.Path, "--token", collection.Token,
            "--incidents", "IN001", "--postmortem", postmortem, "--confirmation-receipt", confirmation.ReceiptPath };
        var index = Array.IndexOf(args, option) + 1;
        var original = args[index];
        args[index] = " " + original;
        var rejected = RunPostmortemRoute(repo, args);
        Assert.Equal(1, rejected.ExitCode);
        Assert.Contains("path-invalid", rejected.Error);
        AssertNoScopeAuthority(repo, allowConfirmation: true);
        args[index] = original;
        var bound = RunPostmortemRoute(repo, args);
        Assert.Equal(0, bound.ExitCode);
        var gatePath = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "operations-learning-gates"), "*.json"));
        using var gate = JsonDocument.Parse(File.ReadAllText(gatePath));
        Assert.Equal(postmortem, gate.RootElement.GetProperty("Postmortem").GetString());
    }

    private static void AssertNoScopeAuthority(string repo, bool allowConfirmation = false)
    {
        var root = Path.Combine(repo, ".engloop", "out");
        if (!Directory.Exists(root)) return;
        Assert.DoesNotContain(Directory.GetFiles(root, "*", SearchOption.AllDirectories),
            path => path.Contains("operations-learning-gates", StringComparison.Ordinal)
                || path.EndsWith(".binding.json", StringComparison.Ordinal)
                || !allowConfirmation && path.EndsWith(".confirmation.json", StringComparison.Ordinal));
    }

    private static void WriteValidRepairContinuationPostmortem(string repo, string postmortem)
    {
        var cardsDirectory = Path.Combine(repo, ".engloop", "learnings", "cards");
        Directory.CreateDirectory(cardsDirectory);
        File.WriteAllText(Path.Combine(repo, ".engloop", "postmortems", "PM001-prior.md"), "# PM001\n\n## Learnings\n\n- **LEARN001** — Keep reliability rules executable.\n");
        File.WriteAllText(Path.Combine(cardsDirectory, "reliability.md"), "# Card: Reliability\n\n## Source learnings\n\nPM001/LEARN001\n\n## Tensions\n\nNone known.\n");
        File.WriteAllText(Path.Combine(repo, "LEARNINGS.md"), "# Learnings\n\n- [reliability](.engloop/learnings/cards/reliability.md)\n");

        var northstarHash = OperationsLearningPolicy.Sha256(Path.Combine(repo, "NORTHSTAR.md"));
        var learningsHash = OperationsLearningPolicy.Sha256(Path.Combine(repo, "LEARNINGS.md"));
        var incidentHash = OperationsLearningPolicy.Sha256(Path.Combine(repo, ".engloop", "incidents", "IN001_example.md"));
        var pyramidDigest = OperationsLearningPolicy.ComputePyramidDigest(repo, "PM005");
        var text = $$"""
        # PM005: Trusted continuation fixture

        - **Status:** COMPLETE

        ## Selected stabilized incidents

        | Incident ID | Path | SHA-256 |
        |---|---|---|
        | `IN001` | `.engloop/incidents/IN001_example.md` | `{{incidentHash}}` |

        ## Root causes

        The fixture contains one bounded repair contract.

        ## SEK Test-Escape Analysis

        - **SEK applicability:** `NOT-RELEVANT`
        - **SEK applicability rationale:** This fixture exercises host-level continuation, not stateful vertical behavior.
        - **SEK version:** `NOT-REQUIRED`
        - **SEK verification class:** `INFRASTRUCTURE`
        - **SEK escape class:** `NOT-RELEVANT`
        - **SEK scenario ID:** `NOT-REQUIRED`
        - **SEK model paths:** `NOT-REQUIRED`
        - **SEK CORD paths:** `NOT-REQUIRED`
        - **SEK generated suite path:** `NOT-REQUIRED`
        - **Why SEK tests missed the incident:** The test concerns hook orchestration and receipt validation rather than modeled product behavior.
        - **Required model/CORD repair:** `NOT-REQUIRED`

        ## Direction and Learning-Pyramid Consultation

        - **North Star path:** `NORTHSTAR.md`
        - **North Star SHA-256:** `{{northstarHash}}`
        - **Direction alignment:** `ALIGNED`
        - **Direction decision:** Preserve the exact accepted repair and fail-closed evidence gates.
        - **Learnings index path:** `LEARNINGS.md`
        - **Learnings index SHA-256:** `{{learningsHash}}`
        - **Pyramid digest:** `{{pyramidDigest}}`
        - **Pyramid decision:** `NO-CHANGE`
        - **Pyramid rationale:** The existing rule is sufficient; this fixture introduces no source learning.
        - **Historical coverage decision:** `NO-CHANGE`
        - **Historical coverage path:** `NOT-REQUIRED`
        - **Changed pyramid paths:** `NOT-REQUIRED`
        - **Retrieval impact:** `UNCHANGED`
        - **Retrieval evidence:** `NOT-REQUIRED`
        - **Retrieval rationale:** No rule meaning or retrieval query changed.

        ### Rule dispositions

        | Rule ID | Card ID | Source IDs | Disposition | Incident evidence | Pyramid action |
        |---|---|---|---|---|---|
        | `RULE:reliability` | `reliability` | `PM001/LEARN001` | `REINFORCED` | The selected incident reinforces the existing reliability rule. | Retain current meaning and bind repair execution to its gate. |

        ## Learnings

        - **No accepted source learning:** The existing reliability rule already covers this fixture.

        ## Repair Items

        | RPI | Description | Status |
        |---|---|---|
        | RPI001 | Preserve the exact route contract. | OPEN |

        ### RPI001 learning contract

        - **Rule IDs:** `RULE:reliability`
        - **Executable gate:** `["dotnet", "--version"]`
        - **Gate proves:** The route remains bound to the exact accepted scope.
        - **SEK applicability:** `NOT-RELEVANT`
        - **SEK scenario ID:** `NOT-REQUIRED`
        - **SEK repair requirement:** `NOT-REQUIRED`
        - **SEK verification gate:** `NOT-REQUIRED`
        """;
        File.WriteAllText(Path.Combine(repo, postmortem.Replace('/', Path.DirectorySeparatorChar)), text);
    }

    private static string PrepareValidRepairContinuation(string repo, string session, string request)
    {
        AssertNoLifecycleWarning(RunHook(repo, "incident", "initialize", request, session));
        const string postmortem = ".engloop/postmortems/PM005-example.md";
        _ = BeginCollection(repo, session);
        var confirmation = AnswerCollection(repo, session, ["IN001"], postmortem, "Confirm", autoBind: true);
        AssertPostToolContext(confirmation.Result, "POSTMORTEM_ROUTE_BOUND", "OPERATIONS_LEARNING_SCOPE_ACTIVE");
        WriteValidRepairContinuationPostmortem(repo, postmortem);
        var validation = OperationsLearningPolicy.ValidatePostmortem(repo, postmortem, ["IN001"]);
        Assert.True(validation.Passed, string.Join(',', validation.Failures));
        return postmortem;
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
        File.WriteAllText(Path.Combine(repo, ".engloop", "numbering-registry.md"), "# Numbering Registry\n\n| Prefix | Scope | Last used | Notes |\n|---|---|---:|---|\n| `IN` | Incidents | `IN001` | incidents |\n| `PM` | Post-mortems | `PM004` | PM001 and PM002 are historical sources |\n");
        File.WriteAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), $"{{\"version\":1,\"isRoot\":true,\"tools\":{{\"engloopkit\":{{\"version\":\"{ToolVersion}\",\"commands\":[\"engloopkit\"]}}}}}}\n");
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

    private static (string Path, string Token) BeginCollection(string repo, string session)
    {
        var result = RunHook(repo, "postmortem", "initialize", "Complete the postmortem for the known incident.", session);
        AssertNoLifecycleWarning(result);
        var relative = $".engloop/out/postmortem-context/{Sha256ForTest(session)}.json";
        using var collection = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar))));
        return (relative, collection.RootElement.GetProperty("Token").GetString()!);
    }

    private static ((int ExitCode, string Output, string Error) Result, string ReceiptPath) AnswerCollection(string repo, string session, string[] incidents, string postmortem, string decision, bool answerByQuestion = false, bool autoBind = false)
    {
        var header = "Confirm postmortem";
        var question = ConfirmationQuestion(incidents, postmortem);
        var questionText = question["questions"]![0]!["question"]!.GetValue<string>();
        var response = ConfirmationResponse(answerByQuestion ? questionText : header, decision);
        if (decision == "Confirm" && !autoBind)
            return WriteConfirmationForBinderTest(repo, session, incidents, postmortem, question, response);
        var result = RunPostTool(repo, session, "vscode_askQuestions", question, response);
        var receipt = string.Empty;
        if (decision == "Confirm" && result.Output.Contains("bindingReceipt=", StringComparison.Ordinal))
            receipt = result.Output[(result.Output.IndexOf("bindingReceipt=", StringComparison.Ordinal) + "bindingReceipt=".Length)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return (result, receipt);
    }

    private static ((int ExitCode, string Output, string Error) Result, string ReceiptPath) WriteConfirmationForBinderTest(
        string repo, string session, string[] incidents, string postmortem, JsonObject question, JsonObject response)
    {
        var sessionHash = Sha256ForTest(session);
        var collectionRelative = $".engloop/out/postmortem-context/{sessionHash}.json";
        using var collection = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, collectionRelative.Replace('/', Path.DirectorySeparatorChar))));
        var value = collection.RootElement;
        var receiptRelative = $".engloop/out/postmortem-context/{sessionHash}.confirmation.json";
        var receipt = new
        {
            SchemaVersion = "1.0",
            SessionHash = sessionHash,
            Head = value.GetProperty("Head").GetString(),
            AssemblySha256 = value.GetProperty("AssemblySha256").GetString(),
            ManifestSha256 = value.GetProperty("ManifestSha256").GetString(),
            CollectionToken = value.GetProperty("Token").GetString(),
            ToolUseId = "binder-test-" + Guid.NewGuid().ToString("N"),
            Incidents = incidents,
            Postmortem = postmortem,
            QuestionSha256 = Sha256ForTest(question.ToJsonString()),
            ResponseSha256 = Sha256ForTest(response.ToJsonString()),
        };
        File.WriteAllText(Path.Combine(repo, receiptRelative.Replace('/', Path.DirectorySeparatorChar)), JsonSerializer.Serialize(receipt));
        var result = (0, JsonSerializer.Serialize(new { @continue = true, systemMessage = "POSTMORTEM_ROUTE_CONFIRMED" }), string.Empty);
        return (result, receiptRelative);
    }

    private static JsonObject ConfirmationQuestion(string[] incidents, string postmortem)
        => new()
        {
            ["questions"] = new JsonArray
            {
                new JsonObject
                {
                    ["header"] = "Confirm postmortem",
                    ["question"] = $"Use incidents {string.Join(',', incidents)} and create {postmortem}?",
                    ["multiSelect"] = false,
                    ["allowFreeformInput"] = false,
                    ["options"] = new JsonArray
                    {
                        new JsonObject { ["label"] = "Confirm" },
                        new JsonObject { ["label"] = "Choose different incident/path" },
                        new JsonObject { ["label"] = "Cancel" },
                    },
                },
            },
        };

    private static JsonObject ConfirmationResponse(string key, params string[] decisions)
        => new()
        {
            ["answers"] = new JsonObject
            {
                [key] = new JsonObject { ["selected"] = new JsonArray(decisions.Select(decision => (JsonNode?)JsonValue.Create(decision)).ToArray()) },
            },
        };

    private static (int ExitCode, string Output, string Error) RunPostTool(string repo, string session, string toolName, object toolInput, object toolResponse, string? toolUseId = null)
        => RunHookRaw(repo, ["post-tool", "postmortem"], JsonSerializer.Serialize(new
        {
            cwd = repo,
            session_id = session,
            tool_name = toolName,
            tool_input = toolInput,
            tool_response = toolResponse,
            tool_use_id = toolUseId ?? "question-" + Guid.NewGuid().ToString("N"),
        }));

    private static (int ExitCode, string Output, string Error) RunGuard(string repo, string session, string toolName, object toolInput)
        => RunHookRaw(repo, ["guard", "postmortem"], JsonSerializer.Serialize(new { cwd = repo, session_id = session, tool_name = toolName, tool_input = toolInput }));

    private static void AssertPostmortemCollectionDecision((int ExitCode, string Output, string Error) result, string decision, bool allowDiagnostic = false)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        if (!allowDiagnostic) Assert.False(json.RootElement.TryGetProperty("systemMessage", out _));
        var specific = json.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("PreToolUse", specific.GetProperty("hookEventName").GetString());
        Assert.Equal(decision, specific.GetProperty("permissionDecision").GetString());
    }

    private static void AssertSubagentStopDeferred((int ExitCode, string Output, string Error) result, string expected)
    {
        AssertLifecycleDeferred(result, "postmortem", "subagent-stop");
        using var json = JsonDocument.Parse(result.Output);
        Assert.Contains(expected, json.RootElement.GetProperty("systemMessage").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertLifecycleDeferred((int ExitCode, string Output, string Error) result, string mode, string phase)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var root = json.RootElement;
        Assert.Equal(JsonValueKind.Null, root.GetProperty("stopReason").ValueKind);
        Assert.False(root.TryGetProperty("hookSpecificOutput", out _));
        var message = root.GetProperty("systemMessage").GetString()!;
        using var details = JsonDocument.Parse(message[message.IndexOf('{')..]);
        var diagnostic = details.RootElement;
        Assert.False(diagnostic.GetProperty("completionAccepted").GetBoolean());
        Assert.Equal(mode, diagnostic.GetProperty("mode").GetString());
        Assert.Equal(phase, diagnostic.GetProperty("phase").GetString());
        Assert.NotEmpty(diagnostic.GetProperty("diagnosticCode").GetString()!);
        Assert.InRange(diagnostic.GetProperty("diagnosticCode").GetString()!.Length, 1, 4110);
        Assert.InRange(diagnostic.GetProperty("diagnostic").GetString()!.Length, 1, 4110);
        Assert.True(Guid.TryParseExact(diagnostic.GetProperty("correlation").GetString(), "N", out _));
        Assert.Contains("retained", diagnostic.GetProperty("unresolvedState").GetString());
        var remediation = diagnostic.GetProperty("remediation").GetString()!;
        if (message.StartsWith("OPERATIONS_POSTMORTEM_CONFIRMATION_UNVERIFIED", StringComparison.Ordinal))
        {
            Assert.Contains("Retry the prompt once", remediation);
            Assert.Contains("Do not ask them to select an unseen dialog", remediation);
        }
        else
        {
            Assert.Contains("Stage 22", remediation);
            Assert.Contains("Stage 50", remediation);
            Assert.Contains("any time without binder tokens", remediation);
            Assert.Contains("Stop does not veto navigation", remediation);
        }
        Assert.DoesNotContain("OPERATIONS_LEARNING_SCOPE_ACTIVE", message);
        Assert.DoesNotContain("INCIDENT_CONTEXT_OK", message);
        Assert.DoesNotContain("POSTMORTEM_LEARNING_OK", message);
        Assert.DoesNotContain("REPAIR_LEARNING_OK", message);
    }

    private static string Sha256ForTest(string value)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static (int ExitCode, string Output, string Error) RunPostmortemRoute(string repo, string[] args)
    {
        var originalDirectory = Environment.CurrentDirectory;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Environment.CurrentDirectory = repo;
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = Program.Main(["postmortem-route", .. args]);
            return (exitCode, output.ToString().Trim(), error.ToString().Trim());
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
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

    private static void AssertNoLifecycleWarning((int ExitCode, string Output, string Error) result)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.False(json.RootElement.TryGetProperty("systemMessage", out _));
        Assert.False(json.RootElement.TryGetProperty("hookSpecificOutput", out _));
        Assert.DoesNotContain("POSTMORTEM_LEARNING_OK", result.Output, StringComparison.Ordinal);
    }

    private static void AssertPostToolContext((int ExitCode, string Output, string Error) result, params string[] expected)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.False(json.RootElement.TryGetProperty("systemMessage", out _));
        Assert.Contains("PostToolUse", json.RootElement.GetProperty("hookSpecificOutput").GetProperty("hookEventName").GetString());
        var localContext = json.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString()!;
        var copilotContext = json.RootElement.GetProperty("additionalContext").GetString()!;
        foreach (var marker in expected)
        {
            Assert.Contains(marker, localContext, StringComparison.Ordinal);
            Assert.Contains(marker, copilotContext, StringComparison.Ordinal);
        }
    }

    private static void AssertPostmortemScopeDenied((int ExitCode, string Output, string Error) result)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.False(json.RootElement.TryGetProperty("systemMessage", out _));
        var specific = json.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("PreToolUse", specific.GetProperty("hookEventName").GetString());
        Assert.Equal("deny", specific.GetProperty("permissionDecision").GetString());
        var reason = specific.GetProperty("permissionDecisionReason").GetString()!;
        Assert.Contains("No action ran", reason, StringComparison.Ordinal);
        Assert.Contains("does not authorize", reason, StringComparison.Ordinal);
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
