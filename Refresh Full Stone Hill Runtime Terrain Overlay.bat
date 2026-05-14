@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-SpyroRuntimeSceneOverlay.ps1" -RamPath ".\stonehill-before-gem-clean.bin" -OutPath ".\stonehill-runtime-scene-editor-overlay.json" -MaxSceneCandidates 1 -MaxOverlayCandidates 1 -MaxPointsPerCandidate 10000 -MaxEdgesPerCandidate 22000 -MaxPolygonsPerCandidate 7000 -Projections xy
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\New-StoneHillTerrainBridgeReport.ps1"
endlocal
