#!/usr/bin/env python3
"""Child-relative four-tile family used-data reference, never a source issuer.

Read only the exact already-verified R07 BIN/CUE. Independently locate its WAD
and actual R03 Body/data, preserve installed metadata and the complete family
tail, and insert the frozen neutral surface. No Original input, Core output,
resource issuance, Body/disc construction, runtime execution or output files.
The next Core issuer must independently establish actual typed source ancestry.
"""
import contextlib
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
IMAGE_LENGTH = 661547040
IMAGE_SHA = "a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50"
BODY_SHA = "2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504"
CUE_BYTES = (b'FILE "native-fresh-saved-level-r01.bin" BINARY\n'
             b'  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n')
HELPERS = {
    "src/Spyro.Editor.NativeId65FreshWizardBodySmoke/source_fresh_chest_spawn_collider_diagnostic.py":
        "a66004ad9fd42b5c20580adb3d8be2783f6e65b65f32e5bcaf94a47e6c057b3b",
    "src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/source_tiled_family_surface_oracle.py":
        "e3bfb4fa6861fc2230b8b3ba22cc78231bf03808b7cd9575e15874f23e068a7f",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py":
        "4aa6bc207bed327c8fb2f9832b8bd1bc098b26b0f50a4817ba55183373be0ec3",
}
PARTICLE_IDS = (0x15, 0x18, 2, 0x21, 0xC, 0xB, 0xA, 9, 1, 0, 7)
# Frozen from this script's first independent child-relative --derive execution,
# before any four-tile family resource issuer exists. Never self-updated.
# Split LQ/HQ record hashes were corrected and independently remeasured against
# the unchanged exact child; all whole metadata/data/allocation pins stayed fixed.
# --derive is a reporting mode, not a bypass of these frozen references.
PINS = {
    "oldUsedByteLength": 0xA6C,
    "oldUsedSha256": "cc8d9503d511c780ee6e583f0109f106b8f634eb8ff58f07283b8b8790a0e05b",
    "oldAllocationSha256": "d1b1b48eb310699a7579d3e7b5dcd5d3aa079f28236a4ad3029252aa22e96235",
    "metadataByteLength": 0x178,
    "metadataSha256": "d36546bd44b8816a4b285afe9782dda0d1e46f8adf63f272412cb50cf9c77067",
    "metadataRecordSha256": [
        "27e844b704fc16372b0006639b0ea57553ece23e89c0ffd56383cddd1377c065",
        "f25d25b4df2cb6b5a633fb7397fe0d563b4af4601ce446438934edf687167c66"],
    "oldSurfaceSha256": "399c3cc296d147abac34e7ba91024bfd1c02e339ae6282f4b57b0e708089b111",
    "tailByteLength": 0x7B0,
    "tailSha256": "279df31a0ce9b325f2066754f78eda88cdf07a6be00c9d6dc01b240915f14f2c",
    "particleSha256": "f803ce1a4593c8f11b63389dbf085232a88249a059422d0d63bee5995b7cbfa4",
    "soundSha256": "c33eb8c23f7de2878c888752a3bf2bb92a3e3d7239d334c240642d1d16a9d7b2",
    "newUsedByteLength": 0xCB8,
    "newUsedSha256": "fe754f8e5e4686fdfe03d7e516dab8f1b52ad605b1541615e98d3f0511e4835e",
    "newZeroPaddedAllocationSha256": "83baa79d7b1f9f27eadecb721f1026b0d0aea57cc65f0758b5353eaa0fc36fb6",
    "newSurfaceSha256": "a18fbf9c2bc48e8d4b6290e7250236773daaf13fe4d8733fb6526cc28d24c11b",
    "newTailOffsets": {"portals": 0x508, "particles": 0x50C, "sound": 0x598},
    "newRemainingPadding": 0x348,
    "orderedFaceBindings": [0, 0, 0, 1, 0, 1, 1, 1],
    "dataRelativeMaterialWords": [532, 548, 692, 708, 852, 868, 1012, 1028],
    "totalTexturePacketBytesPerEmittedPass": 96,
    "increaseOverOldTwoFaceContribution": 72,
    "completeMetadataAndSuffixPreserved": True,
    "completeImmediateDataInverse": True,
}


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def word(raw, at):
    require(type(at) is int and at >= 0 and at % 4 == 0 and at + 4 <= len(raw),
            "Complete bounded aligned dword")
    return struct.unpack_from("<I", raw, at)[0]


def take(raw, at, length):
    require(type(at) is int and type(length) is int and at >= 0 and length > 0 and
            at <= len(raw) - length, "Complete bounded immutable byte owner")
    return bytes(raw[at:at + length])


def identity(value):
    return value.st_dev, value.st_ino, value.st_size, value.st_mtime_ns, value.st_ctime_ns


def authenticate(stream, path, initial, length, digest):
    require(identity(os.fstat(stream.fileno())) == identity(path.stat()) == initial,
            "Actual held/named file identity")
    require(initial[2] == length, "Exact complete input length")
    stream.seek(0)
    checksum, count = hashlib.sha256(), 0
    while block := stream.read(1024 * 1024):
        count += len(block)
        checksum.update(block)
    require(count == length and stream.read(1) == b"" and checksum.hexdigest() == digest,
            "Complete input hash and true EOF")
    require(identity(os.fstat(stream.fileno())) == identity(path.stat()) == initial,
            "Actual held/named identity after complete hash")


def definitions(before):
    require(Path(__file__).read_bytes() == before, "Oracle definition unchanged")
    for relative, expected in HELPERS.items():
        require(sha((ROOT / relative).read_bytes()) == expected, "Pinned helper: " + relative)


def load(relative, name):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def dual(raw, at):
    require(0 <= at <= len(raw) - 8, "Complete ISO dual-endian scalar")
    little = int.from_bytes(raw[at:at + 4], "little")
    require(little == int.from_bytes(raw[at + 4:at + 8], "big"), "ISO dual-endian agreement")
    return little


def directory_record(raw):
    require(len(raw) >= 34 and raw[0] == len(raw) and 33 + raw[32] <= len(raw),
            "Complete ISO directory record/name")
    return raw[33:33 + raw[32]], dual(raw, 2), dual(raw, 10), raw[25]


def extract(image, reader):
    # Reader import supplies only the reviewed bounded Mode2 logical read;
    # its Original/candidate inspection and main are never called.
    pvd = reader.logical(image, 16 * 2048, 2048)
    require(pvd[:7] == b"\x01CD001\x01" and pvd[128:132] == b"\0\x08\x08\0",
            "Actual ISO primary descriptor and 2048-byte dual-endian logical sectors")
    _, root_lba, root_length, flags = directory_record(take(pvd, 156, pvd[156]))
    require((root_lba, root_length, flags) == (22, 2048, 2), "Complete bounded actual root directory")
    root = reader.logical(image, root_lba * 2048, root_length)
    rows, cursor = {}, 0
    while cursor < len(root) and root[cursor]:
        record = take(root, cursor, root[cursor])
        name, lba, length, row_flags = directory_record(record)
        require(name not in rows, "Unique ISO directory owner")
        rows[name] = (lba, length, row_flags)
        cursor += len(record)
    require(root[cursor:] == bytes(len(root) - cursor), "Complete root terminator and zero complement")
    require(b"WAD.WAD;1" in rows and b"SCUS_942.28;1" in rows, "Actual WAD and executable directory owners")
    wad_lba, wad_length, wad_flags = rows[b"WAD.WAD;1"]
    require(wad_lba == 37 and wad_flags == 0 and 0 < wad_length <= IMAGE_LENGTH // 2352 * 2048,
            "Bounded exact child WAD owner")
    wad = reader.logical(image, wad_lba * 2048, 2048)
    host_at, host_length, body_at, body_length = struct.unpack_from("<4I", wad, 79 * 8)
    require((host_at, host_length, body_at, body_length) == (0x6927000, 0x12000, 0x6939000, 0x20F000),
            "Actual child WAD79/80 Host and Body descriptors")
    require(body_at == host_at + host_length and body_at + body_length == wad_length and
            rows[b"SCUS_942.28;1"] == (wad_lba + wad_length // 2048, 0x66000, 0),
            "Packed actual Host/Body/WAD end followed by the retained relocated executable")
    body = reader.logical(image, wad_lba * 2048 + body_at, body_length)
    require(sha(body) == BODY_SHA, "Whole already-verified R03 Body child identity, not Original provenance")
    descriptors, cursor, terminated = [], 2048, False
    for index in range(10):
        at, length = word(body, index * 8), word(body, index * 8 + 4)
        if (at, length) == (0, 0):
            terminated = True
            continue
        require(not terminated and at == cursor and length > 0 and length % 2048 == 0 and
                at <= len(body) - length, "Packed bounded complete actual Body descriptors")
        descriptors.append((at, length))
        cursor += length
    require(len(descriptors) == 8 and cursor == len(body) and body[0x1D0:0x800] == bytes(0x630),
            "Eight subfiles cover the complete Body and retain its zero header padding")
    require(descriptors[0] == (0x800, 0xE5800) and descriptors[1] == (0xE6000, 0x1000) and
            descriptors[2] == (0xE7000, 0x1B000) and descriptors[3] == (0x102000, 0x3800),
            "Actual installed upload/data/model/Scene owners, not P0 capacities")
    allocated = take(body, *descriptors[1])
    owners = [{"index": index, "bodyOffset": at, "byteLength": length,
               "sha256": sha(take(body, at, length))} for index, (at, length) in enumerate(descriptors)]
    return allocated, {
        "pvdSha256": sha(pvd), "rootSha256": sha(root), "wadHeaderSha256": sha(wad),
        "wadLba": wad_lba, "wadByteLength": wad_length,
        "hostWadOffset": host_at, "hostByteLength": host_length,
        "bodyWadOffset": body_at, "bodyByteLength": body_length, "bodySha256": sha(body),
        "dataBodyOffset": descriptors[1][0], "dataLogicalByteOffset": wad_lba * 2048 + body_at + descriptors[1][0],
        "retainedBodySubfiles": owners,
    }


def old_data(allocated):
    require(type(allocated) is bytes and len(allocated) == 0x1000, "Complete actual two-sector data allocation")
    require(word(allocated, 0) == 0x178 and word(allocated, 4) == 2,
            "Exact installed two-record metadata, without recapturing or reallocating sources")
    cursor, components = 0x178, []
    for expected in (0xAC, 0x20, 8, 0x60, 0x10):
        length = word(allocated, cursor)
        require(length == expected and cursor + length <= 0xA6C, "Complete old single-floor component owner")
        components.append((cursor, length))
        cursor += length
    require(cursor == 0x2BC and word(allocated, cursor) == 0, "Old144 surface and raw zero-portal count")
    particles = take(allocated, cursor + 4, 0x8C)
    require(word(particles, 0) == len(particles) and word(particles, 4) == 11 and
            tuple(struct.unpack_from("<h", particles, 8 + 12 * i)[0] for i in range(11)) == PARTICLE_IDS and
            all(struct.unpack_from("<h", particles, 10 + 12 * i)[0] == 8 for i in range(11)),
            "Complete actual eleven-particle table and every signed ID/record width")
    sound = take(allocated, cursor + 4 + len(particles), 0x720)
    require(word(sound, 0) == len(sound) and word(sound, 0x104) == 78 and
            all(value < 78 for value in sound[4:0x104]) and
            all(word(sound, 0x108 + 20 * i) % 16 == 0 for i in range(78)),
            "Complete actual 256-map/78-descriptor sound component and aligned sample owners")
    require(cursor + 4 + len(particles) + len(sound) == 0xA6C and
            allocated[0xA6C:] == bytes(0x594), "Exact old used end and all original allocation padding")
    require(word(allocated, 0x208) == 0 and word(allocated, 0x218) == 1 and
            word(allocated, 0x20C) == word(allocated, 0x21C) == 0x80,
            "Original two installed material slots and primitive flags")
    return take(allocated, 0, 0x178), take(allocated, 0x178, 0x144), take(allocated, 0x2BC, 0x7B0), components


def compose(allocated, neutral):
    metadata, old_surface, tail, components = old_data(allocated)
    raw = neutral.surface()
    neutral.validate_surface(raw)
    require(sha(raw) == neutral.PINS["surfaceSha256"], "Frozen complete independent neutral surface")
    bound = neutral.bind_materials(raw, neutral.FACES)
    require(sha(bound) == neutral.PINS["boundSurfaceSha256"], "Frozen independent eight neutral bindings")
    used = metadata + bound + tail
    require(len(used) == 0xCB8 and len(bound) == 0x390 and len(tail) == 0x7B0,
            "Measured178+390+7B0 exact CB8 used data fits existing1000 allocation")
    require(used[:0x178] == allocated[:0x178] and used[0x508:] == allocated[0x2BC:0xA6C],
            "Entire installed metadata and translated family suffix preserved byte-for-byte")
    neutral.validate_surface(used[0x178:0x508], True)
    require(word(used, 0x508) == 0 and word(used, 0x50C) == 0x8C and word(used, 0x510) == 11 and
            word(used, 0x598) == 0x720 and word(used, 0x69C) == 78,
            "Translated portal/particle/sound/count boundaries, not the smaller P0 suffix")
    return used, old_surface, components


def inverse(supplied, expected, allocated):
    require(type(supplied) is bytes and supplied == expected, "Complete exact new used-data inverse preimage")
    metadata, old_surface, tail, _ = old_data(allocated)
    restored = supplied[:0x178] + old_surface + supplied[0x508:] + allocated[0xA6C:]
    require(restored == allocated and supplied[:0x178] == metadata and supplied[0x508:] == tail,
            "Complete immediate inverse including all captured old surface and zero allocation padding")
    return restored


def reference(allocated, neutral):
    used, old_surface, old_components = compose(allocated, neutral)
    require(inverse(used, used, allocated) == allocated, "Full actual data-allocation inverse")
    # Native metadata is header8 + both16-byte LQ rows + both168-byte HQ
    # rows, NOT two adjacent184-byte records. Reassemble each actual record.
    records = tuple(take(allocated, 8 + 16 * i, 16) + take(allocated, 0x28 + 168 * i, 168)
                    for i in range(2))
    require(all(len(record) == 184 for record in records) and
            allocated[:8] + b"".join(record[:16] for record in records) +
            b"".join(record[16:] for record in records) == allocated[:0x178],
            "Complete split LQ/HQ metadata record reassembly and byte-exact table inverse")
    refusals = []
    def reject(label, action):
        try:
            action()
        except (AssertionError, ValueError):
            refusals.append(label)
            return
        raise AssertionError("Negative control accepted: " + label)
    for at in (0, 4, 0x178, 0x224, 0x244, 0x24C, 0x2AC, 0x2BC, 0x2C0,
               0x2C4, 0x2CA, 0x34C, 0x450, 0x454, 0xA6C, 0xFFF):
        corrupt = bytearray(allocated)
        corrupt[at] ^= 1
        reject("old-owner-boundary-" + hex(at), lambda corrupt=corrupt: old_data(bytes(corrupt)))
    for bad in (allocated[:-1], allocated + bytes(4)):
        reject("old-allocation-extent", lambda bad=bad: old_data(bad))
    # Exact complete inverse covers arbitrary retained metadata/record payload,
    # surface/flag/guard, sound-map/descriptor and first/last suffix mutations.
    offsets = {0, 4, 8, 0x17, 0x18, 0x27, 0x28, 0xCF, 0xD0, 0x177, 0x178, 0x507, 0x508, 0x50C,
               0x514, 0x597, 0x598, 0x59C, 0x69B, 0x69C, 0x6A0, 0xCB7}
    offsets.update(0x178 + at for at in neutral.FACE_OFFSETS)
    offsets.update(0x17C + at for at in neutral.FACE_OFFSETS)
    for at in sorted(offsets):
        corrupt = bytearray(used)
        corrupt[at] ^= 1
        reject("new-owner-inverse-" + hex(at), lambda corrupt=corrupt: inverse(bytes(corrupt), used, allocated))
    for bad in (used[:-1], used + bytes(4), allocated, used + bytes(0x348)):
        reject("new-used-extent", lambda bad=bad: inverse(bad, used, allocated))
    return {
        "oldUsedByteLength": 0xA6C, "oldUsedSha256": sha(allocated[:0xA6C]),
        "oldAllocationSha256": sha(allocated), "metadataByteLength": 0x178,
        "metadataSha256": sha(allocated[:0x178]),
        "metadataRecordSha256": [sha(record) for record in records],
        "oldSurfaceSha256": sha(old_surface), "tailByteLength": 0x7B0,
        "tailSha256": sha(allocated[0x2BC:0xA6C]),
        "particleSha256": sha(allocated[0x2C0:0x34C]), "soundSha256": sha(allocated[0x34C:0xA6C]),
        "newUsedByteLength": len(used), "newUsedSha256": sha(used),
        "newZeroPaddedAllocationSha256": sha(used + bytes(0x348)),
        "newSurfaceSha256": sha(used[0x178:0x508]),
        "newSurfaceComponents": [{"kind": name, "dataOffset": 0x178 + at, "byteLength": length,
                                  "sha256": sha(used[0x178 + at:0x178 + at + length])}
                                 for name, at, length in neutral.SPANS],
        "oldSurfaceComponents": [{"dataOffset": at, "byteLength": length,
                                  "sha256": sha(allocated[at:at + length])} for at, length in old_components],
        "newTailOffsets": {"portals": 0x508, "particles": 0x50C, "sound": 0x598},
        "newRemainingPadding": 0x348, "orderedFaceBindings": list(neutral.FACES),
        "dataRelativeMaterialWords": [0x178 + at for at in neutral.FACE_OFFSETS],
        "totalTexturePacketBytesPerEmittedPass": 96, "increaseOverOldTwoFaceContribution": 72,
        "completeMetadataAndSuffixPreserved": True, "completeImmediateDataInverse": True,
        "negativeControls": len(refusals),
    }


def main(argv=None):
    arguments = list(sys.argv[1:] if argv is None else argv)
    require(len(arguments) == 3 and arguments[0] in ("--derive", "--child-reference"),
            "Only --derive/--child-reference followed by exact absolute R07 BIN and CUE paths")
    mode, bin_name, cue_name = arguments
    bin_path, cue_path = Path(bin_name), Path(cue_name)
    require(bin_path.is_absolute() and cue_path.is_absolute() and bin_path.parent == cue_path.parent and
            bin_path.name == "native-fresh-saved-level-r01.bin" and cue_path.name == "native-fresh-saved-level-r01.cue",
            "Explicit paired R07 artifact names; no automatic source discovery or output arguments")
    before = Path(__file__).read_bytes()
    definitions(before)
    reader = load(next(iter(HELPERS)), "child_only_mode2_reader")
    neutral = load(list(HELPERS)[1], "frozen_tiled_family_neutral")
    neutral.preserved_control()
    neutral.validate_document(neutral.CANONICAL)
    held, report = [], None
    with contextlib.ExitStack() as stack:
        try:
            for path, length, digest in ((bin_path, IMAGE_LENGTH, IMAGE_SHA),
                                         (cue_path, len(CUE_BYTES), sha(CUE_BYTES))):
                stream = stack.enter_context(path.open("rb"))
                initial = identity(os.fstat(stream.fileno()))
                held.append((stream, path, initial, length, digest))
                authenticate(stream, path, initial, length, digest)
            allocated, extraction = extract(held[0][0], reader)
            report = reference(allocated, neutral)
            require(PINS and all(report[key] == value for key, value in PINS.items()),
                    "Independent child-relative pins must already be frozen and match")
        finally:
            failures = []
            for stream, path, initial, length, digest in held:
                try:
                    authenticate(stream, path, initial, length, digest)
                    with path.open("rb") as reopened:
                        authenticate(reopened, path, initial, length, digest)
                except BaseException as error:
                    failures.append(error)
            try:
                definitions(before)
            except BaseException as error:
                failures.append(error)
            if failures:
                raise BaseExceptionGroup("Final complete child/CUE/definition guards", failures)
    print(json.dumps({
        "evidenceKind": "child-relative-detached-used-data-reference",
        "inputBinSha256": IMAGE_SHA, "inputCueSha256": sha(CUE_BYTES), "oracleSha256": sha(before),
        "canonicalDocumentSha256": sha(neutral.CANONICAL), "extraction": extraction, "reference": report,
        "completeBeforeAfterReopenedInputGuards": True, "originalOpened": False,
        "childImageIdentityVerified": True, "sourceCertified": False,
        "resourceRevisionIssued": False, "bodyIssued": False, "discIssued": False,
        "nativeLoaderExecuted": False, "runtimeVerified": False, "publicationApproved": False,
        "completeNativeDependencyClosureProven": False,
        "scope": "Installed R03 child metadata/tail are retained inputs, not independent Original provenance. Core must separately prove actual typed ancestry.",
    }, indent=2))
    print("TILED_FAMILY_DATA_CHILD_REFERENCE_OK" if mode == "--child-reference" else
          "TILED_FAMILY_DATA_CHILD_MEASURED_UNFROZEN")


if __name__ == "__main__":
    main()
