#!/usr/bin/env python3
"""Execute source-bound normal-level native flight particle callbacks with
selectors1A/1B on the bounded scalar CPU. No ROM/card/emulator mutations.
Usage: build NativeFreeFlightSmoke Release; python3 tools/Test-NativeFreeFlightEffects.py ORIGINAL_USA_BIN REPORT_JSON
"""
import hashlib
import json
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile
from SpyroEggScalarMips import Scalar, require

root = Path(__file__).resolve().parent.parent
source, report = map(Path, sys.argv[1:3])
report.parent.mkdir(parents=True, exist_ok=True)
dotnet = shutil.which('dotnet') or '/opt/homebrew/bin/dotnet'
dll = root / 'src/Spyro.Editor.NativeFreeFlightSmoke/bin/Release/net10.0/Spyro.Editor.NativeFreeFlightSmoke.dll'
with tempfile.TemporaryDirectory(prefix='flight-effects-', dir=report.parent) as scratch:
    audit = Path(scratch) / 'audit.json'
    fresh = [sys.argv[sys.argv.index('--fresh-bin') + 1]] if '--fresh-bin' in sys.argv else []
    subprocess.run([dotnet, str(dll), '--audit-effects', str(source), str(audit), *fresh], check=True, stdout=subprocess.DEVNULL)
    source_audit = json.loads(audit.read_text())
checks, cases, rows = 0, 0, []
def check(value, message):
    global checks
    require(value, message); checks += 1

for row in source_audit['rows']:
    if row['LevelId'] == 64:
        rows.append(dict(levelId=64, originalNativeFlightEffectsUnchanged=True, scalarExecuted=False, runtimeVerified=False))
        continue
    entry = int(row['spawn'], 16)
    blob = bytes.fromhex(row['callbackHex'])
    code = {entry + i: struct.unpack_from('<I', blob, i)[0] for i in range(0, len(blob), 4)}
    check(len(row['tables']) == 1, 'Actual native particle callback has one source-owned switch')
    table = row['tables'][0]
    table_address = int(table['table'], 16)
    check(len(table['allTargets']) > 0x1B, 'Both flight selectors within source table bounds')
    for selector in [0x1A, 0x1B]:
        for count in [0, 1, 2, 7]:
            cpu = Scalar(code)
            cpu.r = [0 if i == 0 else 0x51000000 + 0x1001 * i for i in range(32)]
            cpu.r[4:8] = [count, selector, 0x800B0000, 0xAB123456]
            cpu.r[29], cpu.r[31] = 0x80170000, 0x80010000
            initial = cpu.r.copy()
            cpu.map(0x8016FE00, 0x200, 0xA5)
            cpu.map(table_address, len(table['allTargets']) * 4)
            for i, target in enumerate(table['allTargets']): cpu.poke(table_address + i * 4, target)
            cpu.run(entry, {0x80010000}, maximum=30000)
            check(not cpu.calls, 'Native1A/1B normal callbacks call no allocator, RNG, audio or other helper')
            check(all(0x8016FE00 <= address and address + width <= 0x80170000 for address, width, value in cpu.writes), 'Callbacks write only their actual native stack owner')
            check(all(cpu.r[r] == initial[r] for r in [*range(16, 24), 29, 30, 31]), 'Native caller state and stack restored')
            cases += 1
    rows.append(dict(levelId=row['LevelId'], callbackAddress=row['spawn'], callbackSha256=hashlib.sha256(blob).hexdigest(),
        selectors=[0x1A,0x1B], noHelpersOrAllocation=True, onlyStackWrites=True, scalarVerified=True, runtimeVerified=False))
report.write_text(json.dumps(dict(sourceSha256=source_audit['SourceImageSha256'], checks=checks, cases=cases,
    normalCallbacksVerified=sum(r.get('scalarVerified', False) for r in rows), originalNative64EffectsPreserved=True, rows=rows, scalarOnly=True, runtimeVerified=False), indent=2))
print(f'NATIVE_FREE_FLIGHT_EFFECTS_SCALAR_OK checks={checks} cases={cases} noAllocationNormalCallbacks={sum(r.get("scalarVerified", False) for r in rows)}')
