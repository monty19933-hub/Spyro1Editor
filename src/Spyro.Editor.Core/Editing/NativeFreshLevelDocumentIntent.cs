using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Neutral authoring aggregate: unchanged legacy schemas1..3, explicit schema4
/// ordered tiles, or explicit schema5 family intent with either content variant.
/// The bounded closed placement list is independent of native expansion. Only the active content
/// variant owns geometry, spawn and common settings.
/// Counts and editor IDs are document ownership,
/// never native capacity, save bits, source authority or publication approval.
/// </summary>
public sealed class NativeFreshLevelDocumentIntent
{
    public const int MaximumPlacements = 128;
    private NativeFreshLevelDocumentIntent(NativeFreshLevelContent content,
        IReadOnlyList<NativeFreshPlacementIntent> placements, int schemaVersion)
    { Content = content; Placements = placements; SchemaVersion = schemaVersion; }

    public NativeFreshLevelContent Content { get; }
    public NativeFreshLegacyLevelContent? LegacyContent => Content as NativeFreshLegacyLevelContent;
    public NativeFreshTiledLevelContent? TiledContent => Content as NativeFreshTiledLevelContent;
    public NativeId65FreshDocumentIntent Base => LegacyContent?.Base ??
        throw new InvalidDataException("Tiled content has no legacy Base; use its actual tiles and common settings.");
    public IReadOnlyList<NativeFreshPlacementIntent> Placements { get; }
    public int SchemaVersion { get; }
    public NativeFreshFloorMaterialsIntent? FloorMaterials => LegacyContent?.FloorMaterials;
    public int SpawnX => Content.SpawnX;
    public int SpawnY => Content.SpawnY;
    public int SpawnZ => Content.SpawnZ;
    public int Heading => Content.Heading;
    public ColorRgba SkyColor => Content.SkyColor;
    public int MusicTrackId => Content.MusicTrackId;

    /// <summary>Original schema1 factory; never infers or upgrades material intent.</summary>
    public static NativeFreshLevelDocumentIntent Create(NativeId65FreshDocumentIntent @base,
        IEnumerable<NativeFreshPlacementIntent> placements)
    {
        ArgumentNullException.ThrowIfNull(@base);
        return CreateContent(new NativeFreshLegacyLevelContent(@base, null), placements, 1);
    }

    public static NativeFreshLevelDocumentIntent CreateTiled(NativeFreshTiledLevelContent content,
        IEnumerable<NativeFreshPlacementIntent> placements) => CreateContent(content, placements, 4);

    /// <summary>Validate/copy all content while retaining this document's version and material choices.</summary>
    public NativeFreshLevelDocumentIntent WithContent(NativeId65FreshDocumentIntent @base,
        IEnumerable<NativeFreshPlacementIntent> placements)
    {
        ArgumentNullException.ThrowIfNull(@base);
        if (LegacyContent == null) throw new InvalidDataException("Legacy edits cannot replace or downgrade tiled content.");
        return CreateContent(new NativeFreshLegacyLevelContent(@base, FloorMaterials), placements, SchemaVersion);
    }

    public NativeFreshLevelDocumentIntent WithPlacements(IEnumerable<NativeFreshPlacementIntent> placements) =>
        CreateContent(Content, placements, SchemaVersion);

    public NativeFreshLevelDocumentIntent WithTiledContent(NativeFreshTiledLevelContent content) =>
        WithTiledContent(content, Placements);

    /// <summary>Validate the final tiled content and placement list together, without requiring either intermediate aggregate to be valid.</summary>
    public NativeFreshLevelDocumentIntent WithTiledContent(NativeFreshTiledLevelContent content,
        IEnumerable<NativeFreshPlacementIntent> placements)
    {
        if (TiledContent == null) throw new InvalidDataException("Use an explicit tiled upgrade or factory before editing tiled content.");
        return CreateContent(content, placements, SchemaVersion == 5 ? 5 : 4);
    }

    public NativeFreshLevelDocumentIntent UpgradeToTiled(string tileEditorId)
    {
        var old = LegacyContent ?? throw new InvalidDataException("This document already owns tiled content.");
        var b = old.Base;
        var materials = old.FloorMaterials ?? NativeFreshFloorMaterialsIntent.Create(
            NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.BaseFloorColor());
        var tile = NativeFreshFlatTileIntent.Create(tileEditorId, b.FloorX, b.FloorY, b.FloorZ,
            b.FloorWidth, b.FloorDepth, b.FloorColor, materials);
        return CreateContent(NativeFreshTiledLevelContent.Create([tile], b.SpawnX, b.SpawnY, b.SpawnZ,
            b.Heading, b.SkyColor, b.MusicTrackId), Placements, SchemaVersion == 5 ? 5 : 4);
    }

    /// <summary>Explicit material edit, upgrading schema1 to2 but retaining schema3. Clearing both slots does not downgrade.</summary>
    public NativeFreshLevelDocumentIntent WithFloorMaterials(NativeFreshFloorMaterialsIntent materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        return new(new NativeFreshLegacyLevelContent(Base, materials), Placements, Math.Max(SchemaVersion, 2));
    }

    /// <summary>Explicit schema3 upgrade, retaining all existing content and nullable material intent. Removal never downgrades it.</summary>
    public NativeFreshLevelDocumentIntent UpgradeForNativeMobys() =>
        SchemaVersion >= 3 ? this : new(Content, Placements, 3);

    /// <summary>Explicit schema5 upgrade with either active content variant. Family removal never downgrades it.</summary>
    public NativeFreshLevelDocumentIntent UpgradeForFamilies() =>
        SchemaVersion == 5 ? this : new(Content, Placements, 5);

    private static NativeFreshLevelDocumentIntent CreateContent(NativeFreshLevelContent content,
        IEnumerable<NativeFreshPlacementIntent> placements, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(placements);
        var copy = new List<NativeFreshPlacementIntent>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placement in placements)
        {
            // Bound enumeration itself, not just a possibly unbounded ToArray.
            if (copy.Count == MaximumPlacements)
                throw new InvalidDataException("Fresh level authoring permits at most128 placements; this is not native capacity.");
            if (placement is not NativeFreshWizardRouteIntent &&
                !(schemaVersion >= 3 && placement is NativeFreshMobyPlacementIntent) &&
                !(schemaVersion == 5 && placement is NativeFreshLockedChestFamilyPlacementIntent))
                throw new InvalidDataException("Fresh level requires a non-null supported closed placement variant.");
            if (!ids.Add(placement.EditorId))
                throw new InvalidDataException("Fresh level editor placement IDs must be unique without regeneration or deduplication.");
            if (placement is NativeFreshWizardRouteIntent wizard &&
                (!content.ContainsInteriorXY(wizard.OriginX, wizard.OriginY) ||
                 !content.ContainsInteriorXY(wizard.EndX, wizard.EndY)))
                throw new InvalidDataException("Both authored route endpoints must be strictly inside the active surface union; no ground, body or corridor clearance is implied.");
            if (placement is NativeFreshMobyPlacementIntent moby &&
                !content.ContainsInteriorXY(moby.X, moby.Y))
                throw new InvalidDataException("Authored native Moby XY must be strictly inside the active surface union; no body, ground or collision clearance is implied.");
            if (placement is NativeFreshLockedChestFamilyPlacementIntent family &&
                (!content.ContainsInteriorXY(family.KeyX, family.KeyY) || !content.ContainsInteriorXY(family.ChestX, family.ChestY)))
                throw new InvalidDataException("Both authored family anchors must be strictly inside the active surface union; reward destinations, footprints and trajectories are not approved by this check.");
            copy.Add(placement);
        }
        return new(content, copy.AsReadOnly(), schemaVersion);
    }
}

/// <summary>Closed neutral placement union; external subclasses cannot add unvalidated payloads.</summary>
public abstract class NativeFreshPlacementIntent
{
    private protected NativeFreshPlacementIntent(string editorId)
    {
        NativeFreshEditorId.Require(editorId);
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
