"""Independent N1/N3 complete body/raw-image references and full preimage inverse.

Runs preserved N2/legacy/parity controls. All source reads retain authenticated
same-pass raw preimages. No Core, candidate image, file writes or runtime claim.
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
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_selected_material_oracle.py":
        "7b58886dccebb1c03fe6b7d37dc23cb609d56db223f9f572e821a93a4508a715",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_material_disc_oracle.py":
        "df21767a87bf9b7c4a355bb1bf659c5d08364e8606e16bddd98cef79b3fc1f90",
    "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py":
        "f93780c582df180f378a4121a824d01dec6f13e3c5da68a61af571772e732608",
    "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py":
        "e69619f391814c270c9c28bc588f432f79f8a6cd3f61301e3a1c483a8645ad07",
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


def bodies(captured, baseline, geometry):
    untextured = geometry.body(baseline, captured.before_data, captured.tail)
    require(sha(untextured) == "37157a7248c90665309d8cee21c739a846aca284426cd602c65d627788c06009" and
            len(untextured) == 0x1FB800 and untextured[0x800:0xDE800] == captured.before_upload,
            "Independent complete Base tiled body and fresh upload agree")
    require(struct.unpack_from("<8I", untextured) == (0x800, 0xDE000, 0xDE800, 0x1000, 0xDF800, 0xF800, 0xEF000, 0x3000),
            "All physical header allocations remain fixed")
    results = []
    for case in captured.cases:
        require(len(case.upload) == 0xDE000 and len(case.data) in (0xB10, 0xC80), "Complete independent selected upload/data shape")
        body = bytearray(untextured)
        body[0x800:0xDE800] = case.upload
        body[0xDE800:0xDF800] = case.data + bytes(0x1000 - len(case.data))
        require(len(body) == len(untextured) and body[:0x800] == untextured[:0x800] and
                body[0xDF800:] == untextured[0xDF800:] and
                body[0xDE800 + len(case.data):0xDF800] == bytes(0x1000 - len(case.data)),
                "All header/model/scene/auxiliaries and every byte of fixed data padding remain exact")
        expected = bytes(body)

        def restore(after):
            require(after == expected, "Complete body inverse afterimage mismatch")
            result = bytearray(after)
            result[0x800:0xDE800] = captured.before_upload
            result[0xDE800:0xDF800] = captured.before_data + bytes(0x1000 - len(captured.before_data))
            require(result == untextured, "Complete body inverse preimage mismatch")
            return bytes(result)

        require(restore(expected) == untextured, "Whole independently reconstructed body inverse")
        for at in (0, 0x800, 0xDE800, 0xDE800 + len(case.data), 0xDF800, 0xEF000, len(body) - 1):
            corrupt = bytearray(body)
            corrupt[at] ^= 1
            try:
                restore(bytes(corrupt))
            except AssertionError as error:
                require("body inverse afterimage" in str(error), "Exact header/upload/data/padding/model/scene/auxiliary corruption refusal")
            else:
                raise AssertionError("Corrupt complete body accepted")
        for corrupt in (expected[:-1], expected + b"\0"):
            try:
                restore(corrupt)
            except AssertionError as error:
                require("body inverse afterimage" in str(error), "Complete body length/EOF refusal")
            else:
                raise AssertionError("Truncated or extended body accepted")
        results.append((case, expected))
    return tuple(results)


def main(path):
    require(__debug__, "Preserved assertion controls must execute")
    require_helpers()
    selected = load(list(HELPERS)[0], "selected_disc_source")
    old = load(list(HELPERS)[1], "selected_n2_disc_control")
    output = io.StringIO()
    with contextlib.redirect_stdout(output):
        old.main(path)
    control = json.loads(output.getvalue())
    require(control["marker"] == "TILED_MATERIAL_DISC_INDEPENDENT_ORACLE_OK" and control["original_form1_parity_control"] and
            control["legacy_and_tiled_full_image_controls"] and control["all46_music_controls"] and
            control["independent_raw_inverse_executed"] and len(control["cases"]) == 3 and
            all(c["inverse_sha256"] == selected.ORIGINAL for c in control["cases"]),
            "Preserved complete N2/legacy/Base images, parity, all music controls and inverse execute")
    captured = selected.capture(path)
    sys.path.insert(0, str(ROOT / "src/Spyro.Editor.NativeId65FreshDiscSmoke"))
    raw = selected.load("src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py", "selected_raw_disc")
    geometry = selected.load("src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py", "selected_disc_geometry")
    with contextlib.redirect_stdout(io.StringIO()):
        (baseline, overlay), *_ = raw.build_bodies(path)
    fixtures = bodies(captured, baseline, geometry)
    require(len(overlay) == 0xF800, "Complete unchanged physical engine-overlay allocation")
    results = []
    with open(path, "rb") as source:
        index = selected.source_index(source)

        def sector(lba):
            return selected.sector(source, index, lba)

        root_before, wad_before = sector(22)[24:2072], sector(37)[24:2072]
        root, wad = bytearray(root_before), bytearray(wad_before)
        original_exe = selected.logical(source, index, 53875, 0, 0x66000)
        require(sha(original_exe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998", "Whole original executable owner")
        executable = raw.compose_exe(original_exe, 19)
        cursor, rows = 0, {}
        while cursor < 2048 and root[cursor]:
            size = root[cursor]
            require(size >= 34 and cursor + size <= 2048, "Bounded root directory record")
            row = root[cursor:cursor + size]
            name = row[33:33 + row[32]].decode("ascii").removesuffix(";1")
            extent, length = struct.unpack_from("<I", row, 2)[0], struct.unpack_from("<I", row, 10)[0]
            require(extent == int.from_bytes(row[6:10], "big") and length == int.from_bytes(row[14:18], "big") and
                    name not in rows, "Complete unique dual-endian directory ownership")
            rows[name] = (cursor, extent, length)
            cursor += size
        require(rows["WAD.WAD"][1:] == (37, 0x6927000) and rows["SCUS_942.28"][1:] == (53875, 0x66000) and
                rows["PETEXA0.STR"][1] == 60000 and wad[79 * 8:81 * 8] == bytes(16), "Exact original physical directory/allocation preimages")
        final_exe, final_end, final_wad = 54921, 55125, 0x6B32000
        root_fields = set()
        for name, extent, length in (("WAD.WAD", 37, final_wad), ("SCUS_942.28", final_exe, 0x66000)):
            at = rows[name][0]
            for offset, value in ((2, extent), (10, length)):
                struct.pack_into("<I", root, at + offset, value)
                struct.pack_into(">I", root, at + offset + 4, value)
                root_fields.update(range(at + offset, at + offset + 8))
        struct.pack_into("<IIII", wad, 79 * 8, 0x6927000, len(overlay), 0x6936800, 0x1FB800)
        require(all(root[i] == root_before[i] for i in range(2048) if i not in root_fields) and
                wad[:79 * 8] == wad_before[:79 * 8] and wad[81 * 8:] == wad_before[81 * 8:],
                "Complete ISO/WAD metadata complements remain unchanged")
        for case, body in fixtures:
            append = overlay + body
            require(len(append) == 0x20B000 and 53875 + len(append) // 2048 == final_exe and
                    0x6927000 + len(append) == final_wad, "Selected metadata consumes only the fixed data allocation, not new physical disc space")
            forward, inverse = hashlib.sha256(), hashlib.sha256()
            patches = {}
            for lba in range(selected.SECTORS):
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
                require(len(after) == 2352 and after[:16] == before[:16], "Complete raw sector retains its destination MSF/header owner")
                forward.update(after)
                if after != before:
                    patches[lba] = (before, after)
            # Count is measured rather than copied from a production plan.
            require(patches and min(patches) == 22 and max(patches) == final_end - 1 and
                    all(lba in (22, 37, 53874) or 53875 <= lba < final_end for lba in patches),
                    "Exact bounded physical write domains and complete raw afterimages")

            def restore(lba, after):
                require(0 <= lba < selected.SECTORS and len(after) == 2352, "Bounded complete raw inverse sector")
                if lba in patches:
                    before, expected = patches[lba]
                    require(after == expected, "Selected raw inverse afterimage mismatch")
                else:
                    before = after
                require(hashlib.sha256(before).digest() == index[lba * 32:(lba + 1) * 32], "Selected raw inverse preimage mismatch")
                return before

            restored_length = 0
            for lba in range(selected.SECTORS):
                before = sector(lba)
                restored = restore(lba, patches[lba][1] if lba in patches else before)
                require(restored == before, "Every restored raw sector equals its complete original owner")
                inverse.update(restored)
                restored_length += len(restored)
            require(restored_length == selected.SIZE and inverse.hexdigest() == selected.ORIGINAL, "Independent full-image inverse length and original digest")
            for lba in (22, 37, 54079, 55124, 100000):
                after = bytearray(patches[lba][1] if lba in patches else sector(lba))
                after[-1] ^= 1
                try:
                    restore(lba, bytes(after))
                except AssertionError as error:
                    require("inverse" in str(error), "Exact changed/unchanged raw inverse corruption refusal")
                else:
                    raise AssertionError("Corrupt raw afterimage accepted")
            for after in (sector(100000)[:-1], sector(100000) + b"\0"):
                try:
                    restore(100000, after)
                except AssertionError as error:
                    require("complete raw inverse sector" in str(error), "Exact raw sector length refusal")
                else:
                    raise AssertionError("Truncated or extended raw sector accepted")
            results.append(dict(name=case.name, selections=[(s.entry, s.texture) for s in case.sources], orderedFaceIds=case.faces,
                uploadSha256=sha(case.upload), dataSha256=sha(case.data), bodySha256=sha(body), imageSha256=forward.hexdigest(),
                bodyLength=len(body), imageLength=selected.SIZE, usedData=len(case.data), dataPadding=0x1000 - len(case.data),
                changedSectors=len(patches), changedLbasSha256=sha(b"".join(struct.pack("<I", lba) for lba in sorted(patches))),
                inverseSha256=inverse.hexdigest(), inverseLength=restored_length))
        selected.require_original(source)
    with open(path, "rb") as final_named_source:
        selected.require_original(final_named_source)
    require_helpers()
    selected.require_helpers()
    old.require_helpers()
    print(json.dumps(dict(marker="TILED_SELECTED_MATERIAL_DISC_INDEPENDENT_ORACLE_OK", helperSha256=HELPERS,
                         originalSha256=selected.ORIGINAL, cases=results, executableSha256=sha(executable),
                         modelSha256=sha(baseline[0xDF800:0xEF000]), sceneSha256=sha(baseline[0xEF000:0xF2000]),
                         physicalHeaderSha256=sha(baseline[:0x800]), overlaySha256=sha(overlay),
                         rootSha256=sha(root), wadHeaderSha256=sha(wad), preservedN2LegacyBaseImageControlsExecuted=True,
                         originalForm1ParityControl=True, all46MusicControls=True, authenticatedSamePassPreimages=True,
                         independentBodyInverseExecuted=True, independentRawInverseExecuted=True,
                         bodyAndRawCorruptionControlsExecuted=True, referencesAwaitIndependentFreeze=True,
                         actualCSharpInverseExecuted=False, coreExecuted=False, physicalOutputWritten=False,
                         runtimeVerified=False, publicationApproved=False), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_tiled_selected_material_disc_oracle.py original.bin")
    main(sys.argv[1])
