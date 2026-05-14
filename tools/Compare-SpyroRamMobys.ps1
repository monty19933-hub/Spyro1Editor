param(
    [Parameter(Mandatory = $true)]
    [string]$BeforePath,

    [Parameter(Mandatory = $true)]
    [string]$AfterPath,

    [string]$OutPath = ".\stonehill-ram-moby-diff.json"
)

Set-StrictMode -Version 2.0

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Test-PsxPointerInMainRam([uint32]$Pointer) {
    $address = [uint64]$Pointer
    return ($address -ge [Convert]::ToUInt64("80000000", 16) -and $address -lt [Convert]::ToUInt64("80200000", 16))
}

function Convert-PsxAddressToRamOffset([uint32]$Address, [int]$RamLength) {
    $offset = [int]($Address -band 0x001FFFFF)
    if ($offset -lt 0 -or ($offset + 4) -gt $RamLength) { return -1 }
    return $offset
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

function Copy-ByteRange([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $copy = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Offset, $copy, 0, $Length)
    return $copy
}

function Count-PlausibleMobysAtPointer([byte[]]$Bytes, [int]$WindowBase, [uint32]$Pointer) {
    if (-not (Test-PsxPointerInMainRam $Pointer)) { return 0 }
    $tableOffset = [int]($Pointer -band 0x001FFFFF)
    $count = 0
    for ($i = 0; $i -lt 256; $i++) {
        $recordOffset = $WindowBase + $tableOffset + ($i * 80)
        if (($recordOffset + 80) -gt $Bytes.Length) { break }
        if (Test-PlausibleRuntimeMoby $Bytes $recordOffset) { $count++ }
    }
    return $count
}

function Find-PsxRamWindow([byte[]]$Bytes) {
    $windowSize = 0x200000
    if ($Bytes.Length -eq $windowSize) {
        $pointer = Get-UInt32LE $Bytes 0x75828
        $mobyCount = Count-PlausibleMobysAtPointer $Bytes 0 $pointer
        if (-not (Test-PsxPointerInMainRam $pointer) -or $mobyCount -eq 0) {
            throw ("This 2 MB file does not look like live Spyro main RAM. At raw offset 0x75828 the pointer is 0x{0:X8}, and decoded moby count is {1}." -f $pointer, $mobyCount)
        }
        return [ordered]@{
            ram = $Bytes
            baseOffset = 0
            pointer = $pointer
            levelId = Get-UInt32LE $Bytes 0x758B4
            mobyCount = $mobyCount
        }
    }
    if ($Bytes.Length -lt $windowSize) {
        throw "RAM dump is smaller than 2 MB: $($Bytes.Length) bytes."
    }

    $best = $null
    for ($base = 0; $base -le ($Bytes.Length - $windowSize); $base += 0x1000) {
        $pointer = Get-UInt32LE $Bytes ($base + 0x75828)
        $count = Count-PlausibleMobysAtPointer $Bytes $base $pointer
        if ($null -eq $best -or $count -gt $best.mobyCount) {
            $best = [ordered]@{
                ram = Copy-ByteRange $Bytes $base $windowSize
                baseOffset = $base
                pointer = $pointer
                levelId = Get-UInt32LE $Bytes ($base + 0x758B4)
                mobyCount = $count
            }
        }
    }
    if ($null -eq $best -or $best.mobyCount -eq 0) {
        throw "Could not find a plausible PS1 main RAM window in the dump."
    }
    return $best
}

function Get-RecordBytes([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 80) -gt $Ram.Length) { return @() }
    $bytes = New-Object byte[] 80
    [Array]::Copy($Ram, $Offset, $bytes, 0, 80)
    return $bytes
}

function Convert-BytesToHex([byte[]]$Bytes, [int]$Max = 80) {
    $limit = [Math]::Min($Bytes.Length, $Max)
    $parts = @()
    for ($i = 0; $i -lt $limit; $i++) {
        $parts += ("{0:X2}" -f $Bytes[$i])
    }
    return ($parts -join " ")
}

function Get-RecordHash([byte[]]$Bytes) {
    if ($Bytes.Count -eq 0) { return "" }
    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        return ([BitConverter]::ToString($sha1.ComputeHash($Bytes)) -replace "-", "")
    }
    finally {
        $sha1.Dispose()
    }
}

function Read-MobyRecord([byte[]]$Ram, [int]$Index, [int]$Offset) {
    $bytes = [byte[]](Get-RecordBytes $Ram $Offset)
    $plausible = Test-PlausibleRuntimeMoby $Ram $Offset
    return [ordered]@{
        index = $Index
        ramOffset = $Offset
        runtimeAddress = ("0x{0:X8}" -f (0x80000000 + $Offset))
        plausible = $plausible
        specialDataPointer = ("0x{0:X8}" -f (Get-UInt32LE $Ram $Offset))
        rawX = Get-Int32LE $Ram ($Offset + 4)
        rawY = Get-Int32LE $Ram ($Offset + 8)
        rawZ = Get-Int32LE $Ram ($Offset + 12)
        mapX = [Math]::Round((Get-Int32LE $Ram ($Offset + 4)) / 16.0, 2)
        mapY = [Math]::Round((Get-Int32LE $Ram ($Offset + 8)) / 16.0, 2)
        mapZ = [Math]::Round((Get-Int32LE $Ram ($Offset + 12)) / 16.0, 2)
        typeId = [int]$Ram[$Offset + 0x48]
        typeHex = ("0x{0:X2}" -f [int]$Ram[$Offset + 0x48])
        state = [int]$Ram[$Offset + 0x49]
        stateHex = ("0x{0:X2}" -f [int]$Ram[$Offset + 0x49])
        flag4A = [int]$Ram[$Offset + 0x4A]
        flag4B = [int]$Ram[$Offset + 0x4B]
        hash = Get-RecordHash $bytes
        bytesHex = Convert-BytesToHex $bytes
    }
}

function Read-MobyTable([byte[]]$Ram, [uint32]$Pointer) {
    $start = Convert-PsxAddressToRamOffset $Pointer $Ram.Length
    if ($start -lt 0) {
        throw ("Runtime moby pointer 0x{0:X8} is outside this RAM dump." -f $Pointer)
    }

    $records = @()
    $badRun = 0
    for ($i = 0; $i -lt 512; $i++) {
        $offset = $start + ($i * 80)
        if (($offset + 80) -gt $Ram.Length) { break }
        $record = Read-MobyRecord $Ram $i $offset
        if (-not $record.plausible) {
            $badRun++
            if ($records.Count -gt 8 -and $badRun -ge 24) { break }
            continue
        }
        $badRun = 0
        $records += $record
    }
    return $records
}

function Get-ChangedByteOffsets([byte[]]$A, [byte[]]$B) {
    $changed = @()
    $limit = [Math]::Min($A.Length, $B.Length)
    for ($i = 0; $i -lt $limit; $i++) {
        if ($A[$i] -ne $B[$i]) {
            $changed += ("0x{0:X2}:{1:X2}->{2:X2}" -f $i, $A[$i], $B[$i])
        }
    }
    return $changed
}

function Get-ChangedBits([byte]$Before, [byte]$After) {
    $bits = @()
    for ($bit = 0; $bit -lt 8; $bit++) {
        $mask = 1 -shl $bit
        $beforeSet = (($Before -band $mask) -ne 0)
        $afterSet = (($After -band $mask) -ne 0)
        if ($beforeSet -ne $afterSet) {
            $bits += [ordered]@{
                bit = $bit
                before = $beforeSet
                after = $afterSet
                direction = $(if ($afterSet) { "set" } else { "cleared" })
            }
        }
    }
    return $bits
}

function Compare-CollectableStateFlags([byte[]]$BeforeRam, [byte[]]$AfterRam) {
    $baseOffset = 0x77900
    $length = 1231
    $changes = @()
    for ($i = 0; $i -lt $length; $i++) {
        $offset = $baseOffset + $i
        if ($offset -ge $BeforeRam.Length -or $offset -ge $AfterRam.Length) { break }
        $before = [byte]$BeforeRam[$offset]
        $after = [byte]$AfterRam[$offset]
        if ($before -eq $after) { continue }
        $changes += [ordered]@{
            index = $i
            ramOffset = ("0x{0:X}" -f $offset)
            runtimeAddress = ("0x{0:X8}" -f (0x80000000 + $offset))
            beforeHex = ("0x{0:X2}" -f $before)
            afterHex = ("0x{0:X2}" -f $after)
            changedBits = @(Get-ChangedBits $before $after)
        }
    }
    return $changes
}

function Read-ProgressGlobals([byte[]]$Ram) {
    return [ordered]@{
        globalGemCount = [int](Get-UInt16LE $Ram 0x75860)
        globalDragonCount = Get-Int32LE $Ram 0x75750
        globalEggCount = Get-Int32LE $Ram 0x75810
        levelId = Get-UInt32LE $Ram 0x758B4
        collectablesStateFlagsAddress = "0x80077900"
        collectablesStateFlagsLength = 1231
    }
}

function Compare-ProgressGlobals([byte[]]$BeforeRam, [byte[]]$AfterRam) {
    $before = Read-ProgressGlobals $BeforeRam
    $after = Read-ProgressGlobals $AfterRam
    $counterChanges = @()
    foreach ($name in @("globalGemCount", "globalDragonCount", "globalEggCount", "levelId")) {
        if ($before[$name] -ne $after[$name]) {
            $counterChanges += [ordered]@{
                name = $name
                before = $before[$name]
                after = $after[$name]
                delta = ([int64]$after[$name] - [int64]$before[$name])
            }
        }
    }
    $flagChanges = @(Compare-CollectableStateFlags $BeforeRam $AfterRam)
    return [ordered]@{
        source = "Spyro 1 public symbol map: _globalGemCount=0x80075860, _globalDragonCount=0x80075750, _globalEggCount=0x80075810, _collectablesStateFlags=0x80077900[1231]."
        before = $before
        after = $after
        counterChanges = $counterChanges
        collectableFlagChangeCount = $flagChanges.Count
        collectableFlagChanges = $flagChanges
        interpretation = $(if (@($counterChanges | Where-Object { $_.name -eq "globalGemCount" -and $_.delta -gt 0 }).Count -gt 0) {
            "Global gem count increased; changed mobys and collectable flags are likely tied to a gem/treasure pickup sample."
        } elseif (@($counterChanges | Where-Object { $_.name -eq "globalDragonCount" -and $_.delta -gt 0 }).Count -gt 0) {
            "Global dragon count increased; changed mobys and collectable flags are likely tied to a freed dragon sample."
        } elseif (@($counterChanges | Where-Object { $_.name -eq "globalEggCount" -and $_.delta -gt 0 }).Count -gt 0) {
            "Global egg count increased; changed mobys and collectable flags are likely tied to an egg thief sample."
        } elseif ($flagChanges.Count -gt 0) {
            "Collectable flags changed without a known global counter increase; inspect changed mobys and HUD/state records."
        } else {
            "No persistent collectable counter or collectable flag changes were detected."
        })
    }
}

function Compare-MobyRecord($Before, $After) {
    $beforeBytes = [byte[]](Get-RecordBytes $script:BeforeRam $Before.ramOffset)
    $afterBytes = [byte[]](Get-RecordBytes $script:AfterRam $Before.ramOffset)
    $changedBytes = @(Get-ChangedByteOffsets $beforeBytes $afterBytes)
    $fieldChanges = @()
    foreach ($field in @("specialDataPointer", "rawX", "rawY", "rawZ", "typeHex", "stateHex", "flag4A", "flag4B", "plausible")) {
        if ($Before[$field] -ne $After[$field]) {
            $fieldChanges += "${field}: $($Before[$field]) -> $($After[$field])"
        }
    }
    return [ordered]@{
        index = $Before.index
        runtimeAddress = $Before.runtimeAddress
        typeBefore = $Before.typeHex
        stateBefore = $Before.stateHex
        typeAfter = $After.typeHex
        stateAfter = $After.stateHex
        before = $Before
        after = $After
        changedByteCount = $changedBytes.Count
        changedBytes = @($changedBytes | Select-Object -First 32)
        fieldChanges = $fieldChanges
        likelyMeaning = $(if ($Before.plausible -and -not $After.plausible) { "record became implausible/inactive" } elseif ($Before.stateHex -ne $After.stateHex) { "state changed" } elseif ($Before.rawX -ne $After.rawX -or $Before.rawY -ne $After.rawY -or $Before.rawZ -ne $After.rawZ) { "position changed" } elseif ($changedBytes.Count -gt 0) { "internal bytes changed" } else { "unchanged" })
    }
}

$beforeBytes = [System.IO.File]::ReadAllBytes($BeforePath)
$afterBytes = [System.IO.File]::ReadAllBytes($AfterPath)
$beforeWindow = Find-PsxRamWindow $beforeBytes
$afterWindow = Find-PsxRamWindow $afterBytes

$script:BeforeRam = [byte[]]$beforeWindow.ram
$script:AfterRam = [byte[]]$afterWindow.ram

$beforeRecords = @(Read-MobyTable $script:BeforeRam ([uint32]$beforeWindow.pointer))
$afterRecords = @(Read-MobyTable $script:AfterRam ([uint32]$afterWindow.pointer))

$afterByIndex = @{}
foreach ($record in $afterRecords) {
    $afterByIndex[[int]$record.index] = $record
}

$changes = @()
foreach ($before in $beforeRecords) {
    if ($afterByIndex.ContainsKey([int]$before.index)) {
        $after = $afterByIndex[[int]$before.index]
    }
    else {
        $after = Read-MobyRecord $script:AfterRam ([int]$before.index) ([int]$before.ramOffset)
    }
    $change = Compare-MobyRecord $before $after
    if ($change.changedByteCount -gt 0 -or $change.fieldChanges.Count -gt 0) {
        $changes += $change
    }
}

$beforeIndexes = @{}
foreach ($record in $beforeRecords) { $beforeIndexes[[int]$record.index] = $true }
$added = @($afterRecords | Where-Object { -not $beforeIndexes.ContainsKey([int]$_.index) })

$typeSummaryBefore = @($beforeRecords | Group-Object { $_.typeHex } | Sort-Object Count -Descending | Select-Object Count, Name)
$typeSummaryAfter = @($afterRecords | Group-Object { $_.typeHex } | Sort-Object Count -Descending | Select-Object Count, Name)
$progressDiff = Compare-ProgressGlobals $script:BeforeRam $script:AfterRam

$result = [ordered]@{
    beforePath = (Resolve-Path -LiteralPath $BeforePath).Path
    afterPath = (Resolve-Path -LiteralPath $AfterPath).Path
    before = [ordered]@{
        size = $beforeBytes.Length
        ramBaseOffset = $beforeWindow.baseOffset
        mobyPointer = ("0x{0:X8}" -f [uint32]$beforeWindow.pointer)
        levelId = ("0x{0:X8}" -f [uint32]$beforeWindow.levelId)
        decodedMobys = $beforeRecords.Count
        typeSummary = $typeSummaryBefore
    }
    after = [ordered]@{
        size = $afterBytes.Length
        ramBaseOffset = $afterWindow.baseOffset
        mobyPointer = ("0x{0:X8}" -f [uint32]$afterWindow.pointer)
        levelId = ("0x{0:X8}" -f [uint32]$afterWindow.levelId)
        decodedMobys = $afterRecords.Count
        typeSummary = $typeSummaryAfter
    }
    changedRecordCount = $changes.Count
    addedRecordCount = $added.Count
    progressDiff = $progressDiff
    changes = @($changes | Sort-Object -Property @{ Expression = { if ($_.fieldChanges.Count -gt 0) { 0 } else { 1 } } }, changedByteCount -Descending)
    addedRecords = $added
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Before decoded mobys: $($beforeRecords.Count), pointer $($result.before.mobyPointer), level $($result.before.levelId)"
Write-Host "After decoded mobys:  $($afterRecords.Count), pointer $($result.after.mobyPointer), level $($result.after.levelId)"
Write-Host "Changed records: $($changes.Count); added records: $($added.Count)"
Write-Host "Progress diff: $($progressDiff.interpretation)"
if ($progressDiff.counterChanges.Count -gt 0) {
    $progressDiff.counterChanges | ForEach-Object {
        Write-Host ("  {0}: {1} -> {2} (delta {3})" -f $_.name, $_.before, $_.after, $_.delta)
    }
}
Write-Host "Collectable flag changes: $($progressDiff.collectableFlagChangeCount)"
Write-Host "Wrote $resolvedOut"
Write-Host ""
Write-Host "Most relevant changes:"
$changes |
    Sort-Object -Property @{ Expression = { if ($_.fieldChanges.Count -gt 0) { 0 } else { 1 } } }, changedByteCount -Descending |
    Select-Object -First 20 |
    ForEach-Object {
        $fields = if ($_.fieldChanges.Count -gt 0) { $_.fieldChanges -join "; " } else { "byte changes only" }
        Write-Host ("#{0} {1} type {2}->{3} state {4}->{5} changedBytes={6} {7}" -f $_.index, $_.runtimeAddress, $_.typeBefore, $_.typeAfter, $_.stateBefore, $_.stateAfter, $_.changedByteCount, $fields)
    }
