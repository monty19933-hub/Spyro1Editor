param(
    [string]$InventoryPath = ".\stonehill-full-moby-inventory.json",
    [string]$OverridesPath = ".\stonehill-live-validation-overrides.json",
    [string]$NativeEditsPath = ".\stonehill-native-edits.json",
    [string]$ValidationPath = ".\stonehill-current-edits-live-validation.json",
    [string]$OutJsonPath = ".\stonehill-moby-clusters.json",
    [string]$OutMarkdownPath = ".\stonehill-moby-clusters.md",
    [double]$NeighborhoodRadius = 384.0
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

function Get-DoubleField($Object, [string]$Name) {
    return [double](Get-Field $Object $Name 0)
}

function Get-EditedCoord($Edit, [string]$Axis) {
    $edited = Get-Field $Edit "edited" $null
    return [double](Get-Field $edited $Axis 0)
}

function Convert-LegacyIndexToTrue([int]$LegacyIndex) {
    $numerator = ($LegacyIndex * 0x50) - 8
    if ($numerator -ge 0 -and ($numerator % 0x58) -eq 0) { return [int]($numerator / 0x58) }
    return -1
}

function Get-MetadataTrueIndex($Entry) {
    $trueIndex = Get-Field $Entry "trueIndex" $null
    if ($null -ne $trueIndex) { return [int]$trueIndex }
    $legacyIndex = Get-Field $Entry "legacyIndex" $null
    if ($null -ne $legacyIndex) { return Convert-LegacyIndexToTrue ([int]$legacyIndex) }
    $index = Get-Field $Entry "index" $null
    if ($null -eq $index) { return -1 }
    return Convert-LegacyIndexToTrue ([int]$index)
}

function New-MobyRow($Moby, [double]$Distance = 0.0) {
    return [ordered]@{
        id = [string](Get-Field $Moby "id" "")
        trueIndex = [int](Get-Field $Moby "trueIndex" -1)
        legacyAlias = [string](Get-Field $Moby "legacyAlias" "")
        distance = [Math]::Round($Distance, 3)
        typeHex = [string](Get-Field $Moby "typeHex" "")
        stateHex = [string](Get-Field $Moby "stateHex" "")
        x = Get-DoubleField $Moby "x"
        y = Get-DoubleField $Moby "y"
        z = Get-DoubleField $Moby "z"
        specialDataPointer = [string](Get-Field $Moby "specialDataPointer" "")
        flag52Hex = [string](Get-Field $Moby "flag52Hex" "")
        flag53Hex = [string](Get-Field $Moby "flag53Hex" "")
        label = [string](Get-Field $Moby "displayLabel" "")
    }
}

$inventoryPath = Resolve-WorkspacePath $InventoryPath
$overridesPath = Resolve-WorkspacePath $OverridesPath
$editsPath = Resolve-WorkspacePath $NativeEditsPath
$validationPath = Resolve-WorkspacePath $ValidationPath
$outJson = Resolve-WorkspacePath $OutJsonPath
$outMarkdown = Resolve-WorkspacePath $OutMarkdownPath

if (-not (Test-Path -LiteralPath $inventoryPath)) { throw "Missing inventory: $inventoryPath" }
$inventory = Get-Content -Raw -LiteralPath $inventoryPath | ConvertFrom-Json
$mobys = @(Get-ArrayField $inventory "mobys")

if (Test-Path -LiteralPath $overridesPath) {
    $byTrueIndex = @{}
    foreach ($moby in $mobys) {
        $trueIndex = [int](Get-Field $moby "trueIndex" -1)
        if ($trueIndex -ge 0) { $byTrueIndex[$trueIndex] = $moby }
    }

    $overridesRoot = Get-Content -Raw -LiteralPath $overridesPath | ConvertFrom-Json
    foreach ($override in @(Get-ArrayField $overridesRoot "mobys")) {
        $trueIndex = Get-MetadataTrueIndex $override
        if ($trueIndex -lt 0 -or -not $byTrueIndex.ContainsKey($trueIndex)) { continue }
        $moby = $byTrueIndex[$trueIndex]
        $label = [string](Get-Field $override "displayTargetLabel" "")
        if (-not [string]::IsNullOrWhiteSpace($label)) { $moby.displayLabel = $label }
        $kind = [string](Get-Field $override "candidateKind" "")
        if (-not [string]::IsNullOrWhiteSpace($kind)) { $moby.identityFamily = $kind }
        $confidence = [string](Get-Field $override "confidence" "")
        if (-not [string]::IsNullOrWhiteSpace($confidence)) { $moby.confidence = $confidence }
        $evidence = [string](Get-Field $override "evidence" "")
        if (-not [string]::IsNullOrWhiteSpace($evidence)) { $moby.evidence = $evidence }
    }
}

$edits = @()
if (Test-Path -LiteralPath $editsPath) {
    $editsRoot = Get-Content -Raw -LiteralPath $editsPath | ConvertFrom-Json
    $edits = @(Get-ArrayField $editsRoot "edits")
}

$validationByIndex = @{}
$validationTrustNote = ""
if (Test-Path -LiteralPath $validationPath) {
    $validationRoot = Get-Content -Raw -LiteralPath $validationPath | ConvertFrom-Json
    $runtime = Get-Field $validationRoot "runtime" $null
    $runtimePointer = [string](Get-Field $runtime "ptrLevelMobys" "")
    $decodedPlausibleMobys = [int](Get-Field $runtime "decodedPlausibleMobys" 0)
    if ($decodedPlausibleMobys -ge 100 -and $runtimePointer -ne "0x80080000") {
        foreach ($item in @(Get-ArrayField $validationRoot "validations")) {
            $runtimeIndex = [int](Get-Field $item "runtimeMobyIndex" -1)
            if ($runtimeIndex -ge 0) { $validationByIndex[$runtimeIndex] = $item }
        }
    } elseif ($decodedPlausibleMobys -gt 0 -or -not [string]::IsNullOrWhiteSpace($runtimePointer)) {
        $validationTrustNote = "Ignored validation report $validationPath because runtime pointer $runtimePointer decoded only $decodedPlausibleMobys plausible mobys. Stone Hill should decode about 197 records."
    }
}

$exactGroups = New-Object System.Collections.ArrayList
foreach ($group in @($mobys | Group-Object { "{0:0.####},{1:0.####},{2:0.####}" -f (Get-DoubleField $_ "x"), (Get-DoubleField $_ "y"), (Get-DoubleField $_ "z") } | Where-Object { $_.Count -gt 1 } | Sort-Object Count -Descending)) {
    $items = @($group.Group | Sort-Object { [int](Get-Field $_ "trueIndex" 0) } | ForEach-Object { New-MobyRow $_ })
    [void]$exactGroups.Add([ordered]@{
        xyzKey = [string]$group.Name
        count = [int]$group.Count
        records = $items
    })
}

$neighborhoods = New-Object System.Collections.ArrayList
foreach ($edit in @($edits | Sort-Object { [int](Get-Field $_ "trueIndex" (Get-Field $_ "index" 0)) })) {
    $trueIndex = [int](Get-Field $edit "trueIndex" (Get-Field $edit "index" -1))
    if ($trueIndex -lt 0) { continue }
    $original = Get-Field $edit "original" $null
    $originX = [double](Get-Field $original "x" (Get-EditedCoord $edit "x"))
    $originY = [double](Get-Field $original "y" (Get-EditedCoord $edit "y"))
    $neighbors = New-Object System.Collections.ArrayList
    foreach ($moby in $mobys) {
        $dx = (Get-DoubleField $moby "x") - $originX
        $dy = (Get-DoubleField $moby "y") - $originY
        $distance = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
        if ($distance -le $NeighborhoodRadius) {
            [void]$neighbors.Add((New-MobyRow $moby $distance))
        }
    }

    $validation = $null
    if ($validationByIndex.ContainsKey($trueIndex)) { $validation = $validationByIndex[$trueIndex] }
    $currentMoby = $mobys | Where-Object { [int](Get-Field $_ "trueIndex" -1) -eq $trueIndex } | Select-Object -First 1
    $editLabel = [string](Get-Field $currentMoby "displayLabel" (Get-Field $edit "label" ""))
    [void]$neighborhoods.Add([ordered]@{
        editedId = "T$trueIndex"
        label = $editLabel
        typeHex = [string](Get-Field $edit "typeHex" "")
        original = [ordered]@{
            x = $originX
            y = $originY
            z = [double](Get-Field $original "z" (Get-EditedCoord $edit "z"))
        }
        edited = [ordered]@{
            x = Get-EditedCoord $edit "x"
            y = Get-EditedCoord $edit "y"
            z = Get-EditedCoord $edit "z"
        }
        validationStatus = [string](Get-Field $validation "status" "")
        neighbors = @($neighbors.ToArray() | Sort-Object { [double](Get-Field $_ "distance" 0) }, { [int](Get-Field $_ "trueIndex" 0) })
    })
}

$wellRecords = @($mobys | Where-Object {
    $x = Get-DoubleField $_ "x"
    $y = Get-DoubleField $_ "y"
    ($x -ge 7300 -and $x -le 8350 -and $y -ge 7800 -and $y -le 8450)
} | Sort-Object { Get-DoubleField $_ "y" }, { Get-DoubleField $_ "x" }, { [int](Get-Field $_ "trueIndex" 0) } | ForEach-Object { New-MobyRow $_ })

$savedEditTrueIndexes = @($edits | ForEach-Object {
    [int](Get-Field $_ "trueIndex" (Get-Field $_ "index" -1))
} | Where-Object { $_ -ge 0 } | Sort-Object -Unique)

$dragonSeedIndexes = @(86, 140, 185)
$savedDragonEditIds = @($dragonSeedIndexes | Where-Object { $savedEditTrueIndexes -contains $_ } | ForEach-Object { "T$_" })
$missingDragonEditIds = @($dragonSeedIndexes | Where-Object { -not ($savedEditTrueIndexes -contains $_) } | ForEach-Object { "T$_" })
$dragonSeeds = @($mobys | Where-Object { $dragonSeedIndexes -contains [int](Get-Field $_ "trueIndex" -1) })
$dragonCenter = [ordered]@{ x = 0.0; y = 0.0 }
$dragonRecords = @()
if ($dragonSeeds.Count -gt 0) {
    $sumX = 0.0
    $sumY = 0.0
    foreach ($seed in $dragonSeeds) {
        $sumX += Get-DoubleField $seed "x"
        $sumY += Get-DoubleField $seed "y"
    }
    $centerX = $sumX / [double]$dragonSeeds.Count
    $centerY = $sumY / [double]$dragonSeeds.Count
    $dragonCenter = [ordered]@{
        x = [Math]::Round($centerX, 4)
        y = [Math]::Round($centerY, 4)
    }

    $dragonRecordList = New-Object System.Collections.ArrayList
    foreach ($moby in $mobys) {
        $dx = (Get-DoubleField $moby "x") - $centerX
        $dy = (Get-DoubleField $moby "y") - $centerY
        $distance = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
        if ($distance -le $NeighborhoodRadius) {
            [void]$dragonRecordList.Add((New-MobyRow $moby $distance))
        }
    }
    $dragonRecords = @($dragonRecordList.ToArray() | Sort-Object { [double](Get-Field $_ "distance" 0) }, { [int](Get-Field $_ "trueIndex" 0) })
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Groups Stone Hill moby records by exact placement and by neighborhoods around current edits. This helps identify split objects such as dragon/pedestal/control-record sets."
    neighborhoodRadius = $NeighborhoodRadius
    validationTrustNote = $validationTrustNote
    exactPlacementGroups = @($exactGroups.ToArray())
    editedNeighborhoods = @($neighborhoods.ToArray())
    dryWellCandidateCluster = [ordered]@{
        note = "This cluster includes the confirmed T70 delayed-activation well whirlwind plus nearby well pedestal/control records."
        records = $wellRecords
    }
    dragonPlatformCandidateCluster = [ordered]@{
        note = "User-observed dragon station: T86 appears to be the dragon, T140 the pedestal, and T185 may be the fairy or post-rescue pedestal-light record. Move the seed records together for visible tests."
        seedIds = @($dragonSeedIndexes | ForEach-Object { "T$_" })
        savedEditSeedIds = $savedDragonEditIds
        missingEditSeedIds = $missingDragonEditIds
        center = $dragonCenter
        records = $dragonRecords
    }
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outJson -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Stone Hill Moby Clusters")
[void]$lines.Add("")
[void]$lines.Add("- Generated: $($result.generatedAt)")
[void]$lines.Add("- Neighborhood radius: $NeighborhoodRadius")
[void]$lines.Add("- Purpose: $($result.purpose)")
if (-not [string]::IsNullOrWhiteSpace($validationTrustNote)) {
    [void]$lines.Add("- Validation note: $validationTrustNote")
}
[void]$lines.Add("")
[void]$lines.Add("## Dry Well / Whirlwind Candidate Cluster")
[void]$lines.Add("")
[void]$lines.Add("Observation: T70 is confirmed as the delayed-activation well whirlwind. Nearby T75/T180-T184 records remain a pedestal/control stack candidate, so avoid treating every dry-well record as Gavin.")
[void]$lines.Add("")
[void]$lines.Add("| id | type | state | XYZ | special | flags | label |")
[void]$lines.Add("|---:|---:|---:|---|---|---|---|")
foreach ($moby in $wellRecords) {
    $xyz = "{0}, {1}, {2}" -f $moby.x, $moby.y, $moby.z
    $flags = "$($moby.flag52Hex)/$($moby.flag53Hex)"
    [void]$lines.Add("| $($moby.id) | $($moby.typeHex) | $($moby.stateHex) | $xyz | $($moby.specialDataPointer) | $flags | $(Escape-Markdown $moby.label) |")
}

[void]$lines.Add("")
[void]$lines.Add("## Dragon / Pedestal Candidate Cluster")
[void]$lines.Add("")
[void]$lines.Add("Observation: user identified T86 as the visible dragon, T140 as the dragon pedestal, and T185 as the likely fairy or post-rescue pedestal-light record. T86 and T185 share exact original XY, and T140 is within 4 world units.")
[void]$lines.Add("")
if ($savedDragonEditIds.Count -gt 0) {
    [void]$lines.Add("- Current saved seed edits: $($savedDragonEditIds -join ', ')")
}
if ($missingDragonEditIds.Count -gt 0) {
    [void]$lines.Add("- Seed records not in current saved edits: $($missingDragonEditIds -join ', ')")
}
[void]$lines.Add("")
[void]$lines.Add("| id | distance | type | state | XYZ | special | flags | label |")
[void]$lines.Add("|---:|---:|---:|---:|---|---|---|---|")
foreach ($moby in @($dragonRecords | Select-Object -First 24)) {
    $xyz = "{0}, {1}, {2}" -f $moby.x, $moby.y, $moby.z
    $flags = "$($moby.flag52Hex)/$($moby.flag53Hex)"
    [void]$lines.Add("| $($moby.id) | $($moby.distance) | $($moby.typeHex) | $($moby.stateHex) | $xyz | $($moby.specialDataPointer) | $flags | $(Escape-Markdown $moby.label) |")
}

[void]$lines.Add("")
[void]$lines.Add("## Edited Neighborhoods")
foreach ($neighborhood in @($neighborhoods.ToArray())) {
    [void]$lines.Add("")
    [void]$lines.Add("### $($neighborhood.editedId) $(Escape-Markdown $neighborhood.label)")
    [void]$lines.Add("")
    [void]$lines.Add("- Original: $($neighborhood.original.x), $($neighborhood.original.y), $($neighborhood.original.z)")
    [void]$lines.Add("- Edited: $($neighborhood.edited.x), $($neighborhood.edited.y), $($neighborhood.edited.z)")
    if (-not [string]::IsNullOrWhiteSpace([string]$neighborhood.validationStatus)) {
        [void]$lines.Add("- Validation: $($neighborhood.validationStatus)")
    }
    [void]$lines.Add("")
    [void]$lines.Add("| nearby id | distance | type | state | XYZ | special | flags | label |")
    [void]$lines.Add("|---:|---:|---:|---:|---|---|---|---|")
    foreach ($moby in @($neighborhood.neighbors | Select-Object -First 20)) {
        $xyz = "{0}, {1}, {2}" -f $moby.x, $moby.y, $moby.z
        $flags = "$($moby.flag52Hex)/$($moby.flag53Hex)"
        [void]$lines.Add("| $($moby.id) | $($moby.distance) | $($moby.typeHex) | $($moby.stateHex) | $xyz | $($moby.specialDataPointer) | $flags | $(Escape-Markdown $moby.label) |")
    }
}

[void]$lines.Add("")
[void]$lines.Add("## Exact Placement Groups")
[void]$lines.Add("")
[void]$lines.Add("| XYZ | count | records |")
[void]$lines.Add("|---|---:|---|")
foreach ($group in @($exactGroups.ToArray() | Select-Object -First 40)) {
    $records = @($group.records | ForEach-Object { "$($_.id) $($_.typeHex) $($_.flag53Hex)" }) -join ", "
    [void]$lines.Add("| $(Escape-Markdown $group.xyzKey) | $($group.count) | $(Escape-Markdown $records) |")
}

$lines | Set-Content -LiteralPath $outMarkdown -Encoding UTF8

Write-Host "Wrote moby cluster report to $outMarkdown"
Write-Host "Wrote moby cluster data to $outJson"
Write-Host ("Dry well candidate records: {0}; edited neighborhoods: {1}; exact placement groups: {2}" -f $wellRecords.Count, @($neighborhoods).Count, @($exactGroups).Count)
