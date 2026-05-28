param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$ProbePath = ".\_skybox_probe\analysis-refresh.json",
    [string]$OutPath = ".\spyro-skybox-catalog.json",
    [string]$MarkdownPath = ".\spyro-skybox-catalog.md",
    [int]$SkySubfileIndex = 3,
    [switch]$IncludeUnverifiedHardcodedMap
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37
$WadSize = 110260224

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

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Convert-BytesToHex([byte[]]$Bytes) {
    return -join ($Bytes | ForEach-Object { "{0:X2}" -f $_ })
}

function Get-Sha1Hex([byte[]]$Bytes) {
    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        return Convert-BytesToHex ($sha1.ComputeHash($Bytes))
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
    return @(Parse-ArchiveHeader $head $WadSize)
}

function Find-ArchiveEntry($Entries, [int]$Index) {
    foreach ($entry in @($Entries)) {
        if ([int]$entry.index -eq $Index) { return $entry }
    }
    return $null
}

function Find-SubEntry($SubEntries, [int]$Index) {
    foreach ($entry in @($SubEntries)) {
        if ([int]$entry.index -eq $Index) { return $entry }
    }
    return $null
}

function Get-SubfileCapacity($SubEntries, $SubEntry, [int64]$ParentSize) {
    $nextOffset = $ParentSize
    foreach ($entry in @($SubEntries)) {
        if ([int64]$entry.offset -gt [int64]$SubEntry.offset -and [int64]$entry.offset -lt $nextOffset) {
            $nextOffset = [int64]$entry.offset
        }
    }
    return [int]($nextOffset - [int64]$SubEntry.offset)
}

function Get-SkyboxProbeMap([string]$Path) {
    $map = @{}
    $resolvedPath = Resolve-WorkspacePath $Path
    if (-not (Test-Path -LiteralPath $resolvedPath)) { return $map }

    $probe = Get-Content -LiteralPath $resolvedPath -Raw | ConvertFrom-Json
    $levels = Get-JsonProperty $probe "levels"
    if ($null -eq $levels) { return $map }

    foreach ($level in @($levels)) {
        $levelName = [string](Get-JsonProperty $level "levelName")
        $assetWadIndex = Get-JsonProperty $level "assetWadIndex"
        if ([string]::IsNullOrWhiteSpace($levelName) -or $null -eq $assetWadIndex) { continue }

        foreach ($namePart in @($levelName -split "/")) {
            $key = Normalize-LevelKey $namePart
            if ([string]::IsNullOrWhiteSpace($key)) { continue }
            $map[$key] = [ordered]@{
                levelName = $levelName
                metadataWadIndex = Get-JsonProperty $level "metadataWadIndex"
                metadataOffset = Get-JsonProperty $level "metadataOffset"
                metadataSize = Get-JsonProperty $level "metadataSize"
                assetWadIndex = [int]$assetWadIndex
                levelId = Get-JsonProperty $level "levelId"
                source = $resolvedPath
            }
        }
    }
    return $map
}

function Get-SkyboxLevelRows([hashtable]$ProbeMap) {
    $levels = @(
        @{ key = "stonehill"; scriptKey = "StoneHill"; displayName = "Stone Hill"; hardcodedAssetWadEntry = 10 },
        @{ key = "darkhollow"; scriptKey = "DarkHollow"; displayName = "Dark Hollow"; hardcodedAssetWadEntry = 12 },
        @{ key = "townsquare"; scriptKey = "TownSquare"; displayName = "Town Square"; hardcodedAssetWadEntry = 14 },
        @{ key = "toasty"; scriptKey = "Toasty"; displayName = "Toasty"; hardcodedAssetWadEntry = 16 },
        @{ key = "sunnyflight"; scriptKey = "SunnyFlight"; displayName = "Sunny Flight"; hardcodedAssetWadEntry = 18 },
        @{ key = "peacekeepers"; scriptKey = "PeaceKeepers"; displayName = "Peace Keepers"; hardcodedAssetWadEntry = 20 },
        @{ key = "drycanyon"; scriptKey = "DryCanyon"; displayName = "Dry Canyon"; hardcodedAssetWadEntry = 22 },
        @{ key = "clifftown"; scriptKey = "CliffTown"; displayName = "Cliff Town"; hardcodedAssetWadEntry = 24 },
        @{ key = "icecavern"; scriptKey = "IceCavern"; displayName = "Ice Cavern"; hardcodedAssetWadEntry = 26 },
        @{ key = "doctorshemp"; scriptKey = "DoctorShemp"; displayName = "Doctor Shemp"; hardcodedAssetWadEntry = 28 },
        @{ key = "nightflight"; scriptKey = "NightFlight"; displayName = "Night Flight"; hardcodedAssetWadEntry = 30 },
        @{ key = "magiccrafters"; scriptKey = "MagicCrafters"; displayName = "Magic Crafters"; hardcodedAssetWadEntry = 32 },
        @{ key = "alpineridge"; scriptKey = "AlpineRidge"; displayName = "Alpine Ridge"; hardcodedAssetWadEntry = 34 },
        @{ key = "highcaves"; scriptKey = "HighCaves"; displayName = "High Caves"; hardcodedAssetWadEntry = 36 },
        @{ key = "wizardpeak"; scriptKey = "WizardPeak"; displayName = "Wizard Peak"; hardcodedAssetWadEntry = 38 },
        @{ key = "blowhard"; scriptKey = "Blowhard"; displayName = "Blowhard"; hardcodedAssetWadEntry = 40 },
        @{ key = "crystalflight"; scriptKey = "CrystalFlight"; displayName = "Crystal Flight"; hardcodedAssetWadEntry = 42 },
        @{ key = "beastmakers"; scriptKey = "BeastMakers"; displayName = "Beast Makers"; hardcodedAssetWadEntry = 44 },
        @{ key = "terracevillage"; scriptKey = "TerraceVillage"; displayName = "Terrace Village"; hardcodedAssetWadEntry = 46 },
        @{ key = "mistybog"; scriptKey = "MistyBog"; displayName = "Misty Bog"; hardcodedAssetWadEntry = 48 },
        @{ key = "treetops"; scriptKey = "TreeTops"; displayName = "Tree Tops"; hardcodedAssetWadEntry = 50 },
        @{ key = "metalhead"; scriptKey = "Metalhead"; displayName = "Metalhead"; hardcodedAssetWadEntry = 52 },
        @{ key = "wildflight"; scriptKey = "WildFlight"; displayName = "Wild Flight"; hardcodedAssetWadEntry = 54 },
        @{ key = "dreamweavers"; scriptKey = "DreamWeavers"; displayName = "Dream Weavers"; hardcodedAssetWadEntry = 56 },
        @{ key = "darkpassage"; scriptKey = "DarkPassage"; displayName = "Dark Passage"; hardcodedAssetWadEntry = 58 },
        @{ key = "loftycastle"; scriptKey = "LoftyCastle"; displayName = "Lofty Castle"; hardcodedAssetWadEntry = 60 },
        @{ key = "hauntedtowers"; scriptKey = "HauntedTowers"; displayName = "Haunted Towers"; hardcodedAssetWadEntry = 62 },
        @{ key = "jacques"; scriptKey = "Jacques"; displayName = "Jacques"; hardcodedAssetWadEntry = 64 },
        @{ key = "icyflight"; scriptKey = "IcyFlight"; displayName = "Icy Flight"; hardcodedAssetWadEntry = 66 },
        @{ key = "gnastysworld"; scriptKey = "GnastysWorld"; displayName = "Gnasty's World"; hardcodedAssetWadEntry = 68 },
        @{ key = "gnorccove"; scriptKey = "GnorcCove"; displayName = "Gnorc Cove"; hardcodedAssetWadEntry = 70 },
        @{ key = "twilightharbor"; scriptKey = "TwilightHarbor"; displayName = "Twilight Harbor"; hardcodedAssetWadEntry = 72 },
        @{ key = "gnastygnorc"; scriptKey = "GnastyGnorc"; displayName = "Gnasty Gnorc"; hardcodedAssetWadEntry = 74 },
        @{ key = "gnastysloot"; scriptKey = "GnastysLoot"; displayName = "Gnasty's Loot"; hardcodedAssetWadEntry = 76 }
    )

    foreach ($level in $levels) {
        $normalizedName = Normalize-LevelKey ([string]$level.displayName)
        $probeRow = $null
        if ($ProbeMap.ContainsKey($normalizedName)) {
            $probeRow = $ProbeMap[$normalizedName]
        }

        if ($null -ne $probeRow) {
            [ordered]@{
                key = [string]$level.key
                scriptKey = [string]$level.scriptKey
                displayName = [string]$level.displayName
                assetWadEntry = [int]$probeRow.assetWadIndex
                hardcodedAssetWadEntry = [int]$level.hardcodedAssetWadEntry
                assetMapSource = "probe"
                probeLevelName = [string]$probeRow.levelName
                metadataWadEntry = $probeRow.metadataWadIndex
                levelId = $probeRow.levelId
            }
        }
        else {
            [ordered]@{
                key = [string]$level.key
                scriptKey = [string]$level.scriptKey
                displayName = [string]$level.displayName
                assetWadEntry = [int]$level.hardcodedAssetWadEntry
                hardcodedAssetWadEntry = [int]$level.hardcodedAssetWadEntry
                assetMapSource = "unverified-hardcoded"
                probeLevelName = $null
                metadataWadEntry = $null
                levelId = $null
            }
        }
    }
}

function Add-MarkdownRow($Lines, $Columns) {
    [void]$Lines.Add("| " + (($Columns | ForEach-Object { ([string]$_).Replace("|", "\|") }) -join " | ") + " |")
}

$resolvedImagePath = Resolve-WorkspacePath $ImagePath
$resolvedOutPath = Resolve-WorkspacePath $OutPath
$resolvedMarkdownPath = Resolve-WorkspacePath $MarkdownPath
if (-not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing source image: $resolvedImagePath" }

$probeMap = Get-SkyboxProbeMap $ProbePath
$baseRows = @(Get-SkyboxLevelRows $probeMap)
$layout = Detect-DiscLayout $resolvedImagePath
$levelReports = New-Object System.Collections.ArrayList

$stream = [IO.File]::OpenRead($resolvedImagePath)
try {
    $wadEntries = Get-WadEntryTable $stream $layout
    foreach ($baseRow in $baseRows) {
        $report = [ordered]@{
            key = [string]$baseRow.key
            scriptKey = [string]$baseRow.scriptKey
            displayName = [string]$baseRow.displayName
            status = "pending"
            assetMapSource = [string]$baseRow.assetMapSource
            assetWadEntry = [int]$baseRow.assetWadEntry
            hardcodedAssetWadEntry = [int]$baseRow.hardcodedAssetWadEntry
            probeLevelName = $baseRow.probeLevelName
            metadataWadEntry = $baseRow.metadataWadEntry
            levelId = $baseRow.levelId
            skySubfileIndex = $SkySubfileIndex
            assetWadOffset = $null
            assetWadSize = $null
            skySubfileOffset = $null
            skySubfileSize = $null
            skySubfileCapacity = $null
            skySubfileSlackBytes = $null
            skyWadOffset = $null
            skyImageOffset = $null
            skySha1 = $null
            compatibleDonorKeys = @()
            compatibleDonorNames = @()
            notes = @()
        }

        if ([string]$baseRow.assetMapSource -eq "unverified-hardcoded" -and -not $IncludeUnverifiedHardcodedMap) {
            $report.status = "needs-probe-refresh"
            $report.notes = @("Skipped unsafe hardcoded WAD entry. Re-run WAD analysis or use -IncludeUnverifiedHardcodedMap for research only.")
            [void]$levelReports.Add($report)
            continue
        }

        $assetEntry = Find-ArchiveEntry $wadEntries ([int]$baseRow.assetWadEntry)
        if ($null -eq $assetEntry) {
            $report.status = "missing-asset-wad-entry"
            $report.notes = @("WAD entry was not present in the archive table.")
            [void]$levelReports.Add($report)
            continue
        }

        $report.assetWadOffset = [int64]$assetEntry.offset
        $report.assetWadSize = [int64]$assetEntry.size
        $assetHeader = Read-WadBytes $stream $layout ([int64]$assetEntry.offset) ([Math]::Min(65536, [int]$assetEntry.size))
        $subEntries = @(Parse-ArchiveHeader $assetHeader ([int64]$assetEntry.size))
        $skySubEntry = Find-SubEntry $subEntries $SkySubfileIndex
        if ($null -eq $skySubEntry) {
            $report.status = "missing-skybox-subfile"
            $report.notes = @("Asset package did not expose subfile $SkySubfileIndex in the parsed nested archive header.")
            [void]$levelReports.Add($report)
            continue
        }

        $capacity = Get-SubfileCapacity $subEntries $skySubEntry ([int64]$assetEntry.size)
        $skyWadOffset = [int64]$assetEntry.offset + [int64]$skySubEntry.offset
        $skyBytes = Read-WadBytes $stream $layout $skyWadOffset ([int]$skySubEntry.size)
        $report.status = "cataloged"
        $report.skySubfileOffset = [int64]$skySubEntry.offset
        $report.skySubfileSize = [int]$skySubEntry.size
        $report.skySubfileCapacity = [int]$capacity
        $report.skySubfileSlackBytes = [int]($capacity - [int]$skySubEntry.size)
        $report.skyWadOffset = $skyWadOffset
        $report.skyImageOffset = Convert-DiscFileOffsetToImageOffset $layout $WadLba $skyWadOffset
        $report.skySha1 = Get-Sha1Hex $skyBytes
        if ([int]$capacity -lt [int]$skySubEntry.size) {
            $report.status = "invalid-capacity"
            $report.notes = @("Parsed subfile size is larger than its slot capacity.")
        }
        [void]$levelReports.Add($report)
    }
}
finally {
    $stream.Dispose()
}

$cataloged = @($levelReports.ToArray() | Where-Object { $_.status -eq "cataloged" })
$groups = New-Object System.Collections.ArrayList
foreach ($group in @($cataloged | Group-Object -Property { [int]$_["skySubfileSize"] } | Sort-Object { [int]$_.Name })) {
    $members = @($group.Group | Sort-Object displayName)
    [void]$groups.Add([ordered]@{
        skySubfileSize = [int]$group.Name
        memberCount = $members.Count
        safeSwapCandidate = ($members.Count -gt 1)
        members = @($members | ForEach-Object {
            [ordered]@{
                key = $_.key
                scriptKey = $_.scriptKey
                displayName = $_.displayName
                assetWadEntry = $_.assetWadEntry
                skySha1 = $_.skySha1
            }
        })
    })
}

foreach ($report in @($levelReports.ToArray())) {
    if ($report.status -ne "cataloged") { continue }
    $donors = @($cataloged | Where-Object { $_.key -ne $report.key -and [int]$_.skySubfileSize -eq [int]$report.skySubfileSize } | Sort-Object displayName)
    $report.compatibleDonorKeys = @($donors | ForEach-Object { $_.scriptKey })
    $report.compatibleDonorNames = @($donors | ForEach-Object { $_.displayName })
    if ($donors.Count -eq 0) {
        $report.notes = @("No same-size donors in the current verified catalog.")
    }
    else {
        $report.notes = @("Same-size donors are archive-size-compatible only. Use segment probes before any BIN test; whole-subfile swaps are terrain/texture-risk.")
    }
}

$suggestedWholeSubfilePlans = New-Object System.Collections.ArrayList
foreach ($group in @($groups.ToArray() | Where-Object { $_.safeSwapCandidate })) {
    $members = @($group.members)
    for ($i = 0; $i -lt $members.Count; $i++) {
        for ($j = 0; $j -lt $members.Count; $j++) {
            if ($i -eq $j) { continue }
            $target = $members[$i]
            $donor = $members[$j]
            $planName = "_skybox_probe\whole-subfile-$($target.key)-from-$($donor.key).patchplan.json"
            [void]$suggestedWholeSubfilePlans.Add([ordered]@{
                targetKey = $target.scriptKey
                targetName = $target.displayName
                donorKey = $donor.scriptKey
                donorName = $donor.displayName
                skySubfileSize = [int]$group.skySubfileSize
                terrainTextureRisk = "high; whole subfile swaps changed Artisans terrain/material textures in game"
                command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroSkyboxPatchTest.ps1 -TargetLevelKey $($target.scriptKey) -DonorLevelKey $($donor.scriptKey) -PlanOnly -PlanPath `".\$planName`""
            })
        }
    }
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "New-SpyroSkyboxCatalog.ps1"
    imagePath = (Resolve-Path -LiteralPath $resolvedImagePath).Path
    probePath = $(if (Test-Path -LiteralPath (Resolve-WorkspacePath $ProbePath)) { (Resolve-Path -LiteralPath (Resolve-WorkspacePath $ProbePath)).Path } else { $null })
    skySubfileIndex = $SkySubfileIndex
    includeUnverifiedHardcodedMap = [bool]$IncludeUnverifiedHardcodedMap
    discLayout = $layout
    policy = [ordered]@{
        defaultExport = "whole-subfile BIN export disabled; same-size only prevents archive-size corruption, not terrain/texture changes"
        unverifiedMap = "skipped unless -IncludeUnverifiedHardcodedMap is passed"
        sizeChangingSwap = "requires future repack or explicit research override in the exporter"
        nextStep = "use segment probes to isolate the actual sky-controlled ranges before making a BIN/CUE"
    }
    summary = [ordered]@{
        totalLevels = @($levelReports.ToArray()).Count
        catalogedLevels = $cataloged.Count
        needsProbeRefresh = @($levelReports.ToArray() | Where-Object { $_.status -eq "needs-probe-refresh" }).Count
        sameSizeGroups = @($groups.ToArray() | Where-Object { $_.safeSwapCandidate }).Count
        suggestedWholeSubfilePlans = @($suggestedWholeSubfilePlans.ToArray()).Count
    }
    levels = @($levelReports.ToArray())
    sameSizeGroups = @($groups.ToArray())
    suggestedWholeSubfilePlans = @($suggestedWholeSubfilePlans.ToArray())
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutPath -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Spyro Skybox Catalog")
[void]$lines.Add("")
[void]$lines.Add(("Generated: {0}" -f $result.generatedAt))
[void]$lines.Add(('Image: `{0}`' -f $result.imagePath))
[void]$lines.Add(('Sky subfile index: `{0}`' -f $SkySubfileIndex))
[void]$lines.Add("")
[void]$lines.Add("## Summary")
[void]$lines.Add("")
[void]$lines.Add(("- Cataloged levels: {0}/{1}" -f $result.summary.catalogedLevels, $result.summary.totalLevels))
[void]$lines.Add(("- Same-size groups, archive-size only: {0}" -f $result.summary.sameSizeGroups))
[void]$lines.Add(("- Whole-subfile plan-only commands: {0}" -f $result.summary.suggestedWholeSubfilePlans))
[void]$lines.Add(("- Levels needing probe refresh: {0}" -f $result.summary.needsProbeRefresh))
[void]$lines.Add("")
[void]$lines.Add("Same-size does not mean visually safe. Current in-game evidence shows whole subfile swaps can change terrain/material textures, so BIN output is blocked by the exporter unless explicitly overridden for disposable research.")
[void]$lines.Add("")
[void]$lines.Add("## Levels")
[void]$lines.Add("")
Add-MarkdownRow $lines @("Level", "Status", "Map", "WAD", "Sky Size", "Capacity", "SHA1", "Compatible Donors")
Add-MarkdownRow $lines @("---", "---", "---", "---", "---", "---", "---", "---")
foreach ($report in @($levelReports.ToArray() | Sort-Object displayName)) {
    $donorsText = if (@($report.compatibleDonorNames).Count -gt 0) { (@($report.compatibleDonorNames) -join ", ") } else { "" }
    Add-MarkdownRow $lines @(
        $report.displayName,
        $report.status,
        $report.assetMapSource,
        $(if ($null -ne $report.assetWadEntry) { $report.assetWadEntry } else { "" }),
        $(if ($null -ne $report.skySubfileSize) { ("0x{0:X}" -f [int]$report.skySubfileSize) } else { "" }),
        $(if ($null -ne $report.skySubfileCapacity) { ("0x{0:X}" -f [int]$report.skySubfileCapacity) } else { "" }),
        $(if ($null -ne $report.skySha1) { ([string]$report.skySha1).Substring(0, 12) } else { "" }),
        $donorsText
    )
}

[void]$lines.Add("")
[void]$lines.Add("## Same-Size Groups")
[void]$lines.Add("")
foreach ($group in @($groups.ToArray() | Where-Object { $_.safeSwapCandidate })) {
    [void]$lines.Add(('- `0x{0:X}`: {1}' -f [int]$group.skySubfileSize, ((@($group.members) | ForEach-Object { $_.displayName }) -join ", ")))
}
if (@($groups.ToArray() | Where-Object { $_.safeSwapCandidate }).Count -eq 0) {
    [void]$lines.Add("- No same-size groups found in the currently verified catalog.")
}

[void]$lines.Add("")
[void]$lines.Add("## Whole-Subfile Plan-Only Commands")
[void]$lines.Add("")
foreach ($test in @($suggestedWholeSubfilePlans.ToArray() | Select-Object -First 20)) {
    [void]$lines.Add(('- {0} <- {1}: `{2}`' -f $test.targetName, $test.donorName, $test.command))
}
if ($suggestedWholeSubfilePlans.Count -gt 20) {
    [void]$lines.Add(('- {0} more plan commands are in `{1}`.' -f ($suggestedWholeSubfilePlans.Count - 20), $resolvedOutPath))
}
elseif ($suggestedWholeSubfilePlans.Count -eq 0) {
    [void]$lines.Add("- No same-size whole-subfile plan commands yet.")
}

$lines | Set-Content -LiteralPath $resolvedMarkdownPath -Encoding UTF8

Write-Host "Wrote skybox catalog JSON to $resolvedOutPath"
Write-Host "Wrote skybox catalog report to $resolvedMarkdownPath"
Write-Host ("Cataloged {0}/{1} levels; same-size groups: {2}; whole-subfile plan pairs: {3}" -f $result.summary.catalogedLevels, $result.summary.totalLevels, $result.summary.sameSizeGroups, $result.summary.suggestedWholeSubfilePlans)
