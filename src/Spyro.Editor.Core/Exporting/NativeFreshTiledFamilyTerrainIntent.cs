using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Neutral complete-document preflight for the four1536-square-tile R03 union.
/// Stable IDs remain authored identity, not hardcoded native admission values.
/// Material descriptors and face slots are unresolved intent, not source assets,
/// allocated records, a family resource revision or permission to emit a Body.
/// Existing saved/P0 source gates do not consume this privately created value.
/// </summary>
internal sealed class NativeFreshTiledFamilyTerrainIntent
{
    private readonly byte[] _canonical,_surface,_boundSurface;

    private NativeFreshTiledFamilyTerrainIntent(NativeFreshLevelDocumentIntent document,
        NativeFreshTiledLevelContent content)
    {
        DocumentAncestor=document;
        _canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(document);
        PlacementIds=Array.AsReadOnly(document.Placements.Select(p=>p.EditorId).ToArray());
        List<NativeFreshRetailTerrainMaterialIntent> selections=[];
        List<int> bindings=[];
        foreach(var tile in content.Tiles)
        foreach(var material in new[]{tile.Materials.Triangle0,tile.Materials.Triangle1})
        {
            if(material is not NativeFreshRetailTerrainMaterialIntent retail||
                retail.SourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||
                retail.SourceWadEntry is not (14 or 70)||retail.TextureId!=1)
                throw new InvalidDataException("Neutral tiled-family recipe requires only the exact14/70 T1 descriptors; Base or another source is not substituted.");
            int slot=selections.FindIndex(s=>s.SourceVariant==retail.SourceVariant&&
                s.SourceWadEntry==retail.SourceWadEntry&&s.TextureId==retail.TextureId);
            if(slot<0){slot=selections.Count;selections.Add(retail);}
            bindings.Add(slot);
        }
        if(selections.Count!=2||selections[0].SourceWadEntry!=14||selections[1].SourceWadEntry!=70||
            !bindings.SequenceEqual(new[]{0,0,0,1,0,1,1,1}))
            throw new InvalidDataException("Neutral tiled-family materials must preserve the whole-floor two-region diagonal and every ordered face.");
        MaterialSelections=Array.AsReadOnly(selections.ToArray());
        OrderedFaceMaterials=Array.AsReadOnly(bindings.ToArray());

        var snapshot=NativeFlatTileSurfaceLayout.Compile(content.Tiles.Select(t=>t.ToNativeTile()),
            NativeFreshFlatTileIntent.ColorWord(content.SkyColor));
        _surface=snapshot.CopyBundle();
        var decoded=NativeFlatTileSurfaceLayout.DecodeAndValidate(_surface);
        if(!decoded.Tiles.SequenceEqual(content.Tiles.Select(t=>t.ToNativeTile()))||
            decoded.SkyRgb!=NativeFreshFlatTileIntent.ColorWord(content.SkyColor))
            throw new InvalidDataException("Neutral tiled-family complete surface readback differs from its actual ordered authored geometry.");
        Components=Array.AsReadOnly(decoded.Components.ToArray());
        _boundSurface=_surface.ToArray();
        var environment=Components.Single(c=>c.Kind==NativeId65SurfaceComponentKind.Environment);
        List<int> fields=[];
        for(int tile=0;tile<content.Tiles.Count;tile++)
        {
            int start=checked(environment.BlockOffset+4+Word(_surface,environment.BlockOffset+8+4*tile));
            int end=tile+1==content.Tiles.Count?environment.BlockOffset+environment.ByteLength:
                checked(environment.BlockOffset+4+Word(_surface,environment.BlockOffset+8+4*(tile+1)));
            var header=Id65EnvironmentSectorHeaderContract.ReadPointerSpan(_surface.AsSpan(start,end-start),end-start,
                requireExactEnd:false,"neutral tiled-family sector");
            if(header.HighDetailFaceCount!=2||header.PointerGapByteLength!=4)
                throw new InvalidDataException("Neutral tiled-family sector lost its exact two faces and owned lookahead guard.");
            int faces=start+Id65EnvironmentSectorHeaderContract.HeaderByteLength+
                4*header.HighDetailVertexWordOffset+4*header.HighDetailVertexCount+8*header.HighDetailColorCount;
            for(int face=0;face<2;face++)
            {
                int at=faces+face*16+8;
                if(Word(_surface,at)!=0xFF||Word(_surface,at+4)!=0x80)
                    throw new InvalidDataException("Neutral tiled-family binding requires its canonical FF material and complete primitive flag preimage.");
                fields.Add(at);
                BinaryPrimitives.WriteInt32LittleEndian(_boundSurface.AsSpan(at),bindings[tile*2+face]);
            }
        }
        if(fields.Count!=8||fields.Distinct().Count()!=8)
            throw new InvalidDataException("Neutral tiled-family binding must cover eight disjoint complete words.");
        // Normalize only the eight declared material words for exact readback;
        // this comparison cannot promote caller bytes or change the document.
        byte[] inverse=_boundSurface.ToArray();
        foreach(int at in fields)BinaryPrimitives.WriteInt32LittleEndian(inverse.AsSpan(at),0xFF);
        if(!inverse.AsSpan().SequenceEqual(_surface))
            throw new InvalidDataException("Neutral tiled-family bindings changed their complete geometry/flag/guard complement.");
        CanonicalDocumentSha256=Hash(_canonical);SurfaceSha256=Hash(_surface);BoundSurfaceSha256=Hash(_boundSurface);
    }

    internal NativeFreshLevelDocumentIntent DocumentAncestor{get;}
    internal IReadOnlyList<string> PlacementIds{get;}
    internal IReadOnlyList<NativeFreshRetailTerrainMaterialIntent> MaterialSelections{get;}
    internal IReadOnlyList<int> OrderedFaceMaterials{get;}
    internal IReadOnlyList<NativeId65SurfaceComponent> Components{get;}
    internal string CanonicalDocumentSha256{get;}
    internal string SurfaceSha256{get;}
    internal string BoundSurfaceSha256{get;}
    internal bool SourceCertified=>false;
    internal bool BodyIssued=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal byte[] CopyCanonicalDocument()=>_canonical.ToArray();
    internal byte[] CopySurface()=>_surface.ToArray();
    internal byte[] CopyBoundSurface()=>_boundSurface.ToArray();
    internal void RequireAncestry(NativeFreshLevelDocumentIntent document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if(!ReferenceEquals(document,DocumentAncestor)||
            !_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(document)))
            throw new InvalidDataException("Neutral tiled-family intent retains its actual whole document, not an equal canonical replacement.");
    }
    internal void RequireExactSurface(ReadOnlySpan<byte> surface)
    {if(!surface.SequenceEqual(_surface))throw new InvalidDataException("Complete neutral tiled-family surface changed.");}
    internal void RequireExactBoundSurface(ReadOnlySpan<byte> surface)
    {if(!surface.SequenceEqual(_boundSurface))throw new InvalidDataException("Complete neutral tiled-family bound surface or complement changed.");}

    internal static NativeFreshTiledFamilyTerrainIntent Create(NativeFreshLevelDocumentIntent document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(document,RequireDocument(document));
    }

    private static NativeFreshTiledLevelContent RequireDocument(NativeFreshLevelDocumentIntent document)
    {
        if(document.SchemaVersion!=5||document.TiledContent is not { } content||content.Tiles.Count!=4||
            document.Placements.Count!=3||document.Placements[0] is not NativeFreshWizardRouteIntent wizard||
            document.Placements[1] is not NativeFreshMobyPlacementIntent grass||
            document.Placements[2] is not NativeFreshLockedChestFamilyPlacementIntent family)
            throw new InvalidDataException("Neutral tiled-family preflight requires the complete schema5 four-tile document and ordered Wizard/Grass/chest family.");
        (int X,int Y)[] positions=[(8448,8448),(9984,8448),(8448,9984),(9984,9984)];
        for(int i=0;i<positions.Length;i++)
        {
            var tile=content.Tiles[i];
            if(tile.X!=positions[i].X||tile.Y!=positions[i].Y||tile.Z!=8704||tile.Width!=1536||tile.Depth!=1536||
                tile.FloorColor!=new ColorRgba(80,128,112))
                throw new InvalidDataException("Neutral tiled-family preflight requires the exact ordered four1536-square tiles covering the complete R03 rectangle.");
        }
        if(content.SpawnX!=8960||content.SpawnY!=8960||content.SpawnZ!=9216||content.Heading!=64||
            content.MusicTrackId!=19||content.SkyColor!=new ColorRgba(128,96,64)||
            wizard.OriginX!=9984||wizard.OriginY!=9984||wizard.OriginZ!=8704||wizard.EndX!=9472||wizard.EndY!=9984||
            grass.Source.SourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||
            grass.Source.SourceWadEntry!=10||grass.Source.SourceTrueIndex!=121||grass.Source.ExpectedNativeClass!=0x1F5||
            grass.X!=9152||grass.Y!=9152||grass.Z!=8704||grass.NativeYawByte!=64||
            family.Source.SourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||
            family.Source.DefinitionId!=NativeFreshLockedChestFamilyReference.SupportedDefinitionId||
            family.KeyX!=9728||family.KeyY!=9120||family.KeyZ!=8704||
            family.ChestX!=10864||family.ChestY!=9888||family.ChestZ!=8704||
            family.RewardDestinations is not {Values.Count:1} destinations||
            destinations.Values[0] is not {Role:NativeFreshChestRewardRole.Reward2,X:10454,Y:10503,Z:9216})
            throw new InvalidDataException("Neutral tiled-family preflight requires all exact R03 spawn/display/music/actor/source/anchor/Reward2 values; no intent is discarded.");
        // The immutable aggregate already validates all stable IDs and union
        // containment. Do not hardcode fixture IDs or rebuild a legacy/P0 document.
        return content;
    }
    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
