[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('analysis', 'implementation')]
    [string]$Mode,

    [Parameter(Mandatory = $true)]
    [ValidateSet('UserPromptSubmit', 'PreToolUse', 'Stop')]
    [string]$Event
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TokenEfficiencyPolicy.ps1')

try {
    $hook = Get-HookInput
    $session = Get-SafeSessionId $hook

    if ($Event -eq 'UserPromptSubmit') {
        $root = Get-RepositoryRoot $hook
        $stage = if ($Mode -eq 'analysis') { 'speckit.engloop.30-token-efficiency-analyze' } else { 'speckit.engloop.31-token-efficiency-implement' }
        $entryGatePath = Assert-TokenEfficiencyPromptEntry $root $stage $hook
        if ($Mode -eq 'analysis') { Remove-Item -LiteralPath $entryGatePath -Force }
        $gateDirectory = Join-Path $root '.engloop/out/token-efficiency/gates'
        & git -C $root check-ignore -q --no-index -- .engloop/out/token-efficiency/.elk-probe
        if ($LASTEXITCODE -ne 0) { throw '.engloop/out/token-efficiency is not ignored' }
        New-Item -ItemType Directory -Path $gateDirectory -Force | Out-Null
        if ($Mode -eq 'analysis') {
            $gatePath = Join-Path $gateDirectory ($session + '.analysis.json')
            if (Test-Path -LiteralPath $gatePath -PathType Leaf) {
                $gate = Get-Content -LiteralPath $gatePath -Raw | ConvertFrom-Json
                if ([string]$gate.schemaVersion -ne '1.0' -or [string]$gate.mode -ne 'analysis' -or [string]$gate.sessionId -ne $session) {
                    throw 'analysis-guard-state-invalid'
                }
                $artifactPath = [string]$gate.artifactPath
                if (-not [string]::IsNullOrWhiteSpace($artifactPath)) {
                    if ($artifactPath -notmatch '^\.engloop/evidence/token-efficiency-analysis-[A-Za-z0-9._-]+\.json$') {
                        throw 'analysis-guard-artifact-path-invalid'
                    }
                    [void](Resolve-PolicyPath $root $artifactPath $false)
                }
            }
            else {
                $gate = [ordered]@{ schemaVersion = '1.0'; mode = 'analysis'; sessionId = $session; artifactPath = $null }
                [IO.File]::WriteAllText($gatePath, (ConvertTo-CompactJson $gate), (New-Object Text.UTF8Encoding($false)))
            }
        }
        $marker = if ($Mode -eq 'analysis') { 'TOKEN_EFFICIENCY_ANALYSIS_GUARD_ACTIVE' } else { 'TOKEN_EFFICIENCY_IMPLEMENTATION_GUARD_LOADED' }
        Write-HookResult $true '' "$marker session=$session activation=UserPromptSubmit"
        exit 0
    }

    $root = Get-RepositoryRoot $hook
    $gateDirectory = Join-Path $root '.engloop/out/token-efficiency/gates'
    $gatePath = Join-Path $gateDirectory ($session + '.json')
    $analysisGatePath = Join-Path $gateDirectory ($session + '.analysis.json')

    if ($Event -eq 'Stop') {
        if ($Mode -eq 'analysis') {
            if (-not (Test-Path -LiteralPath $analysisGatePath -PathType Leaf)) {
                Write-HookResult $false 'Agent 30 guard state is missing.' ''
                exit 0
            }
            $state = Get-Content -LiteralPath $analysisGatePath -Raw | ConvertFrom-Json
            if ([string]::IsNullOrWhiteSpace([string]$state.artifactPath) -or
                -not (Test-Path -LiteralPath (Join-Path $root ([string]$state.artifactPath)) -PathType Leaf)) {
                Write-HookResult $false 'Agent 30 cannot finish without its single validated analysis JSON.' ''
                exit 0
            }
            Remove-Item -LiteralPath $analysisGatePath -Force
            Write-HookResult $true '' ''
            exit 0
        }
        if (-not (Test-Path -LiteralPath $gatePath -PathType Leaf)) {
            Write-HookResult $false 'Agent 31 scope gate is missing at Stop.' ''
            exit 0
        }
        $gate = Get-Content -LiteralPath $gatePath -Raw | ConvertFrom-Json
        $evidencePath = Join-Path $root ([string]$gate.implementationEvidencePath)
        if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) {
            Write-HookResult $false 'Agent 31 implementation evidence is missing.' ''
            exit 0
        }
        $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
        $repairStatus = @($evidence.repairStatus)
        $statusById = @{}
        foreach ($status in $repairStatus) { $statusById[[string]$status.id] = [string]$status.status }
        $allRepairsTerminal = $true
        foreach ($repairId in @($gate.approvedRepairIds)) {
            if (-not $statusById.ContainsKey([string]$repairId) -or $statusById[[string]$repairId] -notin @('implemented', 'blocked', 'failed')) {
                $allRepairsTerminal = $false
            }
        }
        $actualFinalDigest = Get-GitStatusDigest $root @([string]$gate.analysisPath, [string]$gate.implementationEvidencePath)
        if ([string]$evidence.schemaVersion -ne '1.0' -or
            [string]$evidence.artifactType -ne 'token-efficiency-implementation' -or
            [string]$evidence.outcome -notin @('passed', 'blocked', 'failed') -or
            [string]$evidence.analysis.sha256 -ne [string]$gate.analysisSha256 -or
            [string]$evidence.analysis.analysisId -ne [string]$gate.analysisId -or
            (@($evidence.approvedRepairIds) -join ',') -ne (@($gate.approvedRepairIds) -join ',') -or
            -not $allRepairsTerminal -or
            ([string]$evidence.outcome -eq 'passed' -and @($gate.approvedRepairIds | Where-Object { $statusById[[string]$_] -ne 'implemented' }).Count -ne 0) -or
            ([string]$evidence.outcome -in @('blocked', 'failed') -and $null -eq $evidence.failure) -or
            [string]$evidence.sourceState.initialHead -ne [string]$gate.head -or
            [string]$evidence.sourceState.finalHead -ne [string]$gate.head -or
            [string]$evidence.sourceState.initialStatusDigest -ne [string]$gate.statusDigest -or
            [string]$evidence.sourceState.finalStatusDigest -ne $actualFinalDigest) {
            Write-HookResult $false 'Agent 31 implementation evidence is incomplete, stale, or still in progress.' ''
            exit 0
        }
        Remove-Item -LiteralPath $gatePath -Force
        Write-HookResult $true '' ''
        exit 0
    }

    $toolName = Get-ToolName $hook
    $toolInput = Get-ToolInput $hook
    if ([string]::IsNullOrWhiteSpace($toolName)) { throw 'tool-name-missing' }

    if ($toolName -match '(?i)(read|search|grep|semantic|usage|sessionstore|session_store|agent)') {
        Write-HookResult $true '' ''
        exit 0
    }

    if ($Mode -eq 'analysis') {
        if (-not (Test-Path -LiteralPath $analysisGatePath -PathType Leaf)) {
            Write-HookResult $false 'Agent 30 guard state is absent.' ''
            exit 0
        }
        if ($toolName -match '(?i)(terminal|execute|run|task|test)') {
            $command = Get-ToolCommand $toolInput
            $safe = $command -match '^(?:git (?:status --short|rev-parse HEAD|diff --stat|diff --name-only)|where(?:\.exe)? [A-Za-z0-9_.-]+|[A-Za-z0-9_.\\/-]+ --version|pwsh -NoProfile -File \.specify/extensions/engloop/scripts/(?:Resolve-DeclaredToolchain|Get-TokenEfficiencySourceState)\.ps1 .+)$'
            if (-not $safe -or $command -match '[;&|><\r\n]') {
                Write-HookResult $false 'Agent 30 permits only one bounded read-only probe or the shipped declared-toolchain preflight.' ''
                exit 0
            }
            Write-HookResult $true '' ''
            exit 0
        }

        if ($toolName -notmatch '(?i)(create.*file|file.*create)') {
            Write-HookResult $false 'Agent 30 is read-only except create-new token-efficiency analysis JSON.' ''
            exit 0
        }

        $paths = @(Get-ToolPaths $toolInput)
        if ($paths.Count -ne 1) {
            Write-HookResult $false 'Agent 30 analysis write requires exactly one explicit file path.' ''
            exit 0
        }
        $relative = Convert-ToolPathToRelative $root $paths[0]
        $state = Get-Content -LiteralPath $analysisGatePath -Raw | ConvertFrom-Json
        if (-not [string]::IsNullOrWhiteSpace([string]$state.artifactPath)) {
            Write-HookResult $false 'Agent 30 permits exactly one evidence artifact per session.' ''
            exit 0
        }
        if ($relative -notmatch '^\.engloop/evidence/token-efficiency-analysis-[A-Za-z0-9._-]+\.json$') {
            Write-HookResult $false 'Agent 30 may write only .engloop/evidence/token-efficiency-analysis-<unique-id>.json.' ''
            exit 0
        }
        $resolved = Resolve-PolicyPath $root $relative $true
        if (Test-Path -LiteralPath $resolved.Full) {
            Write-HookResult $false 'Agent 30 evidence is create-new; the selected path already exists.' ''
            exit 0
        }
        $content = [string](Get-PropertyValue $toolInput @('content', 'text', 'value'))
        if ([string]::IsNullOrWhiteSpace($content) -or $content.Length -gt 131072) {
            Write-HookResult $false 'Agent 30 evidence content is missing or exceeds 128 KiB.' ''
            exit 0
        }
        try { $analysis = $content | ConvertFrom-Json } catch {
            Write-HookResult $false 'Agent 30 evidence must be valid JSON.' ''
            exit 0
        }
        [void](Test-AnalysisObject $analysis)
        $suffix = [IO.Path]::GetFileNameWithoutExtension($relative).Substring('token-efficiency-analysis-'.Length)
        if (-not [string]::Equals($suffix, [string]$analysis.analysisId, [StringComparison]::Ordinal)) {
            Write-HookResult $false 'Agent 30 evidence filename suffix must equal analysisId.' ''
            exit 0
        }
        $head = Get-GitHead $root
        if (-not [string]::Equals($head, [string]$analysis.sourceState.head, [StringComparison]::OrdinalIgnoreCase)) {
            Write-HookResult $false 'Agent 30 sourceState.head must equal current HEAD.' ''
            exit 0
        }
        $digest = Get-GitStatusDigest $root @($relative)
        if (-not [string]::Equals($digest, [string]$analysis.sourceState.gitStatusDigest, [StringComparison]::Ordinal)) {
            Write-HookResult $false 'Agent 30 sourceState.gitStatusDigest is stale or malformed.' ''
            exit 0
        }
        $state.artifactPath = $relative
        [IO.File]::WriteAllText($analysisGatePath, (ConvertTo-CompactJson $state), (New-Object Text.UTF8Encoding($false)))
        Write-HookResult $true '' ''
        exit 0
    }

    if (-not (Test-Path -LiteralPath $gatePath -PathType Leaf)) {
        Write-HookResult $false 'Agent 31 scope gate is absent. Submit --analysis and explicit --approve repair IDs first.' ''
        exit 0
    }
    $gate = Get-Content -LiteralPath $gatePath -Raw | ConvertFrom-Json
    if ([string]$gate.sessionId -ne $session -or [string]$gate.head -ne (Get-GitHead $root)) {
        Write-HookResult $false 'Agent 31 scope gate is stale or belongs to another session/HEAD.' ''
        exit 0
    }

    if ($toolName -match '(?i)(terminal|execute|run|task|test)') {
        $command = Get-ToolCommand $toolInput
        if ([string]::IsNullOrWhiteSpace($command) -or
            $command -match '(?i)(^|\s)(git\s+(commit|push)|deploy|release\s+create|install\s+-g|--global)(\s|$)' -or
            -not (@($gate.allowedCommands) -contains $command)) {
            Write-HookResult $false 'Agent 31 command is not an exact approved validation/preflight command or is permanently forbidden.' ''
            exit 0
        }
        Write-HookResult $true '' ''
        exit 0
    }

    if ($toolName -notmatch '(?i)(edit|create|replace|patch|delete|write|file)') {
        Write-HookResult $false 'Agent 31 tool is outside its read/search/edit/approved-command scope.' ''
        exit 0
    }

    $paths = @(Get-ToolPaths $toolInput)
    if ($paths.Count -eq 0) {
        Write-HookResult $false 'Agent 31 edit did not expose a path for scope validation.' ''
        exit 0
    }
    foreach ($path in $paths) {
        $relative = Convert-ToolPathToRelative $root $path
        if (-not (Test-PathAllowed $relative @($gate.allowedPaths) @($gate.prohibitedPaths))) {
            Write-HookResult $false "Agent 31 path is outside approved scope: $relative" ''
            exit 0
        }
        [void](Resolve-PolicyPath $root $relative $true)
    }
    Write-HookResult $true '' ''
}
catch {
    Write-HookResult $false ("Token-efficiency guard failed closed: " + $_.Exception.Message) ''
}
