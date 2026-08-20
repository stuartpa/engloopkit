using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        Assert.False(Continues(missingPath));
        Assert.Contains("--postmortem", missingPath.Output);
    }

    [Fact]
    public void PostmortemGate_isCreateNewHeadAndArgumentBound()
    {
        var repo = CreateRepository();
        const string prompt = "--incidents IN001 --postmortem .engloop/postmortems/PM005_example.md";
        var initialized = RunHook(repo, "postmortem", "initialize", prompt, "pm-session");
        Assert.True(Continues(initialized), initialized.Output);
        Assert.Contains("OPERATIONS_LEARNING_SCOPE_ACTIVE", initialized.Output);

        var followup = RunHook(repo, "postmortem", "initialize", "continue analysis", "pm-session");
        Assert.True(Continues(followup), followup.Output);

        var changed = RunHook(repo, "postmortem", "initialize", "--incidents IN002 --postmortem .engloop/postmortems/PM006_other.md", "pm-session");
        Assert.False(Continues(changed));
        Assert.Contains("arguments-changed", changed.Output);
    }

    [Fact]
    public void PostmortemGate_rejectsExistingOrPreviouslyUsedNumber()
    {
        var repo = CreateRepository();
        Directory.CreateDirectory(Path.Combine(repo, ".engloop", "postmortems"));
        File.WriteAllText(Path.Combine(repo, ".engloop", "postmortems", "PM005_existing.md"), "existing\n");
        var result = RunHook(repo, "postmortem", "initialize", "--incidents IN001 --postmortem .engloop/postmortems/PM005_new.md", "pm-existing");
        Assert.False(Continues(result));
        Assert.Contains("postmortem-number-already-used", result.Output);
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
    public void SubprocessStart_emitsOneJsonResponseFromCompiledTool()
    {
        var repo = CreateRepository();
        var result = RunHookSubprocess(repo, "incident", "start", string.Empty, "subprocess-session");
        Assert.True(Continues(result));
        Assert.Contains("OPERATIONS_LEARNING_GUARD_ACTIVE", result.Output);
    }

    [Fact]
    public void Hook_rejectsMalformedDispatchRootArgumentsAndGateStateMatrix()
    {
        var repo = CreateRepository();
        Assert.False(Continues(RunHookRaw(repo, [], "{}")));
        Assert.False(Continues(RunHookRaw(repo, ["start"], "{}")));
        Assert.False(Continues(RunHookRaw(repo, ["start", "unknown"], HookJson(repo, "s", string.Empty))));
        Assert.False(Continues(RunHookRaw(repo, ["unknown", "incident"], HookJson(repo, "s", string.Empty))));
        Assert.False(Continues(RunHookRaw(repo, ["start", "incident"], "not-json")));
        Assert.False(Continues(RunHookRaw(repo, ["start", "incident"], JsonSerializer.Serialize(new { session_id = "s" }))));

        var child = Path.Combine(repo, "child");
        Directory.CreateDirectory(child);
        Assert.False(Continues(RunHookRaw(repo, ["start", "incident"], HookJson(child, "s", string.Empty))));
        Assert.False(Continues(RunHook(repo, "incident", "initialize", string.Empty, "empty-prompt")));
        Assert.False(Continues(RunHook(repo, "incident", "initialize", "--incident C:/absolute.md", "absolute")));
        Assert.False(Continues(RunHook(repo, "incident", "initialize", "--incident .engloop/postmortems/PM001.md", "wrong-prefix")));
        Assert.False(Continues(RunHook(repo, "postmortem", "initialize", "--incidents IN001,IN001 --postmortem .engloop/postmortems/PM005.md", "duplicate-incidents")));
        Assert.False(Continues(RunHook(repo, "postmortem", "initialize", "--incidents BAD --postmortem .engloop/postmortems/PM005.md", "bad-incident")));
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
        Assert.False(Continues(RunHook(noIgnore, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "not-ignored")));

        var noManifest = CreateRepository();
        File.Delete(Path.Combine(noManifest, ".config", "dotnet-tools.json"));
        Assert.False(Continues(RunHook(noManifest, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "missing-manifest")));
        var wrongVersion = CreateRepository();
        File.WriteAllText(Path.Combine(wrongVersion, ".config", "dotnet-tools.json"), "{\"version\":1,\"isRoot\":true,\"tools\":{\"engloopkit\":{\"version\":\"9.9.9\",\"commands\":[\"engloopkit\"]}}}");
        Assert.False(Continues(RunHook(wrongVersion, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "wrong-version")));
        Assert.False(Continues(RunHook(repo, "incident", "stop", string.Empty, "missing-gate")));

        var unborn = CreateRepository();
        Git(unborn, "checkout", "--orphan", "unborn");
        Git(unborn, "rm", "-rf", ".");
        File.WriteAllText(Path.Combine(unborn, ".gitignore"), ".engloop/out/\n");
        Assert.False(Continues(RunHook(unborn, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "unborn-head")));
    }

    [Fact]
    public void ExistingGate_rejectsCorruptIdentityHeadAndJsonMatrix()
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
            Assert.False(Continues(result));
            Assert.Contains(expected, result.Output);
        }

        RejectMutation(gate => gate["SchemaVersion"] = "2.0", "existing-gate-stale");
        RejectMutation(gate => gate["Mode"] = "repair", "existing-gate-stale");
        RejectMutation(gate => gate["SessionHash"] = new string('0', 64), "existing-gate-stale");
        RejectMutation(gate => gate["Head"] = new string('0', 40), "existing-gate-stale");

        var corrupt = CreateRepository();
        Assert.True(Continues(RunHook(corrupt, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "corrupt-json")));
        var corruptPath = Assert.Single(Directory.GetFiles(Path.Combine(corrupt, ".engloop", "out", "operations-learning-gates"), "*.json"));
        File.WriteAllText(corruptPath, "{");
        Assert.False(Continues(RunHook(corrupt, "incident", "initialize", "continue", "corrupt-json")));

        var head = CreateRepository();
        Assert.True(Continues(RunHook(head, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "head-change")));
        File.WriteAllText(Path.Combine(head, "new.txt"), "new");
        Git(head, "add", "new.txt");
        Git(head, "commit", "-m", "new head");
        Assert.False(Continues(RunHook(head, "incident", "stop", string.Empty, "head-change")));
    }

    [Fact]
    public void Hook_stopModesAndNullGate_failClosedWithoutProtocolDrift()
    {
        var nullGate = CreateRepository();
        Assert.True(Continues(RunHook(nullGate, "incident", "initialize", "--incident .engloop/incidents/IN002.md", "null-gate")));
        var nullPath = Assert.Single(Directory.GetFiles(Path.Combine(nullGate, ".engloop", "out", "operations-learning-gates"), "*.json"));
        File.WriteAllText(nullPath, "null");
        Assert.False(Continues(RunHook(nullGate, "incident", "stop", string.Empty, "null-gate")));

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
        File.WriteAllText(Path.Combine(repo, ".config", "dotnet-tools.json"), "{\"version\":1,\"isRoot\":true,\"tools\":{\"engloopkit\":{\"version\":\"1.15.0\",\"commands\":[\"engloopkit\"]}}}\n");
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
