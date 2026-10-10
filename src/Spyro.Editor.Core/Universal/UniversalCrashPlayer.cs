using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Crash;

namespace Spyro.Editor.Core.Universal;

/// <summary>Crash Bandicoot as the player: Crash 3's own model and animations (Toad Village's WillC object), drawn in
/// Spyro's place while a controller (<see cref="UniversalSpecialBehaviors.CrashPlayerUpdate"/>) gives the buttons
/// Crash's moves. Spyro keeps moving the camera and colliding with the level; he is only hidden.
/// Crash has 305 vertices and a Spyro object allows 255, so he is two objects that move and animate together: the
/// body above the hips and the legs below.</summary>
public static class UniversalCrashPlayer
{
    public const string SourceKey = UniversalCrashBoxes.SourceKey;
    public const string Id = "crash-player", LegsId = "crash-player-legs";
    public const string Name = "Crash (playable)", LegsName = "Crash (playable, legs)";
    // Free like the Crash enemies' classes (no retail level registers, places or dispatches them).
    public const int BodyClass = 0x19A, LegsClass = 0x19B;
    public const string ModelName = "Cr10G";
    /// <summary>Size against Crash's own proportions: at 0.7 he stands about a third taller than a Crash crate.</summary>
    public const double Size = 0.7;
    /// <summary>Where the two objects divide: this fraction of the standing height (just below the hips).</summary>
    public const double SplitHeight = 0.4;

    // Animation slots (the controller picks these by number).
    public const int Idle = 0, Run = 1, JumpUp = 2, Flip = 3, Spin = 4, Slide = 5, Crouch = 6, SlamDive = 7, GetUp = 8,
        Death = 9, Hurt = 10;
    public static readonly CrashSpyroModel.Animation[] Animations =
    [
        // Slow movements keep every other stored frame (half the memory; the game blends between them). The run,
        // the spin and the somersault keep every frame: blending across a fast turn shrinks the limbs.
        new("Cr10V", true, Step: 2),   // idle
        new("CrR0V", true),            // run
        new("CJU1V", true, Step: 2),   // jumping, arms up
        new("CJF1V", false),           // the somersault (double jump)
        new("CWD0V", true),            // spin attack (and the tornado spin)
        new("CrS1V", true),            // into a slide
        new("CrD1V", true),            // crouch
        new("CfS1V", true),            // belly flop: diving flat
        new("CfS6V", true, Step: 2),   // getting up from it
        new("Cf1bV", true, Step: 2),   // flattened
        new("Cm10V", true, Step: 2),   // knocked back
    ];

    /// <summary>Crash's own sounds, in the body's package slots (UniversalSpecialBehaviors.CrashSound*): spin, bounce,
    /// slide, landing and jump.</summary>
    public static readonly string[] Sounds = ["spn0A", "bnc0A", "Sld0A", "ld00A", "jmp0A"];

    // Spyro is drawn by 0x80023AC4 in play (the renderer skips the call while 0x80075814 is set) and in the dragon
    // rescue, death and other scenes (which call it whatever the flag). The hook makes the routine itself return while
    // the flag is set; level loads clear the flag, so levels without Crash keep Spyro everywhere. The hook lives in
    // libgpu's ClearImage (0x8005F98C, 39 words), which nothing in the executable or the WAD calls or points to.
    // In a dragon rescue (game state 8) the hidden Spyro's place is Crash's: the scene renderer (0x8001CFDC) draws only
    // its own actors, and the level update that moves Crash stops after the scene's first phase, so the hook draws his
    // two objects itself, standing where the scene puts Spyro and facing his way. It runs the game's own object pass on a
    // list of just his body and legs, as every renderer does: the list at 0x8006FCF4 (objects whose +0x4B is 1..0x7F,
    // Crash's kind; 0x800521C0 sorts them there in play), 0x8001F158, the 0x900-byte clear, 0x8001F798. The pass restarts
    // the close-object records whose end is kept at 0x80075F00; the hook puts that end back. It draws nothing:
    //  - outside a rescue: other scenes may be loading the next level over the model area;
    //  - when either model lies below the statue's (class 0xFA): the rescue streams into [model base, statue model)
    //    (0x80014B70), and the composer keeps Crash's models above it where the level has room;
    //  - while the rescue loader (0x80014B70, state 0x8007705C) reloads the model subfile after the scene (states 4 and
    //    5: the disc read lands unrelocated, state 5 relocates; every model, above the statue too, is invalid meanwhile);
    //  - in the scene's last phase (0x80077058 = 7) from its 16th frame (0x8007706C): it draws the level's objects
    //    again, Crash among them (the hook requires the scene renderer's retail bytes, RescueRendererSha256).
    // That part lives in unused library code at 0x80068AFC (46 words after the subdivision queue guard's stubs, the tail
    // of an unreferenced library start-up function and two more after it), which nothing references either.
    public const uint SpyroDraw = 0x80023AC4, SpyroDrawResume = 0x80023ACC, DrawHookHost = 0x8005F98C;
    public const int DrawHookHostWords = 39;
    public const uint SceneDrawHost = 0x80068AFC;
    public const int SceneDrawHostWords = 46;
    public const uint ObjectList = 0x8006FCF4, ObjectPass = 0x8001F158, ObjectListClear = 0x80016914, ObjectDraw = 0x8001F798,
        CloseRecordsEnd = 0x80075F00;
    public const int ObjectListBytes = 0x900;
    public const uint RescueRenderer = 0x8001CFDC, RescueRendererEnd = 0x8001D718, RescuePhase = 0x80077058, RescueTimer = 0x8007706C,
        RescueLoader = 0x8007705C;
    public const int RescueLastPhase = 7, RescueLevelObjectsFrame = 0x10, RescueReloadFirst = 4, RescueReloadStates = 2;
    private const uint HideFlag = 0x80075814, GameState = 0x800757D8, MobyArray = 0x80075828, SpyroPosition = 0x80078A58,
        ModelTable = 0x80076378;
    private const int RescueGameState = 8, StatueClass = 0xFA;
    private const string DrawHookHostSha256 = "32024b6f678459d35b6bb04ac5f66ac4de3de53833ace02b0a503b86085d8217";
    /// <summary>ClearImage's words 10..38: untouched by the first hook (which only hid Spyro).</summary>
    private const string DrawHookHostTailSha256 = "ecc2a6769d4558cf6868609b7705ec72f2f1369477bcc75c91c97fb9a5809789";
    private const string SceneDrawHostSha256 = "12cf2c95cebe3e808c65123b0b859069167f2b6cd1dbc7d3dd4ed50155409c47";
    /// <summary>The USA rescue scene renderer (0x8001CFDC..0x8001D718), whose phases the hook reads.</summary>
    private const string RescueRendererSha256 = "f8d72068f5a3c45d4a5e1a76016a8d99010c4d260df626d5abb5cb8b3876793e";
    private static readonly uint[] SpyroDrawPreimage = [0x3C018007, 0x24217DD8];   // lui at, 0x8007; addiu at, at, 0x7DD8

    private const int At = 1, V1 = 3, A0 = 4, A1 = 5, A2 = 6, T0 = 8, T1 = 9, T2 = 10, T3 = 11, T4 = 12, T5 = 13, T6 = 14, T7 = 15,
        Sp = 29, Ra = 31;
    private static uint I(int op, int rs, int rt, int imm) => UniversalMips.EncodeI(op, rs, rt, imm);
    private static uint R(int rs, int rt, int rd, int fn) => ((uint)rs << 21) | ((uint)rt << 16) | ((uint)rd << 11) | (uint)fn;
    private static int Lo(uint address) => (short)(address & 0xFFFF);
    private static int Hi(uint address) => UniversalMips.Hi(address);

    /// <summary>The first release's hook (hide Spyro, draw nothing); an executable carrying it is upgraded.</summary>
    internal static uint[] FirstDrawHook() =>
    [
        0x3C018007, 0x8C215814, 0x00000000, 0x14200004, 0x00000000,
        SpyroDrawPreimage[0], UniversalMips.EncodeJ(2, SpyroDrawResume), SpyroDrawPreimage[1], UniversalMips.JrRa, 0x00000000,
    ];

    /// <summary>The hook at <see cref="DrawHookHost"/>: Spyro's drawing while the flag is clear; otherwise, in a rescue whose
    /// stream leaves Crash's models alone and whose scene is not drawing the level's objects, on to
    /// <see cref="SceneDrawWords"/> with t0 the object table and v1 Spyro.</summary>
    internal static uint[] DrawHookWords()
    {
        uint[] w =
        [
            I(15, 0, At, Hi(HideFlag)),                 // 0  lui at
            I(35, At, At, Lo(HideFlag)),                // 1  lw at, hide flag
            I(15, 0, T0, 0x8007),                       // 2  lui t0, 0x8007 (every global below)
            I(5, At, 0, 8 - 4),                         // 3  bne at, zero, 8
            I(35, T0, T1, Lo(GameState)),               // 4  lw t1, game state
            SpyroDrawPreimage[0],                       // 5  the two instructions the jump replaced
            UniversalMips.EncodeJ(2, SpyroDrawResume),  // 6
            SpyroDrawPreimage[1],                       // 7
            I(35, T0, T2, Lo(ModelTable + 4 * StatueClass)),   // 8  lw t2, statue model (t1's load delay)
            I(9, T1, T1, -RescueGameState),             // 9  hidden: only in a dragon rescue
            I(5, T1, 0, 32 - 11),                       // 10 bne t1, zero, 32 (return)
            I(35, T0, T3, Lo(ModelTable + 4 * BodyClass)),     // 11 lw t3, body model
            I(35, T0, T4, Lo(ModelTable + 4 * LegsClass)),     // 12 lw t4, legs model
            R(T3, T2, T3, 0x2B),                        // 13 sltu t3, t3, t2
            R(T4, T2, T4, 0x2B),                        // 14 sltu t4, t4, t2
            R(T3, T4, T3, 0x25),                        // 15 or t3, t3, t4
            I(5, T3, 0, 32 - 17),                       // 16 bne t3, zero, 32: a model in the rescue's stream area
            I(35, T0, T7, Lo(RescueLoader)),            // 17 lw t7, loader state
            I(35, T0, T5, Lo(RescuePhase)),             // 18 lw t5, phase
            I(9, T7, T7, -RescueReloadFirst),           // 19
            I(11, T7, T7, RescueReloadStates),          // 20 sltiu t7, t7, 2
            I(5, T7, 0, 32 - 22),                       // 21 bne t7, zero, 32: the model area is being reloaded
            I(35, T0, T6, Lo(RescueTimer)),             // 22 lw t6, frames in the phase
            I(9, T5, T5, -RescueLastPhase),             // 23
            I(5, T5, 0, 28 - 25),                       // 24 bne t5, zero, 28
            I(10, T6, T6, RescueLevelObjectsFrame),     // 25 slti t6, t6, 16
            I(4, T6, 0, 32 - 27),                       // 26 beq t6, zero, 32: the last phase draws the level's objects
            0,                                          // 27
            I(35, T0, T0, Lo(MobyArray)),               // 28 lw t0, object table
            I(15, 0, V1, Hi(SpyroPosition)),            // 29 lui v1
            UniversalMips.EncodeJ(2, SceneDrawHost),    // 30 j scene draw
            I(9, V1, V1, Lo(SpyroPosition)),            // 31 addiu v1: Spyro
            UniversalMips.JrRa,                         // 32 return without drawing
            0,                                          // 33
        ];
        if (w.Length > DrawHookHostWords) throw new InvalidOperationException("The Crash draw hook outgrew its host.");
        return w;
    }

    /// <summary>The scene draw at <see cref="SceneDrawHost"/>: Crash's two objects take Spyro's place (position, feet
    /// <see cref="UniversalSpecialBehaviors.CrashPlayerFeet"/> below him, facing) and are the only entries of the object
    /// pass that draws them; the close-record end is put back afterwards.</summary>
    internal static uint[] SceneDrawWords()
    {
        uint[] w =
        [
            I(9, Sp, Sp, -24),                          // 0
            I(43, Sp, Ra, 16),                          // 1  sw ra
            I(15, 0, T7, Hi(CloseRecordsEnd)),          // 2
            I(35, T7, T7, Lo(CloseRecordsEnd)),         // 3  lw t7, close-record end
            I(15, 0, A1, Hi(ObjectList)),               // 4
            I(43, Sp, T7, 20),                          // 5  sw t7
            I(9, A1, A1, Lo(ObjectList)),               // 6  a1: list write pointer
            I(36, T0, T1, 0x48),                        // 7  loop: lbu t1, state
            I(37, T0, T2, 0x36),                        // 8  lhu t2, class
            I(9, T1, T1, -0xFF),                        // 9
            I(4, T1, 0, 27 - 11),                       // 10 beq t1, zero, 27: end of the table
            I(9, T2, T2, -BodyClass),                   // 11
            I(11, T2, T2, 2),                           // 12 sltiu t2, t2, 2
            I(4, T2, 0, 25 - 14),                       // 13 beq t2, zero, 25
            I(35, V1, T3, 0),                           // 14 lw t3, Spyro x
            I(35, V1, T4, 4),                           // 15 lw t4, y
            I(35, V1, T5, 8),                           // 16 lw t5, z
            I(36, V1, T6, 0xE),                         // 17 lbu t6, facing (0x80078A66)
            I(9, T5, T5, -UniversalSpecialBehaviors.CrashPlayerFeet), // 18
            I(43, T0, T3, 0xC),                         // 19
            I(43, T0, T4, 0x10),                        // 20
            I(43, T0, T5, 0x14),                        // 21
            I(40, T0, T6, 0x46),                        // 22 sb t6, yaw
            I(43, A1, T0, 0),                           // 23 sw t0, list
            I(9, A1, A1, 4),                            // 24
            I(4, 0, 0, 7 - 26),                         // 25 b 7
            I(9, T0, T0, UniversalLevelData.RowStride), // 26
            UniversalMips.EncodeJ(3, ObjectPass),       // 27 end: jal the object pass
            I(43, A1, 0, 0),                            // 28 sw zero: list terminator
            I(15, 0, A0, Hi(ObjectList)),               // 29
            I(9, A0, A0, Lo(ObjectList)),               // 30
            R(0, 0, A1, 0x21),                          // 31 move a1, zero
            UniversalMips.EncodeJ(3, ObjectListClear),  // 32 jal clear (the list area, as every renderer does)
            I(9, 0, A2, ObjectListBytes),               // 33 a2 = 0x900
            UniversalMips.EncodeJ(3, ObjectDraw),       // 34 jal draw
            0,                                          // 35
            I(35, Sp, T7, 20),                          // 36
            I(15, 0, At, Hi(CloseRecordsEnd)),          // 37
            I(43, At, T7, Lo(CloseRecordsEnd)),         // 38 restore the close-record end
            I(35, Sp, Ra, 16),                          // 39
            0,                                          // 40
            UniversalMips.JrRa,                         // 41
            I(9, Sp, Sp, 24),                           // 42
        ];
        if (w.Length > SceneDrawHostWords) throw new InvalidOperationException("Crash's scene draw outgrew its host.");
        return w;
    }

    /// <summary>Installs the hide-Spyro hook and Crash's scene draw in an executable (refusing one that differs from the
    /// USA game there); installing it again is a no-op, and the first release's hook is upgraded.</summary>
    public static void PatchExe(byte[] exe)
    {
        int Off(uint address) => (int)(address - UniversalExeBindings.ExeBase);
        uint Word(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(address)));
        bool Holds(uint address, uint[] words) => words.Select((w, i) => Word(address + 4 * (uint)i) == w).All(x => x);
        string Sha(uint address, int bytes) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(exe.AsSpan(Off(address), bytes)));
        void Write(uint address, uint[] words)
        {
            for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(address) + 4 * i), words[i]);
        }
        uint[] hook = DrawHookWords(), scene = SceneDrawWords(), first = FirstDrawHook();
        if (Sha(RescueRenderer, (int)(RescueRendererEnd - RescueRenderer)) != RescueRendererSha256)
            throw new InvalidDataException("The executable's dragon rescue renderer differs from the USA game's, which playable Crash relies on.");
        if (Word(SpyroDraw) == UniversalMips.EncodeJ(2, DrawHookHost) && Word(SpyroDraw + 4) == 0)
        {
            if (Holds(DrawHookHost, hook) && Holds(SceneDrawHost, scene)) return;
            if (!Holds(DrawHookHost, first) || Sha(DrawHookHost + 4 * (uint)first.Length, 4 * (DrawHookHostWords - first.Length)) != DrawHookHostTailSha256 ||
                Sha(SceneDrawHost, 4 * SceneDrawHostWords) != SceneDrawHostSha256)
                throw new InvalidDataException("The executable's Crash hook differs from this editor's.");
            Write(SceneDrawHost, scene);
            Write(DrawHookHost, hook);
            return;
        }
        if (Word(SpyroDraw) != SpyroDrawPreimage[0] || Word(SpyroDraw + 4) != SpyroDrawPreimage[1] ||
            Sha(DrawHookHost, 4 * DrawHookHostWords) != DrawHookHostSha256 || Sha(SceneDrawHost, 4 * SceneDrawHostWords) != SceneDrawHostSha256)
            throw new InvalidDataException("The executable differs from the USA game where playable Crash hooks in.");
        Write(SceneDrawHost, scene);
        Write(DrawHookHost, hook);
        Write(SpyroDraw, [UniversalMips.EncodeJ(2, DrawHookHost), 0]);
    }

    /// <summary>The placements that put playable Crash in a level: his body and legs, appended after the level's own
    /// placements. When those carry save identities (a project's placement ledger), Crash takes the next two; his
    /// objects never hold collected state, so the identities need no keeping between exports.</summary>
    public static IReadOnlyList<UniversalMobyPlacement> WithPlayer(IReadOnlyList<UniversalMobyPlacement> placements)
    {
        if (placements.Any(p => !p.Retired && IsId(p.SpecialId))) return placements;
        int? next = placements.Any(p => p.PersistentSlot != null) ? placements.Max(p => p.PersistentSlot ?? -1) + 1 : null;
        bool identified = placements.Any(p => p.EditorId != null);
        return [.. placements,
            new(SourceKey, -1, 0, 0, 0, SpecialId: Id, EditorId: identified || next != null ? "crash-player-body" : null, PersistentSlot: next),
            new(SourceKey, -1, 0, 0, 0, SpecialId: LegsId, EditorId: identified || next != null ? "crash-player-legs" : null, PersistentSlot: next + 1)];
    }

    /// <summary>Levels Crash can play: every normal level but the flight levels (Crash cannot fly).</summary>
    public static bool IsPlayableLevel(int levelId) => levelId is >= 10 and <= 64 && levelId % 10 <= 4;

    public static bool IsClass(int classId) => classId is BodyClass or LegsClass;
    public static bool IsId(string? id) => id is Id or LegsId;

    private sealed record Cache(string DiscPath, IReadOnlyList<UniversalSpecialAsset> Assets);
    private static readonly ConditionalWeakTable<UniversalSourceLibrary, Cache> Cached = new();

    /// <summary>The body and the legs, sharing one texture image.</summary>
    public static IReadOnlyList<UniversalSpecialAsset> Assets(UniversalSourceLibrary library, CrashWarpedDisc disc)
    {
        if (Cached.TryGetValue(library, out var hit) && hit.DiscPath == disc.ImagePath) return hit.Assets;
        var dark = library.Get("darkhollow").Data;
        int basketRow = Enumerable.Range(0, dark.Scene().RowCount).First(i => BinaryPrimitives.ReadUInt16LittleEndian(dark.Row(i).AsSpan(0x36)) == UniversalCrashBoxes.BasketClass);
        byte[] row = dark.Row(basketRow);
        var nsf = disc.Level(UniversalCrashEnemies.ToadVillageFile, UniversalCrashEnemies.ToadVillageSha256);
        var art = new CrashSpyroModel.Art(nsf);
        var standing = new CrashModel(nsf.Entry(ModelName).Items).Positions(nsf.Entry(Animations[Idle].Name).Items[0]);
        double split = standing.Min(p => p.Z) + (standing.Max(p => p.Z) - standing.Min(p => p.Z)) * SplitHeight;
        bool Body(IReadOnlyList<(double X, double Y, double Z)> rest, CrashModel.Triangle t) =>
            t.Vertices.Average(v => rest[Math.Clamp(v + CrashModel.FrameSpecialVertexCount(nsf.Entry(Animations[Idle].Name).Items[0]), 0, rest.Count - 1)].Z) > split;
        var sounds = UniversalCrashBoxes.SoundSource(dark, [.. Sounds.Select(nsf.Sound)]);
        UniversalSpecialAsset Asset(string id, string name, int classId, bool body)
        {
            var options = new CrashSpyroModel.Options(UniversalCrashEnemies.CrashUnitsPerSpyroUnit / Size)
            { Part = (rest, t) => Body(rest, t) == body, Collision = false };
            byte[]? slots = body ? [.. Enumerable.Range(0, Sounds.Length).Select(i => (byte)i)] : null;
            var built = CrashSpyroModel.Build(nsf, ModelName, Animations, options, slots, art);
            byte[] template = row.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(template.AsSpan(0x36), (ushort)classId);
            template[0x53] = 0xFF;
            template.AsSpan(0x3C, 5).Clear();
            template[0x41] = built.Package[BinaryPrimitives.ReadInt32LittleEndian(built.Package.AsSpan(0x38)) + 0xC];
            return new(id, name, classId, UniversalSpecialBehavior.CrashPlayer, built.Package, art.Vram, body ? sounds : dark, template) { SourceKey = SourceKey };
        }
        IReadOnlyList<UniversalSpecialAsset> assets = [Asset(Id, Name, BodyClass, true), Asset(LegsId, LegsName, LegsClass, false)];
        Cached.AddOrUpdate(library, new(disc.ImagePath, assets));
        return assets;
    }
}
