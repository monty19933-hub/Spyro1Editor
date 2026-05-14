@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Create-StoneHillRuntimeTerrainCollisionTriPatchTest.ps1" -OutPath ".\Spyro the Dragon (USA)-runtime-terrain-collsidepatchtest.bin" -CuePath ".\Spyro the Dragon (USA)-runtime-terrain-collsidepatchtest.cue" %*
echo.
echo Done. Fresh-load Spyro the Dragon (USA)-runtime-terrain-collsidepatchtest.cue in DuckStation.
pause
