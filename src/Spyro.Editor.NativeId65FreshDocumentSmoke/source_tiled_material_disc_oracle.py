"""Independent complete tiled-material body/disc afterimages and raw inverses.

Runs preserved original-parity, legacy and tiled controls first. Reconstructs
three new material afterimages without Core or output files. All subsequent
raw reads retain authenticated same-pass preimages. Not runtime acceptance.
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
ORIGINAL = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
# Independently generated in evidence352 before the new production adapters
# existed or executed. Freeze complete Body and raw-image references together.
REFERENCES = {
    "six-textured": ("461326c0e362a7c0bdb4c15c21996d15f6b1674e2156d60802f0efc94e6c36be",
                     "462c992f80f512f0b6be781b1a79e707e37a0e18f5e5e47356cd332ef61b2ba8"),
    "mixed-base": ("94c11a4de02529ccac39f09207f4c23d294a55652194966a8a4cb9499c73df49",
                   "af46a6a0861af60a92669128512a94b6fca115ab12da699e5c5e12e5354beb22"),
    "changed-third-tile": ("675a29ec5f0876feb32b03a47101ee62ab0c5b656b2e724107c8f18517321898",
                           "bb1c2263afe49a9a603c71981f88e1c750e982bc650156c81bb42ba550ec7578"),
}
HELPERS = {
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_data_oracle.py":
        "59558b225d3c723d02535d07fe57496008574c05ea8aad04cafd317d359b2d67",
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
        require(sha((ROOT / relative).read_bytes()) == expected, "Reviewed helper changed: " + relative)


def load(relative, name):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    previous = sys.argv
    sys.argv = [str(path)]
    try:
        spec.loader.exec_module(module)
    finally:
        sys.argv = previous
    return module


def main(path):
    require(__debug__, "Preserved helper assertions must execute")
    require_helpers()
    tiled = load(next(iter(HELPERS)), "material_tiled_disc_control")
    material = load(list(HELPERS)[1], "material_source_control")
    tiled.require_helpers()
    material.require_helpers()
    output = io.StringIO()
    with contextlib.redirect_stdout(output):
        tiled.main(path)
    tiled_control = json.loads(output.getvalue())
    require(tiled_control["marker"] == "TILED_DATA_INDEPENDENT_AFTERIMAGE_OK" and
            tiled_control["expected_image_sha256"] == "7ba0b29b68a1bee415901382c3a3b5345ccb01ca84872502131f68361afdc305" and
            tiled_control["independent_inverse_sha256"] == ORIGINAL and
            tiled_control["original_form1_parity_control"] and tiled_control["all46_music_controls"],
            "Preserved full tiled/legacy images, original parity, inverse and all music controls execute")
    output = io.StringIO()
    with contextlib.redirect_stdout(output):
        material.main(path)
    material_control = json.loads(output.getvalue())
    require(material_control["marker"] == "TILED_MATERIAL_EXACT_SOURCE_ORACLE_OK" and
            material_control["completeDataInverse"], "Independent material data/inverse controls execute")
    raw = tiled.load("src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py", "material_raw_sectors")
    geometry = tiled.load("src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py", "material_tiled_geometry")
    allocation = material.load("src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_oracle.py", "material_records")
    with contextlib.redirect_stdout(io.StringIO()):
        (baseline, overlay), *_ = raw.build_bodies(path)
        captured = allocation.capture(path)
    tail = captured.data[0x2BC:]
    before_data = struct.pack("<II", 8, 0) + geometry.fresh() + tail
    untextured = geometry.body(baseline, before_data, tail)
    require(sha(untextured) == tiled_control["body_sha256"] and len(untextured) == 0x1FB800 and len(overlay) == 0xF800,
            "Complete current tiled body and retained engine allocations")
    bodies = []
    for case in material_control["cases"]:
        data = material.compose(before_data, captured.records, case["orderedFaceIds"])
        require(sha(data) == material.DATA_REFERENCES[case["name"]], "Frozen material data fixture")
        body = bytearray(untextured)
        body[0x800:0xDE800] = captured.upload
        body[0xDE800:0xDF800] = data + bytes(0x1000 - len(data))
        require(len(body) == len(untextured) and body[:0x800] == untextured[:0x800] and
                body[0xDF800:] == untextured[0xDF800:] and body[0xDF3C8:0xDF800] == bytes(0x438),
                "All header/model/scene/auxiliary bytes and the complete zero data padding remain exact")
        bodies.append((case, bytes(body)))

    with open(path, "rb") as source:
        material.require_original(source)
        source.seek(0)
        index = bytearray(SECTORS * 32)
        original_hash = hashlib.sha256()
        for lba in range(SECTORS):
            sector = source.read(2352)
            require(len(sector) == 2352, "Complete original raw sector")
            original_hash.update(sector)
            index[32 * lba:32 * lba + 32] = hashlib.sha256(sector).digest()
        require(original_hash.hexdigest() == ORIGINAL and source.tell() == SIZE and source.read(1) == b"",
                "Same-pass full original source index and EOF")

        def sector(lba):
            require(0 <= lba < SECTORS, "Bounded source LBA")
            source.seek(lba * 2352)
            data = source.read(2352)
            require(len(data) == 2352 and hashlib.sha256(data).digest() == index[32 * lba:32 * lba + 32],
                    "Every subsequent original-sector read matches its authenticated same-pass preimage")
            return data

        root, wad = bytearray(sector(22)[24:2072]), bytearray(sector(37)[24:2072])
        original_exe = b"".join(sector(53875 + i)[24:2072] for i in range(204))
        require(sha(original_exe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998", "Complete original executable")
        executable = raw.compose_exe(original_exe, 19)
        require(sha(executable) == tiled_control["executable_sha256"], "Same actual document music19 executable")
        cursor, rows = 0, {}
        while cursor < 2048 and root[cursor]:
            size = root[cursor]
            require(size >= 34 and cursor + size <= 2048, "Bounded root directory record")
            row = root[cursor:cursor + size]
            name = row[33:33 + row[32]].decode("ascii").removesuffix(";1")
            extent, length = struct.unpack_from("<I", row, 2)[0], struct.unpack_from("<I", row, 10)[0]
            require(extent == int.from_bytes(row[6:10], "big") and length == int.from_bytes(row[14:18], "big") and
                    name not in rows, "Unique dual-endian directory ownership")
            rows[name] = (cursor, extent, length)
            cursor += size
        require(rows["WAD.WAD"][1:] == (37, 0x6927000) and rows["SCUS_942.28"][1:] == (53875, 0x66000) and
                rows["PETEXA0.STR"][1] == 60000 and wad[79 * 8:81 * 8] == bytes(16), "Exact source allocation preimages")
        final_exe, final_end, final_wad = 54921, 55125, 0x6B32000
        for name, extent, length in (("WAD.WAD", 37, final_wad), ("SCUS_942.28", final_exe, 0x66000)):
            at = rows[name][0]
            for offset, value in ((2, extent), (10, length)):
                struct.pack_into("<I", root, at + offset, value)
                struct.pack_into(">I", root, at + offset + 4, value)
        struct.pack_into("<IIII", wad, 79 * 8, 0x6927000, len(overlay), 0x6936800, len(untextured))
        require(sha(root) == tiled_control["root_sha256"] and sha(wad) == tiled_control["wad_header_sha256"],
                "Metadata is identical to the independently checked tiled physical layout")
        results = []
        for case, body in bodies:
            append = overlay + body
            require(len(append) == 0x20B000 and 53875 + len(append) // 2048 == final_exe and
                    0x6927000 + len(append) == final_wad, "Material growth consumes owned data padding, not extra disc space")
            forward, inverse = hashlib.sha256(), hashlib.sha256()
            patches = {}
            for lba in range(SECTORS):
                before, payload = sector(lba), None
                if lba == 22:
                    payload = root
                elif lba == 37:
                    payload = wad
                elif lba == 53874:
                    payload = before[24:2072]
                elif 53875 <= lba < final_exe:
                    at = (lba - 53875) * 2048
                    payload = append[at:at + 2048]
                elif final_exe <= lba < final_end:
                    at = (lba - final_exe) * 2048
                    payload = executable[at:at + 2048]
                after = before if payload is None else raw.form1(before, payload,
                    0x81 if lba in (22, final_exe - 1, final_end - 1) else 0)
                forward.update(after)
                if after != before:
                    patches[lba] = (before, after)
            require(len(patches) == 1253 and min(patches) == 22 and max(patches) == 55124, "Exact complete changed-sector set")

            def restore(lba, after):
                if lba in patches:
                    before, expected = patches[lba]
                    require(after == expected, "Material inverse afterimage mismatch")
                else:
                    before = after
                require(len(before) == 2352 and hashlib.sha256(before).digest() == index[32 * lba:32 * lba + 32],
                        "Material inverse preimage mismatch")
                return before

            for lba in range(SECTORS):
                before = sector(lba)
                after = patches[lba][1] if lba in patches else before
                restored = restore(lba, after)
                require(restored == before, "Every raw sector restores its complete original owner")
                inverse.update(restored)
            require(inverse.hexdigest() == ORIGINAL, "Complete independent raw inverse")
            for lba in (22, 37, 54079, 55124, 100000):
                after = bytearray(patches[lba][1] if lba in patches else sector(lba))
                after[-1] ^= 1
                try:
                    restore(lba, bytes(after))
                except AssertionError as error:
                    require("inverse" in str(error), "Exact inverse corruption guard")
                else:
                    raise AssertionError("Corrupt changed/unchanged raw sector accepted")
            require((sha(body), forward.hexdigest()) == REFERENCES[case["name"]], "Frozen independent whole Body/image references")
            results.append(dict(name=case["name"], ordered_face_ids=case["orderedFaceIds"],
                                data_sha256=case["dataSha256"], upload_sha256=sha(captured.upload),
                                body_sha256=sha(body), image_sha256=forward.hexdigest(),
                                body_length=len(body), used_data=0xBC8, changed_sectors=len(patches),
                                inverse_sha256=inverse.hexdigest()))
        material.require_original(source)
    with open(path, "rb") as final_named_original:
        material.require_original(final_named_original)
    require_helpers()
    tiled.require_helpers()
    material.require_helpers()
    print(json.dumps(dict(marker="TILED_MATERIAL_DISC_INDEPENDENT_ORACLE_OK", helper_sha256=HELPERS,
                          cases=results, original_sha256=ORIGINAL, original_form1_parity_control=True,
                          legacy_and_tiled_full_image_controls=True, all46_music_controls=True,
                          authenticated_same_pass_preimages=True, independent_raw_inverse_executed=True,
                          actual_csharp_inverse_executed=False, core_executed=False,
                          physical_output_written=False, runtime_verified=False, publication_approved=False), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_tiled_material_disc_oracle.py original.bin")
    main(sys.argv[1])
