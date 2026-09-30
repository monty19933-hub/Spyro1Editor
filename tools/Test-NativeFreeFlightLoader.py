#!/usr/bin/env python3
"""Source-aware scalar verification of the exact normal-player loader patch.
Usage: python3 tools/Test-NativeFreeFlightLoader.py ORIGINAL_USA_BIN REPORT_JSON
Build NativeFreeFlightSmoke Release first. Reads the original disc only; does
not invoke an emulator, write cards, or claim native visual/physics acceptance.
"""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
import shutil

from SpyroEggScalarMips import Scalar, require

ROOT = Path(__file__).resolve().parent.parent
SOURCE_HASH = 'fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37'
LOADER_HASH = '0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb'
ENTRY = 0x80013230
RETURN = 0x80010000
BASE = 0x801C5BB8
LOCAL = 0x800A0000
STACK = 0x80180000
u = lambda b, o: struct.unpack_from('<I', b, o)[0]
sha = lambda b: hashlib.sha256(b).hexdigest()
checks = 0

def emission():
    dotnet = shutil.which('dotnet') or '/opt/homebrew/bin/dotnet'
    dll = Path(sys.argv[sys.argv.index('--dll') + 1]) if '--dll' in sys.argv else ROOT / 'src/Spyro.Editor.NativeFreeFlightSmoke/bin/Release/net10.0/Spyro.Editor.NativeFreeFlightSmoke.dll'
    return json.loads(subprocess.check_output([dotnet, str(dll), '--emit-global'], text=True))

def check(value, message):
    global checks
    require(value, message)
    checks += 1

def logical(f, lba, off, size):
    out = bytearray()
    while size:
        sector, within = divmod(off, 2048)
        count = min(size, 2048 - within)
        f.seek((lba + sector) * 2352 + 24 + within)
        block = f.read(count)
        check(len(block) == count, 'Truncated native source owner')
        out += block
        off += count
        size -= count
    return bytes(out)

def code(blob):
    return {ENTRY + i: u(blob, i) for i in range(0, len(blob), 4)}

def execute(blob, stream, package, level, old_local, old_alias):
    cpu = Scalar(code(blob))
    cpu.r = [0 if i == 0 else (0x51000000 + i * 0x1001) for i in range(32)]
    cpu.r[4], cpu.r[29], cpu.r[31] = LOCAL, STACK, RETURN
    cpu.map(0x80075690, 4); cpu.poke(0x80075690, 1) # deliberately stale
    cpu.map(0x8007596C, 4); cpu.poke(0x8007596C, level)
    cpu.map(0x80076378, 4); cpu.poke(0x80076378, BASE)
    cpu.map(0x800785FC, 4); cpu.poke(0x800785FC, BASE)
    cpu.map(BASE, len(package)); cpu.memory.update({BASE + i: b for i, b in enumerate(package)})
    cpu.poke(BASE + 0x34, BASE + u(package, 0x34))
    for aid in range(46):
        root = u(package, 0x38 + 4 * aid)
        cpu.poke(BASE + 0x38 + 4 * aid, root if root == 0xFFFFFFFF else BASE + root)
        if root == 0xFFFFFFFF and old_local: cpu.poke(BASE + 0x38 + 4 * aid, LOCAL - 0x1000 + 4 * aid)
        if aid in (33, 34, 35) and old_alias: cpu.poke(BASE + 0x38 + 4 * aid, BASE + u(package, 0x78))
    cpu.map(LOCAL, len(stream)); cpu.memory.update({LOCAL + i: b for i, b in enumerate(stream)})
    cpu.map(STACK - 8, 8, 0xA5)
    initial = cpu.r.copy()
    cpu.run(ENTRY, {RETURN}, maximum=30000)
    check(cpu.r[29] == STACK and cpu.r[31] == RETURN, 'Native stack/return preserved')
    check(all(cpu.r[i] == initial[i] for i in [1, 12, 13, 14, 15, *range(16, 29), 30]), 'Unowned registers preserved')
    allowed = [(LOCAL, LOCAL + len(stream)), (BASE + 0x38, BASE + 0xF0), (STACK - 8, STACK)]
    check(all(any(lo <= a and a + n <= hi for lo, hi in allowed) for a, n, _ in cpu.writes), 'Only native roots, stream fixups and existing stack owner written')
    check(cpu.calls == [], 'No helpers, caves or host substitutions')
    return cpu

def main():
    source, report = map(Path, sys.argv[1:3])
    check(source.stat().st_size == 661547040, 'Exact USA source length')
    with source.open('rb') as f:
        check(hashlib.file_digest(f, 'sha256').hexdigest() == SOURCE_HASH, 'Exact USA source SHA256')
        exe = logical(f, 53875, 0, 0x66000)
        original = exe[0x3A30:0x3BE0]
        check(sha(original) == LOADER_HASH, 'Complete original loader owner')
        emitted = next(w for w in emission() if w['Name'] == 'normal-resident-airborne-loader')
        patched = bytes.fromhex(emitted['after'])
        check(bytes.fromhex(emitted['before']) == original and len(patched) == 432, 'Exact bounded before/after source')
        # No source EXE J/JAL enters the replaced function after its entry.
        for offset in range(0x800, len(exe), 4):
            word = u(exe, offset)
            target = 0x80000000 | ((word & 0x3FFFFFF) << 2)
            if word >> 26 in (2, 3) and ENTRY < target < ENTRY + 432:
                check(0x3A30 <= offset < 0x3BE0, 'No external transfer into loader interior')
        package = logical(f, 37, 0x7B9000, 0x32E1C)
        check(sha(package) == '46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76', 'Actual shared player package')
        root = u(package, 0x78)
        check(root == 0x72C and struct.unpack_from('<h', package, root)[0] == 11 and package[root + 4] == 1, 'Resident16 native type1 and11frames')
        frames = []
        geometry = u(package, 0x34)
        for index in range(11):
            packed = u(package, root + 0x24 + 4 * index)
            target = geometry + (packed & 0x1FFFFF)
            check(target % 2 == 0 and geometry <= target < len(package) - 1, 'Every resident16 packed frame within native geometry')
            frames.append(dict(index=index, packedSourceWord=f'{packed:08X}', packageTarget=target))
        for aid in (16, 33, 34, 35):
            check(exe[0x5CCA2 + aid * 4:0x5CCA4 + aid * 4] == bytes([10, 4]), 'Native metadata maxframe10 and4ticks')
        directory = logical(f, 37, 0, 0x800)
        rows = []
        for ordinal in range(35):
            level = (ordinal // 6 + 1) * 10 + ordinal % 6
            offset, length = struct.unpack_from('<II', directory, (10 + ordinal * 2) * 8)
            data = logical(f, 37, offset, length)
            stream = data[u(data, 0x50):u(data, 0x54)]
            ids, cursor = [], 0
            while u(stream, cursor) != 0xFFFFFFFF:
                ids.append(u(stream, cursor)); cursor += 8 + u(stream, cursor + 4)
            normal = level % 10 <= 4
            for previous_local, previous_alias in [(False, False), (True, False), (False, True)]:
                before = execute(original, stream, package, level, previous_local, previous_alias)
                after = execute(patched, stream, package, level, previous_local, previous_alias)
                check(before.bytes(LOCAL, len(stream)) == after.bytes(LOCAL, len(stream)), 'Every actual source local record/fixup byte unchanged')
                check(before.bytes(BASE, 0x38) == after.bytes(BASE, 0x38) and before.bytes(BASE + 0xF0, len(package) - 0xF0) == after.bytes(BASE + 0xF0, len(package) - 0xF0), 'Resident player payload/geometry unchanged')
                for aid in range(46):
                    expected = before.read(BASE + 0x38 + 4 * aid)
                    if normal and aid in (33, 34, 35) and aid not in ids and expected == 0:
                        expected = BASE + root
                    check(after.read(BASE + 0x38 + 4 * aid) == expected, 'Only missing normal33..35 alias to resident16')
            rows.append(dict(levelId=level, nativeCourseUnchanged=not normal, localAnimationIds=ids, scalarVerified=True, runtimeVerified=False))
        # Issued fresh65 uses the same player boilerplate, but source factored
        # from14 here is a scalar loader fixture, not a fresh gameplay claim.
        stream = struct.pack('<i', -1)
        for level in [9, 10, 14, 15, 25, 35, 45, 55, 64, 65, 66, 99, 0xFFFFFFFF]:
            after = execute(patched, stream, package, level, True, False)
            admitted = level == 65 or 10 <= level <= 64 and level % 10 <= 4
            for aid in (33, 34, 35): check(after.read(BASE + 0x38 + 4 * aid) == (BASE + root if admitted else 0), 'Bounded normal/fresh classifier, stale global ignored')
        fresh_receipt = None
        if '--fresh-bin' in sys.argv:
            candidate = Path(sys.argv[sys.argv.index('--fresh-bin') + 1])
            with candidate.open('rb') as fresh:
                candidate_hash = hashlib.file_digest(fresh, 'sha256').hexdigest()
                fresh_directory = logical(fresh, 37, 0, 0x800)
                fresh_offset, fresh_length = struct.unpack_from('<II', fresh_directory, 80 * 8)
                check(fresh_offset > 0 and 0x2800 <= fresh_length <= 0x800000, 'Actual issued fresh65 WAD80 body owner')
                data = logical(fresh, 37, fresh_offset, fresh_length)
                check(struct.unpack_from('<H', data, 0x150)[0] == 0, 'Actual fresh65 special player class0')
                actual_stream = data[u(data, 0x50):u(data, 0x54)]
                ids, cursor = [], 0
                while u(actual_stream, cursor) != 0xFFFFFFFF:
                    ids.append(u(actual_stream, cursor)); cursor += 8 + u(actual_stream, cursor + 4)
                check(cursor + 4 == len(actual_stream), 'Actual fresh65 stream exact native terminator')
                before = execute(original, actual_stream, package, 65, True, False)
                after = execute(patched, actual_stream, package, 65, True, False)
                check(before.bytes(LOCAL, len(actual_stream)) == after.bytes(LOCAL, len(actual_stream)), 'Every actual fresh65 local frame fixup unchanged')
                for aid in range(46):
                    expected = before.read(BASE + 0x38 + 4 * aid)
                    if aid in (33, 34, 35) and aid not in ids and expected == 0: expected = BASE + root
                    check(after.read(BASE + 0x38 + 4 * aid) == expected, 'Actual fresh65 only missing33..35 alias')
                fresh_receipt = dict(candidate=str(candidate.resolve()), candidateSha256=candidate_hash,
                    wadEntry=80, dataWadOffset=fresh_offset, dataByteLength=fresh_length,
                    dataSha256=sha(data), playerStreamSha256=sha(actual_stream), localAnimationIds=ids,
                    scalarVerified=True, runtimeVerified=False)
        f.seek(0)
        check(hashlib.file_digest(f, 'sha256').hexdigest() == SOURCE_HASH, 'Source unchanged after verification')
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(dict(sourceSha256=SOURCE_HASH, originalLoaderSha256=LOADER_HASH,
        patchedLoaderSha256=sha(patched), loaderAddress=f'{ENTRY:08X}', loaderBytes=len(patched),
        controllerStates=[32,33,34], missingNormalAnimationSlots=[33,34,35], residentAliasSlot=16,
        residentFrameCount=11, residentFrameTargets=frames, addedLevelBytes=0, checks=checks,
        rows=rows, fresh65ActualCandidate=fresh_receipt, scalarOnly=True, runtimeVerified=False), indent=2))
    print(f'NATIVE_FREE_FLIGHT_LOADER_SCALAR_OK checks={checks} levels=35 fresh65Classifier=True addedLevelBytes=0')

def source_free():
    """Synthetic buffers exercise full original/replaced integer code, with
    no source assets. Actual-source mode remains a separate stronger check."""
    emitted = next(w for w in emission() if w['Name'] == 'normal-resident-airborne-loader')
    original, patched = bytes.fromhex(emitted['before']), bytes.fromhex(emitted['after'])
    check(sha(original) == LOADER_HASH and len(patched) == 432, 'Bounded instruction preimage')
    package = bytearray(0x1000)
    struct.pack_into('<I', package, 0, 46)
    struct.pack_into('<I', package, 0x34, 0x800)
    for aid in range(46): struct.pack_into('<I', package, 0x38 + 4 * aid, 0xFFFFFFFF)
    struct.pack_into('<I', package, 0x78, 0x100)
    def stream(ids):
        out = bytearray()
        for aid in ids:
            frames = [1, 2, 11, 20][aid % 4]
            blob = bytearray(0x24 + 4 * frames + 0x100)
            struct.pack_into('<h', blob, 0, frames); blob[4] = 1
            for field, value in [(0x10, 0x40), (0x14, 0x80), (0x18, 0xC0)]:
                struct.pack_into('<I', blob, field, value)
            for frame in range(frames):
                struct.pack_into('<I', blob, 0x24 + 4 * frame, (frame % 4) * 0x20 | ((0x53 + frame) << 24))
            out += struct.pack('<II', aid, len(blob)) + blob
        return bytes(out + struct.pack('<i', -1))
    cases = 0
    for level in [0, 9, *range(10, 67), 99, 0xFFFFFFFF]:
        admitted = level == 65 or 10 <= level <= 64 and level % 10 <= 4
        for ids in [[], [9, 18, 19, 23], [33, 34, 35], [9, 18, 33, 34, 35, 45]]:
            payload = stream(ids)
            for old_local, old_alias in [(False, False), (True, False), (False, True)]:
                before = execute(original, payload, package, level, old_local, old_alias)
                after = execute(patched, payload, package, level, old_local, old_alias)
                check(before.bytes(LOCAL, len(payload)) == after.bytes(LOCAL, len(payload)), 'All synthetic frame flag/rebase results identical')
                for aid in range(46):
                    expected = before.read(BASE + 0x38 + 4 * aid)
                    if admitted and aid in (33, 34, 35) and aid not in ids and expected == 0:
                        expected = BASE + 0x100
                    check(after.read(BASE + 0x38 + 4 * aid) == expected, 'Only admitted missing slots alias; existing local precedence')
                cases += 1
    receipt = dict(cases=cases, checks=checks, sourceFree=True, scalarOnly=True, runtimeVerified=False,
        originalLoaderSha256=LOADER_HASH, patchedLoaderSha256=sha(patched), addedLevelBytes=0)
    if len(sys.argv) >= 3 and sys.argv[2] != '--dll':
        target = Path(sys.argv[2]); target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(receipt, indent=2))
    print(f'NATIVE_FREE_FLIGHT_LOADER_SOURCE_FREE_OK checks={checks} cases={cases}')

if __name__ == '__main__':
    if len(sys.argv) >= 2 and sys.argv[1] == '--source-free': source_free()
    else: main()
