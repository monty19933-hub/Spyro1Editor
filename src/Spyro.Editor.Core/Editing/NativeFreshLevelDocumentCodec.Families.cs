using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Editing;

public static partial class NativeFreshLevelDocumentCodec
{
    private static NativeFreshLevelDocumentIntent ParseFamilyContent(JsonElement value,
        IReadOnlyList<NativeFreshPlacementIntent> placements)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("tag", out var tag))
            throw new InvalidDataException("Schema5 requires an explicit closed content tag.");
        switch (String(tag))
        {
            case "legacy-floor":
                var legacy = Object(value, "tag", "base", "floorMaterials");
                var @base = NativeId65FreshDocumentCodec.Parse(StrictUtf8.GetBytes(legacy["base"].GetRawText()));
                var document = NativeFreshLevelDocumentIntent.Create(@base, []).UpgradeForFamilies();
                if (legacy["floorMaterials"].ValueKind != JsonValueKind.Null)
                    document = document.WithFloorMaterials(ParseMaterials(legacy["floorMaterials"]));
                return document.WithPlacements(placements);
            case "ordered-flat-tiles-v1":
                var tiled = Object(value, "tag", "tiles", "spawn", "skyRgb", "musicTrackId");
                return NativeFreshLevelDocumentIntent.CreateTiled(ParseTiledContent(tiled["tiles"], tiled), [])
                    .UpgradeForFamilies().WithPlacements(placements);
            default:
                throw new InvalidDataException("Unsupported schema5 content tag; no competing or opaque content is retained.");
        }
    }

    private static void WriteFamilyContent(Utf8JsonWriter json, NativeFreshLevelDocumentIntent intent)
    {
        json.WriteStartObject("content");
        if (intent.LegacyContent is { } legacy)
        {
            json.WriteString("tag", "legacy-floor");
            json.WritePropertyName("base");
            json.WriteRawValue(NativeId65FreshDocumentCodec.SerializeCanonical(legacy.Base));
            if (legacy.FloorMaterials is { } materials)
            {
                json.WritePropertyName("floorMaterials"); WriteMaterials(json, materials);
            }
            else json.WriteNull("floorMaterials");
        }
        else if (intent.TiledContent is { } tiled)
        {
            json.WriteString("tag", "ordered-flat-tiles-v1");
            WriteTiles(json, tiled);
            WriteTiledSettings(json, tiled);
        }
        else throw new InvalidDataException("Unsupported schema5 content variant.");
        json.WriteEndObject();
    }

    private static NativeFreshLockedChestFamilyPlacementIntent ParseFamilyPlacement(JsonElement value)
    {
        var family = Object(value, "id", "tag", "source", "key", "chest", "rewardDestinations");
        var source = Object(family["source"], "sourceVariant", "definitionId");
        var key = Object(family["key"], "x", "y", "z");
        var chest = Object(family["chest"], "x", "y", "z");
        return NativeFreshLockedChestFamilyPlacementIntent.Create(String(family["id"]),
            NativeFreshLockedChestFamilyReference.Create(String(source["sourceVariant"]), String(source["definitionId"])),
            Integer(key["x"]), Integer(key["y"]), Integer(key["z"]),
            Integer(chest["x"]), Integer(chest["y"]), Integer(chest["z"]), ParseRewardDestinations(family["rewardDestinations"]));
    }

    private static NativeFreshChestRewardDestinationOverrides? ParseRewardDestinations(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 1 or > 5)
            throw new InvalidDataException("Reward destinations require null or one to five explicit semantic roles.");
        var destinations = new NativeFreshChestRewardDestinationOverride[value.GetArrayLength()];
        int index = 0;
        foreach (var row in value.EnumerateArray())
        {
            var destination = Object(row, "role", "x", "y", "z");
            NativeFreshChestRewardRole role = String(destination["role"]) switch
            {
                "reward.1" => NativeFreshChestRewardRole.Reward1,
                "reward.2" => NativeFreshChestRewardRole.Reward2,
                "reward.3" => NativeFreshChestRewardRole.Reward3,
                "reward.4" => NativeFreshChestRewardRole.Reward4,
                "reward.5" => NativeFreshChestRewardRole.Reward5,
                _ => throw new InvalidDataException("Reward destination requires an exact reward.1 through reward.5 role.")
            };
            destinations[index++] = new(role, Integer(destination["x"]), Integer(destination["y"]), Integer(destination["z"]));
        }
        // This immutable owner validates uniqueness/ranges and sorts by semantic role.
        // Null remains absence; explicit WORLD values are never compared with source defaults.
        return NativeFreshChestRewardDestinationOverrides.Create(destinations);
    }

    private static void WriteFamilyPlacement(Utf8JsonWriter json, NativeFreshLockedChestFamilyPlacementIntent family)
    {
        json.WriteStartObject(); json.WriteString("id", family.EditorId); json.WriteString("tag", "locked-chest-family");
        json.WriteStartObject("source");
        json.WriteString("sourceVariant", family.Source.SourceVariant); json.WriteString("definitionId", family.Source.DefinitionId);
        json.WriteEndObject();
        WriteFamilyPoint(json, "key", family.KeyX, family.KeyY, family.KeyZ);
        WriteFamilyPoint(json, "chest", family.ChestX, family.ChestY, family.ChestZ);
        if (family.RewardDestinations is { } destinations)
        {
            json.WriteStartArray("rewardDestinations");
            foreach (var destination in destinations.Values)
            {
                string role = destination.Role switch
                {
                    NativeFreshChestRewardRole.Reward1 => "reward.1",
                    NativeFreshChestRewardRole.Reward2 => "reward.2",
                    NativeFreshChestRewardRole.Reward3 => "reward.3",
                    NativeFreshChestRewardRole.Reward4 => "reward.4",
                    NativeFreshChestRewardRole.Reward5 => "reward.5",
                    _ => throw new InvalidDataException("Unsupported reward destination role.")
                };
                json.WriteStartObject(); json.WriteString("role", role);
                json.WriteNumber("x", destination.X); json.WriteNumber("y", destination.Y); json.WriteNumber("z", destination.Z);
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
        else json.WriteNull("rewardDestinations");
        json.WriteEndObject();
    }

    private static void WriteFamilyPoint(Utf8JsonWriter json, string name, int x, int y, int z)
    {
        json.WriteStartObject(name); json.WriteNumber("x", x); json.WriteNumber("y", y); json.WriteNumber("z", z);
        json.WriteEndObject();
    }
}
