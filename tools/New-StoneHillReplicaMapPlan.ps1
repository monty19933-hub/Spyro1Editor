param(
    [string]$CatalogPath = ".\stonehill-moby-catalog.json",
    [string]$SourceLinksPath = ".\stonehill-moby-source-links.json",
    [string]$SourceLayoutsPath = ".\stonehill-source-window-layouts.json",
    [string]$OutPath = ".\stonehill-replica-map-plan.json"
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

function Test-PlaceholderMoby($Moby) {
    return ([int](Get-Field $Moby "rawX" 0) -eq 0 -and
        [int](Get-Field $Moby "rawY" 0) -eq 4096 -and
        [int](Get-Field $Moby "rawZ" 0) -eq 0)
}

function Get-PlacementMobys($Mobys) {
    return @($Mobys | Where-Object { -not (Test-PlaceholderMoby $_) })
}

function Get-Bounds($Mobys) {
    $xs = @($Mobys | ForEach-Object { [double](Get-Field $_ "x" 0) })
    $ys = @($Mobys | ForEach-Object { [double](Get-Field $_ "y" 0) })
    if ($xs.Count -eq 0 -or $ys.Count -eq 0) {
        return [ordered]@{ minX = 0; maxX = 0; minY = 0; maxY = 0; width = 0; height = 0 }
    }
    $minX = ($xs | Measure-Object -Minimum).Minimum
    $maxX = ($xs | Measure-Object -Maximum).Maximum
    $minY = ($ys | Measure-Object -Minimum).Minimum
    $maxY = ($ys | Measure-Object -Maximum).Maximum
    return [ordered]@{
        minX = [Math]::Round([double]$minX, 2)
        maxX = [Math]::Round([double]$maxX, 2)
        minY = [Math]::Round([double]$minY, 2)
        maxY = [Math]::Round([double]$maxY, 2)
        width = [Math]::Round([double]($maxX - $minX), 2)
        height = [Math]::Round([double]($maxY - $minY), 2)
    }
}

function Get-ZoneLabel($Moby, $Bounds) {
    if (Test-PlaceholderMoby $Moby) { return "placeholder/helper position" }

    $x = [double](Get-Field $Moby "x" 0)
    $y = [double](Get-Field $Moby "y" 0)
    $width = [Math]::Max(1.0, [double]$Bounds.width)
    $height = [Math]::Max(1.0, [double]$Bounds.height)
    $nx = ($x - [double]$Bounds.minX) / $width
    $ny = ($y - [double]$Bounds.minY) / $height

    $horizontal = if ($nx -lt 0.33) { "west" } elseif ($nx -gt 0.66) { "east" } else { "central" }
    $vertical = if ($ny -lt 0.30) { "low-map" } elseif ($ny -gt 0.70) { "high-map" } else { "mid-map" }
    return "$vertical/$horizontal runtime cluster"
}

function Get-RoleLabel($Moby) {
    $typeHex = [string](Get-Field $Moby "typeHex" "")
    $kind = [string](Get-Field $Moby "candidateKind" "unidentified moby")
    $confidence = [string](Get-Field $Moby "confidence" "unknown")
    switch ($typeHex) {
        "0x20" { return "gem/collectible class, confirmed by one gem pickup RAM diff" }
        "0x30" { return "dragon/NPC candidate, weak until a dragon-freeing RAM diff" }
        "0x18" { return "enemy/active-object candidate, weak until enemy/container RAM diffs" }
        default {
            if ($kind -like "*helper*" -or $kind -like "*sparse*") { return "$kind, $confidence" }
            return "$kind, $confidence; possible key/chest/fodder/container/helper until sampled"
        }
    }
}

function Get-ZonePublicRouteLabel([string]$ZoneLabel) {
    switch -Wildcard ($ZoneLabel) {
        "low-map/west*" { return "start/well-side route lead: dry well, Gavin, locked chest, or nearby start-area records" }
        "low-map/east*" { return "beach/hidden-cave route lead: key cave and beach treasure area" }
        "mid-map/east*" { return "castle/return-portal side lead: Astor or exit-side objects" }
        "mid-map/central*" { return "middle castle/interior route lead: transition objects and nearby treasure" }
        "high-map/central*" { return "tower/cave-side highland lead: Gildas/Lindar route objects and nearby enemies/gems" }
        "high-map/east*" { return "high open fields lead: blue thief route, rams/shepherds, and hilltop treasure" }
        "high-map/west*" { return "outlier/sparse high-map lead; verify before treating as Stone Hill geometry" }
        "placeholder/helper*" { return "placeholder/helper record; probably not a direct public target until proven by RAM diffs" }
        default { return "unassigned public-route lead; needs focused in-game sample" }
    }
}

function Get-PublicTargetHints($Moby, [string]$ZoneLabel) {
    $typeHex = [string](Get-Field $Moby "typeHex" "")
    $index = [int](Get-Field $Moby "index" -1)
    $hints = New-Object System.Collections.Generic.List[string]

    switch ($typeHex) {
        "0x20" {
            [void]$hints.Add("Strong item-class hint: likely gem/collectible. Use for treasure layout, but individual gem/container source identity is not complete.")
        }
        "0x30" {
            [void]$hints.Add("Weak dragon/NPC-class hint. Candidate for one of Lindar, Gildas, Astor, or Gavin; needs a before/after dragon-freeing RAM sample.")
            if ($ZoneLabel -like "high-map/central*") {
                [void]$hints.Add("Route fit: tower/cave-side dragon lead, most relevant to Gildas or Lindar until sampled.")
            }
            elseif ($ZoneLabel -like "mid-map/east*") {
                [void]$hints.Add("Route fit: return-portal/castle-side dragon lead, most relevant to Astor until sampled.")
            }
        }
        "0x18" {
            [void]$hints.Add("Weak enemy/active-object hint. Compare focused ram, shepherd, blue-thief, and chest/enemy pickup samples.")
        }
        "0x10" {
            [void]$hints.Add("Unknown placed record. Candidate bucket for container/key/sign/portal/fodder-style records until a focused diff identifies it.")
        }
        "0x5C" {
            [void]$hints.Add("Unknown placed record near public-route clusters. Check against key, locked chest, portal, or container samples.")
        }
        "0x04" {
            [void]$hints.Add("Unknown placed record near the low-map route. Check against well/start-area or key/chest samples.")
        }
    }

    if ($ZoneLabel -notlike "placeholder/helper*") {
        [void]$hints.Add((Get-ZonePublicRouteLabel $ZoneLabel))
    }
    elseif ($index -ge 0) {
        [void]$hints.Add("Do not use L#$index as a replica placement target until a focused RAM diff proves it is gameplay-visible.")
    }

    return $hints.ToArray()
}

function Get-PublicTargetGuess($Moby, [string]$ZoneLabel, $SourceLead) {
    $typeHex = [string](Get-Field $Moby "typeHex" "")
    $kind = [string](Get-Field $Moby "candidateKind" "unidentified moby")
    $diffEvidenceRole = [string](Get-Field $Moby "diffEvidenceRole" "")
    $sourceStatus = if ($null -ne $SourceLead) { [string](Get-Field $SourceLead "status" "no source lead") } else { "no source lead" }
    $hasSourceWindow = ($null -ne $SourceLead -and [int](Get-Field $SourceLead "scaledWindowCount" 0) -gt 0)

    $guess = [ordered]@{
        label = "Unassigned Stone Hill placement lead"
        confidence = "weak-route-fit"
        publicTarget = "unknown item/enemy/helper"
        rationale = "Runtime position fits a public route zone, but no focused RAM diff has identified this moby yet."
        sourceStatus = $sourceStatus
        nextValidation = "Capture a focused before/after RAM sample near this route, then rerun the catalog/source-link pipeline."
    }

    if ($ZoneLabel -like "placeholder/helper*") {
        $guess.label = "Helper/system record, not a replica target yet"
        $guess.confidence = "weak-nonplacement"
        $guess.publicTarget = "internal helper or state record"
        $guess.rationale = "The runtime position is the placeholder 0,4096,0 pattern, so it should not drive a visible replica placement until sampled."
        $guess.nextValidation = "Ignore for placement unless a focused RAM diff shows gameplay-visible state changes."
        return $guess
    }

    if ($kind -like "*sparse*") {
        $guess.label = "Sparse table outlier"
        $guess.confidence = "weak-outlier"
        $guess.publicTarget = "unverified late-table record"
        $guess.rationale = "This appears after a long invalid table gap and needs in-game proof before being treated as Stone Hill content."
        $guess.nextValidation = "Verify in a fresh RAM dump before moving or patch-testing it."
        return $guess
    }

    switch ($typeHex) {
        "0x20" {
            if ($diffEvidenceRole -eq "collected-gem-sample") {
                $guess.label = "Collected gem sample"
                $guess.confidence = "confirmed-sample"
                $guess.publicTarget = "specific Stone Hill gem collected in the gem-clean sample"
                $guess.rationale = "This type 0x20 moby is the only treasure-class record with a state change in the isolated gem-clean RAM pair while globalGemCount increased."
                $guess.nextValidation = $(if ($hasSourceWindow) { "Patch-test this marker's source window on a disposable BIN to prove the disc placement encoder for this specific gem." } else { "Collect another adjacent treasure sample or search nearby source bytes because this marker has no current scaled source window." })
            }
            else {
                $guess.label = "Treasure/gem placement candidate"
                $guess.confidence = "strong-class"
                $guess.publicTarget = "part of Stone Hill 200 treasure total"
                $guess.rationale = "Type 0x20 is confirmed by the available gem-pickup RAM diff; exact gem/container identity still needs more samples."
                $guess.nextValidation = $(if ($hasSourceWindow) { "Patch-test the best scaled source window on a disposable BIN, or collect adjacent treasure samples to name the individual pickup." } else { "Collect adjacent treasure samples; this moby has no current scaled source window." })
            }
            return $guess
        }
        "0x30" {
            $guess.publicTarget = "one of Lindar, Gildas, Astor, or Gavin"
            $guess.confidence = "weak-class-route"
            if ($ZoneLabel -like "mid-map/east*") {
                $guess.label = "Astor route dragon/NPC candidate"
                $guess.rationale = "The 0x30 class is the current dragon/NPC lead and this marker sits in the return-portal route zone."
                $guess.nextValidation = "Capture a before/after sample freeing Astor near Return Home."
            }
            elseif ($ZoneLabel -like "high-map/central*") {
                $guess.label = "Lindar/Gildas route dragon/NPC candidate"
                $guess.rationale = "The 0x30 class is the current dragon/NPC lead and this marker sits in the cave/tower highland zone."
                $guess.nextValidation = "Capture separate before/after samples freeing Lindar in the cave and Gildas at the tower."
            }
            elseif ($ZoneLabel -like "low-map/west*") {
                $guess.label = "Gavin/well route dragon/NPC candidate"
                $guess.rationale = "The 0x30 class is the current dragon/NPC lead and this marker sits near the start/well zone."
                $guess.nextValidation = "Capture a before/after sample freeing Gavin in the dry well."
            }
            else {
                $guess.label = "Dragon/NPC candidate"
                $guess.rationale = "The 0x30 class is the current dragon/NPC lead, but the route fit is not specific enough to name it."
                $guess.nextValidation = "Capture named dragon-freeing samples to assign this record."
            }
            return $guess
        }
        "0x18" {
            $guess.confidence = "weak-class-route"
            if ($ZoneLabel -like "high-map/east*") {
                $guess.label = "High-field enemy/thief route candidate"
                $guess.publicTarget = "blue thief, ram, shepherd, or nearby active object"
                $guess.rationale = "Type 0x18 is the current active-object/enemy lead and this marker sits in the high-field thief/enemy route zone."
                $guess.nextValidation = "Capture focused samples for catching the blue thief, defeating one ram, and defeating one shepherd."
            }
            elseif ($ZoneLabel -like "high-map/central*") {
                $guess.label = "Tower/cave enemy route candidate"
                $guess.publicTarget = "ram, shepherd, or nearby active object"
                $guess.rationale = "Type 0x18 is the current active-object/enemy lead and this marker sits by the tower/cave route."
                $guess.nextValidation = "Capture a local enemy defeat sample on this route."
            }
            else {
                $guess.label = "Enemy/active-object candidate"
                $guess.publicTarget = "enemy, thief, chest, or trigger-class record"
                $guess.rationale = "Type 0x18 is repeated among placed active records, but the exact family is not decoded yet."
                $guess.nextValidation = "Capture an action-specific RAM diff for the nearest visible object."
            }
            return $guess
        }
    }

    if ($ZoneLabel -like "low-map/east*") {
        $guess.label = "Hidden beach/key-cave route candidate"
        $guess.publicTarget = "key, beach treasure, cave chest, or route helper"
        $guess.rationale = "Public guides place the key and beach cave behind the Return Home area; this marker sits in that low/east route cluster."
        $guess.nextValidation = "Capture samples for picking up the key and opening nearby cave treasure."
    }
    elseif ($ZoneLabel -like "low-map/west*") {
        $guess.label = "Start/well/locked-chest route candidate"
        $guess.publicTarget = "Gavin well area, locked chest, start-area chest, or helper"
        $guess.rationale = "Public guides place Gavin and the locked chest in the dry well; this marker sits in the low/west start-side cluster."
        $guess.nextValidation = "Capture samples for opening the well locked chest and freeing Gavin."
    }
    elseif ($ZoneLabel -like "mid-map/east*") {
        $guess.label = "Return-portal/castle route candidate"
        $guess.publicTarget = "Astor route, Return Home portal, chest, or helper"
        $guess.rationale = "This marker sits in the route zone associated with the castle exit and Return Home portal."
        $guess.nextValidation = "Capture Astor and Return Home route samples."
    }
    elseif ($ZoneLabel -like "high-map/east*") {
        $guess.label = "High-field route candidate"
        $guess.publicTarget = "blue thief loop, rams, shepherds, sheep, or hilltop treasure"
        $guess.rationale = "This marker sits in the high open fields where public references place the thief loop and enemies."
        $guess.nextValidation = "Capture thief, ram, shepherd, sheep, and hilltop treasure samples."
    }
    elseif ($ZoneLabel -like "high-map/central*") {
        $guess.label = "Tower/cave-side route candidate"
        $guess.publicTarget = "Lindar/Gildas route, cave treasure, tower object, or enemy"
        $guess.rationale = "This marker sits in the high central route associated with the cave and tower."
        $guess.nextValidation = "Capture Lindar, Gildas, and nearby cave/tower treasure samples."
    }

    return $guess
}

function Get-PublicTargetCandidates($Moby, [string]$ZoneLabel, $SourceLead, $PublicTargetGuess) {
    $typeHex = [string](Get-Field $Moby "typeHex" "")
    $diffEvidenceRole = [string](Get-Field $Moby "diffEvidenceRole" "")
    $hasSourceWindow = ($null -ne $SourceLead -and [int](Get-Field $SourceLead "scaledWindowCount" 0) -gt 0)
    $sourceText = $(if ($hasSourceWindow) { "has scaled source-window lead" } else { "no scaled source-window lead yet" })
    $candidates = New-Object System.Collections.Generic.List[object]

    function Add-Candidate([string]$Name, [string]$Category, [string]$Confidence, [string]$Reason) {
        [void]$candidates.Add([ordered]@{
            name = $Name
            category = $Category
            confidence = $Confidence
            reason = "$Reason; $sourceText"
        })
    }

    if ($ZoneLabel -like "placeholder/helper*") {
        Add-Candidate "Internal helper/state record" "helper" "weak-nonplacement" "Placeholder runtime coordinates are not a visible placement target"
        return $candidates.ToArray()
    }

    $kind = [string](Get-Field $Moby "candidateKind" "")
    if ($kind -like "*sparse*") {
        Add-Candidate "unverified late-table record" "helper" "weak-outlier" "Record appears after a long invalid runtime-table gap and may be RAM noise"
        return $candidates.ToArray()
    }

    switch ($typeHex) {
        "0x20" {
            if ($diffEvidenceRole -eq "collected-gem-sample") {
                Add-Candidate "Collected Stone Hill gem" "treasure" "confirmed-sample" "This specific type 0x20 record changed state when globalGemCount increased in the gem-clean RAM pair"
            }
            else {
                Add-Candidate "Stone Hill treasure/gem" "treasure" "strong-class" "Type 0x20 is confirmed by the gem-pickup RAM diff"
            }
            return $candidates.ToArray()
        }
        "0x30" {
            if ($ZoneLabel -like "mid-map/east*") {
                Add-Candidate "Astor" "dragon" "weak-route-fit" "Only named dragon whose public route is the Return Home side"
            }
            elseif ($ZoneLabel -like "low-map/west*") {
                Add-Candidate "Gavin" "dragon" "weak-route-fit" "Public route places Gavin in the dry well/start-side area"
            }
            elseif ($ZoneLabel -like "high-map/central*") {
                Add-Candidate "Lindar" "dragon" "weak-route-fit" "Cave/tower-side zone can cover Lindar's cave route"
                Add-Candidate "Gildas" "dragon" "weak-route-fit" "Cave/tower-side zone can cover Gildas's tower route"
            }
            else {
                Add-Candidate "Unassigned Stone Hill dragon" "dragon" "weak-class" "Type 0x30 is the current dragon/NPC class lead"
            }
            return $candidates.ToArray()
        }
        "0x18" {
            if ($ZoneLabel -like "high-map/east*") {
                Add-Candidate "Blue thief" "enemy" "weak-route-fit" "Public route places the egg thief in the high open fields"
                Add-Candidate "Ram" "enemy" "weak-route-fit" "High-field active records may include rams"
                Add-Candidate "Shepherd" "enemy" "weak-route-fit" "High-field active records may include shepherds"
            }
            elseif ($ZoneLabel -like "high-map/central*") {
                Add-Candidate "Ram or shepherd" "enemy" "weak-route-fit" "Tower/cave-side active records are plausible enemy records"
            }
            else {
                Add-Candidate "Enemy or active object" "enemy" "weak-class" "Type 0x18 is the current active-object/enemy lead"
            }
            return $candidates.ToArray()
        }
    }

    if ($ZoneLabel -like "low-map/east*") {
        Add-Candidate "Hidden beach key" "item" "weak-route-fit" "Public route places the key in the hidden beach/cave route"
        Add-Candidate "Beach/cave treasure or helper" "item" "weak-route-fit" "Low/east marker sits in the hidden beach route cluster"
    }
    elseif ($ZoneLabel -like "low-map/west*") {
        Add-Candidate "Locked chest or well helper" "container" "weak-route-fit" "Public route places the locked chest and Gavin in the dry well"
        Add-Candidate "Start/well route object" "helper" "weak-route-fit" "Marker sits in the start/well-side runtime cluster"
    }
    elseif ($ZoneLabel -like "high-map/east*") {
        Add-Candidate "Blue thief loop or hilltop object" "enemy" "weak-route-fit" "Marker sits in the public high-field route"
    }
    elseif ($ZoneLabel -like "mid-map/east*") {
        Add-Candidate "Return Home or Astor route object" "helper" "weak-route-fit" "Marker sits near the public Return Home route"
    }
    elseif ($ZoneLabel -like "mid-map/central*") {
        Add-Candidate "Castle/interior treasure or helper" "item" "weak-route-fit" "Marker sits in the central castle/interior route"
    }
    elseif ($ZoneLabel -like "high-map/central*") {
        Add-Candidate "Tower/cave route object" "helper" "weak-route-fit" "Marker sits in the cave/tower-side route"
    }

    if ($candidates.Count -eq 0) {
        $fallback = [string](Get-Field $PublicTargetGuess "publicTarget" "unknown Stone Hill object")
        Add-Candidate $fallback "unknown" "weak-route-fit" "Best available route bucket from the map plan"
    }

    return $candidates.ToArray()
}

function Get-MarkerDisplayTargetLabel($Candidates, $PublicTargetGuess) {
    $candidateNames = @($Candidates | ForEach-Object { [string](Get-Field $_ "name" "") } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($candidateNames.Count -eq 0) {
        $guessLabel = [string](Get-Field $PublicTargetGuess "label" "")
        if (-not [string]::IsNullOrWhiteSpace($guessLabel)) { return $guessLabel }
        return "Stone Hill target?"
    }
    if ($candidateNames -contains "Stone Hill treasure/gem") { return "Treasure/gem" }
    if ($candidateNames -contains "Astor") { return "Astor?" }
    if ($candidateNames -contains "Gavin") { return "Gavin?" }
    if ($candidateNames -contains "Lindar" -and $candidateNames -contains "Gildas") { return "Lindar/Gildas?" }
    if ($candidateNames -contains "Blue thief") { return "Blue thief/enemy?" }
    if ($candidateNames -contains "unverified late-table record") { return "Late-table?" }
    if ($candidateNames -contains "Hidden beach key") { return "Key/beach?" }
    if ($candidateNames -contains "Locked chest or well helper") { return "Well/chest?" }
    if ($candidateNames -contains "Castle/interior treasure or helper") { return "Castle/interior?" }
    if ($candidateNames -contains "Blue thief loop or hilltop object") { return "Hilltop/thief?" }
    if ($candidateNames -contains "Enemy or active object") { return "Enemy/object?" }
    if ($candidateNames -contains "Internal helper/state record") { return "Helper/system" }
    return $candidateNames[0]
}

function Get-SourcePatchPriority($Moby, $SourceLead, $PublicTargetGuess) {
    if ($null -eq $SourceLead -or [int](Get-Field $SourceLead "scaledWindowCount" 0) -le 0) {
        return [ordered]@{
            priority = "none"
            rationale = "No scaled source window is attached to this marker."
        }
    }

    $typeHex = [string](Get-Field $Moby "typeHex" "")
    $confidence = [string](Get-Field $PublicTargetGuess "confidence" "")
    $priority = "medium"
    if ($typeHex -eq "0x20" -or $typeHex -eq "0x18" -or $typeHex -eq "0x30") { $priority = "high" }
    if ($confidence -like "*nonplacement*" -or $confidence -like "*outlier*") { $priority = "low" }

    return [ordered]@{
        priority = $priority
        rationale = "Use Queue SH Leads only as a disposable-BIN validation pass; the lead is still a scaled-int16 source-window hypothesis."
    }
}

function Get-RecordByIndex($SourceLinks) {
    $map = @{}
    if ($null -eq $SourceLinks) { return $map }
    foreach ($record in @(Get-ArrayField $SourceLinks "records")) {
        $index = [int](Get-Field $record "index" -1)
        if ($index -ge 0 -and -not $map.ContainsKey($index)) { $map[$index] = $record }
    }
    return $map
}

function Convert-SourceLead($Record) {
    if ($null -eq $Record) { return $null }
    $windows = @(Get-ArrayField $Record "scaledAxisWindows")
    $bestWindow = @($windows | Select-Object -First 1)
    $exactMatches = @(Get-ArrayField $Record "exactMatches" | Where-Object { [int](Get-Field $_ "count" 0) -gt 0 })
    return [ordered]@{
        status = $(if ($windows.Count -gt 0) { "scaled-int16 source-window lead" } elseif ($exactMatches.Count -gt 0) { "exact coordinate lead only" } else { "no source lead" })
        scaledWindowCount = $windows.Count
        exactMatchKinds = @($exactMatches | ForEach-Object { "$([string](Get-Field $_ 'kind' '')):$([int](Get-Field $_ 'count' 0))" })
        bestScaledWindow = $(if ($bestWindow.Count -gt 0) {
            [ordered]@{
                assetRelativeWindow = [string](Get-Field $bestWindow[0] "assetRelativeWindow" "")
                wadRelativeWindow = [string](Get-Field $bestWindow[0] "wadRelativeWindow" "")
                assetSubfileIndex = [int](Get-Field $bestWindow[0] "assetSubfileIndex" -1)
                axisCount = [int](Get-Field $bestWindow[0] "axisCount" 0)
                spread = [int](Get-Field $bestWindow[0] "spread" 0)
                values = @(Get-ArrayField $bestWindow[0] "values")
                typeByteOffsets = @(Get-ArrayField $bestWindow[0] "typeByteOffsets")
                stateByteOffsets = @(Get-ArrayField $bestWindow[0] "stateByteOffsets")
            }
        } else { $null })
    }
}

function Convert-TopLead($Lead) {
    return [ordered]@{
        score = [int](Get-Field $Lead "score" 0)
        index = [int](Get-Field $Lead "index" -1)
        typeHex = [string](Get-Field $Lead "typeHex" "")
        kindBucket = [string](Get-Field $Lead "kindBucket" "")
        wadRelativeWindow = [string](Get-Field $Lead "wadRelativeWindow" "")
        assetRelativeWindow = [string](Get-Field $Lead "assetRelativeWindow" "")
        assetSubfileIndex = [int](Get-Field $Lead "assetSubfileIndex" -1)
        axisSet = [string](Get-Field $Lead "axisSet" "")
        spread = [int](Get-Field $Lead "spread" 0)
        values = @(Get-ArrayField $Lead "values")
        note = "Patch-test only on a disposable BIN; these are source-window leads, not verified object records."
    }
}

$catalog = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $CatalogPath).Path | ConvertFrom-Json
$sourceLinks = $null
if (Test-Path -LiteralPath $SourceLinksPath) {
    $sourceLinks = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $SourceLinksPath).Path | ConvertFrom-Json
}
$sourceLayouts = $null
if (Test-Path -LiteralPath $SourceLayoutsPath) {
    $sourceLayouts = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $SourceLayoutsPath).Path | ConvertFrom-Json
}

$mobys = @(Get-ArrayField $catalog "mobys")
$placementMobys = @(Get-PlacementMobys $mobys)
$bounds = Get-Bounds $placementMobys
$sourceByIndex = Get-RecordByIndex $sourceLinks

$markers = @()
foreach ($moby in ($mobys | Sort-Object { [int](Get-Field $_ "index" 0) })) {
    $index = [int](Get-Field $moby "index" -1)
    $sourceRecord = $null
    if ($sourceByIndex.ContainsKey($index)) { $sourceRecord = $sourceByIndex[$index] }
    $sourceLead = Convert-SourceLead $sourceRecord
    $zoneLabel = Get-ZoneLabel $moby $bounds
    $publicTargetGuess = Get-PublicTargetGuess $moby $zoneLabel $sourceLead
    $publicTargetCandidates = @(Get-PublicTargetCandidates $moby $zoneLabel $sourceLead $publicTargetGuess)
    $sourcePatchPriority = Get-SourcePatchPriority $moby $sourceLead $publicTargetGuess
    $markers += [ordered]@{
        index = $index
        runtimeAddress = [string](Get-Field $moby "runtimeAddress" "")
        typeHex = [string](Get-Field $moby "typeHex" "")
        stateHex = [string](Get-Field $moby "stateHex" "")
        mapX = [Math]::Round([double](Get-Field $moby "x" 0), 2)
        mapY = [Math]::Round([double](Get-Field $moby "y" 0), 2)
        height = [Math]::Round([double](Get-Field $moby "z" 0), 2)
        rawX = [int](Get-Field $moby "rawX" 0)
        rawY = [int](Get-Field $moby "rawY" 0)
        rawZ = [int](Get-Field $moby "rawZ" 0)
        zoneLabel = $zoneLabel
        workingRole = Get-RoleLabel $moby
        publicTargetGuess = $publicTargetGuess
        publicTargetCandidates = $publicTargetCandidates
        displayTargetLabel = Get-MarkerDisplayTargetLabel $publicTargetCandidates $publicTargetGuess
        publicTargetHints = @(Get-PublicTargetHints $moby $zoneLabel)
        confidence = [string](Get-Field $moby "confidence" "unknown")
        candidateKind = [string](Get-Field $moby "candidateKind" "unidentified moby")
        evidence = [string](Get-Field $moby "evidence" "")
        changedInGemDiff = [bool](Get-Field $moby "changedInDiff" $false)
        diffEvidenceRole = [string](Get-Field $moby "diffEvidenceRole" "")
        diffEvidenceConfidence = [string](Get-Field $moby "diffEvidenceConfidence" "")
        diffEvidenceNote = [string](Get-Field $moby "diffEvidenceNote" "")
        sourcePatchPriority = $sourcePatchPriority
        sourceLead = $sourceLead
    }
}

$publicTargetCandidateLedger = @($markers |
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

$zoneSummary = @($markers |
    Group-Object { [string](Get-Field $_ "zoneLabel" "") } |
    Sort-Object Name |
    ForEach-Object {
        [ordered]@{
            zoneLabel = $_.Name
            mobys = $_.Count
            indexes = @($_.Group | ForEach-Object { [int](Get-Field $_ "index" -1) })
            types = @($_.Group | ForEach-Object { [string](Get-Field $_ "typeHex" "") } | Sort-Object -Unique)
            sourceLeadMobys = @($_.Group | Where-Object { $null -ne $_.sourceLead -and [int]$_.sourceLead.scaledWindowCount -gt 0 }).Count
        }
    })

$zonePublicRouteHypotheses = @($zoneSummary | ForEach-Object {
    [ordered]@{
        zoneLabel = [string](Get-Field $_ "zoneLabel" "")
        publicRouteHypothesis = Get-ZonePublicRouteLabel ([string](Get-Field $_ "zoneLabel" ""))
        mobys = [int](Get-Field $_ "mobys" 0)
        indexes = @(Get-ArrayField $_ "indexes")
        types = @(Get-ArrayField $_ "types")
        confidence = $(if ([string](Get-Field $_ "zoneLabel" "") -like "placeholder/helper*") { "weak-helper" } else { "weak-route-fit" })
    }
})

$topLeads = @()
if ($null -ne $sourceLayouts) {
    $topLeads = @(Get-ArrayField $sourceLayouts "topActionableSourceLeads" | Select-Object -First 16 | ForEach-Object { Convert-TopLead $_ })
}

$publicRouteGraph = @(
    [ordered]@{
        id = "stone-hill-start-field"
        label = "Start field"
        routeRole = "Three-way hub"
        publicEvidence = "Public route descriptions place the player start beside the left cave route, middle castle/portal route, right tower route, and the dry well."
        expectedTargets = @("Gavin", "locked chest", "start-area gems", "rams/shepherds")
        currentRuntimeZones = @("low-map/west runtime cluster", "mid-map/central runtime cluster")
        currentMarkerIndexes = @($markers | Where-Object {
            [string](Get-Field $_ "candidateKind" "") -notlike "*sparse*" -and
            [string](Get-Field $_ "zoneLabel" "") -in @("low-map/west runtime cluster", "mid-map/central runtime cluster")
        } | ForEach-Object { [int](Get-Field $_ "index" -1) } | Sort-Object -Unique)
        validationCue = "A Gavin or locked-chest RAM diff should primarily change low-map/west route candidates."
    }
    [ordered]@{
        id = "stone-hill-left-cave"
        label = "Left cave route"
        routeRole = "Cave, treasure, Lindar route"
        publicEvidence = "Walkthrough sources place Lindar in a cave reached from the start-side left path, with gems and chests in that route."
        expectedTargets = @("Lindar", "cave gems", "cave chests")
        currentRuntimeZones = @("high-map/central runtime cluster")
        currentMarkerIndexes = @($markers | Where-Object {
            [string](Get-Field $_ "candidateKind" "") -notlike "*sparse*" -and
            [string](Get-Field $_ "zoneLabel" "") -eq "high-map/central runtime cluster" -and
            [string](Get-Field $_ "typeHex" "") -in @("0x20", "0x30")
        } | ForEach-Object { [int](Get-Field $_ "index" -1) } | Sort-Object -Unique)
        validationCue = "A Lindar RAM diff should separate L54 from the Gildas/tower hypothesis if 0x30 is the dragon/NPC class."
    }
    [ordered]@{
        id = "stone-hill-middle-castle"
        label = "Middle castle and Return Home route"
        routeRole = "Castle, Astor, Return Home, beach drop"
        publicEvidence = "Public guides route the middle path through the castle to Astor and Return Home, with the hidden beach/key area below or behind the exit side."
        expectedTargets = @("Astor", "Return Home portal", "hidden beach key", "beach cave treasure")
        currentRuntimeZones = @("mid-map/east runtime cluster", "low-map/east runtime cluster")
        currentMarkerIndexes = @($markers | Where-Object {
            [string](Get-Field $_ "candidateKind" "") -notlike "*sparse*" -and
            [string](Get-Field $_ "zoneLabel" "") -in @("mid-map/east runtime cluster", "low-map/east runtime cluster")
        } | ForEach-Object { [int](Get-Field $_ "index" -1) } | Sort-Object -Unique)
        validationCue = "Astor should affect the mid-map/east 0x30 candidate; key pickup should affect low-map/east route candidates."
    }
    [ordered]@{
        id = "stone-hill-right-tower"
        label = "Right tower and high-field route"
        routeRole = "Tower, Gildas, thief loop, high-field enemies"
        publicEvidence = "Public route descriptions place Gildas on the right-side tower route and the blue thief on the surrounding high fields."
        expectedTargets = @("Gildas", "blue egg thief", "rams", "shepherds", "hilltop gems")
        currentRuntimeZones = @("high-map/central runtime cluster", "high-map/east runtime cluster")
        currentMarkerIndexes = @($markers | Where-Object {
            [string](Get-Field $_ "candidateKind" "") -notlike "*sparse*" -and
            [string](Get-Field $_ "zoneLabel" "") -in @("high-map/central runtime cluster", "high-map/east runtime cluster") -and
            [string](Get-Field $_ "typeHex" "") -in @("0x18", "0x20", "0x30", "0x10", "0x5C")
        } | ForEach-Object { [int](Get-Field $_ "index" -1) } | Sort-Object -Unique)
        validationCue = "A thief sample should change egg progress plus high-map/east active-object candidates such as L87/L142."
    }
    [ordered]@{
        id = "stone-hill-dry-well"
        label = "Dry well"
        routeRole = "Gavin and locked chest pocket"
        publicEvidence = "Public guides place Gavin and the locked chest inside the dry well near the start area."
        expectedTargets = @("Gavin", "locked chest")
        currentRuntimeZones = @("low-map/west runtime cluster")
        currentMarkerIndexes = @($markers | Where-Object {
            [string](Get-Field $_ "candidateKind" "") -notlike "*sparse*" -and
            [string](Get-Field $_ "zoneLabel" "") -eq "low-map/west runtime cluster"
        } | ForEach-Object { [int](Get-Field $_ "index" -1) } | Sort-Object -Unique)
        validationCue = "Capture Gavin before/after separately from locked-chest opening so dragon and container records do not collapse into one bucket."
    }
)

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Stone Hill replica map plan for the editor: ties runtime moby markers to conservative identity labels, map clusters, and current WAD/source-window leads."
    sourceStatus = "The current map proxy uses the best generated WAD ground-model overlay plus runtime moby positions. Object identities are conservative; only type 0x20 has direct gem-pickup evidence."
    publicLayoutAnchors = [ordered]@{
        targets = [ordered]@{
            treasureValue = 200
            dragons = @(
                [ordered]@{ name = "Lindar"; publicRoute = "cave with gems and chests"; currentRuntimeEvidence = "not individually mapped" },
                [ordered]@{ name = "Gildas"; publicRoute = "tower/high-field route near the blue thief loop"; currentRuntimeEvidence = "not individually mapped" },
                [ordered]@{ name = "Astor"; publicRoute = "near the Return Home portal"; currentRuntimeEvidence = "not individually mapped" },
                [ordered]@{ name = "Gavin"; publicRoute = "dry well with the locked chest"; currentRuntimeEvidence = "not individually mapped" }
            )
            dragonEggs = 1
            enemyFamilies = @("Rams", "Shepherds", "Blue Thief")
            fodder = @("Sheep")
            keyAndLockedChest = "Key is in the hidden beach cave behind/near the Return Home portal; locked chest is in the dry well near Gavin."
        }
        sources = @(
            "https://spyrowiki.com/wiki/Stone_Hill",
            "https://spyro.fandom.com/wiki/Stone_Hill",
            "https://strategywiki.org/wiki/Spyro_the_Dragon/Stone_Hill",
            "https://spyrowiki.com/wiki/Egg_Thief",
            "https://www.gamepur.com/guides/spyro-reignited-stone-hill-key-location",
            "https://www.gamespew.com/2018/11/how-to-open-locked-chests-in-spyro-the-dragon/"
        )
        routeEdges = @(Get-ArrayField $catalog "publicRouteEdges")
        routeGraph = $publicRouteGraph
    }
    editorUse = @(
        "Load Stone Hill Workbench to see these runtime mobys over the current map proxy.",
        "Use marker index/type/sourceLead fields here to decide which moby to drag or patch-test.",
        "Treat sourceLead.bestScaledWindow as the current byte-level search lead, not a confirmed disc record."
    )
    runtimeMobyBounds = $bounds
    coverage = [ordered]@{
        decodedRuntimeMobys = [int](Get-Field $catalog "decodedMobys" $mobys.Count)
        placementRuntimeMobys = $placementMobys.Count
        gemClassMobys = @($mobys | Where-Object { [string](Get-Field $_ "typeHex" "") -eq "0x20" }).Count
        confirmedCollectedGemSamples = @($mobys | Where-Object { [string](Get-Field $_ "diffEvidenceRole" "") -eq "collected-gem-sample" }).Count
        dragonNpcCandidates = @($mobys | Where-Object { [string](Get-Field $_ "typeHex" "") -eq "0x30" }).Count
        enemyActiveCandidates = @($mobys | Where-Object { [string](Get-Field $_ "typeHex" "") -eq "0x18" }).Count
        mobysWithScaledSourceLeads = @($markers | Where-Object { $null -ne $_.sourceLead -and [int]$_.sourceLead.scaledWindowCount -gt 0 }).Count
        mobysWithPublicTargetHints = @($markers | Where-Object { @(Get-ArrayField $_ "publicTargetHints").Count -gt 0 }).Count
    }
    zoneSummary = $zoneSummary
    zonePublicRouteHypotheses = $zonePublicRouteHypotheses
    publicTargetCandidateLedger = $publicTargetCandidateLedger
    mobyMarkers = $markers
    nextSourcePatchTests = $topLeads
    focusedCapturePlan = @(
        [ordered]@{
            target = "Named dragons"
            samples = @("free Lindar in the cave", "free Gildas on/near the tower", "free Astor near Return Home", "free Gavin in the well")
            expectedSignal = "_globalDragonCount increments, collectablesStateFlags changes, and one local dragon/NPC candidate or associated special data changes."
            editorUse = "Assign names to 0x30 or newly revealed dragon records and attach the matching source-window lead."
        },
        [ordered]@{
            target = "Egg thief"
            samples = @("catch the Stone Hill blue thief in the high open fields")
            expectedSignal = "_globalEggCount increments and high-field route records change."
            editorUse = "Separate blue-thief identity from generic 0x18 enemy/active-object candidates."
        },
        [ordered]@{
            target = "Key and locked chest"
            samples = @("pick up the hidden beach-cave key", "open the locked chest in the well")
            expectedSignal = "collectablesStateFlags and treasure count change around low-map/east and low-map/west route clusters."
            editorUse = "Identify which 0x10/0x5C/0x04/0x33 records are key, chest, or helper records."
        },
        [ordered]@{
            target = "Enemies and fodder"
            samples = @("defeat one ram", "defeat one shepherd", "flame one sheep")
            expectedSignal = "enemy/fodder state changes plus possible spawned dynamic gem records."
            editorUse = "Split 0x18 active-object candidates into enemy families and separate fodder if it uses another type."
        }
    )
    unresolvedReplicaGaps = @(
        "Only one gem pickup diff has been captured, so gem class is strong but individual gem/container identities are not complete.",
        "The two type 0x30 dragon/NPC candidates are not assigned to Lindar/Gildas/Astor/Gavin yet.",
        "The key, locked chest, blue thief, rams, shepherds, sheep fodder, and chest variants still need focused before/after RAM samples.",
        "Exact int32 WAD coordinate triples currently do not explain placement-like mobys; scaled/packed int16 source windows are the active lead."
    )
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote Stone Hill replica map plan to $resolvedOut"
Write-Host "Markers: $($markers.Count); placement: $($placementMobys.Count); scaled source leads: $($result.coverage.mobysWithScaledSourceLeads)"
