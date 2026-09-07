#!/usr/bin/env python3
"""Private source resource proposal; metadata only, no issued output/body/runtime.

Retains existing Wizard resource goldens and adds the three exact Town fallback
packages in memory. Runs real generic loaders and two explicitly separate
post-GTE/end-loop integer renderer read witnesses, never a GTE or callback stub.
"""
import argparse
import contextlib
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
RESOURCE = "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_resource_oracle.py"
RESOURCE_SHA = "3f34505e2058a16bf2eb26751c0d5cb27dc48965e6d5d6b648654f34a50cdc6f"
PACKAGES = {
    0x0E: (0x1C8D04, 0x7EC, "ca65a14da356546aa00e308319e637809c87c9de8f0901b353de9b377a4a4a62"),
    0x0F: (0x1C94F0, 0x78C, "4e1dbb926be5b833e1fb314a154de05f9dd104c5a2148713b2a9e9e9094613f1"),
    0x10: (0x1BED08, 0x330, "947da1119d6159e0a496c91850e167706c6f57e0e8615646d2ae1516c2fa7257"),
}
SPANS = (
    ("simple-renderer-static-owner", 0x80022A2C, 0x1098, "7467b9b2e8b2a319736e5d8c1ae2d482f863abc17fc52ff02be464fee0c9c812"),
    ("face-loop-end-branch-delay", 0x800232A8, 8, "cd83bd0acd809e9dd577be4ab4c4415ec35a5b97190570a811459ae9faafede8"),
    ("post-GTE-face-prefetch", 0x8002332C, 12, "65caae3ab956b0c84a207a94a426aecbb33835c8f1f4fa066576c20f90ed566f"),
    ("end-loop-load-retirement", 0x80023958, 4, "f37b2eb9b6594633e90a6867494d891596004945171b2834479a37b07e0e25ef"),
    ("animated-selected-collider-to-grid-guard", 0x800526A8, 0xB0, "6957574246c153803d760eb6e0001529b8016eaec09da84f94aa893cc5560c21"),
    ("simple-row-initializer", 0x800529CC, 0x18, "4973eb40a058490002e752ee189564f94c173b9489f03b6f77bd25f2b2a3c072"),
    ("particle-header-type-allocator-static", 0x80053570, 0x98, "2eefe927534faee54464ea9af9e53a9382ff3fafa0f90d50b6a68982dce61ddb"),
    ("particle-type2-descriptor-consumer-static", 0x80057C08, 0x274, "5763c22d74429eece44169934917ca2336b4f2a1fb9565c8fb292e5350e261f4"),
    ("particle-type1-untextured-consumer-static", 0x80057E7C, 0x11C, "f01218c13d66114224931e8a049ad297c4ff48fee5ebe830a2f2f58635e5efa4"),
    ("particle-type0-untextured-consumer-static", 0x80057F98, 0xE8, "0cab3a05286a1afd3f64a2f77272a74e161b5bd76e4659bde93b6dad3ba7f3c3"),
    ("particle0C-source-color-constants-static", 0x8006E490, 8, "9ee834b4eff7c64912b6281c6b1bd333c3e293835f2cf4387092198536c5c5c1"),
)


def sha(b): return hashlib.sha256(b).hexdigest()
def require(ok, message):
    if not ok: raise AssertionError(message)
def word(b, at=0): return struct.unpack_from("<I", b, at)[0]
def half(b, at): return struct.unpack_from("<H", b, at)[0]
def put(b, at, n): struct.pack_into("<I", b, at, n)


def load(relative, name):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    m = importlib.util.module_from_spec(spec)
    with contextlib.redirect_stdout(io.StringIO()): spec.loader.exec_module(m)
    return m


def main(path):
    require(__debug__ and sha((ROOT / RESOURCE).read_bytes()) == RESOURCE_SHA, "Frozen resource helper")
    resource = load(RESOURCE, "fallback_resource"); resource.require_helpers()
    raw, upload, body_raw, native = [resource.load(p, "fallback_" + str(i)) for i, p in enumerate(list(resource.HELPERS)[:4])]
    checks, refused = [], []
    def check(ok, label): require(ok, label); checks.append(label)
    def reject(action, phrase, label):
        try: action()
        except (AssertionError, ValueError) as e:
            check(phrase in str(e), "Exact refusal " + label + ": " + str(e)); refused.append(label); return
        raise AssertionError("Expected refusal " + label)
    with open(path, "rb") as image:
        raw.identity(image)
        try:
            assets = resource.capture_assets(path, image, raw); town = assets.town
            fresh = upload.capture(path)
            with contextlib.redirect_stdout(io.StringIO()): (body, _), *_ = body_raw.build_bodies(path)
            check(sha(body) == resource.BASE_BODY_SHA, "Whole unchanged baseline body")
            vram, rewritten, _, _, allocated = resource.install_textures(fresh, assets, check)
            check(sha(vram) == resource.RESOURCE_GOLDENS["vram"], "Whole unchanged installed Wizard VRAM")
            tr = raw.roots(town); models = {}; metadata = []
            for cls, (at, n, h) in PACKAGES.items():
                b = raw.extent(town, at, n)
                check(tr[cls] == (at, n) and sha(b) == h, "Exact complete Town registration " + hex(cls))
                models[cls] = b
                metadata.append(dict(class_id=cls, town_offset=at, wad_offset=0x119E000 + at, length=n, sha256=h))
            simple = models[0xE]
            check(word(simple) == 0xFF31A178 and simple[1] == 161 and word(simple, 12) == 0x2E4 and
                  word(simple, 12) + simple[1] * 8 == len(simple), "Complete simple negative header and 161 fixed8-byte faces")
            reject(lambda: raw.texture_fields(simple), "Animated", "Simple package is not animated descriptor grammar")
            groups = {}; references = {}
            for cls in (0xF, 0x10):
                b = models[cls]; tables, refs = raw.texture_fields(b); references[cls] = refs
                check(word(b) == 1 and b[4:20] == bytes([255]) * 16 and b[20:52] == bytes(32),
                      "Animated local slots FF and all eight collider roots zero " + hex(cls))
                metadata[1 if cls == 0xF else 2].update(face_tables=tables, texture_reference_count=len(refs))
                for at, field in refs: groups.setdefault((cls, half(field, 2), half(field, 6)), []).append((at, field))
            check(len(references[0xF]) == 72 and len(references[0x10]) == 8 and len(groups) == 3, "Complete exact animated draw-field inventory")
            target = bytearray(vram); added, reused = set(), set(); textures = []
            for (cls, clut, page), refs in sorted(groups.items()):
                uv = [(b[i], b[i + 1]) for _, b in refs for i in (0, 4, 8, 10)]
                u0, v0, u1, v1 = min(u for u, _ in uv), min(v for _, v in uv), max(u for u, _ in uv), max(v for _, v in uv)
                x, y, width, rows = (page & 15) * 128 - 1024 + u0 // 2, (256 if page & 16 else 0) + v0, u1 // 2 - u0 // 2 + 1, v1 - v0 + 1
                cx, cy = (clut & 63) * 32 - 1024, clut >> 6
                record = dict(class_id=cls, page=page, clut=clut, pixel_offset=y * 1024 + x, row_bytes=width, rows=rows,
                              palette_offset=cy * 1024 + cx, reference_count=len(refs), new_bytes=0, retained_alias_bytes=0)
                for kind, px, py, w, h in (("pixel", x, y, width, rows), ("palette", cx, cy, 32, 1)):
                    resource.require_rectangle(px, py, w, h, kind)
                    locations = resource.indices(px, py, w, h)
                    payload = bytes(town[0x800 + i] for i in locations)
                    record[kind + "_sha256"] = sha(payload)
                    for i, value in zip(locations, payload):
                        check(not allocated[i], "No overlap with prior Wizard allocation")
                        if fresh.gpu_ownership[i]:
                            check(fresh.gpu_ownership[i] == 1 and target[i] == value, "Whole same-source retained Town byte alias")
                            reused.add(i); record["retained_alias_bytes"] += 1
                        else:
                            check(i not in added, "No partial alias between new source owners")
                            added.add(i); record["new_bytes"] += 1; target[i] = value
                    check(bytes(target[i] for i in locations) == payload, "Full strided pixel/palette readback")
                textures.append(record)
            check(len(added) == 1120 and len(reused) == 8, "Exact new1120/retained8 unique ownership")
            check(all(a == b for i, (a, b) in enumerate(zip(vram, target)) if i not in added), "Full VRAM complement including all protected zero bytes")
            for cls, refs in references.items():
                for _, b in refs:
                    page = half(b, 6)
                    for at in (0, 4, 8, 10):
                        i = ((256 if page & 16 else 0) + b[at + 1]) * 1024 + (page & 15) * 128 - 1024 + b[at] // 2
                        check(target[i] == town[0x800 + i], "Unchanged descriptor resolves exact source index byte and nibble")
            _, sound_at, selectors, sounds = raw.sound(town); sound_metadata = []
            source = dict(assets.owners); old_models = body[0xDF800:0xEF000]
            check(old_models[0xCC34] == 72 and selectors[0] == 17 and selectors[0x2F] == 18, "Actual retained selector/local namespace")
            for name, i in (("global00", 17), ("global2F", 18), ("Sparx78.local0", 72)):
                at, descriptor, sample_at, sample = sounds[i]
                start = word(descriptor); check(fresh.upload[0x80000 + start:0x80000 + start + len(sample)] == sample, "Complete retained sample " + name)
                sound_metadata.append(dict(name=name, descriptor=i, descriptor_wad=0x119E000 + at, descriptor_hex=descriptor.hex(),
                                           sample_offset=start, sample_length=len(sample), sample_sha256=sha(sample)))
            check(word(sounds[18][1], 12) == 0x40008, "Nonzero global2F controls retained, not sample-only equivalence")
            # Reconstruct the current detached grammar byte-for-byte, then leave
            # it and the entire current sound bank unchanged in this proposal.
            old_data = body[0xDE800:0xDF0C8]
            _, particle_at, old_sound_at, old_ids = resource.data_parts(old_data)
            sound = bytearray(old_data[old_sound_at:]); bank = bytearray(fresh.upload[0x80000:])
            for name in ("global1F", "local0"):
                descriptor = bytearray(source[name + ".descriptor"]); put(descriptor, 0, len(bank))
                sound.extend(descriptor); bank.extend(source[name + ".sample"])
            bank.extend(bytes(resource.round_cd(len(bank)) - len(bank)))
            put(sound, 0, len(sound)); put(sound, 0x104, 78); sound[4 + 0x1F] = 76
            particle = bytearray(old_data[particle_at:old_sound_at]); particle.extend(rewritten["particle7"])
            put(particle, 0, len(particle)); put(particle, 4, len(old_ids) + 1)
            unchanged_data = old_data[:particle_at] + particle + sound
            check(sha(unchanged_data) == resource.RESOURCE_GOLDENS["data"] and
                  sha(vram + bank) == resource.RESOURCE_GOLDENS["upload"], "Both whole current data/upload goldens unchanged")
            check(sound[4] == 17 and sound[4 + 0x2F] == 18 and
                  sound[0x108:0x108 + 76 * 20] == old_data[old_sound_at + 0x108:], "All76 original descriptors and required selectors unchanged")
            _, _, _, current_ids = resource.data_parts(unchanged_data)
            record_at = particle_at + 8 + current_ids.index(0xC) * 12
            record = unchanged_data[record_at:record_at + 12]
            town_particle, _, _, _ = raw.sound(town)
            town_record_at = next(at for at in range(town_particle + 8, town_particle + word(town, town_particle), 12) if half(town, at) == 0xC)
            check(record == town[town_record_at:town_record_at + 12] and half(record, 2) == 8, "Whole retained particle0C single-frame record")
            page, clut = half(record, 10), half(record, 6)
            u0, u1 = sorted((record[4], record[8])); v0, v1 = sorted((record[5], record[9]))
            x, y = (page & 15) * 128 - 1024 + u0 // 2, (256 if page & 16 else 0) + v0
            locations = resource.indices(x, y, u1 // 2 - u0 // 2 + 1, v1 - v0 + 1) + resource.indices((clut & 63) * 32 - 1024, clut >> 6, 32, 1)
            check(all(fresh.gpu_ownership[i] == 1 and vram[i] == target[i] == town[0x800 + i] for i in locations),
                  "Whole retained particle0C indexed pixel/palette complement")
            wizard = bytearray(rewritten["wizard.model"]); wizard[4:7] = bytes([77, 66, 67])
            root0 = old_models[:0xCC2C] + source["player.animation8"] + old_models[0xCC2C:0xCC30]
            prior = root0 + old_models[0xCC30:] + rewritten["bolt.model"] + wizard + bytes(0x6CC)
            check(len(prior) == 0x17800 and sha(prior) == resource.RESOURCE_GOLDENS["model"], "Whole unchanged previous models and Wizard lookahead padding")
            proposed = prior + b"".join(models[c] for c in (0xE, 0xF, 0x10)); used = len(proposed)
            proposed += bytes(resource.round_cd(used) - used)
            roots = ((0, 0), (0x78, 0x10A4C), (0x26, 0x1361C), (0x11B, 0x13B80), (0xE, 0x17800), (0xF, 0x17FEC), (0x10, 0x18778))
            check(used == 0x18AA8 and len(proposed) == 0x19000 and proposed[:len(prior)] == prior, "Append after complete prior17800; new19000 with558 generated suffix")
            check(resource.require_roots(proposed, roots) == (18, 19, 29, 40, 8), "Seven unique roots and unchanged complete player stream")
            # Exact native registration with explicit synthetic header/copy anchor;
            # no disk/body/header producer is installed or certified here.
            code = resource.native_code(native, assets)
            for name, start, n, h in SPANS:
                b = assets.exe[start - 0x8000F800:start - 0x8000F800 + n]
                check(sha(b) == h, "Source instruction owner " + name)
                if "static" not in name: code.update((start + i, word(b, i)) for i in range(0, n, 4))
            resident = native.globals_machine(code); resident.blob(native.PETE, assets.pete[:0x32E1C]); resident.r[4] = native.PETE
            resident.run(0x800133E0, {native.RETURN})
            m = native.globals_machine(code); base = 0x8008A7C8
            m.blob(base, proposed); m.blob(native.PETE, resident.bytes(native.PETE, 0x32E1C))
            m.poke(0x80076378, native.PETE); m.poke(0x800785E0, base); m.poke(0x800785FC, 0x801C5BB8)
            header = bytearray(0x1D0); origin = 0x4000
            for at, value in ((0x10, origin), (0x14, len(proposed)), (0x18, origin + len(proposed)), (0x1C, 0x3000)): put(header, at, value)
            for slot, (cls, at) in enumerate(roots):
                put(header, 0x50 + slot * 4, origin + at); struct.pack_into("<H", header, 0x150 + slot * 2, cls)
            for at in range(0, len(header), 4): m.poke(0x80076C00 + at, word(header, at))
            m.r[16] = 1
            m.run(0x80015A54, {0x800163BC})
            for cls, at in roots[1:]:
                expected = base + at
                check(m.read(0x80076378 + cls * 4) == (expected & 0x7FFFFFFF if cls == 0xE else expected), "Actual generic registration type/return " + hex(cls))
            check(all(m.read(0x80076378 + c * 4) == 0 for c in range(512) if c not in {c for c, _ in roots}) and
                  m.read(0x80076378) == native.PETE and m.read(0x800785E4) == base + len(proposed), "Whole other-class complement and scene arithmetic only")
            for at in (4, 8, 12): check(m.read(base + 0x17800 + at) == base + 0x17800 + word(simple, at), "Actual simple loader pointer relocation")
            simple_at = base + 0x17800
            simple_writes = [(at - simple_at, n, v) for at, n, v in m.writes if simple_at <= at < simple_at + len(simple)]
            check(simple_writes == [(4, 4, simple_at + 0x178), (12, 4, simple_at + 0x2E4), (8, 4, simple_at + 0x2E0)],
                  "Complete simple loader exact three-write set, including jump-delay store")
            inverse = bytearray(m.bytes(simple_at, len(simple)))
            for at, n, _ in simple_writes: inverse[at:at+n] = simple[at:at+n]
            check(bytes(inverse) == simple, "Complete simple package relocation inverse and nonfield complement")
            check(m.bytes(base + 0x17134, 0x6CC) == bytes(0x6CC), "Prior Wizard generated lookahead remains untouched")
            # Standalone actual simple initializer; no claim this executes its spawn.
            row = 0x80140000; m.blob(row, bytes([0xA5]) * 0x70); m.r[4], m.r[31] = row, native.RETURN
            m.run(0x800529CC, {native.RETURN})
            check(m.read(row + 8) == 0 and m.read(row + 0x34, 2) == 0xFFFF, "Actual simple initializer null collider and no-grid sentinel")
            for cls in (0xF, 0x10):
                row = 0x80140000 + cls * 0x70; m.blob(row, bytes(0x70)); m.poke(row + 0x36, cls, 2); m.r[4] = row
                m.run(0x800526A8, {0x80052758})
                check(m.read(row + 8) == 0 and m.read(row + 0x34, 2) == 0xFFFF, "Actual animated selected-null collider/grid guard " + hex(cls))
            # A bounded renderer read is not rendering: this is a post-GTE
            # entry witness, with t6 derived from the actual final face's bit0.
            # The loop-end case is a separate entry, not an executed draw body.
            end = base + 0x17FEC
            check(proposed[0x17FEC:0x17FF4] == town[0x1C94F0:0x1C94F8] == struct.pack("<II", 1, 0xFFFFFFFF), "Exact source-adjacent0F supplies simple renderer prefetch8")
            check(word(simple, len(simple) - 8) & 1 == 0, "Actual final simple face selects nonnegative t6=0 route")
            def prefetch(include_following, stop=0x80023338):
                p = native.Machine(code); p.blob(base + 0x17800, m.bytes(base + 0x17800, 0x7EC))
                if include_following: p.blob(end, m.bytes(end, 8))
                p.r[21], p.r[14] = end - 8, word(simple, len(simple) - 8) & 1
                p.run(0x8002332C, {stop}); return p
            reject(lambda: prefetch(False), "Unmapped", "Package-only misses unconditional next-face word")
            reject(lambda: prefetch(True, 0x80023330), "delay", "Cannot stop before prefetched load retirement")
            p = prefetch(True); check(p.r[1] == 1 and p.r[21] == end, "Actual post-GTE prefetch and branch delay retire source next word")
            p.r[21] = p.r[22] = end
            reject(lambda: copy.deepcopy(p).run(0x800232A8, {0x80023958}), "delay", "Cannot stop before end-loop delay load retires")
            p.run(0x800232A8, {0x8002395C})
            check(p.r[16] == 0xFFFFFFFF, "Actual end-loop delay reads second owned next-model word")
            result = dict(marker="SOURCE_TOWN_FALLBACK_ASSETS_OK", checks=len(checks), refusals=len(refused), packages=metadata,
                          textures=textures, new_vram_owned_bytes=len(added), retained_alias_bytes=len(reused), vram_sha256=sha(target),
                          prior_model_sha256=sha(prior), proposed_model_sha256=sha(proposed), model_used=used, model_length=len(proposed),
                          roots=roots, sounds=sound_metadata, source_pins=SPANS, helper_sha256=RESOURCE_SHA,
                          unchanged_data_sha256=sha(unchanged_data), unchanged_bank_sha256=sha(bank), proposed_upload_sha256=sha(target + bank),
                          retained_particle0C=dict(record_hex=record.hex(), record_wad=0x119E000 + town_record_at, indexed_owned_bytes=len(set(locations))),
                          simple_relocation_writes=simple_writes, renderer_selected_reads=p.reads,
                          native_registration=dict(explicit_synthetic_model_base=hex(base), arithmetic_scene_only=hex(base+len(proposed)),
                                                   instructions=m.instructions, unique_pcs=len(m.executed), class0E_low_pointer=True),
                          limits=dict(target_outputs_written=0, actual_issued_revision=False, body_scene_or_overlay=False,
                                      whole_renderer_or_GTE=False, callback_or_spawn=False, audio_delivery=False,
                                      whole_collision_lifetime=False, global1F_compatibility=False, save_or_runtime=False))
        finally:
            try:
                raw.identity(image)  # complete SHA, exact length/tell and actual EOF
            finally:
                require(sha((ROOT / RESOURCE).read_bytes()) == RESOURCE_SHA, "Frozen resource helper changed during run")
                resource.require_helpers()
    print(json.dumps(result, indent=2, sort_keys=True))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(); parser.add_argument("original", type=Path)
    main(parser.parse_args().original)
