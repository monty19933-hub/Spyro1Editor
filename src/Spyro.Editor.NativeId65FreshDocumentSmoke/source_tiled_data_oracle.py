"""Independent three-tile complete raw afterimage and guarded preimage inverse.

No Core, DLL, source mutation, candidate output or native execution. The inverse
is test-side raw-preimage algebra, NOT execution of a production C# inverse.
All later reads authenticate their same-pass original-sector digest. Preserved
disc controls actually execute before the new tiled afterimage is reconstructed.
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
SIZE, SECTORS = 661547040, 281270
SOURCE_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
HELPERS = {
    "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py": "f93780c582df180f378a4121a824d01dec6f13e3c5da68a61af571772e732608",
    "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py": "e69619f391814c270c9c28bc588f432f79f8a6cd3f61301e3a1c483a8645ad07",
    "src/Spyro.Editor.NativeId65NormalResourceSmoke/source_resource_oracle.py": "280087ec3b80e005872b6ec2fe793c9bbbbd69bde70ea6c64de234afa37ad76d",
    "src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py": "bcaa7349277bc5150cd17cf3e8cebbc689b68a58e9dee21a2709841c899aa592",
    "src/Spyro.Editor.NativeId65NormalBodySmoke/NativeBodySourceWindows.cs": "674ac959cd1b451fc11c3945c1446059a2d09b505e03ddda27e6fbe3851594ec",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py": "4aa6bc207bed327c8fb2f9832b8bd1bc098b26b0f50a4817ba55183373be0ec3",
}


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require_helpers():
    for path, expected in HELPERS.items():
        require(sha((ROOT / path).read_bytes()) == expected, "Reviewed helper changed: " + path)


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


def main(path):
    require(__debug__, "Pinned helper assertions must be enabled")
    require_helpers()
    sys.path.insert(0, str(ROOT / "src/Spyro.Editor.NativeId65FreshDiscSmoke"))
    native = load("src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py", "tiled_disc")
    tiled = load("src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py", "tiled_surface")
    controls = io.StringIO()
    with contextlib.redirect_stdout(controls):
        native.main(path)
        (baseline, overlay), *_ = native.build_bodies(path)
    text = controls.getvalue()
    control, _ = json.JSONDecoder().raw_decode(text[text.index('{\n  "marker": "FRESH_ID65_DISC_RAW_ORACLE_OK"'):])
    require(control["original_form1_parity_reproduced"] and
            [row["output_sha256"] for row in control["outputs"]] == [
                "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188",
                "40047bd8e40f23b522bfaa04dd72353013fbcfddbbd9ba6b1e357b87e8664343",
                "e6d5f2abd512814bac54f48b2d3bb3255d09dc4d5abfb6aef87ddf82ea43fc08"],
            "Preserved original-parity and three complete legacy afterimage controls")
    require(len(control["executable_track_sha256"]) == 46, "All selectable music controls executed")
    tail = baseline[0xDE94C:0xDF0C8]
    data = struct.pack("<II", 8, 0) + tiled.fresh() + tail
    body = tiled.body(baseline, data, tail)
    append = overlay + body
    require(len(overlay) == 0xF800 and len(append) == 0x20B000, "Exact original physical append allocation")
    final_exe_lba, final_end, final_wad_size = 54921, 55125, 0x6B32000
    require(53875 + len(append) // 2048 == final_exe_lba and 0x6927000 + len(append) == final_wad_size,
            "Tiled used end does not silently change physical CD allocation")

    with open(path, "rb") as source:
        require(source.seek(0, 2) == SIZE, "Complete original source length")
        source.seek(0)
        index, original_hash = bytearray(SECTORS * 32), hashlib.sha256()
        for lba in range(SECTORS):
            raw = source.read(2352)
            require(len(raw) == 2352, "Complete original raw sector")
            original_hash.update(raw)
            index[32 * lba:32 * lba + 32] = hashlib.sha256(raw).digest()
        require(original_hash.hexdigest() == SOURCE_SHA and source.read(1) == b"" and source.tell() == SIZE and
                source.seek(0, 2) == SIZE, "Exact original hash and EOF before physical reconstruction")

        def sector(lba):
            require(0 <= lba < SECTORS, "Bounded original LBA")
            source.seek(lba * 2352)
            raw = source.read(2352)
            require(len(raw) == 2352 and hashlib.sha256(raw).digest() == index[32 * lba:32 * lba + 32],
                    "Every later raw read retains its authenticated same-pass original owner")
            return raw

        root, wad = bytearray(sector(22)[24:2072]), bytearray(sector(37)[24:2072])
        original_exe = b"".join(sector(53875 + i)[24:2072] for i in range(204))
        require(sha(original_exe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998",
                "Complete original executable owner")
        executable = native.compose_exe(original_exe, 19)
        require(sha(executable) == control["executable_track_sha256"]["19"], "Tiled fixture preserves actual music19 executable control")
        cursor, rows = 0, {}
        while cursor < 2048 and root[cursor]:
            size = root[cursor]
            require(size >= 34 and cursor + size <= 2048, "Bounded root directory record")
            row = root[cursor:cursor + size]
            name = row[33:33 + row[32]].decode("ascii").removesuffix(";1")
            extent, length = struct.unpack_from("<I", row, 2)[0], struct.unpack_from("<I", row, 10)[0]
            require(extent == int.from_bytes(row[6:10], "big") and length == int.from_bytes(row[14:18], "big") and
                    name not in rows, "Exact dual-endian unique root entry")
            rows[name] = (cursor, extent, length)
            cursor += size
        require(rows["WAD.WAD"][1:] == (37, 0x6927000) and rows["SCUS_942.28"][1:] == (53875, 0x66000) and
                rows["PETEXA0.STR"][1] == 60000 and wad[79 * 8:81 * 8] == bytes(16), "Original physical descriptor ownership")
        for name, extent, length in (("WAD.WAD", 37, final_wad_size), ("SCUS_942.28", final_exe_lba, 0x66000)):
            at = rows[name][0]
            for offset, value in ((2, extent), (10, length)):
                struct.pack_into("<I", root, at + offset, value)
                struct.pack_into(">I", root, at + offset + 4, value)
        struct.pack_into("<IIII", wad, 79 * 8, 0x6927000, len(overlay), 0x6936800, len(body))
        forward_hash, patches = hashlib.sha256(), {}
        for lba in range(SECTORS):
            before, payload = sector(lba), None
            if lba == 22:
                payload = root
            elif lba == 37:
                payload = wad
            elif lba == 53874:
                payload = before[24:2072]
            elif 53875 <= lba < final_exe_lba:
                at = (lba - 53875) * 2048
                payload = append[at:at + 2048]
            elif final_exe_lba <= lba < final_end:
                at = (lba - final_exe_lba) * 2048
                payload = executable[at:at + 2048]
            after = before if payload is None else native.form1(before, payload,
                0x81 if lba in (22, final_exe_lba - 1, final_end - 1) else 0)
            forward_hash.update(after)
            if after != before:
                patches[lba] = (before, after)
        require(len(patches) == 1253 and min(patches) == 22 and max(patches) == 55124,
                "Complete tiled afterimage changes only its exact bounded raw-sector set")

        def inverse_sector(lba, after):
            # Retain whole authenticated raw preimages, including original
            # Form2 gap headers/EDC, not just reconstructed logical payloads.
            if lba in patches:
                before, expected_after = patches[lba]
                require(after == expected_after, "Tiled inverse afterimage mismatch")
            else:
                before = after
            require(len(before) == 2352 and hashlib.sha256(before).digest() == index[32 * lba:32 * lba + 32],
                    "Tiled inverse original preimage mismatch")
            return before

        inverse_hash = hashlib.sha256()
        for lba in range(SECTORS):
            original = sector(lba)
            after = patches[lba][1] if lba in patches else original
            restored = inverse_sector(lba, after)
            require(restored == original, "Each complete raw afterimage restores the exact authenticated original")
            inverse_hash.update(restored)
        require(inverse_hash.hexdigest() == SOURCE_SHA, "Complete independent inverse restores original digest")
        for lba in (22, 37, 54079, 55124):
            corrupt = bytearray(patches[lba][1]); corrupt[-1] ^= 1
            try:
                inverse_sector(lba, bytes(corrupt))
            except AssertionError as error:
                require(str(error) == "Tiled inverse afterimage mismatch", "Exact inverse corruption refusal")
            else:
                raise AssertionError("Tiled inverse accepted changed afterimage")
        source.seek(0)
        require(hashlib.file_digest(source, "sha256").hexdigest() == SOURCE_SHA and source.read(1) == b"" and
                source.tell() == SIZE and source.seek(0, 2) == SIZE, "Whole original hash/length/EOF unchanged after work")
    require_helpers()
    print(json.dumps(dict(marker="TILED_DATA_INDEPENDENT_AFTERIMAGE_OK", helper_sha256=HELPERS,
        tiles=[list(p) + [2048, 2048] for p in tiled.TILES], spawn=[8960, 8960, 9216, 64], music_track=19,
        surface_sha256=tiled.SURFACE_SHA, data_sha256=sha(data), data_used=len(data), body_sha256=sha(body),
        expected_image_sha256=forward_hash.hexdigest(), executable_sha256=sha(executable),
        root_sha256=sha(root), wad_header_sha256=sha(wad), changed_sectors=len(patches),
        first_changed_sector=min(patches), last_changed_sector=max(patches), final_executable_lba=final_exe_lba,
        final_wad_size=final_wad_size, original_before_after_sha256=SOURCE_SHA,
        independent_inverse_sha256=inverse_hash.hexdigest(), independent_raw_preimage_inverse_executed=True,
        actual_csharp_inverse_executed=False, legacy_full_image_controls=control["outputs"],
        original_form1_parity_control=True, all46_music_controls=True, private_same_pass_sector_index=True,
        native_compilation_executed=False, physical_output_written=False, runtime_verified=False,
        publication_approved=False), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_tiled_data_oracle.py original.bin")
    main(sys.argv[1])
