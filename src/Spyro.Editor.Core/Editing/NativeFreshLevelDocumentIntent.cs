using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Neutral authoring aggregate: one unchanged immutable floor intent and an
/// ordered closed placement list, an explicit schema2 floor-material pair, and
/// explicitly upgraded schema3 neutral native-Moby references/transforms.
/// Counts and editor IDs are document ownership,
/// never native capacity, save bits, source authority or publication approval.
/// </summary>
public sealed class NativeFreshLevelDocumentIntent
{
    public const int MaximumPlacements = 128;
    private NativeFreshLevelDocumentIntent(NativeId65FreshDocumentIntent @base,
        IReadOnlyList<NativeFreshPlacementIntent> placements, NativeFreshFloorMaterialsIntent? materials, int schemaVersion)
    { Base = @base; Placements = placements; FloorMaterials = materials; SchemaVersion = schemaVersion; }

    public NativeId65FreshDocumentIntent Base { get; }
    public IReadOnlyList<NativeFreshPlacementIntent> Placements { get; }
    public int SchemaVersion { get; }
    public NativeFreshFloorMaterialsIntent? FloorMaterials { get; }

    /// <summary>Original schema1 factory; never infers or upgrades material intent.</summary>
    public static NativeFreshLevelDocumentIntent Create(NativeId65FreshDocumentIntent @base,
        IEnumerable<NativeFreshPlacementIntent> placements) => CreateContent(@base, placements, null, 1);

    /// <summary>Validate/copy all content while retaining this document's version and material choices.</summary>
    public NativeFreshLevelDocumentIntent WithContent(NativeId65FreshDocumentIntent @base,
        IEnumerable<NativeFreshPlacementIntent> placements) => CreateContent(@base, placements, FloorMaterials, SchemaVersion);

    /// <summary>Explicit material edit, upgrading schema1 to2 but retaining schema3. Clearing both slots does not downgrade.</summary>
    public NativeFreshLevelDocumentIntent WithFloorMaterials(NativeFreshFloorMaterialsIntent materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        return new(Base, Placements, materials, Math.Max(SchemaVersion, 2));
    }

    /// <summary>Explicit schema3 upgrade, retaining all existing content and nullable material intent. Removal never downgrades it.</summary>
    public NativeFreshLevelDocumentIntent UpgradeForNativeMobys() =>
        SchemaVersion == 3 ? this : new(Base, Placements, FloorMaterials, 3);

    private static NativeFreshLevelDocumentIntent CreateContent(NativeId65FreshDocumentIntent @base,
        IEnumerable<NativeFreshPlacementIntent> placements, NativeFreshFloorMaterialsIntent? materials, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(@base); ArgumentNullException.ThrowIfNull(placements);
        var copy = new List<NativeFreshPlacementIntent>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int maxX = checked(@base.FloorX + @base.FloorWidth), maxY = checked(@base.FloorY + @base.FloorDepth);
        foreach (var placement in placements)
        {
            // Bound enumeration itself, not just a possibly unbounded ToArray.
            if (copy.Count == MaximumPlacements)
                throw new InvalidDataException("Fresh level authoring permits at most128 placements; this is not native capacity.");
            if (placement is not NativeFreshWizardRouteIntent &&
                !(schemaVersion == 3 && placement is NativeFreshMobyPlacementIntent))
                throw new InvalidDataException("Fresh level requires a non-null supported closed placement variant.");
            if (!ids.Add(placement.EditorId))
                throw new InvalidDataException("Fresh level editor placement IDs must be unique without regeneration or deduplication.");
            if (placement is NativeFreshWizardRouteIntent wizard &&
                (wizard.OriginX <= @base.FloorX || wizard.OriginX >= maxX ||
                wizard.EndX <= @base.FloorX || wizard.EndX >= maxX ||
                wizard.OriginY <= @base.FloorY || wizard.OriginY >= maxY ||
                wizard.EndY <= @base.FloorY || wizard.EndY >= maxY))
                throw new InvalidDataException("Both authored route endpoints must be strictly inside the base rectangle; no body or corridor clearance is implied.");
            if (placement is NativeFreshMobyPlacementIntent moby &&
                (moby.X <= @base.FloorX || moby.X >= maxX || moby.Y <= @base.FloorY || moby.Y >= maxY))
                throw new InvalidDataException("Authored native Moby XY must be strictly inside the current base rectangle; no body, ground or collision clearance is implied.");
            copy.Add(placement);
        }
        return new(@base, copy.AsReadOnly(), materials, schemaVersion);
    }
}

/// <summary>Closed neutral placement union; external subclasses cannot add unvalidated payloads.</summary>
public abstract class NativeFreshPlacementIntent
{
    private protected NativeFreshPlacementIntent(string editorId)
    {
        ArgumentNullException.ThrowIfNull(editorId);
        if (editorId.Length != 32 || editorId.All(c => c == '0') ||
            editorId.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidDataException("Editor placement ID requires exactly32 lowercase hex digits and a nonzero value.");
        EditorId = editorId;
    }
    public string EditorId { get; }
    public abstract string Tag { get; }
}

/// <summary>
/// Geometric Wizard route intent only. XY uses the existing16-unit grid and
/// bounded route grammar; integer Z has no grid or fixed-source height policy.
/// No collision, source eligibility, native ownership or runtime proof is issued.
/// </summary>
public sealed class NativeFreshWizardRouteIntent : NativeFreshPlacementIntent
{
    private NativeFreshWizardRouteIntent(string editorId, NativeWizardPlacementIntent geometry) : base(editorId)
    {
        OriginX = geometry.OriginX; OriginY = geometry.OriginY; OriginZ = geometry.OriginZ;
        EndX = geometry.EndX; EndY = geometry.EndY;
    }
    public override string Tag => "wizard-route";
    public int OriginX { get; }
    public int OriginY { get; }
    public int OriginZ { get; }
    public int EndX { get; }
    public int EndY { get; }

    public static NativeFreshWizardRouteIntent Create(string editorId, int originX, int originY, int originZ, int endX, int endY)
    {
        var geometry = new NativeWizardPlacementIntent(originX, originY, originZ, endX, endY);
        NativeWizardPlacementGeometry.Validate(geometry);
        return new(editorId, geometry);
    }
}
