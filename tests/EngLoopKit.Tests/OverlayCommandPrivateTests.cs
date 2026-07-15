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
        for (var i = 1; i <= 14; i++)
        {
            File.WriteAllText(Path.Combine(agents, $"speckit.engloop.{i:D2}.agent.md"), "agent");
            File.WriteAllText(Path.Combine(prompts, $"speckit.engloop.{i:D2}.prompt.md"), "prompt");
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
