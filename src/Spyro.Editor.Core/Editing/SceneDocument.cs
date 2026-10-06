using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Text;

namespace Spyro.Editor.Core.Editing;

public sealed record AuthoredMaterial(int SourceWadEntry, int TextureId);
public sealed record AuthoredSurface(string Id, Vector3f[] Vertices, ColorRgba Color, AuthoredMaterial? Material,
    ColorRgba[]? NearColors = null, ColorRgba[]? FarColors = null);
public sealed record AuthoredObject(string Identity, Moby Value);

/// <summary>The main editor's neutral authored scene. Native ID65 is its actual compilation target,
/// never a cache donor. Geometry and actors originate only in this document.</summary>
public sealed record SceneDocument(int SchemaVersion, string Id, string Name, Vector3f Spawn, int Heading,
    ColorRgba SkyColor, int MusicTrack, IReadOnlyList<AuthoredSurface> Surfaces,
    IReadOnlyList<AuthoredObject> Objects, UniversalPlacementLedger Ledger)
{
    public const string FileName = "fresh-level.scene.json";
    public SceneTargetBinding? TargetBinding { get; init; }
    /// <summary>WAD entry of the retail level whose sky this scene borrows; null keeps the plain sky color.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? SkySourceWadEntry { get; init; }
    public LevelDefinition ResolveTarget(LevelCatalog catalog) => TargetBinding == null
        ? NativeTarget(Name) : TargetBinding.Resolve(catalog) with { DisplayName = Name, SourceRecordCount = 0 };
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
            if (p.HasSurfaceBehaviorEdit)
                throw new InvalidDataException("This authored compiler has not yet verified native hazard/trigger surface records. Undo that surface behavior before saving; geometry and collision are supported.");
            if (p.IsTerrainAddClone && old != null)
                surfaces.Add((OriginalSurfaces is { } originals && p.SectorIndex < originals.Count ? originals[p.SectorIndex] : old) with { Vertices = p.OriginalPoints.Select((v,i) => new Vector3f(v.X,v.Y,p.OriginalZValues[i])).ToArray() });
            string identity = old?.Id ?? Guid.NewGuid().ToString("N");
            if (p.IsTerrainAddClone) identity = new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Id + ":copy:" + identity)).AsSpan(0,16)).ToString("N");
            surfaces.Add(new(identity,
                p.Points.Select((v,i) => new Vector3f(v.X,v.Y,p.ZValues[i])).ToArray(), p.TextureVisualEdit?.AverageNearColor ?? p.FaceColor, material,
                Enumerable.Range(0,p.Points.Count).Select(i => { int slot=Enumerable.Range(0,p.CornerPointIndexes.Count).FirstOrDefault(k=>p.CornerPointIndexes[k]==i); return p.TextureVisualEdit?.Corners.ElementAtOrDefault(slot)?.NearColor ?? p.NearColors.ElementAtOrDefault(slot); }).ToArray(),
                Enumerable.Range(0,p.Points.Count).Select(i => { int slot=Enumerable.Range(0,p.CornerPointIndexes.Count).FirstOrDefault(k=>p.CornerPointIndexes[k]==i); return p.TextureVisualEdit?.Corners.ElementAtOrDefault(slot)?.FarColor ?? p.FarColors.ElementAtOrDefault(slot); }).ToArray()));
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

    public void Validate()
    {
        if (SchemaVersion != 1 || !Guid.TryParseExact(Id,"N",out _) || string.IsNullOrWhiteSpace(Name) ||
            Surfaces.Count > 255 || Objects.Count > 256 || Heading is < 0 or > 255 ||
            !TextTargetCatalog.IsSafeReplacement(Name,19))
            throw new InvalidDataException("Fresh scene document identity, surfaces or settings are invalid.");
        Ledger.Validate();
        TargetBinding?.Validate();
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
        }
        RequireCoordinate(Spawn);
        foreach (var o in Objects) if (!Guid.TryParseExact(o.Identity,"N",out _)) throw new InvalidDataException("An authored object has no stable identity.");
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
