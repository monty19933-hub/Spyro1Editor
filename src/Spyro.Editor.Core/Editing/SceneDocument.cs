using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Text;

namespace Spyro.Editor.Core.Editing;

/// <summary>A retail terrain texture (WAD entry, texture id) copied into the built level. With <see cref="Image"/>
/// (a PNG file, absolute path like the editor's custom terrain textures) the copy carries that picture instead,
/// written in every detail tier; the retail texture then only lends its layout and distance fade.</summary>
public sealed record AuthoredMaterial(int SourceWadEntry, int TextureId)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Image { get; init; }
}
public sealed record AuthoredSurface(string Id, Vector3f[] Vertices, ColorRgba Color, AuthoredMaterial? Material,
    ColorRgba[]? NearColors = null, ColorRgba[]? FarColors = null)
{
    /// <summary>What Spyro finds on this face (water, lava, supercharge…); null is solid ground.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public AuthoredSurfaceKind? Kind { get; init; }
    /// <summary>Faces sharing a number are drawn as one sector, as in the level they were imported from (null: the
    /// compiler groups the face itself).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? Sector { get; init; }
    /// <summary>Drawn from both sides (thin walls, sails, planks). One-sided faces show only their front, the side
    /// their corners turn anticlockwise from.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool TwoSided { get; init; }
    /// <summary>The face shows its other side: the front is the side its corners turn clockwise from (as Year of the
    /// Dragon's faces with its flip bit). Compiled as the native face flag 0x02, with the far copy and collision
    /// reversed to match, so the texture keeps its orientation.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool FrontReversed { get; init; }
    /// <summary>Drawn only: Spyro passes through it (scenery out of his reach, such as an imported level's backdrop beyond
    /// its invisible walls). Saves the level's collision triangles and data. Textured faces only (an untextured face's
    /// drawing comes from its collision pieces, so it always keeps them).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool NoCollision { get; init; }
}

/// <summary>A sector's own distant (low-detail) mesh, drawn in place of the compiler's simplified copy when the sector's
/// faces need one (more than 64 corners or colours): untextured faces over <paramref name="Vertices"/> in
/// <paramref name="Colors"/>, as the level it was imported from draws that sector far away.</summary>
public sealed record AuthoredFarMesh(int Sector, Vector3f[] Vertices, ColorRgba[] Colors, IReadOnlyList<AuthoredFarFace> Faces)
{
    /// <summary>Drawn far away even when the sector's near tables could serve as its distant mesh, and the sector's faces
    /// keep the far colours they were given (an import whose faces' far colours equal their near colours: the face pass
    /// modulates a textured face drawn whole near the camera with its first near-tier colour table, which the compiler
    /// fills from <see cref="AuthoredSurface.FarColors"/>; USA 0x80026DB8..0x80026FB4). Without it a sector small enough
    /// draws its near faces untextured in their far colours, and far colours equal to the near ones are replaced by the
    /// texture's average times them.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Always { get; init; }
    /// <summary>Every distant face is drawn from both sides (LP face flag 0x80), as the sector's two-sided near faces are.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool TwoSided { get; init; }
}
/// <summary>A distant face: 3 or 4 corners (indices into the mesh's vertices) in perimeter order, anticlockwise seen
/// from the side it shows, and each corner's colour (indices into the mesh's colours).</summary>
public sealed record AuthoredFarFace(int[] Corners, int[] Colors);

/// <summary>A native special-surface record (type and two parameters, as retail levels store them): type 0 is a
/// hazard floor (Param1 0 hurts, 1 deadly; Param2 0 water, 1 lava, 2 ooze), 4 a supercharge ramp, 5 an invisible
/// wall. These kinds are handled by the game's executable, so any level can use them.</summary>
public sealed record AuthoredSurfaceKind(int Type, int Param1, int Param2)
{
    public static readonly AuthoredSurfaceKind Water = new(0, 0, 0), DeadlyWater = new(0, 1, 0), Lava = new(0, 0, 1),
        DeadlyLava = new(0, 1, 1), Ooze = new(0, 0, 2), DeadlyOoze = new(0, 1, 2), Supercharge = new(4, 0, 0), InvisibleWall = new(5, 0, 0);
    public static IReadOnlyList<AuthoredSurfaceKind> All { get; } = [Water, DeadlyWater, Lava, DeadlyLava, Ooze, DeadlyOoze, Supercharge, InvisibleWall];
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSupported => All.Contains(this);
    /// <summary>A homeworld doorway's entry (type 6): the destination level and the doorway's index in the level's
    /// portal table. Built from <see cref="SceneDocument.Portals"/>, never chosen as a tile type.</summary>
    public static AuthoredSurfaceKind Portal(int destinationLevelId, int portalIndex) => new(6, destinationLevelId, portalIndex);
    /// <summary>The native record: type word, then for hazard floors and doorways the two parameter words.</summary>
    public byte[] NativeRecord()
    {
        byte[] r = new byte[Type is 0 or 6 ? 12 : 4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(r, Type);
        if (Type is 0 or 6) { System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(4), Param1); System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(8), Param2); }
        return r;
    }
}
public sealed record AuthoredObject(string Identity, Moby Value);

/// <summary>A face Spyro collides with that is not drawn: an invisible wall or floor (3 or 4 corners, editor units), with
/// an optional ground kind (a deadly floor at the bottom of a pit). Solid like a drawn face's collision: a floor from above,
/// a wall from the side its corners turn anticlockwise from (NativeAuthoredMesh.CollisionWinding). Compiled into the level's
/// collision only (split like a textured face's collision, at most 255 units per piece and axis; keep a wall's pieces
/// within that so they stay exactly upright).</summary>
public sealed record AuthoredCollisionFace(Vector3f[] Vertices,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] AuthoredSurfaceKind? Kind = null);

/// <summary>A doorway of a rebuilt homeworld: the original homeworld's portal to <see cref="DestinationLevelId"/>
/// (its walk-in path, name lettering, return point and the destination's sky seen through it), moved so the bottom
/// middle of its opening stands at <see cref="Position"/> and Spyro walks in towards <see cref="Heading"/>
/// (0-255, 0 = +X, 64 = +Y).</summary>
public sealed record AuthoredPortal(int DestinationLevelId, Vector3f Position, int Heading);

/// <summary>The main editor's neutral authored scene. Native ID65 is its actual compilation target,
/// never a cache donor. Geometry and actors originate only in this document.</summary>
public sealed record SceneDocument(int SchemaVersion, string Id, string Name, Vector3f Spawn, int Heading,
    ColorRgba SkyColor, int MusicTrack, IReadOnlyList<AuthoredSurface> Surfaces,
    IReadOnlyList<AuthoredObject> Objects, UniversalPlacementLedger Ledger)
{
    public const string FileName = "fresh-level.scene.json";
    public SceneTargetBinding? TargetBinding { get; init; }
    /// <summary>A level of the project's new world (70 its homeworld, 71-74 its levels; see <see cref="NewWorldDocument"/>);
    /// null: the level in a retail slot (<see cref="TargetBinding"/>) or File ▾ › New Level's.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? WorldLevelId { get; init; }
    /// <summary>The key the scene is saved and listed under: the retail level it replaces, worldNN, or authored65.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string TargetKey => TargetBinding?.LevelKey ?? (WorldLevelId is int world ? NewWorldDocument.KeyFor(world) : "authored65");
    /// <summary>A rebuilt homeworld's doorways (null: none).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AuthoredPortal>? Portals { get; init; }
    /// <summary>WAD entry of the retail level whose sky this scene borrows; null keeps the plain sky color.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? SkySourceWadEntry { get; init; }
    /// <summary>A sky of the level's own (a native sky block, made by Import Sky from a panorama or sky model); used in
    /// place of <see cref="SkySourceWadEntry"/>. Null: none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? CustomSky { get; init; }
    /// <summary>Distant meshes of imported sectors (by <see cref="AuthoredSurface.Sector"/>); null: every sector that
    /// needs one gets the compiler's simplified copy.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AuthoredFarMesh>? FarMeshes { get; init; }
    /// <summary>Where the level's own game kept its gems (a level imported from another game), for Fill In What the Level
    /// Needs (<see cref="LevelFill"/>); null: gems are spread along the walkable floor.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Vector3f>? GemSpots { get; init; }
    /// <summary>Faces Spyro collides with that are not drawn (invisible walls, deadly pit floors); null: none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AuthoredCollisionFace>? CollisionFaces { get; init; }
    /// <summary>The ground kinds the level's drawn and collision-only faces use (null: solid ground).</summary>
    public IEnumerable<AuthoredSurfaceKind?> GroundKinds() => Surfaces.Select(s => s.Kind).Concat(CollisionFaces?.Select(c => c.Kind) ?? []);
    public LevelDefinition ResolveTarget(LevelCatalog catalog) => TargetBinding == null
        ? WorldLevelId is int world ? NewWorldDocument.Target(world, Name) : NativeTarget(Name)
        : TargetBinding.Resolve(catalog) with { DisplayName = Name, SourceRecordCount = 0 };
    public static string PathForTarget(string workspace, string key) => SceneDocumentStore.PathForTarget(workspace, key);
    public SceneDocument StartBlank() => New(Name) with { TargetBinding = TargetBinding, Ledger = Ledger };

    public static LevelDefinition NativeTarget(string name) => new()
    {
        Key = "authored65", ScriptKey = "Authored65", DisplayName = name,
        LevelId = 65, SourceWadEntry = 80, SourceRecordCount = 0,
        Confidence = "authored neutral document; native ID65 replacement"
    };

    public static SceneDocument New(string name = "Untitled Level")
    {
        // The native portal arrival moves before Spyro settles onto the floor.
        // Only new scenes use this larger starter; saved terrain is read verbatim.
        Vector3f a = new(368, 368, 544), b = new(752, 368, 544), c = new(368, 752, 544), d = new(752, 752, 544);
        return new(1, Guid.NewGuid().ToString("N"), name, new(560, 560, 576), 64,
            new(80, 128, 112), 19,
            [new(Guid.NewGuid().ToString("N"), [a,b,c], new(128,96,64), null),
             new(Guid.NewGuid().ToString("N"), [b,d,c], new(128,96,64), null)], [], UniversalPlacementLedger.Empty);
    }

    public GeometryCandidate ProjectGeometry()
    {
        Validate();
        GeometryCandidate geometry = new() { Name = Name, MinZ = Surfaces.Count == 0 ? Spawn.Z : Surfaces.Min(s => s.Vertices.Min(v => v.Z)), MaxZ = Surfaces.Count == 0 ? Spawn.Z : Surfaces.Max(s => s.Vertices.Max(v => v.Z)) };
        for (int i = 0; i < Surfaces.Count; i++)
        {
            var s = Surfaces[i];
            int[] slots=s.Vertices.Length==3?[0,0,1,2]:[0,1,2,3];
            uint slotWord=(uint)(slots[0]|slots[1]<<8|slots[2]<<16|slots[3]<<24);
            geometry.Polygons.Add(new(s.Vertices.Select(v => new Vector2f(v.X,v.Y)).ToArray(), s.Vertices.Select(v => v.Z).ToArray(),
                s.Material?.TextureId ?? -1, i, 0, "hp", s.Color,
                vertexIndexes:slots,colourIndexes:slots,cornerPointIndexes:slots,
                word3:(s.Material?.TextureId??255).ToString("X8"),word4:"00000080",faceDepth:16,
                nearColors:slots.Select(p=>s.NearColors?[p]??s.Color).ToArray(),farColors:slots.Select(p=>s.FarColors?[p]??s.Color).ToArray(),
                nativeFaceWord0:slotWord,nativeFaceWord1:slotWord,nativeFaceWord2:(uint)(s.Material?.TextureId??255),nativeFaceWord3:0x80,
                nativeMaterialByte:s.Material?.TextureId??255,nativeUntexturedSentinel:s.Material==null,
                nativePrimitiveSemiTransparent:false,nativeTextureId:s.Material?.TextureId??-1));
            // A face's ground kind shows in the editor as a staged tile type and comes back through Capture.
            if (s.Kind is { } kind)
                geometry.Polygons[^1].ApplySurfaceBehaviorEdit(new(kind.Type, kind.Param1, kind.Param2, "built", s.Id,
                    new Spyro.Editor.Core.Analysis.NativeTerrainSurfaceSignature(kind.Type, kind.Param1, kind.Param2).Label));
        }
        geometry.Bounds = Surfaces.Count == 0 ? Rect2f.FromBounds(Spawn.X-64, Spawn.Y-64, Spawn.X+64, Spawn.Y+64) : Rect2f.FromBounds(Surfaces.Min(s => s.Vertices.Min(v => v.X)), Surfaces.Min(s => s.Vertices.Min(v => v.Y)),
            Surfaces.Max(s => s.Vertices.Max(v => v.X)), Surfaces.Max(s => s.Vertices.Max(v => v.Y)));
        return geometry;
    }

    public List<Moby> ProjectObjects() => Objects.Select(o => { o.Value.UniversalPlacementId = o.Identity; return o.Value; }).ToList();

    public SceneDocument Capture(GeometryCandidate geometry, IReadOnlyList<Moby> mobys)
    {
        List<AuthoredSurface> surfaces = [];
        foreach (var p in geometry.Polygons.Where(p => !p.IsTerrainRemoved))
        {
            var old = p.SectorIndex >= 0 && p.SectorIndex < Surfaces.Count ? Surfaces[p.SectorIndex] : null;
            AuthoredMaterial? material = old?.Material;
            if (p.HasTextureVisualEdit && p.TextureVisualEdit is { } visual)
                material = new(LevelSourceEntry(visual.SourceLevelKey), p.TextureId);
            else if (p.TextureId >= 0 && p.TextureId != old?.Material?.TextureId)
                material = material == null ? throw new InvalidDataException("Choose a terrain material with an explicit source level before saving this face.") : material with { TextureId = p.TextureId };
            AuthoredSurfaceKind? kind = p.SurfaceBehaviorEdit is { } behavior ? new(behavior.SurfaceType, behavior.Param1, behavior.Param2) : null;
            if (kind is { IsSupported: false })
                throw new InvalidDataException("This ground kind cannot be used in a level you build. Choose water, lava, ooze, a supercharge ramp, an invisible wall or solid ground.");
            if (p.IsTerrainAddClone && old != null)
                surfaces.Add((OriginalSurfaces is { } originals && p.SectorIndex < originals.Count ? originals[p.SectorIndex] : old) with { Vertices = p.OriginalPoints.Select((v,i) => new Vector3f(v.X,v.Y,p.OriginalZValues[i])).ToArray() });
            string identity = old?.Id ?? Guid.NewGuid().ToString("N");
            if (p.IsTerrainAddClone) identity = new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Id + ":copy:" + identity)).AsSpan(0,16)).ToString("N");
            surfaces.Add(new(identity,
                p.Points.Select((v,i) => new Vector3f(v.X,v.Y,p.ZValues[i])).ToArray(), p.TextureVisualEdit?.AverageNearColor ?? p.FaceColor, material,
                Enumerable.Range(0,p.Points.Count).Select(i => { int slot=Enumerable.Range(0,p.CornerPointIndexes.Count).FirstOrDefault(k=>p.CornerPointIndexes[k]==i); return p.TextureVisualEdit?.Corners.ElementAtOrDefault(slot)?.NearColor ?? p.NearColors.ElementAtOrDefault(slot); }).ToArray(),
                Enumerable.Range(0,p.Points.Count).Select(i => { int slot=Enumerable.Range(0,p.CornerPointIndexes.Count).FirstOrDefault(k=>p.CornerPointIndexes[k]==i); return p.TextureVisualEdit?.Corners.ElementAtOrDefault(slot)?.FarColor ?? p.FarColors.ElementAtOrDefault(slot); }).ToArray()) { Kind = kind, Sector = old?.Sector, TwoSided = old?.TwoSided ?? false, FrontReversed = old?.FrontReversed ?? false, NoCollision = old?.NoCollision ?? false });
        }
        var ledger = ReconcileLedger(mobys);
        var captured = this with { Surfaces = surfaces, Objects = mobys.Select(m => new AuthoredObject(m.UniversalPlacementId,m)).ToArray(), Ledger = ledger };
        captured.Validate();
        return captured;
    }

    // Exact source descriptors are injected by the editor; there is no guessed retail default.
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<AuthoredSurface>? OriginalSurfaces { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<string,int> LevelSourceEntry { get; init; } = _ => throw new InvalidDataException("No terrain material source resolver is bound.");

    /// <summary>The scene with the save rows of deleted objects given back: deleted objects are dropped and the
    /// remaining ones renumbered in their existing order. A card that played the level with the old numbering can
    /// then show some of the new objects as already collected, so this is for a new game.</summary>
    public (SceneDocument Scene, int Freed) FreeRetiredSaveRows()
    {
        var live = Objects.Where(o => !o.Value.IsRemoved).ToArray();
        var order = Ledger.Entries.Select((e, i) => (e.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
        var ids = live.Select(o => o.Value.UniversalPlacementId).Distinct(StringComparer.Ordinal)
            .OrderBy(id => order.TryGetValue(id, out int slot) ? slot : int.MaxValue).ToArray();
        var ledger = new UniversalPlacementLedger(1, ids.Select((id, i) => new UniversalPlacementIdentity(id, i)).ToArray());
        ledger.Validate();
        var scene = this with { Objects = live, Ledger = ledger };
        scene.Validate();
        return (scene, Ledger.Entries.Count - ledger.Entries.Count);
    }

    public UniversalPlacementLedger ReconcileLedger(IReadOnlyList<Moby> mobys)
    {
        List<UniversalPlacementIdentity> rows = [.. Ledger.Entries];
        foreach (var m in mobys)
        {
            if (!m.IsAdded || m.CrossLevelRequiredExporterFeature != UniversalMobyExport.RequiredExporterFeature)
                throw new InvalidDataException("Fresh scene objects must carry an exact game-wide source template.");
            if (!rows.Any(r => r.Id == m.UniversalPlacementId)) rows.Add(new(m.UniversalPlacementId, rows.Count));
        }
        var ledger = new UniversalPlacementLedger(1, rows); ledger.Validate(); return ledger;
    }

    /// <summary>Authored faces in one scene (the compiled level holds at most 5,000 drawn faces).</summary>
    /// <summary>The game keeps one cache byte per drawn face in a 0x1C00-byte table (index 0 unused); its own
    /// levels use up to 7,167 faces (Dark Passage).</summary>
    public const int MaxSurfaces = 7167;
    public void Validate()
    {
        if (SchemaVersion != 1 || !Guid.TryParseExact(Id,"N",out _) || string.IsNullOrWhiteSpace(Name) ||
            Surfaces.Count > MaxSurfaces || Objects.Count > 256 || Heading is < 0 or > 255 ||
            !TextTargetCatalog.IsSafeReplacement(Name,19))
            throw new InvalidDataException("Fresh scene document identity, surfaces or settings are invalid.");
        Ledger.Validate();
        TargetBinding?.Validate();
        if (WorldLevelId is int world && (TargetBinding != null || !NewWorldDocument.IsWorldKey(NewWorldDocument.KeyFor(world), out _)))
            throw new InvalidDataException("A new world level is levels 70-74 and replaces no retail level.");
        if (SkySourceWadEntry is int sky && (sky is < 10 or > 78 || sky % 2 != 0))
            throw new InvalidDataException("The scene's sky source is not a retail level.");
        if ((TargetBinding?.NativePrefixRows ?? 0) + Ledger.Entries.Count > 256)
            throw new InvalidDataException("The cleared level's previous collectible identities fill the native save rows. Restore before clear or use a new project.");
        if (Surfaces.Select(s => s.Id).Distinct().Count() != Surfaces.Count || Objects.Select(o => o.Identity).Distinct().Count() != Objects.Count)
            throw new InvalidDataException("Fresh surfaces and objects require unique identities.");
        foreach (var s in Surfaces)
        {
            if (!Guid.TryParseExact(s.Id,"N",out _) || s.Vertices.Length is < 3 or > 4 || s.Color.A != 255)
                throw new InvalidDataException("An authored face has an invalid identity, vertex count or color.");
            foreach (var colors in new[] {s.NearColors,s.FarColors})
                if(colors != null && (colors.Length!=s.Vertices.Length || colors.Any(c=>c.A!=255)))
                    throw new InvalidDataException("Authored Gouraud color lanes must match their opaque vertices.");
            foreach (var v in s.Vertices) RequireCoordinate(v);
            if (s.Kind is { IsSupported: false })
                throw new InvalidDataException("An authored face has a ground kind the editor does not support.");
        }
        if (CustomSky is { } custom && (!Spyro.Editor.Core.Exporting.Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(custom) ||
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(custom.AsSpan(8)) > 254))
            throw new InvalidDataException("The level's own sky is not a native sky block the game can draw.");
        RequireCoordinate(Spawn);
        foreach (var o in Objects) if (!Guid.TryParseExact(o.Identity,"N",out _)) throw new InvalidDataException("An authored object has no stable identity.");
        if (FarMeshes is { } farMeshes)
        {
            if (farMeshes.Select(m => m.Sector).Distinct().Count() != farMeshes.Count)
                throw new InvalidDataException("A sector has more than one distant mesh.");
            foreach (var m in farMeshes)
            {
                if (m.Vertices.Length == 0 || m.Colors.Length == 0 || m.Faces.Count == 0 || m.Colors.Any(c => c.A != 255))
                    throw new InvalidDataException("A distant mesh has no faces or an invalid colour.");
                foreach (var v in m.Vertices) RequireCoordinate(v);
                foreach (var f in m.Faces)
                    if (f.Corners.Length is < 3 or > 4 || f.Colors.Length != f.Corners.Length || f.Corners.Distinct().Count() != f.Corners.Length ||
                        f.Corners.Any(i => i < 0 || i >= m.Vertices.Length) || f.Colors.Any(i => i < 0 || i >= m.Colors.Length))
                        throw new InvalidDataException("A distant face has invalid corners or colours.");
            }
        }
        if (CollisionFaces is { } collisionFaces)
            foreach (var c in collisionFaces)
            {
                if (c.Vertices is not { Length: 3 or 4 } || c.Kind is { IsSupported: false })
                    throw new InvalidDataException("A collision-only face has an invalid vertex count or ground kind.");
                foreach (var v in c.Vertices) RequireCoordinate(v);
            }
        if (Portals is { } portals)
        {
            if (TargetBinding is not { LevelId: var home } || home % 10 != 0 ||
                portals.Select(p => p.DestinationLevelId).Distinct().Count() != portals.Count ||
                portals.Any(p => p.Heading is < 0 or > 255 || HomeworldPortalControlCatalog.All.All(d => d.DestinationLevelId != p.DestinationLevelId || d.HomeworldKey != TargetBinding.LevelKey)))
                throw new InvalidDataException("Doorways belong to a rebuilt homeworld, one per level that homeworld's original portals lead to.");
            foreach (var p in portals) RequireCoordinate(p.Position);
        }
    }
    private static void RequireCoordinate(Vector3f v)
    {
        if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z) || v.X < 0 || v.Y < 0 || v.Z < 0 || v.X > 16383 || v.Y > 16383 || v.Z > 16383)
            throw new InvalidDataException("Authored coordinates are outside the native level coordinate domain.");
    }
    public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(this, new JsonSerializerOptions { WriteIndented = true });
    public string Fingerprint() => Convert.ToHexStringLower(SHA256.HashData(Serialize()));
    public static SceneDocument Read(string path)
    {
        var d = JsonSerializer.Deserialize<SceneDocument>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("Empty fresh scene."); d.Validate(); return d;
    }
    public async Task SaveAsync(string path)
    {
        Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
        try { await File.WriteAllBytesAsync(temporary,Serialize()); File.Move(temporary,path,true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
