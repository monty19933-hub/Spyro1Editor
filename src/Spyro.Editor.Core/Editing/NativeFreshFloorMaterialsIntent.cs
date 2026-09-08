namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Two neutral floor-triangle choices, in A/B/C then B/D/C order. These values
/// do not issue source handles, static eligibility, rendering or native authority.
/// </summary>
public sealed class NativeFreshFloorMaterialsIntent
{
    private NativeFreshFloorMaterialsIntent(NativeFreshFloorMaterialIntent triangle0,
        NativeFreshFloorMaterialIntent triangle1)
    {
        Triangle0 = triangle0;
        Triangle1 = triangle1;
    }

    public NativeFreshFloorMaterialIntent Triangle0 { get; }
    public NativeFreshFloorMaterialIntent Triangle1 { get; }

    public static NativeFreshFloorMaterialsIntent Create(NativeFreshFloorMaterialIntent triangle0,
        NativeFreshFloorMaterialIntent triangle1)
    {
        RequireMaterial(triangle0);
        RequireMaterial(triangle1);
        return new(triangle0, triangle1);
    }

    private static void RequireMaterial(NativeFreshFloorMaterialIntent material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (material is not (NativeFreshBaseFloorColorMaterialIntent or NativeFreshRetailTerrainMaterialIntent))
            throw new InvalidDataException("Unsupported closed floor material variant.");
    }
}

/// <summary>Closed neutral union. No caller subclasses or source payloads.</summary>
public abstract class NativeFreshFloorMaterialIntent
{
    private protected NativeFreshFloorMaterialIntent() { }
    public abstract string Tag { get; }

    public static NativeFreshFloorMaterialIntent BaseFloorColor() => NativeFreshBaseFloorColorMaterialIntent.Instance;

    public static NativeFreshRetailTerrainMaterialIntent RetailTerrain(string sourceVariant,
        int sourceWadEntry, int textureId) =>
        NativeFreshRetailTerrainMaterialIntent.Create(sourceVariant, sourceWadEntry, textureId);
}

internal sealed class NativeFreshBaseFloorColorMaterialIntent : NativeFreshFloorMaterialIntent
{
    private NativeFreshBaseFloorColorMaterialIntent() { }
    internal static NativeFreshBaseFloorColorMaterialIntent Instance { get; } = new();
    public override string Tag => "base-floor-color";
}

/// <summary>
/// Intended original-content variant and record coordinates, not an issued
/// texture. A valid descriptor may name an absent, animated or ineligible record.
/// </summary>
public sealed class NativeFreshRetailTerrainMaterialIntent : NativeFreshFloorMaterialIntent
{
    internal const string SupportedSourceVariant = "spyro1-usa-retail-mode2-2352-v1";

    private NativeFreshRetailTerrainMaterialIntent(string sourceVariant, int sourceWadEntry, int textureId)
    {
        SourceVariant = sourceVariant;
        SourceWadEntry = sourceWadEntry;
        TextureId = textureId;
    }

    public override string Tag => "retail-terrain-record";
    public string SourceVariant { get; }
    public int SourceWadEntry { get; }
    public int TextureId { get; }

    internal static NativeFreshRetailTerrainMaterialIntent Create(string sourceVariant, int sourceWadEntry, int textureId)
    {
        ArgumentNullException.ThrowIfNull(sourceVariant);
        if (sourceVariant != SupportedSourceVariant || sourceWadEntry is < 10 or > 78 ||
            (sourceWadEntry & 1) != 0 || textureId is < 0 or > 127)
            throw new InvalidDataException("Floor material requires the known retail source variant, an even data entry10..78 and texture ID0..127.");
        return new(sourceVariant, sourceWadEntry, textureId);
    }
}
