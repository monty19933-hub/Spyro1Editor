@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Create-StoneHillRuntimeTerrainCollisionTriPatchTest.ps1" %*
echo.
echo Done. Fresh-load Spyro the Dragon (USA)-runtime-terrain-colltritest.cue in DuckStation.
pause
