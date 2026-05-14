param(
    [string]$LoaderAnalysisPath = ".\stonehill-loader-table-analysis.json",
    [string]$OutJsonPath = ".\stonehill-full-moby-inventory.json",
    [string]$OutMarkdownPath = ".\stonehill-full-moby-inventory.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

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

function Get-LegacyAlias([int]$TrueIndex) {
    $n = ($TrueIndex * 0x58) + 8
    if ($n -ge 0 -and ($n % 0x50) -eq 0) { return [int]($n / 0x50) }
    return $null
}

function Get-SourceOffset([int]$TrueIndex, [int]$FieldOffset) {
    $base = [Convert]::ToInt64("D72B38", 16)
    return "0x{0:X}" -f ($base + ([int64]$TrueIndex * 0x58) + [int64]$FieldOffset)
}

function Get-IdentitySeed($Record) {
    $type = [string](Get-Field $Record "typeHex" "")
    $state = [string](Get-Field $Record "stateHex" "")
    $special = [string](Get-Field $Record "specialDataPointer" "")
    $flag52 = [string](Get-Field $Record "flag52Hex" "")
    $flag53 = [string](Get-Field $Record "flag53Hex" "")

    if ($type -eq "0x18") {
        return [ordered]@{
            family = "active-or-small-pickup"
            confidence = "needs identity sample"
            note = "True 0x18 records include red-gem-like and actor-like placements; use focused before/after RAM or visual selection to name them."
        }
    }
    if ($type -eq "0x20" -and $flag53 -eq "0x54") {
        return [ordered]@{
            family = "container-or-reward-object"
            confidence = "strong candidate"
            note = "Type 0x20 with flag53 0x54 matched tested chest behavior once patched through the loader table."
        }
    }
    if ($type -eq "0x20" -and $special -ne "0x00000000") {
        return [ordered]@{
            family = "linked-collectible-or-actor"
            confidence = "medium"
            note = "Has linked special data; movement through the loader table should preserve collision/reward behavior, but identity still needs naming."
        }
    }
    if ($type -eq "0x20") {
        return [ordered]@{
            family = "simple-collectible-or-scenery"
            confidence = "needs identity sample"
            note = "Common Stone Hill object family; previous sparse aliases included gems, fodder/scenery, and lamps."
        }
    }
    if ($type -eq "0x30") {
        return [ordered]@{
            family = "large-scenery-or-npc"
            confidence = "needs identity sample"
            note = "Previous sparse aliases in this family included tree/scenery; some records may be dragons/NPCs."
        }
    }
    if ($type -eq "0x10" -or $type -eq "0x40") {
        return [ordered]@{
            family = "system-or-trigger"
            confidence = "weak"
            note = "Rare placement-like record; edit only after visual confirmation."
        }
    }
    if ($type -eq "0x00") {
        return [ordered]@{
            family = "nonvisual-control-or-placeholder"
            confidence = "weak"
            note = "Type 0x00 records can contain source-loaded coordinates, but may not render as visible mobys. Treat as markers, triggers, or linked control records until paired with a visible object."
        }
    }
    return [ordered]@{
        family = "unknown"
        confidence = "unknown"
        note = "Needs visual/editor confirmation."
    }
}

function Get-KnownLabel($TrueIndex, $LegacyAlias, $NativeMappings) {
    foreach ($mapping in @($NativeMappings)) {
        $mappedTrue = Get-Field $mapping "trueIndex" $null
        if ($null -ne $mappedTrue -and [int]$mappedTrue -eq $TrueIndex) {
            return [string](Get-Field $mapping "label" "")
        }
        if ($null -ne $LegacyAlias) {
            $legacy = Get-Field $mapping "legacyIndex" $null
            if ($null -ne $legacy -and [int]$legacy -eq [int]$LegacyAlias) {
                return [string](Get-Field $mapping "label" "")
            }
        }
    }
    return ""
}

$loaderPath = Resolve-WorkspacePath $LoaderAnalysisPath
if (-not (Test-Path -LiteralPath $loaderPath)) { throw "Missing loader analysis: $loaderPath" }
$analysis = Get-Content -Raw -LiteralPath $loaderPath | ConvertFrom-Json
$nativeMappings = @(Get-ArrayField $analysis "nativeEditMappings")

$inventory = New-Object System.Collections.ArrayList
foreach ($record in @(Get-ArrayField $analysis "records")) {
    $trueIndex = [int](Get-Field $record "trueIndex" -1)
    if ($trueIndex -lt 0) { continue }
    $legacyAlias = Get-LegacyAlias $trueIndex
    $seed = Get-IdentitySeed $record
    $knownLabel = Get-KnownLabel $trueIndex $legacyAlias $nativeMappings
    $displayLabel = if (-not [string]::IsNullOrWhiteSpace($knownLabel)) { $knownLabel } else { [string]$seed.family }

    [void]$inventory.Add([ordered]@{
        index = $trueIndex
        trueIndex = $trueIndex
        id = "T$trueIndex"
        legacyAlias = $(if ($null -ne $legacyAlias) { "L$legacyAlias" } else { "" })
        displayLabel = $displayLabel
        displayTargetLabel = $displayLabel
        identityFamily = [string]$seed.family
        candidateKind = [string]$seed.family
        confidence = [string]$seed.confidence
        note = [string]$seed.note
        evidence = [string]$seed.note
        typeHex = [string](Get-Field $record "typeHex" "")
        stateHex = [string](Get-Field $record "stateHex" "")
        x = [double](Get-Field $record "x" 0)
        y = [double](Get-Field $record "y" 0)
        z = [double](Get-Field $record "z" 0)
        rawX = [int](Get-Field $record "rawX" 0)
        rawY = [int](Get-Field $record "rawY" 0)
        rawZ = [int](Get-Field $record "rawZ" 0)
        runtimeAddress = [string](Get-Field $record "runtimeAddress" "")
        specialDataPointer = [string](Get-Field $record "specialDataPointer" "")
        flag52Hex = [string](Get-Field $record "flag52Hex" "")
        flag53Hex = [string](Get-Field $record "flag53Hex" "")
        sourceRecordWadOffset = Get-SourceOffset $trueIndex 0
        sourceXWadOffset = Get-SourceOffset $trueIndex 0x0C
        sourceYWadOffset = Get-SourceOffset $trueIndex 0x10
        sourceZWadOffset = Get-SourceOffset $trueIndex 0x14
        loaderPatchable = $true
    })
}

$typeSummary = @($inventory |
    Group-Object { Get-Field $_ "typeHex" "" } |
    Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, Name |
    ForEach-Object {
        [ordered]@{
            typeHex = $_.Name
            count = $_.Count
            families = @($_.Group | ForEach-Object { [string](Get-Field $_ "identityFamily" "") } | Where-Object { $_ } | Select-Object -Unique | Sort-Object)
            sampleIds = @($_.Group | Select-Object -First 12 | ForEach-Object { [string](Get-Field $_ "id" "") })
        }
    })

$familySummary = @($inventory |
    Group-Object { Get-Field $_ "identityFamily" "" } |
    Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, Name |
    ForEach-Object {
        [ordered]@{
            family = $_.Name
            count = $_.Count
            types = @($_.Group | ForEach-Object { [string](Get-Field $_ "typeHex" "") } | Where-Object { $_ } | Select-Object -Unique | Sort-Object)
            sampleIds = @($_.Group | Select-Object -First 12 | ForEach-Object { [string](Get-Field $_ "id" "") })
        }
    })

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    source = [ordered]@{
        loaderAnalysisPath = (Resolve-Path -LiteralPath $loaderPath).Path
        sourceTable = "WAD entry 12 at 0xD72B38"
        stride = "0x58"
        coordinateOffsets = "+0x0C/+0x10/+0x14"
        note = "This inventory is source-patch-oriented. Identities are seeds until each record is visually or behaviorally confirmed."
    }
    counts = [ordered]@{
        trueRecords = $inventory.Count
        legacyAliases = @($inventory | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.legacyAlias) }).Count
        loaderPatchable = @($inventory | Where-Object { $_.loaderPatchable }).Count
    }
    typeSummary = $typeSummary
    familySummary = $familySummary
    mobys = @($inventory.ToArray())
}

$outJson = Resolve-WorkspacePath $OutJsonPath
$outMarkdown = Resolve-WorkspacePath $OutMarkdownPath
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outJson -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Stone Hill Full Moby Inventory")
[void]$lines.Add("")
[void]$lines.Add("- Generated: $($result.generatedAt)")
[void]$lines.Add("- Source table: $($result.source.sourceTable), stride $($result.source.stride)")
[void]$lines.Add("- Records: $($result.counts.trueRecords), legacy aliases: $($result.counts.legacyAliases), loader patchable: $($result.counts.loaderPatchable)")
[void]$lines.Add("- Note: $($result.source.note)")
[void]$lines.Add("")
[void]$lines.Add("## Family Summary")
[void]$lines.Add("")
[void]$lines.Add("| family | count | types | samples |")
[void]$lines.Add("|---|---:|---|---|")
foreach ($row in $familySummary) {
    [void]$lines.Add("| $(Escape-Markdown $row.family) | $($row.count) | $(@($row.types) -join ', ') | $(@($row.sampleIds) -join ', ') |")
}
[void]$lines.Add("")
[void]$lines.Add("## Type Summary")
[void]$lines.Add("")
[void]$lines.Add("| type | count | families | samples |")
[void]$lines.Add("|---:|---:|---|---|")
foreach ($row in $typeSummary) {
    [void]$lines.Add("| $($row.typeHex) | $($row.count) | $(Escape-Markdown (@($row.families) -join ', ')) | $(@($row.sampleIds) -join ', ') |")
}
[void]$lines.Add("")
[void]$lines.Add("## Confirmed / Named Aliases")
[void]$lines.Add("")
[void]$lines.Add("| id | alias | label | type | state | XYZ | source XYZ offsets |")
[void]$lines.Add("|---:|---:|---|---:|---:|---|---|")
foreach ($moby in @($inventory | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.legacyAlias) } | Sort-Object trueIndex)) {
    $xyz = "{0}, {1}, {2}" -f $moby.x, $moby.y, $moby.z
    $offsets = "$($moby.sourceXWadOffset), $($moby.sourceYWadOffset), $($moby.sourceZWadOffset)"
    [void]$lines.Add("| $($moby.id) | $($moby.legacyAlias) | $(Escape-Markdown $moby.displayLabel) | $($moby.typeHex) | $($moby.stateHex) | $xyz | $offsets |")
}
[void]$lines.Add("")
[void]$lines.Add("## Coverage Plan")
[void]$lines.Add("")
[void]$lines.Add("1. Use the full-table editor to click through T0..T196 and name records visually.")
[void]$lines.Add("2. Use one focused RAM before/after pair per uncertain family: dragon/NPCs, enemies, fodder, key/thief, and chests.")
[void]$lines.Add("3. Promote confirmed names back into this inventory, then use the same table scanner on other levels.")

$lines | Set-Content -LiteralPath $outMarkdown -Encoding UTF8

Write-Host "Wrote full Stone Hill moby inventory to $outJson"
Write-Host "Wrote full Stone Hill moby report to $outMarkdown"
Write-Host ("Records: {0}; legacy aliases: {1}" -f $result.counts.trueRecords, $result.counts.legacyAliases)
