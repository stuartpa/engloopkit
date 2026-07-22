using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using EngLoopKit.Components.Overlay;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>
/// Direct deterministic tests for private overlay transaction helpers. These isolate
/// archive/source/exclude/hook safety logic without a UI or a consumer repository.
/// </summary>
public sealed class OverlayCommandPrivateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "elk-overlay-private-" + Guid.NewGuid().ToString("N"));

    public OverlayCommandPrivateTests()
    {
        Directory.CreateDirectory(_root);
        RunGit("init");
        RunGit("config", "user.email", "overlay@example.invalid");
        RunGit("config", "user.name", "Overlay Private Test");
        File.WriteAllText(Path.Combine(_root, "README.md"), "# root");
        RunGit("add", "README.md");
        RunGit("commit", "-m", "initial");
    }

    [Fact]
    public void ExtensionMaterialization_acceptsExplicitLocalArchive()
    {
        var input = Path.Combine(_root, "input.zip");
        File.WriteAllText(input, "archive");
        var copied = Invoke<string>("MaterializeExtensionArchive", _root, input);
        Assert.Equal(Path.Combine(_root, ".engloop-overlay", "cache", "extension.zip"), copied);
        Assert.Equal("archive", File.ReadAllText(copied));
    }

    [Fact]
    public void ExtensionExtraction_handlesDirectoryEntriesAndRequiresExactlyOneManifest()
    {
        var archive = Path.Combine(_root, "extension.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            zip.CreateEntry("folder/");
            WriteZipEntry(zip, "extension.yml", "schema_version: \"1.0\"");
            WriteZipEntry(zip, "README.md", "# extension");
        }
        var extracted = Invoke<string>("ExtractExtensionSource", _root, archive);
        Assert.Equal(Path.Combine(_root, ".engloop-overlay", "cache", "extension-source"), extracted);
        Assert.True(File.Exists(Path.Combine(extracted, "extension.yml")));

        var invalid = Path.Combine(_root, "invalid.zip");
        using (ZipFile.Open(invalid, ZipArchiveMode.Create)) { }
        Assert.Throws<InvalidOperationException>(() => Invoke<string>("ExtractExtensionSource", _root, invalid));
        Directory.Delete(Path.Combine(_root, ".engloop-overlay", "cache", "extension-source"), recursive: true);
        Assert.Throws<InvalidDataException>(() => Invoke<string>("ExtractExtensionSource", _root, invalid));

        var escaped = Path.Combine(_root, "escaped.zip");
        using (var zip = ZipFile.Open(escaped, ZipArchiveMode.Create))
        {
            WriteZipEntry(zip, "../escape.txt", "bad");
        }
        Directory.Delete(Path.Combine(_root, ".engloop-overlay", "cache", "extension-source"), recursive: true);
        Assert.Throws<InvalidDataException>(() => Invoke<string>("ExtractExtensionSource", _root, escaped));
    }

    [Fact]
    public void LocalExcludeAndOverlayHook_helpers_areIdempotentAndOwned()
    {
        var exclude = Path.Combine(_root, ".git", "info", "exclude");
        File.WriteAllText(exclude, "existing\n");
        Invoke<object?>("WriteOverlayExcludes", exclude, "clean");
        var once = File.ReadAllText(exclude);
        Invoke<object?>("WriteOverlayExcludes", exclude, "clean");
        Assert.Equal(once, File.ReadAllText(exclude));
        Assert.Contains("# >>> ELK_OVERLAY_MANAGED >>>", once);
        Assert.Contains("/.engloop/", once);

        Invoke<object?>("InstallHook", _root, "pre-commit", "staged", "clean");
        var hook = File.ReadAllText(Path.Combine(_root, ".git", "hooks", "pre-commit"));
        Assert.Contains("ELK_OVERLAY_HOOK", hook);
        Assert.Contains("--mode staged", hook);
    }

    [Fact]
    public void InitializationAndStableSurface_helpers_writeExplicitUnprovenOverlayState_andFailClosedWhenAbsent()
    {
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("WaitForGeneratedSurface", _root));

        Invoke<object?>("WriteInitialOverlayFiles", _root, "private-product");
        var config = File.ReadAllText(Path.Combine(_root, ".engloop", "config.json"));
        Assert.Contains("\"overlayMode\": true", config);
        Assert.Contains("\"productId\": \"private-product\"", config);
        Assert.Contains("\"status\": \"unproven\"", config);
        Assert.Contains("overlay-local draft", File.ReadAllText(Path.Combine(_root, "NORTHSTAR.md")));
    }

    [Fact]
    public void ManagedPathAndIdentity_helpers_areConservative()
    {
        var manifest = new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion, "product", "repository", null, "base", DateTimeOffset.UtcNow,
            [".engloop", "NORTHSTAR.md"], [], [], "1.8.0", "package.nupkg", "extension", []);
        Assert.True(OverlayArchive.IsManagedPath(manifest, ".engloop/config.json"));
        Assert.True(OverlayArchive.IsManagedPath(manifest, "NORTHSTAR.md"));
        Assert.False(OverlayArchive.IsManagedPath(manifest, "LEARNINGS.md"));
        var subdirectory = Path.Combine(_root, "sub");
        Directory.CreateDirectory(subdirectory);
        Assert.Throws<InvalidOperationException>(() => Invoke<string>("RequireGitRoot", subdirectory));
    }

    [Fact]
    public void GeneratedSurfaceAndManifest_helpers_stabilizeAndRoundTrip()
    {
        var agents = Path.Combine(_root, ".github", "agents");
        var prompts = Path.Combine(_root, ".github", "prompts");
        Directory.CreateDirectory(agents);
        Directory.CreateDirectory(prompts);
        var ids = new[]
        {
            "01-northstar", "02-scaffold", "03-architect", "04-refactor", "05-model",
            "06-explore", "07-validate", "08-unittest", "09-debugger-walk-thru", "10-codereview-prepare",
            "20-incident", "21-postmortem", "22-repair", "30-refactor-scan",
            "31-learnings-pyramid", "40-pomodoro-create", "50-overlay-pack", "51-overlay-remove",
        };
        foreach (var id in ids)
        {
            File.WriteAllText(Path.Combine(agents, $"speckit.engloop.{id}.agent.md"), "agent");
            File.WriteAllText(Path.Combine(prompts, $"speckit.engloop.{id}.prompt.md"), "prompt");
        }
        Invoke<object?>("WaitForGeneratedSurface", _root);

        Directory.CreateDirectory(Path.Combine(_root, ".engloop"));
        File.WriteAllText(Path.Combine(_root, ".engloop", "config.json"), "{}");
        var manifest = Invoke<OverlayManifest>("CreateCurrentManifest", _root, "clean", "private-product", "private-repository", "1.8.1", ".engloop-overlay/packages/tool.nupkg", "extension");
        Assert.Contains(manifest.Files, file => file.RelativePath == ".engloop/config.json");
        Invoke<object?>("WriteManifest", _root, manifest);
        var read = Invoke<OverlayManifest>("ReadManifest", _root);
        Assert.Equal(manifest.RepositoryId, read.RepositoryId);
        Assert.Equal(manifest.Files.Select(file => file.RelativePath), read.Files.Select(file => file.RelativePath));

        var gitInfo = Invoke<string>("GetGitPath", _root, "info/exclude");
        Assert.True(Path.IsPathRooted(gitInfo));
        Assert.Equal("fallback", Invoke<string>("GetOption", new[] { "--other", "x" }, "--wanted", "fallback"));
        Assert.Throws<FileNotFoundException>(() => Invoke<string>("RequireExistingFile", new[] { "--file", Path.Combine(_root, "missing") }, "--file"));
    }



    [Fact]
    public void SecretAndOptionHelpers_failClosed()
    {
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("RejectSecretLikePaths", (object)new[] { new OverlayFile(".env.local", 1, new string('a', 64)) }));
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("RejectSecretLikePaths", (object)new[] { new OverlayFile("credentials.json", 1, new string('a', 64)) }));
        Invoke<object?>("RejectSecretLikePaths", (object)new[] { new OverlayFile(".engloop/config.json", 1, new string('a', 64)) });

        Assert.Throws<InvalidOperationException>(() => Invoke<string>("RequireOption", (object)Array.Empty<string>(), "--required"));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>("NormalizeHostMode", "invalid"));
        Assert.Equal("coexist", Invoke<string>("NormalizeHostMode", "coexist"));
        Assert.Throws<InvalidOperationException>(() => Invoke<IReadOnlyList<string>>("GetOptions", new[] { "--file" }, "--file"));
        Assert.Equal(["one", "two"], Invoke<IReadOnlyList<string>>("GetOptions", new[] { "--file", "one", "--file", "two" }, "--file"));
    }

    [Fact]
    public void ExcludeAndSnapshotHelpers_failClosedAndRestoreExactState()
    {
        var exclude = Path.Combine(_root, ".git", "info", "exclude");
        File.WriteAllText(exclude, "# >>> ELK_OVERLAY_MANAGED >>>\nmissing end\n");
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("WriteManagedExcludeBlock", exclude, (object)new[] { "/runtime/" }));

        var shared = Path.Combine(_root, ".github", "agents");
        Directory.CreateDirectory(shared);
        File.WriteAllText(Path.Combine(shared, "existing.md"), "before");
        var snapshot = Invoke<object>("CaptureDirectorySnapshot", _root, ".github/agents");
        File.WriteAllText(Path.Combine(shared, "existing.md"), "changed");
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("AssertSnapshotPreserved", _root, snapshot));
        File.Delete(Path.Combine(shared, "existing.md"));
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("AssertSnapshotPreserved", _root, snapshot));
        File.WriteAllText(Path.Combine(shared, "existing.md"), "changed");
        File.WriteAllText(Path.Combine(shared, "new.md"), "new");
        Invoke<object?>("RemoveNewDirectoryFiles", _root, snapshot);
        Assert.False(File.Exists(Path.Combine(shared, "new.md")));
        Invoke<object?>("RestoreDirectorySnapshot", _root, snapshot);
        Assert.Equal("before", File.ReadAllText(Path.Combine(shared, "existing.md")));
    }

    [Fact]
    public void CoexistPreflightAndHookChain_failClosed_andHookSnapshotsRestore()
    {
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("PreflightInstall", _root, "coexist"));

        Directory.CreateDirectory(Path.Combine(_root, ".specify", "extensions"));
        File.WriteAllText(Path.Combine(_root, ".specify", "extensions", ".registry"), "tracked");
        RunGit("add", "-f", ".specify/extensions/.registry");
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("PreflightInstall", _root, "coexist"));
        RunGit("reset", "--", ".specify/extensions/.registry");

        var prePush = Path.Combine(_root, ".git", "hooks", "pre-push");
        File.WriteAllText(prePush, "prior hook");
        var snapshots = Invoke<object>("CaptureHookSnapshots", _root);
        File.WriteAllText(prePush + ".elk-prior", "chain collision");
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("InstallHook", _root, "pre-push", "push", "coexist"));

        File.WriteAllText(prePush, "changed");
        Invoke<object?>("RestoreHookSnapshots", _root, snapshots);
        Assert.Equal("prior hook", File.ReadAllText(prePush));
        Assert.False(File.Exists(prePush + ".elk-prior"));
    }

    [Fact]
    public void ProtectedState_rejectsMissingToolManifestAndPackage()
    {
        Directory.CreateDirectory(Path.Combine(_root, "managed"));
        var manifest = new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion, "product", "repository", null,
            RunGitOutput("rev-parse", "HEAD").Trim(), DateTimeOffset.UtcNow,
            ["managed"], ["/managed/"], [], "1.8.2", "managed/tool.nupkg", "extension", []);
        Assert.Throws<InvalidOperationException>(() => Invoke<object>("EnsureProtected", _root, manifest, "all"));

        Directory.CreateDirectory(Path.Combine(_root, ".config"));
        File.WriteAllText(Path.Combine(_root, ".config", "dotnet-tools.json"), "{}");
        var withManifest = manifest with
        {
            ManagedRoots = ["managed", ".config"],
            ExcludePatterns = ["/managed/", "/.config/"],
        };
        File.WriteAllText(Path.Combine(_root, ".git", "info", "exclude"), "/managed/\n/.config/\n");
        Assert.Throws<InvalidOperationException>(() => Invoke<object>("EnsureProtected", _root, withManifest, "all"));
    }

    [Fact]
    public void Rollback_removesOnlyOwnedPathsAndRestoresExcludeAndHooks()
    {
        var exclude = Path.Combine(_root, ".git", "info", "exclude");
        var original = "original-exclude\n";
        File.WriteAllText(exclude, original);
        Directory.CreateDirectory(Path.Combine(_root, ".engloop"));
        Directory.CreateDirectory(Path.Combine(_root, ".engloop-overlay"));
        Directory.CreateDirectory(Path.Combine(_root, ".specify"));
        Directory.CreateDirectory(Path.Combine(_root, ".github", "agents"));
        Directory.CreateDirectory(Path.Combine(_root, ".github", "prompts"));
        Directory.CreateDirectory(Path.Combine(_root, ".vscode"));
        Directory.CreateDirectory(Path.Combine(_root, ".config"));
        File.WriteAllText(Path.Combine(_root, "NORTHSTAR.md"), "local");
        File.WriteAllText(Path.Combine(_root, "LEARNINGS.md"), "local");
        File.WriteAllText(Path.Combine(_root, ".git", "hooks", "pre-commit"), "# ELK_OVERLAY_HOOK\n");
        File.WriteAllText(Path.Combine(_root, ".git", "hooks", "pre-push"), "# ELK_OVERLAY_HOOK\n");

        Invoke<object?>("RollbackInstall", _root, original, "clean");
        Assert.Equal(original, File.ReadAllText(exclude));
        Assert.False(Directory.Exists(Path.Combine(_root, ".engloop")));
        Assert.False(Directory.Exists(Path.Combine(_root, ".engloop-overlay")));
        Assert.False(File.Exists(Path.Combine(_root, "NORTHSTAR.md")));
        Assert.False(File.Exists(Path.Combine(_root, ".git", "hooks", "pre-commit")));
    }

    [Fact]
    public void RemovalHelpers_validatePlanExcludeHooksAndSharedHostState()
    {
        var exclude = Path.Combine(_root, ".git", "info", "exclude");
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("WriteExcludeWithoutManagedBlock", exclude, "unrelated\n"));
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("WriteExcludeWithoutManagedBlock", exclude, "# >>> ELK_OVERLAY_MANAGED >>>\nmissing-end\n"));
        Invoke<object?>("WriteExcludeWithoutManagedBlock", exclude, "before\n# >>> ELK_OVERLAY_MANAGED >>>\n/owned/\n# <<< ELK_OVERLAY_MANAGED <<<\nafter\n");
        Assert.Equal("before" + Environment.NewLine + "after" + Environment.NewLine, File.ReadAllText(exclude));

        var hooks = Path.Combine(_root, ".git", "hooks");
        File.WriteAllText(Path.Combine(hooks, "pre-commit"), "foreign");
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("PreflightRemovalHooks", _root, (object)new[] { "pre-commit" }));
        File.WriteAllText(Path.Combine(hooks, "pre-commit"), "# ELK_OVERLAY_HOOK\n");
        Invoke<object?>("PreflightRemovalHooks", _root, (object)new[] { "pre-commit" });

        Directory.CreateDirectory(Path.Combine(_root, "owned", "child"));
        File.WriteAllText(Path.Combine(_root, "owned", "child", "x.txt"), "x");
        var manifest = new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion, "product", "repository", null,
            RunGitOutput("rev-parse", "HEAD").Trim(), DateTimeOffset.UtcNow,
            ["owned", "owned/child", "owned/file.txt"], ["/owned/"], [], "1.9.0",
            "owned/tool.nupkg", "extension", []);
        var plan = (System.Collections.IEnumerable)Invoke<object>("BuildRemovalPlan", _root, manifest);
        Assert.Single(plan.Cast<object>());

        Directory.CreateDirectory(Path.Combine(_root, ".github", "agents"));
        File.WriteAllText(Path.Combine(_root, ".github", "agents", "existing.md"), "original");
        var quarantine = Path.Combine(_root, ".git", "remove-test");
        Directory.CreateDirectory(quarantine);
        Invoke<object?>("CaptureSharedHostFilesForRollback", _root, quarantine);
        Invoke<object?>("AssertSharedHostFilesPreserved", _root, quarantine, manifest);
        File.WriteAllText(Path.Combine(_root, ".github", "agents", "existing.md"), "changed");
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("AssertSharedHostFilesPreserved", _root, quarantine, manifest));
        Invoke<object?>("RestoreSharedHostFilesFromQuarantine", _root, quarantine);
        Assert.Equal("original", File.ReadAllText(Path.Combine(_root, ".github", "agents", "existing.md")));
    }

    [Fact]
    public void RemovalRollbackHelpers_restoreFilesDirectories_andEmptyExclude()
    {
        var exclude = Path.Combine(_root, ".git", "info", "exclude");
        Invoke<object?>("WriteExcludeWithoutManagedBlock", exclude,
            "# >>> ELK_OVERLAY_MANAGED >>>\n/owned/\n# <<< ELK_OVERLAY_MANAGED <<<\n");
        Assert.Equal(string.Empty, File.ReadAllText(exclude));

        Assert.Throws<InvalidOperationException>(() =>
            Invoke<object?>("PreflightRemovalHooks", _root, (object)new[] { "missing-hook" }));

        var fileSource = Path.Combine(_root, "restored", "file.txt");
        var fileQuarantine = Path.Combine(_root, ".git", "quarantine", "file.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(fileQuarantine)!);
        File.WriteAllText(fileQuarantine, "file-content");

        var directorySource = Path.Combine(_root, "restored-directory");
        var directoryQuarantine = Path.Combine(_root, ".git", "quarantine", "directory");
        Directory.CreateDirectory(directoryQuarantine);
        File.WriteAllText(Path.Combine(directoryQuarantine, "child.txt"), "directory-content");

        var moved = new List<(string Source, string Quarantine, bool Directory)>
        {
            (fileSource, fileQuarantine, false),
            (directorySource, directoryQuarantine, true),
            (Path.Combine(_root, "missing-file"), Path.Combine(_root, ".git", "missing-file"), false),
            (Path.Combine(_root, "missing-directory"), Path.Combine(_root, ".git", "missing-directory"), true),
        };
        Invoke<object?>("RestoreMovedPaths", moved);
        Assert.Equal("file-content", File.ReadAllText(fileSource));
        Assert.Equal("directory-content", File.ReadAllText(Path.Combine(directorySource, "child.txt")));

        var quarantine = Path.Combine(_root, ".git", "shared-missing");
        Directory.CreateDirectory(Path.Combine(quarantine, "shared", ".github", "agents"));
        File.WriteAllText(Path.Combine(quarantine, "shared", ".github", "agents", "existing.md"), "expected");
        var manifest = new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion, "product", "repository", null,
            RunGitOutput("rev-parse", "HEAD").Trim(), DateTimeOffset.UtcNow,
            [".engloop"], ["/.engloop/"], [], "1.9.0", ".engloop/tool.nupkg", "extension", []);
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("AssertSharedHostFilesPreserved", _root, quarantine, manifest));
    }

    [Fact]
    public void RemovalBranchMatrix_coversMissingExclude_priorHook_andMovedPathAbsence()
    {
        var exclude = Path.Combine(_root, ".git", "info", "exclude");
        File.Delete(exclude);
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("WriteExcludeWithoutManagedBlock", exclude, string.Empty));

        var hooks = Path.Combine(_root, ".git", "hooks");
        var prePush = Path.Combine(hooks, "pre-push");
        File.WriteAllText(prePush, "# ELK_OVERLAY_HOOK\n");
        File.WriteAllText(prePush + ".elk-prior", "prior");
        var snapshots = Invoke<object>("CaptureHookSnapshots", _root);
        File.Delete(prePush);
        File.Delete(prePush + ".elk-prior");
        Invoke<object?>("RestoreHookSnapshots", _root, snapshots);
        Assert.Equal("prior", File.ReadAllText(prePush + ".elk-prior"));

        var foreign = Path.Combine(hooks, "pre-commit");
        File.WriteAllText(foreign, "foreign");
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>("PreflightRemovalHooks", _root, (object)new[] { "pre-commit" }));

        var sourceFile = Path.Combine(_root, "owned.txt");
        var quarantineFile = Path.Combine(_root, ".git", "q", "owned.txt");
        File.WriteAllText(sourceFile, "owned");
        var moved = new List<(string Source, string Quarantine, bool Directory)>();
        Invoke<object?>("MoveToQuarantine", sourceFile, quarantineFile, false, moved);
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(quarantineFile));
        Invoke<object?>("RestoreMovedPaths", moved);
        Assert.Equal("owned", File.ReadAllText(sourceFile));

        var sourceDirectory = Path.Combine(_root, "owned-dir");
        var quarantineDirectory = Path.Combine(_root, ".git", "q", "owned-dir");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "child"), "child");
        moved.Clear();
        Invoke<object?>("MoveToQuarantine", sourceDirectory, quarantineDirectory, true, moved);
        Assert.False(Directory.Exists(sourceDirectory));
        Invoke<object?>("RestoreMovedPaths", moved);
        Assert.Equal("child", File.ReadAllText(Path.Combine(sourceDirectory, "child")));
    }

    [Fact]
    public void HookBaselineAndQuarantineFailureMatrix_coversAllRemovalStates()
    {
        var hooks = Path.Combine(_root, ".git", "hooks");
        var overlayHooks = Path.Combine(_root, ".engloop-overlay", "hooks");
        Directory.CreateDirectory(overlayHooks);

        var restore = Path.Combine(hooks, "restore");
        File.WriteAllText(restore, "current-wrapper");
        File.WriteAllText(restore + ".elk-prior", "intermediate-prior");
        File.WriteAllText(Path.Combine(overlayHooks, "restore.before"), "original-before");

        var absent = Path.Combine(hooks, "absent");
        File.WriteAllText(absent, "current-wrapper");
        File.WriteAllText(absent + ".elk-prior", "intermediate-prior");
        File.WriteAllText(Path.Combine(overlayHooks, "absent.absent"), "absent");

        var legacyPrior = Path.Combine(hooks, "legacy-prior");
        File.WriteAllText(legacyPrior, "current-wrapper");
        File.WriteAllText(legacyPrior + ".elk-prior", "legacy-original");

        var legacyWrapper = Path.Combine(hooks, "legacy-wrapper");
        File.WriteAllText(legacyWrapper, "legacy-wrapper-content");

        Invoke<object?>("RestoreOverlayHooks", _root, (object)new[] { "restore", "absent", "legacy-prior", "legacy-wrapper" });
        Assert.Equal("original-before", File.ReadAllText(restore));
        Assert.False(File.Exists(restore + ".elk-prior"));
        Assert.False(File.Exists(absent));
        Assert.False(File.Exists(absent + ".elk-prior"));
        Assert.Equal("legacy-original", File.ReadAllText(legacyPrior));
        Assert.Equal("legacy-wrapper-content", File.ReadAllText(legacyWrapper));

        File.WriteAllText(Path.Combine(hooks, "captured"), "captured-content");
        var snapshots = Invoke<object>("CaptureHookSnapshots", _root);
        Directory.Delete(Path.Combine(_root, ".engloop-overlay"), recursive: true);
        Invoke<object?>("WriteHookBaselines", _root, snapshots);
        Assert.True(File.Exists(Path.Combine(_root, ".engloop-overlay", "hooks", "pre-commit.absent")));
        Assert.True(File.Exists(Path.Combine(_root, ".engloop-overlay", "hooks", "pre-push.absent")));

        var moved = new List<(string Source, string Quarantine, bool Directory)>();
        var sourceFile = Path.Combine(_root, "failure-file");
        var destinationFile = Path.Combine(_root, ".git", "failure-file");
        File.WriteAllText(sourceFile, "source");
        File.WriteAllText(destinationFile, "collision");
        var fileError = Assert.Throws<IOException>(() => Invoke<object?>("MoveToQuarantine", sourceFile, destinationFile, false, moved));
        Assert.Contains("operation=file", fileError.Message);

        var sourceDirectory = Path.Combine(_root, "failure-directory");
        var destinationDirectory = Path.Combine(_root, ".git", "failure-directory");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(destinationDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "child"), "source");
        File.WriteAllText(Path.Combine(destinationDirectory, "child"), "collision");
        var directoryError = Assert.Throws<IOException>(() => Invoke<object?>("MoveToQuarantine", sourceDirectory, destinationDirectory, true, moved));
        Assert.Contains("operation=directory-children-first", directoryError.Message);
    }

    private static T Invoke<T>(string name, params object[] args)
    {
        var method = typeof(OverlayCommands).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("private method not found: " + name);
        try
        {
            return (T)method.Invoke(null, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private void RunGit(params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = _root, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        process.WaitForExit();
        if (process.ExitCode != 0) throw new Xunit.Sdk.XunitException(process.StandardError.ReadToEnd());
    }

    private string RunGitOutput(params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = _root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new Xunit.Sdk.XunitException(process.StandardError.ReadToEnd());
        return output;
    }

    private static void WriteZipEntry(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(text);
    }



    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }
}
