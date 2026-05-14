param(
    [string]$TargetBoardPath = ".\stonehill-replica-target-board.json",
    [string]$PatchTestMatrixPath = ".\stonehill-patch-test-matrix.json",
    [string]$FocusedPairsPath = ".\stonehill-focused-ram-pairs.json",
    [string]$ReadinessPath = ".\stonehill-replica-readiness.json",
    [string]$OutJsonPath = ".\stonehill-validation-playbook.json",
    [string]$OutMarkdownPath = ".\stonehill-validation-playbook.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function Get-ArrayField($Object, [string]$Name) {
    if ($null -eq $Object) { return @() }
    if ($Object -is [System.Collections.IDictionary]) {
        if (-not $Object.Contains($Name) -or $null -eq $Object[$Name]) { return @() }
        return @($Object[$Name])
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return @() }
    return @($property.Value)
}

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

function Read-JsonIfPresent([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $Path).Path | ConvertFrom-Json
}

function Format-Indexes($Indexes) {
    $items = @($Indexes | Where-Object { $null -ne $_ } | ForEach-Object { [int]$_ } | Sort-Object -Unique)
    if ($items.Count -eq 0) { return "none" }
    return ($items | ForEach-Object { "L$_" }) -join ", "
}

function Get-BestSourceLeadText($Test) {
    $lead = Get-Field $Test "sourceLead" $null
    if ($null -eq $lead -or [string](Get-Field $lead "status" "") -ne "patch-testable") { return "none" }
    $window = [string](Get-Field $lead "wadRelativeWindow" "")
    $axisSet = [string](Get-Field $lead "axisSet" "")
    $values = (@(Get-ArrayField $lead "values") -join "; ")
    return "$window $axisSet $values".Trim()
}

function Convert-Test($Test) {
    $index = [int](Get-Field $Test "index" -1)
    $target = [string](Get-Field $Test "target" "")
    $moby = [string](Get-Field $Test "moby" $(if ($index -ge 0) { "L$index" } else { "" }))
    $editorAction = [string](Get-Field $Test "editorAction" "")
    return [ordered]@{
        priority = [int](Get-Field $Test "priority" 0)
        target = $target
        moby = $moby
        index = $index
        typeHex = [string](Get-Field $Test "typeHex" "")
        sourceLead = Get-BestSourceLeadText $Test
        editorAction = $editorAction
        liveIdentificationMove = "In the workbench, drag $moby far from its cluster for top-down X/Y tests. For live-emulator identity checks, the SpyroEdit-style probe is to raise the selected object Z by about 1000 and watch which visible object jumps."
        successSignal = [string](Get-Field $Test "successSignal" "")
        fallback = [string](Get-Field $Test "fallback" "")
    }
}

function Convert-MissingPair($Pair) {
    return [ordered]@{
        id = [string](Get-Field $Pair "id" "")
        target = [string](Get-Field $Pair "target" "")
        location = [string](Get-Field $Pair "location" "")
        beforePath = [string](Get-Field $Pair "beforePath" "")
        afterPath = [string](Get-Field $Pair "afterPath" "")
        candidateMobys = Format-Indexes (Get-ArrayField $Pair "candidateIndexes")
        resolves = [string](Get-Field $Pair "resolves" "")
    }
}

$targetBoard = Read-JsonIfPresent $TargetBoardPath
$matrix = Read-JsonIfPresent $PatchTestMatrixPath
$focusedPairs = Read-JsonIfPresent $FocusedPairsPath
$readiness = Read-JsonIfPresent $ReadinessPath

$readinessGrade = "unknown"
$readinessMeaning = ""
if ($null -ne $readiness) {
    $readinessGrade = [string](Get-Field $readiness "readinessGrade" "unknown")
    $readinessMeaning = [string](Get-Field $readiness "readinessMeaning" "")
}

$topTests = @()
if ($null -ne $matrix) {
    $topTests = @(Get-ArrayField $matrix "rankedPatchTests" | Sort-Object { [int](Get-Field $_ "priority" 999) } | Select-Object -First 8 | ForEach-Object { Convert-Test $_ })
}

$targetRows = @()
if ($null -ne $targetBoard) {
    $targetRows = @(Get-ArrayField $targetBoard "targets" | ForEach-Object {
        [ordered]@{
            targetName = [string](Get-Field $_ "targetName" "")
            status = [string](Get-Field $_ "status" "")
            candidateMobys = Format-Indexes (Get-ArrayField $_ "candidateIndexes")
            relatedMobys = Format-Indexes (Get-ArrayField $_ "relatedRouteIndexes")
            sourceLeads = [string](Get-Field $_ "sourceLeads" "none")
            validationAction = [string](Get-Field $_ "validationAction" "")
        }
    })
}

$missingPairs = @()
if ($null -ne $focusedPairs) {
    $missingPairs = @(Get-ArrayField $focusedPairs "pairs" | Where-Object {
        -not (Test-Path -LiteralPath ([string](Get-Field $_ "beforePath" ""))) -or
        -not (Test-Path -LiteralPath ([string](Get-Field $_ "afterPath" "")))
    } | ForEach-Object { Convert-MissingPair $_ })
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Stone Hill validation playbook for turning the current map workbench and moby target guesses into confirmed item, enemy, dragon, key, and chest identities."
    currentReplicaStatus = [ordered]@{
        grade = $readinessGrade
        meaning = $readinessMeaning
        conclusion = "The editor can show the Stone Hill map proxy and move decoded runtime mobys, but it should not be called a full replica until focused RAM pairs and at least one source-window patch test confirm the remaining named targets."
    }
    externalTechnique = [ordered]@{
        source = "SpyroEdit guide"
        url = "https://lxshades.github.io/spyroedit/spyroedit_guide.html"
        usableIdea = "Identify an unknown object by selecting an object ID/type, moving one coordinate sharply, and watching which visible object moves; their guide specifically suggests increasing Z by about 1000."
        localAdaptation = "Use the Stone Hill target picker to choose L# markers, drag X/Y in the editor for map placement tests, and use focused RAM pairs plus disposable BIN patches to promote guesses into verified source records."
        liveProbe = "Load tools\pcsx-redux-stonehill-moby-probe.lua in PCSX-Redux's Lua console to select the same editor L# marker, apply large live RAM X/Y/Z offsets, and revert them after observing which object moved."
    }
    editorWorkflow = @(
        "Load Stone Hill Workbench.",
        "Choose a labelled L# in the Stone Hill target picker or Show SH Targets.",
        "Drag the selected marker far enough that the move would be obvious in-game.",
        "If using PCSX-Redux, run tools\pcsx-redux-stonehill-moby-probe.lua, choose the same L#, apply a Z delta around 1000, then revert after checking the visible object.",
        "For source tests, use Queue SH Leads and create a disposable patched BIN.",
        "For identity tests, capture isolated before/after RAM pairs from the missing-pair list and rerun the Stone Hill report pipeline.",
        "Promote only targets with counter/state changes or visible patched movement; keep route-only labels marked as hypotheses."
    )
    immediatePatchTests = $topTests
    targetLookup = $targetRows
    missingRamPairs = $missingPairs
    publicSources = @(
        "https://spyrowiki.com/wiki/Stone_Hill",
        "https://spyro.fandom.com/wiki/Stone_Hill",
        "https://strategywiki.org/wiki/Spyro_the_Dragon/Stone_Hill",
        "https://lxshades.github.io/spyroedit/spyroedit_guide.html"
    )
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# Stone Hill Validation Playbook")
[void]$md.Add("")
[void]$md.Add("Generated: $($result.generatedAt)")
[void]$md.Add("")
[void]$md.Add("## Current Status")
[void]$md.Add("- Grade: $($result.currentReplicaStatus.grade)")
if (-not [string]::IsNullOrWhiteSpace($result.currentReplicaStatus.meaning)) {
    [void]$md.Add("- Meaning: $($result.currentReplicaStatus.meaning)")
}
[void]$md.Add("- Conclusion: $($result.currentReplicaStatus.conclusion)")
[void]$md.Add("")
[void]$md.Add("## Live Identification Lead")
[void]$md.Add("- External lead: $($result.externalTechnique.source) - $($result.externalTechnique.url)")
[void]$md.Add("- Usable idea: $($result.externalTechnique.usableIdea)")
[void]$md.Add("- Local adaptation: $($result.externalTechnique.localAdaptation)")
[void]$md.Add("- Live emulator helper: $($result.externalTechnique.liveProbe)")
[void]$md.Add("")
[void]$md.Add("## Editor Workflow")
foreach ($step in $result.editorWorkflow) {
    [void]$md.Add("- $step")
}
[void]$md.Add("")
[void]$md.Add("## Immediate Patch Tests")
[void]$md.Add("")
[void]$md.Add("| Priority | Moby | Target | Source lead | Success signal |")
[void]$md.Add("| --- | --- | --- | --- | --- |")
foreach ($test in $topTests) {
    $target = ([string](Get-Field $test "target" "")).Replace("|", "/")
    $sourceLead = ([string](Get-Field $test "sourceLead" "none")).Replace("|", "/")
    $success = ([string](Get-Field $test "successSignal" "")).Replace("|", "/")
    [void]$md.Add("| $($test.priority) | $($test.moby) ``$($test.typeHex)`` | $target | $sourceLead | $success |")
}
[void]$md.Add("")
[void]$md.Add("## Target Lookup")
[void]$md.Add("")
[void]$md.Add("| Target | Status | Candidate mobys | Related mobys | Source leads | Validation |")
[void]$md.Add("| --- | --- | --- | --- | --- | --- |")
foreach ($row in $targetRows) {
    $name = ([string](Get-Field $row "targetName" "")).Replace("|", "/")
    $status = ([string](Get-Field $row "status" "")).Replace("|", "/")
    $source = ([string](Get-Field $row "sourceLeads" "none")).Replace("|", "/")
    $validation = ([string](Get-Field $row "validationAction" "")).Replace("|", "/")
    [void]$md.Add("| $name | $status | $($row.candidateMobys) | $($row.relatedMobys) | $source | $validation |")
}
[void]$md.Add("")
[void]$md.Add("## Missing RAM Pairs")
[void]$md.Add("")
[void]$md.Add("| Pair | Target | Candidate mobys | Before | After |")
[void]$md.Add("| --- | --- | --- | --- | --- |")
foreach ($pair in $missingPairs) {
    [void]$md.Add("| $($pair.id) | $($pair.target) | $($pair.candidateMobys) | $($pair.beforePath) | $($pair.afterPath) |")
}
[void]$md.Add("")
[void]$md.Add("## Sources")
foreach ($source in $result.publicSources) {
    [void]$md.Add("- $source")
}

$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
$md | Set-Content -LiteralPath $resolvedMarkdown -Encoding UTF8

Write-Host "Wrote Stone Hill validation playbook JSON to $resolvedJson"
Write-Host "Wrote Stone Hill validation playbook Markdown to $resolvedMarkdown"
Write-Host "Immediate tests: $($topTests.Count); missing RAM pairs: $($missingPairs.Count)"
