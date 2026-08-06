[CmdletBinding()]
param(
    [string]$RepositoryRoot = '.',
    [string]$ExcludePath = ''
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TokenEfficiencyPolicy.ps1')

try {
    $hook = [pscustomobject]@{ cwd = $RepositoryRoot }
    $root = Get-RepositoryRoot $hook $RepositoryRoot
    $excluded = if ([string]::IsNullOrWhiteSpace($ExcludePath)) { @() } else { @((Normalize-PolicyPath $ExcludePath)) }
    [ordered]@{
        schemaVersion = '1.0'
        head = Get-GitHead $root
        gitStatusDigest = Get-GitStatusDigest $root $excluded
        excludedPaths = $excluded
    } | ConvertTo-Json -Compress
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 2
}
