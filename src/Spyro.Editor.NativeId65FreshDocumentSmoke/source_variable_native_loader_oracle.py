#!/usr/bin/env python3
"""Exact source scalar loader/relocation oracle, not a runtime or CD emulator.

Reconstructs canonical untextured data independently of Core, executes the real
component parser, sound helper, caller return/store, model loaders and bounded
scene pointer producers. No candidate, DLL, Core, card or emulator access.
"""
import argparse
import contextlib
import hashlib
import importlib.util
import io
import json
import struct
import sys
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
COPY, PETE, RETURN, STACK = 0x80089ECC, 0x801C63B8, 0xDEADBEE0, 0x801FF800
HELPERS = {
    "src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_owner_lifecycle_oracle.py": "4374150efed3d518ae5a6ae0aeae6b8fe05e03b940961049a480759c9f958429",
    "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py": "e69619f391814c270c9c28bc588f432f79f8a6cd3f61301e3a1c483a8645ad07",
    "src/Spyro.Editor.NativeId65NormalResourceSmoke/source_resource_oracle.py": "280087ec3b80e005872b6ec2fe793c9bbbbd69bde70ea6c64de234afa37ad76d",
    "src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py": "bcaa7349277bc5150cd17cf3e8cebbc689b68a58e9dee21a2709841c899aa592",
    "src/Spyro.Editor.NativeId65NormalBodySmoke/NativeBodySourceWindows.cs": "674ac959cd1b451fc11c3945c1446059a2d09b505e03ddda27e6fbe3851594ec",
}
SPANS = (
    ("sound-pointer-initializer", 0x80012CF0, 0x68, "ecd95b895b0f1dd85b2aed1c926a6e5e818ac3c41ddaf38565e34459808f89a0"),
    ("complete-data-parser", 0x80012D58, 0x4D8, "82ceb2a2a0ae9ddcc86caca26ceed4b24e2c6e1187c061750fec4e186f6f7ea3"),
    ("special-player-stream-loader", 0x80013230, 0x1B0, "0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb"),
    ("complete-generic-model-loader", 0x800133E0, 0x26C, "fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598"),
    ("actual-data-call-and-return-store", 0x800158E0, 0x24, "7fe4751ede7f3c6d49218f98e3a6365ff7485f784cf56d778544870ff98b694c"),
    ("registration-and-scene-base", 0x80015A54, 0x12C, "d93030965f8843f9977700d194e86cb0ed9162faa347a4287221719601728ec6"),
    ("scene-global-reload", 0x80013680, 0x44, "a976eeeeeef392e893a723ae5ab9d633a63afcb1698b4204448c0ed28589778f"),
    ("scene-lists-arena-route-buffer-fixups", 0x80013C4C, 0x438, "934e58aeaecedcabdd0202befbb9b6a23554a187ae365eb2b2acb58ade87e250"),
)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def load(relative, name):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    previous = sys.argv
    sys.argv = [str(path)]
    try:
        with contextlib.redirect_stdout(io.StringIO()):
            spec.loader.exec_module(result)
    finally:
        sys.argv = previous
    return result


def require_helpers():
    for path, expected in HELPERS.items():
        require(sha((ROOT / path).read_bytes()) == expected, "Reviewed helper changed: " + path)


require_helpers()
native = load(next(iter(HELPERS)), "variable_native_memory")


class Machine(native.Scalar):
    """Strict byte ownership plus the executed R3000 integer subset only.

    Actual load/control delays, signed traps, multiply-high; no GTE, hardware,
    cache, interrupts or timing. No host implementation stands in for a helper.
    """
    def __init__(self, code):
        super().__init__(code)
        self.hi = self.lo = 0
        self.executed = set()

    def blob(self, address, data):
        self.map(address, len(data))
        self.memory.update((address + i, v) for i, v in enumerate(data))

    def run(self, entry, stops, maximum=250000):
        pc, branch_pending, load_pending = entry, None, None
        for _ in range(maximum):
            if pc in stops:
                require(branch_pending is None and load_pending is None, "Stop before delay retirement")
                return pc
            require(pc in self.code and pc % 4 == 0, f"Unowned native instruction {pc:08X}")
            word = self.code[pc]
            op, rs, rt, rd, shift, fn = word >> 26, word >> 21 & 31, word >> 16 & 31, word >> 11 & 31, word >> 6 & 31, word & 63
            imm, a, b = native.signed(word & 65535, 16), self.r[rs], self.r[rt]
            old_branch, old_load = branch_pending, load_pending
            branch_pending = load_pending = None
            written = set()

            def put(register, value):
                if register:
                    self.r[register] = value & 0xFFFFFFFF
                    written.add(register)

            def transfer(target):
                nonlocal branch_pending
                require(old_branch is None, "Control transfer in delay slot")
                branch_pending = target & 0xFFFFFFFF

            def signed_result(value):
                require(-(1 << 31) <= value < (1 << 31), "Signed arithmetic overflow")
                return value

            if op == 0:
                if fn == 0: put(rd, b << shift)
                elif fn == 2: put(rd, b >> shift)
                elif fn == 3: put(rd, native.signed(b) >> shift)
                elif fn == 8: transfer(a)
                elif fn == 0x10: put(rd, self.hi)
                elif fn == 0x19:
                    product = a * b
                    self.hi, self.lo = product >> 32, product & 0xFFFFFFFF
                elif fn == 0x20: put(rd, signed_result(native.signed(a) + native.signed(b)))
                elif fn == 0x21: put(rd, a + b)
                elif fn == 0x22: put(rd, signed_result(native.signed(a) - native.signed(b)))
                elif fn == 0x23: put(rd, a - b)
                elif fn == 0x24: put(rd, a & b)
                elif fn == 0x25: put(rd, a | b)
                elif fn == 0x2A: put(rd, int(native.signed(a) < native.signed(b)))
                elif fn == 0x2B: put(rd, int(a < b))
                else: raise AssertionError(f"Unsupported SPECIAL {word:08X} at {pc:08X}")
            elif op == 1:
                require(rt in (0, 1), "Unsupported REGIMM")
                transfer(pc + 4 + imm * 4 if (native.signed(a) < 0) == (rt == 0) else pc + 8)
            elif op in (2, 3):
                target = ((pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2)
                if op == 3:
                    put(31, pc + 8)
                    self.calls.append((pc, target))
                transfer(target)
            elif op in (4, 5): transfer(pc + 4 + imm * 4 if (a == b) == (op == 4) else pc + 8)
            elif op in (6, 7):
                require(rt == 0, "Reserved branch field")
                transfer(pc + 4 + imm * 4 if (native.signed(a) <= 0) == (op == 6) else pc + 8)
            elif op == 8: put(rt, signed_result(native.signed(a) + imm))
            elif op == 9: put(rt, a + imm)
            elif op == 10: put(rt, int(native.signed(a) < imm))
            elif op == 11: put(rt, int(a < (imm & 0xFFFFFFFF)))
            elif op == 12: put(rt, a & (word & 65535))
            elif op == 13: put(rt, a | (word & 65535))
            elif op == 15: put(rt, (word & 65535) << 16)
            elif op in (0x20, 0x21, 0x23, 0x24, 0x25):
                width = {0x20: 1, 0x21: 2, 0x23: 4, 0x24: 1, 0x25: 2}[op]
                value = self.read((a + imm) & 0xFFFFFFFF, width)
                if op in (0x20, 0x21): value = native.signed(value, width * 8)
                # A second delayed load to the same register discards the
                # first pending value; it is not an ordinary direct write.
                if old_load is not None and old_load[0] == rt:
                    old_load = None
                load_pending = (rt, value & 0xFFFFFFFF)
            elif op in (0x28, 0x29, 0x2B):
                self.write((a + imm) & 0xFFFFFFFF, b, {0x28: 1, 0x29: 2, 0x2B: 4}[op])
            else: raise AssertionError(f"Unsupported opcode {word:08X} at {pc:08X}")
            if old_load is not None and old_load[0] not in written: put(*old_load)
            self.r[0] = 0
            self.executed.add(pc)
            self.instructions += 1
            pc = old_branch if old_branch is not None else (pc + 4) & 0xFFFFFFFF
        raise AssertionError("Bounded native scalar instruction budget exhausted")


def word(data, offset=0):
    require(offset >= 0 and offset % 4 == 0 and offset + 4 <= len(data), "Bounded aligned source word")
    return struct.unpack_from("<I", data, offset)[0]


def canonical(data, xyz, surface, tail):
    """Test-side canonical owner check, NOT a claim of native validation."""
    require(len(data) % 4 == 0 and 0x8BC <= len(data) <= 0xA38, "Canonical data alignment/length")
    require(len(xyz) == 3 and all(isinstance(v, int) and 0 <= v <= 63 * 4096 and v % 4096 == 0 for v in xyz), "Canonical coordinate bounds")
    require(data[:8] == struct.pack("<II", 8, 0), "Untextured profile is not inferred from length")
    positions, cursor = [], 8
    for _ in range(5):
        positions.append(cursor)
        length = word(data, cursor)
        require(length >= 4 and length % 4 == 0 and cursor + length <= len(data), "Component length/alignment/bounds")
        cursor += length
    require(data[cursor:] == tail, "Complete retained portals/particle/sound tail")
    expected = surface.fresh(*xyz, 16, 16, 0x708050, 0x406080)
    require(data[8:cursor] == expected, "Exact canonical surface/tree bytes")
    return positions, cursor


def globals_machine(code):
    m = Machine(code)
    m.map(0x80076278, 0x100)  # native 64 particle descriptor entries
    m.map(0x80076378, 0x800)  # native 512 model class entries
    m.map(0x80076C00, 0x1D0)  # actual copied level descriptor/root header
    m.map(0x80078560, 0x78)  # source-owned pointer/count globals, not payload padding
    m.map(0x800785DC, 0x2C)
    for at in (0x80075684, 0x800756A4, 0x800756A8, 0x800756E8, 0x800756F0, 0x800756F4,
               0x8007572C, 0x8007573C, 0x80075778, 0x800757B4, 0x800757F8, 0x80075814,
               0x80075828, 0x80075864, 0x80075890, 0x800758BC, 0x800758C8,
               0x80075930, 0x8007596C, 0x800761D0, 0x800761D4, 0x80076234,
               0x80077780, 0x80077784, 0x80077788, 0x8007778C, 0x80077790, 0x80078A40):
        m.map(at, 4)
    m.map(STACK - 0x100, 0x200, 0xA5)
    m.r[29], m.r[31] = STACK, RETURN
    return m


def main(path):
    require(__debug__, "Assertions must be enabled")
    checks, refused, rows = [], [], []

    def check(ok, label):
        require(ok, label)
        checks.append(label)

    def reject(action, phrase, label):
        try:
            action()
        except AssertionError as error:
            check(phrase in str(error), label + ": exact refusal: " + str(error))
            refused.append(label)
            return
        raise AssertionError("Expected refusal: " + label)

    surface = load("src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py", "variable_surface")
    raw_body = load("src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py", "variable_body")
    with contextlib.redirect_stdout(io.StringIO()):
        (body, _), *_ = raw_body.build_bodies(path)
    check(sha(body) == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c", "Independent baseline whole body unchanged")
    header, model, scene = body[:0x800], body[0xDF800:0xEF000], body[0xEF000:0xF2000]
    tail = body[0xDE94C:0xDF0C8]
    check(len(tail) == 0x77C and len(model) == 0xF800 and len(scene) == 0x3000, "Complete fixed physical owners")
    with open(path, "rb") as image:
        native.identity(image)
        check(image.read(1) == b"" and image.tell() == native.IMAGE_LENGTH, "Actual source EOF before scalar capture")
        exe = native.logical(image, 53875 * 2048, 0x66000)
        pete = native.logical(image, 37 * 2048 + 0x7B9000, 0x32E1C)
        check(sha(exe) == native.EXE_SHA and sha(pete) == "46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76", "Complete source executable/PETE owners")
        code = {}
        for name, start, length, expected in SPANS:
            data = exe[start - 0x8000F800:start - 0x8000F800 + length]
            check(sha(data) == expected, "Exact native pin: " + name)
            for offset in range(0, length, 4):
                require(start + offset not in code, "Overlapping code ownership")
                code[start + offset] = word(data, offset)
            bad = bytearray(data); bad[-1] ^= 1
            reject(lambda: require(sha(bad) == expected, "Source instruction pin mismatch"), "pin mismatch", "Mutated " + name)

        # Establish the resident player through the actual generic relocator,
        # not fabricated relocated pointers. This is a separate boot-entry
        # witness: a0=the exact package owner, sp=owned stack, ra=stop sentinel.
        resident = globals_machine(code)
        resident.blob(PETE, pete)
        resident.r[4] = PETE
        resident.run(0x800133E0, {RETURN})
        check(resident.r[2] == PETE and resident.read(PETE + 0x34) == PETE + word(pete, 0x34), "Actual resident player relocation")
        relocated_pete = resident.bytes(PETE, len(pete))

        def parser(data):
            m = globals_machine(code)
            m.blob(COPY, data)  # no following model/padding mapped during parse
            m.poke(0x800785DC, COPY)
            m.poke(0x800785E0, 0xA5A5A5A5)  # poison, NOT expected model base
            m.r[16] = 0x12345678
            m.run(0x800158E0, {0x80015904})
            check(m.r[29] == STACK and m.r[16] == 0x12345678, "Native parser restores stack/s0 through real return")
            check(m.calls == [(0x800158E8, 0x80012D58), (0x80013208, 0x80012CF0)], "Actual parser and sound helper called; no helper stubs")
            return m

        lengths = set()
        for total in range(190):
            cx, cy = min(total, 63), min(max(total - 63, 0), 63)
            xyz = (cx * 4096, cy * 4096, (total - cx - cy) * 4096)
            data = struct.pack("<II", 8, 0) + surface.fresh(*xyz, 16, 16, 0x708050, 0x406080) + tail
            positions, tail_at = canonical(data, xyz, surface, tail)
            used = 0x8B0 + ((2 * (total + 6) + 3) & ~3)
            check(len(data) == used and (used + 0x7FF) & ~0x7FF == 0x1000, "Independent used length and fixed CD sector arithmetic")
            m = parser(data)
            model_at = m.read(0x800785E0)
            check(model_at == COPY + used and m.r[2] == model_at, "Native returned endpoint/store, not seeded model address")
            collision = COPY + positions[3] + 4
            check(m.read(0x800785D4) == collision and all(m.read(collision + field) == collision + word(data, positions[3] + 4 + field)
                  for field in (8, 12, 16, 20, 24)), "All five collision pointers relocated from actual component base")
            sound_at = tail_at + 4 + 0x80
            count = word(data, sound_at + 0x104)
            check(m.read(0x800761D4) == COPY + sound_at + 4 and m.read(0x800761D0) == COPY + sound_at + 0x108 and
                  all(m.read(COPY + sound_at + 0x108 + 20 * i) == word(data, sound_at + 0x108 + 20 * i) + 0x1010 for i in range(count)),
                  "Actual sound helper rebases every retained descriptor and installs moved map/descriptor pointers")
            loaded_data = m.bytes(COPY, used)
            # Explicit completed CD-write algebra, NOT execution or timing of CD.
            m.blob(model_at, model)
            m.blob(PETE, relocated_pete)
            m.poke(0x80076378, PETE)
            m.poke(0x800785FC, 0x801C5BB8)
            for at in range(0, 0x1D0, 4): m.poke(0x80076C00 + at, word(header, at))
            m.r[16] = 1  # source caller's explicit root0-present branch witness
            m.run(0x80015A54, {0x800163BC})
            scene_at = m.read(0x800785E4)
            check(scene_at == model_at + 0xF800 and m.read(0x80078600) == 0xEF000 and m.read(0x80078604) == 0x3000,
                  "Native scene runtime base derives actual endpoint plus full model CD allocation")
            check(m.read(0x80076378 + 0x78 * 4) == model_at + 0xCC30 and m.read(model_at + 0xCC64) == model_at + 0xCC30 + word(model, 0xCC64),
                  "Actual class78 registration and shared geometry relocation move with used data")
            companion, geometry = model_at + 0xCC30, model_at + 0xCC30 + word(model, 0xCC64)
            require(word(model, 0xCC30) == 6, "Exact six-animation companion")
            for animation in range(6):
                relative = word(model, 0xCC68 + 4 * animation)
                record = companion + relative
                check(m.read(companion + 0x38 + 4 * animation) == record and
                      all(m.read(record + field) == geometry + word(model, 0xCC30 + relative + field)
                          for field in (0x14, 0x18)),
                      "Every class78 animation and both geometry-relative fields retain relocated source owners")
            check(m.bytes(model_at + 0xF560, 0x2A0) == model[0xF560:],
                  "Complete registered model padding is retained, not reused as loader storage")
            cursor, player_records = 0, 0
            while word(model, cursor) != 0xFFFFFFFF:
                animation, length = word(model, cursor), word(model, cursor + 4)
                require(animation < 46 and length >= 0x24 and cursor + 8 + length <= 0xCC30, "Bounded original player stream")
                check(m.read(PETE + 0x38 + 4 * animation) == model_at + cursor + 8, "Actual local player animation root moves with model owner")
                cursor += 8 + length
                player_records += 1
            check(player_records > 0 and m.bytes(COPY, used) == loaded_data, "All source local roots retained without overwriting used data")
            m.blob(scene_at, scene)
            m.poke(0x8007596C, 65)
            m.run(0x80013680, {0x800136C4})
            check(m.r[16] == scene_at and m.r[18] == scene_at, "Actual scene initializer reloads moved global base")
            # Bounded entry after the earlier header/reset helpers: original
            # slot14 is at scene+80; s1=0 and s5=0 select the no-updater arm.
            # Those skipped helper bodies/entry chronology are NOT executed.
            m.r[16], m.r[17], m.r[21] = scene_at + 0x80, 0, 0
            m.r[2] = word(scene, 0x80)
            m.run(0x80013C4C, {0x80014084})
            list_at = 0x88
            for count_slot, pointer_slot, length in ((0x560, 0x564, 12), (0x568, 0x56C, 12),
                    (0x580, 0x584, 8), (0x570, 0x574, 8), (0x5A0, 0x5A4, 12),
                    (0x588, 0x58C, 8), (0x578, 0x57C, 8)):
                check(m.read(0x80078000 + count_slot) == 0 and
                      m.read(0x80078000 + pointer_slot) == scene_at + list_at + 8,
                      "Every zero-list pointer/count is derived from the moved scene owner")
                list_at += length
            check(m.read(0x80075828) == scene_at + 0xD4 and m.read(0x80075890) == scene_at + 0xD4 and
                  m.read(0x800756E8) == scene_at + 0x82C and m.read(0x80075930) == scene_at + 0x82C and
                  m.read(0x800756A8) == 16 and m.read(0x800756A4) == 0,
                  "Real list walk and MULTU/MFHI derive relocated empty16 Moby arena")
            check(m.read(0x80075778) == scene_at + 0x83C and m.read(0x800757B4) == 0 and
                  m.read(scene_at + 0xD4 + 0x48, 1) == 255 and m.read(scene_at + 0x82B, 1) == 255,
                  "Scene auxiliary buffer, empty routes and both native arena sentinels remain correctly owned")
            check(m.bytes(COPY, used) == loaded_data and scene_at + len(scene) <= 0x80187BB0,
                  "Scene pointer production preserves used data and remains below separate reserved buffers")
            lengths.add(used)
            rows.append(dict(cell_sum=total, xyz=list(xyz), used=used, data_sha256=sha(data), model=hex(model_at),
                             scene=hex(scene_at), arena_end=hex(m.read(0x800756E8)), instructions=m.instructions))

        check(len(lengths) == 96 and min(lengths) == 0x8BC and max(lengths) == 0xA38, "All96 canonical lengths executed across190 coordinate sums")
        legacy = body[0xDE800:0xDF0C8]
        check(len(legacy) == 0x8C8 and sha(legacy) == "821d25d4c012aa180d369f755d82a780dbcf244b4e53c6c4bcf349da533dce65" and
              parser(legacy).read(0x800785E0) == 0x8008A794, "Exact legacy data golden/native endpoint unchanged")

        origin = struct.pack("<II", 8, 0) + surface.fresh(0, 0, 0, 16, 16, 0x708050, 0x406080) + tail
        collision_at = 8 + 172 + 32 + 8
        bad_tree = bytearray(origin); bad_tree[collision_at + 32] ^= 1
        reject(lambda: canonical(bytes(bad_tree), (0, 0, 0), surface, tail), "surface/tree", "Malformed tree rejected by canonical owner check")
        check(parser(bytes(bad_tree)).read(0x800785E0) == COPY + len(origin),
              "Native pointer parser does not validate tree contents: canonical validation remains essential")
        for value, label in ((0, "zero component"), (173, "unaligned component"), (0x10000, "out-of-owner component")):
            damaged = bytearray(origin); struct.pack_into("<I", damaged, 8, value)
            reject(lambda: canonical(bytes(damaged), (0, 0, 0), surface, tail), "Component", label)
        malformed = bytearray(origin); struct.pack_into("<I", malformed, 8, 173)
        reject(lambda: parser(bytes(malformed)), "Unaligned", "Native access alignment negative")
        malformed = bytearray(origin); struct.pack_into("<I", malformed, 8, 0x10000)
        reject(lambda: parser(bytes(malformed)), "Unmapped", "Native component owner escape negative")
        check(parser(origin[:-1]).read(0x800785E0) == COPY + len(origin),
              "Native parser can return beyond a truncated unread descriptor tail: exact owner validation remains essential")
        reject(lambda: parser(origin[:-17]), "Unmapped", "Native incomplete final descriptor sampled start word")
        reject(lambda: canonical(origin[:-1], (0, 0, 0), surface, tail), "alignment/length", "Canonical truncated length")
        reject(lambda: canonical(origin, (-4096, 0, 0), surface, tail), "coordinate", "Negative cell bound")
        reject(lambda: canonical(origin, (64 * 4096, 0, 0), surface, tail), "coordinate", "Maximum cell bound")
        maximum = struct.pack("<II", 8, 0) + surface.fresh(63 * 4096, 63 * 4096, 63 * 4096, 16, 16, 0x708050, 0x406080) + tail
        noncanonical = bytearray(maximum); struct.pack_into("<I", noncanonical, 4, 1)
        reject(lambda: canonical(bytes(noncanonical), (63 * 4096,) * 3, surface, tail), "profile", "SameA38 length with foreign metadata cannot acquire untextured profile")
        check(len(maximum) == 0xA38, "Untextured maximum equals known textured length; length is not authority")

        # Independent decoder negative/delay controls. Production source pins
        # above are checked separately; these synthetic words are test-only.
        delay = Machine({0x100: 0x8C820000, 0x104: 0x00401821, 0x108: 0x00402821})
        delay.blob(0x1000, struct.pack("<I", 42)); delay.r[4], delay.r[2] = 0x1000, 9
        delay.run(0x100, {0x10C}); check(delay.r[3] == 9 and delay.r[5] == 42, "Load-delay old/new microcontrol")
        cancel = Machine({0x100: 0x8C820000, 0x104: 0x24020007, 0x108: 0x00401821})
        cancel.blob(0x1000, struct.pack("<I", 42)); cancel.r[4] = 0x1000
        cancel.run(0x100, {0x10C}); check(cancel.r[2:4] == [7, 7], "Same-register write cancels delayed load")
        consecutive = Machine({0x100: 0x8C820000, 0x104: 0x8C820004, 0x108: 0x00401821, 0x10C: 0x00402821})
        consecutive.blob(0x1000, struct.pack("<II", 42, 77)); consecutive.r[4], consecutive.r[2] = 0x1000, 9
        consecutive.run(0x100, {0x110})
        check(consecutive.r[3] == 9 and consecutive.r[5] == 77 and consecutive.r[2] == 77,
              "Consecutive same-target loads cancel the earlier pending write")
        jal = Machine({0x100: 0x0C000080, 0x104: 0x24040007, 0x200: 0x03E00008, 0x204: 0x24840001})
        jal.run(0x100, {0x108}); check(jal.r[4] == 8 and jal.r[31] == 0x108, "JAL/JR actual delay microcontrol")
        for a, b, hi in ((0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFE), (0x80000000, 2, 1), (0, 0xFFFFFFFF, 0)):
            multiply = Machine({0x100: 0x00850019, 0x104: 0x00001010})
            multiply.r[4:6] = [a, b]; multiply.run(0x100, {0x108})
            check(multiply.r[2] == hi and multiply.lo == a * b & 0xFFFFFFFF, "Unsigned multiply-high independent edge control")
        signed_load = Machine({0x100: 0x84820000, 0x104: 0, 0x108: 0x94830000, 0x10C: 0})
        signed_load.blob(0x1000, bytes([0xFF, 0xFF])); signed_load.r[4] = 0x1000
        signed_load.run(0x100, {0x110})
        check(signed_load.r[2:4] == [0xFFFFFFFF, 0xFFFF], "Signed/unsigned halfword extension control")
        overflow = Machine({0x100: 0x20820001}); overflow.r[4] = 0x7FFFFFFF
        reject(lambda: overflow.run(0x100, {0x104}), "overflow", "Signed ADDI overflow traps")
        reject(lambda: Machine({0x100: 0xFFFFFFFF}).run(0x100, {0x104}), "Unsupported", "Unknown opcode refuses")
        reject(lambda: Machine({0x100: 0x1000FFFF, 0x104: 0}).run(0x100, set(), 16), "budget", "Finite execution budget")
        native.identity(image)
        check(image.read(1) == b"" and image.tell() == native.IMAGE_LENGTH, "Whole original/actual EOF unchanged after source work")
    require_helpers()
    print(json.dumps(dict(marker="VARIABLE_NATIVE_LOADER_ORACLE_OK", checks=len(checks), expected_refusals=len(refused),
                         original_sha256=native.IMAGE_SHA, complete_executable_sha256=native.EXE_SHA,
                         source_pins=[dict(name=n, pc=hex(a), length=l, sha256=h) for n, a, l, h in SPANS],
                         helper_pins=HELPERS, coordinate_sums=190, distinct_used_lengths=96, cases=rows,
                         refusal_cases=refused, native_parser_and_sound_helper_executed=True,
                         model_global_seeded_to_expected=False, native_model_loaders_executed=True,
                         scene_producers_are_bounded_entry_slices=True, source_tree_query_executed=False,
                         cd_hardware_or_completion_executed=False, complete_boot_or_scene_initializer_executed=False,
                         particle_runtime_pool_or_callbacks_executed=False, runtime_or_publication_approved=False), indent=2))


if __name__ == "__main__":
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument("original_bin")
    main(args.parse_args().original_bin)
