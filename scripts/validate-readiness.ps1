[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

$coverageRoot = Join-Path $root '.engloop/out/readiness-coverage'
$coverageDoc = Join-Path $root '.engloop/coverage/COV003_ordered-engloop-v2-readiness.md'
$coverageStructured = Join-Path $root '.engloop/coverage/COV003_ordered-engloop-v2-readiness.json'
$coverageJson = Join-Path $root '.engloop/out/cov003-readiness.json'
New-Item -ItemType Directory (Split-Path $coverageDoc -Parent) -Force | Out-Null
New-Item -ItemType Directory (Split-Path $coverageJson -Parent) -Force | Out-Null
Remove-Item $coverageRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $coverageRoot -Force | Out-Null

function Invoke-Checked {
    param([string]$Name, [scriptblock]$Action)
    try {
        & $Action | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit $LASTEXITCODE" }
        return $true
    }
    catch {
        Write-Warning $_.Exception.Message
        return $false
    }
}

function Write-CoverletRunSettings {
        param(
                [string]$Path,
                [string]$MergeWith = ''
        )

        $merge = if ([string]::IsNullOrWhiteSpace($MergeWith)) {
                ''
        }
        else {
                "          <MergeWith>$([System.Security.SecurityElement]::Escape($MergeWith))</MergeWith>"
        }

        @"
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
    <DataCollectionRunSettings>
        <DataCollectors>
            <DataCollector friendlyName="XPlat Code Coverage">
                <Configuration>
                    <Format>cobertura,json</Format>
                    <UseSourceLink>false</UseSourceLink>
$merge
                </Configuration>
            </DataCollector>
        </DataCollectors>
    </DataCollectionRunSettings>
</RunSettings>
"@ | Set-Content $Path -Encoding utf8NoBOM
}

# Stage 07 functional evidence remains independent and also contributes measured Core
# coverage to the final whole-product report. Model-derived legal/rejection branches are
# authoritative behavior evidence and must not disappear from the coverage inventory.
$functionalCoverageRoot = Join-Path $coverageRoot 'functional'
$functionalSettings = Join-Path $coverageRoot 'functional.runsettings'
Write-CoverletRunSettings -Path $functionalSettings
$functionalPass = Invoke-Checked 'generated functional suite' {
    dotnet test tests/EngLoopKit.Loop.Generated/ModelProgramTests.csproj -c Debug --nologo `
        --settings $functionalSettings --collect:'XPlat Code Coverage' `
        --results-directory $functionalCoverageRoot
}
$functionalJson = Get-ChildItem $functionalCoverageRoot -Recurse -Filter coverage.json -ErrorAction SilentlyContinue |
    Select-Object -Last 1 -ExpandProperty FullName
if ($functionalPass -and -not $functionalJson) { throw 'generated functional coverage JSON report missing' }

# Stage 08 direct/property evidence plus real Cobertura output (same collector/version as SEK).
# The test process has two deterministic partitions: generic/core validation tests and
# overlay Git/tool transaction tests. Coverlet merges the two native JSON reports. This
# runs every direct test exactly once while avoiding a Windows testhost collector crash
# observed only when both partitions share one host process.
$directPass = Invoke-Checked 'direct test suite with coverage' {
    dotnet build EngLoopKit.slnx -c Debug --nologo
    if ($LASTEXITCODE -ne 0) { throw '.slnx build failed before direct coverage' }

    $coreCoverageRoot = Join-Path $coverageRoot 'core'
    $overlayCoverageRoot = Join-Path $coverageRoot 'overlay'
    $coreSettings = Join-Path $coverageRoot 'core.runsettings'
    $overlaySettings = Join-Path $coverageRoot 'overlay.runsettings'
    Write-CoverletRunSettings -Path $coreSettings -MergeWith $functionalJson

    $coreFilter = 'FullyQualifiedName!~OverlayArchiveTests&FullyQualifiedName!~OverlayCommandCoverageTests&FullyQualifiedName!~OverlayCommandFailureTests&FullyQualifiedName!~OverlayCommandPrivateTests&FullyQualifiedName!~OverlayCommandTests'
    dotnet test tests/EngLoopKit.Tests/EngLoopKit.Tests.csproj -c Debug --no-build --no-restore `
        --filter $coreFilter --settings $coreSettings --collect:'XPlat Code Coverage' `
        --results-directory $coreCoverageRoot --nologo
    if ($LASTEXITCODE -ne 0) { throw 'core direct coverage partition failed' }

    $coreJson = Get-ChildItem $coreCoverageRoot -Recurse -Filter coverage.json | Select-Object -Last 1 -ExpandProperty FullName
    if (-not $coreJson) { throw 'core direct coverage JSON report missing' }
    Write-CoverletRunSettings -Path $overlaySettings -MergeWith $coreJson

    $overlayFilter = 'FullyQualifiedName~OverlayArchiveTests|FullyQualifiedName~OverlayCommandCoverageTests|FullyQualifiedName~OverlayCommandFailureTests|FullyQualifiedName~OverlayCommandPrivateTests|FullyQualifiedName~OverlayCommandTests'
    dotnet test tests/EngLoopKit.Tests/EngLoopKit.Tests.csproj -c Debug --no-build --no-restore `
        --filter $overlayFilter --settings $overlaySettings --collect:'XPlat Code Coverage' `
        --results-directory $overlayCoverageRoot --nologo
    if ($LASTEXITCODE -ne 0) { throw 'overlay direct coverage partition failed' }
}

$architecturePass = Invoke-Checked 'root/config/command/agent validation' {
    dotnet run --project src/EngLoopKit.Tool/EngLoopKit.Tool.csproj -- validate root --root .
    if ($LASTEXITCODE -ne 0) { throw 'validate root failed' }
    dotnet run --project src/EngLoopKit.Tool/EngLoopKit.Tool.csproj -- validate config --root .
    if ($LASTEXITCODE -ne 0) { throw 'validate config failed' }
    dotnet run --project src/EngLoopKit.Tool/EngLoopKit.Tool.csproj -- validate commands --root .
    if ($LASTEXITCODE -ne 0) { throw 'validate commands failed' }
    dotnet run --project src/EngLoopKit.Tool/EngLoopKit.Tool.csproj -- validate agent-surfaces --root .
}

$reportPath = Get-ChildItem (Join-Path $coverageRoot 'overlay') -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1 -ExpandProperty FullName

$packageByModule = [ordered]@{
    'components.numbering'          = 'EngLoopKit.Components.Numbering'
    'components.statemachine'       = 'EngLoopKit.Components.StateMachine'
    'components.documentvalidation' = 'EngLoopKit.Components.DocumentValidation'
    'components.overlay'            = 'EngLoopKit.Components.Overlay'
    'core'                          = 'EngLoopKit.Core'
    'tool'                          = 'engloopkit'
}

$failures = New-Object System.Collections.Generic.List[string]
$rows = @()
$packages = @{}
if ($reportPath) {
    [xml]$coverage = Get-Content $reportPath
    foreach ($package in $coverage.coverage.packages.package) {
        $packages[$package.name] = $package
    }
}
else {
    $failures.Add('missing-cobertura-report') | Out-Null
}

foreach ($moduleId in $packageByModule.Keys) {
    $packageName = $packageByModule[$moduleId]
    $package = $packages[$packageName]
    $line = if ($package) { [math]::Round(([double]$package.'line-rate') * 100, 2) } else { 0.0 }
    $branch = if ($package) { [math]::Round(([double]$package.'branch-rate') * 100, 2) } else { 0.0 }
    $modulePass = $functionalPass -and $directPass -and $architecturePass -and $package -and $line -ge 95.0 -and $branch -ge 95.0
    if (-not $package) { $failures.Add("missing-coverage-module:$moduleId") | Out-Null }
    if ($line -lt 95.0) { $failures.Add("line-coverage-below-threshold:${moduleId}:$line") | Out-Null }
    if ($branch -lt 95.0) { $failures.Add("branch-coverage-below-threshold:${moduleId}:$branch") | Out-Null }
    if (-not $functionalPass) { $failures.Add("functional-validation-fail:$moduleId") | Out-Null }
    if (-not $directPass) { $failures.Add("direct-test-fail:$moduleId") | Out-Null }
    if (-not $architecturePass) { $failures.Add("architecture-validation-fail:$moduleId") | Out-Null }
    $rows += [pscustomobject]@{
        id = $moduleId
        coverageIdentity = $packageName
        line = $line
        branch = $branch
        functionalPass = $functionalPass
        directPass = $directPass
        architecturePass = $architecturePass
        pass = [bool]$modulePass
    }
}

$passed = $failures.Count -eq 0
$verdict = if ($passed) { 'PASS' } else { 'NOT READY' }
$captured = (Get-Date).ToUniversalTime().ToString('o')

$markdown = @(
    '# COV003 Ordered EngLoop v2 Readiness',
    '',
    "- Captured: $captured",
    "- Verdict: $verdict",
    "- Functional generated suite: $functionalPass",
    "- Direct/property suite: $directPass",
    "- Architecture/installation validators: $architecturePass",
    "- Cobertura report: $([IO.Path]::GetRelativePath($root, $reportPath ?? 'missing'))",
    '',
    '## Readiness inventory',
    '',
    '| Module | Coverage identity | Line | Branch | Functional | Direct | Architecture | PASS |',
    '|---|---|---:|---:|---|---|---|---|'
) + ($rows | ForEach-Object {
    "| $($_.id) | $($_.coverageIdentity) | $($_.line)% | $($_.branch)% | $($_.functionalPass) | $($_.directPass) | $($_.architecturePass) | $($_.pass) |"
})

if ($passed) {
    $markdown += @('', '## Readiness Gate verdict', '', '- [x] **PASS**')
}

if (-not $passed) {
    $markdown += @('', '## Blockers')
    $markdown += ($failures | Sort-Object -Unique | ForEach-Object { "- $_" })
}

[string]::Join("`n", $markdown) | Set-Content $coverageDoc -Encoding utf8NoBOM

$evidence = [pscustomobject]@{
    schemaVersion = '1.0'
    artifactType = 'whole-product-readiness'
    capturedAtUtc = $captured
    verdict = $verdict
    coberturaReport = if ($reportPath) { [IO.Path]::GetRelativePath($root, $reportPath) } else { $null }
    coberturaSha256 = if ($reportPath) { (Get-FileHash $reportPath -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
    generatedFunctionalPass = $functionalPass
    directSuitePass = $directPass
    architectureValidationPass = $architecturePass
    modules = $rows
    failures = @($failures | Sort-Object -Unique)
}
$evidence | ConvertTo-Json -Depth 12 | Set-Content $coverageJson -Encoding utf8NoBOM
$evidence | ConvertTo-Json -Depth 12 | Set-Content $coverageStructured -Encoding utf8NoBOM

if (-not $passed) {
    throw "READINESS_NOT_READY evidence=$coverageJson"
}

dotnet run --project src/EngLoopKit.Tool/EngLoopKit.Tool.csproj -- readiness emit --root . --evidence '.engloop/coverage/COV003_ordered-engloop-v2-readiness.json' --verdict pass
if ($LASTEXITCODE -ne 0) { throw 'Structured current readiness emission failed.' }

Write-Output "READINESS_PASS evidence=$coverageJson"
