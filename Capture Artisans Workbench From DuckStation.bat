@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Capture-SpyroLevelWorkbench.ps1" -LevelName "Artisans" -ExpectedLevelId 10 -OutPrefix "artisans"
pause
