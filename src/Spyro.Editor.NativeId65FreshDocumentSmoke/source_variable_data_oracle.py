"""Independent variable collision-tree afterimages; no DLL or physical writes.

Runs preserved original-parity, three legacy disc and edited-disc controls.
New expectations are produced before production widening. The inverse digest
below is expected-original only, not execution of the C# inverse implementation.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
import pathlib
import struct
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parents[2]
EDITED = "src/Spyro.Editor.NativeId65FreshEditedDocumentResearchSmoke/source_edited_document_oracle.py"
EDITED_SHA = "afce585b1144e39ee72e79dcec64b469fbb8eefe7e7af5005e4761217a084693"
SOURCE = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
SIZE = 661547040
SECTORS = SIZE // 2352


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main(path):
    helper = ROOT / EDITED
    assert sha(helper.read_bytes()) == EDITED_SHA
    spec = importlib.util.spec_from_file_location("preserved_edited_oracle", helper)
    edited = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(edited)
    edited.require_helpers()
    output = io.StringIO()
    with contextlib.redirect_stdout(output):
        edited.main(path)
        native = edited.load("src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py", "variable_disc")
        surface = edited.load("src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py", "variable_surface")
        (baseline, overlay), *_ = native.build_bodies(path)
    text = output.getvalue()
    at = text.index('{\n  "marker": "EDITED_FRESH_DOCUMENT_AFTERIMAGE_OK"')
    control, _ = json.JSONDecoder().raw_decode(text[at:])
    assert control["expected_output_sha256"] == "ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16"
    assert control["original_form1_parity_reproduced"] and control["all46_music_controls"]
    assert sha(baseline) == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c"
    data_at, scene_at = (struct.unpack_from("<I", baseline, at)[0] for at in (8, 24))
    assert data_at == 0xDE800 and scene_at == 0xEF000
    old_tail = baseline[data_at + 8 + 324:data_at + 0x8C8]
    assert len(old_tail) == 4 + 0x80 + 0x6F8
    cases = []
    lengths = set()
    for total in range(190):
        cx = min(total, 63)
        cy = min(total - cx, 63)
        cz = total - cx - cy
        fresh = surface.fresh(cx * 4096, cy * 4096, cz * 4096, 16, 16, 0x708050, 0x406080)
        used = 8 + len(fresh) + len(old_tail)
        assert used == 0x8B0 + ((2 * (total + 6) + 3) & ~3)
        assert 0x8BC <= used <= 0xA38 and used % 4 == 0
        assert (used + 0x7FF) & ~0x7FF == 0x1000
        lengths.add(used)
    assert len(lengths) == 96
    for name, x, y, z in [("origin-zero", 0, 0, 0), ("maximum-cell-sum", 63 * 4096, 63 * 4096, 63 * 4096)]:
        fresh = surface.fresh(x, y, z, 16, 16, 0x708050, 0x406080)
        data = baseline[data_at:data_at + 8] + fresh + old_tail
        body = bytearray(baseline)
        body[data_at:data_at + 0x1000] = data + bytes(0x1000 - len(data))
        body[scene_at:scene_at + 16] = struct.pack("<iiiBBBB", x + 1, y + 1, z + 512, 0, 0, 0, 0)
        restored = body.copy()
        restored[data_at:data_at + 0x1000] = baseline[data_at:data_at + 0x1000]
        restored[scene_at:scene_at + 16] = baseline[scene_at:scene_at + 16]
        assert restored == baseline and len(body) == 0x1FB800
        cases.append(dict(name=name, floor=[x, y, z, 16, 16], spawn=[x + 1, y + 1, z + 512, 0],
                          data_used=len(data), data_sha256=sha(data), body_sha256=sha(body),
                          model_runtime=hex(0x80089ECC + len(data)),
                          scene_runtime=hex(0x80089ECC + len(data) + 0xF800),
                          known_scene_end=hex(0x80089ECC + len(data) + 0x12800),
                          append=overlay + body, forward=hashlib.sha256(), changed=0))

    with open(path, "rb") as source:
        assert source.seek(0, 2) == SIZE
        source.seek(0)
        index = bytearray(SECTORS * 32)
        full = hashlib.sha256()
        for lba in range(SECTORS):
            raw = source.read(2352)
            assert len(raw) == 2352
            full.update(raw)
            index[32 * lba:32 * lba + 32] = hashlib.sha256(raw).digest()
        assert full.hexdigest() == SOURCE and source.read(1) == b"" and source.tell() == SIZE
        assert source.seek(0, 2) == SIZE

        def sector(lba):
            assert 0 <= lba < SECTORS
            source.seek(lba * 2352)
            raw = source.read(2352)
            assert len(raw) == 2352 and hashlib.sha256(raw).digest() == index[32 * lba:32 * lba + 32]
            return raw

        exe = native.compose_exe(b"".join(sector(53875 + i)[24:2072] for i in range(204)), 2)
        assert sha(exe) == "2c1fd87e9fa8ce5c5f16be53e738405cec90c0bfc10ac9cb8cf1ce979b2e5c57"
        root, wad = bytearray(sector(22)[24:2072]), bytearray(sector(37)[24:2072])
        assert wad[79 * 8:81 * 8] == bytes(16)
        cursor, rows = 0, {}
        while cursor < 2048 and root[cursor]:
            size = root[cursor]
            assert size >= 34 and cursor + size <= 2048
            row = root[cursor:cursor + size]
            name = row[33:33 + row[32]].decode("ascii").removesuffix(";1")
            lba, length = struct.unpack_from("<I", row, 2)[0], struct.unpack_from("<I", row, 10)[0]
            assert lba == int.from_bytes(row[6:10], "big") and length == int.from_bytes(row[14:18], "big")
            assert name not in rows
            rows[name] = (cursor, lba, length)
            cursor += size
        assert rows["WAD.WAD"][1:] == (37, 0x6927000)
        assert rows["SCUS_942.28"][1:] == (53875, 0x66000) and rows["PETEXA0.STR"][1] == 60000
        for name, lba, length in [("WAD.WAD", 37, 0x6B32000), ("SCUS_942.28", 54921, 0x66000)]:
            at = rows[name][0]
            for offset, value in [(2, lba), (10, length)]:
                struct.pack_into("<I", root, at + offset, value)
                struct.pack_into(">I", root, at + offset + 4, value)
        struct.pack_into("<IIII", wad, 79 * 8, 0x6927000, len(overlay), 0x6936800, len(baseline))
        expected_original = hashlib.sha256()
        for lba in range(SECTORS):
            before = sector(lba)
            expected_original.update(before)
            for case in cases:
                payload = None
                if lba == 22:
                    payload = root
                elif lba == 37:
                    payload = wad
                elif lba == 53874:
                    payload = before[24:2072]
                elif 53875 <= lba < 54921:
                    at = (lba - 53875) * 2048
                    payload = case["append"][at:at + 2048]
                elif 54921 <= lba < 55125:
                    at = (lba - 54921) * 2048
                    payload = exe[at:at + 2048]
                after = before if payload is None else native.form1(before, payload,
                    0x81 if lba in (22, 54920, 55124) else 0)
                case["forward"].update(after)
                case["changed"] += before != after
        assert expected_original.hexdigest() == SOURCE
        source.seek(0)
        assert hashlib.file_digest(source, "sha256").hexdigest() == SOURCE
        assert source.read(1) == b"" and source.tell() == SIZE and source.seek(0, 2) == SIZE
    for case in cases:
        assert case["changed"] == 1253
        case["expected_image_sha256"] = case.pop("forward").hexdigest()
        del case["append"]
    # Frozen from the first independent replay before C# variable compilation
    # passed; changes here are not a substitute for explaining a regression.
    assert [(case["body_sha256"], case["expected_image_sha256"]) for case in cases] == [
        ("055a138680475d075299fd1d722cdfa6d26d25dbf1ece1c3ba2ca9e5303c0134",
         "0c3821141e50c74aee91d4ff4eeb7ef8632c3cd63daab624dd4d504d9ed83db2"),
        ("3c69036d98cc9734a936f9904ad7ffdd453f5376fe602bec3106a722f0cfdee9",
         "40d8cf3fb4168bdd870c8d6f73c2805e357160fa37486205f1b813b743855c2d")]
    edited.require_helpers()
    assert sha(helper.read_bytes()) == EDITED_SHA
    print(json.dumps(dict(marker="VARIABLE_DATA_INDEPENDENT_AFTERIMAGES_OK", cell_sum_cases=190,
        distinct_used_lengths=96, helper_sha256={EDITED: EDITED_SHA, **edited.HELPERS},
        legacy_full_image_controls=control["legacy_full_image_controls"],
        edited_full_image_control=control["expected_output_sha256"], original_form1_parity_control=True,
        all46_music_controls=True, cases=cases, original_before_after_sha256=SOURCE,
        expected_inverse_original_sha256=expected_original.hexdigest(), native_compilation_executed=False,
        actual_inverse_executed=False, physical_output_written=False, runtime_verified=False), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_variable_data_oracle.py original.bin")
    main(sys.argv[1])
