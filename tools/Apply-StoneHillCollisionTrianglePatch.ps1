param(
    [string]$ImagePath = ".\Spyro the Dragon (USA)-runtime-terrainpatchtest.bin",
    [string]$CollisionReportPath = ".\stonehill-collision-bridge-report.json",
    [string]$OutPath = ".\Spyro the Dragon (USA)-runtime-terrain-colltritest.bin",
    [string]$CuePath = "",
    [string]$PlanPath = ".\Spyro the Dragon (USA)-runtime-terrain-colltritest.bin.collisionpatchplan.json",
    [string]$MarkdownPath = ".\Spyro the Dragon (USA)-runtime-terrain-colltritest.bin.collisionpatchplan.md",
    [string]$RamPath = "",
    [int]$WadLba = 37,
    [switch]$RebuildCollisionIndex,
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

function Convert-HexTextToUInt32([string]$Text, [uint32]$Default = 0) {
    $value = Convert-HexTextToInt64 $Text -1
    if ($value -lt 0) { return $Default }
    return [uint32]$value
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

function Get-BytesHex([byte[]]$Bytes) {
    return (($Bytes | ForEach-Object { $_.ToString("X2") }) -join "")
}

function Convert-HexToBytes([string]$Hex) {
    if (($Hex.Length % 2) -ne 0) { throw "Hex byte string has odd length." }
    $bytes = New-Object byte[] ($Hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [byte][Convert]::ToInt32($Hex.Substring($i * 2, 2), 16)
    }
    return $bytes
}

function Convert-SignedTo9Bit([int]$Value) {
    if ($Value -lt -255 -or $Value -gt 255) { throw "9-bit signed delta is out of range: $Value" }
    if ($Value -lt 0) { return ($Value + 512) -band 0x1FF }
    return $Value -band 0x1FF
}

function Convert-SignedBits([int]$Value, [int]$Bits) {
    $sign = 1 -shl ($Bits - 1)
    $mask = 1 -shl $Bits
    if (($Value -band $sign) -ne 0) { return $Value - $mask }
    return $Value
}

function Try-PackCollTri($P1, $P2, $P3, [uint32]$PreservedZFlags) {
    $p1X = [int][Math]::Round([double]$P1.x)
    $p1Y = [int][Math]::Round([double]$P1.y)
    $p1Z = [int][Math]::Round([double]$P1.z)
    $p2X = [int][Math]::Round([double]$P2.x)
    $p2Y = [int][Math]::Round([double]$P2.y)
    $p2Z = [int][Math]::Round([double]$P2.z)
    $p3X = [int][Math]::Round([double]$P3.x)
    $p3Y = [int][Math]::Round([double]$P3.y)
    $p3Z = [int][Math]::Round([double]$P3.z)

    if ($p1X -lt 0 -or $p1X -gt 0x3FFF -or $p1Y -lt 0 -or $p1Y -gt 0x3FFF -or $p1Z -lt 0 -or $p1Z -gt 0x3FFF) { return $null }
    $dx2 = $p2X - $p1X
    $dx3 = $p3X - $p1X
    $dy2 = $p2Y - $p1Y
    $dy3 = $p3Y - $p1Y
    $dz2 = $p2Z - $p1Z
    $dz3 = $p3Z - $p1Z
    if ($dx2 -lt -255 -or $dx2 -gt 255 -or $dx3 -lt -255 -or $dx3 -gt 255) { return $null }
    if ($dy2 -lt -255 -or $dy2 -gt 255 -or $dy3 -lt -255 -or $dy3 -gt 255) { return $null }
    if ($dz2 -lt 0 -or $dz2 -gt 255 -or $dz3 -lt 0 -or $dz3 -gt 255) { return $null }

    $xWord = [uint32](([uint64]($p1X -band 0x3FFF)) -bor ([uint64](Convert-SignedTo9Bit $dx2) -shl 14) -bor ([uint64](Convert-SignedTo9Bit $dx3) -shl 23))
    $yWord = [uint32](([uint64]($p1Y -band 0x3FFF)) -bor ([uint64](Convert-SignedTo9Bit $dy2) -shl 14) -bor ([uint64](Convert-SignedTo9Bit $dy3) -shl 23))
    $zWord = [uint32](([uint64]($PreservedZFlags -band 0xC000)) -bor ([uint64]($p1Z -band 0x3FFF)) -bor ([uint64]($dz2 -band 0xFF) -shl 16) -bor ([uint64]($dz3 -band 0xFF) -shl 24))
    return [pscustomobject]([ordered]@{
        xWord = $xWord
        yWord = $yWord
        zWord = $zWord
        points = @($P1, $P2, $P3)
    })
}

function Read-CollTriFromBytes([byte[]]$Bytes, [int]$Offset, [int]$Index) {
    $xWord = [uint32][BitConverter]::ToUInt32($Bytes, $Offset)
    $yWord = [uint32][BitConverter]::ToUInt32($Bytes, $Offset + 4)
    $zWord = [uint32][BitConverter]::ToUInt32($Bytes, $Offset + 8)
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
        p1 = [pscustomobject]([ordered]@{ x = $p1X; y = $p1Y; z = $p1Z })
        p2 = [pscustomobject]([ordered]@{ x = $p2X; y = $p2Y; z = $p2Z })
        p3 = [pscustomobject]([ordered]@{ x = $p3X; y = $p3Y; z = $p3Z })
    })
}

function New-UInt16List {
    return New-Object 'System.Collections.Generic.List[uint16]'
}

function Add-UInt16([System.Collections.Generic.List[uint16]]$List, [int]$Value) {
    [void]$List.Add([uint16]($Value -band 0xFFFF))
}

function Set-UInt16([System.Collections.Generic.List[uint16]]$List, [int]$Index, [int]$Value) {
    $List[$Index] = [uint16]($Value -band 0xFFFF)
}

function Convert-UInt16ListToBytes([System.Collections.Generic.List[uint16]]$List) {
    $bytes = New-Object byte[] ($List.Count * 2)
    for ($i = 0; $i -lt $List.Count; $i++) {
        $wordBytes = [BitConverter]::GetBytes([uint16]$List[$i])
        $bytes[$i * 2] = $wordBytes[0]
        $bytes[($i * 2) + 1] = $wordBytes[1]
    }
    return $bytes
}

function New-CollisionIndexBytes([byte[]]$TriangleBytes, [int]$NumTriangles, [int]$TreeCapacityBytes, [int]$BlockCapacityBytes) {
    $xBlockSize = New-Object int[] 256
    $yBlockSize = New-Object int[] 256
    $zBlockSize = New-Object int[] 256
    $bounds = New-Object object[] $NumTriangles

    for ($i = 0; $i -lt $NumTriangles; $i++) {
        $tri = Read-CollTriFromBytes $TriangleBytes ($i * 12) $i
        $xs = @([int]$tri.p1.x, [int]$tri.p2.x, [int]$tri.p3.x)
        $ys = @([int]$tri.p1.y, [int]$tri.p2.y, [int]$tri.p3.y)
        $zs = @([int]$tri.p1.z, [int]$tri.p2.z, [int]$tri.p3.z)
        $minX = (($xs | Measure-Object -Minimum).Minimum) -shr 8
        $maxX = (($xs | Measure-Object -Maximum).Maximum) -shr 8
        $minY = (($ys | Measure-Object -Minimum).Minimum) -shr 8
        $maxY = (($ys | Measure-Object -Maximum).Maximum) -shr 8
        $minZ = (($zs | Measure-Object -Minimum).Minimum) -shr 8
        $maxZ = (($zs | Measure-Object -Maximum).Maximum) -shr 8
        if ($minX -lt 0 -or $minY -lt 0 -or $minZ -lt 0 -or $maxX -gt 255 -or $maxY -gt 255 -or $maxZ -gt 255) {
            throw "Collision triangle $i has a block bound outside 0..255."
        }
        $bounds[$i] = [pscustomobject]([ordered]@{
            p1 = $tri.p1
            p2 = $tri.p2
            p3 = $tri.p3
            minXBlock = [int]$minX
            maxXBlock = [int]$maxX
            minYBlock = [int]$minY
            maxYBlock = [int]$maxY
            minZBlock = [int]$minZ
            maxZBlock = [int]$maxZ
        })
        for ($x = $minX; $x -le $maxX; $x++) { $xBlockSize[$x]++ }
        for ($y = $minY; $y -le $maxY; $y++) { $yBlockSize[$y]++ }
        for ($z = $minZ; $z -le $maxZ; $z++) { $zBlockSize[$z]++ }
    }

    $xBlocks = New-Object object[] 256
    $yBlocks = New-Object object[] 256
    $zBlocks = New-Object object[] 256
    $minXBlock = 256
    $minYBlock = 256
    $minZBlock = 256
    $maxXBlock = 0
    $maxYBlock = 0
    $maxZBlock = 0
    for ($i = 0; $i -lt 256; $i++) {
        if ($xBlockSize[$i] -gt 0) { $xBlocks[$i] = New-Object 'System.Collections.Generic.List[int]'; $maxXBlock = $i; if ($i -lt $minXBlock) { $minXBlock = $i } }
        if ($yBlockSize[$i] -gt 0) { $yBlocks[$i] = New-Object 'System.Collections.Generic.List[int]'; $maxYBlock = $i; if ($i -lt $minYBlock) { $minYBlock = $i } }
        if ($zBlockSize[$i] -gt 0) { $zBlocks[$i] = New-Object 'System.Collections.Generic.List[int]'; $maxZBlock = $i; if ($i -lt $minZBlock) { $minZBlock = $i } }
    }
    for ($i = 0; $i -lt $NumTriangles; $i++) {
        $bound = $bounds[$i]
        for ($x = [int]$bound.minXBlock; $x -le [int]$bound.maxXBlock; $x++) { [void]$xBlocks[$x].Add($i) }
        for ($y = [int]$bound.minYBlock; $y -le [int]$bound.maxYBlock; $y++) { [void]$yBlocks[$y].Add($i) }
        for ($z = [int]$bound.minZBlock; $z -le [int]$bound.maxZBlock; $z++) { [void]$zBlocks[$z].Add($i) }
    }

    $zList = New-UInt16List
    $yList = New-UInt16List
    $xList = New-UInt16List
    $blockList = New-UInt16List
    for ($i = 0; $i -le $maxZBlock + 1; $i++) { Add-UInt16 $zList 0xFFFF }
    $maxZSection = 0

    for ($z = 0; $z -le $maxZBlock; $z++) {
        $maxZYSection = -1
        $ySegmentStart = $yList.Count
        for ($i = 0; $i -le $maxYBlock + 1; $i++) { Add-UInt16 $yList 0xFFFF }

        if ($z -ge $minZBlock) {
            for ($y = 0; $y -le $maxYBlock; $y++) {
                $maxZYXSection = -1
                $xSegmentStart = $xList.Count
                for ($i = 0; $i -le $maxXBlock + 1; $i++) { Add-UInt16 $xList 0xFFFF }

                if ($y -ge $minYBlock) {
                    for ($x = 0; $x -le $maxXBlock; $x++) {
                        $numTrisHere = 0
                        if ($x -ge $minXBlock -and $null -ne $xBlocks[$x]) {
                            foreach ($triIndex in @($xBlocks[$x].ToArray())) {
                                $tri = $bounds[$triIndex]
                                if ([int]$tri.minYBlock -gt $y -or [int]$tri.maxYBlock -lt $y -or [int]$tri.minZBlock -gt $z -or [int]$tri.maxZBlock -lt $z) { continue }
                                $blX1 = $x -shl 8
                                $blX2 = ($x + 1) -shl 8
                                $blY1 = $y -shl 8
                                $blY2 = ($y + 1) -shl 8
                                $blZ1 = $z -shl 8
                                $blZ2 = ($z + 1) -shl 8
                                $points = @($tri.p1, $tri.p2, $tri.p3)
                                $pointInBlock = $false
                                foreach ($point in $points) {
                                    if ([int]$point.x -ge $blX1 -and [int]$point.x -lt $blX2 -and [int]$point.y -ge $blY1 -and [int]$point.y -lt $blY2 -and [int]$point.z -ge $blZ1 -and [int]$point.z -lt $blZ2) {
                                        $pointInBlock = $true
                                        break
                                    }
                                }
                                if (-not $pointInBlock) {
                                    for ($p = 0; $p -lt 3 -and -not $pointInBlock; $p++) {
                                        $cur = $points[$p]
                                        $next = $points[($p + 1) % 3]
                                        if ([int]$next.x -ne [int]$cur.x) {
                                            foreach ($testPlane in @($blX1, $blX2)) {
                                                if (([int]$cur.x -ge $testPlane -and [int]$next.x -le $testPlane) -or ([int]$cur.x -le $testPlane -and [int]$next.x -ge $testPlane)) {
                                                    $testY = [int]$cur.y + ([int](([int]$next.y - [int]$cur.y) * ($testPlane - [int]$cur.x) / ([int]$next.x - [int]$cur.x)))
                                                    $testZ = [int]$cur.z + ([int](([int]$next.z - [int]$cur.z) * ($testPlane - [int]$cur.x) / ([int]$next.x - [int]$cur.x)))
                                                    if ($testY -ge $blY1 -and $testY -lt $blY2 -and $testZ -ge $blZ1 -and $testZ -lt $blZ2) { $pointInBlock = $true; break }
                                                }
                                            }
                                        }
                                        if ([int]$next.y -ne [int]$cur.y -and -not $pointInBlock) {
                                            foreach ($testPlane in @($blY1, $blY2)) {
                                                if (([int]$cur.y -ge $testPlane -and [int]$next.y -le $testPlane) -or ([int]$cur.y -le $testPlane -and [int]$next.y -ge $testPlane)) {
                                                    $testX = [int]$cur.x + ([int](([int]$next.x - [int]$cur.x) * ($testPlane - [int]$cur.y) / ([int]$next.y - [int]$cur.y)))
                                                    $testZ = [int]$cur.z + ([int](([int]$next.z - [int]$cur.z) * ($testPlane - [int]$cur.y) / ([int]$next.y - [int]$cur.y)))
                                                    if ($testX -ge $blX1 -and $testX -lt $blX2 -and $testZ -ge $blZ1 -and $testZ -lt $blZ2) { $pointInBlock = $true; break }
                                                }
                                            }
                                        }
                                    }
                                }
                                if (-not $pointInBlock) { continue }
                                if ($numTrisHere -eq 0) { Add-UInt16 $blockList ([int]$triIndex -bor 0x8000) }
                                else { Add-UInt16 $blockList ([int]$triIndex) }
                                $numTrisHere++
                            }
                        }

                        if ($numTrisHere -gt 0) {
                            Set-UInt16 $xList ($xSegmentStart + 1 + $x) ($blockList.Count - $numTrisHere)
                            $maxZYXSection = $x
                        }
                    }
                }

                if ($maxZYXSection -ne -1) {
                    Set-UInt16 $xList $xSegmentStart ($maxZYXSection + 1)
                    Set-UInt16 $yList ($ySegmentStart + 1 + $y) ($xSegmentStart * 2)
                    if ($maxZYXSection + 2 -lt ($maxXBlock + 2)) {
                        $remove = ($maxXBlock + 2) - ($maxZYXSection + 2)
                        $xList.RemoveRange($xSegmentStart + $maxZYXSection + 2, $remove)
                    }
                    $maxZYSection = $y
                }
                else {
                    $xList.RemoveRange($xSegmentStart, $maxXBlock + 2)
                }
            }
        }

        if ($maxZYSection -ne -1) {
            Set-UInt16 $yList $ySegmentStart ($maxZYSection + 1)
            Set-UInt16 $zList (1 + $z) ($ySegmentStart * 2)
            if ($maxZYSection + 2 -lt ($maxYBlock + 2)) {
                $remove = ($maxYBlock + 2) - ($maxZYSection + 2)
                $yList.RemoveRange($ySegmentStart + $maxZYSection + 2, $remove)
            }
            $maxZSection = $z
        }
        else {
            $yList.RemoveRange($ySegmentStart, $maxYBlock + 2)
        }
    }

    Set-UInt16 $zList 0 ($maxZSection + 1)
    if ($maxZSection + 2 -lt $zList.Count) {
        $zList.RemoveRange($maxZSection + 2, $zList.Count - ($maxZSection + 2))
    }

    $zLen = $zList.Count
    $yLen = $yList.Count
    for ($i = 0; $i -lt $zList.Count;) {
        $len = [int]$zList[$i]
        for ($j = 0; $j -lt $len; $j++) {
            $index = $i + 1 + $j
            if ($zList[$index] -ne [uint16]0xFFFF) { Set-UInt16 $zList $index ([int]$zList[$index] + ($zLen * 2)) }
        }
        $i += $len + 1
    }
    for ($i = 0; $i -lt $yList.Count;) {
        $len = [int]$yList[$i]
        for ($j = 0; $j -lt $len; $j++) {
            $index = $i + 1 + $j
            if ($yList[$index] -ne [uint16]0xFFFF) { Set-UInt16 $yList $index ([int]$yList[$index] + (($zLen + $yLen) * 2)) }
        }
        $i += $len + 1
    }

    $treeBytes = New-Object byte[] (($zList.Count + $yList.Count + $xList.Count) * 2)
    $offset = 0
    foreach ($list in @($zList, $yList, $xList)) {
        $bytes = Convert-UInt16ListToBytes $list
        [Array]::Copy($bytes, 0, $treeBytes, $offset, $bytes.Length)
        $offset += $bytes.Length
    }
    $blockBytes = Convert-UInt16ListToBytes $blockList

    if ($treeBytes.Length -gt $TreeCapacityBytes) { throw ("Rebuilt collision tree is {0} bytes; capacity is {1}." -f $treeBytes.Length, $TreeCapacityBytes) }
    if ($blockBytes.Length -gt $BlockCapacityBytes) { throw ("Rebuilt collision blocks are {0} bytes; capacity is {1}." -f $blockBytes.Length, $BlockCapacityBytes) }

    return [pscustomobject]([ordered]@{
        treeBytes = $treeBytes
        blockBytes = $blockBytes
        treeBytesLength = $treeBytes.Length
        blockBytesLength = $blockBytes.Length
        minBlock = [pscustomobject]([ordered]@{ x = $minXBlock; y = $minYBlock; z = $minZBlock })
        maxBlock = [pscustomobject]([ordered]@{ x = $maxXBlock; y = $maxYBlock; z = $maxZBlock })
    })
}

function New-CollTriBytesFromMatch($Match) {
    $oldWords = @(Get-ArrayField $Match "oldWords")
    if ($oldWords.Count -lt 3) { throw "Collision match is missing oldWords." }
    $oldZWord = [uint32](Convert-HexTextToInt64 ([string]$oldWords[2]))
    $flags = [uint32]($oldZWord -band 0xC000)

    $points = @()
    foreach ($vertex in @(Get-ArrayField $Match "matchedVertices")) {
        $points += [pscustomobject]([ordered]@{
            x = [int](Get-Field $vertex "x" 0)
            y = [int](Get-Field $vertex "y" 0)
            z = [double](Get-Field $vertex "editedZ" 0.0)
            vertexIndex = [int](Get-Field $vertex "vertexIndex" -1)
        })
    }
    if ($points.Count -ne 3) { throw "Collision match needs exactly 3 matched vertices." }

    $orders = @(
        @(0, 1, 2),
        @(1, 2, 0),
        @(2, 0, 1)
    )
    foreach ($order in $orders) {
        $packed = Try-PackCollTri $points[$order[0]] $points[$order[1]] $points[$order[2]] $flags
        if ($null -ne $packed) {
            $bytes = New-Object byte[] 12
            [Array]::Copy([BitConverter]::GetBytes([uint32]$packed.xWord), 0, $bytes, 0, 4)
            [Array]::Copy([BitConverter]::GetBytes([uint32]$packed.yWord), 0, $bytes, 4, 4)
            [Array]::Copy([BitConverter]::GetBytes([uint32]$packed.zWord), 0, $bytes, 8, 4)
            return [pscustomobject]([ordered]@{
                bytes = $bytes
                words = @(
                    ("0x{0:X8}" -f [uint32]$packed.xWord),
                    ("0x{0:X8}" -f [uint32]$packed.yWord),
                    ("0x{0:X8}" -f [uint32]$packed.zWord)
                )
                packedPointVertexIndexes = @($packed.points | ForEach-Object { [int]$_.vertexIndex })
            })
        }
    }
    throw "Could not pack collision triangle $([int](Get-Field $Match "triangleIndex" -1)) with edited vertices."
}

function Write-Cue([string]$BinPath, [string]$CueOutPath) {
    $fileName = [System.IO.Path]::GetFileName($BinPath)
    @(
        "FILE `"$fileName`" BINARY",
        "  TRACK 01 MODE2/2352",
        "    INDEX 01 00:00:00"
    ) | Set-Content -LiteralPath $CueOutPath -Encoding ASCII
}

function Write-Markdown($Plan, [string]$Path) {
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add("# Stone Hill Collision Triangle Patch Plan")
    [void]$lines.Add("")
    [void]$lines.Add("Generated: $($Plan.generatedAt)")
    [void]$lines.Add("")
    [void]$lines.Add("- Status: $($Plan.status)")
    [void]$lines.Add("- Collision triangle patches: $($Plan.patchCount)")
    [void]$lines.Add("- Collision index patches: $($Plan.indexPatchCount)")
    [void]$lines.Add("- BIN written: $($Plan.wroteBin)")
    [void]$lines.Add("")
    [void]$lines.Add("| # | Edit | Tri | Source WAD | Runtime offset | Bytes |")
    [void]$lines.Add("|---:|---|---:|---|---|---|")
    $i = 1
    foreach ($patch in @($Plan.patches)) {
        [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | `{5}` -> `{6}` |" -f $i, $patch.runtimeKey, $patch.triangleIndex, $patch.sourceTriangleWadOffset, $patch.runtimeTriangleOffset, $patch.oldBytes, $patch.newBytes))
        $i++
    }
    if (@($Plan.indexPatches).Count -gt 0) {
        [void]$lines.Add("")
        [void]$lines.Add("| Index | Source WAD | Runtime offset | Bytes written | Capacity |")
        [void]$lines.Add("|---|---|---|---:|---:|")
        foreach ($patch in @($Plan.indexPatches)) {
            [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} |" -f $patch.kind, $patch.sourceWadOffset, $patch.runtimeOffset, $patch.byteLength, $patch.capacityBytes))
        }
    }
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedImage = Resolve-WorkspacePath $ImagePath
$resolvedReport = Resolve-WorkspacePath $CollisionReportPath
$resolvedOut = Resolve-WorkspacePath $OutPath
$resolvedCue = if ([string]::IsNullOrWhiteSpace($CuePath)) { [System.IO.Path]::ChangeExtension($resolvedOut, ".cue") } else { Resolve-WorkspacePath $CuePath }
$resolvedPlan = Resolve-WorkspacePath $PlanPath
$resolvedMd = Resolve-WorkspacePath $MarkdownPath

if (-not (Test-Path -LiteralPath $resolvedImage)) { throw "Missing image: $resolvedImage" }
if (-not (Test-Path -LiteralPath $resolvedReport)) { throw "Missing collision bridge report: $resolvedReport" }

$report = Get-Content -LiteralPath $resolvedReport -Raw | ConvertFrom-Json
$matches = @(Get-ArrayField $report "matches")
$patches = New-Object System.Collections.ArrayList
foreach ($match in $matches) {
    $hits = @(Get-ArrayField $match "sourceContextHits")
    if ($hits.Count -ne 1) {
        throw "Collision triangle $([int](Get-Field $match "triangleIndex" -1)) needs exactly one sourceContextHit. Re-run New-StoneHillCollisionBridgeReport.ps1 -SearchWad."
    }
    $sourceWadOffset = Convert-HexTextToInt64 ([string](Get-Field $hits[0] "triangleWadOffset" ""))
    if ($sourceWadOffset -lt 0) { throw "Bad source WAD offset for collision triangle." }
    $packed = New-CollTriBytesFromMatch $match
    [void]$patches.Add([ordered]@{
        runtimeKey = [string](Get-Field $match "runtimeKey" "")
        triangleIndex = [int](Get-Field $match "triangleIndex" -1)
        runtimeTriangleOffset = [string](Get-Field $match "runtimeTriangleOffset" "")
        sourceTriangleWadOffset = ("0x{0:X}" -f $sourceWadOffset)
        sourceTriangleImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $sourceWadOffset))
        oldBytes = [string](Get-Field $match "oldBytes" "")
        newBytes = Get-BytesHex ([byte[]]$packed.bytes)
        oldWords = @(Get-ArrayField $match "oldWords")
        newWords = @($packed.words)
        packedPointVertexIndexes = @($packed.packedPointVertexIndexes)
    })
}

$indexPatchSummaries = New-Object System.Collections.ArrayList
$binaryIndexPatches = New-Object System.Collections.ArrayList
if ($RebuildCollisionIndex) {
    $reportRamPath = [string](Get-Field $report "ramPath" "")
    $resolvedRam = if ([string]::IsNullOrWhiteSpace($RamPath)) { Resolve-WorkspacePath $reportRamPath } else { Resolve-WorkspacePath $RamPath }
    if (-not (Test-Path -LiteralPath $resolvedRam)) { throw "RebuildCollisionIndex needs the original RAM dump: $resolvedRam" }
    $ram = [System.IO.File]::ReadAllBytes($resolvedRam)

    $collisionHeader = Get-Field $report "collisionHeader" $null
    if ($null -eq $collisionHeader) { throw "Collision report is missing collisionHeader." }
    $numTriangles = [int](Get-Field $collisionHeader "numTriangles" 0)
    $triangleOffset = [int](Convert-HexTextToInt64 ([string](Get-Field $collisionHeader "triangleOffset" "")))
    $blockTreeOffset = [int](Convert-HexTextToInt64 ([string](Get-Field $collisionHeader "blockTreeOffset" "")))
    $blocksOffset = [int](Convert-HexTextToInt64 ([string](Get-Field $collisionHeader "blocksOffset" "")))
    if ($numTriangles -le 0 -or $triangleOffset -lt 0 -or $blockTreeOffset -lt 0 -or $blocksOffset -lt 0) {
        throw "Collision report has invalid collision header offsets."
    }
    $triangleBytesLength = $numTriangles * 12
    if (($triangleOffset + $triangleBytesLength) -gt $ram.Length) { throw "Collision triangle table is outside RAM dump." }

    $triangleBytes = New-Object byte[] $triangleBytesLength
    [Array]::Copy($ram, $triangleOffset, $triangleBytes, 0, $triangleBytesLength)
    foreach ($patch in @($patches.ToArray())) {
        $runtimeTriangleOffset = [int](Convert-HexTextToInt64 ([string]$patch.runtimeTriangleOffset)
)
        $relativeOffset = $runtimeTriangleOffset - $triangleOffset
        if ($relativeOffset -lt 0 -or ($relativeOffset + 12) -gt $triangleBytes.Length) {
            throw "Collision triangle patch $($patch.triangleIndex) is outside the triangle table."
        }
        $newBytes = Convert-HexToBytes ([string]$patch.newBytes)
        [Array]::Copy($newBytes, 0, $triangleBytes, $relativeOffset, 12)
    }

    $treeCapacityBytes = $blocksOffset - $blockTreeOffset
    $blockCapacityBytes = $triangleOffset - $blocksOffset
    if ($treeCapacityBytes -le 0 -or $blockCapacityBytes -le 0) { throw "Bad collision index capacities." }
    $indexBytes = New-CollisionIndexBytes $triangleBytes $numTriangles $treeCapacityBytes $blockCapacityBytes

    if ($patches.Count -le 0) { throw "Cannot infer collision runtime/source delta without at least one triangle patch." }
    $firstPatch = $patches[0]
    $runtimeFirst = Convert-HexTextToInt64 ([string]$firstPatch.runtimeTriangleOffset)
    $sourceFirst = Convert-HexTextToInt64 ([string]$firstPatch.sourceTriangleWadOffset)
    $runtimeToWadDelta = $sourceFirst - $runtimeFirst
    $sourceBlockTreeWadOffset = [int64]$blockTreeOffset + $runtimeToWadDelta
    $sourceBlocksWadOffset = [int64]$blocksOffset + $runtimeToWadDelta

    $oldTreeBytes = New-Object byte[] ([byte[]]$indexBytes.treeBytes).Length
    [Array]::Copy($ram, $blockTreeOffset, $oldTreeBytes, 0, $oldTreeBytes.Length)
    $oldBlockBytes = New-Object byte[] ([byte[]]$indexBytes.blockBytes).Length
    [Array]::Copy($ram, $blocksOffset, $oldBlockBytes, 0, $oldBlockBytes.Length)

    [void]$binaryIndexPatches.Add([ordered]@{
        kind = "blockTree"
        sourceWadOffset = $sourceBlockTreeWadOffset
        runtimeOffset = $blockTreeOffset
        oldBytes = $oldTreeBytes
        newBytes = [byte[]]$indexBytes.treeBytes
        capacityBytes = $treeCapacityBytes
    })
    [void]$binaryIndexPatches.Add([ordered]@{
        kind = "blocks"
        sourceWadOffset = $sourceBlocksWadOffset
        runtimeOffset = $blocksOffset
        oldBytes = $oldBlockBytes
        newBytes = [byte[]]$indexBytes.blockBytes
        capacityBytes = $blockCapacityBytes
    })

    foreach ($patch in @($binaryIndexPatches.ToArray())) {
        [void]$indexPatchSummaries.Add([ordered]@{
            kind = [string]$patch.kind
            sourceWadOffset = ("0x{0:X}" -f [int64]$patch.sourceWadOffset)
            sourceImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset ([int64]$patch.sourceWadOffset))
)
            runtimeOffset = ("0x{0:X}" -f [int]$patch.runtimeOffset)
            byteLength = ([byte[]]$patch.newBytes).Length
            capacityBytes = [int]$patch.capacityBytes
            oldFirst16 = Get-BytesHex (([byte[]]$patch.oldBytes)[0..([Math]::Min(15, ([byte[]]$patch.oldBytes).Length - 1))])
            newFirst16 = Get-BytesHex (([byte[]]$patch.newBytes)[0..([Math]::Min(15, ([byte[]]$patch.newBytes).Length - 1))])
        })
    }
}

$wroteBin = $false
if ($AllowExperimentalWrite) {
    $image = [System.IO.File]::ReadAllBytes($resolvedImage)
    foreach ($patch in @($patches.ToArray())) {
        $wadOffset = Convert-HexTextToInt64 ([string]$patch.sourceTriangleWadOffset)
        $oldBytes = Convert-HexToBytes ([string]$patch.oldBytes)
        $existing = Get-ImageBytesForWadOffset $image $wadOffset $oldBytes.Length
        if ((Get-BytesHex $existing) -ne [string]$patch.oldBytes) {
            throw ("Source bytes at {0} are {1}, expected {2}." -f $patch.sourceTriangleWadOffset, (Get-BytesHex $existing), $patch.oldBytes)
        }
        Set-ImageBytesForWadOffset $image $wadOffset (Convert-HexToBytes ([string]$patch.newBytes))
    }
    foreach ($patch in @($binaryIndexPatches.ToArray())) {
        $wadOffset = [int64]$patch.sourceWadOffset
        $oldBytes = [byte[]]$patch.oldBytes
        $existing = Get-ImageBytesForWadOffset $image $wadOffset $oldBytes.Length
        if ((Get-BytesHex $existing) -ne (Get-BytesHex $oldBytes)) {
            throw ("Source collision index bytes at 0x{0:X} did not match the original RAM bytes." -f $wadOffset)
        }
        Set-ImageBytesForWadOffset $image $wadOffset ([byte[]]$patch.newBytes)
    }
    [System.IO.File]::WriteAllBytes($resolvedOut, $image)
    Write-Cue $resolvedOut $resolvedCue
    $wroteBin = $true
}
elseif (-not $PlanOnly) {
    Write-Warning "PlanOnly was not set, but AllowExperimentalWrite was also not set. Wrote a plan only."
}

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    status = if ($wroteBin) { "experimental-collision-triangle-bin-written" } else { "plan-ready" }
    imagePath = (Resolve-Path -LiteralPath $resolvedImage).Path
    collisionReportPath = (Resolve-Path -LiteralPath $resolvedReport).Path
    outPath = $resolvedOut
    cuePath = $resolvedCue
    patchCount = @($patches.ToArray()).Count
    rebuildCollisionIndex = [bool]$RebuildCollisionIndex
    indexPatchCount = @($indexPatchSummaries.ToArray()).Count
    wroteBin = $wroteBin
    patches = @($patches.ToArray())
    indexPatches = @($indexPatchSummaries.ToArray())
}

$plan | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedPlan -Encoding UTF8
Write-Markdown ([pscustomobject]$plan) $resolvedMd

Write-Host "Wrote collision triangle patch plan to $resolvedPlan"
Write-Host "Wrote collision triangle patch summary to $resolvedMd"
if ($wroteBin) {
    Write-Host "Wrote collision triangle patch BIN to $resolvedOut"
    Write-Host "Wrote CUE to $resolvedCue"
}
