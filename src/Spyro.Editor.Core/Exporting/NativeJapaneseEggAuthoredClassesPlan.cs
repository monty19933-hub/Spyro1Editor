using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggAuthoredClassesSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// An authored class binding, NOT NativeMobyModelPackageBinding with a forged
/// donor class. The F5 donor certificate, RGB edit, reserved destination identity
/// and actual final allocation are separately retained. No public constructor.
/// </summary>
public sealed class NativeJapaneseEggAuthoredModelBinding
{
    internal NativeJapaneseEggAuthoredModelBinding(NativeJapaneseEggAuthoredClassReservation reservation,
        NativeJapaneseEggRgbEditPlan edit,NativeModelTailBatchEntry package,NativeJapaneseEggRgbVariantOwner projection,
        NativeJapaneseEggRgbVariantRow row,NativeByteExtent extent)
    {
        reservation.RequireSelected(package.Input.ActorId);
        if(edit.SourceAncestryCertified!=reservation.SourceCertified||extent.Offset!=package.PackageOffset||
            extent.ByteLength!=package.ByteLength||projection.PackageDataOffset!=package.PackageOffset||
            !package.Input.CopyBytes().AsSpan().SequenceEqual(edit.CopyPackage()))
            throw new InvalidDataException("Authored binding lost its separate donor/edit/reservation/allocation ancestry.");
        Edit=edit;AuthoredDestinationClass=package.Input.ActorId;RegistrationSlot=package.Slot;
        RootFieldOffset=package.RootFieldOffset;ClassFieldOffset=package.ClassFieldOffset;
        PackageDataOffset=package.PackageOffset;ByteLength=package.ByteLength;ProjectedRoot=projection.ProjectedLoadedRoot;
        RowSceneOffset=row.RowSceneOffset;ProjectedRowAddress=row.ProjectedRowAddress;ProjectedPrivateAddress=row.ProjectedPrivateAddress;
        RowId=row.Id;LoaderFields=projection.LoaderFields;Extent=extent;
    }
    public ushort SourceActorId=>0xF5;
    public ushort AuthoredDestinationClass{get;}
    public NativeJapaneseEggRgbEditPlan Edit{get;}
    public string PaletteOrigin=>Edit.Origin==NativeJapaneseEggPaletteOrigin.Authored?"authored-rgb":"native-original";
    public string RowId{get;}
    public int RegistrationSlot{get;}
    public int RootFieldOffset{get;}
    public int ClassFieldOffset{get;}
    public int PackageDataOffset{get;}
    public int ByteLength{get;}
    public uint ProjectedRoot{get;}
    public int RowSceneOffset{get;}
    public uint ProjectedRowAddress{get;}
    public uint ProjectedPrivateAddress{get;}
    public IReadOnlyList<NativeJapaneseEggLoaderAddress> LoaderFields{get;}
    internal NativeByteExtent Extent{get;}
}

/// <summary>
/// Functioning detached exact-Artisans registration/scene/dispatch composition
/// for two source-backed RGB-only authored classes. Native load/init/animation/
/// render table consumers are unchanged. Generic effects, dynamic constructors,
/// pickup/persistence, full peak RAM, pixels and publication are not approved.
/// No global renderer hook, per-frame root write or new disc writer is installed.
/// </summary>
public sealed class NativeJapaneseEggAuthoredClassesPlan
{
    private readonly byte[] _before,_after,_overlayBefore,_overlay;
    private readonly NativeModelTailBatch _batch;
    private NativeJapaneseEggAuthoredClassesPlan(NativeJapaneseEggAuthoredClassReservation reservation,
        NativeJapaneseEggRgbVariantTailPlan projection,NativeJapaneseEggRgbEditPlan first,NativeJapaneseEggRgbEditPlan second,bool certified)
    {
        if(reservation.SourceCertified!=certified||projection.SourceCertified!=certified)
            throw new InvalidDataException("Authored composition cannot upgrade or downgrade source authority.");
        Reservation=reservation;Projection=projection;Integration=projection.Integration;SourceCertified=certified;
        _before=Integration.CopyLevelData();_overlayBefore=Integration.CopyOverlay();
        var edits=new[]{first,second};
        var requests=edits.Select((e,i)=>new NativeModelTailPackage(reservation.AuthoredDestinationClasses[i],
            "authored-f5-rgb-"+reservation.AuthoredDestinationClasses[i].ToString("x"),e.CopyPackage())).ToArray();
        _batch=NativeModelTailBatch.Compile(_before,requests,reservation.SharedClasses);_after=_batch.CopyFinalBody();
        byte[] projected=projection.CopyLevelData();
        if(_batch.ModelGrowthBytes!=projection.GrowthBytes||_after.Length!=projected.Length||
            !_after.AsSpan(0x800).SequenceEqual(projected.AsSpan(0x800)))
            throw new InvalidDataException("Native registered model-tail geometry differs from the existing closed RGB-only allocation.");
        FinalDomain=new(NativeAddressKind.LevelData,"artisans-authored-f5:"+reservation.PlanSha256+":"+projection.PlanSha256,NativeAddressRevision.Final,_after.Length);
        BeforeDomain=new(NativeAddressKind.LevelData,"current-artisans-f5:"+Integration.LevelDataSha256,NativeAddressRevision.Original,_before.Length);
        int finalScene=Int(_after,0x18);List<NativeJapaneseEggAuthoredModelBinding> bindings=[];
        for(int i=0;i<2;i++)
        {
            var entry=_batch.Packages[i];var row=projection.Rows[i];var owner=projection.Variants[i];
            if(Half(_after,finalScene+row.RowSceneOffset+0x36)!=0xF5)
                throw new InvalidDataException("Authored class binding requires the exact appended F5 row preimage.");
            BinaryPrimitives.WriteUInt16LittleEndian(_after.AsSpan(finalScene+row.RowSceneOffset+0x36),entry.Input.ActorId);
            bindings.Add(new(reservation,edits[i],entry,owner,row,new(FinalDomain,entry.PackageOffset,entry.ByteLength)));
        }
        Bindings=bindings.AsReadOnly();
        // Recreate the existing owned E800 layout with the SAME native fragment
        // addresses, replacing only its explicitly authored F5 guard block.
        var oldBlocks=Integration.Layout.Blocks.Where(b=>!b.IsPadding).ToArray();
        var sizes=oldBlocks.Select(b=>new NativeOwnedOverlayBlockSize(b.Id,b.Id=="f5-only-dispatch"?0x34:(int)b.Owner.ByteLength));
        OverlayLayout=NativeOwnedOverlayAppendLayout.Create("authored-f5-artisans-guard:"+reservation.PlanSha256,
            Integration.CopyOriginalOverlay(),0x8007AA38,0xE800,sizes,"alignment");
        if(OverlayLayout.CopyAnchor!=Integration.FinalCopyBufferAddress)
            throw new InvalidDataException("Authored dispatcher cannot enlarge the existing copy-buffer/overlay allocation.");
        List<NativeOwnedOverlayBlock> blocks=[];
        foreach(var old in oldBlocks)
        {
            var target=OverlayLayout.Blocks.Single(b=>b.Id==old.Id).Owner;
            if(target.Offset!=old.Owner.Offset)throw new InvalidDataException("Existing native visual fragment PCs cannot move without relinking.");
            if(old.Id=="f5-only-dispatch")
            {
                GuardPc=0x8007AA38+(uint)target.Offset;
                if(GuardPc!=Integration.GuardPc)throw new InvalidDataException("Existing guard entry owner changed.");
                // Every compare restores v1 in its branch delay slot. Neither
                // scratch registers nor SP/HI/LO/GTE are touched at this gate.
                blocks.Add(new(old.Id,target,Words(0x2463FF0B,0x10600009,0x246300F5,
                    0x2463FE17,0x10600006,0x246301E9,0x2463FE0E,0x10600003,0x246301F2,
                    Jump(0x80085780),0,Jump(Integration.VisualAncestor.EntryPc),0)));
            }
            else blocks.Add(new(old.Id,target,_overlayBefore.AsSpan((int)old.Owner.Offset,(int)old.Owner.ByteLength).ToArray()));
        }
        _overlay=OverlayLayout.Materialize(blocks,blocks.Select(b=>new NativeOwnedOverlayEntry(b.Id,b.Id,new(OverlayLayout.Domain,b.Owner.Offset,4))),[]);
        Put(_overlay,0x319C,Jump(GuardPc));Put(_overlay,0x31A0,0);
        // High authored IDs reach a different original default leaf from F5.
        // Its complete J/NOP beforeimage is inside the source-pinned dispatch
        // window; sharing a continuation does not imply sharing a leaf.
        if(Int(_overlayBefore,0x32C4)!=0x080215E0||Int(_overlayBefore,0x32C8)!=0)
            throw new InvalidDataException("High-class native default leaf changed before authored interception.");
        Put(_overlay,0x32C4,Jump(GuardPc));
        if(_overlay.Length!=_overlayBefore.Length)throw new InvalidDataException("The existing overlay allocation is a strict ceiling.");
        int guardOffset=(int)(GuardPc-0x8007AA38);
        if(!_overlay.AsSpan(0,0x32C4).SequenceEqual(_overlayBefore.AsSpan(0,0x32C4))||
            !_overlay.AsSpan(0x32C8,guardOffset-0x32C8).SequenceEqual(_overlayBefore.AsSpan(0x32C8,guardOffset-0x32C8))||
            !_overlay.AsSpan(guardOffset+GuardByteLength).SequenceEqual(_overlayBefore.AsSpan(guardOffset+GuardByteLength)))
            throw new InvalidDataException("Authored aliases changed another overlay owner.");
        FinalDataSha256=Hash(_after);FinalOverlaySha256=Hash(_overlay);
        Graph=CompileGraph(edits);
        PlanSha256=Hash(Encoding.UTF8.GetBytes("artisans-authored-f5-rgb-v1\n"+reservation.PlanSha256+"\n"+projection.PlanSha256+"\n"+
            FinalDataSha256+"\n"+FinalOverlaySha256+"\n"+Graph.Fingerprint+"\n"));
        _=RevertDataExact(_after);_=RevertOverlayExact(_overlay);
    }
    private NativeMobyDependencyClosurePlan CompileGraph(NativeJapaneseEggRgbEditPlan[] edits)
    {
        var beforeRead=new NativeMobyDependencyRead("complete-current",new(BeforeDomain,0,_before.Length),Hash(_before));
        List<NativeMobyDependencyPreservation> preserved=[];
        // Preserve complete shifted subfiles except the TWO owned class words.
        for(int index=0;index<5;index++)
        {
            int oldOffset=Int(_before,index*8),length=Int(_before,index*8+4),newOffset=Int(_after,index*8);
            if(index!=3)preserved.Add(new(beforeRead.Id,oldOffset,new(FinalDomain,newOffset,length)));
            else
            {
                int cursor=0;
                foreach(int field in Bindings.Select(b=>b.RowSceneOffset+0x36).Order())
                {if(field>cursor)preserved.Add(new(beforeRead.Id,oldOffset+cursor,new(FinalDomain,newOffset+cursor,field-cursor)));cursor=field+2;}
                if(cursor<length)preserved.Add(new(beforeRead.Id,oldOffset+cursor,new(FinalDomain,newOffset+cursor,length-cursor)));
            }
        }
        List<NativeMobyDependencyNode> nodes=[new("current",NativeMobyDependencyKind.RuntimeBuffer,
            NativeMobyDependencyResolution.DestinationResident,NativeMobyDependencySharing.Immutable,Hash(_before),[],[beforeRead],Preserved:preserved)];
        for(int i=0;i<2;i++)
        {
            var b=Bindings[i];var edit=edits[i];var asset=edit.Source.AssetAncestor;
            var read=new NativeMobyDependencyRead("native-F5",new(asset.OriginalDataDomain,asset.SourceRootDataOffset,asset.Model.PackageByteLength),edit.OriginalPackageSha256);
            nodes.Add(new(PackageId(b),NativeMobyDependencyKind.ActorPackage,NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable,edit.VariantIdentity,["current"],[read],
                context=>new(edit.ApplyExact(context.Read(read.Id)),[new("authored-rgb-model",b.Extent)],[])));
        }
        byte[] header=_after.AsSpan(0,0x800).ToArray();
        foreach(var b in Bindings)Put(header,b.RootFieldOffset,0);
        var rootReferences=Bindings.Select(b=>new NativeMobyDependencyReference("authored-root-"+b.AuthoredDestinationClass,
            b.RootFieldOffset,PackageId(b),0,4,0)).ToArray();
        nodes.Add(new("registrations",NativeMobyDependencyKind.ActorRegistration,NativeMobyDependencyResolution.ImportFromSource,
            NativeMobyDependencySharing.Immutable,Reservation.PlanSha256,Bindings.Select(PackageId).ToArray(),[beforeRead],
            context=>{if(!context.Read(beforeRead.Id).AsSpan().SequenceEqual(_before))throw new InvalidDataException("Registration ancestry changed.");return new(header,[new("header",new(FinalDomain,0,0x800))],rootReferences);}));
        foreach(var b in Bindings)
        {
            int beforeAt=Int(_before,0x18)+b.RowSceneOffset+0x36,afterAt=Int(_after,0x18)+b.RowSceneOffset+0x36;
            var read=new NativeMobyDependencyRead("F5-row-class",new(BeforeDomain,beforeAt,2),Hash(_before.AsSpan(beforeAt,2)));
            nodes.Add(new("row-class-"+b.RowId,NativeMobyDependencyKind.Instance,NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable,Reservation.PlanSha256,["registrations"],[read],context=>
                {if(BinaryPrimitives.ReadUInt16LittleEndian(context.Read(read.Id))!=0xF5)throw new InvalidDataException("Original appended row is not F5.");byte[] bytes=new byte[2];BinaryPrimitives.WriteUInt16LittleEndian(bytes,b.AuthoredDestinationClass);return new(bytes,[new("authored-row-class",new(FinalDomain,afterAt,2))],[]);}));
        }
        byte[] Read(NativeByteExtent extent)=>ReferenceEquals(extent.Space,BeforeDomain)?_before.AsSpan((int)extent.Offset,(int)extent.ByteLength).ToArray():edits[0].Source.AssetAncestor.ReadSource(extent);
        var graph=NativeMobyDependencyClosureCompiler.Compile(nodes,Bindings.Select(b=>"row-class-"+b.RowId).ToArray(),Read);
        graph.RequirePreservedFinalBytes(FinalDomain,_after,Read);
        foreach(var install in graph.Installs)
            if(!ReferenceEquals(install.Destination.Space,FinalDomain)||!_after.AsSpan((int)install.Destination.Offset,(int)install.Destination.ByteLength).SequenceEqual(graph.CopyPayload(install.NodeId)))
                throw new InvalidDataException("Authored class dependency output differs from its actual final owner.");
        return graph;
    }
    public NativeJapaneseEggAuthoredClassReservation Reservation{get;}
    public NativeJapaneseEggArtisansIntegrationPlan Integration{get;}
    public NativeJapaneseEggRgbVariantTailPlan Projection{get;}
    public IReadOnlyList<NativeJapaneseEggAuthoredModelBinding> Bindings{get;}
    public bool SourceCertified{get;}
    public uint GuardPc{get;}
    public int GuardByteLength=>0x34;
    public string PlanSha256{get;}
    public string FinalDataSha256{get;}
    public string FinalOverlaySha256{get;}
    public bool NativeRendererUnchanged=>true;
    public bool GlobalUnusedNamespaceProven=>false;
    public bool GenericEffectsApproved=>false;
    public bool DynamicSpawnApproved=>false;
    public bool PickupApproved=>false;
    public bool RuntimeFitProven=>false;
    public bool LitPixelsVerified=>false;
    public bool PublicationApproved=>false;
    internal NativeAddressSpace BeforeDomain{get;}
    internal NativeAddressSpace FinalDomain{get;}
    internal NativeOwnedOverlayAppendLayout OverlayLayout{get;}
    internal NativeMobyDependencyClosurePlan Graph{get;}
    public byte[] CopyLevelData()=>_after.ToArray();
    public byte[] CopyScene()=>_after.AsSpan(Int(_after,0x18),Int(_after,0x1C)).ToArray();
    public byte[] CopyOverlay()=>_overlay.ToArray();
    public byte[] CopyExecutable()=>Integration.CopyExecutable();
    public byte[] CopyVariantPackage(ushort authoredClass)
    {var b=Bindings.SingleOrDefault(b=>b.AuthoredDestinationClass==authoredClass)??throw new InvalidDataException("Unknown authored package owner.");return _after.AsSpan(b.PackageDataOffset,b.ByteLength).ToArray();}
    public byte[] RevertDataExact(ReadOnlySpan<byte> after)
    {
        if(!after.SequenceEqual(_after))throw new InvalidDataException("Authored inverse requires the complete exact final data and complement.");
        byte[] registered=after.ToArray();foreach(var b in Bindings)BinaryPrimitives.WriteUInt16LittleEndian(registered.AsSpan(Int(registered,0x18)+b.RowSceneOffset+0x36),0xF5);
        byte[] inverse=_batch.RevertExact(registered);
        if(!inverse.AsSpan().SequenceEqual(_before))throw new InvalidDataException("Authored inverse lost original current-resource bytes.");return inverse;
    }
    public byte[] RevertOverlayExact(ReadOnlySpan<byte> after)
    {if(!after.SequenceEqual(_overlay))throw new InvalidDataException("Authored inverse requires the complete exact overlay.");return _overlayBefore.ToArray();}
    public void RequireActivation()=>throw new InvalidDataException("Detached authored visual registration has no publication, full peak, generic effects/spawn/pickup or gameplay approval.");
    public static NativeJapaneseEggAuthoredClassesPlan Capture(NativeRegionalSourceSession usa,NativeJapaneseEggAuthoredClassReservation reservation,
        NativeJapaneseEggArtisansIntegrationPlan integration,NativeJapaneseEggRgbEditPlan first,NativeJapaneseEggRgbEditPlan second)
    {
        ArgumentNullException.ThrowIfNull(usa);ArgumentNullException.ThrowIfNull(reservation);ArgumentNullException.ThrowIfNull(integration);
        ArgumentNullException.ThrowIfNull(first);ArgumentNullException.ThrowIfNull(second);
        if(usa.Revision!=NativeDiscRevision.UsaScus94228||!reservation.SourceCertified||reservation.SourceImageSha256!=usa.SourceImageSha256)
            throw new InvalidDataException("Authored visual registration requires the actual exact-USA reservation.");
        try
        {
        _=NativeJapaneseEggRenderAdmissionPlan.Capture(usa,integration);
        var projection=NativeJapaneseEggRgbVariantTailPlan.Capture(usa,integration,first,second);
        return new NativeJapaneseEggAuthoredClassesPlan(reservation,projection,first,second,true);
        }
        finally { usa.VerifyUnchanged(); }
    }
    internal static NativeJapaneseEggAuthoredClassesPlan Fixture(NativeJapaneseEggAuthoredClassReservation reservation,
        NativeJapaneseEggArtisansIntegrationPlan integration,NativeJapaneseEggRgbEditPlan first,NativeJapaneseEggRgbEditPlan second)=>
        new(reservation,NativeJapaneseEggRgbVariantTailPlan.Fixture(integration,first,second),first,second,false);
    private static string PackageId(NativeJapaneseEggAuthoredModelBinding b)=>"authored-package-"+b.AuthoredDestinationClass;
    private static int Int(byte[] b,int at)=>checked((int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at)));
    private static ushort Half(byte[] b,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
    private static void Put(byte[] b,int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),v);
    private static uint Jump(uint address)=>0x08000000u|((address>>2)&0x03FFFFFF);
    private static byte[] Words(params uint[] words){byte[] b=new byte[words.Length*4];for(int i=0;i<words.Length;i++)Put(b,i*4,words[i]);return b;}
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
