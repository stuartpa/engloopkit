Set-StrictMode -Version Latest

function ConvertTo-CompactJson {
    param([Parameter(Mandatory = $true)]$Value)
    return ($Value | ConvertTo-Json -Depth 32 -Compress)
}

function Write-HookResult {
    param(
        [bool]$Continue,
        [string]$Reason,
        [string]$SystemMessage = ''
    )
    $result = [ordered]@{ continue = $Continue }
    if (-not [string]::IsNullOrWhiteSpace($Reason)) { $result.stopReason = $Reason }
    if (-not [string]::IsNullOrWhiteSpace($SystemMessage)) { $result.systemMessage = $SystemMessage }
    [Console]::Out.WriteLine((ConvertTo-CompactJson $result))
}

function Get-HookInput {
    $text = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($text)) { return [pscustomobject]@{} }
    return $text | ConvertFrom-Json
}

function Get-PropertyValue {
    param($Object, [string[]]$Names)
    foreach ($name in $Names) {
        if ($null -ne $Object -and $Object.PSObject.Properties.Name -contains $name) {
            return $Object.$name
        }
    }
    return $null
}

function Get-SafeSessionId {
    param($Hook)
    $value = [string](Get-PropertyValue $Hook @('session_id', 'sessionId'))
    if ([string]::IsNullOrWhiteSpace($value)) { return 'no-session-id' }
    return [regex]::Replace($value, '[^A-Za-z0-9._-]', '_')
}

function Get-RepositoryRoot {
    param($Hook, [string]$ExplicitRoot = '')
    $candidate = $ExplicitRoot
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = [string](Get-PropertyValue $Hook @('cwd'))
    }
    if ([string]::IsNullOrWhiteSpace($candidate)) { throw 'hook-cwd-missing' }
    $root = (& git -C $candidate rev-parse --show-toplevel 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) { throw 'git-root-unavailable' }
    $resolved = (Resolve-Path -LiteralPath $root).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
    $candidateResolved = (Resolve-Path -LiteralPath $candidate).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not [string]::Equals($resolved, $candidateResolved, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'hook-cwd-must-be-selected-git-root'
    }
    return $resolved
}

function ConvertTo-NormalizedPolicyPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $normalized = $Path.Replace('\', '/').Trim()
    if ([string]::IsNullOrWhiteSpace($normalized) -or
        [IO.Path]::IsPathRooted($normalized) -or
        $normalized -match '(^|/)\.\.(/|$)' -or
        $normalized.StartsWith('/', [StringComparison]::Ordinal) -or
        $normalized.StartsWith('.git/', [StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($normalized, '.git', [StringComparison]::OrdinalIgnoreCase)) {
        throw "invalid-policy-path:$Path"
    }
    while ($normalized.StartsWith('./', [StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }
    return $normalized
}

function Resolve-PolicyPath {
    param([string]$Root, [string]$RelativePath, [bool]$AllowMissing = $true)
    $relative = ConvertTo-NormalizedPolicyPath $RelativePath
    $full = [IO.Path]::GetFullPath((Join-Path $Root $relative))
    $boundary = $Root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) {
        throw "policy-path-escapes-root:$relative"
    }
    $cursor = if (Test-Path -LiteralPath $full) { $full } else { Split-Path $full -Parent }
    while (-not [string]::IsNullOrWhiteSpace($cursor) -and $cursor.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "policy-path-reparse-point:$relative"
            }
        }
        if ([string]::Equals($cursor, $Root, [StringComparison]::OrdinalIgnoreCase)) { break }
        $parent = Split-Path $cursor -Parent
        if ([string]::Equals($parent, $cursor, [StringComparison]::OrdinalIgnoreCase)) { break }
        $cursor = $parent
    }
    if (-not $AllowMissing -and -not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "policy-file-missing:$relative"
    }
    return [pscustomobject]@{ Relative = $relative; Full = $full }
}

function Get-GitHead {
    param([string]$Root)
    $head = (& git -C $Root rev-parse HEAD 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -notmatch '^[0-9a-fA-F]{40,64}$') { throw 'git-head-unavailable' }
    return $head.ToLowerInvariant()
}

function Get-GitStatusDigest {
    param([string]$Root, [string[]]$ExcludedPaths = @())
    $exclusions = @($ExcludedPaths | ForEach-Object { (ConvertTo-NormalizedPolicyPath $_).ToLowerInvariant() })
    $lines = @(& git -C $Root status --porcelain=v1 --untracked-files=all 2>$null)
    if ($LASTEXITCODE -ne 0) { throw 'git-status-unavailable' }
    $kept = foreach ($line in $lines) {
        if ($line.Length -lt 4) { continue }
        $path = $line.Substring(3).Trim('"').Replace('\', '/').ToLowerInvariant()
        if ($exclusions -contains $path) { continue }
        $line.Replace('\', '/')
    }
    $canonical = (@($kept | Sort-Object) -join "`n")
    $bytes = [Text.Encoding]::UTF8.GetBytes($canonical)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Test-RepairId {
    param([string]$Value, [string]$Prefix = 'TE-R')
    return $Value -match ('^' + [regex]::Escape($Prefix) + '[0-9]{3}$')
}

function Test-AnalysisObject {
    param([Parameter(Mandatory = $true)]$Analysis)
    if ([string]$Analysis.schemaVersion -ne '1.0' -or [string]$Analysis.artifactType -ne 'token-efficiency-analysis') {
        throw 'analysis-identity-invalid'
    }
    if ([string]::IsNullOrWhiteSpace([string]$Analysis.analysisId) -or [string]$Analysis.analysisId -notmatch '^[A-Za-z0-9._-]{1,160}$') {
        throw 'analysis-id-invalid'
    }
    if ($null -eq $Analysis.evidence -or $null -eq $Analysis.findings -or $null -eq $Analysis.recommendedRepoRepairs -or $null -eq $Analysis.recommendedMachineRepairs) {
        throw 'analysis-required-array-missing'
    }
    if ($null -eq $Analysis.sourceState -or [string]$Analysis.sourceState.head -notmatch '^(?:[0-9a-fA-F]{40,64}|unavailable)$' -or [string]$Analysis.sourceState.gitStatusDigest -notmatch '^(?:[0-9a-f]{64}|unavailable)$') {
        throw 'analysis-source-state-invalid'
    }
    $ids = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::Ordinal)
    foreach ($repair in @($Analysis.recommendedRepoRepairs)) {
        $id = [string]$repair.id
        if (-not (Test-RepairId $id) -or -not $ids.Add($id)) { throw "analysis-repair-id-invalid:$id" }
        if ([string]::IsNullOrWhiteSpace([string]$repair.type) -or [string]::IsNullOrWhiteSpace([string]$repair.summary)) { throw "analysis-repair-description-invalid:$id" }
        if (@($repair.allowedPaths).Count -eq 0) { throw "analysis-repair-paths-missing:$id" }
        foreach ($path in @($repair.allowedPaths) + @($repair.prohibitedPaths)) { [void](ConvertTo-NormalizedPolicyPath ([string]$path)) }
        foreach ($prerequisite in @($repair.prerequisites)) {
            if ([string]$prerequisite.id -notmatch '^TE-P[0-9]{3}$' -or [string]$prerequisite.status -notin @('resolved', 'unresolved') -or [string]::IsNullOrWhiteSpace([string]$prerequisite.evidence)) {
                throw "analysis-prerequisite-invalid:$id"
            }
        }
        if (@($repair.validationPlan).Count -eq 0) { throw "analysis-validation-plan-missing:$id" }
        foreach ($step in @($repair.validationPlan)) {
            if ([string]$step.id -notmatch '^TE-V[0-9]{3}$' -or [string]$step.scope -notin @('focused', 'broad') -or @($step.command).Count -eq 0 -or [string]::IsNullOrWhiteSpace([string]$step.purpose)) {
                throw "analysis-validation-step-invalid:$id"
            }
            foreach ($part in @($step.command)) {
                if ([string]::IsNullOrWhiteSpace([string]$part) -or ([string]$part).Length -gt 1000) { throw "analysis-validation-command-invalid:$id" }
            }
        }
    }
    foreach ($repair in @($Analysis.recommendedMachineRepairs)) {
        if (-not (Test-RepairId ([string]$repair.id) 'TE-M')) { throw 'analysis-machine-repair-id-invalid' }
    }
    return $true
}

function Get-ToolName {
    param($Hook)
    return ([string](Get-PropertyValue $Hook @('tool_name', 'toolName'))).ToLowerInvariant()
}

function Get-ToolInput {
    param($Hook)
    $value = Get-PropertyValue $Hook @('tool_input', 'toolInput')
    if ($null -eq $value) { return [pscustomobject]@{} }
    return $value
}

function Get-ToolPaths {
    param($ToolInput)
    $paths = New-Object System.Collections.Generic.List[string]
    function Visit($Value, [string]$Name) {
        if ($null -eq $Value) { return }
        if ($Value -is [string]) {
            if ($Name -match '(?i)(file.?path|path|uri)$') { $paths.Add($Value) }
            if ($Name -match '(?i)(patch|input|diff)') {
                foreach ($match in [regex]::Matches($Value, '(?m)^\*\*\* (?:Add|Update|Delete) File:\s*(.+?)\s*$')) { $paths.Add($match.Groups[1].Value) }
            }
            return
        }
        if ($Value -is [Collections.IDictionary]) {
            foreach ($key in $Value.Keys) { Visit $Value[$key] ([string]$key) }
            return
        }
        if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) {
            foreach ($item in $Value) { Visit $item $Name }
            return
        }
        foreach ($property in $Value.PSObject.Properties) { Visit $property.Value $property.Name }
    }
    Visit $ToolInput ''
    return @($paths | Sort-Object -Unique)
}

function Convert-ToolPathToRelative {
    param([string]$Root, [string]$Candidate)
    $value = $Candidate
    if ($value.StartsWith('file:', [StringComparison]::OrdinalIgnoreCase)) {
        $value = ([Uri]$value).LocalPath
    }
    if ([IO.Path]::IsPathRooted($value)) {
        $full = [IO.Path]::GetFullPath($value)
        $boundary = $Root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $full.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'tool-path-outside-root' }
        return [IO.Path]::GetRelativePath($Root, $full).Replace('\', '/')
    }
    return ConvertTo-NormalizedPolicyPath $value
}

function Test-PathAllowed {
    param([string]$Path, [string[]]$Allowed, [string[]]$Prohibited)
    $candidate = (ConvertTo-NormalizedPolicyPath $Path).ToLowerInvariant()
    foreach ($blocked in $Prohibited) {
        $policy = (ConvertTo-NormalizedPolicyPath $blocked).ToLowerInvariant()
        if ($candidate -eq $policy -or ($policy.EndsWith('/') -and $candidate.StartsWith($policy, [StringComparison]::Ordinal))) { return $false }
    }
    foreach ($entry in $Allowed) {
        $policy = (ConvertTo-NormalizedPolicyPath $entry).ToLowerInvariant()
        if ($candidate -eq $policy -or ($policy.EndsWith('/') -and $candidate.StartsWith($policy, [StringComparison]::Ordinal))) { return $true }
    }
    return $false
}

function Get-ToolCommand {
    param($ToolInput)
    $value = Get-PropertyValue $ToolInput @('command', 'commandLine')
    return ([string]$value).Trim()
}
