param(
    [string]$DependencyTracePath = "",
    [string]$TemplatePath = ".\spyro-object-templates.json",
    [string]$OutJsonPath = ".\spyro-cross-level-object-import-plan.json",
    [string]$OutMarkdownPath = ".\spyro-cross-level-object-import-plan.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Find-DefaultTracePath {
    $rootTrace = Join-Path $WorkspaceRoot "spyro-object-dependency-trace.json"
    if (Test-Path -LiteralPath $rootTrace) { return $rootTrace }

    $localRoot = Join-Path $WorkspaceRoot "_local"
    if (-not (Test-Path -LiteralPath $localRoot)) { return "" }

    $matches = @(Get-ChildItem -LiteralPath $localRoot -Recurse -Filter "spyro-object-dependency-trace.json" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending)
    if ($matches.Count -gt 0) { return $matches[0].FullName }
    return ""
}

function Convert-HexOrInt64($Value) {
    if ($null -eq $Value) { return 0L }
    if ($Value -is [string]) {
        $text = $Value.Trim()
        if ($text.Length -eq 0) { return 0L }
        if ($text.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
            return [Convert]::ToInt64($text.Substring(2), 16)
        }
        return [Convert]::ToInt64($text, 10)
    }
    return [Convert]::ToInt64($Value)
}

function Get-Value($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $Default }
    if ($null -eq $prop.Value) { return $Default }
    return $prop.Value
}

function Get-HitCount($Object) {
    return [int](Get-Value $Object "hitCount" 0)
}

function Test-ZeroPointer($Value) {
    return (Convert-HexOrInt64 $Value) -eq 0
}

function New-ReasonList {
    return New-Object System.Collections.Generic.List[string]
}

function Get-TemplateById($TemplateRoot, [string]$TemplateId) {
    if ($null -eq $TemplateRoot -or $null -eq $TemplateRoot.templates) { return $null }
    foreach ($template in @($TemplateRoot.templates)) {
        if ([string](Get-Value $template "id" "") -eq $TemplateId) { return $template }
    }
    return $null
}

function Classify-Trace($Trace, $Template) {
    $family = ([string](Get-Value $Trace "family" "")).Trim()
    $displayName = [string](Get-Value $Trace "displayName" $family)
    $templateId = [string](Get-Value $Trace "templateId" "")

    $actorTable = Get-Value $Trace "actorPointerTable" $null
    $runtimeActorPointer = Get-Value $actorTable "targetRuntimeActorPointer" ""
    $sourceActorPointer = Get-Value $actorTable "targetSourceActorPointer" ""
    $runtimeActorId = Get-Value $actorTable "runtimeActorId16" ""
    $sourceActorId = Get-Value $actorTable "sourceActorId16" ""
    $actorPointer = if (-not [string]::IsNullOrWhiteSpace([string]$runtimeActorPointer)) { $runtimeActorPointer } else { $sourceActorPointer }
    $actorId = if (-not [string]::IsNullOrWhiteSpace([string]$runtimeActorId)) { $runtimeActorId } else { $sourceActorId }
    $actorSlotZero = Test-ZeroPointer $actorPointer

    $donorRuntime = Get-Value $Trace "donorRuntime" $null
    $runtimeSpecialPointer = Get-Value $donorRuntime "specialDataPointer" "0x00000000"
    $hasRuntimeSpecialPointer = -not (Test-ZeroPointer $runtimeSpecialPointer)

    $patternPresence = Get-Value $Trace "patternPresence" $null
    $targetRam = Get-Value $patternPresence "targetRam" $null
    $runtimeSpecialPrefixHits = Get-HitCount (Get-Value $targetRam "runtimeSpecialPrefix" $null)
    $runtimeRecordHits = Get-HitCount (Get-Value $targetRam "runtimeRecord" $null)
    $runtimeIdentityTailHits = Get-HitCount (Get-Value $targetRam "runtimeIdentityTail" $null)

    $missing = @()
    $missingProp = Get-Value $Trace "likelyMissingDependencies" @()
    if ($null -ne $missingProp) { $missing = @($missingProp) }

    $missingSecondary = @($missing | Where-Object {
        $_ -match "secondary dependency" -or $_ -match "runtime special-data child"
    })

    $reasons = New-ReasonList
    $requiredFeature = "None"
    $supportStatus = "candidate-needs-game-test"
    $canExportNow = $false
    $nextStep = "Test one disposable BIN/CUE before promoting this template."

    if (-not $hasRuntimeSpecialPointer -and -not $actorSlotZero) {
        $supportStatus = "supported-lightweight-object"
        $canExportNow = $true
        $requiredFeature = "DirectSourceRecordAppend"
        $nextStep = "Keep this path as the control case: copy source record, apply known loader identity transform, and append it."
        [void]$reasons.Add("Target actor pointer slot is populated.")
        [void]$reasons.Add("Donor has no runtime special-data pointer.")
    }
    elseif ($actorSlotZero) {
        $supportStatus = "blocked-missing-target-actor-package"
        $requiredFeature = "ActorPackageImportAndPointerRebase"
        $nextStep = "Map the donor actor package from the donor level WAD, copy the complete package into target level storage, rebase internal pointers, then populate the target actor pointer slot."
        [void]$reasons.Add("Target actor pointer slot for this actor id is zero.")
        if ($hasRuntimeSpecialPointer) {
            [void]$reasons.Add("Donor uses runtime special data, so the moby row alone cannot create the object.")
        }
    }
    elseif ($hasRuntimeSpecialPointer -and $runtimeSpecialPrefixHits -le 0) {
        $supportStatus = "blocked-missing-runtime-special-data"
        $requiredFeature = "RuntimeSpecialDataImportAndRebase"
        $nextStep = "Copy and rebase the donor runtime special-data cluster before appending the moby row."
        [void]$reasons.Add("Target actor slot exists, but the donor runtime special-data block is absent from target RAM.")
    }
    elseif ($missingSecondary.Count -gt 0) {
        $supportStatus = "blocked-missing-secondary-dependencies"
        $requiredFeature = "DependencyClusterImportAndRebase"
        $nextStep = "Copy and rebase the donor secondary dependency blocks and child blocks."
        [void]$reasons.Add("Trace found missing secondary/child dependency blocks.")
    }
    else {
        $requiredFeature = "TargetedRuntimeValidation"
        [void]$reasons.Add("Trace did not find a hard missing actor package, but runtime validation is still required.")
    }

    $testedStatus = [string](Get-Value $Template "testedStatus" "")
    if ($testedStatus -like "blocked*") {
        [void]$reasons.Add("Object template is already marked blocked by in-game testing: $testedStatus.")
    }

    return [pscustomobject]([ordered]@{
        templateId = $templateId
        family = $family
        displayName = $displayName
        sourceLevel = Get-Value (Get-Value $Trace "sourceLevel" $null) "displayName" ""
        sourceTrueIndex = Get-Value (Get-Value $Trace "donorSource" $null) "trueIndex" -1
        actorId16 = $actorId
        targetActorPointer = $actorPointer
        targetActorPointerIsZero = [bool]$actorSlotZero
        runtimeSpecialDataPointer = $runtimeSpecialPointer
        hasRuntimeSpecialDataPointer = [bool]$hasRuntimeSpecialPointer
        targetRuntimeSpecialPrefixHits = $runtimeSpecialPrefixHits
        targetRuntimeRecordHits = $runtimeRecordHits
        targetRuntimeIdentityTailHits = $runtimeIdentityTailHits
        missingDependencyCount = @($missing).Count
        missingRuntimeDependencyCount = @($missingSecondary).Count
        canExportNow = [bool]$canExportNow
        supportStatus = $supportStatus
        requiredExporterFeature = $requiredFeature
        nextStep = $nextStep
        reasons = @($reasons)
        traceRecommendation = [string](Get-Value $Trace "recommendation" "")
        templateTestedStatus = $testedStatus
        templateTestedResult = [string](Get-Value $Template "testedResult" "")
    })
}

$resolvedTracePath = Resolve-WorkspacePath $DependencyTracePath
if ([string]::IsNullOrWhiteSpace($resolvedTracePath)) {
    $resolvedTracePath = Find-DefaultTracePath
}
if ([string]::IsNullOrWhiteSpace($resolvedTracePath) -or -not (Test-Path -LiteralPath $resolvedTracePath)) {
    throw "Missing dependency trace JSON. Run tools\New-SpyroObjectDependencyTrace.ps1 first."
}

$resolvedTemplatePath = Resolve-WorkspacePath $TemplatePath
if (-not (Test-Path -LiteralPath $resolvedTemplatePath)) {
    throw "Missing object template file: $resolvedTemplatePath"
}

$resolvedOutJsonPath = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath

$traceRoot = Get-Content -LiteralPath $resolvedTracePath -Raw | ConvertFrom-Json
$templateRoot = Get-Content -LiteralPath $resolvedTemplatePath -Raw | ConvertFrom-Json

$analyses = New-Object System.Collections.Generic.List[object]
foreach ($trace in @($traceRoot.traces)) {
    $template = Get-TemplateById $templateRoot ([string](Get-Value $trace "templateId" ""))
    [void]$analyses.Add((Classify-Trace $trace $template))
}

$blocked = @($analyses | Where-Object { -not $_.canExportNow })
$supported = @($analyses | Where-Object { $_.canExportNow })

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "New-SpyroCrossLevelObjectImportPlan.ps1"
    dependencyTracePath = $resolvedTracePath
    templatePath = $resolvedTemplatePath
    targetLevel = (Get-Value (Get-Value $traceRoot "targetLevel" $null) "displayName" "")
    summary = [ordered]@{
        supportedNow = $supported.Count
        blockedNow = $blocked.Count
        mainIssue = "Locked/spring chest cross-level adds are blocked because the target level does not load their actor package. The fix is actor package import plus pointer rebasing, not more moby-row copying."
    }
    requiredFix = [ordered]@{
        step1 = "Identify the donor actor package for the actor id from the donor actor pointer table."
        step2 = "Map every package-local runtime pointer used by the moby special-data block, secondary dependency block, and child blocks back to donor WAD offsets."
        step3 = "Allocate target storage only in verified unused level-local space, then copy the complete package and dependency cluster."
        step4 = "Rebase internal 0x80xxxxxx pointers from donor RAM addresses to the target RAM address range."
        step5 = "Patch the target actor pointer table/root references, then append the source moby row and special data."
        step6 = "Promote the template from blocked to addable only after a disposable BIN/CUE passes an in-game load and pickup/open test."
    }
    analyses = $analyses.ToArray()
}

$json = $result | ConvertTo-Json -Depth 8
Set-Content -LiteralPath $resolvedOutJsonPath -Value $json -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Spyro Cross-Level Object Import Plan")
[void]$lines.Add("")
[void]$lines.Add("Target: $($result.targetLevel)")
[void]$lines.Add("")
[void]$lines.Add("Main issue: $($result.summary.mainIssue)")
[void]$lines.Add("")
[void]$lines.Add("| Object | Actor | Target actor pointer | Runtime special data | Status | Required exporter feature |")
[void]$lines.Add("| --- | --- | --- | --- | --- | --- |")
foreach ($analysis in $analyses) {
    [void]$lines.Add("| $($analysis.displayName) | $($analysis.actorId16) | $($analysis.targetActorPointer) | $($analysis.runtimeSpecialDataPointer) | $($analysis.supportStatus) | $($analysis.requiredExporterFeature) |")
}
[void]$lines.Add("")
[void]$lines.Add("## Fix Path")
[void]$lines.Add("")
foreach ($key in $result.requiredFix.Keys) {
    [void]$lines.Add("- $($result.requiredFix[$key])")
}
[void]$lines.Add("")
[void]$lines.Add("## Details")
[void]$lines.Add("")
foreach ($analysis in $analyses) {
    [void]$lines.Add("### $($analysis.displayName)")
    [void]$lines.Add("")
    [void]$lines.Add("- Support status: $($analysis.supportStatus)")
    [void]$lines.Add("- Can export now: $($analysis.canExportNow)")
    [void]$lines.Add("- Next step: $($analysis.nextStep)")
    foreach ($reason in @($analysis.reasons)) {
        [void]$lines.Add("- Reason: $reason")
    }
    if (-not [string]::IsNullOrWhiteSpace($analysis.templateTestedResult)) {
        [void]$lines.Add("- In-game test result: $($analysis.templateTestedResult)")
    }
    [void]$lines.Add("")
}

Set-Content -LiteralPath $resolvedOutMarkdownPath -Value $lines -Encoding UTF8

Write-Host "Wrote $resolvedOutJsonPath"
Write-Host "Wrote $resolvedOutMarkdownPath"
