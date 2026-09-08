using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Neutral authoring aggregate: one unchanged immutable floor intent and an
/// ordered closed placement list, with an explicit schema2 floor-material pair.
/// Counts and editor IDs are document ownership,
/// never native capacity, save bits, source authority or publication approval.
/// </summary>
public sealed class NativeFreshLevelDocumentIntent
{
    public const int MaximumPlacements = 128;
    private NativeFreshLevelDocumentIntent(NativeId65FreshDocumentIntent @base,
        IReadOnlyList<NativeFreshPlacementIntent> placements, NativeFreshFloorMaterialsIntent? materials)
    { Base = @base; Placements = placements; FloorMaterials = materials; }

    public NativeId65FreshDocumentIntent Base { get; }
    public IReadOnlyList<NativeFreshPlacementIntent> Placements { get; }
    public int SchemaVersion => FloorMaterials == null ? 1 : 2;
    public NativeFreshFloorMaterialsIntent? FloorMaterials { get; }

    /// <summary>Original schema1 factory; never infers or upgrades material intent.</summary>
    public static NativeFreshLevelDocumentIntent Create(NativeId65FreshDocumentIntent @base,
        IEnumerable<NativeFreshPlacementIntent> placements) => CreateContent(@base, placements, null);

    /// <summary>Validate/copy all content while retaining this document's version and material choices.</summary>
    public NativeFreshLevelDocumentIntent WithContent(NativeId65FreshDocumentIntent @base,
        IEnumerable<NativeFreshPlacementIntent> placements) => CreateContent(@base, placements, FloorMaterials);

    /// <summary>Explicit schema2 edit. Clearing both slots to base color does not downgrade.</summary>
    public NativeFreshLevelDocumentIntent WithFloorMaterials(NativeFreshFloorMaterialsIntent materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        return new(Base, Placements, materials);
    }

    private static NativeFreshLevelDocumentIntent CreateContent(NativeId65FreshDocumentIntent @base,
        IEnumerable<NativeFreshPlacementIntent> placements, NativeFreshFloorMaterialsIntent? materials)
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
            if (placement is not NativeFreshWizardRouteIntent wizard)
                throw new InvalidDataException("Fresh level requires a non-null supported closed placement variant.");
            if (!ids.Add(wizard.EditorId))
                throw new InvalidDataException("Fresh level editor placement IDs must be unique without regeneration or deduplication.");
            if (wizard.OriginX <= @base.FloorX || wizard.OriginX >= maxX ||
                wizard.EndX <= @base.FloorX || wizard.EndX >= maxX ||
                wizard.OriginY <= @base.FloorY || wizard.OriginY >= maxY ||
                wizard.EndY <= @base.FloorY || wizard.EndY >= maxY)
                throw new InvalidDataException("Both authored route endpoints must be strictly inside the base rectangle; no body or corridor clearance is implied.");
            copy.Add(wizard);
        }
        return new(@base, copy.AsReadOnly(), materials);
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
