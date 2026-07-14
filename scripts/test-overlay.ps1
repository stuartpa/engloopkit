[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..'),
    [string]$ToolNupkg,
    [string]$ExtensionArchive,
    [string]$Version = '1.8.0',
    [switch]$KeepWork
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path $Root).Path
$toolNupkg = (Resolve-Path $ToolNupkg).Path
$extensionArchive = (Resolve-Path $ExtensionArchive).Path
$work = Join-Path $repoRoot ('.engloop/out/overlay-integration-' + [Guid]::NewGuid().ToString('N'))
$bare = Join-Path $work 'origin.git'
$source = Join-Path $work 'source'
$target = Join-Path $work 'target'
$driver = Join-Path $work 'driver'
$archive = Join-Path $work 'private-overlay.zip'

function Invoke-Checked {
    param([string]$File, [string]$WorkingDirectory, [string[]]$Arguments)
    Push-Location $WorkingDirectory
    try {
        $output = (& $File @Arguments 2>&1 | Out-String)
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed ($LASTEXITCODE): $File $($Arguments -join ' ')`n$output"
        }
        if (-not [string]::IsNullOrWhiteSpace($output)) { Write-Host $output }
    }
    finally {
        Pop-Location
    }
}

function Invoke-ExpectedFailure {
    param([string]$File, [string]$WorkingDirectory, [string[]]$Arguments, [string]$Expected)
    Push-Location $WorkingDirectory
    try {
        $output = (& $File @Arguments 2>&1 | Out-String)
        if ($LASTEXITCODE -eq 0) {
            throw "Expected failure succeeded: $File $($Arguments -join ' ')"
        }
        if ($output -notmatch [regex]::Escape($Expected)) {
            throw "Expected failure '$Expected' not observed. Output: $output"
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

try {
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    Invoke-Checked git $work @('init', '--bare', $bare)
    Invoke-Checked git $work @('init', $source)
    Invoke-Checked git $source @('config', 'user.email', 'overlay@example.invalid')
    Invoke-Checked git $source @('config', 'user.name', 'Overlay Test')
    Set-Content (Join-Path $source 'README.md') '# Existing repository' -Encoding utf8
    Invoke-Checked git $source @('add', 'README.md')
    Invoke-Checked git $source @('commit', '-m', 'initial')
    Invoke-Checked git $source @('branch', '-M', 'main')
    Invoke-Checked git $source @('remote', 'add', 'origin', $bare)
    Invoke-Checked git $source @('push', '-u', 'origin', 'main')

    New-Item -ItemType Directory -Path $driver -Force | Out-Null
    Clear-ExactToolCache -PackageId 'engloopkit' -PackageVersion $Version
    Invoke-Checked dotnet $driver @('new', 'tool-manifest', '--force')
    $driverManifest = Join-Path $driver '.config/dotnet-tools.json'
    Invoke-Checked dotnet $driver @('tool', 'install', 'engloopkit', '--version', $Version, '--add-source', (Split-Path $toolNupkg -Parent), '--tool-manifest', $driverManifest, '--no-cache')

    Invoke-Checked dotnet $driver @('tool', 'run', 'engloopkit', '--', 'overlay', 'install', '--mode', 'overlay', '--root', $source, '--product-id', 'overlay-test', '--repository-id', 'overlay-test-repository', '--tool-version', $Version, '--tool-nupkg', $toolNupkg, '--extension-archive', $extensionArchive)
    Invoke-Checked dotnet $source @('tool', 'run', 'engloopkit', '--', 'overlay', 'verify', '--root', $source)

    $managed = @('.engloop/config.json', '.engloop-overlay/manifest.json', '.config/dotnet-tools.json', '.github/agents/speckit.engloop.01-northstar.agent.md')
    foreach ($path in $managed) {
        Invoke-Checked git $source @('check-ignore', '-q', '--', $path)
        $tracked = (& git -C $source ls-files -- $path | Out-String).Trim()
        if ($tracked) { throw "Managed path unexpectedly tracked: $path" }
    }
    $status = (& git -C $source status --short | Out-String).Trim()
    if ($status) { throw "Overlay paths leaked into status: $status" }

    # Normal pre-commit protection catches a force-staged managed path.
    Invoke-Checked git $source @('add', '-f', '.engloop/config.json')
    Invoke-ExpectedFailure git $source @('commit', '-m', 'must be blocked') 'overlay-managed-path-staged'
    Invoke-Checked git $source @('reset', '--', '.engloop/config.json')

    # Secrets are forbidden from the plain archive even under a managed root.
    Set-Content (Join-Path $source '.engloop/.env.local') 'not-a-real-secret' -Encoding utf8
    Invoke-ExpectedFailure dotnet $source @('tool', 'run', 'engloopkit', '--', 'overlay', 'pack', '--root', $source, '--output', $archive) 'overlay-secret-like-path-forbidden'
    Remove-Item (Join-Path $source '.engloop/.env.local') -Force

    Invoke-Checked dotnet $source @('tool', 'run', 'engloopkit', '--', 'overlay', 'pack', '--root', $source, '--output', $archive)
    if (-not (Test-Path $archive -PathType Leaf)) { throw 'Overlay archive missing after pack.' }

    Invoke-Checked git $work @('clone', '--branch', 'main', $bare, $target)
    Invoke-Checked git $target @('config', 'user.email', 'overlay@example.invalid')
    Invoke-Checked git $target @('config', 'user.name', 'Overlay Test')
    Invoke-Checked dotnet $driver @('tool', 'run', 'engloopkit', '--', 'overlay', 'unpack', '--root', $target, '--input', $archive, '--repository-id', 'overlay-test-repository')
    Invoke-Checked dotnet $target @('tool', 'run', 'engloopkit', '--', 'overlay', 'verify', '--root', $target)

    $targetStatus = (& git -C $target status --short | Out-String).Trim()
    if ($targetStatus) { throw "Unpack leaked managed paths into target status: $targetStatus" }
    $sourceManifest = Get-Content (Join-Path $source '.engloop-overlay/manifest.json') -Raw | ConvertFrom-Json
    $targetManifest = Get-Content (Join-Path $target '.engloop-overlay/manifest.json') -Raw | ConvertFrom-Json
    foreach ($file in $sourceManifest.files) {
        $targetFile = $targetManifest.files | Where-Object { $_.relativePath -eq $file.relativePath } | Select-Object -First 1
        if ($null -eq $targetFile -or $targetFile.sha256 -ne $file.sha256) {
            throw "Unpacked manifest mismatch: $($file.relativePath)"
        }
    }

    # A matching Git checkout but wrong explicit repository identity must fail before mutation.
    $wrong = Join-Path $work 'wrong-identity'
    Invoke-Checked git $work @('clone', '--branch', 'main', $bare, $wrong)
    Invoke-ExpectedFailure dotnet $driver @('tool', 'run', 'engloopkit', '--', 'overlay', 'unpack', '--root', $wrong, '--input', $archive, '--repository-id', 'wrong-id') 'overlay-repository-id-mismatch'

    Write-Output "OVERLAY_INTEGRATION_PASS archive=$archive"
}
finally {
    if (-not $KeepWork -and (Test-Path $work)) {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}
