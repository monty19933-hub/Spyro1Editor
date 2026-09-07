using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshWizardStagingSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Neutral fixed byte layout, not a source issuer or hardware simulator.</summary>
internal sealed class NativeId65FreshWizardUploadTransferLayout
{
    internal const int UploadLength=0xE5800, VramLength=0x80000, BankLength=0x65800;
    internal const int CpuLength=0x7EFF0, DmaLength=0x7F000;
    private readonly byte[] _transfer;
    private NativeId65FreshWizardUploadTransferLayout(ReadOnlySpan<byte> upload)
    {
        PairedUploadSha256=Hash(upload);
        _transfer=new byte[DmaLength];
        upload.Slice(VramLength,BankLength).CopyTo(_transfer);
        upload.Slice(BankLength,DmaLength-BankLength).CopyTo(_transfer.AsSpan(BankLength));
        TransferSourceSha256=Hash(_transfer);
        Owners=Array.AsReadOnly(new[]{
            new NativeId65TransferSourceOwner("complete-issued-research-bank-CD-read",0,BankLength,VramLength,Hash(_transfer.AsSpan(0,BankLength))),
            new NativeId65TransferSourceOwner("same-issued-resource-VRAM-CD-read-residue",BankLength,DmaLength-BankLength,BankLength,Hash(_transfer.AsSpan(BankLength)))});
        SourceReads=Array.AsReadOnly(new[]{
            new NativeId65TransferSourceRead(NativeId65UploadBackend.CpuFifo,0,CpuLength,CpuLength,2),
            new NativeId65TransferSourceRead(NativeId65UploadBackend.Dma,0,DmaLength,CpuLength,64)});
        int cursor=0;
        foreach(var owner in Owners)
        {
            if(owner.Offset!=cursor || owner.ByteLength<=0 || owner.ByteLength>DmaLength-cursor ||
                owner.PairedUploadOffset<0 || owner.PairedUploadOffset>upload.Length-owner.ByteLength ||
                !_transfer.AsSpan(cursor,owner.ByteLength).SequenceEqual(upload.Slice(owner.PairedUploadOffset,owner.ByteLength)))
                throw new InvalidDataException("Research staging lost its complete disjoint source phase owners.");
            cursor+=owner.ByteLength;
        }
        if(cursor!=DmaLength)throw new InvalidDataException("Research staging has an unowned complement.");
    }
    internal bool SourceCertified=>false;
    internal string PairedUploadSha256{get;}
    internal string TransferSourceSha256{get;}
    internal IReadOnlyList<NativeId65TransferSourceOwner> Owners{get;}
    internal IReadOnlyList<NativeId65TransferSourceRead> SourceReads{get;}
    internal byte[] CopyTransferSourceImage()=>_transfer.ToArray();
    internal void RequireExactTransferSource(ReadOnlySpan<byte> bytes)
    {if(!bytes.SequenceEqual(_transfer))throw new InvalidDataException("Research staging bank/residue/DMA-only tail changed.");}
    internal static NativeId65FreshWizardUploadTransferLayout Compile(ReadOnlySpan<byte> upload)
    {
        // Complete length checked before any clone, hash or allocation.
        if(upload.Length!=UploadLength)throw new InvalidDataException("Research staging requires the complete fixed E5800 paired upload.");
        return new(upload);
    }
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

/// <summary>
/// Detached source-issued transfer bytes for the actual fixed A/N16 Wizard
/// resource. The complete65800 bank replaces the start of the same-resource
/// VRAM buffer, leaving19800 residue through the DMA source end. No existing
/// staging/body/header API is widened. SourceCertified refers only to these
/// bytes and their native request consumers, not chronology or hardware.
/// </summary>
public sealed class NativeId65FreshWizardResearchUploadStaging
{
    private NativeId65FreshWizardResearchUploadStaging(NativeId65FreshWizardResearchResourcePlan resources)
    {
        var compilation=resources.CompilationAncestor;
        resources.RequireAncestry(compilation,resources.FamilyAncestor);
        NativeId65FreshWizardResearchResourceSource.RequireFixedIntent(compilation.Intent);
        byte[] upload=resources.CopyUploadPayload(),data=resources.CopyDataComponents(),models=resources.CopyModelPayload();
        resources.RequireExactOutputs(upload,data,models);
        NativeId65FreshWizardResearchResourceSource.RequireInstalledGrammar(data,models);
        byte[] exe=compilation.FreshResourceAncestor.ResourceChainAncestor.Resources.Surface.CopySourceExecutable();
        // Reuse complete-original EXE and all existing CD/CPU/DMA consumer pins;
        // the old fixed bank/layout issuer remains unchanged and is not invoked.
        NativeId65NormalUploadStagingSource.ValidateSourceExecutable(exe);
        if(resources.SourceImageSha256!=SourceImageSha256 || resources.FamilyAncestor.SourceImageSha256!=SourceImageSha256 ||
            compilation.FreshResourceAncestor.SourceImageSha256!=SourceImageSha256 ||
            Hash(upload)!=resources.UploadSha256 || Hash(data)!=resources.DataSha256 || Hash(models)!=resources.ModelSha256)
            throw new InvalidDataException("Research staging lost its complete actual resource ancestry.");
        ResourceAncestor=resources;
        Layout=NativeId65FreshWizardUploadTransferLayout.Compile(upload);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            ProfileId,SourceImageSha256,ResourcePlanSha256,PairedUploadSha256,TransferSourceSha256,
            BankRequestedByteLength,BankCdSectorCount,CpuRequestedByteLength,DmaProgrammedSourceByteLength,
            LinearSpuDestination,DmaLinearSpuEnd,Owners,SourceReads,PendingOwners,
            CompleteSpuStateProven,SpuWrapSemanticsProven,ReservedLow16PreservationProven,TransferTimingProven,
            PeakRuntimeMemoryProven,HardwareTransfersProven,GlobalAudioCompatibilityProven,
            CompleteNativeDependencyClosureProven,BodyIssued,RuntimeVerified,PublicationApproved}));
    }
    internal NativeId65FreshWizardResearchResourcePlan ResourceAncestor{get;}
    internal NativeId65FreshWizardUploadTransferLayout Layout{get;}
    internal void RequireAncestry(NativeId65FreshWizardResearchResourcePlan resources)
    {
        if(!ReferenceEquals(resources,ResourceAncestor))throw new InvalidDataException("Staging belongs to another actual resource, even if its transfer hash is equal.");
        resources.RequireAncestry(resources.CompilationAncestor,resources.FamilyAncestor);
    }
    public const string ProfileId="fresh-A16-wizard33-T6-detached-upload-transfer-research-v1";
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string ResourcePlanSha256=>ResourceAncestor.PlanSha256;
    public string PairedUploadSha256=>Layout.PairedUploadSha256;
    public string TransferSourceSha256=>Layout.TransferSourceSha256;
    public string PlanSha256{get;}
    public int BankRequestedByteLength=>NativeId65FreshWizardUploadTransferLayout.BankLength;
    public int BankCdSectorCount=>BankRequestedByteLength/0x800;
    public int CpuRequestedByteLength=>NativeId65FreshWizardUploadTransferLayout.CpuLength;
    public int DmaProgrammedSourceByteLength=>NativeId65FreshWizardUploadTransferLayout.DmaLength;
    public int LinearSpuDestination=>0x1010;
    public int DmaLinearSpuEnd=>LinearSpuDestination+DmaProgrammedSourceByteLength;
    public IReadOnlyList<NativeId65TransferSourceOwner> Owners=>Layout.Owners;
    public IReadOnlyList<NativeId65TransferSourceRead> SourceReads=>Layout.SourceReads;
    public IReadOnlyList<string> PendingOwners{get;}=Array.AsReadOnly(new[]{
        "Native request/rounding slices bind65800 to203 sectors; intervening GPU/CD/SPU helper completion and buffer interference are unproved.",
        "CPU requests7EFF0; DMA consumes7F000, ending linearly at SPU80010. Wrap and reserved low16 ownership remain unresolved; no silence/free-storage claim.",
        "Global1F occupied mapping compatibility, callback/reward graph and complete native dependencies remain separate from this detached source-byte image.",
        "No scene/body/header/overlay/copy anchor/peak memory/disc/publication is emitted, and no native hardware/runtime/listening result is granted."});
    public bool SourceCertified=>true;
    public bool CompleteSpuStateProven=>false;
    public bool SpuWrapSemanticsProven=>false;
    public bool ReservedLow16PreservationProven=>false;
    public bool TransferTimingProven=>false;
    public bool PeakRuntimeMemoryProven=>false;
    public bool HardwareTransfersProven=>false;
    public bool GlobalAudioCompatibilityProven=>false;
    public bool CompleteNativeDependencyClosureProven=>false;
    public bool BodyIssued=>false;
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;
    public byte[] CopyTransferSourceImage()=>Layout.CopyTransferSourceImage();
    public void RequireExactTransferSource(ReadOnlySpan<byte> bytes)=>Layout.RequireExactTransferSource(bytes);
    public static async Task<NativeId65FreshWizardResearchUploadStaging> CaptureAsync(string originalBin,
        NativeId65FreshWizardResearchResourcePlan resources,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(resources);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        await using FileStream image=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,
            NativeMobySourceInventory.OriginalImageSha256,token);
        try
        {
            token.ThrowIfCancellationRequested();var result=new NativeId65FreshWizardResearchUploadStaging(resources);
            token.ThrowIfCancellationRequested();return result;
        }
        finally
        {
            // Full source readback on every post-capture exit, even cancellation.
            // There is no publication or commit boundary in this detached stage.
            image.Position=0;
            await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,
                NativeMobySourceInventory.OriginalImageSha256,CancellationToken.None);
            token.ThrowIfCancellationRequested();
        }
    }
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
