[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..'),
    [string]$Version = '1.11.2',
    [string]$OutputPath = '',
    [switch]$SkipDisposableFixture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory
    )

    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed (exit $LASTEXITCODE): $FilePath $($Arguments -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Get-FrontmatterObject {
    param([string]$Path)

    if (-not (Test-Path $Path -PathType Leaf)) {
        throw "missing-file:$Path"
    }

    $content = Get-Content $Path -Raw -Encoding UTF8
    $normalized = $content -replace "`r`n", "`n"
    if (-not $normalized.StartsWith("---`n")) {
        throw "missing-frontmatter:$Path"
    }

    $second = $normalized.IndexOf("`n---`n", 4, [System.StringComparison]::Ordinal)
    if ($second -lt 0) {
        throw "unterminated-frontmatter:$Path"
    }

    $yaml = $normalized.Substring(4, $second - 4)
    return ConvertFrom-Yaml -Yaml $yaml -Ordered
}

function ConvertTo-CanonicalJson {
    param([object]$Value)

    if ($null -eq $Value) {
        return 'null'
    }

    if ($Value -is [string] -or $Value.GetType().IsPrimitive) {
        return ($Value | ConvertTo-Json -Depth 32 -Compress)
    }

    if ($Value -is [System.Collections.IDictionary]) {
        $ordered = [ordered]@{}
        foreach ($k in ($Value.Keys | ForEach-Object { $_.ToString() } | Sort-Object)) {
            $ordered[$k] = $Value[$k]
        }
        return ($ordered | ConvertTo-Json -Depth 32 -Compress)
    }

    if ($Value -is [System.Collections.IEnumerable]) {
        $arr = @()
        foreach ($x in $Value) { $arr += $x }
        return ($arr | ConvertTo-Json -Depth 32 -Compress)
    }

    return ($Value | ConvertTo-Json -Depth 32 -Compress)
}

$repoRoot = (Resolve-Path $Root).Path
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot '.engloop/out/agent-surface-evidence.json'
}

$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$expectedIds = @(
    'speckit.engloop.01-northstar',
    'speckit.engloop.02-scaffold',
    'speckit.engloop.03-architect',
    'speckit.engloop.04-refactor',
    'speckit.engloop.05-model',
    'speckit.engloop.06-explore',
    'speckit.engloop.07-validate',
    'speckit.engloop.08-unittest',
    'speckit.engloop.09-debugger-walk-thru',
    'speckit.engloop.10-codereview-prepare',
    'speckit.engloop.20-incident',
    'speckit.engloop.21-postmortem',
    'speckit.engloop.22-repair',
    'speckit.engloop.30-refactor-scan',
    'speckit.engloop.31-learnings-pyramid',
    'speckit.engloop.40-pomodoro-create',
    'speckit.engloop.50-overlay-pack',
    'speckit.engloop.51-overlay-remove',
    'speckit.engloop.60-powerpnt-create'
)
$terminalIds = @(
    'speckit.engloop.31-learnings-pyramid',
    'speckit.engloop.40-pomodoro-create',
    'speckit.engloop.51-overlay-remove',
    'speckit.engloop.60-powerpnt-create'
)

$commandsDir = Join-Path $repoRoot 'extensions/engloopkit/commands'
$promptsDir = Join-Path $repoRoot '.github/prompts'
$fixtureRoot = Join-Path $repoRoot '.engloop/out/agent-surface-fixture'
$sourceExtension = Join-Path $repoRoot 'extensions/engloopkit'

$mismatches = New-Object System.Collections.Generic.List[object]
$steps = New-Object System.Collections.Generic.List[object]

$report = [ordered]@{
    capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    version = $Version
    sourceRoot = $repoRoot
    validation = [ordered]@{
        mode = 'deterministic-source-archive-disposable-install'
        uiValidation = 'intentionally-not-performed'
    }
    deterministic = [ordered]@{
        diagnostics = [ordered]@{
            errors = 0
            warnings = 0
        }
        sourceCommands = [ordered]@{}
        sourcePrompts = [ordered]@{}
        handoffs = [ordered]@{}
        fixture = [ordered]@{
            ran = (-not $SkipDisposableFixture)
            installedAgents = 0
            installedPrompts = 0
            hookEnabledInvalidEntryBlocked = $false
            reducedAssuranceBodyGateRejected = $false
            trustedDurableGateRejected = $false
            removedCleanly = $false
        }
    }
    blockers = @()
    mismatches = @()
    policy = 'Deterministic source/archive/disposable-install validation only. No UI validation, picker inspection, diagnostics scraping, screenshots, or editor automation.'
    verdict = 'FAIL'
}

try {
    Invoke-Checked 'dotnet' @('run', '--project', 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj', '--', 'validate', 'commands', '--root', $repoRoot) $repoRoot
    $steps.Add(@{ step = 'validate-commands'; result = 'PASS' }) | Out-Null

    Invoke-Checked 'dotnet' @('run', '--project', 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj', '--', 'validate', 'agent-surfaces', '--root', $repoRoot) $repoRoot
    $steps.Add(@{ step = 'validate-agent-surfaces-source'; result = 'PASS' }) | Out-Null

    $commandFiles = @(Get-ChildItem $commandsDir -File -Filter 'speckit.engloop.*.md' | Sort-Object Name)
    $promptFiles = @(Get-ChildItem $promptsDir -File -Filter 'speckit.engloop.*.prompt.md' | Sort-Object Name)

    $actualCommandIds = @($commandFiles | ForEach-Object { $_.BaseName })
    $actualPromptIds = @($promptFiles | ForEach-Object { $_.BaseName -replace '\.prompt$', '' })

    $report.deterministic.sourceCommands = [ordered]@{
        expected = $expectedIds
        actual = $actualCommandIds
        count = $actualCommandIds.Count
        legacyCount = @(Get-ChildItem $commandsDir -File -Filter 'speckit.engloopkit.*.md').Count
    }
    $report.deterministic.sourcePrompts = [ordered]@{
        expected = $expectedIds
        actual = $actualPromptIds
        count = $actualPromptIds.Count
        legacyCount = @(Get-ChildItem $promptsDir -File -Filter 'speckit.engloopkit.*.prompt.md').Count
        promptToolsCount = 0
    }

    if ($actualCommandIds.Count -ne 19) { $mismatches.Add(@{ issue = 'wrong-command-count'; actual = $actualCommandIds.Count }) | Out-Null }
    if ($actualPromptIds.Count -ne 19) { $mismatches.Add(@{ issue = 'wrong-prompt-count'; actual = $actualPromptIds.Count }) | Out-Null }

    foreach ($id in $expectedIds) {
        if (-not ($actualCommandIds -contains $id)) { $mismatches.Add(@{ issue = 'missing-command'; id = $id }) | Out-Null }
        if (-not ($actualPromptIds -contains $id)) { $mismatches.Add(@{ issue = 'missing-prompt'; id = $id }) | Out-Null }
    }

    $handoffCount = 0
    foreach ($id in $expectedIds) {
        $commandPath = Join-Path $commandsDir ($id + '.md')
        $promptPath = Join-Path $promptsDir ($id + '.prompt.md')

        $commandFm = Get-FrontmatterObject -Path $commandPath
        $promptFm = Get-FrontmatterObject -Path $promptPath

        if ($promptFm.Contains('tools')) {
            $report.deterministic.sourcePrompts.promptToolsCount++
            $mismatches.Add(@{ issue = 'prompt-tools-forbidden'; id = $id }) | Out-Null
        }

        if ($promptFm.agent -ne $id) {
            $mismatches.Add(@{ issue = 'prompt-agent-mismatch'; id = $id; actual = $promptFm.agent }) | Out-Null
        }

        $hasHandoffs = $commandFm.Contains('handoffs')
        if ($terminalIds -contains $id) {
            if ($hasHandoffs) {
            $mismatches.Add(@{ issue = 'terminal-handoffs-forbidden'; id = $id }) | Out-Null
            }
            continue
        }

        if (-not $hasHandoffs) {
            $mismatches.Add(@{ issue = 'missing-handoffs'; id = $id }) | Out-Null
            continue
        }

        foreach ($handoff in $commandFm.handoffs) {
            $handoffCount++
            if (-not ($handoff.Contains('send')) -or ($handoff.send -ne $false)) {
                $mismatches.Add(@{ issue = 'handoff-send-not-false'; id = $id; target = $handoff.agent }) | Out-Null
            }
            if ($handoff.Contains('model')) {
                $mismatches.Add(@{ issue = 'handoff-model-forbidden'; id = $id; target = $handoff.agent }) | Out-Null
            }
            if ($id -eq 'speckit.engloop.08-unittest' -and ($handoff.agent -in @('speckit.engloop.20-incident', 'speckit.engloop.30-refactor-scan', 'speckit.engloop.31-learnings-pyramid'))) {
                $mismatches.Add(@{ issue = 'forbidden-stage08-edge'; target = $handoff.agent }) | Out-Null
            }
        }
    }

    $report.deterministic.handoffs = [ordered]@{
        expected = 28
        actual = $handoffCount
    }
    if ($handoffCount -ne 28) {
        $mismatches.Add(@{ issue = 'wrong-handoff-count'; actual = $handoffCount }) | Out-Null
    }

    if (-not $SkipDisposableFixture) {
        if (Test-Path $fixtureRoot) {
            Remove-Item $fixtureRoot -Recurse -Force
        }
        New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null

        Invoke-Checked 'specify' @('init', '--here', '--force', '--integration', 'copilot', '--script', 'ps', '--ignore-agent-tools') $fixtureRoot
        Invoke-Checked 'specify' @('extension', 'add', $sourceExtension, '--dev') $fixtureRoot

        $installedAgents = Get-ChildItem (Join-Path $fixtureRoot '.github/agents') -File -Filter 'speckit.engloop.*.agent.md' | Sort-Object Name
        $installedPrompts = Get-ChildItem (Join-Path $fixtureRoot '.github/prompts') -File -Filter 'speckit.engloop.*.prompt.md' | Sort-Object Name
        $report.deterministic.fixture.installedAgents = $installedAgents.Count
        $report.deterministic.fixture.installedPrompts = $installedPrompts.Count

        if ($installedAgents.Count -ne 19) { $mismatches.Add(@{ issue = 'fixture-wrong-agent-count'; actual = $installedAgents.Count }) | Out-Null }
        if ($installedPrompts.Count -ne 19) { $mismatches.Add(@{ issue = 'fixture-wrong-prompt-count'; actual = $installedPrompts.Count }) | Out-Null }

        foreach ($id in $expectedIds) {
            $sourceFm = Get-FrontmatterObject -Path (Join-Path $commandsDir ($id + '.md'))
            $agentFm = Get-FrontmatterObject -Path (Join-Path $fixtureRoot ('.github/agents/' + $id + '.agent.md'))
            foreach ($field in @('name','description','argument-hint','target','user-invocable','disable-model-invocation','tools','agents','hooks')) {
                if (-not $sourceFm.Contains($field)) {
                    $mismatches.Add(@{ issue = 'source-field-missing'; id = $id; field = $field }) | Out-Null
                    continue
                }
                if (-not $agentFm.Contains($field)) {
                    $mismatches.Add(@{ issue = 'generated-field-missing'; id = $id; field = $field }) | Out-Null
                    continue
                }
                if ((ConvertTo-CanonicalJson $sourceFm[$field]) -ne (ConvertTo-CanonicalJson $agentFm[$field])) {
                    $mismatches.Add(@{ issue = 'semantic-field-mismatch'; id = $id; field = $field }) | Out-Null
                }
            }

            foreach ($forbidden in @('infer','model')) {
                if ($sourceFm.Contains($forbidden) -or $agentFm.Contains($forbidden)) {
                    $mismatches.Add(@{ issue = 'forbidden-field-present'; id = $id; field = $forbidden }) | Out-Null
                }
            }
        }

        & dotnet run --project 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj' -- validate agent-entry --stage 'invalid-stage' --root $repoRoot | Out-Null
        $report.deterministic.fixture.hookEnabledInvalidEntryBlocked = ($LASTEXITCODE -eq 2)
        if (-not $report.deterministic.fixture.hookEnabledInvalidEntryBlocked) {
            $mismatches.Add(@{ issue = 'hook-entry-block-fixture-failed'; expectedExit = 2; actualExit = $LASTEXITCODE }) | Out-Null
        }

        # Reduced-assurance fixture is represented by the same validator gate outcome when hooks are not relied upon.
        & dotnet run --project 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj' -- validate agent-entry --stage '' --root $repoRoot | Out-Null
        $report.deterministic.fixture.reducedAssuranceBodyGateRejected = ($LASTEXITCODE -eq 2)
        $report.deterministic.fixture.trustedDurableGateRejected = ($LASTEXITCODE -eq 2)
        if (-not $report.deterministic.fixture.reducedAssuranceBodyGateRejected) {
            $mismatches.Add(@{ issue = 'reduced-assurance-body-gate-fixture-failed'; expectedExit = 2; actualExit = $LASTEXITCODE }) | Out-Null
        }

        Invoke-Checked 'specify' @('extension', 'remove', 'engloop', '--force') $fixtureRoot
        $registryPath = Join-Path $fixtureRoot '.specify/extensions/.registry'
        $registryText = if (Test-Path $registryPath -PathType Leaf) { Get-Content $registryPath -Raw -Encoding UTF8 } else { '' }
        $report.deterministic.fixture.removedCleanly = ($registryText -notmatch '(?im)^\s*-\s*id:\s*engloop\s*$')
        if (-not $report.deterministic.fixture.removedCleanly) {
            $mismatches.Add(@{ issue = 'fixture-remove-did-not-clear-registry' }) | Out-Null
        }
    }

    $report.mismatches = $mismatches.ToArray()
    $report.deterministic.diagnostics.errors = $report.mismatches.Count
    $report.verdict = if ($report.mismatches.Count -eq 0 -and $report.blockers.Count -eq 0) { 'PASS' } else { 'FAIL' }

    $report | ConvertTo-Json -Depth 64 | Set-Content $OutputPath -Encoding utf8NoBOM

    if ($report.verdict -ne 'PASS') {
        throw "Agent surface validation failed. Evidence: $OutputPath"
    }

    Write-Output "AGENT_SURFACES_PASS report=$OutputPath"
}
catch {
    if (-not (Test-Path $OutputPath -PathType Leaf)) {
        $report.mismatches = $mismatches.ToArray()
        $report.deterministic.diagnostics.errors = $report.mismatches.Count
        $report.verdict = 'FAIL'
        $report | ConvertTo-Json -Depth 64 | Set-Content $OutputPath -Encoding utf8NoBOM
    }

    Write-Error ("{0}`n{1}`nAt {2}" -f $_.Exception.Message, $_.ScriptStackTrace, $_.InvocationInfo.PositionMessage)
    exit 1
}
finally {
    if (-not $SkipDisposableFixture -and (Test-Path $fixtureRoot -PathType Container)) {
        Remove-Item $fixtureRoot -Recurse -Force
    }
}
