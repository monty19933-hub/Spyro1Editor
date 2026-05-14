param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$SceneRuntimeAddress = "0x8008C134",
    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [string]$OutJsonPath = ".\stonehill-visual-mesh-texture-report.json",
    [string]$OutMarkdownPath = ".\stonehill-visual-mesh-texture-report.md"
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

function Read-UserSector([System.IO.FileStream]$Stream, [int]$Lba) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * 2352L) + 24L
    [void]$Stream.Read($buffer, 0, 2048)
    return $buffer
}

function Read-WadBytes([System.IO.FileStream]$Stream, [int]$WadLba, [int64]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int]($WadLba + [Math]::Floor($absolute / 2048))
        $sectorBytes = Read-UserSector $Stream $sector
        $toCopy = [Math]::Min(2048 - $sectorOffset, $remaining)
        [Array]::Copy($sectorBytes, $sectorOffset, $result, $written, $toCopy)
        $written += $toCopy
        $remaining -= $toCopy
        $absolute += $toCopy
    }
    return $result
}

function Parse-ArchiveHeader([byte[]]$Bytes, [int64]$ArchiveSize) {
    $entries = @()
    $firstDataOffset = [int64](Get-UInt32LE $Bytes 0)
    if ($firstDataOffset -le 0 -or $firstDataOffset -gt $Bytes.Length) { $firstDataOffset = $Bytes.Length }
    for ($offset = 0; $offset -le ([Math]::Min($Bytes.Length, $firstDataOffset) - 8); $offset += 8) {
        $fileOffset = [int64](Get-UInt32LE $Bytes $offset)
        $fileSize = [int64](Get-UInt32LE $Bytes ($offset + 4))
        if ($fileOffset -eq 0 -and $fileSize -eq 0) { continue }
        if ($fileOffset -lt 0 -or $fileSize -le 0 -or ($fileOffset + $fileSize) -gt $ArchiveSize) { continue }
        $entries += [pscustomobject]@{ index = [int]($offset / 8); offset = $fileOffset; size = $fileSize }
    }
    return $entries
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
        runtimeAddress = ("0x{0:X8}" -f (0x80000000L + [int64]$Offset))
        numLpVertices = $numLpVertices
        numLpColours = $numLpColours
        numLpFaces = $numLpFaces
        numHpVertices = $numHpVertices
        numHpColours = $numHpColours
        numHpFaces = $numHpFaces
        sizeBytes = $sizeBytes
    }
}

function Add-Count($Map, [string]$Key) {
    if ($Map.ContainsKey($Key)) { $Map[$Key] = [int]$Map[$Key] + 1 }
    else { $Map[$Key] = 1 }
}

function Add-ByteStat($Stats, [int]$Offset, [int]$Value) {
    $key = [string]$Offset
    if (-not $Stats.ContainsKey($key)) {
        $Stats[$key] = @{
            offset = $Offset
            min = 255
            max = 0
            counts = @{}
        }
    }
    $entry = $Stats[$key]
    if ($Value -lt [int]$entry.min) { $entry.min = $Value }
    if ($Value -gt [int]$entry.max) { $entry.max = $Value }
    Add-Count $entry.counts ([string]$Value)
}

function Add-TopValue($Map, [string]$Value) {
    Add-Count $Map $Value
}

function Convert-TopCounts($Map, [int]$Limit) {
    $rows = @()
    foreach ($key in $Map.Keys) {
        $rows += [pscustomobject]@{ value = $key; count = [int]$Map[$key] }
    }
    return @($rows | Sort-Object -Property @{ Expression = { $_.count }; Descending = $true }, value | Select-Object -First $Limit)
}

function Convert-ByteStats($Stats) {
    $rows = @()
    foreach ($key in $Stats.Keys) {
        $entry = $Stats[$key]
        $rows += [pscustomobject]@{
            offset = [int]$entry.offset
            uniqueValues = [int]$entry.counts.Count
            min = [int]$entry.min
            max = [int]$entry.max
            topValues = @(Convert-TopCounts $entry.counts 8)
        }
    }
    return @($rows | Sort-Object offset)
}

function New-FaceAccumulator([string]$Name, [int]$RecordBytes) {
    return @{
        name = $Name
        recordBytes = $RecordBytes
        faces = 0
        invalidIndexFaces = 0
        byteStats = @{}
        u16Stats = @{}
        u32Stats = @{}
        samples = New-Object System.Collections.Generic.List[string]
    }
}

function Add-FaceRecord($Acc, [byte[]]$Ram, [int]$Offset, [int]$RecordBytes, [int]$VertexCount) {
    $Acc.faces = [int]$Acc.faces + 1
    $bad = $false
    for ($i = 0; $i -lt 4; $i++) {
        if ([int]$Ram[$Offset + $i] -ge $VertexCount) { $bad = $true }
    }
    if ($bad) { $Acc.invalidIndexFaces = [int]$Acc.invalidIndexFaces + 1 }

    for ($i = 4; $i -lt $RecordBytes; $i++) {
        Add-ByteStat $Acc.byteStats $i ([int]$Ram[$Offset + $i])
    }
    for ($i = 4; $i -le ($RecordBytes - 2); $i += 2) {
        Add-TopValue $Acc.u16Stats ("0x{0:X4}" -f (Get-UInt16LE $Ram ($Offset + $i)))
    }
    for ($i = 4; $i -le ($RecordBytes - 4); $i += 4) {
        Add-TopValue $Acc.u32Stats ("0x{0:X8}" -f (Get-UInt32LE $Ram ($Offset + $i)))
    }
    if ($Acc.samples.Count -lt 12) {
        $bytes = @()
        for ($i = 0; $i -lt $RecordBytes; $i++) { $bytes += ("{0:X2}" -f [int]$Ram[$Offset + $i]) }
        [void]$Acc.samples.Add([string]::Join(" ", $bytes))
    }
}

function Read-ColourWords([byte[]]$Ram, [int]$StartOffset, [int]$WordCount, $TopMap) {
    for ($i = 0; $i -lt $WordCount; $i++) {
        $word = Get-UInt32LE $Ram ($StartOffset + ($i * 4))
        Add-TopValue $TopMap ("0x{0:X8}" -f $word)
    }
}

function Analyze-RuntimeScene([byte[]]$Ram, [uint32]$SceneRuntimeAddress) {
    $sceneOffset = Convert-PsxAddressToRamOffset $SceneRuntimeAddress $Ram.Length
    if ($sceneOffset -lt 0) { throw "Scene runtime address is outside main RAM." }
    $numSectors = [int](Get-UInt32LE $Ram ($sceneOffset + 8))
    if ($numSectors -le 0 -or $numSectors -gt 4096) { throw "Scene sector count is not plausible." }

    $lpAcc = New-FaceAccumulator "lp" 8
    $hpAcc = New-FaceAccumulator "hp" 16
    $lpColourTop = @{}
    $hpColourTop = @{}
    $sectorRows = New-Object System.Collections.Generic.List[object]

    $valid = 0
    $lpVertices = 0
    $lpColours = 0
    $lpFaces = 0
    $hpVertices = 0
    $hpColours = 0
    $hpFaces = 0

    for ($i = 0; $i -lt $numSectors; $i++) {
        $ptr = [uint32](Get-UInt32LE $Ram ($sceneOffset + 12 + ($i * 4)))
        $sectorOffset = Convert-PsxAddressToRamOffset $ptr $Ram.Length
        if ($sectorOffset -lt 0) { continue }
        $sector = Read-SceneSectorHeader $Ram $sectorOffset
        if ($null -eq $sector) { continue }
        $valid++
        $lpVertices += [int]$sector.numLpVertices
        $lpColours += [int]$sector.numLpColours
        $lpFaces += [int]$sector.numLpFaces
        $hpVertices += [int]$sector.numHpVertices
        $hpColours += [int]$sector.numHpColours
        $hpFaces += [int]$sector.numHpFaces
        if ($sectorRows.Count -lt 16) { [void]$sectorRows.Add($sector) }

        $dataStart = [int]$sector.offset + 28
        $lpVertexWords = [int]$sector.numLpVertices
        $lpColourWords = [int]$sector.numLpColours
        $lpFaceWords = [int]$sector.numLpFaces * 2
        $lpColourStart = $dataStart + ($lpVertexWords * 4)
        $lpFaceStart = $dataStart + (($lpVertexWords + $lpColourWords) * 4)
        Read-ColourWords $Ram $lpColourStart $lpColourWords $lpColourTop
        for ($face = 0; $face -lt [int]$sector.numLpFaces; $face++) {
            Add-FaceRecord $lpAcc $Ram ($lpFaceStart + ($face * 8)) 8 ([int]$sector.numLpVertices)
        }

        $hpVertexStartWords = $lpVertexWords + $lpColourWords + $lpFaceWords
        $hpColourStartWords = $hpVertexStartWords + [int]$sector.numHpVertices
        $hpFaceStartWords = $hpColourStartWords + ([int]$sector.numHpColours * 2)
        $hpColourStart = $dataStart + ($hpColourStartWords * 4)
        $hpFaceStart = $dataStart + ($hpFaceStartWords * 4)
        Read-ColourWords $Ram $hpColourStart ([int]$sector.numHpColours * 2) $hpColourTop
        for ($face = 0; $face -lt [int]$sector.numHpFaces; $face++) {
            Add-FaceRecord $hpAcc $Ram ($hpFaceStart + ($face * 16)) 16 ([int]$sector.numHpVertices)
        }
    }

    return [ordered]@{
        sceneRuntimeAddress = ("0x{0:X8}" -f $SceneRuntimeAddress)
        sceneOffset = ("0x{0:X}" -f $sceneOffset)
        sectorCount = $numSectors
        validSectors = $valid
        totals = [ordered]@{
            lpVertices = $lpVertices
            lpColourEntries = $lpColours
            lpFaces = $lpFaces
            hpVertices = $hpVertices
            hpColourEntries = $hpColours
            hpFaces = $hpFaces
        }
        sectorSamples = @($sectorRows.ToArray())
        facePayloads = @(
            [ordered]@{
                detail = "lp"
                recordBytes = 8
                faceCount = [int]$lpAcc.faces
                invalidIndexFaces = [int]$lpAcc.invalidIndexFaces
                payloadByteStats = @(Convert-ByteStats $lpAcc.byteStats)
                topPayloadU16 = @(Convert-TopCounts $lpAcc.u16Stats 12)
                topPayloadU32 = @(Convert-TopCounts $lpAcc.u32Stats 12)
                sampleRecords = @($lpAcc.samples.ToArray())
            },
            [ordered]@{
                detail = "hp"
                recordBytes = 16
                faceCount = [int]$hpAcc.faces
                invalidIndexFaces = [int]$hpAcc.invalidIndexFaces
                payloadByteStats = @(Convert-ByteStats $hpAcc.byteStats)
                topPayloadU16 = @(Convert-TopCounts $hpAcc.u16Stats 12)
                topPayloadU32 = @(Convert-TopCounts $hpAcc.u32Stats 12)
                sampleRecords = @($hpAcc.samples.ToArray())
            }
        )
        colourWords = [ordered]@{
            topLpColourWords = @(Convert-TopCounts $lpColourTop 12)
            topHpColourWords = @(Convert-TopCounts $hpColourTop 12)
        }
    }
}

function Analyze-TextureBlock([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $u16Top = @{}
    $u32Top = @{}
    $nonZero = 0
    for ($i = 0; $i -lt $Length; $i++) {
        if ($Bytes[$Offset + $i] -ne 0) { $nonZero++ }
    }
    for ($i = 0; $i -le ($Length - 2); $i += 2) {
        Add-TopValue $u16Top ("0x{0:X4}" -f (Get-UInt16LE $Bytes ($Offset + $i)))
    }
    for ($i = 0; $i -le ($Length - 4); $i += 4) {
        Add-TopValue $u32Top ("0x{0:X8}" -f (Get-UInt32LE $Bytes ($Offset + $i)))
    }
    $firstWords = @()
    for ($i = 0; $i -lt [Math]::Min(24, [Math]::Floor($Length / 4)); $i++) {
        $firstWords += ("0x{0:X8}" -f (Get-UInt32LE $Bytes ($Offset + ($i * 4))))
    }
    return [ordered]@{
        offset = ("0x{0:X}" -f $Offset)
        size = $Length
        nonZeroBytes = $nonZero
        possibleRecordCounts = [ordered]@{
            stride4 = [Math]::Floor($Length / 4)
            stride8 = [Math]::Floor($Length / 8)
            stride12 = [Math]::Floor($Length / 12)
            stride16 = [Math]::Floor($Length / 16)
        }
        firstWords = $firstWords
        topU16 = @(Convert-TopCounts $u16Top 16)
        topU32 = @(Convert-TopCounts $u32Top 16)
    }
}

function Analyze-WadModelTextureSource([string]$ImagePath, [int]$WadLba, [int]$AssetWadIndex, [int]$ModelSubfileIndex) {
    $stream = [System.IO.File]::OpenRead($ImagePath)
    try {
        $wadHeader = Read-WadBytes $stream $WadLba 0 4096
        $wadEntries = @(Parse-ArchiveHeader $wadHeader 200000000)
        $assetEntry = @($wadEntries | Where-Object { $_.index -eq $AssetWadIndex })[0]
        if ($null -eq $assetEntry) { throw "Could not find WAD entry $AssetWadIndex." }
        $assetHeader = Read-WadBytes $stream $WadLba $assetEntry.offset 4096
        $subfiles = @(Parse-ArchiveHeader $assetHeader $assetEntry.size)
        $modelSubfile = @($subfiles | Where-Object { $_.index -eq $ModelSubfileIndex })[0]
        if ($null -eq $modelSubfile) { throw "Could not find model subfile $ModelSubfileIndex." }
        $modelBytes = Read-WadBytes $stream $WadLba ($assetEntry.offset + $modelSubfile.offset) ([int]$modelSubfile.size)

        $textureListSize = [int](Get-UInt32LE $modelBytes 0)
        $groundJump = if ($textureListSize -gt 0 -and ($textureListSize + 4) -le $modelBytes.Length) { [int](Get-UInt32LE $modelBytes $textureListSize) } else { 0 }
        $groundModelOffset = $textureListSize + 4
        $groundModelSize = if ($groundJump -gt 4) { $groundJump - 4 } else { 0 }
        $partCount = if ($groundModelOffset -ge 0 -and ($groundModelOffset + 4) -le $modelBytes.Length) { [int](Get-UInt32LE $modelBytes $groundModelOffset) } else { 0 }
        $textureBlock = if ($textureListSize -gt 0 -and $textureListSize -le $modelBytes.Length) { Analyze-TextureBlock $modelBytes 0 $textureListSize } else { $null }

        return [ordered]@{
            wadLba = $WadLba
            assetWadIndex = $AssetWadIndex
            assetOffset = ("0x{0:X}" -f [int64]$assetEntry.offset)
            assetSize = [int64]$assetEntry.size
            modelSubfileIndex = $ModelSubfileIndex
            modelSubfileOffset = ("0x{0:X}" -f [int64]$modelSubfile.offset)
            modelSubfileSize = [int64]$modelSubfile.size
            textureListSize = $textureListSize
            groundJump = $groundJump
            groundModelOffset = ("0x{0:X}" -f $groundModelOffset)
            groundModelSize = $groundModelSize
            documentedPartCount = $partCount
            textureBlockStats = $textureBlock
        }
    }
    finally {
        $stream.Close()
    }
}

function Write-MarkdownReport($Report, [string]$Path) {
    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("# Stone Hill Visual Mesh / Texture References")
    [void]$lines.Add("")
    [void]$lines.Add(("Generated: {0}" -f $Report.generatedAt))
    [void]$lines.Add("")
    [void]$lines.Add("## Current Read")
    [void]$lines.Add("")
    [void]$lines.Add("- Stone Hill already has a decoded runtime scene mesh. This is the best visual-mesh lead, not just a collision placeholder.")
    [void]$lines.Add("- The exporter currently uses the first four bytes of each face record as vertex indexes and ignores the remaining face payload bytes.")
    [void]$lines.Add("- Those remaining bytes plus the WAD model subfile texture-list block are the immediate texture-reference targets.")
    [void]$lines.Add("- High-poly face indexes validate cleanly; low-poly face records appear to use a different packed/flagged form and should not drive the first textured renderer.")
    [void]$lines.Add("")
    [void]$lines.Add("## Runtime Scene")
    [void]$lines.Add("")
    [void]$lines.Add(('Scene: `{0}` / offset `{1}`' -f $Report.runtimeScene.sceneRuntimeAddress, $Report.runtimeScene.sceneOffset))
    [void]$lines.Add(("Sectors: {0}/{1} valid" -f $Report.runtimeScene.validSectors, $Report.runtimeScene.sectorCount))
    [void]$lines.Add(("High poly: {0} vertices, {1} colour entries, {2} faces" -f $Report.runtimeScene.totals.hpVertices, $Report.runtimeScene.totals.hpColourEntries, $Report.runtimeScene.totals.hpFaces))
    [void]$lines.Add(("Low poly: {0} vertices, {1} colour entries, {2} faces" -f $Report.runtimeScene.totals.lpVertices, $Report.runtimeScene.totals.lpColourEntries, $Report.runtimeScene.totals.lpFaces))
    [void]$lines.Add("")
    [void]$lines.Add("## Face Payloads")
    [void]$lines.Add("")
    foreach ($payload in $Report.runtimeScene.facePayloads) {
        [void]$lines.Add(("### {0} faces" -f ([string]$payload.detail).ToUpperInvariant()))
        [void]$lines.Add("")
        [void]$lines.Add(("- Record size: {0} bytes" -f $payload.recordBytes))
        [void]$lines.Add(("- Face count: {0}" -f $payload.faceCount))
        [void]$lines.Add(("- Faces with out-of-range first-four index bytes: {0}" -f $payload.invalidIndexFaces))
        [void]$lines.Add("")
        [void]$lines.Add("| Payload byte | Unique | Min | Max | Top values |")
        [void]$lines.Add("| --- | ---: | ---: | ---: | --- |")
        foreach ($stat in $payload.payloadByteStats) {
            $top = @($stat.topValues | Select-Object -First 4 | ForEach-Object { ("{0} ({1})" -f $_.value, $_.count) })
            [void]$lines.Add(("| +{0} | {1} | {2} | {3} | {4} |" -f $stat.offset, $stat.uniqueValues, $stat.min, $stat.max, [string]::Join(", ", $top)))
        }
        [void]$lines.Add("")
        [void]$lines.Add("Sample face records:")
        [void]$lines.Add("")
        foreach ($sample in $payload.sampleRecords) {
            [void]$lines.Add(('- `{0}`' -f $sample))
        }
        [void]$lines.Add("")
    }
    [void]$lines.Add("## WAD Model / Texture Block")
    [void]$lines.Add("")
    [void]$lines.Add(("Asset WAD entry: {0}, model subfile: {1}" -f $Report.wadModel.assetWadIndex, $Report.wadModel.modelSubfileIndex))
    [void]$lines.Add(('Model subfile offset/size: `{0}` / {1}' -f $Report.wadModel.modelSubfileOffset, $Report.wadModel.modelSubfileSize))
    [void]$lines.Add(("Texture-list jump/size: {0} bytes" -f $Report.wadModel.textureListSize))
    [void]$lines.Add(('Ground-model offset/size: `{0}` / {1} bytes' -f $Report.wadModel.groundModelOffset, $Report.wadModel.groundModelSize))
    [void]$lines.Add(("Documented ground parts: {0}" -f $Report.wadModel.documentedPartCount))
    if ($null -ne $Report.wadModel.textureBlockStats) {
        [void]$lines.Add("")
        [void]$lines.Add("Texture block first words:")
        [void]$lines.Add("")
        foreach ($word in $Report.wadModel.textureBlockStats.firstWords) {
            [void]$lines.Add(('- `{0}`' -f $word))
        }
        [void]$lines.Add("")
        [void]$lines.Add("Texture block possible record counts:")
        [void]$lines.Add("")
        [void]$lines.Add(("- stride 4: {0}" -f $Report.wadModel.textureBlockStats.possibleRecordCounts.stride4))
        [void]$lines.Add(("- stride 8: {0}" -f $Report.wadModel.textureBlockStats.possibleRecordCounts.stride8))
        [void]$lines.Add(("- stride 12: {0}" -f $Report.wadModel.textureBlockStats.possibleRecordCounts.stride12))
        [void]$lines.Add(("- stride 16: {0}" -f $Report.wadModel.textureBlockStats.possibleRecordCounts.stride16))
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Next Decoding Step")
    [void]$lines.Add("")
    [void]$lines.Add("1. Preserve the runtime scene mesh as the editor's visual geometry source.")
    [void]$lines.Add("2. Decode the ignored face payload bytes into colour/texture fields.")
    [void]$lines.Add("3. Match those fields to the model subfile texture-list block and then to VRAM texture pages/palettes.")
    [void]$lines.Add("4. Once the face-to-texture mapping is stable in RAM, map the same scene-sector data back to WAD source bytes for permanent terrain edits.")
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedRam = (Resolve-Path -LiteralPath $RamPath -ErrorAction Stop).Path
$resolvedImage = (Resolve-Path -LiteralPath $ImagePath -ErrorAction Stop).Path
$sceneAddressText = if ($SceneRuntimeAddress.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $SceneRuntimeAddress.Substring(2) } else { $SceneRuntimeAddress }
$sceneAddressValue = [Convert]::ToUInt32($sceneAddressText, 16)
$ramBytes = [System.IO.File]::ReadAllBytes($resolvedRam)
if ($ramBytes.Length -gt 0x200000) {
    $copy = New-Object byte[] 0x200000
    [Array]::Copy($ramBytes, 0, $copy, 0, 0x200000)
    $ramBytes = $copy
}

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceRam = $resolvedRam
    sourceImage = $resolvedImage
    runtimeScene = Analyze-RuntimeScene $ramBytes $sceneAddressValue
    wadModel = Analyze-WadModelTextureSource $resolvedImage $WadLba $AssetWadIndex $ModelSubfileIndex
    sourceNotes = @(
        "Runtime scene decode follows the existing SpyroEdit-style sector structure used by Export-SpyroRuntimeSceneOverlay.ps1.",
        "Texture notes are a discovery report only; they do not yet prove UV/tpage/CLUT field meanings."
    )
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
($report | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8
Write-MarkdownReport $report $resolvedMarkdown

Write-Host ("Wrote visual mesh/texture report to {0}" -f $resolvedJson)
Write-Host ("Wrote markdown summary to {0}" -f $resolvedMarkdown)
Write-Host ("Runtime scene {0}: HP {1} faces, LP {2} faces, texture-list {3} bytes." -f $report.runtimeScene.sceneRuntimeAddress, $report.runtimeScene.totals.hpFaces, $report.runtimeScene.totals.lpFaces, $report.wadModel.textureListSize)
