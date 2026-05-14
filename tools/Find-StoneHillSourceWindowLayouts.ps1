param(
    [string]$SourceLinksPath = ".\stonehill-moby-source-links.json",
    [string]$OutPath = ".\stonehill-source-window-layouts.json",
    [switch]$IncludeHelpers
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function Get-ArrayField($Object, [string]$Name) {
    if ($null -eq $Object) { return @() }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return @() }
    return @($property.Value)
}

function Convert-HexToInt([string]$Hex) {
    $clean = $Hex.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        $clean = $clean.Substring(2)
    }
    return [Convert]::ToInt32($clean, 16)
}

function Parse-WindowValue([string]$ValueText) {
    if ($ValueText -notmatch '^([xyz])=(-?\d+)@\+(\d+)$') { return $null }
    return [pscustomobject]@{
        axis = $matches[1]
        value = [int]$matches[2]
        offset = [int]$matches[3]
    }
}

function Get-KindBucket([string]$Kind) {
    switch -Wildcard ($Kind) {
        "*gem*" { return "gem" }
        "*dragon*" { return "dragonNpc" }
        "*enemy*" { return "enemyActive" }
        "*helper*" { return "helper" }
        "*sparse*" { return "sparse" }
        default { return "unknown" }
    }
}

function New-LeadRecord($Record, $Window, [int]$WindowRank) {
    $values = @(Get-ArrayField $Window "values" | ForEach-Object { Parse-WindowValue ([string]$_) } | Where-Object { $null -ne $_ })
    $unique = @{}
    foreach ($value in $values) {
        $key = "$($value.axis):$($value.offset):$($value.value)"
        if (-not $unique.ContainsKey($key)) { $unique[$key] = $value }
    }
    $values = @($unique.Values)
    if ($values.Count -lt 2) { return $null }

    return [pscustomobject]@{
        mobyIndex = [int]$Record.index
        typeHex = [string]$Record.typeHex
        stateHex = [string]$Record.stateHex
        kind = [string]$Record.candidateKind
        kindBucket = Get-KindBucket ([string]$Record.candidateKind)
        windowRank = $WindowRank
        assetRelativeWindow = [string]$Window.assetRelativeWindow
        wadRelativeWindow = [string]$Window.wadRelativeWindow
        assetWindowInt = Convert-HexToInt ([string]$Window.assetRelativeWindow)
        assetSubfileIndex = [int]$Window.assetSubfileIndex
        axisCount = [int]$Window.axisCount
        spread = [int]$Window.spread
        values = $values
        typeByteOffsets = @(Get-ArrayField $Window "typeByteOffsets")
        stateByteOffsets = @(Get-ArrayField $Window "stateByteOffsets")
    }
}

function Add-GroupItem($Map, [string]$Key, $Item) {
    if (-not $Map.ContainsKey($Key)) {
        $Map[$Key] = New-Object System.Collections.Generic.List[object]
    }
    [void]$Map[$Key].Add($Item)
}

function New-PairOccurrence($Lead, $A, $B, [Nullable[int]]$Stride, [Nullable[int]]$FieldA, [Nullable[int]]$FieldB) {
    return [pscustomobject]@{
        mobyIndex = [int]$Lead.mobyIndex
        typeHex = [string]$Lead.typeHex
        stateHex = [string]$Lead.stateHex
        kind = [string]$Lead.kind
        kindBucket = [string]$Lead.kindBucket
        windowRank = [int]$Lead.windowRank
        assetRelativeWindow = [string]$Lead.assetRelativeWindow
        wadRelativeWindow = [string]$Lead.wadRelativeWindow
        assetWindowInt = [int]$Lead.assetWindowInt
        assetSubfileIndex = [int]$Lead.assetSubfileIndex
        axisCount = [int]$Lead.axisCount
        spread = [int]$Lead.spread
        axisA = [string]$A.axis
        valueA = [int]$A.value
        offsetA = [int]$A.offset
        axisB = [string]$B.axis
        valueB = [int]$B.value
        offsetB = [int]$B.offset
        stride = $Stride
        fieldA = $FieldA
        fieldB = $FieldB
        typeByteOffsets = @(Get-ArrayField $Lead "typeByteOffsets")
        stateByteOffsets = @(Get-ArrayField $Lead "stateByteOffsets")
    }
}

function Summarize-Group([string]$Key, $Items) {
    $array = @()
    foreach ($item in $Items) {
        $array += $item
    }
    $indexes = @($array | Select-Object -ExpandProperty mobyIndex -Unique | Sort-Object)
    $types = @($array | Select-Object -ExpandProperty typeHex -Unique | Sort-Object)
    $kindBuckets = @($array | Select-Object -ExpandProperty kindBucket -Unique | Sort-Object)
    $subfiles = @($array | Select-Object -ExpandProperty assetSubfileIndex -Unique | Sort-Object)
    $typed = @($array | Where-Object { @(Get-ArrayField $_ "typeByteOffsets").Count -gt 0 }).Count
    $stateful = @($array | Where-Object { @(Get-ArrayField $_ "stateByteOffsets").Count -gt 0 }).Count
    $bestExamples = @($array |
        Sort-Object -Property @{ Expression = { [int]$_.windowRank }; Descending = $false }, @{ Expression = { [int]$_.spread }; Descending = $false } |
        Select-Object -First 8 |
        ForEach-Object {
            [ordered]@{
                index = [int]$_.mobyIndex
                typeHex = [string]$_.typeHex
                kindBucket = [string]$_.kindBucket
                assetRelativeWindow = [string]$_.assetRelativeWindow
                wadRelativeWindow = [string]$_.wadRelativeWindow
                assetSubfileIndex = [int]$_.assetSubfileIndex
                spread = [int]$_.spread
                pair = "$($_.axisA)=$($_.valueA)@+$($_.offsetA), $($_.axisB)=$($_.valueB)@+$($_.offsetB)"
                stride = $_.stride
                fields = if ($null -ne $_.stride) { "$($_.axisA)@$($_.fieldA), $($_.axisB)@$($_.fieldB)" } else { $null }
                typeByteOffsets = @(Get-ArrayField $_ "typeByteOffsets")
                stateByteOffsets = @(Get-ArrayField $_ "stateByteOffsets")
            }
        })

    $score = ($indexes.Count * 10) + ($kindBuckets.Count * 4) + ($types.Count * 2) + $typed + $stateful
    if ($kindBuckets -contains "gem" -and ($kindBuckets -contains "enemyActive" -or $kindBuckets -contains "dragonNpc")) {
        $score += 8
    }

    return [pscustomobject][ordered]@{
        key = $Key
        score = $score
        leadCount = $array.Count
        distinctMobys = $indexes.Count
        mobyIndexes = $indexes
        types = $types
        kindBuckets = $kindBuckets
        subfiles = $subfiles
        windowsWithTypeByte = $typed
        windowsWithStateByte = $stateful
        examples = $bestExamples
    }
}

function Get-LeadAxisSet($Lead) {
    return -join (@($Lead.values | Select-Object -ExpandProperty axis -Unique | Sort-Object))
}

function Get-LeadTypeByteCount($Lead) {
    return @(Get-ArrayField $Lead "typeByteOffsets").Count
}

function Get-LeadStateByteCount($Lead) {
    return @(Get-ArrayField $Lead "stateByteOffsets").Count
}

function Get-ActionableLeadScore($Lead) {
    $axisSet = Get-LeadAxisSet $Lead
    $score = ([int]$Lead.axisCount * 20)
    if ($axisSet -eq "xz") { $score += 24 }
    elseif ($axisSet -match "x" -and $axisSet -match "z") { $score += 16 }
    if ([string]$Lead.kindBucket -in @("gem", "enemyActive", "dragonNpc")) { $score += 20 }
    if ([string]$Lead.kindBucket -eq "unknown") { $score -= 12 }
    $score += [Math]::Min(12, (Get-LeadTypeByteCount $Lead) * 4)
    $score += [Math]::Min(8, (Get-LeadStateByteCount $Lead) * 2)
    $score += [Math]::Max(0, 18 - [Math]::Min(18, [int]$Lead.spread))
    $score += [Math]::Max(0, 8 - [int]$Lead.windowRank)
    return $score
}

function ConvertTo-ActionableLeadSummary($Lead) {
    $values = @($Lead.values |
        Sort-Object offset, axis |
        ForEach-Object { "$($_.axis)=$($_.value)@+$($_.offset)" })

    return [ordered]@{
        score = Get-ActionableLeadScore $Lead
        index = [int]$Lead.mobyIndex
        typeHex = [string]$Lead.typeHex
        stateHex = [string]$Lead.stateHex
        kindBucket = [string]$Lead.kindBucket
        assetRelativeWindow = [string]$Lead.assetRelativeWindow
        wadRelativeWindow = [string]$Lead.wadRelativeWindow
        assetSubfileIndex = [int]$Lead.assetSubfileIndex
        axisSet = Get-LeadAxisSet $Lead
        axisCount = [int]$Lead.axisCount
        spread = [int]$Lead.spread
        values = $values
        typeByteOffsets = @(Get-ArrayField $Lead "typeByteOffsets")
        stateByteOffsets = @(Get-ArrayField $Lead "stateByteOffsets")
    }
}

function Test-ActionablePattern($Pattern) {
    $buckets = @(Get-ArrayField $Pattern "kindBuckets")
    if (-not ($buckets | Where-Object { $_ -in @("gem", "enemyActive", "dragonNpc") })) { return $false }
    if ([int]$Pattern.distinctMobys -lt 2) { return $false }
    if ([string]$Pattern.key -notmatch 'x' -or [string]$Pattern.key -notmatch 'z') { return $false }
    return $true
}

$sourceLinks = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $SourceLinksPath).Path | ConvertFrom-Json

$leads = @()
foreach ($record in @(Get-ArrayField $sourceLinks "records")) {
    $kind = [string]$record.candidateKind
    if (-not $IncludeHelpers -and ($kind -like "*helper*" -or $kind -like "*sparse*")) { continue }
    $windows = @(Get-ArrayField $record "scaledAxisWindows")
    for ($i = 0; $i -lt $windows.Count; $i++) {
        $lead = New-LeadRecord $record $windows[$i] $i
        if ($null -ne $lead) { $leads += $lead }
    }
}

$pairGroups = @{}
$strideGroups = @{}
$strideSubfileGroups = @{}
$strideValues = @(8, 12, 16, 20, 24, 28, 32, 36, 40, 48, 56, 64)

foreach ($lead in $leads) {
    $values = @($lead.values | Sort-Object offset, axis)
    for ($i = 0; $i -lt $values.Count; $i++) {
        for ($j = $i + 1; $j -lt $values.Count; $j++) {
            $a = $values[$i]
            $b = $values[$j]
            if ($a.axis -eq $b.axis) { continue }
            $low = $a
            $high = $b
            if ($high.offset -lt $low.offset) {
                $low = $b
                $high = $a
            }
            $delta = [int]($high.offset - $low.offset)
            if ($delta -gt 40) { continue }
            $axisSet = -join (@($a.axis, $b.axis) | Sort-Object)
            $orderedAxes = "$($low.axis)$($high.axis)"
            $pairKey = "axes=$axisSet ordered=$orderedAxes delta=$delta"
            Add-GroupItem $pairGroups $pairKey (New-PairOccurrence $lead $low $high $null $null $null)

            foreach ($stride in $strideValues) {
                $fieldA = ($lead.assetWindowInt + [int]$a.offset) % $stride
                $fieldB = ($lead.assetWindowInt + [int]$b.offset) % $stride
                $parts = @(
                    "$($a.axis)@$fieldA",
                    "$($b.axis)@$fieldB"
                ) | Sort-Object
                $strideKey = "stride=$stride " + ($parts -join " ")
                Add-GroupItem $strideGroups $strideKey (New-PairOccurrence $lead $a $b $stride $fieldA $fieldB)
                $subfileKey = "sub=$($lead.assetSubfileIndex) $strideKey"
                Add-GroupItem $strideSubfileGroups $subfileKey (New-PairOccurrence $lead $a $b $stride $fieldA $fieldB)
            }
        }
    }
}

$pairSummaries = @($pairGroups.GetEnumerator() | ForEach-Object { Summarize-Group $_.Key $_.Value } | Sort-Object -Property score, distinctMobys, leadCount -Descending | Select-Object -First 40)
$strideSummaries = @($strideGroups.GetEnumerator() | ForEach-Object { Summarize-Group $_.Key $_.Value } | Sort-Object -Property score, distinctMobys, leadCount -Descending | Select-Object -First 40)
$strideSubfileSummaries = @($strideSubfileGroups.GetEnumerator() | ForEach-Object { Summarize-Group $_.Key $_.Value } | Sort-Object -Property score, distinctMobys, leadCount -Descending | Select-Object -First 40)
$actionableLeads = @($leads |
    Where-Object { [string]$_.kindBucket -in @("gem", "enemyActive", "dragonNpc") } |
    ForEach-Object { ConvertTo-ActionableLeadSummary $_ } |
    Sort-Object -Property @{ Expression = { [int]$_.score }; Descending = $true }, @{ Expression = { [int]$_.spread }; Descending = $false } |
    Select-Object -First 40)
$actionablePairSummaries = @($pairGroups.GetEnumerator() |
    ForEach-Object { Summarize-Group $_.Key $_.Value } |
    Where-Object { Test-ActionablePattern $_ } |
    Sort-Object -Property score, distinctMobys, leadCount -Descending |
    Select-Object -First 20)
$actionableStrideSummaries = @($strideGroups.GetEnumerator() |
    ForEach-Object { Summarize-Group $_.Key $_.Value } |
    Where-Object { Test-ActionablePattern $_ } |
    Sort-Object -Property score, distinctMobys, leadCount -Descending |
    Select-Object -First 20)

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceLinksPath = (Resolve-Path -LiteralPath $SourceLinksPath).Path
    includeHelpers = [bool]$IncludeHelpers
    note = "Mines repeated axis-pair and modulo-stride patterns from scaled-int16 source windows. High-scoring groups are layout leads only; they still need byte-level validation and before/after samples."
    leadSummary = [ordered]@{
        windows = $leads.Count
        mobys = @($leads | Select-Object -ExpandProperty mobyIndex -Unique).Count
        types = @($leads | Select-Object -ExpandProperty typeHex -Unique | Sort-Object)
        kindBuckets = @($leads | Select-Object -ExpandProperty kindBucket -Unique | Sort-Object)
    }
    topActionableSourceLeads = $actionableLeads
    topActionablePairPatterns = $actionablePairSummaries
    topActionableStrideFieldPatterns = $actionableStrideSummaries
    topPairPatterns = $pairSummaries
    topStrideFieldPatterns = $strideSummaries
    topSubfileStrideFieldPatterns = $strideSubfileSummaries
    interpretation = @(
        "A convincing object-source layout should recur across multiple mobys, have consistent stride/field offsets, and preferably include the moby type/state bytes near the same record.",
        "Patterns dominated by zero-heavy unknown/helper records are weak, especially when they do not include gem/enemy/dragon candidates.",
        "Patterns that mix gem, enemy, and dragon/NPC candidates are useful next leads, but a shared pair spacing alone can still be terrain or model coordinate data.",
        "The actionable sections are triage views for the next byte-patch tests; they intentionally rank real placement candidates above zero-position helper records."
    )
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote Stone Hill source-window layout report to $resolvedOut"
Write-Host "Windows mined: $($leads.Count); mobys: $($result.leadSummary.mobys); pair groups: $($pairGroups.Count); stride groups: $($strideGroups.Count)"
