namespace Spyro.Editor.Core.Universal;

/// <summary>The console memory a game made by Create BIN may use: the project's "Memory" setting
/// (<see cref="Spyro.Editor.Core.Persistence.GameMemorySettings"/>).</summary>
public enum GameMemoryMode
{
    /// <summary>A real PlayStation's 2 MB: the game's own memory layout (the default).</summary>
    PlayStation2MB = 0,
    /// <summary>DuckStation with Settings › Console › "Enable 8MB RAM (Dev Console)": each level's scene loads into the
    /// extra 6 MB (<see cref="Spyro.Editor.Core.Exporting.NativeExtendedRam"/>).</summary>
    Emulator8MB = 1,
}

/// <summary>
/// The game RAM a level may use when it loads, in the export's memory mode.
/// <para>A level loads one block after another from its overlay's copy buffer: its level data (terrain, collision,
/// sky, particles, sounds), its models, and last its scene (subfile 3: the placed objects with their properties and
/// spare slots, paths, cameras). The frame's two draw buffers start at 0x80187BB0
/// (<see cref="UniversalMobyComposer.NormalBufferFloor"/>) and everything must end
/// <see cref="UniversalMobyComposer.RamSafetyMargin"/> bytes below them.</para>
/// <list type="bullet">
/// <item>PlayStation (2 MB): one budget, the whole load (<see cref="UniversalMobyComposer.RuntimeEnd"/>) ends below
/// <see cref="LowLimit"/>. Exactly the checks the editor always made.</item>
/// <item>Emulator only (8 MB): the scene loads at <see cref="ExtendedSceneBase"/> instead, so two budgets. The overlay
/// code, level data and models (<see cref="LowEnd"/>) still end below <see cref="LowLimit"/>: model headers hold 21-bit
/// pointers, so models cannot move above 2 MB. The scene must fit between <see cref="ExtendedSceneBase"/> and
/// <see cref="ExtendedSceneTop"/>.</item>
/// </list>
/// <para>Safe top: 0x80400000. The game's animation relocator (0x800133E0) keeps half-addresses in 21 bits, which
/// reach 4 MB (0x80000000..0x803FFFFF); nothing in the scene is known to go through it, but keeping the scene inside
/// those 4 MB costs nothing (retail scenes are 32..162 KB, the budget is 2 MB). 0x80400000..0x807FFFFF stays free for
/// later steps (the per-face table, the part queues, larger draw buffers). The stack and the game's resident data stay
/// in the first 2 MB, below 0x80200000.</para>
/// <para>The mode comes from the export in progress (<see cref="Use"/>, which Create BIN opens with the saved project
/// setting; the editor runs one Create BIN at a time and refuses to change the setting meanwhile) or, outside one, from
/// the open project (<see cref="SetProjectMode"/>, for the editor's object browser and capacity checks). With neither,
/// it is the PlayStation's 2 MB. Both are process-wide, so the export's tasks and awaits all see the same mode.</para>
/// </summary>
public static class GameRam
{
    /// <summary>The highest address a level's load may reach below the draw buffers (both modes).</summary>
    public const uint LowLimit = UniversalMobyComposer.NormalBufferFloor - UniversalMobyComposer.RamSafetyMargin;
    /// <summary>Where the scene loads in the 8 MB mode, and the safe top of the room it may take (see the class notes).</summary>
    public const uint ExtendedSceneBase = 0x80200000, ExtendedSceneTop = 0x80400000;
    public const int ExtendedSceneCapacity = (int)(ExtendedSceneTop - ExtendedSceneBase);

    private static int s_projectMode;
    private static Scope? s_scope;

    /// <summary>The memory mode budgets use now.</summary>
    public static GameMemoryMode Mode => Volatile.Read(ref s_scope)?.Mode ?? (GameMemoryMode)Volatile.Read(ref s_projectMode);
    public static bool Extended => Mode == GameMemoryMode.Emulator8MB;
    /// <summary>The scope Create BIN opened, if any (its report collects what the export found).</summary>
    public static Scope? Current => Volatile.Read(ref s_scope);

    /// <summary>The open project's choice, for checks made outside an export (the editor sets it on every project or
    /// level load and when the setting changes).</summary>
    public static void SetProjectMode(GameMemoryMode mode) => Volatile.Write(ref s_projectMode, (int)mode);

    /// <summary>Budgets use <paramref name="mode"/> until the scope is disposed (scopes nest).</summary>
    public static Scope Use(GameMemoryMode mode)
    {
        lock (ScopeLock)
        {
            var scope = new Scope(mode, s_scope);
            Volatile.Write(ref s_scope, scope);
            return scope;
        }
    }
    private static readonly object ScopeLock = new();

    /// <summary>An export's memory mode and what the export found about it.</summary>
    public sealed class Scope : IDisposable
    {
        private readonly Scope? _previous;
        private readonly List<string> _needExtended = [];
        private bool _disposed;
        internal Scope(GameMemoryMode mode, Scope? previous) { Mode = mode; _previous = previous; }
        public GameMemoryMode Mode { get; }
        /// <summary>True once the exported game's executable has the 8 MB scene placement installed.</summary>
        public bool Installed { get; private set; }
        /// <summary>Levels of the exported game that do not fit a 2 MB console (they need the 8 MB mode).</summary>
        public IReadOnlyList<string> LevelsNeedingExtendedRam { get { lock (_needExtended) return [.. _needExtended]; } }
        internal void Record(IEnumerable<string> levels)
        {
            lock (_needExtended)
            {
                Installed = true;
                _needExtended.Clear();
                _needExtended.AddRange(levels);
            }
        }
        public void Dispose()
        {
            lock (ScopeLock)
            {
                if (_disposed) return;
                _disposed = true;
                if (ReferenceEquals(s_scope, this)) Volatile.Write(ref s_scope, _previous);
            }
        }
    }

    /// <summary>Bytes the scene takes (it loads whole).</summary>
    public static uint SceneBytes(UniversalLevelData d) => (uint)d.Subfiles()[3].Length;

    /// <summary>The end of everything that loads before the scene (overlay copy buffer, level data, models).</summary>
    public static uint LowEnd(UniversalLevelData d, uint copyBuffer) => UniversalMobyComposer.RuntimeEnd(d, copyBuffer) - SceneBytes(d);

    /// <summary>The level fits a real PlayStation's 2 MB layout.</summary>
    public static bool FitsPlayStation(UniversalLevelData d, uint copyBuffer) => Fits(UniversalMobyComposer.RuntimeEnd(d, copyBuffer), SceneBytes(d), false);

    /// <summary>The level fits the 8 MB layout (scene in the extra memory).</summary>
    public static bool FitsExtended(UniversalLevelData d, uint copyBuffer) => Fits(UniversalMobyComposer.RuntimeEnd(d, copyBuffer), SceneBytes(d), true);

    /// <summary>The level fits game RAM in the current mode.</summary>
    public static bool Fits(UniversalLevelData d, uint copyBuffer) => Fits(UniversalMobyComposer.RuntimeEnd(d, copyBuffer), SceneBytes(d), Extended);

    /// <summary>Bytes past the budget that does not fit in the current mode (zero or less when the level fits).</summary>
    public static long Shortfall(UniversalLevelData d, uint copyBuffer) => Shortfall(UniversalMobyComposer.RuntimeEnd(d, copyBuffer), SceneBytes(d), Extended);

    /// <summary>Room left below the draw buffers for models, behavior code and level data (current mode).</summary>
    public static long ModelRoom(UniversalLevelData d, uint copyBuffer) => ModelRoom(UniversalMobyComposer.RuntimeEnd(d, copyBuffer), SceneBytes(d), Extended);

    /// <summary>Room left for the scene to grow: spare object slots and rows (current mode).</summary>
    public static long SceneRoom(UniversalLevelData d, uint copyBuffer) => SceneRoom(UniversalMobyComposer.RuntimeEnd(d, copyBuffer), SceneBytes(d), Extended);

    /// <summary>A changed level is refused when it does not fit and takes more than the level it replaces
    /// (<paramref name="before"/>): a retail level already past the margin keeps its own end. In the 2 MB mode this is
    /// exactly "end past the limit and past the original end".</summary>
    public static bool Exceeds(UniversalLevelData after, uint copyAfter, UniversalLevelData before, uint copyBefore) =>
        Exceeds(UniversalMobyComposer.RuntimeEnd(after, copyAfter), SceneBytes(after),
            UniversalMobyComposer.RuntimeEnd(before, copyBefore), SceneBytes(before), Extended);

    // The arithmetic, from a level's whole-load end (RuntimeEnd) and its scene's bytes.
    internal static bool Fits(uint runtimeEnd, uint sceneBytes, bool extended) => extended
        ? runtimeEnd - sceneBytes <= LowLimit && sceneBytes <= ExtendedSceneCapacity
        : runtimeEnd <= LowLimit;
    internal static long Shortfall(uint runtimeEnd, uint sceneBytes, bool extended) => extended
        ? Math.Max((long)(runtimeEnd - sceneBytes) - LowLimit, (long)sceneBytes - ExtendedSceneCapacity)
        : (long)runtimeEnd - LowLimit;
    internal static long ModelRoom(uint runtimeEnd, uint sceneBytes, bool extended) => (long)LowLimit - (extended ? runtimeEnd - sceneBytes : runtimeEnd);
    internal static long SceneRoom(uint runtimeEnd, uint sceneBytes, bool extended) => extended
        ? ExtendedSceneCapacity - (long)sceneBytes
        : (long)LowLimit - runtimeEnd;
    internal static bool Exceeds(uint runtimeEnd, uint sceneBytes, uint originalEnd, uint originalSceneBytes, bool extended) => extended
        ? !Fits(runtimeEnd, sceneBytes, true) && !(runtimeEnd - sceneBytes <= Math.Max(LowLimit, originalEnd - originalSceneBytes) &&
            sceneBytes <= Math.Max((uint)ExtendedSceneCapacity, originalSceneBytes))
        : runtimeEnd > LowLimit && runtimeEnd > originalEnd;

    /// <summary>The composer's budget record in the current mode: in the 2 MB mode the whole load against the draw
    /// buffers, as always; in the 8 MB mode what loads below the draw buffers (the scene has its own room, reported in
    /// <see cref="UniversalRamBudget.SceneHeadroom"/>).</summary>
    public static UniversalRamBudget Budget(UniversalLevelData before, uint copyBefore, UniversalLevelData after, uint copyAfter,
        int overlayGrowth, int modelGrowth, int sceneGrowth)
    {
        bool extended = Extended;
        uint start = extended ? LowEnd(before, copyBefore) : UniversalMobyComposer.RuntimeEnd(before, copyBefore);
        uint end = extended ? LowEnd(after, copyAfter) : UniversalMobyComposer.RuntimeEnd(after, copyAfter);
        const uint floor = UniversalMobyComposer.NormalBufferFloor;
        return new UniversalRamBudget(start, end, floor, (int)(floor - start), (int)((long)floor - end), overlayGrowth, modelGrowth,
            sceneGrowth, checked((int)((long)end - start)) - overlayGrowth - modelGrowth - (extended ? 0 : sceneGrowth))
        { ExtendedRam = extended, SceneHeadroom = extended ? (int)SceneRoom(after, copyAfter) : 0 };
    }

    /// <summary>Features whose game code checks the scene address (0x800785E4) against the 2 MB layout refuse the
    /// 8 MB mode: there the scene loads at 0x80200000 and their check would switch them off in the game.</summary>
    public static void RequirePlayStationSceneAddress(string feature)
    {
        if (Extended)
            throw new InvalidDataException($"{feature} only works with the PlayStation (2 MB) memory setting: its game code " +
                "checks where the level's objects are in memory, and with Emulator only (8 MB RAM) they move. " +
                "Choose PlayStation (2 MB) under Game Settings › Memory to include it.");
    }
}
