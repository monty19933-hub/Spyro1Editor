param(
    [string]$CatalogPath = ".\stonehill-moby-catalog.json",
    [string]$SourceLinksPath = ".\stonehill-moby-source-links.json",
    [string]$OutPath = ".\stonehill-replica-checklist.json"
)

Set-StrictMode -Version 2.0

function Get-ArrayField($Object, [string]$Name) {
    if ($null -eq $Object) { return @() }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return @() }
    return @($property.Value)
}

function Convert-MobySummary($Moby) {
    return [ordered]@{
        index = [int]$Moby.index
        runtimeAddress = [string]$Moby.runtimeAddress
        typeHex = [string]$Moby.typeHex
        stateHex = [string]$Moby.stateHex
        x = [double]$Moby.x
        y = [double]$Moby.y
        z = [double]$Moby.z
        confidence = [string]$Moby.confidence
        candidateKind = [string]$Moby.candidateKind
        evidence = [string]$Moby.evidence
    }
}

function Get-SourceLead($SourceLinks, [int]$Index) {
    if ($null -eq $SourceLinks) { return $null }
    $record = @(Get-ArrayField $SourceLinks "records" | Where-Object { [int]$_.index -eq $Index } | Select-Object -First 1)
    if ($record.Count -eq 0) { return $null }
    $record = $record[0]
    $bestWindow = @(Get-ArrayField $record "scaledAxisWindows" | Select-Object -First 1)
    $exact = @(Get-ArrayField $record "exactMatches")
    return [ordered]@{
        exactMatchKinds = @($exact | ForEach-Object { "$($_.kind):$($_.count)" })
        bestScaledWindow = $(if ($bestWindow.Count -gt 0) {
            [ordered]@{
                assetRelativeWindow = [string]$bestWindow[0].assetRelativeWindow
                assetSubfileIndex = [int]$bestWindow[0].assetSubfileIndex
                axisCount = [int]$bestWindow[0].axisCount
                spread = [int]$bestWindow[0].spread
                values = @(Get-ArrayField $bestWindow[0] "values")
                typeByteOffsets = @(Get-ArrayField $bestWindow[0] "typeByteOffsets")
                stateByteOffsets = @(Get-ArrayField $bestWindow[0] "stateByteOffsets")
            }
        } else { $null })
    }
}

$catalog = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $CatalogPath).Path | ConvertFrom-Json
$sourceLinks = $null
if (Test-Path -LiteralPath $SourceLinksPath) {
    $sourceLinks = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $SourceLinksPath).Path | ConvertFrom-Json
}

$mobys = @(Get-ArrayField $catalog "mobys")
$progressDiff = $null
if ($null -ne $catalog.PSObject.Properties["progressDiff"]) {
    $progressDiff = $catalog.progressDiff
}
$gemCandidates = @($mobys | Where-Object { [string]$_.typeHex -eq "0x20" })
$dragonCandidates = @($mobys | Where-Object { [string]$_.typeHex -eq "0x30" })
$enemyCandidates = @($mobys | Where-Object { [string]$_.typeHex -eq "0x18" })
$unknownPlacementCandidates = @($mobys | Where-Object {
    [string]$_.confidence -eq "unknown" -and -not ([int]$_.rawX -eq 0 -and [int]$_.rawY -eq 4096 -and [int]$_.rawZ -eq 0)
})

$gemWithSourceLeads = @($gemCandidates | Where-Object {
    $lead = Get-SourceLead $sourceLinks ([int]$_.index)
    $null -ne $lead -and $null -ne $lead.bestScaledWindow
})
$dragonWithSourceLeads = @($dragonCandidates | Where-Object {
    $lead = Get-SourceLead $sourceLinks ([int]$_.index)
    $null -ne $lead -and $null -ne $lead.bestScaledWindow
})
$enemyWithSourceLeads = @($enemyCandidates | Where-Object {
    $lead = Get-SourceLead $sourceLinks ([int]$_.index)
    $null -ne $lead -and $null -ne $lead.bestScaledWindow
})

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    note = "Stone Hill replica checklist. Public content targets are stable game-guide targets; current decoded counts are RAM/WAD evidence, not full level proof yet."
    publicTargets = [ordered]@{
        treasureValue = 200
        dragons = @(
            [ordered]@{ name = "Lindar"; publicLocation = "Cave with gems and chests." },
            [ordered]@{ name = "Gildas"; publicLocation = "Top of the tower near the blue thief route." },
            [ordered]@{ name = "Astor"; publicLocation = "In front of the Return Home portal." },
            [ordered]@{ name = "Gavin"; publicLocation = "In the dry well near the starting area and locked chest." }
        )
        dragonEggs = 1
        eggThief = [ordered]@{ count = 1; publicLocation = "High open fields above the cave/tower area." }
        key = [ordered]@{ count = 1; publicLocation = "Near the beach by the updraft that leads home, per the in-level sign text." }
        lockedChest = [ordered]@{ count = 1; publicLocation = "Dry well area near Gavin." }
        enemyFamilies = @("Rams", "Shepherds", "Blue Thief")
        fodder = @("Sheep")
        breakables = @("Treasure chests", "Metal chests", "Locked chest")
        sources = @(
            "https://spyrowiki.com/wiki/Stone_Hill",
            "https://spyro.fandom.com/wiki/Stone_Hill",
            "https://spyrowiki.com/wiki/Egg_Thief"
        )
    }
    currentEvidence = [ordered]@{
        decodedRuntimeMobys = [int]$catalog.decodedMobys
        placementRuntimeMobys = [int]$catalog.placementMobys
        gemLikeRuntimeMobys = $gemCandidates.Count
        gemLikeRuntimeMobysWithSourceLeads = $gemWithSourceLeads.Count
        dragonNpcRuntimeCandidates = $dragonCandidates.Count
        dragonNpcRuntimeCandidatesWithSourceLeads = $dragonWithSourceLeads.Count
        enemyRuntimeCandidates = $enemyCandidates.Count
        enemyRuntimeCandidatesWithSourceLeads = $enemyWithSourceLeads.Count
        unknownPlacementRuntimeCandidates = $unknownPlacementCandidates.Count
        focusedSampleProgressDiff = $progressDiff
        sourceLinkSummary = $(if ($null -ne $sourceLinks) { $sourceLinks.summary } else { $null })
    }
    candidatePlacements = [ordered]@{
        gemLike = @($gemCandidates | ForEach-Object {
            $summary = Convert-MobySummary $_
            $summary.sourceLead = Get-SourceLead $sourceLinks ([int]$_.index)
            $summary
        })
        dragonNpc = @($dragonCandidates | ForEach-Object {
            $summary = Convert-MobySummary $_
            $summary.sourceLead = Get-SourceLead $sourceLinks ([int]$_.index)
            $summary
        })
        enemyActiveObject = @($enemyCandidates | ForEach-Object {
            $summary = Convert-MobySummary $_
            $summary.sourceLead = Get-SourceLead $sourceLinks ([int]$_.index)
            $summary
        })
        unknownPlacement = @($unknownPlacementCandidates | ForEach-Object {
            $summary = Convert-MobySummary $_
            $summary.sourceLead = Get-SourceLead $sourceLinks ([int]$_.index)
            $summary
        })
    }
    gaps = @(
        "Full 200 treasure value is not decoded into source records yet; only a loaded subset of likely gem/collectible mobys is visible in the current RAM dump.",
        "Four individual dragon identities are not mapped to moby/source records yet; current type 0x30 count is two weak dragon/NPC candidates.",
        "The key, locked chest, egg thief, sheep fodder, ram/shepherd identities, and breakable gem containers are not confirmed by focused before/after RAM samples yet.",
        "Current exact int32 WAD coordinate matches do not explain placement-like mobys; source records appear packed, transformed, streamed, or split across tables."
    )
    nextEvidenceToCapture = @(
        "Before/after RAM dump after freeing each Stone Hill dragon.",
        "Before/after RAM dump after collecting the key and opening the locked chest.",
        "Before/after RAM dump after defeating the blue egg thief.",
        "Before/after RAM dumps for ram, shepherd, sheep fodder, normal chest, metal chest, and gem pickup variants."
    )
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote Stone Hill replica checklist to $resolvedOut"
Write-Host "Runtime candidates: gems=$($gemCandidates.Count), dragon/NPC=$($dragonCandidates.Count), enemy/active=$($enemyCandidates.Count), unknown-placement=$($unknownPlacementCandidates.Count)"
