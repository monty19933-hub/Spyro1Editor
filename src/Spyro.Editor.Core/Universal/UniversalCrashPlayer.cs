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
    // the flag is set; level loads clear the flag, so levels without Crash keep Spyro everywhere. (Crash is not drawn
    // in those scenes: their renderer only draws the scene's own actors.) The hook lives in libgpu's ClearImage
    // (0x8005F98C, 39 words), which nothing in the executable or the WAD calls or points to.
    public const uint SpyroDraw = 0x80023AC4, SpyroDrawResume = 0x80023ACC, DrawHookHost = 0x8005F98C;
    public const int DrawHookHostWords = 39;
    private const string DrawHookHostSha256 = "32024b6f678459d35b6bb04ac5f66ac4de3de53833ace02b0a503b86085d8217";
    private static readonly uint[] SpyroDrawPreimage = [0x3C018007, 0x24217DD8];   // lui at, 0x8007; addiu at, at, 0x7DD8

    /// <summary>Installs the hide-Spyro hook in an executable (refusing one that differs from the USA game there);
    /// installing it again is a no-op.</summary>
    public static void PatchExe(byte[] exe)
    {
        int Off(uint address) => (int)(address - UniversalExeBindings.ExeBase);
        uint Word(byte[] b, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(Off(address)));
        uint[] hook =
        [
            0x3C018007,                                         // lui at, 0x8007
            0x8C215814,                                         // lw at, 0x5814(at): the hide flag
            0x00000000,
            0x14200004,                                         // bne at, zero, skip
            0x00000000,
            SpyroDrawPreimage[0],                               // the two instructions the jump replaced
            UniversalMips.EncodeJ(2, SpyroDrawResume),
            SpyroDrawPreimage[1],
            UniversalMips.JrRa,                                 // skip: return without drawing
            0x00000000,
        ];
        if (Word(exe, SpyroDraw) == UniversalMips.EncodeJ(2, DrawHookHost))
        {
            for (int i = 0; i < hook.Length; i++)
                if (Word(exe, DrawHookHost + (uint)i * 4) != hook[i]) throw new InvalidDataException("The executable's Crash hook differs from this editor's.");
            return;
        }
        if (Word(exe, SpyroDraw) != SpyroDrawPreimage[0] || Word(exe, SpyroDraw + 4) != SpyroDrawPreimage[1] ||
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(exe.AsSpan(Off(DrawHookHost), DrawHookHostWords * 4))) != DrawHookHostSha256)
            throw new InvalidDataException("The executable differs from the USA game where playable Crash hooks in.");
        for (int i = 0; i < hook.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(DrawHookHost) + 4 * i), hook[i]);
        BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(SpyroDraw)), UniversalMips.EncodeJ(2, DrawHookHost));
        BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(SpyroDraw) + 4), 0);
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
