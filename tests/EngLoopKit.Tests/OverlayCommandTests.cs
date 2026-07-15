using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>
/// End-to-end, non-UI overlay transaction coverage. It uses disposable Git repositories
/// and a uniquely versioned local tool nupkg, so no test writes into a consumer or the
/// checked-in repository state.
/// </summary>
public sealed class OverlayCommandTests : IDisposable
{
    private static readonly string SourceRoot = FindRepoRoot();
    private readonly string _work = Path.Combine(Path.GetTempPath(), "elk-overlay-command-" + Guid.NewGuid().ToString("N"));
    private readonly string _bare;
    private readonly string _source;
    private readonly string _target;
    private readonly string _wrong;
    private readonly string _driver;
    private readonly string _artifactDir;
    private readonly string _archive;
    private readonly string _version = "1.8.1-overlay" + Guid.NewGuid().ToString("N")[..8];

    public OverlayCommandTests()
    {
        _bare = Path.Combine(_work, "origin.git");
        _source = Path.Combine(_work, "source");
        _target = Path.Combine(_work, "target");
        _wrong = Path.Combine(_work, "wrong");
        _driver = Path.Combine(_work, "driver");
        _artifactDir = Path.Combine(_work, "artifacts");
        _archive = Path.Combine(_work, "private-overlay.zip");
        Directory.CreateDirectory(_work);
    }

    [Fact]
    public void ExplicitOverlayInstall_verify_pack_unpack_andLocalGitProtection_workEndToEnd()
    {
        CreateRepository();
        var (nupkg, extension) = BuildArtifacts();
        InstallDriver(nupkg);

        // Mode is mandatory; a normal install cannot silently turn into an overlay.
        Assert.NotEqual(0, OverlayCommands.Execute(["install", "--root", _source]));

        var installExit = OverlayCommands.Execute(InstallArgs(_source, nupkg, extension));
        Assert.True(installExit == 0, OverlayCommands.LastError);
        Assert.Equal(0, OverlayCommands.Execute(["verify", "--root", _source]));

        var managed = new[]
        {
            ".engloop/config.json",
            ".engloop-overlay/manifest.json",
            ".config/dotnet-tools.json",
            ".github/agents/speckit.engloop.01-northstar.agent.md",
        };
        foreach (var path in managed)
        {
            Assert.Equal(0, Run("git", _source, "check-ignore", "-q", "--", path).ExitCode);
            Assert.Equal(string.Empty, Run("git", _source, "ls-files", "--", path).StandardOutput.Trim());
        }
        Assert.Equal(string.Empty, Run("git", _source, "status", "--short").StandardOutput.Trim());

        // Normal local pre-commit hook catches force-staging a managed ELK file.
        Assert.Equal(0, Run("git", _source, "add", "-f", ".engloop/config.json").ExitCode);
        var commit = Run("git", _source, ["commit", "-m", "must be blocked"], throwOnFailure: false);
        Assert.NotEqual(0, commit.ExitCode);
        Assert.Contains("overlay-managed-path-staged", commit.StandardOutput + commit.StandardError);
        Assert.Equal(0, Run("git", _source, "reset", "--", ".engloop/config.json").ExitCode);

        // Plain archives reject secret-like files even when they are locally ignored.
        File.WriteAllText(Path.Combine(_source, ".engloop", ".env.local"), "not-a-secret");
        Assert.NotEqual(0, OverlayCommands.Execute(["pack", "--root", _source, "--output", _archive]));
        File.Delete(Path.Combine(_source, ".engloop", ".env.local"));

        Assert.Equal(0, OverlayCommands.Execute(["pack", "--root", _source, "--output", _archive]));
        Assert.True(File.Exists(_archive));

        Clone(_target);
        Assert.Equal(0, OverlayCommands.Execute(["unpack", "--root", _target, "--input", _archive, "--repository-id", "overlay-test-repository"]));
        Assert.Equal(0, OverlayCommands.Execute(["verify", "--root", _target]));
        Assert.Equal(string.Empty, Run("git", _target, "status", "--short").StandardOutput.Trim());
        AssertManifestsMatch(_source, _target);

        // Existing overlay state is a collision; never merge/overwrite a second install.
        Assert.NotEqual(0, OverlayCommands.Execute(InstallArgs(_target, nupkg, extension)));

        Clone(_wrong);
        Assert.NotEqual(0, OverlayCommands.Execute(["unpack", "--root", _wrong, "--input", _archive, "--repository-id", "wrong-identity"]));
        Assert.False(Directory.Exists(Path.Combine(_wrong, ".engloop")));
    }

    [Fact]
    public void Unpack_rejectsOriginMismatch_beforeCreatingManagedPaths()
    {
        CreateRepository();
        var (nupkg, extension) = BuildArtifacts();
        InstallDriver(nupkg);
        var installExit = OverlayCommands.Execute(InstallArgs(_source, nupkg, extension));
        Assert.True(installExit == 0, OverlayCommands.LastError);
        Assert.Equal(0, OverlayCommands.Execute(["pack", "--root", _source, "--output", _archive]));

        Run("git", _work, "init", _wrong);
        ConfigureGit(_wrong);
        File.WriteAllText(Path.Combine(_wrong, "README.md"), "# other");
        Run("git", _wrong, "add", "README.md");
        Run("git", _wrong, "commit", "-m", "initial");
        Run("git", _wrong, "remote", "add", "origin", Path.Combine(_work, "other-origin.git"));

        Assert.NotEqual(0, OverlayCommands.Execute(["unpack", "--root", _wrong, "--input", _archive, "--repository-id", "overlay-test-repository"]));
        Assert.False(Directory.Exists(Path.Combine(_wrong, ".engloop")));
    }

    [Fact]
    public void CoexistHost_preservesExistingAgentFilesAndChainsExistingPrePushHook()
    {
        CreateRepository();
        Run("specify", _source, "init", "--here", "--force", "--integration", "copilot", "--script", "ps", "--ignore-agent-tools");

        var agents = Path.Combine(_source, ".github", "agents");
        var trackedAgent = Path.Combine(agents, "existing.agent.md");
        var localAgent = Path.Combine(agents, "local.agent.md");
        File.WriteAllText(trackedAgent, "tracked existing agent\n");
        File.WriteAllText(localAgent, "local existing agent\n");
        var trackedBytes = File.ReadAllBytes(trackedAgent);
        var localBytes = File.ReadAllBytes(localAgent);

        Run("git", _source, "add", trackedAgent);
        Run("git", _source, "commit", "-m", "existing agent host");
        Run("git", _source, "push");

        var prePush = Path.Combine(_source, ".git", "hooks", "pre-push");
        var lfsHook = "#!/bin/sh\ncommand -v git-lfs >/dev/null 2>&1 || { echo >&2 \"git-lfs missing\"; exit 2; }\ngit lfs pre-push \"$@\"\n";
        File.WriteAllText(prePush, lfsHook);
        var lfsHookBytes = File.ReadAllBytes(prePush);

        var (nupkg, extension) = BuildArtifacts();
        InstallDriver(nupkg);
        var installExit = OverlayCommands.Execute(InstallArgs(_source, nupkg, extension, "coexist"));
        Assert.True(installExit == 0, OverlayCommands.LastError);

        Assert.Equal(trackedBytes, File.ReadAllBytes(trackedAgent));
        Assert.Equal(localBytes, File.ReadAllBytes(localAgent));
        Assert.True(File.Exists(Path.Combine(_source, ".github", "agents", "speckit.engloop.01-northstar.agent.md")));
        Assert.True(File.Exists(Path.Combine(_source, ".github", "prompts", "speckit.engloop.01-northstar.prompt.md")));
        Assert.Equal(lfsHookBytes, File.ReadAllBytes(prePush + ".elk-prior"));
        var wrapper = File.ReadAllText(prePush);
        Assert.Contains("ELK_OVERLAY_HOOK", wrapper);
        Assert.Contains("pre-push.elk-prior", wrapper);

        Assert.Equal(0, OverlayCommands.Execute(["verify", "--root", _source, "--mode", "all"]));
        Assert.Equal(string.Empty, Run("git", _source, "ls-files", "--", ".github/agents/speckit.engloop.01-northstar.agent.md").StandardOutput.Trim());
        Assert.Equal(".github/agents/existing.agent.md", Run("git", _source, "ls-files", "--", ".github/agents/existing.agent.md").StandardOutput.Trim());
    }

    private void CreateRepository()
    {
        Run("git", _work, "init", "--bare", _bare);
        Run("git", _work, "init", _source);
        ConfigureGit(_source);
        File.WriteAllText(Path.Combine(_source, "README.md"), "# Existing repository");
        Run("git", _source, "add", "README.md");
        Run("git", _source, "commit", "-m", "initial");
        Run("git", _source, "branch", "-M", "main");
        Run("git", _source, "remote", "add", "origin", _bare);
        Run("git", _source, "push", "-u", "origin", "main");
    }

    private (string Nupkg, string Extension) BuildArtifacts()
    {
        Directory.CreateDirectory(_artifactDir);
        Run("dotnet", SourceRoot, "pack", "src/EngLoopKit.Tool/EngLoopKit.Tool.csproj", "-c", "Release", "-o", _artifactDir, "--nologo", "-p:Version=" + _version);
        var nupkg = Directory.GetFiles(_artifactDir, "engloopkit." + _version + "*.nupkg").Single();
        var extension = Path.Combine(_artifactDir, "extension.zip");
        ZipFile.CreateFromDirectory(Path.Combine(SourceRoot, "extensions", "engloopkit"), extension, CompressionLevel.Optimal, includeBaseDirectory: false);
        return (nupkg, extension);
    }

    private void InstallDriver(string nupkg)
    {
        Directory.CreateDirectory(_driver);
        var manifest = Path.Combine(_driver, ".config", "dotnet-tools.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, "{\"version\":1,\"isRoot\":true,\"tools\":{}}");
        ClearToolCache();
        Run("dotnet", _driver, "tool", "install", "engloopkit", "--version", _version,
            "--add-source", Path.GetDirectoryName(nupkg)!, "--tool-manifest", manifest, "--no-cache");
    }

    private string[] InstallArgs(string root, string nupkg, string extension, string hostMode = "clean") =>
    [
        "install", "--mode", "overlay", "--host-mode", hostMode, "--root", root,
        "--product-id", "overlay-test", "--repository-id", "overlay-test-repository",
        "--tool-version", _version, "--tool-nupkg", nupkg, "--extension-archive", extension,
    ];

    private void Clone(string destination)
    {
        Run("git", _work, "clone", "--branch", "main", _bare, destination);
        ConfigureGit(destination);
    }

    private static void ConfigureGit(string root)
    {
        Run("git", root, "config", "user.email", "overlay@example.invalid");
        Run("git", root, "config", "user.name", "Overlay Test");
    }

    private void ClearToolCache()
    {
        var global = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var path = Path.Combine(global, "engloopkit", _version.ToLowerInvariant());
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private static ProcessResult Run(string file, string root, params string[] arguments) => Run(file, root, arguments, throwOnFailure: true);

    private static ProcessResult Run(string file, string root, string[] arguments, bool throwOnFailure)
    {
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        var result = new ProcessResult(process.ExitCode, stdout, stderr);
        if (throwOnFailure && result.ExitCode != 0)
        {
            throw new Xunit.Sdk.XunitException("Process failed: " + file + "\n" + stdout + "\n" + stderr);
        }
        return result;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bundle.yml"))) directory = directory.Parent;
        Assert.True(directory is not null, "could not locate repository root");
        return directory!.FullName;
    }

    private static void AssertManifestsMatch(string source, string target)
    {
        using var sourceJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(source, ".engloop-overlay", "manifest.json")));
        using var targetJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, ".engloop-overlay", "manifest.json")));
        var sourceFiles = sourceJson.RootElement.GetProperty("Files").EnumerateArray().Select(file => (file.GetProperty("RelativePath").GetString(), file.GetProperty("Sha256").GetString())).OrderBy(value => value.Item1).ToArray();
        var targetFiles = targetJson.RootElement.GetProperty("Files").EnumerateArray().Select(file => (file.GetProperty("RelativePath").GetString(), file.GetProperty("Sha256").GetString())).OrderBy(value => value.Item1).ToArray();
        Assert.Equal(sourceFiles, targetFiles);
    }

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            try { Directory.Delete(_work, recursive: true); } catch { /* transient tool processes may hold a disposable test file */ }
        }
        ClearToolCache();
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
