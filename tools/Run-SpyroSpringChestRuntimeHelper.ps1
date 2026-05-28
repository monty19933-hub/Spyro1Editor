param(
    [string]$OutPrefix = ".\_local\experiments\spring-runtime-helper",
    [int]$CandidateIndex = 1,
    [int]$ControllerIndex = 32,
    [int]$ShellIndex = 38,
    [int]$RewardGemIdByte = 0x55,
    [int]$RepeatSeconds = 90,
    [int]$PostTriggerWatchSeconds = 45
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runPrefix = "{0}-{1}" -f $OutPrefix, $stamp
$manifestPath = "{0}-candidates.json" -f $runPrefix
$logPath = "{0}.log" -f $runPrefix
$dumpScript = Join-Path $workspaceRoot "tools\Dump-DuckStationRam.ps1"
$helperScript = Join-Path $workspaceRoot "tools\Patch-SpyroSpringChestRuntimeHelper.ps1"

if (-not (Test-Path -LiteralPath $dumpScript)) {
    throw "Missing DuckStation RAM dump tool: $dumpScript"
}
if (-not (Test-Path -LiteralPath $helperScript)) {
    throw "Missing spring chest runtime helper: $helperScript"
}

Write-Host "Dumping current DuckStation RAM for spring chest helper..."
& $dumpScript -OutPrefix $runPrefix -DumpAllCandidates -MaxCandidates 28

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Expected RAM manifest was not created: $manifestPath"
}

Write-Host "Arming spring chest runtime helper. Flame/charge the chest, then collect the blue gem."
& $helperScript `
    -ManifestPath $manifestPath `
    -CandidateIndex $CandidateIndex `
    -ControllerIndex $ControllerIndex `
    -ShellIndex $ShellIndex `
    -AutoSelectActiveGem `
    -RepeatSeconds $RepeatSeconds `
    -PostTriggerWatchSeconds $PostTriggerWatchSeconds `
    -UseTownPopGemPrivate `
    -GemPoppedTimer 0x46 `
    -RewardGemIdByte $RewardGemIdByte `
    -SpawnZDelta 0x0558 `
    -AnimateGemZ `
    -AnimateGemZEndDelta 0x06EA `
    -AnimateGemZDurationMs 900 `
    -WriteTownShellPoppedPrivate `
    -ShellPoppedTimer 0x46 `
    -WriteBridgeShellPopBytes `
    -ResetShellPrePopState `
    -ClearInactiveNearChestRewardGems `
    -ResetShellAfterPop `
    -ShellResetDelayMs 1200 `
    -ClearHitAfterShellReset `
    -ClearHitAfterShellResetDurationMs 900 `
    -WatchGemCollect `
    -GemCollectMinAgeMs 700 `
    -HideShellOnGemCollect `
    -OutPath $logPath

Write-Host "Spring chest runtime helper log: $logPath"
