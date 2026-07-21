using System.Diagnostics;
using EngLoopKit.Components.Overlay;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>Focused, fast coverage of overlay verification/packing guards in a disposable Git root.</summary>
public sealed class OverlayCommandCoverageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "elk-overlay-coverage-" + Guid.NewGuid().ToString("N"));
    private readonly string _output = Path.Combine(Path.GetTempPath(), "elk-overlay-output-" + Guid.NewGuid().ToString("N") + ".zip");

    public OverlayCommandCoverageTests()
    {
        Directory.CreateDirectory(_root);
        Run("git", _root, "init");
        Run("git", _root, "config", "user.email", "overlay@example.invalid");
        Run("git", _root, "config", "user.name", "Overlay Coverage");
        File.WriteAllText(Path.Combine(_root, "README.md"), "# root");
        Run("git", _root, "add", "README.md");
        Run("git", _root, "commit", "-m", "initial");
    }

    [Fact]
    public void VerifyStatusAndPack_succeedForIgnoredUntrackedManifest()
    {
        CreateOverlayState();
        Assert.Equal(0, OverlayCommands.Execute(["status", "--root", _root]));
        Assert.Equal(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "all"]));
        Assert.Equal(0, OverlayCommands.Execute(["pack", "--root", _root, "--output", _output]));
        Assert.True(File.Exists(_output));
    }

    [Fact]
    public void Verify_rejectsInvalidModeUnignoredStagedAndHistoryManagedPaths()
    {
        CreateOverlayState();
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "invalid"]));
        Assert.Equal("overlay-invalid-verify-mode", OverlayCommands.LastError);

        var exclude = Path.Combine(_root, ".git", "info", "exclude");
        File.WriteAllText(exclude, string.Empty);
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root]));
        Assert.Contains("overlay-managed-root-not-ignored", OverlayCommands.LastError);

        WriteExcludes();
        Run("git", _root, "add", "-f", "overlay-local/state.txt");
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "staged"]));
        Assert.Contains("overlay-managed-path-staged", OverlayCommands.LastError);
        Run("git", _root, "reset", "--", "overlay-local/state.txt");

        Run("git", _root, "add", "-f", "overlay-local/state.txt");
        Run("git", _root, "commit", "--no-verify", "-m", "deliberate history leak");
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "push"]));
        Assert.Contains("overlay-managed-path-in-history", OverlayCommands.LastError);
    }

    [Fact]
    public void Verify_rejectsManifestDriftAndMissingBaseline()
    {
        CreateOverlayState();
        File.WriteAllText(Path.Combine(_root, "overlay-local", "state.txt"), "changed local state");
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root]));
        Assert.Equal("overlay-manifest-file-mismatch", OverlayCommands.LastError);

        CreateOverlayState();
        var manifestPath = Path.Combine(_root, ".engloop-overlay", "manifest.json");
        var manifest = OverlayArchive.ParseManifest(File.ReadAllText(manifestPath)) with { BaseRevision = "0000000000000000000000000000000000000000" };
        File.WriteAllText(manifestPath, OverlayArchive.SerializeManifest(manifest));
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "push"]));
        Assert.Equal("overlay-base-revision-not-found", OverlayCommands.LastError);
    }

    [Fact]
    public void Pack_refreshesLegitimateOverlayStateWhileStagedHookProtectsOnlyGitLeakage()
    {
        CreateOverlayState();
        File.WriteAllText(Path.Combine(_root, "overlay-local", "state.txt"), "legitimate local ELK update");

        // The normal pre-commit/push protection does not require immutable content;
        // it checks that managed paths are not staged or pushed.
        File.WriteAllText(Path.Combine(_root, "unrelated.txt"), "ordinary product work");
        Run("git", _root, "add", "unrelated.txt");
        Assert.Equal(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "staged"]));

        Assert.Equal(0, OverlayCommands.Execute(["pack", "--root", _root, "--output", _output]));
        Assert.Equal(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "all"]));
        Assert.Equal(string.Empty, Run("git", _root, "diff", "--cached", "--name-only", "--", "overlay-local").StandardOutput.Trim());
    }

    [Fact]
    public void Verify_acceptsUnrelatedStagedAndHistoryChanges_butRejectsTrackedManagedRoot()
    {
        CreateOverlayState();
        File.WriteAllText(Path.Combine(_root, "unrelated.txt"), "unrelated");
        Run("git", _root, "add", "unrelated.txt");
        Assert.Equal(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "staged"]));
        Run("git", _root, "commit", "-m", "unrelated change");
        Assert.Equal(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "push"]));

        Run("git", _root, "add", "-f", "overlay-local/state.txt");
        Run("git", _root, "commit", "--no-verify", "-m", "tracked overlay path");
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "staged"]));
        Assert.Contains("overlay-managed-path-tracked", OverlayCommands.LastError);

        // Push mode sees the more specific post-baseline history leak.
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "push"]));
        Assert.Contains("overlay-managed-path-in-history", OverlayCommands.LastError);
    }

    [Fact]
    public void Pack_rejectsOutputInsideRepositoryAndExistingOutput()
    {
        CreateOverlayState();
        Assert.NotEqual(0, OverlayCommands.Execute(["pack", "--root", _root, "--output", Path.Combine(_root, "overlay.zip")]));
        Assert.Equal("overlay-pack-output-must-be-outside-repository", OverlayCommands.LastError);

        File.WriteAllText(_output, "already exists");
        Assert.NotEqual(0, OverlayCommands.Execute(["pack", "--root", _root, "--output", _output]));
        Assert.Contains("overlay-output-already-exists", OverlayCommands.LastError);
    }

    [Fact]
    public void Register_runtimeOutputs_reconcilesManifestAndExcludes_thenBlocksStagedAndHistoryLeakage()
    {
        CreateOverlayState();
        const string modelRoot = "runtime-model/Foo.Model";
        const string generatedFile = "tests/Generated/Foo.g.cs";

        var registerExit = OverlayCommands.Execute([
            "register", "--root", _root,
            "--directory", "runtime-model\\Foo.Model",
            "--file", generatedFile,
        ]);
        Assert.True(registerExit == 0, OverlayCommands.LastError);

        var manifestPath = Path.Combine(_root, ".engloop-overlay", "manifest.json");
        var manifest = OverlayArchive.ParseManifest(File.ReadAllText(manifestPath));
        Assert.Contains(modelRoot, manifest.ManagedRoots, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(generatedFile, manifest.ManagedRoots, StringComparer.OrdinalIgnoreCase);
        var excludes = File.ReadAllText(Path.Combine(_root, ".git", "info", "exclude"));
        Assert.Contains("/runtime-model/Foo.Model/", excludes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/tests/Generated/Foo.g.cs", excludes, StringComparison.OrdinalIgnoreCase);

        Directory.CreateDirectory(Path.Combine(_root, "runtime-model", "Foo.Model"));
        File.WriteAllText(Path.Combine(_root, "runtime-model", "Foo.Model", "Foo.Model.csproj"), "<Project />");
        Directory.CreateDirectory(Path.Combine(_root, "tests", "Generated"));
        File.WriteAllText(Path.Combine(_root, "tests", "Generated", "Foo.g.cs"), "// generated");
        Assert.Equal(0, Run("git", _root, "check-ignore", "-q", "--no-index", "--", modelRoot).ExitCode);
        Assert.Equal(0, Run("git", _root, "check-ignore", "-q", "--no-index", "--", generatedFile).ExitCode);

        Run("git", _root, "add", "-f", "runtime-model/Foo.Model/Foo.Model.csproj");
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "staged"]));
        Assert.Contains("runtime-model/Foo.Model/Foo.Model.csproj", OverlayCommands.LastError, StringComparison.OrdinalIgnoreCase);
        Run("git", _root, "reset", "--", "runtime-model/Foo.Model/Foo.Model.csproj");

        Run("git", _root, "add", "-f", generatedFile);
        Run("git", _root, "commit", "--no-verify", "-m", "deliberate registered output leak");
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "push"]));
        Assert.Contains(generatedFile, OverlayCommands.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Register_isCaseInsensitiveTransactional_andDoesNotBlockUnregisteredProductSource()
    {
        CreateOverlayState();
        var manifestPath = Path.Combine(_root, ".engloop-overlay", "manifest.json");

        var registerExit = OverlayCommands.Execute([
            "register", "--root", _root,
            "--directory", "Runtime-Output\\Model",
            "--directory", "runtime-output/model",
        ]);
        Assert.True(registerExit == 0, OverlayCommands.LastError);
        var manifest = OverlayArchive.ParseManifest(File.ReadAllText(manifestPath));
        Assert.Single(manifest.ManagedRoots, path => string.Equals(path, "runtime-output/model", StringComparison.OrdinalIgnoreCase));

        File.WriteAllText(Path.Combine(_root, "ordinary-product.cs"), "// product source");
        Run("git", _root, "add", "ordinary-product.cs");
        Assert.Equal(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "staged"]));

        var beforeManifest = File.ReadAllText(manifestPath);
        var beforeExclude = File.ReadAllText(Path.Combine(_root, ".git", "info", "exclude"));
        Assert.NotEqual(0, OverlayCommands.Execute(["register", "--root", _root, "--directory", ".git/private"]));
        Assert.Equal("overlay-register-git-path-forbidden", OverlayCommands.LastError);
        Assert.Equal(beforeManifest, File.ReadAllText(manifestPath));
        Assert.Equal(beforeExclude, File.ReadAllText(Path.Combine(_root, ".git", "info", "exclude")));

        Run("git", _root, "commit", "-m", "ordinary product source");
        Directory.CreateDirectory(Path.Combine(_root, "already-tracked-output"));
        File.WriteAllText(Path.Combine(_root, "already-tracked-output", "tracked.txt"), "tracked");
        Run("git", _root, "add", "already-tracked-output/tracked.txt");
        Run("git", _root, "commit", "-m", "existing tracked output");
        Assert.NotEqual(0, OverlayCommands.Execute(["register", "--root", _root, "--directory", "already-tracked-output"]));
        Assert.Contains("overlay-register-path-tracked", OverlayCommands.LastError);
        Assert.Equal(beforeManifest, File.ReadAllText(manifestPath));
        Assert.Equal(beforeExclude, File.ReadAllText(Path.Combine(_root, ".git", "info", "exclude")));

        Directory.CreateDirectory(Path.Combine(_root, "historical-output"));
        File.WriteAllText(Path.Combine(_root, "historical-output", "old.txt"), "old");
        Run("git", _root, "add", "historical-output/old.txt");
        Run("git", _root, "commit", "-m", "historical output added");
        Run("git", _root, "rm", "historical-output/old.txt");
        Run("git", _root, "commit", "-m", "historical output removed");
        Assert.NotEqual(0, OverlayCommands.Execute(["register", "--root", _root, "--directory", "historical-output"]));
        Assert.Contains("overlay-register-path-in-history", OverlayCommands.LastError);
    }

    [Fact]
    public void Remove_deletesManifestOwnedAndRegisteredPaths_restoresPriorHook_andPreservesUnrelatedFiles()
    {
        CreateOverlayState();
        Assert.Equal(0, OverlayCommands.Execute(["register", "--root", _root, "--directory", "runtime-owned", "--file", "generated/Owned.g.cs"]));
        Directory.CreateDirectory(Path.Combine(_root, "runtime-owned"));
        File.WriteAllText(Path.Combine(_root, "runtime-owned", "model.csproj"), "<Project />");
        Directory.CreateDirectory(Path.Combine(_root, "generated"));
        File.WriteAllText(Path.Combine(_root, "generated", "Owned.g.cs"), "// generated");
        File.WriteAllText(Path.Combine(_root, "ordinary-product.cs"), "// preserve");

        var hooks = Path.Combine(_root, ".git", "hooks");
        var preCommit = Path.Combine(hooks, "pre-commit");
        var prePush = Path.Combine(hooks, "pre-push");
        File.WriteAllText(preCommit, "#!/bin/sh\n# ELK_OVERLAY_HOOK\n");
        File.WriteAllText(prePush, "#!/bin/sh\n# ELK_OVERLAY_HOOK\n");
        File.WriteAllText(prePush + ".elk-prior", "#!/bin/sh\necho prior\n");

        var manifest = OverlayArchive.ParseManifest(File.ReadAllText(Path.Combine(_root, ".engloop-overlay", "manifest.json")));
        Assert.NotEqual(0, OverlayCommands.Execute(["remove", "--root", _root, "--confirm", "wrong"]));
        Assert.Equal("overlay-remove-confirmation-mismatch", OverlayCommands.LastError);
        Assert.True(Directory.Exists(Path.Combine(_root, "runtime-owned")));

        var token = $"REMOVE-OVERLAY:{manifest.RepositoryId}@{manifest.BaseRevision}";
        Assert.Equal(0, OverlayCommands.Execute(["remove", "--root", _root, "--confirm", token]));
        Assert.False(Directory.Exists(Path.Combine(_root, "runtime-owned")));
        Assert.False(File.Exists(Path.Combine(_root, "generated", "Owned.g.cs")));
        Assert.False(Directory.Exists(Path.Combine(_root, "overlay-local")));
        Assert.False(Directory.Exists(Path.Combine(_root, ".engloop-overlay")));
        Assert.True(File.Exists(Path.Combine(_root, "ordinary-product.cs")));
        Assert.False(File.Exists(preCommit));
        Assert.Equal("#!/bin/sh\necho prior\n", File.ReadAllText(prePush));
        Assert.False(File.Exists(prePush + ".elk-prior"));
        Assert.DoesNotContain("ELK_OVERLAY_MANAGED", File.ReadAllText(Path.Combine(_root, ".git", "info", "exclude")));

        Assert.NotEqual(0, OverlayCommands.Execute(["remove", "--root", _root, "--confirm", token]));
        Assert.Contains("overlay-manifest-missing", OverlayCommands.LastError);
    }

    [Fact]
    public void Install_rejectsNonGitRootAndInvalidExtensionSources()
    {
        var nonGit = Path.Combine(Path.GetTempPath(), "elk-overlay-nongit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(nonGit);
        try
        {
            Assert.NotEqual(0, OverlayCommands.Execute(["install", "--mode", "overlay", "--root", nonGit, "--product-id", "ok", "--repository-id", "id", "--tool-version", "1.8.0", "--tool-nupkg", _output, "--extension-archive", _output]));
            Assert.NotNull(OverlayCommands.LastError);
        }
        finally
        {
            try { Directory.Delete(nonGit, recursive: true); } catch { }
        }
    }

    private void CreateOverlayState()
    {
        var managed = Path.Combine(_root, "overlay-local");
        Directory.CreateDirectory(managed);
        File.WriteAllText(Path.Combine(managed, "state.txt"), "local state");
        File.WriteAllText(Path.Combine(managed, "tool.nupkg"), "local tool package");
        Directory.CreateDirectory(Path.Combine(_root, ".config"));
        File.WriteAllText(Path.Combine(_root, ".config", "dotnet-tools.json"), "{\"version\":1,\"isRoot\":true,\"tools\":{}}");
        WriteExcludes();
        Directory.CreateDirectory(Path.Combine(_root, ".engloop-overlay"));
        var files = OverlayArchive.CaptureStableFiles(_root, ["overlay-local", ".config", ".engloop-overlay"], [OverlayManifest.ManagedManifestPath]);
        var manifest = new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion,
            "overlay-test",
            "overlay-repository",
            null,
            Run("git", _root, "rev-parse", "HEAD").StandardOutput.Trim(),
            DateTimeOffset.UtcNow,
            ["overlay-local", ".config", ".engloop-overlay"],
            ["/overlay-local/", "/.config/", "/.engloop-overlay/"],
            ["pre-commit", "pre-push"],
            "1.8.0",
            "overlay-local/tool.nupkg",
            "extension#sha256=" + new string('a', 64),
            files);
        File.WriteAllText(Path.Combine(_root, ".engloop-overlay", "manifest.json"), OverlayArchive.SerializeManifest(manifest));
    }

    private void WriteExcludes()
    {
        var exclude = Path.Combine(_root, ".git", "info", "exclude");
        File.WriteAllText(exclude, "unrelated-pattern\n# >>> ELK_OVERLAY_MANAGED >>>\n/overlay-local/\n/.config/\n/.engloop-overlay/\n# <<< ELK_OVERLAY_MANAGED <<<\n");
    }

    private static ProcessResult Run(string file, string root, params string[] arguments)
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
        if (process.ExitCode != 0) throw new Xunit.Sdk.XunitException(stderr);
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        try { if (File.Exists(_output)) File.Delete(_output); } catch { }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
