param(
    [Alias("ImagePath")]
    [string]$SourceImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$SourceCuePath = ".\Spyro the Dragon (USA).cue",
    [string]$OutPath = ".\_local\demo\Spyro Artisans Teaser Demo.bin",
    [string]$CuePath = "",
    [string]$PlanPath = "",
    [switch]$SkipLoaderEdits,
    [switch]$SkipSpringChestPairs,
    [switch]$SkipStoneHillTerrain,
    [switch]$SkipStoneHillNight,
    [string[]]$DemoLevelKeys = @("Artisans", "StoneHill", "TownSquare", "DarkHollow", "Toasty"),
    [ValidateSet("None", "LooseGemsOnly", "GemsAndEnemies", "All")]
    [string]$LoaderAppendPolicy = "LooseGemsOnly",
    [ValidateSet("StoneHillNightKeeper", "StoneHillMoonAtmosphere", "DarkHollowMixed", "StoneHillStableNight", "StoneHillDeepNight")]
    [string]$SkyPreset = "StoneHillNightKeeper",
    [string[]]$LevelTextPatch = @(),
    [string]$StoneHillTerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$StoneHillCustomTexturesPath = ".\stonehill-custom-terrain-textures.json",
    [string]$StoneHillRamPath = ".\stonehill-before-gem-clean.bin"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Resolve-WorkspaceInputFile([string]$Path) {
    $resolved = Resolve-WorkspacePath $Path
    if (Test-Path -LiteralPath $resolved) { return $resolved }

    $fileName = [IO.Path]::GetFileName($Path)
    if ([string]::IsNullOrWhiteSpace($fileName)) { return $resolved }
    $localRoot = Join-Path $WorkspaceRoot "_local"
    if (-not (Test-Path -LiteralPath $localRoot -PathType Container)) { return $resolved }

    $matches = @(Get-ChildItem -LiteralPath $localRoot -Recurse -File -Filter $fileName -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending)
    if ($matches.Count -gt 0) { return $matches[0].FullName }
    return $resolved
}

function Normalize-LevelKey([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return (($Value.ToCharArray() | Where-Object { [char]::IsLetterOrDigit($_) } | ForEach-Object { [char]::ToLowerInvariant($_) }) -join "")
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
    if ($value -is [Array]) { return @($value) }
    return @($value)
}

function Convert-HexOrInt($Value, [int]$Default = 0) {
    if ($null -eq $Value) { return $Default }
    $text = ([string]$Value).Trim()
    if ($text.Length -eq 0) { return $Default }
    if ($text.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        return [Convert]::ToInt32($text.Substring(2), 16)
    }
    return [Convert]::ToInt32($text, 10)
}

function Read-JsonFile([string]$Path) {
    $resolved = Resolve-WorkspacePath $Path
    if (-not (Test-Path -LiteralPath $resolved)) { return $null }
    return Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
}

function Read-EditCount([string]$Path, [string]$FieldName = "edits") {
    $root = Read-JsonFile $Path
    if ($null -eq $root) { return 0 }
    return @(Get-ArrayField $root $FieldName).Count
}

function Write-Cue([string]$BinPath, [string]$CueOutPath) {
    $fileName = [IO.Path]::GetFileName($BinPath)
    @(
        "FILE `"$fileName`" BINARY",
        "  TRACK 01 MODE2/2352",
        "    INDEX 01 00:00:00"
    ) | Set-Content -LiteralPath $CueOutPath -Encoding ASCII
}

function New-StagePath([string]$Name) {
    $script:StageIndex++
    return Join-Path $StageDir ("{0:00}-{1}.bin" -f $script:StageIndex, $Name)
}

function Commit-Stage([string]$BinPath) {
    if (-not (Test-Path -LiteralPath $BinPath)) { throw "Stage did not write expected BIN: $BinPath" }
    $script:CurrentImage = $BinPath
    $script:CurrentCue = [IO.Path]::ChangeExtension($BinPath, ".cue")
    if (-not (Test-Path -LiteralPath $script:CurrentCue)) {
        Write-Cue $script:CurrentImage $script:CurrentCue
    }
}

function Invoke-LevelTextPatch([string]$Spec) {
    if ([string]::IsNullOrWhiteSpace($Spec)) { return }
    $parts = $Spec.Split(@("="), 2, [StringSplitOptions]::None)
    if ($parts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($parts[0]) -or [string]::IsNullOrWhiteSpace($parts[1])) {
        throw "LevelTextPatch must look like StoneHill=MOON HILL."
    }

    $stagePrefix = [IO.Path]::Combine($StageDir, ("{0:00}-text-{1}" -f (++$script:StageIndex), (Normalize-LevelKey $parts[0])))
    & $TextScript `
        -SourceImagePath $script:CurrentImage `
        -SourceCuePath $script:CurrentCue `
        -TargetLevelKey $parts[0].Trim() `
        -ReplacementName $parts[1].Trim().ToUpperInvariant() `
        -OutputPrefix $stagePrefix
    Commit-Stage ("$stagePrefix.bin")
    [void]$AppliedSteps.Add("Text $($parts[0].Trim()) -> $($parts[1].Trim().ToUpperInvariant())")
}

function Get-SpringChestPairs([string]$LevelKey) {
    $path = Resolve-WorkspacePath (".\{0}-native-edits.json" -f (Normalize-LevelKey $LevelKey))
    if (-not (Test-Path -LiteralPath $path)) { return @() }
    $root = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $edits = @(Get-ArrayField $root "edits")
    $byTrue = @{}
    foreach ($edit in $edits) {
        $trueIndex = Convert-HexOrInt (Get-Field $edit "trueIndex" -1) -1
        if ($trueIndex -ge 0) { $byTrue[$trueIndex] = $edit }
    }

    $pairs = @()
    foreach ($edit in $edits) {
        $helper = Get-Field $edit "springChestRuntimeHelper" $null
        if ($null -eq $helper) { continue }
        $role = ([string](Get-Field $helper "role" "")).ToLowerInvariant()
        if ($role -ne "controller") { continue }
        $controllerTrue = Convert-HexOrInt (Get-Field $edit "trueIndex" -1) -1
        $shellTrue = Convert-HexOrInt (Get-Field $helper "partnerTrueIndex" -1) -1
        if ($controllerTrue -lt 0 -or $shellTrue -lt 0 -or -not $byTrue.ContainsKey($shellTrue)) { continue }
        $pairs += [pscustomobject]@{
            LevelKey = $LevelKey
            SourcePath = $path
            SourceRoot = $root
            Controller = $edit
            Shell = $byTrue[$shellTrue]
            ControllerTrueIndex = $controllerTrue
            ShellTrueIndex = $shellTrue
        }
    }
    return @($pairs)
}

function Invoke-SpringChestPair([object]$Pair) {
    $levelKey = [string]$Pair.LevelKey
    $stem = "{0:00}-spring-{1}-T{2}-T{3}" -f (++$script:StageIndex), (Normalize-LevelKey $levelKey), $Pair.ControllerTrueIndex, $Pair.ShellTrueIndex
    $pairEditPath = Join-Path $StageDir ($stem + "-edits.json")
    $outPath = Join-Path $StageDir ($stem + ".bin")
    $cuePath = [IO.Path]::ChangeExtension($outPath, ".cue")
    $planPath = "$outPath.patchplan.json"

    $root = $Pair.SourceRoot | ConvertTo-Json -Depth 30 | ConvertFrom-Json
    $root.edits = @($Pair.Controller, $Pair.Shell)
    $root.editCount = 2
    $root.note = "Teaser demo isolated spring chest pair export."
    $root | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $pairEditPath -Encoding UTF8

    $reward = 0x55
    $rewardEdit = Get-Field $Pair.Shell "rewardColorEdit" $null
    if ($null -ne $rewardEdit) {
        $reward = Convert-HexOrInt (Get-Field $rewardEdit "sourceByte53Hex" "0x55") 0x55
    }
    elseif ($null -ne (Get-Field $Pair.Shell "flag4BHex" $null)) {
        $reward = Convert-HexOrInt (Get-Field $Pair.Shell "flag4BHex" "0x55") 0x55
    }
    if ($reward -lt 0x53 -or $reward -gt 0x57) { $reward = 0x55 }

    & $SpringScript `
        -LevelKey $levelKey `
        -NativeEditsPath $pairEditPath `
        -ImagePath $script:CurrentImage `
        -OutPath $outPath `
        -CuePath $cuePath `
        -PlanPath $planPath `
        -RewardGemIdByte $reward `
        -ControllerIndex $Pair.ControllerTrueIndex `
        -ShellIndex $Pair.ShellTrueIndex
    Commit-Stage $outPath
    [void]$AppliedSteps.Add("Spring chest pair $levelKey T$($Pair.ControllerTrueIndex)/T$($Pair.ShellTrueIndex)")
}

$LoaderScript = Join-Path $PSScriptRoot "Export-SpyroLevelMobyPatchTest.ps1"
$SpringScript = Join-Path $PSScriptRoot "Export-SpyroSpringChestPairInGamePatchTest.ps1"
$TextScript = Join-Path $PSScriptRoot "Export-SpyroLevelTextPatchTest.ps1"
$SkyScript = Join-Path $PSScriptRoot "Export-SpyroSkyPrimitiveColorPalette.ps1"
$FindTerrainScript = Join-Path $PSScriptRoot "Find-StoneHillRuntimeTerrainSource.ps1"
$TerrainScript = Join-Path $PSScriptRoot "Export-StoneHillRuntimeTerrainPatchTest.ps1"
foreach ($script in @($LoaderScript, $SpringScript, $TextScript, $SkyScript, $FindTerrainScript, $TerrainScript)) {
    if (-not (Test-Path -LiteralPath $script)) { throw "Missing teaser dependency: $script" }
}

$SourceImage = Resolve-WorkspaceInputFile $SourceImagePath
$SourceCue = Resolve-WorkspacePath $SourceCuePath
if (-not (Test-Path -LiteralPath $SourceImage)) { throw "Missing source image: $SourceImage" }

$ResolvedOut = Resolve-WorkspacePath $OutPath
if ([string]::IsNullOrWhiteSpace($CuePath)) { $CuePath = [IO.Path]::ChangeExtension($ResolvedOut, ".cue") }
if ([string]::IsNullOrWhiteSpace($PlanPath)) { $PlanPath = "$ResolvedOut.demo-plan.json" }
$ResolvedCue = Resolve-WorkspacePath $CuePath
$ResolvedPlan = Resolve-WorkspacePath $PlanPath
$OutDir = Split-Path -Parent $ResolvedOut
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$StageDir = Join-Path $OutDir "stages"
New-Item -ItemType Directory -Path $StageDir -Force | Out-Null

$script:StageIndex = 0
$script:CurrentImage = $SourceImage
$script:CurrentCue = $SourceCue
$AppliedSteps = New-Object Collections.Generic.List[string]
$Warnings = New-Object Collections.Generic.List[string]

if (-not $SkipLoaderEdits) {
    foreach ($demoLevel in $DemoLevelKeys) {
        if ([string]::IsNullOrWhiteSpace($demoLevel)) { continue }
        $normalizedLevel = Normalize-LevelKey $demoLevel
        $nativeEditsPath = Join-Path $WorkspaceRoot ("{0}-native-edits.json" -f $normalizedLevel)
        $nativeEditCount = Read-EditCount $nativeEditsPath
        if ($nativeEditCount -le 0) { continue }

        $stage = New-StagePath ("loader-{0}" -f $normalizedLevel)
        try {
            & $LoaderScript `
                -LevelKey $demoLevel `
                -ImagePath $script:CurrentImage `
                -AppendPolicy $LoaderAppendPolicy `
                -OutPath $stage `
                -CuePath ([IO.Path]::ChangeExtension($stage, ".cue")) `
                -PlanPath "$stage.patchplan.json"
            Commit-Stage $stage
            [void]$AppliedSteps.Add("Saved $demoLevel loader edits, append policy $LoaderAppendPolicy")
        }
        catch {
            $message = $_.Exception.Message
            if ($message -match "No patchable moby edits") {
                [void]$Warnings.Add("Skipped $demoLevel loader edits: no patchable non-special moby edits were found.")
            }
            else {
                throw
            }
        }
    }
}

if (-not $SkipSpringChestPairs -and $LoaderAppendPolicy -ne "None") {
    [void]$Warnings.Add("Skipped spring chest pairs because loader append policy $LoaderAppendPolicy uses the same append area; use -LoaderAppendPolicy None for the spring-chest-focused teaser until append allocation is unified.")
}
elseif (-not $SkipSpringChestPairs) {
    foreach ($levelKey in @("Artisans", "StoneHill")) {
        $pairs = @(Get-SpringChestPairs $levelKey)
        if ($pairs.Count -gt 0) {
            Invoke-SpringChestPair $pairs[0]
            if ($pairs.Count -gt 1) {
                [void]$Warnings.Add("Only the first $levelKey spring chest pair was included; the helper is one pair per level for now.")
            }
        }
    }
}

if (-not $SkipStoneHillTerrain) {
    $terrainCount = Read-EditCount $StoneHillTerrainEditsPath
    $customRoot = Read-JsonFile $StoneHillCustomTexturesPath
    $customCount = if ($null -eq $customRoot) { 0 } else { @(Get-ArrayField $customRoot "textures").Count }
    $terrainEditsForExport = Resolve-WorkspacePath $StoneHillTerrainEditsPath
    if ($terrainCount -gt 0 -and -not (Test-Path -LiteralPath (Resolve-WorkspacePath $StoneHillRamPath))) {
        [void]$Warnings.Add("Skipped Stone Hill terrain height/face edits because the RAM proof file is missing: $(Resolve-WorkspacePath $StoneHillRamPath)")
        $terrainCount = 0
        $terrainEditsForExport = Join-Path $StageDir "empty-stonehill-terrain-edits.json"
        [ordered]@{
            generatedAt = (Get-Date).ToString("s")
            note = "Empty terrain edit file for teaser demo custom-texture-only export."
            editCount = 0
            edits = @()
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $terrainEditsForExport -Encoding UTF8
    }
    if ($terrainCount -gt 0 -or $customCount -gt 0) {
        $stage = New-StagePath "stonehill-terrain"
        $terrainSearch = Join-Path $StageDir "stonehill-runtime-terrain-source-search.json"
        if ($terrainCount -gt 0) {
            & $FindTerrainScript `
                -ImagePath $script:CurrentImage `
                -RamPath (Resolve-WorkspacePath $StoneHillRamPath) `
                -TerrainEditsPath $terrainEditsForExport `
                -QuickSectorOnly `
                -OutJsonPath $terrainSearch `
                -OutMarkdownPath (Join-Path $StageDir "stonehill-runtime-terrain-source-search.md")
        }
        $terrainArgs = @(
            "-ImagePath", $script:CurrentImage,
            "-RamPath", (Resolve-WorkspacePath $StoneHillRamPath),
            "-TerrainEditsPath", $terrainEditsForExport,
            "-OutPath", $stage,
            "-CuePath", ([IO.Path]::ChangeExtension($stage, ".cue")),
            "-PlanPath", "$stage.terrainpatchplan.json",
            "-MarkdownPath", "$stage.terrainpatchplan.md",
            "-AllowExperimentalWrite"
        )
        if ($terrainCount -gt 0) { $terrainArgs += @("-SourceSearchPath", $terrainSearch) }
        if ($customCount -gt 0) { $terrainArgs += @("-CustomTexturesPath", (Resolve-WorkspacePath $StoneHillCustomTexturesPath)) }
        & $TerrainScript @terrainArgs
        Commit-Stage $stage
        [void]$AppliedSteps.Add("Stone Hill terrain edits/custom texture imports")
    }
}

foreach ($patch in $LevelTextPatch) {
    Invoke-LevelTextPatch $patch
}

if (-not $SkipStoneHillNight) {
    $prefix = [IO.Path]::Combine($StageDir, ("{0:00}-stonehill-sky-{1}" -f (++$script:StageIndex), (Normalize-LevelKey $SkyPreset)))
    & $SkyScript `
        -SourceImagePath $script:CurrentImage `
        -SourceCuePath $script:CurrentCue `
        -Preset $SkyPreset `
        -OutputPrefix $prefix
    Commit-Stage ("$prefix.bin")
    [void]$AppliedSteps.Add("Stone Hill sky preset $SkyPreset")
}

Copy-Item -LiteralPath $script:CurrentImage -Destination $ResolvedOut -Force
Write-Cue $ResolvedOut $ResolvedCue

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    status = "demo-cue-written"
    sourceImagePath = $SourceImage
    outputImagePath = $ResolvedOut
    outputCuePath = $ResolvedCue
    appliedSteps = @($AppliedSteps.ToArray())
    warnings = @($Warnings.ToArray())
    stageDirectory = $StageDir
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResolvedPlan -Encoding UTF8

Write-Host "Wrote Artisans teaser demo BIN to $ResolvedOut"
Write-Host "Wrote Artisans teaser demo CUE to $ResolvedCue"
Write-Host "Wrote demo plan to $ResolvedPlan"
foreach ($warning in $Warnings) { Write-Warning $warning }
