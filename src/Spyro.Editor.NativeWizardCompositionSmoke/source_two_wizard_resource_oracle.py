#!/usr/bin/env python3
"""Independent raw-source two-Wizard composition and bounded native slices.

Reads only the exact original disc and pinned, existing raw-source test helpers.
No Core assemblies, candidate/payload files, emulator, save or RAM writes. The
native scalar slices use synthetic owned memory. Allocator/RNG/renderer/award
helpers are not executed; the child-loop allocator boundary is explicitly stubbed.
External family labels describe the fixture, not identities encoded in native rows.
"""
import argparse
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import runpy
import struct
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPERS = {
    "append": ("src/Spyro.Editor.NativeWizardAppendSmoke/source_wizard_append_oracle.py", "10cf2dce864a98366d94f48ba3dc356e0cb4379ff5db991bbbf8b6fdc190768f"),
    "composition": ("src/Spyro.Editor.NativeResourceCompositionSmoke/source_composition_oracle.py", "4db4d62e30e4184c297e957e2244c46c6030016a0f43703cf3c08ccab8b297fc"),
    "overlay": ("src/Spyro.Editor.NativeMobyDependencyClosureSmoke/source_overlay_oracle.mjs", "394d5971720dc808c35705dcb79201d24f7ada49d877dd10b826c43fbb6d84df"),
    "scalar": ("src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_owner_lifecycle_oracle.py", "4374150efed3d518ae5a6ae0aeae6b8fe05e03b940961049a480759c9f958429"),
}
INTENTS = [("z", 92832, 25248, 10445, 91808, 25248),
           ("a", 86688, 25248, 10445, 87712, 25248)]
SPANS = [
    ("visible-list-tail", "dark", 0x80082914, 0x4C, "93e30da4cb5ceba3232f2ef282fff31f44c453a6833e8aa70f25d5ca7f0f0a36"),
    ("visible-row-active", "dark", 0x8007AEDC, 0x20, "bbcdd4de44c075eb2a42e4fb0b281aab54f5e06777536a38725f0611e5acb250"),
    ("current-wizard-private-owner", "donor", 0x80082818, 0x34, "ee3f8d06a1fe1f65e0666604d4251a4e8da92d0da2df760390753a07e74b5e40"),
    ("child-free-and-four-loop", "donor", 0x8007EB98, 0x7C, "5a9f20f75dedb336789de75e7778e0a24d41045f7c471e7b68e5982e0711a876"),
    ("current-wizard-death-call", "donor", 0x800828C8, 0x14, "ca83fe86286714e440334c486bb64ca44d014dbd616c8bbbafd9d51503081ad4"),
    ("native-scene-arena-loader", "exe", 0x80013F24, 0xAC, "6338caf975b0fa524052abe3e2b439292791273aac359dc5e6d78b90d0a37e33"),
    ("native-selector-value", "exe", 0x8003BA64, 0x58, "5a7677129481343250dfaa32337c82b805d64902f154ea5c0af991a460947e48"),
]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def u32(data, offset):
    return struct.unpack_from("<I", data, offset)[0]


def put(data, offset, value):
    struct.pack_into("<I", data, offset, value)


def imported(name):
    path = ROOT / HELPERS[name][0]
    require(sha(path.read_bytes()) == HELPERS[name][1], "Reviewed helper changed: " + name)
    spec = importlib.util.spec_from_file_location("two_wizard_" + name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main(path):
    if not __debug__:
        raise AssertionError("Oracle requires assertions enabled in pinned helpers")
    raw, native = imported("append"), imported("scalar")
    checks = []

    def check(ok, label):
        require(ok, label)
        checks.append(label)

    for name, (relative, expected) in HELPERS.items():
        check(sha((ROOT / relative).read_bytes()) == expected, "Pinned raw helper " + name)
    # The old script reconstructs pixels, descriptor/sample appends, root0 and
    # generic packages from original sectors. It supplies bytes, never C# hashes.
    # Its original CLI and all its default golden checks remain untouched.
    previous = sys.argv
    prior_stdout = io.StringIO()
    try:
        sys.argv = [str(ROOT / HELPERS["composition"][0]), path]
        with contextlib.redirect_stdout(prior_stdout):
            resources = runpy.run_path(sys.argv[0])
    finally:
        sys.argv = previous
    check(prior_stdout.getvalue().endswith("NATIVE_RESOURCE_COMPOSITION_ORACLE_OK sourceUnchanged=true outputsWritten=0\n"), "Prior raw composition completed")
    original, donor_data = bytes(resources["original"]), bytes(resources["donor"])
    shared = bytes(resources["with_particle"])
    check(len(shared) == len(original) + 0x13000, "One shared resource allocation growth")
    with open(path, "rb") as source:
        native.identity(source)
        exe = native.logical(source, 53875 * 2048, 0x66000)
        dark = native.logical(source, 37 * 2048 + 0xEF6000, 0xB000)
        donor_code = native.logical(source, 37 * 2048 + 0x2F5B000, 0x10000)
        check(sha(exe) == native.EXE_SHA and sha(dark) == native.DARK_SHA, "Exact native EXE and Dark overlay")
        check(sha(donor_code) == "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1", "Exact donor overlay")
        header = native.logical(source, 37 * 2048, 0x800)
        check(struct.unpack_from("<II", header, 14 * 8) == (0xF01000, 0x28D800) and
              struct.unpack_from("<II", header, 40 * 8) == (0x2F6B000, 0x28A000), "Native directory source entries")
        check(native.logical(source, 37 * 2048 + 0xF01000, len(original)) == original and
              native.logical(source, 37 * 2048 + 0x2F6B000, len(donor_data)) == donor_data, "Raw shared-resource source pairing")
        scene_start, scene_length = struct.unpack_from("<II", original, 24)
        scene = original[scene_start:scene_start + scene_length]
        donor_scene = donor_data[0x1CE800:0x1E0000]
        moby, count, props, props_end, fixups, _, fields = raw.scene_layout(scene)
        donor_layout = raw.scene_layout(donor_scene, require_zero_tail=False)
        check((moby, count, props, props_end, fixups, len(fields)) == (0x8C30, 128, 0xE038, 0xEED4, 0x10EF0, 147), "Original native scene owners")
        donor_row_at = donor_layout[0] + 8 + 6 * 0x58
        donor_props_at = u32(donor_scene, donor_row_at)
        row = donor_scene[donor_row_at:donor_row_at + 0x58]
        private = donor_scene[donor_props_at:donor_props_at + 0x50]
        check(donor_row_at == 0x6388 and donor_props_at == 0xC350 and sha(row) == "59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82" and
              sha(private) == "5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b", "Complete source33/T6 row and private owner")
        check([f for f in donor_layout[-1] if donor_row_at <= f < donor_row_at + 0x58] == [donor_row_at] and
              [f for f in donor_layout[-1] if donor_props_at <= f < donor_props_at + 0x50] == [donor_props_at], "Complete donor pointer schema")
        check(u32(private, 0x38) - u32(row, 20) == 287 and row[0x3A] == 0x7F and row[0x48:0x4A] == b"\0\0" and row[0x53] == 0x56, "Donor route delta and deterministic unclaimed reward")
        final_scene_at, final_scene_length = struct.unpack_from("<II", shared, 24)
        check(final_scene_length == len(scene) == 0x11800 and final_scene_at == scene_start + 0x13000, "One final scene outer owner")
        scenes, data_outputs, owner_outputs = [], [], []
        for extra in (1, 2):
            appended = bytearray(scene)
            delta = extra * len(private)
            appended[props_end + delta:fixups + delta] = scene[props_end:fixups]
            put(appended, moby + 4, count + extra)
            put(appended, props, u32(scene, props) + delta)
            final_fields = list(fields)
            owners = []
            for i, (family, x, y, z, end_x, end_y) in enumerate(INTENTS[:extra]):
                row_at, private_at = moby + 8 + (count + i) * 0x58, props_end + i * len(private)
                check(not any(scene[row_at:row_at + 0x58]), "Original unused row capacity " + family)
                appended[row_at:row_at + 0x58] = row
                appended[private_at:private_at + 0x50] = private
                put(appended, row_at, private_at)
                put(appended, private_at, private_at + 0x28)
                final_fields.extend((row_at, private_at))
                owners.append(dict(familyId=family, nativeIndex=count + i, rowOffset=row_at, propertiesOffset=private_at))
            put(appended, fixups + delta, len(final_fields))
            for i, field in enumerate(final_fields):
                put(appended, fixups + delta + 4 + 4 * i, field)
            check(raw.scene_layout(appended)[-1] == final_fields, "Complete appended fixup set")
            check(sha(appended) == ("9ac99fdd150133a75aac8298da4b738dcbcdd12a1ab649088f53c300bd1a37c4" if extra == 1 else "0766f7e16adc2a58ccbc2797d6aba0d2ca4cd5ef5d788ee99599f27e9bd78fb7"), "Prior append raw golden")
            if extra == 1:
                check(shared[final_scene_at:final_scene_at + final_scene_length] == appended, "Prior composition scene independently reproduced")
            placed = bytearray(appended)
            fields9 = []
            for owner, (_, x, y, z, end_x, end_y) in zip(owners, INTENTS):
                r, p = owner["rowOffset"], owner["propertiesOffset"]
                values = [(r + 12, x), (r + 16, y), (r + 20, z), (p + 0x30, x), (p + 0x34, y),
                          (p + 0x38, z + 287), (p + 0x40, end_x), (p + 0x44, end_y), (p + 0x48, z + 287)]
                for at, value in values:
                    fields9.append((at, u32(placed, at)))
                    put(placed, at, value)
            inverse = bytearray(placed)
            for at, before in fields9:
                put(inverse, at, before)
            check(inverse == appended and len(set(at for at, _ in fields9)) == 9 * extra, "Exact nine-field coordinate inverse per owner")
            check(sha(placed) == ("6b34477c459224cbbf4153336d9c4f7e2aada20d9085c975c1eca2ef74c0d16f" if extra == 1 else "9dbdf8839f19cf31becda27b52b5a854f6badb8b59ad1db110c921055e5263dc"), "Prior placed-scene raw golden")
            check(placed[moby + 8:moby + 8 + count * 0x58] == scene[moby + 8:moby + 8 + count * 0x58] and
                  placed[props + 4:props_end] == scene[props + 4:props_end] and
                  placed[props_end + delta:fixups + delta] == scene[props_end:fixups], "All resident rows/private/suffix preserved")
            composed = shared[:final_scene_at] + placed + shared[final_scene_at + final_scene_length:]
            check(len(composed) == len(shared) and composed[:final_scene_at] == shared[:final_scene_at] and
                  composed[final_scene_at + final_scene_length:] == shared[final_scene_at + final_scene_length:], "No layering over any shared resources")
            restored = composed[:final_scene_at] + shared[final_scene_at:final_scene_at + final_scene_length] + composed[final_scene_at + final_scene_length:]
            check(restored == shared, "Complete composed-data inverse to raw shared owner")
            scenes.append(bytes(placed)); data_outputs.append(bytes(composed)); owner_outputs.append(owners)

        # The independently implemented Node oracle emits complete source-linked
        # overlay bytes internally and hashes them. No per-instance code is added.
        overlay_run = subprocess.run(["node", str(ROOT / HELPERS["overlay"][0]), path], cwd=ROOT,
                                     text=True, capture_output=True, check=True, timeout=120)
        overlay = json.loads(overlay_run.stdout)
        copy = int(overlay["copyBuffer"], 16)
        check(copy == 0x8007AA38 + overlay["overlayLength"], "Copy buffer follows complete shared overlay")
        low = native.signed(copy & 65535, 16)
        high = (copy - low) >> 16
        copy_exe = bytearray(exe)
        check(exe[0x4B048:0x4B058].hex() == "0880023c945542240880013cd88522ac", "Exact original copy-buffer instructions")
        put(copy_exe, 0x4B048, 0x3C020000 | high)
        put(copy_exe, 0x4B04C, 0x24420000 | (low & 65535))
        check(sha(copy_exe) == overlay["exeSha"], "Independent copy-buffer EXE matches shared overlay oracle")

        code, certificates = {}, []
        for name, owner, pc, length, expected in SPANS:
            data, origin = {"exe": (exe, 0x8000F800), "dark": (dark, 0x8007AA38), "donor": (donor_code, 0x8007AA38)}[owner]
            span = data[pc - origin:pc - origin + length]
            check(len(span) == length and sha(span) == expected, "Native source pin " + name)
            for i in range(0, length, 4):
                check(pc + i not in code, "Disjoint executable instruction owner")
                code[pc + i] = u32(span, i)
            certificates.append(dict(name=name, owner=owner, pc=f"{pc:08X}", length=length, sha256=expected))

        # Actual class->award compare chain. Stop after the JAL delay at the
        # award helper's entry; audio/collection/unlink are NOT synthesized.
        reward = native.Scalar(code)
        reward.map(0x80100000, 0x58); reward.poke(0x80100036, 0x56, 2)
        reward.r[16] = 0x80100000
        reward.run(0x8003BA64, {0x8003B854})
        check(reward.r[4] == 10 and reward.r[5] == 0x80100000, "Actual class56 value10 and delayed current-row argument")
        pete = native.logical(source, 37 * 2048 + 0x7B8800, 0x800)
        model56 = native.logical(source, 37 * 2048 + 0x7EBF24, 0x108)
        check(struct.unpack_from("<III", pete, 20) == (0x33724, 0x56, 0x3382C) and
              sha(model56) == "52404b4a45b86d26298f62a8b97fa92703fdd7ca6ea5cbde025e17ac8ce46ea8", "Resident PETE complete reward model56")
        target_at = 0x5FC38 + 2 * (6 * (12 // 10 - 1) + 12 % 10)
        check(target_at == 0x5FC3C and struct.unpack_from("<H", copy_exe, target_at)[0] == 100, "Original Dark target100")
        exe_outputs = []
        for owners in owner_outputs:
            after = bytearray(copy_exe)
            struct.pack_into("<H", after, target_at, 100 + len(owners) * reward.r[4])
            inverse = bytearray(after); struct.pack_into("<H", inverse, target_at, 100)
            check(inverse == copy_exe, "Reward target exact two-byte inverse")
            exe_outputs.append(bytes(after))

        # Native data used-end excludes zero sector padding; model/scene CD reads
        # retain their full padded owners. This is address arithmetic, not fit.
        used_start = u32(shared, 8); cursor = used_start
        for _ in range(6): cursor += u32(shared, cursor)
        portals = u32(shared, cursor); cursor += 4
        for _ in range(portals):
            cursor += 0x34 + 12 * u32(shared, cursor + 4)
            cursor += u32(shared, cursor)
        particle_at = cursor; cursor += u32(shared, cursor)
        sound_at = cursor; cursor += u32(shared, cursor)
        check(not any(shared[cursor:used_start + u32(shared, 12)]), "Native used-data tail is zero padding")
        scene_ram = copy + cursor - used_start + u32(shared, 20)
        moby_ram = scene_ram + moby + 8
        owners = owner_outputs[1]
        table = 0x801F0000; stack = 0x801F1000; sentinel = 0xDEADBEE0
        scalar = native.Scalar(code)
        scalar.map(scene_ram, len(scenes[1])); scalar.map(table, 12); scalar.map(stack, 0x1D8)
        scalar.map(0x800758E4, 4)
        for i, byte in enumerate(scenes[1]): scalar.memory[scene_ram + i] = byte
        row_pcs = [moby_ram + owner["nativeIndex"] * 0x58 for owner in owners]
        props_pcs = [scene_ram + owner["propertiesOffset"] for owner in owners]
        for i, (row_pc, private_pc) in enumerate(zip(row_pcs, props_pcs)):
            scalar.poke(table + 4 * i, row_pc)
            scalar.poke(row_pc, private_pc)  # Explicit native-relocation entry witness.
        scalar.poke(stack + 0x188, table); scalar.poke(stack + 0x1D4, sentinel)
        scalar.poke(0x800758E4, 0x800844A0); scalar.r[29] = stack
        visited = []
        for i, owner in enumerate(owners):
            scalar.run(0x80082914, {0x8007AEDC, sentinel})
            check(scalar.r[19] == row_pcs[i] and scalar.read(stack + 0x188) == table + 4 * (i + 1), "Native visible-list advances to " + owner["familyId"])
            scalar.run(0x8007AEDC, {0x8007AEF0})
            scalar.run(0x80082818, {0x80082840})
            check(scalar.r[16] == props_pcs[i] and scalar.r[6] == row_pcs[i], "Current Wizard loads its own private owner " + owner["familyId"])
            scalar.run(0x800828C8, {0x8003ABC0})
            check(scalar.r[4:8] == [row_pcs[i], 3, 0, 0], "Actual per-Wizard death-emitter call arguments " + owner["familyId"])
            visited.append(owner["nativeIndex"])
        scalar.run(0x80082914, {sentinel})
        check(visited == [128, 129] and scalar.r[29] == stack + 0x1D8 and scalar.read(stack + 0x188) == table + 12,
              "Two-owner native continuation and terminal epilogue; handlers stopped separately")

        # Execute the native arena initializer through the MULTU entry. Only its
        # final unsigned MULTU/MFHI arithmetic is evaluated separately below;
        # this pinned helper deliberately has no HI/LO instruction implementation.
        loader = native.Scalar(code); loader.map(scene_ram, len(scenes[1]))
        for i, byte in enumerate(scenes[1]): loader.memory[scene_ram + i] = byte
        for address in (0x80075828, 0x80075890, 0x8007573C, 0x800756A4, 0x800756E8, 0x80075930): loader.map(address, 4)
        loader.r[16] = loader.r[5] = scene_ram + moby
        loader.run(0x80013F24, {0x80013FB4})
        capacity = ((loader.r[2] * loader.r[4]) >> 32) & 0xFFFFFFFF
        check(code[0x80013FB4] == 0x00440019 and code[0x80013FB8] == 0x00004010 and
              loader.r[4] == 0x24924926 and capacity == 89, "Pinned unsigned multiply-high yields native two-owner capacity89")
        check(loader.read(0x80075828) == moby_ram and loader.read(0x80075890) == moby_ram + 130 * 0x58 and
              loader.read(0x800756E8) == scene_ram + props and loader.read(0x80075930) == scene_ram + props and
              loader.read(0x800756A4) == 0, "Actual native row/dynamic/arena/cursor initialization")

        # The allocator is explicitly NOT executed. Each call boundary supplies a
        # distinct synthetic child and increments active by one as an assumption.
        # Native guard, current-parent arguments, counter delay and heading store
        # are executed, making free20/21 and four-cap behavior reproducible.
        guards = []
        for initial_free, expected_counts in ((20, [0, 0]), (21, [1, 0]), (24, [4, 0]), (28, [4, 4])):
            machine = native.Scalar(code)
            for address in (0x800756A8, 0x800756A4, 0x800758CC): machine.map(address, 4)
            machine.map(0x801E0000, 8 * 0x58)
            machine.poke(0x800756A8, 89); machine.poke(0x800756A4, 89 - initial_free)
            machine.poke(0x800758CC, 0x80086DD8)
            counts, calls = [], []
            for parent in row_pcs:
                machine.r[19], machine.r[17], machine.r[16] = parent, 0, 7
                before = len(calls)
                pc = 0x8007EBA8  # Explicit entry after unexecuted RNG/reset.
                while True:
                    stop = machine.run(pc, {0x8007EBDC, 0x80086A50})
                    if stop == 0x80086A50: break
                    check(code[stop] == 0x0040F809 and machine.r[2] == 0x80086DD8 and machine.r[4:6] == [0x26, parent],
                          "Exact guarded allocator boundary/current parent")
                    machine.run(0x8007EBE0, {0x8007EBE4})  # Actual JALR delay instruction, isolated from stub.
                    child = 0x801E0000 + len(calls) * 0x58
                    machine.r[2] = child
                    machine.poke(0x800756A4, machine.read(0x800756A4) + 1)
                    calls.append(dict(parent=f"{parent:08X}", child=f"{child:08X}", heading=machine.r[16] & 255))
                    pc = 0x8007EBE4
                counts.append(len(calls) - before)
            check(counts == expected_counts, "Native free/four-loop with explicit allocation stub free" + str(initial_free))
            for call in calls:
                check(machine.read(int(call["child"], 16) + 0x44, 1) == call["heading"], "Native child heading field store")
            guards.append(dict(initialFree=initial_free, perOwnerChildren=counts, finalFree=89 - machine.read(0x800756A4), calls=calls))

        native.identity(source)
        outputs = []
        for i in range(2):
            layout = raw.scene_layout(scenes[i])
            outputs.append(dict(familyCount=i + 1, owners=owner_outputs[i], dataByteLength=len(data_outputs[i]),
                                dataSha256=sha(data_outputs[i]), sceneSha256=sha(scenes[i]),
                                sceneOffset=final_scene_at, sceneByteLength=len(scenes[i]),
                                fixupCount=len(layout[-1]), fixupEnd=layout[-2],
                                dynamicCapacity=(props - moby - 8 - (129 + i) * 0x58) // 0x70,
                                overlaySha256=overlay["overlaySha"], executableSha256=sha(exe_outputs[i]),
                                targetOffset=target_at, targetBefore=100, targetAfter=110 + 10 * i))
        print(json.dumps(dict(marker="NATIVE_TWO_WIZARD_RESOURCE_RAW_ORACLE_OK", checks=len(checks),
                              sourceImageSha256=native.IMAGE_SHA, helperPins=HELPERS, certificates=certificates,
                              priorCompositionOutput=prior_stdout.getvalue().splitlines(), outputs=outputs,
                              shared=dict(overlayLength=overlay["overlayLength"], copyBuffer=f"{copy:08X}",
                                          copyBufferExecutableSha256=sha(copy_exe),
                                          rootHeaderSha256=sha(shared[:0x800]),
                                          modelSha256=sha(shared[u32(shared, 16):u32(shared, 16) + u32(shared, 20)]),
                                          particleOffset=particle_at, soundOffset=sound_at, usedDataBytes=cursor - used_start,
                                          sceneRam=f"{scene_ram:08X}", mobyTableRam=f"{moby_ram:08X}"),
                              nativeVisibleOwners=visited, guardedChildCalls=guards,
                              explicitBoundaries=["Synthetic relocated-row/list/stack entry witnesses; adapter, visibility builder and whole handlers not executed",
                                                  "RNG not executed; heading seed7 and reset s1=0 are explicit entries",
                                                  "JALR allocator not executed; actual delay is isolated, returned child plus active increment are stubs",
                                                  "Native unsigned MULTU/MFHI final capacity arithmetic evaluated outside the scalar interpreter",
                                                  "Award/death/particle/renderer helper bodies not executed",
                                                  "Prior exact scene golden is not a new collision/placement/body/runtime proof",
                                                  "External family IDs and accepted project/card history are not encoded by this oracle"],
                              nativeEmulatorExecution=False, completeBehaviorClosure=False,
                              placementApproved=False, runtimeFitProven=False, publicationApproved=False,
                              sourceUnchanged=True, payloadFilesWritten=0), indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source_image")
    arguments = parser.parse_args()
    main(arguments.source_image)
