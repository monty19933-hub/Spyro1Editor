using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Generation;

/// <summary>A picture the generator made for a role (64 x 64), to be written to <see cref="Path"/> before the level is
/// saved; the level's materials name that path.</summary>
public sealed record RandomLevelPicture(SurfaceRole Role, string Path, Rgba32[] Pixels);

/// <summary>Texture slots of the original game that can carry a picture (<see cref="Crash.CrashLevelPictures.Carriers"/>,
/// smallest first) and the project folder the pictures go in.</summary>
public sealed record PictureCarriers(IReadOnlyList<(int Wad, int Id)> Slots, string Folder);

public sealed record RandomLevelReport
{
    public long Seed { get; init; }
    public LevelStyle Style { get; init; }
    public string Theme { get; init; } = "";
    public LevelSize Size { get; init; }
    public string Features { get; init; } = "";
    public int Areas { get; init; }
    public int Faces { get; init; }
    public int Materials { get; init; }
    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<LevelLink> Links { get; init; } = [];
    public int Attempts { get; init; }
    /// <summary>Sampled floors the level needs, and how many of them Spyro cannot reach from the start or cannot get back
    /// from.</summary>
    public int RequiredFloors { get; init; }
    public int Unreached { get; init; }
    public int NoWayBack { get; init; }
    public int Traps { get; init; }
    public int GlideOnlyAreas { get; init; }
    public int Plates { get; init; }
    public int GlideMoves { get; init; }
    public string? DrawPlan { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
    /// <summary>The level in a few plain sentences, for the dialog.</summary>
    public string Summary { get; init; } = "";
}

public sealed record RandomLevelResult(SceneDocument Scene, RandomLevelReport Report, IReadOnlyList<RandomLevelPicture> Pictures,
    IReadOnlyList<AuthoredMaterial> Materials)
{
    /// <summary>A top-down picture of the level (one pixel per 30 units, north up): height-shaded floor colours.</summary>
    public required Rgba32[] Map { get; init; }
    public required int MapWidth { get; init; }
    public required int MapHeight { get; init; }
}

/// <summary>File ▾ › Random Level…: makes a level like the game's own from a seed and a few choices. Everything Spyro must
/// reach is built inside his measured moves and checked by <see cref="LevelReach"/>; the faces are ordinary built-level
/// faces, so the level is edited, painted and filled with objects like any level you build.</summary>
public static class RandomLevelGenerator
{
    public const int Origin = 2048;
    public static int CellsFor(LevelSize size) => size switch { LevelSize.Small => 32, LevelSize.Large => 52, _ => 42 };

    /// <summary>Roles folded into others, in order, when a level must use fewer textures (liquids never fold).</summary>
    public static readonly (SurfaceRole From, SurfaceRole To)[] Folds =
    [
        (SurfaceRole.GroundAlt, SurfaceRole.Ground), (SurfaceRole.Ceiling, SurfaceRole.CaveWall), (SurfaceRole.Underside, SurfaceRole.Cliff),
        (SurfaceRole.CaveWall, SurfaceRole.Cliff), (SurfaceRole.Paving, SurfaceRole.Path), (SurfaceRole.Sand, SurfaceRole.Dirt),
        (SurfaceRole.Rock, SurfaceRole.Cliff), (SurfaceRole.Roof, SurfaceRole.Brick), (SurfaceRole.Dirt, SurfaceRole.Path),
        (SurfaceRole.Charge, SurfaceRole.Path), (SurfaceRole.CliffTop, SurfaceRole.Cliff), (SurfaceRole.Brick, SurfaceRole.Cliff),
    ];

    /// <param name="source">The terrain of a look's level by level key, when it can be read (the editor's level cache):
    /// the level's own towers, trees and rocks are then stood in the new level. Null: none.</param>
    public static RandomLevelResult Generate(RandomLevelOptions options, LevelCatalog catalog, PictureCarriers? carriers = null,
        int textureBudget = 24, bool compile = true, CancellationToken token = default, Func<string, LevelPropSource?>? source = null)
    {
        var rnd = new LevelRandom(options.Seed);
        var style = options.Style ?? rnd.Pick<LevelStyle>([(LevelStyle.Valley, 3), (LevelStyle.Islands, 2), (LevelStyle.SkyIslands, 2),
            (LevelStyle.Caverns, 2), (LevelStyle.Canyon, 2), (LevelStyle.Castle, 1.5)]);
        var theme = LevelThemes.Find(options.Theme) ?? LevelThemes.For(style, rnd);
        bool F(bool? choice, double chance) => choice ?? rnd.Chance(chance);
        var features = new FeatureSet(
            Caves: style == LevelStyle.Caverns || F(options.Caves, style is LevelStyle.Valley or LevelStyle.Canyon ? 0.5 : 0.25),
            ChargePaths: F(options.ChargePaths, 0.5), Glides: F(options.Glides, 0.75), Hazards: F(options.Hazards, 0.65),
            Bridges: style is LevelStyle.Islands or LevelStyle.SkyIslands ? options.Bridges ?? true : F(options.Bridges, 0.7),
            Secrets: F(options.Secrets, 0.6));
        for (int shrink = 0; ; shrink++)
        {
            var notes = new List<string>();
            if (shrink > 0) notes.Add("Built smaller than usual so the game can hold and draw it.");
            try { return Build(options, catalog, carriers, textureBudget, compile, token, rnd.Fork("size" + shrink), style, theme, features, shrink, notes, source); }
            catch (InvalidDataException e) when (compile && shrink < 3)
            {
                if (Environment.GetEnvironmentVariable("SPYRO_RANDOM_LEVEL_DEBUG") == "1") Console.Error.WriteLine($"shrink {shrink}: {e.Message}");
            }
        }
    }

    private static RandomLevelResult Build(RandomLevelOptions options, LevelCatalog catalog, PictureCarriers? carriers, int textureBudget, bool compile,
        CancellationToken token, LevelRandom rnd, LevelStyle style, LevelTheme theme, FeatureSet features, int shrink, List<string> notes,
        Func<string, LevelPropSource?>? source)
    {
        // The look's own things, when its level can be read and the level keeps the game's textures.
        List<LevelProp>? props = null;
        bool gameTextures = options.Textures == LevelTextureMode.GameTextures && options.Pictures.Count == 0;
        if (source != null && gameTextures && textureBudget >= 20 && source(theme.Key) is { } terrain)
            props = LevelProps.Extract(terrain, id => LevelDna.Static(theme.Key, id));
        // A few layouts are tried; the first whose every floor is proved reachable both ways is taken (else the best).
        (PlannedLevel Planned, TerrainMesher Mesher, (int Required, int Unreached, int NoWayBack, List<(double X, double Y)> Bad) Check)? best = null;
        int attempts = 0;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            token.ThrowIfCancellationRequested();
            attempts++;
            var tried = SpyroPlanner.Build(style, options.Size, features, theme, rnd.Fork("plan" + attempt), shrink);
            // Into the built level's coordinates: whole units from 0, the lowest floor (and what hangs under floating
            // land) well above the bottom.
            var (bx0, by0, _, _) = tried.Plan.Bounds();
            double lowest = tried.Plan.Zones.Min(z => Math.Min(z.Z, z.Z1));
            double under = tried.Plan.Kind == EnclosureKind.Sky ? 320 + 0.55 * tried.Plan.Zones.Where(z => !z.IsPath).Max(z => z.Discs.Max(d => d.R)) : 0;
            Shift(tried, Math.Round(Origin - bx0), Math.Round(Origin - by0), Math.Round(BaseHeight + under - lowest));
            var mesh = new TerrainMesher(tried.Plan, tried.Spacing);
            mesh.Build(rnd.Fork("mesh" + attempt));
            var reach = mesh.Reach(tried.Start.X, tried.Start.Y, tried.Glides.Select(g => (g.From, g.To)));
            if (best == null || reach.Unreached + reach.NoWayBack < best.Value.Check.Unreached + best.Value.Check.NoWayBack) best = (tried, mesh, reach);
            if (reach.Unreached + reach.NoWayBack == 0) break;
        }
        var (planned, mesher, check) = best!.Value;
        var plan = planned.Plan;
        var pieces = mesher.Pieces;
        planned.Look = theme.Name;
        pieces.AddRange(SpyroStructures.Build(planned, style, rnd.Fork("structures"), props));
        if (check.Unreached + check.NoWayBack > 0)
            notes.Add($"{check.Unreached + check.NoWayBack} of {check.Required} floors could not be proved reachable both ways; play it through once.");
        token.ThrowIfCancellationRequested();

        // Materials: a texture per surface, fewer surfaces when the level must use fewer textures.
        var used = pieces.Where(p => p.Texture == null).Select(p => p.Role).Distinct().ToList();
        var fold = used.ToDictionary(r => r, r => r);
        foreach (var (from, to) in Folds)
        {
            if (fold.Values.Distinct().Count() <= textureBudget) break;
            foreach (var k in fold.Keys.ToList()) if (fold[k] == from) fold[k] = to;
        }
        var pictures = new List<RandomLevelPicture>();
        var materialOf = new Dictionary<SurfaceRole, AuthoredMaterial?>();
        var averages = new Dictionary<AuthoredMaterial, ColorRgba>();
        int slot = 0;
        string slug = $"random-{(ulong)options.Seed:x}";
        foreach (var role in fold.Values.Distinct().OrderBy(r => r))
        {
            bool own = options.Pictures.ContainsKey(role);
            if (carriers != null && (own || options.Textures == LevelTextureMode.GeneratedPictures) && slot < carriers.Slots.Count)
            {
                Rgba32[] pixels = own ? PngRgbaImage.ReadResizedRgba(options.Pictures[role], 64, 64).ToArray()
                    : ProceduralPictures.Paint(role, theme, rnd.Fork("picture" + role));
                string path = Path.GetFullPath(Path.Combine(carriers.Folder, $"{slug}-{role.ToString().ToLowerInvariant()}.png"));
                pictures.Add(new(role, path, pixels));
                var (wad, id) = carriers.Slots[slot++];
                materialOf[role] = new AuthoredMaterial(wad, id) { Image = path };
                continue;
            }
            if ((own || options.Textures == LevelTextureMode.GeneratedPictures) && carriers == null && !notes.Any(x => x.StartsWith("Pictures")))
                notes.Add("Pictures need the original disc's texture slots; the game's own textures were used instead.");
            var t = theme.Textures[role];
            materialOf[role] = catalog.FindByKey(t.Level) is { } level ? new AuthoredMaterial(level.SourceWadEntry, t.Id) : null;
            if (materialOf[role] is { } game && LevelDna.Average(t.Level, t.Id) is { } average) averages[game] = average;
        }
        var surfaces = new List<AuthoredSurface>();
        var faceRandom = rnd.Fork("faces");
        var lookLevel = catalog.FindByKey(theme.Key);
        var propMaterials = new Dictionary<int, AuthoredMaterial>();
        foreach (var p in pieces)
        {
            if (p.Texture is int texture && p.Paint is { } paint && lookLevel != null)
            {
                // A face of the look's level, as that level has it.
                if (!propMaterials.TryGetValue(texture, out var own))
                {
                    propMaterials[texture] = own = new AuthoredMaterial(lookLevel.SourceWadEntry, texture);
                    if (LevelDna.Average(theme.Key, texture) is { } average) averages[own] = average;
                }
                surfaces.Add(new AuthoredSurface(faceRandom.Id(), p.Corners, Average(paint), own, paint) { TwoSided = p.TwoSided, FrontReversed = p.FrontReversed });
                continue;
            }
            var role = fold[p.Role];
            var material = materialOf[role];
            var ramp = Ramp(theme, role, material);
            var near = p.Light.Select(u => Paint(ramp, u)).ToArray();
            surfaces.Add(new AuthoredSurface(faceRandom.Id(), p.Corners, Average(near), material, near)
            { Kind = p.Kind, NoCollision = !p.Collides && material != null, TwoSided = p.TwoSided });
        }
        // Sectors: faces are grouped by where they are (squares a few faces across), as the game's levels group theirs.
        // A face with a sector keeps its four corners, as the game's own faces do (they are not flat either); without
        // one the compiler would cut every face that is not perfectly flat into two triangles.
        // The game holds 127 sectors: the squares grow until the level needs comfortably fewer.
        for (float size = SectorSize; ; size *= 1.2f)
        {
            var sectors = new Dictionary<(int, int), int>();
            var of = new int[surfaces.Count];
            for (int k = 0; k < surfaces.Count; k++)
            {
                var v = surfaces[k].Vertices;
                var key = ((int)MathF.Floor(v.Average(c => c.X) / size), (int)MathF.Floor(v.Average(c => c.Y) / size));
                if (!sectors.TryGetValue(key, out int id)) sectors[key] = id = sectors.Count;
                of[k] = id;
            }
            if (sectors.Count > MaxSectors && size < 4096) continue;
            for (int k = 0; k < surfaces.Count; k++) surfaces[k] = surfaces[k] with { Sector = of[k], CloseDetail = true };
            break;
        }
        // The start, facing into the level.
        var spawn = new Vector3f((float)Math.Round(planned.Start.X), (float)Math.Round(planned.Start.Y),
            (float)Math.Round(plan.Height(planned.StartZone, planned.Start.X, planned.Start.Y)) + 32);
        int heading = (int)Math.Round(Math.Atan2(planned.Facing.Y - planned.Start.Y, planned.Facing.X - planned.Start.X) / (2 * Math.PI) * 256) & 0xFF;
        // The made-up name is always drawn, so a name typed in its place changes nothing else about the level.
        string madeUp = Name(theme, rnd);
        string name = options.Name is { } given && TextTargetName(given) ? given.Trim().ToUpperInvariant() : madeUp;
        var gems = planned.Gems.Select(g => new Vector3f((float)Math.Round(g.X), (float)Math.Round(g.Y), (float)Math.Round(plan.Height(g.Zone, g.X, g.Y)))).Distinct().Take(120).ToArray();
        var scene = SceneDocument.New(name) with
        {
            Id = rnd.Id(), Spawn = spawn, Heading = heading, Surfaces = surfaces, SkyColor = theme.SkyHorizon,
            MusicTrack = rnd.Pick(theme.Music),
            SkySourceWadEntry = catalog.FindByKey(theme.SkyLevel)?.SourceWadEntry,
            GemSpots = gems,
        };
        if (options.PaintedSky)
        {
            try
            {
                var block = ProceduralSky.Build(theme, rnd.Fork("sky"));
                scene = scene with { CustomSky = block, SkySourceWadEntry = null };
            }
            catch (InvalidDataException e) { notes.Add("The painted sky could not be made (" + e.Message + "); the theme's game sky is used."); }
        }
        scene.Validate();
        var materials = surfaces.Select(s => s.Material).OfType<AuthoredMaterial>().Distinct().ToList();
        string? drawPlan = null;
        if (compile)
        {
            NativeAuthoredSpawn.RequirePortalArrivalSupported(scene);
            NativeAuthoredMesh.Compile(scene, materials);
            drawPlan = NativeAuthoredMeshRenderer.LastDrawPlan?.Describe();
            if (NativeAuthoredMeshRenderer.LastDrawPlan?.Note() is { } note) notes.Add(note);
        }
        notes.AddRange(planned.Notes);
        var report = new RandomLevelReport
        {
            Seed = options.Seed, Style = style, Theme = theme.Key, Size = options.Size,
            Features = string.Join(", ", new[] { features.Caves ? "caves" : null, features.ChargePaths ? "supercharge paths" : null, features.Glides ? "glides" : null,
                features.Hazards ? "hazards" : null, features.Bridges ? "bridges" : null, features.Secrets ? "secrets" : null }.OfType<string>()),
            Areas = planned.Areas.Count, Faces = surfaces.Count, Materials = materials.Count, Counts = planned.Counts, Links = planned.Links,
            Attempts = attempts, RequiredFloors = check.Required, Unreached = check.Unreached, NoWayBack = check.NoWayBack,
            GlideMoves = planned.Glides.Count, DrawPlan = drawPlan, Notes = notes,
        };
        report = report with { Summary = Summarise(report, theme, name) };
        var (map, mw, mh) = Map(pieces, surfaces, averages);
        float mapX = surfaces.Min(f => f.Vertices.Min(v => v.X)), mapY = surfaces.Min(f => f.Vertices.Min(v => v.Y));
        foreach (var (x, y) in check.Bad)
        {
            int col = (int)((x - mapX) / 30), row = (int)((y - mapY) / 30);
            if (col >= 0 && col < mw && row >= 0 && row < mh) map[(mh - 1 - row) * mw + col] = new Rgba32(255, 0, 0, 255);
        }
        return new(scene, report, pictures, materials) { Map = map, MapWidth = mw, MapHeight = mh };
    }

    /// <summary>The side of the squares faces are grouped into sectors by.</summary>
    public const float SectorSize = 640;
    /// <summary>Most sectors a generated level is grouped into (the game holds 127; big sectors are split when compiled).</summary>
    public const int MaxSectors = 96;

    /// <summary>The lowest floor's height in the built level.</summary>
    public const int BaseHeight = 512;

    private static void Shift(PlannedLevel L, double dx, double dy, double dz)
    {
        foreach (var z in L.Plan.Zones.Concat(L.Plan.Decals))
        {
            for (int k = 0; k < z.Discs.Count; k++) z.Discs[k] = (z.Discs[k].X + dx, z.Discs[k].Y + dy, z.Discs[k].R);
            for (int k = 0; k < z.Spine.Count; k++) z.Spine[k] = (z.Spine[k].X + dx, z.Spine[k].Y + dy);
            z.Z += dz; z.Z1 += dz;
        }
        L.Plan.SeaLevel += dz;
        foreach (var a in L.Areas) { a.X += dx; a.Y += dy; a.Z += dz; }
        for (int k = 0; k < L.Gems.Count; k++) L.Gems[k] = (L.Gems[k].X + dx, L.Gems[k].Y + dy, L.Gems[k].Zone);
        for (int k = 0; k < L.Links.Count; k++)
        {
            var l = L.Links[k];
            Vector3f M(Vector3f v) => new((float)(v.X + dx), (float)(v.Y + dy), (float)(v.Z + dz));
            L.Links[k] = l with { From = M(l.From), To = M(l.To) };
        }
        L.Start = (L.Start.X + dx, L.Start.Y + dy); L.Facing = (L.Facing.X + dx, L.Facing.Y + dy);
    }

    /// <summary>The paint of a surface, darkest to lightest: the original level's own colours for the texture when they
    /// were measured, else the texture's shade from dim to bright; a plain grey scale for a picture, which has its own colours.</summary>
    private static IReadOnlyList<ColorRgba> Ramp(LevelTheme theme, SurfaceRole role, AuthoredMaterial? material)
    {
        if (material == null)
        {
            var c = theme.Colors[role];
            return [c.Dark, c.Base, c.Light];
        }
        if (material.Image == null && theme.Textures[role] is { } t)
        {
            if (t.Ramp is { } measured) return measured;
            var b = t.Tint ?? new ColorRgba(t.Shade, t.Shade, t.Shade);
            ColorRgba S(double f) => new((byte)Math.Clamp(b.R * f, 8, 255), (byte)Math.Clamp(b.G * f, 8, 255), (byte)Math.Clamp(b.B * f, 8, 255));
            return [S(0.42), S(0.75), S(1.12)];
        }
        return [new(54, 54, 54), new(98, 98, 98), new(142, 142, 142)];
    }

    /// <summary>The ramp's colour at <paramref name="u"/> (0-1), in steps of four so neighbouring faces share colours (a
    /// sector holds 64).</summary>
    private static ColorRgba Paint(IReadOnlyList<ColorRgba> ramp, double u)
    {
        double at = Math.Clamp(u, 0, 1) * (ramp.Count - 1);
        int k = Math.Min((int)at, ramp.Count - 2);
        double f = at - k;
        byte C(byte a, byte b) => (byte)Math.Clamp((int)Math.Round((a + (b - a) * f) / 4) * 4, 8, 252);
        return new(C(ramp[k].R, ramp[k + 1].R), C(ramp[k].G, ramp[k + 1].G), C(ramp[k].B, ramp[k + 1].B));
    }

    private static bool TextTargetName(string s) => NewWorldDocument.IsValidName(s.Trim().ToUpperInvariant()) && s.Trim().All(c => !char.IsDigit(c));

    private static string Name(LevelTheme theme, LevelRandom rnd)
    {
        for (int k = 0; k < 20; k++)
        {
            string s = rnd.Pick(theme.Adjectives) + " " + rnd.Pick(theme.Nouns);
            if (s.Length <= NewWorldDocument.MaxNameLength) return s;
        }
        return rnd.Pick(theme.Nouns);
    }

    private static ColorRgba Average(ColorRgba[] c) =>
        new((byte)c.Average(x => x.R), (byte)c.Average(x => x.G), (byte)c.Average(x => x.B));

    /// <summary>A top-down picture of the level (one pixel per 30 units, north up): every face that looks up, lowest
    /// first, in its texture's colour under its paint.</summary>
    private static (Rgba32[] Pixels, int W, int H) Map(IReadOnlyList<TerrainPiece> pieces, IReadOnlyList<AuthoredSurface> surfaces, Dictionary<AuthoredMaterial, ColorRgba> averages)
    {
        const float unit = 30;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var s in surfaces) foreach (var v in s.Vertices) { x0 = Math.Min(x0, v.X); y0 = Math.Min(y0, v.Y); x1 = Math.Max(x1, v.X); y1 = Math.Max(y1, v.Y); }
        int w = Math.Max(1, (int)Math.Ceiling((x1 - x0) / unit) + 2), h = Math.Max(1, (int)Math.Ceiling((y1 - y0) / unit) + 2);
        var px = new Rgba32[w * h];
        Array.Fill(px, new Rgba32(24, 28, 40, 255));
        var order = Enumerable.Range(0, surfaces.Count).Where(k => !pieces[k].Wall).OrderBy(k => surfaces[k].Vertices.Average(v => v.Z)).ThenBy(k => k);
        foreach (int k in order)
        {
            var s = surfaces[k];
            ColorRgba? avg = s.Material != null && averages.TryGetValue(s.Material, out var found) ? found : null;
            var c = s.Color;
            var colour = avg is { } a
                ? new Rgba32((byte)Math.Min(255, a.R * c.R / 128), (byte)Math.Min(255, a.G * c.G / 128), (byte)Math.Min(255, a.B * c.B / 128), 255)
                : new Rgba32((byte)Math.Min(255, c.R * 2), (byte)Math.Min(255, c.G * 2), (byte)Math.Min(255, c.B * 2), 255);
            var v = s.Vertices;
            float minY = v.Min(p => p.Y), maxY = v.Max(p => p.Y);
            for (int row = (int)MathF.Floor((minY - y0) / unit); row <= (int)MathF.Ceiling((maxY - y0) / unit); row++)
            {
                float y = y0 + (row + 0.5f) * unit, lo = float.MaxValue, hi = float.MinValue;
                for (int e = 0; e < v.Length; e++)
                {
                    var p = v[e]; var q = v[(e + 1) % v.Length];
                    if (p.Y == q.Y || y < Math.Min(p.Y, q.Y) || y >= Math.Max(p.Y, q.Y)) continue;
                    float x = p.X + (q.X - p.X) * (y - p.Y) / (q.Y - p.Y);
                    lo = Math.Min(lo, x); hi = Math.Max(hi, x);
                }
                if (lo > hi || row < 0 || row >= h) continue;
                for (int col = Math.Max(0, (int)MathF.Round((lo - x0) / unit)); col <= Math.Min(w - 1, (int)MathF.Round((hi - x0) / unit)); col++)
                    px[(h - 1 - row) * w + col] = colour;
            }
        }
        return (px, w, h);
    }

    private static string Summarise(RandomLevelReport r, LevelTheme theme, string name)
    {
        string style = r.Style switch
        {
            LevelStyle.Valley => "clearings among the hills", LevelStyle.Islands => "islands in the water", LevelStyle.SkyIslands => "islands in the sky",
            LevelStyle.Caverns => "caverns under the rock", LevelStyle.Canyon => "a canyon", _ => "castle courtyards"
        };
        var parts = r.Counts.Where(c => c.Value > 0 && c.Key != SpyroStructures.OwnThings).OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => $"{c.Value} {Plural(c.Key, c.Value)}").ToList();
        string own = r.Counts.TryGetValue(SpyroStructures.OwnThings, out int things) && things > 0
            ? $"{things} of {theme.Name}{(theme.Name.EndsWith('s') ? "'" : "'s")} own towers, trees and rocks stand in it. " : "";
        string reach = r.Unreached + r.NoWayBack == 0 ? "Every floor can be reached from the start and left again." :
            $"Almost every floor is proved reachable ({r.RequiredFloors - r.Unreached - r.NoWayBack} of {r.RequiredFloors}).";
        return $"{name}: {style} in the style of {theme.Name}, {r.Areas} areas, {r.Faces:N0} faces, {r.Materials} textures. " +
               (parts.Count > 0 ? "It has " + string.Join(", ", parts) + ". " : "") + own + reach;
    }

    private static string Plural(string what, int n) => n == 1 ? what : what switch
    {
        "chain of stepping stones" => "chains of stepping stones", "nook up a charge ramp" => "nooks up charge ramps",
        "lookout with a glide" => "lookouts with glides", _ when what.EndsWith("s") => what, _ => what + "s"
    };
}
