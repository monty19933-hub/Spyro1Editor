"""Executed original native parser/model/scene witnesses for independent N1/N3.

Preserved N2/legacy controls execute first. No Core, output file, CD hardware,
GTE/GPU, full scene-entry chronology, Moby admission or gameplay acceptance.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPERS = {
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_selected_material_oracle.py":
        "7b58886dccebb1c03fe6b7d37dc23cb609d56db223f9f572e821a93a4508a715",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_material_loader_oracle.py":
        "f560da613c5bf63fb06338b5ed5d34746279eb7dd17b501732438a7903bb1945",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py":
        "75f6762822c79520176bb3127e9b426822fb7b007b2ba6da931e4822cb43f283",
}


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require_helpers():
    for relative, expected in HELPERS.items():
        require(sha((ROOT / relative).read_bytes()) == expected, "Reviewed helper changed: " + relative)


def load(relative, name):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    previous = sys.argv
    sys.argv = [str(path)]
    try:
        spec.loader.exec_module(result)
    finally:
        sys.argv = previous
    return result


def canonical(case, captured, selected, geometry):
    require(len(case.sources) in (1, 3) and len(case.records) == len(case.sources), "Separate closed N1/N3 source fixture grammar")
    expected = selected.compose(captured.before_data, case.records, case.faces, geometry, captured.tail)
    require(case.data == expected, "Complete canonical N1/N3 data owner differs")
    positions, cursor = [], selected.word(case.data)
    require(cursor == 8 + 184 * len(case.sources) and selected.word(case.data, 4) == len(case.sources),
            "Actual split table length/count, not an old178-byte metadata assumption")
    for length in (500, 32, 8, 168, 16):
        positions.append(cursor)
        require(selected.word(case.data, cursor) == length and cursor + length <= len(case.data), "Complete aligned source geometry component")
        cursor += length
    require(case.data[cursor:] == captured.tail and len(case.data) == 0xA58 + 184 * len(case.sources) <= 0x1000,
            "Complete retained tail and actual fixed-capacity used endpoint")
    return tuple(positions), cursor


def execute(case, positions, tail_at, loader, code, relocated_pete, header, model, scene):
    data, count = case.data, len(case.sources)
    m = loader.globals_machine(code)
    m.blob(loader.COPY, data)  # No following padding/model is mapped during parsing.
    m.poke(0x800785DC, loader.COPY)
    m.poke(0x800785E0, 0xA5A5A5A5)
    m.r[16] = 0x12345678
    m.run(0x800158E0, {0x80015904})
    require(m.r[29] == loader.STACK and m.r[16] == 0x12345678 and
            m.calls == [(0x800158E8, 0x80012D58), (0x80013208, 0x80012CF0)], "Actual parser/sound calls, delay retirement and return convention")
    model_at = m.read(0x800785E0)
    expected_model, expected_scene, expected_end = {
        1: (0x8008A9DC, 0x8009A1DC, 0x8009D1DC),
        3: (0x8008AB4C, 0x8009A34C, 0x8009D34C),
    }[count]
    require(model_at == m.r[2] == loader.COPY + len(data) == expected_model, "Actual returned/stored N-dependent model endpoint, not a seeded address")
    require(m.read(0x800785C0) == loader.COPY + 8 and m.read(0x800785C4) == loader.COPY + 8 + 16 * count and
            m.read(0x800785C8) == count, "Native LQ/HQ pointers and count derive from the actual selected records")
    collision = loader.COPY + positions[3] + 4
    require(m.read(0x800785D4) == collision and all(m.read(collision + field) == collision + loader.word(data, positions[3] + 4 + field)
            for field in (8, 12, 16, 20, 24)), "All five collision pointers relocate from actual moved component ownership")
    sound_at = tail_at + 4 + 0x80
    sounds = loader.word(data, sound_at + 0x104)
    require(sounds == 76 and m.read(0x800761D4) == loader.COPY + sound_at + 4 and
            m.read(0x800761D0) == loader.COPY + sound_at + 0x108 and
            all(m.read(loader.COPY + sound_at + 0x108 + 20 * i) == loader.word(data, sound_at + 0x108 + 20 * i) + 0x1010
                for i in range(sounds)), "Actual sound helper rebases every retained descriptor and both moved global pointers")
    loaded_data = m.bytes(loader.COPY, len(data))
    # Explicit completed-CD-write algebra is not execution or timing of CD.
    m.blob(model_at, model)
    m.blob(loader.PETE, relocated_pete)
    m.poke(0x80076378, loader.PETE)
    m.poke(0x800785FC, 0x801C5BB8)
    for at in range(0, 0x1D0, 4):
        m.poke(0x80076C00 + at, loader.word(header, at))
    m.r[16] = 1
    m.run(0x80015A54, {0x800163BC})
    scene_at = m.read(0x800785E4)
    require(scene_at == model_at + len(model) == expected_scene and m.read(0x80078600) == 0xEF000 and
            m.read(0x80078604) == len(scene), "Native model loaders derive moved scene base with unchanged physical CD descriptor")
    companion, geometry = model_at + 0xCC30, model_at + 0xCC30 + loader.word(model, 0xCC64)
    require(m.read(0x80076378 + 0x78 * 4) == companion and m.read(model_at + 0xCC64) == geometry,
            "Actual class78 registration and geometry relocation move with the model owner")
    require(loader.word(model, 0xCC30) == 6, "Exact six-animation companion")
    for animation in range(6):
        relative = loader.word(model, 0xCC68 + 4 * animation)
        record = companion + relative
        require(m.read(companion + 0x38 + 4 * animation) == record and
                all(m.read(record + field) == geometry + loader.word(model, 0xCC30 + relative + field) for field in (0x14, 0x18)),
                "Every companion animation and both geometry-relative fields retain their relocated source owners")
    require(m.bytes(model_at + 0xF560, 0x2A0) == model[0xF560:], "Complete registered model padding is not reused as loader storage")
    cursor, player_records = 0, 0
    while loader.word(model, cursor) != 0xFFFFFFFF:
        animation, length = loader.word(model, cursor), loader.word(model, cursor + 4)
        require(animation < 46 and length >= 0x24 and cursor + 8 + length <= 0xCC30, "Bounded original player stream")
        require(m.read(loader.PETE + 0x38 + 4 * animation) == model_at + cursor + 8, "Every actual local player animation root follows the moved model")
        cursor += 8 + length
        player_records += 1
    require(player_records > 0 and m.bytes(loader.COPY, len(data)) == loaded_data, "Native model writes preserve every parsed data byte")
    m.blob(scene_at, scene)
    m.poke(0x8007596C, 65)
    m.run(0x80013680, {0x800136C4})
    require(m.r[16] == scene_at and m.r[18] == scene_at, "Native scene entry reloads the moved global base")
    # Bounded no-updater entry only; preceding reset/helper chronology is not executed.
    m.r[16], m.r[17], m.r[21] = scene_at + 0x80, 0, 0
    m.r[2] = loader.word(scene, 0x80)
    m.run(0x80013C4C, {0x80014084})
    list_at = 0x88
    for count_slot, pointer_slot, length in ((0x560, 0x564, 12), (0x568, 0x56C, 12), (0x580, 0x584, 8),
            (0x570, 0x574, 8), (0x5A0, 0x5A4, 12), (0x588, 0x58C, 8), (0x578, 0x57C, 8)):
        require(m.read(0x80078000 + count_slot) == 0 and m.read(0x80078000 + pointer_slot) == scene_at + list_at + 8,
                "Each zero-list pointer/count is produced from the moved source-owned scene")
        list_at += length
    require(m.read(0x80075828) == scene_at + 0xD4 and m.read(0x80075890) == scene_at + 0xD4 and
            m.read(0x800756E8) == scene_at + 0x82C and m.read(0x80075930) == scene_at + 0x82C and
            m.read(0x800756A8) == 16 and m.read(0x800756A4) == 0, "Native scene walk and MULTU/MFHI derive the moved empty N16 arena")
    require(m.read(0x80075778) == scene_at + 0x83C and m.read(0x800757B4) == 0 and
            m.read(scene_at + 0xD4 + 0x48, 1) == 255 and m.read(scene_at + 0x82B, 1) == 255,
            "Scene auxiliary pointer, empty routes and both arena sentinels retain their actual owners")
    require(m.bytes(loader.COPY, len(data)) == loaded_data and scene_at + len(scene) == expected_end < 0x80187BB0,
            "All later writes preserve parsed data and remain below the separate reserved RAM boundary")
    return dict(name=case.name, selections=[(s.entry, s.texture) for s in case.sources], orderedFaceIds=case.faces,
                dataSha256=sha(data), used=len(data), model=hex(model_at), scene=hex(scene_at), end=hex(expected_end),
                playerRecords=player_records, instructions=m.instructions)


def main(path):
    require(__debug__, "Preserved helper assertions must execute")
    require_helpers()
    selected = load(list(HELPERS)[0], "selected_source_fixture")
    old = load(list(HELPERS)[1], "selected_n2_loader_control")
    loader = load(list(HELPERS)[2], "selected_native_loader")
    output = io.StringIO()
    with contextlib.redirect_stdout(output):
        old.main(path)
    control = json.loads(output.getvalue())
    require(control["marker"] == "TILED_MATERIAL_NATIVE_LOADER_ORACLE_OK" and control["native_parser_and_model_loaders_executed"] and
            len(control["cases"]) == 3 and all(c["used"] == 0xBC8 for c in control["cases"]), "Preserved N2 and inherited190-sum/96-length native controls execute")
    captured = selected.capture(path)
    geometry = selected.load("src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py", "selected_loader_geometry")
    raw_body = loader.load("src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py", "selected_loader_body")
    with contextlib.redirect_stdout(io.StringIO()):
        (body, _), *_ = raw_body.build_bodies(path)
    require(sha(body) == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c" and
            body[0xDE94C:0xDF0C8] == captured.tail, "Complete preserved physical body and source tail control")
    header, model, scene = bytes(body[:0x800]), bytes(body[0xDF800:0xEF000]), bytes(body[0xEF000:0xF2000])
    results, refusals = [], 0
    with open(path, "rb") as source:
        index = selected.source_index(source)
        executable = selected.logical(source, index, 53875, 0, 0x66000)
        pete = selected.logical(source, index, 37, 0x7B9000, 0x32E1C)
        require(sha(executable) == loader.native.EXE_SHA and sha(pete) ==
                "46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76", "Complete authenticated original native owners")
        code = {}
        for name, pc, length, expected in loader.SPANS:
            span = executable[pc - 0x8000F800:pc - 0x8000F800 + length]
            require(sha(span) == expected, "Exact original native instruction span: " + name)
            for offset in range(0, length, 4):
                require(pc + offset not in code, "Disjoint native instruction ownership")
                code[pc + offset] = loader.word(span, offset)
        resident = loader.globals_machine(code)
        resident.blob(loader.PETE, pete)
        resident.r[4] = loader.PETE
        resident.run(0x800133E0, {loader.RETURN})
        require(resident.r[2] == loader.PETE, "Actual resident generic model relocation executes")
        relocated_pete = resident.bytes(loader.PETE, len(pete))
        for case in captured.cases:
            positions, tail_at = canonical(case, captured, selected, geometry)
            for at in (0, 4, 8, positions[0] + 0x98, positions[3] + 12, len(case.data) - 1):
                corrupt = bytearray(case.data)
                corrupt[at] ^= 1
                try:
                    canonical(case._replace(data=bytes(corrupt)), captured, selected, geometry)
                except AssertionError as error:
                    require("canonical N1/N3 data owner differs" in str(error), "Exact selected canonical corruption refusal")
                    refusals += 1
                else:
                    raise AssertionError("Corrupt metadata/face/component/tail accepted")
            for corrupt in (case.data[:-1], case.data + bytes(4)):
                try:
                    canonical(case._replace(data=corrupt), captured, selected, geometry)
                except AssertionError as error:
                    require("canonical N1/N3 data owner differs" in str(error), "Exact no-truncation/no-extra-data refusal")
                    refusals += 1
                else:
                    raise AssertionError("Truncated or extended selected data accepted")
            results.append(execute(case, positions, tail_at, loader, code, relocated_pete, header, model, scene))
        selected.require_original(source)
    with open(path, "rb") as final_named_source:
        selected.require_original(final_named_source)
    require_helpers()
    selected.require_helpers()
    loader.require_helpers()
    print(json.dumps(dict(marker="TILED_SELECTED_MATERIAL_NATIVE_LOADER_ORACLE_OK", helperSha256=HELPERS,
                         originalSha256=selected.ORIGINAL, cases=results, canonicalCorruptionRefusals=refusals,
                         modelSha256=sha(model), sceneSha256=sha(scene), physicalHeaderSha256=sha(header),
                         preservedN2AndLegacyControlsExecuted=True, nativeParserSoundModelLoadersExecuted=True,
                         completePlayerCompanionRelocationsChecked=True, sceneProducersAreBoundedEntrySlices=True,
                         authenticatedSamePassPreimages=True, cdHardwareOrCompletionExecuted=False,
                         completeBootOrSceneInitializerExecuted=False, gpuOrGameplayVerified=False,
                         coreExecuted=False, filesWritten=False, publicationApproved=False), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_tiled_selected_material_loader_oracle.py original.bin")
    main(sys.argv[1])
