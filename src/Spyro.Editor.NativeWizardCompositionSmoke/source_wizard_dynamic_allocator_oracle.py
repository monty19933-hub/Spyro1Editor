#!/usr/bin/env python3
"""Exact native allocator/free/reuse slices in synthetic owned RAM, not gameplay.

Original BIN reads only. No Core/DLL, candidate, emulator, card or payload writes.
The old composition oracle remains unchanged. This executes allocator bodies,
including the actual loader MULTU/MFHI and free-list branches, not allocator stubs.
Synthetic inputs and deliberately unsafe calls are explicitly separated below.
"""
import argparse
import hashlib
import importlib.util
import json
import struct
import sys
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPERS = {
    "native": ("src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_owner_lifecycle_oracle.py", "4374150efed3d518ae5a6ae0aeae6b8fe05e03b940961049a480759c9f958429"),
    "scene": ("src/Spyro.Editor.NativeWizardAppendSmoke/source_wizard_append_oracle.py", "10cf2dce864a98366d94f48ba3dc356e0cb4379ff5db991bbbf8b6fdc190768f"),
}
SPANS = (
    ("allocator-and-free", "exe", 0x800524C4, 0x1E4, "768e4181d81333e0e27bbe6419e8d94114096a9bd8e8556314af5f0e628327ff"),
    ("scene-arena-loader", "exe", 0x80013F24, 0xAC, "6338caf975b0fa524052abe3e2b439292791273aac359dc5e6d78b90d0a37e33"),
    ("dark-child-owner", "dark", 0x8008298C, 0x74, "dd5f8e3d8586d4e47de4ffc5108a9563814f5a66725b196b0a1a43c3373225bf"),
)
GLOBALS = (0x800756A4, 0x800756A8, 0x800756E8, 0x8007573C,
           0x80075778, 0x80075828, 0x80075890, 0x80075930)
SCENE = 0x80100000  # Deliberate synthetic relocation, not a runtime observation.
RETURN = 0xDEADBEE0


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def imported(name):
    path, expected = HELPERS[name]
    path = ROOT / path
    require(sha(path.read_bytes()) == expected, "Reviewed helper changed: " + name)
    spec = importlib.util.spec_from_file_location("allocator_" + name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


native, raw = imported("native"), imported("scene")


class Machine(native.Scalar):
    """Same strict byte owners; bounded integer decoder with real load/delay order.

    Adds the native allocator's REGIMM/BLEZ/BGTZ and loader MULTU/MFHI locally;
    does not alter the frozen interpreter. No GTE, interrupts or cycle timing.
    Signed ADD/ADDI/SUB trap in this fixture instead of silently wrapping.
    """
    def __init__(self, code):
        super().__init__(code)
        self.hi = self.lo = 0
        self.executed = set()
        self.native_reads = []

    def run(self, entry, stops, maximum=40000):
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
                elif fn == 0x25: put(rd, a | b)
                else: raise AssertionError(f"Unsupported SPECIAL {word:08X} at {pc:08X}")
            elif op == 1:
                require(rt in (0, 1), "Unsupported REGIMM")
                taken = (native.signed(a) < 0) == (rt == 0)
                transfer(pc + 4 + imm * 4 if taken else pc + 8)
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
            elif op == 13: put(rt, a | (word & 65535))
            elif op == 15: put(rt, (word & 65535) << 16)
            elif op in (0x20, 0x21, 0x23):
                width = {0x20: 1, 0x21: 2, 0x23: 4}[op]
                address = (a + imm) & 0xFFFFFFFF
                self.native_reads.append((pc, address, width))
                value = self.read(address, width)
                if width < 4: value = native.signed(value, width * 8)
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


def main(path):
    require(__debug__, "Assertions must be enabled")
    checks, refusals, results = [], [], []

    def check(ok, label):
        require(ok, label)
        checks.append(label)

    def refuse(action, phrase, label):
        try:
            action()
        except AssertionError as error:
            check(phrase in str(error), label + ": exact refusal")
            refusals.append(label)
            return
        raise AssertionError("Expected refusal: " + label)

    # Independent decoder controls, including the extra native branch/HI cases.
    for op, rt, values in ((1, 0, (-1, 0, 1)), (1, 1, (-1, 0, 1)), (6, 0, (-1, 0, 1)), (7, 0, (-1, 0, 1))):
        for value in values:
            word = (op << 26) | (4 << 21) | (rt << 16) | 2
            machine = Machine({0x100: word, 0x104: 0x24050007, 0x108: 0x24060009})
            machine.r[4] = value & 0xFFFFFFFF
            machine.run(0x100, {0x10C})
            taken = (value < 0) == (rt == 0) if op == 1 else value <= 0 if op == 6 else value > 0
            check(machine.r[5] == 7 and machine.r[6] == (0 if taken else 9), "Signed branch/delay microcase")
    machine = Machine({0x100: 0x8C820000, 0x104: 0x00401821, 0x108: 0x00402821})
    machine.map(0x1000, 4); machine.poke(0x1000, 42); machine.r[4], machine.r[2] = 0x1000, 9
    machine.run(0x100, {0x10C})
    check(machine.r[3:6:2] == [9, 42], "Load delay old/new values")
    cancel = Machine({0x100: 0x8C820000, 0x104: 0x24020007, 0x108: 0x00401821})
    cancel.map(0x1000, 4); cancel.poke(0x1000, 42); cancel.r[4] = 0x1000
    cancel.run(0x100, {0x10C}); check(cancel.r[2:4] == [7, 7], "Direct same-register write cancels pending load")
    for a, b, hi in ((0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFE), (0x80000000, 2, 1), (0, 0xFFFFFFFF, 0)):
        mult = Machine({0x100: 0x00850019, 0x104: 0x00001010})
        mult.r[4:6] = [a, b]; mult.run(0x100, {0x108})
        check(mult.r[2] == hi and mult.lo == a * b & 0xFFFFFFFF, "Unsigned multiply-high control")
    jal = Machine({0x100: 0x0C000080, 0x104: 0x24040007, 0x200: 0x03E00008, 0x204: 0x24840001})
    jal.run(0x100, {0x108}); check(jal.r[4] == 8 and jal.r[31] == 0x108, "JAL/JR delays")
    refuse(lambda: Machine({0x100: 0xFFFFFFFF}).run(0x100, {0x104}), "Unsupported", "Unknown instruction")
    refuse(lambda: Machine({0x100: 0x1000FFFF, 0x104: 0}).run(0x100, set(), 16), "budget", "Finite loop")
    overflow = Machine({0x100: 0x20820001}); overflow.r[4] = 0x7FFFFFFF
    refuse(lambda: overflow.run(0x100, {0x104}), "overflow", "Signed overflow")

    with open(path, "rb") as image:
        native.identity(image)
        check(image.tell() == native.IMAGE_LENGTH and image.read(1) == b"", "Whole original hash/actual EOF before reads")
        exe = native.logical(image, 53875 * 2048, 0x66000)
        dark_overlay = native.logical(image, 37 * 2048 + 0xEF6000, 0xB000)
        dark = native.logical(image, 37 * 2048 + 0xF01000, 0x28D800)
        donor = native.logical(image, 37 * 2048 + 0x2F6B000, 0x28A000)
        check(sha(exe) == native.EXE_SHA and sha(dark_overlay) == native.DARK_SHA, "Exact complete code owners")
        check(sha(dark) == "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97" and
              sha(donor) == "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8", "Exact complete data owners")
        scene = dark[0x194000:0x1A5800]
        moby, original_count, properties, *_ = raw.scene_layout(scene)
        row = donor[0x1CE800 + 0x6388:0x1CE800 + 0x63E0]
        check((moby, original_count, properties) == (0x8C30, 128, 0xE038) and
              sha(row) == "59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82", "Source component and exact33/T6 row")
        private_prefix = scene[properties:properties + 0x18]
        check(len(private_prefix) == 0x18 and private_prefix[0x17] != 0xFE,
              "Exact original private-prefix lookahead is not a free-hole marker")
        code, certificates = {}, []
        for name, owner, pc, length, expected in SPANS:
            source, base = (exe, 0x8000F800) if owner == "exe" else (dark_overlay, 0x8007AA38)
            block = source[pc - base:pc - base + length]
            check(len(block) == length and sha(block) == expected, "Exact window " + name)
            for i in range(0, length, 4): code[pc + i] = struct.unpack_from("<I", block, i)[0]
            certificates.append(dict(name=name, pc=f"{pc:08X}", length=length, sha256=expected))
            mutated = bytearray(block); mutated[0] ^= 1
            refuse(lambda b=mutated, h=expected: require(sha(b) == h, "Source certificate changed"), "certificate", "Mutated " + name)

        def fixture(count=130, words=None):
            require(count in (129, 130), "Fixture profile count")
            m = Machine(dict(code if words is None else words))
            for address in GLOBALS: m.map(address, 4)
            start, end = SCENE + moby, SCENE + properties
            # The free-all tail peeks at end+17 in the next private component.
            # Bind that actual preserved prefix, not invented sentinel padding.
            # This is an allocator entry witness, not a complete materialized scene.
            m.map(start - 16, 16, 0xA5); m.map(start, end - start)
            m.map(end, 0x18); m.map(end + 0x18, 16, 0x5A)
            for i, value in enumerate(scene[properties:properties + 0x18]): m.memory[end + i] = value
            m.poke(end, raw.word(scene, properties) + (count - 128) * 0x50)
            for i, value in enumerate(scene[moby:properties]): m.memory[start + i] = value
            base = start + 8
            for index in range(128, count):
                for i, value in enumerate(row): m.memory[base + index * 0x58 + i] = value
            m.poke(start + 4, count)
            m.r[16] = m.r[5] = start
            m.run(0x80013F24, {0x80013FC4})
            check(m.read(0x80075828) == base and m.read(0x80075890) == base + count * 0x58 and
                  m.read(0x800756E8) == end and m.read(0x80075930) == end and m.read(0x800756A4) == 0,
                  "Actual loader owners/count/cursors")
            check(m.read(0x800756A8) == (end - base - count * 0x58) // 0x70 == (90 if count == 129 else 89),
                  "Actual MULTU/MFHI capacity one90/two89")
            return m, base, base + count * 0x58, end

        def allocate(m, base, parent_index=128):
            m.r[31] = RETURN
            m.run(0x800524C4, {RETURN})
            actor = m.r[2]
            # Execute the exact native Dark owner derivation; class initializer
            # and collision-link construction are NOT executed by this fixture.
            m.r[16], m.r[21] = 0x26, base + parent_index * 0x58
            m.run(0x8008298C, {0x800829F4})
            check(m.read(actor + 0x56, 1) == parent_index, "Actual parent-to-byte owner after allocation")
            m.poke(actor + 0x34, 0xFFFF, 2)  # Explicit unlinked collision-list entry precondition.
            return actor, m.read(actor)

        def free(m, actor):
            m.r[4], m.r[31] = actor, RETURN
            m.run(0x80052568, {RETURN})

        def disjoint(owners):
            spans = [(a, a + 0x58) for a, _ in owners] + [(p, p + 0x18) for _, p in owners]
            spans.sort()
            return all(a[1] <= b[0] for a, b in zip(spans, spans[1:]))

        def outside_unchanged(m, before, dynamic, end):
            allowed = {0x800756A4, 0x8007573C, 0x80075930}
            for address, value in before.items():
                if dynamic <= address < end or any(g <= address < g + 4 for g in allowed): continue
                require(m.memory[address] == value, f"Protected byte changed {address:08X}")

        one, _, _, _ = fixture(129)
        m, base, dynamic, end = fixture(); before = dict(m.memory)
        allocated = [allocate(m, base, 128 + i % 2) for i in range(89)]
        check(m.read(0x800756A4) == 89 and m.read(0x800756A8) == 89, "One shared pool reaches declared89, not89 per parent")
        check(all(a == dynamic + i * 0x58 and p == end - (i + 1) * 0x18 for i, (a, p) in enumerate(allocated)), "Actual contiguous rows and descending24-byte private owners")
        check(disjoint(allocated), "All89 complete row/private owners are disjoint")
        check(m.read(0x8007573C) == dynamic + 89 * 0x58 and m.read(0x80075930) == end - 89 * 0x18, "Native terminal cursors at capacity")
        outside_unchanged(m, before, dynamic, end); check(True, "All placed rows/header/redzones/unowned globals preserved")
        # This call is deliberately outside the supported allocation precondition.
        # The native routine does NOT reject capacity exhaustion or return null.
        overflow_owner = allocate(m, base)
        check(m.read(0x800756A4) == 90 and overflow_owner[0] != 0 and not disjoint(allocated + [overflow_owner]),
              "Deliberate90th native call returns overlapping row/private owners, not safe refusal")
        check(not any(address == 0x800756A8 for pc, address, _ in m.native_reads if 0x800524C4 <= pc < 0x80052568),
              "Executed allocator reads no native capacity global")
        results.append(dict(case="deliberate-over-capacity", active=90, declaredCapacity=89,
                            row=f"{overflow_owner[0]:08X}", properties=f"{overflow_owner[1]:08X}", ownerOverlap=True))

        # Free interior holes in arbitrary deletion order, then tail collapse.
        m, base, dynamic, end = fixture(); before = dict(m.memory)
        owners = [allocate(m, base, 128 + i % 2) for i in range(6)]
        free(m, owners[1][0]); free(m, owners[4][0])
        check(m.read(0x800756A4) == 4 and m.read(owners[1][0] + 0x48, 1) == 0xFE and
              m.read(owners[4][0] + 0x48, 1) == 0xFE, "Interior free marks holes and decrements active")
        reuse_a, reuse_b = allocate(m, base, 129), allocate(m, base, 128)
        check(reuse_a == owners[1] and reuse_b == owners[4],
              "Actual lowest-row/highest-properties hole reuse returns original pairs")
        current = [owners[0], reuse_a, owners[2], owners[3], reuse_b, owners[5]]
        check(disjoint(current) and m.read(0x800756A4) == 6, "Recycled owners remain independent across parents")
        for index in (2, 4, 5, 3, 1, 0): free(m, current[index][0])
        check(m.read(0x800756A4) == 0 and m.read(0x8007573C) == dynamic and m.read(0x80075930) == end and
              m.read(dynamic + 0x48, 1) == 0xFF and m.read(end - 1, 1) == 0xFF, "Tail/hole collapse restores both native cursors and sentinels")
        again = allocate(m, base, 129)
        check(again == owners[0] and m.read(again[0] + 0x56, 1) == 129, "Recycled first slot receives its current parent owner")
        outside_unchanged(m, before, dynamic, end); check(True, "Recycling preserves original rows and outer owners")
        results.append(dict(case="interior-hole-reuse", rows=[f"{a:08X}" for a, _ in (reuse_a, reuse_b)],
                            properties=[f"{p:08X}" for _, p in (reuse_a, reuse_b)], fullCollapse=True))

        m, base, dynamic, end = fixture(); before = dict(m.memory)
        for cycle in range(3):
            full = [allocate(m, base, 128 + (i + cycle) % 2) for i in range(89)]
            check(disjoint(full) and m.read(0x800756A4) == 89, "Full89 reuse cycle has disjoint live owners")
            for index in [*range(0, 89, 2), *range(87, 0, -2)]: free(m, full[index][0])
            check(m.read(0x800756A4) == 0 and m.read(0x8007573C) == dynamic and m.read(0x80075930) == end,
                  "Full89 arbitrary deletion cycle restores empty cursors")
        outside_unchanged(m, before, dynamic, end); check(True, "Three full cycles preserve complete placed prefix and outer owners")
        results.append(dict(case="three-full89-cycles", actualAllocatorCalls=267, actualFreeCalls=267,
                            owners=[128, 129], finalActive=0, disjointAtCapacity=True))
        check(any(pc == 0x80052660 and address == end + 0x17 and width == 1 for pc, address, width in m.native_reads),
              "Actual free-all needs preserved next-properties byte+17 read owner")

        # Actual collision-list traversal and splice, using an explicit valid
        # synthetic head -> predecessor -> target -> successor chain.
        m, base, dynamic, end = fixture()
        a, b, c = [allocate(m, base) for _ in range(3)]
        table = 0x801E0000; m.map(table, 4); m.poke(0x80075778, table); m.poke(table, a[0])
        m.poke(a[0] + 4, b[0]); m.poke(b[0] + 4, c[0]); m.poke(c[0] + 4, 0)
        m.poke(b[0] + 0x34, 0, 2); free(m, b[0])
        check(m.read(table) == a[0] and m.read(a[0] + 4) == c[0] and m.read(0x800756A4) == 2,
              "Native positive collision-list index traverses and unlinks interior node")
        m.poke(a[0] + 0x34, 0, 2); free(m, a[0])
        check(m.read(table) == c[0] and m.read(0x800756A4) == 1, "Native head unlink preserves successor")
        # Placed actors take FD status, not dynamic free/decrement.
        m.poke(base + 128 * 0x58 + 0x34, 0xFFFF, 2)
        count_before = m.read(0x800756A4); free(m, base + 128 * 0x58)
        check(m.read(base + 128 * 0x58 + 0x48, 1) == 0xFD and m.read(0x800756A4) == count_before,
              "Placed deletion does not consume/recycle a dynamic slot")

        # Malformed entry state must fail the bounded fixture, not be described
        # as a native safe-refusal mechanism or rolled-back allocator transaction.
        broken, base, dynamic, end = fixture(); broken.poke(0x8007573C, 0x80200000)
        refuse(lambda: allocate(broken, base), "Unmapped", "Unowned row cursor")
        check(broken.read(0x800756A4) == 1, "Native active increment precedes malformed cursor fault; no rollback claim")
        broken, base, dynamic, end = fixture(); actor, _ = allocate(broken, base)
        broken.poke(actor + 0x34, 0, 2)
        refuse(lambda: free(broken, actor), "Unmapped", "Unowned collision chain")
        broken, base, dynamic, end = fixture(); actor, _ = allocate(broken, base)
        for address in range(end, end + 0x18): del broken.memory[address]
        refuse(lambda: free(broken, actor), "Unmapped", "Missing original private-prefix lookahead")
        broken, base, dynamic, end = fixture(); actor, _ = allocate(broken, base)
        broken.poke(end + 0x17, 0xFE, 1)
        refuse(lambda: free(broken, actor), "Unmapped", "Hostile private-prefix free marker extends native scan")
        for pc, replacement, phrase in ((0x80052564, 0, "cursor"), (0x80052500, 0, "sentinel")):
            mutated = dict(code); mutated[pc] = replacement
            bad, base, dynamic, end = fixture(words=mutated)
            allocate(bad, base)
            expected = bad.read(0x80075930) == end - 0x18 if phrase == "cursor" else bad.read(dynamic + 0x58 + 0x48, 1) == 0xFF
            refuse(lambda ok=expected: require(ok, "Native " + phrase + " mismatch"), phrase, "Executed mutation " + phrase)
        for name, (relative, expected) in HELPERS.items(): check(sha((ROOT / relative).read_bytes()) == expected, "Unchanged helper " + name)
        native.identity(image)
        check(image.tell() == native.IMAGE_LENGTH and image.read(1) == b"", "Whole original hash/actual EOF after reads")

    print(json.dumps(dict(marker="NATIVE_WIZARD_DYNAMIC_ALLOCATOR_SOURCE_OK", checks=len(checks), refusals=len(refusals),
        sourceImageSha256=native.IMAGE_SHA, helperPins=HELPERS, sourceCertificates=certificates,
        privateLookahead=dict(sceneOffset=properties, byteLength=0x18, sourceSha256=sha(private_prefix),
                              sourceBytes=private_prefix.hex(), requiredByteOffset=0x17,
                              candidatePrefixLengthWordGrowth=0xA0, preservedBytesRange="04..17 inclusive"),
        oracleSha256=sha(Path(__file__).read_bytes()), scenarios=results,
        nativeAllocatorBodyExecuted=True, nativeFreeBodyExecuted=True, nativeLoaderMultiplyHighExecuted=True,
        actualEmulatorRuntime=False, peakMemoryProven=False, completeBehaviorClosure=False, publicationApproved=False,
        limits=["Synthetic relocation/arena entry with original128 plus exact two donor rows; not a complete scene materializer",
                "Parent owner derivation runs separately from allocator; no complete spawn/class initializer/collider construction",
                "Unlinked deletion inputs explicitly seed collision indexFFFF; linked cases supply a valid synthetic chain",
                "Free-all end+17 read is bound to the preserved original private-component prefix, not allocator-owned padding",
                "Allocator has no capacity guard; deliberate90th call demonstrates overlap, not supported operation",
                "MIPS integer semantics only; no cycle timing, interrupts, GTE, renderer, RNG, particle or award helper execution",
                "No runtime peak, concurrent engine scheduling, native lifetime/award/persistence or universal portability proof"],
        sourceUnchanged=True, payloadFilesWritten=0), indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source_image")
    main(parser.parse_args().source_image)
