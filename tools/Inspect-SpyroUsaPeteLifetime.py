#!/usr/bin/env python3
"""Read-only exact-USA PETE lifetime and top-reserve feasibility evidence.

No allocator, image/card writer, runtime hook, or persistent RAM certificate.
Word censuses are bounded syntactic censuses, not indirect-reference closure.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys

spec = importlib.util.spec_from_file_location("_pete_source", Path(__file__).with_name("Inspect-SpyroJapaneseCollectibles.py"))
SOURCE = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = SOURCE
spec.loader.exec_module(SOURCE)
BASE, TOP, NATIVE_LENGTH = 0x8000F800, 0x801FF800, 0x39C48
SPANS = (
    ("boot-bss-heap", 0x8005B8E0, 0x94, "336d9b86228c313bd5e6d861117f6d51138a2e4c26278d34b6fbdb713a984900"),
    ("overlay-base", 0x800113A0, 4, "3c8a286ee3d2980dcdbc89955f78a79bca1046cd41c68e5a351bdb3a3d6e3573"),
    ("top-reservation", 0x800755A4, 8, "284620b7f621e7f47fb58e162ef4aa4db52946a05fb46cdf0dcdd83c0f0a11cb"),
    ("boot-pete", 0x80012C6C, 0x3C, "9bd86968507bb1300fa084f5a07d4d5c4924064547761b93d1a2ae3bff7a0f32"),
    ("pete-reload", 0x8005B7D8, 0x108, "58fa8da2248f5804c79362dad219ccaedcb3214cff735afdb276a1fa04d93e23"),
    ("frame-layout", 0x8005B6F8, 0xE0, "d0b4cf5a7cf9ee407ff413f7261bd5f92f4880d7cee8467852f4d757e6b84295"),
    ("native-forward-copy", 0x80016958, 0x54, "a3b8e30c7eb4c67ee12309f645026b8b9f522276e769b38295fc7e25d40667f5"),
    ("normal-root-retention", 0x80015A84, 0x48, "50460861abce72f5545c2862d25c6dd5783b2d6ee38ece3623a965d09556b94b"),
    ("player-root-retention", 0x80013230, 0x5C, "cfa913206a73ae81cb041ad46d1ac83116c38a16e7668721a0c0a8d904382fa9"),
    ("level-particle-init", 0x8001441C, 0x34, "6840ddf7d901d954c7cbad4022d5f648fda51fd87c77131469b3257bec374b2b"),
    ("level-init-call", 0x80015D40, 0x3C, "7ec7d251f0789e52dbc6316e41ed7ec0f3030b1da86fbed22586d2d4c703b371"),
    ("death-scene-reentry", 0x800144C8, 0x9C, "fae8117d233b5838e56ab55c326c6d86e6a078a9642ef252bb395e6edaec8990"),
    ("death-update-caller", 0x8002EE68, 0x2C, "d50bb2ad6a9e2faf92f2c6116e2aa191f0dc28fce059b239f08256d3684570ca"),
    ("card-entry", 0x8002D170, 0xB8, "b0ffcbd3dff8ff929c2eaa1b746b883174a3bf88bb87a82b42a228bd94638bd6"),
    ("mode15-entry", 0x8002D228, 0x110, "280c93560b19531c78852de07aa9dc5320d73f14a836eda97ff2a945cb116296"),
    ("class-return", 0x8002D440, 0x114, "5630474d6060b69a1dd2227ff3b6249996d3450ee2dcc63fa402eb352d3b6f01"),
    ("card-alternate-transition", 0x80033004, 0xD4, "abce71d91065d9945cdd40336e1e19f3e3cf97313fa0e3899c0cb1ab648dfdae"),
    ("mode14-alternate-transition", 0x800331AC, 0x10C, "c819213b17027934dc5094e5a2655a8bae5340ada4d21928562b752d9972d7cd"),
    ("particle-allocate", 0x80053570, 0x98, "2eefe927534faee54464ea9af9e53a9382ff3fafa0f90d50b6a68982dce61ddb"),
    ("startup-high-cd", 0x80012948, 0x30, "cd2a27c8728a6f1962facc9cdd4daefa768d1535118ecf08b7713464b939d302"),
    ("bios-exec-high-stack", 0x8002BAB8, 0x68, "a11d3e14d6373af261e89f3db30252199796aec811cddc0236637fcf5f785e14"),
    ("bios-exec-wrapper", 0x8005DB24, 0x10, "2d54a20b39b1e994bf31c1483fed1b58c55fc766ee304b2d326473bfd8a0d667"),
)
OVERLAY_HIGH_BRANCHES = (
    (9, 0x1DEC, "397704b7ffb72583936933c687f8707859a7184172efa0cd3a61c56605ebeee6"),
    (11, 0x1E9C, "aff928a32eb4cb29d38f8574081c97a78baddbfc0a5de3575a21b11ce20a4fe9"),
    (15, 0x1E78, "d52b11b1074ea6fe75bba5d6ad1264575a529a1b2837a12dc812073f47d0ad19"),
    (21, 0x27C4, "04819d37b0fa15764b2879106f187c16844b9659d9b89485e96f5d98b2fbdd81"),
    (23, 0x2664, "77e966350b24a81a49db91302eafc948577b152eb3d4131648162b583b0364ca"),
    (25, 0x2664, "77e966350b24a81a49db91302eafc948577b152eb3d4131648162b583b0364ca"),
    (33, 0x27BC, "f975ccc7c27e346a732cb269aa8c71db201fe320b95a82a16f0af310af98e35e"),
    (45, 0x25B0, "087959724eb0defa02d134736a76e0050fa551dcd9061d15b040038bb571fe1b"),
    (57, 0x27E4, "8f168a2009a20289d145b0ec7fbc2e2a054ebc5acaf20f0ddc97191ef676c151"),
    (69, 0x1D5C, "95fdc40b98235412addf6a437512599314c5f442372b2021a43690b2512a7203"),
)


def geometry(growth=0x680, extra_reserve=0x800):
    """Counterfactual byte geometry, never an allocator or capacity certificate."""
    if type(growth) is not int or not 0 < growth <= 0x2000 or growth % 8:
        raise ValueError("Bounded eight-byte-aligned proposed growth required")
    if type(extra_reserve) is not int or not 0x800 <= extra_reserve <= 0x8000 or extra_reserve % 0x800:
        raise ValueError("Bounded sector-aligned proposed extra reservation required")
    new_top = TOP - extra_reserve
    if growth > extra_reserve - 0x10:
        raise ValueError("Proposed ledger leaves no explicit sentinel/alignment guard")
    return {"oldTop": TOP, "nativeResidentBase": TOP - NATIVE_LENGTH,
            "appendedPeteResidentBase": TOP - NATIVE_LENGTH - growth,
            "appendedPeteTail": [TOP - growth, TOP],
            "alternateParticlePoolWithoutReserveChange": [TOP - 0x2000, TOP],
            "lastParticleKindStoreWithoutReserveChange": TOP - 0x20 + 1,
            "newTopIfReserveIncreased": new_top,
            "alternateSentinelWrite": [new_top, new_top + 4],
            "proposedGuard": [new_top, new_top + 0x10],
            "proposedLedger": [new_top + 0x10, new_top + 0x10 + growth],
            "unchangedOriginalStackReservation": [TOP, 0x80200000],
            "normalFloorIfReserveIncreased": new_top - NATIVE_LENGTH - 0x6008 - 0x38000,
            "persistentRamOwnerIssued": False, "allLevelsFitProven": False, "interruptStackPeakProven": False}


def census(data, base, start=0):
    """Aligned words only. Does not claim reachable code or indirect closure."""
    if not isinstance(data, bytes) or len(data) % 4 or start % 4 or not 0 <= start <= len(data):
        raise ValueError("Complete aligned immutable word owner required")
    found = {"peteCalls": [], "frameCalls": [], "reserveLowHalfCandidates": [],
             "reserveGpCandidates": [], "reserveLiteralPointers": [], "highLuiCandidates": [],
             "physicalHighLuiCandidates": [], "highAddressWordCandidates": [],
             "directFcStores": []}
    for off in range(start, len(data), 4):
        w = struct.unpack_from("<I", data, off)[0]
        op, rs, rt, imm = w >> 26, w >> 21 & 31, w >> 16 & 31, w & 65535
        pc = base + off
        target = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        if op in (2, 3) and target == 0x8005B7D8: found["peteCalls"].append(pc)
        if op in (2, 3) and target == 0x8005B6F8: found["frameCalls"].append(pc)
        if imm == 0x55A4 and op in (8, 9, 13, 32, 33, 34, 35, 36, 37, 38, 40, 41, 42, 43, 46):
            found["reserveLowHalfCandidates"].append(pc)
        if rs == 28 and imm == 0x340: found["reserveGpCandidates"].append(pc)
        if w == 0x800755A4: found["reserveLiteralPointers"].append(pc)
        if op == 15 and imm in (0x801F, 0x8020, 0xA01F, 0xA020): found["highLuiCandidates"].append(pc)
        if op == 15 and imm in (0x001F, 0x0020): found["physicalHighLuiCandidates"].append(pc)
        if any(lo <= w <= hi for lo, hi in ((0x1F0000, 0x200000), (0x801F0000, 0x80200000), (0xA01F0000, 0xA0200000))):
            found["highAddressWordCandidates"].append({"pc": pc, "word": w})
        if off >= 4 and op == 43 and imm == 0x85FC and struct.unpack_from("<I", data, off-4)[0] == (15 << 26 | rs << 16 | 0x8008):
            found["directFcStores"].append(pc)
    return found


def capture(path):
    image = SOURCE.AuthenticatedImage(path, SOURCE.USA)
    try:
        exe = image.read(SOURCE.USA.exe_lba, 0, SOURCE.USA.exe_size)
        if hashlib.sha256(exe).hexdigest() != SOURCE.USA.exe_digest:
            raise ValueError("Exact executable identity changed")
        directory = image.read(37, 0, 0x800)
        pete_at, pete_size = struct.unpack_from("<II", directory, 8 * 8)
        if (pete_at, pete_size) != (0x7B8800, 0x3A000): raise ValueError("PETE directory owner changed")
        pete_header = image.read(37, pete_at, 0x800)
        if hashlib.sha256(pete_header).hexdigest() != "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1" or struct.unpack_from("<I", pete_header)[0] != NATIVE_LENGTH:
            raise ValueError("Exact PETE native length/header changed")
        evidence = [SOURCE.describe_span(exe, name, pc-BASE, size, BASE, digest) for name, pc, size, digest in SPANS]
        main = census(exe, BASE, 0x800)
        if main["peteCalls"] != [0x80012C84, 0x8002D284, 0x8002D4A4] or main["frameCalls"] != [0x80033098, 0x80033270, 0x8005B8B8] or main["directFcStores"] != [0x80012C80, 0x80033094, 0x8003326C, 0x8005B860]:
            raise ValueError("Direct PETE/allocator/boundary census changed")
        if main["reserveLowHalfCandidates"] != [0x80012954, 0x80012C70, 0x80033084, 0x8003325C, 0x8005B824, 0x8005B84C, 0x8005B930] or main["reserveGpCandidates"] or main["reserveLiteralPointers"]:
            raise ValueError("Top reserve syntactic census changed")
        # Verify these are seven adjacent LUI8007/LW pairs, not mere low halves.
        for pc in main["reserveLowHalfCandidates"]:
            prev, w = struct.unpack_from("<II", exe, pc-BASE-4)
            rs = w >> 21 & 31
            if w >> 26 != 35 or prev != (15 << 26 | rs << 16 | 0x8007):
                raise ValueError("Reserve reader source pair changed")
        overlays = []
        for index in [2, 7] + list(range(9, 79, 2)) + [81]:
            at, size = struct.unpack_from("<II", directory, index * 8)
            if not size or at % 0x800 or size % 0x800: raise ValueError("Bounded WAD code-owner geometry changed")
            body = image.read(37, at, size)
            c = census(body, 0x8007AA38)
            if any(c[k] for k in ("reserveLowHalfCandidates", "reserveGpCandidates", "reserveLiteralPointers", "peteCalls", "frameCalls", "directFcStores")):
                raise ValueError("Additional bounded WAD top-reserve/PETE reference requires review")
            row = {"entry": index, "wadOffset": at, "length": size, "sha256": hashlib.sha256(body).hexdigest(), "wordCensus": c}
            branch = next((p for p in OVERLAY_HIGH_BRANCHES if p[0] == index), None)
            if branch:
                _, off, digest = branch
                row["guardedHighCopy"] = SOURCE.describe_span(body, "high-copy-only-if-arena-above-801fffff", off, 0x50, 0x8007AA38, digest)
                if c["highLuiCandidates"] != [0x8007AA38 + off, 0x8007AA38 + off + 0x20]: raise ValueError("High copy census changed")
            elif c["highLuiCandidates"]: raise ValueError("Unreviewed high-RAM literal owner")
            overlays.append(row)
        result = {"sourceSha256": SOURCE.USA.digest, "exeSha256": SOURCE.USA.exe_digest,
                  "peteDirectory": [pete_at, pete_size], "nativePayloadLength": NATIVE_LENGTH,
                  "evidence": evidence, "exeWordCensus": main, "boundedWadWordCensus": overlays,
                  "counterfactualGeometry": geometry(), "cpuOrBiosExecuted": False,
                  "indirectAddressClosureProven": False, "allTransitionLifetimeProven": False,
                  "persistentRamOwnerIssued": False, "writesImage": False, "writesCard": False,
                  "publicationApproved": False}
    finally:
        image.close()
    return dict(heldAndNamedOriginalFinal=True, **result)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_usa_bin", type=Path)
    args = parser.parse_args()
    print(json.dumps(capture(args.original_usa_bin), indent=2))
