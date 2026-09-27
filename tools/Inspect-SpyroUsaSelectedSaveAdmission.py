#!/usr/bin/env python3
"""Exact-USA, read-only selected-save instruction evidence, never Ready authority.

Reuses the authenticated source owner and all existing save-buffer pins. This
oracle checks static words/targets; it does not execute MIPS, card I/O or hooks.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys

spec = importlib.util.spec_from_file_location("_selected_save_ownership", Path(__file__).with_name("Inspect-SpyroUsaSaveBufferOwnership.py"))
OWNERSHIP = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = OWNERSHIP
spec.loader.exec_module(OWNERSHIP)
SOURCE = OWNERSHIP.SOURCE

# Additional bounded windows only. Existing tuple, creation and resident-exit
# windows remain sourced from OWNERSHIP.SPANS rather than duplicated pins.
SPANS = (
    ("card", "selected-new-or-overwrite", 0x8007C674, 0x388, "fb0d48f5ce71e4b279bddfcb7f88c832c5c8d8ba363e7ce1ab4ee9c65291cbec"),
    ("card", "existing-prefix-repair-entry", 0x8007B430, 0x84, "739c4fc747765274437816a0cedc0a5b412151b736e3ca69b2ca66ee3834bee3"),
    ("card", "new-prefix-repair", 0x8007BFFC, 0x11C, "27349ef3b02b333584af75de95a2ad2c22c4e1d294a4b2b12d9171a624ee5b49"),
    ("card", "unbound-new-game", 0x8007B898, 0xA0, "6bf8dd6f1f2e7b3d76f908615de58d0e6238e266c1fa6e0b6ffc8d3a2a3124d6"),
    ("card", "prefix-result-and-normal-transition", 0x8007BE98, 0x110, "67b536b9752f06aa3814545b3d8f5bbba45d6f11ad34c9652ade05f7ee493694"),
    ("card", "controller-and-error-selection", 0x8007B210, 0x10C, "64bc064e9df03aee100038c7305e8e0c2db6862e6f11be9cec0a667365659461"),
    ("card", "selected-controller-copy", 0x8007C3D4, 0x34, "fbb0e430fe9e7125d79efc5ded4aeed53fbf318cccf365e25025d08b168407b1"),
    ("card", "selection-drain-and-exit", 0x8007CAA8, 0x1A0, "279af890f435ea64434e52b9be17beba30d60c2c16eddcbfacba0306a0c13f96"),
)

INSTRUCTIONS = {
    "card": (
        (0x8007C768, 0x90420040), (0x8007C770, 0x10400009),
        (0x8007C7C8, 0xAC208D94), (0x8007C7DC, 0xAC308D14), (0x8007C7E4, 0x00021100),
        (0x8007C8A8, 0xAC208D94), (0x8007CA50, 0xAE220000),
        (0x8007CA58, 0x8C428DA0), (0x8007CA64, 0xAC328D94), (0x8007CA68, 0x9467007E),
        (0x8007CA70, 0x8C638D8C), (0x8007CA74, 0x00021100), (0x8007CA7C, 0xAC328D14),
        (0x8007CA84, 0xAC228D18), (0x8007CA8C, 0xAC238D1C), (0x8007CA94, 0xAC278D20),
        (0x8007B900, 0xAC208D94), (0x8007B908, 0xAC208D14),
        (0x8007BE48, 0xAC228D20), (0x8007BE50, 0xA462007E),
        (0x8007BEC4, 0x10400360), (0x8007BED4, 0x1440014F), (0x8007BF20, 0x1440013C),
        (0x8007BF4C, 0x1443001E), (0x8007BF70, 0x14430015), (0x8007BF94, 0x1443000C),
        (0x8007BFA0, 0xAC318D78), (0x8007BFA4, 0xAE020000),
        (0x8007B22C, 0xAC208DA0), (0x8007B264, 0xAC228DA0), (0x8007B2EC, 0x2402FFFF),
        (0x8007B308, 0xAC228DA0), (0x8007C3F8, 0xAC228DA0), (0x8007CC20, 0xAC338D78),
    ),
    "exe": (
        (0x80032B54, 0x8C638D94), (0x80032B60, 0x1460000D),
        (0x80032B98, 0x1470000D), (0x80032BD0, 0x14620017),
    ),
}

CALLS = (
    ("card", 0x8007C798, 0x8001277C), ("card", 0x8007C87C, 0x8001277C),
    ("card", 0x8007CA34, 0x80059594), ("card", 0x8007CA98, 0x80067628),
    ("card", 0x8007B478, 0x8001277C), ("card", 0x8007B4AC, 0x80059864),
    ("card", 0x8007BFFC, 0x8001277C), ("card", 0x8007C030, 0x80059864),
    ("card", 0x8007C064, 0x80059864), ("card", 0x8007C098, 0x80059864),
    ("card", 0x8007B8DC, 0x8001277C), ("card", 0x8007BDF0, 0x80067718),
    ("card", 0x8007BE4C, 0x8001277C), ("card", 0x8007BEBC, 0x80067628),
    ("card", 0x8007BF00, 0x80066E28), ("card", 0x8007CC00, 0x8006631C),
    ("exe", 0x80032BD8, 0x8001277C),
)


def require_word_pc(word, pc):
    if type(word) is not int or not 0 <= word <= 0xFFFFFFFF or type(pc) is not int or not 0 <= pc <= 0xFFFFFFFC or pc % 4:
        raise ValueError("Expected an unsigned instruction and aligned 32-bit PC")


def direct_target(word, pc):
    require_word_pc(word, pc)
    if word >> 26 not in (2, 3):
        raise ValueError("Not a direct jump or call")
    return ((pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2)


def branch_target(word, pc):
    require_word_pc(word, pc)
    if word >> 26 not in (4, 5):
        raise ValueError("Not an inspected equality branch")
    signed = (word & 0xFFFF) - (0x10000 if word & 0x8000 else 0)
    return (pc + 4 + signed * 4) & 0xFFFFFFFF


def require_instructions(data, base, expected):
    require_word_pc(0, base)
    if not isinstance(data, bytes) or not data or len(data) % 4 or base + len(data) > 0x100000000 or not expected:
        raise ValueError("Exact immutable bounded instruction owner and contract required")
    seen, records = set(), []
    for pc, word in expected:
        require_word_pc(word, pc)
        if pc in seen or not base <= pc <= base + len(data) - 4:
            raise ValueError("Duplicate instruction or instruction outside owner")
        seen.add(pc)
        actual = struct.unpack_from("<I", data, pc - base)[0]
        if actual != word:
            raise ValueError(f"Selected-save instruction changed at {pc:08X}")
        records.append({"pc": pc, "word": f"{word:08X}"})
    return records


def evidence_limits():
    return {"staticSourceEvidenceOnly": True, "mipsExecuted": False, "cardServicesExecuted": False,
            "readyAdmissionAuthorized": False, "initializerImplemented": False,
            "legacyUnknownCorruptTailMustBePreservedOrRefused": True,
            "fullIndirectDispatchClosed": False, "fullCardIoClosed": False,
            "writesCard": False, "writesImage": False, "publicationApproved": False}


def capture(path):
    image = SOURCE.AuthenticatedImage(path, SOURCE.USA)
    try:
        exe = image.read(SOURCE.USA.exe_lba, 0, SOURCE.USA.exe_size)
        card = image.read(37, 0x5B800, 0x3800)
        directory = image.read(37, 16, 8)
        if hashlib.sha256(exe).hexdigest() != SOURCE.USA.exe_digest or hashlib.sha256(card).hexdigest() != OWNERSHIP.CARD_SHA or directory.hex() != "00b8050000380000":
            raise ValueError("Exact executable/card directory/body identity changed")
        owners = {"exe": (exe, OWNERSHIP.BASE), "card": (card, OWNERSHIP.CARD_BASE)}
        evidence = [dict(owner=owner, **SOURCE.describe_span(owners[owner][0], label, pc - owners[owner][1], size, owners[owner][1], digest))
                    for owner, label, pc, size, digest in (*OWNERSHIP.SPANS, *SPANS)]
        instructions = [dict(owner=owner, **row) for owner, expected in INSTRUCTIONS.items()
                        for row in require_instructions(*owners[owner], expected)]
        def word_at(owner, pc):
            data, base = owners[owner]
            if pc % 4 or not base <= pc <= base + len(data) - 4:
                raise ValueError("Instruction outside source owner")
            return struct.unpack_from("<I", data, pc - base)[0]
        calls = []
        for owner, pc, target in CALLS:
            word = word_at(owner, pc)
            if word >> 26 != 3 or direct_target(word, pc) != target:
                raise ValueError("Selected-save native call changed")
            calls.append({"owner": owner, "pc": pc, "target": target})
        for pc, target in ((0x8007C7E0, 0x8007CA80), (0x8007C8AC, 0x8007CA68)):
            word = word_at("card", pc)
            if word >> 26 != 2 or direct_target(word, pc) != target:
                raise ValueError("New-selection convergence changed")
        if branch_target(word_at("card", 0x8007C770), 0x8007C770) != 0x8007C798:
            raise ValueError("Native empty-slot branch changed")
        filename = b"BASCUS-94228SPYRO\0"
        if card[4:4 + len(filename)] != filename or exe[0x80010DB8 - OWNERSHIP.BASE:0x80010DB8 - OWNERSHIP.BASE + len(filename)] != filename:
            raise ValueError("Ordinary filename identity changed")
        # Literal store-immediate census in these two owners only: this cannot
        # establish absence of computed-address/indirect/other-overlay writers.
        stores = []
        for owner, (data, base) in owners.items():
            for offset in range(0x800 if owner == "exe" else 0x9C, len(data), 4):
                word = struct.unpack_from("<I", data, offset)[0]
                if word >> 26 in (40, 41, 43) and word & 0xFFFF in (0x8D18, 0x8D1C, 0x8D20):
                    stores.append({"owner": owner, "pc": base + offset, "word": f"{word:08X}"})
        if [(r["owner"], r["pc"]) for r in stores] != [("card", pc) for pc in (0x8007BE48, 0x8007CA84, 0x8007CA8C, 0x8007CA94)]:
            raise ValueError("Bounded literal tuple-writer census changed")
        result = {"sourceSha256": SOURCE.USA.digest, "exeSha256": SOURCE.USA.exe_digest,
                  "cardOverlaySha256": OWNERSHIP.CARD_SHA, "existingPins": len(OWNERSHIP.SPANS),
                  "additionalPins": len(SPANS), "evidence": evidence, "instructions": instructions,
                  "calls": calls, "literalTupleStoreCensus": stores,
                  "selectedTupleBoundaryPc": 0x8007CA98,
                  "tupleFields": {"controllerTimes16": 0x80078D18, "ordinarySlot": 0x80078D1C,
                                  "nativeFileIdentityU16InWord": 0x80078D20},
                  "ordinaryBindingFlag": 0x80078D14, "newOrLoadMarker": 0x80078D94,
                  "nativeMarkerValues": {"new": 0, "deserialized": 1},
                  "nativeFileIdentityIsCollisionFree": False, **evidence_limits()}
    finally:
        image.close()
    return dict(heldAndNamedOriginalFinal=True, **result)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_usa_bin", type=Path)
    args = parser.parse_args()
    print(json.dumps(capture(args.original_usa_bin), indent=2))
