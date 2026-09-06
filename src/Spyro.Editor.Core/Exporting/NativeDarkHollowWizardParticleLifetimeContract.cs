using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeParticleLifetimeEvidence(string Id, string SourceDomain, long SourceOffset, int ByteLength, string Sha256);
internal sealed record NativeParticleFrameWindow(int FrameByteOffset, int FrameStride, int DescriptorReadStart, int DescriptorReadEndExclusive);

internal static class NativeParticleFrameWindowCodec
{
    internal static NativeParticleFrameWindow Decode(ReadOnlySpan<byte> bytes)
    {
        // ra has already advanced by0x20. This lhu reads raw+0x10 and raw+0x11;
        // only the low byte indexes the descriptor, while the high byte is depth bias.
        uint[] expected = [0x97EBFFF0, 0x8FE3FFEC, 0x000B6202, 0x00AC2822, 0x04A0FDC4,
            0x316C00FF, 0x000C60C0, 0x002C0820, 0x8C2B0004, 0x8C2C0008];
        if (bytes.Length != expected.Length * 4) throw new InvalidDataException("Particle renderer requires its complete40-byte frame window.");
        for (int i = 0; i < expected.Length; i++)
            if (Word(bytes, i * 4) != expected[i]) throw new InvalidDataException("Particle raw-frame/depth/stride/descriptor-read grammar changed.");
        return new(0x10, 8, 4, 12);
    }
    internal static void RequireFrameOwner(ReadOnlySpan<byte> record, int frame)
    {
        if (NativeParticleDescriptorCodec.RequireRecord(record) != 7 || frame != 0)
            throw new InvalidDataException("Native Wizard particle7 owns only frame0 in its single8-byte payload; following sound bytes are not frames.");
    }
    internal static void RequireAgeOperations(ReadOnlySpan<byte> tick)
    {
        if (tick.Length != 0x98) throw new InvalidDataException("Particle age proof requires the complete native tick owner.");
        if (Word(tick, 0) != 0x92420001 || Word(tick, 8) != 0x2C420019 || Word(tick, 12) != 0x1040041B)
            throw new InvalidDataException("Particle pre-update unsigned age25 deletion boundary changed.");
        if (Word(tick, 0x5C) != 0x92460001 || Word(tick, 0x7C) != 0x24C60001 || Word(tick, 0x80) != 0xA2460001)
            throw new InvalidDataException("Particle native age reload/increment/store changed.");
    }
    private static uint Word(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
}

/// <summary>Exact original normal-level single-frame ownership, not native CPU/GPU execution or publication authority.</summary>
public sealed class NativeDarkHollowWizardParticleLifetimeContract
{
    private readonly Dictionary<string, byte[]> _bytes = new(StringComparer.Ordinal);
    private NativeDarkHollowWizardParticleLifetimeContract(ReadOnlySpan<byte> donor, ReadOnlySpan<byte> executable,
        ReadOnlySpan<byte> peteHeader, ReadOnlySpan<byte> peteRuntimeCopy)
    {
        // Validate fixed complete source owners before cloning any input. These issuers
        // remain separate from the neutral frame/age grammar and from arbitrary metadata.
        Require(donor, 0x10000, "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1", "donor overlay");
        _ = NativeMobyModelLoaderCertificate.CaptureRetail(executable, peteHeader, peteRuntimeCopy);
        List<NativeParticleLifetimeEvidence> evidence = [];
        void Add(string id, string domain, long offset, ReadOnlySpan<byte> bytes, int length, string hash)
        {
            Require(bytes, length, hash, id);
            _bytes.Add(id, bytes.ToArray()); evidence.Add(new(id, domain, offset, length, hash));
        }
        foreach (var s in DonorSpans)
            Add(s.Id, "original-Wizard-overlay", 0x2F5B000L + s.Pc - 0x8007AA38,
                donor.Slice(checked((int)(s.Pc - 0x8007AA38)), s.Length), s.Length, s.Hash);
        foreach (var s in ExeSpans)
            Add(s.Id, "original-executable", s.Pc - 0x8000F800,
                executable.Slice(checked((int)(s.Pc - 0x8000F800)), s.Length), s.Length, s.Hash);
        Add("PETE-header", "original-WAD", 0x7B8800, peteHeader, 0x800, "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1");
        Add("PETE-runtime-copy", "original-WAD", 0x7B9000, peteRuntimeCopy, 0x39C48, "1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d");
        Evidence = Array.AsReadOnly(evidence.ToArray());
        _ = NativeParticleFrameWindowCodec.Decode(_bytes["renderer-frame-window"]);
        NativeParticleFrameWindowCodec.RequireAgeOperations(_bytes["particle07-tick"]);
        RequireSingleFramePaths(_bytes["particle07-spawn"], _bytes["particle07-frame-zero-tail"],
            _bytes["particle07-tick"], _bytes["kind2-renderer"]);
        // The terminal's speculative next-header LH is PETE+0x20, beyond the
        // particle pool and its four-byte sentinel. It has an actual resident owner.
        if (Word(executable, checked((int)(0x800755A4 - 0x8000F800))) != 0x800 || Word(peteHeader, 0) != 0x39C48 ||
            Word(peteRuntimeCopy, 0) != 46 || BinaryPrimitives.ReadUInt16LittleEndian(peteRuntimeCopy[0x20..]) != 0 ||
            0x80200000u - 0x800u - Word(peteHeader, 0) != NormalPoolEnd || NormalPoolEnd - NormalPoolStart != 0x2000)
            throw new InvalidDataException("Normal particle pool/PETE speculative lookahead owner changed.");
    }
    internal static NativeDarkHollowWizardParticleLifetimeContract CaptureRetail(ReadOnlySpan<byte> donor,
        ReadOnlySpan<byte> executable, ReadOnlySpan<byte> peteHeader, ReadOnlySpan<byte> peteRuntimeCopy) => new(donor, executable, peteHeader, peteRuntimeCopy);
    public IReadOnlyList<NativeParticleLifetimeEvidence> Evidence { get; }
    public int MaxFrameIndex => 0;
    public int MaxAgeUpdates => 25;
    public int MaxAgeCheckCallsIncludingDeletion => 26;
    public uint NormalPoolStart => 0x801C3BB8;
    public uint NormalPoolEnd => 0x801C5BB8;
    public bool SingleFrameOwnershipProven => true;
    public bool CompleteNativeClosureProven => false;
    public bool GteStateProven => false;
    public bool PeakTransientFitProven => false;
    public bool RuntimeProven => false;
    public bool PublicationApproved => false;
    public void RequireResources(NativeDarkHollowWizardResourcePlan resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (resources.SourceImageSha256 != NativeMobySourceInventory.OriginalImageSha256 || resources.TargetLevelId != 12 ||
            resources.SourceDataSha256 != "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97" ||
            !resources.ParticleDescriptorInstalled)
            throw new InvalidDataException("Particle lifetime proof requires the exact source-issued Dark descriptor installation.");
        byte[] final = resources.CopyFinalLevelData(); resources.RequireFinal(final);
        var layout = new NativeLevelSoundLayout(final);
        int at = layout.ParticleOffset, length = checked((int)Word(final, at));
        var table = NativeParticleDescriptorCodec.Decode(final.AsSpan(at, length));
        int index = table.Ids.ToList().IndexOf(7);
        if (table.Count != 12 || index != 11) throw new InvalidDataException("Particle7 must remain the last appended descriptor after all eleven retained originals.");
        ReadOnlySpan<byte> record = final.AsSpan(at + 8 + index * 12, 12);
        NativeParticleFrameWindowCodec.RequireFrameOwner(record, MaxFrameIndex);
        if (!record.SequenceEqual(resources.CopyPendingParticleDescriptor()))
            throw new InvalidDataException("Installed particle frame payload differs from its source-issued texture owner.");
    }
    internal byte[] CopyEvidenceBytes(string id) => _bytes.TryGetValue(id, out var bytes) ? bytes.ToArray() :
        throw new InvalidDataException("Unbound particle lifetime source certificate.");
    internal void RequireEvidence(string id, ReadOnlySpan<byte> bytes)
    {
        var entry = Evidence.SingleOrDefault(e => e.Id == id) ?? throw new InvalidDataException("Unbound particle lifetime source certificate.");
        Require(bytes, entry.ByteLength, entry.Sha256, id);
    }

    private static void RequireSingleFramePaths(byte[] spawn, byte[] tail, byte[] tick, byte[] renderer)
    {
        if (Word(spawn, 4) != 0x24040002 || Word(tail, 0) != 0xA2480011 || Word(tail, 4) != 0x08022A12 || Word(tail, 8) != 0xA2400010)
            throw new InvalidDataException("Particle7 allocation kind or unconditional frame-zero delay slot changed.");
        // A bounded CFG walk, not a native emulator. Exact separately certified
        // allocator/RNG/vector calls return, and both native conditional edges are walked.
        Dictionary<uint, uint> code = [];
        for (int i = 0; i < spawn.Length; i += 4) code.Add(0x80089934u + (uint)i, Word(spawn, i));
        for (int i = 0; i < tail.Length; i += 4) code.Add(0x80089F28u + (uint)i, Word(tail, i));
        Stack<(uint Pc, bool Zero)> pending = new(); pending.Push((0x80089934, false));
        HashSet<(uint Pc, bool Zero)> seen = []; int exits = 0;
        while (pending.TryPop(out var node))
        {
            if (!seen.Add(node)) continue;
            if (seen.Count > 256) throw new InvalidDataException("Particle spawn CFG budget exceeded.");
            if (node.Pc == 0x8008A848)
            { if (!node.Zero) throw new InvalidDataException("Reused particle frame is not overwritten on every spawn path."); exits++; continue; }
            if (!code.TryGetValue(node.Pc, out uint word)) throw new InvalidDataException("Unowned particle spawn control edge.");
            bool zero = node.Zero || word == 0xA2400010; uint op = word >> 26;
            if (op is >= 1 and <= 7)
            {
                if (!code.TryGetValue(node.Pc + 4, out uint delay) || delay >> 26 is >= 1 and <= 7)
                    throw new InvalidDataException("Particle spawn delay owner changed.");
                zero |= delay == 0xA2400010;
                if (op == 3) pending.Push((node.Pc + 8, zero));
                else if (op == 2) pending.Push((((node.Pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2), zero));
                else { pending.Push((node.Pc + 8, zero)); pending.Push((unchecked(node.Pc + 4 + (uint)((short)word * 4)), zero)); }
            }
            else pending.Push((node.Pc + 4, zero));
        }
        if (exits == 0) throw new InvalidDataException("Particle spawn has no proven frame-zero exit.");
        HashSet<int> stores = [];
        for (int i = 0; i < tick.Length; i += 4)
        {
            uint word = Word(tick, i), op = word >> 26;
            if (op is not (0x28 or 0x29 or 0x2B)) continue;
            if (((word >> 21) & 31) != 18) throw new InvalidDataException("Particle tick store base changed.");
            int start = 1 + (short)word, width = op == 0x28 ? 1 : op == 0x29 ? 2 : 4;
            for (int n = 0; n < width; n++) stores.Add(start + n);
        }
        if (!stores.Order().SequenceEqual(new[] { 2, 4, 5, 6, 7, 8, 9, 10, 11 }))
            throw new InvalidDataException("Particle tick writes outside its non-frame field owners.");
        int cursor = 0; List<int> writes = [];
        for (int i = 0; i < renderer.Length; i += 4)
        {
            uint word = Word(renderer, i), op = word >> 26;
            if (word == 0x23FF0020) cursor += 0x20;
            if (op is 0x28 or 0x29 or 0x2B && ((word >> 21) & 31) == 31)
            { if (op != 0x28) throw new InvalidDataException("Particle renderer raw-store width changed."); writes.Add(cursor + (short)word); }
        }
        if (cursor != 0x20 || !writes.SequenceEqual(new[] { 3, 3 }))
            throw new InvalidDataException("Particle renderer may only update the raw visibility byte.");
    }
    private static void Require(ReadOnlySpan<byte> bytes, int length, string hash, string id)
    { if (bytes.Length != length || NativeMobyModelLoaderCertificate.Hash(bytes) != hash) throw new InvalidDataException("Particle lifetime source certificate changed: " + id); }
    private static uint Word(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
    private sealed record SpanSpec(string Id, uint Pc, int Length, string Hash);
    private static readonly SpanSpec[] DonorSpans =
    [
        new("particle07-spawn", 0x80089934, 0x11C, "c3e0db5def4d5c5c63c10b3e8acf314ebabfa91c93f8a2087a268e78da2df6e3"),
        new("particle07-frame-zero-tail", 0x80089F28, 12, "21561d82f9fa377c0a4deef5824e6367b5833dcf17a128a77e5e590123bf0ae8"),
        new("particle07-tick", 0x80088370, 0x98, "a2f5681329e63ab489abec1424ecc230f462f4af53bfc5d0e0ae94bb5a09afc4"),
        new("particle41-tick", 0x800888E8, 0xFC, "bdfa5cf0752d3ca410a216f83a31d196492bd22de836ccfba9255582a5c01a19"),
        new("particle07-tail-and41-spawn", 0x80089F28, 0xD0, "fbff816f4c054e52e663d05b0c170db721b3931ce7baac3bd93340837db075d8")
    ];
    private static readonly SpanSpec[] ExeSpans =
    [
        new("kind2-renderer", 0x80057C08, 0x274, "5763c22d74429eece44169934917ca2336b4f2a1fb9565c8fb292e5350e261f4"),
        new("kind1-renderer", 0x80057E7C, 0x11C, "f01218c13d66114224931e8a049ad297c4ff48fee5ebe830a2f2f58635e5efa4"),
        new("renderer-frame-window", 0x80057D9C, 0x28, "08691eb176e4a9b7eabffe4d85efdaa002500d2c69810fee4aee3c2b2f3684e2"),
        new("allocator", 0x80053570, 0x98, "2eefe927534faee54464ea9af9e53a9382ff3fafa0f90d50b6a68982dce61ddb"),
        new("delete", 0x80053608, 0x9C, "3bf6f2ab0aaba682bc34bb5c4f734e872e144df43022b4e3f389784aa5a11be8"),
        new("random", 0x8006272C, 0x30, "8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4"),
        new("vector-pack", 0x80017BFC, 0x28, "ef85ebacb8d15ec69efdbdfc9b342ec6ef5007f3216e8f2867baffb9519dd312"),
        new("renderer-kind-dispatch", 0x80057494, 0x64, "fe9649cc792838807a4f4218c80e4a9921e6af4a75645ab5d5eef0aa56c2c9d6"),
        new("negative-kind-prefetch", 0x80058080, 0x2C, "5d2a30a886c3a2e684b2a1216d0c9f477a470bd48d258f6e1c45aa0ffceff83e"),
        new("pool-init-and-terminal", 0x8001441C, 0x38, "3f2eab7fbf790f58eab1d2cd3d663bbfd8355b4ebd4bff1e4f6b43d387c12b7d"),
        new("normal-buffer-initializer", 0x8005B6F8, 0xE0, "d0b4cf5a7cf9ee407ff413f7261bd5f92f4880d7cee8467852f4d757e6b84295"),
        new("PETE-copy-and-normal-init", 0x8005B844, 0x8C, "0bd2bacdbb26dc622390405c6c154fa0ddb89c8a1e252704c7877c016e73a2ec"),
        new("renderer-camera-and-scratch-prologue", 0x800573C8, 0xF8, "bf93c47e0546f30ac8c1520d6b66d129a4ce4ee16bdce8ff23ea47ef37e5450d"),
        new("resident-sine-table", 0x8006CBF8, 0x280, "795a93bee7b4d49bef89d4df8c8628a4b4b3a91d694053dbc2b26b4293d4fa55")
    ];
}
