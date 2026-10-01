#!/usr/bin/env python3
"""Execute authenticated original native KillMoby on real emitted deletion rows.
Strict scalar MIPS evidence, not an emulator/gameplay test. No disc/card writes.
First emit with UniversalRowRemovalSmoke ORIGINAL_USA_BIN CATALOG_WORKSPACE JSON.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path
from SpyroEggScalarMips import Scalar, require

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('fixture', type=Path)
p.add_argument('source_executable', type=Path)
p.add_argument('--report', type=Path)
a = p.parse_args()
f = json.loads(a.fixture.read_text())
exe = a.source_executable.read_bytes()
source_sha = 'a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998'
require(hashlib.sha256(exe).hexdigest() == source_sha == f['sourceExecutableSha256'], 'Authenticated exact original USA EXE')
checks = 0
def check(condition, label):
    global checks
    require(condition, label)
    checks += 1

code = {}
for pc_key, data_key in [('killAddress', 'kill'), ('simpleConstructorAddress', 'simpleConstructor'), ('animatedConstructorSentinelAddress', 'animatedConstructorSentinel')]:
    pc = f[pc_key]; data = bytes.fromhex(f[data_key])
    check(exe[pc - 0x8000F800:pc - 0x8000F800 + len(data)] == data, 'Emitted window is byte-exact original native owner')
    code.update({pc + i: struct.unpack_from('<I', data, i)[0] for i in range(0, len(data), 4)})

ACTOR, GROUP, STOP = 0x80100000, 0x80110000, 0xDEADBEE0
def fixture(row):
    m = Scalar(code)
    m.map(ACTOR, 88); m.map(0x80070000, 0x10000)
    for i, b in enumerate(row): m.poke(ACTOR + i, b, 1)
    m.poke(0x80075778, GROUP); m.poke(0x80075890, ACTOR + 88)
    m.r[4], m.r[31] = ACTOR, STOP
    return m

case_receipts = []
for row in f['rows']:
    data = bytes.fromhex(row['after'])
    check(struct.unpack_from('<h', data, 0x34)[0] == -1, 'Every cleared removed row is unlinked, including rows available for reuse')
    m = fixture(data); check(m.run(f['killAddress'], {STOP}) == STOP, 'Native placed-actor kill returns normally')
    check(m.bytes(ACTOR + 0x48, 1) == b'\xfd', 'Native kill retires exact placed actor')
    check(m.writes == [(ACTOR + 0x48, 1, 253)], 'Native kill makes only its owned placed-state write')
    check(not any(GROUP <= pc < GROUP + 0x800 for pc, _ in m.reads), 'Unlinked row never searches collision group entries')
    case_receipts.append({'rowIndex': row['rowIndex'], 'unoccupied': row['unoccupied'], 'instructions': m.instructions, 'reads': m.reads, 'writes': m.writes})

# Reproduce the former zero-group bug with no fabricated callback/kill behavior.
old = bytearray(bytes.fromhex(f['rows'][1]['after'])); old[0x34:0x36] = b'\0\0'
m = fixture(old)
try:
    m.run(f['killAddress'], {STOP})
    raise AssertionError('Former tombstone unexpectedly returned without a linked group')
except AssertionError as error:
    check(str(error).startswith('Unmapped native read 80110000'), 'Former zero-group tombstone attempts the unowned group-list head')
    check(m.writes == [], 'Former failure happens before actor retirement')

# A live linked actor still unlinks correctly: the source routine is unchanged.
for group in (0, 1, 63):
    linked = bytearray(88); struct.pack_into('<h', linked, 0x34, group); struct.pack_into('<I', linked, 4, 0x80112340)
    m = fixture(linked); m.map(GROUP, 256); m.poke(GROUP + group * 4, ACTOR)
    check(m.run(f['killAddress'], {STOP}) == STOP, 'Native linked live actor kill returns')
    check(m.bytes(GROUP + group * 4, 4) == struct.pack('<I', 0x80112340), 'Native linked actor chain advances to exact next pointer')
    check(m.bytes(ACTOR + 0x48, 1) == b'\xfd', 'Live actor retirement preserved')

# Both original constructors establish exactly the emitted negative group value.
m = fixture(bytes(88)); check(m.run(f['simpleConstructorAddress'], {STOP}) == STOP, 'Source simple constructor returns')
check(m.bytes(ACTOR + 0x34, 2) == b'\xff\xff', 'Source simple constructor unlinked sentinel')
m = fixture(bytes(88)); m.r[5] = 0
check(m.run(f['animatedConstructorSentinelAddress'], {0x80052758}) == 0x80052758, 'Source animated constructor skips group insertion when no collider')
check(m.bytes(ACTOR + 0x34, 2) == b'\xff\xff', 'Source animated constructor writes sentinel in branch delay')
receipt = {'evidence': 'source-authenticated strict scalar MIPS; not native runtime', 'checks': checks, 'sourceExecutableSha256': source_sha, 'coreSha256': f['coreSha256'], 'fixtureSha256': hashlib.sha256(a.fixture.read_bytes()).hexdigest(), 'cases': case_receipts, 'oldZeroGroupNegativeControl': 'fails at unmapped group-list read', 'liveActorGroups': [0, 1, 63]}
if a.report: a.report.write_text(json.dumps(receipt, indent=2) + '\n')
print(f'UNIVERSAL_ROW_REMOVAL_NATIVE_CONTRACT_OK checks={checks}')
