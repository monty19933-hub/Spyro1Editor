param(
    [Parameter(Mandatory = $true)]
    [string]$TargetLevelKey,

    [Parameter(Mandatory = $true)]
    [string]$DonorLevelKey,

    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$OutPath = "",
    [string]$CuePath = "",
    [string]$PlanPath = "",
    [int]$SkySubfileIndex = 3,
    [int]$SegmentStart = -1,
    [int]$SegmentLength = 0,
    [switch]$AllowExperimentalSegmentSwap,
    [switch]$AllowExperimentalWholeSubfileSwap,
    [switch]$AllowExperimentalSizeMismatch,
    [switch]$AllowStartupCriticalPatch,
    [switch]$AllowUnverifiedAssetWadMap,
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Normalize-LevelKey([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return (($Value.ToCharArray() | Where-Object { [char]::IsLetterOrDigit($_) } | ForEach-Object { [char]::ToLowerInvariant($_) }) -join "")
}

function Get-JsonProperty($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-SkyboxProbeAssetMap {
    $probePath = Join-Path $WorkspaceRoot "_skybox_probe\analysis-refresh.json"
    $map = @{}
    if (-not (Test-Path -LiteralPath $probePath)) { return $map }

    $probe = Get-Content -LiteralPath $probePath -Raw | ConvertFrom-Json
    $levels = Get-JsonProperty $probe "levels"
    if ($null -eq $levels) { return $map }

    foreach ($level in @($levels)) {
        $levelName = [string](Get-JsonProperty $level "levelName")
        $assetWadIndex = Get-JsonProperty $level "assetWadIndex"
        if ([string]::IsNullOrWhiteSpace($levelName) -or $null -eq $assetWadIndex) { continue }

        foreach ($namePart in @($levelName -split "/")) {
            $key = Normalize-LevelKey $namePart
            if (-not [string]::IsNullOrWhiteSpace($key)) {
                $map[$key] = [int]$assetWadIndex
            }
        }
    }
    return $map
}

function Get-SkyboxLevelCatalog {
    $levels = @(
        @{ key = "stonehill"; scriptKey = "StoneHill"; displayName = "Stone Hill"; assetWadEntry = 10 },
        @{ key = "darkhollow"; scriptKey = "DarkHollow"; displayName = "Dark Hollow"; assetWadEntry = 12 },
        @{ key = "townsquare"; scriptKey = "TownSquare"; displayName = "Town Square"; assetWadEntry = 14 },
        @{ key = "toasty"; scriptKey = "Toasty"; displayName = "Toasty"; assetWadEntry = 16 },
        @{ key = "sunnyflight"; scriptKey = "SunnyFlight"; displayName = "Sunny Flight"; assetWadEntry = 18 },
        @{ key = "peacekeepers"; scriptKey = "PeaceKeepers"; displayName = "Peace Keepers"; assetWadEntry = 20 },
        @{ key = "drycanyon"; scriptKey = "DryCanyon"; displayName = "Dry Canyon"; assetWadEntry = 22 },
        @{ key = "clifftown"; scriptKey = "CliffTown"; displayName = "Cliff Town"; assetWadEntry = 24 },
        @{ key = "icecavern"; scriptKey = "IceCavern"; displayName = "Ice Cavern"; assetWadEntry = 26 },
        @{ key = "doctorshemp"; scriptKey = "DoctorShemp"; displayName = "Doctor Shemp"; assetWadEntry = 28 },
        @{ key = "nightflight"; scriptKey = "NightFlight"; displayName = "Night Flight"; assetWadEntry = 30 },
        @{ key = "magiccrafters"; scriptKey = "MagicCrafters"; displayName = "Magic Crafters"; assetWadEntry = 32 },
        @{ key = "alpineridge"; scriptKey = "AlpineRidge"; displayName = "Alpine Ridge"; assetWadEntry = 34 },
        @{ key = "highcaves"; scriptKey = "HighCaves"; displayName = "High Caves"; assetWadEntry = 36 },
        @{ key = "wizardpeak"; scriptKey = "WizardPeak"; displayName = "Wizard Peak"; assetWadEntry = 38 },
        @{ key = "blowhard"; scriptKey = "Blowhard"; displayName = "Blowhard"; assetWadEntry = 40 },
        @{ key = "crystalflight"; scriptKey = "CrystalFlight"; displayName = "Crystal Flight"; assetWadEntry = 42 },
        @{ key = "beastmakers"; scriptKey = "BeastMakers"; displayName = "Beast Makers"; assetWadEntry = 44 },
        @{ key = "terracevillage"; scriptKey = "TerraceVillage"; displayName = "Terrace Village"; assetWadEntry = 46 },
        @{ key = "mistybog"; scriptKey = "MistyBog"; displayName = "Misty Bog"; assetWadEntry = 48 },
        @{ key = "treetops"; scriptKey = "TreeTops"; displayName = "Tree Tops"; assetWadEntry = 50 },
        @{ key = "metalhead"; scriptKey = "Metalhead"; displayName = "Metalhead"; assetWadEntry = 52 },
        @{ key = "wildflight"; scriptKey = "WildFlight"; displayName = "Wild Flight"; assetWadEntry = 54 },
        @{ key = "dreamweavers"; scriptKey = "DreamWeavers"; displayName = "Dream Weavers"; assetWadEntry = 56 },
        @{ key = "darkpassage"; scriptKey = "DarkPassage"; displayName = "Dark Passage"; assetWadEntry = 58 },
        @{ key = "loftycastle"; scriptKey = "LoftyCastle"; displayName = "Lofty Castle"; assetWadEntry = 60 },
        @{ key = "hauntedtowers"; scriptKey = "HauntedTowers"; displayName = "Haunted Towers"; assetWadEntry = 62 },
        @{ key = "jacques"; scriptKey = "Jacques"; displayName = "Jacques"; assetWadEntry = 64 },
        @{ key = "icyflight"; scriptKey = "IcyFlight"; displayName = "Icy Flight"; assetWadEntry = 66 },
        @{ key = "gnastysworld"; scriptKey = "GnastysWorld"; displayName = "Gnasty's World"; assetWadEntry = 68 },
        @{ key = "gnorccove"; scriptKey = "GnorcCove"; displayName = "Gnorc Cove"; assetWadEntry = 70 },
        @{ key = "twilightharbor"; scriptKey = "TwilightHarbor"; displayName = "Twilight Harbor"; assetWadEntry = 72 },
        @{ key = "gnastygnorc"; scriptKey = "GnastyGnorc"; displayName = "Gnasty Gnorc"; assetWadEntry = 74 },
        @{ key = "gnastysloot"; scriptKey = "GnastysLoot"; displayName = "Gnasty's Loot"; assetWadEntry = 76 }
    )

    $probeMap = Get-SkyboxProbeAssetMap
    foreach ($level in $levels) {
        $levelKey = Normalize-LevelKey ([string]$level.displayName)
        if ($probeMap.ContainsKey($levelKey)) {
            $level.assetWadEntry = [int]$probeMap[$levelKey]
            $level.assetMapSource = "_skybox_probe/analysis-refresh.json"
        }
        else {
            $level.assetMapSource = "unverified-hardcoded"
        }
    }
    return $levels
}

function Get-SkyboxLevel([string]$Key) {
    $wanted = Normalize-LevelKey $Key
    foreach ($level in Get-SkyboxLevelCatalog) {
        $matches = @(
            (Normalize-LevelKey ([string]$level.key)),
            (Normalize-LevelKey ([string]$level.scriptKey)),
            (Normalize-LevelKey ([string]$level.displayName))
        )
        if ($matches -contains $wanted) { return $level }
    }
    throw "No mapped skybox level matches '$Key'. Artisans home is not in this subfile-3 skybox map yet."
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Convert-BytesToHex([byte[]]$Bytes) {
    return -join ($Bytes | ForEach-Object { "{0:X2}" -f $_ })
}

function Convert-HexToBytes([string]$Hex) {
    [byte[]]$bytes = New-Object byte[] ([int]($Hex.Length / 2))
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($Hex.Substring($i * 2, 2), 16)
    }
    return $bytes
}

function Get-Sha1Hex([byte[]]$Bytes) {
    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        return (Convert-BytesToHex ($sha1.ComputeHash($Bytes)))
    }
    finally {
        $sha1.Dispose()
    }
}

function Test-PvdAt([System.IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    $offset = (16 * $SectorSize) + $UserOffset
    if ($Stream.Length -lt ($offset + 2048)) { return $null }
    $buffer = New-Object byte[] 2048
    $Stream.Position = $offset
    [void]$Stream.Read($buffer, 0, 2048)
    $signature = [Text.Encoding]::ASCII.GetString($buffer, 1, 5)
    if ($buffer[0] -ne 1 -or $signature -ne "CD001") { return $null }
    return [ordered]@{
        sectorSize = $SectorSize
        userOffset = $UserOffset
        userSize = 2048
        description = $(if ($SectorSize -eq 2352) { "Raw PS1 BIN/CUE track (2352-byte sectors)" } else { "ISO image (2048-byte sectors)" })
    }
}

function Detect-DiscLayout([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        foreach ($candidate in @(
            @{ SectorSize = 2048; UserOffset = 0 },
            @{ SectorSize = 2352; UserOffset = 24 },
            @{ SectorSize = 2336; UserOffset = 8 }
        )) {
            $layout = Test-PvdAt $stream $candidate.SectorSize $candidate.UserOffset
            if ($null -ne $layout) { return $layout }
        }
    }
    finally {
        $stream.Dispose()
    }
    throw "Could not detect PS1 disc layout for $Path"
}

function Convert-DiscFileOffsetToImageOffset($Layout, [int]$FileLba, [int64]$FileOffset) {
    $sectorOffset = [int]($FileOffset % 2048)
    $sector = [int64]$FileLba + [int64][Math]::Floor($FileOffset / 2048)
    return ([int64]$sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset + $sectorOffset
}

function Read-WadBytes([IO.FileStream]$Stream, $Layout, [int64]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $imageOffset = Convert-DiscFileOffsetToImageOffset $Layout $WadLba $absolute
        $sectorOffset = [int]($absolute % 2048)
        $toRead = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = $imageOffset
        [void]$Stream.Read($result, $written, $toRead)
        $written += $toRead
        $remaining -= $toRead
        $absolute += $toRead
    }
    return $result
}

function Write-WadBytes([IO.FileStream]$Stream, $Layout, [int64]$Offset, [byte[]]$Bytes) {
    $remaining = $Bytes.Length
    $read = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $imageOffset = Convert-DiscFileOffsetToImageOffset $Layout $WadLba $absolute
        $sectorOffset = [int]($absolute % 2048)
        $toWrite = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = $imageOffset
        $Stream.Write($Bytes, $read, $toWrite)
        $read += $toWrite
        $remaining -= $toWrite
        $absolute += $toWrite
    }
}

function Parse-ArchiveHeader([byte[]]$Bytes, [int64]$ArchiveSize) {
    $entries = @()
    $firstDataOffset = [int64](Get-UInt32LE $Bytes 0)
    if ($firstDataOffset -le 0 -or $firstDataOffset -gt $Bytes.Length) {
        $firstDataOffset = $Bytes.Length
    }
    for ($offset = 0; $offset -le ([Math]::Min($Bytes.Length, $firstDataOffset) - 8); $offset += 8) {
        $fileOffset = [int64](Get-UInt32LE $Bytes $offset)
        $fileSize = [int64](Get-UInt32LE $Bytes ($offset + 4))
        if ($fileOffset -eq 0 -and $fileSize -eq 0) { continue }
        if ($fileOffset -lt 0 -or $fileSize -le 0 -or ($fileOffset + $fileSize) -gt $ArchiveSize) { continue }
        $entries += [ordered]@{
            index = [int]($offset / 8)
            offset = $fileOffset
            size = $fileSize
            headerOffset = $offset
        }
    }
    return $entries
}

function Get-WadEntryTable([IO.FileStream]$Stream, $Layout) {
    $head = Read-WadBytes $Stream $Layout 0 4096
    return @(Parse-ArchiveHeader $head 110260224)
}

function Get-WadEntry($Entries, [int]$Index) {
    foreach ($entry in $Entries) {
        if ([int]$entry.index -eq $Index) { return $entry }
    }
    throw "WAD entry $Index was not found in the archive header."
}

function Get-SubEntry($SubEntries, [int]$Index) {
    foreach ($entry in $SubEntries) {
        if ([int]$entry.index -eq $Index) { return $entry }
    }
    throw "Skybox subfile $Index was not found in the target asset package."
}

function Get-SubfileCapacity($SubEntries, $SubEntry, [int64]$ParentSize) {
    $nextOffset = $ParentSize
    foreach ($entry in $SubEntries) {
        if ([int64]$entry.offset -gt [int64]$SubEntry.offset -and [int64]$entry.offset -lt $nextOffset) {
            $nextOffset = [int64]$entry.offset
        }
    }
    return [int]($nextOffset - [int64]$SubEntry.offset)
}

$target = Get-SkyboxLevel $TargetLevelKey
$donor = Get-SkyboxLevel $DonorLevelKey

if (-not $PlanOnly -and [int]$target.assetWadEntry -eq 10 -and -not $AllowStartupCriticalPatch) {
    throw "Refusing to write a BIN/CUE patch against WAD entry 10 by default. Current boot tests show this package is startup-critical for a new game and segment probes can crash before Artisans loads. Use -PlanOnly for analysis, or pass -AllowStartupCriticalPatch only for an intentionally disposable crash-risk test."
}

foreach ($level in @($target, $donor)) {
    if ([string]$level.assetMapSource -eq "unverified-hardcoded" -and -not $AllowUnverifiedAssetWadMap) {
        throw "$($level.displayName)'s skybox asset WAD entry is still using the old unverified hardcoded map. Refusing to export by default because a wrong asset package can corrupt level data. Refresh _skybox_probe\analysis-refresh.json or pass -AllowUnverifiedAssetWadMap for research-only output."
    }
}

if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $OutPath = ".\Spyro the Dragon (USA)-skybox-$($target.key)-from-$($donor.key).bin"
}

$resolvedImagePath = Resolve-WorkspacePath $ImagePath
$resolvedOutPath = Resolve-WorkspacePath $OutPath
if ([string]::IsNullOrWhiteSpace($CuePath)) {
    $CuePath = [System.IO.Path]::ChangeExtension($resolvedOutPath, ".cue")
}
$resolvedCuePath = Resolve-WorkspacePath $CuePath
if ([string]::IsNullOrWhiteSpace($PlanPath)) {
    $PlanPath = "$resolvedOutPath.patchplan.json"
}
$resolvedPlanPath = Resolve-WorkspacePath $PlanPath

if (-not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing source image: $resolvedImagePath" }

$layout = Detect-DiscLayout $resolvedImagePath
$patches = New-Object System.Collections.ArrayList
$isSegmentSwap = $SegmentStart -ge 0

$stream = [IO.File]::OpenRead($resolvedImagePath)
try {
    $wadEntries = Get-WadEntryTable $stream $layout
    $targetWadEntry = Get-WadEntry $wadEntries ([int]$target.assetWadEntry)
    $donorWadEntry = Get-WadEntry $wadEntries ([int]$donor.assetWadEntry)

    $targetHeader = Read-WadBytes $stream $layout ([int64]$targetWadEntry.offset) ([Math]::Min(65536, [int]$targetWadEntry.size))
    $donorHeader = Read-WadBytes $stream $layout ([int64]$donorWadEntry.offset) ([Math]::Min(65536, [int]$donorWadEntry.size))
    $targetSubEntries = @(Parse-ArchiveHeader $targetHeader ([int64]$targetWadEntry.size))
    $donorSubEntries = @(Parse-ArchiveHeader $donorHeader ([int64]$donorWadEntry.size))
    $targetSky = Get-SubEntry $targetSubEntries $SkySubfileIndex
    $donorSky = Get-SubEntry $donorSubEntries $SkySubfileIndex
    $targetCapacity = Get-SubfileCapacity $targetSubEntries $targetSky ([int64]$targetWadEntry.size)

    $dataWadOffset = [int64]$targetWadEntry.offset + [int64]$targetSky.offset

    if ($isSegmentSwap) {
        if ($SegmentStart -lt 0) { throw "SegmentStart must be >= 0." }
        $actualSegmentLength = if ($SegmentLength -gt 0) { $SegmentLength } else { [int]$targetSky.size - $SegmentStart }
        if ($actualSegmentLength -le 0) { throw "SegmentLength resolved to zero bytes." }
        if (($SegmentStart + $actualSegmentLength) -gt [int]$targetSky.size) {
            throw "Target segment 0x$($SegmentStart.ToString('X'))..0x$(($SegmentStart + $actualSegmentLength).ToString('X')) exceeds $($target.displayName) subfile size 0x$(([int]$targetSky.size).ToString('X'))."
        }
        if (($SegmentStart + $actualSegmentLength) -gt [int]$donorSky.size) {
            throw "Donor segment 0x$($SegmentStart.ToString('X'))..0x$(($SegmentStart + $actualSegmentLength).ToString('X')) exceeds $($donor.displayName) subfile size 0x$(([int]$donorSky.size).ToString('X'))."
        }
        if (-not $PlanOnly -and -not $AllowExperimentalSegmentSwap) {
            throw "Refusing to write a skybox segment BIN without -AllowExperimentalSegmentSwap. Segment probes are still terrain/texture-risk research outputs."
        }

        $segmentWadOffset = $dataWadOffset + [int64]$SegmentStart
        [byte[]]$donorSegmentBytes = Read-WadBytes $stream $layout ([int64]$donorWadEntry.offset + [int64]$donorSky.offset + [int64]$SegmentStart) $actualSegmentLength
        [byte[]]$targetSegmentBytes = Read-WadBytes $stream $layout $segmentWadOffset $actualSegmentLength
        [void]$patches.Add([ordered]@{
            levelKey = [string]$target.scriptKey
            levelName = [string]$target.displayName
            kind = "skybox-subfile-segment"
            description = "Research-only segment probe: copy $($donor.displayName) subfile $SkySubfileIndex range 0x$($SegmentStart.ToString('X'))..0x$(($SegmentStart + $actualSegmentLength).ToString('X')) into $($target.displayName) without changing the archive header."
            wadRelativeOffset = ("0x{0:X}" -f $segmentWadOffset)
            imageOffset = Convert-DiscFileOffsetToImageOffset $layout $WadLba $segmentWadOffset
            bytesHex = Convert-BytesToHex $donorSegmentBytes
            oldBytesSha1 = Get-Sha1Hex $targetSegmentBytes
            newBytesSha1 = Get-Sha1Hex $donorSegmentBytes
            targetAssetWadEntry = [int]$target.assetWadEntry
            donorAssetWadEntry = [int]$donor.assetWadEntry
            skySubfileIndex = $SkySubfileIndex
            segmentStart = $SegmentStart
            segmentLength = $actualSegmentLength
            segmentEndExclusive = $SegmentStart + $actualSegmentLength
            targetSkySize = [int]$targetSky.size
            donorSkySize = [int]$donorSky.size
            terrainTextureRisk = "unknown; use disposable BIN/CUE only"
        })
    }
    else {
        if (-not $PlanOnly -and -not $AllowExperimentalWholeSubfileSwap) {
            throw "Whole-subfile skybox exports are disabled by default. Subfile $SkySubfileIndex also affects terrain/material texture data in current tests, so a full replacement can make Artisans terrain look wrong. Use -PlanOnly for analysis or -AllowExperimentalWholeSubfileSwap only for disposable research."
        }
        if ([int]$donorSky.size -gt $targetCapacity) {
            throw "$($donor.displayName) skybox is 0x$(([int]$donorSky.size).ToString('X')) bytes, but $($target.displayName)'s skybox slot has only 0x$($targetCapacity.ToString('X')) bytes before the next subfile. Pick a smaller donor or use a future repack workflow."
        }
        if ([int]$donorSky.size -ne [int]$targetSky.size -and -not $AllowExperimentalSizeMismatch) {
            throw "$($target.displayName) skybox subfile is 0x$(([int]$targetSky.size).ToString('X')) bytes, but $($donor.displayName)'s skybox is 0x$(([int]$donorSky.size).ToString('X')) bytes. Refusing to write a size-changing skybox swap by default because the previous exporter zero-filled the rest of the target slot and could erase adjacent level-package data. Use a same-size donor or pass -AllowExperimentalSizeMismatch for research-only output."
        }

        [byte[]]$donorBytes = Read-WadBytes $stream $layout ([int64]$donorWadEntry.offset + [int64]$donorSky.offset) ([int]$donorSky.size)
        [byte[]]$targetBytesToReplace = Read-WadBytes $stream $layout ([int64]$targetWadEntry.offset + [int64]$targetSky.offset) ([int]$donorBytes.Length)
        [byte[]]$sizeBytes = [BitConverter]::GetBytes([uint32]$donorBytes.Length)
        [byte[]]$oldSizeBytes = [BitConverter]::GetBytes([uint32]$targetSky.size)
        $sizeWadOffset = [int64]$targetWadEntry.offset + [int64]$targetSky.headerOffset + 4L

        [void]$patches.Add([ordered]@{
            levelKey = [string]$target.scriptKey
            levelName = [string]$target.displayName
            kind = "skybox-subfile-size"
            description = "Set $($target.displayName) subfile $SkySubfileIndex size to donor $($donor.displayName) size. Whole-subfile skybox swaps are terrain/texture-risk research only."
            wadRelativeOffset = ("0x{0:X}" -f $sizeWadOffset)
            imageOffset = Convert-DiscFileOffsetToImageOffset $layout $WadLba $sizeWadOffset
            bytesHex = Convert-BytesToHex $sizeBytes
            oldBytesHex = Convert-BytesToHex $oldSizeBytes
            targetAssetWadEntry = [int]$target.assetWadEntry
            donorAssetWadEntry = [int]$donor.assetWadEntry
            skySubfileIndex = $SkySubfileIndex
            targetSkySize = [int]$targetSky.size
            donorSkySize = $donorBytes.Length
            terrainTextureRisk = "high"
        })

        [void]$patches.Add([ordered]@{
            levelKey = [string]$target.scriptKey
            levelName = [string]$target.displayName
            kind = "skybox-asset-subfile"
            description = "Research-only whole-subfile swap of $($target.displayName) asset subfile $SkySubfileIndex to donor $($donor.displayName)."
            wadRelativeOffset = ("0x{0:X}" -f $dataWadOffset)
            imageOffset = Convert-DiscFileOffsetToImageOffset $layout $WadLba $dataWadOffset
            bytesHex = Convert-BytesToHex $donorBytes
            oldBytesSha1 = Get-Sha1Hex $targetBytesToReplace
            newBytesSha1 = Get-Sha1Hex $donorBytes
            targetAssetWadEntry = [int]$target.assetWadEntry
            donorAssetWadEntry = [int]$donor.assetWadEntry
            skySubfileIndex = $SkySubfileIndex
            originalSlotSize = $targetCapacity
            targetSkySize = [int]$targetSky.size
            donorSkySize = $donorBytes.Length
            writesOnlyDonorBytes = $true
            terrainTextureRisk = "high"
        })
    }
}
finally {
    $stream.Dispose()
}

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Export-SpyroSkyboxPatchTest.ps1"
    warning = "Experimental skybox asset subfile patch. Current evidence shows whole subfile swaps can alter terrain/material texture data. Use disposable BIN/CUE outputs only."
    patchMode = $(if ($isSegmentSwap) { "segment-probe" } else { "whole-subfile-research" })
    sizeMismatchPolicy = $(if ($AllowExperimentalSizeMismatch) { "allowed-research-only; writes donor byte count only and preserves trailing target slot bytes" } else { "blocked-by-default" })
    wholeSubfilePolicy = $(if ($AllowExperimentalWholeSubfileSwap) { "allowed-research-only" } else { "blocked-for-bin-output" })
    segmentPolicy = $(if ($AllowExperimentalSegmentSwap) { "allowed-research-only" } else { "blocked-for-bin-output unless PlanOnly" })
    startupCriticalPolicy = $(if ($AllowStartupCriticalPatch) { "WAD entry 10 patch allowed for explicit crash-risk research" } else { "WAD entry 10 BIN/CUE patches blocked by default" })
    assetMapPolicy = $(if ($AllowUnverifiedAssetWadMap) { "unverified hardcoded WAD entries allowed for research-only output" } else { "requires probe-verified asset WAD entry when probe data exists" })
    imagePath = (Resolve-Path -LiteralPath $resolvedImagePath).Path
    outPath = $(if ($PlanOnly) { $null } else { $resolvedOutPath })
    cuePath = $(if ($PlanOnly) { $null } else { $resolvedCuePath })
    discLayout = $layout
    targetLevel = $target
    donorLevel = $donor
    skySubfileIndex = $SkySubfileIndex
    patchCount = $patches.Count
    patches = @($patches.ToArray())
    binaryPatches = @($patches.ToArray())
}

$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedPlanPath -Encoding UTF8

if (-not $PlanOnly) {
    Copy-Item -LiteralPath $resolvedImagePath -Destination $resolvedOutPath -Force
    $outStream = [System.IO.File]::Open($resolvedOutPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::Read)
    try {
        foreach ($patch in @($patches.ToArray())) {
            $hex = [string]$patch.bytesHex
            [byte[]]$bytes = Convert-HexToBytes $hex
            $wadOffsetText = ([string]$patch.wadRelativeOffset).Substring(2)
            $wadOffset = [Convert]::ToInt64($wadOffsetText, 16)
            if ($patch.Contains("oldBytesHex")) {
                $expectedOldHex = [string]$patch.oldBytesHex
                $currentHex = Convert-BytesToHex (Read-WadBytes $outStream $layout $wadOffset $bytes.Length)
                if ($currentHex -ne $expectedOldHex) {
                    throw "Refusing to write $($patch.kind) at $($patch.wadRelativeOffset): source bytes no longer match the patch plan."
                }
            }
            elseif ($patch.Contains("oldBytesSha1")) {
                $expectedOldSha1 = [string]$patch.oldBytesSha1
                $currentSha1 = Get-Sha1Hex (Read-WadBytes $outStream $layout $wadOffset $bytes.Length)
                if ($currentSha1 -ne $expectedOldSha1) {
                    throw "Refusing to write $($patch.kind) at $($patch.wadRelativeOffset): source hash no longer matches the patch plan."
                }
            }
            Write-WadBytes $outStream $layout $wadOffset $bytes
        }
    }
    finally {
        $outStream.Dispose()
    }

    $cueFileName = [System.IO.Path]::GetFileName($resolvedOutPath)
    @(
        "FILE `"$cueFileName`" BINARY",
        "  TRACK 01 MODE2/2352",
        "    INDEX 01 00:00:00"
    ) | Set-Content -LiteralPath $resolvedCuePath -Encoding ASCII
}

Write-Host "Wrote $($target.displayName) skybox patch plan to $resolvedPlanPath"
if ($PlanOnly) {
    Write-Host "PlanOnly set; no BIN/CUE was written."
}
else {
    Write-Host "Wrote patched BIN to $resolvedOutPath"
    Write-Host "Wrote CUE to $resolvedCuePath"
}
Write-Host ("Skybox swap: {0} <- {1}; patches: {2}" -f $target.displayName, $donor.displayName, $patches.Count)
