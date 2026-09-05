using System.Buffers.Binary;
using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class Id65MusicSelectionCases
{
    private const int Mapping = 0x5F79C;
    private const int Late = 0x5F85C;
    private const int PeteXa = 0x5FA00;
    private const uint RuntimeBias = 0x8000F800;
    private static readonly int[] PatchOffsets = [0x1C57C, 0x1C584, 0x1C594, 0x1C5D8, Mapping + 35 * 4];
    private static readonly int[] PatchLengths = [4, 4, 32, 8, 4];

    public static void Run()
    {
        // This fixture proves compiler mechanics, NOT retail provenance or
        // playback. Only this test assembly bypasses the private constructor;
        // production has no synthetic issuer and requires the frozen SCUS.
        byte[] fixture = Fixture();
        Id65MusicSourceCertificate certificate = SyntheticCertificate(fixture);
        Reject(() => Id65MusicSourceCertificate.FromFrozenExecutable(fixture), "frozen");
        byte[] sourceSnapshot = fixture.ToArray();
        int outcomeCount = 0;
        Id65MusicSelectionPlan witness = Id65BuiltInMusicSelectionCompiler.Compile(certificate, fixture, 26);
        byte[] witnessOutput = witness.CopyOutputExecutable();
        bool[] ownedOffsets = new bool[fixture.Length];
        foreach ((int start, int length) in PatchOffsets.Zip(PatchLengths))
            Array.Fill(ownedOffsets, true, start, length);
        foreach (int track in Enumerable.Range(0, 48).Where(track => track is not (30 or 35)))
        {
            Id65MusicSelectionPlan plan = Id65BuiltInMusicSelectionCompiler.Compile(certificate, fixture, track);
            byte[] output = plan.CopyOutputExecutable();
            Require(plan.SelectedTrackId == track && plan.Patches.Count == 5 && plan.InversePatches.Count == 5,
                "Music leaf changed the exact five-window transaction.");
            Require(plan.Patches.Select(patch => patch.FileOffset).SequenceEqual(PatchOffsets), "Music patch offsets changed.");
            Require(plan.Patches.Select(patch => patch.ByteLength).SequenceEqual(PatchLengths), "Music patch lengths changed.");
            Require(Read(output, Mapping + 35 * 4) == track, "Initial ID65 mapping does not select the requested track.");
            byte[] replay = fixture.ToArray();
            Apply(replay, plan.Patches);
            Require(replay.SequenceEqual(output), "The detached owned patches do not reproduce the output.");
            Apply(replay, plan.InversePatches);
            Require(replay.SequenceEqual(fixture), "The music transaction is not exactly invertible.");
            for (int offset = 0; offset < output.Length; offset++)
            {
                Require(ownedOffsets[offset] || output[offset] == fixture[offset], "A protected executable byte changed.");
                if (offset < Mapping + 35 * 4 || offset >= Mapping + 36 * 4)
                    Require(output[offset] == witnessOutput[offset], "Selecting a track changed more than slot35.");
            }
            Require(output.AsSpan(Late, PeteXa + 24 - Late).SequenceEqual(fixture.AsSpan(Late, PeteXa + 24 - Late)),
                "Late rows/PETEXA words were extended or overwritten.");
            for (int level = 0; level <= 35; level++)
            {
                for (int alternate = 0; alternate < 3; alternate++)
                {
                    int expected = level == 35 ? track : checked((int)Read(fixture, Mapping + checked((int)Read(fixture, Late + (level * 3 + alternate) * 4)) * 4));
                    Require(ExecuteEmittedSelector(output, level, alternate) == expected,
                        $"Emitted MIPS changed level {level}, alternate {alternate}, selected track {track}.");
                    outcomeCount++;
                }
            }
            Id65MusicSelectionPlan repeat = Id65BuiltInMusicSelectionCompiler.Compile(certificate, fixture, track);
            Require(plan.OutputExecutableSha256 == repeat.OutputExecutableSha256 && plan.Patches.SequenceEqual(repeat.Patches),
                "Repeated compilation changed the result.");
            output[0] ^= 1;
            Require(plan.CopyOutputExecutable()[0] == fixture[0], "Output exposes its mutable backing array.");
        }
        foreach (int track in new[] { int.MinValue, -1, 30, 35, 48, int.MaxValue })
            Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(certificate, fixture, track), "track");
        Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(null!, fixture, 26), "certificate");
        Reject(() => Id65MusicSourceCertificate.FromFrozenExecutable(new byte[7]), "frozen");
        Reject(() => Id65MusicSourceCertificate.FromFrozenExecutable(new byte[fixture.Length + 4]), "frozen");
        Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(certificate, fixture.AsMemory(0, fixture.Length - 1), 26), "source");
        byte[] oversized = new byte[fixture.Length + 4]; fixture.CopyTo(oversized, 0);
        Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(certificate, oversized, 26), "source");
        foreach (int evidenceOffset in new[] { 0x18, 0x1C, 0x52F2C, 0x1E7E8, 0x6D94, 0x651C })
        {
            byte[] staleRegion = fixture.ToArray(); staleRegion[evidenceOffset] ^= 1;
            Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(certificate, staleRegion, 26), "source");
        }
        byte[] changed = fixture.ToArray(); changed[^1] ^= 1;
        Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(certificate, changed, 26), "source");
        byte[] changedCode = fixture.ToArray(); changedCode[0x1C594] ^= 1;
        Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(SyntheticCertificate(changedCode), changedCode, 26), "preimage");
        byte[] badBridge = fixture.ToArray(); Word(badBridge, Late + 34 * 12, 33);
        Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(SyntheticCertificate(badBridge), badBridge, 26), "row34");
        byte[] badMapping = fixture.ToArray(); Word(badMapping, Mapping, 48);
        Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(SyntheticCertificate(badMapping), badMapping, 26), "mapping");
        byte[] badLate = fixture.ToArray(); Word(badLate, Late, 48);
        Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(SyntheticCertificate(badLate), badLate, 26), "late");
        byte[] aliasesSlot35 = fixture.ToArray(); Word(aliasesSlot35, Late, 35);
        Reject(() => Id65BuiltInMusicSelectionCompiler.Compile(SyntheticCertificate(aliasesSlot35), aliasesSlot35, 26), "retail");
        fixture[0] ^= 1;
        Require(certificate.CopyExecutable().SequenceEqual(sourceSnapshot), "Certificate retained mutable caller bytes.");
        byte[] certificateCopy = certificate.CopyExecutable(); certificateCopy[0] ^= 1;
        Require(certificate.CopyExecutable().SequenceEqual(sourceSnapshot), "Certificate exposes mutable source bytes.");
        Reject(() => ((IList<Id65MusicFilePatch>)witness.Patches)[0] = witness.Patches[0], "read-only");
        Reject(() => ((IList<Id65MusicFilePatch>)witness.InversePatches)[0] = witness.InversePatches[0], "read-only");
        Reject(() => ((IDictionary<string, string>)certificate.RegionSha256)["prng"] = "forged", "read-only");
        Console.WriteLine($"ID65_MUSIC_SELECTION_COMPILER_SMOKE_OK tracks=46 emittedMipsOutcomes={outcomeCount} protectedComplement=1 exactInverse=1 immutableEvidence=1 syntheticNotRetailProof=1");
    }

    private static Id65MusicSourceCertificate SyntheticCertificate(byte[] bytes) =>
        (Id65MusicSourceCertificate)(typeof(Id65MusicSourceCertificate)
            .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(byte[])], null)
            ?.Invoke([bytes]) ?? throw new InvalidOperationException("The private test certificate constructor changed."));

    private static byte[] Fixture()
    {
        byte[] bytes = new byte[0x66000];
        Convert.FromHexString("50532D5820455845").CopyTo(bytes, 0);
        Word(bytes, 0x18, 0x80010000); Word(bytes, 0x1C, 0x65800);
        for (int index = 0; index < 48; index++) Word(bytes, Mapping + index * 4, (uint)index);
        Word(bytes, Mapping + 35 * 4, 19);
        for (int level = 0; level < 35; level++)
        {
            Word(bytes, Late + level * 12, (uint)level);
            Word(bytes, Late + level * 12 + 4, (uint)((level + 1) % 35));
            Word(bytes, Late + level * 12 + 8, (uint)(36 + level % 12));
        }
        for (int index = 0; index < 3; index++) Word(bytes, Late + 34 * 12 + index * 4, 34);
        for (int index = 0; index < 6; index++) Word(bytes, PeteXa + index * 4, (uint)(60000 + 10000 * index));
        Convert.FromHexString("0780033C6459638C0780043C5CF08424402803002128A300802805002128A400").CopyTo(bytes, 0x1C594);
        Convert.FromHexString("0780033CC858638C").CopyTo(bytes, 0x1C5D8);
        bytes[0x7000] = 0xA5;
        return bytes;
    }

    // Independent literal MIPS decoder for the emitted bridge windows. The
    // native intervening PRNG/load chooses each alternate explicitly; no leaf
    // selector-formula helper is called by this oracle.
    private static int ExecuteEmittedSelector(byte[] exe, int level, int alternate)
    {
        uint[] r = new uint[32];
        uint Load(uint address)
        {
            if (address == 0x80075964) return (uint)level;
            if (address == 0x800758C8) return 12345;
            long offset = (long)address - RuntimeBias;
            if (offset < 0 || offset > exe.Length - 4) throw new InvalidOperationException("Emitted MIPS read escaped the executable.");
            return Read(exe, (int)offset);
        }
        void Execute(int start, int length)
        {
            for (int offset = start; offset < start + length; offset += 4)
            {
                uint word = Read(exe, offset);
                int op = (int)(word >> 26), rs = (int)((word >> 21) & 31), rt = (int)((word >> 16) & 31), rd = (int)((word >> 11) & 31);
                uint signed = unchecked((uint)(int)(short)word);
                switch (op)
                {
                    case 0 when (word & 63) == 0x21: r[rd] = unchecked(r[rs] + r[rt]); break;
                    case 0 when (word & 63) == 0: r[rd] = r[rt] << (int)((word >> 6) & 31); break;
                    case 9: r[rt] = unchecked(r[rs] + signed); break;
                    case 11: r[rt] = r[rs] < signed ? 1u : 0u; break;
                    case 14: r[rt] = r[rs] ^ (word & 0xFFFF); break;
                    case 15: r[rt] = (word & 0xFFFF) << 16; break;
                    case 35: r[rt] = Load(unchecked(r[rs] + signed)); break;
                    default: throw new InvalidOperationException($"Unexpected emitted opcode0x{word:X8}.");
                }
                r[0] = 0;
            }
        }
        Execute(0x1C57C, 4); // Existing branch-delay instruction.
        Execute(0x1C584, 4); // Existing JAL delay; frozen PRNG preserves t0/t1.
        Execute(0x1C594, 32);
        uint expectedRow = RuntimeBias + (uint)(Late + (level == 35 ? 34 : level) * 12);
        Require(r[5] == expectedRow, "Emitted MIPS selected the wrong native late row.");
        r[2] = Load(r[5] + (uint)(alternate * 4));
        Execute(0x1C5D8, 8);
        Require(r[2] < 48 && r[3] == 12345, "Emitted MIPS escaped the mapping or lost its timer reload.");
        return checked((int)Read(exe, Mapping + checked((int)r[2]) * 4));
    }

    private static void Apply(byte[] bytes, IReadOnlyList<Id65MusicFilePatch> patches)
    {
        foreach (Id65MusicFilePatch patch in patches)
        {
            byte[] before = Convert.FromHexString(patch.BeforeHex), after = Convert.FromHexString(patch.AfterHex);
            Require(bytes.AsSpan(patch.FileOffset, before.Length).SequenceEqual(before), "Owned music patch has a stale preimage.");
            after.CopyTo(bytes, patch.FileOffset);
        }
    }
    private static void Reject(Action action, string text)
    {
        try { action(); }
        catch (NotSupportedException) when (text == "read-only") { return; }
        catch (Exception exception) when ((exception is InvalidDataException or ArgumentException) && exception.Message.Contains(text, StringComparison.OrdinalIgnoreCase)) { return; }
        throw new InvalidOperationException($"Expected music rejection containing '{text}'.");
    }
    private static uint Read(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static void Word(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
