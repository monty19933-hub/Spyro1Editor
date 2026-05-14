@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Create-StoneHillTerrainPlatformPatchTest.ps1" -OutPath ".\Spyro the Dragon (USA)-runtime-terrain-platformpatchtest.bin" -CuePath ".\Spyro the Dragon (USA)-runtime-terrain-platformpatchtest.cue" %*
if errorlevel 1 goto :failed
echo.
echo Done. Fresh-load Spyro the Dragon (USA)-runtime-terrain-platformpatchtest.cue in DuckStation.
pause
exit /b 0
:failed
echo.
echo Platform topology test failed. See the error above.
pause
exit /b 1
