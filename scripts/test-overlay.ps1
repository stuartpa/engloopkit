[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..'),
    [string]$ToolNupkg,
    [string]$ExtensionArchive,
    [string]$Version = '1.10.0',
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
$coexist = Join-Path $work 'coexist'

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

    # Runtime ownership selected after installation must be registered before creation.
    Invoke-Checked dotnet $source @('tool', 'run', 'engloopkit', '--', 'overlay', 'register', '--root', $source,
        '--directory', 'runtime-model/Foo.Model', '--file', 'tests/Generated/Foo.g.cs')
    New-Item -ItemType Directory -Path (Join-Path $source 'runtime-model/Foo.Model') -Force | Out-Null
    Set-Content (Join-Path $source 'runtime-model/Foo.Model/Foo.Model.csproj') '<Project />' -Encoding utf8
    New-Item -ItemType Directory -Path (Join-Path $source 'tests/Generated') -Force | Out-Null
    Set-Content (Join-Path $source 'tests/Generated/Foo.g.cs') '// generated' -Encoding utf8
    Invoke-Checked git $source @('check-ignore', '-q', '--no-index', '--', 'runtime-model/Foo.Model/Foo.Model.csproj')
    Invoke-Checked git $source @('check-ignore', '-q', '--no-index', '--', 'tests/Generated/Foo.g.cs')
    Invoke-Checked git $source @('add', '-f', 'tests/Generated/Foo.g.cs')
    Invoke-ExpectedFailure dotnet $source @('tool', 'run', 'engloopkit', '--', 'overlay', 'verify', '--root', $source, '--mode', 'staged') 'overlay-managed-path-staged:tests/Generated/Foo.g.cs'
    Invoke-Checked git $source @('reset', '--', 'tests/Generated/Foo.g.cs')

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

    # Generic coexistence host: the repository owns existing agent content and an LFS-style
    # local pre-push hook. ELK must preserve them and add only namespaced ELK entries.
    Invoke-Checked git $work @('clone', '--branch', 'main', $bare, $coexist)
    Invoke-Checked git $coexist @('config', 'user.email', 'overlay@example.invalid')
    Invoke-Checked git $coexist @('config', 'user.name', 'Overlay Test')
    Invoke-Checked specify $coexist @('init', '--here', '--force', '--integration', 'copilot', '--script', 'ps', '--ignore-agent-tools')
    $agents = Join-Path $coexist '.github/agents'
    New-Item -ItemType Directory -Path $agents -Force | Out-Null
    $existingAgent = Join-Path $agents 'existing.agent.md'
    $localAgent = Join-Path $agents 'local.agent.md'
    Set-Content $existingAgent 'tracked existing agent' -Encoding utf8
    Set-Content $localAgent 'local existing agent' -Encoding utf8
    $existingBytes = [IO.File]::ReadAllBytes($existingAgent)
    $localBytes = [IO.File]::ReadAllBytes($localAgent)
    Invoke-Checked git $coexist @('add', $existingAgent)
    Invoke-Checked git $coexist @('commit', '-m', 'existing agent host')
    Invoke-Checked git $coexist @('push')
    $prePush = Join-Path $coexist '.git/hooks/pre-push'
    $lfsHook = "#!/bin/sh`ncommand -v git-lfs >/dev/null 2>&1 || exit 2`ngit lfs pre-push `"`$@`"`n"
    Set-Content $prePush $lfsHook -NoNewline -Encoding utf8
    $lfsHookBytes = [IO.File]::ReadAllBytes($prePush)

    Invoke-Checked dotnet $driver @('tool', 'run', 'engloopkit', '--', 'overlay', 'install', '--mode', 'overlay', '--host-mode', 'coexist', '--root', $coexist, '--product-id', 'overlay-test', '--repository-id', 'overlay-test-repository', '--tool-version', $Version, '--tool-nupkg', $toolNupkg, '--extension-archive', $extensionArchive)
    Invoke-Checked dotnet $coexist @('tool', 'run', 'engloopkit', '--', 'overlay', 'verify', '--root', $coexist)
    if (-not ([System.Linq.Enumerable]::SequenceEqual([IO.File]::ReadAllBytes($existingAgent), $existingBytes)) -or -not ([System.Linq.Enumerable]::SequenceEqual([IO.File]::ReadAllBytes($localAgent), $localBytes))) {
        throw 'Coexistence changed a repository-owned agent file.'
    }
    if (-not (Test-Path (Join-Path $coexist '.github/agents/speckit.engloop.01-northstar.agent.md'))) {
        throw 'Coexistence did not create the ELK namespaced agent entry.'
    }
    if (-not ([System.Linq.Enumerable]::SequenceEqual([IO.File]::ReadAllBytes($prePush + '.elk-prior'), $lfsHookBytes)) -or -not (Select-String -Path $prePush -Pattern 'ELK_OVERLAY_HOOK' -Quiet)) {
        throw 'Coexistence did not preserve and chain the existing pre-push hook.'
    }

    $coexistManifest = Get-Content (Join-Path $coexist '.engloop-overlay/manifest.json') -Raw | ConvertFrom-Json
    $removeToken = "REMOVE-OVERLAY:$($coexistManifest.repositoryId)@$($coexistManifest.baseRevision)"
    Invoke-Checked dotnet $coexist @('tool', 'run', 'engloopkit', '--', 'overlay', 'remove', '--root', $coexist, '--confirm', $removeToken)
    if (Test-Path (Join-Path $coexist '.engloop-overlay/manifest.json')) { throw 'Overlay manifest remained after removal.' }
    if (Test-Path (Join-Path $coexist '.github/agents/speckit.engloop.01-northstar.agent.md')) { throw 'ELK agent remained after removal.' }
    if (-not ([System.Linq.Enumerable]::SequenceEqual([IO.File]::ReadAllBytes($existingAgent), $existingBytes)) -or -not ([System.Linq.Enumerable]::SequenceEqual([IO.File]::ReadAllBytes($localAgent), $localBytes))) {
        throw 'Removal changed a repository-owned agent file.'
    }
    if (-not ([System.Linq.Enumerable]::SequenceEqual([IO.File]::ReadAllBytes($prePush), $lfsHookBytes)) -or (Test-Path ($prePush + '.elk-prior'))) {
        throw 'Removal did not restore the pre-existing pre-push hook exactly.'
    }

    Write-Output "OVERLAY_INTEGRATION_PASS archive=$archive"
}
finally {
    if (-not $KeepWork -and (Test-Path $work)) {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}
