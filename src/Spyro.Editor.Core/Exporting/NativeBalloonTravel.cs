using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>What a balloonist asks for before he flies Spyro on to the next world.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<BalloonRequirementKind>))]
public enum BalloonRequirementKind { Nothing, Dragons, Treasure, DragonEggs }

/// <summary>A balloonist's price: nothing, or at least <see cref="Count"/> dragons rescued, treasure found or dragon
/// eggs recovered in the whole game (the totals the game keeps: USA 0x80075750, 0x80075860, 0x80075810).</summary>
public sealed record BalloonRequirement(BalloonRequirementKind Kind, int Count)
{
    public static BalloonRequirement Nothing { get; } = new(BalloonRequirementKind.Nothing, 0);
    /// <summary>The most each kind can ask (the game's own totals; a new world's levels add theirs to the first two).</summary>
    public const int MostDragons = 255, MostTreasure = 30000, MostEggs = 12;

    public uint Counter => Kind switch
    {
        BalloonRequirementKind.Dragons => 0x80075750, BalloonRequirementKind.Treasure => 0x80075860, BalloonRequirementKind.DragonEggs => 0x80075810,
        _ => 0x80075750,
    };

    /// <summary>Null when the requirement can be built, otherwise why not.</summary>
    public string? Refusal() => Kind switch
    {
        BalloonRequirementKind.Nothing => null,
        BalloonRequirementKind.Dragons when Count is < 1 or > MostDragons => $"A balloonist can ask for 1 to {MostDragons} dragons.",
        BalloonRequirementKind.Treasure when Count is < 1 or > MostTreasure => $"A balloonist can ask for 1 to {MostTreasure} treasure.",
        BalloonRequirementKind.DragonEggs when Count is < 1 or > MostEggs => $"A balloonist can ask for 1 to {MostEggs} dragon eggs (the game has {MostEggs}).",
        _ => null,
    };

    public bool Same(BalloonRequirement other) => Kind == other.Kind && (Kind == BalloonRequirementKind.Nothing || Count == other.Count);

    /// <summary>"rescue 40 dragons", "find 1200 treasure", "recover 5 dragon eggs", or "nothing".</summary>
    public string Describe() => Kind switch
    {
        BalloonRequirementKind.Dragons => $"rescue {Count} {(Count == 1 ? "dragon" : "dragons")}",
        BalloonRequirementKind.Treasure => $"find {Count} treasure",
        BalloonRequirementKind.DragonEggs => $"recover {Count} dragon {(Count == 1 ? "egg" : "eggs")}",
        _ => "nothing",
    };

    /// <summary>"rescued 40 dragons", "found 50 treasure", "recovered 5 dragon eggs" (after "once he has"), or "".</summary>
    public string Done() => Kind switch
    {
        BalloonRequirementKind.Dragons => $"rescued {Count} {(Count == 1 ? "dragon" : "dragons")}",
        BalloonRequirementKind.Treasure => $"found {Count} treasure",
        BalloonRequirementKind.DragonEggs => $"recovered {Count} dragon {(Count == 1 ? "egg" : "eggs")}",
        _ => "",
    };
}

/// <summary>Where the project's new world lies on the balloonists' route: straight after the homeworld
/// <see cref="AfterLevelId"/> (10-60). That homeworld's balloonist flies Spyro to it once <see cref="ToWorld"/> is met;
/// the new world's own balloonist flies him on to the next world of the game once <see cref="Onward"/> is met (after
/// Gnasty's World there is no next world, and Gnasty's World's balloonist has no room to ask for anything: both are
/// <see cref="BalloonRequirement.Nothing"/> there).</summary>
public sealed record BalloonRoute(int AfterLevelId, BalloonRequirement ToWorld, BalloonRequirement Onward)
{
    public string? Refusal()
    {
        if (AfterLevelId is not (10 or 20 or 30 or 40 or 50 or 60)) return "The new world follows one of the game's six homeworlds.";
        if (ToWorld.Refusal() is { } a) return a;
        if (Onward.Refusal() is { } b) return b;
        if (AfterLevelId == 60 && (ToWorld.Kind != BalloonRequirementKind.Nothing || Onward.Kind != BalloonRequirementKind.Nothing))
            return "After Gnasty's World the new world is the last: its balloonist flies nowhere new, and Gnasty's World's balloonist has no room to ask for anything.";
        return null;
    }
}

/// <summary>The balloonists' route with a seventh world on it (the project's new world, level 70, see
/// <see cref="NativeLevelWindow"/>).
/// <para>The game keeps one byte per world after Artisans (USA 0x800758D1-D5: 2 = its balloonist will fly there) and every
/// homeworld's overlay carries the same two routines for the balloonist's talk and menu (callbacks 0x8007574C and
/// 0x800758D8; word for word the same in the six homeworlds and in Town Square, whose copy a level the editor compiles
/// runs). They count the worlds open as the unbroken run of 2s from the first byte, list them in level-id order, and
/// turn the chosen row into a level id by multiplying.</para>
/// <para>With a route, the six saved bytes 0x800758D0-D5 stand for the route's positions 1-6 (position 0, Artisans, is
/// always open), whatever world is there: the run of 2s still means "open as far as here". Each homeworld's two
/// routines are changed in place (same length) to: count seven positions; list the worlds in route order under their
/// own names; turn a row into the level id at that position; offer the route's next world when the balloonist's price
/// is first met. A balloonist's own decision code (which page he opens with, which byte he sets) is rewritten in place
/// for the new world's balloonist and for the one before it; the earlier ones only move one byte down. The balloon
/// lands in the new world where the game reads it from the words of the unused name "THIGH MASTERS" (0x8001000C; the
/// landing table at 0x8006EA4C has no ninth world), and Spyro steps off where the new world's own routine says.</para>
/// <para>A balloonist who asks for something else than the game's own gets his own lines: they are stored in his
/// overlay where the routine keeps Magic Crafters' egg-thief hints, which he never gives.</para></summary>
public static class NativeBalloonTravel
{
    public const int NewWorldId = 70;
    public const uint TickGlobal = 0x8007574C, RenderGlobal = 0x800758D8, BeginGlobal = 0x800757A0;
    public const int TickWords = 2024, RenderWords = 642;
    /// <summary>The two routines with their inner jumps taken relative (they are the same in every homeworld).</summary>
    public const string TickSha256 = "21e1ec4ab362ac75a3349599dc1cfc59a72bb0fe545ea7215427104bde452ee2",
        RenderSha256 = "342c36c9d7ebd71779b630d647910781e5c2367238901701de20c470f079e208";

    private const uint Flags = 0x800758D0, Page = 0x800777F4, Greeting = 0x800777F8, NextLevel = 0x800758B4, Destination = 0x800777FC;
    private const uint Rand = 0x8006272C;
    public const uint LandingTable = 0x8006EA4C, StepOffTable = 0x8006EADC, PageTable = 0x8006F89C, LineTable = 0x8006F8C4, WorldNames = 0x8006F7D0;
    /// <summary>The 16 bytes of the unused world name "THIGH MASTERS" (the level window names world 7 from its own pool):
    /// the new world's landing (x, y, z, yaw), read as the landing table's entry <see cref="NewWorldDestination"/>.</summary>
    public const uint NewWorldLanding = 0x8001000C;
    public static int NewWorldDestination => checked((int)(((long)NewWorldLanding - LandingTable) / 16));
    /// <summary>How far above a level's start the balloon's landing-table height is. The balloon sinks about 490 units
    /// from it and Spyro steps off from 45 above the balloon; the game then looks for the floor below him to set him
    /// down on, so he must still be above it (measured in the game: Peace Keepers' landing 3,940, lowest 3,448, floor
    /// 3,418; Artisans' 1,098.6 over a floor at 574). <see cref="StepOff"/>: Spyro's spot, 64 units against the
    /// balloon's yaw.</summary>
    public const int LandingAbove = 522, StepOff = 64;

    /// <summary>What the game's own balloonists ask (Artisans, Peace Keepers, Magic Crafters, Beast Makers, Dream Weavers).</summary>
    public static IReadOnlyList<BalloonRequirement> Retail { get; } =
    [
        new(BalloonRequirementKind.Dragons, 10), new(BalloonRequirementKind.Treasure, 1200), new(BalloonRequirementKind.DragonEggs, 5),
        new(BalloonRequirementKind.Dragons, 50), new(BalloonRequirementKind.Treasure, 6000),
    ];
    public static IReadOnlyList<string> BalloonistNames { get; } = ["MARCO", "GOSNOLD", "TUCO", "CRAY", "AMOS", "HAK"];

    /// <summary>The route's level ids by position (0 = Artisans).</summary>
    public static int[] Order(BalloonRoute route)
    {
        List<int> ids = [10, 20, 30, 40, 50, 60];
        ids.Insert(ids.IndexOf(route.AfterLevelId) + 1, NewWorldId);
        return [.. ids];
    }
    private static int World(int levelId) => levelId / 10 - 1;
    /// <summary>The route as one word: four bits a position, the world there (0-5 the game's, 6 the new one).</summary>
    public static uint Packed(int[] order) => order.Select((id, k) => (uint)World(id) << (4 * k)).Aggregate(0u, (a, b) => a | b);
    private static int FlagLow(int position) => (int)(Flags & 0xFFFF) + position - 1;     // position 1-6 -> 0x58D0-D5
    private static int DestinationIndex(int levelId) => levelId == NewWorldId ? NewWorldDestination : World(levelId);

    /// <summary>Where the balloon lands (the landing table's words) and where Spyro steps off for a level that starts at
    /// <paramref name="x"/>, <paramref name="y"/> on a floor at <paramref name="z"/> (game units) heading
    /// <paramref name="heading"/> (0-255): Spyro steps off at his start, the balloon comes down behind him.</summary>
    public static ((int X, int Y, int Z, int Yaw) Landing, (int X, int Y) StepOff) Arrival(int x, int y, int z, int heading)
    {
        double a = heading * Math.PI / 128;
        int bx = x - (int)Math.Round(Math.Cos(a) * StepOff * 16), by = y - (int)Math.Round(Math.Sin(a) * StepOff * 16);
        // The game puts Spyro's spot 64 units against the balloon's yaw.
        return ((bx, by, z + LandingAbove * 16, (heading + 128) & 0xFF), (x, y));
    }

    // ---------------------------------------------------------------- the two shared routines

    private static uint Word(byte[] overlay, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(checked((int)(address - UniversalOverlay.Base))));
    private static void Put(byte[] overlay, uint address, uint word) => BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(checked((int)(address - UniversalOverlay.Base))), word);

    /// <summary>The hash of <paramref name="words"/> instructions at <paramref name="start"/> with jumps inside the
    /// routine taken relative to its start.</summary>
    public static string RoutineHash(byte[] overlay, uint start, int words)
    {
        byte[] normal = new byte[4 * words];
        for (int k = 0; k < words; k++)
        {
            uint pc = start + 4 * (uint)k, w = Word(overlay, pc);
            if (UniversalMips.Op(w) is 2 or 3 && UniversalMips.BranchTarget(w, pc) is var t && t >= start && t < start + 4 * (uint)words)
                w = (w & 0xFC000000) | ((t - start) >> 2);
            BinaryPrimitives.WriteUInt32LittleEndian(normal.AsSpan(4 * k), w);
        }
        return Convert.ToHexStringLower(SHA256.HashData(normal));
    }

    /// <summary>True when the overlay has the game's two balloonist routines, unchanged, at these addresses.</summary>
    public static bool HasRoutines(byte[] overlay, uint tick, uint render) =>
        tick >= UniversalOverlay.Base && render >= UniversalOverlay.Base &&
        tick - UniversalOverlay.Base + 4L * TickWords <= overlay.Length && render - UniversalOverlay.Base + 4L * RenderWords <= overlay.Length &&
        RoutineHash(overlay, tick, TickWords) == TickSha256 && RoutineHash(overlay, render, RenderWords) == RenderSha256;

    private static NativeCaveAssembler.Block At(uint address) => new("balloon", false) { Address = address };
    private static void Write(byte[] overlay, NativeCaveAssembler.Block block, int words, IReadOnlyDictionary<string, uint>? outside = null)
    {
        if (block.Items.Count > words) throw new InvalidDataException(words <= 16
            ? "This balloonist is Gnasty's World's, who only ever shows his menu: he has no room to ask for anything. Use another homeworld's balloonist here."
            : $"A balloonist's code needs {block.Items.Count} instructions where the game has {words}.");
        while (block.Items.Count < words) block.Nop();
        Dictionary<string, uint> labels = outside == null ? [] : new(outside);
        foreach (var (name, index) in block.Labels) labels[name] = block.Address + 4 * (uint)index;
        uint[] code = block.Encode(labels);
        for (int k = 0; k < code.Length; k++) Put(overlay, block.Address + 4 * (uint)k, code[k]);
    }
    private static void Expect(byte[] overlay, uint address, uint word, string what)
    {
        if (Word(overlay, address) != word) throw new InvalidDataException($"A homeworld's balloonist routine differs from the game's ({what}, 0x{address:X8}).");
    }

    /// <summary>Changes the talk and menu routines of the homeworld <paramref name="levelId"/> for <paramref name="route"/>
    /// (in place; the overlay must hold the game's own routines at <paramref name="tick"/> and <paramref name="render"/>).</summary>
    public static void PatchRoutines(byte[] overlay, uint tick, uint render, BalloonRoute route, int levelId)
    {
        if (!HasRoutines(overlay, tick, render)) throw new InvalidDataException("This homeworld's overlay does not hold the game's balloonist routines.");
        int[] order = Order(route);
        int q = Array.IndexOf(order, levelId);
        if (q < 0) throw new InvalidDataException($"Level {levelId} is not a homeworld on the balloonists' route.");
        uint packed = Packed(order), end = tick + 0x1F80;
        int hi = (int)(packed >> 16), lo = (int)(packed & 0xFFFF);
        // Artisans' pages 1 and 2 end the talk while the next world is closed: position 1's byte.
        Expect(overlay, tick + 0x978, 0x904258D1, "next world closed");
        Put(overlay, tick + 0x978, 0x90420000 | (uint)FlagLow(1));
        // How many rows the menu has: Stay Here and the worlds open, as the unbroken run of 2s over positions 1-6. A
        // homeworld outside that run (reached through a doorway before its balloon route opened) is not among the
        // worlds listed, so every world of the run is a row.
        Write(overlay, At(tick + 0xB04)
            .Addiu("v1", "zero", 1).Addiu("a0", "zero", 2)
            .Label("next").Lui("at", 0x8007).Addu("at", "at", "v1").Lbu("v0", FlagLow(1) - 1, "at")
            .Slti("t0", "v1", 6).Bne("v0", "a0", "done").Nop()
            .Bnez("t0", "next").Addiu("v1", "v1", 1)
            .Label("done").Slti("v0", "v1", q + 1).Addu("v1", "v1", "v0"), 12);
        // The chosen row (v1, 1 = the first world listed) to the level id at that position, skipping this homeworld's own.
        Expect(overlay, tick + 0xC2C, 0x00031080, "menu choice");
        Write(overlay, At(tick + 0xC2C)
            .Addiu("v0", "v1", -1).Slti("at", "v0", q).Bnez("at", "keep").Lui("v1", hi).Addiu("v0", "v0", 1)
            .Label("keep").Ori("v1", "v1", lo).Sll("v0", "v0", 2).Srlv("v1", "v1", "v0").Andi("v1", "v1", 0xF)
            .Addiu("v0", "v1", 1).Sll("at", "v0", 2).Addu("v0", "at", "v0").Sll("v0", "v0", 1)
            .Lui("at", 0x8007).Sw("v0", (int)(NextLevel & 0xFFFF), "at")
            .Addiu("v0", "zero", 6).Bne("v1", "v0", "game").Lui("at", 0x8007).Addiu("v1", "zero", NewWorldDestination)
            .Label("game").Sw("v1", (int)(Destination & 0xFFFF), "at").J(end).Nop(), 26);
        if (q + 1 < order.Length)
        {
            int next = order[q + 1];
            // "Are you ready to go?" after the price is first met: the route's next world.
            Expect(overlay, tick + 0xDA8, 0x8C42596C, "next world");
            Write(overlay, At(tick + 0xDA0)
                .Addiu("v0", "zero", next).Lui("at", 0x8007).Sw("v0", (int)(NextLevel & 0xFFFF), "at")
                .Addiu("v1", "zero", DestinationIndex(next)).Sw("v1", (int)(Destination & 0xFFFF), "at").J(end).Nop(), 17);
            // ... and its name on that page (the world names start one word in: world w is name w + 1).
            Expect(overlay, render + 0x804, 0x8C63596C, "next world's name");
            Put(overlay, render + 0x804, 0x24030000 | (uint)(World(next) * 10));
        }
        // The menu's rows: the open positions in route order, each under its world's name.
        Expect(overlay, render + 0x514, 0x00111080, "menu row name");
        Expect(overlay, render + 0x5B4, 0x2A220006, "menu rows");
        Expect(overlay, render + 0x5BC, 0x3C026666, "menu loop");
        var names = new Dictionary<string, uint> { ["skip"] = render + 0x5B0, ["exit"] = render + 0x5C0, ["show"] = render + 0x4F8 };
        Write(overlay, At(render + 0x4B4)
            .Addiu("v0", "zero", q).Beq("v0", "s1", "skip").Lui("t0", hi).Ori("t0", "t0", lo)
            .Sll("v0", "s1", 2).Srlv("t0", "t0", "v0").Andi("t0", "t0", 0xF)
            .Beqz("s1", "show").Sll("t0", "t0", 2)
            .Lui("at", 0x8007).Addu("at", "at", "s1").Lbu("v1", FlagLow(1) - 1, "at").Addiu("v0", "zero", 2)
            .Bne("v1", "v0", "exit").Nop(), 17, names);
        Put(overlay, render + 0x514, 0x01001021);                              // move v0, t0
        Put(overlay, render + 0x5B4, 0x2A220007);                              // slti v0, s1, 7
        Put(overlay, render + 0x5BC, 0x24020000 | (uint)q);                    // the loop's first word again (delay slot)
    }

    /// <summary>In a level the editor compiled: Spyro steps off the balloon at (<paramref name="x"/>, <paramref name="y"/>)
    /// (game units) whatever the game's table says for the world (the routine at <paramref name="tick"/> reads it there).</summary>
    public static void PatchStepOff(byte[] overlay, uint tick, int x, int y)
    {
        Expect(overlay, tick + 0x1CD4, 0x8C4277FC, "step-off spot");
        Expect(overlay, tick + 0x1CEC, 0x26040020, "step-off spot");
        Write(overlay, At(tick + 0x1CD0).Lui("v1", (x >> 16) & 0xFFFF).Ori("v1", "v1", x & 0xFFFF), 7);
        Write(overlay, At(tick + 0x1CF4).Lui("v1", (y >> 16) & 0xFFFF).Ori("v1", "v1", y & 0xFFFF), 3);
    }

    // ---------------------------------------------------------------- a balloonist's own decision code

    /// <summary>A balloonist's decision code in an overlay: from after the call that begins the talk to its last jump out.</summary>
    public sealed record Decision(uint Start, int Words, uint Exit);

    /// <summary>Every balloonist's decision code in <paramref name="overlay"/> (one per balloonist kind the level has).</summary>
    public static IReadOnlyList<Decision> Decisions(byte[] overlay)
    {
        List<Decision> found = [];
        for (int o = 0; o + 20 <= overlay.Length; o += 4)
        {
            uint a = UniversalOverlay.Base + (uint)o;
            // lui v0, 0x8007 ; lw v0, 0x57A0(v0) ; nop ; jalr v0 ; move a0, sN
            if (Word(overlay, a) != 0x3C028007 || Word(overlay, a + 4) != 0x8C4257A0 || Word(overlay, a + 8) != 0 || Word(overlay, a + 12) != 0x0040F809) continue;
            uint start = a + 20, far = start, exit = 0;
            int words = 0;
            for (uint pc = start; pc + 8 <= UniversalOverlay.Base + (uint)overlay.Length && pc < start + 4 * 96; pc += 4)
            {
                uint w = Word(overlay, pc);
                if (UniversalMips.IsPcRelative(w)) far = Math.Max(far, UniversalMips.BranchTarget(w, pc));
                else if (UniversalMips.Op(w) == 2)
                {
                    uint t = UniversalMips.BranchTarget(w, pc);
                    if (t >= start && t < start + 4 * 96) { far = Math.Max(far, t); continue; }   // a jump inside the code
                    if (exit == 0) exit = t;
                    if (t == exit && pc + 8 > far) { words = (int)((pc + 8 - start) / 4); break; }
                }
            }
            if (words == 0) throw new InvalidDataException($"A balloonist's code at 0x{start:X8} does not end the way the game's does.");
            found.Add(new(start, words, exit));
        }
        return found;
    }

    /// <summary>The bytes (addresses) a balloonist's decision code reads or writes among the six world bytes.</summary>
    private static List<(uint At, int Low)> FlagUses(byte[] overlay, Decision d)
    {
        List<(uint, int)> uses = [];
        for (int k = 0; k < d.Words; k++)
        {
            uint pc = d.Start + 4 * (uint)k, w = Word(overlay, pc);
            int op = UniversalMips.Op(w), low = UniversalMips.Imm(w);
            if (op is 0x24 or 0x28 or 0x09 && low >= (int)(Flags & 0xFFFF) && low <= (int)(Flags & 0xFFFF) + 5) uses.Add((pc, low));
        }
        return uses;
    }

    /// <summary>A balloonist before the new world on the route keeps what he asks; the byte he reads and sets, and the
    /// ones he sets for the worlds before his, each move one down (position n's byte is where world n's was).</summary>
    public static void ShiftDecision(byte[] overlay, Decision d)
    {
        var uses = FlagUses(overlay, d);
        if (uses.Count == 0 || uses.Any(u => u.Low == (int)(Flags & 0xFFFF)))
            throw new InvalidDataException("A balloonist's code does not use the world bytes the way the game's does.");
        foreach (var (at, low) in uses) Put(overlay, at, UniversalMips.WithImm(Word(overlay, at), low - 1));
    }

    /// <summary>Rewrites a balloonist's decision code for the balloonist at route position <paramref name="position"/>.
    /// <paramref name="opens"/>: the positions he marks open whenever he is talked to (the worlds before his, for a
    /// balloonist of the game). <paramref name="asks"/> null: he only offers the menu (the last on the route). Otherwise
    /// he opens position + 1 once <paramref name="asks"/> is met (and with it <paramref name="own"/>, his own position,
    /// for the new world's balloonist: Spyro may have walked in through its doorway), speaking pages
    /// <paramref name="pages"/>..+5 (0: first meeting, 2: not yet, 3: met at first meeting, 4: met later, then 5 and "Are
    /// you ready to go?").</summary>
    public static void WriteDecision(byte[] overlay, Decision d, int position, IReadOnlyList<int> opens, BalloonRequirement? asks, int pages, bool own)
    {
        var b = At(d.Start).Lui("v1", 0x8007).Addiu("t1", "zero", 2);
        foreach (int p in opens) b.Sb("t1", FlagLow(p), "v1");
        int page = (int)(Page & 0xFFFF);
        if (asks == null)
        {
            b.Addiu("v0", "zero", 0x1E).Sw("v0", page, "v1").J(d.Exit).Nop();
            Write(overlay, b, d.Words);
            return;
        }
        if ((asks.Counter & 0xFFFF0000) != 0x80070000 || asks.Count > short.MaxValue) throw new InvalidDataException("A balloonist's price is out of range.");
        int flag = FlagLow(position + 1);
        b.Lw("v0", (int)(asks.Counter & 0xFFFF), "v1").Lbu("a0", flag, "v1")
            .Slti("v0", "v0", asks.Kind == BalloonRequirementKind.Nothing ? 0 : asks.Count)
            .Bnez("v0", "short").Sltiu("at", "a0", 2)
            .Beqz("at", "menu").Addiu("v0", "zero", pages + 4)
            .Bnez("a0", "open").Nop()
            .Addiu("v0", "zero", pages + 3)
            .Label("open").Sw("v0", page, "v1").Sb("t1", flag, "v1");
        if (own && position >= 1) b.Sb("t1", FlagLow(position), "v1");
        b.J(d.Exit).Nop()
            .Label("short").Bnez("a0", "said").Addiu("v0", "zero", pages + 2)
            .Addiu("v0", "zero", pages).Addiu("a0", "zero", 1).Sb("a0", flag, "v1")
            .Label("said").Sw("v0", page, "v1").J(d.Exit).Nop()
            // The world is open: the menu, under one of its three greetings.
            .Label("menu").Addiu("v0", "zero", 0x1E).Sw("v0", page, "v1").Jal(Rand).Nop()
            .Lui("v1", 0x5555).Ori("v1", "v1", 0x5556).Mult("v0", "v1").Sra("a0", "v0", 31).Mfhi("t1").Subu("a0", "t1", "a0")
            .Sll("v1", "a0", 1).Addu("v1", "v1", "a0").Subu("v0", "v0", "v1")
            .Lui("at", 0x8007).Sw("v0", (int)(Greeting & 0xFFFF), "at").J(d.Exit).Nop();
        Write(overlay, b, d.Words);
    }

    // ---------------------------------------------------------------- a balloonist's own lines

    /// <summary>A balloonist's six pages (three lines each, at most <see cref="MaxLine"/> letters) for what he asks and
    /// where he flies: first meeting (two pages), not yet, met at first meeting, met later, the invitation.</summary>
    public const int MaxLine = 22;
    public static IReadOnlyList<string> Lines(BalloonRequirement asks, string destination)
    {
        string name = destination.ToUpperInvariant();
        string[] want = asks.Kind switch
        {
            BalloonRequirementKind.Dragons => ["HAVE RESCUED", "FOR YOU TO RESCUE", $"{asks.Count} {(asks.Count == 1 ? "DRAGON" : "DRAGONS")}."],
            BalloonRequirementKind.Treasure => ["HAVE FOUND", "FOR YOU TO FIND", $"{asks.Count} TREASURE."],
            BalloonRequirementKind.DragonEggs => ["HAVE RECOVERED", "FOR YOU TO RECOVER", $"{asks.Count} DRAGON {(asks.Count == 1 ? "EGG" : "EGGS")}."],
            _ => ["", "", ""],
        };
        string[] lines = asks.Kind == BalloonRequirementKind.Nothing
            ?
            [
                "MY BALLOON CAN TAKE", "YOU TO", name + ".",
                "JUST SAY THE WORD", "AND WE WILL BE", "ON OUR WAY.",
                "JUST SAY THE WORD", "AND WE WILL BE", "ON OUR WAY.",
                "HELLO THERE, SPYRO!", "MY BALLOON IS READY", "WHENEVER YOU ARE.",
                "HELLO AGAIN, SPYRO!", "MY BALLOON IS READY", "WHENEVER YOU ARE.",
                "HOP ABOARD FOR", name, "IF YOU ARE READY.",
            ]
            :
            [
                "MY BALLOON CAN TAKE", "YOU TO", name + ".",
                "COME BACK WHEN YOU", want[0], want[2],
                "I AM STILL WAITING", want[1], want[2],
                "WELL, LOOK AT YOU!", "YOU HAVE ALREADY DONE", "ALL THAT I ASK.",
                "WELL DONE, SPYRO!", "I KNEW YOU COULD", "DO IT.",
                "HOP ABOARD FOR", name, "IF YOU ARE READY.",
            ];
        if (lines.Any(l => l.Length > MaxLine)) throw new InvalidDataException("A balloonist's line is too long for his speech box.");
        return lines;
    }

    /// <summary>Stores a balloonist's own 18 lines (and, when <paramref name="title"/> is given, his name for the talk's
    /// heading: the new world's) in the overlay and points the routines at them for pages <paramref name="pages"/>..+5.
    /// They go where the talk routine keeps the egg-thief hints (two stretches of code only Magic Crafters' balloonist
    /// reaches, for page 14 and pages 33-38): this balloonist's pages never lead there, and the two ways in are closed.</summary>
    public static void WriteLines(byte[] overlay, uint tick, uint render, int pages, IReadOnlyList<string> lines, string? title)
    {
        if (lines.Count != 18 || pages is not (0 or 6 or 18 or 24)) throw new InvalidDataException("A balloonist speaks six pages of three lines, from a page set without the egg-thief hints.");
        Expect(overlay, tick + 0x9B8, 0x14620041, "hint page");
        Expect(overlay, tick + 0xC98, 0x10400052, "hint pages");
        // Nothing else leads into the two stretches.
        (uint Start, uint End)[] room = [(tick + 0x9C0, tick + 0xAC0), (tick + 0xDE4, tick + 0xEB4)];
        for (int k = 0; k < TickWords; k++)
        {
            uint pc = tick + 4 * (uint)k, w = Word(overlay, pc);
            if (room.Any(r => pc >= r.Start && pc < r.End) || pc == tick + 0x9B8 || pc == tick + 0xC98) continue;
            if ((UniversalMips.IsPcRelative(w) || UniversalMips.Op(w) is 2 or 3) && UniversalMips.BranchTarget(w, pc) is var t && room.Any(r => t >= r.Start && t < r.End))
                throw new InvalidDataException("A balloonist's routine reaches its egg-thief hints from somewhere unexpected.");
        }
        Put(overlay, tick + 0x9B8, 0x10000041);            // page 14 is an ordinary page: on to the menu
        Put(overlay, tick + 0xC98, 0x1040002A);            // pages past 32 end the talk
        // Stretch one: seven page bytes (first line of pages 0-5 and the end), 18 line pointers, then text; stretch two: text.
        uint table = room[0].Start, pointers = table + 8;
        (uint At, uint End)[] text = [(pointers + 72, room[0].End), (room[1].Start, room[1].End)];
        foreach (var r in room) for (uint a = r.Start; a < r.End; a += 4) Put(overlay, a, 0);
        Dictionary<string, uint> placed = new(StringComparer.Ordinal);
        uint Place(string s)
        {
            if (placed.TryGetValue(s, out uint at)) return at;
            byte[] bytes = Encoding.ASCII.GetBytes(s + "\0");
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i].End - text[i].At < bytes.Length) continue;
                at = text[i].At;
                bytes.CopyTo(overlay.AsSpan((int)(at - UniversalOverlay.Base)));
                text[i].At += (uint)bytes.Length;
                return placed[s] = at;
            }
            throw new InvalidDataException("A balloonist's lines do not fit where his overlay can keep them.");
        }
        foreach (string s in lines.Concat(title == null ? [] : new[] { title }).Distinct(StringComparer.Ordinal).OrderByDescending(s => s.Length)) Place(s);
        for (int i = 0; i < 7; i++) overlay[(int)(table - UniversalOverlay.Base) + i] = (byte)(3 * i);
        for (int i = 0; i < 18; i++) Put(overlay, pointers + 4 * (uint)i, Place(lines[i]));
        // The menu routine's page table (first line of page n, and of n + 1) and line pointers.
        void Pair(uint lui, uint use, uint address, uint expectLui, uint expectUse)
        {
            Expect(overlay, lui, expectLui, "page table"); Expect(overlay, use, expectUse, "page table");
            Put(overlay, lui, UniversalMips.WithImm(Word(overlay, lui), UniversalMips.Hi(address)));
            Put(overlay, use, UniversalMips.WithImm(Word(overlay, use), UniversalMips.Lo(address)));
        }
        uint first = table - (uint)pages;
        Pair(render + 0x1C4, render + 0x1CC, first, 0x3C018007, 0x9030F89C);
        Pair(render + 0x1D0, render + 0x1D8, first + 1, 0x3C018007, 0x9022F89D);
        Pair(render + 0x238, render + 0x240, first + 1, 0x3C018007, 0x9022F89D);
        Pair(render + 0x1F4, render + 0x1F8, pointers, 0x3C038007, 0x2463F8C4);
        if (title != null)
        {
            // The heading "%s THE BALLOONIST": this balloonist's own name (the game's table has six).
            Expect(overlay, render + 0x78, 0x8C26F880, "balloonist's name");
            uint name = Place(title);
            Put(overlay, render + 0x70, 0x3C060000 | (uint)UniversalMips.Hi(name));           // lui a2
            Put(overlay, render + 0x74, 0x24C60000 | (uint)(UniversalMips.Lo(name) & 0xFFFF));   // addiu a2, a2
            Put(overlay, render + 0x78, 0);
        }
    }

    // ---------------------------------------------------------------- the executable

    private static int Off(uint address) => checked((int)(address - UniversalExeBindings.ExeBase));
    private static uint ExeWord(byte[] exe, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(address)));
    private static void ExePut(byte[] exe, uint address, uint word) => BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(address)), word);

    /// <summary>Where the balloon lands in one of the game's six worlds and where Spyro steps off there (the two tables'
    /// entries for that world): a homeworld the editor rebuilt has its own start.</summary>
    public static void PatchArrival(byte[] exe, int levelId, (int X, int Y, int Z, int Yaw) landing, (int X, int Y) stepOff)
    {
        if (levelId is not (10 or 20 or 30 or 40 or 50 or 60)) throw new InvalidDataException("The game's landing table has its six homeworlds.");
        uint w = (uint)World(levelId);
        ExePut(exe, LandingTable + 16 * w, (uint)landing.X); ExePut(exe, LandingTable + 16 * w + 4, (uint)landing.Y);
        ExePut(exe, LandingTable + 16 * w + 8, (uint)landing.Z); ExePut(exe, LandingTable + 16 * w + 12, (uint)landing.Yaw);
        ExePut(exe, StepOffTable + 8 * w, (uint)stepOff.X); ExePut(exe, StepOffTable + 8 * w + 4, (uint)stepOff.Y);
    }

    /// <summary>The executable's part of a route (after the level window is installed): a new game starts with every
    /// position closed (the game opens the first byte, Artisans' own, which is now position 1's); the cheat that opens
    /// every world keeps doing so; the new world's landing goes where "THIGH MASTERS" was. When the balloonist before the
    /// new world keeps the game's price (<paramref name="keptPages"/>: his page set, else -1), his invitation page reads
    /// "HOP ABOARD FOR THE / (the new world's name) / IF YOU ARE READY." from lines the game already has.</summary>
    public static void PatchExecutable(byte[] exe, (int X, int Y, int Z, int Yaw) landing, int keptPages)
    {
        if (ExeWord(exe, 0x80012688) != 0x24020002 && ExeWord(exe, 0x80012688) != 0x24020000)
            throw new InvalidDataException("The executable's New Game differs from the USA game's where the worlds are closed.");
        ExePut(exe, 0x80012688, 0x24020000);
        if (ExeWord(exe, WorldNames + 28) == NewWorldLanding) throw new InvalidDataException("The level window must be installed before the balloon route (it names world 7 from its own pool).");
        ExePut(exe, NewWorldLanding, (uint)landing.X); ExePut(exe, NewWorldLanding + 4, (uint)landing.Y);
        ExePut(exe, NewWorldLanding + 8, (uint)landing.Z); ExePut(exe, NewWorldLanding + 12, (uint)landing.Yaw);
        if (keptPages >= 0)
        {
            // Page keptPages + 5's three lines; "HOP ABOARD FOR THE" and "IF YOU ARE READY." are the game's.
            uint firstLine = exe[Off(PageTable) + keptPages + 5];
            ExePut(exe, LineTable + 4 * firstLine, 0x800107C4);
            ExePut(exe, LineTable + 4 * firstLine + 4, ExeWord(exe, WorldNames + 28));
            ExePut(exe, LineTable + 4 * firstLine + 8, 0x80010798);
        }
    }

    /// <summary>The new world off the balloonists' route, with a balloonist in its homeworld all the same: he shows the
    /// game's menu (the worlds open in the game's own order, which fly Spyro out) under his own name. Without this the
    /// talk's heading would read a balloonist's name the game has no seventh of. False: no balloonist stands there.</summary>
    public static bool ApplyOffRoute(byte[] overlay, uint tick, uint render, string balloonistName)
    {
        var decisions = Decisions(overlay);
        if (decisions.Count == 0) return false;
        if (!HasRoutines(overlay, tick, render)) throw new InvalidDataException("This homeworld's overlay does not hold the game's balloonist routines.");
        foreach (var d in decisions) WriteDecision(overlay, d, 6, [], null, 0, false);
        WriteLines(overlay, tick, render, 6, Lines(BalloonRequirement.Nothing, "ARTISANS"), balloonistName);
        return true;
    }

    // ---------------------------------------------------------------- one homeworld

    /// <summary>A homeworld on the route as Create BIN has it.</summary>
    /// <param name="Compiled">The editor compiled it (the new world's, or a rebuilt homeworld of the game): its balloonist
    /// may be any world's, so his decision code is always rewritten for the place he stands in.</param>
    /// <param name="StepOff">Where Spyro steps off the balloon there (game units), for a compiled level.</param>
    /// <param name="WorldName">The new world's name (the destination in the lines of the balloonist before it).</param>
    public sealed record Homeworld(int LevelId, uint Tick, uint Render, bool Compiled, (int X, int Y)? StepOff, string WorldName, string? BalloonistName);

    /// <summary>What <see cref="Apply"/> did to a homeworld (for the status line and tests).</summary>
    public sealed record Applied(int LevelId, int Position, int Balloonists, string Decision, int KeptPages);

    private static readonly string[] WorldTitles = ["ARTISANS", "PEACE KEEPERS", "MAGIC CRAFTERS", "BEAST MAKERS", "DREAM WEAVERS", "GNASTY'S WORLD"];

    /// <summary>Changes one homeworld's overlay for the route: the shared routines, and its balloonist's decision code
    /// and lines as his place on the route needs.</summary>
    public static Applied Apply(byte[] overlay, Homeworld home, BalloonRoute route)
    {
        if (route.Refusal() is { } why) throw new InvalidDataException(why);
        int[] order = Order(route);
        int q = Array.IndexOf(order, home.LevelId), world = World(home.LevelId), before = World(route.AfterLevelId);
        if (q < 0) throw new InvalidDataException($"Level {home.LevelId} is not a homeworld on the balloonists' route.");
        var decisions = Decisions(overlay);
        if (decisions.Count == 0)
        {
            // No balloonist stands here: Spyro can still arrive by balloon, and the routines must agree with the others.
            PatchRoutines(overlay, home.Tick, home.Render, route, home.LevelId);
            if (home.StepOff is { } spot0) PatchStepOff(overlay, home.Tick, spot0.X, spot0.Y);
            return new(home.LevelId, q, 0, "no balloonist", -1);
        }
        string did = "unchanged"; int kept = -1;
        bool last = q == order.Length - 1;
        string nextName = last ? "" : order[q + 1] == NewWorldId ? home.WorldName : WorldTitles[World(order[q + 1])];
        IReadOnlyList<string>? lines = null; int pages = -1; string? title = null;
        foreach (var d in decisions)
        {
            if (home.LevelId == NewWorldId)
            {
                // The new world's balloonist opens nothing by being there (Spyro may have come through the doorway).
                if (last) WriteDecision(overlay, d, q, [], null, 0, false);
                else { pages = 6; WriteDecision(overlay, d, q, [], route.Onward, pages, true); lines = Lines(route.Onward, nextName); }
                title = home.BalloonistName ?? "NILS";
                did = last ? "the menu" : "asks to " + route.Onward.Describe();
            }
            else if (world == before && world == 5)
            {
                // Gnasty's World's balloonist (fifteen instructions) opens every position, the new world's with them.
                WriteDecision(overlay, d, q, [1, 2, 3, 4, 5, 6], null, 0, false);
                did = "opens the new world";
            }
            else if (world == before && !route.ToWorld.Same(Retail[world]))
            {
                pages = q == 0 ? 0 : 6;
                WriteDecision(overlay, d, q, [.. Enumerable.Range(1, q)], route.ToWorld, pages, false);
                lines = Lines(route.ToWorld, nextName);
                did = "asks to " + route.ToWorld.Describe();
            }
            else if (home.Compiled)
            {
                // A rebuilt homeworld's balloonist may be another world's: he is given this world's place and price.
                if (last) WriteDecision(overlay, d, q, [.. Enumerable.Range(1, q)], null, 0, false);
                else WriteDecision(overlay, d, q, [.. Enumerable.Range(1, q)], Retail[world], world * 6, false);
                if (world == before) kept = world * 6;
                did = last ? "the menu" : "the game's price";
            }
            else if (world <= before)
            {
                ShiftDecision(overlay, d);
                if (world == before) kept = world * 6;
                did = "the game's price, one byte down";
            }
            else did = "unchanged";
        }
        PatchRoutines(overlay, home.Tick, home.Render, route, home.LevelId);
        if (lines != null) WriteLines(overlay, home.Tick, home.Render, pages, lines, title);
        else if (title != null) WriteLines(overlay, home.Tick, home.Render, 6, Lines(BalloonRequirement.Nothing, "ARTISANS"), title);
        if (home.StepOff is { } spot) PatchStepOff(overlay, home.Tick, spot.X, spot.Y);
        return new(home.LevelId, q, decisions.Count, did + (lines != null ? ", his own lines" : ""), kept);
    }
}
