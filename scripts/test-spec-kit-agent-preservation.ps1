[CmdletBinding()]
param(
    [string]$SpecKitVersion = '0.12.4',
    [string]$VsCodeVersion = '1.129.0-insider',
    [string]$VsCodeCommit = '29d19ddd1af725baf537b6b328843bcdc2d29ba1'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$canaryRoot = Join-Path $repositoryRoot '.engloop/out/spec-kit-agent-canary'
$fixtureRoot = Join-Path $canaryRoot 'fixture'
$extensionRoot = Join-Path $canaryRoot 'source-extension'
$harnessRoot = Join-Path $canaryRoot 'harness'
$reportPath = Join-Path $canaryRoot 'report.json'
$schemaPath = Join-Path $repositoryRoot 'schemas/vscode-agent-surface.schema.json'
$passed = $false

function Invoke-Checked {
    param([string]$FilePath, [string[]]$ArgumentList, [string]$WorkingDirectory)

    Push-Location $WorkingDirectory
    try {
        & $FilePath @ArgumentList
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($ArgumentList -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

try {
    if (Test-Path $canaryRoot) {
        Remove-Item $canaryRoot -Recurse -Force
    }
    New-Item $fixtureRoot, (Join-Path $extensionRoot 'commands'), $harnessRoot -ItemType Directory -Force | Out-Null

    $actualSpecKitVersion = (& specify --version 2>&1 | Out-String).Trim()
    if ($actualSpecKitVersion -notmatch "(?m)^specify $([regex]::Escape($SpecKitVersion))$") {
        throw "Spec Kit version mismatch. Expected $SpecKitVersion; output was: $actualSpecKitVersion"
    }

    $actualVsCode = (& code-insiders --version 2>&1)
    if ($actualVsCode.Count -lt 2 -or $actualVsCode[0].Trim() -ne $VsCodeVersion -or $actualVsCode[1].Trim() -ne $VsCodeCommit) {
        throw "VS Code Insiders mismatch. Expected $VsCodeVersion/$VsCodeCommit; output was: $($actualVsCode -join ' | ')"
    }

    $schema = Get-Content $schemaPath -Raw | ConvertFrom-Json
    if ($schema.properties.vscodeVersion.const -ne $VsCodeVersion -or
        $schema.properties.vscodeCommit.const -ne $VsCodeCommit) {
        throw 'Tracked VS Code schema projection does not match the requested pinned build.'
    }

        $extensionManifestLines = @(
                'schema_version: "1.0"'
                'extension:'
                '  id: "agent-preservation-canary"'
                '  name: "Agent Preservation Canary"'
                '  version: "0.0.1"'
                '  description: "Disposable rich custom-agent preservation fixture."'
                '  author: "EngLoopKit"'
                '  license: "MIT"'
                'requires:'
                '  speckit_version: ">=0.12.4"'
                'provides:'
                '  commands:'
                '    - name: "speckit.agent-preservation-canary.rich"'
                '      file: "commands/speckit.agent-preservation-canary.rich.md"'
                '      description: "Exercise every rich custom-agent field and a branching handoff."'
                '    - name: "speckit.agent-preservation-canary.terminal"'
                '      file: "commands/speckit.agent-preservation-canary.terminal.md"'
                '      description: "Exercise an explicit empty subagent policy and no handoffs."'
        )
        [string]::Join("`n", $extensionManifestLines) | Set-Content (Join-Path $extensionRoot 'extension.yml') -Encoding utf8NoBOM

        $richCommandLines = @(
                '---'
                'name: speckit.agent-preservation-canary.rich'
                'description: Exercise every rich custom-agent field and a branching handoff.'
                'argument-hint: "[fixture input]"'
                'target: vscode'
                'user-invocable: true'
                'disable-model-invocation: true'
                'tools: [read, search, edit, execute, web, agent]'
                'agents: [Explore]'
                'hooks:'
                '  SessionStart:'
                '    - type: command'
                '      command: dotnet tool run engloopkit validate agent-entry --stage speckit.agent-preservation-canary.rich --root .'
                '      timeout: 30'
                'handoffs:'
                '  - label: Review terminal branch'
                '    agent: speckit.agent-preservation-canary.terminal'
                '    prompt: Review this exact canary context before continuing.'
                '    send: false'
                '---'
                ''
                '## User Input'
                ''
                '```text'
                '$ARGUMENTS'
                '```'
                ''
                '## Fixture'
                ''
                'Disposable preservation evidence only.'
        )
        [string]::Join("`n", $richCommandLines) | Set-Content (Join-Path $extensionRoot 'commands/speckit.agent-preservation-canary.rich.md') -Encoding utf8NoBOM

        $terminalCommandLines = @(
                '---'
                'name: speckit.agent-preservation-canary.terminal'
                'description: Exercise an explicit empty subagent policy and no handoffs.'
                'argument-hint: "[terminal fixture input]"'
                'target: vscode'
                'user-invocable: true'
                'disable-model-invocation: true'
                'tools: [read, search, edit, execute]'
                'agents: []'
                'hooks:'
                '  SessionStart:'
                '    - type: command'
                '      command: dotnet tool run engloopkit validate agent-entry --stage speckit.agent-preservation-canary.terminal --root .'
                '      timeout: 30'
                '---'
                ''
                '## User Input'
                ''
                '```text'
                '$ARGUMENTS'
                '```'
                ''
                '## Fixture'
                ''
                'Disposable terminal preservation evidence only.'
        )
        [string]::Join("`n", $terminalCommandLines) | Set-Content (Join-Path $extensionRoot 'commands/speckit.agent-preservation-canary.terminal.md') -Encoding utf8NoBOM

    Invoke-Checked 'specify' @('init', '--here', '--force', '--integration', 'copilot', '--script', 'ps', '--ignore-agent-tools') $fixtureRoot
    Invoke-Checked 'specify' @('extension', 'add', $extensionRoot, '--dev') $fixtureRoot

    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YamlDotNet" Version="18.1.0" />
  </ItemGroup>
</Project>
'@ | Set-Content (Join-Path $harnessRoot 'Canary.csproj') -Encoding utf8NoBOM

    @'
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;

if (args.Length != 5) throw new ArgumentException("Expected fixture, extension, schema, report, and Spec Kit version arguments.");
var fixture = Path.GetFullPath(args[0]);
var extension = Path.GetFullPath(args[1]);
var schemaPath = Path.GetFullPath(args[2]);
var reportPath = Path.GetFullPath(args[3]);
var specKitVersion = args[4];
var ids = new[] { "speckit.agent-preservation-canary.rich", "speckit.agent-preservation-canary.terminal" };
var required = new[] { "name", "description", "argument-hint", "target", "user-invocable", "disable-model-invocation", "tools", "agents", "hooks" };
var forbiddenAgent = new[] { "infer", "model" };
var mismatches = new List<object>();
var digests = new Dictionary<string, string>();
var deserializer = new DeserializerBuilder().Build();

Dictionary<object, object> Frontmatter(string path)
{
    if (!File.Exists(path))
    {
        mismatches.Add(new { path, issue = "missing-file" });
        return new();
    }
    var text = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
    digests[path] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    var lines = text.Split('\n');
    if (lines.Length < 3 || lines[0].Trim() != "---")
    {
        mismatches.Add(new { path, issue = "missing-frontmatter" });
        return new();
    }
    var end = Array.FindIndex(lines, 1, line => line.Trim() == "---");
    if (end < 0)
    {
        mismatches.Add(new { path, issue = "unterminated-frontmatter" });
        return new();
    }
    return deserializer.Deserialize<Dictionary<object, object>>(string.Join("\n", lines[1..end])) ?? new();
}

object? Canonical(object? value) => value switch
{
    IDictionary<object, object> map => map.OrderBy(k => k.Key.ToString(), StringComparer.Ordinal)
        .ToDictionary(k => k.Key.ToString()!, k => Canonical(k.Value), StringComparer.Ordinal),
    IEnumerable<object> sequence when value is not string => sequence.Select(Canonical).ToArray(),
    _ => value
};

string JsonValue(object? value) => JsonSerializer.Serialize(Canonical(value));

foreach (var id in ids)
{
    var file = id + ".md";
    var sourcePath = Path.Combine(extension, "commands", file);
    var installedCommandPath = Path.Combine(fixture, ".specify", "extensions", "agent-preservation-canary", "commands", file);
    var agentPath = Path.Combine(fixture, ".github", "agents", id + ".agent.md");
    var promptPath = Path.Combine(fixture, ".github", "prompts", id + ".prompt.md");
    var source = Frontmatter(sourcePath);
    var installed = Frontmatter(installedCommandPath);
    var agent = Frontmatter(agentPath);
    var prompt = Frontmatter(promptPath);

    foreach (var field in required)
    {
        if (!source.ContainsKey(field)) mismatches.Add(new { id, surface = "source", field, issue = "required-field-missing" });
        if (!installed.ContainsKey(field)) mismatches.Add(new { id, surface = "installed-command", field, issue = "required-field-missing" });
        if (!agent.ContainsKey(field)) mismatches.Add(new { id, surface = "generated-agent", field, issue = "required-field-missing" });
        if (source.TryGetValue(field, out var expected))
        {
            if (installed.TryGetValue(field, out var installedValue) && JsonValue(expected) != JsonValue(installedValue))
                mismatches.Add(new { id, surface = "installed-command", field, issue = "value-rewritten", expected = Canonical(expected), actual = Canonical(installedValue) });
            if (agent.TryGetValue(field, out var agentValue) && JsonValue(expected) != JsonValue(agentValue))
                mismatches.Add(new { id, surface = "generated-agent", field, issue = "value-rewritten", expected = Canonical(expected), actual = Canonical(agentValue) });
        }
    }

    foreach (var field in forbiddenAgent)
    {
        if (source.ContainsKey(field)) mismatches.Add(new { id, surface = "source", field, issue = "forbidden-field-present" });
        if (installed.ContainsKey(field)) mismatches.Add(new { id, surface = "installed-command", field, issue = "forbidden-field-present" });
        if (agent.ContainsKey(field)) mismatches.Add(new { id, surface = "generated-agent", field, issue = "forbidden-field-present" });
    }

    var shouldHaveHandoffs = id == "speckit.agent-preservation-canary.rich";
    foreach (var (surfaceName, surface) in new[] { ("source", source), ("installed-command", installed), ("generated-agent", agent) })
    {
        if (surface.ContainsKey("handoffs") != shouldHaveHandoffs)
            mismatches.Add(new { id, surface = surfaceName, field = "handoffs", issue = shouldHaveHandoffs ? "required-field-missing" : "forbidden-terminal-field-present" });
        if (surface.TryGetValue("handoffs", out var handoffs) && handoffs is IEnumerable<object> sequence)
        {
            foreach (var handoff in sequence.OfType<IDictionary<object, object>>())
            {
                foreach (var field in new[] { "label", "agent", "prompt", "send" })
                    if (!handoff.ContainsKey(field)) mismatches.Add(new { id, surface = surfaceName, field = "handoffs[]." + field, issue = "required-field-missing" });
                if (handoff.ContainsKey("model")) mismatches.Add(new { id, surface = surfaceName, field = "handoffs[].model", issue = "forbidden-field-present" });
            }
        }
    }

    if (!prompt.TryGetValue("agent", out var selected) || selected?.ToString() != id)
        mismatches.Add(new { id, surface = "generated-prompt", field = "agent", issue = "missing-or-wrong-agent", actual = selected?.ToString() });
    if (prompt.ContainsKey("tools"))
        mismatches.Add(new { id, surface = "generated-prompt", field = "tools", issue = "forbidden-field-present" });
}

var schemaText = File.ReadAllText(schemaPath, Encoding.UTF8);
digests[schemaPath] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schemaText))).ToLowerInvariant();
var report = new
{
    verdict = mismatches.Count == 0 ? "PASS" : "FAIL",
    policy = "No EngLoop-owned alternate generator and no post-processing fallback.",
    specKitVersion,
    yamlDotNetVersion = typeof(Deserializer).Assembly.GetName().Version?.ToString(),
    schemaPath,
    digests,
    mismatches
};
Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
return mismatches.Count == 0 ? 0 : 1;
'@ | Set-Content (Join-Path $harnessRoot 'Program.cs') -Encoding utf8NoBOM

    Invoke-Checked 'dotnet' @('run', '--project', (Join-Path $harnessRoot 'Canary.csproj'), '--', $fixtureRoot, $extensionRoot, $schemaPath, $reportPath, $SpecKitVersion) $repositoryRoot
    $passed = $true
}
catch {
    if (-not (Test-Path $reportPath)) {
        [ordered]@{
            verdict = 'FAIL'
            policy = 'No EngLoop-owned alternate generator and no post-processing fallback.'
            specKitVersion = $SpecKitVersion
            vscodeVersion = $VsCodeVersion
            vscodeCommit = $VsCodeCommit
            failure = $_.Exception.Message
        } | ConvertTo-Json -Depth 20 | Set-Content $reportPath -Encoding utf8NoBOM
    }
    Write-Error "Spec Kit agent preservation canary failed. Evidence retained at $reportPath. $($_.Exception.Message)"
    exit 1
}
finally {
    if ($passed) {
        Remove-Item $fixtureRoot, $extensionRoot, $harnessRoot -Recurse -Force
    }
}

Write-Output "SPEC_KIT_AGENT_PRESERVATION_PASS report=$reportPath"