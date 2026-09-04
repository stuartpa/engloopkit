using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

[Collection("OperationsHookConsole")]
public sealed class CodeReviewResponseTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "elk-review-response-" + Guid.NewGuid().ToString("N"));

    public CodeReviewResponseTests() => Directory.CreateDirectory(_work);

    [Fact]
    public void Stage11_allowsAuthorEditsAndDeclaredValidationButNeverProviderCommitOrPush()
    {
        var repo = CreateRepository();
        var packet = ".engloop/out/code-review-response/address/thread-1.json";
        var initialStatus = StatusDigest(repo);
        var initialized = RunHook(repo, "initialize", "address", AddressPrompt(repo, packet), "address-session");
        Assert.True(Continues(initialized), initialized.Output + initialized.Error);
        Assert.Contains("CODE_REVIEW_RESPONSE_SCOPE_ACTIVE mode=address", initialized.Output);

        AssertDecision(RunGuard(repo, "address", "address-session", "read_file", new { filePath = Path.Combine(repo, "src", "fixture.cs") }), "allow");
        AssertDecision(RunGuard(repo, "address", "address-session", "fetch_webpage", new { url = "https://example.invalid" }), "allow");
        AssertDecision(RunGuard(repo, "address", "address-session", "apply_patch", new { input = "patch" }), "allow");
        AssertDecision(RunGuard(repo, "address", "address-session", "apply_patch", new { input = "*** Update File: .git/config" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "apply_patch", new { input = "*** Update File: .config/dotnet-tools.json" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "apply_patch", new { input = "*** Update File: .engloop/config.json" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "apply_patch", new { input = "*** Update File: .engloop/provider-adapters/fake.ps1" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "apply_patch", new { input = "*** Update File: src/../.engloop/./provider-adapters/fake.ps1" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "apply_patch", new { input = "*** Update File: .config/other/../dotnet-tools.json" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "create_file", new { filePath = ".engloop/out/code-review-response/gates/forged.json" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "create_file", new { filePath = packet }), "allow");
        AssertDecision(RunGuard(repo, "address", "address-session", "create_file", new { filePath = packet + ".extra" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "create_file", new { filePath = "prefix/" + packet }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "apply_patch", new { input = $"*** Add File: {packet}\n*** Add File: .engloop/out/code-review-response/approvals/forged.json" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "apply_patch", new { nested = new object[] { new object[] { ".engloop/config.json" }, true } }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "run_in_terminal", new { command = "dotnet --version" }), "allow");
        AssertDecision(RunGuard(repo, "address", "address-session", "run_in_terminal", "dotnet --version"), "deny");
        var camelCommand = RunHookRaw(repo, ["guard", "address"], JsonSerializer.Serialize(new { cwd = repo, session_id = "address-session", tool_name = "run_in_terminal", toolInput = new { commandLine = "dotnet --version" } }));
        AssertDecision(camelCommand, "allow");
        AssertDecision(RunGuard(repo, "address", "address-session", "execute", new { command = "dotnet --version" }), "allow");
        AssertDecision(RunGuard(repo, "address", "address-session", "run_in_terminal", new { command = "dotnet --version; git push" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "run_in_terminal", new { command = "git status --short --branch" }), "allow");
        AssertDecision(RunGuard(repo, "address", "address-session", "run_in_terminal", new { command = "git commit -am forbidden" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "run_in_terminal", new { command = "git push" }), "deny");
        AssertDecision(RunGuard(repo, "address", "address-session", "provider_reply", new { }), "deny");

        File.AppendAllText(Path.Combine(repo, "src", "fixture.cs"), "// addressed\n");
        WritePacket(repo, packet, initialStatus, StatusDigest(repo), "accepted-actionable", "success", providerMutation: false, commitPush: false,
            changedFiles: ["src/fixture.cs"], validation: ["dotnet --version:PASS"]);

        var stopped = RunHook(repo, "stop", "address", string.Empty, "address-session");
        Assert.True(Continues(stopped), stopped.Output + stopped.Error);
        Assert.Contains("CODE_REVIEW_ADDRESS_OK", stopped.Output);
        var receipt = MarkerValue(stopped.Output, "receipt=");
        Assert.True(File.Exists(Path.Combine(repo, receipt.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(AdapterCallsPath(repo)));
    }

    [Fact]
    public void Stage11Stop_acceptsAnIdenticalPreexistingCompletionReceipt()
    {
        var repo = CreateRepository();
        const string session = "preexisting-receipt";
        var packet = ".engloop/out/code-review-response/address/preexisting-receipt.json";
        var status = StatusDigest(repo);
        var initialized = RunHook(repo, "initialize", "address", AddressPrompt(repo, packet), session);
        Assert.True(Continues(initialized));
        var gate = MarkerValue(initialized.Output, "gate=");
        var gateFull = Path.Combine(repo, gate.Replace('/', Path.DirectorySeparatorChar));
        WritePacket(repo, packet, status, status, "already-addressed", "success", false, false);
        var packetFull = Path.Combine(repo, packet.Replace('/', Path.DirectorySeparatorChar));
        var packetHash = FileHash(packetFull);
        var receipt = $".engloop/out/code-review-response/address-receipts/{Sha256Text(packet + "\n" + packetHash)}.json";
        var receiptFull = Path.Combine(repo, receipt.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(receiptFull)!);
        using var gateJson = JsonDocument.Parse(File.ReadAllText(gateFull));
        using var packetJson = JsonDocument.Parse(File.ReadAllText(packetFull));
        var value = gateJson.RootElement;
        File.WriteAllText(receiptFull, JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            packet,
            packetSha256 = packetHash,
            gateSha256 = FileHash(gateFull),
            gateJson = File.ReadAllText(gateFull),
            head = value.GetProperty("head").GetString(),
            provider = value.GetProperty("provider").GetString(),
            repository = value.GetProperty("repository").GetString(),
            pullRequest = value.GetProperty("pullRequest").GetString(),
            thread = value.GetProperty("thread").GetString(),
            sourceRevision = value.GetProperty("sourceRevision").GetString(),
            targetRevision = value.GetProperty("targetRevision").GetString(),
            iteration = value.GetProperty("iteration").GetString(),
            finalStatusDigest = packetJson.RootElement.GetProperty("finalStatusDigest").GetString(),
        }));

        var stopped = RunHook(repo, "stop", "address", string.Empty, session);

        Assert.True(Continues(stopped), stopped.Output + stopped.Error);
        Assert.Contains("CODE_REVIEW_ADDRESS_OK", stopped.Output);
    }

    [Fact]
    public void Stage11_recordsAnIndexedRenameExactly()
    {
        var repo = CreateRepository();
        const string session = "indexed-rename";
        var packet = ".engloop/out/code-review-response/address/indexed-rename.json";
        var initialStatus = StatusDigest(repo);
        var initialized = RunHook(repo, "initialize", "address", AddressPrompt(repo, packet), session);
        Assert.True(Continues(initialized));

        Git(repo, "mv", "src/fixture.cs", "src/renamed.cs");
        var finalStatus = StatusDigest(repo);
        WritePacket(repo, packet, initialStatus, finalStatus, "accepted-actionable", "success", false, false,
            changedFiles: ["src/fixture.cs", "src/renamed.cs"], validation: ["dotnet --version:PASS"]);

        var stopped = RunHook(repo, "stop", "address", string.Empty, session);

        Assert.True(Continues(stopped), stopped.Output + stopped.Error);
        Assert.Contains("CODE_REVIEW_ADDRESS_OK", stopped.Output);
    }

    [Fact]
    public void Stage11Stop_rejectsControlPathChangesAndStage12_requiresCleanPostCommitRefresh()
    {
        var protectedRepo = CreateRepository();
        var protectedPacket = ".engloop/out/code-review-response/address/protected.json";
        var protectedInitial = StatusDigest(protectedRepo);
        Assert.True(Continues(RunHook(protectedRepo, "initialize", "address", AddressPrompt(protectedRepo, protectedPacket), "protected")));
        File.AppendAllText(Path.Combine(protectedRepo, ".engloop", "config.json"), " ");
        WritePacket(protectedRepo, protectedPacket, protectedInitial, StatusDigest(protectedRepo), "accepted-actionable", "success", false, false,
            changedFiles: [".engloop/config.json"], validation: ["dotnet --version:PASS"]);
        var protectedStop = RunHook(protectedRepo, "stop", "address", string.Empty, "protected");
        Assert.False(Continues(protectedStop));
        Assert.Contains("control-path-changed", protectedStop.Output);

        var renamedRepo = CreateRepository();
        var renamedPacket = ".engloop/out/code-review-response/address/renamed-control.json";
        var renamedInitial = StatusDigest(renamedRepo);
        Assert.True(Continues(RunHook(renamedRepo, "initialize", "address", AddressPrompt(renamedRepo, renamedPacket), "renamed-control")));
        File.Move(Path.Combine(renamedRepo, ".engloop", "provider-adapters", "fake.ps1"), Path.Combine(renamedRepo, ".engloop", "provider-adapters", "moved.ps1"));
        WritePacket(renamedRepo, renamedPacket, renamedInitial, StatusDigest(renamedRepo), "accepted-actionable", "success", false, false,
            changedFiles: [".engloop/provider-adapters/fake.ps1", ".engloop/provider-adapters/moved.ps1"], validation: ["dotnet --version:PASS"]);
        var renamedStop = RunHook(renamedRepo, "stop", "address", string.Empty, "renamed-control");
        Assert.False(Continues(renamedStop));
        Assert.Contains("control-path-changed", renamedStop.Output);

        var dirtyRepo = CreateRepository();
        var dirtyPacket = ".engloop/out/code-review-response/address/dirty-fix.json";
        var initial = StatusDigest(dirtyRepo);
        Assert.True(Continues(RunHook(dirtyRepo, "initialize", "address", AddressPrompt(dirtyRepo, dirtyPacket), "dirty-fix")));
        File.AppendAllText(Path.Combine(dirtyRepo, "src", "fixture.cs"), "// fixed\n");
        WritePacket(dirtyRepo, dirtyPacket, initial, StatusDigest(dirtyRepo), "accepted-actionable", "success", false, false,
            changedFiles: ["src/fixture.cs"], validation: ["dotnet --version:PASS"], allowedOperations: ["reply-and-resolve"]);
        Assert.True(Continues(RunHook(dirtyRepo, "stop", "address", string.Empty, "dirty-fix")));

        var stage12 = RunHook(dirtyRepo, "initialize", "reply-resolve", ReplyPrompt(dirtyPacket, "reply-and-resolve"), "dirty-stage12");
        Assert.False(Continues(stage12));
        Assert.Contains("stage12-checkout-not-clean", stage12.Output);

        Git(dirtyRepo, "add", "src/fixture.cs");
        Git(dirtyRepo, "commit", "-m", "address review");
        var stalePacket = RunHook(dirtyRepo, "initialize", "reply-resolve", ReplyPrompt(dirtyPacket, "reply-and-resolve"), "stale-post-commit");
        Assert.False(Continues(stalePacket));
        Assert.Contains("local-head-mismatch", stalePacket.Output);

        var refreshed = CompleteStage11(dirtyRepo, "refreshed", "success", ["reply-and-resolve"]);
        var accepted = RunHook(dirtyRepo, "initialize", "reply-resolve", ReplyPrompt(refreshed, "reply-and-resolve"), "refreshed-stage12");
        Assert.True(Continues(accepted), accepted.Output + accepted.Error);
    }

    [Theory]
    [InlineData(true, false, "provider-mutation-forbidden")]
    [InlineData(false, true, "commit-push-forbidden")]
    public void Stage11_rejectsPacketsClaimingForbiddenAuthority(bool providerMutation, bool commitPush, string expected)
    {
        var repo = CreateRepository();
        var packet = ".engloop/out/code-review-response/address/forbidden.json";
        var status = StatusDigest(repo);
        Assert.True(Continues(RunHook(repo, "initialize", "address", AddressPrompt(repo, packet), "forbidden-session")));
        WritePacket(repo, packet, status, status, "already-addressed", "success", providerMutation, commitPush);

        var stopped = RunHook(repo, "stop", "address", string.Empty, "forbidden-session");

        Assert.False(Continues(stopped));
        Assert.Contains(expected, stopped.Output);
        Assert.False(File.Exists(AdapterCallsPath(repo)));
    }

    [Fact]
    public void Stage11_rejectsEveryMalformedPacketIdentityEvidenceAndOperationField()
    {
        var repo = CreateRepository();
        const string session = "packet-matrix";
        var packet = ".engloop/out/code-review-response/address/packet-matrix.json";
        var status = StatusDigest(repo);
        Assert.True(Continues(RunHook(repo, "initialize", "address", AddressPrompt(repo, packet), session)));
        WritePacket(repo, packet, status, status, "already-addressed", "success", false, false);
        var packetFull = Path.Combine(repo, packet.Replace('/', Path.DirectorySeparatorChar));
        var original = JsonNode.Parse(File.ReadAllText(packetFull))!.AsObject();

        void Reject(Action<JsonObject> mutate, string expected)
        {
            var candidate = original.DeepClone().AsObject();
            mutate(candidate);
            File.WriteAllText(packetFull, candidate.ToJsonString());
            var stopped = RunHook(repo, "stop", "address", string.Empty, session);
            Assert.False(Continues(stopped));
            Assert.Contains(expected, stopped.Output, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(AdapterCallsPath(repo)));
        }

        Reject(value => value["schemaVersion"] = "2.0", "packet-schema-invalid");
        Reject(value => value["unexpected"] = true, "packet-invalid");
        Reject(value => value["artifactType"] = "other", "packet-type-invalid");
        Reject(value => value["provider"] = "other", "provider-identity-mismatch");
        Reject(value => value["repository"] = "other", "provider-identity-mismatch");
        Reject(value => value["pullRequest"] = "other", "provider-identity-mismatch");
        Reject(value => value["thread"] = "other", "provider-identity-mismatch");
        Reject(value => value["sourceRevision"] = new string('c', 40), "revision-identity-mismatch");
        Reject(value => value["targetRevision"] = new string('c', 40), "revision-identity-mismatch");
        Reject(value => value["iteration"] = "other", "revision-identity-mismatch");
        Reject(value => value["initialHead"] = new string('c', 40), "local-head-mismatch");
        Reject(value => value["finalHead"] = new string('c', 40), "local-head-mismatch");
        Reject(value => value["initialStatusDigest"] = new string('0', 64), "initial-status-mismatch");
        Reject(value => value["finalStatusDigest"] = "bad", "final-status-invalid");
        Reject(value => value["providerMutationPerformed"] = true, "provider-mutation-forbidden");
        Reject(value => value["commitPushPerformed"] = true, "commit-push-forbidden");
        Reject(value => value.Remove("providerMutationPerformed"), "provider-mutation-forbidden");
        Reject(value => value.Remove("commitPushPerformed"), "commit-push-forbidden");
        Reject(value => value["threadStatus"] = "unknown", "thread-status-invalid");
        Reject(value => value["classification"] = "unknown", "classification-invalid");
        Reject(value => value["allowedOperations"] = new JsonArray(), "operations-empty");
        Reject(value => value["allowedOperations"] = new JsonArray("reply", "reply"), "operations-duplicate");
        Reject(value => value["allowedOperations"] = new JsonArray("merge"), "operations-invalid");
        Reject(value => value["replyText"] = "", "reply-empty");
        Reject(value => value["actingPrincipal"] = "", "principal-missing");
        Reject(value => value["actingPrincipal"] = new string('x', 513), "principal-missing");
        Reject(value => value["changedFiles"] = new JsonArray("src/a.cs", "src/a.cs"), "changed-files-invalid");
        Reject(value => value["changedFiles"] = new JsonArray(""), "changed-files-invalid");
        Reject(value => value["validationResults"] = new JsonArray(""), "validation-invalid");
        Reject(value => value["evidence"] = new JsonArray(), "evidence-missing");
        Reject(value => value["evidence"] = new JsonArray(""), "evidence-missing");
        Reject(value => value["unresolvedRisks"] = new JsonArray(""), "risks-invalid");
        Reject(value => value["providerHead"] = "bad", "provider-head-invalid");
        Reject(value => value["requiredFixRevision"] = "bad", "fix-revision-invalid");
        Reject(value => value["adapterRequest"] = "bad", "adapter-request-invalid");
        Reject(value => { value["classification"] = "accepted-actionable"; value["changedFiles"] = new JsonArray(); }, "changed-files-missing");
        Reject(value => { value["classification"] = "accepted-actionable"; value["changedFiles"] = new JsonArray("src/fixture.cs"); value["validationResults"] = new JsonArray(); }, "validation-missing");

        File.WriteAllText(packetFull, "null");
        var invalid = RunHook(repo, "stop", "address", string.Empty, session);
        Assert.False(Continues(invalid));
        Assert.Contains("packet-invalid", invalid.Output);
    }

    [Fact]
    public void ReviewResponseEntry_rejectsMalformedDispatchDirtyCheckoutExistingPacketAndWrongSource()
    {
        var repo = CreateRepository();
        Assert.False(Continues(RunHookRaw(repo, [], "{}")));
        Assert.False(Continues(RunHookRaw(repo, ["initialize"], "{}")));
        Assert.False(Continues(RunHookRaw(repo, ["initialize", "unknown"], HookJson(repo, "s", "x"))));
        Assert.False(Continues(RunHookRaw(repo, ["unknown", "address"], HookJson(repo, "s", "x"))));
        Assert.False(Continues(RunHookRaw(repo, ["initialize", "address"], "not-json")));
        Assert.False(Continues(RunHookRaw(repo, ["initialize", "address"], JsonSerializer.Serialize(new { session_id = "s", prompt = "x" }))));
        Assert.False(Continues(RunHookRaw(repo, ["initialize", "address"], JsonSerializer.Serialize(new { cwd = repo, prompt = "x" }))));

        var child = Path.Combine(repo, "child");
        Directory.CreateDirectory(child);
        Assert.False(Continues(RunHookRaw(repo, ["initialize", "address"], HookJson(child, "s", "x"))));

        File.AppendAllText(Path.Combine(repo, "README.md"), "dirty\n");
        var dirty = RunHook(repo, "initialize", "address", AddressPrompt(repo, ".engloop/out/code-review-response/address/dirty.json"), "dirty");
        Assert.False(Continues(dirty));
        Assert.Contains("checkout-not-clean", dirty.Output);
        Git(repo, "restore", "README.md");

        var existingPath = Path.Combine(repo, ".engloop", "out", "code-review-response", "address", "existing.json");
        Directory.CreateDirectory(Path.GetDirectoryName(existingPath)!);
        File.WriteAllText(existingPath, "{}");
        var existing = RunHook(repo, "initialize", "address", AddressPrompt(repo, ".engloop/out/code-review-response/address/existing.json"), "existing");
        Assert.False(Continues(existing));
        Assert.Contains("packet-already-exists", existing.Output);

        var wrongSource = AddressPrompt(repo, ".engloop/out/code-review-response/address/wrong-source.json")
            .Replace(GitOutput(repo, "rev-parse", "HEAD"), new string('c', 40), StringComparison.Ordinal);
        var wrong = RunHook(repo, "initialize", "address", wrongSource, "wrong-source");
        Assert.False(Continues(wrong));
        Assert.Contains("source-not-local-head", wrong.Output);

        var wrongPacketPath = RunHook(repo, "initialize", "address", AddressPrompt(repo, ".engloop/out/code-review-response/gates/not-a-packet.json"), "wrong-packet-path");
        Assert.False(Continues(wrongPacketPath));
        Assert.Contains("path-invalid", wrongPacketPath.Output);
        var traversingPacket = RunHook(repo, "initialize", "address", AddressPrompt(repo, ".engloop/out/code-review-response/address/../gates/not-a-packet.json"), "traversing-packet");
        Assert.False(Continues(traversingPacket));
        Assert.Contains("path-invalid", traversingPacket.Output);
        var nestedPacket = RunHook(repo, "initialize", "address", AddressPrompt(repo, ".engloop/out/code-review-response/address/nested/not-a-packet.json"), "nested-packet");
        Assert.False(Continues(nestedPacket));
        Assert.Contains("packet-path-invalid", nestedPacket.Output);
        var textPacket = RunHook(repo, "initialize", "address", AddressPrompt(repo, ".engloop/out/code-review-response/address/not-a-packet.txt"), "text-packet");
        Assert.False(Continues(textPacket));
        Assert.Contains("packet-path-invalid", textPacket.Output);
        var badProvider = RunHook(repo, "initialize", "address", AddressPrompt(repo, ".engloop/out/code-review-response/address/bad-provider.json").Replace("--provider fixture-provider", "--provider \"bad provider\""), "bad-provider");
        Assert.False(Continues(badProvider));
        Assert.Contains("provider-invalid", badProvider.Output);
        var longRepository = RunHook(repo, "initialize", "address", AddressPrompt(repo, ".engloop/out/code-review-response/address/long-repository.json").Replace("--repository fixture-repo", "--repository \"" + new string('r', 513) + "\""), "long-repository");
        Assert.False(Continues(longRepository));
        Assert.Contains("repository-invalid", longRepository.Output);
        var badTarget = RunHook(repo, "initialize", "address", AddressPrompt(repo, ".engloop/out/code-review-response/address/bad-target.json").Replace(new string('b', 40), "bad", StringComparison.Ordinal), "bad-target");
        Assert.False(Continues(badTarget));
        Assert.Contains("target-revision-invalid", badTarget.Output);

        var hiddenManifestRepo = CreateRepository();
        File.AppendAllText(Path.Combine(hiddenManifestRepo, ".config", "dotnet-tools.json"), " ");
        Git(hiddenManifestRepo, "update-index", "--assume-unchanged", ".config/dotnet-tools.json");
        var hiddenManifest = RunHook(hiddenManifestRepo, "initialize", "address", AddressPrompt(hiddenManifestRepo, ".engloop/out/code-review-response/address/hidden-manifest.json"), "hidden-manifest");
        Assert.False(Continues(hiddenManifest));
        Assert.Contains("tool-manifest-must-match-head", hiddenManifest.Output);

        var originalDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repo;
            Assert.Equal(1, CodeReviewResponseCommands.Execute([]));
            Assert.Equal(1, CodeReviewResponseCommands.Execute(["unknown"]));
            Assert.Equal(1, CodeReviewResponseCommands.Execute(["apply"]));
            Assert.Equal(1, CodeReviewResponseCommands.Execute(["apply", "--gate"]));
            Assert.Equal(1, CodeReviewResponseCommands.Execute(["apply", "--gate", "--approval", "--approval", "x"]));
        }
        finally { Environment.CurrentDirectory = originalDirectory; }
    }

    [Fact]
    public void ReviewResponseGuard_failsClosedWhenGitHeadBecomesMalformed()
    {
        var repo = CreateRepository();
        const string session = "malformed-head";
        var packet = ".engloop/out/code-review-response/address/malformed-head.json";
        Assert.True(Continues(RunHook(repo, "initialize", "address", AddressPrompt(repo, packet), session)));
        var headReference = GitOutput(repo, "symbolic-ref", "HEAD");
        File.WriteAllText(Path.Combine(repo, ".git", headReference.Replace('/', Path.DirectorySeparatorChar)), "not-a-revision\n");

        var guarded = RunGuard(repo, "address", session, "read_file", new { filePath = "README.md" });

        Assert.False(Continues(guarded));
        Assert.Contains("git-head-unavailable", guarded.Output);
    }

    [Fact]
    public void ReviewResponseInitialize_cleansTemporaryGateWhenDestinationCannotBeCreated()
    {
        var repo = CreateRepository();
        const string session = "gate-directory-collision";
        var gateDirectory = Path.Combine(repo, ".engloop", "out", "code-review-response", "gates");
        var gateName = Sha256Text(session) + ".address.json";
        Directory.CreateDirectory(Path.Combine(gateDirectory, gateName));

        var initialized = RunHook(repo, "initialize", "address",
            AddressPrompt(repo, ".engloop/out/code-review-response/address/gate-directory-collision.json"), session);

        Assert.False(Continues(initialized));
        Assert.Empty(Directory.GetFiles(gateDirectory, gateName + ".*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void ReviewResponseGate_rejectsIdentityRevisionToolAndAuthorityTampering()
    {
        var repo = CreateRepository();
        var packet = CompleteStage11(repo, "gate-matrix", "success", ["reply"]);
        const string session = "gate-matrix";
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), session);
        Assert.True(Continues(initialized));
        var gate = MarkerValue(initialized.Output, "gate=");
        var gateFull = Path.Combine(repo, gate.Replace('/', Path.DirectorySeparatorChar));
        var original = JsonNode.Parse(File.ReadAllText(gateFull))!.AsObject();

        void Reject(Action<JsonObject> mutate, string expected)
        {
            var candidate = original.DeepClone().AsObject();
            mutate(candidate);
            File.WriteAllText(gateFull, candidate.ToJsonString());
            var result = RunGuard(repo, "reply-resolve", session, "read_file", new { filePath = packet });
            Assert.False(Continues(result));
            Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
        }

        Reject(value => value["schemaVersion"] = "2.0", "gate-schema-invalid");
        Reject(value => value["unexpected"] = true, "gate-missing-or-invalid");
        Reject(value => value["mode"] = "unknown", "gate-mode-invalid");
        Reject(value => value["sessionHash"] = "bad", "gate-session-invalid");
        Reject(value => value["packet"] = ".engloop/out/code-review-response/gates/forged.json", "path-invalid");
        Reject(value => value["head"] = "bad", "gate-head-invalid");
        Reject(value => value["provider"] = "bad provider", "gate-provider-invalid");
        Reject(value => value["repository"] = "", "gate-repository-invalid");
        Reject(value => value["pullRequest"] = "bad pr", "gate-pr-invalid");
        Reject(value => value["thread"] = "", "gate-thread-invalid");
        Reject(value => value["sourceRevision"] = "bad", "gate-source-invalid");
        Reject(value => value["targetRevision"] = "bad", "gate-target-invalid");
        Reject(value => value["iteration"] = "bad iteration", "gate-iteration-invalid");
        Reject(value => value["initialStatusDigest"] = "bad", "gate-status-invalid");
        Reject(value => value["addressReceipt"] = null, "gate-address-receipt-missing");
        Reject(value => value["addressReceiptSha256"] = "bad", "gate-address-receipt-hash-invalid");
        Reject(value => value["addressReceiptSha256"] = null, "gate-address-receipt-hash-invalid");
        Reject(value => value["adapter"] = null, "gate-adapter-missing");
        Reject(value => value["adapterSha256"] = "bad", "gate-adapter-hash-invalid");
        Reject(value => value["adapterSha256"] = null, "gate-adapter-hash-invalid");
        Reject(value => value["adapterArtifactSha256"] = "bad", "gate-adapter-artifact-hash-invalid");
        Reject(value => value["adapterArtifactSha256"] = null, "gate-adapter-artifact-hash-invalid");
        Reject(value => value["inspection"] = null, "gate-inspection-missing");
        Reject(value => value["inspectionSha256"] = "bad", "gate-inspection-hash-invalid");
        Reject(value => value["inspectionSha256"] = null, "gate-inspection-hash-invalid");
        Reject(value => value["operation"] = null, "gate-operation-invalid");
        Reject(value => value["operation"] = "merge", "gate-operation-invalid");
        Reject(value => value["packetSha256"] = "bad", "gate-packet-hash-invalid");
        Reject(value => value["manifestSha256"] = new string('0', 64), "tool-manifest-changed");
        Reject(value => value["toolVersion"] = "0.0.0", "tool-version-changed");
        Reject(value =>
        {
            value["mode"] = "address";
            value["packetSha256"] = "";
            value["addressReceipt"] = null;
            value["addressReceiptSha256"] = null;
            value["adapter"] = null;
            value["adapterSha256"] = null;
            value["adapterArtifactSha256"] = null;
            value["inspection"] = null;
            value["inspectionSha256"] = null;
            value["operation"] = null;
        }, "gate-mode-mismatch");

        var addressRepo = CreateRepository();
        const string addressSession = "address-gate-matrix";
        var addressPacket = ".engloop/out/code-review-response/address/address-gate.json";
        var addressInitialized = RunHook(addressRepo, "initialize", "address", AddressPrompt(addressRepo, addressPacket), addressSession);
        Assert.True(Continues(addressInitialized));
        var addressGate = MarkerValue(addressInitialized.Output, "gate=");
        var addressGateFull = Path.Combine(addressRepo, addressGate.Replace('/', Path.DirectorySeparatorChar));
        var addressOriginal = JsonNode.Parse(File.ReadAllText(addressGateFull))!.AsObject();
        void RejectAddress(Action<JsonObject> mutate, string expected)
        {
            var candidate = addressOriginal.DeepClone().AsObject();
            mutate(candidate);
            File.WriteAllText(addressGateFull, candidate.ToJsonString());
            var result = RunGuard(addressRepo, "address", addressSession, "read_file", new { filePath = "README.md" });
            Assert.False(Continues(result));
            Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
        }
        RejectAddress(value => value["packetSha256"] = new string('0', 64), "address-gate-packet-hash-invalid");
        RejectAddress(value => value["addressReceipt"] = ".engloop/out/code-review-response/address-receipts/forged.json", "address-gate-authority-invalid");
        RejectAddress(value => value["addressReceiptSha256"] = new string('0', 64), "address-gate-authority-invalid");
        RejectAddress(value => value["adapter"] = ".engloop/provider-adapters/fake.json", "address-gate-authority-invalid");
        RejectAddress(value => value["adapterSha256"] = new string('0', 64), "address-gate-authority-invalid");
        RejectAddress(value => value["adapterArtifactSha256"] = new string('0', 64), "address-gate-authority-invalid");
        RejectAddress(value => value["inspection"] = ".engloop/out/code-review-response/inspections/forged.json", "address-gate-authority-invalid");
        RejectAddress(value => value["inspectionSha256"] = new string('0', 64), "address-gate-authority-invalid");
        RejectAddress(value => value["operation"] = "reply", "address-gate-authority-invalid");
    }

    [Fact]
    public void Stage12_requiresSeparateExactApprovalAndAppliesReplyExactlyOnce()
    {
        var repo = CreateRepository();
        const string exactReply = "Addressed `exactly` — café ✅";
        var packet = CompleteStage11(repo, "reply", "success", ["reply"], replyText: exactReply);
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), "reply-session");
        Assert.True(Continues(initialized), initialized.Output + initialized.Error);
        var gate = MarkerValue(initialized.Output, "gate=");
        var approval = ApprovalPath("reply-session");
        var applyCommand = ApplyCommand(gate, approval);

        AssertDecision(RunGuard(repo, "reply-resolve", "reply-session", "apply_patch", new { input = "forbidden" }), "deny");
        AssertDecision(RunGuard(repo, "reply-resolve", "reply-session", "fetch_webpage", new { url = "https://example.invalid" }), "deny");
        AssertDecision(RunGuard(repo, "reply-resolve", "reply-session", "run_in_terminal", new { command = applyCommand }), "deny");
        AssertDecision(RunGuard(repo, "reply-resolve", "reply-session", "vscode_askQuestions", new { questions = Array.Empty<object>() }), "deny");
        AssertDecision(RunGuard(repo, "reply-resolve", "reply-session", "vscode_askQuestions", ApprovalQuestion(repo, gate, packet, "reply", "thread-1")), "allow");

        var approved = Approve(repo, "reply-session", gate, packet, "reply", "thread-1", "Confirm");
        Assert.True(Continues(approved), approved.Output + approved.Error);
        Assert.Contains("CODE_REVIEW_RESPONSE_APPROVED", approved.Output);
        AssertDecision(RunGuard(repo, "reply-resolve", "reply-session", "run_in_terminal", new { command = applyCommand }), "allow");
        AssertDecision(RunGuard(repo, "reply-resolve", "reply-session", "execute", new { command = applyCommand }), "allow");
        AssertDecision(RunGuard(repo, "reply-resolve", "reply-session", "run_in_terminal", new { command = applyCommand + "; git push" }), "deny");
        AssertDecision(RunGuard(repo, "reply-resolve", "reply-session", "vscode_askQuestions", ApprovalQuestion(repo, gate, packet, "reply", "thread-1")), "deny");

        var applied = RunApply(repo, gate, approval);
        Assert.Equal(0, applied.ExitCode);
        Assert.Contains("CODE_REVIEW_RESPONSE_APPLIED", applied.Output);
        Assert.False(File.Exists(Path.Combine(repo, approval.Replace('/', Path.DirectorySeparatorChar))));
        AssertDecision(RunGuard(repo, "reply-resolve", "reply-session", "read_file", new { filePath = packet }), "allow");

        var replay = RunApply(repo, gate, approval);
        Assert.Equal(0, replay.ExitCode);
        Assert.Contains("CODE_REVIEW_RESPONSE_ALREADY_APPLIED", replay.Output);
        File.WriteAllText(Path.Combine(repo, approval.Replace('/', Path.DirectorySeparatorChar)), "stale approval");
        var cleanupReplay = RunApply(repo, gate, approval);
        Assert.Equal(0, cleanupReplay.ExitCode);
        Assert.Contains("CODE_REVIEW_RESPONSE_ALREADY_APPLIED", cleanupReplay.Output);
        Assert.False(File.Exists(Path.Combine(repo, approval.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Single(ReadInspectionCalls(repo));
        Assert.Single(ReadMutationCalls(repo));
        Assert.Equal("apply", ReadMutationCalls(repo)[0].GetProperty("phase").GetString());
        Assert.Equal(exactReply, ReadMutationCalls(repo)[0].GetProperty("replyText").GetString());

        var stopped = RunHook(repo, "stop", "reply-resolve", string.Empty, "reply-session");
        Assert.True(Continues(stopped), stopped.Output + stopped.Error);
        Assert.Contains("CODE_REVIEW_REPLY_RESOLVE_OK", stopped.Output);
    }

    [Fact]
    public void Stage12_resolutionRequiresFixOnProviderHeadAndCancelMutatesNothing()
    {
        var staleRepo = CreateRepository();
        var stalePacket = CompleteStage11(staleRepo, "stale", "success", ["resolve"], providerHead: new string('d', 40));
        var stale = RunHook(staleRepo, "initialize", "reply-resolve", ReplyPrompt(stalePacket, "resolve"), "stale-session");
        Assert.False(Continues(stale));
        Assert.Contains("provider-head-not-local-head", stale.Output);
        Assert.False(File.Exists(AdapterCallsPath(staleRepo)));

        var resolvedThreadRepo = CreateRepository();
        var resolvedThreadPacket = CompleteStage11(resolvedThreadRepo, "resolved-thread", "success", ["resolve"], threadStatus: "resolved");
        var resolvedThread = RunHook(resolvedThreadRepo, "initialize", "reply-resolve", ReplyPrompt(resolvedThreadPacket, "resolve"), "resolved-thread");
        Assert.False(Continues(resolvedThread));
        Assert.Contains("thread-not-active", resolvedThread.Output);

        var cancelRepo = CreateRepository();
        var cancelPacket = CompleteStage11(cancelRepo, "cancel", "success", ["reply"]);
        var initialized = RunHook(cancelRepo, "initialize", "reply-resolve", ReplyPrompt(cancelPacket, "reply"), "cancel-session");
        Assert.True(Continues(initialized));
        var gate = MarkerValue(initialized.Output, "gate=");
        var cancelled = Approve(cancelRepo, "cancel-session", gate, cancelPacket, "reply", "thread-1", "Cancel");
        Assert.True(Continues(cancelled));
        Assert.Contains("CODE_REVIEW_RESPONSE_CANCELLED", cancelled.Output);
        Assert.False(File.Exists(Path.Combine(cancelRepo, gate.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Single(ReadInspectionCalls(cancelRepo));
        Assert.Empty(ReadMutationCalls(cancelRepo));
        var stopped = RunHook(cancelRepo, "stop", "reply-resolve", string.Empty, "cancel-session");
        Assert.True(Continues(stopped));
        Assert.DoesNotContain("CODE_REVIEW_REPLY_RESOLVE_OK", stopped.Output);

        var resolveRepo = CreateRepository();
        var resolvePacket = CompleteStage11(resolveRepo, "resolve-only", "resolve-no-reply", ["resolve"]);
        var resolveInitialized = RunHook(resolveRepo, "initialize", "reply-resolve", ReplyPrompt(resolvePacket, "resolve"), "resolve-only");
        Assert.True(Continues(resolveInitialized));
        var resolveGate = MarkerValue(resolveInitialized.Output, "gate=");
        var resolveApproval = ApprovalPath("resolve-only");
        Assert.True(Continues(Approve(resolveRepo, "resolve-only", resolveGate, resolvePacket, "resolve", "thread-1", "Confirm")));
        var resolved = RunApply(resolveRepo, resolveGate, resolveApproval);
        Assert.Equal(0, resolved.ExitCode);
        Assert.Contains("CODE_REVIEW_RESPONSE_APPLIED", resolved.Output);
    }

    [Fact]
    public void Stage12_rejectsInvalidOperationAndNonCleanRefreshPacket()
    {
        var operationRepo = CreateRepository();
        var operationPacket = CompleteStage11(operationRepo, "invalid-operation", "success", ["reply"]);
        var operation = RunHook(operationRepo, "initialize", "reply-resolve", ReplyPrompt(operationPacket, "merge"), "invalid-operation");
        Assert.False(Continues(operation));
        Assert.Contains("operation-invalid", operation.Output);

        var repo = CreateRepository();
        var packet = ".engloop/out/code-review-response/address/non-clean-refresh.json";
        var initialStatus = StatusDigest(repo);
        Assert.True(Continues(RunHook(repo, "initialize", "address", AddressPrompt(repo, packet), "non-clean-refresh")));
        File.AppendAllText(Path.Combine(repo, "src", "fixture.cs"), "// addressed but not committed\n");
        WritePacket(repo, packet, initialStatus, StatusDigest(repo), "accepted-actionable", "success", false, false,
            changedFiles: ["src/fixture.cs"], validation: ["dotnet --version:PASS"]);
        Assert.True(Continues(RunHook(repo, "stop", "address", string.Empty, "non-clean-refresh")));
        Git(repo, "restore", "src/fixture.cs");

        var result = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), "non-clean-refresh-stage12");
        Assert.False(Continues(result));
        Assert.Contains("packet-not-refreshed-after-commit", result.Output);
    }

    [Fact]
    public void Stage12_requiresUntamperedTrustedStage11CompletionReceipt()
    {
        var fabricatedRepo = CreateRepository();
        var fabricatedPacket = ".engloop/out/code-review-response/address/fabricated.json";
        var status = StatusDigest(fabricatedRepo);
        WritePacket(fabricatedRepo, fabricatedPacket, status, status, "already-addressed", "success", false, false);
        var fabricated = RunHook(fabricatedRepo, "initialize", "reply-resolve", ReplyPrompt(fabricatedPacket, "reply"), "fabricated");
        Assert.False(Continues(fabricated));
        Assert.Contains("address-receipt-missing", fabricated.Output);
        Assert.False(File.Exists(AdapterCallsPath(fabricatedRepo)));

        foreach (var delete in new[] { false, true })
        {
            var repo = CreateRepository();
            var packet = CompleteStage11(repo, delete ? "missing-receipt" : "changed-receipt", "success", ["reply"]);
            var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), delete ? "missing-receipt" : "changed-receipt");
            Assert.True(Continues(initialized));
            var gate = MarkerValue(initialized.Output, "gate=");
            using var gateJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, gate.Replace('/', Path.DirectorySeparatorChar))));
            var receipt = gateJson.RootElement.GetProperty("addressReceipt").GetString()!;
            var receiptPath = Path.Combine(repo, receipt.Replace('/', Path.DirectorySeparatorChar));
            if (delete) File.Delete(receiptPath); else File.AppendAllText(receiptPath, " ");
            var result = RunGuard(repo, "reply-resolve", delete ? "missing-receipt" : "changed-receipt", "read_file", new { filePath = packet });
            Assert.False(Continues(result));
            Assert.Contains(delete ? "address-receipt-missing" : "address-receipt-changed", result.Output);
            Assert.Empty(ReadMutationCalls(repo));
        }

        var forgedRepo = CreateRepository();
        var forgedPacket = CompleteStage11(forgedRepo, "forged-receipt", "success", ["reply"]);
        const string forgedSession = "forged-receipt";
        var forgedInitialized = RunHook(forgedRepo, "initialize", "reply-resolve", ReplyPrompt(forgedPacket, "reply"), forgedSession);
        Assert.True(Continues(forgedInitialized));
        var forgedGate = MarkerValue(forgedInitialized.Output, "gate=");
        var forgedGatePath = Path.Combine(forgedRepo, forgedGate.Replace('/', Path.DirectorySeparatorChar));
        var forgedGateValue = JsonNode.Parse(File.ReadAllText(forgedGatePath))!.AsObject();
        var forgedReceiptPath = Path.Combine(forgedRepo, forgedGateValue["addressReceipt"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar));
        var forgedReceiptValue = JsonNode.Parse(File.ReadAllText(forgedReceiptPath))!.AsObject();
        forgedReceiptValue["gateJson"] = "{}";
        forgedReceiptValue["gateSha256"] = Sha256Text("{}");
        File.WriteAllText(forgedReceiptPath, forgedReceiptValue.ToJsonString());
        forgedGateValue["addressReceiptSha256"] = FileHash(forgedReceiptPath);
        File.WriteAllText(forgedGatePath, forgedGateValue.ToJsonString());
        var forged = RunGuard(forgedRepo, "reply-resolve", forgedSession, "read_file", new { filePath = forgedPacket });
        Assert.False(Continues(forged));
        Assert.Contains("address-receipt-gate-invalid", forged.Output);
        Assert.Empty(ReadMutationCalls(forgedRepo));
    }

    [Fact]
    public void Stage12_rejectsEveryIndependentStage11ReceiptBinding()
    {
        var repo = CreateRepository();
        var packet = CompleteStage11(repo, "receipt-binding-matrix", "success", ["reply"]);
        const string session = "receipt-binding-matrix";
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), session);
        Assert.True(Continues(initialized));
        var gate = MarkerValue(initialized.Output, "gate=");
        var gateFull = Path.Combine(repo, gate.Replace('/', Path.DirectorySeparatorChar));
        var originalGate = JsonNode.Parse(File.ReadAllText(gateFull))!.AsObject();
        var receiptFull = Path.Combine(repo, originalGate["addressReceipt"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar));
        var originalReceipt = JsonNode.Parse(File.ReadAllText(receiptFull))!.AsObject();

        void Reject(Action<JsonObject, JsonObject>? mutate, string expected, Action<JsonObject>? afterSeal = null)
        {
            var candidateGate = originalGate.DeepClone().AsObject();
            var candidateReceipt = originalReceipt.DeepClone().AsObject();
            var acceptedGate = JsonNode.Parse(candidateReceipt["gateJson"]!.GetValue<string>())!.AsObject();
            mutate?.Invoke(candidateReceipt, acceptedGate);
            var acceptedGateJson = acceptedGate.ToJsonString();
            candidateReceipt["gateJson"] = acceptedGateJson;
            candidateReceipt["gateSha256"] = Sha256Text(acceptedGateJson);
            afterSeal?.Invoke(candidateReceipt);
            File.WriteAllText(receiptFull, candidateReceipt.ToJsonString());
            candidateGate["addressReceiptSha256"] = FileHash(receiptFull);
            File.WriteAllText(gateFull, candidateGate.ToJsonString());

            var result = RunGuard(repo, "reply-resolve", session, "read_file", new { filePath = packet });
            Assert.False(Continues(result));
            Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(ReadMutationCalls(repo));
        }

        Reject(null, "address-receipt-gate-invalid", receipt => receipt["gateSha256"] = "bad");
        Reject(null, "address-receipt-gate-invalid", receipt => receipt["gateSha256"] = new string('0', 64));
        Reject(null, "address-receipt-gate-invalid", receipt =>
        {
            receipt["gateJson"] = "null";
            receipt["gateSha256"] = Sha256Text("null");
        });

        var authorityValues = new Dictionary<string, JsonNode?>
        {
            ["addressReceipt"] = ".engloop/out/code-review-response/address-receipts/other.json",
            ["addressReceiptSha256"] = new string('0', 64),
            ["adapter"] = ".engloop/provider-adapters/fake.json",
            ["adapterSha256"] = new string('0', 64),
            ["adapterArtifactSha256"] = new string('0', 64),
            ["inspection"] = ".engloop/out/code-review-response/inspections/other.json",
            ["inspectionSha256"] = new string('0', 64),
            ["operation"] = "reply",
        };
        foreach (var (field, replacement) in authorityValues)
            Reject((_, accepted) => accepted[field] = replacement?.DeepClone(), "address-receipt-gate-invalid");

        var scopeValues = new Dictionary<string, string>
        {
            ["head"] = new string('c', 40),
            ["provider"] = "other-provider",
            ["repository"] = "other-repository",
            ["pullRequest"] = "other-pr",
            ["thread"] = "other-thread",
            ["sourceRevision"] = new string('c', 40),
            ["targetRevision"] = new string('c', 40),
            ["iteration"] = "other-iteration",
        };
        foreach (var (field, replacement) in scopeValues)
            Reject((_, accepted) => accepted[field] = replacement, "address-receipt-gate-invalid");

        Reject((receipt, accepted) => { receipt["packet"] = ".engloop/out/code-review-response/address/other.json"; accepted["packet"] = receipt["packet"]!.DeepClone(); }, "address-receipt-identity-mismatch");
        Reject((receipt, _) => receipt["packetSha256"] = new string('0', 64), "address-receipt-identity-mismatch");
        foreach (var (field, replacement) in scopeValues)
            Reject((receipt, accepted) => { receipt[field] = replacement; accepted[field] = replacement; }, "address-receipt-identity-mismatch");
        Reject((receipt, _) => receipt["finalStatusDigest"] = new string('0', 64), "address-receipt-identity-mismatch");
        Reject((_, accepted) => accepted["initialStatusDigest"] = new string('0', 64), "address-receipt-identity-mismatch");
        Reject((_, accepted) => accepted["toolVersion"] = "0.0.0", "address-receipt-identity-mismatch");
        Reject((_, accepted) => accepted["manifestSha256"] = new string('0', 64), "address-receipt-identity-mismatch");
    }

    [Fact]
    public void Stage12_outcomeUnknownReconcilesWithSameAttemptAndNeverBlindRetries()
    {
        var repo = CreateRepository();
        var packet = CompleteStage11(repo, "unknown", "unknown-once", ["reply-and-resolve"]);
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply-and-resolve"), "unknown-session");
        Assert.True(Continues(initialized));
        var gate = MarkerValue(initialized.Output, "gate=");
        var approval = ApprovalPath("unknown-session");
        Assert.True(Continues(Approve(repo, "unknown-session", gate, packet, "reply-and-resolve", "thread-1", "Confirm")));

        var first = RunApply(repo, gate, approval);
        Assert.Equal(2, first.ExitCode);
        Assert.Contains("OUTCOME_UNKNOWN", first.Error);
        var second = RunApply(repo, gate, approval);
        Assert.Equal(0, second.ExitCode);
        Assert.Contains("APPLIED", second.Output);

        var calls = ReadMutationCalls(repo);
        Assert.Equal(2, calls.Length);
        Assert.Equal("apply", calls[0].GetProperty("phase").GetString());
        Assert.Equal("reconcile", calls[1].GetProperty("phase").GetString());
        Assert.Equal(calls[0].GetProperty("marker").GetString(), calls[1].GetProperty("marker").GetString());
    }

    [Fact]
    public async Task Stage12_concurrentApplyProcessesInvokeProviderMutationAtMostOnce()
    {
        var repo = CreateRepository();
        var packet = CompleteStage11(repo, "concurrent", "slow-success", ["reply"]);
        const string session = "concurrent-session";
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), session);
        Assert.True(Continues(initialized));
        var gate = MarkerValue(initialized.Output, "gate=");
        var approval = ApprovalPath(session);
        Assert.True(Continues(Approve(repo, session, gate, packet, "reply", "thread-1", "Confirm")));

        var first = RunApplyProcessAsync(repo, gate, approval);
        var second = RunApplyProcessAsync(repo, gate, approval);
        var results = await Task.WhenAll(first, second);

        Assert.Contains(results, result => result.ExitCode == 0);
        Assert.All(results, result => Assert.Contains(result.ExitCode, new[] { 0, 1 }));
        Assert.Single(ReadMutationCalls(repo));
    }

    [Fact]
    public void Stage12_neverTrustsTamperedLocalAttemptAsAlreadyApplied()
    {
        var repo = CreateRepository();
        var packet = CompleteStage11(repo, "attempt-tamper", "success", ["reply"]);
        const string session = "attempt-tamper";
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), session);
        var gate = MarkerValue(initialized.Output, "gate=");
        var approval = ApprovalPath(session);
        Assert.True(Continues(Approve(repo, session, gate, packet, "reply", "thread-1", "Confirm")));
        Assert.Equal(0, RunApply(repo, gate, approval).ExitCode);
        var attemptPath = AttemptPathFor(repo, packet, "reply");
        var original = JsonNode.Parse(File.ReadAllText(attemptPath))!.AsObject();

        void Reject(Action<JsonObject> mutate, string expected)
        {
            var candidate = original.DeepClone().AsObject();
            mutate(candidate);
            File.WriteAllText(attemptPath, candidate.ToJsonString());
            var replay = RunApply(repo, gate, approval);
            Assert.NotEqual(0, replay.ExitCode);
            Assert.Contains(expected, replay.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Single(ReadMutationCalls(repo));
        }

        Reject(value => value["schemaVersion"] = "2.0", "attempt-schema-invalid");
        Reject(value => value["state"] = "forged", "attempt-state-invalid");
        Reject(value => value["attemptId"] = "bad", "attempt-id-invalid");
        Reject(value => value["gateSha256"] = new string('0', 64), "attempt-gate-hash-invalid");
        Reject(value => value["packetSha256"] = new string('0', 64), "attempt-identity-mismatch");
        Reject(value => value["adapterSha256"] = new string('0', 64), "attempt-identity-mismatch");
        Reject(value => value["operation"] = "resolve", "attempt-identity-mismatch");
        Reject(value => value["marker"] = "forged", "attempt-marker-invalid");
        Reject(value => value["approvalSha256"] = "bad", "attempt-approval-invalid");
        Reject(value => value["approvalJson"] = "{}", "attempt-approval-invalid");
        Reject(value => value["approvalToolUseId"] = "", "attempt-approval-invalid");
        Reject(value => value["approvalQuestionSha256"] = "bad", "attempt-approval-invalid");
        Reject(value => value["approvalResponseSha256"] = "bad", "attempt-approval-invalid");
        Reject(value => value["approvalQuestionJson"] = "{}", "attempt-approval-invalid");
        Reject(value => value["approvalResponseJson"] = "{}", "attempt-approval-invalid");
        Reject(value => value["providerReceiptId"] = "", "success-receipt-missing");

        File.WriteAllText(attemptPath, original.ToJsonString());
        var replay = RunApply(repo, gate, approval);
        Assert.Equal(0, replay.ExitCode);
        Assert.Contains("ALREADY_APPLIED", replay.Output);
    }

    [Fact]
    public void Stage12_rejectsEveryIndependentDurableApprovalBinding()
    {
        var repo = CreateRepository();
        var packet = CompleteStage11(repo, "durable-approval-binding", "success", ["reply"]);
        const string session = "durable-approval-binding";
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), session);
        var gate = MarkerValue(initialized.Output, "gate=");
        var approval = ApprovalPath(session);
        Assert.True(Continues(Approve(repo, session, gate, packet, "reply", "thread-1", "Confirm")));
        Assert.Equal(0, RunApply(repo, gate, approval).ExitCode);
        var attemptFull = AttemptPathFor(repo, packet, "reply");
        var original = JsonNode.Parse(File.ReadAllText(attemptFull))!.AsObject();

        void Reject(Action<JsonObject> mutate)
        {
            var candidate = original.DeepClone().AsObject();
            var embeddedApproval = JsonNode.Parse(candidate["approvalJson"]!.GetValue<string>())!.AsObject();
            mutate(embeddedApproval);
            var embeddedJson = embeddedApproval.ToJsonString();
            candidate["approvalJson"] = embeddedJson;
            candidate["approvalSha256"] = Sha256Text(embeddedJson);
            File.WriteAllText(attemptFull, candidate.ToJsonString());
            var replay = RunApply(repo, gate, approval);
            Assert.NotEqual(0, replay.ExitCode);
            Assert.Contains("attempt-approval-invalid", replay.Error);
            Assert.Single(ReadMutationCalls(repo));
        }

        Reject(value => value["schemaVersion"] = "2.0");
        Reject(value => value["sessionHash"] = new string('0', 64));
        Reject(value => value["gateSha256"] = new string('0', 64));
        Reject(value => value["packetSha256"] = new string('0', 64));
        Reject(value => value["operation"] = "resolve");
        Reject(value => value["toolUseId"] = "other-tool-use");
        Reject(value => value["questionSha256"] = new string('0', 64));
        Reject(value => value["responseSha256"] = new string('0', 64));
        Reject(value => value["questionJson"] = "{}");
        Reject(value => value["responseJson"] = "{}");

        var nullApproval = original.DeepClone().AsObject();
        nullApproval["approvalJson"] = "null";
        nullApproval["approvalSha256"] = Sha256Text("null");
        File.WriteAllText(attemptFull, nullApproval.ToJsonString());
        var nullReplay = RunApply(repo, gate, approval);
        Assert.NotEqual(0, nullReplay.ExitCode);
        Assert.Contains("attempt-approval-invalid", nullReplay.Error);
    }

    [Fact]
    public void Stage12_timesOutProviderMutationAsOutcomeUnknown()
    {
        var repo = CreateRepository();
        var manifestFull = Path.Combine(repo, ".engloop", "provider-adapters", "fake.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestFull))!.AsObject();
        manifest["timeoutSeconds"] = 10;
        File.WriteAllText(manifestFull, manifest.ToJsonString());
        Git(repo, "add", ".engloop/provider-adapters/fake.json");
        Git(repo, "commit", "-m", "short provider timeout");
        var packet = CompleteStage11(repo, "timeout-on-apply", "timeout-on-apply", ["reply"]);
        const string session = "timeout-on-apply";
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), session);
        Assert.True(Continues(initialized));
        var gate = MarkerValue(initialized.Output, "gate=");
        var approval = ApprovalPath(session);
        Assert.True(Continues(Approve(repo, session, gate, packet, "reply", "thread-1", "Confirm")));

        var result = RunApply(repo, gate, approval);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("OUTCOME_UNKNOWN", result.Error);
        var attempt = JsonNode.Parse(File.ReadAllText(AttemptPathFor(repo, packet, "reply")))!.AsObject();
        Assert.Equal("outcome-unknown", attempt["state"]!.GetValue<string>());
        Assert.Contains("adapter-exit=-1", attempt["diagnostic"]!.GetValue<string>());
    }

    [Fact]
    public void Stage12_failsClosedOnMissingUntrackedOrChangedAdapterAndStalePacket()
    {
        var missingRepo = CreateRepository();
        var missingPacket = CompleteStage11(missingRepo, "missing", "success", ["reply"]);
        var missing = RunHook(missingRepo, "initialize", "reply-resolve", $"--packet {missingPacket} --operation reply --adapter .engloop/provider-adapters/missing.json", "missing-adapter");
        Assert.False(Continues(missing));
        Assert.Contains("adapter-must-be-tracked", missing.Output);

        var changedRepo = CreateRepository();
        var changedPacket = CompleteStage11(changedRepo, "changed", "success", ["reply"]);
        var initialized = RunHook(changedRepo, "initialize", "reply-resolve", ReplyPrompt(changedPacket, "reply"), "changed-session");
        Assert.True(Continues(initialized));
        File.AppendAllText(Path.Combine(changedRepo, ".engloop", "provider-adapters", "fake.ps1"), "# changed\n");
        Git(changedRepo, "update-index", "--assume-unchanged", ".engloop/provider-adapters/fake.ps1");
        var guard = RunGuard(changedRepo, "reply-resolve", "changed-session", "read_file", new { filePath = changedPacket });
        Assert.False(Continues(guard));
        Assert.Contains("adapter-artifact-changed", guard.Output);

        var packetRepo = CreateRepository();
        var packet = CompleteStage11(packetRepo, "packet", "success", ["reply"]);
        var packetInitialized = RunHook(packetRepo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), "packet-session");
        Assert.True(Continues(packetInitialized));
        File.AppendAllText(Path.Combine(packetRepo, packet.Replace('/', Path.DirectorySeparatorChar)), " ");
        var staleGuard = RunGuard(packetRepo, "reply-resolve", "packet-session", "read_file", new { filePath = packet });
        Assert.False(Continues(staleGuard));
        Assert.Contains("packet-changed", staleGuard.Output);
    }

    [Fact]
    public void Stage12_rejectsMissingMismatchedMalformedOrTamperedOneTimeApprovalBeforeAdapterInvocation()
    {
        var repo = CreateRepository();
        var packet = CompleteStage11(repo, "approval-matrix", "success", ["reply"]);
        const string session = "approval-matrix-session";
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), session);
        Assert.True(Continues(initialized), initialized.Output + initialized.Error);
        var gate = MarkerValue(initialized.Output, "gate=");
        var approval = ApprovalPath(session);
        Assert.True(Continues(Approve(repo, session, gate, packet, "reply", "thread-1", "Confirm")));
        var approvalFull = Path.Combine(repo, approval.Replace('/', Path.DirectorySeparatorChar));
        var original = JsonNode.Parse(File.ReadAllText(approvalFull))!.AsObject();

        void Reject(Action<JsonObject> mutate, string expected)
        {
            var candidate = original.DeepClone().AsObject();
            mutate(candidate);
            File.WriteAllText(approvalFull, candidate.ToJsonString());
            var result = RunApply(repo, gate, approval);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(expected, result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(ReadMutationCalls(repo));
        }

        Reject(value => value["schemaVersion"] = "2.0", "approval-identity-invalid");
        Reject(value => value["unexpected"] = true, "approval-invalid");
        Reject(value => value["sessionHash"] = new string('0', 64), "approval-identity-invalid");
        Reject(value => value["gateSha256"] = new string('0', 64), "approval-stale");
        Reject(value => value["packetSha256"] = new string('0', 64), "approval-stale");
        Reject(value => value["operation"] = "resolve", "approval-stale");
        Reject(value => value["replySha256"] = "bad", "approval-invalid");
        Reject(value => value["toolUseId"] = "", "approval-invalid");
        Reject(value => value["questionSha256"] = "bad", "approval-invalid");
        Reject(value => value["responseSha256"] = "bad", "approval-invalid");
        Reject(value => value["questionSha256"] = new string('0', 64), "approval-question-changed");
        Reject(value => value["responseSha256"] = new string('0', 64), "approval-response-changed");
        Reject(value => value["questionJson"] = "{}", "approval-question-changed");
        Reject(value => value["responseJson"] = "{}", "approval-response-changed");

        File.Delete(approvalFull);
        var missing = RunApply(repo, gate, approval);
        Assert.NotEqual(0, missing.ExitCode);
        Assert.Contains("approval-invalid", missing.Error);
        Assert.Empty(ReadMutationCalls(repo));

        File.WriteAllText(approvalFull, "null");
        var malformed = RunApply(repo, gate, approval);
        Assert.NotEqual(0, malformed.ExitCode);
        Assert.Contains("approval-invalid", malformed.Error);

        File.WriteAllText(approvalFull, original.ToJsonString());
        var otherApproval = ".engloop/out/code-review-response/approvals/other.json";
        File.WriteAllText(Path.Combine(repo, otherApproval.Replace('/', Path.DirectorySeparatorChar)), original.ToJsonString());
        var wrongPath = RunApply(repo, gate, otherApproval);
        Assert.NotEqual(0, wrongPath.ExitCode);
        Assert.Contains("approval-path-mismatch", wrongPath.Error);

        var addressRepo = CreateRepository();
        var addressPacket = ".engloop/out/code-review-response/address/wrong-mode.json";
        var addressInitialized = RunHook(addressRepo, "initialize", "address", AddressPrompt(addressRepo, addressPacket), "wrong-mode");
        Assert.True(Continues(addressInitialized));
        var addressGate = MarkerValue(addressInitialized.Output, "gate=");
        var wrongMode = RunApply(addressRepo, addressGate, ApprovalPath("wrong-mode"));
        Assert.NotEqual(0, wrongMode.ExitCode);
        Assert.Contains("gate-mode-invalid", wrongMode.Error);
    }

    [Fact]
    public void Stage12_recordsRejectedUnknownAndEveryInvalidProviderReadbackWithoutSuccess()
    {
        var cases = new (string Behavior, string Expected)[]
        {
            ("exit-failure", "adapter-exit"), ("invalid-json", "adapter-exit"),
            ("reported-unknown", "outcome-unknown"), ("bad-schema", "schema-invalid"),
            ("extra-field", "adapter-exit"),
            ("bad-status", "status-invalid"), ("wrong-provider", "identity-mismatch"),
            ("wrong-repository", "identity-mismatch"), ("wrong-pr", "identity-mismatch"),
            ("wrong-thread", "identity-mismatch"), ("wrong-source", "revision-mismatch"),
            ("wrong-target", "revision-mismatch"), ("wrong-iteration", "revision-mismatch"),
            ("wrong-principal", "authorization-mismatch"), ("wrong-operation", "authorization-mismatch"),
            ("wrong-packet", "authorization-mismatch"), ("wrong-inspection", "inspection-mismatch"),
            ("wrong-address-receipt", "address-receipt-mismatch"),
            ("wrong-inspection-receipt", "inspection-mismatch"), ("wrong-marker", "authorization-mismatch"),
            ("wrong-reply", "reply-mismatch"), ("wrong-thread-status", "thread-status-invalid"),
            ("wrong-provider-head", "provider-head-mismatch"), ("no-reply", "reply-not-observed"),
            ("multiple", "match-count-invalid"), ("negative-match-count", "match-count-invalid"), ("no-resolve", "resolution-not-observed"),
            ("missing-reply-observed", "fields-missing"), ("missing-resolve-observed", "fields-missing"),
            ("missing-match-count", "fields-missing"), ("missing-provider-receipt", "fields-missing"),
            ("no-receipt", "receipt-missing"),
        };

        foreach (var (behavior, expected) in cases)
        {
            var repo = CreateRepository();
            var operation = behavior == "no-resolve" ? "reply-and-resolve" : "reply";
            var packet = CompleteStage11(repo, "result-" + behavior, behavior, [operation]);
            var session = "result-" + behavior;
            var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, operation), session);
            Assert.True(Continues(initialized), initialized.Output + initialized.Error);
            var gate = MarkerValue(initialized.Output, "gate=");
            var approval = ApprovalPath(session);
            Assert.True(Continues(Approve(repo, session, gate, packet, operation, "thread-1", "Confirm")));

            var applied = RunApply(repo, gate, approval);
            Assert.Equal(2, applied.ExitCode);
            Assert.Contains("OUTCOME_UNKNOWN", applied.Error);
            var attempt = JsonNode.Parse(File.ReadAllText(AttemptPathFor(repo, packet, operation)))!.AsObject();
            Assert.Equal("outcome-unknown", attempt["state"]!.GetValue<string>());
            Assert.Contains(expected, attempt["diagnostic"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(repo, approval.Replace('/', Path.DirectorySeparatorChar))));
        }

        var rejectedRepo = CreateRepository();
        var rejectedPacket = CompleteStage11(rejectedRepo, "result-rejected", "rejected", ["reply"]);
        var rejectedInitialized = RunHook(rejectedRepo, "initialize", "reply-resolve", ReplyPrompt(rejectedPacket, "reply"), "result-rejected");
        var rejectedGate = MarkerValue(rejectedInitialized.Output, "gate=");
        var rejectedApproval = ApprovalPath("result-rejected");
        Assert.True(Continues(Approve(rejectedRepo, "result-rejected", rejectedGate, rejectedPacket, "reply", "thread-1", "Confirm")));
        var rejected = RunApply(rejectedRepo, rejectedGate, rejectedApproval);
        Assert.Equal(1, rejected.ExitCode);
        Assert.Contains("REJECTED", rejected.Error);
        var rejectedAttempt = JsonNode.Parse(File.ReadAllText(AttemptPathFor(rejectedRepo, rejectedPacket, "reply")))!.AsObject();
        Assert.Equal("rejected", rejectedAttempt["state"]!.GetValue<string>());
        var rejectedReplay = RunApply(rejectedRepo, rejectedGate, rejectedApproval);
        Assert.Equal(1, rejectedReplay.ExitCode);
        Assert.Contains("attempt-retry-forbidden", rejectedReplay.Error);
        Assert.Single(ReadMutationCalls(rejectedRepo));
    }

    [Fact]
    public void Stage12_rejectsAdapterStartFailureBeforeApprovalAndMutation()
    {
        var repo = CreateRepository();
        var manifestPath = Path.Combine(repo, ".engloop", "provider-adapters", "fake.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest["command"] = new JsonArray("engloopkit-provider-command-that-does-not-exist", ".engloop/provider-adapters/fake.ps1");
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        Git(repo, "add", ".engloop/provider-adapters/fake.json");
        Git(repo, "commit", "-m", "missing adapter executable");

        var packet = CompleteStage11(repo, "start-failure", "success", ["reply"]);
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), "start-failure");
        Assert.False(Continues(initialized));
        Assert.Contains("failed closed", initialized.Output, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(repo, ".engloop", "out", "code-review-response", "attempts")));
    }

    [Fact]
    public void Stage12_rejectsEveryInvalidProviderInspectionBeforeApprovalOrMutation()
    {
        var cases = new (string Behavior, string Expected)[]
        {
            ("inspect-exit-failure", "inspection-exit-failed"),
            ("inspect-invalid-json", "inspection-invalid"),
            ("inspect-rejected", "inspection-rejected"),
            ("inspect-bad-schema", "inspection-schema-invalid"),
            ("inspect-extra-field", "inspection-invalid"),
            ("inspect-bad-status", "inspection-status-invalid"),
            ("inspect-wrong-provider", "inspection-identity-mismatch"),
            ("inspect-wrong-repository", "inspection-identity-mismatch"),
            ("inspect-wrong-pr", "inspection-identity-mismatch"),
            ("inspect-wrong-thread", "inspection-identity-mismatch"),
            ("inspect-wrong-source", "inspection-revision-mismatch"),
            ("inspect-wrong-target", "inspection-revision-mismatch"),
            ("inspect-wrong-iteration", "inspection-revision-mismatch"),
            ("inspect-wrong-principal", "inspection-principal-mismatch"),
            ("inspect-invalid-thread-status", "inspection-thread-status-invalid"),
            ("inspect-wrong-thread-status", "inspection-thread-changed"),
            ("inspect-wrong-head", "inspection-head-changed"),
            ("inspect-wrong-operation", "inspection-operation-mismatch"),
            ("inspect-wrong-packet", "inspection-packet-mismatch"),
            ("inspect-wrong-address-receipt", "inspection-address-receipt-mismatch"),
            ("inspect-multiple", "inspection-match-count-invalid"),
            ("inspect-negative-match-count", "inspection-match-count-invalid"),
            ("inspect-match-count-omitted", "inspection-match-count-invalid"),
            ("inspect-mutated", "inspection-mutation-forbidden"),
            ("inspect-mutation-omitted", "inspection-mutation-forbidden"),
            ("inspect-no-receipt", "inspection-receipt-missing"),
        };

        foreach (var (behavior, expected) in cases)
        {
            var repo = CreateRepository();
            var packet = CompleteStage11(repo, behavior, behavior, ["reply"]);
            var result = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), behavior);
            Assert.False(Continues(result));
            Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Single(ReadInspectionCalls(repo));
            Assert.Empty(ReadMutationCalls(repo));
            Assert.False(Directory.Exists(Path.Combine(repo, ".engloop", "out", "code-review-response", "approvals")));
        }
    }

    [Fact]
    public void Stage12_rejectsEveryInvalidProviderAdapterContractBeforeApprovalOrMutation()
    {
        var cases = new (string Name, Action<string, JsonObject> Mutate, string Expected)[]
        {
            ("schema", (_, value) => value["schemaVersion"] = "2.0", "adapter-schema-invalid"),
            ("unexpected", (_, value) => value["unexpected"] = true, "adapter-invalid"),
            ("adapter-id", (_, value) => value["adapterId"] = "Bad Adapter", "adapter-id-invalid"),
            ("protocol", (_, value) => value["protocol"] = "other", "adapter-protocol-invalid"),
            ("provider", (_, value) => value["provider"] = "other", "adapter-provider-mismatch"),
            ("provider-whitespace", (_, value) => value["provider"] = "bad provider", "adapter-provider-invalid"),
            ("provider-too-long", (_, value) => value["provider"] = new string('p', 129), "adapter-provider-invalid"),
            ("empty-command", (_, value) => value["command"] = new JsonArray(), "adapter-command-invalid"),
            ("blank-command", (_, value) => value["command"] = new JsonArray("pwsh", ""), "adapter-command-invalid"),
            ("nul-command", (_, value) => value["command"] = new JsonArray("pwsh", "bad\0arg"), "adapter-command-invalid"),
            ("return-command", (_, value) => value["command"] = new JsonArray("pwsh", "bad\rarg"), "adapter-command-invalid"),
            ("newline-command", (_, value) => value["command"] = new JsonArray("pwsh", "bad\narg"), "adapter-command-invalid"),
            ("shell-command", (_, value) => value["command"] = new JsonArray("pwsh", "-Command", "Write-Output bad"), "shell-command-forbidden"),
            ("encoded-shell-command", (_, value) => value["command"] = new JsonArray("pwsh", "-EncodedCommand", "bad"), "shell-command-forbidden"),
            ("short-eval-command", (_, value) => value["command"] = new JsonArray("python", "-c", "bad", ".engloop/provider-adapters/fake.ps1"), "shell-command-forbidden"),
            ("artifact-prefix", (_, value) => value["commandArtifact"] = "scripts/fake.ps1", "path-invalid"),
            ("hash", (_, value) => value["commandArtifactSha256"] = new string('0', 64), "artifact-hash-mismatch"),
            ("missing-command-artifact", (_, value) => value["command"] = new JsonArray("pwsh", "-NoProfile"), "command-artifact-invalid"),
            ("duplicate-command-artifact", (_, value) => value["command"] = new JsonArray("pwsh", "-File", ".engloop/provider-adapters/fake.ps1", ".engloop/provider-adapters/fake.ps1"), "command-artifact-invalid"),
            ("timeout-low", (_, value) => value["timeoutSeconds"] = 0, "timeout-invalid"),
            ("timeout-high", (_, value) => value["timeoutSeconds"] = 301, "timeout-invalid"),
            ("capabilities-empty", (_, value) => value["capabilities"] = new JsonArray(), "capabilities-empty"),
            ("capabilities-duplicate", (_, value) => value["capabilities"] = new JsonArray("inspect", "reply", "reply"), "capabilities-duplicate"),
            ("capabilities-invalid", (_, value) => value["capabilities"] = new JsonArray("inspect", "reply", "merge"), "capabilities-invalid"),
            ("inspection-capability", (_, value) => value["capabilities"] = new JsonArray("reply"), "inspection-capability-missing"),
            ("capability", (_, value) => value["capabilities"] = new JsonArray("inspect", "resolve"), "capability-missing"),
        };

        foreach (var (name, mutate, expected) in cases)
        {
            var repo = CreateRepository();
            var adapterPath = Path.Combine(repo, ".engloop", "provider-adapters", "fake.json");
            var adapter = JsonNode.Parse(File.ReadAllText(adapterPath))!.AsObject();
            mutate(repo, adapter);
            File.WriteAllText(adapterPath, adapter.ToJsonString());
            Git(repo, "add", ".engloop/provider-adapters/fake.json");
            Git(repo, "commit", "-m", "adapter " + name);
            var packet = CompleteStage11(repo, "adapter-" + name, "success", ["reply"]);
            var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), "adapter-" + name);
            Assert.False(Continues(initialized));
            Assert.Contains(expected, initialized.Output, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(AdapterCallsPath(repo)));
        }

        var malformedRepo = CreateRepository();
        File.WriteAllText(Path.Combine(malformedRepo, ".engloop", "provider-adapters", "fake.json"), "{");
        Git(malformedRepo, "add", ".engloop/provider-adapters/fake.json");
        Git(malformedRepo, "commit", "-m", "malformed adapter");
        var malformedPacket = CompleteStage11(malformedRepo, "adapter-malformed", "success", ["reply"]);
        var malformed = RunHook(malformedRepo, "initialize", "reply-resolve", ReplyPrompt(malformedPacket, "reply"), "adapter-malformed");
        Assert.False(Continues(malformed));
        Assert.Contains("adapter-invalid", malformed.Output);

        var artifactRepo = CreateRepository();
        File.AppendAllText(Path.Combine(artifactRepo, ".engloop", "provider-adapters", "fake.ps1"), "# changed\n");
        Git(artifactRepo, "add", ".engloop/provider-adapters/fake.ps1");
        Git(artifactRepo, "commit", "-m", "stale artifact hash");
        var artifactPacket = CompleteStage11(artifactRepo, "adapter-artifact", "success", ["reply"]);
        var artifact = RunHook(artifactRepo, "initialize", "reply-resolve", ReplyPrompt(artifactPacket, "reply"), "adapter-artifact");
        Assert.False(Continues(artifact));
        Assert.Contains("artifact-hash-mismatch", artifact.Output);

        var untrackedRepo = CreateRepository();
        Git(untrackedRepo, "rm", "--cached", ".engloop/provider-adapters/fake.ps1");
        Git(untrackedRepo, "commit", "-m", "untrack adapter artifact");
        File.AppendAllText(Path.Combine(untrackedRepo, ".git", "info", "exclude"), ".engloop/provider-adapters/fake.ps1\n");
        var untrackedPacket = CompleteStage11(untrackedRepo, "adapter-untracked", "success", ["reply"]);
        var untracked = RunHook(untrackedRepo, "initialize", "reply-resolve", ReplyPrompt(untrackedPacket, "reply"), "adapter-untracked");
        Assert.False(Continues(untracked));
        Assert.Contains("artifact-must-be-tracked", untracked.Output);

        var hiddenRepo = CreateRepository();
        var hiddenScript = Path.Combine(hiddenRepo, ".engloop", "provider-adapters", "fake.ps1");
        var hiddenManifest = Path.Combine(hiddenRepo, ".engloop", "provider-adapters", "fake.json");
        File.AppendAllText(hiddenScript, "# hidden change\n");
        var hiddenValue = JsonNode.Parse(File.ReadAllText(hiddenManifest))!.AsObject();
        hiddenValue["commandArtifactSha256"] = FileHash(hiddenScript);
        File.WriteAllText(hiddenManifest, hiddenValue.ToJsonString());
        Git(hiddenRepo, "update-index", "--assume-unchanged", ".engloop/provider-adapters/fake.ps1", ".engloop/provider-adapters/fake.json");
        var hiddenPacket = CompleteStage11(hiddenRepo, "adapter-hidden", "success", ["reply"]);
        var hidden = RunHook(hiddenRepo, "initialize", "reply-resolve", ReplyPrompt(hiddenPacket, "reply"), "adapter-hidden");
        Assert.False(Continues(hidden));
        Assert.Contains("adapter-must-match-head", hidden.Output);
    }

    [Fact]
    public void Stage12_approvalHookRejectsMalformedQuestionAndAcceptsFlattenedHeaderKeyResponse()
    {
        var repo = CreateRepository();
        var packet = CompleteStage11(repo, "question-matrix", "success", ["reply"], replyText: "Addressed `exactly` as verified.");
        const string session = "question-matrix";
        var initialized = RunHook(repo, "initialize", "reply-resolve", ReplyPrompt(packet, "reply"), session);
        Assert.True(Continues(initialized));
        var gate = MarkerValue(initialized.Output, "gate=");
        var packetHash = FileHash(Path.Combine(repo, packet.Replace('/', Path.DirectorySeparatorChar)));
        var questionText = $"Apply reply to thread thread-1 with packet {packetHash[..12]}?";
        var questionMessage = ApprovalMessage(repo, gate, packet, "reply");
        Assert.DoesNotContain("`exactly`", questionMessage, StringComparison.Ordinal);
        Assert.Contains("\\u0060exactly\\u0060", questionMessage, StringComparison.Ordinal);
        var approvalFull = Path.Combine(repo, ApprovalPath(session).Replace('/', Path.DirectorySeparatorChar));

        JsonObject ValidQuestion() => new()
        {
            ["questions"] = new JsonArray
            {
                new JsonObject
                {
                    ["header"] = "Approve review response",
                    ["question"] = questionText,
                    ["message"] = questionMessage,
                    ["multiSelect"] = false,
                    ["allowFreeformInput"] = false,
                    ["options"] = new JsonArray(new JsonObject { ["label"] = "Confirm" }, new JsonObject { ["label"] = "Cancel" }),
                },
            },
        };
        JsonObject Response(string key, params string[] values) => new()
        {
            ["answers"] = new JsonObject
            {
                [key] = new JsonObject { ["selected"] = new JsonArray(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()) },
            },
        };
        (int ExitCode, string Output, string Error) Invoke(object input, object response, string tool = "vscode_askQuestions")
            => RunHookRaw(repo, ["post-tool", "reply-resolve"], JsonSerializer.Serialize(new
            {
                cwd = repo,
                session_id = session,
                tool_name = tool,
                tool_input = input,
                tool_response = response,
                tool_use_id = "question-" + Guid.NewGuid().ToString("N"),
            }));
        void Reject(object input, object response, string expected)
        {
            if (File.Exists(approvalFull)) File.Delete(approvalFull);
            var result = Invoke(input, response);
            Assert.False(Continues(result));
            Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(approvalFull));
        }

        var unrelated = Invoke(new { }, new { }, "read_file");
        Assert.True(Continues(unrelated));
        Assert.False(File.Exists(approvalFull));
        var addressPostTool = RunHookRaw(repo, ["post-tool", "address"], HookJson(repo, "address-post-tool", string.Empty));
        Assert.True(Continues(addressPostTool));

        Reject(new JsonObject(), Response(questionText, "Confirm"), "question-invalid");
        Reject("[]", Response(questionText, "Confirm"), "question-invalid");
        Reject(new JsonArray(), Response(questionText, "Confirm"), "hook-tool-json-missing");
        var extraTopLevel = ValidQuestion(); extraTopLevel["unexpected"] = true;
        Reject(extraTopLevel, Response(questionText, "Confirm"), "question-invalid");
        Reject(new JsonObject { ["questions"] = "bad" }, Response(questionText, "Confirm"), "question-invalid");
        Reject(new JsonObject { ["questions"] = new JsonArray() }, Response(questionText, "Confirm"), "question-count-invalid");
        var wrongHeader = ValidQuestion(); wrongHeader["questions"]![0]!["header"] = "Other";
        Reject(wrongHeader, Response(questionText, "Confirm"), "question-invalid");
        var wrongQuestion = ValidQuestion(); wrongQuestion["questions"]![0]!["question"] = "Other?";
        Reject(wrongQuestion, Response(questionText, "Confirm"), "question-invalid");
        var wrongMessage = ValidQuestion(); wrongMessage["questions"]![0]!["message"] = "Other payload";
        Reject(wrongMessage, Response(questionText, "Confirm"), "message-invalid");
        var extraQuestion = ValidQuestion(); extraQuestion["questions"]![0]!["unexpected"] = true;
        Reject(extraQuestion, Response(questionText, "Confirm"), "question-invalid");
        var multi = ValidQuestion(); multi["questions"]![0]!["multiSelect"] = true;
        Reject(multi, Response(questionText, "Confirm"), "multiselect-forbidden");
        var noMulti = ValidQuestion(); noMulti["questions"]![0]!.AsObject().Remove("multiSelect");
        Reject(noMulti, Response(questionText, "Confirm"), "multiselect-forbidden");
        var freeform = ValidQuestion(); freeform["questions"]![0]!["allowFreeformInput"] = true;
        Reject(freeform, Response(questionText, "Confirm"), "freeform-forbidden");
        var noFreeform = ValidQuestion(); noFreeform["questions"]![0]!.AsObject().Remove("allowFreeformInput");
        Reject(noFreeform, Response(questionText, "Confirm"), "freeform-forbidden");
        var options = ValidQuestion(); options["questions"]![0]!["options"]![0]!["label"] = "Apply";
        Reject(options, Response(questionText, "Confirm"), "options-invalid");
        var optionMetadata = ValidQuestion(); optionMetadata["questions"]![0]!["options"]![0]!["description"] = "hidden";
        Reject(optionMetadata, Response(questionText, "Confirm"), "options-invalid");
        var noOptions = ValidQuestion(); noOptions["questions"]![0]!.AsObject().Remove("options");
        Reject(noOptions, Response(questionText, "Confirm"), "options-invalid");
        var wrongOptions = ValidQuestion(); wrongOptions["questions"]![0]!["options"] = new JsonObject();
        Reject(wrongOptions, Response(questionText, "Confirm"), "options-invalid");
        Reject(ValidQuestion(), new JsonObject(), "answer-invalid");
        Reject(ValidQuestion(), new JsonObject { ["answers"] = new JsonArray() }, "answer-invalid");
        Reject(ValidQuestion(), new JsonObject { ["answers"] = new JsonObject() }, "selection-invalid");
        var extraResponse = Response(questionText, "Confirm"); extraResponse["unexpected"] = true;
        Reject(ValidQuestion(), extraResponse, "answer-invalid");
        var multipleAnswers = Response(questionText, "Confirm"); multipleAnswers["answers"]!["unrelated"] = new JsonObject { ["selected"] = new JsonArray("Cancel") };
        Reject(ValidQuestion(), multipleAnswers, "selection-invalid");
        Reject(ValidQuestion(), new JsonObject { ["answers"] = new JsonObject { [questionText] = new JsonObject() } }, "selection-invalid");
        Reject(ValidQuestion(), new JsonObject { ["answers"] = new JsonObject { [questionText] = new JsonObject { ["selected"] = new JsonObject() } } }, "selection-invalid");
        Reject(ValidQuestion(), Response(questionText, "Confirm", "Cancel"), "selection-invalid");
        Reject(ValidQuestion(), Response(questionText, "Other"), "decision-invalid");
        Reject(ValidQuestion(), new JsonObject { ["answers"] = new JsonObject { [questionText] = new JsonObject { ["selected"] = new JsonArray((JsonNode?)null) } } }, "decision-invalid");

        var missingToolJson = RunHookRaw(repo, ["post-tool", "reply-resolve"], JsonSerializer.Serialize(new
        {
            cwd = repo,
            session_id = session,
            tool_name = "vscode_askQuestions",
            tool_use_id = "missing-tool-json",
        }));
        Assert.False(Continues(missingToolJson));
        Assert.Contains("hook-tool-json-missing", missingToolJson.Output);

        var camel = RunHookRaw(repo, ["post-tool", "reply-resolve"], JsonSerializer.Serialize(new
        {
            cwd = repo,
            sessionId = session,
            toolName = "vscode_askQuestions",
            toolInput = ValidQuestion(),
            toolResponse = Response("Approve review response", "Confirm"),
            toolUseId = "camel-question",
        }));
        Assert.True(Continues(camel), camel.Output + camel.Error);
        Assert.Contains("CODE_REVIEW_RESPONSE_APPROVED", camel.Output);
        File.Delete(approvalFull);

        var flattened = Invoke(JsonSerializer.Serialize(ValidQuestion()), JsonSerializer.Serialize(Response("Approve review response", "Confirm")));
        Assert.True(Continues(flattened), flattened.Output + flattened.Error);
        Assert.Contains("CODE_REVIEW_RESPONSE_APPROVED", flattened.Output);
        Assert.True(File.Exists(approvalFull));
        var duplicate = Invoke(JsonSerializer.Serialize(ValidQuestion()), JsonSerializer.Serialize(Response("Approve review response", "Confirm")));
        Assert.False(Continues(duplicate));
        Assert.Contains("approval-already-exists", duplicate.Output);
        var duplicateCancel = Invoke(JsonSerializer.Serialize(ValidQuestion()), JsonSerializer.Serialize(Response("Approve review response", "Cancel")));
        Assert.False(Continues(duplicateCancel));
        Assert.Contains("approval-already-exists", duplicateCancel.Output);
        Assert.True(File.Exists(approvalFull));
    }

    private string CreateRepository()
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
        File.WriteAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "{\"version\":1,\"isRoot\":true,\"tools\":{\"engloopkit\":{\"version\":\"1.16.0\",\"commands\":[\"engloopkit\"]}}}\n");

        var adapterScript = Path.Combine(repo, ".engloop", "provider-adapters", "fake.ps1");
        File.WriteAllText(adapterScript, FakeAdapterScript);
        var scriptHash = FileHash(adapterScript);
        File.WriteAllText(Path.Combine(repo, ".engloop", "provider-adapters", "fake.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            adapterId = "fixture-adapter",
            provider = "fixture-provider",
            protocol = "engloop-review-response-v1",
            command = new[] { "pwsh", "-NoProfile", "-File", ".engloop/provider-adapters/fake.ps1" },
            commandArtifact = ".engloop/provider-adapters/fake.ps1",
            commandArtifactSha256 = scriptHash,
            timeoutSeconds = 30,
            capabilities = new[] { "inspect", "reply", "resolve", "reply-and-resolve" },
        }));
        Git(repo, "init");
        Git(repo, "config", "user.email", "review-response@example.invalid");
        Git(repo, "config", "user.name", "Review Response Test");
        Git(repo, "add", ".");
        Git(repo, "commit", "-m", "fixture");
        return repo;
    }

    private static string CompleteStage11(string repo, string suffix, string behavior, string[] operations, string? providerHead = null, string? replyText = null, string threadStatus = "active")
    {
        var packet = $".engloop/out/code-review-response/address/{suffix}.json";
        var status = StatusDigest(repo);
        var initialized = RunHook(repo, "initialize", "address", AddressPrompt(repo, packet), "address-" + suffix);
        Assert.True(Continues(initialized), initialized.Output + initialized.Error);
        WritePacket(repo, packet, status, status, "already-addressed", behavior, false, false, allowedOperations: operations, providerHead: providerHead, replyText: replyText, threadStatus: threadStatus);
        var stopped = RunHook(repo, "stop", "address", string.Empty, "address-" + suffix);
        Assert.True(Continues(stopped), stopped.Output + stopped.Error);
        Assert.Contains("CODE_REVIEW_ADDRESS_OK", stopped.Output);
        return packet;
    }

    private static string AddressPrompt(string repo, string packet)
    {
        var head = GitOutput(repo, "rev-parse", "HEAD");
        return $"--provider fixture-provider --repository fixture-repo --pr pr-1 --thread thread-1 --source {head} --target {new string('b', 40)} --iteration iteration-1 --packet {packet}";
    }

    private static string ReplyPrompt(string packet, string operation)
        => $"--packet {packet} --operation {operation} --adapter .engloop/provider-adapters/fake.json";

    private static void WritePacket(string repo, string relative, string initialStatus, string finalStatus, string classification, string behavior,
        bool providerMutation, bool commitPush, string[]? changedFiles = null, string[]? validation = null, string[]? allowedOperations = null, string? providerHead = null, string? replyText = null, string threadStatus = "active")
    {
        var head = GitOutput(repo, "rev-parse", "HEAD");
        var full = Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            artifactType = "code-review-address",
            provider = "fixture-provider",
            repository = "fixture-repo",
            pullRequest = "pr-1",
            thread = "thread-1",
            sourceRevision = head,
            targetRevision = new string('b', 40),
            iteration = "iteration-1",
            actingPrincipal = "fixture-principal",
            threadStatus,
            classification,
            initialHead = head,
            finalHead = head,
            initialStatusDigest = initialStatus,
            finalStatusDigest = finalStatus,
            changedFiles = changedFiles ?? Array.Empty<string>(),
            validationResults = validation ?? new[] { "evidence-only:PASS" },
            evidence = new[] { "provider-thread:thread-1", "current-source:bound" },
            unresolvedRisks = Array.Empty<string>(),
            replyText = replyText ?? "Addressed the selected feedback and verified the declared checks.",
            allowedOperations = allowedOperations ?? new[] { "reply" },
            requiredFixRevision = (allowedOperations ?? new[] { "reply" }).Any(operation => operation.Contains("resolve", StringComparison.Ordinal)) ? head : string.Empty,
            providerHead = providerHead ?? head,
            providerMutationPerformed = providerMutation,
            commitPushPerformed = commitPush,
            adapterRequest = new { behavior },
        }));
    }

    private static (int ExitCode, string Output, string Error) Approve(string repo, string session, string gate, string packet, string operation, string thread, string decision)
    {
        var packetHash = FileHash(Path.Combine(repo, packet.Replace('/', Path.DirectorySeparatorChar)));
        var questionText = $"Apply {operation} to thread {thread} with packet {packetHash[..12]}?";
        var input = ApprovalQuestion(repo, gate, packet, operation, thread);
        var response = new { answers = new Dictionary<string, object> { [questionText] = new { selected = new[] { decision } } } };
        return RunHookRaw(repo, ["post-tool", "reply-resolve"], JsonSerializer.Serialize(new
        {
            cwd = repo,
            session_id = session,
            tool_name = "vscode_askQuestions",
            tool_input = input,
            tool_response = response,
            tool_use_id = "approval-" + Guid.NewGuid().ToString("N"),
        }));
    }

    private static object ApprovalQuestion(string repo, string gate, string packet, string operation, string thread)
    {
        var packetHash = FileHash(Path.Combine(repo, packet.Replace('/', Path.DirectorySeparatorChar)));
        var message = ApprovalMessage(repo, gate, packet, operation);
        return new
        {
            questions = new[]
            {
                new
                {
                    header = "Approve review response",
                    question = $"Apply {operation} to thread {thread} with packet {packetHash[..12]}?",
                    message,
                    multiSelect = false,
                    allowFreeformInput = false,
                    options = new[] { new { label = "Confirm" }, new { label = "Cancel" } },
                },
            },
        };
    }

    private static string ApprovalMessage(string repo, string gate, string packet, string operation)
    {
        using var packetJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, packet.Replace('/', Path.DirectorySeparatorChar))));
        using var gateJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, gate.Replace('/', Path.DirectorySeparatorChar))));
        var value = packetJson.RootElement;
        var gateValue = gateJson.RootElement;
        var json = JsonSerializer.Serialize(new
        {
            provider = value.GetProperty("provider").GetString(),
            repository = value.GetProperty("repository").GetString(),
            pullRequest = value.GetProperty("pullRequest").GetString(),
            thread = value.GetProperty("thread").GetString(),
            sourceRevision = value.GetProperty("sourceRevision").GetString(),
            targetRevision = value.GetProperty("targetRevision").GetString(),
            iteration = value.GetProperty("iteration").GetString(),
            actingPrincipal = value.GetProperty("actingPrincipal").GetString(),
            threadStatus = value.GetProperty("threadStatus").GetString(),
            classification = value.GetProperty("classification").GetString(),
            providerHead = value.GetProperty("providerHead").GetString(),
            operation,
            replyText = value.GetProperty("replyText").GetString(),
            evidence = value.GetProperty("evidence").EnumerateArray().Select(item => item.GetString()).ToArray(),
            unresolvedRisks = value.GetProperty("unresolvedRisks").EnumerateArray().Select(item => item.GetString()).ToArray(),
            packetSha256 = gateValue.GetProperty("packetSha256").GetString(),
            addressReceiptSha256 = gateValue.GetProperty("addressReceiptSha256").GetString(),
            providerInspectionSha256 = gateValue.GetProperty("inspectionSha256").GetString(),
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
        return "Review this exact provider operation before confirming:\n\n```json\n" + json + "\n```";
    }

    private static (int ExitCode, string Output, string Error) RunGuard(string repo, string mode, string session, string toolName, object toolInput)
        => RunHookRaw(repo, ["guard", mode], JsonSerializer.Serialize(new { cwd = repo, session_id = session, tool_name = toolName, tool_input = toolInput }));

    private static (int ExitCode, string Output, string Error) RunHook(string repo, string action, string mode, string prompt, string session)
        => RunHookRaw(repo, [action, mode], HookJson(repo, session, prompt));

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
            var exit = CodeReviewResponseCommands.ExecuteHook(args);
            return (exit, output.ToString().Trim(), error.ToString().Trim());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static (int ExitCode, string Output, string Error) RunApply(string repo, string gate, string approval)
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
            var exit = Program.Main(["code-review-response", "apply", "--gate", gate, "--approval", approval]);
            return (exit, output.ToString().Trim(), error.ToString().Trim());
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunApplyProcessAsync(string repo, string gate, string approval)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { typeof(Program).Assembly.Location, "code-review-response", "apply", "--gate", gate, "--approval", approval })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, (await output).Trim(), (await error).Trim());
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

    private static string ApprovalPath(string session) => $".engloop/out/code-review-response/approvals/{Sha256Text(session)}.json";
    private static string ApplyCommand(string gate, string approval) => $"dotnet tool run engloopkit -- code-review-response apply --gate {gate} --approval {approval}";
    private static string AdapterCallsPath(string repo) => Path.Combine(repo, ".engloop", "out", "code-review-response", "adapter-calls.jsonl");
    private static string AttemptPathFor(string repo, string packet, string operation)
        => Path.Combine(repo, ".engloop", "out", "code-review-response", "attempts", FileHash(Path.Combine(repo, packet.Replace('/', Path.DirectorySeparatorChar))) + "." + operation + ".json");

    private static JsonElement[] ReadAdapterCalls(string repo)
        => File.ReadAllLines(AdapterCallsPath(repo)).Where(line => line.Length > 0).Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
    private static JsonElement[] ReadInspectionCalls(string repo)
        => ReadAdapterCalls(repo).Where(call => call.GetProperty("phase").GetString() == "inspect").ToArray();
    private static JsonElement[] ReadMutationCalls(string repo)
        => ReadAdapterCalls(repo).Where(call => call.GetProperty("phase").GetString() != "inspect").ToArray();

    private static string StatusDigest(string repo)
    {
        var output = GitRawOutput(repo, "status", "--porcelain=v1", "--untracked-files=all", "--", ":(exclude).engloop/out");
        return Sha256Text(output.Replace("\r\n", "\n").TrimEnd('\n'));
    }

    private static string GitOutput(string repo, params string[] args)
        => GitRawOutput(repo, args).Trim();

    private static string GitRawOutput(string repo, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output + error);
        return output;
    }

    private static void Git(string repo, params string[] args) => _ = GitOutput(repo, args);
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Sha256Text(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public void Dispose()
    {
        if (!Directory.Exists(_work)) return;
        try { Directory.Delete(_work, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private const string FakeAdapterScript = """
$ErrorActionPreference = 'Stop'
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$log = Join-Path (Get-Location) '.engloop/out/code-review-response/adapter-calls.jsonl'
New-Item -ItemType Directory -Force (Split-Path $log -Parent) | Out-Null
Add-Content -LiteralPath $log -Value ($payload | ConvertTo-Json -Depth 32 -Compress)
$behavior = [string]$payload.adapterRequest.behavior
if ([string]$payload.phase -eq 'inspect') {
    if ($behavior -eq 'inspect-exit-failure') { exit 9 }
    if ($behavior -eq 'inspect-invalid-json') { 'not-json'; exit 0 }
    $inspection = [ordered]@{
        schemaVersion = '1.0'
        status = if ($behavior -eq 'inspect-rejected') { 'rejected' } else { 'ready' }
        provider = [string]$payload.provider
        repository = [string]$payload.repository
        pullRequest = [string]$payload.pullRequest
        thread = [string]$payload.thread
        sourceRevision = [string]$payload.sourceRevision
        targetRevision = [string]$payload.targetRevision
        iteration = [string]$payload.iteration
        actingPrincipal = [string]$payload.actingPrincipal
        threadStatus = [string]$payload.expectedThreadStatus
        providerHead = [string]$payload.expectedProviderHead
        operation = [string]$payload.operation
        packetSha256 = [string]$payload.packetSha256
        addressReceiptSha256 = [string]$payload.addressReceiptSha256
        matchCount = 1
        mutationPerformed = $false
        inspectionReceiptId = 'fixture-inspection-' + [string]$payload.packetSha256
    }
    switch ($behavior) {
        'inspect-bad-schema' { $inspection.schemaVersion = '2.0' }
        'inspect-extra-field' { $inspection['unexpected'] = 'value' }
        'inspect-bad-status' { $inspection.status = 'unknown' }
        'inspect-wrong-provider' { $inspection.provider = 'other-provider' }
        'inspect-wrong-repository' { $inspection.repository = 'other-repository' }
        'inspect-wrong-pr' { $inspection.pullRequest = 'other-pr' }
        'inspect-wrong-thread' { $inspection.thread = 'other-thread' }
        'inspect-wrong-source' { $inspection.sourceRevision = ('c' * 40) }
        'inspect-wrong-target' { $inspection.targetRevision = ('c' * 40) }
        'inspect-wrong-iteration' { $inspection.iteration = 'other-iteration' }
        'inspect-wrong-principal' { $inspection.actingPrincipal = 'other-principal' }
        'inspect-wrong-thread-status' { $inspection.threadStatus = 'resolved' }
        'inspect-invalid-thread-status' { $inspection.threadStatus = 'unknown' }
        'inspect-wrong-head' { $inspection.providerHead = ('c' * 40) }
        'inspect-wrong-operation' { $inspection.operation = 'resolve' }
        'inspect-wrong-packet' { $inspection.packetSha256 = ('0' * 64) }
        'inspect-wrong-address-receipt' { $inspection.addressReceiptSha256 = ('0' * 64) }
        'inspect-multiple' { $inspection.matchCount = 2 }
        'inspect-negative-match-count' { $inspection.matchCount = -1 }
        'inspect-match-count-omitted' { $inspection.Remove('matchCount') }
        'inspect-mutated' { $inspection.mutationPerformed = $true }
        'inspect-mutation-omitted' { $inspection.Remove('mutationPerformed') }
        'inspect-no-receipt' { $inspection.inspectionReceiptId = '' }
    }
    $inspection | ConvertTo-Json -Depth 32 -Compress
    exit 0
}
if ($behavior -eq 'unknown-once' -and $payload.phase -eq 'apply') {
    [Console]::Error.WriteLine('simulated ambiguous provider outcome')
    exit 7
}
if ($behavior -eq 'exit-failure') { exit 8 }
if ($behavior -eq 'invalid-json') { 'not-json'; exit 0 }
if ($behavior -eq 'timeout-on-apply') { [Threading.Thread]::Sleep(30000) }
if ($behavior -eq 'slow-success') { [Threading.Thread]::Sleep(750) }
$status = if ($behavior -eq 'rejected') { 'rejected' } elseif ($behavior -eq 'reported-unknown') { 'outcome-unknown' } else { 'success' }
$resolve = ([string]$payload.operation).Contains('resolve')
$result = [ordered]@{
    schemaVersion = '1.0'
    status = $status
    provider = [string]$payload.provider
    repository = [string]$payload.repository
    pullRequest = [string]$payload.pullRequest
    thread = [string]$payload.thread
    sourceRevision = [string]$payload.sourceRevision
    targetRevision = [string]$payload.targetRevision
    iteration = [string]$payload.iteration
    actingPrincipal = if ($behavior -eq 'wrong-principal') { 'other-principal' } else { [string]$payload.actingPrincipal }
    threadStatus = if ($resolve) { 'resolved' } else { 'active' }
    providerHead = [string]$payload.expectedProviderHead
    operation = [string]$payload.operation
    packetSha256 = [string]$payload.packetSha256
    addressReceiptSha256 = [string]$payload.addressReceiptSha256
    inspectionSha256 = [string]$payload.inspectionSha256
    inspectionReceiptId = [string]$payload.inspectionReceiptId
    marker = [string]$payload.marker
    replySha256 = [string]$payload.replySha256
    replyObserved = ($status -eq 'success' -and $behavior -ne 'resolve-no-reply')
    resolveObserved = ($status -eq 'success' -and $resolve)
    matchCount = if ($behavior -eq 'multiple') { 2 } else { 1 }
    providerReceiptId = if ($status -eq 'success') { 'fixture-receipt-' + [string]$payload.marker } else { '' }
}
switch ($behavior) {
    'bad-schema' { $result.schemaVersion = '2.0' }
    'extra-field' { $result['unexpected'] = 'value' }
    'bad-status' { $result.status = 'mystery' }
    'wrong-provider' { $result.provider = 'other-provider' }
    'wrong-repository' { $result.repository = 'other-repository' }
    'wrong-pr' { $result.pullRequest = 'other-pr' }
    'wrong-thread' { $result.thread = 'other-thread' }
    'wrong-source' { $result.sourceRevision = ('c' * 40) }
    'wrong-target' { $result.targetRevision = ('c' * 40) }
    'wrong-iteration' { $result.iteration = 'other-iteration' }
    'wrong-operation' { $result.operation = 'resolve' }
    'wrong-packet' { $result.packetSha256 = ('0' * 64) }
    'wrong-address-receipt' { $result.addressReceiptSha256 = ('0' * 64) }
    'wrong-inspection' { $result.inspectionSha256 = ('0' * 64) }
    'wrong-inspection-receipt' { $result.inspectionReceiptId = 'other-inspection' }
    'wrong-marker' { $result.marker = 'other-marker' }
    'wrong-reply' { $result.replySha256 = ('0' * 64) }
    'wrong-thread-status' { $result.threadStatus = 'unknown' }
    'wrong-provider-head' { $result.providerHead = ('c' * 40) }
    'no-reply' { $result.replyObserved = $false }
    'no-resolve' { $result.resolveObserved = $false }
    'missing-reply-observed' { $result.Remove('replyObserved') }
    'missing-resolve-observed' { $result.Remove('resolveObserved') }
    'missing-match-count' { $result.Remove('matchCount') }
    'missing-provider-receipt' { $result.Remove('providerReceiptId') }
    'multiple' { $result.matchCount = 2 }
    'negative-match-count' { $result.matchCount = -1 }
    'no-receipt' { $result.providerReceiptId = '' }
}
$result | ConvertTo-Json -Depth 32 -Compress
""";
}
