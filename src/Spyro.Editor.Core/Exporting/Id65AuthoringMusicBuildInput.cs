using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Music;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// An explicit authored build input, not a patch or a new interpretation of
/// the schema-6 document's locked music handle. It embeds the unchanged
/// baseline document and binds its exact stored envelope. Geometry changes
/// require a newly authored input; a stale attachment cannot silently follow
/// a different document. No native bytes, address, path or publisher state is
/// accepted here. Serialization alone grants no candidate/runtime authority.
/// </summary>
internal sealed class Id65AuthoringMusicBuildInput
{
    public const string Format = "spyro-editor-id65-authoring-music-build-input";
    public const int SchemaVersion = 1;
    public const string ProfileId = "id65-schema6-built-in-music-build-input-v1";
    public const int MaximumSerializedByteCount = 128 * 1024;
    private const string MusicKind = "built-in-track";
    private static readonly string[] RootProperties =
    [
        "format", "schemaVersion", "profileId", "baselineEnvelopeCanonicalSha256",
        "baselineDocumentCanonicalSha256", "baselineDocument", "music", "contentSha256"
    ];
    private readonly byte[] _canonicalUtf8;

    private Id65AuthoringMusicBuildInput(
        Id65AuthoringEditorDocument baselineDocument,
        string baselineEnvelopeCanonicalSha256,
        int trackId)
    {
        ArgumentNullException.ThrowIfNull(baselineDocument);
        RequireSha256(baselineEnvelopeCanonicalSha256, "baseline envelope");
        if (baselineDocument.SchemaVersion !=
            Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion)
            throw new InvalidDataException("An authored music build input requires the exact schema-6 blank-cyclorama baseline.");
        MusicTrackEntry? track = MusicTrackCatalog.Find(trackId);
        if (track is null || !track.IsSelectable)
            throw new InvalidDataException("The authored music track is unknown or reserved, not a selectable built-in track.");

        BaselineDocument = Id65AuthoringEditorDocument.Parse(baselineDocument.SerializeUtf8());
        BaselineEnvelopeCanonicalSha256 = baselineEnvelopeCanonicalSha256;
        SelectedTrackId = trackId;
        ContentSha256 = Hash(WriteCanonical(contentSha256: null));
        _canonicalUtf8 = WriteCanonical(ContentSha256);
        CanonicalSha256 = Hash(_canonicalUtf8);
    }

    public Id65AuthoringEditorDocument BaselineDocument { get; }
    public string BaselineDocumentCanonicalSha256 => BaselineDocument.CanonicalSha256;
    public string BaselineEnvelopeCanonicalSha256 { get; }
    public int SelectedTrackId { get; }
    public string ContentSha256 { get; }
    public string CanonicalSha256 { get; }
    public byte[] SerializeUtf8() => _canonicalUtf8.ToArray();

    public static Id65AuthoringMusicBuildInput Create(
        Id65AuthoringEditorDocument baselineDocument,
        string baselineEnvelopeCanonicalSha256,
        int selectedTrackId) => new(baselineDocument, baselineEnvelopeCanonicalSha256, selectedTrackId);

    public void RequireExactBaseline(
        Id65AuthoringEditorDocument currentDocument,
        string currentEnvelopeCanonicalSha256)
    {
        ArgumentNullException.ThrowIfNull(currentDocument);
        if (currentEnvelopeCanonicalSha256 != BaselineEnvelopeCanonicalSha256 ||
            currentDocument.CanonicalSha256 != BaselineDocumentCanonicalSha256 ||
            !currentDocument.SerializeUtf8().AsSpan().SequenceEqual(BaselineDocument.SerializeUtf8()))
            throw new InvalidDataException("The authored music input is stale: its exact baseline document or stored envelope changed. Explicitly author a new combined input.");
    }

    public static Id65AuthoringMusicBuildInput Parse(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty || utf8Json.Length > MaximumSerializedByteCount)
            throw new InvalidDataException("The authored music build input is empty or exceeds its bounded canonical JSON size.");
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(utf8Json.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 32
            });
            JsonElement root = parsed.RootElement;
            RequireProperties(root, RootProperties, "build input");
            if (ReadString(root, "format") != Format ||
                ReadInt(root, "schemaVersion") != SchemaVersion ||
                ReadString(root, "profileId") != ProfileId)
                throw new InvalidDataException("The authored music build-input format, schema or profile is unsupported.");
            JsonElement music = root.GetProperty("music");
            RequireProperties(music, ["kind", "trackId"], "music intent");
            if (ReadString(music, "kind") != MusicKind)
                throw new InvalidDataException("Only an explicit built-in-track music intent is supported.");
            JsonElement baseline = root.GetProperty("baselineDocument");
            if (baseline.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The music input must contain its complete canonical baseline document.");
            Id65AuthoringMusicBuildInput result = Create(
                Id65AuthoringEditorDocument.Parse(baseline.GetRawText()),
                ReadString(root, "baselineEnvelopeCanonicalSha256"),
                ReadInt(music, "trackId"));
            if (ReadString(root, "baselineDocumentCanonicalSha256") != result.BaselineDocumentCanonicalSha256 ||
                ReadString(root, "contentSha256") != result.ContentSha256 ||
                !utf8Json.SequenceEqual(result._canonicalUtf8))
                throw new InvalidDataException("The authored music build input is stale, tampered or noncanonical.");
            return result;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The authored music build input is not valid bounded JSON.", exception);
        }
    }

    private byte[] WriteCanonical(string? contentSha256)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteString("profileId", ProfileId);
            writer.WriteString("baselineEnvelopeCanonicalSha256", BaselineEnvelopeCanonicalSha256);
            writer.WriteString("baselineDocumentCanonicalSha256", BaselineDocumentCanonicalSha256);
            writer.WritePropertyName("baselineDocument");
            writer.WriteRawValue(BaselineDocument.SerializeUtf8(), skipInputValidation: false);
            writer.WriteStartObject("music");
            writer.WriteString("kind", MusicKind);
            writer.WriteNumber("trackId", SelectedTrackId);
            writer.WriteEndObject();
            if (contentSha256 is not null) writer.WriteString("contentSha256", contentSha256);
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private static void RequireProperties(JsonElement element, IReadOnlyCollection<string> names, string label)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"The authored music {label} must be an object.");
        string[] found = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (found.Length != names.Count || found.Distinct(StringComparer.Ordinal).Count() != found.Length ||
            found.Any(name => !names.Contains(name, StringComparer.Ordinal)))
            throw new InvalidDataException($"The authored music {label} contains missing, duplicate or unsupported properties.");
    }

    private static string ReadString(JsonElement element, string property) =>
        element.GetProperty(property).ValueKind == JsonValueKind.String
            ? element.GetProperty(property).GetString()!
            : throw new InvalidDataException($"The authored music {property} must be a string.");

    private static int ReadInt(JsonElement element, string property) =>
        element.GetProperty(property).ValueKind == JsonValueKind.Number && element.GetProperty(property).TryGetInt32(out int value)
            ? value
            : throw new InvalidDataException($"The authored music {property} must be an exact 32-bit integer.");

    private static void RequireSha256(string value, string label)
    {
        if (value is null || value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidDataException($"The authored music {label} identity must be a lowercase SHA-256 value.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
