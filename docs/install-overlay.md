# Install ELK as a private overlay

Install EngLoopKit (ELK) into an existing Git repository without adding ELK state to
ordinary commits or pushes.

The installer is explicitly local-only:

- It does not edit tracked `.gitignore` files.
- It does not edit application/workload source files.
- It writes only local Git excludes and ELK-owned local state.
- The bootstrap tool manifest is outside the target repository.
- The procedure verifies pinned release artifact hashes before installation.

> Keep target-repository paths, product IDs, repository IDs, origin URLs, and other
> workload-specific values in a private operational runbook—not in this public document.

## Prerequisites

- Windows PowerShell 5.1 or PowerShell 7+
- Git on `PATH`
- .NET 8 SDK on `PATH`
- An existing Git checkout selected at its repository root
- Permission to create local files under `%LOCALAPPDATA%`

## Bootstrap and install

```powershell
$ErrorActionPreference = 'Stop'

# Set these values in your private operational runbook.
$targetRepositoryRoot = '<repository-root>'
$productId = '<lowercase-product-id>'
$repositoryId = '<stable-repository-id>'
$elkVersion = '1.8.0'
$bootstrapRoot = Join-Path $env:LOCALAPPDATA 'EngLoopKit\bootstrap\1.8.0'
$releaseBase = 'https://github.com/stuartpa/engloopkit/releases/download/v1.8.0'

$expected = @{
    'engloopkit.1.8.0.nupkg' = 'c3b2f488938f58a921ae54a2dcbc5427978ecdb661b96ba09a69dc18713d4724'
    'engloopkit-extension-1.8.0.zip' = 'e0b3da4c16a3fc09dc347e9a89fe475f5031bdecb7568db49ba179b90abb9b92'
}

function Assert-Sha256 {
    param([string]$Path, [string]$Expected)
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Expected.ToLowerInvariant()) {
        throw "SHA-256 mismatch for '$Path'. Expected $Expected; got $actual."
    }
}

foreach ($command in 'git', 'dotnet') {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Required command is not available on PATH: $command"
    }
}

if (-not (Test-Path -LiteralPath $targetRepositoryRoot -PathType Container)) {
    throw "Target repository root does not exist: $targetRepositoryRoot"
}

$targetRepositoryRoot = (Resolve-Path -LiteralPath $targetRepositoryRoot).Path
$gitRoot = (& git -C $targetRepositoryRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Target is not a Git repository: $targetRepositoryRoot"
}

$gitRoot = (Resolve-Path -LiteralPath $gitRoot).Path
if ($gitRoot.TrimEnd('\') -ine $targetRepositoryRoot.TrimEnd('\')) {
    throw "The selected path must be the Git root. Selected '$targetRepositoryRoot'; Git root '$gitRoot'."
}

New-Item -ItemType Directory -Path $bootstrapRoot -Force | Out-Null
$nupkg = Join-Path $bootstrapRoot 'engloopkit.1.8.0.nupkg'
$extensionZip = Join-Path $bootstrapRoot 'engloopkit-extension-1.8.0.zip'

Invoke-WebRequest "$releaseBase/engloopkit.1.8.0.nupkg" -OutFile $nupkg
Invoke-WebRequest "$releaseBase/engloopkit-extension-1.8.0.zip" -OutFile $extensionZip

Assert-Sha256 -Path $nupkg -Expected $expected['engloopkit.1.8.0.nupkg']
Assert-Sha256 -Path $extensionZip -Expected $expected['engloopkit-extension-1.8.0.zip']

# This manifest deliberately lives OUTSIDE the target repository.
Push-Location $bootstrapRoot
try {
    if (-not (Test-Path -LiteralPath (Join-Path $bootstrapRoot '.config\dotnet-tools.json'))) {
        dotnet new tool-manifest --force
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the private ELK bootstrap manifest.' }
    }

    dotnet tool install engloopkit --version $elkVersion --add-source $bootstrapRoot
    if ($LASTEXITCODE -ne 0) {
        dotnet tool update engloopkit --version $elkVersion --add-source $bootstrapRoot
        if ($LASTEXITCODE -ne 0) { throw 'Could not install the pinned ELK version.' }
    }

    dotnet tool run engloopkit -- overlay install `
        --mode overlay `
        --host-mode clean `
        --root $targetRepositoryRoot `
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

Push-Location $targetRepositoryRoot
try {
    dotnet tool run engloopkit -- overlay verify --root . --mode all
    if ($LASTEXITCODE -ne 0) { throw 'ELK overlay verification failed.' }
    git check-ignore -v .config/dotnet-tools.json .engloop-overlay/manifest.json .engloop/config.json
}
finally {
    Pop-Location
}
```

## Important boundary

Do not create a tool manifest in the target repository before running overlay install.
The transaction creates the target-local manifest only after local Git exclusions are in
place.

Normal Git hooks protect ordinary commit/push flows. Do not bypass them with
`--no-verify` if you want overlay protection enforced.

## Existing agent directories or hooks

ELK v1.8.0 `clean` host mode intentionally fails closed when the target already owns
agent directories, generated command surfaces, or non-ELK hooks. Do not delete, rename,
or manually merge those existing files. Use a release that explicitly supports a
repository-host coexistence contract instead.
