param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$GeometryPath = "",
    [int]$GeometryCandidateIndex = 0,
    [string]$DiffPath = "",
    [string]$ReplicaPlanPath = "",
    [string]$OutImagePath = ".\stonehill-moby-map.png",
    [string]$OutCatalogPath = ".\stonehill-moby-catalog.json",
    [int]$Width = 1800,
    [int]$Height = 1300
)

Set-StrictMode -Version 2.0
Add-Type -AssemblyName System.Drawing

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if (-not $Object.Contains($Name) -or $null -eq $Object[$Name]) { return $Default }
        return $Object[$Name]
    }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop -or $null -eq $prop.Value) { return $Default }
    return $prop.Value
}

function Get-ArrayField($Object, [string]$Name) {
    $value = Get-Field $Object $Name @()
    return @($value)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Copy-ByteRange([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $copy = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Offset, $copy, 0, $Length)
    return $copy
}

function Test-PsxPointerInMainRam([uint32]$Pointer) {
    $address = [uint64]$Pointer
    return ($address -ge [Convert]::ToUInt64("80000000", 16) -and $address -lt [Convert]::ToUInt64("80200000", 16))
}

function Test-PlausibleRuntimeMoby([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 80) -gt $Ram.Length) { return $false }
    $type = [int]$Ram[$Offset + 0x48]
    $state = [int]$Ram[$Offset + 0x49]
    if ($type -le 0 -or $type -gt 0x7F) { return $false }
    if ($state -gt 0x7F) { return $false }
    $x = Get-Int32LE $Ram ($Offset + 4)
    $y = Get-Int32LE $Ram ($Offset + 8)
    $z = Get-Int32LE $Ram ($Offset + 12)
    if ([Math]::Abs([int64]$x) -gt 4000000 -or [Math]::Abs([int64]$y) -gt 4000000 -or [Math]::Abs([int64]$z) -gt 4000000) { return $false }
    if ([Math]::Abs([int64]$x) -lt 16 -and [Math]::Abs([int64]$y) -lt 16 -and [Math]::Abs([int64]$z) -lt 16) { return $false }
    return $true
}

function Count-PlausibleMobysAtPointer([byte[]]$Bytes, [int]$WindowBase, [uint32]$Pointer) {
    if (-not (Test-PsxPointerInMainRam $Pointer)) { return 0 }
    $tableOffset = [int]($Pointer -band 0x001FFFFF)
    $count = 0
    for ($i = 0; $i -lt 128; $i++) {
        $recordOffset = $WindowBase + $tableOffset + ($i * 80)
        if (($recordOffset + 80) -gt $Bytes.Length) { break }
        if (Test-PlausibleRuntimeMoby $Bytes $recordOffset) { $count++ }
    }
    return $count
}

function Find-PsxRamWindow([byte[]]$Bytes) {
    $windowSize = 0x200000
    if ($Bytes.Length -lt $windowSize) { throw "RAM dump is smaller than 2 MB." }

    $candidateBases = New-Object System.Collections.Generic.List[int]
    foreach ($base in @(0, 0x80, 0x100, 0x200, 0x400, 0x800, 0x1000, ($Bytes.Length - $windowSize))) {
        if ($base -ge 0 -and ($base + $windowSize) -le $Bytes.Length -and -not $candidateBases.Contains($base)) {
            [void]$candidateBases.Add($base)
        }
    }
    for ($base = 0; $base -le ($Bytes.Length - $windowSize); $base += 0x10000) {
        if (-not $candidateBases.Contains($base)) { [void]$candidateBases.Add($base) }
    }

    $best = $null
    foreach ($base in $candidateBases) {
        $pointer = Get-UInt32LE $Bytes ($base + 0x75828)
        $count = Count-PlausibleMobysAtPointer $Bytes $base $pointer
        $levelId = Get-UInt32LE $Bytes ($base + 0x758B4)
        $score = $count
        if (Test-PsxPointerInMainRam $pointer) { $score += 4 }
        if ($levelId -gt 0 -and $levelId -lt 0x80) { $score += 2 }
        if ($null -eq $best -or $score -gt $best.score) {
            $best = [ordered]@{
                baseOffset = $base
                pointer = $pointer
                levelId = $levelId
                count = $count
                score = $score
            }
        }
    }

    if ($null -eq $best -or -not (Test-PsxPointerInMainRam ([uint32]$best.pointer)) -or $best.count -eq 0) {
        throw "Could not find a live Spyro moby table in the RAM dump."
    }

    return [ordered]@{
        ram = Copy-ByteRange $Bytes $best.baseOffset $windowSize
        baseOffset = $best.baseOffset
        pointer = [uint32]$best.pointer
        levelId = [uint32]$best.levelId
        count = [int]$best.count
    }
}

function Get-MobyHint([int]$TypeId, [int]$State, [bool]$SparseAfterGap) {
    if ($SparseAfterGap) {
        return [ordered]@{
            kind = "sparse false-positive candidate"
            confidence = "weak"
            color = "#6F7E89"
            note = "Appears after a long invalid-table gap; verify in-game before using as a real moby."
        }
    }
    if ($TypeId -eq 0x20) {
        return [ordered]@{
            kind = "likely gem/collectible"
            confidence = "strong"
            color = "#F4D44D"
            note = "Confirmed by the available Stone Hill before/after gem collection RAM sample."
        }
    }
    if ($TypeId -eq 0x30) {
        return [ordered]@{
            kind = "dragon/NPC candidate"
            confidence = "weak"
            color = "#B78CFF"
            note = "Placement-like records with special-data pointers; needs save/free-dragon RAM sample before naming."
        }
    }
    if ($TypeId -eq 0x18) {
        return [ordered]@{
            kind = "active object/enemy candidate"
            confidence = "weak"
            color = "#FF8C5A"
            note = "Repeated placed records with no source identity yet; compare against ram/shepherd/fodder samples."
        }
    }
    if ($TypeId -eq 0x0A -or $TypeId -eq 0x33 -or $TypeId -eq 0x52) {
        return [ordered]@{
            kind = "level helper/system candidate"
            confidence = "weak"
            color = "#8FA6B8"
            note = "Often appears at placeholder coordinates or unusual states; not yet an editable placement target."
        }
    }
    return [ordered]@{
        kind = "unidentified moby"
        confidence = "unknown"
        color = "#5DADE2"
        note = "Needs focused before/after RAM samples."
    }
}

function Get-ProgressCounterDelta($ProgressDiff, [string]$Name) {
    if ($null -eq $ProgressDiff) { return 0 }
    foreach ($change in @(Get-ArrayField $ProgressDiff "counterChanges")) {
        if ([string](Get-Field $change "name" "") -eq $Name) {
            return [int](Get-Field $change "delta" 0)
        }
    }
    return 0
}

function Get-DiffEvidence($Change, [int]$TypeId, $ProgressDiff) {
    if ($null -eq $Change) {
        return [ordered]@{
            role = ""
            confidence = ""
            note = ""
        }
    }

    $meaning = [string](Get-Field $Change "likelyMeaning" "")
    $fieldChanges = @(Get-ArrayField $Change "fieldChanges")
    $gemDelta = Get-ProgressCounterDelta $ProgressDiff "globalGemCount"
    if ($gemDelta -gt 0 -and $TypeId -eq 0x20 -and ($meaning -eq "state changed" -or ($fieldChanges -join ";") -like "*stateHex:*")) {
        return [ordered]@{
            role = "collected-gem-sample"
            confidence = "confirmed-sample"
            note = "This type 0x20 moby changed state in the isolated gem-clean RAM pair while globalGemCount increased, making it the strongest specific collected-gem sample."
        }
    }

    if ($gemDelta -gt 0 -and $TypeId -eq 0x20) {
        return [ordered]@{
            role = "gem-pickup-neighbor-change"
            confidence = "supporting-sample"
            note = "This type 0x20 moby changed during the gem-clean RAM pair, but without the direct state-change signal seen on the collected sample."
        }
    }

    return [ordered]@{
        role = "changed-during-focused-sample"
        confidence = "weak-context"
        note = "This moby changed during the focused RAM pair, but the current counter signal does not identify it as the collected object."
    }
}

function Read-RuntimeMobys([byte[]]$Ram, [uint32]$Pointer, $ChangedByIndex, $ProgressDiff) {
    $start = [int]($Pointer -band 0x001FFFFF)
    if ($start -lt 0 -or ($start + 80) -gt $Ram.Length) { throw ("Moby pointer 0x{0:X8} is outside RAM." -f $Pointer) }

    $records = @()
    $badRun = 0
    $afterLongGap = $false
    for ($i = 0; $i -lt 512; $i++) {
        $offset = $start + ($i * 80)
        if (($offset + 80) -gt $Ram.Length) { break }
        $gapBeforeRecord = $badRun
        if (-not (Test-PlausibleRuntimeMoby $Ram $offset)) {
            $badRun++
            if ($badRun -ge 24) { $afterLongGap = $true }
            continue
        }
        $badRun = 0

        $rawX = Get-Int32LE $Ram ($offset + 4)
        $rawY = Get-Int32LE $Ram ($offset + 8)
        $rawZ = Get-Int32LE $Ram ($offset + 12)
        $typeId = [int]$Ram[$offset + 0x48]
        $state = [int]$Ram[$offset + 0x49]
        $sparseAfterGap = ($afterLongGap -or $gapBeforeRecord -ge 24)
        $hint = Get-MobyHint $typeId $state $sparseAfterGap
        $change = $null
        if ($ChangedByIndex.ContainsKey($i)) { $change = $ChangedByIndex[$i] }
        $diffEvidence = Get-DiffEvidence $change $typeId $ProgressDiff
        $evidenceNote = $hint.note
        if (-not [string]::IsNullOrWhiteSpace([string](Get-Field $diffEvidence "note" ""))) {
            $evidenceNote = "$evidenceNote $([string](Get-Field $diffEvidence 'note' ''))"
        }
        $records += [pscustomobject][ordered]@{
            index = $i
            runtimeAddress = ("0x{0:X8}" -f (0x80000000 + $offset))
            ramOffset = $offset
            typeId = $typeId
            typeHex = ("0x{0:X2}" -f $typeId)
            state = $state
            stateHex = ("0x{0:X2}" -f $state)
            x = [Math]::Round($rawX / 16.0, 2)
            y = [Math]::Round($rawY / 16.0, 2)
            z = [Math]::Round($rawZ / 16.0, 2)
            rawX = $rawX
            rawY = $rawY
            rawZ = $rawZ
            specialDataPointer = ("0x{0:X8}" -f (Get-UInt32LE $Ram $offset))
            flag4A = [int]$Ram[$offset + 0x4A]
            flag4B = [int]$Ram[$offset + 0x4B]
            sparseAfterGap = $sparseAfterGap
            gapBeforeRecord = $gapBeforeRecord
            candidateKind = $hint.kind
            confidence = $hint.confidence
            color = $hint.color
            evidence = $evidenceNote
            changedInDiff = ($null -ne $change)
            diffMeaning = $(if ($null -ne $change) { [string](Get-Field $change "likelyMeaning" "") } else { "" })
            diffEvidenceRole = [string](Get-Field $diffEvidence "role" "")
            diffEvidenceConfidence = [string](Get-Field $diffEvidence "confidence" "")
            diffEvidenceNote = [string](Get-Field $diffEvidence "note" "")
        }
    }
    return @($records)
}

function Load-ChangedMobyIndexes([string]$Path) {
    $result = @{}
    if ([string]::IsNullOrWhiteSpace($Path)) { return $result }
    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $resolved) { return $result }
    $diff = Get-Content -Raw -LiteralPath $resolved.Path | ConvertFrom-Json
    foreach ($change in @(Get-ArrayField $diff "changes")) {
        $result[[int](Get-Field $change "index" -1)] = $change
    }
    return $result
}

function Load-DiffReport([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $resolved) { return $null }
    return (Get-Content -Raw -LiteralPath $resolved.Path | ConvertFrom-Json)
}

function Load-ReplicaPlanLabels([string]$Path) {
    $labels = @{}
    if ([string]::IsNullOrWhiteSpace($Path)) { return $labels }
    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $resolved) { return $labels }
    $plan = Get-Content -Raw -LiteralPath $resolved.Path | ConvertFrom-Json
    foreach ($marker in @(Get-ArrayField $plan "mobyMarkers")) {
        $index = [int](Get-Field $marker "index" -1)
        if ($index -lt 0) { continue }
        $display = [string](Get-Field $marker "displayTargetLabel" "")
        if ([string]::IsNullOrWhiteSpace($display)) { continue }
        $labels[$index] = [ordered]@{
            displayTargetLabel = $display
            targetCandidates = @(Get-ArrayField $marker "publicTargetCandidates")
        }
    }
    return $labels
}

function Get-PointBounds($Points) {
    $minX = [double]::PositiveInfinity
    $maxX = [double]::NegativeInfinity
    $minY = [double]::PositiveInfinity
    $maxY = [double]::NegativeInfinity
    $count = 0
    foreach ($point in $Points) {
        $x = [double](Get-Field $point "x" 0)
        $y = [double](Get-Field $point "y" 0)
        if ($x -lt $minX) { $minX = $x }
        if ($x -gt $maxX) { $maxX = $x }
        if ($y -lt $minY) { $minY = $y }
        if ($y -gt $maxY) { $maxY = $y }
        $count++
    }
    if ($count -eq 0) { return $null }
    return [pscustomobject]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY }
}

function Expand-Bounds($Bounds, $Other) {
    if ($null -eq $Other) { return $Bounds }
    if ($null -eq $Bounds) { return [pscustomobject]@{ minX = $Other.minX; maxX = $Other.maxX; minY = $Other.minY; maxY = $Other.maxY } }
    if ([double]$Other.minX -lt [double]$Bounds.minX) { $Bounds.minX = [double]$Other.minX }
    if ([double]$Other.maxX -gt [double]$Bounds.maxX) { $Bounds.maxX = [double]$Other.maxX }
    if ([double]$Other.minY -lt [double]$Bounds.minY) { $Bounds.minY = [double]$Other.minY }
    if ([double]$Other.maxY -gt [double]$Bounds.maxY) { $Bounds.maxY = [double]$Other.maxY }
    return $Bounds
}

function Convert-HtmlColor([string]$Hex) {
    return [System.Drawing.ColorTranslator]::FromHtml($Hex)
}

function Convert-ToScreen($X, $Y, $Bounds, [int]$Left, [int]$Top, [int]$DrawWidth, [int]$DrawHeight) {
    $spanX = [Math]::Max(1.0, [double]$Bounds.maxX - [double]$Bounds.minX)
    $spanY = [Math]::Max(1.0, [double]$Bounds.maxY - [double]$Bounds.minY)
    $scale = [Math]::Min($DrawWidth / $spanX, $DrawHeight / $spanY)
    $actualWidth = $spanX * $scale
    $actualHeight = $spanY * $scale
    $originX = $Left + (($DrawWidth - $actualWidth) / 2.0)
    $originY = $Top + (($DrawHeight - $actualHeight) / 2.0)
    return [System.Drawing.PointF]::new(
        [float]($originX + (([double]$X - [double]$Bounds.minX) * $scale)),
        [float]($originY + (([double]$Y - [double]$Bounds.minY) * $scale))
    )
}

function Draw-Label($Graphics, [string]$Text, [System.Drawing.Font]$Font, [System.Drawing.Brush]$Brush, [float]$X, [float]$Y) {
    $size = $Graphics.MeasureString($Text, $Font)
    $rect = [System.Drawing.RectangleF]::new($X, $Y, [float]($size.Width + 7), [float]($size.Height + 4))
    $back = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180, 12, 15, 20))
    try {
        $Graphics.FillRectangle($back, $rect)
        $Graphics.DrawString($Text, $Font, $Brush, $X + 3, $Y + 2)
    }
    finally {
        $back.Dispose()
    }
}

function New-RouteAnchor([string]$Name, [string]$Target, [double]$X, [double]$Y, [string]$Evidence, [int[]]$RelatedMobys) {
    return [ordered]@{
        name = $Name
        target = $Target
        x = [Math]::Round($X, 2)
        y = [Math]::Round($Y, 2)
        evidence = $Evidence
        relatedMobys = @($RelatedMobys)
    }
}

function Get-AverageAnchor($Mobys, [int[]]$Indexes, [string]$Name, [string]$Target, [string]$Evidence) {
    $selected = @($Mobys | Where-Object { $Indexes -contains [int]$_.index })
    if ($selected.Count -eq 0) { return $null }
    $avgX = ($selected | Measure-Object -Property x -Average).Average
    $avgY = ($selected | Measure-Object -Property y -Average).Average
    return New-RouteAnchor $Name $Target ([double]$avgX) ([double]$avgY) $Evidence $Indexes
}

function Get-PublicRouteAnchors($Mobys) {
    $anchors = New-Object System.Collections.Generic.List[object]

    $wellAnchor = Get-AverageAnchor $Mobys @(95, 183, 390, 399, 412, 413) "Start / dry well" "Gavin, locked chest, start-side helpers" "Public walkthroughs place Gavin and the locked chest in the starting-area dry well; these are the current low-west runtime leads."
    if ($null -ne $wellAnchor) { [void]$anchors.Add($wellAnchor) }

    $beachAnchor = Get-AverageAnchor $Mobys @(421, 422) "Beach / key cave" "Hidden key and beach-cave treasure" "Public walkthroughs place the key in the secret beach cave behind the Return Home side; these are the current low-east route leads."
    if ($null -ne $beachAnchor) { [void]$anchors.Add($beachAnchor) }

    $astorAnchor = Get-AverageAnchor $Mobys @(65, 186, 208, 398) "Castle / Return Home" "Astor, portal-side treasure/helpers" "Public walkthroughs place Astor near the Return Home portal; L65 is the current 0x30 route candidate with a source-window lead."
    if ($null -ne $astorAnchor) { [void]$anchors.Add($astorAnchor) }

    $towerAnchor = Get-AverageAnchor $Mobys @(54, 98, 109, 120, 153) "Cave / tower route" "Lindar or Gildas route, tower-side enemies/gems" "Public walkthroughs place Lindar in the left cave and Gildas at the tower; L54 is the current shared 0x30 cave/tower candidate."
    if ($null -ne $towerAnchor) { [void]$anchors.Add($towerAnchor) }

    $highFieldAnchor = Get-AverageAnchor $Mobys @(10, 21, 32, 87, 131, 142, 405, 411) "High fields" "Blue thief loop, rams/shepherds, hilltop treasure" "Public walkthroughs place the egg thief in the high open fields; these are the current high-east runtime leads."
    if ($null -ne $highFieldAnchor) { [void]$anchors.Add($highFieldAnchor) }

    return $anchors.ToArray()
}

function Get-RouteAnchorByName($Anchors, [string]$Name) {
    $match = @($Anchors | Where-Object { [string](Get-Field $_ "name" "") -eq $Name } | Select-Object -First 1)
    if ($match.Count -eq 0) { return $null }
    return $match[0]
}

function New-RouteEdge($Anchors, [string]$From, [string]$To, [string]$RouteRole, [string]$Evidence) {
    $fromAnchor = Get-RouteAnchorByName $Anchors $From
    $toAnchor = Get-RouteAnchorByName $Anchors $To
    if ($null -eq $fromAnchor -or $null -eq $toAnchor) { return $null }
    return [ordered]@{
        from = $From
        to = $To
        routeRole = $RouteRole
        evidence = $Evidence
        x1 = [double](Get-Field $fromAnchor "x" 0)
        y1 = [double](Get-Field $fromAnchor "y" 0)
        x2 = [double](Get-Field $toAnchor "x" 0)
        y2 = [double](Get-Field $toAnchor "y" 0)
    }
}

function Get-PublicRouteEdges($Anchors) {
    $edges = New-Object System.Collections.Generic.List[object]

    foreach ($edge in @(
        (New-RouteEdge $Anchors "Start / dry well" "Castle / Return Home" "start-to-middle-castle" "Main route from the starting field through the castle/Return Home side, including Astor and the beach drop."),
        (New-RouteEdge $Anchors "Castle / Return Home" "Beach / key cave" "return-home-to-hidden-beach" "Hidden beach/key cave sits below or behind the Return Home/castle side in public walkthroughs."),
        (New-RouteEdge $Anchors "Start / dry well" "Cave / tower route" "start-to-left-cave" "Start-side cave route leads toward Lindar/cave treasure and overlaps the current tower-side runtime cluster."),
        (New-RouteEdge $Anchors "Cave / tower route" "High fields" "tower-to-high-fields" "Tower/highland route connects to the open fields used by Gildas, the blue thief loop, rams, shepherds, and hilltop gems."),
        (New-RouteEdge $Anchors "Start / dry well" "High fields" "start-to-right-route" "Right-side route from the starting area climbs toward the tower and high open fields.")
    )) {
        if ($null -ne $edge) { [void]$edges.Add($edge) }
    }

    return $edges.ToArray()
}

$ramBytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath).Path)
$window = Find-PsxRamWindow $ramBytes
$changedByIndex = Load-ChangedMobyIndexes $DiffPath
$diffReport = Load-DiffReport $DiffPath
$replicaPlanLabels = Load-ReplicaPlanLabels $ReplicaPlanPath
$progressDiff = Get-Field $diffReport "progressDiff" $null
$mobys = @(Read-RuntimeMobys ([byte[]]$window.ram) ([uint32]$window.pointer) $changedByIndex $progressDiff)
if ($mobys.Count -eq 0) { throw "No mobys decoded from RAM." }
foreach ($moby in $mobys) {
    if ($replicaPlanLabels.ContainsKey([int]$moby.index)) {
        $labelInfo = $replicaPlanLabels[[int]$moby.index]
        $moby | Add-Member -NotePropertyName displayTargetLabel -NotePropertyValue ([string](Get-Field $labelInfo "displayTargetLabel" "")) -Force
        $moby | Add-Member -NotePropertyName publicTargetCandidates -NotePropertyValue @(Get-ArrayField $labelInfo "targetCandidates") -Force
    }
}
$publicRouteAnchors = @(Get-PublicRouteAnchors $mobys)
$publicRouteEdges = @(Get-PublicRouteEdges $publicRouteAnchors)

$geometryCandidate = $null
if (-not [string]::IsNullOrWhiteSpace($GeometryPath)) {
    $resolvedGeometry = Resolve-Path -LiteralPath $GeometryPath -ErrorAction SilentlyContinue
    if ($null -ne $resolvedGeometry) {
        $geometry = Get-Content -Raw -LiteralPath $resolvedGeometry.Path | ConvertFrom-Json
        $candidates = @(Get-ArrayField $geometry "candidates")
        if ($GeometryCandidateIndex -ge 0 -and $GeometryCandidateIndex -lt $candidates.Count) {
            $geometryCandidate = $candidates[$GeometryCandidateIndex]
        }
    }
}

$placementMobys = @($mobys | Where-Object { -not ($_.x -eq 0 -and $_.y -eq 256 -and $_.z -eq 0) })
if ($placementMobys.Count -eq 0) { $placementMobys = $mobys }
$mobyPoints = @($placementMobys | ForEach-Object { [pscustomobject]@{ x = $_.x; y = $_.y } })
$bounds = Get-PointBounds $mobyPoints

if ($null -ne $geometryCandidate) {
    $geometryPoints = @(Get-ArrayField $geometryCandidate "projectedPoints")
    $geometryBounds = Get-Field $geometryCandidate "projectedBounds" $null
    if ($null -eq $geometryBounds) { $geometryBounds = Get-PointBounds $geometryPoints }
    $bounds = Expand-Bounds $bounds $geometryBounds
}

$padX = ([double]$bounds.maxX - [double]$bounds.minX) * 0.08
$padY = ([double]$bounds.maxY - [double]$bounds.minY) * 0.08
$bounds.minX = [double]$bounds.minX - [Math]::Max(128, $padX)
$bounds.maxX = [double]$bounds.maxX + [Math]::Max(128, $padX)
$bounds.minY = [double]$bounds.minY - [Math]::Max(128, $padY)
$bounds.maxY = [double]$bounds.maxY + [Math]::Max(128, $padY)

$bitmap = New-Object System.Drawing.Bitmap $Width, $Height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(22, 24, 28))

$font = New-Object System.Drawing.Font("Segoe UI", 10)
$smallFont = New-Object System.Drawing.Font("Consolas", 8)
$titleFont = New-Object System.Drawing.Font("Segoe UI Semibold", 14)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(238, 244, 248, 250))
$mutedBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 185, 198, 208))
$gridPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(36, 255, 255, 255), 1)
$axisPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90, 255, 255, 255), 1)
$geometryPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(95, 64, 224, 255), 1)
$geometryBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(105, 64, 224, 255))
$changedPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(240, 255, 255, 255), 3)
$anchorPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 255, 210, 96), 2)
$anchorBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 255, 210, 96))
$routePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(130, 255, 210, 96), 2)
$routePen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash

try {
    $left = 60
    $top = 76
    $drawWidth = $Width - 110
    $drawHeight = $Height - 150

    $graphics.DrawString("Stone Hill runtime moby map", $titleFont, $textBrush, 60, 24)
    $progressText = ""
    if ($null -ne $progressDiff) {
        $counterChanges = @(Get-ArrayField $progressDiff "counterChanges")
        $gemChange = @($counterChanges | Where-Object { [string](Get-Field $_ "name" "") -eq "globalGemCount" } | Select-Object -First 1)
        if ($gemChange.Count -gt 0) {
            $progressText = " Gem counter delta: $([int](Get-Field $gemChange[0] 'delta' 0))."
        }
    }
    $subtitle = "RAM pointer 0x{0:X8}, level 0x{1:X}, decoded {2} plausible records. Yellow = gem evidence; white ring = changed in diff.{3}" -f ([uint32]$window.pointer), ([uint32]$window.levelId), $mobys.Count, $progressText
    $graphics.DrawString($subtitle, $font, $mutedBrush, 60, 49)

    $gridStep = 1024
    $startX = [Math]::Floor([double]$bounds.minX / $gridStep) * $gridStep
    for ($x = $startX; $x -le [double]$bounds.maxX; $x += $gridStep) {
        $p1 = Convert-ToScreen $x ([double]$bounds.minY) $bounds $left $top $drawWidth $drawHeight
        $p2 = Convert-ToScreen $x ([double]$bounds.maxY) $bounds $left $top $drawWidth $drawHeight
        $graphics.DrawLine($(if ([Math]::Abs($x) -lt 0.1) { $axisPen } else { $gridPen }), $p1, $p2)
        $graphics.DrawString([string][int]$x, $smallFont, $mutedBrush, $p1.X + 3, $top + 3)
    }
    $startY = [Math]::Floor([double]$bounds.minY / $gridStep) * $gridStep
    for ($y = $startY; $y -le [double]$bounds.maxY; $y += $gridStep) {
        $p1 = Convert-ToScreen ([double]$bounds.minX) $y $bounds $left $top $drawWidth $drawHeight
        $p2 = Convert-ToScreen ([double]$bounds.maxX) $y $bounds $left $top $drawWidth $drawHeight
        $graphics.DrawLine($(if ([Math]::Abs($y) -lt 0.1) { $axisPen } else { $gridPen }), $p1, $p2)
        $graphics.DrawString([string][int]$y, $smallFont, $mutedBrush, $left + 3, $p1.Y + 3)
    }

    if ($null -ne $geometryCandidate) {
        foreach ($edge in @(Get-ArrayField $geometryCandidate "edges")) {
            $p1 = Convert-ToScreen (Get-Field $edge "x1" 0) (Get-Field $edge "y1" 0) $bounds $left $top $drawWidth $drawHeight
            $p2 = Convert-ToScreen (Get-Field $edge "x2" 0) (Get-Field $edge "y2" 0) $bounds $left $top $drawWidth $drawHeight
            $graphics.DrawLine($geometryPen, $p1, $p2)
        }
        $geometryPoints = @(Get-ArrayField $geometryCandidate "projectedPoints")
        $step = [Math]::Max(1, [int][Math]::Ceiling($geometryPoints.Count / 1600.0))
        for ($i = 0; $i -lt $geometryPoints.Count; $i += $step) {
            $point = $geometryPoints[$i]
            $screen = Convert-ToScreen (Get-Field $point "x" 0) (Get-Field $point "y" 0) $bounds $left $top $drawWidth $drawHeight
            $graphics.FillRectangle($geometryBrush, $screen.X - 1, $screen.Y - 1, 2, 2)
        }
    }

    foreach ($edge in $publicRouteEdges) {
        $p1 = Convert-ToScreen (Get-Field $edge "x1" 0) (Get-Field $edge "y1" 0) $bounds $left $top $drawWidth $drawHeight
        $p2 = Convert-ToScreen (Get-Field $edge "x2" 0) (Get-Field $edge "y2" 0) $bounds $left $top $drawWidth $drawHeight
        $graphics.DrawLine($routePen, $p1, $p2)
    }

    foreach ($anchor in $publicRouteAnchors) {
        $screen = Convert-ToScreen (Get-Field $anchor "x" 0) (Get-Field $anchor "y" 0) $bounds $left $top $drawWidth $drawHeight
        $graphics.DrawEllipse($anchorPen, $screen.X - 15, $screen.Y - 15, 30, 30)
        $graphics.DrawLine($anchorPen, $screen.X - 20, $screen.Y, $screen.X + 20, $screen.Y)
        $graphics.DrawLine($anchorPen, $screen.X, $screen.Y - 20, $screen.X, $screen.Y + 20)
        $anchorText = "$([string](Get-Field $anchor 'name' 'Route')): $([string](Get-Field $anchor 'target' 'target'))"
        Draw-Label $graphics $anchorText $smallFont $anchorBrush ($screen.X + 18) ($screen.Y + 8)
    }

    foreach ($moby in ($mobys | Sort-Object sparseAfterGap, index)) {
        $screen = Convert-ToScreen $moby.x $moby.y $bounds $left $top $drawWidth $drawHeight
        $color = Convert-HtmlColor ([string]$moby.color)
        if ($moby.sparseAfterGap) { $color = [System.Drawing.Color]::FromArgb(145, $color) }
        $brush = New-Object System.Drawing.SolidBrush($color)
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(230, 12, 15, 20), 2)
        try {
            $radius = $(if ($moby.sparseAfterGap) { 5 } else { 8 })
            $graphics.FillEllipse($brush, $screen.X - $radius, $screen.Y - $radius, $radius * 2, $radius * 2)
            $graphics.DrawEllipse($pen, $screen.X - $radius, $screen.Y - $radius, $radius * 2, $radius * 2)
            if ($moby.changedInDiff) {
                $graphics.DrawEllipse($changedPen, $screen.X - ($radius + 4), $screen.Y - ($radius + 4), ($radius + 4) * 2, ($radius + 4) * 2)
            }
        }
        finally {
            $brush.Dispose()
            $pen.Dispose()
        }
        $shortKind = switch ([string]$moby.candidateKind) {
            "likely gem/collectible" { "gem" }
            "dragon/NPC candidate" { "dragon?" }
            "active object/enemy candidate" { "enemy?" }
            "level helper/system candidate" { "helper?" }
            "sparse false-positive candidate" { "sparse?" }
            default { "unknown" }
        }
        $targetLabel = [string](Get-Field $moby "displayTargetLabel" "")
        if (-not [string]::IsNullOrWhiteSpace($targetLabel)) {
            $shortKind = $targetLabel
        }
        $label = "#{0} {1} {2}" -f $moby.index, $moby.typeHex, $shortKind
        Draw-Label $graphics $label $smallFont $textBrush ($screen.X + 10) ($screen.Y - 10)
    }

    $legendY = $Height - 56
    $legendText = "Yellow crosshair labels are public route anchors; dashed yellow lines are route hypotheses. Moby labels remain conservative. Only 0x20 has direct gem evidence. Target: 4 dragons, 1 egg thief, enemies, fodder, 200 gems."
    if ($null -ne $progressDiff -and [int](Get-Field $progressDiff "collectableFlagChangeCount" 0) -gt 0) {
        $legendText += " Diff also records $([int](Get-Field $progressDiff 'collectableFlagChangeCount' 0)) collectable flag byte change(s)."
    }
    $graphics.DrawString($legendText, $font, $mutedBrush, 60, $legendY)

    $resolvedImage = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutImagePath)
    $bitmap.Save($resolvedImage, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
    $font.Dispose()
    $smallFont.Dispose()
    $titleFont.Dispose()
    $textBrush.Dispose()
    $mutedBrush.Dispose()
    $gridPen.Dispose()
    $axisPen.Dispose()
    $geometryPen.Dispose()
    $geometryBrush.Dispose()
    $changedPen.Dispose()
    $anchorPen.Dispose()
    $anchorBrush.Dispose()
    $routePen.Dispose()
}

$typeSummary = @($mobys |
    Group-Object typeHex |
    Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, @{ Expression = { $_.Name }; Descending = $false } |
    ForEach-Object {
        $sample = $_.Group | Select-Object -First 1
        [pscustomobject][ordered]@{
            typeHex = $_.Name
            count = $_.Count
            candidateKind = $sample.candidateKind
            confidence = $sample.confidence
            evidence = $sample.evidence
        }
    })

$catalog = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    ramPath = (Resolve-Path -LiteralPath $RamPath).Path
    geometryPath = $(if ([string]::IsNullOrWhiteSpace($GeometryPath)) { "" } else { (Resolve-Path -LiteralPath $GeometryPath -ErrorAction SilentlyContinue).Path })
    geometryCandidateIndex = $GeometryCandidateIndex
    diffPath = $(if ([string]::IsNullOrWhiteSpace($DiffPath)) { "" } else { (Resolve-Path -LiteralPath $DiffPath -ErrorAction SilentlyContinue).Path })
    replicaPlanPath = $(if ([string]::IsNullOrWhiteSpace($ReplicaPlanPath)) { "" } else { (Resolve-Path -LiteralPath $ReplicaPlanPath -ErrorAction SilentlyContinue).Path })
    mobyPointer = ("0x{0:X8}" -f ([uint32]$window.pointer))
    levelId = ("0x{0:X8}" -f ([uint32]$window.levelId))
    decodedMobys = $mobys.Count
    placementMobys = $placementMobys.Count
    progressDiff = $progressDiff
    publicRouteAnchors = $publicRouteAnchors
    publicRouteEdges = $publicRouteEdges
    expectedStoneHillContents = [ordered]@{
        gems = 200
        dragons = @("Lindar", "Gildas", "Astor", "Gavin")
        eggs = 1
        enemies = @("Rams", "Shepherds", "Blue Thief")
        fodder = @("Sheep")
        sourceNotes = @(
            "https://spyrowiki.com/wiki/Stone_Hill",
            "https://spyro.fandom.com/wiki/Stone_Hill",
            "https://strategywiki.org/wiki/Spyro_the_Dragon/Stone_Hill",
            "https://www.gamepur.com/guides/spyro-reignited-stone-hill-key-location",
            "https://www.gamepressure.com/spyro-reignited-trilogy/dragons/zab996"
        )
    }
    typeSummary = $typeSummary
    mobys = $mobys
}

$resolvedCatalog = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutCatalogPath)
$catalog | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedCatalog -Encoding UTF8

Write-Host "Wrote labelled moby map to $($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutImagePath))"
Write-Host "Wrote moby catalog to $resolvedCatalog"
Write-Host "Decoded mobys: $($mobys.Count); placement-like: $($placementMobys.Count); pointer: $('0x{0:X8}' -f ([uint32]$window.pointer))"
