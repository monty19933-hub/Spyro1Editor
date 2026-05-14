param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$OutJsonPath = ".\stonehill-collision-bridge-report.json",
    [string]$OutMarkdownPath = ".\stonehill-collision-bridge-report.md",
    [int]$WadLba = 37,
    [int]$WadSize = 110260224,
    [int]$MaxSeparation = 6,
    [int]$ContextBytes = 256,
    [string]$CollisionTriangleWadBaseOffset = "",
    [switch]$SearchWad
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

$byteSearchSource = @"
using System;
using System.Collections.Generic;

public static class SpyroCollisionByteSearch {
    public static int[] FindAll(byte[] haystack, byte[] needle, int maxHits) {
        if (haystack == null || needle == null || needle.Length == 0 || needle.Length > haystack.Length)
            return new int[0];

        List<int> hits = new List<int>();
        int last = haystack.Length - needle.Length;
        byte first = needle[0];
        for (int i = 0; i <= last; i++) {
            if (haystack[i] != first)
                continue;

            bool ok = true;
            for (int j = 1; j < needle.Length; j++) {
                if (haystack[i + j] != needle[j]) {
                    ok = false;
                    break;
                }
            }

            if (ok) {
                hits.Add(i);
                if (hits.Count >= maxHits)
                    break;
            }
        }

        return hits.ToArray();
    }
}
"@
Add-Type -TypeDefinition $byteSearchSource

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

function Convert-HexTextToInt([string]$Text, [int]$Default = -1) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Default }
    $clean = $Text.Trim()
    try {
        if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
            return [int][Convert]::ToInt64($clean.Substring(2), 16)
        }
        return [int]$clean
    }
    catch {
        return $Default
    }
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Copy-Bytes([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Offset, $result, 0, $Length)
    return $result
}

function Get-BytesHex([byte[]]$Bytes) {
    return (($Bytes | ForEach-Object { $_.ToString("X2") }) -join "")
}

function Convert-HexToUInt32([string]$Hex) {
    return [Convert]::ToUInt32($Hex, 16)
}

function Test-PsxPointer([uint32]$Value) {
    return ($Value -ge (Convert-HexToUInt32 "80000000") -and $Value -lt (Convert-HexToUInt32 "80200000") -and (($Value -band [uint32]3) -eq 0))
}

function Convert-PsxPointerToOffset([uint32]$Value) {
    if (-not (Test-PsxPointer $Value)) { return -1 }
    return [int]($Value -band [uint32]0x001FFFFF)
}

function Convert-BuildAddress([uint32]$UpperInstruction, [uint32]$LowerInstruction) {
    $upper = [int64]($UpperInstruction -band 0xFFFF)
    $lower = [int64]($LowerInstruction -band 0xFFFF)
    if ($lower -ge 0x8000) { $lower -= 0x10000 }
    return [int](((($upper -shl 16) + $lower) -band 0x003FFFFF))
}

function Convert-WadOffsetToImageOffset([int64]$WadOffset) {
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    $sectorOffset = [int]($WadOffset % 2048)
    return ($sector * 2352L) + 24L + [int64]$sectorOffset
}

function Read-UserSector([System.IO.FileStream]$Stream, [int64]$Lba) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * 2352L) + 24L
    [void]$Stream.Read($buffer, 0, 2048)
    return $buffer
}

function Read-WadBytes([System.IO.FileStream]$Stream, [int64]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int64]$WadLba + [int64][Math]::Floor($absolute / 2048)
        $sectorBytes = Read-UserSector $Stream $sector
        $toCopy = [Math]::Min(2048 - $sectorOffset, $remaining)
        [Array]::Copy($sectorBytes, $sectorOffset, $result, $written, $toCopy)
        $written += $toCopy
        $remaining -= $toCopy
        $absolute += $toCopy
    }
    return $result
}

function Read-LogicalWad([string]$ImagePathValue) {
    $stream = [System.IO.File]::OpenRead($ImagePathValue)
    try {
        return Read-WadBytes $stream 0 $WadSize
    }
    finally {
        $stream.Dispose()
    }
}

function Find-BytePattern([byte[]]$Haystack, [byte[]]$Needle, [int]$MaxHits = 32) {
    return @([SpyroCollisionByteSearch]::FindAll($Haystack, $Needle, $MaxHits))
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
    $x = $sectorX + [int]((($cur -shr 19) -band 0x1FFC) -shr 2)
    $y = $sectorY + [int]((($cur -shr 8) -band 0x1FFC) -shr 2)
    $z = $sectorZ + [int]((($cur -shl 3) -band 0x1FFC) -shr 3)
    if (Test-FlatSceneSector $Sector) { $z = $z -shr 3 }
    return [pscustomobject]([ordered]@{ x = [int]$x; y = [int]$y; z = [int]$z })
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

function Find-Spyro1SceneAndCollisionPointers([byte[]]$Ram) {
    $wordCount = [int][Math]::Floor($Ram.Length / 4)
    $results = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt ($wordCount - 8); $i++) {
        $offset = $i * 4
        $w0 = Get-UInt32LE $Ram $offset
        if ($w0 -ne (Convert-HexToUInt32 "02023021")) { continue }
        $w1 = Get-UInt32LE $Ram ($offset + 4)
        $w2 = Get-UInt32LE $Ram ($offset + 8)
        $w3 = Get-UInt32LE $Ram ($offset + 12)
        $w4 = Get-UInt32LE $Ram ($offset + 16)
        $w5 = Get-UInt32LE $Ram ($offset + 20)
        $w6 = Get-UInt32LE $Ram ($offset + 24)
        if ($w1 -ne (Convert-HexToUInt32 "00C08021") -or $w2 -ne (Convert-HexToUInt32 "24C60004") -or $w3 -ne (Convert-HexToUInt32 "8CC20000") -or $w4 -ne (Convert-HexToUInt32 "24C60004")) { continue }
        if (($w5 -shr 16) -ne [uint32]0x3C01 -or ($w6 -shr 16) -ne [uint32]0xAC26) { continue }

        $addr = Convert-BuildAddress $w5 $w6
        if ($addr -lt 0 -or ($addr + 48) -gt $Ram.Length) { continue }
        $scenePointer = [uint32](Get-UInt32LE $Ram $addr)
        $collisionPointer = [uint32](Get-UInt32LE $Ram ($addr + 44))
        if (-not (Test-PsxPointer $scenePointer) -or -not (Test-PsxPointer $collisionPointer)) { continue }
        $sceneOffset = Convert-PsxPointerToOffset ([uint32]($scenePointer - [uint32]12))
        $collisionOffset = Convert-PsxPointerToOffset $collisionPointer
        if ($sceneOffset -lt 0 -or $collisionOffset -lt 0) { continue }
        [void]$results.Add([pscustomobject]([ordered]@{
            codeOffset = $offset
            pointerTableOffset = $addr
            scenePointer = $scenePointer
            sceneOffset = $sceneOffset
            collisionPointer = $collisionPointer
            collisionOffset = $collisionOffset
        }))
    }
    return @($results.ToArray())
}

function Read-Spyro1CollisionHeader([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 28) -gt $Ram.Length) { return $null }
    $numTriangles = [uint32](Get-UInt32LE $Ram $Offset)
    $blockTree = [uint32](Get-UInt32LE $Ram ($Offset + 8))
    $blocks = [uint32](Get-UInt32LE $Ram ($Offset + 12))
    $triangles = [uint32](Get-UInt32LE $Ram ($Offset + 16))
    if ($numTriangles -le 0 -or $numTriangles -gt 50000) { return $null }
    if (-not (Test-PsxPointer $blockTree) -or -not (Test-PsxPointer $blocks) -or -not (Test-PsxPointer $triangles)) { return $null }
    $triOffset = Convert-PsxPointerToOffset $triangles
    if ($triOffset -lt 0 -or ($triOffset + ([int64]$numTriangles * 12L)) -gt $Ram.Length) { return $null }
    return [pscustomobject]([ordered]@{
        offset = $Offset
        runtimeAddress = ("0x{0:X8}" -f ([uint64](Convert-HexToUInt32 "80000000") + [uint64]$Offset))
        numTriangles = [int]$numTriangles
        numUnkn1 = [uint32](Get-UInt32LE $Ram ($Offset + 4))
        blockTreePointer = $blockTree
        blockTreeOffset = Convert-PsxPointerToOffset $blockTree
        blocksPointer = $blocks
        blocksOffset = Convert-PsxPointerToOffset $blocks
        trianglePointer = $triangles
        triangleOffset = $triOffset
        etc1 = [uint32](Get-UInt32LE $Ram ($Offset + 20))
        etc2 = [uint32](Get-UInt32LE $Ram ($Offset + 24))
    })
}

function Convert-SignedBits([int]$Value, [int]$Bits) {
    $sign = 1 -shl ($Bits - 1)
    $mask = 1 -shl $Bits
    if (($Value -band $sign) -ne 0) { return $Value - $mask }
    return $Value
}

function Read-CollTri([byte[]]$Ram, [int]$Offset, [int]$Index = -1) {
    $xWord = [uint32](Get-UInt32LE $Ram $Offset)
    $yWord = [uint32](Get-UInt32LE $Ram ($Offset + 4))
    $zWord = [uint32](Get-UInt32LE $Ram ($Offset + 8))
    $p1X = [int]($xWord -band 0x3FFF)
    $p1Y = [int]($yWord -band 0x3FFF)
    $p1Z = [int]($zWord -band 0x3FFF)
    $p2X = $p1X + (Convert-SignedBits ([int](($xWord -shr 14) -band 0x1FF)) 9)
    $p3X = $p1X + (Convert-SignedBits ([int](($xWord -shr 23) -band 0x1FF)) 9)
    $p2Y = $p1Y + (Convert-SignedBits ([int](($yWord -shr 14) -band 0x1FF)) 9)
    $p3Y = $p1Y + (Convert-SignedBits ([int](($yWord -shr 23) -band 0x1FF)) 9)
    $p2Z = $p1Z + [int](($zWord -shr 16) -band 0xFF)
    $p3Z = $p1Z + [int](($zWord -shr 24) -band 0xFF)
    return [pscustomobject]([ordered]@{
        index = $Index
        offset = $Offset
        xWord = $xWord
        yWord = $yWord
        zWord = $zWord
        zFlags = [uint32]($zWord -band 0xC000)
        points = @(
            [pscustomobject]([ordered]@{ x = $p1X; y = $p1Y; z = $p1Z }),
            [pscustomobject]([ordered]@{ x = $p2X; y = $p2Y; z = $p2Z }),
            [pscustomobject]([ordered]@{ x = $p3X; y = $p3Y; z = $p3Z })
        )
    })
}

function Find-MatchingHpVertex($Point, $Vertices, [int]$MaxDistance) {
    $best = $null
    foreach ($vertex in @($Vertices)) {
        $dx = [int]$Point.x - [int]$vertex.x
        $dy = [int]$Point.y - [int]$vertex.y
        $dz = [int]$Point.z - [int]$vertex.z
        if ([Math]::Abs($dx) -gt $MaxDistance -or [Math]::Abs($dy) -gt $MaxDistance -or [Math]::Abs($dz) -gt $MaxDistance) { continue }
        $dist = [Math]::Sqrt(($dx * $dx) + ($dy * $dy) + ($dz * $dz))
        if ($null -eq $best -or $dist -lt [double]$best.distance) {
            $best = [pscustomobject]([ordered]@{
                vertexIndex = [int]$vertex.vertexIndex
                editArrayIndex = [int]$vertex.editArrayIndex
                x = [int]$vertex.x
                y = [int]$vertex.y
                z = [int]$vertex.z
                editedZ = [double]$vertex.editedZ
                isMoved = [bool]$vertex.isMoved
                distance = [double]$dist
            })
        }
    }
    return $best
}

function Find-CollisionMatchesForEdit([byte[]]$Ram, $CollisionHeader, $Edit) {
    $sectorOffset = Convert-HexTextToInt ([string](Get-Field $Edit "sectorOffset" ""))
    $detail = [string](Get-Field $Edit "detail" "hp")
    if (-not [string]::Equals($detail, "hp", [System.StringComparison]::OrdinalIgnoreCase)) { return @() }
    $sector = Read-SceneSectorHeader $Ram $sectorOffset
    $runtimeKey = [string](Get-Field $Edit "runtimeKey" "")
    if ($null -eq $sector) { throw "Bad scene sector for terrain edit $runtimeKey" }

    $vertexIndexes = @(Get-ArrayField $Edit "vertexIndexes")
    $editedZ = @(Get-ArrayField $Edit "editedZ")
    $deltaZ = [double](Get-Field $Edit "deltaZ" 0.0)
    $movedVertexTargets = @{}
    for ($i = 0; $i -lt $vertexIndexes.Count; $i++) {
        $vertexIndex = [int]$vertexIndexes[$i]
        $movedVertexTargets[[string]$vertexIndex] = if ($i -lt $editedZ.Count) { [double]$editedZ[$i] } else { $null }
    }

    $hpVertices = New-Object System.Collections.ArrayList
    for ($vertexIndex = 0; $vertexIndex -lt [int]$sector.numHpVertices; $vertexIndex++) {
        $vertexOffset = Get-SceneVertexOffset $sector "hp" $vertexIndex
        if ($vertexOffset -lt 0) { continue }
        $word = [uint32](Get-UInt32LE $Ram $vertexOffset)
        $coord = Convert-SceneVertex $word $sector
        $targetZ = [double]$coord.z
        $isMoved = $false
        if ($movedVertexTargets.ContainsKey([string]$vertexIndex)) {
            $isMoved = $true
            $explicitTarget = $movedVertexTargets[[string]$vertexIndex]
            $targetZ = if ($null -ne $explicitTarget) { [double]$explicitTarget } else { [double]$coord.z + $deltaZ }
        }
        [void]$hpVertices.Add([pscustomobject]([ordered]@{
            vertexIndex = $vertexIndex
            editArrayIndex = if ($movedVertexTargets.ContainsKey([string]$vertexIndex)) { [Array]::IndexOf([int[]]@($vertexIndexes | ForEach-Object { [int]$_ }), $vertexIndex) } else { -1 }
            x = [int]$coord.x
            y = [int]$coord.y
            z = [int]$coord.z
            editedZ = $targetZ
            isMoved = $isMoved
        }))
    }

    $matches = New-Object System.Collections.ArrayList
    $triBase = [int]$CollisionHeader.triangleOffset
    for ($i = 0; $i -lt [int]$CollisionHeader.numTriangles; $i++) {
        $triOffset = $triBase + ($i * 12)
        $tri = Read-CollTri $Ram $triOffset $i
        $pointMatches = New-Object System.Collections.ArrayList
        $usedVertices = New-Object 'System.Collections.Generic.HashSet[int]'
        $totalDistance = 0.0
        $movedPointCount = 0
        foreach ($point in @($tri.points)) {
            $match = Find-MatchingHpVertex $point @($hpVertices.ToArray()) $MaxSeparation
            if ($null -eq $match) { break }
            if (-not $usedVertices.Add([int]$match.vertexIndex)) { break }
            $totalDistance += [double]$match.distance
            if ([bool](Get-Field $match "isMoved" $false)) { $movedPointCount++ }
            [void]$pointMatches.Add($match)
        }
        if ($pointMatches.Count -eq 3 -and $movedPointCount -gt 0) {
            [void]$matches.Add([pscustomobject]([ordered]@{
                runtimeKey = [string](Get-Field $Edit "runtimeKey" "")
                triangleIndex = $i
                runtimeTriangleOffset = ("0x{0:X}" -f $triOffset)
                totalVertexDistance = [double]$totalDistance
                touchedMovedVertexCount = [int]$movedPointCount
                oldBytes = Get-BytesHex (Copy-Bytes $Ram $triOffset 12)
                oldWords = @(
                    ("0x{0:X8}" -f [uint32]$tri.xWord),
                    ("0x{0:X8}" -f [uint32]$tri.yWord),
                    ("0x{0:X8}" -f [uint32]$tri.zWord)
                )
                points = @($tri.points)
                matchedVertices = @($pointMatches.ToArray())
            }))
        }
    }
    return @($matches.ToArray())
}

$resolvedImage = Resolve-WorkspacePath $ImagePath
$resolvedRam = Resolve-WorkspacePath $RamPath
$resolvedEdits = Resolve-WorkspacePath $TerrainEditsPath
if (-not (Test-Path -LiteralPath $resolvedRam)) { throw "Missing RAM dump: $resolvedRam" }
if (-not (Test-Path -LiteralPath $resolvedEdits)) { throw "Missing terrain edits: $resolvedEdits" }
if ($SearchWad -and -not (Test-Path -LiteralPath $resolvedImage)) { throw "Missing image: $resolvedImage" }

$ram = [System.IO.File]::ReadAllBytes($resolvedRam)
$editsRoot = Get-Content -LiteralPath $resolvedEdits -Raw | ConvertFrom-Json
$edits = @(Get-ArrayField $editsRoot "edits")

$pointerCandidates = @(Find-Spyro1SceneAndCollisionPointers $ram)
if ($pointerCandidates.Count -le 0) { throw "Could not find the Spyro 1 scene/collision pointer table in RAM." }
$pointerCandidate = $pointerCandidates[0]
$collisionHeader = Read-Spyro1CollisionHeader $ram ([int]$pointerCandidate.collisionOffset)
if ($null -eq $collisionHeader) { throw "Could not parse Spyro 1 collision header." }

$wad = $null
$collisionTriangleWadBase = -1L
if (-not [string]::IsNullOrWhiteSpace($CollisionTriangleWadBaseOffset)) {
    $collisionTriangleWadBase = [int64](Convert-HexTextToInt $CollisionTriangleWadBaseOffset)
    if ($collisionTriangleWadBase -lt 0) { throw "Bad CollisionTriangleWadBaseOffset: $CollisionTriangleWadBaseOffset" }
}

if ($SearchWad -and $collisionTriangleWadBase -lt 0) {
    Write-Host "Reading logical WAD stream for collision source search..."
    $wad = Read-LogicalWad $resolvedImage
}

$allMatches = New-Object System.Collections.ArrayList
foreach ($edit in $edits) {
    foreach ($match in @(Find-CollisionMatchesForEdit $ram $collisionHeader $edit)) {
        if ($collisionTriangleWadBase -ge 0) {
            $triWadOffset = $collisionTriangleWadBase + ([int64]([int](Get-Field $match "triangleIndex" -1)) * 12L)
            $match | Add-Member -NotePropertyName sourceContextHits -NotePropertyValue @(
                [ordered]@{
                    contextWadOffset = ("0x{0:X}" -f [Math]::Max(0L, $triWadOffset - [int64][Math]::Floor($ContextBytes / 2)))
                    triangleWadOffset = ("0x{0:X}" -f $triWadOffset)
                    triangleImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $triWadOffset))
                }
            ) -Force
        } elseif ($SearchWad) {
            $runtimeTriOffset = Convert-HexTextToInt ([string]$match.runtimeTriangleOffset)
            $contextStart = [Math]::Max([int]$collisionHeader.triangleOffset, $runtimeTriOffset - [Math]::Floor($ContextBytes / 2))
            $contextEnd = [Math]::Min([int]$collisionHeader.triangleOffset + ([int]$collisionHeader.numTriangles * 12), $runtimeTriOffset + 12 + [Math]::Ceiling($ContextBytes / 2))
            $contextLength = [int]($contextEnd - $contextStart)
            $context = Copy-Bytes $ram $contextStart $contextLength
            $hits = @(Find-BytePattern $wad $context 16)
            $match | Add-Member -NotePropertyName sourceContextHits -NotePropertyValue @($hits | ForEach-Object {
                $triWadOffset = [int64]$_ + [int64]($runtimeTriOffset - $contextStart)
                [ordered]@{
                    contextWadOffset = ("0x{0:X}" -f [int64]$_)
                    triangleWadOffset = ("0x{0:X}" -f $triWadOffset)
                    triangleImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $triWadOffset))
                }
            }) -Force
        }
        [void]$allMatches.Add($match)
    }
}

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    note = "Decodes Spyro 1's packed collision triangle table and matches saved Stone Hill terrain HP faces to collision triangles."
    source = "SpyroEdit SpyroCollisionS1 and CollTri packing were used as the decoding guide."
    ramPath = (Resolve-Path -LiteralPath $resolvedRam).Path
    terrainEditsPath = (Resolve-Path -LiteralPath $resolvedEdits).Path
    imagePath = if (Test-Path -LiteralPath $resolvedImage) { (Resolve-Path -LiteralPath $resolvedImage).Path } else { $resolvedImage }
    maxSeparation = $MaxSeparation
    searchWad = [bool]($SearchWad -and $collisionTriangleWadBase -lt 0)
    collisionTriangleWadBaseOffset = if ($collisionTriangleWadBase -ge 0) { ("0x{0:X}" -f $collisionTriangleWadBase) } else { "" }
    pointerCandidate = [ordered]@{
        codeOffset = ("0x{0:X}" -f [int]$pointerCandidate.codeOffset)
        pointerTableOffset = ("0x{0:X}" -f [int]$pointerCandidate.pointerTableOffset)
        scenePointer = ("0x{0:X8}" -f [uint32]$pointerCandidate.scenePointer)
        sceneOffset = ("0x{0:X}" -f [int]$pointerCandidate.sceneOffset)
        collisionPointer = ("0x{0:X8}" -f [uint32]$pointerCandidate.collisionPointer)
        collisionOffset = ("0x{0:X}" -f [int]$pointerCandidate.collisionOffset)
    }
    collisionHeader = [ordered]@{
        offset = ("0x{0:X}" -f [int]$collisionHeader.offset)
        runtimeAddress = [string]$collisionHeader.runtimeAddress
        numTriangles = [int]$collisionHeader.numTriangles
        numUnkn1 = ("0x{0:X8}" -f [uint32]$collisionHeader.numUnkn1)
        blockTreePointer = ("0x{0:X8}" -f [uint32]$collisionHeader.blockTreePointer)
        blockTreeOffset = ("0x{0:X}" -f [int]$collisionHeader.blockTreeOffset)
        blocksPointer = ("0x{0:X8}" -f [uint32]$collisionHeader.blocksPointer)
        blocksOffset = ("0x{0:X}" -f [int]$collisionHeader.blocksOffset)
        trianglePointer = ("0x{0:X8}" -f [uint32]$collisionHeader.trianglePointer)
        triangleOffset = ("0x{0:X}" -f [int]$collisionHeader.triangleOffset)
        etc1 = ("0x{0:X8}" -f [uint32]$collisionHeader.etc1)
        etc2 = ("0x{0:X8}" -f [uint32]$collisionHeader.etc2)
    }
    terrainEditCount = $edits.Count
    collisionMatchCount = @($allMatches.ToArray()).Count
    matches = @($allMatches.ToArray())
}

$resolvedOutJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutJsonPath))
$resolvedOutMd = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutMarkdownPath))
$report | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Stone Hill Collision Bridge Report")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($report.generatedAt)")
[void]$lines.Add("")
[void]$lines.Add("- Collision header: $($report.collisionHeader.offset)")
[void]$lines.Add("- Collision triangles: $($report.collisionHeader.numTriangles)")
[void]$lines.Add("- Triangle data: $($report.collisionHeader.triangleOffset)")
[void]$lines.Add("- Saved terrain edits: $($report.terrainEditCount)")
[void]$lines.Add("- Matched collision triangles: $($report.collisionMatchCount)")
[void]$lines.Add("- WAD source search: $($report.searchWad)")
[void]$lines.Add("")
[void]$lines.Add("| Edit | Collision tri | Moved points | Runtime offset | Source hits | Old bytes |")
[void]$lines.Add("|---|---:|---:|---|---:|---|")
foreach ($match in @($allMatches.ToArray())) {
    $sourceHits = if ($match.PSObject.Properties.Name -contains "sourceContextHits") { @($match.sourceContextHits).Count } else { 0 }
    [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | `{5}` |" -f $match.runtimeKey, $match.triangleIndex, $match.touchedMovedVertexCount, $match.runtimeTriangleOffset, $sourceHits, $match.oldBytes))
}
[void]$lines.Add("")
[void]$lines.Add("## Match Details")
[void]$lines.Add("")
foreach ($match in @($allMatches.ToArray())) {
    [void]$lines.Add(("### {0} tri {1}" -f $match.runtimeKey, $match.triangleIndex))
    foreach ($vertex in @($match.matchedVertices)) {
        $tag = if ([bool]$vertex.isMoved) { "moved" } else { "unchanged" }
        [void]$lines.Add(("- collision point matched HP vertex {0} ({1}): ({2},{3},{4}) -> target Z {5:N2}" -f $vertex.vertexIndex, $tag, $vertex.x, $vertex.y, $vertex.z, [double]$vertex.editedZ))
    }
    if ($match.PSObject.Properties.Name -contains "sourceContextHits") {
        $hits = @($match.sourceContextHits)
        if ($hits.Count -gt 0) {
            [void]$lines.Add(("- source triangle WAD: {0}" -f (($hits | ForEach-Object { $_.triangleWadOffset }) -join ", ")))
        }
    }
    [void]$lines.Add("")
}
[System.IO.File]::WriteAllLines($resolvedOutMd, $lines.ToArray(), [System.Text.Encoding]::UTF8)

Write-Host "Wrote collision bridge report to $resolvedOutJson"
Write-Host "Wrote collision bridge summary to $resolvedOutMd"
