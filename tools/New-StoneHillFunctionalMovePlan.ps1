param(
    [string]$CatalogPath = ".\stonehill-moby-catalog.json",
    [string]$LiveOverridesPath = ".\stonehill-live-validation-overrides.json",
    [string]$SpecialDataPath = ".\stonehill-moby-special-data.json",
    [string]$PatchMatrixPath = ".\stonehill-patch-test-matrix.json",
    [string]$SourceValidationPath = ".\stonehill-source-patch-live-validation-current.json",
    [string]$BehaviorDiffPath = ".\stonehill-moby-behavior-diff-current.json",
    [string]$OutJsonPath = ".\stonehill-functional-move-plan.json",
    [string]$OutMarkdownPath = ".\stonehill-functional-move-plan.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Read-JsonIfPresent([string]$Path) {
    $resolved = Resolve-WorkspacePath $Path
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) { return $null }
    return Get-Content -Raw -LiteralPath $resolved | ConvertFrom-Json
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

function Convert-HexTextToInt64([string]$Text, [int64]$Default = -1) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Default }
    $clean = $Text.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
    try { return [Convert]::ToInt64($clean, 16) }
    catch { return $Default }
}

function Format-HexOffset([int64]$Value) {
    if ($Value -lt 0) { return "" }
    return ("0x{0:X}" -f $Value)
}

function Test-PsxPointer([int64]$Value) {
    return ($Value -ge 0x80000000L -and $Value -lt 0x80200000L -and (($Value -band 3L) -eq 0L))
}

function Get-FirstSourceWadOffset($SpecialRecord) {
    foreach ($match in @(Get-ArrayField $SpecialRecord "sourceMatches")) {
        if ([int](Get-Field $match "count" 0) -le 0) { continue }
        $offsets = @(Get-ArrayField $match "wadRelativeOffsets")
        if ($offsets.Count -gt 0) { return Convert-HexTextToInt64 ([string]$offsets[0]) -1 }
    }
    return -1
}

function Get-SourceMatchText($SpecialRecord) {
    $parts = New-Object System.Collections.ArrayList
    foreach ($match in @(Get-ArrayField $SpecialRecord "sourceMatches")) {
        if ([int](Get-Field $match "count" 0) -le 0) { continue }
        $kind = [string](Get-Field $match "kind" "")
        $offset = @((Get-ArrayField $match "wadRelativeOffsets") | Select-Object -First 1)
        if ([string]::IsNullOrWhiteSpace($kind)) { continue }
        if ($offset.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$offset[0])) {
            [void]$parts.Add("$kind@$($offset[0])")
        }
        else {
            [void]$parts.Add($kind)
        }
    }
    return ($parts.ToArray() -join "; ")
}

function Get-LinkedSourceLeads($SpecialRecord) {
    $basePointer = Convert-HexTextToInt64 ([string](Get-Field $SpecialRecord "specialDataPointer" "")) -1
    $baseWad = Get-FirstSourceWadOffset $SpecialRecord
    $leads = New-Object System.Collections.ArrayList
    foreach ($field in @(Get-ArrayField $SpecialRecord "pointerFields")) {
        $pointer = Convert-HexTextToInt64 ([string](Get-Field $field "pointer" "")) -1
        if (-not (Test-PsxPointer $pointer)) { continue }
        $lead = [ordered]@{
            role = [string](Get-Field $field "offset" "")
            pointer = [string](Get-Field $field "pointer" "")
            targetPreview = @((Get-ArrayField $field "targetLeadInt16Fields") | Select-Object -First 6)
            inferredSourceWadOffset = ""
        }
        if ((Test-PsxPointer $basePointer) -and $baseWad -ge 0) {
            $lead.inferredSourceWadOffset = Format-HexOffset ($baseWad + ($pointer - $basePointer))
        }
        [void]$leads.Add($lead)
    }
    return @($leads.ToArray())
}

function Get-FunctionalCategory([string]$Text) {
    $lower = $Text.ToLowerInvariant()
    if ($lower -match "helper|invisible|inactive|no-collision") { return "helper or inactive record" }
    if ($lower -match "sheep|fodder|ai|home") { return "actor/home-anchor risk" }
    if ($lower -match "chest|container|collision|reward|contents|interact") { return "container/collision/reward risk" }
    if ($lower -match "gem|treasure|collect") { return "collectible/reward candidate" }
    if ($lower -match "tree|lamp|flag|scenery") { return "scenery placement candidate" }
    return "unknown functional role"
}

function Get-LabelMap($Catalog, $LiveOverrides) {
    $map = @{}
    foreach ($source in @($Catalog, $LiveOverrides)) {
        foreach ($moby in @(Get-ArrayField $source "mobys")) {
            $index = [int](Get-Field $moby "index" -1)
            if ($index -lt 0) { continue }
            if (-not $map.ContainsKey($index)) { $map[$index] = [ordered]@{} }
            foreach ($name in @("displayTargetLabel", "typeHex", "stateHex", "confidence", "evidence")) {
                $value = Get-Field $moby $name $null
                if ($null -ne $value -and -not [string]::IsNullOrWhiteSpace([string]$value)) {
                    $map[$index][$name] = $value
                }
            }
            $behavior = ""
            foreach ($candidate in @(Get-ArrayField $moby "publicTargetCandidates")) {
                if ([string](Get-Field $candidate "category" "") -ne "behavior") { continue }
                $behavior = [string](Get-Field $candidate "reason" (Get-Field $candidate "name" ""))
                break
            }
            if (-not [string]::IsNullOrWhiteSpace($behavior)) { $map[$index]["behaviorNote"] = $behavior }
        }
    }
    return $map
}

$catalog = Read-JsonIfPresent $CatalogPath
$liveOverrides = Read-JsonIfPresent $LiveOverridesPath
$specialData = Read-JsonIfPresent $SpecialDataPath
$patchMatrix = Read-JsonIfPresent $PatchMatrixPath
$sourceValidation = Read-JsonIfPresent $SourceValidationPath
$behaviorDiff = Read-JsonIfPresent $BehaviorDiffPath

$labelByIndex = Get-LabelMap $catalog $liveOverrides
$specialByIndex = @{}
foreach ($record in @(Get-ArrayField $specialData "records")) {
    $index = [int](Get-Field $record "index" -1)
    if ($index -ge 0) { $specialByIndex[$index] = $record }
}
$patchByIndex = @{}
foreach ($test in @(Get-ArrayField $patchMatrix "rankedPatchTests")) {
    $index = [int](Get-Field $test "index" -1)
    if ($index -ge 0) { $patchByIndex[$index] = $test }
}
$validationByIndex = @{}
foreach ($validation in @(Get-ArrayField $sourceValidation "validations")) {
    $index = [int](Get-Field $validation "runtimeMobyIndex" -1)
    if ($index -ge 0) { $validationByIndex[$index] = $validation }
}
$behaviorByIndex = @{}
foreach ($row in @(Get-ArrayField $behaviorDiff "rows")) {
    $index = [int](Get-Field $row "index" -1)
    if ($index -ge 0) { $behaviorByIndex[$index] = $row }
}

$indexes = New-Object System.Collections.Generic.HashSet[int]
foreach ($i in @(21, 32, 87, 109, 120, 131, 142, 153, 186, 208)) { [void]$indexes.Add([int]$i) }
foreach ($i in @($patchByIndex.Keys)) { [void]$indexes.Add([int]$i) }
foreach ($i in @($specialByIndex.Keys)) { [void]$indexes.Add([int]$i) }

$objects = New-Object System.Collections.ArrayList
foreach ($index in @($indexes | Sort-Object)) {
    $meta = if ($labelByIndex.ContainsKey($index)) { $labelByIndex[$index] } else { [ordered]@{} }
    $test = if ($patchByIndex.ContainsKey($index)) { $patchByIndex[$index] } else { $null }
    $lead = Get-Field $test "sourceLead" $null
    $special = if ($specialByIndex.ContainsKey($index)) { $specialByIndex[$index] } else { $null }
    $validation = if ($validationByIndex.ContainsKey($index)) { $validationByIndex[$index] } else { $null }
    $behavior = if ($behaviorByIndex.ContainsKey($index)) { $behaviorByIndex[$index] } else { $null }
    $label = [string](Get-Field $meta "displayTargetLabel" (Get-Field $test "currentLabel" "L$index"))
    $behaviorNote = [string](Get-Field $meta "behaviorNote" "")
    $sourceValues = @()
    if ($null -ne $lead) { $sourceValues = @(Get-ArrayField $lead "values") }
    $linkedLeads = if ($null -ne $special -and [bool](Get-Field $special "validMainRamPointer" $false)) { @(Get-LinkedSourceLeads $special) } else { @() }

    [void]$objects.Add([ordered]@{
        index = $index
        label = $label
        typeHex = [string](Get-Field $meta "typeHex" (Get-Field $special "typeHex" ""))
        confidence = [string](Get-Field $meta "confidence" "")
        functionalCategory = Get-FunctionalCategory (($label, $behaviorNote) -join " ")
        behaviorNote = $behaviorNote
        sourcePatchPriority = [int](Get-Field $test "priority" 0)
        sourceLead = $(if ($null -ne $lead) { [ordered]@{
            status = [string](Get-Field $lead "status" "")
            wadRelativeWindow = [string](Get-Field $lead "wadRelativeWindow" "")
            axisSet = [string](Get-Field $lead "axisSet" "")
            values = $sourceValues
        }} else { $null })
        sourceValidationStatus = [string](Get-Field $validation "status" "")
        interactionProofStatus = [string](Get-Field $validation "interactionProofStatus" "")
        behaviorDiffInterpretation = [string](Get-Field $behavior "interpretation" "")
        specialData = $(if ($null -ne $special -and [bool](Get-Field $special "validMainRamPointer" $false)) { [ordered]@{
            pointer = [string](Get-Field $special "specialDataPointer" "")
            linkedPointerCount = @(Get-ArrayField $special "pointerFields").Count
            sourceMatches = Get-SourceMatchText $special
            linkedSourceLeads = $linkedLeads
        }} else { $null })
    })
}

$highSignal = @($objects | Where-Object {
    [string](Get-Field $_ "functionalCategory" "") -match "container|actor|collectible" -or [int](Get-Field $_ "sourcePatchPriority" 0) -gt 0
})

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "New-StoneHillFunctionalMovePlan.ps1"
    purpose = "Consolidated Stone Hill object identity, permanent source-placement, and behavior/collision proof plan."
    inputs = [ordered]@{
        catalogPath = Resolve-WorkspacePath $CatalogPath
        liveOverridesPath = Resolve-WorkspacePath $LiveOverridesPath
        specialDataPath = Resolve-WorkspacePath $SpecialDataPath
        patchMatrixPath = Resolve-WorkspacePath $PatchMatrixPath
        sourceValidationPath = Resolve-WorkspacePath $SourceValidationPath
        behaviorDiffPath = Resolve-WorkspacePath $BehaviorDiffPath
    }
    currentConclusion = @(
        "Live RAM XYZ edits are identity probes only. In the current DuckStation state, chest main XYZ fields moved while linked special-data blocks did not.",
        "Permanent functional movement should be judged only after fresh-loading a Source BIN and seeing source-placement-loaded in validation.",
        "For chests, a placement proof is still not enough; break/open interaction needs a follow-up RAM pair showing reward counters, collectable flags, or linked behavior blocks changing."
    )
    highSignalObjects = @($highSignal)
    objects = @($objects.ToArray())
}

$outJsonPath = Resolve-WorkspacePath $OutJsonPath
$outMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outJsonPath -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# Stone Hill Functional Move Plan")
[void]$md.Add("")
[void]$md.Add("Generated: $($result.generatedAt)")
[void]$md.Add("")
[void]$md.Add("## Current Conclusion")
foreach ($line in @($result.currentConclusion)) {
    [void]$md.Add("- $line")
}
[void]$md.Add("")
[void]$md.Add("## Native Editor Workflow")
[void]$md.Add("- Move one moby at a time in the native editor and save edits.")
[void]$md.Add("- Use Create Source BIN, then fresh-load the generated CUE before using Validate Source.")
[void]$md.Add("- Treat source-placement-loaded as permanent placement proof only.")
[void]$md.Add("- For chests, gems, and fodder, interact with the object after placement proof, then run Behavior Diff so collision/reward/AI evidence is separated from visual placement.")
[void]$md.Add("- If placement loads but behavior fails, search the linked source WAD offsets listed below instead of moving only the runtime XYZ fields.")
[void]$md.Add("")
[void]$md.Add("## High-Signal Objects")
[void]$md.Add("")
[void]$md.Add("| Moby | Role | Source lead | Source validation | Interaction proof | Behavior/linked-block note |")
[void]$md.Add("| --- | --- | --- | --- | --- | --- |")
foreach ($item in @($highSignal | Sort-Object @{ Expression = { if ([int](Get-Field $_ "sourcePatchPriority" 0) -gt 0) { [int](Get-Field $_ "sourcePatchPriority" 0) } else { 99 } } }, index)) {
    $lead = Get-Field $item "sourceLead" $null
    $leadText = "none"
    if ($null -ne $lead) {
        $values = @(Get-ArrayField $lead "values") -join "; "
        $leadWindow = [string](Get-Field $lead "wadRelativeWindow" "")
        $leadAxisSet = [string](Get-Field $lead "axisSet" "")
        $leadText = "$leadWindow $leadAxisSet $values".Trim()
        if ([string]::IsNullOrWhiteSpace($leadText)) { $leadText = "none" }
    }
    $note = [string](Get-Field $item "behaviorDiffInterpretation" "")
    if ([string]::IsNullOrWhiteSpace($note)) { $note = [string](Get-Field $item "behaviorNote" "") }
    if ([string]::IsNullOrWhiteSpace($note)) { $note = "-" }
    $sourceStatus = [string](Get-Field $item "sourceValidationStatus" "")
    if ([string]::IsNullOrWhiteSpace($sourceStatus)) { $sourceStatus = "-" }
    $proofStatus = [string](Get-Field $item "interactionProofStatus" "")
    if ([string]::IsNullOrWhiteSpace($proofStatus)) { $proofStatus = "-" }
    [void]$md.Add("| L$($item.index) $($item.label) | $($item.functionalCategory) | $leadText | $sourceStatus | $proofStatus | $($note.Replace('|','/')) |")
}
[void]$md.Add("")
[void]$md.Add("## Linked Behavior Source Leads")
[void]$md.Add("")
[void]$md.Add("| Moby | Special pointer | Source match | Linked source leads |")
[void]$md.Add("| --- | --- | --- | --- |")
foreach ($item in @($objects | Where-Object { $null -ne (Get-Field $_ "specialData" $null) } | Sort-Object index)) {
    $special = Get-Field $item "specialData" $null
    $links = @(Get-ArrayField $special "linkedSourceLeads" | Select-Object -First 8 | ForEach-Object {
        $role = [string](Get-Field $_ "role" "")
        $sourceOffset = [string](Get-Field $_ "inferredSourceWadOffset" "")
        if ([string]::IsNullOrWhiteSpace($sourceOffset)) { $sourceOffset = "no-source-match" }
        "$role->$sourceOffset"
    }) -join "; "
    if ([string]::IsNullOrWhiteSpace($links)) { $links = "none" }
    [void]$md.Add("| L$($item.index) $($item.label) | $($special.pointer) | $($special.sourceMatches) | $links |")
}
[void]$md.Add("")
[void]$md.Add("## Practical Next Proof")
[void]$md.Add("- Fresh-load a patched CUE that moves L109 or L120 and rerun Validate Source. Current live RAM still reflects visual RAM tests, not source-placement proof.")
[void]$md.Add("- Once a chest reports source-placement-loaded, interact with it and rerun Behavior Diff with the post-interaction RAM. A functional success needs collision/break behavior plus a reward/counter or linked-block change.")
[void]$md.Add("- Keep L87/L153 as red-gem source proof candidates because live pickup already changed their states; L186 remains the collected-gem calibration target.")

$md | Set-Content -LiteralPath $outMarkdownPath -Encoding UTF8

Write-Host "Wrote Stone Hill functional move plan JSON to $outJsonPath"
Write-Host "Wrote Stone Hill functional move plan Markdown to $outMarkdownPath"
