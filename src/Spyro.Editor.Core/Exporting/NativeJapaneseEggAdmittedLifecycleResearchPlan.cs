using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Joins source decoder/consumer evidence to the ACTUAL successor projection.
/// Ancestor render rows and model roots are not final runtime addresses. This
/// immutable owner binds the same composition instance; no metadata reissue or
/// arbitrary projected-address input can produce a physical publication owner.
/// </summary>
internal sealed class NativeJapaneseEggAdmittedLifecycleResearchRenderBinding
{
    internal sealed record Row(string RowId,Guid InstanceId,uint BitIndex,ushort AuthoredClass,int NativeIndex,
        int RowSceneOffset,uint RowAddress,uint PrivateAddress,uint ModelRoot,uint AnimationHeader,byte NativeRenderMode,
        int X,int Y,int Z,int PackageDataOffset,int PackageByteLength,IReadOnlyList<NativeJapaneseEggLoaderAddress> LoaderFields);
    private readonly NativeJapaneseEggAdmittedLifecyclePlan composition;
    private NativeJapaneseEggAdmittedLifecycleResearchRenderBinding(NativeJapaneseEggAdmittedLifecyclePlan composition,
        NativeJapaneseEggRenderAdmissionPlan source)
    {
        this.composition = composition; SourceEvidence = source;
        RequireCurrent(composition);
        byte[] data = composition.CopyLevelData(); var authored = composition.Authored; var lifecycle = composition.Lifecycle;
        Rows = Array.AsReadOnly(lifecycle.Binding.Rows.Select(r =>
        {
            var model = lifecycle.Projection.Models.Single(m => m.RowId == r.RowId);
            var b = authored.Bindings.Single(b => b.RowId == r.RowId);
            var placement = authored.Integration.Rows.Single(p => p.Placement.Id == r.RowId);
            int at = checked((int)r.RowOwner.Offset);
            return new Row(r.RowId,r.InstanceId,r.BitIndex,r.AuthoredClass,placement.NativeIndex,b.RowSceneOffset,
                r.RowAddress,r.PrivateAddress,model.RootAddress,model.RootAddress+0x3C,data[at+0x4F],
                Int(data,at+12),Int(data,at+16),Int(data,at+20),b.PackageDataOffset,b.ByteLength,model.LoaderFields);
        }).ToArray());
    }
    internal NativeJapaneseEggRenderAdmissionPlan SourceEvidence { get; }
    internal IReadOnlyList<Row> Rows { get; }
    internal static NativeJapaneseEggAdmittedLifecycleResearchRenderBinding Capture(NativeRegionalSourceSession usa,
        NativeJapaneseEggAdmittedLifecyclePlan composition)
    {
        ArgumentNullException.ThrowIfNull(usa); ArgumentNullException.ThrowIfNull(composition);
        try
        {
            composition.RequireSourceComposition();
            return new(composition,NativeJapaneseEggRenderAdmissionPlan.Capture(usa,composition.Authored.Integration));
        }
        finally { usa.VerifyUnchanged(); }
    }
    internal void RequireCurrent(NativeJapaneseEggAdmittedLifecyclePlan actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (!ReferenceEquals(actual,composition)) throw new InvalidDataException("Final render binding belongs to another actual composition owner.");
        actual.RequireSourceComposition();
        var authored = actual.Authored; var lifecycle = actual.Lifecycle; var projection = lifecycle.Projection;
        projection.RequireCurrent(authored); lifecycle.Binding.RequireCurrent(authored,NativeCollectibleCardLoadResearchComposition.FixedManifest());
        if (!SourceEvidence.SourceCertified || !SourceEvidence.AnimatedListAndModelOwnersProven ||
            !ReferenceEquals(SourceEvidence.Integration,authored.Integration) ||
            !ReferenceEquals(lifecycle.Binding.Projection,projection) || !ReferenceEquals(projection.Layout,lifecycle.Layout) ||
            !actual.CopyLevelData().AsSpan().SequenceEqual(authored.CopyLevelData()) ||
            !actual.CopyArtisansOverlay().AsSpan().SequenceEqual(lifecycle.CopyOverlay()) ||
            !actual.Startup.FinalMemory.ModeledExtentsFit || actual.Startup.LedgerStackMargin < 0 ||
            actual.Startup.FinalMemory.SceneEnd != projection.SceneEnd || lifecycle.Binding.ModelAddress != projection.ModelAddress ||
            lifecycle.Binding.SceneAddress != projection.SceneAddress || lifecycle.LiveGuard is not { SourceBound: true } ||
            !authored.Bindings.Select(b => b.AuthoredDestinationClass).SequenceEqual(new ushort[] { 0x1E9,0x1F2 }))
            throw new InvalidDataException("Native source consumers are not bound to this actual final guarded resource projection.");
        byte[] data = actual.CopyLevelData(); int modelOffset = Int(data,0x10), sceneOffset = Int(data,0x18);
        foreach (var row in lifecycle.Binding.Rows)
        {
            var b = authored.Bindings.Single(b => b.RowId == row.RowId);
            var m = projection.Models.Single(m => m.RowId == row.RowId);
            var source = SourceEvidence.Rows.Single(r => r.Id == row.RowId);
            int at = checked(sceneOffset+b.RowSceneOffset);
            uint near = row.RowId == "egg-a" ? 0x332211u : 0xCCBBAAu, far = row.RowId == "egg-a" ? 0x665544u : 0xFFEEDDu;
            if (row.RowOwner.Offset != at || row.RowAddress != projection.SceneAddress+b.RowSceneOffset ||
                row.PrivateAddress != projection.SceneAddress+Word(data,at) || m.RowAddress != row.RowAddress ||
                m.PrivateAddress != row.PrivateAddress || m.AuthoredClass != row.AuthoredClass ||
                m.RootAddress != projection.ModelAddress+b.PackageDataOffset-modelOffset ||
                Word(data,b.RootFieldOffset) != b.PackageDataOffset || Half(data,b.ClassFieldOffset) != row.AuthoredClass ||
                Half(data,at+0x36) != row.AuthoredClass || data[at+0x48] != 0 || data[at+0x4F] != 1 || source.NativeRenderMode != 1 ||
                data[at+0x3C] != source.Animation || data[at+0x3E] != source.CurrentFrame || data[at+0x3F] != source.NextFrame ||
                b.Edit.Origin != NativeJapaneseEggPaletteOrigin.Authored || b.Edit.Fields.Count != 140 ||
                b.Edit.Fields.Any(f => f.FinalRgb != (f.Far ? far : near)) ||
                !b.Edit.Source.AssetAncestor.ProjectLoaderAddresses(m.RootAddress).SequenceEqual(m.LoaderFields))
                throw new InvalidDataException("Current final row/root/loader/palette owner differs from the fixed source-derived research profile.");
        }
        // The decoder windows are unchanged in the merged EXE, not merely the
        // source EXE from which the older render certificate was captured.
        byte[] executable = actual.CopyExecutable();
        foreach (var consumer in SourceEvidence.Consumers)
            if (Hash(executable.AsSpan(checked((int)(consumer.Pc-0x8000F800)),consumer.ByteLength)) != consumer.Sha256)
                throw new InvalidDataException("Merged executable changed a native renderer consumer.");
    }
    private static int Int(byte[] bytes,int at) => checked((int)Word(bytes,at));
    private static uint Word(byte[] bytes,int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static ushort Half(byte[] bytes,int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

/// <summary>
/// Closed exact-USA/JP, two-identity slot-zero admitted Artisans experiment.
/// Source composition, final projection and physical integrity are separate
/// from runtime, IRQ/lifetime, durable save and ordinary editor/export gates.
/// </summary>
public sealed class NativeJapaneseEggAdmittedLifecycleResearchPlan
{
    public const string Profile = "native-artisans-two-japanese-eggs-admitted-lifecycle-research-v1";
    public const string ImageName = "artisans-native-japanese-eggs-admitted-lifecycle-r01.bin";
    public const string CueName = "artisans-native-japanese-eggs-admitted-lifecycle-r01.cue";
    public const string ReceiptName = "native-japanese-eggs-admitted-lifecycle.receipt.json";
    private const string JapanHash = "1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7";
    internal static string[] OutputNames => [ImageName,ReceiptName,CueName];
    private static readonly (int Offset,int Length)[] ExecutableSpans =
        [(0x10,4),(0x2F00,0x50),(0x3030,8),(0x1EAFC,8),(0x23E24,8),(0x4AF18,8),(0x65DA4,4),(0x65E40,0x1C0)];
    private static readonly (int Offset,int Length)[] CardSpans = [(0x174,8),(0x2060,4),(0x21C8,4)];
    private static readonly (int Offset,int Length)[] ArtisansSpans = [(0x319C,8),(0x32C4,4)];
    private static readonly (int Offset,int Length)[] LifecycleSpans = [(0xE0AC,4),(0xE204,4)];
    private readonly NativePackedResearchImageOwner physical;
    private readonly NativeFreeFlightResearchSourceCue cue;
    private NativeJapaneseEggAdmittedLifecycleResearchPlan(string japan,NativeFreeFlightResearchSourceCue cue,
        NativeJapaneseEggAdmittedLifecyclePlan composition,NativeJapaneseEggAdmittedLifecycleResearchRenderBinding render,
        NativePackedResearchImageOwner physical)
    {
        JapaneseSourceImagePath = japan; this.cue = cue; Composition = composition; FinalRenderBinding = render; this.physical = physical;
        RequireStageReversion();
        ExecutableSha256 = Hash(CopyExecutable()); CardOverlaySha256 = Hash(composition.CopyCardOverlay());
        ArtisansOverlaySha256 = Hash(composition.CopyArtisansOverlay()); LevelDataSha256 = Hash(composition.CopyLevelData());
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new { Profile, JapaneseSourceImagePath, SourceImagePath, SourceCuePath,
            sourceUsaSha256 = NativeMobySourceInventory.OriginalImageSha256, sourceJapanSha256 = JapanHash, sourceCueSha256 = cue.SourceSha256,
            lifecycle = composition.Lifecycle.PlanSha256, startup = composition.Startup.PlanSha256,
            manifestSha256 = composition.Lifecycle.Collector.ManifestSha256, ordinarySlot = 0,
            ExecutableSha256, CardOverlaySha256, ArtisansOverlaySha256, LevelDataSha256, OriginalWadSha256, WadSha256, RootSha256,
            Entries, GrowthBytes, RelocatedExecutableLba, FinalExecutableEndLba, finalRows = render.Rows,
            GuardedReadyAdmissionEmitted, GuardedCollectionEmitted, RuntimeVerified, LifetimeProven, InterruptSafetyProven, CardWrites, NormalCreateBinApproved }));
    }
    internal NativeJapaneseEggAdmittedLifecyclePlan Composition { get; }
    internal NativeJapaneseEggAdmittedLifecycleResearchRenderBinding FinalRenderBinding { get; }
    public string JapaneseSourceImagePath { get; }
    public string SourceImagePath => physical.SourceImagePath;
    public string SourceCuePath => cue.CuePath;
    public string PlanSha256 { get; }
    public string ExecutableSha256 { get; }
    public string CardOverlaySha256 { get; }
    public string ArtisansOverlaySha256 { get; }
    public string LevelDataSha256 { get; }
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
    public bool GuardedCollectionEmitted => true;
    public bool RuntimeVerified => false;
    public bool ReadyStateIssued => false;
    public bool NativeInstallationApproved => false;
    public bool LifetimeProven => false;
    public bool InterruptSafetyProven => false;
    public bool WholeEngineFitProven => false;
    public bool CardWrites => false;
    public bool PersistentSaveChanges => false;
    public bool NormalCreateBinApproved => false;
    internal DiscLayout Layout => physical.Layout;
    public static async Task<NativeJapaneseEggAdmittedLifecycleResearchPlan> CaptureAsync(string japaneseImagePath,string originalImagePath,
        string originalCuePath,CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); ArgumentException.ThrowIfNullOrWhiteSpace(japaneseImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath); ArgumentException.ThrowIfNullOrWhiteSpace(originalCuePath);
        string japanPath = Path.GetFullPath(japaneseImagePath), usaPath = Path.GetFullPath(originalImagePath);
        var cue = NativeFreeFlightResearchSourceCue.Capture(originalCuePath,usaPath,ImageName);
        using var japan = NativeRegionalSourceSession.Open(japanPath,NativeDiscRevision.JapanScps10083);
        try
        {
            using var usa = NativeRegionalSourceSession.Open(usaPath,NativeDiscRevision.UsaScus94228);
            try
            {
                var composition = NativeJapaneseEggAdmittedLifecyclePlan.Capture(japan,usa);
                var render = NativeJapaneseEggAdmittedLifecycleResearchRenderBinding.Capture(usa,composition);
                var physical = NativePackedResearchImageOwner.CaptureAdmittedEgg(usaPath,composition,render);
                var plan = new NativeJapaneseEggAdmittedLifecycleResearchPlan(japanPath,cue,composition,render,physical);
                await plan.RequireFreshAsync(token); return plan;
            }
            finally { usa.VerifyUnchanged(); }
        }
        finally { try { japan.VerifyUnchanged(); } finally { cue.RequireFresh(); } }
    }
    public async Task RequireFreshAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); cue.RequireFresh();
        try
        {
            await physical.RequireFreshAsync(token);
            using var japan = NativeRegionalSourceSession.Open(JapaneseSourceImagePath,NativeDiscRevision.JapanScps10083);
            japan.VerifyUnchanged(); token.ThrowIfCancellationRequested();
        }
        finally { cue.RequireFresh(); }
    }
    public void RequireNativeInstallation() => throw new InvalidDataException(
        "Research code publication is not runtime, all-transition lifetime, IRQ, durable save or normal editor/export approval.");
    internal byte[] CopyExecutable() => physical.CopyExecutable();
    internal byte[] CopyWad() => physical.CopyWad();
    internal byte[] CopyRoot() => physical.CopyRoot();
    internal byte[] CopyCueBytes() => cue.CopyOutputBytes();
    internal byte[] RevertExecutableExact(ReadOnlySpan<byte> bytes) => Composition.RevertExecutableExact(bytes);
    internal void RequireStageReversion()
    {
        Composition.RequireSourceComposition(); FinalRenderBinding.RequireCurrent(Composition);
        if (!Composition.EntryOwners.Select(e => e.Index).SequenceEqual(new[] {2,9,10}) ||
            !CopyExecutable().AsSpan().SequenceEqual(Composition.CopyExecutable()) ||
            !RevertExecutableExact(CopyExecutable()).AsSpan().SequenceEqual(Composition.CopyOriginalExecutable()))
            throw new InvalidDataException("Admitted research requires its exact three source owners and complete merged EXE inverse.");
        byte[] wad = CopyWad(); int growth = 0;
        foreach (var owner in Composition.EntryOwners)
        {
            var packed = Entries.Single(e => e.Index == owner.Index);
            byte[] after = owner.Index switch { 2 => Composition.CopyCardOverlay(), 9 => Composition.CopyArtisansOverlay(), _ => Composition.CopyLevelData() };
            if (packed.OriginalLength != owner.OriginalLength || packed.FinalLength != owner.FinalLength ||
                packed.OriginalSha256 != owner.OriginalSha256 || packed.FinalSha256 != owner.FinalSha256 ||
                after.Length != owner.FinalLength || Hash(after) != owner.FinalSha256 ||
                !wad.AsSpan(packed.FinalOffset,packed.FinalLength).SequenceEqual(after))
                throw new InvalidDataException("Packed admitted owner differs from the complete source-issued composition.");
            growth = checked(growth+owner.FinalLength-owner.OriginalLength);
        }
        if (growth != GrowthBytes || growth % 0x800 != 0 || RelocatedExecutableLba != 53875+growth/0x800 ||
            FinalExecutableEndLba != RelocatedExecutableLba+CopyExecutable().Length/0x800 ||
            Entries.Any(e => e.Index is not (2 or 9 or 10) && (e.OriginalLength != e.FinalLength || e.OriginalSha256 != e.FinalSha256)))
            throw new InvalidDataException("Admitted physical growth differs from actual verified owners or changed another payload.");
        RequireComplement(Composition.CopyOriginalExecutable(),CopyExecutable(),ExecutableSpans);
        RequireComplement(Composition.Admission.CopyOriginalOverlay(),Composition.CopyCardOverlay().AsSpan(0,0x3800),CardSpans);
        RequireComplement(Composition.Authored.Integration.CopyOriginalOverlay(),Composition.CopyArtisansOverlay().AsSpan(0,0xE000),ArtisansSpans);
        RequireComplement(Composition.Authored.CopyOverlay(),Composition.CopyArtisansOverlay().AsSpan(0,0xE800),LifecycleSpans);
        physical.RequireOriginalWadReversion();
    }
    private static void RequireComplement(ReadOnlySpan<byte> before,ReadOnlySpan<byte> after,(int Offset,int Length)[] spans)
    {
        if (before.Length != after.Length) throw new InvalidDataException("Research complement length changed.");
        int cursor = 0;
        foreach (var span in spans)
        {
            if (span.Offset < cursor || span.Length <= 0 || span.Offset+span.Length > before.Length ||
                !before.Slice(cursor,span.Offset-cursor).SequenceEqual(after.Slice(cursor,span.Offset-cursor)))
                throw new InvalidDataException("Research complement changed outside its bounded disjoint patch owners.");
            cursor = span.Offset+span.Length;
        }
        if (!before[cursor..].SequenceEqual(after[cursor..])) throw new InvalidDataException("Research trailing complement changed.");
    }
    internal void WriteNewImage(string path,CancellationToken token = default) => physical.WriteNewImage(path,token);
    internal (string Hash,int Changed) VerifyImage(string path)
    {
        var result = physical.VerifyImage(path);
        using var image = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read); var layout = DiscImage.DetectLayout(image);
        _ = RevertExecutableExact(DiscImage.ReadFileBytes(image,layout,RelocatedExecutableLba,0,0x66000));
        foreach (int index in new[] {2,9,10})
        {
            var entry = Entries.Single(e => e.Index == index);
            byte[] bytes = DiscImage.ReadFileBytes(image,layout,37,entry.FinalOffset,entry.FinalLength);
            _ = index switch { 2 => Composition.RevertCardOverlayExact(bytes), 9 => Composition.RevertArtisansOverlayExact(bytes), _ => Composition.RevertLevelDataExact(bytes) };
        }
        return result;
    }
    internal byte[] ExpectedSector(int lba,byte[] before) => physical.ExpectedSector(lba,before);
    internal byte[] BuildDescription() => Describe(null);
    internal byte[] BuildReceipt((string Hash,int Changed) verified) => Describe(verified);
    private byte[] Describe((string Hash,int Changed)? verified)
    {
        var p = Composition; var admission = p.Admission; var reader = admission.Reader; var cardKernel = admission.Kernel;
        var lifecycle = p.Lifecycle; var projection = lifecycle.Projection; var live = lifecycle.LiveGuard!;
        byte[] card = p.CopyCardOverlay(), artisans = p.CopyArtisansOverlay(), data = p.CopyLevelData();
        var manifest = NativeCollectibleCardLoadResearchComposition.FixedManifest();
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = Profile, ImageName, CueName, ReceiptName, PlanSha256, JapaneseSourceImagePath, SourceImagePath, SourceCuePath,
            sourceUsaSha256 = NativeMobySourceInventory.OriginalImageSha256, sourceJapanSha256 = JapanHash, sourceCueSha256 = cue.SourceSha256,
            originalExecutableSha256 = Hash(p.CopyOriginalExecutable()), ExecutableSha256, CardOverlaySha256, ArtisansOverlaySha256, LevelDataSha256,
            OriginalWadSha256, WadSha256, RootSha256, GrowthBytes, RelocatedExecutableLba, FinalExecutableEndLba, Entries,
            sourceWadLba = 37, originalWadBytes = 0x6927000, finalWadBytes = 0x6927000+GrowthBytes, Layout.RootExtent, Layout.RootLength,
            outputImageSha256 = verified?.Hash, changedRawSectors = verified?.Changed, outputCueSha256 = Hash(CopyCueBytes()),
            manifest = new { contentId = NativeCollectibleCardLoadResearchComposition.ContentId, sha256 = manifest.Sha256,
                identities = manifest.Entries.Select(e => e.InstanceId), ordinarySlot = 0 },
            executablePatchSpans = Spans(p.CopyOriginalExecutable(),CopyExecutable(),ExecutableSpans),
            card = new { runtimeBase = 0x8007AA38u, originalByteLength = 0x3800, cardKernel.Layout.RetainedByteLength,
                cardKernel.Layout.UsedByteLength, cardKernel.Layout.FinalByteLength, cardKernel.Layout.CopyAnchor,
                cardKernel.EntryPc, cardKernel.BeginPc, cardKernel.GatePc, cardKernel.PublishPc, cardKernel.StateAddress,
                readerStateAddress = reader.StateAddress, reader.ReadBufferAddress, reader.PrefixAddress, reader.ShadowAddress, reader.InputAddress,
                patchSpans = Spans(admission.CopyOriginalOverlay(),card,CardSpans),
                blocks = Blocks(card,reader.Layout.Blocks.Concat(cardKernel.Layout.Blocks),false) },
            artisans = new { runtimeBase = 0x8007AA38u, originalByteLength = 0xE000, lifecycle.Layout.RetainedByteLength,
                lifecycle.Layout.UsedByteLength, lifecycle.Layout.FinalByteLength, lifecycle.Layout.CopyAnchor,
                lifecycle.PreBridgePc, lifecycle.PostBridgePc, lifecycle.Lifecycle.BeforeVisualEntryPc, lifecycle.Lifecycle.AfterVisualEntryPc,
                patchSpans = Spans(p.Authored.Integration.CopyOriginalOverlay(),artisans,ArtisansSpans),
                retainedAuthoredPatchSpans = Spans(p.Authored.CopyOverlay(),artisans,LifecycleSpans),
                blocks = Blocks(artisans,p.Authored.OverlayLayout.Blocks.Concat(lifecycle.Layout.Blocks),true) },
            guard = new { live.BeforeEntryPc, live.AfterEntryPc, live.ValidatePc, live.CodeByteLength,
                offset = live.Owner.Offset, byteLength = live.Owner.ByteLength, sha256 = Hash(live.CopyBytes()),
                bytesBase64 = Convert.ToBase64String(live.CopyBytes()),
                expectedGlobals = live.ExpectedSceneGlobals.Select(g => new { address = g.Address, value = g.Value }),
                additionalWordGuards = new[] { new { address = 0x800755A4u, value = 0xA00u },
                    new { address = 0x80078D14u, value = 1u }, new { address = 0x80078D1Cu, value = 0u } },
                ledger = new { address = 0x801FF600u, byteLength = 0x200, readyPhaseOffset = 0x16, readyPhase = 1,
                    slotOffset = 0x17, ordinarySlot = 0, controllerOffset = 0x18, nativeControllerAddress = 0x80078D18u,
                    nativeControllerShift = 4, markerOffset = 0x1A, nativeMarkerAddress = 0x80078D20u, epochOffset = 0x8C, epochMustBeNonzero = true },
                incomingSpMinimum = NativeJapaneseEggLiveGuardKernel.StackFloor+NativeJapaneseEggLiveGuardKernel.NestedBytes,
                incomingSpMaximum = NativeJapaneseEggLiveGuardKernel.StackEnd-NativeJapaneseEggLiveGuardKernel.CallerBytes,
                boundRowOnly = true, unboundVisualPreserved = true, codeStores = 0, addedStackBytes = 0 },
            projectedRows = FinalRenderBinding.Rows,
            projectedModels = projection.Models,
            projectedScene = new { projection.CopyAnchor, projection.ModelAddress, projection.SceneAddress, projection.SceneEnd,
                projection.NormalGap, lifecycle.Lifecycle.DynamicBase, lifecycle.Binding.SceneByteLength, lifecycle.Binding.ModelByteLength },
            levelData = new { byteLength = data.Length, sha256 = LevelDataSha256,
                originalSha256 = Hash(p.Authored.Integration.CopyOriginalLevelData()), unchangedAuthoredData = true,
                headerBase64 = Convert.ToBase64String(data.AsSpan(0,0x800)),
                packages = p.Authored.Bindings.Select(b => new { b.RowId, b.AuthoredDestinationClass, b.RegistrationSlot,
                    b.RootFieldOffset, b.ClassFieldOffset, b.PackageDataOffset, b.ByteLength,
                    sha256 = Hash(data.AsSpan(b.PackageDataOffset,b.ByteLength)), nearRgb = b.RowId == "egg-a" ? 0x332211u : 0xCCBBAAu,
                    farRgb = b.RowId == "egg-a" ? 0x665544u : 0xFFEEDDu, paletteMeaning = "fixed diagnostic authored RGB, not recovered native colors" }),
                rows = lifecycle.Binding.Rows.Select(r => new { r.RowId, offset = r.RowOwner.Offset,
                    rowBase64 = Convert.ToBase64String(data.AsSpan((int)r.RowOwner.Offset,0x58)),
                    privateOffset = r.PrivateOwner.Offset, privateBase64 = Convert.ToBase64String(data.AsSpan((int)r.PrivateOwner.Offset,12)) }) },
            sourceRenderConsumers = FinalRenderBinding.SourceEvidence.Consumers,
            sourceRenderBoundary = "Consumer semantics only; all observer addresses above use the final guarded projection, not ancestor render rows or roots.",
            memory = p.Startup.FinalMemory, p.Startup.WorstSynchronousStackLow, p.Startup.LedgerStackMargin,
            completeOriginalExecutableInverse = true, completeOriginalThreeEntryInverse = true, completeOriginalWadInverse = true,
            wholeImagePhysicalReadback = verified is not null, exactCueAndReceiptReadback = verified is not null,
            exceptionRollback = true, crashAtomic = false, hostileConcurrentFilesystemSafety = false,
            ResearchOnly, ResearchDiscoveryPublicationEnabled, GuardedReadyAdmissionEmitted, GuardedCollectionEmitted,
            ReadyStateIssued, NativeInstallationApproved, RuntimeVerified, LifetimeProven, InterruptSafetyProven, WholeEngineFitProven,
            CardWrites, PersistentSaveChanges, NormalCreateBinApproved,
            boundary = "Guarded admission and Artisans collection code emitted in a separate research profile. No runtime, all-transition/IRQ lifetime, save/card-write or normal Create BIN approval.",
            pending = new[] { "exact full-firmware disc entry and guarded collection observations", "death/re-entry/reset and all overlay/IRQ/DMA lifetimes",
                "whole-engine peak fit and native pixels/gameplay", "durable save/write persistence and power-loss behavior", "normal editor/export approval" }
        },new JsonSerializerOptions { WriteIndented = true });
    }
    private static object[] Spans(byte[] before,byte[] after,(int Offset,int Length)[] spans) => spans.Select(s => (object)new
    { offset = s.Offset, byteLength = s.Length, originalSha256 = Hash(before.AsSpan(s.Offset,s.Length)),
        sha256 = Hash(after.AsSpan(s.Offset,s.Length)), bytesBase64 = Convert.ToBase64String(after.AsSpan(s.Offset,s.Length)) }).ToArray();
    private static object[] Blocks(byte[] after,IEnumerable<NativeOwnedOverlayAllocation> blocks,bool allCode) => blocks.Select(b => (object)new
    {
        id = b.Id, offset = b.Owner.Offset, byteLength = b.Owner.ByteLength, address = 0x8007AA38+b.Owner.Offset, b.IsPadding,
        sha256 = Hash(after.AsSpan((int)b.Owner.Offset,(int)b.Owner.ByteLength)),
        zeroInitialized = after.AsSpan((int)b.Owner.Offset,(int)b.Owner.ByteLength).IndexOfAnyExcept((byte)0) < 0,
        bytesBase64 = !b.IsPadding && (allCode || b.Id is "sha" or "loader" or "control" or "admission-control") ?
            Convert.ToBase64String(after.AsSpan((int)b.Owner.Offset,(int)b.Owner.ByteLength)) : null
    }).ToArray();
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
