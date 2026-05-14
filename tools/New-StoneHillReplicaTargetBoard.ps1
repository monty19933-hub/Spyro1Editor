param(
    [string]$ReplicaPlanPath = ".\stonehill-replica-map-plan.json",
    [string]$LiveOverridesPath = ".\stonehill-live-validation-overrides.json",
    [string]$OutJsonPath = ".\stonehill-replica-target-board.json",
    [string]$OutMarkdownPath = ".\stonehill-replica-target-board.md"
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
    return [pscustomobject]$merged
}

function Get-LiveOverrideMap($LiveOverrides) {
    $map = @{}
    foreach ($moby in @(Get-ArrayField $LiveOverrides "mobys")) {
        $index = [int](Get-Field $moby "index" -1)
        if ($index -ge 0) { $map[$index] = $moby }
    }
    return $map
}

function Build-PublicTargetLedger($Markers) {
    return @($Markers |
        ForEach-Object {
            $marker = $_
            foreach ($candidate in @(Get-ArrayField $marker "publicTargetCandidates")) {
                [pscustomobject][ordered]@{
                    name = [string](Get-Field $candidate "name" "")
                    category = [string](Get-Field $candidate "category" "")
                    confidence = [string](Get-Field $candidate "confidence" "")
                    index = [int](Get-Field $marker "index" -1)
                    typeHex = [string](Get-Field $marker "typeHex" "")
                    zoneLabel = [string](Get-Field $marker "zoneLabel" "")
                    displayTargetLabel = [string](Get-Field $marker "displayTargetLabel" "")
                    hasScaledSourceLead = ($null -ne (Get-Field $marker "sourceLead" $null) -and [int](Get-Field (Get-Field $marker "sourceLead" $null) "scaledWindowCount" 0) -gt 0)
                }
            }
        } |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string](Get-Field $_ "name" "")) } |
        Group-Object name |
        Sort-Object Name |
        ForEach-Object {
            $group = @($_.Group)
            [ordered]@{
                name = $_.Name
                category = [string](Get-Field $group[0] "category" "")
                confidence = [string](Get-Field $group[0] "confidence" "")
                candidateCount = $group.Count
                candidateIndexes = @($group | ForEach-Object { [int](Get-Field $_ "index" -1) } | Sort-Object -Unique)
                candidateTypes = @($group | ForEach-Object { [string](Get-Field $_ "typeHex" "") } | Sort-Object -Unique)
                zones = @($group | ForEach-Object { [string](Get-Field $_ "zoneLabel" "") } | Sort-Object -Unique)
                scaledSourceLeadCount = @($group | Where-Object { [bool](Get-Field $_ "hasScaledSourceLead" $false) }).Count
            }
        })
}

function Format-Indexes($Indexes) {
    $items = @($Indexes | Where-Object { $null -ne $_ } | ForEach-Object { [int]$_ } | Sort-Object -Unique)
    if ($items.Count -eq 0) { return "none" }
    return ($items | ForEach-Object { "L$_" }) -join ", "
}

function Format-SourceLeads($Markers) {
    $items = @()
    foreach ($marker in @($Markers)) {
        $sourceLead = Get-Field $marker "sourceLead" $null
        if ($null -eq $sourceLead) { continue }
        $count = [int](Get-Field $sourceLead "scaledWindowCount" 0)
        if ($count -le 0) { continue }
        $best = Get-Field $sourceLead "bestScaledWindow" $null
        $window = if ($null -ne $best) { [string](Get-Field $best "wadRelativeWindow" "") } else { "" }
        if ([string]::IsNullOrWhiteSpace($window)) {
            $items += "L$([int](Get-Field $marker "index" -1)):$count"
        }
        else {
            $items += "L$([int](Get-Field $marker "index" -1)):$count@$window"
        }
    }
    if ($items.Count -eq 0) { return "none" }
    return ($items | Sort-Object -Unique) -join "; "
}

function Convert-MarkerBrief($Marker) {
    $sourceLead = Get-Field $Marker "sourceLead" $null
    $best = if ($null -ne $sourceLead) { Get-Field $sourceLead "bestScaledWindow" $null } else { $null }
    return [ordered]@{
        index = [int](Get-Field $Marker "index" -1)
        typeHex = [string](Get-Field $Marker "typeHex" "")
        stateHex = [string](Get-Field $Marker "stateHex" "")
        mapX = [double](Get-Field $Marker "mapX" 0)
        mapY = [double](Get-Field $Marker "mapY" 0)
        height = [double](Get-Field $Marker "height" 0)
        zoneLabel = [string](Get-Field $Marker "zoneLabel" "")
        displayTargetLabel = [string](Get-Field $Marker "displayTargetLabel" "")
        scaledSourceWindowCount = $(if ($null -ne $sourceLead) { [int](Get-Field $sourceLead "scaledWindowCount" 0) } else { 0 })
        bestWadRelativeWindow = $(if ($null -ne $best) { [string](Get-Field $best "wadRelativeWindow" "") } else { "" })
        bestValues = $(if ($null -ne $best) { @(Get-ArrayField $best "values") } else { @() })
    }
}

function New-TargetRow(
    [string]$Name,
    [string]$Category,
    [string]$PublicRoute,
    [string]$Status,
    [string]$Confidence,
    [int[]]$CandidateIndexes,
    [int[]]$RelatedIndexes,
    [string]$EditorAction,
    [string]$ValidationAction,
    [string]$Reason
) {
    $candidateMarkers = @($CandidateIndexes | ForEach-Object {
        $idx = [int]$_
        $markerByIndex[$idx]
    } | Where-Object { $null -ne $_ })
    $relatedMarkers = @($RelatedIndexes | ForEach-Object {
        $idx = [int]$_
        $markerByIndex[$idx]
    } | Where-Object { $null -ne $_ })

    return [ordered]@{
        targetName = $Name
        category = $Category
        publicRoute = $PublicRoute
        status = $Status
        confidence = $Confidence
        candidateIndexes = @($CandidateIndexes | Sort-Object -Unique)
        relatedRouteIndexes = @($RelatedIndexes | Sort-Object -Unique)
        sourceLeads = Format-SourceLeads $candidateMarkers
        relatedSourceLeads = Format-SourceLeads $relatedMarkers
        editorAction = $EditorAction
        validationAction = $ValidationAction
        reason = $Reason
        candidates = @($candidateMarkers | ForEach-Object { Convert-MarkerBrief $_ })
        relatedRouteMarkers = @($relatedMarkers | ForEach-Object { Convert-MarkerBrief $_ })
    }
}

function New-LocationRow(
    [string]$Name,
    [string]$PublicLocation,
    [string[]]$ExpectedTargets,
    [string]$Status,
    [string]$Confidence,
    [int[]]$CandidateIndexes,
    [string]$EditorUse,
    [string]$ValidationAction
) {
    $candidateMarkers = @($CandidateIndexes | ForEach-Object {
        $idx = [int]$_
        $markerByIndex[$idx]
    } | Where-Object { $null -ne $_ })

    return [ordered]@{
        locationName = $Name
        publicLocation = $PublicLocation
        expectedTargets = @($ExpectedTargets)
        status = $Status
        confidence = $Confidence
        candidateIndexes = @($CandidateIndexes | Sort-Object -Unique)
        sourceLeads = Format-SourceLeads $candidateMarkers
        editorUse = $EditorUse
        validationAction = $ValidationAction
        markers = @($candidateMarkers | ForEach-Object { Convert-MarkerBrief $_ })
    }
}

function Get-LedgerIndexes([string]$Name) {
    $record = @($ledger | Where-Object { [string](Get-Field $_ "name" "") -eq $Name } | Select-Object -First 1)
    if ($record.Count -eq 0) { return @() }
    return @(Get-ArrayField $record[0] "candidateIndexes" | ForEach-Object { [int]$_ })
}

$resolvedPlan = Resolve-Path -LiteralPath $ReplicaPlanPath
$plan = Get-Content -Raw -LiteralPath $resolvedPlan.Path | ConvertFrom-Json
$liveOverrides = Read-JsonIfPresent $LiveOverridesPath
$liveByIndex = Get-LiveOverrideMap $liveOverrides
$markers = @(Get-ArrayField $plan "mobyMarkers" | ForEach-Object {
    $index = [int](Get-Field $_ "index" -1)
    if ($liveByIndex.ContainsKey($index)) { Merge-Objects $_ $liveByIndex[$index] } else { $_ }
})
$ledger = @(Build-PublicTargetLedger $markers)
$publicLayoutAnchors = Get-Field $plan "publicLayoutAnchors" $null
$routeGraph = @()
if ($null -ne $publicLayoutAnchors) {
    $routeGraph = @(Get-ArrayField $publicLayoutAnchors "routeGraph")
}

$markerByIndex = @{}
foreach ($marker in $markers) {
    $markerByIndex[[int](Get-Field $marker "index" -1)] = $marker
}

$gemIndexes = @(
    @(Get-LedgerIndexes "Stone Hill treasure/gem") +
    @(Get-LedgerIndexes "Collected Stone Hill gem") +
    @(Get-LedgerIndexes "Red gem") +
    @(Get-LedgerIndexes "Stone Hill treasure") +
    @(Get-LedgerIndexes "Metal chest") +
    @(Get-LedgerIndexes "Regular chest")
) | Sort-Object -Unique
$confirmedGemIndexes = @(Get-LedgerIndexes "Collected Stone Hill gem")
$astorIndexes = @(Get-LedgerIndexes "Astor")
$lindarIndexes = @(Get-LedgerIndexes "Lindar")
$gildasIndexes = @(Get-LedgerIndexes "Gildas")
$highFieldEnemyIndexes = @((Get-LedgerIndexes "Blue thief") + (Get-LedgerIndexes "Ram") + (Get-LedgerIndexes "Shepherd") | Sort-Object -Unique)
$towerEnemyIndexes = @(Get-LedgerIndexes "Ram or shepherd")
$keyIndexes = @(Get-LedgerIndexes "Hidden beach key")
$wellIndexes = @(Get-LedgerIndexes "Locked chest or well helper")
$startRouteIndexes = @(Get-LedgerIndexes "Start/well route object")
$hilltopObjectIndexes = @(Get-LedgerIndexes "Blue thief loop or hilltop object")
$helperIndexes = @(Get-LedgerIndexes "Internal helper/state record")
$lateTableIndexes = @(Get-LedgerIndexes "unverified late-table record")

$startFieldIndexes = @(43, 186)
$castleIndexes = @($astorIndexes + @(186, 208) | Sort-Object -Unique)
$leftCaveIndexes = @($lindarIndexes + @(98, 109, 120, 153) | Sort-Object -Unique)
$towerIndexes = @($gildasIndexes + @(87, 98, 142, 153) | Sort-Object -Unique)
$highFieldIndexes = @($highFieldEnemyIndexes + $hilltopObjectIndexes + @(10, 21, 32, 131) | Sort-Object -Unique)

$astorEditorAction = if ($astorIndexes.Count -gt 0) { "Move $(Format-Indexes $astorIndexes) only as a dragon hypothesis, then source-test after a fresh Astor-free RAM pair." } else { "No live-tested Astor moby is available; L65 is now a tree, so capture Astor-free RAM before patching a dragon candidate." }
$lindarEditorAction = if ($lindarIndexes.Count -gt 0) { "Move $(Format-Indexes $lindarIndexes) only as a Lindar hypothesis, then confirm with a Lindar-free RAM pair." } else { "No live-tested Lindar moby is available; L54 is now a tree, so capture Lindar-free RAM before assigning a source record." }
$gildasEditorAction = if ($gildasIndexes.Count -gt 0) { "Move $(Format-Indexes $gildasIndexes) only as a Gildas hypothesis, then confirm with a Gildas-free RAM pair." } else { "No live-tested Gildas moby is available; L54 is now a tree and high-field 0x18 leads are red gems, so capture Gildas-free RAM first." }
$astorStatus = if ($astorIndexes.Count -gt 0) { "best named-dragon candidate" } else { "not mapped to a dragon moby yet" }
$lindarStatus = if ($lindarIndexes.Count -gt 0) { "ambiguous with Gildas" } else { "not mapped to a dragon moby yet" }
$gildasStatus = if ($gildasIndexes.Count -gt 0) { "ambiguous with Lindar" } else { "not mapped to a dragon moby yet" }
$thiefEditorAction = if ($highFieldEnemyIndexes.Count -gt 0) { "Move $(Format-Indexes $highFieldEnemyIndexes) only as thief/enemy hypotheses, then confirm with a thief-caught RAM pair." } else { "No live-tested thief moby is available; L87/L142/L153 are red gems, so capture a blue-thief RAM pair before patching enemy records." }
$enemyEditorAction = if (($highFieldEnemyIndexes.Count + $towerEnemyIndexes.Count) -gt 0) { "Move active-object candidates only after a focused ram/shepherd defeat pair points at them." } else { "No live-tested ram/shepherd moby is available; current high-field active-looking leads are red gems or inactive helpers." }

$targetRows = @(
    (New-TargetRow `
        -Name "Stone Hill treasure/gems" `
        -Category "treasure" `
        -PublicRoute "200 total treasure spread across gems, chests, enemies, and the locked chest." `
        -Status "usable class label" `
        -Confidence "strong class; one confirmed collected sample" `
        -CandidateIndexes $gemIndexes `
        -RelatedIndexes @() `
        -EditorAction "Load Stone Hill Workbench; L186 is the collected-gem sample, and L10/L21/L32/L43/L109/L120/L131 are confirmed treasure-class markers." `
        -ValidationAction "Patch-test L186 or L109 source leads on a disposable BIN, then collect adjacent treasure samples." `
        -Reason "Type 0x20 changed in the gem-pickup RAM comparison; L186 has the strongest specific evidence because it changed state while globalGemCount increased.")
    (New-TargetRow `
        -Name "Astor" `
        -Category "dragon" `
        -PublicRoute "Return Home portal / castle exit side." `
        -Status $astorStatus `
        -Confidence "weak route fit" `
        -CandidateIndexes $astorIndexes `
        -RelatedIndexes @() `
        -EditorAction $astorEditorAction `
        -ValidationAction "Capture before/after RAM while freeing Astor, then rerun the moby catalog and source-link scripts." `
        -Reason "Live validation removed the old L65 dragon hypothesis by identifying it as tree/scenery.")
    (New-TargetRow `
        -Name "Lindar" `
        -Category "dragon" `
        -PublicRoute "Cave with gems and chests, left/start-side route." `
        -Status $lindarStatus `
        -Confidence "weak route fit" `
        -CandidateIndexes $lindarIndexes `
        -RelatedIndexes $towerEnemyIndexes `
        -EditorAction $lindarEditorAction `
        -ValidationAction "Capture a before/after RAM sample freeing Lindar in the cave." `
        -Reason "Live validation removed the old L54 dragon hypothesis by identifying it as tree/scenery.")
    (New-TargetRow `
        -Name "Gildas" `
        -Category "dragon" `
        -PublicRoute "Tower top near the blue-thief high-field route." `
        -Status $gildasStatus `
        -Confidence "weak route fit" `
        -CandidateIndexes $gildasIndexes `
        -RelatedIndexes $highFieldEnemyIndexes `
        -EditorAction $gildasEditorAction `
        -ValidationAction "Capture a before/after RAM sample freeing Gildas on the tower." `
        -Reason "Public guides place Gildas near the thief route, but current live-tested candidates in that area are scenery or red gems.")
    (New-TargetRow `
        -Name "Gavin" `
        -Category "dragon" `
        -PublicRoute "Dry well near the starting area and locked chest." `
        -Status "not mapped to a dragon moby yet" `
        -Confidence "open gap" `
        -CandidateIndexes @() `
        -RelatedIndexes $wellIndexes `
        -EditorAction "Use the related well/chest route markers for orientation only; no 0x30 Gavin marker is isolated yet." `
        -ValidationAction "Capture a before/after RAM sample freeing Gavin in the well, ideally before opening the locked chest." `
        -Reason "The public route is clear, but the current loaded 0x30 candidates do not sit in the low-map/west well cluster.")
    (New-TargetRow `
        -Name "Blue egg thief" `
        -Category "enemy/egg" `
        -PublicRoute "High open fields surrounding Gildas's tower." `
        -Status "candidate group" `
        -Confidence "weak route fit" `
        -CandidateIndexes $highFieldEnemyIndexes `
        -RelatedIndexes $hilltopObjectIndexes `
        -EditorAction $thiefEditorAction `
        -ValidationAction "Capture before/after RAM after catching the thief; expect egg count/state flags and high-field records to change." `
        -Reason "The high-map/east 0x18 records match the public thief/enemy route, and L87 has actionable source-window leads.")
    (New-TargetRow `
        -Name "Rams and shepherds" `
        -Category "enemy" `
        -PublicRoute "Start/cave/tower/high-field routes." `
        -Status "candidate groups" `
        -Confidence "weak class and route fit" `
        -CandidateIndexes @($highFieldEnemyIndexes + $towerEnemyIndexes | Sort-Object -Unique) `
        -RelatedIndexes @() `
        -EditorAction $enemyEditorAction `
        -ValidationAction "Capture separate before/after RAM after defeating one ram and one shepherd." `
        -Reason "Type 0x18 repeats as placed active-object records, but no enemy-family sample has split ram from shepherd yet.")
    (New-TargetRow `
        -Name "Hidden beach key" `
        -Category "item" `
        -PublicRoute "Secret beach below/behind the Return Home portal, inside the hidden cave." `
        -Status "route candidates only" `
        -Confidence "weak route fit" `
        -CandidateIndexes $keyIndexes `
        -RelatedIndexes @() `
        -EditorAction "No current non-sparse key marker. Treat L421/L422 as late-table diagnostics only until a key-pickup RAM pair confirms them." `
        -ValidationAction "Capture before/after RAM after picking up the key." `
        -Reason "Public guides place the key on the beach/cave route, but current source windows do not yet isolate a key record.")
    (New-TargetRow `
        -Name "Locked chest" `
        -Category "container" `
        -PublicRoute "Bottom of the dry well near Gavin." `
        -Status "route candidate group" `
        -Confidence "weak route fit" `
        -CandidateIndexes $wellIndexes `
        -RelatedIndexes $startRouteIndexes `
        -EditorAction "Use only L95/L183 for well/chest experiments; L390/L399/L412/L413 are late-table diagnostics, not replica targets." `
        -ValidationAction "Capture before/after RAM after opening the locked chest with the key." `
        -Reason "Public guides place the locked chest in the well; the low-map/west cluster is dense and needs a focused chest sample.")
)

$locationRows = @(
    (New-LocationRow `
        -Name "Start field hub" `
        -PublicLocation "Opening field where the routes split toward the well, castle/Return Home, cave/tower path, and high fields." `
        -ExpectedTargets @("start-area treasure", "route split helpers", "nearby well access") `
        -Status "usable route bucket" `
        -Confidence "weak route fit, strong map utility" `
        -CandidateIndexes $startFieldIndexes `
        -EditorUse "Use L43/L186 to orient the central start-to-castle route; both are treasure-class markers." `
        -ValidationAction "Capture nearby start-field treasure/container samples to split gems from helpers.")
    (New-LocationRow `
        -Name "Dry well / Gavin / locked chest" `
        -PublicLocation "Low-west well pocket near the starting area." `
        -ExpectedTargets @("Gavin", "locked chest", "well-side helper records") `
        -Status "dense route candidate group" `
        -Confidence "weak route fit" `
        -CandidateIndexes $wellIndexes `
        -EditorUse "Use L95/L183 for well and locked-chest experiments; keep late-table well-side markers in the diagnostic row." `
        -ValidationAction "Capture Gavin-free and locked-chest-open RAM pairs separately.")
    (New-LocationRow `
        -Name "Castle / Return Home / Astor" `
        -PublicLocation "Middle castle and Return Home side, including the Astor route and beach drop." `
        -ExpectedTargets @("Astor", "Return Home portal-side helpers", "portal-side treasure") `
        -Status "usable route bucket" `
        -Confidence "weak named target fit" `
        -CandidateIndexes $castleIndexes `
        -EditorUse "Use L65 only as a tree/scenery source test; capture Astor-free RAM before assigning a dragon marker." `
        -ValidationAction "Capture Astor-free RAM before touching nearby treasure or exit helpers.")
    (New-LocationRow `
        -Name "Hidden beach / key cave" `
        -PublicLocation "Low-east beach/cave route below or behind the Return Home side." `
        -ExpectedTargets @("hidden beach key", "beach cave treasure", "beach route helpers") `
        -Status "route candidates only" `
        -Confidence "weak route fit" `
        -CandidateIndexes $keyIndexes `
        -EditorUse "No non-sparse beach/key marker is available yet; keep L421/L422 as late-table diagnostics only." `
        -ValidationAction "Capture a key-pickup RAM pair and then rerun source-link discovery.")
    (New-LocationRow `
        -Name "Left cave / Lindar route" `
        -PublicLocation "Cave-side route with Lindar, cave treasure, and nearby chests/enemies." `
        -ExpectedTargets @("Lindar", "cave gems", "cave chests", "nearby enemies") `
        -Status "shared cave/tower candidate group" `
        -Confidence "weak route fit" `
        -CandidateIndexes $leftCaveIndexes `
        -EditorUse "Use L54 only as a tree/scenery source test; L109/L120 are live-tested chest markers for cave-route functional tests." `
        -ValidationAction "Capture Lindar-free plus local cave treasure samples.")
    (New-LocationRow `
        -Name "Tower / Gildas route" `
        -PublicLocation "Tower-side highland route leading toward Gildas and the high fields." `
        -ExpectedTargets @("Gildas", "tower-side enemies", "route treasure") `
        -Status "shared cave/tower candidate group" `
        -Confidence "weak route fit" `
        -CandidateIndexes $towerIndexes `
        -EditorUse "Do not use L54/L87/L142/L153 as dragon/enemy proof; live validation made them tree/red-gem leads." `
        -ValidationAction "Capture Gildas-free and one nearby enemy-defeat RAM pair.")
    (New-LocationRow `
        -Name "High fields / blue thief loop" `
        -PublicLocation "High open fields around the thief loop, enemies, and hilltop treasure." `
        -ExpectedTargets @("blue egg thief", "rams", "shepherds", "hilltop treasure", "high-field helpers") `
        -Status "usable route bucket" `
        -Confidence "weak route fit, good marker coverage" `
        -CandidateIndexes $highFieldIndexes `
        -EditorUse "Use L87 and L142 for thief/enemy tests; L10/L21/L32/L131 are treasure-class high-field markers." `
        -ValidationAction "Capture blue-thief, ram, shepherd, and hilltop treasure RAM pairs separately.")
    (New-LocationRow `
        -Name "Late-table / outlier lead" `
        -PublicLocation "Sparse records after the first long invalid runtime-table gap." `
        -ExpectedTargets @("unverified late-table record") `
        -Status "do not treat as replica placement yet" `
        -Confidence "weak outlier" `
        -CandidateIndexes $lateTableIndexes `
        -EditorUse "Keep these visible as diagnostics only until a fresh RAM dump confirms one of them." `
        -ValidationAction "Verify in another Stone Hill RAM dump before moving or patch-testing.")
    (New-LocationRow `
        -Name "Placeholder/helper records" `
        -PublicLocation "Runtime placeholder coordinate bucket, probably not visible Stone Hill placement." `
        -ExpectedTargets @("internal helpers", "state/system records") `
        -Status "not a visible location" `
        -Confidence "weak non-placement" `
        -CandidateIndexes $helperIndexes `
        -EditorUse "Ignore L7/L18/L106/L161 for replica placement unless a focused diff proves gameplay-visible behavior." `
        -ValidationAction "Only revisit if a progress or state sample points directly at one of these records.")
)

$coverage = Get-Field $plan "coverage" $null
$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Stone Hill target board for deciding which visible editor moby corresponds to each public item, enemy, dragon, or route objective."
    sourcePlan = $resolvedPlan.Path
    publicSources = @(
        "https://spyrowiki.com/wiki/Stone_Hill",
        "https://strategywiki.org/wiki/Spyro_the_Dragon/Stone_Hill",
        "https://www.gamepur.com/guides/spyro-reignited-stone-hill-key-location"
    )
    liveOverridesPath = $(if ($null -ne $liveOverrides) { (Resolve-Path -LiteralPath $LiveOverridesPath).Path } else { $null })
    currentEditorReadiness = [ordered]@{
        mapWorkbench = "Load Stone Hill Workbench shows the current map proxy plus runtime moby markers."
        movableRuntimeMarkers = [int](Get-Field $coverage "decodedRuntimeMobys" $markers.Count)
        placementRuntimeMarkers = [int](Get-Field $coverage "placementRuntimeMobys" 0)
        scaledSourceLeadMarkers = [int](Get-Field $coverage "mobysWithScaledSourceLeads" 0)
        strongIdentity = $(if ($confirmedGemIndexes.Count -gt 0) { "Treasure/gem class plus live-tested red gems, sheep, trees, and chest visuals. Named dragons, enemies, key, and locked chest remain unmapped until focused pairs." } else { "Treasure/gem class plus live-tested overrides. Named dragons, enemies, key, and locked chest remain unmapped until focused pairs." })
    }
    publicRouteGraph = $routeGraph
    locations = $locationRows
    targets = $targetRows
    priorityOrder = @(
        "Patch-test L186 first because it is the collected-gem sample; then test L109 and L87 with Queue SH Leads on disposable BINs.",
        "Capture Astor/Lindar/Gildas/Gavin dragon-freeing RAM pairs; current 0x30 live-tested records are trees, not dragons.",
        "Capture thief/key/locked-chest RAM pairs to split the high-field and well/beach route candidate groups.",
        "After each new RAM pair, rerun Compare-SpyroRamMobys.ps1, Render-StoneHillMobyMap.ps1, Find-StoneHillMobySourceLinks.ps1, New-StoneHillReplicaMapPlan.ps1, and this target board."
    )
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$result | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# Stone Hill Replica Target Board")
[void]$md.Add("")
[void]$md.Add("Generated: $($result.generatedAt)")
[void]$md.Add("")
[void]$md.Add("This board converts the current Stone Hill workbench evidence into editor actions. `L#` means the runtime moby marker index shown in the editor.")
[void]$md.Add("")
[void]$md.Add("## Public Route Graph")
foreach ($route in $routeGraph) {
    $label = [string](Get-Field $route "label" "")
    $role = [string](Get-Field $route "routeRole" "")
    $indexes = Format-Indexes (Get-ArrayField $route "currentMarkerIndexes")
    $targets = (@(Get-ArrayField $route "expectedTargets") -join ", ")
    [void]$md.Add("- ${label}: $role. Expected targets: $targets. Current markers: $indexes.")
}
[void]$md.Add("")
[void]$md.Add("## Location Ledger")
[void]$md.Add("")
[void]$md.Add("| Location | Expected targets | Candidate mobys | Source leads | Validation |")
[void]$md.Add("| --- | --- | --- | --- | --- |")
foreach ($row in $locationRows) {
    $locationName = [string](Get-Field $row "locationName" "")
    $targets = (@(Get-ArrayField $row "expectedTargets") -join ", ").Replace("|", "/")
    $candidateText = Format-Indexes (Get-ArrayField $row "candidateIndexes")
    $sourceText = [string](Get-Field $row "sourceLeads" "none")
    $validation = ([string](Get-Field $row "validationAction" "")).Replace("|", "/")
    [void]$md.Add("| $locationName | $targets | $candidateText | $sourceText | $validation |")
}
[void]$md.Add("")
[void]$md.Add("| Target | Status | Candidate mobys | Source leads | What to try next |")
[void]$md.Add("| --- | --- | --- | --- | --- |")
foreach ($row in $targetRows) {
    $candidateText = Format-Indexes (Get-ArrayField $row "candidateIndexes")
    $related = Format-Indexes (Get-ArrayField $row "relatedRouteIndexes")
    if ($related -ne "none") { $candidateText = "$candidateText (related: $related)" }
    $sourceText = [string](Get-Field $row "sourceLeads" "none")
    $action = ([string](Get-Field $row "editorAction" "")).Replace("|", "/")
    $targetName = [string](Get-Field $row "targetName" "")
    $status = [string](Get-Field $row "status" "")
    [void]$md.Add("| $targetName | $status | $candidateText | $sourceText | $action |")
}
[void]$md.Add("")
[void]$md.Add("## Priority")
foreach ($item in @(Get-ArrayField $result "priorityOrder")) {
    [void]$md.Add("- $item")
}
[void]$md.Add("")
[void]$md.Add("## Sources")
foreach ($source in @(Get-ArrayField $result "publicSources")) {
    [void]$md.Add("- $source")
}

$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
$md | Set-Content -LiteralPath $resolvedMarkdown -Encoding UTF8

Write-Host "Wrote Stone Hill target board JSON to $resolvedJson"
Write-Host "Wrote Stone Hill target board Markdown to $resolvedMarkdown"
Write-Host "Targets: $($targetRows.Count)"
