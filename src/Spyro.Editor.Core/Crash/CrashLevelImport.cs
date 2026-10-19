using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Crash;

/// <summary>How a Crash Bandicoot: Warped level is rebuilt (see <see cref="CrashLevelImport"/>).</summary>
public sealed record CrashLevelImportOptions
{
    /// <summary>Folder for the level's pictures (64x64 PNG, one per kept texture); the editor keeps a project's pictures
    /// under the project's custom-terrain-textures/&lt;name&gt;/. Created if missing; pictures of the same name are replaced.</summary>
    public required string PictureFolder { get; init; }
    /// <summary>Spyro the Dragon levels whose terrain textures may carry the pictures (null: every retail level).</summary>
    public IReadOnlyList<string>? CarrierLevels { get; init; }
    /// <summary>Editor units per Crash unit. <see cref="CrateScale"/> keeps a crate 46 units wide, the size of the
    /// editor's Crash crates and enemies (<see cref="UniversalCrashEnemies.CrashUnitsPerSpyroUnit"/>).</summary>
    public double Scale { get; init; } = CrateScale;
    public const double CrateScale = 46.0 / 400;
    /// <summary>First and last zone of the level's main path to import (indices into <see cref="CrashLevelData.MainZones"/>;
    /// null: the first / the last). A level with more faces than a Spyro level holds is imported in parts.</summary>
    public int? FirstZone { get; init; }
    public int? LastZone { get; init; }
    /// <summary>Backdrop kept around the zones' volumes, in Crash units (scenery Crash shows beyond its path: hills,
    /// houses). Null: as much as <see cref="FaceBudget"/> allows, up to <see cref="MaxBackdropMargin"/>. The zones' own
    /// volumes (everything Crash can walk on) are always kept.</summary>
    public int? BackdropMargin { get; init; }
    public const int MaxBackdropMargin = 4000;
    /// <summary>Drawn faces the import may use (a quad drawn whole counts one, a quad drawn as two triangles two); the
    /// game holds <see cref="NativeAuthoredMeshRenderer.MaxFaces"/>, the rest is left for the editor's own faces.</summary>
    public int FaceBudget { get; init; } = 7000;
    /// <summary>Turn about the vertical axis in degrees (null: none when the level fits the game's 16,383-unit coordinate
    /// range, otherwise the smallest turn that makes it fit; a long level often fits only diagonally).</summary>
    public double? Turn { get; init; }
    /// <summary>Invisible walls where Crash's own collision walls the path in (zone collision kinds 0 and 2), so Spyro
    /// cannot jump or glide off the path into the scenery behind it (its backdrop hills are layers with gaps).</summary>
    public bool InvisibleWalls { get; init; } = true;
    /// <summary>Crash's pits (collision kind 4, a fall that kills) get an invisible deadly floor a little below their rim,
    /// so Spyro dies falling in as Crash does (Spyro the Dragon's deadly water: no hit point, at once).</summary>
    public bool DeadlyPits { get; init; } = true;
    /// <summary>Faces whose picture is at least this see-through (0-1) are left out unless they are floor: Spyro the
    /// Dragon's terrain has no see-through texels, so grass tufts and bunting would become solid cards. 1 keeps them all.</summary>
    public double DropSeeThrough { get; init; } = 0.5;
    /// <summary>Walls and other steep faces are drawn from both sides, as Crash draws them (its winding means nothing), so
    /// no wall shows the sky through its back wherever Spyro's free camera looks from; the chosen front still decides which
    /// side is solid. Floors and slopes (turned more than 30° up) keep one side, the one that looks up (and overhangs both,
    /// by the side rules): drawing every face twice filled the game's draw buffer (0x800757B0 runs into the ordering table
    /// at 0x801BFBB8) and crashed it in Toad Village's busiest views. False: every face one-sided by the side rules.</summary>
    public bool DrawBothSides { get; init; } = true;
    /// <summary>When the compile finds a view with more terrain than the game's draw buffer holds comfortably even with
    /// simpler distant meshes and a shorter texture distance (<see cref="NativeAuthoredMeshRenderer.DrawPlan"/>), the
    /// import is built again with 15% less backdrop, until it fits or only the zones are left.</summary>
    public bool TrimBackdropForDrawLoad { get; init; } = true;
    /// <summary>Floors and slopes drawn from both sides too (every face twice: the game's draw buffer overflows in busy views).</summary>
    public bool DrawFloorsBothSides { get; init; }
    /// <summary>Two triangles that draw the two halves of one texture across a shared edge become one quad when the game
    /// draws that quad exactly as the two triangles (lossless; fewer drawn faces).</summary>
    public bool MergeTrianglePairs { get; init; } = true;
    /// <summary>Where Crash's collision has walkable ground but no drawn floor lies at or below it (a deck drawn above
    /// Crash's ground, a crack between faces), an undrawn floor at Crash's ground height, so Spyro cannot fall where
    /// Crash cannot.</summary>
    public bool CatchFloors { get; init; } = true;
    /// <summary>Undrawn walls under the open edges of floors (see CrashLevelImport: skirts), so Spyro cannot walk under a
    /// slab whose sides Crash never drew.</summary>
    public bool Skirts { get; init; } = true;
    /// <summary>Invisible walls rise this far (editor units) above the highest floor beside them.</summary>
    public int WallHeight { get; init; } = 200;
    /// <summary>At most this many pictures (the game's materials are numbered 0-127).</summary>
    public int MaxTextures { get; init; } = 127;
    /// <summary>Checks the pictures' carriers against the built level's free texture memory and merges pictures until
    /// they fit (the check Create BIN makes). False skips it (tests without the export's bootstrap capture).</summary>
    public bool CheckTextureMemory { get; init; } = true;
    /// <summary>Texture memory (bytes) kept free after the terrain and the level's dragon rescue scenes, for objects.</summary>
    public int TextureMemoryReserve { get; init; } = 48 * 1024;
    /// <summary>Compiles the result as Create BIN does and refuses it over a limit (false: research, the report's terrain
    /// numbers are then estimates).</summary>
    public bool CompileCheck { get; init; } = true;
    /// <summary>Overrides the start position (editor units) and heading (0-255).</summary>
    public Vector3f? Spawn { get; init; }
    public int? Heading { get; init; }
}

/// <summary>What an import produced and what it left out. <see cref="Lines"/> is the readable report.</summary>
public sealed record CrashLevelImportReport
{
    public required string Level { get; init; }
    public string Zones { get; init; } = "";
    public double Scale { get; init; }
    public double Turn { get; init; }
    public int BackdropMargin { get; init; }
    public int MainFaces { get; init; }
    public int Faces { get; init; }
    public int DrawnFaces { get; init; }
    public int Triangles { get; init; }
    public int MergedPairs { get; init; }
    public IReadOnlyDictionary<string, int> LeftOut { get; init; } = new Dictionary<string, int>();
    public int TwoSidedFaces { get; init; }
    public int ReversedFaces { get; init; }
    public int Pictures { get; init; }
    public int KeptPictures { get; init; }
    public IReadOnlyDictionary<int, int> Merges { get; init; } = new Dictionary<int, int>();
    public int Carriers { get; init; }
    public int FreeTextureBytes { get; init; } = -1;
    public int Sectors { get; init; }
    public int NativeSectors { get; init; }
    public int WallFaces { get; init; }
    public double WallLength { get; init; }
    public int PitFloors { get; init; }
    public int NoCollisionFaces { get; init; }
    /// <summary>Undrawn floors where Crash's ground has no drawn floor (CrashLevelImportOptions.CatchFloors).</summary>
    public int CatchFloors { get; init; }
    /// <summary>Undrawn skirt walls under open floor edges (CrashLevelImportOptions.Skirts).</summary>
    public int Skirts { get; init; }
    /// <summary>Crash's way through the level in editor units: from the start to the walkable ground farthest along,
    /// keeping to the middle of the path (a point about every 92 units at crate scale); its length.</summary>
    public IReadOnlyList<Vector3f> Route { get; init; } = [];
    public double RouteLength { get; init; }
    public int PlayableCells { get; init; }
    public int CollisionTriangles { get; init; }
    public int TerrainBytes { get; init; }
    /// <summary>The compile's draw plan (<see cref="NativeAuthoredMeshRenderer.DrawPlan.Describe"/>; empty when not compiled).</summary>
    public string DrawLoad { get; init; } = "";
    public Vector3f Spawn { get; init; }
    public int Heading { get; init; }
    public string SpawnSource { get; init; } = "";
    public float Lift { get; init; }
    public Vector3f Extent { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> Lines()
    {
        List<string> l =
        [
            $"{Level}, zones {Zones}: {Faces} faces ({DrawnFaces} drawn, {Triangles} triangles; {MergedPairs} triangle pairs joined into quads) from {MainFaces} in the main area, " +
                $"{TwoSidedFaces} two-sided, {ReversedFaces} showing their other side",
            $"scale {Scale:0.####} editor units per Crash unit (a crate {400 * Scale:0.#} units), turned {Turn:0} degrees, backdrop kept {BackdropMargin:N0} Crash units around the zones; " +
                $"size {Extent.X:0} x {Extent.Y:0} x {Extent.Z:0}",
            "left out: " + (LeftOut.Count == 0 ? "nothing" : string.Join(", ", LeftOut.Select(k => $"{k.Value} {k.Key}"))),
            $"pictures: {Pictures} used, {KeptPictures} kept on {Carriers} carriers" + (FreeTextureBytes >= 0 ? $", {FreeTextureBytes:N0} bytes of texture memory left for objects" : "") +
                (Merges.Count > 0 ? $"; merged {string.Join(" ", Merges.OrderBy(m => m.Key).Select(m => $"{m.Key}->{m.Value}"))}" : ""),
            $"sectors: {Sectors} ({NativeSectors} compiled) of the game's {NativeAuthoredMeshRenderer.MaxSectors}, each with its own distant mesh",
            $"Crash's collision: {PlayableCells:N0} walkable cells; {WallFaces} invisible wall faces ({WallLength:N0} units long), {PitFloors} deadly pit floors; " +
                 $"{CatchFloors} undrawn floors where Crash's ground has no drawn floor, {Skirts} skirts under open floor edges; {NoCollisionFaces} faces beyond the walls drawn without collision; {CollisionTriangles:N0} collision triangles",
            $"start: ({Spawn.X:0},{Spawn.Y:0},{Spawn.Z:0}) heading {Heading}, {SpawnSource}" + (Route.Count > 1 ? $"; Crash's way to the end is {RouteLength:N0} units long" : ""),
            $"terrain data: {TerrainBytes:N0} of {NativeAuthoredMesh.MaxSurfaceBytes:N0} bytes" + (Lift > 0 ? $"; raised {Lift:0} units above the replaced level's kill height" : ""),
        ];
        if (DrawLoad.Length > 0) l.Add("draw load: " + DrawLoad);
        l.AddRange(Warnings.Select(w => "warning: " + w));
        return l;
    }
}

/// <summary>An imported level: the scene (bound to the target), its report and its materials.</summary>
public sealed record CrashLevelImportResult(SceneDocument Scene, CrashLevelImportReport Report, IReadOnlyList<AuthoredMaterial> Materials)
{
    /// <summary>The imported terrain, invisible walls, name, start and sky colour on a Start Blank scene (keeping its
    /// identity, save rows, objects, borrowed retail sky if any, and music).</summary>
    public SceneDocument ApplyTo(SceneDocument blank) =>
        blank with { Name = Scene.Name, Spawn = Scene.Spawn, Heading = Scene.Heading, Surfaces = Scene.Surfaces, FarMeshes = Scene.FarMeshes,
            CollisionFaces = Scene.CollisionFaces, SkyColor = Scene.SkyColor };
}

/// <summary>Why an import was refused (a limit the level cannot be brought under without losing parts Spyro walks on).</summary>
public sealed class CrashLevelImportException(string message) : Exception(message);

/// <summary>Rebuilds a Crash Bandicoot: Warped level's terrain as a Spyro the Dragon scene (the pipeline first prototyped
/// in Python, export_crash.py, 2026-10-10):
/// <list type="bullet">
/// <item>the main area's scenery (its zones' path, or a part of it) and, around it, as much backdrop as the face budget
/// allows; untextured faces (dark pit bottoms) take a plain grey picture that shows their vertex colours; faces whose
/// texture corners are not the region's corners are left out;</item>
/// <item>Crash axes (x, y up, z along) become editor (x, -z, y) turned about the vertical axis when the level only fits
/// the game's coordinate range diagonally, at <see cref="CrashLevelImportOptions.Scale"/>, raised above the replaced
/// level's kill height;</item>
/// <item>each texture region becomes a 64x64 picture (a triangle's picture is turned or mirrored so its corners show
/// the picture's top-left, bottom-left and bottom-right; transparent texels take the picture's average colour); a quad's
/// corners show its picture's TR, TL, BL, BR; near and far colours are Crash's vertex colours (both modulate the
/// texture), and each sector gets its own untextured distant mesh in the picture's average times them;</item>
/// <item>triangle pairs drawing one texture across the quad's TR-BL diagonal become one quad; see-through decorations
/// (grass tufts, bunting) are left out; the farthest backdrop is trimmed to the face budget;</item>
/// <item>Crash draws every face from both sides: here floors and slopes face up, a wall faces the side whose ground
/// is lower, a small object's walls face out from its middle, other walls face the path; see-through faces and floors
/// with floor under them (overhangs) are drawn from both sides;</item>
/// <item>Crash's zone collision (an octree per zone) gives the walkable ground Spyro can reach from the start:
/// invisible walls follow its edge (Crash's own corridor walls), its pits get deadly floors, and scenery beyond the
/// walls is drawn without collision;</item>
/// <item>pictures are carried by Spyro the Dragon texture slots and merged by similarity until they fit; faces are
/// grouped into sectors that each fit one native sector (at most 170 corners: the game's scratchpad); the start is
/// Crash's own start (for a later part of the path, where Crash enters it), with an undrawn landing pad under it when
/// the path is narrower than a doorway arrival needs;</item>
/// <item>the result is compiled as Create BIN compiles it; over a limit the import is refused with the numbers.</item>
/// </list></summary>
public static class CrashLevelImport
{
    public const int Pad = 32;
    /// <summary>Lowest faces are kept this far above the kill height (Spyro stands about 22 units above the floor).</summary>
    public const float KillHeightMargin = 64;
    /// <summary>Spacing (Crash units) of the samples of Crash's collision that the walls follow.</summary>
    public const double CollisionStep = 100;

    public static async Task<CrashLevelImportResult> ImportAsync(string crashDisc, CrashLevel level, string spyroDisc, LevelCatalog catalog,
        SceneTargetBinding? binding, CrashLevelImportOptions options, IProgress<string>? progress = null, CancellationToken token = default)
    {
        progress?.Report($"Reading {level.Name}…");
        var data = CrashLevels.Read(new CrashWarpedDisc(crashDisc), level);
        token.ThrowIfCancellationRequested();
        progress?.Report("Finding Spyro the Dragon textures that can carry pictures…");
        var lib = new UniversalSourceLibrary(spyroDisc, catalog);
        var carriers = CrashLevelPictures.Carriers(lib, catalog, options.CarrierLevels ?? [.. catalog.Levels.Select(l => l.Key)], spyroDisc, token);
        CrashPictureVram? vram = null;
        if (options.CheckTextureMemory && carriers.Count > 0)
        {
            progress?.Report("Measuring the built level's free texture memory…");
            vram = await CrashPictureVram.CaptureAsync(spyroDisc, new(carriers[0].Wad, carriers[0].Id),
                binding == null ? null : lib.Get(binding.LevelKey).Data.Vram().ToArray(), token);
        }
        float kill = binding == null ? 0 : CrashLevelPictures.KillHeight(lib.Disc.Exe, binding.LevelId);
        return await BuildAsync(data, level, binding, carriers, vram == null ? null : vram.Test, options, progress, token, kill);
    }

    /// <summary>A face of the scene before materials, sectors and sides are chosen (editor units).</summary>
    private sealed class Piece
    {
        public required string Key;
        public required Vector3f[] Points;
        public required ColorRgba[] Near;
        public required int Picture;
        public required double Backdrop;     // Crash units outside the zones' volumes (0 inside)
        public required (double X, double Z)[] Crash;   // each corner's Crash x, z
        public bool TwoSided, Reversed, Horizontal, Water, NoCollision;
        public Vector3f Normal;
        public Vector3f Centre => new(Points.Average(p => p.X), Points.Average(p => p.Y), Points.Average(p => p.Z));
    }

    /// <summary>One picture: the texture region (page, box, palette, mode, segment) and the turn/mirror applied.</summary>
    private sealed record PictureKey(string Page, int U0, int U1, int V0, int V1, int ClutX, int ClutY, int Mode, int Segment, bool Swap, bool FlipX, bool FlipY);
    private sealed record Picture(Rgba32[] Pixels, ColorRgba Average, double SeeThrough, int Blend);

    /// <summary>The import without the discs: <paramref name="vram"/> tests a material list against the built level's free
    /// texture memory (null: not checked); <paramref name="killHeight"/> is the replaced level's kill height.</summary>
    public static async Task<CrashLevelImportResult> BuildAsync(CrashLevelData data, CrashLevel level, SceneTargetBinding? binding,
        IReadOnlyList<CrashLevelPictures.Carrier> carriers, Func<IReadOnlyList<AuthoredMaterial>, (bool Fits, int FreeBytes, string? Reason)>? vram,
        CrashLevelImportOptions options, IProgress<string>? progress = null, CancellationToken token = default, float killHeight = 0)
    {
        List<string> warnings = [];
        Dictionary<string, int> leftOut = [];
        void Out(string why, int n = 1) { if (n > 0) leftOut[why] = leftOut.GetValueOrDefault(why) + n; }
        if (data.MainZones.Count == 0 || data.Main.Count == 0) throw new CrashLevelImportException($"{level.Name} has no main area with zones.");
        if (options.Scale is <= 0 or > 1) throw new CrashLevelImportException("The scale must be between 0 and 1 editor units per Crash unit.");
        int first = options.FirstZone ?? 0, last = options.LastZone ?? data.MainZones.Count - 1;
        if (first < 0 || last >= data.MainZones.Count || first > last)
            throw new CrashLevelImportException($"{level.Name}'s main path has zones 0-{data.MainZones.Count - 1} ({data.MainZones[0].Name}..{data.MainZones[^1].Name}); choose a range inside it.");
        var zones = data.MainZones.Skip(first).Take(last - first + 1).ToList();
        var allBoxes = data.MainZones.Select(z => z.Box).ToList();
        double scale = options.Scale;

        // 1. Faces of the main area in the chosen zones, with their distance outside the zones' volumes.
        progress?.Report("Choosing faces…");
        double Backdrop(double x, double y, double z)
        {
            double best = double.MaxValue;
            foreach (var b in zones.Select(z => z.Box))
            {
                double dx = Math.Max(0, Math.Max(b.X - x, x - (b.X + b.W))), dz = Math.Max(0, Math.Max(b.Z - z, z - (b.Z + b.D)));
                double dy = Math.Max(0, Math.Max(b.Y - 2000 - y, y - (b.Y + b.H)));
                best = Math.Min(best, Math.Max(dx, Math.Max(dy, dz)));
            }
            return best;
        }
        int ZoneOf(double x, double z)
        {
            int best = 0; double d = double.MaxValue;
            for (int i = 0; i < allBoxes.Count; i++) { double e = allBoxes[i].DistanceXZ(x, z); if (e < d) { d = e; best = i; } }
            return best;
        }
        int maxMargin = options.BackdropMargin ?? CrashLevelImportOptions.MaxBackdropMargin;
        var mainFaces = data.MainFaces().ToList();
        List<(CrashScenery Sc, CrashSceneryFace F, int Index, double Backdrop)> chosen = [];
        var faceIndex = new Dictionary<CrashScenery, int>();
        foreach (var (sc, f) in mainFaces)
        {
            int index = faceIndex.GetValueOrDefault(sc); faceIndex[sc] = index + 1;
            var ps = f.Corners.Select(i => sc.Vertices[i]).ToArray();
            double cx = ps.Average(p => p.X), cy = ps.Average(p => p.Y), cz = ps.Average(p => p.Z);
            int zi = ZoneOf(cx, cz);
            if (zi < first || zi > last) continue;
            double bd = Backdrop(cx, cy, cz);
            if (bd > maxMargin) { Out("backdrop faces beyond the margin"); continue; }
            chosen.Add((sc, f, index, bd));
        }
        if (chosen.Count == 0) throw new CrashLevelImportException($"{level.Name} has no textured faces in zones {zones[0].Name}..{zones[^1].Name}.");

        // 2. The turn and placement: Crash (x, y, z) -> editor (x, -z, y), turned, scaled, shifted to the positive range.
        var planPoints = chosen.SelectMany(c => c.F.Corners.Select(i => c.Sc.Vertices[i])).Select(p => (X: (double)p.X, W: (double)-p.Z))
            .Concat(zones.SelectMany(z => new[] { (X: (double)z.Box.X, W: (double)-z.Box.Z), (z.Box.X + z.Box.W, -z.Box.Z), (z.Box.X, -(z.Box.Z + z.Box.D)), (z.Box.X + z.Box.W, -(z.Box.Z + z.Box.D)) })).ToList();
        double Extent(double degrees)
        {
            double r = degrees * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
            double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
            foreach (var (x, w) in planPoints) { double a = x * c - w * s, b = x * s + w * c; x0 = Math.Min(x0, a); x1 = Math.Max(x1, a); y0 = Math.Min(y0, b); y1 = Math.Max(y1, b); }
            return Math.Max(x1 - x0, y1 - y0) * scale;
        }
        double room = 16383 - 2 * Pad;
        double turn;
        if (options.Turn is double t) turn = t;
        else if (Extent(0) <= room) turn = 0;
        else
        {
            var fits = Enumerable.Range(1, 179).SelectMany(a => new[] { a, -a }).Where(a => Extent(a) <= room).Cast<int?>().FirstOrDefault();
            if (fits is int a) turn = a;
            else
            {
                int best = Enumerable.Range(0, 180).MinBy(a => Extent(a));
                throw new CrashLevelImportException($"{level.Name} zones {zones[0].Name}..{zones[^1].Name} are {Extent(best):N0} units long even turned {best} degrees at this scale; " +
                    $"a Spyro level spans at most {room:N0}. Import fewer zones or use a smaller scale (at most {options.Scale * room / Extent(best):0.###}).");
            }
        }
        double rad = turn * Math.PI / 180, cos = Math.Cos(rad), sin = Math.Sin(rad);
        (double X, double Y) Plan(double x, double z) { double w = -z; return ((x * cos - w * sin) * scale, (x * sin + w * cos) * scale); }
        double ox = planPoints.Min(p => (p.X * cos - p.W * sin) * scale) - Pad, oy = planPoints.Min(p => (p.X * sin + p.W * cos) * scale) - Pad;
        double yMin = chosen.Min(c => c.F.Corners.Min(i => c.Sc.Vertices[i].Y));
        Vector3f Editor(double x, double y, double z) { var (px, py) = Plan(x, z); return new((float)Math.Round(px - ox), (float)Math.Round(py - oy), (float)Math.Round((y - yMin) * scale) + Pad); }
        Vector3f EditorExact(double x, double y, double z) { var (px, py) = Plan(x, z); return new((float)(px - ox), (float)(py - oy), (float)((y - yMin) * scale + Pad)); }

        // 3. Pictures and corner order.
        progress?.Report("Converting textures…");
        Dictionary<PictureKey, int> pictureIds = [];
        List<Picture> pictures = [];
        Dictionary<(string, int, int, int, int, int, int, int, int), CrashTextureImage?> regions = [];
        CrashTextureImage? Region(CrashScenery sc, CrashModel.Texture tex)
        {
            string? pageName = sc.PageOf(tex);
            if (pageName == null) return null;
            var key = (pageName, (int)tex.U.Min(), (int)tex.U.Max(), (int)tex.V.Min(), (int)tex.V.Max(), tex.ClutX, tex.ClutY, tex.Mode, tex.Segment);
            if (!regions.TryGetValue(key, out var image))
                regions[key] = image = data.Pages.TryGetValue(pageName, out var page) ? CrashTextureImage.Read(page, tex) : null;
            return image;
        }
        int PictureOf(CrashScenery sc, CrashModel.Texture tex, CrashTextureImage image, (bool Swap, bool FlipX, bool FlipY) op)
        {
            var key = new PictureKey(sc.PageOf(tex)!, tex.U.Min(), tex.U.Max(), tex.V.Min(), tex.V.Max(), tex.ClutX, tex.ClutY, tex.Mode, tex.Segment, op.Swap, op.FlipX, op.FlipY);
            if (pictureIds.TryGetValue(key, out int id)) return id;
            var avg = image.Average();
            var px = new Rgba32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    // Output (x, y) of the turned picture: undo the flips, then the swap, to find the source texel.
                    int a = op.FlipX ? 63 - x : x, b = op.FlipY ? 63 - y : y;
                    if (op.Swap) (a, b) = (b, a);
                    var c = image.Pixels[b * image.Height / 64 * image.Width + a * image.Width / 64];
                    px[y * 64 + x] = c.A == 0 ? new(avg.R, avg.G, avg.B, 255) : c;
                }
            id = pictures.Count; pictureIds[key] = id;
            pictures.Add(new(px, avg, image.Transparent, tex.Blend));
            return id;
        }

        // Untextured faces: a plain picture (texel 128 shows the vertex colours as they are), never merged.
        int plain = -1;
        int Plain()
        {
            if (plain >= 0) return plain;
            plain = pictures.Count;
            pictures.Add(new([.. Enumerable.Repeat(new Rgba32(128, 128, 128, 255), 64 * 64)], new(128, 128, 128), 0, 3));
            return plain;
        }
        List<Piece> pieces = [];
        Dictionary<string, (CrashModel.Texture Tex, string[] Labels, CrashTextureImage Image, CrashScenery Sc)> triangleSource = [];
        foreach (var (sc, f, index, bd) in chosen)
        {
            if (f.Texture == null)
            {
                var corners = f.Outline;
                var pe = corners.Select(i => sc.Vertices[i]).ToArray();
                var pp = pe.Select(p => Editor(p.X, p.Y, p.Z)).ToArray();
                if (pp.Distinct().Count() != pp.Length || Newell(pp) is not { } pn) { Out("faces with no area at this scale"); continue; }
                pieces.Add(new Piece { Key = $"crash:{level.File}:{sc.Name}:{index}", Points = pp, Near = [.. corners.Select(i => sc.VertexColours[i])], Picture = Plain(),
                    Backdrop = bd, Normal = pn, Crash = [.. pe.Select(p => ((double)p.X, (double)p.Z))] });
                continue;
            }
            var tex = sc.Textures[f.Texture.Value];
            var image = Region(sc, tex);
            if (image == null) { Out("faces whose texture page is missing"); continue; }
            int n = f.Quad ? 4 : 3;
            var labels = Labels(tex, n);
            if (labels == null) { Out("faces whose texture corners are not the region's corners"); continue; }
            var pts = f.Corners.Select(i => sc.Vertices[i]).ToArray();
            var ed = pts.Select(p => Editor(p.X, p.Y, p.Z)).ToArray();
            var col = f.Corners.Select(i => sc.VertexColours[i]).ToArray();
            int[] order; int picture;
            if (n == 4)
            {
                int[] per = [0, 1, 3, 2];
                int[]? found = null;
                foreach (var seq in new[] { per, per.Reverse().ToArray() })
                {
                    for (int r = 0; r < 4 && found == null; r++)
                    {
                        int[] s = [.. seq[r..], .. seq[..r]];
                        if (s.Select(i => labels[i]).SequenceEqual(Cycle)) found = s;
                    }
                    if (found != null) break;
                }
                if (found == null) { Out("quads whose texture is twisted"); continue; }
                order = found; picture = PictureOf(sc, tex, image, (false, false, false));
            }
            else
            {
                (int[] Order, (bool, bool, bool) Op)? hit = null;
                foreach (var op in Dihedral)
                {
                    var ml = labels.Select(l => MapLabel(l, op)).ToArray();
                    foreach (var seq in new[] { new[] { 0, 1, 2 }, new[] { 0, 2, 1 } })
                    {
                        for (int r = 0; r < 3 && hit == null; r++)
                        {
                            int[] s = [.. seq[r..], .. seq[..r]];
                            if (s.Select(i => ml[i]).SequenceEqual(TriangleCycle)) hit = (s, op);
                        }
                        if (hit != null) break;
                    }
                    if (hit != null) break;
                }
                order = hit!.Value.Order; picture = PictureOf(sc, tex, image, hit.Value.Op);
            }
            var points = order.Select(i => ed[i]).ToArray();
            if (points.Distinct().Count() != n) { Out("faces too small to keep their corners apart at this scale"); continue; }
            var normal = Newell(points);
            if (normal == null) { Out("faces with no area at this scale"); continue; }
            string key = $"crash:{level.File}:{sc.Name}:{index}";
            var piece = new Piece { Key = key, Points = points, Near = [.. order.Select(i => col[i])], Picture = picture, Backdrop = bd, Normal = normal.Value,
                Crash = [.. order.Select(i => ((double)pts[i].X, (double)pts[i].Z))] };
            pieces.Add(piece);
            if (n == 3) triangleSource[key] = (tex, [.. order.Select(i => labels[i])], image, sc);
        }

        // 4. Triangle pairs that draw one texture's two halves become one quad (when the game draws that quad exactly so).
        int mergedPairs = 0;
        if (options.MergeTrianglePairs) mergedPairs = MergePairs(pieces, triangleSource, (sc, tex, image) => PictureOf(sc, tex, image, (false, false, false)));

        // 5. See-through decorations (not floor) are left out.
        foreach (var p in pieces.ToList())
        {
            p.Horizontal = Math.Abs(p.Normal.Z) >= 0.5;
            var pic = pictures[p.Picture];
            p.Water = pic.Blend != 3 && p.Horizontal && pic.SeeThrough < 0.05;
            if (pic.SeeThrough >= options.DropSeeThrough && Math.Abs(p.Normal.Z) < 0.7) { pieces.Remove(p); Out("see-through decorations (grass tufts, bunting)"); }
        }

        // 6. The face budget: the farthest backdrop goes first; the zones' own volumes always stay.
        int Drawn(Piece p) => p.Points.Length == 4 && !ConvexQuad(p.Points) ? 2 : 1;
        int drawn = pieces.Sum(Drawn);
        int margin = maxMargin;
        if (drawn > options.FaceBudget)
        {
            var inside = pieces.Where(p => p.Backdrop <= 0).Sum(Drawn);
            if (inside > options.FaceBudget)
                throw new CrashLevelImportException($"{level.Name} zones {zones[0].Name}..{zones[^1].Name} draw {inside:N0} faces inside the zones Crash walks through; " +
                    $"the budget is {options.FaceBudget:N0} (the game holds {NativeAuthoredMeshRenderer.MaxFaces:N0}). Import fewer zones (each zone draws about {inside / zones.Count:N0}).");
            var ordered = pieces.OrderBy(p => p.Backdrop).ToList();
            int total = 0, keep = 0;
            while (keep < ordered.Count && total + Drawn(ordered[keep]) <= options.FaceBudget) total += Drawn(ordered[keep++]);
            // Keep whole distance steps (no face kept farther out than one left out).
            while (keep > 0 && keep < ordered.Count && ordered[keep - 1].Backdrop == ordered[keep].Backdrop) keep--;
            margin = keep < ordered.Count ? (int)Math.Floor(ordered[keep].Backdrop) - 1 : maxMargin;
            var cut = ordered.Skip(keep).ToList();
            Out("backdrop faces beyond the margin", cut.Count);
            var cutSet = cut.ToHashSet();
            pieces = [.. pieces.Where(p => !cutSet.Contains(p))];
            drawn = pieces.Sum(Drawn);
        }
        token.ThrowIfCancellationRequested();

        // 7. Which side shows.
        progress?.Report("Choosing which side of each face shows…");
        var path = zones.Select(z => EditorExact(z.Box.X + z.Box.W / 2.0, z.Box.Y, z.Box.Z + z.Box.D / 2.0)).Select(p => (p.X, p.Y)).ToList();
        var floors = new FloorGrid(pieces.Where(p => Math.Abs(p.Normal.Z) >= 0.5).Select(p => p.Points));
        ChooseSides(pieces, pictures, floors, path);

        // 8. Crash's collision: walkable ground, walls and pits.
        progress?.Report("Reading Crash's collision…");
        // Crash's start when it lies in the chosen zones; for a later part of the path, where Crash enters its first zone
        // (the point of that zone nearest the previous zone's middle).
        CrashPoint? playerStart = data.PlayerStart is { } crashStart && zones.Any(z => z.Box.ContainsXZ(crashStart.X, crashStart.Z)) ? crashStart : null;
        CrashPoint entry;
        {
            var b = zones[0].Box;
            var (tx, tz) = first > 0 ? (data.MainZones[first - 1].Box.X + data.MainZones[first - 1].Box.W / 2.0, data.MainZones[first - 1].Box.Z + data.MainZones[first - 1].Box.D / 2.0)
                : (b.X + b.W / 2.0, b.Z + b.D / 2.0);
            entry = new((int)Math.Clamp(tx, b.X + 300, b.X + b.W - 300), b.Y + b.H / 2, (int)Math.Clamp(tz, b.Z + 300, b.Z + b.D - 300));
        }
        var collision = Collision(zones, data.MainZones, options, scale, playerStart ?? entry);
        List<AuthoredCollisionFace> hidden = [];
        double wallLength = 0;
        // The drawn floors (Crash's ground can lie above or below them): walls reach below the lowest drawn floor beside them
        // and above the highest, so Spyro cannot walk under or jump over one where the two disagree.
        var drawnFloors = new FloorGrid(pieces.Where(p => Math.Abs(p.Normal.Z) >= 0.5).Select(p => p.Points));
        foreach (var w in collision.Walls)
        {
            var a = Editor(w.X0, w.Bottom, w.Z0); var b = Editor(w.X1, w.Bottom, w.Z1);
            // Crash's walkable ground is on the left of X0,Z0 -> X1,Z1; the wall is solid (and "visible") from that side.
            double lx = -(w.Z1 - w.Z0), lz = w.X1 - w.X0, ll = Math.Sqrt(lx * lx + lz * lz);
            var inside = EditorExact((w.X0 + w.X1) / 2 + lx / ll * 50, w.Bottom, (w.Z0 + w.Z1) / 2 + lz / ll * 50);
            if ((b.X - a.X) * (inside.Y - a.Y) - (b.Y - a.Y) * (inside.X - a.X) < 0) (a, b) = (b, a);
            float top = (float)Math.Round((w.Top - yMin) * scale) + Pad + options.WallHeight;
            float bottom = Math.Max(0, a.Z);
            {
                // Drawn floors within 40 units of the wall on its walkable side, at heights Crash's ground there could mean.
                double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                if (len > 0)
                {
                    double ux = (b.X - a.X) / len, uy = (b.Y - a.Y) / len;   // walkable side: (-uy, ux)
                    double floorLow = double.MaxValue, floorHigh = double.MinValue;
                    for (double along = 0; along <= len; along += 16)
                        foreach (double off in new[] { 8.0, 24, 40 })
                            foreach (double h in drawnFloors.Heights(a.X + ux * along - uy * off, a.Y + uy * along + ux * off).Where(h => h >= bottom - 300 && h <= top))
                            { floorLow = Math.Min(floorLow, h); floorHigh = Math.Max(floorHigh, h); }
                    if (floorLow != double.MaxValue) bottom = Math.Max(0, Math.Min(bottom, MathF.Floor((float)floorLow) - 40));
                    if (floorHigh != double.MinValue) top = Math.Max(top, MathF.Ceiling((float)floorHigh) + options.WallHeight);
                }
            }
            if (a.X == b.X && a.Y == b.Y || top <= bottom) continue;
            wallLength += Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
            // Pieces within one collision piece (250 units a side), so each stays exactly upright when compiled.
            int across = (int)Math.Ceiling(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)) / 250), up = (int)Math.Ceiling((top - bottom) / 250);
            for (int k = 0; k < across; k++)
            {
                var p = new Vector3f(MathF.Round(a.X + (b.X - a.X) * k / across), MathF.Round(a.Y + (b.Y - a.Y) * k / across), 0);
                var q = new Vector3f(MathF.Round(a.X + (b.X - a.X) * (k + 1) / across), MathF.Round(a.Y + (b.Y - a.Y) * (k + 1) / across), 0);
                if (p.X == q.X && p.Y == q.Y) continue;
                for (int m = 0; m < up; m++)
                {
                    float z0 = MathF.Round(bottom + (top - bottom) * m / up), z1 = MathF.Round(bottom + (top - bottom) * (m + 1) / up);
                    // Anticlockwise seen from the walkable side (on the left of p -> q seen from above).
                    hidden.Add(new([q with { Z = z0 }, p with { Z = z0 }, p with { Z = z1 }, q with { Z = z1 }]));
                }
            }
        }
        int wallFaces = hidden.Count;
        foreach (var pit in collision.Pits)
        {
            var c = pit.Corners.Select(p => Editor(p.X, pit.Height, p.Z)).Select(v => v with { Z = Math.Max(0, v.Z) }).ToArray();
            if (c.Distinct().Count() < 3 || Newell(c) == null) continue;
            // Counter-clockwise from above, so the floor is solid from above.
            if (Newell(c)!.Value.Z < 0) c = [.. c.Reverse()];
            hidden.Add(new(c, AuthoredSurfaceKind.DeadlyWater));
        }
        int pitFloors = hidden.Count - wallFaces;
        int catchFloors = 0;
        if (options.CatchFloors)
        {
            // Walkable samples with no drawn floor from 300 units below Crash's ground to 24 above it.
            var kinds = collision.Kinds; int nx = kinds.GetLength(0), nz = kinds.GetLength(1); double g = CollisionStep;
            var need = new float[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    need[i, j] = float.NaN;
                    if (kinds[i, j] != 1) continue;
                    var e = EditorExact(collision.X0 + (i + 0.5) * g, collision.Heights[i, j], collision.Z0 + (j + 0.5) * g);
                    if (!drawnFloors.Heights(e.X, e.Y).Any(h => h >= e.Z - 300 && h <= e.Z + 24)) need[i, j] = e.Z;
                }
            // Runs along x in each row (heights within 20 units), each an undrawn floor overlapping its neighbours a little.
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (float.IsNaN(need[i, j])) continue;
                    int e = i; float low = need[i, j];
                    while (e + 1 < nx && !float.IsNaN(need[e + 1, j]) && Math.Abs(need[e + 1, j] - need[i, j]) <= 20) { e++; low = Math.Min(low, need[e, j]); }
                    double ax = collision.X0 + i * g - g / 4, bx = collision.X0 + (e + 1) * g + g / 4, az = collision.Z0 + j * g - g / 4, bz = collision.Z0 + (j + 1) * g + g / 4;
                    var c = new[] { (ax, az), (bx, az), (bx, bz), (ax, bz) }.Select(q => Editor(q.Item1, 0, q.Item2) with { Z = Math.Max(0, MathF.Floor(low) - 1) }).ToArray();
                    if (Newell(c) is { } n) { if (n.Z < 0) c = [.. c.Reverse()]; hidden.Add(new(c)); catchFloors++; }
                    i = e;
                }
        }
        // Scenery Spyro cannot reach (beyond the invisible walls, more than ReachSamples samples from walkable ground) is
        // drawn without collision: it would only fill the level's collision data.
        int noCollision = 0;
        if (options.InvisibleWalls && collision.Walls.Count > 0)
            foreach (var p in pieces)
                if (!collision.Reachable(p.Crash)) { p.NoCollision = true; noCollision++; }
        if (!options.InvisibleWalls) warnings.Add("No invisible walls: Spyro can jump and glide off the path into the gaps between the backdrop's layers.");

        // Skirts: Crash's collision is not drawn, so a raised slab or step often has a drawn top and no drawn side, and Spyro
        // would walk under its edge (a floor is solid only from above). Each open edge of a floor with collision (an edge
        // no other face shares, with no drawn floor just beyond it at its height) gets an undrawn wall from 60 units below
        // up to just under the edge, solid from outside. Spyro walking on the floor passes over it; a real passage under a
        // bridge keeps the clearance below the skirt.
        int skirts = 0;
        if (options.Skirts)
        {
            var solidFloors = pieces.Where(p => !p.NoCollision && Math.Abs(p.Normal.Z) >= 0.5).ToList();
            var floorGrid = new FloorGrid(solidFloors.Select(p => p.Points));
            Dictionary<(Vector3f, Vector3f), int> edgeUse = [];
            (Vector3f, Vector3f) Key(Vector3f a, Vector3f b) => Compare(a, b) <= 0 ? (a, b) : (b, a);
            foreach (var p in pieces)
                for (int k = 0; k < p.Points.Length; k++) { var e = Key(p.Points[k], p.Points[(k + 1) % p.Points.Length]); edgeUse[e] = edgeUse.GetValueOrDefault(e) + 1; }
            foreach (var p in solidFloors)
            {
                var c = p.Centre;
                for (int k = 0; k < p.Points.Length; k++)
                {
                    var a = p.Points[k]; var b = p.Points[(k + 1) % p.Points.Length];
                    if (edgeUse[Key(a, b)] > 1) continue;
                    double ex = b.X - a.X, ey = b.Y - a.Y, len = Math.Sqrt(ex * ex + ey * ey);
                    if (len < 4) continue;
                    double nx = ey / len, ny = -ex / len;   // perpendicular; turned to point away from the face's middle
                    if (nx * (c.X - (a.X + b.X) / 2) + ny * (c.Y - (a.Y + b.Y) / 2) > 0) { nx = -nx; ny = -ny; }
                    bool continues = true;
                    foreach (double f in new[] { 0.2, 0.5, 0.8 })
                    {
                        double x = a.X + ex * f + nx * 6, y = a.Y + ey * f + ny * 6, h = a.Z + (b.Z - a.Z) * f;
                        if (!floorGrid.Heights(x, y).Any(z => Math.Abs(z - h) <= 12)) { continues = false; break; }
                    }
                    if (continues) continue;
                    // Upright pieces of at most 250 units, corners anticlockwise seen from outside.
                    int parts = (int)Math.Ceiling(len / 250);
                    for (int m = 0; m < parts; m++)
                    {
                        Vector3f P(double f) => new(MathF.Round((float)(a.X + ex * f)), MathF.Round((float)(a.Y + ey * f)), MathF.Round((float)(a.Z + (b.Z - a.Z) * f)) - 2);
                        var u = P(m / (double)parts); var v = P((m + 1) / (double)parts);
                        if (u.X == v.X && u.Y == v.Y) continue;
                        var quad = new[] { u with { Z = Math.Max(0, u.Z - 58) }, v with { Z = Math.Max(0, v.Z - 58) }, v, u };
                        if (Newell(quad) is not { } n) continue;
                        if (n.X * nx + n.Y * ny < 0) quad = [quad[1], quad[0], quad[3], quad[2]];
                        hidden.Add(new(quad)); skirts++;
                    }
                }
            }
        }

        var route = collision.Route.Select(r => Editor(r.X, r.Y, r.Z)).ToList();
        // 9. Above the replaced level's kill height.
        var surfaces0 = pieces.Select(p => p.Points).ToList();
        float lowest = surfaces0.Min(s => s.Min(v => v.Z));
        float lift = killHeight > 0 ? Math.Max(0, MathF.Ceiling(killHeight + KillHeightMargin - lowest)) : 0;
        Vector3f Up(Vector3f v) => v with { Z = v.Z + lift };
        foreach (var p in pieces) p.Points = [.. p.Points.Select(Up)];
        hidden = [.. hidden.Select(h => h with { Vertices = [.. h.Vertices.Select(Up)] })];
        route = [.. route.Select(Up)];
        float maxX = Math.Max(pieces.Max(p => p.Points.Max(v => v.X)), hidden.Count == 0 ? 0 : hidden.Max(h => h.Vertices.Max(v => v.X)));
        float maxY = Math.Max(pieces.Max(p => p.Points.Max(v => v.Y)), hidden.Count == 0 ? 0 : hidden.Max(h => h.Vertices.Max(v => v.Y)));
        float maxZ = Math.Max(pieces.Max(p => p.Points.Max(v => v.Z)), hidden.Count == 0 ? 0 : hidden.Max(h => h.Vertices.Max(v => v.Z)));
        if (maxX > 16383 || maxY > 16383 || maxZ > 16383)
            throw new CrashLevelImportException($"{level.Name} reaches ({maxX:0}, {maxY:0}, {maxZ:0}) after raising it {lift:0} units above the replaced level's kill height; a Spyro level stays within 16,383. Use a smaller scale or fewer zones.");
        if (lift > 0) warnings.Add($"Raised {lift:0} units: the replaced level kills Spyro below height {killHeight:0}.");

        // 10. Pictures: merge until they fit the carriers and the texture memory.
        var use = pieces.GroupBy(p => p.Picture).ToDictionary(g => g.Key, g => g.Count());
        var usedPictures = use.Keys.Order().ToDictionary(k => k, k => pictures[k].Pixels);
        int limit = Math.Min(Math.Min(options.MaxTextures, 127), carriers.Count);
        if (limit == 0) throw new CrashLevelImportException("No Spyro the Dragon texture can carry a picture.");
        var merger = new CrashLevelPictures.TextureMerger(usedPictures, use, use.ContainsKey(plain) ? new HashSet<int> { plain } : []);
        merger.MergeTo(limit);
        int free = -1;
        List<AuthoredMaterial> materials;
        while (true)
        {
            materials = [.. merger.Kept.Select((k, i) => new AuthoredMaterial(carriers[i].Wad, carriers[i].Id) { Image = PicturePath(options.PictureFolder, level, k) })];
            if (vram == null) break;
            if (materials.Count == limit || materials.Count % 10 == 0) progress?.Report($"Fitting {materials.Count} pictures into texture memory…");
            var (fits, bytes, reason) = vram(materials);
            if (fits && bytes >= options.TextureMemoryReserve) { free = bytes; break; }
            reason ??= $"{bytes:N0} bytes would be left for objects; {options.TextureMemoryReserve:N0} are kept";
            if (!merger.CanMerge) throw new CrashLevelImportException($"{level.Name}'s pictures do not fit the built level's texture memory even merged to {merger.Kept.Count}: {reason}");
            merger.MergeTo(merger.Kept.Count - 1);
            token.ThrowIfCancellationRequested();
        }
        var materialOf = merger.Kept.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => materials[x.i]);

        // 11. Surfaces and sectors.
        progress?.Report("Grouping sectors…");
        // Near colours modulate the texture (Crash's vertex colours, 128 = as stored) whether the face is subdivided or
        // drawn whole, so the faces' far colours equal them; the untextured distant meshes take the picture's average
        // times them (AuthoredFarMesh.Always, built per sector below).
        var surfaces = pieces.Select(p => new AuthoredSurface(StableId(p.Key), p.Points, p.Near[0], materialOf[merger.Target(p.Picture)], p.Near, p.Near)
            { TwoSided = p.TwoSided || options.DrawFloorsBothSides || options.DrawBothSides && Math.Abs(p.Normal.Z) < 0.5, FrontReversed = p.Reversed, NoCollision = p.NoCollision }).ToList();
        ColorRgba[] Untextured(int i)
        {
            var avg = pictures[merger.Target(pieces[i].Picture)].Average;
            return [.. pieces[i].Near.Select(c => new ColorRgba((byte)Math.Min(255, avg.R * c.R / 128), (byte)Math.Min(255, avg.G * c.G / 128), (byte)Math.Min(255, avg.B * c.B / 128)))];
        }
        var sectorOf = Sectors(surfaces);
        int sectors = sectorOf.Max() + 1;
        if (sectors > NativeAuthoredMeshRenderer.MaxSectors)
            throw new CrashLevelImportException($"{level.Name} needs {sectors} terrain sectors; the game holds {NativeAuthoredMeshRenderer.MaxSectors}. Import fewer zones.");
        surfaces = [.. surfaces.Select((s, i) => s with { Sector = sectorOf[i] })];
        var farMeshes = Enumerable.Range(0, sectors).Select(k =>
        {
            var members = Enumerable.Range(0, surfaces.Count).Where(i => sectorOf[i] == k).ToList();
            return DistantMesh(k, members.Select(i => surfaces[i]).ToList(), members.Select(Untextured).ToList()) is { } m ? m with { TwoSided = options.DrawBothSides } : null;
        }).OfType<AuthoredFarMesh>().ToList();

        // 12. The scene and its start.
        var sky = data.Scenery.Values.Where(s => s.IsSky && s.VertexColours.Count > 0).SelectMany(s => s.VertexColours).ToList();
        var doc = SceneDocument.New(level.SpyroName) with
        {
            TargetBinding = binding, Surfaces = surfaces, CollisionFaces = hidden.Count > 0 ? hidden : null, FarMeshes = farMeshes,
            SkyColor = sky.Count > 0 ? new((byte)sky.Average(c => c.R), (byte)sky.Average(c => c.G), (byte)sky.Average(c => c.B)) : new(96, 140, 220),
        };
        string spawnSource;
        int forward = (int)Math.Round(Math.Atan2(Plan(0, -1).Y, Plan(0, -1).X) / (2 * Math.PI) * 256) & 255;
        if (options.Spawn is { } chosenSpawn) { doc = doc with { Spawn = chosenSpawn, Heading = options.Heading ?? forward }; spawnSource = "chosen by the caller"; }
        else
        {
            // Crash's own start, on the floor under it. A doorway arrival needs 384 units of floor around the start;
            // where Crash's path is narrower (or runs diagonally) an undrawn landing pad just under the floor gives it.
            // Crash's start, or for a later part of the path the first point of Crash's way through it.
            Vector3f? anchor = playerStart is { } s0 ? Up(Editor(s0.X, s0.Y, s0.Z)) : route.Count > 0 ? route[0] : null;
            string anchorName = playerStart != null ? "Crash's start" : "where Crash enters the first zone";
            (double X, double Z) CrashXZ(double ex, double ey)
            {
                double px = (ex + ox) / scale, py = (ey + oy) / scale;
                return (px * cos + py * sin, -(-px * sin + py * cos));
            }
            (Vector3f Spawn, string How, AuthoredCollisionFace? Pad)? atStart = anchor is { } a0 ? StartAt(doc, a0, anchorName, (x, y) => collision.KindAt(CrashXZ(x, y))) : null;
            if (atStart is { } st)
            {
                if (st.Pad != null) { hidden.Add(st.Pad); doc = doc with { CollisionFaces = hidden }; }
                doc = doc with { Spawn = st.Spawn, Heading = options.Heading ?? forward };
                spawnSource = st.How;
            }
            else
            {
                var (spawn, how) = FindSpawn(doc, anchor, collision.Walls.Count > 0 ? hidden.Take(wallFaces).ToList() : [], anchorName);
                doc = doc with { Spawn = spawn, Heading = options.Heading ?? forward };
                spawnSource = how;
            }
        }
        doc.Validate();
        // Options with 15% fewer drawn faces of backdrop (the zones' own faces always stay), or null when none is left.
        CrashLevelImportOptions? LessBackdrop()
        {
            if (!options.TrimBackdropForDrawLoad) return null;
            int all = pieces.Sum(Drawn), inside = pieces.Where(p => p.Backdrop <= 0).Sum(Drawn);
            if (all - inside < 50) return null;
            progress?.Report("Leaving out some backdrop so the game can draw the busiest views…");
            return options with { FaceBudget = Math.Max(inside, all * 85 / 100) };
        }

        // 13. Compile as Create BIN does.
        progress?.Report("Compiling as Create BIN does…");
        byte[] bundle;
        NativeAuthoredMeshRenderer.DrawPlan? drawPlan = null;
        if (!options.CompileCheck)
        {
            var (env, est) = TerrainSize(doc, materials);
            warnings.Add($"Not compiled: drawn terrain {env:N0} bytes, about {est:N0} collision triangles (walls counted once).");
            bundle = new byte[env];
        }
        else try { bundle = NativeAuthoredMesh.Compile(doc, materials); drawPlan = NativeAuthoredMeshRenderer.LastDrawPlan; }
        catch (InvalidDataException e)
        {
            if (e.Message.StartsWith(NativeAuthoredMeshRenderer.DrawLoadRefusal, StringComparison.Ordinal) && LessBackdrop() is { } fewer)
                return await BuildAsync(data, level, binding, carriers, vram, fewer, progress, token, killHeight);
            var (env, pieces1) = TerrainSize(doc, materials);
            throw new CrashLevelImportException($"{level.Name} cannot be built as a Spyro the Dragon level: {e.Message} " +
                $"(drawn terrain {env:N0} bytes, about {pieces1:N0} collision triangles = {pieces1 * 14:N0} bytes; the game loads {NativeAuthoredMesh.MaxSurfaceBytes:N0}; " +
                $"{doc.Surfaces.Count(s => s.NoCollision)} of {doc.Surfaces.Count} faces without collision, {doc.CollisionFaces?.Count ?? 0} collision-only faces)");
        }
        var (compiledSectors, _, _) = NativeAuthoredMeshRenderer.LastReport;
        // Still over the draw budget with simpler distant meshes and a shorter texture distance: less backdrop.
        if (drawPlan is { OverBudget: true } && LessBackdrop() is { } lighter)
            return await BuildAsync(data, level, binding, carriers, vram, lighter, progress, token, killHeight);
        if (drawPlan?.Note() is { } drawNote) warnings.Add(drawNote);
        int collisionTriangles = options.CompileCheck ? System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(CollisionBlock(bundle).AsSpan(4)) : -1;
        try { NativeAuthoredSpawn.RequirePortalArrivalSupported(doc); }
        catch (InvalidDataException e) { warnings.Add("The start needs attention before Create BIN: " + e.Message); }

        // 14. Pictures.
        Directory.CreateDirectory(options.PictureFolder);
        foreach (int k in merger.Kept)
            await TerrainTexturePngWriter.WriteRgbaAsync(PicturePath(options.PictureFolder, level, k), 64, 64, pictures[k].Pixels, token);

        var report = new CrashLevelImportReport
        {
            Level = level.Name, Zones = $"{zones[0].Name}..{zones[^1].Name} ({first}-{last} of 0-{data.MainZones.Count - 1})", Scale = scale, Turn = turn,
            BackdropMargin = margin, MainFaces = mainFaces.Count, Faces = surfaces.Count, DrawnFaces = pieces.Sum(Drawn), Triangles = surfaces.Count(s => s.Vertices.Length == 3),
            MergedPairs = mergedPairs, LeftOut = leftOut, TwoSidedFaces = surfaces.Count(s => s.TwoSided), ReversedFaces = surfaces.Count(s => s.FrontReversed),
            Pictures = use.Count, KeptPictures = merger.Kept.Count, Merges = merger.Merges(), Carriers = carriers.Count, FreeTextureBytes = free,
            Sectors = sectors, NativeSectors = compiledSectors, WallFaces = wallFaces, WallLength = wallLength, PitFloors = pitFloors, NoCollisionFaces = noCollision, CatchFloors = catchFloors, Skirts = skirts,
            PlayableCells = collision.PlayableCells, CollisionTriangles = collisionTriangles, TerrainBytes = bundle.Length, DrawLoad = drawPlan?.Describe() ?? "",
            Spawn = doc.Spawn, Heading = doc.Heading, SpawnSource = spawnSource, Lift = lift, Extent = new(maxX, maxY, maxZ), Warnings = warnings,
            Route = route, RouteLength = route.Zip(route.Skip(1)).Sum(p => Math.Sqrt((p.First.X - p.Second.X) * (p.First.X - p.Second.X) + (p.First.Y - p.Second.Y) * (p.First.Y - p.Second.Y))),
        };
        return new(doc, report, materials);
    }

    /// <summary>The drawn terrain's bytes (sectors and view lists) and an estimate of the collision triangles (each face
    /// split into pieces of at most 255 units per axis, as the compiler splits textured faces).</summary>
    internal static (int Drawn, int CollisionTriangles) TerrainSize(SceneDocument doc, IReadOnlyList<AuthoredMaterial> materials)
    {
        int drawn;
        try { var (env, vis) = NativeAuthoredMeshRenderer.Compile(doc, materials, []); drawn = env.Length + vis.Length; }
        catch (InvalidDataException) { drawn = -1; }
        int pieces = 0;
        IEnumerable<Vector3f[]> Triangles(Vector3f[] v) => v.Length == 4 ? [[v[0], v[1], v[2]], [v[0], v[2], v[3]]] : [v];
        foreach (var t in doc.Surfaces.Where(s => !s.NoCollision).SelectMany(s => Triangles(s.Vertices)).Concat((doc.CollisionFaces ?? []).SelectMany(c => Triangles(c.Vertices))))
        {
            float ext = Math.Max(t.Max(p => p.X) - t.Min(p => p.X), Math.Max(t.Max(p => p.Y) - t.Min(p => p.Y), t.Max(p => p.Z) - t.Min(p => p.Z)));
            int d = Math.Max(1, (int)Math.Ceiling(ext / 254)); pieces += d * d;
        }
        return (drawn, pieces);
    }

    /// <summary>The collision component of a compiled bundle (its triangle count is at +4).</summary>
    private static byte[] CollisionBlock(byte[] bundle)
    {
        // environment, visibility, special surfaces, collision, sky: each starts with its own length.
        int at = 0;
        for (int i = 0; i < 3; i++) at += System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bundle.AsSpan(at));
        int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bundle.AsSpan(at));
        return bundle.AsSpan(at, length).ToArray();
    }

    /// <summary>Where a picture is written: &lt;folder&gt;/&lt;level&gt;-NNN.png.</summary>
    public static string PicturePath(string folder, CrashLevel level, int picture) =>
        Path.GetFullPath(Path.Combine(folder, $"{Slug(level.Name)}-{picture:000}.png"));
    public static string Slug(string name) => string.Join('-', new string([.. name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : ' ')])
        .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    private static string StableId(string key) => new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16)).ToString("N");

    // ---- texture corners -------------------------------------------------------------------------------------------
    /// <summary>Scene corner k of a quad shows the picture's TR, TL, BL, BR; a triangle's corners show TL, BL, BR.</summary>
    private static readonly string[] Cycle = ["TR", "TL", "BL", "BR"], TriangleCycle = ["TL", "BL", "BR"];
    /// <summary>The eight turns and mirrors of a picture (swap the axes, then mirror x, then mirror y).</summary>
    private static readonly (bool Swap, bool FlipX, bool FlipY)[] Dihedral =
        [.. from swap in new[] { false, true } from fx in new[] { false, true } from fy in new[] { false, true } select (swap, fx, fy)];
    private static string MapLabel(string label, (bool Swap, bool FlipX, bool FlipY) op)
    {
        int x = label[1] == 'L' ? 0 : 1, y = label[0] == 'T' ? 0 : 1;
        if (op.Swap) (x, y) = (y, x);
        if (op.FlipX) x = 1 - x;
        if (op.FlipY) y = 1 - y;
        return (y == 0 ? "T" : "B") + (x == 0 ? "L" : "R");
    }
    /// <summary>Which corner of its texture region each face corner samples (null when a corner is inside the region or
    /// two corners sample the same one).</summary>
    private static string[]? Labels(CrashModel.Texture t, int n)
    {
        int u0 = t.U.Min(), u1 = t.U.Max(), v0 = t.V.Min(), v1 = t.V.Max();
        var labels = new string[n];
        for (int i = 0; i < n; i++)
        {
            int u = t.U[i], v = t.V[i];
            if (u != u0 && u != u1 || v != v0 && v != v1) return null;
            labels[i] = (v == v0 ? "T" : "B") + (u == u0 ? "L" : "R");
        }
        return labels.Distinct().Count() == n ? labels : null;
    }

    private static Vector3f? Newell(IReadOnlyList<Vector3f> p)
    {
        double nx = 0, ny = 0, nz = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            nx += (a.Y - b.Y) * (a.Z + b.Z); ny += (a.Z - b.Z) * (a.X + b.X); nz += (a.X - b.X) * (a.Y + b.Y);
        }
        double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return l < 1e-9 ? null : new((float)(nx / l), (float)(ny / l), (float)(nz / l));
    }

    /// <summary>A quad whose corners turn the same way round seen along its average normal (as the compiler draws an
    /// imported quad whole; otherwise it draws two triangles).</summary>
    internal static bool ConvexQuad(Vector3f[] p)
    {
        double nx = 0, ny = 0, nz = 0;
        for (int i = 0; i < 4; i++) { var a = p[i]; var b = p[(i + 1) % 4]; nx += (a.Y - b.Y) * (a.Z + b.Z); ny += (a.Z - b.Z) * (a.X + b.X); nz += (a.X - b.X) * (a.Y + b.Y); }
        double[] n = [nx, ny, nz];
        if (n.All(x => x == 0)) return false;
        int drop = Enumerable.Range(0, 3).MaxBy(i => Math.Abs(n[i])); int[] axes = [.. Enumerable.Range(0, 3).Where(i => i != drop)];
        double C(Vector3f v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
        int sign = 0;
        for (int i = 0; i < 4; i++)
        {
            var x = p[i]; var y = p[(i + 1) % 4]; var z = p[(i + 2) % 4];
            double cross = (C(y, axes[0]) - C(x, axes[0])) * (C(z, axes[1]) - C(y, axes[1])) - (C(y, axes[1]) - C(x, axes[1])) * (C(z, axes[0]) - C(y, axes[0]));
            if (cross == 0 || sign != 0 && Math.Sign(cross) != sign) return false; sign = Math.Sign(cross);
        }
        return true;
    }

    /// <summary>Joins triangle pairs: two triangles of the same texture region sharing an edge whose corners sample the
    /// same texture corners in both, together sampling all four, become the quad TR, TL, BL, BR when the game draws that
    /// quad as the same two triangles (its diagonal TR-BL is the shared edge, or the four corners are coplanar) and
    /// draws it whole (convex). Returns the number of pairs joined.</summary>
    private static int MergePairs(List<Piece> pieces, Dictionary<string, (CrashModel.Texture Tex, string[] Labels, CrashTextureImage Image, CrashScenery Sc)> source,
        Func<CrashScenery, CrashModel.Texture, CrashTextureImage, int> identityPicture)
    {
        var tris = pieces.Where(p => p.Points.Length == 3 && source.ContainsKey(p.Key)).ToList();
        Dictionary<(Vector3f, Vector3f), List<Piece>> edges = [];
        (Vector3f, Vector3f) E(Vector3f a, Vector3f b) => Compare(a, b) <= 0 ? (a, b) : (b, a);
        foreach (var t in tris)
            for (int i = 0; i < 3; i++)
            {
                var k = E(t.Points[i], t.Points[(i + 1) % 3]);
                if (!edges.TryGetValue(k, out var l)) edges[k] = l = [];
                l.Add(t);
            }
        HashSet<Piece> used = [];
        List<Piece> added = [];
        foreach (var t in tris)
        {
            if (used.Contains(t)) continue;
            var (tex, la, image, sc) = source[t.Key];
            for (int i = 0; i < 3 && !used.Contains(t); i++)
            {
                var p = t.Points[i]; var q = t.Points[(i + 1) % 3];
                foreach (var u in edges[E(p, q)])
                {
                    if (u == t || used.Contains(u)) continue;
                    var (tu, lu, _, _) = source[u.Key];
                    if (!SameRegion(tex, tu) || sc.PageOf(tex) != source[u.Key].Sc.PageOf(tu)) continue;
                    int pu = Array.IndexOf(u.Points, p), qu = Array.IndexOf(u.Points, q);
                    if (pu < 0 || qu < 0 || lu[pu] != la[i] || lu[qu] != la[(i + 1) % 3]) continue;
                    var a = t.Points[(i + 2) % 3]; string labelA = la[(i + 2) % 3];
                    int bi = 3 - pu - qu; var b = u.Points[bi]; string labelB = lu[bi];
                    var labels = new Dictionary<string, (Vector3f P, ColorRgba C, (double, double) X)>
                    {
                        [la[i]] = (p, t.Near[i], t.Crash[i]), [la[(i + 1) % 3]] = (q, t.Near[(i + 1) % 3], t.Crash[(i + 1) % 3]),
                        [labelA] = (a, t.Near[(i + 2) % 3], t.Crash[(i + 2) % 3]), [labelB] = (b, u.Near[bi], u.Crash[bi]),
                    };
                    if (labels.Count != 4) continue;
                    var quad = Cycle.Select(l => labels[l].P).ToArray();
                    bool diagonal = (la[i] is "TR" or "BL") && (la[(i + 1) % 3] is "TR" or "BL");
                    if (!diagonal && !Coplanar(quad)) continue;
                    if (!ConvexQuad(quad)) continue;
                    var n = Newell(quad);
                    if (n == null) continue;
                    used.Add(t); used.Add(u);
                    added.Add(new Piece
                    {
                        Key = t.Key + "+" + u.Key, Points = quad, Near = [.. Cycle.Select(l => labels[l].C)], Picture = identityPicture(sc, tex, image),
                        Backdrop = Math.Min(t.Backdrop, u.Backdrop), Normal = n.Value, Crash = [.. Cycle.Select(l => labels[l].X)],
                    });
                    break;
                }
            }
        }
        pieces.RemoveAll(used.Contains);
        pieces.AddRange(added);
        return added.Count;
    }
    private static int Compare(Vector3f a, Vector3f b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z.CompareTo(b.Z);
    private static bool SameRegion(CrashModel.Texture a, CrashModel.Texture b) =>
        a.U.Min() == b.U.Min() && a.U.Max() == b.U.Max() && a.V.Min() == b.V.Min() && a.V.Max() == b.V.Max() &&
        a.ClutX == b.ClutX && a.ClutY == b.ClutY && a.Mode == b.Mode && a.Segment == b.Segment && a.Page == b.Page;
    private static bool Coplanar(Vector3f[] q)
    {
        var n = Newell(q);
        if (n == null) return false;
        double d0 = n.Value.X * q[0].X + n.Value.Y * q[0].Y + n.Value.Z * q[0].Z;
        return q.All(p => Math.Abs(n.Value.X * p.X + n.Value.Y * p.Y + n.Value.Z * p.Z - d0) <= 0.5);
    }

    // ---- sides -----------------------------------------------------------------------------------------------------
    /// <summary>Floor heights by position (faces at most 60 degrees from level), for the sides of walls and the start.</summary>
    private sealed class FloorGrid
    {
        private const int Cell = 32;
        private readonly Dictionary<(int, int), List<(Vector3f A, Vector3f B, Vector3f C)>> _grid = [];
        public FloorGrid(IEnumerable<Vector3f[]> faces)
        {
            foreach (var f in faces)
                foreach (var (a, b, c) in f.Length == 4 ? new[] { (f[0], f[1], f[2]), (f[0], f[2], f[3]) } : [(f[0], f[1], f[2])])
                    for (int gx = (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X)) / Cell); gx <= (int)Math.Floor(Math.Max(a.X, Math.Max(b.X, c.X)) / Cell); gx++)
                        for (int gy = (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y)) / Cell); gy <= (int)Math.Floor(Math.Max(a.Y, Math.Max(b.Y, c.Y)) / Cell); gy++)
                        {
                            if (!_grid.TryGetValue((gx, gy), out var l)) _grid[(gx, gy)] = l = [];
                            l.Add((a, b, c));
                        }
        }
        public IEnumerable<double> Heights(double x, double y)
        {
            if (!_grid.TryGetValue(((int)Math.Floor(x / Cell), (int)Math.Floor(y / Cell)), out var l)) yield break;
            foreach (var (a, b, c) in l)
            {
                double det = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
                if (det == 0) continue;
                double l1 = ((b.X - x) * (c.Y - y) - (c.X - x) * (b.Y - y)) / det, l2 = ((c.X - x) * (a.Y - y) - (a.X - x) * (c.Y - y)) / det, l3 = 1 - l1 - l2;
                if (l1 < -1e-6 || l2 < -1e-6 || l3 < -1e-6) continue;
                yield return l1 * a.Z + l2 * b.Z + l3 * c.Z;
            }
        }
        /// <summary>The highest floor at (x, y) no higher than <paramref name="top"/> (null: none).</summary>
        public double? Below(double x, double y, double top)
        {
            double? best = null;
            foreach (double z in Heights(x, y)) if (z <= top && (best == null || z > best)) best = z;
            return best;
        }
    }

    /// <summary>The prototype's side rules (export_crash.py): a wall (at most 60 degrees from vertical) whose ground 24 units
    /// out (the highest floor at most 24 units above the wall's top) is clearly lower on one side shows that side; otherwise slopes and floors face up, the walls of a small connected
    /// object (at most 600 units across) face out from its middle and other walls face the nearest point of the zones'
    /// path. See-through faces (not water) and floors with other floor 30-280 units below them are drawn from both sides.</summary>
    private static void ChooseSides(List<Piece> pieces, List<Picture> pictures, FloorGrid floors, List<(float X, float Y)> path)
    {
        var vertical = pieces.Where(p => Math.Abs(p.Normal.Z) < 0.25).ToList();
        var parent = vertical.ToDictionary(p => p, p => p);
        Piece Find(Piece a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        Dictionary<Vector3f, Piece> owner = [];
        foreach (var p in vertical)
            foreach (var v in p.Points)
                if (owner.TryGetValue(v, out var o)) parent[Find(p)] = Find(o); else owner[v] = p;
        Dictionary<Piece, (double X, double Y)> centreOf = [];
        foreach (var g in vertical.GroupBy(Find))
        {
            var pts = g.SelectMany(p => p.Points).ToList();
            double ext = Math.Max(pts.Max(v => v.X) - pts.Min(v => v.X), pts.Max(v => v.Y) - pts.Min(v => v.Y));
            if (ext > 600) continue;
            var c = (pts.Average(v => v.X), pts.Average(v => v.Y));
            foreach (var p in g) centreOf[p] = c;
        }
        (double X, double Y) Nearest(double x, double y)
        {
            if (path.Count == 1) return path[0];
            (double D, double X, double Y) best = (double.MaxValue, 0, 0);
            for (int i = 0; i + 1 < path.Count; i++)
            {
                var a = path[i]; var b = path[i + 1];
                double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                double t = l2 == 0 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / l2, 0, 1);
                double qx = a.X + t * dx, qy = a.Y + t * dy, d = (qx - x) * (qx - x) + (qy - y) * (qy - y);
                if (d < best.D) best = (d, qx, qy);
            }
            return (best.X, best.Y);
        }
        foreach (var p in pieces)
        {
            var n = p.Normal; var c = p.Centre;
            bool? frontShows = null;
            if (Math.Abs(n.Z) < 0.5)
            {
                // Floors up to a little above the face's top count: the top of the step or slab this face is the side of
                // (a slab's own top would otherwise be missed, its side taken for one facing the void under it).
                double top = p.Points.Max(v => v.Z) + 24, l = Math.Sqrt(n.X * n.X + n.Y * n.Y);
                if (l > 0)
                {
                    double front = floors.Below(c.X + 24 * n.X / l, c.Y + 24 * n.Y / l, top) ?? -1e9, back = floors.Below(c.X - 24 * n.X / l, c.Y - 24 * n.Y / l, top) ?? -1e9;
                    if (Math.Abs(front - back) > 12) frontShows = front < back;
                }
            }
            if (frontShows == null)
            {
                double wx, wy, wz;
                if (Math.Abs(n.Z) >= 0.25) (wx, wy, wz) = (0, 0, 1);
                else if (centreOf.TryGetValue(p, out var oc)) (wx, wy, wz) = (c.X - oc.X, c.Y - oc.Y, 0);
                else { var q = Nearest(c.X, c.Y); (wx, wy, wz) = (q.X - c.X, q.Y - c.Y, 0); }
                if (wx == 0 && wy == 0 && wz == 0) (wx, wy, wz) = (n.X, n.Y, n.Z);
                frontShows = n.X * wx + n.Y * wy + n.Z * wz >= 0;
            }
            p.Reversed = !frontShows.Value;
            p.TwoSided = pictures[p.Picture].SeeThrough > 0.05 && !p.Water;
        }
        // Overhangs: a level face with other level floor 30-280 units below its middle shows both sides.
        const int Cell = 48;
        Dictionary<(int, int), List<double>> grid = [];
        foreach (var p in pieces.Where(p => p.Horizontal))
        {
            var c = p.Centre; var k = ((int)Math.Floor(c.X / Cell), (int)Math.Floor(c.Y / Cell));
            if (!grid.TryGetValue(k, out var l)) grid[k] = l = [];
            l.Add(c.Z);
        }
        foreach (var p in pieces.Where(p => p.Horizontal && !p.TwoSided))
        {
            var c = p.Centre;
            if (grid[((int)Math.Floor(c.X / Cell), (int)Math.Floor(c.Y / Cell))].Any(z => c.Z - z is >= 30 and <= 280)) p.TwoSided = true;
        }
    }

    // ---- sectors ---------------------------------------------------------------------------------------------------
    /// <summary>A sector's distant (untextured) mesh: its faces' corners merged on a grid (the cell grows until at most 64
    /// corners remain and the far tables fit their one-byte offset), each merged corner coloured by the average of its
    /// corners' untextured colours (the picture's average times the near colour); faces that collapse are dropped. Drawn
    /// always (<see cref="AuthoredFarMesh.Always"/>). Null when nothing is left.</summary>
    private static AuthoredFarMesh? DistantMesh(int sector, IReadOnlyList<AuthoredSurface> faces, IReadOnlyList<ColorRgba[]> colours)
    {
        var all = faces.SelectMany(f => f.Vertices).ToList();
        float x0 = all.Min(p => p.X), y0 = all.Min(p => p.Y), z0 = all.Min(p => p.Z);
        foreach (float cell in new[] { 4f, 8, 12, 16, 24, 32, 48, 64, 96, 128, 192, 256, 384, 512, 768, 1024, 1536, 2048, 4096 })
        {
            Dictionary<(int, int, int), int> ids = [];
            List<(double X, double Y, double Z, double R, double G, double B, int N)> sums = [];
            List<int[]> list = []; HashSet<string> seen = [];
            for (int f = 0; f < faces.Count; f++)
            {
                var v = faces[f].Vertices;
                int[] order = faces[f].FrontReversed ? [.. Enumerable.Range(0, v.Length).Reverse()] : [.. Enumerable.Range(0, v.Length)];
                List<int> ring = [];
                foreach (int k in order)
                {
                    var key = ((int)((v[k].X - x0) / cell), (int)((v[k].Y - y0) / cell), (int)((v[k].Z - z0) / cell));
                    if (!ids.TryGetValue(key, out int id)) { id = sums.Count; ids[key] = id; sums.Add(default); }
                    var c = colours[f][k]; var t = sums[id];
                    sums[id] = (t.X + v[k].X, t.Y + v[k].Y, t.Z + v[k].Z, t.R + c.R, t.G + c.G, t.B + c.B, t.N + 1);
                    if (ring.Count == 0 || ring[^1] != id) ring.Add(id);
                }
                if (ring.Count > 1 && ring[0] == ring[^1]) ring.RemoveAt(ring.Count - 1);
                if (ring.Count < 3 || ring.Distinct().Count() != ring.Count || !seen.Add(string.Join(",", ring.Order()))) continue;
                list.Add([.. ring]);
            }
            var used = list.SelectMany(f => f).Distinct().Order().ToList();
            if (list.Count == 0) return null;
            if (used.Count > 64 || 2 * used.Count + 2 * list.Count > 255) continue;
            var remap = used.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var vertices = used.Select(id => { var t = sums[id]; return new Vector3f(MathF.Round((float)(t.X / t.N)), MathF.Round((float)(t.Y / t.N)), MathF.Round((float)(t.Z / t.N))); }).ToArray();
            var cols = used.Select(id => { var t = sums[id]; return new ColorRgba((byte)Math.Round(t.R / t.N), (byte)Math.Round(t.G / t.N), (byte)Math.Round(t.B / t.N)); }).ToArray();
            var farFaces = list.Select(f => { int[] k = [.. f.Select(id => remap[id])]; return new AuthoredFarFace(k, k); })
                .Where(f => f.Corners.Select(i => vertices[i]).Distinct().Count() == f.Corners.Length).ToList();
            if (farFaces.Count == 0) return null;
            return new AuthoredFarMesh(sector, vertices, cols, farFaces) { Always = true };
        }
        return null;
    }

    /// <summary>Sector numbers: the faces are cut in halves along their longest axis (at the median, or at the middle of
    /// the span when only too wide) until each part fits one native sector (at most 255 drawn faces, vertices and colour
    /// pairs, 170 vertices; spans 2,047 x 2,047 x 1,023, together 8,191) and spans at most <paramref name="span"/> across; the span
    /// grows until the parts fit the game's sector count with room for the editor's own faces.</summary>
    internal static int[] Sectors(IReadOnlyList<AuthoredSurface> surfaces, int maxSectors = NativeAuthoredMeshRenderer.MaxSectors - 7)
    {
        int[] result = new int[surfaces.Count];
        for (double span = 640; ; span *= 1.25)
        {
            List<List<int>> parts = [];
            Split([.. Enumerable.Range(0, surfaces.Count)], surfaces, span, parts);
            if (parts.Count <= maxSectors || span > 8192)
            {
                for (int k = 0; k < parts.Count; k++) foreach (int i in parts[k]) result[i] = k;
                return result;
            }
        }
    }
    private static void Split(List<int> faces, IReadOnlyList<AuthoredSurface> surfaces, double span, List<List<int>> output)
    {
        var pts = faces.SelectMany(i => surfaces[i].Vertices).ToArray();
        double[] ext = [pts.Max(p => p.X) - pts.Min(p => p.X), pts.Max(p => p.Y) - pts.Min(p => p.Y), pts.Max(p => p.Z) - pts.Min(p => p.Z)];
        bool fits = FitsSector(faces.Select(i => surfaces[i]));
        if (fits && Math.Max(ext[0], ext[1]) <= span || faces.Count == 1)
        {
            if (!fits) throw new CrashLevelImportException("A face is too large for a native sector.");
            output.Add(faces); return;
        }
        double[] over = [ext[0] / Math.Min(2047, span), ext[1] / Math.Min(2047, span), ext[2] / 1023];
        if (fits) over[2] = 0;
        int axis = over[0] >= over[1] && over[0] >= over[2] ? 0 : over[1] >= over[2] ? 1 : 2;
        double C(int i) => surfaces[i].Vertices.Average(p => axis == 0 ? p.X : axis == 1 ? p.Y : p.Z);
        var ordered = faces.OrderBy(C).ThenBy(i => i).ToList();
        int half = ordered.Count / 2;
        Split(ordered[..half], surfaces, span, output); Split(ordered[half..], surfaces, span, output);
    }
    /// <summary>The compiler's test for one imported sector (NativeAuthoredMeshRenderer.ImportedFits), on drawn faces.</summary>
    internal static bool FitsSector(IEnumerable<AuthoredSurface> surfaces)
    {
        int faces = 0; HashSet<Vector3f> v = []; HashSet<(ColorRgba, ColorRgba, ColorRgba)> colours = [];
        foreach (var s in surfaces)
        {
            faces += s.Vertices.Length == 4 && !ConvexQuad(s.Vertices) ? 2 : 1;
            var near = s.NearColors ?? [.. Enumerable.Repeat(s.Color, s.Vertices.Length)];
            var far = s.FarColors ?? near; var distant = s.DistantColors ?? far;
            for (int k = 0; k < s.Vertices.Length; k++) { v.Add(s.Vertices[k]); colours.Add((near[k], far[k], distant[k])); }
        }
        if (faces > 255 || v.Count > NativeAuthoredMeshRenderer.MaxNearVertices || colours.Count > 255) return false;
        float dx = v.Max(p => p.X) - v.Min(p => p.X), dy = v.Max(p => p.Y) - v.Min(p => p.Y), dz = v.Max(p => p.Z) - v.Min(p => p.Z);
        return dx <= 2047 && dy <= 2047 && dz <= 1023 && dx + dy + dz <= 8191;
    }

    // ---- Crash's collision -----------------------------------------------------------------------------------------
    /// <summary>A wall from (X0, Z0) to (X1, Z1) (Crash units) from <paramref name="Bottom"/> to above <paramref name="Top"/>.</summary>
    internal sealed record Wall(double X0, double Z0, double X1, double Z1, double Bottom, double Top);
    /// <summary>A deadly floor over a pit: its corners (Crash x, z) and height.</summary>
    internal sealed record Pit((double X, double Z)[] Corners, double Height);
    internal sealed record CollisionPlan(IReadOnlyList<Wall> Walls, IReadOnlyList<Pit> Pits, int PlayableCells, bool[,] Playable, double X0, double Z0)
    {
        /// <summary>Crash's way through the level: from his start to the walkable ground farthest along from it, keeping
        /// to the middle of the path (Crash x, height, z; a point every 8 samples). Empty without a start.</summary>
        public IReadOnlyList<(double X, double Y, double Z)> Route { get; init; } = [];
        /// <summary>Samples within <see cref="ReachSamples"/> of walkable ground (or of a pit).</summary>
        public required bool[,] Near { get; init; }
        /// <summary>The sample at a Crash x, z: 0 blocked or outside, 1 walkable, 2 pit.</summary>
        public int KindAt((double X, double Z) p)
        {
            int i = (int)Math.Floor((p.X - X0) / CollisionStep), j = (int)Math.Floor((p.Z - Z0) / CollisionStep);
            return i >= 0 && j >= 0 && i < Kinds.GetLength(0) && j < Kinds.GetLength(1) ? Kinds[i, j] : 0;
        }
        public required byte[,] Kinds { get; init; }
        /// <summary>A walkable sample's (highest) ground height, Crash units.</summary>
        public required double[,] Heights { get; init; }
        /// <summary>Whether any point of a face (corners in Crash x, z) lies near walkable ground.</summary>
        public bool Reachable((double X, double Z)[] corners)
        {
            double g = CollisionStep;
            bool At(double x, double z)
            {
                int i = (int)Math.Floor((x - X0) / g), j = (int)Math.Floor((z - Z0) / g);
                return i >= 0 && j >= 0 && i < Near.GetLength(0) && j < Near.GetLength(1) && Near[i, j];
            }
            var tris = corners.Length == 4 ? new[] { (corners[0], corners[1], corners[2]), (corners[0], corners[2], corners[3]) } : [(corners[0], corners[1], corners[2])];
            foreach (var (a, b, c) in tris)
            {
                double edge = Math.Max(Math.Max(Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z), Math.Abs(b.X - c.X) + Math.Abs(b.Z - c.Z)), Math.Abs(c.X - a.X) + Math.Abs(c.Z - a.Z));
                int n = Math.Max(1, (int)Math.Ceiling(edge / (g / 2)));
                for (int i = 0; i <= n; i++)
                    for (int j = 0; i + j <= n; j++)
                    {
                        double u = i / (double)n, v = j / (double)n, w = 1 - u - v;
                        if (At(a.X * w + b.X * u + c.X * v, a.Z * w + b.Z * u + c.Z * v)) return true;
                    }
            }
            return false;
        }
    }
    /// <summary>Faces this many collision samples (400 Crash units, 46 editor units at crate scale) from walkable ground
    /// keep their collision; farther ones are beyond the walls' reach.</summary>
    public const int ReachSamples = 4;

    /// <summary>Samples Crash's zone collision every <see cref="CollisionStep"/> units: a sample is walkable where a ground
    /// leaf (or a plain solid one, kind 0) has empty space above it below the zone's ceiling, a pit where a deadly leaf lies under no walkable ground, otherwise blocked (solid,
    /// outside the chosen zones). Walls follow the edges between walkable and blocked samples (traced, then straightened
    /// within 2.5 samples, so the octree cells' staircase becomes straight walls); pits get a floor per run of pit samples
    /// a little below the deadly leaves' top.</summary>
    internal static CollisionPlan Collision(IReadOnlyList<CrashZone> zones, IReadOnlyList<CrashZone> all, CrashLevelImportOptions options, double scale, CrashPoint? start = null)
    {
        double g = CollisionStep;
        double x0 = zones.Min(z => z.Box.X) - g, x1 = zones.Max(z => z.Box.X + z.Box.W) + g;
        double z0 = zones.Min(z => z.Box.Z) - g, z1 = zones.Max(z => z.Box.Z + z.Box.D) + g;
        int nx = (int)Math.Ceiling((x1 - x0) / g), nz = (int)Math.Ceiling((z1 - z0) / g);
        var kind = new byte[nx, nz];          // 0 blocked, 1 walkable, 2 pit
        var high = new double[nx, nz]; var low = new double[nx, nz]; var pitTop = new double[nx, nz];
        var chosen = zones.ToHashSet();
        Parallel.For(0, nx, i =>
        {
            for (int j = 0; j < nz; j++)
            {
                double x = x0 + (i + 0.5) * g, z = z0 + (j + 0.5) * g;
                var zone = all.FirstOrDefault(zz => zz.Box.ContainsXZ(x, z));
                if (zone == null || !chosen.Contains(zone)) continue;
                var column = zone.Collision.Column(x, z);
                double best = double.NaN, lowest = double.NaN, deadly = double.NaN;
                foreach (var leaf in column)
                {
                    if (leaf.IsDeadly) deadly = double.IsNaN(deadly) ? leaf.Top : Math.Max(deadly, leaf.Top);
                    // Crash stands on ground leaves, and on the top of plain solid ones (kind 0: some arenas have no
                    // ground kind at all); never on the zone's ceiling (a wall reaching the top of the zone).
                    if (!leaf.IsGround && leaf.Kind != 0) continue;
                    double top = leaf.Top;
                    if (top >= zone.Box.Y + zone.Box.H - 1) continue;
                    // Covered: another leaf starts at (or just above) this one's top.
                    if (column.Any(o => !o.Equals(leaf) && o.Y <= top + 1 && o.Top > top + 1)) continue;
                    best = double.IsNaN(best) ? top : Math.Max(best, top);
                    lowest = double.IsNaN(lowest) ? top : Math.Min(lowest, top);
                }
                if (!double.IsNaN(best)) { kind[i, j] = 1; high[i, j] = best; low[i, j] = lowest; }
                else if (!double.IsNaN(deadly) && options.DeadlyPits) { kind[i, j] = 2; pitTop[i, j] = deadly; }
            }
        });
        // Small islands of walkable ground (fewer than 16 samples: tops of posts, ledges Crash never reaches) are blocked.
        var seen = new bool[nx, nz];
        int playable = 0;
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if (kind[i, j] != 1 || seen[i, j]) continue;
                List<(int, int)> comp = []; var stack = new Stack<(int, int)>(); stack.Push((i, j)); seen[i, j] = true;
                while (stack.Count > 0)
                {
                    var (a, b) = stack.Pop(); comp.Add((a, b));
                    foreach (var (c, d) in new[] { (a + 1, b), (a - 1, b), (a, b + 1), (a, b - 1) })
                        if (c >= 0 && d >= 0 && c < nx && d < nz && kind[c, d] == 1 && !seen[c, d]) { seen[c, d] = true; stack.Push((c, d)); }
                }
                if (comp.Count < 16) foreach (var (a, b) in comp) kind[a, b] = 0;
                else playable += comp.Count;
            }
        // Only the ground Spyro can reach from Crash's start is walled in: walking (down any drop, up at most ClimbUp
        // between neighbouring samples) and across pits; walkable patches cut off from it (behind Crash's walls, on top
        // of tall solid blocks) count as blocked, so the walls stand at their foot.
        if (start is { } st)
        {
            int si = (int)Math.Floor((st.X - x0) / g), sj = (int)Math.Floor((st.Z - z0) / g);
            (int, int)? seed = null;
            for (int r = 0; r <= 40 && seed == null; r++)
                for (int di = -r; di <= r && seed == null; di++)
                    for (int dj = -r; dj <= r && seed == null; dj++)
                        if (si + di >= 0 && sj + dj >= 0 && si + di < nx && sj + dj < nz && kind[si + di, sj + dj] == 1 && Math.Abs(high[si + di, sj + dj] - st.Y) <= 400)
                            seed = (si + di, sj + dj);
            if (seed is { } sd)
            {
                // level: the height Spyro is at in a sample (a pit sample keeps the height he jumped from).
                var level = new double[nx, nz];
                for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) level[i, j] = double.NaN;
                var reach = new bool[nx, nz]; var stack = new Stack<(int, int)>(); stack.Push(sd); reach[sd.Item1, sd.Item2] = true; level[sd.Item1, sd.Item2] = high[sd.Item1, sd.Item2];
                while (stack.Count > 0)
                {
                    var (a, b) = stack.Pop();
                    foreach (var (c, d) in new[] { (a + 1, b), (a - 1, b), (a, b + 1), (a, b - 1) })
                    {
                        if (c < 0 || d < 0 || c >= nx || d >= nz || kind[c, d] == 0) continue;
                        double at = kind[c, d] == 1 ? high[c, d] : level[a, b];
                        if (kind[c, d] == 1 && high[c, d] - level[a, b] > ClimbUp) continue;
                        if (reach[c, d] && !(kind[c, d] == 2 && at > level[c, d])) continue;
                        reach[c, d] = true; level[c, d] = at; stack.Push((c, d));
                    }
                }
                playable = 0;
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                        if (!reach[i, j] && kind[i, j] != 0) kind[i, j] = 0;
                        else if (kind[i, j] == 1) playable++;
            }
        }
        var walkable = new bool[nx, nz];
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) walkable[i, j] = kind[i, j] == 1;
        var route = start is { } rs ? Route(kind, high, nx, nz, (int)Math.Floor((rs.X - x0) / g), (int)Math.Floor((rs.Z - z0) / g))
            .Select(c => (x0 + (c.I + 0.5) * g, high[c.I, c.J], z0 + (c.J + 0.5) * g)).ToList() : [];

        List<Wall> walls = [];
        if (options.InvisibleWalls)
        {
            // Directed boundary edges on the sample lattice (corner (i, j) at x0 + i g, z0 + j g), walkable on the left.
            Dictionary<(int, int), List<(int, int)>> next = [];
            void Edge((int, int) a, (int, int) b) { if (!next.TryGetValue(a, out var l)) next[a] = l = []; l.Add(b); }
            bool Blocked(int i, int j) => i < 0 || j < 0 || i >= nx || j >= nz || kind[i, j] == 0;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (kind[i, j] != 1) continue;
                    if (Blocked(i, j - 1)) Edge((i, j), (i + 1, j));
                    if (Blocked(i + 1, j)) Edge((i + 1, j), (i + 1, j + 1));
                    if (Blocked(i, j + 1)) Edge((i + 1, j + 1), (i, j + 1));
                    if (Blocked(i - 1, j)) Edge((i, j + 1), (i, j));
                }
            var chains = Chains(next);
            foreach (var chain in chains)
            {
                var points = chain.Select(p => (X: x0 + p.Item1 * g, Z: z0 + p.Item2 * g)).ToList();
                // A small closed loop is an obstacle inside the walkable ground (a post, a barrel): its drawn faces
                // already stop Spyro.
                if (chain.Count > 2 && chain[0] == chain[^1] && Math.Max(points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Z) - points.Min(p => p.Z)) < SmallObstacle &&
                    LoopArea(points) < 0) continue;
                var simple = Simplify(points, g * 2.5);
                for (int k = 0; k + 1 < simple.Count; k++)
                {
                    var (ax, az) = simple[k]; var (bx, bz) = simple[k + 1];
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    int parts = Math.Max(1, (int)Math.Ceiling(len / 1500));
                    for (int m = 0; m < parts; m++)
                    {
                        double px = ax + (bx - ax) * m / parts, pz = az + (bz - az) * m / parts, qx = ax + (bx - ax) * (m + 1) / parts, qz = az + (bz - az) * (m + 1) / parts;
                        // Heights of the walkable ground within 1.5 samples of the wall.
                        double top = double.MinValue, bottom = double.MaxValue;
                        int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((qx - px) * (qx - px) + (qz - pz) * (qz - pz)) / (g / 2)));
                        for (int s = 0; s <= steps; s++)
                        {
                            double sx = px + (qx - px) * s / steps, sz = pz + (qz - pz) * s / steps;
                            for (int di = -2; di <= 1; di++)
                                for (int dj = -2; dj <= 1; dj++)
                                {
                                    int ci = (int)Math.Floor((sx - x0) / g) + di, cj = (int)Math.Floor((sz - z0) / g) + dj;
                                    if (ci < 0 || cj < 0 || ci >= nx || cj >= nz || kind[ci, cj] != 1) continue;
                                    double cx = x0 + (ci + 0.5) * g, cz = z0 + (cj + 0.5) * g;
                                    if ((cx - sx) * (cx - sx) + (cz - sz) * (cz - sz) > 2.25 * g * g) continue;
                                    top = Math.Max(top, high[ci, cj]); bottom = Math.Min(bottom, low[ci, cj]);
                                }
                        }
                        if (top == double.MinValue) continue;
                        walls.Add(new(px, pz, qx, qz, bottom - 32 / scale, top));
                    }
                }
            }
        }


        // Pit floors: runs of pit samples along x in each row, merged with the same run in the next rows.
        List<Pit> pits = [];
        if (options.DeadlyPits)
        {
            var done = new bool[nx, nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (kind[i, j] != 2 || done[i, j]) continue;
                    int e = i; while (e + 1 < nx && kind[e + 1, j] == 2 && !done[e + 1, j]) e++;
                    int f = j;
                    while (f + 1 < nz && f - j < 15 && Enumerable.Range(i, e - i + 1).All(k => kind[k, f + 1] == 2 && !done[k, f + 1]) &&
                           (i == 0 || kind[i - 1, f + 1] != 2) && (e + 1 >= nx || kind[e + 1, f + 1] != 2)) f++;
                    double h = double.MaxValue;
                    for (int a = i; a <= e; a++) for (int b = j; b <= f; b++) { done[a, b] = true; h = Math.Min(h, pitTop[a, b]); }
                    // Never above the walkable ground around it (the floor overlaps its neighbours a little).
                    for (int a = Math.Max(0, i - 1); a <= Math.Min(nx - 1, e + 1); a++)
                        for (int b = Math.Max(0, j - 1); b <= Math.Min(nz - 1, f + 1); b++)
                            if (kind[a, b] == 1) h = Math.Min(h, low[a, b] - 100 + 300);
                    // Overlap the neighbours by a quarter sample so no seam is left between runs.
                    double ax = x0 + i * g - g / 4, bx = x0 + (e + 1) * g + g / 4, az = z0 + j * g - g / 4, bz = z0 + (f + 1) * g + g / 4;
                    pits.Add(new([(ax, az), (bx, az), (bx, bz), (ax, bz)], h - 300));
                }
        }
        var near = new bool[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if (kind[i, j] == 0) continue;
                for (int di = -ReachSamples; di <= ReachSamples; di++)
                    for (int dj = -ReachSamples; dj <= ReachSamples; dj++)
                        if (di * di + dj * dj <= ReachSamples * ReachSamples && i + di >= 0 && j + dj >= 0 && i + di < nx && j + dj < nz) near[i + di, j + dj] = true;
            }
        return new(walls, pits, playable, walkable, x0, z0) { Near = near, Kinds = kind, Route = route, Heights = high };
    }

    /// <summary>Directed lattice edges (walkable ground on the left) joined into chains: open chains from their free
    /// ends first, then closed loops; at a crossing the sharpest left turn keeps the walkable ground on the left.</summary>
    private static List<List<(int, int)>> Chains(Dictionary<(int, int), List<(int, int)>> next)
    {
        var incoming = new Dictionary<(int, int), int>();
        foreach (var (a, l) in next) foreach (var b in l) incoming[b] = incoming.GetValueOrDefault(b) + 1;
        List<List<(int, int)>> chains = [];
        (int, int)? Take((int, int) at, (int, int)? from)
        {
            if (!next.TryGetValue(at, out var l) || l.Count == 0) return null;
            int pick = 0;
            if (l.Count > 1 && from is { } f)
            {
                (int dx, int dz) d = (at.Item1 - f.Item1, at.Item2 - f.Item2);
                pick = Enumerable.Range(0, l.Count).MaxBy(k => { var e = (l[k].Item1 - at.Item1, l[k].Item2 - at.Item2); return d.dx * e.Item2 - d.dz * e.Item1; });
            }
            var b = l[pick]; l.RemoveAt(pick); return b;
        }
        foreach (var startSet in new[] { next.Keys.Where(k => incoming.GetValueOrDefault(k) == 0).ToList(), next.Keys.ToList() })
            foreach (var s in startSet)
                while (next.TryGetValue(s, out var l) && l.Count > 0)
                {
                    List<(int, int)> chain = [s];
                    (int, int)? prev = null; var cur = s;
                    while (Take(cur, prev) is { } b) { chain.Add(b); prev = cur; cur = b; if (b == s) break; }
                    chains.Add(chain);
                }
        return chains;
    }

    /// <summary>The cells of the cheapest way over walkable samples (8 neighbours; height steps over 300 Crash units up
    /// not taken; cost grows near blocked samples, so the way keeps to the middle) from the walkable sample nearest
    /// (si, sj) to the one farthest from it, every 8th cell and the last.</summary>
    private static List<(int I, int J)> Route(byte[,] kind, double[,] high, int nx, int nz, int si, int sj)
    {
        (int, int)? seed = null; double best = double.MaxValue;
        for (int i = Math.Max(0, si - 10); i <= Math.Min(nx - 1, si + 10); i++)
            for (int j = Math.Max(0, sj - 10); j <= Math.Min(nz - 1, sj + 10); j++)
                if (kind[i, j] == 1 && (i - si) * (i - si) + (j - sj) * (j - sj) < best) { best = (i - si) * (i - si) + (j - sj) * (j - sj); seed = (i, j); }
        if (seed is not { } sd) return [];
        // Clearance: samples to the nearest non-walkable one (capped).
        var clear = new int[nx, nz]; var queue = new Queue<(int, int)>();
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) { if (kind[i, j] != 1) queue.Enqueue((i, j)); else clear[i, j] = int.MaxValue; }
        while (queue.Count > 0)
        {
            var (a, b) = queue.Dequeue();
            foreach (var (c, d) in new[] { (a + 1, b), (a - 1, b), (a, b + 1), (a, b - 1) })
                if (c >= 0 && d >= 0 && c < nx && d < nz && clear[c, d] == int.MaxValue) { clear[c, d] = clear[a, b] + 1; queue.Enqueue((c, d)); }
        }
        var dist = new double[nx, nz]; var from = new (int, int)[nx, nz];
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) dist[i, j] = double.MaxValue;
        var heap = new PriorityQueue<(int, int), double>(); dist[sd.Item1, sd.Item2] = 0; heap.Enqueue(sd, 0);
        (int I, int J) far = sd; double farDist = 0;
        while (heap.TryDequeue(out var cell, out double dc))
        {
            var (a, b) = cell;
            if (dc > dist[a, b]) continue;
            if (dc > farDist) { farDist = dc; far = cell; }
            for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                {
                    int c = a + di, d = b + dj;
                    if (di == 0 && dj == 0 || c < 0 || d < 0 || c >= nx || d >= nz || kind[c, d] != 1 || high[c, d] - high[a, b] > 300) continue;
                    double step = (di != 0 && dj != 0 ? 1.4142 : 1) * (1 + 6.0 / Math.Min(6, clear[c, d]));
                    if (dc + step < dist[c, d]) { dist[c, d] = dc + step; from[c, d] = (a, b); heap.Enqueue((c, d), dc + step); }
                }
        }
        List<(int I, int J)> path = [];
        for (var at = far; ; at = from[at.I, at.J]) { path.Add(at); if (at == sd) break; }
        path.Reverse();
        return [.. path.Where((_, k) => k % 8 == 0 || k == path.Count - 1)];
    }

    /// <summary>The most (Crash units) Spyro climbs from one collision sample to the next: about his jump (72 editor units
    /// at crate scale).</summary>
    public const double ClimbUp = 600;
    /// <summary>Closed loops of Crash's walls smaller than this (Crash units) inside the walkable ground get no invisible wall.</summary>
    public const double SmallObstacle = 800;
    /// <summary>Signed area of a closed chain (x, z): positive anticlockwise (the walkable ground's outer edge), negative
    /// for an obstacle inside it.</summary>
    private static double LoopArea(List<(double X, double Z)> p)
    {
        double a = 0;
        for (int i = 0; i + 1 < p.Count; i++) a += p[i].X * p[i + 1].Z - p[i + 1].X * p[i].Z;
        return a / 2;
    }

    /// <summary>Douglas-Peucker on an open or closed chain of points.</summary>
    private static List<(double X, double Z)> Simplify(List<(double X, double Z)> pts, double tolerance) =>
        [.. SimplifyIndices(pts, tolerance).Select(k => pts[k])];
    private static List<int> SimplifyIndices(List<(double X, double Z)> pts, double tolerance)
    {
        if (pts.Count < 3) return [.. Enumerable.Range(0, pts.Count)];
        var keep = new bool[pts.Count]; keep[0] = keep[^1] = true;
        bool closed = pts[0] == pts[^1];
        if (closed)
        {
            // Split a loop at its farthest point from the start.
            int far = Enumerable.Range(1, pts.Count - 2).MaxBy(k => (pts[k].X - pts[0].X) * (pts[k].X - pts[0].X) + (pts[k].Z - pts[0].Z) * (pts[k].Z - pts[0].Z));
            keep[far] = true; Mark(0, far); Mark(far, pts.Count - 1);
        }
        else Mark(0, pts.Count - 1);
        return [.. Enumerable.Range(0, pts.Count).Where(k => keep[k])];
        void Mark(int a, int b)
        {
            if (b <= a + 1) return;
            double dx = pts[b].X - pts[a].X, dz = pts[b].Z - pts[a].Z, l = Math.Sqrt(dx * dx + dz * dz);
            int worst = -1; double d = tolerance;
            for (int k = a + 1; k < b; k++)
            {
                double e = l == 0 ? Math.Sqrt((pts[k].X - pts[a].X) * (pts[k].X - pts[a].X) + (pts[k].Z - pts[a].Z) * (pts[k].Z - pts[a].Z))
                    : Math.Abs(dx * (pts[a].Z - pts[k].Z) - dz * (pts[a].X - pts[k].X)) / l;
                if (e > d) { d = e; worst = k; }
            }
            if (worst < 0) return;
            keep[worst] = true; Mark(a, worst); Mark(worst, b);
        }
    }

    // ---- start -----------------------------------------------------------------------------------------------------
    /// <summary>The start on the floor under <paramref name="anchor"/> (Crash's start), certified for a doorway arrival;
    /// when the floor around it is too narrow, with a landing pad: an undrawn 400-unit square of solid floor a little under
    /// the lowest walkable floor around the start (only where no pit lies within it and the walkable floor varies by at
    /// most 90 units, so Spyro never stands on it above the drawn floor). Where a pit is too close, the nearest walkable
    /// point (within 1,200 units) that takes a pad is used. Null when none works.</summary>
    private static (Vector3f Spawn, string How, AuthoredCollisionFace? Pad)? StartAt(SceneDocument doc, Vector3f anchor, string name, Func<double, double, int> kindAt)
    {
        var solid = new FloorGrid(doc.Surfaces.Where(s => !s.NoCollision && s.Kind == null).Select(s => s.Vertices));
        double? floor = solid.Heights(anchor.X, anchor.Y).Where(h => h <= anchor.Z + 24 && h >= anchor.Z - 64).Cast<double?>().Max();
        if (floor is double f0)
        {
            var at = new Vector3f(MathF.Round(anchor.X), MathF.Round(anchor.Y), MathF.Round((float)f0) + 32);
            try { NativeAuthoredSpawn.RequirePortalArrivalSupported(doc with { Spawn = at }); return (at, $"{name} ({anchor.X:0},{anchor.Y:0},{anchor.Z:0})", null); }
            catch (InvalidDataException) { }
        }
        const int Half = 200, Reach = 1200;
        var candidates = new List<(double D, float X, float Y)>();
        for (int dx = -Reach / 32 * 32; dx <= Reach; dx += 32)
            for (int dy = -Reach / 32 * 32; dy <= Reach; dy += 32)
                if (dx * dx + dy * dy <= Reach * Reach) candidates.Add((dx * dx + dy * dy, MathF.Round(anchor.X) + dx, MathF.Round(anchor.Y) + dy));
        int tried = 0;
        foreach (var (_, x, y) in candidates.OrderBy(c => c.D))
        {
            if (kindAt(x, y) != 1 && !(x == MathF.Round(anchor.X) && y == MathF.Round(anchor.Y))) continue;
            bool atAnchor = x == MathF.Round(anchor.X) && y == MathF.Round(anchor.Y);
            // At the anchor the floor under Crash (as above); elsewhere the floor nearest his height.
            var near = solid.Heights(x, y).Where(h => atAnchor ? h <= anchor.Z + 24 && h >= anchor.Z - 64 : h <= anchor.Z + 64 && h >= anchor.Z - 160).ToList();
            if (near.Count == 0) continue;
            double f = atAnchor ? near.Max() : near.MinBy(h => Math.Abs(h - (anchor.Z - 8)));
            double lowest = f; bool pit = false;
            for (int dx = -Half; dx <= Half && !pit; dx += 16)
                for (int dy = -Half; dy <= Half && !pit; dy += 16)
                {
                    int kind = kindAt(x + dx, y + dy);
                    if (kind == 2) { pit = true; break; }
                    if (kind != 1) continue;
                    foreach (double h in solid.Heights(x + dx, y + dy).Where(h => h <= f + 24 && h >= f - 120)) lowest = Math.Min(lowest, h);
                }
            if (pit || f - lowest > 90) continue;
            var spawn = new Vector3f(x, y, MathF.Round((float)f) + 32);
            float z = MathF.Floor((float)lowest) - 2;
            var pad = new AuthoredCollisionFace([new(x - Half, y - Half, z), new(x + Half, y - Half, z), new(x + Half, y + Half, z), new(x - Half, y + Half, z)]);
            if (++tried > 60) break;
            try { NativeAuthoredSpawn.RequirePortalArrivalSupported(doc with { Spawn = spawn, CollisionFaces = [.. doc.CollisionFaces ?? [], pad] }); }
            catch (InvalidDataException) { continue; }
            string where = x == MathF.Round(anchor.X) && y == MathF.Round(anchor.Y) ? $"{name} ({anchor.X:0},{anchor.Y:0},{anchor.Z:0})"
                : $"{Math.Sqrt((x - anchor.X) * (x - anchor.X) + (y - anchor.Y) * (y - anchor.Y)):0} units from {name} ({anchor.X:0},{anchor.Y:0},{anchor.Z:0}; the nearest point whose surroundings take a landing pad: no pit and even floor within 200 units)";
            return (spawn, $"{where}, with an undrawn 400-unit landing pad {f - z:0} units under its floor for the doorway arrival", pad);
        }
        return null;
    }

    /// <summary>The start: the floor point nearest <paramref name="anchor"/> (Crash's own start; within 1,024 units, on a
    /// floor within 160 of it) else nearest the level's middle, where a doorway arrival is certified
    /// (<see cref="NativeAuthoredSpawn.RequirePortalArrivalSupported"/>) on open, level floor not cut off from the anchor
    /// by an invisible wall. (The search mirrors the Year of the Dragon importer's.)</summary>
    private static (Vector3f Spawn, string How) FindSpawn(SceneDocument doc, Vector3f? anchor, IReadOnlyList<AuthoredCollisionFace> walls, string name = "Crash's start")
    {
        var solid = doc.Surfaces.Where(s => !s.NoCollision).ToList();
        var floors = new FloorGrid(solid.Where(s => s.Kind == null).Select(s => s.Vertices).Where(v =>
        {
            var n = Newell(v); return n != null && Math.Abs(n.Value.Z) >= 0.9;
        }));
        var all = new FloorGrid(solid.Select(s => s.Vertices));
        bool Open(double x, double y, double z) => !all.Heights(x, y).Any(h => h > z + 2 && h < z + 220);
        double? Floor(double x, double y, double? near = null)
        {
            var hs = all.Heights(x, y).Where(h => near == null || h <= near + 24 && h >= near - 64).ToList();
            if (hs.Count == 0) return null;
            double top = hs.Max();
            return floors.Heights(x, y).Any(h => Math.Abs(h - top) < 0.01) ? top : null;
        }
        bool Crosses(double ax, double ay, double bx, double by) => walls.Any(w =>
        {
            var p = w.Vertices[0]; var q = w.Vertices[1];
            double d1 = (q.X - p.X) * (ay - p.Y) - (q.Y - p.Y) * (ax - p.X), d2 = (q.X - p.X) * (by - p.Y) - (q.Y - p.Y) * (bx - p.X);
            double d3 = (bx - ax) * (p.Y - ay) - (by - ay) * (p.X - ax), d4 = (bx - ax) * (q.Y - ay) - (by - ay) * (q.X - ax);
            return d1 * d2 < 0 && d3 * d4 < 0;
        });
        float minX = doc.Surfaces.Min(s => s.Vertices.Min(v => v.X)), maxX = doc.Surfaces.Max(s => s.Vertices.Max(v => v.X));
        float minY = doc.Surfaces.Min(s => s.Vertices.Min(v => v.Y)), maxY = doc.Surfaces.Max(s => s.Vertices.Max(v => v.Y));
        if (anchor is { } a0)
        {
            if (Spot(a0.X, a0.Y, 1024, a0.Z, true) is { } s) return (s, $"the open, level floor nearest {name} (at {a0.X:0},{a0.Y:0},{a0.Z:0}) with room for a doorway arrival");
        }
        double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        if (Spot(cx, cy, double.MaxValue, null, false) is { } c) return (c, $"the open, level floor nearest the level's middle ({cx:0},{cy:0}) with room for a doorway arrival");
        throw new CrashLevelImportException("No floor in this level is level and open across the 384 units a doorway arrival needs; choose the start yourself (Spawn option).");

        Vector3f? Spot(double ox, double oy, double reach, float? height, bool sameSide)
        {
            var candidates = new List<(double D, double X, double Y)>();
            for (double x = Math.Ceiling(Math.Max(minX, ox - Math.Min(reach, 1e6)) / 16) * 16; x <= Math.Min(maxX, ox + Math.Min(reach, 1e6)); x += 16)
                for (double y = Math.Ceiling(Math.Max(minY, oy - Math.Min(reach, 1e6)) / 16) * 16; y <= Math.Min(maxY, oy + Math.Min(reach, 1e6)); y += 16)
                {
                    double d = (x - ox) * (x - ox) + (y - oy) * (y - oy);
                    if (d <= reach * reach) candidates.Add((d, x, y));
                }
            int tried = 0;
            foreach (var (_, x, y) in candidates.OrderBy(c => c.D))
            {
                if (Floor(x, y) is not double z || !Open(x, y, z)) continue;
                if (height is float h && Math.Abs(z - h) > 160) continue;
                if (sameSide && Crosses(ox, oy, x, y)) continue;
                bool flat = true;
                for (int dx = -192; dx <= 192 && flat; dx += 48)
                    for (int dy = -192; dy <= 192 && flat; dy += 48)
                        flat = Floor(x + dx, y + dy, z) is double f && Math.Abs(f - z) <= 24;
                if (!flat) continue;
                var spawn = new Vector3f((float)x, (float)y, MathF.Round((float)z + 32));
                if (++tried > 600) break;
                try { NativeAuthoredSpawn.RequirePortalArrivalSupported(doc with { Spawn = spawn }); }
                catch (InvalidDataException) { continue; }
                return spawn;
            }
            return null;
        }
    }
}
