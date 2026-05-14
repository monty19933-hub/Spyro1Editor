param(
    [string]$AuditPath = ".\stonehill-moby-identity-audit.json",
    [string]$SpecialDataPath = ".\stonehill-moby-special-data.json",
    [string]$OutJsonPath = ".\stonehill-behavior-links.json",
    [string]$OutMarkdownPath = ".\stonehill-behavior-links.md"
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

function Remove-IdentitySuffix([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return "" }
    $clean = $Text.Trim()
    foreach ($suffix in @(
        " \(safe-ground observed\)",
        " \(wide-row observed\)",
        " \(raised-row observed\)",
        " \(single-row observed\)",
        " \(live-observed\)",
        " \(live-tested\)",
        " \(live-confirmed\)",
        " \(signature-confirmed\)",
        " \(observed\)"
    )) {
        $clean = $clean -replace "$suffix$", ""
    }
    return ($clean -replace "\s+", " ").Trim()
}

function Get-RecordText($Record) {
    return (([string](Get-Field $Record "label" "")) + " " +
        ([string](Get-Field $Record "kind" "")) + " " +
        ([string](Get-Field $Record "zone" "")) + " " +
        ([string](Get-Field $Record "evidence" ""))).ToLowerInvariant()
}

function Get-MemberSummary($Record) {
    [ordered]@{
        id = "T$([int](Get-Field $Record "trueIndex" -1))"
        trueIndex = [int](Get-Field $Record "trueIndex" -1)
        label = Remove-IdentitySuffix ([string](Get-Field $Record "label" ""))
        kind = Remove-IdentitySuffix ([string](Get-Field $Record "kind" ""))
        x = [double](Get-Field $Record "x" 0)
        y = [double](Get-Field $Record "y" 0)
        z = [double](Get-Field $Record "z" 0)
        specialDataPointer = [string](Get-Field $Record "specialDataPointer" "")
    }
}

function Get-MemberSignature([int[]]$Indexes) {
    return (@($Indexes | Sort-Object) -join ",")
}

function Test-IndexesCoveredBySeenGroup([hashtable]$SeenSignatures, [int[]]$Indexes) {
    foreach ($signature in $SeenSignatures.Keys) {
        $seen = @($signature -split "," | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { [int]$_ })
        $covered = $true
        foreach ($index in $Indexes) {
            if ($seen -notcontains $index) {
                $covered = $false
                break
            }
        }
        if ($covered) { return $true }
    }
    return $false
}

function Add-LinkGroup(
    [System.Collections.ArrayList]$Groups,
    [hashtable]$SeenSignatures,
    [hashtable]$RecordByTrueIndex,
    [string]$Key,
    [string]$Name,
    [int[]]$TrueIndexes,
    [bool]$LinkedMove,
    [string]$Confidence,
    [string]$Basis,
    [string]$Reason,
    [string]$Caution = ""
) {
    $existingRecords = @()
    foreach ($index in @($TrueIndexes | Sort-Object -Unique)) {
        if ($RecordByTrueIndex.ContainsKey($index)) {
            $existingRecords += $RecordByTrueIndex[$index]
        }
    }
    if ($existingRecords.Count -lt 2) { return }

    $resolvedIndexes = @($existingRecords | ForEach-Object { [int](Get-Field $_ "trueIndex" -1) } | Sort-Object)
    $signature = Get-MemberSignature $resolvedIndexes
    if ($SeenSignatures.ContainsKey($signature)) { return }
    $SeenSignatures[$signature] = $true

    $members = @($existingRecords | Sort-Object { [int](Get-Field $_ "trueIndex" -1) } | ForEach-Object { Get-MemberSummary $_ })
    $group = [ordered]@{
        key = $Key
        name = $Name
        linkedMove = $LinkedMove
        confidence = $Confidence
        basis = $Basis
        reason = $Reason
        caution = $Caution
        trueIndexes = $resolvedIndexes
        memberIds = @($resolvedIndexes | ForEach-Object { "T$_" })
        members = $members
    }
    [void]$Groups.Add($group)
}

$resolvedAudit = Resolve-WorkspacePath $AuditPath
$resolvedSpecialData = Resolve-WorkspacePath $SpecialDataPath
$resolvedOutJson = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMarkdown = Resolve-WorkspacePath $OutMarkdownPath
if (-not (Test-Path -LiteralPath $resolvedAudit)) { throw "Missing audit file: $resolvedAudit" }

$audit = Get-Content -Raw -LiteralPath $resolvedAudit | ConvertFrom-Json
$records = @(Get-ArrayField $audit "records")
$recordByTrueIndex = @{}
foreach ($record in $records) {
    $recordByTrueIndex[[int](Get-Field $record "trueIndex" -1)] = $record
}

$groups = New-Object System.Collections.ArrayList
$seenSignatures = @{}

Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
    "behavior:dragon-platform-main" `
    "Behavior: Dragon platform T86/T140/T185" `
    @(86, 140, 185) `
    $true `
    "confirmed" `
    "live-observed dragon, pedestal, and nonvisual control at one platform" `
    "Move the dragon actor/model, pedestal, and local control record together."

Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
    "behavior:return-home-platform-trigger" `
    "Behavior: Return Home platform + sound trigger" `
    @(177, 179) `
    $true `
    "observed" `
    "co-located return platform and portal sound trigger" `
    "Move the visible Return Home platform with its nearby trigger."

Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
    "behavior:dragon-pedestal-stack-east" `
    "Behavior: Dragon pedestal stack T141/T142/T186" `
    @(141, 142, 186) `
    $true `
    "strong-inferred" `
    "two dragon pedestal records and one nonvisual control share the same X/Y" `
    "Move both pedestal records and the local control record together; the matching dragon actor still needs confirmation."

Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
    "behavior:flag-doublet-central" `
    "Behavior: Co-located flag pair T42/T43" `
    @(42, 43) `
    $true `
    "strong-inferred" `
    "two flag/scenery records occupy the same X/Y" `
    "Move both flag records together so the visual stack stays intact."

Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
    "review:dragon-life-control-stack" `
    "Review: Dragon/life-chest/control stack T143/T144/T187" `
    @(143, 144, 187) `
    $false `
    "ambiguous" `
    "dragon actor, life chest, and nonvisual control share the same X/Y" `
    "This is a real spatial stack, but the life chest makes the behavior relationship uncertain." `
    "Use group selection to inspect; do not linked-move automatically yet."

Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
    "review:well-whirlwind-dragon-control" `
    "Review: Dry-well whirlwind/dragon/control neighborhood" `
    @(70, 170, 188) `
    $false `
    "nearby-inferred" `
    "delayed well whirlwind, dragon actor/model, and nonvisual control are within the dry-well cluster" `
    "Likely related to well/Gavin behavior, but not tight enough for automatic linked move." `
    "Needs a focused move test."

Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
    "review:dragon-gem-control-stack" `
    "Review: Dragon/gem/control stack T169/T170/T188" `
    @(169, 170, 188) `
    $false `
    "ambiguous" `
    "collected gem, dragon actor/model, and nonvisual control share the same X/Y" `
    "The exact stack is suspicious, but the gem may be an unrelated collected/reward state." `
    "Needs a focused move test."

Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
    "review:tree-control-stack" `
    "Review: Tree with nonvisual control stack T75/T180-T184" `
    @(75, 180, 181, 182, 183, 184) `
    $false `
    "ambiguous" `
    "one tree prop and five nonvisual controls share the same X/Y" `
    "This is likely a control stack near a prop, but the controls may not belong to the tree." `
    "Use as an inspection group only."

Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
    "review:system-trigger-flag" `
    "Review: System trigger near flag T166/T196" `
    @(166, 196) `
    $false `
    "nearby-inferred" `
    "system-or-trigger record is very close to a flag/scenery record" `
    "Potential local trigger relation; needs testing before linked move." `
    "Use as an inspection group only."

Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
    "review:system-trigger-chest-fodder" `
    "Review: System trigger near chest/fodder T195/T176/T74/T73" `
    @(195, 176, 74, 73) `
    $false `
    "nearby-inferred" `
    "system/control records sit near chest and fodder records" `
    "Spatially clustered, but behavior ownership is not known." `
    "Use as an inspection group only."

$xyGroups = @{}
foreach ($record in $records) {
    $x = [double](Get-Field $record "x" 0)
    $y = [double](Get-Field $record "y" 0)
    $key = "{0:F3},{1:F3}" -f $x, $y
    if (-not $xyGroups.ContainsKey($key)) { $xyGroups[$key] = @() }
    $xyGroups[$key] += $record
}

foreach ($entry in $xyGroups.GetEnumerator() | Sort-Object Name) {
    $members = @($entry.Value)
    if ($members.Count -lt 2) { continue }
    $indexes = @($members | ForEach-Object { [int](Get-Field $_ "trueIndex" -1) } | Sort-Object)
    if (Test-IndexesCoveredBySeenGroup $seenSignatures $indexes) { continue }
    $labels = @($members | Sort-Object { [int](Get-Field $_ "trueIndex" -1) } | ForEach-Object { Remove-IdentitySuffix ([string](Get-Field $_ "label" "")) })
    $name = "Review: co-located " + ((@($indexes | ForEach-Object { "T$_" })) -join "/")
    $reason = "Same X/Y position. Labels: " + (($labels | Select-Object -First 4) -join "; ")
    Add-LinkGroup $groups $seenSignatures $recordByTrueIndex `
        ("review:co-located:" + ($indexes -join "-")) `
        $name `
        $indexes `
        $false `
        "co-located" `
        "automatic exact X/Y overlap scan" `
        $reason `
        "Review before enabling linked move."
}

$sharedPointers = @()
if (Test-Path -LiteralPath $resolvedSpecialData) {
    $pointerGroups = @((Get-Content -Raw -LiteralPath $resolvedSpecialData | ConvertFrom-Json).pointerGroups)
    foreach ($pointerGroup in $pointerGroups) {
        $pointer = [string](Get-Field $pointerGroup "specialDataPointer" "")
        $count = [int](Get-Field $pointerGroup "count" 0)
        if ($count -lt 2) { continue }
        $sharedPointers += [ordered]@{
            specialDataPointer = $pointer
            count = $count
            note = "Shared special-data pointers appear to be class/descriptor signals in Stone Hill, not direct move links by themselves."
            mobys = @(Get-ArrayField $pointerGroup "mobys")
            labels = @(Get-ArrayField $pointerGroup "labels")
        }
    }
}

$linkedCount = @($groups | Where-Object { $_.linkedMove }).Count
$reviewCount = @($groups | Where-Object { -not $_.linkedMove }).Count
$root = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Stone Hill behavior-link pass. linkedMove=true groups are conservative editor move links; linkedMove=false groups are inspection/review links."
    auditPath = (Resolve-Path -LiteralPath $resolvedAudit).Path
    summary = [ordered]@{
        totalGroups = $groups.Count
        linkedMoveGroups = $linkedCount
        reviewGroups = $reviewCount
        sharedSpecialDataPointerSignals = $sharedPointers.Count
    }
    linkGroups = @($groups.ToArray())
    signals = [ordered]@{
        sharedSpecialDataPointers = $sharedPointers
    }
}

$root | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Stone Hill Behavior Links")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($root.generatedAt)")
[void]$lines.Add("")
[void]$lines.Add("`linkedMove=true` groups are wired for linked moving in the native editor. Review groups are useful for inspection and cycling, but should not move together until tested.")
[void]$lines.Add("")
[void]$lines.Add("## Linked Move Groups")
[void]$lines.Add("")
[void]$lines.Add("| Group | Members | Confidence | Reason |")
[void]$lines.Add("|---|---|---|---|")
foreach ($group in @($groups | Where-Object { $_.linkedMove })) {
    [void]$lines.Add("| $($group.name) | $($group.memberIds -join ', ') | $($group.confidence) | $($group.reason.Replace('|','\|')) |")
}
[void]$lines.Add("")
[void]$lines.Add("## Review Groups")
[void]$lines.Add("")
[void]$lines.Add("| Group | Members | Confidence | Caution |")
[void]$lines.Add("|---|---|---|---|")
foreach ($group in @($groups | Where-Object { -not $_.linkedMove })) {
    [void]$lines.Add("| $($group.name) | $($group.memberIds -join ', ') | $($group.confidence) | $($group.caution.Replace('|','\|')) |")
}
[void]$lines.Add("")
[void]$lines.Add("## Shared Special-Data Pointer Signals")
[void]$lines.Add("")
[void]$lines.Add("These are not linked-move groups by themselves. Large shared pointer groups mostly identify object classes or descriptors.")
[void]$lines.Add("")
[void]$lines.Add("| Pointer | Count | Labels |")
[void]$lines.Add("|---|---:|---|")
foreach ($signal in $sharedPointers) {
    [void]$lines.Add("| $($signal.specialDataPointer) | $($signal.count) | $((@($signal.labels) -join ', ').Replace('|','\|')) |")
}

$lines | Set-Content -LiteralPath $resolvedOutMarkdown -Encoding UTF8

Write-Host "Wrote $resolvedOutJson"
Write-Host "Wrote $resolvedOutMarkdown"
Write-Host "Linked move groups: $linkedCount"
Write-Host "Review groups: $reviewCount"
