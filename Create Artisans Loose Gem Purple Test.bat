@echo off
setlocal
cd /d "%~dp0"
rem Proven loose gem color/value path: changes standalone gem T165 to purple / 25.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-SpyroMobyRecordMutation.ps1" -LevelKey Artisans -Mode RewardColor -TargetIndex 165 -GemColor purple -OutBinPath ".\Spyro the Dragon (USA)-artisans-gem165-purple-test.bin"
pause
