using System.Text.Json;
using Spyro.Editor.Core.Rendering;

namespace Spyro.Editor.Core.Scene;

public static class SourceSceneOverlayContract
{
    public const int CurrentFormatVersion = 9;
    public const int CornerSlotCount = 4;
    public const string HpColorLayout = "split-contiguous-4-byte-table1-table2-v1";
    public const string HpCornerPayload = "raw-four-slot-near-fade-v1";
    public const string HpFaceMaterialPayload = "raw-words0-3-explicit-word2-word3-material-v2";
    public const string HpCoordinatePayload = NativeTerrainHighPolyCoordinates.Contract;
    public const string LpColorLayout = "single-4-byte-gouraud-rgb";
    public const string LpFacePayload = "packed-six-bit-four-slot-static-v1";
    public const string TerrainLodPreview = "static-lp-all-sectors-no-occlusion-v1";
    public const string SceneChainPolicy = "trim-short-unterminated-suffix-v1";
    public const string TerrainOcclusion = "environment-sector-lists-plus-collision-triangle-assignment-v1";

    internal static bool HasExplicitSourceProvenance(JsonElement root)
    {
        if (root.TryGetProperty("sourcePackage", out JsonElement sourcePackage) &&
            sourcePackage.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        return root.TryGetProperty("candidates", out JsonElement candidates) &&
            candidates.ValueKind == JsonValueKind.Array &&
            candidates.EnumerateArray().Any(candidate =>
                JsonValue.GetString(candidate, "source").Contains("source-wad", StringComparison.OrdinalIgnoreCase));
    }
}
