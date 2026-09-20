#!/usr/bin/env python3
"""Source-only fresh Wizard resource installation, never a body or candidate.

Exact original, immutable independently reconstructed upstreams; no C# imports,
DLL/ROM writes, native callback/audio hardware/scene/gameplay execution. The
native interpreter executes only the exact parser/model-loader instruction pins.
"""
import argparse
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys
from typing import NamedTuple

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPERS = {
    "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_wizard_family_asset_oracle.py": "e92cfc1a4c53611cfe11ccc87931dec71ce6b4c254c34a9d6a74bf56caef2a43",
    "src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_fresh_upload_fixture.py": "727ea1028d36db37c70e25a112836c4740b967a667aad64c78f6a900786b6cfe",
    "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py": "e69619f391814c270c9c28bc588f432f79f8a6cd3f61301e3a1c483a8645ad07",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py": "75f6762822c79520176bb3127e9b426822fb7b007b2ba6da931e4822cb43f283",
    "src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_owner_lifecycle_oracle.py": "4374150efed3d518ae5a6ae0aeae6b8fe05e03b940961049a480759c9f958429",
    "src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py": "bcaa7349277bc5150cd17cf3e8cebbc689b68a58e9dee21a2709841c899aa592",
    "src/Spyro.Editor.NativeId65NormalResourceSmoke/source_resource_oracle.py": "280087ec3b80e005872b6ec2fe793c9bbbbd69bde70ea6c64de234afa37ad76d",
    "src/Spyro.Editor.NativeId65NormalResourceSmoke/source_fresh_scene_oracle.py": "d4946487dc45cb353d49d5fc18ab1894ad6b47849b8348b3e14857c7e9f8a8a6",
    "src/Spyro.Editor.NativeId65NormalBodySmoke/NativeBodySourceWindows.cs": "674ac959cd1b451fc11c3945c1446059a2d09b505e03ddda27e6fbe3851594ec",
}
ASSET_SHA = "af13490b7dbb22fc32ab070e9662ca85faf112c9068816f750ac9b8627a2a147"
BASE_BODY_SHA = "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c"
# Derived from the first complete source/native run03, before any target Core
# installer exists. These are independent byte goldens, not capture authority.
RESOURCE_GOLDENS = {
    "upload": "d74c2571ed857865b2b989af3001117f44e0915f9c6d59927e30a49d90a79402",
    "vram": "c44226f0b677da153c2da4661831471393a7a11ed69d28c6bb08dbad5212a1d5",
    "bank": "f922913cf797ee91f0e0ccb909e511a2746c9f28a5df9d8ca2b54934979b968c",
    "data": "bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4",
    "data_cd": "1536654261475fcab6b0c0e3e4c4997f899a045ce92eb3efc36a5fc94624cffe",
    "model": "782c4500b3152f6051b45b8a0a56f6c99c4f703c753948f656318ccfa8618cda",
}


def sha(data): return hashlib.sha256(data).hexdigest()
def require(ok, label):
    if not ok: raise AssertionError(label)
def word(data, at=0):
    require(at >= 0 and at % 4 == 0 and at + 4 <= len(data), "Bounded aligned word")
    return struct.unpack_from("<I", data, at)[0]
def half(data, at): return struct.unpack_from("<H", data, at)[0]
def put(data, at, value): struct.pack_into("<I", data, at, value)
def round_cd(length): return (length + 0x7FF) & ~0x7FF


def require_helpers():
    for relative, expected in HELPERS.items():
        require(sha((ROOT / relative).read_bytes()) == expected, "Frozen helper changed: " + relative)


def load(relative, name):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    before = sys.argv
    try:
        sys.argv = [str(path)]
        with contextlib.redirect_stdout(io.StringIO()): spec.loader.exec_module(module)
    finally: sys.argv = before
    return module


class Assets(NamedTuple):
    owners: tuple[tuple[str, bytes], ...]
    metadata_json: str
    donor: bytes
    town: bytes
    exe: bytes
    pete: bytes


def capture_assets(path, image, raw):
    # The old CLI remains unchanged. Its current metadata is cross-checked
    # against complete raw source bytes, never used as a caller certificate.
    text = io.StringIO()
    with contextlib.redirect_stdout(text): raw.main(path)
    metadata, end = json.JSONDecoder().raw_decode(text.getvalue())
    require(text.getvalue()[end:].strip() == "NATIVE_WIZARD_FAMILY_ASSET_ORACLE_OK sourceUnchanged=true targetOutputsWritten=0",
            "Unchanged detached oracle marker")
    require(metadata["checks"] == 58 and metadata["refusals"] == 3 and metadata["blockSha256"] == ASSET_SHA,
            "Unchanged detached oracle control")
    sources = {n: raw.logical(image, offset, length) for n, (offset, length, _) in raw.SOURCE.items()}
    for n, (_, length, expected) in raw.SOURCE.items():
        require(len(sources[n]) == length and sha(sources[n]) == expected, "Complete source " + n)
    donor, town = sources["donor"], sources["town"]
    roots = raw.roots(donor)
    animations = raw.animations(donor, roots[0])
    particle, _, _, sounds = raw.sound(donor)
    by_id = {
        "wizard.model": raw.extent(donor, *roots[0x11B]), "bolt.model": raw.extent(donor, *roots[0x26]),
        "player.animation8": animations[8][1], "wizard.row": raw.extent(donor, 0x1D4B88, 0x58),
        "wizard.private": raw.extent(donor, 0x1DAB50, 0x50), "particle7": raw.extent(donor, particle + 20, 12),
    }
    for tile in metadata["textures"]:
        at, width, rows = tile["pixelOffset"], tile["rowBytes"], tile["rows"]
        pixels = b"".join(raw.extent(donor, 0x800 + at + row * 1024, width, 1) for row in range(rows))
        palette = raw.extent(donor, 0x800 + tile["paletteOffset"], 32)
        require(sha(pixels) == tile["pixelsSha256"] and sha(palette) == tile["paletteSha256"], "Immutable strided source tile")
        by_id[tile["id"] + ".pixels"], by_id[tile["id"] + ".clut"] = pixels, palette
    for entry in metadata["sounds"]:
        _, descriptor, _, sample = sounds[entry["descriptor"]]
        by_id[entry["id"] + ".descriptor"], by_id[entry["id"] + ".sample"] = descriptor, sample
    ordered, cursor = [], 0
    for owner in metadata["owners"]:
        data = by_id[owner["id"]]
        require(owner["offset"] == cursor and len(data) == owner["length"] and sha(data) == owner["sha256"], "Complete detached owner partition")
        ordered.append((owner["id"], data)); cursor += len(data)
    require(len(ordered) == 26 and cursor == 0x13274 and sha(b"".join(b for _, b in ordered)) == ASSET_SHA,
            "Reconstructed immutable asset block")
    return Assets(tuple(ordered), json.dumps(metadata, sort_keys=True), donor, town, sources["exe"], sources["pete"])


def require_rectangle(x, y, width, rows, kind):
    require(all(isinstance(n, int) for n in (x, y, width, rows)) and width > 0 and rows > 0 and
            0 <= x <= 1024 - width and 0 <= y <= 512 - rows, "Whole texture extent bounds")
    if kind == "palette": require(width == 32 and rows == 1 and x % 32 == 0, "CLUT encoding/alignment")
    else: require(kind == "pixel" and width <= 128 and x % 128 + width <= 128 and y % 256 + rows <= 256,
                  "Native 4bpp page boundary")


def indices(x, y, width, rows):
    return tuple((y + row) * 1024 + x + col for row in range(rows) for col in range(width))


def require_background(ownership, x, y, width, rows, kind):
    require(len(ownership) == 0x80000, "Complete immutable occupancy owner")
    require_rectangle(x, y, width, rows, kind)
    require(all(ownership[i] == 0 for i in indices(x, y, width, rows)), "Protected or previously allocated texture owner")


def rebase_uv(u, v, source_u, source_v, x, y):
    u, v = u - source_u + (x % 128) * 2, v - source_v + y % 256
    require(0 <= u <= 255 and 0 <= v <= 255, "Reencoded UV width overflow")
    return bytes([u, v])


def equivalent(left_descriptor, left_sample, right_descriptor, right_sample):
    require(len(left_descriptor) == len(right_descriptor) == 20, "Complete sound descriptor")
    return left_descriptor[4:] == right_descriptor[4:] and left_sample == right_sample


def require_sample(sample, offset):
    require(offset >= 0 and offset % 16 == 0 and len(sample) > 0 and len(sample) % 16 == 0, "Aligned complete ADPCM sample")
    for at in range(0, len(sample), 16):
        require(sample[at] >> 4 <= 4 and sample[at] & 15 <= 12 and sample[at + 1] == (1 if at == len(sample) - 16 else 0),
                "Exact simple one-shot ADPCM termination")


def data_parts(data):
    require(len(data) > 8 and len(data) <= 0x1000 and len(data) % 4 == 0 and data[:8] == struct.pack("<II", 8, 0),
            "Exact untextured data profile/alignment/bound")
    cursor, positions = 8, []
    for _ in range(5):
        positions.append(cursor); length = word(data, cursor)
        require(length >= 4 and length % 4 == 0 and cursor + length <= len(data), "Component alignment/bound")
        cursor += length
    require(word(data, cursor) == 0, "Exact zero portal count")
    cursor += 4; particle_at = cursor; length, count = word(data, cursor), word(data, cursor + 4)
    require(count <= 64 and length == 8 + 12 * count and cursor + length <= len(data), "Complete particle component")
    ids = []
    for at in range(cursor + 8, cursor + length, 12):
        id_, size = half(data, at), half(data, at + 2)
        require(id_ < 64 and id_ not in ids and size == 8, "Unique signed-safe particle descriptor ID/owner")
        ids.append(id_)
    cursor += length; sound_at = cursor; length = word(data, cursor)
    require(length >= 0x108 and cursor + length == len(data), "Complete sound component/data endpoint")
    count = word(data, cursor + 0x104)
    require(count <= 256 and length == 0x108 + 20 * count, "Complete sound descriptor count")
    require(all(v < count for v in data[cursor + 4:cursor + 0x104]), "Global sound selector descriptor bound")
    for at in range(cursor + 0x108, len(data), 20):
        require(word(data, at) % 16 == 0, "Sound sample offset alignment")
    return positions, particle_at, sound_at, tuple(ids)


def require_roots(model, roots):
    require(2 <= len(roots) <= 64 and roots[0] == (0, 0) and
            len({cls for cls, _ in roots}) == len(roots), "Unique root0/generic registration")
    for i, (cls, offset) in enumerate(roots):
        end = roots[i + 1][1] if i + 1 < len(roots) else len(model)
        require(0 <= cls < 512 and offset % 4 == 0 and 0 <= offset < end <= len(model), "Complete model root interval")
    p = 0; ids = []
    while p + 4 <= roots[1][1] and word(model, p) != 0xFFFFFFFF:
        id_, length = word(model, p), word(model, p + 4)
        require(id_ < 46 and id_ not in ids and length >= 0x28 and length % 4 == 0 and p + 8 + length < roots[1][1],
                "Complete local animation stream/terminator")
        ids.append(id_); p += 8 + length
    require(p + 4 == roots[1][1] and word(model, p) == 0xFFFFFFFF, "Complete local animation stream/terminator")
    return tuple(ids)


def install_textures(fresh, assets, check):
    metadata = json.loads(assets.metadata_json); source = dict(assets.owners)
    # Every bound/source owner is validated before making an output clone.
    require(len(fresh.upload) == 0xDE000 and len(fresh.gpu_ownership) == 0x80000, "Complete fresh upload owners")
    end = 0
    for owner in fresh.owners:
        require(owner.offset == end and owner.length > 0 and sha(fresh.upload[end:end + owner.length]) == owner.before_sha256,
                "Exact exhaustive fresh owner partition")
        end += owner.length
    require(end == len(fresh.upload), "No unowned upload suffix")
    for tile in metadata["textures"]:
        require(len(source[tile["id"] + ".pixels"]) == tile["rowBytes"] * tile["rows"] and
                len(source[tile["id"] + ".clut"]) == 32, "Complete detached indexed pair")
        x, y = tile["pixelOffset"] % 1024, tile["pixelOffset"] // 1024
        require_rectangle(x, y, tile["rowBytes"], tile["rows"], "pixel")
        require_rectangle(tile["paletteOffset"] % 1024, tile["paletteOffset"] // 1024, 32, 1, "palette")
    output, occupied = bytearray(fresh.upload[:0x80000]), bytearray(fresh.gpu_ownership)
    allocated = bytearray(0x80000); placements, owners = {}, []
    # No alias optimization: five independently source-owned groups. Stable
    # ID order, palettes first; pixels scan16-byte starts within native pages.
    for kind in ("palette", "pixel"):
        for tile in sorted(metadata["textures"], key=lambda t: t["id"]):
            width, rows = (32, 1) if kind == "palette" else (tile["rowBytes"], tile["rows"])
            point = None
            for y in range(513 - rows):
                if kind == "pixel" and y % 256 + rows > 256: continue
                for x in range(0, 1025 - width, 32 if kind == "palette" else 16):
                    if kind == "pixel" and x % 128 + width > 128: continue
                    if all(not any(occupied[(y + r) * 1024 + x:(y + r) * 1024 + x + width]) for r in range(rows)):
                        point = (x, y); break
                if point is not None: break
            require(point is not None, "Bounded background allocation exhausted")
            x, y = point; require_background(occupied, x, y, width, rows, kind)
            payload = source[tile["id"] + (".clut" if kind == "palette" else ".pixels")]
            locations = indices(x, y, width, rows)
            for i, value in zip(locations, payload):
                require(allocated[i] == 0, "Partial allocation alias")
                output[i], occupied[i], allocated[i] = value, 3, 1
            placements[(tile["id"], kind)] = point
            owners.append(dict(id=tile["id"], kind=kind, x=x, y=y, row_bytes=width, rows=rows, sha256=sha(payload)))
    rewritten = {id_: bytearray(source[id_]) for id_ in ("wizard.model", "bolt.model", "particle7")}
    patches, field_sets = [], {id_: set() for id_ in rewritten}
    tiles = {tile["id"]: tile for tile in metadata["textures"]}
    for reference in metadata["textureReferences"]:
        id_, at, length, tile_id = reference["owner"], reference["offset"], reference["length"], reference["tile"]
        original = source[id_][at:at + length]
        require(len(original) == length and sha(original) == reference["sha256"], "Complete source texture field")
        tile = tiles[tile_id]; page = half(original, 6)
        source_u = 2 * (tile["pixelOffset"] % 1024 - ((page & 15) * 128 - 1024))
        source_v = tile["pixelOffset"] // 1024 - (256 if page & 16 else 0)
        x, y = placements[(tile_id, "pixel")]; cx, cy = placements[(tile_id, "palette")]
        target = bytearray(original)
        for coordinate in (0, 4) if length == 8 else (0, 4, 8, 10):
            target[coordinate:coordinate + 2] = rebase_uv(original[coordinate], original[coordinate + 1], source_u, source_v, x, y)
        struct.pack_into("<H", target, 2, cy * 64 + (cx + 1024) // 32)
        struct.pack_into("<H", target, 6, (page & ~31) | (8 + x // 128) | (16 if y >= 256 else 0))
        require(not field_sets[id_].intersection(range(at, at + length)), "Overlapping model texture field ownership")
        rewritten[id_][at:at + length] = target; field_sets[id_].update(range(at, at + length))
        # Decode each actual consumer's final four/two UVs independently of its
        # allocation rectangle, preserving material flags and exact texel reads.
        check(half(target, 6) & ~31 == page & ~31, "Native material bits retained")
        for coordinate in (0, 4) if length == 8 else (0, 4, 8, 10):
            old_u, old_v = original[coordinate:coordinate + 2]; new_u, new_v = target[coordinate:coordinate + 2]
            old_at = ((page & 16 != 0) * 256 + old_v) * 1024 + (page & 15) * 128 - 1024 + old_u // 2
            new_page = half(target, 6)
            new_at = ((new_page & 16 != 0) * 256 + new_v) * 1024 + (new_page & 15) * 128 - 1024 + new_u // 2
            check(assets.donor[0x800 + old_at] == output[new_at] and old_u % 2 == new_u % 2, "Every final UV endpoint preserves indexed byte/nibble")
        palette_at = (half(target, 2) >> 6) * 1024 + (half(target, 2) & 63) * 32 - 1024
        check(output[palette_at:palette_at + 32] == source[tile_id + ".clut"], "Every final palette descriptor resolves complete owner")
        patches.append(dict(owner=id_, offset=at, before=original.hex(), after=target.hex()))
    for tile in metadata["textures"]:
        for kind, suffix, width, rows in (("pixel", ".pixels", tile["rowBytes"], tile["rows"]), ("palette", ".clut", 32, 1)):
            x, y = placements[(tile["id"], kind)]
            check(bytes(output[i] for i in indices(x, y, width, rows)) == source[tile["id"] + suffix], "Complete target strided pair readback")
    for id_, bytes_ in rewritten.items():
        check(all(a == b for i, (a, b) in enumerate(zip(source[id_], bytes_)) if i not in field_sets[id_]), "Complete model/particle nonfield complement")
    check(allocated.count(1) == 2216 and all(a == b for i, (a, b) in enumerate(zip(fresh.upload, output)) if not allocated[i]),
          "Full2216-byte allocation and protected/unallocated complement")
    return bytes(output), {n: bytes(b) for n, b in rewritten.items()}, owners, patches, bytes(allocated)


def native_code(native, assets):
    code = {}
    for name, start, length, expected in native.SPANS:
        raw = assets.exe[start - 0x8000F800:start - 0x8000F800 + length]
        require(sha(raw) == expected, "Exact native instruction source " + name)
        code.update((start + i, word(raw, i)) for i in range(0, length, 4))
    return code


def native_parser(native, code, data):
    m = native.globals_machine(code); m.blob(native.COPY, data)
    m.poke(0x800785DC, native.COPY); m.poke(0x800785E0, 0xA5A5A5A5)
    m.r[16] = 0x12345678
    m.run(0x800158E0, {0x80015904})
    return m


def native_readback(native, assets, data, model, roots, scene_length, check):
    code = native_code(native, assets)
    # Exact resident PETE package relocation, including real JR delay effects.
    resident = native.globals_machine(code); pete = assets.pete[:0x32E1C]
    resident.blob(native.PETE, pete); resident.r[4] = native.PETE
    resident.run(0x800133E0, {native.RETURN})
    relocated = resident.bytes(native.PETE, len(pete))
    m = native_parser(native, code, data)
    model_at = m.read(0x800785E0)
    check(model_at == native.COPY + len(data) and m.r[2] == model_at and m.r[16] == 0x12345678 and m.r[29] == native.STACK,
          "Actual native parser returned/stored endpoint and restored ABI; no expected model-global seed")
    check(m.calls == [(0x800158E8, 0x80012D58), (0x80013208, 0x80012CF0)], "Real parser and sound helper, zero helper stubs")
    positions, particle_at, sound_at, ids = data_parts(data)
    collision = native.COPY + positions[3] + 4
    check(all(m.read(collision + field) == collision + word(data, positions[3] + 4 + field) for field in (8, 12, 16, 20, 24)),
          "All actual collision pointer fields derived from complete original terrain")
    for i, id_ in enumerate(ids):
        # 131C4 loads ID from a2; 131D4 stores a2 itself. The consumer
        # table owns record header+payload, not a pre-advanced frame pointer.
        check(m.read(0x80076278 + id_ * 4) == native.COPY + particle_at + 8 + i * 12, "Actual native complete-record descriptor registration")
    check(all(m.read(0x80076278 + i * 4) == 0 for i in range(64) if i not in ids), "All unselected native descriptor slots remain untouched")
    check(m.read(0x80076378) == 0, "Particle descriptors end before model root0 table")
    count = word(data, sound_at + 0x104)
    check(m.read(0x800761D4) == native.COPY + sound_at + 4 and m.read(0x800761D0) == native.COPY + sound_at + 0x108 and
          all(m.read(native.COPY + sound_at + 0x108 + 20 * i) == word(data, sound_at + 0x108 + 20 * i) + 0x1010 for i in range(count)),
          "All native sound sample offsets and map/descriptor globals rebased")
    loaded_data = m.bytes(native.COPY, len(data))
    m.blob(model_at, model); m.blob(native.PETE, relocated)
    m.poke(0x80076378, native.PETE); m.poke(0x800785FC, 0x801C5BB8)
    # Synthetic scalar registration header ONLY: origin4000 is not a disk/body
    # layout or a proposed final copy anchor. No header bytes are emitted.
    fixture_header = bytearray(0x1D0); origin = 0x4000
    put(fixture_header, 0x10, origin); put(fixture_header, 0x14, len(model))
    put(fixture_header, 0x18, origin + len(model)); put(fixture_header, 0x1C, scene_length)
    for slot, (cls, offset) in enumerate(roots):
        put(fixture_header, 0x50 + slot * 4, origin + offset); struct.pack_into("<H", fixture_header, 0x150 + slot * 2, cls)
    for at in range(0, 0x1D0, 4): m.poke(0x80076C00 + at, word(fixture_header, at))
    m.r[16] = 1  # Explicit nonzero local-stream branch entry, not chronology.
    m.run(0x80015A54, {0x800163BC})
    check(m.read(0x800785E4) == model_at + len(model), "Native scene-base arithmetic only; scene not loaded/executed")
    for cls, offset in roots[1:]:
        root = model_at + offset; geometry = root + word(model, offset + 0x34)
        check(m.read(0x80076378 + cls * 4) == root and m.read(root + 0x34) == geometry, "Actual generic class registration/geometry root")
        for i in range(word(model, offset)):
            relative = word(model, offset + 0x38 + 4 * i)
            if relative == 0xFFFFFFFF: continue
            record = root + relative
            check(m.read(root + 0x38 + i * 4) == record and all(m.read(record + field) == geometry + word(model, offset + relative + field) for field in (0x14, 0x18)),
                  "Every source generic animation and geometry reference relocated")
    classes = {cls for cls, _ in roots}
    check(m.read(0x80076378) == native.PETE and all(m.read(0x80076378 + i * 4) == 0 for i in range(512) if i not in classes),
          "Resident player and every other model-table slot preserved")
    p, ids = 0, []
    while word(model, p) != 0xFFFFFFFF:
        animation, length = word(model, p), word(model, p + 4)
        check(m.read(native.PETE + 0x38 + 4 * animation) == model_at + p + 8, "Native special player stream installs every actual animation ID")
        ids.append(animation); p += length + 8
    check(m.bytes(native.COPY, len(data)) == loaded_data and m.r[29] == native.STACK, "Model loaders preserve complete used data and stack")
    return m, dict(model=hex(model_at), scene_arithmetic_only=hex(m.read(0x800785E4)), animation_ids=ids,
                   instructions=m.instructions + resident.instructions, calls=[list(c) for c in m.calls],
                   executed_instruction_count=len(m.executed | resident.executed))


def main(path):
    require(__debug__, "Assertions must be enabled")
    require_helpers()
    raw = load(next(iter(HELPERS)), "fresh_wizard_assets")
    upload_raw = load(list(HELPERS)[1], "fresh_wizard_upload")
    body_raw = load(list(HELPERS)[2], "fresh_wizard_body")
    native = load(list(HELPERS)[3], "fresh_wizard_native")
    checks, refused = [], []

    def check(ok, label): require(ok, label); checks.append(label)
    def reject(action, phrase, label):
        try: action()
        except (AssertionError, ValueError) as error:
            check(phrase in str(error), "Exact refusal " + label + ": " + str(error)); refused.append(label); return
        raise AssertionError("Expected refusal: " + label)

    with open(path, "rb") as image:
        raw.identity(image)
        try:
            assets = capture_assets(path, image, raw)
            fresh = upload_raw.capture(path)
            with contextlib.redirect_stdout(io.StringIO()): (body, _), *_ = body_raw.build_bodies(path)
            check(sha(body) == BASE_BODY_SHA and body[0x800:0xDE800] == fresh.upload, "Complete independent fresh baseline pairing")
            # A zero value is not free texture storage. Actual source-referenced
            # zero pixels are used, not a fabricated occupied canary.
            protected_zero = next(i for i, (v, owner) in enumerate(zip(fresh.upload, fresh.gpu_ownership)) if v == 0 and owner != 0)
            reject(lambda: require_background(fresh.gpu_ownership, protected_zero % 1024, protected_zero // 1024, 1, 1, "pixel"),
                   "Protected", "actual protected-zero texture byte")
            for values, phrase in (((-1, 0, 16, 32, "pixel"), "bounds"), ((1008, 0, 32, 32, "pixel"), "bounds"),
                                  ((112, 0, 32, 32, "pixel"), "page"), ((16, 250, 16, 32, "pixel"), "page"),
                                  ((1, 0, 32, 1, "palette"), "alignment"), ((0, 512, 32, 1, "palette"), "bounds")):
                reject(lambda values=values: require_rectangle(*values), phrase, "Invalid complete rectangle " + str(values))
            reject(lambda: rebase_uv(255, 0, 0, 0, 127, 0), "overflow", "Target UV byte overflow")
            reject(lambda: rebase_uv(0, 0, 1, 0, 0, 0), "overflow", "Negative target UV byte")
            source = dict(assets.owners)
            bad_block = bytearray(b"".join(b for _, b in assets.owners)); bad_block[0x35AC] ^= 1
            reject(lambda: require(sha(bad_block) == ASSET_SHA, "Complete source/padding changed"), "padding", "Original registered padding mutation")
            vram, rewritten, texture_owners, texture_patches, allocated = install_textures(fresh, assets, check)
            first = texture_owners[0]
            occupied = bytes(max(a, b) for a, b in zip(fresh.gpu_ownership, allocated))
            reject(lambda: require_background(occupied, first["x"] + 1, first["y"], 1, 1, "pixel"), "Protected", "Partial allocated-owner alias")

            data_before = body[0xDE800:0xDF0C8]; model_before = body[0xDF800:0xEF000]; scene_before = body[0xEF000:0xF2000]
            check(len(data_before) == 0x8C8 and sha(data_before) == "821d25d4c012aa180d369f755d82a780dbcf244b4e53c6c4bcf349da533dce65" and
                  sha(model_before) == "88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d" and
                  sha(scene_before) == "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433", "All exact old data/model/scene controls")
            _, particle_at, sound_at, old_ids = data_parts(data_before)
            check(old_ids == (0x15, 0x18, 2, 0x21, 0xC, 0xB, 0xA, 9, 1, 0) and 7 not in old_ids,
                  "Actual ten retained descriptor IDs leave07 absent")
            old_particle = data_before[particle_at:sound_at]; old_sound = data_before[sound_at:]
            _, town_sound_at, town_map, town_sounds = raw.sound(assets.town)
            check(old_sound == assets.town[town_sound_at:town_sound_at + len(old_sound)] and len(old_sound) == 0x6F8 and word(old_sound, 0x104) == 76,
                  "Complete same-issued76-descriptor Town sound component")
            equivalent_targets = {}
            for name, expected in (("local0", ()), ("local1", (66,)), ("local2", (67,)), ("global1F", ()), ("global26", (22,))):
                actual = tuple(i for i, (_, d, _, sample) in enumerate(town_sounds) if equivalent(source[name + ".descriptor"], source[name + ".sample"], d, sample))
                check(actual == expected, "Source-derived full descriptor/sample equivalence " + name)
                equivalent_targets[name] = actual
            changed_controls = bytearray(source["local1.descriptor"]); changed_controls[12] = 1
            reject(lambda: require(equivalent(changed_controls, source["local1.sample"], town_sounds[66][1], town_sounds[66][3]),
                                   "Different native sound controls"), "controls", "Same sample is not descriptor equivalence")
            require_sample(source["global1F.sample"], 0); require_sample(source["local0.sample"], 0)
            reject(lambda: require_sample(source["local0.sample"], 1), "Aligned", "Misaligned sample offset")
            reject(lambda: require_sample(source["local0.sample"][:-1], 0), "Aligned", "Truncated ADPCM owner")
            malformed_sample = bytearray(source["local0.sample"]); malformed_sample[1] = 1
            reject(lambda: require_sample(malformed_sample, 0), "termination", "Premature one-shot termination")

            bank = bytearray(fresh.upload[0x80000:]); sound = bytearray(old_sound); audio_additions = []
            for name, target_id in (("global1F", 76), ("local0", 77)):
                descriptor = bytearray(source[name + ".descriptor"]); sample = source[name + ".sample"]
                require(word(descriptor, 4) == word(descriptor, 12) == word(descriptor, 16) == 0, "Supported appended descriptor control policy")
                at = len(bank); require_sample(sample, at); put(descriptor, 0, at)
                sound.extend(descriptor); bank.extend(sample)
                audio_additions.append(dict(id=name, descriptor=target_id, sample_offset=at, length=len(sample), sha256=sha(sample)))
            used_bank = len(bank); bank.extend(bytes(round_cd(len(bank)) - len(bank)))
            put(sound, 0, len(sound)); put(sound, 0x104, 78); sound[4 + 0x1F] = 76
            check(used_bank == 0x65170 and len(bank) == 0x65800 and len(sound) == 0x720 and
                  [a["sample_offset"] for a in audio_additions] == [0x5E000, 0x5FD40], "Source-derived append-only bank/descriptor arithmetic")
            check(bank[:0x5E000] == fresh.upload[0x80000:] and bank[used_bank:] == bytes(len(bank) - used_bank) and
                  sound[0x108:len(old_sound)] == old_sound[0x108:] and
                  all(a == b for i, (a, b) in enumerate(zip(old_sound[4:0x104], sound[4:0x104])) if i != 0x1F),
                  "All76 old descriptors/bank bytes, padding and255 other global selectors preserved")
            check(sound[4 + 0x26] == 22 and word(sound, 0x108 + 22 * 20 + 12) == 0x20028, "Resident global26 nonzero controls retained exactly")
            wizard = bytearray(rewritten["wizard.model"]); wizard[4:7] = bytes([77, 66, 67])
            wrong_slots = bytearray(wizard); wrong_slots[4] = 0x1F
            reject(lambda: require(wrong_slots[4:7] == bytes([77, 66, 67]), "Local/global namespace mismatch"), "namespace", "Global selector cannot replace model-local descriptor ID")
            wrong_map = bytearray(sound); wrong_map[4 + 0x20] ^= 1
            reject(lambda: require(all(a == b for i, (a, b) in enumerate(zip(old_sound[4:0x104], wrong_map[4:0x104])) if i != 0x1F),
                                   "Global map complement changed"), "complement", "Unowned global selector patch")
            check(wizard[:4] == rewritten["wizard.model"][:4] and wizard[7:] == rewritten["wizard.model"][7:], "Only three declared source model sound slots changed")
            particle = bytearray(old_particle); particle.extend(rewritten["particle7"])
            put(particle, 0, len(particle)); put(particle, 4, len(old_ids) + 1)
            data = data_before[:particle_at] + bytes(particle) + bytes(sound)
            _, new_particle_at, new_sound_at, new_ids = data_parts(data)
            check(len(data) == 0x8FC and round_cd(len(data)) == 0x1000 and new_particle_at == particle_at and new_sound_at == sound_at + 12 and
                  new_ids == old_ids + (7,) and particle[8:len(old_particle)] == old_particle[8:] and data[:particle_at] == data_before[:particle_at],
                  "Actual new component grammar/endpoint and whole original data prefix/descriptor complement")
            for value, phrase in ((old_ids[0], "Unique"), (64, "signed-safe")):
                bad = bytearray(data); struct.pack_into("<H", bad, new_particle_at + len(old_particle), value)
                reject(lambda bad=bad: data_parts(bad), phrase, "Duplicate/out-of-table appended particle ID " + str(value))
            for offset, value, phrase, label in ((8, 173, "Component", "Unaligned terrain component"), (8, 0x10000, "Component", "Escaping component owner"),
                (new_particle_at, len(particle) + 4, "particle", "Particle length/count disagreement"),
                (new_sound_at + 0x104, 79, "count", "Unowned extra sound descriptor")):
                bad = bytearray(data); put(bad, offset, value)
                reject(lambda bad=bad: data_parts(bad), phrase, label)
            reject(lambda: data_parts(data[:-1]), "alignment", "Truncated complete data tail")

            root0 = model_before[:0xCC2C] + source["player.animation8"] + model_before[0xCC2C:0xCC30]
            class78 = model_before[0xCC30:]
            model = bytearray(root0 + class78 + rewritten["bolt.model"] + wizard)
            used_model = len(model); model.extend(bytes(round_cd(len(model)) - len(model)))
            roots = ((0, 0), (0x78, len(root0)), (0x26, len(root0) + len(class78)),
                     (0x11B, len(root0) + len(class78) + len(rewritten["bolt.model"])))
            check(roots == ((0, 0), (0x78, 0x10A4C), (0x26, 0x1361C), (0x11B, 0x13B80)) and used_model == 0x17134 and len(model) == 0x17800,
                  "Actual complete root/model allocation arithmetic with one final rounding")
            check(require_roots(model, roots) == (18, 19, 29, 40, 8), "All old stream records plus real animation8 before complete terminator")
            check(root0[:0xCC2C] == model_before[:0xCC2C] and root0[-4:] == model_before[0xCC2C:0xCC30] and
                  model[0x10A4C:0x1361C] == class78 and model[0x1712C:0x17134] == source["wizard.model"][0x35AC:] and
                  model[used_model:] == bytes(0x6CC), "Whole old player/class78 allocation and distinct original/new padding preserved")
            bad = bytearray(model); put(bad, len(root0) - 4, 8)
            reject(lambda: require_roots(bad, roots), "terminator", "Missing stream terminator")
            reject(lambda: require_roots(model, roots[:-1] + ((0x78, 0x13B80),)), "Unique", "Duplicate generic class registration")
            reject(lambda: require_roots(model, roots[:-1] + ((0x11B, len(model) + 4),)), "interval", "Root beyond complete model owner")
            upload = vram + bytes(bank)
            check(len(upload) == 0xE5800 and body[0xEF000:0xF2000] == scene_before and sha(body) == BASE_BODY_SHA,
                  "Complete paired resource output only; immutable original scene/body unchanged")

            # Run both exact old control and new resource case; no synthetic
            # helper return replaces parser, sample-offset or model relocation.
            old_machine, old_native = native_readback(native, assets, data_before, model_before, ((0, 0), (0x78, 0xCC30)), len(scene_before), check)
            m, new_native = native_readback(native, assets, data, bytes(model), roots, len(scene_before), check)
            for at, length in ((0x10A4C + 0x2930, 0x2A0), (0x13B80 + 0x35AC, 8), (used_model, len(model) - used_model)):
                check(m.bytes(native.COPY + len(data) + at, length) == model[at:at + length], "Actual loader preserves full registered/new padding")
            check(m.read(0x80076278 + 7 * 4) == native.COPY + new_particle_at + len(old_particle) and
                  new_native["animation_ids"] == [18, 19, 29, 40, 8], "Actual native registration of descriptor07 and player animation8")
            # Native code trusts some lengths. A complete-owner guard is a
            # separate proof; do not report the native parser as a validator.
            check(old_native["model"] == "0x8008a794", "Old source-native endpoint control unchanged")
            code = native_code(native, assets)
            truncated_native = native_parser(native, code, data[:-1])
            check(truncated_native.read(0x800785E0) == native.COPY + len(data),
                  "Actual native blind spot: unread final descriptor tail is not validated by returned endpoint")
            broken = bytearray(data); put(broken, 8, 0x10000)
            reject(lambda: native_parser(native, code, broken), "Unmapped", "Actual native component access escapes mapped owner")
            reject(lambda: native_readback(native, assets, data, bytes(model), roots[:-1] + ((0x11B, len(model) + 4),), len(scene_before), check),
                   "Unmapped", "Actual native model loader refuses unowned memory via strict interpreter")
            for name, start, _, _ in native.SPANS:
                changed = bytearray(assets.exe); changed[start - 0x8000F800] ^= 1
                reject(lambda changed=changed: native_code(native, assets._replace(exe=bytes(changed))), "instruction source", "Mutated native source pin " + name)
            double_load = native.Machine({0x100: 0x8C820000, 0x104: 0x8C820004, 0x108: 0x00401821, 0x10C: 0x00402821})
            double_load.blob(0x1000, struct.pack("<II", 42, 77)); double_load.r[4], double_load.r[2] = 0x1000, 9
            double_load.run(0x100, {0x110})
            check(double_load.r[2:4] == [77, 9] and double_load.r[5] == 77, "Pinned same-target load cancellation microcontrol")
            reject(lambda: native.Machine({0x100: 0xFFFFFFFF}).run(0x100, {0x104}), "Unsupported", "Unknown opcode never synthesized")
            # Exhaustive byte partition: every VRAM byte is exactly one old
            # source/background owner or one of the ten disjoint new rectangles.
            owner_tags = bytearray(fresh.gpu_ownership)
            for index, owner in enumerate(texture_owners, 3):
                for at in indices(owner["x"], owner["y"], owner["row_bytes"], owner["rows"]):
                    require(owner_tags[at] == 0, "Final VRAM partition aliases original/new owner")
                    owner_tags[at] = index
            partition = []; begin = 0
            while begin < len(owner_tags):
                end = begin + 1
                while end < len(owner_tags) and owner_tags[end] == owner_tags[begin]: end += 1
                partition.append((begin, end - begin, owner_tags[begin], sha(vram[begin:end]))); begin = end
            check(sum(n for _, n, _, _ in partition) == 0x80000 and sum(n for _, n, tag, _ in partition if tag >= 3) == 2216,
                  "Complete unambiguous VRAM byte-owner partition")
            data_cd = data + bytes(round_cd(len(data)) - len(data))
            for name, payload in (("upload", upload), ("vram", vram), ("bank", bank), ("data", data), ("data_cd", data_cd), ("model", model)):
                check(sha(payload) == RESOURCE_GOLDENS[name], "First source-only complete output golden " + name)
                changed = bytearray(payload); changed[-1] ^= 1
                reject(lambda changed=changed, name=name: require(sha(changed) == RESOURCE_GOLDENS[name], "Complete afterimage golden changed"),
                       "golden", "Changed complete output/padding " + name)
            result = dict(marker="FRESH_WIZARD_RESOURCE_SOURCE_ORACLE_OK", checks=len(checks), refusals=len(refused),
                helper_pins=HELPERS, original_sha256=raw.IMAGE_SHA, source_asset_sha256=ASSET_SHA,
                baseline_body_sha256=sha(body), scene_unchanged_sha256=sha(scene_before),
                upload_length=len(upload), upload_sha256=sha(upload), vram_sha256=sha(vram),
                bank_used=used_bank, bank_length=len(bank), bank_sha256=sha(bank),
                data_used=len(data), data_sha256=sha(data), data_cd_length=round_cd(len(data)),
                data_cd_sha256=sha(data + bytes(round_cd(len(data)) - len(data))),
                model_used=used_model, model_length=len(model), model_sha256=sha(model),
                relative_model_roots=[list(r) for r in roots], texture_owners=texture_owners, texture_field_patches=texture_patches,
                allocated_vram_bytes=allocated.count(1), protected_zero_bytes=sum(v == 0 and o != 0 for v, o in zip(fresh.upload, fresh.gpu_ownership)),
                complete_vram_partition_runs=len(partition), vram_partition_sha256=sha(json.dumps(partition, separators=(",", ":")).encode()),
                sound_additions=audio_additions, global_map_patch=dict(selector=31, before=0, after=76), local_slots=[77, 66, 67],
                particle_ids=list(new_ids), source_native_pins=[dict(name=n, pc=hex(a), length=l, sha256=h) for n, a, l, h in native.SPANS],
                original_native=old_native, target_native=new_native, refusal_cases=refused,
                native_parser_sound_and_model_loaders_executed=True, native_helper_stubs=0,
                scalar_copy_base_is_fixture_not_final_anchor=True, registration_header_is_synthetic_not_emitted=True,
                scene_body_overlay_disc_outputs_emitted=False, dynamic_capacity_or_peak_proved=False,
                native_parser_validates_complete_input=False, global_audio_all_consumer_compatibility=False,
                particle_frame_or_callbacks_executed=False, spu_hardware_or_listening_accepted=False,
                runtime_or_publication_approved=False, payload_files_written=0)
        finally:
            raw.identity(image)
            require_helpers()
    result["source_unchanged"] = True
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    args = argparse.ArgumentParser(description=__doc__); args.add_argument("original_bin")
    main(args.parse_args().original_bin)
