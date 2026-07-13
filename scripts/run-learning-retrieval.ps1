[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..'),
    [string]$CasesPath = '',
    [string]$ObservedResultsPath = '',
    [string]$OutputPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path $Root).Path
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($CasesPath)) {
    $CasesPath = Join-Path $repoRoot '.engloop/learnings/retrieval-cases.json'
}
if ([string]::IsNullOrWhiteSpace($ObservedResultsPath)) {
    $ObservedResultsPath = Join-Path $repoRoot '.engloop/learnings/retrieval-observed.json'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot '.engloop/out/learning-retrieval-results.json'
}

if (-not (Test-Path $CasesPath -PathType Leaf)) { throw "Missing retrieval cases file: $CasesPath" }
if (-not (Test-Path $ObservedResultsPath -PathType Leaf)) {
    throw "Missing observed retrieval results: $ObservedResultsPath. This script never copies expected IDs into actual results."
}

$cases = Get-Content $CasesPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$observed = Get-Content $ObservedResultsPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
if (-not $cases.ContainsKey('cases')) { throw 'retrieval-cases.json missing cases' }
if (-not $observed.ContainsKey('results')) { throw 'retrieval-observed.json missing results' }

$cardsRoot = Join-Path $repoRoot '.engloop/learnings/cards'
$cardIndex = @{}
foreach ($cardPath in Get-ChildItem $cardsRoot -File -Filter '*.md' | Sort-Object Name) {
    $slug = [IO.Path]::GetFileNameWithoutExtension($cardPath.Name)
    $text = Get-Content $cardPath.FullName -Raw -Encoding UTF8
    $sources = @([regex]::Matches($text, 'PM\d{3}/LEARN\d{3}') | ForEach-Object Value | Sort-Object -Unique)
    $cue = ([regex]::Match($text, '(?m)^- \*\*Recall cue:\*\*\s*(.+)$')).Groups[1].Value
    $cardIndex[$slug] = [ordered]@{ cue = $cue; sources = $sources; text = $text }
}

function NormalizeSet([object[]]$values) {
    return @($values | ForEach-Object { $_.ToString() } | Sort-Object -Unique)
}

$observedById = @{}
foreach ($result in $observed.results) {
    $observedById[$result.id] = $result
}

$results = @()
$failures = New-Object System.Collections.Generic.List[string]
foreach ($case in $cases.cases) {
    if (-not $observedById.ContainsKey($case.id)) {
        $failures.Add("missing-observed-case:$($case.id)") | Out-Null
        continue
    }

    $actual = $observedById[$case.id]
    $actualCards = NormalizeSet @($actual.actualCardIds)
    $actualSources = NormalizeSet @($actual.actualSourceIds)
    $expectedCards = NormalizeSet @($case.expectedCardIds)
    $expectedSources = NormalizeSet @($case.expectedSourceIds)

    # Observed cards must be current cards and sources must actually be cited by them.
    foreach ($card in $actualCards) {
        if (-not $cardIndex.ContainsKey($card)) {
            $failures.Add("unknown-observed-card:$($case.id):$card") | Out-Null
            continue
        }
        foreach ($source in $actualSources) {
            if ($cardIndex[$card].sources -notcontains $source) {
                $failures.Add("source-not-cited-by-observed-card:$($case.id):$card:$source") | Out-Null
            }
        }
    }

    if ($case.expectGap) {
        if ($actualCards.Count -ne 0 -or $actualSources.Count -ne 0 -or $actual.verdict -ne 'GAP') {
            $failures.Add("gap-case-not-reported:$($case.id)") | Out-Null
        }
    }
    else {
        if ($actual.verdict -ne 'PASS') { $failures.Add("case-not-pass:$($case.id)") | Out-Null }
        if (@(Compare-Object $expectedCards $actualCards).Count -ne 0) { $failures.Add("card-set-mismatch:$($case.id)") | Out-Null }
        if (@(Compare-Object $expectedSources $actualSources).Count -ne 0) { $failures.Add("source-set-mismatch:$($case.id)") | Out-Null }
    }

    $results += [ordered]@{
        id = $case.id
        expectedCardIds = $expectedCards
        expectedSourceIds = $expectedSources
        actualCardIds = $actualCards
        actualSourceIds = $actualSources
        verdict = $actual.verdict
    }
}

foreach ($id in $observedById.Keys) {
    if (@($cases.cases | Where-Object { $_.id -eq $id }).Count -eq 0) {
        $failures.Add("unexpected-observed-case:$id") | Out-Null
    }
}

$outputDir = Split-Path $OutputPath -Parent
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$payload = [ordered]@{
    capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    casesPath = [IO.Path]::GetRelativePath($repoRoot, $CasesPath)
    observedResultsPath = [IO.Path]::GetRelativePath($repoRoot, $ObservedResultsPath)
    results = $results
    failures = @($failures | Sort-Object -Unique)
    verdict = if ($failures.Count -eq 0) { 'PASS' } else { 'FAIL' }
    policy = 'Observed results are supplied independently; expected IDs are never copied into actual results.'
}
$payload | ConvertTo-Json -Depth 64 | Set-Content $OutputPath -Encoding utf8NoBOM
if ($payload.verdict -ne 'PASS') { throw "LEARNING_RETRIEVAL_FAIL results=$OutputPath" }
Write-Output "LEARNING_RETRIEVAL_PASS results=$OutputPath"
