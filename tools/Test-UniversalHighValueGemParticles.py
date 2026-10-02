#!/usr/bin/env python3
"""Actual source-native type12 constructors, allocators, updates and pool retirement.

No emulator/GTE timing or visual claim. Matrix/vector transforms and PRNG are
explicit ABI doubles; all colour aging and native pool bookkeeping execute the
original instructions. --owners is private output from the source smoke tool.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path
from SpyroEggScalarMips import Scalar, require

ap = argparse.ArgumentParser()
ap.add_argument('--source-image', required=True)
ap.add_argument('--owners', required=True)
ap.add_argument('--receipt')
args = ap.parse_args()
with open(args.source_image, 'rb') as stream:
    def read(lba, size):
        out = bytearray()
        for n in range((size+2047)//2048):
            stream.seek((lba+n)*2352+24)
            out.extend(stream.read(2048))
        return bytes(out[:size])
    pvd = read(16, 2048)
    r = pvd[156:190]
    root = read(struct.unpack_from('<I', r, 2)[0], struct.unpack_from('<I', r, 10)[0])
    files = {}
    p = 0
    while p < len(root):
        n = root[p]
        if not n:
            p = (p//2048+1)*2048
            continue
        r = root[p:p+n]
        p += n
        files[r[33:33+r[32]].decode(errors='replace')] = (struct.unpack_from('<I', r, 2)[0], struct.unpack_from('<I', r, 10)[0])
    exe = read(*next(v for k, v in files.items() if k.startswith('SCUS_942.28')))
    wad = read(*next(v for k, v in files.items() if k.startswith('WAD.WAD')))
require(hashlib.sha256(exe).hexdigest() == 'a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998', 'original USA executable')
owners = json.loads(Path(args.owners).read_text())
catalog = json.loads((Path(__file__).resolve().parent.parent/'spyro-level-catalog.json').read_text())['levels']
levels = {r['key']: r for r in catalog if r['sourceWadEntry'] > 0}
require(len(owners) == 35 and {r['Level'] for r in owners} == set(levels), 'complete 35-level particle owner inventory')
ACTOR, POOL, SP, STOP = 0x80110000, 0x80120000, 0x801E0800, 0x80010004
checks = 0
cases = []

def check(b, label):
    global checks
    require(b, label)
    checks += 1

def live(m):
    count = 0
    for n in range(256):
        tag = m.read(POOL+n*32+1, 1)
        if tag == 255:
            return count
        if tag < 128:
            count += 1
    require(m.read(0x80075738) == POOL+0x2000, 'full native pool must end exactly at its allocation bound')
    return count

for own in owners:
    lev = levels[own['Level']]
    offset, size = struct.unpack_from('<II', wad, (lev['sourceWadEntry']-1)*8)
    ov = wad[offset:offset+size]
    check(ov == bytes.fromhex(own['OverlayBytes']), 'private owner bytes independently match actual source disc')
    for prefix in ['Spawn', 'Update']:
        data = ov[own[prefix]-0x8007AA38:own[prefix]-0x8007AA38+own[prefix+'Length']]
        check(hashlib.sha256(data).hexdigest() == own[prefix+'Sha256'], 'complete native particle function source owner')
        check(struct.unpack_from('<I', ov, own[prefix+'Table']-0x8007AA38+48)[0] == own[prefix+'Case'], 'independent native type12 table dispatch')
    for cls, colour, count, full in [(cls, colour, count, full) for cls, colour in [(0x1CF, 0x2C808000), (0x1DA, 0x2C808080)] for count in [1, 6] for full in [False, True]]:
        m = Scalar({0x8007AA38+i: struct.unpack_from('<I', ov, i)[0] for i in range(0, len(ov)-3, 4)})
        # Pool allocator and retirement execute their authenticated original code.
        m.code.update({pc: struct.unpack_from('<I', exe, pc-0x8000F800)[0] for pc in range(0x80053570, 0x800536A4, 4)})
        m.map(0x80060000, 0x20000)
        for i, byte in enumerate(ov):
            m.memory[0x8007AA38+i] = byte
        m.map(ACTOR, 0x600)
        m.map(POOL, 0x2020) # Native update reads the trailing terminating tag at full capacity.
        m.poke(POOL+0x2001, 255, 1)
        m.map(0x801E0000, 0x1000)
        m.poke(ACTOR+0x36, cls, 2)
        for n, value in enumerate([10000, 20000, 30000]):
            m.poke(ACTOR+12+n*4, value)
        m.poke(0x80075824, POOL)
        m.poke(0x80075738, POOL)
        m.poke(POOL+1, 255, 1)
        if full:
            # The source allocator uses bounded random replacement at capacity.
            # Fill all256 records with valid type12 stars, with no external sentinel.
            for record in range(256):
                particle = POOL+record*32
                for offset, value in [(0, 12), (1, 2), (3, 1), (12, 128), (13, 128), (14, 128)]:
                    m.poke(particle+offset, value, 1)
            m.poke(0x80075738, POOL+0x2000)
        m.r[4:8] = [count, 12, ACTOR, colour]
        m.r[29], m.r[31] = SP, STOP
        external = set()
        for pc in range(own['Spawn'], own['Spawn']+own['SpawnLength'], 4):
            w = m.code[pc]
            if w >> 26 == 3:
                target = 0x80000000 | ((w & 0x3FFFFFF) << 2)
                if target not in m.code:
                    external.add(target)
        pc = own['Spawn']
        helpers = set()
        while True:
            target = m.run(pc, {STOP} | external)
            if target == STOP:
                break
            pc = m.r[31]
            helpers.add(target)
            # The actor class chooses the native generic-position path, which
            # consumes matrix/vector helpers. These doubles do not certify GTE math.
            if target in (0x80017700, 0x80017758):
                for n in range(3):
                    m.poke(m.r[4]+n*4, m.read(m.r[5]+n*4))
            elif target in (0x80017048, 0x80017BFC):
                pass
            elif target == 0x8006272C:
                m.r[2] = 8
            else:
                raise AssertionError(f'Unowned constructor helper {target:08X}')
        check(live(m) == (256 if full else count), 'exact source particle count / bounded full-pool reuse')
        check(m.read(ACTOR+0x36, 2) == cls, 'constructor never changes actor class')
        check(not any(a <= ACTOR < a+n or ACTOR <= a < ACTOR+0x58 for a, n, _ in m.writes), 'constructor never writes its parent actor')
        check(not any(v == ACTOR for _, _, v in m.writes), 'native particle stores no parent actor pointer')
        written_records = sorted({POOL+(address-POOL)//32*32 for address, size, value in m.writes if POOL <= address < POOL+0x2000})
        written_records = [particle for particle in written_records if m.read(particle+1, 1) < 128]
        check(bool(written_records) and len(written_records) <= count, 'constructor writes only its allocated/reused records')
        check(not any(POOL <= address < POOL+0x2100 and address+size > POOL+0x2000 for address, size, value in m.writes), 'full-pool replacement never writes beyond the native pool')
        for particle in written_records:
            check(m.read(particle, 1) == 12 and m.read(particle+1, 1) == 2 and m.read(particle+3, 1) == 1, 'native type12 allocation and state')
            check(m.read(particle+12, 1) == (colour & 255) and m.read(particle+13, 1) == (colour >> 8 & 255) and m.read(particle+14, 1) == (colour >> 16 & 255), 'exact authored native collection star RGB')
        # Execute each destination's complete original update loop, including its
        # original shared pool delete helper. No particle aging/deletion double.
        for frame in range(1, 17):
            m.r[4], m.r[29], m.r[31] = 1, SP, STOP
            check(m.run(own['Update'], {STOP}) == STOP, 'native particle update returns')
            check(live(m) == ((256 if full else count) if frame < 16 else 0), 'bounded native fade and retirement without RGB wrap')
        check(m.read(POOL+1, 1) == 255 and m.read(0x80075738) == POOL, 'native allocator returns to empty reusable pool')
        cases.append({'Level': own['Level'], 'GemClass': cls, 'Particles': count, 'FullPool': full, 'RetiredAfterUpdates': 16,
                      'TransformAndPrngHelpers': sorted(helpers)})
receipt = {'Marker': 'HIGH_VALUE_GEM_NATIVE_PARTICLES_OK', 'Checks': checks, 'Cases': cases,
           'Evidence': 'original 35-level type12 constructors, allocation, RGB update and native pool retirement execute; transform/vector/PRNG helpers are ABI doubles; not GTE timing, visual or gameplay acceptance',
           'SourceExecutableSha256': hashlib.sha256(exe).hexdigest(), 'OwnerReceiptSha256': hashlib.sha256(Path(args.owners).read_bytes()).hexdigest(), 'RuntimeStatus': 'not-tested'}
if args.receipt:
    Path(args.receipt).write_text(json.dumps(receipt, indent=2)+'\n')
print(json.dumps({'Marker': receipt['Marker'], 'Checks': checks, 'Cases': len(cases)}))
