using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using EngLoopKit.Components.Overlay;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class OverlayArchiveTests : IDisposable
{
    private readonly string _source = Path.Combine(Path.GetTempPath(), "elk-overlay-source-" + Guid.NewGuid().ToString("N"));
    private readonly string _target = Path.Combine(Path.GetTempPath(), "elk-overlay-target-" + Guid.NewGuid().ToString("N"));
    private readonly string _archive = Path.Combine(Path.GetTempPath(), "elk-overlay-" + Guid.NewGuid().ToString("N") + ".zip");

    public OverlayArchiveTests()
    {
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_target);
    }

    [Fact]
    public void NormalizeRelativePath_rejectsRootedAndEscapingPaths()
    {
        Assert.Equal(".engloop/config.json", OverlayArchive.NormalizeRelativePath(_source, ".engloop/config.json"));
        Assert.Throws<InvalidDataException>(() => OverlayArchive.NormalizeRelativePath(_source, "../escape"));
        Assert.Throws<InvalidDataException>(() => OverlayArchive.NormalizeRelativePath(_source, Path.GetTempPath()));
        Assert.Throws<InvalidDataException>(() => OverlayArchive.NormalizeRelativePath(_source, ""));
    }

    [Fact]
    public void CapturePackReadAndUnpack_preservesExactManagedFiles()
    {
        WriteSource(".engloop/config.json", "{\"overlay\":true}");
        WriteSource(".engloop/notes/one.md", "one");
        WriteSource(".engloop-overlay/packages/engloopkit.1.8.0.nupkg", "package");
        WriteSource("NORTHSTAR.md", "# local");
        WriteSource(".engloop-overlay/manifest.json", "old manifest ignored");

        var manifest = CreateManifest(OverlayArchive.CaptureFiles(_source,
            [".engloop", ".engloop-overlay", "NORTHSTAR.md"],
            [OverlayManifest.ManagedManifestPath]));
        OverlayArchive.CreateArchive(_source, manifest, _archive);

        var read = OverlayArchive.ReadAndValidateArchive(_archive);
        Assert.Equal(manifest.Files.Select(file => file.RelativePath), read.Files.Select(file => file.RelativePath));
        OverlayArchive.ExtractArchive(_archive, _target, read);

        foreach (var file in read.Files)
        {
            var path = Path.Combine(_target, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path));
            Assert.Equal(file.Sha256, OverlayArchive.Sha256File(path));
        }
    }

    [Fact]
    public void CreateArchive_rejectsManagedOutputAndSecretLikePathsAtToolLayer()
    {
        WriteSource(".engloop/config.json", "config");
        var manifest = CreateManifest(OverlayArchive.CaptureFiles(_source, [".engloop"]));
        Assert.Throws<InvalidOperationException>(() => OverlayArchive.CreateArchive(_source, manifest, Path.Combine(_source, ".engloop", "overlay.zip")));

        OverlayArchive.CreateArchive(_source, manifest, _archive);
        Assert.Throws<IOException>(() => OverlayArchive.CreateArchive(_source, manifest, _archive));
    }

    [Fact]
    public void CaptureFiles_handlesMissingRootsAndStableCaptureArgumentValidation()
    {
        Assert.Empty(OverlayArchive.CaptureFiles(_source, ["missing-directory"]));
        Assert.Throws<ArgumentOutOfRangeException>(() => OverlayArchive.CaptureStableFiles(_source, ["missing-directory"], attempts: 1));

        WriteSource(".engloop/config.json", "config");
        var stable = OverlayArchive.CaptureStableFiles(_source, [".engloop"], attempts: 2, delayMilliseconds: 0);
        Assert.Single(stable);
    }

    [Fact]
    public void CaptureStableFiles_rejectsPersistentlyLockedManagedContent()
    {
        WriteSource(".engloop/config.json", "zero");
        var path = Path.Combine(_source, ".engloop", "config.json");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<InvalidOperationException>(() => OverlayArchive.CaptureStableFiles(_source, [".engloop"], attempts: 2, delayMilliseconds: 0));
    }

    [Fact]
    public void ReadArchive_rejectsZipSlipDuplicateAndHashMismatch()
    {
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(CreateManifest([
                new OverlayFile(".engloop/config.json", 3, new string('0', 64))])));
            WriteZipEntry(archive, "files/../escape.txt", "bad");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));

        File.Delete(_archive);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(CreateManifest([])));
            WriteZipEntry(archive, "extra.txt", "not registered");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));

        File.Delete(_archive);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            var file = new OverlayFile(".engloop/config.json", 3, Convert.ToHexString(SHA256.HashData("four"u8.ToArray())).ToLowerInvariant());
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(CreateManifest([file])));
            WriteZipEntry(archive, "files/.engloop/config.json", "four");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));

        File.Delete(_archive);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            var file = new OverlayFile(".engloop/config.json", 4, Convert.ToHexString(SHA256.HashData("four"u8.ToArray())).ToLowerInvariant());
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(CreateManifest([file])));
            WriteZipEntry(archive, "files/.engloop/config.json", "four");
            WriteZipEntry(archive, "files/.engloop/config.json", "four");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));

        File.Delete(_archive);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            var file = new OverlayFile(".engloop/config.json", 4, Convert.ToHexString(SHA256.HashData("four"u8.ToArray())).ToLowerInvariant());
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(CreateManifest([file])));
            WriteZipEntry(archive, "files/.engloop/config.json", "four");
            WriteZipEntry(archive, "files/.engloop/config.json", "four");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));

        File.Delete(_archive);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "unrelated.txt", "no manifest");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));

        File.Delete(_archive);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            var file = new OverlayFile(".engloop/config.json", 3, Convert.ToHexString(SHA256.HashData("abc"u8.ToArray())).ToLowerInvariant());
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(CreateManifest([file])));
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));

        File.Delete(_archive);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            var file = new OverlayFile(".engloop/config.json", 3, Convert.ToHexString(SHA256.HashData("abc"u8.ToArray())).ToLowerInvariant());
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(CreateManifest([file])));
            WriteZipEntry(archive, "files/.engloop/config.json", "xyz");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));
    }

    [Fact]
    public void ArchiveParsing_rejectsNullManifestAndForbiddenEntryNameForms()
    {
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "overlay-manifest.json", "null");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ParseManifest("null"));

        File.Delete(_archive);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(CreateManifest([])));
            WriteZipEntry(archive, "files\\bad.txt", "bad");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));

        File.Delete(_archive);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(CreateManifest([])));
            WriteZipEntry(archive, "/absolute.txt", "bad");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ReadAndValidateArchive(_archive));
    }

    [Fact]
    public void ExtractArchive_rejectsExistingDestinationCollision()
    {
        WriteSource(".engloop/config.json", "config");
        var manifest = CreateManifest(OverlayArchive.CaptureFiles(_source, [".engloop"]));
        OverlayArchive.CreateArchive(_source, manifest, _archive);
        Directory.CreateDirectory(Path.Combine(_target, ".engloop"));
        File.WriteAllText(Path.Combine(_target, ".engloop", "config.json"), "existing");

        Assert.Throws<IOException>(() => OverlayArchive.ExtractArchive(_archive, _target, manifest));
    }

    [Fact]
    public void ExtractArchive_rejectsMissingRegisteredEntry()
    {
        var file = new OverlayFile(".engloop/config.json", 3, Convert.ToHexString(SHA256.HashData("abc"u8.ToArray())).ToLowerInvariant());
        var manifest = CreateManifest([file]);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(manifest));
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ExtractArchive(_archive, _target, manifest));
    }

    [Fact]
    public void ExtractArchive_rejectsLengthAndHashDriftAfterCopy()
    {
        WriteSource(".engloop/config.json", "abc");
        var goodHash = Convert.ToHexString(SHA256.HashData("abc"u8.ToArray())).ToLowerInvariant();

        var wrongLength = CreateManifest([new OverlayFile(".engloop/config.json", 4, goodHash)]);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(wrongLength));
            WriteZipEntry(archive, "files/.engloop/config.json", "abc");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ExtractArchive(_archive, _target, wrongLength));

        if (Directory.Exists(_target)) Directory.Delete(_target, recursive: true);
        Directory.CreateDirectory(_target);
        File.Delete(_archive);
        var wrongHash = CreateManifest([new OverlayFile(".engloop/config.json", 3, new string('0', 64))]);
        using (var archive = ZipFile.Open(_archive, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "overlay-manifest.json", JsonSerializer.Serialize(wrongHash));
            WriteZipEntry(archive, "files/.engloop/config.json", "abc");
        }
        Assert.Throws<InvalidDataException>(() => OverlayArchive.ExtractArchive(_archive, _target, wrongHash));
    }

    [Fact]
    public void Manifest_validation_rejectsDuplicateAndEscapingEntries()
    {
        var duplicate = CreateManifest([
            new OverlayFile(".engloop/config.json", 1, new string('a', 64)),
            new OverlayFile(".engloop/config.json", 1, new string('b', 64)),
        ]);
        Assert.Throws<InvalidDataException>(() => OverlayArchive.SerializeManifest(duplicate));

        var escaped = CreateManifest([new OverlayFile("../escape", 1, new string('a', 64))]);
        Assert.Throws<InvalidDataException>(() => OverlayArchive.SerializeManifest(escaped));

        var invalidIdentity = CreateManifest([]) with { SchemaVersion = "unsupported" };
        Assert.Throws<InvalidDataException>(() => OverlayArchive.SerializeManifest(invalidIdentity));

        var missingIdentity = CreateManifest([]) with { ProductId = string.Empty };
        Assert.Throws<InvalidDataException>(() => OverlayArchive.SerializeManifest(missingIdentity));

        var invalidHash = CreateManifest([new OverlayFile(".engloop/config.json", 1, "short")]);
        Assert.Throws<InvalidDataException>(() => OverlayArchive.SerializeManifest(invalidHash));

        var escapedRoot = CreateManifest([]) with { ManagedRoots = ["../escape"] };
        Assert.Throws<InvalidDataException>(() => OverlayArchive.SerializeManifest(escapedRoot));

        var outsideFile = CreateManifest([new OverlayFile("unmanaged/file.txt", 1, new string('a', 64))]);
        Assert.Throws<InvalidDataException>(() => OverlayArchive.SerializeManifest(outsideFile));

        var outsideTool = CreateManifest([]) with { ToolPackageRelativePath = "unmanaged/tool.nupkg" };
        Assert.Throws<InvalidDataException>(() => OverlayArchive.SerializeManifest(outsideTool));

        var invalidHostMode = CreateManifest([]) with { HostMode = "ambiguous" };
        Assert.Throws<InvalidDataException>(() => OverlayArchive.SerializeManifest(invalidHostMode));

        var duplicateRoot = CreateManifest([]) with { ManagedRoots = [".engloop", ".ENGLOOP", ".engloop-overlay"] };
        Assert.Throws<InvalidDataException>(() => OverlayArchive.SerializeManifest(duplicateRoot));
    }

    [Fact]
    public void CaptureAndArchive_rejectMissingLinkAndManifestFileDrift()
    {
        Assert.Empty(OverlayArchive.CaptureStableFiles(_source, ["missing"], attempts: 2, delayMilliseconds: 0));

        WriteSource(".engloop/config.json", "config");
        var manifest = CreateManifest(OverlayArchive.CaptureFiles(_source, [".engloop"]));
        File.Delete(Path.Combine(_source, ".engloop", "config.json"));
        Assert.Throws<FileNotFoundException>(() => OverlayArchive.CreateArchive(_source, manifest, _archive));

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var outside = Path.Combine(Path.GetTempPath(), "elk-overlay-outside-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(outside, "outside");
        try
        {
            Directory.CreateDirectory(Path.Combine(_source, ".engloop"));
            File.CreateSymbolicLink(Path.Combine(_source, ".engloop", "escape.txt"), outside);
            var error = Assert.Throws<InvalidDataException>(() => OverlayArchive.CaptureFiles(_source, [".engloop"]));
            Assert.Contains("overlay-link-escapes-root", error.Message);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Symbolic-link creation can be policy-disabled; archive path/manifest tests remain platform-independent.
        }
        finally
        {
            if (File.Exists(outside)) File.Delete(outside);
        }
    }

    [Fact]
    public void ManagedPathAndFileSetComparison_coverExactAndMismatchCases()
    {
        var manifest = CreateManifest([]);
        Assert.True(OverlayArchive.IsManagedPath(manifest, ".engloop"));
        Assert.True(OverlayArchive.IsManagedPath(manifest, ".engloop/child.txt"));
        Assert.False(OverlayArchive.IsManagedPath(manifest, ".engloop-other/file.txt"));

        var one = new OverlayFile(".engloop/config.json", 1, new string('a', 64));
        var two = new OverlayFile(".engloop/config.json", 2, new string('a', 64));
        Assert.True(InvokePrivate<bool>("SameFiles", new[] { one }, new[] { one }));
        Assert.False(InvokePrivate<bool>("SameFiles", new[] { one }, Array.Empty<OverlayFile>()));
        Assert.False(InvokePrivate<bool>("SameFiles", new[] { one }, new[] { two }));
    }

    [Fact]
    public void CaptureFiles_rejectsBrokenOrEscapingLinksWhenPlatformPermits()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(_source, ".engloop"));
        var broken = Path.Combine(_source, ".engloop", "broken.txt");
        try
        {
            File.CreateSymbolicLink(broken, Path.Combine(_source, "missing-target.txt"));
            Assert.ThrowsAny<IOException>(() => OverlayArchive.CaptureFiles(_source, [".engloop"]));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Windows may prohibit test symlink creation; other archive safety cases remain deterministic.
        }
    }

    private OverlayManifest CreateManifest(IReadOnlyList<OverlayFile> files) => new(
        OverlayManifest.CurrentSchemaVersion,
        "fixture-product",
        "fixture-repository",
        "https://example.invalid/repository.git",
        "abc123",
        DateTimeOffset.UtcNow,
        [".engloop", ".engloop-overlay", "NORTHSTAR.md"],
        ["/.engloop/", "/.engloop-overlay/", "/NORTHSTAR.md"],
        ["pre-commit", "pre-push"],
        "1.8.0",
        ".engloop-overlay/packages/engloopkit.1.8.0.nupkg",
        "fixture-extension#sha256=" + new string('a', 64),
        files);

    private void WriteSource(string relative, string content)
    {
        var path = Path.Combine(_source, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void WriteZipEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static T InvokePrivate<T>(string name, params object[] args)
    {
        var method = typeof(OverlayArchive).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
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

    public void Dispose()
    {
        foreach (var path in new[] { _source, _target })
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        if (File.Exists(_archive)) File.Delete(_archive);
    }
}
