[CmdletBinding()]
param(
    [string]$RepositoryRoot = '',
    [string]$Prompt = '',
    [string]$SessionId = ''
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TokenEfficiencyPolicy.ps1')

function Get-ArgumentValue {
    param([string]$Text, [string]$Name)
    $escaped = [regex]::Escape($Name)
    $match = [regex]::Match($Text, '(?:^|\s)' + $escaped + '(?:=|\s+)(?:"(?<dq>[^"]+)"|''(?<sq>[^'']+)''|(?<bare>[^\s]+))')
    if (-not $match.Success) { return '' }
    foreach ($group in @('dq', 'sq', 'bare')) {
        if ($match.Groups[$group].Success) { return $match.Groups[$group].Value }
    }
    return ''
}

try {
    $hook = if ([string]::IsNullOrWhiteSpace($Prompt)) { Get-HookInput } else { [pscustomobject]@{} }
    if ([string]::IsNullOrWhiteSpace($Prompt)) { $Prompt = [string](Get-PropertyValue $hook @('prompt')) }
    if ([string]::IsNullOrWhiteSpace($SessionId)) { $SessionId = Get-SafeSessionId $hook } else { $SessionId = [regex]::Replace($SessionId, '[^A-Za-z0-9._-]', '_') }
    $root = Get-RepositoryRoot $hook $RepositoryRoot

    $existingGatePath = Join-Path (Join-Path $root '.engloop/out/token-efficiency/gates') ($SessionId + '.json')
    if (Test-Path -LiteralPath $existingGatePath -PathType Leaf) {
        $existingGate = Get-Content -LiteralPath $existingGatePath -Raw | ConvertFrom-Json
        if ([string]$existingGate.sessionId -eq $SessionId -and [string]$existingGate.head -eq (Get-GitHead $root)) {
            Write-HookResult $true '' "TOKEN_EFFICIENCY_IMPLEMENTATION_SCOPE_ACTIVE gate=$existingGatePath evidence=$($existingGate.implementationEvidencePath) repairs=$(@($existingGate.approvedRepairIds) -join ',')"
            exit 0
        }
        throw 'existing-scope-gate-is-stale'
    }

    $analysisArgument = Get-ArgumentValue $Prompt '--analysis'
    $approveArgument = Get-ArgumentValue $Prompt '--approve'
    if ([string]::IsNullOrWhiteSpace($analysisArgument) -or [string]::IsNullOrWhiteSpace($approveArgument)) {
        throw 'Agent 31 requires --analysis <path> and --approve <TE-Rxxx,...> in this prompt.'
    }
    if ($approveArgument -match '(?i)(^|,)(all|\*)(,|$)' -or $approveArgument -match '\.\.') {
        throw 'Agent 31 approval cannot use all, wildcard, or ranges.'
    }
    $approved = @($approveArgument.Split(',', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() } | Sort-Object -Unique)
    if ($approved.Count -eq 0 -or @($approved | Where-Object { -not (Test-RepairId $_) }).Count -ne 0) {
        throw 'Agent 31 approval must contain only explicit TE-Rddd IDs.'
    }

    $analysisRelative = Normalize-PolicyPath $analysisArgument
    if ($analysisRelative -notmatch '^\.engloop/evidence/token-efficiency-analysis-[A-Za-z0-9._-]+\.json$') {
        throw 'Agent 31 analysis path is outside the governed evidence contract.'
    }
    $analysisPath = Resolve-PolicyPath $root $analysisRelative $false
    $analysis = Get-Content -LiteralPath $analysisPath.Full -Raw | ConvertFrom-Json
    [void](Test-AnalysisObject $analysis)

    $head = Get-GitHead $root
    if (-not [string]::Equals($head, [string]$analysis.sourceState.head, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Agent 31 analysis HEAD is stale.'
    }
    $statusDigest = Get-GitStatusDigest $root @($analysisRelative)
    if (-not [string]::Equals($statusDigest, [string]$analysis.sourceState.gitStatusDigest, [StringComparison]::Ordinal)) {
        throw 'Agent 31 analysis Git status digest is stale.'
    }

    $repairMap = @{}
    foreach ($repair in @($analysis.recommendedRepoRepairs)) { $repairMap[[string]$repair.id] = $repair }
    foreach ($id in $approved) {
        if (-not $repairMap.ContainsKey($id)) { throw "Agent 31 repair ID is not a repository repair in the analysis: $id" }
        foreach ($prerequisite in @($repairMap[$id].prerequisites)) {
            if ([string]$prerequisite.status -ne 'resolved') { throw "Agent 31 repair has unresolved prerequisite: $id/$($prerequisite.id)" }
        }
    }

    $allowedPaths = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::OrdinalIgnoreCase)
    $prohibitedPaths = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::OrdinalIgnoreCase)
    $allowedCommands = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::Ordinal)
    foreach ($id in $approved) {
        $repair = $repairMap[$id]
        foreach ($path in @($repair.allowedPaths)) { [void]$allowedPaths.Add((Normalize-PolicyPath ([string]$path))) }
        foreach ($path in @($repair.prohibitedPaths)) { [void]$prohibitedPaths.Add((Normalize-PolicyPath ([string]$path))) }
        foreach ($step in @($repair.validationPlan)) { [void]$allowedCommands.Add((@($step.command) -join ' ')) }
    }

    $attempt = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ')
    $revision = $head.Substring(0, 12) + '-' + $attempt
    $evidenceRelative = ".engloop/evidence/token-efficiency-implementation-$revision.json"
    [void]$allowedPaths.Add($evidenceRelative)
    [void]$allowedCommands.Add('git status --short')
    [void]$allowedCommands.Add('git diff --check')
    [void]$allowedCommands.Add('git diff --stat')

    $outRoot = Join-Path $root '.engloop/out/token-efficiency'
    New-Item -ItemType Directory -Path $outRoot -Force | Out-Null
    & git -C $root check-ignore -q --no-index -- .engloop/out/token-efficiency/.elk-probe
    if ($LASTEXITCODE -ne 0) { throw '.engloop/out/token-efficiency is not ignored; scope gate will not write logs/records.' }

    $gateDirectory = Join-Path $outRoot 'gates'
    New-Item -ItemType Directory -Path $gateDirectory -Force | Out-Null
    $gatePath = Join-Path $gateDirectory ($SessionId + '.json')
    $gate = [ordered]@{
        schemaVersion = '1.0'
        sessionId = $SessionId
        createdAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        head = $head
        statusDigest = $statusDigest
        analysisPath = $analysisRelative
        analysisSha256 = (Get-FileHash -LiteralPath $analysisPath.Full -Algorithm SHA256).Hash.ToLowerInvariant()
        analysisId = [string]$analysis.analysisId
        approvedRepairIds = $approved
        allowedPaths = @($allowedPaths | Sort-Object)
        prohibitedPaths = @($prohibitedPaths | Sort-Object)
        allowedCommands = @($allowedCommands | Sort-Object)
        implementationEvidencePath = $evidenceRelative
    }
    $initialEvidence = [ordered]@{
        schemaVersion = '1.0'
        artifactType = 'token-efficiency-implementation'
        capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        revision = $revision
        outcome = 'in-progress'
        analysis = [ordered]@{ path = $analysisRelative; sha256 = $gate.analysisSha256; analysisId = $gate.analysisId }
        approvedRepairIds = $approved
        repairStatus = @($approved | ForEach-Object { [ordered]@{ id = $_; status = 'pending'; detail = '' } })
        changedFiles = @()
        customizationDecisions = @()
        toolchainPreflight = @()
        validation = @()
        unavailableToolDecisions = @()
        failure = $null
        residualRisks = @()
        sourceState = [ordered]@{ initialHead = $head; finalHead = $head; initialStatusDigest = $statusDigest; finalStatusDigest = $statusDigest }
    }
    $evidencePath = Resolve-PolicyPath $root $evidenceRelative $true
    if (Test-Path -LiteralPath $evidencePath.Full) { throw 'implementation-evidence-collision' }
    New-Item -ItemType Directory -Path (Split-Path $evidencePath.Full -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($evidencePath.Full, (ConvertTo-CompactJson $initialEvidence), (New-Object Text.UTF8Encoding($false)))
    [IO.File]::WriteAllText($gatePath, (ConvertTo-CompactJson $gate), (New-Object Text.UTF8Encoding($false)))

    Write-HookResult $true '' "TOKEN_EFFICIENCY_IMPLEMENTATION_SCOPE_ACTIVE gate=$gatePath evidence=$evidenceRelative repairs=$($approved -join ',')"
}
catch {
    Write-HookResult $false ("Agent 31 scope initialization failed: " + $_.Exception.Message) ''
}
