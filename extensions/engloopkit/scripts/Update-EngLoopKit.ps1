[CmdletBinding()]
param(
    [string]$Root = '.',
    [switch]$CheckOnly,
    [string]$ReleaseApiRoot = 'https://api.github.com/repos/stuartpa/engloopkit/releases'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ApiRoot = $ReleaseApiRoot.TrimEnd('/')
$Headers = @{ 'User-Agent' = 'EngLoopKit-Updater'; 'Accept' = 'application/vnd.github+json' }

function Get-ExactGitRoot([string]$Candidate) {
    $selected = (Resolve-Path -LiteralPath $Candidate).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
    $root = (& git -C $selected rev-parse --show-toplevel 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) { throw 'ELK_UPGRADE_GIT_ROOT_UNAVAILABLE' }
    $resolved = (Resolve-Path -LiteralPath $root).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not [string]::Equals($selected, $resolved, [StringComparison]::OrdinalIgnoreCase)) { throw 'ELK_UPGRADE_REQUIRES_EXACT_GIT_ROOT' }
    return $resolved
}

function Get-Release([string]$Uri) {
    $release = Invoke-RestMethod -Uri $Uri -Headers $Headers -TimeoutSec 60
    if ($release.draft -or $release.prerelease) { throw 'ELK_UPGRADE_RELEASE_NOT_STABLE' }
    if ([string]::IsNullOrWhiteSpace([string]$release.tag_name) -or $release.tag_name -notmatch '^v\d+\.\d+\.\d+$') { throw 'ELK_UPGRADE_RELEASE_TAG_INVALID' }
    return $release
}

function Get-Asset($Release, [string]$Name) {
    $releaseAssets = @($Release.assets | Where-Object name -eq $Name)
    if ($releaseAssets.Count -ne 1) { throw "ELK_UPGRADE_ASSET_MISSING_OR_AMBIGUOUS name=$Name count=$($releaseAssets.Count)" }
    return $releaseAssets[0]
}

function Save-Asset($Asset, [string]$Path) {
    Invoke-WebRequest -Uri $Asset.browser_download_url -Headers $Headers -OutFile $Path -UseBasicParsing -TimeoutSec 300
}

function Assert-Hash([string]$Path, [string]$Expected, [string]$Identity) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "ELK_UPGRADE_FILE_MISSING identity=$Identity" }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Expected.ToLowerInvariant()) { throw "ELK_UPGRADE_HASH_MISMATCH identity=$Identity expected=$Expected actual=$actual" }
}

function Get-VersionState([string]$RepoRoot) {
    $toolManifest = Join-Path $RepoRoot '.config/dotnet-tools.json'
    if (-not (Test-Path -LiteralPath $toolManifest -PathType Leaf)) { throw 'ELK_UPGRADE_TOOL_MANIFEST_MISSING' }
    $manifest = Get-Content -LiteralPath $toolManifest -Raw | ConvertFrom-Json -AsHashtable
    $toolVersion = [string]$manifest.tools.engloopkit.version
    $sekToolVersion = if ($manifest.tools.ContainsKey('specexplorerkit.tool')) { [string]$manifest.tools['specexplorerkit.tool'].version } else { '' }
    $extensionManifest = Join-Path $RepoRoot '.specify/extensions/engloop/extension.yml'
    if (-not (Test-Path -LiteralPath $extensionManifest -PathType Leaf)) { throw 'ELK_UPGRADE_EXTENSION_NOT_INSTALLED' }
    $extensionText = Get-Content -LiteralPath $extensionManifest -Raw
    $match = [regex]::Match($extensionText, '(?m)^\s*version:\s*"(?<version>\d+\.\d+\.\d+)"\s*$')
    if (-not $match.Success) { throw 'ELK_UPGRADE_EXTENSION_VERSION_INVALID' }
    $sekManifest = Join-Path $RepoRoot '.specify/extensions/sek/extension.yml'
    $sekExtensionVersion = ''
    if (Test-Path -LiteralPath $sekManifest -PathType Leaf) {
        $sekMatch = [regex]::Match((Get-Content -LiteralPath $sekManifest -Raw), '(?m)^\s*version:\s*"(?<version>\d+\.\d+\.\d+)"\s*$')
        if ($sekMatch.Success) { $sekExtensionVersion = $sekMatch.Groups['version'].Value }
    }
    return [ordered]@{
        tool = $toolVersion
        extension = $match.Groups['version'].Value
        sekTool = $sekToolVersion
        sekExtension = $sekExtensionVersion
    }
}

function Read-ReleaseManifest($Release, [string]$Version, [string]$Directory) {
    $name = "engloopkit-release-manifest-$Version.json"
    $path = Join-Path $Directory $name
    Save-Asset (Get-Asset $Release $name) $path
    $manifest = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    if ($manifest.schemaVersion -ne '1.0' -or $manifest.version -ne $Version) { throw 'ELK_UPGRADE_RELEASE_MANIFEST_INVALID' }
    return $manifest
}

function Assert-DotNetPlatform($Manifest) {
    $minimum = [version][string]$Manifest.platform.dotnetSdk.minimum
    $selectedText = (& dotnet --version 2>$null | Out-String).Trim()
    $selected = $null
    if (-not [version]::TryParse($selectedText, [ref]$selected) -or $selected -lt $minimum) {
        throw "ELK_UPGRADE_DOTNET_TOO_OLD selected=$selectedText minimum=$minimum"
    }
    if ([string]$Manifest.platform.targetFramework -ne 'net10.0' -or [string]$Manifest.platform.solution -ne 'EngLoopKit.slnx') {
        throw 'ELK_UPGRADE_PLATFORM_MANIFEST_INVALID'
    }
}

function Save-VerifiedElkArtifacts($Release, $Manifest, [string]$Directory) {
    $result = @{}
    foreach ($key in @('tool','extension')) {
        $entry = $Manifest.artifacts[$key]
        $name = [string]$entry.name
        $path = Join-Path $Directory $name
        Save-Asset (Get-Asset $Release $name) $path
        Assert-Hash $path ([string]$entry.sha256) "elk-$key"
        $result[$key] = $path
    }
    return $result
}

function Save-VerifiedSekArtifacts($Manifest, [string]$Directory) {
    $sek = $Manifest.dependencies.sek
    if ($sek.version -ne '0.1.3') { throw "ELK_UPGRADE_SEK_VERSION_UNSUPPORTED version=$($sek.version)" }
    $result = @{}
    foreach ($key in @('tool','modeling','extension')) {
        $entry = $sek.artifacts[$key]
        $path = Join-Path $Directory ([string]$entry.name)
        Invoke-WebRequest -Uri ([string]$entry.url) -OutFile $path -UseBasicParsing -TimeoutSec 300
        Assert-Hash $path ([string]$entry.sha256) "sek-$key"
        $result[$key] = $path
    }
    return $result
}

function Copy-ManagedSnapshot([string]$RepoRoot, [string]$Snapshot) {
    New-Item -ItemType Directory -Path $Snapshot -Force | Out-Null
    foreach ($relative in @('.config/dotnet-tools.json','.specify/extensions/engloop','.specify/extensions/sek','.specify/extensions/.registry','.specify/extensions.yml','.github/agents','.github/prompts')) {
        $source = Join-Path $RepoRoot $relative
        if (-not (Test-Path -LiteralPath $source)) { continue }
        $destination = Join-Path $Snapshot $relative
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        if (Test-Path -LiteralPath $source -PathType Container) { Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force }
        else { Copy-Item -LiteralPath $source -Destination $destination -Force }
    }
}

function Restore-ManagedSnapshot([string]$RepoRoot, [string]$Snapshot) {
    foreach ($relative in @('.config/dotnet-tools.json','.specify/extensions/engloop','.specify/extensions/sek','.specify/extensions/.registry','.specify/extensions.yml','.github/agents','.github/prompts')) {
        $target = Join-Path $RepoRoot $relative
        $source = Join-Path $Snapshot $relative
        Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
        if (-not (Test-Path -LiteralPath $source)) { continue }
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        if (Test-Path -LiteralPath $source -PathType Container) { Copy-Item -LiteralPath $source -Destination $target -Recurse -Force }
        else { Copy-Item -LiteralPath $source -Destination $target -Force }
    }
}

function Get-SnapshotDigest([string]$RootPath) {
    $entries = @()
    foreach ($file in Get-ChildItem -LiteralPath $RootPath -Recurse -File | Sort-Object FullName) {
        $relative = [IO.Path]::GetRelativePath($RootPath, $file.FullName).Replace('\','/')
        $entries += "${relative}:$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())"
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($entries -join "`n"))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Assert-RestoredSnapshot([string]$RepoRoot, [string]$Snapshot, $ExpectedState) {
    $currentState = Get-VersionState $RepoRoot
    foreach ($key in @('tool','extension','sekTool','sekExtension')) {
        if ([string]$currentState[$key] -ne [string]$ExpectedState[$key]) { throw "ELK_UPGRADE_ROLLBACK_VERSION_MISMATCH key=$key" }
    }
    $actualSnapshot = Join-Path (Split-Path $Snapshot -Parent) ('verify-' + [Guid]::NewGuid().ToString('N'))
    Copy-ManagedSnapshot $RepoRoot $actualSnapshot
    try {
        if ((Get-SnapshotDigest $actualSnapshot) -ne (Get-SnapshotDigest $Snapshot)) { throw 'ELK_UPGRADE_ROLLBACK_SNAPSHOT_MISMATCH' }
    }
    finally { Remove-Item -LiteralPath $actualSnapshot -Recurse -Force -ErrorAction SilentlyContinue }
}

function Save-CachedToolPackage([string]$PackageId, [string]$Version, [string]$Directory) {
    if ([string]::IsNullOrWhiteSpace($Version)) { return $null }
    $packagesRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $packageRoot = Join-Path $packagesRoot (Join-Path $PackageId.ToLowerInvariant() $Version.ToLowerInvariant())
    $preferred = @(
        (Join-Path $packageRoot ($PackageId.ToLowerInvariant() + '.' + $Version.ToLowerInvariant() + '.nupkg')),
        (Join-Path $packageRoot ($PackageId.ToLowerInvariant() + '.nupkg'))
    )
    $source = $preferred | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($source)) { throw "ELK_UPGRADE_ROLLBACK_PACKAGE_MISSING id=$PackageId version=$Version" }
    $target = Join-Path $Directory ([IO.Path]::GetFileName($source))
    Copy-Item -LiteralPath $source -Destination $target -Force
    $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    $targetHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($sourceHash -ne $targetHash) { throw "ELK_UPGRADE_ROLLBACK_PACKAGE_COPY_MISMATCH id=$PackageId version=$Version" }
    return $target
}

function Install-Extension([string]$Archive, [string]$Id, [string]$WorkRoot) {
    $directory = Join-Path $WorkRoot ("extension-" + $Id)
    Remove-Item -LiteralPath $directory -Recurse -Force -ErrorAction SilentlyContinue
    Expand-Archive -LiteralPath $Archive -DestinationPath $directory
    specify extension add $directory --dev --force
    if ($LASTEXITCODE -ne 0) { throw "ELK_UPGRADE_EXTENSION_INSTALL_FAILED id=$Id" }
}

function Assert-InstalledSurface([string]$RepoRoot, $Manifest) {
    $state = Get-VersionState $RepoRoot
    if ($state.tool -ne $Manifest.version -or $state.extension -ne $Manifest.version) { throw 'ELK_UPGRADE_ELK_VERSION_DIVERGENCE' }
    if ($state.sekTool -ne $Manifest.dependencies.sek.version -or $state.sekExtension -ne $Manifest.dependencies.sek.version) { throw 'ELK_UPGRADE_SEK_VERSION_DIVERGENCE' }
    foreach ($relative in @(
        '.specify/extensions/sek/skills/sek-cord-authoring/SKILL.md',
        '.specify/extensions/sek/skills/using-sek-to-generate-tests/SKILL.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot $relative) -PathType Leaf)) { throw "ELK_UPGRADE_SEK_SKILL_MISSING path=$relative" }
    }
    $ids = @($Manifest.commands.ids)
    if ($ids.Count -ne [int]$Manifest.commands.count) { throw 'ELK_UPGRADE_MANIFEST_COMMAND_COUNT_INVALID' }
    foreach ($id in $ids) {
        foreach ($relative in @(".specify/extensions/engloop/commands/$id.md", ".github/agents/$id.agent.md", ".github/prompts/$id.prompt.md")) {
            if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot $relative) -PathType Leaf)) { throw "ELK_UPGRADE_SURFACE_MISSING path=$relative" }
        }
        $prompt = Get-Content -LiteralPath (Join-Path $RepoRoot ".github/prompts/$id.prompt.md") -Raw
        if ($prompt -notmatch ('(?m)^agent:\s*' + [regex]::Escape($id) + '\s*$')) { throw "ELK_UPGRADE_PROMPT_AGENT_MISMATCH id=$id" }
    }
    $agentCount = @(Get-ChildItem (Join-Path $RepoRoot '.github/agents') -Filter 'speckit.engloop.*.agent.md' -File).Count
    $promptCount = @(Get-ChildItem (Join-Path $RepoRoot '.github/prompts') -Filter 'speckit.engloop.*.prompt.md' -File).Count
    if ($agentCount -ne $ids.Count -or $promptCount -ne $ids.Count) { throw "ELK_UPGRADE_SURFACE_COUNT_MISMATCH agents=$agentCount prompts=$promptCount expected=$($ids.Count)" }
}

$repoRoot = Get-ExactGitRoot $Root
$outRoot = Join-Path $repoRoot '.engloop/out/upgrade-elk'
New-Item -ItemType Directory -Path $outRoot -Force | Out-Null
& git -C $repoRoot check-ignore -q --no-index -- '.engloop/out/upgrade-elk/.probe'
if ($LASTEXITCODE -ne 0) { throw 'ELK_UPGRADE_OUTPUT_ROOT_NOT_IGNORED' }

$latestRelease = Get-Release "$ApiRoot/latest"
$latestVersion = ([string]$latestRelease.tag_name).TrimStart('v')
$current = Get-VersionState $repoRoot
if ($current.tool -ne $current.extension) { throw "ELK_UPGRADE_CURRENT_ELK_VERSION_DIVERGENCE tool=$($current.tool) extension=$($current.extension)" }
if ([version]$current.tool -gt [version]$latestVersion) { throw "ELK_UPGRADE_LOCAL_VERSION_AHEAD current=$($current.tool) latest=$latestVersion" }

$latestWork = Join-Path $outRoot ("latest-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $latestWork -Force | Out-Null
$latestManifest = Read-ReleaseManifest $latestRelease $latestVersion $latestWork
Assert-DotNetPlatform $latestManifest
if ($current.tool -eq $latestVersion) {
    try {
        Assert-InstalledSurface $repoRoot $latestManifest
        Write-Output "ELK_UPGRADE_CURRENT version=$latestVersion sek=$($latestManifest.dependencies.sek.version)"
        exit 0
    }
    catch {
        if ($CheckOnly) { throw "ELK_UPGRADE_CURRENT_INSTALLATION_INVALID $($_.Exception.Message)" }
        Write-Verbose "Repairing drifted current-version installation: $($_.Exception.Message)"
    }
}
if ($CheckOnly) {
    Write-Output "ELK_UPGRADE_AVAILABLE current=$($current.tool) latest=$latestVersion sek=$($latestManifest.dependencies.sek.version)"
    exit 0
}

$currentWork = Join-Path $outRoot ("rollback-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $currentWork -Force | Out-Null
$null = Save-CachedToolPackage 'engloopkit' $current.tool $currentWork
$null = Save-CachedToolPackage 'specexplorerkit.tool' $current.sekTool $currentWork
$latestArtifacts = Save-VerifiedElkArtifacts $latestRelease $latestManifest $latestWork
$sekArtifacts = Save-VerifiedSekArtifacts $latestManifest $latestWork
$snapshot = Join-Path $outRoot ("snapshot-" + [Guid]::NewGuid().ToString('N'))
Copy-ManagedSnapshot $repoRoot $snapshot

try {
    dotnet tool update engloopkit --version $latestVersion --tool-manifest (Join-Path $repoRoot '.config/dotnet-tools.json') --add-source $latestWork --no-cache
    if ($LASTEXITCODE -ne 0) { throw 'ELK_UPGRADE_TOOL_INSTALL_FAILED' }
    dotnet tool update SpecExplorerKit.Tool --version $latestManifest.dependencies.sek.version --tool-manifest (Join-Path $repoRoot '.config/dotnet-tools.json') --add-source $latestWork --no-cache
    if ($LASTEXITCODE -ne 0) { throw 'ELK_UPGRADE_SEK_TOOL_INSTALL_FAILED' }
    $sekAssetRoot = Join-Path $repoRoot ".engloop/out/sek-v$($latestManifest.dependencies.sek.version)-assets"
    New-Item -ItemType Directory -Path $sekAssetRoot -Force | Out-Null
    Copy-Item -LiteralPath $sekArtifacts.tool,$sekArtifacts.modeling,$sekArtifacts.extension -Destination $sekAssetRoot -Force
    Install-Extension $sekArtifacts.extension 'sek' $latestWork
    Install-Extension $latestArtifacts.extension 'engloop' $latestWork
    Assert-InstalledSurface $repoRoot $latestManifest
    $result = [ordered]@{ capturedAtUtc=(Get-Date).ToUniversalTime().ToString('o'); verdict='PASS'; previousVersion=$current.tool; version=$latestVersion; sekVersion=$latestManifest.dependencies.sek.version }
    $result | ConvertTo-Json | Set-Content (Join-Path $outRoot 'result.json') -Encoding utf8NoBOM
    Write-Output "ELK_UPGRADE_PASS previous=$($current.tool) version=$latestVersion sek=$($latestManifest.dependencies.sek.version)"
}
catch {
    $upgradeError = $_.Exception.Message
    Restore-ManagedSnapshot $repoRoot $snapshot
    dotnet tool restore --tool-manifest (Join-Path $repoRoot '.config/dotnet-tools.json') --add-source $currentWork --no-cache | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "ELK_UPGRADE_ROLLBACK_TOOL_RESTORE_FAILED original=$upgradeError" }
    Assert-RestoredSnapshot $repoRoot $snapshot $current
    throw "ELK_UPGRADE_FAILED_ROLLED_BACK error=$upgradeError restored=$($current.tool)"
}
