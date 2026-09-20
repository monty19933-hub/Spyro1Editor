#!/usr/bin/env python3
"""Original-only bounded chest computed-callback source inspection.

Reports five exact JALR context windows and initializer callback stores. The
frozen Host reference is imported for constants only: no main, reconstruction,
scalar execution, callback return, Core, body, output file or emulator action.
Context is not a control-flow proof and no preceding helper return is assumed.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
BASE, EXE = 0x8007AA38, 0x8000F800
REFERENCE = "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_host_extension_oracle.py"
CONTINUATION = "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_continuation_oracle.py"
READER = "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_wizard_family_asset_oracle.py"
PINS = {
    REFERENCE: "9d96b3023ae27a6fdc1cb5d0bda07dd080142e71e192eedc07a66046e87bdb77",
    CONTINUATION: "4f407c80de5ae78642f94bf727dd113384ba3ad5baa62da308e9b4407a946b8a",
    READER: "e92cfc1a4c53611cfe11ccc87931dec71ce6b4c254c34a9d6a74bf56caef2a43",
}
SITES = ((0x80084304, 0x26100001), (0x8008431C, 0x02802821),
         (0x8008436C, 0x26100001), (0x80084394, 0x24070018), (0x80084454, 0))
PRECEDING_WORDS, FOLLOWING_WORDS = 12, 4
STATE_START, STATE_LENGTH = 0x80083D68, 0x70C
STATE_SHA = "83c507315fcf747b0c94c6bd47c7622e79f38c68ff746421fd9b2021314a479f"
TOWN_INIT, PEACE_INIT, INIT_LENGTH = 0x8005A898, 0x8005AA28, 0x98
INIT_PINS = {
    TOWN_INIT: "fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b",
    PEACE_INIT: "229c1235ff1e6860db55fb922e66eb526356d88f669e9ed41b609ea9f8af1187",
}


def sha(data): return hashlib.sha256(data).hexdigest()
def hx(value): return f"{value:08X}"
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]
def s16(value): return (value & 65535) - (65536 if value & 32768 else 0)


def require(ok, label):
    if not ok: raise AssertionError(label)


def guard_pins():
    for path, digest in PINS.items():
        require(sha((ROOT / path).read_bytes()) == digest, "Frozen source helper " + path)


def load(path, name):
    require(sha((ROOT / path).read_bytes()) == PINS[path], "Pinned definition-only import")
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module


def identity(stat):
    return stat.st_dev, stat.st_ino, stat.st_size, stat.st_mtime_ns, stat.st_ctime_ns


def callback_stores(data, entry, compressed):
    """Decode only the explicit eight LUI/ADDIU/SW callback setter records.

    This is literal instruction grammar, not a general MIPS interpreter or an
    execution/reachability claim. The separately conditional copy setter is
    outside these records and is reported independently.
    """
    require(len(data) == INIT_LENGTH, "Complete initializer owner")
    stores = []
    if compressed: require(word(data, 0x2C) == 0x3C018007, "Compressed callback common at base")
    for i in range(8):
        at = 0x30 + i * 12 if compressed else 0x10 + i * 16
        high, low = word(data, at), word(data, at + 4)
        if not compressed: require(word(data, at + 8) == 0x3C018007, "Original callback exact at-base word")
        store_at = at + (8 if compressed else 12); store = word(data, store_at)
        require(high & 0xFFFF0000 == 0x3C020000 and low & 0xFFFF0000 == 0x24420000
                and store & 0xFFFF0000 == 0xAC220000, "Exact native LUI-v0/ADDIU-v0/SW-v0-at setter grammar")
        value = (((high & 65535) << 16) + s16(low)) & 0xFFFFFFFF
        destination = (0x80070000 + s16(store)) & 0xFFFFFFFF
        require(value % 4 == 0 and BASE <= value < BASE + 0x12800
                and 0x80075000 <= destination < 0x80076000, "Bounded overlay callback and singleton address")
        stores.append(dict(index=i, setterPc=hx(entry + at), storePc=hx(entry + store_at),
                           globalAddress=hx(destination), callbackValue=hx(value),
                           words=[hx(high), hx(low), hx(store)],
                           role={0x800758CC: "moby-spawn", 0x800758E4: "particle-spawn"}.get(destination, "other-retained-callback"),
                           bodyExecuted=False))
    require(len({row["globalAddress"] for row in stores}) == 8, "Eight distinct native callback singleton stores")
    return stores


def inspect(owners, prior, reference):
    state = prior.extent(owners, "peace", STATE_START, STATE_LENGTH)
    require(sha(state) == STATE_SHA, "Whole retained chest state handler")
    actual_sites = []
    for at in range(0, len(state), 4):
        instruction = word(state, at)
        if instruction >> 26 == 0 and instruction & 63 in (8, 9):
            require(at + 8 <= len(state), "Complete computed-control delay owner")
            actual_sites.append((STATE_START + at, instruction, word(state, at + 4)))
    require(actual_sites == [(pc, 0x0040F809, delay) for pc, delay in SITES], "Exactly five recorded JALR-ra-v0 sites and delays")
    windows = []
    for pc, delay in SITES:
        start = pc - PRECEDING_WORDS * 4
        length = (PRECEDING_WORDS + 2 + FOLLOWING_WORDS) * 4
        require(STATE_START <= start and start + length <= STATE_START + STATE_LENGTH,
                "Context stays within actual complete state owner")
        data = prior.extent(owners, "peace", start, length)
        decoded = [prior.decoded(start + at, word(data, at)) for at in range(0, length, 4)]
        windows.append(dict(callPc=hx(pc), startPc=hx(start), byteLength=length, sha256=sha(data), bytesHex=data.hex(),
                            instructions=decoded, precedingWordCount=PRECEDING_WORDS, followingWordsAfterDelay=FOLLOWING_WORDS,
                            callWord="0040F809", targetRegister=2, linkRegister=31, delayWord=hx(delay),
                            returnPc=hx(pc + 8), callbackTargetProven=False, precedingHelpersExecuted=False,
                            syntacticV0Loads=[row for row in decoded if int(row["pc"], 16) < pc
                                              and row["op"] == 0x23 and row["rt"] == 2],
                            syntaxIsNotIncomingControlFlowProof=True))
    originals = {}
    for entry, digest in INIT_PINS.items():
        data = prior.extent(owners, "exe", entry, INIT_LENGTH)
        require(sha(data) == digest, "Complete actual source initializer")
        originals[entry] = data
    revised = bytes(reference.INITIALIZER)
    require(len(revised) == INIT_LENGTH and word(revised, 0x20) == 0x2442CA38,
            "Frozen450 complete new initializer constant")
    current = bytearray(revised); struct.pack_into("<I", current, 0x20, 0x2442BA38); current = bytes(current)
    require(sha(current) == reference.CURRENT_INIT_SHA, "Inverse anchor word reproduces pinned complete current initializer")
    cases = [("original-Town13", TOWN_INIT, originals[TOWN_INIT], False),
             ("original-Peace20", PEACE_INIT, originals[PEACE_INIT], False),
             ("current-Fresh65-reference", TOWN_INIT, current, True),
             ("new-detached-Fresh65-reference", TOWN_INIT, revised, True)]
    initializers = [dict(id=name, entryPc=hx(entry), byteLength=len(data), sha256=sha(data), bytesHex=data.hex(),
                         callbackStores=callback_stores(data, entry, compressed), scalarExecuted=False)
                    for name, entry, data, compressed in cases]
    values = lambda row: [(store["globalAddress"], store["callbackValue"]) for store in row["callbackStores"]]
    require(values(initializers[0]) == values(initializers[2]) == values(initializers[3]),
            "All eight current/new callback singleton values retain exact original Town values")
    for index, expected in ((0, (0x800872A4, 0x80088F68)), (1, (0x8008A258, 0x8008BFF0))):
        stores = {int(row["globalAddress"], 16): int(row["callbackValue"], 16) for row in initializers[index]["callbackStores"]}
        require((stores[0x800758CC], stores[0x800758E4]) == expected, "Previously recorded Town/Peace spawn/particle callbacks")
    return dict(marker="FRESH_CHEST_COMPUTED_CALLBACKS_INSPECTION_OK", stateOwnerSha256=STATE_SHA,
                callsiteWindows=windows, initializers=initializers, callbackValuesPreservedFromTown=True,
                currentCopyAnchor="8008BA38", detachedCopyAnchor="8008CA38", callbackBodiesExecuted=False,
                incomingCallsiteControlFlowProven=False, callbackReturnAssumed=False, fullClosureCertified=False,
                actualCurrentExecutableRead=False, currentAndNewInitializersArePinnedReferences=True,
                bodyOrSceneIssued=False, runtime=False, publication=False)


def main(path):
    require(__debug__, "Python assertions enabled"); guard_pins()
    reference = load(REFERENCE, "chest_callbacks_frozen450_constants")
    prior = load(CONTINUATION, "chest_callbacks_frozen436_definitions")
    raw = load(READER, "chest_callbacks_original_reader")
    with open(path, "rb") as image:
        initial = identity(os.fstat(image.fileno()))
        def same():
            require(identity(os.fstat(image.fileno())) == initial and identity(os.stat(path)) == initial,
                    "Held and named original identity")
        same(); raw.identity(image)
        try:
            directory = raw.logical(image, 37 * 2048, 0x800)
            require(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "Complete original WAD directory")
            for index, pair in ((15, (0x118E800, 0xF800)), (21, (0x187E000, 0x12800))):
                require(struct.unpack_from("<II", directory, index * 8) == pair, "Exact Town/Peace directory owner")
            owners = {name: raw.logical(image, at, n) for name, (at, n, _) in prior.OWNERS.items()}
            for name, (_, n, digest) in prior.OWNERS.items():
                require(len(owners[name]) == n and sha(owners[name]) == digest, "Whole original domain " + name)
            report = inspect(owners, prior, reference)
            report.update(sourceImageSha256=raw.IMAGE_SHA, helperPins=PINS,
                          originalOwners=[dict(id=name, logicalOffset=at, byteLength=n, sha256=digest)
                                          for name, (at, n, digest) in prior.OWNERS.items()])
        finally:
            raw.identity(image); same(); guard_pins()
    with open(path, "rb") as image:
        require(identity(os.fstat(image.fileno())) == initial, "Reopened named original identity")
        raw.identity(image); same()
    guard_pins(); report["sourceUnchanged"] = True
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_image"); parser.add_argument("--inspect", action="store_true", required=True)
    args = parser.parse_args(); main(args.original_image)
