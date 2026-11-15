using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>A dragon's rescue must not end in a stopped game because something had hold of the camera when it began.
/// <para>An object asks the camera to look at it through Spyro's command word (0x80078C4C, bit 0x200): what to look at
/// goes to 0x80078C74 and how to 0x80078C78, and Spyro's own code (0x80040204) hands both to the camera (mode
/// 0x8000000A, target 0x80076EA0). Stone Hill's ram does so when it has butted Spyro: one frame names the target and
/// starts a timer of fifty (0x80081D24), and every frame after that only sets the bit again while the timer runs
/// (0x80081800). A scan of the levels' code finds the same shape in nine other levels.</para>
/// <para>A rescue stands the level's objects still, but not at once: a ram that was charging Spyro as he touched the
/// statue still reaches him and starts its sequence. As the rescue ends the game clears everything it keeps about
/// Spyro (0x8004AC24 from 0x8002CBD0: 0x2A4 bytes from 0x80078A58, the target with them). In the first frame after it
/// the ram, its timer still running, sets the bit once more, the camera is told to look at nothing, and its next step
/// reads through address 0: the game stops. Seen and traced in the emulator, 2026-11-14 (a level with four dragons and
/// Stone Hill's rams; the ram's write at its 0x80081800 with 0x80078C74 empty, then the stop one frame after the
/// rescue). The game's own levels have this too; they keep such objects away from their dragons, and a level made in
/// the editor need not.</para>
/// <para>The guard: with nothing to look at, the command is passed over (the camera stays as the rescue left it, behind
/// Spyro). The same ten words of the game, two of them saved by keeping one register loaded, the test in their place.
/// Written only over the game's own words, and only into a game with a dragon in a level the editor changed or added
/// (<see cref="NativeDragonTotals.ApplyForExport"/>).</para></summary>
public static class NativeLookAtGuard
{
    /// <summary>The ten words from here on: the camera is given its mode, target and angles, then on to the rest.</summary>
    public const uint Site = 0x80040220;
    /// <summary>Where both ways go on (the rest of Spyro's commands).</summary>
    public const uint Onward = 0x800405D8;

    /// <summary>The game's own: ori v0,v0,10; lui at; sw zero,0x6EA4(at); sw v0,(a2); lui at; sw v1,0x6EA0(at); lui at;
    /// sw a0,0x6EA8(at); j onward; nop. (v1: what to look at, a0: how, a2: 0x80076E90, v0: 0x80000000.)</summary>
    public static IReadOnlyList<uint> Own { get; } =
        [0x3442000A, 0x3C018007, 0xAC206EA4, 0xACC20000, 0x3C018007, 0xAC236EA0, 0x3C018007, 0xAC246EA8, 0x08010176, 0x00000000];

    /// <summary>The same behind a test: beqz v1,onward (nothing to look at); ori v0,v0,10; lui at; sw zero,0x6EA4(at);
    /// sw v0,(a2); sw v1,0x6EA0(at); j onward; sw a0,0x6EA8(at); nop; nop.</summary>
    public static IReadOnlyList<uint> Guarded { get; } =
        [0x106000ED, 0x3442000A, 0x3C018007, 0xAC206EA4, 0xACC20000, 0xAC236EA0, 0x08010176, 0xAC246EA8, 0x00000000, 0x00000000];

    private static int Off => (int)(Site - UniversalExeBindings.ExeBase);

    private static bool Has(ReadOnlySpan<byte> exe, IReadOnlyList<uint> words)
    {
        if (exe.Length < Off + 4 * words.Count) return false;
        for (int i = 0; i < words.Count; i++)
            if (BinaryPrimitives.ReadUInt32LittleEndian(exe[(Off + 4 * i)..]) != words[i]) return false;
        return true;
    }

    /// <summary>Whether the executable has the guard.</summary>
    public static bool IsGuarded(ReadOnlySpan<byte> exe) => Has(exe, Guarded);

    /// <summary>Whether the executable has the game's own words there.</summary>
    public static bool IsOwn(ReadOnlySpan<byte> exe) => Has(exe, Own);

    /// <summary>Writes the guard over the game's own words; true when it wrote (false: it is there already, or the words
    /// there are not the game's own, and then they are left alone).</summary>
    public static bool Apply(byte[] exe) => Write(exe, Own, Guarded);

    /// <summary>Puts the game's own words back over the guard; true when it wrote.</summary>
    public static bool Restore(byte[] exe) => Write(exe, Guarded, Own);

    private static bool Write(byte[] exe, IReadOnlyList<uint> from, IReadOnlyList<uint> to)
    {
        if (!Has(exe, from)) return false;
        for (int i = 0; i < to.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off + 4 * i), to[i]);
        return true;
    }
}
