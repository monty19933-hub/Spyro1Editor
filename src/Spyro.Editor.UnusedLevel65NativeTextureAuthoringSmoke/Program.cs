using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string foundationPrefix = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE");
string foundationImagePath = foundationPrefix + ".bin";
string foundationCuePath = foundationPrefix + ".cue";
Require(File.Exists(foundationImagePath), $"Missing exact foundation BIN: {foundationImagePath}");
Require(File.Exists(foundationCuePath), $"Missing exact foundation CUE: {foundationCuePath}");
string sourceHashBefore = HashFile(foundationImagePath);
DateTime sourceWriteBefore = File.GetLastWriteTimeUtc(foundationImagePath);
long sourceLengthBefore = new FileInfo(foundationImagePath).Length;

UnusedLevel65NativeTextureStaticPlan first =
    UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(foundationImagePath);
UnusedLevel65NativeTextureStaticPlan second =
    UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(foundationImagePath);

Require(first.ProfileId == UnusedLevel65NativeTextureAuthoringContract.ProfileId,
    "The ID65 native-texture profile id changed.");
Require(first.SourceImageSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedFoundationImageSha256,
    "The ID65 native-texture plan is not bound to the exact foundation image.");
Require(first.TargetWadEntry == 80 && first.TargetDataWadOffset == 0x6936800 &&
        first.TargetDataByteLength == 0x2E2000 &&
        first.TargetTexturePagesWadOffset == 0x6937000 &&
        first.TargetTexturePagesByteLength == 0xDE000 &&
        first.TargetModelWadOffset == 0x6A15000 && first.TargetModelByteLength == 0x94800,
    "The exact row80 data, texture-page, or model boundary changed.");
Require(first.SourceTextureCount == 66 && first.OutputTextureCount == 67 &&
        first.PrivateTextureId == 66 && first.MaterialTemplateTextureId == 25 &&
        first.RecordGrowthByteCount == 184 &&
        first.SourceTextureComponentByteLength == 0x2F78 &&
        first.OutputTextureComponentByteLength == 0x3030 &&
        first.SourceZeroTailByteCount == 0x2C8 && first.OutputZeroTailByteCount == 0x210,
    "The exact private T66 append/growth contract changed.");
Require(first.OutputLowDetailRowWadOffset == 0x6A15428 &&
        first.OutputHighDetailRowWadOffset == 0x6A17F88 &&
        first.OutputPrivateLowDetailRowSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedPrivateLowDetailRowSha256 &&
        first.OutputPrivateHighDetailRowSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedPrivateHighDetailRowSha256,
    "The appended T66 LQ/HQ row offsets changed.");
Require(first.SourceTexturePagesSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedSourceTexturePagesSha256 &&
        first.OutputTexturePagesSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputTexturePagesSha256 &&
        first.SourceModelSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedSourceModelSha256 &&
        first.OutputModelSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputModelSha256 &&
        first.SourceDataSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedSourceDataSha256 &&
        first.OutputDataSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputDataSha256,
    "The exact row80 texture-page/model/data preimage or composed hash changed.");

UnusedLevel65NativeTextureDonorManifest donor = first.Donor;
Require(donor.ManifestId == UnusedLevel65NativeTextureAuthoringContract.DonorManifestId &&
        donor.DonorLevelKey == "gnastysworld" && donor.DonorLevelName == "Gnasty's World" &&
        donor.DonorWadEntry == 70 && donor.DonorTextureId == 12 && donor.DonorTextureCount == 32 &&
        donor.LowDetailDescriptorCount == 2 && donor.NormalHighDetailDescriptorCount == 4 &&
        donor.CloseHighDetailDescriptorCount == 16 &&
        donor.UniquePhysicalPixelAndClutBytes == 0xA00 &&
        donor.HighDetailSideHistogram.OrderBy(pair => pair.Key)
            .SequenceEqual(new Dictionary<int, int> { [16] = 16, [32] = 5 }.OrderBy(pair => pair.Key)) &&
        donor.ReferencingHighDetailFaceCount > 0 &&
        !string.IsNullOrWhiteSpace(donor.RepresentativeHighDetailFace) &&
        donor.RuntimeControlAuditComplete && !donor.RuntimeControlled && !donor.AnimationSource &&
        donor.CompleteNativeGrammar && donor.SourceOwnedImmutable,
    "The immutable Gnasty's World T12 donor dependency manifest changed.");
Require(donor.TexturePagesWadOffset == 0x51EF000 && donor.TexturePagesByteLength == 0xBB800 &&
        donor.TextureComponentWadOffset == 0x52AA800 && donor.TextureComponentByteLength == 0x1708 &&
        donor.LowDetailRowWadOffset == 0x52AA8C8 && donor.HighDetailRowWadOffset == 0x52AB1E8 &&
        donor.TexturePagesSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorTexturePagesSha256 &&
        donor.TextureComponentSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorTextureComponentSha256 &&
        donor.LowDetailRowSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorLowDetailRowSha256 &&
        donor.HighDetailRowSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorHighDetailRowSha256 &&
        donor.CompleteRecordSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorCompleteRecordSha256 &&
        donor.RuntimeSceneSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorRuntimeSceneSha256 &&
        donor.ReferencingHighDetailFaceCount == 73 && donor.RepresentativeHighDetailFace == "0:15:hp",
    "The exact Gnasty's World T12 source offsets, record hashes, runtime scene, or face scope changed.");

UnusedLevel65NativeTextureFaceBinding face = first.FaceBinding;
Require(face.SectorIndex == 213 && face.SourceHighDetailFaceIndex == 113 &&
        face.SourceLowDetailFaceIndex == 21 &&
        face.SourceTextureId == 25 && face.OutputTextureId == 66 &&
        face.OutputHighDetailFaceWadOffset == face.SourceHighDetailFaceWadOffset + 184 &&
        face.OutputHighDetailTextureWordWadOffset == face.SourceHighDetailTextureWordWadOffset + 184 &&
        face.OutputLowDetailFaceWadOffset == face.SourceLowDetailFaceWadOffset + 184 &&
        face.OutputTextureLinkedHighDetailFaceCount == 1 &&
        face.SourceTextureLinkedHighDetailFaceCount == 63 &&
        face.SourceHighDetailFaceWadOffset == 0x6A3F9EC &&
        face.OutputHighDetailFaceWadOffset == 0x6A3FAA4 &&
        face.SourceLowDetailFaceWadOffset == 0x6A3EAD0 &&
        face.OutputLowDetailFaceWadOffset == 0x6A3EB88 &&
        face.SourceHighDetailFaceHex == "8C8C8D8E0101000219024001080A1000" &&
        face.OutputHighDetailFaceHex == "8C8C8D8E0101000242024001080A1000" &&
        face.SourceLowDetailFaceHex == "0065498E00821000" &&
        face.OutputLowDetailFaceHex == "0065498E00821000" &&
        face.HighDetailTextureIdOnlyChanged && face.LowDetailFacePreserved &&
        face.LowDetailGeometryIsColorOnly && face.CompleteTextureRecordCarriesNativeLqAndHqTiers,
    "The exact authored HP face or paired color-only LP face scope changed.");
Require(first.RelocatedComponents.Count == 8 &&
        first.RelocatedComponents.All(component =>
            component.OutputRelativeOffset == component.SourceRelativeOffset + 184 &&
            component.ContentsPreserved),
    "One or more post-texture native components did not relocate intact by exactly 184 bytes: " +
    string.Join("; ", first.RelocatedComponents.Select(component =>
        $"{component.Name} 0x{component.SourceRelativeOffset:X}->0x{component.OutputRelativeOffset:X} len=0x{component.ByteLength:X} preserved={component.ContentsPreserved}")));
Require(first.DiffRanges.Count > 0 &&
        first.DiffRanges.Count == UnusedLevel65NativeTextureAuthoringContract.ExpectedDiffRangeCount &&
        first.ChangedTexturePageByteCount == UnusedLevel65NativeTextureAuthoringContract.ExpectedChangedTexturePageByteCount &&
        first.ChangedModelByteCount == UnusedLevel65NativeTextureAuthoringContract.ExpectedChangedModelByteCount &&
        first.ChangedDataByteCount == UnusedLevel65NativeTextureAuthoringContract.ExpectedChangedDataByteCount &&
        first.ChangedDataByteCount == first.ChangedTexturePageByteCount + first.ChangedModelByteCount,
    "The row80 page/model logical-diff boundary changed.");
Require(first.DiffManifestSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedDiffManifestSha256 &&
        first.DeterministicPlanSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedDeterministicPlanSha256 &&
        first.Composition.TexturePagePatchCount == UnusedLevel65NativeTextureAuthoringContract.ExpectedTexturePagePatchCount &&
        first.Composition.TexturePagePatchedByteCount == UnusedLevel65NativeTextureAuthoringContract.ExpectedChangedTexturePageByteCount &&
        first.DiffRanges.All(range =>
            (range.WadOffset >= 0x6937000 && range.WadOffset + range.ByteLength <= 0x6937000 + 0xDE000) ||
            (range.WadOffset >= 0x6A15000 && range.WadOffset + range.ByteLength <= 0x6A15000 + 0x94800)),
    "The pinned logical-diff manifest, page-patch plan, or exact row80 subfile allowlist changed.");
Require(first.DestinationOwnershipClosureComplete &&
        first.DestinationRuntimeControlAuditComplete &&
        first.PrivateRecordRuntimePersistent &&
        first.DonorDependencyClosureVerified &&
        first.PageAndClutRelocationVerified &&
        first.PrivatePerFaceStorageVerified &&
        first.HpLpPairingVerified &&
        first.CollisionAndOcclusionPreserved &&
        first.TargetEntryHeaderPreserved &&
        first.ProtectedSubfilesPreserved &&
        first.RetailWadEntriesExcluded &&
        first.ExecutableExcluded &&
        first.DeterministicReadbackRequired &&
        !first.DisposableRuntimeCandidateAuthorized &&
        !first.PromotionAuthorized &&
        !first.NormalCreateBinEnabled,
    "A static-only ID65 native-texture safety or non-promotion gate changed.");

Require(first.SourceTexturePagesSha256 == second.SourceTexturePagesSha256 &&
        first.OutputTexturePagesSha256 == second.OutputTexturePagesSha256 &&
        first.SourceModelSha256 == second.SourceModelSha256 &&
        first.OutputModelSha256 == second.OutputModelSha256 &&
        first.SourceDataSha256 == second.SourceDataSha256 &&
        first.OutputDataSha256 == second.OutputDataSha256 &&
        first.OutputPrivateLowDetailRowSha256 == second.OutputPrivateLowDetailRowSha256 &&
        first.OutputPrivateHighDetailRowSha256 == second.OutputPrivateHighDetailRowSha256 &&
        first.DiffManifestSha256 == second.DiffManifestSha256 &&
        first.DeterministicPlanSha256 == second.DeterministicPlanSha256 &&
        first.DiffRanges.SequenceEqual(second.DiffRanges) &&
        first.FaceBinding == second.FaceBinding &&
        first.Donor.ManifestId == second.Donor.ManifestId &&
        first.Donor.TexturePagesSha256 == second.Donor.TexturePagesSha256 &&
        first.Donor.TextureComponentSha256 == second.Donor.TextureComponentSha256 &&
        first.Donor.LowDetailRowSha256 == second.Donor.LowDetailRowSha256 &&
        first.Donor.HighDetailRowSha256 == second.Donor.HighDetailRowSha256 &&
        first.Donor.CompleteRecordSha256 == second.Donor.CompleteRecordSha256 &&
        first.Donor.RuntimeSceneSha256 == second.Donor.RuntimeSceneSha256 &&
        first.Donor.ReferencingHighDetailFaceCount == second.Donor.ReferencingHighDetailFaceCount &&
        first.Donor.RepresentativeHighDetailFace == second.Donor.RepresentativeHighDetailFace &&
        first.Donor.HighDetailSideHistogram.OrderBy(pair => pair.Key)
            .SequenceEqual(second.Donor.HighDetailSideHistogram.OrderBy(pair => pair.Key)),
    "Two ID65 native-texture builds did not produce the same deterministic static plan.");

Require(HashFile(foundationImagePath) == sourceHashBefore &&
        new FileInfo(foundationImagePath).Length == sourceLengthBefore &&
        File.GetLastWriteTimeUtc(foundationImagePath) == sourceWriteBefore,
    "The static ID65 native-texture planner modified its source foundation BIN.");

Console.WriteLine("ID65 native-texture authoring static plan:");
Console.WriteLine($"  donor: {donor.DonorLevelName} WAD{donor.DonorWadEntry} T{donor.DonorTextureId}; faces={donor.ReferencingHighDetailFaceCount}; representative={donor.RepresentativeHighDetailFace}");
Console.WriteLine($"  donor rows: LQ={donor.LowDetailRowSha256}; HQ={donor.HighDetailRowSha256}; complete={donor.CompleteRecordSha256}");
Console.WriteLine($"  donor pages/model: pages={donor.TexturePagesSha256}; texture-component={donor.TextureComponentSha256}; scene={donor.RuntimeSceneSha256}");
Console.WriteLine($"  donor offsets: pages WAD 0x{donor.TexturePagesWadOffset:X}/0x{donor.TexturePagesByteLength:X}; texture WAD 0x{donor.TextureComponentWadOffset:X}/0x{donor.TextureComponentByteLength:X}; LQ WAD 0x{donor.LowDetailRowWadOffset:X}; HQ WAD 0x{donor.HighDetailRowWadOffset:X}");
Console.WriteLine($"  target face: {face.SectorIndex}:{face.SourceHighDetailFaceIndex}:hp T{face.SourceTextureId}->T{face.OutputTextureId} WAD 0x{face.SourceHighDetailFaceWadOffset:X}->0x{face.OutputHighDetailFaceWadOffset:X}");
Console.WriteLine($"  target linked scope: source T{face.SourceTextureId} faces={face.SourceTextureLinkedHighDetailFaceCount}; output T{face.OutputTextureId} faces={face.OutputTextureLinkedHighDetailFaceCount}; before={face.SourceHighDetailFaceHex}; after={face.OutputHighDetailFaceHex}");
Console.WriteLine($"  paired LP: {face.SectorIndex}:{face.SourceLowDetailFaceIndex}:lp WAD 0x{face.SourceLowDetailFaceWadOffset:X}->0x{face.OutputLowDetailFaceWadOffset:X}; bytes={face.SourceLowDetailFaceHex}");
Console.WriteLine($"  target rows: LQ WAD 0x{first.OutputLowDetailRowWadOffset:X} {first.OutputPrivateLowDetailRowSha256}; HQ WAD 0x{first.OutputHighDetailRowWadOffset:X} {first.OutputPrivateHighDetailRowSha256}");
Console.WriteLine($"  pages: {first.SourceTexturePagesSha256} -> {first.OutputTexturePagesSha256}; changed={first.ChangedTexturePageByteCount:N0}");
Console.WriteLine($"  model: {first.SourceModelSha256} -> {first.OutputModelSha256}; changed={first.ChangedModelByteCount:N0}");
Console.WriteLine($"  data: {first.SourceDataSha256} -> {first.OutputDataSha256}; changed={first.ChangedDataByteCount:N0}");
Console.WriteLine($"  diff ranges={first.DiffRanges.Count:N0}; manifest={first.DiffManifestSha256}; plan={first.DeterministicPlanSha256}");
Console.WriteLine($"  used/tail: 0x{first.SourceUsedModelByteLength:X}/0x{first.SourceZeroTailByteCount:X} -> 0x{first.OutputUsedModelByteLength:X}/0x{first.OutputZeroTailByteCount:X}; page patches={first.Composition.TexturePagePatchCount}; page patch bytes={first.Composition.TexturePagePatchedByteCount:N0}");
foreach (UnusedLevel65NativeTextureComponentRelocation component in first.RelocatedComponents)
    Console.WriteLine($"  component {component.Name}: 0x{component.SourceRelativeOffset:X}->0x{component.OutputRelativeOffset:X} len=0x{component.ByteLength:X} sha={component.SourceSha256}");
Console.WriteLine("PASS UnusedLevel65NativeTextureAuthoringSmoke: immutable static cross-level donor, private T66 allocation, face-local HP assignment, paired LP preservation, exact page/CLUT relocation, collision/occlusion preservation, and deterministic no-write planning passed.");

static string FindRepositoryRoot(string? supplied)
{
    string current = string.IsNullOrWhiteSpace(supplied)
        ? Directory.GetCurrentDirectory()
        : Path.GetFullPath(supplied);
    while (true)
    {
        if (File.Exists(Path.Combine(current, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(current, "src", "Spyro.Editor.Core")))
        {
            return current;
        }
        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent == null)
            throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
        current = parent.FullName;
    }
}

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
