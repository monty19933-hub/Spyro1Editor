using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Bounded neutral schema1 JSON only. It owns no paths, file I/O, native payloads,
/// legacy migration, source certificates or publisher state. Serialization is
/// explicit so derived values and future object properties cannot leak into JSON.
/// </summary>
public static class NativeId65FreshDocumentCodec
{
    private const string Kind = "spyro-native-fresh-intent";
    private const int MaximumByteLength = 16 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static NativeId65FreshDocumentIntent Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length is < 1 or > MaximumByteLength)
            throw new InvalidDataException("Fresh intent JSON must contain1..16384 UTF8 bytes.");
        if (utf8.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            throw new InvalidDataException("Fresh intent JSON does not accept a BOM.");
        try
        {
            _ = StrictUtf8.GetCharCount(utf8); // Strict validation before the bounded parser snapshot.
            using var json = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
            var root = Object(json.RootElement, "kind", "schema", "floor", "spawn", "display", "musicTrackId");
            if (DecodeString(root["kind"]) != Kind ||
                Integer(root["schema"]) != 1)
                throw new InvalidDataException("Not the supported distinct fresh-intent kind/schema1.");
            var floor = Object(root["floor"], "x", "y", "z", "width", "depth");
            var spawn = Object(root["spawn"], "x", "y", "heading");
            var display = Object(root["display"], "floorRgb", "skyRgb");
            return NativeId65FreshDocumentIntent.Create(
                Integer(floor["x"]), Integer(floor["y"]), Integer(floor["z"]),
                Integer(floor["width"]), Integer(floor["depth"]),
                Integer(spawn["x"]), Integer(spawn["y"]), Integer(spawn["heading"]),
                Color(display["floorRgb"]), Color(display["skyRgb"]), Integer(root["musicTrackId"]));
        }
        catch (DecoderFallbackException error)
        { throw new InvalidDataException("Fresh intent contains invalid UTF8.", error); }
        catch (JsonException error)
        { throw new InvalidDataException("Fresh intent contains invalid or over-depth JSON.", error); }
    }

    public static byte[] SerializeCanonical(NativeId65FreshDocumentIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        using var memory = new MemoryStream();
        using (var json = new Utf8JsonWriter(memory))
        {
            json.WriteStartObject();
            json.WriteString("kind", Kind);
            json.WriteNumber("schema", 1);
            json.WriteStartObject("floor");
            json.WriteNumber("x", intent.FloorX); json.WriteNumber("y", intent.FloorY); json.WriteNumber("z", intent.FloorZ);
            json.WriteNumber("width", intent.FloorWidth); json.WriteNumber("depth", intent.FloorDepth);
            json.WriteEndObject();
            json.WriteStartObject("spawn");
            json.WriteNumber("x", intent.SpawnX); json.WriteNumber("y", intent.SpawnY); json.WriteNumber("heading", intent.Heading);
            json.WriteEndObject();
            json.WriteStartObject("display");
            WriteColor(json, "floorRgb", intent.FloorColor);
            WriteColor(json, "skyRgb", intent.SkyColor);
            json.WriteEndObject();
            json.WriteNumber("musicTrackId", intent.MusicTrackId);
            json.WriteEndObject();
        }
        return memory.ToArray();
    }

    private static Dictionary<string, JsonElement> Object(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Fresh intent requires an exact JSON object.");
        var result = new Dictionary<string, JsonElement>(fields.Length, StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            string name;
            try { name = property.Name; }
            catch (InvalidOperationException error)
            { throw new InvalidDataException("Fresh intent property contains malformed escaped UTF16.", error); }
            if (!fields.Contains(name, StringComparer.Ordinal) || !result.TryAdd(name, property.Value))
                throw new InvalidDataException("Fresh intent contains an unknown, case-mismatched or duplicate property.");
        }
        if (result.Count != fields.Length)
            throw new InvalidDataException("Fresh intent is missing a required property.");
        return result;
    }

    private static string? DecodeString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Fresh intent kind must be a string.");
        // JsonDocument accepts ASCII surrogate escapes before decoding strings.
        // Only this decode operation is normalized; no broad Parse exception catch.
        try { return value.GetString(); }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Fresh intent string contains malformed escaped UTF16.", error); }
    }

    private static int Integer(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException("Fresh intent requires integer literals, not strings or null.");
        string raw = value.GetRawText();
        if (raw.Contains('.') || raw.Contains('e') || raw.Contains('E') || !value.TryGetInt32(out int integer))
            throw new InvalidDataException("Fresh intent requires bounded integer literals without fractions or exponents.");
        return integer;
    }

    private static ColorRgba Color(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3)
            throw new InvalidDataException("Fresh intent display RGB requires exactly three integer channels; alpha is not serialized.");
        int r = Integer(value[0]), g = Integer(value[1]), b = Integer(value[2]);
        if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
            throw new InvalidDataException("Fresh intent display RGB channels must be0..255 without clamping.");
        return new((byte)r, (byte)g, (byte)b);
    }

    private static void WriteColor(Utf8JsonWriter json, string name, ColorRgba value)
    {
        json.WriteStartArray(name);
        json.WriteNumberValue(value.R); json.WriteNumberValue(value.G); json.WriteNumberValue(value.B);
        json.WriteEndArray();
    }
}
