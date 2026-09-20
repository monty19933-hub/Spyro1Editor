#!/usr/bin/env python3
"""Independent original-byte Wizard staging and bounded native request witness.

No Core/DLL, output payload, body/header/candidate, hardware or helper-return
emulation. Native slices have explicit entry witnesses and retire actual delays.
The intervening GPU/CD/SPU helper bodies and async scheduler are NOT executed.
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
RESOURCE_HELPER = "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_resource_oracle.py"
RESOURCE_SHA = "2767ddf79a33e3b97a972d6650e1ee42575c24849753b011142ec434145ea57d"
SPANS = (
    ("source-s0-copy-global-address", 0x80015784, 8, "15d44e624b77e93218f097ced75d8a415830725b99a3b55ffd74fe2ce3e25801"),
    ("phase-increment-bank-request", 0x800157B0, 0x84, "385084ac7101e2882db5be60e3b25a524b8f1a24f8ac14923808982cad537f4b"),
    ("CD-actual-argument-register-copies", 0x80016698, 0x1C, "1971ee8ef601d0cb458c5cbc08f402594d8ee586c2957fea203ebfa4c5eed617"),
    ("CD-signed-sector-rounding-and-call-delay", 0x80016724, 0x3C, "6ca8b181241e79acf9df6b3216fc702b159e35afebbf0310a0a1fde1b396d0d7"),
    ("normal-fixed-SPU-call-arguments", 0x8001583C, 0x20, "0ecd01fbbbafa7e7c809e25e6d19a7dea97dffc4a89fcf36beae656c022d7fa8"),
)


def sha(b): return hashlib.sha256(b).hexdigest()
def require(ok, message):
    if not ok: raise AssertionError(message)
def word(b, at): return struct.unpack_from("<I", b, at)[0]


def load_resource():
    path = ROOT / RESOURCE_HELPER
    require(sha(path.read_bytes()) == RESOURCE_SHA, "Pinned resource helper changed")
    spec = importlib.util.spec_from_file_location("staging_resource", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.require_helpers()
    return module


def code_for(exe):
    require(len(exe) == 0x66000 and sha(exe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998", "Whole source EXE changed")
    code = {}
    for name, start, length, digest in SPANS:
        raw = exe[start - 0x8000F800:start - 0x8000F800 + length]
        require(len(raw) == length and sha(raw) == digest, "Native source pin changed: " + name)
        code.update((start + at, word(raw, at)) for at in range(0, length, 4))
    return code


def request_machine(native, code, upload_length, phase=1, omit_header=False):
    m = native.Machine(code)
    # Explicit bounded fixture RAM, not emitted native header or archive values.
    # s0 is established by actual source instructions; g785DC supplies the
    # synthetic copy address. Source table index3/offset4000 are arithmetic
    # witnesses only and do not claim a live level descriptor.
    values = {0x800785DC:0x80100000, 0x8007576C:phase, 0x80076B90:37,
              0x80076C00:0x800, 0x80075964:3, 0x80075864:11, 0x8007A750:0x4000}
    if not omit_header: values[0x80076C04] = upload_length
    for at, value in values.items(): m.map(at, 4); m.poke(at, value)
    m.map(0x801FF700, 0x200, 0xA5); m.r[29] = 0x801FF800
    m.map(0x80076B94, 4); m.map(0x80076B9C, 4); m.map(0x80076BB8, 4)
    m.run(0x80015784, {0x8001578C})
    return m


def transfer(upload):
    require(len(upload) == 0xE5800, "Exact complete research upload length")
    result = upload[0x80000:] + upload[0x65800:0x7F000]
    require(len(result) == 0x7F000, "Complete DMA-source length")
    return result


def main(path):
    require(__debug__, "Assertions must be enabled")
    resource = load_resource()
    raw = resource.load(next(iter(resource.HELPERS)), "staging_assets")
    fresh_raw = resource.load(list(resource.HELPERS)[1], "staging_fresh")
    native = resource.load(list(resource.HELPERS)[3], "staging_native")
    checks, refusals = [], []
    def check(ok, label): require(ok, label); checks.append(label)
    def reject(action, phrase, label):
        try: action()
        except AssertionError as e:
            check(phrase in str(e), "Expected refusal: " + label + ": " + str(e)); refusals.append(label); return
        raise AssertionError("Expected refusal: " + label)
    with open(path, "rb") as image:
        raw.identity(image)
        try:
            assets = resource.capture_assets(path, image, raw)
            fresh = fresh_raw.capture(path)
            vram, _, _, _, _ = resource.install_textures(fresh, assets, check)
            owners = dict(assets.owners)
            bank = fresh.upload[0x80000:] + owners["global1F.sample"] + owners["local0.sample"]
            check(len(bank) == 0x65170, "Complete retained bank and two source sample used end")
            bank += bytes((-len(bank)) % 0x800)
            upload = vram + bank
            check(len(bank) == 0x65800 and sha(bank) == resource.RESOURCE_GOLDENS["bank"] and
                  sha(upload) == resource.RESOURCE_GOLDENS["upload"], "Complete pre-existing independent resource goldens")
            check(sha(fresh.upload[0x80000:] + fresh.upload[0x5E000:0x7F000]) ==
                  "626ae349b98cb4bea955ac53020586f645f872225085a67bc809f46f4ab0fb07", "Old unchanged transfer golden control")
            before = sha(upload); result = transfer(upload)
            check(sha(result) == "33282cc17414acccd98381f8d5b9e068ea2327bb3c8a7138dd82b1705c217fad" and
                  sha(result[0x65800:]) == "e2e713a331281d553f43cd2df764208ba00770dc1d2fea767963f698cd238ac1",
                  "Independent first-derivation full transfer and residue goldens, before Core staging exists")
            check(result[:0x65800] == bank and result[0x65800:] == upload[0x65800:0x7F000] and sha(upload) == before,
                  "Complete same-upload bank/residue phase owners and immutable input")
            code = code_for(assets.exe); native_cases = []
            for length, expected_bank, sectors in ((0xDE000,0x5E000,188),(len(upload),len(bank),203)):
                m = request_machine(native, code, length)
                m.run(0x800157B0, {0x80016698})
                check(m.r[16] == 0x800785DC and m.r[4:8] == [37,0x80100000,expected_bank,0x84800] and
                      m.r[31] == 0x80015834 and m.read(0x801FF810) == 600 and m.read(0x8007576C) == 2,
                      "Actual bank argument/subtraction/call delay from descriptor length " + hex(length))
                # Run actual ABI moves; stop BEFORE the CD mode helper call.
                m.run(0x80016698, {0x800166B4})
                check(m.r[19] == expected_bank and m.r[20] == 0x80100000, "Actual caller arguments carried into CD-owned saved registers")
                # Independent rounding slice with those exact carried registers.
                # Intervening helper bodies are not invoked or fake-returned.
                m.run(0x80016724, {0x8006606C})
                check(m.r[4:7] == [sectors,0x80100000,0x80] and m.read(0x80076B94) == sectors and
                      m.read(0x80076B9C) == 0x80100000 and m.r[31] == 0x80016760,
                      "Actual signed sector rounding and final JAL delay " + str(sectors))
                native_cases.append(dict(upload_length=length, bank_length=expected_bank, sectors=sectors,
                                         executed=m.instructions, calls=m.calls))
            skipped = request_machine(native, code, len(upload), phase=0)
            skipped.run(0x800157B0, {0x800163BC})
            check(skipped.calls == [] and skipped.r[6] == 0xFFF80000, "Wrong phase skips bank call but executes native branch delay")
            missing = request_machine(native, code, len(upload), omit_header=True)
            reject(lambda: missing.run(0x800157B0,{0x80016698}), "Unmapped", "Missing real descriptor-length owner")
            delayed = request_machine(native, code, len(upload))
            reject(lambda: delayed.run(0x800157B0,{0x800157CC}), "delay", "No stop before branch delay retirement")
            for name, at, _, _ in SPANS:
                changed = bytearray(assets.exe); changed[at-0x8000F800] ^= 1
                reject(lambda changed=changed:code_for(changed), "EXE", "Mutated complete source/code " + name)
            spu = request_machine(native, code, len(upload))
            spu.run(0x8001583C,{0x8005CB24})
            check(spu.r[4] == 0x1010 and spu.r[31] == 0x80015844, "SPU address argument actual delay")
            # Independent post-address-helper entry witness, not continuation
            # of the preceding machine or a synthesized helper return.
            count = request_machine(native, code, len(upload))
            count.run(0x80015844,{0x8005CAC4})
            check(count.r[4:6] == [0x80100000,0x7EFF0], "Independent fixed SPU source/count entry witness; no helper body")
            check((count.r[5]+63)//64*64 == len(result) and len(result)-count.r[5] == 16,
                  "Source-request arithmetic exposes distinct DMA-only final16; DMA backend not newly executed")
            for bad in (upload[:-1], upload+b"\0", fresh.upload):
                reject(lambda bad=bad:transfer(bad), "length", "Wrong complete upload shape " + str(len(bad)))
            check(result != bank + bytes(0x19800), "Zero-padded bank is not native transfer source")
            output = dict(marker="FRESH_WIZARD_STAGING_RAW_ORACLE_OK",checks=len(checks),refusals=len(refusals),
                resource_helper_sha256=RESOURCE_SHA, transitive_helper_pins=resource.HELPERS,
                native_source_pins=[dict(id=n,address=hex(a),length=l,sha256=h) for n,a,l,h in SPANS],
                upload_sha256=sha(upload), transfer_sha256=sha(result), transfer_length=len(result),
                bank_sha256=sha(bank), bank_length=len(bank), residue_sha256=sha(result[0x65800:]), residue_length=0x19800,
                dma_only_tail_hex=result[0x7EFF0:].hex(), native_cases=native_cases,
                cpu_request=0x7EFF0,dma_source_length=0x7F000,linear_spu_destination=0x1010,linear_spu_end=0x80010,
                independent_spu_slices=[dict(entry="0x8001583C",stop_before_helper="0x8005CB24",purpose="address argument"),
                    dict(entry="0x80015844",stop_before_helper="0x8005CAC4",purpose="independent post-address-helper count witness")],
                native_argument_and_rounding_slices_executed=True,native_gpu_cd_spu_helpers_executed=False,
                helper_returns_synthesized=False,async_chronology_or_hardware_proven=False,
                current_header_table_values_are_explicit_fixture_witnesses=True,
                runtime_or_publication_approved=False,payload_files_written=0)
        finally:
            raw.identity(image); resource.require_helpers()
            require(sha((ROOT/RESOURCE_HELPER).read_bytes())==RESOURCE_SHA,"Pinned helper changed after capture")
    output["source_before_after_sha_eof_verified"] = True
    print(json.dumps(output,indent=2))


if __name__ == "__main__":
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument("original_bin")
    main(parser.parse_args().original_bin)
