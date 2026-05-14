@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Create-StoneHillTerrainDonorPlatformPatchTest.ps1" -OutPath ".\Spyro the Dragon (USA)-runtime-terrain-donorplatform2test.bin" -CuePath ".\Spyro the Dragon (USA)-runtime-terrain-donorplatform2test.cue"
if errorlevel 1 goto :failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\New-StoneHillCollisionBridgeReport.ps1" -ImagePath ".\Spyro the Dragon (USA).bin" -RamPath ".\stonehill-before-gem-clean.bin" -TerrainEditsPath ".\stonehill-terrain-edits.json" -OutJsonPath ".\stonehill-donorplatform2-collision-bridge-report.json" -OutMarkdownPath ".\stonehill-donorplatform2-collision-bridge-report.md" -CollisionTriangleWadBaseOffset "0xCE4338" -SearchWad
if errorlevel 1 goto :failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Apply-StoneHillCollisionTrianglePatch.ps1" -ImagePath ".\Spyro the Dragon (USA)-runtime-terrain-donorplatform2test.bin" -CollisionReportPath ".\stonehill-donorplatform2-collision-bridge-report.json" -OutPath ".\Spyro the Dragon (USA)-runtime-terrain-donorplatform2colltest.bin" -CuePath ".\Spyro the Dragon (USA)-runtime-terrain-donorplatform2colltest.cue" -RamPath ".\stonehill-before-gem-clean.bin" -PlanPath ".\Spyro the Dragon (USA)-runtime-terrain-donorplatform2colltest.bin.collisionpatchplan.json" -MarkdownPath ".\Spyro the Dragon (USA)-runtime-terrain-donorplatform2colltest.bin.collisionpatchplan.md" -AllowExperimentalWrite
if errorlevel 1 goto :failed
echo.
echo Done. Fresh-load Spyro the Dragon (USA)-runtime-terrain-donorplatform2colltest.cue in DuckStation.
pause
exit /b 0
:failed
echo.
echo Donor platform collision test failed. See the error above.
pause
exit /b 1
