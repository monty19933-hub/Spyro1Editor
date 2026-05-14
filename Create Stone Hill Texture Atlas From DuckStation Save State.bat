@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Render-StoneHillTextureAtlas.ps1" ^
  -SaveStatePath "C:\Users\monty\AppData\Local\DuckStation\savestates\SCUS-94228_1.sav" ^
  -OutVramPath ".\duckstation-state-gpu-vram-fresh-stonehill.bin" ^
  -OutPath ".\stonehill-texture-atlas-fresh-stonehill.png" ^
  -OutJsonPath ".\stonehill-texture-atlas-fresh-stonehill.json"

pause
