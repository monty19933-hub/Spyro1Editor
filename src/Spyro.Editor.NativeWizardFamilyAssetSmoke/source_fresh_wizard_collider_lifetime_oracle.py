#!/usr/bin/env python3
"""Private exact-source integer slices with explicit synthetic entry witnesses.

No GTE, native helper substitute, body, candidate, RAM or emulator input. The
miss/return witnesses are not a collision, allocation-peak or attribution proof.
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

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPERS = {
    "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_wizard_family_asset_oracle.py": "e92cfc1a4c53611cfe11ccc87931dec71ce6b4c254c34a9d6a74bf56caef2a43",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py": "75f6762822c79520176bb3127e9b426822fb7b007b2ba6da931e4822cb43f283",
}
PINS = (
    ("allocator-and-free", 0x800524C4, 0x1E4, "768e4181d81333e0e27bbe6419e8d94114096a9bd8e8556314af5f0e628327ff"),
    ("model-selection-grid-insertion", 0x800526A8, 0xB0, "6957574246c153803d760eb6e0001529b8016eaec09da84f94aa893cc5560c21"),
    ("animation-collider-update", 0x800529E4, 0x80, "ec41ef500fe7a57b343c8f0bbaf3dc6d1c96d500b2a0bbdf06092e33c784ce73"),
    ("grid-move-before-GTE", 0x80052A64, 0x120, "48cae4cca772044e190630f1e759122c1d4e2cfa5eda506b3cc1e4988a6bad23"),
    ("grid-and-null-delay", 0x8004E500, 0xB8, "e3519006e49a0a832d3733a451536c74ff91b85362a2156521cf3b4b783d3b3b"),
    ("type3-post-GTE-miss", 0x8004E950, 8, "8e9b1f663ffb81c19c6a639841ab6a39fb7c0278bc1af65dd31256ac52288caf"),
    ("complete-sphere-helper-not-executed-through-GTE", 0x8004E2E8, 0xE0, "616f1e7f8fed79452a3ec0793db251866c9127bf2e76f66dd634ed02f89163f3"),
    ("spatial-return-pointer-and-restore", 0x8004EB58, 0x58, "519689194c66007f0a65762cc4d1959bc85686e4ae9d659388fb23fcabf6f8f4"),
    ("player-spatial-return-and-receiver", 0x800407F0, 0x260, "929c6233e52986188e58ab977de730d62bae9095e58459e611abb277a55077fc"),
    ("native-startup-gp", 0x8005B95C, 8, "87aee6de9ebb5229353f2dc7d635e2c86348d8ccac653a746a872c085ee8e9df"),
    ("contact-pointer-lowbits-consumer", 0x80041270, 0x104, "d365028f1a3287f3dc529ebca6ce086c2e9a4d73bc6a5c43970e4c819e2b95d7"),
    ("contact-pointer-state-consumer", 0x80044450, 0x80, "4b5bfc84a9a901f59aeaaf21b2978067a4362e95074a97bd2c3c507ca56087c4"),
)
EXE, MODEL, ROW, GRID = 0x8000F800, 0x80100000, 0x80140000, 0x80180000
SPYRO, FLAGS, GP, CONTACT, STACK, RETURN = 0x80078A58, 0x80078A84, 0x80075264, 0x80075804, 0x801F0000, 0xDEADBEE0


def require(ok, label):
    if not ok: raise AssertionError(label)
def sha(b): return hashlib.sha256(b).hexdigest()
def word(b, at=0): return struct.unpack_from("<I", b, at)[0]
def hx(v): return f"{v:08X}"


def load(relative, expected, name):
    path = ROOT / relative
    require(sha(path.read_bytes()) == expected, "Frozen helper " + relative)
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    with contextlib.redirect_stdout(io.StringIO()): spec.loader.exec_module(module)
    return module


def main(path, naive):
    raw, scalar = [load(p, digest, "lifetime_" + str(i)) for i, (p, digest) in enumerate(HELPERS.items())]
    checks, refusals, reads, executed = [], [], [], set()
    def check(ok, label): require(ok, label); checks.append(label)
    def refuse(action, phrase, label):
        try: action()
        except AssertionError as error:
            require(phrase in str(error), "Wrong refusal: " + str(error))
            refusals.append(dict(label=label, error=str(error))); return
        raise AssertionError("Expected ownership refusal: " + label)

    with open(path, "rb") as image:
        raw.identity(image)
        try:
            source = {key: raw.logical(image, *value[:2]) for key, value in raw.SOURCE.items()}
            for key, value in raw.SOURCE.items(): check(sha(source[key]) == value[2], "Complete original owner " + key)
            exe, donor = source["exe"], source["donor"]
            code = {EXE + p: word(exe, p) for p in range(0, len(exe), 4)}
            bolt_overlay = raw.logical(image, 37 * 2048 + 0x2F5B000, 0x10000)
            check(sha(bolt_overlay) == "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1", "Complete original Wizard code owner")
            bolt_main = raw.extent(bolt_overlay, 0x8007EAD0 - 0x8007AA38, 0x1A0)
            check(sha(bolt_main) == "1c0de3f70b36876cbf526730df0219c206f9e5d5621551bd8abb062c83609fe5", "Complete source lightning-main fragment")
            code.update((0x8007EAD0+p, word(bolt_main,p)) for p in range(0,len(bolt_main),4))
            for name, pc, length, digest in PINS:
                check(sha(exe[pc - EXE:pc - EXE + length]) == digest, "Exact native pin " + name)
            wizard = raw.extent(donor, 0x19B518, 0x35B4)
            roots = raw.roots(donor)
            check(roots[0x11B] == (0x19B518, 0x35B4) and roots[0x11D] == (0x19EACC, 0x8918), "Exact adjacent distinct model owners")
            next_word = raw.extent(donor, 0x19EACC, 4)
            check(next_word == bytes.fromhex("08000000"), "Actual next class11D animation count8 lookahead")

            class Trace(scalar.Machine):
                def read(self, address, size=4):
                    reads.append((address, size)); return super().read(address, size)
                def run(self, entry, stops, maximum=2000):
                    result = super().run(entry, stops, maximum)
                    executed.update(self.executed); return result

            def miss(adjacent=False):
                m = Trace(code); m.blob(MODEL, wizard); m.map(ROW, 0x58)
                # Selected post-GTE miss witness: t7>=0, final source command,
                # at points at vertex+flag, fp is the explicit current grid row.
                m.r[15], m.r[2], m.r[1], m.r[30] = 0, 0x01900301, MODEL + 0x35AC, ROW
                if adjacent: m.blob(MODEL + len(wizard), next_word)
                m.run(0x8004E950, {0x8004E51C})
                return m

            if naive:
                miss()  # Intentional RED: package ownership alone is insufficient.
                raise AssertionError("Naive package-only read bound unexpectedly passed")
            refuse(miss, "Unmapped native read", "Final-command delay needs four bytes outside Wizard source package")
            before = len(reads); m = miss(True)
            check((MODEL + 0x35B4, 4) in reads[before:] and m.r[2] == 8 and m.r[30] == 0,
                  "Actual delay LW reads adjacent class11D before next-chain stop; no extra command dispatched")
            def null_descriptor():
                m = Trace(code); m.map(ROW, 0x58); m.r[30], m.r[1] = ROW, 0
                m.run(0x8004E554, {0x8004E51C})
            refuse(null_descriptor, "Unmapped native read 00000000", "Null-descriptor branch still reads address0 halfword")
            def null_chain():
                m = Trace(code); m.r[30] = 0
                m.run(0x8004E51C, {0x8004E504})
            refuse(null_chain, "Unmapped native read 0000000C", "Null-chain branch still reads low RAM word0C")

            def fixture():
                m = Trace(code); m.blob(MODEL, wizard); m.map(ROW, 0x108); m.map(GRID, 0x2000)
                m.map(0x80076378 + 0x11B * 4, 4); m.poke(0x80076378 + 0x11B * 4, MODEL)
                # Explicit post-relocation witness; this does not execute the
                # generic loader. Only source slot0 and animation0 are rebased.
                m.poke(MODEL + 0x14, MODEL + 0x35A4); m.poke(MODEL + 0x38, MODEL + word(wizard, 0x38))
                m.map(0x80075778, 4); m.poke(0x80075778, GRID)
                m.poke(ROW + 0x36, 0x11B, 2); m.poke(ROW + 0xC, 0x4000); m.poke(ROW + 0x10, 0x4000)
                m.poke(GRID + 66 * 4, ROW + 0x58); m.r[4] = ROW
                m.run(0x800526A8, {0x80052758})  # Stop before any GTE work.
                return m
            m = fixture()
            check(m.read(ROW + 8) == MODEL + 0x35A4 and m.read(ROW + 0x34, 2) == 66 and
                  m.read(GRID + 66 * 4) == ROW and m.read(ROW + 4) == ROW + 0x58,
                  "Actual initializer selects source slot0 and prepends row to native grid")
            m.poke(ROW + 8, 0); m.r[4], m.r[5] = ROW, 1
            m.run(0x800529E4, {0x80052A64})
            check(m.read(ROW + 8) == MODEL + 0x35A4, "Actual animation update restores selected pointer, not an immutable row field")
            m.poke(ROW + 0xC, 0x6000); m.poke(GRID + 67 * 4, ROW + 0xB0); m.r[4], m.r[5] = ROW, 2
            m.run(0x80052A64, {0x80052AEC})
            check(m.read(GRID + 66 * 4) == ROW + 0x58 and m.read(GRID + 67 * 4) == ROW and
                  m.read(ROW + 4) == ROW + 0xB0 and m.read(ROW + 0x34, 2) == 67,
                  "Actual move unlinks old grid and prepends new grid; next/index change")
            # Actual placed-row delete branch (not a dynamic-allocation test).
            m.map(0x80075890, 4); m.poke(0x80075890, ROW + 0x108); m.r[4], m.r[31] = ROW, RETURN
            old_descriptor, old_next = m.read(ROW + 8), m.read(ROW + 4)
            m.run(0x80052568, {RETURN})
            check(m.read(GRID + 67 * 4) == ROW + 0xB0 and m.read(ROW + 0x48, 1) == 0xFD,
                  "Actual placed free removes grid link and marksFD")
            check(m.read(ROW + 8) == old_descriptor and m.read(ROW + 4) == old_next and m.read(ROW + 0x34, 2) == 67,
                  "Free does not clear descriptor/next/index; stale row bytes are not live ownership")

            # Actual allocator with a synthetic valid tail row/property owner.
            m = Trace(code); m.map(ROW, 0x100, 0xA5); m.map(0x80160000, 0x40)
            for at, value in ((0x800756A4,0),(0x8007573C,ROW),(0x80075930,0x80160040)):
                m.map(at,4); m.poke(at,value)
            m.poke(ROW+0x48,0xFF,1); m.poke(0x8016003F,0xFF,1); m.r[31] = RETURN
            m.run(0x800524C4, {RETURN})
            check(m.r[2] == ROW and m.read(ROW+8) == 0xA5A5A5A5 and m.read(ROW+0x36,2) == 0xA5A5,
                  "Actual allocation returns row without clearing stale collider/class bytes")

            # Selected return witnesses, not calls auto-returned by a helper.
            for returned in (0, ROW, ROW+0x58):
                m = Trace(code)
                for at,value in ((0x8007584C,100),(0x800757D0,12),(CONTACT,0x12345678)):
                    m.map(at,4); m.poke(at,value)
                m.map(STACK,0x80); m.map(SPYRO,0x100)
                m.r[2],m.r[28],m.r[29],m.r[18],m.r[22],m.r[20] = returned,GP,STACK,SPYRO,SPYRO+0x88,1
                m.run(0x80040864, {0x8004097C,0x8001778C})
                check(m.read(CONTACT) == (returned or 0x12345678), "Spatial return stores nonzero contact only; null return preserves old pointer")
            # Distinct sphere events collapse to the same flags and never write
            # CONTACT. Starting after GTE is explicit, not a proven bolt hit.
            for source_pointer in (ROW,ROW+0x58):
                m = Trace(code); m.map(FLAGS,4); m.map(CONTACT,4); m.poke(CONTACT,0x12345678)
                m.r[1],m.r[4],m.r[6],m.r[9],m.r[31] = 0,source_pointer,0x20020,SPYRO,RETURN
                m.run(0x8004E3A0, {RETURN})
                check(m.r[2] == 1 and m.read(FLAGS) == 0x20020 and m.read(CONTACT) == 0x12345678 and
                      m.writes == [(FLAGS,4,0x20020)], "Sphere success suffix has one flags write/bool only for each distinct input identity")
            m = Trace(code); m.r[19],m.r[29] = ROW,STACK
            m.run(0x8007EB34, {0x80017700})
            check(m.r[16] == ROW+0xC and m.r[5] == ROW+0xC and m.r[4] == STACK+0x68,
                  "Actual bolt state2 setup derives source position from current row; vector helper boundary only")
            m = Trace(code); m.r[16] = ROW+0xC
            m.run(0x8007EB80, {0x8004E2E8})
            check((m.r[4],m.r[5],m.r[6],m.r[31]) == (ROW+0xC,0x80,0x20020,0x8007EB94),
                  "Actual bolt sphere call and retained ORI delay arguments")
            for returned,target in ((0,0x8007EBFC),(1,0x8006272C)):
                m = Trace(code); m.r[2],m.r[17] = returned,0x80160000
                m.run(0x8007EB94, {target})
                check(m.r[4] == 0x80160000 and m.r[17] == (0 if returned else 0x80160000),
                      "Actual bool consumer selects miss continuation or native RNG call; no helper body executed")
            for p,digest in HELPERS.items(): check(sha((ROOT/p).read_bytes()) == digest, "Unchanged helper after " + p)
            result = dict(marker="NATIVE_COLLIDER_LIFETIME_SLICES_OK",checks=len(checks),refusals=refusals,
                sourceImageSha256=raw.IMAGE_SHA,sourcePins=PINS,helpers=HELPERS,
                sourceLookahead=dict(nextClass="11D",donorOffset="19EACC",wordHex=next_word.hex(),wizardPackageLength="35B4"),
                successfullyCompletedSlicePcs=[hx(p) for p in sorted(executed)],contactPointer="80075804",
                boltSourceFragment=dict(pc="8007EAD0",length=0x1A0,sha256=sha(bolt_main)),
                syntheticEntryWitnesses=["post-GTE type3 miss","post-relocation animation0 model","valid grid chains",
                    "placed deletion boundary","allocator tail ownership","spatial helper returns","sphere post-GTE success"],
                noGteExecuted=True,noNativeHelperStub=True,completeFrameOrLifetimeProven=False,
                issuedFreshResourceReadOwnerVerified=False,wizardDamageAttributionProven=False,targetOutputsWritten=0)
        finally:
            raw.identity(image)
    result["sourceUnchanged"] = True
    print(json.dumps(result,indent=2,sort_keys=True))


if __name__ == "__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_bin"); parser.add_argument("--naive-package-read-bound",action="store_true")
    args=parser.parse_args(); main(args.original_bin,args.naive_package_read_bound)
