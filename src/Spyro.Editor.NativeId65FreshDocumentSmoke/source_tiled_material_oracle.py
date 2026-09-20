"""Independent exact-source tiled material upload/data fixtures.

Executes the preserved two-source allocator oracle, then independently binds its
actual relocated records to six faces of the preserved three-tile surface. No
Core, source mutation, body/disc construction, output files or runtime approval.
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
ORIGINAL = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
SIZE = 661547040
# Independently captured from the preserved Python source/geometry oracles in
# evidence341, before the new Core source issuer existed or executed.
DATA_REFERENCES = {
    "six-textured": "ba244a6a9b496aa5507721d4817f32edeed337d72ac3700ed12021bbddd687aa",
    "mixed-base": "c0ac16f6e8e063dece5e9386649452f3d507693db656f996099350adb492bfa2",
    "changed-third-tile": "03d9dc3a28685abe159fd0efb650e40bcd47dd992e8eaff31a70d05f20b1f853",
}
HELPERS = {
    "src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_oracle.py": "c4ee51758031edb3d2b9cab9279985a537d0c06d1251cd24ab090dec67b07d6f",
    "src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_fresh_upload_fixture.py": "727ea1028d36db37c70e25a112836c4740b967a667aad64c78f6a900786b6cfe",
    "src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_golden.json": "c6c16f0bac8fd1816676404f50c99ab422551ddc60da6c2ca2555b0d149e779f",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py": "4aa6bc207bed327c8fb2f9832b8bd1bc098b26b0f50a4817ba55183373be0ec3",
    "src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py": "bcaa7349277bc5150cd17cf3e8cebbc689b68a58e9dee21a2709841c899aa592",
    "src/Spyro.Editor.NativeId65NormalResourceSmoke/source_resource_oracle.py": "280087ec3b80e005872b6ec2fe793c9bbbbd69bde70ea6c64de234afa37ad76d",
    "src/Spyro.Editor.NativeId65NormalResourceSmoke/source_fresh_scene_oracle.py": "d4946487dc45cb353d49d5fc18ab1894ad6b47849b8348b3e14857c7e9f8a8a6",
}


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def word(data, at):
    require(at >= 0 and at % 4 == 0 and at + 4 <= len(data), "Complete bounded word")
    return struct.unpack_from("<I", data, at)[0]


def require_helpers():
    for relative, expected in HELPERS.items():
        require(sha((ROOT / relative).read_bytes()) == expected, "Preserved helper changed: " + relative)


def require_original(source):
    require(source.seek(0, 2) == SIZE, "Complete source size")
    source.seek(0)
    require(hashlib.file_digest(source, "sha256").hexdigest() == ORIGINAL, "Complete source hash")
    require(source.tell() == SIZE and source.read(1) == b"" and source.seek(0, 2) == SIZE, "Complete source EOF")


def load(relative, name):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def compose(before, records, faces):
    require(len(before) == 0xA58 and before[:8] == struct.pack("<II", 8, 0), "Complete untextured input")
    require(len(records) == 2 and all(len(record) == 184 for record in records), "Actual two relocated records")
    require(len(faces) == 6 and all(face in (0, 1, 255) for face in faces), "Exact six-face binding")
    metadata = struct.pack("<II", 0x178, 2)
    metadata += b"".join(record[:16] for record in records) + b"".join(record[16:] for record in records)
    output = bytearray(metadata + before[8:])
    require(len(output) == 0xBC8, "Measured metadata growth remains inside the current1000 allocation")
    face_offsets = []
    environment = 8
    require(word(before, environment) == 500 and word(before, environment + 4) == 3, "Three complete sectors")
    for tile in range(3):
        sector = environment + 4 + word(before, environment + 8 + 4 * tile)
        require(sector == 28 + 160 * tile, "Actual ordered sector pointer")
        counts = before[sector + 16:sector + 24]
        require(counts == bytes((4, 4, 2, 0, 4, 4, 2, 12)), "Canonical native table counts/offset")
        face_start = sector + 28 + 4 * counts[7] + 4 * counts[4] + 8 * counts[5]
        for triangle in range(2):
            at = face_start + 16 * triangle + 8
            require(word(before, at) == 255 and word(before, at + 4) == 128, "Exact FF/80 material owner")
            struct.pack_into("<I", output, at + 0x170, faces[tile * 2 + triangle])
            face_offsets.append(at)
    require(face_offsets == [0xA0, 0xB0, 0x140, 0x150, 0x1E0, 0x1F0], "Six independently derived input face offsets")
    changed = {at + byte for at in face_offsets for byte in range(4)}
    require(all(before[at] == output[at + 0x170] for at in range(8, len(before)) if at not in changed),
            "Complete translated geometry/collision/visibility/sky/guard/particle/sound complement")
    require(all(word(output, at + 0x174) == 128 for at in face_offsets), "All direct-LQ flags retained")
    for slot, record in enumerate(records):
        require(output[8 + slot * 16:24 + slot * 16] + output[40 + slot * 168:208 + slot * 168] == record,
                "Every ordered complete native descriptor record retained")
    restored = bytearray(struct.pack("<II", 8, 0) + output[0x178:])
    for at in face_offsets:
        struct.pack_into("<I", restored, at, 255)
    require(restored == before, "Complete independent data inverse")
    return bytes(output)


def main(path):
    require(__debug__, "Existing helper assertions must be enabled")
    require_helpers()
    sys.path.insert(0, str(ROOT / "src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke"))
    allocation = load("src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_oracle.py", "tiled_source_allocation")
    geometry = load("src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py", "tiled_source_geometry")
    with open(path, "rb") as original:
        require_original(original)
        with contextlib.redirect_stdout(io.StringIO()):
            captured = allocation.capture(path)
        control = json.loads(captured.summary_json)
        require(control["marker"] == "NATIVE_FRESH_TERRAIN_TEXTURE_RAW_ORACLE_OK" and
                control["completeUploadComplement"] and control["completeTranslatedDataComplement"] and
                control["allocatedBytes"] == 28160 and control["allocations"] == 31 and control["containedAliases"] == 16,
                "Preserved full source allocator/complement controls execute")
        require(len(captured.data) == 0xA38 and sha(captured.upload) ==
                "2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333", "Old complete real-source byte controls")
        # The old source oracle proves this complete portal/particle/sound tail.
        # Its first0x178 bytes are metadata and next0x144 are legacy geometry.
        tail = captured.data[0x2BC:]
        before = struct.pack("<II", 8, 0) + geometry.fresh() + tail
        geometry.canonical(before, tail)
        cases = []
        for name, faces in (("six-textured", [0, 1, 0, 1, 0, 1]),
                            ("mixed-base", [0, 1, 255, 1, 0, 255]),
                            ("changed-third-tile", [0, 1, 0, 1, 1, 0])):
            output = compose(before, captured.records, faces)
            require(sha(output) == DATA_REFERENCES[name], "Frozen independent complete data reference: " + name)
            cases.append(dict(name=name, orderedFaceIds=faces, uploadSha256=sha(captured.upload),
                              dataSha256=sha(output), usedBytes=len(output),
                              additionalPacketBytesPerPass=12 * sum(face != 255 for face in faces)))
        require_original(original)
    with open(path, "rb") as final_named_original:
        require_original(final_named_original)
    require_helpers()
    print(json.dumps(dict(marker="TILED_MATERIAL_EXACT_SOURCE_ORACLE_OK", originalSha256=ORIGINAL,
                          helperSha256=HELPERS, beforeDataSha256=sha(before), tailSha256=sha(tail),
                          sourceHandles=control["sourceHandles"], encodedRecordSha256=control["encodedRecordSha256"],
                          allocationControl=control, cases=cases, completeDataInverse=True,
                          coreExecuted=False, filesWritten=False, bodyIssued=False, runtimeVerified=False,
                          publicationApproved=False), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_tiled_material_oracle.py original.bin")
    main(sys.argv[1])
