using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65NativeTextureDonorManifest(
    string ManifestId,
    string SourceImageSha256,
    string DonorLevelKey,
    string DonorLevelName,
    int DonorWadEntry,
    int DonorTextureId,
    int DonorTextureCount,
    long TexturePagesWadOffset,
    int TexturePagesByteLength,
    string TexturePagesSha256,
    long TextureComponentWadOffset,
    int TextureComponentByteLength,
    string TextureComponentSha256,
    long LowDetailRowWadOffset,
    string LowDetailRowHex,
    string LowDetailRowSha256,
    long HighDetailRowWadOffset,
    string HighDetailRowHex,
    string HighDetailRowSha256,
    string CompleteRecordSha256,
    int LowDetailDescriptorCount,
    int NormalHighDetailDescriptorCount,
    int CloseHighDetailDescriptorCount,
    int UniquePhysicalPixelAndClutBytes,
    IReadOnlyDictionary<int, int> HighDetailSideHistogram,
    int ReferencingHighDetailFaceCount,
    string RepresentativeHighDetailFace,
    string RuntimeSceneSha256,
    bool RuntimeControlAuditComplete,
    bool RuntimeControlled,
    bool AnimationSource,
    bool CompleteNativeGrammar,
    bool SourceOwnedImmutable);

internal sealed record UnusedLevel65NativeTextureFaceBinding(
    int SectorIndex,
    int SourceHighDetailFaceIndex,
    int SourceLowDetailFaceIndex,
    long SourceHighDetailFaceWadOffset,
    long OutputHighDetailFaceWadOffset,
    long SourceHighDetailTextureWordWadOffset,
    long OutputHighDetailTextureWordWadOffset,
    string SourceHighDetailFaceHex,
    string OutputHighDetailFaceHex,
    int SourceTextureId,
    int OutputTextureId,
    int SourceTextureLinkedHighDetailFaceCount,
    int OutputTextureLinkedHighDetailFaceCount,
    long SourceLowDetailFaceWadOffset,
    long OutputLowDetailFaceWadOffset,
    string SourceLowDetailFaceHex,
    string OutputLowDetailFaceHex,
    bool HighDetailTextureIdOnlyChanged,
    bool LowDetailFacePreserved,
    bool LowDetailGeometryIsColorOnly,
    bool CompleteTextureRecordCarriesNativeLqAndHqTiers);

internal sealed record UnusedLevel65NativeTextureComponentRelocation(
    string Name,
    int SourceRelativeOffset,
    int OutputRelativeOffset,
    int ByteLength,
    string SourceSha256,
    string OutputSha256,
    bool ContentsPreserved);

internal sealed record UnusedLevel65NativeTextureDiffRange(
    long WadOffset,
    int ByteLength,
    string BeforeSha256,
    string AfterSha256,
    string Owner);

internal sealed record UnusedLevel65NativeTextureStaticPlan(
    string ProfileId,
    string SourceImagePath,
    string SourceImageSha256,
    int TargetWadEntry,
    long TargetDataWadOffset,
    int TargetDataByteLength,
    long TargetTexturePagesWadOffset,
    int TargetTexturePagesByteLength,
    string SourceTexturePagesSha256,
    string OutputTexturePagesSha256,
    long TargetModelWadOffset,
    int TargetModelByteLength,
    string SourceModelSha256,
    string OutputModelSha256,
    string SourceDataSha256,
    string OutputDataSha256,
    int SourceTextureCount,
    int OutputTextureCount,
    int PrivateTextureId,
    int MaterialTemplateTextureId,
    int RecordGrowthByteCount,
    int SourceTextureComponentByteLength,
    int OutputTextureComponentByteLength,
    int SourceUsedModelByteLength,
    int OutputUsedModelByteLength,
    int SourceZeroTailByteCount,
    int OutputZeroTailByteCount,
    long OutputLowDetailRowWadOffset,
    long OutputHighDetailRowWadOffset,
    string OutputPrivateLowDetailRowSha256,
    string OutputPrivateHighDetailRowSha256,
    UnusedLevel65NativeTextureDonorManifest Donor,
    UnusedLevel65NativeTextureFaceBinding FaceBinding,
    IReadOnlyList<UnusedLevel65NativeTextureComponentRelocation> RelocatedComponents,
    IReadOnlyList<UnusedLevel65NativeTextureDiffRange> DiffRanges,
    int ChangedTexturePageByteCount,
    int ChangedModelByteCount,
    int ChangedDataByteCount,
    string DiffManifestSha256,
    string DeterministicPlanSha256,
    NativeTerrainTextureFixedTailPrivateRecordPlan Composition,
    bool DestinationOwnershipClosureComplete,
    bool DestinationRuntimeControlAuditComplete,
    bool PrivateRecordRuntimePersistent,
    bool DonorDependencyClosureVerified,
    bool PageAndClutRelocationVerified,
    bool PrivatePerFaceStorageVerified,
    bool HpLpPairingVerified,
    bool CollisionAndOcclusionPreserved,
    bool TargetEntryHeaderPreserved,
    bool ProtectedSubfilesPreserved,
    bool RetailWadEntriesExcluded,
    bool ExecutableExcluded,
    bool DeterministicReadbackRequired,
    bool DisposableRuntimeCandidateAuthorized,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

/// <summary>
/// First destination-owned native-texture contract for the ID65 authoring
/// substrate. It consumes the exact foundation BIN, imports one complete static
/// Gnasty's World record into private target T66, and redirects only the newly
/// authored sector-213 HP face. It returns source-bound patches but never writes
/// an image, CUE, project manifest, normal Create BIN state, or release state.
/// </summary>
internal static class UnusedLevel65NativeTextureAuthoringContract
{
    public const string ProfileId =
        "unused-level-65-native-texture-authoring-gnastys-world-t12-private-t66-static-v1";
    public const string DonorManifestId =
        "clean-usa-foundation-image:gnastysworld:wad70:t12:runtime-static-complete-record-v1";
    public const string ExpectedFoundationImageSha256 =
        "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
    public const int TargetWadEntry = 80;
    public const int DonorWadEntry = 70;
    public const int DonorTextureId = 12;
    public const int SourceTextureCount = 66;
    public const int PrivateTextureId = 66;
    public const int MaterialTemplateTextureId = 25;
    public const int TargetSectorIndex = 213;
    public const int TargetHighDetailFaceIndex = 113;
    public const int TargetLowDetailFaceIndex = 21;
    public const int RecordGrowthByteCount = 184;
    public const int TargetDataByteLength = 0x2E2000;
    public const int TargetTexturePagesByteLength = 0xDE000;
    public const int TargetModelByteLength = 0x94800;
    public const int ExpectedSourceTextureComponentByteLength = 0x2F78;
    public const int ExpectedOutputTextureComponentByteLength = 0x3030;
    public const int ExpectedSourceZeroTailByteCount = 0x2C8;
    public const int ExpectedOutputZeroTailByteCount = 0x210;
    public const int ExpectedDonorUniquePhysicalByteCount = 0xA00;
    public const string StablePrivateEditId =
        "id65-private-t66-gnastysworld-t12-material-t25-v1";
    public const string ExpectedDonorTexturePagesSha256 =
        "787ff2615d815c01b08c099894cd0cb96fd4ff3263bfeedbe11f3f6d53c86e40";
    public const string ExpectedDonorTextureComponentSha256 =
        "ceda4db243e0dbed3443788ff81c4fb14c965ad8ececb10a28fe9bf9e7379903";
    public const string ExpectedDonorLowDetailRowSha256 =
        "2b2125d135b8840e1092a3f8b8c6c5cdc9faf1d87741d5140e7d6a52c075cf70";
    public const string ExpectedDonorHighDetailRowSha256 =
        "cda34d1e3b49fdd2e70318ec0d299f8bd55a4c642e7b0cb8a50dca4b0058eb25";
    public const string ExpectedDonorCompleteRecordSha256 =
        "a96f9658fc7ab179c98e292220e630a49b8417a2d4f4243756fdb566ca367fe3";
    public const string ExpectedDonorRuntimeSceneSha256 =
        "2eb5fea15d2440a8ffc03df792dcec73cde22a8b241eb4dc77ab4d5bea4460fa";
    public const string ExpectedSourceTexturePagesSha256 =
        "5fb81c4ac63eb235a172c4f16f83ef08efb148a6f543ef6100ba21359ade408f";
    public const string ExpectedOutputTexturePagesSha256 =
        "34a6d81c740c1e2494ede138e45556941e6daf59d21668ca2b594055f61ac47a";
    public const string ExpectedSourceModelSha256 =
        "ccd18568b9b6cb7a41d2bf8a47c7dc475ca2cb1f9f127ac9a90ef9ac0a8be4f1";
    public const string ExpectedOutputModelSha256 =
        "2c0cbdc33a902730760a957847e82be09d391fe2cc4bb7821c651bfb7ebf3d75";
    public const string ExpectedSourceDataSha256 =
        "eb5ca8459300392de12246e57770a57447f7bd64ccf3e978137d2362d7694160";
    public const string ExpectedOutputDataSha256 =
        "de2e712372cc84207724ca4405776a0b65bd53a548adac3cc0c1f99e36aad9e9";
    public const string ExpectedPrivateLowDetailRowSha256 =
        "c32b270ab41d9e9299cca05a4a0d14f0addfa32a108c5aa8c4f509eef3b98d80";
    public const string ExpectedPrivateHighDetailRowSha256 =
        "d63d613b56b7dcd5adf506847bab76fb9aa69e3ebb9d00643c76aec929557fcc";
    public const string ExpectedDiffManifestSha256 =
        "b42f7efdea04a2f6a3233b7ca657a247d2836292187ba78153872bee16abd068";
    public const string ExpectedDeterministicPlanSha256 =
        "3557d1ee1b5874b764e0ce9742dd04b1281c6f5d762f59fce09f9a3d3da0315b";
    public const int ExpectedChangedTexturePageByteCount = 402_194;
    public const int ExpectedChangedModelByteCount = 517_731;
    public const int ExpectedChangedDataByteCount = 919_925;
    public const int ExpectedDiffRangeCount = 55_255;
    public const int ExpectedTexturePagePatchCount = 7_949;

    public static UnusedLevel65NativeTextureStaticPlan BuildFirstStaticPlan(
        string foundationImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(foundationImagePath);
        string sourcePath = Path.GetFullPath(foundationImagePath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The exact ID65 foundation BIN is missing.", sourcePath);
        string sourceImageSha256 = HashFile(sourcePath);
        RequireHash(sourceImageSha256, ExpectedFoundationImageSha256, "foundation BIN");

        LevelDefinition targetLevel = UnusedLevel65BlankLevelLabProfileRegistry.Definition;
        LevelDefinition donorLevel = new()
        {
            Key = "gnastysworld",
            ScriptKey = "gnastysworld",
            DisplayName = "Gnasty's World",
            LevelId = 60,
            SourceWadEntry = DonorWadEntry
        };

        LevelAsset targetAsset = ReadLevelAsset(sourcePath, TargetWadEntry);
        LevelAsset donorAsset = ReadLevelAsset(sourcePath, DonorWadEntry);
        RequireTargetAsset(targetAsset);

        NativeTerrainTextureRecordAppendSourceBinding binding =
            NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourcePath, targetLevel);
        if (binding.TargetWadEntry != TargetWadEntry ||
            binding.ExpectedSourceTextureCount != SourceTextureCount ||
            !HashEquals(binding.SourceImageSha256, ExpectedFoundationImageSha256) ||
            !HashEquals(binding.ExpectedTextureComponentSha256, Hash(targetAsset.Model.AsSpan(0, ExpectedSourceTextureComponentByteLength))) ||
            !HashEquals(binding.ExpectedLevelDataSha256, Hash(targetAsset.Model)))
        {
            throw new InvalidDataException("The ID65 private-texture source binding changed.");
        }

        ParsedModel sourceModel = ParseModel(targetAsset.Model, targetAsset.ModelWadOffset);
        RequireFoundationModel(sourceModel);
        ParsedSector sourceSector = sourceModel.Sectors[TargetSectorIndex];
        FaceLocation sourceFaces = LocateAuthoredFaces(
            targetAsset.Model,
            targetAsset.ModelWadOffset,
            sourceSector,
            MaterialTemplateTextureId);

        NativeTerrainTextureRuntimeControlAudit targetRuntime =
            NativeTerrainTextureRuntimeControlScanner.Inspect(sourcePath, targetLevel);
        if (!targetRuntime.Complete || targetRuntime.TextureCount != SourceTextureCount ||
            !targetRuntime.IsRuntimePersistentTarget(MaterialTemplateTextureId) ||
            targetRuntime.Controls.Any(control => control.TextureId >= SourceTextureCount))
        {
            throw new InvalidDataException(
                $"The ID65 runtime texture-control closure changed: {targetRuntime.SafetyBlockers.FirstOrDefault()}");
        }
        NativeTexturePageOwnershipReport targetOwnership =
            NativeTexturePageOwnershipScanner.Scan(sourcePath, targetLevel);
        if (!targetOwnership.AllConsumerClosureComplete ||
            !targetOwnership.ProvablyPrivatePixelAndClutSpace ||
            targetOwnership.WadEntry != TargetWadEntry ||
            targetOwnership.TexturePagesSubfileByteLength != TargetTexturePagesByteLength ||
            !HashEquals(targetOwnership.TexturePagesSubfileSha256, Hash(targetAsset.TexturePages)))
        {
            throw new InvalidDataException(
                $"The ID65 texture-page ownership closure changed: {targetOwnership.SafetyBlockers.FirstOrDefault()}");
        }

        UnusedLevel65NativeTextureDonorManifest donor = BuildDonorManifest(
            sourcePath,
            sourceImageSha256,
            donorLevel,
            donorAsset);

        uint sourceFaceWord = BinaryPrimitives.ReadUInt32LittleEndian(sourceFaces.HighDetailFace.AsSpan(8, 4));
        if ((sourceFaceWord & 0x7Fu) != MaterialTemplateTextureId)
            throw new InvalidDataException("The authored ID65 HP face no longer uses native material template T25.");
        uint outputFaceWord = (sourceFaceWord & 0xFFFFFF80u) | PrivateTextureId;
        byte[] outputFaceWordBytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(outputFaceWordBytes, outputFaceWord);
        NativeTerrainTextureRecordExistingPatch facePatch = new(
            sourceFaces.HighDetailFaceWadOffset + 8,
            sourceFaces.HighDetailFace.AsSpan(8, 4).ToArray(),
            outputFaceWordBytes,
            "id65-private-texture-face-id",
            "213:113:hp");

        NativeTerrainTextureFixedTailPrivateRecordRequest request = new(
            sourcePath,
            targetLevel,
            binding,
            ExistingRecordOverrides: Array.Empty<NativeTerrainTextureRelocationImport>(),
            SyntheticRecords:
            [
                new NativeTerrainTexturePrivateSyntheticRecord(
                    StablePrivateEditId,
                    PrivateTextureId,
                    donorLevel.Key,
                    DonorWadEntry,
                    DonorTextureId,
                    MaterialTemplateTextureId)
            ],
            OrdinaryLevelDataPatches: [facePatch]);
        NativeTerrainTextureFixedTailPrivateRecordPlan composition =
            NativeTerrainTextureFixedTailPrivateRecordComposer.BuildPlan(request);
        RequireComposition(composition, binding, donor, facePatch);

        byte[] outputModel = composition.Append.Patch.After;
        ParsedModel parsedOutput = ParseModel(outputModel, targetAsset.ModelWadOffset);
        FaceLocation outputFaces = LocateAuthoredFaces(
            outputModel,
            targetAsset.ModelWadOffset,
            parsedOutput.Sectors[TargetSectorIndex],
            PrivateTextureId);
        uint readbackFaceWord = BinaryPrimitives.ReadUInt32LittleEndian(outputFaces.HighDetailFace.AsSpan(8, 4));
        if ((readbackFaceWord & 0x7Fu) != PrivateTextureId ||
            (readbackFaceWord & 0xFFFFFF80u) != (sourceFaceWord & 0xFFFFFF80u) ||
            !sourceFaces.LowDetailFace.SequenceEqual(outputFaces.LowDetailFace))
        {
            throw new InvalidDataException("The ID65 private face or paired LP face failed exact composed readback.");
        }
        int outputPrivateUseCount = CountHighDetailTextureUses(outputModel, parsedOutput, PrivateTextureId);
        int sourceTemplateUseCount = CountHighDetailTextureUses(targetAsset.Model, sourceModel, MaterialTemplateTextureId);
        if (outputPrivateUseCount != 1 || sourceTemplateUseCount < 1)
            throw new InvalidDataException("The ID65 private T66 face scope is not exactly one authored HP face.");

        NativeTerrainTextureGlobalRepackPackedRecord packedPrivate =
            composition.GlobalPacking.SyntheticRecords.Single(record => record.TargetTextureId == PrivateTextureId);
        NativeTerrainTextureRecordAppendResolvedRecord resolvedPrivate =
            composition.Append.ResolvedRecords.Single(record => record.AssignedTextureId == PrivateTextureId);
        long outputLowRowWadOffset = targetAsset.ModelWadOffset + 8 + (PrivateTextureId * 16L);
        long outputHighRowWadOffset = targetAsset.ModelWadOffset + 8 +
                                      (composition.Append.OutputTextureCount * 16L) +
                                      (PrivateTextureId * 168L);
        if (outputLowRowWadOffset != targetAsset.ModelWadOffset + 0x428 ||
            outputHighRowWadOffset != targetAsset.ModelWadOffset + 0x2F88 ||
            !HashEquals(resolvedPrivate.LowDetailSha256, packedPrivate.LowDetailRowSha256) ||
            !HashEquals(resolvedPrivate.HighDetailSha256, packedPrivate.HighDetailRowSha256))
        {
            throw new InvalidDataException("The private T66 output row offsets or hashes changed.");
        }

        byte[] outputPages = targetAsset.TexturePages.ToArray();
        foreach (NativeTerrainTextureRelocationPatch patch in composition.GlobalPacking.TexturePagePatches)
        {
            int relative = checked((int)(patch.WadOffset - targetAsset.TexturePagesWadOffset));
            if (relative < 0 || relative + patch.ByteLength > outputPages.Length ||
                !outputPages.AsSpan(relative, patch.ByteLength).SequenceEqual(patch.Before))
            {
                throw new InvalidDataException("A packed ID65 texture-page patch escaped its exact target-owned subfile preimage.");
            }
            patch.After.CopyTo(outputPages, relative);
        }

        byte[] outputData = targetAsset.Data.ToArray();
        foreach (NativeTerrainTexturePrivateStructuralPatch patch in composition.CombinedPatches)
        {
            int relative = checked((int)(patch.WadOffset - targetAsset.DataWadOffset));
            if (relative < 0 || relative + patch.ByteLength > outputData.Length ||
                !outputData.AsSpan(relative, patch.ByteLength).SequenceEqual(patch.Before))
            {
                throw new InvalidDataException("An ID65 texture-authoring patch escaped row80 or lost its exact preimage.");
            }
            patch.After.CopyTo(outputData, relative);
        }

        UnusedLevel65NativeTextureDiffRange[] diffRanges = BuildDiffRanges(composition.CombinedPatches);
        RequireDiffAllowlist(diffRanges, targetAsset);
        int changedTexturePageBytes = CountChangedBytes(targetAsset.TexturePages, outputPages);
        int changedModelBytes = CountChangedBytes(targetAsset.Model, outputModel);
        int changedDataBytes = CountChangedBytes(targetAsset.Data, outputData);
        if (changedDataBytes != changedTexturePageBytes + changedModelBytes)
            throw new InvalidDataException("The ID65 native-texture data diff escaped the exact page/model union.");

        List<UnusedLevel65NativeTextureComponentRelocation> relocationRows = composition.Append.ShiftedComponents
            .Select(component => new UnusedLevel65NativeTextureComponentRelocation(
                component.Name,
                component.SourceOffset,
                component.OutputOffset,
                component.ByteLength,
                NormalizeHash(component.ComposedSourceSha256),
                NormalizeHash(component.OutputSha256),
                HashEquals(component.ComposedSourceSha256, component.OutputSha256)))
            .ToList();
        ParsedComponent sourcePortal = sourceModel.Components.Single(component => component.Name == "portal table");
        ParsedComponent outputPortal = parsedOutput.Components.Single(component => component.Name == "portal table");
        int sourcePortalRelativeOffset = checked((int)(sourcePortal.WadOffset - targetAsset.ModelWadOffset));
        int outputPortalRelativeOffset = checked((int)(outputPortal.WadOffset - targetAsset.ModelWadOffset));
        relocationRows.Add(new UnusedLevel65NativeTextureComponentRelocation(
            "portal table",
            sourcePortalRelativeOffset,
            outputPortalRelativeOffset,
            sourcePortal.ByteLength,
            sourcePortal.Sha256,
            outputPortal.Sha256,
            outputPortalRelativeOffset == sourcePortalRelativeOffset + RecordGrowthByteCount &&
            sourcePortal.ByteLength == outputPortal.ByteLength &&
            HashEquals(sourcePortal.Sha256, outputPortal.Sha256)));
        UnusedLevel65NativeTextureComponentRelocation[] relocations = relocationRows
            .OrderBy(component => component.SourceRelativeOffset)
            .ToArray();
        string diffManifestSha256 = HashDiffManifest(diffRanges);
        string sourceModelSha256 = Hash(targetAsset.Model);
        string outputModelSha256 = Hash(outputModel);
        string sourceDataSha256 = Hash(targetAsset.Data);
        string outputDataSha256 = Hash(outputData);
        string outputPagesSha256 = Hash(outputPages);
        string deterministicPlanSha256 = HashPlanIdentity(
            sourceImageSha256,
            donor,
            sourceModelSha256,
            outputModelSha256,
            targetOwnership.TexturePagesSubfileSha256,
            outputPagesSha256,
            sourceDataSha256,
            outputDataSha256,
            packedPrivate.LowDetailRowSha256,
            packedPrivate.HighDetailRowSha256,
            diffManifestSha256,
            sourceFaces,
            outputFaces,
            composition.Append);

        UnusedLevel65NativeTextureFaceBinding faceBinding = new(
            TargetSectorIndex,
            TargetHighDetailFaceIndex,
            TargetLowDetailFaceIndex,
            sourceFaces.HighDetailFaceWadOffset,
            outputFaces.HighDetailFaceWadOffset,
            sourceFaces.HighDetailFaceWadOffset + 8,
            outputFaces.HighDetailFaceWadOffset + 8,
            Convert.ToHexString(sourceFaces.HighDetailFace),
            Convert.ToHexString(outputFaces.HighDetailFace),
            MaterialTemplateTextureId,
            PrivateTextureId,
            sourceTemplateUseCount,
            outputPrivateUseCount,
            sourceFaces.LowDetailFaceWadOffset,
            outputFaces.LowDetailFaceWadOffset,
            Convert.ToHexString(sourceFaces.LowDetailFace),
            Convert.ToHexString(outputFaces.LowDetailFace),
            HighDetailTextureIdOnlyChanged: DifferentByteIndexes(
                sourceFaces.HighDetailFace,
                outputFaces.HighDetailFace).SequenceEqual([8]),
            LowDetailFacePreserved: sourceFaces.LowDetailFace.SequenceEqual(outputFaces.LowDetailFace),
            LowDetailGeometryIsColorOnly: true,
            CompleteTextureRecordCarriesNativeLqAndHqTiers: true);
        if (!faceBinding.HighDetailTextureIdOnlyChanged || !faceBinding.LowDetailFacePreserved)
            throw new InvalidDataException("The first private texture did not remain face-local across the HP/LP construction pair.");

        bool collisionAndOcclusionPreserved = relocations
            .Where(component => component.Name is "occlusion" or "special surface" or "collision")
            .All(component => component.ContentsPreserved) &&
            relocations.Count(component => component.Name is "occlusion" or "special surface" or "collision") == 3;
        bool targetEntryHeaderPreserved = targetAsset.Data.AsSpan(0, 0x800)
            .SequenceEqual(outputData.AsSpan(0, 0x800));
        bool protectedSubfilesPreserved = TargetProtectedSubfilesPreserved(targetAsset.Data, outputData);
        bool retailWadEntriesExcluded = diffRanges.All(range =>
            range.WadOffset >= targetAsset.DataWadOffset &&
            range.WadOffset + range.ByteLength <= targetAsset.DataWadOffset + targetAsset.Data.Length);
        bool executableExcluded = retailWadEntriesExcluded;
        if (!collisionAndOcclusionPreserved || !targetEntryHeaderPreserved ||
            !protectedSubfilesPreserved || !retailWadEntriesExcluded || !executableExcluded)
            throw new InvalidDataException("The ID65 native-texture plan changed protected terrain semantics or row80 subfiles.");

        RequirePinnedFirstPlan(
            donor,
            faceBinding,
            outputLowRowWadOffset,
            outputHighRowWadOffset,
            packedPrivate,
            sourceModelSha256,
            outputModelSha256,
            targetOwnership.TexturePagesSubfileSha256,
            outputPagesSha256,
            sourceDataSha256,
            outputDataSha256,
            changedTexturePageBytes,
            changedModelBytes,
            changedDataBytes,
            diffRanges.Length,
            diffManifestSha256,
            deterministicPlanSha256,
            composition);

        return new UnusedLevel65NativeTextureStaticPlan(
            ProfileId,
            sourcePath,
            sourceImageSha256,
            TargetWadEntry,
            targetAsset.DataWadOffset,
            targetAsset.Data.Length,
            targetAsset.TexturePagesWadOffset,
            targetAsset.TexturePages.Length,
            Hash(targetAsset.TexturePages),
            outputPagesSha256,
            targetAsset.ModelWadOffset,
            targetAsset.Model.Length,
            sourceModelSha256,
            outputModelSha256,
            sourceDataSha256,
            outputDataSha256,
            composition.Append.SourceTextureCount,
            composition.Append.OutputTextureCount,
            PrivateTextureId,
            MaterialTemplateTextureId,
            composition.Append.GrowthByteCount,
            composition.Append.SourceTextureComponentByteLength,
            composition.Append.OutputTextureComponentByteLength,
            composition.Append.SourceUsedByteLength,
            composition.Append.OutputUsedByteLength,
            composition.Append.SourceZeroTailByteCount,
            composition.Append.OutputZeroTailByteCount,
            outputLowRowWadOffset,
            outputHighRowWadOffset,
            NormalizeHash(packedPrivate.LowDetailRowSha256),
            NormalizeHash(packedPrivate.HighDetailRowSha256),
            donor,
            faceBinding,
            relocations,
            diffRanges,
            changedTexturePageBytes,
            changedModelBytes,
            changedDataBytes,
            diffManifestSha256,
            deterministicPlanSha256,
            composition,
            DestinationOwnershipClosureComplete: true,
            DestinationRuntimeControlAuditComplete: true,
            PrivateRecordRuntimePersistent: true,
            DonorDependencyClosureVerified: true,
            PageAndClutRelocationVerified: true,
            PrivatePerFaceStorageVerified: true,
            HpLpPairingVerified: true,
            CollisionAndOcclusionPreserved: true,
            TargetEntryHeaderPreserved: true,
            ProtectedSubfilesPreserved: true,
            RetailWadEntriesExcluded: true,
            ExecutableExcluded: true,
            DeterministicReadbackRequired: true,
            DisposableRuntimeCandidateAuthorized: false,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);
    }

    private static void RequirePinnedFirstPlan(
        UnusedLevel65NativeTextureDonorManifest donor,
        UnusedLevel65NativeTextureFaceBinding face,
        long outputLowRowWadOffset,
        long outputHighRowWadOffset,
        NativeTerrainTextureGlobalRepackPackedRecord packedPrivate,
        string sourceModelSha256,
        string outputModelSha256,
        string sourcePagesSha256,
        string outputPagesSha256,
        string sourceDataSha256,
        string outputDataSha256,
        int changedPageBytes,
        int changedModelBytes,
        int changedDataBytes,
        int diffRangeCount,
        string diffManifestSha256,
        string deterministicPlanSha256,
        NativeTerrainTextureFixedTailPrivateRecordPlan composition)
    {
        if (donor.TexturePagesWadOffset != 0x51EF000 ||
            donor.TexturePagesByteLength != 0xBB800 ||
            donor.TextureComponentWadOffset != 0x52AA800 ||
            donor.TextureComponentByteLength != 0x1708 ||
            donor.LowDetailRowWadOffset != 0x52AA8C8 ||
            donor.HighDetailRowWadOffset != 0x52AB1E8 ||
            donor.ReferencingHighDetailFaceCount != 73 ||
            donor.RepresentativeHighDetailFace != "0:15:hp" ||
            !HashEquals(donor.TexturePagesSha256, ExpectedDonorTexturePagesSha256) ||
            !HashEquals(donor.TextureComponentSha256, ExpectedDonorTextureComponentSha256) ||
            !HashEquals(donor.LowDetailRowSha256, ExpectedDonorLowDetailRowSha256) ||
            !HashEquals(donor.HighDetailRowSha256, ExpectedDonorHighDetailRowSha256) ||
            !HashEquals(donor.CompleteRecordSha256, ExpectedDonorCompleteRecordSha256) ||
            !HashEquals(donor.RuntimeSceneSha256, ExpectedDonorRuntimeSceneSha256) ||
            face.SourceHighDetailFaceWadOffset != 0x6A3F9EC ||
            face.OutputHighDetailFaceWadOffset != 0x6A3FAA4 ||
            face.SourceLowDetailFaceWadOffset != 0x6A3EAD0 ||
            face.OutputLowDetailFaceWadOffset != 0x6A3EB88 ||
            face.SourceTextureLinkedHighDetailFaceCount != 63 ||
            face.OutputTextureLinkedHighDetailFaceCount != 1 ||
            face.SourceHighDetailFaceHex != "8C8C8D8E0101000219024001080A1000" ||
            face.OutputHighDetailFaceHex != "8C8C8D8E0101000242024001080A1000" ||
            face.SourceLowDetailFaceHex != "0065498E00821000" ||
            face.OutputLowDetailFaceHex != "0065498E00821000" ||
            outputLowRowWadOffset != 0x6A15428 ||
            outputHighRowWadOffset != 0x6A17F88 ||
            !HashEquals(packedPrivate.LowDetailRowSha256, ExpectedPrivateLowDetailRowSha256) ||
            !HashEquals(packedPrivate.HighDetailRowSha256, ExpectedPrivateHighDetailRowSha256) ||
            !HashEquals(sourceModelSha256, ExpectedSourceModelSha256) ||
            !HashEquals(outputModelSha256, ExpectedOutputModelSha256) ||
            !HashEquals(sourcePagesSha256, ExpectedSourceTexturePagesSha256) ||
            !HashEquals(outputPagesSha256, ExpectedOutputTexturePagesSha256) ||
            !HashEquals(sourceDataSha256, ExpectedSourceDataSha256) ||
            !HashEquals(outputDataSha256, ExpectedOutputDataSha256) ||
            changedPageBytes != ExpectedChangedTexturePageByteCount ||
            changedModelBytes != ExpectedChangedModelByteCount ||
            changedDataBytes != ExpectedChangedDataByteCount ||
            diffRangeCount != ExpectedDiffRangeCount ||
            !HashEquals(diffManifestSha256, ExpectedDiffManifestSha256) ||
            !HashEquals(deterministicPlanSha256, ExpectedDeterministicPlanSha256) ||
            composition.TexturePagePatchCount != ExpectedTexturePagePatchCount ||
            composition.TexturePagePatchedByteCount != ExpectedChangedTexturePageByteCount)
        {
            throw new InvalidDataException("The exact first ID65 cross-level private-texture plan drifted from its pinned donor, face, row, hash, or diff contract.");
        }
    }

    private static UnusedLevel65NativeTextureDonorManifest BuildDonorManifest(
        string sourceImagePath,
        string sourceImageSha256,
        LevelDefinition donorLevel,
        LevelAsset donorAsset)
    {
        NativeTerrainTextureRuntimeControlAudit runtime =
            NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, donorLevel);
        NativeTerrainTextureInitialStateResult initialized =
            NativeTerrainTextureRuntimeControlScanner.InitializeTextureRecords(runtime, donorAsset.Model);
        if (!runtime.Complete || !initialized.Complete || runtime.TextureCount != 32 ||
            runtime.ControlledTextureIds.Contains(DonorTextureId) ||
            runtime.AnimationSourceTextureIds.Contains(DonorTextureId) ||
            initialized.Mutations.Any(mutation => mutation.DestinationTextureId == DonorTextureId))
        {
            throw new InvalidDataException("Gnasty's World T12 is no longer one static, load-stable donor record.");
        }

        int textureLength = ReadInt32(donorAsset.Model, 0);
        int textureCount = ReadInt32(donorAsset.Model, 4);
        if (textureCount != runtime.TextureCount || textureLength != 8 + (textureCount * RecordGrowthByteCount))
            throw new InvalidDataException("Gnasty's World terrain texture table changed.");
        int lowOffset = 8 + (DonorTextureId * 16);
        int highStart = 8 + (textureCount * 16);
        int highOffset = highStart + (DonorTextureId * 168);
        byte[] lowRow = initialized.InitializedTextureData.AsSpan(lowOffset, 16).ToArray();
        byte[] highRow = initialized.InitializedTextureData.AsSpan(highOffset, 168).ToArray();
        if (!lowRow.SequenceEqual(donorAsset.Model.AsSpan(lowOffset, 16).ToArray()) ||
            !highRow.SequenceEqual(donorAsset.Model.AsSpan(highOffset, 168).ToArray()))
        {
            throw new InvalidDataException("Gnasty's World T12 unexpectedly needs load-time mutation.");
        }

        NativeTerrainTextureRecordStorageRequirement requirement =
            NativeTexturePageOwnershipScanner.InspectTerrainRecordStorage(
                sourceImagePath,
                donorLevel,
                DonorTextureId);
        NativeTexturePageTargetStorageIsolationReport isolation =
            NativeTexturePageOwnershipScanner.InspectTargetStorageIsolation(
                sourceImagePath,
                donorLevel,
                DonorTextureId);
        NativeTexturePageOwnershipReport ownership =
            NativeTexturePageOwnershipScanner.Scan(sourceImagePath, donorLevel);
        if (!requirement.FormatComplete || requirement.LowDetailDescriptorCount != 2 ||
            requirement.HqDescriptorCount != 21 ||
            !requirement.HqSideHistogram.OrderBy(pair => pair.Key)
                .SequenceEqual(new Dictionary<int, int> { [16] = 16, [32] = 5 }.OrderBy(pair => pair.Key)) ||
            !isolation.AllConsumerClosureComplete ||
            isolation.TargetPixelAndClutByteCount != ExpectedDonorUniquePhysicalByteCount ||
            !ownership.AllConsumerClosureComplete ||
            !HashEquals(ownership.TexturePagesSubfileSha256, Hash(donorAsset.TexturePages)))
        {
            throw new InvalidDataException(
                $"Gnasty's World T12 lost its complete LQ/normal/close, CLUT/page, or all-consumer ownership proof: {requirement.Blockers.FirstOrDefault() ?? isolation.SafetyBlockers.FirstOrDefault()}");
        }

        ParsedModel donorModel = ParseTerrainModelForFaceScope(
            donorAsset.Model,
            donorAsset.ModelWadOffset);
        (int faceCount, string representative) = FindHighDetailTextureUses(
            donorAsset.Model,
            donorModel,
            DonorTextureId);
        if (faceCount <= 0 || string.IsNullOrWhiteSpace(representative))
            throw new InvalidDataException("Gnasty's World T12 is no longer face-backed.");

        byte[] completeRecord = [.. lowRow, .. highRow];
        return new UnusedLevel65NativeTextureDonorManifest(
            DonorManifestId,
            sourceImageSha256,
            donorLevel.Key,
            donorLevel.DisplayName,
            DonorWadEntry,
            DonorTextureId,
            textureCount,
            donorAsset.TexturePagesWadOffset,
            donorAsset.TexturePages.Length,
            Hash(donorAsset.TexturePages),
            donorAsset.ModelWadOffset,
            textureLength,
            Hash(donorAsset.Model.AsSpan(0, textureLength)),
            donorAsset.ModelWadOffset + lowOffset,
            Convert.ToHexString(lowRow),
            Hash(lowRow),
            donorAsset.ModelWadOffset + highOffset,
            Convert.ToHexString(highRow),
            Hash(highRow),
            Hash(completeRecord),
            LowDetailDescriptorCount: 2,
            NormalHighDetailDescriptorCount: 4,
            CloseHighDetailDescriptorCount: 16,
            UniquePhysicalPixelAndClutBytes: isolation.TargetPixelAndClutByteCount,
            HighDetailSideHistogram: requirement.HqSideHistogram
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            ReferencingHighDetailFaceCount: faceCount,
            RepresentativeHighDetailFace: representative,
            RuntimeSceneSha256: NormalizeHash(runtime.SceneSha256),
            RuntimeControlAuditComplete: true,
            RuntimeControlled: false,
            AnimationSource: false,
            CompleteNativeGrammar: true,
            SourceOwnedImmutable: true);
    }

    private static void RequireComposition(
        NativeTerrainTextureFixedTailPrivateRecordPlan composition,
        NativeTerrainTextureRecordAppendSourceBinding binding,
        UnusedLevel65NativeTextureDonorManifest donor,
        NativeTerrainTextureRecordExistingPatch facePatch)
    {
        NativeTerrainTextureRecordAppendPlan append = composition.Append;
        if (!composition.SourceBindingVerified ||
            !composition.GlobalPackingProofComplete ||
            !composition.OriginalRowsInstalledExactly ||
            !composition.SyntheticRowsInstalledExactly ||
            !composition.OrdinaryPatchesIncluded ||
            !composition.PatchPreimagesVerified ||
            !composition.CombinedPatchesDisjoint ||
            !composition.FixedSubfileBoundaryPreserved ||
            composition.TargetWadEntry != TargetWadEntry ||
            composition.GlobalPacking.SourceTextureCount != SourceTextureCount ||
            composition.GlobalPacking.OutputTextureCount != PrivateTextureId + 1 ||
            composition.GlobalPacking.SyntheticRecords.Count != 1 ||
            composition.OrdinaryLevelDataPatches.Count != 1 ||
            composition.OrdinaryPatchRebaseProofs.Count != 1 ||
            append.SourceTextureCount != SourceTextureCount ||
            append.OutputTextureCount != PrivateTextureId + 1 ||
            append.GrowthByteCount != RecordGrowthByteCount ||
            append.SourceTextureComponentByteLength != ExpectedSourceTextureComponentByteLength ||
            append.OutputTextureComponentByteLength != ExpectedOutputTextureComponentByteLength ||
            append.SourceZeroTailByteCount != ExpectedSourceZeroTailByteCount ||
            append.OutputZeroTailByteCount != ExpectedOutputZeroTailByteCount ||
            !append.FixedSubfileBoundaryPreserved ||
            !append.ArchiveHeadersRequireNoFixup ||
            !append.ExistingRowsPreserved ||
            !append.AppendedRowsCopiedExactly ||
            !append.ShiftedComponentChainReparsed ||
            !append.ShiftedTerrainSurfaceSemanticsVerified ||
            !append.ZeroTailVerified ||
            !append.ExistingPatchesRebasedExactly ||
            !HashEquals(append.SourceBinding.SourceImageSha256, binding.SourceImageSha256))
        {
            throw new InvalidDataException("The ID65 fixed-tail private-texture composition lost a required structural or logical proof.");
        }

        NativeTerrainTextureGlobalRepackResearchPlan packing = composition.GlobalPacking.PackingProof;
        if (!packing.ExactIndexedPixelReadbackVerified ||
            !packing.ExactPaletteReadbackVerified ||
            !packing.PixelAliasRelationshipsPreserved ||
            !packing.PaletteAliasRelationshipsPreserved ||
            !packing.LowDetailAliasPreserved ||
            !packing.ProtectedStoragePreserved ||
            !packing.FixedDescriptorRowsPreserved ||
            !packing.TargetMaterialBitsPreserved)
        {
            throw new InvalidDataException("The ID65 private texture lost exact indexed-pixel, palette, alias, protected-page, or material proof.");
        }

        NativeTerrainTextureGlobalRepackPackedRecord synthetic =
            composition.GlobalPacking.SyntheticRecords.Single();
        NativeTerrainTextureRecordAppendResolvedRecord resolved =
            append.ResolvedRecords.Single();
        NativeTerrainTextureRecordRebasedPatchProof rebased =
            composition.OrdinaryPatchRebaseProofs.Single();
        if (synthetic.TargetTextureId != PrivateTextureId ||
            synthetic.DonorWadEntry != DonorWadEntry ||
            synthetic.DonorTextureId != DonorTextureId ||
            resolved.AssignedTextureId != PrivateTextureId ||
            resolved.DonorWadEntry != DonorWadEntry ||
            resolved.DonorTextureId != DonorTextureId ||
            resolved.MaterialTemplateTextureId != MaterialTemplateTextureId ||
            !HashEquals(synthetic.LowDetailRowSha256, resolved.LowDetailSha256) ||
            !HashEquals(synthetic.HighDetailRowSha256, resolved.HighDetailSha256) ||
            rebased.SourceWadOffset != facePatch.WadOffset ||
            rebased.OutputWadOffset != facePatch.WadOffset + RecordGrowthByteCount ||
            rebased.ByteLength != 4 ||
            !HashEquals(rebased.BeforeSha256, Hash(facePatch.Before)) ||
            !HashEquals(rebased.AfterSha256, Hash(facePatch.After)) ||
            donor.RuntimeControlled || donor.AnimationSource)
        {
            throw new InvalidDataException("The ID65 private row provenance or face-patch rebase changed.");
        }
    }

    private static void RequireTargetAsset(LevelAsset target)
    {
        if (target.DataWadOffset != UnusedLevel65FullAuthoringConstructionTemplate.DataEntryWadOffset ||
            target.Data.Length != TargetDataByteLength ||
            target.TexturePagesWadOffset != target.DataWadOffset + 0x800 ||
            target.TexturePages.Length != TargetTexturePagesByteLength ||
            target.ModelWadOffset != UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset ||
            target.Model.Length != TargetModelByteLength)
        {
            throw new InvalidDataException("The exact ID65 row80 data/subfile layout changed.");
        }
    }

    private static void RequireFoundationModel(ParsedModel model)
    {
        if (model.Texture.ByteLength != ExpectedSourceTextureComponentByteLength ||
            model.TextureCount != SourceTextureCount ||
            model.Sectors.Count != 216 ||
            model.UsedByteLength != TargetModelByteLength - ExpectedSourceZeroTailByteCount ||
            model.ZeroTailByteCount != ExpectedSourceZeroTailByteCount ||
            !HashEquals(model.ModelSha256,
                "ccd18568b9b6cb7a41d2bf8a47c7dc475ca2cb1f9f127ac9a90ef9ac0a8be4f1") ||
            model.Sectors.Sum(sector => sector.LowDetailFaceCount) != 1_438 ||
            model.Sectors.Sum(sector => sector.HighDetailFaceCount) != 3_888)
        {
            throw new InvalidDataException("The exact ID65 HP/LP foundation model changed.");
        }
    }

    private static FaceLocation LocateAuthoredFaces(
        byte[] model,
        long modelWadOffset,
        ParsedSector sector,
        int expectedTextureId)
    {
        int sectorOffset = checked((int)(sector.WadOffset - modelWadOffset));
        if (sector.Index != TargetSectorIndex ||
            sector.LowDetailVertexCount != 38 ||
            sector.LowDetailColorCount != 48 ||
            sector.LowDetailFaceCount != 22 ||
            sector.HighDetailVertexCount != 143 ||
            sector.HighDetailColorCount != 185 ||
            sector.HighDetailFaceCount != 114)
        {
            throw new InvalidDataException("The authored ID65 sector-213 HP/LP counts changed.");
        }

        int lpVertexStart = sectorOffset + 28;
        int lpColorStart = lpVertexStart + (sector.LowDetailVertexCount * 4);
        int lpFaceStart = lpColorStart + (sector.LowDetailColorCount * 4);
        int hpVertexStart = lpFaceStart + (sector.LowDetailFaceCount * 8);
        int hpColorStart = hpVertexStart + (sector.HighDetailVertexCount * 4);
        int hpFaceStart = hpColorStart + (sector.HighDetailColorCount * 8);
        int sectorEnd = hpFaceStart + (sector.HighDetailFaceCount * 16);
        if (sectorEnd != sectorOffset + sector.ByteLength)
            throw new InvalidDataException("The authored ID65 sector-213 table chain changed.");

        int lpFaceOffset = lpFaceStart + (TargetLowDetailFaceIndex * 8);
        int hpFaceOffset = hpFaceStart + (TargetHighDetailFaceIndex * 16);
        byte[] lpFace = model.AsSpan(lpFaceOffset, 8).ToArray();
        byte[] hpFace = model.AsSpan(hpFaceOffset, 16).ToArray();
        byte[] expectedHpFace = Convert.FromHexString("8C8C8D8E0101000219024001080A1000");
        expectedHpFace[8] = checked((byte)expectedTextureId);
        if (!hpFace.SequenceEqual(expectedHpFace))
            throw new InvalidDataException(
                $"The authored ID65 HP face preimage changed: {Convert.ToHexString(hpFace)}.");
        return new FaceLocation(
            modelWadOffset + lpFaceOffset,
            lpFace,
            modelWadOffset + hpFaceOffset,
            hpFace);
    }

    private static ParsedModel ParseModel(byte[] model, long modelWadOffset)
    {
        int cursor = 0;
        ParsedComponent texture = ReadComponent(model, modelWadOffset, ref cursor, "texture");
        ParsedComponent environment = ReadComponent(model, modelWadOffset, ref cursor, "environment");
        ParsedComponent occlusion = ReadComponent(model, modelWadOffset, ref cursor, "occlusion");
        ParsedComponent special = ReadComponent(model, modelWadOffset, ref cursor, "special surface");
        ParsedComponent collision = ReadComponent(model, modelWadOffset, ref cursor, "collision");
        ParsedComponent cyclorama = ReadComponent(model, modelWadOffset, ref cursor, "cyclorama");
        int portalCount = ReadInt32(model, cursor);
        if (portalCount < 0 || portalCount > 1024)
            throw new InvalidDataException("The native portal table count is invalid.");
        int portalStart = cursor;
        cursor += 4;
        for (int portal = 0; portal < portalCount; portal++)
        {
            int pointCount = ReadInt32(model, cursor + 4);
            if (pointCount < 1 || pointCount > 4096)
                throw new InvalidDataException("A native portal point count is invalid.");
            cursor = checked(cursor + 8 + (pointCount * 12));
        }
        ParsedComponent portalComponent = new(
            "portal table",
            modelWadOffset + portalStart,
            cursor - portalStart,
            Hash(model.AsSpan(portalStart, cursor - portalStart)));
        ParsedComponent particles = ReadComponent(model, modelWadOffset, ref cursor, "particles");
        ParsedComponent sound = ReadComponent(model, modelWadOffset, ref cursor, "sound");
        if (model.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The model suffix is not exact zero tail.");

        int environmentOffset = checked((int)(environment.WadOffset - modelWadOffset));
        int sectorCount = ReadInt32(model, environmentOffset + 4);
        if (sectorCount <= 0 || sectorCount > 4096)
            throw new InvalidDataException("The native environment sector count is invalid.");
        List<ParsedSector> sectors = new(sectorCount);
        for (int index = 0; index < sectorCount; index++)
        {
            int pointer = ReadInt32(model, environmentOffset + 8 + (index * 4));
            int sectorOffset = checked(environmentOffset + 4 + pointer);
            int nextOffset = index + 1 < sectorCount
                ? checked(environmentOffset + 4 + ReadInt32(model, environmentOffset + 8 + ((index + 1) * 4)))
                : environmentOffset + environment.ByteLength;
            if (sectorOffset < environmentOffset || sectorOffset + 28 > nextOffset)
                throw new InvalidDataException($"Native scene sector {index} has an invalid pointer.");
            int lpVertices = model[sectorOffset + 16];
            int lpColors = model[sectorOffset + 17];
            int lpFaces = model[sectorOffset + 18];
            int hpVertices = model[sectorOffset + 20];
            int hpColors = model[sectorOffset + 21];
            int hpFaces = model[sectorOffset + 22];
            int byteLength = checked((7 + lpVertices + lpColors + (lpFaces * 2) +
                                      hpVertices + (hpColors * 2) + (hpFaces * 4)) * 4);
            if (sectorOffset + byteLength > nextOffset)
                throw new InvalidDataException($"Native scene sector {index} overruns its pointer boundary.");
            sectors.Add(new ParsedSector(
                index,
                modelWadOffset + sectorOffset,
                byteLength,
                lpVertices,
                lpColors,
                lpFaces,
                hpVertices,
                hpColors,
                hpFaces));
        }

        return new ParsedModel(
            ReadInt32(model, 4),
            texture,
            [texture, environment, occlusion, special, collision, cyclorama, portalComponent, particles, sound],
            sectors,
            cursor,
            model.Length - cursor,
            Hash(model));
    }

    private static ParsedModel ParseTerrainModelForFaceScope(byte[] model, long modelWadOffset)
    {
        int cursor = 0;
        ParsedComponent texture = ReadComponent(model, modelWadOffset, ref cursor, "texture");
        ParsedComponent environment = ReadComponent(model, modelWadOffset, ref cursor, "environment");
        int environmentOffset = checked((int)(environment.WadOffset - modelWadOffset));
        int sectorCount = ReadInt32(model, environmentOffset + 4);
        if (sectorCount <= 0 || sectorCount > 4096)
            throw new InvalidDataException("The donor environment sector count is invalid.");

        List<ParsedSector> sectors = new(sectorCount);
        for (int index = 0; index < sectorCount; index++)
        {
            int pointer = ReadInt32(model, environmentOffset + 8 + (index * 4));
            int sectorOffset = checked(environmentOffset + 4 + pointer);
            int nextOffset = index + 1 < sectorCount
                ? checked(environmentOffset + 4 + ReadInt32(model, environmentOffset + 8 + ((index + 1) * 4)))
                : environmentOffset + environment.ByteLength;
            if (sectorOffset < environmentOffset || sectorOffset + 28 > nextOffset)
                throw new InvalidDataException($"Donor scene sector {index} has an invalid pointer.");
            int lpVertices = model[sectorOffset + 16];
            int lpColors = model[sectorOffset + 17];
            int lpFaces = model[sectorOffset + 18];
            int hpVertices = model[sectorOffset + 20];
            int hpColors = model[sectorOffset + 21];
            int hpFaces = model[sectorOffset + 22];
            int byteLength = checked((7 + lpVertices + lpColors + (lpFaces * 2) +
                                      hpVertices + (hpColors * 2) + (hpFaces * 4)) * 4);
            if (sectorOffset + byteLength > nextOffset)
                throw new InvalidDataException($"Donor scene sector {index} overruns its pointer boundary.");
            sectors.Add(new ParsedSector(
                index,
                modelWadOffset + sectorOffset,
                byteLength,
                lpVertices,
                lpColors,
                lpFaces,
                hpVertices,
                hpColors,
                hpFaces));
        }

        return new ParsedModel(
            ReadInt32(model, 4),
            texture,
            [texture, environment],
            sectors,
            cursor,
            model.Length - cursor,
            Hash(model));
    }

    private static ParsedComponent ReadComponent(
        byte[] bytes,
        long wadOffset,
        ref int cursor,
        string name)
    {
        if (cursor < 0 || cursor + 4 > bytes.Length)
            throw new InvalidDataException($"The native {name} component header is outside model data.");
        int length = ReadInt32(bytes, cursor);
        if (length < 4 || (length & 3) != 0 || cursor + (long)length > bytes.Length)
            throw new InvalidDataException($"The native {name} component length 0x{length:X} is invalid.");
        ParsedComponent result = new(
            name,
            wadOffset + cursor,
            length,
            Hash(bytes.AsSpan(cursor, length)));
        cursor += length;
        return result;
    }

    private static int CountHighDetailTextureUses(byte[] model, ParsedModel parsed, int textureId) =>
        FindHighDetailTextureUses(model, parsed, textureId).Count;

    private static (int Count, string Representative) FindHighDetailTextureUses(
        byte[] model,
        ParsedModel parsed,
        int textureId)
    {
        int count = 0;
        string representative = "";
        foreach (ParsedSector sector in parsed.Sectors)
        {
            int sectorOffset = checked((int)(sector.WadOffset - parsed.Components[0].WadOffset));
            int lpVertexStart = sectorOffset + 28;
            int lpColorStart = lpVertexStart + (sector.LowDetailVertexCount * 4);
            int lpFaceStart = lpColorStart + (sector.LowDetailColorCount * 4);
            int hpVertexStart = lpFaceStart + (sector.LowDetailFaceCount * 8);
            int hpColorStart = hpVertexStart + (sector.HighDetailVertexCount * 4);
            int hpFaceStart = hpColorStart + (sector.HighDetailColorCount * 8);
            for (int faceIndex = 0; faceIndex < sector.HighDetailFaceCount; faceIndex++)
            {
                int faceOffset = hpFaceStart + (faceIndex * 16);
                int faceTextureId = model[faceOffset + 8] & 0x7F;
                if (faceTextureId != textureId)
                    continue;
                count++;
                if (string.IsNullOrEmpty(representative))
                    representative = $"{sector.Index}:{faceIndex}:hp";
            }
        }
        return (count, representative);
    }

    private static UnusedLevel65NativeTextureDiffRange[] BuildDiffRanges(
        IReadOnlyList<NativeTerrainTexturePrivateStructuralPatch> patches)
    {
        List<UnusedLevel65NativeTextureDiffRange> ranges = [];
        foreach (NativeTerrainTexturePrivateStructuralPatch patch in patches.OrderBy(item => item.WadOffset))
        {
            int cursor = 0;
            while (cursor < patch.ByteLength)
            {
                while (cursor < patch.ByteLength && patch.Before[cursor] == patch.After[cursor])
                    cursor++;
                if (cursor == patch.ByteLength)
                    break;
                int start = cursor;
                while (cursor < patch.ByteLength && patch.Before[cursor] != patch.After[cursor])
                    cursor++;
                int length = cursor - start;
                ranges.Add(new UnusedLevel65NativeTextureDiffRange(
                    patch.WadOffset + start,
                    length,
                    Hash(patch.Before.AsSpan(start, length)),
                    Hash(patch.After.AsSpan(start, length)),
                    patch.Kind));
            }
        }
        return ranges.OrderBy(range => range.WadOffset).ToArray();
    }

    private static void RequireDiffAllowlist(
        IReadOnlyList<UnusedLevel65NativeTextureDiffRange> ranges,
        LevelAsset target)
    {
        if (ranges.Count == 0)
            throw new InvalidDataException("The ID65 native-texture plan emitted no logical differences.");
        long pageStart = target.TexturePagesWadOffset;
        long pageEnd = pageStart + target.TexturePages.Length;
        long modelStart = target.ModelWadOffset;
        long modelEnd = modelStart + target.Model.Length;
        if (ranges.Any(range =>
                range.ByteLength <= 0 ||
                !((range.WadOffset >= pageStart && range.WadOffset + range.ByteLength <= pageEnd) ||
                  (range.WadOffset >= modelStart && range.WadOffset + range.ByteLength <= modelEnd))))
        {
            throw new InvalidDataException("The ID65 native-texture diff escaped row80 subfiles 0 and 1.");
        }
        for (int index = 1; index < ranges.Count; index++)
        {
            if (ranges[index].WadOffset < ranges[index - 1].WadOffset + ranges[index - 1].ByteLength)
                throw new InvalidDataException("The ID65 native-texture logical diff ranges overlap.");
        }
    }

    private static string HashDiffManifest(IReadOnlyList<UnusedLevel65NativeTextureDiffRange> ranges)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendInt32(hash, ranges.Count);
        foreach (UnusedLevel65NativeTextureDiffRange range in ranges)
        {
            AppendInt64(hash, range.WadOffset);
            AppendInt32(hash, range.ByteLength);
            AppendString(hash, NormalizeHash(range.BeforeSha256));
            AppendString(hash, NormalizeHash(range.AfterSha256));
            AppendString(hash, range.Owner);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string HashPlanIdentity(
        string sourceImageSha256,
        UnusedLevel65NativeTextureDonorManifest donor,
        string sourceModelSha256,
        string outputModelSha256,
        string sourcePagesSha256,
        string outputPagesSha256,
        string sourceDataSha256,
        string outputDataSha256,
        string privateLowSha256,
        string privateHighSha256,
        string diffManifestSha256,
        FaceLocation sourceFaces,
        FaceLocation outputFaces,
        NativeTerrainTextureRecordAppendPlan append)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string value in new[]
                 {
                     ProfileId,
                     NormalizeHash(sourceImageSha256),
                     donor.ManifestId,
                     donor.CompleteRecordSha256,
                     NormalizeHash(sourceModelSha256),
                     NormalizeHash(outputModelSha256),
                     NormalizeHash(sourcePagesSha256),
                     NormalizeHash(outputPagesSha256),
                     NormalizeHash(sourceDataSha256),
                     NormalizeHash(outputDataSha256),
                     NormalizeHash(privateLowSha256),
                     NormalizeHash(privateHighSha256),
                     NormalizeHash(diffManifestSha256),
                     Convert.ToHexString(sourceFaces.HighDetailFace),
                     Convert.ToHexString(outputFaces.HighDetailFace),
                     Convert.ToHexString(sourceFaces.LowDetailFace),
                     Convert.ToHexString(outputFaces.LowDetailFace)
                 })
        {
            AppendString(hash, value);
        }
        AppendInt32(hash, append.SourceTextureCount);
        AppendInt32(hash, append.OutputTextureCount);
        AppendInt32(hash, append.GrowthByteCount);
        AppendInt32(hash, append.SourceUsedByteLength);
        AppendInt32(hash, append.OutputUsedByteLength);
        AppendInt32(hash, append.SourceZeroTailByteCount);
        AppendInt32(hash, append.OutputZeroTailByteCount);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static bool TargetProtectedSubfilesPreserved(byte[] sourceData, byte[] outputData)
    {
        (int Offset, int Length)[] subfiles =
        [
            (0x000800, 0x0DE000),
            (0x0DE800, 0x094800),
            (0x173000, 0x05D000),
            (0x1D0000, 0x008800),
            (0x1D8800, 0x049800),
            (0x222000, 0x042800),
            (0x264800, 0x05D000),
            (0x2C1800, 0x020800)
        ];
        return subfiles.Where((_, index) => index >= 2)
            .All(subfile => sourceData.AsSpan(subfile.Offset, subfile.Length)
                .SequenceEqual(outputData.AsSpan(subfile.Offset, subfile.Length)));
    }

    private static LevelAsset ReadLevelAsset(string imagePath, int wadEntry)
    {
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.OpenRead(imagePath);
        byte[] row = DiscImage.ReadFileBytes(image, layout, UnusedLevel65FullAuthoringConstructionTemplate.WadLba, wadEntry * 8L, 8);
        long dataWadOffset = BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(0, 4));
        int dataByteLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(4, 4)));
        if (dataWadOffset <= 0 || dataByteLength < 0x40)
            throw new InvalidDataException($"WAD row {wadEntry} has an invalid level entry.");
        byte[] header = DiscImage.ReadFileBytes(
            image,
            layout,
            UnusedLevel65FullAuthoringConstructionTemplate.WadLba,
            dataWadOffset,
            0x40);
        int pagesOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4)));
        int pagesLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4)));
        int modelOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, 4)));
        int modelLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12, 4)));
        if (pagesOffset < 0x40 || pagesLength <= 0 || modelOffset < 0x40 || modelLength <= 0 ||
            pagesOffset + (long)pagesLength > dataByteLength || modelOffset + (long)modelLength > dataByteLength)
        {
            throw new InvalidDataException($"WAD row {wadEntry} has invalid texture/model subfile boundaries.");
        }
        return new LevelAsset(
            dataWadOffset,
            DiscImage.ReadFileBytes(
                image,
                layout,
                UnusedLevel65FullAuthoringConstructionTemplate.WadLba,
                dataWadOffset,
                dataByteLength),
            dataWadOffset + pagesOffset,
            DiscImage.ReadFileBytes(
                image,
                layout,
                UnusedLevel65FullAuthoringConstructionTemplate.WadLba,
                dataWadOffset + pagesOffset,
                pagesLength),
            dataWadOffset + modelOffset,
            DiscImage.ReadFileBytes(
                image,
                layout,
                UnusedLevel65FullAuthoringConstructionTemplate.WadLba,
                dataWadOffset + modelOffset,
                modelLength));
    }

    private static int[] DifferentByteIndexes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
            return [];
        List<int> result = [];
        for (int index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
                result.Add(index);
        }
        return result.ToArray();
    }

    private static int CountChangedBytes(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("A deterministic ID65 diff compares unequal-length buffers.");
        int count = 0;
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index])
                count++;
        }
        return count;
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("A native Int32 read escaped its source buffer.");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string NormalizeHash(string value) => (value ?? "").Trim().ToLowerInvariant();

    private static bool HashEquals(string left, string right) =>
        string.Equals(NormalizeHash(left), NormalizeHash(right), StringComparison.Ordinal);

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!HashEquals(actual, expected))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}; expected {expected}.");
    }

    private sealed record LevelAsset(
        long DataWadOffset,
        byte[] Data,
        long TexturePagesWadOffset,
        byte[] TexturePages,
        long ModelWadOffset,
        byte[] Model);

    private sealed record ParsedComponent(
        string Name,
        long WadOffset,
        int ByteLength,
        string Sha256);

    private sealed record ParsedSector(
        int Index,
        long WadOffset,
        int ByteLength,
        int LowDetailVertexCount,
        int LowDetailColorCount,
        int LowDetailFaceCount,
        int HighDetailVertexCount,
        int HighDetailColorCount,
        int HighDetailFaceCount);

    private sealed record ParsedModel(
        int TextureCount,
        ParsedComponent Texture,
        IReadOnlyList<ParsedComponent> Components,
        IReadOnlyList<ParsedSector> Sectors,
        int UsedByteLength,
        int ZeroTailByteCount,
        string ModelSha256);

    private sealed record FaceLocation(
        long LowDetailFaceWadOffset,
        byte[] LowDetailFace,
        long HighDetailFaceWadOffset,
        byte[] HighDetailFace);
}
