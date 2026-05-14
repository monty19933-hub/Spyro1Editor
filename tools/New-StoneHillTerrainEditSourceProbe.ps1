param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$OverlayPath = ".\stonehill-runtime-scene-editor-overlay.json",
    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [string]$OutJsonPath = ".\stonehill-terrain-edit-source-probe.json",
    [string]$OutMarkdownPath = ".\stonehill-terrain-edit-source-probe.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
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

function Read-WadBytes([System.IO.FileStream]$Stream, [int]$WadLba, [int64]$Offset, [int]$Length) {
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

function IndexOf-Bytes([byte[]]$Haystack, [byte[]]$Needle) {
    if ($null -eq $Haystack -or $null -eq $Needle -or $Needle.Length -eq 0 -or $Needle.Length -gt $Haystack.Length) { return -1 }
    for ($i = 0; $i -le ($Haystack.Length - $Needle.Length); $i++) {
        $ok = $true
        for ($j = 0; $j -lt $Needle.Length; $j++) {
            if ($Haystack[$i + $j] -ne $Needle[$j]) {
                $ok = $false
                break
            }
        }
        if ($ok) { return $i }
    }
    return -1
}

function Copy-Bytes([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $copy = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Offset, $copy, 0, $Length)
    return $copy
}

function Get-ArrayField($Object, [string]$Name) {
    if ($null -eq $Object) { return @() }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop -or $null -eq $prop.Value) { return @() }
    if ($prop.Value -is [System.Array]) { return @($prop.Value) }
    return @($prop.Value)
}

function Get-ModelSubfileBytes([string]$ResolvedImagePath, [int]$WadLba, [int]$AssetWadIndex, [int]$ModelSubfileIndex) {
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

        $assetBytes = Read-WadBytes $stream $WadLba $assetEntry.offset ([int]$assetEntry.size)
        $modelBytes = New-Object byte[] ([int]$modelSubfile.size)
        [Array]::Copy($assetBytes, [int]$modelSubfile.offset, $modelBytes, 0, $modelBytes.Length)
        return [pscustomobject]@{
            assetEntry = $assetEntry
            subfiles = $subfiles
            modelSubfile = $modelSubfile
            assetBytes = $assetBytes
            modelBytes = $modelBytes
        }
    }
    finally {
        $stream.Dispose()
    }
}

function New-SampleHit($Name, [byte[]]$Ram, [int]$RamOffset, [int]$Length, [byte[]]$AssetBytes, [byte[]]$ModelBytes, [byte[]]$GroundBytes, [int64]$AssetWadOffset, [int64]$ModelSubfileOffset, [int]$GroundSubfileOffset) {
    $needle = Copy-Bytes $Ram $RamOffset $Length
    $assetHit = IndexOf-Bytes $AssetBytes $needle
    $modelHit = IndexOf-Bytes $ModelBytes $needle
    $groundHit = IndexOf-Bytes $GroundBytes $needle
    return [ordered]@{
        name = $Name
        ramOffset = ("0x{0:X}" -f $RamOffset)
        length = $Length
        exactAssetHit = $(if ($assetHit -ge 0) { "0x{0:X}" -f $assetHit } else { "" })
        exactAssetWadOffset = $(if ($assetHit -ge 0) { "0x{0:X}" -f ([int64]$AssetWadOffset + [int64]$assetHit) } else { "" })
        exactModelHit = $(if ($modelHit -ge 0) { "0x{0:X}" -f $modelHit } else { "" })
        exactModelWadOffset = $(if ($modelHit -ge 0) { "0x{0:X}" -f ([int64]$AssetWadOffset + [int64]$ModelSubfileOffset + [int64]$modelHit) } else { "" })
        exactGroundHit = $(if ($groundHit -ge 0) { "0x{0:X}" -f $groundHit } else { "" })
        exactGroundModelOffset = $(if ($groundHit -ge 0) { "0x{0:X}" -f ([int64]$GroundSubfileOffset + [int64]$groundHit) } else { "" })
    }
}

function Get-GroundPartSummary([byte[]]$Ground) {
    $partCount = [int](Get-UInt32LE $Ground 0)
    $pointers = @()
    for ($i = 0; $i -lt $partCount; $i++) {
        $ptr = [int](Get-UInt32LE $Ground (4 + ($i * 4)))
        if ($ptr -gt 0 -and $ptr -lt $Ground.Length) { $pointers += $ptr }
    }
    $pointers = @($pointers | Sort-Object -Unique)
    $parts = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $pointers.Count; $i++) {
        $start = [int]$pointers[$i]
        $end = if ($i -lt ($pointers.Count - 1)) { [int]$pointers[$i + 1] } else { $Ground.Length }
        if ($end -le $start) { continue }
        [void]$parts.Add([ordered]@{
            index = $i
            offset = ("0x{0:X}" -f $start)
            size = ($end - $start)
            firstWords = @(
                ("0x{0:X8}" -f (Get-UInt32LE $Ground $start)),
                ("0x{0:X8}" -f (Get-UInt32LE $Ground ($start + 4))),
                ("0x{0:X8}" -f (Get-UInt32LE $Ground ($start + 8))),
                ("0x{0:X8}" -f (Get-UInt32LE $Ground ($start + 12)))
            )
        })
    }
    return [ordered]@{
        partCount = $partCount
        pointerCount = $pointers.Count
        firstParts = @($parts.ToArray() | Select-Object -First 16)
    }
}

function Write-Markdown($Report, [string]$Path) {
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add("# Stone Hill Terrain Edit Source Probe")
    [void]$lines.Add("")
    [void]$lines.Add("Generated: $($Report.generatedAt)")
    [void]$lines.Add("")
    [void]$lines.Add("## Result")
    [void]$lines.Add("")
    [void]$lines.Add("- Runtime terrain face/vertex records are editable in RAM by sector and vertex word.")
    [void]$lines.Add("- Exact runtime scene bytes were not found in the Stone Hill model subfile in this probe, so permanent BIN terrain editing should target the packed WAD ground model source, not the runtime scene bytes.")
    [void]$lines.Add("- Current bridge: editor terrain edits -> stonehill-terrain-edits.json -> tools/Move-DuckStationTerrainFace.ps1 live RAM vertex writes.")
    [void]$lines.Add("")
    [void]$lines.Add("## WAD Model")
    [void]$lines.Add("")
    [void]$lines.Add("- Asset WAD entry: $($Report.wad.assetWadIndex) at $($Report.wad.assetOffset)")
    [void]$lines.Add("- Model subfile: $($Report.wad.modelSubfileIndex) at $($Report.wad.modelSubfileOffset), size $($Report.wad.modelSubfileSize)")
    [void]$lines.Add("- Texture list size: $($Report.wad.textureListSize)")
    [void]$lines.Add("- Ground model offset/size: $($Report.wad.groundModelOffset) / $($Report.wad.groundModelSize)")
    [void]$lines.Add("- Ground parts: $($Report.ground.partCount) documented pointers, $($Report.ground.pointerCount) valid sorted pointers")
    [void]$lines.Add("")
    [void]$lines.Add("## Exact Byte Probes")
    [void]$lines.Add("")
    [void]$lines.Add("| Sample | RAM | Bytes | Asset Hit | Model Hit | Ground Hit |")
    [void]$lines.Add("|---|---:|---:|---:|---:|---:|")
    foreach ($sample in $Report.exactByteProbes) {
        $asset = if ([string]::IsNullOrWhiteSpace([string]$sample.exactAssetHit)) { "-" } else { [string]$sample.exactAssetHit }
        $model = if ([string]::IsNullOrWhiteSpace([string]$sample.exactModelHit)) { "-" } else { [string]$sample.exactModelHit }
        $ground = if ([string]::IsNullOrWhiteSpace([string]$sample.exactGroundHit)) { "-" } else { [string]$sample.exactGroundHit }
        [void]$lines.Add("| $($sample.name) | $($sample.ramOffset) | $($sample.length) | $asset | $model | $ground |")
    }
    [void]$lines.Add("")
    [void]$lines.Add("## First Ground Parts")
    [void]$lines.Add("")
    [void]$lines.Add("| Part | Offset | Size | First Words |")
    [void]$lines.Add("|---:|---:|---:|---|")
    foreach ($part in $Report.ground.firstParts) {
        [void]$lines.Add("| $($part.index) | $($part.offset) | $($part.size) | $(@($part.firstWords) -join ' ') |")
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Next Work")
    [void]$lines.Add("")
    [void]$lines.Add("1. Test one live terrain edit in DuckStation to confirm whether the runtime vertex write changes collision as well as visuals.")
    [void]$lines.Add("2. Decode the packed WAD ground model to map runtime vertices back to source part/vertex offsets.")
    [void]$lines.Add("3. Once that source mapping is stable, add a terrain BIN exporter beside the moby loader-table exporter.")
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedImage = (Resolve-Path -LiteralPath (Resolve-WorkspacePath $ImagePath) -ErrorAction Stop).Path
$resolvedRam = (Resolve-Path -LiteralPath (Resolve-WorkspacePath $RamPath) -ErrorAction Stop).Path
$resolvedOverlay = Resolve-WorkspacePath $OverlayPath
$ram = [System.IO.File]::ReadAllBytes($resolvedRam)
$overlay = if (Test-Path -LiteralPath $resolvedOverlay) { Get-Content -LiteralPath $resolvedOverlay -Raw | ConvertFrom-Json } else { $null }
$modelInfo = Get-ModelSubfileBytes $resolvedImage $WadLba $AssetWadIndex $ModelSubfileIndex
$modelBytes = [byte[]]$modelInfo.modelBytes
$assetBytes = [byte[]]$modelInfo.assetBytes
$textureListSize = [int](Get-UInt32LE $modelBytes 0)
$groundJump = [int](Get-UInt32LE $modelBytes $textureListSize)
$groundModelOffset = $textureListSize + 4
$groundModelSize = $groundJump - 4
$groundBytes = New-Object byte[] $groundModelSize
[Array]::Copy($modelBytes, $groundModelOffset, $groundBytes, 0, $groundModelSize)

$sampleSpecs = @(
    @{ name = "scene-header-64"; offset = 0x8C134; length = 64 },
    @{ name = "scene-pointer-table-64"; offset = 0x8C140; length = 64 },
    @{ name = "sector1-header-64"; offset = 0x8C548; length = 64 },
    @{ name = "sector1-first-hp-vertices-32"; offset = 0x8C618; length = 32 },
    @{ name = "sector1-face0-16"; offset = 0x8C8D8; length = 16 },
    @{ name = "sector72-water-face0-16"; offset = 0xA1DC8; length = 16 }
)

$samples = New-Object System.Collections.ArrayList
foreach ($spec in $sampleSpecs) {
    if (($spec.offset + $spec.length) -le $ram.Length) {
        [void]$samples.Add((New-SampleHit $spec.name $ram $spec.offset $spec.length $assetBytes $modelBytes $groundBytes ([int64]$modelInfo.assetEntry.offset) ([int64]$modelInfo.modelSubfile.offset) $groundModelOffset))
    }
}

$candidate = $null
if ($null -ne $overlay) {
    $candidate = @(Get-ArrayField $overlay "candidates" | Select-Object -First 1)
}

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceImage = $resolvedImage
    sourceRam = $resolvedRam
    overlayPath = $resolvedOverlay
    runtimeOverlay = $(if ($null -ne $candidate) {
        [ordered]@{
            runtimeAddress = [string]$candidate.runtimeAddress
            sceneRuntimeAddress = [string]$candidate.sceneRuntimeAddress
            validPolygons = [int]$candidate.validPolygons
            validVertices = [int]$candidate.validVertices
            sectorCount = [int]$candidate.sectorCount
        }
    } else { $null })
    wad = [ordered]@{
        wadLba = $WadLba
        assetWadIndex = $AssetWadIndex
        assetOffset = ("0x{0:X}" -f [int64]$modelInfo.assetEntry.offset)
        assetSize = [int64]$modelInfo.assetEntry.size
        modelSubfileIndex = $ModelSubfileIndex
        modelSubfileOffset = ("0x{0:X}" -f [int64]$modelInfo.modelSubfile.offset)
        modelSubfileSize = [int64]$modelInfo.modelSubfile.size
        textureListSize = $textureListSize
        groundJump = $groundJump
        groundModelOffset = ("0x{0:X}" -f $groundModelOffset)
        groundModelSize = $groundModelSize
    }
    ground = Get-GroundPartSummary $groundBytes
    exactByteProbes = @($samples.ToArray())
    conclusion = "Runtime scene bytes are not a direct on-disc copy in the Stone Hill model subfile. Live RAM terrain editing is now the practical validation path; permanent BIN terrain editing needs runtime-to-WAD packed ground-model vertex mapping."
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutJsonPath))
$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutMarkdownPath))
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8
Write-Markdown $report $resolvedMarkdown
Write-Host "Wrote terrain edit source probe to $resolvedJson"
Write-Host "Wrote markdown summary to $resolvedMarkdown"
