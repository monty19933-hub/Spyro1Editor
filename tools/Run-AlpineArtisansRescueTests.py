#!/usr/bin/env python3
"""Run current Artisans rescue source/export/replay tests in a new output folder.

Usage: Run-AlpineArtisansRescueTests.py ORIGINAL_BIN CATALOG_DIRECTORY NEW_OUTPUT
DOTNET optionally selects the installed dotnet executable. No emulator or UI.
The original image is opened read-only; generated candidate images stay in output.
"""
from pathlib import Path
import os,subprocess,sys
root=Path(__file__).resolve().parent.parent
if len(sys.argv)!=4:raise SystemExit(__doc__)
disc,catalog,out=Path(sys.argv[1]).resolve(),Path(sys.argv[2]).resolve(),Path(sys.argv[3]).resolve()
if out.exists():raise SystemExit('Output must be a new directory.')
out.mkdir(parents=True);dotnet=os.environ.get('DOTNET','dotnet')
commands=[
 [dotnet,'run','--project',str(root/'src/Spyro.Editor.AlpineArtisansSmoke'),'-c','Release','--',str(disc),str(catalog),str(out/'compose')],
 [dotnet,'run','--project',str(root/'src/Spyro.Editor.AlpineArtisansExportSmoke'),'-c','Release','--',str(disc),str(catalog),str(out/'export'),str(out/'compose')],
 [sys.executable,str(root/'tools/Test-AlpineArtisansRescue.py'),str(out/'compose'),str(out/'export')],
]
for command in commands:subprocess.run(command,cwd=root,check=True)
print('ALPINE_ARTISANS_RESCUE_SOURCE_EXPORT_REPLAY_OK output='+str(out))
