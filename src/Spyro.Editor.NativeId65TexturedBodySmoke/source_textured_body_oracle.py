"""Independent raw-source textured body/transfer oracle; no DLL or payload writes.

Uses the explicit immutable allocation-oracle return API, never frame locals or
an editor receipt. Preserves both old body goldens while composing the same
complete header/models/scenes/auxiliaries with the actual new texture bytes.
"""
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke"))
from source_texture_allocation_oracle import capture as capture_texture
from source_fresh_upload_fixture import capture as capture_fresh

ORIGINAL = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def compose(upload, data, model, scene, auxiliary):
    assert len(upload) == 0xDE000 and len(data) in (0x8C8, 0xA38)
    assert len(model) == 0xF800 and len(scene) == 0x3000 and len(auxiliary) == 4
    parts = [upload, data+bytes((-len(data))%0x800), model, scene, *auxiliary]
    header = bytearray(0x800); offset = len(header)
    for index, part in enumerate(parts):
        struct.pack_into("<II", header, 8*index, offset, len(part)); offset += len(part)
    assert struct.unpack_from("<I", header, 16)[0] == 0xDF800
    struct.pack_into("<II", header, 0x50, 0xDF800, 0xEC430)
    struct.pack_into("<H", header, 0x152, 0x78)
    result = bytes(header)+b"".join(parts)
    assert len(result) == offset == 0x1FB800
    assert sha(header) == "cd5658f83da4a953c5a3e66c848a8be606aabde9fea8b32d049c2bcf364c472d"
    return result


def main(path):
    texture = capture_texture(path); fresh = capture_fresh(path)
    assert isinstance(texture.upload, bytes) and isinstance(texture.data, bytes)
    assert isinstance(texture.records, tuple) and all(isinstance(r, bytes) for r in texture.records)
    assert json.loads(texture.summary_json)["uploadSha256"] == sha(texture.upload)
    old_data = bytearray(struct.pack("<II", 8, 0)+texture.data[0x178:])
    for offset in (0x98, 0xA8): struct.pack_into("<I", old_data, offset, 255)
    assert sha(old_data) == "821d25d4c012aa180d369f755d82a780dbcf244b4e53c6c4bcf349da533dce65"
    helper_path = ROOT / "src/Spyro.Editor.NativeId65NormalResourceSmoke/source_resource_oracle.py"
    assert sha(helper_path.read_bytes()) == "280087ec3b80e005872b6ec2fe793c9bbbbd69bde70ea6c64de234afa37ad76d"
    spec = importlib.util.spec_from_file_location("textured_body_model", helper_path)
    helper = importlib.util.module_from_spec(spec); spec.loader.exec_module(helper)
    with open(path, "rb") as image:
        def original():
            assert image.seek(0, 2) == 661547040; image.seek(0)
            assert hashlib.file_digest(image, "sha256").hexdigest() == ORIGINAL
            assert image.tell() == 661547040 and image.seek(0, 2) == 661547040

        def read(lba, offset, length):
            result = bytearray()
            while length:
                amount = min(length, 2048-offset%2048)
                image.seek((lba+offset//2048)*2352+24+offset%2048)
                part = image.read(amount); assert len(part) == amount
                result += part; offset += amount; length -= amount
            return bytes(result)

        original()
        town = read(37, 0x119E000, 0x2E2000); exe = read(53875, 0, 0x66000)
        overlay = read(37, 0x118E800, 0xF800)
        assert sha(town) == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"
        assert sha(exe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
        assert sha(overlay) == "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5"
        pin_path = ROOT / "src/Spyro.Editor.NativeId65NormalBodySmoke/NativeBodySourceWindows.cs"
        pins = re.findall(r'new\("([^"]+)", (0x[0-9A-F]+), "([0-9a-f]+)", "([0-9a-f]{64})"\)', pin_path.read_text())
        assert len(pins) == 16
        for name, address, encoded, digest in pins:
            data = bytes.fromhex(encoded); offset = int(address, 16)-0x8000F800
            assert sha(data) == digest and exe[offset:offset+len(data)] == data, name
        first, second = struct.unpack_from("<II", exe, 0x4B098)
        assert first == 0x3C028009 and second == 0x24429ECC
        copy_buffer = ((first&65535)<<16)+struct.unpack("<h", struct.pack("<H", second&65535))[0]
        assert copy_buffer == 0x80089ECC
        package = bytearray(town[0x1C9C7C:0x1CC5AC])
        fields = helper.texture_fields(package, (0x3F8, 0x1EA8, 0x23CC, 0x7B0, 0x2260, 0x2784))
        assert len(fields) == 36
        for field in fields:
            assert struct.unpack_from("<H", package, field+2)[0] == 0x7A36
            assert struct.unpack_from("<H", package, field+6)[0] == 0x1D
            for at in (0, 4, 8, 10): package[field+at] -= 64; package[field+at+1] -= 96
            struct.pack_into("<H", package, field+2, 33); struct.pack_into("<H", package, field+6, 8)
        assert package[4:6] == bytes([72, 73])
        model = town[0x173000:0x17FC30]+package+bytes(0x2A0)
        assert sha(model) == "88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d"
        auxiliary = []
        for i in range(4, 8):
            at, size = struct.unpack_from("<II", town, 8*i); auxiliary.append(town[at:at+size])
        assert list(map(len, auxiliary)) == [0x49800, 0x42800, 0x5D000, 0x20800]
        lists = b"".join(struct.pack("<II", size, 0)+bytes(size-8) for size in [12, 12, 8, 8, 12, 8, 8])
        outputs = []
        for x, heading, old_hash in ((8960, 64, "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c"),
                                      (8976, 128, "1314b299759ad0f66eed054db853c18e8db6dabda7c56a28cabd3fd74ea39643")):
            arena = 0x70*16+0x58
            scene = struct.pack("<iiiBBBB", x, 8960, 9216, 0, 0, heading, 0)+town[0x1D0010:0x1D0088]+lists
            scene += struct.pack("<II", 8+arena, 0)+bytes(arena)
            scene += struct.pack("<IIII", 4, 8, 0, 0x2004)+bytes(0x2000)+bytes(4)
            scene += bytes((-len(scene))%0x800)
            old_body = compose(fresh.upload, bytes(old_data), model, scene, auxiliary)
            new_body = compose(texture.upload, texture.data, model, scene, auxiliary)
            assert sha(old_body) == old_hash and old_body[:0x800] == new_body[:0x800]
            # Complete inherited suffix: model, scene, four full auxiliaries.
            assert old_body[0xDF800:] == new_body[0xDF800:]
            assert new_body[0xDE800+len(texture.data):0xDF800] == bytes(0x1000-len(texture.data))
            outputs.append(dict(x=x, heading=heading, bytes=len(new_body), old_body_sha256=sha(old_body),
                body_sha256=sha(new_body), scene_sha256=sha(scene), header_sha256=sha(new_body[:0x800]),
                model_runtime=hex(copy_buffer+len(texture.data)),
                scene_runtime=hex(copy_buffer+len(texture.data)+len(model)),
                known_scene_end=hex(copy_buffer+len(texture.data)+len(model)+len(scene))))
        old_transfer = fresh.upload[0x80000:]+fresh.upload[0x5E000:0x7F000]
        new_transfer = texture.upload[0x80000:]+texture.upload[0x5E000:0x7F000]
        assert old_transfer == new_transfer and len(new_transfer) == 0x7F000
        assert sha(new_transfer) == "626ae349b98cb4bea955ac53020586f645f872225085a67bc809f46f4ab0fb07"
        assert fresh.upload != texture.upload
        original()
    result = dict(marker="NATIVE_ID65_TEXTURED_BODY_RAW_ORACLE_OK", original_sha256=ORIGINAL,
        source_pins=len(pins), old_upload_sha256=sha(fresh.upload), upload_sha256=sha(texture.upload),
        data_sha256=sha(texture.data), data_used=0xA38, data_sectors=0x1000, used_growth=0x170,
        old_padding=0x738, new_padding=0x5C8, transfer_sha256=sha(new_transfer), transfer_bytes_equal=True,
        paired_upload_bytes_equal=False, outputs=outputs, complete_model_scene_aux_complement=True,
        overlay_sha256=sha(overlay), runtime_verified=False, publication_approved=False)
    golden = Path(__file__).with_name("source_textured_body_golden.json")
    assert json.loads(golden.read_text()) == result
    print(json.dumps(result, indent=2))
    return result


if __name__ == "__main__":
    if len(sys.argv) != 2: raise SystemExit("Usage: source_textured_body_oracle.py original.bin")
    main(sys.argv[1])
