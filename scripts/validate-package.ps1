[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..'),
    [string]$Version = '1.14.0',
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
$releaseManifestAssetPath = Join-Path $OutDir ('engloopkit-release-manifest-' + $Version + '.json')

foreach ($dir in @($toolPackDir, $nupkgDir, $bundleBuildDir, $bundleStageDir)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

# Tool artifact and isolated local-tool smoke validation.
Invoke-Checked 'dotnet' @('pack', 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj', '-c', 'Release', '-o', $nupkgDir, '--nologo') $repoRoot
$toolNupkg = Get-ChildItem $nupkgDir -Filter ("engloopkit.$Version*.nupkg") | Select-Object -First 1
if ($null -eq $toolNupkg) { throw "Failed to produce engloopkit.$Version tool nupkg." }

$isolatedToolPath = Join-Path $toolPackDir 'isolated-tool'
New-Item -ItemType Directory -Path $isolatedToolPath -Force | Out-Null
Clear-ExactToolCache -PackageId 'engloopkit' -PackageVersion $Version
Invoke-Checked 'dotnet' @('tool', 'install', 'engloopkit', '--version', $Version, '--add-source', $nupkgDir, '--tool-path', $isolatedToolPath, '--no-cache') $repoRoot
$toolExecutableName = if ($IsWindows) { 'engloopkit.exe' } else { 'engloopkit' }
$toolExecutable = Join-Path $isolatedToolPath $toolExecutableName
if (-not (Test-Path -LiteralPath $toolExecutable -PathType Leaf)) { throw "Isolated tool executable was not created: $toolExecutable" }
Invoke-Checked $toolExecutable @('validate', 'installation', '--root', $repoRoot) $repoRoot

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
$commandIds = @([regex]::Matches($extensionText, '(?m)^\s*-\s+name:\s+"(?<id>speckit\.engloop\.[^"]+)"\s*$') | ForEach-Object { $_.Groups['id'].Value })

$catalogBefore = Get-Content $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
if ($catalogBefore.extensions[0].sha256 -ne '') {
    throw 'catalog.json checksum was expected to be blank before writing final artifact hash.'
}
$catalogBefore.extensions[0].sha256 = $extensionHash
$catalogBefore | ConvertTo-Json -Depth 32 | Set-Content $catalogPath -Encoding utf8NoBOM

$summary = [ordered]@{
    schemaVersion = '1.0'
    capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    version = $Version
    platform = [ordered]@{
        dotnetSdk = [ordered]@{ minimum = '10.0.0'; pinned = '10.0.303' }
        targetFramework = 'net10.0'
        solution = 'EngLoopKit.slnx'
    }
    artifacts = [ordered]@{
        tool = [ordered]@{ name = $toolNupkg.Name; sha256 = $toolHash }
        extension = [ordered]@{ name = [IO.Path]::GetFileName($extensionZipPath); sha256 = $extensionHash }
        bundle = [ordered]@{ name = [IO.Path]::GetFileName($bundleZipPath); sha256 = $bundleHash }
    }
    commands = [ordered]@{ count = $commandIds.Count; ids = $commandIds }
    dependencies = [ordered]@{
        sek = [ordered]@{
            version = '0.1.3'
            tag = 'v0.1.3'
            artifacts = [ordered]@{
                tool = [ordered]@{ name = 'SpecExplorerKit.Tool.0.1.3.nupkg'; url = 'https://github.com/stuartpa/sek/releases/download/v0.1.3/SpecExplorerKit.Tool.0.1.3.nupkg'; sha256 = '5bda43161665fba562375023afe2c205fa2dfa3f765cf290336d32953f49ee36' }
                modeling = [ordered]@{ name = 'SpecExplorerKit.Modeling.0.1.3.nupkg'; url = 'https://github.com/stuartpa/sek/releases/download/v0.1.3/SpecExplorerKit.Modeling.0.1.3.nupkg'; sha256 = '95afc47fe0cf0216fa6d1288a7bc9f013c50f9e61d6ca0eb4c683724fa95566a' }
                extension = [ordered]@{ name = 'spec-kit-sek.zip'; url = 'https://github.com/stuartpa/sek/releases/download/v0.1.3/spec-kit-sek.zip'; sha256 = '4fb18feeb679fb20f018321e9a9b50ba9814e6d63256c29dbe7d6bde619f6b63' }
            }
        }
    }
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
$summary | ConvertTo-Json -Depth 32 | Set-Content $releaseManifestAssetPath -Encoding utf8NoBOM
Write-Output "VALIDATE_PACKAGE_PASS manifest=$EmitReleaseManifest"
