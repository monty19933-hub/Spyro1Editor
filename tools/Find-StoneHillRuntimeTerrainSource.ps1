param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [int]$WadLba = 37,
    [int]$WadSize = 110260224,
    [int]$ContextBytes = 96,
    [switch]$QuickSectorOnly,
    [string]$OutJsonPath = ".\stonehill-runtime-terrain-source-search.json",
    [string]$OutMarkdownPath = ".\stonehill-runtime-terrain-source-search.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

$byteSearchSource = @"
using System;
using System.Collections.Generic;

public static class SpyroBytePatternSearch {
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

function Convert-HexTextToInt([string]$Text) {
    $clean = $Text.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        return [int][Convert]::ToInt64($clean.Substring(2), 16)
    }
    return [int]$clean
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Read-UserSector([System.IO.FileStream]$Stream, [int64]$Lba) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * 2352L) + 24L
    [void]$Stream.Read($buffer, 0, 2048)
    return $buffer
}

function Read-WadBytes([System.IO.FileStream]$Stream, [int]$WadLbaValue, [int64]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int64]$WadLbaValue + [int64][Math]::Floor($absolute / 2048)
        $sectorBytes = Read-UserSector $Stream $sector
        $toCopy = [Math]::Min(2048 - $sectorOffset, $remaining)
        [Array]::Copy($sectorBytes, $sectorOffset, $result, $written, $toCopy)
        $written += $toCopy
        $remaining -= $toCopy
        $absolute += $toCopy
    }
    return $result
}

function Convert-WadOffsetToImageOffset([int64]$WadOffset, [int]$WadLbaValue) {
    $sector = [int64]$WadLbaValue + [int64][Math]::Floor($WadOffset / 2048)
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
        sizeBytes = $sizeBytes
        numLpVertices = $numLpVertices
        numLpColours = $numLpColours
        numLpFaces = $numLpFaces
        numHpVertices = $numHpVertices
        numHpColours = $numHpColours
        numHpFaces = $numHpFaces
    }
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

function Find-BytePattern([byte[]]$Haystack, [byte[]]$Needle, [int]$MaxHits = 64) {
    return @([SpyroBytePatternSearch]::FindAll($Haystack, $Needle, $MaxHits))
}

function Get-BytesHex([byte[]]$Bytes) {
    return (($Bytes | ForEach-Object { $_.ToString("X2") }) -join "")
}

$resolvedImage = Resolve-WorkspacePath $ImagePath
$resolvedRam = Resolve-WorkspacePath $RamPath
$resolvedEdits = Resolve-WorkspacePath $TerrainEditsPath
if (-not (Test-Path -LiteralPath $resolvedImage)) { throw "Missing image: $resolvedImage" }
if (-not (Test-Path -LiteralPath $resolvedRam)) { throw "Missing RAM dump: $resolvedRam" }
if (-not (Test-Path -LiteralPath $resolvedEdits)) { throw "Missing terrain edits: $resolvedEdits" }

$ram = [System.IO.File]::ReadAllBytes($resolvedRam)
$editsRoot = Get-Content -LiteralPath $resolvedEdits -Raw | ConvertFrom-Json
$edits = @(Get-ArrayField $editsRoot "edits")

$stream = [System.IO.File]::OpenRead($resolvedImage)
try {
    $wad = Read-WadBytes $stream $WadLba 0 $WadSize
}
finally {
    $stream.Dispose()
}

$results = New-Object System.Collections.ArrayList
foreach ($edit in $edits) {
    $sectorOffset = Convert-HexTextToInt ([string](Get-Field $edit "sectorOffset" ""))
    $detail = [string](Get-Field $edit "detail" "hp")
    $sector = Read-SceneSectorHeader $ram $sectorOffset
    if ($null -eq $sector) {
        [void]$results.Add([ordered]@{
            edit = Get-Field $edit "runtimeKey" ""
            status = "bad-runtime-sector"
            sectorOffset = ("0x{0:X}" -f $sectorOffset)
        })
        continue
    }

    $sectorBytes = New-Object byte[] $sector.sizeBytes
    [Array]::Copy($ram, $sectorOffset, $sectorBytes, 0, $sector.sizeBytes)
    $sectorHits = Find-BytePattern $wad $sectorBytes 16

    $vertexSearches = New-Object System.Collections.ArrayList
    if (-not $QuickSectorOnly) {
        foreach ($rawVertexIndex in @(Get-ArrayField $edit "vertexIndexes")) {
            $vertexIndex = [int]$rawVertexIndex
            $vertexOffset = Get-SceneVertexOffset $sector $detail $vertexIndex
            if ($vertexOffset -lt 0) { continue }
            $ctxStart = [Math]::Max($sectorOffset, $vertexOffset - [Math]::Floor($ContextBytes / 2))
            $ctxEnd = [Math]::Min($sectorOffset + $sector.sizeBytes, $vertexOffset + 4 + [Math]::Ceiling($ContextBytes / 2))
            $ctxLength = [int]($ctxEnd - $ctxStart)
            $ctx = New-Object byte[] $ctxLength
            [Array]::Copy($ram, $ctxStart, $ctx, 0, $ctxLength)
            $ctxHits = Find-BytePattern $wad $ctx 32
            $word = New-Object byte[] 4
            [Array]::Copy($ram, $vertexOffset, $word, 0, 4)
            $wordHits = Find-BytePattern $wad $word 64

            [void]$vertexSearches.Add([ordered]@{
                vertexIndex = $vertexIndex
                runtimeVertexOffset = ("0x{0:X}" -f $vertexOffset)
                runtimeWord = Get-BytesHex $word
                contextLength = $ctxLength
                contextHits = @($ctxHits | ForEach-Object {
                    [ordered]@{
                        wadOffset = ("0x{0:X}" -f [int64]$_)
                        imageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset ([int64]$_) $WadLba))
                    }
                })
                wordHitCountCapped = @($wordHits).Count
                wordHitPreview = @($wordHits | Select-Object -First 12 | ForEach-Object {
                    [ordered]@{
                        wadOffset = ("0x{0:X}" -f [int64]$_)
                        imageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset ([int64]$_) $WadLba))
                    }
                })
            })
        }
    }

    [void]$results.Add([ordered]@{
        edit = Get-Field $edit "runtimeKey" ""
        status = "searched"
        sectorOffset = ("0x{0:X}" -f $sectorOffset)
        sectorSizeBytes = [int]$sector.sizeBytes
        fullSectorHits = @($sectorHits | ForEach-Object {
            [ordered]@{
                wadOffset = ("0x{0:X}" -f [int64]$_)
                imageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset ([int64]$_) $WadLba))
            }
        })
        vertexSearches = @($vertexSearches.ToArray())
    })
}

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Search the logical Spyro WAD stream for exact runtime scene-sector terrain bytes from the saved Stone Hill terrain edits."
    imagePath = (Resolve-Path -LiteralPath $resolvedImage).Path
    ramPath = (Resolve-Path -LiteralPath $resolvedRam).Path
    terrainEditsPath = (Resolve-Path -LiteralPath $resolvedEdits).Path
    wadLba = $WadLba
    wadSize = $WadSize
    contextBytes = $ContextBytes
    quickSectorOnly = [bool]$QuickSectorOnly
    results = @($results.ToArray())
}

$resolvedOutJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutJsonPath))
$resolvedOutMd = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutMarkdownPath))
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Stone Hill Runtime Terrain Source Search")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($report.generatedAt)")
[void]$lines.Add("")
[void]$lines.Add("- Context bytes: $ContextBytes")
[void]$lines.Add("- Edits searched: $($results.Count)")
[void]$lines.Add("")
[void]$lines.Add("| Edit | Sector | Full sector hits | Vertex context hits |")
[void]$lines.Add("|---|---|---:|---:|")
foreach ($item in @($results.ToArray())) {
    $ctxHits = 0
    foreach ($vertex in @($item.vertexSearches)) { $ctxHits += @($vertex.contextHits).Count }
    [void]$lines.Add(("| {0} | {1} | {2} | {3} |" -f $item.edit, $item.sectorOffset, @($item.fullSectorHits).Count, $ctxHits))
}
[void]$lines.Add("")
[void]$lines.Add("## Vertex Details")
[void]$lines.Add("")
foreach ($item in @($results.ToArray())) {
    [void]$lines.Add("### $($item.edit)")
    foreach ($vertex in @($item.vertexSearches)) {
        $hits = @($vertex.contextHits)
        $hitText = if ($hits.Count -eq 0) { "none" } else { (($hits | ForEach-Object { $_.wadOffset }) -join ", ") }
        [void]$lines.Add(("- v{0}: word `{1}`, context hits: {2}" -f $vertex.vertexIndex, $vertex.runtimeWord, $hitText))
    }
    [void]$lines.Add("")
}
[System.IO.File]::WriteAllLines($resolvedOutMd, $lines.ToArray(), [System.Text.Encoding]::UTF8)

Write-Host "Wrote runtime terrain source search to $resolvedOutJson"
Write-Host "Wrote runtime terrain source search summary to $resolvedOutMd"
