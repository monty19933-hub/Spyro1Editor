using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Closed source-issued composition of the slot-zero card reader and cold
/// NOT-LOADED reservation. Each peer is captured from the same original EXE;
/// neither peer's arbitrary afterimage can stand in for the other's ancestor.
/// </summary>
internal sealed class NativeCollectibleCardLoadResearchComposition
{
    internal static readonly Guid ContentId = Guid.Parse("12345678-1234-5678-9012-123456789abc");
    private static readonly (int Offset, int Length)[] CardSpans = [(0x3030,8),(0x1EAFC,8),(0x23E24,8)];
    private static readonly (int Offset, int Length)[] StartupSpans = [(0x10,4),(0x65DA4,4),(0x65E40,0x1C0)];
    private readonly byte[] original, executable;
    private NativeCollectibleCardLoadResearchComposition(NativeCollectibleCardLoadPlan card,
        NativeCollectibleColdBootBootstrap bootstrap)
    {
        Card = card; Bootstrap = bootstrap; original = card.CopyOriginalExecutable();
        byte[] startup = bootstrap.CopyExecutable(), reader = card.CopyExecutable();
        RequireAncestry();
        executable = original.ToArray();
        foreach (var span in CardSpans) reader.AsSpan(span.Offset,span.Length).CopyTo(executable.AsSpan(span.Offset,span.Length));
        foreach (var span in StartupSpans) startup.AsSpan(span.Offset,span.Length).CopyTo(executable.AsSpan(span.Offset,span.Length));
        // The full tails and complements are owned, even where their bytes are zero.
        if (!RevertExecutableExact(executable).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Card-load research composition lost its complete original inverse.");
    }
    internal NativeCollectibleCardLoadPlan Card { get; }
    internal NativeCollectibleColdBootBootstrap Bootstrap { get; }
    internal static DetachedCollectibleManifest FixedManifest() => DetachedCollectibleManifest.CreateNew(ContentId,
        [Guid.Parse("11111111-1111-1111-1111-111111111111"),Guid.Parse("22222222-2222-2222-2222-222222222222")]);
    internal static NativeCollectibleCardLoadResearchComposition Create(NativeCollectibleCardLoadPlan card,
        NativeCollectibleColdBootBootstrap bootstrap)
    {
        ArgumentNullException.ThrowIfNull(card); ArgumentNullException.ThrowIfNull(bootstrap);
        return new(card,bootstrap);
    }
    internal void RequireAncestry()
    {
        var manifest = FixedManifest();
        var residence = Bootstrap.Reservation.Residence;
        residence.RequireManifest(manifest);
        if (!Card.SourceCertified || !Bootstrap.SourceEvidenceBound || !Bootstrap.Reservation.SourceEvidenceBound ||
            !residence.SourceEvidenceBound || Card.OrdinarySlot != 0 || Card.Loader.OrdinarySlot != 0 ||
            residence.OrdinarySlot != 0 || Card.Loader.ManifestSha256 != manifest.Sha256 ||
            Card.Layout.RetainedByteLength != 0x3800 || Card.Layout.FinalByteLength != 0xA800 ||
            Card.Layout.CopyAnchor != 0x80085238 || Card.CardWrites || Card.ReadyIssued ||
            Bootstrap.LedgerStart != 0x801FF600 || Bootstrap.LedgerEnd != 0x801FF800 ||
            !Card.CopyOriginalExecutable().AsSpan().SequenceEqual(original) ||
            !Bootstrap.Reservation.CopyOriginalExecutable().AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Card-load research requires fixed manifest/slot and matching complete original EXE ancestry.");
        NativeCollectibleColdBootBootstrap.RequireOriginalSource(original);
        if (!Card.RevertExecutableExact(Card.CopyExecutable()).AsSpan().SequenceEqual(original) ||
            !Bootstrap.RevertExact(Bootstrap.CopyExecutable()).AsSpan().SequenceEqual(original) ||
            !Card.RevertExact(Card.CopyOverlay()).AsSpan().SequenceEqual(Card.CopyOriginalOverlay()))
            throw new InvalidDataException("Card-load research ancestor inverse differs from complete original owners.");
        RequireComplement(Card.CopyExecutable(),CardSpans);
        RequireComplement(Bootstrap.CopyExecutable(),StartupSpans);
    }
    private void RequireComplement(byte[] after,(int Offset,int Length)[] owned)
    {
        if (after.Length != original.Length) throw new InvalidDataException("Composition ancestor EXE length changed.");
        for (int at = 0; at < after.Length; at++)
            if (!owned.Any(s => at >= s.Offset && at < s.Offset+s.Length) && after[at] != original[at])
                throw new InvalidDataException("Composition ancestor changed an executable complement byte.");
    }
    internal byte[] CopyOriginalExecutable() => original.ToArray();
    internal byte[] CopyExecutable() => executable.ToArray();
    internal byte[] RevertExecutableExact(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.SequenceEqual(executable))
            throw new InvalidDataException("Composed executable inverse requires the entire exact afterimage including both tails and complement.");
        byte[] cardOnly = bytes.ToArray(), startupOnly = bytes.ToArray();
        foreach (var span in StartupSpans) original.AsSpan(span.Offset,span.Length).CopyTo(cardOnly.AsSpan(span.Offset,span.Length));
        foreach (var span in CardSpans) original.AsSpan(span.Offset,span.Length).CopyTo(startupOnly.AsSpan(span.Offset,span.Length));
        byte[] fromCard = Card.RevertExecutableExact(cardOnly), fromStartup = Bootstrap.RevertExact(startupOnly);
        if (!fromCard.AsSpan().SequenceEqual(original) || !fromStartup.AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Composed executable peer inverses do not recover the same complete original.");
        return original.ToArray();
    }
}

/// <summary>
/// Original-USA card-load research only: slot 0 and one fixed two-identity
/// manifest. No collector, live Ready transition, card write, arbitrary payload
/// or ordinary Create BIN authorization is issued by this publication profile.
/// </summary>
public sealed class NativeCollectibleCardLoadResearchPlan
{
    public const string Profile = "native-collectible-card-load-research-v1";
    public const string ImageName = "collectible-card-load-research-r01.bin";
    public const string CueName = "collectible-card-load-research-r01.cue";
    public const string ReceiptName = "collectible-card-load-research.receipt.json";
    internal static string[] OutputNames => [ImageName,ReceiptName,CueName];
    private readonly NativePackedResearchImageOwner physical;
    private readonly NativeFreeFlightResearchSourceCue cue;
    private NativeCollectibleCardLoadResearchPlan(NativeFreeFlightResearchSourceCue cue,
        NativeCollectibleCardLoadResearchComposition composition,NativePackedResearchImageOwner physical)
    {
        this.cue = cue; Composition = composition; this.physical = physical;
        RequireStageReversion();
        ExecutableSha256 = Hash(CopyExecutable()); OverlaySha256 = Hash(composition.Card.CopyOverlay());
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new { Profile, SourceImagePath, SourceCuePath,
            sourceImageSha256 = NativeMobySourceInventory.OriginalImageSha256, sourceCueSha256 = cue.SourceSha256,
            manifestSha256 = composition.Card.Loader.ManifestSha256, ordinarySlot = 0,
            ExecutableSha256, OverlaySha256, OriginalWadSha256, WadSha256, RootSha256, Entries,
            GrowthBytes, RelocatedExecutableLba, FinalExecutableEndLba, ResearchOnly,
            ResearchDiscoveryPublicationEnabled, NativeInstallationApproved, ReadyStateIssued, CardWrites,
            LifetimeProven, WholeEngineFitProven, RuntimeVerified, NormalCreateBinApproved }));
    }
    internal NativeCollectibleCardLoadResearchComposition Composition { get; }
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
    public bool NativeInstallationApproved => false;
    public bool ReadyStateIssued => false;
    public bool CollectorEnabled => false;
    public bool CardWrites => false;
    public bool LifetimeProven => false;
    public bool WholeEngineFitProven => false;
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
    internal DiscLayout Layout => physical.Layout;

    public static async Task<NativeCollectibleCardLoadResearchPlan> CaptureAsync(string originalImagePath,
        string originalCuePath,CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath); ArgumentException.ThrowIfNullOrWhiteSpace(originalCuePath);
        string source = Path.GetFullPath(originalImagePath);
        var cue = NativeFreeFlightResearchSourceCue.Capture(originalCuePath,source,ImageName);
        using var usa = NativeRegionalSourceSession.Open(source,NativeDiscRevision.UsaScus94228);
        try
        {
            var manifest = NativeCollectibleCardLoadResearchComposition.FixedManifest();
            var card = NativeCollectibleCardLoadPlan.Capture(usa,manifest,0);
            var residence = NativeCollectibleSelectedSlotResidence.Capture(usa,manifest,0);
            var bootstrap = NativeCollectibleColdBootBootstrap.Create(NativeCollectibleReservationMechanics.Create(residence));
            var composition = NativeCollectibleCardLoadResearchComposition.Create(card,bootstrap);
            var physical = NativePackedResearchImageOwner.CaptureCardLoad(source,composition);
            var plan = new NativeCollectibleCardLoadResearchPlan(cue,composition,physical);
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
    public void RequireNativeInstallation() => Composition.Bootstrap.RequireNativeInstallation();
    internal byte[] CopyOriginalExecutable() => Composition.CopyOriginalExecutable();
    internal byte[] CopyExecutable() => physical.CopyExecutable();
    internal byte[] CopyWad() => physical.CopyWad();
    internal byte[] CopyRoot() => physical.CopyRoot();
    internal byte[] CopyCueBytes() => cue.CopyOutputBytes();
    internal byte[] RevertExecutableExact(ReadOnlySpan<byte> bytes) => Composition.RevertExecutableExact(bytes);
    internal void RequireStageReversion()
    {
        Composition.RequireAncestry();
        var entry = Entries.Single(e => e.Index == 2);
        if (GrowthBytes != 0x7000 || RelocatedExecutableLba != 53889 || FinalExecutableEndLba != 54093 ||
            entry.OriginalOffset != 0x5B800 || entry.FinalOffset != 0x5B800 ||
            entry.OriginalLength != 0x3800 || entry.FinalLength != 0xA800 ||
            Entries.Any(e => e.Index != 2 && (e.OriginalLength != e.FinalLength || e.OriginalSha256 != e.FinalSha256)) ||
            !RevertExecutableExact(CopyExecutable()).AsSpan().SequenceEqual(CopyOriginalExecutable()))
            throw new InvalidDataException("Card-load research physical composition differs from its closed source owners.");
        byte[] wad = CopyWad();
        if (!wad.AsSpan(entry.FinalOffset,entry.FinalLength).SequenceEqual(Composition.Card.CopyOverlay()))
            throw new InvalidDataException("Packed card overlay differs from the actual source-issued card loader.");
        physical.RequireOriginalWadReversion();
    }
    internal void WriteNewImage(string path,CancellationToken token = default) => physical.WriteNewImage(path,token);
    internal (string Hash,int Changed) VerifyImage(string path)
    {
        var verified = physical.VerifyImage(path);
        using var image = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var layout = DiscImage.DetectLayout(image);
        byte[] exe = DiscImage.ReadFileBytes(image,layout,RelocatedExecutableLba,0,0x66000);
        byte[] card = DiscImage.ReadFileBytes(image,layout,37,0x5B800,0xA800);
        if (!RevertExecutableExact(exe).AsSpan().SequenceEqual(CopyOriginalExecutable()) ||
            !Composition.Card.RevertExact(card).AsSpan().SequenceEqual(Composition.Card.CopyOriginalOverlay()))
            throw new InvalidDataException("Physical card/EXE readback lost its complete original inverse.");
        return verified;
    }
    internal byte[] ExpectedSector(int lba,byte[] before) => physical.ExpectedSector(lba,before);

    // Describes deterministic source-derived bytes for a separate physical
    // reconstruction. Hash-pinning generated code does not re-prove its semantics.
    internal byte[] BuildDescription()
    {
        var card = Composition.Card; byte[] overlay = card.CopyOverlay();
        var manifest = NativeCollectibleCardLoadResearchComposition.FixedManifest();
        byte[] referenceEnvelope = DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(manifest)
            .WithCollected(0,Guid.Parse("11111111-1111-1111-1111-111111111111")));
        var referenceShadow = DetachedCollectibleLedgerImage.Hydrate(Composition.Bootstrap.Reservation.Residence,
            manifest,referenceEnvelope,0,0,1);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = Profile, ImageName, CueName, ReceiptName, PlanSha256, SourceImagePath, SourceCuePath,
            sourceImageSha256 = NativeMobySourceInventory.OriginalImageSha256, sourceCueSha256 = cue.SourceSha256,
            originalExecutableSha256 = Hash(CopyOriginalExecutable()), ExecutableSha256,
            originalOverlaySha256 = Hash(card.CopyOriginalOverlay()), OverlaySha256,
            OriginalWadSha256, WadSha256, RootSha256, GrowthBytes, RelocatedExecutableLba, FinalExecutableEndLba,
            sourceWadLba = 37, originalWadBytes = 0x6927000, finalWadBytes = 0x692E000,
            Layout.RootExtent, Layout.RootLength, Entries,
            manifest = new { contentId = NativeCollectibleCardLoadResearchComposition.ContentId,
                identities = manifest.Entries.Select(e => e.InstanceId),
                sha256 = card.Loader.ManifestSha256, ordinarySlot = 0, card.Loader.EnvelopeByteLength },
            referenceFixture = new { slotBits = "01/00/00", envelopeSha256 = Hash(referenceEnvelope),
                envelopeBase64 = Convert.ToBase64String(referenceEnvelope),
                shadowSha256 = referenceShadow.ImageSha256, shadowBase64 = Convert.ToBase64String(referenceShadow.CopyBytes()),
                controllerIndex = 0, nativeFileMarker = 0, sessionEpoch = 1,
                boundary = "Host reference shadow only, not installed or observed. Actual native marker and epoch must be observed before exact comparison." },
            overlay = new { runtimeBase = NativeCollectibleCardLoadPlan.OverlayBase, card.Layout.RetainedByteLength,
                card.Layout.UsedByteLength, card.Layout.FinalByteLength, card.Layout.CopyAnchor,
                card.BeginPc, card.PumpPc, card.GatePc, card.StateAddress, card.ReadBufferAddress,
                card.PrefixAddress, card.ShadowAddress, card.InputAddress,
                shaPc = card.Sha256.EntryPc, loaderPc = card.Loader.EntryPc,
                blocks = card.Layout.Blocks.Select(b => new { id = b.Id, offset = b.Owner.Offset,
                    byteLength = b.Owner.ByteLength, address = NativeCollectibleCardLoadPlan.OverlayBase+b.Owner.Offset,
                    sha256 = Hash(overlay.AsSpan((int)b.Owner.Offset,(int)b.Owner.ByteLength)), b.IsPadding,
                    zeroInitialized = overlay.AsSpan((int)b.Owner.Offset,(int)b.Owner.ByteLength).IndexOfAnyExcept((byte)0) < 0,
                    bytesBase64 = b.Id is "sha" or "loader" or "control" ?
                        Convert.ToBase64String(overlay.AsSpan((int)b.Owner.Offset,(int)b.Owner.ByteLength)) : null }) },
            bootstrap = new { Composition.Bootstrap.EntryPc, Composition.Bootstrap.OriginalEntryPc,
                Composition.Bootstrap.LedgerStart, Composition.Bootstrap.LedgerEnd,
                tailOffset = 0x65E40, tailBytes = 0x1C0, Composition.Bootstrap.StubByteLength },
            ResearchOnly, ReadyStateIssued, CollectorEnabled, CardWrites, NativeInstallationApproved,
            RuntimeVerified, WholeEngineFitProven, LifetimeProven, NormalCreateBinApproved,
            verificationBoundary = "Source-derived block bytes for independent physical reconstruction; generated MIPS semantics are not independently established by these hashes."
        },new JsonSerializerOptions { WriteIndented = true });
    }
    internal byte[] BuildReceipt((string Hash,int Changed) verified) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile = Profile, PlanSha256, SourceImagePath, SourceCuePath,
        sourceImageSha256 = NativeMobySourceInventory.OriginalImageSha256, sourceCueSha256 = cue.SourceSha256,
        outputImageSha256 = verified.Hash, changedRawSectors = verified.Changed, outputCueSha256 = Hash(CopyCueBytes()),
        ExecutableSha256, OverlaySha256, OriginalWadSha256, WadSha256, RootSha256, Entries,
        GrowthBytes, RelocatedExecutableLba, FinalExecutableEndLba,
        manifestSha256 = Composition.Card.Loader.ManifestSha256, ordinarySlot = 0,
        copyAnchor = Composition.Card.Layout.CopyAnchor, Composition.Card.BeginPc, Composition.Card.PumpPc, Composition.Card.GatePc,
        Composition.Card.StateAddress, Composition.Card.ReadBufferAddress, Composition.Card.PrefixAddress,
        Composition.Card.InputAddress, Composition.Card.ShadowAddress,
        executableOwners = new[] { "3030+8", "1EAFC+8", "23E24+8", "10+4", "65DA4+4", "65E40+1C0" },
        overlayPrefixOwners = new[] { "2060+4 selected-tail Begin JAL", "21C8+4 shutdown Gate J" },
        liveReservation = "801FF600..801FF800 cold NOT-LOADED zeros; no live Ready copy",
        completeOriginalExecutableInverseVerified = true, completeOriginalOverlayInverseVerified = true,
        completeOriginalWadInverseVerified = true, wholeImagePhysicalReadback = true,
        ResearchOnly, ResearchDiscoveryPublicationEnabled, NativeInstallationApproved,
        ReadyStateIssued, CollectorEnabled, CardWrites, LifetimeProven, WholeEngineFitProven,
        RuntimeVerified, NormalCreateBinApproved, exceptionRollbackPublication = true,
        crashAtomicPublication = false, hostileFilesystemRaceSafe = false,
        pending = new[] { "actual native filename/read/callback/IRQ/BIOS completion", "cold boot and all transition RAM/stack lifetime",
            "Prepared shadow handoff into live Ready residence", "explicit new-file initialization and durable saves",
            "native collection, inventory X/X, completion percentage and full milestone integration" }
    },new JsonSerializerOptions { WriteIndented = true });
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
