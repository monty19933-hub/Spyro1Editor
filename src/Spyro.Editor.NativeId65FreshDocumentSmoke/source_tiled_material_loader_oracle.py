"""Independent material-tile native parser and moved-resource witnesses.

Runs preserved untextured controls, then executes pinned original integer code
on independently assembled two-material tiled data. No Core, output image,
hardware/CD completion, GTE/GPU, full scene entry or gameplay acceptance.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPERS = {
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py":
        "75f6762822c79520176bb3127e9b426822fb7b007b2ba6da931e4822cb43f283",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_material_oracle.py":
        "50fadadfe2f668705d60d203b80c01f4da2b28c012ab017238d4d6d554738237",
}


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require_helpers():
    for relative, expected in HELPERS.items():
        require(sha((ROOT / relative).read_bytes()) == expected, "Preserved helper changed: " + relative)


def load(relative, name):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main(path):
    require(__debug__, "All preserved helper assertions must execute")
    require_helpers()
    loader = load(next(iter(HELPERS)), "material_native_loader")
    material = load(list(HELPERS)[1], "material_source_reference")
    loader.require_helpers()
    material.require_helpers()
    controls = io.StringIO()
    with contextlib.redirect_stdout(controls):
        loader.main(path)
    control = json.loads(controls.getvalue())
    require(control["marker"] == "VARIABLE_NATIVE_LOADER_ORACLE_OK" and
            control["coordinate_sums"] == 190 and control["distinct_used_lengths"] == 96 and
            control["native_parser_and_sound_helper_executed"] and control["native_model_loaders_executed"],
            "Preserved native parser/delay/negative/legacy/tiled controls actually execute")
    controls = io.StringIO()
    with contextlib.redirect_stdout(controls):
        material.main(path)
    source_control = json.loads(controls.getvalue())
    require(source_control["marker"] == "TILED_MATERIAL_EXACT_SOURCE_ORACLE_OK" and
            source_control["completeDataInverse"], "Independent material source references execute")

    allocation = material.load("src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_oracle.py", "material_allocated")
    geometry = material.load("src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py", "material_geometry")
    raw_body = loader.load("src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py", "material_body_reference")
    with contextlib.redirect_stdout(io.StringIO()):
        captured = allocation.capture(path)
        (body, _), *_ = raw_body.build_bodies(path)
    require(sha(body) == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c", "Complete preserved body control")
    header, model, scene = body[:0x800], body[0xDF800:0xEF000], body[0xEF000:0xF2000]
    tail = captured.data[0x2BC:]
    require(tail == body[0xDE94C:0xDF0C8], "Independent body and material sources agree on the complete retained tail")
    before = struct.pack("<II", 8, 0) + geometry.fresh() + tail
    geometry.canonical(before, tail)
    results = []
    with open(path, "rb") as original:
        material.require_original(original)
        executable = loader.native.logical(original, 53875 * 2048, 0x66000)
        pete = loader.native.logical(original, 37 * 2048 + 0x7B9000, 0x32E1C)
        require(sha(executable) == loader.native.EXE_SHA and
                sha(pete) == "46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76", "Complete original native owners")
        code = {}
        for name, pc, length, expected in loader.SPANS:
            span = executable[pc - 0x8000F800:pc - 0x8000F800 + length]
            require(sha(span) == expected, "Exact original instruction span: " + name)
            for offset in range(0, length, 4):
                require(pc + offset not in code, "Disjoint original instruction ownership")
                code[pc + offset] = loader.word(span, offset)
        resident = loader.globals_machine(code)
        resident.blob(loader.PETE, pete)
        resident.r[4] = loader.PETE
        resident.run(0x800133E0, {loader.RETURN})
        require(resident.r[2] == loader.PETE, "Real resident generic relocation executes")
        relocated_pete = resident.bytes(loader.PETE, len(pete))

        for case in source_control["cases"]:
            data = material.compose(before, captured.records, case["orderedFaceIds"])
            require(sha(data) == material.DATA_REFERENCES[case["name"]], "Frozen whole material data owner")
            positions, cursor = [], loader.word(data)
            require(cursor == 0x178 and loader.word(data, 4) == 2 and len(data) == 0xBC8, "Measured material metadata and used data")
            for _ in range(5):
                positions.append(cursor)
                length = loader.word(data, cursor)
                require(length >= 4 and length % 4 == 0 and cursor + length <= len(data), "Complete aligned component owner")
                cursor += length
            require(data[cursor:] == tail, "Complete material-shifted particle/sound tail")
            m = loader.globals_machine(code)
            m.blob(loader.COPY, data)  # No padding or following model mapped.
            m.poke(0x800785DC, loader.COPY)
            m.poke(0x800785E0, 0xA5A5A5A5)
            m.r[16] = 0x12345678
            m.run(0x800158E0, {0x80015904})
            require(m.r[29] == loader.STACK and m.r[16] == 0x12345678 and
                    m.calls == [(0x800158E8, 0x80012D58), (0x80013208, 0x80012CF0)], "Actual parser/sound calls and return conventions")
            model_at = m.read(0x800785E0)
            require(model_at == m.r[2] == loader.COPY + len(data) == 0x8008AA94, "Native parser returns the material-shifted used end")
            require(m.read(0x800785C0) == loader.COPY + 8 and m.read(0x800785C4) == loader.COPY + 40 and
                    m.read(0x800785C8) == 2, "Native count and separated LQ/HQ table pointers derive from two actual records")
            collision = loader.COPY + positions[3] + 4
            require(m.read(0x800785D4) == collision and all(m.read(collision + field) == collision + loader.word(data, positions[3] + 4 + field)
                    for field in (8, 12, 16, 20, 24)), "All five collision pointers relocate from shifted component ownership")
            sound_at = cursor + 4 + 0x80
            count = loader.word(data, sound_at + 0x104)
            require(m.read(0x800761D4) == loader.COPY + sound_at + 4 and m.read(0x800761D0) == loader.COPY + sound_at + 0x108 and
                    all(m.read(loader.COPY + sound_at + 0x108 + 20 * i) == loader.word(data, sound_at + 0x108 + 20 * i) + 0x1010 for i in range(count)),
                    "Actual sound helper relocates every descriptor after material metadata growth")
            loaded_data = m.bytes(loader.COPY, len(data))
            m.blob(model_at, model)  # Explicit completed CD write, not hardware emulation.
            m.blob(loader.PETE, relocated_pete)
            m.poke(0x80076378, loader.PETE)
            m.poke(0x800785FC, 0x801C5BB8)
            for at in range(0, 0x1D0, 4):
                m.poke(0x80076C00 + at, loader.word(header, at))
            m.r[16] = 1
            m.run(0x80015A54, {0x800163BC})
            scene_at = m.read(0x800785E4)
            require(scene_at == model_at + len(model) == 0x8009A294 and
                    m.read(0x80078600) == 0xEF000 and m.read(0x80078604) == len(scene), "Native model loaders derive moved scene base and unchanged CD descriptor")
            require(m.read(0x80076378 + 0x78 * 4) == model_at + 0xCC30 and
                    m.read(model_at + 0xCC64) == model_at + 0xCC30 + loader.word(model, 0xCC64), "Actual companion registration and geometry relocation")
            m.blob(scene_at, scene)
            m.poke(0x8007596C, 65)
            m.run(0x80013680, {0x800136C4})
            require(m.r[16] == scene_at and m.r[18] == scene_at, "Native scene initialization reloads moved source-owned global")
            m.r[16], m.r[17], m.r[21] = scene_at + 0x80, 0, 0
            m.r[2] = loader.word(scene, 0x80)
            m.run(0x80013C4C, {0x80014084})
            require(m.read(0x80075828) == scene_at + 0xD4 and m.read(0x800756E8) == scene_at + 0x82C and
                    m.read(0x800756A8) == 16 and m.read(0x800756A4) == 0 and
                    m.read(0x80075778) == scene_at + 0x83C and m.read(0x800757B4) == 0,
                    "Bounded native scene slice derives the moved empty N16 arena and route buffer")
            require(m.bytes(loader.COPY, len(data)) == loaded_data and scene_at + len(scene) == 0x8009D294 < 0x80187BB0,
                    "Later model/scene writes preserve complete parsed data and reserved RAM boundary")
            results.append(dict(name=case["name"], data_sha256=sha(data), used=len(data),
                                model=hex(model_at), scene=hex(scene_at), end=hex(scene_at + len(scene)), instructions=m.instructions))
        material.require_original(original)
    with open(path, "rb") as final_named_original:
        material.require_original(final_named_original)
    require_helpers()
    loader.require_helpers()
    material.require_helpers()
    print(json.dumps(dict(marker="TILED_MATERIAL_NATIVE_LOADER_ORACLE_OK", helper_sha256=HELPERS,
                          legacy_control_checks=control["checks"], legacy_control_refusals=control["expected_refusals"],
                          source_reference_cases=source_control["cases"], cases=results,
                          original_sha256=material.ORIGINAL, native_parser_and_model_loaders_executed=True,
                          scene_producers_are_bounded_entry_slices=True, cd_hardware_or_completion_executed=False,
                          complete_boot_or_scene_initializer_executed=False, gpu_or_gameplay_verified=False,
                          core_executed=False, files_written=False, publication_approved=False), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_tiled_material_loader_oracle.py original.bin")
    main(sys.argv[1])
