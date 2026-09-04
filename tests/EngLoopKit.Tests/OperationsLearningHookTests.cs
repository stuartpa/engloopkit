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
            Assert.Contains("read-only", specific.GetProperty("permissionDecisionReason").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("confirmation", specific.GetProperty("permissionDecisionReason").GetString(), StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void NaturalLanguagePostmortem_collectsReadOnlyContextAndBindsRegistryBackedProposalAfterConfirmation()
    {
        var repo = CreateRepository();
        const string session = "natural-language-postmortem";

        var initialized = RunHook(repo, "postmortem", "initialize", "Complete the postmortem for the known stencil incident.", session);
        Assert.True(Continues(initialized), initialized.Output + initialized.Error);
        using var initializedJson = JsonDocument.Parse(initialized.Output);
        var message = initializedJson.RootElement.GetProperty("systemMessage").GetString()!;
        Assert.Contains("OPERATIONS_POSTMORTEM_CONTEXT_COLLECTION_ACTIVE", message);
        var collection = JsonDocument.Parse(message[message.IndexOf('{')..]).RootElement;
        var collectionPath = collection.GetProperty("collectionPath").GetString()!;
        var token = collection.GetProperty("token").GetString()!;
        Assert.True(File.Exists(Path.Combine(repo, collectionPath.Replace('/', Path.DirectorySeparatorChar))));

        AssertPostmortemCollectionDecision(RunGuard(repo, session, "read_file", new { filePath = Path.Combine(repo, ".engloop", "incidents", "IN001_example.md") }), "allow");
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "vscode_askQuestions", new { questions = Array.Empty<object>() }), "allow");
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "apply_patch", new { input = "*** Begin Patch" }), "deny");
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "run_in_terminal", new { command = "git status --short" }), "deny");

        var postmortem = ".engloop/postmortems/PM005-known-incident.md";
        var confirmation = AnswerCollection(repo, session, ["IN001"], postmortem, "Confirm", answerByQuestion: true);
        Assert.Contains("POSTMORTEM_ROUTE_CONFIRMED", confirmation.Result.Output);
        var command = $"dotnet tool run engloopkit -- postmortem-route bind --collection {collectionPath} --token {token} --incidents IN001 --postmortem {postmortem} --confirmation-receipt {confirmation.ReceiptPath}";
        AssertPostmortemCollectionDecision(RunGuard(repo, session, "run_in_terminal", new { command }), "allow");
        var bound = RunPostmortemRoute(repo, ["bind", "--collection", collectionPath, "--token", token, "--incidents", "IN001", "--postmortem", postmortem, "--confirmation-receipt", confirmation.ReceiptPath]);
        Assert.Equal(0, bound.ExitCode);
        Assert.Contains("POSTMORTEM_ROUTE_BOUND", bound.Output);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", bound.Output);
        Assert.False(File.Exists(Path.Combine(repo, collectionPath.Replace('/', Path.DirectorySeparatorChar))));

        var editAllowed = RunGuard(repo, session, "apply_patch", new { input = "*** Begin Patch" });
        Assert.True(Continues(editAllowed), editAllowed.Output + editAllowed.Error);
        using var editJson = JsonDocument.Parse(editAllowed.Output);
        Assert.False(editJson.RootElement.TryGetProperty("hookSpecificOutput", out _));

        var stop = RunHook(repo, "postmortem", "stop", string.Empty, session);
        Assert.False(Continues(stop));
        Assert.Contains("validation failed", stop.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NaturalLanguagePostmortem_delegatedLifecycleBlocksUntilBindOrExplicitCancellation()
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
        AssertSubagentStopBlocked(RunHook(repo, "postmortem", "subagent-stop", string.Empty, session), "confirmation");

        var cancelled = AnswerCollection(repo, session, ["IN001"], ".engloop/postmortems/PM005-cancelled.md", "Cancel");
        Assert.Contains("POSTMORTEM_ROUTE_CANCELLED", cancelled.Result.Output);
        var completedCancellation = RunHook(repo, "postmortem", "subagent-stop", string.Empty, session);
        Assert.True(Continues(completedCancellation), completedCancellation.Output + completedCancellation.Error);
        using var cancellationJson = JsonDocument.Parse(completedCancellation.Output);
        Assert.Contains("POSTMORTEM_ROUTE_CANCELLED", cancellationJson.RootElement.GetProperty("systemMessage").GetString());
        Assert.False(cancellationJson.RootElement.TryGetProperty("hookSpecificOutput", out _));

        var invalidRepo = CreateRepository();
        const string invalidSession = "delegated-invalid-postmortem";
        var invalidCollection = BeginCollection(invalidRepo, invalidSession);
        var invalidPostmortem = ".engloop/postmortems/PM005-invalid.md";
        var invalidConfirmation = AnswerCollection(invalidRepo, invalidSession, ["IN001"], invalidPostmortem, "Confirm");
        var bound = RunPostmortemRoute(invalidRepo, ["bind", "--collection", invalidCollection.Path, "--token", invalidCollection.Token, "--incidents", "IN001", "--postmortem", invalidPostmortem, "--confirmation-receipt", invalidConfirmation.ReceiptPath]);
        Assert.Equal(0, bound.ExitCode);
        File.WriteAllText(Path.Combine(invalidRepo, ".engloop", "postmortems", "PM005-invalid.md"), "# invalid PM\n");
        AssertSubagentStopBlocked(RunHook(invalidRepo, "postmortem", "subagent-stop", string.Empty, invalidSession), "validation failed");
    }

    [Fact]
    public void NaturalLanguagePostmortemBinder_rejectsUnstabilizedIncidentWrongRegistryPathAndWrongToken()
    {
        var activeRepo = CreateRepository();
        File.WriteAllText(Path.Combine(activeRepo, ".engloop", "incidents", "IN001_example.md"), "# IN001\n\n- **Status:** ACTIVE\n");
        var active = BeginCollection(activeRepo, "active-natural-language");
        var activePostmortem = ".engloop/postmortems/PM005-active.md";
        var activeConfirmation = AnswerCollection(activeRepo, "active-natural-language", ["IN001"], activePostmortem, "Confirm");
        var activeResult = RunPostmortemRoute(activeRepo, ["bind", "--collection", active.Path, "--token", active.Token, "--incidents", "IN001", "--postmortem", activePostmortem, "--confirmation-receipt", activeConfirmation.ReceiptPath]);
        Assert.NotEqual(0, activeResult.ExitCode);
        Assert.Contains("incident", activeResult.Error, StringComparison.OrdinalIgnoreCase);

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
        AssertSubagentStopBlocked(RunHook(repo, "postmortem", "subagent-stop", string.Empty, session), "confirmation");

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
        Assert.Contains("OPERATIONS_POSTMORTEM_CONFIRMATION_REJECTED", malformed.Output);
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
            AssertPostmortemCollectionDecision(guarded, "deny");
            Assert.Contains(expected, guarded.Output, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(full));
        }

        DenyMutation("SchemaVersion", "2.0", "schema-invalid");
        DenyMutation("SessionHash", "bad", "session-invalid");
        DenyMutation("Token", "bad", "token-invalid");
        DenyMutation("Head", new string('0', 40), "head-changed");
        DenyMutation("AssemblyPath", "wrong", "assembly-path");
        DenyMutation("AssemblySha256", new string('0', 64), "assembly-hash");
        DenyMutation("ToolVersion", "0.0.0", "tool-version");
        DenyMutation("ManifestPath", "wrong", "manifest-path");
        DenyMutation("ManifestSha256", new string('0', 64), "manifest-hash");

        var corruptRepo = CreateRepository();
        var corrupt = BeginCollection(corruptRepo, "corrupt-collection");
        var corruptFull = Path.Combine(corruptRepo, corrupt.Path.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(corruptFull, "{");
        AssertPostmortemCollectionDecision(RunGuard(corruptRepo, "corrupt-collection", "read_file", new { }), "deny");

        var noScopeDirect = CreateRepository();
        AssertPostmortemContextRequired(RunHook(noScopeDirect, "postmortem", "stop", string.Empty, "no-scope-direct"), "stop", "gate-missing");
        AssertSubagentStopBlocked(RunHook(noScopeDirect, "postmortem", "subagent-stop", string.Empty, "no-scope-subagent"), "no validated postmortem scope");
        AssertSubagentStopBlocked(RunHookRaw(noScopeDirect, ["subagent-stop", "postmortem"], "not-json"), "invalid JSON");
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
            Assert.Contains("OPERATIONS_POSTMORTEM_CONFIRMATION_REJECTED", result.Output);
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
        Assert.Contains("POSTMORTEM_ROUTE_CONFIRMED", flattened.Output);
        Assert.True(File.Exists(receiptPath));
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
        Assert.Contains("POSTMORTEM_ROUTE_CONFIRMED", camel.Output);
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
        AssertSubagentStopBlocked(RunHook(suspendedRepo, "postmortem", "subagent-stop", string.Empty, suspendedSession), "context is incomplete");
    }

    [Fact]
    public void NaturalLanguagePostmortem_collectionReadFailuresDenyWithBoundedDiagnostics()
    {
        var nullRepo = CreateRepository();
        var nullCollection = BeginCollection(nullRepo, "null-collection");
        File.WriteAllText(Path.Combine(nullRepo, nullCollection.Path.Replace('/', Path.DirectorySeparatorChar)), "null");
        AssertPostmortemCollectionDecision(RunGuard(nullRepo, "null-collection", "read_file", new { }), "deny");

        if (OperatingSystem.IsWindows())
        {
            var lockedRepo = CreateRepository();
            var lockedCollection = BeginCollection(lockedRepo, "locked-collection");
            var lockedPath = Path.Combine(lockedRepo, lockedCollection.Path.Replace('/', Path.DirectorySeparatorChar));
            using var stream = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var guarded = RunGuard(lockedRepo, "locked-collection", "read_file", new { });
            AssertPostmortemCollectionDecision(guarded, "deny");
            Assert.Contains("storage-unavailable", guarded.Output);
        }

        var longRepo = CreateRepository();
        const string longSession = "long-subagent-diagnostic";
        Assert.True(Continues(RunHook(longRepo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005-long.md", longSession)));
        var gate = Assert.Single(Directory.GetFiles(Path.Combine(longRepo, ".engloop", "out", "operations-learning-gates"), "*.json"));
        File.WriteAllText(gate + ".context-required", "operations-hook-option-missing:--postmortem" + new string('a', 5000));
        var blocked = RunHook(longRepo, "postmortem", "subagent-stop", string.Empty, longSession);
        AssertSubagentStopBlocked(blocked, "context is incomplete");
        Assert.Contains("...[truncated]", blocked.Output);
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

        var emptyFollowup = RunHook(repo, "postmortem", "initialize", string.Empty, "pm-session");
        Assert.True(Continues(emptyFollowup), emptyFollowup.Output);

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
        File.WriteAllText(Path.Combine(repo, ".engloop", "numbering-registry.md"), "# Numbering Registry\n\n| Prefix | Scope | Last used | Notes |\n|---|---|---:|---|\n| `IN` | Incidents | `IN001` | incidents |\n| `PM` | Post-mortems | `PM004` | PM001 and PM002 are historical sources |\n");
        File.WriteAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "{\"version\":1,\"isRoot\":true,\"tools\":{\"engloopkit\":{\"version\":\"1.16.0\",\"commands\":[\"engloopkit\"]}}}\n");
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
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var message = json.RootElement.GetProperty("systemMessage").GetString()!;
        var collection = JsonDocument.Parse(message[message.IndexOf('{')..]).RootElement;
        return (collection.GetProperty("collectionPath").GetString()!, collection.GetProperty("token").GetString()!);
    }

    private static ((int ExitCode, string Output, string Error) Result, string ReceiptPath) AnswerCollection(string repo, string session, string[] incidents, string postmortem, string decision, bool answerByQuestion = false)
    {
        var header = "Confirm postmortem";
        var question = ConfirmationQuestion(incidents, postmortem);
        var questionText = question["questions"]![0]!["question"]!.GetValue<string>();
        var response = ConfirmationResponse(answerByQuestion ? questionText : header, decision);
        var result = RunPostTool(repo, session, "vscode_askQuestions", question, response);
        var receipt = string.Empty;
        if (decision == "Confirm" && result.Output.Contains("receipt=", StringComparison.Ordinal))
            receipt = result.Output[(result.Output.IndexOf("receipt=", StringComparison.Ordinal) + "receipt=".Length)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return (result, receipt);
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

    private static void AssertPostmortemCollectionDecision((int ExitCode, string Output, string Error) result, string decision)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var specific = json.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("PreToolUse", specific.GetProperty("hookEventName").GetString());
        Assert.Equal(decision, specific.GetProperty("permissionDecision").GetString());
    }

    private static void AssertSubagentStopBlocked((int ExitCode, string Output, string Error) result, string expected)
    {
        Assert.True(Continues(result), result.Output + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var specific = json.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("SubagentStop", specific.GetProperty("hookEventName").GetString());
        Assert.Equal("block", specific.GetProperty("decision").GetString());
        Assert.Contains(expected, specific.GetProperty("reason").GetString(), StringComparison.OrdinalIgnoreCase);
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
