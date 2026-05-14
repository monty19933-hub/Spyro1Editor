param(
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$SourceLinksPath = ".\stonehill-terrain-source-links.json",
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$OutPath = ".\Spyro the Dragon (USA)-terrainpatchtest.bin",
    [string]$CuePath = "",
    [string]$PlanPath = "",
    [string]$MarkdownPath = "",
    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [ValidateSet("localX", "localY", "localZ")]
    [string]$SourceAxis = "localY",
    [double]$SourceUnitsPerZ = 1.0,
    [double]$MaxLinkDistance = 32.0,
    [int]$MaxSourceAliasesPerRuntime = 4,
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

function Read-JsonIfPresent([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
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
    return @($entries)
}

function Get-ModelSubfileLocation([string]$ResolvedImagePath) {
    $stream = [System.IO.File]::OpenRead($ResolvedImagePath)
    try {
        $wadHeader = Read-WadBytes $stream $WadLba 0 4096
        $wadEntries = @(Parse-ArchiveHeader $wadHeader 200000000)
        $assetEntry = @($wadEntries | Where-Object { $_.index -eq $AssetWadIndex } | Select-Object -First 1)
        if ($null -eq $assetEntry) { throw "Could not find WAD entry $AssetWadIndex." }

        $assetHeader = Read-WadBytes $stream $WadLba $assetEntry.offset 4096
        $subfiles = @(Parse-ArchiveHeader $assetHeader $assetEntry.size)
        $modelSubfile = @($subfiles | Where-Object { $_.index -eq $ModelSubfileIndex } | Select-Object -First 1)
        if ($null -eq $modelSubfile) { throw "Could not find model subfile $ModelSubfileIndex." }

        return [ordered]@{
            assetWadIndex = $AssetWadIndex
            assetWadOffset = [int64]$assetEntry.offset
            assetSize = [int64]$assetEntry.size
            modelSubfileIndex = $ModelSubfileIndex
            modelSubfileOffset = [int64]$modelSubfile.offset
            modelSubfileSize = [int64]$modelSubfile.size
            absoluteModelWadOffset = ([int64]$assetEntry.offset + [int64]$modelSubfile.offset)
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Convert-WadOffsetToImageOffset([int64]$WadOffset) {
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    $sectorOffset = [int]($WadOffset % 2048)
    return ($sector * 2352L) + 24L + [int64]$sectorOffset
}

function Get-BytesHex([byte[]]$Bytes) {
    return (($Bytes | ForEach-Object { $_.ToString("X2") }) -join "")
}

function Convert-DiscVertexBytesToRaw([byte[]]$Bytes) {
    if ($Bytes.Length -ne 4) { throw "Packed terrain vertex must be exactly 4 bytes." }
    return [uint32]((([uint32]$Bytes[0]) -shl 24) -bor (([uint32]$Bytes[1]) -shl 16) -bor (([uint32]$Bytes[2]) -shl 8) -bor [uint32]$Bytes[3])
}

function Convert-PackedRawToDiscBytes([uint32]$Raw) {
    $bytes = New-Object byte[] 4
    $bytes[0] = [byte](($Raw -shr 24) -band 0xFF)
    $bytes[1] = [byte](($Raw -shr 16) -band 0xFF)
    $bytes[2] = [byte](($Raw -shr 8) -band 0xFF)
    $bytes[3] = [byte]($Raw -band 0xFF)
    return $bytes
}

function Get-PackedField([uint32]$Raw, [string]$Axis) {
    switch ($Axis) {
        "localX" { return [int]($Raw -band 0x7FF) }
        "localY" { return [int](($Raw -shr 11) -band 0x7FF) }
        default { return [int](($Raw -shr 22) -band 0x3FF) }
    }
}

function Set-PackedField([uint32]$Raw, [string]$Axis, [int]$Value) {
    switch ($Axis) {
        "localX" {
            if ($Value -lt 0 -or $Value -gt 2047) { throw "localX target $Value is outside 11-bit range." }
            return [uint32]((([uint64]$Raw) -band [uint64]4294965248) -bor [uint64]$Value)
        }
        "localY" {
            if ($Value -lt 0 -or $Value -gt 2047) { throw "localY target $Value is outside 11-bit range." }
            return [uint32]((([uint64]$Raw) -band [uint64]4290775039) -bor ([uint64]$Value -shl 11))
        }
        default {
            if ($Value -lt 0 -or $Value -gt 1023) { throw "localZ target $Value is outside 10-bit range." }
            return [uint32]((([uint64]$Raw) -band [uint64]4194303) -bor ([uint64]$Value -shl 22))
        }
    }
}

function Get-RuntimeKey([string]$Detail, [int]$SectorIndex, [int]$VertexIndex) {
    return ("{0}:{1}:{2}" -f $Detail.ToLowerInvariant(), $SectorIndex, $VertexIndex)
}

function Build-SourceLinkMap($LinksRoot, [double]$MaxDistance) {
    $map = @{}
    foreach ($link in @(Get-ArrayField (Get-Field $LinksRoot "links" $null) "links")) {
        $distance = [double](Get-Field $link "distance" 999999)
        if ($distance -gt $MaxDistance) { continue }
        $runtime = Get-Field $link "runtime" $null
        $key = [string](Get-Field $runtime "key" "")
        if ([string]::IsNullOrWhiteSpace($key)) { continue }
        if (-not $map.ContainsKey($key)) { $map[$key] = New-Object System.Collections.ArrayList }
        [void]$map[$key].Add($link)
    }

    foreach ($key in @($map.Keys)) {
        $sorted = @($map[$key].ToArray() | Sort-Object -Property @{ Expression = { [double](Get-Field $_ "distance" 999999) }; Descending = $false } | Select-Object -First $MaxSourceAliasesPerRuntime)
        $map[$key] = $sorted
    }
    return $map
}

function Read-TerrainEdits([string]$Path) {
    $root = Read-JsonIfPresent $Path
    if ($null -eq $root) { return @() }
    return @(Get-ArrayField $root "edits")
}

function New-SourceTerrainPatches($Edits, $SourceLinkMap, $ModelLocation, [string]$ResolvedImagePath) {
    $targets = @{}
    foreach ($edit in @($Edits)) {
        $deltaZ = [double](Get-Field $edit "deltaZ" 0)
        if ([Math]::Abs($deltaZ) -lt 0.0001) { continue }
        $detail = ([string](Get-Field $edit "detail" "hp")).ToLowerInvariant()
        $sectorIndex = [int](Get-Field $edit "sectorIndex" -1)
        $seenVertices = New-Object 'System.Collections.Generic.HashSet[int]'
        foreach ($rawVertexIndex in @(Get-ArrayField $edit "vertexIndexes")) {
            $vertexIndex = [int]$rawVertexIndex
            if (-not $seenVertices.Add($vertexIndex)) { continue }
            $runtimeKey = Get-RuntimeKey $detail $sectorIndex $vertexIndex
            if (-not $SourceLinkMap.ContainsKey($runtimeKey)) { continue }
            foreach ($link in @($SourceLinkMap[$runtimeKey])) {
                $source = Get-Field $link "source" $null
                $modelSubfileRel = Convert-HexTextToInt64 ([string](Get-Field $source "modelSubfileOffset" ""))
                if ($modelSubfileRel -lt 0) { continue }
                $targetKey = "0x{0:X}" -f $modelSubfileRel
                if (-not $targets.ContainsKey($targetKey)) {
                    $targets[$targetKey] = [ordered]@{
                        modelSubfileOffset = $modelSubfileRel
                        source = $source
                        deltas = New-Object System.Collections.ArrayList
                        runtimeHandles = New-Object System.Collections.ArrayList
                    }
                }
                [void]$targets[$targetKey].deltas.Add($deltaZ)
                [void]$targets[$targetKey].runtimeHandles.Add([ordered]@{
                    runtimeKey = $runtimeKey
                    distance = [double](Get-Field $link "distance" 999999)
                    sectorIndex = $sectorIndex
                    vertexIndex = $vertexIndex
                    faceIndex = [int](Get-Field $edit "faceIndex" -1)
                    faceOffset = [string](Get-Field $edit "faceOffset" "")
                    editDeltaZ = $deltaZ
                })
            }
        }
    }

    $stream = [System.IO.File]::OpenRead($ResolvedImagePath)
    try {
        $patches = New-Object System.Collections.ArrayList
        foreach ($target in $targets.Values) {
            $sum = 0.0
            foreach ($d in @($target.deltas)) { $sum += [double]$d }
            $avgDeltaZ = if ($target.deltas.Count -gt 0) { $sum / [double]$target.deltas.Count } else { 0.0 }
            $sourceDelta = [int][Math]::Round($avgDeltaZ * $SourceUnitsPerZ)
            if ($sourceDelta -eq 0) { continue }

            $sourceWadOffset = [int64]$ModelLocation.absoluteModelWadOffset + [int64]$target.modelSubfileOffset
            $imageOffset = Convert-WadOffsetToImageOffset $sourceWadOffset
            if ($imageOffset -lt 0 -or ($imageOffset + 4) -gt $stream.Length) {
                throw ("Computed image offset 0x{0:X} is outside source image." -f $imageOffset)
            }
            $stream.Position = $imageOffset
            $oldBytes = New-Object byte[] 4
            [void]$stream.Read($oldBytes, 0, 4)
            $oldRaw = Convert-DiscVertexBytesToRaw $oldBytes
            $oldField = Get-PackedField $oldRaw $SourceAxis
            $newField = $oldField + $sourceDelta
            $newRaw = Set-PackedField $oldRaw $SourceAxis $newField
            $newBytes = Convert-PackedRawToDiscBytes $newRaw

            [void]$patches.Add([pscustomobject][ordered]@{
                kind = "stone-hill-terrain-packed-vertex-edit"
                sourceAxis = $SourceAxis
                sourceUnitsPerZ = $SourceUnitsPerZ
                averagedDeltaZ = [Math]::Round($avgDeltaZ, 4)
                sourceDelta = $sourceDelta
                originalField = $oldField
                targetField = $newField
                oldRaw = ("0x{0:X8}" -f $oldRaw)
                targetRaw = ("0x{0:X8}" -f $newRaw)
                oldBytesHex = Get-BytesHex $oldBytes
                bytesHex = Get-BytesHex $newBytes
                sourcePart = [int](Get-Field $target.source "part" -1)
                sourceVertexIndex = [int](Get-Field $target.source "vertexIndex" -1)
                sourceKind = [string](Get-Field $target.source "kind" "")
                sourceOffset = [string](Get-Field $target.source "sourceOffset" "")
                modelSubfileOffset = ("0x{0:X}" -f [int64]$target.modelSubfileOffset)
                sourceWadOffset = ("0x{0:X}" -f $sourceWadOffset)
                imageOffset = ("0x{0:X}" -f $imageOffset)
                imageOffsetInt = $imageOffset
                runtimeHandles = @($target.runtimeHandles.ToArray())
                runtimeHandleCount = [int]$target.runtimeHandles.Count
                experimental = $true
            })
        }
        return @($patches.ToArray() | Sort-Object -Property imageOffsetInt)
    }
    finally {
        $stream.Dispose()
    }
}

function Write-Markdown($Plan, [string]$Path) {
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add("# Stone Hill Terrain Patch Plan")
    [void]$lines.Add("")
    [void]$lines.Add("Generated: $($Plan.generatedAt)")
    [void]$lines.Add("")
    [void]$lines.Add("Status: ``$($Plan.status)``")
    [void]$lines.Add("")
    [void]$lines.Add("- Terrain edits: $($Plan.terrainEditCount)")
    [void]$lines.Add("- Runtime vertices with source links: $($Plan.linkedRuntimeVertexCount)")
    [void]$lines.Add("- Packed source vertex patches: $($Plan.patchCount)")
    [void]$lines.Add("- Source axis: ``$($Plan.experiment.sourceAxis)``, source units per Z: $($Plan.experiment.sourceUnitsPerZ), max link distance: $($Plan.experiment.maxLinkDistance)")
    [void]$lines.Add("")
    [void]$lines.Add("## Patch Preview")
    [void]$lines.Add("")
    [void]$lines.Add("| # | Source | Axis | Field | Delta | Bytes | Runtime handles |")
    [void]$lines.Add("|---:|---|---|---|---:|---|---:|")
    $i = 1
    foreach ($patch in @($Plan.binaryPatches | Select-Object -First 40)) {
        [void]$lines.Add(("| {0} | part {1} v{2} {3} | {4} | {5}->{6} | {7} | {8}->{9} | {10} |" -f $i, $patch.sourcePart, $patch.sourceVertexIndex, $patch.modelSubfileOffset, $patch.sourceAxis, $patch.originalField, $patch.targetField, $patch.sourceDelta, $patch.oldBytesHex, $patch.bytesHex, $patch.runtimeHandleCount))
        $i++
    }
    if ($Plan.patchCount -gt 40) {
        [void]$lines.Add("")
        [void]$lines.Add("Only the first 40 patches are shown here; see the JSON patch plan for the full list.")
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Validation")
    [void]$lines.Add("")
    [void]$lines.Add("This is still experimental. Use plan-only first, then test a tiny terrain edit in DuckStation before trusting this as a permanent terrain exporter.")
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedTerrainEditsPath = Resolve-WorkspacePath $TerrainEditsPath
$resolvedSourceLinksPath = Resolve-WorkspacePath $SourceLinksPath
$resolvedImagePath = Resolve-WorkspacePath $ImagePath
$resolvedOutPath = Resolve-WorkspacePath $OutPath
if ([string]::IsNullOrWhiteSpace($CuePath)) { $CuePath = [System.IO.Path]::ChangeExtension($resolvedOutPath, ".cue") }
$resolvedCuePath = Resolve-WorkspacePath $CuePath
if ([string]::IsNullOrWhiteSpace($PlanPath)) { $PlanPath = "$resolvedOutPath.terrainpatchplan.json" }
$resolvedPlanPath = Resolve-WorkspacePath $PlanPath
if ([string]::IsNullOrWhiteSpace($MarkdownPath)) { $MarkdownPath = [System.IO.Path]::ChangeExtension($resolvedPlanPath, ".md") }
$resolvedMarkdownPath = Resolve-WorkspacePath $MarkdownPath

if (-not (Test-Path -LiteralPath $resolvedSourceLinksPath)) { throw "Missing source links: $resolvedSourceLinksPath" }
if (-not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing source image: $resolvedImagePath" }

$sourceLinksRoot = Get-Content -LiteralPath $resolvedSourceLinksPath -Raw | ConvertFrom-Json
$edits = @(Read-TerrainEdits $resolvedTerrainEditsPath)
$modelLocation = Get-ModelSubfileLocation $resolvedImagePath
$sourceLinkMap = Build-SourceLinkMap $sourceLinksRoot $MaxLinkDistance
$patches = @(New-SourceTerrainPatches $edits $sourceLinkMap ([pscustomobject]$modelLocation) $resolvedImagePath)

$linkedRuntimeKeys = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($patch in $patches) {
    foreach ($handle in @(Get-ArrayField $patch "runtimeHandles")) {
        [void]$linkedRuntimeKeys.Add([string](Get-Field $handle "runtimeKey" ""))
    }
}

$status = if ($edits.Count -eq 0) {
    "no-terrain-edits"
}
elseif ($patches.Count -eq 0) {
    "no-linked-source-vertices"
}
else {
    "experimental-plan-ready"
}

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    status = $status
    imagePath = (Resolve-Path -LiteralPath $resolvedImagePath).Path
    outPath = $resolvedOutPath
    cuePath = $resolvedCuePath
    terrainEditsPath = if (Test-Path -LiteralPath $resolvedTerrainEditsPath) { (Resolve-Path -LiteralPath $resolvedTerrainEditsPath).Path } else { $resolvedTerrainEditsPath }
    sourceLinksPath = (Resolve-Path -LiteralPath $resolvedSourceLinksPath).Path
    source = [ordered]@{
        wadLba = $WadLba
        assetWadIndex = $AssetWadIndex
        assetWadOffset = ("0x{0:X}" -f [int64]$modelLocation.assetWadOffset)
        modelSubfileIndex = $ModelSubfileIndex
        modelSubfileOffset = ("0x{0:X}" -f [int64]$modelLocation.modelSubfileOffset)
        absoluteModelWadOffset = ("0x{0:X}" -f [int64]$modelLocation.absoluteModelWadOffset)
        packedVertexFormat = "big-endian 11/11/10-bit localX/localY/localZ fields"
    }
    experiment = [ordered]@{
        sourceAxis = $SourceAxis
        sourceUnitsPerZ = $SourceUnitsPerZ
        maxLinkDistance = $MaxLinkDistance
        maxSourceAliasesPerRuntime = $MaxSourceAliasesPerRuntime
        note = "Runtime-to-WAD terrain source links are nearest-neighbor candidates. This exporter is intentionally marked experimental until a fresh-load BIN test proves the axis and scale."
    }
    terrainEditCount = $edits.Count
    runtimeSourceLinkKeyCount = $sourceLinkMap.Keys.Count
    linkedRuntimeVertexCount = $linkedRuntimeKeys.Count
    patchCount = $patches.Count
    binaryPatches = $patches
}

$plan | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedPlanPath -Encoding UTF8
Write-Markdown ([pscustomobject]$plan) $resolvedMarkdownPath

$shouldWriteBin = (-not $PlanOnly) -and [bool]$AllowExperimentalWrite
if ($shouldWriteBin) {
    Copy-Item -LiteralPath $resolvedImagePath -Destination $resolvedOutPath -Force
    $stream = [System.IO.File]::Open($resolvedOutPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::Read)
    try {
        foreach ($patch in $patches) {
            $byteCount = [int]($patch.bytesHex.Length / 2)
            $bytes = New-Object byte[] $byteCount
            for ($i = 0; $i -lt $byteCount; $i++) {
                $bytes[$i] = [Convert]::ToByte($patch.bytesHex.Substring($i * 2, 2), 16)
            }
            $stream.Position = [int64]$patch.imageOffsetInt
            $stream.Write($bytes, 0, $bytes.Length)
        }
    }
    finally {
        $stream.Dispose()
    }
    $cueFileName = [System.IO.Path]::GetFileName($resolvedOutPath)
    @(
        "FILE `"$cueFileName`" BINARY",
        "  TRACK 01 MODE2/2352",
        "    INDEX 01 00:00:00"
    ) | Set-Content -LiteralPath $resolvedCuePath -Encoding ASCII
}

Write-Host "Wrote terrain patch plan to $resolvedPlanPath"
Write-Host "Wrote terrain patch summary to $resolvedMarkdownPath"
if ($PlanOnly) {
    Write-Host "PlanOnly set; no BIN/CUE was written."
}
elseif (-not $AllowExperimentalWrite) {
    Write-Host "No BIN/CUE was written. Pass -AllowExperimentalWrite to create the experimental terrain patch BIN."
}
else {
    Write-Host "Wrote experimental terrain BIN to $resolvedOutPath"
    Write-Host "Wrote CUE to $resolvedCuePath"
}
Write-Host ("Terrain edits: {0}; source vertex patches: {1}; status: {2}" -f $edits.Count, $patches.Count, $status)
