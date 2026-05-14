param(
    [string]$InventoryPath = ".\stonehill-full-moby-inventory.json",
    [string]$OutJsonPath = ".\stonehill-moby-identity-audit.json",
    [string]$OutMarkdownPath = ".\stonehill-moby-identity-audit.md"
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

function Convert-LegacyIndexToTrue([int]$LegacyIndex) {
    $trueNumerator = ($LegacyIndex * 0x50) - 8
    if ($trueNumerator -lt 0 -or ($trueNumerator % 0x58) -ne 0) { return -1 }
    return [int]($trueNumerator / 0x58)
}

function Resolve-MetadataIndex($Entry, [bool]$IndexIsLegacyByDefault) {
    $trueIndex = [int](Get-Field $Entry "trueIndex" -1)
    if ($trueIndex -ge 0) { return $trueIndex }
    $legacyIndex = [int](Get-Field $Entry "legacyIndex" -1)
    if ($legacyIndex -ge 0) { return Convert-LegacyIndexToTrue $legacyIndex }
    $index = [int](Get-Field $Entry "index" -1)
    if ($index -lt 0) { return -1 }
    if ($IndexIsLegacyByDefault) { return Convert-LegacyIndexToTrue $index }
    return $index
}

function Merge-MetadataFile([hashtable]$Map, [string]$Path, [string]$ArrayName, [bool]$IndexIsLegacyByDefault) {
    $resolved = Resolve-WorkspacePath $Path
    if (-not (Test-Path -LiteralPath $resolved)) { return }
    $root = Get-Content -Raw -LiteralPath $resolved | ConvertFrom-Json
    foreach ($entry in @(Get-ArrayField $root $ArrayName)) {
        $index = Resolve-MetadataIndex $entry $IndexIsLegacyByDefault
        if ($index -lt 0) { continue }
        if (-not $Map.ContainsKey($index)) { $Map[$index] = [ordered]@{} }
        $target = $Map[$index]
        foreach ($pair in @(
            @("label", @("displayTargetLabel", "displayLabel", "label")),
            @("zone", @("zoneLabel", "zone")),
            @("kind", @("candidateKind", "kind")),
            @("confidence", @("confidence")),
            @("evidence", @("evidence", "note")),
            @("behaviorNote", @("behaviorNote")),
            @("specialDataNote", @("specialDataNote"))
        )) {
            $outName = [string]$pair[0]
            foreach ($sourceName in @($pair[1])) {
                $value = [string](Get-Field $entry $sourceName "")
                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    $target[$outName] = $value
                    break
                }
            }
        }
    }
}

function Get-IdentityStatus($Record) {
    $text = (([string]$Record.label) + " " + ([string]$Record.kind) + " " + ([string]$Record.confidence) + " " + ([string]$Record.evidence)).ToLowerInvariant()
    if ($text -match "live-confirmed|signature-confirmed|confirmed in stone hill|editor trial|live-tested|confirmed red|confirmed green|confirmed blue|confirmed yellow|confirmed purple") {
        return "confirmed"
    }
    if ($text -match "provisional") { return "questionable" }
    if ($text -match "live-observed|wide-row-observed|raised-row-observed|isolate-observed|safe-ground-observed") { return "observed" }
    if ($text -match "live-inferred|cluster-inferred|candidate|\?|needs identity|needs visual|unknown|placeholder|control stack") {
        return "questionable"
    }
    if ($text -match "container-or-reward|active-or-small-pickup|simple-collectible-or-scenery|linked-collectible-or-actor|nonvisual-control-or-placeholder") {
        return "unresolved"
    }
    return "seeded"
}

function Get-TestSuggestion($Record) {
    $type = [string]$Record.typeHex
    $text = (([string]$Record.label) + " " + ([string]$Record.kind)).ToLowerInvariant()
    if ($text -match "dragon|pedestal|fairy") { return "Move linked cluster together, then capture rescue/collection behavior." }
    if ($text -match "enemy|ram|shepherd") { return "Retest in a wide single row and map each slot to ram, shepherd, or invisible/inactive." }
    if ($text -match "chest|container|reward") { return "Move far in Loader BIN, break/open it, and confirm collision plus reward." }
    if ($text -match "gem|key|collectible") { return "Move to a clear test spot and confirm pickup/counter or key visibility." }
    if ($type -eq "0x00") { return "Treat as helper/control; test only with nearby visible cluster." }
    if ($type -eq "0x30") { return "Move a large distance to confirm scenery/terrain prop identity." }
    if ($type -eq "0x20") { return "Move far in Loader BIN and observe whether it is scenery, actor, chest, or helper." }
    return "Needs focused visual move or before/after RAM pair."
}

$resolvedInventoryPath = Resolve-WorkspacePath $InventoryPath
$resolvedOutJsonPath = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath
if (-not (Test-Path -LiteralPath $resolvedInventoryPath)) { throw "Missing inventory: $resolvedInventoryPath" }

$metadata = @{}
Merge-MetadataFile $metadata ".\stonehill-full-moby-inventory.json" "mobys" $false
Merge-MetadataFile $metadata ".\stonehill-moby-catalog.json" "mobys" $true
Merge-MetadataFile $metadata ".\stonehill-replica-map-plan.json" "mobyMarkers" $true
Merge-MetadataFile $metadata ".\stonehill-moby-special-data.json" "mobys" $false
Merge-MetadataFile $metadata ".\stonehill-gem-value-overrides.json" "mobys" $false
Merge-MetadataFile $metadata ".\stonehill-live-validation-overrides.json" "mobys" $false

$inventory = Get-Content -Raw -LiteralPath $resolvedInventoryPath | ConvertFrom-Json
$records = New-Object System.Collections.ArrayList
foreach ($moby in @(Get-ArrayField $inventory "mobys")) {
    $trueIndex = [int](Get-Field $moby "trueIndex" (Get-Field $moby "index" -1))
    $meta = if ($metadata.ContainsKey($trueIndex)) { $metadata[$trueIndex] } else { [ordered]@{} }
    $label = if ($meta.Contains("label")) { [string]$meta.label } else { [string](Get-Field $moby "displayTargetLabel" (Get-Field $moby "displayLabel" "unlabeled")) }
    $kind = if ($meta.Contains("kind")) { [string]$meta.kind } else { [string](Get-Field $moby "candidateKind" "") }
    $zone = if ($meta.Contains("zone")) { [string]$meta.zone } else { "" }
    $confidence = if ($meta.Contains("confidence")) { [string]$meta.confidence } else { [string](Get-Field $moby "confidence" "") }
    $evidence = if ($meta.Contains("evidence")) { [string]$meta.evidence } else { [string](Get-Field $moby "evidence" "") }
    $record = [ordered]@{
        id = "T$trueIndex"
        trueIndex = $trueIndex
        legacyAlias = [string](Get-Field $moby "legacyAlias" "")
        label = $label
        typeHex = [string](Get-Field $moby "typeHex" "")
        stateHex = [string](Get-Field $moby "stateHex" "")
        x = [double](Get-Field $moby "x" 0)
        y = [double](Get-Field $moby "y" 0)
        z = [double](Get-Field $moby "z" 0)
        specialDataPointer = [string](Get-Field $moby "specialDataPointer" "")
        flag52Hex = [string](Get-Field $moby "flag52Hex" "")
        flag53Hex = [string](Get-Field $moby "flag53Hex" "")
        kind = $kind
        zone = $zone
        confidence = $confidence
        evidence = $evidence
    }
    $record.identityStatus = Get-IdentityStatus $record
    $record.testSuggestion = Get-TestSuggestion $record
    [void]$records.Add($record)
}

$statusCounts = @{}
foreach ($record in $records) {
    if (-not $statusCounts.ContainsKey($record.identityStatus)) { $statusCounts[$record.identityStatus] = 0 }
    $statusCounts[$record.identityStatus] = [int]$statusCounts[$record.identityStatus] + 1
}

$questionable = @($records | Where-Object { $_["identityStatus"] -in @("questionable", "unresolved") } | Sort-Object { [int]$_["trueIndex"] })
$root = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    inventoryPath = (Resolve-Path -LiteralPath $resolvedInventoryPath).Path
    totalRecords = $records.Count
    statusCounts = $statusCounts
    questionableCount = $questionable.Count
    records = @($records.ToArray())
    questionableRecords = $questionable
}
$root | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutJsonPath -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Stone Hill Moby Identity Audit")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($root.generatedAt)")
[void]$lines.Add("")
[void]$lines.Add("This report merges the full Stone Hill loader-table inventory with catalog, gem-value, special-data, and live-validation override metadata. It is meant to show which T-records still need visual or behavior confirmation.")
[void]$lines.Add("")
[void]$lines.Add("## Status Counts")
[void]$lines.Add("")
[void]$lines.Add("| Status | Count |")
[void]$lines.Add("|---|---:|")
foreach ($key in @("confirmed", "observed", "questionable", "unresolved", "seeded")) {
    $count = if ($statusCounts.ContainsKey($key)) { $statusCounts[$key] } else { 0 }
    [void]$lines.Add("| $key | $count |")
}
[void]$lines.Add("")
[void]$lines.Add("## Questionable / Unresolved Records")
[void]$lines.Add("")
[void]$lines.Add("| ID | Type | Label | Confidence | Why / Next Test |")
[void]$lines.Add("|---|---|---|---|---|")
foreach ($record in $questionable) {
    $label = ([string]$record.label).Replace("|", "\|")
    $confidence = ([string]$record.confidence).Replace("|", "\|")
    $suggestion = ([string]$record.testSuggestion).Replace("|", "\|")
    [void]$lines.Add("| $($record.id) | $($record.typeHex) | $label | $confidence | $suggestion |")
}
[void]$lines.Add("")
[void]$lines.Add("## Confirmed / Observed Highlights")
[void]$lines.Add("")
[void]$lines.Add("| ID | Type | Label | Status | Evidence |")
[void]$lines.Add("|---|---|---|---|---|")
foreach ($record in @($records | Where-Object { $_["identityStatus"] -in @("confirmed", "observed") } | Sort-Object { [int]$_["trueIndex"] })) {
    $label = ([string]$record.label).Replace("|", "\|")
    $evidence = ([string]$record.evidence).Replace("|", "\|")
    if ($evidence.Length -gt 140) { $evidence = $evidence.Substring(0, 137) + "..." }
    [void]$lines.Add("| $($record.id) | $($record.typeHex) | $label | $($record.identityStatus) | $evidence |")
}
$lines | Set-Content -LiteralPath $resolvedOutMarkdownPath -Encoding UTF8

Write-Host "Wrote $resolvedOutJsonPath"
Write-Host "Wrote $resolvedOutMarkdownPath"
Write-Host "Questionable/unresolved records: $($questionable.Count) / $($records.Count)"
