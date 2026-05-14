param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$OutPath = ".\stonehill-runtime-moby-tables.json",
    [int]$MaxSlots = 512
)

Set-StrictMode -Version 2.0

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Copy-ByteRange([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $copy = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Offset, $copy, 0, $Length)
    return $copy
}

function Test-PsxPointerInMainRam([uint32]$Pointer) {
    $address = [uint64]$Pointer
    return ($address -ge [Convert]::ToUInt64("80000000", 16) -and $address -lt [Convert]::ToUInt64("80200000", 16))
}

function Test-PlausibleRuntimeMoby([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 80) -gt $Ram.Length) { return $false }
    $type = [int]$Ram[$Offset + 0x48]
    $state = [int]$Ram[$Offset + 0x49]
    if ($type -le 0 -or $type -gt 0x7F) { return $false }
    if ($state -gt 0x7F) { return $false }
    $x = Get-Int32LE $Ram ($Offset + 4)
    $y = Get-Int32LE $Ram ($Offset + 8)
    $z = Get-Int32LE $Ram ($Offset + 12)
    if ([Math]::Abs([int64]$x) -gt 4000000 -or [Math]::Abs([int64]$y) -gt 4000000 -or [Math]::Abs([int64]$z) -gt 4000000) { return $false }
    if ([Math]::Abs([int64]$x) -lt 16 -and [Math]::Abs([int64]$y) -lt 16 -and [Math]::Abs([int64]$z) -lt 16) { return $false }
    return $true
}

function Count-PlausibleMobysAtPointer([byte[]]$Bytes, [int]$WindowBase, [uint32]$Pointer) {
    if (-not (Test-PsxPointerInMainRam $Pointer)) { return 0 }
    $tableOffset = [int]($Pointer -band 0x001FFFFF)
    $count = 0
    for ($i = 0; $i -lt 128; $i++) {
        $recordOffset = $WindowBase + $tableOffset + ($i * 80)
        if (($recordOffset + 80) -gt $Bytes.Length) { break }
        if (Test-PlausibleRuntimeMoby $Bytes $recordOffset) { $count++ }
    }
    return $count
}

function Find-PsxRamWindow([byte[]]$Bytes) {
    $windowSize = 0x200000
    if ($Bytes.Length -lt $windowSize) { throw "RAM dump is smaller than 2 MB." }

    $candidateBases = New-Object System.Collections.Generic.List[int]
    foreach ($base in @(0, 0x80, 0x100, 0x200, 0x400, 0x800, 0x1000, ($Bytes.Length - $windowSize))) {
        if ($base -ge 0 -and ($base + $windowSize) -le $Bytes.Length -and -not $candidateBases.Contains($base)) {
            [void]$candidateBases.Add($base)
        }
    }
    for ($base = 0; $base -le ($Bytes.Length - $windowSize); $base += 0x10000) {
        if (-not $candidateBases.Contains($base)) { [void]$candidateBases.Add($base) }
    }

    $best = $null
    foreach ($base in $candidateBases) {
        $pointer = Get-UInt32LE $Bytes ($base + 0x75828)
        $count = Count-PlausibleMobysAtPointer $Bytes $base $pointer
        $levelId = Get-UInt32LE $Bytes ($base + 0x758B4)
        $score = $count
        if (Test-PsxPointerInMainRam $pointer) { $score += 4 }
        if ($levelId -gt 0 -and $levelId -lt 0x80) { $score += 2 }
        if ($null -eq $best -or $score -gt $best.score) {
            $best = [ordered]@{
                baseOffset = $base
                pointer = $pointer
                levelId = $levelId
                count = $count
                score = $score
            }
        }
    }

    if ($null -eq $best -or -not (Test-PsxPointerInMainRam ([uint32]$best.pointer)) -or $best.count -eq 0) {
        throw "Could not find a live Spyro moby table in the RAM dump."
    }

    return [ordered]@{
        ram = Copy-ByteRange $Bytes $best.baseOffset $windowSize
        baseOffset = $best.baseOffset
        pointer = [uint32]$best.pointer
        levelId = [uint32]$best.levelId
        count = [int]$best.count
    }
}

function Get-MobyHint([int]$TypeId, [bool]$SparseAfterGap, [string]$TableName) {
    if ($TableName -ne "level" -or $SparseAfterGap) {
        return [ordered]@{ kind = "sparse/runtime-table candidate"; confidence = "weak"; note = "Decoded outside the clean contiguous level-moby run; verify with focused RAM samples." }
    }
    if ($TypeId -eq 0x20) {
        return [ordered]@{ kind = "likely gem/collectible"; confidence = "strong"; note = "Confirmed by the available Stone Hill before/after gem collection RAM sample." }
    }
    if ($TypeId -eq 0x30) {
        return [ordered]@{ kind = "dragon/NPC candidate"; confidence = "weak"; note = "Placement-like records with special-data pointers; needs save/free-dragon sample." }
    }
    if ($TypeId -eq 0x18) {
        return [ordered]@{ kind = "active object/enemy candidate"; confidence = "weak"; note = "Repeated placed records; compare against ram/shepherd/fodder samples." }
    }
    if ($TypeId -eq 0x0A -or $TypeId -eq 0x33 -or $TypeId -eq 0x52) {
        return [ordered]@{ kind = "level helper/system candidate"; confidence = "weak"; note = "Often appears at placeholder coordinates or unusual states." }
    }
    return [ordered]@{ kind = "unidentified moby"; confidence = "unknown"; note = "Needs focused before/after RAM samples." }
}

function Read-MobyTable([byte[]]$Ram, [uint32]$Pointer, [string]$TableName, [int]$MaxSlots) {
    if (-not (Test-PsxPointerInMainRam $Pointer)) {
        return [ordered]@{
            table = $TableName
            pointer = ("0x{0:X8}" -f $Pointer)
            validPointer = $false
            decodedCount = 0
            placementCount = 0
            typeSummary = @()
            records = @()
        }
    }

    $start = [int]($Pointer -band 0x001FFFFF)
    $records = @()
    $badRun = 0
    $afterLongGap = $false
    for ($i = 0; $i -lt $MaxSlots; $i++) {
        $offset = $start + ($i * 80)
        if (($offset + 80) -gt $Ram.Length) { break }
        $gapBeforeRecord = $badRun
        if (-not (Test-PlausibleRuntimeMoby $Ram $offset)) {
            $badRun++
            if ($badRun -ge 24) { $afterLongGap = $true }
            continue
        }
        $badRun = 0

        $rawX = Get-Int32LE $Ram ($offset + 4)
        $rawY = Get-Int32LE $Ram ($offset + 8)
        $rawZ = Get-Int32LE $Ram ($offset + 12)
        $typeId = [int]$Ram[$offset + 0x48]
        $state = [int]$Ram[$offset + 0x49]
        $sparseAfterGap = ($afterLongGap -or $gapBeforeRecord -ge 24)
        $hint = Get-MobyHint $typeId $sparseAfterGap $TableName
        $records += [pscustomobject][ordered]@{
            table = $TableName
            index = $i
            runtimeAddress = ("0x{0:X8}" -f (0x80000000 + $offset))
            ramOffset = ("0x{0:X}" -f $offset)
            typeId = $typeId
            typeHex = ("0x{0:X2}" -f $typeId)
            state = $state
            stateHex = ("0x{0:X2}" -f $state)
            x = [Math]::Round($rawX / 16.0, 2)
            y = [Math]::Round($rawY / 16.0, 2)
            z = [Math]::Round($rawZ / 16.0, 2)
            rawX = $rawX
            rawY = $rawY
            rawZ = $rawZ
            specialDataPointer = ("0x{0:X8}" -f (Get-UInt32LE $Ram $offset))
            flag4A = [int]$Ram[$offset + 0x4A]
            flag4B = [int]$Ram[$offset + 0x4B]
            sparseAfterGap = $sparseAfterGap
            gapBeforeRecord = $gapBeforeRecord
            candidateKind = $hint.kind
            confidence = $hint.confidence
            evidence = $hint.note
        }
    }

    $typeSummary = @($records |
        Group-Object typeHex |
        Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, @{ Expression = { $_.Name }; Descending = $false } |
        ForEach-Object {
            $sample = $_.Group | Select-Object -First 1
            [pscustomobject][ordered]@{
                typeHex = $_.Name
                count = $_.Count
                candidateKind = $sample.candidateKind
                confidence = $sample.confidence
            }
        })
    $placements = @($records | Where-Object { -not ($_.rawX -eq 0 -and $_.rawY -eq 4096 -and $_.rawZ -eq 0) })

    return [ordered]@{
        table = $TableName
        pointer = ("0x{0:X8}" -f $Pointer)
        validPointer = $true
        decodedCount = $records.Count
        placementCount = $placements.Count
        typeSummary = $typeSummary
        records = $records
    }
}

$ramBytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath).Path)
$window = Find-PsxRamWindow $ramBytes
$ram = [byte[]]$window.ram

$levelPointer = Get-UInt32LE $ram 0x75828
$dynamicPointer = Get-UInt32LE $ram 0x7573C
$specialDataPointer = Get-UInt32LE $ram 0x75930

$tables = @(
    (Read-MobyTable $ram $levelPointer "level" $MaxSlots),
    (Read-MobyTable $ram $dynamicPointer "dynamic" $MaxSlots),
    (Read-MobyTable $ram $specialDataPointer "special-data-as-moby-probe" $MaxSlots)
)

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    ramPath = (Resolve-Path -LiteralPath $RamPath).Path
    ramBaseOffset = ("0x{0:X}" -f [int]$window.baseOffset)
    levelId = ("0x{0:X8}" -f [uint32]$window.levelId)
    globals = [ordered]@{
        ptrLevelMobys = ("0x{0:X8}" -f $levelPointer)
        ptrDynamicLevelMobys = ("0x{0:X8}" -f $dynamicPointer)
        ptrLevelMobySpecialData = ("0x{0:X8}" -f $specialDataPointer)
    }
    notes = @(
        "Level table is the current clean editable source for the editor.",
        "Dynamic and special-data probes are included because Spyro 1 docs identify separate runtime arrays; sparse records need focused RAM samples before treating them as editable Stone Hill objects."
    )
    tables = $tables
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8
Write-Output "Wrote runtime moby table analysis to $resolvedOut"
