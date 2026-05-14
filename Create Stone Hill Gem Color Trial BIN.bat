@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\New-StoneHillGemColorTrial.ps1" -BuildBin
pause
