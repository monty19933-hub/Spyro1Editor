using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>How many dragons a finished game holds: per level of the game (36 places) and in each level beyond them (a
/// new world's levels, in the order given).</summary>
public sealed record DragonCount(IReadOnlyList<int> PerLevel, IReadOnlyList<int> Beyond)
{
    public int Total => PerLevel.Sum() + Beyond.Sum();
    public bool IsRetail => Beyond.Sum() == 0 && PerLevel.SequenceEqual(NativeDragonTotals.RetailPerLevel);
}

/// <summary>What <see cref="NativeDragonTotals.ApplyForExport"/> did.</summary>
public sealed record DragonTotalsApplied(DragonCount Count, bool TableChanged, bool PercentageChanged, bool GateChanged)
{
    public int Total => Count.Total;
}

/// <summary>The game's dragon totals follow the dragons its levels really hold.
/// <para>The game (USA) knows of eighty dragons in three places, none of them read from the levels: a table of dragons
/// per level (0x8006F414, 36 bytes: the inventory writes "rescued/there" on a level's line, and a dash where the table
/// says none), the completion percentage (0x8002BB20: 50 a gem, 6,000 a dragon, 10,000 an egg, over 12,000) and the
/// doorway to Gnasty's Loot (Gnasty's World 0x80081898: at least eighty dragons). A dragon added to a level, or a level
/// rebuilt without its dragons, changes none of them: the inventory would count five dragons of four, the percentage
/// pass 100 or never reach it, and the doorway open a dragon early or never.</para>
/// <para>Rescued dragons are counted by the game when a rescue starts (0x8002C924): the level's own count
/// 0x800772D8 + 4 x level index and the whole game's 0x80075750. Both are saved with the level (a new world's levels in
/// the level window's records) and added up at load, so nothing there needs changing.</para>
/// <para>Dragons are counted from the finished levels themselves: every statue (class 0xFA) is one. A level of the game
/// has the game's own number plus the statues it gained or lost (Gnasty's World has one statue for two dragons: the
/// second stands in the same place once Gnasty is beaten). With the game's own dragons in their places nothing is
/// written. Call it right after <see cref="NativeEggTotals.ApplyForExport"/>, which leaves the percentage as the eggs
/// alone make it.</para></summary>
public static class NativeDragonTotals
{
    public const int RetailTotal = 80, Levels = 36, StatueClass = 0xFA;
    /// <summary>The most dragons a game can hold: the counter on screen has two digits.</summary>
    public const int MaxTotal = 99;
    /// <summary>What one dragon is worth in the completion percentage (the divisor grows by a hundredth of it).</summary>
    public const int PercentageWorth = 6000;
    public const uint LevelTotals = 0x8006F414, CollectedPerLevel = 0x800772D8, CollectedTotal = 0x80075750;
    /// <summary>Gnasty's World's doorway to Gnasty's Loot: slti v0,v0,80 after lui v0,0x8007; lw v0,0x5750(v0); nop.</summary>
    public const uint LootGate = 0x80081898;
    private const int GnastysWorldIndex = 30;
    // The percentage as NativeEggTotals writes it (lui t0,0x8007 first; its divisor is word 16) and as the dragonfly eggs'
    // counter does (addiu sp,sp,-24 first; word 9 the dragonfly eggs, word 11 the dragons asked for, word 40 the divisor).
    private const uint EggFormFirst = 0x3C088007, DragonflyFormFirst = 0x27BDFFE8, LoadT1 = 0x24090000;
    private const int EggFormDivisor = 16, DragonflyFormEggs = 9, DragonflyFormDragons = 11, DragonflyFormDivisor = 40;

    public static IReadOnlyList<int> RetailPerLevel { get; } =
        [4, 4, 3, 4, 1, 0, 3, 4, 3, 5, 1, 0, 3, 4, 3, 3, 1, 0, 2, 2, 4, 3, 1, 0, 3, 5, 3, 3, 2, 0, 2, 2, 2, 0, 0, 0];

    public static int DataEntry(int levelIndex) => 10 + 2 * levelIndex;
    public static int OverlayEntry(int levelIndex) => 9 + 2 * levelIndex;

    /// <summary>The dragon statues a level's data holds (rows of class 0xFA that are not put out of the way).</summary>
    public static int StatuesIn(byte[] levelData)
    {
        try
        {
            var data = new UniversalLevelData(levelData);
            int rows = data.Scene().RowCount, count = 0;
            for (int i = 0; i < rows; i++)
            {
                byte[] row = data.Row(i);
                if (BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36)) != StatueClass) continue;
                if (BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(0x0C)) <= -400000) continue;
                count++;
            }
            return count;
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException) { return 0; }
    }

    /// <summary>The dragons in every level of a game. <paramref name="entry"/> gives a WAD entry's bytes as the game will
    /// have it, <paramref name="originalEntry"/> as the game's own disc has it; <paramref name="beyond"/> the data
    /// entries of levels past the game's 36 places (a new world's).</summary>
    public static DragonCount Count(Func<int, byte[]> entry, Func<int, byte[]> originalEntry, IEnumerable<int>? beyond = null)
    {
        int[] per = [.. RetailPerLevel];
        for (int i = 0; i < Levels; i++)
        {
            byte[] now, before;
            try { now = entry(DataEntry(i)); before = originalEntry(DataEntry(i)); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException) { continue; }
            if (now.AsSpan().SequenceEqual(before)) continue;
            int statues = StatuesIn(now);
            per[i] = statues == 0 ? 0 : Math.Max(0, RetailPerLevel[i] + statues - StatuesIn(before));
        }
        return new(per, [.. (beyond ?? []).Select(e => StatuesIn(entry(e)))]);
    }

    /// <summary>What the percentage divides by: 12,000, 100 more for every egg past the game's twelve (dragonfly eggs
    /// too) and 60 more for every dragon past eighty, so that everything collected is exactly 100.</summary>
    public static int Divisor(int eggsBeyondTwelve, int dragons) => 12000 + 100 * eggsBeyondTwelve + PercentageWorth / 100 * (dragons - RetailTotal);

    /// <summary>The percentage the game shows (the routine's arithmetic) with <paramref name="total"/> dragons and
    /// <paramref name="eggTotal"/> eggs in the game.</summary>
    public static int Percent(int treasure, int dragons, int eggs, int total, int eggTotal = NativeEggTotals.RetailTotal)
    {
        int sum = 50 * treasure + PercentageWorth * dragons + 10000 * eggs, percent = sum / Divisor(eggTotal - NativeEggTotals.RetailTotal, total);
        return treasure >= 12001 ? 100 + (treasure - 12000) / 100 : percent;
    }

    /// <summary>Sets the game's dragon totals from the statues its levels hold. <paramref name="replacements"/> are the
    /// WAD entries this export has changed (Gnasty's World's overlay is added when its doorway changes),
    /// <paramref name="entryBytes"/> the rest as the image being finished has them, <paramref name="originalEntry"/> the
    /// game's own; <paramref name="guests"/> the data entries of the levels past the game's 36 places. Returns null, having
    /// written nothing, while the game's own dragons are where the game put them and nothing here was changed before.</summary>
    public static DragonTotalsApplied? ApplyForExport(byte[] exe, ReadOnlySpan<byte> originalExe, IDictionary<int, byte[]> replacements,
        Func<int, byte[]> entryBytes, Func<int, byte[]> originalEntry, IReadOnlyList<int>? guests = null)
    {
        byte[] Entry(int e) => replacements.TryGetValue(e, out var changed) ? changed : entryBytes(e);
        var count = Count(Entry, originalEntry, guests);
        int total = count.Total;
        if (total > MaxTotal)
            throw new InvalidDataException($"This game would have {total} dragons, and the game's dragon counter shows two digits: a game can hold {MaxTotal}. " +
                $"Take {total - MaxTotal} {(total - MaxTotal == 1 ? "dragon" : "dragons")} out and Create BIN again.");
        byte[] original = originalExe.ToArray();

        // The inventory: dragons on each level's line.
        bool table = false;
        for (int i = 0; i < Levels; i++)
        {
            byte want = (byte)count.PerLevel[i];
            if (exe[Off(LevelTotals) + i] == want) continue;
            exe[Off(LevelTotals) + i] = want; table = true;
        }

        // The percentage: the divisor of whichever routine the eggs left there.
        bool percentage = false;
        int at = Off(NativeEggTotals.Percentage), words = NativeEggTotals.PercentageWords;
        uint Word(int index) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at + 4 * index));
        void Immediate(int index, int value, string what)
        {
            if ((Word(index) & 0xFFFF0000) != LoadT1 || value is <= 0 or > short.MaxValue)
                throw new InvalidDataException($"The game's completion percentage is not one this editor knows ({what}), so it cannot be set for the dragons in this game.");
            if ((int)(Word(index) & 0xFFFF) == value) return;
            BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(at + 4 * index), LoadT1 | (uint)value);
            percentage = true;
        }
        int eggs = (int)(BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(NativeEggTotals.RowBound))) & 0xFFFF);
        if (eggs is < 0 or > NativeEggTotals.MaxTotal) throw new InvalidDataException("The game's egg total is not one this editor wrote, so its completion percentage cannot be set for the dragons in this game.");
        if (exe.AsSpan(at, 4 * words).SequenceEqual(original.AsSpan(at, 4 * words)))
        {
            if (total != RetailTotal)
            {
                uint[] routine = NativeEggTotals.PercentageRoutine(NativeEggTotals.RetailTotal);
                for (int i = 0; i < routine.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(at + 4 * i), routine[i]);
                Immediate(EggFormDivisor, Divisor(0, total), "its divisor");
                percentage = true;
            }
        }
        else if (Word(0) == EggFormFirst) Immediate(EggFormDivisor, Divisor(eggs - NativeEggTotals.RetailTotal, total), "its divisor");
        else if (Word(0) == DragonflyFormFirst)
        {
            int dragonfly = (int)(Word(DragonflyFormEggs) & 0xFFFF);
            if ((Word(DragonflyFormEggs) & 0xFFFF0000) != LoadT1 || dragonfly < 1 || dragonfly > UniversalSpecialBehaviors.MaxDragonflyEggs)
                throw new InvalidDataException("The game's completion percentage with dragonfly eggs is not one this editor wrote, so it cannot be set for the dragons in this game.");
            Immediate(DragonflyFormDragons, total, "the dragons it asks for");
            Immediate(DragonflyFormDivisor, Divisor(dragonfly + eggs - NativeEggTotals.RetailTotal, total), "its divisor");
        }
        else throw new InvalidDataException("The game's completion percentage is not the USA game's own, so it cannot be set for the dragons in this game.");

        // The doorway to Gnasty's Loot asks for every dragon.
        bool gated = false;
        int gateEntry = OverlayEntry(GnastysWorldIndex);
        if (SetLootGate(Entry(gateEntry), total) is { } gate) { replacements[gateEntry] = gate; gated = true; }

        return table || percentage || gated || !count.IsRetail ? new(count, table, percentage, gated) : null;
    }

    /// <summary>Where Gnasty's World's overlay asks for the dragons at the doorway to Gnasty's Loot: the offset of its
    /// "fewer than" word, or -1 (a level built in Gnasty's World's place has no such doorway). The game's own test, or
    /// the one the dragonfly eggs put in its place (<see cref="UniversalEggUnlocks.PortalGate"/>: slti v1,v1,80 after
    /// lw v1,0x5750(t0)).</summary>
    public static int LootGateWord(ReadOnlySpan<byte> overlay)
    {
        static uint W(ReadOnlySpan<byte> o, int at) => BinaryPrimitives.ReadUInt32LittleEndian(o[at..]);
        int own = (int)(LootGate - UniversalOverlay.Base);
        if (own >= 12 && own + 4 <= overlay.Length && W(overlay, own - 12) == 0x3C028007 && W(overlay, own - 8) == 0x8C425750 && W(overlay, own - 4) == 0 &&
            (W(overlay, own) & 0xFFFF0000) == 0x28420000) return own;
        int rewritten = (int)(UniversalEggUnlocks.PortalAddress - UniversalOverlay.Base);
        if (rewritten >= 0 && rewritten + 72 <= overlay.Length && W(overlay, rewritten + 4 * 5) == 0x8D035750 && (W(overlay, rewritten + 4 * 8) & 0xFFFF0000) == 0x28630000)
            return rewritten + 4 * 8;
        return -1;
    }

    /// <summary>Gnasty's World's overlay asking for <paramref name="total"/> dragons at the doorway to Gnasty's Loot;
    /// null when it already does or has no such doorway.</summary>
    public static byte[]? SetLootGate(byte[] overlay, int total)
    {
        int at = LootGateWord(overlay);
        if (at < 0) return null;
        uint word = BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(at));
        if ((word & 0xFFFF) == total) return null;
        byte[] patched = [.. overlay];
        BinaryPrimitives.WriteUInt32LittleEndian(patched.AsSpan(at), (word & 0xFFFF0000) | (uint)total);
        return patched;
    }

    /// <summary>What a finished game says about its dragons: the dragons per level, the dragons the doorway to Gnasty's
    /// Loot asks for (-1 when that overlay has no doorway), and what the percentage divides by.</summary>
    public static (byte[] PerLevel, int Gate, int Divisor) Read(ReadOnlySpan<byte> exe, ReadOnlySpan<byte> gnastysWorldOverlay)
    {
        int gateAt = LootGateWord(gnastysWorldOverlay);
        int gate = gateAt < 0 ? -1 : (int)(BinaryPrimitives.ReadUInt32LittleEndian(gnastysWorldOverlay[gateAt..]) & 0xFFFF);
        int at = Off(NativeEggTotals.Percentage), divisor = 12000;
        uint first = BinaryPrimitives.ReadUInt32LittleEndian(exe[at..]);
        if (first == EggFormFirst) divisor = (int)(BinaryPrimitives.ReadUInt32LittleEndian(exe[(at + 4 * EggFormDivisor)..]) & 0xFFFF);
        else if (first == DragonflyFormFirst) divisor = (int)(BinaryPrimitives.ReadUInt32LittleEndian(exe[(at + 4 * DragonflyFormDivisor)..]) & 0xFFFF);
        return (exe.Slice(Off(LevelTotals), Levels).ToArray(), gate, divisor);
    }

    /// <summary>What a finished game image says about its dragons, or "" when it has the game's own eighty or the image
    /// can't be read. For the line Create BIN ends with. <paramref name="worldDragons"/>: the dragons in the new world's
    /// levels (they are not in the game's table of 36 places).</summary>
    public static string DescribeGame(string imagePath)
    {
        try
        {
            var image = UniversalDiscImage.Read(imagePath);
            byte[] gate = image.EntryBytes(OverlayEntry(GnastysWorldIndex));
            int at = LootGateWord(gate);
            if (at < 0) return "";
            int total = (int)(BinaryPrimitives.ReadUInt32LittleEndian(gate.AsSpan(at)) & 0xFFFF);
            if (total == RetailTotal || total is < 0 or > MaxTotal) return "";
            return $" This game has {total} dragons (the game's own has {RetailTotal}): the inventory, 100% and the doorway to Gnasty's Loot count {total}.";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException or UnauthorizedAccessException) { return ""; }
    }

    private static int Off(uint address) => (int)(address - UniversalExeBindings.ExeBase);
}
