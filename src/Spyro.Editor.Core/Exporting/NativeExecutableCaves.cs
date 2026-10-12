using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Every range of the USA executable that an editor feature overwrites with its own code or data ("caves":
/// retail functions or data nothing in the executable, the card overlay, the credits overlay or any level overlay calls,
/// jumps or branches to, or points at). Features that share one range on purpose (only one of them can be installed:
/// each checks the range is still blank) are one entry. A new feature takes its caves from ranges not listed here and
/// adds them; <see cref="RequireDisjoint"/> is the test.</summary>
public static class NativeExecutableCaves
{
    public sealed record Cave(uint Start, uint End, string Owner)
    {
        public int Length => checked((int)(End - Start));
        public bool Overlaps(Cave other) => Start < other.End && other.Start < End;
    }

    /// <summary>The level window's caves (<see cref="NativeLevelWindow"/>): unused libgpu routines (ClearOTag, ClearOTagR,
    /// DrawOTagEnv, SetGraphQueue, debug dumps, the third image-transfer wrapper), C library helpers (toupper, tolower,
    /// longjmp, the static-constructor runner), unused card and sound library routines and two unused game helpers.</summary>
    public static IReadOnlyList<Cave> LevelWindow { get; } =
    [
        new(0x8005FBB4, 0x8005FD64, nameof(NativeLevelWindow)),
        new(0x800604D8, 0x80060670, nameof(NativeLevelWindow)),
        new(0x8005FEDC, 0x80060030, nameof(NativeLevelWindow)),
        new(0x8005F5A0, 0x8005F6C8, nameof(NativeLevelWindow)),
        new(0x8005C114, 0x8005C1C0, nameof(NativeLevelWindow)),
        new(0x80065FD0, 0x8006606C, nameof(NativeLevelWindow)),
        new(0x800381E8, 0x80038250, nameof(NativeLevelWindow)),
        new(0x8005B9F8, 0x8005BA60, nameof(NativeLevelWindow)),
        new(0x80062E60, 0x80062EC0, nameof(NativeLevelWindow)),
        new(0x800669C0, 0x80066A08, nameof(NativeLevelWindow)),
        new(0x8003DF60, 0x8003DFA4, nameof(NativeLevelWindow)),
        new(0x80063928, 0x80063964, nameof(NativeLevelWindow)),
        new(0x8005EA08, 0x8005EA94, nameof(NativeLevelWindow)),
        new(0x8005DEEC, 0x8005DF1C, nameof(NativeLevelWindow)),
        new(0x8006629C, 0x800662BC, nameof(NativeLevelWindow)),
        new(0x80069040, 0x80069060, nameof(NativeLevelWindow)),
        new(0x8005DF2C, 0x8005DF44, nameof(NativeLevelWindow)),
        new(0x8005EB88, 0x8005EBA0, nameof(NativeLevelWindow)),
        new(0x80063B58, 0x80063B70, nameof(NativeLevelWindow)),
    ];

    public static IReadOnlyList<Cave> All { get; } =
    [
        new(0x80017188, 0x800171FC, nameof(NativeDragonStatueColors)),
        new(0x80017548, 0x800175B8, nameof(NativeDragonStatueColors)),
        new(0x8001771C, 0x80017758, nameof(NativeDragonStatueColors)),
        new(0x80017854, 0x80017894, nameof(NativeDragonStatueColors)),
        new(0x8005F428, 0x8005F4A0, nameof(NativeDragonStatueColors)),
        new(UniversalSpecialBehaviors.CaveStart, UniversalSpecialBehaviors.CaveEnd, nameof(UniversalSpecialBehaviors) + " (sign hook, egg data)"),
        new(UniversalSpecialBehaviors.EggModelCave, UniversalSpecialBehaviors.EggModelCaveEnd, nameof(UniversalSpecialBehaviors) + " (egg model)"),
        new(UniversalSpecialBehaviors.EggInventoryHost, UniversalSpecialBehaviors.EggInventoryHostEnd, nameof(UniversalSpecialBehaviors) + " (egg inventory, egg progress)"),
        // Its two 152-byte slots; the rest of its area (the third wrapper, to 0x8005EA94) is the level window's.
        new(NativeClearedRetailBinding.RelocationAreaStart, NativeClearedRetailBinding.RelocationSlots[^1] + 0x98, nameof(NativeClearedRetailBinding) + " (short-initializer copies)"),
        new(0x8005D628, 0x8005D680, "8 MB mode scene check (feature/eight-mb-20261011)"),
        new(NativeDrawBufferGuard.DataCave, NativeDrawBufferGuard.DataCaveEnd, nameof(NativeDrawBufferGuard) + " (data)"),
        new(NativeDrawBufferGuard.Cave, NativeDrawBufferGuard.CaveEnd, nameof(NativeDrawBufferGuard)),
        new(NativeSubdivisionQueueGuard.Cave, NativeSubdivisionQueueGuard.Cave + 4 * 13 * 4, nameof(NativeSubdivisionQueueGuard)),
        new(UniversalCrashPlayer.SceneDrawHost, UniversalCrashPlayer.SceneDrawHost + 4 * (uint)UniversalCrashPlayer.SceneDrawHostWords, nameof(UniversalCrashPlayer) + " (scene draw)"),
        new(UniversalCrashPlayer.DrawHookHost, UniversalCrashPlayer.DrawHookHost + 4 * (uint)UniversalCrashPlayer.DrawHookHostWords, nameof(UniversalCrashPlayer) + " (draw hook)"),
        new(0x8007314C, 0x8007354C, "helper payload, one owner at a time: NativeEnvironmentGradeExporter, SpringChestInGamePatchBuilder, NativeSkyOcclusionPatcher (0x80073400)"),
        .. LevelWindow,
    ];

    /// <summary>Throws when two features' caves overlap (or a range is empty or backwards).</summary>
    public static void RequireDisjoint(IReadOnlyList<Cave>? caves = null)
    {
        caves ??= All;
        foreach (var c in caves)
            if (c.End <= c.Start || (c.Start & 3) != 0 || (c.End & 3) != 0)
                throw new InvalidDataException($"Executable cave 0x{c.Start:X8}..0x{c.End:X8} ({c.Owner}) is not a word-aligned range.");
        for (int i = 0; i < caves.Count; i++)
            for (int j = i + 1; j < caves.Count; j++)
                if (caves[i].Overlaps(caves[j]))
                    throw new InvalidDataException($"Executable caves overlap: 0x{caves[i].Start:X8}..0x{caves[i].End:X8} ({caves[i].Owner}) and " +
                        $"0x{caves[j].Start:X8}..0x{caves[j].End:X8} ({caves[j].Owner}).");
    }

    /// <summary>The caves registered to <paramref name="owner"/> (case-insensitive prefix match on the owner text).</summary>
    public static IReadOnlyList<Cave> Of(string owner) =>
        All.Where(c => c.Owner.StartsWith(owner, StringComparison.OrdinalIgnoreCase)).ToArray();
}
