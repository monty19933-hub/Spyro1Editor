using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>Chests that give a Teal gem (50) or a Rainbow gem (100).
///
/// The game keeps what an object gives in one row byte, +0x53: its reward routine (USA 0x8003ABC0) masks it with 0x7F
/// and has the level make that class. A teal or rainbow reward is the byte of the gem that stands in for it in the
/// game's own code with bit 7 set: 0xD6 (a gold gem's 0x56) and 0xD7 (a purple gem's 0x57). The reward routine, the
/// chest's code, the gem's own flight, bounce and pickup and Sparx all run on that original gem, unchanged.
///
/// Three small routines in the level's added code make it the high-value gem:
/// - after every object update, a gem that a row with such a byte has just paid out (the gem's +0x56 names the row, the
///   row's +0x3A bit 7 says it paid) is marked in the gem's own unused reward byte, and every marked gem is given the
///   high-value gem's class for drawing (its double-size model and shine colours);
/// - before the next object update the marked gems get their original class back, so the game's code never sees another;
/// - the calls to the game's gem collection routine (0x8003B9D4) go through a routine that awards a marked gem 50 or 100
///   with the game's own award routine (0x8003B854: treasure totals, the numeral, and the collected bit of the row that
///   paid it) and passes every other gem on unchanged.
/// The loader's own rule then applies when the level is entered again: the row of a collected reward is removed or left
/// with nothing to give.</summary>
public static class UniversalHighValueRewards
{
    public const int RowOffset = UniversalRewardChoice.RowOffset;
    public const byte TealReward = 0xD6, RainbowReward = 0xD7;
    /// <summary>The original gems that stand in for the high-value ones while the game's own code runs.</summary>
    public const int TealStandInClass = 0x56, RainbowStandInClass = 0x57;
    public const string TealSpringChestId = "chest:spring-teal", RainbowSpringChestId = "chest:spring-rainbow";
    public const string TealSpringChestName = "Spring chest (Teal reward)", RainbowSpringChestName = "Spring chest (Rainbow reward)";

    /// <summary>The spring chest classes (Gnorc Cove's 0x0C7 and the common 0x149): flame or charge one and its gem
    /// springs up on the lid for Spyro to grab.</summary>
    public static readonly IReadOnlySet<int> SpringChestClasses = new HashSet<int> { 0xC7, 0x149 };

    public static bool IsReward(int rewardByte) => rewardByte is TealReward or RainbowReward;
    public static int ValueFor(int rewardByte) => rewardByte switch { TealReward => 50, RainbowReward => 100, _ => 0 };
    public static int GemClassFor(int rewardByte) => rewardByte switch
    {
        TealReward => UniversalHighValueGems.TealClass, RainbowReward => UniversalHighValueGems.RainbowClass,
        _ => throw new ArgumentOutOfRangeException(nameof(rewardByte))
    };
    public static string GemIdFor(int rewardByte) => UniversalHighValueGems.IdForClass(GemClassFor(rewardByte))!;
    public static byte RewardForGemClass(int gemClass) => gemClass == UniversalHighValueGems.TealClass ? TealReward
        : gemClass == UniversalHighValueGems.RainbowClass ? RainbowReward : throw new ArgumentOutOfRangeException(nameof(gemClass));

    /// <summary>True for an object kind that may carry a teal or rainbow reward: its own code was checked to pay its
    /// reward through the game's reward routine with its own row as the payer.</summary>
    public static bool Supports(int sourceClass) => SpringChestClasses.Contains(sourceClass);

    /// <summary>The reward byte a placement's row will carry.</summary>
    public static int RewardOf(UniversalMobyPlacement placement, ReadOnlySpan<byte> sourceRow) =>
        placement.RowByteOverrides != null && placement.RowByteOverrides.TryGetValue(RowOffset, out byte chosen) ? chosen : sourceRow[RowOffset];

    // Spring chests tint their lid by what they hold: on their first update they load a colour word for row +0x4C from
    // the executable's table at 0x8006E52C + 4 x reward byte (red, green, blue, gold and purple at 0x8006E678). The
    // table has no entry for a teal or rainbow byte, so the added code sets the word itself.
    public const uint TintTable = 0x8006E52C;
    public const int TintTableLoadImmediate = 0xE52C;
    /// <summary>Tint words by shine-table entry (<see cref="UniversalHighValueGems.ShinePair"/>): teal, red, green,
    /// blue, gold, purple. The five original ones are the executable's own words.</summary>
    public static readonly uint[] Tints = [0x00A00C30, 0x00A30000, 0x00A00C00, 0x00A00030, 0x00A30C00, 0x00A30030];

    /// <summary>True when the class's own update code reads its tint from the reward-indexed table.</summary>
    public static bool TintsByReward(UniversalOverlay overlay, int classId)
    {
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
        if (!UniversalDispatchAnalysis.Handles(overlay, sw, classId)) return false;
        var trace = UniversalDispatchAnalysis.Trace(overlay, sw.TreeStart, classId, sw.Seeds, sw.ClassRegister);
        // The table load follows the reward byte's own load within the class's first block.
        for (uint pc = trace.Stop; pc < trace.Stop + 0x80 && overlay.Contains(pc + 16); pc += 4)
        {
            uint w = overlay.Word(pc);
            if (UniversalMips.Op(w) != 36 || UniversalMips.SImm(w) != RowOffset) continue;
            for (uint q = pc + 4; q <= pc + 20; q += 4)
            {
                uint load = overlay.Word(q);
                if (UniversalMips.Op(load) == 35 && UniversalMips.Imm(load) == TintTableLoadImmediate) return true;
            }
        }
        return false;
    }

    /// <summary>Every call to the game's gem collection routine in the level's own code.</summary>
    public static IReadOnlyList<uint> CollectionCalls(UniversalOverlay overlay)
    {
        List<uint> calls = [];
        uint call = UniversalMips.EncodeJ(3, UniversalGemCollectionAdapter.CollectItem);
        for (uint pc = overlay.CodeStart; pc + 4 <= overlay.CopyBuffer && pc + 4 <= overlay.End; pc += 4)
            if (overlay.Word(pc) == call) calls.Add(pc);
        return calls;
    }

    /// <summary>Sends the level's own calls to the gem collection routine through the added routine. Calls another
    /// adapter already took over are left: that adapter's own call goes through the routine instead.</summary>
    public static int Apply(byte[] overlay, IReadOnlyList<uint> calls, uint routine)
    {
        uint native = UniversalMips.EncodeJ(3, UniversalGemCollectionAdapter.CollectItem);
        int patched = 0;
        foreach (uint pc in calls)
        {
            int offset = checked((int)(pc - UniversalOverlay.Base));
            if (BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(offset)) != native) continue;
            BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(offset), UniversalMips.EncodeJ(3, routine));
            patched++;
        }
        return patched;
    }
}

/// <summary>What a level's added code needs for teal and rainbow rewards: the destination classes of its chests that
/// tint themselves by their reward.</summary>
public sealed record UniversalHighValueRewardPlan(IReadOnlyList<int> TintedClasses);
