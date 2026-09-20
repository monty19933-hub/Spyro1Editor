#!/usr/bin/env python3
"""Exact Original/candidate offline collider inspection; no emulator or writes.

The recorded matrix is an observed input, not a pristine-entry certificate.
No GTE execution, collision response, gameplay or clearance approval is issued.
"""
import contextlib
import hashlib
import json
import os
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
ORIGINAL = Path('/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin')
CANDIDATE = ROOT / '_local/native-v6-chest-runtime-20260919-r01.nqKxBi/candidate-r01/fresh-chest-p9-experiment-r01.bin'
OBSERVATIONS = ROOT / '_local/native-v6-chest-runtime-20260919-r01.nqKxBi/observations/runtime-observer.jsonl'
IMAGE_LENGTH = 661547040
PROFILES = (
    ('original', ORIGINAL, 'fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37'),
    ('candidate', CANDIDATE, '4ba67d92fef764ab80d4e70a3b233e5b3e75a69e2aa0ae74c4ffb3410c58284b'),
)
WINDOWS = (
    (0x80040500, 0x940, 'f7b7a6aee6b1ca474b5d088c64afa42b2ab95103aab67e44889c58971aa58714'),
    (0x8004E3C8, 0x7E0, '7bb841a0f73d9d42c747f8cc51667d7ce1721b725f77abbec6d4f40275c17504'),
    (0x800526A8, 0x670, 'f8a0459a64054397765abd0f44396bdbb7260cfe4df36111a6f8a1ecad1fc809'),
    (0x800133E0, 0x280, '4d38a5e373feaf98cf6478f43db3638121d24a909adce47c9aafb0cdd5f72cad'),
    (0x80013DA0, 0x360, '1fa240fcd4808a454521efde3727b207739f28d9c9c7996d22b65beca7d74ffa'),
)


def require(ok, label):
    if not ok: raise ValueError(label)


def sha(data): return hashlib.sha256(data).hexdigest()
def word(data, at=0): return struct.unpack_from('<I', data, at)[0]
def xyz(data, at=0): return struct.unpack_from('<3i', data, at)
def identity(value): return (value.st_dev, value.st_ino, value.st_size, value.st_mtime_ns, value.st_ctime_ns)


def authenticate(stream, path, expected, initial):
    require(identity(os.fstat(stream.fileno())) == identity(initial) == identity(path.stat()), 'Held/named identity before hash')
    require(initial.st_size == IMAGE_LENGTH, 'Exact full raw image length')
    stream.seek(0)
    digest, length = hashlib.sha256(), 0
    while block := stream.read(1024 * 1024):
        length += len(block); digest.update(block)
    require(length == IMAGE_LENGTH and stream.read(1) == b'' and digest.hexdigest() == expected, 'Complete image SHA and true EOF')
    require(identity(os.fstat(stream.fileno())) == identity(initial) == identity(path.stat()), 'Held/named identity after hash')


def logical(stream, offset, length):
    require(0 <= offset and 0 < length <= 0x300000 and offset + length <= IMAGE_LENGTH // 2352 * 2048, 'Bounded logical extent')
    parts = []
    while length:
        sector, inside = divmod(offset, 2048)
        stream.seek(sector * 2352)
        raw = stream.read(2352)
        require(len(raw) == 2352 and raw[:12] == b'\0' + b'\xff' * 10 + b'\0' and raw[15] == 2 and raw[16:20] == raw[20:24], 'Actual complete Mode2 sector')
        take = min(length, 2048 - inside)
        parts.append(raw[24 + inside:24 + inside + take]); offset += take; length -= take
    return b''.join(parts)


def read_observation():
    selected = []
    for line in OBSERVATIONS.read_bytes().splitlines():
        if not line.startswith(b'{'): continue
        value = json.loads(line)
        if value.get('label') == '04-first-paused-level65-frame': selected.append((line, value))
    require(len(selected) == 1, 'One exact recorded sample04, not newest or stale sample')
    line, value = selected[0]
    require(value['bounded_sample_valid'] and value['gate_reread_consistent'] and value['level'] == 65 and value['game_state'] == 0, 'Recorded bounded normal-level65 gates')
    require((value['model_anchor'], value['scene_anchor'], value['copy_anchor']) == (0x8008D4A4, 0x800A84A4, 0x8008CA38), 'Recorded actual current owner anchors')
    return line, value


def type1_geometry(exe, descriptor, chest, spawn, recorded_player):
    # Pinned dispatch chooses E668, not animated-vertex/GTE-matrix path E830.
    # This is scalar interpretation of the native predicates, not a fabricated
    # GTE/helper return or an execution of the normalization/push-out loop.
    premises = (
        (0x80040904, 0x24050164), (0x80040764, 0x24140001),
        (0x8004E548, 0x8FC10008), (0x8004E554, 0x1020FFEF),
        (0x8004E558, 0x94220000), (0x8004E560, 0x00561020),
        (0x8004E590, 0x30420001), (0x8004E598, 0x8C220000),
        (0x8004E5A4, 0x304AFF00), (0x8004E5A8, 0x000A5142),
        (0x8004E5C0, 0x0801399A), (0x8004E668, 0x00025402),
        (0x8004E66C, 0x01565020), (0x8004E674, 0x8C2B0000),
        (0x8004E67C, 0x000B5C03), (0x8004E680, 0x01655820),
        (0x8004E684, 0x016B0018), (0x8004E68C, 0x01866020),
        (0x8004E690, 0x01876020), (0x8004E694, 0x018A6822),
        (0x8004E698, 0x05A1FFBD), (0x8004E69C, 0x20210008),
        (0x8004E6CC, 0x8C30FFFC), (0x8004E6D4, 0x01F07825),
        (0x8004E6D8, 0xAE8F0000), (0x8004E6DC, 0x304F0002),
        (0x8004E6E0, 0x15E0FFAB), (0x8004E6E8, 0x326F0001),
        (0x8004E6EC, 0x11E00005), (0x8004E6FC, 0x08013A76),
        (0x8004E700, 0x21650000),
    )
    require(all(word(exe, pc - 0x8000F800) == expected for pc, expected in premises), 'Actual native type1 predicates and positional-response branch operands')
    broadphase, command, vertical, incoming = descriptor
    require(descriptor == (0x3A8, 0x02580101, 0x01500000, 4), 'Frozen observed exact source AE final type1 descriptor')
    z_offset = struct.unpack('<i', struct.pack('<I', vertical))[0] >> 16
    centre = (chest[0], chest[1], chest[2] + z_offset)
    radius, player_radius = command >> 16, word(exe, 0x80040904 - 0x8000F800) & 0xFFFF
    raw_delta = tuple(a - b for a, b in zip(chest, spawn))
    contact_delta = tuple(a - b for a, b in zip(centre, spawn))
    require(all(-32768 <= v <= 32767 for v in raw_delta + contact_delta), 'Bounded signed IR operands; scalar squares cannot wrap')
    raw_distance2 = sum(v * v for v in raw_delta)
    contact_distance2 = sum(v * v for v in contact_delta)
    require(centre == (9472, 8960, 9040) and radius == 600 and player_radius == 356 and z_offset == 336, 'Exact conditional native type1 centre/radii')
    require(raw_distance2 == 524288 < (broadphase + player_radius) ** 2 == 1669264 and
            contact_distance2 == 293120 < (radius + player_radius) ** 2 == 913936, 'Stored spawn passes broadphase and strictly overlaps actual type1 sphere')
    return {'nativeShapeType': 1, 'nativeShapeEntry': '8004E668', 'broadphaseRadius': broadphase,
            'sphereRadius': radius, 'playerDefaultModeRadius': player_radius, 'signedZOffset': z_offset,
            'centre': centre, 'rawOriginDistanceSquared': raw_distance2, 'broadphaseCombinedRadiusSquared': (broadphase + player_radius) ** 2,
            'contactDistanceSquared': contact_distance2, 'contactCombinedRadiusSquared': (radius + player_radius) ** 2,
            'conditionalStoredSpawnOverlap': True, 'responseDisabledMask02': bool(command & 2),
            'incomingPlayerAccumulatorMask': incoming, 'animationVertexAndMatrixUsedByType1': False,
            'centreMinusStoredSpawn': contact_delta, 'chestOnlySeparationHasZeroYComponentAtStoredSpawn': contact_delta[1] == 0,
            'recordedSample04ContactDistanceSquared': sum((a - b) ** 2 for a, b in zip(centre, recorded_player)),
            'condition': 'Default mode1/radius356 query at exact stored spawn with the preserved selected chest descriptor; actual earliest query and its incoming motion were not observed.',
            'completeYMotionExplanationProven': False, 'actualDamageProven': False}


def inspect(original, candidate, observation):
    peace = logical(original, 37 * 2048 + 0x1890800, 0x29E800)
    require(sha(peace) == 'c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3', 'Complete Original Peace data')
    package = peace[0x1B3978:0x1B4980]
    require(sha(package) == '8edc9e9ac1e5fb1a92224d2f5f8ca541371940c1ae3f9873c02f6b802d6e22e7', 'Complete original AE package')
    directory = logical(candidate, 37 * 2048, 2048)
    host_at, host_length, body_at, body_length = struct.unpack_from('<4I', directory, 79 * 8)
    require((host_at // 2048 + 37, host_length, body_at // 2048 + 37, body_length) == (53875, 0x12000, 53911, 0x20F000), 'Actual candidate directory owns Host/Body geometry')
    body = logical(candidate, 37 * 2048 + body_at, body_length)
    require(sha(body) == '77fdfded0383af6636ffa991c47d55f113b0f17327584213bb60a72494529d45', 'Complete candidate Body')
    model_at, model_length, scene_at, scene_length = struct.unpack_from('<4I', body, 0x10)
    require((model_at, model_length, scene_at, scene_length) == (0xE7000, 0x1B000, 0x102000, 0x3800), 'Actual model/scene extents')
    models, scene = body[model_at:model_at + model_length], body[scene_at:scene_at + scene_length]
    require(sha(scene) == '075c84cfd5ec13dc245dfa8b888f349903056b07676fc1990c494875e90f20d3', 'Complete placed scene')
    installed = models[0x19800:0x1A808]
    require(len(installed) == len(package) and installed[0x14:0x68] == package[0x14:0x68] and installed[0xEF0:] == package[0xEF0:], 'Entire collider/header and actual vertex stream source complement')
    # The Wizard has a separately generated zero lookahead owner. Do not
    # assume the same for this independently appended chest package; report
    # the actual next model bytes without assigning them to the AE source.
    require(len(models[0x1A808:0x1A80C]) == 4, 'Complete actual adjacent lookahead read')
    source_exe, final_exe = logical(original, 53875 * 2048, 0x66000), logical(candidate, 54965 * 2048, 0x66000)
    require(sha(source_exe) == 'a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998' and sha(final_exe) == '81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431', 'Complete original/current executable owners')
    windows = []
    for pc, length, expected in WINDOWS:
        at = pc - 0x8000F800
        selected = source_exe[at:at + length]
        require(sha(selected) == expected and selected == final_exe[at:at + length], 'Pinned native input/transform/response/relocation window')
        windows.append({'pc': hex(pc), 'length': length, 'sha256': expected})
    stored = scene[0xD4 + 3 * 0x58:0xD4 + 4 * 0x58]
    live = bytes.fromhex(observation['placed_rows'][3]['raw_hex'])
    require(len(stored) == len(live) == 88 and xyz(stored, 12) == xyz(live, 12) == (9472, 8960, 8704) and xyz(scene) == (8960, 8960, 9216), 'Actual stored spawn and stored/recorded chest coordinates')
    require(stored[0x36:0x38] == live[0x36:0x38] == b'\xae\0' and stored[0x3C:0x40] == live[0x3C:0x40] == bytes(4) and stored[0x44:0x48] == live[0x44:0x48] == bytes.fromhex('0000dc04'), 'Actual animation0/frame0/yawDC retained')
    require(word(live, 8) == 0x8008D4A4 + 0x19800 + 0xFF8 and word(package, 0x14) == 0x80000FF8, 'Source/native relocated selected collider root')
    require(tuple(word(package, 0x14 + 4 * i) for i in range(1, 8)) == (0,) * 7, 'No hidden second collider root')
    animation = word(package, 0x38)
    frame = word(package, animation + 0x24)
    frame_at = word(package, 0x34) + (frame & 0x1FFFFF)
    require(frame_at == 0xEF0 and ((frame >> 19) & 0x1C) == 0, 'Actual frame0 vertex address and collider selector')
    descriptor = struct.unpack_from('<4I', package, 0xFF8)
    report = {'sourcePackageSha256': sha(package), 'installedPackageSha256': sha(installed), 'modelRuntime': '800A6CA4',
              'descriptorRuntime': '800A7C9C', 'descriptorWords': [f'{v:08X}' for v in descriptor],
              'adjacentNonAeLookaheadHex': models[0x1A808:0x1A80C].hex(),
              'storedSpawn': xyz(scene), 'storedChest': xyz(stored, 12), 'storedMatrixHex': stored[0x20:0x34].hex(),
              'recordedMatrixWords': [f'{word(live, at):08X}' for at in range(0x20, 0x34, 4)],
              'recordedMatrixSigned': struct.unpack_from('<9h', live, 0x20), 'frameWord': f'{frame:08X}',
              'animationHeaderHex': package[animation:animation + 0x24].hex(), 'firstFrameHex': package[frame_at:frame_at + 64].hex(),
              'animationOffset': animation, 'scale': package[animation + 5], 'nativeWindows': windows,
              'candidateAuthenticated': True, 'nativeGteExecuted': False, 'initialEntryPoseObserved': False,
              'actualContactOrDisplacementProven': False, 'runtimeVerified': False, 'publicationApproved': False}
    report['conditionalNativeGeometry'] = type1_geometry(source_exe, descriptor, xyz(stored, 12), xyz(scene), observation['player_xyz'])
    return report


def main():
    self_before = Path(__file__).read_bytes()
    line, observation = read_observation()
    held = []
    with contextlib.ExitStack() as stack:
        try:
            for name, path, digest in PROFILES:
                stream = stack.enter_context(path.open('rb'))
                initial = os.fstat(stream.fileno()); held.append((name, stream, path, digest, initial))
                authenticate(stream, path, digest, initial)
            report = inspect(held[0][1], held[1][1], observation)
            report.update(recordedSample04Sha256=sha(line), diagnosticSha256=sha(self_before))
        finally:
            failures = []
            for name, stream, path, digest, initial in held:
                try:
                    authenticate(stream, path, digest, initial)
                    with path.open('rb') as reopened: authenticate(reopened, path, digest, initial)
                except BaseException as error: failures.append(error)
            try:
                require(Path(__file__).read_bytes() == self_before, 'Diagnostic definition unchanged')
                require(read_observation()[0] == line, 'Exact recorded sample remains unchanged; later appends allowed')
            except BaseException as error: failures.append(error)
            if failures: raise BaseExceptionGroup('Final source/candidate/definition checks', failures)
    report['completeBeforeAfterReopenedImageGuardsPassed'] = True
    print(json.dumps(report, indent=2))
    print('NATIVE_FRESH_CHEST_COLLIDER_OFFLINE_INSPECTION_OK; NO_NATIVE_EXECUTION_OR_RUNTIME_APPROVAL')


if __name__ == '__main__': main()
