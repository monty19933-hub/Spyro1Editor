using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

internal static class SceneListCases
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var lists = NativeId65ZeroSceneAnimationLists.CreateNeutral();
        byte[] bytes = lists.CopyComponents();
        check(bytes.Length == 0x44 && lists.Lists.Count == 7, "seven exact empty scene list owners");
        check(!lists.SourceCertified && !lists.SceneInitialized && !lists.PublicationApproved,
            "neutral byte grammar cannot issue native scene/source authority");
        check(lists.Lists.Select(l => l.ByteLength).SequenceEqual(new[] { 12, 12, 8, 8, 12, 8, 8 }),
            "only the three actual speculative readers receive guard words");
        check(lists.Lists.Select(l => l.NativeCountGlobal).SequenceEqual(new uint[]
            { 0x80078560, 0x80078568, 0x80078580, 0x80078570, 0x800785A0, 0x80078588, 0x80078578 }),
            "native physical component order differs from global numerical order");
        check(lists.Lists.Where(l => l.OwnsGuardWord).Select(l => l.ComponentIndex).SequenceEqual(new[] { 0, 1, 4 }),
            "actual zero-count delay-slot readers are identified");
        check(lists.PlacementRequirement.SceneRelativeOffset == 0x88 &&
            lists.PlacementRequirement.NextComponentSceneRelativeOffset == 0xCC,
            "typed placement requirement owns only the prefix after the native scene header");
        NativeId65ZeroSceneAnimationLists.ValidateCanonicalBytes(bytes);
        foreach (var owner in lists.Lists)
        {
            check(Word(bytes, owner.ComponentOffset) == owner.ByteLength && Word(bytes, owner.ComponentOffset + 4) == 0,
                "actual component length and zero count are emitted");
            check(owner.NativePointerGlobal == owner.NativeCountGlobal + 4 &&
                owner.FirstEntrySceneRelativeOffset == 0x88 + owner.ComponentOffset + 8,
                "native first-entry pointer basis is component+8, not length/count word");
        }
        for (int i = 0; i < bytes.Length; i++)
        {
            byte[] corrupt = bytes.ToArray(); corrupt[i] ^= 1;
            reject(() => NativeId65ZeroSceneAnimationLists.ValidateCanonicalBytes(corrupt),
                "every length/count/guard byte belongs to the canonical grammar");
        }
        foreach (int length in new[] { 0, 4, 8, 56, 67, 69, 72, 4096 })
            reject(() => NativeId65ZeroSceneAnimationLists.ValidateCanonicalBytes(new byte[length]),
                "missing guards, extra bytes or oversized prefixes refuse before allocation");
        foreach (var owner in lists.Lists)
        {
            byte[] corrupt = bytes.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(corrupt.AsSpan(owner.ComponentOffset + 4), uint.MaxValue);
            reject(() => NativeId65ZeroSceneAnimationLists.ValidateCanonicalBytes(corrupt),
                "negative signed count is not accepted merely because native BLEZ skips it");
        }
        reject(() => NativeId65ZeroSceneAnimationLists.ValidateSourceExecutable([]), "missing source consumers refuse");
        reject(() => NativeId65NormalResourceChain.Compile(null!, lists), "missing resource source refuses");
        MipsOracle.SelfTest(check, reject);
    }

    internal static async Task Source(string image, NativeId65NormalResourceBundle first,
        NativeId65NormalResourceBundle second, Action<bool, string> check, Action<Action, string> reject)
    {
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(image);
        var neutral = NativeId65ZeroSceneAnimationLists.CreateNeutral();
        reject(() => NativeId65NormalResourceChain.Compile(first, neutral), "neutral scene-list metadata cannot issue source pairing");
        byte[] executable = first.Surface.CopySourceExecutable();
        lists.RequireSourceExecutable(executable);
        check(lists.SourceCertified && !lists.SceneInitialized && !lists.PublicationApproved, "captured consumers do not initialize a complete scene");
        foreach (var evidence in lists.Evidence)
        {
            byte[] corrupt = executable.ToArray(); corrupt[(int)evidence.Offset] ^= 1;
            reject(() => NativeId65ZeroSceneAnimationLists.ValidateSourceExecutable(corrupt), "changed native list consumer refuses");
        }
        MipsOracle.Source(executable, lists, check, reject);
        var plan = NativeId65NormalResourceChain.Compile(first, lists);
        byte[] data = plan.CopyDataComponents(), prefix = plan.CopySceneAnimationComponents();
        reject(() => NativeId65NormalResourceChain.Compile(first, null!), "missing paired scene authority refuses");
        check(plan.DataSha256 == "821d25d4c012aa180d369f755d82a780dbcf244b4e53c6c4bcf349da533dce65" &&
            plan.SceneComponentsSha256 == "b9ee5eb6825630fd5d4ede0bba2a140552191a4e8edf369ddc4aacf36fe09f60",
            "paired bytes equal the independent raw-sector field oracle");
        check(data.Length == 0x8C8 && prefix.Length == 0x44, "actual paired native resource bytes");
        check(Word(data, 0) == 8 && Word(data, 4) == 0 && data.AsSpan(8).SequenceEqual(first.CopyDataComponentTail()),
            "only fresh empty texture metadata is prepended; entire old tail preserved");
        check(plan.DataComponentsEncoded && plan.SceneListBytesEncoded && !plan.CompleteLevelResourcesProven &&
            !plan.SceneInitialized && !plan.PublicationApproved && !plan.RuntimeVerified,
            "paired byte production does not promote full-level or runtime gates");
        check(!first.CompleteDataChainAvailable, "old bundle refusal remains unchanged");
        reject(() => first.CopyDataComponents(), "existing accessor must still refuse");
        check(plan.ScenePlacementRequirement == lists.PlacementRequirement, "paired scene placement remains typed");
        plan.RequireExactPairedBytes(data, prefix);
        check(true, "exact paired readback");
        for (int i = 0; i < prefix.Length; i++)
        {
            byte[] corrupt = prefix.ToArray(); corrupt[i] ^= 1;
            reject(() => plan.RequireExactPairedBytes(data, corrupt), "unpaired scene/count/guard corruption refuses");
        }
        foreach (var owner in plan.DataOwners)
        {
            byte[] corrupt = data.ToArray(); corrupt[owner.Offset] ^= 1;
            reject(() => plan.RequireExactPairedBytes(corrupt, prefix), "all emitted component owners remain bound");
        }
        reject(() => plan.RequireExactPairedBytes(data[..^1], prefix), "truncated data refuses");
        reject(() => plan.RequireExactPairedBytes(data, prefix.Concat(new byte[4]).ToArray()), "unowned scene suffix refuses");
        var alternate = NativeId65NormalResourceChain.Compile(second, lists);
        check(alternate.DataSha256 == "e1759617b8312795595d0b1229565e68725739404b4416641a9ba408cf512ddd",
            "second authored floor equals independent raw-sector oracle");
        check(alternate.DataSha256 != plan.DataSha256 && alternate.SceneComponentsSha256 == plan.SceneComponentsSha256,
            "different authored surfaces retain identical owned scene-list bytes");
        check(NativeId65NormalResourceChain.Compile(first, lists).PlanSha256 == plan.PlanSha256, "repeat paired compilation deterministic");
        check(plan.CopyUploadPayload().SequenceEqual(first.CopyUploadPayload()) && plan.CopyModelPayload().SequenceEqual(first.CopyModelPayload()),
            "fixed VRAM/audio/model payloads are untouched by scene-list integration");
        data[0] ^= 1;
        prefix[0] ^= 1;
        check(plan.CopyDataComponents()[0] == 8 && plan.CopySceneAnimationComponents()[0] == 12,
            "returned paired bytes cannot mutate immutable producer state");
        Console.WriteLine($"NATIVE_ID65_ZERO_SCENE_LIST_SOURCE_OK data={plan.DataSha256} lists={plan.SceneComponentsSha256} pair={plan.PlanSha256}");
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}

/// <summary>Bounded instruction/memory oracle, not a production emulator or source authority.</summary>
internal sealed class MipsOracle
{
    private readonly Dictionary<uint, uint> _code = [];
    private readonly Dictionary<uint, byte> _memory = [];
    private readonly HashSet<uint> _writable = [];
    private readonly HashSet<uint> _reads = [];
    private readonly HashSet<uint> _writes = [];
    internal readonly uint[] Registers = new uint[32];
    private const uint SceneBase = 0x80100000;
    private const uint ReturnPc = 0x800FFFF0;

    internal void Code(uint pc, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 0x2000 || bytes.Length % 4 != 0 || pc % 4 != 0)
            throw new InvalidDataException("Oracle code owner is unsupported.");
        for (int i = 0; i < bytes.Length; i += 4) _code.Add(pc + (uint)i, BinaryPrimitives.ReadUInt32LittleEndian(bytes[i..]));
    }
    private void Map(uint address, ReadOnlySpan<byte> bytes, bool writable)
    {
        if (bytes.Length > 0x1000) throw new InvalidDataException("Oracle memory budget.");
        for (int i = 0; i < bytes.Length; i++)
        {
            uint at = checked(address + (uint)i);
            _memory.Add(at, bytes[i]);
            if (writable) _writable.Add(at);
        }
    }
    private uint Read(uint address)
    {
        if (address % 4 != 0) throw new InvalidDataException("Oracle unaligned read.");
        uint result = 0;
        for (int i = 0; i < 4; i++)
        {
            if (!_memory.TryGetValue(address + (uint)i, out byte value))
                throw new InvalidDataException($"Native speculative/indexed read has no owned storage at {address:X8}.");
            result |= (uint)value << (8 * i);
        }
        _reads.Add(address); return result;
    }
    private void Write(uint address, uint value)
    {
        if (address % 4 != 0) throw new InvalidDataException("Oracle unaligned write.");
        for (int i = 0; i < 4; i++)
        {
            if (!_writable.Contains(address + (uint)i))
                throw new InvalidDataException($"Native write escaped its exact global owner at {address:X8}.");
            _memory[address + (uint)i] = (byte)(value >> (8 * i));
        }
        _writes.Add(address);
    }
    internal void Execute(uint pc, uint stop)
    {
        uint? delayedBranch = null;
        (int Register, uint Value)? delayedLoad = null;
        for (int steps = 0; steps < 4096; steps++)
        {
            if (pc == stop)
            {
                if (delayedBranch != null || delayedLoad != null) throw new InvalidDataException("Oracle stopped with pending architectural delay.");
                return;
            }
            if (!_code.TryGetValue(pc, out uint word)) throw new InvalidDataException($"Unowned native instruction {pc:X8}.");
            int rs = (int)(word >> 21) & 31, rt = (int)(word >> 16) & 31, rd = (int)(word >> 11) & 31;
            int shift = (int)(word >> 6) & 31; short immediate = (short)word;
            uint? nextBranch = null;
            (int Register, uint Value)? nextLoad = null, write = null;
            switch (word >> 26)
            {
                case 0:
                    switch (word & 63)
                    {
                        case 0: write = (rd, Registers[rt] << shift); break;
                        case 8: nextBranch = Registers[rs]; break;
                        case 0x20: write = (rd, Add(Registers[rs], Registers[rt])); break;
                        case 0x21: write = (rd, unchecked(Registers[rs] + Registers[rt])); break;
                        case 0x2A: write = (rd, (int)Registers[rs] < (int)Registers[rt] ? 1u : 0u); break;
                        default: throw new InvalidDataException("Unexpected native SPECIAL on zero-list path.");
                    }
                    break;
                case 2: nextBranch = ((pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2); break;
                case 3:
                    write = (31, pc + 8); nextBranch = ((pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2); break;
                case 4: nextBranch = Registers[rs] == Registers[rt] ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 5: nextBranch = Registers[rs] != Registers[rt] ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 6: nextBranch = (int)Registers[rs] <= 0 ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 8: write = (rt, Add(Registers[rs], unchecked((uint)immediate))); break;
                case 9: write = (rt, unchecked(Registers[rs] + (uint)immediate)); break;
                case 15: write = (rt, (uint)(ushort)immediate << 16); break;
                case 35: nextLoad = (rt, Read(unchecked(Registers[rs] + (uint)immediate))); break;
                case 43: Write(unchecked(Registers[rs] + (uint)immediate), Registers[rt]); break;
                default: throw new InvalidDataException("Unexpected native instruction on zero-list path.");
            }
            // The instruction in a load delay slot observes the old register.
            // A same-register ordinary write wins over the retiring old load.
            if (delayedLoad is { } load && load.Register != 0 && write?.Register != load.Register)
                Registers[load.Register] = load.Value;
            if (write is { } changed && changed.Register != 0) Registers[changed.Register] = changed.Value;
            Registers[0] = 0;
            if (delayedBranch != null && nextBranch != null) throw new InvalidDataException("Branch in branch delay slot.");
            pc = delayedBranch ?? pc + 4;
            delayedBranch = nextBranch; delayedLoad = nextLoad;
        }
        throw new InvalidDataException("Native zero-list path exceeded its instruction budget.");
    }
    private static uint Add(uint a, uint b)
    {
        long value = (long)(int)a + (int)b;
        if (value < int.MinValue || value > int.MaxValue) throw new InvalidDataException("Native signed ADD overflow.");
        return unchecked((uint)value);
    }

    internal static void SelfTest(Action<bool, string> check, Action<Action, string> reject)
    {
        var machine = new MipsOracle();
        uint[] words = [0x8C220000, 0x24430000, 0x24440000, 0x10000001, 0x8C250000, 0x24A60000, 0x24A70000, 0x03E00008, 0];
        byte[] code = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(code.AsSpan(i * 4), words[i]);
        machine.Code(0x80000000, code); machine.Map(0x1000, new byte[] { 0x78, 0x56, 0x34, 0x12 }, false);
        machine.Registers[1] = 0x1000; machine.Registers[2] = 0xAAAA; machine.Registers[5] = 0xBBBB; machine.Registers[31] = ReturnPc;
        machine.Execute(0x80000000, ReturnPc);
        check(machine.Registers[3] == 0xAAAA && machine.Registers[4] == 0x12345678 &&
            machine.Registers[6] == 0xBBBB && machine.Registers[7] == 0x12345678,
            "oracle honors ordinary load delay and load in a taken branch delay slot");
        reject(() => machine.Read(0x1004), "oracle refuses an unmapped speculative read");
    }

    internal static void Source(byte[] executable, NativeId65ZeroSceneAnimationLists lists,
        Action<bool, string> check, Action<Action, string> reject)
    {
        foreach (uint mode in new uint[] { 0, 1 })
        {
            var loader = Prepare(executable, lists);
            loader.Registers[16] = SceneBase + 0x88; // source-bound entry s0=scene+88
            loader.Registers[17] = 0; loader.Registers[21] = mode;
            loader.Registers[3] = 0x80078560; // preceding native13C4C/50 establishes v1
            // Native13C5C has loaded scene+84, the second word of the final
            // header Tiledef. Its value is not constrained by this list asset.
            uint headerWord = mode == 0 ? 0u : 0xA5C37E91u;
            loader.Registers[2] = headerWord;
            loader.Execute(0x80013C64, 0x80013F24);
            check(loader.Read(0x800756F4) == headerWord,
                "native header word store is preserved for both zero and nonzero incoming values");
            check(loader.Registers[16] == SceneBase + 0xCC, "native loader advances through all seven emitted owners");
            foreach (var owner in lists.Lists)
            {
                check(loader.Read(owner.NativeCountGlobal) == 0, "native loader installs actual zero count");
                check(loader.Read(owner.NativePointerGlobal) == SceneBase + owner.FirstEntrySceneRelativeOffset,
                    "native loader installs exact first-entry pointer including guard owner");
            }
            // Updaters must not depend on this unrelated header field, even
            // through a branch that accidentally passed with a zero seed.
            for (uint i = 0; i < 4; i++) loader._memory.Remove(0x800756F4 + i);
            foreach (uint frame in new uint[] { 0, 1, 4, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF })
            {
                foreach (uint entry in new uint[] { 0x8002A6FC, 0x8002B4AC })
                {
                    loader._reads.Clear(); loader._writes.Clear();
                    loader.Registers[4] = frame; loader.Registers[31] = ReturnPc;
                    loader.Execute(entry, ReturnPc);
                    check(loader._writes.Count == 0, "zero lists cannot modify terrain, metadata or scene indices");
                    foreach (var guard in lists.Lists.Where(l => l.OwnsGuardWord))
                        check(loader._reads.Contains(SceneBase + guard.FirstEntrySceneRelativeOffset),
                            "native branch delay slot actually reads its emitted guard");
                }
            }
        }
        foreach (var guard in lists.Lists.Where(l => l.OwnsGuardWord))
        {
            foreach (uint entry in new uint[] { 0x8002A6FC, 0x8002B4AC })
            {
                var missing = Prepare(executable, lists);
                foreach (var owner in lists.Lists)
                {
                    missing.Write(owner.NativeCountGlobal, 0);
                    missing.Write(owner.NativePointerGlobal, SceneBase + owner.FirstEntrySceneRelativeOffset);
                }
                for (uint i = 0; i < 4; i++) missing._memory.Remove(SceneBase + guard.FirstEntrySceneRelativeOffset + i);
                missing.Registers[31] = ReturnPc;
                reject(() => missing.Execute(entry, ReturnPc), "zero count without its actual owned guard must fail the memory oracle");
            }
        }
    }
    private static MipsOracle Prepare(byte[] executable, NativeId65ZeroSceneAnimationLists lists)
    {
        var machine = new MipsOracle();
        foreach (var (pc, length) in new[] { (0x80013C40u, 0x2E4), (0x8002A6FCu, 0xC94), (0x8002B4ACu, 0x520) })
            machine.Code(pc, executable.AsSpan((int)(pc - 0x8000F800), length));
        machine.Map(SceneBase + 0x88, lists.CopyComponents(), false);
        foreach (var owner in lists.Lists)
            machine.Map(owner.NativeCountGlobal, Enumerable.Repeat((byte)0xD0, 8).ToArray(), true);
        machine.Map(0x800756F4, new byte[4], true);
        // Terrain metadata g785C0/C4, environment and collision pointer storage
        // are deliberately unmapped, as are all scene bytes outside the prefix.
        return machine;
    }
}
