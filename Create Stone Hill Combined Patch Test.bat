@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Create-StoneHillCombinedPatchTest.ps1" %*
echo.
echo Done. Fresh-load Spyro the Dragon (USA)-combinedpatchtest.cue in DuckStation.
pause
