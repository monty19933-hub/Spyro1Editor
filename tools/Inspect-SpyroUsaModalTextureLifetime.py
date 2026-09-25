#!/usr/bin/env python3
"""Read-only exact-USA modal restore evidence; no install hook or storage authority.

The bounded scalar interpreter executes native argument/setup words while
intercepting GPU services. It proves requests/order, not GPU completion/runtime.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys


def _load_source():
    spec = importlib.util.spec_from_file_location("_modal_source_auth", Path(__file__).with_name("Inspect-SpyroJapaneseCollectibles.py"))
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


SOURCE = _load_source()
BASE = 0x8000F800
STORE, LOAD, SYNC = 0x8005FA8C, 0x8005FA28, 0x8005F764
END_GLOBAL, STAGING_GLOBAL, PHASE_GLOBAL, MODE_GLOBAL = 0x800785F0, 0x800785DC, 0x8007576C, 0x800757D8
SPANS = (
    ("complete-modal-draw", 0x8001A40C, 0x2288, "28e4eeee2309522d505feb7ccff4c3a1433928ed6d4fa7776783d146f5d15f0a"),
    ("pause-enter", 0x8002C420, 0x114, "ef4fea6275d727cb9f0d2647a0a0b5a437344c7e72db212aaf0c5d782e0d2243"),
    ("complete-pause-resume", 0x8002C534, 0xE4, "f720db0b746c096e733a92e8365708031bc7d7efe78008597f70f983bebac967"),
    ("inventory-enter", 0x8002C714, 0xA8, "f844a13c5f1cdb59288237943f34c77e9ddf8ed56c6ce7aeae752329c267fb36"),
    ("complete-inventory-resume", 0x8002C7BC, 0xA0, "4d2b9cdb3e74d1ccf47abdeeaea23ea8fc129cc43e00172cec91fa5103cfdbba"),
    ("complete-pause-update", 0x8002E12C, 0xA00, "538c7923507bf5489f95f14c863e56711115b6e0fda8f693be768bce36b9da30"),
    ("complete-inventory-update", 0x8002EB2C, 0x2C4, "8bcb87ab6cba8cabb0b2c0c59e9071daec18ab2a83cb545c23f00f642b506a3d"),
    ("mode6-update-wrapper", 0x8002F3C4, 0x20, "fe58447b7c10cda819d95d6a6fc9e4fd238248a9bb99e71d86d335b5f48f9e9e"),
    ("draw-mode-ladder", 0x8001EDDC, 0x254, "37980df646580950e30917cdd6faba99605bac020d7c803660d050a850d44021"),
    ("update-mode-ladder", 0x80033884, 0x1D0, "7e6bf1d3f61b128830ed268e38904daea449c60e10ee6a555d81f3abf52757ad"),
    ("level-upload-and-staging-reuse", 0x80015770, 0xC8, "cfc34c0b99c8c2d0b4e127ce4f707bcaa29e6623711a0e09c58a5f88ae859414"),
    ("complete-frame-arena-layout", 0x8005B6F8, 0xE0, "d0b4cf5a7cf9ee407ff413f7261bd5f92f4880d7cee8467852f4d757e6b84295"),
    ("model-descriptor-read", 0x800159FC, 0x48, "83ea25ec1c8183cfc9102d106ed5edad86807ce7c2f6e3606345bb367e7c7690"),
    ("registration-loop-and-scene-placement", 0x80015AD8, 0xA8, "4ef8e29a3058143b2905006924c846be8332409decdbe85f69582f4f846e7fa6"),
    ("scene-descriptor-read", 0x80015B80, 0x58, "edf30591c467d163e41cca4f58a8081e6ba68039326051bb1250c28d72bbc8f1"),
    ("death-life-state-entry", 0x8002C85C, 0x48, "37dee82fe6e38eaa24e03ab0cf8fdf3226b10e33857a475fd16376f7748f30ad"),
    ("death-mode0-and-player-reset", 0x8002C8A4, 0x70, "54b36185bb6f7d87e64d6de78688f4387d6f76b6351dce3d2f6ea24b2f0dc0d0"),
    ("death-scene-read-reinitialization", 0x800144C8, 0x9C, "fae8117d233b5838e56ab55c326c6d86e6a078a9642ef252bb395e6edaec8990"),
    ("return-home-level-load-entry", 0x8002C664, 0xB0, "17c22be3523fbd4e6730d25d24d5ad80530185d67d2fc4fc9ccceabd5385f423"),
    ("new-game-progression-reset", 0x80012604, 0x178, "57e807bf638fe6394f46cc7c673f5fc27e8910fc8fce0acee60761c51548538f"),
    ("native-upload-wrapper", LOAD, 0x64, "18db14f842d5a903afb21394f277e4f3cbe92358f111c16e4767ba9c21119713"),
    ("native-readback-wrapper", STORE, 0x64, "8952ec7779ea4c783a6576c4df3d08acd05470acdcca263d322005c84cea3053"),
    ("native-sync-wrapper", SYNC, 0x6C, "0e573f70016f10b87ac196ff7fa304e28d16549d9b1c5ef11f756f248e12689d"),
)


@dataclass(frozen=True)
class Rect:
    x: int
    y: int
    width: int
    height: int

    def __post_init__(self):
        if any(type(v) is not int for v in (self.x, self.y, self.width, self.height)) or self.x < 0 or self.y < 0 or self.width <= 0 or self.height <= 0 or self.x + self.width > 1024 or self.y + self.height > 512:
            raise ValueError("Rectangle leaves physical word-addressed VRAM")

    @property
    def byte_length(self):
        return self.width * self.height * 2

    def intersection_bytes(self, other):
        return max(0, min(self.x + self.width, other.x + other.width) - max(self.x, other.x)) * max(0, min(self.y + self.height, other.y + other.height) - max(self.y, other.y)) * 2

    def right_upload_rows(self):
        if self.x < 512:
            raise ValueError("Rectangle is not in the right-half upload domain")
        return tuple(((self.y + row) * 1024 + (self.x - 512) * 2, self.width * 2) for row in range(self.height))


BACKDROP = Rect(512, 0, 256, 225)
MIRROR = Rect(512, 472, 512, 40)


class SetupTrace:
    """Closed straight-line scalar subset with real load/JAL delay semantics.

    Only stack and the modeled game-state word may be written. GPU calls are
    recorded, never executed or represented as completed hardware transfers.
    """
    def __init__(self, register_seeds=None, globals_=None):
        self.reg = [0] * 32
        self.reg[29] = 0x801FF000
        for index, value in (register_seeds or {}).items():
            if index not in range(1, 32): raise ValueError("Invalid seed register")
            self.reg[index] = value & 0xFFFFFFFF
        self.memory = {}
        for address, value in (globals_ or {}).items():
            for n, byte in enumerate(struct.pack("<I", value)): self.memory[address + n] = byte
        self.pending = None
        self.events = []

    def _read(self, address, size):
        if address % size: raise ValueError("Unaligned scalar read")
        if any(address + n not in self.memory for n in range(size)): raise ValueError("Read outside seeded native owners")
        return int.from_bytes(bytes(self.memory[address + n] for n in range(size)), "little")

    def _write(self, address, value, size, pc):
        if address % size or not (0x801FEF00 <= address <= 0x801FF080 - size or address == MODE_GLOBAL and size == 4):
            raise ValueError("Store outside bounded fixture stack/game-state owner")
        for n, byte in enumerate((value & ((1 << (size * 8)) - 1)).to_bytes(size, "little")): self.memory[address + n] = byte
        if address == MODE_GLOBAL: self.events.append({"kind": "game-state-store", "pc": pc, "value": value})

    def _plain(self, word, pc):
        op, rs, rt, rd, sh, fn = word >> 26, word >> 21 & 31, word >> 16 & 31, word >> 11 & 31, word >> 6 & 31, word & 63
        imm = word & 65535; signed = imm if imm < 32768 else imm - 65536
        old = self.pending; self.pending = None; write = None
        if word == 0: pass
        elif op == 15: write = (rt, imm << 16)
        elif op == 13: write = (rt, self.reg[rs] | imm)
        elif op == 9: write = (rt, self.reg[rs] + signed)
        elif op == 0 and fn == 33: write = (rd, self.reg[rs] + self.reg[rt])
        elif op == 0 and fn == 0: write = (rd, self.reg[rt] << sh)
        elif op == 35: self.pending = (rt, self._read((self.reg[rs] + signed) & 0xFFFFFFFF, 4))
        elif op in (41, 43): self._write((self.reg[rs] + signed) & 0xFFFFFFFF, self.reg[rt], 2 if op == 41 else 4, pc)
        else: raise ValueError(f"Unmodeled native instruction {word:08X} at {pc:08X}")
        if old:
            if write and old[0] == write[0]: raise ValueError("Unmodeled conflicting load/write")
            self.reg[old[0]] = old[1]
        if write: self.reg[write[0]] = write[1] & 0xFFFFFFFF
        self.reg[0] = 0

    def run(self, code: bytes, pc: int):
        if not isinstance(code, bytes) or len(code) % 4 or pc % 4: raise ValueError("Exact aligned immutable code required")
        offset = 0
        while offset < len(code):
            word = struct.unpack_from("<I", code, offset)[0]
            if word >> 26 == 3:
                if offset + 8 > len(code) or self.pending: raise ValueError("Truncated or unsupported pending-load call")
                target = ((pc + offset + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2)
                self.reg[31] = pc + offset + 8
                self._plain(struct.unpack_from("<I", code, offset + 4)[0], pc + offset + 4)
                if target in (LOAD, STORE):
                    values = [self._read(self.reg[4] + n, 2) for n in (0, 2, 4, 6)]
                    rectangle = Rect(*values)
                    self.events.append({"kind": "load-image" if target == LOAD else "store-image", "pc": pc + offset,
                        "rect": values, "ramPointer": self.reg[5], "byteLength": rectangle.byte_length})
                elif target == SYNC:
                    if self.reg[4] != 0: raise ValueError("Only inspected blocking DrawSync(0) requests admitted")
                    self.events.append({"kind": "draw-sync-request", "pc": pc + offset, "argument": 0})
                else: raise ValueError("Unknown native service is not stubbed")
                offset += 8
            else:
                self._plain(word, pc + offset); offset += 4
        if self.pending: raise ValueError("Unconsumed load-delay result")
        return tuple(dict(event) for event in self.events)


def restore_complement(backup: Rect, changed: Rect):
    """Byte-owner counterexample, not a GPU execution model."""
    original = bytes((n * 17 + n // 1024) & 255 for n in range(0x80000))
    current = bytearray(original)
    saved = b"".join(original[offset:offset + size] for offset, size in backup.right_upload_rows())
    for offset, size in changed.right_upload_rows(): current[offset:offset + size] = bytes(value ^ 255 for value in original[offset:offset + size])
    cursor = 0
    for offset, size in backup.right_upload_rows(): current[offset:offset + size] = saved[cursor:cursor + size]; cursor += size
    differing = sum(a != b for a, b in zip(original, current))
    return {"savedBytes": len(saved), "changedBytes": changed.byte_length, "restoredChangedBytes": changed.byte_length - differing,
        "unrestoredBytes": differing, "originalSha256": hashlib.sha256(original).hexdigest(), "afterSha256": hashlib.sha256(current).hexdigest(),
        "gpuExecuted": False}


def capture(path: Path):
    source = SOURCE.AuthenticatedImage(path, SOURCE.USA)
    try:
        exe = source.read(SOURCE.USA.exe_lba, 0, SOURCE.USA.exe_size)
        if hashlib.sha256(exe).hexdigest() != SOURCE.USA.exe_digest: raise ValueError("Complete executable identity changed")
        evidence = [SOURCE.describe_span(exe, label, pc - BASE, size, BASE, digest) for label, pc, size, digest in SPANS]
        def code(pc, size): return exe[pc - BASE:pc - BASE + size]
        end, staging = 0x801F0000, 0x80090000  # scalar test seeds only, NOT allocations
        seeds = {END_GLOBAL: end, STAGING_GLOBAL: staging, PHASE_GLOBAL: 0, MODE_GLOBAL: 2}
        if code(0x8001A494, 8) != struct.pack("<II", 0x3C108008, 0x261085F0): raise ValueError("Capture pointer seed owner changed")
        captured = SetupTrace({16: END_GLOBAL}, seeds).run(code(0x8001A4DC, 0x34), 0x8001A4DC)
        resumes = [SetupTrace(globals_=seeds).run(code(pc, size), pc) for pc, size in ((0x8002C534, 0x58), (0x8002C7BC, 0x50))]
        uploads = [SetupTrace(globals_=seeds | {PHASE_GLOBAL: phase}).run(code(0x80015770, 0x40), 0x80015770) for phase in (0, 1)]
        expected_rect = [512, 0, 256, 225]
        if captured[0]["rect"] != expected_rect or captured[0]["ramPointer"] != end - 0x1C200: raise ValueError("Native capture request changed")
        for resume in resumes:
            if [event["kind"] for event in resume] != ["load-image", "draw-sync-request", "game-state-store"] or resume[0]["rect"] != expected_rect or resume[0]["ramPointer"] != end - 0x1C200 or resume[2]["value"] != 0:
                raise ValueError("Native resume request/barrier/state ordering changed")
        for phase, upload in enumerate(uploads):
            if upload[0]["rect"] != [512, phase * 256, 512, 256] or upload[0]["ramPointer"] != staging + phase * 0x40000: raise ValueError("Normal upload request changed")
        targets = {0x8002C534, 0x8002C7BC, 0x8001A40C}
        calls = []
        for offset in range(0x800, len(exe), 4):
            word = struct.unpack_from("<I", exe, offset)[0]
            target = (word & 0x3FFFFFF) << 2 | 0x80000000
            if word >> 26 in (2, 3) and target in targets: calls.append({"pc": BASE + offset, "target": target, "word": f"{word:08X}"})
        if calls != [{"pc": 0x8001EE34, "target": 0x8001A40C, "word": "0C006903"},
                     {"pc": 0x8002E9F0, "target": 0x8002C534, "word": "0C00B14D"},
                     {"pc": 0x8002EDD8, "target": 0x8002C7BC, "word": "0C00B1EF"}]: raise ValueError("Executable direct-call census changed")
        report = {"sourceSha256": SOURCE.USA.digest, "sourceByteLength": SOURCE.USA.size, "exeSha256": SOURCE.USA.exe_digest,
            "evidence": evidence, "nativeCaptureSetup": captured, "nativeResumeSetups": resumes, "nativeLevelUploadSetups": uploads,
            "setupAddressValuesAreFixtureSeedsNotAllocations": True, "executableDirectCallCensus": calls,
            "mirrorRestoreCounterexample": restore_complement(BACKDROP, MIRROR),
            "wholeFontMirrorRestoredByNativeResume": False, "completeAllExitProof": False, "gpuCompletionProven": False,
            "nativeInstallApproved": False, "publicationApproved": False, "candidateWritten": False,
            "remainingEdges": ["indirect overlay/card exits", "all reset/reentry and exception paths", "new sign-mode dispatch/lifetime",
                "owned backup and atlas-transfer CPU extents", "GPU asynchronous completion and final primitive-queue drain"]}
    finally:
        source.close()  # Rechecks held and named complete original before returning evidence.
    report["heldAndNamedOriginalFinal"] = True
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("image", type=Path)
    parser.add_argument("--report", type=Path, help="Create a new-only JSON evidence artifact; never overwrite a source or existing report")
    options = parser.parse_args()
    result = capture(options.image)
    if options.report:
        with options.report.open("x", encoding="utf-8") as output:
            output.write(json.dumps(result, indent=2) + "\n")
        print(json.dumps({"report": str(options.report.resolve()), "sourceSha256": result["sourceSha256"],
            "heldAndNamedOriginalFinal": result["heldAndNamedOriginalFinal"], "evidenceSpans": len(result["evidence"]),
            "mirrorRestoreCounterexample": result["mirrorRestoreCounterexample"], "nativeInstallApproved": False}, indent=2))
    else:
        print(json.dumps(result, indent=2))
