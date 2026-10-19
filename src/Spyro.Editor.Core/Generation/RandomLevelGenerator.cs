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

    public static RandomLevelResult Generate(RandomLevelOptions options, LevelCatalog catalog, PictureCarriers? carriers = null,
        int textureBudget = 24, bool compile = true, CancellationToken token = default)
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
            if (shrink > 0) notes.Add($"Built {shrink * 4} cells narrower than usual so the game can hold and draw it.");
            try { return Build(options, catalog, carriers, textureBudget, compile, token, rnd.Fork("size" + shrink), style, theme, features, CellsFor(options.Size) - 4 * shrink, notes); }
            catch (InvalidDataException) when (compile && shrink < 3) { }
        }
    }

    private static RandomLevelResult Build(RandomLevelOptions options, LevelCatalog catalog, PictureCarriers? carriers, int textureBudget, bool compile,
        CancellationToken token, LevelRandom rnd, LevelStyle style, LevelTheme theme, FeatureSet features, int n, List<string> notes)
    {
        (Layout L, LevelReach R, double Score)? best = null;
        int attempts = 0;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            token.ThrowIfCancellationRequested();
            attempts++;
            var L = new Layout
            {
                Grid = new LevelGrid(n, n, Origin, Origin), Theme = theme, Style = style, Random = rnd.Fork("layout" + attempt), Features = features
            };
            switch (style)
            {
                case LevelStyle.Islands: case LevelStyle.SkyIslands: LayoutIslands.Build(L, n); break;
                case LevelStyle.Caverns: LayoutCaverns.Build(L, n); break;
                default: LayoutValley.Build(L, n); break;
            }
            L.Undulate(style switch { LevelStyle.Castle => 4, LevelStyle.Caverns => 8, _ => 12 });
            var reach = new LevelReach(L.Grid);
            reach.Check(Spawn(L));
            // Score: the share of the level's floors Spyro can reach and come back from, worst area counted twice.
            var areas = reach.ByArea.Where(a => a.Value.Total >= 8).Select(a => a.Value.Good / (double)a.Value.Total).ToList();
            double whole = reach.RequiredFloors == 0 ? 0 : 1 - (reach.Unreached + reach.NoWayBack) / (double)reach.RequiredFloors;
            double score = whole + (areas.Count == 0 ? 0 : areas.Min());
            // Supercharge paths asked for outright: a layout with a supercharge leap is preferred (a few more tries).
            bool wantLeap = options.ChargePaths == true && style is LevelStyle.Valley or LevelStyle.Canyon or LevelStyle.Castle;
            bool hasLeap = L.Counts.ContainsKey("supercharge leap");
            if (wantLeap && hasLeap) score += 0.05;
            if (best == null || score > best.Value.Score) best = (L, reach, score);
            if (areas.Count > 0 && areas.Min() >= 0.9 && whole >= 0.97 && (!wantLeap || hasLeap || attempt >= 5)) break;
        }
        var (layout, check, _) = best!.Value;
        if (check.Unreached + check.NoWayBack > 0)
            notes.Add($"{check.Unreached + check.NoWayBack} of {check.RequiredFloors} floor samples could not be proved reachable both ways (corners and edges); play it through once.");

        // Faces, materials, light.
        var pieces = layout.Grid.Faces(out int unpaired);
        if (unpaired > 0 && Environment.GetEnvironmentVariable("SPYRO_RANDOM_LEVEL_DEBUG") == "1")
        { notes.Add($"{unpaired} wall pieces were left out where two sloping surfaces cross."); notes.AddRange(layout.Grid.Unmatched); }
        var used = pieces.Select(p => p.Role).Distinct().ToList();
        var fold = used.ToDictionary(r => r, r => r);
        foreach (var (from, to) in Folds)
        {
            if (fold.Values.Distinct().Count() <= textureBudget) break;
            foreach (var k in fold.Keys.ToList()) if (fold[k] == from) fold[k] = to;
        }
        var pictures = new List<RandomLevelPicture>();
        var materialOf = new Dictionary<SurfaceRole, AuthoredMaterial?>();
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
        }
        var light = Light(theme);
        var surfaces = new List<AuthoredSurface>();
        var faceRandom = rnd.Fork("faces");
        foreach (var p in pieces)
        {
            var role = fold[p.Role];
            var material = materialOf[role];
            var shade = material?.Image != null ? new ColorRgba(128, 128, 128) : theme.Textures[role] is { } tt ? tt.Tint ?? new ColorRgba(tt.Shade, tt.Shade, tt.Shade) : new ColorRgba(128, 128, 128);
            var basis = material == null ? theme.Colors[role].Base : shade;
            var near = Shade(p, basis, light, theme, faceRandom);
            surfaces.Add(new AuthoredSurface(faceRandom.Id(), p.Corners, Average(near), material, near)
            { Kind = p.Kind, NoCollision = !p.Collides && material != null });
        }
        // The start, facing into the level.
        var spawn = Spawn(layout);
        float cx = layout.Areas.SelectMany(a => a.Cells).Average(c => c.I + 0.5f), cy = layout.Areas.SelectMany(a => a.Cells).Average(c => c.J + 0.5f);
        var middle = layout.Point(cx, cy, 0);
        int heading = (int)Math.Round(Math.Atan2(middle.Y - spawn.Y, middle.X - spawn.X) / (2 * Math.PI) * 256) & 0xFF;
        // The made-up name is always drawn, so a name typed in its place changes nothing else about the level.
        string madeUp = Name(theme, rnd);
        string name = options.Name is { } given && TextTargetName(given) ? given.Trim().ToUpperInvariant() : madeUp;
        var scene = SceneDocument.New(name) with
        {
            Id = rnd.Id(), Spawn = spawn, Heading = heading, Surfaces = surfaces, SkyColor = theme.SkyHorizon,
            MusicTrack = rnd.Pick(theme.Music),
            SkySourceWadEntry = catalog.FindByKey(theme.SkyLevel)?.SourceWadEntry,
            GemSpots = [.. layout.GemSpots.Where(g => g.X >= 0 && g.Y >= 0).Distinct().Take(120)],
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
        notes.AddRange(layout.Notes);
        var links = layout.Links.Select(l => l with { JumpAt = l.JumpAt ?? JumpAt(layout.Grid, l) }).ToList();
        var report = new RandomLevelReport
        {
            Seed = options.Seed, Style = style, Theme = theme.Key, Size = options.Size,
            Features = string.Join(", ", new[] { features.Caves ? "caves" : null, features.ChargePaths ? "supercharge paths" : null, features.Glides ? "glides" : null,
                features.Hazards ? "hazards" : null, features.Bridges ? "bridges" : null, features.Secrets ? "secrets" : null }.OfType<string>()),
            Areas = layout.Areas.Count, Faces = surfaces.Count, Materials = materials.Count, Counts = layout.Counts, Links = links,
            Attempts = attempts, RequiredFloors = check.RequiredFloors, Unreached = check.Unreached, NoWayBack = check.NoWayBack, Traps = check.Traps,
            GlideOnlyAreas = check.GlideOnlyAreas.Count, Plates = check.Plates, GlideMoves = check.GlideEdges, DrawPlan = drawPlan, Notes = notes,
        };
        report = report with { Summary = Summarise(report, theme, name) };
        var (map, mw, mh) = Map(layout.Grid, theme);
        foreach (var (si, sj) in check.Bad) map[(mh - 1 - sj) * mw + si] = new Rgba32(255, 0, 0, 255);
        return new(scene, report, pictures, materials) { Map = map, MapWidth = mw, MapHeight = mh };
    }

    /// <summary>How far from a link's start (editor units, along the line to its end) the floor Spyro stands on ends or
    /// rises: where a jump starts. Null when the floor runs all the way (a ramp, a bridge).</summary>
    private static float? JumpAt(LevelGrid grid, LevelLink l)
    {
        float dx = l.To.X - l.From.X, dy = l.To.Y - l.From.Y, len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1) return null;
        for (float t = 4; t < len; t += 4)
        {
            float x = l.From.X + dx / len * t, y = l.From.Y + dy / len * t;
            int i = (int)MathF.Floor((x - grid.OriginX) / LevelGrid.Cell), j = (int)MathF.Floor((y - grid.OriginY) / LevelGrid.Cell);
            if (!grid.Inside(i, j)) return t;
            float u = (x - grid.X(i)) / LevelGrid.Cell, v = (y - grid.Y(j)) / LevelGrid.Cell;
            var under = grid.At(i, j).Where(s => s.TopAt(u, v) <= l.From.Z + 40).Select(s => (Z: s.TopAt(u, v), s.Kind)).DefaultIfEmpty((Z: float.NegativeInfinity, Kind: null)).MaxBy(p => p.Z);
            if (Math.Abs(under.Z - l.From.Z) > 6 || under.Kind is { } k && k != AuthoredSurfaceKind.Supercharge) return t;
        }
        return null;
    }

    private static bool TextTargetName(string s) => NewWorldDocument.IsValidName(s.Trim().ToUpperInvariant()) && s.Trim().All(c => !char.IsDigit(c));

    /// <summary>The start: the middle of the start pad, a little above its floor.</summary>
    private static Vector3f Spawn(Layout L)
    {
        var (i, j) = L.StartCell;
        float z = L.Grid.Ground(i, j)?.Top[0] ?? L.Grid.At(i, j).FirstOrDefault()?.Top[0] ?? LayoutValley.Ground0;
        return L.Point(i, j, z + 32);
    }

    private static string Name(LevelTheme theme, LevelRandom rnd)
    {
        for (int k = 0; k < 20; k++)
        {
            string s = rnd.Pick(theme.Adjectives) + " " + rnd.Pick(theme.Nouns);
            if (s.Length <= NewWorldDocument.MaxNameLength) return s;
        }
        return rnd.Pick(theme.Nouns);
    }

    private static (double X, double Y, double Z) Light(LevelTheme theme)
    {
        double az = theme.SunAzimuth * Math.PI / 180, el = theme.SunElevation * Math.PI / 180;
        return (Math.Cos(el) * Math.Cos(az), Math.Cos(el) * Math.Sin(az), Math.Sin(el));
    }

    /// <summary>Per-corner colours: the texture's shade lit by the theme's sun (faces turned away keep the ambient part),
    /// darker under rock and where walls meet the floor, with a slight random variation between faces.</summary>
    private static ColorRgba[] Shade(LevelGrid.Piece p, ColorRgba basis, (double X, double Y, double Z) sun, LevelTheme theme, LevelRandom rnd)
    {
        var v = p.Corners;
        double nx = 0, ny = 0, nz = 0;
        for (int k = 0; k < v.Length; k++)
        {
            var a = v[k]; var b = v[(k + 1) % v.Length];
            nx += (a.Y - b.Y) * (a.Z + b.Z); ny += (a.Z - b.Z) * (a.X + b.X); nz += (a.X - b.X) * (a.Y + b.Y);
        }
        double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (len > 0) { nx /= len; ny /= len; nz /= len; }
        double lambert = Math.Max(0, nx * sun.X + ny * sun.Y + nz * sun.Z);
        double i0 = nz < -0.5 ? theme.Ambient * 0.8 : theme.Ambient + (1 - theme.Ambient) * lambert;
        if (p.Covered) i0 *= 0.66;
        var result = new ColorRgba[v.Length];
        for (int k = 0; k < v.Length; k++)
        {
            // Light in steps of 1/32, so neighbouring faces share their colours (a sector holds at most 64).
            double i = Math.Round(i0 * (p.Foot?[k] == true ? 0.74 : 1) * 32) / 32;
            byte C(byte b, byte l) => (byte)Math.Clamp(Math.Round(b * l / 128.0 * i * 1.08), 8, 255);
            result[k] = new ColorRgba(C(basis.R, theme.Light.R), C(basis.G, theme.Light.G), C(basis.B, theme.Light.B));
        }
        return result;
    }

    private static ColorRgba Average(ColorRgba[] c) =>
        new((byte)c.Average(x => x.R), (byte)c.Average(x => x.G), (byte)c.Average(x => x.B));

    /// <summary>A top-down picture of the floors: each 30-unit spot coloured by its role, brighter the higher it is.</summary>
    private static (Rgba32[] Pixels, int W, int H) Map(LevelGrid grid, LevelTheme theme)
    {
        int s = 4, w = grid.W * s, h = grid.H * s;
        var px = new Rgba32[w * h];
        int lo = int.MaxValue, hi = int.MinValue;
        foreach (var (i, j) in grid.AllCells()) foreach (var slab in grid.At(i, j)) if (slab.DrawTop) { lo = Math.Min(lo, slab.MinTop); hi = Math.Max(hi, slab.MaxTop); }
        foreach (var (i, j) in grid.AllCells())
        {
            var top = grid.At(i, j).LastOrDefault(x => x.DrawTop || x.Required) ?? grid.At(i, j).LastOrDefault();
            for (int a = 0; a < s; a++)
                for (int b = 0; b < s; b++)
                {
                    var c = new Rgba32(24, 28, 40, 255);
                    if (top != null)
                    {
                        float z = top.TopAt((a + 0.5f) / s, (b + 0.5f) / s);
                        double light = 0.55 + 0.6 * (hi == lo ? 0.5 : (z - lo) / (hi - lo));
                        var basis = theme.Colors[top.TopRole].Base;
                        if (!top.DrawTop) basis = new(70, 66, 72);
                        c = new Rgba32((byte)Math.Clamp(basis.R * light, 0, 255), (byte)Math.Clamp(basis.G * light, 0, 255), (byte)Math.Clamp(basis.B * light, 0, 255), 255);
                    }
                    // North up: row 0 is the top of the picture.
                    px[(h - 1 - (j * s + b)) * w + i * s + a] = c;
                }
        }
        return (px, w, h);
    }

    private static string Summarise(RandomLevelReport r, LevelTheme theme, string name)
    {
        string style = r.Style switch
        {
            LevelStyle.Valley => "a valley of terraces", LevelStyle.Islands => "islands in the water", LevelStyle.SkyIslands => "islands in the sky",
            LevelStyle.Caverns => "caverns under the rock", LevelStyle.Canyon => "a winding canyon trail", _ => "a walled castle"
        };
        var parts = r.Counts.Where(c => c.Value > 0).OrderByDescending(c => c.Value).Select(c => $"{c.Value} {Plural(c.Key, c.Value)}").ToList();
        string reach = r.Unreached + r.NoWayBack == 0 ? "Every area can be reached from the start and left again." :
            $"Almost every floor is proved reachable ({r.RequiredFloors - r.Unreached - r.NoWayBack} of {r.RequiredFloors} spots).";
        return $"{name}: {style} in the {theme.Name} theme, {r.Areas} areas, {r.Faces:N0} faces, {r.Materials} textures. " +
               (parts.Count > 0 ? "It has " + string.Join(", ", parts) + ". " : "") + reach;
    }

    private static string Plural(string what, int n) => n == 1 ? what : what switch
    {
        "open ground" => "open joins", "ledge" => "ledges", "stepping stones" => "stepping stones", "chasm" => "chasms", "glide gap" => "glide gaps",
        _ when what.EndsWith("s") => what, _ => what + "s"
    };
}
