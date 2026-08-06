using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class TokenEfficiencyHookTests : IDisposable
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string Scripts = Path.Combine(Root, "extensions", "engloopkit", "scripts");
    private readonly string _work = Path.Combine(Path.GetTempPath(), "elk-token-hooks-" + Guid.NewGuid().ToString("N"));

    public TokenEfficiencyHookTests() => Directory.CreateDirectory(_work);

    [Fact]
    public void AnalysisGuard_allowsOneValidEvidenceArtifact_andBlocksOtherWrites()
    {
        var repo = CreateRepository();
        var session = "analysis-session";
        var start = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "SessionStart"], Hook(repo, session));
        Assert.True(Continue(start));
        Assert.Contains("TOKEN_EFFICIENCY_ANALYSIS_GUARD_ACTIVE", start.Output);

        var analysisId = "known-session-20260805T120000000Z";
        var relative = $".engloop/evidence/token-efficiency-analysis-{analysisId}.json";
        var content = Analysis(repo, analysisId, relative).ToJsonString();
        var create = RunScript("Guard-TokenEfficiencyAgent.ps1", ["-Mode", "analysis", "-Event", "PreToolUse"], Hook(repo, session, "create_file", new { filePath = Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar)), content }));
        Assert.True(Continue(create), create.Output + "\n" + create.Error);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(repo, relative))!);
        File.WriteAllText(Path.Combine(repo, relative), content);

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
        var init = RunScript("Initialize-TokenEfficiencyImplementationGate.ps1", ["-RepositoryRoot", repo, "-Prompt", prompt, "-SessionId", session], string.Empty);
        Assert.True(Continue(init), init.Output + "\n" + init.Error);
        Assert.Contains("TOKEN_EFFICIENCY_IMPLEMENTATION_SCOPE_ACTIVE", init.Output);

        var followUp = RunScript("Initialize-TokenEfficiencyImplementationGate.ps1", ["-RepositoryRoot", repo, "-Prompt", "continue focused repair", "-SessionId", session], string.Empty);
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

        var result = RunScript("Initialize-TokenEfficiencyImplementationGate.ps1", ["-RepositoryRoot", repo, "-Prompt", $"--analysis {relative} --approve TE-R999", "-SessionId", "bad-session"], string.Empty);
        Assert.False(Continue(result));
        Assert.Contains("repair", result.Output, StringComparison.OrdinalIgnoreCase);
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

    private string CreateRepository()
    {
        var repo = Path.Combine(_work, "repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(repo, ".engloop", "evidence"));
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "README.md"), "old\n");
        File.WriteAllText(Path.Combine(repo, "docs", ".keep"), "keep\n");
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

    private static string Hook(string repo, string session, string? toolName = null, object? toolInput = null)
        => JsonSerializer.Serialize(new { cwd = repo, session_id = session, tool_name = toolName, tool_input = toolInput });

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
