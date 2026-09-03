using System.Text.Json;
using System.Text.RegularExpressions;
using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>Release-facing structural checks for the ordered v1.8 source surface.</summary>
public sealed class BundleConformanceTests
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string ExtensionRoot = Path.Combine(Root, "extensions", "engloopkit");

    private static readonly string[] ExpectedIds =
    [
        "speckit.engloop.01-northstar",
        "speckit.engloop.02-scaffold",
        "speckit.engloop.03-architect",
        "speckit.engloop.04-refactor",
        "speckit.engloop.05-model",
        "speckit.engloop.06-explore",
        "speckit.engloop.07-validate",
        "speckit.engloop.08-unittest",
        "speckit.engloop.09-debugger-walk-thru",
        "speckit.engloop.10-codereview-prepare",
        "speckit.engloop.20-incident",
        "speckit.engloop.21-postmortem",
        "speckit.engloop.22-repair",
        "speckit.engloop.23-happy-minute",
        "speckit.engloop.30-token-efficiency-analyze",
        "speckit.engloop.31-token-efficiency-implement",
        "speckit.engloop.40-refactor-plan",
        "speckit.engloop.41-deadcode",
        "speckit.engloop.42-learnings-pyramid",
        "speckit.engloop.50-handoff-create",
        "speckit.engloop.60-overlay-pack",
        "speckit.engloop.61-overlay-remove",
        "speckit.engloop.70-six-pager-create",
        "speckit.engloop.71-powerpnt-create",
        "speckit.engloop.72-academic-paper-create",
        "speckit.engloop.80-upgrade-elk",
    ];

    [Fact]
    public void VersionAndIdentity_areConsistentAcrossReleaseMetadata()
    {
        var extension = File.ReadAllText(Path.Combine(ExtensionRoot, "extension.yml"));
        var bundle = File.ReadAllText(Path.Combine(Root, "bundle.yml"));
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "catalog.json")));

        Assert.Contains("id: \"engloop\"", extension);
        Assert.Contains("version: \"1.15.4\"", extension);
        Assert.Contains("id: \"engloopkit\"", bundle);
        Assert.Contains("version: \"1.15.4\"", bundle);
        Assert.Equal("engloop", catalog.RootElement.GetProperty("extensions")[0].GetProperty("id").GetString());
        Assert.Equal("1.15.4", catalog.RootElement.GetProperty("extensions")[0].GetProperty("version").GetString());
        Assert.Equal(26, catalog.RootElement.GetProperty("extensions")[0].GetProperty("provides").GetProperty("commands").GetInt32());

        var changelog = File.ReadAllText(Path.Combine(Root, "CHANGELOG.md"));
        Assert.Single(Regex.Matches(changelog, @"(?m)^## \[1\.15\.4\] - 2026-09-03\r?$").Cast<Match>());
        Assert.Single(Regex.Matches(changelog, @"(?m)^## \[1\.15\.3\] - 2026-08-31\r?$").Cast<Match>());
        Assert.Single(Regex.Matches(changelog, @"(?m)^## \[1\.15\.2\] - 2026-08-30\r?$").Cast<Match>());
        Assert.Single(Regex.Matches(changelog, @"(?m)^## \[1\.15\.1\] - 2026-08-20\r?$").Cast<Match>());
        Assert.Single(Regex.Matches(changelog, @"(?m)^## \[1\.15\.0\] - 2026-08-20\r?$").Cast<Match>());
        Assert.Single(Regex.Matches(changelog, @"(?m)^## \[1\.14\.0\] - 2026-08-17\r?$").Cast<Match>());
        Assert.Single(Regex.Matches(changelog, @"(?m)^## \[1\.13\.0\] - 2026-08-05\r?$").Cast<Match>());
    }

    [Fact]
    public void Extension_declaresExactOrderedTwentySixCommandSurface()
    {
        var manifest = File.ReadAllText(Path.Combine(ExtensionRoot, "extension.yml"));
        var ids = Regex.Matches(manifest, @"^\s*-\s*name:\s*""?(speckit\.engloop\.[\w-]+)""?", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToArray();
        Assert.Equal(ExpectedIds, ids);

        var commandDirectory = Path.Combine(ExtensionRoot, "commands");
        var files = Directory.GetFiles(commandDirectory, "speckit.engloop.*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedIds, files);
        Assert.Empty(Directory.GetFiles(commandDirectory, "speckit.engloopkit.*.md", SearchOption.TopDirectoryOnly));
    }

    [Theory]
    [MemberData(nameof(CommandIds))]
    public void EveryCommand_hasLoopContractAndMatchingPrompt(string id)
    {
        var command = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", id + ".md"));
        Assert.StartsWith("---", command.TrimStart());
        Assert.Contains("name: " + id, command);
        Assert.Contains("## User Input", command);
        Assert.Contains("## Artifact root", command);
        Assert.Contains("## Loop definition", command);
        Assert.Contains("**Trigger:**", command);
        Assert.Contains("**Goal:", command);
        Assert.Contains("**Actions:**", command);
        Assert.Contains("**Verification:**", command);
        Assert.Contains("**Memory:**", command);
        Assert.Contains("## Done when", command);

        var prompt = File.ReadAllText(Path.Combine(Root, ".github", "prompts", id + ".prompt.md"));
        Assert.Contains("agent: " + id, prompt);
        Assert.DoesNotContain("tools:", prompt);
    }

    [Fact]
    public void OverlayPackCommand_describesPrivateLocalOnlyContract()
    {
        var command = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.60-overlay-pack.md"));
        Assert.Contains(".git/info/exclude", command);
        Assert.Contains("overlay pack", command);
        Assert.Contains("unencrypted", command);
        Assert.Contains("never edits tracked `.gitignore`", command);
    }

    [Fact]
    public void NewUtilityCommands_haveTheirRequiredBoundaries()
    {
        var debugger = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.09-debugger-walk-thru.md"));
        Assert.Contains("personally stepped through", debugger, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("per-chunk engineer attestation", debugger, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SKILL.md", debugger, StringComparison.Ordinal);
        Assert.Contains("Do not infer a debugger", debugger, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicit `--debugger` choice", debugger, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("recommended engineering practice, not a transition gate", debugger, StringComparison.OrdinalIgnoreCase);

        var review = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.10-codereview-prepare.md"));
        Assert.Contains("github", review, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("azure-devops", review, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no persistent personal profile", review, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not reject Stage 10", review, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stage 09 is recommended but non-blocking", review, StringComparison.OrdinalIgnoreCase);

        var analysis = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.30-token-efficiency-analyze.md"));
        Assert.Contains("Non-negotiable read-only boundary", analysis, StringComparison.Ordinal);
        Assert.Contains("copilot_sessionStoreSql", analysis, StringComparison.Ordinal);
        Assert.Contains("Aggregate each one-to-many table independently", analysis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("polling-duplication", analysis, StringComparison.Ordinal);
        Assert.Contains("token-efficiency-analysis-", analysis, StringComparison.Ordinal);
        Assert.Contains("nothing was implemented", analysis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UserPromptSubmit:", analysis, StringComparison.Ordinal);
        Assert.Contains("validate agent-entry-hook --stage speckit.engloop.30-token-efficiency-analyze", analysis, StringComparison.Ordinal);
        Assert.Contains("-Mode analysis -Event UserPromptSubmit", analysis, StringComparison.Ordinal);
        Assert.DoesNotContain("-Mode analysis -Event SessionStart", analysis, StringComparison.Ordinal);
        Assert.DoesNotContain(".specify/scripts/", File.ReadAllText(Path.Combine(Root, ".github", "agents", "speckit.engloop.30-token-efficiency-analyze.agent.md")), StringComparison.OrdinalIgnoreCase);

        var implementation = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.31-token-efficiency-implement.md"));
        Assert.Contains("approved repair-ID list", implementation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("corepack pnpm --version", implementation, StringComparison.Ordinal);
        Assert.Contains("never disable signature", implementation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("package-lock.json", implementation, StringComparison.Ordinal);
        Assert.Contains("open-standard Agent Skill", implementation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("focused validation", implementation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never commit or push", implementation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validate agent-entry-hook --stage speckit.engloop.31-token-efficiency-implement", implementation, StringComparison.Ordinal);
        Assert.Contains("-Mode implementation -Event UserPromptSubmit", implementation, StringComparison.Ordinal);
        Assert.DoesNotContain("-Mode implementation -Event SessionStart", implementation, StringComparison.Ordinal);
        Assert.DoesNotContain(".specify/scripts/", File.ReadAllText(Path.Combine(Root, ".github", "agents", "speckit.engloop.31-token-efficiency-implement.agent.md")), StringComparison.OrdinalIgnoreCase);

        var incident = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.20-incident.md"));
        Assert.Contains("--incidents <INxxx,...> --postmortem", incident, StringComparison.Ordinal);

        var postmortem = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.21-postmortem.md"));
        Assert.Contains("operations-hook guard postmortem", postmortem, StringComparison.Ordinal);
        Assert.Contains("OPERATIONS_LEARNING_CONTEXT_REQUIRED", postmortem, StringComparison.Ordinal);
        Assert.Contains("no scope", postmortem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("completion was accepted", postmortem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unrelated command-style options", postmortem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fill the missing value", postmortem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Plain follow-up text cannot clear", postmortem, StringComparison.OrdinalIgnoreCase);

        var analysisTemplate = File.ReadAllText(Path.Combine(ExtensionRoot, "templates", "TOKEN-EFFICIENCY-ANALYSIS-template.json"));
        Assert.Contains("token-efficiency-analysis", analysisTemplate, StringComparison.Ordinal);
        Assert.Contains("dataAvailability", analysisTemplate, StringComparison.Ordinal);
        Assert.Contains("recommendedRepoRepairs", analysisTemplate, StringComparison.Ordinal);

        var implementationTemplate = File.ReadAllText(Path.Combine(ExtensionRoot, "templates", "TOKEN-EFFICIENCY-IMPLEMENTATION-template.json"));
        Assert.Contains("approvedRepairIds", implementationTemplate, StringComparison.Ordinal);
        Assert.Contains("unavailableToolDecisions", implementationTemplate, StringComparison.Ordinal);

        var skillTemplate = File.ReadAllText(Path.Combine(ExtensionRoot, "templates", "TOKEN-EFFICIENCY-SKILL-template.md"));
        Assert.Contains("scripts/", skillTemplate, StringComparison.Ordinal);
        Assert.Contains("references/", skillTemplate, StringComparison.Ordinal);
        Assert.Contains("progressively", skillTemplate, StringComparison.OrdinalIgnoreCase);

        var preflight = File.ReadAllText(Path.Combine(ExtensionRoot, "scripts", "Resolve-DeclaredToolchain.ps1"));
        Assert.Contains("corepack-pnpm-available", preflight, StringComparison.Ordinal);
        Assert.Contains("integrityVerificationBypassAllowed", preflight, StringComparison.Ordinal);
        Assert.DoesNotContain("npm install", preflight, StringComparison.OrdinalIgnoreCase);

        using var analysisSchema = JsonDocument.Parse(File.ReadAllText(Path.Combine(ExtensionRoot, "schemas", "token-efficiency-analysis.schema.json")));
        Assert.Equal("token-efficiency-analysis", analysisSchema.RootElement.GetProperty("properties").GetProperty("artifactType").GetProperty("const").GetString());
        Assert.True(analysisSchema.RootElement.GetProperty("$defs").TryGetProperty("repoRepair", out _));

        using var implementationSchema = JsonDocument.Parse(File.ReadAllText(Path.Combine(ExtensionRoot, "schemas", "token-efficiency-implementation.schema.json")));
        Assert.Contains(implementationSchema.RootElement.GetProperty("required").EnumerateArray(), value => value.GetString() == "outcome");
        Assert.Contains(implementationSchema.RootElement.GetProperty("required").EnumerateArray(), value => value.GetString() == "repairStatus");

        foreach (var trustedScript in new[] { "TokenEfficiencyPolicy.ps1", "Guard-TokenEfficiencyAgent.ps1", "Initialize-TokenEfficiencyImplementationGate.ps1", "Get-TokenEfficiencySourceState.ps1" })
        {
            Assert.True(File.Exists(Path.Combine(ExtensionRoot, "scripts", trustedScript)), trustedScript);
        }

        var refactor = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.40-refactor-plan.md"));
        foreach (var marker in new[]
        {
            "--scope <path-or-topic>", "--profile <point|bounded|deep>",
            "DEFAULT-POINT", "Never infer", "No repository-wide survey",
            "At most one read-only `Explore` survey", "At most two read-only `Explore` surveys",
            "Stage 04 owns implementation", "MAI-Flash-1.1", "Luna/low-thinking",
            "Tera/medium-thinking", "SOL/frontier max-thinking", "PM002/LEARN001–003",
            "dotnet tool run engloopkit -- refactor-profile bind",
            "dotnet tool run engloopkit -- refactor-profile clear",
            "NORTHSTAR.md", ".engloop/architecture/ARCH*.md", "docs/component-pattern.md",
            "work with the user", "Never edit product source", "vertical → component",
            "Proposed component API/responsibility", "Stage 04 implementation slices"
        }) Assert.Contains(marker, refactor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("point` is intentionally optimized for inexpensive/fast models", refactor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deep` is appropriate", refactor, StringComparison.OrdinalIgnoreCase);

        var generatedRefactor = File.ReadAllText(Path.Combine(Root, ".github", "agents", "speckit.engloop.40-refactor-plan.agent.md"));
        Assert.Contains("--profile <point|bounded|deep>", generatedRefactor, StringComparison.Ordinal);
        Assert.Contains("Runtime model", generatedRefactor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DEFAULT-POINT", generatedRefactor, StringComparison.Ordinal);

        var implementationRefactor = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.04-refactor.md"));
        foreach (var marker in new[]
        {
            "Implementation-only boundary", "accepted SPEC task slice", "REFACT plan/slice",
            "does not choose among refactor candidates", "North Star", "architecture decisions",
            "/speckit.engloop.40-refactor-plan", "Do not bundle adjacent cleanup"
        }) Assert.Contains(marker, implementationRefactor, StringComparison.OrdinalIgnoreCase);

        var refactTemplate = File.ReadAllText(Path.Combine(ExtensionRoot, "templates", "REFACT-template.md"));
        foreach (var marker in new[]
        {
            "Compute profile", "Profile source", "Declared scope", "Scope class",
            "UNAVAILABLE-NOT-INFERRED", "Implementation envelope", "Escalation decision",
            "Ordered implementation slices"
        }) Assert.Contains(marker, refactTemplate, StringComparison.OrdinalIgnoreCase);

        var handoff = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.50-handoff-create.md"));
        Assert.Contains("HANDOFF001", handoff, StringComparison.Ordinal);
        Assert.Contains("another chat window or engineering team", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HANDOFF<NNN>-<brief-kebab-description>.md", handoff, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(ExtensionRoot, "templates", "HANDOFF-template.md")));

        var happy = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.23-happy-minute.md"));
        foreach (var marker in new[]
        {
            "HAPPY001-everything-worked-perfectly.md", "user's description is enough",
            "Give the person a break", "LIVE/DEPLOYED", "LOCAL-CONTEXT",
            "NOT-PROVIDED", "do not auto-discover parent/sibling repositories",
            "Do not require readiness", "Do not change source", "gratitude"
        }) Assert.Contains(marker, happy, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(ExtensionRoot, "templates", "HAPPY-template.md")));
        var happyTemplate = File.ReadAllText(Path.Combine(ExtensionRoot, "templates", "HAPPY-template.md"));
        Assert.Contains("Stage 42 candidate:** NOT-YET", happyTemplate, StringComparison.Ordinal);
        Assert.Contains("LOCAL-CONTEXT", happyTemplate, StringComparison.Ordinal);

        var learnings = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.42-learnings-pyramid.md"));
        Assert.Contains("Positive provenance from Happy Minutes", learnings, StringComparison.Ordinal);
        Assert.Contains("HAPPY<NNN>", learnings, StringComparison.Ordinal);
        Assert.Contains("NO` or `NOT-YET", learnings, StringComparison.Ordinal);

        var deadcode = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.41-deadcode.md"));
        foreach (var marker in new[]
        {
            "DEADCODE<NNN>-<brief-kebab-description>.md", "Confidence: HIGH",
            "no current source changes before approval", "explicit yes/approve/proceed",
            "Status: REJECTED", "declined without reason", "next-best candidate",
            "dotnet build EngLoopKit.slnx", "dotnet test EngLoopKit.slnx"
        }) Assert.Contains(marker, deadcode, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(ExtensionRoot, "templates", "DEADCODE-template.md")));

        var remove = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.61-overlay-remove.md"));
        Assert.Contains("REMOVE-OVERLAY:<repository-id>@<base-revision>", remove, StringComparison.Ordinal);
        Assert.Contains("restore", remove, StringComparison.OrdinalIgnoreCase);

        var sixPager = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.70-six-pager-create.md"));
        Assert.Contains("exactly six rendered pages", sixPager, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Introduction", sixPager, StringComparison.Ordinal);
        Assert.Contains("Goals", sixPager, StringComparison.Ordinal);
        Assert.Contains("Tenets", sixPager, StringComparison.Ordinal);
        Assert.Contains("State of the business/system", sixPager, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lessons learned", sixPager, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Strategic priorities", sixPager, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pandoc", sixPager, StringComparison.Ordinal);
        Assert.Contains("DOCX", sixPager, StringComparison.Ordinal);

        var presentation = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.71-powerpnt-create.md"));
        Assert.Contains("North Star", presentation, StringComparison.Ordinal);
        Assert.Contains("boxes-and-lines", presentation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("7 +/- 2 nodes", presentation, StringComparison.Ordinal);
        Assert.Contains("straight-line", presentation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@marp-team/marp-cli", presentation, StringComparison.Ordinal);
        Assert.Contains("powerpnt-create", presentation, StringComparison.Ordinal);
        Assert.Contains("Connector-label geometry", presentation, StringComparison.Ordinal);
        Assert.Contains("must not intersect any node bounding box", presentation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dedicated label lane", presentation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Export every generated graph slide to PNG", presentation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Every graph connector label is collision-free", presentation, StringComparison.OrdinalIgnoreCase);

        var paper = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.72-academic-paper-create.md"));
        Assert.Contains("High-level architecture", paper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Evaluation methodology", paper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("**Results.**", paper, StringComparison.Ordinal);
        Assert.Contains("limitations, threats to validity", paper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Related work", paper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pandoc", paper, StringComparison.Ordinal);
        Assert.Contains("citeproc", paper, StringComparison.Ordinal);
        Assert.Contains("PDF", paper, StringComparison.Ordinal);

        var upgrade = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "speckit.engloop.80-upgrade-elk.md"));
        Assert.Contains("Update-EngLoopKit.ps1", upgrade, StringComparison.Ordinal);
        Assert.Contains("ELK_UPGRADE_CURRENT", upgrade, StringComparison.Ordinal);
        Assert.Contains("ELK_UPGRADE_PASS", upgrade, StringComparison.Ordinal);
        Assert.Contains("latest", upgrade, StringComparison.OrdinalIgnoreCase);
        var updater = File.ReadAllText(Path.Combine(ExtensionRoot, "scripts", "Update-EngLoopKit.ps1"));
        foreach (var marker in new[]
        {
            "/latest", "draft", "prerelease", "engloopkit-release-manifest-", "Assert-Hash",
            "ELK_UPGRADE_CURRENT", "ELK_UPGRADE_AVAILABLE", "ELK_UPGRADE_PASS",
            "ELK_UPGRADE_FAILED_ROLLED_BACK", "Restore-ManagedSnapshot", "Assert-RestoredSnapshot",
            "Save-CachedToolPackage", "dotnet tool restore", "dependencies.sek", "sek-cord-authoring", "using-sek-to-generate-tests", "0.1.3",
            "Assert-DotNetPlatform", "ELK_UPGRADE_DOTNET_TOO_OLD", "net10.0", "EngLoopKit.slnx"
        }) Assert.Contains(marker, updater, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dotnet tool install -g", updater, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Standards_matchExecutableNumberingVocabulary()
    {
        var standards = File.ReadAllText(Path.Combine(Root, "docs", "standards.md"));
        foreach (var prefix in NumberingRegistry.Prefixes)
        {
            Assert.Contains("`" + prefix + "`", standards);
        }
        Assert.Contains(".engloop", standards);
    }

    [Fact]
    public void PublicPolicy_explicitlyForbidsUiValidation()
    {
        var architecture = File.ReadAllText(Path.Combine(Root, ".engloop", "architecture", "ARCH006_deterministic-agent-surface-validation.md"));
        Assert.Contains("never performs UI validation", architecture);
        var validator = File.ReadAllText(Path.Combine(Root, "scripts", "validate-agent-surfaces.ps1"));
        Assert.Contains("No UI validation", validator);
    }

    public static IEnumerable<object[]> CommandIds() => ExpectedIds.Select(id => new object[] { id });

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bundle.yml")))
        {
            directory = directory.Parent;
        }
        Assert.True(directory is not null, "could not locate repository root");
        return directory!.FullName;
    }
}
