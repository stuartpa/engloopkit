using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class TokenEfficiencyToolchainTests : IDisposable
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string Script = Path.Combine(Root, "extensions", "engloopkit", "scripts", "Resolve-DeclaredToolchain.ps1");
    private readonly string _work = Path.Combine(Path.GetTempPath(), "elk-token-toolchain-" + Guid.NewGuid().ToString("N"));

    public TokenEfficiencyToolchainTests() => Directory.CreateDirectory(_work);

    [Fact]
    public void PnpmDeclaration_usesCorepackPnpm_whenDirectPnpmIsAbsent()
    {
        var repo = CreatePnpmRepository();
        var bin = Path.Combine(_work, "bin-ready");
        Directory.CreateDirectory(bin);
        WriteCommand(bin, "node", "if \"%1\"==\"--version\" echo v22.0.0\r\nexit /b 0\r\n");
        WriteCommand(bin, "corepack", "if \"%1\"==\"--version\" echo 0.31.0\r\nif \"%1\"==\"pnpm\" if \"%2\"==\"--version\" echo 9.15.0\r\nexit /b 0\r\n");

        var result = Run(repo, bin);

        Assert.Equal(0, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal("ready", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("corepack-pnpm-available", json.RootElement.GetProperty("reason").GetString());
        Assert.Equal(["corepack", "pnpm"], json.RootElement.GetProperty("invocation").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.False(File.Exists(Path.Combine(repo, "package-lock.json")));
    }

    [Fact]
    public void CorepackPnpmFailure_blocks_withoutNpmFallbackOrLockfileMutation()
    {
        var repo = CreatePnpmRepository();
        var bin = Path.Combine(_work, "bin-blocked");
        Directory.CreateDirectory(bin);
        WriteCommand(bin, "node", "if \"%1\"==\"--version\" echo v22.0.0\r\nexit /b 0\r\n");
        WriteCommand(bin, "corepack", "if \"%1\"==\"--version\" echo 0.31.0 & exit /b 0\r\necho Signature verification failed 1>&2\r\nexit /b 1\r\n");
        WriteCommand(bin, "npm", "echo npm-must-not-run>\"%TEMP%\\elk-npm-fallback.txt\"\r\nexit /b 0\r\n");
        var marker = Path.Combine(Path.GetTempPath(), "elk-npm-fallback.txt");
        File.Delete(marker);

        var result = Run(repo, bin);

        Assert.Equal(2, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal("blocked", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("corepack-pnpm-unavailable", json.RootElement.GetProperty("reason").GetString());
        Assert.Empty(json.RootElement.GetProperty("invocation").EnumerateArray());
        Assert.False(json.RootElement.GetProperty("evidence").GetProperty("integrityVerificationBypassAllowed").GetBoolean());
        Assert.False(File.Exists(marker));
        Assert.False(File.Exists(Path.Combine(repo, "package-lock.json")));
        Assert.True(File.Exists(Path.Combine(repo, "pnpm-lock.yaml")));
    }

    private string CreatePnpmRepository()
    {
        var repo = Path.Combine(_work, "repo-" + Guid.NewGuid().ToString("N"));
        var package = Path.Combine(repo, "packages", "ui");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(repo, "package.json"), "{\"private\":true,\"packageManager\":\"pnpm@9\"}");
        File.WriteAllText(Path.Combine(repo, "pnpm-lock.yaml"), "lockfileVersion: '9.0'\n");
        File.WriteAllText(Path.Combine(package, "package.json"), "{\"private\":true}");
        return repo;
    }

    private static void WriteCommand(string directory, string name, string body)
        => File.WriteAllText(Path.Combine(directory, name + ".cmd"), "@echo off\r\n" + body);

    private static (int ExitCode, string Output, string Error) Run(string repo, string bin)
    {
        var package = Path.Combine(repo, "packages", "ui");
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(Script);
        start.ArgumentList.Add("-RepositoryRoot");
        start.ArgumentList.Add(repo);
        start.ArgumentList.Add("-PackageDirectory");
        start.ArgumentList.Add(package);
        start.Environment["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bundle.yml")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return directory!.FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true);
    }
}
