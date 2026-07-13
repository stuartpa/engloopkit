[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..'),
    [string]$Version = '1.7.0',
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

$repoRoot = (Resolve-Path $Root).Path
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $repoRoot '.engloop/out/release'
}
if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

if ([string]::IsNullOrWhiteSpace($EmitReleaseManifest)) {
    $EmitReleaseManifest = Join-Path $repoRoot '.engloop/out/release-manifest.json'
}
$manifestDir = Split-Path $EmitReleaseManifest -Parent
if (-not (Test-Path $manifestDir)) {
    New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
}

$catalogPath = Join-Path $repoRoot 'catalog.json'
$bundlePath = Join-Path $repoRoot 'bundle.yml'
$extensionManifestPath = Join-Path $repoRoot 'extensions/engloopkit/extension.yml'

$bundleText = Get-Content $bundlePath -Raw -Encoding UTF8
$extensionText = Get-Content $extensionManifestPath -Raw -Encoding UTF8
$catalog = Get-Content $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable

if ($bundleText -notmatch 'version:\s*"1\.7\.0"') { throw 'bundle.yml is not pinned to 1.7.0' }
if ($extensionText -notmatch 'version:\s*"1\.7\.0"') { throw 'extension.yml is not pinned to 1.7.0' }
if ($catalog.extensions[0].version -ne '1.7.0') { throw 'catalog.json is not pinned to 1.7.0' }
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

# Tool artifact
Invoke-Checked 'dotnet' @('pack', 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj', '-c', 'Release', '-o', $nupkgDir, '--nologo') $repoRoot
$toolNupkg = Get-ChildItem $nupkgDir -Filter 'engloopkit.1.7.0*.nupkg' | Select-Object -First 1
if ($null -eq $toolNupkg) { throw 'Failed to produce engloopkit tool nupkg.' }

$localToolManifestRoot = Join-Path $toolPackDir 'tool-manifest'
New-Item -ItemType Directory -Path $localToolManifestRoot -Force | Out-Null
Invoke-Checked 'dotnet' @('new', 'tool-manifest', '--force') $localToolManifestRoot
$localToolManifestPath = Join-Path $localToolManifestRoot '.config/dotnet-tools.json'
Invoke-Checked 'dotnet' @('tool', 'install', 'engloopkit', '--version', '1.7.0', '--add-source', $nupkgDir, '--tool-manifest', $localToolManifestPath) $localToolManifestRoot
Invoke-Checked 'dotnet' @('tool', 'run', 'engloopkit', '--', 'validate', 'installation', '--root', $repoRoot) $localToolManifestRoot

# Extension artifact (zip exact extension folder)
if (Test-Path $extensionZipPath) { Remove-Item $extensionZipPath -Force }
Compress-Archive -Path (Join-Path $repoRoot 'extensions/engloopkit/*') -DestinationPath $extensionZipPath -CompressionLevel Optimal

# Bundle artifact using a minimal staged source. The first-party extension is
# unpublished at build time, so official offline validation records it as an unchecked
# reference; its exact archive/install semantics are independently gated below.
Copy-Item $bundlePath (Join-Path $bundleStageDir 'bundle.yml') -Force
Copy-Item (Join-Path $repoRoot 'README.md') (Join-Path $bundleStageDir 'README.md') -Force
Invoke-Checked 'specify' @('bundle', 'validate', '--offline', '--path', $bundleStageDir) $repoRoot
Invoke-Checked 'specify' @('bundle', 'build', '--path', $bundleStageDir, '--output', $bundleBuildDir) $repoRoot
$builtBundle = Get-ChildItem $bundleBuildDir -Filter '*.zip' | Where-Object { $_.Name -match '^engloopkit-1\.7\.0\.zip$' } | Select-Object -First 1
if ($null -eq $builtBundle) { throw 'Spec Kit bundle build did not produce engloopkit-1.7.0.zip' }
Copy-Item $builtBundle.FullName $bundleZipPath -Force

# Agent surfaces: deterministic source/archive/disposable-install semantic gate.
# UI validation is intentionally outside the product contract.
$agentSurfaceEvidence = Join-Path $OutDir 'agent-surface-evidence.json'
$agentSurfaceArguments = @(
    '-NoProfile',
    '-File', (Join-Path $repoRoot 'scripts/validate-agent-surfaces.ps1'),
    '-Root', $repoRoot,
    '-Version', $Version,
    '-OutputPath', $agentSurfaceEvidence
)
Invoke-Checked 'pwsh' $agentSurfaceArguments $repoRoot

$agentEvidence = Get-Content $agentSurfaceEvidence -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
if ($agentEvidence.verdict -ne 'PASS') { throw 'Agent surface evidence is not PASS.' }

# Hashes
$toolHash = (Get-FileHash $toolNupkg.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$extHash = (Get-FileHash $extensionZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$bundleHash = (Get-FileHash $bundleZipPath -Algorithm SHA256).Hash.ToLowerInvariant()

# Validate catalog checksum remains blank prior to final write
$catalogBefore = Get-Content $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
if ($catalogBefore.extensions[0].sha256 -ne '') {
    throw 'catalog.json checksum was expected to be blank before writing final artifact hash.'
}

$catalogBefore.extensions[0].sha256 = $extHash
$catalogBefore | ConvertTo-Json -Depth 32 | Set-Content $catalogPath -Encoding utf8NoBOM

$artifactSummary = [ordered]@{
    capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    version = $Version
    tool = [ordered]@{
        path = $toolNupkg.FullName
        sha256 = $toolHash
        source = $nupkgDir
    }
    extension = [ordered]@{
        path = $extensionZipPath
        sha256 = $extHash
    }
    bundle = [ordered]@{
        path = $bundleZipPath
        sha256 = $bundleHash
    }
    gates = [ordered]@{
        bundleValidate = 'PASS'
        bundleBuild = 'PASS'
        toolInstallValidate = 'PASS'
        agentSurfaceEvidence = $agentSurfaceEvidence
    }
    policy = 'No EngLoop-owned alternate generator or post-processing fallback. Agent-surface validation is deterministic and non-UI.'
    catalogChecksumSet = $true
}

$artifactSummary | ConvertTo-Json -Depth 32 | Set-Content $EmitReleaseManifest -Encoding utf8NoBOM
Write-Output "VALIDATE_PACKAGE_PASS manifest=$EmitReleaseManifest"
