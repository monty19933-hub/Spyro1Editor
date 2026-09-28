#!/usr/bin/env python3
"""Exact-USA read-only card completion evidence; no card services or MIPS run.

The snapshot model describes scalar semantics without concurrent interrupts.
It deliberately reports a blocking poll as wouldBlock instead of executing it.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys

spec = importlib.util.spec_from_file_location("_card_completion_selection", Path(__file__).with_name("Inspect-SpyroUsaSelectedSaveAdmission.py"))
SELECTED = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = SELECTED
spec.loader.exec_module(SELECTED)
OWNERSHIP, SOURCE = SELECTED.OWNERSHIP, SELECTED.SOURCE

SPANS = (
    ("exe", "card-poll", 0x80067628, 0xF0, "19553c82e1ac2ff3a3089e3c8b101ade63a08c9620216785f79df4a2d2da3e21"),
    ("exe", "event-result-map", 0x80067C7C, 0x58, "1ea0b56fedceaa9c7937eaf613dc8240131284307df24e5e454e5acba6c1504e"),
    ("exe", "completion-publication", 0x80067CD4, 0xA0, "cbf560c2cda1c506b7aebf714a10a70ff5ea759b7253ea21c91d302a7a12db95"),
    ("exe", "callback-stack", 0x80068F30, 0x110, "4d53f4a9da52c4f13eb3ca72aa0e2bbc6649ce102256123073bbfd3b989fe214"),
    ("exe", "read-state-table", 0x800120C4, 0x7C, "e375bc84a969fcd64e54b212952ff6b77d41d676329831c837f16d8097deb58b"),
    ("exe", "read-card-precheck", 0x800663D8, 0x1E0, "938ca375014e7ad6b69efcc37ea451a3867ebdc874c65a4e39a2529319b9b64c"),
    ("exe", "file-service-veneers", 0x800684B4, 0x40, "1d08e446177a10d74f0375c6a0133279323c307b883bf0168e433e245b94a52b"),
    ("exe", "card-service-start", 0x800662BC, 0x60, "e23d1f4697a7870a5e1e16faef2d1f384818bd3771c69619b2388bdf1ef9fed9"),
    ("exe", "read-filename-prefix", 0x80067D74, 0x5C, "ca057f2642941761ca53f90f4c7032e166e443f607406a745291292890d65ed1"),
    ("exe", "read-filename-concat", 0x800687D4, 0xA8, "1f1aa65c0b63c8f8cd31b7ab2caf5dcf0c7a64e9035201b07a35dda0dd52b056"),
    ("exe", "read-filename-length", 0x8006276C, 0x30, "67eba1fda7d9cdea44c7e0341d218d166b95f8baf32341890895a84f25308943"),
    ("card", "native-read-result-gate", 0x8007B388, 0x20, "c8d71730850e115acea0edaed04b1493fb82df2bd212a47bb7a870af09c6be41"),
    ("card", "selected-poll-output-base", 0x8007C554, 8, "8021f7897c66e5711fcbce0e73bea21b8ee63b05a6c4309c6da8080afb8a6688"),
)

INSTRUCTIONS = {
    "exe": (
        (0x80066E6C, 0x10400005), (0x80066E70, 0x32C2007F), (0x80066E84, 0x1440001B),
        (0x80066EAC, 0x24020003), (0x80066EB0, 0xAE220000),
        (0x80066EB8, 0xAC205B54), (0x80066EC0, 0xAC205B58),
        (0x80066EC8, 0xAC345B64), (0x80066ED0, 0xAC355B6C),
        (0x80066ED8, 0xAC365B68), (0x80066EE0, 0xAC335B5C),
        (0x80066EF0, 0x24020001), (0x80066F04, 0x00001021),
        (0x80066FC8, 0x24020005), (0x80066FCC, 0xAE020000),
        (0x80066FF8, 0x1443FFF9), (0x8006702C, 0x1440FFFA),
        (0x80067044, 0x10400021), (0x8006707C, 0x28420010),
        (0x800670A8, 0xAC225B54), (0x800670BC, 0xAC235B60), (0x800670C4, 0x24020001),
        (0x80067638, 0x14400006), (0x8006764C, 0x10400030), (0x80067650, 0x2402FFFF),
        (0x80067668, 0x1480000C), (0x8006768C, 0x1040FFFD),
        (0x800676B8, 0xACC30000), (0x800676C0, 0x00001021), (0x800676C8, 0xACA70000),
        (0x800676E4, 0xACC20000), (0x80067700, 0xACA20000),
        (0x80067708, 0xAC205B58), (0x8006770C, 0x24020001),
        (0x80067D10, 0xAC225B58), (0x80067D20, 0xAC820000), (0x80067D38, 0xAC225B98),
        (0x80067D3C, 0xAC600000), (0x80067D44, 0xAC205B54),
        (0x80066334, 0x1440FFFD), (0x800662D4, 0xAC400000),
        (0x800662E8, 0xAC205B58), (0x80066308, 0x24040007),
    ),
    "card": (
        (0x8007B390, 0x8E050004), (0x8007B398, 0x14A0006B),
        (0x8007C554, 0x3C118008), (0x8007C558, 0x26318D7C),
        (0x8007C7A0, 0x00002021), (0x8007C7A4, 0x2625003C), (0x8007C7C0, 0x26260040),
        (0x8007C884, 0x00002021), (0x8007C888, 0x2625003C), (0x8007C8B0, 0x26260040),
        (0x8007CA3C, 0x00002021), (0x8007CA40, 0x2625003C), (0x8007CA5C, 0x26260040),
        (0x8007CA98, 0x0C019D8A), (0x8007CAA0, 0x0801F312),
        (0x8007CC00, 0x0C0198C7), (0x8007CC20, 0xAC338D78),
    ),
}


def u32(value):
    if type(value) is not int or not 0 <= value <= 0xFFFFFFFF:
        raise ValueError("Unsigned scalar fixture word required")
    return value


def poll_snapshot(mode, active, current_result, completion, completed_operation, completed_result):
    """Non-concurrent translation of 80067628; no services/interrupts are run.

    None outputs mean the native function would not store to those pointers.
    The native function also permits null output pointers, omitted in this model.
    """
    for value in (mode, active, current_result, completion, completed_operation, completed_result):
        u32(value)
    if active == 0 and completion == 0:
        return dict(returnValue=-1, operation=None, result=None, completionAfter=0, wouldBlock=False)
    if completion:
        return dict(returnValue=1, operation=completed_operation, result=completed_result,
                    completionAfter=0, wouldBlock=False)
    if mode == 0:
        return dict(returnValue=None, operation=None, result=None, completionAfter=0, wouldBlock=True)
    return dict(returnValue=0, operation=active, result=current_result, completionAfter=0, wouldBlock=False)


def native_read_accepts(active, count):
    """Only the observed busy and low-seven-count-bit native admission tests."""
    return u32(active) == 0 and (u32(count) & 0x7F) == 0


def mapped_event_result(raw):
    """80067C7C numeric mapping; does not guess unverified symbolic errors."""
    u32(raw)
    return {0: 0, 1: 2, 2: 1, 4: 3}.get(raw, raw | 0x8000)


def evidence_limits():
    return dict(staticSourceEvidenceOnly=True, snapshotModelsAreNotCpuExecution=True,
                mipsExecuted=False, biosExecuted=False, cardServicesExecuted=False,
                boundedCompletionProven=False, fullCardIoClosed=False,
                hooksImplemented=False, readyAdmissionAuthorized=False,
                writesCard=False, writesImage=False, publicationApproved=False)


def capture(path):
    image = SOURCE.AuthenticatedImage(path, SOURCE.USA)
    try:
        exe = image.read(SOURCE.USA.exe_lba, 0, SOURCE.USA.exe_size)
        card = image.read(37, 0x5B800, 0x3800)
        if hashlib.sha256(exe).hexdigest() != SOURCE.USA.exe_digest or hashlib.sha256(card).hexdigest() != OWNERSHIP.CARD_SHA:
            raise ValueError("Exact executable/card body identity changed")
        owners = {"exe": (exe, OWNERSHIP.BASE), "card": (card, OWNERSHIP.CARD_BASE)}
        evidence = [dict(owner=owner, **SOURCE.describe_span(owners[owner][0], label, pc - owners[owner][1], size, owners[owner][1], digest))
                    for owner, label, pc, size, digest in (*OWNERSHIP.SPANS, *SELECTED.SPANS, *SPANS)]
        instructions = [dict(owner=owner, **row) for owner, expected in INSTRUCTIONS.items()
                        for row in SELECTED.require_instructions(*owners[owner], expected)]
        at = 0x800120C4 - OWNERSHIP.BASE
        table = struct.unpack_from("<31I", exe, at)
        expected = [0x800670C8] * 31
        for state, target in ((0, 0x80066F74), (10, 0x80066F98), (11, 0x80066FD8), (20, 0x80067008), (30, 0x8006703C)):
            expected[state] = target
        if tuple(expected) != table:
            raise ValueError("Read callback state targets changed")
        probes = {"idle": poll_snapshot(1, 0, 0, 0, 3, 0),
                  "busyNonblocking": poll_snapshot(1, 3, 0, 0, 99, 99),
                  "busyBlocking": poll_snapshot(0, 3, 0, 0, 99, 99),
                  "readSuccess": poll_snapshot(1, 0, 0, 1, 3, 0),
                  "readOpenFailure": poll_snapshot(1, 0, 0, 1, 3, 5),
                  "otherCompletion": poll_snapshot(1, 0, 0, 1, 4, 0)}
        result = dict(sourceSha256=SOURCE.USA.digest, exeSha256=SOURCE.USA.exe_digest,
                      cardOverlaySha256=OWNERSHIP.CARD_SHA, inheritedPins=len(OWNERSHIP.SPANS) + len(SELECTED.SPANS),
                      additionalPins=len(SPANS), evidence=evidence, instructions=instructions,
                      readCallbackTable=[dict(state=i, target=t) for i, t in enumerate(table)],
                      snapshotProbes=probes, readOperationCode=3, resultSuccessCode=0,
                      acceptedRequestReturn=1, completionPollReturn=1, busyPollReturn=0, idlePollReturn=-1,
                      blockingPollMode=0, nativeSelectionPollMode=0,
                      nativeSelectionPollOutputs=[0x80078DB8, 0x80078DBC],
                      pollLocalStackBytes=0, readRequestLocalStackBytes=0x30,
                      successfulSynchronousRequestNestedLocalStackBytes=0x50,
                      stackDepthExcludesInterruptBiosAndDiagnostics=True,
                      nativeReadChecksPositiveCount=False, nativeReadChecksBufferExtent=False,
                      nativeReadPreservesUnconsumedCompletion=False, **evidence_limits())
    finally:
        image.close()
    return dict(heldAndNamedOriginalFinal=True, **result)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_usa_bin", type=Path)
    print(json.dumps(capture(parser.parse_args().original_usa_bin), indent=2))
