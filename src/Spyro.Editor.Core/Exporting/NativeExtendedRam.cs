using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// "Emulator only (8 MB RAM)": the game puts each level's scene (subfile 3: its placed objects with their properties
/// and spare slots, paths and cameras) at 0x80200000 when the console has DuckStation's extra 6 MB, so a level can
/// hold more below the draw buffers (<see cref="GameRam"/> has the budgets).
/// <para>The scene address (0x800785E4) is the models' address plus their size, computed in two places: the in-game
/// level loader (0x80015B50 <c>addu v0,v0,a0</c>, stored at 0x80015B5C) and the start-up loader's stage 8 (0x800149B4
/// <c>addu v0,v0,v1</c>, stored by <c>sw v0,(a1)</c> at 0x800149B8). Both call an 11-word check instead: it makes the
/// same sum, then compares the word at 0x80200080 with the kernel word at 0x80000080. On a 2 MB console the first is
/// a mirror of the second (the same word), and the scene stays right after the models, as in the original game. With
/// real memory there they differ (that address holds nothing, or the scene's own header word, never the kernel's
/// exception code) and the scene goes to 0x80200000. The game finds the objects (0x80075828) and the spare slots
/// (0x8007573C, 0x80075930, 0x800756E8) relative to the scene, so they move with it. Models stay where they are: model
/// headers keep 21-bit pointers (0x8004B8AC and eight more readers).</para>
/// <para>The check sits in an unused library routine (0x8005D628, 0x90 bytes): no call, jump or pointer to it in the
/// executable or WAD.WAD, and in the emulator only the two hooks reached it. Registers: the size register (a0, v1) is
/// dead after the replaced sum at both sites and <c>at</c> is reloaded right after; both loaders save <c>ra</c> on
/// their stacks. The in-game hook's delay slot (<c>addiu v1,v1,1</c>) is left as it was; the start-up hook's delay slot
/// becomes a nop and the check stores the scene address itself.</para>
/// <para>Emulator-verified with the prototype (outputs/Research-8MB-RAM-20261011): New Game, 21 drops and exits to two
/// other levels with 8 MB on (scene 0x80200000) and off (scene after the models).</para>
/// </summary>
public static class NativeExtendedRam
{
    /// <summary>The unused library routine that holds the check, and its end.</summary>
    public const uint Cave = 0x8005D628, CaveEnd = 0x8005D6B8;
    private const string CaveSha256 = "a5ee5da0f80f5cf64408b981436a6907dd58464eea7346a124ad29914483de5f";
    /// <summary>The in-game loader's sum and the start-up loader's sum and store.</summary>
    public const uint InGameHook = 0x80015B50, StartupHook = 0x800149B4, StartupStore = 0x800149B8;
    /// <summary>The kernel word and the word 2 MB above it that the check compares.</summary>
    public const uint KernelProbe = 0x80000080, ExtendedProbe = 0x80200080;
    public const uint SceneAddressGlobal = 0x800785E4;
    internal const int CheckWords = 11;
    internal const uint InGameCheck = Cave, StartupCheck = Cave + 4 * CheckWords;
    /// <summary>Retail words at and around the hooks (the replaced words, their neighbours and the stores of the sum).</summary>
    private static readonly (uint At, uint Word)[] Preimage =
    [
        (InGameHook - 4, 0x8C635864), (InGameHook, 0x00441021), (InGameHook + 4, 0x24630001), (InGameHook + 8, 0x3C018008), (InGameHook + 12, 0xAC2285E4),
        (StartupHook - 4, 0x8C846B90), (StartupHook, 0x00431021), (StartupStore, 0xACA20000), (StartupStore + 4, 0x3C028007),
    ];

    private const int At = 1, V0 = 2, V1 = 3, A0 = 4, A1 = 5, Ra = 31;
    private static uint Lui(int rt, int imm) => UniversalMips.EncodeI(15, 0, rt, imm);
    private static uint Lw(int rt, int offset, int rs) => UniversalMips.EncodeI(35, rs, rt, offset);
    private static uint Sw(int rt, int offset, int rs) => UniversalMips.EncodeI(43, rs, rt, offset);
    private static uint Beq(int rs, int rt, int skip) => UniversalMips.EncodeI(4, rs, rt, skip);
    private static uint Addu(int rd, int rs, int rt) => (uint)(rs << 21 | rt << 16 | rd << 11 | 0x21);
    private static uint Jal(uint target) => UniversalMips.EncodeJ(3, target);
    private const uint JrRa = Ra << 21 | 8;

    /// <summary>One site's check: the replaced sum, then the scene at 0x80200000 when the memory there is real.
    /// <paramref name="size"/> holds the models' size (dead afterwards); <paramref name="store"/>: also store the result
    /// through a1 (the start-up site).</summary>
    internal static uint[] Check(int size, bool store) =>
    [
        Addu(V0, V0, size),                  // 0  scene = models + their size (the replaced instruction)
        Lui(size, 0x8000), Lw(size, 0x80, size), // 1  the kernel word at 0x80000080
        Lui(At, 0x8020), Lw(At, 0x80, At),   // 3  the word 2 MB higher
        0,                                   // 5  (load delay)
        Beq(size, At, 2), 0,                 // 6  the same word: a mirror (2 MB), keep the scene after the models
        Lui(V0, 0x8020),                     // 8  real memory: the scene at 0x80200000
        JrRa, store ? Sw(V0, 0, A1) : 0,     // 9  back (the start-up site stores the scene address here)
    ];

    /// <summary>The routine's bytes once installed: both checks, then zeros to its end.</summary>
    internal static byte[] CaveImage()
    {
        uint[] words = [.. Check(A0, false), .. Check(V1, true)];
        byte[] image = new byte[CaveEnd - Cave];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(4 * i), words[i]);
        return image;
    }

    /// <summary>Installs the scene placement, refusing an executable that differs from the USA game at the hooks or in
    /// the routine; installing it again is a no-op.</summary>
    public static void PatchExe(byte[] exe)
    {
        byte[] cave = CaveImage();
        if (IsInstalled(exe))
        {
            if (!exe.AsSpan(Off(Cave), cave.Length).SequenceEqual(cave) || Word(exe, StartupHook) != Jal(StartupCheck) || Word(exe, StartupStore) != 0)
                throw new InvalidDataException("The executable's 8 MB scene placement differs from this editor's.");
            return;
        }
        foreach (var (at, word) in Preimage)
            if (Word(exe, at) != word)
                throw new InvalidDataException("The executable differs from the USA game where the 8 MB memory setting hooks into the level loaders.");
        if (Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan(Off(Cave), cave.Length))) != CaveSha256)
            throw new InvalidDataException("The executable differs from the USA game where the 8 MB memory setting stores its check.");
        cave.CopyTo(exe, Off(Cave));
        Put(exe, InGameHook, Jal(InGameCheck));
        Put(exe, StartupHook, Jal(StartupCheck));
        Put(exe, StartupStore, 0);
    }

    public static bool IsInstalled(byte[] exe) => Word(exe, InGameHook) == Jal(InGameCheck);

    /// <summary>One level of an exported game: whether it fits a 2 MB console and the 8 MB layout.</summary>
    public sealed record LevelFit(int LevelId, string Name, bool FitsPlayStation, bool FitsExtended, uint RuntimeEnd, uint LowEnd, int SceneBytes);

    /// <summary>Every level of a finished game (<paramref name="entry"/> reads its WAD entries) against both layouts.
    /// A level that is past the 2 MB limit but takes no more than it did in <paramref name="original"/> (Twilight Harbor
    /// already ends 324 bytes inside the editor's margin) counts as fitting, as the composer's checks treat it.</summary>
    public static IReadOnlyList<LevelFit> Survey(byte[] exe, Func<int, byte[]> entry, IEnumerable<LevelDefinition> levels,
        UniversalDiscImage? original = null)
    {
        List<LevelFit> fits = [];
        foreach (var level in levels.Where(l => l.SourceWadEntry > 1).DistinctBy(l => l.LevelId).OrderBy(l => l.LevelId))
        {
            if (!UniversalExeBindings.Read(exe, level.LevelId).TryGetValue(UniversalExeBindings.CopyBufferGlobal, out var copy)) continue;
            var data = new UniversalLevelData(entry(level.SourceWadEntry));
            uint end = UniversalMobyComposer.RuntimeEnd(data, copy.Value);
            bool fits2 = end <= GameRam.LowLimit;
            if (!fits2 && original != null && level.LevelId != 65 &&
                UniversalExeBindings.Read(original.Exe, level.LevelId).TryGetValue(UniversalExeBindings.CopyBufferGlobal, out var originalCopy))
                fits2 = end <= UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(original.EntryBytes(level.SourceWadEntry)), originalCopy.Value);
            fits.Add(new(level.LevelId, level.DisplayName, fits2, GameRam.FitsExtended(data, copy.Value), end, GameRam.LowEnd(data, copy.Value),
                (int)GameRam.SceneBytes(data)));
        }
        return fits;
    }

    /// <summary>Create BIN's last executable step. In the 8 MB mode: installs the scene placement into
    /// <paramref name="exe"/>, checks every level of the game against the 8 MB layout (refusing one that does not fit)
    /// and records in the export's scope which levels need the 8 MB mode. In the 2 MB mode it changes nothing.</summary>
    public static IReadOnlyList<LevelFit> ApplyForExport(byte[] exe, Func<int, byte[]> entry, IEnumerable<LevelDefinition> levels,
        UniversalDiscImage? original = null)
    {
        if (!GameRam.Extended) return [];
        PatchExe(exe);
        var fits = Survey(exe, entry, levels, original);
        var tooBig = fits.Where(f => !f.FitsExtended).ToArray();
        if (tooBig.Length > 0)
            throw new InvalidDataException($"{string.Join(", ", tooBig.Select(f => f.Name))} " + (tooBig.Length == 1 ? "does" : "do") +
                " not fit game RAM even with 8 MB: models and behavior must end below the draw buffers (0x80187BB0) and the " +
                $"objects and layout within {GameRam.ExtendedSceneCapacity / 1024} KB. Remove some of the added objects.");
        GameRam.Current?.Record(fits.Where(f => !f.FitsPlayStation).Select(f => f.Name));
        return fits;
    }

    /// <summary>The sentence Create BIN adds about memory (empty in the 2 MB mode).</summary>
    public static string ExportNote(GameRam.Scope? scope)
    {
        if (scope is not { Mode: GameMemoryMode.Emulator8MB, Installed: true }) return "";
        var levels = scope.LevelsNeedingExtendedRam;
        if (levels.Count == 0)
            return " Memory: Emulator only (8 MB RAM) is on, and every level still fits 2 MB, so this game also works on a normal PlayStation setup.";
        string names = levels.Count == 1 ? levels[0] : string.Join(", ", levels.Take(levels.Count - 1)) + " and " + levels[^1];
        return $" Memory: this game needs 8 MB RAM. {names} " + (levels.Count == 1 ? "does" : "do") + " not fit 2 MB: play it in DuckStation with " +
            "Settings › Console › Enable 8MB RAM turned on. Without it, levels that fit 2 MB still work.";
    }

    private static int Off(uint address) => (int)(address - UniversalExeBindings.ExeBase);
    private static uint Word(byte[] exe, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(address)));
    private static void Put(byte[] exe, uint address, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(address)), value);
}
