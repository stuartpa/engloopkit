using System.IO.Compression;
using System.Diagnostics;
using System.Text.Json;
using EngLoopKit.Components.Overlay;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class OverlayCommandFailureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "elk-overlay-fail-" + Guid.NewGuid().ToString("N"));
    private readonly string _tool = Path.Combine(Path.GetTempPath(), "elk-overlay-tool-" + Guid.NewGuid().ToString("N") + ".nupkg");
    private readonly string _extension = Path.Combine(Path.GetTempPath(), "elk-overlay-extension-" + Guid.NewGuid().ToString("N") + ".zip");
    private readonly string _archive = Path.Combine(Path.GetTempPath(), "elk-overlay-archive-" + Guid.NewGuid().ToString("N") + ".zip");

    public OverlayCommandFailureTests()
    {
        Directory.CreateDirectory(_root);
        Run("git", _root, "init");
        Run("git", _root, "config", "user.email", "overlay@example.invalid");
        Run("git", _root, "config", "user.name", "Overlay Test");
        File.WriteAllText(Path.Combine(_root, "README.md"), "# root");
        Run("git", _root, "add", "README.md");
        Run("git", _root, "commit", "-m", "initial");
        File.WriteAllText(_tool, "not-a-real-package");
        using (ZipFile.Open(_extension, ZipArchiveMode.Create)) { }
    }

    [Fact]
    public void Dispatcher_rejectsEmptyAndUnknownOverlayCommands()
    {
        Assert.NotEqual(0, Program.Main(["overlay"]));
        Assert.NotEqual(0, OverlayCommands.Execute([]));
        Assert.Contains("Usage:", OverlayCommands.LastError);
        Assert.NotEqual(0, OverlayCommands.Execute(["unknown"]));
        Assert.Equal("overlay-invalid-command", OverlayCommands.LastError);
    }

    [Fact]
    public void Register_requiresAPath()
    {
        Assert.NotEqual(0, OverlayCommands.Execute(["register", "--root", _root]));
        Assert.Equal("overlay-register-requires-path", OverlayCommands.LastError);
    }

    [Fact]
    public void Install_requiresExplicitOverlayModeAndValidIdentity()
    {
        Assert.NotEqual(0, OverlayCommands.Execute(["install", "--root", _root]));
        Assert.Contains("overlay-missing-option:--mode", OverlayCommands.LastError);

        Assert.NotEqual(0, OverlayCommands.Execute(InstallArgs("normal", "valid-id")));
        Assert.Equal("overlay-install-requires-mode-overlay", OverlayCommands.LastError);

        Assert.NotEqual(0, OverlayCommands.Execute(InstallArgs("overlay", "Invalid Product!")));
        Assert.Equal("overlay-invalid-product-id", OverlayCommands.LastError);

        var missingArchive = InstallArgs("overlay", "valid-id");
        missingArchive[Array.IndexOf(missingArchive, "--extension-archive") + 1] = Path.Combine(_root, "missing-extension.zip");
        Assert.NotEqual(0, OverlayCommands.Execute(missingArchive));
        Assert.Contains("overlay-missing-file:--extension-archive", OverlayCommands.LastError);
    }

    [Fact]
    public void Install_rejectsManagedPathAndHookConflictsBeforeMutation()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".engloop"));
        Assert.NotEqual(0, OverlayCommands.Execute(InstallArgs("overlay", "valid-id")));
        Assert.Equal("overlay-install-conflict:.engloop", OverlayCommands.LastError);
        Directory.Delete(Path.Combine(_root, ".engloop"), recursive: true);

        var hook = Path.Combine(_root, ".git", "hooks", "pre-commit");
        File.WriteAllText(hook, "#!/bin/sh\necho non-elk\n");
        Assert.NotEqual(0, OverlayCommands.Execute(InstallArgs("overlay", "valid-id")));
        Assert.Equal("overlay-hook-conflict:pre-commit", OverlayCommands.LastError);
    }

    [Fact]
    public void Install_rejectsGitTrackedManagedPathWithoutWorkingTreeCollision()
    {
        var payload = Path.Combine(_root, "index-payload.txt");
        File.WriteAllText(payload, "tracked only in index");
        var hash = Run("git", _root, "hash-object", "-w", payload).StandardOutput.Trim();
        File.Delete(payload);
        Run("git", _root, "update-index", "--add", "--cacheinfo", "100644," + hash + ",NORTHSTAR.md");

        Assert.NotEqual(0, OverlayCommands.Execute(InstallArgs("overlay", "valid-id")));
        Assert.Equal("overlay-install-tracked-conflict:NORTHSTAR.md", OverlayCommands.LastError);
    }

    [Fact]
    public void Install_rollsBackLocalExcludesAndManagedRootsWhenExtensionCannotMaterialize()
    {
        var originalExclude = File.ReadAllText(Path.Combine(_root, ".git", "info", "exclude"));
        var args = InstallArgs("overlay", "valid-id");
        args[Array.IndexOf(args, "--extension-archive") + 1] = _extension;
        Assert.NotEqual(0, OverlayCommands.Execute(args));
        Assert.Contains("overlay-extension-archive-must-contain-one-extension-manifest", OverlayCommands.LastError);
        Assert.False(Directory.Exists(Path.Combine(_root, ".engloop-overlay")));
        Assert.False(Directory.Exists(Path.Combine(_root, ".engloop")));
        Assert.Equal(originalExclude, File.ReadAllText(Path.Combine(_root, ".git", "info", "exclude")));

    }

    [Fact]
    public void Install_rollbackRecreatesEmptyLocalExcludeWhenItWasAbsent()
    {
        var exclude = Path.Combine(_root, ".git", "info", "exclude");
        File.Delete(exclude);
        Assert.NotEqual(0, OverlayCommands.Execute(InstallArgs("overlay", "valid-id")));
        Assert.True(File.Exists(exclude));
        Assert.Equal(string.Empty, File.ReadAllText(exclude));
        Assert.False(Directory.Exists(Path.Combine(_root, ".engloop-overlay")));
    }

    [Fact]
    public void VerifyStatusAndPack_rejectMissingOrMalformedOverlayState()
    {
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root]));
        Assert.Contains("overlay-manifest-missing", OverlayCommands.LastError);
        Assert.NotEqual(0, OverlayCommands.Execute(["status", "--root", _root]));
        Assert.Contains("overlay-manifest-missing", OverlayCommands.LastError);
        Assert.NotEqual(0, OverlayCommands.Execute(["pack", "--root", _root, "--output", _archive]));
        Assert.Contains("overlay-manifest-missing", OverlayCommands.LastError);

        Directory.CreateDirectory(Path.Combine(_root, ".engloop-overlay"));
        File.WriteAllText(Path.Combine(_root, ".engloop-overlay", "manifest.json"), "not-json");
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", _root, "--mode", "nonsense"]));
        Assert.NotNull(OverlayCommands.LastError);
    }

    [Fact]
    public void Unpack_rejectsMissingInputMalformedArchiveAndOutsideRoot()
    {
        Assert.NotEqual(0, OverlayCommands.Execute(["unpack", "--root", _root, "--input", _archive, "--repository-id", "id"]));
        Assert.Contains("overlay-missing-file:--input", OverlayCommands.LastError);

        File.WriteAllText(_archive, "not-a-zip");
        Assert.NotEqual(0, OverlayCommands.Execute(["unpack", "--root", _root, "--input", _archive, "--repository-id", "id"]));
        Assert.NotNull(OverlayCommands.LastError);

        var subdirectory = Path.Combine(_root, "sub");
        Directory.CreateDirectory(subdirectory);
        Assert.NotEqual(0, OverlayCommands.Execute(["verify", "--root", subdirectory]));
        Assert.Equal("overlay-root-must-be-selected-git-root", OverlayCommands.LastError);
    }

    [Fact]
    public void Unpack_rejectsRepositoryOriginAndRevisionMismatchesBeforeMutation()
    {
        var toolManifestBytes = "{\"version\":1,\"isRoot\":true,\"tools\":{}}"u8.ToArray();
        var packageBytes = "placeholder-package"u8.ToArray();
        var toolPath = ".engloop-overlay/packages/tool.nupkg";
        var manifest = new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion, "overlay", "expected-id", "https://example.invalid/origin.git",
            "0000000000000000000000000000000000000000", DateTimeOffset.UtcNow,
            [".config", ".engloop-overlay"], ["/.config/", "/.engloop-overlay/"], [], "1.8.0", toolPath, "extension",
            [
                new OverlayFile(".config/dotnet-tools.json", toolManifestBytes.Length, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(toolManifestBytes)).ToLowerInvariant()),
                new OverlayFile(toolPath, packageBytes.Length, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(packageBytes)).ToLowerInvariant()),
            ]);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry(OverlayManifest.ArchiveManifestEntry);
            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write(OverlayArchive.SerializeManifest(manifest));
            }
            WriteEntry(archive, "files/.config/dotnet-tools.json", toolManifestBytes);
            WriteEntry(archive, "files/" + toolPath, packageBytes);
        }

        Assert.NotEqual(0, OverlayCommands.Execute(["unpack", "--root", _root, "--input", _archive, "--repository-id", "wrong-id"]));
        Assert.Equal("overlay-repository-id-mismatch", OverlayCommands.LastError);

        Run("git", _root, "remote", "add", "origin", "https://another.invalid/repo.git");
        Assert.NotEqual(0, OverlayCommands.Execute(["unpack", "--root", _root, "--input", _archive, "--repository-id", "expected-id"]));
        Assert.Equal("overlay-origin-mismatch", OverlayCommands.LastError);

        Run("git", _root, "remote", "set-url", "origin", "https://example.invalid/origin.git");
        Assert.NotEqual(0, OverlayCommands.Execute(["unpack", "--root", _root, "--input", _archive, "--repository-id", "expected-id"]));
        Assert.Equal("overlay-base-revision-mismatch", OverlayCommands.LastError);
        Assert.False(Directory.Exists(Path.Combine(_root, ".engloop")));
    }

    [Fact]
    public void Unpack_rejectsArchiveWithoutRequiredLocalToolFiles()
    {
        var baseline = Run("git", _root, "rev-parse", "HEAD").StandardOutput.Trim();
        var manifest = new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion, "overlay", "fixture-repository", null,
            baseline, DateTimeOffset.UtcNow, [".engloop"], ["/.engloop/"], ["pre-commit", "pre-push"],
            "1.8.0", ".engloop/missing-packages/tool.nupkg", "extension", []);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry(OverlayManifest.ArchiveManifestEntry);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(OverlayArchive.SerializeManifest(manifest));
        }

        Assert.NotEqual(0, OverlayCommands.Execute(["unpack", "--root", _root, "--input", _archive, "--repository-id", "fixture-repository"]));
        Assert.Equal("overlay-archive-missing-tool-manifest", OverlayCommands.LastError);
        Assert.False(Directory.Exists(Path.Combine(_root, ".engloop")));
        Assert.False(Directory.Exists(Path.Combine(_root, ".engloop-overlay")));
    }

    [Fact]
    public void Unpack_rejectsArchiveWithToolManifestButNoToolPackage()
    {
        var baseline = Run("git", _root, "rev-parse", "HEAD").StandardOutput.Trim();
        var toolManifest = "{\"version\":1,\"isRoot\":true,\"tools\":{}}"u8.ToArray();
        var manifest = new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion, "overlay", "fixture-repository", null,
            baseline, DateTimeOffset.UtcNow, [".config", ".engloop-overlay"], ["/.config/", "/.engloop-overlay/"], [],
            "1.8.0", ".engloop-overlay/packages/tool.nupkg", "extension",
            [new OverlayFile(".config/dotnet-tools.json", toolManifest.Length, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(toolManifest)).ToLowerInvariant())]);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry(OverlayManifest.ArchiveManifestEntry);
            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write(OverlayArchive.SerializeManifest(manifest));
            }
            WriteEntry(archive, "files/.config/dotnet-tools.json", toolManifest);
        }

        Assert.NotEqual(0, OverlayCommands.Execute(["unpack", "--root", _root, "--input", _archive, "--repository-id", "fixture-repository"]));
        Assert.Equal("overlay-archive-missing-tool-package", OverlayCommands.LastError);
    }

    [Fact]
    public void Unpack_rollsBackAfterInvalidLocalToolRestore()
    {
        var source = Path.Combine(Path.GetTempPath(), "elk-overlay-unpack-source-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(Path.GetTempPath(), "elk-overlay-unpack-target-" + Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(Path.GetTempPath(), "elk-overlay-unpack-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            Directory.CreateDirectory(source);
            Run("git", source, "init");
            Run("git", source, "config", "user.email", "overlay@example.invalid");
            Run("git", source, "config", "user.name", "Overlay Test");
            File.WriteAllText(Path.Combine(source, "README.md"), "# source");
            Run("git", source, "add", "README.md");
            Run("git", source, "commit", "-m", "initial");

            Directory.CreateDirectory(Path.Combine(source, ".config"));
            Directory.CreateDirectory(Path.Combine(source, ".engloop-overlay", "packages"));
                        File.WriteAllText(Path.Combine(source, ".config", "dotnet-tools.json"), """
                        {
                            "version": 1,
                            "isRoot": true,
                            "tools": {
                                "engloopkit": {
                                      "version": "9.9.9-rollback",
                                    "commands": ["engloopkit"],
                                    "rollForward": false
                                }
                            }
                        }
                        """);
            File.WriteAllText(Path.Combine(source, ".engloop-overlay", "packages", "broken.nupkg"), "not a package");
            var files = OverlayArchive.CaptureStableFiles(source, [".config", ".engloop-overlay"]);
            var manifest = new OverlayManifest(
                OverlayManifest.CurrentSchemaVersion, "overlay", "rollback-repository", null,
                Run("git", source, "rev-parse", "HEAD").StandardOutput.Trim(), DateTimeOffset.UtcNow,
                [".config", ".engloop-overlay"], ["/.config/", "/.engloop-overlay/"], ["pre-commit", "pre-push"],
                "9.9.9-rollback", ".engloop-overlay/packages/broken.nupkg", "extension", files);
            OverlayArchive.CreateArchive(source, manifest, archivePath);

            Run("git", Path.GetTempPath(), "clone", source, target);
            Run("git", target, "config", "user.email", "overlay@example.invalid");
            Run("git", target, "config", "user.name", "Overlay Test");
            var originalExclude = File.ReadAllText(Path.Combine(target, ".git", "info", "exclude"));

            Assert.NotEqual(0, OverlayCommands.Execute(["unpack", "--root", target, "--input", archivePath, "--repository-id", "rollback-repository"]));
            Assert.Contains("overlay-command-failed:dotnet", OverlayCommands.LastError);
            Assert.False(File.Exists(Path.Combine(target, ".config", "dotnet-tools.json")));
            Assert.False(Directory.Exists(Path.Combine(target, ".engloop-overlay")));
            Assert.Equal(originalExclude, File.ReadAllText(Path.Combine(target, ".git", "info", "exclude")));
        }
        finally
        {
            foreach (var path in new[] { source, target }) { try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { } }
            try { if (File.Exists(archivePath)) File.Delete(archivePath); } catch { }
        }
    }

    private string[] InstallArgs(string mode, string productId) =>
    [
        "install", "--mode", mode, "--root", _root,
        "--product-id", productId, "--repository-id", "fixture-repository",
        "--tool-version", "1.8.0", "--tool-nupkg", _tool,
        "--extension-archive", _extension,
    ];

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

    private static void WriteEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, recursive: true); } catch { /* temporary Git objects can remain briefly locked on Windows */ }
        }
        foreach (var path in new[] { _tool, _extension, _archive })
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort disposable cleanup */ }
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
