@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Create-StoneHillRuntimeTerrainPatchTest.ps1" %*
echo.
echo Done. Fresh-load Spyro the Dragon (USA)-runtime-terrainpatchtest.cue in DuckStation.
pause
