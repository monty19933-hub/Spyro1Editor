using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

/// <summary>Dream Weavers' resize cannon (class 0x5B, row 26), its gunner (a Winged Fool, class 0x5C, row 27) and the
/// Winged/Armored Fools (0x5C/0x82) name each other by row number, read by the game every frame:
/// <list type="bullet">
/// <item>The cannon's property word +0x04 is its gunner's row (read at 0x8007F558). While the gunner lives (state
/// below 0x7F) the cannon fires at any fool that calls it; once the gunner is dead (state 0x80 or more) Spyro can fire
/// it: standing within 0x4E2 units of it, behind it, facing the fuse (local point (-0x3E8, 0, 0x172)), a flame hit
/// (flag 0x10000) fires one cannonball (class 0x68) at the best target within +/-7/256 of the barrel and 0xAFFF units,
/// skipping classes 0xFA/0x6E (dragon statues and dragons) and aiming 0x320 higher at classes 0x5C/0x82.</item>
/// <item>A fool's +0x40 is the cannon's row. With +0x3C (call distance) above zero, once Spyro is that near or inside its
/// trigger area it writes its own row into the cannon's byte +0x49 and its +0x44 into the cannon's first property word,
/// then negates +0x3C (0x8008032C/0x80080388 Winged, 0x800843C8/0x80084424 Armored); the cannon reads byte +0x49 = 0
/// as no call, so a calling fool that lands in physical row 0 is never answered. An Armored Fool's +0x54/+0x58 name
/// partner fools whose call timers it restarts (0x80084460/0x8008449C; -1 for none).</item>
/// <item>The gunner (+0x34 = 1) switches to state 0x1E on its first update and from then on keeps 0x5DC units behind
/// the cannon, opposite Spyro, turning the cannon (yaw +0x46 through 0x80038EE0) to point away from itself - at Spyro
/// (0x80080C50). Nothing else turns the cannon except its own shot at a calling fool (0x8007FDF8). Spyro cannot turn
/// it: the barrel keeps the heading it had when the gunner was defeated.</item>
/// </list>
/// The cannon and its gunner are one required pair (<see cref="Capture"/>). A fool is linked to a cannon only when the
/// cannon is in its linked group (<see cref="Bind"/>); a fool without one never calls (+0x3C = 0, which every store to
/// it keeps at zero: they only negate it or clear it), forgets its partners (-1), and its now unread cannon word names
/// the fool's own row, so not even a wrong reading could touch another object.</summary>
public static class UniversalDreamWeaversCannonRows
{
    public const string SourceKey = "dreamweavers";
    public const string DataSha256 = "cadd3a2366eae64995c0eb40427410c569b6cbc8faea1ba0872639f31230c559";
    public const string OverlaySha256 = "dcbe76c16c51393e784037af420c7fb7c00cecd526ed4d890ae5f1b257cdb352";
    public const int Cannon = 0x05B, WingedFool = 0x05C, ArmoredFool = 0x082;
    public const int CannonRow = 26, GunnerRow = 27;
    public const int GunnerOffset = 0x04, ModeOffset = 0x34, CallOffset = 0x3C, CannonOffset = 0x40;
    public const int GunnerMode = 1;
    public static readonly IReadOnlyList<int> PartnerOffsets = [0x54, 0x58];
    /// <summary>Dream Weavers' Winged/Armored Fool rows other than the gunner.</summary>
    public static readonly IReadOnlySet<int> FoolRows = new HashSet<int>([.. Enumerable.Range(12, 14), 28, 29, 30]);
    /// <summary>The cannon's class reads of other objects (the target scan and the aim height), each comparison-only.</summary>
    public static readonly IReadOnlyList<uint> CannonClassReads = [0x8007FADC, 0x8007FC54];

    private const uint Update = 0x8007E3C0, Init = 0x80088E24, CopyBuffer = 0x8008BB38;

    public static bool IsSource(string? key) => string.Equals(key, SourceKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>The Add Object browser's Dream Weavers cannon (it brings its gunner).</summary>
    public static bool IsCannonRow(string? key, int row) => IsSource(key) && row == CannonRow;

    /// <summary>A fool that a cannon in its linked group can shoot at its call, and that the cannon targets: it moves and
    /// is removed on its own, not with the group.</summary>
    public static bool IsFoolRow(string? key, int row) => IsSource(key) && FoolRows.Contains(row);

    /// <summary>How a Dream Weavers fool behaves with a cannon, for the Add Object browser.</summary>
    public static string Describe(UniversalLevelData data, int row)
    {
        if (!FoolRows.Contains(row) || row >= data.Scene().RowCount) return "";
        var t = data.CaptureRow(SourceKey, row);
        if (t.Properties.Length < 0x44) return "";
        int call = I(t.Properties, CallOffset);
        string size = I(t.Properties, 0x10) != 0 ? "Starts big" : "Starts small";
        return call > 0
            ? $"{size}; with a Resize cannon in its group it calls the cannon once Spyro comes within {call / 16} units (or its trigger area), and the gunner's shot changes its size."
            : $"{size}; never calls a cannon, but a Resize cannon in its group can shoot it.";
    }

    private static bool IsFamily(UniversalRowTemplate t) => IsSource(t.SourceLevelKey) && t.ClassId is Cannon or WingedFool or ArmoredFool;
    private static bool IsGunner(UniversalRowTemplate t) => t.ClassId == WingedFool && t.Properties.Length >= ModeOffset + 4 && I(t.Properties, ModeOffset) == GunnerMode;

    /// <summary>The required links: the cannon names its gunner and the gunner its cannon. Fools are bound per group.</summary>
    public static UniversalRowTemplate Capture(UniversalLevelData data, UniversalOverlay overlay, UniversalRowTemplate t)
    {
        if (!IsFamily(t) || t.SourceRowIndices.Count > 0) return t;
        Prove(data, overlay);
        var original = data.CaptureRow(t.SourceLevelKey, t.SourceRowIndex);
        if (!t.Row.SequenceEqual(original.Row) || !t.Properties.SequenceEqual(original.Properties) ||
            !t.InternalPointers.SequenceEqual(original.InternalPointers) || t.ExternalRowPointers.Count != 0 || t.DroppedExternalPointers != 0)
            throw new InvalidDataException("A Dream Weavers cannon or fool differs from its original row.");
        if (t.ClassId == Cannon)
        {
            int gunner = Link(data, t, GunnerOffset, WingedFool);
            var g = data.CaptureRow(t.SourceLevelKey, gunner);
            if (!IsGunner(g) || I(g.Properties, CannonOffset) != t.SourceRowIndex)
                throw new InvalidDataException("The Dream Weavers cannon's gunner is not where the game keeps it.");
            return t with { SourceRowIndices = [new(GunnerOffset, gunner, WingedFool)] };
        }
        if (IsGunner(t))
        {
            int cannon = Link(data, t, CannonOffset, Cannon);
            if (I(data.CaptureRow(t.SourceLevelKey, cannon).Properties, GunnerOffset) != t.SourceRowIndex)
                throw new InvalidDataException("The Dream Weavers gunner's cannon is not where the game keeps it.");
            return t with { SourceRowIndices = [new(CannonOffset, cannon, Cannon)] };
        }
        if (!FoolRows.Contains(t.SourceRowIndex) || t.Properties.Length < 0x5C)
            throw new InvalidDataException("A Dream Weavers fool is not where the game keeps it.");
        return t;
    }

    /// <summary>Links a fool to the one cannon in its linked group (and an Armored Fool to its partner fools there), or
    /// keeps a Dream Weavers copy on the level's own cannon, or makes it never call. The cannon and the gunner must
    /// come as a pair in their group.</summary>
    public static UniversalRowTemplate Bind(UniversalRowTemplate t, UniversalMobyPlacement p, IReadOnlyList<UniversalMobyPlacement> placements,
        string destinationKey, UniversalLevelData destination, IReadOnlySet<int> removed, UniversalLevelData source)
    {
        if (p.SpecialId != null || !IsFamily(t)) return t;
        string group = p.LinkGroupId ?? "";
        var members = placements.Where(m => m.SpecialId == null && !m.Retired && IsSource(m.SourceLevelKey) && (m.LinkGroupId ?? "") == group).ToList();
        int Count(int row) => members.Count(m => m.SourceRowIndex == row);
        if (t.ClassId == Cannon || IsGunner(t))
        {
            if (group.Length == 0 || Count(CannonRow) != 1 || Count(GunnerRow) != 1)
                throw new InvalidDataException("A Dream Weavers Resize cannon and its gunner (a Winged Fool) belong together in one linked group. " +
                    "Remove the cannon and add it again from the Add Object browser, which adds its gunner with it.");
            return t;
        }
        byte[] props = t.Properties.ToArray();
        if (group.Length > 0 && Count(CannonRow) == 1)
        {
            List<UniversalSourceRowIndex> links = [new(CannonOffset, CannonRow, Cannon)];
            if (I(props, CannonOffset) != CannonRow) throw new InvalidDataException("A Dream Weavers fool does not name the cannon.");
            if (t.ClassId == ArmoredFool)
                foreach (int offset in PartnerOffsets)
                {
                    int partner = I(props, offset);
                    if (partner < 0) continue;
                    if (Count(partner) == 1 && partner < source.Scene().RowCount && Class(source, partner) == ArmoredFool) links.Add(new(offset, partner, ArmoredFool));
                    else W(props, offset, -1);
                }
            return t with { Properties = props, SourceRowIndices = links };
        }
        // A copy inside Dream Weavers itself still has the level's own cannon (and partners) at their original rows.
        if (IsSource(destinationKey) && Native(destination, removed, CannonRow, Cannon) && Native(destination, removed, GunnerRow, WingedFool))
        {
            foreach (int offset in t.ClassId == ArmoredFool ? PartnerOffsets : [])
                if (I(props, offset) is int partner and >= 0 && !Native(destination, removed, partner, ArmoredFool)) W(props, offset, -1);
            return t with { Properties = props };
        }
        W(props, CallOffset, 0);
        if (t.ClassId == ArmoredFool) foreach (int offset in PartnerOffsets) W(props, offset, -1);
        W(props, CannonOffset, 0);
        return t with { Properties = props, OwnRowIndexOffsets = [CannonOffset] };
    }

    /// <summary>Removing Dream Weavers' own cannon, gunner or a partner fool would let the objects that still name it touch
    /// whatever takes its slot.</summary>
    public static void RequireNativeRemoval(string destinationKey, UniversalLevelData current, UniversalLevelData original,
        UniversalOverlay overlay, IReadOnlySet<int> removed)
    {
        if (removed.Count == 0 || !IsSource(destinationKey)) return;
        Prove(original, overlay);
        for (int row = 0; row < original.Scene().RowCount; row++)
        {
            if (removed.Contains(row) || row >= current.Scene().RowCount) continue;
            int c = Class(original, row);
            if (c is not (Cannon or WingedFool or ArmoredFool) || Class(current, row) != c) continue;
            var t = current.CaptureRow(destinationKey, row);
            List<int> named = [];
            if (c == Cannon) named.Add(I(t.Properties, GunnerOffset));
            else if (IsGunner(t)) named.Add(I(t.Properties, CannonOffset));
            else if (I(t.Properties, CallOffset) != 0)
            {
                named.Add(I(t.Properties, CannonOffset));
                if (c == ArmoredFool) named.AddRange(PartnerOffsets.Select(o => I(t.Properties, o)).Where(r => r >= 0));
            }
            foreach (int target in named.Where(removed.Contains))
                throw new InvalidDataException($"Dream Weavers row {row} still names row {target} (the resize cannon, its gunner or a partner fool); " +
                    "remove them together or keep both.");
        }
    }

    /// <summary>When Dream Weavers' classes are renumbered in the destination, the cannon's two class reads of other
    /// objects (the target scan skipping dragons and the fools' aim height) see source numbers again. Both are
    /// comparison-only: the class register is compared with constants and then overwritten.</summary>
    internal static IReadOnlyList<uint> TranslateOwnedReads(UniversalAsmPart part, string sourceKey, UniversalOverlay source,
        IReadOnlyList<int> classes, IReadOnlyDictionary<int, int> map)
    {
        if (!IsSource(sourceKey) || !classes.Contains(Cannon) || !map.Any(m => m.Key != m.Value)) return [];
        ProveOverlay(source);
        var included = part.Items.Where(i => i.Word != null && (i.SourcePc ?? i.Orig) != null).Select(i => (i.SourcePc ?? i.Orig)!.Value).ToHashSet();
        List<uint> sites = [];
        foreach (uint read in CannonClassReads)
        {
            var copies = Enumerable.Range(0, part.Items.Count).Where(i => (part.Items[i].SourcePc ?? part.Items[i].Orig) == read).ToArray();
            if (copies.Length == 0 || copies.Any(i => part.Items[i].Word != source.Word(read) || part.Items[i].Fix.Kind != UniversalFixKind.None || part.Items[i].ClassReadOwned))
                throw new InvalidDataException($"The Dream Weavers cannon's class read at 0x{read:X8} is missing or changed.");
            if (!UniversalClassRemapping.ComparisonOnly(source, included, read + 4, UniversalMips.Rt(source.Word(read)), Enumerable.Range(0, 512).ToArray()))
                throw new InvalidDataException($"The Dream Weavers cannon's class read at 0x{read:X8} is not only compared.");
            foreach (int index in copies.OrderDescending())
                UniversalClassRemapping.TranslateOwnedClassification(part, index, map, "dw_cannon_target_class");
            sites.Add(read);
        }
        return sites;
    }

    /// <summary>The source is the original Dream Weavers data and overlay, and every row-number reader, producer and
    /// call guard this mapping relies on is where it was analysed.</summary>
    public static void Prove(UniversalLevelData data, UniversalOverlay overlay)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(data.Bytes)) != DataSha256)
            throw new InvalidDataException("Dream Weavers cannon rows need the original Dream Weavers level data.");
        ProveOverlay(overlay);
    }

    private static readonly object ProofLock = new();
    private static readonly HashSet<string> ProvedOverlays = [];

    private static void ProveOverlay(UniversalOverlay overlay)
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes));
        if (hash != OverlaySha256 || overlay.Update != Update || overlay.Init != Init || overlay.CopyBuffer != CopyBuffer)
            throw new InvalidDataException("Dream Weavers cannon rows need the original Dream Weavers code.");
        lock (ProofLock) if (ProvedOverlays.Contains(hash)) return;
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
        (HashSet<uint> Owned, HashSet<uint> Trace) Handler(int c)
        {
            var trace = UniversalDispatchAnalysis.Trace(overlay, sw.TreeStart, c, sw.Seeds, sw.ClassRegister);
            var owned = UniversalDispatchAnalysis.Closure(overlay, sw.Function, [trace.Stop], new HashSet<uint> { sw.TreeStart }).Instructions.ToHashSet();
            return (owned, trace.Sequence.Where(r => r.SourcePc != null).Select(r => r.SourcePc!.Value).ToHashSet());
        }
        void Packet(HashSet<uint> owned, uint start, params uint[] words)
        {
            for (int i = 0; i < words.Length; i++)
                if (!owned.Contains(start + (uint)i * 4) || overlay.Word(start + (uint)i * 4) != words[i])
                    throw new InvalidDataException($"Dream Weavers cannon code changed at 0x{start + (uint)i * 4:X8}.");
        }
        // Loads and stores of one properties word through the handler's properties register.
        uint[] Access(HashSet<uint> owned, int register, int offset, bool store) => owned.Where(pc =>
            (store ? UniversalMips.IsStore(overlay.Word(pc)) : UniversalMips.IsLoad(overlay.Word(pc))) &&
            UniversalMips.Rs(overlay.Word(pc)) == register && UniversalMips.SImm(overlay.Word(pc)) == offset).Order().ToArray();
        void Exactly(uint[] found, uint[] expected, string what)
        {
            if (!found.SequenceEqual(expected)) throw new InvalidDataException($"Dream Weavers {what} changed.");
        }

        var cannon = Handler(Cannon);
        // lw s5,0(s3); move a0,s3; lw v1,4(s5); the 0x58-stride row address of the gunner into s0, then its state.
        Packet(cannon.Trace, 0x8007F550, 0x8E750000, 0x02602021, 0x8EA30004, 0x24050004, 0x00031040, 0x00431021,
            0x00021080, 0x00431023, 0x3C038007, 0x8C635828, 0x000210C0);
        Packet(cannon.Owned, 0x8007F57C, 0x0C014A79, 0x00628021);
        Packet(cannon.Owned, 0x8007F66C, 0x92020048, 0, 0x2C42007F);
        Packet(cannon.Owned, 0x8007F6C0, 0x92020048, 0, 0x2C420080);
        Exactly(Access(cannon.Owned, 21, GunnerOffset, true), [], "cannon gunner word producer");
        // Target scan: skip classes 0xFA/0x6E; aim 0x320 higher at 0x5C/0x82.
        Packet(cannon.Owned, 0x8007FAD8, 0x240200FA, 0x86430036, 0, 0x10620051, 0x2402006E, 0x1062004F);
        Packet(cannon.Owned, 0x8007FC54, 0x87C30036, 0x2402005C, 0x10620003, 0x24020082, 0x14620006);

        var winged = Handler(WingedFool);
        Packet(winged.Trace, 0x80080094, 0x8E710000);
        Exactly(Access(winged.Owned, 17, CannonOffset, false), [0x8008032C, 0x80080388, 0x80080C50], "Winged Fool cannon readers");
        Exactly(Access(winged.Owned, 17, CannonOffset, true), [], "Winged Fool cannon producers");
        Exactly(Access(winged.Owned, 17, CallOffset, true), [0x80080320, 0x80080328, 0x80080408, 0x8008040C], "Winged Fool call producers");
        // lw a0,+3C; bnez a0 (else only a cannonball hit); ...; blez a0 skips the call.
        Packet(winged.Owned, 0x80080290, 0x8E24003C, 0, 0x14800006);
        Packet(winged.Owned, 0x800802B4, 0x18800045);
        // The four stores: -(+3C) or -1 after a call; -(+3C) or 0 after a cannonball hit.
        Packet(winged.Owned, 0x80080310, 0x8E22003C, 0, 0x00021023, 0x14400003, 0xAE22003C, 0x2402FFFF, 0xAE22003C);
        Packet(winged.Owned, 0x800803F8, 0x8E22003C, 0, 0x00021023, 0x08020104, 0xAE22003C, 0xAE20003C);
        // +34 = 1: the gunner (state 0x1E, which reads +40 every frame).
        Packet(winged.Owned, 0x800804C0, 0x8E220034, 0x24090001, 0x14490004, 0x2402001E, 0xAE200034, 0x08022376, 0xA2620048);

        var armored = Handler(ArmoredFool);
        Packet(armored.Trace, 0x800840F0, 0x8E700000);
        Exactly(Access(armored.Owned, 16, CannonOffset, false), [0x800843C8, 0x80084424, 0x80084DA0], "Armored Fool cannon readers");
        Exactly(Access(armored.Owned, 16, 0x54, false), [0x80084460], "Armored Fool first partner reader");
        Exactly(Access(armored.Owned, 16, 0x58, false), [0x8008449C], "Armored Fool second partner reader");
        foreach (int offset in new[] { CannonOffset, 0x54, 0x58, 0x60 })
            Exactly(Access(armored.Owned, 16, offset, true), [], "Armored Fool row-word producers");
        Exactly(Access(armored.Owned, 16, 0x60, false), [], "Armored Fool +60 reader");
        Exactly(Access(armored.Owned, 16, CallOffset, true), [0x800843BC, 0x800843C4, 0x80084514, 0x80084518], "Armored Fool call producers");
        Packet(armored.Owned, 0x80084324, 0x8E05003C, 0, 0x14A00006);
        Packet(armored.Owned, 0x80084348, 0x18A00063);
        Packet(armored.Owned, 0x800843AC, 0x8E02003C, 0, 0x00021023, 0x14400003, 0xAE02003C, 0x2402FFFF, 0xAE02003C);
        Packet(armored.Owned, 0x80084504, 0x8E02003C, 0, 0x00021023, 0x08021147, 0xAE02003C, 0xAE00003C);
        // Partners are skipped when -1.
        Packet(armored.Owned, 0x80084460, 0x8E030054, 0x2404FFFF, 0x1064000C);
        Packet(armored.Owned, 0x8008449C, 0x8E030058, 0, 0x10640055);
        // State 0x1E (the gunner's circling) is only entered through the Winged Fool's +34 = 1 check: no Armored Fool
        // store gives a state the value 0x1E.
        if (armored.Owned.Any(pc => UniversalMips.Op(overlay.Word(pc)) == 9 && UniversalMips.Rs(overlay.Word(pc)) == 0 && UniversalMips.SImm(overlay.Word(pc)) == 0x1E))
            throw new InvalidDataException("Dream Weavers Armored Fool code can enter the gunner's state.");
        lock (ProofLock) ProvedOverlays.Add(hash);
    }

    private static bool Native(UniversalLevelData d, IReadOnlySet<int> removed, int row, int c) =>
        !removed.Contains(row) && row < d.Scene().RowCount && Class(d, row) == c;

    private static int Link(UniversalLevelData data, UniversalRowTemplate t, int offset, int targetClass)
    {
        if (t.Properties.Length < offset + 4 || t.InternalPointers.Any(p => p.Offset == offset))
            throw new InvalidDataException("A Dream Weavers cannon link is not where the game keeps it.");
        int row = I(t.Properties, offset);
        if (row < 0 || row >= data.Scene().RowCount || Class(data, row) != targetClass)
            throw new InvalidDataException("A Dream Weavers cannon link is not where the game keeps it.");
        return row;
    }

    private static int Class(UniversalLevelData d, int row) => BinaryPrimitives.ReadUInt16LittleEndian(d.Row(row).AsSpan(0x36));
    private static int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void W(byte[] b, int at, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), v);
}
