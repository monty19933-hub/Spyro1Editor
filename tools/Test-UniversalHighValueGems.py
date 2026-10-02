#!/usr/bin/env python3
"""Strict scalar proof of emitted high-value gem instructions and source-native awards.

No emulator, BIOS, save states, card writes or host RAM. Original FreeMoby/MarkCollected
and canonical save-map gateway execute; native audio, particle and HUD text calls are
explicit ABI effect doubles. This is instruction/accounting evidence, not visual or
memory-card acceptance. Pass a fresh --emit result from UniversalHighValueGemSmoke.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path
from SpyroEggScalarMips import Scalar, require

ap = argparse.ArgumentParser()
ap.add_argument('--source-image', required=True)
ap.add_argument('--emitted', required=True)
ap.add_argument('--receipt')
args = ap.parse_args()

# Independent read-only MODE2/2352 ISO extraction.
with open(args.source_image, 'rb') as stream:
    def read(lba, size):
        out = bytearray()
        for sector in range((size + 2047) // 2048):
            stream.seek((lba + sector) * 2352 + 24)
            out.extend(stream.read(2048))
        return bytes(out[:size])
    pvd = read(16, 2048)
    r = pvd[156:190]
    root = read(struct.unpack_from('<I', r, 2)[0], struct.unpack_from('<I', r, 10)[0])
    pos = 0
    exe = None
    while pos < len(root):
        n = root[pos]
        if not n:
            pos = (pos // 2048 + 1) * 2048
            continue
        r = root[pos:pos+n]
        pos += n
        if r[33:33+r[32]].startswith(b'SCUS_942.28'):
            exe = read(struct.unpack_from('<I', r, 2)[0], struct.unpack_from('<I', r, 10)[0])
            break
require(exe is not None and hashlib.sha256(exe).hexdigest() ==
        'a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998', 'original source executable')
a = json.loads(Path(args.emitted).read_text())
BASE, ACTORS, PROPS, STACK, STOP = a['Base'], 0x80110000, 0x80118000, 0x801E0800, 0x80010004
MARK, FREE, PARTICLE, TEXT, FLUSH = 0x8003B854, 0x80052568, 0x800D0000, 0x800181AC, 0x80018880
AUDIO = {0x800557CC, 0x80055A78}
CLASSES = a['Classes']
NATIVE_FREE = exe[FREE-0x8000F800:FREE-0x8000F800+0x64]
NATIVE_MARK = exe[MARK-0x8000F800:MARK-0x8000F800+0x180]
checks = 0

def check(b, label):
    global checks
    require(b, label)
    checks += 1

def words(blob, address):
    return {address+i: struct.unpack_from('<I', blob, i)[0] for i in range(0, len(blob), 4)}

def instruction(op, rs, rt, imm):
    return (op << 26) | (rs << 21) | (rt << 16) | (imm & 65535)

def fixture(cls, physical=0, logical=None, level=0, saved=False, position=(0, 0, 0)):
    remap = logical is not None
    logical = physical if logical is None else logical
    update = bytes.fromhex(a['RemappedUpdate' if remap else 'Update'])
    mark = bytes.fromhex(a['RemappedMark']) if remap else NATIVE_MARK
    m = Scalar(words(update, BASE) | words(mark, MARK) | words(NATIVE_FREE, FREE))
    m.map(0x80060000, 0x20000)
    m.map(ACTORS, 0xC000)
    m.map(0x801E0000, 0x1000)
    actor = ACTORS + physical*0x58
    prop = PROPS + physical*16
    for reg in range(1, 32):
        m.r[reg] = 0xB0000000 + reg*0x10201
    m.r[29], m.r[31] = STACK, STOP
    m.poke(0x80075828, ACTORS)
    m.poke(0x80075890, ACTORS+(physical+1)*0x58)
    m.poke(0x80075964, level)
    m.poke(0x80075778, 0x80119200)
    m.poke(0x80119200, actor)
    m.poke(0x800761D4, 0x8006FFF0)
    m.poke(0x800758E4, PARTICLE)
    m.poke(0x80075824, 0x80120000)
    m.poke(0x80075738, 0x80120000)
    m.poke(actor, prop)
    m.poke(actor+0x36, cls, 2)
    m.poke(actor+0x34, 0, 2)
    for k, v in enumerate(position):
        m.poke(actor+12+k*4, v)
    if remap:
        # Execute the canonical gateway and a bounded ushort physical->logical leaf.
        count = physical+1
        table, leaf, metadata = 0x80119000, 0x800A0000, 0x8011A000
        for n in range(count):
            m.poke(table+n*2, logical if n == physical else n, 2)
        leaf_words = [instruction(11, 6, 2, count), instruction(4, 2, 0, 9),
                      6 << 16 | 2 << 11 | 1 << 6,
                      instruction(15, 0, 3, 0x8012), instruction(9, 3, 3, -0x7000),
                      2 << 21 | 3 << 16 | 2 << 11 | 0x21, instruction(33, 2, 6, 0),
                      31 << 21 | 8, 0, 31 << 21 | 8, instruction(9, 0, 6, -1)]
        # In-range cases do not use the invalid branch; the bounded leaf matches
        # NativeMobySaveMap.Append's load-delay-safe address/count convention.
        m.code.update({leaf+i*4: w for i, w in enumerate(leaf_words)})
        m.poke(0x80075734, metadata)
        m.poke(metadata+8, 0x50414D53)
        m.poke(metadata+12, leaf)
    if saved:
        m.poke(0x80077908+level*32+(logical//32)*4, 1 << (logical & 31))
    return m, actor, prop, logical


def run(m, kind='Update', native_text=False):
    code = bytes.fromhex(a[kind])
    m.code.update(words(code, BASE))
    if native_text:
        m.code.update(words(exe[TEXT-0x8000F800:TEXT-0x8000F800+0x2A0], TEXT))
    initial = m.r.copy()
    m.r[31] = STOP
    effects = []
    pc = BASE
    while True:
        stops = {STOP, PARTICLE, FLUSH} | AUDIO | ({0x80016914, 0x80017700} if native_text else {TEXT})
        target = m.run(pc, stops)
        if target == STOP:
            break
        effect = {'Target': target, 'Arguments': m.r[4:8].copy()}
        if target == TEXT:
            p = m.r[4]
            text = bytearray()
            while m.read(p, 1):
                text.append(m.read(p, 1))
                p += 1
            effect['Text'] = text.decode('ascii')
            effect['Position'] = [m.read(m.r[5]+n*4) for n in range(3)]
            effect['Step'] = [m.read(m.r[6]+n*4) for n in range(3)]
            effect['Style'] = m.read(m.r[29]+16)
        if target == 0x80016914:
            for n in range(m.r[6]):
                m.poke(m.r[4]+n, m.r[5], 1)
        elif target == 0x80017700:
            for n in range(3):
                m.poke(m.r[4]+n*4, m.read(m.r[5]+n*4))
        effects.append(effect)
        pc = m.r[31]
        # Real native calls can consume all four argument home words and volatile
        # registers. Generated routines preserve actor/list/property ownership.
        for n in range(0, 16, 4):
            m.poke(m.r[29]+n, 0xCCCC0000+n)
        for reg in list(range(1, 16)) + [24, 25]:
            m.r[reg] = 0xDD000000+reg*0x10001
    for reg in list(range(16, 24))+[28, 29, 30]:
        check(m.r[reg] == initial[reg], f'{kind} callee-saved register{reg}')
    check(m.r[31] == STOP, 'native return address')
    return effects

cases = []
for cls, value, text in zip(CLASSES, [50, 100], ['50', '100']):
    for level in [0, 7, 35]:
        for physical, logical in [(0, None), (31, None), (32, None), (255, None), (0, 195), (1, 255)]:
            m, actor, prop, bit = fixture(cls, physical, logical, level)
            code_kind = 'RemappedUpdate' if logical is not None else 'Update'
            effects = run(m, code_kind)
            check(m.read(0x80075860) == value, 'exact native global gem increment')
            check(m.read(0x80077420+level*4) == value, 'exact native current-level increment')
            check(m.read(0x80077908+level*32+(bit//32)*4) == 1 << (bit & 31), 'native durable logical saved bit')
            check(m.read(0x800778B8+(bit//32)*4) == 1 << (bit & 31), 'native checkpoint logical saved bit')
            check(m.read(actor+0x48, 1) == 0xFD and m.read(actor+0x36, 2) == cls, 'native placed retirement preserves gem class')
            check(m.read(actor) == prop and m.read(prop) == 45, 'native retirement preserves popup properties')
            stars = [e for e in effects if e['Target'] == PARTICLE]
            check(len(stars) == 1 and stars[0]['Arguments'][:3] == [6, 12, actor], 'native gem particle kind and physical actor')
            colour = stars[0]['Arguments'][3]
            check(all((colour >> shift & 255) % 8 == 0 for shift in [0, 8, 16]), 'native subtract-eight RGB lifetime terminates')
            check(m.read(0x800756C8) == 0 and m.read(0x80077DB8, 1) == 0, 'retail recent-gem ring untouched')
            drawn = [e for e in run(m, 'Draw') if e['Target'] == TEXT]
            check(len(drawn) == 1 and drawn[0]['Text'] == text, 'exact native HUD digit text')
            check(drawn[0]['Position'][1:] == [97, 0x1100] and drawn[0]['Step'] == [16, 0, 0x1100], 'bounded popup geometry and step ABI')
            for frame in range(45):
                run(m, code_kind)
            check(m.read(prop) == 0, 'popup expires offscreen without draw calls')
            check(m.read(0x80075860) == value, 'no duplicate award while remaining in pickup radius')
            check(not any(e['Target'] == TEXT for e in run(m, 'Draw')), 'expired popup emits no HUD letters')
            cold, actor2, prop2, bit2 = fixture(cls, physical, logical, level, saved=True)
            before = cold.read(0x80077908+level*32+(bit2//32)*4)
            loaded = run(cold, code_kind)
            check(cold.read(actor2+0x48, 1) == 0xFD, 'saved gem suppressed on new native row setup')
            check(cold.read(0x80075860) == 0 and cold.read(0x80077420+level*4) == 0, 'saved gem cannot award again')
            check(cold.read(prop2) == 0 and not any(e['Target'] == PARTICLE for e in loaded), 'saved gem produces no popup or effect')
            check(cold.read(0x80077908+level*32+(bit2//32)*4) == before, 'saved bitmap unchanged by suppression')
            cases.append({'Class': cls, 'Value': value, 'LevelIndex': level, 'PhysicalRow': physical, 'LogicalRow': bit, 'Popup': text})
# Contact boundary, unknown classes and inactive states must not award.
for cls in CLASSES:
    for position in [(640, 0, 0), (0, -640, 0), (0, 0, 896), (-640, 0, 0)]:
        m, actor, prop, _ = fixture(cls, position=position)
        e = run(m)
        check(m.read(0x80075860) == 0 and m.read(actor+0x48, 1) == 0, 'outside native authored pickup boundary')
        stars = [v for v in e if v['Target'] == PARTICLE]
        check(m.read(prop) == 0 and len(stars) == 1 and stars[0]['Arguments'][:3] == [1, 12, actor], 'nearby uncollected boundary gem emits one idle star without popup')
# Idle stars are transient, nearby and throttled independently of draw calls.
idle_cases = []
for cls in CLASSES:
    m, actor, prop, _ = fixture(cls, position=(2048, 0, 0))
    emitted_frames = []
    for frame in range(65):
        effects = [e for e in run(m) if e['Target'] == PARTICLE]
        if effects:
            emitted_frames.append(frame)
            check(len(effects) == 1 and effects[0]['Arguments'][:3] == [1, 12, actor], 'single native idle star per gem')
        check(m.read(actor+0x48, 1) == 0 and m.read(prop) == 0, 'idle gem remains active without collection popup')
        check(m.read(0x80075860) == 0 and m.read(0x80077908) == 0, 'idle sparkle changes no treasure or saved identity')
    check(emitted_frames == [0, 32, 64], 'deterministic thirty-two-update idle sparkle cadence')
    cooldown = m.read(prop+4)
    check(not any(e['Target'] == TEXT for e in run(m, 'Draw')) and m.read(prop+4) == cooldown, 'active cooldown survives render hook without popup')
    m.poke(actor+12, 0)
    pickup = [e for e in run(m) if e['Target'] == PARTICLE]
    check(len(pickup) == 1 and pickup[0]['Arguments'][:3] == [6, 12, actor], 'pickup replaces idle sparkle with unchanged six-star burst')
    for frame in range(32):
        check(not any(e['Target'] == PARTICLE for e in run(m)), 'retired gem never emits an idle star')
    idle_cases.append({'Class': cls, 'EmissionUpdates': emitted_frames, 'PickupBurst': 6})
for cls in CLASSES:
    for position in [(4096, 0, 0), (0, -4096, 0), (0, 0, 4096), (10000, 10000, 10000)]:
        m, actor, prop, _ = fixture(cls, position=position)
        check(not run(m) and m.read(prop+4) == 0, 'distant gem does not emit or accumulate invisible sparkle timers')
    for address in [0x80075690, 0x800757D8]:
        m, actor, prop, _ = fixture(cls, position=(2048, 0, 0))
        m.poke(address, 1)
        check(not run(m) and m.read(prop+4) == 0, 'modal/non-gameplay idle particle gate')
    m, actor, prop, _ = fixture(cls, position=(2048, 0, 0))
    m.poke(actor+0x48, 0x80, 1)
    check(not run(m) and m.read(prop+4) == 0, 'inactive gem does not emit idle particles')
    m, actor, prop, _ = fixture(cls, position=(2048, 0, 0))
    m.poke(0x80075738, 0x80122000)
    check(not run(m) and m.read(prop+4) == 0, 'full native pool skips idle cosmetics without evicting gameplay particles or aging cooldown')
    m.poke(0x80075738, 0x80121000)
    check(any(e['Target'] == PARTICLE for e in run(m)), 'freed native particle slot resumes pending idle sparkle')
    m.poke(0x80075738, 0x80122000)
    m.poke(actor+12, 0)
    burst = [e for e in run(m) if e['Target'] == PARTICLE]
    check(len(burst) == 1 and burst[0]['Arguments'][0] == 6, 'collection retains native burst at pool capacity')
# All 256 simultaneous nearby gems share the per-scan emission budget. Both
# values get a native star; no callback keeps actor pointers or modifies saves.
m, actor, prop, _ = fixture(CLASSES[0], physical=255, position=(2048, 0, 0))
for row in range(256):
    moby, properties = ACTORS+row*0x58, PROPS+row*16
    m.poke(moby, properties)
    m.poke(moby+0x36, CLASSES[row % 2], 2)
    m.poke(moby+12, 2048)
sparkled = []
for frame in range(64):
    effects = [e for e in run(m) if e['Target'] == PARTICLE]
    check(len(effects) <= 8, 'eight idle native particles per scan across all gems')
    sparkled.extend(e['Arguments'][2] for e in effects)
    check(m.read(0x80075860) == 0 and m.read(0x80077908) == 0, 'dense idle gems preserve accounting')
check(all(sparkled.count(ACTORS+row*0x58) == 2 for row in range(256)), 'all 256 nearby gems each receive bounded repeated idle stars')
for cls in [0x53, 0x54, 0x55, 0x56, 0x57, 0x1FE]:
    m, actor, prop, _ = fixture(cls)
    check(not run(m) and m.read(actor+0x48, 1) == 0 and m.read(prop) == 0, 'retail five gems and inert actors untouched')
# Execute the actual original SpawnText instructions too: its step Z replaces
# subsequent digit depths, rather than adding an ordinary position delta.
# This guards the observed native scale/diagonal bug that an ABI double misses.
for cls, text in zip(CLASSES, ['50', '100']):
    m, actor, prop, _ = fixture(cls)
    run(m)
    hud_end = 0x8011B000
    m.poke(0x80075710, hud_end)
    run(m, 'Draw', native_text=True)
    check(m.read(0x80075710) == hud_end-len(text)*0x58, 'native text allocates exact numeral count')
    positions = []
    for n, digit in enumerate(text):
        glyph = hud_end-(n+1)*0x58
        check(m.read(glyph+0x36, 2) == ord(digit)+0xD4, 'actual native SpawnText numeral class')
        check(m.read(glyph+0x4F, 1) == 0xB, 'actual native SpawnText style')
        positions.append([m.read(glyph+12+k*4) for k in range(3)])
    check(all(p[1:] == [97, 0x1100] for p in positions), 'all native digits share height and depth/size')
    check([p[0] for p in positions] == ([238, 256] if cls == CLASSES[0] else [266, 284, 302]), 'actual native horizontal numeral spacing')
    for address in [0x80075690, 0x800757D8]:
        m.poke(address, 1)
        before = m.read(0x80075710)
        effects = run(m, 'Draw', native_text=True)
        check(not effects and m.read(0x80075710) == before, 'native non-gameplay/modal classifier suppresses popup')
        m.poke(address, 0)
# Busy native HUD skips additional text while the update still ages its popup.
m, actor, prop, _ = fixture(CLASSES[0])
run(m)
for i in range(150):
    m.poke(0x800720F4+i*4, 0x80110000)
check(not any(e['Target'] == TEXT for e in run(m, 'Draw')), 'native HUD capacity protected')
receipt = {'Marker': 'HIGH_VALUE_GEM_NATIVE_SCALAR_OK', 'Evidence':
           'source-native award/retirement and emitted update/draw; strict integer branch/load delays; audio/particle/HUD text are ABI effect doubles, not gameplay/card/visual acceptance',
           'Checks': checks, 'Cases': cases, 'IdleCases': idle_cases, 'SourceExecutableSha256': hashlib.sha256(exe).hexdigest(),
           'EmittedSha256': hashlib.sha256(Path(args.emitted).read_bytes()).hexdigest(), 'RuntimeStatus': 'not-tested'}
if args.receipt:
    Path(args.receipt).write_text(json.dumps(receipt, indent=2)+'\n')
print(json.dumps({'Marker': receipt['Marker'], 'Checks': checks, 'Cases': len(cases)}))
