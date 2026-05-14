param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$CatalogPath = ".\stonehill-moby-catalog.json",
    [string]$OutPath = ".\stonehill-moby-source-links.json"
)

Set-StrictMode -Version 2.0

$WadLba = 37
$StoneMetadataOffset = 8333312
$StoneMetadataSize = 57344
$StoneAssetOffset = 8390656
$StoneAssetSize = 3682304

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int32Bytes([int]$Value) {
    return [BitConverter]::GetBytes($Value)
}

function Get-Int16Bytes([int]$Value) {
    if ($Value -lt [int16]::MinValue -or $Value -gt [int16]::MaxValue) { return $null }
    return [BitConverter]::GetBytes([int16]$Value)
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

function Get-AssetSubfileIndex($Subfiles, [int]$AssetRelativeOffset) {
    foreach ($subfile in @($Subfiles)) {
        $start = [int]$subfile.offset
        $end = $start + [int]$subfile.size
        if ($AssetRelativeOffset -ge $start -and $AssetRelativeOffset -lt $end) {
            return [int]$subfile.index
        }
    }
    return -1
}

function Find-ByteOffsetsInWindow([byte[]]$Bytes, [int]$Start, [int]$Length, [byte]$Value) {
    $offsets = @()
    $end = [Math]::Min($Bytes.Length, $Start + $Length)
    for ($i = [Math]::Max(0, $Start); $i -lt $end; $i++) {
        if ($Bytes[$i] -eq $Value) {
            $offsets += "+$($i - $Start)"
        }
    }
    return $offsets
}

function Find-PatternOffsets([byte[]]$Bytes, [byte[]]$Pattern, [int]$MaxMatches = 128) {
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
            if ($matches.Count -ge $MaxMatches) { return $matches }
        }
    }
    return $matches
}

function Join-Bytes($Arrays) {
    $length = 0
    foreach ($array in $Arrays) { $length += $array.Length }
    $result = New-Object byte[] $length
    $offset = 0
    foreach ($array in $Arrays) {
        [Array]::Copy($array, 0, $result, $offset, $array.Length)
        $offset += $array.Length
    }
    return $result
}

function New-MatchResult([string]$Kind, [int[]]$Offsets) {
    return [ordered]@{
        kind = $Kind
        count = @($Offsets).Count
        offsets = @($Offsets | Select-Object -First 16 | ForEach-Object { "0x{0:X}" -f $_ })
    }
}

function Get-ScaledInt16Candidates([int]$Raw) {
    $scaled = [double]$Raw / 16.0
    $values = New-Object System.Collections.Generic.HashSet[int]
    foreach ($value in @([Math]::Floor($scaled), [Math]::Round($scaled), [Math]::Ceiling($scaled))) {
        $intValue = [int]$value
        if ($intValue -ge [int16]::MinValue -and $intValue -le [int16]::MaxValue) {
            [void]$values.Add($intValue)
        }
    }
    return @($values)
}

function Build-Int16OffsetIndex([byte[]]$Bytes) {
    $index = @{}
    for ($i = 0; $i -le ($Bytes.Length - 2); $i++) {
        $value = [int][BitConverter]::ToInt16($Bytes, $i)
        if (-not $index.ContainsKey($value)) {
            $index[$value] = New-Object System.Collections.Generic.List[int]
        }
        [void]$index[$value].Add($i)
    }
    return $index
}

function Find-ScaledAxisWindows($Moby, $Index, $Subfiles, [byte[]]$AssetBytes, [int]$WindowSize = 64) {
    $axes = [ordered]@{
        x = @(Get-ScaledInt16Candidates ([int]$Moby.rawX))
        y = @(Get-ScaledInt16Candidates ([int]$Moby.rawY))
        z = @(Get-ScaledInt16Candidates ([int]$Moby.rawZ))
    }

    $windows = @{}
    foreach ($axis in @("x", "y", "z")) {
        foreach ($value in @($axes[$axis])) {
            if (-not $Index.ContainsKey($value)) { continue }
            foreach ($offset in @($Index[$value] | Select-Object -First 5000)) {
                $key = [int][Math]::Floor($offset / [double]$WindowSize)
                if (-not $windows.ContainsKey($key)) {
                    $windows[$key] = New-Object System.Collections.Generic.List[object]
                }
                [void]$windows[$key].Add([pscustomobject]@{ axis = $axis; value = $value; offset = $offset; inWindow = ($offset - ($key * $WindowSize)) })
            }
        }
    }

    $hits = @()
    foreach ($entry in $windows.GetEnumerator()) {
        $items = @($entry.Value.ToArray())
        $axisCount = @($items | Select-Object -ExpandProperty axis -Unique).Count
        if ($axisCount -lt 2) { continue }
        $offsets = @($items | ForEach-Object { [int]$_.offset })
        $min = ($offsets | Measure-Object -Minimum).Minimum
        $max = ($offsets | Measure-Object -Maximum).Maximum
        $windowStart = [int]$entry.Key * $WindowSize
        $hits += [ordered]@{
            assetRelativeWindow = "0x{0:X}" -f $windowStart
            wadRelativeWindow = "0x{0:X}" -f ($StoneAssetOffset + $windowStart)
            assetSubfileIndex = Get-AssetSubfileIndex $Subfiles $windowStart
            axisCount = $axisCount
            spread = [int]($max - $min)
            values = @($items | Select-Object -First 8 | ForEach-Object { "$($_.axis)=$($_.value)@+$($_.inWindow)" })
            typeByteOffsets = @(Find-ByteOffsetsInWindow $AssetBytes $windowStart $WindowSize ([byte]$Moby.typeId))
            stateByteOffsets = @(Find-ByteOffsetsInWindow $AssetBytes $windowStart $WindowSize ([byte]$Moby.state))
        }
    }
    return @($hits | Sort-Object -Property @{ Expression = { [int]$_.axisCount }; Descending = $true }, @{ Expression = { [int]$_.spread }; Descending = $false } | Select-Object -First 12)
}

function Test-PlaceholderMoby($Moby) {
    return ([int]$Moby.rawX -eq 0 -and [int]$Moby.rawY -eq 4096 -and [int]$Moby.rawZ -eq 0)
}

function Test-PlacementLikeMoby($Moby) {
    if (Test-PlaceholderMoby $Moby) { return $false }
    $mapX = [double]([int]$Moby.rawX) / 16.0
    $mapY = [double]([int]$Moby.rawZ) / 16.0
    return ($mapX -gt 0 -or $mapY -gt 512)
}

$resolvedImage = Resolve-Path -LiteralPath $ImagePath -ErrorAction Stop
$resolvedCatalog = Resolve-Path -LiteralPath $CatalogPath -ErrorAction Stop
$catalog = Get-Content -Raw -LiteralPath $resolvedCatalog.Path | ConvertFrom-Json

$stream = [System.IO.File]::OpenRead($resolvedImage.Path)
try {
    $metadataBytes = Read-WadBytes $stream $StoneMetadataOffset $StoneMetadataSize
    $assetBytes = Read-WadBytes $stream $StoneAssetOffset $StoneAssetSize
}
finally {
    $stream.Dispose()
}

$assetInt16Index = Build-Int16OffsetIndex $assetBytes
$assetSubfiles = @(Parse-ArchiveHeader $assetBytes $StoneAssetSize)
$mobys = @($catalog.mobys)
$records = @()
foreach ($moby in $mobys) {
    $rawX = [int]$moby.rawX
    $rawY = [int]$moby.rawY
    $rawZ = [int]$moby.rawZ
    $xyz = Join-Bytes @((Get-Int32Bytes $rawX), (Get-Int32Bytes $rawY), (Get-Int32Bytes $rawZ))
    $xy = Join-Bytes @((Get-Int32Bytes $rawX), (Get-Int32Bytes $rawY))
    $xz = Join-Bytes @((Get-Int32Bytes $rawX), (Get-Int32Bytes $rawZ))
    $yz = Join-Bytes @((Get-Int32Bytes $rawY), (Get-Int32Bytes $rawZ))

    $assetMatches = @(
        New-MatchResult "asset-int32-xyz" @(Find-PatternOffsets $assetBytes $xyz 64)
        New-MatchResult "asset-int32-xy" @(Find-PatternOffsets $assetBytes $xy 64)
        New-MatchResult "asset-int32-xz" @(Find-PatternOffsets $assetBytes $xz 64)
        New-MatchResult "asset-int32-yz" @(Find-PatternOffsets $assetBytes $yz 64)
    )
    $metadataMatches = @(
        New-MatchResult "metadata-int32-xyz" @(Find-PatternOffsets $metadataBytes $xyz 64)
        New-MatchResult "metadata-int32-xy" @(Find-PatternOffsets $metadataBytes $xy 64)
        New-MatchResult "metadata-int32-xz" @(Find-PatternOffsets $metadataBytes $xz 64)
        New-MatchResult "metadata-int32-yz" @(Find-PatternOffsets $metadataBytes $yz 64)
    )

    $scaledWindows = @()
    if (-not (Test-PlaceholderMoby $moby)) {
        $scaledWindows = @(Find-ScaledAxisWindows $moby $assetInt16Index $assetSubfiles $assetBytes)
    }

    $records += [ordered]@{
        index = [int]$moby.index
        runtimeAddress = [string]$moby.runtimeAddress
        typeHex = [string]$moby.typeHex
        stateHex = [string]$moby.stateHex
        candidateKind = [string]$moby.candidateKind
        confidence = [string]$moby.confidence
        placeholderPosition = (Test-PlaceholderMoby $moby)
        placementLike = (Test-PlacementLikeMoby $moby)
        rawPosition = [ordered]@{ x = $rawX; y = $rawY; z = $rawZ }
        scaled16Candidates = [ordered]@{
            x = @(Get-ScaledInt16Candidates $rawX)
            y = @(Get-ScaledInt16Candidates $rawY)
            z = @(Get-ScaledInt16Candidates $rawZ)
        }
        exactMatches = @($assetMatches + $metadataMatches | Where-Object { $_.count -gt 0 })
        scaledAxisWindows = $scaledWindows
    }
}

$exactLinked = @($records | Where-Object { @($_.exactMatches | Where-Object { $_.kind -like "*xyz" }).Count -gt 0 }).Count
$pairLinked = @($records | Where-Object { @($_.exactMatches).Count -gt 0 }).Count
$scaledWindowLinked = @($records | Where-Object { @($_.scaledAxisWindows).Count -gt 0 }).Count
$nonPlaceholderExactLinked = @($records | Where-Object { -not $_.placeholderPosition -and @($_.exactMatches | Where-Object { $_.kind -like "*xyz" }).Count -gt 0 }).Count
$placementLikeExactLinked = @($records | Where-Object { $_.placementLike -and @($_.exactMatches | Where-Object { $_.kind -like "*xyz" }).Count -gt 0 }).Count
$nonHelperScaledWindowLinked = @($records | Where-Object { @($_.scaledAxisWindows).Count -gt 0 -and [string]$_.candidateKind -notlike "*helper*" -and [string]$_.candidateKind -notlike "*sparse*" }).Count

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    imagePath = $resolvedImage.Path
    catalogPath = $resolvedCatalog.Path
    note = "Compact source-link probe. Exact int32 matches are patch candidates only when unique and non-placeholder; scaled int16 windows are research leads, not verified source records."
    stoneHill = [ordered]@{
        metadataWadIndex = 9
        metadataOffset = $StoneMetadataOffset
        metadataSize = $StoneMetadataSize
        assetWadIndex = 10
        assetOffset = $StoneAssetOffset
        assetSize = $StoneAssetSize
        assetSubfiles = @($assetSubfiles | ForEach-Object { [ordered]@{ index = [int]$_.index; offset = "0x{0:X}" -f [int64]$_.offset; size = [int64]$_.size } })
    }
    summary = [ordered]@{
        mobys = $records.Count
        exactInt32TripleMobys = $exactLinked
        nonPlaceholderExactInt32TripleMobys = $nonPlaceholderExactLinked
        placementLikeExactInt32TripleMobys = $placementLikeExactLinked
        anyExactInt32PairOrTripleMobys = $pairLinked
        scaledInt16WindowMobys = $scaledWindowLinked
        nonHelperScaledInt16WindowMobys = $nonHelperScaledWindowLinked
    }
    records = $records
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote Stone Hill moby source-link report to $resolvedOut"
Write-Host "Mobys: $($records.Count); exact int32 triples: $exactLinked ($nonPlaceholderExactLinked non-placeholder, $placementLikeExactLinked placement-like); any exact int32 pair/triple: $pairLinked; scaled int16 window leads: $scaledWindowLinked ($nonHelperScaledWindowLinked non-helper)"
