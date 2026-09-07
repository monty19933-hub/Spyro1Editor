using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshWizardResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Exact primary A/P1/N32 research recipe bound to one actual Disc object; not native/runtime or normal exporter authority.</summary>
public sealed class NativeId65FreshWizardResearchCandidatePlan
{
    public const string ProfileId="native-id65-fresh-wizard-primary-p1-n32-research-r01";
    internal const string ImageName="native-id65-fresh-wizard-p1-n32-r01.bin";
    internal const string CueName="native-id65-fresh-wizard-p1-n32-r01.cue";
    internal const string ReceiptName="native-id65-fresh-wizard-p1-n32-r01.receipt.json";
    internal static string[] OutputNames=>[ImageName,ReceiptName,CueName];
    private const string ImageHash="c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6";
    private const string CanonicalJson="""{"kind":"spyro-native-fresh-intent","schema":1,"floor":{"x":8448,"y":8448,"z":8704,"width":1024,"depth":1024},"spawn":{"x":8960,"y":8960,"heading":64},"display":{"floorRgb":[80,128,112],"skyRgb":[128,96,64]},"musicTrackId":19}""";
    private readonly NativeId65FreshWizardResearchDiscPlan _disc;
    private readonly byte[] _sourceCue,_outputCue,_canonical;
    private readonly string _imageIdentity,_cueIdentity;
    private NativeId65FreshWizardResearchCandidatePlan(string image,string cue,NativeId65FreshWizardResearchDiscPlan disc,
        byte[] sourceCue,string imageIdentity,string cueIdentity)
    {
        SourceImagePath=image;SourceCuePath=cue;_disc=disc;_sourceCue=sourceCue;_imageIdentity=imageIdentity;_cueIdentity=cueIdentity;
        _canonical=disc.ExecutableAncestor.BodyAncestor.HostAncestor.SceneAncestor.CompilationAncestor.CopyCanonicalDocument();
        _outputCue=DiscCueSheet.TextEncoding.GetBytes($"FILE \"{ImageName}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        SourceCueSha256=Hash(sourceCue);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{ProfileId,SourceImagePath,SourceCuePath,SourceCueSha256,imageIdentity,cueIdentity,
            DiscPlanSha256,ExecutablePlanSha256,BodyPlanSha256,HostPlanSha256,ScenePlanSha256,FallbackPlanSha256,CanonicalDocumentSha256,
            expectedImage=ImageHash,changedSectors=1290}));
        _=BuildReceipt(new(ImageHash,1290)); // Bound prospective companion, not a claim that any image was written/read back.
    }
    internal NativeId65FreshWizardResearchDiscPlan DiscAncestor=>_disc;
    internal void RequireAncestry(NativeId65FreshWizardResearchDiscPlan disc)
    {if(!ReferenceEquals(disc,_disc))throw new InvalidDataException("Candidate belongs to another actual Disc, even if all bytes/hashes match.");RequireDisc(disc);}
    public string SourceImagePath{get;}
    public string SourceCuePath{get;}
    public string SourceCueSha256{get;}
    public string PlanSha256{get;}
    public string DiscPlanSha256=>_disc.PlanSha256;
    public string ExecutablePlanSha256=>_disc.ExecutablePlanSha256;
    public string BodyPlanSha256=>_disc.BodyPlanSha256;
    public string HostPlanSha256=>_disc.HostPlanSha256;
    public string ScenePlanSha256=>_disc.ExecutableAncestor.BodyAncestor.HostAncestor.ScenePlanSha256;
    public string FallbackPlanSha256=>_disc.ExecutableAncestor.BodyAncestor.HostAncestor.FallbackResourcePlanSha256;
    public string CanonicalDocumentSha256=>_disc.CanonicalDocumentSha256;
    public string ExpectedImageSha256=>ImageHash;
    public long ExpectedImageByteLength=>NativeId65FreshDiscSource.ImageByteLength;
    public int ExpectedChangedRawSectors=>1290;
    public bool SourceCertified=>true;
    public bool RuntimeVerified=>false;
    public bool NormalCreateBinApproved=>false;
    public bool CompleteNativeDependencyClosureProven=>false;
    public bool PersistentOwnerIssued=>false;
    public bool GlobalAudioCompatibilityProven=>false;
    public bool PeakRuntimeMemoryProven=>false;
    public static async Task<NativeId65FreshWizardResearchCandidatePlan> CaptureAsync(string originalBin,string sourceCue,
        NativeId65FreshWizardResearchDiscPlan disc,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(disc);RequireDisc(disc);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);ArgumentException.ThrowIfNullOrWhiteSpace(sourceCue);
        string image=Path.GetFullPath(originalBin),cue=Path.GetFullPath(sourceCue),imageIdentity=PathIdentity(image),cueIdentity=PathIdentity(cue);
        byte[] bytes=ReadCueFile(cue,token);ValidateSourceCue(bytes,cue,image);
        var plan=new NativeId65FreshWizardResearchCandidatePlan(image,cue,disc,bytes,imageIdentity,cueIdentity);
        await plan.RequireFreshAsync(token);token.ThrowIfCancellationRequested();return plan;
    }
    internal static void RequireDisc(NativeId65FreshWizardResearchDiscPlan disc)
    {
        ArgumentNullException.ThrowIfNull(disc);disc.RequireAncestry(disc.ExecutableAncestor);
        var exe=disc.ExecutableAncestor;var body=exe.BodyAncestor;var host=body.HostAncestor;var scene=host.SceneAncestor;
        if(!scene.CompilationAncestor.CopyCanonicalDocument().AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(CanonicalJson))||
            scene.Placement!=new NativeWizardPlacementIntent(8704,8960,8704,9216,8960)||exe.SelectedBuiltInTrack!=19||
            exe.Census.StaticTreasureTarget!=10||exe.Census.TargetSlot!=35||disc.FinalExecutableLba!=54958||disc.FinalExecutableEndExclusive!=55162||
            disc.FinalWadByteLength!=0x6B44800||disc.ChangedSectorOwnerCount!=1290||
            disc.PlanSha256!="896677d4bdfabfb2bd90556fd7d2e27c6997c88b9dcb6df7a8b670616ce66752"||
            Hash(disc.CopyBody())!="33e54ff9e15608ada3f6d430c54fbabc21a8263e059c050b391db22cc541bc00"||
            Hash(disc.CopyOverlay())!="ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e"||
            Hash(disc.CopyExecutable())!="a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc"||
            Hash(disc.CopyWadDirectory())!="4a862b5fa39739d5a6041b8de75aa8801824aed35afe0283f9ad572810580e82"||
            Hash(disc.CopyIsoRootDirectory())!="2769092a104860606518faf9cc8188c27257236b471d11153ac714ebba6ee5ee")
            throw new InvalidDataException("Wizard candidate requires the actual exact primary A/P1/N32/track19/target10 recipe; alternate valid scenes are not replaced.");
    }
    internal byte[] CopyCanonicalDocument()=>_canonical.ToArray();
    internal byte[] CopyCueBytes()=>_outputCue.ToArray();
    internal byte[] BuildReceipt(NativeId65FreshDiscVerification verified)
    {
        RequireVerification(verified);var exe=_disc.ExecutableAncestor;var body=exe.BodyAncestor;var host=body.HostAncestor;
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(new{schema=1,profile=ProfileId,planSha256=PlanSha256,
            sourceImageSha256=NativeId65FreshDiscSource.OriginalSha256,sourceCueSha256=SourceCueSha256,sourceImagePath=SourceImagePath,sourceCuePath=SourceCuePath,
            discPlanSha256=DiscPlanSha256,executablePlanSha256=ExecutablePlanSha256,bodyPlanSha256=BodyPlanSha256,hostPlanSha256=HostPlanSha256,
            scenePlanSha256=ScenePlanSha256,fallbackPlanSha256=FallbackPlanSha256,
            canonicalDocumentJson=Encoding.UTF8.GetString(_canonical),canonicalDocumentSha256=CanonicalDocumentSha256,projectSaveBindingClaimed=false,
            bodySha256=_disc.BodySha256,headerSha256=body.HeaderSha256,overlaySha256=_disc.OverlaySha256,initializerSha256=host.InitializerSha256,
            transferSha256=body.TransferSourceSha256,executableSha256=_disc.ExecutableSha256,wadDirectorySha256=_disc.WadDirectorySha256,
            rootDirectorySha256=_disc.IsoRootSha256,cueSha256=Hash(_outputCue),imageSha256=verified.OutputSha256,imageByteLength=ExpectedImageByteLength,
            changedRawSectors=verified.ChangedSectors,executableLba=_disc.FinalExecutableLba,executableEndExclusive=_disc.FinalExecutableEndExclusive,
            wadByteLength=_disc.FinalWadByteLength,overlayWadOffset=0x6927000,overlayByteLength=0x11000,bodyWadOffset=0x6938000,bodyByteLength=0x20C800,
            staticCensus=exe.Census,sourceReadbackRequired=true,researchOnly=true,runtimeVerified=false,normalCreateBinApproved=false,
            persistentOwnerIssued=false,cardOrHistoryCompatibilityProven=false,wholePickupOrDeathProven=false,globalAudioCompatibilityProven=false,
            peakRuntimeMemoryProven=false,hardwareTransfersProven=false,completeIncomingEdgeClosureProven=false,completeNativeDependencyClosureProven=false,
            checkpointOrForeignStateSupported=false,discPlanLimitations=_disc.PendingOwners,
            candidatePublicationScope="Exact new-only research BIN/CUE/receipt; detached ancestor limitations are not runtime certification."},new JsonSerializerOptions{WriteIndented=true});
        if(bytes.Length is <1 or >65536)throw new InvalidDataException("Wizard receipt exceeds the exact bounded companion owner.");
        return bytes;
    }
    internal async Task RequireFreshAsync(CancellationToken token=default)
    {
        RequireCueFresh(token);using var source=new FileStream(SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        await NativeId65FreshDiscSource.RequireWholeOriginalAsync(source,token);RequireCueFresh(token);
    }
    internal void WriteNewImage(string path,CancellationToken token,Action firstSector)
    {
        token.ThrowIfCancellationRequested();RequireCueFresh(token);
        using var source=new FileStream(SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        // The complete original finally also covers CreateNew/flush/dispose failures.
        RequireOriginal(source,token);
        try
        {
            using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            _=WriteImageCore(source,output,token,firstSector);
            output.Flush(flushToDisk:true);token.ThrowIfCancellationRequested();RequireCueFresh(token);
        }
        finally{RequireOriginal(source,CancellationToken.None);RequireCueFresh(CancellationToken.None);token.ThrowIfCancellationRequested();}
    }
    // Same production loop. Tests may stop after a few MemoryStream bytes;
    // streams cannot issue a Plan or bypass its actual original per-LBA index.
    internal NativeId65FreshDiscVerification WriteImageForSmoke(Stream source,Stream output,CancellationToken token,Action firstSector)
    {
        token.ThrowIfCancellationRequested();RequireOriginal(source,token);
        try{return WriteImageCore(source,output,token,firstSector);}
        finally{RequireOriginal(source,CancellationToken.None);RequireCueFresh(CancellationToken.None);token.ThrowIfCancellationRequested();}
    }
    private NativeId65FreshDiscVerification WriteImageCore(Stream source,Stream output,CancellationToken token,Action firstSector)
    {
        ArgumentNullException.ThrowIfNull(output);ArgumentNullException.ThrowIfNull(firstSector);
        if(!output.CanWrite||!output.CanSeek||output.Length!=0||output.Position!=0)
            throw new InvalidDataException("Wizard streaming output must be empty, writable and seekable.");
        using var originalHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var outputHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        source.Position=0;byte[] before=new byte[2352];int changed=0;
        for(int lba=0;lba<281270;lba++)
        {
            token.ThrowIfCancellationRequested();source.ReadExactly(before);
            if(source.Length!=ExpectedImageByteLength||source.Position!=(lba+1)*2352L)throw new InvalidDataException("Wizard original length/position changed at a streaming pair.");
            byte[] after=_disc.CreateExpectedRawSector(lba,before);originalHash.AppendData(before);outputHash.AppendData(after);
            if(!before.AsSpan().SequenceEqual(after))changed++;output.Write(after);
            if(output.Position!=(lba+1)*2352L||output.Length!=output.Position)throw new InvalidDataException("Wizard output length/position changed at a streaming pair.");
            token.ThrowIfCancellationRequested();if(lba==0)firstSector();
        }
        NativeId65FreshDiscSource.RequireActualEof(source,token);
        if(Convert.ToHexStringLower(originalHash.GetHashAndReset())!=NativeId65FreshDiscSource.OriginalSha256)
            throw new InvalidDataException("Wizard streaming full original identity changed.");
        var result=new NativeId65FreshDiscVerification(Convert.ToHexStringLower(outputHash.GetHashAndReset()),changed);
        RequireVerification(result);token.ThrowIfCancellationRequested();return result;
    }
    internal NativeId65FreshDiscVerification VerifyImage(string path,CancellationToken token)
    {
        using var source=new FileStream(SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        using var output=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var result=_disc.VerifyImage(source,output,token);RequireVerification(result);return result;
    }
    private static void RequireVerification(NativeId65FreshDiscVerification verified)
    {ArgumentNullException.ThrowIfNull(verified);if(verified.OutputSha256!=ImageHash||verified.ChangedSectors!=1290)throw new InvalidDataException("Wizard readback must match the independent primary full image and1290 changed sectors.");}
    internal static void ValidateSourceCue(ReadOnlySpan<byte> bytes,string cue,string image)
    {
        try{_=NativeId65FreshDiscResearchCandidatePlan.CompileCueForSmoke(bytes,cue,image);}
        catch(DecoderFallbackException error){throw new InvalidDataException("Wizard source CUE must be strict UTF8.",error);}
    }
    private static byte[] ReadCueFile(string path,CancellationToken token)
    {using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(input,token);}
    private void RequireCueFresh(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(_cueIdentity!=PathIdentity(SourceCuePath)||_imageIdentity!=PathIdentity(SourceImagePath))throw new InvalidDataException("Wizard source CUE/image path binding changed.");
        byte[] current=ReadCueFile(SourceCuePath,token);
        if(!current.AsSpan().SequenceEqual(_sourceCue))throw new InvalidDataException("Wizard source CUE snapshot changed.");
        ValidateSourceCue(current,SourceCuePath,SourceImagePath);
        if(_cueIdentity!=PathIdentity(SourceCuePath)||_imageIdentity!=PathIdentity(SourceImagePath))throw new InvalidDataException("Wizard source paths changed during same-snapshot FILE revalidation.");
        token.ThrowIfCancellationRequested();
    }
    private static string PathIdentity(string path)
    {
        FileInfo file=new(path);if(!file.Exists||Directory.Exists(path))throw new InvalidDataException("Wizard source path is missing or not a file.");
        List<string> identity=[file.FullName,file.LinkTarget??""];var resolved=file.ResolveLinkTarget(true)??file;identity.Add(resolved.FullName);
        foreach(string leaf in new[]{file.FullName,resolved.FullName})
            for(DirectoryInfo? parent=new FileInfo(leaf).Directory;parent!=null;parent=parent.Parent)
            {
                if(identity.Count>132)throw new InvalidDataException("Wizard source path depth exceeds its bound.");
                identity.Add(parent.FullName+"|"+parent.LinkTarget);
            }
        return string.Join('\n',identity);
    }
    private static void RequireOriginal(Stream source,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(source);token.ThrowIfCancellationRequested();
        if(!source.CanRead||!source.CanSeek||source.Length!=NativeId65FreshDiscSource.ImageByteLength)throw new InvalidDataException("Wizard original stream has invalid readable length.");
        source.Position=0;byte[] block=new byte[2352*256];using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for(long at=0;at<NativeId65FreshDiscSource.ImageByteLength;)
        {
            token.ThrowIfCancellationRequested();int n=(int)Math.Min(block.Length,NativeId65FreshDiscSource.ImageByteLength-at);source.ReadExactly(block.AsSpan(0,n));at+=n;
            if(source.Length!=NativeId65FreshDiscSource.ImageByteLength||source.Position!=at)throw new InvalidDataException("Wizard original identity read lost length/position.");
            hash.AppendData(block.AsSpan(0,n));
        }
        NativeId65FreshDiscSource.RequireActualEof(source,token);
        if(Convert.ToHexStringLower(hash.GetHashAndReset())!=NativeId65FreshDiscSource.OriginalSha256)throw new InvalidDataException("Wizard original full source SHA changed.");
        token.ThrowIfCancellationRequested();
    }
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
