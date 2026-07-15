# Install ELK v1.8.0 in overlay mode into DsMainDev

This runbook installs **EngLoopKit (ELK) v1.8.0** as a private overlay in the
`DsMainDev` repository, the main SQL Server code base.

The procedure is intentionally local-only:

- It does not edit tracked `.gitignore` files.
- It does not edit SQL Server source files.
- It does not add ELK files to ordinary commits or pushes.
- It creates the bootstrap tool manifest **outside** `DsMainDev`.
- It verifies the downloaded release artifacts before installation.

> Run the following PowerShell from the machine that contains the `DsMainDev` checkout.
> Edit only the configuration values in the first section if your path or identity differs.

## Prerequisites

- Windows PowerShell 5.1 or PowerShell 7+
- Git on `PATH`
- .NET 8 SDK on `PATH`
- An existing `DsMainDev` Git checkout
- Permission to create local files under `%LOCALAPPDATA%`
- The checkout must be the selected repository root, not a subdirectory

## Download, verify, and install

```powershell
$ErrorActionPreference = 'Stop'

# ----- Explicit configuration -----
$DsMainDevRoot = 'C:\src\DsMainDev'
$productId = 'dsmain-dev'
$repositoryId = 'dsmain-dev'
$elkVersion = '1.8.0'
$bootstrapRoot = Join-Path $env:LOCALAPPDATA 'EngLoopKit\bootstrap\1.8.0'
$releaseBase = 'https://github.com/stuartpa/engloopkit/releases/download/v1.8.0'

# ----- Expected immutable release hashes -----
$expected = @{
    'engloopkit.1.8.0.nupkg' = 'c3b2f488938f58a921ae54a2dcbc5427978ecdb661b96ba09a69dc18713d4724'
    'engloopkit-extension-1.8.0.zip' = 'e0b3da4c16a3fc09dc347e9a89fe475f5031bdecb7568db49ba179b90abb9b92'
}

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command is not available on PATH: $Name"
    }
}

function Assert-Sha256 {
    param(
        [string]$Path,
        [string]$Expected
    )

    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Expected.ToLowerInvariant()) {
        throw "SHA-256 mismatch for '$Path'. Expected $Expected but got $actual."
    }
    Write-Host "Verified SHA-256: $([IO.Path]::GetFileName($Path))" -ForegroundColor Green
}

Require-Command 'git'
Require-Command 'dotnet'

if (-not (Test-Path -LiteralPath $DsMainDevRoot -PathType Container)) {
    throw "DsMainDev checkout does not exist: $DsMainDevRoot"
}

$DsMainDevRoot = (Resolve-Path -LiteralPath $DsMainDevRoot).Path
$gitRoot = (& git -C $DsMainDevRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "DsMainDevRoot is not a Git repository: $DsMainDevRoot"
}

$gitRoot = (Resolve-Path -LiteralPath $gitRoot).Path
if ($gitRoot.TrimEnd('\') -ine $DsMainDevRoot.TrimEnd('\')) {
    throw "DsMainDevRoot must be the selected Git root. Selected '$DsMainDevRoot'; Git root is '$gitRoot'."
}

# Confirm the repository has an explicit origin when one exists. ELK will bind the
# overlay archive to this repository identity and later verify it on unpack.
$origin = (& git -C $DsMainDevRoot config --get remote.origin.url 2>$null).Trim()
if ($LASTEXITCODE -eq 0 -and [string]::IsNullOrWhiteSpace($origin)) {
    throw 'Git origin is configured but empty; fix the repository identity before installing ELK.'
}

New-Item -ItemType Directory -Path $bootstrapRoot -Force | Out-Null
$nupkg = Join-Path $bootstrapRoot 'engloopkit.1.8.0.nupkg'
$extensionZip = Join-Path $bootstrapRoot 'engloopkit-extension-1.8.0.zip'

# Download exactly the pinned v1.8.0 artifacts.
Invoke-WebRequest "$releaseBase/engloopkit.1.8.0.nupkg" -OutFile $nupkg
Invoke-WebRequest "$releaseBase/engloopkit-extension-1.8.0.zip" -OutFile $extensionZip

Assert-Sha256 -Path $nupkg -Expected $expected['engloopkit.1.8.0.nupkg']
Assert-Sha256 -Path $extensionZip -Expected $expected['engloopkit-extension-1.8.0.zip']

# The bootstrap tool manifest is deliberately outside DsMainDev. Do not run
# 'dotnet new tool-manifest' in DsMainDev before overlay installation.
Push-Location $bootstrapRoot
try {
    if (-not (Test-Path -LiteralPath (Join-Path $bootstrapRoot '.config\dotnet-tools.json'))) {
        dotnet new tool-manifest --force
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the private ELK bootstrap tool manifest.' }
    }

    dotnet tool install engloopkit --version $elkVersion --add-source $bootstrapRoot
    if ($LASTEXITCODE -ne 0) {
        # A previous installation in this private bootstrap directory is allowed only
        # when it is the same explicit version; never silently switch versions.
        dotnet tool update engloopkit --version $elkVersion --add-source $bootstrapRoot
        if ($LASTEXITCODE -ne 0) { throw 'Could not install or update the pinned ELK version.' }
    }

    dotnet tool run engloopkit -- overlay install `
        --mode overlay `
        --root $DsMainDevRoot `
        --product-id $productId `
        --repository-id $repositoryId `
        --tool-version $elkVersion `
        --tool-nupkg $nupkg `
        --extension-archive $extensionZip
    if ($LASTEXITCODE -ne 0) { throw 'ELK overlay installation failed.' }
}
finally {
    Pop-Location
}

# Verify through the target repository's newly created, ignored local tool manifest.
Push-Location $DsMainDevRoot
try {
    dotnet tool run engloopkit -- overlay verify --root . --mode all
    if ($LASTEXITCODE -ne 0) { throw 'ELK overlay verification failed after installation.' }

    Write-Host ''
    Write-Host 'ELK overlay installation succeeded.' -ForegroundColor Green
    Write-Host "DsMainDev: $DsMainDevRoot"
    Write-Host "Product ID: $productId"
    Write-Host "Repository ID: $repositoryId"
    Write-Host "ELK version: $elkVersion"
    Write-Host ''
    Write-Host 'Local managed paths:' -ForegroundColor Cyan
    git check-ignore -v .config/dotnet-tools.json .engloop-overlay/manifest.json .engloop/config.json 2>$null
    Write-Host ''
    Write-Host 'The overlay is ready. Do not stage ELK-managed paths and do not bypass the ELK Git hooks with --no-verify.'
}
finally {
    Pop-Location
}
```

## Expected result

The install should print:

```text
OVERLAY_INSTALL_PASS
OVERLAY_VERIFY_PASS
ELK overlay installation succeeded.
```

The following files are local to `DsMainDev` and should be reported as ignored by
`git check-ignore`:

- `.config/dotnet-tools.json`
- `.engloop-overlay/manifest.json`
- `.engloop/config.json`

ELK also installs local `pre-commit` and `pre-push` hooks. Those hooks reject ELK-managed
paths from staged changes and pushed history. Do not bypass them with `--no-verify` if you
want overlay protection enforced.

## Moving the overlay to another DsMainDev checkout

Use the target checkout's local tool to pack the state:

```powershell
Push-Location 'C:\src\DsMainDev'
dotnet tool run engloopkit -- overlay pack --root . --output "$env:TEMP\dsmain-dev-overlay.zip"
Pop-Location
```

For a second checkout that does not yet have ELK installed, use the same external
`$bootstrapRoot` tool from the installation procedure:

```powershell
Push-Location $bootstrapRoot
dotnet tool run engloopkit -- overlay unpack `
    --root 'C:\src\DsMainDev-second-checkout' `
    --input "$env:TEMP\dsmain-dev-overlay.zip" `
    --repository-id 'dsmain-dev'
Pop-Location
```

The receiving checkout must have the same repository identity and packed base revision.
ELK rejects tracked collisions, ZIP path traversal, secret-like paths, missing local tool
files, hash mismatches, origin mismatches, and repository/base-revision mismatches.
