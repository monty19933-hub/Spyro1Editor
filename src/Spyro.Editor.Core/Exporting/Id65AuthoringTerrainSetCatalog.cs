using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Immutable, closed catalog for the first two independently selectable ID65
/// structural terrain units. This catalog deliberately has no document,
/// persistence, App, disc-image, or publishing surface.
/// </summary>
internal static class Id65AuthoringTerrainSetCatalog
{
    public const string ProfileId = "id65-authoring-terrain-set-v3-two-optional-triangles-v1";
    public const int SchemaVersion = 1;
    public const string Tile1Id = "tile.remote-pad.1";
    public const string Tile2Id = "tile.remote-pad.2";
    public const string Tile1VertexHandle = "vertex.remote-pad.d";
    public const string Tile2VertexHandle = "vertex.remote-pad.e";

    private static readonly Id65TerrainTriangleIntent Tile1Value = new(
        Tile1Id,
        "sector.remote-pad.216",
        "texture.locked.record25",
        new(Tile1VertexHandle, new(496, 496, 512)),
        "vertex.remote-pad.b",
        Tile1VertexHandle,
        "vertex.remote-pad.c",
        "vertex.remote-pad.b",
        "vertex.remote-pad.c",
        Tile1VertexHandle,
        0,
        0);

    private static readonly Id65TerrainTriangleIntent Tile2Value = new(
        Tile2Id,
        "sector.remote-pad.216",
        "texture.locked.record25",
        new(Tile2VertexHandle, new(272, 496, 512)),
        "vertex.remote-pad.a",
        "vertex.remote-pad.c",
        Tile2VertexHandle,
        "vertex.remote-pad.a",
        Tile2VertexHandle,
        "vertex.remote-pad.c",
        0,
        0);

    private static readonly ReadOnlyCollection<Id65TerrainTriangleIntent> AllValue =
        Array.AsReadOnly(new[] { Tile1Value, Tile2Value });

    public static Id65TerrainTriangleIntent Tile1 => Tile1Value;
    public static Id65TerrainTriangleIntent Tile2 => Tile2Value;
    public static IReadOnlyList<Id65TerrainTriangleIntent> All => AllValue;

    public static Id65AuthoringTerrainSetManifest Create(params string[] activeUnitIds) =>
        Create((IEnumerable<string>)activeUnitIds);

    public static Id65AuthoringTerrainSetManifest Create(IEnumerable<string> activeUnitIds)
    {
        ArgumentNullException.ThrowIfNull(activeUnitIds);
        string[] requested = activeUnitIds.ToArray();
        if (requested.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("A terrain-set unit ID is empty.");
        if (requested.Distinct(StringComparer.Ordinal).Count() != requested.Length)
            throw new InvalidDataException("A terrain-set manifest contains a duplicate unit ID.");
        Dictionary<string, Id65TerrainTriangleIntent> catalog = AllValue.ToDictionary(
            item => item.Id,
            StringComparer.Ordinal);
        if (requested.Any(id => !catalog.ContainsKey(id)))
            throw new InvalidDataException("A terrain-set manifest contains an unknown unit ID.");
        Id65TerrainTriangleIntent[] selected = AllValue
            .Where(item => requested.Contains(item.Id, StringComparer.Ordinal))
            .ToArray();
        return new(
            ProfileId,
            Id65AuthoringModelCompiler.CreateMinimalV3EquivalentManifest(),
            selected);
    }

    internal static Id65TerrainTriangleIntent ExactById(string id) => id switch
    {
        Tile1Id => Tile1Value,
        Tile2Id => Tile2Value,
        _ => throw new InvalidDataException($"Unknown terrain-set unit ID {id}.")
    };
}

internal sealed class Id65AuthoringTerrainSetManifest
{
    private readonly ReadOnlyCollection<Id65TerrainTriangleIntent> _activeUnits;

    public Id65AuthoringTerrainSetManifest(
        string profileId,
        Id65AuthoringManifest lockedV3Base,
        IEnumerable<Id65TerrainTriangleIntent> activeUnits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(lockedV3Base);
        ArgumentNullException.ThrowIfNull(activeUnits);
        ProfileId = profileId;
        LockedV3Base = lockedV3Base;
        _activeUnits = Array.AsReadOnly(activeUnits.ToArray());
        CanonicalJson = BuildCanonicalJson();
        CanonicalSha256 = Hash(Encoding.UTF8.GetBytes(CanonicalJson));
    }

    public string ProfileId { get; }
    public Id65AuthoringManifest LockedV3Base { get; }
    public IReadOnlyList<Id65TerrainTriangleIntent> ActiveUnits => _activeUnits;
    public string CanonicalJson { get; }
    public string CanonicalSha256 { get; }

    private string BuildCanonicalJson()
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions { Indented = false }))
        {
            json.WriteStartObject();
            json.WriteString("profileId", ProfileId);
            json.WriteString("lockedV3BaseManifestSha256", LockedV3Base.CanonicalSha256);
            json.WritePropertyName("activeUnits");
            json.WriteStartArray();
            foreach (Id65TerrainTriangleIntent unit in _activeUnits.OrderBy(
                         item => item.Id,
                         StringComparer.Ordinal))
            {
                json.WriteStartObject();
                json.WriteString("id", unit.Id);
                json.WriteString("sectorId", unit.SectorId);
                json.WriteString("textureIntentId", unit.TextureIntentId);
                json.WritePropertyName("addedVertex");
                json.WriteStartObject();
                json.WriteString("handle", unit.AddedVertex.Handle);
                json.WritePropertyName("point");
                json.WriteStartArray();
                json.WriteNumberValue(unit.AddedVertex.Point.X);
                json.WriteNumberValue(unit.AddedVertex.Point.Y);
                json.WriteNumberValue(unit.AddedVertex.Point.Z);
                json.WriteEndArray();
                json.WriteEndObject();
                WriteHandles(json, "render", unit.RenderA, unit.RenderB, unit.RenderC);
                WriteHandles(json, "collision", unit.CollisionA, unit.CollisionB, unit.CollisionC);
                json.WriteNumber("collisionAssignment", unit.CollisionAssignment);
                json.WriteNumber("occlusionGroupIndex", unit.OcclusionGroupIndex);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteHandles(Utf8JsonWriter json, string name, string a, string b, string c)
    {
        json.WritePropertyName(name);
        json.WriteStartArray();
        json.WriteStringValue(a);
        json.WriteStringValue(b);
        json.WriteStringValue(c);
        json.WriteEndArray();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
