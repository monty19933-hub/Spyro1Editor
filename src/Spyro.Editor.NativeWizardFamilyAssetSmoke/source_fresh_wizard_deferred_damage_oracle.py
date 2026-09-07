#!/usr/bin/env python3
"""Private original-source scalar regression; no GTE/audio/runtime success.

Selected input/return witnesses are explicit. No helpers are implemented,
replaced or auto-returned. Stdout is metadata, never resource/candidate bytes.
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
MACHINE_PATH = "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py"
MACHINE_SHA = "8410f6fcf9f06504d9e607406f1c1169f104b434b07f64a4cf5ec27a61f5c8fa"
AUDIT_SHA = "7fe4e4e65d1552420bb48778a274e25afdf65005d55e8590125d2f84db23f532"
EXE_BASE, WIZARD_BASE = 0x8000F800, 0x8007AA38
SPYRO, FLAGS, STACK, RETURN = 0x80078A58, 0x80078A84, 0x801F0000, 0xDEADBEE0
MAP, POINTERS, TRIGGER, BOLT = 0x80110000, 0x80111000, 0x80112000, 0x80113000
PLAY, STATE = 0x80055A78, 0x8003EA68
CALLERS = (0x800416B0, 0x80041800, 0x800418F8, 0x80041A4C, 0x80041BEC,
           0x80041C74, 0x80041F04, 0x80042238, 0x8004232C, 0x800423C0,
           0x80042644, 0x800426C0, 0x80042870, 0x80042AA0, 0x80042AB0,
           0x80042C3C, 0x80042D5C, 0x800430E4, 0x8004319C, 0x8004359C,
           0x8004363C, 0x800436E0, 0x8004393C, 0x80043B78, 0x80043DDC, 0x80043E08)
PINS = (
    ("trigger-caller-tail", "exe", 0x80048C64, 0xAC, "6251507cc6e0cd8d46eaebb48c4d5267680596fb5ef8badb743f6e7c221bcc0a"),
    ("trigger-dispatcher", "exe", 0x80056F64, 0x41C, "759c01c5c7eb5fa9d073ec837400d05ffaa5b70d8685ad16dcd243125b701152"),
    ("trigger-table-data", "exe-data", 0x80011380, 0x20, "527099efb8d985bb3409404bffe91dacc4ecc728b66415f4069e70117f21d05c"),
    ("bolt-argument-call", "wizard", 0x8007EB80, 0x18, "ddc1bc4f5d7fbb9bd08158a845dc0f7bcb3a44b2be72239c5dc93d87d5d34bda"),
    ("sphere-helper", "exe", 0x8004E2E8, 0xE0, "616f1e7f8fed79452a3ec0793db251866c9127bf2e76f66dd634ed02f89163f3"),
    ("damage-consumer", "exe", 0x80040F68, 0x308, "f5cb321da9444ee83d3b6a2bc042f4aa5383e987c6558498fb8345835a0333c1"),
    ("normal-clear", "exe", 0x8004A404, 8, "9a58c1d3016c9eece96612a2bf9db3df1f1ad5e333c4e077d28de0e5236a40e2"),
    ("reset-clear", "exe", 0x8004ADBC, 8, "9a58c1d3016c9eece96612a2bf9db3df1f1ad5e333c4e077d28de0e5236a40e2"),
    ("separate-low20-consumer", "exe", 0x800427BC, 0x18, "99b9a5db82514ae74de06600ffe80208419d2cc4179ef014db88e31cce9b51dc"),
    ("ordered-trigger-call-evidence", "exe-evidence", 0x8004A1BC, 0xE4, "edad3532fbddbfec475a4c63b85196e40de43cbb928bfd7cb8164803d84963aa"),
    ("ordered-damage-call-evidence", "exe-evidence", 0x8004A2A0, 0x160, "70b8d77673b94b89bd45e1b6ec17a8b77aae6e40ef0101481d8a0aaaacffb460"),
)


def require(ok, label):
    if not ok: raise AssertionError(label)
def sha(data): return hashlib.sha256(data).hexdigest()
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]
def hx(value): return f"{value:08X}"
def peek(m, at): return int.from_bytes(m.bytes(at, 4), "little")


def module(path, expected, name):
    require(sha(path.read_bytes()) == expected, "Unchanged reviewed helper " + str(path))
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    with contextlib.redirect_stdout(io.StringIO()): spec.loader.exec_module(result)
    return result


def main(path, naive):
    raw = module(Path(__file__).with_name("source_fresh_wizard_audio_audit.py"), AUDIT_SHA, "deferred_raw")
    native = module(ROOT / MACHINE_PATH, MACHINE_SHA, "deferred_scalar")
    checks, refusals, executed, rows = [], [], set(), []

    def check(ok, label):
        require(ok, label); checks.append(label)

    def reject(action, phrase, label):
        try: action()
        except (AssertionError, ValueError) as error:
            require(phrase in str(error), "Unexpected refusal " + str(error))
            refusals.append({"label": label, "message": str(error)}); return
        raise AssertionError("Expected refusal " + label)

    with open(path, "rb") as image:
        raw.identity(image)
        try:
            source = {name: raw.logical(image, *raw.SOURCES[name][:2]) for name in ("exe", "wizard", "town-data")}
            for name, data in source.items(): check(sha(data) == raw.SOURCES[name][3], "Complete original " + name)
            exe = source["exe"]; code, certificates = {}, []
            for label, owner, pc, size, expected in PINS:
                data, base = (source["wizard"], WIZARD_BASE) if owner == "wizard" else (exe, EXE_BASE)
                part = data[pc - base:pc - base + size]
                check(len(part) == size and sha(part) == expected, "Exact source pin " + label)
                bad = bytearray(part); bad[-1] ^= 1
                reject(lambda b=bad, e=expected: require(sha(b) == e, "Source pin mismatch"), "pin mismatch", "Mutated " + label)
                if owner in ("exe", "wizard"):
                    for offset in range(0, size, 4):
                        require(pc + offset not in code, "Overlapping executable source owners")
                        code[pc + offset] = word(part, offset)
                certificates.append({"id": label, "pc": hx(pc), "length": size, "sha256": expected, "executable": owner in ("exe", "wizard")})
            for caller in CALLERS:
                pair = exe[caller - EXE_BASE:caller - EXE_BASE + 8]
                expected_mask = 0x8400 if caller == 0x80043E08 else 0xFBF9 if caller in (0x80042238, 0x8004363C) else 0xFFF9
                check(word(pair) == 0x0C0103DA and word(pair, 4) == (0x34040000 | expected_mask), "Actual JAL / ORI mask " + hx(caller))
                for offset in (0, 4):
                    require(caller + offset not in code, "Distinct damage caller owner")
                    code[caller + offset] = word(pair, offset)
                certificates.append({"id": "damage-caller-" + hx(caller), "pc": hx(caller), "length": 8, "sha256": sha(pair), "executable": True})
            sound_at, mapping, sounds = raw.sounds(source["town-data"])
            check(len(sounds) == 76 and mapping[31] == 0, "Actual original occupied Town map1F")
            table = exe[0x80011380 - EXE_BASE:0x800113A0 - EXE_BASE]

            def machine(flags=0, health=3, invulnerability=0, secondary=0, level=65, loaded=65, no_decrement=0):
                m = native.Machine(dict(code))
                m.map(0x80078A50, 0x2C0)
                m.map(STACK - 0x100, 0x200, 0xA5)
                for at in (0x80075718, 0x800785B8, 0x800758B4, 0x8007596C, 0x800756A0, 0x800761D4): m.map(at, 4)
                m.map(0x800777C0, 16 * 4)
                m.blob(MAP, mapping); m.blob(0x80011380, table)
                m.map(POINTERS, 64 * 4); m.map(TRIGGER, 8); m.map(BOLT, 12)
                m.poke(0x800785B8, POINTERS); m.poke(POINTERS + 3 * 4, TRIGGER); m.poke(POINTERS + 4 * 4, TRIGGER)
                m.poke(TRIGGER, 7, 1); m.poke(TRIGGER + 4, 3); m.poke(0x800777C0 + 3 * 4, 1)
                m.poke(FLAGS, flags); m.poke(0x80078BBC, health & 0xFFFFFFFF)
                m.poke(0x80078BB8, invulnerability); m.poke(0x80078B6C, secondary)
                m.poke(0x8007596C, level); m.poke(0x800758B4, loaded)
                m.poke(0x800756A0, no_decrement); m.poke(0x800761D4, MAP)
                for register in range(16, 24): m.r[register] = 0xA5000000 + register
                m.r[29], m.r[31] = STACK, RETURN
                return m

            def run(m, pc, stops):
                stop = m.run(pc, stops, maximum=2000)
                executed.update(m.executed)
                check(m.bytes(MAP, 256) == mapping and m.bytes(0x80011380, 32) == table, "Retained map/table complement")
                return stop

            def damage(m, caller=CALLERS[0], stop_after_mask=False):
                m.r[29] = STACK
                saved = tuple(m.r[r] for r in (16, 17))
                mask_stop = run(m, caller, {0x80040FDC, caller + 8})
                masked = m.r[4] if mask_stop == 0x80040FDC else None
                if stop_after_mask: return masked
                stop = mask_stop if mask_stop == caller + 8 else run(m, mask_stop, {PLAY, STATE, caller + 8, 0x8003DFA4})
                result = {"stop": hx(stop), "maskedFlags": None if masked is None else hx(masked),
                          "globalFlags": hx(peek(m, FLAGS)), "health": hx(peek(m, 0x80078BBC)),
                          "arguments": [hx(m.r[r]) for r in (4, 5, 6, 7)], "ra": hx(m.r[31])}
                if stop == PLAY:
                    check(tuple(m.r[r] for r in (4, 5, 6, 7, 31)) == (0, SPYRO, 4, 0x80078CF8, 0x80041064), "Actual original PlaySound ABI and JAL/load delay")
                if stop == caller + 8:
                    check((m.r[16], m.r[17], m.r[29]) == (*saved, STACK), "Real damage return restores s0/s1/SP")
                    result["returnValue"] = m.r[2]
                check(stop != 0x8003DFA4, "No fixture silently substitutes other native helper")
                return result

            if naive:
                m = machine(flags=0x20020)
                masked = damage(m, stop_after_mask=True)
                print(json.dumps({"control": "naive-high-bit-preserved-in-a0", "actualMaskedA0": hx(masked), "expectedNaive": "00020020"}))
                require(masked == 0x20020, "EXPECTED SOURCE RED: actual ORI/AND removes20000; a0=00000020")

            # No interpreter edits: microcontrols run the reviewed machine.
            micro = native.Machine({0x100: 0x8C820000, 0x104: 0x8C820004, 0x108: 0x00401821, 0x10C: 0x00402821})
            micro.map(0x1000, 8); micro.poke(0x1000, 42); micro.poke(0x1004, 77)
            micro.r[4], micro.r[2] = 0x1000, 9; micro.run(0x100, {0x110})
            check((micro.r[3], micro.r[5]) == (9, 77), "Reviewed consecutive same-rt delayed load cancellation")

            # Explicit post-height-query v1/a1 and already-entered caller frame.
            modes = []
            for label, blocker, previous, separation, expected in (
                ("mode2", 0, 0x3F, 0x100, 2), ("mode0", 1, 4, 0x100, 0),
                ("mode1", 1, 0x3F, 0x100, 1), ("mode2-boundary200", 0, 0x3F, 0x200, 2),
                ("mode1-boundary201", 0, 0x3F, 0x201, 1)):
                m = machine(flags=0x20020); saved0, saved1 = m.r[16], m.r[17]
                m.r[29] = STACK - 0x28; m.poke(STACK - 4, RETURN); m.poke(STACK - 8, saved0)
                m.r[3], m.r[5] = blocker, 0x2000 - separation
                m.poke(0x80075718, 3); m.poke(0x80078BC4, previous)
                m.poke(0x80078A60, 0x2000); m.poke(0x80078AF8, 0x2000 - separation)
                check(run(m, 0x80048C64, {0x80056F64, RETURN}) == 0x80056F64, "Actual caller reaches trigger " + label)
                check(m.r[5] == expected and m.r[31] == 0x80048CFC, "Native mode and JAL return " + label)
                mode = m.r[5]; run(m, 0x80056F64, {RETURN})
                expected_flags = 0x26 if expected == 2 else 0x20020
                check(peek(m, FLAGS) == expected_flags and (m.r[16], m.r[17], m.r[29]) == (saved0, saved1, STACK), "Actual type7 flags and both real epilogues " + label)
                modes.append({"case": label, "mode": mode, "flags": hx(peek(m, FLAGS))})
            m = machine(flags=0x20020); m.r[29] = STACK - 0x28
            m.poke(STACK - 4, RETURN); m.poke(STACK - 8, m.r[16])
            m.r[3], m.r[5] = 0, 0x1000
            m.poke(0x80075718, 0x3F); m.poke(0x80078BC4, 0x3F); m.poke(0x80078AF8, 0x1000)
            check(run(m, 0x80048C64, {RETURN, 0x80056F64}) == RETURN and peek(m, FLAGS) == 0x20020, "Native trigger sentinel no-call path")

            def trigger(m, mode=2, condition=1, type_=7):
                m.r[29], m.r[31], m.r[4], m.r[5] = STACK, RETURN, 3, mode
                m.poke(0x800777C0 + 3 * 4, condition); m.poke(TRIGGER, type_, 1)
                saved = tuple(m.r[r] for r in (16, 17))
                run(m, 0x80056F64, {RETURN})
                check((m.r[16], m.r[17], m.r[29]) == (*saved, STACK), "Real standalone trigger epilogue")
                return peek(m, FLAGS)

            for mode, condition, type_, expected in ((0, 1, 7, 0x20020), (1, 1, 7, 0x20020), (2, 0, 7, 0x20020), (2, 1, 8, 0x20020), (2, 1, 7, 0x26)):
                check(trigger(machine(flags=0x20020), mode, condition, type_) == expected, "Native trigger mode/condition/type guard")

            def bolt(m, hit=True):
                m.r[16] = BOLT
                run(m, 0x8007EB80, {0x8004E2E8})
                check((m.r[4], m.r[5], m.r[6], m.r[31]) == (BOLT, 0x80, 0x20020, 0x8007EB94), "Actual bolt argument/JAL delay")
                # Declared post-GTE entry: not a fake helper returning success.
                m.r[9] = SPYRO
                run(m, 0x8004E3A8 if hit else 0x8004E3C0, {0x8007EB94})
                check(m.r[2] == int(hit), "Actual selected outcome tail JR-delay result")
                return peek(m, FLAGS)

            m = machine(flags=0x800); check(bolt(m, False) == 0x800, "Actual no-hit tail does not change flags")
            orders = []
            for order, expected in (("bolt-then-type7", 0x26), ("type7-then-bolt", 0x20026)):
                m = machine()
                if order.startswith("bolt"): bolt(m); trigger(m)
                else: trigger(m); bolt(m)
                check(peek(m, FLAGS) == expected, "Executed selected native windows " + order)
                result = damage(m); check(result["stop"] == hx(PLAY), "Both declared orders reach original electric ABI")
                orders.append({"order": order, "beforeDamage": hx(expected), "damage": result})
            m = machine(); bolt(m); bolt(m)
            check(peek(m, FLAGS) == 0x20020, "Two declared hits coalesce through native OR")
            fresh = damage(m)
            stale = damage(machine(flags=0x20020))
            check(fresh == stale, "Same accumulator bytes cannot distinguish fresh hit from stale-high/unrelated-low witness")

            for caller in CALLERS:
                result = damage(machine(flags=0x20020), caller)
                check(result["maskedFlags"] == ("00000000" if caller == 0x80043E08 else "00000020"), "Actual caller mask erases high20000 " + hx(caller))
                check(result["stop"] == hx(caller + 8 if caller == 0x80043E08 else PLAY), "Actual accepted/excluded electric selection " + hx(caller))
                rows.append({"case": "caller-" + hx(caller), **result})

            cases = (
                ("no-flags", dict(flags=0), CALLERS[0], "return", 3, None),
                ("high-only", dict(flags=0x20000), CALLERS[0], "return", 3, None),
                ("unrelated-low20", dict(flags=0x20), CALLERS[0], "play", 2, None),
                ("bit10-priority", dict(flags=0x20030), CALLERS[0], "state", 2, 0x19),
                ("bit10-zero-health", dict(flags=0x20030, health=0), CALLERS[0], "state", 0xFFFFFFFF, 0x1F),
                ("electric-zero-health", dict(flags=0x20020, health=0), CALLERS[0], "play", 0xFFFFFFFF, None),
                ("negative-health", dict(flags=0x20020, health=-1), CALLERS[0], "return", 0xFFFFFFFF, None),
                ("no-decrement-guard", dict(flags=0x20020, no_decrement=1), CALLERS[0], "play", 3, None),
                ("level-mismatch", dict(flags=0x20020, loaded=10), CALLERS[0], "return", 3, None),
                ("invulnerable-secondary-zero", dict(flags=0x20020, invulnerability=1), CALLERS[0], "return", 3, None),
                ("invulnerable-secondary-positive", dict(flags=0x20020, invulnerability=90, secondary=1), CALLERS[0], "return", 3, None),
                ("FFF9-bit400", dict(flags=0x400), CALLERS[0], "state", 2, 0x1D),
                ("FBF9-excludes400", dict(flags=0x400), 0x80042238, "return", 3, None),
                ("8400-includes400", dict(flags=0x400), 0x80043E08, "state", 2, 0x1D),
            )
            for label, kwargs, caller, kind, health, state in cases:
                m = machine(**kwargs); result = damage(m, caller)
                target = PLAY if kind == "play" else STATE if kind == "state" else caller + 8
                check(result["stop"] == hx(target) and peek(m, 0x80078BBC) == health, "Native damage guard/state/health " + label)
                if state is not None: check(m.r[4] == state, "Actual state-helper argument " + label)
                rows.append({"case": label, **result})

            # A separate actual audio-return entry, never a synthesized audio run.
            m = machine(); m.r[4] = 0xBAD
            check(run(m, 0x80041064, {STATE}) == STATE and m.r[4] == 7 and m.r[31] == 0x800410F8, "Separate original electric continuation selects state7 through both JAL/J delays")
            for entry in (0x8004A404, 0x8004ADBC):
                m = machine(flags=0xFFFFFFFF); before = m.bytes(0x80078A50, 0x2C0)
                run(m, entry, {entry + 8})
                expected = bytearray(before); expected[FLAGS - 0x80078A50:FLAGS - 0x80078A50 + 4] = bytes(4)
                check(m.bytes(0x80078A50, 0x2C0) == expected and m.writes == [(FLAGS, 4, 0)], "Actual reset store and complete neighbor complement " + hx(entry))
            for flags in (0, 0x20, 0x20000, 0x20020):
                m = machine(flags=flags)
                stop = run(m, 0x800427BC, {0x800427D4, 0x80043FC8})
                check(stop == (0x80043FC8 if flags & 0x20 else 0x800427D4), "Separate native state path consumes low20")

            missing = machine(flags=0x20020); del missing.memory[MAP + 31]
            # Bypass only the post-run full-map comparison: the native LBU,
            # not a fixture's snapshot of deliberately unmapped bytes, must fail.
            reject(lambda: missing.run(CALLERS[0], {PLAY, CALLERS[0] + 8}), "Unmapped native read", "Missing actual audio-map owner")
            premature = machine(flags=0x20020)
            reject(lambda: premature.run(0x80048C9C, {0x80048CA0}), "Stop before delay retirement", "Cannot stop at unexecuted trigger branch delay")
            unowned = machine()
            reject(lambda: unowned.run(PLAY, {RETURN}), "Unowned native instruction", "PlaySound body is not synthesized")
            gte = machine()
            reject(lambda: gte.run(0x8004E380, {RETURN}), "Unsupported opcode", "Real GTE command remains unsupported")
            wrong_descriptor = machine(); wrong_descriptor.poke(TRIGGER + 4, 16)
            reject(lambda: trigger(wrong_descriptor), "Unmapped native read", "Synthetic condition owner cannot be overrun")
            reject(lambda: require(stale["globalFlags"] != fresh["globalFlags"], "Attribution is ambiguous"), "ambiguous", "Reject naive flags-only identity")

            metadata = {"checks": len(checks), "refusals": len(refusals), "certificates": certificates,
                        "triggerCallerModes": modes, "declaredWindowOrders": orders, "damageCases": rows,
                        "negativeControls": refusals, "distinctNativeInstructionsExecuted": len(executed),
                        "nativeExecutedPcs": [hx(pc) for pc in sorted(executed)],
                        "originalSoundOffset": sound_at, "helperSha256": MACHINE_SHA,
                        "rawHelperSha256": AUDIT_SHA, "sourceSha256": raw.IMAGE_SHA,
                        "gteExecuted": False, "audioOrStateHelperBodiesExecuted": False,
                        "postGteOutcomeEntryWitnesses": True, "postAudioReturnEntryWitness": True,
                        "wholeFrameExecuted": False, "freshTrigger7ReachabilityProven": False,
                        "collisionDescriptorOwnershipProven": False, "wizardAttributionProven": False,
                        "globalAudioCompatibilityProven": False, "runtimeVerified": False,
                        "targetOutputsWritten": 0}
        finally:
            raw.identity(image)
    print(json.dumps(metadata, indent=2, sort_keys=True))
    print("FRESH_WIZARD_DEFERRED_DAMAGE_ORACLE_OK sourceUnchanged=true targetOutputsWritten=0")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_bin")
    parser.add_argument("--expect-naive-attribution", action="store_true")
    args = parser.parse_args()
    main(args.original_bin, args.expect_naive_attribution)
