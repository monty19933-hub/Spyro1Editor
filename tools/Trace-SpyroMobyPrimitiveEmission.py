#!/usr/bin/env python3
"""Trace Spyro 1 Moby primitive emission through DuckStation's GDB server.

This is a focused research helper for the USA retail executable. It watches a
Moby's native ``m_WasDrawn`` byte, then follows that exact Moby through the
whole-model clip test and the shaded-model face loop. The resulting primitive
cursor delta distinguishes a successfully queued Moby from one that actually
emitted GPU packets.

DuckStation is always resumed in ``finally`` after installed breakpoints and
watchpoints are removed.
"""

from __future__ import annotations

import argparse
import json
import socket
import sys
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable


LEVEL_MOBYS_POINTER = 0x80075828
PRIMITIVE_CURSOR_POINTER = 0x800757B0
MOBY_STRIDE = 0x58
WAS_DRAWN_OFFSET = 0x51

WAS_DRAWN_SUCCESS_PC = 0x80022D9C
WHOLE_MODEL_CLIP_PC = 0x800230D0
WHOLE_MODEL_CLIP_DISPATCH_PC = 0x80023080
PRIMITIVE_START_PC = 0x80023290
PRIMITIVE_FINISH_PC = 0x80023978

REGISTER_S1 = 17
REGISTER_S7 = 23
REGISTER_T9 = 25
REGISTER_FP = 30
REGISTER_PC = 37


class GdbRemoteError(RuntimeError):
    pass


class GdbRemote:
    def __init__(self, host: str, port: int, timeout: float) -> None:
        self._socket = socket.create_connection((host, port), timeout=timeout)
        self._socket.settimeout(timeout)
        self._installed: set[tuple[int, int, int]] = set()
        self._resumed = False

    def close(self) -> None:
        self._socket.close()

    @staticmethod
    def _frame(payload: str) -> bytes:
        raw = payload.encode("ascii")
        return b"$" + raw + b"#" + f"{sum(raw) & 0xFF:02x}".encode("ascii")

    def _read_byte(self) -> bytes:
        value = self._socket.recv(1)
        if not value:
            raise GdbRemoteError("DuckStation closed the GDB connection.")
        return value

    def _read_packet(self) -> str:
        while self._read_byte() != b"$":
            pass
        payload = bytearray()
        while True:
            value = self._read_byte()
            if value == b"#":
                break
            payload.extend(value)
        checksum = self._socket.recv(2)
        if len(checksum) != 2:
            raise GdbRemoteError("Truncated GDB packet checksum.")
        expected = int(checksum, 16)
        actual = sum(payload) & 0xFF
        if actual != expected:
            self._socket.sendall(b"-")
            raise GdbRemoteError(
                f"GDB checksum mismatch: expected {expected:02X}, calculated {actual:02X}."
            )
        self._socket.sendall(b"+")
        return payload.decode("ascii")

    def command(self, payload: str, *, wait_for_reply: bool = True) -> str | None:
        self._socket.sendall(self._frame(payload))
        acknowledgement = self._read_byte()
        if acknowledgement != b"+":
            raise GdbRemoteError(
                f"GDB did not acknowledge {payload!r}: received {acknowledgement!r}."
            )
        if not wait_for_reply:
            return None
        reply = self._read_packet()
        if reply.startswith("E"):
            raise GdbRemoteError(f"GDB command {payload!r} failed with {reply}.")
        return reply

    def stop(self) -> str:
        reply = self.command("?")
        assert reply is not None
        return reply

    def continue_until_stop(self) -> str:
        reply = self.command("c")
        assert reply is not None
        return reply

    def resume_without_waiting(self) -> None:
        self.command("c", wait_for_reply=False)
        self._resumed = True

    def read_memory(self, address: int, length: int) -> bytes:
        reply = self.command(f"m{address:x},{length:x}")
        assert reply is not None
        raw = bytes.fromhex(reply)
        if len(raw) != length:
            raise GdbRemoteError(
                f"Read {len(raw)} bytes at 0x{address:08X}; expected {length}."
            )
        return raw

    def read_u32(self, address: int) -> int:
        return int.from_bytes(self.read_memory(address, 4), "little")

    def read_registers(self) -> list[int]:
        reply = self.command("g")
        assert reply is not None
        if len(reply) % 8:
            raise GdbRemoteError(f"Unexpected register packet length: {len(reply)} hex digits.")
        return [
            int.from_bytes(bytes.fromhex(reply[index : index + 8]), "little")
            for index in range(0, len(reply), 8)
        ]

    def install(self, kind: int, address: int, length: int) -> None:
        key = (kind, address, length)
        if key in self._installed:
            return
        reply = self.command(f"Z{kind},{address:x},{length:x}")
        if reply != "OK":
            raise GdbRemoteError(
                f"Could not install GDB point kind {kind} at 0x{address:08X}: {reply}."
            )
        self._installed.add(key)

    def remove(self, kind: int, address: int, length: int) -> None:
        key = (kind, address, length)
        if key not in self._installed:
            return
        reply = self.command(f"z{kind},{address:x},{length:x}")
        if reply != "OK":
            raise GdbRemoteError(
                f"Could not remove GDB point kind {kind} at 0x{address:08X}: {reply}."
            )
        self._installed.remove(key)

    def remove_all(self) -> list[str]:
        errors: list[str] = []
        for kind, address, length in list(self._installed):
            try:
                self.remove(kind, address, length)
            except Exception as exc:  # best-effort cleanup must continue
                errors.append(str(exc))
        return errors


@dataclass(frozen=True)
class PrimitiveSample:
    sample: int
    moby_address: str
    whole_model_clip_path: str
    whole_model_clip_mask: str
    outcome: str
    primitive_start: str | None
    primitive_finish: str | None
    primitive_bytes: int | None


def require_pc(registers: list[int], expected: int, context: str) -> None:
    actual = registers[REGISTER_PC]
    if actual != expected:
        raise GdbRemoteError(
            f"Stopped at 0x{actual:08X}, expected 0x{expected:08X} during {context}."
        )


def trace_target(
    remote: GdbRemote,
    base: int,
    true_index: int,
    sample_count: int,
) -> list[PrimitiveSample]:
    target = base + true_index * MOBY_STRIDE
    was_drawn = target + WAS_DRAWN_OFFSET
    samples: list[PrimitiveSample] = []

    remote.install(2, was_drawn, 1)
    while len(samples) < sample_count:
        remote.continue_until_stop()
        watch_registers = remote.read_registers()
        watch_pc = watch_registers[REGISTER_PC]
        watch_value = remote.read_memory(was_drawn, 1)[0]
        if watch_pc != WAS_DRAWN_SUCCESS_PC or watch_value != 1:
            continue
        if watch_registers[REGISTER_FP] != target:
            raise GdbRemoteError(
                "Successful m_WasDrawn watchpoint did not retain the watched Moby: "
                f"fp=0x{watch_registers[REGISTER_FP]:08X}, target=0x{target:08X}."
            )

        remote.remove(2, was_drawn, 1)
        remote.install(0, WHOLE_MODEL_CLIP_DISPATCH_PC, 4)
        remote.continue_until_stop()
        dispatch_registers = remote.read_registers()
        require_pc(
            dispatch_registers,
            WHOLE_MODEL_CLIP_DISPATCH_PC,
            "whole-model clip dispatch",
        )
        if dispatch_registers[REGISTER_FP] != target:
            raise GdbRemoteError(
                "Whole-model clip dispatch did not retain the watched Moby: "
                f"fp=0x{dispatch_registers[REGISTER_FP]:08X}, target=0x{target:08X}."
            )
        clip_dispatch = "bypassed-s7-zero"
        clip_mask = 0
        remote.remove(0, WHOLE_MODEL_CLIP_DISPATCH_PC, 4)

        if dispatch_registers[REGISTER_S7] != 0:
            clip_dispatch = "tested-s7-nonzero"
            remote.install(0, WHOLE_MODEL_CLIP_PC, 4)
            remote.continue_until_stop()
            clip_registers = remote.read_registers()
            require_pc(clip_registers, WHOLE_MODEL_CLIP_PC, "whole-model clip test")
            if clip_registers[REGISTER_FP] != target:
                raise GdbRemoteError(
                    "Whole-model clip breakpoint did not retain the watched Moby: "
                    f"fp=0x{clip_registers[REGISTER_FP]:08X}, target=0x{target:08X}."
                )
            clip_mask = clip_registers[REGISTER_S1] & 0xF
            remote.remove(0, WHOLE_MODEL_CLIP_PC, 4)

        if clip_mask:
            samples.append(
                PrimitiveSample(
                    sample=len(samples) + 1,
                    moby_address=f"0x{target:08X}",
                    whole_model_clip_path=clip_dispatch,
                    whole_model_clip_mask=f"0x{clip_mask:X}",
                    outcome="whole-model-clipped",
                    primitive_start=None,
                    primitive_finish=None,
                    primitive_bytes=None,
                )
            )
            if len(samples) < sample_count:
                remote.install(2, was_drawn, 1)
            continue

        remote.install(0, PRIMITIVE_START_PC, 4)
        remote.continue_until_stop()
        start_registers = remote.read_registers()
        require_pc(start_registers, PRIMITIVE_START_PC, "primitive cursor start")
        if start_registers[REGISTER_FP] != target:
            raise GdbRemoteError("Primitive-start breakpoint changed Moby identity.")
        # 0x80023290 is the instruction immediately after ``lw t9``. The PS1
        # CPU's load delay means a debugger can still expose the preceding T9
        # value here, while the global cursor is already the authoritative
        # start pointer for this Moby.
        primitive_start = remote.read_u32(PRIMITIVE_CURSOR_POINTER)
        remote.remove(0, PRIMITIVE_START_PC, 4)

        remote.install(0, PRIMITIVE_FINISH_PC, 4)
        remote.continue_until_stop()
        finish_registers = remote.read_registers()
        require_pc(finish_registers, PRIMITIVE_FINISH_PC, "primitive cursor finish")
        if finish_registers[REGISTER_FP] != target:
            raise GdbRemoteError("Primitive-finish breakpoint changed Moby identity.")
        primitive_finish = finish_registers[REGISTER_T9]
        remote.remove(0, PRIMITIVE_FINISH_PC, 4)

        primitive_bytes = (primitive_finish - primitive_start) & 0xFFFFFFFF
        if primitive_bytes & 0x80000000:
            raise GdbRemoteError(
                "Primitive cursor moved backwards: "
                f"0x{primitive_start:08X} -> 0x{primitive_finish:08X}."
            )
        samples.append(
            PrimitiveSample(
                sample=len(samples) + 1,
                moby_address=f"0x{target:08X}",
                whole_model_clip_path=clip_dispatch,
                whole_model_clip_mask="0x0",
                outcome="emitted" if primitive_bytes else "all-faces-rejected",
                primitive_start=f"0x{primitive_start:08X}",
                primitive_finish=f"0x{primitive_finish:08X}",
                primitive_bytes=primitive_bytes,
            )
        )
        if len(samples) < sample_count:
            remote.install(2, was_drawn, 1)

    return samples


def summarize(samples: Iterable[PrimitiveSample]) -> dict[str, object]:
    materialized = list(samples)
    outcomes: dict[str, int] = {}
    primitive_bytes: list[int] = []
    for sample in materialized:
        outcomes[sample.outcome] = outcomes.get(sample.outcome, 0) + 1
        if sample.primitive_bytes is not None:
            primitive_bytes.append(sample.primitive_bytes)
    return {
        "samples": len(materialized),
        "outcomes": outcomes,
        "primitiveBytesMinimum": min(primitive_bytes) if primitive_bytes else None,
        "primitiveBytesMaximum": max(primitive_bytes) if primitive_bytes else None,
        "primitiveBytesUnique": sorted(set(primitive_bytes)),
    }


def parse_indices(raw: str) -> list[int]:
    result: list[int] = []
    for item in raw.split(","):
        value = int(item.strip(), 0)
        if value < 0:
            raise argparse.ArgumentTypeError("Moby indices must be non-negative.")
        result.append(value)
    if not result:
        raise argparse.ArgumentTypeError("At least one Moby index is required.")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=2345)
    parser.add_argument("--timeout", type=float, default=30.0)
    parser.add_argument("--indices", type=parse_indices, default=parse_indices("21,22,47"))
    parser.add_argument("--samples", type=int, default=60)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.samples <= 0:
        parser.error("--samples must be positive.")

    remote = GdbRemote(args.host, args.port, args.timeout)
    report: dict[str, object] = {
        "schemaVersion": 1,
        "capturedAtUtc": datetime.now(timezone.utc).isoformat(),
        "host": args.host,
        "port": args.port,
        "requestedSamplesPerMoby": args.samples,
        "targets": [],
    }
    cleanup_errors: list[str] = []
    try:
        report["initialStop"] = remote.stop()
        base = remote.read_u32(LEVEL_MOBYS_POINTER)
        if not 0x80000000 <= base < 0x80200000:
            raise GdbRemoteError(f"Implausible level Moby base pointer 0x{base:08X}.")
        report["levelMobysPointerAddress"] = f"0x{LEVEL_MOBYS_POINTER:08X}"
        report["levelMobysBase"] = f"0x{base:08X}"

        targets: list[dict[str, object]] = []
        for true_index in args.indices:
            target_address = base + true_index * MOBY_STRIDE
            native_class = int.from_bytes(remote.read_memory(target_address + 0x36, 2), "little")
            samples = trace_target(remote, base, true_index, args.samples)
            targets.append(
                {
                    "trueIndex": true_index,
                    "address": f"0x{target_address:08X}",
                    "nativeClass": f"0x{native_class:04X}",
                    "summary": summarize(samples),
                    "samples": [asdict(sample) for sample in samples],
                }
            )
        report["targets"] = targets
    finally:
        cleanup_errors.extend(remote.remove_all())
        try:
            remote.resume_without_waiting()
        except Exception as exc:
            cleanup_errors.append(f"Could not resume DuckStation: {exc}")
        remote.close()

    report["cleanupErrors"] = cleanup_errors
    encoded = json.dumps(report, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    sys.stdout.write(encoded)
    return 0 if not cleanup_errors else 2


if __name__ == "__main__":
    raise SystemExit(main())
