#!/usr/bin/env python3
"""Independent read-only raw-source / bounded scalar MIPS lifecycle oracle.

No Core parser, emulator socket, ROM/save write, native audio/unlink execution or
memory-card runtime claim. Entry registers and mapped synthetic RAM are explicit.
Only the listed exact source instruction windows are executable. Stdout is JSON.
"""
import argparse
import hashlib
import json
import os
import struct
from pathlib import Path

IMAGE_LENGTH = 661547040
IMAGE_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
EXE_SHA = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
DARK_SHA = "9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32"
SPANS = [
    ("placed-and-dynamic-award", 0x8003B7C0, 0x214, "9afcd3b814b7eec265302f63c0ceb4aae26c321f0571149aae3dcdb735d63b73"),
    ("reload-collected-and-checkpoint", 0x800142C8, 0x13C, "3a37e43ef8fcef2f4533c6f8f2ca34e4277ec66bfe1ab8f0f3120837668dd683"),
    ("checkpoint-capture", 0x80059474, 0xAC, "4af69307b6482ceea3fcaf8487afea4d7ba187b15a465035e80c5108bcf061ea"),
    ("save-restore-call", 0x80059814, 0x24, "45b12460b0eee08b1cb163de0d630bcb39a39e020b60cd310b91e8bdabe9a38f"),
    ("save-serialize-call", 0x80059A04, 0x24, "531ddec5ba1cce5f57f3f925c65e6dfac3d8bf302e8e9b2996732588debf073c"),
    ("native-word-copy", 0x80016958, 0x54, "a3b8e30c7eb4c67ee12309f645026b8b9f522276e769b38295fc7e25d40667f5"),
    ("new-level-clear-through-loader-delay", 0x80015CF8, 0x84, "d69b366b54edcb24b583182d8d7c302c2e12659071c9736b29a3e0dd75669053"),
    ("native-word-fill", 0x80016914, 0x1C, "572bd776ba509d54a3848293f5cf78731da439e3b203d100b9cb6fb2ec60decc"),
    ("death-predecrement-mode", 0x8002C85C, 0x30, "ca00eea5e6e2e8176ee37bd208a8cbb02d8b988d52d4f7bc0cd1ae346b08d203"),
    ("mode4-reload-calls", 0x8002EE74, 0x18, "ea3156e02dffccdb247b36ad001b06c2643231f18c42f4ac0a0c9dc640629dfb"),
    ("ordinary-death-reload", 0x800144C8, 0x64, "d7975716e17740884212a51f8554a84343fd0c1c11ace9c7fb8b4bb56b6a2931"),
    ("dark-byte-owner-fallback", 0x8008298C, 0x74, "dd5f8e3d8586d4e47de4ffc5108a9563814f5a66725b196b0a1a43c3373225bf"),
]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def identity(stream):
    stream.seek(0)
    h = hashlib.sha256()
    length = 0
    while block := stream.read(8 * 1024 * 1024):
        h.update(block)
        length += len(block)
    require(length == IMAGE_LENGTH and h.hexdigest() == IMAGE_SHA, "Exact original image identity changed")


def logical(stream, start, length):
    output = bytearray()
    while len(output) < length:
        position = start + len(output)
        count = min(length - len(output), 2048 - position % 2048)
        stream.seek(position // 2048 * 2352 + 24 + position % 2048)
        block = stream.read(count)
        require(len(block) == count, "Truncated raw-sector user payload")
        output.extend(block)
    return bytes(output)


def signed(value, bits=32):
    return value - (1 << bits) if value & (1 << (bits - 1)) else value


class Scalar:
    """Small test interpreter: integer subset only, strict memory/code ownership.

    A branch always executes one delay instruction. A load commits after the
    following instruction has read the old registers; a direct write to that
    same register cancels the pending load. No external helper is synthesized.
    """
    def __init__(self, code):
        self.code = code
        self.r = [0] * 32
        self.memory = {}
        self.writes = []
        self.reads = []
        self.calls = []
        self.instructions = 0

    def map(self, address, size, fill=0):
        for i in range(size):
            require(address + i not in self.memory, "Synthetic memory owner overlap")
            self.memory[address + i] = fill

    def poke(self, address, value, size=4):
        for i in range(size):
            require(address + i in self.memory, f"Unmapped fixture write {address+i:08X}")
            self.memory[address + i] = (value >> (i * 8)) & 255

    def read(self, address, size=4):
        require(size == 1 or address % size == 0, "Unaligned native scalar read")
        require(all(address + i in self.memory for i in range(size)), f"Unmapped native read {address:08X}+{size}")
        self.reads.append((address, size))
        return sum(self.memory[address + i] << (8 * i) for i in range(size))

    def write(self, address, value, size):
        require(size == 1 or address % size == 0, "Unaligned native scalar write")
        self.poke(address, value, size)
        self.writes.append((address, size, value & ((1 << (size * 8)) - 1)))

    def bytes(self, address, size):
        return bytes(self.memory[address + i] for i in range(size))

    def run(self, entry, stops, maximum=40000):
        pc, pending_branch, pending_load = entry, None, None
        for _ in range(maximum):
            if pc in stops:
                require(pending_branch is None, "Stopped before a branch delay completed")
                require(pending_load is None, "Stopped with an uncommitted load")
                return pc
            require(pc in self.code and pc % 4 == 0, f"Unowned native instruction {pc:08X}")
            w = self.code[pc]
            op, rs, rt, rd, shift, fn = w >> 26, (w >> 21) & 31, (w >> 16) & 31, (w >> 11) & 31, (w >> 6) & 31, w & 63
            imm = signed(w & 65535, 16)
            a, b = self.r[rs], self.r[rt]
            old_branch, old_load = pending_branch, pending_load
            pending_branch = pending_load = None
            written = set()

            def put(register, value):
                if register:
                    self.r[register] = value & 0xFFFFFFFF
                    written.add(register)

            def branch(target):
                nonlocal pending_branch
                require(old_branch is None, "Control transfer in a branch delay is not in this bounded profile")
                pending_branch = target & 0xFFFFFFFF

            if op == 0:
                if fn == 0: put(rd, b << shift)
                elif fn == 2: put(rd, b >> shift)
                elif fn == 3: put(rd, signed(b) >> shift)
                elif fn == 4: put(rd, b << (a & 31))
                elif fn == 6: put(rd, b >> (a & 31))
                elif fn == 7: put(rd, signed(b) >> (a & 31))
                elif fn == 8: branch(a)
                elif fn in (0x20, 0x21): put(rd, a + b)
                elif fn in (0x22, 0x23): put(rd, a - b)
                elif fn == 0x24: put(rd, a & b)
                elif fn == 0x25: put(rd, a | b)
                elif fn == 0x26: put(rd, a ^ b)
                elif fn == 0x2A: put(rd, int(signed(a) < signed(b)))
                elif fn == 0x2B: put(rd, int(a < b))
                else: raise AssertionError(f"Unsupported SPECIAL {w:08X} at {pc:08X}")
            elif op in (2, 3):
                target = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
                if op == 3:
                    put(31, pc + 8)
                    self.calls.append((pc, target))
                branch(target)
            elif op in (4, 5): branch(pc + 4 + imm * 4 if (a == b) == (op == 4) else pc + 8)
            elif op in (8, 9): put(rt, a + imm)
            elif op == 10: put(rt, int(signed(a) < imm))
            elif op == 11: put(rt, int(a < (imm & 0xFFFFFFFF)))
            elif op == 12: put(rt, a & (w & 65535))
            elif op == 13: put(rt, a | (w & 65535))
            elif op == 14: put(rt, a ^ (w & 65535))
            elif op == 15: put(rt, (w & 65535) << 16)
            elif op in (0x20, 0x21, 0x23, 0x24, 0x25):
                width = {0x20: 1, 0x21: 2, 0x23: 4, 0x24: 1, 0x25: 2}[op]
                value = self.read((a + imm) & 0xFFFFFFFF, width)
                if op in (0x20, 0x21): value = signed(value, width * 8) & 0xFFFFFFFF
                pending_load = (rt, value)
            elif op in (0x28, 0x29, 0x2B): self.write((a + imm) & 0xFFFFFFFF, b, {0x28: 1, 0x29: 2, 0x2B: 4}[op])
            else: raise AssertionError(f"Unsupported opcode {w:08X} at {pc:08X}")
            if old_load is not None and old_load[0] not in written:
                put(*old_load)
            self.r[0] = 0
            pc = old_branch if old_branch is not None else (pc + 4) & 0xFFFFFFFF
            self.instructions += 1
        raise AssertionError("Bounded native scalar instruction budget exhausted")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source_image")
    args = parser.parse_args()
    require(os.stat(args.source_image).st_size == IMAGE_LENGTH, "Original image length mismatch")
    with open(args.source_image, "rb") as source:
        identity(source)
        exe = logical(source, 53875 * 2048, 0x66000)
        dark = logical(source, 37 * 2048 + 0xEF6000, 0xB000)
        require(sha(exe) == EXE_SHA and sha(dark) == DARK_SHA, "Native code owners differ")
        code, certificates = {}, []
        for name, pc, size, expected in SPANS:
            owner, origin = (dark, 0x8007AA38) if name.startswith("dark-") else (exe, 0x8000F800)
            data = owner[pc - origin:pc - origin + size]
            require(len(data) == size and sha(data) == expected, "Native certificate changed: " + name)
            for i in range(0, size, 4): code[pc + i] = struct.unpack_from("<I", data, i)[0]
            certificates.append(dict(name=name, pc=f"{pc:08X}", length=size, sha256=expected))

        checks = []
        def check(condition, label):
            require(condition, label)
            checks.append(label)

        def vm(slot=2, count=258):
            m = Scalar(code)
            for address in (0x80075828, 0x80075890, 0x80075964): m.map(address, 4)
            m.map(0x80077880, 0x600, 0)
            m.map(0x80100000, 0x6000, 0)
            m.map(0x80120000, 0x58, 0)
            m.poke(0x80075828, 0x80100000)
            m.poke(0x80075890, 0x80100000 + count * 0x58)
            m.poke(0x80075964, slot)
            return m

        # Independent micro-fixtures ensure this interpreter itself retains old
        # load values in the next instruction and executes taken-branch/JAL delay.
        synthetic = Scalar({0x100: 0x8C820000, 0x104: 0x00401821, 0x108: 0x00402821,
                            0x10C: 0x10000002, 0x110: 0x24060007, 0x114: 0x24060063})
        synthetic.map(0x1000, 4); synthetic.poke(0x1000, 42)
        synthetic.r[4], synthetic.r[2] = 0x1000, 9
        synthetic.run(0x100, {0x118})
        check(synthetic.r[3] == 9 and synthetic.r[5] == 42 and synthetic.r[6] == 7, "scalar load and taken-branch delay control")
        jal = Scalar({0x100: 0x0C000080, 0x104: 0x24040007, 0x200: 0x03E00008, 0x204: 0x24840001})
        jal.run(0x100, {0x108})
        check(jal.r[31] == 0x108 and jal.r[4] == 8, "scalar JAL and JR delay control")

        arithmetic = []
        for index in (0, 31, 32, 127, 128, 129, 255, 256, 257):
            m = vm()
            actor = 0x80100000 + index * 0x58
            m.r[17] = actor  # s1 at the exact placed/dynamic selection window.
            m.run(0x8003B8C4, {0x8003B9A4})  # Stop BEFORE sound-map/audio call.
            live = 0x800778B8 + (index >> 5) * 4
            saved = 0x80077908 + 2 * 32 + (index >> 5) * 4
            mask = 1 << (index & 31)
            check(m.writes == [(live, 4, mask), (saved, 4, mask)], f"actual placed award writes ordinal{index}")
            check(m.read(live) == mask and m.read(saved) == mask, f"actual owner bit persisted ordinal{index}")
            arithmetic.append(dict(index=index, liveWord=f"{live:08X}", darkCollectedWord=f"{saved:08X}", mask=f"{mask:08X}", nativeInstructions=m.instructions))
            deletion = vm(); deletion.r[4] = actor; deletion.r[31] = 0xDEADBEE0
            deletion.run(0x8003B7C0, {0xDEADBEE0})
            check(deletion.writes == [(live, 4, mask)], f"native live-deletion marker writes no durable word ordinal{index}")
        check(arithmetic[-2]["liveWord"] == "800778D8" and arithmetic[-2]["darkCollectedWord"] == "80077968",
              "ordinal256 concretely overruns live32 bytes and aliases next level collected bitmap")

        dynamic = vm(); dynamic.r[17] = 0x80120000; dynamic.poke(0x80120056, 129, 1)
        dynamic.run(0x8003B8C4, {0x8003B9A4})
        check(dynamic.writes == [(0x80077958, 4, 2)], "dynamic award consumes child owner byte, not its row ordinal")

        fallback = []
        # Final BEQ's delay slot stores owner. Stop at either certified target
        # after the delay without interpreting its unrelated class dispatch.
        branch_word = code[0x800829EC]
        check(branch_word >> 26 == 4 and code[0x800829F0] >> 26 == 0x28, "native fallback delay store instruction shape")
        target = 0x800829F0 + signed(branch_word & 65535, 16) * 4
        for index in (0, 128, 255, 256, 257):
            m = vm(); m.r[21] = 0x80100000 + index * 0x58; m.r[2] = 0x80120000; m.r[16] = 0x11B
            m.run(0x8008298C, {target, 0x800829F4})
            expected = index if index < 256 else 0
            check(m.read(0x80120056, 1) == expected and m.writes == [(0x80120036, 2, 0x11B), (0x80120056, 1, expected)], f"Dark source child fallback ordinal{index}")
            fallback.append(dict(parentIndex=index, emittedOwner=expected))
        null_parent = vm(); null_parent.r[2] = 0x80120000; null_parent.r[16] = 0x11B
        null_parent.run(0x8008298C, {target, 0x800829F4})
        check(null_parent.read(0x80120056, 1) == 0, "Dark null parent binds owner0, not independent identity")

        # Execute actual native memcpy with every byte patterned, protected bytes
        # immediately outside target, and fresh zero RAM on native restore.
        saved = vm(); saved.map(0x801E0000, 0x600, 0xA5)
        pattern = bytes((i * 73 + 19) & 255 for i in range(0x480))
        for i, value in enumerate(pattern): saved.poke(0x80077908 + i, value, 1)
        saved.r[16] = 0x801E0000
        saved.run(0x80059A10, {0x80059A24})
        check(saved.bytes(0x801E010C, 0x480) == pattern and saved.bytes(0x801E0108, 4) == b'\xA5' * 4 and
              saved.bytes(0x801E058C, 4) == b'\xA5' * 4, "actual save call+memcpy copies36x32 with adjacent protection")
        check(saved.calls == [(0x80059A1C, 0x80016958)], "save oracle stops before native checksum call")
        for i in range(0x480): saved.poke(0x80077908 + i, 0, 1)
        saved.writes.clear(); saved.calls.clear(); saved.run(0x80059820, {0x80059834})
        check(saved.bytes(0x80077908, 0x480) == pattern and len(saved.writes) == 0x480 // 4,
              "actual native restore recovers exact collected bitmap bytes")

        checkpoint = vm()
        checkpoint.map(0x800772E0, 4); checkpoint.map(0x80077428, 4)
        checkpoint.poke(0x800778C8, 3)  # placed128 and129 live deletion.
        checkpoint.poke(0x80077958, 3)  # durable remains a separate owner.
        checkpoint.r[16] = 0x800778E4  # s0 after the unexecuted vector-copy helper.
        checkpoint.run(0x800594A8, {0x8005951C})
        check(checkpoint.read(0x800778A8) == 3 and checkpoint.read(0x80077958) == 3,
              "actual checkpoint capture copies live128/129 without changing durable bits")
        checkpoint.poke(0x800778C8, 0); checkpoint.writes.clear()
        checkpoint.run(0x800143D0, {0x80014400})
        check(checkpoint.read(0x800778C8) == 3 and checkpoint.read(0x80077958) == 3 and
              all(0x800778B8 <= address < 0x800778D8 for address, _, _ in checkpoint.writes),
              "actual reload restores checkpoint live32 bytes and preserves collected owner bits")

        new_level = vm()
        for address in (0x8006EFA4, 0x800774B0, 0x80077428, 0x80075830, 0x800756C8, 0x8007587C): new_level.map(address, 4)
        new_level.map(0x80078E7A, 1)
        for i in range(0x68): new_level.poke(0x80077888 + i, 0xA5, 1)
        new_level.poke(0x80077958, 3); new_level.poke(0x80075830, 1)
        new_level.run(0x80015CF8, {0x8001364C})
        check(new_level.bytes(0x80077888, 0x68) == bytes(0x68) and new_level.read(0x80075830) == 0 and
              new_level.read(0x80077958) == 3, "new-level native fill clears checkpoint/live/key but preserves durable owner bits")
        check(new_level.calls == [(0x80015D38, 0x80016914), (0x80015D74, 0x8001364C)],
              "new-level scalar path enters actual shared loader only after exact clear")
        for lives in (0, 1, 2, 255):
            death = vm(); death.map(0x8007582C, 4); death.map(0x800757D8, 4); death.poke(0x8007582C, lives)
            death.run(0x8002C85C, {0x8002C88C})
            check(death.read(0x800757D8) == (5 if lives == 0 else 4) and death.read(0x8007582C) == max(0, lives - 1),
                  f"death mode tests predecrement lives{lives}")
        same_level = vm(); same_level.map(0x80075830, 4); same_level.poke(0x80075830, 1)
        same_level.poke(0x800778A8, 3); same_level.r[3] = same_level.r[2] = 4
        same_level.run(0x8002EE74, {0x8002C8A4})
        same_level.run(0x8002EE84, {0x800144C8})
        same_level.run(0x80014520, {0x8001364C})
        check(same_level.r[4] == 1 and same_level.read(0x80075830) == 1 and same_level.read(0x800778A8) == 3 and
              same_level.calls == [(0x8002EE7C, 0x8002C8A4), (0x8002EE84, 0x800144C8), (0x80014520, 0x8001364C)],
              "death call windows bind reload(1), not new-level clear; intervening cleanup/CD helpers are NOT executed")

        # Execute reload selection only. Source native unlink52568 is a STOP,
        # not a test-provided implementation or an actor-retirement certificate.
        reload_paths = []
        for collected, checkpoint_bit, kind in ((False, False, 0x7D), (True, False, 0x7D), (True, True, 0x7D),
                                                  (True, False, 0x1B), (False, False, 0x1B)):
            m = vm(); m.poke(0x80077958, 1 if collected else 0); m.poke(0x800778A8, 1 if checkpoint_bit else 0)
            row = 0x80100000 + 128 * 0x58; m.poke(row + 0x3A, kind, 1); m.poke(row + 0x53, 6, 1)
            m.r[4], m.r[16], m.r[17], m.r[18], m.r[19], m.r[20] = 0x80100000, 128 * 0x58, 128, 0, 255, 1
            stopped = m.run(0x800142C8, {0x800143B0, 0x80052568})
            should_unlink = collected and kind == 0x7D
            check((stopped == 0x80052568) == should_unlink, "source collected selector respects7D special case " + str((collected, checkpoint_bit, kind)))
            if should_unlink: check(m.r[4] == row, "native unlink would receive exact stable owner row")
            if collected and kind != 0x7D:
                check(m.read(row + 0x53, 1) == 255 and stopped == 0x800143B0, "non7D collection disables drop, not universal actor deletion")
            reload_paths.append(dict(collected=collected, checkpoint=checkpoint_bit, kind=kind, stop=f"{stopped:08X}", unlinkExecuted=False))

        # Cheap legacy reproduction is explicitly a source-aligned scalar model,
        # not executing the App/exporter or asserting an observed historical save.
        root = Path(__file__).resolve().parents[2]
        exporter = (root / "src/Spyro.Editor.Core/Exporting/MobySourcePatchExporter.cs").read_text()
        allocator = (root / "src/Spyro.Editor.Core/Scene/NativeMobyDependencyInstantiation.cs").read_text()
        app = (root / "src/Spyro.Editor.App/Views/MainWindow.cs").read_text()
        check("appendNextTrueIndex = level.SourceRecordCount" in exporter and "appendNextTrueIndex++" in exporter,
              "legacy fresh-build append counter remains the independently reviewed path")
        check("OrderBy" in allocator and "StringComparer.Ordinal" in allocator and
              "_currentMobys.Remove(member)" in app and "_currentMobys.Add(member)" in app,
              "legacy ordering/remove/undo source paths remain present")
        allocate = lambda ids: {name: 128 + i for i, name in enumerate(ids)}
        legacy = dict(original=allocate(["A", "B"]), deletedA=allocate(["B"]), undoAppendA=allocate(["B", "A"]),
                      freshSortedZ=allocate(sorted(["z"])), freshSortedAZ=allocate(sorted(["z", "a"])),
                      executedProductionExporter=False)
        check(legacy["original"]["B"] == 129 and legacy["deletedA"]["B"] == 128 and
              legacy["freshSortedZ"]["z"] == 128 and legacy["freshSortedAZ"]["z"] == 129,
              "legacy scalar temporal/remove/undo drift reproduces native ordinal renumbering")
        identity(source)

    print(json.dumps(dict(marker="NATIVE_MOBY_OWNER_LIFECYCLE_RAW_SCALAR_OK", checks=len(checks),
                          sourceImageSha256=IMAGE_SHA, sourceExeSha256=EXE_SHA, sourceDarkOverlaySha256=DARK_SHA,
                          sourceUnchanged=True, nativeRuntime=False, memoryCardRuntime=False, fullCpuEmulation=False,
                          nativeUnlinkExecuted=False, nativeAudioExecuted=False, checksumExecuted=False,
                          certificates=certificates, ownerArithmetic=arithmetic, dynamicFallback=fallback,
                          collectedReloadPaths=reload_paths, legacyScalarDrift=legacy, assertions=checks), indent=2))


if __name__ == "__main__":
    main()
