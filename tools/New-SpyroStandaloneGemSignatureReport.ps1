param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$SourceTablesPath = ".\spyro-moby-source-tables.json",
    [string]$OutJsonPath = ".\spyro-standalone-gem-signatures.json",
    [string]$OutMarkdownPath = ".\spyro-standalone-gem-signatures.md"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37
$RecordStride = 0x58

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

function Get-HexInt($Value) {
    if ($Value -is [string]) {
        $text = $Value.Trim()
        if ($text.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
            return [int64][Convert]::ToInt64($text.Substring(2), 16)
        }
    }
    return [int64]$Value
}

function Convert-WadOffsetToImageOffset([int64]$WadOffset) {
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    $sectorOffset = [int]($WadOffset % 2048)
    return ($sector * 2352L) + 24L + [int64]$sectorOffset
}

function Get-KnownGemColor([byte]$Byte36, [byte]$Byte4F) {
    $key = "0x{0:X2}/0x{1:X2}" -f $Byte36, $Byte4F
    switch ($key) {
        "0x53/0x01" { return [ordered]@{ color = "red"; rewardValue = 1; confidence = "confirmed in Stone Hill" } }
        "0x54/0x02" { return [ordered]@{ color = "green"; rewardValue = 2; confidence = "confirmed in Stone Hill" } }
        "0x55/0x03" { return [ordered]@{ color = "blue"; rewardValue = 5; confidence = "confirmed in Stone Hill editor trial" } }
        "0x56/0x04" { return [ordered]@{ color = "yellow"; rewardValue = 10; confidence = "confirmed in Stone Hill editor trial" } }
        "0x57/0x05" { return [ordered]@{ color = "purple"; rewardValue = 25; confidence = "confirmed in Stone Hill editor trial" } }
        default { return [ordered]@{ color = ""; rewardValue = $null; confidence = "unmapped" } }
    }
}

$resolvedImagePath = Resolve-WorkspacePath $ImagePath
$resolvedSourceTablesPath = Resolve-WorkspacePath $SourceTablesPath
$resolvedOutJsonPath = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath

if (-not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing image: $resolvedImagePath" }
if (-not (Test-Path -LiteralPath $resolvedSourceTablesPath)) { throw "Missing source tables: $resolvedSourceTablesPath" }

$sourceTablesRoot = Get-Content -Raw -LiteralPath $resolvedSourceTablesPath | ConvertFrom-Json
$tables = @()
$tables += @(Get-Field $sourceTablesRoot "knownStoneHillCandidate" @())
$tables += @(Get-Field $sourceTablesRoot "candidates" @())

$groups = @{}
$stream = [System.IO.File]::OpenRead((Resolve-Path -LiteralPath $resolvedImagePath).Path)
try {
    foreach ($table in $tables) {
        $wadEntry = [int](Get-Field $table "wadEntry" -1)
        $recordCount = [int](Get-Field $table "recordCount" 0)
        $tableWadOffset = Get-HexInt (Get-Field $table "tableWadOffset" 0)
        if ($wadEntry -lt 0 -or $recordCount -le 0 -or $tableWadOffset -le 0) { continue }

        for ($recordIndex = 0; $recordIndex -lt $recordCount; $recordIndex++) {
            $recordWadOffset = $tableWadOffset + ([int64]$recordIndex * $RecordStride)
            $recordImageOffset = Convert-WadOffsetToImageOffset $recordWadOffset
            $bytes = New-Object byte[] $RecordStride
            $stream.Position = $recordImageOffset
            [void]$stream.Read($bytes, 0, $bytes.Length)

            if ($bytes[0x50] -ne 0x18 -or $bytes[0x52] -ne 0x40 -or $bytes[0x53] -ne 0xFF) { continue }

            $byte36 = [byte]$bytes[0x36]
            $byte4F = [byte]$bytes[0x4F]
            $groupKey = "0x{0:X2}/0x{1:X2}" -f $byte36, $byte4F
            if (-not $groups.ContainsKey($groupKey)) {
                $known = Get-KnownGemColor $byte36 $byte4F
                $groups[$groupKey] = [ordered]@{
                    sourceByte36 = ("0x{0:X2}" -f $byte36)
                    sourceByte4F = ("0x{0:X2}" -f $byte4F)
                    tierByte = [int]$byte4F
                    inferredColor = $known.color
                    rewardValue = $known.rewardValue
                    confidence = $known.confidence
                    count = 0
                    entryCounts = @{}
                    samples = @()
                }
            }

            $group = $groups[$groupKey]
            $group.count = [int]$group.count + 1
            $entryKey = "E$wadEntry"
            if (-not $group.entryCounts.Contains($entryKey)) { $group.entryCounts[$entryKey] = 0 }
            $group.entryCounts[$entryKey] = [int]$group.entryCounts[$entryKey] + 1
            if ($group.samples.Count -lt 20) {
                $group.samples += [ordered]@{
                    wadEntry = $wadEntry
                    recordIndex = $recordIndex
                    id = "E$wadEntry:T$recordIndex"
                    sourceRecordWadOffset = ("0x{0:X}" -f $recordWadOffset)
                }
            }
        }
    }
}
finally {
    $stream.Dispose()
}

$records = @(
    $groups.Values |
        Sort-Object @{ Expression = { [int]$_.tierByte }; Ascending = $true }, @{ Expression = { [string]$_.sourceByte36 }; Ascending = $true } |
        ForEach-Object {
            $entryCounts = $_.entryCounts
            $topEntries = @(
                $entryCounts.GetEnumerator() |
                    Sort-Object Value -Descending |
                    Select-Object -First 10 |
                    ForEach-Object { [ordered]@{ wadEntry = $_.Key; count = $_.Value } }
            )
            [ordered]@{
                sourceByte36 = $_.sourceByte36
                sourceByte4F = $_.sourceByte4F
                tierByte = $_.tierByte
                inferredColor = $_.inferredColor
                rewardValue = $_.rewardValue
                confidence = $_.confidence
                count = $_.count
                entryCount = $entryCounts.Count
                topEntries = $topEntries
                samples = $_.samples
            }
        }
)

$root = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    imagePath = (Resolve-Path -LiteralPath $resolvedImagePath).Path
    sourceTablesPath = (Resolve-Path -LiteralPath $resolvedSourceTablesPath).Path
    filter = "0x58 source records with +0x50=0x18, +0x52=0x40, +0x53=0xFF"
    conclusion = "Standalone gem source records use +0x36 as the visible gem/color id and +0x4F as a gem tier byte. Red, green, blue, yellow, and purple are in-game confirmed in Stone Hill editor trials."
    signatures = $records
}

$root | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutJsonPath -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Spyro Standalone Gem Signatures")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($root.generatedAt)")
[void]$lines.Add("")
[void]$lines.Add("Filter: ``$($root.filter)``")
[void]$lines.Add("")
[void]$lines.Add($root.conclusion)
[void]$lines.Add("")
[void]$lines.Add("| +0x36 | +0x4F tier | Inferred color | Reward | Confidence | Count | Level-table count | Top tables |")
[void]$lines.Add("|---:|---:|---|---:|---|---:|---:|---|")
foreach ($sig in $records) {
    $reward = if ($null -eq $sig.rewardValue) { "" } else { [string]$sig.rewardValue }
    $top = (($sig.topEntries | Select-Object -First 6 | ForEach-Object { "$($_.wadEntry):$($_.count)" }) -join ", ")
    [void]$lines.Add("| ``$($sig.sourceByte36)`` | ``$($sig.sourceByte4F)`` | $($sig.inferredColor) | $reward | $($sig.confidence) | $($sig.count) | $($sig.entryCount) | $top |")
}
[void]$lines.Add("")
[void]$lines.Add("## Editor Mapping")
[void]$lines.Add("")
[void]$lines.Add("- Red 1: ``+0x36=0x53``, ``+0x4F=0x01``. Confirmed in Stone Hill.")
[void]$lines.Add("- Green 2: ``+0x36=0x54``, ``+0x4F=0x02``. Confirmed in Stone Hill.")
[void]$lines.Add("- Blue 5: ``+0x36=0x55``, ``+0x4F=0x03``. Confirmed in Stone Hill.")
[void]$lines.Add("- Yellow 10: ``+0x36=0x56``, ``+0x4F=0x04``. Confirmed in Stone Hill.")
[void]$lines.Add("- Purple 25: ``+0x36=0x57``, ``+0x4F=0x05``. Confirmed in Stone Hill.")
$lines | Set-Content -LiteralPath $resolvedOutMarkdownPath -Encoding UTF8

Write-Host "Wrote $resolvedOutJsonPath"
Write-Host "Wrote $resolvedOutMarkdownPath"
