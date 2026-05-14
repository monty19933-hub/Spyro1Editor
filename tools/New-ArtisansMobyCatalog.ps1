param(
    [string]$RamPath = ".\artisans-before-clean.bin",
    [string]$OutJsonPath = ".\artisans-moby-catalog.json",
    [string]$OutMarkdownPath = ".\artisans-moby-catalog.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function Get-GemInfo([int]$Byte36, [int]$Byte4F) {
    if ($Byte36 -eq 0x53 -and $Byte4F -eq 0x01) {
        return [pscustomobject]@{ label = "Red gem (1)"; kind = "red 1-gem collectible"; color = "#E74C3C"; confidence = "signature-inferred"; value = 1 }
    }
    if ($Byte36 -eq 0x54 -and $Byte4F -eq 0x02) {
        return [pscustomobject]@{ label = "Green gem (2)"; kind = "green 2-gem collectible"; color = "#2ECC71"; confidence = "signature-inferred"; value = 2 }
    }
    if ($Byte36 -eq 0x55 -and $Byte4F -eq 0x03) {
        return [pscustomobject]@{ label = "Blue gem (5)"; kind = "blue 5-gem collectible"; color = "#3498DB"; confidence = "signature-inferred"; value = 5 }
    }
    if ($Byte36 -eq 0x56 -and $Byte4F -eq 0x04) {
        return [pscustomobject]@{ label = "Yellow gem (10)"; kind = "yellow 10-gem collectible"; color = "#F1C40F"; confidence = "signature-inferred"; value = 10 }
    }
    if ($Byte36 -eq 0x57 -and $Byte4F -eq 0x05) {
        return [pscustomobject]@{ label = "Purple gem (25)"; kind = "purple 25-gem collectible"; color = "#9B59B6"; confidence = "signature-inferred"; value = 25 }
    }
    return $null
}

function Get-RewardColorName([int]$Byte53) {
    switch ($Byte53) {
        0x53 { return "red" }
        0x54 { return "green" }
        0x55 { return "blue" }
        0x56 { return "yellow" }
        0x57 { return "purple" }
        default { return "" }
    }
}

function Get-RewardColor([string]$Name) {
    switch ($Name) {
        "red" { return "#E74C3C" }
        "green" { return "#2ECC71" }
        "blue" { return "#3498DB" }
        "yellow" { return "#F1C40F" }
        "purple" { return "#9B59B6" }
        default { return "#F4D44D" }
    }
}

function New-MobyEntry($Record) {
    $label = ""
    $kind = ""
    $confidence = "unknown"
    $color = "#5DADE2"
    $evidence = "Artisans live RAM capture; visual identity not confirmed yet."
    $behaviorNote = ""

    $gem = Get-GemInfo $Record.byte36 $Record.byte4F
    if ($Record.type -eq 0x18 -and $null -ne $gem) {
        $label = $gem.label
        $kind = $gem.kind
        $confidence = $gem.confidence
        $color = $gem.color
        $evidence = "Standalone pickup bytes +0x36/+0x4F match the Stone Hill live-validated gem signature."
    }
    elseif ($Record.type -eq 0x30) {
        $dragonSlot = 1 + @($script:dragonIndexes).IndexOf($Record.index)
        $label = "Dragon candidate " + $dragonSlot.ToString() + " (name pending)"
        $kind = "dragon/NPC candidate"
        $confidence = "type-inferred"
        $color = "#B78CFF"
        $evidence = "Type 0x30 matches the dragon/NPC family used by Stone Hill. Artisans dragon names still need visual confirmation."
        $behaviorNote = "Likely one of Artisans' rescued-dragon records. Link pedestal/helper records after a focused behavior pass."
    }
    elseif ($Record.type -eq 0x10) {
        $label = "Balloonist/transport NPC candidate"
        $kind = "transport NPC candidate"
        $confidence = "type-inferred"
        $color = "#AED6F1"
        $evidence = "Unique Artisans type 0x10 actor near the home-world transport area; visual name pending."
    }
    elseif ($Record.type -eq 0x3C) {
        $label = "Portal/transport object candidate"
        $kind = "portal or level-entry object candidate"
        $confidence = "type-inferred"
        $color = "#F8C471"
        $evidence = "Unique Artisans type 0x3C object. Needs in-game visual confirmation."
    }
    elseif ($Record.type -eq 0x20) {
        $rewardColor = Get-RewardColorName $Record.byte53
        if ($rewardColor.Length -gt 0) {
            $label = (Get-Culture).TextInfo.ToTitleCase($rewardColor) + " treasure/reward candidate"
            $kind = $rewardColor + " gem/reward object candidate"
            $confidence = "byte-inferred"
            $color = Get-RewardColor $rewardColor
            $evidence = "Type 0x20 record has a gem-like +0x53 color byte. Confirm whether it is a loose gem, container reward, or linked treasure object."
        }
        elseif ($Record.byte52 -eq 0xFF -and $Record.byte4F -eq 0x02) {
            $label = "Linked reward marker candidate"
            $kind = "linked reward/control candidate"
            $confidence = "weak"
            $color = "#F4D44D"
            $evidence = "Type 0x20 record uses nonstandard marker bytes and appears in a tight position sequence. Needs focused visual/collection testing."
        }
        else {
            $label = "Treasure/object candidate"
            $kind = "treasure, container, or scenery candidate"
            $confidence = "unknown"
            $color = "#F4D44D"
            $evidence = "Type 0x20 Artisans record. Needs visual or behavior confirmation."
        }
    }
    elseif ($Record.type -eq 0x00) {
        $label = "Nonvisual control/link record"
        $kind = "nonvisual control or linked helper"
        $confidence = "weak"
        $color = "#8FA6B8"
        $evidence = "Type 0x00 is usually not a visible standalone object. Keep separate until linked behavior is known."
    }
    else {
        $label = "Type " + ("0x{0:X2}" -f $Record.type) + " object candidate"
        $kind = "unidentified Artisans object"
    }

    switch ($Record.index) {
        46 {
            $label = "Balloon"
            $kind = "transport balloon / scenery prop"
            $confidence = "live-observed"
            $color = "#F8C471"
            $evidence = "Observed in DuckStation Artisans identity Batch 01 after moving T46 to the open test point."
            $behaviorNote = "Visible balloon object. This is not the balloonist NPC."
        }
        102 {
            $label = "Skinny Tree"
            $kind = "scenery tree"
            $confidence = "live-observed"
            $color = "#2ECC71"
            $evidence = "Observed in DuckStation Artisans identity Batch 02 after moving T102 to the open test point."
            $behaviorNote = "Previously type-inferred as dragon/NPC because type 0x30 overlaps Stone Hill actor usage; Artisans uses this record as a visible skinny tree prop."
        }
        103 {
            $label = "Tall 2-Ball Tree"
            $kind = "scenery tree"
            $confidence = "live-observed"
            $color = "#2ECC71"
            $evidence = "Observed in DuckStation Artisans identity Batch 03 after moving T103 to the open test point."
            $behaviorNote = "Visible two-ball tree prop."
        }
        104 {
            $label = "Wider Tree"
            $kind = "scenery tree"
            $confidence = "live-observed"
            $color = "#2ECC71"
            $evidence = "Observed in DuckStation Artisans identity Batch 04 after moving T104 to the open test point."
            $behaviorNote = "Visible wider tree prop."
        }
        105 {
            $label = "Invisible special record"
            $kind = "nonvisual or hidden helper"
            $confidence = "live-observed invisible"
            $color = "#8FA6B8"
            $evidence = "Observed in DuckStation Artisans identity Batch 05; moving T105 produced no visible object at the test point."
            $behaviorNote = "Keep separate from confirmed visible tree records until linked behavior is known."
        }
        174 {
            $label = "Sparx-linked control"
            $kind = "Sparx behavior/control record"
            $confidence = "live-observed behavior"
            $color = "#AED6F1"
            $evidence = "Observed in DuckStation Artisans identity Batch 06; Sparx flew to the moved position and then returned to Spyro."
            $behaviorNote = "Moving this record appears to affect Sparx behavior rather than moving a visible balloonist/NPC."
        }
    }

    $treasureGnorcIndexes = @(7, 29, 30, 31, 32, 33)
    $flameChargeChestIndexes = @(50, 51, 52, 58, 59, 77, 78, 79, 80, 83, 173, 175)
    $towerFlagIndexes = @(0, 1, 2, 3, 4, 5)
    $towerSideFlagLongIndexes = @(6, 45, 88)
    $dragonPedestalIndexes = @(89, 90, 92, 142)
    $dragonActorIndexes = @(91, 93, 143)
    $lifeChestIndexes = @(94)
    $towerFlagTopIndexes = @(95, 96)
    $ceilingLampCandidateIndexes = @(109)
    $sheepSnapbackIndexes = @(110, 111, 112, 113, 114)
    $tulipFlowerClusterIndexes = @(97, 98, 99, 101, 108, 118, 120, 123, 124, 125, 127, 131, 132, 139)
    $tulipFlowerSingleIndexes = @(100, 106, 107, 119, 126, 140)
    $grassIndexes = @(121, 122, 128, 129, 130)
    $padTopRightFacingSfIndexes = @(133)
    $padTopLeftFacingSfIndexes = @(134)
    $padBottomLeftFacingFsIndexes = @(135)
    $padBottomMiddleFacingSfIndexes = @(136)
    $padBottomRightFacingSfIndexes = @(137)
    $liveUnavailableIndexes = @(176, 177, 178, 179, 180, 181, 182, 183)

    if ($treasureGnorcIndexes -contains $Record.index) {
        $label = "Treasure Gnorc (3-hit)"
        $kind = "enemy / treasure thief"
        $confidence = "live-observed"
        $color = "#F39C12"
        $evidence = "Observed in DuckStation Artisans identity Batches 07-08; moved records appeared as Treasure Gnorc enemies that took three hits."
        $behaviorNote = "Treat as an active enemy object, not a loose gem or passive reward marker."
    }
    elseif ($flameChargeChestIndexes -contains $Record.index) {
        $label = "Flame/Charge Chest"
        $kind = "breakable treasure chest"
        $confidence = "live-observed"
        $color = "#D35400"
        $evidence = "Observed in DuckStation Artisans identity Batches 09-12; moved records appeared as Flame/Charge chests."
        $behaviorNote = "Breakable treasure object. Confirm contained reward value later with focused collection tests."
    }
    elseif ($towerFlagIndexes -contains $Record.index) {
        $label = "Tower Flag"
        $kind = "scenery flag"
        $confidence = "live-observed"
        $color = "#E74C3C"
        $evidence = "Observed in DuckStation Artisans identity Batches 16-17; moved records appeared as tower flags."
        $behaviorNote = "Visible scenery prop."
    }
    elseif ($towerSideFlagLongIndexes -contains $Record.index) {
        $label = "Tower Side Flag Long"
        $kind = "scenery flag"
        $confidence = "live-observed"
        $color = "#E74C3C"
        $evidence = "Observed in DuckStation Artisans identity Batch 18; moved records appeared as long side flags."
        $behaviorNote = "Visible scenery prop."
    }
    elseif ($dragonPedestalIndexes -contains $Record.index) {
        $label = "Dragon Pedestal"
        $kind = "dragon pedestal / linked rescue prop"
        $confidence = "live-observed"
        $color = "#B78CFF"
        $evidence = "Observed in DuckStation Artisans focused identity retest; moved record appeared as a dragon pedestal."
        $behaviorNote = "Likely linked with nearby dragon actor/helper records. Move with its dragon group once Artisans behavior links are mapped."
    }
    elseif ($dragonActorIndexes -contains $Record.index) {
        $label = "Dragon"
        $kind = "dragon actor/model"
        $confidence = "live-observed"
        $color = "#B78CFF"
        $evidence = "Observed in DuckStation Artisans focused identity retest; moved record appeared as a dragon."
        $behaviorNote = "Actual visible dragon actor. Link to the matching pedestal/control records during the Artisans behavior-link pass."
    }
    elseif ($lifeChestIndexes -contains $Record.index) {
        $label = "Life Chest"
        $kind = "extra life chest"
        $confidence = "live-observed"
        $color = "#DDE6E8"
        $evidence = "Observed in DuckStation Artisans solo identity tester; moved record appeared as a Life Chest."
        $behaviorNote = "Breakable/collectible reward chest. Confirm internal reward behavior later if needed."
    }
    elseif ($towerFlagTopIndexes -contains $Record.index) {
        $label = "Tower Flag (Top)"
        $kind = "scenery flag"
        $confidence = "live-observed"
        $color = "#E74C3C"
        $evidence = "Observed in DuckStation Artisans solo identity tester; moved record appeared as a top tower flag."
        $behaviorNote = "Visible scenery prop."
    }
    elseif ($ceilingLampCandidateIndexes -contains $Record.index) {
        $label = "Ceiling Lamp?"
        $kind = "scenery lamp candidate"
        $confidence = "live-observed tentative"
        $color = "#F7DC6F"
        $evidence = "Observed in DuckStation Artisans solo identity tester; user reported this looked like a Ceiling Lamp, with uncertainty."
        $behaviorNote = "Tentative visual identity. Recheck later if this object appears in a clearer camera position."
    }
    elseif ($sheepSnapbackIndexes -contains $Record.index) {
        $label = "Sheep"
        $kind = "fodder sheep / moving critter"
        $confidence = "live-observed behavior"
        $color = "#F4F6F7"
        $evidence = "Observed in DuckStation Artisans solo identity tester; moved record appeared as a sheep."
        $behaviorNote = "The sheep bounced back and forth from its original location during live movement, suggesting extra behavior/path state or a linked home position."
    }
    elseif ($tulipFlowerClusterIndexes -contains $Record.index) {
        $label = "Tulip Flowers"
        $kind = "scenery flowers"
        $confidence = "live-observed"
        $color = "#FF66B3"
        $evidence = "Observed in DuckStation Artisans solo identity tester; moved record appeared as a tulip flower cluster."
        $behaviorNote = "Visible scenery prop."
    }
    elseif ($tulipFlowerSingleIndexes -contains $Record.index) {
        $label = "Tulip Flower (single)"
        $kind = "scenery flowers"
        $confidence = "live-observed"
        $color = "#FF66B3"
        $evidence = "Observed in DuckStation Artisans solo identity tester; moved record appeared as a single tulip flower."
        $behaviorNote = "Visible scenery prop."
    }
    elseif ($grassIndexes -contains $Record.index) {
        $label = "Grass"
        $kind = "scenery grass"
        $confidence = "live-observed"
        $color = "#6CCB5F"
        $evidence = "Observed in DuckStation Artisans solo identity tester; moved record appeared as grass."
        $behaviorNote = "Visible scenery prop."
    }
    elseif ($padTopRightFacingSfIndexes -contains $Record.index) {
        $label = "Pad (Top Right facing SF)"
        $kind = "scenery pad"
        $confidence = "live-observed"
        $color = "#D2B48C"
        $evidence = "Observed in DuckStation Artisans solo identity tester; user reported pad orientation as top right facing SF."
        $behaviorNote = "Visible pad prop. Orientation naming is user-observed and may need a later naming cleanup."
    }
    elseif ($padTopLeftFacingSfIndexes -contains $Record.index) {
        $label = "Pad (Top Left facing SF)"
        $kind = "scenery pad"
        $confidence = "live-observed"
        $color = "#D2B48C"
        $evidence = "Observed in DuckStation Artisans solo identity tester; user reported pad orientation as top left facing SF."
        $behaviorNote = "Visible pad prop. Orientation naming is user-observed and may need a later naming cleanup."
    }
    elseif ($padBottomLeftFacingFsIndexes -contains $Record.index) {
        $label = "Pad (Bottom Left facing FS)"
        $kind = "scenery pad"
        $confidence = "live-observed"
        $color = "#D2B48C"
        $evidence = "Observed in DuckStation Artisans solo identity tester; user reported pad orientation as bottom left facing FS."
        $behaviorNote = "Visible pad prop. Orientation naming is user-observed and may need a later naming cleanup."
    }
    elseif ($padBottomMiddleFacingSfIndexes -contains $Record.index) {
        $label = "Pad (Bottom Middle facing SF)"
        $kind = "scenery pad"
        $confidence = "live-observed"
        $color = "#D2B48C"
        $evidence = "Observed in DuckStation Artisans solo identity tester; user reported pad orientation as bottom middle facing SF."
        $behaviorNote = "Visible pad prop. Orientation naming is user-observed and may need a later naming cleanup."
    }
    elseif ($padBottomRightFacingSfIndexes -contains $Record.index) {
        $label = "Pad (Bottom Right facing SF)"
        $kind = "scenery pad"
        $confidence = "live-observed"
        $color = "#D2B48C"
        $evidence = "Observed in DuckStation Artisans solo identity tester; user reported pad orientation as bottom right facing SF."
        $behaviorNote = "Visible pad prop. Orientation naming is user-observed and may need a later naming cleanup."
    }
    elseif ($liveUnavailableIndexes -contains $Record.index) {
        $label = "Inactive linked reward marker"
        $kind = "not live-plausible in current state"
        $confidence = "live-unavailable"
        $color = "#8FA6B8"
        $evidence = "DuckStation live identity Batches 13-15 failed with 'Moby index is not currently a plausible live moby record' for this linked marker range."
        $behaviorNote = "Skip normal live move tests for now. These may be inactive, ephemeral, or not represented as normal live moby records in the current Artisans state."
    }

    [ordered]@{
        index = $Record.index
        trueIndex = $Record.index
        typeHex = ("0x{0:X2}" -f $Record.type)
        stateHex = ("0x{0:X2}" -f $Record.state)
        displayTargetLabel = $label
        candidateKind = $kind
        confidence = $confidence
        color = $color
        x = $Record.x
        y = $Record.y
        z = $Record.z
        specialDataPointer = $Record.special
        sourceByte36Hex = ("0x{0:X2}" -f $Record.byte36)
        sourceByte4FHex = ("0x{0:X2}" -f $Record.byte4F)
        flag52Hex = ("0x{0:X2}" -f $Record.byte52)
        flag53Hex = ("0x{0:X2}" -f $Record.byte53)
        evidence = $evidence
        behaviorNote = $behaviorNote
        patchStatus = "runtime-captured; source patcher pending"
        patchLead = "Captured from Artisans live RAM table. Permanent source table mapping is not wired yet."
        patchPriority = 0
    }
}

$resolvedRam = Resolve-Path -LiteralPath $RamPath -ErrorAction Stop
$ram = [IO.File]::ReadAllBytes($resolvedRam.Path)
$levelId = [BitConverter]::ToUInt32($ram, 0x758B4)
$pointer = [BitConverter]::ToUInt32($ram, 0x75828)
$dynamicPointer = [BitConverter]::ToUInt32($ram, 0x7573C)
$start = [int]($pointer -band 0x001FFFFF)
$dynamicStart = [int]($dynamicPointer -band 0x001FFFFF)
if ($start -lt 0 -or $start + 0x58 -gt $ram.Length) {
    throw "Artisans moby table pointer is not usable: $('0x{0:X8}' -f $pointer)"
}

$count = 512
if ($dynamicStart -gt $start -and $dynamicStart -le $ram.Length -and (($dynamicStart - $start) % 0x58) -eq 0) {
    $count = [Math]::Min(512, ($dynamicStart - $start) / 0x58)
}

$records = @()
for ($i = 0; $i -lt $count; $i++) {
    $offset = $start + ($i * 0x58)
    if ($offset + 0x58 -gt $ram.Length) { break }
    $records += [pscustomobject]@{
        index = $i
        offset = $offset
        runtimeAddress = ("0x{0:X8}" -f (0x80000000 + $offset))
        type = [int]$ram[$offset + 0x50]
        state = [int]$ram[$offset + 0x51]
        x = [Math]::Round([BitConverter]::ToInt32($ram, $offset + 0x0C) / 16.0, 2)
        y = [Math]::Round([BitConverter]::ToInt32($ram, $offset + 0x10) / 16.0, 2)
        z = [Math]::Round([BitConverter]::ToInt32($ram, $offset + 0x14) / 16.0, 2)
        special = ("0x{0:X8}" -f [BitConverter]::ToUInt32($ram, $offset + 0x08))
        byte36 = [int]$ram[$offset + 0x36]
        byte4F = [int]$ram[$offset + 0x4F]
        byte52 = [int]$ram[$offset + 0x52]
        byte53 = [int]$ram[$offset + 0x53]
    }
}

$script:dragonIndexes = @($records | Where-Object { $_.type -eq 0x30 } | Select-Object -ExpandProperty index)
$mobys = @($records | ForEach-Object { New-MobyEntry $_ })

$typeSummary = @($records |
    Group-Object { "0x{0:X2}" -f $_.type } |
    Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, Name |
    ForEach-Object {
        [ordered]@{
            typeHex = $_.Name
            count = $_.Count
            sampleIndexes = @($_.Group | Select-Object -First 12 -ExpandProperty index)
        }
    })

$root = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "New-ArtisansMobyCatalog.ps1"
    levelName = "Artisans"
    levelId = ("0x{0:X2}" -f $levelId)
    source = [ordered]@{
        ramPath = $resolvedRam.Path
        mobyPointer = ("0x{0:X8}" -f $pointer)
        dynamicPointer = ("0x{0:X8}" -f $dynamicPointer)
        stride = "0x58"
        note = "First Artisans catalog pass. Strong gem names come from Stone Hill-confirmed source-byte signatures; other names are conservative candidates."
    }
    counts = [ordered]@{
        records = $records.Count
        named = $mobys.Count
        redGemSignature = @($mobys | Where-Object { $_.candidateKind -eq "red 1-gem collectible" }).Count
        greenGemSignature = @($mobys | Where-Object { $_.candidateKind -eq "green 2-gem collectible" }).Count
        dragons = @($mobys | Where-Object { $_.candidateKind -eq "dragon/NPC candidate" }).Count
    }
    typeSummary = $typeSummary
    mobys = $mobys
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$resolvedMd = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
$root | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Artisans Moby Catalog")
[void]$lines.Add("")
[void]$lines.Add("- Records: $($records.Count)")
[void]$lines.Add("- Red gem signatures: $($root.counts.redGemSignature)")
[void]$lines.Add("- Green gem signatures: $($root.counts.greenGemSignature)")
[void]$lines.Add("- Dragon candidates: $($root.counts.dragons)")
[void]$lines.Add("")
[void]$lines.Add("## Types")
[void]$lines.Add("")
[void]$lines.Add("| Type | Count | Sample indexes |")
[void]$lines.Add("| --- | ---: | --- |")
foreach ($type in $typeSummary) {
    [void]$lines.Add("| $($type.typeHex) | $($type.count) | $($type.sampleIndexes -join ', ') |")
}
[void]$lines.Add("")
[void]$lines.Add("## Records")
[void]$lines.Add("")
[void]$lines.Add("| T | Type | Label | Kind | Confidence | X | Y | Z | Bytes |")
[void]$lines.Add("| ---: | --- | --- | --- | --- | ---: | ---: | ---: | --- |")
foreach ($moby in $mobys) {
    $bytes = "+36 $($moby.sourceByte36Hex), +4F $($moby.sourceByte4FHex), +52 $($moby.flag52Hex), +53 $($moby.flag53Hex)"
    [void]$lines.Add("| $($moby.trueIndex) | $($moby.typeHex) | $($moby.displayTargetLabel) | $($moby.candidateKind) | $($moby.confidence) | $($moby.x) | $($moby.y) | $($moby.z) | $bytes |")
}
$lines | Set-Content -LiteralPath $resolvedMd -Encoding UTF8

Write-Host "Wrote Artisans moby catalog to $resolvedJson"
Write-Host "Wrote Artisans moby catalog report to $resolvedMd"
