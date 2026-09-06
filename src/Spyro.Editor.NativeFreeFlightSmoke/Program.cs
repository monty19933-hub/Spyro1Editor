using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

int checks = 0;
if (args.Length > 1)
    throw new ArgumentException("Usage: NativeFreeFlightSmoke [original.bin]");

const int target = 14;
var windows = NativeFreeFlightPatchBuilder.Build(target);
Require(windows.Count == 11, "bounded eleven-window producer");
Require(windows.All(w => w.Before.Length == w.After.Length), "no executable growth");
Require(windows.SelectMany(w => Enumerable.Range(w.Offset, w.Before.Length)).Distinct().Count()
    == windows.Sum(w => w.Before.Length), "disjoint ownership");
foreach (var window in windows.Where(w => w.Name.StartsWith("enter-", StringComparison.Ordinal)))
foreach (int level in Enumerable.Range(-2, 104).Concat(new[] { int.MinValue, int.MaxValue }))
foreach (int classification in new[] { 0, 1, -1, int.MinValue, int.MaxValue })
{
    var memory = BaseMemory(level);
    memory[0x80075690] = classification;
    var before = Machine.Run(window.Address, window.Before, memory, InitialRegisters());
    var after = Machine.Run(window.Address, window.After, memory, InitialRegisters());
    SameRegistersExcept(before, after, [2, 3, 4]);
    bool flying = classification != 0 || level == 64 || level == target;
    if (window.Name == "enter-cross")
        Require(after.Exit == (flying ? 0x80041FACu : 0x80041FE0u), "native Cross entry includes only target");
    else
        Require(after.Exit == 0x80043FC0 && after.Registers[4] == (flying ? 32 : 15), "native flight animation-state selection");
    if (level != target)
        Require(before.Exit == after.Exit && (window.Name == "enter-cross" || before.Registers[4] == after.Registers[4]),
            "every unrelated level retains native entry, even permission cleared at respawn");
    Require(after.Writes.Count == 0, "entry decision cannot mutate unrelated native state");
}
foreach (int level in Enumerable.Range(-2, 104))
{
    foreach (int ground in new[] { 0, 1, -1, int.MinValue, int.MaxValue })
    {
        var memory = BaseMemory(level);
        memory[0x80078AF4] = ground;
        memory[0x80078CA4] = 0;
        var permission = windows.Single(w => w.Name == "permission");
        var original = Machine.Run(permission.Address, permission.Before, memory, InitialRegisters());
        var changed = Machine.Run(permission.Address, permission.After, memory, InitialRegisters());
        SameRegistersExcept(original, changed, [1, 2, 3]);
        Require(original.Memory[0x80078CA4] == (level == 64 && ground == 0 ? 1 : 0), "native64 permission oracle");
        Require(changed.Memory[0x80078CA4] == ((level == 64 || level == target) && ground == 0 ? 1 : 0),
            "target permission with delayed loads and failed ground guard");
        Require(changed.Writes.All(a => a == 0x80078CA4), "permission writes only existing field");
    }
}
foreach (var window in windows.Where(w => w.Name.StartsWith("height-", StringComparison.Ordinal)))
{
    foreach (int level in new[] { -1, 0, 10, 13, 14, 15, 25, 35, 45, 55, 63, 64, 65, 99, int.MinValue, int.MaxValue })
    foreach (int substate in new[] { 0, 1, 2, -1, int.MinValue, int.MaxValue })
    foreach (int delta in new[] { int.MinValue, -1000000, -513, -1, 0, 511, 512, 513, 1000000, int.MaxValue })
    {
        var memory = BaseMemory(level);
        memory[0x80078AD4] = substate;
        memory[0x80078A60] = delta;
        memory[0x80078BB0] = 0;
        var before = Machine.Run(window.Address, window.Before, memory, InitialRegisters());
        var after = Machine.Run(window.Address, window.After, memory, InitialRegisters());
        SameRegistersExcept(before, after, [2, 3]);
        bool bypass = level == target && substate == 0;
        Require(after.Exit == (bypass ? window.SuccessAddress : before.Exit), "height predicate destination isolation");
        Require(after.Writes.Count == 0, "height predicate no writes");
        if (substate == 0)
        {
            int zRegister = window.Name is "height-glide" or "height-flight" ? 4 : 5;
            Require(after.Registers[zRegister] == delta, "native downstream Z register preserved");
            if (zRegister == 5)
                Require(after.Registers[4] == unchecked((int)0x80078AD4), "native substate pointer retained");
        }
    }
}
var pitch = windows.Single(w => w.Name == "pitch-ceiling");
foreach (int level in new[] { -1, 0, 10, 13, 14, 15, 25, 35, 45, 55, 63, 64, 65, 99, int.MinValue, int.MaxValue })
foreach (int delta in new[] { int.MinValue, -1000000, -307, -306, -305, -1, 0, 1, 1000000, int.MaxValue })
foreach (int angle in new[] { int.MinValue, -801, -800, -1, 0, 1, 76, 77, 799, 800, int.MaxValue })
{
    var memory = BaseMemory(level);
    memory[0x80078B78] = angle;
    int[] registers = InitialRegisters();
    registers[18] = delta;
    registers[21] = unchecked((int)0x80078B70);
    var before = Machine.Run(pitch.Address, pitch.Before, memory, registers);
    var after = Machine.Run(pitch.Address, pitch.After, memory, registers);
    SameRegistersExcept(before, after, [1, 2, 3]);
    Require(after.Registers[18] == before.Registers[18] && after.Registers[19] == before.Registers[19],
        "native s2/s3 values preserved even across target pitch bypass");
    Require(after.Memory[0x80078B78] == (level == target ? Math.Max(angle, -800) : before.Memory[0x80078B78]),
        "only destination releases pitch ceiling; signed native lower limit preserved");
    Require(after.Writes.All(a => a == 0x80078B78), "pitch exact owned write");
}
foreach (int invalid in new[] { -1, 0, 5, 9, 15, 25, 35, 45, 55, 65, 99, int.MaxValue })
    Reject(() => NativeFreeFlightPatchBuilder.Build(invalid), "unsupported destination");

if (args.Length == 1)
{
    var plan = await NativeFreeFlightPatchPlan.CaptureAsync(args[0], target, enabled: true);
    var disabled = await NativeFreeFlightPatchPlan.CaptureAsync(args[0], target, enabled: false);
    byte[] original = plan.CopySourceExecutable();
    byte[] changed = plan.ApplyToExecutable(original);
    Require(!changed.SequenceEqual(original), "enabled actual source changes");
    Require(disabled.ApplyToExecutable(original).SequenceEqual(original), "disabled exact retail");
    Require(disabled.Instructions.Count == 0 && disabled.RequiredPlayerAnimationIds.Count == 0, "disabled declares no writes or added dependencies");
    Require(plan.RevertExecutable(changed).SequenceEqual(original), "exact inverse");
    Require(plan.ApplyToExecutable(original).SequenceEqual(changed), "determinism");
    Require(!plan.PublicationAuthorized && !plan.RuntimeVerified, "no dependency or runtime promotion");
    Require(plan.RequiredPlayerAnimationIds.SequenceEqual(new[] { 33, 34, 35 }), "real root0 requirements");
    Reject(() => plan.ApplyToExecutable(changed), "reject duplicate application");
    Reject(() => plan.RevertExecutable(original), "reject wrong inverse");
    Reject(() => disabled.ApplyToExecutable(changed), "disabled cannot silently retain old flight patch");
    Reject(() => plan.ApplyToExecutable(original[..^1]), "truncated executable");
    foreach (var window in windows)
    {
        Require(original.AsSpan(window.Offset, window.Before.Length).SequenceEqual(window.Before), "actual source window");
        byte[] corrupt = original.ToArray();
        corrupt[window.Offset] ^= 1;
        Reject(() => plan.ApplyToExecutable(corrupt), "preimage corruption");
        byte[] corruptAfter = changed.ToArray();
        corruptAfter[window.Offset] ^= 1;
        Reject(() => plan.RevertExecutable(corruptAfter), "afterimage corruption");
    }
    HashSet<int> owned = windows.SelectMany(w => Enumerable.Range(w.Offset, w.Before.Length)).ToHashSet();
    Require(Enumerable.Range(0, original.Length).All(i => owned.Contains(i) || original[i] == changed[i]), "complete untouched complement");
    byte[] copy = plan.CopySourceExecutable();
    copy[0] ^= 1;
    Require(plan.CopySourceExecutable().SequenceEqual(original), "immutable source clone");
    byte[] unrelated = original.ToArray();
    unrelated[0x200] ^= 1;
    Require(plan.RevertExecutable(plan.ApplyToExecutable(unrelated)).SequenceEqual(unrelated),
        "unrelated composed bytes retained through exact inverse");
    AuditIncomingReferences(args[0], original);
    AuditExitLiveness(original);
    Console.WriteLine($"NATIVE_FREE_FLIGHT_SOURCE_OK checks={checks} plan={plan.PlanSha256}");
}
else
{
    Console.WriteLine($"NATIVE_FREE_FLIGHT_SOURCE_FREE_OK checks={checks}");
}

Dictionary<uint, int> BaseMemory(int level) => new()
{
    [0x8007596C] = level, [0x80075690] = 0, [0x80078AF4] = 0, [0x80078CA4] = 0,
    [0x80078AD4] = 0, [0x80078A60] = 0, [0x80078BB0] = 0,
    [0x80078B78] = 0
};
int[] InitialRegisters() => Enumerable.Range(0, 32)
    .Select(i => i == 0 ? 0 : unchecked((int)(0x51A00000u + (uint)i * 0x1001))).ToArray();
void SameRegistersExcept(Machine.Result before, Machine.Result after, int[] allowed)
{
    Require(Enumerable.Range(0, 32).All(r => allowed.Contains(r) || before.Registers[r] == after.Registers[r]),
        "all unowned registers preserve arbitrary incoming values");
}
void Require(bool value, string label)
{
    if (!value) throw new InvalidDataException("FAILED: " + label);
    checks++;
}
void Reject(Action action, string label)
{
    try { action(); }
    catch (Exception e) when (e is InvalidDataException or ArgumentException) { checks++; return; }
    throw new InvalidDataException("Accepted: " + label);
}

void AuditIncomingReferences(string path, byte[] executable)
{
    List<(byte[] Bytes, uint Base)> domains = [(executable[0x800..], 0x80010000)];
    using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    DiscLayout layout = DiscImage.DetectLayout(path);
    for (int index = 0; index < 35; index++)
    {
        byte[] entry = DiscImage.ReadFileBytes(image, layout, 37, (9 + index * 2) * 8L, 8);
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(entry);
        int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry.AsSpan(4)));
        domains.Add((DiscImage.ReadFileBytes(image, layout, 37, offset, length), 0x8007AA38));
    }
    int words = 0, external = 0, literalPointers = 0, stateCallers = 0, statePointers = 0;
    foreach (var domain in domains)
    {
        for (int offset = 0; offset < domain.Bytes.Length; offset += 4)
        {
            words++;
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(domain.Bytes.AsSpan(offset, 4));
            uint address = domain.Base + (uint)offset;
            if (word == 0x80041670) statePointers++;
            uint? targetAddress = null;
            int opcode = (int)(word >> 26);
            if (opcode is 2 or 3)
                targetAddress = ((address + 4) & 0xF0000000) | ((word & 0x03FFFFFF) << 2);
            else if (opcode is 1 or 4 or 5 or 6 or 7)
                targetAddress = unchecked(address + 4 + (uint)(unchecked((short)word) * 4));
            var owner = windows.FirstOrDefault(w => word >= w.Address && word < w.Address + w.Before.Length);
            if (owner != null)
                literalPointers++;
            if (targetAddress is not uint targetAddressValue)
                continue;
            if (targetAddressValue == 0x80041670)
            {
                stateCallers++;
                Require(opcode == 3 && address == 0x8004A30C, "exact state-function direct caller");
            }
            var targetOwner = windows.FirstOrDefault(w => targetAddressValue >= w.Address && targetAddressValue < w.Address + w.Before.Length);
            if (targetOwner == null || address >= targetOwner.Address && address < targetOwner.Address + targetOwner.Before.Length)
                continue;
            external++;
            Require(targetAddressValue == targetOwner.Address, "no external direct branch into changed window interior");
        }
    }
    Require(domains.Count == 36 && words == 602112 && external == 6 && literalPointers == 0,
        "source-wide bounded direct-reference linkage baseline");
    Require(stateCallers == 1 && statePointers == 0, "source-wide state caller liveness linkage");
}

void AuditExitLiveness(byte[] executable)
{
    // Inspect actual native instructions, not an ABI assumption. The only
    // direct caller of the state function is separately counted in the
    // source-wide scan; its continuation is followed on the epilogue path.
    foreach (uint exit in new uint[] { 0x80043FC0, 0x80041FAC, 0x80041FE0,
        0x800429D8, 0x800429E0, 0x80043FC8, 0x8004387C, 0x8004388C, 0x800438D8,
        0x80043AC4, 0x80043AD4, 0x80043B14, 0x80043D28, 0x80043D38, 0x80043D78 })
    foreach (int register in new[] { 2, 3 })
        Require(ExitLiveness.KilledBeforeRead(executable, exit, register),
            $"state exit {exit:X8} r{register} redefined before use including caller/branch delays");
    foreach (int register in new[] { 1, 2, 3 })
    {
        Require(ExitLiveness.KilledBeforeRead(executable, 0x80044070, register), "permission successor register kill");
        Require(ExitLiveness.KilledBeforeRead(executable, 0x800456E4, register), "pitch successor register kill");
    }
    byte[] hostile = executable.ToArray();
    // Replace the first successor LUI with a read of the stale v1.
    BinaryPrimitives.WriteUInt32LittleEndian(hostile.AsSpan(checked((int)(0x80045708 - 0x8000F800))), 0x00601021);
    Reject(() => ExitLiveness.KilledBeforeRead(hostile, 0x800456E4, 3), "live successor register mutation");
    hostile = executable.ToArray();
    // A load does not kill the incoming value for its following instruction.
    BinaryPrimitives.WriteUInt32LittleEndian(hostile.AsSpan(checked((int)(0x8001775C - 0x8000F800))), 0x00201021);
    Reject(() => ExitLiveness.KilledBeforeRead(hostile, 0x80044070, 1), "live permission fallback load delay mutation");
}

// Narrow, fail-closed read-before-definition walk. Branch alternatives and
// their delay slots are followed; direct calls enter the actual native callee
// until its first definition. No caller-saved register shortcut is used.
static class ExitLiveness
{
    internal static bool KilledBeforeRead(byte[] executable, uint start, int register)
    {
        HashSet<(uint Pc, uint? Next)> visited = [];
        Stack<(uint Pc, uint? Next)> pending = new([(start, null)]);
        while (pending.TryPop(out var state))
        {
            if (!visited.Add(state)) continue;
            if (visited.Count > 512) throw new InvalidDataException("Liveness traversal budget.");
            uint pc = state.Pc, word = Word(pc);
            var (reads, written, delayed) = Registers(word);
            if ((reads & (1u << register)) != 0)
                throw new InvalidDataException($"Live r{register} read at {pc:X8}.");
            if (written == register)
            {
                if (delayed && (Registers(Word(state.Next ?? pc + 4)).Reads & (1u << register)) != 0)
                    throw new InvalidDataException($"Live load-delay r{register} read at {pc + 4:X8}.");
                continue;
            }
            int op = (int)(word >> 26);
            if (state.Next is uint next)
            {
                pending.Push((next, null));
            }
            else if (op is 2 or 3)
            {
                uint target = ((pc + 4) & 0xF0000000) | ((word & 0x03FFFFFF) << 2);
                pending.Push((pc + 4, target));
            }
            else if (op is 1 or 4 or 5 or 6 or 7)
            {
                uint target = unchecked(pc + 4 + (uint)(unchecked((short)word) * 4));
                pending.Push((pc + 4, target));
                pending.Push((pc + 4, pc + 8));
            }
            else if (op == 0 && (word & 63) == 8)
            {
                if (pc != 0x80043FDC || (word >> 21 & 31) != 31)
                    throw new InvalidDataException("Unbound indirect liveness edge.");
                pending.Push((pc + 4, 0x8004A314));
            }
            else
            {
                pending.Push((pc + 4, null));
            }
        }
        return true;

        uint Word(uint address)
        {
            long offset = (long)address - 0x8000F800;
            if (offset < 0x800 || offset + 4 > executable.Length || offset % 4 != 0)
                throw new InvalidDataException("Unbound liveness code address.");
            return BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan((int)offset, 4));
        }
    }

    private static (uint Reads, int Written, bool Delayed) Registers(uint word)
    {
        int op = (int)(word >> 26), rs = (int)(word >> 21 & 31), rt = (int)(word >> 16 & 31);
        int rd = (int)(word >> 11 & 31), fn = (int)(word & 63);
        uint R(int r) => r == 0 ? 0 : 1u << r;
        if (op == 0)
            return fn switch
            {
                0 or 2 or 3 => (R(rt), rd, false),
                8 => (R(rs), -1, false),
                0x10 or 0x12 => (0, rd, false),
                0x18 or 0x19 or 0x1A or 0x1B => (R(rs) | R(rt), -1, false),
                0x20 or 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x2A or 0x2B =>
                    (R(rs) | R(rt), rd, false),
                _ => throw new InvalidDataException($"Unsupported liveness instruction {word:X8}.")
            };
        return op switch
        {
            1 or 6 or 7 => (R(rs), -1, false),
            2 => (0, -1, false),
            3 => (0, 31, false),
            4 or 5 => (R(rs) | R(rt), -1, false),
            >= 8 and <= 14 => (R(rs), rt, false),
            15 => (0, rt, false),
            32 or 33 or 35 or 36 or 37 => (R(rs), rt, true),
            40 or 41 or 43 => (R(rs) | R(rt), -1, false),
            _ => throw new InvalidDataException($"Unsupported liveness instruction {word:X8}.")
        };
    }
}

// Independent, deliberately tiny R3000 window interpreter. Both branch and
// load delay slots are modeled; a pending load is not visible to the next
// instruction. No external function or unbound memory execution is allowed.
static class Machine
{
    internal sealed record Result(uint Exit, int[] Registers, Dictionary<uint, int> Memory, HashSet<uint> Writes);
    internal static Result Run(uint start, byte[] code, Dictionary<uint, int> source, int[]? initial = null)
    {
        int[] r = initial?.ToArray() ?? new int[32];
        Dictionary<uint, int> memory = new(source);
        HashSet<uint> writes = [];
        uint pc = start;
        uint? branch = null;
        (int Register, int Value)? load = null;
        for (int step = 0; step < 128; step++)
        {
            if (pc < start || pc >= start + code.Length)
                return new(pc, r, memory, writes);
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(checked((int)(pc - start)), 4));
            int op = (int)(word >> 26), rs = (int)(word >> 21 & 31), rt = (int)(word >> 16 & 31);
            int rd = (int)(word >> 11 & 31), shift = (int)(word >> 6 & 31), fn = (int)(word & 63);
            int immediate = unchecked((short)word);
            uint address = unchecked((uint)(r[rs] + immediate));
            uint next = branch ?? pc + 4;
            branch = null;
            var previousLoad = load;
            load = null;
            int written = -1;
            void Set(int register, int value) { if (register != 0) r[register] = value; written = register; }
            switch (op)
            {
                case 0 when fn == 0: Set(rd, r[rt] << shift); break;
                case 0 when fn == 3: Set(rd, r[rt] >> shift); break;
                case 0 when fn == 0x21: Set(rd, unchecked(r[rs] + r[rt])); break;
                case 0 when fn == 0x23: Set(rd, unchecked(r[rs] - r[rt])); break;
                case 0 when fn == 0x2A: Set(rd, r[rs] < r[rt] ? 1 : 0); break;
                case 1 when rt == 1: branch = r[rs] >= 0 ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 2: branch = ((pc + 4) & 0xF0000000) | ((word & 0x03FFFFFF) << 2); break;
                case 4: branch = r[rs] == r[rt] ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 5: branch = r[rs] != r[rt] ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 9: Set(rt, unchecked(r[rs] + immediate)); break;
                case 10: Set(rt, r[rs] < immediate ? 1 : 0); break;
                case 15: Set(rt, unchecked((int)(word << 16))); break;
                case 35:
                    if (!memory.TryGetValue(address, out int value)) throw new InvalidDataException($"Unbound read {address:X8}");
                    load = (rt, value); break;
                case 43: memory[address] = r[rt]; writes.Add(address); break;
                default: throw new InvalidDataException($"Unsupported instruction {word:X8} at {pc:X8}");
            }
            if (previousLoad is { } pending && pending.Register != 0 && written != pending.Register)
                r[pending.Register] = pending.Value;
            r[0] = 0;
            pc = next;
        }
        throw new InvalidDataException("Window execution budget exhausted.");
    }
}
