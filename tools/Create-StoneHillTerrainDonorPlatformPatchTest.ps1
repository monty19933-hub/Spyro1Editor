param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$SourceSearchPath = ".\stonehill-runtime-terrain-source-search.json",
    [string]$OutPath = ".\Spyro the Dragon (USA)-runtime-terrain-donorplatformtest.bin",
    [string]$CuePath = "",
    [int[]]$DonorVertexIndexes = @(7, 45, 46, 49),
    [int[]]$DonorFaceIndexes = @(27, 4, 25, 26, 29),
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

function Convert-WadOffsetToImageOffset([int64]$WadOffset) {
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    $sectorOffset = [int]($WadOffset % 2048)
    return ($sector * 2352L) + 24L + [int64]$sectorOffset
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

function Set-ImageBytesForWadOffset([byte[]]$Image, [int64]$WadOffset, [byte[]]$Bytes) {
    for ($i = 0; $i -lt $Bytes.Length; $i++) {
        $imageOffset = Convert-WadOffsetToImageOffset ($WadOffset + [int64]$i)
        if ($imageOffset -lt 0 -or $imageOffset -ge $Image.Length) {
            throw ("Image offset 0x{0:X} is outside image." -f $imageOffset)
        }
        $Image[$imageOffset] = $Bytes[$i]
    }
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

function Test-FlatSceneSector($Sector) {
    return ((([int]$Sector.centreRadiusAndFlags -shr 12) -band 1) -eq 1)
}

function Convert-SceneVertex([uint32]$Word, $Sector) {
    $cur = [uint64]$Word
    $xyPos = [uint64]$Sector.xyPos
    $zPos = [uint64]$Sector.zPos
    $sectorX = [int](($xyPos -shr 16) -band 0xFFFF)
    $sectorY = [int]($xyPos -band 0xFFFF)
    $sectorZ = [int](($zPos -shr 14) -band 0xFFFF)
    $sectorZ = $sectorZ -shr 2
    $x = $sectorX + [int](($cur -shr 21) -band 0x7FF)
    $y = $sectorY + [int](($cur -shr 10) -band 0x7FF)
    $z = $sectorZ + [int]($cur -band 0x3FF)
    if (Test-FlatSceneSector $Sector) { $z = $z -shr 3 }
    return [pscustomobject]([ordered]@{ x = [double]$x; y = [double]$y; z = [double]$z })
}

function New-SceneVertexWord($Sector, [double]$X, [double]$Y, [double]$Z) {
    $xyPos = [uint64]$Sector.xyPos
    $zPos = [uint64]$Sector.zPos
    $sectorX = [int](($xyPos -shr 16) -band 0xFFFF)
    $sectorY = [int]($xyPos -band 0xFFFF)
    $baseZ = [int]((($zPos -shr 14) -band 0xFFFF) -shr 2)
    $encodedX = [int][Math]::Round($X - [double]$sectorX)
    $encodedY = [int][Math]::Round($Y - [double]$sectorY)
    $encodedZ = if (Test-FlatSceneSector $Sector) {
        [int][Math]::Round(($Z * 8.0) - [double]$baseZ)
    } else {
        [int][Math]::Round($Z - [double]$baseZ)
    }
    if ($encodedX -lt 0 -or $encodedX -gt 0x7FF -or $encodedY -lt 0 -or $encodedY -gt 0x7FF -or $encodedZ -lt 0 -or $encodedZ -gt 0x3FF) {
        throw ("Cannot encode scene vertex ({0:N2},{1:N2},{2:N2}) in sector 0x{3:X}." -f $X, $Y, $Z, [int]$Sector.offset)
    }
    return [uint32]((([uint64]$encodedX -band 0x7FF) -shl 21) -bor (([uint64]$encodedY -band 0x7FF) -shl 10) -bor ([uint64]($encodedZ -band 0x3FF)))
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

function Get-HpFaceStartOffset($Sector) {
    $dataStart = [int]$Sector.offset + 28
    $hpVertexStartWords = [int]$Sector.numLpVertices + [int]$Sector.numLpColours + ([int]$Sector.numLpFaces * 2)
    $hpColourStartWords = $hpVertexStartWords + [int]$Sector.numHpVertices
    $hpFaceStartWords = $hpColourStartWords + ([int]$Sector.numHpColours * 2)
    return $dataStart + ($hpFaceStartWords * 4)
}

function New-FaceRecord([int[]]$VertexIndexes, [int[]]$ColourIndexes, [byte[]]$TemplateBytes) {
    $bytes = New-Object byte[] 16
    [Array]::Copy($TemplateBytes, 0, $bytes, 0, 16)
    for ($i = 0; $i -lt 4; $i++) {
        $bytes[$i] = [byte]$VertexIndexes[$i]
        $bytes[4 + $i] = [byte]$ColourIndexes[$i]
    }
    return $bytes
}

$findTerrainScript = Join-Path $PSScriptRoot "Find-StoneHillRuntimeTerrainSource.ps1"
if (-not (Test-Path -LiteralPath $findTerrainScript)) { throw "Missing required script: $findTerrainScript" }

$resolvedImage = Resolve-WorkspacePath $ImagePath
$resolvedRam = Resolve-WorkspacePath $RamPath
$resolvedEdits = Resolve-WorkspacePath $TerrainEditsPath
$resolvedSourceSearch = Resolve-WorkspacePath $SourceSearchPath
$resolvedOut = Resolve-WorkspacePath $OutPath
$resolvedCue = if ([string]::IsNullOrWhiteSpace($CuePath)) { [System.IO.Path]::ChangeExtension($resolvedOut, ".cue") } else { Resolve-WorkspacePath $CuePath }

if ($DonorVertexIndexes.Count -ne 4) { throw "Need exactly four donor vertices." }
if ($DonorFaceIndexes.Count -ne 5) { throw "Need exactly five donor faces: top plus four skirts." }

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
if ($edits.Count -ne 1) { throw "Donor platform test currently expects exactly one saved terrain edit." }
$edit = $edits[0]
$oldVertexIndexes = @(Get-ArrayField $edit "vertexIndexes" | ForEach-Object { [int]$_ })
$editedZ = @(Get-ArrayField $edit "editedZ" | ForEach-Object { [double]$_ })
if ($oldVertexIndexes.Count -ne 4 -or $editedZ.Count -lt 4) { throw "Donor platform test expects one quad edit." }

$searchRoot = Get-Content -LiteralPath $resolvedSourceSearch -Raw | ConvertFrom-Json
$result = @(Get-ArrayField $searchRoot "results" | Where-Object { [string](Get-Field $_ "edit" "") -eq [string](Get-Field $edit "runtimeKey" "") }) | Select-Object -First 1
if ($null -eq $result) { throw "No source-search result found for terrain edit." }
$sectorHits = @(Get-ArrayField $result "fullSectorHits")
if ($sectorHits.Count -ne 1) { throw "Terrain edit needs exactly one source-sector hit, found $($sectorHits.Count)." }
$sourceSectorWadOffset = Convert-HexTextToInt64 ([string](Get-Field $sectorHits[0] "wadOffset" ""))

$sectorOffset = [int](Convert-HexTextToInt64 ([string](Get-Field $edit "sectorOffset" "")))
$sector = Read-SceneSectorHeader $ram $sectorOffset
if ($null -eq $sector) { throw "Could not parse runtime sector." }
$hpFaceStart = Get-HpFaceStartOffset $sector

$patches = New-Object System.Collections.ArrayList
for ($i = 0; $i -lt 4; $i++) {
    $sourceVertexOffset = Get-SceneVertexOffset $sector "hp" $oldVertexIndexes[$i]
    $donorVertexOffset = Get-SceneVertexOffset $sector "hp" $DonorVertexIndexes[$i]
    if ($sourceVertexOffset -lt 0 -or $donorVertexOffset -lt 0) { throw "Bad source/donor vertex index." }
    $sourceWord = [uint32](Get-UInt32LE $ram $sourceVertexOffset)
    $coord = Convert-SceneVertex $sourceWord $sector
    $newWord = New-SceneVertexWord $sector ([double]$coord.x) ([double]$coord.y) ([double]$editedZ[$i])
    $oldBytes = Copy-Bytes $ram $donorVertexOffset 4
    $newBytes = [BitConverter]::GetBytes([uint32]$newWord)
    [void]$patches.Add([ordered]@{
        kind = "donor-hp-vertex"
        donorVertexIndex = [int]$DonorVertexIndexes[$i]
        copiedFromVertexIndex = [int]$oldVertexIndexes[$i]
        runtimeOffset = ("0x{0:X}" -f $donorVertexOffset)
        sourceWadOffset = ("0x{0:X}" -f ($sourceSectorWadOffset + [int64]($donorVertexOffset - $sectorOffset)))
        oldBytes = Get-BytesHex $oldBytes
        newBytes = Get-BytesHex $newBytes
        x = [double]$coord.x
        y = [double]$coord.y
        z = [double]$editedZ[$i]
    })
}

$selectedFaceOffset = [int](Convert-HexTextToInt64 ([string](Get-Field $edit "faceOffset" "")))
$templateFace = Copy-Bytes $ram $selectedFaceOffset 16
$selectedColours = [int[]]@([int]$templateFace[4], [int]$templateFace[5], [int]$templateFace[6], [int]$templateFace[7])
$newFaceRecords = @(
    (New-FaceRecord ([int[]]@($DonorVertexIndexes[0], $DonorVertexIndexes[1], $DonorVertexIndexes[2], $DonorVertexIndexes[3])) $selectedColours $templateFace),
    (New-FaceRecord ([int[]]@($oldVertexIndexes[0], $oldVertexIndexes[1], $DonorVertexIndexes[1], $DonorVertexIndexes[0])) ([int[]]@($selectedColours[0], $selectedColours[1], $selectedColours[1], $selectedColours[0])) $templateFace),
    (New-FaceRecord ([int[]]@($oldVertexIndexes[1], $oldVertexIndexes[2], $DonorVertexIndexes[2], $DonorVertexIndexes[1])) ([int[]]@($selectedColours[1], $selectedColours[2], $selectedColours[2], $selectedColours[1])) $templateFace),
    (New-FaceRecord ([int[]]@($oldVertexIndexes[2], $oldVertexIndexes[3], $DonorVertexIndexes[3], $DonorVertexIndexes[2])) ([int[]]@($selectedColours[2], $selectedColours[3], $selectedColours[3], $selectedColours[2])) $templateFace),
    (New-FaceRecord ([int[]]@($oldVertexIndexes[3], $oldVertexIndexes[0], $DonorVertexIndexes[0], $DonorVertexIndexes[3])) ([int[]]@($selectedColours[3], $selectedColours[0], $selectedColours[0], $selectedColours[3])) $templateFace)
)
for ($i = 0; $i -lt 5; $i++) {
    $faceOffset = $hpFaceStart + ([int]$DonorFaceIndexes[$i] * 16)
    if ($faceOffset -lt $hpFaceStart -or ($faceOffset + 16) -gt ($sectorOffset + [int]$sector.sizeBytes)) { throw "Bad donor face index $($DonorFaceIndexes[$i])." }
    $oldBytes = Copy-Bytes $ram $faceOffset 16
    [void]$patches.Add([ordered]@{
        kind = if ($i -eq 0) { "donor-top-face" } else { "donor-skirt-face" }
        donorFaceIndex = [int]$DonorFaceIndexes[$i]
        runtimeOffset = ("0x{0:X}" -f $faceOffset)
        sourceWadOffset = ("0x{0:X}" -f ($sourceSectorWadOffset + [int64]($faceOffset - $sectorOffset)))
        oldBytes = Get-BytesHex $oldBytes
        newBytes = Get-BytesHex ([byte[]]$newFaceRecords[$i])
    })
}

if (-not $PlanOnly) {
    foreach ($patch in @($patches.ToArray())) {
        $wadOffset = Convert-HexTextToInt64 ([string]$patch.sourceWadOffset)
        $oldBytes = for ($i = 0; $i -lt ([string]$patch.oldBytes).Length; $i += 2) { [Convert]::ToByte(([string]$patch.oldBytes).Substring($i, 2), 16) }
        $newBytes = for ($i = 0; $i -lt ([string]$patch.newBytes).Length; $i += 2) { [Convert]::ToByte(([string]$patch.newBytes).Substring($i, 2), 16) }
        $existing = Get-ImageBytesForWadOffset $image $wadOffset @($oldBytes).Count
        if ((Get-BytesHex $existing) -ne [string]$patch.oldBytes) {
            throw ("Source bytes at {0} are {1}, expected {2}." -f $patch.sourceWadOffset, (Get-BytesHex $existing), $patch.oldBytes)
        }
        Set-ImageBytesForWadOffset $image $wadOffset ([byte[]]$newBytes)
    }
    [System.IO.File]::WriteAllBytes($resolvedOut, $image)
    Write-Cue $resolvedOut $resolvedCue
}

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    status = if ($PlanOnly) { "plan-ready" } else { "donor-platform-bin-written" }
    note = "In-place topology proof. Sacrifices existing donor HP vertices/faces instead of relocating or expanding the scene sector."
    terrainEditsPath = (Resolve-Path -LiteralPath $resolvedEdits).Path
    sourceSectorWadOffset = ("0x{0:X}" -f $sourceSectorWadOffset)
    runtimeSectorOffset = ("0x{0:X}" -f $sectorOffset)
    donorVertexIndexes = @($DonorVertexIndexes)
    donorFaceIndexes = @($DonorFaceIndexes)
    patchCount = @($patches.ToArray()).Count
    outPath = $resolvedOut
    cuePath = $resolvedCue
    wroteBin = -not [bool]$PlanOnly
    patches = @($patches.ToArray())
}
$planPath = "$resolvedOut.donorplatformpatchplan.json"
$planMd = "$resolvedOut.donorplatformpatchplan.md"
$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding UTF8
@(
    "# Stone Hill Donor Platform Patch Plan",
    "",
    "Generated: $($plan.generatedAt)",
    "",
    "- Status: $($plan.status)",
    "- Source sector WAD: $($plan.sourceSectorWadOffset)",
    "- Runtime sector: $($plan.runtimeSectorOffset)",
    "- Donor vertices: $([string]::Join(', ', @($plan.donorVertexIndexes)))",
    "- Donor faces: $([string]::Join(', ', @($plan.donorFaceIndexes)))",
    "- Patch count: $($plan.patchCount)",
    "- BIN written: $($plan.wroteBin)"
) | Set-Content -LiteralPath $planMd -Encoding UTF8

Write-Host "Wrote donor platform patch plan to $planPath"
Write-Host "Wrote donor platform patch summary to $planMd"
if (-not $PlanOnly) {
    Write-Host "Wrote donor platform BIN to $resolvedOut"
    Write-Host "Wrote CUE to $resolvedCue"
}
