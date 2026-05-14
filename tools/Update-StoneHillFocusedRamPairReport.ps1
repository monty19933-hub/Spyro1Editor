param(
    [string]$ManifestPath = ".\stonehill-focused-ram-pairs.json",
    [string]$OutJsonPath = ".\stonehill-focused-ram-pair-report.json",
    [string]$OutMarkdownPath = ".\stonehill-focused-ram-pair-report.md",
    [string]$CompareToolPath = ".\tools\Compare-SpyroRamMobys.ps1",
    [switch]$Force
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

function Format-Indexes($Indexes) {
    $items = @($Indexes | Where-Object { $null -ne $_ } | ForEach-Object { [int]$_ } | Sort-Object -Unique)
    if ($items.Count -eq 0) { return "none" }
    return ($items | ForEach-Object { "L$_" }) -join ", "
}

function New-FocusedPair(
    [string]$Id,
    [string]$Target,
    [string]$Location,
    [string]$BeforePath,
    [string]$AfterPath,
    [string]$DiffPath,
    [int[]]$CandidateIndexes,
    [string]$ExpectedSignal,
    [string]$Resolves
) {
    return [ordered]@{
        id = $Id
        target = $Target
        location = $Location
        beforePath = $BeforePath
        afterPath = $AfterPath
        diffPath = $DiffPath
        candidateIndexes = @($CandidateIndexes)
        expectedSignal = $ExpectedSignal
        resolves = $Resolves
    }
}

function New-DefaultManifest {
    return [ordered]@{
        generatedAt = (Get-Date).ToString("s")
        purpose = "Focused Stone Hill before/after RAM pairs. Drop raw 2 MB main-RAM dumps at these paths, then rerun tools\\Update-StoneHillFocusedRamPairReport.ps1."
        captureRules = @(
            "Use a before dump immediately before the action and an after dump immediately after the game registers the action.",
            "Keep each pair isolated: do not collect another gem, free another dragon, open a chest, or kill another enemy between the before and after dump.",
            "After adding a pair, rerun this report; completed pairs can then feed Compare-SpyroRamMobys, Render-StoneHillMobyMap, source-link discovery, map-plan generation, and the target board."
        )
        pairs = @(
            (New-FocusedPair "gem-clean" "Known gem pickup" "Existing calibration sample" ".\stonehill-before-gem-clean.bin" ".\stonehill-after-gem-clean.bin" ".\stonehill-ram-gem-clean-diff.json" @(10, 21, 32, 43, 109, 120, 131, 186) "_globalGemCount increases by 1 and type 0x20 records/collectable flags change." "Keeps type 0x20 as the proven treasure/gem class.")
            (New-FocusedPair "astor-free" "Astor freed" "Castle / Return Home / Astor" ".\stonehill-before-astor-free.bin" ".\stonehill-after-astor-free.bin" ".\stonehill-ram-astor-free-diff.json" @(65, 186, 208, 398) "_globalDragonCount increases by 1; L65 or nearby route records should change." "Assigns or rejects L65 as Astor.")
            (New-FocusedPair "gavin-free" "Gavin freed" "Dry well / Gavin / locked chest" ".\stonehill-before-gavin-free.bin" ".\stonehill-after-gavin-free.bin" ".\stonehill-ram-gavin-free-diff.json" @(95, 183, 390, 399, 412, 413) "_globalDragonCount increases by 1; low-west well records should change." "Finds Gavin's runtime/source record or proves it is not in the current visible 0x30 pair.")
            (New-FocusedPair "lindar-free" "Lindar freed" "Left cave / Lindar route" ".\stonehill-before-lindar-free.bin" ".\stonehill-after-lindar-free.bin" ".\stonehill-ram-lindar-free-diff.json" @(54, 98, 109, 120, 153) "_globalDragonCount increases by 1; cave/tower route records should change." "Separates Lindar from the shared L54 Lindar/Gildas candidate.")
            (New-FocusedPair "gildas-free" "Gildas freed" "Tower / Gildas route" ".\stonehill-before-gildas-free.bin" ".\stonehill-after-gildas-free.bin" ".\stonehill-ram-gildas-free-diff.json" @(54, 87, 98, 142, 153) "_globalDragonCount increases by 1; tower/high-field route records should change." "Separates Gildas from the shared L54 Lindar/Gildas candidate.")
            (New-FocusedPair "hidden-key" "Hidden beach key picked up" "Hidden beach / key cave" ".\stonehill-before-hidden-key.bin" ".\stonehill-after-hidden-key.bin" ".\stonehill-ram-hidden-key-diff.json" @(421, 422) "Collectable flags change, likely without gem/dragon/egg counter movement." "Identifies the key or confirms the current beach markers are only route helpers.")
            (New-FocusedPair "locked-chest" "Locked chest opened" "Dry well / Gavin / locked chest" ".\stonehill-before-locked-chest.bin" ".\stonehill-after-locked-chest.bin" ".\stonehill-ram-locked-chest-diff.json" @(95, 183, 390, 399, 412, 413) "_globalGemCount jumps by the locked-chest value and low-west records change." "Splits the locked chest from Gavin/well helpers.")
            (New-FocusedPair "blue-thief" "Blue egg thief caught" "High fields / blue thief loop" ".\stonehill-before-blue-thief.bin" ".\stonehill-after-blue-thief.bin" ".\stonehill-ram-blue-thief-diff.json" @(87, 142, 405, 411) "_globalEggCount increases by 1; high-field active-object records should change." "Assigns the blue thief among L87/L142 and helper candidates.")
            (New-FocusedPair "ram-defeat" "One ram defeated" "High fields or start-side route" ".\stonehill-before-ram-defeat.bin" ".\stonehill-after-ram-defeat.bin" ".\stonehill-ram-defeat-diff.json" @(87, 98, 142, 153) "Enemy state changes and possibly spawned treasure/fodder side effects." "Splits ram identity from generic type 0x18 enemy/active candidates.")
            (New-FocusedPair "shepherd-defeat" "One shepherd defeated" "High fields or tower route" ".\stonehill-before-shepherd-defeat.bin" ".\stonehill-after-shepherd-defeat.bin" ".\stonehill-ram-shepherd-defeat-diff.json" @(87, 98, 142, 153) "Enemy state changes and possibly spawned treasure/fodder side effects." "Splits shepherd identity from generic type 0x18 enemy/active candidates.")
            (New-FocusedPair "sheep-fodder" "One sheep/fodder flamed" "High fields or start field" ".\stonehill-before-sheep-fodder.bin" ".\stonehill-after-sheep-fodder.bin" ".\stonehill-ram-sheep-fodder-diff.json" @(10, 21, 32, 87, 131, 142, 405, 411) "Fodder state changes, possibly health/butterfly state rather than collectable counters." "Finds whether fodder appears in the level moby table or a dynamic/special-data table.")
        )
    }
}

function Test-FileReady([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    return Test-Path -LiteralPath $Path
}

function Get-FileStamp([string]$Path) {
    if (-not (Test-FileReady $Path)) { return $null }
    $item = Get-Item -LiteralPath $Path
    return [ordered]@{
        path = $item.FullName
        length = [int64]$item.Length
        lastWriteTime = $item.LastWriteTime.ToString("s")
    }
}

function Get-DiffSummary($Diff, $CandidateIndexes) {
    $progress = Get-Field $Diff "progressDiff" $null
    $changes = @(Get-ArrayField $Diff "changes")
    $candidateSet = @{}
    foreach ($index in @($CandidateIndexes)) { $candidateSet[[int]$index] = $true }
    $candidateChanges = @($changes | Where-Object { $candidateSet.ContainsKey([int](Get-Field $_ "index" -1)) })
    $fieldChanges = @($changes | Where-Object { @(Get-ArrayField $_ "fieldChanges").Count -gt 0 })
    $counterChanges = if ($null -ne $progress) { @(Get-ArrayField $progress "counterChanges") } else { @() }

    return [ordered]@{
        changedRecordCount = [int](Get-Field $Diff "changedRecordCount" $changes.Count)
        addedRecordCount = [int](Get-Field $Diff "addedRecordCount" 0)
        interpretation = if ($null -ne $progress) { [string](Get-Field $progress "interpretation" "") } else { "" }
        counterChanges = @($counterChanges | ForEach-Object {
            [ordered]@{
                name = [string](Get-Field $_ "name" "")
                before = Get-Field $_ "before" $null
                after = Get-Field $_ "after" $null
                delta = Get-Field $_ "delta" $null
            }
        })
        collectableFlagChangeCount = if ($null -ne $progress) { [int](Get-Field $progress "collectableFlagChangeCount" 0) } else { 0 }
        candidateChangedIndexes = @($candidateChanges | ForEach-Object { [int](Get-Field $_ "index" -1) } | Sort-Object -Unique)
        topChangedIndexes = @($changes | Select-Object -First 12 | ForEach-Object { [int](Get-Field $_ "index" -1) })
        fieldChangedIndexes = @($fieldChanges | Select-Object -First 12 | ForEach-Object { [int](Get-Field $_ "index" -1) })
    }
}

$resolvedManifestPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ManifestPath)
if (-not (Test-Path -LiteralPath $resolvedManifestPath)) {
    $manifest = New-DefaultManifest
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedManifestPath -Encoding UTF8
}

$manifest = Get-Content -Raw -LiteralPath $resolvedManifestPath | ConvertFrom-Json
$pairs = @(Get-ArrayField $manifest "pairs")
$resolvedCompare = Resolve-Path -LiteralPath $CompareToolPath

$pairReports = @()
foreach ($pair in $pairs) {
    $beforePath = [string](Get-Field $pair "beforePath" "")
    $afterPath = [string](Get-Field $pair "afterPath" "")
    $diffPath = [string](Get-Field $pair "diffPath" "")
    $candidateIndexes = @(Get-ArrayField $pair "candidateIndexes" | ForEach-Object { [int]$_ })
    $beforeReady = Test-FileReady $beforePath
    $afterReady = Test-FileReady $afterPath
    $diffReady = Test-FileReady $diffPath
    $status = "missing"
    $summary = $null
    $missingFiles = @()
    if (-not $beforeReady) { $missingFiles += $beforePath }
    if (-not $afterReady) { $missingFiles += $afterPath }

    if ($beforeReady -and $afterReady) {
        if ($Force -or -not $diffReady) {
            & $resolvedCompare.Path -BeforePath $beforePath -AfterPath $afterPath -OutPath $diffPath | Out-Host
            $diffReady = Test-FileReady $diffPath
        }
        if ($diffReady) {
            $diff = Get-Content -Raw -LiteralPath $diffPath | ConvertFrom-Json
            $summary = Get-DiffSummary $diff $candidateIndexes
            $status = "compared"
        }
        else {
            $status = "pair-ready-diff-missing"
        }
    }
    elseif ($beforeReady -or $afterReady) {
        $status = "incomplete-pair"
    }

    $pairReports += [ordered]@{
        id = [string](Get-Field $pair "id" "")
        target = [string](Get-Field $pair "target" "")
        location = [string](Get-Field $pair "location" "")
        status = $status
        before = Get-FileStamp $beforePath
        after = Get-FileStamp $afterPath
        diffPath = $diffPath
        diffExists = $diffReady
        missingFiles = $missingFiles
        candidateIndexes = $candidateIndexes
        expectedSignal = [string](Get-Field $pair "expectedSignal" "")
        resolves = [string](Get-Field $pair "resolves" "")
        summary = $summary
    }
}

$completed = @($pairReports | Where-Object { [string](Get-Field $_ "status" "") -eq "compared" })
$partial = @($pairReports | Where-Object { [string](Get-Field $_ "status" "") -eq "incomplete-pair" })
$missing = @($pairReports | Where-Object { [string](Get-Field $_ "status" "") -eq "missing" })

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    manifestPath = $resolvedManifestPath
    compareToolPath = $resolvedCompare.Path
    statusSummary = [ordered]@{
        totalPairs = $pairReports.Count
        compared = $completed.Count
        incomplete = $partial.Count
        missing = $missing.Count
    }
    captureRules = @(Get-ArrayField $manifest "captureRules")
    pairs = $pairReports
    nextMissingPairs = @($pairReports | Where-Object { [string](Get-Field $_ "status" "") -ne "compared" } | Select-Object -First 8)
}

$resolvedOutJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# Stone Hill Focused RAM Pair Report")
[void]$md.Add("")
[void]$md.Add("Generated: $($report.generatedAt)")
[void]$md.Add("")
[void]$md.Add("Compared pairs: $($completed.Count) / $($pairReports.Count). Incomplete: $($partial.Count). Missing: $($missing.Count).")
[void]$md.Add("")
[void]$md.Add("## Capture Rules")
foreach ($rule in @(Get-ArrayField $report "captureRules")) {
    [void]$md.Add("- $([string]$rule)")
}
[void]$md.Add("")
[void]$md.Add("## Pair Status")
[void]$md.Add("")
[void]$md.Add("| Pair | Target | Location | Status | Candidate mobys | Signal / result |")
[void]$md.Add("| --- | --- | --- | --- | --- | --- |")
foreach ($pair in $pairReports) {
    $status = [string](Get-Field $pair "status" "")
    $signal = [string](Get-Field $pair "expectedSignal" "")
    $summary = Get-Field $pair "summary" $null
    if ($null -ne $summary) {
        $counterText = @((Get-ArrayField $summary "counterChanges") | ForEach-Object {
            "$([string](Get-Field $_ 'name' ''))=$([string](Get-Field $_ 'before' ''))->$([string](Get-Field $_ 'after' ''))"
        })
        $candidateText = Format-Indexes (Get-ArrayField $summary "candidateChangedIndexes")
        $fieldText = Format-Indexes (Get-ArrayField $summary "fieldChangedIndexes")
        $signal = "changed=$([int](Get-Field $summary 'changedRecordCount' 0)), flags=$([int](Get-Field $summary 'collectableFlagChangeCount' 0)), counters=$($counterText -join ', '), candidate changes=$candidateText, field changes=$fieldText"
    }
    elseif (@(Get-ArrayField $pair "missingFiles").Count -gt 0) {
        $signal = "Missing: $((@(Get-ArrayField $pair 'missingFiles')) -join ', ')"
    }
    $line = "| $([string](Get-Field $pair 'id' '')) | $([string](Get-Field $pair 'target' '')) | $([string](Get-Field $pair 'location' '')) | $status | $(Format-Indexes (Get-ArrayField $pair 'candidateIndexes')) | $($signal.Replace('|', '/')) |"
    [void]$md.Add($line)
}

$resolvedOutMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
$md | Set-Content -LiteralPath $resolvedOutMarkdown -Encoding UTF8

Write-Host "Wrote focused RAM pair manifest to $resolvedManifestPath"
Write-Host "Wrote focused RAM pair JSON report to $resolvedOutJson"
Write-Host "Wrote focused RAM pair Markdown report to $resolvedOutMarkdown"
Write-Host "Compared pairs: $($completed.Count) / $($pairReports.Count); incomplete: $($partial.Count); missing: $($missing.Count)"
