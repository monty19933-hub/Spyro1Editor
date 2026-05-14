param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$SourceSearchPath = ".\stonehill-runtime-terrain-source-search.json",
    [string]$CollisionReportPath = ".\stonehill-platform-collision-bridge-report.json",
    [string]$OutPath = ".\Spyro the Dragon (USA)-runtime-terrain-platformpatchtest.bin",
    [string]$CuePath = "",
    [string]$SceneRuntimeAddress = "0x8008C134",
    [string]$CollisionTriangleWadBaseOffset = "0xCE4338",
    [string]$RelocationSearchStart = "0x8C134",
    [string]$RelocationSearchEnd = "0x180000",
    [int]$WadLba = 37,
    [switch]$PlanOnly
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
        return [Convert]::ToInt64($clean.Substring(2), 16)
    }
    return [int64]$clean
}

function Convert-PsxAddressToRamOffset([uint32]$Address, [int]$RamLength) {
    $ramBase = [Convert]::ToUInt32("80000000", 16)
    $ramEnd = [Convert]::ToUInt32("80200000", 16)
    if ($Address -lt $ramBase -or $Address -ge $ramEnd) { return -1 }
    $offset = [int]($Address - $ramBase)
    if ($offset -lt 0 -or $offset -ge $RamLength) { return -1 }
    return $offset
}

function Convert-WadOffsetToImageOffset([int64]$WadOffset) {
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    $sectorOffset = [int]($WadOffset % 2048)
    return ($sector * 2352L) + 24L + [int64]$sectorOffset
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

function Copy-Bytes([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Offset, $result, 0, $Length)
    return $result
}

function Write-Cue([string]$BinPath, [string]$CueOutPath) {
    $fileName = [System.IO.Path]::GetFileName($BinPath)
    @(
        "FILE `"$fileName`" BINARY",
        "  TRACK 01 MODE2/2352",
        "    INDEX 01 00:00:00"
    ) | Set-Content -LiteralPath $CueOutPath -Encoding ASCII
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
    return [pscustomobject]([ordered]@{
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
    })
}

function Get-SceneSectorBaseZ($Sector) {
    $zPos = [uint64]$Sector.zPos
    $sectorZ = [int](($zPos -shr 14) -band 0xFFFF)
    return [int]($sectorZ -shr 2)
}

function Test-FlatSceneSector($Sector) {
    return ((([int]$Sector.centreRadiusAndFlags -shr 12) -band 1) -eq 1)
}

function Set-SceneVertexZWord([uint32]$Word, $Sector, [double]$TargetZ) {
    $baseZ = Get-SceneSectorBaseZ $Sector
    if (Test-FlatSceneSector $Sector) {
        $encodedZ = [int][Math]::Round(($TargetZ * 8.0) - [double]$baseZ)
    } else {
        $encodedZ = [int][Math]::Round($TargetZ - [double]$baseZ)
    }
    if ($encodedZ -lt 0 -or $encodedZ -gt 1023) {
        throw ("Target Z {0:N2} cannot be encoded in sector 0x{1:X}; encoded delta would be {2}." -f $TargetZ, [int]$Sector.offset, $encodedZ)
    }
    return [uint32]((([uint64]$Word) -band [uint64]4294966272) -bor ([uint64]($encodedZ -band 0x3FF)))
}

function Get-SceneVertexOffset($Sector, [string]$DetailName, [int]$VertexIndex) {
    $dataStart = [int]$Sector.offset + 28
    if ([string]::Equals($DetailName, "hp", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($VertexIndex -lt 0 -or $VertexIndex -ge [int]$Sector.numHpVertices) { return -1 }
        $hpVertexStartWords = [int]$Sector.numLpVertices + [int]$Sector.numLpColours + ([int]$Sector.numLpFaces * 2)
        return $dataStart + (($hpVertexStartWords + $VertexIndex) * 4)
    }
    return -1
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

function Find-MappedZeroRun([byte[]]$Ram, [byte[]]$Image, [int64]$RuntimeToWadDelta, [int]$StartOffset, [int]$EndOffset, [int]$NeededBytes) {
    if ($StartOffset -lt 0) { $StartOffset = 0 }
    if ($EndOffset -gt $Ram.Length) { $EndOffset = $Ram.Length }
    if ($EndOffset -le $StartOffset) { throw "Bad relocation scan range." }

    $best = $null
    $i = $StartOffset
    while ($i -lt $EndOffset) {
        $b = $Ram[$i]
        if ($b -ne 0 -and $b -ne 0xFF) {
            $i++
            continue
        }

        $runStart = $i
        while ($i -lt $EndOffset -and $Ram[$i] -eq $b) {
            $wadOffset = [int64]$i + $RuntimeToWadDelta
            $imageOffset = Convert-WadOffsetToImageOffset $wadOffset
            if ($imageOffset -lt 0 -or $imageOffset -ge $Image.Length -or $Image[$imageOffset] -ne $b) { break }
            $i++
        }
        $runLength = $i - $runStart
        $alignedStart = ($runStart + 3) -band (-bnot 3)
        $alignedLoss = $alignedStart - $runStart
        $alignedLength = $runLength - $alignedLoss
        if ($alignedLength -ge $NeededBytes) {
            $candidate = [pscustomobject]([ordered]@{
                runtimeOffset = $alignedStart
                wadOffset = [int64]$alignedStart + $RuntimeToWadDelta
                byteValue = $b
                rawRuntimeOffset = $runStart
                rawLength = $runLength
                alignedLength = $alignedLength
            })
            if ($null -eq $best -or [int]$candidate.alignedLength -gt [int]$best.alignedLength) {
                $best = $candidate
            }
        }
        if ($i -eq $runStart) { $i++ }
    }
    return $best
}

function New-FaceRecord([int[]]$VertexIndexes, [int[]]$ColourIndexes, [byte[]]$TemplateBytes) {
    if ($VertexIndexes.Count -ne 4 -or $ColourIndexes.Count -ne 4) { throw "Face records need four vertex indexes and four colour indexes." }
    $bytes = New-Object byte[] 16
    [Array]::Copy($TemplateBytes, 0, $bytes, 0, 16)
    for ($i = 0; $i -lt 4; $i++) {
        if ($VertexIndexes[$i] -lt 0 -or $VertexIndexes[$i] -gt 255) { throw "Vertex index cannot fit in HP face byte: $($VertexIndexes[$i])" }
        if ($ColourIndexes[$i] -lt 0 -or $ColourIndexes[$i] -gt 255) { throw "Colour index cannot fit in HP face byte: $($ColourIndexes[$i])" }
        $bytes[$i] = [byte]$VertexIndexes[$i]
        $bytes[4 + $i] = [byte]$ColourIndexes[$i]
    }
    return $bytes
}

function New-ExpandedPlatformSectorBytes([byte[]]$Ram, $Sector, $Edit, [int]$AvailableBytes) {
    $sectorOffset = [int]$Sector.offset
    $oldSize = [int]$Sector.sizeBytes
    $dataStartRelative = 28
    $lpVertexBytes = [int]$Sector.numLpVertices * 4
    $lpColourBytes = [int]$Sector.numLpColours * 4
    $lpFaceBytes = [int]$Sector.numLpFaces * 8
    $hpVertexStart = $dataStartRelative + $lpVertexBytes + $lpColourBytes + $lpFaceBytes
    $hpVertexBytes = [int]$Sector.numHpVertices * 4
    $hpColourStart = $hpVertexStart + $hpVertexBytes
    $hpColourBytes = [int]$Sector.numHpColours * 8
    $hpFaceStart = $hpColourStart + $hpColourBytes
    $hpFaceBytes = [int]$Sector.numHpFaces * 16
    if (($hpFaceStart + $hpFaceBytes) -ne $oldSize) { throw "Sector layout math did not land on sector size." }

    $oldVertexIndexes = @(Get-ArrayField $Edit "vertexIndexes" | ForEach-Object { [int]$_ })
    $editedZ = @(Get-ArrayField $Edit "editedZ" | ForEach-Object { [double]$_ })
    if ($oldVertexIndexes.Count -ne 4 -or $editedZ.Count -lt 4) {
        throw "Platform topology test expects one quad terrain edit with four vertex indexes and edited Z values."
    }

    $newVertexCount = [int]$Sector.numHpVertices + 4
    $newFaceCount = [int]$Sector.numHpFaces + 5
    if ($newVertexCount -gt 255 -or $newFaceCount -gt 255) { throw "Expanded sector counts cannot fit in byte fields." }
    $newSize = $oldSize + (4 * 4) + (5 * 16)
    if ($AvailableBytes -gt 0 -and $newSize -gt $AvailableBytes) {
        throw ("Expanded sector would be {0} bytes but only {1} bytes are available before the next sector." -f $newSize, $AvailableBytes)
    }

    $newBytes = New-Object byte[] $newSize
    [Array]::Copy($Ram, $sectorOffset, $newBytes, 0, 28)
    $newBytes[20] = [byte]$newVertexCount
    $newBytes[22] = [byte]$newFaceCount

    $src = $sectorOffset + $dataStartRelative
    [Array]::Copy($Ram, $src, $newBytes, $dataStartRelative, $lpVertexBytes + $lpColourBytes + $lpFaceBytes)

    $newHpVertexStart = $hpVertexStart
    [Array]::Copy($Ram, $sectorOffset + $hpVertexStart, $newBytes, $newHpVertexStart, $hpVertexBytes)
    $newTopIndexes = @()
    for ($i = 0; $i -lt 4; $i++) {
        $oldVertexIndex = $oldVertexIndexes[$i]
        $oldVertexOffset = Get-SceneVertexOffset $Sector "hp" $oldVertexIndex
        if ($oldVertexOffset -lt 0) { throw "Bad HP vertex index $oldVertexIndex." }
        $oldWord = [uint32](Get-UInt32LE $Ram $oldVertexOffset)
        $newWord = Set-SceneVertexZWord $oldWord $Sector ([double]$editedZ[$i])
        [Array]::Copy([BitConverter]::GetBytes([uint32]$newWord), 0, $newBytes, $newHpVertexStart + $hpVertexBytes + ($i * 4), 4)
        $newTopIndexes += ([int]$Sector.numHpVertices + $i)
    }

    $newHpColourStart = $newHpVertexStart + ($newVertexCount * 4)
    [Array]::Copy($Ram, $sectorOffset + $hpColourStart, $newBytes, $newHpColourStart, $hpColourBytes)

    $newHpFaceStart = $newHpColourStart + $hpColourBytes
    [Array]::Copy($Ram, $sectorOffset + $hpFaceStart, $newBytes, $newHpFaceStart, $hpFaceBytes)

    $faceIndex = [int](Get-Field $Edit "faceIndex" -1)
    if ($faceIndex -lt 0 -or $faceIndex -ge [int]$Sector.numHpFaces) { throw "Bad HP face index $faceIndex." }
    $selectedFaceOffset = $sectorOffset + $hpFaceStart + ($faceIndex * 16)
    $templateFace = Copy-Bytes $Ram $selectedFaceOffset 16
    $selectedColours = @([int]$templateFace[4], [int]$templateFace[5], [int]$templateFace[6], [int]$templateFace[7])

    $appendOffset = $newHpFaceStart + $hpFaceBytes
    $newFaces = New-Object System.Collections.ArrayList
    [void]$newFaces.Add((New-FaceRecord ([int[]]@($newTopIndexes[0], $newTopIndexes[1], $newTopIndexes[2], $newTopIndexes[3])) ([int[]]$selectedColours) $templateFace))
    for ($i = 0; $i -lt 4; $i++) {
        $j = ($i + 1) % 4
        $sideVertices = [int[]]@($oldVertexIndexes[$i], $oldVertexIndexes[$j], $newTopIndexes[$j], $newTopIndexes[$i])
        $sideColours = [int[]]@($selectedColours[$i], $selectedColours[$j], $selectedColours[$j], $selectedColours[$i])
        [void]$newFaces.Add((New-FaceRecord $sideVertices $sideColours $templateFace))
    }
    for ($i = 0; $i -lt $newFaces.Count; $i++) {
        [Array]::Copy([byte[]]$newFaces[$i], 0, $newBytes, $appendOffset + ($i * 16), 16)
    }

    return [pscustomobject]([ordered]@{
        bytes = $newBytes
        oldSize = $oldSize
        newSize = $newSize
        availableBytes = $AvailableBytes
        oldHpVertices = [int]$Sector.numHpVertices
        newHpVertices = $newVertexCount
        oldHpFaces = [int]$Sector.numHpFaces
        newHpFaces = $newFaceCount
        duplicatedVertexIndexes = @($newTopIndexes)
        addedFaceIndexes = @(($Sector.numHpFaces)..($newFaceCount - 1))
    })
}

$findTerrainScript = Join-Path $PSScriptRoot "Find-StoneHillRuntimeTerrainSource.ps1"
$collisionReportScript = Join-Path $PSScriptRoot "New-StoneHillCollisionBridgeReport.ps1"
$collisionPatchScript = Join-Path $PSScriptRoot "Apply-StoneHillCollisionTrianglePatch.ps1"
foreach ($script in @($findTerrainScript, $collisionReportScript, $collisionPatchScript)) {
    if (-not (Test-Path -LiteralPath $script)) { throw "Missing required script: $script" }
}

$resolvedImage = Resolve-WorkspacePath $ImagePath
$resolvedRam = Resolve-WorkspacePath $RamPath
$resolvedEdits = Resolve-WorkspacePath $TerrainEditsPath
$resolvedSourceSearch = Resolve-WorkspacePath $SourceSearchPath
$resolvedCollisionReport = Resolve-WorkspacePath $CollisionReportPath
$resolvedOut = Resolve-WorkspacePath $OutPath
$resolvedCue = if ([string]::IsNullOrWhiteSpace($CuePath)) { [System.IO.Path]::ChangeExtension($resolvedOut, ".cue") } else { Resolve-WorkspacePath $CuePath }

Write-Host "Searching WAD for exact runtime terrain sector..."
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $findTerrainScript `
    -ImagePath $resolvedImage `
    -RamPath $resolvedRam `
    -TerrainEditsPath $resolvedEdits `
    -QuickSectorOnly `
    -OutJsonPath $resolvedSourceSearch `
    -OutMarkdownPath (Resolve-WorkspacePath ".\stonehill-runtime-terrain-source-search.md")
if ($LASTEXITCODE -ne 0) { throw "Runtime terrain source search failed." }

$ram = [System.IO.File]::ReadAllBytes($resolvedRam)
$image = [System.IO.File]::ReadAllBytes($resolvedImage)
$editsRoot = Get-Content -LiteralPath $resolvedEdits -Raw | ConvertFrom-Json
$edits = @(Get-ArrayField $editsRoot "edits")
if ($edits.Count -ne 1) { throw "Platform topology test currently expects exactly one saved terrain edit." }
$edit = $edits[0]

$searchRoot = Get-Content -LiteralPath $resolvedSourceSearch -Raw | ConvertFrom-Json
$result = @(Get-ArrayField $searchRoot "results" | Where-Object { [string](Get-Field $_ "edit" "") -eq [string](Get-Field $edit "runtimeKey" "") }) | Select-Object -First 1
if ($null -eq $result) { throw "No source-search result found for terrain edit." }
$sectorHits = @(Get-ArrayField $result "fullSectorHits")
if ($sectorHits.Count -ne 1) { throw "Terrain edit needs exactly one source-sector hit, found $($sectorHits.Count)." }
$sourceSectorWadOffset = Convert-HexTextToInt64 ([string](Get-Field $sectorHits[0] "wadOffset" ""))

$sectorOffset = [int](Convert-HexTextToInt64 ([string](Get-Field $edit "sectorOffset" "")))
$sectorIndex = [int](Get-Field $edit "sectorIndex" -1)
$sector = Read-SceneSectorHeader $ram $sectorOffset
if ($null -eq $sector) { throw "Could not parse source runtime sector." }

$sceneAddressValue = [uint32](Convert-HexTextToInt64 $SceneRuntimeAddress)
$sceneOffset = Convert-PsxAddressToRamOffset $sceneAddressValue $ram.Length
if ($sceneOffset -lt 0) { throw "SceneRuntimeAddress is outside main RAM." }
$numSectors = [int](Get-UInt32LE $ram ($sceneOffset + 8))
if ($sectorIndex -lt 0 -or $sectorIndex -ge $numSectors) { throw "Bad sector index $sectorIndex." }
if (($sectorIndex + 1) -ge $numSectors) { throw "Cannot infer available sector padding for the final scene sector." }
$thisPtr = [uint32](Get-UInt32LE $ram ($sceneOffset + 12 + ($sectorIndex * 4)))
$nextPtr = [uint32](Get-UInt32LE $ram ($sceneOffset + 12 + (($sectorIndex + 1) * 4)))
$thisOffset = Convert-PsxAddressToRamOffset $thisPtr $ram.Length
$nextOffset = Convert-PsxAddressToRamOffset $nextPtr $ram.Length
if ($thisOffset -ne $sectorOffset -or $nextOffset -le $sectorOffset) {
    throw "Scene pointer table did not match terrain edit sector offset."
}
$availableBytes = $nextOffset - $sectorOffset

$expanded = New-ExpandedPlatformSectorBytes $ram $sector $edit 0
$oldSectorBytes = Copy-Bytes $ram $sectorOffset ([int]$sector.sizeBytes)
$existing = Get-ImageBytesForWadOffset $image $sourceSectorWadOffset ([int]$sector.sizeBytes)
if ((Get-BytesHex $existing) -ne (Get-BytesHex $oldSectorBytes)) {
    throw ("Source sector bytes at 0x{0:X} did not match the clean RAM sector." -f $sourceSectorWadOffset)
}

$runtimeToWadDelta = $sourceSectorWadOffset - [int64]$sectorOffset
$sceneWadOffset = [int64]$sceneOffset + $runtimeToWadDelta
$scenePointerEntryWadOffset = $sceneWadOffset + 12L + ([int64]$sectorIndex * 4L)
$oldRelativeOffset = [uint32]($sectorOffset - $sceneOffset - 8)
$oldRelativeBytes = [BitConverter]::GetBytes([uint32]$oldRelativeOffset)
$existingRelativeBytes = Get-ImageBytesForWadOffset $image $scenePointerEntryWadOffset 4
if ((Get-BytesHex $existingRelativeBytes) -ne (Get-BytesHex $oldRelativeBytes)) {
    throw ("Scene pointer entry at 0x{0:X} did not match expected relative offset 0x{1:X8}; saw {2}." -f $scenePointerEntryWadOffset, $oldRelativeOffset, (Get-BytesHex $existingRelativeBytes))
}

$writeWadOffset = $sourceSectorWadOffset
$writeRuntimeOffset = $sectorOffset
$newRelativeOffset = $oldRelativeOffset
$relocated = $false
$relocation = $null
if ([int]$expanded.newSize -gt $availableBytes) {
    $searchStart = [int](Convert-HexTextToInt64 $RelocationSearchStart)
    $searchEnd = [int](Convert-HexTextToInt64 $RelocationSearchEnd)
    $relocation = Find-MappedZeroRun $ram $image $runtimeToWadDelta $searchStart $searchEnd ([int]$expanded.newSize)
    if ($null -eq $relocation) {
        throw ("Expanded sector would be {0} bytes but only {1} bytes are available in-place, and no mapped zero run was found." -f $expanded.newSize, $availableBytes)
    }
    $writeRuntimeOffset = [int]$relocation.runtimeOffset
    $writeWadOffset = [int64]$relocation.wadOffset
    $newRelativeOffset = [uint32]($writeRuntimeOffset - $sceneOffset - 8)
    $relocated = $true
}

if (-not $PlanOnly) {
    if ($relocated) {
        Set-ImageBytesForWadOffset $image $scenePointerEntryWadOffset ([BitConverter]::GetBytes([uint32]$newRelativeOffset))
    }
    Set-ImageBytesForWadOffset $image $writeWadOffset ([byte[]]$expanded.bytes)
    [System.IO.File]::WriteAllBytes($resolvedOut, $image)
    Write-Cue $resolvedOut $resolvedCue
}

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    status = if ($PlanOnly) { "plan-ready" } else { "platform-sector-bin-written-before-collision" }
    terrainEditsPath = (Resolve-Path -LiteralPath $resolvedEdits).Path
    sourceSectorWadOffset = ("0x{0:X}" -f $sourceSectorWadOffset)
    writeSectorWadOffset = ("0x{0:X}" -f $writeWadOffset)
    sectorIndex = $sectorIndex
    runtimeSectorOffset = ("0x{0:X}" -f $sectorOffset)
    writeRuntimeSectorOffset = ("0x{0:X}" -f $writeRuntimeOffset)
    nextSectorOffset = ("0x{0:X}" -f $nextOffset)
    sceneWadOffset = ("0x{0:X}" -f $sceneWadOffset)
    scenePointerEntryWadOffset = ("0x{0:X}" -f $scenePointerEntryWadOffset)
    oldRelativeOffset = ("0x{0:X8}" -f [uint32]$oldRelativeOffset)
    newRelativeOffset = ("0x{0:X8}" -f [uint32]$newRelativeOffset)
    relocated = [bool]$relocated
    relocationByteValue = if ($null -ne $relocation) { ("0x{0:X2}" -f [byte]$relocation.byteValue) } else { "" }
    relocationRunLength = if ($null -ne $relocation) { [int]$relocation.alignedLength } else { 0 }
    oldSectorSize = [int]$expanded.oldSize
    newSectorSize = [int]$expanded.newSize
    availableBytes = [int]$expanded.availableBytes
    oldHpVertices = [int]$expanded.oldHpVertices
    newHpVertices = [int]$expanded.newHpVertices
    oldHpFaces = [int]$expanded.oldHpFaces
    newHpFaces = [int]$expanded.newHpFaces
    duplicatedVertexIndexes = @($expanded.duplicatedVertexIndexes)
    addedFaceIndexes = @($expanded.addedFaceIndexes)
    outPath = $resolvedOut
    cuePath = $resolvedCue
}
$planPath = "$resolvedOut.platformpatchplan.json"
$planMd = "$resolvedOut.platformpatchplan.md"
$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding UTF8
@(
    "# Stone Hill Platform Topology Patch Plan",
    "",
    "Generated: $($plan.generatedAt)",
    "",
    "- Status: $($plan.status)",
    "- Sector: $($plan.sectorIndex) at $($plan.runtimeSectorOffset)",
    "- Source sector WAD: $($plan.sourceSectorWadOffset)",
    "- Write sector WAD: $($plan.writeSectorWadOffset)",
    "- Relocated: $($plan.relocated)",
    "- Scene pointer entry WAD: $($plan.scenePointerEntryWadOffset)",
    "- Relative offset: $($plan.oldRelativeOffset) -> $($plan.newRelativeOffset)",
    "- Sector bytes: $($plan.oldSectorSize) -> $($plan.newSectorSize) of $($plan.availableBytes) available",
    "- HP vertices: $($plan.oldHpVertices) -> $($plan.newHpVertices)",
    "- HP faces: $($plan.oldHpFaces) -> $($plan.newHpFaces)",
    "- Duplicated raised vertices: $([string]::Join(', ', @($plan.duplicatedVertexIndexes)))",
    "- Added top/skirt faces: $([string]::Join(', ', @($plan.addedFaceIndexes)))"
) | Set-Content -LiteralPath $planMd -Encoding UTF8

if ($PlanOnly) {
    Write-Host "Wrote platform topology plan only."
    return
}

Write-Host "Wrote expanded platform sector BIN to $resolvedOut"
Write-Host "Mapping original terrain edit to packed collision triangles..."
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $collisionReportScript `
    -ImagePath $resolvedImage `
    -RamPath $resolvedRam `
    -TerrainEditsPath $resolvedEdits `
    -OutJsonPath $resolvedCollisionReport `
    -OutMarkdownPath (Resolve-WorkspacePath ".\stonehill-platform-collision-bridge-report.md") `
    -CollisionTriangleWadBaseOffset $CollisionTriangleWadBaseOffset `
    -SearchWad
if ($LASTEXITCODE -ne 0) { throw "Collision bridge report failed." }

Write-Host "Applying raised-platform collision triangle patch..."
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $collisionPatchScript `
    -ImagePath $resolvedOut `
    -CollisionReportPath $resolvedCollisionReport `
    -OutPath $resolvedOut `
    -CuePath $resolvedCue `
    -RamPath $resolvedRam `
    -PlanPath "$resolvedOut.collisionpatchplan.json" `
    -MarkdownPath "$resolvedOut.collisionpatchplan.md" `
    -AllowExperimentalWrite
if ($LASTEXITCODE -ne 0) { throw "Collision triangle patch failed." }

Write-Host "Platform topology + collision patch workflow complete."
Write-Host "Fresh-load CUE: $resolvedCue"
