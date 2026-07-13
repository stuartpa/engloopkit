[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

$coverageRoot = Join-Path $root '.engloop/out/readiness-coverage'
$coverageDoc = Join-Path $root '.engloop/coverage/COV003_ordered-engloop-v2-readiness.md'
$coverageJson = Join-Path $root '.engloop/out/cov003-readiness.json'
New-Item -ItemType Directory (Split-Path $coverageDoc -Parent) -Force | Out-Null
New-Item -ItemType Directory (Split-Path $coverageJson -Parent) -Force | Out-Null
Remove-Item $coverageRoot -Recurse -Force -ErrorAction SilentlyContinue

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

# Stage 07 functional evidence remains independent: only freshly generated tests run here.
$functionalPass = Invoke-Checked 'generated functional suite' {
    dotnet test tests/EngLoopKit.Loop.Generated/ModelProgramTests.csproj -c Debug --nologo
}

# Stage 08 direct/property evidence plus real Cobertura output (same collector/version as SEK).
$directPass = Invoke-Checked 'direct test suite with coverage' {
    dotnet test tests/EngLoopKit.Tests/EngLoopKit.Tests.csproj -c Debug `
        --collect:'XPlat Code Coverage' --results-directory $coverageRoot --nologo
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

$reportPath = Get-ChildItem $coverageRoot -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1 -ExpandProperty FullName

$packageByModule = [ordered]@{
    'components.numbering'          = 'EngLoopKit.Components.Numbering'
    'components.statemachine'       = 'EngLoopKit.Components.StateMachine'
    'components.documentvalidation' = 'EngLoopKit.Components.DocumentValidation'
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

if (-not $passed) {
    $markdown += @('', '## Blockers')
    $markdown += ($failures | Sort-Object -Unique | ForEach-Object { "- $_" })
}

[string]::Join("`n", $markdown) | Set-Content $coverageDoc -Encoding utf8NoBOM

$evidence = [pscustomobject]@{
    capturedAtUtc = $captured
    verdict = $verdict
    coberturaReport = if ($reportPath) { [IO.Path]::GetRelativePath($root, $reportPath) } else { $null }
    generatedFunctionalPass = $functionalPass
    directSuitePass = $directPass
    architectureValidationPass = $architecturePass
    modules = $rows
    failures = @($failures | Sort-Object -Unique)
}
$evidence | ConvertTo-Json -Depth 12 | Set-Content $coverageJson -Encoding utf8NoBOM

if (-not $passed) {
    throw "READINESS_NOT_READY evidence=$coverageJson"
}

Write-Output "READINESS_PASS evidence=$coverageJson"
