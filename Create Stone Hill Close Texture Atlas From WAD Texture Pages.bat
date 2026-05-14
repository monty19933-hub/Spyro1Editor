@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Render-StoneHillTextureAtlas.ps1" -ImagePath ".\Spyro the Dragon (USA).bin" -UseWadTexturePages -TexturePagesSubfileIndex 0 -UseHqClose -OutPath ".\stonehill-texture-atlas-close-from-wad-subfile00.png" -OutJsonPath ".\stonehill-texture-atlas-close-from-wad-subfile00.json" -OutVramPath ".\stonehill-wad-subfile00-texture-pages-padded-vram.bin"
pause
