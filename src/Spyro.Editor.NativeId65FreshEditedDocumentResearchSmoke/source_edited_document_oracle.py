"""Independent proposed edited-document afterimage; no DLL or candidate writes.

Reuses preserved independent scalar/body/sector encoders, not production C#.
This research draft only prints evidence; it grants no publication authority.
"""
import contextlib
import hashlib
import io
import importlib.util
import json
import pathlib
import struct
import sys

sys.dont_write_bytecode = True
REPO = pathlib.Path(__file__).resolve().parents[2]
SOURCE = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
SIZE = 661547040
SECTORS = SIZE // 2352
HELPERS = {
    "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py": "f93780c582df180f378a4121a824d01dec6f13e3c5da68a61af571772e732608",
    "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py": "e69619f391814c270c9c28bc588f432f79f8a6cd3f61301e3a1c483a8645ad07",
    "src/Spyro.Editor.NativeId65NormalResourceSmoke/source_resource_oracle.py": "280087ec3b80e005872b6ec2fe793c9bbbbd69bde70ea6c64de234afa37ad76d",
    "src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py": "bcaa7349277bc5150cd17cf3e8cebbc689b68a58e9dee21a2709841c899aa592",
    "src/Spyro.Editor.NativeId65NormalBodySmoke/NativeBodySourceWindows.cs": "674ac959cd1b451fc11c3945c1446059a2d09b505e03ddda27e6fbe3851594ec",
}
INTENT = dict(kind="spyro-native-fresh-intent", schema=1,
              floor=dict(x=8192, y=8192, z=8704, width=2048, depth=2048),
              spawn=dict(x=9216, y=9216, heading=96),
              display=dict(floorRgb=[72, 132, 120], skyRgb=[44, 66, 100]), musicTrackId=2)


def load(relative, name):
    path = REPO / relative
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    saved = sys.argv
    sys.argv = [str(path)]
    try:
        spec.loader.exec_module(value)
    finally:
        sys.argv = saved
    return value


def digest(data):
    return hashlib.sha256(data).hexdigest()


def require_helpers():
    for relative, expected in HELPERS.items():
        assert digest((REPO / relative).read_bytes()) == expected, relative


def main(path):
    require_helpers()
    sys.path.insert(0, str(REPO / "src/Spyro.Editor.NativeId65FreshDiscSmoke"))
    native = load("src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py", "edited_disc")
    surface = load("src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py", "edited_surface")
    # Run the controls, not merely import their functions: real original Form1
    # parity, complete canonical Form2 gap, all46 music EXEs and three full images.
    control_output = io.StringIO()
    with contextlib.redirect_stdout(control_output):
        native.main(path)
        (baseline, overlay), *_ = native.build_bodies(path)
    control_text = control_output.getvalue()
    control = json.JSONDecoder().raw_decode(control_text[control_text.index("{"):])[0]
    assert control["marker"] == "FRESH_ID65_DISC_RAW_ORACLE_OK"
    assert control["original_form1_parity_reproduced"]
    assert [row["output_sha256"] for row in control["outputs"]] == [
        "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188",
        "40047bd8e40f23b522bfaa04dd72353013fbcfddbbd9ba6b1e357b87e8664343",
        "e6d5f2abd512814bac54f48b2d3bb3255d09dc4d5abfb6aef87ddf82ea43fc08"]

    assert digest(baseline) == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c"
    assert len(baseline) == 0x1FB800
    body = bytearray(baseline)
    data = struct.unpack_from("<I", body, 8)[0]
    scene = struct.unpack_from("<I", body, 24)[0]
    old_surface = surface.fresh(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080)
    fresh_surface = surface.fresh(8192, 8192, 8704, 2048, 2048, 0x788448, 0x64422C)
    assert len(fresh_surface) == len(old_surface) and body[data + 8:data + 8 + len(old_surface)] == old_surface
    body[data + 8:data + 8 + len(old_surface)] = fresh_surface
    body[scene:scene + 16] = struct.pack("<iiiBBBB", 9216, 9216, 9216, 0, 0, 96, 0)
    restored = body.copy()
    restored[data + 8:data + 8 + len(old_surface)] = old_surface
    restored[scene:scene + 16] = baseline[scene:scene + 16]
    assert restored == baseline
    assert struct.unpack_from("<I", body, scene + 0xD0)[0] == 0
    assert struct.unpack_from("<I", body, scene + 0xCC)[0] == 8 + 16 * 0x70 + 0x58

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

        def logical(lba, length):
            assert length % 2048 == 0
            return b"".join(sector(lba + i)[24:2072] for i in range(length // 2048))

        original_exe = logical(53875, 0x66000)
        assert digest(original_exe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
        exe = native.compose_exe(original_exe, 2)
        original_root = logical(22, 2048)
        original_wad = logical(37, 2048)
        assert original_wad[79 * 8:81 * 8] == bytes(16)
        root, wad = bytearray(original_root), bytearray(original_wad)
        cursor, rows = 0, {}
        while cursor < 2048 and root[cursor]:
            length = root[cursor]
            assert length >= 34 and cursor + length <= 2048
            row = root[cursor:cursor + length]
            name = row[33:33 + row[32]].decode("ascii").removesuffix(";1")
            lba, size = struct.unpack_from("<I", row, 2)[0], struct.unpack_from("<I", row, 10)[0]
            assert lba == int.from_bytes(row[6:10], "big") and size == int.from_bytes(row[14:18], "big")
            assert name not in rows
            rows[name] = (cursor, lba, size)
            cursor += length
        assert rows["WAD.WAD"][1:] == (37, 0x6927000)
        assert rows["SCUS_942.28"][1:] == (53875, 0x66000) and rows["PETEXA0.STR"][1] == 60000
        append = overlay + body
        final_exe = 53875 + len(append) // 2048
        end = final_exe + 204
        final_wad_size = 0x6927000 + len(append)
        assert final_exe == 54921 and end == 55125 and final_wad_size == 0x6B32000
        for name, lba, size in [("WAD.WAD", 37, final_wad_size), ("SCUS_942.28", final_exe, 0x66000)]:
            at = rows[name][0]
            for offset, number in [(2, lba), (10, size)]:
                struct.pack_into("<I", root, at + offset, number)
                struct.pack_into(">I", root, at + offset + 4, number)
        struct.pack_into("<IIII", wad, 79 * 8, 0x6927000, len(overlay), 0x6936800, len(body))
        forward, inverse = hashlib.sha256(), hashlib.sha256()
        changed = 0
        for lba in range(SECTORS):
            before = sector(lba)
            payload = None
            if lba == 22:
                payload = root
            elif lba == 37:
                payload = wad
            elif lba == 53874:
                payload = before[24:2072]
            elif 53875 <= lba < final_exe:
                at = (lba - 53875) * 2048
                payload = append[at:at + 2048]
            elif final_exe <= lba < end:
                at = (lba - final_exe) * 2048
                payload = exe[at:at + 2048]
            after = before if payload is None else native.form1(before, payload,
                0x81 if lba in (22, final_exe - 1, end - 1) else 0)
            forward.update(after)
            inverse.update(before)  # Expected original only; actual C# inverse still must be tested.
            changed += before != after
        assert changed == 1253 and inverse.hexdigest() == SOURCE
        source.seek(0)
        assert hashlib.file_digest(source, "sha256").hexdigest() == SOURCE
        assert source.read(1) == b"" and source.tell() == SIZE and source.seek(0, 2) == SIZE
    canonical = json.dumps(INTENT, separators=(",", ":"), ensure_ascii=True).encode("utf8")
    require_helpers()
    assert digest(canonical) == "b3f10d40db88cbfa3d3393e0d9c9e5e49e9a829f3fd311bcfa005082016e4cf1"
    assert digest(body) == "bae43882a39dfc1604f4e6d578a354f34f99107f97faa9330600b3e01f34b15b"
    assert digest(exe) == "2c1fd87e9fa8ce5c5f16be53e738405cec90c0bfc10ac9cb8cf1ce979b2e5c57"
    assert forward.hexdigest() == "ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16"
    print(json.dumps(dict(marker="EDITED_FRESH_DOCUMENT_AFTERIMAGE_OK", intent=INTENT,
        helper_sha256=HELPERS, legacy_full_image_controls=control["outputs"],
        original_form1_parity_reproduced=True, all46_music_controls=True,
        canonical_document_sha256=digest(canonical), body_sha256=digest(body), overlay_sha256=digest(overlay),
        executable_sha256=digest(exe), wad_directory_sha256=digest(wad), root_directory_sha256=digest(root),
        expected_output_sha256=forward.hexdigest(), changed_raw_sectors=changed,
        body_length=len(body), final_executable_lba=final_exe, final_wad_length=final_wad_size,
        source_before_after_sha256=SOURCE, inverse_expected_original_sha256=inverse.hexdigest(),
        native_compilation_executed=False, physical_output_written=False, runtime_verified=False,
        publication_approved=False), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: edited-document-afterimage-draft.py original.bin")
    main(sys.argv[1])
