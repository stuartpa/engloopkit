using System.Diagnostics;
using System.Text.Json.Nodes;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>Exercises the public validators against isolated source and installed layouts.</summary>
public sealed class InstalledCommandSurfaceTests : IDisposable
{
    private const string SourceCommands = "extensions/engloopkit/commands";
    private const string InstalledCommands = ".specify/extensions/engloop/commands";
    private const string FirstId = "speckit.engloop.01-northstar";
    private static readonly string SourceRoot = FindRepoRoot();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "engloopkit-installed-" + Guid.NewGuid().ToString("N"));

    public InstalledCommandSurfaceTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(SourceCommands)]
    [InlineData(InstalledCommands)]
    public void CompleteLayout_acceptsBothValidatorsWithExactly29CommandsAgentsAndPrompts(string commandRoot)
    {
        CopySurface(commandRoot);
        Assert.Equal(29, Directory.GetFiles(FullPath(commandRoot), "*.md").Length);
        Assert.Equal(29, Directory.GetFiles(FullPath(".github/agents"), "*.agent.md").Length);
        Assert.Equal(29, Directory.GetFiles(FullPath(".github/prompts"), "*.prompt.md").Length);
        Assert.False(Directory.Exists(FullPath(commandRoot == SourceCommands ? InstalledCommands : SourceCommands)));
        AssertBothPass();
    }

    [Theory]
    [InlineData(SourceCommands)]
    [InlineData(InstalledCommands)]
    public void PartialLayout_reportsOneBoundedMissingCommandWithoutThrowing(string commandRoot)
    {
        CopySurface(commandRoot);
        File.Delete(FullPath(commandRoot + "/" + FirstId + ".md"));
        foreach (var result in Both())
        {
            AssertFailure(result, "missing-command:" + FirstId);
            Assert.Contains(commandRoot, result.Error.Replace('\\', '/'), StringComparison.Ordinal);
            Assert.Single(result.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
    }

    [Fact]
    public void NeitherLayout_reportsBothActionableRootsEvenWithoutAgentDirectories()
    {
        foreach (var result in Both())
        {
            AssertFailure(result, "missing-command-directory");
            Assert.Contains(SourceCommands, result.Error, StringComparison.Ordinal);
            Assert.Contains(InstalledCommands, result.Error, StringComparison.Ordinal);
            Assert.Contains("--command-root", result.Error, StringComparison.Ordinal);
            Assert.Contains(_root, result.Error, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLayouts_rejectAmbiguityEvenWhenOneIsIncomplete(bool incomplete)
    {
        CopySurface(SourceCommands);
        CopyCommands(InstalledCommands);
        if (incomplete) File.Delete(FullPath(InstalledCommands + "/" + FirstId + ".md"));
        foreach (var result in Both())
        {
            AssertFailure(result, "ambiguous-command-directory");
            Assert.Contains(SourceCommands, result.Error, StringComparison.Ordinal);
            Assert.Contains(InstalledCommands, result.Error, StringComparison.Ordinal);
            Assert.Contains("--command-root", result.Error, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(SourceCommands)]
    [InlineData(InstalledCommands)]
    [InlineData("custom commands")]
    [InlineData("./.specify/extensions/engloop/commands")]
    [InlineData(".specify\\extensions\\engloop\\commands")]
    public void SafeExplicitOverride_selectsOnlyThatDirectory(string commandRoot)
    {
        CopySurface(SourceCommands);
        CopyCommands(InstalledCommands);
        CopyCommands(commandRoot);
        AssertBothPass("--command-root", commandRoot);

        File.Delete(FullPath(commandRoot + "/" + FirstId + ".md"));
        foreach (var result in Both("--command-root", commandRoot))
            AssertFailure(result, "missing-command:" + FirstId);
    }

    [Fact]
    public void MissingExplicitDirectory_doesNotSubstituteCompleteDefaultLayout()
    {
        CopySurface(SourceCommands);
        CopyCommands(InstalledCommands);
        foreach (var result in Both("--command-root", "chosen/commands"))
        {
            AssertFailure(result, "missing-command-directory");
            Assert.Contains("chosen/commands", result.Error.Replace('\\', '/'), StringComparison.Ordinal);
            Assert.Contains("no alternate command root", result.Error, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("commands/../../outside")]
    [InlineData("commands/../other")]
    [InlineData("/absolute")]
    [InlineData("C:\\absolute")]
    [InlineData("C:relative")]
    [InlineData("\\\\server\\share")]
    [InlineData("commands:stream")]
    [InlineData("commands/*")]
    [InlineData("commands\0")]
    [InlineData("commands\n")]
    [InlineData("commands.")]
    [InlineData("commands ")]
    [InlineData("commands//child")]
    [InlineData("--root")]
    public void UnsafeExplicitOverride_rejectsRatherThanUsingInstalledLayout(string commandRoot)
    {
        CopySurface(InstalledCommands);
        foreach (var result in Both("--command-root", commandRoot))
            AssertFailure(result, "invalid-command-root");
    }

    [Fact]
    public void AbsolutePathInsideRepository_isStillNotARepoRelativeOverride()
    {
        CopySurface(InstalledCommands);
        foreach (var result in Both("--command-root", FullPath(InstalledCommands)))
            AssertFailure(result, "invalid-command-root");
    }

    [Fact]
    public void MissingOrRepeatedOverrideValue_failsClosed()
    {
        CopySurface(InstalledCommands);
        foreach (var result in Both("--command-root"))
            AssertFailure(result, "invalid-command-root");
        foreach (var result in Both("--command-root", InstalledCommands, "--command-root", SourceCommands))
            AssertFailure(result, "invalid-command-root");
    }

    [Theory]
    [InlineData("\0")]
    [InlineData("")]
    public void InvalidRepositoryPath_returnsActionableFailureWithoutThrowing(string root)
    {
        foreach (var validator in Validators)
        {
            var result = Capture(() => validator(["--root", root]));
            AssertFailure(result, "command-surface-path-error");
            Assert.Contains("--root", result.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MissingRootOptionValue_returnsActionableFailure()
    {
        foreach (var validator in Validators)
            AssertFailure(Capture(() => validator(["--root"])), "missing-option:--root");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileInPlaceOfCommandDirectoryOrAncestor_doesNotFallBack(bool ancestor)
    {
        CopySurface(InstalledCommands);
        var file = FullPath(ancestor ? "extensions" : SourceCommands);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "not a directory");
        foreach (var result in Both()) AssertFailure(result, "not-a-directory");
        foreach (var result in Both("--command-root", SourceCommands)) AssertFailure(result, "not-a-directory");
        AssertBothPass("--command-root", InstalledCommands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinkedCommandDirectoryOrAncestor_rejectsEvenIfTargetIsComplete(bool ancestor)
    {
        CopySurface(InstalledCommands);
        var link = FullPath(ancestor ? "linked" : SourceCommands);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        var target = ancestor ? _root : FullPath(InstalledCommands);
        if (OperatingSystem.IsWindows())
        {
            // Junctions exercise Windows reparse points without requiring symlink elevation.
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, output + error);
        }
        else Directory.CreateSymbolicLink(link, target);
        try
        {
            var selected = ancestor ? "linked/" + InstalledCommands : SourceCommands;
            foreach (var result in Both("--command-root", selected))
                AssertFailure(result, "reparse-point-forbidden");
            if (!ancestor)
                foreach (var result in Both()) AssertFailure(result, "reparse-point-forbidden");
        }
        finally { Directory.Delete(link); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExtraOrReplacedCommandId_isRejectedEvenWith29Files(bool replace)
    {
        CopySurface(InstalledCommands);
        if (replace) File.Delete(FullPath(InstalledCommands + "/" + FirstId + ".md"));
        File.WriteAllText(FullPath(InstalledCommands + "/speckit.engloop.99-extra.md"), "extra");
        foreach (var result in Both())
        {
            AssertFailure(result, "extra-command:speckit.engloop.99-extra");
            if (replace) Assert.Contains("missing-command:" + FirstId, result.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void InstalledLegacyCommandSurface_isRejectedByBothValidators()
    {
        CopySurface(InstalledCommands);
        File.WriteAllText(FullPath(InstalledCommands + "/speckit.engloopkit.legacy.md"), "legacy");
        foreach (var result in Both()) AssertFailure(result, "legacy-command-surface-present");
    }

    [Theory]
    [InlineData("no frontmatter")]
    [InlineData("---\ntools: [unterminated\n---\n")]
    public void MalformedCommandFrontmatter_preservesBoundedDiagnostic(string text)
    {
        CopySurface(InstalledCommands);
        File.WriteAllText(FullPath(InstalledCommands + "/" + FirstId + ".md"), text);
        foreach (var result in Both()) AssertFailure(result, "missing-frontmatter:" + FirstId);
    }

    [Theory]
    [InlineData("agents", "agent", "missing-agent-frontmatter:")]
    [InlineData("prompts", "prompt", "missing-prompt-frontmatter:")]
    public void MalformedInstalledAgentOrPrompt_isRejectedWithoutThrowing(string folder, string suffix, string token)
    {
        CopySurface(InstalledCommands);
        File.WriteAllText(FullPath($".github/{folder}/{FirstId}.{suffix}.md"), "---\ntools: [unterminated\n---\n");
        AssertFailure(Capture(() => ValidationCommands.ValidateAgentSurfaces(["--root", _root])), token + FirstId);
    }

    [Theory]
    [InlineData("agents", "agent", "wrong-agent-count", "missing-agent:")]
    [InlineData("prompts", "prompt", "wrong-prompt-count", "missing-prompt:")]
    public void InstalledAgentAndPromptIds_remainStrict(string folder, string suffix, string countToken, string missingToken)
    {
        CopySurface(InstalledCommands);
        File.Delete(FullPath($".github/{folder}/{FirstId}.{suffix}.md"));
        AssertFailure(Capture(() => ValidationCommands.ValidateAgentSurfaces(["--root", _root])), countToken);
        File.WriteAllText(FullPath($".github/{folder}/speckit.engloop.99-extra.{suffix}.md"), "extra");
        AssertFailure(Capture(() => ValidationCommands.ValidateAgentSurfaces(["--root", _root])), missingToken + FirstId);
    }

    [Fact]
    public void LockedCommand_returnsBoundedFilePathDiagnosticWithoutThrowing()
    {
        CopySurface(InstalledCommands);
        var path = FullPath(InstalledCommands + "/" + FirstId + ".md");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        foreach (var result in Both())
        {
            AssertFailure(result, "surface-file-error:");
            Assert.Contains(FirstId + ".md", result.Error, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("speckit.engloop.20-incident")]
    [InlineData("speckit.engloop.21-postmortem")]
    [InlineData("speckit.engloop.22-repair")]
    public void FocusedEntry_acceptsValidConfigWithoutReadinessRunwayOrCommandSurfaces(string stage)
    {
        CreateEntryRoot();
        Assert.False(File.Exists(FullPath(".engloop/readiness/current.json")));
        Assert.False(Directory.Exists(FullPath(SourceCommands)));
        Assert.False(Directory.Exists(FullPath(InstalledCommands)));
        var result = Capture(() => ValidationCommands.ValidateAgentEntry(["--root", _root, "--stage", stage]));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Contains("AGENT_ENTRY_OK", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage10_stillRequiresReadinessWhileStage20DoesNot()
    {
        CreateEntryRoot();
        var review = Capture(() => ValidationCommands.ValidateAgentEntry(["--root", _root, "--stage", "speckit.engloop.10-codereview-prepare"]));
        Assert.Equal(2, review.ExitCode);
        Assert.Contains("missing-current-readiness", review.Error, StringComparison.Ordinal);
        Assert.Equal(0, ValidationCommands.ValidateAgentEntry(["--root", _root, "--stage", "speckit.engloop.20-incident"]));
    }

    [Theory]
    [InlineData("speckit.engloop.20-incident")]
    [InlineData("speckit.engloop.21-postmortem")]
    [InlineData("speckit.engloop.22-repair")]
    public void FocusedEntry_stillRejectsMissingRootMissingConfigUnsafeConfigAndMalformedJson(string stage)
    {
        void Reject(string token)
        {
            var result = Capture(() => ValidationCommands.ValidateAgentEntry(["--root", _root, "--stage", stage]));
            Assert.Equal(2, result.ExitCode);
            Assert.Contains(token, result.Error, StringComparison.Ordinal);
        }
        Reject("missing-process-root");
        CreateEntryRoot();
        var path = FullPath(".engloop/config.json");
        var config = File.ReadAllText(path);
        File.Delete(path);
        Reject("missing-config");
        var unsafeConfig = JsonNode.Parse(config)!.AsObject();
        unsafeConfig["artifactRoot"] = "../outside";
        File.WriteAllText(path, unsafeConfig.ToJsonString());
        Reject("invalid-artifact-root");
        File.WriteAllText(path, "{");
        var malformed = Capture(() => ValidationCommands.ValidateAgentEntry(["--root", _root, "--stage", stage]));
        Assert.Equal(2, malformed.ExitCode);
        Assert.NotEmpty(malformed.Error);
        File.WriteAllText(path, config);
        Directory.CreateDirectory(FullPath(".engloopkit"));
        Reject("forbidden-root-present");
    }

    private static readonly Func<string[], int>[] Validators = [ValidationCommands.ValidateCommands, ValidationCommands.ValidateAgentSurfaces];

    private (int ExitCode, string Output, string Error)[] Both(params string[] options)
        => Validators.Select(validator => Capture(() => validator(["--root", _root, .. options]))).ToArray();

    private void AssertBothPass(params string[] options)
    {
        var results = Both(options);
        foreach (var result in results)
        {
            Assert.True(result.ExitCode == 0, result.Error);
            Assert.Equal(string.Empty, result.Error);
        }
        Assert.Contains("COMMANDS_OK", results[0].Output, StringComparison.Ordinal);
        Assert.Contains("AGENT_SURFACES_OK", results[1].Output, StringComparison.Ordinal);
    }

    private static void AssertFailure((int ExitCode, string Output, string Error) result, string token)
    {
        Assert.Equal(1, result.ExitCode);
        Assert.Contains(token, result.Error, StringComparison.Ordinal);
        Assert.InRange(result.Error.Length, 1, 1800);
        Assert.Equal(string.Empty, result.Output);
        Assert.DoesNotContain(" at EngLoopKit.", result.Error, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output, string Error) Capture(Func<int> action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            return (action(), output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private string FullPath(string relative) => Path.Combine(_root, relative.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar));

    private void CopySurface(string commandRoot)
    {
        CopyCommands(commandRoot);
        CopyFiles(Path.Combine(SourceRoot, ".github", "agents"), FullPath(".github/agents"), "speckit.engloop.*.agent.md");
        CopyFiles(Path.Combine(SourceRoot, ".github", "prompts"), FullPath(".github/prompts"), "speckit.engloop.*.prompt.md");
    }

    private void CopyCommands(string commandRoot)
        => CopyFiles(Path.Combine(SourceRoot, SourceCommands.Replace('/', Path.DirectorySeparatorChar)), FullPath(commandRoot), "speckit.engloop.*.md");

    private static void CopyFiles(string source, string target, string pattern)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source, pattern)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
    }

    private void CreateEntryRoot()
    {
        Directory.CreateDirectory(FullPath(".engloop"));
        File.WriteAllText(FullPath("NORTHSTAR.md"), "# Fixture direction\n");
        File.WriteAllText(FullPath("LEARNINGS.md"), "# Fixture learnings\n");
        var config = JsonNode.Parse(File.ReadAllText(Path.Combine(SourceRoot, ".engloop", "config.json")))!.AsObject();
        config["testRunway"] = new JsonObject { ["status"] = "unproven" };
        File.WriteAllText(FullPath(".engloop/config.json"), config.ToJsonString());
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bundle.yml"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}