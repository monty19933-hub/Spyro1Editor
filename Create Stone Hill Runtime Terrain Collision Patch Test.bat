@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Create-StoneHillRuntimeTerrainPatchTest.ps1" -IncludeCollisionLp -OutPath ".\Spyro the Dragon (USA)-runtime-terrain-collisionpatchtest.bin" -CuePath ".\Spyro the Dragon (USA)-runtime-terrain-collisionpatchtest.cue" -PlanPath ".\Spyro the Dragon (USA)-runtime-terrain-collisionpatchtest.bin.terrainpatchplan.json" -MarkdownPath ".\Spyro the Dragon (USA)-runtime-terrain-collisionpatchtest.bin.terrainpatchplan.md"
echo.
echo Done. Fresh-load Spyro the Dragon (USA)-runtime-terrain-collisionpatchtest.cue in DuckStation.
pause
