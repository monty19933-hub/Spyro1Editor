using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

/// <summary>An issued, source-specific decision for Dry Canyon's unreachable legacy B7 bird
/// spawn. The real E6 bird path remains in the copied behavior. This is source/asset authority,
/// not a gameplay receipt.</summary>
public sealed class UniversalDryCanyonBirdCertificate
{
    internal const uint DecisionPc = 0x8008461C, ReachableTarget = 0x80084720;
    internal const uint RemovedStart = 0x80084624, RemovedEnd = 0x80084720;
    internal const uint ActualBirdSpawn = 0x80084944;
    private readonly UniversalOverlay _owner;
    private UniversalDryCanyonBirdCertificate(UniversalOverlay owner) { _owner = owner; }
    internal static UniversalDryCanyonBirdCertificate Issue(UniversalLevelData data, UniversalOverlay overlay, byte[] executable)
    {
        UniversalDryCanyonBirdBranches.RequireSource(data, overlay, executable);
        return new(overlay);
    }
    public string SourceLevelKey => "drycanyon";
    public int OwnerClassId => 0xB8;
    public uint UnreachableSpawnPc => 0x80084644;
    public uint RetainedBirdSpawnPc => ActualBirdSpawn;
    internal IReadOnlyDictionary<uint, (bool Taken, bool NotTaken)> Decisions { get; } =
        new ReadOnlyDictionary<uint, (bool, bool)>(new Dictionary<uint, (bool, bool)> { [DecisionPc] = (true, false) });

    internal void RequireOwner(UniversalOverlay overlay)
    {
        // Full bytes and all callbacks bind the proof even when a caller presents another view.
        UniversalDryCanyonBirdBranches.RequireOriginalOverlay(overlay);
        if (overlay.Update != _owner.Update || overlay.Init != _owner.Init || overlay.CopyBuffer != _owner.CopyBuffer)
            throw new InvalidDataException("Dry Canyon bird certificate does not own these callbacks.");
    }
    internal bool TryDecision(uint pc, out uint target)
    {
        target = ReachableTarget;
        return pc == DecisionPc;
    }
    internal void RequireEmitted(UniversalAsmPart part)
    {
        var words = part.Items.Where(i => i.Word != null).ToArray();
        var matches = words.Select((it, i) => (it, i)).Where(p => p.it.Orig == DecisionPc).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Dry Canyon bird decision was not emitted exactly once.");
        var (decision, at) = matches[0];
        if (decision.Word != UniversalMips.EncodeI(4, 0, 0, 0) || decision.Fix.Kind != UniversalFixKind.Branch || decision.Fix.Orig != ReachableTarget ||
            at + 1 >= words.Length || words[at + 1].Orig != DecisionPc + 4 || words[at + 1].Word != 0 || words[at + 1].Fix.Kind != UniversalFixKind.None)
            throw new InvalidDataException("Dry Canyon bird decision changed its target or delay slot.");
        if (words.Any(i => i.Orig is uint p && p >= RemovedStart && p < RemovedEnd) ||
            words.Any(i => i.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump && i.Fix.Orig >= RemovedStart && i.Fix.Orig < RemovedEnd))
            throw new InvalidDataException("Dry Canyon legacy B7 body or incoming edge survived the issued decision.");
        uint[] actualPacket = [0x240400E6, 0x02802821, 0x3C038007, 0x8C6358CC, 0, 0x0060F809, 0x00408821, 0x00409021];
        var actual = words.Select((it, i) => (it, i)).Where(p => p.it.Orig == 0x80084930).ToArray();
        if (actual.Length != 1 || actual[0].i + actualPacket.Length > words.Length)
            throw new InvalidDataException("Dry Canyon real E6 bird spawn was not retained.");
        for (int i = 0; i < actualPacket.Length; i++)
        {
            var item = words[actual[0].i + i];
            if (item.Orig != 0x80084930 + (uint)i * 4 || item.Word != actualPacket[i] || item.Fix.Kind != UniversalFixKind.None)
                throw new InvalidDataException("Dry Canyon real E6 bird spawn packet changed or was reordered.");
        }
    }
}

public static class UniversalDryCanyonBirdBranches
{
    private const string DataHash = "94cc8d715ea0564ac50a456fb852ff80393a97c0963fe7b5bb97ad3a9ad23224";
    private const string OverlayHash = "8aa4036028de3ea9cdc6ee93b4d96cb7dc2b9fa5f818b6e748c0c2bda946cc0f";
    private const string ModelHash = "a91d79a488be9802bc8e894dd7b63f1feec9d02593b61cea07095ee44651285c";
    private const string TickHash = "43915998682dbb7fe33175b21f638f2720ce954efe2dd3079f57bb51275be512";
    private static readonly int[] FrameCounts = [45, 1, 4, 15, 18, 4, 40];
    private static readonly uint[] AnimationRoots = [0x54, 0x1E0, 0x20C, 0x250, 0x2EC, 0x3A0, 0x3E4];
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Issue only for the complete original B8 source and native frame engine. Original
    /// state2 writers select animation2 before returning; native frame-event bit1 is set after
    /// copying next animation/frame into current animation/frame, including crossfades. Animation2
    /// has four frames, so its event can never have current frame17. Flag0 alone is not assumed.</summary>
    public static UniversalDryCanyonBirdCertificate? Capture(string sourceKey, UniversalLevelData data,
        UniversalOverlay overlay, byte[] executable)
    {
        if (!string.Equals(sourceKey, "drycanyon", StringComparison.OrdinalIgnoreCase)) return null;
        return UniversalDryCanyonBirdCertificate.Issue(data, overlay, executable);
    }
    internal static void RequireSource(UniversalLevelData data, UniversalOverlay overlay, byte[] executable)
    {
        RequireOriginalOverlay(overlay);
        if (Hash(data.Bytes) != DataHash || Hash(data.Package(0xB8)) != ModelHash)
            throw new InvalidDataException("Dry Canyon bird source data or original animation asset changed.");
        RequireNative(executable);
        RequireAnimationFrames(data.Package(0xB8));
        int[] rows = Enumerable.Range(0, data.Scene().RowCount).Where(i => BinaryPrimitives.ReadUInt16LittleEndian(data.Row(i).AsSpan(0x36)) == 0xB8).ToArray();
        if (!rows.SequenceEqual(new[] { 51, 52, 53, 54, 55, 56 })) throw new InvalidDataException("Dry Canyon original bird owner rows changed.");
        foreach (int row in rows) RequireInitialRow(data.Row(row));
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
        var tr = UniversalDispatchAnalysis.Trace(overlay, sw.TreeStart, 0xB8, sw.Seeds, sw.ClassRegister);
        var own = UniversalDispatchAnalysis.Closure(overlay, sw.Function, [tr.Stop], new HashSet<uint> { sw.TreeStart }).Instructions;
        // Exact two own state2 writers and next-animation2 transitions. Other states cannot
        // enter the frame17 check directly: the authenticated state jump table selects84600.
        RequireWords(overlay, 0x800845C8, [0x9282003D, 0x24090002, 0xA2890048, 0x24090002, 0x104917AA, 0x02802021, 0x24030010, 0x9082003D, 0x9085003F, 0x24090002, 0xA0830040, 0xA0830041, 0x08021B3D, 0xA089003D]);
        RequireWords(overlay, 0x800848C0, [0x9282003D, 0x24090002, 0xA2890048, 0x24090002, 0x104916EC, 0x02802021, 0x24030010, 0x9082003D, 0x9085003F, 0x24090002, 0xA0830040, 0xA0830041, 0x08021B3D, 0xA089003D]);
        RequireWords(overlay, 0x80084600, [0x3C028007, 0x8C4257F4, 0x24090001, 0x10400044, 0xAEA90004, 0x9283003E, 0x24020011, 0x14620040, 0]);
        RequireWords(overlay, 0x80086CF4, [0xA080003F, 0xA082003C, 0x0C00DFA6, 0xA085003E, 0x08022921, 0]);
        RequireWords(overlay, 0x8007E304, [0x92830042, 0x3C018007, 0xAC2256C4, 0x96820036, 0x30630001, 0x3C018007, 0xAC2357F4]);
        if (!own.Contains(0x80084644) || !own.Contains(0x80084944)) throw new InvalidDataException("Dry Canyon bird producer closure changed.");
        var all = UniversalDispatchAnalysis.Closure(overlay, sw.Function, [sw.Function], new HashSet<uint>());
        foreach (uint pc in all.Instructions)
        {
            uint w = overlay.Word(pc);
            if (UniversalMips.IsPcRelative(w) || UniversalMips.Op(w) is 2 or 3)
            {
                uint target = UniversalMips.BranchTarget(w, pc);
                if (target >= UniversalDryCanyonBirdCertificate.RemovedStart && target < UniversalDryCanyonBirdCertificate.RemovedEnd)
                    throw new InvalidDataException("Dry Canyon legacy bird block has an alternate direct entry.");
            }
        }
        if (all.Tables.SelectMany(t => t.Targets).Any(p => p >= UniversalDryCanyonBirdCertificate.RemovedStart && p < UniversalDryCanyonBirdCertificate.RemovedEnd))
            throw new InvalidDataException("Dry Canyon legacy bird block has an alternate jump-table entry.");
    }

    internal static void RequireOriginalOverlay(UniversalOverlay overlay)
    {
        if (Hash(overlay.Bytes) != OverlayHash || overlay.Update != 0x8007E240 || overlay.Init != 0x8008A4D0 || overlay.CopyBuffer != 0x8008D600)
            throw new InvalidDataException("Dry Canyon bird source overlay or callbacks changed.");
    }
    public static void RequireNative(byte[] executable)
    {
        const int start = (int)(0x800522C0u - 0x8000F800u), length = (int)(0x80052464u - 0x800522C0u);
        if (executable.Length < start + length || Hash(executable.AsSpan(start, length)) != TickHash)
            throw new InvalidDataException("Dry Canyon bird native animation event/transition engine changed.");
        // Source transition calls this certified no-op. It cannot rewrite animation state.
        int noOp = (int)(0x80037E98u - 0x8000F800u);
        if (executable.Length < noOp + 8 || BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(noOp)) != 0x03E00008 ||
            BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(noOp + 4)) != 0)
            throw new InvalidDataException("Dry Canyon bird animation transition callee changed.");
    }
    public static void RequireInitialRow(byte[] row)
    {
        if (row.Length != UniversalLevelData.RowStride || row.AsSpan(0x3C, 5).IndexOfAnyExcept((byte)0) >= 0 || row[0x41] != 32 || row[0x42] != 0 || row[0x48] != 0)
            throw new InvalidDataException("Dry Canyon bird branch proof requires the original initial animation/state.");
    }
    public static void RequireAnimationFrames(byte[] model)
    {
        if (model.Length < 0x54 || BinaryPrimitives.ReadInt32LittleEndian(model) != FrameCounts.Length)
            throw new InvalidDataException("Dry Canyon bird animation count changed.");
        for (int i = 0; i < FrameCounts.Length; i++)
        {
            uint root = BinaryPrimitives.ReadUInt32LittleEndian(model.AsSpan(0x38 + 4 * i));
            if (root != AnimationRoots[i] || root > model.Length - 0x24 || BinaryPrimitives.ReadUInt16LittleEndian(model.AsSpan((int)root)) != FrameCounts[i])
                throw new InvalidDataException("Dry Canyon bird animation frame domain changed.");
        }
    }
    private static void RequireWords(UniversalOverlay overlay, uint pc, uint[] words)
    {
        for (int i = 0; i < words.Length; i++) if (overlay.Word(pc + (uint)i * 4) != words[i])
            throw new InvalidDataException($"Dry Canyon bird source packet changed at 0x{pc + (uint)i * 4:X8}.");
    }
}
