@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\New-StoneHillTerrainSculptEdit.ps1" -TerrainEditsPath ".\stonehill-terrain-edits.json" -OverlayPath ".\stonehill-runtime-scene-editor-overlay.json" -OutPath ".\stonehill-terrain-edits-sculpt.json" -InnerRadius 160 -OuterRadius 640 -MinimumWeight 0.03
if errorlevel 1 goto :done
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Create-StoneHillRuntimeTerrainCollisionTriPatchTest.ps1" -TerrainEditsPath ".\stonehill-terrain-edits-sculpt.json" -OutPath ".\Spyro the Dragon (USA)-runtime-terrain-sculptpatchtest.bin" -CuePath ".\Spyro the Dragon (USA)-runtime-terrain-sculptpatchtest.cue" %*
:done
echo.
echo Done. Fresh-load Spyro the Dragon (USA)-runtime-terrain-sculptpatchtest.cue in DuckStation.
pause
