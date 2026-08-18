# HANDOFF001: Bootstrap DsMainDev to ELK v1.14.0

- **Recipient:** DsMainDev repository maintainers / coding agent
- **Status:** READY
- **Purpose:** One-time bootstrap to the first release that contains Stage 80. After this succeeds, use `/speckit.engloop.80-upgrade-elk` for every later ELK update.
- **ELK release:** [v1.14.0](https://github.com/stuartpa/engloopkit/releases/tag/v1.14.0)
- **SEK dependency:** [v0.1.3](https://github.com/stuartpa/sek/releases/tag/v0.1.3), native .NET 10
- **Tested platform:** .NET SDK 10.0.303, `net10.0`, Spec Kit CLI 0.12.4

## Expected installed state

- Root-local `engloopkit` tool: `1.14.0`
- Installed Spec Kit extension `engloop`: `1.14.0`
- Root-local `SpecExplorerKit.Tool`: `0.1.3`
- Installed Spec Kit extension `sek`: `0.1.3`
- Exact ELK surface: 25 commands, 25 agents, 25 prompts
- Stewardship commands:
  - `/speckit.engloop.40-refactor`
  - `/speckit.engloop.41-deadcode`
  - `/speckit.engloop.42-learnings-pyramid`
- Future updater: `/speckit.engloop.80-upgrade-elk`

## Preconditions

1. Run from the **exact DsMainDev Git root**, not a parent or child directory.
2. Commit or stash pending changes under these managed paths before bootstrapping:
   `.config/dotnet-tools.json`, `.specify/`, `.github/agents/`, and `.github/prompts/`.
3. Install .NET 10. The release requires at least `10.0.0`; SDK `10.0.303` is the pinned and tested version.
4. Install a compatible Spec Kit CLI (`specify >= 0.12.0`; `0.12.4` is the tested version).
5. The repository must already be an initialized Spec Kit project with a `.specify/` directory. If it is not, stop and initialize it according to DsMainDev repository policy before continuing.
6. Do not install either .NET tool globally, use a sibling source checkout, or copy generated agent files manually.

## One-time bootstrap

Run the following PowerShell 7 block from the exact DsMainDev repository root. It downloads all published assets, verifies pinned SHA-256 values before mutation, updates root-local tools, installs the exact release extensions, and runs deterministic validation.

```powershell
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$elkVersion = '1.14.0'
$sekVersion = '0.1.3'
$elkBase = "https://github.com/stuartpa/engloopkit/releases/download/v$elkVersion"
$sekBase = "https://github.com/stuartpa/sek/releases/download/v$sekVersion"

$root = (Resolve-Path '.').Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
$gitRootText = (& git -C $root rev-parse --show-toplevel 2>$null | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitRootText)) {
    throw 'Not a Git repository.'
}
$gitRoot = (Resolve-Path $gitRootText).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
if (-not [string]::Equals($root, $gitRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Run from the exact Git root. Selected='$root'; GitRoot='$gitRoot'."
}

$managedStatus = (& git -C $root status --porcelain -- `
    .config/dotnet-tools.json .specify .github/agents .github/prompts | Out-String).Trim()
if ($managedStatus) {
    throw "Managed paths have pending changes. Commit or stash them first:`n$managedStatus"
}

$dotnetText = (& dotnet --version | Out-String).Trim()
$dotnetVersion = $null
if (-not [version]::TryParse($dotnetText, [ref]$dotnetVersion) -or
    $dotnetVersion -lt [version]'10.0.0') {
    throw "ELK v1.14.0 requires .NET SDK >=10.0.0; selected='$dotnetText'."
}

$specifyText = (& specify --version 2>&1 | Out-String).Trim()
$specifyMatch = [regex]::Match($specifyText, '(?m)^specify\s+(?<version>\d+\.\d+\.\d+)\s*$')
if (-not $specifyMatch.Success -or
    [version]$specifyMatch.Groups['version'].Value -lt [version]'0.12.0') {
    throw "Compatible Spec Kit CLI not found; output='$specifyText'."
}
if (-not (Test-Path -LiteralPath (Join-Path $root '.specify') -PathType Container)) {
    throw 'This is not an initialized Spec Kit project: .specify/ is missing.'
}

$work = Join-Path ([IO.Path]::GetTempPath()) 'dsmain-elk-v1.14.0-bootstrap'
Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $work -Force | Out-Null

$assets = [ordered]@{
    'engloopkit-release-manifest-1.14.0.json' = [ordered]@{
        url = "$elkBase/engloopkit-release-manifest-1.14.0.json"
        sha256 = '569ac054cd15fcb59d147997680464635536a4fcf93e9aa7430156b1fa53a5a5'
    }
    'engloopkit.1.14.0.nupkg' = [ordered]@{
        url = "$elkBase/engloopkit.1.14.0.nupkg"
        sha256 = '7408db406da7b4794d4feba40004e616e6e54720db711e1741774537b3c3cde7'
    }
    'engloopkit-extension-1.14.0.zip' = [ordered]@{
        url = "$elkBase/engloopkit-extension-1.14.0.zip"
        sha256 = 'e01fec4a1366c9f7fad76723cc4754544e9299ecf598fe6e13a7d4c521710385'
    }
    'engloopkit-1.14.0.zip' = [ordered]@{
        url = "$elkBase/engloopkit-1.14.0.zip"
        sha256 = 'db570d666392fd35b8dcb5021113dc36ab755742323e130f61ab4620dc8e86a3'
    }
    'SpecExplorerKit.Tool.0.1.3.nupkg' = [ordered]@{
        url = "$sekBase/SpecExplorerKit.Tool.0.1.3.nupkg"
        sha256 = '5bda43161665fba562375023afe2c205fa2dfa3f765cf290336d32953f49ee36'
    }
    'SpecExplorerKit.Modeling.0.1.3.nupkg' = [ordered]@{
        url = "$sekBase/SpecExplorerKit.Modeling.0.1.3.nupkg"
        sha256 = '95afc47fe0cf0216fa6d1288a7bc9f013c50f9e61d6ca0eb4c683724fa95566a'
    }
    'spec-kit-sek.zip' = [ordered]@{
        url = "$sekBase/spec-kit-sek.zip"
        sha256 = '4fb18feeb679fb20f018321e9a9b50ba9814e6d63256c29dbe7d6bde619f6b63'
    }
}

foreach ($name in $assets.Keys) {
    $path = Join-Path $work $name
    Invoke-WebRequest -Uri $assets[$name].url -OutFile $path -UseBasicParsing
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $assets[$name].sha256) {
        throw "SHA-256 mismatch for '$name'. Expected=$($assets[$name].sha256); Actual=$actual"
    }
}

$releaseManifest = Get-Content `
    (Join-Path $work 'engloopkit-release-manifest-1.14.0.json') -Raw |
    ConvertFrom-Json
if ($releaseManifest.version -ne $elkVersion -or
    $releaseManifest.commands.count -ne 25 -or
    $releaseManifest.dependencies.sek.version -ne $sekVersion -or
    $releaseManifest.platform.dotnetSdk.pinned -ne '10.0.303' -or
    $releaseManifest.platform.targetFramework -ne 'net10.0') {
    throw 'Published ELK release manifest does not match the expected bootstrap contract.'
}

$configRoot = Join-Path $root '.config'
$toolManifest = Join-Path $configRoot 'dotnet-tools.json'
if (-not (Test-Path -LiteralPath $toolManifest -PathType Leaf)) {
    New-Item -ItemType Directory -Path $configRoot -Force | Out-Null
    & dotnet new tool-manifest --force --output $configRoot
    if ($LASTEXITCODE -ne 0) { throw 'Failed to create the root-local tool manifest.' }
}

function Set-RootLocalTool {
    param([string]$PackageId, [string]$ManifestKey, [string]$Version)

    $state = Get-Content -LiteralPath $toolManifest -Raw | ConvertFrom-Json -AsHashtable
    if ($state.tools.ContainsKey($ManifestKey)) {
        & dotnet tool update $PackageId --version $Version `
            --tool-manifest $toolManifest --add-source $work --no-cache
    }
    else {
        & dotnet tool install $PackageId --version $Version `
            --tool-manifest $toolManifest --add-source $work --no-cache
    }
    if ($LASTEXITCODE -ne 0) { throw "Failed to install root-local $PackageId $Version." }
}

Set-RootLocalTool -PackageId 'engloopkit' `
    -ManifestKey 'engloopkit' -Version $elkVersion
Set-RootLocalTool -PackageId 'SpecExplorerKit.Tool' `
    -ManifestKey 'specexplorerkit.tool' -Version $sekVersion

& dotnet tool restore --tool-manifest $toolManifest --add-source $work --no-http-cache
if ($LASTEXITCODE -ne 0) { throw 'Root-local tool restore failed.' }

$sekExtension = Join-Path $work 'sek-extension'
$elkExtension = Join-Path $work 'elk-extension'
Expand-Archive -LiteralPath (Join-Path $work 'spec-kit-sek.zip') `
    -DestinationPath $sekExtension -Force
Expand-Archive -LiteralPath (Join-Path $work 'engloopkit-extension-1.14.0.zip') `
    -DestinationPath $elkExtension -Force

& specify extension add $sekExtension --dev --force
if ($LASTEXITCODE -ne 0) { throw 'SEK extension installation failed.' }
& specify extension add $elkExtension --dev --force
if ($LASTEXITCODE -ne 0) { throw 'ELK extension installation failed.' }

$toolState = Get-Content -LiteralPath $toolManifest -Raw | ConvertFrom-Json -AsHashtable
if ($toolState.tools.engloopkit.version -ne $elkVersion -or
    $toolState.tools['specexplorerkit.tool'].version -ne $sekVersion) {
    throw 'Root-local tool versions do not match the requested release.'
}

$elkInstalled = Get-Content '.specify/extensions/engloop/extension.yml' -Raw
$sekInstalled = Get-Content '.specify/extensions/sek/extension.yml' -Raw
if ($elkInstalled -notmatch 'version:\s*"1\.14\.0"' -or
    $sekInstalled -notmatch 'version:\s*"0\.1\.3"') {
    throw 'Installed extension versions do not match the requested release.'
}

& dotnet tool run sek -- version
if ($LASTEXITCODE -ne 0) { throw 'SEK tool validation failed.' }
& dotnet tool run engloopkit -- validate root --root .
if ($LASTEXITCODE -ne 0) { throw 'ELK root validation failed.' }
& dotnet tool run engloopkit -- validate config --root .
if ($LASTEXITCODE -ne 0) { throw 'ELK config validation failed.' }
& dotnet tool run engloopkit -- validate commands --root .
if ($LASTEXITCODE -ne 0) { throw 'ELK command validation failed.' }
& dotnet tool run engloopkit -- validate agent-surfaces --root .
if ($LASTEXITCODE -ne 0) { throw 'ELK agent-surface validation failed.' }
& dotnet tool run engloopkit -- validate installation --root .
if ($LASTEXITCODE -ne 0) { throw 'ELK installation validation failed.' }

$agents = @(Get-ChildItem '.github/agents' -Filter 'speckit.engloop.*.agent.md' -File)
$prompts = @(Get-ChildItem '.github/prompts' -Filter 'speckit.engloop.*.prompt.md' -File)
if ($agents.Count -ne 25 -or $prompts.Count -ne 25) {
    throw "Expected 25 ELK agents/prompts; found agents=$($agents.Count), prompts=$($prompts.Count)."
}
foreach ($required in @(
    '.github/agents/speckit.engloop.40-refactor.agent.md',
    '.github/agents/speckit.engloop.41-deadcode.agent.md',
    '.github/agents/speckit.engloop.42-learnings-pyramid.agent.md',
    '.github/agents/speckit.engloop.80-upgrade-elk.agent.md')) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required installed agent is missing: $required"
    }
}

$check = & pwsh -NoProfile -File `
    '.specify/extensions/engloop/scripts/Update-EngLoopKit.ps1' `
    -Root . -CheckOnly | Out-String
if ($LASTEXITCODE -ne 0 -or
    $check -notmatch 'ELK_UPGRADE_CURRENT version=1\.14\.0 sek=0\.1\.3') {
    throw "Stage 80 current-version validation failed: $check"
}

Write-Host 'DSMAIN_ELK_BOOTSTRAP_PASS version=1.14.0 sek=0.1.3 commands=25' -ForegroundColor Green
Write-Host 'Future updates: run /speckit.engloop.80-upgrade-elk' -ForegroundColor Green
```

## Expected success

The last two lines must be:

```text
DSMAIN_ELK_BOOTSTRAP_PASS version=1.14.0 sek=0.1.3 commands=25
Future updates: run /speckit.engloop.80-upgrade-elk
```

Then review `git status`. The bootstrap may update repository-managed tool manifests,
Spec Kit extension state, and generated `.github/agents` / `.github/prompts` files.
Commit only according to DsMainDev repository policy. Never commit the temporary download
directory; this handoff places it outside the repository.

## Future upgrades

Do **not** rerun the bootstrap block for later releases. Use:

```text
/speckit.engloop.80-upgrade-elk
```

For a non-mutating check:

```powershell
pwsh -NoProfile -File .specify/extensions/engloop/scripts/Update-EngLoopKit.ps1 -Root . -CheckOnly
```

For an upgrade when explicitly requested:

```powershell
pwsh -NoProfile -File .specify/extensions/engloop/scripts/Update-EngLoopKit.ps1 -Root .
```

Stage 80 verifies the latest stable GitHub release manifest and hashes, snapshots managed
state, upgrades root-local ELK and SEK together, validates the exact installed surface,
and restores the prior verified release on failure.

## Troubleshooting boundary

If `dotnet tool run` says to restore after a successful restore, close active test/build
processes and retry from a fresh terminal. A stale per-user `.dotnet/toolResolverCache`
can cause that .NET CLI symptom; do not delete or modify it automatically. Escalate the
exact diagnostic rather than switching to global tools, a development checkout, another
package source, or copied binaries.
