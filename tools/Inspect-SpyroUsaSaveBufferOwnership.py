#!/usr/bin/env python3
"""Read-only exact-USA save workspace/transfer evidence. Never opens a card.

The scalar probe records native call arguments, not BIOS execution or ownership
authorization. Whole source identities are checked before and after capture.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys


def load_module(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


SOURCE = load_module("_usa_save_source", "Inspect-SpyroJapaneseCollectibles.py")
SCALAR = load_module("_usa_save_scalar", "Inspect-SpyroUsaModalTextureLifetime.py")
BASE, CARD_BASE = 0x8000F800, 0x8007AA38
READ, WRITE = 0x80066E28, 0x800670E4
CARD_SHA = "8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c"
SPANS = (
    ("exe", "overlay-destination", 0x800113A0, 4, "3c8a286ee3d2980dcdbc89955f78a79bca1046cd41c68e5a351bdb3a3d6e3573"),
    ("exe", "directory-install", 0x80012544, 0x18, "8a86d6b8b0b323caa945923902f75a5a36a762c28797c4b891f4967b59048360"),
    ("exe", "whole-card-overlay-load", 0x80012978, 0x24, "ef93a92b27c18a7ea508bd01646708366321f4af7994b27ef34da67d5455dd49"),
    ("exe", "card-entry-buffer", 0x8002D170, 0xB8, "b0ffcbd3dff8ff929c2eaa1b746b883174a3bf88bb87a82b42a228bd94638bd6"),
    ("exe", "frame-layout", 0x8005B6F8, 0xE0, "d0b4cf5a7cf9ee407ff413f7261bd5f92f4880d7cee8467852f4d757e6b84295"),
    ("exe", "game-header-read", 0x80032134, 0x2C, "ba9df6fc824c759e97bba76835245ae1c4191aeb7ff0caa6003442b29792b573"),
    ("exe", "game-slot-write", 0x800321E8, 0x50, "a64cabebc5597a58e898440b6c20b732680d11c4136cf5d2f35aa03d3991ad16"),
    ("exe", "game-slot-readback", 0x80032280, 0x40, "7be882efa97431a4c35226e670e811b856901e07790182d40b2125f6de251146"),
    ("exe", "game-readback-check", 0x800322CC, 0x6C, "436bee1cdf87278c7f4977fa7422d8bf7a27fd24b731cf51f7b56e4f3fa75704"),
    ("exe", "slot-checksum", 0x8005956C, 0x28, "d83b069d73f991a0ea4b1a318759f6ea66c5606de361242bab29d02c840824db"),
    ("exe", "slot-serialize", 0x80059864, 0x1E4, "b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056"),
    ("exe", "card-read-api", 0x80066E28, 0x10C, "87ba1be6dead52b5b297e0587aad7c37f99b5676e497988c0846859e802f9b6d"),
    ("exe", "card-read-callback", 0x80066F34, 0x1B0, "9ccae6396d8da31d84cd08ee1a9a885cd7a69a9d4eada22f94293e350541ae10"),
    ("exe", "card-write-api", 0x800670E4, 0x10C, "791708cc4c2bb40cdef08c7fdd0a4c8981abd9ede32105b305f25074c1bc868a"),
    ("exe", "card-write-callback", 0x800671F0, 0x1B0, "f1fd33dbea4c571b4632c2c7e3b77874ed058f60ee813721d08232f4db2eb9ca"),
    ("exe", "card-shutdown", 0x8006631C, 0x40, "966aaf82f3e029b34567f511b5d1933f6f053bb91180eb7f248958d6e5631e9e"),
    ("exe", "create-block-flags", 0x800677DC, 0x18, "68f8593f8b171099158fd00aa68db6ab6bd7d690a45199eb653dcbe556f2dca7"),
    ("exe", "mode13-dispatch", 0x800339C0, 0x3C, "9a5e6fa72cef17674582c0f47e6abee767b205bfdfd377cddfd970fc6f927e99"),
    ("exe", "card-exit-transition", 0x80032B08, 0x150, "d3ad247f70ad06102b483afadf5d3bcdf8f5939110f6bd3d7475a2216e9a46de"),
    ("card", "header", 0x8007AA38, 0x9C, "79e96efbe41a96e329c572ebb7fa6c68d72ac6f030ede8783a0a3be952cdc513"),
    ("card", "sound-helper", 0x8007AAD4, 0xD8, "f8679f07da9182c3a72c60511fbf02ef6756c1818044625040586d007c2ca569"),
    ("card", "menu-state-dispatch", 0x8007B0B8, 0x30, "21adc7ed8b750d5e61a24d098b5e0e10ffcae4fbed1e6d0917fc12541ff5c421"),
    ("card", "intro-background-staging", 0x8007AD64, 0x10C, "242c7d9c6442fe823073a66fe35aae14cf962c11a69fe7e960ac66e316e1c007"),
    ("card", "menu-prefix-read", 0x8007B348, 0x2C, "483a97c3bc80ad352f3341d1ca3f0184f09571c350078ce6df01ec7499666dc8"),
    ("card", "new-file-and-prefix-write", 0x8007BDCC, 0xE8, "468764c703598ea45eb6b440dbc21cd7eb0c09013daa05f63db00f339ff0eef9"),
    ("card", "menu-prefix-readback", 0x8007BEDC, 0x2C, "483a97c3bc80ad352f3341d1ca3f0184f09571c350078ce6df01ec7499666dc8"),
    ("card", "menu-slot-validation", 0x8007BF18, 0xC0, "c9d93daa20857883556931e45795cd85b3a763111fd7f4ad19246a5dbbad0887"),
    ("card", "menu-slot-pointers", 0x8007C4D4, 0x38, "9287e58efc90e6e621ffe5b4f378d583c2363681ba239e0042207d5555846565"),
    ("card", "selected-slot-deserialize", 0x8007C9FC, 0xA8, "84d736a2f5f3bf9ca2cca31cdbd844acf9c93f63a92ef37dde7ad6fe865ef9e3"),
    ("card", "menu-drain-exit", 0x8007CBA0, 0xA8, "04ce50edabeae6d4721e8e8795bd9a1c5aaf9f8cd29903c2f13d9e521c265817"),
)


class CallSetup(SCALAR.SetupTrace):
    """One straight-line setup ending in a recorded call and its delay slot."""
    def run_call(self, code, pc, expected_target):
        if not isinstance(code, bytes) or len(code) < 8 or len(code) % 4 or pc % 4:
            raise ValueError("Exact aligned complete call setup required")
        for offset in range(0, len(code), 4):
            word = struct.unpack_from("<I", code, offset)[0]
            if word >> 26 == 3:
                if offset + 8 != len(code) or self.pending:
                    raise ValueError("Only a terminal call without pending load is admitted")
                target = ((pc + offset + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2)
                if target != expected_target:
                    raise ValueError("Unexpected service call")
                self.reg[31] = pc + offset + 8
                self._plain(struct.unpack_from("<I", code, offset + 4)[0], pc + offset + 4)
                if self.pending:
                    raise ValueError("Call arguments depend on an unconsumed delay-slot load")
                return {"pc": pc + offset, "target": target, "controller": self.reg[4],
                        "filenamePointer": self.reg[5], "buffer": self.reg[6], "fileOffset": self.reg[7],
                        "byteCount": self._read(self.reg[29] + 16, 4), "serviceExecuted": False}
            self._plain(word, pc + offset)
        raise ValueError("Missing terminal service call")


def workspace_geometry(arena_base, arena_end):
    """Arithmetic footprint only; supplied addresses confer no live ownership."""
    if any(type(v) is not int or v % 4 or not 0x80040000 <= v <= 0x80200000 for v in (arena_base, arena_end)) or arena_end <= arena_base:
        raise ValueError("Invalid bounded fixture arena endpoints")
    return {"menuWorkspace": [arena_base - 0x2000, arena_base],
            "menuNativePrefix": [arena_base - 0x2000, arena_base - 0xC00],
            "menuUnreadTail": [arena_base - 0xC00, arena_base],
            "earlierIntroCdStaging": [arena_base - 0x40000, arena_base],
            "gameSlotScratch": [arena_end - 0x600, arena_end],
            "persistentLedgerOwnerEstablished": False}


def direct_io_calls(data, base, start):
    result = []
    for offset in range(start, len(data), 4):
        word = struct.unpack_from("<I", data, offset)[0]
        target = ((base + offset + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2)
        if word >> 26 == 3 and target in (READ, WRITE):
            result.append({"pc": base + offset, "offset": offset, "target": target, "word": f"{word:08X}"})
    return result


def capture(path):
    image = SOURCE.AuthenticatedImage(path, SOURCE.USA)
    try:
        exe = image.read(SOURCE.USA.exe_lba, 0, SOURCE.USA.exe_size)
        card = image.read(37, 0x5B800, 0x3800)
        directory = image.read(37, 16, 8)
        if hashlib.sha256(exe).hexdigest() != SOURCE.USA.exe_digest or hashlib.sha256(card).hexdigest() != CARD_SHA or directory.hex() != "00b8050000380000":
            raise ValueError("Exact executable/card directory/body identity changed")
        owners = {"exe": (exe, BASE), "card": (card, CARD_BASE)}
        evidence = [dict(owner=owner, **SOURCE.describe_span(owners[owner][0], label, pc - owners[owner][1], size, owners[owner][1], digest))
                    for owner, label, pc, size, digest in SPANS]
        def code(owner, pc, size):
            data, base = owners[owner]
            return data[pc - base:pc - base + size]
        calls = {owner: direct_io_calls(data, base, 0x800 if owner == "exe" else 0x9C)
                 for owner, (data, base) in owners.items()}
        if [r["pc"] for r in calls["exe"]] != [0x80032158, 0x80032230, 0x800322B8] or [r["pc"] for r in calls["card"]] != [0x8007B36C, 0x8007BE90, 0x8007BF00]:
            raise ValueError("Bounded direct I/O call census changed")
        arena, end = 0x80180000, 0x801B8000  # Fixture seeds, NOT allocations.
        menu = arena - 0x2000
        trace = []
        for controller in (0, 1):
            seeds = {0x80078DC4: menu, 0x80078DA0: controller, 0x800785F0: end, 0x80078D18: controller * 16}
            for pc, target in ((0x8007B348, READ), (0x8007BE6C, WRITE), (0x8007BEDC, READ)):
                request = CallSetup(globals_=seeds).run_call(code("card", pc, 0x2C), pc, target)
                if (request["controller"], request["buffer"], request["fileOffset"], request["byteCount"], request["filenamePointer"]) != (controller * 16, menu, 0, 0x1400, CARD_BASE + 4):
                    raise ValueError("Menu transfer setup changed")
                trace.append(request)
            request = CallSetup(globals_=seeds).run_call(code("exe", 0x80032134, 0x2C), 0x80032134, READ)
            if (request["buffer"], request["fileOffset"], request["byteCount"]) != (end - 0x600, 0, 0x80):
                raise ValueError("Gameplay identity-read setup changed")
            trace.append(request)
            for slot in range(3):
                for pc, size, target in ((0x800321FC, 0x3C, WRITE), (0x80032280, 0x40, READ)):
                    request = CallSetup({16: 0x800785F0}, seeds | {0x80078D1C: slot}).run_call(code("exe", pc, size), pc, target)
                    if (request["controller"], request["buffer"], request["fileOffset"], request["byteCount"], request["filenamePointer"]) != (controller * 16, end - 0x600, 0x200 + slot * 0x600, 0x600, 0x80010DB8):
                        raise ValueError("Gameplay slot transfer setup changed")
                    trace.append(dict(slot=slot, **request))
        result = {"sourceSha256": SOURCE.USA.digest, "exeSha256": SOURCE.USA.exe_digest, "cardOverlaySha256": CARD_SHA,
                  "cardOverlayWholeRuntimeBase": CARD_BASE, "cardOverlayDirectoryHex": directory.hex(),
                  "evidence": evidence, "directIoCallCensus": calls, "nativeArgumentProbes": trace,
                  "fixtureGeometry": workspace_geometry(arena, end), "addressSeedsAreNotAllocations": True,
                  "nativeFileBlockBytes": 0x2000, "nativePrefixBytes": 0x1400,
                  "tailInitializedOrReadByObservedNativePaths": False, "persistentExtensionRamOwnerProven": False,
                  "extensionTransactionOrHooksImplemented": False, "biosExecuted": False,
                  "writesCard": False, "writesImage": False, "publicationApproved": False}
    finally:
        image.close()
    return dict(heldAndNamedOriginalFinal=True, **result)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_usa_bin", type=Path)
    args = parser.parse_args()
    print(json.dumps(capture(args.original_usa_bin), indent=2))
