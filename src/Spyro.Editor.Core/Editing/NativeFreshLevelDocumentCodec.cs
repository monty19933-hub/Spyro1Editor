using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Strict neutral whole-level schema1/schema2. No filesystem, implicit migration, native payload,
/// compiler or publisher API. The embedded floor uses the unchanged v1 codec;
/// its legacy format and its independent16KiB parser bound remain intact.
/// </summary>
public static class NativeFreshLevelDocumentCodec
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
                2 => Object(rootElement, "kind", "schema", "base", "placements", "floorMaterials"),
                _ => throw new InvalidDataException("Not a supported distinct fresh-level schema.")
            };
            if (String(root["kind"]) != Kind)
                throw new InvalidDataException("Not the supported distinct fresh-level kind.");
            JsonElement rows = root["placements"];
            if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > NativeFreshLevelDocumentIntent.MaximumPlacements)
                throw new InvalidDataException("Fresh level placements require an array with at most128 authoring rows.");
            var @base = NativeId65FreshDocumentCodec.Parse(StrictUtf8.GetBytes(root["base"].GetRawText()));
            var placements = new List<NativeFreshPlacementIntent>(rows.GetArrayLength());
            foreach (var row in rows.EnumerateArray())
            {
                var placement = Object(row, "id", "tag", "origin", "routeEnd");
                if (String(placement["tag"]) != "wizard-route")
                    throw new InvalidDataException("Unknown fresh-level placement tag; no opaque payload is retained or discarded.");
                var origin = Object(placement["origin"], "x", "y", "z");
                var end = Object(placement["routeEnd"], "x", "y");
                placements.Add(NativeFreshWizardRouteIntent.Create(String(placement["id"]),
                    Integer(origin["x"]), Integer(origin["y"]), Integer(origin["z"]), Integer(end["x"]), Integer(end["y"])));
            }
            var intent = NativeFreshLevelDocumentIntent.Create(@base, placements);
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
            json.WritePropertyName("base");
            json.WriteRawValue(NativeId65FreshDocumentCodec.SerializeCanonical(intent.Base));
            json.WriteStartArray("placements");
            foreach (var placement in intent.Placements)
            {
                if (placement is not NativeFreshWizardRouteIntent wizard)
                    throw new InvalidDataException("Unsupported fresh-level placement variant.");
                json.WriteStartObject(); json.WriteString("id", wizard.EditorId); json.WriteString("tag", "wizard-route");
                json.WriteStartObject("origin");
                json.WriteNumber("x", wizard.OriginX); json.WriteNumber("y", wizard.OriginY); json.WriteNumber("z", wizard.OriginZ);
                json.WriteEndObject(); json.WriteStartObject("routeEnd");
                json.WriteNumber("x", wizard.EndX); json.WriteNumber("y", wizard.EndY);
                json.WriteEndObject(); json.WriteEndObject();
            }
            json.WriteEndArray();
            if (intent.FloorMaterials is { } materials)
            {
                json.WriteStartObject("floorMaterials");
                json.WriteString("layout", "rectangle-two-triangles-v1");
                json.WritePropertyName("triangle0"); WriteMaterial(json, materials.Triangle0);
                json.WritePropertyName("triangle1"); WriteMaterial(json, materials.Triangle1);
                json.WriteEndObject();
            }
            json.WriteEndObject();
        }
        if (memory.Length > MaximumByteLength)
            throw new InvalidDataException("Canonical fresh level exceeds the independent authoring byte budget.");
        return memory.ToArray();
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
