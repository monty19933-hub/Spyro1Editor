using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.YearOfTheDragon;

/// <summary>How a Year of the Dragon level is rebuilt (see <see cref="YearOfTheDragonLevelImport"/>).</summary>
public sealed record YearOfTheDragonImportOptions
{
    /// <summary>Folder for the level's pictures (64x64 PNG, one per kept texture); the editor keeps a project's pictures
    /// under the project's custom-terrain-textures/&lt;name&gt;/. Created if missing; existing pictures of the same
    /// name are replaced.</summary>
    public required string PictureFolder { get; init; }
    /// <summary>Spyro the Dragon levels whose terrain textures may carry the pictures (null: every retail level).</summary>
    public IReadOnlyList<string>? CarrierLevels { get; init; }
    /// <summary>Year of the Dragon's burning ground (lava, Lost Fleet's acid) kills at once, as the Lost Fleet build
    /// chose; false: it only hurts (Spyro the Dragon's own lava and ooze kinds).</summary>
    public bool HotGroundIsDeadly { get; init; } = true;
    /// <summary>Year of the Dragon's swimmable water surfaces become Spyro the Dragon's water (costs a life, as all
    /// water in that game); false keeps them solid ground (Spyro walks on them).</summary>
    public bool WaterCostsALife { get; init; } = true;
    /// <summary>At most this many textures (the game's materials are numbered 0-127).</summary>
    public int MaxTextures { get; init; } = 127;
    /// <summary>Checks the pictures' carriers against the built level's free texture memory and merges textures until
    /// they fit (the check Create BIN makes). False skips it (tests without the export's bootstrap capture).</summary>
    public bool CheckTextureMemory { get; init; } = true;
    /// <summary>Texture memory (bytes) kept free after the terrain and the level's dragon rescue scenes, for the
    /// objects added later (gems, enemies, dragons); textures are merged until this much is left.</summary>
    public int TextureMemoryReserve { get; init; } = DefaultTextureMemoryReserve;
    public const int DefaultTextureMemoryReserve = 48 * 1024;
    /// <summary>Overrides the start position (editor units) and heading (0-255).</summary>
    public Vector3f? Spawn { get; init; }
    public int? Heading { get; init; }
}

/// <summary>What an import produced and what it could not keep. <see cref="Lines"/> is the readable report.</summary>
public sealed record YearOfTheDragonImportReport
{
    public required string Level { get; init; }
    public int YotdSectors { get; init; }
    public int FarOnlySectors { get; init; }
    public int SectorGroups { get; init; }
    public int NativeSectors { get; init; }
    public int YotdFaces { get; init; }
    public int Faces { get; init; }
    /// <summary>Faces drawn as triangles because two of their four corners coincide (kept).</summary>
    public int RepairedFaces { get; init; }
    /// <summary>Faces with fewer than three different corners (they draw nothing in Year of the Dragon either).</summary>
    public int EmptyFaces { get; init; }
    /// <summary>Repaired faces dropped because a face with the same corners exists.</summary>
    public int DuplicateFaces { get; init; }
    public int CollinearFaces { get; init; }
    public int TwoSidedFaces { get; init; }
    public int ReversedFaces { get; init; }
    public int Textures { get; init; }
    public int KeptTextures { get; init; }
    /// <summary>Texture -> the texture it was merged into (only merged ones).</summary>
    public IReadOnlyDictionary<int, int> Merges { get; init; } = new Dictionary<int, int>();
    public int Carriers { get; init; }
    public int FreeTextureBytes { get; init; } = -1;
    public IReadOnlyDictionary<string, int> GroundKinds { get; init; } = new Dictionary<string, int>();
    public int CollisionTriangles { get; init; }
    public Vector3f Spawn { get; init; }
    public int Heading { get; init; }
    public string SpawnSource { get; init; } = "";
    public int TerrainBytes { get; init; }
    /// <summary>The compile's draw plan (<see cref="NativeAuthoredMeshRenderer.DrawPlan.Describe"/>).</summary>
    public string DrawLoad { get; init; } = "";
    public int FarMeshes { get; init; }
    /// <summary>Year of the Dragon's sky background colour, now the scene's sky colour (null: none in the level file).</summary>
    public ColorRgba? SkyBackground { get; init; }
    /// <summary>Units the level was raised above the replaced level's kill height (0: not raised).</summary>
    public float Lift { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> Lines()
    {
        List<string> l =
        [
            $"{Level}: {Faces} faces from {YotdFaces} ({RepairedFaces} repaired triangles, {EmptyFaces} empty, {DuplicateFaces} duplicate and {CollinearFaces} flat faces left out), {TwoSidedFaces} two-sided, {ReversedFaces} reversed",
            $"sectors: {YotdSectors} in Year of the Dragon ({FarOnlySectors} distant-only), {SectorGroups} groups, {NativeSectors} of the game's 127; {FarMeshes} distant meshes",
            $"textures: {Textures} used, {KeptTextures} kept on {Carriers} carriers" + (FreeTextureBytes >= 0 ? $", {FreeTextureBytes:N0} bytes of texture memory left for objects" : "") +
                (Merges.Count > 0 ? $"; merged {string.Join(" ", Merges.OrderBy(m => m.Key).Select(m => $"{m.Key}->{m.Value}"))}" : ""),
            $"ground: {string.Join(", ", GroundKinds.Select(k => $"{k.Value} {k.Key}"))} (of {CollisionTriangles} collision triangles)",
            $"start: ({Spawn.X:0},{Spawn.Y:0},{Spawn.Z:0}) heading {Heading}, {SpawnSource}",
            $"terrain data: {TerrainBytes:N0} of {NativeAuthoredMesh.MaxSurfaceBytes:N0} bytes" +
                (SkyBackground is { } sky ? $"; sky colour ({sky.R},{sky.G},{sky.B}), Year of the Dragon's sky background (its sky shapes are not converted)" : ""),
        ];
        if (DrawLoad.Length > 0) l.Add("draw load: " + DrawLoad);
        l.AddRange(Warnings.Select(w => "warning: " + w));
        return l;
    }
}

/// <summary>An imported level: the scene (bound to the target) and its report.</summary>
public sealed record YearOfTheDragonImportResult(SceneDocument Scene, YearOfTheDragonImportReport Report, IReadOnlyList<AuthoredMaterial> Materials)
{
    /// <summary>The imported terrain, name, start, sky colour and distant meshes on a Start Blank scene (keeping its
    /// identity, save rows, objects, borrowed retail sky if any, and music).</summary>
    public SceneDocument ApplyTo(SceneDocument blank) =>
        blank with { Name = Scene.Name, Spawn = Scene.Spawn, Heading = Scene.Heading, Surfaces = Scene.Surfaces, FarMeshes = Scene.FarMeshes, SkyColor = Scene.SkyColor, GemSpots = Scene.GemSpots };
}

/// <summary>Why an import was refused (a limit the level cannot be brought under without losing parts of it).</summary>
public sealed class YearOfTheDragonImportException(string message) : Exception(message);

/// <summary>Rebuilds the main area of a Spyro: Year of the Dragon level as a Spyro the Dragon scene (the pipeline first
/// used by hand for Lost Fleet):
/// <list type="bullet">
/// <item>every near face becomes a face of the scene (editor units = world units / 16), with its near and far corner
/// colours, its sector (<see cref="AuthoredSurface.Sector"/>), two-sided (flag 0x08) and reversed front (flag 0x04); a
/// triangle drops its repeated first corner, a quad with two equal corners becomes the triangle of its other three, a
/// face with fewer than three different corners is left out (it draws nothing in its own game either);</item>
/// <item>each texture becomes a 64x64 picture of its four close-up quarters, carried by a Spyro the Dragon terrain
/// texture slot that shares no pixels or palettes and is not animated (smallest texture-memory footprint first); when
/// there are more textures than carriers, or their textures do not fit the built level's free texture memory,
/// the texture whose faces would change least is merged into the most similar kept one (8x8 colour blocks), repeatedly;
/// textures of liquid or other special ground are never merged;</item>
/// <item>ground kinds come from the level's collision: each collision triangle's kind and water bit are matched to the
/// face sharing its three corners (sink -> water/lava/ooze as Spyro the Dragon stores the same record; hot ground ->
/// lava, or ooze when the face is drawn green; water surfaces -> water; supercharge -> supercharge; others stay solid and are
/// reported);</item>
/// <item>sectors are grouped (nearest first) until the game's 127 sector slots hold them; each group draws its sectors'
/// own distant meshes (<see cref="SceneDocument.FarMeshes"/>);</item>
/// <item>the start is the flat, open floor that a doorway arrival needs nearest the level's start object (class 1002,
/// one per level where Spyro plays, beside the level's beginning), else nearest the level's centre;</item>
/// <item>the level keeps its own heights: Create BIN lowers the replaced retail level's kill height under its lowest floor
/// (<see cref="NativeKillHeights"/>; Ice Cavern's is 1,000 units), but never under 64 units, so a level with floors lower
/// than that is lifted (<see cref="NativeKillHeights.Minimum"/>);</item>
/// <item>the result is compiled as Create BIN compiles it; a level over the game's face, sector, collision or terrain-data
/// limits is refused with the numbers.</item>
/// </list></summary>
public static class YearOfTheDragonLevelImport
{
    /// <summary>Year of the Dragon's gem class (its loose gems; 200 and 201 are the vases and baskets that hold gems): where
    /// they lie becomes <see cref="SceneDocument.GemSpots"/>.</summary>
    public const int GemClass = 1;

    /// <summary>A Spyro the Dragon texture slot that can carry a picture.</summary>
    public sealed record Carrier(int Wad, int Id, int Bytes);

    /// <summary>Imports <paramref name="level"/> from the Year of the Dragon disc for the blank level <paramref name="binding"/>
    /// (pictures into <see cref="YearOfTheDragonImportOptions.PictureFolder"/>; carriers and texture memory from the
    /// original Spyro the Dragon disc <paramref name="spyroDisc"/>).</summary>
    public static async Task<YearOfTheDragonImportResult> ImportAsync(string yotdDisc, YearOfTheDragonLevel level, string spyroDisc, LevelCatalog catalog,
        SceneTargetBinding? binding, YearOfTheDragonImportOptions options, IProgress<string>? progress = null, CancellationToken token = default)
    {
        var disc = new YearOfTheDragonDisc(yotdDisc);
        YearOfTheDragonLevels.Read(disc);
        progress?.Report($"Reading {level.Name}…");
        var data = YearOfTheDragonLevelData.Read(disc, level);
        token.ThrowIfCancellationRequested();
        progress?.Report("Finding Spyro the Dragon textures that can carry pictures…");
        var lib = new UniversalSourceLibrary(spyroDisc, catalog);
        var carriers = Carriers(lib, catalog, options.CarrierLevels ?? [.. catalog.Levels.Select(l => l.Key)], spyroDisc, token);
        NativeAuthoredPictureVram? vram = null;
        if (options.CheckTextureMemory && carriers.Count > 0)
        {
            progress?.Report("Measuring the built level's free texture memory…");
            vram = await NativeAuthoredPictureVram.CaptureAsync(spyroDisc, new(carriers[0].Wad, carriers[0].Id),
                binding == null ? null : lib.Get(binding.LevelKey).Data.Vram().ToArray(), token);
        }
        return await BuildAsync(data, binding, carriers, vram == null ? null : vram.Test, options, progress, token,
            NativeKillHeights.Minimum(lib.Disc.Exe, binding?.LevelId ?? 65));
    }

    /// <summary>The height (editor units) below which Spyro dies in the retail level <paramref name="levelId"/> (10-64) of the
    /// original executable (<see cref="NativeKillHeights"/>; Ice Cavern 1,000, Cliff Town 781, Dry Canyon 687, most others 64
    /// or less). Create BIN lowers it for a rebuilt level down to <see cref="NativeKillHeights.Minimum"/>, which is what
    /// imports pass as <c>killHeight</c>.</summary>
    public static float KillHeight(byte[] spyroExe, int levelId) => NativeKillHeights.ReadOriginal(spyroExe, levelId);

    /// <summary>Lowest faces are kept this far above the kill height (Spyro stands about 22 units above the floor).</summary>
    public const float KillHeightMargin = 64;

    /// <summary>Texture slots that can carry a picture: canonical layout (<see cref="NativeTerrainTextureImageWriter.Records"/>),
    /// no mirrored tiles, sharing no pixels or palettes with another texture, not animated or scrolled and not an
    /// animation's source; smallest texture-memory footprint first (the free memory holds more of them).</summary>
    public static List<Carrier> Carriers(UniversalSourceLibrary lib, LevelCatalog catalog, IEnumerable<string> keys, string spyroDisc, CancellationToken token = default)
    {
        List<Carrier> sized = [];
        foreach (string key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            var level = catalog.FindByKey(key);
            if (level == null || level.SourceWadEntry is < 10 or > 78) continue;
            var data = lib.Get(key).Data;
            var (mo, ml) = data.Subfiles()[1];
            var textureList = data.Bytes.AsSpan(mo, ml);
            var records = NativeTerrainTextureImageWriter.Records(textureList);
            var footprint = NativeTerrainTextureImageWriter.Footprint(textureList);
            var groups = NativeTerrainTextureImageWriter.SharingGroups(footprint);
            var audit = NativeTerrainTextureRuntimeControlScanner.Inspect(spyroDisc, level.SourceWadEntry);
            if (!audit.Complete) continue;
            foreach (var g in groups.Where(g => g.Count == 1))
            {
                int id = g[0];
                if (records[id] == null || records[id]!.Descriptors.Any(d => d.Mirrored)) continue;
                if (audit.IsRuntimePersistentTarget(id) && !audit.AnimationSourceTextureIds.Contains(id))
                    sized.Add(new(level.SourceWadEntry, id, footprint[id].Sum(r => r.Width * r.Rows)));
            }
        }
        return [.. sized.OrderBy(c => c.Bytes).ThenBy(c => c.Wad).ThenBy(c => c.Id)];
    }

    /// <summary>One face of the scene before materials and groups are chosen.</summary>
    private sealed record Piece(int Sector, int Index, Vector3f[] Points, ColorRgba[] Near, ColorRgba[] Far, int Texture, bool TwoSided, bool FrontReversed)
    {
        public AuthoredSurfaceKind? Kind { get; set; }
        public string Key => string.Join(";", Points.Select(p => $"{p.X},{p.Y},{p.Z}").Order(StringComparer.Ordinal));
    }

    /// <summary>The import without the discs: <paramref name="vram"/> tests a material list against the built level's
    /// free texture memory (null: not checked); <paramref name="killHeight"/> is the lowest kill height the replaced level
    /// can have (<see cref="NativeKillHeights.Minimum"/>): the level is lifted until its lowest face is
    /// <see cref="KillHeightMargin"/> above it.</summary>
    public static async Task<YearOfTheDragonImportResult> BuildAsync(YearOfTheDragonLevelData data, SceneTargetBinding? binding, IReadOnlyList<Carrier> carriers,
        Func<IReadOnlyList<AuthoredMaterial>, (bool Fits, int FreeBytes, string? Reason)>? vram, YearOfTheDragonImportOptions options,
        IProgress<string>? progress = null, CancellationToken token = default, float killHeight = 0)
    {
        var level = data.Level;
        List<string> warnings = [];

        // 1. Faces.
        var (pieces, empty, repaired, duplicate, collinear) = Pieces(data);
        int yotdFaces = data.Sectors.Sum(s => s.NearFaces.Count);
        if (pieces.Count == 0) throw new YearOfTheDragonImportException($"{level.Name} has no faces this importer can read.");
        if (pieces.Count > SceneDocument.MaxSurfaces)
            throw new YearOfTheDragonImportException($"{level.Name} has {pieces.Count:N0} faces; a Spyro the Dragon level holds at most {SceneDocument.MaxSurfaces:N0} (the game's per-face table). It cannot be imported whole.");
        int farOnly = data.Sectors.Count(s => s.FarOnly && s.Far != null);
        if (farOnly > 0) warnings.Add($"{farOnly} distant-only sectors (scenery drawn only far away, {data.Sectors.Where(s => s.FarOnly && s.Far != null).Sum(s => s.Far!.Faces.Count)} faces) are not imported: a Spyro the Dragon sector needs near faces.");
        token.ThrowIfCancellationRequested();

        // 2. Pictures (64x64) of the textures in use, and their average colours.
        var use = pieces.GroupBy(p => p.Texture).ToDictionary(g => g.Key, g => g.Count());
        var pictures = use.Keys.Order().ToDictionary(t => t, data.Picture);
        var average = pictures.ToDictionary(p => p.Key, p => (R: p.Value.Average(c => c.R), G: p.Value.Average(c => c.G), B: p.Value.Average(c => c.B)));

        // 3. Ground kinds from the collision.
        var (kindCounts, unsupported, unmatched) = AssignKinds(data, pieces, average, options);
        foreach (var (label, count) in unsupported.OrderByDescending(u => u.Value))
            warnings.Add($"{count} collision triangles of {label} ground have no Spyro the Dragon equivalent and are solid ground.");
        if (unmatched > 0) warnings.Add($"{unmatched} collision triangles with a special ground kind match no drawn face (invisible ground; left out).");
        int seeThrough = pieces.Where(p => data.Textures[p.Texture].SeeThrough).Select(p => p.Texture).Distinct().Count();
        if (seeThrough > 0) warnings.Add($"{seeThrough} see-through textures (water, glass, acid) are drawn opaque.");
        int turned = data.Sectors.SelectMany(s => s.NearFaces).Count(f => f.Triangle && f.Rotation != 0 && f.DrawnCorners().Length == 3);
        if (turned > 0) warnings.Add($"{turned} triangles use a turned texture corner in Year of the Dragon; they show their picture's usual three corners.");

        // 4. Textures: merge until they fit the carriers and the texture memory.
        var hazard = pieces.Where(p => p.Kind != null).Select(p => p.Texture).ToHashSet();
        int limit = Math.Min(Math.Min(options.MaxTextures, 127), carriers.Count);
        if (hazard.Count > limit)
            throw new YearOfTheDragonImportException($"{level.Name} needs {hazard.Count} textures for its special ground alone; only {limit} carriers are available.");
        var merger = new TextureMerger(pictures, use, hazard);
        merger.MergeTo(limit);
        int free = -1;
        List<AuthoredMaterial> materials;
        while (true)
        {
            materials = [.. merger.Kept.Select((t, i) => new AuthoredMaterial(carriers[i].Wad, carriers[i].Id) { Image = PicturePath(options.PictureFolder, level, t) })];
            if (vram == null) break;
            progress?.Report($"Fitting {materials.Count} textures into texture memory…");
            var (fits, bytes, reason) = vram(materials);
            if (fits && bytes >= options.TextureMemoryReserve) { free = bytes; break; }
            reason ??= $"{bytes:N0} bytes would be left for objects; {options.TextureMemoryReserve:N0} are kept";
            if (!merger.CanMerge)
                throw new YearOfTheDragonImportException($"{level.Name}'s textures do not fit the built level's texture memory even merged to {merger.Kept.Count} " +
                    $"({hazard.Count} of them special ground, which is never merged): {reason}");
            merger.MergeTo(merger.Kept.Count - 1);
            token.ThrowIfCancellationRequested();
        }
        var materialOf = merger.Kept.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => materials[x.i]);

        // 5. Sector groups within the game's 127 sectors.
        var surfaces = pieces.Select(p => Surface(level, p, materialOf[merger.Target(p.Texture)], p.Sector)).ToList();
        var (groupOf, groups, nativeSectors) = GroupSectors(level, pieces, surfaces);
        surfaces = [.. surfaces.Select((s, i) => s with { Sector = groupOf[pieces[i].Sector] })];

        // 6. Distant meshes per group.
        List<AuthoredFarMesh> farMeshes = [];
        foreach (var g in groups.OrderBy(g => g.Key))
        {
            var meshes = g.Value.Select(s => data.Sectors[s].Far).OfType<AuthoredFarMesh>().ToList();
            if (meshes.Count > 0) farMeshes.Add(MergeFar(g.Key, meshes));
        }

        // 7. Above the replaced level's kill height (Spyro dies below it wherever he stands).
        float lowest = surfaces.Min(s => s.Vertices.Min(v => v.Z));
        float lift = killHeight > 0 ? Math.Max(0, MathF.Ceiling(killHeight + KillHeightMargin - lowest)) : 0;
        if (lift > 0)
        {
            Vector3f Up(Vector3f v) => v with { Z = v.Z + lift };
            if (surfaces.Max(s => s.Vertices.Max(v => v.Z)) + lift > 16383 || farMeshes.Any(m => m.Vertices.Any(v => v.Z + lift > 16383)))
                throw new YearOfTheDragonImportException($"{level.Name} is too tall to lift above the replaced level's kill height ({killHeight:0}).");
            surfaces = [.. surfaces.Select(s => s with { Vertices = [.. s.Vertices.Select(Up)] })];
            farMeshes = [.. farMeshes.Select(m => m with { Vertices = [.. m.Vertices.Select(Up)] })];
            warnings.Add($"Raised {lift:0} units: the replaced level kills Spyro below height {killHeight:0}, and this level's lowest face was at {lowest:0}.");
        }

        // 8. The start.
        var doc = SceneDocument.New(level.SpyroName) with { TargetBinding = binding, Surfaces = surfaces, FarMeshes = farMeshes,
            GemSpots = [.. data.Objects.Where(o => o.Class is GemClass or 200 or 201).Select(o => o.Position)] };
        // The sky's own parts are not converted; the screen clears to Year of the Dragon's sky background colour.
        if (data.SkyBackground is { } sky) doc = doc with { SkyColor = sky };
        else warnings.Add("The level file has no sky block; the sky keeps the scene's plain colour.");
        string spawnSource;
        if (options.Spawn is { } chosen) { doc = doc with { Spawn = chosen, Heading = options.Heading ?? 0 }; spawnSource = "chosen by the caller"; }
        else
        {
            // Year of the Dragon's own start is not stored as a position this reads; every level where Spyro plays has
            // one object of class 1002 (1003 in a few) hovering 32 units above the floor where the level begins (in Lost
            // Fleet's demo the player's recorded start lies 72 units from it). The start is the certified floor nearest
            // to it, else nearest the level's centre.
            var anchor = data.Objects.Where(o => o.Class is 1002 or 1003).OrderBy(o => o.Class).FirstOrDefault();
            var (spawn, heading, how) = FindSpawn(doc, anchor is { } a ? a.Position with { Z = a.Position.Z + lift } : null);
            doc = doc with { Spawn = spawn, Heading = options.Heading ?? heading };
            spawnSource = how;
        }
        doc.Validate();

        // 9. Compile as Create BIN does.
        byte[] bundle;
        NativeAuthoredMeshRenderer.DrawPlan? drawPlan;
        try { bundle = NativeAuthoredMesh.Compile(doc, materials); drawPlan = NativeAuthoredMeshRenderer.LastDrawPlan; }
        catch (InvalidDataException e) { throw new YearOfTheDragonImportException($"{level.Name} cannot be built as a Spyro the Dragon level: {e.Message}"); }
        var (compiledSectors, _, _) = NativeAuthoredMeshRenderer.LastReport;
        if (drawPlan?.Note() is { } drawNote) warnings.Add(drawNote);
        try { NativeAuthoredSpawn.RequirePortalArrivalSupported(doc); }
        catch (InvalidDataException e) { warnings.Add("The start needs attention before Create BIN: " + e.Message); }

        // 10. Pictures.
        Directory.CreateDirectory(options.PictureFolder);
        foreach (int t in merger.Kept)
            await TerrainTexturePngWriter.WriteRgbaAsync(PicturePath(options.PictureFolder, level, t), 64, 64, pictures[t], token);

        var report = new YearOfTheDragonImportReport
        {
            Level = level.Name, YotdSectors = data.Sectors.Count, FarOnlySectors = farOnly, SectorGroups = groups.Count, NativeSectors = compiledSectors,
            YotdFaces = yotdFaces, Faces = surfaces.Count, RepairedFaces = repaired, EmptyFaces = empty, DuplicateFaces = duplicate, CollinearFaces = collinear,
            TwoSidedFaces = surfaces.Count(s => s.TwoSided), ReversedFaces = surfaces.Count(s => s.FrontReversed),
            Textures = use.Count, KeptTextures = merger.Kept.Count, Merges = merger.Merges(), Carriers = carriers.Count, FreeTextureBytes = free,
            GroundKinds = kindCounts, CollisionTriangles = data.Collision.Count, Spawn = doc.Spawn, Heading = doc.Heading, SpawnSource = spawnSource,
            TerrainBytes = bundle.Length, DrawLoad = drawPlan?.Describe() ?? "", FarMeshes = farMeshes.Count, SkyBackground = data.SkyBackground, Lift = lift, Warnings = warnings,
        };
        return new(doc, report, materials);
    }

    /// <summary>Where a texture's picture is written: &lt;folder&gt;/&lt;level&gt;-NN.png.</summary>
    public static string PicturePath(string folder, YearOfTheDragonLevel level, int texture) =>
        Path.GetFullPath(Path.Combine(folder, $"{Slug(level.Name)}-{texture:00}.png"));
    public static string Slug(string name) => string.Join('-', new string([.. name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : ' ')])
        .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static AuthoredSurface Surface(YearOfTheDragonLevel level, Piece p, AuthoredMaterial material, int sector) =>
        new(StableId($"yotd:{level.Index}:{p.Sector}:{p.Index}"), p.Points, p.Near[0], material, p.Near, p.Far)
        { Kind = p.Kind, Sector = sector, TwoSided = p.TwoSided, FrontReversed = p.FrontReversed };

    private static string StableId(string key) => new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16)).ToString("N");

    /// <summary>The faces: each near face's drawn corners (<see cref="YotdNearFace.DrawnCorners"/>). A face that needed
    /// repair is left out when a face with the same corners exists; a face whose corners lie on one line is left out.</summary>
    private static (List<Piece> Pieces, int Empty, int Repaired, int Duplicate, int Collinear) Pieces(YearOfTheDragonLevelData data)
    {
        List<Piece> pieces = [];
        List<(Piece Piece, YotdNearFace Face)> repairs = [];
        int empty = 0, collinear = 0, duplicate = 0;
        foreach (var sector in data.Sectors)
            for (int i = 0; i < sector.NearFaces.Count; i++)
            {
                var f = sector.NearFaces[i];
                int[] corners = f.DrawnCorners();
                if (corners.Length == 0) { empty++; continue; }
                var points = corners.Select(k => f.Corners[k]).ToArray();
                var a = points[0]; var b = points[1]; var c = points[2];
                float nx = (b.Y - a.Y) * (c.Z - a.Z) - (b.Z - a.Z) * (c.Y - a.Y), ny = (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z), nz = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                if (nx == 0 && ny == 0 && nz == 0) { collinear++; continue; }
                var piece = new Piece(sector.Number, i, points, [.. corners.Select(k => f.Near[k])], [.. corners.Select(k => f.Far[k])], f.Texture, f.TwoSided, f.FrontReversed);
                bool whole = corners.Length == (f.Triangle ? 3 : 4);
                if (whole) pieces.Add(piece); else repairs.Add((piece, f));
            }
        var present = pieces.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        int repaired = 0;
        foreach (var (piece, _) in repairs)
        {
            if (!present.Add(piece.Key)) { duplicate++; continue; }
            pieces.Add(piece); repaired++;
        }
        return (pieces, empty, repaired, duplicate, collinear);
    }

    /// <summary>Ground kinds: every collision triangle with a kind (or the water bit) votes for the face that has its
    /// three corners; a face takes its most voted kind.</summary>
    private static (Dictionary<string, int> Counts, Dictionary<string, int> Unsupported, int Unmatched) AssignKinds(YearOfTheDragonLevelData data, List<Piece> pieces,
        Dictionary<int, (double R, double G, double B)> average, YearOfTheDragonImportOptions options)
    {
        Dictionary<(int, int, int), List<int>> byCorner = [];
        for (int i = 0; i < pieces.Count; i++)
            foreach (var p in pieces[i].Points)
            {
                var key = ((int)p.X, (int)p.Y, (int)p.Z);
                if (!byCorner.TryGetValue(key, out var list)) byCorner[key] = list = [];
                if (list.Count == 0 || list[^1] != i) list.Add(i);
            }
        bool Has(Piece p, Vector3f v) => p.Points.Any(q => q.X == v.X && q.Y == v.Y && Math.Abs(q.Z - v.Z) <= 1);
        var votes = new Dictionary<int, Dictionary<AuthoredSurfaceKind, int>>();
        Dictionary<string, int> unsupported = [];
        int unmatched = 0;
        foreach (var t in data.Collision)
        {
            AuthoredSurfaceKind? kind = null;
            bool special = false;
            if (t.Kind != 0x3F && t.Kind < data.GroundKinds.Count)
            {
                var k = data.GroundKinds[t.Kind];
                kind = Map(k, null);
                special = true;
                if (kind == null && k.Type is not (6)) { string label = k.Label; unsupported[label] = unsupported.GetValueOrDefault(label) + 1; }
            }
            if (kind == null && t.Water && options.WaterCostsALife) { kind = AuthoredSurfaceKind.Water; special = true; }
            if (kind == null) continue;
            var candidates = new List<int>();
            foreach (int dz in new[] { 0, -1, 1 })
                if (byCorner.TryGetValue(((int)t.A.X, (int)t.A.Y, (int)t.A.Z + dz), out var list)) candidates.AddRange(list);
            var hits = candidates.Distinct().Where(i => Has(pieces[i], t.B) && Has(pieces[i], t.C)).ToList();
            if (hits.Count == 0) { if (special) unmatched++; continue; }
            foreach (int i in hits)
            {
                var resolved = kind;
                // Hot ground: lava, or ooze where the face is drawn green (Lost Fleet's acid: a grey texture tinted by
                // its corner colours; the game multiplies texel by colour / 128).
                if (t.Kind != 0x3F && t.Kind < data.GroundKinds.Count && data.GroundKinds[t.Kind].Type == 1)
                {
                    var (r, g, b) = average[pieces[i].Texture];
                    var near = pieces[i].Near;
                    resolved = Map(data.GroundKinds[t.Kind], (r * near.Average(c => c.R) / 128, g * near.Average(c => c.G) / 128, b * near.Average(c => c.B) / 128), options.HotGroundIsDeadly);
                }
                if (resolved == null) continue;
                if (!votes.TryGetValue(i, out var v)) votes[i] = v = [];
                v[resolved] = v.GetValueOrDefault(resolved) + 1;
            }
        }
        Dictionary<string, int> counts = [];
        foreach (var (i, v) in votes)
        {
            var best = v.OrderByDescending(x => x.Value).ThenBy(x => x.Key.Type).ThenBy(x => x.Key.Param1).ThenBy(x => x.Key.Param2).First().Key;
            pieces[i].Kind = best;
            string name = Name(best);
            counts[name] = counts.GetValueOrDefault(name) + 1;
        }
        return (counts, unsupported, unmatched);
    }

    /// <summary>A Year of the Dragon ground kind as Spyro the Dragon's (null: none). Sink (type 0) is the hazard floor
    /// both games store as type 0 (first parameter: 1 kills; second: 0 water, 1 lava, 2 ooze); hot ground (type 1)
    /// becomes lava, or ooze when the face is drawn mostly green (<paramref name="texture"/>: its texture's average colour
/// tinted by its corner colours); supercharge (type 2) is Spyro the Dragon's type 4.</summary>
    public static AuthoredSurfaceKind? Map(YotdGroundKind kind, (double R, double G, double B)? texture, bool hotIsDeadly = true)
    {
        switch (kind.Type)
        {
            case 0:
            {
                int deadly = kind.Parameters.Count > 0 && kind.Parameters[0] != 0 ? 1 : 0, liquid = kind.Parameters.Count > 1 ? kind.Parameters[1] : 0;
                var k = new AuthoredSurfaceKind(0, deadly, liquid);
                return k.IsSupported ? k : new AuthoredSurfaceKind(0, deadly, 0);
            }
            case 1:
            {
                bool green = texture is { } c && c.G > c.R && c.G > c.B;
                return green ? (hotIsDeadly ? AuthoredSurfaceKind.DeadlyOoze : AuthoredSurfaceKind.Ooze) : (hotIsDeadly ? AuthoredSurfaceKind.DeadlyLava : AuthoredSurfaceKind.Lava);
            }
            case 2: return AuthoredSurfaceKind.Supercharge;
            default: return null;
        }
    }

    private static string Name(AuthoredSurfaceKind k) =>
        k == AuthoredSurfaceKind.Water ? "water" : k == AuthoredSurfaceKind.DeadlyWater ? "deadly water" : k == AuthoredSurfaceKind.Lava ? "lava" :
        k == AuthoredSurfaceKind.DeadlyLava ? "deadly lava" : k == AuthoredSurfaceKind.Ooze ? "ooze" : k == AuthoredSurfaceKind.DeadlyOoze ? "deadly ooze" :
        k == AuthoredSurfaceKind.Supercharge ? "supercharge" : k == AuthoredSurfaceKind.InvisibleWall ? "invisible wall" : $"kind {k.Type}";

    /// <summary>Greedy texture merge (texture_merge.py): the texture whose merge costs least (its faces x the mean
    /// squared difference of 8x8 colour blocks to the nearest other kept texture) folds into that texture. Special
    /// ground textures neither fold nor take others.</summary>
    private sealed class TextureMerger
    {
        private readonly Dictionary<int, double[]> _blocks = [];
        private readonly Dictionary<int, int> _weight, _target = [];
        private readonly HashSet<int> _fixed;
        private readonly SortedSet<int> _keep;
        public TextureMerger(Dictionary<int, Rgba32[]> pictures, Dictionary<int, int> use, HashSet<int> fixedTextures)
        {
            _weight = new(use); _fixed = fixedTextures; _keep = [.. use.Keys];
            foreach (var (t, p) in pictures)
            {
                var b = new double[64 * 3];
                for (int by = 0; by < 8; by++)
                    for (int bx = 0; bx < 8; bx++)
                    {
                        double r = 0, g = 0, bl = 0;
                        for (int y = by * 8; y < by * 8 + 8; y++)
                            for (int x = bx * 8; x < bx * 8 + 8; x++) { var c = p[y * 64 + x]; r += c.R; g += c.G; bl += c.B; }
                        int k = (by * 8 + bx) * 3; b[k] = r / 64; b[k + 1] = g / 64; b[k + 2] = bl / 64;
                    }
                _blocks[t] = b;
            }
            foreach (int t in use.Keys) _target[t] = t;
        }
        public IReadOnlyList<int> Kept => [.. _keep];
        public bool CanMerge => _keep.Count(t => !_fixed.Contains(t)) >= 2;
        public int Target(int texture) => _target[texture];
        public Dictionary<int, int> Merges() => _target.Where(x => x.Key != x.Value).ToDictionary(x => x.Key, x => x.Value);
        private double Distance(int a, int b)
        {
            double s = 0; var x = _blocks[a]; var y = _blocks[b];
            for (int i = 0; i < x.Length; i += 3) s += (x[i] - y[i]) * (x[i] - y[i]) + (x[i + 1] - y[i + 1]) * (x[i + 1] - y[i + 1]) + (x[i + 2] - y[i + 2]) * (x[i + 2] - y[i + 2]);
            return s / 64;
        }
        public void MergeTo(int count)
        {
            while (_keep.Count > count)
            {
                (double Cost, int T, int U)? best = null;
                foreach (int t in _keep.Where(t => !_fixed.Contains(t)))
                {
                    int u = -1; double d = double.MaxValue;
                    foreach (int k in _keep) if (k != t && !_fixed.Contains(k)) { double e = Distance(t, k); if (e < d) { d = e; u = k; } }
                    if (u < 0) continue;
                    double cost = _weight[t] * d;
                    if (best == null || cost < best.Value.Cost) best = (cost, t, u);
                }
                if (best is not { } b) throw new YearOfTheDragonImportException("No texture is left that can be merged.");
                _keep.Remove(b.T); _weight[b.U] += _weight[b.T];
                foreach (var k in _target.Keys.ToList()) if (_target[k] == b.T) _target[k] = b.U;
            }
        }
    }

    /// <summary>Groups the sectors (each starts alone) until they need at most 127 native sectors: repeatedly the two
    /// groups whose merged box is smallest among those that then need fewer native sectors (counted exactly as the
    /// compiler splits them). Group numbers are the smallest sector number in the group.</summary>
    private static (Dictionary<int, int> GroupOf, Dictionary<int, List<int>> Groups, int NativeSectors) GroupSectors(YearOfTheDragonLevel level, List<Piece> pieces, List<AuthoredSurface> surfaces)
    {
        var bySector = Enumerable.Range(0, pieces.Count).GroupBy(i => pieces[i].Sector).ToDictionary(g => g.Key, g => g.ToList());
        var groups = bySector.Keys.ToDictionary(s => s, s => new List<int> { s });
        var members = bySector.ToDictionary(g => g.Key, g => g.Value.ToList());
        int Count(List<int> faces) => NativeAuthoredMeshRenderer.ImportedSectorCount(faces.Select(i => surfaces[i]));
        var count = members.ToDictionary(m => m.Key, m => Count(m.Value));
        (Vector3f Min, Vector3f Max) Box(List<int> faces)
        {
            var pts = faces.SelectMany(i => pieces[i].Points).ToArray();
            return (new(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Min(p => p.Z)), new(pts.Max(p => p.X), pts.Max(p => p.Y), pts.Max(p => p.Z)));
        }
        var box = members.ToDictionary(m => m.Key, m => Box(m.Value));
        Dictionary<(int, int), int> merged = [];
        int total = count.Values.Sum();
        while (total > NativeAuthoredMeshRenderer.MaxSectors)
        {
            (double Score, int A, int B, int Count)? best = null;
            var keys = groups.Keys.ToList();
            foreach (int a in keys)
            {
                var ca = box[a];
                var near = keys.Where(b => b != a).OrderBy(b => Gap(ca, box[b])).Take(6);
                foreach (int b in near)
                {
                    var (lo, hi) = (Min(ca.Min, box[b].Min), Max(ca.Max, box[b].Max));
                    if (hi.X - lo.X > 2047 || hi.Y - lo.Y > 2047 || hi.Z - lo.Z > 1023) continue;
                    var pair = (Math.Min(a, b), Math.Max(a, b));
                    if (!merged.TryGetValue(pair, out int both)) merged[pair] = both = Count([.. members[a], .. members[b]]);
                    int gain = count[a] + count[b] - both;
                    if (gain <= 0) continue;
                    double score = ((hi.X - lo.X) + (hi.Y - lo.Y) + (hi.Z - lo.Z)) / gain;
                    if (best == null || score < best.Value.Score) best = (score, pair.Item1, pair.Item2, both);
                }
            }
            if (best is not { } m)
                throw new YearOfTheDragonImportException($"{level.Name} needs {total} terrain sectors and no two neighbouring sectors can share one; the game holds {NativeAuthoredMeshRenderer.MaxSectors}.");
            groups[m.A].AddRange(groups[m.B]); members[m.A].AddRange(members[m.B]);
            groups.Remove(m.B); members.Remove(m.B); count.Remove(m.B); box.Remove(m.B);
            count[m.A] = m.Count; box[m.A] = Box(members[m.A]);
            foreach (var k in merged.Keys.Where(k => k.Item1 == m.A || k.Item2 == m.A || k.Item1 == m.B || k.Item2 == m.B).ToList()) merged.Remove(k);
            total = count.Values.Sum();
        }
        var groupOf = new Dictionary<int, int>();
        foreach (var (g, sectors) in groups) foreach (int s in sectors) groupOf[s] = g;
        return (groupOf, groups, total);
    }
    private static Vector3f Min(Vector3f a, Vector3f b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
    private static Vector3f Max(Vector3f a, Vector3f b) => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
    private static double Gap((Vector3f Min, Vector3f Max) a, (Vector3f Min, Vector3f Max) b)
    {
        double dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X)), dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y)), dz = Math.Max(0, Math.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Several sectors' distant meshes as one (tables concatenated; the compiler fits them to its 64 entries).</summary>
    private static AuthoredFarMesh MergeFar(int group, List<AuthoredFarMesh> meshes)
    {
        if (meshes.Count == 1) return meshes[0] with { Sector = group };
        List<Vector3f> v = []; List<ColorRgba> c = []; List<AuthoredFarFace> f = [];
        foreach (var m in meshes)
        {
            int vo = v.Count, co = c.Count;
            v.AddRange(m.Vertices); c.AddRange(m.Colors);
            f.AddRange(m.Faces.Select(x => new AuthoredFarFace([.. x.Corners.Select(k => k + vo)], [.. x.Colors.Select(k => k + co)])));
        }
        return new(group, [.. v], [.. c], f);
    }

    /// <summary>The start: the floor point nearest <paramref name="anchor"/> (within 1,024 units, on a floor within 160 of
    /// the anchor's) else nearest the level's centre (its faces' box) where a doorway arrival is certified (<see cref="NativeAuthoredSpawn.RequirePortalArrivalSupported"/>: solid floor across 384 units within
    /// 128 below the start) on solid, level ground with room above, facing the longest open run of floor.</summary>
    internal static (Vector3f Spawn, int Heading, string How) FindSpawn(SceneDocument doc, Vector3f? anchor = null)
    {
        var tris = new List<(Vector3f A, Vector3f B, Vector3f C, bool Hazard, bool Floor)>();
        foreach (var s in doc.Surfaces)
        {
            var v = s.Vertices;
            foreach (var (a, b, c) in v.Length == 4 ? new[] { (v[0], v[1], v[2]), (v[0], v[2], v[3]) } : [(v[0], v[1], v[2])])
            {
                double nx = (b.Y - a.Y) * (c.Z - a.Z) - (b.Z - a.Z) * (c.Y - a.Y), ny = (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z), nz = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len == 0) continue;
                tris.Add((a, b, c, s.Kind != null, Math.Abs(nz) / len >= 0.9));
            }
        }
        const int Cell = 64;
        Dictionary<(int, int), List<int>> grid = [];
        for (int i = 0; i < tris.Count; i++)
        {
            var t = tris[i];
            int x0 = (int)Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)) / Cell, x1 = (int)Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)) / Cell;
            int y0 = (int)Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) / Cell, y1 = (int)Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)) / Cell;
            for (int gx = x0; gx <= x1; gx++) for (int gy = y0; gy <= y1; gy++) { if (!grid.TryGetValue((gx, gy), out var l)) grid[(gx, gy)] = l = []; l.Add(i); }
        }
        IEnumerable<(double Z, int Index)> Heights(double x, double y)
        {
            if (!grid.TryGetValue(((int)x / Cell, (int)y / Cell), out var list)) yield break;
            foreach (int i in list)
            {
                var (a, b, c, _, _) = tris[i];
                double det = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                if (Math.Abs(det) < 1e-9) continue;
                double l1 = ((b.X - x) * (c.Y - y) - (b.Y - y) * (c.X - x)) / det, l2 = ((c.X - x) * (a.Y - y) - (c.Y - y) * (a.X - x)) / det, l3 = 1 - l1 - l2;
                if (l1 < -1e-6 || l2 < -1e-6 || l3 < -1e-6) continue;
                yield return (l1 * a.Z + l2 * b.Z + l3 * c.Z, i);
            }
        }
        // The top floor at a point (null: none, a hazard, a steep face, or something just above it).
        double? Floor(double x, double y, double? near = null)
        {
            var hs = Heights(x, y).Where(h => near == null || (h.Z <= near + 24 && h.Z >= near - 64)).ToList();
            if (hs.Count == 0) return null;
            var top = hs.MaxBy(h => h.Z);
            return tris[top.Index].Hazard || !tris[top.Index].Floor ? null : top.Z;
        }
        bool Open(double x, double y, double z) => !Heights(x, y).Any(h => h.Z > z + 2 && h.Z < z + 220);
        float minX = doc.Surfaces.Min(s => s.Vertices.Min(v => v.X)), maxX = doc.Surfaces.Max(s => s.Vertices.Max(v => v.X));
        float minY = doc.Surfaces.Min(s => s.Vertices.Min(v => v.Y)), maxY = doc.Surfaces.Max(s => s.Vertices.Max(v => v.Y));
        double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        // Near the anchor (within 1,024 units, its floor first), else near the centre.
        if (anchor is { } a0 && a0.X >= minX && a0.X <= maxX && a0.Y >= minY && a0.Y <= maxY)
        {
            var near = Spot(a0.X, a0.Y, 1024, a0.Z);
            if (near != null) return (near.Value.Spawn, near.Value.Heading, $"the open, level floor nearest Year of the Dragon's start object (at {a0.X:0},{a0.Y:0},{a0.Z:0})");
        }
        var central = Spot(cx, cy, double.MaxValue, null);
        if (central != null) return (central.Value.Spawn, central.Value.Heading, $"the open, level floor nearest the level's centre ({cx:0},{cy:0}) with room for a doorway arrival");
        throw new YearOfTheDragonImportException("No floor in this level is level and open across the 384 units a doorway arrival needs; choose the start yourself (Spawn option).");

        (Vector3f Spawn, int Heading)? Spot(double ox, double oy, double reach, float? height)
        {
            var candidates = new List<(double D, double X, double Y)>();
            for (double x = Math.Ceiling(minX / 32) * 32; x <= maxX; x += 32)
                for (double y = Math.Ceiling(minY / 32) * 32; y <= maxY; y += 32)
                {
                    double d = (x - ox) * (x - ox) + (y - oy) * (y - oy);
                    if (d <= reach * reach) candidates.Add((d, x, y));
                }
            int tried = 0;
            foreach (var (_, x, y) in candidates.OrderBy(c => c.D))
            {
                if (Floor(x, y) is not double z || !Open(x, y, z)) continue;
                if (height is float anchorZ && Math.Abs(z - (anchorZ - 32)) > 160) continue;
                bool flat = true;
                for (int dx = -192; dx <= 192 && flat; dx += 48)
                    for (int dy = -192; dy <= 192 && flat; dy += 48)
                        flat = Floor(x + dx, y + dy, z) is double f && Math.Abs(f - z) <= 24 && Open(x + dx, y + dy, f);
                if (!flat) continue;
                var spawn = new Vector3f((float)x, (float)y, MathF.Round((float)z + 32));
                if (++tried > 400) break;
                try { NativeAuthoredSpawn.RequirePortalArrivalSupported(doc with { Spawn = spawn }); }
                catch (InvalidDataException) { continue; }
                // Face the longest run of open floor (8 directions, up to 2,048 units).
                int heading = 0; double longest = -1;
                for (int h = 0; h < 256; h += 32)
                {
                    double a = h * Math.PI / 128, run = 0, level = z;
                    for (double d = 32; d <= 2048; d += 32)
                    {
                        if (Floor(x + Math.Cos(a) * d, y + Math.Sin(a) * d, level) is not double f || Math.Abs(f - level) > 40 || !Open(x + Math.Cos(a) * d, y + Math.Sin(a) * d, f)) break;
                        run = d; level = f;
                    }
                    if (run > longest) { longest = run; heading = h; }
                }
                return (spawn, heading);
            }
            return null;
        }
    }
}
