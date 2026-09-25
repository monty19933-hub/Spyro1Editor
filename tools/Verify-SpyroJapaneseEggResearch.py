#!/usr/bin/env python3
"""Read-only, independently reconstructed two-F5 USA Artisans disc verifier.

Expected bytes are derived from the two exact original discs, not an editor DLL,
candidate payload, receipt pass flag or claimed candidate hash. The only reused
code is the separately inspected read-only file and CD-ROM EDC/ECC oracle from
Verify-SpyroJapaneseSignReference.py. No BIN, CUE or receipt is ever written.
This verifies the fixed visual experiment, not native gameplay or publication.
"""
from __future__ import annotations

import argparse
import contextlib
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import re
import struct
import sys

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("egg_independent_sector_oracle", Path(__file__).with_name("Verify-SpyroJapaneseSignReference.py"))
oracle = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = oracle
_spec.loader.exec_module(oracle)
require, sha, bounded, u32 = oracle.require, oracle.sha, oracle.bounded, oracle.u32
ReadOnlyFile, logical, rebuild_sector = oracle.ReadOnlyFile, oracle.logical, oracle.rebuild_sector
RAW, USER = 2352, 2048
USA_SIZE, JP_SIZE = 661547040, 727179600
USA_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
JP_SHA = "1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7"
USA_DATA_SHA = "86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f"
JP_DATA_SHA = "7184fff950efa2bdffa37f97875ef7de2a3219480c61ec3ab37f784778d8cfde"
USA_EXE_SHA = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
JP_EXE_SHA = "a67c21f441deb3a4df26099d78ddaa29cc66b927a7387718a678eee71664cee4"
USA_OVERLAY_SHA = "7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55"
JP_OVERLAY_SHA = "bcfe62569db265135d5627de1902a9d31da82c72f51acdc461d2872e08f7cdf2"
PROFILE = "native-artisans-two-japanese-eggs-visual-research-v1"
IMAGE_NAME = "artisans-native-japanese-eggs-visual-r01.bin"
CUE_NAME = "artisans-native-japanese-eggs-visual-r01.cue"
RECEIPT_NAME = "native-japanese-eggs-visual.receipt.json"
POSITIONS = ((72704, 47104, 6656, 32), (72704, 49152, 6656, 96))
FRAGMENTS = (
    (0x8008DE1C, 0x10, "489e0bafad1196885277e64e10780f34ae57b59867bedd92dbfc38a54ac999ca"),
    (0x8008DEC4, 0x0C, "ce7bbcdaa907a872683388d05de3d066a5cde841954832595d6c748a584a6bfb"),
    (0x8008DEE4, 0x80, "1ce7bed9b67aa42553c3c31a78282fd6431da05cda08a7622fe189cdbe06894b"),
)


def put(data, offset, value):
    bounded(data, offset, 4)
    struct.pack_into("<I", data, offset, value)


def words(values):
    return struct.pack("<" + "I" * len(values), *values)


def pinned(data, digest, label):
    require(sha(data) == digest, label + " source identity differs")
    return data


def iso_directory(root):
    result, cursor = {}, 0
    while cursor < len(root) and root[cursor]:
        row = bounded(root, cursor, root[cursor])
        require(len(row) >= 34, "Short ISO directory record")
        name = bounded(row, 33, row[32]).decode("ascii").removesuffix(";1")
        require(name not in result and u32(row, 2) == int.from_bytes(row[6:10], "big") and
                u32(row, 10) == int.from_bytes(row[14:18], "big"), "Duplicate or inconsistent ISO directory owner")
        result[name] = (u32(row, 2), u32(row, 10), cursor)
        cursor += len(row)
    require(not any(root[cursor:]), "Unexpected nonzero root directory tail")
    return result


def iso_source(image, wad_owner, executable_owner, executable_name):
    pvd = logical(image, 16, USER, 0, USER)
    require(pvd[:7] == b"\x01CD001\x01" and u32(pvd, 158) == int.from_bytes(pvd[162:166], "big") and
            u32(pvd, 166) == int.from_bytes(pvd[170:174], "big") == USER, "ISO PVD/root owner differs")
    root_lba = u32(pvd, 158)
    require(root_lba == 22, "Unexpected exact-source root LBA")
    root = logical(image, root_lba, USER, 0, USER)
    records = iso_directory(root)
    require(records.get("WAD.WAD", ())[:2] == wad_owner and records.get(executable_name, ())[:2] == executable_owner,
            "Regional WAD/EXE ISO ownership differs")
    return root_lba, root, records


def scene_layout(scene):
    at = 0x88
    for _ in range(7):
        at = oracle.component_end(scene, at, 8)
    moby, props = at, oracle.component_end(scene, at, 8)
    count = u32(scene, moby + 4)
    require(count <= 256 and moby + 8 + count * 88 <= props, "Scene row capacity exceeded")
    props_end = oracle.component_end(scene, props, 4)
    fixups = oracle.component_end(scene, oracle.component_end(scene, props_end, 4), 4)
    fixup_count = u32(scene, fixups)
    require(fixup_count <= (len(scene) - fixups - 4) // 4, "Scene fixup count exceeds owner")
    end = fixups + 4 + 4 * fixup_count
    require(not any(scene[end:]), "Scene tail is not exact zero padding")
    fields = [u32(scene, fixups + 4 + i * 4) for i in range(fixup_count)]
    rows = {moby + 8 + i * 88 for i in range(count)}
    require(len(set(fields)) == len(fields) and rows.issubset(fields), "Duplicate or missing row relocation")
    for field in fields:
        require(field % 4 == 0 and (field in rows or props + 4 <= field <= props_end - 4), "Unowned relocation field")
        target = u32(scene, field)
        require(target % 4 == 0 and props + 4 <= target < props_end, "Unowned relocation target")
    return moby, props, count, props_end, fixups, end, fields


def append_rows(scene, donor_row, private, positions=POSITIONS):
    """Bounded inner-scene reconstruction; callable with source-free fixtures."""
    require(len(donor_row) == 88 and len(private) == 12 and not any(private), "Unsupported donor row/private owner")
    require(len(positions) == 2 and tuple(positions) == POSITIONS, "Only the fixed two mode1 research sites are admitted")
    require(donor_row[0x36:0x38] == b"\xf5\0" and donor_row[0x49] == 0 and donor_row[0x4F] == 1,
            "Wrong donor actor, initialization state or opaque render mode")
    moby, props, count, props_end, fixups, end, fields = scene_layout(scene)
    first = moby + 8 + count * 88
    require(first + 176 <= props and not any(scene[first:first + 176]), "Nonzero/exhausted dynamic row reservation")
    new_end = end + 24 + 8
    require(new_end <= len(scene), "Scene tail cannot hold two properties and relocations")
    result = bytearray(len(scene))
    result[:props_end] = scene[:props_end]
    result[props_end + 24:fixups + 24] = scene[props_end:fixups]
    result[fixups + 24:end + 24] = scene[fixups:end]
    put(result, moby + 4, count + 2)
    put(result, props, u32(scene, props) + 24)
    put(result, fixups + 24, len(fields) + 2)
    row_reports = []
    for i, (x, y, z, yaw) in enumerate(positions):
        row, at, private_at = bytearray(donor_row), first + i * 88, props_end + i * 12
        put(row, 0, private_at)
        for relative, value in zip((12, 16, 20), (x, y, z)):
            put(row, relative, value)
        angle = ((256 - yaw) & 255) * 2 * math.pi / 256
        c, s = round(math.cos(angle) * 4096), round(math.sin(angle) * 4096)
        row[0x20:0x32] = struct.pack("<9h", c, 0, s, 0, 4096, 0, -s, 0, c)
        row[0x46] = yaw
        result[at:at + 88] = row
        result[private_at:private_at + 12] = private
        put(result, end + 24 + i * 4, at)
        row_reports.append({"NativeIndex": count + i, "RowSceneOffset": at, "PrivateSceneOffset": private_at,
                            "Placement": {"Id": "egg-" + "ab"[i], "X": x, "Y": y, "Z": z, "Yaw": yaw, "ProfileIndex": 1},
                            "SourceRowSha256": sha(donor_row), "FinalRowSha256": sha(row)})
    parsed = scene_layout(result)
    require(parsed[2] == count + 2 and parsed[6] == fields + [first, first + 88], "Reconstructed scene relocation readback differs")
    require(result[moby + 8:first] == scene[moby + 8:first], "Original placed rows changed")
    return bytes(result), row_reports


def level_afterimage(original, donor):
    pinned(original, USA_DATA_SHA, "USA Artisans")
    pinned(donor, JP_DATA_SHA, "Japanese Artisans")
    old_scene, scene_length = u32(original, 0x18), u32(original, 0x1C)
    require((old_scene, scene_length, u32(original, 0x14)) == (0x1C9800, 0x14800, 0x4D800), "Original scene/model layout differs")
    package = pinned(bounded(donor, 0x1CCF5C, 0x808), "da0c1ae7ab529a8ab5b43954452f368adb93c09f1a9b17013c52ee3039f4a810", "Complete donor F5 model")
    require(u32(donor, 0x50 + 26 * 4) == 0x1CCF5C and donor[0x150 + 26 * 2:0x152 + 26 * 2] == b"\xf5\0", "Donor F5 registration changed")
    require(original[0x50 + 35 * 4:0x50 + 36 * 4] == bytes(4) and original[0x150 + 35 * 2:0x150 + 36 * 2] == bytes(2), "Destination registration slot is occupied")
    scene = bounded(original, old_scene, scene_length)
    pinned(scene, "6ca481a134fa5e2801b31cfeb97ec22a8bfb56c46cd0990ecc5718ab61a99342", "Original USA scene")
    donor_scene = bounded(donor, u32(donor, 0x18), u32(donor, 0x1C))
    donor_layout = scene_layout(donor_scene)
    donor_row = pinned(bounded(donor_scene, 0xE418, 88), "1d2b9428be365e08b951218f7daf73530b433e28d40b9455b1b6f60e857cc083", "Original JP row193")
    require(donor_layout[0] + 8 + 193 * 88 == 0xE418 and u32(donor_row) == 0x133A8, "Donor row/private binding differs")
    # Complete regional identity plus structural row/fixup ownership binds this
    # extraction; an adjacent root or a caller-supplied scene is not authority.
    final_scene, rows = append_rows(scene, donor_row, bounded(donor_scene, 0x133A8, 12))
    result = bytearray(original[:old_scene] + package + bytes(0x1000 - len(package)) + original[old_scene:])
    put(result, 0x14, u32(original, 0x14) + 0x1000)
    for i in range(3, 10):
        offset, length = u32(original, i * 8), u32(original, i * 8 + 4)
        if offset or length:
            require(offset >= old_scene and offset + length <= len(original), "Later level-data descriptor is not owned")
            put(result, i * 8, offset + 0x1000)
    put(result, 0x50 + 35 * 4, old_scene)
    struct.pack_into("<H", result, 0x150 + 35 * 2, 0xF5)
    result[old_scene + 0x1000:old_scene + 0x1000 + scene_length] = final_scene
    inverse = bytearray(result[:old_scene] + result[old_scene + 0x1000:])
    inverse[:0x800] = original[:0x800]
    inverse[old_scene:old_scene + scene_length] = scene
    require(inverse == original and result[0x800:old_scene] == original[0x800:old_scene], "Complete level/model inverse failed")
    return bytes(result), rows


def runtime_owners(data, rows):
    """Normal-load owner arithmetic only; no peak/lifetime/runtime approval."""
    start, length = u32(data, 8), u32(data, 12)
    body = bounded(data, start, length)
    cursor = 0
    for _ in range(6):
        cursor = oracle.component_end(body, cursor, 4)
    portals = u32(body, cursor)
    cursor += 4
    require(portals <= (len(body) - cursor) // 0x38, "Portal count exceeds environment owner")
    for _ in range(portals):
        points = u32(body, cursor + 4)
        require(0 < points <= (len(body) - cursor - 0x34) // 12, "Portal points exceed owner")
        cursor = oracle.component_end(body, cursor + 0x34 + points * 12, 4)
    cursor = oracle.component_end(body, oracle.component_end(body, cursor, 4), 4)
    require(not any(body[cursor:]), "Unexplained environment tail")
    copy_anchor = 0x8007AA38 + 0xE800
    model_anchor = copy_anchor + cursor
    scene_anchor = model_anchor + u32(data, 0x14)
    scene = bounded(data, u32(data, 0x18), u32(data, 0x1C))
    moby, props, count, *_ = scene_layout(scene)
    package_at = u32(data, 0x50 + 35 * 4)
    root = model_anchor + package_at - u32(data, 0x10)
    package = bounded(data, package_at, 0x808)
    return {"copy_anchor": copy_anchor, "model_anchor": model_anchor, "scene_anchor": scene_anchor,
            "scene_disk_offset": u32(data, 0x18), "scene_disk_length": len(scene),
            "moby_base": scene_anchor + moby + 8, "dynamic_base": scene_anchor + moby + 8 + count * 88,
            "dynamic_capacity": (props - moby - 8 - count * 88) // 0x70, "egg_model_root": root,
            "rows": [scene_anchor + row["RowSceneOffset"] for row in rows],
            "private": [scene_anchor + row["PrivateSceneOffset"] for row in rows],
            "colors": [root + 0x70 + u32(package, field) for field in (0x54, 0x5C)],
            "near_color_sha256": sha(bounded(package, 0x70 + u32(package, 0x54), 0x118)),
            "far_color_sha256": sha(bounded(package, 0x70 + u32(package, 0x5C), 0x118))}


def jump(pc, link=False):
    require(0x80000000 <= pc < 0x80200000 and pc % 4 == 0, "MIPS target outside aligned main RAM")
    return (0x0C000000 if link else 0x08000000) | ((pc >> 2) & 0x3FFFFFF)


def instruction(op, rs, rt, immediate):
    return op << 26 | rs << 21 | rt << 16 | immediate & 0xFFFF


def overlay_afterimage(original, donor):
    pinned(original, USA_OVERLAY_SHA, "USA overlay")
    pinned(donor, JP_OVERLAY_SHA, "JP overlay")
    base, start = 0x8007AA38, 0x80088A38
    fragments, mapping, cursor = [], {}, start
    for source_pc, size, digest in FRAGMENTS:
        payload = pinned(bounded(donor, source_pc - 0x80084A28, size), digest, "Visual fragment")
        # Each of these three pinned slices has a fall-through boundary, not a
        # copied Japanese gate/pickup/epilogue. Verify rather than assume it.
        penultimate = u32(payload, size - 8)
        op, rs, rt = penultimate >> 26, penultimate >> 21 & 31, penultimate >> 16 & 31
        require(op != 2 and not (op == 0 and penultimate & 63 == 8) and not (op == 4 and rs == rt), "Fragment fall-through grammar changed")
        for relative in range(0, size, 4):
            mapping[source_pc + relative] = cursor + relative
        fragments.append((source_pc, payload, cursor))
        cursor += size + 8
    bridges = {name: cursor + i * 8 for i, name in enumerate(("initialize", "state", "sine", "random", "turn"))}
    restore_pc, entry_pc = cursor + 40, cursor + 40 + 0x98
    external = {0x8008DEF0: (0x8001623C, bridges["sine"]), 0x8008DF2C: (0x800675A8, bridges["random"]),
                0x8008DF3C: (0x800675A8, bridges["random"]), 0x8008DF5C: (0x8003C4B4, bridges["turn"])}
    exits = {0x8008DE2C: bridges["initialize"], 0x8008DED0: bridges["state"], 0x8008DF64: restore_pc}
    emitted, calls, branches = [], [], []
    for source_pc, payload, final_pc in fragments:
        for relative in range(0, len(payload), 4):
            word, pc = u32(payload, relative), source_pc + relative
            op = word >> 26
            if op in (2, 3):
                target = (pc + 4) & 0xF0000000 | (word & 0x3FFFFFF) << 2
                if target in mapping:
                    target = mapping[target]
                else:
                    require(op == 3 and external.get(pc, (None,))[0] == target, "Unexpected external visual call")
                    calls.append(pc)
                    target = external[pc][1]
                word = jump(target, op == 3)
            elif op in (1, 4, 5, 6, 7):
                require(op in (4, 5), "Unsupported branch in bounded visual fragments")
                displacement = struct.unpack("<h", struct.pack("<H", word & 0xFFFF))[0]
                target = pc + 4 + displacement * 4
                require(target in mapping, "Unexpected external branch")
                delta = (mapping[target] - (final_pc + relative + 4)) // 4
                require(-32768 <= delta <= 32767, "Relocated branch out of range")
                word = word & 0xFFFF0000 | delta & 0xFFFF
                branches.append(pc)
            emitted.append(word)
        emitted.extend((jump(exits[source_pc + len(payload)]), 0))
    require(set(calls) == set(external) and len(branches) == 2, "Visual transfer census differs")
    for target in (mapping[0x8008DEC4], mapping[0x8008DEE4], 0x80016C58, 0x8006272C, 0x80038EE0):
        emitted.extend((jump(target), 0))
    require(start + len(emitted) * 4 == restore_pc, "Restore adapter placement differs")
    registers = [r for r in range(1, 32) if r != 29]
    emitted.extend((instruction(35, 29, 8, 0x2A8), 0, 8 << 21 | 17,
                    instruction(35, 29, 8, 0x2AC), 0, 8 << 21 | 19))
    emitted.extend(instruction(35, 29, reg, 0x230 + i * 4) for i, reg in enumerate(registers))
    emitted.extend((jump(0x80085780), instruction(9, 29, 29, 0x2B0)))
    require(start + len(emitted) * 4 == entry_pc == 0x80088BAC, "Owned entry placement differs")
    emitted.append(instruction(9, 29, 29, -0x2B0))
    emitted.extend(instruction(43, 29, reg, 0x230 + i * 4) for i, reg in enumerate(registers))
    emitted.extend((8 << 11 | 16, instruction(43, 29, 8, 0x2A8), 8 << 11 | 18, instruction(43, 29, 8, 0x2AC),
                    instruction(9, 0, 30, 1), jump(mapping[0x8008DE1C]), 0))
    guard_pc = start + len(emitted) * 4
    require(guard_pc == 0x80088C44, "F5-only dispatch placement differs")
    emitted.extend((0x2463FF0B, 0x10600003, 0x246300F5, jump(0x80085780), 0, jump(entry_pc), 0))
    result = bytearray(original + words(emitted))
    require(len(result) <= 0xE800 and original[0x319C:0x31A4] == words([jump(0x80085780), 0]), "Original dispatch hook changed")
    result.extend(bytes(0xE800 - len(result)))
    put(result, 0x319C, jump(guard_pc))
    reverted = bytearray(result[:len(original)])
    reverted[0x319C:0x31A4] = original[0x319C:0x31A4]
    require(reverted == original, "Overlay prefix complement changed")
    return bytes(result)


def executable_afterimage(original):
    pinned(original, USA_EXE_SHA, "USA executable")
    require(original[0x4AF18:0x4AF28] == words([0x3C028009, 0x24428620, 0x3C018008, 0xAC2285D8]), "Original copy anchor initializer changed")
    result = bytearray(original)
    anchor = 0x8007AA38 + 0xE800
    low = struct.unpack("<h", struct.pack("<H", anchor & 0xFFFF))[0]
    put(result, 0x4AF18, 0x3C020000 | (anchor - low) >> 16)
    put(result, 0x4AF1C, 0x24420000 | anchor & 0xFFFF)
    return bytes(result)


def packed_wad(original, replacements):
    require(len(original) % USER == 0 and len(original) >= USER and set(replacements) == {9, 10}, "Wrong packed-WAD replacement owners")
    entries = []
    for i in range(256):
        at, size = u32(original, i * 8), u32(original, i * 8 + 4)
        if not at and not size:
            continue
        require(at >= USER and size > 0 and at % USER == size % USER == 0 and at + size <= len(original), "Invalid packed-WAD entry")
        entries.append((at, size, i))
    entries.sort()
    result, cursor, reports = bytearray(original[:USER]), USER, []
    inverse = bytearray(original[:USER])
    for at, size, i in entries:
        require(at == cursor, "Packed-WAD overlap or gap")
        before = bounded(original, at, size)
        after = replacements.get(i, before)
        require(len(after) >= size and len(after) % USER == 0, "Unaligned/shrinking replacement")
        new_at = len(result)
        put(result, i * 8, new_at)
        put(result, i * 8 + 4, len(after))
        result.extend(after)
        inverse.extend(before if i in replacements else result[new_at:new_at + len(after)])
        reports.append({"Index": i, "OriginalOffset": at, "FinalOffset": new_at, "OriginalLength": size, "FinalLength": len(after),
                        "OriginalSha256": sha(before), "FinalSha256": sha(after)})
        cursor += size
    require(cursor == len(original) and {i for _, _, i in entries}.issuperset(replacements) and inverse == original,
            "Complete original WAD inverse failed")
    return bytes(result), reports


def root_afterimage(original, records, final_wad_size):
    result = bytearray(original)
    new_exe_lba = 37 + final_wad_size // USER
    next_lba = min(lba for lba, _, _ in records.values() if lba > 53875)
    require(new_exe_lba == 53878 and new_exe_lba + 0x66000 // USER <= next_lba, "Relocated executable exceeds proved ISO padding")
    for name, lba, size in (("WAD.WAD", 37, final_wad_size), ("SCUS_942.28", new_exe_lba, 0x66000)):
        at = records[name][2]
        struct.pack_into("<I", result, at + 2, lba)
        struct.pack_into(">I", result, at + 6, lba)
        struct.pack_into("<I", result, at + 10, size)
        struct.pack_into(">I", result, at + 14, size)
    iso_directory(result)
    return bytes(result)


def prepare_padding(before, lba):
    require(len(before) == RAW and before[16:20] == before[20:24] and before[16:18] == b"\0\0" and
            before[19] == 0 and before[18] in (8, 32), "Owned padding has an unexpected XA owner")
    result = bytearray(before)
    if before[18] == 32:
        require(not any(before[24:2348]) and u32(before, 2348) == oracle.edc(before[16:2348]), "Owned Form2 padding is nonzero or corrupt")
        result[18] = result[22] = 8
        result[24:] = bytes(RAW - 24)
    else:
        require(not any(before[24:2072]) and rebuild_sector(before, lba) == before, "Owned Form1 padding is nonzero or corrupt")
    return bytearray(rebuild_sector(result, lba))


def expected_sector(before, lba, wad, exe, root, root_lba, wad_flags, exe_flags):
    final_exe_lba, final_end = 37 + len(wad) // USER, 37 + (len(wad) + len(exe)) // USER
    result = prepare_padding(before, lba) if 54079 <= lba < final_end else bytearray(before)
    if 37 <= lba < final_exe_lba:
        result[24:2072] = bounded(wad, (lba - 37) * USER, USER)
    if final_exe_lba <= lba < final_end:
        result[24:2072] = bounded(exe, (lba - final_exe_lba) * USER, USER)
    if lba == root_lba:
        result[24:2072] = root
    if lba in (53874, 54078, final_exe_lba - 1, final_end - 1):
        flags = wad_flags if lba == final_exe_lba - 1 else exe_flags if lba == final_end - 1 else 0
        result[18] = result[22] = result[18] & ~0x81 | flags
    if result == before:
        return before
    if not 54079 <= lba < final_end:
        require(rebuild_sector(before, lba) == before, f"Independent parity failed on original LBA {lba}")
    return rebuild_sector(result, lba)


def verify_complement(source, candidate, transform, progress=False):
    require(source.initial.st_size % RAW == 0, "Source is not a whole raw-sector image")
    if candidate is not None:
        require(candidate.initial.st_size == source.initial.st_size, "Candidate whole-image length changed")
    digest, changed_sectors, changed_bytes = hashlib.sha256(), 0, 0
    for offset in range(0, source.initial.st_size, RAW * 128):
        before = source.read(offset, min(RAW * 128, source.initial.st_size - offset))
        after = bytearray(before)
        for within in range(0, len(before), RAW):
            lba = (offset + within) // RAW
            raw = before[within:within + RAW]
            expected = transform(raw, lba)
            require(len(expected) == RAW, "Expected sector callback returned wrong extent")
            if raw != expected:
                changed_sectors += 1
                changed_bytes += sum(a != b for a, b in zip(raw, expected))
            after[within:within + RAW] = expected
            if progress and lba % 5000 == 0:
                print(f"independent raw afterimage: LBA {lba}; {changed_sectors} changed sectors", file=sys.stderr, flush=True)
        digest.update(after)
        if candidate is not None:
            actual = candidate.read(offset, len(before))
            require(actual == after, f"Candidate differs from independent raw afterimage in LBAs {offset // RAW}..{(offset + len(before)) // RAW - 1}")
    return digest.hexdigest(), changed_sectors, changed_bytes


def unique_json(data):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, "Duplicate receipt JSON key")
            result[key] = value
        return result
    value = json.loads(data, object_pairs_hook=pairs)
    require(isinstance(value, dict), "Receipt must be an object")
    return value


def verify_receipt(data, report, entries, rows, cue_hash, source_cue_hash):
    receipt = unique_json(data)
    expected = {"profile": PROFILE, "sourceUsaSha256": USA_SHA, "sourceJapanSha256": JP_SHA,
                "outputImageSha256": report["expectedCandidateSha256"], "changedRawSectors": report["changedRawSectors"],
                "outputCueSha256": cue_hash, "sourceCueSha256": source_cue_hash, "OriginalWadSha256": report["originalWadSha256"],
                "WadSha256": report["wadSha256"], "RootSha256": report["rootSha256"], "GrowthBytes": 0x1800,
                "RelocatedExecutableLba": 53878, "FinalExecutableEndLba": 54082, "Entries": entries,
                "LevelDataSha256": report["levelDataSha256"], "OverlaySha256": report["overlaySha256"],
                "ExecutableSha256": report["executableSha256"], "opaqueRenderMode": 1}
    require(all(type(receipt.get(k)) is type(v) and receipt[k] == v for k, v in expected.items()), "Receipt byte ownership/hash description differs from reconstruction")
    actual_rows = receipt.get("Rows")
    require(isinstance(actual_rows, list) and len(actual_rows) == 2 and
            all(all(actual.get(key) == value for key, value in row.items()) for actual, row in zip(actual_rows, rows)),
            "Receipt fixed row/private/placement description differs")
    require(receipt.get("ResearchOnly") is True and all(receipt.get(key) is False for key in
            ("RuntimeVerified", "NormalCreateBinApproved", "CompleteNativeClosureProven", "PickupEnabled", "PersistentSaveChanges",
             "IndependentColorVariants", "TestLevelSelectorEnabled", "acceptedProjectHistory", "FullRuntimePeakProven")),
            "Receipt exceeds bounded visual-research authority")
    # Never consume any positive readback/closure/pass flag as proof.
    return sha(data)


def source_alias(path):
    """Allow an explicit source alias, but pin both alias chain and held target.

    Candidate artifacts still use the strict no-symlink ReadOnlyFile. This is a
    cooperative freshness check, not a hostile-filesystem race-safety claim.
    """
    absolute = Path(os.path.abspath(path))
    resolved = absolute.resolve(strict=True)
    signature = lambda p: (lambda s: (s.st_dev, s.st_ino, s.st_mode, s.st_mtime_ns, s.st_ctime_ns))(os.lstat(p))
    chain = [(p, signature(p)) for p in (absolute, *absolute.parents)]
    def stable():
        require(absolute.resolve(strict=True) == resolved and all(signature(p) == before for p, before in chain), "Source alias chain changed")
    return resolved, stable


def expected_cue(source_bytes, cue_path, original_path):
    lines = [line.strip() for line in source_bytes.decode("utf-8-sig").splitlines() if line.strip()]
    match = re.fullmatch(r'FILE\s+"([^"\r\n]+)"\s+BINARY', lines[0], re.IGNORECASE) if lines else None
    require(len(lines) == 3 and match is not None and lines[1].upper() == "TRACK 01 MODE2/2352" and lines[2].upper() == "INDEX 01 00:00:00",
            "Source CUE is not the bounded single-track raw layout")
    require((Path(cue_path).parent / match.group(1)).resolve(strict=True) == Path(original_path).resolve(strict=True), "Source CUE does not bind original USA BIN")
    return (f'FILE "{IMAGE_NAME}" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n').encode()


def verify(args):
    aliases = [source_alias(path) for path in (args.usa, args.japan, args.usa_cue)]
    with contextlib.ExitStack() as stack:
        usa, japan, cue = [stack.enter_context(ReadOnlyFile(path)) for path, _ in aliases]
        require(usa.initial.st_size == USA_SIZE and usa.digest() == USA_SHA, "Requires complete exact original USA disc")
        require(japan.initial.st_size == JP_SIZE and japan.digest() == JP_SHA, "Requires complete exact original Japanese disc")
        require(cue.initial.st_size <= 65536, "Oversized source CUE")
        cue_before = cue.read(0, cue.initial.st_size)
        cue_after = expected_cue(cue_before, args.usa_cue, args.usa)
        root_lba, root, records = iso_source(usa, (37, 0x6927000), (53875, 0x66000), "SCUS_942.28")
        iso_source(japan, (500, 112793600), (24, 0x70000), "SCPS_100.83")
        original_wad = logical(usa, 37, 0x6927000, 0, 0x6927000)
        jp_header = logical(japan, 500, 112793600, 0, USER)
        def jp_entry(index):
            at, length = u32(jp_header, index * 8), u32(jp_header, index * 8 + 4)
            require(length > 0 and at % USER == length % USER == 0, "Invalid regional donor WAD entry")
            return logical(japan, 500, 112793600, at, length)
        def us_entry(index):
            return bounded(original_wad, u32(original_wad, index * 8), u32(original_wad, index * 8 + 4))
        pinned(logical(japan, 24, 0x70000, 0, 0x70000), JP_EXE_SHA, "JP executable")
        final_data, rows = level_afterimage(us_entry(10), jp_entry(23))
        final_overlay = overlay_afterimage(us_entry(9), jp_entry(22))
        final_exe = executable_afterimage(logical(usa, 53875, 0x66000, 0, 0x66000))
        wad, entries = packed_wad(original_wad, {9: final_overlay, 10: final_data})
        require(len(wad) - len(original_wad) == 0x1800 and len(final_data) == 0x384000, "Fixed owner growth differs")
        final_root = root_afterimage(root, records, len(wad))
        wad_flags = usa.read(53874 * RAW + 18, 1)[0] & 0x81
        exe_flags = usa.read(54078 * RAW + 18, 1)[0] & 0x81
        candidate = None
        if args.candidate_directory:
            directory = Path(os.path.abspath(args.candidate_directory))
            candidate = stack.enter_context(ReadOnlyFile(directory / IMAGE_NAME))
        transform = lambda raw, lba: expected_sector(raw, lba, wad, final_exe, final_root, root_lba, wad_flags, exe_flags)
        digest, changed, changed_bytes = verify_complement(usa, candidate, transform, not args.quiet)
        result = {"schemaVersion": 1, "profile": PROFILE, "sourceUsaSha256": USA_SHA, "sourceJapanSha256": JP_SHA,
                  "mode": "independent-candidate-verification" if candidate else "original-only-preview",
                  "expectedCandidateSha256": digest, "changedRawSectors": changed, "changedRawBytes": changed_bytes,
                  "originalWadSha256": sha(original_wad), "wadSha256": sha(wad), "rootSha256": sha(final_root),
                  "levelDataSha256": sha(final_data), "overlaySha256": sha(final_overlay), "executableSha256": sha(final_exe),
                  "expectedCueSha256": sha(cue_after), "sourceCueSha256": sha(cue_before), "rows": rows,
                  "normalLoadOwners": runtime_owners(final_data, rows),
                  "candidateVerified": candidate is not None, "completeOriginalWadInverseVerified": True,
                  "runtimeVerified": False, "normalExportApproved": False, "pickupVerified": False, "saveVerified": False,
                  "outputImageWrites": 0, "hostileFilesystemRaceSafe": False}
        if candidate is not None:
            output_cue = stack.enter_context(ReadOnlyFile(directory / CUE_NAME, 65536))
            receipt = stack.enter_context(ReadOnlyFile(directory / RECEIPT_NAME, 4 * 1024 * 1024))
            require(output_cue.read(0, output_cue.initial.st_size) == cue_after, "Candidate CUE differs from preserved track geometry")
            receipt_hash = verify_receipt(receipt.read(0, receipt.initial.st_size), result, entries, rows, sha(cue_after), sha(cue_before))
            result["receiptSha256"] = receipt_hash
            candidate.stable(digest)
            output_cue.stable(sha(cue_after))
            receipt.stable(receipt_hash)
        usa.stable(USA_SHA)
        japan.stable(JP_SHA)
        cue.stable(sha(cue_before))
        for _, stable in aliases:
            stable()
        return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--usa", required=True)
    parser.add_argument("--japan", required=True)
    parser.add_argument("--usa-cue", required=True)
    parser.add_argument("--candidate-directory", help="Omit to predict the afterimage from originals without writing any candidate")
    parser.add_argument("--quiet", action="store_true")
    print(json.dumps(verify(parser.parse_args()), indent=2))


if __name__ == "__main__":
    main()
