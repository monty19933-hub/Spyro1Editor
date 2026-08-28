using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// The mutually exclusive Moby choices supported by the first schema-4 fresh
/// ID65 authoring document. None is an authored choice, not a missing value.
/// </summary>
public enum Id65RemotePadMobyKind
{
    None = 0,
    GrassT107 = 1,
    RedGemT21 = 2
}

/// <summary>
/// One path-free choice in the exact fresh-ID65 remote-pad Moby catalog.
/// </summary>
public sealed record Id65RemotePadMobyChoice(
    Id65RemotePadMobyKind Kind,
    string KindId,
    string Label,
    string? PlacementId,
    int? TargetTrueIndex,
    string? ExactCatalogProfileId,
    string? ExactCatalogSha256);

/// <summary>
/// Pure schema-4 selection catalog. It binds None, Grass T107, and the resident
/// Red Gem T21 to one shared nine-position grid while keeping runtime,
/// materialization, Create BIN, and release authorization closed.
/// </summary>
public static class Id65RemotePadMobyChoiceCatalog
{
    public const string ProfileId = "id65-authoring-remote-pad-moby-choice-v1";
    public const int SchemaVersion = 1;
    public const string NoneKindId = "none";
    public const string GrassT107KindId = "grass-t107";
    public const string RedGemT21KindId = "red-gem-t21";
    public const string SelectionSemantics = "mutually-exclusive-one-or-none";
    public const int MaximumSimultaneousPlacementCount = 1;
    public const int MandatoryBaseFaceTextureId = 66;
    public const bool GrassT88Allowed = false;
    public const bool RuntimeAcceptancePassed = false;
    public const bool RuntimeCandidateAuthorized = false;
    public const bool PhysicalMaterializationEnabled = false;
    public const bool NormalCreateBinAuthorized = false;
    public const bool ReleasePublicationAuthorized = false;

    // Pinned after the complete canonical catalog below is constructed.
    public const string ExpectedCanonicalSha256 =
        "e83d33ca8b8ad4fc3555eb5d668df0a67792e0a33e3c3f7d96bd0ee620f297bf";

    private static readonly Id65RemotePadMobyChoice[] ChoiceArray =
    [
        new(Id65RemotePadMobyKind.None, NoneKindId, "None", null, null, null, null),
        new(
            Id65RemotePadMobyKind.GrassT107,
            GrassT107KindId,
            "Grass",
            Id65RemotePadGrassT107Catalog.PlacementId,
            Id65RemotePadGrassT107Catalog.TargetTrueIndex,
            Id65RemotePadGrassT107Catalog.ProfileId,
            Id65RemotePadGrassT107Catalog.ExpectedCanonicalSha256),
        new(
            Id65RemotePadMobyKind.RedGemT21,
            RedGemT21KindId,
            "Red Gem",
            Id65RemotePadRedGemT21Catalog.PlacementId,
            Id65RemotePadRedGemT21Catalog.TargetTrueIndex,
            Id65RemotePadRedGemT21Catalog.ProfileId,
            Id65RemotePadRedGemT21Catalog.ExpectedCanonicalSha256)
    ];

    private static readonly ReadOnlyCollection<Id65RemotePadMobyChoice> ChoiceView =
        Array.AsReadOnly(ChoiceArray);
    private static readonly ReadOnlyDictionary<Id65RemotePadMobyKind, Id65RemotePadMobyChoice>
        ChoiceByKind = new(ChoiceArray.ToDictionary(item => item.Kind));
    private static readonly ReadOnlyDictionary<string, Id65RemotePadMobyChoice> ChoiceById =
        new(ChoiceArray.ToDictionary(item => item.KindId, StringComparer.Ordinal));

    public static IReadOnlyList<Id65RemotePadMobyChoice> Choices => ChoiceView;
    public static string DefaultPositionId => Id65RemotePadGrassT107Catalog.DefaultPositionId;
    public static string CanonicalJson { get; } = BuildCanonicalJson();
    public static string CanonicalSha256 { get; } = Hash(Encoding.UTF8.GetBytes(CanonicalJson));

    static Id65RemotePadMobyChoiceCatalog() => ValidateFrozenCatalog();

    public static Id65RemotePadMobyChoice GetRequiredChoice(Id65RemotePadMobyKind kind)
    {
        if (!ChoiceByKind.TryGetValue(kind, out Id65RemotePadMobyChoice? choice))
            throw new ArgumentOutOfRangeException(nameof(kind));
        return choice;
    }

    public static Id65RemotePadMobyKind ParseKindId(string kindId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kindId);
        if (!ChoiceById.TryGetValue(kindId, out Id65RemotePadMobyChoice? choice))
            throw new KeyNotFoundException($"Unknown remote-pad Moby kind ID '{kindId}'.");
        return choice.Kind;
    }

    public static string GetKindId(Id65RemotePadMobyKind kind) =>
        GetRequiredChoice(kind).KindId;

    /// <summary>
    /// Resolves the retained grid position through both exact actor catalogs.
    /// Even None retains one valid dormant position so switching choices is
    /// deterministic and cannot invent coordinates.
    /// </summary>
    public static string GetRequiredPositionId(string positionId)
    {
        Id65RemotePadGrassT107Position grass =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        Id65RemotePadRedGemT21Position gem =
            Id65RemotePadRedGemT21Catalog.GetRequiredPosition(positionId);
        if (grass.Ordinal != gem.Ordinal || grass.PositionId != gem.PositionId ||
            grass.Label != gem.Label || grass.EditorX != gem.EditorX ||
            grass.EditorY != gem.EditorY || grass.EditorZ != gem.EditorZ ||
            grass.RawX != gem.RawX || grass.RawY != gem.RawY || grass.RawZ != gem.RawZ ||
            grass.IsDefault != gem.IsDefault)
        {
            throw new InvalidDataException(
                "The Grass and Red Gem remote-pad grids no longer share one exact position identity.");
        }
        return grass.PositionId;
    }

    public static string? GetSelectedExactRowSha256(
        Id65RemotePadMobyKind kind,
        string positionId)
    {
        string exactPositionId = GetRequiredPositionId(positionId);
        return kind switch
        {
            Id65RemotePadMobyKind.None => null,
            Id65RemotePadMobyKind.GrassT107 =>
                Id65RemotePadGrassT107Catalog.GetRequiredPosition(exactPositionId).RowSha256,
            Id65RemotePadMobyKind.RedGemT21 =>
                Id65RemotePadRedGemT21Catalog.GetRequiredPosition(exactPositionId).OutputRowSha256,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    internal static Id65MobyPlacementIntent[] CreateExactPlacements(
        Id65RemotePadMobyKind kind,
        string positionId)
    {
        string exactPositionId = GetRequiredPositionId(positionId);
        return kind switch
        {
            Id65RemotePadMobyKind.None => [],
            Id65RemotePadMobyKind.GrassT107 =>
                [Id65RemotePadGrassT107Catalog.CreateRemotePadT107Placement(exactPositionId)],
            Id65RemotePadMobyKind.RedGemT21 =>
                [Id65RemotePadRedGemT21Catalog.CreateRemotePadT21Placement(exactPositionId)],
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static void ValidateFrozenCatalog()
    {
        if (ChoiceArray.Length != 3 || ChoiceByKind.Count != 3 || ChoiceById.Count != 3 ||
            MaximumSimultaneousPlacementCount != 1 || MandatoryBaseFaceTextureId != 66 ||
            GrassT88Allowed || RuntimeAcceptancePassed || RuntimeCandidateAuthorized ||
            PhysicalMaterializationEnabled || NormalCreateBinAuthorized ||
            ReleasePublicationAuthorized ||
            Id65RemotePadGrassT107Catalog.Positions.Count != 9 ||
            Id65RemotePadRedGemT21Catalog.Positions.Count != 9)
        {
            throw new InvalidDataException(
                "The schema-4 remote-pad Moby choice cardinality or fail-closed policy changed.");
        }

        foreach (Id65RemotePadGrassT107Position position in
                 Id65RemotePadGrassT107Catalog.Positions)
        {
            if (GetRequiredPositionId(position.PositionId) != position.PositionId)
                throw new InvalidDataException("The shared schema-4 remote-pad grid changed.");
        }

        if (!string.Equals(CanonicalSha256, ExpectedCanonicalSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Remote-pad Moby choice catalog canonical SHA changed: actual {CanonicalSha256}, " +
                $"expected {ExpectedCanonicalSha256}.");
        }
    }

    private static string BuildCanonicalJson()
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter json = new(stream, new JsonWriterOptions { Indented = false }))
        {
            json.WriteStartObject();
            json.WriteString("profileId", ProfileId);
            json.WriteNumber("schemaVersion", SchemaVersion);
            json.WriteString("selectionSemantics", SelectionSemantics);
            json.WriteNumber("maximumSimultaneousPlacementCount", MaximumSimultaneousPlacementCount);
            json.WriteNumber("mandatoryBaseFaceTextureId", MandatoryBaseFaceTextureId);
            json.WriteBoolean("grassT88Allowed", GrassT88Allowed);
            json.WriteString("defaultPositionId", DefaultPositionId);
            json.WriteString("grassCatalogSha256", Id65RemotePadGrassT107Catalog.CanonicalSha256);
            json.WriteString("redGemCatalogSha256", Id65RemotePadRedGemT21Catalog.CanonicalSha256);
            json.WritePropertyName("choices");
            json.WriteStartArray();
            foreach (Id65RemotePadMobyChoice choice in ChoiceArray)
            {
                json.WriteStartObject();
                json.WriteString("kindId", choice.KindId);
                json.WriteString("label", choice.Label);
                if (choice.PlacementId is null)
                    json.WriteNull("placementId");
                else
                    json.WriteString("placementId", choice.PlacementId);
                if (choice.TargetTrueIndex.HasValue)
                    json.WriteNumber("targetTrueIndex", choice.TargetTrueIndex.Value);
                else
                    json.WriteNull("targetTrueIndex");
                if (choice.ExactCatalogProfileId is null)
                    json.WriteNull("exactCatalogProfileId");
                else
                    json.WriteString("exactCatalogProfileId", choice.ExactCatalogProfileId);
                if (choice.ExactCatalogSha256 is null)
                    json.WriteNull("exactCatalogSha256");
                else
                    json.WriteString("exactCatalogSha256", choice.ExactCatalogSha256);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteBoolean("runtimeAcceptancePassed", RuntimeAcceptancePassed);
            json.WriteBoolean("runtimeCandidateAuthorized", RuntimeCandidateAuthorized);
            json.WriteBoolean("physicalMaterializationEnabled", PhysicalMaterializationEnabled);
            json.WriteBoolean("normalCreateBinAuthorized", NormalCreateBinAuthorized);
            json.WriteBoolean("releasePublicationAuthorized", ReleasePublicationAuthorized);
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
