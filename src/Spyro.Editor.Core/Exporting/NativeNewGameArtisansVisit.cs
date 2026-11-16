using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Load Game lists a saved game only when that game has been to Artisans: the menu shows a slot as EMPTY while
/// the save's first "visited" byte (Artisans', RAM 0x80078E78, save record +0x40) is 0, because the game always begins
/// there. When "New Game starts in" sends a new game somewhere else, a save made before Spyro reaches Artisans could not
/// be loaded. This marks Artisans visited as the game enters a level, in a game whose New Game starts elsewhere only:
/// two repeated <c>lui at,0x8007</c> of the level-entry code (USA 0x80015D64 and 0x80015D6C, where <c>at</c> already
/// holds 0x80070000) become <c>addiu a0,zero,1</c> and <c>sb a0,0x15F0(s3)</c> (<c>s3</c> = 0x80077888 there, set at
/// 0x80015CF8; <c>a0</c> is dead until the <c>move a0,s0</c> two instructions later). Nothing branches into these
/// instructions; the entry is the jump-table case at 0x80015CCC. A game that starts in Artisans keeps the game's words
/// byte for byte. Measured 2026-11-15: a slot with only that byte set is listed and loads; a slot with only another
/// homeworld visited, or only a dragon in Artisans, stays EMPTY.</summary>
public static class NativeNewGameArtisansVisit
{
    private const uint ExeBase = 0x8000F800;
    public const uint FirstAddress = 0x80015D64, SecondAddress = 0x80015D6C;
    public const uint GameFirst = 0x3C018007, GameSecond = 0x3C018007;       // lui at,0x8007 (twice)
    public const uint SetOne = 0x24040001, MarkArtisans = 0xA26415F0;        // addiu a0,zero,1 / sb a0,0x15F0(s3)
    /// <summary>Artisans' visited byte in RAM: s3 (0x80077888) + 0x15F0.</summary>
    public const uint ArtisansVisited = 0x80078E78;

    // What the two new instructions rely on, as in the USA game.
    private static readonly (uint Address, uint Word)[] Context =
    [
        (0x80015CF8, 0x3C138007), (0x80015CFC, 0x26737888),   // s3 = 0x80077888
        (0x80015D5C, 0x3C018007), (0x80015D60, 0xAC205830),   // at = 0x80070000; sw zero,0x5830(at)
        (0x80015D68, 0xAC2056C8), (0x80015D70, 0xAC22587C),   // the stores that keep using at
        (0x80015D74, 0x0C004D93), (0x80015D78, 0x02002021),   // jal 0x8001364C / move a0,s0
    ];

    /// <summary>True when New Game starting in <paramref name="levelId"/> needs Artisans marked (every level but Artisans).</summary>
    public static bool Needed(int levelId) => levelId != 10;

    /// <summary>Whether the executable (PS-X EXE with its 0x800 header) marks Artisans visited on level entry.</summary>
    public static bool IsInstalled(byte[] exe) => Word(exe, FirstAddress) == SetOne && Word(exe, SecondAddress) == MarkArtisans;

    /// <summary>Installs the mark (<paramref name="on"/>) or takes it out again (the game's two words back). Off never
    /// touches words it did not write, so a game starting in Artisans is left exactly as it was. On refuses, changing
    /// nothing, when the level-entry code is not the USA game's or another change owns these words.</summary>
    public static void Apply(byte[] exe, bool on)
    {
        ArgumentNullException.ThrowIfNull(exe);
        if (exe.Length < Off(0x80015D78) + 4)
        {
            if (!on) return;
            throw new InvalidDataException("The executable is too short for New Game's Artisans visit.");
        }
        uint first = Word(exe, FirstAddress), second = Word(exe, SecondAddress);
        bool game = first == GameFirst && second == GameSecond, marked = first == SetOne && second == MarkArtisans;
        if (!on && !marked) return;
        foreach (var (address, word) in Context)
            if (Word(exe, address) != word)
                throw new InvalidDataException($"The executable's level-entry code differs from the USA game's at 0x{address:X8}; New Game cannot mark Artisans visited.");
        if (!game && !marked)
            throw new InvalidDataException("Another change owns the level-entry code where New Game marks Artisans visited.");
        BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(FirstAddress)), on ? SetOne : GameFirst);
        BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(SecondAddress)), on ? MarkArtisans : GameSecond);
    }

    private static int Off(uint address) => checked((int)(address - ExeBase));
    private static uint Word(byte[] exe, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(address)));
}
