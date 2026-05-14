param(
    [string]$CatalogPath = ".\stonehill-moby-catalog.json",
    [string]$ReplicaPlanPath = ".\stonehill-replica-map-plan.json",
    [string]$TargetBoardPath = ".\stonehill-replica-target-board.json",
    [string]$PatchMatrixPath = ".\stonehill-patch-test-matrix.json",
    [string]$SpecialDataPath = ".\stonehill-moby-special-data.json",
    [string]$ReadinessPath = ".\stonehill-replica-readiness.json",
    [string]$LiveOverridesPath = ".\stonehill-live-validation-overrides.json",
    [string]$OutJsonPath = ".\stonehill-identity-matrix.json",
    [string]$OutMarkdownPath = ".\stonehill-identity-matrix.md"
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

function Merge-Objects($Base, $Override) {
    if ($null -eq $Override) { return $Base }
    $merged = [ordered]@{}
    if ($null -ne $Base) {
        foreach ($prop in $Base.PSObject.Properties) {
            $merged[$prop.Name] = $prop.Value
        }
    }
    foreach ($prop in $Override.PSObject.Properties) {
        $hasValue = $null -ne $prop.Value
        if ($hasValue -and $prop.Value -is [string]) { $hasValue = -not [string]::IsNullOrWhiteSpace($prop.Value) }
        if ($hasValue -and $prop.Value -is [System.Array]) { $hasValue = @($prop.Value).Count -gt 0 }
        if ($hasValue) {
            $merged[$prop.Name] = $prop.Value
        }
    }
    return $merged
}

function Get-LiveOverrideMap($LiveOverrides) {
    $map = @{}
    foreach ($moby in @(Get-ArrayField $LiveOverrides "mobys")) {
        $index = [int](Get-Field $moby "index" -1)
        if ($index -ge 0) { $map[$index] = $moby }
    }
    return $map
}

function Format-Indexes($Indexes) {
    $items = @($Indexes | Where-Object { $null -ne $_ } | ForEach-Object { [int]$_ } | Sort-Object -Unique)
    if ($items.Count -eq 0) { return "none" }
    return ($items | ForEach-Object { "L$_" }) -join ", "
}

function Get-TargetCertainty($Target) {
    $name = [string](Get-Field $Target "targetName" "")
    $status = [string](Get-Field $Target "status" "")
    $candidates = @(Get-ArrayField $Target "candidateIndexes")
    $sourceLeads = [string](Get-Field $Target "sourceLeads" "none")
    $relatedSourceLeads = [string](Get-Field $Target "relatedSourceLeads" "none")

    if ($name -like "*treasure*" -and $status -like "*usable*") { return "confirmed class" }
    if ($candidates.Count -gt 0 -and ($sourceLeads -ne "none" -or $relatedSourceLeads -ne "none")) { return "patch-testable hypothesis" }
    if ($candidates.Count -gt 0) { return "route hypothesis" }
    return "blocked"
}

function Get-SourceSummary($Target) {
    $sourceLeads = [string](Get-Field $Target "sourceLeads" "none")
    $relatedSourceLeads = [string](Get-Field $Target "relatedSourceLeads" "none")
    if ($sourceLeads -ne "none") { return $sourceLeads }
    if ($relatedSourceLeads -ne "none") { return "related: $relatedSourceLeads" }
    return "none"
}

function Get-SpecialDataMap($SpecialData) {
    $map = @{}
    foreach ($record in @(Get-ArrayField $SpecialData "records")) {
        $index = [int](Get-Field $record "index" -1)
        if ($index -lt 0 -or $map.ContainsKey($index)) { continue }
        $map[$index] = $record
    }
    return $map
}

function Get-PatchMatrixMap($PatchMatrix) {
    $map = @{}
    foreach ($test in @(Get-ArrayField $PatchMatrix "rankedPatchTests")) {
        $index = [int](Get-Field $test "index" -1)
        if ($index -ge 0 -and -not $map.ContainsKey($index)) {
            $map[$index] = $test
        }
    }
    return $map
}

function Get-MarkerSourceLeadText($Marker) {
    $lead = Get-Field $Marker "sourceLead" $null
    if ($null -eq $lead) { return "no source lead" }
    $status = [string](Get-Field $lead "status" "no source lead")
    $best = Get-Field $lead "bestScaledWindow" $null
    if ($null -eq $best) { return $status }
    $window = [string](Get-Field $best "wadRelativeWindow" "")
    $values = (@(Get-ArrayField $best "values") | Select-Object -First 3) -join "; "
    if ([string]::IsNullOrWhiteSpace($window)) { return $status }
    return "$status $window $values".Trim()
}

function Get-PatchMatrixSourceLeadText($PatchTest) {
    if ($null -eq $PatchTest) { return "" }
    $lead = Get-Field $PatchTest "sourceLead" $null
    if ($null -eq $lead) { return "" }
    $status = [string](Get-Field $lead "status" "")
    if ($status -ne "patch-testable") { return "" }
    $window = [string](Get-Field $lead "wadRelativeWindow" "")
    $axisSet = [string](Get-Field $lead "axisSet" "")
    $values = (@(Get-ArrayField $lead "values") | Select-Object -First 3) -join "; "
    $parts = @("patch-test lead")
    if (-not [string]::IsNullOrWhiteSpace($window)) { $parts += $window }
    if (-not [string]::IsNullOrWhiteSpace($axisSet)) { $parts += $axisSet }
    if (-not [string]::IsNullOrWhiteSpace($values)) { $parts += $values }
    return ($parts -join " ").Trim()
}

function Get-PatchMatrixSourceLeadShort($PatchTest) {
    if ($null -eq $PatchTest) { return "" }
    $index = [int](Get-Field $PatchTest "index" -1)
    $lead = Get-Field $PatchTest "sourceLead" $null
    if ($null -eq $lead) { return "" }
    if ([string](Get-Field $lead "status" "") -ne "patch-testable") { return "" }
    $window = [string](Get-Field $lead "wadRelativeWindow" "")
    if ([string]::IsNullOrWhiteSpace($window)) { return "" }
    $axisSet = [string](Get-Field $lead "axisSet" "")
    $prefix = if ($index -ge 0) { "L$index" } else { "L?" }
    return "$prefix@$window $axisSet".Trim()
}

function Get-TargetSourceSummary($Target, $PatchByIndex) {
    $patchParts = New-Object System.Collections.Generic.List[string]
    foreach ($indexValue in @(Get-ArrayField $Target "candidateIndexes")) {
        $index = [int]$indexValue
        if (-not $PatchByIndex.ContainsKey($index)) { continue }
        $text = Get-PatchMatrixSourceLeadShort $PatchByIndex[$index]
        if (-not [string]::IsNullOrWhiteSpace($text)) { [void]$patchParts.Add($text) }
        if ($patchParts.Count -ge 8) { break }
    }
    if ($patchParts.Count -gt 0) { return ($patchParts.ToArray() -join "; ") }
    return Get-SourceSummary $Target
}

function Get-MarkerTargetText($Marker) {
    $label = [string](Get-Field $Marker "displayTargetLabel" "")
    $candidates = @(Get-ArrayField $Marker "publicTargetCandidates" | ForEach-Object { [string](Get-Field $_ "name" "") } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($candidates.Count -gt 0) {
        $joined = ($candidates | Select-Object -First 4) -join " / "
        if (-not [string]::IsNullOrWhiteSpace($label) -and $label -ne $joined) { return "$label ($joined)" }
        return $joined
    }
    if (-not [string]::IsNullOrWhiteSpace($label)) { return $label }
    return [string](Get-Field $Marker "workingRole" "unidentified moby")
}

$catalog = Read-JsonIfPresent $CatalogPath
$replicaPlan = Read-JsonIfPresent $ReplicaPlanPath
$targetBoard = Read-JsonIfPresent $TargetBoardPath
$patchMatrix = Read-JsonIfPresent $PatchMatrixPath
$specialData = Read-JsonIfPresent $SpecialDataPath
$readiness = Read-JsonIfPresent $ReadinessPath
$liveOverrides = Read-JsonIfPresent $LiveOverridesPath
$liveByIndex = Get-LiveOverrideMap $liveOverrides
$patchByIndex = Get-PatchMatrixMap $patchMatrix

$targetRows = @(Get-ArrayField $targetBoard "targets" | ForEach-Object {
    $indexes = @(Get-ArrayField $_ "candidateIndexes")
    [ordered]@{
        target = [string](Get-Field $_ "targetName" "")
        certainty = Get-TargetCertainty $_
        candidateMobys = Format-Indexes $indexes
        relatedMobys = Format-Indexes (Get-ArrayField $_ "relatedRouteIndexes")
        sourceLeads = Get-TargetSourceSummary $_ $patchByIndex
        editorUse = [string](Get-Field $_ "editorAction" "")
        nextProof = [string](Get-Field $_ "validationAction" "")
    }
})

$specialByIndex = Get-SpecialDataMap $specialData
$mobyRows = @(Get-ArrayField $replicaPlan "mobyMarkers" |
    Sort-Object { [int](Get-Field $_ "index" 999999) } |
    ForEach-Object {
        $index = [int](Get-Field $_ "index" -1)
        $marker = $_
        if ($liveByIndex.ContainsKey($index)) {
            $marker = Merge-Objects $marker $liveByIndex[$index]
        }
        $special = $null
        if ($specialByIndex.ContainsKey($index)) { $special = $specialByIndex[$index] }
        $specialText = "none"
        if ($null -ne $special -and [bool](Get-Field $special "validMainRamPointer" $false)) {
            $specialText = [string](Get-Field $special "specialDataPointer" "")
            $matches = @(Get-ArrayField $special "sourceMatches" | Where-Object { [int](Get-Field $_ "count" 0) -gt 0 })
            if ($matches.Count -gt 0) {
                $specialText += " WAD-match"
            }
            $pointerFields = @(Get-ArrayField $special "pointerFields")
            if ($pointerFields.Count -gt 0) {
                $specialText += " linked-pointers=$($pointerFields.Count)"
            }
        }
        $sourceLeadText = Get-MarkerSourceLeadText $marker
        if ($patchByIndex.ContainsKey($index)) {
            $patchLeadText = Get-PatchMatrixSourceLeadText $patchByIndex[$index]
            if (-not [string]::IsNullOrWhiteSpace($patchLeadText)) {
                $sourceLeadText = $patchLeadText
            }
        }
        [ordered]@{
            moby = $(if ($index -ge 0) { "L$index" } else { "L?" })
            index = $index
            typeHex = [string](Get-Field $marker "typeHex" "")
            target = Get-MarkerTargetText $marker
            certainty = [string](Get-Field (Get-Field $marker "publicTargetGuess" $null) "confidence" (Get-Field $marker "confidence" "unknown"))
            zone = [string](Get-Field $marker "zoneLabel" "")
            sourceLead = $sourceLeadText
            specialData = $specialText
            editorAction = "Load Stone Hill Workbench, choose this L# in the target picker, then move it or queue SH leads only for disposable tests."
        }
    })

$readinessGrade = [string](Get-Field $readiness "readinessGrade" "unknown")
$coverage = Get-Field $readiness "coverage" $null
$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Single lookup table for deciding which Stone Hill runtime moby corresponds to each known item, enemy, dragon, or route target."
    liveOverridesPath = $(if ($null -ne $liveOverrides) { (Resolve-Path -LiteralPath $LiveOverridesPath).Path } else { $null })
    patchMatrixPath = $(if ($null -ne $patchMatrix) { (Resolve-Path -LiteralPath $PatchMatrixPath).Path } else { $null })
    status = [ordered]@{
        grade = $readinessGrade
        conclusion = "The editor can show and move the current Stone Hill map proxy. Type 0x20 is a confirmed treasure class; named dragons, enemies, key, and chest remain hypotheses until focused RAM pairs or visible patch tests prove them."
        decodedRuntimeMobys = [int](Get-Field $coverage "decodedRuntimeMobys" (Get-Field $catalog "decodedMobys" 0))
        strongIdentityMobys = [int](Get-Field $coverage "strongIdentityMobys" 0)
        scaledSourceLeadMobys = [int](Get-Field $coverage "scaledSourceLeadMobys" 0)
        missingFocusedPairs = [int](Get-Field $coverage "missingFocusedPairs" 0)
    }
    targetLookup = $targetRows
    mobyLookup = $mobyRows
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
[void]$md.Add("# Stone Hill Identity Matrix")
[void]$md.Add("")
[void]$md.Add("Generated: $($result.generatedAt)")
[void]$md.Add("")
[void]$md.Add("## Status")
[void]$md.Add("- Grade: $($result.status.grade)")
[void]$md.Add("- Conclusion: $($result.status.conclusion)")
[void]$md.Add("- Runtime mobys: $($result.status.decodedRuntimeMobys)")
[void]$md.Add("- Strong identity mobys: $($result.status.strongIdentityMobys)")
[void]$md.Add("- Scaled source-window leads: $($result.status.scaledSourceLeadMobys)")
[void]$md.Add("- Missing focused RAM pairs: $($result.status.missingFocusedPairs)")
[void]$md.Add("")
[void]$md.Add("## Target Lookup")
[void]$md.Add("")
[void]$md.Add("| Target | Certainty | Candidate mobys | Related mobys | Source leads | Next proof |")
[void]$md.Add("| --- | --- | --- | --- | --- | --- |")
foreach ($row in $targetRows) {
    $target = ([string](Get-Field $row "target" "")).Replace("|", "/")
    $source = ([string](Get-Field $row "sourceLeads" "none")).Replace("|", "/")
    $proof = ([string](Get-Field $row "nextProof" "")).Replace("|", "/")
    [void]$md.Add("| $target | $($row.certainty) | $($row.candidateMobys) | $($row.relatedMobys) | $source | $proof |")
}
[void]$md.Add("")
[void]$md.Add("## Moby Lookup")
[void]$md.Add("")
[void]$md.Add("| Moby | Type | Current target label | Certainty | Zone | Source lead | Special data |")
[void]$md.Add("| --- | --- | --- | --- | --- | --- | --- |")
foreach ($row in $mobyRows) {
    $target = ([string](Get-Field $row "target" "")).Replace("|", "/")
    $zone = ([string](Get-Field $row "zone" "")).Replace("|", "/")
    $sourceLead = ([string](Get-Field $row "sourceLead" "")).Replace("|", "/")
    $specialText = ([string](Get-Field $row "specialData" "")).Replace("|", "/")
    [void]$md.Add("| $($row.moby) | ``$($row.typeHex)`` | $target | $($row.certainty) | $zone | $sourceLead | $specialText |")
}
[void]$md.Add("")
[void]$md.Add("## Editor Use")
[void]$md.Add("- Load Stone Hill Workbench.")
[void]$md.Add("- Use the Stone Hill target picker to jump to the L# from Target Lookup.")
[void]$md.Add("- Move confirmed treasure-class mobys freely in the editor, but use source-window patches only on disposable BIN copies.")
[void]$md.Add("- Promote dragon/enemy/key/chest labels only after the matching RAM pair or visible patch test confirms the moby.")
[void]$md.Add("")
[void]$md.Add("## Sources")
foreach ($source in $result.publicSources) {
    [void]$md.Add("- $source")
}

$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
$md | Set-Content -LiteralPath $resolvedMarkdown -Encoding UTF8

Write-Host "Wrote Stone Hill identity matrix JSON to $resolvedJson"
Write-Host "Wrote Stone Hill identity matrix Markdown to $resolvedMarkdown"
Write-Host "Targets: $($targetRows.Count); moby rows: $($mobyRows.Count); grade: $readinessGrade"
