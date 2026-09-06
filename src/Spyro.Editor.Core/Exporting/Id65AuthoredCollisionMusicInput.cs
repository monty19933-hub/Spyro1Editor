using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal enum Id65AuthoredCollisionMusicSubstrate { SingleSector = 1, ExactCollapsedSky = 2 }

/// <summary>
/// Explicit new compiled profile around the unchanged canonical schema-6 music
/// input. It is retained with immutable candidates, never as a loose editable
/// sidecar. Compiler stages are not a migration of the persisted editor schema.
/// </summary>
internal sealed class Id65AuthoredCollisionMusicInput
{
    public const string Format = "spyro-editor-id65-authored-collision-music-input";
    public const string ProfileId = "id65-authored-collision-music-input-v1";
    private readonly byte[] _canonical;
    private Id65AuthoredCollisionMusicInput(Id65AuthoringMusicBuildInput music, Id65AuthoredCollisionMusicSubstrate substrate)
    {
        ArgumentNullException.ThrowIfNull(music);
        if (!Enum.IsDefined(substrate)) throw new InvalidDataException("Unknown authored collision substrate profile.");
        Music = Id65AuthoringMusicBuildInput.Parse(music.SerializeUtf8()); Substrate = substrate;
        ContentSha256 = Id65AuthoredCollisionMusicPlan.Hash(Write(null));
        _canonical = Write(ContentSha256); CanonicalSha256 = Id65AuthoredCollisionMusicPlan.Hash(_canonical);
    }
    public Id65AuthoringMusicBuildInput Music { get; }
    public Id65AuthoredCollisionMusicSubstrate Substrate { get; }
    public string ContentSha256 { get; }
    public string CanonicalSha256 { get; }
    public byte[] SerializeUtf8() => _canonical.ToArray();
    public static Id65AuthoredCollisionMusicInput Create(Id65AuthoringMusicBuildInput music,
        Id65AuthoredCollisionMusicSubstrate substrate = Id65AuthoredCollisionMusicSubstrate.SingleSector) => new(music, substrate);
    public static Id65AuthoredCollisionMusicInput Parse(ReadOnlySpan<byte> json)
    {
        if (json.IsEmpty || json.Length > Id65AuthoringMusicBuildInput.MaximumSerializedByteCount + 1024)
            throw new InvalidDataException("Authored composition input exceeds bounded canonical JSON.");
        try
        {
            using JsonDocument document = JsonDocument.Parse(json.ToArray(), new() { MaxDepth = 40 });
            var root = document.RootElement;
            string[] names = ["format", "schemaVersion", "profileId", "substrate", "musicBuildInput", "contentSha256"];
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != names.Length ||
                root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != names.Length ||
                root.EnumerateObject().Any(p => !names.Contains(p.Name, StringComparer.Ordinal)) ||
                ReadString(root, "format") != Format || root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number ||
                !root.GetProperty("schemaVersion").TryGetInt32(out int version) || version != 1 ||
                ReadString(root, "profileId") != ProfileId)
                throw new InvalidDataException("Unknown, incomplete or duplicate authored composition profile fields.");
            var substrate = ReadString(root, "substrate") switch
            {
                "single-sector-isolated-collision" => Id65AuthoredCollisionMusicSubstrate.SingleSector,
                "exact-collapsed-sky-isolated-collision" => Id65AuthoredCollisionMusicSubstrate.ExactCollapsedSky,
                _ => throw new InvalidDataException("Unknown authored substrate intent.")
            };
            var result = Create(Id65AuthoringMusicBuildInput.Parse(System.Text.Encoding.UTF8.GetBytes(root.GetProperty("musicBuildInput").GetRawText())), substrate);
            if (ReadString(root, "contentSha256") != result.ContentSha256 || !json.SequenceEqual(result._canonical))
                throw new InvalidDataException("Authored composition input is tampered, stale or noncanonical.");
            return result;
        }
        catch (JsonException exception) { throw new InvalidDataException("Invalid authored composition JSON.", exception); }
    }
    private byte[] Write(string? contentHash)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject(); writer.WriteString("format", Format); writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("profileId", ProfileId);
            writer.WriteString("substrate", Substrate == Id65AuthoredCollisionMusicSubstrate.SingleSector
                ? "single-sector-isolated-collision" : "exact-collapsed-sky-isolated-collision");
            writer.WritePropertyName("musicBuildInput"); writer.WriteRawValue(Music.SerializeUtf8(), skipInputValidation: false);
            if (contentHash is not null) writer.WriteString("contentSha256", contentHash);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }
    private static string ReadString(JsonElement root, string key) => root.GetProperty(key).ValueKind == JsonValueKind.String
        ? root.GetProperty(key).GetString()! : throw new InvalidDataException("Authored composition string field has a wrong JSON type.");
}
