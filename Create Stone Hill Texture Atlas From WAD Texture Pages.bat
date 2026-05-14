@echo off
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Render-StoneHillTextureAtlas.ps1" ^
  -UseWadTexturePages ^
  -OutVramPath ".\stonehill-wad-subfile00-texture-pages-padded-vram.bin" ^
  -OutPath ".\stonehill-texture-atlas-from-wad-subfile00.png" ^
  -OutJsonPath ".\stonehill-texture-atlas-from-wad-subfile00.json"

pause
