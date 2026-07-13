[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

$destination = Join-Path $root 'tests/EngLoopKit.Loop.Generated'
$temporary = Join-Path $root '.engloop/out/generated-suite-candidate'
$sekProject = Join-Path (Split-Path $root -Parent) 'SEK/src/Sek.Cli/Sek.Cli.csproj'

if (-not (Test-Path $sekProject -PathType Leaf)) {
    throw "Pinned sibling SEK source is missing: $sekProject"
}

if (Test-Path $temporary) { Remove-Item $temporary -Recurse -Force }
New-Item -ItemType Directory -Path $temporary -Force | Out-Null

dotnet run --project $sekProject -- generate ModelProgram --project $root --out $temporary --max 200
if ($LASTEXITCODE -ne 0) { throw 'SEK generation failed.' }

$generatedSource = Join-Path $temporary 'ModelProgramTests.cs'
$generatedProject = Join-Path $temporary 'ModelProgramTests.csproj'
if (-not (Test-Path $generatedSource -PathType Leaf) -or -not (Test-Path $generatedProject -PathType Leaf)) {
    throw 'SEK generation did not produce the expected standalone project.'
}

$sourceText = Get-Content $generatedSource -Raw -Encoding UTF8
if ($sourceText -match [regex]::Escape($root) -or $sourceText -match 'SEK_BINDING|DefaultBinding') {
    throw 'Generated source contains an absolute binding or environment fallback.'
}
if ($sourceText -notmatch 'BindingAssets' -or $sourceText -notmatch 'negative test\(s\)') {
    throw 'Generated source lacks the portable binding snapshot or negative conformance.'
}

dotnet test $generatedProject -c Debug --nologo
if ($LASTEXITCODE -ne 0) { throw 'Fresh generated suite failed before replacement.' }

$backup = Join-Path $root ('.engloop/out/generated-suite-retired-' + [Guid]::NewGuid().ToString('N'))
if (Test-Path $destination) { Move-Item $destination $backup }
try {
    Move-Item $temporary $destination
}
catch {
    if (Test-Path $backup) { Move-Item $backup $destination }
    throw
}

# Testhost or an IDE may retain a retired binding DLL briefly on Windows. The new suite
# is already atomically live; leave a uniquely named ignored retirement directory if it
# cannot be removed now. It is never a validation candidate and is retried on later runs.
if (Test-Path $backup) {
    try {
        Remove-Item $backup -Recurse -Force
    }
    catch {
        Write-Warning "Generated-suite retirement retained for later cleanup: $backup"
    }
}

$finalProject = Join-Path $destination 'ModelProgramTests.csproj'
$finalSource = Join-Path $destination 'ModelProgramTests.cs'
$sourceHash = (Get-FileHash $finalSource -Algorithm SHA256).Hash.ToLowerInvariant()
$bindingHashes = @(Get-ChildItem (Join-Path $destination 'BindingAssets') -File | Sort-Object Name | ForEach-Object {
    [ordered]@{ file = $_.Name; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})

$positiveMatch = [regex]::Match($sourceText, '//\s+(\d+) positive test\(s\)')
$negativeMatch = [regex]::Match($sourceText, '//\s+(\d+) negative test\(s\)')
if (-not $positiveMatch.Success -or -not $negativeMatch.Success) {
    throw 'Generated source does not declare positive/negative conformance counts.'
}

$report = [pscustomobject]@{
    capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    generatedProject = 'tests/EngLoopKit.Loop.Generated/ModelProgramTests.csproj'
    sourceSha256 = $sourceHash
    bindingAssets = $bindingHashes
    positiveTests = [int]$positiveMatch.Groups[1].Value
    negativeTests = [int]$negativeMatch.Groups[1].Value
    status = 'fresh'
}
$report | ConvertTo-Json -Depth 5 | Set-Content '.engloop/out/generated-suite-freshness.json' -Encoding utf8NoBOM
Write-Output 'GENERATED_SUITE_PASS'
