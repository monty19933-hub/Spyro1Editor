param(
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$NativeEditsPath = ".\stonehill-native-edits.json",
    [string]$OutJsonPath = ".\stonehill-loader-table-analysis.json",
    [string]$OutMarkdownPath = ".\stonehill-loader-table-analysis.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$PsxRamBase = [uint64][Convert]::ToUInt64("80000000", 16)
$RamSize = 0x200000
$TrueStride = 0x58
$LegacyStride = 0x50
$WadLba = 37
$StoneMetadataOffset = 8333312
$StoneMetadataSize = 57344
$StoneAssetOffset = 8390656
$StoneAssetSize = 3682304
$CandidateSourceEntry = 12
$CandidateSourceEntryOffset = 12138496
$CandidateSourceTableRel = 0x1DF338

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
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

function Test-PsxPointer([uint32]$Value) {
    $v = [uint64]$Value
    return ($v -ge $PsxRamBase -and $v -lt ($PsxRamBase + $RamSize))
}

function Convert-PointerToOffset([uint32]$Value) {
    return [int]([uint64]$Value - $PsxRamBase)
}

function Format-Hex32([uint32]$Value) {
    return ("0x{0:X8}" -f $Value)
}

function Format-RuntimeAddress([int]$Offset) {
    return ("0x{0:X8}" -f ($PsxRamBase + [uint64]$Offset))
}

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name)) { return $Object[$Name] }
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

function Get-Signed16([uint32]$Value) {
    $v = [int]($Value -band 0xFFFF)
    if ($v -ge 0x8000) { $v -= 0x10000 }
    return $v
}

function Get-MipsInstructionText([uint32]$Word, [uint64]$Address) {
    $reg = @("zero","at","v0","v1","a0","a1","a2","a3","t0","t1","t2","t3","t4","t5","t6","t7","s0","s1","s2","s3","s4","s5","s6","s7","t8","t9","k0","k1","gp","sp","fp","ra")
    $op = ($Word -shr 26) -band 0x3F
    $rs = ($Word -shr 21) -band 0x1F
    $rt = ($Word -shr 16) -band 0x1F
    $rd = ($Word -shr 11) -band 0x1F
    $sa = ($Word -shr 6) -band 0x1F
    $fn = $Word -band 0x3F
    $imm = $Word -band 0xFFFF
    $sim = Get-Signed16 $Word

    switch ($op) {
        0 {
            switch ($fn) {
                0 { if ($Word -eq 0) { return "nop" } return "sll $($reg[$rd]),$($reg[$rt]),$sa" }
                2 { return "srl $($reg[$rd]),$($reg[$rt]),$sa" }
                3 { return "sra $($reg[$rd]),$($reg[$rt]),$sa" }
                8 { return "jr $($reg[$rs])" }
                9 { return "jalr $($reg[$rd]),$($reg[$rs])" }
                16 { return "mfhi $($reg[$rd])" }
                18 { return "mflo $($reg[$rd])" }
                24 { return "mult $($reg[$rs]),$($reg[$rt])" }
                25 { return "multu $($reg[$rs]),$($reg[$rt])" }
                32 { return "add $($reg[$rd]),$($reg[$rs]),$($reg[$rt])" }
                33 { return "addu $($reg[$rd]),$($reg[$rs]),$($reg[$rt])" }
                34 { return "sub $($reg[$rd]),$($reg[$rs]),$($reg[$rt])" }
                35 { return "subu $($reg[$rd]),$($reg[$rs]),$($reg[$rt])" }
                36 { return "and $($reg[$rd]),$($reg[$rs]),$($reg[$rt])" }
                37 { return "or $($reg[$rd]),$($reg[$rs]),$($reg[$rt])" }
                42 { return "slt $($reg[$rd]),$($reg[$rs]),$($reg[$rt])" }
                43 { return "sltu $($reg[$rd]),$($reg[$rs]),$($reg[$rt])" }
                default { return "special/fn=0x{0:X2}" -f $fn }
            }
        }
        2 { return "j 0x{0:X8}" -f ((($Address + 4) -band 0xFFFFFFFFF0000000L) -bor (($Word -band 0x03FFFFFF) -shl 2)) }
        3 { return "jal 0x{0:X8}" -f ((($Address + 4) -band 0xFFFFFFFFF0000000L) -bor (($Word -band 0x03FFFFFF) -shl 2)) }
        4 { return "beq $($reg[$rs]),$($reg[$rt]),0x{0:X8}" -f ([uint64]([int64]$Address + 4 + ([int64]$sim * 4))) }
        5 { return "bne $($reg[$rs]),$($reg[$rt]),0x{0:X8}" -f ([uint64]([int64]$Address + 4 + ([int64]$sim * 4))) }
        6 { return "blez $($reg[$rs]),0x{0:X8}" -f ([uint64]([int64]$Address + 4 + ([int64]$sim * 4))) }
        7 { return "bgtz $($reg[$rs]),0x{0:X8}" -f ([uint64]([int64]$Address + 4 + ([int64]$sim * 4))) }
        8 { return "addi $($reg[$rt]),$($reg[$rs]),$sim" }
        9 { return "addiu $($reg[$rt]),$($reg[$rs]),$sim" }
        10 { return "slti $($reg[$rt]),$($reg[$rs]),$sim" }
        11 { return "sltiu $($reg[$rt]),$($reg[$rs]),$sim" }
        12 { return "andi $($reg[$rt]),$($reg[$rs]),0x{0:X}" -f $imm }
        13 { return "ori $($reg[$rt]),$($reg[$rs]),0x{0:X}" -f $imm }
        14 { return "xori $($reg[$rt]),$($reg[$rs]),0x{0:X}" -f $imm }
        15 { return "lui $($reg[$rt]),0x{0:X}" -f $imm }
        32 { return "lb $($reg[$rt]),$sim($($reg[$rs]))" }
        33 { return "lh $($reg[$rt]),$sim($($reg[$rs]))" }
        35 { return "lw $($reg[$rt]),$sim($($reg[$rs]))" }
        36 { return "lbu $($reg[$rt]),$sim($($reg[$rs]))" }
        37 { return "lhu $($reg[$rt]),$sim($($reg[$rs]))" }
        40 { return "sb $($reg[$rt]),$sim($($reg[$rs]))" }
        41 { return "sh $($reg[$rt]),$sim($($reg[$rs]))" }
        43 { return "sw $($reg[$rt]),$sim($($reg[$rs]))" }
        default { return "op=0x{0:X2}" -f $op }
    }
}

function Get-CodeContext([byte[]]$Ram, [int]$CenterOffset, [int]$Before = 28, [int]$After = 40) {
    $rows = @()
    for ($offset = [Math]::Max(0, $CenterOffset - $Before); $offset -le [Math]::Min($Ram.Length - 4, $CenterOffset + $After); $offset += 4) {
        $word = Get-UInt32LE $Ram $offset
        $address = $PsxRamBase + [uint64]$offset
        $rows += [ordered]@{
            runtimeAddress = ("0x{0:X8}" -f $address)
            ramOffset = ("0x{0:X}" -f $offset)
            word = ("0x{0:X8}" -f $word)
            instruction = Get-MipsInstructionText $word $address
        }
    }
    return $rows
}

function Find-GlobalAccesses([byte[]]$Ram) {
    $offsets = @{
        0x5828 = "ptrLevelMobys"
        0x573C = "ptrDynamicLevelMobys"
        0x5930 = "ptrLevelMobySpecialData"
    }

    $hits = @()
    for ($offset = 0; $offset -le ($Ram.Length - 4); $offset += 4) {
        $word = Get-UInt32LE $Ram $offset
        $op = ($word -shr 26) -band 0x3F
        $imm = [int]($word -band 0xFFFF)
        if (($op -ne 0x23 -and $op -ne 0x2B) -or -not $offsets.ContainsKey($imm)) { continue }

        $hits += [ordered]@{
            globalName = $offsets[$imm]
            globalAddress = ("0x8007{0:X4}" -f $imm)
            access = $(if ($op -eq 0x2B) { "write" } else { "read" })
            runtimeAddress = Format-RuntimeAddress $offset
            ramOffset = ("0x{0:X}" -f $offset)
            word = ("0x{0:X8}" -f $word)
            instruction = Get-MipsInstructionText $word ($PsxRamBase + [uint64]$offset)
            context = Get-CodeContext $Ram $offset
        }
    }
    return $hits
}

function Get-LegacyAliasIndex([int]$TrueIndex) {
    $legacyNumerator = ($TrueIndex * $TrueStride) + 8
    if (($legacyNumerator % $LegacyStride) -ne 0) { return $null }
    return [int]($legacyNumerator / $LegacyStride)
}

function Convert-LegacyIndexToTrue([int]$LegacyIndex) {
    $trueNumerator = ($LegacyIndex * $LegacyStride) - 8
    if ($trueNumerator -lt 0 -or ($trueNumerator % $TrueStride) -ne 0) { return $null }
    return [int]($trueNumerator / $TrueStride)
}

function Read-TrueMobyRecord([byte[]]$Ram, [int]$Start, [int]$Index) {
    $offset = $Start + ($Index * $TrueStride)
    if (($offset + $TrueStride) -gt $Ram.Length) { return $null }
    $rawX = Get-Int32LE $Ram ($offset + 0x0C)
    $rawY = Get-Int32LE $Ram ($offset + 0x10)
    $rawZ = Get-Int32LE $Ram ($offset + 0x14)
    $type = [int]$Ram[$offset + 0x50]
    $state = [int]$Ram[$offset + 0x51]
    $legacy = Get-LegacyAliasIndex $Index

    return [ordered]@{
        trueIndex = $Index
        legacyAliasIndex = $legacy
        runtimeAddress = Format-RuntimeAddress $offset
        legacyRuntimeAddress = $(if ($null -ne $legacy) { Format-RuntimeAddress ($offset + 8) } else { $null })
        recordOffset = ("0x{0:X}" -f $offset)
        pointer00 = Format-Hex32 (Get-UInt32LE $Ram $offset)
        pointer04 = Format-Hex32 (Get-UInt32LE $Ram ($offset + 0x04))
        specialDataPointer = Format-Hex32 (Get-UInt32LE $Ram ($offset + 0x08))
        rawX = $rawX
        rawY = $rawY
        rawZ = $rawZ
        x = [Math]::Round($rawX / 16.0, 4)
        y = [Math]::Round($rawY / 16.0, 4)
        z = [Math]::Round($rawZ / 16.0, 4)
        type = $type
        typeHex = ("0x{0:X2}" -f $type)
        state = $state
        stateHex = ("0x{0:X2}" -f $state)
        flag52 = [int]$Ram[$offset + 0x52]
        flag52Hex = ("0x{0:X2}" -f [int]$Ram[$offset + 0x52])
        flag53 = [int]$Ram[$offset + 0x53]
        flag53Hex = ("0x{0:X2}" -f [int]$Ram[$offset + 0x53])
        field34 = ("0x{0:X8}" -f (Get-UInt32LE $Ram ($offset + 0x34)))
        field36 = ("0x{0:X4}" -f (Get-UInt16LE $Ram ($offset + 0x36)))
        field44 = ("0x{0:X8}" -f (Get-UInt32LE $Ram ($offset + 0x44)))
        field48 = ("0x{0:X8}" -f (Get-UInt32LE $Ram ($offset + 0x48)))
        field50 = ("0x{0:X8}" -f (Get-UInt32LE $Ram ($offset + 0x50)))
    }
}

function Test-PlacementRecord($Record) {
    if ($null -eq $Record) { return $false }
    if ([int]$Record.type -le 0 -or [int]$Record.type -ge 0xFF) { return $false }
    if ([Math]::Abs([int64]$Record.rawX) -gt 4000000 -or [Math]::Abs([int64]$Record.rawY) -gt 4000000 -or [Math]::Abs([int64]$Record.rawZ) -gt 4000000) { return $false }
    if ([int]$Record.rawX -eq 0 -and [int]$Record.rawY -eq 4096 -and [int]$Record.rawZ -eq 0) { return $false }
    return $true
}

function Load-NativeEditMappings([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    $root = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    $mappings = @()
    foreach ($edit in @(Get-ArrayField $root "edits")) {
        $legacyIndex = [int](Get-Field $edit "index" -1)
        if ($legacyIndex -lt 0) { continue }
        $trueIndex = Convert-LegacyIndexToTrue $legacyIndex
        $mappings += [ordered]@{
            legacyIndex = $legacyIndex
            trueIndex = $trueIndex
            label = [string](Get-Field $edit "label" "")
            typeHex = [string](Get-Field $edit "typeHex" "")
            stateHex = [string](Get-Field $edit "stateHex" "")
            runtimeAddress = [string](Get-Field $edit "runtimeAddress" "")
            original = Get-Field $edit "original" $null
            edited = Get-Field $edit "edited" $null
            patchLead = [string](Get-Field $edit "patchLead" "")
            behaviorNote = [string](Get-Field $edit "behaviorNote" "")
        }
    }
    return $mappings
}

$patternScannerSource = @"
using System;
using System.Collections.Generic;
using System.Text;

public static class StoneHillExactPatternScanner {
    public static string Scan(byte[] bytes, string spec, int maxPerPattern) {
        List<Tuple<string, byte[]>> patterns = new List<Tuple<string, byte[]>>();
        foreach (string line in spec.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries)) {
            string[] parts = line.Trim().Split('|');
            if (parts.Length != 2) continue;
            string hex = parts[1];
            byte[] pattern = new byte[hex.Length / 2];
            for (int i = 0; i < pattern.Length; i++) {
                pattern[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            patterns.Add(Tuple.Create(parts[0], pattern));
        }

        StringBuilder sb = new StringBuilder();
        foreach (Tuple<string, byte[]> item in patterns) {
            byte first = item.Item2[0];
            int emitted = 0;
            for (int offset = 0; offset <= bytes.Length - item.Item2.Length; offset++) {
                if (bytes[offset] != first) continue;
                bool ok = true;
                for (int j = 1; j < item.Item2.Length; j++) {
                    if (bytes[offset + j] != item.Item2[j]) {
                        ok = false;
                        break;
                    }
                }
                if (!ok) continue;
                sb.Append(item.Item1).Append('|').Append(offset).Append('\n');
                emitted++;
                if (emitted >= maxPerPattern) break;
            }
        }
        return sb.ToString();
    }
}
"@

Add-Type -TypeDefinition $patternScannerSource

function Get-ByteHex([byte[]]$Bytes) {
    return (($Bytes | ForEach-Object { $_.ToString("X2") }) -join "")
}

function Get-CoordinateTriplePattern([byte[]]$Ram, [int]$RecordOffset) {
    $bytes = New-Object byte[] 12
    [Array]::Copy([BitConverter]::GetBytes((Get-Int32LE $Ram ($RecordOffset + 0x0C))), 0, $bytes, 0, 4)
    [Array]::Copy([BitConverter]::GetBytes((Get-Int32LE $Ram ($RecordOffset + 0x10))), 0, $bytes, 4, 4)
    [Array]::Copy([BitConverter]::GetBytes((Get-Int32LE $Ram ($RecordOffset + 0x14))), 0, $bytes, 8, 4)
    return Get-ByteHex $bytes
}

function Invoke-StoneHillSourceProbe([byte[]]$Ram, [object[]]$Mappings, [int]$LevelOffset, [string]$ResolvedImagePath) {
    if (-not (Test-Path -LiteralPath $ResolvedImagePath)) {
        return [ordered]@{
            imagePath = $ResolvedImagePath
            available = $false
            note = "Image path was not found; exact Stone Hill WAD source probe was skipped."
            hits = @()
        }
    }

    $specLines = New-Object System.Collections.ArrayList
    foreach ($mapping in @($Mappings)) {
        $trueIndex = Get-Field $mapping "trueIndex" $null
        if ($null -eq $trueIndex) { continue }
        $recordOffset = $LevelOffset + ([int]$trueIndex * $TrueStride)
        if (($recordOffset + $TrueStride) -gt $Ram.Length) { continue }
        $pattern = Get-CoordinateTriplePattern $Ram $recordOffset
        [void]$specLines.Add("$trueIndex|$pattern")
    }

    if ($specLines.Count -eq 0) {
        return [ordered]@{
            imagePath = $ResolvedImagePath
            available = $true
            note = "No mapped native-edit records were available for exact coordinate-triple probing."
            hits = @()
        }
    }

    $stream = [System.IO.File]::OpenRead($ResolvedImagePath)
    try {
        $metadata = Read-WadBytes $stream $StoneMetadataOffset $StoneMetadataSize
        $asset = Read-WadBytes $stream $StoneAssetOffset $StoneAssetSize
    }
    finally {
        $stream.Dispose()
    }

    $hits = @()
    foreach ($region in @(
        [ordered]@{ name = "stone-hill-metadata"; base = $StoneMetadataOffset; bytes = $metadata },
        [ordered]@{ name = "stone-hill-asset"; base = $StoneAssetOffset; bytes = $asset }
    )) {
        $scanText = [StoneHillExactPatternScanner]::Scan([byte[]]$region.bytes, ($specLines.ToArray() -join "`n"), 8)
        foreach ($line in @($scanText -split "`n")) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $parts = $line.Split("|")
            if ($parts.Length -ne 2) { continue }
            $offset = [int]$parts[1]
            $hits += [ordered]@{
                trueIndex = [int]$parts[0]
                region = [string]$region.name
                regionOffset = ("0x{0:X}" -f $offset)
                wadOffset = ("0x{0:X}" -f ([int64]$region.base + [int64]$offset))
            }
        }
    }

    return [ordered]@{
        imagePath = (Resolve-Path -LiteralPath $ResolvedImagePath).Path
        available = $true
        note = "Exact raw int32 coordinate triples for mapped edited records were searched inside Stone Hill metadata and asset WAD entries only."
        hitCount = $hits.Count
        hits = $hits
    }
}

function Invoke-CandidateSourceTableProbe([byte[]]$Ram, [object[]]$Mappings, [int]$LevelOffset, [int]$LevelCount, [string]$ResolvedImagePath) {
    if (-not (Test-Path -LiteralPath $ResolvedImagePath)) {
        return [ordered]@{
            available = $false
            note = "Image path was not found; candidate source-table probe was skipped."
        }
    }

    $length = $LevelCount * $TrueStride
    $sourceWadOffset = [int64]$CandidateSourceEntryOffset + [int64]$CandidateSourceTableRel
    $stream = [System.IO.File]::OpenRead($ResolvedImagePath)
    try {
        $sourceBytes = Read-WadBytes $stream $sourceWadOffset $length
    }
    finally {
        $stream.Dispose()
    }

    $same = 0
    $diff = 0
    for ($i = 0; $i -lt $length; $i++) {
        if ($sourceBytes[$i] -eq $Ram[$LevelOffset + $i]) { $same++ } else { $diff++ }
    }

    $coordinateMatches = @()
    foreach ($mapping in @($Mappings)) {
        $trueIndex = Get-Field $mapping "trueIndex" $null
        if ($null -eq $trueIndex) { continue }
        $recordRel = [int]$trueIndex * $TrueStride
        $axisMatches = @()
        foreach ($axis in @("x", "y", "z")) {
            $fieldOffset = switch ($axis) {
                "x" { 0x0C }
                "y" { 0x10 }
                default { 0x14 }
            }
            $runtimeValue = Get-Int32LE $Ram ($LevelOffset + $recordRel + $fieldOffset)
            $sourceValue = [BitConverter]::ToInt32($sourceBytes, $recordRel + $fieldOffset)
            $axisMatches += [ordered]@{
                axis = $axis
                runtimeRaw = $runtimeValue
                sourceRaw = $sourceValue
                match = ($runtimeValue -eq $sourceValue)
                sourceWadOffset = ("0x{0:X}" -f ($sourceWadOffset + [int64]$recordRel + [int64]$fieldOffset))
            }
        }
        $coordinateMatches += [ordered]@{
            legacyIndex = [int](Get-Field $mapping "legacyIndex" -1)
            trueIndex = [int]$trueIndex
            label = [string](Get-Field $mapping "label" "")
            allAxesMatch = -not @($axisMatches | Where-Object { -not $_.match })
            axes = $axisMatches
        }
    }

    return [ordered]@{
        available = $true
        wadEntry = $CandidateSourceEntry
        wadEntryOffset = ("0x{0:X}" -f $CandidateSourceEntryOffset)
        tableRelativeOffset = ("0x{0:X}" -f $CandidateSourceTableRel)
        tableWadOffset = ("0x{0:X}" -f $sourceWadOffset)
        comparedBytes = $length
        sameBytes = $same
        diffBytes = $diff
        samePercent = [Math]::Round($same * 100.0 / [Math]::Max(1, $length), 2)
        note = "Entry 12 has a 0x58-stride table whose coordinate fields match the corrected runtime moby table; pointer/initializer fields differ after load."
        coordinateMatches = $coordinateMatches
    }
}

$resolvedRamPath = Resolve-WorkspacePath $RamPath
$resolvedImagePath = Resolve-WorkspacePath $ImagePath
$resolvedNativeEditsPath = Resolve-WorkspacePath $NativeEditsPath
$resolvedOutJsonPath = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath

if (-not (Test-Path -LiteralPath $resolvedRamPath)) { throw "Missing RAM dump: $resolvedRamPath" }
$ram = [System.IO.File]::ReadAllBytes($resolvedRamPath)
if ($ram.Length -ne $RamSize) { throw "Expected 2 MB RAM dump, got $($ram.Length) bytes." }

$levelPointer = Get-UInt32LE $ram 0x75828
$dynamicPointer = Get-UInt32LE $ram 0x7573C
$specialDataPointer = Get-UInt32LE $ram 0x75930
if (-not (Test-PsxPointer $levelPointer)) { throw "_ptr_levelMobys is not a valid main RAM pointer: $(Format-Hex32 $levelPointer)" }
if (-not (Test-PsxPointer $dynamicPointer)) { throw "_ptrDynamicLevelMobys is not a valid main RAM pointer: $(Format-Hex32 $dynamicPointer)" }

$levelOffset = Convert-PointerToOffset $levelPointer
$dynamicOffset = Convert-PointerToOffset $dynamicPointer
$levelByteLength = $dynamicOffset - $levelOffset
$levelCount = if ($levelByteLength -gt 0 -and ($levelByteLength % $TrueStride) -eq 0) { [int]($levelByteLength / $TrueStride) } else { 0 }

$records = @()
for ($i = 0; $i -lt $levelCount; $i++) {
    $record = Read-TrueMobyRecord $ram $levelOffset $i
    if ($null -ne $record) {
        $record["placementLike"] = Test-PlacementRecord $record
        $records += [pscustomobject]$record
    }
}

$nativeMappings = @(Load-NativeEditMappings $resolvedNativeEditsPath)
$nativeMappedRecords = @()
foreach ($mapping in $nativeMappings) {
    $trueIndex = Get-Field $mapping "trueIndex" $null
    $record = $null
    if ($null -ne $trueIndex) {
        $record = @($records | Where-Object { [int]$_.trueIndex -eq [int]$trueIndex } | Select-Object -First 1)
    }
    $nativeMappedRecords += [ordered]@{
        legacyIndex = [int](Get-Field $mapping "legacyIndex" -1)
        trueIndex = $trueIndex
        label = [string](Get-Field $mapping "label" "")
        oldRuntimeAddress = [string](Get-Field $mapping "runtimeAddress" "")
        trueRuntimeAddress = $(if ($null -ne $record) { [string]$record.runtimeAddress } else { $null })
        typeHex = $(if ($null -ne $record) { [string]$record.typeHex } else { [string](Get-Field $mapping "typeHex" "") })
        stateHex = $(if ($null -ne $record) { [string]$record.stateHex } else { [string](Get-Field $mapping "stateHex" "") })
        specialDataPointer = $(if ($null -ne $record) { [string]$record.specialDataPointer } else { $null })
        x = $(if ($null -ne $record) { [double]$record.x } else { $null })
        y = $(if ($null -ne $record) { [double]$record.y } else { $null })
        z = $(if ($null -ne $record) { [double]$record.z } else { $null })
        patchLead = [string](Get-Field $mapping "patchLead" "")
        behaviorNote = [string](Get-Field $mapping "behaviorNote" "")
    }
}

$globalAccesses = @(Find-GlobalAccesses $ram)
$writerHits = @($globalAccesses | Where-Object { $_.access -eq "write" })
$readerHits = @($globalAccesses | Where-Object { $_.access -eq "read" })

$legacyVisible = @($records | Where-Object { $null -ne $_.legacyAliasIndex })
$placementRecords = @($records | Where-Object { $_.placementLike })
$typeSummary = @($placementRecords |
    Group-Object typeHex |
    Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, Name |
    ForEach-Object {
        [ordered]@{
            typeHex = $_.Name
            count = $_.Count
            sampleTrueIndexes = @($_.Group | Select-Object -First 8 -ExpandProperty trueIndex)
            sampleLegacyAliases = @($_.Group | Where-Object { $null -ne $_.legacyAliasIndex } | Select-Object -First 8 -ExpandProperty legacyAliasIndex)
        }
    })

$sourceProbe = Invoke-StoneHillSourceProbe $ram $nativeMappings $levelOffset $resolvedImagePath
$candidateSourceTable = Invoke-CandidateSourceTableProbe $ram $nativeMappings $levelOffset $levelCount $resolvedImagePath

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    ramPath = (Resolve-Path -LiteralPath $resolvedRamPath).Path
    imagePath = $(if (Test-Path -LiteralPath $resolvedImagePath) { (Resolve-Path -LiteralPath $resolvedImagePath).Path } else { $resolvedImagePath })
    nativeEditsPath = $(if (Test-Path -LiteralPath $resolvedNativeEditsPath) { (Resolve-Path -LiteralPath $resolvedNativeEditsPath).Path } else { "" })
    conclusion = "Stone Hill's authoritative level moby table uses 0x58-byte records. The previous 0x50 decoder landed eight bytes into every tenth true record, so the editor exposed a sparse alias view rather than the real loader table."
    globals = [ordered]@{
        ptrLevelMobysAddress = "0x80075828"
        ptrLevelMobys = Format-Hex32 $levelPointer
        ptrDynamicLevelMobysAddress = "0x8007573C"
        ptrDynamicLevelMobys = Format-Hex32 $dynamicPointer
        ptrLevelMobySpecialDataAddress = "0x80075930"
        ptrLevelMobySpecialData = Format-Hex32 $specialDataPointer
    }
    trueLayout = [ordered]@{
        strideBytes = $TrueStride
        strideHex = "0x58"
        recordCountFromDynamicPointer = $levelCount
        byteLength = $levelByteLength
        coordinates = "raw int32 at +0x0C/+0x10/+0x14, scaled /16"
        specialDataPointer = "+0x08"
        typeStateFlags = "type +0x50, state +0x51, flags +0x52/+0x53"
        terminatorEvidence = "Common loader writes 0xFF to +0x48 at the dynamic-table boundary and initializes active records with 0x20 at +0x50."
    }
    legacyLayout = [ordered]@{
        strideBytes = $LegacyStride
        strideHex = "0x50"
        explanation = "The old decoder starts at true record +8. It only aligns every ten true records, producing legacy labels such as L10, L21, L32, ..., L186."
        visibleAliasCount = $legacyVisible.Count
    }
    loaderCode = [ordered]@{
        writes = $writerHits
        readCount = $readerHits.Count
        writeSummary = @(
            "0x80013F38 stores the stream/current block pointer into _ptr_levelMobys, reads a count, and computes later moby globals with count * 0x58.",
            "0x80014AA0 stores another heap-derived pointer into _ptr_levelMobys and loops with index * 0x58 while initializing records."
        )
    }
    nativeEditMappings = $nativeMappedRecords
    sourceProbe = $sourceProbe
    candidateSourceTable = $candidateSourceTable
    typeSummary = $typeSummary
    records = $records
}

$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedOutJsonPath -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Stone Hill Loader Table Analysis")
[void]$lines.Add("")
[void]$lines.Add("- Generated: $($result.generatedAt)")
[void]$lines.Add("- RAM: $($result.ramPath)")
[void]$lines.Add("- Conclusion: $($result.conclusion)")
[void]$lines.Add("")
[void]$lines.Add("## True loader layout")
[void]$lines.Add("")
[void]$lines.Add("- _ptr_levelMobys = $($result.globals.ptrLevelMobys), _ptrDynamicLevelMobys = $($result.globals.ptrDynamicLevelMobys)")
[void]$lines.Add("- True level table byte length: $levelByteLength bytes, count: $levelCount records, stride: 0x58")
[void]$lines.Add("- Coordinates: raw int32 at +0x0C/+0x10/+0x14, scaled /16")
[void]$lines.Add("- Special-data pointer: +0x08; type/state/flags: +0x50/+0x51/+0x52/+0x53")
[void]$lines.Add("- Previous 0x50 view starts at true record +8; it exposes only every tenth true record.")
[void]$lines.Add("- Exact raw int32 coordinate triples in Stone Hill WAD: $($sourceProbe.hitCount)")
[void]$lines.Add("- Candidate source table: WAD entry $($candidateSourceTable.wadEntry) at $($candidateSourceTable.tableWadOffset), byte match $($candidateSourceTable.samePercent)%")
[void]$lines.Add("")
[void]$lines.Add("## Source probe")
[void]$lines.Add("")
[void]$lines.Add("- $($sourceProbe.note)")
[void]$lines.Add("- Hits inside Stone Hill metadata/asset WAD entries: $($sourceProbe.hitCount)")
[void]$lines.Add("- $($candidateSourceTable.note)")
[void]$lines.Add("- Candidate table: entry $($candidateSourceTable.wadEntry), rel $($candidateSourceTable.tableRelativeOffset), WAD $($candidateSourceTable.tableWadOffset), compared $($candidateSourceTable.comparedBytes) bytes, same $($candidateSourceTable.samePercent)%")
[void]$lines.Add("")
[void]$lines.Add("## Loader write sites")
[void]$lines.Add("")
foreach ($hit in $writerHits) {
    [void]$lines.Add("- $($hit.runtimeAddress) $($hit.instruction) -> $($hit.globalName) ($($hit.globalAddress))")
}
[void]$lines.Add("")
[void]$lines.Add("## Native edit remap")
[void]$lines.Add("")
[void]$lines.Add("| old label | true index | type | state | true address | x | y | z |")
[void]$lines.Add("|---:|---:|---:|---:|---|---:|---:|---:|")
foreach ($row in $nativeMappedRecords) {
    [void]$lines.Add("| L$($row.legacyIndex) $($row.label) | $($row.trueIndex) | $($row.typeHex) | $($row.stateHex) | $($row.trueRuntimeAddress) | $($row.x) | $($row.y) | $($row.z) |")
}
[void]$lines.Add("")
[void]$lines.Add("## Placement type summary")
[void]$lines.Add("")
[void]$lines.Add("| type | count | sample true indexes | sample old aliases |")
[void]$lines.Add("|---:|---:|---|---|")
foreach ($row in $typeSummary) {
    [void]$lines.Add("| $($row.typeHex) | $($row.count) | $(@($row.sampleTrueIndexes) -join ', ') | $(@($row.sampleLegacyAliases | ForEach-Object { "L$_" }) -join ', ') |")
}
[void]$lines.Add("")
[void]$lines.Add("## Why broad BIN did not move Stone Hill")
[void]$lines.Add("")
[void]$lines.Add("The broad BIN patched candidates from the sparse 0x50 alias model and scaled-int16 source coincidences. The loader evidence says the game builds a 0x58 table, and the real source is upstream of that table. The next patch attempt should target the true 0x58 source path, not the old alias windows.")

$lines | Set-Content -LiteralPath $resolvedOutMarkdownPath -Encoding UTF8

Write-Host "Wrote loader table analysis to $resolvedOutJsonPath"
Write-Host "Wrote loader table summary to $resolvedOutMarkdownPath"
Write-Host ("True records: {0}; legacy-visible aliases: {1}; placement-like records: {2}" -f $records.Count, $legacyVisible.Count, $placementRecords.Count)
