#!/usr/bin/env python3
"""Read the three ID65 runtime discriminator words through DuckStation GDB.

This watcher is intentionally narrow: it connects only to DuckStation's local
GDB server on 127.0.0.1:2345, keeps one socket open, and sends only the three
approved ``m`` memory-read packets plus Remote Serial Protocol acknowledgements.
It never writes emulated memory or sends continue, stop, step, register, or
breakpoint commands.

The audited DuckStation build pauses a running game once when a GDB client
connects. After connection, manually resume DuckStation in its UI while leaving
this watcher connected. Subsequent ``m`` reads do not pause emulation.
"""

from __future__ import annotations

import argparse
import io
import math
import socket
import time
from dataclasses import dataclass
from datetime import datetime
from typing import Protocol


HOST = "127.0.0.1"
PORT = 2345
DEFAULT_PERIOD_SECONDS = 0.10
SOCKET_TIMEOUT_SECONDS = 2.0
MAX_RSP_PAYLOAD_BYTES = 4096
MAX_RSP_EVENTS_PER_READ = 32

LIVES_ADDRESS = 0x8007582C
DEATH_STATE_ADDRESS = 0x800757D8
PLAYER_Z_ADDRESS = 0x80078A60
DEATH_PLANE_Z = 0x400

APPROVED_READS = (
    ("lives", LIVES_ADDRESS),
    ("death-state", DEATH_STATE_ADDRESS),
    ("player-z", PLAYER_Z_ADDRESS),
)
APPROVED_ADDRESSES = frozenset(address for _, address in APPROVED_READS)


class RspProtocolError(RuntimeError):
    """The local DuckStation GDB server returned an invalid RSP exchange."""


class SocketLike(Protocol):
    def recv(self, size: int) -> bytes: ...

    def sendall(self, data: bytes) -> None: ...


def positive_period(value: str) -> float:
    try:
        period = float(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("period must be a number") from exc
    if not math.isfinite(period) or not period > 0.0:
        raise argparse.ArgumentTypeError("period must be finite and positive")
    return period


def frame_payload(payload: str) -> bytes:
    raw = payload.encode("ascii")
    checksum = sum(raw) & 0xFF
    return b"$" + raw + b"#" + f"{checksum:02x}".encode("ascii")


def read_exact(stream: SocketLike, length: int) -> bytes:
    result = bytearray()
    while len(result) < length:
        chunk = stream.recv(length - len(result))
        if not chunk:
            raise RspProtocolError("DuckStation closed the GDB connection.")
        result.extend(chunk)
    return bytes(result)


def read_packet_payload(stream: SocketLike) -> bytes:
    payload = bytearray()
    while True:
        value = read_exact(stream, 1)
        if value == b"#":
            break
        if len(payload) >= MAX_RSP_PAYLOAD_BYTES:
            stream.sendall(b"-")
            raise RspProtocolError(
                "GDB reply exceeded the strict "
                f"{MAX_RSP_PAYLOAD_BYTES}-byte payload limit."
            )
        payload.extend(value)

    encoded_checksum = read_exact(stream, 2)
    if not all(
        value in b"0123456789abcdefABCDEF" for value in encoded_checksum
    ):
        stream.sendall(b"-")
        raise RspProtocolError(
            f"Invalid GDB reply checksum {encoded_checksum!r}."
        )
    try:
        received_checksum = int(encoded_checksum, 16)
    except ValueError as exc:
        stream.sendall(b"-")
        raise RspProtocolError(
            f"Invalid GDB reply checksum {encoded_checksum!r}."
        ) from exc

    calculated_checksum = sum(payload) & 0xFF
    if received_checksum != calculated_checksum:
        stream.sendall(b"-")
        raise RspProtocolError(
            "GDB reply checksum mismatch: "
            f"received {received_checksum:02x}, calculated {calculated_checksum:02x}."
        )

    stream.sendall(b"+")
    return bytes(payload)


def is_stop_notification(payload: bytes) -> bool:
    if payload.startswith(b"S"):
        signal = payload[1:]
        return len(signal) == 2 and all(
            value in b"0123456789abcdefABCDEF" for value in signal
        )
    if payload.startswith(b"T"):
        signal = payload[1:3]
        return len(signal) == 2 and all(
            value in b"0123456789abcdefABCDEF" for value in signal
        )
    return False


def read_data_reply(stream: SocketLike) -> bytes:
    """Consume bounded ACK/notification events until one read reply arrives."""

    acknowledgement_seen = False
    for _ in range(MAX_RSP_EVENTS_PER_READ):
        event = read_exact(stream, 1)
        if event == b"+":
            acknowledgement_seen = True
            continue
        if event == b"-":
            raise RspProtocolError("DuckStation negatively acknowledged the read request.")
        if event != b"$":
            raise RspProtocolError(
                f"Unexpected GDB event byte while awaiting a read reply: {event!r}."
            )

        payload = read_packet_payload(stream)
        if is_stop_notification(payload):
            continue
        if not acknowledgement_seen:
            raise RspProtocolError(
                "DuckStation returned read data before acknowledging the request."
            )
        return payload

    raise RspProtocolError(
        f"GDB read exceeded the {MAX_RSP_EVENTS_PER_READ}-event safety limit."
    )


class ReadOnlyRspClient:
    """Whitelisted four-byte reads; no generic command API is exposed."""

    def __init__(self, stream: SocketLike) -> None:
        self._stream = stream

    def read_word(self, address: int) -> bytes:
        if address not in APPROVED_ADDRESSES:
            raise ValueError(f"Address 0x{address:08X} is not an approved ID65 read.")

        payload = f"m{address:08x},4"
        self._stream.sendall(frame_payload(payload))
        reply = read_data_reply(self._stream)
        if (
            len(reply) == 3
            and reply.startswith(b"E")
            and all(value in b"0123456789abcdefABCDEF" for value in reply[1:])
        ):
            try:
                error_text = reply.decode("ascii")
            except UnicodeDecodeError:
                error_text = repr(reply)
            raise RspProtocolError(
                f"DuckStation rejected read at 0x{address:08X}: {error_text}."
            )
        if len(reply) != 8:
            raise RspProtocolError(
                f"Read at 0x{address:08X} returned {len(reply)} hex digits; expected 8."
            )
        try:
            raw = bytes.fromhex(reply.decode("ascii"))
        except (UnicodeDecodeError, ValueError) as exc:
            raise RspProtocolError(
                f"Read at 0x{address:08X} returned non-hex data {reply!r}."
            ) from exc
        if len(raw) != 4:
            raise RspProtocolError(
                f"Read at 0x{address:08X} returned {len(raw)} bytes; expected 4."
            )
        return raw


@dataclass(frozen=True)
class RuntimeSample:
    lives: int
    death_state: int
    player_z: int
    lives_raw: bytes
    death_state_raw: bytes
    player_z_raw: bytes


def decode_sample(lives_raw: bytes, death_state_raw: bytes, player_z_raw: bytes) -> RuntimeSample:
    for name, value in (
        ("lives", lives_raw),
        ("death state", death_state_raw),
        ("player Z", player_z_raw),
    ):
        if len(value) != 4:
            raise ValueError(f"{name} must contain exactly four bytes.")

    return RuntimeSample(
        lives=int.from_bytes(lives_raw, "little", signed=False),
        death_state=int.from_bytes(death_state_raw, "little", signed=False),
        player_z=int.from_bytes(player_z_raw, "little", signed=True),
        lives_raw=lives_raw,
        death_state_raw=death_state_raw,
        player_z_raw=player_z_raw,
    )


def death_state_label(value: int) -> str:
    if value == 0:
        return "gameplay/non-death"
    if value == 4:
        return "death-respawn"
    if value == 5:
        return "game-over"
    return f"other-{value}"


def z_marker(value: int) -> str:
    return "Z<0x400:YES" if value < DEATH_PLANE_Z else "Z<0x400:no"


def capture_sample(client: ReadOnlyRspClient) -> RuntimeSample:
    # These are intentionally three separate approved m packets. They are
    # near-contemporaneous, not an atomic same-frame snapshot.
    lives_raw = client.read_word(LIVES_ADDRESS)
    death_state_raw = client.read_word(DEATH_STATE_ADDRESS)
    player_z_raw = client.read_word(PLAYER_Z_ADDRESS)
    return decode_sample(lives_raw, death_state_raw, player_z_raw)


def print_sample(sample: RuntimeSample) -> None:
    timestamp = datetime.now().astimezone().isoformat(timespec="milliseconds")
    print(
        f"{timestamp} "
        f"lives={sample.lives} "
        f"death-state={sample.death_state}({death_state_label(sample.death_state)}) "
        f"player-Z={sample.player_z} [{z_marker(sample.player_z)}] "
        "raw="
        f"{sample.lives_raw.hex()}/"
        f"{sample.death_state_raw.hex()}/"
        f"{sample.player_z_raw.hex()}",
        flush=True,
    )


class FakeSocket:
    """In-memory SocketLike used only by --self-test."""

    def __init__(self, incoming: bytes) -> None:
        self._incoming = io.BytesIO(incoming)
        self.sent: list[bytes] = []

    def recv(self, size: int) -> bytes:
        return self._incoming.read(size)

    def sendall(self, data: bytes) -> None:
        self.sent.append(bytes(data))


def reply_frame(payload: bytes, *, checksum: int | None = None) -> bytes:
    actual_checksum = (sum(payload) & 0xFF) if checksum is None else checksum
    return b"$" + payload + b"#" + f"{actual_checksum:02x}".encode("ascii")


def self_test() -> None:
    expected_frames = {
        LIVES_ADDRESS: b"$m8007582c,4#9e",
        DEATH_STATE_ADDRESS: b"$m800757d8,4#a4",
        PLAYER_Z_ADDRESS: b"$m80078a60,4#9b",
    }
    for address, expected in expected_frames.items():
        actual = frame_payload(f"m{address:08x},4")
        assert actual == expected, (actual, expected)

    decoded = decode_sample(
        bytes.fromhex("04000000"),
        bytes.fromhex("05000000"),
        bytes.fromhex("00fcffff"),
    )
    assert decoded.lives == 4
    assert decoded.death_state == 5
    assert decoded.player_z == -1024

    assert death_state_label(0) == "gameplay/non-death"
    assert death_state_label(4) == "death-respawn"
    assert death_state_label(5) == "game-over"
    assert death_state_label(9) == "other-9"
    assert z_marker(1023) == "Z<0x400:YES"
    assert z_marker(1024) == "Z<0x400:no"
    assert reply_frame(b"S05") == b"$S05#b8"

    good_reply = b"04000000"

    # The installed server queues a standalone resume ACK before its ACK for
    # the first m request after the operator resumes through the UI.
    multiple_acks = FakeSocket(b"++" + reply_frame(good_reply))
    raw = ReadOnlyRspClient(multiple_acks).read_word(LIVES_ADDRESS)
    assert raw == bytes.fromhex("04000000")
    assert multiple_acks.sent == [expected_frames[LIVES_ADDRESS], b"+"]

    # A later UI pause can queue an asynchronous stop before the m data. Both
    # packets must be checksum-ACKed while only the data satisfies the read.
    stop_then_data = FakeSocket(
        b"+" + reply_frame(b"S05") + b"+" + reply_frame(good_reply)
    )
    raw = ReadOnlyRspClient(stop_then_data).read_word(LIVES_ADDRESS)
    assert raw == bytes.fromhex("04000000")
    assert stop_then_data.sent == [expected_frames[LIVES_ADDRESS], b"+", b"+"]

    extended_stop_then_data = FakeSocket(
        b"+" + reply_frame(b"T05thread:1;") + b"+" + reply_frame(good_reply)
    )
    raw = ReadOnlyRspClient(extended_stop_then_data).read_word(LIVES_ADDRESS)
    assert raw == bytes.fromhex("04000000")
    assert extended_stop_then_data.sent == [
        expected_frames[LIVES_ADDRESS],
        b"+",
        b"+",
    ]

    negative_ack = FakeSocket(b"-")
    try:
        ReadOnlyRspClient(negative_ack).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "negatively acknowledged" in str(exc)
    else:
        raise AssertionError("A GDB negative acknowledgement was accepted.")
    assert negative_ack.sent == [expected_frames[LIVES_ADDRESS]]

    oversized = FakeSocket(b"+$" + b"A" * (MAX_RSP_PAYLOAD_BYTES + 1))
    try:
        ReadOnlyRspClient(oversized).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "payload limit" in str(exc)
    else:
        raise AssertionError("An oversized unterminated GDB packet was accepted.")
    assert oversized.sent == [expected_frames[LIVES_ADDRESS], b"-"]

    eof = FakeSocket(b"+")
    try:
        ReadOnlyRspClient(eof).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "closed" in str(exc)
    else:
        raise AssertionError("EOF while awaiting a GDB reply was accepted.")
    assert eof.sent == [expected_frames[LIVES_ADDRESS]]

    bad_checksum = FakeSocket(b"+" + reply_frame(good_reply, checksum=0x00))
    try:
        ReadOnlyRspClient(bad_checksum).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "checksum mismatch" in str(exc)
    else:
        raise AssertionError("A bad fake reply checksum was accepted.")
    assert bad_checksum.sent == [expected_frames[LIVES_ADDRESS], b"-"]

    malformed_checksum = FakeSocket(b"+$" + good_reply + b"#zz")
    try:
        ReadOnlyRspClient(malformed_checksum).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "Invalid GDB reply checksum" in str(exc)
    else:
        raise AssertionError("A malformed GDB reply checksum was accepted.")
    assert malformed_checksum.sent == [expected_frames[LIVES_ADDRESS], b"-"]

    missing_ack = FakeSocket(reply_frame(good_reply))
    try:
        ReadOnlyRspClient(missing_ack).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "before acknowledging" in str(exc)
    else:
        raise AssertionError("Read data without a preceding ACK was accepted.")
    assert missing_ack.sent == [expected_frames[LIVES_ADDRESS], b"+"]

    unexpected_event = FakeSocket(b"+?")
    try:
        ReadOnlyRspClient(unexpected_event).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "Unexpected GDB event byte" in str(exc)
    else:
        raise AssertionError("An unexpected GDB event byte was accepted.")
    assert unexpected_event.sent == [expected_frames[LIVES_ADDRESS]]

    nonhex_reply = FakeSocket(b"+" + reply_frame(b"nothex!!"))
    try:
        ReadOnlyRspClient(nonhex_reply).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "non-hex" in str(exc)
    else:
        raise AssertionError("A non-hex four-byte GDB reply was accepted.")
    assert nonhex_reply.sent == [expected_frames[LIVES_ADDRESS], b"+"]

    short_reply = FakeSocket(b"+" + reply_frame(b"040000"))
    try:
        ReadOnlyRspClient(short_reply).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "expected 8" in str(exc)
    else:
        raise AssertionError("A short GDB memory reply was accepted.")
    assert short_reply.sent == [expected_frames[LIVES_ADDRESS], b"+"]

    error_payload = b"E00"
    error_reply = FakeSocket(b"+" + reply_frame(error_payload))
    try:
        ReadOnlyRspClient(error_reply).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "E00" in str(exc)
    else:
        raise AssertionError("A fake GDB error reply was accepted.")
    assert error_reply.sent == [expected_frames[LIVES_ADDRESS], b"+"]

    # An eight-digit memory value may legitimately begin with uppercase E;
    # only the exact three-byte Ehh RSP shape is an error reply.
    valid_e_prefixed_data = FakeSocket(b"+" + reply_frame(b"E0000000"))
    raw = ReadOnlyRspClient(valid_e_prefixed_data).read_word(LIVES_ADDRESS)
    assert raw == bytes.fromhex("E0000000")
    assert valid_e_prefixed_data.sent == [expected_frames[LIVES_ADDRESS], b"+"]

    too_many_events = FakeSocket(b"+" * MAX_RSP_EVENTS_PER_READ)
    try:
        ReadOnlyRspClient(too_many_events).read_word(LIVES_ADDRESS)
    except RspProtocolError as exc:
        assert "event safety limit" in str(exc)
    else:
        raise AssertionError("An unbounded ACK stream was accepted.")
    assert too_many_events.sent == [expected_frames[LIVES_ADDRESS]]

    unapproved = FakeSocket(b"")
    try:
        ReadOnlyRspClient(unapproved).read_word(0x80000000)
    except ValueError as exc:
        assert "not an approved ID65 read" in str(exc)
    else:
        raise AssertionError("An unapproved address was accepted.")
    assert unapproved.sent == []


def run_watch(period: float) -> int:
    print(
        "WARNING: connecting to this DuckStation GDB server pauses a running "
        "game exactly once.",
        flush=True,
    )
    print(
        "This watcher sends only the three approved m reads and protocol ACKs; "
        "it cannot resume the game.",
        flush=True,
    )

    with socket.create_connection(
        (HOST, PORT), timeout=SOCKET_TIMEOUT_SECONDS
    ) as connection:
        connection.settimeout(SOCKET_TIMEOUT_SECONDS)
        client = ReadOnlyRspClient(connection)
        print(
            f"Connected once to {HOST}:{PORT}. DuckStation is now paused.",
            flush=True,
        )
        input(
            "Manually Resume in DuckStation, leave this watcher connected, "
            f"then press Return here to begin the {period:g}-second-period watch: "
        )
        print(
            "Watching one near-contemporaneous tuple per period; "
            "press Ctrl-C to stop cleanly.",
            flush=True,
        )
        while True:
            started = time.monotonic()
            print_sample(capture_sample(client))
            remaining = period - (time.monotonic() - started)
            if remaining > 0.0:
                time.sleep(remaining)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--period",
        type=positive_period,
        default=DEFAULT_PERIOD_SECONDS,
        metavar="SECONDS",
        help="positive interval between samples (default: 0.10 seconds / 10 Hz)",
    )
    parser.add_argument(
        "--self-test",
        action="store_true",
        help="run offline protocol/decoding checks without opening a socket",
    )
    args = parser.parse_args()

    if args.self_test:
        self_test()
        print("WATCH_SPYRO_ID65_RUNTIME_STATE_SELF_TEST_OK", flush=True)
        return 0

    try:
        return run_watch(args.period)
    except KeyboardInterrupt:
        print(
            "\nStopped cleanly; the socket is closed and no control packet was sent. "
            "If DuckStation was not manually resumed, it remains paused.",
            flush=True,
        )
        return 0
    except (OSError, EOFError, RspProtocolError, ValueError) as exc:
        print(
            f"ERROR: {exc} If connection occurred before this error, "
            "manually resume DuckStation because disconnect does not restore it.",
            flush=True,
        )
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
