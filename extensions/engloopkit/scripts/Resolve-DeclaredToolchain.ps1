[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,

    [ValidateRange(1, 60)]
    [int]$TimeoutSeconds = 10
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Result {
    param(
        [string]$Status,
        [string]$Manager,
        [string]$DeclaredVersion,
        [string[]]$Invocation,
        [string]$Reason,
        [hashtable]$Evidence
    )
    [ordered]@{
        schemaVersion = '1.0'
        status = $Status
        manager = $Manager
        declaredVersion = $DeclaredVersion
        invocation = @($Invocation)
        reason = $Reason
        evidence = $Evidence
    } | ConvertTo-Json -Depth 8 -Compress
}

function Get-BoundedText {
    param([string]$Value, [int]$Maximum = 4096)
    if ($null -eq $Value) { return '' }
    $normalized = $Value.Replace("`r`n", "`n").Replace("`r", "`n").Trim()
    if ($normalized.Length -le $Maximum) { return $normalized }
    return $normalized.Substring(0, $Maximum) + "`n...[truncated]"
}

function Assert-NoReparsePoint {
    param([string]$Path, [string]$Reason)
    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw $Reason }
}

function Get-Application {
    param([string]$Name)
    return Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
}

function ConvertTo-CmdArgument {
    param([string]$Value)
    if ($Value -notmatch '[\s"]') { return $Value }
    return '"' + ($Value.Replace('"', '\"')) + '"'
}

function Invoke-BoundedProcess {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [hashtable]$Environment,
        [int]$Timeout
    )
    $start = [Diagnostics.ProcessStartInfo]::new()
    $extension = [IO.Path]::GetExtension($FilePath)
    if ($extension -in @('.cmd', '.bat')) {
        $start.FileName = if ($env:ComSpec) { $env:ComSpec } else { 'cmd.exe' }
        $start.ArgumentList.Add('/d')
        $start.ArgumentList.Add('/s')
        $start.ArgumentList.Add('/c')
        $line = (ConvertTo-CmdArgument $FilePath) + ' ' + (($Arguments | ForEach-Object { ConvertTo-CmdArgument $_ }) -join ' ')
        $start.ArgumentList.Add($line.Trim())
    }
    else {
        $start.FileName = $FilePath
        foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    }
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    foreach ($key in $Environment.Keys) { $start.Environment[[string]$key] = [string]$Environment[$key] }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    if (-not $process.Start()) { throw "process-start-failed:$FilePath" }
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $completed = $process.WaitForExit($Timeout * 1000)
    if (-not $completed) {
        try { $process.Kill($true) } catch { }
        $process.WaitForExit()
    }
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    return [pscustomobject]@{
        ExitCode = if ($completed) { $process.ExitCode } else { -1 }
        TimedOut = -not $completed
        StandardOutput = Get-BoundedText $stdout
        StandardError = Get-BoundedText $stderr
    }
}

function Test-VersionMatch {
    param([string]$Declared, [string]$Observed)
    $descriptor = $Declared.Split('+', 2)[0]
    if ($descriptor -notmatch '^\d+(?:\.\d+){0,2}(?:-[0-9A-Za-z.-]+)?$') { return $false }
    $observedMatch = [regex]::Match($Observed, '\d+(?:\.\d+){1,2}(?:-[0-9A-Za-z.-]+)?')
    if (-not $observedMatch.Success) { return $false }
    $declaredCore = $descriptor.Split('-', 2)[0].Split('.')
    $observedCore = $observedMatch.Value.Split('-', 2)[0].Split('.')
    if ($observedCore.Length -lt $declaredCore.Length) { return $false }
    for ($index = 0; $index -lt $declaredCore.Length; $index++) {
        if ($declaredCore[$index] -ne $observedCore[$index]) { return $false }
    }
    return $true
}

try {
    $root = (Resolve-Path -LiteralPath $RepositoryRoot).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
    $package = (Resolve-Path -LiteralPath $PackageDirectory).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
    Assert-NoReparsePoint $root 'repository-root-reparse-point'
    Assert-NoReparsePoint $package 'package-directory-reparse-point'
    $boundary = $root + [IO.Path]::DirectorySeparatorChar
    if (-not [string]::Equals($package, $root, [StringComparison]::OrdinalIgnoreCase) -and
        -not $package.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'package-directory-outside-repository'
    }

    $manifests = New-Object System.Collections.Generic.List[string]
    $cursor = $package
    while ($true) {
        $candidate = Join-Path $cursor 'package.json'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            Assert-NoReparsePoint $candidate 'package-manifest-reparse-point'
            $manifests.Add($candidate)
        }
        if ([string]::Equals($cursor, $root, [StringComparison]::OrdinalIgnoreCase)) { break }
        $parent = Split-Path $cursor -Parent
        if ([string]::IsNullOrWhiteSpace($parent) -or [string]::Equals($parent, $cursor, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'repository-boundary-not-reached'
        }
        $cursor = $parent
    }

    $declarations = @($manifests | ForEach-Object {
        $json = Get-Content -LiteralPath $_ -Raw | ConvertFrom-Json
        if ($json.PSObject.Properties.Name -contains 'packageManager' -and -not [string]::IsNullOrWhiteSpace([string]$json.packageManager)) {
            [pscustomobject]@{ Path = $_; Value = [string]$json.packageManager }
        }
    })
    if ($declarations.Count -eq 0) {
        Write-Result 'blocked' '' '' @() 'package-manager-not-declared' @{ manifests = @($manifests) }
        exit 2
    }
    $distinct = @($declarations.Value | Sort-Object -Unique)
    if ($distinct.Count -ne 1) {
        Write-Result 'blocked' '' '' @() 'conflicting-package-manager-declarations' @{ declarations = @($declarations) }
        exit 2
    }

    $match = [regex]::Match($distinct[0], '^(?<manager>pnpm|npm|yarn|bun)@(?<version>\d+(?:\.\d+){0,2}(?:-[0-9A-Za-z.-]+)?(?:\+sha(?:224|512)\.[0-9a-fA-F]+)?)$')
    if (-not $match.Success) {
        Write-Result 'blocked' '' '' @() 'invalid-or-unsafe-package-manager-declaration' @{ declaration = $distinct[0] }
        exit 2
    }
    $manager = $match.Groups['manager'].Value
    $version = $match.Groups['version'].Value
    $declarationPath = [string]$declarations[0].Path
    $workspaceRoot = Split-Path $declarationPath -Parent
    Assert-NoReparsePoint $workspaceRoot 'workspace-root-reparse-point'

    $lockNames = @{ pnpm = 'pnpm-lock.yaml'; npm = 'package-lock.json'; yarn = 'yarn.lock'; bun = 'bun.lockb' }
    $lockPath = Join-Path $workspaceRoot $lockNames[$manager]
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
        Write-Result 'blocked' $manager $version @() 'declared-manager-lockfile-missing' @{ expectedLockfile = $lockPath; workspaceRoot = $workspaceRoot }
        exit 2
    }
    Assert-NoReparsePoint $lockPath 'lockfile-reparse-point'

    $unsafe = @()
    if ($env:COREPACK_ENABLE_STRICT -eq '0') { $unsafe += 'COREPACK_ENABLE_STRICT=0' }
    if ($env:COREPACK_ENABLE_PROJECT_SPEC -eq '0') { $unsafe += 'COREPACK_ENABLE_PROJECT_SPEC=0' }
    if ($env:COREPACK_ENABLE_UNSAFE_CUSTOM_URLS -eq '1') { $unsafe += 'COREPACK_ENABLE_UNSAFE_CUSTOM_URLS=1' }
    if ($null -ne $env:COREPACK_INTEGRITY_KEYS) { $unsafe += 'COREPACK_INTEGRITY_KEYS override' }
    if ($env:COREPACK_NPM_REGISTRY -and $env:COREPACK_NPM_REGISTRY.TrimEnd('/') -ne 'https://registry.npmjs.org') { $unsafe += 'COREPACK_NPM_REGISTRY override' }
    if ($unsafe.Count -gt 0) {
        Write-Result 'blocked' $manager $version @() 'unsafe-corepack-environment' @{ variables = $unsafe; lockfile = $lockPath }
        exit 2
    }

    $safeEnvironment = @{
        COREPACK_ENABLE_NETWORK = '0'
        COREPACK_DEFAULT_TO_LATEST = '0'
        COREPACK_ENABLE_AUTO_PIN = '0'
        COREPACK_ENABLE_DOWNLOAD_PROMPT = '0'
        COREPACK_ENABLE_STRICT = '1'
        COREPACK_ENABLE_PROJECT_SPEC = '1'
        COREPACK_ENABLE_UNSAFE_CUSTOM_URLS = '0'
        COREPACK_ENV_FILE = '0'
        CI = '1'
    }

    $node = Get-Application 'node'
    if ($null -eq $node) {
        Write-Result 'blocked' $manager $version @() 'node-unavailable' @{ lockfile = $lockPath; workspaceRoot = $workspaceRoot }
        exit 2
    }
    $nodeResult = Invoke-BoundedProcess $node.Source @('--version') $workspaceRoot $safeEnvironment $TimeoutSeconds
    if ($nodeResult.TimedOut -or $nodeResult.ExitCode -ne 0) {
        Write-Result 'blocked' $manager $version @() 'node-version-check-failed' @{ executable = $node.Source; timedOut = $nodeResult.TimedOut; diagnostic = $nodeResult.StandardError }
        exit 2
    }

    $direct = Get-Application $manager
    if ($null -ne $direct) {
        $directResult = Invoke-BoundedProcess $direct.Source @('--version') $workspaceRoot $safeEnvironment $TimeoutSeconds
        if ($directResult.TimedOut -or $directResult.ExitCode -ne 0) {
            Write-Result 'blocked' $manager $version @() 'direct-manager-version-check-failed' @{ executable = $direct.Source; timedOut = $directResult.TimedOut; diagnostic = $directResult.StandardError; lockfile = $lockPath }
            exit 2
        }
        if (-not (Test-VersionMatch $version $directResult.StandardOutput)) {
            Write-Result 'blocked' $manager $version @() 'direct-manager-version-mismatch' @{ executable = $direct.Source; observed = $directResult.StandardOutput; lockfile = $lockPath }
            exit 2
        }
        Write-Result 'ready' $manager $version @($manager) 'direct-manager-available' @{
            declaration = $distinct[0]; declarationPath = $declarationPath; workspaceRoot = $workspaceRoot; lockfile = $lockPath
            nodeVersion = $nodeResult.StandardOutput; managerVersion = $directResult.StandardOutput; executable = $direct.Source; networkAllowed = $false
        }
        exit 0
    }

    if ($manager -ne 'pnpm') {
        Write-Result 'blocked' $manager $version @() 'declared-manager-unavailable' @{ lockfile = $lockPath; workspaceRoot = $workspaceRoot }
        exit 2
    }
    $corepack = Get-Application 'corepack'
    if ($null -eq $corepack) {
        Write-Result 'blocked' $manager $version @() 'pnpm-and-corepack-unavailable' @{ lockfile = $lockPath; workspaceRoot = $workspaceRoot }
        exit 2
    }
    $corepackResult = Invoke-BoundedProcess $corepack.Source @('--version') $workspaceRoot $safeEnvironment $TimeoutSeconds
    if ($corepackResult.TimedOut -or $corepackResult.ExitCode -ne 0) {
        Write-Result 'blocked' $manager $version @() 'corepack-version-check-failed' @{ executable = $corepack.Source; timedOut = $corepackResult.TimedOut; diagnostic = $corepackResult.StandardError; lockfile = $lockPath }
        exit 2
    }
    $pnpmResult = Invoke-BoundedProcess $corepack.Source @('pnpm', '--version') $workspaceRoot $safeEnvironment $TimeoutSeconds
    if ($pnpmResult.TimedOut -or $pnpmResult.ExitCode -ne 0) {
        Write-Result 'blocked' $manager $version @() 'corepack-pnpm-unavailable' @{
            executable = $corepack.Source; corepackVersion = $corepackResult.StandardOutput; timedOut = $pnpmResult.TimedOut
            diagnostic = $pnpmResult.StandardError; lockfile = $lockPath; workspaceRoot = $workspaceRoot
            integrityVerificationBypassAllowed = $false; networkAllowed = $false
        }
        exit 2
    }
    if (-not (Test-VersionMatch $version $pnpmResult.StandardOutput)) {
        Write-Result 'blocked' $manager $version @() 'corepack-pnpm-version-mismatch' @{ executable = $corepack.Source; observed = $pnpmResult.StandardOutput; lockfile = $lockPath; workspaceRoot = $workspaceRoot }
        exit 2
    }
    Write-Result 'ready' $manager $version @('corepack', 'pnpm') 'corepack-pnpm-available' @{
        declaration = $distinct[0]; declarationPath = $declarationPath; workspaceRoot = $workspaceRoot; lockfile = $lockPath
        nodeVersion = $nodeResult.StandardOutput; corepackVersion = $corepackResult.StandardOutput; managerVersion = $pnpmResult.StandardOutput
        executable = $corepack.Source; integrityVerificationBypassAllowed = $false; networkAllowed = $false
    }
    exit 0
}
catch {
    Write-Result 'blocked' '' '' @() 'preflight-exception' @{ diagnostic = Get-BoundedText $_.Exception.Message }
    exit 2
}
