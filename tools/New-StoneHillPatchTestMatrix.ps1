param(
    [string]$CatalogPath = ".\stonehill-moby-catalog.json",
    [string]$SourceLinksPath = ".\stonehill-moby-source-links.json",
    [string]$SourceLayoutsPath = ".\stonehill-source-window-layouts.json",
    [string]$TargetBoardPath = ".\stonehill-replica-target-board.json",
    [string]$LiveOverridesPath = ".\stonehill-live-validation-overrides.json",
    [string]$OutJsonPath = ".\stonehill-patch-test-matrix.json",
    [string]$OutMarkdownPath = ".\stonehill-patch-test-matrix.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function Get-ArrayField($Object, [string]$Name) {
    if ($null -eq $Object) { return @() }
    if ($Object -is [System.Collections.IDictionary]) {
        if (-not $Object.Contains($Name) -or $null -eq $Object[$Name]) { return @() }
        return @($Object[$Name])
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return @() }
    return @($property.Value)
}

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if (-not $Object.Contains($Name) -or $null -eq $Object[$Name]) { return $Default }
        return $Object[$Name]
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return $Default }
    return $property.Value
}

function Read-JsonIfPresent([string]$Path) {
    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $resolved) { return $null }
    return Get-Content -Raw -LiteralPath $resolved.Path | ConvertFrom-Json
}

function Merge-Objects($Base, $Override) {
    if ($null -eq $Override) { return $Base }
    $merged = [ordered]@{}
    if ($null -ne $Base) {
        foreach ($prop in $Base.PSObject.Properties) {
            $merged[$prop.Name] = $prop.Value
        }
    }
    foreach ($prop in $Override.PSObject.Properties) {
        $hasValue = $null -ne $prop.Value
        if ($hasValue -and $prop.Value -is [string]) { $hasValue = -not [string]::IsNullOrWhiteSpace($prop.Value) }
        if ($hasValue -and $prop.Value -is [System.Array]) { $hasValue = @($prop.Value).Count -gt 0 }
        if ($hasValue) {
            $merged[$prop.Name] = $prop.Value
        }
    }
    return $merged
}

function Get-LiveOverrideMap($LiveOverrides) {
    $map = @{}
    foreach ($moby in @(Get-ArrayField $LiveOverrides "mobys")) {
        $index = [int](Get-Field $moby "index" -1)
        if ($index -ge 0) { $map[$index] = $moby }
    }
    return $map
}

function Format-LeadValues($Lead) {
    if ($null -eq $Lead) { return "" }
    return (@(Get-ArrayField $Lead "values") -join "; ")
}

function Get-AxisSet($Lead) {
    if ($null -eq $Lead) { return "" }
    $axisSet = [string](Get-Field $Lead "axisSet" "")
    if (-not [string]::IsNullOrWhiteSpace($axisSet)) { return $axisSet }
    $axes = @(Format-LeadValues $Lead | Select-String -AllMatches -Pattern '([xyz])=' | ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    return -join $axes
}

function Get-AxisPreferenceRank([string]$AxisSet, [string[]]$PreferredAxisSets) {
    for ($i = 0; $i -lt $PreferredAxisSets.Count; $i++) {
        if ($AxisSet -eq $PreferredAxisSets[$i]) { return $i }
    }
    return 999
}

function Get-BestLayoutLead($Layouts, [int]$Index, [string[]]$PreferredAxisSets = @("xz")) {
    $leads = @(Get-ArrayField $Layouts "topActionableSourceLeads" | Where-Object { [int](Get-Field $_ "index" -1) -eq $Index })
    if ($leads.Count -eq 0) { return $null }
    $preferred = @($leads | Where-Object { [string](Get-Field $_ "axisSet" "") -in $PreferredAxisSets })
    if ($preferred.Count -gt 0) { return ($preferred | Sort-Object -Property @{ Expression = { [int](Get-Field $_ "score" 0) }; Descending = $true }, @{ Expression = { [int](Get-Field $_ "spread" 9999) }; Descending = $false } | Select-Object -First 1) }
    return ($leads | Sort-Object -Property @{ Expression = { [int](Get-Field $_ "score" 0) }; Descending = $true }, @{ Expression = { [int](Get-Field $_ "spread" 9999) }; Descending = $false } | Select-Object -First 1)
}

function Get-BestSourceLinkWindow($SourceLinks, [int]$Index, [string[]]$PreferredAxisSets = @()) {
    $record = @(Get-ArrayField $SourceLinks "records" | Where-Object { [int](Get-Field $_ "index" -1) -eq $Index } | Select-Object -First 1)
    if ($record.Count -eq 0) { return $null }
    $windows = @(Get-ArrayField $record[0] "scaledAxisWindows")
    if ($windows.Count -eq 0) { return $null }
    foreach ($axisSet in $PreferredAxisSets) {
        $preferred = @($windows | Where-Object { (Get-AxisSet $_) -eq $axisSet } | Select-Object -First 1)
        if ($preferred.Count -gt 0) { return $preferred[0] }
    }
    return $windows[0]
}

function Get-Moby($Catalog, [int]$Index) {
    $match = @(Get-ArrayField $Catalog "mobys" | Where-Object { [int](Get-Field $_ "index" -1) -eq $Index } | Select-Object -First 1)
    $moby = if ($match.Count -eq 0) { $null } else { $match[0] }
    if ($script:LiveOverrideMap.ContainsKey($Index)) {
        return Merge-Objects $moby $script:LiveOverrideMap[$Index]
    }
    return $moby
}

function New-TestRow(
    [int]$Priority,
    [string]$Target,
    [int]$Index,
    [string]$Purpose,
    [string]$EditorAction,
    [string]$SuccessSignal,
    [string]$Fallback,
    $Catalog,
    $SourceLinks,
    $Layouts,
    [string[]]$PreferredAxisSets = @("xz")
) {
    $moby = Get-Moby $Catalog $Index
    $lead = Get-BestLayoutLead $Layouts $Index $PreferredAxisSets
    $sourceWindow = Get-BestSourceLinkWindow $SourceLinks $Index $PreferredAxisSets
    $leadAxisSet = Get-AxisSet $lead
    $sourceAxisSet = Get-AxisSet $sourceWindow
    $activeLead = if ($null -ne $lead) { $lead } else { $sourceWindow }
    $leadRank = Get-AxisPreferenceRank $leadAxisSet $PreferredAxisSets
    $sourceRank = Get-AxisPreferenceRank $sourceAxisSet $PreferredAxisSets
    if ($null -ne $sourceWindow -and $sourceRank -lt $leadRank) {
        $activeLead = $sourceWindow
    }
    $hasLead = $null -ne $activeLead
    $wadWindow = if ($hasLead) { [string](Get-Field $activeLead "wadRelativeWindow" "") } else { "" }
    $axisSet = Get-AxisSet $activeLead

    return [ordered]@{
        priority = $Priority
        target = $Target
        moby = "L$Index"
        index = $Index
        typeHex = [string](Get-Field $moby "typeHex" "")
        currentLabel = [string](Get-Field $moby "displayTargetLabel" "")
        candidateKind = [string](Get-Field $moby "candidateKind" "")
        sourceLead = [ordered]@{
            status = $(if ($hasLead) { "patch-testable" } else { "missing-source-window" })
            wadRelativeWindow = $wadWindow
            assetSubfileIndex = $(if ($hasLead) { Get-Field $activeLead "assetSubfileIndex" $null } else { $null })
            axisSet = $axisSet
            score = $(if ($activeLead -eq $lead) { Get-Field $lead "score" $null } else { $null })
            spread = $(if ($hasLead) { Get-Field $activeLead "spread" $null } else { $null })
            values = @(Get-ArrayField $activeLead "values")
        }
        purpose = $Purpose
        editorAction = $EditorAction
        successSignal = $SuccessSignal
        fallback = $Fallback
    }
}

$catalog = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $CatalogPath).Path | ConvertFrom-Json
$sourceLinks = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $SourceLinksPath).Path | ConvertFrom-Json
$layouts = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $SourceLayoutsPath).Path | ConvertFrom-Json
$targetBoard = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $TargetBoardPath).Path | ConvertFrom-Json
$liveOverrides = Read-JsonIfPresent $LiveOverridesPath
$script:LiveOverrideMap = Get-LiveOverrideMap $liveOverrides

$tests = @(
    (New-TestRow 1 "Collected gem source proof" 186 "Use the only current type 0x20 moby with a direct state-change signal from the gem-clean RAM pair to prove a specific treasure source window." "Load Stone Hill Workbench, move L186 a large visible distance, then use Create Source BIN for the disposable fresh-load test." "The exact gem collected in the calibration sample, or its pickup position, visibly moves in Stone Hill." "If nothing moves, keep the L186 window as negative evidence and test the higher-score L109/L120 source-window family next." $catalog $sourceLinks $layouts),
    (New-TestRow 2 "Metal chest source encoder" 109 "Prove that a map-position source window controls the live-tested metal chest moby." "Move L109 a large visible distance, then use Create Source BIN and fresh-load the patched CUE before testing collision/reward behavior." "The metal chest loads at the edited map position, then collision and break behavior can be tested from a fresh boot." "If the chest model moves but interaction does not, keep the source window and search nearby bytes for the linked contents/interact record." $catalog $sourceLinks $layouts @("xy", "xz")),
    (New-TestRow 3 "High-field red gem source proof" 87 "Test the live-validated L87 red-gem source window from the high-field route bucket." "Move L87 far enough to be obvious, then use Create Source BIN and test the disposable copy from a fresh Stone Hill boot." "The red gem loads at the edited map position and remains collectible from the patched disc image." "If the model moves but collection state or reward does not, keep the source window and search nearby bytes for the linked collectable-state record." $catalog $sourceLinks $layouts),
    (New-TestRow 4 "Regular chest source encoder" 120 "Check whether the live-tested regular chest uses the map X/Y source-window family." "Move L120 separately from L109, create a source BIN, and compare the patched fresh-load result against the L109 result." "The regular chest loads at the edited map position with collision/interactions rebuilt by the level loader." "If L109 works but L120 does not, keep L109 as the first confirmed encoder target and rescan around L120's source windows." $catalog $sourceLinks $layouts @("xy", "xz")),
    (New-TestRow 5 "Tree/scenery source proof" 65 "Test the live-validated L65 tree/scenery source window near the castle/Return Home route." "Move L65 and create a source BIN as a low-risk scenery source-record test." "The tree loads at the edited map position from a fresh patched-disc boot." "If the patch does not affect the tree, keep the source window as negative evidence and rescan nearby bytes for the full type 0x30 source record." $catalog $sourceLinks $layouts @("xz", "yz", "xy")),
    (New-TestRow 6 "Tower/cave red gem source proof" 153 "Test the tower/cave-side live-validated red-gem lead separately from the high-field L87 bucket." "Move L153, create a source BIN, and inspect the left-cave/tower route in the disposable copy." "A red gem loads at the edited map position and remains collectible from the patched disc image." "If L87 works but L153 does not, use the result to split red-gem source-window families before spending time on enemy records." $catalog $sourceLinks $layouts),
    (New-TestRow 7 "Portal-side active object" 208 "Check whether the mid-map/east type 0x18 marker is a portal-side enemy/object or a helper near Astor/Return Home." "Move L208 and create a source BIN only after the gem/enemy leads above have been tested." "A visible portal-side object changes, or the result is useful negative evidence for helper classification." "If no visible target changes, demote L208 in replica placement and keep it as route context only." $catalog $sourceLinks $layouts),
    (New-TestRow 8 "Well/chest route bucket" 183 "Probe the well-side helper/source bucket only after focused Gavin/chest RAM samples are unavailable." "Do not treat L183 as a visible object yet; if testing, move it on a disposable BIN and inspect the dry well." "Locked chest, Gavin pedestal, or a well-side object changes." "Preferred path is still separate Gavin-free and locked-chest-open RAM pairs before patching helper-looking records." $catalog $sourceLinks $layouts @("xz", "xy", "yz")),
    (New-TestRow 9 "Hidden beach/key route" 421 "Document that the current key/beach marker has no source-window lead, so RAM capture is the next required step." "Do not patch-test L421 yet; capture hidden-key before/after RAM, then rerun source-link discovery." "The key pickup sample isolates a beach-route record and produces a patch-testable source window." "Use L421/L422 as route anchors only until the hidden-key sample changes them or reveals a new record." $catalog $sourceLinks $layouts)
)

$blockedTargets = @(
    [ordered]@{ target = "Gavin"; blocker = "No isolated dragon/NPC moby in the dry-well cluster."; requiredSample = "stonehill-before-gavin-free.bin and stonehill-after-gavin-free.bin" },
    [ordered]@{ target = "Lindar vs Gildas"; blocker = "The current type 0x30 candidates L54 and L65 are live-tested trees, not named dragons."; requiredSample = "Separate Lindar-free and Gildas-free RAM pairs" },
    [ordered]@{ target = "Blue thief vs ram/shepherd"; blocker = "The current high-field type 0x18 patch leads L87/L142/L153 are live-tested red gems, not enemies."; requiredSample = "Blue-thief, ram-defeat, and shepherd-defeat RAM pairs" },
    [ordered]@{ target = "Key and locked chest"; blocker = "Key route has no source lead, and well/chest route candidates are helper-heavy."; requiredSample = "Hidden-key and locked-chest before/after RAM pairs" },
    [ordered]@{ target = "Sheep fodder"; blocker = "L21 and L32 are live-tested sheep, but source placement and AI/home records are not isolated."; requiredSample = "Sheep-fodder before/after RAM pair" }
)

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Ranked Stone Hill patch-test matrix for turning movable runtime moby hypotheses into verified source-record controls."
    liveOverridesPath = $(if ($null -ne $liveOverrides) { (Resolve-Path -LiteralPath $LiveOverridesPath).Path } else { $null })
    readinessSummary = [ordered]@{
        decodedRuntimeMobys = [int](Get-Field $catalog "decodedMobys" 0)
        placementRuntimeMobys = [int](Get-Field $catalog "placementMobys" 0)
        scaledSourceWindowMobys = [int](Get-Field (Get-Field $sourceLinks "summary" $null) "scaledInt16WindowMobys" 0)
        targetBoardGrade = [string](Get-Field (Get-Field $targetBoard "coverage" $null) "grade" "editor-workbench-ready")
    }
    rankedPatchTests = $tests
    blockedTargetSamples = $blockedTargets
    publicLayoutSources = @(
        "https://spyrowiki.com/wiki/Stone_Hill",
        "https://spyro.fandom.com/wiki/Stone_Hill",
        "https://strategywiki.org/wiki/Spyro_the_Dragon/Stone_Hill"
    )
    interpretation = @(
        "Run the first four tests before spending time on helper-looking records; they target the specific collected gem, the strongest chest lead, and two live-tested red-gem source leads.",
        "A successful L186, L109, or L120 test is the fastest path from movable runtime mobys to a real Stone Hill object-placement encoder.",
        "Named dragons, the key, locked chest, thief, enemy families, and fodder source/home records still need isolated before/after RAM samples before they should be called replica-complete."
    )
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# Stone Hill Patch-Test Matrix")
[void]$md.Add("")
[void]$md.Add("Generated: $($result.generatedAt)")
[void]$md.Add("")
[void]$md.Add("Purpose: ranked tests for turning the current movable Stone Hill runtime mobys into verified source-record controls.")
[void]$md.Add("")
[void]$md.Add("## Current Conclusion")
[void]$md.Add("- The editor workbench is usable now: it can show the Stone Hill map proxy, select labelled runtime mobys, and move them.")
[void]$md.Add("- The first source-record proof should be L186 because it is the specific collected-gem sample; then test L109/L120 for chests and L87/L153 for live-tested red-gem source-window families.")
[void]$md.Add("- Stone Hill is not a verified replica yet because named dragons, enemy families, the key, locked chest, and fodder source/home records still lack focused RAM samples.")
[void]$md.Add("")
[void]$md.Add("## Ranked Patch Tests")
[void]$md.Add("")
[void]$md.Add("| Priority | Target | Moby | Source lead | Why this first | Success signal |")
[void]$md.Add("| --- | --- | --- | --- | --- | --- |")
foreach ($test in $tests) {
    $source = if ([string](Get-Field $test.sourceLead "status" "") -eq "patch-testable") {
        "{0} {1} {2}" -f (Get-Field $test.sourceLead "wadRelativeWindow" ""), (Get-Field $test.sourceLead "axisSet" ""), (Format-LeadValues $test.sourceLead)
    } else {
        "none yet"
    }
    $typeHex = [string](Get-Field $test "typeHex" "")
    [void]$md.Add("| $($test.priority) | $($test.target) | $($test.moby) ``$typeHex`` | $source | $($test.purpose) | $($test.successSignal) |")
}
[void]$md.Add("")
[void]$md.Add("## Blocked Identity Targets")
[void]$md.Add("")
[void]$md.Add("| Target | Blocker | Required sample |")
[void]$md.Add("| --- | --- | --- |")
foreach ($blocked in $blockedTargets) {
    [void]$md.Add("| $($blocked.target) | $($blocked.blocker) | $($blocked.requiredSample) |")
}
[void]$md.Add("")
[void]$md.Add("## Public Layout Sources")
foreach ($source in $result.publicLayoutSources) {
    [void]$md.Add("- $source")
}
[void]$md.Add("")
[void]$md.Add("## Use In The Editor")
[void]$md.Add("- Start with **Load Stone Hill Workbench**.")
[void]$md.Add("- Move the listed moby a large visible distance.")
[void]$md.Add("- Use **Create Source BIN** first; use **Create Broad BIN** only when the best source lead does not visibly move the target.")
[void]$md.Add("- Fresh-load the generated CUE before judging collision or rewards. If the model moves but behavior does not, keep that source window and search nearby bytes for the linked behavior/content record.")

$md | Set-Content -LiteralPath $resolvedMarkdown -Encoding UTF8

Write-Host "Wrote Stone Hill patch-test matrix JSON to $resolvedJson"
Write-Host "Wrote Stone Hill patch-test matrix Markdown to $resolvedMarkdown"
