#!/usr/bin/env python3
"""Exact AE final-command lookahead inspection and five-instruction witness.

Only actual integer instructions around the final-bit branch are executed.
No full collider/GTE/player update, resource promotion or runtime claim.
"""
import contextlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
DIAGNOSTIC = 'src/Spyro.Editor.NativeId65FreshWizardBodySmoke/source_fresh_chest_spawn_collider_diagnostic.py'
MACHINE = 'src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py'
PINS = {
    DIAGNOSTIC: 'a66004ad9fd42b5c20580adb3d8be2783f6e65b65f32e5bcaf94a47e6c057b3b',
    MACHINE: '75f6762822c79520176bb3127e9b426822fb7b007b2ba6da931e4822cb43f283',
}


def load(relative, name):
    import hashlib
    require(hashlib.sha256((ROOT / relative).read_bytes()).hexdigest() == PINS[relative], 'Exact existing helper definition')
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def require(ok, label):
    if not ok: raise AssertionError(label)


class TraceCode(dict):
    def __init__(self, code): super().__init__(code); self.trace = []
    def __getitem__(self, pc): self.trace.append(pc); return super().__getitem__(pc)


def branch_witness(Machine, code, neighbor, *, final=True, readable=True):
    # Explicit cut after type1 consumed its command/offset/flags. Actual final
    # command remains in v0; at points exactly one dword after the AE package.
    # Synthetic next-row link is only used to retire both load delays.
    tail, row = 0x800A7CAC, 0x800A8680
    trace = TraceCode(code); machine = Machine(trace)
    machine.r = [0] + [0x61000000 + i * 0x10101 for i in range(1, 32)]
    machine.r[1], machine.r[2], machine.r[30] = tail, 0x02580101 if final else 0x02580100, row
    machine.map(row + 4, 4)
    if readable: machine.blob(tail, struct.pack('<I', neighbor))
    before, memory = machine.r.copy(), machine.memory.copy()
    stop = machine.run(0x8004E590, {0x8004E51C, 0x8004E5AC}, maximum=12)
    if final:
        expected = before.copy(); expected[2], expected[30] = neighbor, 0
        require(stop == 0x8004E51C and trace.trace == [0x8004E590, 0x8004E594, 0x8004E598, 0x8004E514, 0x8004E518], 'Actual taken branch, executed LW delay and complete load retirement')
        require(machine.r == expected and machine.reads == [(tail, 4), (row + 4, 4)], 'No adjacent command dispatch; only exact lookahead and next-link reads')
    else:
        require(stop == 0x8004E5AC and machine.r[10] == (neighbor & 0xFF00) >> 5, 'Cleared-final-bit control enters next-command dispatch')
    require(machine.memory == memory and machine.writes == [] and machine.calls == [] and machine.hi == machine.lo == 0, 'Complete memory/call/HI/LO complement at bounded cut')
    return {'neighborWord': f'{neighbor:08X}', 'finalBitSet': final, 'stopPc': f'{stop:08X}',
            'executedPcs': [f'{pc:08X}' for pc in trace.trace], 'dataReads': [[f'{at:08X}', size] for at, size in machine.reads]}


def registration_at(header, at):
    found = [(i, struct.unpack_from('<H', header, 0x150 + 2 * i)[0])
             for i in range(64) if struct.unpack_from('<I', header, 0x50 + 4 * i)[0] == at]
    require(len(found) == 1, 'One actual next-package registration at adjacent boundary')
    return {'slot': found[0][0], 'nativeClass': f'{found[0][1]:X}', 'dataOffset': f'{at:X}'}


def main():
    d, native = load(DIAGNOSTIC, 'frozen_spawn_diagnostic'), load(MACHINE, 'frozen_lookahead_machine')
    self_before = Path(__file__).read_bytes()
    line, observation = d.read_observation()
    held = []
    with contextlib.ExitStack() as stack:
        try:
            for name, path, digest in d.PROFILES:
                stream = stack.enter_context(path.open('rb'))
                initial = os.fstat(stream.fileno()); held.append((name, stream, path, digest, initial))
                d.authenticate(stream, path, digest, initial)
            original, candidate = held[0][1], held[1][1]
            source_proof = d.inspect(original, candidate, observation)
            peace_at, candidate_body_at = 37 * 2048 + 0x1890800, 53911 * 2048
            original_tail = d.logical(original, peace_at + 0x1B4980, 16)
            candidate_tail = d.logical(candidate, candidate_body_at + 0xE7000 + 0x1A808, 16)
            original_next = registration_at(d.logical(original, peace_at, 0x800), 0x1B4980)
            candidate_next = registration_at(d.logical(candidate, candidate_body_at, 0x800), 0xE7000 + 0x1A808)
            instructions = d.logical(original, 53875 * 2048 + 0x8004E3C8 - 0x8000F800, 0x7E0)
            require(d.sha(instructions) == d.WINDOWS[1][2], 'Actual authenticated complete collider instruction window')
            code = {0x8004E3C8 + at: d.word(instructions, at) for at in range(0, len(instructions), 4)}
            expected = {0x8004E590: 0x30420001, 0x8004E594: 0x1440FFDF, 0x8004E598: 0x8C220000,
                        0x8004E514: 0x8FDE0004, 0x8004E518: 0, 0x8004EB60: 0x22420000,
                        0x8004E558: 0x94220000, 0x8004E69C: 0x20210008}
            require(all(code[pc] == word for pc, word in expected.items()), 'Final-bit branch/delay, type1 pointer increment and later v0 overwrite instructions')
            words = tuple(dict.fromkeys((d.word(original_tail), d.word(candidate_tail), 0, 0xFFFFFFFF, 0xDEADBEEF, 0xA5A5A5A5, 0x12340301)))
            witnesses = [branch_witness(native.Machine, code, value) for value in words]
            controls = [branch_witness(native.Machine, code, value, final=False) for value in (0, 0x12340301)]
            refused = False
            try: branch_witness(native.Machine, code, 0, readable=False)
            except AssertionError as error:
                require(str(error) == 'Unmapped native read 800A7CAC+4', 'Only the intended unmapped-adjacent-read control may refuse')
                refused = True
            require(refused, 'Readable adjacent dword is genuinely required even for a final command')
            report = {'sourceImageSha256': d.PROFILES[0][2], 'candidateImageSha256': d.PROFILES[1][2],
                      'sourcePackageSha256': source_proof['sourcePackageSha256'], 'descriptorWords': source_proof['descriptorWords'],
                      'originalTailWords': [f'{d.word(original_tail, at):08X}' for at in range(0, 16, 4)],
                      'candidateTailWords': [f'{d.word(candidate_tail, at):08X}' for at in range(0, 16, 4)],
                      'originalAdjacentOwner': original_next, 'candidateAdjacentOwner': candidate_next,
                      'finalCommandWitnesses': witnesses, 'nonfinalNegativeControls': controls, 'unmappedLookaheadRefused': refused,
                      'adjacentWordIsRequiredReadable': True, 'adjacentWordIsTerminatorOrNextCommand': False,
                      'finalCommandNeighborValueAffectsDispatch': False, 'fullColliderExecuted': False,
                      'runtimeVerified': False, 'publicationApproved': False, 'diagnosticSha256': d.sha(self_before)}
        finally:
            failures = []
            for name, stream, path, digest, initial in held:
                try:
                    d.authenticate(stream, path, digest, initial)
                    with path.open('rb') as reopened: d.authenticate(reopened, path, digest, initial)
                except BaseException as error: failures.append(error)
            try:
                require(Path(__file__).read_bytes() == self_before and d.read_observation()[0] == line, 'Diagnostic and exact recorded input unchanged')
                for relative, digest in PINS.items(): require(d.sha((ROOT / relative).read_bytes()) == digest, 'Final helper pin')
                native.require_helpers()
            except BaseException as error: failures.append(error)
            if failures: raise BaseExceptionGroup('Final original/candidate/helper guards', failures)
    report['completeBeforeAfterReopenedImageGuardsPassed'] = True
    print(json.dumps(report, indent=2))
    print('NATIVE_FRESH_CHEST_FINAL_COMMAND_LOOKAHEAD_OK; FIVE_INSTRUCTION_CUT_NOT_FULL_COLLIDER')


if __name__ == '__main__': main()
