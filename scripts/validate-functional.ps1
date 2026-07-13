[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

New-Item -ItemType Directory '.engloop/out' -Force | Out-Null
New-Item -ItemType Directory '.engloop/coverage' -Force | Out-Null

dotnet test tests/EngLoopKit.Loop.Generated/ModelProgramTests.csproj -c Debug
if ($LASTEXITCODE -ne 0) { throw 'Generated functional validation failed.' }

$coverageMd = @(
    '# COV002 Ordered EngLoop v2 Functional'
    ''
    "- Captured: $((Get-Date).ToUniversalTime().ToString('o'))"
    '- Scope: generated-suite-only functional validation'
    '- Verdict: functional-pass'
)
[string]::Join("`n", $coverageMd) | Set-Content '.engloop/coverage/COV002_ordered-engloop-v2-functional.md' -Encoding utf8NoBOM

$report = [pscustomobject]@{
    capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    verdict = 'functional-pass'
    suite = 'tests/EngLoopKit.Loop.Generated/ModelProgramTests.csproj'
}
$report | ConvertTo-Json -Depth 5 | Set-Content '.engloop/out/cov002-functional.json' -Encoding utf8NoBOM
Write-Output 'FUNCTIONAL_PASS'
