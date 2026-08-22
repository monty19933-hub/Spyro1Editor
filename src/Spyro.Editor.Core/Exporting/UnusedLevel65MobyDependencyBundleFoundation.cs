using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal enum UnusedLevel65MobyDependencyEvidenceState
{
    ExactPinnedPreimage,
    ExactDerivedAllocation,
    NotRequiredByDecodedGrammar,
    ExcludedByPinnedDonorIdentity,
    UnresolvedHardBlocker
}

internal enum UnusedLevel65MobyDependencyKind
{
    SourceRecord,
    SceneProperties,
    ScenePointerFixups,
    ActorModelPackage,
    Animation,
    TexturePixelsAndCluts,
    OverlayDispatchAndController,
    ControllerPropertySemantics,
    SoundAndParticles,
    NativeAndEditorLinks,
    RewardsTotalsAndPersistence,
    DestinationObjectRows,
    DestinationActorRoots,
    DestinationSceneStorage,
    RuntimeAcceptance
}

internal sealed record UnusedLevel65MobyDependencyRequirement(
    string Id,
    UnusedLevel65MobyDependencyKind Kind,
    bool RequiredForRunnableBundle,
    UnusedLevel65MobyDependencyEvidenceState EvidenceState,
    IReadOnlyList<string> DependsOn,
    string Evidence);

internal sealed record UnusedLevel65MobyFaceStreamManifest(
    string Tier,
    int PackageRelativeOffset,
    int BodyByteLength,
    int TotalByteLength,
    int RecordCount,
    int UntexturedTriangleCount,
    int TexturedRecordCount,
    string Sha256,
    bool EndsExactlyUnderRetailRendererGrammar);

internal sealed record UnusedLevel65MobySourceRecordManifest(
    int TrueIndex,
    long WadOffset,
    int SceneRelativeOffset,
    int ByteLength,
    string Hex,
    string Sha256,
    uint SceneRelativePropertiesPointer,
    uint LegacySpecialDataPointer,
    ushort ActorId,
    byte RenderType,
    byte RuntimeState,
    byte SpecularType,
    byte UpdateDistance,
    byte DropActorOrClass,
    byte PodOrGroup,
    byte CullingSector,
    byte RendererDistance,
    string Identity,
    string IdentityEvidence,
    bool IsDragon,
    bool IsPortal,
    bool IsThief,
    bool IsCollectible,
    bool IsTotalsLinked,
    bool HasRewardDrop,
    bool HasLegacySpecialDataPointer);

internal sealed record UnusedLevel65MobyScenePropertiesManifest(
    long SceneWadOffset,
    int SceneByteLength,
    int ObjectCount,
    int ObjectTableSceneOffset,
    int PropertiesSceneOffset,
    int PropertiesByteLength,
    string PropertiesHex,
    string PropertiesSha256,
    IReadOnlyList<int> SharingTrueIndices,
    int NextDistinctPropertiesSceneOffset,
    int SourcePointerFieldSceneOffset,
    int SourcePointerFixupCount,
    int SourcePointerFixupListSceneOffset,
    int SourcePointerFixupActiveByteLength,
    string SourcePointerFixupActiveSha256,
    int PointerFieldOccurrenceCount,
    IReadOnlyList<int> InternalPropertiesPointerFixups,
    bool PropertiesPointerIsSceneRelative,
    bool LegacySpecialDataPointerIsPropertiesPointer);

internal sealed record UnusedLevel65MobyActorPackageManifest(
    int DonorWadEntry,
    long DataEntryWadOffset,
    int DataEntryByteLength,
    int ActorSubfileRelativeOffset,
    int ActorSubfileByteLength,
    int ActorRootIndex,
    int ActorRootEntryRelativeOffset,
    int ActorPackageByteLength,
    string ActorPackageSha256,
    int AnimationCount,
    int ModelDataRelativeOffset,
    int PackageInternalEntryRelativeReferenceCount,
    UnusedLevel65MobyFaceStreamManifest NormalFaces,
    UnusedLevel65MobyFaceStreamManifest FarLodFaces,
    int TotalTexturedFaceCount,
    bool TexturePixelsRequired,
    bool ClutsRequired,
    bool PackageRebaseRequired);

internal sealed record UnusedLevel65MobyDestinationAllocator(
    int TargetWadEntry,
    long DataEntryWadOffset,
    int DataEntryByteLength,
    long SceneWadOffset,
    int SceneByteLength,
    int ExistingObjectCount,
    int ObjectTableSceneOffset,
    long ObjectTableWadOffset,
    string ExistingObjectTableSha256,
    int FirstAppendTrueIndex,
    int FirstAppendRowSceneOffset,
    long FirstAppendRowWadOffset,
    string FirstAppendRowSha256,
    int ContiguousZeroRowBytes,
    int CompleteAppendRowCapacity,
    int RemainingBytesAfterCompleteRows,
    string ContiguousZeroRowsSha256,
    int PlayerAnchorTrueIndex,
    string PlayerAnchorSha256,
    int ActorSubfileRelativeOffset,
    int ActorSubfileByteLength,
    int ExistingActorRootCount,
    int NewActorRootIndex,
    int NewActorRootSlotEntryRelativeOffset,
    int NewActorIdSlotEntryRelativeOffset,
    int LastActorRootEntryRelativeOffset,
    int LastActorUsedByteLength,
    string LastActorUsedSha256,
    int ActorTailEntryRelativeOffset,
    int ActorTailByteLength,
    string ActorTailSha256,
    int NewActorRootEntryRelativeOffset,
    int NewActorPackageEndEntryRelativeOffset,
    int ActorTailBytesRemaining,
    int ScenePointerFixupCountOffset,
    int ScenePointerFixupListOffset,
    int ScenePointerFixupCountBefore,
    int ScenePointerFixupCountAfter,
    int ScenePointerFixupActiveByteLength,
    string ScenePointerFixupActiveSha256,
    int ScenePointerFixupAppendOffset,
    int AppendedPointerFieldSceneOffset,
    int PropertiesAllocationSceneOffset,
    int PropertiesAllocationByteLength,
    int SceneTailBytesRemaining,
    string SceneTailPreimageSha256,
    IReadOnlyList<int> FuturePlacementMutationOffsets,
    bool RequiresSourceCountIncrement,
    bool RequiresRowPointerFixupAppend,
    bool RequiresInternalPropertiesFixupAppend,
    bool RequiresActorPackageRebase,
    bool RequiresTextureAllocation,
    bool RequiresNestedSubfileRelocation,
    bool RequiresDataEntryGrowth,
    bool StructuralAllocationComplete);

internal sealed record UnusedLevel65MobyDispatchTraceManifest(
    string LevelKey,
    string LevelName,
    int OverlayWadEntry,
    long OverlayWadOffset,
    int OverlayByteLength,
    string OverlaySha256,
    uint OverlayLoadAddress,
    ushort ActorId,
    uint ClassLoadAddress,
    uint ClassLoadWord,
    uint DispatchStartAddress,
    uint DecisionWindowAddress,
    int DecisionWindowByteLength,
    string DecisionWindowSha256,
    uint DefaultLoopExitAddress,
    string DefaultLoopExitSha256,
    IReadOnlyList<uint> ExecutedInstructionAddresses,
    IReadOnlyList<uint> ExecutedInstructionWords,
    string ExecutedTraceSha256,
    ushort? AdjacentDedicatedClassId,
    uint? AdjacentDedicatedHandlerAddress,
    int CallInstructionCount,
    int PropertiesReadCount,
    int RuntimeActorWriteCount,
    bool DedicatedHandlerPresent,
    bool ReachesDefaultLoopExit,
    bool ClassSpecificControllerPresent);

internal sealed record UnusedLevel65MobyGlobalExecutableManifest(
    int Lba,
    int ByteLength,
    string Sha256,
    int HeaderByteLength,
    uint InitialProgramCounter,
    uint TextLoadAddress,
    int TextByteLength,
    string TextSha256,
    ushort ActorId,
    int AlignedITypeImmediateOccurrenceCount);

internal sealed record UnusedLevel65MobyBehaviorClosureManifest(
    UnusedLevel65MobyDispatchTraceManifest DonorDispatch,
    UnusedLevel65MobyDispatchTraceManifest TargetDispatch,
    UnusedLevel65MobyGlobalExecutableManifest GlobalExecutable,
    IReadOnlyList<uint> ExactPropertiesWords,
    bool PropertiesContainPointers,
    bool PropertiesControllerSemanticsRequired,
    bool SoundDependencyRequired,
    bool ParticleOrEffectDependencyRequired,
    bool DynamicSpawnDependencyRequired,
    bool DynamicNativeLinkDependencyRequired,
    bool StaticRuntimeDependencyClosureComplete);

internal sealed record UnusedLevel65MobyExporterCompatibilityBoundary(
    string PropertiesPointerPolicy,
    string LegacySpecialDataPointerPolicy,
    bool UsesSharedMobySourcePatchExporter,
    bool UsesSharedCrossLevelRecipeRegistry,
    bool ExistingActorPackageRecipeIsSufficient,
    IReadOnlyList<string> ExactReasons);

internal sealed record UnusedLevel65MobyDependencyBundleContract(
    string ProfileId,
    int SchemaVersion,
    string FoundationImagePath,
    string FoundationImageSha256,
    string LockedIndependentBaseImageSha256,
    string LockedIndependentObjectTableSha256,
    string DonorId,
    string DonorLevelKey,
    string DonorLevelName,
    UnusedLevel65MobySourceRecordManifest SourceRecord,
    UnusedLevel65MobyScenePropertiesManifest Properties,
    UnusedLevel65MobyActorPackageManifest ActorPackage,
    UnusedLevel65MobyDestinationAllocator Destination,
    UnusedLevel65MobyBehaviorClosureManifest BehaviorClosure,
    IReadOnlyList<UnusedLevel65MobyDependencyRequirement> Dependencies,
    IReadOnlyList<string> HardRuntimePublicationBlockers,
    UnusedLevel65MobyExporterCompatibilityBoundary ExporterBoundary,
    string DeterministicContractSha256,
    bool StructurallyCompleteAllocation,
    bool StaticRuntimeDependencyClosureComplete,
    bool RunnableBundleSupport,
    bool StaticInspectionOnly,
    bool ProducesPatches,
    bool WritesBin,
    bool WritesCue,
    bool DisposableRuntimeCandidateAuthorized,
    bool AppIntegrated,
    bool NormalCreateBinEnabled,
    bool ReleasePublicationAuthorized);

/// <summary>
/// Read-only dependency-bundle schema and first cross-level ID65 allocator.
/// The first structural donor is Artisans Grass T121 (actor 0x01F5): its row,
/// scene properties, fixup membership, actor package, normal faces, and far-LOD
/// faces are exact, and the package is entirely untextured. Exact donor and
/// destination dispatcher traces prove that class 0x01F5 takes the default
/// no-update exit in both overlays. Consequently the two-word properties block
/// has no class controller consumer and no sound, particle, spawn, or dynamic
/// link dependency. Runtime acceptance remains deliberately fail-closed.
/// </summary>
internal static class UnusedLevel65MobyDependencyBundleFoundation
{
    public const string ProfileId =
        "unused-level-65-moby-dependency-bundle-artisans-grass-static-closure-v2";
    public const int SchemaVersion = 2;
    public const string ExpectedFoundationImageSha256 =
        "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
    public const string LockedIndependentBaseImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string DonorId = "artisans:t121:actor-01f5:grass-untextured-no-update-v2";
    public const string ExpectedContractSha256 =
        "8ec952c5972d7e2a37e5e89f8ec2be26144635b92b58cba85f745f7d05a76d4f";

    private const int WadLba = 37;
    private const int ExpectedWadByteLength = 0x6C18800;
    private const int RecordStride = 0x58;
    private const uint OverlayLoadAddress = 0x8007AA38;

    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const int ExecutableHeaderByteLength = 0x800;
    private const uint ExecutableInitialProgramCounter = 0x8005B8E0;
    private const uint ExecutableTextLoadAddress = 0x80010000;
    private const int ExecutableTextByteLength = 0x65800;
    private const string ExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    private const string ExecutableTextSha256 =
        "94eb7b3679075fdd2a856316abe1ac1278819a36a4e12584149d683715e70c5a";

    private const int ArtisansOverlayEntry = 9;
    private const long ArtisansOverlayWadOffset = 0x7F2800;
    private const int ArtisansOverlayByteLength = 0xE000;
    private const string ArtisansOverlaySha256 =
        "7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55";
    private const uint ArtisansClassLoadAddress = 0x8007DA74;
    private const uint ArtisansClassLoadWord = 0x86630036;
    private const uint ArtisansDispatchStartAddress = 0x8007DA9C;
    private const uint ArtisansDecisionWindowAddress = 0x8007DBDC;
    private const int ArtisansDecisionWindowByteLength = 0x128;
    private const string ArtisansDecisionWindowSha256 =
        "ba55f492574c72c1eed3c21a4cf20c57dc49fe07136f9156b422ca4825455c40";
    private const uint ArtisansDefaultLoopExitAddress = 0x80085780;
    private const string ArtisansDefaultLoopExitSha256 =
        "427d89f344d0cc0fde0faa67e9ab199d67cf68510d3433478cdf646c50a4c61b";
    private const string ArtisansTraceSha256 =
        "ffdacdf97f86f1aed4505e0869ee2a9bfbdaad92025704fd33d1d4bfb9ba6d88";
    private const int ArtisansDataEntry = 10;
    private const long ArtisansDataWadOffset = 0x800800;
    private const int ArtisansDataByteLength = 0x383000;
    private const int ArtisansActorSubfileOffset = 0x17C000;
    private const int ArtisansActorSubfileByteLength = 0x4D800;
    private const int ArtisansSceneOffset = 0x1C9800;
    private const int ArtisansSceneByteLength = 0x14800;
    private const int ArtisansObjectCount = 174;
    private const int ArtisansObjectTableOffset = 0xA2AC;
    private const int GrassTrueIndex = 121;
    private const int GrassRowSceneOffset = 0xCC44;
    private const string GrassRowSha256 =
        "de8450049c0bea92fba8fe4e7f9f9cb749a514d1318dbd83dc9b6b1b18a0b190";
    private const uint GrassPropertiesOffset = 0x11ACC;
    private const int GrassPropertiesByteLength = 8;
    private const string GrassPropertiesSha256 =
        "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b";
    private const int ArtisansFixupCountOffset = 0x13ED8;
    private const int ArtisansFixupCount = 0xC9;
    private const int ArtisansFixupActiveByteLength = 4 + (ArtisansFixupCount * 4);
    private const string ArtisansFixupActiveSha256 =
        "7d677b0b8d6ad795a6319bd31e781c4e5f52a76be67ed81ed1b1c6fbdb4775fb";
    private const ushort GrassActorId = 0x01F5;
    private const int GrassActorRootIndex = 22;
    private const int GrassActorRoot = 0x1C1520;
    private const int GrassActorNextRoot = 0x1C1694;
    private const string GrassActorPackageSha256 =
        "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3";
    private const string GrassNormalFacesSha256 =
        "f06f9c3ff1ba9bebb7379507144b595b1c326681c0713063892ae15e16162b3b";
    private const string GrassFarLodFacesSha256 =
        "b30459d48479636f8f1c385e3a5e86df21a24637806c5e5699553ec789e75c94";

    private const int Id65OverlayEntry = 79;
    private const long Id65OverlayWadOffset = 0x6927000;
    private const int Id65OverlayByteLength = 0xF800;
    private const string Id65OverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    private const uint Id65ClassLoadAddress = 0x8007DB0C;
    private const uint Id65ClassLoadWord = 0x86640036;
    private const uint Id65DispatchStartAddress = 0x8007DB34;
    private const uint Id65DecisionWindowAddress = 0x8007DC70;
    private const int Id65DecisionWindowByteLength = 0x128;
    private const string Id65DecisionWindowSha256 =
        "fc27e64af0f02e3617fd9b791759ea4b2663b9bfc6aeb19abb885f68d41e5b44";
    private const uint Id65DefaultLoopExitAddress = 0x80087258;
    private const string Id65DefaultLoopExitSha256 =
        "edb2f325f44eafb8149e0f2b93912a9009e7aea08c77d17bdeb71bb6af20e372";
    private const string Id65TraceSha256 =
        "cb3c5bf3677e27833294c86b83ea12e6a1d5a91fd8cd155aa548f1f59f02809f";
    private const ushort Id65AdjacentDedicatedClassId = 0x01F6;
    private const uint Id65AdjacentDedicatedHandlerAddress = 0x800870F0;
    private const int Id65DataEntry = 80;
    private const long Id65DataWadOffset = 0x6936800;
    private const int Id65DataByteLength = 0x2E2000;
    private const int Id65ActorSubfileOffset = 0x173000;
    private const int Id65ActorSubfileByteLength = 0x5D000;
    private const int Id65SceneOffset = 0x1D0000;
    private const int Id65SceneByteLength = 0x8800;
    private const int Id65ObjectCount = 107;
    private const int Id65ObjectTableOffset = 0x170;
    private const string Id65ObjectTableSha256 =
        "2d5743b6895cb6142150812e06eb772b492ab21665edec239e17e98b9ed2af1d";
    private const int Id65FirstAppendRowOffset = 0x2638;
    private const int Id65ContiguousZeroRowBytes = 0x2800;
    private const string Id65ContiguousZeroRowsSha256 =
        "84ff92691f909a05b224e1c56abb4864f01b4f8e3c854e4bb4c7baf1d3f6d652";
    private const string ZeroRecordSha256 =
        "10eef285deef7a4b7c82b22aa53589b7833df29de3814649c772bbd5c832f365";
    private const int Id65PlayerAnchorTrueIndex = 92;
    private const string Id65PlayerAnchorSha256 =
        "4987c539f3178c555da26e4ec2f6cdc25094a27382b9465c9845846a39679bb2";
    private const int Id65ExistingActorRootCount = 37;
    private const int Id65LastActorRoot = 0x1CF994;
    private const int Id65LastActorUsedByteLength = 0xB0;
    private const string Id65LastActorUsedSha256 =
        "d80ee82b365a3a29b84227e9cd63b3ea909e32c4113597200d9f53a70fb80b9e";
    private const int Id65ActorTailOffset = 0x1CFA44;
    private const int Id65ActorTailByteLength = 0x5BC;
    private const string Id65ActorTailSha256 =
        "bed95ae176cbd1efa4cfc7f400fc1eb294bc107dabf29d77e8174471a789cca2";
    private const int Id65FixupCountOffset = 0x7F28;
    private const int Id65FixupCount = 0x81;
    private const int Id65FixupActiveByteLength = 4 + (Id65FixupCount * 4);
    private const string Id65FixupActiveSha256 =
        "019e1b2c30b7e5f7f9a851175fe93940ea1cf148fbc37f8512781aaed6587dc2";
    private const int Id65FixupAppendOffset = 0x8130;
    private const int Id65PropertiesAllocationOffset = 0x8138;
    private const string Id65SceneTailSha256 =
        "2df7244b4c2c10726911058b50c0602fdc399c69d26ca4551f1633ab02666068";

    public static UnusedLevel65MobyDependencyBundleContract InspectFirstCrossLevelDonor(
        string foundationImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(foundationImagePath);
        string imagePath = Path.GetFullPath(foundationImagePath);
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("The exact ID65 full-authoring foundation BIN is missing.", imagePath);
        string imageSha256 = HashFile(imagePath);
        RequireHash(imageSha256, ExpectedFoundationImageSha256, "ID65 full-authoring foundation BIN");

        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("The ID65 Moby dependency contract accepts only MODE2/2352 input.");

        using FileStream image = File.OpenRead(imagePath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            image,
            layout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != WadLba || wad.Size != ExpectedWadByteLength)
        {
            throw new InvalidDataException(
                $"WAD.WAD changed from LBA {WadLba}/0x{ExpectedWadByteLength:X} to LBA {wad.Lba}/0x{wad.Size:X}.");
        }
        DiscFileRecord executable = DiscImage.FindRootFileRecord(
            image,
            layout,
            name => name.StartsWith("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
        {
            throw new InvalidDataException(
                $"SCUS_942.28 changed from LBA {ExecutableLba}/0x{ExecutableByteLength:X} to LBA {executable.Lba}/0x{executable.Size:X}.");
        }
        byte[] executableBytes = DiscImage.ReadFileBytes(
            image,
            layout,
            executable.Lba,
            0,
            executable.Size);
        RequireHash(Hash(executableBytes), ExecutableSha256, "relocated ID65 executable preimage");
        RequireExecutableHeader(executableBytes);
        byte[] executableText = executableBytes
            .AsSpan(ExecutableHeaderByteLength, ExecutableTextByteLength)
            .ToArray();
        RequireHash(Hash(executableText), ExecutableTextSha256, "relocated ID65 executable text");
        int executableClassImmediateCount = CountAlignedITypeImmediate(
            executableText,
            GrassActorId);
        if (executableClassImmediateCount != 0)
            throw new InvalidDataException("The global executable gained an aligned I-type class-0x01F5 immediate.");

        RequireDirectoryEntry(image, layout, ArtisansOverlayEntry, ArtisansOverlayWadOffset, ArtisansOverlayByteLength);
        RequireDirectoryEntry(image, layout, ArtisansDataEntry, ArtisansDataWadOffset, ArtisansDataByteLength);
        RequireDirectoryEntry(image, layout, Id65OverlayEntry, Id65OverlayWadOffset, Id65OverlayByteLength);
        RequireDirectoryEntry(image, layout, Id65DataEntry, Id65DataWadOffset, Id65DataByteLength);
        byte[] artisansOverlay = ReadWad(image, layout, ArtisansOverlayWadOffset, ArtisansOverlayByteLength);
        byte[] id65Overlay = ReadWad(image, layout, Id65OverlayWadOffset, Id65OverlayByteLength);
        RequireHash(Hash(artisansOverlay), ArtisansOverlaySha256, "Artisans overlay preimage");
        RequireHash(Hash(id65Overlay), Id65OverlaySha256, "ID65 Town Square overlay preimage");

        RequireNestedSubfile(
            image,
            layout,
            ArtisansDataWadOffset,
            2,
            ArtisansActorSubfileOffset,
            ArtisansActorSubfileByteLength);
        RequireNestedSubfile(
            image,
            layout,
            ArtisansDataWadOffset,
            3,
            ArtisansSceneOffset,
            ArtisansSceneByteLength);
        RequireNestedSubfile(
            image,
            layout,
            Id65DataWadOffset,
            2,
            Id65ActorSubfileOffset,
            Id65ActorSubfileByteLength);
        RequireNestedSubfile(
            image,
            layout,
            Id65DataWadOffset,
            3,
            Id65SceneOffset,
            Id65SceneByteLength);

        long artisansSceneWadOffset = ArtisansDataWadOffset + ArtisansSceneOffset;
        long id65SceneWadOffset = Id65DataWadOffset + Id65SceneOffset;
        RequireUInt32(
            ReadWad(image, layout, artisansSceneWadOffset + ArtisansObjectTableOffset - 4, 4),
            ArtisansObjectCount,
            "Artisans source-record count");
        RequireUInt32(
            ReadWad(image, layout, id65SceneWadOffset + Id65ObjectTableOffset - 4, 4),
            Id65ObjectCount,
            "ID65 source-record count");

        byte[] grassRow = ReadWad(image, layout, artisansSceneWadOffset + GrassRowSceneOffset, RecordStride);
        RequireHash(Hash(grassRow), GrassRowSha256, "Artisans Grass T121 row");
        if (GrassRowSceneOffset != ArtisansObjectTableOffset + (GrassTrueIndex * RecordStride))
            throw new InvalidDataException("The pinned Artisans T121 table arithmetic changed.");
        RequireUInt32(grassRow.AsSpan(0, 4), GrassPropertiesOffset, "Artisans Grass properties pointer");
        RequireUInt32(grassRow.AsSpan(8, 4), 0, "Artisans Grass legacy special-data pointer");
        RequireUInt16(grassRow.AsSpan(0x36, 2), GrassActorId, "Artisans Grass actor id");
        if (grassRow[0x4F] != 0 || grassRow[0x50] != 0x20 || grassRow[0x51] != 0x00 ||
            grassRow[0x52] != 0x10 || grassRow[0x53] != 0xFF || grassRow[0x43] != 0xFF ||
            grassRow[0x4A] != 0xFF || grassRow[0x4B] != 0x0C)
        {
            throw new InvalidDataException("The pinned Artisans Grass scenery fingerprint changed.");
        }

        byte[] properties = ReadWad(
            image,
            layout,
            artisansSceneWadOffset + GrassPropertiesOffset,
            GrassPropertiesByteLength);
        RequireHash(Hash(properties), GrassPropertiesSha256, "Artisans Grass properties");
        if (!properties.AsSpan().SequenceEqual(Convert.FromHexString("040000008A000000")))
            throw new InvalidDataException("Artisans Grass properties are not the pinned two-word block.");

        byte[] artisansRows = ReadWad(
            image,
            layout,
            artisansSceneWadOffset + ArtisansObjectTableOffset,
            ArtisansObjectCount * RecordStride);
        List<int> sharingTrueIndices = [];
        SortedSet<int> positivePropertiesPointers = [];
        for (int index = 0; index < ArtisansObjectCount; index++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(
                artisansRows.AsSpan(index * RecordStride, 4));
            if (pointer == GrassPropertiesOffset)
                sharingTrueIndices.Add(index);
            if (pointer > 0)
                positivePropertiesPointers.Add(checked((int)pointer));
        }
        int[] expectedSharingIndices =
            [118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133];
        if (!sharingTrueIndices.SequenceEqual(expectedSharingIndices))
            throw new InvalidDataException("The exact Artisans shared Grass-properties cluster changed.");
        int nextPropertiesPointer = positivePropertiesPointers.First(pointer => pointer > GrassPropertiesOffset);
        if (nextPropertiesPointer != GrassPropertiesOffset + GrassPropertiesByteLength)
            throw new InvalidDataException("The exact Artisans Grass properties extent changed.");

        byte[] artisansFixups = ReadWad(
            image,
            layout,
            artisansSceneWadOffset + ArtisansFixupCountOffset,
            ArtisansFixupActiveByteLength);
        RequireHash(Hash(artisansFixups), ArtisansFixupActiveSha256, "Artisans pointer-fixup component");
        RequireUInt32(artisansFixups.AsSpan(0, 4), ArtisansFixupCount, "Artisans pointer-fixup count");
        int[] sourceFixupFields = ReadFixupFields(artisansFixups, ArtisansFixupCount);
        int pointerFieldOccurrenceCount = sourceFixupFields.Count(offset => offset == GrassRowSceneOffset);
        if (pointerFieldOccurrenceCount != 1)
            throw new InvalidDataException("Artisans T121's properties field is not registered exactly once in the scene fixup list.");
        int[] internalPropertiesFixups = sourceFixupFields
            .Where(offset => offset >= GrassPropertiesOffset && offset < GrassPropertiesOffset + GrassPropertiesByteLength)
            .ToArray();
        if (internalPropertiesFixups.Length != 0)
            throw new InvalidDataException("The pinned Grass properties unexpectedly gained an internal scene pointer.");

        byte[] artisansHeader = ReadWad(image, layout, ArtisansDataWadOffset, 0x200);
        ActorRootTable artisansRoots = ReadActorRootTable(artisansHeader);
        if (artisansRoots.PopulatedCount != 35 ||
            artisansRoots.Roots[GrassActorRootIndex] != GrassActorRoot ||
            artisansRoots.ActorIds[GrassActorRootIndex] != GrassActorId ||
            artisansRoots.Roots[GrassActorRootIndex + 1] != GrassActorNextRoot)
        {
            throw new InvalidDataException("The Artisans Grass actor-root envelope changed.");
        }
        byte[] grassPackage = ReadWad(
            image,
            layout,
            ArtisansDataWadOffset + GrassActorRoot,
            GrassActorNextRoot - GrassActorRoot);
        RequireHash(Hash(grassPackage), GrassActorPackageSha256, "Artisans Grass actor package");
        int animationCount = ReadInt32(grassPackage, 0);
        int modelDataRelative = ReadInt32(grassPackage, 0x34);
        if (animationCount != 1 || modelDataRelative != 0x68 || ReadInt32(grassPackage, 0x38) != 0x3C)
            throw new InvalidDataException("The Artisans Grass animation header changed.");
        int animationOffset = ReadInt32(grassPackage, 0x38);
        int normalOffset = checked(modelDataRelative + ReadInt32(grassPackage, animationOffset + 0x14));
        int farLodOffset = checked(modelDataRelative + ReadInt32(grassPackage, animationOffset + 0x1C));
        UnusedLevel65MobyFaceStreamManifest normalFaces = ParseFaceStream(grassPackage, normalOffset, "normal");
        UnusedLevel65MobyFaceStreamManifest farLodFaces = ParseFaceStream(grassPackage, farLodOffset, "far-LOD");
        RequireHash(normalFaces.Sha256, GrassNormalFacesSha256, "Artisans Grass normal face table");
        RequireHash(farLodFaces.Sha256, GrassFarLodFacesSha256, "Artisans Grass far-LOD face table");
        if (normalFaces.RecordCount != 9 || normalFaces.UntexturedTriangleCount != 9 || normalFaces.TexturedRecordCount != 0 ||
            farLodFaces.RecordCount != 9 || farLodFaces.UntexturedTriangleCount != 9 || farLodFaces.TexturedRecordCount != 0)
        {
            throw new InvalidDataException("The Artisans Grass actor package is no longer entirely untextured at both LOD tiers.");
        }
        int packageEntryRelativeReferenceCount = CountAlignedValuesInRange(
            grassPackage,
            ArtisansActorSubfileOffset,
            ArtisansActorSubfileOffset + ArtisansActorSubfileByteLength);
        if (packageEntryRelativeReferenceCount != 0)
            throw new InvalidDataException("The Artisans Grass package gained an entry-relative actor-subfile pointer requiring rebasing.");

        byte[] id65ObjectTable = ReadWad(
            image,
            layout,
            id65SceneWadOffset + Id65ObjectTableOffset,
            Id65ObjectCount * RecordStride);
        RequireHash(Hash(id65ObjectTable), Id65ObjectTableSha256, "ID65 107-row Town Square object table");
        byte[] firstAppendRow = ReadWad(
            image,
            layout,
            id65SceneWadOffset + Id65FirstAppendRowOffset,
            RecordStride);
        RequireHash(Hash(firstAppendRow), ZeroRecordSha256, "ID65 first append row T107");
        RequireZero(firstAppendRow, "ID65 first append row T107");
        byte[] zeroRows = ReadWad(
            image,
            layout,
            id65SceneWadOffset + Id65FirstAppendRowOffset,
            Id65ContiguousZeroRowBytes);
        RequireHash(Hash(zeroRows), Id65ContiguousZeroRowsSha256, "ID65 contiguous object-row reserve");
        RequireZero(zeroRows, "ID65 contiguous object-row reserve");
        byte[] playerAnchor = id65ObjectTable.AsSpan(
            Id65PlayerAnchorTrueIndex * RecordStride,
            RecordStride).ToArray();
        RequireHash(Hash(playerAnchor), Id65PlayerAnchorSha256, "ID65 T92 player anchor");

        byte[] id65Header = ReadWad(image, layout, Id65DataWadOffset, 0x200);
        ActorRootTable id65Roots = ReadActorRootTable(id65Header);
        if (id65Roots.PopulatedCount != Id65ExistingActorRootCount ||
            id65Roots.Roots[Id65ExistingActorRootCount - 1] != Id65LastActorRoot ||
            id65Roots.ActorIds[Id65ExistingActorRootCount - 1] != 0x00FB ||
            id65Roots.Roots[Id65ExistingActorRootCount] != 0 ||
            id65Roots.ActorIds[Id65ExistingActorRootCount] != 0 ||
            id65Roots.ActorIds.Take(Id65ExistingActorRootCount).Contains(GrassActorId))
        {
            throw new InvalidDataException("ID65 actor-root capacity or actor 0x01F5 residency changed.");
        }
        byte[] lastActorUsed = ReadWad(
            image,
            layout,
            Id65DataWadOffset + Id65LastActorRoot,
            Id65LastActorUsedByteLength);
        RequireHash(Hash(lastActorUsed), Id65LastActorUsedSha256, "ID65 last native actor used bytes");
        RequireSimpleModelUsedLength(lastActorUsed, Id65LastActorUsedByteLength, "ID65 actor 0x00FB");
        byte[] actorTail = ReadWad(
            image,
            layout,
            Id65DataWadOffset + Id65ActorTailOffset,
            Id65ActorTailByteLength);
        RequireHash(Hash(actorTail), Id65ActorTailSha256, "ID65 actor/model zero tail");
        RequireZero(actorTail, "ID65 actor/model zero tail");
        int grassPackageLength = GrassActorNextRoot - GrassActorRoot;
        int newActorPackageEnd = checked(Id65ActorTailOffset + grassPackageLength);
        if (newActorPackageEnd > Id65ActorSubfileOffset + Id65ActorSubfileByteLength)
            throw new InvalidDataException("The Grass actor package no longer fits the existing ID65 actor/model tail.");

        byte[] id65Fixups = ReadWad(
            image,
            layout,
            id65SceneWadOffset + Id65FixupCountOffset,
            Id65FixupActiveByteLength);
        RequireHash(Hash(id65Fixups), Id65FixupActiveSha256, "ID65 pointer-fixup component");
        RequireUInt32(id65Fixups.AsSpan(0, 4), Id65FixupCount, "ID65 pointer-fixup count");
        int[] id65FixupFields = ReadFixupFields(id65Fixups, Id65FixupCount);
        if (id65FixupFields.Contains(Id65FirstAppendRowOffset))
            throw new InvalidDataException("The blank ID65 T107 properties field is already registered as a pointer fixup.");
        byte[] id65SceneTail = ReadWad(
            image,
            layout,
            id65SceneWadOffset + Id65FixupAppendOffset,
            Id65SceneByteLength - Id65FixupAppendOffset);
        RequireHash(Hash(id65SceneTail), Id65SceneTailSha256, "ID65 post-fixup scene tail");
        RequireZero(id65SceneTail, "ID65 post-fixup scene tail");
        if (Id65FixupCountOffset + Id65FixupActiveByteLength != Id65FixupAppendOffset ||
            Id65PropertiesAllocationOffset < Id65FixupAppendOffset + sizeof(uint) ||
            Id65PropertiesAllocationOffset + GrassPropertiesByteLength > Id65SceneByteLength)
        {
            throw new InvalidDataException("The exact ID65 fixup/props tail allocation changed.");
        }

        UnusedLevel65MobySourceRecordManifest sourceRecord = new(
            GrassTrueIndex,
            artisansSceneWadOffset + GrassRowSceneOffset,
            GrassRowSceneOffset,
            RecordStride,
            Convert.ToHexString(grassRow),
            Hash(grassRow),
            GrassPropertiesOffset,
            0,
            GrassActorId,
            grassRow[0x50],
            grassRow[0x51],
            grassRow[0x4F],
            grassRow[0x52],
            grassRow[0x53],
            grassRow[0x43],
            grassRow[0x4A],
            grassRow[0x4B],
            "Grass",
            "Observed Artisans fingerprint class=0x01F5 type=0x20 state=0 update=0x10 drop=0xFF; user override identifies Grass scenery.",
            IsDragon: false,
            IsPortal: false,
            IsThief: false,
            IsCollectible: false,
            IsTotalsLinked: false,
            HasRewardDrop: false,
            HasLegacySpecialDataPointer: false);

        UnusedLevel65MobyScenePropertiesManifest propertiesManifest = new(
            artisansSceneWadOffset,
            ArtisansSceneByteLength,
            ArtisansObjectCount,
            ArtisansObjectTableOffset,
            checked((int)GrassPropertiesOffset),
            GrassPropertiesByteLength,
            Convert.ToHexString(properties),
            Hash(properties),
            sharingTrueIndices,
            nextPropertiesPointer,
            GrassRowSceneOffset,
            ArtisansFixupCount,
            ArtisansFixupCountOffset + 4,
            ArtisansFixupActiveByteLength,
            Hash(artisansFixups),
            pointerFieldOccurrenceCount,
            internalPropertiesFixups,
            PropertiesPointerIsSceneRelative: true,
            LegacySpecialDataPointerIsPropertiesPointer: false);

        UnusedLevel65MobyActorPackageManifest actorPackage = new(
            ArtisansDataEntry,
            ArtisansDataWadOffset,
            ArtisansDataByteLength,
            ArtisansActorSubfileOffset,
            ArtisansActorSubfileByteLength,
            GrassActorRootIndex,
            GrassActorRoot,
            grassPackage.Length,
            Hash(grassPackage),
            animationCount,
            modelDataRelative,
            packageEntryRelativeReferenceCount,
            normalFaces,
            farLodFaces,
            normalFaces.TexturedRecordCount + farLodFaces.TexturedRecordCount,
            TexturePixelsRequired: false,
            ClutsRequired: false,
            PackageRebaseRequired: false);

        int completeRowCapacity = Id65ContiguousZeroRowBytes / RecordStride;
        int remainingRowBytes = Id65ContiguousZeroRowBytes % RecordStride;
        int newRootSlot = 0x50 + (Id65ExistingActorRootCount * sizeof(uint));
        int newActorIdSlot = 0x150 + (Id65ExistingActorRootCount * sizeof(ushort));
        int sceneTailRemaining = Id65SceneByteLength - (Id65PropertiesAllocationOffset + GrassPropertiesByteLength);
        UnusedLevel65MobyDestinationAllocator destination = new(
            Id65DataEntry,
            Id65DataWadOffset,
            Id65DataByteLength,
            id65SceneWadOffset,
            Id65SceneByteLength,
            Id65ObjectCount,
            Id65ObjectTableOffset,
            id65SceneWadOffset + Id65ObjectTableOffset,
            Hash(id65ObjectTable),
            Id65ObjectCount,
            Id65FirstAppendRowOffset,
            id65SceneWadOffset + Id65FirstAppendRowOffset,
            Hash(firstAppendRow),
            Id65ContiguousZeroRowBytes,
            completeRowCapacity,
            remainingRowBytes,
            Hash(zeroRows),
            Id65PlayerAnchorTrueIndex,
            Hash(playerAnchor),
            Id65ActorSubfileOffset,
            Id65ActorSubfileByteLength,
            Id65ExistingActorRootCount,
            Id65ExistingActorRootCount,
            newRootSlot,
            newActorIdSlot,
            Id65LastActorRoot,
            Id65LastActorUsedByteLength,
            Hash(lastActorUsed),
            Id65ActorTailOffset,
            Id65ActorTailByteLength,
            Hash(actorTail),
            Id65ActorTailOffset,
            newActorPackageEnd,
            Id65ActorSubfileOffset + Id65ActorSubfileByteLength - newActorPackageEnd,
            Id65FixupCountOffset,
            Id65FixupCountOffset + 4,
            Id65FixupCount,
            Id65FixupCount + 1,
            Id65FixupActiveByteLength,
            Hash(id65Fixups),
            Id65FixupAppendOffset,
            Id65FirstAppendRowOffset,
            Id65PropertiesAllocationOffset,
            GrassPropertiesByteLength,
            sceneTailRemaining,
            Hash(id65SceneTail),
            [0x00, 0x0C, 0x10, 0x14, 0x20, 0x46, 0x4A],
            RequiresSourceCountIncrement: true,
            RequiresRowPointerFixupAppend: true,
            RequiresInternalPropertiesFixupAppend: false,
            RequiresActorPackageRebase: false,
            RequiresTextureAllocation: false,
            RequiresNestedSubfileRelocation: false,
            RequiresDataEntryGrowth: false,
            StructuralAllocationComplete: true);

        UnusedLevel65MobyDispatchTraceManifest donorDispatch = InspectDefaultDispatchTrace(
            "artisans",
            "Artisans",
            ArtisansOverlayEntry,
            ArtisansOverlayWadOffset,
            artisansOverlay,
            ArtisansOverlaySha256,
            classRegister: 3,
            ArtisansClassLoadAddress,
            ArtisansClassLoadWord,
            ArtisansDispatchStartAddress,
            ArtisansDecisionWindowAddress,
            ArtisansDecisionWindowByteLength,
            ArtisansDecisionWindowSha256,
            ArtisansDefaultLoopExitAddress,
            ArtisansDefaultLoopExitSha256,
            ArtisansTraceSha256,
            adjacentDedicatedClassId: null,
            adjacentDedicatedHandlerAddress: null);
        UnusedLevel65MobyDispatchTraceManifest targetDispatch = InspectDefaultDispatchTrace(
            "id65-town-square",
            "ID65 / Town Square overlay",
            Id65OverlayEntry,
            Id65OverlayWadOffset,
            id65Overlay,
            Id65OverlaySha256,
            classRegister: 4,
            Id65ClassLoadAddress,
            Id65ClassLoadWord,
            Id65DispatchStartAddress,
            Id65DecisionWindowAddress,
            Id65DecisionWindowByteLength,
            Id65DecisionWindowSha256,
            Id65DefaultLoopExitAddress,
            Id65DefaultLoopExitSha256,
            Id65TraceSha256,
            Id65AdjacentDedicatedClassId,
            Id65AdjacentDedicatedHandlerAddress);
        UnusedLevel65MobyGlobalExecutableManifest globalExecutable = new(
            executable.Lba,
            executable.Size,
            Hash(executableBytes),
            ExecutableHeaderByteLength,
            ExecutableInitialProgramCounter,
            ExecutableTextLoadAddress,
            ExecutableTextByteLength,
            Hash(executableText),
            GrassActorId,
            executableClassImmediateCount);
        uint[] propertiesWords =
        [
            BinaryPrimitives.ReadUInt32LittleEndian(properties.AsSpan(0, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(properties.AsSpan(4, 4))
        ];
        UnusedLevel65MobyBehaviorClosureManifest behaviorClosure = new(
            donorDispatch,
            targetDispatch,
            globalExecutable,
            propertiesWords,
            PropertiesContainPointers: false,
            PropertiesControllerSemanticsRequired: false,
            SoundDependencyRequired: false,
            ParticleOrEffectDependencyRequired: false,
            DynamicSpawnDependencyRequired: false,
            DynamicNativeLinkDependencyRequired: false,
            StaticRuntimeDependencyClosureComplete: true);

        List<UnusedLevel65MobyDependencyRequirement> dependencies = BuildDependencies(
            sourceRecord,
            propertiesManifest,
            actorPackage,
            destination,
            behaviorClosure);
        string[] hardBlockers = dependencies
            .Where(dependency => dependency.EvidenceState == UnusedLevel65MobyDependencyEvidenceState.UnresolvedHardBlocker)
            .Select(dependency => $"{dependency.Id}: {dependency.Evidence}")
            .ToArray();
        if (hardBlockers.Length != 1 ||
            !hardBlockers[0].StartsWith("runtime-acceptance: ", StringComparison.Ordinal))
            throw new InvalidDataException("The fail-closed runtime-publication blocker set changed.");

        UnusedLevel65MobyExporterCompatibilityBoundary exporterBoundary = new(
            "Native record +0x00 is interpreted only as a scene-relative m_Props pointer; destination pointer fields are registered in the scene relocation list.",
            "Legacy record +0x08 is preserved and audited independently; it is never used to decide whether native properties exist.",
            UsesSharedMobySourcePatchExporter: false,
            UsesSharedCrossLevelRecipeRegistry: false,
            ExistingActorPackageRecipeIsSufficient: false,
            [
                "The shared generic exporter derives an entry-relative base from tableWadOffset-tableRelativeOffset, but native m_Props pointers are scene-relative.",
                "The shared catalog's legacy +0x08 SpecialDataPointer classification cannot prove the +0x00 properties graph.",
                "The v2 trace closes the no-handler controller/sound/particle/spawn/link dependency set, but existing actor-package recipes still do not implement this exact row/root/properties/fixup allocation.",
                "This contract is independent and read-only; it neither invokes nor mutates those shared paths."
            ]);

        string deterministicContractSha256 = ComputeContractHash(
            imageSha256,
            sourceRecord,
            propertiesManifest,
            actorPackage,
            destination,
            behaviorClosure,
            dependencies,
            hardBlockers,
            exporterBoundary);
        RequireHash(
            deterministicContractSha256,
            ExpectedContractSha256,
            "ID65 Moby dependency-bundle contract");

        return new UnusedLevel65MobyDependencyBundleContract(
            ProfileId,
            SchemaVersion,
            imagePath,
            imageSha256,
            LockedIndependentBaseImageSha256,
            Id65ObjectTableSha256,
            DonorId,
            "artisans",
            "Artisans",
            sourceRecord,
            propertiesManifest,
            actorPackage,
            destination,
            behaviorClosure,
            dependencies,
            hardBlockers,
            exporterBoundary,
            deterministicContractSha256,
            StructurallyCompleteAllocation: true,
            StaticRuntimeDependencyClosureComplete: true,
            RunnableBundleSupport: false,
            StaticInspectionOnly: true,
            ProducesPatches: false,
            WritesBin: false,
            WritesCue: false,
            DisposableRuntimeCandidateAuthorized: false,
            AppIntegrated: false,
            NormalCreateBinEnabled: false,
            ReleasePublicationAuthorized: false);
    }

    private static List<UnusedLevel65MobyDependencyRequirement> BuildDependencies(
        UnusedLevel65MobySourceRecordManifest row,
        UnusedLevel65MobyScenePropertiesManifest properties,
        UnusedLevel65MobyActorPackageManifest actorPackage,
        UnusedLevel65MobyDestinationAllocator destination,
        UnusedLevel65MobyBehaviorClosureManifest behavior) =>
    [
        new(
            "source-row",
            UnusedLevel65MobyDependencyKind.SourceRecord,
            true,
            UnusedLevel65MobyDependencyEvidenceState.ExactPinnedPreimage,
            [],
            $"Artisans T{row.TrueIndex} is pinned as {row.ByteLength}-byte row {row.Sha256}, actor 0x{row.ActorId:X4}."),
        new(
            "scene-properties",
            UnusedLevel65MobyDependencyKind.SceneProperties,
            true,
            UnusedLevel65MobyDependencyEvidenceState.ExactPinnedPreimage,
            ["source-row"],
            $"Scene-relative 0x{properties.PropertiesSceneOffset:X} resolves to exact {properties.PropertiesByteLength}-byte block {properties.PropertiesSha256}; next distinct pointer proves the extent."),
        new(
            "scene-pointer-fixups",
            UnusedLevel65MobyDependencyKind.ScenePointerFixups,
            true,
            UnusedLevel65MobyDependencyEvidenceState.ExactPinnedPreimage,
            ["scene-properties"],
            $"The source row field 0x{properties.SourcePointerFieldSceneOffset:X} occurs once in the C9 fixup list; the properties block has {properties.InternalPropertiesPointerFixups.Count} internal fixups."),
        new(
            "actor-model-package",
            UnusedLevel65MobyDependencyKind.ActorModelPackage,
            true,
            UnusedLevel65MobyDependencyEvidenceState.ExactPinnedPreimage,
            ["source-row"],
            $"Actor 0x{row.ActorId:X4} package is 0x{actorPackage.ActorPackageByteLength:X} bytes at donor root 0x{actorPackage.ActorRootEntryRelativeOffset:X}, SHA {actorPackage.ActorPackageSha256}; no entry-relative rebase sites."),
        new(
            "normal-and-far-lod-animation",
            UnusedLevel65MobyDependencyKind.Animation,
            true,
            UnusedLevel65MobyDependencyEvidenceState.ExactPinnedPreimage,
            ["actor-model-package"],
            $"One animation resolves exact normal and far-LOD tables; each has {actorPackage.NormalFaces.RecordCount} grammar-complete face commands."),
        new(
            "texture-pixels-and-cluts",
            UnusedLevel65MobyDependencyKind.TexturePixelsAndCluts,
            true,
            UnusedLevel65MobyDependencyEvidenceState.NotRequiredByDecodedGrammar,
            ["normal-and-far-lod-animation"],
            "Normal and far-LOD tables contain 18 untextured triangle commands and zero textured descriptors, so no external VRAM pixels or CLUTs travel with this package."),
        new(
            "destination-object-row",
            UnusedLevel65MobyDependencyKind.DestinationObjectRows,
            true,
            UnusedLevel65MobyDependencyEvidenceState.ExactDerivedAllocation,
            ["source-row", "scene-properties", "scene-pointer-fixups"],
            $"ID65 T{destination.FirstAppendTrueIndex} at scene+0x{destination.FirstAppendRowSceneOffset:X} is blank; {destination.CompleteAppendRowCapacity} complete 0x58-byte rows are available."),
        new(
            "destination-actor-root",
            UnusedLevel65MobyDependencyKind.DestinationActorRoots,
            true,
            UnusedLevel65MobyDependencyEvidenceState.ExactDerivedAllocation,
            ["actor-model-package"],
            $"Root slot {destination.NewActorRootIndex} installs at header+0x{destination.NewActorRootSlotEntryRelativeOffset:X}; package fits root 0x{destination.NewActorRootEntryRelativeOffset:X} with 0x{destination.ActorTailBytesRemaining:X} bytes left."),
        new(
            "destination-scene-storage",
            UnusedLevel65MobyDependencyKind.DestinationSceneStorage,
            true,
            UnusedLevel65MobyDependencyEvidenceState.ExactDerivedAllocation,
            ["destination-object-row"],
            $"Append field fixup 0x{destination.AppendedPointerFieldSceneOffset:X} at scene+0x{destination.ScenePointerFixupAppendOffset:X}, raise 0x81 to 0x82, and reserve 8-byte props at scene+0x{destination.PropertiesAllocationSceneOffset:X}; no subfile growth."),
        new(
            "reward-totals-persistence",
            UnusedLevel65MobyDependencyKind.RewardsTotalsAndPersistence,
            true,
            UnusedLevel65MobyDependencyEvidenceState.ExcludedByPinnedDonorIdentity,
            ["source-row"],
            "Pinned Grass is scenery, not dragon/portal/thief/collectible; it has no legacy special-data pointer and +0x53 is 0xFF rather than a reward actor/class. No totals or persistence mutation is allocated."),
        new(
            "overlay-dispatch-controller",
            UnusedLevel65MobyDependencyKind.OverlayDispatchAndController,
            true,
            UnusedLevel65MobyDependencyEvidenceState.NotRequiredByDecodedGrammar,
            ["source-row", "actor-model-package"],
            $"Exact donor trace {behavior.DonorDispatch.ExecutedTraceSha256} reaches default exit 0x{behavior.DonorDispatch.DefaultLoopExitAddress:X8}; exact target trace {behavior.TargetDispatch.ExecutedTraceSha256} reaches default exit 0x{behavior.TargetDispatch.DefaultLoopExitAddress:X8}. Neither overlay has a class-0x01F5 handler, call, property read, or actor mutation."),
        new(
            "controller-property-semantics",
            UnusedLevel65MobyDependencyKind.ControllerPropertySemantics,
            true,
            UnusedLevel65MobyDependencyEvidenceState.NotRequiredByDecodedGrammar,
            ["scene-properties", "overlay-dispatch-controller"],
            $"The exact non-pointer words 0x{behavior.ExactPropertiesWords[0]:X8}/0x{behavior.ExactPropertiesWords[1]:X8} remain relocated source data. Both class traces perform zero m_Props reads and enter no controller, so controller semantics are not required for this passive actor."),
        new(
            "sound-particle-spawn-closure",
            UnusedLevel65MobyDependencyKind.SoundAndParticles,
            true,
            UnusedLevel65MobyDependencyEvidenceState.NotRequiredByDecodedGrammar,
            ["overlay-dispatch-controller", "controller-property-semantics"],
            "Class 0x01F5 executes no handler and both exact dispatch traces contain zero call instructions. There is therefore no class-specific sound, particle, effect, or dynamic-Moby spawn call graph to transplant."),
        new(
            "native-dynamic-link-closure",
            UnusedLevel65MobyDependencyKind.NativeAndEditorLinks,
            true,
            UnusedLevel65MobyDependencyEvidenceState.NotRequiredByDecodedGrammar,
            ["overlay-dispatch-controller", "controller-property-semantics"],
            "The copied row has +0x08=0, its exact properties contain no relocated pointer, neither default dispatch path reads properties or writes the actor, and no editor move-link is assigned. No native dynamic-link dependency exists for this passive actor."),
        new(
            "runtime-acceptance",
            UnusedLevel65MobyDependencyKind.RuntimeAcceptance,
            true,
            UnusedLevel65MobyDependencyEvidenceState.UnresolvedHardBlocker,
            ["overlay-dispatch-controller", "controller-property-semantics", "sound-particle-spawn-closure", "native-dynamic-link-closure"],
            "Static row/model/properties/allocation and no-handler dependency closure is complete, but no BIN/CUE is authorized here. A separate transactional writer and DuckStation test must prove boot, visible normal and far LOD, stable idle/update, exit/re-entry, and save/reset behavior.")
    ];

    private static UnusedLevel65MobyDispatchTraceManifest InspectDefaultDispatchTrace(
        string levelKey,
        string levelName,
        int overlayWadEntry,
        long overlayWadOffset,
        byte[] overlay,
        string overlaySha256,
        int classRegister,
        uint classLoadAddress,
        uint classLoadWord,
        uint dispatchStartAddress,
        uint decisionWindowAddress,
        int decisionWindowByteLength,
        string decisionWindowSha256,
        uint defaultLoopExitAddress,
        string defaultLoopExitSha256,
        string expectedTraceSha256,
        ushort? adjacentDedicatedClassId,
        uint? adjacentDedicatedHandlerAddress)
    {
        RequireOverlayWord(overlay, classLoadAddress, classLoadWord, $"{levelName} class load");
        byte[] decisionWindow = ReadOverlayBytes(
            overlay,
            decisionWindowAddress,
            decisionWindowByteLength);
        RequireHash(Hash(decisionWindow), decisionWindowSha256, $"{levelName} high-class decision window");
        byte[] defaultLoopExit = ReadOverlayBytes(overlay, defaultLoopExitAddress, 0x18);
        RequireHash(Hash(defaultLoopExit), defaultLoopExitSha256, $"{levelName} default loop exit");
        if (CountDedicatedClassComparisons(overlay, classRegister, GrassActorId, expectedHandler: null) != 0)
            throw new InvalidDataException($"{levelName} unexpectedly gained a class-0x{GrassActorId:X4} handler comparison.");

        if (adjacentDedicatedClassId.HasValue != adjacentDedicatedHandlerAddress.HasValue)
            throw new InvalidDataException($"{levelName} adjacent-handler evidence is incomplete.");
        if (adjacentDedicatedClassId is ushort adjacentClass &&
            adjacentDedicatedHandlerAddress is uint adjacentHandler &&
            CountDedicatedClassComparisons(overlay, classRegister, adjacentClass, adjacentHandler) != 1)
        {
            throw new InvalidDataException(
                $"{levelName} does not contain exactly one class-0x{adjacentClass:X4} comparison to 0x{adjacentHandler:X8}.");
        }

        uint[] registers = new uint[32];
        registers[classRegister] = GrassActorId;
        List<uint> addresses = [];
        List<uint> words = [];
        uint pc = dispatchStartAddress;
        bool reachedDefaultExit = false;
        for (int step = 0; step < 64 && !reachedDefaultExit; step++)
        {
            uint word = ReadOverlayWord(overlay, pc);
            addresses.Add(pc);
            words.Add(word);
            uint opcode = word >> 26;
            switch (opcode)
            {
                case 0 when word == 0:
                    pc += 4;
                    break;
                case 9: // ADDIU
                case 10: // SLTI
                    ExecuteSimpleInstruction(registers, word, levelName);
                    pc += 4;
                    break;
                case 4: // BEQ
                case 5: // BNE
                {
                    int rs = checked((int)((word >> 21) & 0x1F));
                    int rt = checked((int)((word >> 16) & 0x1F));
                    bool equal = registers[rs] == registers[rt];
                    bool take = opcode == 4 ? equal : !equal;
                    uint delayAddress = pc + 4;
                    uint delayWord = ReadOverlayWord(overlay, delayAddress);
                    addresses.Add(delayAddress);
                    words.Add(delayWord);
                    ExecuteSimpleInstruction(registers, delayWord, levelName);
                    int branchWords = (short)(word & 0xFFFF);
                    uint branchTarget = unchecked(pc + 4 + (uint)(branchWords * 4));
                    pc = take ? branchTarget : pc + 8;
                    break;
                }
                case 2: // J
                {
                    uint delayAddress = pc + 4;
                    uint delayWord = ReadOverlayWord(overlay, delayAddress);
                    addresses.Add(delayAddress);
                    words.Add(delayWord);
                    ExecuteSimpleInstruction(registers, delayWord, levelName);
                    uint target = ((pc + 4) & 0xF0000000u) | ((word & 0x03FFFFFFu) << 2);
                    reachedDefaultExit = target == defaultLoopExitAddress;
                    pc = target;
                    break;
                }
                default:
                    throw new InvalidDataException(
                        $"{levelName} class-0x{GrassActorId:X4} trace reached unsupported word 0x{word:X8} at 0x{pc:X8}.");
            }
        }
        if (!reachedDefaultExit || pc != defaultLoopExitAddress)
            throw new InvalidDataException($"{levelName} class-0x{GrassActorId:X4} did not reach its default loop exit.");

        byte[] traceBytes = new byte[words.Count * sizeof(uint)];
        for (int index = 0; index < words.Count; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(traceBytes.AsSpan(index * 4, 4), words[index]);
        string traceSha256 = Hash(traceBytes);
        RequireHash(traceSha256, expectedTraceSha256, $"{levelName} class-0x{GrassActorId:X4} executed trace");

        uint[] defaultExitWords = Enumerable.Range(0, defaultLoopExit.Length / 4)
            .Select(index => ReadUInt32(defaultLoopExit, index * 4))
            .ToArray();
        int callCount = words.Count(IsCallInstruction) + defaultExitWords.Count(IsCallInstruction);
        int propertiesReadCount = words.Count(word => IsPropertiesRead(word, runtimeActorRegister: 19)) +
                                  defaultExitWords.Count(word => IsPropertiesRead(word, runtimeActorRegister: 19));
        int actorWriteCount = words.Count(word => IsRuntimeActorWrite(word, runtimeActorRegister: 19)) +
                              defaultExitWords.Count(word => IsRuntimeActorWrite(word, runtimeActorRegister: 19));
        if (callCount != 0 || propertiesReadCount != 0 || actorWriteCount != 0)
            throw new InvalidDataException($"{levelName} passive class trace gained a call, m_Props read, or actor write.");

        return new UnusedLevel65MobyDispatchTraceManifest(
            levelKey,
            levelName,
            overlayWadEntry,
            overlayWadOffset,
            overlay.Length,
            overlaySha256,
            OverlayLoadAddress,
            GrassActorId,
            classLoadAddress,
            classLoadWord,
            dispatchStartAddress,
            decisionWindowAddress,
            decisionWindowByteLength,
            Hash(decisionWindow),
            defaultLoopExitAddress,
            Hash(defaultLoopExit),
            addresses,
            words,
            traceSha256,
            adjacentDedicatedClassId,
            adjacentDedicatedHandlerAddress,
            callCount,
            propertiesReadCount,
            actorWriteCount,
            DedicatedHandlerPresent: false,
            ReachesDefaultLoopExit: true,
            ClassSpecificControllerPresent: false);
    }

    private static void ExecuteSimpleInstruction(uint[] registers, uint word, string levelName)
    {
        if (word == 0)
            return;
        uint opcode = word >> 26;
        int rs = checked((int)((word >> 21) & 0x1F));
        int rt = checked((int)((word >> 16) & 0x1F));
        int immediate = (short)(word & 0xFFFF);
        switch (opcode)
        {
            case 9: // ADDIU
                registers[rt] = unchecked(registers[rs] + (uint)immediate);
                break;
            case 10: // SLTI
                registers[rt] = (int)registers[rs] < immediate ? 1u : 0u;
                break;
            default:
                throw new InvalidDataException(
                    $"{levelName} trace delay/simple slot contains unsupported word 0x{word:X8}.");
        }
        registers[0] = 0;
    }

    private static int CountDedicatedClassComparisons(
        byte[] overlay,
        int classRegister,
        ushort actorId,
        uint? expectedHandler)
    {
        int count = 0;
        for (int offset = 0; offset <= overlay.Length - 8; offset += 4)
        {
            uint load = ReadUInt32(overlay, offset);
            if ((load >> 26) != 9 || ((load >> 21) & 0x1F) != 0 ||
                (load & 0xFFFF) != actorId)
            {
                continue;
            }
            int temporaryRegister = checked((int)((load >> 16) & 0x1F));
            uint branch = ReadUInt32(overlay, offset + 4);
            if ((branch >> 26) != 4)
                continue;
            int branchRs = checked((int)((branch >> 21) & 0x1F));
            int branchRt = checked((int)((branch >> 16) & 0x1F));
            if (!((branchRs == classRegister && branchRt == temporaryRegister) ||
                  (branchRt == classRegister && branchRs == temporaryRegister)))
            {
                continue;
            }
            uint branchAddress = OverlayLoadAddress + checked((uint)(offset + 4));
            int branchWords = (short)(branch & 0xFFFF);
            uint target = unchecked(branchAddress + 4 + (uint)(branchWords * 4));
            if (!expectedHandler.HasValue || target == expectedHandler.Value)
                count++;
        }
        return count;
    }

    private static bool IsCallInstruction(uint word) =>
        (word >> 26) == 3 || ((word >> 26) == 0 && (word & 0x3F) == 9);

    private static bool IsPropertiesRead(uint word, int runtimeActorRegister)
    {
        uint opcode = word >> 26;
        bool isLoad = opcode is >= 0x20 and <= 0x27 or 0x30 or 0x31 or 0x32 or 0x33;
        return isLoad && ((word >> 21) & 0x1F) == runtimeActorRegister && (word & 0xFFFF) == 0;
    }

    private static bool IsRuntimeActorWrite(uint word, int runtimeActorRegister)
    {
        uint opcode = word >> 26;
        bool isStore = opcode is >= 0x28 and <= 0x2E or 0x38 or 0x39 or 0x3A or 0x3B;
        return isStore && ((word >> 21) & 0x1F) == runtimeActorRegister;
    }

    private static byte[] ReadOverlayBytes(byte[] overlay, uint address, int byteLength)
    {
        int offset = checked((int)(address - OverlayLoadAddress));
        if (offset < 0 || byteLength < 0 || offset > overlay.Length - byteLength)
            throw new InvalidDataException($"Overlay address 0x{address:X8}/0x{byteLength:X} is out of range.");
        return overlay.AsSpan(offset, byteLength).ToArray();
    }

    private static uint ReadOverlayWord(byte[] overlay, uint address) =>
        BinaryPrimitives.ReadUInt32LittleEndian(ReadOverlayBytes(overlay, address, 4));

    private static void RequireOverlayWord(byte[] overlay, uint address, uint expected, string label)
    {
        uint actual = ReadOverlayWord(overlay, address);
        if (actual != expected)
            throw new InvalidDataException($"{label} is 0x{actual:X8}, expected 0x{expected:X8}.");
    }

    private static void RequireExecutableHeader(byte[] executable)
    {
        if (executable.Length != ExecutableByteLength ||
            !executable.AsSpan(0, 8).SequenceEqual("PS-X EXE"u8) ||
            ReadUInt32(executable, 0x10) != ExecutableInitialProgramCounter ||
            ReadUInt32(executable, 0x18) != ExecutableTextLoadAddress ||
            ReadUInt32(executable, 0x1C) != ExecutableTextByteLength ||
            ExecutableHeaderByteLength + ExecutableTextByteLength != executable.Length)
        {
            throw new InvalidDataException("The relocated ID65 executable header/load envelope changed.");
        }
    }

    private static int CountAlignedITypeImmediate(byte[] bytes, ushort immediate)
    {
        int count = 0;
        for (int offset = 0; offset <= bytes.Length - 4; offset += 4)
        {
            uint word = ReadUInt32(bytes, offset);
            uint opcode = word >> 26;
            if (opcode is >= 8 and <= 15 && (word & 0xFFFF) == immediate)
                count++;
        }
        return count;
    }

    private static UnusedLevel65MobyFaceStreamManifest ParseFaceStream(
        byte[] package,
        int start,
        string tier)
    {
        if (start < 0 || start + 4 > package.Length)
            throw new InvalidDataException($"The Grass {tier} face table starts outside its package.");
        int bodyLength = ReadInt32(package, start);
        int end = checked(start + 4 + bodyLength);
        if (bodyLength < 0 || (bodyLength & 3) != 0 || end > package.Length)
            throw new InvalidDataException($"The Grass {tier} face table has invalid length 0x{bodyLength:X}.");
        int cursor = start + 4;
        int records = 0;
        int untexturedTriangles = 0;
        int textured = 0;
        while (cursor < end)
        {
            if (cursor + 8 > end)
                throw new InvalidDataException($"The Grass {tier} face table ends inside a renderer command.");
            uint control = ReadUInt32(package, cursor);
            int recordBytes;
            bool isTextured;
            bool isUntexturedTriangle = false;
            if ((control & 0x80000000u) == 0)
            {
                isTextured = (control & 0x2u) != 0;
                recordBytes = isTextured ? 20 : 8;
                isUntexturedTriangle = !isTextured;
            }
            else if ((control & 0x4u) != 0)
            {
                isTextured = true;
                recordBytes = 20;
            }
            else
            {
                isTextured = (control & 0x2u) != 0;
                recordBytes = isTextured ? 24 : 12;
            }
            if (cursor + recordBytes > end)
                throw new InvalidDataException($"The Grass {tier} renderer command overruns its face table.");
            records++;
            if (isTextured)
                textured++;
            if (isUntexturedTriangle)
                untexturedTriangles++;
            cursor += recordBytes;
        }
        byte[] table = package.AsSpan(start, 4 + bodyLength).ToArray();
        return new UnusedLevel65MobyFaceStreamManifest(
            tier,
            start,
            bodyLength,
            table.Length,
            records,
            untexturedTriangles,
            textured,
            Hash(table),
            EndsExactlyUnderRetailRendererGrammar: cursor == end);
    }

    private static string ComputeContractHash(
        string imageSha256,
        UnusedLevel65MobySourceRecordManifest row,
        UnusedLevel65MobyScenePropertiesManifest properties,
        UnusedLevel65MobyActorPackageManifest actorPackage,
        UnusedLevel65MobyDestinationAllocator destination,
        UnusedLevel65MobyBehaviorClosureManifest behavior,
        IReadOnlyList<UnusedLevel65MobyDependencyRequirement> dependencies,
        IReadOnlyList<string> blockers,
        UnusedLevel65MobyExporterCompatibilityBoundary boundary)
    {
        StringBuilder value = new();
        Add(ProfileId);
        Add(SchemaVersion);
        Add(imageSha256);
        Add(LockedIndependentBaseImageSha256);
        Add(DonorId);
        Add(row.TrueIndex);
        Add(row.WadOffset);
        Add(row.Sha256);
        Add(row.SceneRelativePropertiesPointer);
        Add(row.LegacySpecialDataPointer);
        Add(row.ActorId);
        Add(properties.PropertiesSha256);
        Add(properties.SourcePointerFixupActiveSha256);
        foreach (int index in properties.SharingTrueIndices)
            Add(index);
        Add(actorPackage.ActorPackageSha256);
        Add(actorPackage.NormalFaces.Sha256);
        Add(actorPackage.FarLodFaces.Sha256);
        Add(actorPackage.TotalTexturedFaceCount);
        Add(destination.ExistingObjectTableSha256);
        Add(destination.FirstAppendRowSha256);
        Add(destination.ContiguousZeroRowsSha256);
        Add(destination.PlayerAnchorSha256);
        Add(destination.LastActorUsedSha256);
        Add(destination.ActorTailSha256);
        Add(destination.ScenePointerFixupActiveSha256);
        Add(destination.SceneTailPreimageSha256);
        Add(destination.NewActorRootEntryRelativeOffset);
        Add(destination.NewActorPackageEndEntryRelativeOffset);
        Add(destination.PropertiesAllocationSceneOffset);
        foreach (UnusedLevel65MobyDispatchTraceManifest dispatch in
                 new[] { behavior.DonorDispatch, behavior.TargetDispatch })
        {
            Add(dispatch.LevelKey);
            Add(dispatch.OverlayWadEntry);
            Add(dispatch.OverlaySha256);
            Add(dispatch.ClassLoadAddress);
            Add(dispatch.ClassLoadWord);
            Add(dispatch.DispatchStartAddress);
            Add(dispatch.DecisionWindowAddress);
            Add(dispatch.DecisionWindowByteLength);
            Add(dispatch.DecisionWindowSha256);
            Add(dispatch.DefaultLoopExitAddress);
            Add(dispatch.DefaultLoopExitSha256);
            foreach (uint address in dispatch.ExecutedInstructionAddresses)
                Add(address);
            foreach (uint word in dispatch.ExecutedInstructionWords)
                Add(word);
            Add(dispatch.ExecutedTraceSha256);
            Add(dispatch.AdjacentDedicatedClassId);
            Add(dispatch.AdjacentDedicatedHandlerAddress);
            Add(dispatch.CallInstructionCount);
            Add(dispatch.PropertiesReadCount);
            Add(dispatch.RuntimeActorWriteCount);
            Add(dispatch.DedicatedHandlerPresent);
            Add(dispatch.ReachesDefaultLoopExit);
            Add(dispatch.ClassSpecificControllerPresent);
        }
        Add(behavior.GlobalExecutable.Lba);
        Add(behavior.GlobalExecutable.ByteLength);
        Add(behavior.GlobalExecutable.Sha256);
        Add(behavior.GlobalExecutable.InitialProgramCounter);
        Add(behavior.GlobalExecutable.TextLoadAddress);
        Add(behavior.GlobalExecutable.TextByteLength);
        Add(behavior.GlobalExecutable.TextSha256);
        Add(behavior.GlobalExecutable.AlignedITypeImmediateOccurrenceCount);
        foreach (uint word in behavior.ExactPropertiesWords)
            Add(word);
        Add(behavior.PropertiesContainPointers);
        Add(behavior.PropertiesControllerSemanticsRequired);
        Add(behavior.SoundDependencyRequired);
        Add(behavior.ParticleOrEffectDependencyRequired);
        Add(behavior.DynamicSpawnDependencyRequired);
        Add(behavior.DynamicNativeLinkDependencyRequired);
        Add(behavior.StaticRuntimeDependencyClosureComplete);
        foreach (UnusedLevel65MobyDependencyRequirement dependency in dependencies)
        {
            Add(dependency.Id);
            Add((int)dependency.Kind);
            Add((int)dependency.EvidenceState);
            Add(dependency.RequiredForRunnableBundle);
            foreach (string prerequisite in dependency.DependsOn)
                Add(prerequisite);
            Add(dependency.Evidence);
        }
        foreach (string blocker in blockers)
            Add(blocker);
        Add(boundary.PropertiesPointerPolicy);
        Add(boundary.LegacySpecialDataPointerPolicy);
        foreach (string reason in boundary.ExactReasons)
            Add(reason);
        return Hash(Encoding.UTF8.GetBytes(value.ToString()));

        void Add(object? item) => value.Append(item).Append('\n');
    }

    private static ActorRootTable ReadActorRootTable(byte[] header)
    {
        const int rootCount = 64;
        int[] roots = new int[rootCount];
        ushort[] actorIds = new ushort[rootCount];
        bool sawZero = false;
        int populated = 0;
        int previous = 0;
        for (int index = 0; index < rootCount; index++)
        {
            int root = checked((int)ReadUInt32(header, 0x50 + (index * 4)));
            ushort actorId = ReadUInt16(header, 0x150 + (index * 2));
            roots[index] = root;
            actorIds[index] = actorId;
            if (root == 0)
            {
                sawZero = true;
                continue;
            }
            if (sawZero || root <= previous)
                throw new InvalidDataException("An actor-root table is not contiguous and strictly ascending.");
            populated++;
            previous = root;
        }
        return new ActorRootTable(roots, actorIds, populated);
    }

    private static int[] ReadFixupFields(byte[] component, int expectedCount)
    {
        int[] result = new int[expectedCount];
        for (int index = 0; index < expectedCount; index++)
            result[index] = checked((int)ReadUInt32(component, 4 + (index * 4)));
        return result;
    }

    private static int CountAlignedValuesInRange(byte[] bytes, int minimumInclusive, int maximumExclusive)
    {
        int count = 0;
        for (int offset = 0; offset <= bytes.Length - 4; offset += 4)
        {
            uint value = ReadUInt32(bytes, offset);
            if (value >= minimumInclusive && value < maximumExclusive)
                count++;
        }
        return count;
    }

    private static void RequireSimpleModelUsedLength(byte[] model, int expectedUsedLength, string label)
    {
        if (ReadInt32(model, 0) >= 0 || model.Length < 0x10)
            throw new InvalidDataException($"{label} is not the expected simple model.");
        int faceCount = model[1];
        int faceRelative = ReadInt32(model, 0x0C);
        int usedLength = checked(faceRelative + (faceCount * 8));
        if (usedLength != expectedUsedLength)
            throw new InvalidDataException($"{label} used length is 0x{usedLength:X}, expected 0x{expectedUsedLength:X}.");
    }

    private static void RequireDirectoryEntry(
        FileStream image,
        DiscLayout layout,
        int index,
        long expectedOffset,
        int expectedLength)
    {
        byte[] row = ReadWad(image, layout, index * 8L, 8);
        RequireUInt32(row.AsSpan(0, 4), checked((uint)expectedOffset), $"WAD row {index} offset");
        RequireUInt32(row.AsSpan(4, 4), checked((uint)expectedLength), $"WAD row {index} length");
    }

    private static void RequireNestedSubfile(
        FileStream image,
        DiscLayout layout,
        long entryWadOffset,
        int subfileIndex,
        int expectedOffset,
        int expectedLength)
    {
        byte[] descriptor = ReadWad(image, layout, entryWadOffset + (subfileIndex * 8L), 8);
        RequireUInt32(descriptor.AsSpan(0, 4), checked((uint)expectedOffset), $"nested subfile {subfileIndex} offset");
        RequireUInt32(descriptor.AsSpan(4, 4), checked((uint)expectedLength), $"nested subfile {subfileIndex} length");
    }

    private static byte[] ReadWad(FileStream image, DiscLayout layout, long offset, int length) =>
        DiscImage.ReadFileBytes(image, layout, WadLba, offset, length);

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void RequireZero(ReadOnlySpan<byte> bytes, string label)
    {
        if (bytes.ContainsAnyExcept((byte)0))
            throw new InvalidDataException($"{label} is no longer zero-filled.");
    }

    private static void RequireUInt32(ReadOnlySpan<byte> bytes, long expected, string label)
    {
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        if (actual != expected)
            throw new InvalidDataException($"{label} is 0x{actual:X}, expected 0x{expected:X}.");
    }

    private static void RequireUInt16(ReadOnlySpan<byte> bytes, ushort expected, string label)
    {
        ushort actual = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        if (actual != expected)
            throw new InvalidDataException($"{label} is 0x{actual:X4}, expected 0x{expected:X4}.");
    }

    private static int ReadInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));

    private sealed record ActorRootTable(int[] Roots, ushort[] ActorIds, int PopulatedCount);
}
