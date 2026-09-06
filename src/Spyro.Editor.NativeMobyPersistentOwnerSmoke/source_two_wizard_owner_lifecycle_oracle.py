#!/usr/bin/env python3
"""Two source-native Wizard owner branches in synthetic RAM; no game/Core IO.

Original image reads only. No native RNG, spawn allocation, audio, unlink,
checksum, renderer, emulator or memory-card execution. Stdout is evidence JSON.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPER = ROOT / "src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_owner_lifecycle_oracle.py"
HELPER_SHA = "4374150efed3d518ae5a6ae0aeae6b8fe05e03b940961049a480759c9f958429"
assert hashlib.sha256(HELPER.read_bytes()).hexdigest() == HELPER_SHA
spec = importlib.util.spec_from_file_location("unchanged_owner_scalar", HELPER)
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)  # Does not invoke the old default main.

BASE, CHILD = 0x80100000, 0x80120000
LIVE, CHECKPOINT, DURABLE = 0x800778C8, 0x800778A8, 0x80077958
OWNERS = (128, 129)
DONOR_DATA_SHA = "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8"
DONOR_ROW_SHA = "59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82"
EXTRA_SPANS = [
    ("death-emitter", 0x8003ABC0, 0x51C, "979e567dd4bebbdd9cadaf8f884e1110be16f293c6590ba6214941be5be7a4a8"),
    ("collection-value-award", 0x8003B7C0, 0x310, "bdaa92cb7c343607f5bbc7462f3bf54743f8f6842bb70b9f74cee06b712038e7"),
]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source_image")
    args = parser.parse_args()
    checks, certificates, reload_results, collection_results = [], [], [], []

    def check(ok, label):
        native.require(ok, label)
        checks.append(label)

    with open(args.source_image, "rb") as image:
        native.identity(image)
        exe = native.logical(image, 53875 * 2048, 0x66000)
        dark = native.logical(image, 37 * 2048 + 0xEF6000, 0xB000)
        donor = native.logical(image, 37 * 2048 + 0x2F6B000, 0x28A000)
        check(native.sha(exe) == native.EXE_SHA and native.sha(dark) == native.DARK_SHA and
              native.sha(donor) == DONOR_DATA_SHA, "Complete original EXE/Dark/donor data owners match")
        row = donor[0x1CE800 + 0x6388:0x1CE800 + 0x63E0]
        check(native.sha(row) == DONOR_ROW_SHA and row[0x3A] == 0x7F and row[0x53] == 0x56 and
              struct.unpack_from("<H", row, 0x36)[0] == 0x11B,
              "Actual Wizard Peak33/T6 starts class11B/kind7F/reward56")
        code = {}
        for name, pc, size, expected in native.SPANS + EXTRA_SPANS:
            owner, origin = (dark, 0x8007AA38) if name.startswith("dark-") else (exe, 0x8000F800)
            data = owner[pc-origin:pc-origin+size]
            check(len(data) == size and native.sha(data) == expected, "Native source window " + name)
            for offset in range(0, size, 4):
                address, word = pc + offset, struct.unpack_from("<I", data, offset)[0]
                check(address not in code or code[address] == word, "Overlapping code preimage agrees " + hex(address))
                code[address] = word
            certificates.append(dict(name=name, pc=f"{pc:08X}", length=size, sha256=expected))

        def vm():
            m = native.Scalar(code)
            for address in (0x80075828, 0x80075890, 0x80075964): m.map(address, 4)
            m.map(0x80077880, 0x600)
            m.map(BASE, 0x6000)
            m.map(CHILD, 0x58)
            m.poke(0x80075828, BASE)
            m.poke(0x80075890, BASE + 130 * 0x58)
            m.poke(0x80075964, 2)
            for index in OWNERS:
                for offset, byte in enumerate(row): m.poke(BASE + index * 0x58 + offset, byte, 1)
            return m

        # The imported scalar implementation is pinned, and these independent
        # microcases detect accidental loss of load and JAL/JR delay semantics.
        control = native.Scalar({0x100: 0x8C820000, 0x104: 0x00401821, 0x108: 0x00402821,
                                 0x10C: 0x10000002, 0x110: 0x24060007, 0x114: 0x24060063})
        control.map(0x1000, 4); control.poke(0x1000, 42)
        control.r[4], control.r[2] = 0x1000, 9
        control.run(0x100, {0x118})
        check(control.r[3:7] == [9, 0x1000, 42, 7], "Load delay and taken-branch delay oracle control")
        call = native.Scalar({0x100: 0x0C000080, 0x104: 0x24040007, 0x200: 0x03E00008, 0x204: 0x24840001})
        call.run(0x100, {0x108})
        check(call.r[31] == 0x108 and call.r[4] == 8, "JAL/JR delay oracle control")

        def death_selection(m, index):
            m.r[19], m.r[7] = BASE + index * 0x58, 0
            return m.run(0x8003ABF4, {0x8003AC88, 0x8003AC30, 0x8003B0AC})

        for order in (OWNERS, tuple(reversed(OWNERS))):
            m = vm()
            for index in order:
                other = OWNERS[1] if index == OWNERS[0] else OWNERS[0]
                other_before = m.bytes(BASE + other * 0x58, 0x58)
                m.writes.clear()
                check(death_selection(m, index) == 0x8003AC88 and m.r[16] == 0x56,
                      f"Initial owner{index} selects deterministic56 order{order}")
                check(m.writes == [(BASE + index * 0x58 + 0x3A, 1, 0xFF)] and
                      m.bytes(BASE + other * 0x58, 0x58) == other_before,
                      f"Deterministic one-shot bit is private to owner{index}")
                m.writes.clear()
                check(death_selection(m, index) == 0x8003B0AC and not m.writes,
                      f"Second deterministic death of owner{index} is refused before emission")

        # Actual Dark child owner arithmetic, stopping after its branch delay.
        branch = code[0x800829EC]
        owner_target = 0x800829F0 + native.signed(branch & 65535, 16) * 4

        def child_owner(m, index):
            m.r[21], m.r[2], m.r[16] = BASE + index * 0x58, CHILD, 0x56
            m.run(0x8008298C, {owner_target, 0x800829F4})
            return m.read(CHILD + 0x56, 1)

        for initial in range(4):
            for order in (OWNERS, tuple(reversed(OWNERS))):
                m = vm(); m.poke(DURABLE, initial)
                expected = initial
                before = m.bytes(0x80077880, 0x600)
                for index in order:
                    check(child_owner(m, index) == index, f"Native gem child retains parent owner{index}")
                    # Native class56 value chain only: stop at the award entry,
                    # then explicitly initialize s1 for its independent bitmap
                    # subwindow. The award prologue/totals/audio are NOT executed.
                    m.r[16] = CHILD
                    check(m.run(0x8003BA64, {0x8003B854}) == 0x8003B854 and m.r[4] == 10,
                          "Actual class56 chain reaches native award with value10")
                    m.r[17] = CHILD; m.writes.clear()
                    m.run(0x8003B8C4, {0x8003B9A4})
                    expected |= 1 << (index - 128)
                    check(m.writes == [(DURABLE, 4, expected)] and m.read(DURABLE) == expected,
                          f"Dynamic reward ORs only durable owner{index}, initial{initial}, order{order}")
                after = bytearray(m.bytes(0x80077880, 0x600))
                after[DURABLE-0x80077880:DURABLE-0x80077880+4] = before[DURABLE-0x80077880:DURABLE-0x80077880+4]
                check(bytes(after) == before and expected == 3, "Collection leaves all neighboring live/checkpoint/save bytes unchanged")
                collection_results.append(dict(initial=initial, order=order, final=expected))

        # Complete 4x4 durable/checkpoint matrix, independently selecting EACH
        # actual7F/56 owner. Unlink is a STOP, not simulated actor deletion.
        for durable in range(4):
            for checkpoint in range(4):
                for index in OWNERS:
                    m = vm(); m.poke(DURABLE, durable); m.poke(CHECKPOINT, checkpoint)
                    before_rows = m.bytes(BASE, 130 * 0x58)
                    bit = 1 << (index - 128)
                    collected, retired_at_checkpoint = bool(durable & bit), bool(checkpoint & bit)
                    m.r[4], m.r[16], m.r[17], m.r[18], m.r[19], m.r[20] = BASE, index * 0x58, index, 0, 255, 1
                    m.writes.clear()
                    stop = m.run(0x800142C8, {0x800143B0, 0x80052568})
                    unlink = collected and retired_at_checkpoint
                    check((stop == 0x80052568) == unlink, f"Actual7F reload branch owner{index} durable{durable}/checkpoint{checkpoint}")
                    expected_row = bytearray(before_rows)
                    if collected and not retired_at_checkpoint:
                        expected_row[index * 0x58 + 0x53] = 255
                        check(m.writes == [(BASE + index * 0x58 + 0x53, 1, 255)], "Collected7F writes only selected owner rewardFF")
                        check(death_selection(m, index) == 0x8003AC30 and m.r[16] == 0x7F,
                              "PostcollectionFF enters native RNG branch, NOT a universal no-drop path")
                        check(m.run(0x8003AC30, {0x8006272C}) == 0x8006272C and m.r[16] == 0x0F,
                              "Native RNG call and delay execute; RNG helper itself is not executed")
                    else:
                        check(not m.writes, "Uncollected or native-unlink-stop path does not synthesize a row mutation")
                    check(m.bytes(BASE, 130 * 0x58) == bytes(expected_row) and
                          m.read(DURABLE) == durable and m.read(CHECKPOINT) == checkpoint,
                          "Other owner row and both persistent bitmaps remain exact during reload selection")
                    reload_results.append(dict(owner=index, durable=durable, checkpoint=checkpoint,
                                               stop=f"{stop:08X}", postCollectionRngSelected=collected and not retired_at_checkpoint,
                                               nativeUnlinkExecuted=False))

        for mask in range(4):
            m = vm()
            for address in (0x800772E0, 0x80077428): m.map(address, 4)
            m.poke(LIVE, mask); m.poke(DURABLE, 3 ^ mask)
            m.r[16] = 0x800778E4  # Explicit entry after unexecuted vector helper.
            m.run(0x800594A8, {0x8005951C})
            check(m.read(CHECKPOINT) == mask and m.read(DURABLE) == (3 ^ mask), "Native checkpoint copy preserves independent durable bits")
            m.poke(LIVE, 0); m.writes.clear(); m.run(0x800143D0, {0x80014400})
            check(m.read(LIVE) == mask and all(0x800778B8 <= a < 0x800778D8 for a, _, _ in m.writes),
                  "Native reload restores only checkpoint32-byte live owner")
            m.map(0x801E0000, 0x600, 0xA5)
            pattern = bytearray((i * 73 + 19) & 255 for i in range(0x480))
            struct.pack_into("<I", pattern, DURABLE - 0x80077908, mask)
            for i, value in enumerate(pattern): m.poke(0x80077908 + i, value, 1)
            m.r[16] = 0x801E0000
            m.run(0x80059A10, {0x80059A24})
            check(m.bytes(0x801E010C, 0x480) == bytes(pattern) and m.bytes(0x801E0108, 4) == b'\xA5'*4 and
                  m.bytes(0x801E058C, 4) == b'\xA5'*4, "Native36x32 save copy includes both owners and protects record neighbors")
            for i in range(0x480): m.poke(0x80077908 + i, 0, 1)
            m.run(0x80059820, {0x80059834})
            check(m.bytes(0x80077908, 0x480) == bytes(pattern), "Native save restore recovers exact two-owner masks and other35 slots")

        boundary_results = []
        for index in (255, 256):
            m = vm(); actual = child_owner(m, index)
            check(actual == (index if index < 256 else 0), "Native byte-owner boundary is255 valid /256 fallback0")
            # Demonstrate WHY the producer's <=256 placed high-water must stay;
            # these deliberately enlarged synthetic rows are not candidate data.
            m.poke(0x80075890, BASE + 258 * 0x58)
            m.r[17] = BASE + index * 0x58; m.writes.clear()
            m.run(0x8003B8C4, {0x8003B9A4})
            live, durable = 0x800778B8 + (index >> 5)*4, 0x80077948 + (index >> 5)*4
            check(m.writes == [(live, 4, 1 << (index & 31)), (durable, 4, 1 << (index & 31))],
                  "Native placed arithmetic has no clamp; source producer must refuse overflow")
            boundary_results.append(dict(index=index, nativeChildOwner=actual, liveWord=f"{live:08X}", durableWord=f"{durable:08X}",
                                         independentOwnerWithinByteDomain=index < 256))
        native.identity(image)
        check(image.tell() == native.IMAGE_LENGTH, "Complete original identity reverified after all source reads")

    print(json.dumps(dict(marker="TWO_NATIVE_WIZARD_OWNER_LIFECYCLE_OK", checkCount=len(checks),
        sourceImageSha256=native.IMAGE_SHA, exeSha256=native.EXE_SHA, darkOverlaySha256=native.DARK_SHA,
        donorDataSha256=DONOR_DATA_SHA, donorRowSha256=DONOR_ROW_SHA, scalarHelperSha256=HELPER_SHA,
        oracleSha256=native.sha(Path(__file__).read_bytes()), sourceCertificates=certificates,
        collectionOrders=collection_results, reloadMatrix=reload_results, boundaries=boundary_results,
        checks=checks, limits=["Synthetic RAM, exact native integer windows only; ADD/SUB overflow exceptions not emulated.",
            "Death prologue/emission/allocator/RNG/unlink/audio/checksum and actual save IO are not executed.",
            "Value10 and durable OR subwindows are separate; actual total update or gameplay pickup is not claimed.",
            "Postcollection RNG selection is proved; subsequent0E/0F/10/78 callbacks remain unexecuted dependencies.",
            "No candidate, Core, publisher, observer, installed app or original file mutation."],
        runtimeVerified=False, publicationApproved=False), indent=2))


if __name__ == "__main__":
    main()
