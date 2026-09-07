using System.Buffers.Binary;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Detached full physical afterimages for one actual Wizard EXE/Body/Host chain.
/// Every changed and unchanged source sector authenticates its original LBA.
/// No image writer, CUE, receipt, output path or runtime/publication capability.
/// </summary>
public sealed class NativeId65FreshWizardResearchDiscPlan
{
    private static readonly DiscLayout Layout=new(2352,24,22,2048);
    private readonly NativeId65FreshWizardResearchDiscSource _source;
    private readonly NativeId65FreshWizardDiscGeometry _geometry;
    private readonly byte[] _body,_overlay,_executable,_wad,_root;
    private readonly byte _wadFlags,_exeFlags;
    private NativeId65FreshWizardResearchDiscPlan(NativeId65FreshWizardResearchDiscSource source)
    {
        _source=source;var exe=source.Executable;var body=exe.BodyAncestor;
        _body=body.CopyLevelData();_overlay=body.CopyOverlay();_executable=exe.CopyExecutable();
        _geometry=NativeId65FreshWizardDiscGeometry.Compile(_overlay.Length,_body.Length);
        _wad=source.CopyWadHeader();_root=source.CopyRoot();
        if(!_wad.AsSpan(79*8,16).SequenceEqual(new byte[16])||!_wad.AsSpan(81*8,8).SequenceEqual(Convert.FromHexString("0078C90500200000")))
            throw new InvalidDataException("Reserved Wizard WAD pair or adjacent occupied owner changed.");
        BinaryPrimitives.WriteInt32LittleEndian(_wad.AsSpan(79*8),_geometry.OverlayWadOffset);
        BinaryPrimitives.WriteInt32LittleEndian(_wad.AsSpan(79*8+4),_overlay.Length);
        BinaryPrimitives.WriteInt32LittleEndian(_wad.AsSpan(80*8),_geometry.BodyWadOffset);
        BinaryPrimitives.WriteInt32LittleEndian(_wad.AsSpan(80*8+4),_body.Length);
        foreach(var row in source.RootFiles.Where(r=>r.Name is "WAD.WAD" or "SCUS_942.28"))
            PatchRoot(_root,row,row.Name=="WAD.WAD"?37:FinalExecutableLba,row.Name=="WAD.WAD"?FinalWadByteLength:0x66000);
        _wadFlags=(byte)(source.CopyPreimage(53874)[18]&0x81);_exeFlags=(byte)(source.CopyPreimage(54078)[18]&0x81);
        BodySha256=Hash(_body);OverlaySha256=Hash(_overlay);ExecutableSha256=Hash(_executable);
        WadDirectorySha256=Hash(_wad);IsoRootSha256=Hash(_root);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{ProfileId,SourceImageSha256,ExecutablePlanSha256,BodyPlanSha256,
            HostPlanSha256,CanonicalDocumentSha256,BodySha256,OverlaySha256,ExecutableSha256,WadDirectorySha256,IsoRootSha256,
            FinalExecutableLba,FinalWadByteLength,FinalExecutableEndExclusive,ChangedSectorOwnerCount,PendingOwners,
            SourceCertified,InstalledDispatchProven,GlobalTransitionState12Supported,PersistentOwnerIssued,CardOrHistoryCompatibilityProven,
            WholePickupOrDeathProven,GlobalAudioCompatibilityProven,PeakRuntimeMemoryProven,HardwareTransfersProven,
            CompleteIncomingEdgeClosureProven,CompleteNativeDependencyClosureProven,RuntimeVerified,PublicationApproved,NormalCreateBinEnabled}));
        var original=new NativeAddressSpace(NativeAddressKind.DiscLogical,SourceImageSha256,NativeAddressRevision.Original,281270L*2048);
        var final=new NativeAddressSpace(NativeAddressKind.DiscLogical,PlanSha256,NativeAddressRevision.Final,original.ByteLength);
        AddressMap=new(original,final,[
            new("ISO-root-fixed-position",new(original,22*2048L,2048),new(final,22*2048L,2048)),
            new("original-WAD-fixed-position",new(original,37*2048L,0x6927000),new(final,37*2048L,0x6927000)),
            new("actual-Wizard-Host",null,new(final,53875*2048L,_overlay.Length)),
            new("actual-Wizard-Body",null,new(final,53909*2048L,_body.Length)),
            new("relocated-actual-Wizard-EXE",new(original,53875*2048L,0x66000),new(final,FinalExecutableLba*2048L,0x66000))]);
        Regions=Array.AsReadOnly(AddressMap.Regions.Select(r=>new NativeId65FreshDiscRegion(r.Id,r.Original?.Offset,r.Final.Offset,r.Final.ByteLength)).ToArray());
    }
    public const string ProfileId="fresh-A-P1-N32-Town-Host-R1-target10-detached-physical-disc-v1";
    internal NativeId65FreshWizardResearchExecutablePlan ExecutableAncestor=>_source.Executable;
    internal NativeRelocationMap AddressMap{get;}
    internal void RequireAncestry(NativeId65FreshWizardResearchExecutablePlan executable)
    {
        if(!ReferenceEquals(executable,ExecutableAncestor))throw new InvalidDataException("Disc belongs to another actual EXE, even when executable bytes/hashes are equal.");
        NativeId65FreshWizardResearchDiscSource.RequireExecutable(executable);
    }
    public string SourceImageSha256=>NativeId65FreshDiscSource.OriginalSha256;
    public string ExecutablePlanSha256=>ExecutableAncestor.PlanSha256;
    public string BodyPlanSha256=>ExecutableAncestor.BodyPlanSha256;
    public string HostPlanSha256=>ExecutableAncestor.HostPlanSha256;
    public string CanonicalDocumentSha256=>ExecutableAncestor.CanonicalDocumentSha256;
    public string BodySha256{get;}
    public string OverlaySha256{get;}
    public string ExecutableSha256{get;}
    public string WadDirectorySha256{get;}
    public string IsoRootSha256{get;}
    public string PlanSha256{get;}
    public int FinalExecutableLba=>_geometry.FinalExecutableLba;
    public int FinalWadByteLength=>_geometry.FinalWadByteLength;
    public int FinalExecutableEndExclusive=>_geometry.FinalExecutableEndExclusive;
    public int ChangedSectorOwnerCount=>1290;
    public IReadOnlyList<NativeId65FreshDiscRegion> Regions{get;}
    public IReadOnlyList<string> PendingOwners{get;}=Array.AsReadOnly(new[]{
        "Detached exact physical bytes only. No destination writer, CUE, receipt, transaction, project authority, candidate or normal Create BIN route is supplied.",
        "The later physical source index authenticates this disc capture/readback, not historical nested payload-capture reads. Actual immutable ancestry and complete source bytes bind those separate captures.",
        "Static target10 and placed ordinal0 are not durable ID65 owner/card/history/repeated-award or complete death/pickup compatibility.",
        "Complete callback/effect/collider/global1F audio and computed/asynchronous incoming/lifetime closure remain unproved. Global transition state12 and other skip/checkpoint entries remain unsupported.",
        "Physical file placement and known address bounds do not prove peak actor/particle/stack use, allocation33 safety or CD/GPU/SPU completion/interference.",
        "No installed executable/dispatch, hardware, gameplay, runtime, public distribution or publication approval follows from full physical readback."});
    public bool SourceCertified=>true;
    public bool InstalledDispatchProven=>false;
    public bool GlobalTransitionState12Supported=>false;
    public bool PersistentOwnerIssued=>false;
    public bool CardOrHistoryCompatibilityProven=>false;
    public bool WholePickupOrDeathProven=>false;
    public bool GlobalAudioCompatibilityProven=>false;
    public bool PeakRuntimeMemoryProven=>false;
    public bool HardwareTransfersProven=>false;
    public bool CompleteIncomingEdgeClosureProven=>false;
    public bool CompleteNativeDependencyClosureProven=>false;
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;
    public bool NormalCreateBinEnabled=>false;
    public byte[] CopyBody()=>_body.ToArray();
    public byte[] CopyOverlay()=>_overlay.ToArray();
    public byte[] CopyExecutable()=>_executable.ToArray();
    public byte[] CopyWadDirectory()=>_wad.ToArray();
    public byte[] CopyIsoRootDirectory()=>_root.ToArray();
    public static async Task<NativeId65FreshWizardResearchDiscPlan> CaptureAsync(string originalBin,
        NativeId65FreshWizardResearchExecutablePlan executable,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();NativeId65FreshWizardResearchDiscSource.RequireExecutable(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        await using FileStream image=new(Path.GetFullPath(originalBin),FileMode.Open,FileAccess.Read,FileShare.Read);
        var source=await NativeId65FreshWizardResearchDiscSource.CaptureAsync(image,executable,token);
        token.ThrowIfCancellationRequested();var result=new NativeId65FreshWizardResearchDiscPlan(source);token.ThrowIfCancellationRequested();return result;
    }
    internal static void PatchRoot(Span<byte> root,NativeFlatIsoFile row,int lba,int length)
    {
        int at=row.RecordOffset;
        if(row.IsDirectory||row.Name is not("WAD.WAD" or "SCUS_942.28")||lba<0||length<=0||at<0||at>root.Length-34||
            BinaryPrimitives.ReadInt32LittleEndian(root[(at+2)..])!=row.Lba||BinaryPrimitives.ReadInt32BigEndian(root[(at+6)..])!=row.Lba||
            BinaryPrimitives.ReadInt32LittleEndian(root[(at+10)..])!=row.Size||BinaryPrimitives.ReadInt32BigEndian(root[(at+14)..])!=row.Size)
            throw new InvalidDataException("Wizard ISO fields lost exact bounded both-endian preimages.");
        BinaryPrimitives.WriteInt32LittleEndian(root[(at+2)..],lba);BinaryPrimitives.WriteInt32BigEndian(root[(at+6)..],lba);
        BinaryPrimitives.WriteInt32LittleEndian(root[(at+10)..],length);BinaryPrimitives.WriteInt32BigEndian(root[(at+14)..],length);
    }
    internal byte[] CreateExpectedRawSector(int lba,ReadOnlySpan<byte> sourceSector)
    {
        _source.RequireRawSector(lba,sourceSector);byte[] before=sourceSector.ToArray();
        if(!IsChangedOwner(lba))return before;
        byte[] after=lba>=54079?RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(before,Layout,lba):before.ToArray();
        RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(after,Layout,lba);
        if(lba==22)_root.CopyTo(after,24);
        else if(lba==37)_wad.CopyTo(after,24);
        else if(lba>=53875&&lba<FinalExecutableLba)
        {
            int at=checked((lba-53875)*2048);
            if(at<_overlay.Length)_overlay.AsSpan(at,2048).CopyTo(after.AsSpan(24,2048));
            else _body.AsSpan(at-_overlay.Length,2048).CopyTo(after.AsSpan(24,2048));
        }
        else if(lba>=FinalExecutableLba)_executable.AsSpan((lba-FinalExecutableLba)*2048,2048).CopyTo(after.AsSpan(24,2048));
        int flags=lba==22?before[18]&0x81:lba==FinalExecutableLba-1?_wadFlags:lba==FinalExecutableEndExclusive-1?_exeFlags:0;
        after[18]=after[22]=(byte)((after[18]&~0x81)|flags);
        RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(after,Layout,lba);return after;
    }
    internal byte[] CreateOriginalRawSector(int lba,ReadOnlySpan<byte> finalSector)
    {
        if(lba<0||lba>=281270||finalSector.Length!=2352)throw new InvalidDataException("Wizard inverse requires one bounded physical sector.");
        if(!IsChangedOwner(lba)){_source.RequireRawSector(lba,finalSector);return finalSector.ToArray();}
        byte[] before=_source.CopyPreimage(lba);
        if(!finalSector.SequenceEqual(CreateExpectedRawSector(lba,before)))throw new InvalidDataException("Wizard inverse afterimage payload/header/parity changed.");
        return before;
    }
    public NativeId65FreshDiscVerification VerifyImage(Stream original,Stream output,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(output);
        var result=NativeTownSquareClosurePhysicalReadback.Verify(original,output,NativeId65FreshDiscSource.ImageByteLength,SourceImageSha256,(lba,before)=>
        {
            token.ThrowIfCancellationRequested();long position=(lba+1)*2352L;
            if(original.Length!=NativeId65FreshDiscSource.ImageByteLength||output.Length!=NativeId65FreshDiscSource.ImageByteLength||
                original.Position!=position||output.Position!=position)throw new InvalidDataException("Wizard physical readback length/position changed during a sector read.");
            return CreateExpectedRawSector(lba,before);
        });
        NativeId65FreshDiscSource.RequireActualEof(original,token);NativeId65FreshDiscSource.RequireActualEof(output,token);
        token.ThrowIfCancellationRequested();return new(result.Hash,result.Changed);
    }
    private static bool IsChangedOwner(int lba)=>lba is 22 or 37||lba>=53874&&lba<55162;
    private static string Hash(ReadOnlySpan<byte> b)=>NativeId65FreshExecutablePlan.Hash(b);
}
