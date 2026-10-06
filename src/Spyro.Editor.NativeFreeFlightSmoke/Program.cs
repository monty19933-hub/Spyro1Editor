using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

if (args.Length == 4 && args[0] == "--verify-global")
{
    await GlobalFreeFlightOutput.VerifyAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length is 3 or 4 && args[0] == "--audit-effects")
{
    await GlobalFreeFlightEffectAudit.RunAsync(args[1], args[2], args.Length == 4 ? args[3] : null);
    return;
}

if (args.Length == 4 && args[0] == "--apply-global")
{
    await GlobalFreeFlightOutput.ApplyAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length == 4 && args[0] == "--dump-native")
{
    // --dump-native <original.bin> <runtime address hex> <word count>: raw executable words with a light decode.
    var plan = await NativeFreeFlightCompositionPlan.CaptureAsync(args[1], true);
    byte[] exe = plan.ExecutablePatch.CopySourceExecutable();
    uint address = Convert.ToUInt32(args[2], 16);
    int count = int.Parse(args[3]);
    string[] regs = ["zero","at","v0","v1","a0","a1","a2","a3","t0","t1","t2","t3","t4","t5","t6","t7","s0","s1","s2","s3","s4","s5","s6","s7","t8","t9","k0","k1","gp","sp","fp","ra"];
    for (int i = 0; i < count; i++)
    {
        uint a = address + (uint)(i * 4);
        uint w = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan((int)(a - 0x8000F800), 4));
        int op = (int)(w >> 26), rs = (int)((w >> 21) & 31), rt = (int)((w >> 16) & 31), rd = (int)((w >> 11) & 31), sh = (int)((w >> 6) & 31), fn = (int)(w & 63);
        short imm = unchecked((short)(w & 0xFFFF));
        string text = op switch
        {
            0 => w == 0 ? "nop" : fn switch { 0x21 => $"addu {regs[rd]},{regs[rs]},{regs[rt]}", 0x23 => $"subu {regs[rd]},{regs[rs]},{regs[rt]}", 0x25 => $"or {regs[rd]},{regs[rs]},{regs[rt]}", 0x24 => $"and {regs[rd]},{regs[rs]},{regs[rt]}", 0x2A => $"slt {regs[rd]},{regs[rs]},{regs[rt]}", 0x2B => $"sltu {regs[rd]},{regs[rs]},{regs[rt]}", 0 => $"sll {regs[rd]},{regs[rt]},{sh}", 3 => $"sra {regs[rd]},{regs[rt]},{sh}", 2 => $"srl {regs[rd]},{regs[rt]},{sh}", 8 => $"jr {regs[rs]}", 9 => $"jalr {regs[rd]},{regs[rs]}", _ => $"R fn={fn:X}" },
            2 => $"j 0x{((a + 4) & 0xF0000000) | ((w & 0x03FFFFFF) << 2):X8}",
            3 => $"jal 0x{((a + 4) & 0xF0000000) | ((w & 0x03FFFFFF) << 2):X8}",
            4 => $"beq {regs[rs]},{regs[rt]} -> 0x{a + 4 + (uint)(imm * 4):X8}",
            5 => $"bne {regs[rs]},{regs[rt]} -> 0x{a + 4 + (uint)(imm * 4):X8}",
            1 => $"b{(rt == 0 ? "ltz" : rt == 1 ? "gez" : rt == 16 ? "ltzal" : "gezal")} {regs[rs]} -> 0x{a + 4 + (uint)(imm * 4):X8}",
            6 => $"blez {regs[rs]} -> 0x{a + 4 + (uint)(imm * 4):X8}",
            7 => $"bgtz {regs[rs]} -> 0x{a + 4 + (uint)(imm * 4):X8}",
            9 => $"addiu {regs[rt]},{regs[rs]},{imm}",
            10 => $"slti {regs[rt]},{regs[rs]},{imm}",
            11 => $"sltiu {regs[rt]},{regs[rs]},{imm}",
            12 => $"andi {regs[rt]},{regs[rs]},0x{w & 0xFFFF:X}",
            13 => $"ori {regs[rt]},{regs[rs]},0x{w & 0xFFFF:X}",
            15 => $"lui {regs[rt]},0x{w & 0xFFFF:X}",
            32 => $"lb {regs[rt]},{imm}({regs[rs]})",
            33 => $"lh {regs[rt]},{imm}({regs[rs]})",
            35 => $"lw {regs[rt]},{imm}({regs[rs]})",
            36 => $"lbu {regs[rt]},{imm}({regs[rs]})",
            37 => $"lhu {regs[rt]},{imm}({regs[rs]})",
            40 => $"sb {regs[rt]},{imm}({regs[rs]})",
            41 => $"sh {regs[rt]},{imm}({regs[rs]})",
            43 => $"sw {regs[rt]},{imm}({regs[rs]})",
            _ => $"op={op}"
        };
        Console.WriteLine($"{a:X8}  {w:X8}  {text}");
    }
    return;
}

if (args.Length == 3 && args[0] == "--find-imm")
{
    // --find-imm <original.bin> <imm16 hex>: every lw/sw/lb/sb/lh/sh/addiu whose immediate is that value.
    var plan = await NativeFreeFlightCompositionPlan.CaptureAsync(args[1], true);
    byte[] exe = plan.ExecutablePatch.CopySourceExecutable();
    ushort wanted = Convert.ToUInt16(args[2], 16);
    for (int offset = 0; offset + 4 <= exe.Length; offset += 4)
    {
        uint w = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(offset, 4));
        int op = (int)(w >> 26);
        if ((w & 0xFFFF) != wanted || op is not (9 or 32 or 33 or 35 or 36 or 37 or 40 or 41 or 43))
            continue;
        uint a = 0x8000F800 + (uint)offset;
        Console.WriteLine($"{a:X8}  {w:X8}  op={op} rs={(w >> 21) & 31} rt={(w >> 16) & 31}");
    }
    return;
}

if (args.Length == 3 && args[0] == "--zero-runs")
{
    // --zero-runs <original.bin> <min bytes>: zero-filled runs inside the loaded executable image.
    var plan = await NativeFreeFlightCompositionPlan.CaptureAsync(args[1], true);
    byte[] exe = plan.ExecutablePatch.CopySourceExecutable();
    int min = int.Parse(args[2]);
    Console.WriteLine($"executable image {exe.Length} bytes: 0x8000F800..0x{0x8000F800 + exe.Length:X8}");
    int start = -1;
    for (int i = 0; i <= exe.Length; i++)
    {
        bool zero = i < exe.Length && exe[i] == 0;
        if (zero && start < 0) start = i;
        if (!zero && start >= 0)
        {
            if (i - start >= min) Console.WriteLine($"0x{0x8000F800 + start:X8}..0x{0x8000F800 + i - 1:X8}  {i - start} bytes");
            start = -1;
        }
    }
    return;
}

if (args.Length == 4 && args[0] == "--refs-into")
{
    // --refs-into <original.bin> <start hex> <end hex>: immediates and pointer words that could address the range.
    var plan = await NativeFreeFlightCompositionPlan.CaptureAsync(args[1], true);
    byte[] exe = plan.ExecutablePatch.CopySourceExecutable();
    uint lo = Convert.ToUInt32(args[2], 16), hi = Convert.ToUInt32(args[3], 16);
    int pointerWords = 0, immediates = 0;
    for (int offset = 0; offset + 4 <= exe.Length; offset += 4)
    {
        uint w = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(offset, 4));
        uint a = 0x8000F800 + (uint)offset;
        if (w >= lo && w <= hi) { pointerWords++; Console.WriteLine($"pointer word {a:X8}: {w:X8}"); }
        int op = (int)(w >> 26);
        if (op is 9 or 32 or 33 or 35 or 36 or 37 or 40 or 41 or 43 or 13)
        {
            short imm = unchecked((short)(w & 0xFFFF));
            // any 0x8007 base: hi half 0x8007 → address 0x80070000 + imm (signed); 0x8008 base with negative imm too
            foreach (uint baseHi in new uint[] { 0x80070000, 0x80080000 })
            {
                long addr = baseHi + imm;
                if (addr >= lo && addr <= hi) { immediates++; Console.WriteLine($"immediate {a:X8}: {w:X8} -> {addr:X8}"); }
            }
        }
    }
    Console.WriteLine($"pointer words {pointerWords}, immediates {immediates}");
    return;
}

if (args.Length == 2 && args[0] == "--level-animations")
{
    // --level-animations <original.bin>: which player animation records each level's local stream holds.
    var source = await NativePlayerAnimationSource.CaptureAsync(args[1]);
    Console.WriteLine("shared: " + string.Join(",", source.SharedAnimationIds));
    foreach (var level in source.Levels.OrderBy(l => l.LevelId))
        Console.WriteLine($"level {level.LevelId}: {string.Join(",", level.Stream.Records.Select(r => r.AnimationId).Order())}");
    return;
}

if (args.Length == 1 && args[0] == "--loader-spare")
{
    _ = NativeFreeFlightAirborneLoader.Build();
    Console.WriteLine($"loader used {NativeFreeFlightAirborneLoader.UsedBytes} of {NativeFreeFlightAirborneLoader.ByteLength} bytes; spare {NativeFreeFlightAirborneLoader.ByteLength - NativeFreeFlightAirborneLoader.UsedBytes}");
    return;
}

if (args.Length == 1 && args[0] == "--emit-global")
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(NativeFreeFlightPatchBuilder.BuildGlobalNormal().Select(w => new { w.Name, address = w.Address, before = Convert.ToHexStringLower(w.Before), after = Convert.ToHexStringLower(w.After) })));
    return;
}

int checks = 0;
if (args.Length > 2)
    throw new ArgumentException("Usage: NativeFreeFlightSmoke [original.bin] [global-report.json]");

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

checks += GlobalFreeFlightCases.Run();
checks += LevelSetFreeFlightCases.Run();

if (args.Length >= 1)
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
    checks += await GlobalFreeFlightCases.RunSourceAsync(args[0], args.Length == 2 ? args[1] : null);
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
    internal static Action<string>? Trace;
    internal static Result Run(uint start, byte[] code, Dictionary<uint, int> source, int[]? initial = null, int maxSteps = 128)
    {
        int[] r = initial?.ToArray() ?? new int[32];
        Dictionary<uint, int> memory = new(source);
        HashSet<uint> writes = [];
        uint pc = start;
        uint? branch = null;
        (int Register, int Value)? load = null;
        for (int step = 0; step < maxSteps; step++)
        {
            if (pc < start || pc >= start + code.Length)
                return new(pc, r, memory, writes);
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(checked((int)(pc - start)), 4));
            Trace?.Invoke($"{pc:X8} {word:X8} v0={r[2]:X8} v1={r[3]:X8} a0={r[4]} a1={r[5]:X8} t1={r[9]} t2={r[10]}");
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
                case 0 when fn == 0x25: Set(rd, r[rs] | r[rt]); break;
                case 0 when fn == 0x2A: Set(rd, r[rs] < r[rt] ? 1 : 0); break;
                case 0 when fn == 6: Set(rd, unchecked((int)((uint)r[rt] >> (r[rs] & 31)))); break; // srlv
                case 0 when fn == 0x2B: Set(rd, unchecked((uint)r[rs]) < unchecked((uint)r[rt]) ? 1 : 0); break; // sltu
                case 0 when fn == 0x24: Set(rd, r[rs] & r[rt]); break;
                case 12: Set(rt, r[rs] & (int)(word & 0xFFFF)); break; // andi
                case 13: Set(rt, r[rs] | (int)(word & 0xFFFF)); break; // ori
                case 14: Set(rt, r[rs] ^ (int)(word & 0xFFFF)); break; // xori
                case 1 when rt == 1: branch = r[rs] >= 0 ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 2: branch = ((pc + 4) & 0xF0000000) | ((word & 0x03FFFFFF) << 2); break;
                case 4: branch = r[rs] == r[rt] ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 5: branch = r[rs] != r[rt] ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 9: Set(rt, unchecked(r[rs] + immediate)); break;
                case 10: Set(rt, r[rs] < immediate ? 1 : 0); break;
                case 11: Set(rt, unchecked((uint)r[rs]) < unchecked((uint)immediate) ? 1 : 0); break;
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
