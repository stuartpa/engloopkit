[CmdletBinding()]
param(
    [switch]$SkipCanary,
    [switch]$Release
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repositoryRoot

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
}

Invoke-Step -Name 'dotnet build (.slnx graph)' -Action {
    dotnet build 'EngLoopKit.slnx' -c Debug
    if ($LASTEXITCODE -ne 0) { throw 'EngLoopKit.slnx build failed.' }
}

Invoke-Step -Name 'dotnet test (.slnx graph)' -Action {
    dotnet test 'EngLoopKit.slnx' -c Debug --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'EngLoopKit.slnx test failed.' }
}

Invoke-Step -Name 'tool validate root/config/commands/agent-surfaces' -Action {
    dotnet run --project 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj' -- validate root --root .
    if ($LASTEXITCODE -ne 0) { throw 'validate root failed.' }

    dotnet run --project 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj' -- validate config --root .
    if ($LASTEXITCODE -ne 0) { throw 'validate config failed.' }

    dotnet run --project 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj' -- validate commands --root .
    if ($LASTEXITCODE -ne 0) { throw 'validate commands failed.' }

    dotnet run --project 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj' -- validate agent-surfaces --root .
    if ($LASTEXITCODE -ne 0) { throw 'validate agent-surfaces failed.' }
}

if (-not $SkipCanary) {
    Invoke-Step -Name 'Spec Kit preservation canary' -Action {
        & "$repositoryRoot\scripts\test-spec-kit-agent-preservation.ps1" -SpecKitVersion '0.12.4' -VsCodeVersion '1.129.0-insider' -VsCodeCommit '29d19ddd1af725baf537b6b328843bcdc2d29ba1'
        if ($LASTEXITCODE -ne 0) { throw 'Spec Kit preservation canary failed.' }
    }
}

if ($Release) {
    Invoke-Step -Name 'whole-product readiness' -Action {
        & "$repositoryRoot\scripts\validate-readiness.ps1"
        if ($LASTEXITCODE -ne 0) { throw 'Readiness gate failed.' }
    }

    Invoke-Step -Name 'immutable release packaging' -Action {
        & "$repositoryRoot\scripts\validate-package.ps1" -Version '1.15.3'
        if ($LASTEXITCODE -ne 0) { throw 'Package gate failed.' }
    }
}

Write-Host 'VALIDATE_V2_PASS' -ForegroundColor Green
