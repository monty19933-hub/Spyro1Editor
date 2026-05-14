param(
    [string]$CatalogPath = ".\stonehill-moby-catalog.json",
    [string]$SourceLinksPath = ".\stonehill-moby-source-links.json",
    [string]$ReplicaPlanPath = ".\stonehill-replica-map-plan.json",
    [string]$TargetBoardPath = ".\stonehill-replica-target-board.json",
    [string]$FocusedPairReportPath = ".\stonehill-focused-ram-pair-report.json",
    [string]$OutJsonPath = ".\stonehill-replica-readiness.json",
    [string]$OutMarkdownPath = ".\stonehill-replica-readiness.md"
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
    $items = @($Indexes | Where-Object { $null -ne $_ } | ForEach-Object { [int]$_ } | Sort-Object -Unique)
    if ($items.Count -eq 0) { return "none" }
    return ($items | ForEach-Object { "L$_" }) -join ", "
}

function Get-TargetReadiness($Target) {
    $name = [string](Get-Field $Target "targetName" "")
    $status = [string](Get-Field $Target "status" "")
    $confidence = [string](Get-Field $Target "confidence" "")
    $candidates = @(Get-ArrayField $Target "candidateIndexes")
    $sourceLeads = [string](Get-Field $Target "sourceLeads" "none")
    $relatedSourceLeads = [string](Get-Field $Target "relatedSourceLeads" "none")
    $sourceLeadSummary = $sourceLeads
    if ($sourceLeadSummary -eq "none" -and $relatedSourceLeads -ne "none") {
        $sourceLeadSummary = "related: $relatedSourceLeads"
    }

    $grade = "blocked"
    if ($name -like "*treasure*" -and $status -like "*usable*") {
        $grade = "usable-class"
    }
    elseif ($candidates.Count -gt 0 -and ($sourceLeads -ne "none" -or $relatedSourceLeads -ne "none")) {
        $grade = "patch-testable-hypothesis"
    }
    elseif ($candidates.Count -gt 0) {
        $grade = "route-hypothesis"
    }

    return [ordered]@{
        targetName = $name
        grade = $grade
        status = $status
        confidence = $confidence
        candidateIndexes = @($candidates | ForEach-Object { [int]$_ } | Sort-Object -Unique)
        relatedRouteIndexes = @(Get-ArrayField $Target "relatedRouteIndexes" | ForEach-Object { [int]$_ } | Sort-Object -Unique)
        sourceLeads = $sourceLeads
        relatedSourceLeads = $relatedSourceLeads
        sourceLeadSummary = $sourceLeadSummary
        nextValidation = [string](Get-Field $Target "validationAction" "")
        editorAction = [string](Get-Field $Target "editorAction" "")
    }
}

$catalog = Read-JsonIfPresent $CatalogPath
$sourceLinks = Read-JsonIfPresent $SourceLinksPath
$replicaPlan = Read-JsonIfPresent $ReplicaPlanPath
$targetBoard = Read-JsonIfPresent $TargetBoardPath
$pairReport = Read-JsonIfPresent $FocusedPairReportPath

$catalogMobys = @(Get-ArrayField $catalog "mobys")
$catalogTypes = @(Get-ArrayField $catalog "typeSummary")
$targetRows = @(Get-ArrayField $targetBoard "targets")
$locationRows = @(Get-ArrayField $targetBoard "locations")
$sourceSummary = Get-Field $sourceLinks "summary" $null
$planCoverage = Get-Field $replicaPlan "coverage" $null
$pairRows = @(Get-ArrayField $pairReport "pairs")
if ($pairRows.Count -eq 0) {
    $pairRows = @(Get-ArrayField $pairReport "pairStatus")
}

$targetReadiness = @($targetRows | ForEach-Object { Get-TargetReadiness $_ })
$usableTargets = @($targetReadiness | Where-Object { [string](Get-Field $_ "grade" "") -eq "usable-class" })
$patchTestableTargets = @($targetReadiness | Where-Object { [string](Get-Field $_ "grade" "") -eq "patch-testable-hypothesis" })
$routeHypothesisTargets = @($targetReadiness | Where-Object { [string](Get-Field $_ "grade" "") -eq "route-hypothesis" })
$blockedTargets = @($targetReadiness | Where-Object { [string](Get-Field $_ "grade" "") -eq "blocked" })

$completedPairs = @($pairRows | Where-Object {
    $status = [string](Get-Field $_ "status" "")
    $status -in @("compared", "complete", "completed")
})
$missingPairs = @($pairRows | Where-Object {
    $status = [string](Get-Field $_ "status" "")
    $status -eq "missing" -or $status -eq ""
})

$strongIdentityCount = @($catalogMobys | Where-Object { [string](Get-Field $_ "confidence" "") -eq "strong" }).Count
$scaledLeadCount = 0
if ($null -ne $sourceSummary) {
    $scaledLeadCount = [int](Get-Field $sourceSummary "scaledInt16WindowMobys" 0)
}
elseif ($null -ne $planCoverage) {
    $scaledLeadCount = [int](Get-Field $planCoverage "mobysWithScaledSourceLeads" 0)
}

$readinessGrade = "not-a-replica-yet"
if ($usableTargets.Count -ge 1 -and $patchTestableTargets.Count -ge 3) {
    $readinessGrade = "editor-workbench-ready"
}
if ($blockedTargets.Count -eq 0 -and $missingPairs.Count -eq 0 -and $strongIdentityCount -ge 12) {
    $readinessGrade = "replica-candidate"
}

$blockers = New-Object System.Collections.Generic.List[string]
if ($missingPairs.Count -gt 0) {
    [void]$blockers.Add("$($missingPairs.Count) focused RAM pair(s) are still missing, so named dragons/enemies/key/chest cannot be promoted beyond hypotheses.")
}
if ($strongIdentityCount -lt 12) {
    [void]$blockers.Add("Only $strongIdentityCount runtime moby marker(s) have strong identity evidence; individual dragons/enemies/items are not confirmed.")
}
if ($scaledLeadCount -lt 20) {
    [void]$blockers.Add("$scaledLeadCount moby marker(s) have scaled-int16 source-window leads; these still need disposable-BIN patch tests.")
}
if ($blockedTargets.Count -gt 0) {
    [void]$blockers.Add("Blocked target rows: $((@($blockedTargets | ForEach-Object { [string](Get-Field $_ 'targetName' '') }) | Where-Object { $_ }) -join ', ').")
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Stone Hill replica readiness report for deciding whether the editor can be treated as a usable Stone Hill map and moby identity workbench."
    readinessGrade = $readinessGrade
    readinessMeaning = switch ($readinessGrade) {
        "replica-candidate" { "Enough identities and samples are present to start treating the project as a replica candidate." }
        "editor-workbench-ready" { "The editor can show Stone Hill, move runtime mobys, and guide patch tests, but it is not a verified replica yet." }
        default { "The current data is still a research workbench, not a finished Stone Hill replica." }
    }
    coverage = [ordered]@{
        decodedRuntimeMobys = [int](Get-Field $catalog "decodedMobys" (Get-Field $planCoverage "decodedRuntimeMobys" $catalogMobys.Count))
        placementRuntimeMobys = [int](Get-Field $catalog "placementMobys" (Get-Field $planCoverage "placementRuntimeMobys" 0))
        strongIdentityMobys = $strongIdentityCount
        scaledSourceLeadMobys = $scaledLeadCount
        targetRows = $targetRows.Count
        usableClassTargets = $usableTargets.Count
        patchTestableHypothesisTargets = $patchTestableTargets.Count
        routeHypothesisTargets = $routeHypothesisTargets.Count
        blockedTargets = $blockedTargets.Count
        completedFocusedPairs = $completedPairs.Count
        missingFocusedPairs = $missingPairs.Count
        routeLocations = $locationRows.Count
    }
    typeSummary = $catalogTypes
    targetReadiness = $targetReadiness
    missingFocusedPairs = @($missingPairs | ForEach-Object {
        $candidates = @(Get-ArrayField $_ "candidateMobys")
        if ($candidates.Count -eq 0) {
            $candidates = @(Get-ArrayField $_ "candidateIndexes")
        }
        [ordered]@{
            pair = [string](Get-Field $_ "id" (Get-Field $_ "pair" ""))
            target = [string](Get-Field $_ "target" "")
            location = [string](Get-Field $_ "location" "")
            candidateMobys = @($candidates)
            signal = [string](Get-Field $_ "expectedSignal" (Get-Field $_ "signal" ""))
        }
    })
    blockers = @($blockers.ToArray())
    nextActions = @(
        "Use Load Stone Hill Workbench to inspect and move the current runtime moby map.",
        "Patch-test L109 or L87 with Queue SH Leads on a disposable BIN; both have high-score source-window leads.",
        "Capture Astor/Gavin/Lindar/Gildas before-after RAM pairs before promoting dragon names.",
        "Capture blue-thief, hidden-key, locked-chest, ram, shepherd, and sheep/fodder pairs to split the remaining route buckets."
    )
    publicLayoutSources = @(
        "https://spyrowiki.com/wiki/Stone_Hill",
        "https://strategywiki.org/wiki/Spyro_the_Dragon/Stone_Hill",
        "https://www.gamepur.com/guides/spyro-reignited-stone-hill-key-location",
        "https://www.gamespew.com/2018/11/how-to-open-locked-chests-in-spyro-the-dragon/"
    )
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$result | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# Stone Hill Replica Readiness")
[void]$md.Add("")
[void]$md.Add("Generated: $($result.generatedAt)")
[void]$md.Add("")
[void]$md.Add("Grade: **$($result.readinessGrade)**")
[void]$md.Add("")
[void]$md.Add($result.readinessMeaning)
[void]$md.Add("")
[void]$md.Add("## Coverage")
[void]$md.Add("- Runtime mobys decoded: $($result.coverage.decodedRuntimeMobys)")
[void]$md.Add("- Placement-like runtime mobys: $($result.coverage.placementRuntimeMobys)")
[void]$md.Add("- Strong identity mobys: $($result.coverage.strongIdentityMobys)")
[void]$md.Add("- Scaled source-window leads: $($result.coverage.scaledSourceLeadMobys)")
[void]$md.Add("- Target rows: $($result.coverage.targetRows) ($($result.coverage.usableClassTargets) usable class, $($result.coverage.patchTestableHypothesisTargets) patch-testable, $($result.coverage.routeHypothesisTargets) route-only, $($result.coverage.blockedTargets) blocked)")
[void]$md.Add("- Focused RAM pairs: $($result.coverage.completedFocusedPairs) complete, $($result.coverage.missingFocusedPairs) missing")
[void]$md.Add("")
[void]$md.Add("## Target Readiness")
[void]$md.Add("")
[void]$md.Add("| Target | Grade | Candidate mobys | Source leads | Next validation |")
[void]$md.Add("| --- | --- | --- | --- | --- |")
foreach ($target in $targetReadiness) {
    $name = ([string](Get-Field $target "targetName" "")).Replace("|", "/")
    $grade = [string](Get-Field $target "grade" "")
    $indexes = Format-Indexes (Get-ArrayField $target "candidateIndexes")
    $source = ([string](Get-Field $target "sourceLeadSummary" (Get-Field $target "sourceLeads" "none"))).Replace("|", "/")
    $next = ([string](Get-Field $target "nextValidation" "")).Replace("|", "/")
    [void]$md.Add("| $name | $grade | $indexes | $source | $next |")
}
[void]$md.Add("")
[void]$md.Add("## Blockers")
if ($blockers.Count -eq 0) {
    [void]$md.Add("- No readiness blockers recorded.")
}
else {
    foreach ($blocker in $blockers) {
        [void]$md.Add("- $blocker")
    }
}
[void]$md.Add("")
[void]$md.Add("## Next Actions")
foreach ($action in @(Get-ArrayField $result "nextActions")) {
    [void]$md.Add("- $action")
}
[void]$md.Add("")
[void]$md.Add("## Public Layout Sources")
foreach ($source in @(Get-ArrayField $result "publicLayoutSources")) {
    [void]$md.Add("- $source")
}

$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
$md | Set-Content -LiteralPath $resolvedMarkdown -Encoding UTF8

Write-Host "Wrote Stone Hill readiness JSON to $resolvedJson"
Write-Host "Wrote Stone Hill readiness Markdown to $resolvedMarkdown"
Write-Host "Grade: $($result.readinessGrade); strong identities: $strongIdentityCount; source leads: $scaledLeadCount; missing pairs: $($missingPairs.Count)"
