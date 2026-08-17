[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..'),
    [switch]$InstallExtension
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath $Root).Path
$assetRoot = Join-Path $repoRoot '.engloop/out/sek-v0.1.3-assets'
$toolManifest = Join-Path $repoRoot '.config/dotnet-tools.json'
$assets = [ordered]@{
    'SpecExplorerKit.Tool.0.1.3.nupkg' = [ordered]@{
        url = 'https://github.com/stuartpa/sek/releases/download/v0.1.3/SpecExplorerKit.Tool.0.1.3.nupkg'
        sha256 = '5bda43161665fba562375023afe2c205fa2dfa3f765cf290336d32953f49ee36'
    }
    'SpecExplorerKit.Modeling.0.1.3.nupkg' = [ordered]@{
        url = 'https://github.com/stuartpa/sek/releases/download/v0.1.3/SpecExplorerKit.Modeling.0.1.3.nupkg'
        sha256 = '95afc47fe0cf0216fa6d1288a7bc9f013c50f9e61d6ca0eb4c683724fa95566a'
    }
    'spec-kit-sek.zip' = [ordered]@{
        url = 'https://github.com/stuartpa/sek/releases/download/v0.1.3/spec-kit-sek.zip'
        sha256 = '4fb18feeb679fb20f018321e9a9b50ba9814e6d63256c29dbe7d6bde619f6b63'
    }
}

New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null
foreach ($name in $assets.Keys) {
    $target = Join-Path $assetRoot $name
    $expected = $assets[$name].sha256
    $valid = (Test-Path -LiteralPath $target -PathType Leaf) -and
        ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $expected)
    if (-not $valid) {
        Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
        Invoke-WebRequest -Uri $assets[$name].url -OutFile $target -UseBasicParsing
    }
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) { throw "SEK_ASSET_HASH_MISMATCH asset=$name expected=$expected actual=$actual" }
}

if (-not (Test-Path -LiteralPath $toolManifest -PathType Leaf)) { throw "Missing root-local tool manifest: $toolManifest" }
$manifest = Get-Content -LiteralPath $toolManifest -Raw | ConvertFrom-Json -AsHashtable
if (-not $manifest.tools.ContainsKey('specexplorerkit.tool') -or
    $manifest.tools['specexplorerkit.tool'].version -ne '0.1.3') {
    throw 'Root-local tool manifest is not pinned to SpecExplorerKit.Tool 0.1.3.'
}

dotnet tool update SpecExplorerKit.Tool --version 0.1.3 --tool-manifest $toolManifest --add-source $assetRoot --no-cache
if ($LASTEXITCODE -ne 0) { throw 'Failed to restore root-local SEK v0.1.3 tool.' }
$previousLocation = Get-Location
Set-Location $repoRoot
try { $version = (& dotnet tool run sek -- version | Out-String).Trim() }
finally { Set-Location $previousLocation }
if ($LASTEXITCODE -ne 0 -or $version -notmatch '0\.1\.3') { throw "SEK tool version mismatch: $version" }

if ($InstallExtension) {
    $extensionRoot = Join-Path $assetRoot 'extension'
    Remove-Item -LiteralPath $extensionRoot -Recurse -Force -ErrorAction SilentlyContinue
    Expand-Archive -LiteralPath (Join-Path $assetRoot 'spec-kit-sek.zip') -DestinationPath $extensionRoot
    specify extension add $extensionRoot --dev --force
    if ($LASTEXITCODE -ne 0) { throw 'Failed to install SEK Spec Kit extension v0.1.3.' }
    $extensionManifest = Join-Path $repoRoot '.specify/extensions/sek/extension.yml'
    if (-not (Test-Path -LiteralPath $extensionManifest -PathType Leaf) -or
        (Get-Content -LiteralPath $extensionManifest -Raw) -notmatch 'version:\s*"0\.1\.3"') {
        throw 'Installed SEK extension version mismatch.'
    }
}

Write-Output "SEK_V013_READY assets=$assetRoot"
