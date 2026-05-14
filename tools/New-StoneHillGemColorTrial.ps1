param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$InventoryPath = ".\stonehill-full-moby-inventory.json",
    [string]$OutJsonPath = ".\stonehill-gem-color-trial.json",
    [string]$OutEditsPath = ".\stonehill-gem-color-trial-edits.json",
    [string]$OutMarkdownPath = ".\stonehill-gem-color-trial.md",
    [string]$OutBinPath = ".\Spyro the Dragon (USA)-gem-color-trial-field.bin",
    [string]$OutPlanPath = ".\stonehill-gem-color-trial-patchplan.json",
    [double]$StartX = 5400,
    [double]$StartY = 9286,
    [double]$StartZ = 1280.125,
    [double]$Spacing = 110,
    [switch]$BuildBin,
    [switch]$PlanOnly,
    [switch]$SwapProof
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37
$SourceTableOffset = [Convert]::ToInt64("D72B38", 16)
$RecordStride = 0x58
$KnownRedTrueIndexes = @(79, 129, 139)

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

function Escape-Markdown([string]$Text) {
    if ($null -eq $Text) { return "" }
    return $Text.Replace("|", "\|")
}

function Convert-BytesToHex([byte[]]$Bytes) {
    return -join ($Bytes | ForEach-Object { "{0:X2}" -f $_ })
}

function Convert-DoubleToRaw([double]$Value) {
    return [int][Math]::Round($Value * 16.0)
}

function New-Vector([double]$X, [double]$Y, [double]$Z) {
    return [ordered]@{ x = [double]$X; y = [double]$Y; z = [double]$Z }
}

function New-RawVector([double]$X, [double]$Y, [double]$Z) {
    return [ordered]@{ x = Convert-DoubleToRaw $X; y = Convert-DoubleToRaw $Y; z = Convert-DoubleToRaw $Z }
}

function Test-PvdAt([System.IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    $offset = (16 * $SectorSize) + $UserOffset
    if ($Stream.Length -lt ($offset + 2048)) { return $null }
    $buffer = New-Object byte[] 2048
    $Stream.Position = $offset
    [void]$Stream.Read($buffer, 0, 2048)
    $signature = [System.Text.Encoding]::ASCII.GetString($buffer, 1, 5)
    if ($buffer[0] -ne 1 -or $signature -ne "CD001") { return $null }
    return [ordered]@{
        sectorSize = $SectorSize
        userOffset = $UserOffset
        userSize = 2048
        description = $(if ($SectorSize -eq 2352) { "Raw PS1 BIN/CUE track (2352-byte sectors)" } else { "ISO image (2048-byte sectors)" })
    }
}

function Detect-DiscLayout([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
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

function Read-WadBytes([System.IO.FileStream]$Stream, $Layout, [int64]$Offset, [int]$Length) {
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

function Get-RecordBytes([System.IO.FileStream]$Stream, $Layout, [int]$TrueIndex) {
    return Read-WadBytes $Stream $Layout ($SourceTableOffset + ([int64]$TrueIndex * $RecordStride)) $RecordStride
}

function Get-HexSlice([byte[]]$Bytes, [int]$Start, [int]$Length) {
    $slice = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Start, $slice, 0, $Length)
    return Convert-BytesToHex $slice
}

function New-Edit($Record, [double]$X, [double]$Y, [double]$Z, [string]$Label) {
    $trueIndex = [int](Get-Field $Record "trueIndex" -1)
    $original = New-Vector ([double](Get-Field $Record "x" 0)) ([double](Get-Field $Record "y" 0)) ([double](Get-Field $Record "z" 0))
    $edited = New-Vector $X $Y $Z
    $rawOriginal = New-RawVector $original.x $original.y $original.z
    $rawEdited = New-RawVector $X $Y $Z
    return [ordered]@{
        index = $trueIndex
        trueIndex = $trueIndex
        legacyIndex = $(if (-not [string]::IsNullOrWhiteSpace([string](Get-Field $Record "legacyAlias" ""))) { [int](([string](Get-Field $Record "legacyAlias" "")).TrimStart("L")) } else { $null })
        label = $Label
        typeHex = [string](Get-Field $Record "typeHex" "")
        stateHex = [string](Get-Field $Record "stateHex" "")
        runtimeAddress = [string](Get-Field $Record "runtimeAddress" "")
        specialDataPointer = [string](Get-Field $Record "specialDataPointer" "")
        flag4AHex = [string](Get-Field $Record "flag52Hex" "")
        flag4BHex = [string](Get-Field $Record "flag53Hex" "")
        patchStatus = "loader-table-patchable"
        patchLead = "WAD entry 12, true record $trueIndex, XYZ +0x0C/+0x10/+0x14"
        behaviorNote = "Gem color/value trial row. Observe left-to-right in game and compare HUD gem count."
        specialDataNote = ""
        original = $original
        edited = $edited
        rawOriginal = $rawOriginal
        rawEdited = $rawEdited
        rawDelta = [ordered]@{
            x = [int]$rawEdited.x - [int]$rawOriginal.x
            y = [int]$rawEdited.y - [int]$rawOriginal.y
            z = [int]$rawEdited.z - [int]$rawOriginal.z
        }
    }
}

$imagePath = Resolve-WorkspacePath $ImagePath
$inventoryPath = Resolve-WorkspacePath $InventoryPath

if ($SwapProof) {
    if (-not $PSBoundParameters.ContainsKey("OutJsonPath")) { $OutJsonPath = ".\stonehill-gem-swap-proof.json" }
    if (-not $PSBoundParameters.ContainsKey("OutEditsPath")) { $OutEditsPath = ".\stonehill-gem-swap-proof-edits.json" }
    if (-not $PSBoundParameters.ContainsKey("OutMarkdownPath")) { $OutMarkdownPath = ".\stonehill-gem-swap-proof.md" }
    if (-not $PSBoundParameters.ContainsKey("OutBinPath")) { $OutBinPath = ".\Spyro the Dragon (USA)-gem-swap-proof.bin" }
    if (-not $PSBoundParameters.ContainsKey("OutPlanPath")) { $OutPlanPath = ".\stonehill-gem-swap-proof-patchplan.json" }
    if (-not $PSBoundParameters.ContainsKey("Spacing")) { $Spacing = 360 }
}

$outJsonPath = Resolve-WorkspacePath $OutJsonPath
$outEditsPath = Resolve-WorkspacePath $OutEditsPath
$outMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath
$outBinPath = Resolve-WorkspacePath $OutBinPath
$outPlanPath = Resolve-WorkspacePath $OutPlanPath

if (-not (Test-Path -LiteralPath $imagePath)) { throw "Missing image: $imagePath" }
if (-not (Test-Path -LiteralPath $inventoryPath)) { throw "Missing inventory: $inventoryPath" }

$layout = Detect-DiscLayout $imagePath
$inventory = Get-Content -Raw -LiteralPath $inventoryPath | ConvertFrom-Json
$mobys = @(Get-ArrayField $inventory "mobys")
$smallPickups = @($mobys | Where-Object {
    [string](Get-Field $_ "typeHex" "") -eq "0x18" -and
    [string](Get-Field $_ "specialDataPointer" "") -eq "0x00000000" -and
    [string](Get-Field $_ "flag52Hex" "") -eq "0x40" -and
    [string](Get-Field $_ "flag53Hex" "") -eq "0xFF"
})

$stream = [System.IO.File]::OpenRead($imagePath)
try {
    $rows = New-Object System.Collections.ArrayList
    foreach ($record in $smallPickups) {
        $trueIndex = [int](Get-Field $record "trueIndex" -1)
        $bytes = Get-RecordBytes $stream $layout $trueIndex
        $sig36 = Get-HexSlice $bytes 0x36 0x22
        [void]$rows.Add([ordered]@{
            trueIndex = $trueIndex
            id = "T$trueIndex"
            legacyAlias = [string](Get-Field $record "legacyAlias" "")
            label = [string](Get-Field $record "displayTargetLabel" "")
            x = [double](Get-Field $record "x" 0)
            y = [double](Get-Field $record "y" 0)
            z = [double](Get-Field $record "z" 0)
            sourceSignature36 = $sig36
            sourceByte36Hex = "0x{0:X2}" -f [int]$bytes[0x36]
            sourceByte44Hex = "0x{0:X2}" -f [int]$bytes[0x44]
            sourceByte45Hex = "0x{0:X2}" -f [int]$bytes[0x45]
            sourceByte4FHex = "0x{0:X2}" -f [int]$bytes[0x4F]
            sourceByte50Hex = "0x{0:X2}" -f [int]$bytes[0x50]
            sourceByte52Hex = "0x{0:X2}" -f [int]$bytes[0x52]
            sourceByte53Hex = "0x{0:X2}" -f [int]$bytes[0x53]
            suspectedValue = $(if ([int]$bytes[0x50] -eq 0x18 -and [int]$bytes[0x52] -eq 0x40 -and [int]$bytes[0x4F] -in @(1,2,5,10,25)) { [int]$bytes[0x4F] } else { $null })
            knownValue = $(if ($trueIndex -in $KnownRedTrueIndexes) { 1 } else { $null })
            record = $record
        })
    }
}
finally {
    $stream.Dispose()
}

$groups = @($rows |
    Group-Object { Get-Field $_ "sourceSignature36" "" } |
    Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, Name |
    ForEach-Object {
        $sample = $_.Group | Select-Object -First 1
        [ordered]@{
            groupId = ""
            count = [int]$_.Count
            representativeTrueIndex = [int](Get-Field $sample "trueIndex" -1)
            representativeId = [string](Get-Field $sample "id" "")
            sampleIds = @($_.Group | Select-Object -First 16 | ForEach-Object { [string](Get-Field $_ "id" "") + [string](Get-Field $_ "legacyAlias" "") })
            knownValue = $(if (@($_.Group | Where-Object { $null -ne (Get-Field $_ "knownValue" $null) }).Count -gt 0) { 1 } else { $null })
            suspectedValue = Get-Field $sample "suspectedValue" $null
            sourceByte36Hex = [string](Get-Field $sample "sourceByte36Hex" "")
            sourceByte44Hex = [string](Get-Field $sample "sourceByte44Hex" "")
            sourceByte45Hex = [string](Get-Field $sample "sourceByte45Hex" "")
            sourceByte4FHex = [string](Get-Field $sample "sourceByte4FHex" "")
            sourceByte50Hex = [string](Get-Field $sample "sourceByte50Hex" "")
            sourceByte52Hex = [string](Get-Field $sample "sourceByte52Hex" "")
            sourceByte53Hex = [string](Get-Field $sample "sourceByte53Hex" "")
            sourceSignature36 = [string](Get-Field $sample "sourceSignature36" "")
        }
    })

for ($i = 0; $i -lt $groups.Count; $i++) {
    $groups[$i].groupId = "G$($i + 1)"
}

$trialRows = New-Object System.Collections.ArrayList
if ($SwapProof) {
    foreach ($wantedId in @(79, 84)) {
        $group = $groups | Where-Object { [int](Get-Field $_ "representativeTrueIndex" -1) -eq $wantedId } | Select-Object -First 1
        if ($null -ne $group) {
            [void]$trialRows.Add($group)
        }
    }
}
else {
    $representativeIds = New-Object System.Collections.Generic.HashSet[int]
    foreach ($group in $groups) {
        $id = [int](Get-Field $group "representativeTrueIndex" -1)
        if ($id -ge 0 -and $representativeIds.Add($id)) {
            [void]$trialRows.Add($group)
        }
    }
}

$swapRecord = $mobys | Where-Object { [int](Get-Field $_ "trueIndex" -1) -eq 129 } | Select-Object -First 1
$customBytePatches = @(
    [ordered]@{
        trueIndex = 129
        id = "T129"
        purpose = "Patch confirmed red gem toward suspected green/value-2 signature"
        sourceByteOffset = "0x36"
        originalHex = "0x53"
        patchedHex = "0x54"
        wadOffset = "0x{0:X}" -f ($SourceTableOffset + ([int64]129 * $RecordStride) + 0x36)
        imageOffset = Convert-DiscFileOffsetToImageOffset $layout $WadLba ($SourceTableOffset + ([int64]129 * $RecordStride) + 0x36)
    },
    [ordered]@{
        trueIndex = 129
        id = "T129"
        purpose = "Patch confirmed red gem value byte from 1 to suspected 2"
        sourceByteOffset = "0x4F"
        originalHex = "0x01"
        patchedHex = "0x02"
        wadOffset = "0x{0:X}" -f ($SourceTableOffset + ([int64]129 * $RecordStride) + 0x4F)
        imageOffset = Convert-DiscFileOffsetToImageOffset $layout $WadLba ($SourceTableOffset + ([int64]129 * $RecordStride) + 0x4F)
    }
)

$edits = New-Object System.Collections.ArrayList
$testPlacements = New-Object System.Collections.ArrayList
$slot = 0
foreach ($group in $trialRows) {
    $trueIndex = [int](Get-Field $group "representativeTrueIndex" -1)
    $record = $mobys | Where-Object { [int](Get-Field $_ "trueIndex" -1) -eq $trueIndex } | Select-Object -First 1
    if ($null -eq $record) { continue }
    $x = $StartX + ($Spacing * $slot)
    $label = "$($group.groupId) gem signature test"
    if ($null -ne (Get-Field $group "knownValue" $null)) { $label = "$($group.groupId) known red 1-gem control" }
    elseif ($null -ne (Get-Field $group "suspectedValue" $null)) { $label = "$($group.groupId) suspected value $([int](Get-Field $group "suspectedValue" 0)) gem" }
    elseif ([string](Get-Field $group "sourceByte36Hex" "") -eq "0xAD") { $label = "$($group.groupId) key/non-gem signature candidate" }
    [void]$edits.Add((New-Edit $record $x $StartY $StartZ $label))
    [void]$testPlacements.Add([ordered]@{
        slot = $slot + 1
        leftToRight = $slot + 1
        groupId = [string](Get-Field $group "groupId" "")
        trueIndex = $trueIndex
        id = "T$trueIndex"
        expected = $label
        x = $x
        y = $StartY
        z = $StartZ
        sourceByte36Hex = [string](Get-Field $group "sourceByte36Hex" "")
        sourceByte4FHex = [string](Get-Field $group "sourceByte4FHex" "")
        suspectedValue = Get-Field $group "suspectedValue" $null
        knownValue = Get-Field $group "knownValue" $null
    })
    $slot++
}

if ($null -ne $swapRecord) {
    $x = $StartX + ($Spacing * $slot)
    [void]$edits.Add((New-Edit $swapRecord $x $StartY $StartZ "SWAP T129 red-to-suspected-value-2 byte patch"))
    [void]$testPlacements.Add([ordered]@{
        slot = $slot + 1
        leftToRight = $slot + 1
        groupId = "SWAP"
        trueIndex = 129
        id = "T129"
        expected = "Confirmed red gem with +0x36 0x53->0x54 and +0x4F 0x01->0x02"
        x = $x
        y = $StartY
        z = $StartZ
        sourceByte36Hex = "0x53->0x54"
        sourceByte4FHex = "0x01->0x02"
        suspectedValue = 2
        knownValue = 1
    })
}

$editsRoot = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    editor = "New-StoneHillGemColorTrial"
    levelName = "Stone Hill"
    note = "Temporary gem color/value trial row. Use with tools/New-StoneHillGemColorTrial.ps1 -BuildBin so the SWAP byte patches are applied too."
    editCount = $edits.Count
    edits = @($edits.ToArray())
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    mode = $(if ($SwapProof) { "swap-proof" } else { "signature-row" })
    sourceImage = $imagePath
    inventoryPath = $inventoryPath
    outputEditsPath = $outEditsPath
    outputBinPath = $(if ($BuildBin -and -not $PlanOnly) { $outBinPath } else { $null })
    outputPlanPath = $outPlanPath
    discLayout = $layout
    hypothesis = "Standalone 0x18 gem color/value appears to use source bytes around +0x36 and +0x4F. Confirmed red gems have +0x36=0x53 and +0x4F=0x01; the common suspected value-2 group has +0x36=0x54 and +0x4F=0x02."
    smallPickupRecordCount = $smallPickups.Count
    signatureGroupCount = $groups.Count
    groups = $groups
    testPlacements = @($testPlacements.ToArray())
    customBytePatches = $customBytePatches
}

$editsRoot | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outEditsPath -Encoding UTF8
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outJsonPath -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
$title = if ($SwapProof) { "Stone Hill Gem Swap Proof" } else { "Stone Hill Gem Color Trial" }
[void]$lines.Add("# $title")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($result.generatedAt)")
[void]$lines.Add("")
if ($SwapProof) {
    [void]$lines.Add("This proof moves only three objects into a clear row: a confirmed red 1-gem control, a natural green/value-2 candidate, and confirmed red gem T129 patched to the suspected green/value-2 bytes.")
}
else {
    [void]$lines.Add("This trial moves one representative from each standalone ``0x18`` small-pickup source signature into a left-to-right row. It also includes one controlled byte-swap test on confirmed red gem T129.")
}
[void]$lines.Add("")
[void]$lines.Add("## Current Hypothesis")
[void]$lines.Add("")
[void]$lines.Add("- Confirmed red 1-gems: source `+0x36 = 0x53`, `+0x4F = 0x01`.")
[void]$lines.Add("- Common suspected green/value-2 group: source `+0x36 = 0x54`, `+0x4F = 0x02`.")
[void]$lines.Add("- The odd `+0x36 = 0xAD` signature is now a key/non-gem candidate based on the first trial observation.")
[void]$lines.Add("- The SWAP slot patches T129 from the red bytes to those suspected value-2 bytes. If it turns green and gives 2 gems, we have a color/value edit proof.")
[void]$lines.Add("")
[void]$lines.Add("## First Wide-Row Observation")
[void]$lines.Add("")
[void]$lines.Add("The first wide row was observed as: Green Gem, Key, Green, Red, Red, Red, with the last two inside geometry. Because the final slots were hidden and the visual order may not have matched strict X-order, this compact rebuild keeps the same data tests but shortens the row so every slot should be visible.")
[void]$lines.Add("")
[void]$lines.Add("## In-Game Row")
[void]$lines.Add("")
[void]$lines.Add("| Slot | ID | Expected | XYZ | +0x36 | +0x4F |")
[void]$lines.Add("| ---: | --- | --- | --- | --- | --- |")
foreach ($placement in @($testPlacements)) {
    $xyz = "{0:0.###}, {1:0.###}, {2:0.###}" -f [double]$placement.x, [double]$placement.y, [double]$placement.z
    [void]$lines.Add("| $($placement.slot) | $($placement.id) | $(Escape-Markdown ([string]$placement.expected)) | $xyz | $($placement.sourceByte36Hex) | $($placement.sourceByte4FHex) |")
}
[void]$lines.Add("")
[void]$lines.Add("## Signature Groups")
[void]$lines.Add("")
[void]$lines.Add("| Group | Count | Representative | Samples | Known | Suspected Value | +0x36 | +0x4F |")
[void]$lines.Add("| --- | ---: | --- | --- | --- | --- | --- | --- |")
foreach ($group in @($groups)) {
    $known = if ($null -ne (Get-Field $group "knownValue" $null)) { [string](Get-Field $group "knownValue" "") } else { "" }
    $suspect = if ($null -ne (Get-Field $group "suspectedValue" $null)) { [string](Get-Field $group "suspectedValue" "") } else { "" }
    [void]$lines.Add("| $($group.groupId) | $($group.count) | $($group.representativeId) | $(Escape-Markdown (@($group.sampleIds) -join ', ')) | $known | $suspect | $($group.sourceByte36Hex) | $($group.sourceByte4FHex) |")
}
[void]$lines.Add("")
[void]$lines.Add("## How To Test")
[void]$lines.Add("")
$runCommand = if ($SwapProof) {
    "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillGemColorTrial.ps1 -SwapProof -BuildBin"
}
else {
    "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillGemColorTrial.ps1 -BuildBin"
}
[void]$lines.Add("1. Run ``$runCommand``.")
$trialCueName = [System.IO.Path]::GetFileName(([System.IO.Path]::ChangeExtension($outBinPath, ".cue")))
[void]$lines.Add("2. Boot ``$trialCueName`` and enter Stone Hill fresh.")
[void]$lines.Add("3. Find the row near the previous test area around X=$StartX, Y=$StartY.")
[void]$lines.Add("4. Note each slot's visible color left-to-right.")
[void]$lines.Add("5. Pick up each visible gem left-to-right and note the HUD gem-count delta.")
$lines | Set-Content -LiteralPath $outMarkdownPath -Encoding UTF8

if ($BuildBin) {
    $exportArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $PSScriptRoot "Export-StoneHillLoaderTablePatchTest.ps1"),
        "-NativeEditsPath", $outEditsPath,
        "-OutPath", $outBinPath,
        "-PlanPath", $outPlanPath
    )
    if ($PlanOnly) { $exportArgs += "-PlanOnly" }
    & powershell.exe @exportArgs
    if ($LASTEXITCODE -ne 0) { throw "Export-StoneHillLoaderTablePatchTest.ps1 failed with exit code $LASTEXITCODE." }

    if (-not $PlanOnly) {
        $stream = [System.IO.File]::Open($outBinPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite)
        try {
            foreach ($patch in @($customBytePatches)) {
                $stream.Position = [int64](Get-Field $patch "imageOffset" 0)
                $byte = [byte][Convert]::ToByte(([string](Get-Field $patch "patchedHex" "0")).Replace("0x", ""), 16)
                $stream.WriteByte($byte)
            }
        }
        finally {
            $stream.Dispose()
        }
    }
}

Write-Host "Gem color trial groups: $($groups.Count)"
Write-Host "Trial edits: $outEditsPath"
Write-Host "Trial report: $outMarkdownPath"
if ($BuildBin -and -not $PlanOnly) {
    Write-Host "Trial BIN: $outBinPath"
    Write-Host "Trial CUE: $([System.IO.Path]::ChangeExtension($outBinPath, '.cue'))"
}
