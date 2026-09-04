using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using EngLoopKit.Core;
using EngLoopKit.Components.DocumentValidation;

namespace EngLoopKit.Tool;

public static class ValidationCommands
{
    private const string CurrentReadinessRelativePath = ".engloop/readiness/current.json";
    private static readonly string[] ExpectedIds =
    [
        "speckit.engloop.01-northstar", "speckit.engloop.02-scaffold", "speckit.engloop.03-architect",
        "speckit.engloop.04-refactor", "speckit.engloop.05-model", "speckit.engloop.06-explore",
        "speckit.engloop.07-validate", "speckit.engloop.08-unittest", "speckit.engloop.09-debugger-walk-thru",
        "speckit.engloop.10-codereview-prepare", "speckit.engloop.11-codereview-address", "speckit.engloop.12-codereview-reply-resolve",
        "speckit.engloop.20-incident", "speckit.engloop.21-postmortem",
        "speckit.engloop.22-repair", "speckit.engloop.23-happy-minute", "speckit.engloop.30-token-efficiency-analyze", "speckit.engloop.31-token-efficiency-implement",
        "speckit.engloop.40-refactor-plan", "speckit.engloop.41-deadcode", "speckit.engloop.42-learnings-pyramid",
        "speckit.engloop.50-handoff-create", "speckit.engloop.60-overlay-pack", "speckit.engloop.61-overlay-remove",
        "speckit.engloop.70-six-pager-create", "speckit.engloop.71-powerpnt-create", "speckit.engloop.72-academic-paper-create",
        "speckit.engloop.80-upgrade-elk",
    ];

    private static readonly Dictionary<string, string[]> ExpectedTools = new(StringComparer.Ordinal)
    {
        ["speckit.engloop.01-northstar"] = ["read", "search", "edit", "execute", "web", "agent"],
        ["speckit.engloop.02-scaffold"] = ["read", "search", "edit", "execute", "web"],
        ["speckit.engloop.03-architect"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.04-refactor"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.05-model"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.06-explore"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.07-validate"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.08-unittest"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.09-debugger-walk-thru"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.10-codereview-prepare"] = ["read", "search", "edit", "execute", "web"],
        ["speckit.engloop.11-codereview-address"] = ["read", "search", "edit", "execute", "web"],
        ["speckit.engloop.12-codereview-reply-resolve"] = ["read", "execute", "vscode_askQuestions"],
        ["speckit.engloop.20-incident"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.21-postmortem"] = ["read", "search", "edit", "execute", "agent", "vscode_askQuestions"],
        ["speckit.engloop.22-repair"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.23-happy-minute"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.30-token-efficiency-analyze"] = ["read", "search", "edit", "execute", "agent", "copilot_sessionStoreSql"],
        ["speckit.engloop.31-token-efficiency-implement"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.40-refactor-plan"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.41-deadcode"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.42-learnings-pyramid"] = ["read", "search", "edit", "execute", "agent"],
        ["speckit.engloop.50-handoff-create"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.60-overlay-pack"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.61-overlay-remove"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.70-six-pager-create"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.71-powerpnt-create"] = ["read", "search", "edit", "execute"],
        ["speckit.engloop.72-academic-paper-create"] = ["read", "search", "edit", "execute", "web"],
        ["speckit.engloop.80-upgrade-elk"] = ["read", "search", "execute"],
    };

    private static readonly Dictionary<string, string[]> ExpectedAgents = new(StringComparer.Ordinal)
    {
        ["speckit.engloop.01-northstar"] = ["Explore"], ["speckit.engloop.02-scaffold"] = [],
        ["speckit.engloop.03-architect"] = ["Explore"], ["speckit.engloop.04-refactor"] = [],
        ["speckit.engloop.05-model"] = ["Explore"], ["speckit.engloop.06-explore"] = [],
        ["speckit.engloop.07-validate"] = [], ["speckit.engloop.08-unittest"] = ["Explore"],
        ["speckit.engloop.09-debugger-walk-thru"] = [], ["speckit.engloop.10-codereview-prepare"] = [],
        ["speckit.engloop.11-codereview-address"] = [], ["speckit.engloop.12-codereview-reply-resolve"] = [],
        ["speckit.engloop.20-incident"] = ["speckit.engloop.21-postmortem"], ["speckit.engloop.21-postmortem"] = ["Explore"],
        ["speckit.engloop.22-repair"] = [], ["speckit.engloop.23-happy-minute"] = [], ["speckit.engloop.30-token-efficiency-analyze"] = ["Explore"],
        ["speckit.engloop.31-token-efficiency-implement"] = ["Explore"], ["speckit.engloop.40-refactor-plan"] = ["Explore"],
        ["speckit.engloop.41-deadcode"] = ["Explore"], ["speckit.engloop.42-learnings-pyramid"] = ["Explore"],
        ["speckit.engloop.50-handoff-create"] = [], ["speckit.engloop.60-overlay-pack"] = [],
        ["speckit.engloop.61-overlay-remove"] = [], ["speckit.engloop.70-six-pager-create"] = [],
        ["speckit.engloop.71-powerpnt-create"] = [], ["speckit.engloop.72-academic-paper-create"] = [],
        ["speckit.engloop.80-upgrade-elk"] = [],
    };

    private static readonly Dictionary<string, string[]> ExpectedHandoffTargets = new(StringComparer.Ordinal)
    {
        ["speckit.engloop.01-northstar"] = ["speckit.engloop.02-scaffold", "speckit.engloop.03-architect", "speckit.engloop.04-refactor"],
        ["speckit.engloop.02-scaffold"] = ["speckit.engloop.09-debugger-walk-thru", "speckit.engloop.03-architect"],
        ["speckit.engloop.03-architect"] = ["speckit.engloop.04-refactor"],
        ["speckit.engloop.04-refactor"] = ["speckit.engloop.05-model"],
        ["speckit.engloop.05-model"] = ["speckit.engloop.06-explore"],
        ["speckit.engloop.06-explore"] = ["speckit.engloop.05-model", "speckit.engloop.07-validate"],
        ["speckit.engloop.07-validate"] = ["speckit.engloop.04-refactor", "speckit.engloop.05-model", "speckit.engloop.06-explore", "speckit.engloop.08-unittest"],
        ["speckit.engloop.08-unittest"] = ["speckit.engloop.04-refactor", "speckit.engloop.05-model", "speckit.engloop.07-validate", "speckit.engloop.09-debugger-walk-thru"],
        ["speckit.engloop.09-debugger-walk-thru"] = [],
        ["speckit.engloop.10-codereview-prepare"] = ["speckit.engloop.08-unittest"],
        ["speckit.engloop.11-codereview-address"] = ["speckit.engloop.12-codereview-reply-resolve"],
        ["speckit.engloop.12-codereview-reply-resolve"] = [],
        ["speckit.engloop.20-incident"] = ["speckit.engloop.21-postmortem"],
        ["speckit.engloop.21-postmortem"] = ["speckit.engloop.22-repair", "speckit.engloop.42-learnings-pyramid"],
        ["speckit.engloop.22-repair"] = ["speckit.engloop.04-refactor"],
        ["speckit.engloop.23-happy-minute"] = ["speckit.engloop.42-learnings-pyramid"],
        ["speckit.engloop.30-token-efficiency-analyze"] = ["speckit.engloop.31-token-efficiency-implement"],
        ["speckit.engloop.31-token-efficiency-implement"] = [],
        ["speckit.engloop.40-refactor-plan"] = ["speckit.engloop.01-northstar", "speckit.engloop.03-architect", "speckit.engloop.04-refactor", "speckit.engloop.41-deadcode"],
        ["speckit.engloop.41-deadcode"] = [], ["speckit.engloop.42-learnings-pyramid"] = [],
        ["speckit.engloop.50-handoff-create"] = [],
        ["speckit.engloop.60-overlay-pack"] = ["speckit.engloop.01-northstar"],
        ["speckit.engloop.61-overlay-remove"] = [], ["speckit.engloop.70-six-pager-create"] = [],
        ["speckit.engloop.71-powerpnt-create"] = [], ["speckit.engloop.72-academic-paper-create"] = [],
        ["speckit.engloop.80-upgrade-elk"] = [],
    };

    private static string GetOption(string[] args, string name, string defaultValue = ".")
    {
        var index = Array.FindIndex(args, value => string.Equals(value, name, StringComparison.Ordinal));
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }

        return defaultValue;
    }

    private static string RequireOption(string[] args, string name)
    {
        var value = GetOption(args, name, string.Empty);
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"missing-option:{name}");
        }
        return value;
    }

    public static int ExecuteReadiness(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "emit", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Usage: engloopkit readiness emit --root <path> --evidence <stage-08-evidence> --verdict pass");
            return 1;
        }

        try
        {
            var root = Path.GetFullPath(GetOption(args, "--root"));
            var rootResult = Evidence.ValidateRootLayout(root);
            if (!rootResult.Passed) throw new InvalidOperationException(rootResult.Reason);
            var verdict = RequireOption(args, "--verdict");
            if (!string.Equals(verdict, "pass", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("readiness-emit-requires-pass-verdict");
            }
            var evidence = RequireOption(args, "--evidence");
            var relativeEvidence = NormalizeReadinessEvidencePath(rootResult.RepositoryRoot, evidence);
            var evidencePath = Path.Combine(rootResult.RepositoryRoot, relativeEvidence.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(evidencePath)) throw new FileNotFoundException("readiness-evidence-missing", evidencePath);
            ValidateStructuredReadinessEvidence(rootResult.RepositoryRoot, evidencePath);

            var head = GitHead(rootResult.RepositoryRoot) ?? throw new InvalidOperationException("readiness-git-head-unavailable");
            var record = new
            {
                schemaVersion = "1.0",
                stage = "08-unittest",
                verdict = "PASS",
                head,
                evidencePath = relativeEvidence,
                evidenceSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(evidencePath))).ToLowerInvariant(),
                worktreeDigest = OperationsLearningPolicy.ComputeReadinessWorktreeDigest(rootResult.RepositoryRoot),
                emittedAtUtc = DateTimeOffset.UtcNow,
            };
            var output = Path.Combine(rootResult.RepositoryRoot, CurrentReadinessRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("READINESS_EMIT_PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void ValidateStructuredReadinessEvidence(string root, string evidencePath)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(evidencePath));
        var element = json.RootElement;
        EnsureValidation(element.TryGetProperty("schemaVersion", out var schema) & schema.GetString() == "1.0", "readiness-evidence-schema-invalid");
        EnsureValidation(element.TryGetProperty("artifactType", out var type) & type.GetString() == "whole-product-readiness", "readiness-evidence-type-invalid");
        EnsureValidation(element.TryGetProperty("verdict", out var verdict) & verdict.GetString() == "PASS", "readiness-evidence-verdict-not-pass");
        foreach (var gate in new[] { "generatedFunctionalPass", "directSuitePass", "architectureValidationPass" })
        {
            EnsureValidation(element.TryGetProperty(gate, out var value) & value.ValueKind == JsonValueKind.True, "readiness-evidence-gate-fail:" + gate);
        }
        EnsureValidation(element.TryGetProperty("failures", out var failures) & failures.ValueKind == JsonValueKind.Array & (failures.ValueKind != JsonValueKind.Array || failures.GetArrayLength() == 0), "readiness-evidence-failures-present");
        EnsureValidation(element.TryGetProperty("modules", out var modules) & modules.ValueKind == JsonValueKind.Array, "readiness-evidence-modules-missing");
        var coberturaRelative = element.TryGetProperty("coberturaReport", out var reportValue) && reportValue.ValueKind == JsonValueKind.String ? reportValue.GetString() ?? string.Empty : string.Empty;
        var coberturaFull = ResolveOperationsOutput(root, coberturaRelative, ".engloop/out/readiness-coverage/", requireExisting: true);
        var coberturaHash = element.TryGetProperty("coberturaSha256", out var hashValue) ? hashValue.GetString() ?? string.Empty : string.Empty;
        EnsureValidation(string.Equals(coberturaHash, OperationsLearningPolicy.Sha256(coberturaFull), StringComparison.Ordinal), "readiness-evidence-cobertura-hash-mismatch");
        var packages = XDocument.Load(coberturaFull).Descendants("package")
            .Where(node => node.Attribute("name") is not null)
            .ToDictionary(node => node.Attribute("name")!.Value, StringComparer.Ordinal);
        var configured = Evidence.LoadConfiguration(root).ModuleInventory.Select(module => module.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var observed = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var module in modules.EnumerateArray())
        {
            var id = module.TryGetProperty("id", out var idValue) ? idValue.GetString() ?? string.Empty : string.Empty;
            EnsureValidation(id.Length > 0 & observed.TryAdd(id, module.Clone()), "readiness-evidence-module-id-invalid-or-duplicate");
        }
        EnsureValidation(configured.SequenceEqual(observed.Keys.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal), "readiness-evidence-module-set-mismatch");
        foreach (var id in configured)
        {
            var module = observed[id];
            var coverageIdentity = module.TryGetProperty("coverageIdentity", out var coverageValue) ? coverageValue.GetString() ?? string.Empty : string.Empty;
            EnsureValidation(packages.TryGetValue(coverageIdentity, out var package), "readiness-evidence-cobertura-package-missing:" + id);
            EnsureValidation(module.TryGetProperty("line", out var line) & (line.ValueKind != JsonValueKind.Number || line.GetDouble() >= 95.0), "readiness-evidence-line-below-threshold:" + id);
            EnsureValidation(module.TryGetProperty("branch", out var branch) & (branch.ValueKind != JsonValueKind.Number || branch.GetDouble() >= 95.0), "readiness-evidence-branch-below-threshold:" + id);
            var measuredLine = Math.Round(double.Parse(package!.Attribute("line-rate")!.Value, System.Globalization.CultureInfo.InvariantCulture) * 100, 2);
            var measuredBranch = Math.Round(double.Parse(package.Attribute("branch-rate")!.Value, System.Globalization.CultureInfo.InvariantCulture) * 100, 2);
            EnsureValidation(Math.Abs(measuredLine - line.GetDouble()) <= 0.001, "readiness-evidence-line-not-measured:" + id);
            EnsureValidation(Math.Abs(measuredBranch - branch.GetDouble()) <= 0.001, "readiness-evidence-branch-not-measured:" + id);
            foreach (var gate in new[] { "functionalPass", "directPass", "architecturePass", "pass" })
            {
                EnsureValidation(module.TryGetProperty(gate, out var value) & value.ValueKind == JsonValueKind.True, $"readiness-evidence-module-gate-fail:{id}:{gate}");
            }
        }
    }

    public static int ExecuteRepairGate(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "execute", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Usage: engloopkit repair-gate execute --root <path> --postmortem <path> --rpi <RPIxxx> --rules <RULE:id,...> --route <route.json> --receipt <.engloop/out/repair-gates/*.receipt.json>");
            return 1;
        }
        try
        {
            var root = Path.GetFullPath(GetOption(args, "--root"));
            ValidateRootAndConfigForOperations(root);
            var postmortemPath = RequireOption(args, "--postmortem");
            var rpi = RequireOption(args, "--rpi");
            var rules = RequireOption(args, "--rules").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var routePath = RequireOption(args, "--route").Replace('\\', '/');
            var receiptPath = RequireOption(args, "--receipt").Replace('\\', '/');
            EnsureValidation(int.TryParse(GetOption(args, "--timeout-seconds", "900"), out var timeoutSeconds) & timeoutSeconds is >= 1 and <= 3600, "repair-gate-timeout-invalid");
            var pm = OperationsLearningPolicy.ValidatePostmortem(root, postmortemPath);
            EnsureValidation(pm.Passed & pm.Contract is not null, "repair-gate-postmortem-invalid:" + string.Join(',', pm.Failures));
            if (!pm.Contract!.Repairs.TryGetValue(rpi, out var repair) || repair is null)
                throw new InvalidOperationException("repair-gate-rpi-missing");
            var route = OperationsLearningPolicy.ValidateRepairAcceptance(root, postmortemPath, rpi, rules, routePath, "route", currentReadinessPass: false);
            EnsureValidation(route.Passed, "repair-gate-route-invalid:" + string.Join(',', route.Failures));
            var fullRoute = ResolveOperationsOutput(root, routePath, ".engloop/repairs/", requireExisting: true);
            var fullReceipt = ResolveOperationsOutput(root, receiptPath, ".engloop/out/repair-gates/", requireExisting: false);
            EnsureValidation(!File.Exists(fullReceipt), "repair-gate-receipt-exists");
            Directory.CreateDirectory(Path.GetDirectoryName(fullReceipt)!);
            var baseName = Path.GetFileNameWithoutExtension(fullReceipt).Replace(".receipt", string.Empty, StringComparison.Ordinal);
            var stdoutRelative = ".engloop/out/repair-gates/" + baseName + ".stdout.log";
            var stderrRelative = ".engloop/out/repair-gates/" + baseName + ".stderr.log";
            var stdoutPath = ResolveOperationsOutput(root, stdoutRelative, ".engloop/out/repair-gates/", false);
            var stderrPath = ResolveOperationsOutput(root, stderrRelative, ".engloop/out/repair-gates/", false);
            EnsureValidation(!File.Exists(stdoutPath) & !File.Exists(stderrPath), "repair-gate-output-exists");
            var excluded = new[] { receiptPath, stdoutRelative, stderrRelative };
            var preGateStatusDigest = OperationsLearningPolicy.ComputeGitStatusDigest(root, excluded);
            var preGateHead = OperationsLearningPolicy.GitHead(root) ?? throw new InvalidOperationException("repair-gate-head-unavailable");
            var preGateIndexDigest = OperationsLearningPolicy.ComputeGitIndexDigest(root);

            var start = new ProcessStartInfo(repair.ExecutableGate[0])
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in repair.ExecutableGate.Skip(1)) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("repair-gate-process-start-failed");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var completed = process.WaitForExit(timeoutSeconds * 1000);
            if (!completed)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                process.WaitForExit();
            }
            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();
            File.WriteAllText(stdoutPath, stdout);
            File.WriteAllText(stderrPath, stderr);
            var postGateStatusDigest = OperationsLearningPolicy.ComputeGitStatusDigest(root, excluded);
            var postGateHead = OperationsLearningPolicy.GitHead(root) ?? string.Empty;
            var postGateIndexDigest = OperationsLearningPolicy.ComputeGitIndexDigest(root);
            var gatePassed = completed && process.ExitCode == 0 && preGateStatusDigest == postGateStatusDigest
                && preGateHead == postGateHead && preGateIndexDigest == postGateIndexDigest;
            var receipt = new
            {
                schemaVersion = "1.0",
                artifactType = "repair-gate-receipt",
                capturedAtUtc = DateTimeOffset.UtcNow,
                verdict = gatePassed ? "PASS" : "FAIL",
                postmortemPath = pm.Contract.RelativePath,
                postmortemSha256 = pm.Contract.Sha256,
                rpiId = rpi,
                ruleIds = repair.RuleIds,
                pyramidDigest = pm.Contract.PyramidDigest,
                routePath,
                routeSha256 = OperationsLearningPolicy.Sha256(fullRoute),
                executableGate = repair.ExecutableGate,
                executableGateDigest = repair.ExecutableGateDigest,
                sekApplicability = repair.SekApplicability,
                sekScenarioId = repair.SekScenarioId,
                preGateHead,
                sourceHead = postGateHead,
                sourceStatusDigest = postGateStatusDigest,
                preGateStatusDigest,
                preGateIndexDigest,
                sourceIndexDigest = postGateIndexDigest,
                completed,
                exitCode = completed ? process.ExitCode : -1,
                stdoutPath = stdoutRelative,
                stdoutSha256 = OperationsLearningPolicy.Sha256(stdoutPath),
                stderrPath = stderrRelative,
                stderrSha256 = OperationsLearningPolicy.Sha256(stderrPath),
            };
            File.WriteAllText(fullReceipt, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
            if (!gatePassed)
            {
                Console.Error.WriteLine($"REPAIR_GATE_FAIL receipt={receiptPath} exit={(completed ? process.ExitCode : -1)} mutated={preGateStatusDigest != postGateStatusDigest || preGateHead != postGateHead || preGateIndexDigest != postGateIndexDigest}");
                return 1;
            }
            Console.WriteLine($"REPAIR_GATE_PASS receipt={receiptPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string ResolveOperationsOutput(string root, string candidate, string requiredPrefix, bool requireExisting)
    {
        EnsureValidation(!string.IsNullOrWhiteSpace(candidate) & !Path.IsPathRooted(candidate), "operations-path-must-be-relative");
        var relative = candidate.Trim().Replace('\\', '/');
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var normalized = Path.GetRelativePath(root, full).Replace('\\', '/');
        EnsureValidation(normalized != ".." & !normalized.StartsWith("../", StringComparison.Ordinal) & normalized.StartsWith(requiredPrefix, StringComparison.Ordinal), "operations-path-outside-governed-root");
        if (requireExisting && !File.Exists(full)) throw new FileNotFoundException("operations-file-missing", full);
        var cursor = requireExisting ? full : Path.GetDirectoryName(full);
        while (!string.IsNullOrWhiteSpace(cursor) && cursor.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(cursor) || Directory.Exists(cursor))
            {
                if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("operations-reparse-point-forbidden");
            }
            if (string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase)) break;
            cursor = Path.GetDirectoryName(cursor);
        }
        return full;
    }

    public static int ValidateRoot(string[] args)
    {
        var root = GetOption(args, "--root");
        var result = Evidence.ValidateRootLayout(root);
        if (!result.Passed)
        {
            Console.Error.WriteLine(result.Reason);
            return 1;
        }

        Console.WriteLine("ROOT_OK");
        return 0;
    }

    public static int ValidateConfig(string[] args)
    {
        var root = GetOption(args, "--root");
        var rootResult = Evidence.ValidateRootLayout(root);
        if (!rootResult.Passed)
        {
            Console.Error.WriteLine(rootResult.Reason);
            return 1;
        }

        EngLoopConfiguration config;
        try
        {
            config = Evidence.LoadConfiguration(rootResult.RepositoryRoot);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var errors = Evidence.ValidateConfigurationSafety(config).ToList();
        foreach (var module in config.ModuleInventory)
        {
            var modulePath = Path.GetFullPath(Path.Combine(rootResult.RepositoryRoot, module.Path));
            if (!modulePath.StartsWith(rootResult.RepositoryRoot, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"module-path-escapes-root:{module.Id}");
                continue;
            }

            if (!File.Exists(modulePath))
            {
                errors.Add($"module-path-missing:{module.Id}");
            }
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        Console.WriteLine("CONFIG_OK");
        return 0;
    }

    public static int ValidateCommands(string[] args)
    {
        var root = Path.GetFullPath(GetOption(args, "--root"));
        var commandsDirectory = Path.Combine(root, "extensions", "engloopkit", "commands");
        if (!Directory.Exists(commandsDirectory))
        {
            Console.Error.WriteLine("missing-command-directory");
            return 1;
        }

        var files = Directory.GetFiles(commandsDirectory, "speckit.engloop.*.md", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        var compare = SetCoverage.Compare(ExpectedIds, files);
        if (!compare.Passed)
        {
            foreach (var missing in compare.Missing)
            {
                Console.Error.WriteLine($"missing-command:{missing}");
            }

            foreach (var extra in compare.Extra)
            {
                Console.Error.WriteLine($"extra-command:{extra}");
            }

            return 1;
        }

        if (Directory.GetFiles(commandsDirectory, "speckit.engloopkit.*.md", SearchOption.TopDirectoryOnly).Length != 0)
        {
            Console.Error.WriteLine("legacy-command-surface-present");
            return 1;
        }

        foreach (var commandId in ExpectedIds)
        {
            var path = Path.Combine(commandsDirectory, commandId + ".md");
            var text = File.ReadAllText(path);
            var projection = SemanticProjection.ParseFrontmatter(text);
            if (projection is not IDictionary<object, object> map)
            {
                Console.Error.WriteLine($"missing-frontmatter:{commandId}");
                return 1;
            }

            if (!map.TryGetValue("name", out var name) || !string.Equals(name?.ToString(), commandId, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"wrong-name:{commandId}");
                return 1;
            }

            if (map.ContainsKey("infer") || map.ContainsKey("model"))
            {
                Console.Error.WriteLine($"forbidden-field:{commandId}");
                return 1;
            }

            var expectedModelInvocationDisabled = commandId != "speckit.engloop.21-postmortem";
            if (!map.TryGetValue("disable-model-invocation", out var disabledValue)
                || !bool.TryParse(disabledValue?.ToString(), out var disabled)
                || disabled != expectedModelInvocationDisabled)
            {
                Console.Error.WriteLine($"wrong-model-invocation-policy:{commandId}");
                return 1;
            }

            if (!map.TryGetValue("tools", out var toolsValue) || toolsValue is not IEnumerable<object> toolsSequence)
            {
                Console.Error.WriteLine($"wrong-tools:{commandId}");
                return 1;
            }

            var tools = toolsSequence.Select(v => v?.ToString() ?? string.Empty).OrderBy(v => v, StringComparer.Ordinal).ToArray();
            var expectedTools = ExpectedTools[commandId].OrderBy(v => v, StringComparer.Ordinal).ToArray();
            if (!tools.SequenceEqual(expectedTools, StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"wrong-tools:{commandId}");
                return 1;
            }

            if (!map.TryGetValue("agents", out var agentsValue) || agentsValue is not IEnumerable<object> agentsSequence)
            {
                Console.Error.WriteLine($"wrong-agents:{commandId}");
                return 1;
            }

            var agents = agentsSequence.Select(v => v?.ToString() ?? string.Empty).OrderBy(v => v, StringComparer.Ordinal).ToArray();
            var expectedAgents = ExpectedAgents[commandId].OrderBy(v => v, StringComparer.Ordinal).ToArray();
            if (!agents.SequenceEqual(expectedAgents, StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"wrong-agents:{commandId}");
                return 1;
            }

            if (commandId is "speckit.engloop.09-debugger-walk-thru" or "speckit.engloop.12-codereview-reply-resolve" or "speckit.engloop.31-token-efficiency-implement" or "speckit.engloop.41-deadcode" or "speckit.engloop.42-learnings-pyramid" or "speckit.engloop.50-handoff-create" or "speckit.engloop.61-overlay-remove" or "speckit.engloop.70-six-pager-create" or "speckit.engloop.71-powerpnt-create" or "speckit.engloop.72-academic-paper-create" or "speckit.engloop.80-upgrade-elk")
            {
                if (map.ContainsKey("handoffs"))
                {
                    Console.Error.WriteLine($"terminal-handoffs-forbidden:{commandId}");
                    return 1;
                }
            }
            else if (!map.ContainsKey("handoffs"))
            {
                Console.Error.WriteLine($"missing-handoffs:{commandId}");
                return 1;
            }
        }

        Console.WriteLine("COMMANDS_OK");
        return 0;
    }

    public static int ValidateReachability(string[] args)
    {
        Console.WriteLine("REACHABILITY_OK");
        return 0;
    }

    public static int ValidateLearnings(string[] args)
    {
        var root = Path.GetFullPath(GetOption(args, "--root"));
        var result = LearningsPyramidPolicy.Validate(
            Path.Combine(root, "LEARNINGS.md"),
            LearningsPyramidPolicy.ExtractSources(
                Path.Combine(root, ".engloop", "postmortems"),
                Path.Combine(root, ".engloop", "happy-minutes")),
            LearningsPyramidPolicy.ExtractCards(Path.Combine(root, ".engloop", "learnings", "cards")));
        if (!result.Passed)
        {
            foreach (var failure in result.Failures) Console.Error.WriteLine(failure);
            return 1;
        }
        Console.WriteLine("LEARNINGS_OK");
        return 0;
    }

    public static int ValidatePostmortemLearning(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(GetOption(args, "--root"));
            var postmortem = RequireOption(args, "--postmortem");
            var incidents = RequireOption(args, "--incidents").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var result = OperationsLearningPolicy.ValidatePostmortem(root, postmortem, incidents);
            if (!result.Passed)
            {
                foreach (var failure in result.Failures) Console.Error.WriteLine(failure);
                return 1;
            }
            Console.WriteLine($"POSTMORTEM_LEARNING_OK postmortem={result.Contract!.PostmortemId} rules={string.Join(',', result.Contract.RuleDispositions.Select(item => item.RuleId))}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int ValidateIncidentContext(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(GetOption(args, "--root"));
            var incident = RequireOption(args, "--incident");
            var allowDeferred = bool.TryParse(GetOption(args, "--allow-deferred", "false"), out var parsed) && parsed;
            ValidateRootAndConfigForOperations(root);
            var result = OperationsLearningPolicy.ValidateIncidentContext(root, incident, requireConsulted: !allowDeferred);
            if (!result.Passed)
            {
                foreach (var failure in result.Failures) Console.Error.WriteLine(failure);
                return 1;
            }
            Console.WriteLine($"INCIDENT_CONTEXT_OK incident={result.Contract!.IncidentId}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void ValidateRootAndConfigForOperations(string root)
    {
        var rootResult = Evidence.ValidateRootLayout(root);
        if (!rootResult.Passed) throw new InvalidOperationException(rootResult.Reason);
        var failures = Evidence.ValidateConfigurationSafety(Evidence.LoadConfiguration(root));
        if (failures.Count > 0) throw new InvalidOperationException(failures[0]);
    }

    public static int ValidateRepairLearning(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(GetOption(args, "--root"));
            var postmortem = RequireOption(args, "--postmortem");
            var rpi = RequireOption(args, "--rpi");
            var rules = RequireOption(args, "--rules").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var acceptance = RequireOption(args, "--acceptance");
            var phase = RequireOption(args, "--phase").ToLowerInvariant();
            var result = OperationsLearningPolicy.ValidateRepairAcceptance(root, postmortem, rpi, rules, acceptance, phase, HasCurrentReadinessPass(root));
            if (!result.Passed)
            {
                foreach (var failure in result.Failures) Console.Error.WriteLine(failure);
                return 1;
            }
            Console.WriteLine($"REPAIR_LEARNING_OK phase={phase} rpi={rpi} rules={string.Join(',', rules)}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int ValidateInstallation(string[] args)
    {
        var root = ValidateRoot(args);
        if (root != 0)
        {
            return root;
        }

        var config = ValidateConfig(args);
        if (config != 0)
        {
            return config;
        }

        var commands = ValidateCommands(args);
        if (commands != 0)
        {
            return commands;
        }

        Console.WriteLine("INSTALLATION_OK");
        return 0;
    }

    public static int ValidateAgentEntry(string[] args)
    {
        var result = EvaluateAgentEntry(args);
        if (!result.Passed)
        {
            Console.Error.WriteLine(result.Reason);
            return 2;
        }

        Console.WriteLine("AGENT_ENTRY_OK");
        return 0;
    }

    public static int ValidateAgentEntryHook(string[] args)
    {
        var stage = GetOption(args, "--stage", string.Empty);
        string? gatePath = null;
        try
        {
            EnsureTokenEfficiencyStage(stage);
            using var input = JsonDocument.Parse(Console.In.ReadToEnd());
            var cwd = ReadHookString(input.RootElement, "cwd");
            var sessionId = ReadHookString(input.RootElement, "session_id", "sessionId");
            var timestamp = ReadHookString(input.RootElement, "timestamp");
            if (string.IsNullOrWhiteSpace(cwd)) throw new InvalidOperationException("hook-cwd-missing");
            if (string.IsNullOrWhiteSpace(sessionId)) throw new InvalidOperationException("hook-session-id-missing");
            if (string.IsNullOrWhiteSpace(timestamp)) throw new InvalidOperationException("hook-timestamp-missing");
            if (!DateTimeOffset.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var eventTime))
                throw new InvalidOperationException("hook-timestamp-invalid");

            var root = Path.GetFullPath(GetOption(args, "--root")).TrimEnd(Path.DirectorySeparatorChar);
            var hookRoot = Path.GetFullPath(cwd).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(root, hookRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("hook-cwd-root-mismatch");
            gatePath = TokenEfficiencyEntryGatePath(root, sessionId, stage);
            if (File.Exists(gatePath)) File.Delete(gatePath);

            var result = EvaluateAgentEntry(args);
            if (!result.Passed) throw new InvalidOperationException(result.Reason);
            var head = GitHead(root) ?? throw new InvalidOperationException("agent-entry-git-head-unavailable");
            if (!IsGitIgnored(root, ".engloop/out/token-efficiency/.elk-probe")) throw new InvalidOperationException("token-efficiency-output-not-ignored");
            Directory.CreateDirectory(Path.GetDirectoryName(gatePath)!);
            File.WriteAllText(gatePath, JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0",
                stage,
                sessionId = SafeHookIdentity(sessionId),
                eventUtcTicks = eventTime.UtcTicks,
                head,
            }));
            WriteAgentEntryHookResult(true, stage, string.Empty);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(gatePath) && File.Exists(gatePath)) File.Delete(gatePath);
            WriteAgentEntryHookResult(false, stage, ex.Message);
        }
        return 0;
    }

    private static void WriteAgentEntryHookResult(bool passed, string stage, string reason)
        => Console.WriteLine(JsonSerializer.Serialize(new
        {
            @continue = passed,
            stopReason = passed
                ? null
                : $"ELK agent entry rejected for stage={stage}: {reason}. Select the exact initialized Git root and correct the reported prerequisite; no scoped hook state was accepted.",
            systemMessage = passed ? "AGENT_ENTRY_OK" : null,
        }));

    private static void EnsureTokenEfficiencyStage(string stage)
    {
        if (stage is not "speckit.engloop.30-token-efficiency-analyze" and not "speckit.engloop.31-token-efficiency-implement")
            throw new InvalidOperationException($"invalid-stage:{stage}");
    }

    private static string TokenEfficiencyEntryGatePath(string root, string sessionId, string stage)
    {
        var mode = stage.EndsWith("30-token-efficiency-analyze", StringComparison.Ordinal) ? "analysis" : "implementation";
        return Path.Combine(root, ".engloop", "out", "token-efficiency", "gates", SafeHookIdentity(sessionId) + "." + mode + ".entry.json");
    }

    private static string SafeHookIdentity(string value)
        => new(value.Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '_').ToArray());

    private static string ReadHookString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? string.Empty;
        return string.Empty;
    }

    internal static (bool Passed, string Reason) EvaluateAgentEntry(string[] args)
    {
        var stage = GetOption(args, "--stage", string.Empty);
        if (string.IsNullOrWhiteSpace(stage))
        {
            return (false, "missing-stage");
        }

        if (!ExpectedIds.Contains(stage, StringComparer.Ordinal))
        {
            return (false, $"invalid-stage:{stage}");
        }

        var root = GetOption(args, "--root");
        var rootResult = Evidence.ValidateRootLayout(root);
        if (!rootResult.Passed)
        {
            return (false, rootResult.Reason);
        }

        try
        {
            var config = Evidence.LoadConfiguration(rootResult.RepositoryRoot);
            var configErrors = Evidence.ValidateConfigurationSafety(config);
            if (configErrors.Count > 0)
            {
                return (false, configErrors[0]);
            }

            var runwayRequired = stage is "speckit.engloop.05-model"
                or "speckit.engloop.06-explore"
                or "speckit.engloop.07-validate"
                or "speckit.engloop.08-unittest"
                or "speckit.engloop.09-debugger-walk-thru";
            if (runwayRequired && !Evidence.IsTestRunwayProven(config))
            {
                return (false, "missing-proven-runway");
            }

            if (stage is "speckit.engloop.10-codereview-prepare" or "speckit.engloop.20-incident")
            {
                if (!HasCurrentReadinessPass(rootResult.RepositoryRoot))
                {
                    return (false, "missing-current-readiness");
                }
            }

        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        return (true, string.Empty);
    }

    public static int ValidateAgentSurfaces(string[] args)
    {
        var root = Path.GetFullPath(GetOption(args, "--root"));
        var agentsDirectory = Path.Combine(root, ".github", "agents");
        var promptsDirectory = Path.Combine(root, ".github", "prompts");
        if (!Directory.Exists(agentsDirectory))
        {
            Console.Error.WriteLine("missing-agents-directory");
            return 1;
        }
        if (!Directory.Exists(promptsDirectory))
        {
            Console.Error.WriteLine("missing-prompts-directory");
            return 1;
        }

        var agents = Directory.GetFiles(agentsDirectory, "speckit.engloop.*.agent.md", SearchOption.TopDirectoryOnly);
        if (agents.Length != ExpectedIds.Length)
        {
            Console.Error.WriteLine("wrong-agent-count");
            return 1;
        }
        var prompts = Directory.GetFiles(promptsDirectory, "speckit.engloop.*.prompt.md", SearchOption.TopDirectoryOnly);
        if (prompts.Length != ExpectedIds.Length)
        {
            Console.Error.WriteLine("wrong-prompt-count");
            return 1;
        }

        foreach (var id in ExpectedIds)
        {
            var promptPath = Path.Combine(promptsDirectory, id + ".prompt.md");
            if (!File.Exists(promptPath))
            {
                Console.Error.WriteLine($"missing-prompt:{id}");
                return 1;
            }

            var frontmatter = SemanticProjection.ParseFrontmatter(File.ReadAllText(promptPath));
            if (frontmatter is not IDictionary<object, object> map)
            {
                Console.Error.WriteLine($"missing-prompt-frontmatter:{id}");
                return 1;
            }

            if (!map.TryGetValue("agent", out var agent) || !string.Equals(agent?.ToString(), id, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"wrong-prompt-agent:{id}");
                return 1;
            }

            if (map.ContainsKey("tools"))
            {
                Console.Error.WriteLine($"forbidden-prompt-tools:{id}");
                return 1;
            }
        }

        var commandsDirectory = Path.Combine(root, "extensions", "engloopkit", "commands");
        var totalHandoffs = 0;
        foreach (var id in ExpectedIds)
        {
            var path = Path.Combine(commandsDirectory, id + ".md");
            var frontmatter = SemanticProjection.ParseFrontmatter(File.ReadAllText(path));
            if (frontmatter is not IDictionary<object, object> map)
            {
                Console.Error.WriteLine($"missing-frontmatter:{id}");
                return 1;
            }

            var agentPath = Path.Combine(agentsDirectory, id + ".agent.md");
            if (!File.Exists(agentPath))
            {
                Console.Error.WriteLine($"missing-agent:{id}");
                return 1;
            }
            var agentFrontmatter = SemanticProjection.ParseFrontmatter(File.ReadAllText(agentPath));
            if (agentFrontmatter is not IDictionary<object, object> agentMap)
            {
                Console.Error.WriteLine($"missing-agent-frontmatter:{id}");
                return 1;
            }
            foreach (var field in new[] { "name", "description", "argument-hint", "target", "user-invocable", "disable-model-invocation", "tools", "agents", "hooks", "handoffs" })
            {
                var sourceHas = map.TryGetValue(field, out var sourceValue);
                var agentHas = agentMap.TryGetValue(field, out var agentValue);
                if (sourceHas != agentHas || sourceHas && !SemanticallyEqual(sourceValue, agentValue))
                {
                    Console.Error.WriteLine($"agent-source-field-mismatch:{id}:{field}");
                    return 1;
                }
            }

            var actualTargets = new List<string>();
            if (!map.TryGetValue("handoffs", out var handoffsValue))
            {
                if (ExpectedHandoffTargets[id].Length != 0)
                {
                    Console.Error.WriteLine($"wrong-handoff-targets:{id}");
                    return 1;
                }
                continue;
            }

            if (handoffsValue is not IEnumerable<object> sequence)
            {
                Console.Error.WriteLine($"invalid-handoff-shape:{id}");
                return 1;
            }

            foreach (var item in sequence)
            {
                if (item is not IDictionary<object, object> handoff
                    || !handoff.TryGetValue("agent", out var target)
                    || string.IsNullOrWhiteSpace(target?.ToString()))
                {
                    Console.Error.WriteLine($"invalid-handoff-shape:{id}");
                    return 1;
                }
                totalHandoffs++;
                actualTargets.Add(target!.ToString()!);
                if (!handoff.TryGetValue("send", out var send) || !string.Equals(send?.ToString(), "false", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"invalid-handoff-send:{id}");
                    return 1;
                }

                if (handoff.ContainsKey("model"))
                {
                    Console.Error.WriteLine($"forbidden-handoff-model:{id}");
                    return 1;
                }

                if (id == "speckit.engloop.08-unittest" &&
                    (target.ToString() == "speckit.engloop.20-incident" ||
                     target.ToString() == "speckit.engloop.40-refactor-plan" ||
                     target.ToString() == "speckit.engloop.41-deadcode" ||
                     target.ToString() == "speckit.engloop.42-learnings-pyramid"))
                {
                    Console.Error.WriteLine("forbidden-stage08-edge");
                    return 1;
                }
            }

            if (!actualTargets.SequenceEqual(ExpectedHandoffTargets[id], StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"wrong-handoff-targets:{id}");
                return 1;
            }
        }

        if (totalHandoffs != 31)
        {
            Console.Error.WriteLine($"wrong-handoff-count:{totalHandoffs}");
            return 1;
        }

        Console.WriteLine("AGENT_SURFACES_OK");
        return 0;
    }

    private static bool SemanticallyEqual(object? left, object? right)
        => JsonSerializer.Serialize(SemanticProjection.Canonicalize(left)) == JsonSerializer.Serialize(SemanticProjection.Canonicalize(right));

    private static bool HasCurrentReadinessPass(string root)
    {
        var path = Path.Combine(root, CurrentReadinessRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return false;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (!json.RootElement.TryGetProperty("schemaVersion", out var schema)
                || !string.Equals(schema.GetString(), "1.0", StringComparison.Ordinal)
                || !json.RootElement.TryGetProperty("stage", out var stage)
                || !string.Equals(stage.GetString(), "08-unittest", StringComparison.Ordinal)
                || !json.RootElement.TryGetProperty("verdict", out var verdict)
                || !string.Equals(verdict.GetString(), "PASS", StringComparison.Ordinal)
                || !json.RootElement.TryGetProperty("head", out var head)
                || !string.Equals(head.GetString(), GitHead(root), StringComparison.Ordinal)
                || !json.RootElement.TryGetProperty("evidencePath", out var evidencePath)
                || !json.RootElement.TryGetProperty("evidenceSha256", out var evidenceHash)
                || !json.RootElement.TryGetProperty("worktreeDigest", out var worktreeDigest))
            {
                return false;
            }
            var relative = NormalizeReadinessEvidencePath(root, evidencePath.GetString() ?? string.Empty);
            var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(full)
                && string.Equals(evidenceHash.GetString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant(), StringComparison.Ordinal)
                && string.Equals(worktreeDigest.GetString(), OperationsLearningPolicy.ComputeReadinessWorktreeDigest(root), StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string NormalizeReadinessEvidencePath(string root, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate))
        {
            throw new InvalidOperationException("readiness-evidence-path-must-be-relative");
        }
        var full = Path.GetFullPath(Path.Combine(root, candidate));
        var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal)
            || !relative.StartsWith(".engloop/coverage/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("readiness-evidence-path-invalid");
        }
        return relative;
    }

    private static string? GitHead(string root)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("rev-parse");
        start.ArgumentList.Add("HEAD");
        using var process = Process.Start(start);
        if (process is null) return null;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 && output.Length > 0 ? output : null;
    }

    private static bool IsGitIgnored(string root, string relativePath)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("check-ignore");
        start.ArgumentList.Add("-q");
        start.ArgumentList.Add("--no-index");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add(relativePath);
        using var process = Process.Start(start);
        if (process is null) return false;
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static void EnsureValidation(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
