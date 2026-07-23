[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..'),
    [string]$Version = '1.11.0',
    [string]$EmitReleaseManifest = '',
    [string]$OutDir = ''
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

function Clear-ExactToolCache {
    param([string]$PackageId, [string]$PackageVersion)
    $globalPackages = if ($env:NUGET_PACKAGES) {
        $env:NUGET_PACKAGES
    }
    else {
        Join-Path $env:USERPROFILE '.nuget\packages'
    }
    $cachePath = Join-Path $globalPackages (Join-Path $PackageId.ToLowerInvariant() $PackageVersion.ToLowerInvariant())
    if (Test-Path $cachePath) {
        Remove-Item $cachePath -Recurse -Force
    }
}

$repoRoot = (Resolve-Path $Root).Path
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $repoRoot '.engloop/out/release'
}
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($EmitReleaseManifest)) {
    $EmitReleaseManifest = Join-Path $repoRoot '.engloop/out/release-manifest.json'
}
New-Item -ItemType Directory -Path (Split-Path $EmitReleaseManifest -Parent) -Force | Out-Null

$catalogPath = Join-Path $repoRoot 'catalog.json'
$bundlePath = Join-Path $repoRoot 'bundle.yml'
$extensionManifestPath = Join-Path $repoRoot 'extensions/engloopkit/extension.yml'
$bundleText = Get-Content $bundlePath -Raw -Encoding UTF8
$extensionText = Get-Content $extensionManifestPath -Raw -Encoding UTF8
$catalog = Get-Content $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable

$escapedVersion = [regex]::Escape($Version)
$versionPattern = 'version:\s*"' + $escapedVersion + '"'
if ($bundleText -notmatch $versionPattern) { throw "bundle.yml is not pinned to $Version" }
if ($extensionText -notmatch $versionPattern) { throw "extension.yml is not pinned to $Version" }
if ($catalog.extensions[0].version -ne $Version) { throw "catalog.json is not pinned to $Version" }
if ($catalog.extensions[0].sha256 -ne '') { throw 'catalog checksum must remain blank before final artifact creation.' }

$toolPackDir = Join-Path $OutDir 'tool'
$nupkgDir = Join-Path $OutDir 'nupkg'
$bundleBuildDir = Join-Path $OutDir 'bundle-build'
$bundleStageDir = Join-Path $OutDir 'bundle-stage'
$extensionZipPath = Join-Path $OutDir ('engloopkit-extension-' + $Version + '.zip')
$bundleZipPath = Join-Path $OutDir ('engloopkit-' + $Version + '.zip')

foreach ($dir in @($toolPackDir, $nupkgDir, $bundleBuildDir, $bundleStageDir)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

# Tool artifact and isolated local-tool smoke validation.
Invoke-Checked 'dotnet' @('pack', 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj', '-c', 'Release', '-o', $nupkgDir, '--nologo') $repoRoot
$toolNupkg = Get-ChildItem $nupkgDir -Filter ("engloopkit.$Version*.nupkg") | Select-Object -First 1
if ($null -eq $toolNupkg) { throw "Failed to produce engloopkit.$Version tool nupkg." }

$localToolManifestRoot = Join-Path $toolPackDir 'tool-manifest'
New-Item -ItemType Directory -Path $localToolManifestRoot -Force | Out-Null
Invoke-Checked 'dotnet' @('new', 'tool-manifest', '--force') $localToolManifestRoot
$localToolManifestPath = Join-Path $localToolManifestRoot '.config/dotnet-tools.json'
Clear-ExactToolCache -PackageId 'engloopkit' -PackageVersion $Version
Invoke-Checked 'dotnet' @('tool', 'install', 'engloopkit', '--version', $Version, '--add-source', $nupkgDir, '--tool-manifest', $localToolManifestPath, '--no-cache') $localToolManifestRoot
Invoke-Checked 'dotnet' @('tool', 'run', 'engloopkit', '--', 'validate', 'installation', '--root', $repoRoot) $localToolManifestRoot

# Extension artifact: exact extension folder payload.
if (Test-Path $extensionZipPath) { Remove-Item $extensionZipPath -Force }
Compress-Archive -Path (Join-Path $repoRoot 'extensions/engloopkit/*') -DestinationPath $extensionZipPath -CompressionLevel Optimal

# Bundle artifact: first-party extension is independently validated above.
Copy-Item $bundlePath (Join-Path $bundleStageDir 'bundle.yml') -Force
Copy-Item (Join-Path $repoRoot 'README.md') (Join-Path $bundleStageDir 'README.md') -Force
Invoke-Checked 'specify' @('bundle', 'validate', '--offline', '--path', $bundleStageDir) $repoRoot
Invoke-Checked 'specify' @('bundle', 'build', '--path', $bundleStageDir, '--output', $bundleBuildDir) $repoRoot
$builtBundle = Get-ChildItem $bundleBuildDir -Filter '*.zip' | Where-Object { $_.Name -eq ("engloopkit-$Version.zip") } | Select-Object -First 1
if ($null -eq $builtBundle) { throw "Spec Kit bundle build did not produce engloopkit-$Version.zip" }
Copy-Item $builtBundle.FullName $bundleZipPath -Force

# Agent surfaces: deterministic source/archive/disposable-install semantic gate.
# UI validation is intentionally outside the product contract.
$agentSurfaceEvidence = Join-Path $OutDir 'agent-surface-evidence.json'
Invoke-Checked 'pwsh' @(
    '-NoProfile', '-File', (Join-Path $repoRoot 'scripts/validate-agent-surfaces.ps1'),
    '-Root', $repoRoot, '-Version', $Version, '-OutputPath', $agentSurfaceEvidence
) $repoRoot
$agentEvidence = Get-Content $agentSurfaceEvidence -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
if ($agentEvidence.verdict -ne 'PASS') { throw 'Agent surface evidence is not PASS.' }

# Private overlay transactions: clean host, existing agent host, and chained hook coexistence.
Invoke-Checked 'pwsh' @(
    '-NoProfile', '-File', (Join-Path $repoRoot 'scripts/test-overlay.ps1'),
    '-Root', $repoRoot, '-ToolNupkg', $toolNupkg.FullName,
    '-ExtensionArchive', $extensionZipPath, '-Version', $Version
) $repoRoot

$toolHash = (Get-FileHash $toolNupkg.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$extensionHash = (Get-FileHash $extensionZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$bundleHash = (Get-FileHash $bundleZipPath -Algorithm SHA256).Hash.ToLowerInvariant()

$catalogBefore = Get-Content $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
if ($catalogBefore.extensions[0].sha256 -ne '') {
    throw 'catalog.json checksum was expected to be blank before writing final artifact hash.'
}
$catalogBefore.extensions[0].sha256 = $extensionHash
$catalogBefore | ConvertTo-Json -Depth 32 | Set-Content $catalogPath -Encoding utf8NoBOM

$summary = [ordered]@{
    capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    version = $Version
    tool = [ordered]@{ path = $toolNupkg.FullName; sha256 = $toolHash; source = $nupkgDir }
    extension = [ordered]@{ path = $extensionZipPath; sha256 = $extensionHash }
    bundle = [ordered]@{ path = $bundleZipPath; sha256 = $bundleHash }
    gates = [ordered]@{
        bundleValidate = 'PASS'
        bundleBuild = 'PASS'
        toolInstallValidate = 'PASS'
        agentSurfaceEvidence = $agentSurfaceEvidence
        overlayCleanAndCoexistTransactions = 'PASS'
    }
    policy = 'Deterministic source/archive/disposable-install validation only. No UI validation or editor automation.'
    catalogChecksumSet = $true
}
$summary | ConvertTo-Json -Depth 32 | Set-Content $EmitReleaseManifest -Encoding utf8NoBOM
Write-Output "VALIDATE_PACKAGE_PASS manifest=$EmitReleaseManifest"
