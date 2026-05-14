param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$SourceSearchPath = ".\stonehill-runtime-terrain-source-search.json",
    [string]$OutPath = ".\Spyro the Dragon (USA)-runtime-terrainpatchtest.bin",
    [string]$CuePath = "",
    [string]$PlanPath = ".\Spyro the Dragon (USA)-runtime-terrainpatchtest.bin.terrainpatchplan.json",
    [string]$MarkdownPath = ".\Spyro the Dragon (USA)-runtime-terrainpatchtest.bin.terrainpatchplan.md",
    [int]$WadLba = 37,
    [switch]$IncludeCollisionLp,
    [double]$CollisionLpPadding = 256.0,
    [int]$MaxCollisionLpVerticesPerEdit = 8,
    [switch]$PlanOnly,
    [switch]$AllowExperimentalWrite
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
        return $Default
    }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop -or $null -eq $prop.Value) { return $Default }
    return $prop.Value
}

function Get-ArrayField($Object, [string]$Name) {
    $value = Get-Field $Object $Name @()
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return @($value) }
    return @($value)
}

function Convert-HexTextToInt64([string]$Text, [int64]$Default = -1) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Default }
    $clean = $Text.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        $clean = $clean.Substring(2)
    }
    try { return [Convert]::ToInt64($clean, 16) }
    catch { return $Default }
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-BytesHex([byte[]]$Bytes) {
    return (($Bytes | ForEach-Object { $_.ToString("X2") }) -join "")
}

function Convert-WadOffsetToImageOffset([int64]$WadOffset) {
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    $sectorOffset = [int]($WadOffset % 2048)
    return ($sector * 2352L) + 24L + [int64]$sectorOffset
}

function Read-SceneSectorHeader([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 28) -gt $Ram.Length) { return $null }
    $numLpVertices = [int]$Ram[$Offset + 16]
    $numLpColours = [int]$Ram[$Offset + 17]
    $numLpFaces = [int]$Ram[$Offset + 18]
    $numHpVertices = [int]$Ram[$Offset + 20]
    $numHpColours = [int]$Ram[$Offset + 21]
    $numHpFaces = [int]$Ram[$Offset + 22]
    $sizeWords = 7 + $numLpVertices + $numLpColours + ($numLpFaces * 2) + $numHpVertices + ($numHpColours * 2) + ($numHpFaces * 4)
    $sizeBytes = $sizeWords * 4
    if ($sizeBytes -lt 28 -or $sizeBytes -gt 0x40000 -or ($Offset + $sizeBytes) -gt $Ram.Length) { return $null }
    if (($numLpVertices + $numHpVertices) -eq 0 -or ($numLpFaces + $numHpFaces) -eq 0) { return $null }
    return [pscustomobject]@{
        offset = $Offset
        centreRadiusAndFlags = [int](Get-UInt16LE $Ram ($Offset + 4))
        xyPos = [uint32](Get-UInt32LE $Ram ($Offset + 8))
        zPos = [uint32](Get-UInt32LE $Ram ($Offset + 12))
        sizeBytes = $sizeBytes
        numLpVertices = $numLpVertices
        numLpColours = $numLpColours
        numLpFaces = $numLpFaces
        numHpVertices = $numHpVertices
        numHpColours = $numHpColours
        numHpFaces = $numHpFaces
    }
}

function Get-SceneSectorBaseZ($Sector) {
    $zPos = [uint64]$Sector.zPos
    $sectorZ = [int](($zPos -shr 14) -band 0xFFFF)
    return [int]($sectorZ -shr 2)
}

function Test-FlatSceneSector($Sector) {
    return ((([int]$Sector.centreRadiusAndFlags -shr 12) -band 1) -eq 1)
}

function Convert-SceneVertexZ([uint32]$Word, $Sector) {
    $cur = [uint64]$Word
    $z = (Get-SceneSectorBaseZ $Sector) + [int]((($cur -shl 3) -band 0x1FFC) -shr 3)
    if (Test-FlatSceneSector $Sector) { $z = $z -shr 3 }
    return [double]$z
}

function Convert-SceneVertex([uint32]$Word, $Sector) {
    $cur = [uint64]$Word
    $xyPos = [uint64]$Sector.xyPos
    $zPos = [uint64]$Sector.zPos
    $sectorX = [int](($xyPos -shr 16) -band 0xFFFF)
    $sectorY = [int]($xyPos -band 0xFFFF)
    $sectorZ = [int](($zPos -shr 14) -band 0xFFFF)
    $sectorZ = $sectorZ -shr 2
    $x = $sectorX + [int]((($cur -shr 19) -band 0x1FFC) -shr 2)
    $y = $sectorY + [int]((($cur -shr 8) -band 0x1FFC) -shr 2)
    $z = $sectorZ + [int]((($cur -shl 3) -band 0x1FFC) -shr 3)
    if (Test-FlatSceneSector $Sector) { $z = $z -shr 3 }
    return [pscustomobject]([ordered]@{
        x = [double]$x
        y = [double]$y
        z = [double]$z
    })
}

function Set-SceneVertexZWord([uint32]$Word, $Sector, [double]$TargetZ) {
    $baseZ = Get-SceneSectorBaseZ $Sector
    if (Test-FlatSceneSector $Sector) {
        $encodedZ = [int][Math]::Round(($TargetZ * 8.0) - [double]$baseZ)
    }
    else {
        $encodedZ = [int][Math]::Round($TargetZ - [double]$baseZ)
    }
    if ($encodedZ -lt 0 -or $encodedZ -gt 1023) {
        throw ("Target Z {0:N2} cannot be encoded in sector 0x{1:X}; encoded delta would be {2}." -f $TargetZ, [int]$Sector.offset, $encodedZ)
    }
    return [uint32]((([uint64]$Word) -band [uint64]4294966272) -bor ([uint64]($encodedZ -band 0x3FF)))
}

function Get-SceneVertexOffset($Sector, [string]$DetailName, [int]$VertexIndex) {
    $dataStart = [int]$Sector.offset + 28
    if ([string]::Equals($DetailName, "lp", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($VertexIndex -lt 0 -or $VertexIndex -ge [int]$Sector.numLpVertices) { return -1 }
        return $dataStart + ($VertexIndex * 4)
    }
    if ([string]::Equals($DetailName, "hp", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($VertexIndex -lt 0 -or $VertexIndex -ge [int]$Sector.numHpVertices) { return -1 }
        $hpVertexStartWords = [int]$Sector.numLpVertices + [int]$Sector.numLpColours + ([int]$Sector.numLpFaces * 2)
        return $dataStart + (($hpVertexStartWords + $VertexIndex) * 4)
    }
    return -1
}

function Get-LpCollisionCompanionVertices([byte[]]$Ram, $Sector, [string]$DetailName, $VertexIndexes, [double]$Padding, [int]$MaxCount) {
    if (-not [string]::Equals($DetailName, "hp", [System.StringComparison]::OrdinalIgnoreCase)) { return @() }
    if ([int]$Sector.numLpVertices -le 0) { return @() }

    $hpCoords = New-Object System.Collections.ArrayList
    $seenHp = New-Object 'System.Collections.Generic.HashSet[int]'
    foreach ($rawIndex in @($VertexIndexes)) {
        $vertexIndex = [int]$rawIndex
        if (-not $seenHp.Add($vertexIndex)) { continue }
        $runtimeVertexOffset = Get-SceneVertexOffset $Sector "hp" $vertexIndex
        if ($runtimeVertexOffset -lt 0) { continue }
        $word = Get-UInt32LE $Ram $runtimeVertexOffset
        [void]$hpCoords.Add((Convert-SceneVertex $word $Sector))
    }
    if ($hpCoords.Count -le 0) { return @() }

    $minX = [double]::PositiveInfinity
    $maxX = [double]::NegativeInfinity
    $minY = [double]::PositiveInfinity
    $maxY = [double]::NegativeInfinity
    foreach ($coord in @($hpCoords.ToArray())) {
        $minX = [Math]::Min($minX, [double]$coord.x)
        $maxX = [Math]::Max($maxX, [double]$coord.x)
        $minY = [Math]::Min($minY, [double]$coord.y)
        $maxY = [Math]::Max($maxY, [double]$coord.y)
    }
    $centerX = ($minX + $maxX) / 2.0
    $centerY = ($minY + $maxY) / 2.0
    $minX -= $Padding
    $maxX += $Padding
    $minY -= $Padding
    $maxY += $Padding

    $lpCandidates = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt [int]$Sector.numLpVertices; $i++) {
        $runtimeVertexOffset = Get-SceneVertexOffset $Sector "lp" $i
        if ($runtimeVertexOffset -lt 0) { continue }
        $word = Get-UInt32LE $Ram $runtimeVertexOffset
        $coord = Convert-SceneVertex $word $Sector
        $dx = [double]$coord.x - $centerX
        $dy = [double]$coord.y - $centerY
        $inBounds = ([double]$coord.x -ge $minX -and [double]$coord.x -le $maxX -and [double]$coord.y -ge $minY -and [double]$coord.y -le $maxY)
        [void]$lpCandidates.Add([pscustomobject]([ordered]@{
            vertexIndex = $i
            runtimeVertexOffset = $runtimeVertexOffset
            word = [uint32]$word
            x = [double]$coord.x
            y = [double]$coord.y
            z = [double]$coord.z
            distance = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
            inExpandedHpBounds = $inBounds
        }))
    }

    $selected = @($lpCandidates.ToArray() | Where-Object { $_.inExpandedHpBounds } | Sort-Object distance, vertexIndex)
    if ($selected.Count -le 0) {
        $fallbackCount = [Math]::Min(4, [Math]::Max(1, $MaxCount))
        $selected = @($lpCandidates.ToArray() | Sort-Object distance, vertexIndex | Select-Object -First $fallbackCount)
    }
    elseif ($selected.Count -gt $MaxCount) {
        $selected = @($selected | Select-Object -First $MaxCount)
    }
    return @($selected)
}

function Set-ImageBytesForWadOffset([byte[]]$Image, [int64]$WadOffset, [byte[]]$Bytes) {
    for ($i = 0; $i -lt $Bytes.Length; $i++) {
        $imageOffset = Convert-WadOffsetToImageOffset ($WadOffset + [int64]$i)
        if ($imageOffset -lt 0 -or $imageOffset -ge $Image.Length) {
            throw ("Image offset 0x{0:X} is outside image." -f $imageOffset)
        }
        $Image[$imageOffset] = $Bytes[$i]
    }
}

function Get-ImageBytesForWadOffset([byte[]]$Image, [int64]$WadOffset, [int]$Length) {
    $result = New-Object byte[] $Length
    for ($i = 0; $i -lt $Length; $i++) {
        $imageOffset = Convert-WadOffsetToImageOffset ($WadOffset + [int64]$i)
        if ($imageOffset -lt 0 -or $imageOffset -ge $Image.Length) {
            throw ("Image offset 0x{0:X} is outside image." -f $imageOffset)
        }
        $result[$i] = $Image[$imageOffset]
    }
    return $result
}

function Write-Cue([string]$BinPath, [string]$CueOutPath) {
    $fileName = [System.IO.Path]::GetFileName($BinPath)
    $lines = @(
        "FILE `"$fileName`" BINARY",
        "  TRACK 01 MODE2/2352",
        "    INDEX 01 00:00:00"
    )
    [System.IO.File]::WriteAllLines($CueOutPath, $lines, [System.Text.Encoding]::ASCII)
}

function Write-Markdown($Plan, [string]$Path) {
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add("# Stone Hill Runtime Terrain Patch Plan")
    [void]$lines.Add("")
    [void]$lines.Add("Generated: $($Plan.generatedAt)")
    [void]$lines.Add("")
    [void]$lines.Add(("Status: ``{0}``" -f $Plan.status))
    [void]$lines.Add("")
    [void]$lines.Add("- Terrain edits: $($Plan.terrainEditCount)")
    [void]$lines.Add("- Sector patches: $($Plan.sectorPatchCount)")
    [void]$lines.Add("- Vertex patches: $($Plan.vertexPatchCount)")
    [void]$lines.Add("- Collision LP companion patches: $($Plan.collisionLpPatchCount)")
    [void]$lines.Add("- BIN written: $($Plan.wroteBin)")
    [void]$lines.Add("")
    [void]$lines.Add("| # | Kind | Edit | Source sector | Vertex | Source WAD | Runtime Z | Target Z | Bytes |")
    [void]$lines.Add("|---:|---|---|---|---:|---|---:|---:|---|")
    $i = 1
    foreach ($patch in @($Plan.vertexPatches)) {
        [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6:N2} | {7:N2} | {8}->{9} |" -f $i, $patch.kind, $patch.runtimeKey, $patch.sourceSectorWadOffset, $patch.vertexIndex, $patch.sourceVertexWadOffset, [double]$patch.originalZ, [double]$patch.targetZ, $patch.oldBytes, $patch.newBytes))
        $i++
    }
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedImage = Resolve-WorkspacePath $ImagePath
$resolvedRam = Resolve-WorkspacePath $RamPath
$resolvedEdits = Resolve-WorkspacePath $TerrainEditsPath
$resolvedSearch = Resolve-WorkspacePath $SourceSearchPath
$resolvedOut = Resolve-WorkspacePath $OutPath
$resolvedPlan = Resolve-WorkspacePath $PlanPath
$resolvedMd = Resolve-WorkspacePath $MarkdownPath
$resolvedCue = if ([string]::IsNullOrWhiteSpace($CuePath)) { [System.IO.Path]::ChangeExtension($resolvedOut, ".cue") } else { Resolve-WorkspacePath $CuePath }

if (-not (Test-Path -LiteralPath $resolvedImage)) { throw "Missing image: $resolvedImage" }
if (-not (Test-Path -LiteralPath $resolvedRam)) { throw "Missing RAM dump: $resolvedRam" }
if (-not (Test-Path -LiteralPath $resolvedEdits)) { throw "Missing terrain edits: $resolvedEdits" }
if (-not (Test-Path -LiteralPath $resolvedSearch)) { throw "Missing source search report: $resolvedSearch. Run Find-StoneHillRuntimeTerrainSource.ps1 first." }

$ram = [System.IO.File]::ReadAllBytes($resolvedRam)
$editsRoot = Get-Content -LiteralPath $resolvedEdits -Raw | ConvertFrom-Json
$searchRoot = Get-Content -LiteralPath $resolvedSearch -Raw | ConvertFrom-Json
$edits = @(Get-ArrayField $editsRoot "edits")
$searchResults = @{}
foreach ($item in @(Get-ArrayField $searchRoot "results")) {
    $searchResults[[string](Get-Field $item "edit" "")] = $item
}

$vertexPatches = New-Object System.Collections.ArrayList
$sectorPatchCount = 0
foreach ($edit in $edits) {
    $runtimeKey = [string](Get-Field $edit "runtimeKey" "")
    if (-not $searchResults.ContainsKey($runtimeKey)) { throw "No source-search result for edit $runtimeKey." }
    $search = $searchResults[$runtimeKey]
    $sectorHits = @(Get-ArrayField $search "fullSectorHits")
    if ($sectorHits.Count -ne 1) {
        throw "Edit $runtimeKey needs exactly one full-sector source hit, found $($sectorHits.Count)."
    }
    $sourceSectorWadOffset = Convert-HexTextToInt64 ([string](Get-Field $sectorHits[0] "wadOffset" ""))
    if ($sourceSectorWadOffset -lt 0) { throw "Bad source sector WAD offset for edit $runtimeKey." }

    $sectorOffset = [int](Convert-HexTextToInt64 ([string](Get-Field $edit "sectorOffset" "")))
    $detail = [string](Get-Field $edit "detail" "hp")
    $sector = Read-SceneSectorHeader $ram $sectorOffset
    if ($null -eq $sector) { throw "Runtime sector $($edit.sectorOffset) no longer parses." }

    $vertexIndexes = @(Get-ArrayField $edit "vertexIndexes")
    $editedZ = @(Get-ArrayField $edit "editedZ")
    $originalZ = @(Get-ArrayField $edit "originalZ")
    $seen = New-Object 'System.Collections.Generic.HashSet[int]'
    for ($i = 0; $i -lt $vertexIndexes.Count; $i++) {
        $vertexIndex = [int]$vertexIndexes[$i]
        if (-not $seen.Add($vertexIndex)) { continue }
        $runtimeVertexOffset = Get-SceneVertexOffset $sector $detail $vertexIndex
        if ($runtimeVertexOffset -lt 0) { throw "Vertex $vertexIndex is not valid for $runtimeKey." }
        $sourceVertexWadOffset = $sourceSectorWadOffset + [int64]($runtimeVertexOffset - $sectorOffset)
        $oldWord = Get-UInt32LE $ram $runtimeVertexOffset
        $targetZ = [double]$editedZ[$i]
        $newWord = Set-SceneVertexZWord $oldWord $sector $targetZ
        $oldBytes = [BitConverter]::GetBytes([uint32]$oldWord)
        $newBytes = [BitConverter]::GetBytes([uint32]$newWord)

        [void]$vertexPatches.Add([ordered]@{
            runtimeKey = $runtimeKey
            kind = "visual-$detail"
            sectorIndex = [int](Get-Field $edit "sectorIndex" -1)
            faceIndex = [int](Get-Field $edit "faceIndex" -1)
            detail = $detail
            sourceSectorWadOffset = ("0x{0:X}" -f $sourceSectorWadOffset)
            sourceVertexWadOffset = ("0x{0:X}" -f $sourceVertexWadOffset)
            sourceVertexImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $sourceVertexWadOffset))
            runtimeSectorOffset = ("0x{0:X}" -f $sectorOffset)
            runtimeVertexOffset = ("0x{0:X}" -f $runtimeVertexOffset)
            vertexIndex = $vertexIndex
            originalZ = if ($i -lt $originalZ.Count) { [double]$originalZ[$i] } else { Convert-SceneVertexZ $oldWord $sector }
            decodedOriginalZ = Convert-SceneVertexZ $oldWord $sector
            targetZ = $targetZ
            oldBytes = Get-BytesHex $oldBytes
            newBytes = Get-BytesHex $newBytes
            oldWord = ("0x{0:X8}" -f $oldWord)
            newWord = ("0x{0:X8}" -f $newWord)
        })
    }

    if ($IncludeCollisionLp) {
        $deltaZValue = [double](Get-Field $edit "deltaZ" 0.0)
        $companions = @(Get-LpCollisionCompanionVertices $ram $sector $detail $vertexIndexes $CollisionLpPadding $MaxCollisionLpVerticesPerEdit)
        foreach ($companion in $companions) {
            $runtimeVertexOffset = [int]$companion.runtimeVertexOffset
            $sourceVertexWadOffset = $sourceSectorWadOffset + [int64]($runtimeVertexOffset - $sectorOffset)
            $oldWord = [uint32]$companion.word
            $targetZ = [double]$companion.z + $deltaZValue
            $newWord = Set-SceneVertexZWord $oldWord $sector $targetZ
            $oldBytes = [BitConverter]::GetBytes([uint32]$oldWord)
            $newBytes = [BitConverter]::GetBytes([uint32]$newWord)

            [void]$vertexPatches.Add([ordered]@{
                runtimeKey = $runtimeKey
                kind = "collision-lp-candidate"
                sectorIndex = [int](Get-Field $edit "sectorIndex" -1)
                faceIndex = [int](Get-Field $edit "faceIndex" -1)
                detail = "lp"
                sourceSectorWadOffset = ("0x{0:X}" -f $sourceSectorWadOffset)
                sourceVertexWadOffset = ("0x{0:X}" -f $sourceVertexWadOffset)
                sourceVertexImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $sourceVertexWadOffset))
                runtimeSectorOffset = ("0x{0:X}" -f $sectorOffset)
                runtimeVertexOffset = ("0x{0:X}" -f $runtimeVertexOffset)
                vertexIndex = [int]$companion.vertexIndex
                originalZ = [double]$companion.z
                decodedOriginalZ = Convert-SceneVertexZ $oldWord $sector
                targetZ = $targetZ
                deltaZ = $deltaZValue
                x = [double]$companion.x
                y = [double]$companion.y
                distanceFromHpFaceCenter = [double]$companion.distance
                inExpandedHpBounds = [bool]$companion.inExpandedHpBounds
                oldBytes = Get-BytesHex $oldBytes
                newBytes = Get-BytesHex $newBytes
                oldWord = ("0x{0:X8}" -f $oldWord)
                newWord = ("0x{0:X8}" -f $newWord)
            })
        }
    }
    $sectorPatchCount++
}

$mergedVertexPatches = New-Object System.Collections.ArrayList
$patchesByWadOffset = @{}
foreach ($patch in @($vertexPatches.ToArray())) {
    $key = [string]$patch.sourceVertexWadOffset
    if (-not $patchesByWadOffset.ContainsKey($key)) {
        $patchesByWadOffset[$key] = $patch
        [void]$mergedVertexPatches.Add($patch)
        continue
    }

    $existing = $patchesByWadOffset[$key]
    if ([string]$existing.oldBytes -ne [string]$patch.oldBytes -or [string]$existing.newBytes -ne [string]$patch.newBytes) {
        throw ("Conflicting terrain vertex edits target {0}: {1}->{2} and {3}->{4}." -f $key, $existing.oldBytes, $existing.newBytes, $patch.oldBytes, $patch.newBytes)
    }
}

$wroteBin = $false
if ($AllowExperimentalWrite) {
    $image = [System.IO.File]::ReadAllBytes($resolvedImage)
    foreach ($patch in @($mergedVertexPatches.ToArray())) {
        $wadOffset = Convert-HexTextToInt64 ([string]$patch.sourceVertexWadOffset)
        $existing = Get-ImageBytesForWadOffset $image $wadOffset 4
        if ((Get-BytesHex $existing) -ne [string]$patch.oldBytes) {
            throw ("Source bytes at {0} are {1}, expected {2}." -f $patch.sourceVertexWadOffset, (Get-BytesHex $existing), $patch.oldBytes)
        }
        $newBytes = New-Object byte[] 4
        $hex = [string]$patch.newBytes
        for ($i = 0; $i -lt 4; $i++) {
            $newBytes[$i] = [byte][Convert]::ToInt32($hex.Substring($i * 2, 2), 16)
        }
        Set-ImageBytesForWadOffset $image $wadOffset $newBytes
    }
    [System.IO.File]::WriteAllBytes($resolvedOut, $image)
    Write-Cue $resolvedOut $resolvedCue
    $wroteBin = $true
}
elseif (-not $PlanOnly) {
    Write-Warning "PlanOnly was not set, but AllowExperimentalWrite was also not set. Wrote a plan only."
}

$collisionLpPatchCount = @($mergedVertexPatches.ToArray() | Where-Object { [string]$_.kind -eq "collision-lp-candidate" }).Count
$visualPatchCount = @($mergedVertexPatches.ToArray() | Where-Object { [string]$_.kind -like "visual-*" }).Count

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    status = if ($wroteBin) { "experimental-runtime-sector-bin-written" } else { "plan-ready" }
    note = "Patches the exact serialized runtime scene-sector bytes found in the logical WAD stream, not the fuzzy visual model source map."
    terrainEditCount = $edits.Count
    sectorPatchCount = $sectorPatchCount
    vertexPatchCount = @($mergedVertexPatches.ToArray()).Count
    visualPatchCount = $visualPatchCount
    collisionLpPatchCount = $collisionLpPatchCount
    includeCollisionLp = [bool]$IncludeCollisionLp
    collisionLpPadding = [double]$CollisionLpPadding
    maxCollisionLpVerticesPerEdit = [int]$MaxCollisionLpVerticesPerEdit
    rawVertexPatchCount = @($vertexPatches.ToArray()).Count
    imagePath = (Resolve-Path -LiteralPath $resolvedImage).Path
    ramPath = (Resolve-Path -LiteralPath $resolvedRam).Path
    terrainEditsPath = (Resolve-Path -LiteralPath $resolvedEdits).Path
    sourceSearchPath = (Resolve-Path -LiteralPath $resolvedSearch).Path
    outPath = $resolvedOut
    cuePath = $resolvedCue
    wroteBin = $wroteBin
    vertexPatches = @($mergedVertexPatches.ToArray())
}

$plan | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedPlan -Encoding UTF8
Write-Markdown ([pscustomobject]$plan) $resolvedMd

Write-Host "Wrote runtime terrain patch plan to $resolvedPlan"
Write-Host "Wrote runtime terrain patch summary to $resolvedMd"
if ($wroteBin) {
    Write-Host "Wrote runtime terrain patch BIN to $resolvedOut"
    Write-Host "Wrote CUE to $resolvedCue"
}
