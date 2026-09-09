using Spyro.Editor.Core.Editing;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeFreshWizardAuthoringSmoke")]

namespace Spyro.Editor.Core.Exporting;

// Closed internal producer selection, never a caller-supplied export policy.
internal enum NativeFreshWizardResourceProfile { FixedA, AuthoredSameLayout }

/// <summary>
/// Admits authored surfaces only while the existing native data/copy geometry
/// remains identical. This is byte grammar and actual-input binding, not spawn
/// clearance, arena capacity, source issuance or permission to publish a ROM.
/// </summary>
internal static class NativeFreshWizardAuthoringShape
{
    internal const string ProfileId = "fresh-authored-untextured-same-layout-wizard33-T6-research-v1";
    private static readonly byte[] FixedSurface = NativeId65AuthoredSurfaceLayout.Encode(
        new(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080));

    internal static void RequireCompilation(NativeId65FreshDocumentCompilation compilation,
        NativeFreshWizardResourceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        if (profile == NativeFreshWizardResourceProfile.FixedA)
        {
            NativeId65FreshWizardResearchResourceSource.RequireFixedIntent(compilation.Intent);
            return;
        }
        if (profile != NativeFreshWizardResourceProfile.AuthoredSameLayout)
            throw new InvalidDataException("Unknown closed Wizard resource producer.");
        var fresh = compilation.FreshResourceAncestor;
        var chain = fresh.ResourceChainAncestor;
        byte[] data = chain.CopyDataComponents();
        RequireSurfaceComplement(data, 0x8C8, "821d25d4c012aa180d369f755d82a780dbcf244b4e53c6c4bcf349da533dce65");
        var layout = NativeId65NormalDataLayout.DecodeUntextured(data);
        if (layout.SurfaceByteLength != 0x144 || !chain.Resources.Surface.SourceCertified ||
            !data.AsSpan(8, 0x144).SequenceEqual(chain.Resources.Surface.CopyBundle()) ||
            !compilation.CopyCanonicalDocument().AsSpan().SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(compilation.Intent)))
            throw new InvalidDataException("Authored Wizard resources lost actual canonical surface ownership or fixed copy geometry.");
        chain.RequireExactPairedBytes(data, fresh.CopyScene().AsSpan(0x88, 0x44));
        var decoded = NativeId65FreshSceneLayout.DecodeCanonical(fresh.CopyScene());
        var intent = compilation.Intent;
        if (decoded.NativeNominalCapacity != 16 ||
            decoded.Spawn != new NativeId65SceneSpawn(intent.SpawnX, intent.SpawnY, intent.SpawnZ, intent.Heading))
            throw new InvalidDataException("Authored Wizard base must retain its actual canonical P0/N16 spawn.");
    }

    // Normalize ONLY a strictly decoded canonical surface for complement
    // comparison. Never return normalized bytes or silently replace authorship.
    internal static void RequireSurfaceComplement(ReadOnlySpan<byte> data, int length, string fixedHash)
    {
        if (data.Length != length || length is not (0x8C8 or 0x8FC) || FixedSurface.Length != 0x144)
            throw new InvalidDataException("Authored Wizard data requires the existing fixed-size native surface/copy layout.");
        _ = NativeId65AuthoredSurfaceLayout.DecodeAndValidate(data.Slice(8, 0x144));
        byte[] normalized = data.ToArray();
        FixedSurface.CopyTo(normalized, 8);
        if (NativeId65FreshWizardResearchResourceSource.Hash(normalized) != fixedHash)
            throw new InvalidDataException("Authored Wizard data changed a non-surface owner or native layout complement.");
    }

    internal static NativeWizardPlacementIntent RequireDocument(NativeFreshLevelDocumentIntent document,
        NativeId65FreshDocumentCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(compilation);
        if (document.SchemaVersion != 1 || document.FloorMaterials != null)
            throw new InvalidDataException("This research scene currently requires schema 1 without floor material intent.");
        if (!ReferenceEquals(document.Base, compilation.Intent) || document.Placements.Count != 1 ||
            document.Placements[0] is not NativeFreshWizardRouteIntent wizard)
            throw new InvalidDataException("This research scene requires the actual compiled Base and exactly one authored Wizard route.");
        var placement = new NativeWizardPlacementIntent(wizard.OriginX, wizard.OriginY, wizard.OriginZ, wizard.EndX, wizard.EndY);
        RequirePlacement(placement, document.Base);
        return placement;
    }

    // New closed schema2 arm only. Neutral Store accepts more than this recipe.
    internal static NativeWizardPlacementIntent RequireMaterialDocument(NativeFreshLevelDocumentIntent document,
        NativeId65FreshDocumentCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(document);ArgumentNullException.ThrowIfNull(compilation);
        if (!ReferenceEquals(document.Base, compilation.Intent))
            throw new InvalidDataException("Material-authored Scene requires the actual compiled Base, not equal canonical bytes.");
        if (document.SchemaVersion != 2 || document.FloorMaterials == null || document.Placements.Count != 1 ||
            document.Placements[0] is not NativeFreshWizardRouteIntent wizard)
            throw new InvalidDataException("Material-authored Scene requires schema2 and exactly one actual Wizard route.");
        if (document.FloorMaterials.Triangle0 is not NativeFreshRetailTerrainMaterialIntent first ||
            document.FloorMaterials.Triangle1 is not NativeFreshRetailTerrainMaterialIntent second ||
            first.SourceVariant != NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant ||
            second.SourceVariant != NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant ||
            first.TextureId != 1 || second.TextureId != 1 ||
            (first.SourceWadEntry, second.SourceWadEntry) is not ((14,70) or (70,14) or (14,14)))
            throw new InvalidDataException("Material-authored Scene supports only the three explicit T1 retail pairs14/70,70/14,14/14.");
        var placement = new NativeWizardPlacementIntent(wizard.OriginX,wizard.OriginY,wizard.OriginZ,wizard.EndX,wizard.EndY);
        var b = document.Base;
        if (b.FloorX != 8448 || b.FloorY != 8448 || b.FloorZ != 8704 || b.FloorWidth != 2048 || b.FloorDepth != 2048 ||
            b.SpawnX != 8960 || b.SpawnY != 8960 || b.SpawnZ != 9216 || b.Heading != 64 || b.MusicTrackId != 19 ||
            b.FloorColor != new Spyro.Editor.Core.Primitives.ColorRgba(80,128,112) ||
            b.SkyColor != new Spyro.Editor.Core.Primitives.ColorRgba(128,96,64) ||
            placement != new NativeWizardPlacementIntent(9984,9984,8704,9472,9984))
            throw new InvalidDataException("Material-authored Scene currently supports only the exact separatedB floor/route/track19.");
        RequirePlacement(placement,b);
        return placement;
    }

    internal static void RequirePlacement(NativeWizardPlacementIntent placement, NativeId65FreshDocumentIntent floor)
    {
        NativeWizardPlacementGeometry.Validate(placement);
        if (placement.OriginX <= floor.FloorX || placement.OriginX >= floor.FloorX + floor.FloorWidth ||
            placement.OriginY <= floor.FloorY || placement.OriginY >= floor.FloorY + floor.FloorDepth ||
            placement.EndX <= floor.FloorX || placement.EndX >= floor.FloorX + floor.FloorWidth ||
            placement.EndY <= floor.FloorY || placement.EndY >= floor.FloorY + floor.FloorDepth ||
            placement.OriginZ < floor.FloorZ || placement.OriginZ > floor.SpawnZ)
            throw new InvalidDataException("Authored Wizard route must remain geometrically inside the actual floor and floor..floor+512 height.");
        // Deliberately not a body-radius, trajectory or spawn certificate.
    }
}
