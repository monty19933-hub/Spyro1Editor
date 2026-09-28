using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Separate original-USA fixed-slot research profile. The source-issued code
/// includes guarded live admission and reset invalidation; emitting it is not
/// proof that Ready was reached, residence survives all native transitions, or
/// collection/save behavior is safe. No caller-provided payload can issue it.
/// </summary>
public sealed class NativeCollectibleCardAdmissionResearchPlan
{
    public const string Profile = "native-collectible-card-admission-research-v1";
    public const string ImageName = "collectible-card-admission-research-r01.bin";
    public const string CueName = "collectible-card-admission-research-r01.cue";
    public const string ReceiptName = "collectible-card-admission-research.receipt.json";
    internal static string[] OutputNames => [ImageName,ReceiptName,CueName];
    private static readonly (int Offset,int Length)[] ExecutableSpans =
        [(0x10,4),(0x2F00,0x50),(0x3030,8),(0x1EAFC,8),(0x23E24,8),(0x65DA4,4),(0x65E40,0x1C0)];
    private static readonly (int Offset,int Length)[] OverlaySpans = [(0x174,8),(0x2060,4),(0x21C8,4)];
    private readonly NativePackedResearchImageOwner physical;
    private readonly NativeFreeFlightResearchSourceCue cue;
    private NativeCollectibleCardAdmissionResearchPlan(NativeFreeFlightResearchSourceCue cue,
        NativeCollectibleCardAdmissionPlan composition,NativePackedResearchImageOwner physical)
    {
        this.cue = cue; Composition = composition; this.physical = physical;
        RequireStageReversion();
        ExecutableSha256 = Hash(CopyExecutable()); OverlaySha256 = Hash(composition.CopyOverlay());
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new { Profile, SourceImagePath, SourceCuePath,
            sourceImageSha256 = NativeMobySourceInventory.OriginalImageSha256, sourceCueSha256 = cue.SourceSha256,
            manifestSha256 = composition.Reader.Loader.ManifestSha256, ordinarySlot = 0,
            ExecutableSha256, OverlaySha256, OriginalWadSha256, WadSha256, RootSha256, Entries,
            GrowthBytes, RelocatedExecutableLba, FinalExecutableEndLba, ResearchOnly,
            ResearchDiscoveryPublicationEnabled, GuardedReadyAdmissionEmitted, NativeInstallationApproved,
            ReadyStateIssued, CollectorEnabled, CardWrites, LifetimeProven, WholeEngineFitProven,
            RuntimeVerified, NormalCreateBinApproved }));
    }
    internal NativeCollectibleCardAdmissionPlan Composition { get; }
    public string SourceImagePath => physical.SourceImagePath;
    public string SourceCuePath => cue.CuePath;
    public string PlanSha256 { get; }
    public string ExecutableSha256 { get; }
    public string OverlaySha256 { get; }
    public string OriginalWadSha256 => physical.OriginalWadSha256;
    public string WadSha256 => physical.WadSha256;
    public string RootSha256 => physical.RootSha256;
    public int GrowthBytes => physical.GrowthBytes;
    public int RelocatedExecutableLba => physical.RelocatedExecutableLba;
    public int FinalExecutableEndLba => physical.FinalExecutableEndLba;
    public IReadOnlyList<NativeWizardResearchEntry> Entries => physical.Entries;
    public bool ResearchOnly => true;
    public bool ResearchDiscoveryPublicationEnabled => true;
    public bool GuardedReadyAdmissionEmitted => true;
    public bool NativeInstallationApproved => false;
    public bool ReadyStateIssued => false;
    public bool CollectorEnabled => false;
    public bool CardWrites => false;
    public bool LifetimeProven => false;
    public bool WholeEngineFitProven => false;
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
    internal DiscLayout Layout => physical.Layout;

    public static async Task<NativeCollectibleCardAdmissionResearchPlan> CaptureAsync(string originalImagePath,
        string originalCuePath,CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath); ArgumentException.ThrowIfNullOrWhiteSpace(originalCuePath);
        string source = Path.GetFullPath(originalImagePath);
        var cue = NativeFreeFlightResearchSourceCue.Capture(originalCuePath,source,ImageName);
        using var usa = NativeRegionalSourceSession.Open(source,NativeDiscRevision.UsaScus94228);
        try
        {
            var composition = NativeCollectibleCardAdmissionPlan.Capture(usa);
            composition.RequireSourceComposition();
            var physical = NativePackedResearchImageOwner.CaptureCardAdmission(source,composition);
            var plan = new NativeCollectibleCardAdmissionResearchPlan(cue,composition,physical);
            await plan.RequireFreshAsync(token); return plan;
        }
        finally { try { usa.VerifyUnchanged(); } finally { cue.RequireFresh(); } }
    }
    public async Task RequireFreshAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); cue.RequireFresh();
        try { await physical.RequireFreshAsync(token); }
        finally { cue.RequireFresh(); }
    }
    public void RequireNativeInstallation() => throw new InvalidOperationException(
        "Guarded admission research publication is not native installation, all-transition residence, runtime, collector or save approval.");
    internal byte[] CopyOriginalExecutable() => Composition.CopyOriginalExecutable();
    internal byte[] CopyExecutable() => physical.CopyExecutable();
    internal byte[] CopyWad() => physical.CopyWad();
    internal byte[] CopyRoot() => physical.CopyRoot();
    internal byte[] CopyCueBytes() => cue.CopyOutputBytes();
    internal byte[] RevertExecutableExact(ReadOnlySpan<byte> bytes) => Composition.RevertExecutableExact(bytes);
    internal void RequireStageReversion()
    {
        Composition.RequireSourceComposition();
        var reader = Composition.Reader; var kernel = Composition.Kernel;
        var entry = Entries.Single(e => e.Index == 2);
        if (!Composition.SourceCertified || !Composition.ReadyAdmissionEmitted || Composition.CardWrites || Composition.CollectorEnabled ||
            reader.OrdinarySlot != 0 || reader.Loader.ManifestSha256 != NativeCollectibleCardLoadResearchComposition.FixedManifest().Sha256 ||
            kernel.Layout.RetainedByteLength != 0xA800 || kernel.Layout.FinalByteLength != 0xD000 || kernel.Layout.CopyAnchor != 0x80087A38 ||
            GrowthBytes != 0x9800 || RelocatedExecutableLba != 53894 || FinalExecutableEndLba != 54098 ||
            entry.OriginalOffset != 0x5B800 || entry.FinalOffset != 0x5B800 || entry.OriginalLength != 0x3800 || entry.FinalLength != 0xD000 ||
            Entries.Any(e => e.Index != 2 && (e.OriginalLength != e.FinalLength || e.OriginalSha256 != e.FinalSha256)) ||
            !CopyExecutable().AsSpan().SequenceEqual(Composition.CopyExecutable()) ||
            !RevertExecutableExact(CopyExecutable()).AsSpan().SequenceEqual(CopyOriginalExecutable()))
            throw new InvalidDataException("Card-admission research composition differs from its fixed source-issued owners.");
        byte[] overlay = Composition.CopyOverlay(), wad = CopyWad();
        if (!wad.AsSpan(entry.FinalOffset,entry.FinalLength).SequenceEqual(overlay) ||
            !Composition.RevertOverlayExact(overlay).AsSpan().SequenceEqual(Composition.CopyOriginalOverlay()))
            throw new InvalidDataException("Packed admission overlay lost its complete exact source inverse.");
        RequireComplement(CopyOriginalExecutable(),CopyExecutable(),ExecutableSpans);
        RequireComplement(Composition.CopyOriginalOverlay(),overlay.AsSpan(0,0x3800),OverlaySpans);
        physical.RequireOriginalWadReversion();
    }
    private static void RequireComplement(ReadOnlySpan<byte> original,ReadOnlySpan<byte> after,(int Offset,int Length)[] spans)
    {
        if (original.Length != after.Length) throw new InvalidDataException("Research source owner length changed.");
        for (int i = 0; i < original.Length; i++)
            if (!spans.Any(s => i >= s.Offset && i < s.Offset+s.Length) && original[i] != after[i])
                throw new InvalidDataException("Research source complement changed outside closed patch spans.");
    }
    internal void WriteNewImage(string path,CancellationToken token = default) => physical.WriteNewImage(path,token);
    internal (string Hash,int Changed) VerifyImage(string path)
    {
        var verified = physical.VerifyImage(path);
        using var image = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var layout = DiscImage.DetectLayout(image);
        byte[] executable = DiscImage.ReadFileBytes(image,layout,RelocatedExecutableLba,0,0x66000);
        byte[] overlay = DiscImage.ReadFileBytes(image,layout,37,0x5B800,0xD000);
        if (!RevertExecutableExact(executable).AsSpan().SequenceEqual(CopyOriginalExecutable()) ||
            !Composition.RevertOverlayExact(overlay).AsSpan().SequenceEqual(Composition.CopyOriginalOverlay()))
            throw new InvalidDataException("Physical admission readback lost the complete original EXE/overlay inverse.");
        return verified;
    }
    internal byte[] ExpectedSector(int lba,byte[] before) => physical.ExpectedSector(lba,before);

    // A read-only source-derived recipe, not a payload-taking publication route.
    // Complete code capacities and zero owners let a separate verifier rebuild
    // the disc; matching generated bytes alone does not prove MIPS semantics.
    internal byte[] BuildDescription()
    {
        var reader = Composition.Reader; var kernel = Composition.Kernel; var bootstrap = Composition.Ancestry!.Bootstrap;
        byte[] overlay = Composition.CopyOverlay(), originalOverlay = Composition.CopyOriginalOverlay();
        var manifest = NativeCollectibleCardLoadResearchComposition.FixedManifest();
        byte[] referenceEnvelope = DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(manifest)
            .WithCollected(0,Guid.Parse("11111111-1111-1111-1111-111111111111")));
        var referenceShadow = DetachedCollectibleLedgerImage.Hydrate(bootstrap.Reservation.Residence,manifest,referenceEnvelope,0,0,1);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = Profile, ImageName, CueName, ReceiptName, PlanSha256, SourceImagePath, SourceCuePath,
            sourceImageSha256 = NativeMobySourceInventory.OriginalImageSha256, sourceCueSha256 = cue.SourceSha256,
            originalExecutableSha256 = Hash(CopyOriginalExecutable()), ExecutableSha256,
            originalOverlaySha256 = Hash(originalOverlay), OverlaySha256,
            OriginalWadSha256, WadSha256, RootSha256, GrowthBytes, RelocatedExecutableLba, FinalExecutableEndLba,
            sourceWadLba = 37, originalWadBytes = 0x6927000, finalWadBytes = 0x6930800,
            Layout.RootExtent, Layout.RootLength, Entries,
            manifest = new { contentId = NativeCollectibleCardLoadResearchComposition.ContentId,
                identities = manifest.Entries.Select(e => e.InstanceId), sha256 = reader.Loader.ManifestSha256,
                ordinarySlot = 0, reader.Loader.EnvelopeByteLength },
            referenceFixture = new { slotBits = "01/00/00", envelopeSha256 = Hash(referenceEnvelope),
                envelopeBase64 = Convert.ToBase64String(referenceEnvelope), shadowSha256 = referenceShadow.ImageSha256,
                shadowBase64 = Convert.ToBase64String(referenceShadow.CopyBytes()), controllerIndex = 0, nativeFileMarker = 0, sessionEpoch = 1,
                boundary = "Host reference only. Actual controller/marker/epoch and successful native admission must be observed before comparison." },
            executablePatchSpans = DescribeSpans(CopyOriginalExecutable(),CopyExecutable(),ExecutableSpans),
            overlay = new { runtimeBase = NativeCollectibleCardLoadPlan.OverlayBase,
                kernel.Layout.RetainedByteLength, kernel.Layout.UsedByteLength, kernel.Layout.FinalByteLength, kernel.Layout.CopyAnchor,
                kernel.EntryPc, kernel.BeginPc, kernel.GatePc, kernel.ValidatePc, kernel.PublishPc, kernel.AfterLoaderPc,
                kernel.StateAddress, kernel.CodeByteLength,
                reader = new { reader.BeginPc, reader.PumpPc, reader.GatePc, reader.StateAddress, reader.ReadBufferAddress,
                    reader.PrefixAddress, reader.ShadowAddress, reader.InputAddress, shaPc = reader.Sha256.EntryPc, loaderPc = reader.Loader.EntryPc },
                patchSpans = DescribeSpans(originalOverlay,overlay,OverlaySpans),
                blocks = reader.Layout.Blocks.Concat(kernel.Layout.Blocks).Select(b => new { id = b.Id,
                    offset = b.Owner.Offset, byteLength = b.Owner.ByteLength,
                    address = NativeCollectibleCardLoadPlan.OverlayBase+b.Owner.Offset,
                    sha256 = Hash(overlay.AsSpan((int)b.Owner.Offset,(int)b.Owner.ByteLength)), b.IsPadding,
                    zeroInitialized = overlay.AsSpan((int)b.Owner.Offset,(int)b.Owner.ByteLength).IndexOfAnyExcept((byte)0) < 0,
                    bytesBase64 = b.Id is "sha" or "loader" or "control" or "admission-control" ?
                        Convert.ToBase64String(overlay.AsSpan((int)b.Owner.Offset,(int)b.Owner.ByteLength)) : null }) },
            bootstrap = new { bootstrap.EntryPc, bootstrap.OriginalEntryPc, bootstrap.LedgerStart, bootstrap.LedgerEnd,
                tailOffset = 0x65E40, tailBytes = 0x1C0, bootstrap.StubByteLength },
            ResearchOnly, ResearchDiscoveryPublicationEnabled, GuardedReadyAdmissionEmitted, ReadyStateIssued,
            CollectorEnabled, CardWrites, NativeInstallationApproved, RuntimeVerified, WholeEngineFitProven, LifetimeProven, NormalCreateBinApproved,
            verificationBoundary = "Exact source-derived code/patch owners for independent physical reconstruction; not runtime, IRQ/DMA, lifetime, collector or save certification."
        },new JsonSerializerOptions { WriteIndented = true });
    }
    private static object[] DescribeSpans(byte[] original,byte[] after,(int Offset,int Length)[] spans) =>
        spans.Select(s => (object)new { offset = s.Offset, byteLength = s.Length,
            originalSha256 = Hash(original.AsSpan(s.Offset,s.Length)), sha256 = Hash(after.AsSpan(s.Offset,s.Length)),
            bytesBase64 = Convert.ToBase64String(after.AsSpan(s.Offset,s.Length)) }).ToArray();
    internal byte[] BuildReceipt((string Hash,int Changed) verified) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile = Profile, PlanSha256, SourceImagePath, SourceCuePath,
        sourceImageSha256 = NativeMobySourceInventory.OriginalImageSha256, sourceCueSha256 = cue.SourceSha256,
        outputImageSha256 = verified.Hash, changedRawSectors = verified.Changed, outputCueSha256 = Hash(CopyCueBytes()),
        ExecutableSha256, OverlaySha256, OriginalWadSha256, WadSha256, RootSha256, Entries,
        GrowthBytes, RelocatedExecutableLba, FinalExecutableEndLba,
        manifestSha256 = Composition.Reader.Loader.ManifestSha256, ordinarySlot = 0,
        copyAnchor = Composition.Kernel.Layout.CopyAnchor, Composition.Kernel.EntryPc, Composition.Kernel.BeginPc,
        Composition.Kernel.GatePc, Composition.Kernel.ValidatePc, Composition.Kernel.PublishPc,
        Composition.Kernel.StateAddress, Composition.Kernel.CodeByteLength,
        executableOwners = ExecutableSpans.Select(s => new { offset = s.Offset, byteLength = s.Length }),
        overlayPrefixOwners = OverlaySpans.Select(s => new { offset = s.Offset, byteLength = s.Length }),
        completeOriginalExecutableInverse = true, completeOriginalOverlayInverse = true, completeOriginalWadInverse = true,
        wholeImageExpectedSectorReadback = true, exactCueAndReceiptReadback = true,
        exceptionRollback = true, crashAtomic = false, hostileConcurrentFilesystemSafety = false,
        ResearchOnly, ResearchDiscoveryPublicationEnabled, GuardedReadyAdmissionEmitted, ReadyStateIssued,
        CollectorEnabled, CardWrites, NativeInstallationApproved, RuntimeVerified, WholeEngineFitProven, LifetimeProven, NormalCreateBinApproved,
        boundary = "Contains guarded admission code and native reset invalidation. Publication does not issue live Ready, runtime/lifetime/save approval or normal Create BIN authority.",
        pending = new[] { "exact-disc full-firmware runtime admission and rejection observations", "all native transitions and IRQ/DMA residence ownership",
            "whole-engine fit", "collection composition", "save/write persistence and power-loss behavior" }
    },new JsonSerializerOptions { WriteIndented = true });
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
