using System.Buffers.Binary;
using System.Text;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

// The balloonists' route with the project's new world on it (NativeBalloonTravel).
//   (no arguments)        source-free checks: the route, a balloonist's lines, his decision code run instruction by
//                         instruction for every state of the game
//   --source <disc .bin>  also the game's own overlays: every homeworld's two balloonist routines changed for every
//                         place the new world can take, then run (the menu's rows, the level each row flies to, the
//                         "ready to go" page), and every balloonist's own decision code
int checks = 0;
void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException("check failed: " + what); checks++; }
string? source = null;
for (int i = 0; i < args.Length; i++) if (args[i] == "--source") source = args[++i];

const uint Flags = 0x800758D0, Page = 0x800777F4, Greeting = 0x800777F8, NextLevel = 0x800758B4, Destination = 0x800777FC;
var none = BalloonRequirement.Nothing;

// ---- the route
for (int after = 10; after <= 60; after += 10)
{
    var route = new BalloonRoute(after, none, none);
    int[] order = NativeBalloonTravel.Order(route);
    Check(order.Length == 7 && order[0] == 10 && order[after / 10] == 70 && order.Where(i => i != 70).SequenceEqual([10, 20, 30, 40, 50, 60]), $"after {after}: the new world follows it");
    uint packed = NativeBalloonTravel.Packed(order);
    Check(Enumerable.Range(0, 7).All(k => ((packed >> (4 * k)) & 15) == order[k] / 10 - 1), "the route packs four bits a position");
}
Check(new BalloonRoute(15, none, none).Refusal() != null && new BalloonRoute(60, new(BalloonRequirementKind.Dragons, 5), none).Refusal() != null &&
      new BalloonRoute(60, none, none).Refusal() == null && new BalloonRoute(50, new(BalloonRequirementKind.Treasure, 6000), new(BalloonRequirementKind.DragonEggs, 12)).Refusal() == null,
    "routes: only after a homeworld; nothing can be asked around the last place");
Check(new BalloonRequirement(BalloonRequirementKind.Dragons, 0).Refusal() != null && new BalloonRequirement(BalloonRequirementKind.Treasure, 30001).Refusal() != null &&
      new BalloonRequirement(BalloonRequirementKind.DragonEggs, 13).Refusal() != null && new BalloonRequirement(BalloonRequirementKind.Dragons, 80).Refusal() == null, "prices within what the game counts");
Check(NativeBalloonTravel.NewWorldDestination == -24228 && unchecked((uint)(0x8006EA4C + 16 * NativeBalloonTravel.NewWorldDestination)) == 0x8001000C, "the new world's landing is read from the unused world name");
foreach (var kind in Enum.GetValues<BalloonRequirementKind>())
foreach (int count in new[] { 1, 12, 255, 30000 })
{
    var asks = new BalloonRequirement(kind, count);
    if (asks.Refusal() != null) continue;
    var lines = NativeBalloonTravel.Lines(asks, "Gnasty's World");
    Check(lines.Count == 18 && lines.All(l => l.Length is > 0 and <= NativeBalloonTravel.MaxLine && l.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or ' ' or '\'' or '.' or ',' or '!')),
        $"{asks.Describe()}: six pages of three lines the speech box and the font can show");
    Check(lines[2] == "GNASTY'S WORLD." && lines[16] == "GNASTY'S WORLD" && (kind == BalloonRequirementKind.Nothing || lines[5].StartsWith(count.ToString()) && lines[8] == lines[5]),
        $"{asks.Describe()}: the lines name the world and the price");
}
var (landing, step) = NativeBalloonTravel.Arrival(16 * 4000, 16 * 3000, 16 * 500, 0);
Check(step == (64000, 48000) && landing == (64000 - 1024, 48000, 16 * 1022, 128), "the balloon comes down behind Spyro's start, from 522 above it");
// The game's own tables follow the same rule (Artisans: lands (84992, 46149), yaw 192; Spyro at (84992, 47173)).
Check((84992 - (int)Math.Round(Math.Cos(192 * Math.PI / 128) * 1024), 46149 - (int)Math.Round(Math.Sin(192 * Math.PI / 128) * 1024)) == (84992, 47173), "the game puts Spyro 64 units against the balloon's yaw");

// ---- the world's place as the project keeps it (new-world.json)
{
    var world = new Spyro.Editor.Core.Editing.NewWorldDocument(1, "SKY ISLES", "dreamweavers", [70, 71]);
    var auto = world.BalloonPlaceWith(true);
    Check(auto.After == "dreamweavers" && auto.ToWorld == none && auto.Onward == new BalloonRequirement(BalloonRequirementKind.Treasure, 6000) && auto.Route()!.AfterLevelId == 50,
        "a world with a balloonist and no place chosen follows its entry homeworld: free to fly there, the game's own price to fly on");
    Check(world.BalloonPlaceWith(false) == auto, "reached by balloon only (the default), it follows its entry homeworld even before its balloonist stands there (Create BIN insists on him)");
    Check((world with { Doorway = true }).BalloonPlaceWith(false) == Spyro.Editor.Core.Editing.WorldBalloonPlace.Default && (world with { Doorway = true }).BalloonPlaceWith(false).Route()!.AfterLevelId == 60,
        "with a doorway as well and no balloonist it is last on the route");
    var chosen = new Spyro.Editor.Core.Editing.WorldBalloonPlace("artisans", new(BalloonRequirementKind.Dragons, 3), new(BalloonRequirementKind.DragonEggs, 2), "PIP");
    string folder = Path.Combine(Path.GetTempPath(), "balloon-travel-smoke-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    try
    {
        (world with { Balloon = chosen }).SaveAsync(folder).GetAwaiter().GetResult();
        string json = File.ReadAllText(Spyro.Editor.Core.Editing.NewWorldDocument.PathFor(folder));
        var read = Spyro.Editor.Core.Editing.NewWorldDocument.Read(folder)!;
        Check(read.Balloon == chosen && read.BalloonPlaceWith(false) == chosen && json.Contains("\"Dragons\"") && json.Contains("\"DragonEggs\""), "the chosen place is saved in words and read back");
        world.SaveAsync(folder).GetAwaiter().GetResult();
        Check(!File.ReadAllText(Spyro.Editor.Core.Editing.NewWorldDocument.PathFor(folder)).Contains("balloon") && Spyro.Editor.Core.Editing.NewWorldDocument.Read(folder)!.Balloon == null, "a world with no place chosen is saved as before");
        bool refused = false;
        try { (world with { Balloon = chosen with { Balloonist = "pip 2" } }).SaveAsync(folder).GetAwaiter().GetResult(); } catch (InvalidDataException) { refused = true; }
        Check(refused, "a balloonist's name the game cannot write is refused");
    }
    finally { Directory.Delete(folder, true); }
    var off = new Spyro.Editor.Core.Editing.WorldBalloonPlace(Spyro.Editor.Core.Editing.WorldBalloonPlace.Off, none, none);
    Check(!off.OnRoute && off.Route() == null && off.Refusal() == null, "a world can be off the route");
    Check(Spyro.Editor.Core.Editing.WorldBalloonPlace.GamePrice("artisans") == new BalloonRequirement(BalloonRequirementKind.Dragons, 10) &&
          Spyro.Editor.Core.Editing.WorldBalloonPlace.GamePrice("magiccrafters") == new BalloonRequirement(BalloonRequirementKind.DragonEggs, 5) &&
          Spyro.Editor.Core.Editing.WorldBalloonPlace.GamePrice("gnastysworld") == none, "the game's own prices: 10 dragons, 1200 treasure, 5 eggs, 50 dragons, 6000 treasure, nothing");
}

// ---- how players reach the world (WorldAccess): by balloon only unless a doorway as well is chosen
{
    // A world saved before balloon-only worlds (the words of a real project's new-world.json): reached by balloon only.
    const string before25 = """
        {
          "schemaVersion": 1,
          "name": "SKY ISLES",
          "entryHomeworldKey": "artisans",
          "levelIds": [70, 71, 72, 73, 74],
          "balloon": {
            "after": "artisans",
            "toWorld": { "kind": "Treasure", "count": 50, "counter": 2147965024 },
            "onward": { "kind": "Nothing", "count": 0, "counter": 2147964752 },
            "balloonist": "NILS"
          }
        }
        """;
    string folder = Path.Combine(Path.GetTempPath(), "balloon-travel-access-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    try
    {
        File.WriteAllText(Spyro.Editor.Core.Editing.NewWorldDocument.PathFor(folder), before25);
        var old = Spyro.Editor.Core.Editing.NewWorldDocument.Read(folder)!;
        Check(old.BalloonOnly && !old.HasDoorway && !old.Doorway, "a world saved before is reached by balloon only (its doorway, if one was placed, is not built)");
        var place = old.BalloonPlaceWith(true);
        Check(place.After == "artisans" && place.ToWorld == new BalloonRequirement(BalloonRequirementKind.Treasure, 50) && place.Onward == none && place.Balloonist == "NILS" &&
              old.BalloonPlaceWith(false) == place, "its balloon price stays as the project has it: 50 treasure from Artisans, nothing to fly on");
        var named = old.NamedAfter("GLOOMY FEN");
        string Name(int id) => id switch { 10 => "Artisans", 20 => "Peace Keepers", 30 => "Magic Crafters", 40 => "Beast Makers", 50 => "Dream Weavers", _ => "Gnasty's World" };
        string said = Spyro.Editor.Core.Editing.WorldAccess.Describe(named, place, Name);
        Check(said == "Players reach GLOOMY FEN by balloon: Artisans' balloonist flies Spyro there once he has found 50 treasure, and GLOOMY FEN's balloonist NILS flies him back to Artisans (and on to Peace Keepers).",
            "one sentence says how players get there and back: " + said);
        named.SaveAsync(folder).GetAwaiter().GetResult();
        string json = File.ReadAllText(Spyro.Editor.Core.Editing.NewWorldDocument.PathFor(folder));
        Check(!json.Contains("doorway", StringComparison.OrdinalIgnoreCase) && !json.Contains("balloonOnly", StringComparison.OrdinalIgnoreCase) && !json.Contains("hasDoorway", StringComparison.OrdinalIgnoreCase) &&
              json.Contains("\"GLOOMY FEN\""), "a balloon-only world is saved as before, under its homeworld's name");
        var withDoorway = named with { Doorway = true };
        withDoorway.SaveAsync(folder).GetAwaiter().GetResult();
        var read = Spyro.Editor.Core.Editing.NewWorldDocument.Read(folder)!;
        Check(File.ReadAllText(Spyro.Editor.Core.Editing.NewWorldDocument.PathFor(folder)).Contains("\"doorway\": true") && read.Doorway && read.HasDoorway && !read.BalloonOnly,
            "a doorway as well is the user's choice, saved in words");
        Check(Spyro.Editor.Core.Editing.WorldAccess.Describe(read, place, Name).Contains("They can also walk in through its doorway in Artisans"), "the sentence says so");
        var offRoute = named with { Balloon = new(Spyro.Editor.Core.Editing.WorldBalloonPlace.Off, none, none) };
        Check(offRoute.HasDoorway && !offRoute.BalloonOnly && Spyro.Editor.Core.Editing.WorldAccess.Describe(offRoute, offRoute.Balloon!, Name).StartsWith("Players reach GLOOMY FEN through its doorway in Artisans"),
            "off the balloon route a doorway is the only way in");
        var last = new Spyro.Editor.Core.Editing.WorldBalloonPlace("gnastysworld", none, none, "NILS");
        Check(Spyro.Editor.Core.Editing.WorldAccess.Describe(named, last, Name).StartsWith("Players reach GLOOMY FEN by balloon at the end of the route: once Gnasty's World's balloonist"), "last on the route: the sentence says so");
        var dragons = new Spyro.Editor.Core.Editing.WorldBalloonPlace("beastmakers", none, new(BalloonRequirementKind.Dragons, 50), "PIP");
        Check(Spyro.Editor.Core.Editing.WorldAccess.Describe(named, dragons, Name) == "Players reach GLOOMY FEN by balloon: Beast Makers' balloonist flies Spyro there whenever he asks, and GLOOMY FEN's balloonist PIP flies him back to Beast Makers (and on to Dream Weavers once he has rescued 50 dragons).",
            "free there, a price to fly on");
        Check(old.NamedAfter("SKY ISLES") == old && old.NamedAfter(null) == old && old.NamedAfter("A NAME TOO LONG HERE") == old && old.NamedAfter("FEN 2") == old && old.NamedAfter("gloomy fen") == old &&
              old.NamedAfter("O'MALLEY'S BOG").Name == "O'MALLEY'S BOG", "the world takes its homeworld's name only when the game can show it (15 capital letters, spaces, apostrophes)");
    }
    finally { Directory.Delete(folder, true); }

    // The way in and out Create BIN builds (NativeNewWorldExport.WayInRefusal), before anything is built.
    var catalog = new LevelCatalog([new LevelDefinition { Key = "artisans", DisplayName = "Artisans", LevelId = 10, SourceWadEntry = 12 },
        new LevelDefinition { Key = "peacekeepers", DisplayName = "Peace Keepers", LevelId = 20, SourceWadEntry = 26 }]);
    var artisans = catalog.FindByKey("artisans")!;
    var balloonOnly = new Spyro.Editor.Core.Editing.NewWorldDocument(1, "GLOOMY FEN", "artisans", [70, 71]);
    var doorway = balloonOnly with { Doorway = true };
    Spyro.Editor.Core.Editing.AuthoredPortal Door(int to) => new(to, new(4000, 4000, 400), 0);
    var bare = Spyro.Editor.Core.Editing.SceneDocument.New("GLOOMY FEN") with { WorldLevelId = 70, Portals = [Door(71)] };
    var wayBack = bare with { Portals = [Door(71), Door(10)] };
    var entrance = new Spyro.Editor.Core.Editing.WorldEntranceDocument(1, "artisans", new(5888, 2240, 410), 240, "stone-arch", 1);
    Exception? Why(Spyro.Editor.Core.Editing.NewWorldDocument w, Spyro.Editor.Core.Editing.SceneDocument home, bool balloonist, Spyro.Editor.Core.Editing.WorldEntranceDocument? e) =>
        NativeNewWorldExport.WayInRefusal(w, home, balloonist, e, artisans, catalog);
    Check(Why(balloonOnly, bare, true, null) == null && Why(balloonOnly, bare, true, entrance) == null,
        "by balloon only: a homeworld with its balloonist and no doorway back out is built (a doorway placed before is left in the project)");
    Check(Why(balloonOnly, wayBack, true, entrance) is { } back && ExportRepair.Find(back) is ExportRepair.RemoveWorldWayBack && back.Message.Contains("still has a doorway back out to Artisans"),
        "by balloon only: a doorway back out left from before stops Create BIN, and Fix It For Me takes it out");
    Check(Why(balloonOnly, bare, false, null) is { } alone && ExportRepair.Find(alone) is ExportRepair.PlaceWorldBalloonist && alone.Message.Contains("has no balloonist"),
        "by balloon only: a homeworld without a balloonist stops Create BIN (Spyro could not fly back), and Fix It For Me stands one");
    Check(Why(doorway, wayBack, false, entrance) == null, "with a doorway as well: the entrance and the way back go together");
    Check(Why(doorway, bare, true, entrance) is { } noBack && ExportRepair.Find(noBack) == null && noBack.Message.Contains("needs a doorway back to Artisans"), "with a doorway: an entrance without a way back is refused");
    Check(Why(doorway, wayBack, true, null) is { } noIn && ExportRepair.Find(noIn) is ExportRepair.PlaceWorldEntrance { HomeworldKey: "artisans" }, "with a doorway: a way back without its entrance gets the entrance placed");
    Check(Why(doorway, bare with { Portals = [Door(71), Door(20)] }, true, entrance) is { } wrong && wrong.Message.Contains("leads to Peace Keepers"), "with a doorway: a way back to another homeworld is refused");

    // Whether the homeworld has its balloonist (the game's six kinds, not deleted).
    Spyro.Editor.Core.Scene.Moby Thing(int cls, bool removed = false) => new() { SourceByte36 = cls & 0xFF, SourceByte37 = cls >> 8, IsAdded = true, IsRemoved = removed, Label = "thing" };
    Spyro.Editor.Core.Editing.SceneDocument With(params Spyro.Editor.Core.Scene.Moby[] things) => bare with { Objects = [.. things.Select(t => new Spyro.Editor.Core.Editing.AuthoredObject(Guid.NewGuid().ToString("N"), t))] };
    Check(Spyro.Editor.Core.Editing.WorldAccess.HasBalloonist(With(Thing(0xBB), Thing(0x1A0))) && Spyro.Editor.Core.Editing.WorldAccess.HasBalloonist(With(Thing(0xC0))) &&
          !Spyro.Editor.Core.Editing.WorldAccess.HasBalloonist(With(Thing(0xBB, removed: true), Thing(0x1A0))) && !Spyro.Editor.Core.Editing.WorldAccess.HasBalloonist(With(Thing(0xC1), Thing(0x1BB))),
        "a balloonist is one of the game's six (0xBB-0xC0) that is not deleted");
    Check(Spyro.Editor.Core.Editing.WorldAccess.WaysBack(wayBack).Single().DestinationLevelId == 10 && Spyro.Editor.Core.Editing.WorldAccess.WaysBack(bare).Count == 0, "the doorways back out are those not to the world's own levels");

    // Where a balloonist is stood: level, open floor Spyro walks to, near where he arrives, clear of objects, doorways,
    // water and roofs, facing where he arrives.
    Spyro.Editor.Core.Editing.AuthoredSurface Tile(float x, float y, float z, float size = 256, Spyro.Editor.Core.Editing.AuthoredSurfaceKind? kind = null) =>
        new(Guid.NewGuid().ToString("N"), [new(x, y, z), new(x + size, y, z), new(x + size, y + size, z), new(x, y + size, z)], new(128, 128, 128), null) { Kind = kind };
    List<Spyro.Editor.Core.Editing.AuthoredSurface> floor = [];
    for (int i = 0; i < 12; i++) for (int j = 0; j < 12; j++) floor.Add(Tile(3000 + 256 * i, 3000 + 256 * j, 400));
    var meadow = Spyro.Editor.Core.Editing.SceneDocument.New("MEADOW") with { WorldLevelId = 70, Surfaces = floor, Spawn = new(4536, 4536, 400), Heading = 0 };
    var spot = Spyro.Editor.Core.Editing.WorldAccess.BalloonistSpot(meadow);
    double D(Spyro.Editor.Core.Primitives.Vector3f a, Spyro.Editor.Core.Primitives.Vector3f b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    Check(spot is { } s0 && D(s0.Position, meadow.Spawn) is >= 256 and <= 600 && Math.Abs(s0.Position.Z - 400) < 1 && s0.Position.X > meadow.Spawn.X && s0.Position.Y > meadow.Spawn.Y &&
          s0.Yaw == LevelFillFacing(s0.Position, meadow.Spawn), $"on an open meadow: a few hundred units ahead of Spyro and to his left, on the floor, facing him ({spot?.Position}, yaw {spot?.Yaw})");
    // Water ahead and to the left, a doorway and an object on the other side: he stands clear of all of them.
    var pond = floor.Where(f => f.Vertices[0].X >= 4536 && f.Vertices[0].Y >= 4536).Select(f => f with { Kind = Spyro.Editor.Core.Editing.AuthoredSurfaceKind.Water }).ToList();
    var wet = meadow with { Surfaces = [.. floor.Where(f => !(f.Vertices[0].X >= 4536 && f.Vertices[0].Y >= 4536)), .. pond], Portals = [new(71, new(4536, 4100, 400), 0)],
        Objects = [new(Guid.NewGuid().ToString("N"), new Spyro.Editor.Core.Scene.Moby { SourceByte36 = 0x54, IsAdded = true, Label = "gem", Position = new(4900, 4536, 400) })] };
    var s1 = Spyro.Editor.Core.Editing.WorldAccess.BalloonistSpot(wet);
    Check(s1 is { } p1 && !(p1.Position.X >= 4536 - 32 && p1.Position.Y >= 4536 - 32) && D(p1.Position, new(4536, 4100, 400)) >= 320 && D(p1.Position, new(4900, 4536, 400)) >= 160 && D(p1.Position, wet.Spawn) >= 256,
        $"not in the water, not in front of a doorway, not on an object ({s1?.Position})");
    // A roof over the meadow (a floor 200 above everything but a corner): only the open corner is left.
    var roofed = meadow with { Surfaces = [.. floor, .. floor.Where(f => f.Vertices[0].X < 5500 || f.Vertices[0].Y < 5500).Select(f => Tile(f.Vertices[0].X, f.Vertices[0].Y, 600))] };
    var s2 = Spyro.Editor.Core.Editing.WorldAccess.BalloonistSpot(roofed);
    Check(s2 == null || s2.Position.X >= 5500 + 96 && s2.Position.Y >= 5500 + 96, $"no balloon under a roof ({s2?.Position})");
    Check(Spyro.Editor.Core.Editing.WorldAccess.BalloonistSpot(meadow with { Surfaces = [Tile(4400, 4400, 400, 300)] }) == null, "no place on a floor too small for him and his balloon");
}
static int LevelFillFacing(Spyro.Editor.Core.Primitives.Vector3f from, Spyro.Editor.Core.Primitives.Vector3f to) => Spyro.Editor.Core.Editing.LevelFill.Facing(from, to);

// ---- what a copied creature gives (UniversalRewardChoice): another gem in place of its own, nothing else
{
    byte[] row = new byte[0x58]; row[0x53] = 0x54;
    Check(UniversalRewardChoice.Allows(row, 0x53, 0x57) && UniversalRewardChoice.Allows(row, 0x53, 0x53), "a creature that gives a gem may give any of the five");
    Check(!UniversalRewardChoice.Allows(row, 0x53, 0xFF) && !UniversalRewardChoice.Allows(row, 0x53, 0x58) && !UniversalRewardChoice.Allows(row, 0x52, 0x57) && !UniversalRewardChoice.Allows(row, 0x50, 0x55),
        "not nothing, not another kind of object, and no other byte of its row");
    row[0x53] = 0xFF;
    Check(!UniversalRewardChoice.Allows(row, 0x53, 0x55), "a creature that gives nothing keeps that");
    Check(Enumerable.Range(0, 256).Count(UniversalRewardChoice.IsGem) == 5 && Spyro.Editor.Core.Scene.GemValue.Known.All(g => UniversalRewardChoice.IsGem(g.IdByte)), "the five gems are the reward routine's five");
}

// ---- a balloonist's decision code, run for every state of the game (a synthetic overlay: the call that begins the talk,
// then room as long as the game's own code, ending in the jump out)
byte[] Synthetic(int words, out NativeBalloonTravel.Decision d)
{
    byte[] o = new byte[0x400];
    uint[] head = [0x3C028007, 0x8C4257A0, 0, 0x0040F809, 0x02602021];
    for (int i = 0; i < head.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x100 + 4 * i), head[i]);
    uint exit = UniversalOverlay.Base + 0x300;
    BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x114 + 4 * (words - 2)), 0x08000000 | (exit >> 2 & 0x03FFFFFF));
    d = NativeBalloonTravel.Decisions(o).Single();
    return o;
}
{
    byte[] o = Synthetic(58, out var d58);
    Check(d58 == new NativeBalloonTravel.Decision(UniversalOverlay.Base + 0x114, 58, UniversalOverlay.Base + 0x300), "a balloonist's decision code is found by the call that begins his talk");
    Synthetic(15, out var d15);
    Check(d15.Words == 15, "Gnasty's World's balloonist has fifteen instructions");
}
int runs = 0;
foreach (var asks in new[] { new BalloonRequirement(BalloonRequirementKind.Dragons, 40), new BalloonRequirement(BalloonRequirementKind.Treasure, 30000), new BalloonRequirement(BalloonRequirementKind.DragonEggs, 5), none })
foreach (int position in new[] { 0, 1, 3, 5 })
foreach (bool own in new[] { false, true })
{
    if (own && position == 0) continue;
    byte[] o = Synthetic(58, out var d);
    int[] opens = own ? [] : [.. Enumerable.Range(1, position)];
    int pages = position == 0 ? 0 : 6;
    NativeBalloonTravel.WriteDecision(o, d, position, opens, asks, pages, own);
    foreach (int have in new[] { 0, asks.Count - 1, asks.Count, asks.Count + 7 })
    foreach (int flag in new[] { 0, 1, 2 })
    foreach (int before in new[] { 0, 2 })
    {
        if (have < 0) continue;
        var cpu = new Cpu(o);
        cpu.W32(asks.Counter, (uint)have);
        for (int p = 1; p <= 6; p++) cpu.Mem[Flags + (uint)p - 1] = (byte)(p <= position ? before : 0);
        cpu.Mem[Flags + (uint)position] = (byte)flag;                 // position + 1's byte
        cpu.Rand = 7;
        cpu.Run(d.Start, d.Exit);
        bool met = asks.Kind == BalloonRequirementKind.Nothing || have >= asks.Count;
        int page = (int)cpu.R32(Page), after = cpu.R8(Flags + (uint)position);
        int wantPage = flag == 2 && met ? 0x1E : met ? pages + (flag == 0 ? 3 : 4) : pages + (flag == 0 ? 0 : 2);
        int wantFlag = met ? 2 : flag == 0 ? 1 : flag;
        Check(page == wantPage && after == wantFlag, $"{asks.Describe()} at position {position} with {have}, byte {flag}: page {page} (wanted {wantPage}), byte {after} (wanted {wantFlag})");
        Check(page != 0x1E || cpu.R32(Greeting) == 7 % 3, "the menu opens under one of the three greetings");
        for (int p = 1; p <= position; p++)
        {
            int want = !own ? 2 : p == position && met && flag != 2 ? 2 : before;
            Check(cpu.R8(Flags + (uint)p - 1) == want, $"position {p}'s byte is {(want == 2 ? "open" : "left alone")} ({(own ? "the new world's balloonist" : "a balloonist of the game")})");
        }
        runs++;
    }
}
// The last balloonist on the route: the menu, in fifteen instructions, opening what he is told to.
{
    byte[] o = Synthetic(15, out var d);
    NativeBalloonTravel.WriteDecision(o, d, 5, [1, 2, 3, 4, 5, 6], null, 0, false);
    var cpu = new Cpu(o); cpu.Run(d.Start, d.Exit);
    Check(cpu.R32(Page) == 0x1E && Enumerable.Range(0, 6).All(i => cpu.R8(Flags + (uint)i) == 2), "Gnasty's World's balloonist opens every place and shows the menu");
    bool refused = false;
    try { NativeBalloonTravel.WriteDecision(Synthetic(15, out var small), small, 5, [1, 2, 3, 4, 5], new(BalloonRequirementKind.Dragons, 5), 6, false); } catch (InvalidDataException) { refused = true; }
    Check(refused, "a price does not fit Gnasty's World's balloonist");
}
Console.WriteLine($"decision code run {runs} times");

if (source != null)
{
    string root = AppContext.BaseDirectory;
    while (!File.Exists(Path.Combine(root, "spyro-level-catalog.json"))) root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("No level catalog above the smoke.");
    var catalog = LevelCatalog.Load(root);
    var library = new UniversalSourceLibrary(source, catalog);
    byte[] exe = library.Disc.Exe;
    var homes = catalog.Levels.Where(l => l.LevelId is 10 or 20 or 30 or 40 or 50 or 60).OrderBy(l => l.LevelId).ToArray();
    Check(homes.Length == 6, "the six homeworlds");
    var town = catalog.Levels.Single(l => l.LevelId == 13);
    (byte[] Overlay, uint Tick, uint Render) Routines(LevelDefinition level)
    {
        var bindings = UniversalExeBindings.Read(exe, level.LevelId);
        return (library.Disc.EntryBytes(level.SourceWadEntry - 1), bindings[NativeBalloonTravel.TickGlobal].Value, bindings[NativeBalloonTravel.RenderGlobal].Value);
    }
    foreach (var level in homes.Append(town))
    {
        var (o, tick, render) = Routines(level);
        Check(NativeBalloonTravel.HasRoutines(o, tick, render), $"{level.DisplayName}'s overlay holds the two balloonist routines, word for word the game's");
        var found = NativeBalloonTravel.Decisions(o);
        Check(found.Count == (level.LevelId == 13 ? 0 : 1), $"{level.DisplayName}: {found.Count} balloonist");
        if (found.Count == 1) Console.WriteLine($"{level.DisplayName}: decision code at 0x{found[0].Start:X8}, {found[0].Words} instructions, leaves to 0x{found[0].Exit:X8}");
    }
    int[] sizes = homes.Select(h => NativeBalloonTravel.Decisions(Routines(h).Overlay).Single().Words).ToArray();
    Check(sizes.Take(5).All(n => n >= 55) && sizes[5] == 15, "five balloonists have room for a price; Gnasty's World's has fifteen instructions");
    // The game's own decision code, run: the page it opens and the byte it sets, for each world.
    for (int w = 0; w < 5; w++)
    {
        var (o, _, _) = Routines(homes[w]);
        var d = NativeBalloonTravel.Decisions(o).Single();
        var asks = NativeBalloonTravel.Retail[w];
        foreach (int have in new[] { 0, asks.Count - 1, asks.Count })
        foreach (int flag in new[] { 0, 1, 2 })
        {
            var cpu = new Cpu(o); cpu.W32(asks.Counter, (uint)have); cpu.Mem[Flags + (uint)w + 1] = (byte)flag; cpu.Reg[30] = 1; cpu.Rand = 5;
            cpu.Run(d.Start, d.Exit);
            bool met = have >= asks.Count;
            int wantPage = flag == 2 && met ? 0x1E : met ? 6 * w + (flag == 0 ? 3 : 4) : 6 * w + (flag == 0 ? 0 : 2);
            Check((int)cpu.R32(Page) == wantPage && cpu.R8(Flags + (uint)w + 1) == (met ? 2 : flag == 0 ? 1 : flag), $"{homes[w].DisplayName}'s own balloonist asks to {asks.Describe()} (with {have}, byte {flag}: page {cpu.R32(Page)})");
        }
    }

    int flights = 0;
    foreach (int after in new[] { 10, 20, 30, 40, 50, 60 })
    {
        var route = new BalloonRoute(after, after == 60 ? none : new(BalloonRequirementKind.Treasure, 777), after == 60 ? none : new(BalloonRequirementKind.Dragons, 33));
        int[] order = NativeBalloonTravel.Order(route);
        string[] worldNames = ["ARTISANS", "PEACE KEEPERS", "MAGIC CRAFTERS", "BEAST MAKERS", "DREAM WEAVERS", "GNASTY'S WORLD", "NEW WORLD"];
        // Every homeworld as the game has it, then again as the editor rebuilds it (Town Square's copy of the routines
        // with a balloonist copied in: Peace Keepers' own in Artisans' place and Artisans' in the others, to show that a
        // rebuilt homeworld's balloonist is given the place he stands in whoever he is).
        foreach (var (id, rebuiltHere) in order.Select(i => (i, false)).Concat(order.Where(i => i != 70).Select(i => (i, true))))
        {
            // The new world's homeworld runs Town Square's copy of the routines (a compiled level), with a balloonist
            // copied from Artisans.
            bool compiledHere = id == 70 || rebuiltHere;
            var level = compiledHere ? town : homes[id / 10 - 1];
            var (o, tick, render) = Routines(level);
            if (compiledHere)
            {
                // A balloonist's code appended where the overlay has room.
                var (ao, _, _) = Routines(homes[rebuiltHere && id == 10 ? 1 : 0]);
                var ad = NativeBalloonTravel.Decisions(ao).Single();
                int at = o.Length - 0x200;
                ao.AsSpan((int)(ad.Start - UniversalOverlay.Base) - 20, 20 + 4 * ad.Words).CopyTo(o.AsSpan(at));
            }
            var home = new NativeBalloonTravel.Homeworld(id, tick, render, compiledHere, id == 70 ? (123456, 65432) : null, "NEW WORLD", id == 70 ? "NILS" : null);
            var applied = NativeBalloonTravel.Apply(o, home, route);
            int q = Array.IndexOf(order, id);
            Check(applied.Position == q && applied.Balloonists == 1, $"after {after}: {level.DisplayName} stands at position {q}");
            Console.WriteLine($"after {after}: level {id}{(rebuiltHere ? " (rebuilt)" : "")} at position {q}: {applied.Decision}");
            // Every state of the route's six bytes that the game can reach: open as far as position n.
            for (int open = 0; open <= 6; open++)
            {
                Cpu Fresh()
                {
                    var cpu = new Cpu(o) { Exe = exe };
                    for (int p = 1; p <= 6; p++) cpu.Mem[Flags + (uint)p - 1] = (byte)(p <= open ? 2 : 0);
                    return cpu;
                }
                // The worlds listed: positions 0..open in route order, without this homeworld.
                int[] listed = order.Take(open + 1).Where(l => l != id).ToArray();
                // 1. How many rows the menu has.
                var count = Fresh(); count.Run(tick + 0xB04, tick + 0xB34);
                Check(count.Reg[3] == listed.Length + 1, $"after {after}, in {id}, open to {open}: {count.Reg[3]} menu rows (Stay Here and {listed.Length} worlds)");
                // 2. The rows drawn: each world's name, in route order.
                var draw = Fresh(); draw.W32(0x80075710, 0x80100000); draw.Reg[18] = 1;
                List<uint> names = [];
                draw.Watch = (pc, cpu) => { if (pc == render + 0x52C) names.Add(cpu.Reg[4]); };
                draw.Run(render + 0x4A0, render + 0x5C0);
                Check(names.Select(NameIndex).SequenceEqual(listed.Select(l => l / 10 - 1)),
                    $"after {after}, in {id}, open to {open}: the menu lists {string.Join(", ", listed.Select(l => worldNames[l / 10 - 1]))}");
                Check(draw.Reg[18] == listed.Length + 1, "the menu's box is as tall as its rows");
                // 3. Each row flies to its world.
                for (int row = 1; row <= listed.Length; row++)
                {
                    var pick = Fresh(); pick.Reg[3] = (uint)row;
                    pick.Run(tick + 0xC2C, tick + 0x1F80);
                    int to = (int)pick.R32(NextLevel), index = (int)pick.R32(Destination);
                    Check(to == listed[row - 1] && index == (to == 70 ? NativeBalloonTravel.NewWorldDestination : to / 10 - 1), $"after {after}, in {id}: row {row} flies to level {to} (landing {index})");
                    flights++;
                }
            }
            // 4. "Are you ready to go?": the route's next world and its name.
            if (q < 6)
            {
                var ready = new Cpu(o) { Exe = exe };
                ready.Run(tick + 0xDA0, tick + 0x1F80);
                Check((int)ready.R32(NextLevel) == order[q + 1], $"after {after}: {id}'s balloonist first flies on to {order[q + 1]}");
                var name = new Cpu(o) { Exe = exe };
                name.Reg[2] = 0x66660000;                                   // the branch before sets the multiplier's top half
                uint shown = 0;
                name.Watch = (pc, cpu) => { if (pc == render + 0x830) shown = cpu.Reg[6]; };
                name.Run(render + 0x800, render + 0x834);
                Check(NameIndex(shown) == order[q + 1] / 10 - 1, $"after {after}: that page names {worldNames[order[q + 1] / 10 - 1]}");
            }
            // 5. The balloonist's decision code in its place: the byte he opens is the next position's.
            var d = NativeBalloonTravel.Decisions(o).Single();
            if (q < 6 && !(id == 60 && after == 60))
            {
                var asks = id == 70 ? route.Onward : id == after ? route.ToWorld : NativeBalloonTravel.Retail[id / 10 - 1];
                var cpu = new Cpu(o) { Exe = exe }; cpu.W32(asks.Counter, 30000); cpu.Reg[30] = 1;
                for (int p = 1; p <= q; p++) cpu.Mem[Flags + (uint)p - 1] = 2;
                cpu.Run(d.Start, d.Exit);
                Check(cpu.R8(Flags + (uint)q) == 2 && Enumerable.Range(q + 2, 6 - q - 1 < 0 ? 0 : 6 - q - 1).All(p => cpu.R8(Flags + (uint)p - 1) == 0),
                    $"after {after}: {id}'s balloonist opens position {q + 1} and none beyond");
                int page = (int)cpu.R32(Page);
                Check(page % 6 == 3 && page < 30, $"after {after}: {id}'s balloonist greets Spyro with his met-at-once page ({page})");
                Check(!rebuiltHere || applied.Decision.Contains("own lines") || page == 6 * (id / 10 - 1) + 3, $"after {after}: rebuilt {id}'s balloonist speaks that homeworld's own pages ({page})");
                var poor = new Cpu(o) { Exe = exe }; poor.Reg[30] = 1; poor.Run(d.Start, d.Exit);
                Check(asks.Kind == BalloonRequirementKind.Nothing || poor.R8(Flags + (uint)q) == 1, $"after {after}: {id}'s balloonist waits for Spyro to {asks.Describe()}");
            }
            else
            {
                var cpu = new Cpu(o) { Exe = exe }; cpu.Reg[30] = 2; cpu.Run(d.Start, d.Exit);
                Check(cpu.R32(Page) == 0x1E, $"after {after}: the last balloonist ({id}) shows the menu");
                Check(id != 60 || Enumerable.Range(0, 6).All(i => cpu.R8(Flags + (uint)i) == 2) || after != 60, "Gnasty's World's balloonist opens the new world after it");
            }
            // 6. His own lines, where he has them: read back through the changed routine's tables.
            if (applied.Decision.Contains("own lines"))
            {
                var asks = id == 70 ? route.Onward : route.ToWorld;
                var want = NativeBalloonTravel.Lines(asks, id == 70 ? worldNames[order[q + 1] / 10 - 1] : "NEW WORLD");
                int pages = q == 0 ? 0 : 6;
                uint table = Address(o, render + 0x1C4, render + 0x1CC), lines = Address(o, render + 0x1F4, render + 0x1F8);
                List<string> got = [];
                for (int p = pages; p < pages + 6; p++)
                    for (int l = o[(int)(table + (uint)p - UniversalOverlay.Base)]; l < o[(int)(table + (uint)p + 1 - UniversalOverlay.Base)]; l++)
                        got.Add(Text(o, BinaryPrimitives.ReadUInt32LittleEndian(o.AsSpan((int)(lines + 4 * (uint)l - UniversalOverlay.Base)))));
                Check(got.SequenceEqual(want), $"after {after}: {id}'s balloonist says \"{string.Join(" / ", want.Skip(3).Take(3))}\"");
                Check(id != 70 || Text(o, Address(o, render + 0x70, render + 0x74)) == "NILS", "the new world's balloonist has his own name in the heading");
            }
            if (id == 70)
            {
                var land = new Cpu(o) { Exe = exe };
                land.Reg[16] = 0x800777EC; land.Run(tick + 0x1CD0, tick + 0x1D00);
                Check(land.R32(0x8007780C) == 123456 && land.Reg[3] == 65432, "in the new world Spyro steps off the balloon at its own spot");
            }
        }
        // The executable's part.
        byte[] patched = exe.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(patched.AsSpan(Off(0x8006F7D0 + 28)), 0x80060000);     // the window's name pool (stands in)
        int keptPages = after == 60 ? -1 : 6 * (after / 10 - 1);
        NativeBalloonTravel.PatchExecutable(patched, (1, 2, 3, 4), keptPages);
        Check(BinaryPrimitives.ReadUInt32LittleEndian(patched.AsSpan(Off(0x80012688))) == 0x24020000 && BinaryPrimitives.ReadUInt32LittleEndian(patched.AsSpan(Off(0x8001000C + 8))) == 3,
            "a new game starts with every place closed; the new world's landing is stored");
        if (keptPages >= 0)
        {
            // The invitation page of the balloonist before the new world, read as the routine reads it.
            int firstLine = patched[Off(0x8006F89C) + keptPages + 5];
            string Line(int i)
            {
                uint at = BinaryPrimitives.ReadUInt32LittleEndian(patched.AsSpan(Off(0x8006F8C4 + 4 * (uint)(firstLine + i))));
                return at == 0x80060000 ? "(the new world's name)" : Encoding.ASCII.GetString(patched, Off(at), Array.IndexOf(patched, (byte)0, Off(at)) - Off(at));
            }
            Check(Line(0) == "HOP ABOARD FOR THE" && Line(1) == "(the new world's name)" && Line(2) == "IF YOU ARE READY.", $"after {after}: the balloonist's invitation names the new world");
        }
        bool early = false;
        try { NativeBalloonTravel.PatchExecutable(exe.ToArray(), (1, 2, 3, 4), -1); } catch (InvalidDataException) { early = true; }
        Check(early, "the route's executable part waits for the level window");
    }
    Console.WriteLine($"{flights} menu rows flown");
    // Off the route, a balloonist in the new world's homeworld: the game's menu under his own name; none there: nothing.
    {
        var (o, tick, render) = Routines(town);
        Check(!NativeBalloonTravel.ApplyOffRoute(o.ToArray(), tick, render, "PIP"), "off the route with no balloonist there is nothing to change");
        var (ao, _, _) = Routines(homes[0]);
        var ad = NativeBalloonTravel.Decisions(ao).Single();
        ao.AsSpan((int)(ad.Start - UniversalOverlay.Base) - 20, 20 + 4 * ad.Words).CopyTo(o.AsSpan(o.Length - 0x200));
        Check(NativeBalloonTravel.ApplyOffRoute(o, tick, render, "PIP"), "off the route a balloonist in the world's homeworld is found");
        var d = NativeBalloonTravel.Decisions(o).Single();
        var cpu = new Cpu(o) { Exe = exe }; cpu.Run(d.Start, d.Exit);
        Check(cpu.R32(Page) == 0x1E && Enumerable.Range(0, 6).All(i => cpu.R8(Flags + (uint)i) == 0) && Text(o, Address(o, render + 0x70, render + 0x74)) == "PIP",
            "he shows the menu, opens nothing, and is headed with his own name");
        // The game's own menu routine is untouched: its rows fly to level id = row x 10.
        var pick = new Cpu(o) { Exe = exe }; pick.Reg[3] = 3; pick.W32(0x8007596C, 70); pick.Reg[16] = 0x800777EC; pick.W32(0x800777F0, 3);
        pick.Run(tick + 0xC18, tick + 0x1F80);
        Check(pick.R32(NextLevel) == 30, "his menu's third row flies to Magic Crafters as in the game");
    }
    // Gnasty's World's balloonist cannot stand where a price is asked.
    {
        var (o, tick, render) = Routines(town);
        var (go, _, _) = Routines(homes[5]);
        var gd = NativeBalloonTravel.Decisions(go).Single();
        go.AsSpan((int)(gd.Start - UniversalOverlay.Base) - 20, 20 + 4 * gd.Words).CopyTo(o.AsSpan(o.Length - 0x200));
        string? why = null;
        try { NativeBalloonTravel.Apply(o, new(70, tick, render, true, (1, 2), "NEW WORLD", "NILS"), new BalloonRoute(10, none, new(BalloonRequirementKind.Dragons, 10))); } catch (InvalidDataException e) { why = e.Message; }
        Check(why != null && why.Contains("Gnasty's World's"), "Gnasty's World's balloonist in the new world is refused where a price is asked: " + why);
    }
    // A homeworld the editor rebuilt: the game's landing and step-off tables take its own start.
    byte[] moved = exe.ToArray();
    NativeBalloonTravel.PatchArrival(moved, 30, (11, 22, 33, 44), (55, 66));
    Check(BinaryPrimitives.ReadInt32LittleEndian(moved.AsSpan(Off(0x8006EA4C + 32))) == 11 && BinaryPrimitives.ReadInt32LittleEndian(moved.AsSpan(Off(0x8006EADC + 16 + 4))) == 66, "a rebuilt homeworld's landing replaces the game's");
    Console.WriteLine($"BALLOON_TRAVEL_SOURCE_OK checks={checks}");

    int NameIndex(uint pointer)
    {
        // Which world-name slot holds this pointer (slot w + 1 is world w); the seventh is the new world's.
        for (int w = 0; w < 7; w++) if (BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(0x8006F7D4 + 4 * (uint)w))) == pointer) return w;
        return -1;
    }
}
else
    Console.WriteLine($"BALLOON_TRAVEL_FREE_OK checks={checks}");
return 0;

static int Off(uint address) => (int)(address - UniversalExeBindings.ExeBase);
static uint Address(byte[] o, uint lui, uint low) =>
    (uint)(((BinaryPrimitives.ReadUInt32LittleEndian(o.AsSpan((int)(lui - UniversalOverlay.Base))) & 0xFFFF) << 16) + (short)(BinaryPrimitives.ReadUInt32LittleEndian(o.AsSpan((int)(low - UniversalOverlay.Base))) & 0xFFFF));
static string Text(byte[] o, uint address)
{
    int at = (int)(address - UniversalOverlay.Base), end = Array.IndexOf(o, (byte)0, at);
    return Encoding.ASCII.GetString(o, at, end - at);
}

/// <summary>Runs MIPS I code from an overlay image (delay slots and load delays as the console has them); memory outside
/// the overlay is a sparse byte map (0 where never written) over the executable when one is given.</summary>
sealed class Cpu(byte[] overlay)
{
    public uint[] Reg = new uint[32];
    public Dictionary<uint, byte> Mem = [];
    public byte[]? Exe;
    public uint Rand;
    public Action<uint, Cpu>? Watch;
    private uint _hi, _lo;

    public byte R8(uint a)
    {
        if (Mem.TryGetValue(a, out byte b)) return b;
        if (a >= UniversalOverlay.Base && a < UniversalOverlay.Base + (uint)overlay.Length) return overlay[(int)(a - UniversalOverlay.Base)];
        if (Exe != null && a >= 0x80010000 && a - UniversalExeBindings.ExeBase < (uint)Exe.Length) return Exe[(int)(a - UniversalExeBindings.ExeBase)];
        return 0;
    }
    public uint R32(uint a) => (uint)(R8(a) | R8(a + 1) << 8 | R8(a + 2) << 16 | R8(a + 3) << 24);
    public void W32(uint a, uint v) { for (int i = 0; i < 4; i++) Mem[a + (uint)i] = (byte)(v >> (8 * i)); }

    /// <summary>Runs from <paramref name="pc"/> until control reaches <paramref name="stop"/>.</summary>
    public void Run(uint pc, uint stop)
    {
        uint next = pc + 4;
        (int Reg, uint Value)? pending = null;
        for (int steps = 0; pc != stop; steps++)
        {
            if (steps > 20000) throw new InvalidOperationException($"The code did not reach 0x{stop:X8} (at 0x{pc:X8}).");
            uint w = R32(pc), after = next + 4;
            int op = (int)(w >> 26), rs = (int)(w >> 21 & 31), rt = (int)(w >> 16 & 31), rd = (int)(w >> 11 & 31), sh = (int)(w >> 6 & 31), fn = (int)(w & 63);
            uint imm = w & 0xFFFF, simm = (uint)(short)imm, a = Reg[rs], b = Reg[rt];
            (int Reg, uint Value)? load = null;
            void Set(int r, uint v) { if (r != 0) Reg[r] = v; if (pending is { } p && p.Reg == r) pending = null; }
            uint Target() => pc + 4 + (simm << 2);
            switch (op)
            {
                case 0:
                    switch (fn)
                    {
                        case 0x00: Set(rd, b << sh); break;
                        case 0x02: Set(rd, b >> sh); break;
                        case 0x03: Set(rd, (uint)((int)b >> sh)); break;
                        case 0x04: Set(rd, b << (int)(a & 31)); break;
                        case 0x06: Set(rd, b >> (int)(a & 31)); break;
                        case 0x08: after = a; break;
                        case 0x09: Set(rd, pc + 8); after = a; break;
                        case 0x10: Set(rd, _hi); break;
                        case 0x12: Set(rd, _lo); break;
                        case 0x18: { long m = (long)(int)a * (int)b; _hi = (uint)(m >> 32); _lo = (uint)m; break; }
                        case 0x21: Set(rd, a + b); break;
                        case 0x23: Set(rd, a - b); break;
                        case 0x24: Set(rd, a & b); break;
                        case 0x25: Set(rd, a | b); break;
                        case 0x2A: Set(rd, (int)a < (int)b ? 1u : 0); break;
                        case 0x2B: Set(rd, a < b ? 1u : 0); break;
                        default: throw new InvalidOperationException($"Unknown instruction 0x{w:X8} at 0x{pc:X8}.");
                    }
                    break;
                case 2: after = (pc + 4 & 0xF0000000) | (w & 0x03FFFFFF) << 2; break;
                case 3:
                    // A call into the game: only the random-number routine matters here; any other returns nothing.
                    Reg[2] = ((pc + 4 & 0xF0000000) | (w & 0x03FFFFFF) << 2) == 0x8006272C ? Rand : 0;
                    break;
                case 4: if (a == b) after = Target(); break;
                case 5: if (a != b) after = Target(); break;
                case 6: if ((int)a <= 0) after = Target(); break;
                case 7: if ((int)a > 0) after = Target(); break;
                case 9: Set(rt, a + simm); break;
                case 0x0A: Set(rt, (int)a < (int)simm ? 1u : 0); break;
                case 0x0B: Set(rt, a < simm ? 1u : 0); break;
                case 0x0C: Set(rt, a & imm); break;
                case 0x0D: Set(rt, a | imm); break;
                case 0x0F: Set(rt, imm << 16); break;
                case 0x20: load = (rt, (uint)(sbyte)R8(a + simm)); break;
                case 0x21: load = (rt, (uint)(short)(R8(a + simm) | R8(a + simm + 1) << 8)); break;
                case 0x23: load = (rt, R32(a + simm)); break;
                case 0x24: load = (rt, R8(a + simm)); break;
                case 0x25: load = (rt, (uint)(R8(a + simm) | R8(a + simm + 1) << 8)); break;
                case 0x28: Mem[a + simm] = (byte)b; break;
                case 0x29: Mem[a + simm] = (byte)b; Mem[a + simm + 1] = (byte)(b >> 8); break;
                case 0x2B: W32(a + simm, b); break;
                default: throw new InvalidOperationException($"Unknown instruction 0x{w:X8} at 0x{pc:X8}.");
            }
            // A loaded value arrives after the instruction that follows the load.
            if (pending is { } done && done.Reg != 0) Reg[done.Reg] = done.Value;
            pending = load;
            Watch?.Invoke(pc, this);
            pc = next; next = after;
        }
        if (pending is { } last && last.Reg != 0) Reg[last.Reg] = last.Value;
    }
}
