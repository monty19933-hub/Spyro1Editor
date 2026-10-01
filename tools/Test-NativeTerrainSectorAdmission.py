#!/usr/bin/env python3
"""Execute source-authenticated native LP/HP queue admission; no GTE or emulator.

Example: python3 tools/Test-NativeTerrainSectorAdmission.py --source-image USA.bin
Optional --before/--after compare a correction affecting only LP-only radius flags.
"""
import argparse
import json
import struct
from pathlib import Path

from SpyroEggScalarMips import Scalar
from terrain_native_readback import components, logical, sha, wad, word

SOURCE_SHA = 'fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37'
START, STOP = 0x80025B50, 0x80025BAC
# Authenticated USA selector instructions, including the branch delay slots.
EXPECTED = bytes.fromhex(
    '0000bea2002021310900201c008021310400201c20080a01000121202208c102'
    '03002104000005ae0400102200140200004021310900201c22080a012218c102'
    '0600601800ff212002002104ffff42300200a5200000e5ad0400ef21')


def model(path, entry, sectors):
    directory = logical(path, 37 * 2048, 0x800)
    offset, length = struct.unpack_from('<II', directory, entry * 8)
    body = wad(path, offset, length)
    model_offset, model_length = struct.unpack_from('<II', body, 8)
    environment = components(body[model_offset:model_offset + model_length])[0][1]
    records = []
    for index in sectors:
        assert 0 <= index < word(environment, 4), 'Sector index outside environment'
        header_offset = 4 + word(environment, 8 + index * 4)
        header = environment[header_offset:header_offset + 28]
        records.append({'sector': index, 'header': header.hex(),
                        'radiusFlags': hex(word(header, 4) & 0xFFFF),
                        'hp': list(header[20:23]), 'lp': list(header[16:19]),
                        'bodyFlagByte': model_offset + word(body, model_offset) + header_offset + 5})
    return {'path': str(path.resolve()), 'sha256': sha(path), 'sectors': records}, body


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-image', required=True, type=Path)
    parser.add_argument('--before', type=Path)
    parser.add_argument('--after', type=Path)
    parser.add_argument('--wad-entry', type=int, default=10)
    parser.add_argument('--sectors', type=int, nargs='+', default=[178, 179])
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    assert bool(args.before) == bool(args.after), 'Supply both --before and --after'
    assert sha(args.source_image) == SOURCE_SHA, 'Exact authenticated USA source required'
    exe = logical(args.source_image, 53875 * 2048, 0x66000)
    offset = START - 0x8000F800
    assert exe[offset:offset + len(EXPECTED)] == EXPECTED, 'Native selector preimage differs'
    code = {pc: word(exe, pc - 0x8000F800) for pc in range(START, STOP, 4)}
    count = 0
    for radius in (0, 1, 512, 1897, 8191):
        for distance in (-4096, 0, 256, 1024, 8192, 65535):
            for threshold in (256, 2048, 8192):
                for flags in (0, 0x2000, 0x4000, 0x8000, 0xC000):
                    cpu = Scalar(code)
                    for address in (0x1000, 0x2000, 0x3000):
                        cpu.map(address, 4)
                    cpu.r[8], cpu.r[9], cpu.r[10] = distance & 0xFFFFFFFF, radius | flags, radius
                    cpu.r[22] = threshold
                    cpu.r[16], cpu.r[15], cpu.r[21] = 0x1000, 0x2000, 0x3000
                    cpu.r[5], cpu.r[30], cpu.r[29] = 0x800C0A49, 1, 0x1234
                    cpu.run(START, {STOP}, maximum=100)
                    # SW in a taken branch's delay slot does not enqueue unless the
                    # associated queue pointer advances; inspect both pointers.
                    lp, hp = (cpu.r[16] - 0x1000) // 4, (cpu.r[15] - 0x2000) // 4
                    assert lp in (0, 1) and hp in (0, 1)
                    if flags & 0x4000:
                        assert hp == 0
                    if flags & 0x2000:
                        assert lp == 0
                    if flags & 0x8000 and not flags & 0x2000:
                        assert lp == 1
                    count += 1
    receipt = {'scope': 'Authenticated native scalar queue admission only; no GTE/GPU/runtime execution',
               'authUSAsha256': SOURCE_SHA, 'start': hex(START), 'stop': hex(STOP),
               'cases': count, 'allC000ForceLPAndSkipHP': True,
               'delaySlotsExecuted': True, 'nativeRuntime': False}
    if args.before:
        before, old = model(args.before, args.wad_entry, args.sectors)
        after, new = model(args.after, args.wad_entry, args.sectors)
        assert len(old) == len(new), 'Correction changes level allocation'
        differences = [i for i, (a, b) in enumerate(zip(old, new)) if a != b]
        allowed = [r['bodyFlagByte'] for r in after['sectors']]
        assert differences == sorted(allowed), 'Changes outside exact selected header flag bytes'
        for a, b in zip(before['sectors'], after['sectors']):
            assert a['hp'] == b['hp'] == [0, 0, 0] and b['lp'][2] > 0
            old_flags, new_flags = int(a['radiusFlags'], 16), int(b['radiusFlags'], 16)
            assert not old_flags & 0xE000 and new_flags == old_flags | 0xC000
        assert all(old[i] ^ new[i] == 0xC0 for i in differences)
        receipt.update(bodyDifferences=differences, onlySelectedFlagBytesChanged=True,
                       models=[before, after])
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(receipt, indent=2) + '\n')
    print(f'NATIVE_SECTOR_ADMISSION_SCALAR_OK {count} C000forcesLP/skipsHP nativeRuntime=false')


if __name__ == '__main__':
    main()
