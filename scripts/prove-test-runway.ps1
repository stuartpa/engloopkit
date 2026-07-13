[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

$testProject = 'tests/EngLoopKit.Tests/EngLoopKit.Tests.csproj'
$boundaryTest = 'EngLoopKit.Tests.RunwayBoundaryTests.RunwayBoundaryTest'
$runwayFamily = 'EngLoopKit.Tests.RunwayBoundaryTests'
$tempFile = Join-Path $root 'tests/EngLoopKit.Tests/RunwayControlledFailure.g.cs'
$evidencePath = Join-Path $root '.engloop/scaffolds/SCAF001_test-runway.md'
$outputPath = Join-Path $root '.engloop/out/scaf001-test-runway.json'

New-Item -ItemType Directory (Split-Path $evidencePath -Parent) -Force | Out-Null
New-Item -ItemType Directory (Split-Path $outputPath -Parent) -Force | Out-Null

if (Test-Path $tempFile -PathType Leaf) {
    throw "Runway proof refuses to overwrite existing controlled-failure source: $tempFile"
}

$command = @('test', $testProject, '-c', 'Debug', '--nologo', '--logger', 'console;verbosity=detailed', '--filter', "FullyQualifiedName~$runwayFamily")

function Invoke-TestCommand {
    $capture = Join-Path $env:TEMP ('engloopkit-runway-' + [Guid]::NewGuid().ToString('N') + '.log')
    try {
        & dotnet @command *> $capture
        $exitCode = $LASTEXITCODE
        [pscustomobject]@{
            exitCode = $exitCode
            output = Get-Content $capture -Raw -Encoding utf8
        }
    }
    finally {
        Remove-Item $capture -Force -ErrorAction SilentlyContinue
    }
}

function Remove-ControlledFailure {
    if (-not (Test-Path $tempFile)) { return }

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Remove-Item $tempFile -Force
            return
        }
        catch {
            if ($attempt -eq 5) { throw }
            Start-Sleep -Milliseconds (100 * $attempt)
        }
    }
}

$observations = @()
try {
    $baseline = Invoke-TestCommand
    $observations += [pscustomobject]@{ phase = 'baseline-pass'; exitCode = $baseline.exitCode; boundaryObserved = $baseline.output.Contains($boundaryTest, [StringComparison]::Ordinal); controlledFailureObserved = $false }
    if ($baseline.exitCode -ne 0 -or -not $baseline.output.Contains($boundaryTest, [StringComparison]::Ordinal)) {
        throw 'Runway baseline did not build/discover/pass the selected real-boundary test.'
    }

    @'
using Xunit;

namespace EngLoopKit.Tests;

public sealed partial class RunwayBoundaryTests
{
    [Fact]
    public void ControlledFailure()
    {
        Assert.True(false, "controlled Stage 02 runway proof failure");
    }
}
'@ | Set-Content $tempFile -Encoding utf8NoBOM

    $intentional = Invoke-TestCommand
    $expectedFailure = 'EngLoopKit.Tests.RunwayBoundaryTests.ControlledFailure'
    $observations += [pscustomobject]@{ phase = 'controlled-failure'; exitCode = $intentional.exitCode; boundaryObserved = $intentional.output.Contains($boundaryTest, [StringComparison]::Ordinal); controlledFailureObserved = $intentional.output.Contains($expectedFailure, [StringComparison]::Ordinal) }
    if ($intentional.exitCode -eq 0 -or -not $intentional.output.Contains($expectedFailure, [StringComparison]::Ordinal)) {
        throw 'Runway intentional-failure proof did not produce the selected controlled failure.'
    }

    Remove-ControlledFailure
    $restored = Invoke-TestCommand
    $observations += [pscustomobject]@{ phase = 'restored-pass'; exitCode = $restored.exitCode; boundaryObserved = $restored.output.Contains($boundaryTest, [StringComparison]::Ordinal); controlledFailureObserved = $false }
    if ($restored.exitCode -ne 0 -or -not $restored.output.Contains($boundaryTest, [StringComparison]::Ordinal)) {
        throw 'Runway restoration did not rediscover and pass the selected real-boundary test.'
    }
}
finally {
    Remove-ControlledFailure
}

$report = [ordered]@{
    capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    sdk = (& dotnet --version)
    framework = 'xUnit 2.9.2 / Microsoft.NET.Test.Sdk 17.11.1'
    terseCommand = @('dotnet') + $command
    boundaryTest = $boundaryTest
    generatedDestination = 'tests/EngLoopKit.Loop.Generated/'
    sourceRevision = (git rev-parse HEAD 2>$null)
    observations = $observations
}

$report | ConvertTo-Json -Depth 12 | Set-Content $outputPath -Encoding utf8NoBOM
$sourceDigest = (Get-FileHash $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()

$markdown = @(
    '# SCAF001 Test Runway',
    '',
    "- Captured: $($report.capturedAtUtc)",
    "- SDK: $($report.sdk)",
    "- Framework: $($report.framework)",
    ('- Terse command: ``{0}``' -f ($report.terseCommand -join ' ')),
    ('- Boundary test: ``{0}``' -f $boundaryTest),
    ('- Generated destination: ``{0}``' -f $report.generatedDestination),
    ('- Evidence digest: ``{0}``' -f $sourceDigest),
    '',
    '## Observations'
) + ($observations | ForEach-Object { "- $($_.phase): exit=$($_.exitCode), boundary=$($_.boundaryObserved), controlledFailure=$($_.controlledFailureObserved)" })

[string]::Join("`n", $markdown) | Set-Content $evidencePath -Encoding utf8NoBOM
Write-Output "RUNWAY_PASS evidence=$outputPath digest=$sourceDigest"
