param(
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$SceneRuntimeAddress = "0x8008C134",
    [string]$OutJsonPath = ".\stonehill-hp-colour-entry-decode.json",
    [string]$OutMarkdownPath = ".\stonehill-hp-colour-entry-decode.md",
    [int]$TextureRecordCount = 68
)

Set-StrictMode -Version 2.0

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Convert-PsxAddressToRamOffset([uint32]$Pointer, [int]$RamLength) {
    $address = [uint64]$Pointer
    if ($address -lt 0x80000000L -or $address -ge 0x80200000L) { return -1 }
    $offset = [int]($Pointer -band 0x001FFFFF)
    if ($offset -lt 0 -or $offset -ge $RamLength) { return -1 }
    return $offset
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
    return [pscustomobject]@{
        offset = $Offset
        numLpVertices = $numLpVertices
        numLpColours = $numLpColours
        numLpFaces = $numLpFaces
        numHpVertices = $numHpVertices
        numHpColours = $numHpColours
        numHpFaces = $numHpFaces
    }
}

function Add-Count($Map, [string]$Key) {
    if ($Map.ContainsKey($Key)) { $Map[$Key] = [int]$Map[$Key] + 1 }
    else { $Map[$Key] = 1 }
}

function Convert-TopCounts($Map, [int]$Limit) {
    $rows = @()
    foreach ($key in $Map.Keys) {
        $rows += [pscustomobject]@{ value = $key; count = [int]$Map[$key] }
    }
    return @($rows | Sort-Object -Property @{ Expression = { $_.count }; Descending = $true }, value | Select-Object -First $Limit)
}

function New-ByteAccumulator {
    $rows = @()
    for ($i = 0; $i -lt 8; $i++) {
        $rows += @{
            offset = $i
            min = 255
            max = 0
            values = @{}
            inTextureRange = 0
        }
    }
    return $rows
}

function Add-ByteValue($Acc, [int]$Offset, [int]$Value, [int]$TextureRecordCount) {
    $entry = $Acc[$Offset]
    if ($Value -lt [int]$entry.min) { $entry.min = $Value }
    if ($Value -gt [int]$entry.max) { $entry.max = $Value }
    if ($Value -ge 0 -and $Value -lt $TextureRecordCount) { $entry.inTextureRange = [int]$entry.inTextureRange + 1 }
    Add-Count $entry.values ([string]$Value)
}

function Format-Bytes([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $items = @()
    for ($i = 0; $i -lt $Length; $i++) { $items += ("{0:X2}" -f [int]$Bytes[$Offset + $i]) }
    return [string]::Join(" ", $items)
}

function Read-HpColourEntries([byte[]]$Ram, [uint32]$SceneAddress, [int]$TextureRecordCount) {
    $sceneOffset = Convert-PsxAddressToRamOffset $SceneAddress $Ram.Length
    if ($sceneOffset -lt 0) { throw "Scene runtime address is outside main RAM." }
    $sectorCount = [int](Get-UInt32LE $Ram ($sceneOffset + 8))
    if ($sectorCount -le 0 -or $sectorCount -gt 4096) { throw "Scene sector count is not plausible." }

    $byteAcc = New-ByteAccumulator
    $u16Maps = @(@{}, @{}, @{}, @{})
    $u32Maps = @(@{}, @{})
    $entryCount = 0
    $sectorEntries = New-Object System.Collections.Generic.List[object]
    $samples = New-Object System.Collections.Generic.List[object]

    for ($sectorIndex = 0; $sectorIndex -lt $sectorCount; $sectorIndex++) {
        $ptr = [uint32](Get-UInt32LE $Ram ($sceneOffset + 12 + ($sectorIndex * 4)))
        $sectorOffset = Convert-PsxAddressToRamOffset $ptr $Ram.Length
        if ($sectorOffset -lt 0) { continue }
        $sector = Read-SceneSectorHeader $Ram $sectorOffset
        if ($null -eq $sector -or [int]$sector.numHpColours -le 0) { continue }

        $dataStart = [int]$sector.offset + 28
        $lpVertexWords = [int]$sector.numLpVertices
        $lpColourWords = [int]$sector.numLpColours
        $lpFaceWords = [int]$sector.numLpFaces * 2
        $hpVertexStartWords = $lpVertexWords + $lpColourWords + $lpFaceWords
        $hpColourStartWords = $hpVertexStartWords + [int]$sector.numHpVertices
        $hpColourStart = $dataStart + ($hpColourStartWords * 4)

        [void]$sectorEntries.Add([ordered]@{
            sectorIndex = $sectorIndex
            sectorRuntimeAddress = ("0x{0:X8}" -f (0x80000000L + [int64]$sectorOffset))
            hpColourStart = ("0x{0:X8}" -f (0x80000000L + [int64]$hpColourStart))
            numHpColours = [int]$sector.numHpColours
        })

        for ($i = 0; $i -lt [int]$sector.numHpColours; $i++) {
            $offset = $hpColourStart + ($i * 8)
            if (($offset + 8) -gt $Ram.Length) { continue }
            $entryCount++
            for ($b = 0; $b -lt 8; $b++) {
                Add-ByteValue $byteAcc $b ([int]$Ram[$offset + $b]) $TextureRecordCount
            }
            for ($w = 0; $w -lt 4; $w++) {
                Add-Count $u16Maps[$w] ("0x{0:X4}" -f (Get-UInt16LE $Ram ($offset + ($w * 2))))
            }
            for ($w = 0; $w -lt 2; $w++) {
                Add-Count $u32Maps[$w] ("0x{0:X8}" -f (Get-UInt32LE $Ram ($offset + ($w * 4))))
            }
            if ($samples.Count -lt 64) {
                [void]$samples.Add([ordered]@{
                    sectorIndex = $sectorIndex
                    entryIndex = $i
                    runtimeAddress = ("0x{0:X8}" -f (0x80000000L + [int64]$offset))
                    bytes = Format-Bytes $Ram $offset 8
                    rawRgb0 = ("#{0:X2}{1:X2}{2:X2}" -f [int]$Ram[$offset], [int]$Ram[$offset + 1], [int]$Ram[$offset + 2])
                    rawRgb4 = ("#{0:X2}{1:X2}{2:X2}" -f [int]$Ram[$offset + 4], [int]$Ram[$offset + 5], [int]$Ram[$offset + 6])
                    candidateTextureBytes = @(
                        [int]$Ram[$offset + 0],
                        [int]$Ram[$offset + 1],
                        [int]$Ram[$offset + 2],
                        [int]$Ram[$offset + 3],
                        [int]$Ram[$offset + 4],
                        [int]$Ram[$offset + 5],
                        [int]$Ram[$offset + 6],
                        [int]$Ram[$offset + 7]
                    ) | Where-Object { $_ -ge 0 -and $_ -lt $TextureRecordCount }
                })
            }
        }
    }

    $byteSummaries = @()
    for ($i = 0; $i -lt 8; $i++) {
        $entry = $byteAcc[$i]
        $byteSummaries += [ordered]@{
            offset = $i
            uniqueValues = [int]$entry.values.Count
            min = [int]$entry.min
            max = [int]$entry.max
            inTextureRecordRange = [int]$entry.inTextureRange
            inTextureRecordRangeRatio = if ($entryCount -gt 0) { [Math]::Round(([double]$entry.inTextureRange / [double]$entryCount), 4) } else { 0 }
            topValues = @(Convert-TopCounts $entry.values 12)
        }
    }

    $u16Summaries = @()
    for ($i = 0; $i -lt 4; $i++) {
        $u16Summaries += [ordered]@{ wordOffset = $i * 2; uniqueValues = [int]$u16Maps[$i].Count; topValues = @(Convert-TopCounts $u16Maps[$i] 12) }
    }
    $u32Summaries = @()
    for ($i = 0; $i -lt 2; $i++) {
        $u32Summaries += [ordered]@{ wordOffset = $i * 4; uniqueValues = [int]$u32Maps[$i].Count; topValues = @(Convert-TopCounts $u32Maps[$i] 12) }
    }

    return [ordered]@{
        sceneRuntimeAddress = ("0x{0:X8}" -f $SceneAddress)
        sceneSectors = $sectorCount
        hpColourEntries = $entryCount
        sectorsWithHpColourEntries = [int]$sectorEntries.Count
        byteSummaries = $byteSummaries
        u16Summaries = $u16Summaries
        u32Summaries = $u32Summaries
        sectorSamples = @($sectorEntries.ToArray() | Select-Object -First 24)
        entrySamples = @($samples.ToArray())
    }
}

function Write-MarkdownReport($Report, [string]$Path) {
    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("# Stone Hill HP Colour Entry Decode")
    [void]$lines.Add("")
    [void]$lines.Add(("Generated: {0}" -f $Report.generatedAt))
    [void]$lines.Add("")
    [void]$lines.Add("## Summary")
    [void]$lines.Add("")
    [void]$lines.Add(("- HP colour entries: {0}" -f $Report.decode.hpColourEntries))
    [void]$lines.Add(("- Sectors with HP colour entries: {0}/{1}" -f $Report.decode.sectorsWithHpColourEntries, $Report.decode.sceneSectors))
    [void]$lines.Add(("- Texture-record count used for range checks: {0}" -f $Report.textureRecordCount))
    [void]$lines.Add("")
    [void]$lines.Add("## Byte Fields")
    [void]$lines.Add("")
    [void]$lines.Add("| Offset | Unique | Min | Max | In 0..textureCount-1 | Top values |")
    [void]$lines.Add("| ---: | ---: | ---: | ---: | ---: | --- |")
    foreach ($stat in $Report.decode.byteSummaries) {
        $top = @($stat.topValues | Select-Object -First 5 | ForEach-Object { ("{0} ({1})" -f $_.value, $_.count) })
        [void]$lines.Add(("| +{0} | {1} | {2} | {3} | {4:P1} | {5} |" -f $stat.offset, $stat.uniqueValues, $stat.min, $stat.max, [double]$stat.inTextureRecordRangeRatio, [string]::Join(", ", $top)))
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Interpretation")
    [void]$lines.Add("")
    [void]$lines.Add("- Bytes +0..+2 and +4..+6 work as raw RGB lighting/colour triples in the rendered proof sheet.")
    [void]$lines.Add("- If a texture-record index is present in this entry, it is not obvious as a clean single byte: the RGB channels naturally overlap the 0..67 range.")
    [void]$lines.Add("- Byte +3 and byte +7 are the best metadata candidates because they are outside the rendered RGB triples.")
    [void]$lines.Add("")
    [void]$lines.Add("## Sample Entries")
    [void]$lines.Add("")
    foreach ($sample in @($Report.decode.entrySamples | Select-Object -First 24)) {
        [void]$lines.Add(('- `{0}` sector {1} entry {2}: rgb0 {3}, rgb4 {4}, candidate texture-range bytes [{5}]' -f $sample.bytes, $sample.sectorIndex, $sample.entryIndex, $sample.rawRgb0, $sample.rawRgb4, [string]::Join(",", $sample.candidateTextureBytes)))
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Next Check")
    [void]$lines.Add("")
    [void]$lines.Add("The next check is to render the 68 texture records as raw tile/mipmap data and compare their visual clusters with face UV ranges. The colour entry table proves lighting, but does not yet expose a clean texture-record pointer.")
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedRam = (Resolve-Path -LiteralPath $RamPath -ErrorAction Stop).Path
$ram = [System.IO.File]::ReadAllBytes($resolvedRam)
if ($ram.Length -gt 0x200000) {
    $copy = New-Object byte[] 0x200000
    [Array]::Copy($ram, 0, $copy, 0, 0x200000)
    $ram = $copy
}
$sceneAddressText = if ($SceneRuntimeAddress.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $SceneRuntimeAddress.Substring(2) } else { $SceneRuntimeAddress }
$sceneAddressValue = [Convert]::ToUInt32($sceneAddressText, 16)
$decode = Read-HpColourEntries $ram $sceneAddressValue $TextureRecordCount

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceRam = $resolvedRam
    sceneRuntimeAddress = ("0x{0:X8}" -f $sceneAddressValue)
    textureRecordCount = $TextureRecordCount
    decode = $decode
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
($report | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8
Write-MarkdownReport $report $resolvedMarkdown

Write-Host ("Wrote HP colour entry decode to {0}" -f $resolvedJson)
Write-Host ("HP colour entries: {0}; sectors: {1}/{2}" -f $decode.hpColourEntries, $decode.sectorsWithHpColourEntries, $decode.sceneSectors)
