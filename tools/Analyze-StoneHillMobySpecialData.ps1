param(
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$CatalogPath = ".\stonehill-moby-catalog.json",
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$OutJsonPath = ".\stonehill-moby-special-data.json",
    [string]$OutMarkdownPath = ".\stonehill-moby-special-data.md",
    [int]$BytesToRead = 128
)

Set-StrictMode -Version 2.0

$WadLba = 37
$StoneMetadataOffset = 8333312
$StoneMetadataSize = 57344
$StoneAssetOffset = 8390656
$StoneAssetSize = 3682304

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt16($Bytes, $Offset)
}

function Convert-HexToUInt32([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return [uint32]0 }
    return [Convert]::ToUInt32(($Text -replace "^0x", ""), 16)
}

function Test-PsxPointerInMainRam([uint32]$Pointer) {
    $address = [uint64]$Pointer
    return ($address -ge [Convert]::ToUInt64("80000000", 16) -and $address -lt [Convert]::ToUInt64("80200000", 16))
}

function Convert-PsxAddressToRamOffset([uint32]$Address, [int]$RamLength) {
    if (-not (Test-PsxPointerInMainRam $Address)) { return -1 }
    $offset = [int]($Address -band 0x001FFFFF)
    if ($offset -lt 0 -or $offset -ge $RamLength) { return -1 }
    return $offset
}

function Copy-ByteRange([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $available = [Math]::Max(0, [Math]::Min($Length, $Bytes.Length - $Offset))
    $copy = New-Object byte[] $available
    if ($available -gt 0) {
        [Array]::Copy($Bytes, $Offset, $copy, 0, $available)
    }
    return $copy
}

function Convert-BytesToHex([byte[]]$Bytes, [int]$Max = 64) {
    $limit = [Math]::Min($Bytes.Length, $Max)
    $parts = @()
    for ($i = 0; $i -lt $limit; $i++) {
        $parts += ("{0:X2}" -f $Bytes[$i])
    }
    return ($parts -join " ")
}

function Get-RecordHash([byte[]]$Bytes) {
    if ($Bytes.Count -eq 0) { return "" }
    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        return ([BitConverter]::ToString($sha1.ComputeHash($Bytes)) -replace "-", "")
    }
    finally {
        $sha1.Dispose()
    }
}

function Find-PatternOffsets([byte[]]$Bytes, [byte[]]$Pattern, [int]$MaxMatches = 16) {
    $matches = @()
    if ($Pattern.Length -eq 0 -or $Bytes.Length -lt $Pattern.Length) { return $matches }
    $first = $Pattern[0]
    for ($i = 0; $i -le ($Bytes.Length - $Pattern.Length); $i++) {
        if ($Bytes[$i] -ne $first) { continue }
        $ok = $true
        for ($j = 1; $j -lt $Pattern.Length; $j++) {
            if ($Bytes[$i + $j] -ne $Pattern[$j]) {
                $ok = $false
                break
            }
        }
        if ($ok) {
            $matches += $i
            if ($matches.Count -ge $MaxMatches) { break }
        }
    }
    return $matches
}

function New-MatchResult([string]$Kind, [int[]]$Offsets, [int64]$BaseOffset = 0) {
    return [ordered]@{
        kind = $Kind
        count = @($Offsets).Count
        offsets = @($Offsets | Select-Object -First 16 | ForEach-Object { "0x{0:X}" -f $_ })
        wadRelativeOffsets = @($Offsets | Select-Object -First 16 | ForEach-Object { "0x{0:X}" -f ($BaseOffset + $_) })
    }
}

function Read-UserSector([System.IO.FileStream]$Stream, [int]$Lba) {
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

function Get-SubfileIndex($Subfiles, [int]$Offset) {
    foreach ($subfile in @($Subfiles)) {
        $start = [int]$subfile.offset
        $end = $start + [int]$subfile.size
        if ($Offset -ge $start -and $Offset -lt $end) { return [int]$subfile.index }
    }
    return -1
}

function Parse-ArchiveHeader([byte[]]$Bytes, [int64]$ArchiveSize) {
    $entries = @()
    $firstDataOffset = [int64](Get-UInt32LE $Bytes 0)
    if ($firstDataOffset -le 0 -or $firstDataOffset -gt $Bytes.Length) { $firstDataOffset = $Bytes.Length }
    for ($offset = 0; $offset -le ([Math]::Min($Bytes.Length, $firstDataOffset) - 8); $offset += 8) {
        $fileOffset = [int64](Get-UInt32LE $Bytes $offset)
        $fileSize = [int64](Get-UInt32LE $Bytes ($offset + 4))
        if ($fileOffset -eq 0 -and $fileSize -eq 0) { continue }
        if ($fileOffset -le 0 -or $fileSize -le 0 -or ($fileOffset + $fileSize) -gt $ArchiveSize) { continue }
        $entries += [ordered]@{ index = [int]($offset / 8); offset = $fileOffset; size = $fileSize }
    }
    return $entries
}

function Get-UInt32PointerFields([byte[]]$Bytes, [byte[]]$Ram) {
    $fields = @()
    for ($i = 0; $i -le ($Bytes.Length - 4); $i += 4) {
        $value = Get-UInt32LE $Bytes $i
        if (Test-PsxPointerInMainRam $value) {
            $targetOffset = Convert-PsxAddressToRamOffset $value $Ram.Length
            $targetBytes = @()
            if ($targetOffset -ge 0) {
                $targetBytes = [byte[]](Copy-ByteRange $Ram $targetOffset 32)
            }
            $fields += [ordered]@{
                offset = ("+0x{0:X}" -f $i)
                pointer = ("0x{0:X8}" -f $value)
                targetRamOffset = $(if ($targetOffset -ge 0) { "0x{0:X}" -f $targetOffset } else { "" })
                targetFirst16Hex = $(if (@($targetBytes).Count -gt 0) { Convert-BytesToHex ([byte[]]$targetBytes) 16 } else { "" })
                targetLeadInt16Fields = @(if (@($targetBytes).Count -gt 0) { Get-LeadInt16Fields ([byte[]]$targetBytes) } else { @() })
            }
        }
    }
    return $fields
}

function Get-LeadInt16Fields([byte[]]$Bytes) {
    $fields = @()
    $limit = [Math]::Min($Bytes.Length - 2, 30)
    for ($i = 0; $i -le $limit; $i += 2) {
        $value = Get-Int16LE $Bytes $i
        if ($value -ne 0 -and [Math]::Abs($value) -lt 20000) {
            $fields += ("+0x{0:X}={1}" -f $i, $value)
        }
    }
    return $fields
}

$resolvedRam = Resolve-Path -LiteralPath $RamPath -ErrorAction Stop
$resolvedCatalog = Resolve-Path -LiteralPath $CatalogPath -ErrorAction Stop
$ram = [System.IO.File]::ReadAllBytes($resolvedRam.Path)
$catalog = Get-Content -Raw -LiteralPath $resolvedCatalog.Path | ConvertFrom-Json

$assetBytes = @()
$metadataBytes = @()
$assetSubfiles = @()
$imageResolvedPath = $null
if (Test-Path -LiteralPath $ImagePath) {
    $resolvedImage = Resolve-Path -LiteralPath $ImagePath -ErrorAction Stop
    $imageResolvedPath = $resolvedImage.Path
    $stream = [System.IO.File]::OpenRead($resolvedImage.Path)
    try {
        $metadataBytes = Read-WadBytes $stream $StoneMetadataOffset $StoneMetadataSize
        $assetBytes = Read-WadBytes $stream $StoneAssetOffset $StoneAssetSize
        $assetSubfiles = @(Parse-ArchiveHeader $assetBytes $StoneAssetSize)
    }
    finally {
        $stream.Dispose()
    }
}

$records = @()
foreach ($moby in @($catalog.mobys)) {
    $pointer = Convert-HexToUInt32 ([string]$moby.specialDataPointer)
    $validPointer = Test-PsxPointerInMainRam $pointer
    $ramOffset = Convert-PsxAddressToRamOffset $pointer $ram.Length
    $bytes = @()
    if ($ramOffset -ge 0) {
        $bytes = [byte[]](Copy-ByteRange $ram $ramOffset $BytesToRead)
    }

    $first16 = @()
    $first32 = @()
    if ($bytes.Count -ge 16) {
        $first16 = [byte[]](Copy-ByteRange $bytes 0 16)
    }
    if ($bytes.Count -ge 32) {
        $first32 = [byte[]](Copy-ByteRange $bytes 0 32)
    }

    $asset16Offsets = @()
    $asset32Offsets = @()
    $metadata16Offsets = @()
    if ($assetBytes.Count -gt 0 -and $first16.Count -eq 16) {
        $asset16Offsets = @(Find-PatternOffsets $assetBytes $first16 16)
        $metadata16Offsets = @(Find-PatternOffsets $metadataBytes $first16 16)
    }
    if ($assetBytes.Count -gt 0 -and $first32.Count -eq 32) {
        $asset32Offsets = @(Find-PatternOffsets $assetBytes $first32 16)
    }

    $records += [pscustomobject][ordered]@{
        index = [int]$moby.index
        typeHex = [string]$moby.typeHex
        displayTargetLabel = [string]$moby.displayTargetLabel
        candidateKind = [string]$moby.candidateKind
        confidence = [string]$moby.confidence
        mapX = [double]$moby.x
        mapY = [double]$moby.y
        height = [double]$moby.z
        specialDataPointer = ("0x{0:X8}" -f $pointer)
        validMainRamPointer = $validPointer
        ramOffset = $(if ($ramOffset -ge 0) { "0x{0:X}" -f $ramOffset } else { "" })
        byteCount = @($bytes).Count
        hash = Get-RecordHash ([byte[]]$bytes)
        bytesHex = Convert-BytesToHex ([byte[]]$bytes) $BytesToRead
        leadInt16Fields = @(Get-LeadInt16Fields ([byte[]]$bytes))
        pointerFields = @(Get-UInt32PointerFields ([byte[]]$bytes) $ram)
        sourceMatches = @(
            (New-MatchResult "asset-first16" $asset16Offsets $StoneAssetOffset),
            (New-MatchResult "asset-first32" $asset32Offsets $StoneAssetOffset),
            (New-MatchResult "metadata-first16" $metadata16Offsets $StoneMetadataOffset)
        )
        assetSubfileIndexes = @($asset16Offsets | ForEach-Object { Get-SubfileIndex $assetSubfiles $_ } | Sort-Object -Unique)
    }
}

$validRecords = @($records | Where-Object { $_.validMainRamPointer })
$pointerGroups = @($validRecords |
    Group-Object specialDataPointer |
    Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, Name |
    ForEach-Object {
        [ordered]@{
            specialDataPointer = $_.Name
            count = $_.Count
            mobys = @($_.Group | ForEach-Object { "L$($_.index) $($_.typeHex)" })
            labels = @($_.Group | ForEach-Object { [string]$_.displayTargetLabel } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
        }
    })

$hashGroups = @($validRecords |
    Group-Object hash |
    Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, Name |
    ForEach-Object {
        [ordered]@{
            hash = $_.Name
            count = $_.Count
            mobys = @($_.Group | ForEach-Object { "L$($_.index) $($_.typeHex)" })
            specialDataPointers = @($_.Group | ForEach-Object { $_.specialDataPointer } | Sort-Object -Unique)
        }
    })

$recordsWithSourceMatches = @($validRecords | Where-Object {
    @($_.sourceMatches | Where-Object { [int]$_.count -gt 0 }).Count -gt 0
})
$recordsWithPointerChains = @($validRecords | Where-Object { @($_.pointerFields).Count -gt 0 })

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Stone Hill runtime moby special-data pointer inventory. These records help split shared moby classes and identify extra object data that may need WAD mapping."
    ramPath = $resolvedRam.Path
    catalogPath = $resolvedCatalog.Path
    imagePath = $imageResolvedPath
    byteWindow = $BytesToRead
    summary = [ordered]@{
        catalogMobys = @($records).Count
        validMainRamPointers = @($validRecords).Count
        uniqueValidPointers = @($pointerGroups).Count
        sharedPointerGroups = @($pointerGroups | Where-Object { [int]$_.count -gt 1 }).Count
        uniqueSpecialDataHashes = @($hashGroups).Count
        recordsWithSourceMatches = @($recordsWithSourceMatches).Count
        recordsWithPointerChains = @($recordsWithPointerChains).Count
    }
    notes = @(
        "A valid pointer here does not prove identity by itself; use focused before/after RAM pairs to promote names.",
        "Exact source matches are searched against the first 16/32 bytes only. A zero match can still be useful because runtime special data may contain RAM pointers or transformed values.",
        "Pointer fields inside special-data blocks can reveal chains or shared class descriptors. The analyzer reads 128 bytes by default so links just past the old 64-byte cutoff are visible."
    )
    pointerGroups = $pointerGroups
    hashGroups = $hashGroups
    records = $records
}

$resolvedOutJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$resolvedOutMd = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Stone Hill Moby Special Data")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($result.generatedAt)")
[void]$lines.Add("")
[void]$lines.Add("This report inventories valid runtime special-data pointers from the Stone Hill moby catalog. It is a research aid for assigning concrete item/enemy/dragon meanings to moby markers.")
[void]$lines.Add("")
[void]$lines.Add("## Summary")
[void]$lines.Add("- Catalog mobys: $($result.summary.catalogMobys)")
[void]$lines.Add("- Valid main-RAM special-data pointers: $($result.summary.validMainRamPointers)")
[void]$lines.Add("- Unique valid pointers: $($result.summary.uniqueValidPointers)")
[void]$lines.Add("- Shared pointer groups: $($result.summary.sharedPointerGroups)")
[void]$lines.Add("- Records with exact first-byte WAD matches: $($result.summary.recordsWithSourceMatches)")
[void]$lines.Add("- Records with linked pointer chains: $($result.summary.recordsWithPointerChains)")
[void]$lines.Add("")
[void]$lines.Add("## Shared Pointer Groups")
[void]$lines.Add("")
[void]$lines.Add("| Pointer | Mobys | Labels |")
[void]$lines.Add("| --- | --- | --- |")
foreach ($group in @($pointerGroups | Where-Object { [int]$_.count -gt 1 })) {
    [void]$lines.Add("| $($group.specialDataPointer) | $(@($group.mobys) -join ', ') | $(@($group.labels) -join ', ') |")
}
if (@($pointerGroups | Where-Object { [int]$_.count -gt 1 }).Count -eq 0) {
    [void]$lines.Add("| none | none | none |")
}
[void]$lines.Add("")
[void]$lines.Add("## Valid Special-Data Records")
[void]$lines.Add("")
[void]$lines.Add("| Moby | Pointer | Lead int16 fields | Pointer fields | WAD first-byte matches |")
[void]$lines.Add("| --- | --- | --- | --- | --- |")
foreach ($record in @($validRecords | Sort-Object index)) {
    $moby = "L$($record.index) $($record.typeHex)"
    $leadFields = @($record.leadInt16Fields) -join "; "
    if ([string]::IsNullOrWhiteSpace($leadFields)) { $leadFields = "none" }
    $pointerFields = @($record.pointerFields | ForEach-Object { "$($_.offset)=$($_.pointer)" }) -join "; "
    if ([string]::IsNullOrWhiteSpace($pointerFields)) { $pointerFields = "none" }
    $matchParts = @()
    foreach ($match in @($record.sourceMatches)) {
        if ([int]$match.count -gt 0) {
            $offsetText = @($match.wadRelativeOffsets) -join ","
            $matchParts += "$($match.kind):$($match.count)@$offsetText"
        }
    }
    $matchText = $matchParts -join "; "
    if ([string]::IsNullOrWhiteSpace($matchText)) { $matchText = "none" }
    [void]$lines.Add("| $moby | $($record.specialDataPointer) | $leadFields | $pointerFields | $matchText |")
}
[void]$lines.Add("")
[void]$lines.Add("## Linked Pointer Chains")
[void]$lines.Add("")
[void]$lines.Add("| Moby | Source pointer | Linked targets | Target preview |")
[void]$lines.Add("| --- | --- | --- | --- |")
foreach ($record in @($recordsWithPointerChains | Sort-Object index)) {
    $moby = "L$($record.index) $($record.typeHex)"
    $targets = @($record.pointerFields | ForEach-Object { "$($_.offset)=$($_.pointer)" }) -join "; "
    if ([string]::IsNullOrWhiteSpace($targets)) { $targets = "none" }
    $previews = @($record.pointerFields | Select-Object -First 3 | ForEach-Object {
        $lead = @($_.targetLeadInt16Fields | Select-Object -First 4) -join ","
        if ([string]::IsNullOrWhiteSpace($lead)) { $lead = "no small int16s" }
        "$($_.targetRamOffset): $lead"
    }) -join "; "
    if ([string]::IsNullOrWhiteSpace($previews)) { $previews = "none" }
    [void]$lines.Add("| $moby | $($record.specialDataPointer) | $targets | $previews |")
}
if (@($recordsWithPointerChains).Count -eq 0) {
    [void]$lines.Add("| none | none | none | none |")
}
[void]$lines.Add("")
[void]$lines.Add("## Current Interpretation")
[void]$lines.Add("- L21/L32 share one treasure-class special-data block, and L120/L131 share another; this supports the idea that special data is class/group metadata, not a one-record placement identity.")
[void]$lines.Add("- L109, L120, and L131 all have linked pointer chains beyond the first 64 bytes. These are the best current leads for chest collision, contents, or follow-up behavior records after a placement source lead is proven.")
[void]$lines.Add("- L54 and L65 are the only valid dragon/NPC-class special-data pointers in the current catalog. L65's block points at L54's block, so they should be treated as a linked dragon/NPC chain until dragon-free RAM pairs split the names.")
[void]$lines.Add("- Most valid special-data blocks exact-match at least their first 16 bytes in the Stone Hill asset WAD, which gives extra class/behavior source leads. L65's block is the current exception, possibly because its linked pointer field is runtime-relocated or because it is generated/overlaid.")
[void]$lines.Add("- These special-data matches are not placement encoders by themselves, so placement source mapping should continue to use scaled-int16 source-window leads plus disposable patch tests.")
[void]$lines.Add("")
[void]$lines.Add("## Next Proof Step")
[void]$lines.Add("- Capture isolated Astor, Lindar, Gildas, and Gavin free-dragon RAM pairs. Compare changes in the main moby record and the pointed special-data block before promoting L54/L65 to named dragons.")
[void]$lines.Add("- Patch-test L109 and L87 first for source-record control; special-data evidence helps classify shared behavior blocks but does not replace source-window validation.")

$lines | Set-Content -LiteralPath $resolvedOutMd -Encoding UTF8
Write-Output "Wrote Stone Hill moby special-data report to $resolvedOutJson and $resolvedOutMd"
