using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>How many egg thieves a finished game holds: per level of the game (36 places) and in levels beyond them
/// (the created level, a new world's levels).</summary>
public sealed record EggThiefCount(IReadOnlyList<int> PerLevel, int Beyond)
{
    public int Total => PerLevel.Sum() + Beyond;
    /// <summary>Thieves in levels whose eggs the game has no place of their own to count in (worlds four to six, the
    /// created level, a new world): their eggs are counted together.</summary>
    public int Unlisted => PerLevel.Skip(NativeEggTotals.ListedLevels).Sum() + Beyond;
    public bool IsRetail => Beyond == 0 && PerLevel.SequenceEqual(NativeEggTotals.RetailPerLevel);
}

/// <summary>What <see cref="NativeEggTotals.ApplyForExport"/> did.</summary>
public sealed record EggTotalsApplied(EggThiefCount Count, int Bucket, IReadOnlyList<int> RedirectedOverlays)
{
    public int Total => Count.Total;
}

/// <summary>The game's dragon-egg totals follow the egg thieves its levels really hold.
/// <para>The game (USA) knows of twelve eggs in four places, none of them read from the levels: a table of eggs per
/// level for the first three worlds (0x8006F480, 18 bytes: the inventory draws that many egg places on a level's line),
/// the inventory's row of twelve egg places (loop bound 0x8001B9EC), the completion percentage (0x8002BB20: 50 a gem,
/// 6,000 a dragon, 10,000 an egg, over 12,000) and the doorway to Gnasty's Loot (Gnasty's World 0x800818B0: at least
/// twelve eggs). An egg thief added to a level, or taken out of one, changes none of them, so the inventory showed
/// thirteen eggs of twelve, the percentage passed 100, or 100% could not be reached.</para>
/// <para>Collected eggs are counted by the egg itself (class 0x22, in every level's code): the whole game's count
/// 0x80075810 and the level's own 0x80076FE8 + 4 x level index, an array of 18 words (the save keeps 18 bytes and adds
/// them up at load). In a level past the first three worlds that write lands in other variables and the egg is lost at
/// the next load; there the egg's level index is replaced by a spare place among the eighteen (a flight level's: no
/// line shows it, nothing else reads it), so the egg is counted and saved like any other.</para>
/// <para>Thieves are counted from the finished levels themselves (rows whose model is a thief's and that still carry an
/// egg), so every way of adding or removing one is covered. With the game's own twelve thieves in their places nothing
/// is written.</para></summary>
public static class NativeEggTotals
{
    public const int RetailTotal = 12, ListedLevels = 18, Levels = 36;
    /// <summary>The most eggs the inventory's row can show (its places are drawn closer together past seventeen).</summary>
    public const int MaxTotal = 60;
    public const uint LevelTotals = 0x8006F480, CollectedPerLevel = 0x80076FE8, CollectedTotal = 0x80075810;
    public const uint RowMarkStep = 0x8001B9E4, RowBound = 0x8001B9EC, RowEggStep = 0x8001B9F4;
    public const uint Percentage = 0x8002BB20; public const int PercentageWords = 48;
    public const uint LootGate = 0x800818B0;
    public const int RowStep = 27, RowFirst = 24, RowLast = 480;
    private const int GnastysWorldIndex = 30;
    /// <summary>The spare places an unlisted level's eggs can be counted in, in order of choice: the three flight levels
    /// of the first worlds (a thief has no business there), then any other level without thieves that no balloonist
    /// asks about (their egg hints read Stone Hill, Dry Canyon, Cliff Town, Alpine Ridge and Wizard Peak).</summary>
    private static readonly int[] BucketChoices = [5, 11, 17, 4, 10, 16, 2, 9, 0, 3, 6, 12, 14];

    public static IReadOnlyList<int> RetailPerLevel { get; } =
        [0, 1, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 2, 1, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>The egg's count of itself: lui a1,0x8007; lw a1,0x5964(a1) [level index]; lui v0,0x8007;
    /// addiu v0,v0,0x6FE8; sll a1,a1,2; addu a1,a1,v0.</summary>
    private static readonly uint[] EggCountsItself = [0x3C058007, 0x8CA55964, 0x3C028007, 0x24426FE8, 0x00052880, 0x00A22821];

    public static int DataEntry(int levelIndex) => 10 + 2 * levelIndex;
    public static int OverlayEntry(int levelIndex) => 9 + 2 * levelIndex;

    /// <summary>The egg thieves a level's data holds: rows whose model is an egg thief's (whatever class number the
    /// level gives it), not hidden away and still carrying their egg.</summary>
    public static int ThievesIn(byte[] levelData)
    {
        try
        {
            var classes = NativeEggThiefColours.ThiefClasses(levelData);
            if (classes.Count == 0) return 0;
            var data = new UniversalLevelData(levelData);
            int rows = data.Scene().RowCount, count = 0;
            for (int i = 0; i < rows; i++)
            {
                byte[] row = data.Row(i);
                if (!classes.Contains(BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36))) || row[0x53] == 0xFF) continue;
                // A row put out of the way (the old way of removing one) is not in the level.
                if (BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(0x0C)) <= -400000) continue;
                count++;
            }
            return count;
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException) { return 0; }
    }

    /// <summary>The thieves in every level of a game: <paramref name="entry"/> gives a WAD entry's bytes;
    /// <paramref name="beyond"/> the data entries of levels past the game's 36 places (a new world's).</summary>
    public static EggThiefCount Count(Func<int, byte[]> entry, IEnumerable<int>? beyond = null)
    {
        int[] per = new int[Levels];
        for (int i = 0; i < Levels; i++)
        {
            byte[] data;
            try { data = entry(DataEntry(i)); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException) { continue; }
            per[i] = ThievesIn(data);
        }
        int more = 0;
        foreach (int e in beyond ?? []) more += ThievesIn(entry(e));
        return new(per, more);
    }

    /// <summary>The completion percentage with <paramref name="total"/> eggs in the game: as the game's own
    /// (50 a gem, 6,000 a dragon, 10,000 an egg), over 12,000 + 100 for every egg past twelve, so that everything
    /// collected is still exactly 100; past 12,000 treasure it is 100 + (treasure - 12,000) / 100, as in the game.</summary>
    public static uint[] PercentageRoutine(int total)
    {
        if (total is < 0 or > MaxTotal) throw new ArgumentOutOfRangeException(nameof(total));
        const int Zero = 0, V0 = 2, V1 = 3, A0 = 4, A1 = 5, A2 = 6, T0 = 8, T1 = 9, T2 = 10, Ra = 31;
        static uint I(int op, int rs, int rt, int imm) => UniversalMips.EncodeI(op, rs, rt, imm);
        static uint R(int rs, int rt, int rd, int fn) => (uint)(rs << 21 | rt << 16 | rd << 11 | fn);
        List<uint> w =
        [
            I(15, Zero, T0, 0x8007),
            I(35, T0, A2, 0x5860),                 // treasure
            I(35, T0, A0, 0x5750),                 // dragons
            I(35, T0, A1, 0x5810),                 // eggs
            I(9, Zero, T1, 50),
            R(A2, T1, Zero, 0x18),                 // mult
            R(Zero, Zero, V1, 0x12),               // mflo v1
            I(9, Zero, T1, 6000), 0,
            R(A0, T1, Zero, 0x18),
            R(Zero, Zero, T2, 0x12),
            R(V1, T2, V1, 0x21),
            I(9, Zero, T1, 10000),
            R(A1, T1, Zero, 0x18),
            R(Zero, Zero, T2, 0x12),
            R(V1, T2, V1, 0x21),
            I(9, Zero, T1, Divisor(total)),
            R(V1, T1, Zero, 0x1A),                 // div
            R(Zero, Zero, V1, 0x12),
            I(10, A2, V0, 12001),                  // slti v0, a2, 12001
            I(5, V0, Zero, 5),                     // bnez v0, done
            I(9, Zero, T1, 100),
            I(9, A2, V0, -12000),
            R(V0, T1, Zero, 0x1A),
            R(Zero, Zero, V0, 0x12),
            I(9, V0, V1, 100),
            R(Ra, Zero, Zero, 0x08),               // done: jr ra
            R(V1, Zero, V0, 0x21),                 //       move v0, v1
        ];
        if (w.Count > PercentageWords) throw new InvalidOperationException("The percentage routine outgrew the game's own.");
        while (w.Count < PercentageWords) w.Add(0);
        return [.. w];
    }

    /// <summary>What the percentage divides by with <paramref name="total"/> eggs in the game.</summary>
    public static int Divisor(int total) => 12000 + 100 * (total - RetailTotal);

    /// <summary>The percentage the game shows (the routine's arithmetic).</summary>
    public static int Percent(int treasure, int dragons, int eggs, int total)
    {
        int sum = 50 * treasure + 6000 * dragons + 10000 * eggs, percent = sum / Divisor(total);
        return treasure >= 12001 ? 100 + (treasure - 12000) / 100 : percent;
    }

    /// <summary>How far apart the inventory draws its row of egg places: the game's 27 while they fit, closer after.</summary>
    public static int RowStepFor(int total) => total <= 1 ? RowStep : Math.Clamp((RowLast - RowFirst) / (total - 1), 6, RowStep);

    /// <summary>Sets the game's egg totals from the thieves its levels hold. <paramref name="replacements"/> are the WAD
    /// entries this export has changed (an overlay patched here is added to them), <paramref name="entryBytes"/> the
    /// rest; <paramref name="guests"/> the levels past the game's 36 places (overlay entry, data entry). Does nothing,
    /// and returns null, while the game's own twelve thieves are where the game put them and nothing here was changed
    /// before.</summary>
    public static EggTotalsApplied? ApplyForExport(byte[] exe, ReadOnlySpan<byte> originalExe, IDictionary<int, byte[]> replacements,
        Func<int, byte[]> entryBytes, IReadOnlyList<(int Overlay, int Data)>? guests = null)
    {
        byte[] Entry(int e) => replacements.TryGetValue(e, out var changed) ? changed : entryBytes(e);
        var count = Count(Entry, guests?.Select(g => g.Data));
        byte[] original = originalExe.ToArray();
        bool touched = !Same(exe, original, LevelTotals, ListedLevels) || !Same(exe, original, RowBound, 4) || !Same(exe, original, Percentage, 4 * PercentageWords);
        if (count.IsRetail && !touched) return null;
        int total = count.Total;
        if (total > MaxTotal)
            throw new InvalidDataException($"This game would have {total} dragon eggs (one for every egg thief), and the inventory can show {MaxTotal}. Take out {total - MaxTotal} egg thieves and Create BIN again.");

        // Where the eggs of levels without a place of their own are counted.
        int bucket = -1;
        if (count.Unlisted > 0)
        {
            foreach (int choice in BucketChoices)
                if (count.PerLevel[choice] == 0) { bucket = choice; break; }
            if (bucket < 0) throw new InvalidDataException("Every one of the game's first eighteen levels has an egg thief, so the eggs of thieves in later levels cannot be counted. Take an egg thief out of one of the flight levels.");
        }

        // The inventory: egg places on each level's line, and the row of all of them.
        for (int i = 0; i < ListedLevels; i++) exe[Off(LevelTotals) + i] = (byte)Math.Min(255, count.PerLevel[i]);
        SetImmediate(exe, RowBound, 0x2A620000, total, "the inventory's egg row");
        int step = RowStepFor(total);
        SetImmediate(exe, RowMarkStep, 0x26310000, step, "the inventory's egg row");
        SetImmediate(exe, RowEggStep, 0x26100000, step, "the inventory's egg row");

        // The percentage: the game's own routine, or the one that also counts dragonfly eggs.
        int dragonfly = InstalledDragonflyEggs(exe, original);
        if (dragonfly > 0)
        {
            byte[] routine = UniversalEggProgress.PercentageRoutine(dragonfly, total);
            routine.CopyTo(exe, Off(Percentage));
        }
        else if (total == RetailTotal) original.AsSpan(Off(Percentage), 4 * PercentageWords).CopyTo(exe.AsSpan(Off(Percentage)));
        else
        {
            if (!Same(exe, original, Percentage, 4 * PercentageWords) && !IsOwnPercentage(exe))
                throw new InvalidDataException("The game's completion percentage is not the USA game's own, so it cannot be set for the egg thieves in this game.");
            uint[] routine = PercentageRoutine(total);
            for (int i = 0; i < routine.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(Percentage) + 4 * i), routine[i]);
        }

        // The doorway to Gnasty's Loot asks for every egg.
        int gateEntry = OverlayEntry(GnastysWorldIndex);
        byte[] gate = Entry(gateEntry);
        if (SetLootGate(gate, total) is { } gated) replacements[gateEntry] = gated;

        // Levels whose eggs have no place of their own count them in the spare place.
        List<int> redirected = [];
        if (bucket >= 0)
        {
            for (int i = ListedLevels; i < Levels; i++)
                if (count.PerLevel[i] > 0 && Redirect(Entry(OverlayEntry(i)), bucket) is { } overlay) { replacements[OverlayEntry(i)] = overlay; redirected.Add(OverlayEntry(i)); }
            foreach (var (overlayEntry, dataEntry) in guests ?? [])
                if (ThievesIn(Entry(dataEntry)) > 0 && Redirect(Entry(overlayEntry), bucket) is { } overlay) { replacements[overlayEntry] = overlay; redirected.Add(overlayEntry); }
        }
        return new(count, bucket, redirected);
    }

    /// <summary>The overlay with every egg counting itself in <paramref name="bucket"/> instead of the level's own
    /// place; null when it has no egg code or already does.</summary>
    public static byte[]? Redirect(byte[] overlay, int bucket)
    {
        if (bucket is < 0 or >= ListedLevels) throw new ArgumentOutOfRangeException(nameof(bucket));
        byte[]? result = null;
        uint replacement = UniversalMips.EncodeI(9, 0, 5, bucket);   // addiu a1, zero, bucket
        for (int o = 0; o + 4 * EggCountsItself.Length <= overlay.Length; o += 4)
        {
            bool match = true;
            for (int k = 0; k < EggCountsItself.Length && match; k++)
            {
                uint word = BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(o + 4 * k));
                // The level-index load, or a place already chosen for it.
                match = k == 1 ? word == EggCountsItself[1] || (word & 0xFFFF0000) == 0x24050000 && (word & 0xFFFF) < ListedLevels : word == EggCountsItself[k];
            }
            if (!match || BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(o + 4)) == replacement) continue;
            result ??= [.. overlay];
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(o + 4), replacement);
        }
        return result;
    }

    /// <summary>How many places an overlay counts collected eggs in (its own egg code and any brought in with a thief).</summary>
    public static (int Own, int Redirected) EggCounters(ReadOnlySpan<byte> overlay)
    {
        int own = 0, redirected = 0;
        for (int o = 0; o + 4 * EggCountsItself.Length <= overlay.Length; o += 4)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(overlay[o..]) != EggCountsItself[0]) continue;
            bool rest = true;
            for (int k = 2; k < EggCountsItself.Length && rest; k++) rest = BinaryPrimitives.ReadUInt32LittleEndian(overlay[(o + 4 * k)..]) == EggCountsItself[k];
            if (!rest) continue;
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(overlay[(o + 4)..]);
            if (word == EggCountsItself[1]) own++;
            else if ((word & 0xFFFF0000) == 0x24050000) redirected++;
        }
        return (own, redirected);
    }

    /// <summary>Gnasty's World's overlay asking for <paramref name="total"/> eggs at the doorway to Gnasty's Loot; null
    /// when it already does, or when the overlay has no such doorway (a level built in Gnasty's World's place).</summary>
    public static byte[]? SetLootGate(byte[] overlay, int total)
    {
        int at = (int)(LootGate - UniversalOverlay.Base);
        // With dragonfly eggs in the game the doorway's test was rewritten (UniversalEggUnlocks): the same test, elsewhere.
        int rewritten = (int)(UniversalEggUnlocks.PortalAddress - UniversalOverlay.Base);
        if (rewritten >= 0 && rewritten + 72 <= overlay.Length)
        {
            for (int have = 0; have <= MaxTotal; have++)
            {
                if (!overlay.AsSpan(rewritten, 72).SequenceEqual(UniversalEggUnlocks.PortalGate(have))) continue;
                if (have == total) return null;
                byte[] result = [.. overlay];
                UniversalEggUnlocks.PortalGate(total).CopyTo(result, rewritten);
                return result;
            }
        }
        if (at < 12 || at + 4 > overlay.Length) return null;
        uint word = BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(at));
        // lui v0,0x8007; lw v0,0x5810(v0); nop; slti v0,v0,<eggs>
        if (BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(at - 12)) != 0x3C028007 || BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(at - 8)) != 0x8C425810 ||
            BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(at - 4)) != 0 || (word & 0xFFFF0000) != 0x28420000) return null;
        if ((word & 0xFFFF) == total) return null;
        byte[] patched = [.. overlay];
        BinaryPrimitives.WriteUInt32LittleEndian(patched.AsSpan(at), 0x28420000u | (uint)total);
        return patched;
    }

    /// <summary>What a finished game says about its eggs: the eggs per level of the first three worlds, the row's
    /// length, and the eggs the doorway to Gnasty's Loot asks for (-1 when that overlay has no doorway).</summary>
    public static (byte[] PerLevel, int Row, int Step, int Gate, int Divisor) Read(ReadOnlySpan<byte> exe, ReadOnlySpan<byte> gnastysWorldOverlay)
    {
        int gateAt = (int)(LootGate - UniversalOverlay.Base), gate = -1;
        if (gateAt + 4 <= gnastysWorldOverlay.Length && (BinaryPrimitives.ReadUInt32LittleEndian(gnastysWorldOverlay[gateAt..]) & 0xFFFF0000) == 0x28420000)
            gate = (int)(BinaryPrimitives.ReadUInt32LittleEndian(gnastysWorldOverlay[gateAt..]) & 0xFFFF);
        int divisor = 12000;
        uint first = BinaryPrimitives.ReadUInt32LittleEndian(exe[Off(Percentage)..]);
        if (first == 0x3C088007) divisor = (int)(BinaryPrimitives.ReadUInt32LittleEndian(exe[(Off(Percentage) + 4 * 16)..]) & 0xFFFF);
        return (exe.Slice(Off(LevelTotals), ListedLevels).ToArray(), (int)(BinaryPrimitives.ReadUInt32LittleEndian(exe[Off(RowBound)..]) & 0xFFFF),
            (int)(BinaryPrimitives.ReadUInt32LittleEndian(exe[Off(RowEggStep)..]) & 0xFFFF), gate, divisor);
    }

    /// <summary>One line for the person making the game, or "" when the game has the game's own twelve eggs.</summary>
    public static string Describe(EggTotalsApplied? applied)
    {
        if (applied == null || applied.Count.IsRetail) return "";
        int total = applied.Total;
        return $" This game has {total} dragon {(total == 1 ? "egg" : "eggs")} (one for every egg thief in it): the inventory and 100% count {total}.";
    }

    /// <summary>The dragonfly eggs the installed completion counter counts (0: none installed).</summary>
    private static int InstalledDragonflyEggs(byte[] exe, byte[] original)
    {
        int at = (int)(UniversalEggProgress.CountAddress - UniversalExeBindings.ExeBase);
        if (exe.AsSpan(at, 128).SequenceEqual(original.AsSpan(at, 128))) return 0;
        // CountRoutine: la t0; la t1; addiu t2, zero, total; …
        int total = (int)(BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at + 16)) & 0xFFFF);
        if (total < 1 || total > UniversalSpecialBehaviors.MaxDragonflyEggs) throw new InvalidDataException("The game's dragonfly-egg counter is not one this editor wrote.");
        byte[] expected = UniversalEggProgress.CountRoutine(total);
        if (!exe.AsSpan(at, expected.Length).SequenceEqual(expected)) throw new InvalidDataException("The game's dragonfly-egg counter is not one this editor wrote.");
        return total;
    }

    private static bool IsOwnPercentage(byte[] exe)
    {
        int at = Off(Percentage);
        int divisor = (int)(BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at + 4 * 16)) & 0xFFFF);
        int total = (divisor - 12000) / 100 + RetailTotal;
        if (total is < 0 or > MaxTotal || Divisor(total) != divisor) return false;
        uint[] routine = PercentageRoutine(total);
        for (int i = 0; i < routine.Length; i++)
            if (BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at + 4 * i)) != routine[i]) return false;
        return true;
    }

    private static void SetImmediate(byte[] exe, uint address, uint instruction, int value, string what)
    {
        uint word = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(address)));
        if ((word & 0xFFFF0000) != instruction) throw new InvalidDataException($"The game's code for {what} is not the USA game's, so its egg total cannot be set.");
        BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(address)), instruction | (uint)(value & 0xFFFF));
    }

    private static bool Same(byte[] exe, byte[] original, uint address, int bytes) =>
        exe.AsSpan(Off(address), bytes).SequenceEqual(original.AsSpan(Off(address), bytes));

    private static int Off(uint address) => (int)(address - UniversalExeBindings.ExeBase);
}
