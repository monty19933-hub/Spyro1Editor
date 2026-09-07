#!/usr/bin/env python3
"""Private read-only source inventory, not execution/alias/reachability proof.

Print metadata only. No dependency on compiled output, current detached resource
bytes, candidate images, GTE emulation, sound helpers, or a running game.
"""
import argparse
import hashlib
import json
import struct

IMAGE_LENGTH = 661547040
IMAGE_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
SOURCES = {
    "exe": (53875 * 2048, 0x66000, 0x8000F800, "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"),
    "town": (37 * 2048 + 0x118E800, 0xF800, 0x8007AA38, "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5"),
    "wizard": (37 * 2048 + 0x2F5B000, 0x10000, 0x8007AA38, "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1"),
    "town-data": (37 * 2048 + 0x119E000, 0x2E2000, 0, "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"),
    "wizard-data": (37 * 2048 + 0x2F6B000, 0x28A000, 0, "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8"),
}
WINDOWS = [
    ("Town selector table", "town", 0x8007AC34, 0x28),
    ("Town selector body and final jump delay", "town", 0x80084900, 0x420),
    ("Imported lightning collision arguments", "wizard", 0x8007EB80, 0x18),
    ("Shared sphere helper including real GTE command", "exe", 0x8004E2E8, 0xE0),
    ("Deferred damage consumer", "exe", 0x80040F68, 0x308),
    ("Trigger type table", "exe", 0x80011380, 0x20),
    ("Trigger entry and table dispatch", "exe", 0x80056F64, 0x54),
    ("Trigger type7 guard and shared write", "exe", 0x8005731C, 0x64),
    ("Player collision arguments", "exe", 0x800407A0, 0x180),
    ("Collision helper argument ownership", "exe", 0x8004E3C8, 0x64),
    ("Collision flags OR1", "exe", 0x8004E5F0, 0x40),
    ("Collision flags OR2", "exe", 0x8004E69C, 0x40),
    ("Collision flags OR3", "exe", 0x8004E7B4, 0x40),
    ("Collision flags OR4", "exe", 0x8004E954, 0x40),
    ("Accumulator normal clear", "exe", 0x8004A3FC, 0x18),
    ("Accumulator reset clear", "exe", 0x8004ADB4, 0x20),
    ("Town direct flag800 writer", "town", 0x8007DF54, 0x28),
    ("Town direct flag80 writer", "town", 0x8007E764, 0x24),
    ("Model local signed slot consumer", "exe", 0x800523F0, 0x34),
    ("Global death selector26", "exe", 0x800385BC, 0x54),
    ("Player collision base setup", "exe", 0x80040600, 0x24),
    ("Player collision derived base", "exe", 0x80040740, 0x60),
    ("Complete trigger caller with branch delay modes", "exe", 0x80048B9C, 0x170),
    ("Player routine entry and trigger call", "exe", 0x8004A1BC, 0xE4),
    ("Conditional damage call after trigger", "exe", 0x8004A2A0, 0x160),
]


def require(ok, text):
    if not ok:
        raise ValueError(text)


def sha(data): return hashlib.sha256(data).hexdigest()
def u32(data, at):
    require(at >= 0 and at % 4 == 0 and at + 4 <= len(data), "Aligned bounded word")
    return struct.unpack_from("<I", data, at)[0]
def signed16(value): return (value & 0x7FFF) - (value & 0x8000)
def hx(value): return f"{value:08X}"


def identity(image):
    require(image.seek(0, 2) == IMAGE_LENGTH, "Original length")
    image.seek(0)
    require(hashlib.file_digest(image, "sha256").hexdigest() == IMAGE_SHA and
            image.tell() == IMAGE_LENGTH and image.read(1) == b"" and
            image.seek(0, 2) == IMAGE_LENGTH, "Original full SHA / actual EOF / final length")


def logical(image, offset, length):
    require(0 <= offset and 0 < length <= 0x400000, "Bounded logical owner")
    out = bytearray()
    while length:
        size = min(length, 2048 - offset % 2048)
        image.seek(offset // 2048 * 2352 + 24 + offset % 2048)
        part = image.read(size)
        require(len(part) == size, "Complete raw sector user read")
        out.extend(part); offset += size; length -= size
    return bytes(out)


def map_inventory(data, base):
    # This intentionally is an instruction-shape inventory, not an abstract
    # interpreter. The complete-source binding and retained windows permit
    # independent review of the first same-base LBU's actual dataflow.
    result = []
    for off in range(0, len(data), 4):
        word = u32(data, off)
        if word >> 26 != 35 or word & 0xFFFF != 0x61D4:
            continue
        rt = word >> 16 & 31
        matching = [p for p in range(off + 4, min(off + 64, len(data)), 4)
                    if u32(data, p) >> 26 == 36 and u32(data, p) >> 21 & 31 == rt]
        first = matching[0] if matching else None
        special = base + off in (0x80015570, 0x80084D00)
        result.append({"mapLoadPc": hx(base + off), "firstSameBaseLbuPc": None if first is None else hx(base + first),
                       "literalSelector": None if first is None or special else u32(data, first) & 0xFFFF,
                       "special": "relocation" if base + off == 0x80015570 else "dynamic" if special else None,
                       "sourceWindowPc": hx(base + max(0, off - 12)),
                       "sourceWindowHex": data[max(0, off - 12):off + 40].hex()})
    return result


def direct_edges(data, base):
    for off in range(0, len(data), 4):
        word = u32(data, off); op = word >> 26; pc = base + off
        if op in (2, 3):
            yield pc, ((pc + 4) & 0xF0000000) | (word & 0x3FFFFFF) << 2, op == 3
        elif op in (4, 5) or (op in (6, 7) and (word >> 16 & 31) == 0) or (
                op == 1 and (word >> 16 & 31) in (0, 1, 16, 17)):
            yield pc, pc + 4 + 4 * signed16(word & 0xFFFF), False


def sounds(data):
    cursor = u32(data, 8)
    for _ in range(6): cursor += u32(data, cursor)
    count = u32(data, cursor); cursor += 4
    require(count <= 256, "Portal bound")
    for _ in range(count):
        cursor += 0x34 + 12 * u32(data, cursor + 4)
        cursor += u32(data, cursor)
    cursor += u32(data, cursor)
    count = u32(data, cursor + 0x104)
    require(count <= 256 and u32(data, cursor) == 0x108 + count * 20, "Sound component exact end")
    bank = u32(data, 0) + 0x80000; bank_length = u32(data, 4) - 0x80000
    result = []
    for index in range(count):
        at = cursor + 0x108 + index * 20; descriptor = data[at:at + 20]
        start = u32(descriptor, 0); end = start
        while True:
            require(end % 16 == 0 and 0 <= end and end + 16 <= bank_length, "Owned sample")
            frame = data[bank + end:bank + end + 16]; end += 16
            require(frame[0] >> 4 <= 4 and frame[0] & 15 <= 12 and frame[1] <= 7, "ADPCM shape")
            if frame[1] & 1: break
        result.append({"index": index, "offset": at, "descriptorHex": descriptor.hex(),
                       "sampleLength": end - start, "sampleSha256": sha(data[bank + start:bank + end])})
    return cursor, data[cursor + 4:cursor + 260], result


def main(path):
    with open(path, "rb") as image:
        identity(image)
        try:
            sources = {name: logical(image, off, length) for name, (off, length, _, _) in SOURCES.items()}
            for name, (_, _, _, expected) in SOURCES.items(): require(sha(sources[name]) == expected, "Complete " + name)
            windows = []
            for label, name, pc, size in WINDOWS:
                off = pc - SOURCES[name][2]; raw = sources[name][off:off + size]
                require(off >= 0 and len(raw) == size, "Complete window")
                windows.append({"label": label, "source": name, "pc": hx(pc), "length": size,
                                "sha256": sha(raw), "hex": raw.hex()})
            inventories = {name: map_inventory(sources[name], SOURCES[name][2]) for name in ("exe", "town")}
            require(len(inventories["exe"]) == 56 and len(inventories["town"]) == 10, "Exact direct inventory cardinality")
            require([r["firstSameBaseLbuPc"] for r in inventories["exe"] if r["literalSelector"] == 31] == ["80041058"], "Shared1F read")
            require({r["literalSelector"] for r in inventories["town"]} == {None, 0, 45, 47}, "Town literal set")
            edges = []; dynamic_entries = []; literal_entries = []
            for name in ("exe", "town", "wizard"):
                data, base = sources[name], SOURCES[name][2]
                for pc, target, is_call in direct_edges(data, base):
                    if target in (0x80040F68, 0x8004E2E8, 0x80056F64) and is_call:
                        off = pc - base
                        edges.append({"source": name, "pc": hx(pc), "target": hx(target), "windowPc": hx(pc - 12), "hex": data[off - 12:off + 8].hex()})
                    if name != "wizard" and 0x80084900 <= target < 0x80084D20 and not 0x80084900 <= pc < 0x80084D20:
                        dynamic_entries.append({"source": name, "pc": hx(pc), "target": hx(target)})
                if name != "wizard":
                    for off in range(0, len(data), 4):
                        word = u32(data, off)
                        if 0x80084900 <= word < 0x80084D20 and word % 4 == 0:
                            literal_entries.append({"source": name, "pc": hx(base + off), "target": hx(word)})
            require(len([e for e in edges if e["target"] == "80040F68"]) == 26, "Shared damage direct callers")
            exe = sources["exe"]; exe_base = SOURCES["exe"][2]
            masks = {}
            for edge in edges:
                if edge["target"] != "80040F68": continue
                delay = u32(exe, int(edge["pc"], 16) + 4 - exe_base)
                require(delay >> 16 == 0x3404, "Actual ORI a0,zero delay, not sign-extending ADDIU")
                mask = delay & 0xFFFF
                masks[hx(mask)] = masks.get(hx(mask), 0) + 1
                require(mask & 0x20000 == 0, "Every actual caller mask removes high20000")
            require(masks == {"0000FFF9": 23, "0000FBF9": 2, "00008400": 1}, "Complete exact mask inventory")
            exact_words = {
                0x8004060C: 0x3C108008, 0x80040610: 0x26108B58,
                0x80040760: 0x2615FF78, 0x80040850: 0x26A6FFB4,
                0x80048C9C: 0x14400015, 0x80048CA0: 0x24050002,
                0x80048CC4: 0x00002821, 0x80048CF0: 0x24050001,
                0x8004A298: 0x0C0122E7, 0x8004A30C: 0x0C01059C,
                0x8004A408: 0xAC208A84, 0x8005735C: 0x304203FF,
                0x80057360: 0x34420026,
            }
            for pc, word in exact_words.items(): require(u32(exe, pc - exe_base) == word, "Expanded attribution pin " + hx(pc))
            s0 = ((u32(exe, 0x8004060C - exe_base) & 0xFFFF) << 16) + signed16(u32(exe, 0x80040610 - exe_base) & 0xFFFF)
            s5 = s0 + signed16(u32(exe, 0x80040760 - exe_base) & 0xFFFF)
            a2 = s5 + signed16(u32(exe, 0x80040850 - exe_base) & 0xFFFF)
            require((s0, s5, a2) == (0x80078B58, 0x80078AD0, 0x80078A84), "Derived player damage destination")
            namespaces = {}
            for name in ("town-data", "wizard-data"):
                at, mapping, descriptors = sounds(sources[name])
                namespaces[name] = {"soundOffset": at, "descriptorCount": len(descriptors), "map1F": mapping[31], "map26": mapping[38],
                                    "global1F": descriptors[mapping[31]], "global26": descriptors[mapping[38]], "allDescriptors": descriptors}
            town, donor = namespaces["town-data"], namespaces["wizard-data"]
            require((town["map1F"], donor["map1F"], town["map26"], donor["map26"]) == (0, 40, 22, 22), "Exact occupied namespaces")
            wanted = donor["global1F"]
            matches = [d["index"] for d in town["allDescriptors"] if
                       d["descriptorHex"][8:] == wanted["descriptorHex"][8:] and d["sampleSha256"] == wanted["sampleSha256"]]
            require(matches == [], "No exact electric descriptor/sample equivalence in retained Town")
            require(sources["wizard-data"][0x19B51C:0x19B51F] == bytes((49, 50, 51)), "Actual Wizard local slots")
            output = {"sources": {n: {"logicalOffset": o, "length": l, "runtimeBase": hx(b), "sha256": s} for n, (o, l, b, s) in SOURCES.items()},
                      "windows": windows, "mapInventory": inventories, "directCallsites": edges,
                      "townDynamicExternalDirectEdges": dynamic_entries, "townDynamicLiteralPointers": literal_entries,
                      "namespaces": namespaces, "electricEquivalentTownDescriptors": matches,
                      "damageCallerZeroExtendedMasks": masks,
                      "expandedSourceAttribution": {"s0": hx(s0), "s5": hx(s5), "collisionA2": hx(a2),
                          "admittedTriggerModes": [0, 1, 2],
                          "orderedRoutinePcs": ["8004A298", "8004A30C", "8004A408"],
                          "damageCallConditional": True, "allHelpersExecuted": False,
                          "abstractBoltThenType7Flags": hx((0x20020 & 0x3FF) | 0x26),
                          "abstractType7ThenBoltFlags": hx(0x26 | 0x20020)},
                      "nativeInstructionsExecuted": False, "allPointerAliasesProven": False, "freshTrigger7ReachabilityProven": False,
                      "globalAudioCompatibilityProven": False, "targetResourcesWritten": 0, "runtimeVerified": False}
        finally:
            identity(image)
    print(json.dumps(output, indent=2, sort_keys=True))
    print("FRESH_WIZARD_GLOBAL1F_SOURCE_AUDIT_OK sourceUnchanged=true targetOutputsWritten=0")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_bin")
    main(parser.parse_args().original_bin)
