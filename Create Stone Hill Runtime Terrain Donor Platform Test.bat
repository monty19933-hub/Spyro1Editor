@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Create-StoneHillTerrainDonorPlatformPatchTest.ps1" -OutPath ".\Spyro the Dragon (USA)-runtime-terrain-donorplatformtest.bin" -CuePath ".\Spyro the Dragon (USA)-runtime-terrain-donorplatformtest.cue" %*
if errorlevel 1 goto :failed
echo.
echo Done. Fresh-load Spyro the Dragon (USA)-runtime-terrain-donorplatformtest.cue in DuckStation.
pause
exit /b 0
:failed
echo.
echo Donor platform test failed. See the error above.
pause
exit /b 1
