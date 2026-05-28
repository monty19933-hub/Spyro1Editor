param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$WadAnalysisPath = ".\spyro-wad-analysis.json",
    [int[]]$EntryIndexes = @(10, 22),
    [int[]]$TextureIds = @(0x2E, 0x2F, 0x41, 0x42, 0x43, 0x44, 0x45),
    [int]$WadLba = 37
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $WorkspaceRoot $Path
}

function Get-U16([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-U32([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Read-WadBytes([IO.FileStream]$Stream, [int64]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int64]$WadLba + [int64][Math]::Floor($absolute / 2048)
        $imageOffset = ($sector * 2352L) + 24L + $sectorOffset
        $toRead = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = $imageOffset
        [void]$Stream.Read($result, $written, $toRead)
        $written += $toRead
        $remaining -= $toRead
        $absolute += $toRead
    }
    return $result
}

function Decode-TexDescriptor([byte[]]$Bytes, [int]$Offset, [int]$DescriptorIndex) {
    $xmin = [int]$Bytes[$Offset]
    $ymin = [int]$Bytes[$Offset + 1]
    $palette = [int](Get-U16 $Bytes ($Offset + 2))
    $xmax = [int]$Bytes[$Offset + 4]
    $ymax = [int]$Bytes[$Offset + 5]
    $region = [int]$Bytes[$Offset + 6]
    $unknown = [int]$Bytes[$Offset + 7]
    [pscustomobject]@{
        descriptor = $DescriptorIndex
        xmin = $xmin
        ymin = $ymin
        palette = $palette
        paletteByteStart = $palette * 32
        xmax = $xmax
        ymax = $ymax
        region = $region
        unknown = ("0x{0:X2}" -f $unknown)
        orientation = (($unknown -shr 4) -band 7)
        vramXMin = ((($region * 128) % 2048) + $xmin)
        vramYMin = ([int][Math]::Floor(($region -band 0x1F) / 16.0) * 256) + $ymin
        vramXMax = ((($region * 128) % 2048) + $xmax)
        vramYMax = ([int][Math]::Floor(($region -band 0x1F) / 16.0) * 256) + $ymax
    }
}

$wad = Get-Content -LiteralPath (Resolve-WorkspacePath $WadAnalysisPath) -Raw | ConvertFrom-Json
$stream = [IO.File]::OpenRead((Resolve-WorkspacePath $ImagePath))
try {
    foreach ($entryIndex in $EntryIndexes) {
        $entry = $wad.entries | Where-Object { [int]$_.index -eq $entryIndex } | Select-Object -First 1
        if ($null -eq $entry) { throw "Missing WAD entry $entryIndex" }
        $header = Read-WadBytes $stream ([int64]$entry.offset) 0x200
        $subfile1Offset = [int64](Get-U32 $header 8)
        $subfile1Size = [int](Get-U32 $header 12)
        $subfile1 = Read-WadBytes $stream ([int64]$entry.offset + $subfile1Offset) $subfile1Size
        $textureListSize = [int](Get-U32 $subfile1 0)
        $textureCount = [int](Get-U32 $subfile1 4)
        $recordBytes = [int](($textureListSize - 8) / [Math]::Max(1, $textureCount))

        Write-Host ""
        Write-Host ("Entry {0}: subfile1 offset 0x{1:X}, size 0x{2:X}, textureList 0x{3:X}, count {4}, recordBytes {5}" -f $entryIndex, $subfile1Offset, $subfile1Size, $textureListSize, $textureCount, $recordBytes)
        foreach ($textureId in $TextureIds) {
            if ($textureId -lt 0 -or $textureId -ge $textureCount) { continue }
            $recordOffset = 8 + ($textureId * $recordBytes)
            Write-Host ("Texture {0} / 0x{0:X2} at record offset 0x{1:X}" -f $textureId, $recordOffset)
            $rows = New-Object System.Collections.Generic.List[object]
            for ($i = 0; $i -lt 4; $i++) {
                [void]$rows.Add((Decode-TexDescriptor $subfile1 ($recordOffset + 24 + ($i * 8)) $i))
            }
            $rows | Format-Table -AutoSize
        }
    }
}
finally {
    $stream.Dispose()
}
