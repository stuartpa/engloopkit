[CmdletBinding()]
param(
    [switch]$SkipCanary,
    [switch]$Release
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repositoryRoot

$projectBuildOrder = @(
    'components/EngLoopKit.Components.StateMachine/EngLoopKit.Components.StateMachine.csproj'
    'components/EngLoopKit.Components.Numbering/EngLoopKit.Components.Numbering.csproj'
    'components/EngLoopKit.Components.DocumentValidation/EngLoopKit.Components.DocumentValidation.csproj'
    'src/EngLoopKit.Core/EngLoopKit.Core.csproj'
    'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj'
    'model/EngLoopKit.Model/EngLoopKit.Model.csproj'
    'tests/EngLoopKit.Loop.Generated/ModelProgramTests.csproj'
    'tests/EngLoopKit.Tests/EngLoopKit.Tests.csproj'
)

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
}

Invoke-Step -Name 'dotnet restore/build (project graph)' -Action {
    foreach ($project in $projectBuildOrder) {
        dotnet build $project -c Debug
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for $project"
        }
    }
}

Invoke-Step -Name 'dotnet test (project graph)' -Action {
    dotnet test 'tests/EngLoopKit.Loop.Generated/ModelProgramTests.csproj' -c Debug --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Generated test project failed.' }

    dotnet test 'tests/EngLoopKit.Tests/EngLoopKit.Tests.csproj' -c Debug --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Core test project failed.' }
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
    Write-Warning 'Release mode requested: additional release packaging gates are not yet wired in this script.'
}

Write-Host 'VALIDATE_V2_PASS' -ForegroundColor Green
