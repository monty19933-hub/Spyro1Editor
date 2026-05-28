param(
    [string]$LevelKey = "StoneHill",
    [string]$NativeEditsPath = "",
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$OutPath = "",
    [string]$CuePath = "",
    [string]$PlanPath = "",
    [switch]$RuntimeInitAppended,
    [string]$RuntimeTemplateRamPath = ".\duckstation-mainram-fresh-stonehill.bin",
    [ValidateSet("LooseGemsOnly", "GemsAndEnemies", "All", "None")]
    [string]$AppendPolicy = "GemsAndEnemies",
    [int]$SingleAppendTrueIndex = -1,
    [switch]$SkipTreasureTotalPatch,
    [switch]$ExperimentalImportExternalChestPackages,
    [ValidateSet("CopyOnly", "RegisterPrimaryRoot", "RegisterSecondaryRoot", "RegisterAllRoots", "ReplaceUnusedRoot", "RegisterCompanionRoot")]
    [string]$ExperimentalExternalChestPackageMode = "CopyOnly",
    [switch]$AllowExperimentalPackageRootRegistration,
    [ValidateSet("Clone", "Blank")]
    [string]$ExperimentalAppendRecordMode = "Clone",
    [ValidateSet("Copy", "KeepDonorPointer", "ZeroPointer")]
    [string]$ExperimentalAppendSpecialDataMode = "Copy",
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37
$RecordStride = 0x58
$TreasureTotalTableImageOffset = [Convert]::ToInt64("7945FB0", 16)
$MainRamBase = [Convert]::ToUInt64("80000000", 16)
$UInt32Mask = [Convert]::ToUInt64("FFFFFFFF", 16)
$LegacyStride = 0x50
$CoordOffsets = [ordered]@{ x = 0x0C; y = 0x10; z = 0x14 }

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Resolve-WorkspaceInputFile([string]$Path) {
    $resolved = Resolve-WorkspacePath $Path
    if (Test-Path -LiteralPath $resolved) { return $resolved }

    $fileName = [System.IO.Path]::GetFileName($Path)
    if ([string]::IsNullOrWhiteSpace($fileName)) { return $resolved }

    $localRoot = Join-Path $WorkspaceRoot "_local"
    if (-not (Test-Path -LiteralPath $localRoot -PathType Container)) { return $resolved }

    $matches = @(Get-ChildItem -LiteralPath $localRoot -Recurse -File -Filter $fileName -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending)
    if ($matches.Count -gt 0) { return $matches[0].FullName }
    return $resolved
}

function Convert-HexOrInt64($Value) {
    if ($null -eq $Value) { return 0L }
    if ($Value -is [string]) {
        $text = $Value.Trim()
        if ($text.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
            return [Convert]::ToInt64($text.Substring(2), 16)
        }
        if ($text.Length -eq 0) { return 0L }
        return [Convert]::ToInt64($text, 10)
    }
    return [Convert]::ToInt64($Value)
}

function Normalize-LevelKey([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return (($Value.ToCharArray() | Where-Object { [char]::IsLetterOrDigit($_) } | ForEach-Object { [char]::ToLowerInvariant($_) }) -join "")
}

function Get-LevelCatalogEntries {
    $path = Resolve-WorkspacePath ".\spyro-level-catalog.json"
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing level catalog: $path" }
    $root = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    return @($root.levels)
}

function Get-LevelSourceTable([string]$Key) {
    $wanted = Normalize-LevelKey $Key
    $levels = @(Get-LevelCatalogEntries)
    for ($levelIndex = 0; $levelIndex -lt $levels.Count; $levelIndex++) {
        $level = $levels[$levelIndex]
        $matches = @(
            (Normalize-LevelKey ([string]$level.key)),
            (Normalize-LevelKey ([string]$level.scriptKey)),
            (Normalize-LevelKey ([string]$level.displayName))
        )
        if ($matches -notcontains $wanted) { continue }
        $recordCount = [int]$level.sourceRecordCount
        if ($recordCount -le 0) { throw "Level '$Key' has no mapped source moby table yet." }
        $table = [ordered]@{
            levelKey = [string]$level.scriptKey
            displayName = [string]$level.displayName
            editSlug = [string]$level.key
            wadEntry = [int]$level.sourceWadEntry
            tableWadOffset = Convert-HexOrInt64 $level.sourceTableWadOffset
            tableRelativeOffset = Convert-HexOrInt64 $level.sourceTableRelativeOffset
            recordCount = $recordCount
            confidence = [string]$level.confidence
            treasureTotalTableIndex = $levelIndex
        }
        if ($level.PSObject.Properties.Name -contains "runtimeMobyPointer" -and -not [string]::IsNullOrWhiteSpace([string]$level.runtimeMobyPointer)) {
            $table.runtimeMobyPointer = [uint64](Convert-HexOrInt64 $level.runtimeMobyPointer)
        }
        return $table
    }
    throw "Unsupported level key: $Key"
}

function Get-LevelSourceTables([string]$Key) {
    if ($Key -eq "All") {
        $tables = @()
        foreach ($level in Get-LevelCatalogEntries) {
            if ([int]$level.sourceRecordCount -gt 0) {
                $tables += Get-LevelSourceTable ([string]$level.scriptKey)
            }
        }
        return $tables
    }
    return @((Get-LevelSourceTable $Key))
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

function Get-AppendSkipReason($Edit, [string]$Policy, [string]$TargetLevelKey = "") {
    $crashRiskReason = Get-CrashRiskAppendSkipReason $Edit $TargetLevelKey
    if (-not [string]::IsNullOrWhiteSpace($crashRiskReason)) { return $crashRiskReason }
    if ($Policy -eq "All") { return "" }
    if ($Policy -eq "None") { return "AppendPolicy=None skips all true-added source records." }
    if (Test-ExternalObjectLibraryAppend $Edit $TargetLevelKey) {
        $mutation = Get-Field $Edit "recordMutation" $null
        $policy = [string](Get-Field $mutation "runtimeIdentityPolicy" "")
        if (-not [string]::IsNullOrWhiteSpace($policy)) {
            return "Object Library cross-level true-adds are disabled in normal exports. This donor uses $policy metadata, so test it with Test Selected Add BIN before including it in a full loader export."
        }
        return "Object Library cross-level true-adds are disabled in normal exports; even keys can corrupt level-local loader/audio state until asset/data import is proven. Use Test Selected Add BIN for one selected object first."
    }

    $typeHex = ([string](Get-Field $Edit "typeHex" "")).Trim()
    if ($Policy -eq "LooseGemsOnly") {
        if (Test-StandaloneGemAppend $Edit) { return "" }
        return "AppendPolicy=LooseGemsOnly only exports standalone gem true-adds; behavior, controller, chest, enemy, and scenery appends need linked-data handling first."
    }
    if ($Policy -eq "GemsAndEnemies") {
        if (Test-StandaloneGemAppend $Edit) { return "" }
        if (Test-EnemyAppendCandidate $Edit) { return "" }
        if (Test-LinkedChestContentAppend $Edit) { return "" }
        if (Test-LevelLocalChestAppend $Edit $TargetLevelKey) { return "" }
        if (Test-CommonTreasureAppend $Edit) { return "" }
        return "AppendPolicy=GemsAndEnemies only exports standalone gem, named enemy/fodder, level-local reward chest, and local linked chest-content true-adds; cross-level Object Library appends need linked asset/data handling first."
    }

    return "Unknown append policy: $Policy"
}

function Test-LocalActorFallbackAppend($RecordMutation) {
    if ($null -eq $RecordMutation) { return $false }
    $fallback = Get-Field $RecordMutation "localActorFallback" $false
    if ($fallback -is [bool] -and $fallback) { return $true }
    $policy = ([string](Get-Field $RecordMutation "runtimeIdentityPolicy" "")).Trim().ToLowerInvariant()
    return $policy -eq "local-actor-fallback"
}

function Get-CrashRiskAppendSkipReason($Edit, [string]$TargetLevelKey = "") {
    $mutation = Get-Field $Edit "recordMutation" $null
    $sourceLevelKey = ([string](Get-Field $mutation "sourceLevelKey" "")).Trim()
    if ([string]::IsNullOrWhiteSpace($sourceLevelKey)) { return "" }
    if (-not [string]::IsNullOrWhiteSpace($TargetLevelKey) -and
        (Normalize-LevelKey $sourceLevelKey) -eq (Normalize-LevelKey $TargetLevelKey)) { return "" }

    $family = ([string](Get-Field $mutation "sourceFamily" "")).Trim().ToLowerInvariant()
    $text = (([string](Get-Field $Edit "label" "")) + " " +
        ([string](Get-Field $Edit "displayTargetLabel" "")) + " " +
        ([string](Get-Field $Edit "kind" "")) + " " +
        ([string](Get-Field $Edit "candidateKind" "")) + " " +
        ([string](Get-Field $mutation "sourceLabel" "")) + " " +
        ([string](Get-Field $mutation "dependencyRisk" ""))).ToLowerInvariant()

    $isBlockedChest = $family -eq "lockedchest" -or $family -eq "springchest" -or
        $text.Contains("locked chest") -or $text.Contains("unlock chest") -or $text.Contains("spring chest")
    if (-not $isBlockedChest) { return "" }

    if (Test-LocalActorFallbackAppend $mutation) { return "" }

    if ($ExperimentalImportExternalChestPackages -and
        ($ExperimentalExternalChestPackageMode -eq "ReplaceUnusedRoot" -or $ExperimentalExternalChestPackageMode -eq "RegisterCompanionRoot")) {
        $targetKey = Normalize-LevelKey $TargetLevelKey
        $sourceKey = Normalize-LevelKey $sourceLevelKey
        if ($ExperimentalExternalChestPackageMode -eq "RegisterCompanionRoot" -and
            (($targetKey -eq "artisans" -and
              (($family -eq "lockedchest" -and $sourceKey -eq "peacekeepers") -or
               ($family -eq "springchest" -and $sourceKey -eq "townsquare"))) -or
             ($targetKey -eq "stonehill" -and $family -eq "springchest" -and $sourceKey -eq "townsquare") -or
             ($targetKey -eq "darkhollow" -and $family -eq "springchest" -and $sourceKey -eq "townsquare"))) {
            return ""
        }
        if ($ExperimentalExternalChestPackageMode -eq "ReplaceUnusedRoot" -and
            (($targetKey -eq "artisans" -and
              (($family -eq "lockedchest" -and $sourceKey -eq "peacekeepers") -or
               ($family -eq "springchest" -and $sourceKey -eq "townsquare"))) -or
             ($targetKey -eq "stonehill" -and $family -eq "springchest" -and $sourceKey -eq "townsquare") -or
             ($targetKey -eq "darkhollow" -and $family -eq "springchest" -and $sourceKey -eq "townsquare"))) {
            return ""
        }
    }

    return "Blocked crash-risk cross-level chest true-add. Locked/spring chests need actor-package import and pointer rebasing; the latest Artisans package-root import test froze during the fly-in, so normal and isolated exports will not write this append."
}

function Test-ExternalObjectLibraryAppend($Edit, [string]$TargetLevelKey = "") {
    $mutation = Get-Field $Edit "recordMutation" $null
    $sourceLevelKey = ([string](Get-Field $mutation "sourceLevelKey" "")).Trim()
    if ([string]::IsNullOrWhiteSpace($sourceLevelKey)) { return $false }
    if ([string]::IsNullOrWhiteSpace($TargetLevelKey)) { return $true }
    return (Normalize-LevelKey $sourceLevelKey) -ne (Normalize-LevelKey $TargetLevelKey)
}

function Test-StandaloneGemAppend($Edit) {
    $text = (([string](Get-Field $Edit "label" "")) + " " +
        ([string](Get-Field $Edit "displayTargetLabel" "")) + " " +
        ([string](Get-Field $Edit "kind" "")) + " " +
        ([string](Get-Field $Edit "candidateKind" ""))).ToLowerInvariant()
    if ($text.Contains("key")) { return $false }
    $typeHex = ([string](Get-Field $Edit "typeHex" "")).Trim().ToLowerInvariant()
    $flag4AHex = ([string](Get-Field $Edit "flag4AHex" "")).Trim().ToLowerInvariant()
    if ($typeHex -eq "0x18" -or $typeHex -eq "18") {
        if ([string]::IsNullOrWhiteSpace($flag4AHex) -or $flag4AHex -eq "0x40" -or $flag4AHex -eq "40") { return $true }
    }
    return $false
}

function Test-CommonTreasureAppend($Edit) {
    $mutation = Get-Field $Edit "recordMutation" $null
    $family = ([string](Get-Field $mutation "sourceFamily" "")).Trim().ToLowerInvariant()
    if ($family -eq "key" -or $family -eq "springchest" -or $family -eq "lockedchest" -or $family -eq "chestcontent") { return $false }

    $text = (([string](Get-Field $Edit "label" "")) + " " +
        ([string](Get-Field $Edit "displayTargetLabel" "")) + " " +
        ([string](Get-Field $Edit "kind" "")) + " " +
        ([string](Get-Field $Edit "candidateKind" ""))).ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($text)) { return $false }
    if ($text.Contains("key")) { return $true }
    return $false
}

function Test-LevelLocalChestAppend($Edit, [string]$TargetLevelKey = "") {
    $mutation = Get-Field $Edit "recordMutation" $null
    $mode = ([string](Get-Field $mutation "mode" "")).Trim()
    if ($mode -ne "appendFromSource") { return $false }

    $sourceLevelKey = ([string](Get-Field $mutation "sourceLevelKey" "")).Trim()
    if (-not [string]::IsNullOrWhiteSpace($sourceLevelKey) -and
        -not [string]::IsNullOrWhiteSpace($TargetLevelKey) -and
        (Normalize-LevelKey $sourceLevelKey) -ne (Normalize-LevelKey $TargetLevelKey)) {
        return $false
    }

    $family = ([string](Get-Field $mutation "sourceFamily" "")).Trim().ToLowerInvariant()
    if ($family -eq "springchest" -or $family -eq "lockedchest" -or $family -eq "chestcontent" -or $family -eq "key") { return $false }

    $typeHex = ([string](Get-Field $Edit "typeHex" "")).Trim().ToLowerInvariant()
    $flag4AHex = ([string](Get-Field $Edit "flag4AHex" "")).Trim().ToLowerInvariant()
    if ($typeHex -ne "0x20" -and $typeHex -ne "20") { return $false }
    if ($flag4AHex -ne "0x10" -and $flag4AHex -ne "10") { return $false }

    $label = (([string](Get-Field $Edit "label" "")) + " " +
        ([string](Get-Field $Edit "displayTargetLabel" "")) + " " +
        ([string](Get-Field $Edit "kind" "")) + " " +
        ([string](Get-Field $Edit "candidateKind" ""))).ToLowerInvariant()
    if ($label.Contains("life chest") -or $label.Contains("extra life")) { return $false }
    if ($label.Contains("locked") -or $label.Contains("spring")) { return $false }
    return $label.Contains("chest") -or $label.Contains("box") -or $label.Contains("container")
}

function Test-EnemyAppendCandidate($Edit) {
    $text = (([string](Get-Field $Edit "label" "")) + " " +
        ([string](Get-Field $Edit "displayTargetLabel" "")) + " " +
        ([string](Get-Field $Edit "kind" "")) + " " +
        ([string](Get-Field $Edit "candidateKind" ""))).ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($text)) { return $false }
    if ($text.Contains("enemy/object") -or $text.Contains("object?")) { return $false }
    if ($text.Contains("chest") -or $text.Contains("dragon") -or $text.Contains("portal") -or $text.Contains("camera") -or $text.Contains("scenery") -or $text.Contains("helper") -or $text.Contains("control") -or $text.Contains("balloon")) { return $false }
    return $text.Contains("enemy") -or
        $text.Contains("fodder") -or
        $text.Contains("gnorc") -or
        $text.Contains("norc") -or
        $text.Contains("torro") -or
        $text.Contains("bull") -or
        $text.Contains("sheep") -or
        $text.Contains("shepherd") -or
        $text.Contains("shepard") -or
        $text.Contains("ram") -or
        $text.Contains("thief") -or
        $text.Contains("theif")
}

function Convert-BytesToHex([byte[]]$Bytes) {
    return -join ($Bytes | ForEach-Object { "{0:X2}" -f $_ })
}

function Convert-HexToBytes([string]$Hex) {
    $bytes = New-Object byte[] ([int]($Hex.Length / 2))
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($Hex.Substring($i * 2, 2), 16)
    }
    return $bytes
}

function Write-Int32LE([byte[]]$Bytes, [int]$Offset, [int]$Value) {
    [byte[]]$raw = [BitConverter]::GetBytes([int32]$Value)
    [Array]::Copy($raw, 0, $Bytes, $Offset, 4)
}

function Write-UInt32LE([byte[]]$Bytes, [int]$Offset, [uint64]$Value) {
    [byte[]]$raw = [BitConverter]::GetBytes([uint32]($Value -band $UInt32Mask))
    [Array]::Copy($raw, 0, $Bytes, $Offset, 4)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-RuntimeTemplateRecord([byte[]]$RuntimeRam, [uint64]$RuntimeMobyPointer, [int]$TrueIndex) {
    if ($null -eq $RuntimeRam) { throw "RuntimeInitAppended needs a runtime template RAM dump." }
    $runtimeOffset = [int](($RuntimeMobyPointer - $MainRamBase) + ([uint64]$TrueIndex * [uint64]$RecordStride))
    if ($runtimeOffset -lt 0 -or ($runtimeOffset + $RecordStride) -gt $RuntimeRam.Length) {
        throw "Runtime template record T$TrueIndex is outside the supplied RAM dump."
    }
    $record = New-Object byte[] $RecordStride
    [Array]::Copy($RuntimeRam, $runtimeOffset, $record, 0, $RecordStride)
    return $record
}

function Set-RecordByteFromEdit([byte[]]$RecordBytes, $Edit, [string]$Field, [int]$Offset, [System.Collections.ArrayList]$Descriptions) {
    $rawValue = Get-Field $Edit $Field $null
    if ($null -eq $rawValue) { return }
    $value = Convert-PatchInt $rawValue $Field
    if ($value -lt 0 -or $value -gt 255) {
        throw "$Field value 0x$($value.ToString('X')) is outside byte range."
    }
    if ($RecordBytes[$Offset] -ne [byte]$value) {
        $RecordBytes[$Offset] = [byte]$value
        [void]$Descriptions.Add("+0x$($Offset.ToString('X2'))=$Field 0x$($value.ToString('X2'))")
    }
}

function Set-ExternalTemplateSourceByteFromEdit([byte[]]$RecordBytes, $Edit, [string]$Field, [int]$Offset, [System.Collections.ArrayList]$Descriptions, [bool]$AllowExplicitZero = $false) {
    $rawValue = Get-Field $Edit $Field $null
    if ($null -eq $rawValue) { return }
    $value = Convert-PatchInt $rawValue $Field
    if ($value -lt 0 -or $value -gt 255) {
        throw "$Field value 0x$($value.ToString('X')) is outside byte range."
    }

    if (-not $AllowExplicitZero -and $value -eq 0 -and $RecordBytes[$Offset] -ne 0) {
        [void]$Descriptions.Add("+0x$($Offset.ToString('X2'))=$Field kept donor 0x$($RecordBytes[$Offset].ToString('X2')) because saved Object Library byte was blank/0x00")
        return
    }

    if ($RecordBytes[$Offset] -ne [byte]$value) {
        $RecordBytes[$Offset] = [byte]$value
        [void]$Descriptions.Add("+0x$($Offset.ToString('X2'))=$Field 0x$($value.ToString('X2'))")
    }
}

function Set-ExternalTemplateFlagByteFromEdit([byte[]]$RecordBytes, $Edit, $RecordMutation, [string]$Field, [int]$Offset, [System.Collections.ArrayList]$Descriptions) {
    $rawValue = Get-Field $Edit $Field $null
    if ($null -eq $rawValue) { return }
    $value = Convert-PatchInt $rawValue $Field
    if ($value -lt 0 -or $value -gt 255) {
        throw "$Field value 0x$($value.ToString('X')) is outside byte range."
    }

    $sourceFamily = ([string](Get-Field $RecordMutation "sourceFamily" "")).Trim().ToLowerInvariant()
    $isExternalChest = $sourceFamily -eq "springchest" -or $sourceFamily -eq "lockedchest"
    if ($isExternalChest -and -not (Test-LocalActorFallbackAppend $RecordMutation) -and $Offset -eq 0x52 -and $RecordBytes[$Offset] -eq 0xFF -and $value -eq 0x10) {
        [void]$Descriptions.Add("+0x$($Offset.ToString('X2'))=$Field kept donor source flag 0xFF for external chest loader transform")
        return
    }

    if ($RecordBytes[$Offset] -ne [byte]$value) {
        $RecordBytes[$Offset] = [byte]$value
        [void]$Descriptions.Add("+0x$($Offset.ToString('X2'))=$Field 0x$($value.ToString('X2'))")
    }
}

function Apply-ExternalTemplateIdentityBytes([byte[]]$RecordBytes, $Edit, $RecordMutation, $SourceTable, $TargetTable) {
    $sourceLevelKey = [string](Get-Field $RecordMutation "sourceLevelKey" "")
    $externalDonor = -not [string]::IsNullOrWhiteSpace($sourceLevelKey) -and (Normalize-LevelKey $sourceLevelKey) -ne (Normalize-LevelKey ([string]$TargetTable.levelKey))
    if (-not $externalDonor) { return "" }

    $descriptions = New-Object System.Collections.ArrayList
    Set-ExternalTemplateSourceByteFromEdit $RecordBytes $Edit "sourceByte36Hex" 0x36 $descriptions
    Set-ExternalTemplateSourceByteFromEdit $RecordBytes $Edit "sourceByte37Hex" 0x37 $descriptions $true
    Set-ExternalTemplateSourceByteFromEdit $RecordBytes $Edit "sourceByte4FHex" 0x4F $descriptions
    Set-RecordByteFromEdit $RecordBytes $Edit "typeHex" 0x50 $descriptions
    Set-RecordByteFromEdit $RecordBytes $Edit "stateHex" 0x51 $descriptions
    Set-ExternalTemplateFlagByteFromEdit $RecordBytes $Edit $RecordMutation "flag4AHex" 0x52 $descriptions
    Set-ExternalTemplateFlagByteFromEdit $RecordBytes $Edit $RecordMutation "flag4BHex" 0x53 $descriptions

    if ($descriptions.Count -eq 0) { return "" }
    return " Forced Object Library identity bytes from saved template metadata: " + ([string]::Join(", ", @($descriptions.ToArray()))) + "."
}

function Test-AllZero([byte[]]$Bytes) {
    foreach ($b in $Bytes) {
        if ($b -ne 0) { return $false }
    }
    return $true
}

function Align-Value([uint64]$Value, [uint64]$Alignment) {
    if ($Alignment -le 1) { return $Value }
    $remainder = $Value % $Alignment
    if ($remainder -eq 0) { return $Value }
    return $Value + ($Alignment - $remainder)
}

function Test-SourceSpecialDataOffset($Table, [uint64]$Offset) {
    if (-not $Table.Contains("tableRelativeOffset")) { return $false }
    if ($Offset -eq 0) { return $false }
    return ($Offset -lt [uint64]$Table.tableRelativeOffset)
}

function Get-DefaultSpecialDataLengthForType([int]$TypeByte) {
    switch ($TypeByte) {
        0x00 { return 0x28 }
        0x18 { return 0x18 }
        default { return 0x18 }
    }
}

function New-SpecialDataAllocator([IO.FileStream]$Stream, $Layout, $Table) {
    if (-not $Table.Contains("tableRelativeOffset")) { return $null }
    $tableRelativeOffset = [uint64]$Table.tableRelativeOffset
    if ($tableRelativeOffset -le 0) { return $null }

    $entries = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt [int]$Table.recordCount; $i++) {
        $recordOffset = [int64]$Table.tableWadOffset + ([int64]$i * $RecordStride)
        [byte[]]$recordBytes = Read-WadBytes $Stream $Layout $recordOffset $RecordStride
        $specialDataOffset = [uint64](Get-UInt32LE $recordBytes 0)
        if (-not (Test-SourceSpecialDataOffset $Table $specialDataOffset)) { continue }
        [void]$entries.Add([pscustomobject]@{
            trueIndex = $i
            offset = $specialDataOffset
            type = [int]$recordBytes[0x50]
        })
    }

    $uniqueEntries = @($entries | Sort-Object { [uint64]$_.offset } | Group-Object offset | ForEach-Object { $_.Group[0] })
    $lengthsByOffset = @{}
    [uint64]$maxEnd = 0
    for ($i = 0; $i -lt $uniqueEntries.Count; $i++) {
        $entry = $uniqueEntries[$i]
        [uint64]$entryOffset = [uint64]$entry.offset
        [int]$length = 0
        if (($i + 1) -lt $uniqueEntries.Count) {
            [uint64]$nextOffset = [uint64]$uniqueEntries[$i + 1].offset
            [uint64]$delta = $nextOffset - $entryOffset
            if ($delta -gt 0 -and $delta -le 0x400) {
                $length = [int]$delta
            }
        }
        if ($length -le 0) {
            $length = Get-DefaultSpecialDataLengthForType ([int]$entry.type)
        }
        $lengthsByOffset[$entryOffset.ToString()] = $length
        [uint64]$endOffset = $entryOffset + [uint64]$length
        if ($endOffset -gt $maxEnd) { $maxEnd = $endOffset }
    }

    return [ordered]@{
        wadBaseOffset = ([int64]$Table.tableWadOffset - [int64]$Table.tableRelativeOffset)
        tableRelativeOffset = $tableRelativeOffset
        nextSearchOffset = (Align-Value $maxEnd 4)
        specialLengths = $lengthsByOffset
    }
}

function Get-SpecialDataLength($Allocator, [uint64]$SourceOffset, [byte[]]$RecordBytes) {
    if ($null -eq $Allocator) { return 0 }
    $key = $SourceOffset.ToString()
    if ($Allocator.specialLengths.ContainsKey($key)) {
        return [int]$Allocator.specialLengths[$key]
    }
    if ($null -ne $RecordBytes -and $RecordBytes.Length -gt 0x50) {
        return Get-DefaultSpecialDataLengthForType ([int]$RecordBytes[0x50])
    }
    return 0x18
}

function Reserve-SpecialDataOffset([IO.FileStream]$Stream, $Layout, $Table, $Allocator, [int]$Length) {
    if ($null -eq $Allocator -or $Length -le 0) { throw "Invalid special-data allocation request." }
    [uint64]$candidate = Align-Value ([uint64]$Allocator.nextSearchOffset) 4
    [uint64]$limit = [uint64]$Allocator.tableRelativeOffset
    while (($candidate + [uint64]$Length) -le $limit) {
        $targetWadOffset = [int64]$Allocator.wadBaseOffset + [int64]$candidate
        [byte[]]$before = Read-WadBytes $Stream $Layout $targetWadOffset $Length
        if (Test-AllZero $before) {
            $Allocator.nextSearchOffset = Align-Value ($candidate + [uint64]$Length) 4
            return $candidate
        }
        $candidate = Align-Value ($candidate + 4) 4
    }
    throw "Could not find $Length free special-data bytes before $($Table.displayName)'s source moby table."
}

function Convert-LegacyIndexToTrue([int]$LegacyIndex) {
    $trueNumerator = ($LegacyIndex * $LegacyStride) - 8
    if ($trueNumerator -lt 0 -or ($trueNumerator % $RecordStride) -ne 0) { return $null }
    return [int]($trueNumerator / $RecordStride)
}

function Resolve-EditTrueIndex($Edit) {
    $editIndex = [int](Get-Field $Edit "index" -1)
    $trueIndex = Get-Field $Edit "trueIndex" $null
    if ($null -ne $trueIndex) { return [int]$trueIndex }

    $legacyIndex = Get-Field $Edit "legacyIndex" $null
    if ($null -ne $legacyIndex) {
        $mapped = Convert-LegacyIndexToTrue ([int]$legacyIndex)
        if ($null -ne $mapped) { return $mapped }
    }

    $fallback = Convert-LegacyIndexToTrue $editIndex
    if ($null -ne $fallback) { return $fallback }
    return $editIndex
}

function Convert-PatchInt($Value, [string]$Name) {
    if ($null -eq $Value) { throw "Missing numeric patch field: $Name" }
    if ($Value -is [string]) {
        $text = $Value.Trim()
        if ($text.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
            return [int][Convert]::ToInt32($text.Substring(2), 16)
        }
        return [int]$text
    }
    return [int]$Value
}

function Get-RawEditedAxis($Edit, [string]$Axis) {
    $rawEdited = Get-Field $Edit "rawEdited" $null
    $value = Get-Field $rawEdited $Axis $null
    if ($null -ne $value) { return [int]$value }

    $edited = Get-Field $Edit "edited" $null
    $floatValue = [double](Get-Field $edited $Axis 0)
    return [int][Math]::Round($floatValue * 16.0)
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

function New-PatchRecord($Table, $Layout, $Edit, [int]$TrueIndex, [string]$Kind, [int]$RecordOffset, [byte[]]$Bytes, [string]$Description) {
    $wadOffset = [int64]$Table.tableWadOffset + ([int64]$TrueIndex * $RecordStride) + [int64]$RecordOffset
    return [ordered]@{
        levelKey = [string]$Table.levelKey
        levelName = [string]$Table.displayName
        sourceEntry = [int]$Table.wadEntry
        index = [int](Get-Field $Edit "index" -1)
        trueIndex = $TrueIndex
        label = [string](Get-Field $Edit "label" "")
        kind = $Kind
        description = $Description
        recordOffset = ("+0x{0:X2}" -f $RecordOffset)
        wadRelativeOffset = ("0x{0:X}" -f $wadOffset)
        imageOffset = Convert-DiscFileOffsetToImageOffset $Layout $WadLba $wadOffset
        bytesHex = Convert-BytesToHex $Bytes
    }
}

function New-RawPatch($Table, $Layout, [string]$Kind, [int64]$WadOffset, [byte[]]$Bytes, [string]$Description) {
    return [ordered]@{
        levelKey = [string]$Table.levelKey
        levelName = [string]$Table.displayName
        sourceEntry = [int]$Table.wadEntry
        index = -1
        trueIndex = -1
        label = ""
        kind = $Kind
        description = $Description
        recordOffset = ""
        wadRelativeOffset = ("0x{0:X}" -f $WadOffset)
        imageOffset = Convert-DiscFileOffsetToImageOffset $Layout $WadLba $WadOffset
        bytesHex = Convert-BytesToHex $Bytes
    }
}

function New-UInt32LEBytes([uint64]$Value) {
    return [BitConverter]::GetBytes([uint32]($Value -band $UInt32Mask))
}

function Get-LevelWadBaseOffset($Table) {
    return [int64]$Table.tableWadOffset - [int64]$Table.tableRelativeOffset
}

function Get-ExternalChestPackageImportRecipe($TargetTable, $SourceTable, $RecordMutation) {
    if (-not $ExperimentalImportExternalChestPackages) { return $null }

    $targetKey = Normalize-LevelKey ([string]$TargetTable.levelKey)
    $sourceKey = Normalize-LevelKey ([string]$SourceTable.levelKey)
    $sourceFamily = ([string](Get-Field $RecordMutation "sourceFamily" "")).Trim().ToLowerInvariant()

    if ($targetKey -eq "artisans" -and $sourceKey -eq "peacekeepers" -and $sourceFamily -eq "lockedchest") {
        if ($ExperimentalExternalChestPackageMode -eq "RegisterCompanionRoot") {
            return [ordered]@{
                id = "artisans.peacekeepers.lockedChest.package.safeGapActorId.v4"
                sourceStart = [int64]0x1B3978
                sourcePartnerStart = [int64]0x1B4980
                copyLength = [int]0x2830
                targetStart = [int64]0x30800
                copySegments = @(
                    [ordered]@{ sourceStart = [int64]0x1B3978; targetStart = [int64]0x30800; length = [int]0x2830 }
                )
                rootEntries = @(
                    [ordered]@{ targetRootSlot = [int]0xDC; targetRoot = [int64]0x30800; actorId = [int]0x00AE }
                )
                description = "Peace Keepers locked chest actor package copied into the proven Artisans 0x30800 zero gap with a single actor-id list registration"
            }
        }
        if ($ExperimentalExternalChestPackageMode -eq "ReplaceUnusedRoot") {
            return [ordered]@{
                id = "artisans.peacekeepers.lockedChest.package.actorSegmentSlotSwap.v2"
                sourceStart = [int64]0x1B3978
                sourcePartnerStart = [int64]0x1B4980
                copyLength = [int]0x2830
                targetStart = [int64]0x1C4AC4
                targetRootSlot = [int]0xB8
                replaceRootEntries = @(
                    [ordered]@{ targetRootSlot = [int]0xB8; targetRoot = [int64]0x1C4AC4; actorId = [int]0x00AE; originalActor = "0x0078" },
                    [ordered]@{ targetRootSlot = [int]0xBC; targetRoot = [int64]0x1C5ACC; actorId = [int]0x01A5; originalActor = "0x00FF" },
                    [ordered]@{ targetRootSlot = [int]0xC0; targetRoot = [int64]0x1C6B08; actorId = [int]0x000E; originalActor = "0x0100" }
                )
                internalDependencyRebases = @(
                    [ordered]@{ sourceRoot = [int64]0x1B9C80; targetRoot = [int64]0x1C1E00; length = [int]0x2538; actorId = [int]0x006E; note = "reuse Artisans resident actor 0x006E dependency root" },
                    [ordered]@{ sourceRoot = [int64]0x1BC1B8; targetRoot = [int64]0x1C4338; length = [int]0x078C; actorId = [int]0x000F; note = "reuse Artisans resident actor 0x000F dependency root" }
                )
                targetOriginalActor = "0x0078/0x00FF/0x0100"
                targetOriginalUse = "Artisans actor-root slots with no source moby rows; intentionally sacrificed only for this isolated chest test"
                description = "Peace Keepers locked chest actor packages swapped into real Artisans actor-segment slots and remapped to donor actor ids"
            }
        }
        return [ordered]@{
            id = "artisans.peacekeepers.lockedChest.package.dependencyClosure.v2"
            sourceStart = [int64]0x1B3978
            copyLength = [int]0x2830
            targetStart = [int64]0x30800
            sourceRootRanges = @(
                [ordered]@{
                    start = [int64]0x1B3978
                    end = [int64]0x1B4980
                },
                [ordered]@{
                    start = [int64]0x1B59BC
                    end = [int64]0x1B61A8
                }
            )
            description = "Peace Keepers locked chest actor package dependency closure"
        }
    }

    if ($targetKey -eq "artisans" -and $sourceKey -eq "townsquare" -and $sourceFamily -eq "springchest") {
        if ($ExperimentalExternalChestPackageMode -eq "RegisterCompanionRoot") {
            $packageImportProfile = ([string](Get-Field $RecordMutation "packageImportProfile" "")).Trim().ToLowerInvariant()
            if ($packageImportProfile -eq "minimal00c2safegap30800") {
                return [ordered]@{
                    id = "artisans.townsquare.springChest.package.minimal00C2SafeGap30800.v1"
                    sourceStart = [int64]0x1BF470
                    copyLength = [int]0x3E8
                    targetStart = [int64]0x30800
                    copySegments = @(
                        [ordered]@{ sourceStart = [int64]0x1BF470; targetStart = [int64]0x30800; length = [int]0x03E8 },
                        [ordered]@{ sourceStart = [int64]0x1BF858; targetStart = [int64]0x30BE8; length = [int]0x08BC },
                        [ordered]@{ sourceStart = [int64]0x1C0114; targetStart = [int64]0x314A4; length = [int]0x0528 }
                    )
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x74; targetRoot = [int64]0x30800; actorId = [int]0x00C2; originalActor = "0x00C2" }
                    )
                    rootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0xDC; targetRoot = [int64]0x30BE8; actorId = [int]0x0186 },
                        [ordered]@{ targetRootSlot = [int]0xE0; targetRoot = [int64]0x314A4; actorId = [int]0x0149 }
                    )
                    description = "Town Square spring controller plus minimal helper/shell roots copied into the proven Artisans 0x30800 zero gap, remapping only the real 0x00C2 route and leaving dragon/scenery roots untouched"
                }
            }
            return [ordered]@{
                id = "artisans.townsquare.springChest.package.zeroGapRoot.v1"
                sourceStart = [int64]0x1C0114
                sourcePartnerStart = [int64]0x1C063C
                copyLength = [int]0x567C
                targetStart = [int64]0x197A00
                targetRootSlot = [int]0xE0
                description = "Town Square spring chest actor package plus companion package registered in an Artisans zero-filled actor-package gap"
            }
        }
        if ($ExperimentalExternalChestPackageMode -eq "ReplaceUnusedRoot") {
            $packageImportProfile = ([string](Get-Field $RecordMutation "packageImportProfile" "")).Trim().ToLowerInvariant()
            if ($packageImportProfile -eq "alias00c2root14") {
                return [ordered]@{
                    id = "artisans.townsquare.springChest.package.alias00C2Root14.v1"
                    sourceStart = [int64]0x1BF470
                    sourcePartnerStart = [int64]0x1C0114
                    copyLength = [int]0x6320
                    targetStart = [int64]0x1B092C
                    targetRootSlot = [int]0x88
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x88; targetRoot = [int64]0x1B092C; actorId = [int]0x01FE; originalActor = "0x0156" },
                        [ordered]@{ targetRootSlot = [int]0xDC; targetRoot = [int64]0x1B0D14; actorId = [int]0x0186; originalActor = "0x0000" },
                        [ordered]@{ targetRootSlot = [int]0xE0; targetRoot = [int64]0x1B15D0; actorId = [int]0x0149; originalActor = "0x0000" },
                        [ordered]@{ targetRootSlot = [int]0xE4; targetRoot = [int64]0x1B1AF8; actorId = [int]0x0021; originalActor = "0x0000" },
                        [ordered]@{ targetRootSlot = [int]0xE8; targetRoot = [int64]0x1B63E4; actorId = [int]0x0009; originalActor = "0x0000" }
                    )
                    targetOriginalActor = "0x0156 plus blank root slots"
                    targetOriginalUse = "Artisans root 0x0156 is sacrificed, while the original Artisans 0x00C2 package remains registered; blank root slots register the Town Square spring dependencies"
                    description = "Town Square spring chest full dependency span registered under a private 0x01FE controller alias, leaving Artisans actor 0x00C2 untouched"
                }
            }
            if ($packageImportProfile -eq "real00c2root09") {
                return [ordered]@{
                    id = "artisans.townsquare.springChest.package.real00C2Root09.v1"
                    sourceStart = [int64]0x1BF470
                    sourcePartnerStart = [int64]0x1C0114
                    copyLength = [int]0x6320
                    targetStart = [int64]0x1B092C
                    targetRootSlot = [int]0x74
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x74; targetRoot = [int64]0x1B092C; actorId = [int]0x00C2; originalActor = "0x00C2" },
                        [ordered]@{ targetRootSlot = [int]0xDC; targetRoot = [int64]0x1B0D14; actorId = [int]0x0186; originalActor = "0x0000" },
                        [ordered]@{ targetRootSlot = [int]0xE0; targetRoot = [int64]0x1B15D0; actorId = [int]0x0149; originalActor = "0x0000" },
                        [ordered]@{ targetRootSlot = [int]0xE4; targetRoot = [int64]0x1B1AF8; actorId = [int]0x0021; originalActor = "0x0000" },
                        [ordered]@{ targetRootSlot = [int]0xE8; targetRoot = [int64]0x1B63E4; actorId = [int]0x0009; originalActor = "0x0000" }
                    )
                    clearRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x88 }
                    )
                    targetOriginalActor = "0x00C2 plus blank root slots; stale 0x0156 root slot cleared"
                    targetOriginalUse = "the real Artisans 0x00C2 actor route is intentionally sacrificed so flame/charge dispatch can target the imported Town Square spring controller package"
                    description = "Town Square spring chest full dependency span registered under the real Artisans actor 0x00C2 route, with stale overlapping 0x0156 root cleared"
                }
            }
            if ($packageImportProfile -eq "real00c2root14actorswap") {
                return [ordered]@{
                    id = "artisans.townsquare.springChest.package.real00C2Root14ActorSwap.v1"
                    sourceStart = [int64]0x1BF470
                    sourcePartnerStart = [int64]0x1C0114
                    copyLength = [int]0x6320
                    targetStart = [int64]0x1B092C
                    targetRootSlot = [int]0x88
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x88; targetRoot = [int64]0x1B092C; actorId = [int]0x00C2; originalActor = "0x0156" },
                        [ordered]@{ targetRootSlot = [int]0xDC; targetRoot = [int64]0x1B0D14; actorId = [int]0x0186; originalActor = "0x0000" },
                        [ordered]@{ targetRootSlot = [int]0xE0; targetRoot = [int64]0x1B15D0; actorId = [int]0x0149; originalActor = "0x0000" },
                        [ordered]@{ targetRootSlot = [int]0xE4; targetRoot = [int64]0x1B1AF8; actorId = [int]0x0021; originalActor = "0x0000" },
                        [ordered]@{ targetRootSlot = [int]0xE8; targetRoot = [int64]0x1B63E4; actorId = [int]0x0009; originalActor = "0x0000" }
                    )
                    actorIdOnlyRemaps = @(
                        [ordered]@{ targetRootSlot = [int]0x74; actorId = [int]0x01FE; originalActor = "0x00C2"; note = "keep the original Artisans chest package reachable only through the private alias used by hidden vanilla chest rows" }
                    )
                    targetOriginalActor = "0x0156 plus blank root slots; original 0x00C2 actor id is moved to private 0x01FE without moving its root"
                    targetOriginalUse = "Artisans root 0x0156 is sacrificed, while the original Artisans 0x00C2 root stays in-place under private actor 0x01FE"
                    description = "Town Square spring chest full dependency span registered under the real 0x00C2 actor id at the previously safe root14 import slot, while the original Artisans chest root is actor-swapped to private 0x01FE"
                }
            }
            if ($packageImportProfile -eq "real00c2originalrun") {
                return [ordered]@{
                    id = "artisans.townsquare.springChest.package.real00C2OriginalRun.v1"
                    sourceStart = [int64]0x1BF470
                    sourcePartnerStart = [int64]0x1C0114
                    copyLength = [int]0x6320
                    targetStart = [int64]0x1AE664
                    targetRootSlot = [int]0x74
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x74; targetRoot = [int64]0x1AE664; actorId = [int]0x00C2; originalActor = "0x00C2" },
                        [ordered]@{ targetRootSlot = [int]0x78; targetRoot = [int64]0x1AEA4C; actorId = [int]0x0186; originalActor = "0x01A8" },
                        [ordered]@{ targetRootSlot = [int]0x7C; targetRoot = [int64]0x1AF308; actorId = [int]0x0149; originalActor = "0x014B" },
                        [ordered]@{ targetRootSlot = [int]0x80; targetRoot = [int64]0x1AF830; actorId = [int]0x0021; originalActor = "0x01A5" },
                        [ordered]@{ targetRootSlot = [int]0x84; targetRoot = [int64]0x1B411C; actorId = [int]0x0009; originalActor = "0x000E" }
                    )
                    clearRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x88 }
                    )
                    targetOriginalActor = "0x00C2/0x01A8/0x014B/0x01A5/0x000E/0x0156"
                    targetOriginalUse = "a consecutive Artisans actor-root run, preserving sorted root order and avoiding the old unsafe 0x00C2 -> 0x1B092C remap"
                    description = "Town Square spring chest full dependency span imported at the original Artisans 0x00C2 root and mapped across the following consecutive root slots"
                }
            }
            if ($packageImportProfile -eq "include0186dependency") {
                return [ordered]@{
                    id = "artisans.townsquare.springChest.package.with0186Dependency.v1"
                    sourceStart = [int64]0x1BF858
                    sourcePartnerStart = [int64]0x1C0114
                    copyLength = [int]0x5F38
                    targetStart = [int64]0x1AF104
                    targetRootSlot = [int]0x80
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x80; targetRoot = [int64]0x1AF104; actorId = [int]0x0186; originalActor = "0x01A5" },
                        [ordered]@{ targetRootSlot = [int]0x84; targetRoot = [int64]0x1AF9C0; actorId = [int]0x0149; originalActor = "0x000E" },
                        [ordered]@{ targetRootSlot = [int]0x88; targetRoot = [int64]0x1AFEE8; actorId = [int]0x0021; originalActor = "0x0156" },
                        [ordered]@{ targetRootSlot = [int]0x8C; targetRoot = [int64]0x1B47D4; actorId = [int]0x0009; originalActor = "0x0150" }
                    )
                    targetOriginalActor = "0x01A5/0x000E/0x0156/0x0150"
                    targetOriginalUse = "life-chest/gem/scenery package roots intentionally sacrificed only for this isolated spring-chest dependency test"
                    description = "Town Square spring chest dependency span, including actor 0x0186 plus spring chest actor package and companions, swapped into Artisans actor-root slots"
                }
            }
            return [ordered]@{
                id = "artisans.townsquare.springChest.package.actorIdSlotSwap.v2"
                sourceStart = [int64]0x1C0114
                sourcePartnerStart = [int64]0x1C063C
                copyLength = [int]0x567C
                targetStart = [int64]0x1B092C
                targetRootSlot = [int]0x88
                replaceRootEntries = @(
                    [ordered]@{ targetRootSlot = [int]0x88; targetRoot = [int64]0x1B092C; actorId = [int]0x0149 },
                    [ordered]@{ targetRootSlot = [int]0x8C; targetRoot = [int64]0x1B0E54; actorId = [int]0x0021 },
                    [ordered]@{ targetRootSlot = [int]0x90; targetRoot = [int64]0x1B5740; actorId = [int]0x0009 }
                )
                targetOriginalActor = "0x0156/0x0150"
                targetOriginalUse = "scenery/flower package roots intentionally sacrificed only for this isolated chest test"
                description = "Town Square spring chest actor package plus companion package span swapped into Artisans actor-root slots and remapped to donor actor ids"
            }
        }
    }

    if ($targetKey -eq "stonehill" -and $sourceKey -eq "townsquare" -and $sourceFamily -eq "springchest") {
        if ($ExperimentalExternalChestPackageMode -eq "ReplaceUnusedRoot") {
            $packageImportProfile = ([string](Get-Field $RecordMutation "packageImportProfile" "")).Trim().ToLowerInvariant()
            if ($packageImportProfile -eq "alias00c2root0full") {
                return [ordered]@{
                    id = "stonehill.townsquare.springChest.package.alias00C2Root0Full.v1"
                    sourceStart = [int64]0x1BF470
                    sourcePartnerStart = [int64]0x1C0114
                    copyLength = [int]0x6320
                    targetStart = [int64]0x18E800
                    targetRootSlot = [int]0x50
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x50; targetRoot = [int64]0x18E800; actorId = [int]0x01FE; originalActor = "0x0000" },
                        [ordered]@{ targetRootSlot = [int]0x54; targetRoot = [int64]0x18EBE8; actorId = [int]0x0186; originalActor = "0x00EA" },
                        [ordered]@{ targetRootSlot = [int]0x58; targetRoot = [int64]0x18F4A4; actorId = [int]0x0149; originalActor = "0x0195" },
                        [ordered]@{ targetRootSlot = [int]0x5C; targetRoot = [int64]0x18F9CC; actorId = [int]0x0021; originalActor = "0x01DD" },
                        [ordered]@{ targetRootSlot = [int]0x60; targetRoot = [int64]0x1942B8; actorId = [int]0x0009; originalActor = "0x01A0" }
                    )
                    targetOriginalActor = "0x0000/0x00EA/0x0195/0x01DD/0x01A0"
                    targetOriginalUse = "Stone Hill actor-root slots with no known source moby rows in the current source scan; selected for this isolated spring-chest package import probe"
                    description = "Town Square spring chest full dependency span imported into Stone Hill's first large no-source actor-root run under a private 0x01FE controller alias"
                }
            }
            if ($packageImportProfile -eq "local00c2shell0149over014b") {
                return [ordered]@{
                    id = "stonehill.townsquare.springChest.package.local00C2Shell0149Over014B.v1"
                    sourceStart = [int64]0x1C0114
                    copyLength = [int]0x0528
                    targetStart = [int64]0x1CFDEC
                    targetRootSlot = [int]0x98
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x98; targetRoot = [int64]0x1CFDEC; actorId = [int]0x0149; originalActor = "0x014B" }
                    )
                    internalDependencyRebases = @(
                        [ordered]@{ sourceRoot = [int64]0x1C063C; targetRoot = [int64]0x1D03A0; length = [int]0x48EC; actorId = [int]0x0021; note = "reuse Stone Hill resident actor 0x0021 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C4F28; targetRoot = [int64]0x1D64B4; length = [int]0x0868; actorId = [int]0x0009; note = "reuse Stone Hill resident actor 0x0009 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C7CC8; targetRoot = [int64]0x1D4C8C; length = [int]0x103C; actorId = [int]0x01A5; note = "reuse Stone Hill resident actor 0x01A5 dependency root" }
                    )
                    targetOriginalActor = "0x014B"
                    targetOriginalUse = "Stone Hill 0x014B has matching package capacity and only a small source-row footprint; selected for a narrow shell-only spring probe"
                    description = "Town Square spring shell actor 0x0149 copied over Stone Hill actor 0x014B while keeping Stone Hill's local 0x00C2 chest controller package"
                }
            }
            if ($packageImportProfile -eq "local00c2shell0149over0195") {
                return [ordered]@{
                    id = "stonehill.townsquare.springChest.package.local00C2Shell0149Over0195.v1"
                    sourceStart = [int64]0x1C0114
                    copyLength = [int]0x0528
                    targetStart = [int64]0x1A4B40
                    targetRootSlot = [int]0x58
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x58; targetRoot = [int64]0x1A4B40; actorId = [int]0x0149; originalActor = "0x0195" }
                    )
                    internalDependencyRebases = @(
                        [ordered]@{ sourceRoot = [int64]0x1C063C; targetRoot = [int64]0x1D03A0; length = [int]0x48EC; actorId = [int]0x0021; note = "reuse Stone Hill resident actor 0x0021 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C4F28; targetRoot = [int64]0x1D64B4; length = [int]0x0868; actorId = [int]0x0009; note = "reuse Stone Hill resident actor 0x0009 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C7CC8; targetRoot = [int64]0x1D4C8C; length = [int]0x103C; actorId = [int]0x01A5; note = "reuse Stone Hill resident actor 0x01A5 dependency root" }
                    )
                    targetOriginalActor = "0x0195"
                    targetOriginalUse = "Stone Hill actor 0x0195 has no source moby rows in the current source scan and enough package capacity for the 0x0149 shell"
                    description = "Town Square spring shell actor 0x0149 copied over unused-looking Stone Hill actor 0x0195 while keeping Stone Hill's local 0x00C2 chest controller package"
                }
            }
            if ($packageImportProfile -eq "local00c2shell0149over000e") {
                return [ordered]@{
                    id = "stonehill.townsquare.springChest.package.local00C2Shell0149Over000E.v1"
                    sourceStart = [int64]0x1C0114
                    copyLength = [int]0x0528
                    targetStart = [int64]0x1D5CC8
                    targetRootSlot = [int]0xA4
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0xA4; targetRoot = [int64]0x1D5CC8; actorId = [int]0x0149; originalActor = "0x000E" }
                    )
                    internalDependencyRebases = @(
                        [ordered]@{ sourceRoot = [int64]0x1C063C; targetRoot = [int64]0x1D03A0; length = [int]0x48EC; actorId = [int]0x0021; note = "reuse Stone Hill resident actor 0x0021 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C4F28; targetRoot = [int64]0x1D64B4; length = [int]0x0868; actorId = [int]0x0009; note = "reuse Stone Hill resident actor 0x0009 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C7CC8; targetRoot = [int64]0x1D4C8C; length = [int]0x103C; actorId = [int]0x01A5; note = "reuse Stone Hill resident actor 0x01A5 dependency root" }
                    )
                    targetOriginalActor = "0x000E"
                    targetOriginalUse = "Stone Hill actor 0x000E has no source moby rows in the current source scan and enough package capacity for the 0x0149 shell"
                    description = "Town Square spring shell actor 0x0149 copied over unused-looking Stone Hill actor 0x000E while keeping Stone Hill's local 0x00C2 chest controller package"
                }
            }
            if ($packageImportProfile -eq "local00c2shell0149over000f") {
                return [ordered]@{
                    id = "stonehill.townsquare.springChest.package.local00C2Shell0149Over000F.v1"
                    sourceStart = [int64]0x1C0114
                    copyLength = [int]0x0528
                    targetStart = [int64]0x1D9254
                    targetRootSlot = [int]0xB0
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0xB0; targetRoot = [int64]0x1D9254; actorId = [int]0x0149; originalActor = "0x000F" }
                    )
                    internalDependencyRebases = @(
                        [ordered]@{ sourceRoot = [int64]0x1C063C; targetRoot = [int64]0x1D03A0; length = [int]0x48EC; actorId = [int]0x0021; note = "reuse Stone Hill resident actor 0x0021 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C4F28; targetRoot = [int64]0x1D64B4; length = [int]0x0868; actorId = [int]0x0009; note = "reuse Stone Hill resident actor 0x0009 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C7CC8; targetRoot = [int64]0x1D4C8C; length = [int]0x103C; actorId = [int]0x01A5; note = "reuse Stone Hill resident actor 0x01A5 dependency root" }
                    )
                    targetOriginalActor = "0x000F"
                    targetOriginalUse = "Stone Hill actor 0x000F has no source moby rows in the current source scan and enough package capacity for the 0x0149 shell"
                    description = "Town Square spring shell actor 0x0149 copied over unused-looking Stone Hill actor 0x000F while keeping Stone Hill's local 0x00C2 chest controller package"
                }
            }
            if ($packageImportProfile -eq "local00c2shell0149over0078") {
                return [ordered]@{
                    id = "stonehill.townsquare.springChest.package.local00C2Shell0149Over0078.v1"
                    sourceStart = [int64]0x1C0114
                    copyLength = [int]0x0528
                    targetStart = [int64]0x1D99E0
                    targetRootSlot = [int]0xB4
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0xB4; targetRoot = [int64]0x1D99E0; actorId = [int]0x0149; originalActor = "0x0078" }
                    )
                    internalDependencyRebases = @(
                        [ordered]@{ sourceRoot = [int64]0x1C063C; targetRoot = [int64]0x1D03A0; length = [int]0x48EC; actorId = [int]0x0021; note = "reuse Stone Hill resident actor 0x0021 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C4F28; targetRoot = [int64]0x1D64B4; length = [int]0x0868; actorId = [int]0x0009; note = "reuse Stone Hill resident actor 0x0009 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C7CC8; targetRoot = [int64]0x1D4C8C; length = [int]0x103C; actorId = [int]0x01A5; note = "reuse Stone Hill resident actor 0x01A5 dependency root" }
                    )
                    targetOriginalActor = "0x0078"
                    targetOriginalUse = "Stone Hill actor 0x0078 has no source moby rows in the current source scan and a larger package capacity for the 0x0149 shell"
                    description = "Town Square spring shell actor 0x0149 copied over unused-looking Stone Hill actor 0x0078 while keeping Stone Hill's local 0x00C2 chest controller package"
                }
            }
        }
        if ($ExperimentalExternalChestPackageMode -eq "RegisterCompanionRoot") {
            $packageImportProfile = ([string](Get-Field $RecordMutation "packageImportProfile" "")).Trim().ToLowerInvariant()
            if ($packageImportProfile -eq "alias00c2zerogap26540") {
                return [ordered]@{
                    id = "stonehill.townsquare.springChest.package.alias00C2ZeroGap26540.v1"
                    sourceStart = [int64]0x1BF470
                    copyLength = [int]0x11CC
                    targetStart = [int64]0x26540
                    copySegments = @(
                        [ordered]@{ sourceStart = [int64]0x1BF470; targetStart = [int64]0x26540; length = [int]0x03E8 },
                        [ordered]@{ sourceStart = [int64]0x1BF858; targetStart = [int64]0x26928; length = [int]0x08BC },
                        [ordered]@{ sourceStart = [int64]0x1C0114; targetStart = [int64]0x271E4; length = [int]0x0528 }
                    )
                    rootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0xE4; targetRoot = [int64]0x26540; actorId = [int]0x01FE },
                        [ordered]@{ targetRootSlot = [int]0xE8; targetRoot = [int64]0x26928; actorId = [int]0x0186 },
                        [ordered]@{ targetRootSlot = [int]0xEC; targetRoot = [int64]0x271E4; actorId = [int]0x0149 }
                    )
                    targetOriginalActor = "blank root slots"
                    targetOriginalUse = "Stone Hill has blank actor-root slots and a verified zero-filled gap at entry-relative 0x26540"
                    description = "Town Square spring controller/helper/shell roots copied into a Stone Hill zero gap, keeping Stone Hill's original 0x00C2 chest package intact"
                }
            }
            if ($packageImportProfile -eq "alias00c2tailgap1e3640") {
                return [ordered]@{
                    id = "stonehill.townsquare.springChest.package.alias00C2TailGap1E3640.v1"
                    sourceStart = [int64]0x1BF470
                    copyLength = [int]0x11CC
                    targetStart = [int64]0x1E3640
                    copySegments = @(
                        [ordered]@{ sourceStart = [int64]0x1BF470; targetStart = [int64]0x1E3640; length = [int]0x03E8 },
                        [ordered]@{ sourceStart = [int64]0x1BF858; targetStart = [int64]0x1E3A28; length = [int]0x08BC },
                        [ordered]@{ sourceStart = [int64]0x1C0114; targetStart = [int64]0x1E42E4; length = [int]0x0528 }
                    )
                    rootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0xE4; targetRoot = [int64]0x1E3640; actorId = [int]0x01FE },
                        [ordered]@{ targetRootSlot = [int]0xE8; targetRoot = [int64]0x1E3A28; actorId = [int]0x0186 },
                        [ordered]@{ targetRootSlot = [int]0xEC; targetRoot = [int64]0x1E42E4; actorId = [int]0x0149 }
                    )
                    targetOriginalActor = "blank root slots"
                    targetOriginalUse = "Stone Hill has blank actor-root slots and a verified zero-filled tail gap at entry-relative 0x1E3640, after the existing actor-root range"
                    description = "Town Square spring controller/helper/shell roots copied into a Stone Hill tail zero gap, preserving root ordering and keeping Stone Hill's original 0x00C2 chest package intact"
                }
            }
        }
    }

    if ($targetKey -eq "darkhollow" -and $sourceKey -eq "townsquare" -and $sourceFamily -eq "springchest") {
        if ($ExperimentalExternalChestPackageMode -eq "ReplaceUnusedRoot") {
            $packageImportProfile = ([string](Get-Field $RecordMutation "packageImportProfile" "")).Trim().ToLowerInvariant()
            if ($packageImportProfile -eq "local00c2shell0149over000e") {
                return [ordered]@{
                    id = "darkhollow.townsquare.springChest.package.local00C2Shell0149Over000E.v1"
                    sourceStart = [int64]0x1C0114
                    copyLength = [int]0x0528
                    targetStart = [int64]0x18ADE8
                    targetRootSlot = [int]0x9C
                    replaceRootEntries = @(
                        [ordered]@{ targetRootSlot = [int]0x9C; targetRoot = [int64]0x18ADE8; actorId = [int]0x0149; originalActor = "0x000E" }
                    )
                    internalDependencyRebases = @(
                        [ordered]@{ sourceRoot = [int64]0x1C4F28; targetRoot = [int64]0x18B5D4; length = [int]0x0868; actorId = [int]0x0009; note = "reuse Dark Hollow resident actor 0x0009 dependency root" },
                        [ordered]@{ sourceRoot = [int64]0x1C7CC8; targetRoot = [int64]0x189DAC; length = [int]0x103C; actorId = [int]0x01A5; note = "reuse Dark Hollow resident actor 0x01A5 dependency root" }
                    )
                    targetOriginalActor = "0x000E"
                    targetOriginalUse = "Dark Hollow actor 0x000E has no source moby rows in the current source scan and enough package capacity for the 0x0149 shell"
                    description = "Town Square spring shell actor 0x0149 copied over unused-looking Dark Hollow actor 0x000E while keeping Dark Hollow's local 0x00C2 flame/charge chest package"
                }
            }
        }
    }

    return $null
}

function Rebase-ExternalPackageInternalReferences([byte[]]$Bytes, $Recipe) {
    $mappings = @(Get-Field $Recipe "internalDependencyRebases" @())
    if ($mappings.Count -eq 0) { return "" }

    $rewrites = New-Object System.Collections.ArrayList
    for ($offset = 0; $offset -le ($Bytes.Length - 4); $offset += 4) {
        [uint64]$value = [uint64](Get-UInt32LE $Bytes $offset)
        foreach ($mapping in $mappings) {
            [uint64]$sourceRoot = [uint64]([int64](Get-Field $mapping "sourceRoot" -1))
            [uint64]$targetRoot = [uint64]([int64](Get-Field $mapping "targetRoot" -1))
            [uint64]$length = [uint64]([int](Get-Field $mapping "length" 0))
            if ($sourceRoot -eq [uint64]0 -or $targetRoot -eq [uint64]0 -or $length -eq [uint64]0) { continue }
            if ($value -lt $sourceRoot -or $value -ge ($sourceRoot + $length)) { continue }

            [uint64]$rebased = $targetRoot + ($value - $sourceRoot)
            Write-UInt32LE $Bytes $offset $rebased
            [string]$actorText = ""
            $actorIdRaw = Get-Field $mapping "actorId" $null
            if ($null -ne $actorIdRaw) {
                $actorText = " actor 0x$(([int]$actorIdRaw).ToString('X4'))"
            }
            [void]$rewrites.Add("+0x$($offset.ToString('X')) 0x$($value.ToString('X'))->0x$($rebased.ToString('X'))$actorText")
            break
        }
    }

    if ($rewrites.Count -eq 0) {
        return " No internal dependency pointers matched the configured rebase map."
    }

    return " Rebased internal dependency pointers: " + ([string]::Join("; ", @($rewrites.ToArray()))) + "."
}

function Get-RecipeRootRanges($Recipe) {
    $ranges = @()
    $sourceRootRanges = Get-Field $Recipe "sourceRootRanges" $null
    if ($null -ne $sourceRootRanges) {
        foreach ($range in @($sourceRootRanges)) {
            $start = [int64](Get-Field $range "start" -1)
            $end = [int64](Get-Field $range "end" -1)
            if ($start -ge 0 -and $end -gt $start) {
                $ranges += [ordered]@{
                    sourceStart = $start
                    sourceEnd = $end
                }
            }
        }
    }

    $sourceRootOffsets = Get-Field $Recipe "sourceRootOffsets" $null
    if ($ranges.Count -eq 0 -and $null -ne $sourceRootOffsets) {
        $offsets = @($sourceRootOffsets | ForEach-Object { [int64]$_ })
        for ($i = 0; $i + 1 -lt $offsets.Count; $i += 2) {
            if ($offsets[$i + 1] -gt $offsets[$i]) {
                $ranges += [ordered]@{
                    sourceStart = $offsets[$i]
                    sourceEnd = $offsets[$i + 1]
                }
            }
        }
    }

    if ($ranges.Count -eq 0) {
        $ranges += [ordered]@{
            sourceStart = [int64]$Recipe.sourceStart
            sourceEnd = [int64]$Recipe.sourceStart + [int64]$Recipe.copyLength
        }
    }

    return @($ranges)
}

function Find-FreePackageRootSlots([IO.FileStream]$Stream, $Layout, $Table, [int]$Count) {
    $entryBase = Get-LevelWadBaseOffset $Table
    [int[]]$rangeStartSlots = @()
    for ($slotRel = 0x50; $slotRel -le 0x148; $slotRel += 8) {
        [byte[]]$slotBytes = Read-WadBytes $Stream $Layout ($entryBase + [int64]$slotRel) 8
        $startValue = Get-UInt32LE $slotBytes 0
        $endValue = Get-UInt32LE $slotBytes 4
        if ($startValue -eq 0 -and $endValue -eq 0) {
            $rangeStartSlots += $slotRel
            if ($rangeStartSlots.Count -eq $Count) { return $rangeStartSlots }
        }
    }

    throw "$($Table.displayName) does not have $Count free package-root start/end ranges in the expected WAD root list."
}

function Get-PackageRootCapacityEnd([IO.FileStream]$Stream, $Layout, $Table, [int]$SlotRel, [uint64]$TargetRootStart) {
    $entryBase = Get-LevelWadBaseOffset $Table
    $bestNext = [uint64]0
    for ($slot = 0x50; $slot -le 0x148; $slot += 8) {
        if ($slot -eq $SlotRel) { continue }
        [byte[]]$slotBytes = Read-WadBytes $Stream $Layout ($entryBase + [int64]$slot) 8
        [uint64]$startValue = [uint64](Get-UInt32LE $slotBytes 0)
        if ($startValue -le $TargetRootStart) { continue }
        if ($bestNext -eq 0 -or $startValue -lt $bestNext) {
            $bestNext = $startValue
        }
    }

    if ($bestNext -ne 0) { return $bestNext }

    [byte[]]$subfileHeader = Read-WadBytes $Stream $Layout $entryBase 0x18
    [uint64]$sub2Start = [uint64](Get-UInt32LE $subfileHeader 0x10)
    [uint64]$sub2Length = [uint64](Get-UInt32LE $subfileHeader 0x14)
    if ($sub2Start -ne 0 -and $sub2Length -ne 0) {
        return $sub2Start + $sub2Length
    }

    return $TargetRootStart
}

function Add-ExternalChestPackageImportPatches(
    [IO.FileStream]$Stream,
    $Layout,
    $TargetTable,
    $SourceTable,
    $RecordMutation,
    [System.Collections.ArrayList]$PatchList,
    [hashtable]$ImportedPackages
) {
    $recipe = Get-ExternalChestPackageImportRecipe $TargetTable $SourceTable $RecordMutation
    if ($null -eq $recipe) { return "" }
    if ($ExperimentalExternalChestPackageMode -ne "CopyOnly" -and
        -not $AllowExperimentalPackageRootRegistration) {
        throw "External chest package-root registration is disabled by default. The Artisans tests crashed or soft-locked when imported chest actor roots were registered, so rerun only with -AllowExperimentalPackageRootRegistration for deliberate crash-risk diagnostics."
    }

    $importKey = [string]$recipe.id
    if ($ImportedPackages.ContainsKey($importKey)) {
        return " External chest package import already queued: $($recipe.description)."
    }

    $sourceEntryBase = Get-LevelWadBaseOffset $SourceTable
    $targetEntryBase = Get-LevelWadBaseOffset $TargetTable
    $sourceWadOffset = $sourceEntryBase + [int64]$recipe.sourceStart
    $targetWadOffset = $targetEntryBase + [int64]$recipe.targetStart
    [int]$copyLength = [int]$recipe.copyLength

    [byte[]]$packageBytes = Read-WadBytes $Stream $Layout $sourceWadOffset $copyLength
    [string]$internalRebaseDescription = Rebase-ExternalPackageInternalReferences $packageBytes $recipe

    if ($ExperimentalExternalChestPackageMode -eq "RegisterCompanionRoot") {
        $copySegments = Get-Field $recipe "copySegments" $null
        if ($null -ne $copySegments) {
            foreach ($segment in @($copySegments)) {
                [int64]$segmentSourceStart = [int64](Get-Field $segment "sourceStart" -1)
                [int64]$segmentTargetStart = [int64](Get-Field $segment "targetStart" -1)
                [int]$segmentLength = [int](Get-Field $segment "length" 0)
                if ($segmentSourceStart -lt 0 -or $segmentTargetStart -lt 0 -or $segmentLength -le 0) {
                    throw "External chest package recipe '$($recipe.id)' has an invalid split copy segment."
                }

                [byte[]]$segmentTargetBefore = Read-WadBytes $Stream $Layout ($targetEntryBase + $segmentTargetStart) $segmentLength
                if (-not (Test-AllZero $segmentTargetBefore)) {
                    throw "$($TargetTable.displayName) external chest split-package target 0x$(([int64]($targetEntryBase + $segmentTargetStart)).ToString('X')) is not empty."
                }

                [byte[]]$segmentBytes = Read-WadBytes $Stream $Layout ($sourceEntryBase + $segmentSourceStart) $segmentLength
                [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-split-zero-gap-import-experimental" ($targetEntryBase + $segmentTargetStart) $segmentBytes ("Import split segment for $($recipe.description) from $($SourceTable.displayName) entry-relative 0x$($segmentSourceStart.ToString('X'))-0x$(([int64]($segmentSourceStart + $segmentLength)).ToString('X')) into $($TargetTable.displayName) entry-relative 0x$($segmentTargetStart.ToString('X')).")))
            }

            $rootEntries = @(Get-Field $recipe "rootEntries" @())
            if ($rootEntries.Count -gt 0) {
                foreach ($entry in $rootEntries) {
                    [int]$targetRootSlot = [int](Get-Field $entry "targetRootSlot" -1)
                    [uint64]$targetRoot = [uint64]([int64](Get-Field $entry "targetRoot" -1))
                    [int]$actorId = [int](Get-Field $entry "actorId" -1)
                    if ($targetRootSlot -lt 0 -or (($targetRootSlot - 0x50) % 4) -ne 0 -or $targetRoot -eq [uint64]0 -or $actorId -lt 0 -or $actorId -gt 0xFFFF) {
                        throw "External chest package recipe '$($recipe.id)' has an invalid root/actor-id entry."
                    }

                    [byte[]]$slotBefore = Read-WadBytes $Stream $Layout ($targetEntryBase + [int64]$targetRootSlot) 4
                    if ((Get-UInt32LE $slotBefore 0) -ne 0) {
                        throw "$($TargetTable.displayName) external chest package root slot +0x$($targetRootSlot.ToString('X')) is not empty."
                    }

                    [int]$rootIndex = [int](($targetRootSlot - 0x50) / 4)
                    [int64]$actorIdWadOffset = $targetEntryBase + 0x150 + ([int64]$rootIndex * 2)
                    [byte[]]$actorBefore = Read-WadBytes $Stream $Layout $actorIdWadOffset 2
                    $existingActorId = [int][BitConverter]::ToUInt16($actorBefore, 0)
                    if ($existingActorId -ne 0) {
                        throw "$($TargetTable.displayName) actor-id list entry $rootIndex for root slot +0x$($targetRootSlot.ToString('X')) is already 0x$($existingActorId.ToString('X4'))."
                    }

                    [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-root-list-register" ($targetEntryBase + [int64]$targetRootSlot) (New-UInt32LEBytes $targetRoot) ("Register imported $($recipe.description) root index $rootIndex in $($TargetTable.displayName) root slot +0x$($targetRootSlot.ToString('X')) with start 0x$($targetRoot.ToString('X')).")))

                    [byte[]]$actorBytes = [BitConverter]::GetBytes([uint16]$actorId)
                    [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-actor-id-list-register" $actorIdWadOffset $actorBytes ("Map $($TargetTable.displayName) actor-id list entry $rootIndex to actor 0x$($actorId.ToString('X4')) for imported $($recipe.description).")))
                }

                $replaceRootEntries = @(Get-Field $recipe "replaceRootEntries" @())
                foreach ($entry in $replaceRootEntries) {
                    [int]$targetRootSlot = [int](Get-Field $entry "targetRootSlot" -1)
                    [uint64]$targetRoot = [uint64]([int64](Get-Field $entry "targetRoot" -1))
                    [int]$actorId = [int](Get-Field $entry "actorId" -1)
                    if ($targetRootSlot -lt 0 -or (($targetRootSlot - 0x50) % 4) -ne 0 -or $targetRoot -eq [uint64]0 -or $actorId -lt 0 -or $actorId -gt 0xFFFF) {
                        throw "External chest package recipe '$($recipe.id)' has an invalid replacement root/actor-id entry."
                    }

                    [int]$rootIndex = [int](($targetRootSlot - 0x50) / 4)
                    [int64]$actorIdWadOffset = $targetEntryBase + 0x150 + ([int64]$rootIndex * 2)
                    [byte[]]$oldRootBytes = Read-WadBytes $Stream $Layout ($targetEntryBase + [int64]$targetRootSlot) 4
                    [byte[]]$oldActorBytes = Read-WadBytes $Stream $Layout $actorIdWadOffset 2
                    [uint64]$oldRoot = [uint64](Get-UInt32LE $oldRootBytes 0)
                    [int]$oldActorId = [int][BitConverter]::ToUInt16($oldActorBytes, 0)

                    [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-root-list-remap" ($targetEntryBase + [int64]$targetRootSlot) (New-UInt32LEBytes $targetRoot) ("Remap $($TargetTable.displayName) actor-root index $rootIndex from root 0x$($oldRoot.ToString('X')) actor 0x$($oldActorId.ToString('X4')) to split imported root 0x$($targetRoot.ToString('X')) actor 0x$($actorId.ToString('X4')).")))
                    [byte[]]$actorBytes = [BitConverter]::GetBytes([uint16]$actorId)
                    [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-actor-id-list-remap" $actorIdWadOffset $actorBytes ("Map $($TargetTable.displayName) actor-id list entry $rootIndex to actor 0x$($actorId.ToString('X4')) for split imported $($recipe.description).")))
                }

                $ImportedPackages[$importKey] = $true
                return " Imported external chest actor package into Artisans zero gap and registered root/actor-id list entries: $($recipe.description)."
            }

            $rootPairs = @(Get-Field $recipe "rootPairs" @())
            if ($rootPairs.Count -eq 0) {
                throw "External chest package recipe '$($recipe.id)' has split copies but no rootEntries/rootPairs."
            }

            foreach ($pair in $rootPairs) {
                [int]$targetRootSlot = [int](Get-Field $pair "targetRootSlot" -1)
                [uint64]$targetFirst = [uint64]([int64](Get-Field $pair "targetFirst" -1))
                [uint64]$targetSecond = [uint64]([int64](Get-Field $pair "targetSecond" -1))
                if ($targetRootSlot -lt 0 -or $targetFirst -eq [uint64]0 -or $targetSecond -eq [uint64]0) {
                    throw "External chest package recipe '$($recipe.id)' has an invalid root pair."
                }

                [byte[]]$slotBefore = Read-WadBytes $Stream $Layout ($targetEntryBase + [int64]$targetRootSlot) 8
                if ((Get-UInt32LE $slotBefore 0) -ne 0 -or (Get-UInt32LE $slotBefore 4) -ne 0) {
                    throw "$($TargetTable.displayName) external chest package root slot +0x$($targetRootSlot.ToString('X')) is not empty."
                }

                [byte[]]$rangeBytes = New-Object byte[] 8
                [Array]::Copy((New-UInt32LEBytes $targetFirst), 0, $rangeBytes, 0, 4)
                [Array]::Copy((New-UInt32LEBytes $targetSecond), 0, $rangeBytes, 4, 4)
                [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-split-zero-gap-root-register" ($targetEntryBase + [int64]$targetRootSlot) $rangeBytes ("Register split imported $($recipe.description) in $($TargetTable.displayName) blank actor-root slot +0x$($targetRootSlot.ToString('X')) with start 0x$($targetFirst.ToString('X')) and companion package start 0x$($targetSecond.ToString('X')). Existing Artisans actor packages are left untouched.")))
            }

            $ImportedPackages[$importKey] = $true
            return " Imported external chest actor package into split zero-filled Artisans gaps and registered new root slots: $($recipe.description)."
        }

        [byte[]]$targetBefore = Read-WadBytes $Stream $Layout $targetWadOffset $copyLength
        if (-not (Test-AllZero $targetBefore)) {
            throw "$($TargetTable.displayName) external chest package zero-gap target 0x$($targetWadOffset.ToString('X')) is not empty."
        }

        [int64]$sourcePartnerStart = [int64](Get-Field $recipe "sourcePartnerStart" ([int64]$recipe.sourceStart + [int64]$recipe.copyLength))
        if ($sourcePartnerStart -lt [int64]$recipe.sourceStart -or $sourcePartnerStart -ge ([int64]$recipe.sourceStart + [int64]$recipe.copyLength)) {
            throw "External chest package recipe '$($recipe.id)' has invalid sourcePartnerStart 0x$($sourcePartnerStart.ToString('X'))."
        }
        [uint64]$targetPartnerStart = [uint64](([int64]$recipe.targetStart) + ($sourcePartnerStart - [int64]$recipe.sourceStart))

        $targetRootSlot = [int](Get-Field $recipe "targetRootSlot" -1)
        if ($targetRootSlot -lt 0) {
            [int[]]$freeSlots = Find-FreePackageRootSlots $Stream $Layout $TargetTable 1
            $targetRootSlot = [int]$freeSlots[0]
        }
        else {
            [byte[]]$slotBefore = Read-WadBytes $Stream $Layout ($targetEntryBase + [int64]$targetRootSlot) 8
            if ((Get-UInt32LE $slotBefore 0) -ne 0 -or (Get-UInt32LE $slotBefore 4) -ne 0) {
                throw "$($TargetTable.displayName) external chest package root slot +0x$($targetRootSlot.ToString('X')) is not empty."
            }
        }

        [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-zero-gap-import-experimental" $targetWadOffset $packageBytes ("Import $($recipe.description) from $($SourceTable.displayName) entry-relative 0x$(([int64]$recipe.sourceStart).ToString('X'))-0x$(([int64]$recipe.sourceStart + [int64]$recipe.copyLength).ToString('X')) into $($TargetTable.displayName) zero-filled actor-package gap at entry-relative 0x$(([int64]$recipe.targetStart).ToString('X')).$internalRebaseDescription")))

        [byte[]]$rangeBytes = New-Object byte[] 8
        [Array]::Copy((New-UInt32LEBytes ([uint64]([int64]$recipe.targetStart))), 0, $rangeBytes, 0, 4)
        [Array]::Copy((New-UInt32LEBytes $targetPartnerStart), 0, $rangeBytes, 4, 4)
        [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-zero-gap-root-register" ($targetEntryBase + [int64]$targetRootSlot) $rangeBytes ("Register imported $($recipe.description) in $($TargetTable.displayName) blank actor-root slot +0x$($targetRootSlot.ToString('X')) with start 0x$(([int64]$recipe.targetStart).ToString('X')) and companion package start 0x$($targetPartnerStart.ToString('X')). Existing Artisans tulip/scenery packages are left untouched.")))

        $ImportedPackages[$importKey] = $true
        return " Imported external chest actor package into zero-filled Artisans actor-package gap and registered a new root slot: $($recipe.description)."
    }

    if ($ExperimentalExternalChestPackageMode -eq "ReplaceUnusedRoot") {
        $targetRootSlot = [int](Get-Field $recipe "targetRootSlot" -1)
        if ($targetRootSlot -lt 0) {
            throw "External chest package recipe '$($recipe.id)' is missing targetRootSlot for ReplaceUnusedRoot mode."
        }

        $replaceRootEntries = @(Get-Field $recipe "replaceRootEntries" @() | Sort-Object { [int](Get-Field $_ "targetRootSlot" -1) })
        if ($replaceRootEntries.Count -gt 0) {
            $firstReplaceEntry = $replaceRootEntries[0]
            [int]$firstReplaceSlot = [int](Get-Field $firstReplaceEntry "targetRootSlot" -1)
            [uint64]$firstReplaceRoot = [uint64]([int64](Get-Field $firstReplaceEntry "targetRoot" -1))
            if ($firstReplaceSlot -ne $targetRootSlot -or $firstReplaceRoot -ne [uint64]([int64]$recipe.targetStart)) {
                throw "External chest package recipe '$($recipe.id)' replaceRootEntries must start at targetRootSlot +0x$($targetRootSlot.ToString('X')) / targetStart 0x$(([int64]$recipe.targetStart).ToString('X'))."
            }

            [byte[]]$actorSegmentHeader = Read-WadBytes $Stream $Layout $targetEntryBase 0x18
            [uint64]$actorSegmentStart = [uint64](Get-UInt32LE $actorSegmentHeader 0x10)
            [uint64]$actorSegmentLength = [uint64](Get-UInt32LE $actorSegmentHeader 0x14)
            [uint64]$actorSegmentEnd = $actorSegmentStart + $actorSegmentLength
            [uint64]$targetStart = [uint64]([int64]$recipe.targetStart)
            [uint64]$targetCopyEnd = $targetStart + [uint64]$copyLength
            if ($actorSegmentStart -eq 0 -or $actorSegmentLength -eq 0 -or $targetStart -lt $actorSegmentStart -or $targetCopyEnd -gt $actorSegmentEnd) {
                throw "$($TargetTable.displayName) replacement package target 0x$($targetStart.ToString('X'))-0x$($targetCopyEnd.ToString('X')) is outside the actor package segment 0x$($actorSegmentStart.ToString('X'))-0x$($actorSegmentEnd.ToString('X'))."
            }

            $replacedSlots = @{}
            $clearRootEntries = @(Get-Field $recipe "clearRootEntries" @() | Sort-Object { [int](Get-Field $_ "targetRootSlot" -1) })
            foreach ($entry in $replaceRootEntries) {
                [int]$entrySlot = [int](Get-Field $entry "targetRootSlot" -1)
                [uint64]$entryRoot = [uint64]([int64](Get-Field $entry "targetRoot" -1))
                [int]$actorId = [int](Get-Field $entry "actorId" -1)
                if ($entrySlot -lt 0 -or (($entrySlot - 0x50) % 4) -ne 0 -or $entryRoot -eq [uint64]0 -or $actorId -lt 0 -or $actorId -gt 0xFFFF) {
                    throw "External chest package recipe '$($recipe.id)' has an invalid replacement root entry."
                }
                if ($entryRoot -lt $targetStart -or $entryRoot -ge $targetCopyEnd) {
                    throw "External chest package recipe '$($recipe.id)' maps root slot +0x$($entrySlot.ToString('X')) to 0x$($entryRoot.ToString('X')), outside copied package span 0x$($targetStart.ToString('X'))-0x$($targetCopyEnd.ToString('X'))."
                }
                $replacedSlots[$entrySlot.ToString()] = $true
            }
            foreach ($entry in $clearRootEntries) {
                [int]$entrySlot = [int](Get-Field $entry "targetRootSlot" -1)
                if ($entrySlot -lt 0 -or (($entrySlot - 0x50) % 4) -ne 0) {
                    throw "External chest package recipe '$($recipe.id)' has an invalid clear-root entry."
                }
                $replacedSlots[$entrySlot.ToString()] = $true
            }

            [uint64]$capacityEnd = [uint64]0
            for ($slot = 0x50; $slot -le 0x148; $slot += 4) {
                if ($replacedSlots.ContainsKey($slot.ToString())) { continue }
                [byte[]]$slotBytes = Read-WadBytes $Stream $Layout ($targetEntryBase + [int64]$slot) 4
                [uint64]$startValue = [uint64](Get-UInt32LE $slotBytes 0)
                if ($startValue -le $targetStart) { continue }
                if ($capacityEnd -eq 0 -or $startValue -lt $capacityEnd) {
                    $capacityEnd = $startValue
                }
            }
            if ($capacityEnd -eq 0) { $capacityEnd = $actorSegmentEnd }
            if ($targetCopyEnd -gt $capacityEnd) {
                throw "$($TargetTable.displayName) replacement package '$($recipe.id)' needs 0x$($copyLength.ToString('X')) bytes, but the selected replacement root run only has 0x$(([int64]($capacityEnd - $targetStart)).ToString('X')) bytes before the next untouched actor root."
            }

            [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-root-swap-experimental" $targetWadOffset $packageBytes ("Swap $($recipe.description) from $($SourceTable.displayName) entry-relative 0x$(([int64]$recipe.sourceStart).ToString('X'))-0x$(([int64]$recipe.sourceStart + [int64]$recipe.copyLength).ToString('X')) into $($TargetTable.displayName) actor package segment at entry-relative 0x$(([int64]$recipe.targetStart).ToString('X')). Original actor roots $([string](Get-Field $recipe 'targetOriginalActor' 'unknown')) are intentionally replaced only for this isolated test.$internalRebaseDescription")))

            $leftoverLength = [int]([int64]$capacityEnd - [int64]$targetCopyEnd)
            if ($leftoverLength -gt 0) {
                [byte[]]$zeroFill = New-Object byte[] $leftoverLength
                [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-root-swap-clear-tail" ($targetEntryBase + [int64]$targetCopyEnd) $zeroFill ("Clear 0x$($leftoverLength.ToString('X')) stale bytes after the imported actor package span before the next untouched actor root.")))
            }

            foreach ($entry in $replaceRootEntries) {
                [int]$entrySlot = [int](Get-Field $entry "targetRootSlot" -1)
                [uint64]$entryRoot = [uint64]([int64](Get-Field $entry "targetRoot" -1))
                [int]$actorId = [int](Get-Field $entry "actorId" -1)
                [int]$rootIndex = [int](($entrySlot - 0x50) / 4)
                [int64]$actorIdWadOffset = $targetEntryBase + 0x150 + ([int64]$rootIndex * 2)
                [byte[]]$oldRootBytes = Read-WadBytes $Stream $Layout ($targetEntryBase + [int64]$entrySlot) 4
                [byte[]]$oldActorBytes = Read-WadBytes $Stream $Layout $actorIdWadOffset 2
                [uint64]$oldRoot = [uint64](Get-UInt32LE $oldRootBytes 0)
                [int]$oldActorId = [int][BitConverter]::ToUInt16($oldActorBytes, 0)

                [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-root-list-remap" ($targetEntryBase + [int64]$entrySlot) (New-UInt32LEBytes $entryRoot) ("Remap $($TargetTable.displayName) actor-root index $rootIndex from root 0x$($oldRoot.ToString('X')) actor 0x$($oldActorId.ToString('X4')) to imported root 0x$($entryRoot.ToString('X')) actor 0x$($actorId.ToString('X4')).")))
                [byte[]]$actorBytes = [BitConverter]::GetBytes([uint16]$actorId)
                [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-actor-id-list-remap" $actorIdWadOffset $actorBytes ("Map $($TargetTable.displayName) actor-id list entry $rootIndex to actor 0x$($actorId.ToString('X4')) for imported $($recipe.description).")))
            }
            foreach ($entry in $clearRootEntries) {
                [int]$entrySlot = [int](Get-Field $entry "targetRootSlot" -1)
                [int]$rootIndex = [int](($entrySlot - 0x50) / 4)
                [int64]$actorIdWadOffset = $targetEntryBase + 0x150 + ([int64]$rootIndex * 2)
                [byte[]]$oldRootBytes = Read-WadBytes $Stream $Layout ($targetEntryBase + [int64]$entrySlot) 4
                [byte[]]$oldActorBytes = Read-WadBytes $Stream $Layout $actorIdWadOffset 2
                [uint64]$oldRoot = [uint64](Get-UInt32LE $oldRootBytes 0)
                [int]$oldActorId = [int][BitConverter]::ToUInt16($oldActorBytes, 0)

                [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-root-list-clear" ($targetEntryBase + [int64]$entrySlot) (New-UInt32LEBytes 0) ("Clear $($TargetTable.displayName) actor-root index $rootIndex from stale root 0x$($oldRoot.ToString('X')) actor 0x$($oldActorId.ToString('X4')) because it now falls inside the imported locked-chest dependency span.")))
                [byte[]]$actorBytes = [BitConverter]::GetBytes([uint16]0)
                [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-actor-id-list-clear" $actorIdWadOffset $actorBytes ("Clear $($TargetTable.displayName) actor-id list entry $rootIndex for stale root slot +0x$($entrySlot.ToString('X')).")))
            }
            foreach ($entry in @(Get-Field $recipe "actorIdOnlyRemaps" @())) {
                [int]$entrySlot = [int](Get-Field $entry "targetRootSlot" -1)
                [int]$actorId = [int](Get-Field $entry "actorId" -1)
                if ($entrySlot -lt 0 -or (($entrySlot - 0x50) % 4) -ne 0 -or $actorId -lt 0 -or $actorId -gt 0xFFFF) {
                    throw "External chest package recipe '$($recipe.id)' has an invalid actor-id-only remap entry."
                }

                [int]$rootIndex = [int](($entrySlot - 0x50) / 4)
                [int64]$actorIdWadOffset = $targetEntryBase + 0x150 + ([int64]$rootIndex * 2)
                [byte[]]$oldActorBytes = Read-WadBytes $Stream $Layout $actorIdWadOffset 2
                [int]$oldActorId = [int][BitConverter]::ToUInt16($oldActorBytes, 0)
                [byte[]]$actorBytes = [BitConverter]::GetBytes([uint16]$actorId)
                [string]$note = [string](Get-Field $entry "note" "")
                if (-not [string]::IsNullOrWhiteSpace($note)) { $note = " $note" }
                [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-actor-id-list-remap" $actorIdWadOffset $actorBytes ("Map $($TargetTable.displayName) actor-id list entry $rootIndex for unchanged root slot +0x$($entrySlot.ToString('X')) from actor 0x$($oldActorId.ToString('X4')) to actor 0x$($actorId.ToString('X4')).$note")))
            }

            $ImportedPackages[$importKey] = $true
            return " Imported external chest actor package via actor-segment root/id remap: $($recipe.description)."
        }

        [int64]$slotWadOffset = $targetEntryBase + [int64]$targetRootSlot
        [byte[]]$slotBefore = Read-WadBytes $Stream $Layout $slotWadOffset 8
        [uint64]$originalRootStart = [uint64](Get-UInt32LE $slotBefore 0)
        [uint64]$originalSecondPackageStart = [uint64](Get-UInt32LE $slotBefore 4)
        if ($originalRootStart -ne [uint64]([int64]$recipe.targetStart)) {
            throw "$($TargetTable.displayName) external chest package slot +0x$($targetRootSlot.ToString('X')) starts at 0x$($originalRootStart.ToString('X')), not expected target 0x$(([int64]$recipe.targetStart).ToString('X'))."
        }

        [uint64]$capacityEnd = Get-PackageRootCapacityEnd $Stream $Layout $TargetTable $targetRootSlot $originalRootStart
        if ($capacityEnd -le $originalRootStart) {
            throw "$($TargetTable.displayName) external chest package slot +0x$($targetRootSlot.ToString('X')) has no finite replaceable package capacity."
        }

        [uint64]$targetCopyEnd = [uint64]([int64]$recipe.targetStart + [int64]$copyLength)
        if ($targetCopyEnd -gt $capacityEnd) {
            throw "$($TargetTable.displayName) external chest package '$($recipe.id)' needs 0x$($copyLength.ToString('X')) bytes, but root slot +0x$($targetRootSlot.ToString('X')) only has 0x$(([int64]($capacityEnd - $originalRootStart)).ToString('X')) bytes before the next actor root."
        }

        [int64]$sourcePartnerStart = [int64](Get-Field $recipe "sourcePartnerStart" ([int64]$recipe.sourceStart + [int64]$recipe.copyLength))
        if ($sourcePartnerStart -lt [int64]$recipe.sourceStart -or $sourcePartnerStart -ge ([int64]$recipe.sourceStart + [int64]$recipe.copyLength)) {
            throw "External chest package recipe '$($recipe.id)' has invalid sourcePartnerStart 0x$($sourcePartnerStart.ToString('X'))."
        }
        [uint64]$targetPartnerStart = [uint64](([int64]$recipe.targetStart) + ($sourcePartnerStart - [int64]$recipe.sourceStart))

        [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-root-swap-experimental" $targetWadOffset $packageBytes ("Swap $($recipe.description) from $($SourceTable.displayName) entry-relative 0x$(([int64]$recipe.sourceStart).ToString('X'))-0x$(([int64]$recipe.sourceStart + [int64]$recipe.copyLength).ToString('X')) into $($TargetTable.displayName) root slot +0x$($targetRootSlot.ToString('X')) at entry-relative 0x$(([int64]$recipe.targetStart).ToString('X')). Root slot second package moves from 0x$($originalSecondPackageStart.ToString('X')) to 0x$($targetPartnerStart.ToString('X')). Original actor $([string](Get-Field $recipe 'targetOriginalActor' 'unknown')) was selected because it is $([string](Get-Field $recipe 'targetOriginalUse' 'not currently known to be required')).$internalRebaseDescription")))

        $leftoverLength = [int]([int64]$capacityEnd - [int64]$targetCopyEnd)
        if ($leftoverLength -gt 0) {
            [byte[]]$zeroFill = New-Object byte[] $leftoverLength
            [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-root-swap-clear-tail" ($targetEntryBase + [int64]$targetCopyEnd) $zeroFill ("Clear 0x$($leftoverLength.ToString('X')) stale bytes after the swapped package span so the loader does not see the old package tail before the next actor root.")))
        }

        [byte[]]$rangeBytes = New-Object byte[] 8
        [Array]::Copy((New-UInt32LEBytes $originalRootStart), 0, $rangeBytes, 0, 4)
        [Array]::Copy((New-UInt32LEBytes $targetPartnerStart), 0, $rangeBytes, 4, 4)
        [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-root-swap-range" $slotWadOffset $rangeBytes ("Retarget $($TargetTable.displayName) actor-root slot +0x$($targetRootSlot.ToString('X')) to imported $($recipe.description) start 0x$($originalRootStart.ToString('X')) with companion package start 0x$($targetPartnerStart.ToString('X')).")))

        $ImportedPackages[$importKey] = $true
        return " Imported external chest actor package via unused-root swap: $($recipe.description)."
    }

    [byte[]]$targetBefore = Read-WadBytes $Stream $Layout $targetWadOffset $copyLength
    if (-not (Test-AllZero $targetBefore)) {
        throw "$($TargetTable.displayName) external chest package target 0x$($targetWadOffset.ToString('X')) is not empty."
    }

    [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-import-experimental" $targetWadOffset $packageBytes ("Import $($recipe.description) from $($SourceTable.displayName) entry-relative 0x$(([int64]$recipe.sourceStart).ToString('X')) to $($TargetTable.displayName) entry-relative 0x$(([int64]$recipe.targetStart).ToString('X')).$internalRebaseDescription")))

    $rootRanges = @()
    if ($ExperimentalExternalChestPackageMode -eq "RegisterPrimaryRoot") {
        $allRootRanges = @(Get-RecipeRootRanges $recipe)
        if ($allRootRanges.Count -gt 0) { $rootRanges = @($allRootRanges[0]) }
    }
    elseif ($ExperimentalExternalChestPackageMode -eq "RegisterSecondaryRoot") {
        $allRootRanges = @(Get-RecipeRootRanges $recipe)
        if ($allRootRanges.Count -gt 1) { $rootRanges = @($allRootRanges[1]) }
    }
    elseif ($ExperimentalExternalChestPackageMode -eq "RegisterAllRoots") {
        $rootRanges = @(Get-RecipeRootRanges $recipe)
    }

    if ($rootRanges.Count -gt 0) {
        [int[]]$freeSlots = Find-FreePackageRootSlots $Stream $Layout $TargetTable $rootRanges.Count
        for ($i = 0; $i -lt $rootRanges.Count; $i++) {
            $range = $rootRanges[$i]
            [int64]$sourceRootStart = [int64](Get-Field $range "sourceStart" -1)
            [int64]$sourceRootEnd = [int64](Get-Field $range "sourceEnd" -1)
            if ($sourceRootStart -lt 0 -or $sourceRootEnd -le $sourceRootStart) {
                throw "Invalid package root range in $($recipe.description): start=$sourceRootStart end=$sourceRootEnd"
            }

            [uint64]$targetRootStart = [uint64](([int64]$recipe.targetStart) + ($sourceRootStart - [int64]$recipe.sourceStart))
            [uint64]$targetRootEnd = [uint64]($targetRootStart + ($sourceRootEnd - $sourceRootStart))
            [int64]$slotWadOffset = $targetEntryBase + [int64]$freeSlots[$i]
            [byte[]]$rangeBytes = New-Object byte[] 8
            [Array]::Copy((New-UInt32LEBytes $targetRootStart), 0, $rangeBytes, 0, 4)
            [Array]::Copy((New-UInt32LEBytes $targetRootEnd), 0, $rangeBytes, 4, 4)
            [void]$PatchList.Add((New-RawPatch $TargetTable $Layout "moby-package-root-import-experimental" $slotWadOffset $rangeBytes ("Register imported $($recipe.description) root range 0x$($targetRootStart.ToString('X'))-0x$($targetRootEnd.ToString('X')) in $($TargetTable.displayName) package-root slots +0x$($freeSlots[$i].ToString('X'))/+0x$(([int]$freeSlots[$i] + 4).ToString('X')).")))
        }
    }

    $ImportedPackages[$importKey] = $true
    return " Imported external chest package data: $($recipe.description) using mode $ExperimentalExternalChestPackageMode."
}

function New-SpecialDataPatch($Table, $Layout, $Edit, [int]$TrueIndex, [int64]$WadOffset, [string]$FieldOffset, [byte[]]$Bytes, [string]$Description) {
    return [ordered]@{
        levelKey = [string]$Table.levelKey
        levelName = [string]$Table.displayName
        sourceEntry = [int]$Table.wadEntry
        index = [int](Get-Field $Edit "index" -1)
        trueIndex = $TrueIndex
        label = [string](Get-Field $Edit "label" "")
        kind = "moby-special-data"
        description = $Description
        recordOffset = $FieldOffset
        wadRelativeOffset = ("0x{0:X}" -f $WadOffset)
        imageOffset = Convert-DiscFileOffsetToImageOffset $Layout $WadLba $WadOffset
        bytesHex = Convert-BytesToHex $Bytes
    }
}

function New-DirectImagePatch($Table, [string]$Kind, [int64]$ImageOffset, [byte[]]$Bytes, [string]$Description, [int]$BeforeValue, [int]$AfterValue) {
    return [ordered]@{
        levelKey = [string]$Table.levelKey
        levelName = [string]$Table.displayName
        sourceEntry = -1
        index = -1
        trueIndex = -1
        label = "Inventory treasure total"
        kind = $Kind
        description = $Description
        recordOffset = ""
        wadRelativeOffset = ""
        imageOffset = $ImageOffset
        beforeValue = $BeforeValue
        afterValue = $AfterValue
        bytesHex = Convert-BytesToHex $Bytes
    }
}

function Get-GemValueFromColor([string]$Color) {
    $text = if ($null -eq $Color) { "" } else { $Color.Trim().ToLowerInvariant() }
    switch ($text) {
        "red" { return 1 }
        "green" { return 2 }
        "blue" { return 5 }
        "yellow" { return 10 }
        "purple" { return 25 }
        default { return 0 }
    }
}

function Get-GemValueFromByteHex([string]$Hex) {
    $text = if ($null -eq $Hex) { "" } else { $Hex.Trim() }
    if ($text.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        $text = $text.Substring(2)
    }
    if ($text.Length -eq 0) { return 0 }
    $value = [Convert]::ToInt32($text, 16)
    switch ($value) {
        0x53 { return 1 }
        0x54 { return 2 }
        0x55 { return 5 }
        0x56 { return 10 }
        0x57 { return 25 }
        default { return 0 }
    }
}

function Get-GemValueFromGemIdByte([int]$Value) {
    switch ($Value) {
        0x53 { return 1 }
        0x54 { return 2 }
        0x55 { return 5 }
        0x56 { return 10 }
        0x57 { return 25 }
        default { return 0 }
    }
}

function Get-GemValueFromOrdinalByte([int]$Value) {
    switch ($Value) {
        1 { return 1 }
        2 { return 2 }
        3 { return 5 }
        4 { return 10 }
        5 { return 25 }
        default { return 0 }
    }
}

function Get-LooseGemValueFromLabel([string]$Label) {
    $text = if ($null -eq $Label) { "" } else { $Label.ToLowerInvariant() }
    if ($text -match "purple gem|25-gem|gem \(25\)|\(25\)") { return 25 }
    if ($text -match "yellow gem|10-gem|gem \(10\)|\(10\)") { return 10 }
    if ($text -match "blue gem|5-gem|gem \(5\)|\(5\)") { return 5 }
    if ($text -match "green gem|2-gem|gem \(2\)|\(2\)") { return 2 }
    if ($text -match "red gem|1-gem|gem \(1\)|\(1\)") { return 1 }
    return 0
}

function Get-EditedByteValue($Record, [int]$Offset) {
    foreach ($byteEdit in @(Get-ArrayField $Record "sourceByteEdits")) {
        $rawOffset = Get-Field $byteEdit "offset" (Get-Field $byteEdit "offsetHex" $null)
        if ($null -eq $rawOffset) { continue }
        $byteOffset = Convert-PatchInt $rawOffset "sourceByteEdits.offset"
        if ($byteOffset -ne $Offset) { continue }
        $rawValue = Get-Field $byteEdit "value" (Get-Field $byteEdit "valueHex" $null)
        if ($null -eq $rawValue) { continue }
        return Convert-PatchInt $rawValue "sourceByteEdits.value"
    }
    return $null
}

function Test-ChestContentRecord($Record) {
    if ($null -eq $Record) { return $false }
    $rewardColorEdit = Get-Field $Record "rewardColorEdit" $null
    $rewardMode = ([string](Get-Field $rewardColorEdit "mode" "")).ToLowerInvariant()
    if ($rewardMode.Contains("contained") -or $rewardMode.Contains("locked-chest") -or $rewardMode.Contains("chest-content")) { return $true }

    $label = ([string](Get-Field $Record "displayTargetLabel" (Get-Field $Record "label" ""))).ToLowerInvariant()
    return $label.Contains("chest content") -or
        $label.Contains("contained gem") -or
        $label.Contains("gem explosion") -or
        $label.Contains("linked reward marker") -or
        $label.Contains("reward marker")
}

function Test-LinkedChestContentAppend($Record) {
    $mutation = Get-Field $Record "recordMutation" $null
    $family = ([string](Get-Field $mutation "sourceFamily" "")).Trim().ToLowerInvariant()
    if ($family -eq "chestcontent") { return $false }
    if (-not (Test-ChestContentRecord $Record)) { return $false }
    return $null -ne (Get-Field $Record "chestContentLinkEdit" $null)
}

function Get-RecordTreasureValue($Record) {
    if ($null -eq $Record) { return 0 }
    $mutation = Get-Field $Record "recordMutation" $null
    if ([string](Get-Field $mutation "mode" "") -eq "hide") { return 0 }

    $label = [string](Get-Field $Record "displayTargetLabel" (Get-Field $Record "label" ""))
    $loose = Get-LooseGemValueFromLabel $label
    $reward = Get-GemValueFromByteHex ([string](Get-Field $Record "flag53Hex" (Get-Field $Record "flag4BHex" "")))

    $editedGemId = Get-EditedByteValue $Record 0x36
    $editedGemOrdinal = Get-EditedByteValue $Record 0x4F
    if ($null -ne $editedGemOrdinal) {
        $override = Get-GemValueFromOrdinalByte ([int]$editedGemOrdinal)
        if ($override -gt 0) { $loose = $override }
    }
    if ($null -ne $editedGemId) {
        $override = Get-GemValueFromGemIdByte ([int]$editedGemId)
        if ($override -gt 0) { $loose = $override }
    }

    $editedRewardId = Get-EditedByteValue $Record 0x53
    if ($null -ne $editedRewardId) {
        $override = Get-GemValueFromGemIdByte ([int]$editedRewardId)
        if ($override -gt 0) { $reward = $override }
    }

    $gemColorEdit = Get-Field $Record "gemColorEdit" $null
    if ($null -ne $gemColorEdit) {
        $override = Get-GemValueFromColor ([string](Get-Field $gemColorEdit "color" ""))
        if ($override -gt 0) { $loose = $override }
    }

    $rewardColorEdit = Get-Field $Record "rewardColorEdit" $null
    if ($null -ne $rewardColorEdit) {
        $override = Get-GemValueFromColor ([string](Get-Field $rewardColorEdit "color" ""))
        if ($override -gt 0) { $reward = $override }
    }

    if (Test-ChestContentRecord $Record) { return $reward }
    return $loose + $reward
}

function Get-SourceRecordTreasureValue([byte[]]$RecordBytes) {
    if ($null -eq $RecordBytes -or $RecordBytes.Length -lt 0x54) { return 0 }

    $type = [int]$RecordBytes[0x50]
    $flag4A = [int]$RecordBytes[0x52]
    $flag4B = [int]$RecordBytes[0x53]

    if ($type -eq 0x18 -and $flag4A -eq 0x40 -and $flag4B -eq 0xFF) {
        $valueFromOrdinal = Get-GemValueFromOrdinalByte ([int]$RecordBytes[0x4F])
        if ($valueFromOrdinal -gt 0) { return $valueFromOrdinal }
        return Get-GemValueFromGemIdByte ([int]$RecordBytes[0x36])
    }

    if ($type -eq 0x20 -and $flag4A -eq 0x10) {
        return Get-GemValueFromGemIdByte $flag4B
    }

    if ($type -eq 0x00 -and $flag4A -eq 0xFF) {
        return Get-GemValueFromGemIdByte $flag4B
    }

    if ($type -eq 0x18 -and $flag4A -eq 0x10) {
        return Get-GemValueFromGemIdByte $flag4B
    }

    return 0
}

function Get-LevelSourceTreasureMap($Table, $Layout, [IO.FileStream]$Stream) {
    $result = @{
        values = @{}
        treasureRecords = 0
    }

    $recordCount = [int]$Table.recordCount
    for ($trueIndex = 0; $trueIndex -lt $recordCount; $trueIndex++) {
        $sourceWadOffset = [int64]$Table.tableWadOffset + ([int64]$trueIndex * $RecordStride)
        [byte[]]$recordBytes = Read-WadBytes $Stream $Layout $sourceWadOffset $RecordStride
        $value = Get-SourceRecordTreasureValue $recordBytes
        $result.values[$trueIndex] = $value
        if ($value -gt 0) { $result.treasureRecords++ }
    }

    return $result
}

function Add-RecordTreasureMapEntries($Map, $Records, [int]$RecordCount) {
    $applied = 0
    foreach ($record in @($Records)) {
        $trueIndex = [int](Get-Field $record "trueIndex" (Get-Field $record "index" -1))
        if ($trueIndex -lt 0) { continue }
        if ($RecordCount -gt 0 -and $trueIndex -ge $RecordCount) { continue }
        $value = Get-RecordTreasureValue $record
        $Map[$trueIndex] = $value
        $applied++
    }
    return $applied
}

function Add-MissingRecordTreasureMapEntries($Map, $Records, [int]$RecordCount) {
    $applied = 0
    foreach ($record in @($Records)) {
        $trueIndex = [int](Get-Field $record "trueIndex" (Get-Field $record "index" -1))
        if ($trueIndex -lt 0) { continue }
        if ($RecordCount -gt 0 -and $trueIndex -ge $RecordCount) { continue }
        $value = Get-RecordTreasureValue $record
        if ($value -le 0) { continue }
        if ($Map.ContainsKey($trueIndex) -and [int]$Map[$trueIndex] -gt 0) { continue }
        $Map[$trueIndex] = $value
        $applied++
    }
    return $applied
}

function Test-EditHasTreasureOverride($Edit) {
    if ($null -ne (Get-Field $Edit "gemColorEdit" $null)) { return $true }
    if ($null -ne (Get-Field $Edit "rewardColorEdit" $null)) { return $true }
    foreach ($byteEdit in @(Get-ArrayField $Edit "sourceByteEdits")) {
        $rawOffset = Get-Field $byteEdit "offset" (Get-Field $byteEdit "offsetHex" $null)
        if ($null -eq $rawOffset) { continue }
        $byteOffset = Convert-PatchInt $rawOffset "sourceByteEdits.offset"
        if ($byteOffset -eq 0x36 -or $byteOffset -eq 0x4F -or $byteOffset -eq 0x53) { return $true }
    }
    return $false
}

function Write-ChestContentLinkBytes([byte[]]$Bytes, $Link, [string]$Context) {
    if ($null -eq $Bytes -or $Bytes.Length -lt 16) {
        throw "$Context needs at least 0x10 bytes of contained-gem special data to write the chest link."
    }

    $chestTrueIndex = Convert-PatchInt (Get-Field $Link "chestTrueIndex" $null) "$Context.chestTrueIndex"
    $rawOffset = Get-Field $Link "rawOffset" $null
    $rawX = Convert-PatchInt (Get-Field $rawOffset "x" 0) "$Context.rawOffset.x"
    $rawY = Convert-PatchInt (Get-Field $rawOffset "y" 0) "$Context.rawOffset.y"
    $rawZ = Convert-PatchInt (Get-Field $rawOffset "z" 0) "$Context.rawOffset.z"

    Write-UInt32LE $Bytes 0 ([uint64]$chestTrueIndex)
    Write-Int32LE $Bytes 4 $rawX
    Write-Int32LE $Bytes 8 $rawY
    Write-Int32LE $Bytes 12 $rawZ

    return [ordered]@{
        chestTrueIndex = $chestTrueIndex
        rawX = $rawX
        rawY = $rawY
        rawZ = $rawZ
    }
}

function Add-ChestContentLinkPatches($Table, $Layout, [IO.FileStream]$Stream, $Edit, [int]$TrueIndex, [System.Collections.ArrayList]$PatchList) {
    $link = Get-Field $Edit "chestContentLinkEdit" $null
    if ($null -eq $link) { return }
    if ($TrueIndex -lt 0 -or $TrueIndex -ge [int]$Table.recordCount) {
        throw "$($Table.displayName) chest-content link edit T$TrueIndex is outside the source record table."
    }
    if (-not $Table.Contains("tableRelativeOffset")) {
        throw "$($Table.displayName) cannot patch chest-content links without a table-relative offset."
    }

    $recordWadOffset = [int64]$Table.tableWadOffset + ([int64]$TrueIndex * $RecordStride)
    [byte[]]$recordBytes = Read-WadBytes $Stream $Layout $recordWadOffset $RecordStride
    [uint64]$specialDataOffset = [uint64](Get-UInt32LE $recordBytes 0)
    if (-not (Test-SourceSpecialDataOffset $Table $specialDataOffset)) {
        throw "$($Table.displayName) chest-content link edit T$TrueIndex has no valid source special-data offset."
    }

    [byte[]]$linkBytes = New-Object byte[] 16
    $linkValues = Write-ChestContentLinkBytes $linkBytes $link "chestContentLinkEdit"
    $chestTrueIndex = [int]$linkValues["chestTrueIndex"]
    if ($chestTrueIndex -lt 0 -or $chestTrueIndex -ge [int]$Table.recordCount) {
        throw "$($Table.displayName) chest-content link target T$chestTrueIndex is outside the source record table."
    }

    $wadBaseOffset = [int64]$Table.tableWadOffset - [int64]$Table.tableRelativeOffset
    $specialWadOffset = $wadBaseOffset + [int64]$specialDataOffset
    $rawX = [int]$linkValues["rawX"]
    $rawY = [int]$linkValues["rawY"]
    $rawZ = [int]$linkValues["rawZ"]
    $description = "Link contained gem T$TrueIndex to chest T$chestTrueIndex and set explosion offset raw ($rawX, $rawY, $rawZ)."
    [void]$PatchList.Add((New-SpecialDataPatch $Table $Layout $Edit $TrueIndex $specialWadOffset "special+0x00..0x0F" $linkBytes $description))
}

function Add-SourceSpecialDataOverridePatches($Table, $Layout, $Edit, [System.Collections.ArrayList]$PatchList) {
    $override = Get-Field $Edit "sourceSpecialDataOverride" $null
    if ($null -eq $override) { return }

    $entryBase = Get-LevelWadBaseOffset $Table
    foreach ($item in @($override)) {
        $rawOffset = Get-Field $item "targetOffset" (Get-Field $item "targetOffsetHex" $null)
        if ($null -eq $rawOffset) { throw "sourceSpecialDataOverride needs targetOffsetHex." }
        [uint64]$targetOffset = [uint64](Convert-PatchInt $rawOffset "sourceSpecialDataOverride.targetOffset")
        if (-not (Test-SourceSpecialDataOffset $Table $targetOffset)) {
            throw "$($Table.displayName) sourceSpecialDataOverride target 0x$($targetOffset.ToString('X')) is not a valid source special-data offset."
        }

        $bytesHex = [string](Get-Field $item "bytesHex" "")
        $bytesHex = ($bytesHex -replace "\s", "")
        if ([string]::IsNullOrWhiteSpace($bytesHex) -or (($bytesHex.Length % 2) -ne 0)) {
            throw "sourceSpecialDataOverride bytesHex must contain an even number of hex digits."
        }
        [byte[]]$bytes = Convert-HexToBytes $bytesHex
        if ($bytes.Length -le 0) { throw "sourceSpecialDataOverride bytesHex produced no bytes." }

        [string]$note = [string](Get-Field $item "note" "")
        if ([string]::IsNullOrWhiteSpace($note)) {
            $note = "Override loader-managed source special-data slot for experimental object import."
        }
        [int64]$wadOffset = [int64]$entryBase + [int64]$targetOffset
        [void]$PatchList.Add((New-RawPatch $Table $Layout "moby-special-data-override-experimental" $wadOffset $bytes ("$note Target source offset 0x$($targetOffset.ToString('X')), length 0x$($bytes.Length.ToString('X')).")))
    }
}

function Get-LevelInventoryTreasureTotal([IO.FileStream]$Stream, [int]$TreasureTotalTableIndex) {
    $imageOffset = $TreasureTotalTableImageOffset + ([int64]$TreasureTotalTableIndex * 2L)
    if ($imageOffset -lt 0 -or ($imageOffset + 2) -gt $Stream.Length) {
        throw "Treasure-total table offset 0x$($imageOffset.ToString('X')) is outside the source image."
    }
    $Stream.Position = $imageOffset
    $bytes = New-Object byte[] 2
    [void]$Stream.Read($bytes, 0, 2)
    return [BitConverter]::ToUInt16($bytes, 0)
}

function Get-LevelTreasureSummary($Table, $Layout, [IO.FileStream]$Stream, $Edits) {
    $inventoryBaseTotal = Get-LevelInventoryTreasureTotal $Stream ([int]$Table.treasureTotalTableIndex)
    $sourceMap = Get-LevelSourceTreasureMap $Table $Layout $Stream
    $baseByTrueIndex = $sourceMap.values
    $recordCount = [int]$Table.recordCount

    $catalogPath = Resolve-WorkspacePath ".\$($Table.editSlug)-moby-catalog.json"
    $catalogRecordsApplied = 0
    if (Test-Path -LiteralPath $catalogPath) {
        $catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
        $catalogRecordsApplied = Add-RecordTreasureMapEntries $baseByTrueIndex @(Get-ArrayField $catalog "mobys") $recordCount
    }

    $overridePath = Resolve-WorkspacePath ".\$($Table.editSlug)-moby-user-overrides.json"
    $overrideRecordsApplied = 0
    if (Test-Path -LiteralPath $overridePath) {
        $overrides = Get-Content -Raw -LiteralPath $overridePath | ConvertFrom-Json
        $overrideRecordsApplied = Add-MissingRecordTreasureMapEntries $baseByTrueIndex @(Get-ArrayField $overrides "mobys") $recordCount
    }

    $editedByTrueIndex = @{}
    foreach ($edit in @($Edits)) {
        $trueIndex = Resolve-EditTrueIndex $edit
        if ($trueIndex -ge 0) {
            $editedByTrueIndex[$trueIndex] = $edit
        }
    }

    $deltaTotal = 0
    $editedRecords = 0
    $ignoredRecords = 0
    foreach ($pair in $editedByTrueIndex.GetEnumerator()) {
        $trueIndex = [int]$pair.Key
        $edit = $pair.Value
        $mutation = Get-Field $edit "recordMutation" $null
        $mutationMode = [string](Get-Field $mutation "mode" "")
        if ($mutationMode -eq "appendFromSource") { continue }
        if ($trueIndex -lt 0 -or $trueIndex -ge $recordCount) {
            $ignoredRecords++
            continue
        }
        if ($mutationMode -ne "hide" -and $mutationMode -ne "cloneIntoSlot" -and -not (Test-EditHasTreasureOverride $edit)) {
            continue
        }

        $baseValue = 0
        if ($baseByTrueIndex.ContainsKey($trueIndex)) { $baseValue = [int]$baseByTrueIndex[$trueIndex] }
        $currentValue = Get-RecordTreasureValue $edit
        if ($mutationMode -eq "hide") { $currentValue = 0 }
        if ($currentValue -ne $baseValue) {
            $deltaTotal += ($currentValue - $baseValue)
            $editedRecords++
        }
    }

    $appendedRecords = 0
    $skippedTreasureAppends = 0
    foreach ($pair in $editedByTrueIndex.GetEnumerator()) {
        $trueIndex = [int]$pair.Key
        $edit = $pair.Value
        $mutation = Get-Field $edit "recordMutation" $null
        $mutationMode = [string](Get-Field $mutation "mode" "")
        if ($mutationMode -ne "appendFromSource") { continue }
        $skipReason = Get-AppendSkipReason $edit $AppendPolicy ([string]$Table.levelKey)
        if (-not [string]::IsNullOrWhiteSpace($skipReason)) {
            $skippedTreasureAppends++
            continue
        }
        $value = Get-RecordTreasureValue $edit
        if (-not (Test-EditHasTreasureOverride $edit)) {
            $sourceTrueIndex = [int](Get-Field $mutation "sourceTrueIndex" -1)
            $sourceLevelKey = [string](Get-Field $mutation "sourceLevelKey" "")
            $sameLevelAppendDonor = [string]::IsNullOrWhiteSpace($sourceLevelKey) -or (Normalize-LevelKey $sourceLevelKey) -eq (Normalize-LevelKey ([string]$Table.levelKey))
            if ($sameLevelAppendDonor -and $editedByTrueIndex.ContainsKey($sourceTrueIndex) -and (Test-EditHasTreasureOverride $editedByTrueIndex[$sourceTrueIndex])) {
                $value = Get-RecordTreasureValue $editedByTrueIndex[$sourceTrueIndex]
            }
        }
        if ($value -gt 0) {
            $deltaTotal += $value
            $appendedRecords++
        }
    }

    $editedTotal = [int]$inventoryBaseTotal + [int]$deltaTotal
    $catalogPathForPlan = if (Test-Path -LiteralPath $catalogPath) { (Resolve-Path -LiteralPath $catalogPath).Path } else { $catalogPath }
    $overridePathForPlan = if (Test-Path -LiteralPath $overridePath) { (Resolve-Path -LiteralPath $overridePath).Path } else { $overridePath }

    return [ordered]@{
        catalogPath = $catalogPathForPlan
        userOverridesPath = $overridePathForPlan
        baseTotal = [int]$inventoryBaseTotal
        editedTotal = $editedTotal
        deltaTotal = [int]$deltaTotal
        editedRecords = $editedRecords
        appendedRecords = $appendedRecords
        skippedTreasureAppends = $skippedTreasureAppends
        ignoredRecords = $ignoredRecords
        appendPolicy = $AppendPolicy
        sourceTreasureRecords = [int]$sourceMap.treasureRecords
        catalogRecordsApplied = $catalogRecordsApplied
        overrideRecordsApplied = $overrideRecordsApplied
        canPatch = $true
        note = "Uses the vanilla inventory-table value as the base total, then applies saved gem/reward/hide/append deltas. Catalog values are preferred when present; otherwise source table bytes and user-confirmed overrides supply per-record base values."
    }
}

function Add-TreasureTotalPatch($Table, $Layout, $Stream, $Edits, [System.Collections.ArrayList]$PatchList, [System.Collections.ArrayList]$TreasureSummaries) {
    if ($SkipTreasureTotalPatch -or $SingleAppendTrueIndex -ge 0) { return }
    $summary = Get-LevelTreasureSummary $Table $Layout $Stream $Edits
    $summary["levelKey"] = [string]$Table.levelKey
    $summary["levelName"] = [string]$Table.displayName
    $summary["treasureTotalTableIndex"] = [int]$Table.treasureTotalTableIndex
    $summary["treasureTotalTableImageOffset"] = ("0x{0:X}" -f ($TreasureTotalTableImageOffset + ([int64]$Table.treasureTotalTableIndex * 2L)))
    [void]$TreasureSummaries.Add($summary)
    if (-not [bool]$summary.canPatch) { return }

    $editedTotal = [int]$summary.editedTotal
    $baseTotal = [int]$summary.baseTotal
    if ($editedTotal -eq $baseTotal) { return }
    if ($editedTotal -lt 0 -or $editedTotal -gt 65535) {
        throw "$($Table.displayName) treasure total $editedTotal is outside the 16-bit inventory-table range."
    }

    $imageOffset = $TreasureTotalTableImageOffset + ([int64]$Table.treasureTotalTableIndex * 2L)
    $beforeValue = Get-LevelInventoryTreasureTotal $Stream ([int]$Table.treasureTotalTableIndex)

    [byte[]]$totalBytes = [BitConverter]::GetBytes([uint16]$editedTotal)
    $description = "Set $($Table.displayName) inventory treasure target from $beforeValue to $editedTotal so the pause/inventory denominator matches the edited level treasure total."
    [void]$PatchList.Add((New-DirectImagePatch $Table "level-treasure-total" $imageOffset $totalBytes $description $beforeValue $editedTotal))
}

$tables = @(Get-LevelSourceTables $LevelKey)
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $outSlug = if ($LevelKey -eq "All") { "loaderpatchtest" } else { "$($tables[0].editSlug)-loaderpatchtest" }
    $OutPath = ".\Spyro the Dragon (USA)-$outSlug.bin"
}

$resolvedImagePath = Resolve-WorkspaceInputFile $ImagePath
$resolvedOutPath = Resolve-WorkspacePath $OutPath
$resolvedRuntimeTemplateRamPath = Resolve-WorkspacePath $RuntimeTemplateRamPath
if ([string]::IsNullOrWhiteSpace($CuePath)) {
    $CuePath = [System.IO.Path]::ChangeExtension($resolvedOutPath, ".cue")
}
$resolvedCuePath = Resolve-WorkspacePath $CuePath
if ([string]::IsNullOrWhiteSpace($PlanPath)) {
    $PlanPath = "$resolvedOutPath.patchplan.json"
}
$resolvedPlanPath = Resolve-WorkspacePath $PlanPath

if (-not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing source image: $resolvedImagePath" }
if ($RuntimeInitAppended) {
    throw "RuntimeInitAppended is disabled. The live test showed that copying runtime-layout records into source-table append slots corrupts Stone Hill during load. Source-table appends must clone source records instead."
}

$layout = Detect-DiscLayout $resolvedImagePath
$patches = New-Object System.Collections.ArrayList
$skippedAppends = New-Object System.Collections.ArrayList
$editSources = New-Object System.Collections.ArrayList
$treasureSummaries = New-Object System.Collections.ArrayList
$importedExternalPackages = @{}
$runtimeTemplateRam = $null
if ($RuntimeInitAppended) {
    $runtimeTemplateRam = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $resolvedRuntimeTemplateRamPath).Path)
}

$stream = [IO.File]::OpenRead($resolvedImagePath)
try {
    foreach ($table in $tables) {
        $nativePath = if ([string]::IsNullOrWhiteSpace($NativeEditsPath) -or $LevelKey -eq "All") {
            Resolve-WorkspacePath ".\$($table.editSlug)-native-edits.json"
        }
        else {
            Resolve-WorkspacePath $NativeEditsPath
        }
        if (-not (Test-Path -LiteralPath $nativePath)) {
            if ($LevelKey -eq "All") { continue }
            throw "Missing native edits: $nativePath"
        }

        $editsRoot = Get-Content -Raw -LiteralPath $nativePath | ConvertFrom-Json
        $edits = @(Get-ArrayField $editsRoot "edits")
        [void]$editSources.Add([ordered]@{
            levelKey = $table.levelKey
            displayName = $table.displayName
            nativeEditsPath = (Resolve-Path -LiteralPath $nativePath).Path
            editCount = $edits.Count
        })

        Add-TreasureTotalPatch $table $layout $stream $edits $patches $treasureSummaries

        $sourceEditsByTrueIndex = @{}
        foreach ($candidateEdit in $edits) {
            $candidateMutation = Get-Field $candidateEdit "recordMutation" $null
            $candidateMutationMode = [string](Get-Field $candidateMutation "mode" "")
            if ($candidateMutationMode -eq "appendFromSource") { continue }
            $candidateTrueIndex = Resolve-EditTrueIndex $candidateEdit
            if ($candidateTrueIndex -ge 0 -and $candidateTrueIndex -lt [int]$table.recordCount) {
                $sourceEditsByTrueIndex[$candidateTrueIndex] = $candidateEdit
            }
        }

        $appendMaxTrueIndex = [int]$table.recordCount - 1
        $appendTrueIndexMap = @{}
        $hasAppend = $false
        $specialDataAllocator = New-SpecialDataAllocator $stream $layout $table
        foreach ($edit in $edits) {
            $trueIndex = Resolve-EditTrueIndex $edit
            $recordMutation = Get-Field $edit "recordMutation" $null
            $mutationMode = [string](Get-Field $recordMutation "mode" "")
            Add-SourceSpecialDataOverridePatches $table $layout $edit $patches

            if ($mutationMode -eq "appendFromSource") {
                if ($SingleAppendTrueIndex -ge 0 -and $trueIndex -ne $SingleAppendTrueIndex) {
                    [void]$skippedAppends.Add([ordered]@{
                        levelKey = [string]$table.levelKey
                        levelName = [string]$table.displayName
                        index = [int](Get-Field $edit "index" -1)
                        trueIndex = $trueIndex
                        label = [string](Get-Field $edit "label" "")
                        typeHex = [string](Get-Field $edit "typeHex" "")
                        reason = "SingleAppendTrueIndex selected T$SingleAppendTrueIndex for isolated testing."
                    })
                    continue
                }

                $appendSkipReason = Get-AppendSkipReason $edit $AppendPolicy ([string]$table.levelKey)
                if (-not [string]::IsNullOrWhiteSpace($appendSkipReason)) {
                    $skippedLabel = [string](Get-Field $edit "label" "")
                    [void]$skippedAppends.Add([ordered]@{
                        levelKey = [string]$table.levelKey
                        levelName = [string]$table.displayName
                        index = [int](Get-Field $edit "index" -1)
                        trueIndex = $trueIndex
                        label = $skippedLabel
                        typeHex = [string](Get-Field $edit "typeHex" "")
                        reason = $appendSkipReason
                    })
                    Write-Warning "Skipping $($table.displayName) true-add T$trueIndex '$skippedLabel': $appendSkipReason"
                    continue
                }

                $sourceTrueIndex = [int](Get-Field $recordMutation "sourceTrueIndex" -1)
                $sourceLevelKey = [string](Get-Field $recordMutation "sourceLevelKey" "")
                $sourceTable = $table
                if (-not [string]::IsNullOrWhiteSpace($sourceLevelKey) -and (Normalize-LevelKey $sourceLevelKey) -ne (Normalize-LevelKey ([string]$table.levelKey))) {
                    $sourceTable = Get-LevelSourceTable $sourceLevelKey
                }
                if ($sourceTrueIndex -lt 0 -or $sourceTrueIndex -ge [int]$sourceTable.recordCount) {
                    throw "Append source $($sourceTable.displayName) T$sourceTrueIndex is outside source table range."
                }
                $savedTrueIndex = $trueIndex
                $trueIndex = $appendMaxTrueIndex + 1
                $packageImportDescription = ""
                if ($ExperimentalAppendRecordMode -eq "Clone") {
                    $packageImportDescription = Add-ExternalChestPackageImportPatches $stream $layout $table $sourceTable $recordMutation $patches $importedExternalPackages
                }

                $sourceWadOffset = [int64]$sourceTable.tableWadOffset + ([int64]$sourceTrueIndex * $RecordStride)
                [byte[]]$recordBytes = Read-WadBytes $stream $layout $sourceWadOffset $RecordStride
                $externalTemplateDescription = ""
                if ($ExperimentalAppendRecordMode -eq "Blank") {
                    $recordBytes = New-Object byte[] $RecordStride
                    $externalTemplateDescription = " Appended a blank diagnostic row instead of cloning donor data."
                }
                else {
                    $externalTemplateDescription = Apply-ExternalTemplateIdentityBytes $recordBytes $edit $recordMutation $sourceTable $table
                }
                $runtimeInitDescription = ""
                if ($RuntimeInitAppended) {
                    if ($table.levelKey -ne "StoneHill" -or -not $table.Contains("runtimeMobyPointer")) {
                        throw "RuntimeInitAppended is only mapped for Stone Hill right now."
                    }
                    [byte[]]$runtimeRecordBytes = Get-RuntimeTemplateRecord $runtimeTemplateRam ([uint64]$table.runtimeMobyPointer) $sourceTrueIndex
                    [Array]::Copy($runtimeRecordBytes, $recordBytes, $RecordStride)
                    $appendRuntimePointer = ([uint64]$table.runtimeMobyPointer + ([uint64]$trueIndex * [uint64]$RecordStride)) -band $UInt32Mask
                    Write-UInt32LE $recordBytes 0x00 $appendRuntimePointer
                    $runtimeInitDescription = " Runtime-initialized from donor T$sourceTrueIndex template and self pointer set to 0x$($appendRuntimePointer.ToString('X8'))."
                }
                $specialDataDescription = ""
                $appendLink = Get-Field $edit "chestContentLinkEdit" $null
                $linkedAppendSpecialData = $false
                if ($ExperimentalAppendRecordMode -eq "Clone" -and -not $RuntimeInitAppended -and $null -ne $specialDataAllocator -and $ExperimentalAppendSpecialDataMode -eq "Copy") {
                    [uint64]$sourceSpecialDataOffset = [uint64](Get-UInt32LE $recordBytes 0)
                    if (Test-SourceSpecialDataOffset $sourceTable $sourceSpecialDataOffset) {
                        $sourceSpecialDataAllocator = $specialDataAllocator
                        if ((Normalize-LevelKey ([string]$sourceTable.levelKey)) -ne (Normalize-LevelKey ([string]$table.levelKey))) {
                            $sourceSpecialDataAllocator = New-SpecialDataAllocator $stream $layout $sourceTable
                        }
                        $specialDataLength = Get-SpecialDataLength $sourceSpecialDataAllocator $sourceSpecialDataOffset $recordBytes
                        [uint64]$appendSpecialDataOffset = Reserve-SpecialDataOffset $stream $layout $table $specialDataAllocator $specialDataLength
                        $sourceSpecialDataWadOffset = [int64]$sourceSpecialDataAllocator.wadBaseOffset + [int64]$sourceSpecialDataOffset
                        $appendSpecialDataWadOffset = [int64]$specialDataAllocator.wadBaseOffset + [int64]$appendSpecialDataOffset
                        [byte[]]$specialDataBytes = Read-WadBytes $stream $layout $sourceSpecialDataWadOffset $specialDataLength
                        if ($null -ne $appendLink) {
                            $linkChestTrueIndexRaw = Convert-PatchInt (Get-Field $appendLink "chestTrueIndex" $null) "chestContentLinkEdit.chestTrueIndex"
                            $linkForWrite = $appendLink
                            if ($linkChestTrueIndexRaw -ge [int]$table.recordCount) {
                                if (-not $appendTrueIndexMap.ContainsKey($linkChestTrueIndexRaw)) {
                                    throw "$($table.displayName) appended chest-content T$savedTrueIndex links to appended chest T$linkChestTrueIndexRaw before that chest has been exported."
                                }
                                $linkForWrite = [ordered]@{
                                    chestTrueIndex = [int]$appendTrueIndexMap[$linkChestTrueIndexRaw]
                                    rawOffset = (Get-Field $appendLink "rawOffset" $null)
                                }
                            }
                            $linkValues = Write-ChestContentLinkBytes $specialDataBytes $linkForWrite "chestContentLinkEdit"
                            $linkChestTrueIndex = [int]$linkValues["chestTrueIndex"]
                            if ($linkChestTrueIndex -lt 0 -or $linkChestTrueIndex -gt $appendMaxTrueIndex) {
                                throw "$($table.displayName) appended chest-content link target T$linkChestTrueIndex is outside the source record table."
                            }
                            $linkedAppendSpecialData = $true
                        }
                        [void]$patches.Add((New-RawPatch $table $layout "moby-special-data-append" $appendSpecialDataWadOffset $specialDataBytes ("Copy $($sourceTable.displayName) donor T$sourceTrueIndex special data from 0x$($sourceSpecialDataOffset.ToString('X')) to $($table.displayName) source offset 0x$($appendSpecialDataOffset.ToString('X')) for appended T$trueIndex.")))
                        Write-UInt32LE $recordBytes 0x00 $appendSpecialDataOffset
                        $specialDataDescription = " Copied donor special data 0x$($specialDataLength.ToString('X')) bytes to source offset 0x$($appendSpecialDataOffset.ToString('X')) and repointed +0x00 so the append has independent animation/state."
                        if ($linkedAppendSpecialData) {
                            $rawX = [int]$linkValues["rawX"]
                            $rawY = [int]$linkValues["rawY"]
                            $rawZ = [int]$linkValues["rawZ"]
                            $specialDataDescription += " Linked copied contained-gem special data to chest T$linkChestTrueIndex with explosion offset raw ($rawX, $rawY, $rawZ)."
                        }
                    }
                }
                elseif ($ExperimentalAppendRecordMode -eq "Clone" -and $ExperimentalAppendSpecialDataMode -eq "KeepDonorPointer") {
                    $specialDataDescription = " Kept donor special-data pointer unchanged for diagnostic testing."
                }
                elseif ($ExperimentalAppendRecordMode -eq "Clone" -and $ExperimentalAppendSpecialDataMode -eq "ZeroPointer") {
                    Write-UInt32LE $recordBytes 0x00 0
                    $specialDataDescription = " Zeroed +0x00 special-data pointer for diagnostic testing."
                }
                if ($null -ne $appendLink -and -not $linkedAppendSpecialData) {
                    throw "$($table.displayName) appended chest-content T$savedTrueIndex cannot be exported because its donor T$sourceTrueIndex has no copyable source special-data block."
                }
                if ($ExperimentalAppendRecordMode -eq "Clone") {
                    foreach ($axis in @("x", "y", "z")) {
                        Write-Int32LE $recordBytes ([int]$CoordOffsets[$axis]) (Get-RawEditedAxis $edit $axis)
                    }
                    $appendByteEdits = @()
                    $sameLevelDonor = (Normalize-LevelKey ([string]$sourceTable.levelKey)) -eq (Normalize-LevelKey ([string]$table.levelKey))
                    if ($sameLevelDonor -and $sourceEditsByTrueIndex.ContainsKey($sourceTrueIndex)) {
                        $appendByteEdits += @(Get-ArrayField $sourceEditsByTrueIndex[$sourceTrueIndex] "sourceByteEdits")
                    }
                    $appendByteEdits += @(Get-ArrayField $edit "sourceByteEdits")
                    foreach ($byteEdit in $appendByteEdits) {
                        $byteOffset = Convert-PatchInt (Get-Field $byteEdit "offset" (Get-Field $byteEdit "offsetHex" $null)) "sourceByteEdits.offset"
                        $value = Convert-PatchInt (Get-Field $byteEdit "value" (Get-Field $byteEdit "valueHex" $null)) "sourceByteEdits.value"
                        if ($byteOffset -lt 0 -or $byteOffset -ge $RecordStride) { throw "Byte edit offset 0x$($byteOffset.ToString('X')) is outside loader record stride 0x58." }
                        if ($value -lt 0 -or $value -gt 255) { throw "Byte edit value 0x$($value.ToString('X')) is outside byte range." }
                        $recordBytes[$byteOffset] = [byte]$value
                    }
                }

                $appendWadOffset = [int64]$table.tableWadOffset + ([int64]$trueIndex * $RecordStride)
                [byte[]]$appendBefore = Read-WadBytes $stream $layout $appendWadOffset $RecordStride
                if (-not (Test-AllZero $appendBefore)) {
                    throw "$($table.displayName) append target T$trueIndex at WAD 0x$($appendWadOffset.ToString('X')) is not empty in the source image."
                }

                $donorDescription = if ((Normalize-LevelKey ([string]$sourceTable.levelKey)) -eq (Normalize-LevelKey ([string]$table.levelKey))) { "donor T$sourceTrueIndex" } else { "$($sourceTable.displayName) donor T$sourceTrueIndex" }
                $description = "Append source record T$trueIndex cloned from $donorDescription, preserving edited position." + $packageImportDescription + $externalTemplateDescription + $runtimeInitDescription + $specialDataDescription
                if ($savedTrueIndex -ge [int]$table.recordCount -and $savedTrueIndex -ne $trueIndex) {
                    $description += " Compacted from saved append target T$savedTrueIndex to avoid a gap."
                }
                [void]$patches.Add((New-PatchRecord $table $layout $edit $trueIndex "moby-record-append" 0x00 $recordBytes $description))
                $appendTrueIndexMap[$savedTrueIndex] = $trueIndex
                $appendMaxTrueIndex = [Math]::Max($appendMaxTrueIndex, $trueIndex)
                $hasAppend = $true
                continue
            }

            if ($SingleAppendTrueIndex -ge 0) {
                continue
            }

            if ($trueIndex -lt 0 -or $trueIndex -ge [int]$table.recordCount) {
                Write-Warning "Skipping $($table.displayName) T$trueIndex; it is outside source table range 0..$([int]$table.recordCount - 1)."
                continue
            }

            if ($mutationMode -eq "replaceFromSource") {
                $sourceTrueIndex = [int](Get-Field $recordMutation "sourceTrueIndex" -1)
                $sourceLevelKey = [string](Get-Field $recordMutation "sourceLevelKey" "")
                $sourceTable = $table
                if (-not [string]::IsNullOrWhiteSpace($sourceLevelKey) -and (Normalize-LevelKey $sourceLevelKey) -ne (Normalize-LevelKey ([string]$table.levelKey))) {
                    $sourceTable = Get-LevelSourceTable $sourceLevelKey
                }
                if ($sourceTrueIndex -lt 0 -or $sourceTrueIndex -ge [int]$sourceTable.recordCount) {
                    throw "Replace source $($sourceTable.displayName) T$sourceTrueIndex is outside source table range."
                }

                $packageImportDescription = Add-ExternalChestPackageImportPatches $stream $layout $table $sourceTable $recordMutation $patches $importedExternalPackages

                $sourceWadOffset = [int64]$sourceTable.tableWadOffset + ([int64]$sourceTrueIndex * $RecordStride)
                [byte[]]$recordBytes = Read-WadBytes $stream $layout $sourceWadOffset $RecordStride
                $externalTemplateDescription = Apply-ExternalTemplateIdentityBytes $recordBytes $edit $recordMutation $sourceTable $table

                $specialDataDescription = ""
                if ($null -ne $specialDataAllocator -and $ExperimentalAppendSpecialDataMode -eq "Copy") {
                    [uint64]$sourceSpecialDataOffset = [uint64](Get-UInt32LE $recordBytes 0)
                    if (Test-SourceSpecialDataOffset $sourceTable $sourceSpecialDataOffset) {
                        $sourceSpecialDataAllocator = $specialDataAllocator
                        if ((Normalize-LevelKey ([string]$sourceTable.levelKey)) -ne (Normalize-LevelKey ([string]$table.levelKey))) {
                            $sourceSpecialDataAllocator = New-SpecialDataAllocator $stream $layout $sourceTable
                        }
                        $specialDataLength = Get-SpecialDataLength $sourceSpecialDataAllocator $sourceSpecialDataOffset $recordBytes
                        $targetSpecialDataRaw = Get-Field $edit "targetSpecialDataOffset" (Get-Field $edit "targetSpecialDataOffsetHex" $null)
                        if ($null -ne $targetSpecialDataRaw) {
                            [uint64]$targetSpecialDataOffset = [uint64](Convert-PatchInt $targetSpecialDataRaw "targetSpecialDataOffset")
                            if (-not (Test-SourceSpecialDataOffset $table $targetSpecialDataOffset)) {
                                throw "$($table.displayName) replacement target special-data offset 0x$($targetSpecialDataOffset.ToString('X')) is not valid."
                            }
                        }
                        else {
                            [uint64]$targetSpecialDataOffset = Reserve-SpecialDataOffset $stream $layout $table $specialDataAllocator $specialDataLength
                        }

                        $sourceSpecialDataWadOffset = [int64]$sourceSpecialDataAllocator.wadBaseOffset + [int64]$sourceSpecialDataOffset
                        $targetSpecialDataWadOffset = [int64]$specialDataAllocator.wadBaseOffset + [int64]$targetSpecialDataOffset
                        [byte[]]$specialDataBytes = Read-WadBytes $stream $layout $sourceSpecialDataWadOffset $specialDataLength
                        [void]$patches.Add((New-RawPatch $table $layout "moby-special-data-replace-from-source" $targetSpecialDataWadOffset $specialDataBytes ("Copy $($sourceTable.displayName) donor T$sourceTrueIndex special data from 0x$($sourceSpecialDataOffset.ToString('X')) to $($table.displayName) source offset 0x$($targetSpecialDataOffset.ToString('X')) for replacement T$trueIndex.")))
                        Write-UInt32LE $recordBytes 0x00 $targetSpecialDataOffset
                        $specialDataDescription = " Copied donor special data 0x$($specialDataLength.ToString('X')) bytes to source offset 0x$($targetSpecialDataOffset.ToString('X')) and repointed +0x00."
                    }
                }
                elseif ($ExperimentalAppendSpecialDataMode -eq "KeepDonorPointer") {
                    $specialDataDescription = " Kept donor special-data pointer unchanged for diagnostic testing."
                }
                elseif ($ExperimentalAppendSpecialDataMode -eq "ZeroPointer") {
                    Write-UInt32LE $recordBytes 0x00 0
                    $specialDataDescription = " Zeroed +0x00 special-data pointer for diagnostic testing."
                }

                foreach ($axis in @("x", "y", "z")) {
                    Write-Int32LE $recordBytes ([int]$CoordOffsets[$axis]) (Get-RawEditedAxis $edit $axis)
                }
                foreach ($byteEdit in @(Get-ArrayField $edit "sourceByteEdits")) {
                    $byteOffset = Convert-PatchInt (Get-Field $byteEdit "offset" (Get-Field $byteEdit "offsetHex" $null)) "sourceByteEdits.offset"
                    $value = Convert-PatchInt (Get-Field $byteEdit "value" (Get-Field $byteEdit "valueHex" $null)) "sourceByteEdits.value"
                    if ($byteOffset -lt 0 -or $byteOffset -ge $RecordStride) { throw "Byte edit offset 0x$($byteOffset.ToString('X')) is outside loader record stride 0x58." }
                    if ($value -lt 0 -or $value -gt 255) { throw "Byte edit value 0x$($value.ToString('X')) is outside byte range." }
                    $recordBytes[$byteOffset] = [byte]$value
                }

                $donorDescription = if ((Normalize-LevelKey ([string]$sourceTable.levelKey)) -eq (Normalize-LevelKey ([string]$table.levelKey))) { "donor T$sourceTrueIndex" } else { "$($sourceTable.displayName) donor T$sourceTrueIndex" }
                $description = "Replace source record T$trueIndex with $donorDescription, preserving edited target position." + $packageImportDescription + $externalTemplateDescription + $specialDataDescription
                [void]$patches.Add((New-PatchRecord $table $layout $edit $trueIndex "moby-record-replace-from-source" 0x00 $recordBytes $description))
                continue
            }

            if ($mutationMode -eq "cloneIntoSlot") {
                $sourceTrueIndex = [int](Get-Field $recordMutation "sourceTrueIndex" -1)
                if ($sourceTrueIndex -lt 0 -or $sourceTrueIndex -ge [int]$table.recordCount) {
                    throw "Clone source T$sourceTrueIndex is outside $($table.displayName) source table range."
                }
                $sourceWadOffset = [int64]$table.tableWadOffset + ([int64]$sourceTrueIndex * $RecordStride)
                [byte[]]$recordBytes = Read-WadBytes $stream $layout $sourceWadOffset $RecordStride
                foreach ($axis in @("x", "y", "z")) {
                    Write-Int32LE $recordBytes ([int]$CoordOffsets[$axis]) (Get-RawEditedAxis $edit $axis)
                }
                foreach ($byteEdit in @(Get-ArrayField $edit "sourceByteEdits")) {
                    $byteOffset = Convert-PatchInt (Get-Field $byteEdit "offset" (Get-Field $byteEdit "offsetHex" $null)) "sourceByteEdits.offset"
                    $value = Convert-PatchInt (Get-Field $byteEdit "value" (Get-Field $byteEdit "valueHex" $null)) "sourceByteEdits.value"
                    if ($byteOffset -lt 0 -or $byteOffset -ge $RecordStride) { throw "Byte edit offset 0x$($byteOffset.ToString('X')) is outside loader record stride 0x58." }
                    if ($value -lt 0 -or $value -gt 255) { throw "Byte edit value 0x$($value.ToString('X')) is outside byte range." }
                    $recordBytes[$byteOffset] = [byte]$value
                }
                $description = "Clone source record T$sourceTrueIndex into T$trueIndex, preserving edited target position."
                [void]$patches.Add((New-PatchRecord $table $layout $edit $trueIndex "moby-record-clone-into-slot" 0x00 $recordBytes $description))
                continue
            }

            foreach ($axis in @("x", "y", "z")) {
                $raw = Get-RawEditedAxis $edit $axis
                [byte[]]$bytes = [BitConverter]::GetBytes([int32]$raw)
                $kind = if ($mutationMode -eq "hide") { "moby-coordinate-hide" } else { "moby-coordinate" }
                [void]$patches.Add((New-PatchRecord $table $layout $edit $trueIndex $kind ([int]$CoordOffsets[$axis]) $bytes ("Set " + $axis.ToUpperInvariant() + " to raw " + $raw.ToString())))
            }

            foreach ($byteEdit in @(Get-ArrayField $edit "sourceByteEdits")) {
                $byteOffset = Convert-PatchInt (Get-Field $byteEdit "offset" (Get-Field $byteEdit "offsetHex" $null)) "sourceByteEdits.offset"
                $value = Convert-PatchInt (Get-Field $byteEdit "value" (Get-Field $byteEdit "valueHex" $null)) "sourceByteEdits.value"
                if ($byteOffset -lt 0 -or $byteOffset -ge $RecordStride) { throw "Byte edit offset 0x$($byteOffset.ToString('X')) is outside loader record stride 0x58." }
                if ($value -lt 0 -or $value -gt 255) { throw "Byte edit value 0x$($value.ToString('X')) is outside byte range." }
                [byte[]]$bytes = @([byte]$value)
                $field = [string](Get-Field $byteEdit "field" "source-byte")
                [void]$patches.Add((New-PatchRecord $table $layout $edit $trueIndex "moby-source-byte" $byteOffset $bytes ("Set " + $field + " to 0x" + $value.ToString("X2"))))
            }

            Add-ChestContentLinkPatches $table $layout $stream $edit $trueIndex $patches
        }

        if ($hasAppend) {
            $newCount = $appendMaxTrueIndex + 1
            $countWadOffset = [int64]$table.tableWadOffset - 4
            [byte[]]$countBefore = Read-WadBytes $stream $layout $countWadOffset 4
            $currentCount = [int](Get-UInt32LE $countBefore 0)
            if ($currentCount -ne [int]$table.recordCount) {
                throw "$($table.displayName) source-count field at 0x$($countWadOffset.ToString('X')) is $currentCount, expected $($table.recordCount)."
            }
            [byte[]]$countBytes = [BitConverter]::GetBytes([uint32]$newCount)
            [void]$patches.Add((New-RawPatch $table $layout "moby-source-count" $countWadOffset $countBytes ("Increase $($table.displayName) source moby count from $($table.recordCount) to $newCount.")))
        }
    }
}
finally {
    $stream.Dispose()
}

if ($patches.Count -eq 0) { throw "No patchable moby edits were found for $LevelKey." }

$totalEditCount = 0
foreach ($source in @($editSources.ToArray())) {
    $totalEditCount += [int]$source["editCount"]
}

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Export-SpyroLevelMobyPatchTest.ps1"
    warning = "Experimental level moby source-table patch. Use disposable BIN/CUE outputs only."
    appendPolicy = $AppendPolicy
    singleAppendTrueIndex = $SingleAppendTrueIndex
    imagePath = (Resolve-Path -LiteralPath $resolvedImagePath).Path
    outPath = $(if ($PlanOnly) { $null } else { $resolvedOutPath })
    cuePath = $(if ($PlanOnly) { $null } else { $resolvedCuePath })
        runtimeInitAppended = [bool]$RuntimeInitAppended
        experimentalImportExternalChestPackages = [bool]$ExperimentalImportExternalChestPackages
        experimentalExternalChestPackageMode = $ExperimentalExternalChestPackageMode
        allowExperimentalPackageRootRegistration = [bool]$AllowExperimentalPackageRootRegistration
        experimentalAppendRecordMode = $ExperimentalAppendRecordMode
        experimentalAppendSpecialDataMode = $ExperimentalAppendSpecialDataMode
    runtimeTemplateRamPath = $(if ($RuntimeInitAppended) { (Resolve-Path -LiteralPath $resolvedRuntimeTemplateRamPath).Path } else { $null })
    skipTreasureTotalPatch = [bool]$SkipTreasureTotalPatch
    treasureTotalTable = [ordered]@{
        imageOffset = ("0x{0:X}" -f $TreasureTotalTableImageOffset)
        valueFormat = "uint16 little-endian"
        note = "Consecutive per-level inventory/pause-screen treasure targets, starting at Artisans in spyro-level-catalog.json order."
    }
    treasureSummaries = @($treasureSummaries.ToArray())
    nativeEditsPath = $(if ($LevelKey -eq "All") { $null } else { @($editSources.ToArray())[0].nativeEditsPath })
    editSources = @($editSources.ToArray())
    discLayout = $layout
    sources = @($tables | ForEach-Object {
        [ordered]@{
            levelKey = $_.levelKey
            displayName = $_.displayName
            wadLba = $WadLba
            wadEntry = $_.wadEntry
            tableWadOffset = ("0x{0:X}" -f [int64]$_.tableWadOffset)
            tableRelativeOffset = ("0x{0:X}" -f [int64]$_.tableRelativeOffset)
            recordCount = $_.recordCount
            recordStride = "0x58"
            coordinateOffsets = [ordered]@{ x = "+0x0C"; y = "+0x10"; z = "+0x14" }
            treasureTotalTableIndex = $_.treasureTotalTableIndex
            confidence = $_.confidence
        }
    })
    source = $(if ($tables.Count -eq 1) { $tables[0] } else { $null })
    level = $(if ($tables.Count -eq 1) { $tables[0] } else { [ordered]@{ levelKey = "All"; displayName = "All mapped levels" } })
    editCount = $totalEditCount
    patchCount = $patches.Count
    skippedAppendCount = $skippedAppends.Count
    skippedAppends = @($skippedAppends.ToArray())
    patches = @($patches.ToArray())
    binaryPatches = @($patches.ToArray())
}

$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedPlanPath -Encoding UTF8

if (-not $PlanOnly) {
    Copy-Item -LiteralPath $resolvedImagePath -Destination $resolvedOutPath -Force
    $stream = [System.IO.File]::Open($resolvedOutPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::Read)
    try {
        foreach ($patch in @($patches.ToArray())) {
            [byte[]]$bytes = Convert-HexToBytes ([string]$patch.bytesHex)
            $wadRelativeOffset = [string]$patch.wadRelativeOffset
            if (-not [string]::IsNullOrWhiteSpace($wadRelativeOffset)) {
                Write-WadBytes $stream $layout ([Convert]::ToInt64($wadRelativeOffset, 16)) $bytes
            }
            else {
                $stream.Position = [int64]$patch.imageOffset
                $stream.Write($bytes, 0, $bytes.Length)
            }
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

$levelText = if ($LevelKey -eq "All") { "all mapped levels" } else { $tables[0].displayName }
Write-Host "Wrote $levelText moby patch plan to $resolvedPlanPath"
if ($PlanOnly) {
    Write-Host "PlanOnly set; no BIN/CUE was written."
}
else {
    Write-Host "Wrote patched BIN to $resolvedOutPath"
    Write-Host "Wrote CUE to $resolvedCuePath"
}
$patchedRecordKeys = @($patches | Where-Object { [int]$_["trueIndex"] -ge 0 } | ForEach-Object { ([string]$_["levelKey"]) + ":T" + ([int]$_["trueIndex"]).ToString() } | Select-Object -Unique)
Write-Host ("Patches: {0} source-table writes across {1} edited mobys" -f $patches.Count, $patchedRecordKeys.Count)
if ($skippedAppends.Count -gt 0) {
    Write-Host ("Skipped true-add records: {0}" -f $skippedAppends.Count)
}
