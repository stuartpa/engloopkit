using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using EngLoopKit.Components.Overlay;

namespace EngLoopKit.Tool;

/// <summary>
/// Private overlay installation, verification, pack, and unpack commands. This is the
/// product-specific vertical over the generic <c>Components.Overlay</c> archive/path
/// component. It deliberately uses local Git excludes and hooks; it never edits tracked
/// .gitignore files or an existing ELK/Spec Kit installation.
/// </summary>
public static class OverlayCommands
{
    public static string? LastError { get; private set; }

    private static readonly string[] ManagedRoots =
    [
        ".engloop",
        ".engloop-overlay",
        ".config/dotnet-tools.json",
        ".specify",
        ".github/agents",
        ".github/prompts",
        ".vscode/settings.json",
        "NORTHSTAR.md",
        "LEARNINGS.md",
    ];

    private static readonly string[] ExcludePatterns =
    [
        "/.engloop/",
        "/.engloop-overlay/",
        "/.config/dotnet-tools.json",
        "/.specify/",
        "/.github/agents/",
        "/.github/prompts/",
        "/.vscode/settings.json",
        "/NORTHSTAR.md",
        "/LEARNINGS.md",
    ];

    private static readonly string[] HookNames = ["pre-commit", "pre-push"];

    public static int Execute(string[] args)
    {
        LastError = null;
        if (args.Length == 0)
        {
            LastError = "Usage: engloopkit overlay <install|verify|pack|unpack|status> [options]";
            Console.Error.WriteLine(LastError);
            return 1;
        }

        try
        {
            return args[0] switch
            {
                "install" => Install(args[1..]),
                "verify" => Verify(args[1..]),
                "pack" => Pack(args[1..]),
                "unpack" => Unpack(args[1..]),
                "status" => Status(args[1..]),
                _ => Fail("overlay-invalid-command"),
            };
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Install(string[] args)
    {
        var root = RequireGitRoot(GetOption(args, "--root", "."));
        if (!string.Equals(RequireOption(args, "--mode"), "overlay", StringComparison.Ordinal))
        {
            return Fail("overlay-install-requires-mode-overlay");
        }
        var productId = RequireOption(args, "--product-id");
        var repositoryId = RequireOption(args, "--repository-id");
        var toolVersion = RequireOption(args, "--tool-version");
        var toolNupkg = RequireExistingFile(args, "--tool-nupkg");
        var extensionSource = RequireExistingFile(args, "--extension-archive");

        if (!System.Text.RegularExpressions.Regex.IsMatch(productId, "^[a-z0-9][a-z0-9-]{0,63}$"))
        {
            return Fail("overlay-invalid-product-id");
        }
        PreflightInstall(root);
        var excludePath = GetGitPath(root, "info/exclude");
        var originalExclude = File.Exists(excludePath) ? File.ReadAllText(excludePath) : string.Empty;
        var created = new List<string>();
        try
        {
            WriteOverlayExcludes(excludePath);
            Directory.CreateDirectory(Path.Combine(root, ".engloop-overlay", "packages"));
            Directory.CreateDirectory(Path.Combine(root, ".engloop-overlay", "cache"));

            var packageDestination = Path.Combine(root, ".engloop-overlay", "packages", Path.GetFileName(toolNupkg));
            File.Copy(toolNupkg, packageDestination, overwrite: false);
            created.Add(packageDestination);

            var extensionArchive = MaterializeExtensionArchive(root, extensionSource);
            created.Add(extensionArchive);
            var extensionSourceDirectory = ExtractExtensionSource(root, extensionArchive);
            created.Add(extensionSourceDirectory);

            var toolManifest = Path.Combine(root, ".config", "dotnet-tools.json");
            Directory.CreateDirectory(Path.GetDirectoryName(toolManifest)!);
            File.WriteAllText(toolManifest, "{\"version\":1,\"isRoot\":true,\"tools\":{}}");
            created.Add(toolManifest);
            Run("dotnet", root, "tool", "install", "engloopkit", "--version", toolVersion,
                "--add-source", Path.Combine(root, ".engloop-overlay", "packages"),
                "--tool-manifest", toolManifest, "--no-cache");

            Run("specify", root, "init", "--here", "--force", "--integration", "copilot", "--script", "ps", "--ignore-agent-tools");
            Run("specify", root, "extension", "add", extensionSourceDirectory, "--dev", "--force");
            WaitForGeneratedSurface(root);

            WriteInitialOverlayFiles(root, productId);
            InstallHook(root, "pre-commit", "staged");
            InstallHook(root, "pre-push", "push");

            var manifest = CreateCurrentManifest(root, productId, repositoryId, toolVersion,
                Path.GetRelativePath(root, packageDestination).Replace('\\', '/'),
                ExtensionIdentity(extensionSource, extensionArchive));
            WriteManifest(root, manifest);
            EnsureVerified(root, manifest, "all");
            Console.WriteLine("OVERLAY_INSTALL_PASS");
            return 0;
        }
        catch
        {
            RollbackInstall(root, originalExclude);
            throw;
        }
    }

    private static int Verify(string[] args)
    {
        var root = RequireGitRoot(GetOption(args, "--root", "."));
        var mode = GetOption(args, "--mode", "all");
        var manifest = ReadManifest(root);
        EnsureVerified(root, manifest, mode);
        Console.WriteLine("OVERLAY_VERIFY_PASS");
        return 0;
    }

    private static int Pack(string[] args)
    {
        var root = RequireGitRoot(GetOption(args, "--root", "."));
        var output = RequireOption(args, "--output");
        var manifest = ReadManifest(root);
        // Pack is the explicit checkpoint that refreshes the content manifest after
        // legitimate local ELK work. It still proves every current path is local-only
        // and absent from staged/history leakage before writing a portable archive.
        EnsureProtected(root, manifest, "all");

        var fullOutput = Path.GetFullPath(output);
        var relativeOutput = Path.GetRelativePath(root, fullOutput);
        if (!relativeOutput.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relativeOutput.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            && relativeOutput != "..")
        {
            return Fail("overlay-pack-output-must-be-outside-repository");
        }

        var excluded = new[] { OverlayManifest.ManagedManifestPath };
        var files = OverlayArchive.CaptureStableFiles(root, manifest.ManagedRoots, excluded);
        RejectSecretLikePaths(files);
        var refreshed = manifest with
        {
            // EnsureVerified proves no managed path has entered history since the prior
            // baseline; advancing to the current clean checkout makes later pack/unpack
            // portable across another checkout at this exact working revision.
            BaseRevision = Git(root, "rev-parse", "HEAD").Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Files = files,
        };
        WriteManifest(root, refreshed);
        OverlayArchive.CreateArchive(root, refreshed, fullOutput);
        Console.WriteLine($"OVERLAY_PACK_PASS archive={fullOutput}");
        return 0;
    }

    private static int Unpack(string[] args)
    {
        var root = RequireGitRoot(GetOption(args, "--root", "."));
        var input = RequireExistingFile(args, "--input");
        var repositoryId = RequireOption(args, "--repository-id");
        var manifest = OverlayArchive.ReadAndValidateArchive(input);

        if (!manifest.Files.Any(file => string.Equals(file.RelativePath, ".config/dotnet-tools.json", StringComparison.Ordinal)))
        {
            return Fail("overlay-archive-missing-tool-manifest");
        }
        if (!manifest.Files.Any(file => string.Equals(file.RelativePath, manifest.ToolPackageRelativePath, StringComparison.Ordinal)))
        {
            return Fail("overlay-archive-missing-tool-package");
        }

        if (!string.Equals(manifest.RepositoryId, repositoryId, StringComparison.Ordinal))
        {
            return Fail("overlay-repository-id-mismatch");
        }
        var origin = TryGit(root, "config", "--get", "remote.origin.url");
        if (!string.IsNullOrWhiteSpace(manifest.OriginUrl)
            && !string.Equals(manifest.OriginUrl, origin, StringComparison.Ordinal))
        {
            return Fail("overlay-origin-mismatch");
        }
        var targetRevision = Git(root, "rev-parse", "HEAD").Trim();
        if (!string.Equals(targetRevision, manifest.BaseRevision, StringComparison.Ordinal))
        {
            return Fail("overlay-base-revision-mismatch");
        }

        PreflightInstall(root);
        var excludePath = GetGitPath(root, "info/exclude");
        var originalExclude = File.Exists(excludePath) ? File.ReadAllText(excludePath) : string.Empty;
        try
        {
            WriteOverlayExcludes(excludePath);
            OverlayArchive.ExtractArchive(input, root, manifest);
            WriteManifest(root, manifest);
            InstallHook(root, "pre-commit", "staged");
            InstallHook(root, "pre-push", "push");

            var packageDirectory = Path.GetDirectoryName(Path.Combine(root, manifest.ToolPackageRelativePath))!;
            Run("dotnet", root, "tool", "restore", "--add-source", packageDirectory);
            EnsureVerified(root, manifest, "all");
            Console.WriteLine("OVERLAY_UNPACK_PASS");
            return 0;
        }
        catch
        {
            RollbackInstall(root, originalExclude);
            throw;
        }
    }

    private static int Status(string[] args)
    {
        var root = RequireGitRoot(GetOption(args, "--root", "."));
        var manifest = ReadManifest(root);
        Console.WriteLine(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static OverlayManifest CreateCurrentManifest(
        string root,
        string productId,
        string repositoryId,
        string toolVersion,
        string toolPackageRelativePath,
        string extensionIdentity)
    {
        var files = OverlayArchive.CaptureStableFiles(root, ManagedRoots, [OverlayManifest.ManagedManifestPath]);
        RejectSecretLikePaths(files);
        return new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion,
            productId,
            repositoryId,
            TryGit(root, "config", "--get", "remote.origin.url"),
            Git(root, "rev-parse", "HEAD").Trim(),
            DateTimeOffset.UtcNow,
            ManagedRoots,
            ExcludePatterns,
            HookNames,
            toolVersion,
            toolPackageRelativePath,
            extensionIdentity,
            files);
    }

    private static void EnsureVerified(string root, OverlayManifest manifest, string mode)
    {
        var currentFiles = EnsureProtected(root, manifest, mode);
        if (mode == "all" && !SameFiles(manifest.Files, currentFiles))
        {
            throw new InvalidOperationException("overlay-manifest-file-mismatch");
        }
    }

    private static IReadOnlyList<OverlayFile> EnsureProtected(string root, OverlayManifest manifest, string mode)
    {
        if (mode is not ("all" or "staged" or "push"))
        {
            throw new InvalidOperationException("overlay-invalid-verify-mode");
        }

        var currentFiles = OverlayArchive.CaptureStableFiles(root, manifest.ManagedRoots, [OverlayManifest.ManagedManifestPath]);
        if (!currentFiles.Any(file => string.Equals(file.RelativePath, ".config/dotnet-tools.json", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("overlay-local-tool-manifest-missing");
        }
        if (!currentFiles.Any(file => string.Equals(file.RelativePath, manifest.ToolPackageRelativePath, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("overlay-local-tool-package-missing");
        }

        if (mode is "staged" or "all")
        {
            var staged = Git(root, "diff", "--cached", "--name-only").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in staged)
            {
                if (OverlayArchive.IsManagedPath(manifest, path))
                {
                    throw new InvalidOperationException($"overlay-managed-path-staged:{path}");
                }
            }
        }

        if (mode is "push" or "all")
        {
            var baseExists = Run("git", root, ["cat-file", "-e", manifest.BaseRevision + "^{commit}"], throwOnFailure: false).ExitCode == 0;
            if (!baseExists)
            {
                throw new InvalidOperationException("overlay-base-revision-not-found");
            }

            var history = Git(root, "diff", "--name-only", manifest.BaseRevision + "..HEAD")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in history)
            {
                if (OverlayArchive.IsManagedPath(manifest, path))
                {
                    throw new InvalidOperationException($"overlay-managed-path-in-history:{path}");
                }
            }
        }

        foreach (var managedRoot in manifest.ManagedRoots)
        {
            var tracked = Git(root, "ls-files", "--", managedRoot).Trim();
            if (!string.IsNullOrWhiteSpace(tracked))
            {
                throw new InvalidOperationException($"overlay-managed-path-tracked:{managedRoot}");
            }
        }

        foreach (var file in currentFiles)
        {
            var ignored = Run("git", root, ["check-ignore", "-q", "--", file.RelativePath], throwOnFailure: false).ExitCode == 0;
            if (!ignored)
            {
                throw new InvalidOperationException($"overlay-managed-path-not-ignored:{file.RelativePath}");
            }
        }

        return currentFiles;
    }

    private static void PreflightInstall(string root)
    {
        foreach (var managedRoot in ManagedRoots)
        {
            var full = Path.Combine(root, managedRoot.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full) || Directory.Exists(full))
            {
                throw new InvalidOperationException($"overlay-install-conflict:{managedRoot}");
            }

            var tracked = Git(root, "ls-files", "--", managedRoot).Trim();
            if (!string.IsNullOrWhiteSpace(tracked))
            {
                throw new InvalidOperationException($"overlay-install-tracked-conflict:{managedRoot}");
            }
        }

        foreach (var hook in HookNames)
        {
            var path = GetGitPath(root, "hooks/" + hook);
            if (File.Exists(path) && !File.ReadAllText(path).Contains("ELK_OVERLAY_HOOK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"overlay-hook-conflict:{hook}");
            }
        }
    }

    private static void WriteOverlayExcludes(string excludePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
        var existing = File.Exists(excludePath) ? File.ReadAllText(excludePath) : string.Empty;
        const string begin = "# >>> ELK_OVERLAY_MANAGED >>>";
        const string end = "# <<< ELK_OVERLAY_MANAGED <<<";
        if (existing.Contains(begin, StringComparison.Ordinal))
        {
            return;
        }

        var lines = new List<string> { existing.TrimEnd(), begin };
        lines.AddRange(ExcludePatterns);
        lines.Add(end);
        File.WriteAllText(excludePath, string.Join(Environment.NewLine, lines.Where(line => line is not null)) + Environment.NewLine);
    }

    private static void InstallHook(string root, string hookName, string mode)
    {
        var path = GetGitPath(root, "hooks/" + hookName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var content = $$"""
#!/bin/sh
# ELK_OVERLAY_HOOK
set -eu
ROOT="$(git rev-parse --show-toplevel)"
exec dotnet tool run engloopkit -- overlay verify --root "$ROOT" --mode {{mode}}
""";
        File.WriteAllText(path, content);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static string MaterializeExtensionArchive(string root, string source)
    {
        var destination = Path.Combine(root, ".engloop-overlay", "cache", "extension.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: false);
        return destination;
    }

    private static string ExtractExtensionSource(string root, string archivePath)
    {
        var destination = Path.Combine(root, ".engloop-overlay", "cache", "extension-source");
        if (Directory.Exists(destination))
        {
            throw new InvalidOperationException("overlay-extension-extraction-conflict");
        }
        Directory.CreateDirectory(destination);
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }
            var relative = OverlayArchive.NormalizeRelativePath(destination, entry.FullName);
            var path = Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var input = entry.Open();
            using var output = File.Create(path);
            input.CopyTo(output);
        }

        var manifests = Directory.GetFiles(destination, "extension.yml", SearchOption.AllDirectories);
        if (manifests.Length != 1)
        {
            throw new InvalidDataException("overlay-extension-archive-must-contain-one-extension-manifest");
        }
        return Path.GetDirectoryName(manifests[0])!;
    }

    private static void WriteInitialOverlayFiles(string root, string productId)
    {
        File.WriteAllText(Path.Combine(root, "NORTHSTAR.md"), "# Northstar\n\n- **Status:** overlay-local draft\n- **Product ID:** " + productId + "\n\nUse `/speckit.engloop.01-northstar` to establish evidence-backed direction.\n");
        File.WriteAllText(Path.Combine(root, "LEARNINGS.md"), "# Learnings\n\n- **Status:** overlay-local draft\n\nUse `/speckit.engloop.31-learnings-pyramid` after accepted source learnings exist.\n");

        var config = new
        {
            schemaVersion = "2.0",
            productId,
            artifactRoot = ".engloop",
            transientOutputRoot = ".engloop/out",
            northstarPath = "NORTHSTAR.md",
            validatorCommand = new[] { "dotnet", "tool", "run", "engloopkit", "--" },
            moduleDiscoveryCommand = new[] { "engloopkit", "overlay", "configuration-required", "module-discovery" },
            architectureCommand = new[] { "engloopkit", "overlay", "configuration-required", "architecture" },
            regressionCommand = new[] { "engloopkit", "overlay", "configuration-required", "regression" },
            coverageInputs = new Dictionary<string, string> { ["status"] = "configuration-required" },
            testRunway = new
            {
                status = "unproven",
                framework = (string?)null,
                terseCommand = (string[]?)null,
                boundaryTest = (string?)null,
                generatedDestination = (string?)null,
                evidenceDigest = (string?)null,
                provenAtRevision = (string?)null,
            },
            moduleInventory = Array.Empty<object>(),
            overlayMode = true,
        };
        Directory.CreateDirectory(Path.Combine(root, ".engloop"));
        File.WriteAllText(Path.Combine(root, ".engloop", "config.json"), JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WaitForGeneratedSurface(string root)
    {
        const int attempts = 20;
        const int delayMilliseconds = 150;
        IReadOnlyDictionary<string, string>? prior = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var agentsDirectory = Path.Combine(root, ".github", "agents");
            var promptsDirectory = Path.Combine(root, ".github", "prompts");
            var agents = Directory.Exists(agentsDirectory)
                ? Directory.GetFiles(agentsDirectory, "speckit.engloop.*.agent.md", SearchOption.TopDirectoryOnly)
                : [];
            var prompts = Directory.Exists(promptsDirectory)
                ? Directory.GetFiles(promptsDirectory, "speckit.engloop.*.prompt.md", SearchOption.TopDirectoryOnly)
                : [];

            if (agents.Length == 14 && prompts.Length == 14)
            {
                var snapshot = agents.Concat(prompts)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToDictionary(
                        path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                        path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                        StringComparer.Ordinal);
                if (snapshot.Values.All(hash => !string.Equals(hash, EmptySha256, StringComparison.Ordinal))
                    && prior is not null
                    && prior.Count == snapshot.Count
                    && prior.All(pair => snapshot.TryGetValue(pair.Key, out var current) && current == pair.Value))
                {
                    return;
                }
                prior = snapshot;
            }

            Thread.Sleep(delayMilliseconds);
        }

        throw new InvalidOperationException("overlay-generated-agent-surface-did-not-stabilize");
    }

    private static void WriteManifest(string root, OverlayManifest manifest)
    {
        var path = Path.Combine(root, OverlayManifest.ManagedManifestPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, OverlayArchive.SerializeManifest(manifest));
    }

    private static OverlayManifest ReadManifest(string root)
    {
        var path = Path.Combine(root, OverlayManifest.ManagedManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("overlay-manifest-missing", path);
        }
        return OverlayArchive.ParseManifest(File.ReadAllText(path));
    }

    private static void RollbackInstall(string root, string originalExclude)
    {
        foreach (var managed in ManagedRoots.OrderByDescending(path => path.Length))
        {
            var path = Path.Combine(root, managed.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        var excludePath = GetGitPath(root, "info/exclude");
        File.WriteAllText(excludePath, originalExclude);
        foreach (var hook in HookNames)
        {
            var hookPath = GetGitPath(root, "hooks/" + hook);
            if (File.Exists(hookPath) && File.ReadAllText(hookPath).Contains("ELK_OVERLAY_HOOK", StringComparison.Ordinal))
            {
                File.Delete(hookPath);
            }
        }
    }

    private static void RejectSecretLikePaths(IEnumerable<OverlayFile> files)
    {
        var secret = new System.Text.RegularExpressions.Regex(@"(^|/)(\.env(?:\..*)?|.*\.(pem|key|pfx|p12)|.*(credential|secret|token).*)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var file in files)
        {
            if (secret.IsMatch(file.RelativePath))
            {
                throw new InvalidOperationException($"overlay-secret-like-path-forbidden:{file.RelativePath}");
            }
        }
    }

    private static string ExtensionIdentity(string source, string archive)
        => source + "#sha256=" + OverlayArchive.Sha256File(archive);

    private static bool SameFiles(IReadOnlyList<OverlayFile> expected, IReadOnlyList<OverlayFile> actual)
        => expected.Count == actual.Count
           && expected.Zip(actual).All(pair => string.Equals(pair.First.RelativePath, pair.Second.RelativePath, StringComparison.Ordinal)
               && pair.First.Length == pair.Second.Length
               && string.Equals(pair.First.Sha256, pair.Second.Sha256, StringComparison.OrdinalIgnoreCase));

    private static readonly string EmptySha256 = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant();

    private static string RequireGitRoot(string root)
    {
        var selected = Path.GetFullPath(root);
        var gitRoot = Git(selected, "rev-parse", "--show-toplevel").Trim();
        if (!PathEquals(selected, gitRoot))
        {
            throw new InvalidOperationException("overlay-root-must-be-selected-git-root");
        }
        return gitRoot;
    }

    private static string GetGitPath(string root, string path)
    {
        var result = Git(root, "rev-parse", "--git-path", path).Trim();
        return Path.GetFullPath(result, root);
    }

    private static string Git(string workingDirectory, params string[] args)
    {
        var result = Run("git", workingDirectory, args);
        return result.StandardOutput;
    }

    private static string? TryGit(string workingDirectory, params string[] args)
    {
        var result = Run("git", workingDirectory, args, throwOnFailure: false);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }

    private static ProcessResult Run(string fileName, string workingDirectory, params string[] arguments)
        => Run(fileName, workingDirectory, arguments, throwOnFailure: true);

    private static ProcessResult Run(string fileName, string workingDirectory, string[] arguments, bool throwOnFailure)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"overlay-process-start-failed:{fileName}");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        var result = new ProcessResult(process.ExitCode, stdout, stderr);
        if (throwOnFailure && result.ExitCode != 0)
        {
            throw new InvalidOperationException($"overlay-command-failed:{fileName}:{result.ExitCode}:{stderr.Trim()}");
        }
        return result;
    }

    private static string GetOption(string[] args, string name, string defaultValue)
    {
        var index = Array.FindIndex(args, value => string.Equals(value, name, StringComparison.Ordinal));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : defaultValue;
    }

    private static string RequireOption(string[] args, string name)
    {
        var value = GetOption(args, name, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"overlay-missing-option:{name}");
        }
        return value;
    }

    private static string RequireExistingFile(string[] args, string name)
    {
        var path = Path.GetFullPath(RequireOption(args, name));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"overlay-missing-file:{name}", path);
        }
        return path;
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    private static int Fail(string reason)
    {
        LastError = reason;
        Console.Error.WriteLine(reason);
        return 1;
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
