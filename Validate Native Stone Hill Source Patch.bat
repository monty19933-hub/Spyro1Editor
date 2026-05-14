@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Validate-StoneHillSourcePatch.ps1" -CaptureLive -PatchPlanPath ".\Spyro the Dragon (USA)-nativepatchtest-topranked.bin.patchplan.json" -OutPath ".\stonehill-source-patch-live-validation.json"
pause
