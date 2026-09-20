using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Strict neutral whole-level schemas1..5. No filesystem, implicit migration, native payload,
/// compiler or publisher API. The embedded floor uses the unchanged v1 codec;
/// its legacy format and its independent16KiB parser bound remain intact.
/// </summary>
public static partial class NativeFreshLevelDocumentCodec
{
    public const int MaximumByteLength = 64 * 1024;
    private const string Kind = "spyro-native-fresh-level";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static NativeFreshLevelDocumentIntent Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length is < 1 or > MaximumByteLength)
            throw new InvalidDataException("Fresh level JSON must contain1..65536 UTF8 bytes.");
        if (utf8.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
            throw new InvalidDataException("Fresh level JSON does not accept a BOM.");
        try
        {
            _ = StrictUtf8.GetCharCount(utf8);
            using var json = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions
            { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 10 });
            JsonElement rootElement = json.RootElement;
            if (rootElement.ValueKind != JsonValueKind.Object || !rootElement.TryGetProperty("schema", out var schema))
                throw new InvalidDataException("Fresh level requires an explicit schema.");
            int version = Integer(schema);
            var root = version switch
            {
                1 => Object(rootElement, "kind", "schema", "base", "placements"),
                2 or 3 => Object(rootElement, "kind", "schema", "base", "placements", "floorMaterials"),
                4 => Object(rootElement, "kind", "schema", "surface", "spawn", "skyRgb", "musicTrackId", "placements"),
                5 => Object(rootElement, "kind", "schema", "content", "placements"),
                _ => throw new InvalidDataException("Not a supported distinct fresh-level schema.")
            };
            if (String(root["kind"]) != Kind)
                throw new InvalidDataException("Not the supported distinct fresh-level kind.");
            JsonElement rows = root["placements"];
            if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > NativeFreshLevelDocumentIntent.MaximumPlacements)
                throw new InvalidDataException("Fresh level placements require an array with at most128 authoring rows.");
            var @base = version is 4 or 5 ? null : NativeId65FreshDocumentCodec.Parse(StrictUtf8.GetBytes(root["base"].GetRawText()));
            var placements = new List<NativeFreshPlacementIntent>(rows.GetArrayLength());
            foreach (var row in rows.EnumerateArray())
                placements.Add(ParsePlacement(row, version));
            if (version == 5)
                return ParseFamilyContent(root["content"], placements);
            if (version == 4)
                return NativeFreshLevelDocumentIntent.CreateTiled(ParseTiledContent(root), placements);
            if (version == 3)
            {
                var modern = NativeFreshLevelDocumentIntent.Create(@base!, []).UpgradeForNativeMobys().WithContent(@base!, placements);
                return root["floorMaterials"].ValueKind == JsonValueKind.Null
                    ? modern : modern.WithFloorMaterials(ParseMaterials(root["floorMaterials"]));
            }
            var intent = NativeFreshLevelDocumentIntent.Create(@base!, placements);
            return version == 1 ? intent : intent.WithFloorMaterials(ParseMaterials(root["floorMaterials"]));
        }
        catch (DecoderFallbackException error)
        { throw new InvalidDataException("Fresh level contains invalid UTF8.", error); }
        catch (JsonException error)
        { throw new InvalidDataException("Fresh level contains invalid or over-depth JSON.", error); }
    }

    public static byte[] SerializeCanonical(NativeFreshLevelDocumentIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        using var memory = new MemoryStream();
        using (var json = new Utf8JsonWriter(memory))
        {
            json.WriteStartObject(); json.WriteString("kind", Kind); json.WriteNumber("schema", intent.SchemaVersion);
            if (intent.SchemaVersion == 5)
                WriteFamilyContent(json, intent);
            else if (intent.TiledContent is { } tiled)
                WriteTiledContent(json, tiled);
            else
            {
                json.WritePropertyName("base");
                json.WriteRawValue(NativeId65FreshDocumentCodec.SerializeCanonical(intent.Base));
            }
            json.WriteStartArray("placements");
            foreach (var placement in intent.Placements)
                WritePlacement(json, placement, intent.SchemaVersion);
            json.WriteEndArray();
            if (intent.SchemaVersion != 5 && intent.FloorMaterials is { } materials)
            {
                json.WritePropertyName("floorMaterials"); WriteMaterials(json, materials);
            }
            else if (intent.SchemaVersion == 3) json.WriteNull("floorMaterials");
            json.WriteEndObject();
        }
        if (memory.Length > MaximumByteLength)
            throw new InvalidDataException("Canonical fresh level exceeds the independent authoring byte budget.");
        return memory.ToArray();
    }

    private static NativeFreshPlacementIntent ParsePlacement(JsonElement row, int version)
    {
        if (version >= 3 && row.ValueKind == JsonValueKind.Object && row.TryGetProperty("tag", out var tag))
        {
            if (version == 5 && String(tag) == "locked-chest-family")
                return ParseFamilyPlacement(row);
            if (String(tag) == "native-moby")
            {
                var native = Object(row, "id", "tag", "source", "position", "nativeYawByte");
                var source = Object(native["source"], "sourceVariant", "sourceWadEntry", "sourceTrueIndex", "expectedNativeClass");
                var position = Object(native["position"], "x", "y", "z");
                return NativeFreshMobyPlacementIntent.Create(String(native["id"]),
                    NativeFreshRetailMobyReference.Create(String(source["sourceVariant"]), Integer(source["sourceWadEntry"]),
                        Integer(source["sourceTrueIndex"]), Integer(source["expectedNativeClass"])),
                    Integer(position["x"]), Integer(position["y"]), Integer(position["z"]), Integer(native["nativeYawByte"]));
            }
        }
        var placement = Object(row, "id", "tag", "origin", "routeEnd");
        if (String(placement["tag"]) != "wizard-route")
            throw new InvalidDataException("Unknown fresh-level placement tag; no opaque payload is retained or discarded.");
        var origin = Object(placement["origin"], "x", "y", "z");
        var end = Object(placement["routeEnd"], "x", "y");
        return NativeFreshWizardRouteIntent.Create(String(placement["id"]),
            Integer(origin["x"]), Integer(origin["y"]), Integer(origin["z"]), Integer(end["x"]), Integer(end["y"]));
    }

    private static void WritePlacement(Utf8JsonWriter json, NativeFreshPlacementIntent placement, int version)
    {
        if (version == 5 && placement is NativeFreshLockedChestFamilyPlacementIntent family)
        {
            WriteFamilyPlacement(json, family);
            return;
        }
        if (version >= 3 && placement is NativeFreshMobyPlacementIntent moby)
        {
            json.WriteStartObject(); json.WriteString("id", moby.EditorId); json.WriteString("tag", "native-moby");
            json.WriteStartObject("source");
            json.WriteString("sourceVariant", moby.Source.SourceVariant);
            json.WriteNumber("sourceWadEntry", moby.Source.SourceWadEntry);
            json.WriteNumber("sourceTrueIndex", moby.Source.SourceTrueIndex);
            json.WriteNumber("expectedNativeClass", moby.Source.ExpectedNativeClass);
            json.WriteEndObject(); json.WriteStartObject("position");
            json.WriteNumber("x", moby.X); json.WriteNumber("y", moby.Y); json.WriteNumber("z", moby.Z);
            json.WriteEndObject(); json.WriteNumber("nativeYawByte", moby.NativeYawByte); json.WriteEndObject();
            return;
        }
        if (placement is not NativeFreshWizardRouteIntent wizard)
            throw new InvalidDataException("Unsupported fresh-level placement variant.");
        json.WriteStartObject(); json.WriteString("id", wizard.EditorId); json.WriteString("tag", "wizard-route");
        json.WriteStartObject("origin");
        json.WriteNumber("x", wizard.OriginX); json.WriteNumber("y", wizard.OriginY); json.WriteNumber("z", wizard.OriginZ);
        json.WriteEndObject(); json.WriteStartObject("routeEnd");
        json.WriteNumber("x", wizard.EndX); json.WriteNumber("y", wizard.EndY);
        json.WriteEndObject(); json.WriteEndObject();
    }

    private static NativeFreshTiledLevelContent ParseTiledContent(Dictionary<string, JsonElement> root)
    {
        var surface = Object(root["surface"], "layout", "tiles");
        if (String(surface["layout"]) != "ordered-flat-tiles-v1")
            throw new InvalidDataException("Unsupported tiled surface layout.");
        return ParseTiledContent(surface["tiles"], root);
    }

    private static NativeFreshTiledLevelContent ParseTiledContent(JsonElement rows, Dictionary<string, JsonElement> settings)
    {
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() is < 1 or > NativeFreshTiledLevelContent.MaximumTiles)
            throw new InvalidDataException("Tiled surface requires1..255 ordered authoring tiles.");
        List<NativeFreshFlatTileIntent> tiles = [];
        foreach (var row in rows.EnumerateArray())
        {
            var tile = Object(row, "id", "x", "y", "z", "width", "depth", "floorRgb", "materials");
            tiles.Add(NativeFreshFlatTileIntent.Create(String(tile["id"]), Integer(tile["x"]), Integer(tile["y"]),
                Integer(tile["z"]), Integer(tile["width"]), Integer(tile["depth"]), Color(tile["floorRgb"]),
                ParseMaterials(tile["materials"])));
        }
        var spawn = Object(settings["spawn"], "x", "y", "z", "heading");
        return NativeFreshTiledLevelContent.Create(tiles, Integer(spawn["x"]), Integer(spawn["y"]),
            Integer(spawn["z"]), Integer(spawn["heading"]), Color(settings["skyRgb"]), Integer(settings["musicTrackId"]));
    }

    private static void WriteTiledContent(Utf8JsonWriter json, NativeFreshTiledLevelContent tiled)
    {
        json.WriteStartObject("surface"); json.WriteString("layout", "ordered-flat-tiles-v1");
        WriteTiles(json, tiled);
        json.WriteEndObject();
        WriteTiledSettings(json, tiled);
    }

    private static void WriteTiles(Utf8JsonWriter json, NativeFreshTiledLevelContent tiled)
    {
        json.WriteStartArray("tiles");
        foreach (var tile in tiled.Tiles)
        {
            json.WriteStartObject(); json.WriteString("id", tile.EditorId);
            json.WriteNumber("x", tile.X); json.WriteNumber("y", tile.Y); json.WriteNumber("z", tile.Z);
            json.WriteNumber("width", tile.Width); json.WriteNumber("depth", tile.Depth);
            WriteColor(json, "floorRgb", tile.FloorColor);
            json.WritePropertyName("materials"); WriteMaterials(json, tile.Materials);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteTiledSettings(Utf8JsonWriter json, NativeFreshTiledLevelContent tiled)
    {
        json.WriteStartObject("spawn"); json.WriteNumber("x", tiled.SpawnX); json.WriteNumber("y", tiled.SpawnY);
        json.WriteNumber("z", tiled.SpawnZ); json.WriteNumber("heading", tiled.Heading); json.WriteEndObject();
        WriteColor(json, "skyRgb", tiled.SkyColor); json.WriteNumber("musicTrackId", tiled.MusicTrackId);
    }

    private static ColorRgba Color(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3)
            throw new InvalidDataException("Tiled display color requires exactly three integer channels.");
        int r = Integer(value[0]), g = Integer(value[1]), b = Integer(value[2]);
        if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
            throw new InvalidDataException("Tiled display color channels require0..255.");
        return new((byte)r, (byte)g, (byte)b);
    }

    private static void WriteColor(Utf8JsonWriter json, string name, ColorRgba color)
    {
        json.WriteStartArray(name); json.WriteNumberValue(color.R); json.WriteNumberValue(color.G);
        json.WriteNumberValue(color.B); json.WriteEndArray();
    }

    private static void WriteMaterials(Utf8JsonWriter json, NativeFreshFloorMaterialsIntent materials)
    {
        json.WriteStartObject(); json.WriteString("layout", "rectangle-two-triangles-v1");
        json.WritePropertyName("triangle0"); WriteMaterial(json, materials.Triangle0);
        json.WritePropertyName("triangle1"); WriteMaterial(json, materials.Triangle1);
        json.WriteEndObject();
    }

    private static NativeFreshFloorMaterialsIntent ParseMaterials(JsonElement value)
    {
        var fields = Object(value, "layout", "triangle0", "triangle1");
        if (String(fields["layout"]) != "rectangle-two-triangles-v1")
            throw new InvalidDataException("Unsupported floor-material topology.");
        return NativeFreshFloorMaterialsIntent.Create(ParseMaterial(fields["triangle0"]), ParseMaterial(fields["triangle1"]));
    }

    private static NativeFreshFloorMaterialIntent ParseMaterial(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("tag", out var tag))
            throw new InvalidDataException("Floor material requires an explicit closed tag.");
        switch (String(tag))
        {
            case "base-floor-color":
                _ = Object(value, "tag");
                return NativeFreshFloorMaterialIntent.BaseFloorColor();
            case "retail-terrain-record":
                var fields = Object(value, "tag", "sourceVariant", "sourceWadEntry", "textureId");
                return NativeFreshFloorMaterialIntent.RetailTerrain(String(fields["sourceVariant"]),
                    Integer(fields["sourceWadEntry"]), Integer(fields["textureId"]));
            default:
                throw new InvalidDataException("Unsupported floor-material tag; no payload is discarded.");
        }
    }

    private static void WriteMaterial(Utf8JsonWriter json, NativeFreshFloorMaterialIntent material)
    {
        json.WriteStartObject();
        switch (material)
        {
            case NativeFreshBaseFloorColorMaterialIntent:
                json.WriteString("tag", "base-floor-color");
                break;
            case NativeFreshRetailTerrainMaterialIntent terrain:
                json.WriteString("tag", "retail-terrain-record");
                json.WriteString("sourceVariant", terrain.SourceVariant);
                json.WriteNumber("sourceWadEntry", terrain.SourceWadEntry);
                json.WriteNumber("textureId", terrain.TextureId);
                break;
            default:
                throw new InvalidDataException("Unsupported floor-material variant.");
        }
        json.WriteEndObject();
    }

    private static Dictionary<string, JsonElement> Object(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Fresh level requires an exact JSON object.");
        var result = new Dictionary<string, JsonElement>(fields.Length, StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            string name;
            try { name = property.Name; }
            catch (InvalidOperationException error)
            { throw new InvalidDataException("Fresh level property contains malformed escaped UTF16.", error); }
            if (!fields.Contains(name, StringComparer.Ordinal) || !result.TryAdd(name, property.Value))
                throw new InvalidDataException("Fresh level contains an unknown, case-mismatched or duplicate property.");
        }
        if (result.Count != fields.Length)
            throw new InvalidDataException("Fresh level is missing a required property.");
        return result;
    }

    private static string String(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Fresh level requires a string value.");
        try { return value.GetString()!; }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Fresh level string contains malformed escaped UTF16.", error); }
    }

    private static int Integer(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException("Fresh level requires integer literals.");
        string raw = value.GetRawText();
        if (raw.Contains('.') || raw.Contains('e') || raw.Contains('E') || !value.TryGetInt32(out int result))
            throw new InvalidDataException("Fresh level requires bounded integers without fractions or exponents.");
        return result;
    }
}
