#!/usr/bin/env python3
"""Strict scalar proof of emitted high-value gem instructions and source-native awards.
The retail gems' own idle wobble (Artisans overlay) runs beside the emitted wobble at every phase.

No emulator, BIOS, save states, card writes or host RAM. Original FreeMoby/MarkCollected
and canonical save-map gateway execute; native sound, particle, trigonometry and actor allocation calls are
explicit ABI effect doubles. Original amount-to-numeral popup instructions execute. This is instruction/accounting evidence, not visual or
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
    exe = wad_lba = None
    while pos < len(root):
        n = root[pos]
        if not n:
            pos = (pos // 2048 + 1) * 2048
            continue
        r = root[pos:pos+n]
        pos += n
        if r[33:33+r[32]].startswith(b'SCUS_942.28'):
            exe = read(struct.unpack_from('<I', r, 2)[0], struct.unpack_from('<I', r, 10)[0])
        elif r[33:33+r[32]].startswith(b'WAD.WAD'):
            wad_lba = struct.unpack_from('<I', r, 2)[0]
    # Artisans' level code (WAD entry 9, loaded at 0x8007AA38): the retail gems' idle wobble.
    require(wad_lba is not None, 'WAD.WAD')
    overlay_offset, overlay_size = struct.unpack_from('<II', read(wad_lba, 2048), 9 * 8)
    stream.seek(0)
    ARTISANS_OVERLAY = read(wad_lba + overlay_offset // 2048, overlay_size)
require(exe is not None and hashlib.sha256(exe).hexdigest() ==
        'a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998', 'original source executable')
a = json.loads(Path(args.emitted).read_text())
BASE, ACTORS, PROPS, STACK, STOP = a['Base'], 0x80110000, 0x80118000, 0x801E0800, 0x80010004
MARK, FREE, PARTICLE, POPUP, SPAWN = 0x8003B854, 0x80052568, 0x800D0000, 0x800557CC, 0x800D1000
SOUND, TRIG = 0x80055A78, {0x80016CB0, 0x80016C58}
CLASSES = a['Classes']
NATIVE_FREE = exe[FREE-0x8000F800:FREE-0x8000F800+0x64]
NATIVE_MARK = exe[MARK-0x8000F800:MARK-0x8000F800+0x180]
NATIVE_POPUP = exe[POPUP-0x8000F800:POPUP-0x8000F800+0x190]
require(hashlib.sha256(NATIVE_POPUP).hexdigest() == "ede979bdf91a0602de89166b225633adf582a1c1a4eabd32e81f131271723709", "native amount-to-numeral owner")
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
    m = Scalar(words(update, BASE) | words(mark, MARK) | words(NATIVE_FREE, FREE) | words(NATIVE_POPUP, POPUP))
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
    m.poke(0x800758CC, SPAWN)
    m.poke(0x800758E4, PARTICLE)
    m.poke(0x80075824, 0x80120000)
    m.poke(0x80075738, 0x80120000)
    m.poke(actor, prop)
    m.poke(actor+0x36, cls, 2)
    m.poke(actor+0x34, 0, 2)
    for k, v in enumerate(position):
        m.poke(actor+12+k*4, v+(8704 if k == 2 else 0))
    m.poke(0x80078A60, 8704)
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


def run(m, kind='Update'):
    code = bytes.fromhex(a[kind])
    m.code.update(words(code, BASE))
    initial = m.r.copy()
    m.r[31] = STOP
    effects = []
    pc = BASE
    while True:
        target = m.run(pc, {STOP, PARTICLE, SOUND, SPAWN} | TRIG)
        if target == STOP:
            break
        effect = {'Target': target, 'Arguments': m.r[4:8].copy()}
        result = None
        if target == SPAWN:
            # Only native dynamic actor allocation is doubled. The original popup
            # chooses every numeral class, digit place value, velocity and position.
            number = getattr(m, 'popup_glyph_count', 0)
            glyph, properties = 0x80116000+number*0x58, 0x80117800+number*0x20
            m.poke(glyph, properties)
            m.poke(glyph+0x36, m.r[4], 2)
            effect['Glyph'], effect['Properties'] = glyph, properties
            m.popup_glyph_count = number+1
            result = glyph
        elif target in TRIG:
            result = 4096*1024
        effects.append(effect)
        pc = m.r[31]
        # Real native calls can consume all four argument home words and volatile
        # registers. Generated routines preserve actor/list/property ownership.
        for n in range(0, 16, 4):
            m.poke(m.r[29]+n, 0xCCCC0000+n)
        for reg in list(range(1, 16)) + [24, 25]:
            m.r[reg] = 0xDD000000+reg*0x10001
        if result is not None:
            m.r[2] = result
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
            check(m.read(actor) == prop and m.read(prop) == 0, 'native retirement preserves private properties without a redundant HUD timer')
            stars = [e for e in effects if e['Target'] == PARTICLE]
            check(len(stars) == 1 and stars[0]['Arguments'][:3] == [6, 12, actor], 'native gem particle kind and physical actor')
            colour = stars[0]['Arguments'][3]
            check(all((colour >> shift & 255) % 8 == 0 for shift in [0, 8, 16]), 'native subtract-eight RGB lifetime terminates')
            check(m.read(0x800756C8) == 0 and m.read(0x80077DB8, 1) == 0, 'retail recent-gem ring untouched')
            glyphs = [e for e in effects if e['Target'] == SPAWN]
            expected = [0x104, 0x109] if value == 50 else [0x104, 0x104, 0x105]
            check([m.read(e['Glyph']+0x36, 2) for e in glyphs] == expected, 'one native amount-to-numeral popup with exact50/100 digits')
            check(sum(m.read(e['Properties']+4) for e in glyphs) == value, 'native numeral place values sum to exact pickup amount')
            check(all(m.read(e['Glyph']+0x14) == m.read(actor+0x14) for e in glyphs), 'native popup uses physical gem world height')
            for frame in range(45):
                check(not any(e['Target'] == SPAWN for e in run(m, code_kind)), 'retired gem never creates duplicate native numerals')
            check(m.read(0x80075860) == value, 'no duplicate award while remaining in pickup radius')
            cold, actor2, prop2, bit2 = fixture(cls, physical, logical, level, saved=True)
            before = cold.read(0x80077908+level*32+(bit2//32)*4)
            loaded = run(cold, code_kind)
            check(cold.read(actor2+0x48, 1) == 0xFD, 'saved gem suppressed on new native row setup')
            check(cold.read(0x80075860) == 0 and cold.read(0x80077420+level*4) == 0, 'saved gem cannot award again')
            check(cold.read(prop2) == 0 and not any(e['Target'] in (PARTICLE, SPAWN) for e in loaded), 'saved gem produces no native popup or effect')
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
    m.poke(moby+20, 8704)
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
# Preserve the native popup floor guard: MarkCollected emits numerals only for
# physical actors at Z>=768. Accounting and retirement still occur below it.
for cls, value in zip(CLASSES, [50, 100]):
    for height in [0, 767, 768, 8704]:
        m, actor, prop, _ = fixture(cls)
        m.poke(actor+0x14, height)
        m.poke(0x80078A60, height)
        glyphs = [e for e in run(m) if e['Target'] == SPAWN]
        check(len(glyphs) == ((2 if value == 50 else 3) if height >= 768 else 0), 'native floor guard and exact decimal numeral count')
        check(m.read(0x80075860) == value and m.read(actor+0x48, 1) == 0xFD, 'native popup floor guard changes no pickup accounting')
# Wobble and shine. The retail gems' own idle wobble (Artisans 0x800802F4-0x80080364: tilt +0x44/+0x45 from the
# executable's cosine/sine tables, phase += 2 x frame step) runs beside the emitted update at every phase; both must
# write the same tilt bytes and advance the phase alike. Yaw stays, the teal pair sits in shine-table entry 0 and
# the rainbow gem walks entries red, gold, green, teal, blue, purple.
RETAIL_WOBBLE, RETAIL_WOBBLE_END = 0x800802F4, 0x80080364
wobble_code = ARTISANS_OVERLAY[RETAIL_WOBBLE-0x8007AA38:RETAIL_WOBBLE_END-0x8007AA38]
require(hashlib.sha256(wobble_code).hexdigest() == '16592ab3b58a710de6e2454401a9fe37f4bde86f8ff20c73d9cfcf3aa3dded52', 'retail gem idle wobble owner')
SINE_COSINE = exe[0x8006CBF8-0x8000F800:0x8006CBF8-0x8000F800+640]
require(hashlib.sha256(SINE_COSINE).hexdigest() == '795a93bee7b4d49bef89d4df8c8628a4b4b3a91d694053dbc2b26b4293d4fa55', 'native sine/cosine tables')
SHINE_TABLE = 0x8006E44C
require(exe[SHINE_TABLE-0x8000F800:SHINE_TABLE-0x8000F800+48] == bytes(8) + struct.pack('<10I', 0x50, 0xFF, 0x5000, 0xFF00, 0x500000, 0xFF0000, 0x5050, 0xFFFF, 0x500050, 0xFF00FF),
        'native shine table: entry 0 free, entries 1-5 the retail gem pairs')
TEAL_PAIR, RAINBOW_ORDER = (0x00485000, 0x00E8FF00), [1, 4, 2, 0, 3, 5]
retail = Scalar(words(wobble_code, RETAIL_WOBBLE))
retail.map(0x80060000, 0x20000)
retail.map(0x80110000, 0x100)
retail.map(0x80111000, 0x20)
for i, b in enumerate(SINE_COSINE):
    retail.poke(0x8006CBF8+i, b, 1)
retail.poke(0x800756CC, 2)

def retail_wobble(phase):
    retail.poke(0x80111000+0x11, 0, 1); retail.poke(0x80111000+0x12, 0, 1); retail.poke(0x80111000+0x13, phase, 1)
    retail.r[17], retail.r[19] = 0x80111000, 0x80110000
    require(retail.run(RETAIL_WOBBLE, {RETAIL_WOBBLE_END}) == RETAIL_WOBBLE_END, 'retail wobble window ends')
    return retail.read(0x80110044, 1), retail.read(0x80110045, 1), retail.read(0x80111013, 1)

wobble_cases = []
for cls in CLASSES:
    for position in [(2048, 0, 0), (2048, 1000, 0), (-4000, 3000, 0)]:
        m, actor, prop, _ = fixture(cls, position=position)
        for i, b in enumerate(SINE_COSINE):
            m.poke(0x8006CBF8+i, b, 1)
        m.poke(0x800756CC, 2)
        m.poke(actor+0x46, 0x5A, 1)
        offset = ((position[0] + position[1]) >> 4) & 255
        start = m.read(prop+8, 1)
        hues = []
        for update in range(64):
            stored = m.read(prop+8, 1)
            display = (stored + offset) & 255
            run(m)
            x, y, advanced = retail_wobble(display)
            check(m.read(actor+0x44, 1) == x and m.read(actor+0x45, 1) == y, 'placed gem tilts exactly like the retail gem wobble')
            check(m.read(prop+8, 1) == (stored + 4) & 255 and advanced == (display + 4) & 255, 'phase advances like the retail gem (2 x frame step)')
            check(m.read(actor+0x46, 1) == 0x5A, 'yaw untouched, as on the retail gems')
            check((m.read(SHINE_TABLE), m.read(SHINE_TABLE+4)) == TEAL_PAIR, 'teal pair in the free shine-table entry 0')
            entry = m.read(actor+0x4F, 1)
            if cls == CLASSES[1]:
                check(entry == RAINBOW_ORDER[display*6 >> 8], 'rainbow entry follows its phase')
                hues.append(entry)
            else:
                check(entry == 0, 'teal keeps shine-table entry 0')
            check(m.read(0x80075860) == 0 and m.read(actor+0x48, 1) == 0, 'wobble changes no award or state')
        check(m.read(prop+8, 1) == start, 'one wobble turn takes 64 updates (about 2.1 seconds at 30 updates a second)')
        if cls == CLASSES[1]:
            check([h for k, h in enumerate(hues) if k == 0 or hues[k-1] != h][:6] == RAINBOW_ORDER[RAINBOW_ORDER.index(hues[0]):] + RAINBOW_ORDER[:RAINBOW_ORDER.index(hues[0])], 'rainbow walks all six hues in order')
        wobble_cases.append({'Class': cls, 'Position': position, 'Updates': 64, 'Hues': sorted(set(hues))})
for cls in [0x53, 0x54, 0x55, 0x56, 0x57]:
    m, actor, prop, _ = fixture(cls, position=(2048, 0, 0))
    m.poke(actor+0x44, 0x1234, 2); m.poke(actor+0x4F, cls-0x52, 1)
    run(m)
    check(m.read(actor+0x44, 2) == 0x1234 and m.read(actor+0x4F, 1) == cls-0x52 and m.read(prop+8, 1) == 0, 'retail gems keep their own wobble and colour')
receipt = {'Marker': 'HIGH_VALUE_GEM_NATIVE_SCALAR_OK', 'Evidence':
           'source-native award/retirement/decimal-numeral popup and emitted update; strict integer branch/load delays; sound/particle/trigonometry/actor allocation are ABI effect doubles, not gameplay/card/visual acceptance',
           'Checks': checks, 'Cases': cases, 'IdleCases': idle_cases, 'WobbleCases': wobble_cases, 'SourceExecutableSha256': hashlib.sha256(exe).hexdigest(),
           'NativePopupSha256': hashlib.sha256(NATIVE_POPUP).hexdigest(),
           'EmittedSha256': hashlib.sha256(Path(args.emitted).read_bytes()).hexdigest(), 'RuntimeStatus': 'not-tested'}
if args.receipt:
    Path(args.receipt).write_text(json.dumps(receipt, indent=2)+'\n')
print(json.dumps({'Marker': receipt['Marker'], 'Checks': checks, 'Cases': len(cases), 'WobbleCases': len(wobble_cases)}))
