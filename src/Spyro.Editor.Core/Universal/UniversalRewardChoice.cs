namespace Spyro.Editor.Core.Universal;

/// <summary>What a copied creature gives when it is beaten: row byte +0x53, the gem the game's own reward routine makes
/// (USA 0x8003ABC0 reads the byte, marks the creature paid at +0x3A and has the level make that gem; every level has all
/// five). The creature families whose copies must otherwise keep every row byte they came with (boars, Haunted Towers'
/// actors, Dark Passage's dogs, High Caves' creatures) may give another gem than their own: nothing of theirs reads the
/// byte. A creature that came with no gem (0xFF: the routine then makes a butterfly or nothing) keeps that.</summary>
public static class UniversalRewardChoice
{
    public const int RowOffset = 0x53;
    public static bool IsGem(int value) => value is >= 0x53 and <= 0x57;

    /// <summary>True when <paramref name="value"/> at row offset <paramref name="offset"/> is another gem in place of the
    /// gem the creature came with.</summary>
    public static bool Allows(ReadOnlySpan<byte> original, int offset, byte value) =>
        offset == RowOffset && original.Length > RowOffset && IsGem(original[RowOffset]) && IsGem(value);
}
