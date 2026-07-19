using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace EngLoopKit.Components.Overlay;

public sealed record OverlayFile(string RelativePath, long Length, string Sha256);

public sealed record OverlayManifest(
    string SchemaVersion,
    string ProductId,
    string RepositoryId,
    string? OriginUrl,
    string BaseRevision,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> ManagedRoots,
    IReadOnlyList<string> ExcludePatterns,
    IReadOnlyList<string> HookNames,
    string ToolVersion,
    string ToolPackageRelativePath,
    string ExtensionArchiveIdentity,
    IReadOnlyList<OverlayFile> Files,
    string HostMode = "clean")
{
    public const string CurrentSchemaVersion = "1.0";
    public const string ArchiveManifestEntry = "overlay-manifest.json";
    public const string ManagedManifestPath = ".engloop-overlay/manifest.json";
}

/// <summary>
/// Domain-free safe archive and path machinery for private overlay state. It knows nothing
/// about Spec Kit, Git, or a workload: callers provide the managed roots and repository
/// identity. Every path is root-relative, every archive entry is hashed, and any path
/// escape, duplicate, collision, or hash mismatch is a failure.
/// </summary>
public static class OverlayArchive
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
    };

    // Treat case-ambiguous overlay paths as duplicates on every platform. This is more
    // conservative than the underlying filesystem on some hosts, but prevents archives
    // from changing meaning when moved between case-sensitive and case-insensitive roots.
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public static string NormalizeRelativePath(string repositoryRoot, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate))
        {
            throw new InvalidDataException("overlay-path-must-be-nonempty-relative");
        }

        var root = Path.GetFullPath(repositoryRoot);
        var full = Path.GetFullPath(Path.Combine(root, candidate));
        var relative = Path.GetRelativePath(root, full);
        if (relative == "." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || relative == "..")
        {
            throw new InvalidDataException($"overlay-path-escapes-root:{candidate}");
        }

        return relative.Replace('\\', '/');
    }

    public static IReadOnlyList<OverlayFile> CaptureFiles(
        string repositoryRoot,
        IEnumerable<string> managedRoots,
        IEnumerable<string>? excludedRelativePaths = null)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var excluded = new HashSet<string>(excludedRelativePaths ?? [], PathComparer);
        var discovered = new Dictionary<string, OverlayFile>(PathComparer);

        foreach (var managedRoot in managedRoots)
        {
            var relativeRoot = NormalizeRelativePath(root, managedRoot);
            var fullRoot = Path.Combine(root, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullRoot))
            {
                AddFile(root, fullRoot, excluded, discovered);
                continue;
            }

            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
                         .OrderBy(path => path, PathComparer))
            {
                AddFile(root, path, excluded, discovered);
            }
        }

        return discovered.Values.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Capture managed files only after two consecutive snapshots agree. Official extension
    /// installation may create agent links before their targets are fully materialized; an
    /// unstable snapshot must fail or retry rather than produce a misleading manifest.
    /// </summary>
    public static IReadOnlyList<OverlayFile> CaptureStableFiles(
        string repositoryRoot,
        IEnumerable<string> managedRoots,
        IEnumerable<string>? excludedRelativePaths = null,
        int attempts = 12,
        int delayMilliseconds = 100)
    {
        if (attempts < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(attempts));
        }

        IReadOnlyList<OverlayFile>? prior = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            IReadOnlyList<OverlayFile> current;
            try
            {
                current = CaptureFiles(repositoryRoot, managedRoots, excludedRelativePaths);
            }
            catch (IOException)
            {
                Thread.Sleep(delayMilliseconds);
                continue;
            }
            if (prior is not null && SameFiles(prior, current))
            {
                return current;
            }

            prior = current;
            Thread.Sleep(delayMilliseconds);
        }

        throw new InvalidOperationException("overlay-managed-files-did-not-stabilize");
    }

    public static void CreateArchive(string repositoryRoot, OverlayManifest manifest, string outputArchivePath)
    {
        ValidateManifest(manifest);
        var root = Path.GetFullPath(repositoryRoot);
        var output = Path.GetFullPath(outputArchivePath);
        var outputRelative = Path.GetRelativePath(root, output);
        if (!outputRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !outputRelative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(outputRelative, "..", StringComparison.Ordinal)
            && IsManagedPath(manifest, outputRelative.Replace('\\', '/')))
        {
            throw new InvalidOperationException("overlay-output-must-not-be-inside-managed-root");
        }

        var content = new List<(OverlayFile File, byte[] Bytes)>();
        foreach (var file in manifest.Files)
        {
            var path = ToFullPath(root, file.RelativePath);
            var bytes = ReadAndVerifyFile(path, file, root);
            content.Add((file, bytes));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        if (File.Exists(output))
        {
            throw new IOException($"overlay-output-already-exists:{output}");
        }

        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
        WriteEntry(archive, OverlayManifest.ArchiveManifestEntry, JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));
        foreach (var item in content)
        {
            var entryName = "files/" + item.File.RelativePath;
            WriteEntry(archive, entryName, item.Bytes);
        }
    }

    public static OverlayManifest ReadAndValidateArchive(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var manifestEntry = archive.GetEntry(OverlayManifest.ArchiveManifestEntry)
            ?? throw new InvalidDataException("overlay-archive-missing-manifest");
        using var stream = manifestEntry.Open();
        var manifest = JsonSerializer.Deserialize<OverlayManifest>(stream, JsonOptions)
            ?? throw new InvalidDataException("overlay-archive-invalid-manifest");
        ValidateManifest(manifest);

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (!names.Add(entry.FullName))
            {
                throw new InvalidDataException($"overlay-archive-duplicate-entry:{entry.FullName}");
            }

            if (entry.FullName.Contains("\\", StringComparison.Ordinal)
                || entry.FullName.StartsWith("/", StringComparison.Ordinal)
                || entry.FullName.Contains("../", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"overlay-archive-path-escape:{entry.FullName}");
            }

            var expected = string.Equals(entry.FullName, OverlayManifest.ArchiveManifestEntry, StringComparison.Ordinal)
                || manifest.Files.Any(file => string.Equals(entry.FullName, "files/" + file.RelativePath, StringComparison.Ordinal));
            if (!expected)
            {
                throw new InvalidDataException($"overlay-archive-unregistered-entry:{entry.FullName}");
            }
        }

        foreach (var file in manifest.Files)
        {
            var entry = archive.GetEntry("files/" + file.RelativePath)
                ?? throw new InvalidDataException($"overlay-archive-missing-file:{file.RelativePath}");
            using var entryStream = entry.Open();
            using var hash = SHA256.Create();
            var actualHash = Convert.ToHexString(hash.ComputeHash(entryStream)).ToLowerInvariant();
            if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"overlay-archive-hash-mismatch:{file.RelativePath}");
            }

            if (entry.Length != file.Length)
            {
                throw new InvalidDataException($"overlay-archive-length-mismatch:{file.RelativePath}");
            }
        }

        return manifest;
    }

    public static void ExtractArchive(string archivePath, string repositoryRoot, OverlayManifest manifest)
    {
        ValidateManifest(manifest);
        var root = Path.GetFullPath(repositoryRoot);
        using var archive = ZipFile.OpenRead(archivePath);

        foreach (var file in manifest.Files)
        {
            var destination = ToFullPath(root, file.RelativePath);
            if (File.Exists(destination) || Directory.Exists(destination))
            {
                throw new IOException($"overlay-unpack-collision:{file.RelativePath}");
            }
        }

        foreach (var file in manifest.Files)
        {
            var entry = archive.GetEntry("files/" + file.RelativePath)
                ?? throw new InvalidDataException($"overlay-archive-missing-file:{file.RelativePath}");
            var destination = ToFullPath(root, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (var input = entry.Open())
            using (var output = File.Create(destination))
            {
                input.CopyTo(output);
            }
            _ = ReadAndVerifyFile(destination, file, root);
        }
    }

    public static string SerializeManifest(OverlayManifest manifest)
    {
        ValidateManifest(manifest);
        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    public static OverlayManifest ParseManifest(string json)
    {
        var manifest = JsonSerializer.Deserialize<OverlayManifest>(json, JsonOptions)
            ?? throw new InvalidDataException("overlay-invalid-manifest");
        ValidateManifest(manifest);
        return manifest;
    }

    public static bool IsManagedPath(OverlayManifest manifest, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        return manifest.ManagedRoots.Any(root =>
        {
            var normalizedRoot = root.Replace('\\', '/').TrimEnd('/');
            return string.Equals(normalized, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
        });
    }

    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        using var hash = SHA256.Create();
        return Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
    }

    private static void AddFile(string root, string fullPath, HashSet<string> excluded, Dictionary<string, OverlayFile> discovered)
    {
        EnsureLinkStaysWithinRoot(root, fullPath);
        var relative = NormalizeRelativePath(root, Path.GetRelativePath(root, fullPath));
        if (excluded.Contains(relative))
        {
            return;
        }

        var bytes = File.ReadAllBytes(fullPath);
        discovered[relative] = new OverlayFile(
            relative,
            bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static byte[] ReadAndVerifyFile(string path, OverlayFile expected, string root)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("overlay-managed-file-missing", path);
        }

        EnsureLinkStaysWithinRoot(root, path);
        var bytes = File.ReadAllBytes(path);
        if (bytes.LongLength != expected.Length)
        {
            throw new InvalidDataException($"overlay-file-length-mismatch:{expected.RelativePath}");
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(actualHash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"overlay-file-hash-mismatch:{expected.RelativePath}");
        }
        return bytes;
    }

    private static string ToFullPath(string root, string relativePath)
    {
        var normalized = NormalizeRelativePath(root, relativePath);
        return Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void EnsureLinkStaysWithinRoot(string root, string fullPath)
    {
        var resolved = File.ResolveLinkTarget(fullPath, returnFinalTarget: true)?.FullName;
        if (resolved is null) return;
        var boundary = Path.GetFullPath(root);
        var relative = Path.GetRelativePath(boundary, resolved).Replace('\\', '/');
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"overlay-link-escapes-root:{fullPath}");
        }
    }

    private static bool SameFiles(IReadOnlyList<OverlayFile> left, IReadOnlyList<OverlayFile> right)
        => left.Count == right.Count
           && left.Zip(right).All(pair => string.Equals(pair.First.RelativePath, pair.Second.RelativePath, StringComparison.Ordinal)
               && pair.First.Length == pair.Second.Length
               && string.Equals(pair.First.Sha256, pair.Second.Sha256, StringComparison.OrdinalIgnoreCase));

    private static void WriteEntry(ZipArchive archive, string name, byte[] bytes)
    {
        // Callers supply only the fixed manifest entry or validated relative file paths.
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static void ValidateManifest(OverlayManifest manifest)
    {
        if (!string.Equals(manifest.SchemaVersion, OverlayManifest.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("overlay-unsupported-schema");
        }
        if (string.IsNullOrWhiteSpace(manifest.ProductId)
            || string.IsNullOrWhiteSpace(manifest.RepositoryId)
            || string.IsNullOrWhiteSpace(manifest.ToolVersion)
            || string.IsNullOrWhiteSpace(manifest.ToolPackageRelativePath))
        {
            throw new InvalidDataException("overlay-manifest-missing-identity");
        }
        if (manifest.HostMode is not ("clean" or "coexist"))
        {
            throw new InvalidDataException("overlay-invalid-host-mode");
        }

        var paths = new HashSet<string>(PathComparer);
        var roots = new HashSet<string>(PathComparer);
        foreach (var root in manifest.ManagedRoots)
        {
            var normalizedRoot = NormalizeRelativePath(Path.GetTempPath(), root);
            if (!string.Equals(normalizedRoot, root.Replace('\\', '/'), StringComparison.Ordinal)
                || !roots.Add(normalizedRoot))
            {
                throw new InvalidDataException($"overlay-invalid-managed-root:{root}");
            }
        }

        var toolPackagePath = NormalizeRelativePath(Path.GetTempPath(), manifest.ToolPackageRelativePath);
        if (!string.Equals(toolPackagePath, manifest.ToolPackageRelativePath.Replace('\\', '/'), StringComparison.Ordinal)
            || !IsManagedPath(manifest, toolPackagePath))
        {
            throw new InvalidDataException("overlay-invalid-tool-package-path");
        }

        foreach (var file in manifest.Files)
        {
            var normalized = NormalizeRelativePath(Path.GetTempPath(), file.RelativePath);
            if (!string.Equals(normalized, file.RelativePath.Replace('\\', '/'), StringComparison.Ordinal)
                || !paths.Add(normalized)
                || !IsManagedPath(manifest, normalized)
                || file.Length < 0
                || file.Sha256.Length != 64)
            {
                throw new InvalidDataException($"overlay-invalid-file-entry:{file.RelativePath}");
            }
        }
    }
}
