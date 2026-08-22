using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

const string ExpectedCleanImageSha256 =
    "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
const string ExpectedLockedBaseImageSha256 =
    "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
const string ExpectedV4TerrainImageSha256 =
    "6f63a7645c0ed07ad21cc884856c52a5df598f5fa81c31441d342f44d5e16f0c";
const int WadLba = 37;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string catalogPath = Path.Combine(repositoryRoot, "spyro-level-catalog.json");
string cleanImagePath = Path.Combine(repositoryRoot, "Spyro the Dragon (USA).bin");
string cleanCuePath = Path.Combine(repositoryRoot, "Spyro the Dragon (USA).cue");
string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    $"Spyro.Editor.UnusedLevel65BlankLevelLabSmoke-{Guid.NewGuid():N}");
string workspaceContainer = Path.Combine(smokeRoot, "workspace-container");

Require(File.Exists(catalogPath), $"Missing retail catalog: {catalogPath}");
Require(File.Exists(cleanImagePath), $"Missing exact clean USA source BIN: {cleanImagePath}");
Require(File.Exists(cleanCuePath), $"Missing exact clean USA source CUE: {cleanCuePath}");

string catalogHashBefore = await HashFileAsync(catalogPath);
string cleanHashBefore = await HashFileAsync(cleanImagePath);
Require(cleanHashBefore == ExpectedCleanImageSha256, "The local source BIN is not the exact clean USA fixture.");
Directory.CreateDirectory(smokeRoot);

try
{
    VerifyProfileAndCatalog(repositoryRoot, catalogHashBefore);
    VerifyPathsManifestAndGuards(workspaceContainer);
    await VerifyInvalidSourceRejectionAsync(smokeRoot);

    UnusedLevel65BlankLevelLabBootstrapRequest bootstrapRequest = new(
        cleanImagePath,
        cleanCuePath,
        workspaceContainer);
    UnusedLevel65BlankLevelLabBootstrapResult first =
        await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(bootstrapRequest);
    await VerifyFirstBootstrapAsync(first, cleanHashBefore, catalogHashBefore, catalogPath);
    await VerifyFullAuthoringConstructionContractAsync(first.Paths);
    await VerifyBootstrapStaleOperationRecoveryAsync(first.Paths);

    byte[] manifestBytesBeforeReuse = await File.ReadAllBytesAsync(first.Paths.ManifestPath);
    CreateOwnedStaleOperation(first.Paths.OperationsDirectoryPath, "bootstrap-");
    UnusedLevel65BlankLevelLabBootstrapResult reused =
        await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(bootstrapRequest);
    await VerifyDeterministicReuseAsync(first, reused, manifestBytesBeforeReuse);
    await VerifyBootstrapIncompleteRollbackRestartRecoveryAsync(
        bootstrapRequest,
        first.Paths);

    await VerifyGenericTerrainV4ParityAsync(first.Paths, smokeRoot);

    Require(
        await HashFileAsync(cleanImagePath) == cleanHashBefore,
        "The exact clean source BIN changed during the smoke.");
    Require(
        await HashFileAsync(catalogPath) == catalogHashBefore,
        "The retail level catalog changed during the smoke.");
    Console.WriteLine(
        "PASS UnusedLevel65BlankLevelLabSmoke: exact 35+1 catalog, source-bound 66-record native texture cache with tamper refusal and atomic rollback, component-aware HP+LP allocation with native-ordered collision repack, full exposure/topmost proof, and occlusion ownership, immutable receipt provenance, journaled restart recovery, atomic replacement rollback, platform-aware handoff, and generic v4 parity passed.");
}
finally
{
    if (Directory.Exists(smokeRoot) &&
        Path.GetFileName(smokeRoot).StartsWith(
            "Spyro.Editor.UnusedLevel65BlankLevelLabSmoke-",
            StringComparison.Ordinal))
    {
        Directory.Delete(smokeRoot, recursive: true);
    }

    Require(!Directory.Exists(smokeRoot), "The smoke retained temporary game data.");
}

void VerifyProfileAndCatalog(string root, string catalogHash)
{
    UnusedLevel65BlankLevelLabProfile profile = UnusedLevel65BlankLevelLabProfileRegistry.Profile;
    Require(profile.Key == "unusedlevel65blank", "Lab key drifted.");
    Require(profile.ProfileVersion == 1, "Lab profile version drifted.");
    Require(profile.LevelId == 65 && profile.DataWadEntry == 80, "Lab level/data identity drifted.");
    Require(
        profile.ObjectTableWadOffset == 0x6B06970 &&
        profile.ObjectTableRelativeOffset == 0x1D0170 &&
        profile.ObjectRecordCount == 107,
        "Lab object-table binding drifted.");
    Require(profile.CleanUsaImageSha256 == ExpectedCleanImageSha256, "Clean source binding drifted.");
    Require(profile.LockedBaseImageSha256 == ExpectedLockedBaseImageSha256, "Locked-base binding drifted.");
    Require(!profile.PromotionAuthorized && !profile.NormalCreateBinEnabled, "Lab promotion/Create BIN guard opened.");

    UnusedLevel65BlankLevelLabCapabilities capabilities = profile.Capabilities;
    Require(
        capabilities.RuntimeBaseBootstrap == UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly &&
        capabilities.ExistingHpZTerrainEdits == UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly &&
        capabilities.TerrainTextureEdits == UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly &&
        capabilities.ObjectTableInspection == UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly,
        "A supported lab capability lost its explicit research-only state.");
    Require(
        capabilities.StructuralTerrainGrowth == UnusedLevel65BlankLevelLabCapabilityState.Unavailable &&
        capabilities.LowDetailTerrainAuthoring == UnusedLevel65BlankLevelLabCapabilityState.Unavailable &&
        capabilities.ObjectMutation == UnusedLevel65BlankLevelLabCapabilityState.Unavailable &&
        capabilities.CrossLevelObjectImport == UnusedLevel65BlankLevelLabCapabilityState.Unavailable &&
        capabilities.PortalExitRouting == UnusedLevel65BlankLevelLabCapabilityState.Unavailable &&
        capabilities.SaveOwnership == UnusedLevel65BlankLevelLabCapabilityState.Unavailable &&
        capabilities.NormalCreateBin == UnusedLevel65BlankLevelLabCapabilityState.Unavailable &&
        capabilities.ReleasePromotion == UnusedLevel65BlankLevelLabCapabilityState.Unavailable,
        "An unavailable lab capability was accidentally opened.");

    VerifyEvidenceBinding(root, profile.PhysicalCloneEvidence);
    VerifyEvidenceBinding(root, profile.LockedBaseEvidence);

    LevelCatalog retail = LevelCatalog.Load(root);
    Require(retail.Levels.Count == 35, "The on-disk catalog is not the exact 35-level retail catalog.");
    Require(retail.FindByKey("unusedlevel65blank") == null, "The research level leaked into the retail catalog.");
    LevelDefinition[] retailRowsBefore = retail.Levels.ToArray();

    LevelCatalog augmented = UnusedLevel65BlankLevelLabProfileRegistry.AugmentCatalog(retail);
    Require(retail.Levels.Count == 35, "Catalog augmentation mutated the retail catalog instance.");
    Require(retail.Levels.SequenceEqual(retailRowsBefore), "Catalog augmentation changed a retail row.");
    Require(augmented.Levels.Count == 36, "Catalog augmentation did not produce exactly 35 retail + 1 research row.");
    Require(
        augmented.Levels.Take(35).SequenceEqual(retailRowsBefore) &&
        augmented.Levels.Count(UnusedLevel65BlankLevelLabProfileRegistry.IsLabLevel) == 1,
        "Catalog augmentation changed retail ordering or duplicated the lab row.");
    LevelDefinition lab = augmented.FindByKey("unusedlevel65blank")
        ?? throw new InvalidDataException("The in-memory lab definition cannot be resolved.");
    Require(UnusedLevel65BlankLevelLabProfileRegistry.IsLabLevel(lab), "The augmented lab row is not exact.");
    Require(UnusedLevel65BlankLevelLabProfileRegistry.Resolve(lab) == profile, "The lab resolver returned a different profile.");
    Require(
        UnusedLevel65BlankLevelLabProfileRegistry.TryResolve(lab, out UnusedLevel65BlankLevelLabProfile? resolved) &&
        resolved == profile,
        "The lab TryResolve helper failed.");
    Require(
        ReferenceEquals(UnusedLevel65BlankLevelLabProfileRegistry.AugmentCatalog(augmented), augmented),
        "Idempotent in-memory catalog augmentation replaced the exact augmented catalog.");
    Require(HashFile(catalogPath) == catalogHash, "In-memory augmentation wrote the retail catalog.");
}

void VerifyEvidenceBinding(string root, UnusedLevel65BlankLevelLabEvidenceBinding evidence)
{
    string evidencePath = Path.Combine(
        root,
        evidence.EvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
    Require(File.Exists(evidencePath), $"Missing bound runtime evidence: {evidence.EvidenceRelativePath}");
    Require(HashFile(evidencePath) == evidence.EvidenceSha256, $"Evidence hash drifted: {evidence.EvidenceRelativePath}");
    Require(!evidence.PromotionAuthorized, $"Evidence was unexpectedly promoted: {evidence.EvidenceRelativePath}");
}

void VerifyPathsManifestAndGuards(string container)
{
    UnusedLevel65BlankLevelLabWorkspacePaths paths =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(container);
    string expectedRoot = Path.Combine(container, "_research", "unusedlevel65blank", "v1");
    Require(PathsEqual(paths.RootPath, expectedRoot), "Versioned lab root drifted.");
    Require(PathsEqual(paths.ManifestPath, Path.Combine(expectedRoot, "lab-manifest.json")), "Manifest path drifted.");
    Require(PathsEqual(paths.LockedBaseDirectoryPath, Path.Combine(expectedRoot, "locked-base")), "Locked-base directory drifted.");
    Require(
        PathsEqual(paths.LockedBaseImagePath, Path.Combine(expectedRoot, "locked-base", "unusedlevel65blank-locked-base-v1.bin")) &&
        PathsEqual(paths.LockedBaseCuePath, Path.Combine(expectedRoot, "locked-base", "unusedlevel65blank-locked-base-v1.cue")),
        "Locked BIN/CUE path drifted.");
    Require(PathsEqual(paths.AuthoredEditsDirectoryPath, Path.Combine(expectedRoot, "authored-edits")), "Authored-edits path drifted.");
    Require(PathsEqual(paths.OperationsDirectoryPath, Path.Combine(expectedRoot, ".bootstrap-operations")), "Operation path drifted.");

    UnusedLevel65BlankLevelLabManifest manifest = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
    UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(manifest, paths);
    Require(
        manifest.LockedBaseImageRelativePath == "locked-base/unusedlevel65blank-locked-base-v1.bin" &&
        manifest.LockedBaseCueRelativePath == "locked-base/unusedlevel65blank-locked-base-v1.cue" &&
        manifest.AuthoredEditsRelativePath == "authored-edits",
        "Manifest paths are not isolated relative paths.");
    Require(
        manifest.ExternalGameDataRequired &&
        !manifest.GameDataEmbedded &&
        !manifest.PromotionAuthorized &&
        !manifest.NormalCreateBinEnabled,
        "Manifest safety flags drifted.");

    ExpectInvalidData(
        () => UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(manifest with { PromotionAuthorized = true }, paths),
        "promotion manifest tamper");
    ExpectInvalidData(
        () => UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(manifest with { NormalCreateBinEnabled = true }, paths),
        "Create BIN manifest tamper");
    ExpectInvalidData(
        () => UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(manifest with { LockedBaseImageSha256 = new string('0', 64) }, paths),
        "locked hash manifest tamper");
    ExpectInvalidData(
        () => UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(manifest with { LockedBaseImageRelativePath = "../escape.bin" }, paths),
        "manifest path escape");
    ExpectInvalidData(
        () => UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
            manifest with
            {
                Capabilities = manifest.Capabilities with
                {
                    NormalCreateBin = UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly
                }
            },
            paths),
        "capability manifest tamper");
    ExpectInvalidData(
        () => UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(
            paths with { ManifestPath = Path.Combine(container, "escaped-manifest.json") }),
        "workspace path divergence");
}

async Task VerifyInvalidSourceRejectionAsync(string root)
{
    string invalidRoot = Path.Combine(root, "invalid-source");
    Directory.CreateDirectory(invalidRoot);
    string invalidImage = Path.Combine(invalidRoot, "invalid.bin");
    string invalidCue = Path.Combine(invalidRoot, "invalid.cue");
    await File.WriteAllBytesAsync(invalidImage, Encoding.ASCII.GetBytes("not-a-clean-spyro-image"));
    await File.WriteAllTextAsync(
        invalidCue,
        "FILE \"invalid.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n",
        Encoding.ASCII);
    string invalidWorkspace = Path.Combine(root, "invalid-workspace-container");
    bool rejected = false;
    try
    {
        await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(new(
            invalidImage,
            invalidCue,
            invalidWorkspace));
    }
    catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or EndOfStreamException)
    {
        rejected = true;
    }

    Require(rejected, "A non-clean source image was accepted by the lab bootstrap.");
    UnusedLevel65BlankLevelLabWorkspacePaths invalidPaths =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(invalidWorkspace);
    Require(
        !File.Exists(invalidPaths.ManifestPath) &&
        !File.Exists(invalidPaths.LockedBaseImagePath) &&
        !File.Exists(invalidPaths.LockedBaseCuePath) &&
        !Directory.Exists(invalidPaths.OperationsDirectoryPath),
        "Invalid-source rejection left a published lab or operation debris.");
}

async Task VerifyFirstBootstrapAsync(
    UnusedLevel65BlankLevelLabBootstrapResult result,
    string sourceHashBefore,
    string catalogHashBeforeValue,
    string retailCatalogPath)
{
    Require(!result.ReusedExistingLockedBase, "First bootstrap unexpectedly reported reuse.");
    Require(result.SourceImagePreserved, "First bootstrap did not prove source preservation.");
    Require(result.AtomicPublicationCompleted, "First bootstrap did not complete atomic publication.");
    Require(result.OwnedTemporaryIntermediatesRemoved, "First bootstrap did not remove owned intermediates.");
    Require(result.SourceImageSha256 == sourceHashBefore, "First bootstrap source binding drifted.");
    Require(
        result.PhysicalCloneImageSha256 == UnusedLevel65BlankLevelLabProfileRegistry.Profile.PhysicalCloneImageSha256,
        "Physical-clone stage hash drifted.");
    Require(result.LockedBaseImageSha256 == ExpectedLockedBaseImageSha256, "Locked-base result hash drifted.");
    Require(File.Exists(result.Paths.ManifestPath), "Published lab manifest is missing.");
    Require(File.Exists(result.Paths.LockedBaseImagePath), "Published locked BIN is missing.");
    Require(File.Exists(result.Paths.LockedBaseCuePath), "Published locked CUE is missing.");
    Require(Directory.Exists(result.Paths.AuthoredEditsDirectoryPath), "Published authored-edits directory is missing.");
    Require(!Directory.Exists(result.Paths.OperationsDirectoryPath), "Bootstrap operation debris remains.");
    Require(await HashFileAsync(result.Paths.LockedBaseImagePath) == ExpectedLockedBaseImageSha256, "Published locked BIN hash drifted.");
    ValidateCuePair(result.Paths.LockedBaseCuePath, result.Paths.LockedBaseImagePath);
    UnusedLevel65BlankLevelLabManifest validated =
        await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceAsync(result.Paths);
    Require(validated == result.Manifest, "Published manifest readback differs from the bootstrap result.");

    string manifestJson = await File.ReadAllTextAsync(result.Paths.ManifestPath);
    using JsonDocument document = JsonDocument.Parse(manifestJson);
    Require(!document.RootElement.GetProperty("promotionAuthorized").GetBoolean(), "Serialized manifest promoted the lab.");
    Require(!document.RootElement.GetProperty("normalCreateBinEnabled").GetBoolean(), "Serialized manifest enabled normal Create BIN.");
    Require(!document.RootElement.GetProperty("gameDataEmbedded").GetBoolean(), "Serialized manifest claims embedded game data.");

    string[] files = Directory.GetFiles(result.Paths.RootPath, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(result.Paths.RootPath, path).Replace(Path.DirectorySeparatorChar, '/'))
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(
        files.SequenceEqual(new[]
        {
            "lab-manifest.json",
            "locked-base/unusedlevel65blank-locked-base-v1.bin",
            "locked-base/unusedlevel65blank-locked-base-v1.cue"
        }),
        $"First bootstrap retained unexpected files: {string.Join(", ", files)}");
    Require(await HashFileAsync(cleanImagePath) == sourceHashBefore, "First bootstrap changed the clean source BIN.");
    Require(await HashFileAsync(retailCatalogPath) == catalogHashBeforeValue, "First bootstrap changed the retail catalog.");
}

async Task VerifyFullAuthoringConstructionContractAsync(
    UnusedLevel65BlankLevelLabWorkspacePaths paths)
{
    string[] artifactPathsBefore = SnapshotArtifactPaths(paths.RootPath);
    string baselineHashBefore = await HashFileAsync(paths.LockedBaseImagePath);
    UnusedLevel65FullAuthoringConstructionContract contract =
        await UnusedLevel65FullAuthoringConstructionTemplate.InspectAsync(paths.LockedBaseImagePath);
    Require(
        contract.ProfileId == "unused-level-65-full-authoring-construction-template-clean-usa-research-v1" &&
        contract.BaselineProfileId == UnusedLevel65BlankLevelLabProfileRegistry.ProfileId &&
        contract.BaselineImageSha256 == ExpectedLockedBaseImageSha256,
        "The full-authoring construction profile or V5 baseline binding drifted.");
    Require(
        contract.WadLba == 37 &&
        contract.WadByteLength == 0x6C18800 &&
        contract.DataEntryWadOffset == 0x6936800 &&
        contract.DataEntryByteLength == 0x2E2000 &&
        contract.ModelSubfileWadOffset == 0x6A15000 &&
        contract.ModelSubfileByteLength == 0x94800,
        "The full-authoring row-80/model-subfile storage contract drifted.");
    Require(
        contract.NativeTextureRecordCount == 66 &&
        contract.SceneSectorCount == 216 &&
        contract.LowDetailVertexCount == 2_853 &&
        contract.LowDetailFaceCount == 1_437 &&
        contract.HighDetailVertexCount == 5_863 &&
        contract.HighDetailFaceCount == 3_887 &&
        contract.SpecialSurfaceCount == 3 &&
        contract.CollisionTriangleCount == 19_808 &&
        contract.PortalCount == 0 &&
        contract.ObjectTableWadOffset == 0x6B06970 &&
        contract.ObjectRecordCount == 107,
        "The full-authoring native content census drifted.");
    Require(
        contract.Landing.WadOffset == 0x6B06800 &&
        contract.Landing.ByteLength == 0x10 &&
        contract.Landing.Hex == "29E901009A8801006621000000004000" &&
        contract.Landing.Sha256 == "ac6446f7f11382b1f5d9c4f73c17281cda8db6251b14ace4e237c3c3d9f1896e" &&
        contract.Landing.RawX == 125_225 &&
        contract.Landing.RawY == 100_506 &&
        contract.Landing.RawZ == 8_550 &&
        contract.Landing.YawByte == 64,
        "The exact ID65 fly-in landing bytes, hash, coordinates, or heading drifted.");
    Require(
        contract.PlayerAnchor.TrueIndex == 92 &&
        contract.PlayerAnchor.WadOffset == 0x6B08910 &&
        contract.PlayerAnchor.ByteLength == 0x58 &&
        contract.PlayerAnchor.Sha256 == "4987c539f3178c555da26e4ec2f6cdc25094a27382b9465c9845846a39679bb2" &&
        contract.PlayerAnchor.RawX == 125_225 &&
        contract.PlayerAnchor.RawY == 100_506 &&
        contract.PlayerAnchor.RawZ == 8_704 &&
        contract.PlayerAnchor.RawX == contract.Landing.RawX &&
        contract.PlayerAnchor.RawY == contract.Landing.RawY &&
        contract.PlayerAnchor.RawZ - contract.Landing.RawZ == 154,
        "The exact ID65 T92 player anchor or its landing-relative spatial invariant drifted.");
    Require(
        contract.DisplayName.ExecutableName == "SCUS_942.28" &&
        contract.DisplayName.ExecutableLba == 55_382 &&
        contract.DisplayName.ExecutableByteLength == 0x66000 &&
        contract.DisplayName.PointerTableFileOffset == 0x5FFF0 &&
        contract.DisplayName.SlotIndex == 35 &&
        contract.DisplayName.PointerFileOffset == 0x6007C &&
        contract.DisplayName.PointerHex == "E4010180" &&
        contract.DisplayName.ResolvedStringFileOffset == 0x9E4 &&
        contract.DisplayName.ResolvedString == "TOWN SQUARE",
        "The exact ID65 slot-35 Town Square display-name binding drifted.");
    Require(
        contract.UsedModelEndWadOffset == 0x6AA9508 &&
        contract.VerifiedZeroTailBytes == 0x2F8 &&
        contract.SceneSectors.All(sector => sector.GapBytesAfter == 0),
        "The full-authoring component chain or verified model tail drifted.");
    Require(
        contract.Components.Single(component => component.Name == "cyclorama").Sha256 ==
            "8e8c62273ab0d4ea691a40409d5e4be77fe578cfebcfb2e3c6bb374cad7d40bb",
        "The exact ID65 cyclorama component hash drifted.");
    Require(
        contract.DataSubfiles.Select(subfile =>
                (subfile.Index, subfile.RelativeOffset, subfile.WadOffset, subfile.ByteLength, subfile.Sha256))
            .SequenceEqual(new[]
            {
                (0, 0x000800, 0x6937000L, 0x0DE000, "5fb81c4ac63eb235a172c4f16f83ef08efb148a6f543ef6100ba21359ade408f"),
                (1, 0x0DE800, 0x6A15000L, 0x094800, "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47"),
                (2, 0x173000, 0x6AA9800L, 0x05D000, "a37b7a8e5e5e660befed91e63c699a6c6d4b25ff8406f68b08e418c00d2a711d"),
                (3, 0x1D0000, 0x6B06800L, 0x008800, "63b5e699b3f175c0289a795c4f6f36786b9bc33ce608e1f1e9bd7bb4c04f09f6"),
                (4, 0x1D8800, 0x6B0F000L, 0x049800, "bdf27988f3d2b0fd4c4b905636b6980aff49ab1475d85edb4ce8aa2cc469351f"),
                (5, 0x222000, 0x6B58800L, 0x042800, "23968146e46b3263997e16692a192bbdb049966b010673d17611f2ab6903cef0"),
                (6, 0x264800, 0x6B9B000L, 0x05D000, "402bb40e21bf84922cbc35312b0e49b95292a2f96326636aba05685443686991"),
                (7, 0x2C1800, 0x6BF8000L, 0x020800, "eabadb4ea663043665bc4ba2d2e00c1c7c104688906b56bac2d7aba7c6cae8da")
            }) &&
        contract.DataSubfiles.Where(subfile => subfile.Index != 1)
            .All(subfile => subfile.MustRemainByteIdenticalInFirstTerrainGate) &&
        !contract.DataSubfiles.Single(subfile => subfile.Index == 1).MustRemainByteIdenticalInFirstTerrainGate,
        "The exact eight-subfile row-80 preservation contract drifted.");
    Require(
        contract.RequiresSpawnSafeHighAndLowDetailFoundation &&
        contract.RequiresCompleteCollisionAndOcclusionOwnership &&
        !contract.PromotionAuthorized &&
        !contract.NormalCreateBinEnabled,
        "The construction-template safety boundary was weakened.");
    Require(
        contract.Components.Single(component => component.Name == "environment").Policy ==
            UnusedLevel65ConstructionComponentPolicy.RebuildAtomically &&
        contract.Components.Single(component => component.Name == "collision").Policy ==
            UnusedLevel65ConstructionComponentPolicy.RebuildAtomically &&
        contract.Components.Single(component => component.Name == "texture").Policy ==
            UnusedLevel65ConstructionComponentPolicy.PreserveContainer,
        "The full-authoring component ownership policy drifted.");

    UnusedLevel65SceneSectorCapacityPlan plan =
        await UnusedLevel65FullAuthoringConstructionTemplate.BuildSectorCapacityPlanAsync(
            paths.LockedBaseImagePath,
            targetSectorIndex: 213,
            reserveByteCount: 28);
    UnusedLevel65SceneSectorCapacityPlan repeated =
        await UnusedLevel65FullAuthoringConstructionTemplate.BuildSectorCapacityPlanAsync(
            paths.LockedBaseImagePath,
            targetSectorIndex: 213,
            reserveByteCount: 28);
    Require(
        plan.TargetSectorIndex == 213 &&
        plan.ReservedByteCount == 28 &&
        plan.ReservationWadOffset == 0x6A3F9CC &&
        plan.OldEnvironmentByteLength == 0x284A4 &&
        plan.NewEnvironmentByteLength == 0x284C0 &&
        plan.OldUsedModelEndWadOffset == 0x6AA9508 &&
        plan.NewUsedModelEndWadOffset == 0x6AA9524 &&
        plan.ZeroTailBytesBefore == 0x2F8 &&
        plan.ZeroTailBytesAfter == 0x2DC,
        "The first 28-byte component-aware terrain-capacity plan drifted.");
    Require(
        plan.SceneSectors[213].WadOffset == 0x6A3E8B4 &&
        plan.SceneSectors[213].ByteLength == 0x1118 &&
        plan.SceneSectors[213].GapBytesAfter == 28 &&
        plan.SceneSectors[214].WadOffset == 0x6A3F9E8 &&
        plan.SceneSectors[215].WadOffset == 0x6A3FF50,
        "The sector-213 capacity plan did not preserve/relocate the exact native scene pointers.");
    Require(
        plan.RelocatedComponents.All(component => component.NewWadOffset == component.OldWadOffset + 28) &&
        plan.RelocatedComponents.Single(component => component.Name == "occlusion").NewWadOffset == 0x6A40438 &&
        plan.RelocatedComponents.Single(component => component.Name == "collision").NewWadOffset == 0x6A40DDC &&
        plan.RelocatedComponents.Single(component => component.Name == "sound").NewWadOffset == 0x6AA8E2C,
        "Following ID65 components did not relocate together by exactly 28 bytes.");
    Require(
        plan.ComponentContentsPreserved &&
        !plan.RetailDataTouched &&
        !plan.Publishable &&
        !plan.PromotionAuthorized &&
        !plan.NormalCreateBinEnabled,
        "The structural-capacity primitive crossed its non-publishable safety boundary.");
    Require(
        plan.BeforeModelSha256 == repeated.BeforeModelSha256 &&
        plan.AfterModelSha256 == repeated.AfterModelSha256 &&
        plan.AfterModelBytes.AsSpan().SequenceEqual(repeated.AfterModelBytes),
        "The full-authoring component-aware capacity plan is not deterministic.");

    UnusedLevel65ConstructionVisualTerrainPlan visual =
        await UnusedLevel65FullAuthoringConstructionTemplate.BuildFirstVisualTerrainPlanAsync(
            paths.LockedBaseImagePath);
    UnusedLevel65ConstructionVisualTerrainPlan visualRepeated =
        await UnusedLevel65FullAuthoringConstructionTemplate.BuildFirstVisualTerrainPlanAsync(
            paths.LockedBaseImagePath);
    Require(
        visual.SectorIndex == 213 &&
        visual.AddedLowDetailVertexCount == 3 &&
        visual.AddedLowDetailFaceCount == 1 &&
        visual.AddedHighDetailVertexCount == 3 &&
        visual.AddedHighDetailFaceCount == 1 &&
        visual.SectorWadOffset == 0x6A3E8B4 &&
        visual.OldSectorByteLength == 0x1118 &&
        visual.NewSectorByteLength == 0x1148,
        "The first independent HP+LP construction tile allocation drifted.");
    Require(
        visual.Points.SequenceEqual(new[]
        {
            new UnusedLevel65ConstructionPoint(7762, 6346, 512),
            new UnusedLevel65ConstructionPoint(7890, 6346, 512),
            new UnusedLevel65ConstructionPoint(7826, 6474, 640)
        }) &&
        visual.OldOcclusionWadOffset == 0x6A4041C &&
        visual.NewOcclusionWadOffset == 0x6A4044C &&
        visual.OldCollisionWadOffset == 0x6A40DC0 &&
        visual.NewCollisionWadOffset == 0x6A40DF0 &&
        visual.ZeroTailBytesAfter == 0x2C8,
        "The first construction tile geometry or following-component relocation drifted.");
    Require(
        visual.HighAndLowDetailAllocated &&
        visual.SectorCullBoundsPreserved &&
        visual.OcclusionContainerPreserved &&
        !visual.CollisionComposed &&
        !visual.Publishable &&
        !visual.PromotionAuthorized &&
        !visual.NormalCreateBinEnabled,
        "The visual-only construction tile crossed its collision/publication safety gate.");
    Require(
        visual.BeforeModelSha256 == visualRepeated.BeforeModelSha256 &&
        visual.AfterModelSha256 == visualRepeated.AfterModelSha256 &&
        visual.AfterModelBytes.AsSpan().SequenceEqual(visualRepeated.AfterModelBytes),
        "The first independent HP+LP construction tile plan is not deterministic.");

    UnusedLevel65ConstructionCollidableTerrainPlan collidable =
        await UnusedLevel65FullAuthoringConstructionTemplate.BuildFirstCollidableTerrainPlanAsync(
            paths.LockedBaseImagePath);
    UnusedLevel65ConstructionCollidableTerrainPlan collidableRepeated =
        await UnusedLevel65FullAuthoringConstructionTemplate.BuildFirstCollidableTerrainPlanAsync(
            paths.LockedBaseImagePath);
    Require(
        collidable.CollisionBinding.ReusedTriangleIndex == 13_995 &&
        collidable.CollisionBinding.SourceTriangleWadOffset == 0x6A881C8 &&
        collidable.CollisionBinding.AuthoredTriangleWadOffset == 0x6A881F8 &&
        collidable.CollisionBinding.BeforeHex == "A7A2140044631900E0010000" &&
        collidable.CollisionBinding.AfterHex == "521E2020CA18004000020080" &&
        collidable.CollisionBinding.SourceAssignment == 255 &&
        collidable.CollisionBinding.TargetAssignment == 0 &&
        collidable.CollisionBinding.SourceLookupReferenceCount == 1 &&
        collidable.CollisionBinding.AuthoredLookupReferenceCount > 0,
        "The first construction tile collision-slot binding drifted.");
    Require(
        collidable.CollisionBinding.TargetCells.SequenceEqual(new[]
        {
            new UnusedLevel65ConstructionCollisionCell(30, 24, 2),
            new UnusedLevel65ConstructionCollisionCell(30, 25, 2)
        }) &&
        collidable.CollisionBinding.SourceWasZeroArea &&
        collidable.CollisionBinding.UpwardWinding &&
        collidable.CollisionBinding.OrdinaryCollisionFlags,
        "The construction tile collision cells, winding, or ordinary native flags drifted.");
    Require(
        collidable.CollisionTreeCapacityBytes == 0x6A60 &&
        collidable.CollisionTreeUsedBytes == 0x6A60 &&
        collidable.CollisionBlocksCapacityBytes == 0x17984 &&
        collidable.CollisionBlocksUsedBytes == 0x17962 &&
        collidable.CollisionBinding.AuthoredLookupReferenceCount == 2 &&
        collidable.CollisionBinding.AuthoredLookupWadOffsets.SequenceEqual(new long[]
        {
            0x6A5168A,
            0x6A51918
        }) &&
        collidable.OcclusionGroupCount == 16 &&
        collidable.OcclusionAssignment == 0 &&
        collidable.AssignedOcclusionGroupContainsSector,
        $"The native-ordered construction collision repack or native occlusion assignment drifted: " +
        $"tree={collidable.CollisionTreeUsedBytes:X}/{collidable.CollisionTreeCapacityBytes:X}, " +
        $"blocks={collidable.CollisionBlocksUsedBytes:X}/{collidable.CollisionBlocksCapacityBytes:X}, " +
        $"refs={collidable.CollisionBinding.AuthoredLookupReferenceCount}, " +
        $"offsets=[{string.Join(',', collidable.CollisionBinding.AuthoredLookupWadOffsets.Select(offset => $"0x{offset:X}"))}].");
    Require(
        collidable.PreservedDegenerateBindings.Select(binding => binding.TriangleIndex)
            .SequenceEqual(new[] { 1_295, 1_298, 11_184 }) &&
        collidable.PreservedDegenerateBindings.All(binding =>
            binding.NativeCells.SequenceEqual(binding.AuthoredCells)) &&
        collidable.PreservedDegenerateBindings.Single(binding => binding.TriangleIndex == 1_295)
            .NativeLookupWadOffsets.SequenceEqual(new long[] { 0x6A51838, 0x6A51AEC }) &&
        collidable.PreservedDegenerateBindings.Single(binding => binding.TriangleIndex == 1_295)
            .AuthoredLookupWadOffsets.SequenceEqual(new long[] { 0x6A51868, 0x6A51B1E }) &&
        collidable.PreservedDegenerateBindings.Single(binding => binding.TriangleIndex == 1_298)
            .NativeLookupWadOffsets.SequenceEqual(new long[] { 0x6A51832 }) &&
        collidable.PreservedDegenerateBindings.Single(binding => binding.TriangleIndex == 1_298)
            .AuthoredLookupWadOffsets.SequenceEqual(new long[] { 0x6A51862 }) &&
        collidable.PreservedDegenerateBindings.Single(binding => binding.TriangleIndex == 11_184)
            .NativeLookupWadOffsets.SequenceEqual(new long[] { 0x6A4D5F8 }) &&
        collidable.PreservedDegenerateBindings.Single(binding => binding.TriangleIndex == 11_184)
            .AuthoredLookupWadOffsets.SequenceEqual(new long[] { 0x6A4D626 }),
        "The native referenced degenerate collision rows were not preserved through the ordered construction index repack: " +
        string.Join("; ", collidable.PreservedDegenerateBindings.Select(binding =>
            $"T{binding.TriangleIndex} src=[{string.Join(',', binding.NativeLookupWadOffsets.Select(offset => $"0x{offset:X}"))}] " +
            $"dst=[{string.Join(',', binding.AuthoredLookupWadOffsets.Select(offset => $"0x{offset:X}"))}]").ToArray()));
    Require(
        collidable.NativeCollisionCellCount == 4_252 &&
        collidable.AuthoredCollisionCellCount == 4_252 &&
        collidable.ChangedCollisionCells.SequenceEqual(new[]
        {
            new UnusedLevel65ConstructionCollisionCell(34, 35, 1),
            new UnusedLevel65ConstructionCollisionCell(30, 24, 2),
            new UnusedLevel65ConstructionCollisionCell(30, 25, 2)
        }) &&
        collidable.UnchangedCollisionCellSequencesPreserved &&
        collidable.IntendedCollisionCellSequencesVerified &&
        collidable.NativeCollisionOrderingPreserved,
        $"The ordered collision semantic delta escaped its exact three cells: native={collidable.NativeCollisionCellCount}, " +
        $"authored={collidable.AuthoredCollisionCellCount}, changed=[{string.Join(',', collidable.ChangedCollisionCells)}].");
    UnusedLevel65ConstructionExposureProof exposure = collidable.ExposureProof;
    Require(
        exposure.ScannedSectorCount == 216 &&
        exposure.ScannedLowDetailFaceCount == 1_437 &&
        exposure.ScannedHighDetailFaceCount == 3_887 &&
        exposure.ScannedCollisionTriangleCount == 19_808 &&
        exposure.NativeLowDetailInteriorOverlaps.SequenceEqual(new[]
        {
            new UnusedLevel65ConstructionTerrainOverlap(124, 0, 480, 480),
            new UnusedLevel65ConstructionTerrainOverlap(213, 1, 512, 512),
            new UnusedLevel65ConstructionTerrainOverlap(213, 3, 512, 512)
        }) &&
        exposure.NativeHighDetailInteriorOverlaps.SequenceEqual(new[]
        {
            new UnusedLevel65ConstructionTerrainOverlap(213, 37, 512, 512)
        }) &&
        exposure.NativeCollisionInteriorOverlaps.SequenceEqual(new[]
        {
            new UnusedLevel65ConstructionCollisionOverlap(1_353, 512, 512),
            new UnusedLevel65ConstructionCollisionOverlap(1_354, 512, 512)
        }) &&
        exposure.NativeLowDetailTopmostAtAuthoredCentroid.SequenceEqual(new[]
        {
            new UnusedLevel65ConstructionTerrainOverlap(213, 1, 512, 512)
        }) &&
        exposure.NativeHighDetailTopmostAtAuthoredCentroid.SequenceEqual(new[]
        {
            new UnusedLevel65ConstructionTerrainOverlap(213, 37, 512, 512)
        }) &&
        exposure.AuthoredLowDetailFaceIndex == 21 &&
        exposure.AuthoredHighDetailFaceIndex == 113 &&
        exposure.ExactUnderlyingFoundationIdentified &&
        exposure.NoHigherNativeLowDetailFace &&
        exposure.NoHigherNativeHighDetailFace &&
        exposure.NoHigherNativeCollisionTriangle &&
        exposure.AuthoredSurfaceTopmostOverOpenInterior &&
        exposure.CloseHighDetailFarLowDetailRuntimeGuideSupported,
        "The full 216-sector HP/LP and 19,808-triangle exposure/topmost proof drifted.");
    Require(
        collidable.BeforeCollisionSha256 ==
            "84901b6b9faa2f7fb0fce1d3aaa7e00fadf49e2d4bd7b2bcb2a0bb9a77397e2f" &&
        collidable.AfterCollisionSha256 ==
            "5e7b4430c9bfbd2793df1d9833d8d3af005924d7c110b66f0b0e1f8bc818c056" &&
        collidable.AfterModelSha256 ==
            "ccd18568b9b6cb7a41d2bf8a47c7dc475ca2cb1f9f127ac9a90ef9ac0a8be4f1",
        $"The exact first collidable construction component/model hashes drifted: collision={collidable.AfterCollisionSha256}, model={collidable.AfterModelSha256}.");
    Require(
        collidable.CollisionIndexRepacked &&
        collidable.CollisionComposed &&
        collidable.OcclusionOwnershipVerified &&
        !collidable.DisposableRuntimeCandidateAuthorized &&
        !collidable.Publishable &&
        !collidable.PromotionAuthorized &&
        !collidable.NormalCreateBinEnabled,
        "The collidable construction tile crossed its static-only publication boundary.");
    Require(
        collidable.AfterModelSha256 == collidableRepeated.AfterModelSha256 &&
        collidable.AfterModelBytes.AsSpan().SequenceEqual(collidableRepeated.AfterModelBytes) &&
        collidable.CollisionBinding.AuthoredLookupWadOffsets.SequenceEqual(
            collidableRepeated.CollisionBinding.AuthoredLookupWadOffsets) &&
        collidable.ExposureProof.NativeLowDetailInteriorOverlaps.SequenceEqual(
            collidableRepeated.ExposureProof.NativeLowDetailInteriorOverlaps) &&
        collidable.ExposureProof.NativeHighDetailInteriorOverlaps.SequenceEqual(
            collidableRepeated.ExposureProof.NativeHighDetailInteriorOverlaps) &&
        collidable.ExposureProof.NativeCollisionInteriorOverlaps.SequenceEqual(
            collidableRepeated.ExposureProof.NativeCollisionInteriorOverlaps),
        "The first HP+LP+collision+occlusion construction plan is not deterministic.");

    VerifyModelOnlyConstructionPlansPreserveSiblingSubfiles(
        paths.LockedBaseImagePath,
        contract,
        plan.AfterModelBytes,
        visual.AfterModelBytes,
        collidable.AfterModelBytes);

    bool unalignedRejected = false;
    try
    {
        await UnusedLevel65FullAuthoringConstructionTemplate.BuildSectorCapacityPlanAsync(
            paths.LockedBaseImagePath,
            213,
            2);
    }
    catch (ArgumentOutOfRangeException)
    {
        unalignedRejected = true;
    }
    Require(unalignedRejected, "An unaligned scene-sector capacity request was accepted.");

    bool tailOverflowRejected = false;
    try
    {
        await UnusedLevel65FullAuthoringConstructionTemplate.BuildSectorCapacityPlanAsync(
            paths.LockedBaseImagePath,
            213,
            0x2FC);
    }
    catch (InvalidDataException)
    {
        tailOverflowRejected = true;
    }
    Require(tailOverflowRejected, "A scene-sector capacity request beyond the verified model tail was accepted.");
    Require(
        await HashFileAsync(paths.LockedBaseImagePath) == baselineHashBefore,
        "The full-authoring construction inspection/capacity plan changed the locked V5 baseline.");
    Require(
        artifactPathsBefore.SequenceEqual(SnapshotArtifactPaths(paths.RootPath)),
        "The full-authoring construction inspection/planner calls created or deleted a workspace artifact.");
}

async Task VerifyBootstrapStaleOperationRecoveryAsync(
    UnusedLevel65BlankLevelLabWorkspacePaths paths)
{
    Directory.CreateDirectory(paths.OperationsDirectoryPath);
    string nonOwned = Path.Combine(paths.OperationsDirectoryPath, "do-not-delete");
    Directory.CreateDirectory(nonOwned);
    bool nonOwnedRejected = false;
    try
    {
        await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceAsync(paths);
    }
    catch (InvalidDataException)
    {
        nonOwnedRejected = true;
    }
    Require(nonOwnedRejected, "Bootstrap validation accepted a non-owned operation directory.");
    Require(Directory.Exists(nonOwned), "Bootstrap recovery deleted a non-owned operation directory.");
    Directory.Delete(nonOwned, recursive: true);
    Directory.Delete(paths.OperationsDirectoryPath);

    string stale = CreateOwnedStaleOperation(paths.OperationsDirectoryPath, "bootstrap-");
    UnusedLevel65BlankLevelLabManifest manifest =
        await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceAsync(paths);
    Require(manifest.LockedBaseImageSha256 == ExpectedLockedBaseImageSha256, "Stale recovery changed the valid manifest.");
    Require(!Directory.Exists(stale), "Validation did not remove strictly owned bootstrap crash debris.");
    Require(!Directory.Exists(paths.OperationsDirectoryPath), "Validation retained an empty bootstrap operations root.");
}

async Task VerifyDeterministicReuseAsync(
    UnusedLevel65BlankLevelLabBootstrapResult first,
    UnusedLevel65BlankLevelLabBootstrapResult reused,
    byte[] manifestBefore)
{
    Require(reused.ReusedExistingLockedBase, "Second bootstrap did not reuse the exact locked base.");
    Require(reused.SourceImagePreserved, "Reused bootstrap lost source-preservation proof.");
    Require(reused.AtomicPublicationCompleted, "Reused bootstrap lost publication proof.");
    Require(reused.OwnedTemporaryIntermediatesRemoved, "Reused bootstrap reported temporary debris.");
    Require(reused.Manifest == first.Manifest, "Reuse returned a different manifest.");
    Require(reused.LockedBaseImageSha256 == first.LockedBaseImageSha256, "Reuse returned a different locked hash.");
    Require(await HashFileAsync(reused.Paths.LockedBaseImagePath) == ExpectedLockedBaseImageSha256, "Reuse changed the locked BIN.");
    Require((await File.ReadAllBytesAsync(reused.Paths.ManifestPath)).SequenceEqual(manifestBefore), "Reuse rewrote the manifest.");
    Require(!Directory.Exists(reused.Paths.OperationsDirectoryPath), "Reuse created operation debris.");
}

async Task VerifyBootstrapIncompleteRollbackRestartRecoveryAsync(
    UnusedLevel65BlankLevelLabBootstrapRequest normalRequest,
    UnusedLevel65BlankLevelLabWorkspacePaths paths)
{
    IReadOnlyDictionary<string, string> publishedBefore = SnapshotDirectory(paths.RootPath);
    bool incompleteRollbackObserved = false;
    try
    {
        await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(normalRequest with
        {
            ReplaceInvalidExistingWorkspace = true,
            TestStageHook = stage =>
            {
                if (stage == "before-existing-workspace-reuse")
                    throw new InvalidDataException("Injected valid-workspace rebuild proof.");
                if (stage == "after-workspace-publication")
                    throw new IOException("Injected bootstrap post-publication failure.");
                if (stage == "before-previous-workspace-restore")
                    throw new IOException("Injected bootstrap rollback-restore failure.");
            }
        });
    }
    catch (IOException ex) when (
        ex.ToString().Contains("Injected bootstrap rollback-restore failure", StringComparison.Ordinal))
    {
        incompleteRollbackObserved = true;
    }

    Require(incompleteRollbackObserved, "Bootstrap did not preserve an injected incomplete rollback.");
    Require(Directory.Exists(paths.OperationsDirectoryPath), "Bootstrap deleted its sole restart-recovery operation.");
    string[] operations = Directory.GetDirectories(paths.OperationsDirectoryPath, "bootstrap-*");
    Require(operations.Length == 1, "Bootstrap incomplete rollback did not retain exactly one owned operation.");
    Require(
        File.Exists(Path.Combine(operations[0], "operation-journal.json")) &&
        Directory.GetFiles(operations[0], "previous-*", SearchOption.TopDirectoryOnly).Length == 3,
        "Bootstrap incomplete rollback did not retain its journal and three prior-workspace backups.");

    UnusedLevel65BlankLevelLabBootstrapResult recovered =
        await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(normalRequest);
    Require(recovered.ReusedExistingLockedBase, "Bootstrap restart recovery did not reuse the restored prior workspace.");
    RequireSnapshot(paths.RootPath, publishedBefore);
    Require(!Directory.Exists(paths.OperationsDirectoryPath), "Bootstrap restart recovery retained operation debris.");
    Require(
        await HashFileAsync(paths.LockedBaseImagePath) == ExpectedLockedBaseImageSha256,
        "Bootstrap restart recovery changed the locked base.");
}

async Task VerifyGenericTerrainV4ParityAsync(
    UnusedLevel65BlankLevelLabWorkspacePaths labPaths,
    string root)
{
    string parityRoot = Path.Combine(root, "generic-v4-parity");
    Directory.CreateDirectory(parityRoot);
    string analysisPath = Path.Combine(parityRoot, "id65-wad-analysis.json");
    string overlayPath = Path.Combine(parityRoot, "id65-source-overlay.json");
    string sourceSearchPath = Path.Combine(parityRoot, "id65-source-search.json");
    string editsPath = Path.Combine(labPaths.AuthoredEditsDirectoryPath, "id65-v4-parity.json");
    string alternateEditsPath = Path.Combine(labPaths.AuthoredEditsDirectoryPath, "id65-v4-rollback-alternate.json");

    object analysis = new
    {
        wad = new { lba = WadLba },
        entries = new[]
        {
            new
            {
                index = 80,
                offset = 0x6936800,
                level = new
                {
                    subfiles = new[]
                    {
                        new { index = 1, offset = 0xDE800, size = 0x94800 }
                    }
                }
            }
        }
    };
    await File.WriteAllTextAsync(
        analysisPath,
        JsonSerializer.Serialize(analysis),
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    SourceSceneOverlayResult overlay = await SourceSceneOverlayExporter.ExportAsync(
        labPaths.LockedBaseImagePath,
        analysisPath,
        UnusedLevel65BlankLevelLabProfileRegistry.Definition,
        overlayPath);
    Require(
        overlay.WadEntry == 80 && overlay.SectorCount == 216 && overlay.HpFaces == 3887 && overlay.LpFaces == 1437,
        $"ID65 source overlay drifted: sectors={overlay.SectorCount}, HP={overlay.HpFaces}, LP={overlay.LpFaces}.");

    await VerifySourceBoundTextureCacheAsync(labPaths, parityRoot, overlayPath);

    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
    TerrainSourceSearchResult sourceSearch = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(new(
        labPaths.LockedBaseImagePath,
        sourceSearchPath,
        UnusedLevel65BlankLevelLabProfileRegistry.Definition,
        geometry));
    Require(
        sourceSearch.Report.SectorCount > 0 &&
        sourceSearch.Report.MatchedSectorCount == sourceSearch.Report.SectorCount &&
        sourceSearch.Report.MissingSectorCount == 0 &&
        sourceSearch.Report.AmbiguousSectorCount == 0,
        $"ID65 source-derived sector mapping is incomplete or ambiguous: " +
        $"sectors={sourceSearch.Report.SectorCount}, matched={sourceSearch.Report.MatchedSectorCount}, " +
        $"missing={sourceSearch.Report.MissingSectorCount}, ambiguous={sourceSearch.Report.AmbiguousSectorCount}.");

    TerrainPolygon face = geometry.Polygons.Single(polygon =>
        polygon.Detail.Equals("hp", StringComparison.OrdinalIgnoreCase) &&
        polygon.SectorIndex == 213 &&
        polygon.FaceIndex == 37);
    Require(face.RuntimeKey == "213:37:hp", "V4 parity selected the wrong runtime face.");
    Require(face.VertexIndexes.SequenceEqual(new[] { 40, 57, 48, 39 }), "V4 parity face vertex indexes drifted.");
    Require(
        face.Points.Count == 4 &&
        FacePoint(face, 0, 7762, 6346, 512) &&
        FacePoint(face, 1, 7890, 6346, 512) &&
        FacePoint(face, 2, 7890, 6474, 512) &&
        FacePoint(face, 3, 7762, 6474, 512),
        "V4 parity face coordinates drifted.");
    face.ApplyTerrainVertexDeltas(new float[] { 0, 0, 96, 96 });
    int saved = await TerrainEditStore.SaveAsync(editsPath, new[] { face }, "ID65 Blank-Level Lab v4 parity");
    Require(saved == 1, "V4 parity did not save exactly one authored HP Z edit.");

    UnusedLevel65BlankLevelLabTerrainTestPaths parityPaths =
        UnusedLevel65BlankLevelLabTerrainTestExporter.CreatePaths(
            labPaths.ContainerRootPath,
            "id65-v4-generic-parity");
    string ownedStaleTerrainOperation = CreateOwnedStaleOperation(
        parityPaths.OperationsDirectoryPath,
        "terrain-test-");
    string staleCandidateDirectory = Path.Combine(ownedStaleTerrainOperation, "candidate");
    Directory.CreateDirectory(staleCandidateDirectory);
    File.WriteAllText(
        Path.Combine(staleCandidateDirectory, "stale-static-readback-receipt.json"),
        "{\"stale\":true}\n");
    string nonOwnedTerrainOperation = Path.Combine(
        parityPaths.OperationsDirectoryPath,
        "do-not-delete");
    Directory.CreateDirectory(nonOwnedTerrainOperation);
    bool nonOwnedTerrainRejected = false;
    try
    {
        await UnusedLevel65BlankLevelLabTerrainTestExporter.CreateDisposableTerrainTestAsync(new(
            labPaths.ContainerRootPath,
            editsPath,
            "id65-v4-generic-parity",
            "ID65 v4 generic terrain parity"));
    }
    catch (InvalidDataException)
    {
        nonOwnedTerrainRejected = true;
    }
    Require(nonOwnedTerrainRejected, "Terrain export accepted a non-owned operation directory.");
    Require(
        Directory.Exists(nonOwnedTerrainOperation) && Directory.Exists(ownedStaleTerrainOperation),
        "Terrain recovery deleted entries before completing its strict ownership preflight.");
    Directory.Delete(nonOwnedTerrainOperation, recursive: true);

    UnusedLevel65BlankLevelLabTerrainTestResult disposable =
        await UnusedLevel65BlankLevelLabTerrainTestExporter.CreateDisposableTerrainTestAsync(new(
            labPaths.ContainerRootPath,
            editsPath,
            "id65-v4-generic-parity",
            "ID65 v4 generic terrain parity"));
    TerrainPatchResult terrain = disposable.Terrain;
    Require(terrain.WroteImage, "Generic v4 parity did not write a BIN/CUE.");
    Require(
        disposable.LockedBasePreserved &&
        disposable.LogicalPatchReadbackVerified &&
        disposable.VerifiedRawSectorCount == 2 &&
        !disposable.PromotionAuthorized &&
        !disposable.NormalCreateBinEnabled,
        "Core disposable terrain-test safety/readback flags drifted.");
    bool expectedFinderHandoff = OperatingSystem.IsMacOS();
    Require(
        disposable.FinderHandoffVerified == expectedFinderHandoff &&
        (disposable.FinderReveal != null) == expectedFinderHandoff,
        "Core disposable terrain test did not apply the platform-aware Finder policy.");
    Require(
        disposable.LoadCodes.Select(code => code.LevelId).SequenceEqual(new[] { 65, 13, 64, 15 }),
        "Core disposable terrain test omitted a comparison load code.");
    Require(File.Exists(disposable.Paths.RuntimeChecklistPath), "Core disposable terrain checklist is missing.");
    Require(
        File.Exists(disposable.Paths.FinderHelperPath) == expectedFinderHandoff,
        "Core disposable terrain Finder helper presence does not match the platform policy.");
    Require(File.Exists(disposable.Paths.StaticReceiptPath), "Core disposable terrain static/readback receipt is missing.");
    Require(File.Exists(disposable.Paths.TerrainEditsSnapshotPath), "Core disposable immutable terrain-edit snapshot is missing.");
    Require(!Directory.Exists(parityPaths.OperationsDirectoryPath), "Terrain export retained owned stale or active operation debris.");
    UnusedLevel65BlankLevelLabTerrainTestReceipt receipt = disposable.Receipt;
    Require(
        receipt.ReceiptSchemaVersion == 2 &&
        receipt.ProfileId == UnusedLevel65BlankLevelLabProfileRegistry.ProfileId &&
        receipt.ProfileVersion == UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion &&
        receipt.WorkspaceKey == UnusedLevel65BlankLevelLabProfileRegistry.Key &&
        receipt.TestKey == "id65-v4-generic-parity" &&
        receipt.LockedBaseImageSha256 == ExpectedLockedBaseImageSha256 &&
        receipt.OutputImageSha256 == ExpectedV4TerrainImageSha256 &&
        receipt.PatchCount == 12 &&
        receipt.VerifiedRawSectorCount == 2 &&
        receipt.LogicalPatchReadbackVerified &&
        receipt.LockedBasePreserved &&
        !receipt.PromotionAuthorized &&
        !receipt.NormalCreateBinAuthorized,
        "Core disposable terrain receipt identity, hashes, counts, or safety flags drifted.");
    Require(
        receipt.PatchKinds.SequenceEqual(new[]
        {
            new UnusedLevel65BlankLevelLabTerrainTestPatchKindReceipt("collision-triangle-fan", 10),
            new UnusedLevel65BlankLevelLabTerrainTestPatchKindReceipt("visual-hp-topology-safe", 2)
        }),
        "Core disposable terrain receipt patch-kind counts drifted.");
    Require(
        PathsEqual(receipt.WorkspaceRootPath, labPaths.RootPath) &&
        PathsEqual(receipt.ManifestPath, labPaths.ManifestPath) &&
        PathsEqual(receipt.SourceTerrainEditsPath, editsPath) &&
        PathsEqual(receipt.TerrainEditsSnapshotPath, disposable.Paths.TerrainEditsSnapshotPath) &&
        receipt.TerrainEditsSnapshotSha256 == HashFile(disposable.Paths.TerrainEditsSnapshotPath) &&
        PathsEqual(receipt.SourceOverlayPath, disposable.Paths.SourceOverlayPath) &&
        PathsEqual(receipt.SourceSearchPath, disposable.Paths.SourceSearchPath) &&
        PathsEqual(receipt.LockedBaseImagePath, labPaths.LockedBaseImagePath) &&
        PathsEqual(receipt.OutputImagePath, disposable.Paths.OutputImagePath) &&
        PathsEqual(receipt.OutputCuePath, disposable.Paths.OutputCuePath) &&
        PathsEqual(receipt.OutputPlanPath, disposable.Paths.OutputPlanPath) &&
        receipt.OutputPlanSha256 == HashFile(disposable.Paths.OutputPlanPath) &&
        PathsEqual(receipt.RuntimeChecklistPath, disposable.Paths.RuntimeChecklistPath) &&
        PathsEqual(receipt.StaticReceiptPath, disposable.Paths.StaticReceiptPath),
        "Core disposable terrain receipt artifact paths drifted.");
    Require(
        PathsEqual(terrain.Plan.TerrainEditsPath, disposable.Paths.TerrainEditsSnapshotPath),
        "The published terrain plan is not bound to the immutable candidate snapshot.");
    Require(
        receipt.LoadCodes.Select(code => code.LevelId).SequenceEqual(new[] { 65, 13, 64, 15 }) &&
        receipt.LoadCodes.All(code => !string.IsNullOrWhiteSpace(code.InputCode)),
        "Core disposable terrain receipt omitted an exact comparison code.");
    UnusedLevel65BlankLevelLabTerrainTestReceipt publicReceiptReadback =
        await UnusedLevel65BlankLevelLabTerrainTestExporter.ReadAndValidateReceiptAsync(disposable.Paths);
    Require(
        JsonSerializer.Serialize(publicReceiptReadback) == JsonSerializer.Serialize(receipt),
        "Public terrain receipt readback differs from the published result.");
    using (JsonDocument receiptDocument = JsonDocument.Parse(await File.ReadAllTextAsync(disposable.Paths.StaticReceiptPath)))
    {
        Require(
            !receiptDocument.RootElement.GetProperty("promotionAuthorized").GetBoolean() &&
            !receiptDocument.RootElement.GetProperty("normalCreateBinAuthorized").GetBoolean() &&
            receiptDocument.RootElement.GetProperty("terrainEditsSnapshotSha256").GetString() ==
                receipt.TerrainEditsSnapshotSha256 &&
            receiptDocument.RootElement.GetProperty("outputPlanSha256").GetString() ==
                receipt.OutputPlanSha256 &&
            receiptDocument.RootElement.GetProperty("loadCodes").GetArrayLength() == 4,
            "Serialized terrain receipt changed its safety flags or code count.");
    }

    byte[] mutableEditsBytes = await File.ReadAllBytesAsync(editsPath);
    await File.WriteAllTextAsync(
        editsPath,
        "{\"editCount\":0,\"edits\":[]}\n",
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    await UnusedLevel65BlankLevelLabTerrainTestExporter.ReadAndValidateReceiptAsync(disposable.Paths);
    File.Delete(editsPath);
    await UnusedLevel65BlankLevelLabTerrainTestExporter.ReadAndValidateReceiptAsync(disposable.Paths);
    await File.WriteAllBytesAsync(editsPath, mutableEditsBytes);

    byte[] snapshotBytes = await File.ReadAllBytesAsync(disposable.Paths.TerrainEditsSnapshotPath);
    await File.WriteAllBytesAsync(
        disposable.Paths.TerrainEditsSnapshotPath,
        [.. snapshotBytes, (byte)'\n']);
    await ExpectInvalidDataAsync(
        () => UnusedLevel65BlankLevelLabTerrainTestExporter.ReadAndValidateReceiptAsync(disposable.Paths),
        "terrain snapshot SHA tamper");
    await File.WriteAllBytesAsync(disposable.Paths.TerrainEditsSnapshotPath, snapshotBytes);

    byte[] receiptBytes = await File.ReadAllBytesAsync(disposable.Paths.StaticReceiptPath);
    byte[] invalidSnapshot = Encoding.UTF8.GetBytes("{\"editCount\":0,\"edits\":[]}\n");
    await File.WriteAllBytesAsync(disposable.Paths.TerrainEditsSnapshotPath, invalidSnapshot);
    string rewrittenReceipt = Encoding.UTF8.GetString(receiptBytes).Replace(
        receipt.TerrainEditsSnapshotSha256,
        HashBytes(invalidSnapshot),
        StringComparison.Ordinal);
    await File.WriteAllTextAsync(
        disposable.Paths.StaticReceiptPath,
        rewrittenReceipt,
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    await ExpectInvalidDataAsync(
        () => UnusedLevel65BlankLevelLabTerrainTestExporter.ReadAndValidateReceiptAsync(disposable.Paths),
        "terrain snapshot semantic tamper");
    await File.WriteAllBytesAsync(disposable.Paths.TerrainEditsSnapshotPath, snapshotBytes);
    await File.WriteAllBytesAsync(disposable.Paths.StaticReceiptPath, receiptBytes);

    byte[] planBytes = await File.ReadAllBytesAsync(disposable.Paths.OutputPlanPath);
    await File.WriteAllBytesAsync(disposable.Paths.OutputPlanPath, [.. planBytes, (byte)'\n']);
    await ExpectInvalidDataAsync(
        () => UnusedLevel65BlankLevelLabTerrainTestExporter.ReadAndValidateReceiptAsync(disposable.Paths),
        "terrain plan SHA tamper");
    await File.WriteAllBytesAsync(disposable.Paths.OutputPlanPath, planBytes);
    await UnusedLevel65BlankLevelLabTerrainTestExporter.ReadAndValidateReceiptAsync(disposable.Paths);

    Require(terrain.Plan.SkippedEdits.Count == 0, $"Generic v4 parity skipped edits: {string.Join(" | ", terrain.Plan.SkippedEdits)}");
    TerrainPatch[] visual = terrain.Plan.Patches
        .Where(patch => patch.Kind == "visual-hp-topology-safe")
        .ToArray();
    TerrainPatch[] collision = terrain.Plan.Patches
        .Where(patch => patch.Kind == "collision-triangle-fan")
        .ToArray();
    Require(visual.Length == 2, $"Generic v4 parity emitted {visual.Length} visual patches instead of 2.");
    Require(collision.Length == 10, $"Generic v4 parity emitted {collision.Length} collision patches instead of 10.");
    Require(terrain.Plan.Patches.Count == 12, "Generic v4 parity emitted unexpected patch kinds.");
    Require(
        ExtractCollisionTriangleIndexes(collision).SetEquals(new[] { 1353, 1354, 1360, 1361, 1363, 1369, 1370, 1395, 1400, 1401 }),
        "Generic v4 parity collision fan drifted.");
    Require(await HashFileAsync(terrain.OutputImagePath) == ExpectedV4TerrainImageSha256, "Generic exporter did not reproduce the exact runtime-passed v4 BIN.");
    Require(disposable.OutputImageSha256 == ExpectedV4TerrainImageSha256, "Disposable result hash differs from exact v4.");
    Require(await HashFileAsync(labPaths.LockedBaseImagePath) == ExpectedLockedBaseImageSha256, "Generic export consumed or changed the locked base.");
    ValidateCuePair(terrain.OutputCuePath, terrain.OutputImagePath);
    Require(
        terrain.Plan.Patches
            .Select(patch => WadLba + checked((int)(ParseHexOffset(patch.WadRelativeOffset) / 2048)))
            .Distinct()
            .Order()
            .SequenceEqual(new[] { 54434, 54507 }),
        "Generic v4 parity touched unexpected logical/raw sectors.");

    IReadOnlyDictionary<string, string> publishedSnapshot = SnapshotDirectory(disposable.Paths.TestDirectoryPath);
    Require(
        publishedSnapshot.Keys.Any(path => path.EndsWith("-static-readback-receipt.json", StringComparison.Ordinal)),
        "Replacement rollback snapshot omitted the durable receipt.");
    Require(
        publishedSnapshot.Keys.Any(path => path.EndsWith("-terrain-edits-snapshot.json", StringComparison.Ordinal)),
        "Replacement rollback snapshot omitted the immutable terrain-edit provenance.");
    face.ResetTerrainEdit();
    face.ApplyTerrainVertexDeltas(new float[] { 0, 0, 64, 64 });
    int alternateSaved = await TerrainEditStore.SaveAsync(
        alternateEditsPath,
        new[] { face },
        "ID65 Blank-Level Lab post-publication rollback fixture");
    Require(alternateSaved == 1, "Rollback fixture did not save exactly one alternate HP Z edit.");
    bool injectedRollbackObserved = false;
    try
    {
        await UnusedLevel65BlankLevelLabTerrainTestExporter.CreateDisposableTerrainTestAsync(new(
            labPaths.ContainerRootPath,
            alternateEditsPath,
            "id65-v4-generic-parity",
            "ID65 v4 rollback replacement",
            ReplaceExistingTest: true,
            RequestFinderReveal: true,
            TestStageHook: stage =>
            {
                if (stage == "after-candidate-publication")
                    throw new IOException("Injected post-publication rollback proof.");
            }));
    }
    catch (IOException ex) when (ex.Message.Contains("Injected post-publication rollback proof", StringComparison.Ordinal))
    {
        injectedRollbackObserved = true;
    }
    Require(injectedRollbackObserved, "The terrain replacement post-publication fault was not injected.");
    RequireSnapshot(disposable.Paths.TestDirectoryPath, publishedSnapshot);
    Require(!Directory.Exists(parityPaths.OperationsDirectoryPath), "Replacement rollback retained operation or backup debris.");
    Require(await HashFileAsync(disposable.Paths.OutputImagePath) == ExpectedV4TerrainImageSha256, "Replacement rollback did not restore the exact prior v4 BIN.");
    UnusedLevel65BlankLevelLabTerrainTestReceipt restoredReceipt =
        await UnusedLevel65BlankLevelLabTerrainTestExporter.ReadAndValidateReceiptAsync(disposable.Paths);
    Require(
        restoredReceipt.OutputImageSha256 == ExpectedV4TerrainImageSha256 &&
        restoredReceipt.PatchCount == 12,
        "Replacement rollback did not restore a valid prior static/readback receipt.");

    bool incompleteTerrainRollbackObserved = false;
    try
    {
        await UnusedLevel65BlankLevelLabTerrainTestExporter.CreateDisposableTerrainTestAsync(new(
            labPaths.ContainerRootPath,
            alternateEditsPath,
            "id65-v4-generic-parity",
            "ID65 v4 incomplete rollback restart fixture",
            ReplaceExistingTest: true,
            RequestFinderReveal: true,
            TestStageHook: stage =>
            {
                if (stage == "after-candidate-publication")
                    throw new IOException("Injected terrain post-publication failure.");
                if (stage == "before-previous-candidate-restore")
                    throw new IOException("Injected terrain rollback-restore failure.");
            }));
    }
    catch (IOException ex) when (
        ex.ToString().Contains("Injected terrain rollback-restore failure", StringComparison.Ordinal))
    {
        incompleteTerrainRollbackObserved = true;
    }
    Require(incompleteTerrainRollbackObserved, "Terrain exporter did not preserve an injected incomplete rollback.");
    Require(Directory.Exists(parityPaths.OperationsDirectoryPath), "Terrain exporter deleted its sole restart-recovery operation.");
    string[] terrainRecoveryOperations = Directory.GetDirectories(
        parityPaths.OperationsDirectoryPath,
        "terrain-test-*");
    Require(terrainRecoveryOperations.Length == 1, "Terrain incomplete rollback retained an unexpected operation count.");
    Require(
        File.Exists(Path.Combine(terrainRecoveryOperations[0], "operation-journal.json")) &&
        Directory.Exists(Path.Combine(terrainRecoveryOperations[0], "previous-candidate")),
        "Terrain incomplete rollback did not retain its journal and prior-candidate snapshot.");
    UnusedLevel65BlankLevelLabTerrainTestReceipt restartRecoveredReceipt =
        await UnusedLevel65BlankLevelLabTerrainTestExporter.ReadAndValidateReceiptAsync(disposable.Paths);
    Require(
        restartRecoveredReceipt.OutputImageSha256 == ExpectedV4TerrainImageSha256,
        "Terrain restart recovery did not restore the exact prior receipt.");
    RequireSnapshot(disposable.Paths.TestDirectoryPath, publishedSnapshot);
    Require(!Directory.Exists(parityPaths.OperationsDirectoryPath), "Terrain restart recovery retained operation debris.");

    UnusedLevel65BlankLevelLabTerrainTestPaths noFinderPaths =
        UnusedLevel65BlankLevelLabTerrainTestExporter.CreatePaths(
            labPaths.ContainerRootPath,
            "id65-v4-no-finder");
    string ambiguousOperation = CreateOwnedStaleOperation(
        noFinderPaths.OperationsDirectoryPath,
        "terrain-test-");
    Directory.CreateDirectory(Path.Combine(ambiguousOperation, "previous-candidate"));
    bool ambiguousBackupRejected = false;
    try
    {
        await UnusedLevel65BlankLevelLabTerrainTestExporter.CreateDisposableTerrainTestAsync(new(
            labPaths.ContainerRootPath,
            editsPath,
            "id65-v4-no-finder",
            "ID65 v4 platform-neutral handoff",
            RequestFinderReveal: false));
    }
    catch (InvalidDataException)
    {
        ambiguousBackupRejected = true;
    }
    Require(ambiguousBackupRejected, "Terrain recovery deleted an ambiguous prior-candidate backup.");
    Require(Directory.Exists(ambiguousOperation), "Ambiguous terrain recovery did not preserve its backup for manual recovery.");
    Directory.Delete(ambiguousOperation, recursive: true);
    Directory.Delete(noFinderPaths.OperationsDirectoryPath);

    UnusedLevel65BlankLevelLabTerrainTestResult noFinder =
        await UnusedLevel65BlankLevelLabTerrainTestExporter.CreateDisposableTerrainTestAsync(new(
            labPaths.ContainerRootPath,
            editsPath,
            "id65-v4-no-finder",
            "ID65 v4 platform-neutral handoff",
            RequestFinderReveal: false));
    Require(noFinder.FinderReveal == null && !noFinder.FinderHandoffVerified, "Finder-disabled export returned a reveal helper.");
    Require(!File.Exists(noFinder.Paths.FinderHelperPath), "Finder-disabled export wrote a helper file.");
    Require(noFinder.OutputImageSha256 == ExpectedV4TerrainImageSha256, "Finder-disabled export changed the exact v4 BIN.");
    Require(
        noFinder.Receipt.FinderHelperPath == null &&
        noFinder.Receipt.LoadCodes.Count == 4 &&
        File.Exists(noFinder.Paths.StaticReceiptPath),
        "Finder-disabled receipt omitted its durable platform-neutral handoff.");
    await UnusedLevel65BlankLevelLabTerrainTestExporter.ReadAndValidateReceiptAsync(noFinder.Paths);
    string noFinderChecklist = await File.ReadAllTextAsync(noFinder.Paths.RuntimeChecklistPath);
    Require(
        noFinderChecklist.Contains(Path.GetFileName(noFinder.Paths.OutputCuePath), StringComparison.Ordinal) &&
        noFinder.LoadCodes.Select(code => code.LevelId).SequenceEqual(new[] { 65, 13, 64, 15 }) &&
        noFinder.LoadCodes.All(code => noFinderChecklist.Contains(code.InputCode, StringComparison.Ordinal)),
        "Finder-disabled handoff omitted its CUE or comparison codes.");
    Require(await HashFileAsync(labPaths.LockedBaseImagePath) == ExpectedLockedBaseImageSha256, "Rollback/platform handoff tests changed the locked base.");
}

async Task VerifySourceBoundTextureCacheAsync(
    UnusedLevel65BlankLevelLabWorkspacePaths labPaths,
    string parityRoot,
    string overlayPath)
{
    LevelDefinition level = UnusedLevel65BlankLevelLabProfileRegistry.Definition;
    string cacheWorkspaceRoot = Path.Combine(parityRoot, "id65-source-bound-texture-cache");
    string cacheDirectory = Path.Combine(
        cacheWorkspaceRoot,
        "editor-cache",
        "terrain-textures",
        LevelCatalog.NormalizeKey(level.Key));
    string operationsRoot = Path.Combine(cacheWorkspaceRoot, ".terrain-texture-cache-operations");
    string sourceImageSha256 = await HashFileAsync(labPaths.LockedBaseImagePath);
    string sourceOverlaySha256 = await HashFileAsync(overlayPath);
    FileInfo sourceImageInfo = new(labPaths.LockedBaseImagePath);
    FileInfo sourceOverlayInfo = new(overlayPath);
    string wrongSourceSha256 = sourceImageSha256[0] == '0'
        ? $"1{sourceImageSha256[1..]}"
        : $"0{sourceImageSha256[1..]}";
    string wrongOverlaySha256 = sourceOverlaySha256[0] == '0'
        ? $"1{sourceOverlaySha256[1..]}"
        : $"0{sourceOverlaySha256[1..]}";

    Require(
        sourceImageSha256 == ExpectedLockedBaseImageSha256 &&
        level.LevelId == 65 &&
        level.SourceWadEntry == 80,
        "The source-bound texture-cache fixture is not the exact locked ID65 row-80 source.");

    int textureCount = await PortableEditorCacheBuilder.BuildTerrainTexturePreviewCacheFromSourceAsync(
        labPaths.LockedBaseImagePath,
        overlayPath,
        cacheWorkspaceRoot,
        level,
        overwrite: true,
        expectedSourceImageSha256: sourceImageSha256,
        expectedSourceOverlaySha256: sourceOverlaySha256);
    Require(textureCount == 66, $"The source-bound ID65 texture cache decoded {textureCount} records instead of 66.");
    Require(
        PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCacheFromSource(
            cacheWorkspaceRoot,
            level,
            sourceImageSha256,
            sourceOverlaySha256,
            sourceImageInfo.Length,
            sourceOverlayInfo.Length,
            expectedTextureCount: 66),
        "The exact ID65 source/overlay-bound texture cache failed immediate readback.");

    string manifestPath = Path.Combine(cacheDirectory, "manifest.json");
    string bindingPath = Path.Combine(cacheDirectory, "source-binding.json");
    Require(File.Exists(manifestPath) && File.Exists(bindingPath), "The source-bound cache omitted its manifest or binding receipt.");
    using (JsonDocument manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath)))
    {
        JsonElement root = manifest.RootElement;
        JsonElement textures = root.GetProperty("textures");
        int[] textureIds = textures.EnumerateArray()
            .Select(texture => texture.GetProperty("textureId").GetInt32())
            .Distinct()
            .Order()
            .ToArray();
        Require(
            root.GetProperty("nativeTextureCount").GetInt32() == 66 &&
            root.GetProperty("requestedTextureCount").GetInt32() == 66 &&
            root.GetProperty("decodedTextureCount").GetInt32() == 66 &&
            root.GetProperty("completeDualTierTextureCount").GetInt32() == 66 &&
            textures.GetArrayLength() == 132 &&
            textureIds.SequenceEqual(Enumerable.Range(0, 66)) &&
            Directory.GetFiles(cacheDirectory, "*.png", SearchOption.TopDirectoryOnly).Length == 132,
            "The exact ID65 cache is not a complete 66-record, 132-frame dual-tier cache.");
    }
    byte[] bindingBytes = await File.ReadAllBytesAsync(bindingPath);
    using (JsonDocument binding = JsonDocument.Parse(bindingBytes))
    {
        JsonElement root = binding.RootElement;
        Require(
            root.GetProperty("bindingSchemaVersion").GetInt32() == 1 &&
            root.GetProperty("levelKey").GetString() == LevelCatalog.NormalizeKey(level.Key) &&
            root.GetProperty("levelId").GetInt32() == 65 &&
            root.GetProperty("sourceWadEntry").GetInt32() == 80 &&
            root.GetProperty("sourceImageByteLength").GetInt64() == sourceImageInfo.Length &&
            root.GetProperty("sourceImageSha256").GetString() == sourceImageSha256 &&
            root.GetProperty("sourceOverlayByteLength").GetInt64() == sourceOverlayInfo.Length &&
            root.GetProperty("sourceOverlaySha256").GetString() == sourceOverlaySha256 &&
            root.GetProperty("textureCount").GetInt32() == 66,
            "The ID65 texture-cache binding receipt is not exact for its source, overlay, level, and 66 records.");
    }

    Require(
        !PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCacheFromSource(
            cacheWorkspaceRoot,
            level,
            wrongSourceSha256,
            sourceOverlaySha256,
            sourceImageInfo.Length,
            sourceOverlayInfo.Length,
            expectedTextureCount: 66) &&
        !PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCacheFromSource(
            cacheWorkspaceRoot,
            level,
            sourceImageSha256,
            wrongOverlaySha256,
            sourceImageInfo.Length,
            sourceOverlayInfo.Length,
            expectedTextureCount: 66),
        "The source-bound cache accepted a wrong expected source or overlay hash.");

    string bindingJson = Encoding.UTF8.GetString(bindingBytes);
    string tamperedBindingJson = bindingJson.Replace(
        "\"levelId\": 65",
        "\"levelId\": 64",
        StringComparison.Ordinal);
    Require(tamperedBindingJson != bindingJson, "The binding-tamper fixture did not alter the serialized level identity.");
    await File.WriteAllTextAsync(
        bindingPath,
        tamperedBindingJson,
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Require(
        PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(cacheWorkspaceRoot, level.Key) &&
        !PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCacheFromSource(
            cacheWorkspaceRoot,
            level,
            sourceImageSha256,
            sourceOverlaySha256,
            sourceImageInfo.Length,
            sourceOverlayInfo.Length,
            expectedTextureCount: 66),
        "The exact source-binding guard accepted a tampered binding receipt.");
    await File.WriteAllBytesAsync(bindingPath, bindingBytes);

    string pngPath = Directory.GetFiles(cacheDirectory, "*.png", SearchOption.TopDirectoryOnly)
        .Order(StringComparer.Ordinal)
        .First();
    byte[] pngBytes = await File.ReadAllBytesAsync(pngPath);
    await File.WriteAllBytesAsync(pngPath, [.. pngBytes, 0]);
    Require(
        !PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCacheFromSource(
            cacheWorkspaceRoot,
            level,
            sourceImageSha256,
            sourceOverlaySha256,
            sourceImageInfo.Length,
            sourceOverlayInfo.Length,
            expectedTextureCount: 66),
        "The source-bound cache accepted a tampered PNG frame.");
    await File.WriteAllBytesAsync(pngPath, pngBytes);
    Require(
        PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCacheFromSource(
            cacheWorkspaceRoot,
            level,
            sourceImageSha256,
            sourceOverlaySha256,
            sourceImageInfo.Length,
            sourceOverlayInfo.Length,
            expectedTextureCount: 66),
        "Restoring the binding and PNG bytes did not restore the exact source-bound cache.");

    IReadOnlyDictionary<string, string> priorCache = SnapshotDirectory(cacheDirectory);
    await ExpectInvalidDataAsync(
        async () =>
        {
            _ = await PortableEditorCacheBuilder.BuildTerrainTexturePreviewCacheFromSourceAsync(
                labPaths.LockedBaseImagePath,
                overlayPath,
                cacheWorkspaceRoot,
                level,
                overwrite: true,
                expectedSourceImageSha256: wrongSourceSha256,
                expectedSourceOverlaySha256: sourceOverlaySha256);
        },
        "wrong expected texture-cache source");
    await ExpectInvalidDataAsync(
        async () =>
        {
            _ = await PortableEditorCacheBuilder.BuildTerrainTexturePreviewCacheFromSourceAsync(
                labPaths.LockedBaseImagePath,
                overlayPath,
                cacheWorkspaceRoot,
                level,
                overwrite: true,
                expectedSourceImageSha256: sourceImageSha256,
                expectedSourceOverlaySha256: wrongOverlaySha256);
        },
        "wrong expected texture-cache overlay");
    RequireSnapshot(cacheDirectory, priorCache);

    bool postPublicationFailureObserved = false;
    try
    {
        _ = await PortableEditorCacheBuilder.BuildTerrainTexturePreviewCacheFromSourceAsync(
            labPaths.LockedBaseImagePath,
            overlayPath,
            cacheWorkspaceRoot,
            level,
            overwrite: true,
            expectedSourceImageSha256: sourceImageSha256,
            expectedSourceOverlaySha256: sourceOverlaySha256,
            testStageHook: stage =>
            {
                if (stage == "after-candidate-publication")
                    throw new IOException("Injected source-bound texture-cache publication failure.");
            });
    }
    catch (IOException ex) when (
        ex.Message.Contains("Injected source-bound texture-cache publication failure", StringComparison.Ordinal))
    {
        postPublicationFailureObserved = true;
    }
    Require(postPublicationFailureObserved, "The source-bound cache did not reach its injected post-publication failure.");
    RequireSnapshot(cacheDirectory, priorCache);
    Require(
        !Directory.Exists(operationsRoot) || !Directory.EnumerateFileSystemEntries(operationsRoot).Any(),
        "The source-bound cache rollback retained operation or backup debris.");
    Require(
        PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCacheFromSource(
            cacheWorkspaceRoot,
            level,
            sourceImageSha256,
            sourceOverlaySha256,
            sourceImageInfo.Length,
            sourceOverlayInfo.Length,
            expectedTextureCount: 66),
        "The injected publication failure did not restore the exact prior source-bound cache.");
    Require(
        await HashFileAsync(labPaths.LockedBaseImagePath) == sourceImageSha256 &&
        await HashFileAsync(overlayPath) == sourceOverlaySha256,
        "The source-bound cache build or rollback changed its source image or overlay.");
}

string CreateOwnedStaleOperation(string operationsRoot, string prefix)
{
    Directory.CreateDirectory(operationsRoot);
    string operation = Path.Combine(operationsRoot, prefix + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(operation);
    File.WriteAllText(Path.Combine(operation, "orphan.txt"), "strictly-owned stale smoke fixture\n");
    return operation;
}

void VerifyModelOnlyConstructionPlansPreserveSiblingSubfiles(
    string baselineImagePath,
    UnusedLevel65FullAuthoringConstructionContract contract,
    params byte[][] afterModels)
{
    DiscLayout layout = DiscImage.DetectLayout(baselineImagePath);
    using FileStream image = File.OpenRead(baselineImagePath);
    DiscFileRecord wad = DiscImage.FindRootFileRecord(
        image,
        layout,
        name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
    Require(wad.Lba == contract.WadLba, "The model-only preservation proof resolved an unexpected WAD extent.");
    byte[] baselineEntry = DiscImage.ReadFileBytes(
        image,
        layout,
        wad.Lba,
        contract.DataEntryWadOffset,
        contract.DataEntryByteLength);
    UnusedLevel65ConstructionDataSubfileLayout modelSubfile =
        contract.DataSubfiles.Single(subfile => subfile.Index == 1);
    Require(
        modelSubfile.RelativeOffset == contract.ModelSubfileWadOffset - contract.DataEntryWadOffset &&
        modelSubfile.ByteLength == contract.ModelSubfileByteLength,
        "The construction model plan no longer maps exactly to row-80 subfile 1.");

    foreach (byte[] afterModel in afterModels)
    {
        Require(
            afterModel.Length == modelSubfile.ByteLength,
            "A construction planner returned bytes outside the exact model-subfile length.");
        byte[] composedEntry = baselineEntry.ToArray();
        afterModel.AsSpan().CopyTo(composedEntry.AsSpan(modelSubfile.RelativeOffset, modelSubfile.ByteLength));
        foreach (UnusedLevel65ConstructionDataSubfileLayout preserved in
                 contract.DataSubfiles.Where(subfile => subfile.Index != modelSubfile.Index))
        {
            ReadOnlySpan<byte> baselineSubfile = baselineEntry.AsSpan(preserved.RelativeOffset, preserved.ByteLength);
            ReadOnlySpan<byte> composedSubfile = composedEntry.AsSpan(preserved.RelativeOffset, preserved.ByteLength);
            Require(
                preserved.MustRemainByteIdenticalInFirstTerrainGate &&
                baselineSubfile.SequenceEqual(composedSubfile) &&
                HashBytes(composedSubfile) == preserved.Sha256,
                $"The model-only construction plan changed preserved row-80 subfile {preserved.Index}.");
        }
    }
}

string[] SnapshotArtifactPaths(string root) =>
    Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .Select(path =>
            $"{(Directory.Exists(path) ? "D" : "F")}:{Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/')}")
        .Order(StringComparer.Ordinal)
        .ToArray();

IReadOnlyDictionary<string, string> SnapshotDirectory(string root)
{
    return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(
            path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
            HashFile,
            StringComparer.Ordinal);
}

void RequireSnapshot(string root, IReadOnlyDictionary<string, string> expected)
{
    IReadOnlyDictionary<string, string> actual = SnapshotDirectory(root);
    Require(actual.Count == expected.Count, "Replacement rollback changed the prior candidate file count.");
    foreach ((string relativePath, string hash) in expected)
    {
        Require(
            actual.TryGetValue(relativePath, out string? actualHash) && actualHash == hash,
            $"Replacement rollback did not restore {relativePath} byte-for-byte.");
    }
}

bool FacePoint(TerrainPolygon face, int index, int x, int y, int z) =>
    Math.Abs(face.Points[index].X - x) < 0.001f &&
    Math.Abs(face.Points[index].Y - y) < 0.001f &&
    Math.Abs(face.ZValues[index] - z) < 0.001f;

HashSet<int> ExtractCollisionTriangleIndexes(IEnumerable<TerrainPatch> patches)
{
    HashSet<int> indexes = new();
    foreach (TerrainPatch patch in patches)
    {
        Match match = Regex.Match(patch.Label + " " + patch.Description, @"triangle\s+(\d+)", RegexOptions.IgnoreCase);
        Require(match.Success, $"Cannot parse collision triangle index from {patch.Label}: {patch.Description}");
        indexes.Add(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    return indexes;
}

long ParseHexOffset(string value)
{
    string text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
    return long.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

void ExpectInvalidData(Action action, string label)
{
    try
    {
        action();
        throw new InvalidOperationException($"The {label} guard did not reject the input.");
    }
    catch (InvalidDataException)
    {
    }
}

async Task ExpectInvalidDataAsync(Func<Task> action, string label)
{
    try
    {
        await action();
        throw new InvalidOperationException($"The {label} guard did not reject the input.");
    }
    catch (InvalidDataException)
    {
    }
}

void ValidateCuePair(string cuePath, string imagePath)
{
    Require(File.Exists(cuePath) && File.Exists(imagePath), "The expected BIN/CUE pair is incomplete.");
    string cueText = File.ReadAllText(cuePath);
    Require(cueText.Contains("MODE2/2352", StringComparison.OrdinalIgnoreCase), "The CUE is not MODE2/2352.");
    Require(
        cueText.Contains($"\"{Path.GetFileName(imagePath)}\"", StringComparison.Ordinal),
        "The CUE does not reference its paired BIN filename.");
    Require(new FileInfo(imagePath).Length % 2352 == 0, "The BIN length is not a whole number of raw 2352-byte sectors.");
}

string FindRepositoryRoot(string? requestedRoot)
{
    string start = string.IsNullOrWhiteSpace(requestedRoot)
        ? Directory.GetCurrentDirectory()
        : Path.GetFullPath(requestedRoot);
    for (DirectoryInfo? current = new(start); current != null; current = current.Parent)
    {
        if (File.Exists(Path.Combine(current.FullName, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
        {
            return current.FullName;
        }
    }

    throw new DirectoryNotFoundException($"Could not locate the Spyro Editor repository from {start}.");
}

string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

string HashBytes(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    byte[] hash = await SHA256.HashDataAsync(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

bool PathsEqual(string left, string right) =>
    string.Equals(
        Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);

void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
