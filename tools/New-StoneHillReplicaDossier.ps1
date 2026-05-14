param(
    [string]$IdentityPath = ".\stonehill-identity-matrix.json",
    [string]$ReadinessPath = ".\stonehill-replica-readiness.json",
    [string]$ValidationPath = ".\stonehill-validation-playbook.json",
    [string]$FocusedPairReportPath = ".\stonehill-focused-ram-pair-report.json",
    [string]$SourceLayoutsPath = ".\stonehill-source-window-layouts.json",
    [string]$OutJsonPath = ".\stonehill-replica-dossier.json",
    [string]$OutMarkdownPath = ".\stonehill-replica-dossier.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if (-not $Object.Contains($Name) -or $null -eq $Object[$Name]) { return $Default }
        return $Object[$Name]
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return $Default }
    return $property.Value
}

function Get-ArrayField($Object, [string]$Name) {
    $value = Get-Field $Object $Name @()
    return @($value)
}

function Read-JsonIfPresent([string]$Path) {
    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $resolved) { return $null }
    return Get-Content -Raw -LiteralPath $resolved.Path | ConvertFrom-Json
}

function Format-Indexes($Indexes) {
    $items = @($Indexes | Where-Object { $null -ne $_ } | ForEach-Object {
        $text = [string]$_
        if ($text -match "^L(?<n>\d+)$") { [int]$Matches.n }
        elseif ($text -match "^\d+$") { [int]$text }
    } | Sort-Object -Unique)
    if ($items.Count -eq 0) { return "none" }
    return ($items | ForEach-Object { "L$_" }) -join ", "
}

function Clean-Cell([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return "none" }
    return $Text.Replace("|", "/")
}

function Get-PairCandidateText($Pair) {
    $candidateIndexes = @(Get-ArrayField $Pair "candidateIndexes")
    if ($candidateIndexes.Count -eq 0) {
        $candidateIndexes = @(Get-ArrayField $Pair "candidateMobys")
    }
    return Format-Indexes $candidateIndexes
}

$identity = Read-JsonIfPresent $IdentityPath
$readiness = Read-JsonIfPresent $ReadinessPath
$validation = Read-JsonIfPresent $ValidationPath
$pairReport = Read-JsonIfPresent $FocusedPairReportPath
$sourceLayouts = Read-JsonIfPresent $SourceLayoutsPath

$status = Get-Field $identity "status" $null
$coverage = Get-Field $readiness "coverage" $null
$targetLookup = @(Get-ArrayField $identity "targetLookup")
$mobyLookup = @(Get-ArrayField $identity "mobyLookup")
$patchTests = @(Get-ArrayField $validation "immediatePatchTests" | Sort-Object { [int](Get-Field $_ "priority" 999) })
$pairs = @(Get-ArrayField $pairReport "pairs")
$missingPairs = @($pairs | Where-Object { [string](Get-Field $_ "status" "") -ne "compared" })
$completedPairs = @($pairs | Where-Object { [string](Get-Field $_ "status" "") -eq "compared" })
$topSourceLeads = @(Get-ArrayField $sourceLayouts "topActionableSourceLeads" | Select-Object -First 12)

$strongIdentityMobys = [int](Get-Field $coverage "strongIdentityMobys" (Get-Field $status "strongIdentityMobys" 0))
$missingFocusedPairs = [int](Get-Field $coverage "missingFocusedPairs" $missingPairs.Count)
$grade = [string](Get-Field $readiness "readinessGrade" (Get-Field $status "grade" "unknown"))

$verdict = if ($grade -eq "replica-candidate") {
    "Replica candidate"
}
elseif ($grade -eq "editor-workbench-ready") {
    "Workbench ready, not a replica yet"
}
else {
    "Research workbench, not a replica yet"
}

$publicFacts = @(
    [ordered]@{
        fact = "Stone Hill contains 200 treasure, four dragons, one dragon egg, rams, shepherds, a blue thief, sheep fodder, a key, and a locked chest."
        source = "https://spyrowiki.com/wiki/Stone_Hill"
    },
    [ordered]@{
        fact = "The named dragons are Lindar, Gildas, Astor, and Gavin; Gavin is in the dry well, Astor is near Return Home, Lindar is in the cave, and Gildas is by the tower/high-field route."
        source = "https://spyro.fandom.com/wiki/Stone_Hill"
    },
    [ordered]@{
        fact = "The hidden key is reached from the secret beach/cave route behind the castle side of the level."
        source = "https://strategywiki.org/wiki/Spyro_the_Dragon/Stone_Hill"
    },
    [ordered]@{
        fact = "A proven way to identify an unknown object is to move one coordinate sharply, such as raising Z by about 1000, then watch which visible object moves."
        source = "https://lxshades.github.io/spyroedit/spyroedit_guide.html"
    }
)

$targetSummary = @($targetLookup | ForEach-Object {
    [ordered]@{
        target = [string](Get-Field $_ "target" "")
        certainty = [string](Get-Field $_ "certainty" "")
        candidateMobys = [string](Get-Field $_ "candidateMobys" "")
        relatedMobys = [string](Get-Field $_ "relatedMobys" "")
        sourceLeads = [string](Get-Field $_ "sourceLeads" "")
        nextProof = [string](Get-Field $_ "nextProof" "")
    }
})

$movementBuckets = [ordered]@{
    moveNow = @($targetSummary | Where-Object { $_.target -like "*treasure*" })
    patchTestFirst = @($targetSummary | Where-Object {
        $_.certainty -like "*patch-testable*" -and $_.target -notlike "*treasure*"
    })
    captureFirst = @($targetSummary | Where-Object {
        $_.certainty -eq "blocked" -or $_.sourceLeads -eq "none"
    })
}

$patchQueue = @($patchTests | Select-Object -First 8 | ForEach-Object {
    [ordered]@{
        priority = [int](Get-Field $_ "priority" 0)
        moby = [string](Get-Field $_ "moby" "")
        target = [string](Get-Field $_ "target" "")
        sourceLead = [string](Get-Field $_ "sourceLead" "")
        successSignal = [string](Get-Field $_ "successSignal" "")
    }
})

$sourceLeadQueue = @($topSourceLeads | ForEach-Object {
    [ordered]@{
        score = [int](Get-Field $_ "score" 0)
        moby = "L$([int](Get-Field $_ "index" -1))"
        typeHex = [string](Get-Field $_ "typeHex" "")
        kindBucket = [string](Get-Field $_ "kindBucket" "")
        wadRelativeWindow = [string](Get-Field $_ "wadRelativeWindow" "")
        axisSet = [string](Get-Field $_ "axisSet" "")
        values = (@(Get-ArrayField $_ "values") -join "; ")
    }
})

$captureQueue = @($missingPairs | ForEach-Object {
    [ordered]@{
        pair = [string](Get-Field $_ "id" "")
        target = [string](Get-Field $_ "target" "")
        location = [string](Get-Field $_ "location" "")
        candidateMobys = Get-PairCandidateText $_
        expectedSignal = [string](Get-Field $_ "expectedSignal" "")
        resolves = [string](Get-Field $_ "resolves" "")
    }
})

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Stone Hill replica dossier: a single editor-facing summary of public layout facts, current moby identity evidence, source patch tests, and missing proof."
    verdict = $verdict
    grade = $grade
    conclusion = "The editor can show and move the current Stone Hill map proxy. Type 0x20 treasure is the only confirmed gameplay class; dragons, enemies, key, and chest still need focused proof before this can be called a replica."
    coverage = [ordered]@{
        decodedRuntimeMobys = [int](Get-Field $coverage "decodedRuntimeMobys" (Get-Field $status "decodedRuntimeMobys" 0))
        placementRuntimeMobys = [int](Get-Field $coverage "placementRuntimeMobys" 0)
        strongIdentityMobys = $strongIdentityMobys
        scaledSourceLeadMobys = [int](Get-Field $coverage "scaledSourceLeadMobys" 0)
        completedFocusedPairs = $completedPairs.Count
        missingFocusedPairs = $missingFocusedPairs
        targetRows = $targetSummary.Count
    }
    publicFacts = $publicFacts
    targetSummary = $targetSummary
    movementBuckets = $movementBuckets
    patchQueue = $patchQueue
    sourceLeadQueue = $sourceLeadQueue
    captureQueue = $captureQueue
    mobyLookup = $mobyLookup
    recommendedNextStep = "Patch-test L186 first because it is the collected-gem sample, then L109 for the stronger treasure source-window family and L87 for high-field enemy/thief behavior."
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$result | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# Stone Hill Replica Dossier")
[void]$md.Add("")
[void]$md.Add("Generated: $($result.generatedAt)")
[void]$md.Add("")
[void]$md.Add("Verdict: **$($result.verdict)**")
[void]$md.Add("")
[void]$md.Add($result.conclusion)
[void]$md.Add("")
[void]$md.Add("## Coverage")
[void]$md.Add("- Runtime mobys decoded: $($result.coverage.decodedRuntimeMobys)")
[void]$md.Add("- Placement-like runtime mobys: $($result.coverage.placementRuntimeMobys)")
[void]$md.Add("- Strong identity mobys: $($result.coverage.strongIdentityMobys)")
[void]$md.Add("- Scaled source-window lead mobys: $($result.coverage.scaledSourceLeadMobys)")
[void]$md.Add("- Focused RAM pairs: $($result.coverage.completedFocusedPairs) complete, $($result.coverage.missingFocusedPairs) missing")
[void]$md.Add("")
[void]$md.Add("## Public Layout Facts")
foreach ($fact in $publicFacts) {
    [void]$md.Add("- $($fact.fact) Source: $($fact.source)")
}
[void]$md.Add("")
[void]$md.Add("## Target To Moby Lookup")
[void]$md.Add("")
[void]$md.Add("| Target | Certainty | Candidate mobys | Source leads | Next proof |")
[void]$md.Add("| --- | --- | --- | --- | --- |")
foreach ($target in $targetSummary) {
    [void]$md.Add("| $(Clean-Cell $target.target) | $(Clean-Cell $target.certainty) | $(Clean-Cell $target.candidateMobys) | $(Clean-Cell $target.sourceLeads) | $(Clean-Cell $target.nextProof) |")
}
[void]$md.Add("")
[void]$md.Add("## Editor Movement Buckets")
[void]$md.Add("- Move now: $((@($movementBuckets.moveNow | ForEach-Object { $_.candidateMobys }) | Where-Object { $_ }) -join '; ') for confirmed treasure-class layout work.")
$patchTestBucketText = (@($movementBuckets.patchTestFirst | ForEach-Object { "$($_.target) -> $($_.candidateMobys)" }) | Where-Object { $_ }) -join '; '
$captureBucketText = (@($movementBuckets.captureFirst | ForEach-Object { "$($_.target) -> $($_.candidateMobys)" }) | Where-Object { $_ }) -join '; '
if ([string]::IsNullOrWhiteSpace($patchTestBucketText)) { $patchTestBucketText = "none" }
if ([string]::IsNullOrWhiteSpace($captureBucketText)) { $captureBucketText = "none" }
[void]$md.Add("- Patch-test first: $patchTestBucketText.")
[void]$md.Add("- Capture first: $captureBucketText.")
[void]$md.Add("")
[void]$md.Add("## Immediate Patch Queue")
[void]$md.Add("")
[void]$md.Add("| Priority | Moby | Target | Source lead | Success signal |")
[void]$md.Add("| --- | --- | --- | --- | --- |")
foreach ($test in $patchQueue) {
    [void]$md.Add("| $($test.priority) | $($test.moby) | $(Clean-Cell $test.target) | $(Clean-Cell $test.sourceLead) | $(Clean-Cell $test.successSignal) |")
}
[void]$md.Add("")
[void]$md.Add("## Top Source Windows")
[void]$md.Add("")
[void]$md.Add("| Score | Moby | Type | Kind | WAD window | Axes | Values |")
[void]$md.Add("| --- | --- | --- | --- | --- | --- | --- |")
foreach ($lead in $sourceLeadQueue) {
    [void]$md.Add("| $($lead.score) | $($lead.moby) | ``$($lead.typeHex)`` | $(Clean-Cell $lead.kindBucket) | $($lead.wadRelativeWindow) | $($lead.axisSet) | $(Clean-Cell $lead.values) |")
}
[void]$md.Add("")
[void]$md.Add("## Missing Proof Captures")
[void]$md.Add("")
[void]$md.Add("| Pair | Target | Location | Candidate mobys | Resolves |")
[void]$md.Add("| --- | --- | --- | --- | --- |")
foreach ($pair in $captureQueue) {
    [void]$md.Add("| $(Clean-Cell $pair.pair) | $(Clean-Cell $pair.target) | $(Clean-Cell $pair.location) | $(Clean-Cell $pair.candidateMobys) | $(Clean-Cell $pair.resolves) |")
}
[void]$md.Add("")
[void]$md.Add("## Recommended Next Step")
[void]$md.Add($result.recommendedNextStep)

$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
$md | Set-Content -LiteralPath $resolvedMarkdown -Encoding UTF8

Write-Host "Wrote Stone Hill replica dossier JSON to $resolvedJson"
Write-Host "Wrote Stone Hill replica dossier Markdown to $resolvedMarkdown"
Write-Host "Verdict: $($result.verdict); targets: $($targetSummary.Count); patch tests: $($patchQueue.Count); missing captures: $($captureQueue.Count)"
