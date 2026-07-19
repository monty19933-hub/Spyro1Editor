using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public sealed record ToastyWizardOverlayExperimentPlan(
    string RecipeId,
    string RetiredRecipeId,
    string RetiredRecipeReason,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    int WadLba,
    int ToastyOverlayEntry,
    int WizardPeakOverlayEntry,
    int ToastyOverlayBytes,
    int WizardPeakOverlayBytes,
    int WizardPeakCopiedBytes,
    int WizardPeakExcludedTailBytes,
    int ParticleBundleBytes,
    int ShimBytes,
    int PaddingBytes,
    int ExpandedToastyOverlayBytes,
    int WadGrowthBytes,
    int OriginalExecutableLba,
    int RelocatedExecutableLba,
    int AvailableGrowthBytes,
    uint OverlayLoadAddress,
    uint OriginalCopyBufferAddress,
    uint RelocatedCopyBufferAddress,
    uint CopyBufferCeilingAddress,
    uint LowerPolygonBufferAddress,
    int PolygonBufferMarginBytes,
    uint DispatchShimAddress,
    uint LightningTrailParticleTextureSlotAddress,
    uint LightningTrailParticleDescriptorAddress,
    int LightningTrailParticleSourceDescriptorOffset,
    int LightningTrailParticleDescriptorBytes,
    string LightningTrailParticleSourceDescriptorSha256,
    string LightningTrailParticleInstalledDescriptorSha256,
    uint GreenWizardHandlerAddress,
    uint LightningHandlerAddress,
    uint LightningDamagePatchAddress,
    uint LightningDamageOriginalWord,
    uint LightningDamagePatchedWord,
    uint OriginalLightningDamageFlags,
    uint PatchedLightningDamageFlags,
    uint PatchedSpyroHurtState,
    uint PatchedSpyroAnimation,
    uint LightningImpactBurstPatchAddress,
    uint LightningImpactBurstOriginalWord,
    uint LightningImpactBurstPatchedWord,
    uint LightningImpactSafeExitAddress,
    int SuppressedLightningImpactChildCount,
    uint WizardSpawnInitializerAddress,
    uint ParticleSpawnType7Address,
    uint ParticleSpawnType41Address,
    uint ParticleUpdateType7Address,
    uint ParticleUpdateType41Address,
    int RelocatedJumpCount,
    int PreservedBranchCount,
    int RelocatedPointerCount,
    int ExcludedTailPointerCount,
    int RelocatedHiLoPairCount,
    int LightningPackageBytes,
    int LightningPackageTargetOffset,
    int LightningRootSlotOffset,
    int ToastySceneBaseOffset,
    int WizardPeakSceneBaseOffset,
    int ToastyT0PropertiesPointerOffset,
    uint ImportedToastyT0PropertiesPointer,
    uint RestoredToastyT0PropertiesPointer,
    int WizardPeakPropertiesOffset,
    int ToastyPropertiesOffset,
    int PropertiesBytes,
    uint RebasedPropertiesInternalPointer,
    IReadOnlyList<ToastyWizardRoutePoint> TranslatedRoutePoints,
    int ToastyPointerFixupListOffset,
    int PointerFixupCountBefore,
    int PointerFixupCountAfter,
    uint RemovedPointerFixup,
    int ExecutableCopyBufferHiOffset,
    int ExecutableCopyBufferLoOffset,
    int TextureDependencyBytes,
    int TexturePixelBytes,
    int TextureClutBytes,
    string TextureDependencyDonorSha256,
    string TextureDependencyTargetPreimageSha256,
    IReadOnlyList<ToastyWizardTextureDependencyRegion> TextureDependencyRegions,
    IReadOnlyList<string> FocusedChecks);

public sealed record ToastyWizardRoutePoint(int X, int Y, int Z);

public sealed record ToastyWizardTextureDependencyRegion(
    string Label,
    int PixelOffset,
    int PixelRows,
    int PixelRowBytes,
    int PixelRowStrideBytes,
    int ClutOffset,
    int ClutBytes,
    string DonorPixelsSha256,
    string TargetPixelsPreimageSha256,
    string DonorClutSha256,
    string TargetClutPreimageSha256,
    int? DonorPixelOffset = null,
    int? DonorClutOffset = null);

public static class ToastyWizardOverlayExperimentExporter
{
    public const string RecipeId = "toasty.wizardpeak.greenWizard.compactDependencyBundle.propsFixup.impactBurstQuarantine.genericHurtDamage.textureDependencies.lightningTrailParticleTexture.v11";
    public const string RetiredV8RecipeId = "toasty.wizardpeak.greenWizard.compactDependencyBundle.propsFixup.impactBurstQuarantine.zappedFilterAlphaQuarantine.v8";
    public const string RetiredV8Reason = "Retired by live DuckStation evidence: the exact v8 BIN and SCUS alpha-zero patch were active, Spyro's filter alpha was zero, and the four impact children remained absent. Electric state 7 selected Spyro body animation 8, but Toasty's live g_Models animation-8 slot at 0x801C5C10 was null; the renderer then treated address zero as the model and ended in a Reserved Instruction exception at PC 0. v9 retains real damage but maps only this transplanted lightning hit to native generic hurt state 0x0E and its populated animation 15.";

    private const int SectorBytes = 2048;
    private const int ToastyOverlayEntry = 17;
    private const int ToastyDataEntry = 18;
    private const int WizardPeakOverlayEntry = 39;
    private const int WizardPeakDataEntry = 40;
    private const uint OverlayLoadAddress = 0x8007AA38;
    private const uint SourceCodeAddress = 0x8007B7A8;
    private const uint SourceCompactEndAddress = 0x80087B40;
    private const uint SourceOverlayEndAddress = 0x8008AA38;
    private const uint OriginalToastyCopyBufferAddress = 0x80084A10;
    private const uint CopyBufferCeilingAddress = 0x800926D4;
    private const uint LowerPolygonBufferAddress = 0x80187BB0;
    private const int ToastyPostCopyBufferFootprint = 0xF5220;
    private const int MaximumExpandedOverlayBytes = 0x17800;
    private const uint ToastyDispatchHookAddress = 0x8007B078;
    private const uint ToastyDispatchContinueAddress = 0x8007B084;
    private const uint ToastyFbHandlerAddress = 0x8007F358;
    private const uint ToastyNextMobyAddress = 0x80081D5C;
    private const uint SourceLightningHandlerAddress = 0x8007EAD0;
    private const uint SourceLightningDamageFlagsSetupAddress = 0x8007EB88;
    private const uint SourceLightningHitCollisionCallAddress = 0x8007EB8C;
    private const uint SourceLightningImpactBurstAddress = 0x8007EB9C;
    private const uint SourceLightningNoHitAddress = 0x8007EBFC;
    private const uint SourceLightningImpactSafeExitAddress = 0x80086A50;
    private const uint SourceLightningImpactBurstOriginalWord = 0x0C0189CB;
    private const uint SourceLightningImpactBurstDelaySlotWord = 0x00008821;
    private const uint SourceLightningDamageFlagsHighWord = 0x3C060002;
    private const uint SourceLightningDamageFlagsLowWord = 0x34C60020;
    private const uint SourceLightningGenericHurtLowWord = 0x34060001;
    private const uint OriginalLightningDamageFlags = 0x00020020;
    private const uint PatchedLightningDamageFlags = 0x00000001;
    private const uint PatchedSpyroHurtState = 0x0000000E;
    private const uint PatchedSpyroAnimation = 0x0000000F;
    private const int SuppressedLightningImpactChildCount = 4;
    private const uint SourceGreenWizardHandlerAddress = 0x80082818;
    private const uint SourceMainExitAddress = 0x80086D8C;
    private const uint SourceSpawnInitializerAddress = 0x80086DD8;
    private static readonly uint[] SourceSpawnCallAddresses = [0x8007EBDC, 0x80082DB4, 0x80082DF0, 0x80082E38];
    private const int LightningSourcePackageOffset = 0x1CAC88;
    private const int LightningTargetPackageOffset = 0x156818;
    private const int LightningPackageBytes = 0x564;
    private const int LightningSourceRootSlotOffset = 0xB4;
    private const int LightningTargetRootSlotOffset = 0xB0;
    private const int ActorIdTableOffset = 0x128;
    private const ushort LightningActorId = 0x0026;
    private const int ExecutableCopyBufferHiOffset = 0x4B130;
    private const int ExecutableCopyBufferLoOffset = 0x4B134;
    private const uint OriginalExecutableCopyBufferHiWord = 0x3C028008;
    private const uint OriginalExecutableCopyBufferLoWord = 0x24424A10;
    private const uint ToastyParticleSpawnTableAddress = 0x8007AE0C;
    private const uint ToastyParticleUpdateTableAddress = 0x8007ACC8;
    private const uint ToastyParticleSpawnContinueAddress = 0x800849B8;
    private const uint ToastyParticleUpdateContinueAddress = 0x800838CC;
    private const uint ToastyParticleUpdateDeleteAddress = 0x800838C4;
    private const uint ToastyParticleUpdateSharedAddress = 0x800838A8;
    private const uint ParticleTexturePointerTableAddress = 0x80076278;
    private const int LightningTrailParticleType = 0x07;
    private const uint LightningTrailParticleTextureSlotAddress = ParticleTexturePointerTableAddress + (LightningTrailParticleType * 4);
    private const int LightningTrailParticleSourceDescriptorOffset = 0x173518;
    private const int LightningTrailParticleDescriptorBytes = 0x0C;
    private const int LightningTrailParticleDescriptorShimOffset = 0x94;
    private const string LightningTrailParticleSourceDescriptorSha256 = "4baed93eb98ac71d27f59e3ddf8b5382dda7e74319de188749dce3831397f7b3";
    private const string LightningTrailParticleInstalledDescriptorSha256 = "82f2f94882c9e383d7fdbf278cd615c1a78a015751a4d617a108d399081c38f5";
    private const int ParticleBundleBytes = 0x380;
    private const string LightningPackageSha256 = "9ea5c72ba675e9c6a81433f7408695399e19bc188b4b2a923069d71de601789a";
    private const string ParticleBundleSha256 = "eaab7adf4f1d5657c9d1ed66a5e944b007a560b5cebb3516705ee6cda62b38fc";
    private const int ToastySceneBaseOffset = 0x1B7000;
    private const int ToastySceneBytes = 0xD800;
    private const int WizardPeakSceneBaseOffset = 0x1CE800;
    private const int SceneBaseHeaderOffset = 0x18;
    private const int SceneSizeHeaderOffset = 0x1C;
    private const int ToastyT0PropertiesPointerSceneOffset = 0x629C;
    private const uint ImportedToastyT0PropertiesPointer = 0x10860;
    private const uint RestoredToastyT0PropertiesPointer = 0x9EE8;
    private const int WizardPeakPropertiesSceneOffset = 0xC350;
    private const int ToastyPropertiesSceneOffset = 0x9EE8;
    private const int WizardPropertiesBytes = 0x50;
    private const uint WizardSourceInternalPointer = 0xC378;
    private const uint ToastyRebasedInternalPointer = 0x9F10;
    private const int FirstRoutePointPropertiesOffset = 0x30;
    private const int RoutePointStride = 0x10;
    private const int PointerFixupListSceneOffset = 0xD314;
    private const int PointerFixupCountBefore = 0x62;
    private const int PointerFixupCountAfter = 0x61;
    private const uint RemovedPointerFixup = 0x9F28;
    private const string WizardPropertiesSha256 = "5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b";
    private const string ToastyPropertiesPreimageSha256 = "7ba44839ac22e9fe3e6bc5512b502d716639e18390d09a42567450d4979c0c47";
    private const string ToastyPointerFixupPreimageSha256 = "0d9d2f824e1a22c9eaea0a8f01b4142d3b37795387e27c42b7fee12596c75c6e";
    private const string ToastyPropertiesResultSha256 = "7355013684269348d96a79107c7e6ad18ba991ceb00c02a0ed3bf986d54e15c7";
    private const string ToastyPointerFixupResultSha256 = "2a4b43e97eb7fa8d995fb00169fa42c8bed785e37f7790d38aa20efa9176f037";
    private const int TextureDependencyBytes = 2216;
    private const int TexturePixelBytes = 2056;
    private const int TextureClutBytes = 160;
    private const string TextureDependencyDonorSha256 = "9448477af6f60a253f43c49e0642f8ce406027ff811dd37b41a4b1d1f4efbfc7";
    private const string TextureDependencyTargetPreimageSha256 = "43d5062f7ec0fa5aba2a423a12e9f8296a02b878f833eaf90a72571af00b65e1";
    private static readonly ToastyWizardTextureDependencyRegion[] TextureDependencyRegions =
    [
        new(
            "Wizard accent",
            0x50900,
            32,
            0x10,
            0x400,
            0x7A8E0,
            0x20,
            "8ff165f1d889594f9221913fdfa69f471eb33c57ea0780be4b1ddf8f22317c12",
            "2d0ba2bbf0173d3abfdae683ca0addbcb837b8296f46218b444871d7fa09b38d",
            "60a0eb6d64d068d96e76de01db8bf4b942d0771fefbef6baed7d2964940522af",
            "d09a0a5129dbe7a6918a245979964d2b7cdfba2dc28cad3bf38cb2d7534d991a"),
        new(
            "Wizard special",
            0x58910,
            32,
            0x10,
            0x400,
            0x708A0,
            0x20,
            "2229d62ddadd04752fcc5790b019b55d166b774007ab646e46f1d7a440069dea",
            "6a50dfa98235d46a36c173c6e52440c08e549ec541db76545ef1ca950b964d06",
            "5cb82309aae7685152d17f22df8e9e8a55d5202b449adf08f580a254038798a1",
            "c3532586916a3ab267e554e3ec749c693d221833095527d96f3d141e0b4cf4d1"),
        new(
            "Wizard main",
            0x58960,
            32,
            0x10,
            0x400,
            0x72980,
            0x20,
            "6832e7f686f47f7224b1e7b1bb136885d2326650bde4eaf99cffce3c74dba445",
            "8c691b13eb6cf14816f01d18335277075783b482065dec7eddb5903b86b85401",
            "98c5ea6b2eaeeb703c65f10e6ba11e9f2d5b4272a4bbc261d00ef178cfd397ba",
            "6e684213fd836b22512bcad1dc2c7d0558adda6fbefece005598688bbc2e6257"),
        new(
            "Lightning",
            0x70850,
            4,
            0x02,
            0x400,
            0x78840,
            0x20,
            "e6f48a0036f29213687545ad901eb55949d15e150213f2db8b32f248d55ec411",
            "015d0f8bec5b09f6315e270c74f087aaa8afdd3bbedae61231a040a33bb949f1",
            "ab654734be1754fd47a82b2974389dac5b3bcca0e0015604e6724b56d1492d00",
            "b1d0e05858a79820fc5dec9197e6678e9075fc4f05dba5548736a17c8bd6eb71"),
        new(
            "Lightning trail particle 0x07",
            0x78AB0,
            32,
            0x10,
            0x400,
            0x34C60,
            0x20,
            "31f14b4cd1994f468e2bd1d9d1937ca4e7bccd276cd32efa37ecfba4ecb92249",
            "076a27c79e5ace2a3d47f9dd2e83e4ff6ea8872b3c2218f66c92b89b55f36560",
            "c8c516666d7b96b4039b451cffcf69e8c610a93a112532859932befbd2ea9384",
            "66687aadf862bd776c8fc18b8e9f8e20089714856ee233b3902a591d0d5f2925",
            DonorPixelOffset: 0x78810,
            DonorClutOffset: 0x759A0)
    ];
    private static readonly ToastyWizardRoutePoint[] TranslatedRoutePoints =
    [
        new(119081, 109773, 16163),
        new(112558, 106629, 16163)
    ];

    public static ToastyWizardOverlayExperimentPlan BuildAndWrite(
        string sourceImagePath,
        string sourceCuePath,
        string wadAnalysisPath,
        string outputImagePath,
        string outputCuePath,
        string planPath)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("The proven Toasty Green Wizard v3 image is missing.", sourceImagePath);

        WadLayout wad = LoadWadLayout(wadAnalysisPath);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream source = File.Open(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        List<IsoRootRecord> rootFiles = ReadRootFiles(source, layout);
        IsoRootRecord wadFile = rootFiles.Single(file => file.Name.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase) || file.Name.Equals("WAD", StringComparison.OrdinalIgnoreCase));
        IsoRootRecord executable = rootFiles.Single(file => IsExecutableName(file.Name));
        IsoRootRecord nextFile = rootFiles.Where(file => file.Lba > executable.Lba).OrderBy(file => file.Lba).First();
        if (wadFile.Lba != wad.WadLba || wadFile.Size != wad.WadSize)
            throw new InvalidDataException("The live WAD extent does not match the checked analysis.");
        if (executable.Lba != wad.WadLba + (wad.WadSize / SectorBytes))
            throw new InvalidDataException("The executable does not immediately follow WAD.WAD.");

        WadEntry toasty = wad.EntriesByIndex[ToastyOverlayEntry];
        WadEntry toastyDataEntry = wad.EntriesByIndex[ToastyDataEntry];
        WadEntry wizard = wad.EntriesByIndex[WizardPeakOverlayEntry];
        WadEntry wizardDataEntry = wad.EntriesByIndex[WizardPeakDataEntry];
        if (toasty.Size != 0xA000 || wizard.Size != 0x10000)
            throw new InvalidDataException($"Unexpected overlay sizes: Toasty=0x{toasty.Size:X}, Wizard Peak=0x{wizard.Size:X}.");

        byte[] toastyBytes = DiscImage.ReadFileBytes(source, layout, wad.WadLba, toasty.Offset, toasty.Size);
        byte[] wizardBytes = DiscImage.ReadFileBytes(source, layout, wad.WadLba, wizard.Offset, wizard.Size);
        byte[] toastyDataBytes = DiscImage.ReadFileBytes(source, layout, wad.WadLba, toastyDataEntry.Offset, toastyDataEntry.Size);
        byte[] wizardDataBytes = DiscImage.ReadFileBytes(source, layout, wad.WadLba, wizardDataEntry.Offset, wizardDataEntry.Size);
        ValidateWord(toastyBytes, ToastyDispatchHookAddress, 0x240200FB, "Toasty dispatch hook");
        ValidateWord(wizardBytes, SourceLightningHandlerAddress, 0x24090002, "Wizard Peak lightning handler");
        ValidateWord(wizardBytes, SourceGreenWizardHandlerAddress, 0x24090002, "Wizard Peak Green Wizard handler");
        ValidateWord(wizardBytes, SourceMainExitAddress, 0x8FA901C0, "Wizard Peak shared exit");
        ValidateWord(wizardBytes, SourceSpawnInitializerAddress, 0x27BDFFA8, "Wizard Peak spawn initializer");

        int compactSourceBytes = checked((int)(SourceCompactEndAddress - OverlayLoadAddress));
        int appendDelta = toasty.Size;
        byte[] compactSource = wizardBytes.AsSpan(0, compactSourceBytes).ToArray();
        ValidateSha256(compactSource, "6cc3fc694ebefc487e0609c7ef86155ad880910b2e56c6873707c6c44f4d7a25", "compact Wizard overlay source prefix");
        RelocationResult relocation = RelocateOverlayPrefix(compactSource, appendDelta);
        if (relocation is not { Jumps: 357, Branches: 1029, Pointers: 675, ExcludedTailPointers: 178, HiLoPairs: 9 })
        {
            throw new InvalidDataException(
                $"The compact Wizard relocation expected 357/1029/675/178/9 J/branch/pointer/excluded/HI-LO fixups, found " +
                $"{relocation.Jumps}/{relocation.Branches}/{relocation.Pointers}/{relocation.ExcludedTailPointers}/{relocation.HiLoPairs}.");
        }
        uint particleBundleAddress = checked(OverlayLoadAddress + (uint)toasty.Size + (uint)compactSourceBytes);
        ParticleBundle particleBundle = BuildParticleBundle(wizardBytes, particleBundleAddress);
        if (particleBundle.Bytes.Length != ParticleBundleBytes)
            throw new InvalidDataException($"The Wizard particle closure expected 0x{ParticleBundleBytes:X} bytes, found 0x{particleBundle.Bytes.Length:X}.");
        uint dispatchShimAddress = checked(particleBundleAddress + (uint)particleBundle.Bytes.Length);
        byte[] lightningTrailParticleDescriptor = BuildLightningTrailParticleDescriptor(wizardDataBytes);
        uint lightningTrailParticleDescriptorAddress = checked(dispatchShimAddress + LightningTrailParticleDescriptorShimOffset);
        byte[] shim = BuildDispatchShim(dispatchShimAddress, appendDelta, lightningTrailParticleDescriptor);
        int expandedOverlayBytes = AlignSector(checked(toasty.Size + compactSourceBytes + particleBundle.Bytes.Length + shim.Length));
        int paddingBytes = expandedOverlayBytes - toasty.Size - compactSourceBytes - particleBundle.Bytes.Length - shim.Length;
        if (expandedOverlayBytes > MaximumExpandedOverlayBytes)
            throw new InvalidOperationException($"The compact dependency bundle needs 0x{expandedOverlayBytes:X} bytes; the checked Toasty ceiling is 0x{MaximumExpandedOverlayBytes:X}.");

        uint relocatedCopyBufferAddress = checked(OverlayLoadAddress + (uint)expandedOverlayBytes);
        if (relocatedCopyBufferAddress > CopyBufferCeilingAddress)
            throw new InvalidOperationException($"The relocated copy buffer 0x{relocatedCopyBufferAddress:X8} exceeds the checked ceiling 0x{CopyBufferCeilingAddress:X8}.");
        uint sceneEndAddress = checked(relocatedCopyBufferAddress + ToastyPostCopyBufferFootprint);
        int polygonBufferMarginBytes = checked((int)(LowerPolygonBufferAddress - sceneEndAddress));
        if (polygonBufferMarginBytes < 0x2BC)
            throw new InvalidOperationException($"The compact bundle leaves only 0x{polygonBufferMarginBytes:X} bytes before the lower polygon buffer.");

        byte[] expandedOverlay = new byte[expandedOverlayBytes];
        toastyBytes.CopyTo(expandedOverlay, 0);
        relocation.Bytes.CopyTo(expandedOverlay, toasty.Size);
        particleBundle.Bytes.CopyTo(expandedOverlay, toasty.Size + compactSourceBytes);
        shim.CopyTo(expandedOverlay, toasty.Size + compactSourceBytes + particleBundle.Bytes.Length);

        ValidateRelocatedAddressPair(expandedOverlay, 0x80088E10, 0x80088E18, 0x800852A4, "lightning jump table");
        ValidateRelocatedAddressPair(expandedOverlay, 0x8008D1D8, 0x8008D1E0, 0x80085304, "Green Wizard primary jump table");
        ValidateRelocatedAddressPair(expandedOverlay, 0x8008D860, 0x8008D868, 0x80085324, "Green Wizard secondary jump table");

        WriteWord(expandedOverlay, ToastyDispatchHookAddress, EncodeJump(2, dispatchShimAddress));
        WriteWord(expandedOverlay, ToastyDispatchHookAddress + 4, 0);
        uint mappedExit = checked(SourceMainExitAddress + (uint)appendDelta);
        uint returnShimAddress = dispatchShimAddress + 0x80;
        WriteWord(expandedOverlay, mappedExit, EncodeJump(2, returnShimAddress));
        WriteWord(expandedOverlay, mappedExit + 4, 0);
        uint mappedDamageFlagsSetup = checked(SourceLightningDamageFlagsSetupAddress + (uint)appendDelta);
        uint mappedImpactBurst = checked(SourceLightningImpactBurstAddress + (uint)appendDelta);
        uint mappedImpactSafeExit = checked(SourceLightningImpactSafeExitAddress + (uint)appendDelta);
        uint mappedHitCollisionCall = checked(SourceLightningHitCollisionCallAddress + (uint)appendDelta);
        uint mappedNoHit = checked(SourceLightningNoHitAddress + (uint)appendDelta);
        uint impactBurstQuarantineWord = EncodeJump(2, mappedImpactSafeExit);
        ValidateWord(expandedOverlay, mappedDamageFlagsSetup - 4, 0x24050080, "lightning collision radius setup");
        ValidateWord(expandedOverlay, mappedDamageFlagsSetup, SourceLightningDamageFlagsHighWord, "lightning damage-flags high word");
        ValidateWord(expandedOverlay, mappedHitCollisionCall, 0x0C0138BA, "lightning hit/damage collision call");
        ValidateWord(expandedOverlay, mappedHitCollisionCall + 4, SourceLightningDamageFlagsLowWord, "lightning damage-flags low-word delay slot");
        ValidateWord(expandedOverlay, mappedHitCollisionCall + 8, 0x10400019, "lightning no-hit branch");
        ValidateWord(expandedOverlay, mappedHitCollisionCall + 12, 0x02202021, "lightning no-hit branch delay slot");
        ValidateWord(expandedOverlay, mappedImpactBurst, SourceLightningImpactBurstOriginalWord, "lightning impact-burst rand call");
        ValidateWord(expandedOverlay, mappedImpactBurst + 4, SourceLightningImpactBurstDelaySlotWord, "lightning impact-burst delay slot");
        ValidateSha256(
            expandedOverlay.AsSpan(checked((int)(mappedHitCollisionCall - OverlayLoadAddress)), 0x18),
            "2c82369212014da628f0edf57afcb6635d6bf6a7b2bec2400f772153d632226f",
            "lightning hit branch pre-patch window");
        ValidateWord(expandedOverlay, mappedNoHit, 0x0C00DFE4, "lightning no-hit native test");
        ValidateWord(expandedOverlay, mappedNoHit + 4, 0x24050004, "lightning no-hit native-test delay slot");
        ValidateWord(expandedOverlay, mappedNoHit + 8, 0x14401F92, "lightning no-hit cleanup branch");
        ValidateWord(expandedOverlay, mappedNoHit + 12, 0x02602021, "lightning no-hit cleanup delay slot");
        ValidateWord(expandedOverlay, mappedImpactSafeExit, 0x0C01495A, "lightning shared projectile delete");
        ValidateWord(expandedOverlay, mappedImpactSafeExit + 4, 0x02602021, "lightning shared projectile-delete delay slot");
        ValidateWord(expandedOverlay, mappedImpactSafeExit + 8, 0x08024363, "lightning shared main-exit jump");
        ValidateWord(expandedOverlay, mappedImpactSafeExit + 12, 0x00000000, "lightning shared main-exit delay slot");
        ValidateSha256(
            expandedOverlay.AsSpan(checked((int)(mappedImpactSafeExit - OverlayLoadAddress)), 0x10),
            "026e82b5d53244514709459ea784f4ac6c221fa3890ac6e792888acbf116fa6c",
            "lightning shared projectile-delete window");
        WriteWord(expandedOverlay, mappedImpactBurst, impactBurstQuarantineWord);
        ValidateWord(expandedOverlay, mappedImpactBurst, impactBurstQuarantineWord, "lightning impact-burst quarantine");
        ValidateSha256(
            expandedOverlay.AsSpan(checked((int)(mappedHitCollisionCall - OverlayLoadAddress)), 0x18),
            "91ab1cd60f257d68278fe46029fff839a5f0cddbfe681754087d36681ea44859",
            "lightning hit branch post-patch window");
        ValidateSha256(
            expandedOverlay.AsSpan(checked((int)(mappedDamageFlagsSetup - 4 - OverlayLoadAddress)), 0x20),
            "343b19f4f972ba727155d0b587cac3578533b2cefef9b0c57bf7cab1ae8c9050",
            "v8 lightning hit/damage window before generic-hurt quarantine");
        WriteWord(expandedOverlay, mappedHitCollisionCall + 4, SourceLightningGenericHurtLowWord);
        ValidateWord(expandedOverlay, mappedHitCollisionCall + 4, SourceLightningGenericHurtLowWord, "lightning generic-hurt damage delay slot");
        ValidateSha256(
            expandedOverlay.AsSpan(checked((int)(mappedDamageFlagsSetup - 4 - OverlayLoadAddress)), 0x20),
            "24605022ec77d0ac7ee88288adec4df8f5b88460191090dfa2d093d465b15080",
            "v9 lightning hit/damage window after generic-hurt quarantine");
        uint mappedSpawn = checked(SourceSpawnInitializerAddress + (uint)appendDelta);
        foreach (uint call in SourceSpawnCallAddresses)
            WriteWord(expandedOverlay, checked(call + (uint)appendDelta), EncodeJump(3, mappedSpawn));
        ValidateWord(expandedOverlay, ToastyParticleSpawnTableAddress + (7 * 4), ToastyParticleSpawnContinueAddress, "Toasty particle spawn type 0x07 default");
        ValidateWord(expandedOverlay, ToastyParticleSpawnTableAddress + (0x41 * 4), ToastyParticleSpawnContinueAddress, "Toasty particle spawn type 0x41 default");
        ValidateWord(expandedOverlay, ToastyParticleUpdateTableAddress + (7 * 4), ToastyParticleUpdateContinueAddress, "Toasty particle update type 0x07 default");
        ValidateWord(expandedOverlay, ToastyParticleUpdateTableAddress + (0x41 * 4), ToastyParticleUpdateContinueAddress, "Toasty particle update type 0x41 default");
        WriteWord(expandedOverlay, ToastyParticleSpawnTableAddress + (7 * 4), particleBundle.SpawnType7Address);
        WriteWord(expandedOverlay, ToastyParticleSpawnTableAddress + (0x41 * 4), particleBundle.SpawnType41Address);
        WriteWord(expandedOverlay, ToastyParticleUpdateTableAddress + (7 * 4), particleBundle.UpdateType7Address);
        WriteWord(expandedOverlay, ToastyParticleUpdateTableAddress + (0x41 * 4), particleBundle.UpdateType41Address);

        InstallWizardTextureDependencies(toastyDataBytes, wizardDataBytes);
        InstallLightningPackage(toastyDataBytes, wizardDataBytes);
        InstallGreenWizardProperties(toastyDataBytes, wizardDataBytes);

        int growth = expandedOverlay.Length - toasty.Size;
        int relocatedExecutableLba = checked(executable.Lba + (growth / SectorBytes));
        int executableSectors = DivideRoundUp(executable.Size, SectorBytes);
        int availableGrowth = checked((nextFile.Lba - executable.Lba - executableSectors) * SectorBytes);
        if (growth > availableGrowth || relocatedExecutableLba + executableSectors > nextFile.Lba)
            throw new InvalidOperationException($"The overlay bundle needs {growth:N0} bytes but only {availableGrowth:N0} bytes are safely available.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath)!);
        File.Copy(sourceImagePath, outputImagePath, true);
        using FileStream output = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        byte[] wadHeader = DiscImage.ReadFileBytes(source, layout, wad.WadLba, 0, SectorBytes);
        long relocatedCursor = SectorBytes;
        foreach (WadEntry entry in wad.Entries.OrderBy(entry => entry.Offset))
        {
            byte[] bytes = entry.Index switch
            {
                ToastyOverlayEntry => expandedOverlay,
                ToastyDataEntry => toastyDataBytes,
                _ => DiscImage.ReadFileBytes(source, layout, wad.WadLba, entry.Offset, entry.Size)
            };
            WriteUInt32(wadHeader, entry.Index * 8, checked((uint)relocatedCursor));
            WriteUInt32(wadHeader, (entry.Index * 8) + 4, checked((uint)bytes.Length));
            DiscImage.WriteFileBytes(output, layout, wad.WadLba, relocatedCursor, bytes);
            relocatedCursor += bytes.Length;
        }
        int expandedWadSize = checked((int)relocatedCursor);
        if (expandedWadSize != wad.WadSize + growth)
            throw new InvalidDataException("Expanded WAD accounting did not balance.");
        DiscImage.WriteFileBytes(output, layout, wad.WadLba, 0, wadHeader);

        byte[] executableBytes = DiscImage.ReadFileBytes(source, layout, executable.Lba, 0, executable.Size);
        ValidateWordAtOffset(executableBytes, ExecutableCopyBufferHiOffset, OriginalExecutableCopyBufferHiWord, "Toasty copy-buffer HI16");
        ValidateWordAtOffset(executableBytes, ExecutableCopyBufferLoOffset, OriginalExecutableCopyBufferLoWord, "Toasty copy-buffer LO16");
        (uint copyBufferHiWord, uint copyBufferLoWord) = EncodeAddressPair(2, relocatedCopyBufferAddress);
        WriteWordAtOffset(executableBytes, ExecutableCopyBufferHiOffset, copyBufferHiWord);
        WriteWordAtOffset(executableBytes, ExecutableCopyBufferLoOffset, copyBufferLoWord);
        DiscImage.WriteFileBytes(output, layout, relocatedExecutableLba, 0, executableBytes);
        byte[] rootDirectory = DiscImage.ReadFileBytes(output, layout, layout.RootExtent, 0, layout.RootLength);
        PatchRootRecord(rootDirectory, wadFile.Name, wadFile.Lba, expandedWadSize);
        PatchRootRecord(rootDirectory, executable.Name, relocatedExecutableLba, executable.Size);
        DiscImage.WriteFileBytes(output, layout, layout.RootExtent, 0, rootDirectory);
        output.Flush();

        byte[] readback = DiscImage.ReadFileBytes(output, layout, wad.WadLba, toasty.Offset, expandedOverlay.Length);
        if (!readback.AsSpan().SequenceEqual(expandedOverlay))
            throw new InvalidDataException("Expanded Toasty overlay readback failed.");
        byte[] dataReadback = DiscImage.ReadFileBytes(output, layout, wad.WadLba, toasty.Offset + expandedOverlay.Length, toastyDataBytes.Length);
        if (!dataReadback.AsSpan().SequenceEqual(toastyDataBytes))
            throw new InvalidDataException("Toasty v11 data-entry readback failed.");
        byte[] relocatedExeReadback = DiscImage.ReadFileBytes(output, layout, relocatedExecutableLba, 0, executable.Size);
        if (!relocatedExeReadback.AsSpan().SequenceEqual(executableBytes))
            throw new InvalidDataException("Relocated executable readback failed.");
        ValidateWordAtOffset(relocatedExeReadback, ExecutableCopyBufferHiOffset, copyBufferHiWord, "relocated Toasty copy-buffer HI16");
        ValidateWordAtOffset(relocatedExeReadback, ExecutableCopyBufferLoOffset, copyBufferLoWord, "relocated Toasty copy-buffer LO16");

        File.WriteAllText(outputCuePath, DiscImage.BuildCueText(sourceCuePath, Path.GetFileName(outputImagePath)), Encoding.ASCII);
        ToastyWizardOverlayExperimentPlan plan = new(
            RecipeId,
            RetiredV8RecipeId,
            RetiredV8Reason,
            sourceImagePath,
            outputImagePath,
            outputCuePath,
            wad.WadLba,
            ToastyOverlayEntry,
            WizardPeakOverlayEntry,
            toasty.Size,
            wizard.Size,
            compactSourceBytes,
            wizard.Size - compactSourceBytes,
            particleBundle.Bytes.Length,
            shim.Length,
            paddingBytes,
            expandedOverlay.Length,
            growth,
            executable.Lba,
            relocatedExecutableLba,
            availableGrowth,
            OverlayLoadAddress,
            OriginalToastyCopyBufferAddress,
            relocatedCopyBufferAddress,
            CopyBufferCeilingAddress,
            LowerPolygonBufferAddress,
            polygonBufferMarginBytes,
            dispatchShimAddress,
            LightningTrailParticleTextureSlotAddress,
            lightningTrailParticleDescriptorAddress,
            LightningTrailParticleSourceDescriptorOffset,
            LightningTrailParticleDescriptorBytes,
            LightningTrailParticleSourceDescriptorSha256,
            LightningTrailParticleInstalledDescriptorSha256,
            checked(SourceGreenWizardHandlerAddress + (uint)appendDelta),
            checked(SourceLightningHandlerAddress + (uint)appendDelta),
            mappedHitCollisionCall + 4,
            SourceLightningDamageFlagsLowWord,
            SourceLightningGenericHurtLowWord,
            OriginalLightningDamageFlags,
            PatchedLightningDamageFlags,
            PatchedSpyroHurtState,
            PatchedSpyroAnimation,
            mappedImpactBurst,
            SourceLightningImpactBurstOriginalWord,
            impactBurstQuarantineWord,
            mappedImpactSafeExit,
            SuppressedLightningImpactChildCount,
            mappedSpawn,
            particleBundle.SpawnType7Address,
            particleBundle.SpawnType41Address,
            particleBundle.UpdateType7Address,
            particleBundle.UpdateType41Address,
            relocation.Jumps,
            relocation.Branches,
            relocation.Pointers,
            relocation.ExcludedTailPointers,
            relocation.HiLoPairs,
            LightningPackageBytes,
            LightningTargetPackageOffset,
            LightningTargetRootSlotOffset,
            ToastySceneBaseOffset,
            WizardPeakSceneBaseOffset,
            ToastyT0PropertiesPointerSceneOffset,
            ImportedToastyT0PropertiesPointer,
            RestoredToastyT0PropertiesPointer,
            WizardPeakPropertiesSceneOffset,
            ToastyPropertiesSceneOffset,
            WizardPropertiesBytes,
            ToastyRebasedInternalPointer,
            TranslatedRoutePoints,
            PointerFixupListSceneOffset,
            PointerFixupCountBefore,
            PointerFixupCountAfter,
            RemovedPointerFixup,
            ExecutableCopyBufferHiOffset,
            ExecutableCopyBufferLoOffset,
            TextureDependencyBytes,
            TexturePixelBytes,
            TextureClutBytes,
            TextureDependencyDonorSha256,
            TextureDependencyTargetPreimageSha256,
            TextureDependencyRegions,
            [
                "v8 retirement: the exact alpha-zero patch was live and filter alpha was zero, but state 7 selected Spyro animation 8 whose Toasty g_Models slot at 0x801C5C10 was null, causing the renderer to use address zero as model data.",
                "Properties closure: Toasty T0 points to 0x9EE8, which contains the guarded native Wizard T6 0x50-byte properties structure rebased to 0x9F10 with Toasty-space route points.",
                "Relocation closure: the obsolete 0x9F28 properties-word fixup is removed exactly once and the Toasty fixup count is reduced from 0x62 to 0x61.",
                "Handler closure: actor 0x011B dispatches to the relocated Wizard Peak Green Wizard handler with J/JAL, jump-table, and HI16/LO16 fixups.",
                "Lightning closure: actor 0x0026 retains its projectile model/package, initializer, update handler, collision test, health damage, invulnerability, and shared cleanup path.",
                "Impact safety: the successful-hit rand call at 0x80088B9C is replaced by a guarded jump to shared cleanup at 0x80090A50, suppressing only the four unsafe state-3 impact children.",
                "State-7 safety: the successful-hit delay slot at 0x80088B90 now supplies native damage flag 1 instead of 0x00020020, selecting generic hurt state 0x0E and populated Spyro animation 15 instead of Toasty's null animation-8 slot, without modifying global SCUS behavior.",
                "Texture closure: all 51 textured Green Wizard faces, all 9 textured lightning faces, and the single-frame type-0x07 lightning-trail billboard receive 2,216 guarded pixel/CLUT bytes.",
                "Particle root cause: Toasty's native particle-texture table has no type-0x07 record, while the imported spawn path allocates textured render class 2 and indexes pointer slot 0x80076294.",
                "Particle rebase: the 32x32 4-bpp trail tile and 16-color CLUT are moved away from their occupied Wizard Peak coordinates, and a checked 12-byte descriptor in overlay padding points to the isolated Toasty destinations.",
                "Runtime layout: the SCUS copy-buffer constant is relocated beyond the compact code while retaining the checked polygon-buffer margin.",
                "Shared paths: relocated animation/death control flow returns through a checked Toasty register/loop shim."
            ]);
        File.WriteAllText(planPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        return plan;
    }

    private static RelocationResult RelocateOverlayPrefix(byte[] source, int delta)
    {
        byte[] original = source.ToArray();
        byte[] result = source.ToArray();
        int codeOffset = checked((int)(SourceCodeAddress - OverlayLoadAddress));
        int jumps = 0;
        int branches = 0;
        int pointers = 0;
        int excludedTailPointers = 0;
        int hiLoPairs = 0;

        for (int offset = 0; offset < codeOffset; offset += 4)
        {
            uint word = ReadWordAtOffset(original, offset);
            if (word < OverlayLoadAddress || word >= SourceOverlayEndAddress || (word & 3) != 0)
                continue;
            if (word >= SourceCompactEndAddress)
            {
                excludedTailPointers++;
                continue;
            }
            WriteWordAtOffset(result, offset, checked(word + (uint)delta));
            pointers++;
        }

        for (int offset = codeOffset; offset <= original.Length - 4; offset += 4)
        {
            uint word = ReadWordAtOffset(original, offset);
            uint opcode = word >> 26;
            uint pc = checked(OverlayLoadAddress + (uint)offset);
            if (opcode is 2 or 3)
            {
                uint target = DecodeJumpTarget(word, pc);
                if (target >= OverlayLoadAddress && target < SourceOverlayEndAddress)
                {
                    EnsureCopiedTarget(target, pc, "J/JAL");
                    WriteWordAtOffset(result, offset, EncodeJump((int)opcode, checked(target + (uint)delta)));
                    jumps++;
                }
            }
            else if (TryDecodeBranchTarget(word, pc, out uint branchTarget) &&
                     branchTarget >= OverlayLoadAddress && branchTarget < SourceOverlayEndAddress)
            {
                EnsureCopiedTarget(branchTarget, pc, "branch");
                branches++;
            }
        }

        Dictionary<int, ushort> patchedHighWords = [];
        for (int offset = codeOffset; offset <= original.Length - 4; offset += 4)
        {
            uint lui = ReadWordAtOffset(original, offset);
            if ((lui >> 26) != 0x0F)
                continue;
            int register = (int)((lui >> 16) & 0x1F);
            uint high = (lui & 0xFFFF) << 16;
            for (int step = 1; step <= 8 && offset + (step * 4) <= original.Length - 4; step++)
            {
                int lowOffset = offset + (step * 4);
                uint lowWord = ReadWordAtOffset(original, lowOffset);
                if (TryDecodeAddressLow(lowWord, register, high, out uint target, out bool unsignedLow) &&
                    target >= OverlayLoadAddress && target < SourceOverlayEndAddress)
                {
                    EnsureCopiedTarget(target, checked(OverlayLoadAddress + (uint)lowOffset), "HI16/LO16");
                    uint relocated = checked(target + (uint)delta);
                    ushort newHigh = checked((ushort)(unsignedLow ? relocated >> 16 : (relocated + 0x8000) >> 16));
                    ushort newLow = checked((ushort)(relocated & 0xFFFF));
                    if (patchedHighWords.TryGetValue(offset, out ushort existingHigh) && existingHigh != newHigh)
                        throw new InvalidDataException($"One LUI at 0x{OverlayLoadAddress + (uint)offset:X8} feeds incompatible relocated addresses.");
                    patchedHighWords[offset] = newHigh;
                    WriteWordAtOffset(result, offset, (lui & 0xFFFF0000) | newHigh);
                    WriteWordAtOffset(result, lowOffset, (lowWord & 0xFFFF0000) | newLow);
                    hiLoPairs++;
                }

                if (InstructionWritesRegister(lowWord, register) && !PreservesHighBase(lowWord, register))
                    break;
            }
        }

        if (jumps == 0 || pointers == 0 || hiLoPairs == 0)
            throw new InvalidDataException("The compact Wizard relocation did not find all expected fixup classes.");
        return new RelocationResult(result, jumps, branches, pointers, excludedTailPointers, hiLoPairs);
    }

    private static ParticleBundle BuildParticleBundle(byte[] wizard, uint destinationAddress)
    {
        byte[] spawn7 = CopyAddressRange(wizard, 0x80089934, 0x80089A50);
        byte[] spawn7Tail = CopyAddressRange(wizard, 0x80089F28, 0x80089F34);
        byte[] spawn41 = CopyAddressRange(wizard, 0x80089F34, 0x80089FF8);
        byte[] update7 = CopyAddressRange(wizard, 0x80088370, 0x80088408);
        byte[] update41 = CopyAddressRange(wizard, 0x800888E8, 0x800889E4);

        ValidateSha256(spawn7, "c3e0db5def4d5c5c63c10b3e8acf314ebabfa91c93f8a2087a268e78da2df6e3", "particle spawn type 0x07 source");
        ValidateSha256(spawn7Tail, "21561d82f9fa377c0a4deef5824e6367b5833dcf17a128a77e5e590123bf0ae8", "particle spawn type 0x07 tail source");
        ValidateSha256(spawn41, "3ac2b7df71d06fbcf2807956dbd7540d578616a04156cfcdcae92307c5dbf06d", "particle spawn type 0x41 source");
        ValidateSha256(update7, "a2f5681329e63ab489abec1424ecc230f462f4af53bfc5d0e0ae94bb5a09afc4", "particle update type 0x07 source");
        ValidateSha256(update41, "bdfa5cf0752d3ca410a216f83a31d196492bd22de836ccfba9255582a5c01a19", "particle update type 0x41 source");

        uint spawn7Address = destinationAddress;
        uint spawn7TailAddress = checked(spawn7Address + (uint)spawn7.Length);
        uint spawn41Address = checked(spawn7TailAddress + (uint)spawn7Tail.Length);
        uint update7Address = checked(spawn41Address + (uint)spawn41.Length);
        uint update41Address = checked(update7Address + (uint)update7.Length);

        PatchFragmentJump(spawn7, 0x80089934, 0x80089A48, 0x80089F28, spawn7TailAddress, spawn7Address);
        PatchFragmentJump(spawn7Tail, 0x80089F28, 0x80089F2C, 0x8008A848, ToastyParticleSpawnContinueAddress, spawn7TailAddress);
        PatchFragmentJump(spawn41, 0x80089F34, 0x80089FF0, 0x8008A848, ToastyParticleSpawnContinueAddress, spawn41Address);

        int update7Rewrites = ReplaceRegister(update7, 18, 17);
        int update41Rewrites = ReplaceRegister(update41, 18, 17);
        if (update7Rewrites != 18 || update41Rewrites != 19)
            throw new InvalidDataException($"The Wizard particle update adapters expected 18/19 s2-to-s1 rewrites, found {update7Rewrites}/{update41Rewrites}.");
        PatchFragmentBranch(update7, 0x80088370, 0x8008837C, 0x800893EC, ToastyParticleUpdateDeleteAddress, update7Address);
        PatchFragmentBranch(update7, 0x80088370, 0x8008838C, 0x800893EC, ToastyParticleUpdateDeleteAddress, update7Address);
        PatchFragmentJump(update7, 0x80088370, 0x800883B8, 0x800883C4, checked(update7Address + (0x800883C4 - 0x80088370)), update7Address);
        PatchFragmentJump(update7, 0x80088370, 0x80088400, 0x800893F4, ToastyParticleUpdateContinueAddress, update7Address);

        uint[] stackScratchAddresses =
        [
            0x800888FC, 0x80088914, 0x8008892C, 0x80088944, 0x80088958, 0x8008896C,
            0x80088980, 0x80088984, 0x80088994, 0x8008899C, 0x800889B0, 0x800889C8
        ];
        foreach (uint address in stackScratchAddresses)
            PatchFragmentStackOffset(update41, 0x800888E8, address, update41Address);
        PatchFragmentJump(update41, 0x800888E8, 0x800889DC, 0x800893D0, ToastyParticleUpdateSharedAddress, update41Address);

        byte[] result = new byte[checked(spawn7.Length + spawn7Tail.Length + spawn41.Length + update7.Length + update41.Length)];
        int cursor = 0;
        foreach (byte[] fragment in new[] { spawn7, spawn7Tail, spawn41, update7, update41 })
        {
            fragment.CopyTo(result, cursor);
            cursor += fragment.Length;
        }
        ValidateSha256(result, ParticleBundleSha256, "patched particle dependency bundle");
        return new ParticleBundle(result, spawn7Address, spawn41Address, update7Address, update41Address);
    }

    private static byte[] CopyAddressRange(byte[] source, uint startAddress, uint endAddress)
    {
        int start = checked((int)(startAddress - OverlayLoadAddress));
        int length = checked((int)(endAddress - startAddress));
        if (start < 0 || length <= 0 || start + length > source.Length || (length & 3) != 0)
            throw new InvalidDataException($"Invalid Wizard fragment range 0x{startAddress:X8}-0x{endAddress:X8}.");
        return source.AsSpan(start, length).ToArray();
    }

    private static void PatchFragmentJump(
        byte[] fragment,
        uint sourceStart,
        uint sourceInstructionAddress,
        uint expectedTarget,
        uint newTarget,
        uint destinationStart)
    {
        int offset = checked((int)(sourceInstructionAddress - sourceStart));
        uint word = ReadWordAtOffset(fragment, offset);
        int opcode = checked((int)(word >> 26));
        if (opcode is not 2 and not 3 || DecodeJumpTarget(word, sourceInstructionAddress) != expectedTarget)
            throw new InvalidDataException($"Unexpected Wizard fragment jump at 0x{sourceInstructionAddress:X8}.");
        WriteWordAtOffset(fragment, offset, EncodeJump(opcode, newTarget));
        _ = checked(destinationStart + (uint)offset);
    }

    private static void PatchFragmentBranch(
        byte[] fragment,
        uint sourceStart,
        uint sourceInstructionAddress,
        uint expectedTarget,
        uint newTarget,
        uint destinationStart)
    {
        int offset = checked((int)(sourceInstructionAddress - sourceStart));
        uint word = ReadWordAtOffset(fragment, offset);
        if (!TryDecodeBranchTarget(word, sourceInstructionAddress, out uint target) || target != expectedTarget)
            throw new InvalidDataException($"Unexpected Wizard fragment branch at 0x{sourceInstructionAddress:X8}.");
        uint mappedInstruction = checked(destinationStart + (uint)offset);
        long delta = (long)newTarget - (mappedInstruction + 4L);
        if ((delta & 3) != 0 || delta / 4 is < short.MinValue or > short.MaxValue)
            throw new InvalidDataException($"Wizard fragment branch at 0x{mappedInstruction:X8} cannot reach 0x{newTarget:X8}.");
        WriteWordAtOffset(fragment, offset, (word & 0xFFFF0000) | (ushort)(short)(delta / 4));
    }

    private static void PatchFragmentStackOffset(byte[] fragment, uint sourceStart, uint sourceInstructionAddress, uint destinationStart)
    {
        int offset = checked((int)(sourceInstructionAddress - sourceStart));
        uint word = ReadWordAtOffset(fragment, offset);
        int baseRegister = (int)((word >> 21) & 0x1F);
        ushort oldOffset = (ushort)word;
        if (baseRegister != 29 || oldOffset is not (0x70 or 0x72 or 0x74))
            throw new InvalidDataException($"Unexpected Wizard stack scratch instruction at 0x{sourceInstructionAddress:X8}.");
        WriteWordAtOffset(fragment, offset, (word & 0xFFFF0000) | (uint)(oldOffset - 0x40));
        _ = checked(destinationStart + (uint)offset);
    }

    private static int ReplaceRegister(byte[] instructions, int sourceRegister, int targetRegister)
    {
        int replacements = 0;
        for (int offset = 0; offset <= instructions.Length - 4; offset += 4)
        {
            uint word = ReadWordAtOffset(instructions, offset);
            uint opcode = word >> 26;
            if (opcode is 2 or 3 || opcode is >= 0x10 and <= 0x13)
                continue;

            bool useRs = opcode != 0x0F;
            bool useRt = opcode is not (1 or 6 or 7);
            bool useRd = opcode == 0;
            if (useRs && ((word >> 21) & 0x1F) == sourceRegister)
            {
                word = (word & ~(0x1Fu << 21)) | ((uint)targetRegister << 21);
                replacements++;
            }
            if (useRt && ((word >> 16) & 0x1F) == sourceRegister)
            {
                word = (word & ~(0x1Fu << 16)) | ((uint)targetRegister << 16);
                replacements++;
            }
            if (useRd && ((word >> 11) & 0x1F) == sourceRegister)
            {
                word = (word & ~(0x1Fu << 11)) | ((uint)targetRegister << 11);
                replacements++;
            }
            WriteWordAtOffset(instructions, offset, word);
        }
        return replacements;
    }

    private static uint DecodeJumpTarget(uint word, uint instructionAddress) =>
        ((instructionAddress + 4) & 0xF0000000) | ((word & 0x03FFFFFF) << 2);

    private static bool TryDecodeBranchTarget(uint word, uint instructionAddress, out uint target)
    {
        uint opcode = word >> 26;
        bool isBranch = opcode is 1 or 4 or 5 or 6 or 7 ||
                        (opcode is 0x10 or 0x11 or 0x12) && ((word >> 21) & 0x1F) == 8;
        if (!isBranch)
        {
            target = 0;
            return false;
        }
        long address = instructionAddress + 4L + ((long)(short)(word & 0xFFFF) * 4L);
        target = checked((uint)address);
        return true;
    }

    private static bool TryDecodeAddressLow(uint word, int register, uint high, out uint target, out bool unsignedLow)
    {
        uint opcode = word >> 26;
        if (((word >> 21) & 0x1F) != register)
        {
            target = 0;
            unsignedLow = false;
            return false;
        }

        unsignedLow = opcode == 0x0D;
        bool signedAddress = opcode is 0x08 or 0x09 || opcode is >= 0x20 and <= 0x3B;
        if (!unsignedLow && !signedAddress)
        {
            target = 0;
            return false;
        }
        target = unsignedLow
            ? high | (word & 0xFFFF)
            : unchecked((uint)((long)high + (short)(word & 0xFFFF)));
        return true;
    }

    private static bool InstructionWritesRegister(uint word, int register)
    {
        uint opcode = word >> 26;
        if (opcode == 0)
        {
            int function = (int)(word & 0x3F);
            bool writesRd = function is <= 7 or 9 or 0x10 or 0x12 or >= 0x20 and <= 0x2B;
            return writesRd && ((word >> 11) & 0x1F) == register;
        }
        if (opcode == 3)
            return register == 31;
        bool writesRt = opcode is >= 0x08 and <= 0x0F || opcode is >= 0x20 and <= 0x26;
        return writesRt && ((word >> 16) & 0x1F) == register;
    }

    private static bool PreservesHighBase(uint word, int register)
    {
        if ((word >> 26) != 0 || (word & 0x3F) is not (0x20 or 0x21 or 0x25))
            return false;
        int rs = (int)((word >> 21) & 0x1F);
        int rt = (int)((word >> 16) & 0x1F);
        int rd = (int)((word >> 11) & 0x1F);
        return rd == register && (rs == register || rt == register);
    }

    private static void EnsureCopiedTarget(uint target, uint instructionAddress, string kind)
    {
        if (target >= SourceCompactEndAddress)
            throw new InvalidDataException($"Compact Wizard {kind} at 0x{instructionAddress:X8} targets excluded tail address 0x{target:X8}.");
    }

    private static void InstallWizardTextureDependencies(byte[] toastyData, byte[] wizardData)
    {
        byte[] donorAggregate = new byte[TextureDependencyBytes];
        byte[] targetPreimageAggregate = new byte[TextureDependencyBytes];
        int aggregateOffset = 0;
        int pixelBytes = 0;
        int clutBytes = 0;

        foreach (ToastyWizardTextureDependencyRegion region in TextureDependencyRegions)
        {
            int donorPixelOffset = region.DonorPixelOffset ?? region.PixelOffset;
            int donorClutOffset = region.DonorClutOffset ?? region.ClutOffset;
            byte[] donorPixels = ReadStridedRows(
                wizardData,
                donorPixelOffset,
                region.PixelRows,
                region.PixelRowBytes,
                region.PixelRowStrideBytes,
                $"{region.Label} donor pixels");
            byte[] targetPixels = ReadStridedRows(
                toastyData,
                region.PixelOffset,
                region.PixelRows,
                region.PixelRowBytes,
                region.PixelRowStrideBytes,
                $"{region.Label} target pixels");
            byte[] donorClut = ReadCheckedRange(wizardData, donorClutOffset, region.ClutBytes, $"{region.Label} donor CLUT");
            byte[] targetClut = ReadCheckedRange(toastyData, region.ClutOffset, region.ClutBytes, $"{region.Label} target CLUT");

            ValidateSha256(donorPixels, region.DonorPixelsSha256, $"{region.Label} donor pixels");
            ValidateSha256(targetPixels, region.TargetPixelsPreimageSha256, $"{region.Label} Toasty pixel preimage");
            ValidateSha256(donorClut, region.DonorClutSha256, $"{region.Label} donor CLUT");
            ValidateSha256(targetClut, region.TargetClutPreimageSha256, $"{region.Label} Toasty CLUT preimage");

            donorPixels.CopyTo(donorAggregate, aggregateOffset);
            targetPixels.CopyTo(targetPreimageAggregate, aggregateOffset);
            aggregateOffset += donorPixels.Length;
            donorClut.CopyTo(donorAggregate, aggregateOffset);
            targetClut.CopyTo(targetPreimageAggregate, aggregateOffset);
            aggregateOffset += donorClut.Length;
            pixelBytes += donorPixels.Length;
            clutBytes += donorClut.Length;
        }

        if (aggregateOffset != TextureDependencyBytes || pixelBytes != TexturePixelBytes || clutBytes != TextureClutBytes)
        {
            throw new InvalidDataException(
                $"Texture dependency accounting expected {TextureDependencyBytes}/{TexturePixelBytes}/{TextureClutBytes} total/pixel/CLUT bytes, " +
                $"found {aggregateOffset}/{pixelBytes}/{clutBytes}.");
        }
        ValidateSha256(donorAggregate, TextureDependencyDonorSha256, "Wizard/lightning/particle texture dependency donor aggregate");
        ValidateSha256(targetPreimageAggregate, TextureDependencyTargetPreimageSha256, "Toasty texture dependency preimage aggregate");

        foreach (ToastyWizardTextureDependencyRegion region in TextureDependencyRegions)
        {
            int donorPixelOffset = region.DonorPixelOffset ?? region.PixelOffset;
            int donorClutOffset = region.DonorClutOffset ?? region.ClutOffset;
            CopyStridedRows(
                wizardData,
                toastyData,
                donorPixelOffset,
                region.PixelOffset,
                region.PixelRows,
                region.PixelRowBytes,
                region.PixelRowStrideBytes,
                region.Label);
            ReadOnlySpan<byte> sourceClut = ReadCheckedSpan(wizardData, donorClutOffset, region.ClutBytes, $"{region.Label} donor CLUT");
            Span<byte> targetClut = ReadCheckedSpan(toastyData, region.ClutOffset, region.ClutBytes, $"{region.Label} target CLUT");
            sourceClut.CopyTo(targetClut);
        }

        byte[] installedAggregate = CollectTextureDependencyBytes(toastyData);
        ValidateSha256(installedAggregate, TextureDependencyDonorSha256, "installed Toasty Wizard/lightning/particle texture dependencies");
    }

    private static byte[] CollectTextureDependencyBytes(byte[] data)
    {
        byte[] aggregate = new byte[TextureDependencyBytes];
        int aggregateOffset = 0;
        foreach (ToastyWizardTextureDependencyRegion region in TextureDependencyRegions)
        {
            byte[] pixels = ReadStridedRows(
                data,
                region.PixelOffset,
                region.PixelRows,
                region.PixelRowBytes,
                region.PixelRowStrideBytes,
                $"{region.Label} installed pixels");
            pixels.CopyTo(aggregate, aggregateOffset);
            aggregateOffset += pixels.Length;
            ReadOnlySpan<byte> clut = ReadCheckedSpan(data, region.ClutOffset, region.ClutBytes, $"{region.Label} installed CLUT");
            clut.CopyTo(aggregate.AsSpan(aggregateOffset, clut.Length));
            aggregateOffset += clut.Length;
        }
        if (aggregateOffset != aggregate.Length)
            throw new InvalidDataException($"Installed texture dependency aggregate expected {aggregate.Length} bytes, found {aggregateOffset}.");
        return aggregate;
    }

    private static byte[] ReadStridedRows(
        byte[] data,
        int firstRowOffset,
        int rowCount,
        int rowBytes,
        int rowStrideBytes,
        string label)
    {
        if (rowCount <= 0 || rowBytes <= 0 || rowStrideBytes < rowBytes)
            throw new InvalidDataException($"{label} has invalid row geometry {rowCount}x{rowBytes} stride {rowStrideBytes}.");
        byte[] result = new byte[checked(rowCount * rowBytes)];
        for (int row = 0; row < rowCount; row++)
        {
            ReadOnlySpan<byte> source = ReadCheckedSpan(
                data,
                checked(firstRowOffset + (row * rowStrideBytes)),
                rowBytes,
                $"{label} row {row}");
            source.CopyTo(result.AsSpan(row * rowBytes, rowBytes));
        }
        return result;
    }

    private static void CopyStridedRows(
        byte[] source,
        byte[] target,
        int sourceFirstRowOffset,
        int targetFirstRowOffset,
        int rowCount,
        int rowBytes,
        int rowStrideBytes,
        string label)
    {
        for (int row = 0; row < rowCount; row++)
        {
            int sourceRowOffset = checked(sourceFirstRowOffset + (row * rowStrideBytes));
            int targetRowOffset = checked(targetFirstRowOffset + (row * rowStrideBytes));
            ReadOnlySpan<byte> sourceRow = ReadCheckedSpan(source, sourceRowOffset, rowBytes, $"{label} donor row {row}");
            Span<byte> targetRow = ReadCheckedSpan(target, targetRowOffset, rowBytes, $"{label} target row {row}");
            sourceRow.CopyTo(targetRow);
        }
    }

    private static byte[] ReadCheckedRange(byte[] data, int offset, int length, string label) =>
        ReadCheckedSpan(data, offset, length, label).ToArray();

    private static Span<byte> ReadCheckedSpan(byte[] data, int offset, int length, string label)
    {
        if (offset < 0 || length < 0 || offset > data.Length || length > data.Length - offset)
            throw new InvalidDataException($"{label} range 0x{offset:X}+0x{length:X} exceeds data entry length 0x{data.Length:X}.");
        return data.AsSpan(offset, length);
    }

    private static void InstallLightningPackage(byte[] toastyData, byte[] wizardData)
    {
        int sourceActorOffset = ActorIdTableOffset + ((LightningSourceRootSlotOffset / 4) * 2);
        int targetActorOffset = ActorIdTableOffset + ((LightningTargetRootSlotOffset / 4) * 2);
        if (ReadWordAtOffset(wizardData, LightningSourceRootSlotOffset) != LightningSourcePackageOffset ||
            ReadUInt16(wizardData, sourceActorOffset) != LightningActorId)
        {
            throw new InvalidDataException("Wizard Peak actor 0x0026 root/package coordinates changed.");
        }
        if (ReadWordAtOffset(toastyData, LightningTargetRootSlotOffset) != 0 || ReadUInt16(toastyData, targetActorOffset) != 0)
            throw new InvalidDataException("Toasty's checked first empty actor-root slot is no longer empty.");

        ReadOnlySpan<byte> sourcePackage = wizardData.AsSpan(LightningSourcePackageOffset, LightningPackageBytes);
        Span<byte> targetPackage = toastyData.AsSpan(LightningTargetPackageOffset, LightningPackageBytes);
        string sourceHash = ComputeSha256(sourcePackage);
        string targetHash = ComputeSha256(targetPackage);
        if (sourceHash != LightningPackageSha256 ||
            targetHash != "f4655445a60a5c0a3f9632eb534deb72861cd1acd7e40aaa67292d18afcde834")
        {
            throw new InvalidDataException("The checked Wizard lightning donor or Toasty destination gap changed.");
        }
        sourcePackage.CopyTo(targetPackage);
        WriteWordAtOffset(toastyData, LightningTargetRootSlotOffset, LightningTargetPackageOffset);
        WriteUInt16(toastyData, targetActorOffset, LightningActorId);
    }

    private static void InstallGreenWizardProperties(byte[] toastyData, byte[] wizardData)
    {
        int toastySceneBytes = ValidateSceneLayout(
            toastyData,
            ToastySceneBaseOffset,
            ToastySceneBytes,
            "Toasty");
        int wizardSceneBytes = ValidateSceneLayout(
            wizardData,
            WizardPeakSceneBaseOffset,
            expectedSceneBytes: null,
            "Wizard Peak");
        int toastyT0PointerOffset = CheckedSceneOffset(
            toastyData,
            ToastySceneBaseOffset,
            toastySceneBytes,
            ToastyT0PropertiesPointerSceneOffset,
            sizeof(uint),
            "Toasty T0 m_Props pointer");
        int wizardPropertiesOffset = CheckedSceneOffset(
            wizardData,
            WizardPeakSceneBaseOffset,
            wizardSceneBytes,
            WizardPeakPropertiesSceneOffset,
            WizardPropertiesBytes,
            "Wizard Peak T6 properties");
        int toastyPropertiesOffset = CheckedSceneOffset(
            toastyData,
            ToastySceneBaseOffset,
            toastySceneBytes,
            ToastyPropertiesSceneOffset,
            WizardPropertiesBytes,
            "Toasty T0 properties destination");
        int pointerFixupBytes = checked(sizeof(uint) + (PointerFixupCountBefore * sizeof(uint)));
        int pointerFixupOffset = CheckedSceneOffset(
            toastyData,
            ToastySceneBaseOffset,
            toastySceneBytes,
            PointerFixupListSceneOffset,
            pointerFixupBytes,
            "Toasty scene pointer-fixup count/list");

        if (RangesOverlap(toastyPropertiesOffset, WizardPropertiesBytes, pointerFixupOffset, pointerFixupBytes) ||
            RangesOverlap(toastyT0PointerOffset, sizeof(uint), toastyPropertiesOffset, WizardPropertiesBytes) ||
            RangesOverlap(toastyT0PointerOffset, sizeof(uint), pointerFixupOffset, pointerFixupBytes))
        {
            throw new InvalidDataException("The guarded Toasty properties, T0 pointer, and scene fixup-list ranges unexpectedly overlap.");
        }

        ValidateWordAtOffset(
            toastyData,
            toastyT0PointerOffset,
            ImportedToastyT0PropertiesPointer,
            "v3 imported Toasty T0 m_Props pointer");
        ReadOnlySpan<byte> sourceProperties = wizardData.AsSpan(wizardPropertiesOffset, WizardPropertiesBytes);
        Span<byte> targetProperties = toastyData.AsSpan(toastyPropertiesOffset, WizardPropertiesBytes);
        ValidateSha256(sourceProperties, WizardPropertiesSha256, "Wizard Peak T6 native properties preimage");
        ValidateSha256(targetProperties, ToastyPropertiesPreimageSha256, "Toasty T0 native properties-space preimage");
        if (BinaryPrimitives.ReadUInt32LittleEndian(sourceProperties) != WizardSourceInternalPointer)
        {
            throw new InvalidDataException(
                $"Wizard Peak T6 properties expected internal pointer 0x{WizardSourceInternalPointer:X}, " +
                $"found 0x{BinaryPrimitives.ReadUInt32LittleEndian(sourceProperties):X}.");
        }

        Span<byte> pointerFixups = toastyData.AsSpan(pointerFixupOffset, pointerFixupBytes);
        ValidateSha256(pointerFixups, ToastyPointerFixupPreimageSha256, "Toasty scene pointer-fixup preimage");
        int fixupCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(pointerFixups));
        if (fixupCount != PointerFixupCountBefore)
            throw new InvalidDataException($"Toasty pointer-fixup count expected 0x{PointerFixupCountBefore:X}, found 0x{fixupCount:X}.");

        uint[] fixups = new uint[fixupCount];
        for (int i = 0; i < fixups.Length; i++)
            fixups[i] = BinaryPrimitives.ReadUInt32LittleEndian(pointerFixups.Slice(sizeof(uint) + (i * sizeof(uint)), sizeof(uint)));
        int removedIndex = Array.IndexOf(fixups, RemovedPointerFixup);
        if (removedIndex != 1 || fixups.Count(value => value == RemovedPointerFixup) != 1 ||
            fixups[0] != ToastyT0PropertiesPointerSceneOffset || fixups[2] != ToastyPropertiesSceneOffset)
        {
            throw new InvalidDataException(
                "Toasty's checked T0 properties fixup sequence changed; expected 0x629C, 0x9F28, 0x9EE8 with 0x9F28 occurring exactly once.");
        }

        sourceProperties.CopyTo(targetProperties);
        BinaryPrimitives.WriteUInt32LittleEndian(targetProperties, ToastyRebasedInternalPointer);
        for (int i = 0; i < TranslatedRoutePoints.Length; i++)
        {
            ToastyWizardRoutePoint point = TranslatedRoutePoints[i];
            int routePointOffset = checked(FirstRoutePointPropertiesOffset + (i * RoutePointStride));
            if (routePointOffset < 0 || routePointOffset > targetProperties.Length - (3 * sizeof(int)))
                throw new InvalidDataException($"Translated route point {i} exceeds the guarded 0x{WizardPropertiesBytes:X}-byte properties block.");
            BinaryPrimitives.WriteInt32LittleEndian(targetProperties.Slice(routePointOffset, sizeof(int)), point.X);
            BinaryPrimitives.WriteInt32LittleEndian(targetProperties.Slice(routePointOffset + sizeof(int), sizeof(int)), point.Y);
            BinaryPrimitives.WriteInt32LittleEndian(targetProperties.Slice(routePointOffset + (2 * sizeof(int)), sizeof(int)), point.Z);
        }
        WriteWordAtOffset(toastyData, toastyT0PointerOffset, RestoredToastyT0PropertiesPointer);

        BinaryPrimitives.WriteUInt32LittleEndian(pointerFixups, PointerFixupCountAfter);
        for (int i = removedIndex; i < fixups.Length - 1; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                pointerFixups.Slice(sizeof(uint) + (i * sizeof(uint)), sizeof(uint)),
                fixups[i + 1]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(
            pointerFixups.Slice(sizeof(uint) + ((fixups.Length - 1) * sizeof(uint)), sizeof(uint)),
            0);

        ValidateWordAtOffset(
            toastyData,
            toastyT0PointerOffset,
            RestoredToastyT0PropertiesPointer,
            "restored Toasty T0 m_Props pointer");
        ValidateSha256(targetProperties, ToastyPropertiesResultSha256, "Toasty Green Wizard properties result");
        ValidateSha256(pointerFixups, ToastyPointerFixupResultSha256, "Toasty pointer-fixup result");
    }

    private static int ValidateSceneLayout(
        byte[] data,
        int expectedSceneBase,
        int? expectedSceneBytes,
        string label)
    {
        if (data.Length < SceneSizeHeaderOffset + sizeof(uint))
            throw new InvalidDataException($"{label} data entry is too short to contain its scene header.");
        uint sceneBaseWord = ReadWordAtOffset(data, SceneBaseHeaderOffset);
        uint sceneBytesWord = ReadWordAtOffset(data, SceneSizeHeaderOffset);
        if (sceneBaseWord > int.MaxValue || sceneBytesWord > int.MaxValue)
            throw new InvalidDataException($"{label} scene header exceeds the supported signed offset range.");
        int sceneBase = (int)sceneBaseWord;
        int sceneBytes = (int)sceneBytesWord;
        if (sceneBase != expectedSceneBase)
        {
            throw new InvalidDataException(
                $"{label} scene base expected 0x{expectedSceneBase:X}, found 0x{sceneBase:X}.");
        }
        if (expectedSceneBytes is int expectedBytes && sceneBytes != expectedBytes)
        {
            throw new InvalidDataException(
                $"{label} scene size expected 0x{expectedBytes:X}, found 0x{sceneBytes:X}.");
        }
        if (sceneBytes <= 0 || sceneBase > data.Length || sceneBytes > data.Length - sceneBase)
        {
            throw new InvalidDataException(
                $"{label} scene range 0x{sceneBase:X}+0x{sceneBytes:X} exceeds data entry length 0x{data.Length:X}.");
        }
        return sceneBytes;
    }

    private static int CheckedSceneOffset(
        byte[] data,
        int sceneBase,
        int sceneBytes,
        int sceneOffset,
        int length,
        string label)
    {
        if (sceneBase < 0 || sceneBytes < 0 || sceneOffset < 0 || length < 0)
            throw new InvalidDataException($"{label} has a negative guarded range component.");
        if (sceneOffset > sceneBytes || length > sceneBytes - sceneOffset)
        {
            throw new InvalidDataException(
                $"{label} scene-relative range 0x{sceneOffset:X}+0x{length:X} exceeds scene size 0x{sceneBytes:X}.");
        }
        int offset = checked(sceneBase + sceneOffset);
        if (offset > data.Length || length > data.Length - offset)
        {
            throw new InvalidDataException(
                $"{label} range 0x{offset:X}+0x{length:X} exceeds data entry length 0x{data.Length:X}.");
        }
        return offset;
    }

    private static bool RangesOverlap(int firstOffset, int firstLength, int secondOffset, int secondLength) =>
        (long)firstOffset < (long)secondOffset + secondLength &&
        (long)secondOffset < (long)firstOffset + firstLength;

    private static byte[] BuildLightningTrailParticleDescriptor(byte[] wizardData)
    {
        byte[] descriptor = ReadCheckedRange(
            wizardData,
            LightningTrailParticleSourceDescriptorOffset,
            LightningTrailParticleDescriptorBytes,
            "Wizard Peak lightning-trail particle type-0x07 descriptor");
        ValidateSha256(
            descriptor,
            LightningTrailParticleSourceDescriptorSha256,
            "Wizard Peak lightning-trail particle type-0x07 descriptor");
        if (BinaryPrimitives.ReadUInt16LittleEndian(descriptor.AsSpan(0, 2)) != LightningTrailParticleType ||
            BinaryPrimitives.ReadUInt16LittleEndian(descriptor.AsSpan(2, 2)) != LightningTrailParticleDescriptorBytes - 4)
        {
            throw new InvalidDataException("Wizard Peak lightning-trail particle descriptor header is not the guarded type-0x07/8-byte record.");
        }

        descriptor[4] = 0x60; // u0: next free Toasty particle-atlas tile, word x856 on page x832
        descriptor[5] = 0xE0; // v0: target y480 on page y256
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(6, 2), 0x3463); // next free particle CLUT slot, x560/y209
        descriptor[8] = 0x7F; // u2: 32 texels through target word x863
        descriptor[9] = 0xFF; // v2: 32 texels through target y511
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(10, 2), 0x003D); // native particle page x832/y256, ABR=1
        ValidateSha256(
            descriptor,
            LightningTrailParticleInstalledDescriptorSha256,
            "rebased Toasty lightning-trail particle type-0x07 descriptor");
        return descriptor;
    }

    private static byte[] BuildDispatchShim(uint shimAddress, int delta, ReadOnlySpan<byte> lightningTrailParticleDescriptor)
    {
        if (lightningTrailParticleDescriptor.Length != LightningTrailParticleDescriptorBytes)
            throw new InvalidDataException($"Lightning-trail particle descriptor expected {LightningTrailParticleDescriptorBytes} bytes, found {lightningTrailParticleDescriptor.Length}.");

        uint green = checked(SourceGreenWizardHandlerAddress + (uint)delta);
        uint lightning = checked(SourceLightningHandlerAddress + (uint)delta);
        uint descriptorAddress = checked(shimAddress + LightningTrailParticleDescriptorShimOffset);
        (uint descriptorHi, uint descriptorLo) = EncodeAddressPair(8, descriptorAddress);
        (uint particleSlotHi, uint particleSlotStore) = EncodeAbsoluteStoreWord(8, 9, LightningTrailParticleTextureSlotAddress);
        List<uint> words =
        [
            0x2408011B,                         // addiu t0, zero, 0x011B
            EncodeBranch(4, 3, 8, shimAddress + 4, shimAddress + 0x34),
            0x00000000,
            0x24080026,                         // addiu t0, zero, 0x0026
            EncodeBranch(4, 3, 8, shimAddress + 0x10, shimAddress + 0x58),
            0x00000000,
            0x240200FB,                         // displaced Toasty dispatch
            EncodeBranch(5, 3, 2, shimAddress + 0x1C, shimAddress + 0x2C),
            0x286200FC,                         // slti v0, v1, 0xFC
            EncodeJump(2, ToastyFbHandlerAddress),
            0x00000000,
            EncodeJump(2, ToastyDispatchContinueAddress),
            0x00000000,
            0x03C04021,                         // green: addu t0, fp, zero
            0x02E0F021,                         // addu fp, s7, zero
            0x0100B821,                         // addu s7, t0, zero
            descriptorHi,                       // install type-0x07 sprite descriptor pointer
            descriptorLo,
            particleSlotHi,
            particleSlotStore,
            EncodeJump(2, green),
            0x00000000,
            0x03C04021,                         // lightning: swap fp/s7
            0x02E0F021,
            0x0100B821,
            descriptorHi,                       // keep direct actor-0x26 dispatch safe too
            descriptorLo,
            particleSlotHi,
            particleSlotStore,
            EncodeJump(2, lightning),
            0x00000000
        ];
        while (words.Count < 0x80 / 4)
            words.Add(0);
        words.Add(0x03C04021);                  // return: restore Toasty fp/s7
        words.Add(0x02E0F021);
        words.Add(0x0100B821);
        words.Add(EncodeJump(2, ToastyNextMobyAddress));
        words.Add(0);
        if (words.Count * 4 != LightningTrailParticleDescriptorShimOffset)
            throw new InvalidDataException($"Dispatch shim code expected descriptor offset 0x{LightningTrailParticleDescriptorShimOffset:X}, found 0x{words.Count * 4:X}.");
        byte[] bytes = new byte[(words.Count * 4) + lightningTrailParticleDescriptor.Length];
        for (int i = 0; i < words.Count; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4, 4), words[i]);
        lightningTrailParticleDescriptor.CopyTo(bytes.AsSpan(LightningTrailParticleDescriptorShimOffset));
        return bytes;
    }

    private static uint EncodeBranch(int opcode, int rs, int rt, uint instructionAddress, uint targetAddress)
    {
        long delta = (long)targetAddress - (instructionAddress + 4L);
        if ((delta & 3) != 0)
            throw new InvalidOperationException("Branch target is not instruction-aligned.");
        long displacement = delta / 4;
        if (displacement is < short.MinValue or > short.MaxValue)
            throw new InvalidOperationException("Branch target is out of range.");
        return ((uint)opcode << 26) | ((uint)rs << 21) | ((uint)rt << 16) | (ushort)(short)displacement;
    }

    private static uint EncodeJump(int opcode, uint address)
    {
        if ((address & 3) != 0)
            throw new InvalidOperationException($"Jump target 0x{address:X8} is not instruction-aligned.");
        return ((uint)opcode << 26) | ((address >> 2) & 0x03FFFFFF);
    }

    private static void ValidateWord(byte[] overlay, uint address, uint expected, string label)
    {
        int offset = checked((int)(address - OverlayLoadAddress));
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(offset, 4));
        if (actual != expected)
            throw new InvalidDataException($"{label} expected 0x{expected:X8} at 0x{address:X8}, found 0x{actual:X8}.");
    }

    private static void ValidateRelocatedAddressPair(byte[] overlay, uint highAddress, uint lowAddress, uint expectedTarget, string label)
    {
        int highOffset = checked((int)(highAddress - OverlayLoadAddress));
        int lowOffset = checked((int)(lowAddress - OverlayLoadAddress));
        uint highWord = ReadWordAtOffset(overlay, highOffset);
        uint lowWord = ReadWordAtOffset(overlay, lowOffset);
        if ((highWord >> 26) != 0x0F)
            throw new InvalidDataException($"{label} expected LUI at 0x{highAddress:X8}.");
        int register = (int)((highWord >> 16) & 0x1F);
        uint high = (highWord & 0xFFFF) << 16;
        if (!TryDecodeAddressLow(lowWord, register, high, out uint target, out _) || target != expectedTarget)
        {
            throw new InvalidDataException(
                $"{label} expected relocated target 0x{expectedTarget:X8} at 0x{highAddress:X8}/0x{lowAddress:X8}, found 0x{target:X8}.");
        }
    }

    private static string ComputeSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void ValidateSha256(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        string actual = ComputeSha256(bytes);
        if (actual != expected)
            throw new InvalidDataException($"{label} expected SHA-256 {expected}, found {actual}.");
    }

    private static void WriteWord(byte[] overlay, uint address, uint value)
    {
        int offset = checked((int)(address - OverlayLoadAddress));
        BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(offset, 4), value);
    }

    private static (uint HiWord, uint LoWord) EncodeAddressPair(int register, uint address)
    {
        uint high = (address + 0x8000) >> 16;
        uint low = address & 0xFFFF;
        uint hiWord = (0x0Fu << 26) | ((uint)register << 16) | high;
        uint loWord = (0x09u << 26) | ((uint)register << 21) | ((uint)register << 16) | low;
        return (hiWord, loWord);
    }

    private static (uint HiWord, uint StoreWord) EncodeAbsoluteStoreWord(int valueRegister, int baseRegister, uint address)
    {
        uint high = (address + 0x8000) >> 16;
        uint low = address & 0xFFFF;
        uint hiWord = (0x0Fu << 26) | ((uint)baseRegister << 16) | high;
        uint storeWord = (0x2Bu << 26) | ((uint)baseRegister << 21) | ((uint)valueRegister << 16) | low;
        return (hiWord, storeWord);
    }

    private static uint ReadWordAtOffset(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static void WriteWordAtOffset(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);

    private static void ValidateWordAtOffset(byte[] bytes, int offset, uint expected, string label)
    {
        uint actual = ReadWordAtOffset(bytes, offset);
        if (actual != expected)
            throw new InvalidDataException($"{label} expected 0x{expected:X8} at file offset 0x{offset:X}, found 0x{actual:X8}.");
    }

    private static WadLayout LoadWadLayout(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        int wadLba = JsonValue.GetInt32(root.GetProperty("wad"), "lba", 37);
        int wadSize = JsonValue.GetInt32(root.GetProperty("wad"), "size", 0);
        WadEntry[] entries = root.GetProperty("entries").EnumerateArray()
            .Select(item => new WadEntry(JsonValue.GetInt32(item, "index", -1), JsonValue.GetInt64(item, "offset", -1), JsonValue.GetInt32(item, "size", -1)))
            .Where(entry => entry.Index >= 0 && entry.Offset >= SectorBytes && entry.Size > 0)
            .OrderBy(entry => entry.Offset).ToArray();
        if (entries.Length == 0 || entries[^1].Offset + entries[^1].Size != wadSize)
            throw new InvalidDataException("WAD analysis does not cover the complete archive.");
        return new WadLayout(wadLba, wadSize, entries, entries.ToDictionary(entry => entry.Index));
    }

    private static List<IsoRootRecord> ReadRootFiles(FileStream image, DiscLayout layout)
    {
        byte[] directory = DiscImage.ReadFileBytes(image, layout, layout.RootExtent, 0, layout.RootLength);
        List<IsoRootRecord> result = [];
        for (int offset = 0; offset < directory.Length;)
        {
            int length = directory[offset];
            if (length == 0) { offset = ((offset / SectorBytes) + 1) * SectorBytes; continue; }
            if (length < 34 || offset + length > directory.Length) break;
            int nameLength = directory[offset + 32];
            string name = Encoding.ASCII.GetString(directory, offset + 33, nameLength).Replace(";1", "", StringComparison.OrdinalIgnoreCase);
            if (name is not "\0" and not "\u0001")
                result.Add(new IsoRootRecord(name, checked((int)ReadUInt32(directory, offset + 2)), checked((int)ReadUInt32(directory, offset + 10))));
            offset += length;
        }
        return result;
    }

    private static void PatchRootRecord(byte[] directory, string name, int lba, int size)
    {
        for (int offset = 0; offset < directory.Length;)
        {
            int length = directory[offset];
            if (length == 0) { offset = ((offset / SectorBytes) + 1) * SectorBytes; continue; }
            if (length < 34 || offset + length > directory.Length) break;
            int nameLength = directory[offset + 32];
            string current = Encoding.ASCII.GetString(directory, offset + 33, nameLength).Replace(";1", "", StringComparison.OrdinalIgnoreCase);
            if (current.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                WriteBothEndianUInt32(directory, offset + 2, checked((uint)lba));
                WriteBothEndianUInt32(directory, offset + 10, checked((uint)size));
                return;
            }
            offset += length;
        }
        throw new InvalidDataException($"ISO root record {name} was not found.");
    }

    private static bool IsExecutableName(string name) => name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase);
    private static int AlignSector(int value) => checked(((value + SectorBytes - 1) / SectorBytes) * SectorBytes);
    private static int DivideRoundUp(int value, int divisor) => (value + divisor - 1) / divisor;
    private static uint ReadUInt32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static void WriteUInt32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void WriteBothEndianUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 4, 4), value);
    }

    private sealed record WadEntry(int Index, long Offset, int Size);
    private sealed record WadLayout(int WadLba, int WadSize, IReadOnlyList<WadEntry> Entries, IReadOnlyDictionary<int, WadEntry> EntriesByIndex);
    private sealed record IsoRootRecord(string Name, int Lba, int Size);
    private sealed record RelocationResult(byte[] Bytes, int Jumps, int Branches, int Pointers, int ExcludedTailPointers, int HiLoPairs);
    private sealed record ParticleBundle(byte[] Bytes, uint SpawnType7Address, uint SpawnType41Address, uint UpdateType7Address, uint UpdateType41Address);
}
