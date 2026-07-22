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
$elkVersion = '1.8.1'
$bootstrapRoot = Join-Path $env:LOCALAPPDATA 'EngLoopKit\bootstrap\1.8.1'
$releaseBase = 'https://github.com/stuartpa/engloopkit/releases/download/v1.8.1'

$expected = @{
    # Copy v1.8.1 release hashes here before execution.
    'engloopkit.1.8.1.nupkg' = '<sha256-from-v1.8.1-release>'
    'engloopkit-extension-1.8.1.zip' = '<sha256-from-v1.8.1-release>'
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
$nupkg = Join-Path $bootstrapRoot 'engloopkit.1.8.1.nupkg'
$extensionZip = Join-Path $bootstrapRoot 'engloopkit-extension-1.8.1.zip'

Invoke-WebRequest "$releaseBase/engloopkit.1.8.1.nupkg" -OutFile $nupkg
Invoke-WebRequest "$releaseBase/engloopkit-extension-1.8.1.zip" -OutFile $extensionZip

Assert-Sha256 -Path $nupkg -Expected $expected['engloopkit.1.8.1.nupkg']
Assert-Sha256 -Path $extensionZip -Expected $expected['engloopkit-extension-1.8.1.zip']

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

## Register runtime-created local outputs

Overlay installation can only know its installation-time files. Before a workflow creates
an additional local model or generated output outside `.engloop/`, register that exact
path explicitly:

```powershell
dotnet tool run engloopkit -- overlay register --root . `
    --directory <model-or-generated-directory> `
    --file <generated-file>
```

The command updates `.engloop-overlay/manifest.json` and the ELK block in
`.git/info/exclude`, then verifies the path is untracked, absent from staged/history
leakage, and locally ignored. It does not infer ownership from product names, module
inventory entries, or repository layout. Unregistered product source remains trackable.

## Restore private managed content after install

A fresh install records hashes for its initial managed files. If an operator intentionally
restores older private content over a managed file (for example a living direction or
local evidence), `overlay verify --mode all` must report
`overlay-manifest-file-mismatch` until the restored state is explicitly checkpointed.

Use this order:

1. Register any additional file/directory ownership **before** restoring that path.
2. Restore the private managed files from the authoritative private backup.
3. Run `overlay pack --root . --output <new-archive-outside-repository>`; the output must
    not already exist.
4. Run `overlay verify --root . --mode all` and require `OVERLAY_VERIFY_PASS`.

`overlay register` changes ownership/excludes; it does not bless content hashes. `overlay
pack` is the explicit hash-refresh checkpoint. Never hand-edit the manifest to suppress a
mismatch.

## Remove an overlay

`overlay remove` derives deletion only from the manifest and requires the exact
`REMOVE-OVERLAY:<repository-id>@<base-revision>` confirmation token. Registered directory
contents are quarantined child-first before the empty root is deleted. Installation stores
exact pre-install hook bytes (or an explicit absent marker) under overlay-owned state;
removal restores those bytes atomically before reporting `OVERLAY_REMOVE_PASS`.

Legacy overlays without authoritative hook-baseline metadata preserve an ambiguous
existing ELK wrapper rather than silently deleting repository protection.

## Existing agent directories or hooks

ELK v1.8.1 adds an explicit coexistence contract for a repository that already owns a
local Spec Kit host, agent directories, or local hooks. Replace `--host-mode clean` above
with `--host-mode coexist` only when the target has a pre-existing local `.specify/`
directory:

```powershell
dotnet tool run engloopkit -- overlay install `
    --mode overlay `
    --host-mode coexist `
    --root $targetRepositoryRoot `
    --product-id $productId `
    --repository-id $repositoryId `
    --tool-version $elkVersion `
    --tool-nupkg $nupkg `
    --extension-archive $extensionZip
```

Coexist mode preserves existing `.github/agents/` and `.github/prompts/` content
byte-for-byte. ELK adds only exact `speckit.engloop.*` entries. An existing local hook is
preserved as `*.elk-prior`; the ELK wrapper invokes it before ELK verification. Tracked
shared Spec Kit registration files or exact ELK name collisions fail closed. Do not delete,
rename, or manually merge existing host files.
