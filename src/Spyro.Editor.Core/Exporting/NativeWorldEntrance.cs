using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Sky;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Where the way into the New World stands in one of the game's own homeworlds (<see cref="NativeWorldEntrance"/>).</summary>
/// <param name="HomeworldKey">The homeworld it is built into (<see cref="NewWorldDocument.HomeworldKeys"/>: Artisans, Peace
/// Keepers, Magic Crafters, Beast Makers, Dream Weavers or Gnasty's World).</param>
/// <param name="Position">The bottom middle of its opening, in editor units (game units / 16), on the ground.</param>
/// <param name="Heading">The way Spyro walks in, 0-255 (0 = +X, 64 = +Y), as <see cref="AuthoredPortal.Heading"/>.</param>
/// <param name="Shape">A <see cref="DoorwayShapes"/> id.</param>
/// <param name="Scale">Its size, <see cref="DoorwayShapes.MinScale"/>..<see cref="DoorwayShapes.MaxScale"/>.</param>
public sealed record WorldEntranceSpec(string HomeworldKey, Vector3f Position, int Heading, string Shape, double Scale = 1)
{
    /// <summary>The homeworld's own texture (material number in its texture table) for the frame's front and back;
    /// -1: the texture of the homeworld's own doorway arches.</summary>
    public int FaceTexture { get; init; } = -1;
    /// <summary>The texture of the frame's sides, top and inside; -1: as <see cref="FaceTexture"/>.</summary>
    public int SideTexture { get; init; } = -1;
    /// <summary>Spyro bumps into the frame (its faces get collision), as he does into the game's own arches.</summary>
    public bool SolidFrame { get; init; } = true;
}

/// <summary>Which copy of the new world's sky the entrance carries (<see cref="NativeWorldEntrance"/>). The game draws it in
/// the opening and as the sky of the first half of the flight in (and of the second half of the flight back).</summary>
public enum WorldEntranceSky
{
    /// <summary>The largest of the others that fits the homeworld's memory.</summary>
    Auto,
    /// <summary>The whole sky, as the game's own doorways carry their level's.</summary>
    Full,
    /// <summary>The sky's parts nearest the horizon (what the opening shows) that fit the room left; the rest of the sky
    /// is filled with the dropped parts' average colour.</summary>
    Reduced,
    /// <summary>One colour (the sky's horizon colour, 16 bytes): the opening shows it flat.</summary>
    Plain,
}

/// <summary>What <see cref="NativeWorldEntrance.Apply"/> made.</summary>
/// <param name="LevelData">The homeworld's new level data (its WAD data entry).</param>
/// <param name="Overlay">Its overlay (WAD entry before the data): unchanged today, returned so callers install both.</param>
/// <param name="DoorwayIndex">The entrance's place in the homeworld's doorway table (0-5).</param>
/// <param name="PathRow">The row of its path object (written into the doorway record at +0x18).</param>
/// <param name="Rows">The rows of its three objects: path (0x18E), floating name (class 1), return controller (0x11E).</param>
/// <param name="RoomLeft">Bytes still free below the draw buffers when the homeworld loads (current memory mode).</param>
public sealed record WorldEntranceResult(byte[] LevelData, byte[] Overlay, int DoorwayIndex, int PathRow, IReadOnlyList<int> Rows,
    WorldEntranceSky Sky, int SkyBytes, int SkyParts, int OpeningPoints, int EntryTriangles, int FrameFaces, int FrameSectors,
    int FrameTriangles, int FaceTexture, int SideTexture, int AddedBytes, long RoomLeft)
{
    /// <summary>A readable line for Create BIN's report.</summary>
    public string Describe(string homeworld) =>
        $"{homeworld}: the New World's doorway (doorway {DoorwayIndex + 1}, {OpeningPoints}-point opening, {FrameFaces} frame faces in {FrameSectors} " +
        $"part{(FrameSectors == 1 ? "" : "s")}, {EntryTriangles} entry triangles" + (FrameTriangles > 0 ? $", {FrameTriangles} frame collision triangles" : "") +
        $"); sky: {Sky switch { WorldEntranceSky.Full => "the whole sky", WorldEntranceSky.Reduced => $"the sky near the horizon ({SkyParts} parts)", _ => "one colour" }} " +
        $"({SkyBytes:N0} bytes); {AddedBytes:N0} bytes added, {RoomLeft:N0} bytes of memory left.";
}

/// <summary>
/// The way into the New World (<see cref="NativeLevelWindow"/>, homeworld <see cref="Destination"/> = 70) from one of the
/// game's own homeworlds: a sixth doorway added to that homeworld's own level data. USA addresses:
/// <list type="bullet">
/// <item><b>Doorway table.</b> Level data subfile 1 holds, in order, the texture, environment (terrain sectors),
/// visibility, special-surface, collision and sky components (each length-prefixed), then the doorway table: a count and
/// the records, then particles and sounds. The loader walks it (0x800130A0..0x800131A0): count to 0x800758BC, one record
/// pointer per doorway to 0x80078640 (six slots), record +0 = its sky header (record + 0x20 + 12n), and relocates the
/// record's sky. Nothing outside the subfile points into it, so a record can be added at the table's end.</item>
/// <item><b>Record.</b> +4 point count n (at most 10, convex, anticlockwise seen from the normal's side), +8/+0xC/+0x10 the
/// normal (4096 = 1, the way Spyro walks in), +0x14 sector (-1: always considered), +0x18 the path object's row, +0x1C the
/// return token, +0x20 n points (game units), a 20-byte sky header (filled by the loader), then a whole sky block. The
/// entrance's token is 70: leaving homeworld 70 for this homeworld through a doorway stores 70 as where Spyro came from
/// (0x800758AC, the level window's hook at 0x80057178), and arrival takes the doorway whose +0x1C matches (sky
/// 0x8001596C, path 0x80015F0C).</item>
/// <item><b>Entry.</b> A collision triangle whose flag byte (low 6 bits) names a special surface of type 6 (+4 destination,
/// +8 doorway index; table 0x800785B8, count 0x800785BC) starts the walk in (0x80048CF4 -> 0x80056F64): destination to
/// 0x800758B4 (ids above 64 need the window's guard at 0x80057110), index to 0x8007576C, then the record's path object
/// (+0x18). The triangles fan the opening from point 0 in both windings (<see cref="NativeHubPortals.EntryTriangles"/>).
/// A triangle at or past the collision's flag count reads flag 0xFF (0x8004C94C), so the flag count grows to cover the
/// new triangles and the gap gets 0xFF.</item>
/// <item><b>Objects.</b> A copy of one of the homeworld's own doorways' three objects, moved rigidly with the doorway: the
/// path object (0x18E; its properties point at a two-node path), the floating name (class 1; its first property word is
/// the name number, <see cref="NativeLevelWindow.VirtualIndex"/>(70) = 36, which the window's name hook reads) and the
/// return controller (0x11E, carrying a copy of the path). They are added as the editor adds any object to an original
/// level (<see cref="UniversalMobyComposer"/>); the name's letters take spare object slots.</item>
/// <item><b>Frame.</b> The shape's arch (<see cref="DoorwayShapes.Frame"/>) as new terrain sectors appended to the
/// environment (<see cref="NativeAuthoredMeshRenderer.AddedSectors"/>: the homeworld's own textures, subdivided only up
/// close), listed in every view group of the visibility component, with optional collision.</item>
/// <item><b>Sky.</b> The record carries the new world's sky (<see cref="WorldEntranceSky"/>); a plain sky has no parts and
/// the opening is filled with its colour (the opening fill 0x8004FEA0 uses the sky header's colour).</item>
/// </list>
/// The executable needs nothing beyond the level window (its doorway guard and arrival hook). Nothing else in the homeworld
/// changes: its doorways, special surfaces, collision triangles and sectors keep their numbers and bytes.
/// </summary>
public static class NativeWorldEntrance
{
    /// <summary>Where the entrance leads: the new world's homeworld.</summary>
    public const int Destination = NativeLevelWindow.FirstId;
    /// <summary>Memory Auto keeps free beyond the game's own margin when it chooses a larger sky.</summary>
    public const int AutoCushion = 0x800;
    public const string PathId = "world-entrance-path", NameId = "world-entrance-name", ReturnId = "world-entrance-return";
    private const int MaxSectors = 255, MaxFaces = 7167, MaxSpecialSurfaces = 63, MaxCollisionTriangles = 0x7FFF, MaxListedSectors = 254;

    /// <summary>The doorway the new world's homeworld (70) needs for the way back: it leads to <paramref name="entryHomeworldId"/>
    /// (type-6 destination), carries that id as its return token (record +0x1C: arriving from the entry homeworld puts Spyro
    /// there), and its floating name shows that homeworld's name (lettering word).</summary>
    public static (int Destination, int ReturnToken, int Lettering) WayBack(int entryHomeworldId)
    {
        if (entryHomeworldId is < 10 or > 60 || entryHomeworldId % 10 != 0) throw new InvalidDataException($"{entryHomeworldId} is not one of the game's homeworlds.");
        return (entryHomeworldId, entryHomeworldId, NativeLevelWindow.VirtualIndex(entryHomeworldId));
    }

    /// <summary>Builds the entrance into <paramref name="levelData"/> (the homeworld's data entry as the export has staged it:
    /// after its terrain, sky and object edits) and returns the new data. <paramref name="overlay"/> and
    /// <paramref name="executable"/> are the staged homeworld overlay and executable (read only: the copy buffer, the
    /// object classes); <paramref name="destinationSky"/> is the sky block homeworld 70 has in this export. Refuses (with
    /// <see cref="InvalidDataException"/>, in plain words) a homeworld that already has six doorways or an entrance, a spot
    /// outside the map, a level whose tables are full, and one without room in the current memory mode.</summary>
    public static WorldEntranceResult Apply(UniversalSourceLibrary originals, LevelCatalog catalog, WorldEntranceSpec spec,
        byte[] levelData, byte[] overlay, byte[] executable, byte[] destinationSky, WorldEntranceSky sky = WorldEntranceSky.Auto)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var level = RequireHomeworld(catalog, spec.HomeworldKey);
        RequireSky(destinationSky);
        if (sky != WorldEntranceSky.Auto) return Build(originals, catalog, level, spec, levelData, overlay, executable, Sky(destinationSky, sky), sky);
        // The sky only lengthens the record, so the room left with a plain one says how much sky fits.
        var plain = Build(originals, catalog, level, spec, levelData, overlay, executable, Sky(destinationSky, WorldEntranceSky.Plain), WorldEntranceSky.Plain);
        long budget = plain.RoomLeft - AutoCushion + plain.SkyBytes;
        if (destinationSky.Length <= budget)
            return Build(originals, catalog, level, spec, levelData, overlay, executable, destinationSky.ToArray(), WorldEntranceSky.Full);
        byte[] reduced = ReducedSky(destinationSky, (int)Math.Max(0, Math.Min(int.MaxValue, budget)));
        if (PartCount(reduced) > 0)
            return Build(originals, catalog, level, spec, levelData, overlay, executable, reduced, WorldEntranceSky.Reduced);
        return plain;
    }

    /// <summary>The sky block the entrance carries for <paramref name="choice"/> (Auto: the whole sky; Reduced: at most
    /// <paramref name="reducedBytes"/>).</summary>
    public static byte[] Sky(byte[] destinationSky, WorldEntranceSky choice, int reducedBytes = DefaultReducedSkyBytes)
    {
        RequireSky(destinationSky);
        return choice switch
        {
            WorldEntranceSky.Plain => PlainSky(HorizonColour(destinationSky)),
            WorldEntranceSky.Reduced => ReducedSky(destinationSky, reducedBytes),
            _ => destinationSky.ToArray(),
        };
    }

    /// <summary>The size an explicitly requested reduced sky keeps to.</summary>
    public const int DefaultReducedSkyBytes = 8192;

    /// <summary>A sky of one colour and no parts (the opening is filled with <paramref name="colour"/>).</summary>
    public static byte[] PlainSky(ColorRgba colour)
    {
        byte[] b = new byte[16];
        W(b, 0, 16); b[4] = colour.R; b[5] = colour.G; b[6] = colour.B;
        return b;
    }

    /// <summary>The new world sky's parts nearest the line of sight through a doorway (centres closest to 10° above the
    /// horizon first) that fit in <paramref name="maxBytes"/>, in the sky's own drawing order; where parts were left out
    /// the opening is filled with their average colour. Fewer than one part fits: a plain sky of that colour.</summary>
    public static byte[] ReducedSky(byte[] block, int maxBytes)
    {
        RequireSky(block);
        int parts = PartCount(block);
        var all = new List<(int Index, byte[] Bytes, double Elevation)>();
        for (int p = 0; p < parts; p++)
        {
            int at = 4 + I(block, 12 + 4 * p), end = p + 1 < parts ? 4 + I(block, 12 + 4 * (p + 1)) : block.Length;
            // Centre in the camera axes: VX = -Y, VY = -Z, VZ = X (world up = -VY).
            double vx = S(block, at), vy = S(block, at + 2), vz = S(block, at + 6);
            all.Add((p, block.AsSpan(at, end - at).ToArray(), Math.Atan2(-vy, Math.Sqrt(vx * vx + vz * vz)) * 180 / Math.PI));
        }
        var kept = new HashSet<int>();
        int size = 12;
        foreach (var part in all.OrderBy(a => Math.Abs(a.Elevation - 10)).ThenBy(a => a.Index))
            if (size + 4 + part.Bytes.Length <= maxBytes) { kept.Add(part.Index); size += 4 + part.Bytes.Length; }
        long r = 0, g = 0, b = 0, n = 0;
        foreach (var part in all.Where(a => !kept.Contains(a.Index)))
        {
            int colours = BinaryPrimitives.ReadUInt16LittleEndian(part.Bytes.AsSpan(18)), at = 24 + 4 * BinaryPrimitives.ReadUInt16LittleEndian(part.Bytes.AsSpan(12));
            for (int k = 0; k < colours && at + 4 * k + 3 <= part.Bytes.Length; k++) { r += part.Bytes[at + 4 * k]; g += part.Bytes[at + 4 * k + 1]; b += part.Bytes[at + 4 * k + 2]; n++; }
        }
        var fill = n == 0 ? new ColorRgba(block[4], block[5], block[6]) : new ColorRgba((byte)(r / n), (byte)(g / n), (byte)(b / n));
        if (kept.Count == 0) return PlainSky(fill);
        var list = all.Where(a => kept.Contains(a.Index)).ToList();
        int head = 12 + 4 * list.Count;
        byte[] result = new byte[head + list.Sum(k => k.Bytes.Length)];
        W(result, 0, result.Length); result[4] = fill.R; result[5] = fill.G; result[6] = fill.B; result[7] = block[7]; W(result, 8, list.Count);
        int cursor = head;
        for (int k = 0; k < list.Count; k++) { W(result, 12 + 4 * k, cursor - 4); list[k].Bytes.CopyTo(result, cursor); cursor += list[k].Bytes.Length; }
        return result;
    }

    /// <summary>The average colour of the sky from the horizon to 30° up (what Spyro sees through a doorway).</summary>
    public static ColorRgba HorizonColour(byte[] block)
    {
        if (PartCount(block) == 0) return new(block[4], block[5], block[6]);
        var image = NativeSkyPreview.Render(block, 256);
        long r = 0, g = 0, b = 0, n = 0;
        // Rows: the top is up (+90°), the middle the horizon.
        for (int y = image.Height / 3; y < image.Height / 2; y++)
            for (int x = 0; x < image.Width; x++) { var c = image.Pixels[y * image.Width + x]; r += c.R; g += c.G; b += c.B; n++; }
        return new((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }

    /// <summary>The sky a scene of the new world shows (its imported sky, the game level's sky it borrows, or its plain sky
    /// colour), as one native sky block: what the editor checks the entrance with before Create BIN knows the final one.</summary>
    public static byte[] SceneSky(string originalUSA, SceneDocument scene) =>
        NativeAuthoredMesh.CaptureSky(originalUSA, scene) ?? PlainSky(scene.SkyColor);

    /// <summary>The homeworld's own doorway whose objects and record head the entrance copies (its lowest destination).</summary>
    public static NativeHubPortals.DoorwayTemplate Template(UniversalSourceLibrary originals, LevelCatalog catalog, string homeworldKey) =>
        NativeHubPortals.Templates(originals.ImagePath, catalog, homeworldKey).OrderBy(p => p.Key).First().Value;

    /// <summary>The texture (material number) the homeworld's own doorway arches use most: the textured near faces within 80
    /// units of the template doorway's opening, in the original level.</summary>
    public static int ArchTexture(UniversalSourceLibrary originals, LevelCatalog catalog, string homeworldKey)
    {
        var template = Template(originals, catalog, homeworldKey);
        var outline = Enumerable.Range(0, template.PointCount).Select(i => template.Point(i)).Select(p => (X: p.X / 16.0, Y: p.Y / 16.0, Z: p.Z / 16.0)).ToArray();
        var counts = new Dictionary<int, int>();
        foreach (var face in NearFaces(Body.Read(originals.Get(homeworldKey).Data).Components[1]))
            if (face.Material < 127 && outline.Any(o => Math.Abs(o.X - face.At.X) + Math.Abs(o.Y - face.At.Y) + Math.Abs(o.Z - face.At.Z) < 80))
                counts[face.Material] = counts.GetValueOrDefault(face.Material) + 1;
        return counts.Count == 0 ? 0 : counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key;
    }

    /// <summary>How many textures (materials) the homeworld's level data has.</summary>
    public static int TextureCount(byte[] levelData) => I(Body.Read(new UniversalLevelData(levelData)).Components[0], 4);

    // ------------------------------------------------------------------------------------------------------------------

    private static WorldEntranceResult Build(UniversalSourceLibrary originals, LevelCatalog catalog, LevelDefinition level, WorldEntranceSpec spec,
        byte[] levelData, byte[] overlayBytes, byte[] executable, byte[] skyBlock, WorldEntranceSky skyKind)
    {
        string name = level.DisplayName;
        var before = new UniversalLevelData(levelData.ToArray());
        var body = Body.Read(before);
        // ---- what the homeworld has now ----
        if (body.Records.Any(r => I(r, 0x1C) == Destination)) throw new InvalidDataException($"{name} already has a doorway to the New World.");
        if (body.Records.Any(r => I(r, 0) != 0)) throw new InvalidDataException($"{name}'s doorway table is not the game's own (a record's sky pointer is set).");
        if (body.Records.Count >= NativeHubPortals.MaxDoorways)
            throw new InvalidDataException($"{name} already has {body.Records.Count} doorways; the game keeps at most {NativeHubPortals.MaxDoorways}, so there is no room for the New World's doorway.");
        int doorway = body.Records.Count;
        byte[] special = body.Components[3];
        int specialCount = I(special, 4);
        if (specialCount is < 0 or >= MaxSpecialSurfaces)
            throw new InvalidDataException($"{name} has no room for another kind of ground ({specialCount} of {MaxSpecialSurfaces}), so the doorway's entry can't be added.");
        var bindings = UniversalExeBindings.Read(executable, level.LevelId);
        var overlay = new UniversalOverlay(overlayBytes, bindings);
        var shape = DoorwayShapes.Find(spec.Shape) ?? throw new InvalidDataException($"There is no doorway shape \"{spec.Shape}\".");
        if (spec.Scale < DoorwayShapes.MinScale - 1e-6 || spec.Scale > DoorwayShapes.MaxScale + 1e-6)
            throw new InvalidDataException($"A doorway's size is {DoorwayShapes.MinScale:P0} to {DoorwayShapes.MaxScale:P0} of the shape's own.");
        if (spec.Heading is < 0 or > 255) throw new InvalidDataException("A doorway's heading is 0-255.");

        // ---- the record ----
        var template = Template(originals, catalog, level.Key);
        var portal = new AuthoredPortal(Destination, spec.Position, spec.Heading) { Shape = shape.Id, Scale = (float)spec.Scale };
        var (record, triggers, place) = NativeHubPortals.PlaceRecord(template, portal);
        int points = I(record, 4);
        int skyAt = 0x20 + 12 * points + 20;
        record = [.. record.AsSpan(0, skyAt), .. skyBlock];
        W(record, 0x00, 0); W(record, 0x14, -1); W(record, 0x18, 0); W(record, 0x1C, Destination);
        for (int i = 0; i < points; i++)
            for (int a = 0; a < 3; a++)
                if (I(record, 0x20 + 12 * i + 4 * a) is < 0 or >= 16384 * 16)
                    throw new InvalidDataException("There is no room for the doorway there: part of it would be outside the game's map. Move it further in.");

        // ---- the frame ----
        int textures = I(body.Components[0], 4);
        int faceTexture = spec.FaceTexture >= 0 ? spec.FaceTexture : ArchTexture(originals, catalog, level.Key);
        int sideTexture = spec.SideTexture >= 0 ? spec.SideTexture : faceTexture;
        foreach (int t in new[] { faceTexture, sideTexture })
            if (t < 0 || t >= textures || t > 127) throw new InvalidDataException($"{name} has textures 0-{textures - 1}; {t} is not one of them.");
        var frame = DoorwayShapes.Frame(shape, spec.Scale, spec.Position, spec.Heading);
        if (frame.SelectMany(f => f.Corners).Any(c => c.X is < 0 or > 16383 || c.Y is < 0 or > 16383 || c.Z is < 0 or > 16383))
            throw new InvalidDataException("There is no room for the doorway there: part of its frame would be outside the game's map. Move it further in.");
        var light = LocalLight(body.Components[1], spec.Position);
        ColorRgba Lit(ColorRgba shade) => new((byte)Math.Min(255, light.R * shade.R / 128), (byte)Math.Min(255, light.G * shade.G / 128), (byte)Math.Min(255, light.B * shade.B / 128));
        var faces = frame.Select(f =>
        {
            var c = Enumerable.Repeat(Lit(f.Shade), f.Corners.Length).ToArray();
            return new NativeAuthoredMeshRenderer.AddedFace(f.Corners, c, c, f.Part == DoorwayFramePart.Face ? faceTexture : sideTexture);
        }).ToArray();
        var env = body.Components[1];
        int sectorCount = I(env, 4);
        var sectorBytes = Sectors(env);
        // Each sector's near faces own a run of the per-face table from its +12 index (retail runs are not in sector
        // order; a sector without near faces has 0xFFFF); the frame's runs start after the highest.
        int firstCache = 1;
        foreach (var s in sectorBytes)
            if ((U(s, 12) & 0xFFFF) is var c && c != 0xFFFF) firstCache = Math.Max(firstCache, (int)c + s[22]);
        var added = NativeAuthoredMeshRenderer.AddedSectors(faces, firstCache);
        int frameFaces = added.Sum(s => s[22]);
        if (sectorCount + added.Count > MaxSectors)
            throw new InvalidDataException($"{name} has {sectorCount} terrain parts; the doorway's frame needs {added.Count} more and the game numbers at most {MaxSectors}. Choose a simpler shape.");
        if (firstCache + frameFaces > MaxFaces + 1)
            throw new InvalidDataException($"{name}'s terrain would have {firstCache - 1 + frameFaces:N0} faces; the game's per-face table holds {MaxFaces:N0}.");
        byte[] newEnv = PackSectors([.. sectorBytes, .. added]);
        byte[] newVisibility = AddToViewGroups(body.Components[2], Enumerable.Range(sectorCount, added.Count).ToArray(), name);

        // ---- entry and frame collision ----
        // The entry triangles need a flag (their ground kind): they go right after the level's flagged triangles, so the
        // flag table grows by them alone (a triangle at or past the flag count reads 0xFF, solid ground). The frame's
        // solid triangles need no flag and go at the end.
        List<(Vector3f A, Vector3f B, Vector3f C)> entryTriangles = [.. triggers];
        List<(Vector3f A, Vector3f B, Vector3f C)> frameTriangles = [];
        if (spec.SolidFrame)
            foreach (var f in frame)
            {
                var v = f.Corners;
                foreach (var (a, b, c) in v.Length == 4 ? new[] { (v[0], v[1], v[2]), (v[0], v[2], v[3]) } : [(v[0], v[1], v[2])])
                {
                    if (Area(a, b, c) == 0) continue;
                    var (wa, wb, wc) = NativeAuthoredMesh.CollisionWinding(a, b, c);
                    foreach (var piece in Pieces(wa, wb, wc)) frameTriangles.Add(piece);
                }
            }
        int entry = entryTriangles.Count;
        byte[] newCollision = AddCollision(body.Components[4], entryTriangles, (byte)specialCount, frameTriangles, name, out int firstEntry);
        byte[] newSpecial = AddSpecialSurface(special, AuthoredSurfaceKind.Portal(Destination, doorway).NativeRecord());

        // ---- the level data with the new components ----
        var components = body.Components.ToArray();
        components[1] = newEnv; components[2] = newVisibility; components[3] = newSpecial; components[4] = newCollision;
        var data = body.Write(before, components, [.. body.Records, record]);

        string NoRoom(long bytes) => $"{name} has no room for the New World's doorway: it needs {bytes:N0} more bytes of the console's memory" +
            (GameRam.Extended ? "." : skyKind == WorldEntranceSky.Plain
                ? " even with a one-colour sky. Choose another homeworld for the entrance, a simpler shape, or Game Settings › Memory › Emulator only (8 MB RAM)."
                : ". A smaller sky (one colour) may fit.");
        if (!GameRam.Fits(data, overlay.CopyBuffer)) throw new InvalidDataException(NoRoom(GameRam.Shortfall(data, overlay.CopyBuffer)));

        // ---- the three objects ----
        var placements = Objects(originals, level.Key, template, place);
        UniversalComposeResult composed;
        try
        {
            composed = UniversalMobyComposer.Compose(level, data.Bytes, overlayBytes, executable, k => originals.Get(k), originals.SharedClasses,
                [], placements, sharedModelPackage: originals.SharedModelPackage);
        }
        catch (UniversalBudgetException e) { throw new InvalidDataException(NoRoom(Math.Max(1, -e.Budget.FinalHeadroom)), e); }
        if (composed.Classes.Any(c => c.ModelImported || c.BehaviorImported) || composed.VramBytesUsed != 0)
            throw new InvalidDataException($"{name}'s doorway objects are not its own any more (their models or behaviour would have to be copied in).");
        if (!composed.Executable.AsSpan().SequenceEqual(executable))
            throw new InvalidDataException($"Adding {name}'s doorway objects would change the game's program; the New World's doorway needs none.");
        var rows = composed.NewRowIndices;
        if (rows.Count != 3 || rows.Any(r => r is < 0 or > 255)) throw new InvalidDataException($"{name}'s doorway objects were not placed.");
        var result = new UniversalLevelData(composed.LevelData);
        BindPathRow(result, doorway, rows[0]);

        // ---- memory and readback ----
        if (!GameRam.Fits(result, overlay.CopyBuffer)) throw new InvalidDataException(NoRoom(GameRam.Shortfall(result, overlay.CopyBuffer)));
        Verify(before, result, body, record, doorway, specialCount, firstEntry, entry, frameTriangles.Count, sectorCount, added.Count, name);
        return new(result.Bytes, composed.Overlay, doorway, rows[0], rows, skyKind, skyBlock.Length, PartCount(skyBlock), points, entry, frameFaces,
            added.Count, frameTriangles.Count, faceTexture, sideTexture,
            (int)(UniversalMobyComposer.RuntimeEnd(result, overlay.CopyBuffer) - UniversalMobyComposer.RuntimeEnd(before, overlay.CopyBuffer)),
            GameRam.ModelRoom(result, overlay.CopyBuffer));
    }

    private static LevelDefinition RequireHomeworld(LevelCatalog catalog, string key)
    {
        if (!NewWorldDocument.HomeworldKeys.Contains(key))
            throw new InvalidDataException($"The New World's doorway goes in one of the game's homeworlds ({string.Join(", ", NewWorldDocument.HomeworldKeys)}), not \"{key}\".");
        var level = catalog.FindByKey(key) ?? throw new InvalidDataException($"The level catalog has no {key}.");
        if (level.LevelId % 10 != 0 || level.LevelId is < 10 or > 60) throw new InvalidDataException($"{level.DisplayName} is not a homeworld.");
        return level;
    }

    private static void RequireSky(byte[] block)
    {
        ArgumentNullException.ThrowIfNull(block);
        bool plain = block.Length == 16 && I(block, 0) == 16 && I(block, 8) == 0;
        if (!plain && (!Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(block) || I(block, 0) != block.Length))
            throw new InvalidDataException("The New World's sky is not one native sky block.");
    }

    private static int PartCount(byte[] block) => I(block, 8);

    /// <summary>The three objects of <paramref name="template"/>'s doorway, moved with it (positions, turn, path nodes), the
    /// name showing the New World's homeworld.</summary>
    internal static IReadOnlyList<UniversalMobyPlacement> Objects(UniversalSourceLibrary originals, string key, NativeHubPortals.DoorwayTemplate template,
        NativeHubPortals.DoorwayPlacement place)
    {
        var home = originals.Get(key).Data;
        int bias = template.SourceMobyIndexBias, yawTurn = (int)Math.Round(place.Turn * 128 / Math.PI);
        var control = template.Control;
        UniversalMobyPlacement Object(int row, string id, Dictionary<int, int>? extra = null)
        {
            byte[] r = home.Row(row);
            var (x, y, z) = place.Move(I(r, 0x0C), I(r, 0x10), I(r, 0x14));
            var captured = home.CaptureRow(key, row);
            Dictionary<int, int> overrides = extra ?? [];
            // Path blocks reached from the properties: node count, current node, then nodes of x, y, z, 0.
            foreach (var (_, target) in captured.InternalPointers)
            {
                var p = captured.Properties;
                if (target + 8 > p.Length) continue;
                int count = p[target];
                if (count is < 1 or > 16 || p[target + 1] >= count || p[target + 2] != 0 || p[target + 3] != 0 || target + 8 + 16 * count > p.Length) continue;
                for (int n = 0; n < count; n++)
                {
                    int at = target + 8 + 16 * n;
                    var (px, py, pz) = place.Move(I(p, at), I(p, at + 4), I(p, at + 8));
                    overrides[at] = px; overrides[at + 4] = py; overrides[at + 8] = pz;
                }
            }
            return new(key, row, x, y, z, Yaw: (r[0x46] + yawTurn) & 0xFF, EditorId: id) { PropertyWordOverrides = overrides.Count > 0 ? overrides : null };
        }
        var name = home.CaptureRow(key, control.LetteringTrueIndex - bias);
        if (name.ClassId != 1 || name.Properties.Length < 4 || I(name.Properties, 0) != NativeLevelWindow.VirtualIndex(template.DestinationLevelId))
            throw new InvalidDataException($"The {key} doorway's floating name is not the game's own.");
        return
        [
            Object(control.PathTrueIndex - bias, PathId),
            Object(control.LetteringTrueIndex - bias, NameId, new() { [0] = NativeLevelWindow.VirtualIndex(Destination) }),
            Object(control.CompanionTrueIndex - bias, ReturnId),
        ];
    }

    private static void BindPathRow(UniversalLevelData data, int doorway, int row)
    {
        var body = Body.Read(data);
        if (body.Records.Count != doorway + 1 || I(body.Records[doorway], 0x1C) != Destination)
            throw new InvalidDataException("The New World's doorway record is not where it was written.");
        var scene = data.Scene();
        int at = data.Subfiles()[3].Offset + scene.RowsOffset + row * UniversalLevelData.RowStride;
        if (BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(at + 0x36)) != 0x18E)
            throw new InvalidDataException("The New World's doorway path object is not where it was placed.");
        W(data.Bytes, body.RecordOffsets[doorway] + 0x18, row);
    }

    // ---- subfile 1: six components, the doorway table, then particles and sounds ----
    private sealed class Body
    {
        public required byte[][] Components;
        public required List<byte[]> Records;
        public required List<int> RecordOffsets;
        /// <summary>From the end of the doorway table to the end of the sounds (particles, sounds), and the subfile's offset.</summary>
        public required byte[] Tail;
        public required int Offset, Length;

        public static Body Read(UniversalLevelData data)
        {
            var (offset, length) = data.Subfiles()[1];
            var b = data.Bytes;
            int at = offset, end = offset + length;
            var components = new byte[6][];
            string[] names = ["texture", "terrain", "visibility", "special-surface", "collision", "sky"];
            for (int n = 0; n < 6; n++)
            {
                int size = at + 4 <= end ? I(b, at) : 0;
                if (size < 4 || size % 4 != 0 || at + (long)size > end) throw new InvalidDataException($"The level's {names[n]} data is not the game's own layout.");
                components[n] = b.AsSpan(at, size).ToArray(); at += size;
            }
            int count = I(b, at);
            if (count is < 0 or > NativeHubPortals.MaxDoorways) throw new InvalidDataException("The level's doorway table is not the game's own layout.");
            at += 4;
            List<byte[]> records = []; List<int> offsets = [];
            for (int k = 0; k < count; k++)
            {
                int pts = at + 8 <= end ? I(b, at + 4) : 0;
                if (pts is < 3 or > DoorwayShapes.MaxOutlinePoints) throw new InvalidDataException($"The level's doorway {k + 1} is not the game's own layout.");
                int skyAt = at + 0x20 + 12 * pts + 20, sky = skyAt + 4 <= end ? I(b, skyAt) : 0;
                if (sky < 4 || sky % 4 != 0 || skyAt + (long)sky > end) throw new InvalidDataException($"The level's doorway {k + 1} sky is not the game's own layout.");
                offsets.Add(at); records.Add(b.AsSpan(at, skyAt + sky - at).ToArray()); at = skyAt + sky;
            }
            var sound = new NativeLevelSoundLayout(b);
            if (sound.DataOffset != offset || sound.SoundEnd < at || sound.SoundEnd > end)
                throw new InvalidDataException("The level's particles and sounds are not where the game keeps them.");
            if (b.AsSpan(sound.SoundEnd, end - sound.SoundEnd).IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException("The level's data has bytes after its sounds that the editor does not know.");
            return new() { Components = components, Records = records, RecordOffsets = offsets, Tail = b.AsSpan(at, sound.SoundEnd - at).ToArray(), Offset = offset, Length = length };
        }

        /// <summary>The level data with these components and doorway records (the tail unchanged), subfile 1 grown by whole
        /// sectors when its padding does not take the growth.</summary>
        public UniversalLevelData Write(UniversalLevelData data, byte[][] components, IReadOnlyList<byte[]> records)
        {
            byte[] count = new byte[4]; W(count, 0, records.Count);
            byte[] used = [.. components.SelectMany(c => c), .. count, .. records.SelectMany(r => r), .. Tail];
            var output = new UniversalLevelData(data.Bytes.ToArray());
            if (used.Length > Length) output.GrowSubfile(1, UniversalLevelData.Align(used.Length - Length, 0x800));
            var (offset, length) = output.Subfiles()[1];
            output.Bytes.AsSpan(offset, length).Clear();
            used.CopyTo(output.Bytes, offset);
            return output;
        }
    }

    internal static byte[][] Sectors(byte[] env)
    {
        int count = I(env, 4);
        if (count is < 0 or > MaxSectors || 8 + 4 * count > env.Length) throw new InvalidDataException("The level's terrain parts are not the game's own layout.");
        var result = new byte[count][];
        for (int n = 0; n < count; n++)
        {
            int at = 4 + I(env, 8 + 4 * n), end = n + 1 < count ? 4 + I(env, 8 + 4 * (n + 1)) : env.Length;
            if (at < 8 + 4 * count || end < at + 28 || end > env.Length || at % 4 != 0) throw new InvalidDataException("The level's terrain parts are not in the game's order.");
            result[n] = env.AsSpan(at, end - at).ToArray();
        }
        return result;
    }

    internal static byte[] PackSectors(IReadOnlyList<byte[]> sectors)
    {
        int at = 8 + 4 * sectors.Count;
        byte[] b = new byte[at + sectors.Sum(s => s.Length)];
        W(b, 0, b.Length); W(b, 4, sectors.Count);
        for (int n = 0; n < sectors.Count; n++) { W(b, 8 + 4 * n, at - 4); sectors[n].CopyTo(b, at); at += sectors[n].Length; }
        return b;
    }

    /// <summary>Adds the sector numbers to every view group's list (a level without view groups draws every part anyway).</summary>
    internal static byte[] AddToViewGroups(byte[] b, IReadOnlyList<int> ids, string name)
    {
        if (b.Length < 16 || ids.Count == 0) return b.ToArray();
        int count = I(b, 8), sky = 4 + I(b, 4);
        if (count is < 0 or > 255 || 12 + 4 * count > sky || sky > b.Length) throw new InvalidDataException($"{name}'s visibility data is not the game's own layout.");
        if (ids.Any(id => id is < 0 or > MaxListedSectors)) throw new InvalidDataException($"{name} has no sector number left for the doorway's frame.");
        List<byte[]> lists = [];
        for (int n = 0; n < count; n++)
        {
            int at = 4 + I(b, 12 + 4 * n), end = n + 1 < count ? 4 + I(b, 12 + 4 * (n + 1)) : sky;
            if (at < 12 + 4 * count || end <= at || end > sky) throw new InvalidDataException($"{name}'s visibility data is not in the game's order.");
            var list = b.AsSpan(at, end - at);
            int terminal = list.IndexOf((byte)255);
            if (terminal < 0 || list[terminal..].IndexOfAnyExcept((byte)255) >= 0) throw new InvalidDataException($"{name}'s visibility lists are not the game's own layout.");
            if (terminal + ids.Count > MaxListedSectors) throw new InvalidDataException($"{name} shows too many terrain parts at once from one place to add the doorway's frame.");
            byte[] grown = new byte[UniversalLevelData.Align(terminal + ids.Count + 1, 4)];
            grown.AsSpan().Fill(255); list[..terminal].CopyTo(grown);
            for (int k = 0; k < ids.Count; k++) grown[terminal + k] = (byte)ids[k];
            lists.Add(grown);
        }
        int cursor = 12 + 4 * count, newSky = cursor + lists.Sum(l => l.Length);
        byte[] result = new byte[newSky + b.Length - sky];
        b.AsSpan(0, 12).CopyTo(result);
        W(result, 0, result.Length); W(result, 4, newSky - 4);
        for (int n = 0; n < count; n++) { W(result, 12 + 4 * n, cursor - 4); lists[n].CopyTo(result, cursor); cursor += lists[n].Length; }
        b.AsSpan(sky).CopyTo(result.AsSpan(newSky));
        return result;
    }

    internal static byte[] AddSpecialSurface(byte[] b, byte[] record)
    {
        int count = I(b, 4), table = 8 + 4 * count;
        if (table > b.Length) throw new InvalidDataException("The level's ground kinds are not the game's own layout.");
        for (int n = 0; n < count; n++)
            if (4 + I(b, 8 + 4 * n) is var at && (at < table || at >= b.Length)) throw new InvalidDataException("The level's ground kinds are not in the game's order.");
        int records = b.Length - table;
        byte[] result = new byte[table + 4 + records + record.Length];
        W(result, 0, result.Length); W(result, 4, count + 1);
        for (int n = 0; n < count; n++) W(result, 8 + 4 * n, I(b, 8 + 4 * n) + 4);
        W(result, 8 + 4 * count, table + 4 + records - 4);
        b.AsSpan(table, records).CopyTo(result.AsSpan(table + 4));
        record.CopyTo(result, table + 4 + records);
        return result;
    }

    // ---- collision: length, count, flag count, then (from +4) block tree, blocks, triangles, assignments, flags ----
    internal sealed record CollisionLayout(int Count, int FlagCount, int Tree, int Blocks, int Triangles, int Assignment, int Flags, int UsedBlocks);

    internal static CollisionLayout ReadCollision(byte[] c, string name)
    {
        if (c.Length < 36 || I(c, 0) != c.Length) throw new InvalidDataException($"{name}'s collision is not the game's own layout.");
        int count = I(c, 4), flags = I(c, 8);
        int tree = 4 + I(c, 12), blocks = 4 + I(c, 16), tris = 4 + I(c, 20), assignment = 4 + I(c, 24), flagAt = 4 + I(c, 28);
        if (count is < 0 or > MaxCollisionTriangles || flags < 0 || flags > count || tree != 32 || blocks < tree + 4 || tris < blocks + 4 ||
            assignment != tris + 12 * count || flagAt < assignment + count || flagAt + flags > c.Length)
            throw new InvalidDataException($"{name}'s collision is not the game's own layout.");
        int used = tris - blocks;
        while (used >= 4 && BinaryPrimitives.ReadUInt16LittleEndian(c.AsSpan(blocks + used - 2)) == 0) used -= 2;
        _ = NativeCollisionIndexCodec.Decode(c.AsSpan(tree, blocks - tree), c.AsSpan(blocks, tris - blocks), used, count, false);
        return new(count, flags, tree, blocks, tris, assignment, flagAt, used);
    }

    /// <summary>The collision with <paramref name="flagged"/> inserted right after the level's flagged triangles (each with
    /// <paramref name="flag"/>; the level's later triangles move up by their count) and <paramref name="solid"/> appended
    /// (no flag: solid ground). The lookup grid is rebuilt with every original cell keeping its triangles (renumbered) in
    /// their order.</summary>
    internal static byte[] AddCollision(byte[] c, IReadOnlyList<(Vector3f A, Vector3f B, Vector3f C)> flagged, byte flag,
        IReadOnlyList<(Vector3f A, Vector3f B, Vector3f C)> solid, string name, out int firstFlagged)
    {
        var l = ReadCollision(c, name);
        int k = flagged.Count, count = l.Count + k + solid.Count, flags = l.FlagCount + k;
        firstFlagged = l.FlagCount;
        if (count > MaxCollisionTriangles) throw new InvalidDataException($"{name} would have {count:N0} collision triangles; the game numbers at most {MaxCollisionTriangles:N0}.");
        int Moved(int old) => old < l.FlagCount ? old : old + k;
        var index = NativeCollisionIndexCodec.Decode(c.AsSpan(l.Tree, l.Blocks - l.Tree), c.AsSpan(l.Blocks, l.Triangles - l.Blocks), l.UsedBlocks, l.Count, false);
        bool descending = index.Cells.Values.All(cell => cell.OrderedTriangleIndexes.Zip(cell.OrderedTriangleIndexes.Skip(1)).All(p => p.First > p.Second));
        var cells = index.Cells.ToDictionary(p => p.Key, p => p.Value.OrderedTriangleIndexes.Select(Moved).ToList());
        var added = new List<(int Number, (Vector3f A, Vector3f B, Vector3f C) Triangle)>();
        for (int t = 0; t < k; t++) added.Add((l.FlagCount + t, flagged[t]));
        for (int t = 0; t < solid.Count; t++) added.Add((l.Count + k + t, solid[t]));
        var fresh = new HashSet<int>(added.Select(a => a.Number));
        byte[] words = new byte[12 * count];
        foreach (var (number, (a, b, cc)) in added)
        {
            var (x, y, z) = Pack(a, b, cc);
            W(words, 12 * number, (int)x); W(words, 12 * number + 4, (int)y); W(words, 12 * number + 8, (int)z);
            Vector3f[] v = [a, b, cc];
            for (int gz = (int)v.Min(p => p.Z) >> 8; gz <= (int)v.Max(p => p.Z) >> 8; gz++)
                for (int gy = (int)v.Min(p => p.Y) >> 8; gy <= (int)v.Max(p => p.Y) >> 8; gy++)
                    for (int gx = (int)v.Min(p => p.X) >> 8; gx <= (int)v.Max(p => p.X) >> 8; gx++)
                    {
                        var cell = new NativeCollisionCell(gx, gy, gz);
                        if (!cells.TryGetValue(cell, out var list)) cells.Add(cell, list = []);
                        list.Add(number);
                    }
        }
        // The game's own cells list their triangles from the highest number down; new ones join in that order.
        var rebuilt = NativeCollisionIndexCodec.Create(cells.Select(p => new KeyValuePair<NativeCollisionCell, IReadOnlyList<int>>(p.Key,
            descending ? p.Value.OrderDescending().ToArray() : [.. p.Value.Where(fresh.Contains).OrderDescending(), .. p.Value.Where(i => !fresh.Contains(i))])), count, descending);
        byte[] tree = rebuilt.CopyTree(), blocks = rebuilt.CopyBlocks();
        int ta = 32 + tree.Length + blocks.Length, aa = ta + 12 * count, fa = aa + UniversalLevelData.Align(count, 4);
        byte[] r = new byte[fa + UniversalLevelData.Align(flags, 4)];
        W(r, 0, r.Length); W(r, 4, count); W(r, 8, flags); W(r, 12, 28); W(r, 16, 28 + tree.Length); W(r, 20, ta - 4); W(r, 24, aa - 4); W(r, 28, fa - 4);
        tree.CopyTo(r, 32); blocks.CopyTo(r, 32 + tree.Length);
        // Triangles and their view-group assignments: the flagged ones, the new flagged, the rest, the new solid.
        c.AsSpan(l.Triangles, 12 * l.FlagCount).CopyTo(words.AsSpan(0));
        c.AsSpan(l.Triangles + 12 * l.FlagCount, 12 * (l.Count - l.FlagCount)).CopyTo(words.AsSpan(12 * (l.FlagCount + k)));
        words.CopyTo(r, ta);
        r.AsSpan(aa, count).Fill(255);
        c.AsSpan(l.Assignment, l.FlagCount).CopyTo(r.AsSpan(aa));
        c.AsSpan(l.Assignment + l.FlagCount, l.Count - l.FlagCount).CopyTo(r.AsSpan(aa + l.FlagCount + k));
        c.AsSpan(l.Flags, l.FlagCount).CopyTo(r.AsSpan(fa));
        r.AsSpan(fa + l.FlagCount, k).Fill(flag);
        return r;
    }

    /// <summary>A collision triangle's three words, its corners rotated (never reversed) so the lowest comes first: X and Y
    /// keep 14 bits and two signed 9-bit offsets, Z 14 bits and two unsigned byte offsets.</summary>
    internal static (uint X, uint Y, uint Z) Pack(Vector3f a, Vector3f b, Vector3f c)
    {
        if (b.Z < a.Z && b.Z <= c.Z) (a, b, c) = (b, c, a);
        else if (c.Z < a.Z && c.Z < b.Z) (a, b, c) = (c, a, b);
        uint Axis(float p, float q, float r, bool height)
        {
            int o = Unit(p), d1 = Unit(q) - o, d2 = Unit(r) - o;
            if (height ? d1 is < 0 or > 255 || d2 is < 0 or > 255 : d1 is < -256 or > 255 || d2 is < -256 or > 255)
                throw new InvalidDataException("A doorway collision triangle spans more than the game's collision can hold.");
            return height ? (uint)o | (uint)d1 << 16 | (uint)d2 << 24 : (uint)o | ((uint)d1 & 511) << 14 | ((uint)d2 & 511) << 23;
        }
        return (Axis(a.X, b.X, c.X, false), Axis(a.Y, b.Y, c.Y, false), Axis(a.Z, b.Z, c.Z, true));
    }

    /// <summary>Halves a triangle until no side spans more than 240 units along an axis (corners on whole units).</summary>
    internal static IEnumerable<(Vector3f A, Vector3f B, Vector3f C)> Pieces(Vector3f a, Vector3f b, Vector3f c, int depth = 0)
    {
        float span = new[] { Spread(a.X, b.X, c.X), Spread(a.Y, b.Y, c.Y), Spread(a.Z, b.Z, c.Z) }.Max();
        if (span <= 240 || depth > 6) { if (Area(a, b, c) != 0) yield return (a, b, c); yield break; }
        Vector3f Mid(Vector3f p, Vector3f q) => new(MathF.Round((p.X + q.X) / 2), MathF.Round((p.Y + q.Y) / 2), MathF.Round((p.Z + q.Z) / 2));
        var ab = Mid(a, b); var bc = Mid(b, c); var ca = Mid(c, a);
        foreach (var t in Pieces(a, ab, ca, depth + 1)) yield return t;
        foreach (var t in Pieces(ab, b, bc, depth + 1)) yield return t;
        foreach (var t in Pieces(ca, bc, c, depth + 1)) yield return t;
        foreach (var t in Pieces(ab, bc, ca, depth + 1)) yield return t;
    }

    private static float Spread(float a, float b, float c) => Math.Max(a, Math.Max(b, c)) - Math.Min(a, Math.Min(b, c));
    private static double Area(Vector3f a, Vector3f b, Vector3f c)
    {
        double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
        return Math.Abs(uy * vz - uz * vy) + Math.Abs(uz * vx - ux * vz) + Math.Abs(ux * vy - uy * vx);
    }

    // ---- reading the level's own terrain ----
    private readonly record struct NearFace((double X, double Y, double Z) At, int Material, ColorRgba Colour);

    /// <summary>The level's near (textured) faces: first corner, material number and that corner's near colour.</summary>
    private static IEnumerable<NearFace> NearFaces(byte[] env)
    {
        foreach (var s in Sectors(env))
        {
            uint w2 = U(s, 8), w3 = U(s, 12);
            int ox = (int)(w2 >> 16), oy = (int)(w2 & 0xFFFF), oz = (int)(w3 >> 16);
            int v = s[20], c = s[21], f = s[22], words = s[23];
            int hpv = 28 + 4 * words, hpc = hpv + 4 * v, hpf = hpc + 8 * c;
            if (hpf + 16 * f > s.Length) continue;
            for (int k = 0; k < f; k++)
            {
                uint corners = U(s, hpf + 16 * k), colours = U(s, hpf + 16 * k + 4);
                int i0 = (int)(corners & 255), c0 = (int)(colours & 255);
                if (i0 >= v || c0 >= c) continue;
                uint p = U(s, hpv + 4 * i0), rgb = U(s, hpc + 4 * c + 4 * c0);
                yield return new((ox + (p >> 21), oy + ((p >> 10) & 0x7FF), oz + (p & 0x3FF)), (int)(U(s, hpf + 16 * k + 8) & 0x7F),
                    new((byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16)));
            }
        }
    }

    /// <summary>The average near colour of the terrain within 384 units of <paramref name="at"/> (its lighting there).</summary>
    private static ColorRgba LocalLight(byte[] env, Vector3f at)
    {
        long r = 0, g = 0, b = 0, n = 0;
        foreach (var f in NearFaces(env))
            if (Math.Abs(f.At.X - at.X) + Math.Abs(f.At.Y - at.Y) + Math.Abs(f.At.Z - at.Z) < 384) { r += f.Colour.R; g += f.Colour.G; b += f.Colour.B; n++; }
        return n == 0 ? new(128, 128, 128) : new((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }

    // ---- readback ----
    private static void Verify(UniversalLevelData before, UniversalLevelData after, Body old, byte[] record, int doorway, int special, int firstEntry,
        int entryTriangles, int frameTriangles, int sectors, int addedSectors, string name)
    {
        void Require(bool ok, string what) { if (!ok) throw new InvalidDataException($"{name}'s New World doorway failed its readback: {what}."); }
        var now = Body.Read(after);
        Require(now.Records.Count == doorway + 1, "doorway count");
        for (int k = 0; k < doorway; k++) Require(now.Records[k].AsSpan().SequenceEqual(old.Records[k]), $"doorway {k + 1} changed");
        byte[] mine = now.Records[doorway];
        Require(mine.Length == record.Length && mine.AsSpan(0x1C).SequenceEqual(record.AsSpan(0x1C)) && I(mine, 0x18) is >= 0 and < 256 && I(mine, 0x1C) == Destination, "the record");
        Require(now.Tail.AsSpan().SequenceEqual(old.Tail), "particles and sounds changed");
        Require(now.Components[0].AsSpan().SequenceEqual(old.Components[0]) && now.Components[5].AsSpan().SequenceEqual(old.Components[5]), "textures or sky changed");
        var oldSectors = Sectors(old.Components[1]); var newSectors = Sectors(now.Components[1]);
        Require(newSectors.Length == sectors + addedSectors && oldSectors.Select((s, i) => s.AsSpan().SequenceEqual(newSectors[i])).All(x => x), "terrain parts");
        var surfaces = PortalSourceDataLocator.ParseTerrainSurfaces(after.Bytes.AsSpan(after.Subfiles()[1].Offset, after.Subfiles()[1].Length).ToArray(),
            after.Subfiles()[1].Offset, "", name);
        Require(surfaces.SpecialSurfaces.Count > special && surfaces.SpecialSurfaces[special] is { Type: 6, Param1: Destination } kind && kind.Param2 == doorway, "the entry's ground kind");
        var oldSurfaces = PortalSourceDataLocator.ParseTerrainSurfaces(before.Bytes.AsSpan(before.Subfiles()[1].Offset, before.Subfiles()[1].Length).ToArray(),
            before.Subfiles()[1].Offset, "", name);
        Require(oldSurfaces.SpecialSurfaces.All(s => surfaces.SpecialSurfaces[s.Index].RawHex == s.RawHex), "the level's own ground kinds changed");
        int oldCount = oldSurfaces.CollisionSurfaceTriangles.Count;
        Require(surfaces.CollisionSurfaceTriangles.Count == oldCount + entryTriangles + frameTriangles, "collision count");
        int Moved(int t) => t < firstEntry ? t : t + entryTriangles;
        static PortalSourcePoint P(PortalSourcePoint p) => p with { WadOffset = 0 };
        for (int t = 0; t < oldCount; t++)
        {
            var a = oldSurfaces.CollisionSurfaceTriangles[t]; var b = surfaces.CollisionSurfaceTriangles[Moved(t)];
            Require(P(a.P1) == P(b.P1) && P(a.P2) == P(b.P2) && P(a.P3) == P(b.P3) && (a.FlagByte & 0x3F) == (b.FlagByte & 0x3F), $"collision triangle {t} changed");
        }
        for (int t = 0; t < entryTriangles; t++)
            Require(surfaces.CollisionSurfaceTriangles[firstEntry + t] is { SurfaceType: 6, Param1: Destination } e && e.Param2 == doorway, "entry triangles");
        for (int t = 0; t < frameTriangles; t++)
            Require(surfaces.CollisionSurfaceTriangles[oldCount + entryTriangles + t].FlagByte == 0xFF, "frame triangles are solid ground");
        var oldIndex = ReadCollision(old.Components[4], name); var newIndex = ReadCollision(now.Components[4], name);
        var o = NativeCollisionIndexCodec.Decode(old.Components[4].AsSpan(oldIndex.Tree, oldIndex.Blocks - oldIndex.Tree), old.Components[4].AsSpan(oldIndex.Blocks, oldIndex.Triangles - oldIndex.Blocks), oldIndex.UsedBlocks, oldIndex.Count, false);
        var n = NativeCollisionIndexCodec.Decode(now.Components[4].AsSpan(newIndex.Tree, newIndex.Blocks - newIndex.Tree), now.Components[4].AsSpan(newIndex.Blocks, newIndex.Triangles - newIndex.Blocks), newIndex.UsedBlocks, newIndex.Count, false);
        var original = new HashSet<int>(Enumerable.Range(0, oldCount).Select(Moved));
        Require(o.Cells.All(p => n.Cells.TryGetValue(p.Key, out var cell) && cell.OrderedTriangleIndexes.Where(original.Contains).SequenceEqual(p.Value.OrderedTriangleIndexes.Select(Moved))),
            "the collision grid lost a triangle");
        Require(newIndex.FlagCount == oldIndex.FlagCount + entryTriangles, "flag count");
        var oldScene = before.Scene(); var newScene = after.Scene();
        Require(newScene.RowCount == oldScene.RowCount + 3, "object rows");
        for (int r = 0; r < oldScene.RowCount; r++)
            Require(before.Row(r).AsSpan(0x0C, 12).SequenceEqual(after.Row(r).AsSpan(0x0C, 12)) && before.Row(r).AsSpan(0x36, 2).SequenceEqual(after.Row(r).AsSpan(0x36, 2)),
                $"object {r} moved or changed kind");
        int[] classes = [0x18E, 1, 0x11E];
        for (int k = 0; k < 3; k++)
            Require(BinaryPrimitives.ReadUInt16LittleEndian(after.Row(oldScene.RowCount + k).AsSpan(0x36)) == classes[k], "the doorway objects' kinds");
        Require(before.Subfiles()[0] == after.Subfiles()[0] &&
                before.Bytes.AsSpan(before.Subfiles()[0].Offset, before.Subfiles()[0].Length).SequenceEqual(after.Bytes.AsSpan(after.Subfiles()[0].Offset, after.Subfiles()[0].Length)),
            "textures in video memory changed");
    }

    private static int Unit(float v)
    {
        if (v != MathF.Truncate(v) || v is < 0 or > 16383) throw new InvalidDataException("There is no room for the doorway there: part of it would be outside the game's map. Move it further in.");
        return (int)v;
    }
    private static int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static uint U(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
    private static short S(byte[] b, int at) => BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at));
    private static void W(byte[] b, int at, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), v);
}
