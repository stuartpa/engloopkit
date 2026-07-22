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

    private static readonly string[] ArchiveRoots =
    [
        ".engloop",
        ".engloop-overlay",
        ".config/dotnet-tools.json",
        "NORTHSTAR.md",
        "LEARNINGS.md",
    ];

    private static readonly string[] CleanOnlyManagedRoots =
    [
        ".specify",
        ".github/agents",
        ".github/prompts",
        ".vscode/settings.json",
    ];

    private static readonly string[] SharedHostPaths =
    [
        ".specify/extensions/.registry",
        ".specify/extensions.yml",
    ];

    private static readonly string[] ArchiveExcludePatterns =
    [
        "/.engloop/",
        "/.engloop-overlay/",
        "/.config/dotnet-tools.json",
        "/NORTHSTAR.md",
        "/LEARNINGS.md",
    ];

    private static readonly string[] HookNames = ["pre-commit", "pre-push"];

    private sealed record DirectorySnapshot(string RelativeDirectory, IReadOnlyDictionary<string, byte[]> Files);
    private sealed record HookSnapshot(string HookName, byte[]? Content, byte[]? PriorContent);
    private sealed record RemovalItem(string RelativePath, string FullPath, bool IsDirectory);

    private static readonly string[] EngLoopCommandIds =
    [
        "speckit.engloop.01-northstar", "speckit.engloop.02-scaffold", "speckit.engloop.03-architect",
        "speckit.engloop.04-refactor", "speckit.engloop.05-model", "speckit.engloop.06-explore",
        "speckit.engloop.07-validate", "speckit.engloop.08-unittest", "speckit.engloop.09-codereview-prepare",
        "speckit.engloop.20-incident", "speckit.engloop.21-postmortem", "speckit.engloop.22-repair",
        "speckit.engloop.30-refactor-scan", "speckit.engloop.31-learnings-pyramid",
        "speckit.engloop.40-pomodoro-create", "speckit.engloop.50-overlay-pack", "speckit.engloop.51-overlay-remove",
    ];

    private static string NormalizeHostMode(string mode)
    {
        if (mode is "clean" or "coexist") return mode;
        throw new InvalidOperationException("overlay-invalid-host-mode");
    }

    private static string[] GetManagedRoots(string hostMode)
    {
        var roots = ArchiveRoots.ToList();
        if (hostMode == "clean")
        {
            roots.AddRange(CleanOnlyManagedRoots);
        }
        else
        {
            roots.Add(".specify/extensions/engloop");
            roots.AddRange(EngLoopCommandIds.Select(id => ".github/agents/" + id + ".agent.md"));
            roots.AddRange(EngLoopCommandIds.Select(id => ".github/prompts/" + id + ".prompt.md"));
        }
        return roots.ToArray();
    }

    private static string[] GetExcludePatterns(string hostMode)
    {
        var patterns = ArchiveExcludePatterns.ToList();
        if (hostMode == "clean")
        {
            patterns.AddRange(["/.specify/", "/.github/agents/", "/.github/prompts/", "/.vscode/settings.json"]);
        }
        else
        {
            patterns.AddRange(SharedHostPaths.Select(path => "/" + path));
            patterns.Add("/.specify/extensions/engloop/");
            patterns.AddRange(EngLoopCommandIds.Select(id => "/.github/agents/" + id + ".agent.md"));
            patterns.AddRange(EngLoopCommandIds.Select(id => "/.github/prompts/" + id + ".prompt.md"));
        }
        return patterns.ToArray();
    }

    public static int Execute(string[] args)
    {
        LastError = null;
        if (args.Length == 0)
        {
            LastError = "Usage: engloopkit overlay <install|register|verify|pack|unpack|remove|status> [options]";
            Console.Error.WriteLine(LastError);
            return 1;
        }

        try
        {
            return args[0] switch
            {
                "install" => Install(args[1..]),
                "register" => Register(args[1..]),
                "verify" => Verify(args[1..]),
                "pack" => Pack(args[1..]),
                "unpack" => Unpack(args[1..]),
                "remove" => Remove(args[1..]),
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

    private static int Remove(string[] args)
    {
        var root = RequireGitRoot(GetOption(args, "--root", "."));
        var manifest = ReadManifest(root);
        var expectedConfirmation = $"REMOVE-OVERLAY:{manifest.RepositoryId}@{manifest.BaseRevision}";
        var confirmation = RequireOption(args, "--confirm");
        if (!string.Equals(confirmation, expectedConfirmation, StringComparison.Ordinal))
        {
            return Fail("overlay-remove-confirmation-mismatch");
        }

        _ = EnsureProtected(root, manifest, "all");
        var items = BuildRemovalPlan(root, manifest);
        var excludePath = GetGitPath(root, "info/exclude");
        var originalExclude = File.Exists(excludePath) ? File.ReadAllText(excludePath) : string.Empty;
        var hookSnapshots = CaptureHookSnapshots(root);
        var quarantine = GetGitPath(root, "elk-overlay-remove-" + Guid.NewGuid().ToString("N"));
        var moved = new List<(string Source, string Quarantine, bool Directory)>();

        PreflightRemovalHooks(root, manifest.HookNames);
        Directory.CreateDirectory(quarantine);
        try
        {
            if (manifest.HostMode == "coexist")
            {
                CaptureSharedHostFilesForRollback(root, quarantine);
                Run("specify", root, "extension", "remove", "engloop", "--force");
                AssertSharedHostFilesPreserved(root, quarantine, manifest);
            }

            foreach (var item in items.Where(item => !string.Equals(item.RelativePath, ".engloop-overlay", StringComparison.OrdinalIgnoreCase)))
            {
                MoveToQuarantine(item.FullPath, Path.Combine(quarantine, "owned", item.RelativePath.Replace('/', Path.DirectorySeparatorChar)), item.IsDirectory, moved);
            }

            RestoreOverlayHooks(root, manifest.HookNames);
            WriteExcludeWithoutManagedBlock(excludePath, originalExclude);

            var overlayItem = items.FirstOrDefault(item => string.Equals(item.RelativePath, ".engloop-overlay", StringComparison.OrdinalIgnoreCase));
            if (overlayItem is not null)
            {
                MoveToQuarantine(overlayItem.FullPath, Path.Combine(quarantine, "owned", ".engloop-overlay"), overlayItem.IsDirectory, moved);
            }

            foreach (var item in items)
            {
                if (File.Exists(item.FullPath) || Directory.Exists(item.FullPath))
                {
                    throw new InvalidOperationException($"overlay-remove-path-remains:{item.RelativePath}");
                }
            }
            if (File.ReadAllText(excludePath).Contains("# >>> ELK_OVERLAY_MANAGED >>>", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("overlay-remove-exclude-block-remains");
            }

            Directory.Delete(quarantine, recursive: true);
            Console.WriteLine("OVERLAY_REMOVE_PASS");
            return 0;
        }
        catch
        {
            RestoreMovedPaths(moved);
            RestoreSharedHostFilesFromQuarantine(root, quarantine);
            RestoreHookSnapshots(root, hookSnapshots);
            Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
            File.WriteAllText(excludePath, originalExclude);
            if (Directory.Exists(quarantine)) Directory.Delete(quarantine, recursive: true);
            throw;
        }
    }

    private static IReadOnlyList<RemovalItem> BuildRemovalPlan(string root, OverlayManifest manifest)
    {
        var normalized = manifest.ManagedRoots
            .Select(path => OverlayArchive.NormalizeRelativePath(root, path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path.Length)
            .ToArray();
        var directoryRoots = normalized
            .Where(path => Directory.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))
                || manifest.ExcludePatterns.Any(pattern => string.Equals(pattern, "/" + path.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var plan = new List<RemovalItem>();
        foreach (var path in normalized)
        {
            if (directoryRoots.Any(parent => !string.Equals(parent, path, StringComparison.OrdinalIgnoreCase)
                && path.StartsWith(parent.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            var isDirectory = directoryRoots.Contains(path, StringComparer.OrdinalIgnoreCase);
            plan.Add(new RemovalItem(path, full, isDirectory));
        }

        return plan.OrderByDescending(item => item.RelativePath.Length).ToArray();
    }

    private static void PreflightRemovalHooks(string root, IReadOnlyList<string> hookNames)
    {
        foreach (var hookName in hookNames)
        {
            var path = GetGitPath(root, "hooks/" + hookName);
            if (!File.Exists(path) || !File.ReadAllText(path).Contains("ELK_OVERLAY_HOOK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"overlay-remove-hook-not-owned:{hookName}");
            }
        }
    }

    private static void MoveToQuarantine(string source, string destination, bool directory, List<(string Source, string Quarantine, bool Directory)> moved)
    {
        try
        {
            if (directory)
            {
                if (!Directory.Exists(source)) return;
                Directory.CreateDirectory(destination);
                foreach (var childDirectory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, childDirectory)));
                }
                foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    var target = Path.Combine(destination, Path.GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Move(file, target);
                }
                foreach (var childDirectory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
                {
                    Directory.Delete(childDirectory);
                }
                Directory.Delete(source);
            }
            else
            {
                if (!File.Exists(source)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(source, destination);
            }
            moved.Add((source, destination, directory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"overlay-remove-move-failed:path={Path.GetRelativePath(Path.GetDirectoryName(source) ?? source, source)};operation={(directory ? "directory-children-first" : "file")};exception={ex.GetType().Name}", ex);
        }
    }

    private static void RestoreMovedPaths(List<(string Source, string Quarantine, bool Directory)> moved)
    {
        foreach (var item in moved.AsEnumerable().Reverse())
        {
            if (item.Directory)
            {
                if (!Directory.Exists(item.Quarantine)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(item.Source)!);
                Directory.Move(item.Quarantine, item.Source);
            }
            else
            {
                if (!File.Exists(item.Quarantine)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(item.Source)!);
                File.Move(item.Quarantine, item.Source);
            }
        }
    }

    private static void RestoreOverlayHooks(string root, IReadOnlyList<string> hookNames)
    {
        foreach (var hookName in hookNames)
        {
            var path = GetGitPath(root, "hooks/" + hookName);
            var priorPath = path + ".elk-prior";
            var baselinePath = GetHookBaselinePath(root, hookName, present: true);
            var absentPath = GetHookBaselinePath(root, hookName, present: false);
            var beforeHash = File.Exists(path) ? Sha256Bytes(File.ReadAllBytes(path)) : "absent";

            if (File.Exists(baselinePath))
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(priorPath)) File.Delete(priorPath);
                File.WriteAllBytes(path, File.ReadAllBytes(baselinePath));
                Console.WriteLine($"OVERLAY_REMOVE_HOOK hook={hookName} state=restored before={beforeHash} after={Sha256Bytes(File.ReadAllBytes(path))}");
            }
            else if (File.Exists(absentPath))
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(priorPath)) File.Delete(priorPath);
                Console.WriteLine($"OVERLAY_REMOVE_HOOK hook={hookName} state=removed-no-prior before={beforeHash} after=absent");
            }
            else if (File.Exists(priorPath))
            {
                if (File.Exists(path)) File.Delete(path);
                File.Move(priorPath, path);
                Console.WriteLine($"OVERLAY_REMOVE_HOOK hook={hookName} state=restored-legacy-prior before={beforeHash} after={Sha256Bytes(File.ReadAllBytes(path))}");
            }
            else
            {
                // A schema-1.0 overlay cannot prove whether an existing ELK wrapper
                // predated this installation. Preserve it rather than silently weakening
                // repository protection; a subsequent install may replace it safely.
                Console.WriteLine($"OVERLAY_REMOVE_HOOK hook={hookName} state=preserved-legacy-wrapper before={beforeHash} after={beforeHash}");
            }
        }
    }

    private static void WriteHookBaselines(string root, IReadOnlyList<HookSnapshot> snapshots)
    {
        var directory = Path.Combine(root, ".engloop-overlay", "hooks");
        Directory.CreateDirectory(directory);
        foreach (var snapshot in snapshots)
        {
            var baselinePath = GetHookBaselinePath(root, snapshot.HookName, present: true);
            var absentPath = GetHookBaselinePath(root, snapshot.HookName, present: false);
            if (File.Exists(baselinePath)) File.Delete(baselinePath);
            if (File.Exists(absentPath)) File.Delete(absentPath);
            if (snapshot.Content is null)
            {
                File.WriteAllText(absentPath, "absent");
            }
            else
            {
                File.WriteAllBytes(baselinePath, snapshot.Content);
            }
        }
    }

    private static string GetHookBaselinePath(string root, string hookName, bool present)
        => Path.Combine(root, ".engloop-overlay", "hooks", hookName + (present ? ".before" : ".absent"));

    private static string Sha256Bytes(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void WriteExcludeWithoutManagedBlock(string excludePath, string original)
    {
        const string begin = "# >>> ELK_OVERLAY_MANAGED >>>";
        const string end = "# <<< ELK_OVERLAY_MANAGED <<<";
        var normalized = original.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var beginIndex = normalized.IndexOf(begin, StringComparison.Ordinal);
        if (beginIndex < 0) throw new InvalidOperationException("overlay-remove-exclude-block-missing");
        var endIndex = normalized.IndexOf(end, beginIndex, StringComparison.Ordinal);
        if (endIndex < 0) throw new InvalidOperationException("overlay-exclude-block-malformed");
        var before = normalized[..beginIndex].Trim();
        var after = normalized[(endIndex + end.Length)..].Trim();
        var remaining = string.Join(Environment.NewLine, new[] { before, after }.Where(part => part.Length > 0));
        Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
        File.WriteAllText(excludePath, remaining.Length == 0 ? string.Empty : remaining + Environment.NewLine);
    }

    private static void CaptureSharedHostFilesForRollback(string root, string quarantine)
    {
        foreach (var relative in new[] { ".specify", ".github/agents", ".github/prompts" })
        {
            var source = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(source)) continue;
            CopyDirectory(source, Path.Combine(quarantine, "shared", relative.Replace('/', Path.DirectorySeparatorChar)));
        }
    }

    private static void AssertSharedHostFilesPreserved(string root, string quarantine, OverlayManifest manifest)
    {
        var sharedRoot = Path.Combine(quarantine, "shared");
        if (!Directory.Exists(sharedRoot)) return;
        foreach (var backup in Directory.GetFiles(sharedRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sharedRoot, backup).Replace('\\', '/');
            if (OverlayArchive.IsManagedPath(manifest, relative) || SharedHostPaths.Contains(relative, StringComparer.OrdinalIgnoreCase)) continue;
            var current = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(current) || !File.ReadAllBytes(current).SequenceEqual(File.ReadAllBytes(backup)))
            {
                throw new InvalidOperationException($"overlay-remove-shared-host-file-changed:{relative}");
            }
        }
    }

    private static void RestoreSharedHostFilesFromQuarantine(string root, string quarantine)
    {
        var sharedRoot = Path.Combine(quarantine, "shared");
        if (!Directory.Exists(sharedRoot)) return;
        foreach (var relative in new[] { ".specify", ".github/agents", ".github/prompts" })
        {
            var backup = Path.Combine(sharedRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(backup)) continue;
            var destination = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
            CopyDirectory(backup, destination);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static int Register(string[] args)
    {
        var root = RequireGitRoot(GetOption(args, "--root", "."));
        var directories = GetOptions(args, "--directory");
        var files = GetOptions(args, "--file");
        if (directories.Count == 0 && files.Count == 0)
        {
            return Fail("overlay-register-requires-path");
        }

        var manifestPath = Path.Combine(root, OverlayManifest.ManagedManifestPath.Replace('/', Path.DirectorySeparatorChar));
        var manifestText = File.ReadAllText(manifestPath);
        var manifest = OverlayArchive.ParseManifest(manifestText);
        _ = EnsureProtected(root, manifest, "all");

        var registrations = directories.Select(path => (Path: NormalizeRegisteredPath(root, path), Directory: true))
            .Concat(files.Select(path => (Path: NormalizeRegisteredPath(root, path), Directory: false)))
            .DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        RejectRegisteredLeakage(root, manifest.BaseRevision, registrations.Select(item => item.Path));

        var managedRoots = manifest.ManagedRoots
            .Concat(registrations.Select(item => item.Path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var excludePatterns = manifest.ExcludePatterns
            .Concat(registrations.Select(item => "/" + item.Path.TrimEnd('/') + (item.Directory ? "/" : string.Empty)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var updated = manifest with
        {
            ManagedRoots = managedRoots,
            ExcludePatterns = excludePatterns,
        };

        var excludePath = GetGitPath(root, "info/exclude");
        var excludeText = File.Exists(excludePath) ? File.ReadAllText(excludePath) : string.Empty;
        try
        {
            WriteManifest(root, updated);
            WriteManagedExcludeBlock(excludePath, excludePatterns);
            foreach (var registration in registrations)
            {
                var probe = registration.Directory ? registration.Path.TrimEnd('/') + "/.elk-overlay-probe" : registration.Path;
                var ignored = Run("git", root, ["check-ignore", "-q", "--no-index", "--", probe], throwOnFailure: false).ExitCode == 0;
                if (!ignored)
                {
                    throw new InvalidOperationException($"overlay-register-path-not-ignored:{registration.Path}");
                }
            }
            _ = EnsureProtected(root, updated, "all");
        }
        catch
        {
            File.WriteAllText(manifestPath, manifestText);
            Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
            File.WriteAllText(excludePath, excludeText);
            throw;
        }

        Console.WriteLine("OVERLAY_REGISTER_PASS paths=" + string.Join(',', registrations.Select(item => item.Path)));
        return 0;
    }

    private static int Install(string[] args)
    {
        var root = RequireGitRoot(GetOption(args, "--root", "."));
        if (!string.Equals(RequireOption(args, "--mode"), "overlay", StringComparison.Ordinal))
        {
            return Fail("overlay-install-requires-mode-overlay");
        }
        var hostMode = NormalizeHostMode(GetOption(args, "--host-mode", "clean"));
        var productId = RequireOption(args, "--product-id");
        var repositoryId = RequireOption(args, "--repository-id");
        var toolVersion = RequireOption(args, "--tool-version");
        var toolNupkg = RequireExistingFile(args, "--tool-nupkg");
        var extensionSource = RequireExistingFile(args, "--extension-archive");

        if (!System.Text.RegularExpressions.Regex.IsMatch(productId, "^[a-z0-9][a-z0-9-]{0,63}$"))
        {
            return Fail("overlay-invalid-product-id");
        }
        PreflightInstall(root, hostMode);
        var excludePath = GetGitPath(root, "info/exclude");
        var originalExclude = File.Exists(excludePath) ? File.ReadAllText(excludePath) : string.Empty;
        var existingAgentSnapshot = CaptureDirectorySnapshot(root, ".github/agents");
        var existingPromptSnapshot = CaptureDirectorySnapshot(root, ".github/prompts");
        var existingSpecKitSnapshot = CaptureDirectorySnapshot(root, ".specify");
        var hookSnapshots = CaptureHookSnapshots(root);
        var created = new List<string>();
        try
        {
            WriteOverlayExcludes(excludePath, hostMode);
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

            if (hostMode == "clean")
            {
                Run("specify", root, "init", "--here", "--force", "--integration", "copilot", "--script", "ps", "--ignore-agent-tools");
            }
            else
            {
                RequireExistingSpecKitHost(root);
            }
            Run("specify", root, "extension", "add", extensionSourceDirectory, "--dev", "--force");
            WaitForGeneratedSurface(root);
            AssertSnapshotPreserved(root, existingAgentSnapshot);
            AssertSnapshotPreserved(root, existingPromptSnapshot);

            WriteInitialOverlayFiles(root, productId);
            WriteHookBaselines(root, hookSnapshots);
            InstallHook(root, "pre-commit", "staged", hostMode);
            InstallHook(root, "pre-push", "push", hostMode);

            var manifest = CreateCurrentManifest(root, hostMode, productId, repositoryId, toolVersion,
                Path.GetRelativePath(root, packageDestination).Replace('\\', '/'),
                ExtensionIdentity(extensionSource, extensionArchive));
            WriteManifest(root, manifest);
            EnsureVerified(root, manifest, "all");
            Console.WriteLine("OVERLAY_INSTALL_PASS");
            return 0;
        }
        catch
        {
            RollbackInstall(root, originalExclude, hostMode);
            RemoveNewDirectoryFiles(root, existingSpecKitSnapshot);
            RestoreDirectorySnapshot(root, existingAgentSnapshot);
            RestoreDirectorySnapshot(root, existingPromptSnapshot);
            RestoreDirectorySnapshot(root, existingSpecKitSnapshot);
            RestoreHookSnapshots(root, hookSnapshots);
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

        var hostMode = NormalizeHostMode(manifest.HostMode);
        PreflightInstall(root, hostMode);
        var excludePath = GetGitPath(root, "info/exclude");
        var originalExclude = File.Exists(excludePath) ? File.ReadAllText(excludePath) : string.Empty;
        var existingAgentSnapshot = CaptureDirectorySnapshot(root, ".github/agents");
        var existingPromptSnapshot = CaptureDirectorySnapshot(root, ".github/prompts");
        var existingSpecKitSnapshot = CaptureDirectorySnapshot(root, ".specify");
        var hookSnapshots = CaptureHookSnapshots(root);
        try
        {
            WriteManagedExcludeBlock(excludePath, manifest.ExcludePatterns);
            OverlayArchive.ExtractArchive(input, root, manifest);
            WriteManifest(root, manifest);
            WriteHookBaselines(root, hookSnapshots);
            if (hostMode == "coexist")
            {
                RequireExistingSpecKitHost(root);
                var sourceDirectory = Path.Combine(root, ".engloop-overlay", "cache", "extension-source");
                if (!Directory.Exists(sourceDirectory))
                {
                    throw new InvalidOperationException("overlay-archive-missing-extension-source");
                }
                Run("specify", root, "extension", "add", sourceDirectory, "--dev", "--force");
                WaitForGeneratedSurface(root);
                AssertSnapshotPreserved(root, existingAgentSnapshot);
                AssertSnapshotPreserved(root, existingPromptSnapshot);
            }
            InstallHook(root, "pre-commit", "staged", hostMode);
            InstallHook(root, "pre-push", "push", hostMode);

            var packageDirectory = Path.GetDirectoryName(Path.Combine(root, manifest.ToolPackageRelativePath))!;
            Run("dotnet", root, "tool", "restore", "--add-source", packageDirectory);
            EnsureVerified(root, manifest, "all");
            Console.WriteLine("OVERLAY_UNPACK_PASS");
            return 0;
        }
        catch
        {
            RollbackInstall(root, originalExclude, hostMode);
            RemoveNewDirectoryFiles(root, existingSpecKitSnapshot);
            RestoreDirectorySnapshot(root, existingAgentSnapshot);
            RestoreDirectorySnapshot(root, existingPromptSnapshot);
            RestoreDirectorySnapshot(root, existingSpecKitSnapshot);
            RestoreHookSnapshots(root, hookSnapshots);
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
        string hostMode,
        string productId,
        string repositoryId,
        string toolVersion,
        string toolPackageRelativePath,
        string extensionIdentity)
    {
        var files = OverlayArchive.CaptureStableFiles(root, GetManagedRoots(hostMode), [OverlayManifest.ManagedManifestPath]);
        RejectSecretLikePaths(files);
        return new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion,
            productId,
            repositoryId,
            TryGit(root, "config", "--get", "remote.origin.url"),
            Git(root, "rev-parse", "HEAD").Trim(),
            DateTimeOffset.UtcNow,
            GetManagedRoots(hostMode),
            GetExcludePatterns(hostMode),
            HookNames,
            toolVersion,
            toolPackageRelativePath,
            extensionIdentity,
            files,
            hostMode);
    }

    private static void EnsureVerified(string root, OverlayManifest manifest, string mode)
    {
        _ = EnsureProtected(root, manifest, mode);
        if (mode == "all")
        {
            var currentSnapshot = OverlayArchive.CaptureStableFiles(root, manifest.Files.Select(file => file.RelativePath));
            if (!SameFiles(manifest.Files, currentSnapshot))
            {
                throw new InvalidOperationException("overlay-manifest-file-mismatch");
            }
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

            var history = GetHistoryPaths(root, manifest.BaseRevision);
            foreach (var path in history)
            {
                if (OverlayArchive.IsManagedPath(manifest, path))
                {
                    throw new InvalidOperationException($"overlay-managed-path-in-history:{path}");
                }
            }
        }

        var trackedPaths = Git(root, "ls-files").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var tracked in trackedPaths)
        {
            if (OverlayArchive.IsManagedPath(manifest, tracked))
            {
                throw new InvalidOperationException($"overlay-managed-path-tracked:{tracked}");
            }
        }

        foreach (var managedRoot in manifest.ManagedRoots)
        {
            var directoryPattern = "/" + managedRoot.TrimEnd('/') + "/";
            var isDirectory = manifest.ExcludePatterns.Any(pattern => string.Equals(pattern, directoryPattern, StringComparison.OrdinalIgnoreCase));
            var probe = isDirectory ? managedRoot.TrimEnd('/') + "/.elk-overlay-probe" : managedRoot;
            var ignored = Run("git", root, ["check-ignore", "-q", "--no-index", "--", probe], throwOnFailure: false).ExitCode == 0;
            if (!ignored)
            {
                throw new InvalidOperationException($"overlay-managed-root-not-ignored:{managedRoot}");
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

    private static void PreflightInstall(string root, string hostMode)
    {
        foreach (var managedRoot in GetManagedRoots(hostMode))
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

        if (hostMode == "coexist")
        {
            RequireExistingSpecKitHost(root);
            foreach (var sharedPath in SharedHostPaths)
            {
                var tracked = Git(root, "ls-files", "--", sharedPath).Trim();
                if (!string.IsNullOrWhiteSpace(tracked))
                {
                    throw new InvalidOperationException($"overlay-coexist-tracked-host-config:{sharedPath}");
                }
            }
        }
        else
        {
            foreach (var hook in HookNames)
            {
                var path = GetGitPath(root, "hooks/" + hook);
                if (File.Exists(path) && !File.ReadAllText(path).Contains("ELK_OVERLAY_HOOK", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"overlay-hook-conflict:{hook}");
                }
            }
        }
    }

    private static void RequireExistingSpecKitHost(string root)
    {
        var hostRoot = Path.Combine(root, ".specify");
        if (!Directory.Exists(hostRoot))
        {
            throw new InvalidOperationException("overlay-coexist-requires-spec-kit-host");
        }
    }

    private static void WriteOverlayExcludes(string excludePath, string hostMode)
        => WriteManagedExcludeBlock(excludePath, GetExcludePatterns(hostMode));

    private static void WriteManagedExcludeBlock(string excludePath, IReadOnlyList<string> patterns)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
        var existing = File.Exists(excludePath) ? File.ReadAllText(excludePath) : string.Empty;
        const string begin = "# >>> ELK_OVERLAY_MANAGED >>>";
        const string end = "# <<< ELK_OVERLAY_MANAGED <<<";
        var beginIndex = existing.IndexOf(begin, StringComparison.Ordinal);
        if (beginIndex >= 0)
        {
            var endIndex = existing.IndexOf(end, beginIndex, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                throw new InvalidOperationException("overlay-exclude-block-malformed");
            }
            existing = existing.Remove(beginIndex, endIndex + end.Length - beginIndex);
        }

        var lines = new List<string>();
        var prefix = existing.Trim();
        if (prefix.Length > 0) lines.Add(prefix);
        lines.Add(begin);
        lines.AddRange(patterns.Distinct(StringComparer.OrdinalIgnoreCase));
        lines.Add(end);
        File.WriteAllText(excludePath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static string NormalizeRegisteredPath(string root, string path)
    {
        var normalized = OverlayArchive.NormalizeRelativePath(root, path);
        if (string.Equals(normalized, ".git", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".git/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("overlay-register-git-path-forbidden");
        }
        return normalized;
    }

    private static void RejectRegisteredLeakage(string root, string baseRevision, IEnumerable<string> registeredPaths)
    {
        var probe = new OverlayManifest(
            OverlayManifest.CurrentSchemaVersion, "registration-probe", "registration-probe", null,
            baseRevision, DateTimeOffset.UtcNow, registeredPaths.ToArray(), [], [], "registration-probe",
            registeredPaths.First(), "registration-probe", []);

        var tracked = Git(root, "ls-files").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var history = GetHistoryPaths(root, baseRevision);
        foreach (var path in tracked)
        {
            if (OverlayArchive.IsManagedPath(probe, path)) throw new InvalidOperationException($"overlay-register-path-tracked:{path}");
        }
        foreach (var path in history)
        {
            if (OverlayArchive.IsManagedPath(probe, path)) throw new InvalidOperationException($"overlay-register-path-in-history:{path}");
        }
    }

    private static void InstallHook(string root, string hookName, string mode, string hostMode)
    {
        var path = GetGitPath(root, "hooks/" + hookName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var priorPath = path + ".elk-prior";
        if (File.Exists(path) && !File.ReadAllText(path).Contains("ELK_OVERLAY_HOOK", StringComparison.Ordinal))
        {
            if (hostMode != "coexist")
            {
                throw new InvalidOperationException($"overlay-hook-conflict:{hookName}");
            }
            if (File.Exists(priorPath))
            {
                throw new InvalidOperationException($"overlay-hook-chain-conflict:{hookName}");
            }
            File.Move(path, priorPath);
        }

        var priorInvocation = File.Exists(priorPath)
            ? "\"$HOOK_DIR/" + Path.GetFileName(priorPath) + "\" \"$@\"\n"
            : string.Empty;
        var content = $$"""
#!/bin/sh
# ELK_OVERLAY_HOOK
set -eu
ROOT="$(git rev-parse --show-toplevel)"
HOOK_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
{{priorInvocation}}exec dotnet tool run engloopkit -- overlay verify --root "$ROOT" --mode {{mode}}
""";
        File.WriteAllText(path, content);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static DirectorySnapshot CaptureDirectorySnapshot(string root, string relativeDirectory)
    {
        var full = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(full))
        {
            foreach (var file in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
            {
                files[Path.GetRelativePath(full, file).Replace('\\', '/')] = File.ReadAllBytes(file);
            }
        }
        return new DirectorySnapshot(relativeDirectory, files);
    }

    private static void AssertSnapshotPreserved(string root, DirectorySnapshot snapshot)
    {
        var full = Path.Combine(root, snapshot.RelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        foreach (var (relative, expected) in snapshot.Files)
        {
            var path = Path.Combine(full, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(expected))
            {
                throw new InvalidOperationException($"overlay-shared-host-file-changed:{snapshot.RelativeDirectory}/{relative}");
            }
        }
    }

    private static void RestoreDirectorySnapshot(string root, DirectorySnapshot snapshot)
    {
        var full = Path.Combine(root, snapshot.RelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        foreach (var (relative, content) in snapshot.Files)
        {
            var path = Path.Combine(full, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
        }
    }

    private static void RemoveNewDirectoryFiles(string root, DirectorySnapshot snapshot)
    {
        var full = Path.Combine(root, snapshot.RelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(full)) return;
        foreach (var path in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(full, path).Replace('\\', '/');
            if (!snapshot.Files.ContainsKey(relative))
            {
                File.Delete(path);
            }
        }
    }

    private static IReadOnlyList<HookSnapshot> CaptureHookSnapshots(string root)
        => HookNames.Select(hook =>
        {
            var path = GetGitPath(root, "hooks/" + hook);
            var priorPath = path + ".elk-prior";
            return new HookSnapshot(
                hook,
                File.Exists(path) ? File.ReadAllBytes(path) : null,
                File.Exists(priorPath) ? File.ReadAllBytes(priorPath) : null);
        }).ToArray();

    private static void RestoreHookSnapshots(string root, IReadOnlyList<HookSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var path = GetGitPath(root, "hooks/" + snapshot.HookName);
            var priorPath = path + ".elk-prior";
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(priorPath)) File.Delete(priorPath);
            if (snapshot.Content is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, snapshot.Content);
            }
            if (snapshot.PriorContent is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(priorPath)!);
                File.WriteAllBytes(priorPath, snapshot.PriorContent);
            }
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

            var expectedAgents = EngLoopCommandIds.Select(id => id + ".agent.md").OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var expectedPrompts = EngLoopCommandIds.Select(id => id + ".prompt.md").OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var actualAgents = agents.Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var actualPrompts = prompts.Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();

            if (actualAgents.SequenceEqual(expectedAgents, StringComparer.Ordinal)
                && actualPrompts.SequenceEqual(expectedPrompts, StringComparer.Ordinal))
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

    private static void RollbackInstall(string root, string originalExclude, string hostMode)
    {
        foreach (var managed in GetManagedRoots(hostMode).OrderByDescending(path => path.Length))
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

    private static string[] GetHistoryPaths(string root, string baseRevision)
        => Git(root, "log", "--format=", "--name-only", baseRevision + "..HEAD")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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

    private static IReadOnlyList<string> GetOptions(string[] args, string name)
    {
        var values = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], name, StringComparison.Ordinal)) continue;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"overlay-missing-option:{name}");
            }
            values.Add(args[++index]);
        }
        return values;
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
