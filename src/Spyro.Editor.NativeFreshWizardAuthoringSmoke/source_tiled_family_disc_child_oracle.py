#!/usr/bin/env python3
"""Independent physical tiled-family afterimage relative to the exact R07 child.

Only stdout digests/metadata are emitted. Complete raw-sector streaming and
immediate inverse return to the actual child, not Original. No output image,
production/native certificate, full Original inverse, runtime or publication.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
BODY = "src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/source_tiled_family_body_child_oracle.py"
DISC = "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py"
OLD_BODY = "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py"
HELPERS = {
    BODY: "223a05de692ab0e4f9a660d62842f1805ac4d9bc13329653b147d6888a62b06a",
    DISC: "f93780c582df180f378a4121a824d01dec6f13e3c5da68a61af571772e732608",
    OLD_BODY: "e69619f391814c270c9c28bc588f432f79f8a6cd3f61301e3a1c483a8645ad07",
}
ORIGINAL_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
ORIGINAL_CUE_SHA = "05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766"
ORIGINAL_EXE_SHA = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
CURRENT_EXE_SHA = "81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431"
HOST_SHA = "9d828474ed59e411b570e011e4b4156037e628c4cc9596cb476e7e2abdb995b2"
RAW, USER, IMAGE_LENGTH, SECTORS = 2352, 2048, 661547040, 281270
# Frozen after independent raw-sector construction and complete streaming.
# --derive reports the same fixed reference and cannot bypass these pins.
PINS = {
    "bodyLba": 53911, "bodySectorCount": 1054, "dataLba": 54371, "dataSectorCount": 2,
    "changedSectors": [
        {"lba": 54371, "bodyOffset": 942080, "rawOffset": 127880592,
         "beforeSha256": "05f8009bef60a28f435f9c0ec4ddddd3d32391058db29318d46d3f0a9b6421cf",
         "afterSha256": "b147d5a91c541fc64a09e62db2a739c7309d899fd6742da58a433af6bbdae115",
         "beforePayloadSha256": "50d7aa3ad1fbe70ecde8912f9af3e56d2e1df3b75a580c7d3c1624f89a4ca76b",
         "afterPayloadSha256": "2dc528137dbc953a544dac984e2b4e571575d0a4d6e597227dd68aad2d05e1d9",
         "changedRawBytes": 1230, "changedPayloadBytes": 952, "changedEdcBytes": 4,
         "changedPBytes": 170, "changedQBytes": 104, "firstRawDifference": 400, "lastRawDifference": 2351},
        {"lba": 54372, "bodyOffset": 944128, "rawOffset": 127882944,
         "beforeSha256": "36cf637241fa61be1eee31e50ef7c917946a4c26296c3c518f5ea0570b8fa7cd",
         "afterSha256": "e73673c35631c4cbedee94f07f991360bf1965948af6d69d28eb8231a82db3f0",
         "beforePayloadSha256": "56b59267207b6aca7bc8c4aa8753720572aec608c9e2afc81e8eab6a48a3d39f",
         "afterPayloadSha256": "37cfb6e06150a317f9167803cad9c82e9b885c7bba4bb6f2c46f9f3bf8e500fb",
         "changedRawBytes": 739, "changedPayloadBytes": 461, "changedEdcBytes": 4,
         "changedPBytes": 171, "changedQBytes": 103, "firstRawDifference": 24, "lastRawDifference": 2351},
    ],
    "logicalChangedBytes": 1413,
    "oldBodySha256": "2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504",
    "newBodySha256": "c8c85e71b633e29ce573dddccb04a4a0adcd6696d292658304328e435dfaa9ca",
    "unchangedBodyRawComplementSha256": "01680bad3d732938779cdea109650d0b32b4f874b0a86e429e5fdc57abe573b7",
    "oldBodyRawParityReproduced": True, "completeOutsideDataRawBodyComplementUnchanged": True,
    "candidateByteLength": IMAGE_LENGTH,
    "candidateSha256": "689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192",
    "childSha256": "a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50",
    "immediateInverseChildSha256": "a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50",
    "changedRawSectorCount": 2, "changedLbas": [54371, 54372], "changedRawByteCount": 1969,
    "firstChangedRawOffset": 127880992, "lastChangedRawOffset": 127885295,
    "entireUnchangedRawComplementSha256": "cc1b83a8734a513dad71a219a81e57daa988039e1468f68da6bcb0fe6784c8c0",
    "completePhysicalComplementVerified": True, "completeImmediateInverseVerified": True,
    "negativeControlCount": 111,
}


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def load(relative, digest, name):
    path = ROOT / relative
    require(sha(path.read_bytes()) == digest, "Pinned definition immediately before import: " + relative)
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    previous = sys.argv
    try:
        sys.argv = [str(path)]
        with contextlib.redirect_stdout(io.StringIO()):
            spec.loader.exec_module(module)
    finally:
        sys.argv = previous
    return module


def add_manifest(manifest, entries):
    for path, digest in entries.items():
        require(path not in manifest or manifest[path] == digest, "Consistent transitive definition pin")
        manifest[path] = digest


def definition_checks(before, manifest):
    checks = [(Path(__file__), sha(before))] + [(ROOT / path, digest) for path, digest in manifest.items()]
    errors = []
    for path, digest in checks:
        try:
            require(sha(path.read_bytes()) == digest, "Complete retained definition: " + str(path))
        except BaseException as error:
            errors.append(error)
    if errors:
        raise BaseExceptionGroup("All own and helper definition checks", errors)


def load_parity(manifest):
    # The frozen Form1 module imports this sibling by ordinary module name.
    # Supply the exact independently pinned module; never trust an existing
    # sys.modules alias or change the frozen helper's imports/source.
    imported = load(OLD_BODY, manifest[OLD_BODY], "tiled_disc_old_body_import")
    previous = sys.modules.get("source_fresh_disc_body")
    try:
        sys.modules["source_fresh_disc_body"] = imported
        parity = load(DISC, manifest[DISC], "tiled_disc_form1")
        require(parity.build_bodies is imported.build_bodies,
                "Frozen Form1 transitive sibling import is the exact pinned definition")
    finally:
        if previous is None:
            sys.modules.pop("source_fresh_disc_body", None)
        else:
            sys.modules["source_fresh_disc_body"] = previous
    # build_bodies and compose_exe are deliberately not called: this task has
    # no independent complete Original-to-current physical ownership plan.
    return parity


class IndexedImage:
    """Complete same-pass raw digests bind every subsequent physical read."""
    def __init__(self, stream, digest):
        self.stream, self.index = stream, bytearray(SECTORS * 32)
        stream.seek(0)
        complete = hashlib.sha256()
        for lba in range(SECTORS):
            raw = stream.read(RAW)
            require(len(raw) == RAW, "Complete source raw-sector index extent")
            self.index[lba * 32:(lba + 1) * 32] = hashlib.sha256(raw).digest()
            complete.update(raw)
        require(stream.tell() == IMAGE_LENGTH and stream.read(1) == b"" and complete.hexdigest() == digest,
                "Same-pass full raw source SHA and true EOF")
        self.digest = digest

    def authenticate_raw(self, lba, raw):
        require(type(lba) is int and 0 <= lba < SECTORS and type(raw) is bytes and len(raw) == RAW,
                "Complete bounded physical raw-sector owner")
        require(hashlib.sha256(raw).digest() == self.index[lba * 32:(lba + 1) * 32],
                "Actual same-pass complete raw preimage")

    def raw(self, lba):
        require(type(lba) is int and 0 <= lba < SECTORS, "Bounded source LBA")
        self.stream.seek(lba * RAW)
        raw = self.stream.read(RAW)
        self.authenticate_raw(lba, raw)
        return raw

    def logical(self, stream, at, length):
        require(stream is self.stream and type(at) is int and type(length) is int and
                0 <= at and 0 < length <= 0x300000 and at + length <= SECTORS * USER,
                "Bounded same-owner logical extent")
        output = bytearray()
        while len(output) < length:
            lba, inside = divmod(at + len(output), USER)
            raw = self.raw(lba)
            require(raw[:12] == b"\0" + b"\xff" * 10 + b"\0" and raw[15] == 2 and
                    raw[16:20] == raw[20:24], "Exact complete Mode2 physical read")
            take = min(length - len(output), USER - inside)
            output.extend(raw[24 + inside:24 + inside + take])
        return bytes(output)

    def all_raw(self):
        self.stream.seek(0)
        for lba in range(SECTORS):
            raw = self.stream.read(RAW)
            self.authenticate_raw(lba, raw)
            yield lba, raw
        require(self.stream.tell() == IMAGE_LENGTH and self.stream.read(1) == b"",
                "Complete physical stream ends at true EOF")


def sector_header(lba, raw):
    require(type(lba) is int and 0 <= lba < SECTORS and type(raw) is bytes and len(raw) == RAW,
            "Complete bounded Form1 raw sector")
    absolute = lba + 150
    bcd = lambda n: (n // 10) * 16 + n % 10
    msf = bytes((bcd(absolute // 4500), bcd((absolute // 75) % 60), bcd(absolute % 75), 2))
    require(raw[:12] == b"\0" + b"\xff" * 10 + b"\0" and raw[12:16] == msf and
            raw[16:20] == raw[20:24] and raw[16] == raw[17] == raw[19] == 0 and raw[18] in (8, 0x89),
            "Exact physical destination MSF/mode and duplicated XA header")


def validate_form1(lba, raw, parity):
    sector_header(lba, raw)
    require(parity.form1(raw, raw[24:2072], raw[18] & 0x81) == raw,
            "Complete independently reproduced Form1 EDC/P/Q parity")


class ChildTransition:
    """Two physical owner replacements derived from complete old/new Bodies."""
    def __init__(self, image, old_body, new_body, extraction, body_reference, parity):
        require(type(old_body) is bytes and type(new_body) is bytes and
                sha(old_body) == body_reference.OLD_BODY_SHA and
                sha(new_body) == body_reference.PINS["newBodySha256"] and len(old_body) == len(new_body) == 0x20F000,
                "Exact complete independent old/new Body owners")
        descriptors = body_reference.descriptors(old_body)
        require(body_reference.descriptors(new_body) == descriptors, "Unchanged actual Body descriptors")
        data_at, data_length = descriptors[1]
        require(old_body[:data_at] == new_body[:data_at] and
                old_body[data_at + data_length:] == new_body[data_at + data_length:],
                "Complete Body complement outside the exact data allocation")
        logical_start = extraction["wadLba"] * USER + extraction["bodyWadOffset"]
        require(logical_start % USER == data_at % USER == data_length % USER == 0 and
                data_at == 0xE6000 and data_length == 0x1000 and
                extraction["bodyByteLength"] == len(old_body), "Exact aligned child-relative Body/data physical extent")
        self.image, self.patches, self.old_body, self.new_body = image, {}, old_body, new_body
        self.body_lba, self.body_sectors = logical_start // USER, len(old_body) // USER
        self.data_lba, self.data_sectors = (logical_start + data_at) // USER, data_length // USER
        self.logical_changes = [offset for offset, (old, new) in enumerate(zip(old_body, new_body)) if old != new]
        require(len(self.logical_changes) == body_reference.PINS["changedByteCount"] == 1413 and
                self.logical_changes[0] == body_reference.PINS["firstChangedBodyOffset"] and
                self.logical_changes[-1] == body_reference.PINS["lastChangedBodyOffset"],
                "Every logical Body byte difference matches independent complete Body reference")
        sector_changes = set(offset // USER for offset in self.logical_changes)
        require(sector_changes == set(range(data_at // USER, (data_at + data_length) // USER)),
                "Entire changed-sector set independently derives exactly the two data sectors")
        unchanged_before, unchanged_after = hashlib.sha256(), hashlib.sha256()
        sector_reports = []
        for ordinal in range(self.body_sectors):
            lba, at = self.body_lba + ordinal, ordinal * USER
            before = image.raw(lba)
            validate_form1(lba, before, parity)
            require(before[24:2072] == old_body[at:at + USER], "Every actual physical Body payload equals old Body")
            after = parity.form1(before, new_body[at:at + USER], before[18] & 0x81)
            validate_form1(lba, after, parity)
            require(after[:24] == before[:24], "Every raw sync/MSF/mode/XA header remains unchanged")
            if ordinal in sector_changes:
                require(before != after, "Every independently derived changed payload changes raw sector")
                self.patches[lba] = (before, after)
                diff = [offset for offset, (left, right) in enumerate(zip(before, after)) if left != right]
                require(all(24 <= offset < RAW for offset in diff), "Only payload and its EDC/ECC bytes change")
                sector_reports.append(dict(lba=lba, bodyOffset=at, rawOffset=lba * RAW,
                                           beforeSha256=sha(before), afterSha256=sha(after),
                                           beforePayloadSha256=sha(before[24:2072]), afterPayloadSha256=sha(after[24:2072]),
                                           changedRawBytes=len(diff), changedPayloadBytes=sum(offset < 2072 for offset in diff),
                                           changedEdcBytes=sum(2072 <= offset < 2076 for offset in diff),
                                           changedPBytes=sum(2076 <= offset < 2248 for offset in diff),
                                           changedQBytes=sum(2248 <= offset for offset in diff),
                                           firstRawDifference=diff[0], lastRawDifference=diff[-1]))
            else:
                require(after == before, "Complete raw-sector Body complement including all source parity")
                unchanged_before.update(before); unchanged_after.update(after)
        require(len(self.patches) == 2 and set(self.patches) == set(range(self.data_lba, self.data_lba + self.data_sectors)) and
                unchanged_before.digest() == unchanged_after.digest(), "Exact physical Body partition/complement")
        self.report = dict(bodyLba=self.body_lba, bodySectorCount=self.body_sectors, dataLba=self.data_lba,
                           dataSectorCount=self.data_sectors, changedSectors=sector_reports,
                           logicalChangedBytes=len(self.logical_changes), oldBodySha256=sha(old_body),
                           newBodySha256=sha(new_body), unchangedBodyRawComplementSha256=unchanged_before.hexdigest(),
                           oldBodyRawParityReproduced=True, completeOutsideDataRawBodyComplementUnchanged=True)

    def forward(self, lba, before):
        self.image.authenticate_raw(lba, before)
        if lba in self.patches:
            old, new = self.patches[lba]
            require(before == old, "Exact complete forward raw preimage")
            return new
        return before

    def inverse(self, lba, supplied):
        require(type(lba) is int and 0 <= lba < SECTORS and type(supplied) is bytes and len(supplied) == RAW,
                "Complete bounded inverse raw sector")
        if lba in self.patches:
            before, after = self.patches[lba]
            require(supplied == after, "Exact complete inverse afterimage")
            restored = before
        else:
            self.image.authenticate_raw(lba, supplied)
            restored = supplied
        self.image.authenticate_raw(lba, restored)
        return restored

    def stream(self, byte_length=IMAGE_LENGTH):
        require(type(byte_length) is int and byte_length == IMAGE_LENGTH == RAW * SECTORS,
                "Exact complete virtual BIN length")
        before_hash, after_hash, inverse_hash = hashlib.sha256(), hashlib.sha256(), hashlib.sha256()
        outside_before, outside_after = hashlib.sha256(), hashlib.sha256()
        changed, differences, first, last, counted = [], 0, None, None, 0
        for lba, before in self.image.all_raw():
            after = self.forward(lba, before)
            restored = self.inverse(lba, after)
            require(restored == before, "Actual per-sector immediate inverse equals authenticated child preimage")
            before_hash.update(before); after_hash.update(after); inverse_hash.update(restored)
            counted += len(after)
            if after != before:
                changed.append(lba)
                offsets = [at for at, pair in enumerate(zip(before, after)) if pair[0] != pair[1]]
                differences += len(offsets)
                first = lba * RAW + offsets[0] if first is None else first
                last = lba * RAW + offsets[-1]
            else:
                outside_before.update(before); outside_after.update(after)
        require(counted == IMAGE_LENGTH and before_hash.hexdigest() == inverse_hash.hexdigest() == self.image.digest and
                changed == sorted(self.patches) and outside_before.digest() == outside_after.digest(),
                "Complete streamed child/candidate/immediate-inverse and physical complement")
        return dict(candidateByteLength=counted, candidateSha256=after_hash.hexdigest(),
                    childSha256=before_hash.hexdigest(), immediateInverseChildSha256=inverse_hash.hexdigest(),
                    changedRawSectorCount=len(changed), changedLbas=changed, changedRawByteCount=differences,
                    firstChangedRawOffset=first, lastChangedRawOffset=last,
                    entireUnchangedRawComplementSha256=outside_before.hexdigest(),
                    completePhysicalComplementVerified=True, completeImmediateInverseVerified=True)


def negative_controls(transition, parity, body_reference, extraction):
    refused = []

    def reject(label, action, phrase):
        try:
            action()
        except AssertionError as error:
            require(phrase in str(error), label + ": unexpected refusal: " + str(error))
            refused.append(label)
            return
        raise AssertionError("Accepted negative control: " + label)

    for lba, (before, after) in transition.patches.items():
        for at in (0, 12, 15, 16, 18, 20, 24, 2071, 2072, 2075, 2076, 2247, 2248, 2351):
            bad = bytearray(after); bad[at] ^= 1
            reject("inverse-changed-sector-" + str(lba) + "-" + hex(at),
                   lambda bad=bad, lba=lba: transition.inverse(lba, bytes(bad)), "inverse afterimage")
        for label, bad in (("old-preimage", before), ("truncated", after[:-1]),
                           ("appended", after + bytes(1))):
            reject("inverse-" + label + "-" + str(lba), lambda bad=bad, lba=lba: transition.inverse(lba, bad),
                   "inverse afterimage" if label == "old-preimage" else "bounded inverse raw sector")
        for at in (0, 12, 18, 20):
            bad = bytearray(after); bad[at] ^= 1
            reject("physical-header-" + str(lba) + "-" + hex(at),
                   lambda bad=bad, lba=lba: validate_form1(lba, bytes(bad), parity), "destination MSF/mode")
        for at in (24, 2072, 2076, 2248, 2351):
            bad = bytearray(after); bad[at] ^= 1
            reject("physical-parity-" + str(lba) + "-" + hex(at),
                   lambda bad=bad, lba=lba: validate_form1(lba, bytes(bad), parity), "Form1 EDC/P/Q parity")
        for at in (12, 24, 2072, 2351):
            bad = bytearray(before); bad[at] ^= 1
            reject("forward-foreign-preimage-" + str(lba) + "-" + hex(at),
                   lambda bad=bad, lba=lba: transition.forward(lba, bytes(bad)), "same-pass complete raw preimage")
    # Neighbors cover old data boundary/model boundary. Metadata, complete Host
    # and executable sectors are also immutable physical complement owners.
    probes = (transition.data_lba - 1, transition.data_lba + 2, 16, 22, 37,
              extraction["wadLba"] + extraction["hostWadOffset"] // USER,
              extraction["wadLba"] + extraction["wadByteLength"] // USER,
              transition.body_lba, transition.body_lba + transition.body_sectors - 1, SECTORS - 1)
    for lba in probes:
        raw = transition.image.raw(lba)
        for at in (12, 24, 2072, 2351):
            bad = bytearray(raw); bad[at] ^= 1
            reject("inverse-physical-complement-" + str(lba) + "-" + hex(at),
                   lambda bad=bad, lba=lba: transition.inverse(lba, bytes(bad)), "same-pass complete raw preimage")
    for length in (IMAGE_LENGTH - 1, IMAGE_LENGTH + 1, IMAGE_LENGTH - RAW, IMAGE_LENGTH + RAW):
        reject("wrong-complete-bin-length-" + str(length), lambda length=length: transition.stream(length),
               "complete virtual BIN length")
    for lba in (-1, SECTORS):
        reject("inverse-outside-disc-" + str(lba), lambda lba=lba: transition.inverse(lba, bytes(RAW)),
               "bounded inverse raw sector")
    for at in (0, 0x7FF, 0xE5FFF, 0xE7000, len(transition.new_body) - 1):
        bad = bytearray(transition.new_body); bad[at] ^= 1
        reject("foreign-new-body-complement-" + hex(at), lambda bad=bad:
               ChildTransition(transition.image, transition.old_body, bytes(bad), extraction, body_reference, parity),
               "complete independent old/new Body")
    return refused


def main(argv=None):
    args = list(sys.argv[1:] if argv is None else argv)
    require(__debug__ and len(args) == 5 and args[0] in ("--derive", "--child-reference"),
            "Usage: --derive/--child-reference absolute-Original.bin Original.cue exact-child.bin child.cue")
    mode, *names = args
    paths = [Path(name) for name in names]
    require(all(path.is_absolute() for path in paths) and paths[0].parent == paths[1].parent and
            paths[2].parent == paths[3].parent and [path.name for path in paths] ==
            ["Spyro the Dragon (USA).bin", "Spyro the Dragon (USA).cue",
             "native-fresh-saved-level-r01.bin", "native-fresh-saved-level-r01.cue"],
            "Only explicit absolute exact paired input names; no output path")
    before, manifest = Path(__file__).read_bytes(), dict(HELPERS)
    held, failures, report = [], [], None
    with contextlib.ExitStack() as stack:
        try:
            body_reference = load(BODY, manifest[BODY], "tiled_disc_body_reference")
            add_manifest(manifest, body_reference.HELPERS)
            child = load(body_reference.CHILD, manifest[body_reference.CHILD], "tiled_disc_child")
            neutral = load(body_reference.NEUTRAL, manifest[body_reference.NEUTRAL], "tiled_disc_neutral")
            add_manifest(manifest, child.HELPERS)
            add_manifest(manifest, neutral.HELPERS)
            parity = load_parity(manifest)
            definition_checks(before, manifest)
            specs = ((IMAGE_LENGTH, ORIGINAL_SHA), (88, ORIGINAL_CUE_SHA),
                     (IMAGE_LENGTH, child.IMAGE_SHA), (len(child.CUE_BYTES), sha(child.CUE_BYTES)))
            for path, (length, digest) in zip(paths, specs):
                stream = stack.enter_context(path.open("rb"))
                initial = child.identity(os.fstat(stream.fileno()))
                held.append((stream, path, initial, length, digest))
                child.authenticate(stream, path, initial, length, digest)
            original = IndexedImage(held[0][0], ORIGINAL_SHA)
            image = IndexedImage(held[2][0], child.IMAGE_SHA)
            for lba in (22, 37, 53874, 53875, 54078):
                validate_form1(lba, original.raw(lba), parity)
            original_exe = original.logical(held[0][0], 53875 * USER, 0x66000)
            require(sha(original_exe) == ORIGINAL_EXE_SHA, "Exact complete Original executable owner")
            allocated, extraction = child.extract(held[2][0], image)
            neutral.preserved_control()
            neutral.validate_document(neutral.CANONICAL)
            data_report = child.reference(allocated, neutral)
            require(all(data_report[key] == value for key, value in child.PINS.items()),
                    "Unchanged complete independent child-relative data reference")
            data, _, _ = child.compose(allocated, neutral)
            old_body = image.logical(held[2][0], extraction["wadLba"] * USER + extraction["bodyWadOffset"],
                                     extraction["bodyByteLength"])
            new_body = body_reference.assemble(old_body, data)
            body_report = body_reference.reference(old_body, data, child)
            require(all(body_report[key] == value for key, value in body_reference.PINS.items()),
                    "Complete independent Body reference and all47 controls, no native certificate inherited")
            host = image.logical(held[2][0], extraction["wadLba"] * USER + extraction["hostWadOffset"],
                                 extraction["hostByteLength"])
            exe = image.logical(held[2][0], (extraction["wadLba"] + extraction["wadByteLength"] // USER) * USER, 0x66000)
            require(len(host) == 0x12000 and sha(host) == HOST_SHA and sha(exe) == CURRENT_EXE_SHA,
                    "Complete unchanged actual R07 Host and executable owners")
            transition = ChildTransition(image, old_body, new_body, extraction, body_reference, parity)
            physical = transition.stream()
            controls = negative_controls(transition, parity, body_reference, extraction)
            case = dict(**transition.report, **physical, negativeControlCount=len(controls))
            require(case == PINS, "Complete frozen independently streamed physical child reference")
            report = dict(marker="TILED_FAMILY_DISC_CHILD_REFERENCE_OK" if mode == "--child-reference" else
                          "TILED_FAMILY_DISC_CHILD_REFERENCE_MEASURED_UNFROZEN", oracleSha256=sha(before),
                          helperPins=manifest, case=case, negativeControls=controls,
                          originalSha256=ORIGINAL_SHA, originalCueSha256=ORIGINAL_CUE_SHA,
                          childCueSha256=sha(child.CUE_BYTES), originalExecutableSha256=sha(original_exe),
                          retainedExecutableSha256=sha(exe), retainedHostSha256=sha(host),
                          bodyReferenceSha256=body_reference.PINS["newBodySha256"], preservedBodyRefusals=47,
                          originalAndChildSamePassRawIndexes=True, originalForm1ParityReproduced=True,
                          completeChildRelativePhysicalReference=True, sourceOnlyCandidate=True,
                          physicalCandidateWritten=False, fullOriginalInverseProven=False,
                          completeOriginalAssetClosureProven=False, nativeCertificateInherited=False,
                          nativeInstructionsExecuted=False, sourceCertified=False, bodyIssued=False,
                          discIssued=False, runtimeVerified=False, publicationApproved=False)
        except BaseException as error:
            failures.append(error)
        finally:
            for stream, path, initial, length, digest in held:
                for reopen in (False, True):
                    try:
                        if reopen:
                            with path.open("rb") as reopened:
                                child.authenticate(reopened, path, initial, length, digest)
                        else:
                            child.authenticate(stream, path, initial, length, digest)
                    except BaseException as error:
                        failures.append(error)
            try:
                definition_checks(before, manifest)
            except BaseException as error:
                failures.append(error)
            if failures:
                raise BaseExceptionGroup("Final exact Original/child BIN/CUE and all definitions", failures)
    report["completeBeforeAfterReopenedFourFileGuards"] = True
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
