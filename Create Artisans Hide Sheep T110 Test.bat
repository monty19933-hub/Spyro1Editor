@echo off
setlocal
cd /d "%~dp0"
rem Soft-removes sheep T110 by moving the source record far outside the playable level.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-SpyroMobyRecordMutation.ps1" -LevelKey Artisans -Mode Hide -TargetIndex 110 -OutBinPath ".\Spyro the Dragon (USA)-artisans-hide-sheep110-test.bin"
pause
