using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedBaseImage = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string foundationImage = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin");
Require(File.Exists(lockedBaseImage), $"Missing exact locked display-name image: {lockedBaseImage}");
Require(File.Exists(foundationImage), $"Missing exact static texture-witness source: {foundationImage}");
IReadOnlyDictionary<string, FileState> lockedBefore = SnapshotDirectory(Path.GetDirectoryName(lockedBaseImage)!);
IReadOnlyDictionary<string, FileState> foundationBefore = SnapshotDirectory(Path.GetDirectoryName(foundationImage)!);

UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2 =
    UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.BuildStaticPlan(lockedBaseImage);
Id65AuthoringManifest baseManifest = Id65AuthoringModelCompiler.CreateMinimalV2EquivalentManifest();
Id65CompiledAuthoringModel v2Model = Id65AuthoringModelCompiler.Compile(baseManifest, v2);
UnusedLevel65NativeTextureStaticPlan oldStaticTextureProof =
    UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(foundationImage);
Id65V2NativeTextureWitness witness =
    Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(oldStaticTextureProof);
Id65AuthoringManifest manifest =
    Id65V2NativeTextureCompositionCompiler.CreateMinimalV2T66Manifest();

Id65CompiledV2NativeTextureComposition first =
    Id65V2NativeTextureCompositionCompiler.Compile(manifest, v2Model, v2, witness);
Id65CompiledV2NativeTextureComposition repeat =
    Id65V2NativeTextureCompositionCompiler.Compile(
        Id65V2NativeTextureCompositionCompiler.CreateMinimalV2T66Manifest(),
        v2Model,
        v2,
        Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(oldStaticTextureProof));

Require(first.ProfileId == Id65V2NativeTextureCompositionCompiler.ProfileId &&
        first.Manifest.ProfileId == Id65V2NativeTextureCompositionCompiler.ManifestProfileId &&
        first.Witness.ProfileId == Id65V2NativeTextureCompositionCompiler.WitnessProfileId &&
        first.ManifestSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedManifestSha256 &&
        first.WitnessSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 &&
        first.SourceRow80Sha256 == Id65V2NativeTextureCompositionCompiler.ExpectedSourceRow80Sha256 &&
        first.OutputRow80Sha256 == Id65V2NativeTextureCompositionCompiler.ExpectedOutputRow80Sha256 &&
        first.SourceModelSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedSourceModelSha256 &&
        first.OutputModelSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256 &&
        first.SourceTexturePagesSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedSourceTexturePagesSha256 &&
        first.OutputTexturePagesSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256 &&
        first.DiffManifestSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedDiffManifestSha256 &&
        first.RelocationMapSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedRelocationMapSha256 &&
        first.HandleRebaseMapSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedHandleRebaseMapSha256 &&
        first.DeterministicPlanSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedDeterministicPlanSha256,
    "The exact v2/T66 source or output identity changed.");
Require(first.Manifest.Textures.Count == 2 &&
        first.Manifest.Textures.Single(item => item.Id == "texture.locked.record25").DestinationRecordIndex == 25 &&
        first.Manifest.Textures.Single(item => item.Id ==
            Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId) is var privateTexture &&
        privateTexture.Disposition == Id65AuthoringIntentDisposition.AuthorExact &&
        privateTexture.DestinationRecordIndex == 66 &&
        privateTexture.SourceHandle == Id65V2NativeTextureCompositionCompiler.PrivateTextureSourceHandle &&
        first.Manifest.RenderFaces.Single().TextureIntentId == privateTexture.Id,
    "The locked T25/private T66 manifest allocator changed.");

Id65V2NativeTextureAllocationReadback allocation = first.Allocation;
Require(allocation.SourceTextureCount == 66 && allocation.OutputTextureCount == 67 &&
        allocation.FirstPrivateTextureId == 66 && allocation.LastPrivateTextureId == 66 &&
        allocation.RecordGrowthBytes == 0xB8 &&
        allocation.SourceTextureComponentBytes == 0x2F78 && allocation.OutputTextureComponentBytes == 0x3030 &&
        allocation.SourceUsedModelBytes == 0x9457C && allocation.OutputUsedModelBytes == 0x94634 &&
        allocation.SourceZeroTailBytes == 0x284 && allocation.OutputZeroTailBytes == 0x1CC &&
        allocation.SevenBitRemainingIds == 61 && allocation.TailRemainingWholeRecords == 2,
    "The exact T66 allocator/capacity readback changed.");
Id65V2NativeTextureFaceBindingReadback face = first.FaceBinding;
Require(face.RenderFaceId == "terrain.remote-pad.face.0" &&
        face.TextureIntentId == Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId &&
        face.SectorIndex == 216 && face.LowDetailFaceIndex == 0 && face.HighDetailFaceIndex == 0 &&
        face.SourceLowFaceModelOffset == 0x2B454 && face.OutputLowFaceModelOffset == 0x2B50C &&
        face.SourceHighFaceModelOffset == 0x2B480 && face.OutputHighFaceModelOffset == 0x2B538 &&
        face.SourceHighTextureWordModelOffset == 0x2B488 && face.OutputHighTextureWordModelOffset == 0x2B540 &&
        face.SourceLowFaceHex == "0082100000821000" && face.OutputLowFaceHex == face.SourceLowFaceHex &&
        face.SourceHighFaceHex == "000001020101000219024001080A1000" &&
        face.OutputHighFaceHex == "000001020101000242024001080A1000" &&
        face.SourceTextureId == 25 && face.OutputTextureId == 66 &&
        face.SourceT25UseCount == 63 && face.OutputT25UseCount == 62 && face.OutputT66UseCount == 1 &&
        face.HighTextureIdOnlyChanged && face.LowFacePreserved && face.ExplicitHpLpPairingVerified,
    "The exact sector-216 HP/LP private T66 binding changed.");

byte[] outputModel = first.CopyOutputModel();
Require(Hash(outputModel.AsSpan(0x428, 16)) == Id65V2NativeTextureCompositionCompiler.ExpectedPrivateLowRowSha256 &&
        Hash(outputModel.AsSpan(0x2F88, 168)) == Id65V2NativeTextureCompositionCompiler.ExpectedPrivateHighRowSha256,
    "The exact private T66 LQ/HQ rows or output offsets changed.");
Require(first.ChangedTexturePageByteCount == 402_194 && first.ChangedModelByteCount == 517_811 &&
        first.ChangedRow80ByteCount == 920_005 && first.DiffRanges.Count == 55_243 &&
        first.DiffRanges.Count(item => item.Space == "texture-pages") == 7_949 &&
        first.DiffRanges.Count(item => item.Space == "model") == 47_294,
    "The exact page/model/row80 diff boundary changed.");
Require(first.Relocations.Count == 9 &&
        first.Relocations.Single(item => item.StableId == "component.texture") is var textureComponent &&
        textureComponent.SourceRelativeOffset == 0 && textureComponent.SourceByteLength == 0x2F78 &&
        textureComponent.OutputRelativeOffset == 0 && textureComponent.OutputByteLength == 0x3030 &&
        !textureComponent.ContentsPreserved &&
        first.Relocations.Single(item => item.StableId == "component.environment") is var environment &&
        environment.SourceRelativeOffset == 0x2F78 && environment.OutputRelativeOffset == 0x3030 &&
        !environment.ContentsPreserved &&
        first.Relocations.Single(item => item.StableId == "component.occlusion").ContentsPreserved &&
        first.Relocations.Single(item => item.StableId == "component.collision").ContentsPreserved &&
        first.Relocations[^1].OutputRelativeOffset + first.Relocations[^1].OutputByteLength == 0x94634,
    "The exact +0xB8 component rebase/preservation map changed.");
Require(first.HandleRebases.Count == 19 &&
        first.HandleRebases.Single(item => item.StableId ==
            Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId).Source is null &&
        first.HandleRebases.Single(item => item.StableId ==
            Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId).Output ==
            new Id65LogicalAddress("texture-record", 66, 0),
    "The private T66 stable-handle rebase changed.");

byte[] sourceRow80 = first.CopySourceRow80();
byte[] applied = Id65V2NativeTextureCompositionCompiler.ApplyRow80Transactional(first, sourceRow80, false);
byte[] reversed = Id65V2NativeTextureCompositionCompiler.ApplyRow80Transactional(first, applied, true);
Require(applied.SequenceEqual(first.CopyOutputRow80()) && reversed.SequenceEqual(sourceRow80) &&
        Id65V2NativeTextureCompositionCompiler.InvertRelocations(
            Id65V2NativeTextureCompositionCompiler.InvertRelocations(first.Relocations)).SequenceEqual(first.Relocations) &&
        Id65V2NativeTextureCompositionCompiler.InvertHandleRebases(
            Id65V2NativeTextureCompositionCompiler.InvertHandleRebases(first.HandleRebases)).SequenceEqual(first.HandleRebases),
    "The exact row80/relocation/handle inverse changed.");

int rejectionCount = 0;
ExpectReject("manifest profile", () => Compile(CopyManifest(manifest, profileId: "wrong")), "manifest");
ExpectReject("manual private id", () => Compile(CopyManifest(manifest, textures:
    manifest.Textures.Select(item => item.Id == Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId
        ? item with { DestinationRecordIndex = 67 }
        : item))), "manifest");
ExpectReject("unproven music", () => Compile(CopyManifest(manifest,
    music: manifest.Music with { Disposition = Id65AuthoringIntentDisposition.AuthorExact, TrackId = 0 })), "manifest");
ExpectReject("positive-wound model", () => Id65V2NativeTextureCompositionCompiler.Compile(
    manifest,
    v2Model,
    v2 with { OutputModelSha256 = UnusedLevel65NativeTextureAuthoringContract.ExpectedSourceModelSha256 },
    witness), "negative-winding");
ExpectReject("weakened collision", () => Id65V2NativeTextureCompositionCompiler.Compile(
    manifest, v2Model, v2 with { Collision = v2.Collision with { UpwardWinding = true } }, witness), "negative-winding");
ExpectReject("runtime-controlled donor", () => Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(
    oldStaticTextureProof with { Donor = oldStaticTextureProof.Donor with { RuntimeControlled = true } }), "witness");
ExpectReject("weakened page proof", () => Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(
    oldStaticTextureProof with
    {
        Composition = oldStaticTextureProof.Composition with
        {
            GlobalPacking = oldStaticTextureProof.Composition.GlobalPacking with
            {
                PackingProof = oldStaticTextureProof.Composition.GlobalPacking.PackingProof with
                {
                    ExactPaletteReadbackVerified = false
                }
            }
        }
    }), "palette");
ExpectReject("wrong witness profile", () => Compile(witness: CopyWitness(witness, profileId: "wrong")), "witness");
ExpectReject("wrong witness page hash", () => Compile(witness: CopyWitness(
    witness, outputPagesSha256: new string('0', 64))), "witness");
ExpectReject("donor manifest metadata", () => Compile(witness: CopyWitness(
    witness, donorManifestId: "wrong-donor-manifest")), "donor manifest");
ExpectReject("donor complete-record metadata", () => Compile(witness: CopyWitness(
    witness, donorCompleteRecordSha256: new string('0', 64))), "complete-record");
ExpectReject("stored canonical identity", () => Compile(witness: CopyWitness(
    witness, canonicalSha256: new string('0', 64))), "canonical identity");

IReadOnlyList<Id65V2NativeTexturePagePatch> pagePatches = witness.CopyPagePatches();
byte[] wrongPageBefore = pagePatches[0].CopyBefore();
wrongPageBefore[0] ^= 1;
Id65V2NativeTexturePagePatch wrongPreimage = new(
    pagePatches[0].RelativeOffset,
    wrongPageBefore,
    pagePatches[0].CopyAfter(),
    Hash(wrongPageBefore),
    pagePatches[0].AfterSha256,
    pagePatches[0].Owner);
ExpectReject("page preimage", () => Compile(witness: CopyWitness(
    witness, pagePatches: [wrongPreimage, .. pagePatches.Skip(1)])), "canonical identity");
byte[] wrongPageAfter = pagePatches[0].CopyAfter();
wrongPageAfter[0] ^= 1;
Id65V2NativeTexturePagePatch wrongAfterimage = new(
    pagePatches[0].RelativeOffset,
    pagePatches[0].CopyBefore(),
    wrongPageAfter,
    pagePatches[0].BeforeSha256,
    Hash(wrongPageAfter),
    pagePatches[0].Owner);
ExpectReject("page afterimage", () => Compile(witness: CopyWitness(
    witness, pagePatches: [wrongAfterimage, .. pagePatches.Skip(1)])), "canonical identity");
Id65V2NativeTexturePagePatch escaped = new(
    0xDE000,
    [0],
    [1],
    Hash([0]),
    Hash([1]),
    "terrain-texture-global-repack-data");
ExpectReject("page escape", () => Compile(witness: CopyWitness(
    witness, pagePatches: [.. pagePatches, escaped])), "patch count");
Id65V2NativeTexturePagePatch wrongOwner = new(
    pagePatches[0].RelativeOffset,
    pagePatches[0].CopyBefore(),
    pagePatches[0].CopyAfter(),
    pagePatches[0].BeforeSha256,
    pagePatches[0].AfterSha256,
    "wrong-owner");
ExpectReject("page patch owner", () => Compile(witness: CopyWitness(
    witness, pagePatches: [wrongOwner, .. pagePatches.Skip(1)])), "canonical owner");

IReadOnlyList<Id65V2NativeTexturePackedRow> rows = witness.CopyPackedRows();
Id65V2NativeTexturePackedRow original = rows.First(item => !item.Synthetic);
Id65V2NativeTexturePackedRow wrongOriginalProvenance = new(
    original.TextureId,
    70,
    original.DonorTextureId,
    false,
    original.CopyLow(),
    original.CopyHigh(),
    original.LowSha256,
    original.HighSha256);
ExpectReject("non-synthetic row provenance", () => Compile(witness: CopyWitness(
    witness,
    packedRows: [wrongOriginalProvenance, .. rows.Where(item => item != original)])), "provenance");
Id65V2NativeTexturePackedRow synthetic = rows.Single(item => item.Synthetic);
byte[] wrongLow = synthetic.CopyLow();
wrongLow[0] ^= 1;
Id65V2NativeTexturePackedRow wrongSynthetic = new(
    synthetic.TextureId,
    synthetic.DonorWadEntry,
    synthetic.DonorTextureId,
    true,
    wrongLow,
    synthetic.CopyHigh(),
    Hash(wrongLow),
    synthetic.HighSha256);
ExpectReject("packed T66 row", () => Compile(witness: CopyWitness(
    witness, packedRows: [.. rows.Where(item => !item.Synthetic), wrongSynthetic])), "synthetic T66");
Id65V2NativeTexturePackedRow wrongId = new(
    67,
    synthetic.DonorWadEntry,
    synthetic.DonorTextureId,
    true,
    synthetic.CopyLow(),
    synthetic.CopyHigh(),
    synthetic.LowSha256,
    synthetic.HighSha256);
ExpectReject("non-contiguous private id", () => Compile(witness: CopyWitness(
    witness, packedRows: [.. rows.Where(item => !item.Synthetic), wrongId])), "out-of-range");

ExpectReject("model capacity", () => Compile(limits: new(ModelByteCapacity: 0x947FF)), "model byte capacity");
ExpectReject("tail capacity", () => Compile(limits: new(AvailableV2TailBytes: 0xB7)), "tail capacity");
ExpectReject("used-model capacity", () => Compile(limits: new(MaximumOutputUsedModelBytes: 0x94633)), "used-model capacity");
ExpectReject("seven-bit capacity", () => Compile(limits: new(MaximumNativeTextureId: 65)), "seven-bit");
ExpectReject("unproven record count", () => Compile(limits: new(AuthorizedWitnessRecordCount: 2)), "one-record");

byte[] tamperedSource = first.CopySourceRow80();
tamperedSource[0x800] ^= 1;
ExpectReject("transaction preimage", () =>
    Id65V2NativeTextureCompositionCompiler.ApplyRow80Transactional(first, tamperedSource, false), "preimage");
byte[] tamperedOutput = first.CopyOutputRow80();
tamperedOutput[0xDE800 + 0x2B540] ^= 1;
ExpectReject("transaction afterimage", () =>
    Id65V2NativeTextureCompositionCompiler.ApplyRow80Transactional(first, tamperedOutput, true), "afterimage");

Require(rejectionCount == 26, $"The complete T66 rejection matrix ran {rejectionCount}, expected 26.");
Require(first.ManifestSha256 == repeat.ManifestSha256 && first.WitnessSha256 == repeat.WitnessSha256 &&
        first.OutputRow80Sha256 == repeat.OutputRow80Sha256 &&
        first.OutputTexturePagesSha256 == repeat.OutputTexturePagesSha256 &&
        first.OutputModelSha256 == repeat.OutputModelSha256 &&
        first.DiffManifestSha256 == repeat.DiffManifestSha256 &&
        first.RelocationMapSha256 == repeat.RelocationMapSha256 &&
        first.HandleRebaseMapSha256 == repeat.HandleRebaseMapSha256 &&
        first.DeterministicPlanSha256 == repeat.DeterministicPlanSha256 &&
        first.DiffRanges.SequenceEqual(repeat.DiffRanges) &&
        first.Relocations.SequenceEqual(repeat.Relocations) &&
        first.HandleRebases.SequenceEqual(repeat.HandleRebases),
    "Two exact T66 compiles were not deterministic.");
Require(first.TypedWitnessProjectionVerified && first.DestinationPrivateRecordVerified &&
        first.DestinationPrivatePageAllocationVerified && first.ExactHpLpBindingReadbackVerified &&
        first.CollisionAndOcclusionPreserved && first.TargetEntryHeaderPreserved &&
        first.ProtectedRow80SubfilesPreserved && first.RetailWadEntriesExcluded &&
        first.ExecutableExcluded && first.ScusPreserved && first.ExactInverseVerified &&
        first.DeterministicReadbackRequired && first.LaterSinglePassCompositeInputAvailable &&
        !first.CrossSliceCompositionAuthorized && !first.AfterimageStackingAuthorized &&
        !first.ExactV2OnlyTerrainCompilerAfterimageCompatible &&
        !first.WritesFileSystem && !first.WritesDiscImage && !first.WritesCue &&
        !first.AppIntegrated && !first.CreateBinEnabled && !first.NormalCreateBinEnabled &&
        !first.RuntimeCandidateAuthorized && !first.RetiredPublisherCalled &&
        !first.ReleaseIntegrated && !first.PromotionAuthorized && !first.Publishable,
    "A pure/no-stacking/no-writer/no-promotion T66 safety flag changed.");
Require(!witness.ContainsFoundationModelBytes && !witness.ContainsFoundationFaceBinding &&
        !witness.ContainsAppendAfterimage && !witness.ContainsPublisherState &&
        witness.LaterSinglePassCompositeInputAvailable && !witness.CrossSliceCompositionAuthorized &&
        !witness.AfterimageStackingAuthorized,
    "The typed witness leaked retired foundation/publisher state or stacking authority.");
Require(SnapshotsEqual(lockedBefore, SnapshotDirectory(Path.GetDirectoryName(lockedBaseImage)!)) &&
        SnapshotsEqual(foundationBefore, SnapshotDirectory(Path.GetDirectoryName(foundationImage)!)),
    "The pure T66 compiler changed or published beside an input artifact.");

Console.WriteLine(
    "PASS Id65V2NativeTextureCompositionCompilerSmoke: " +
    $"manifest={first.ManifestSha256}; witness={first.WitnessSha256}; " +
    $"pages={first.OutputTexturePagesSha256}; model={first.OutputModelSha256}; row80={first.OutputRow80Sha256}; " +
    $"changed={first.ChangedRow80ByteCount}; ranges={first.DiffRanges.Count}; diff={first.DiffManifestSha256}; " +
    $"relocations={first.RelocationMapSha256}; rebases={first.HandleRebaseMapSha256}; " +
    $"plan={first.DeterministicPlanSha256}; rejects={rejectionCount}; " +
    "typed T66 proof, exact inverse, pure in-memory, cross-slice composition/stacking unauthorized, " +
    "no writer/CUE/App/CreateBIN/runtime/release/promotion.");

void Compile(
    Id65AuthoringManifest? selectedManifest = null,
    Id65V2NativeTextureWitness? witness = null,
    Id65V2NativeTextureCompilerLimits? limits = null) =>
    Id65V2NativeTextureCompositionCompiler.Compile(
        selectedManifest ?? manifest,
        v2Model,
        v2,
        witness ?? Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(oldStaticTextureProof),
        limits);

void ExpectReject(string label, Action action, string requiredText)
{
    bool rejected = false;
    try
    {
        action();
    }
    catch (Exception ex) when (ex.ToString().Contains(requiredText, StringComparison.OrdinalIgnoreCase))
    {
        rejected = true;
    }
    Require(rejected, $"Atomic rejection `{label}` was not observed with text `{requiredText}`.");
    rejectionCount++;
}

static Id65V2NativeTextureWitness CopyWitness(
    Id65V2NativeTextureWitness source,
    string? profileId = null,
    string? donorManifestId = null,
    string? donorCompleteRecordSha256 = null,
    string? outputPagesSha256 = null,
    IReadOnlyList<Id65V2NativeTexturePackedRow>? packedRows = null,
    IReadOnlyList<Id65V2NativeTexturePagePatch>? pagePatches = null,
    string? canonicalSha256 = null) =>
    new(
        profileId ?? source.ProfileId,
        donorManifestId ?? source.DonorManifestId,
        donorCompleteRecordSha256 ?? source.DonorCompleteRecordSha256,
        source.SourceTexturePagesSha256,
        outputPagesSha256 ?? source.OutputTexturePagesSha256,
        source.SourceTextureComponentSha256,
        source.OutputTextureComponentSha256,
        source.SourceTextureCount,
        source.OutputTextureCount,
        source.PrivateTextureId,
        source.DonorWadEntry,
        source.DonorTextureId,
        source.MaterialTemplateTextureId,
        packedRows ?? source.CopyPackedRows(),
        pagePatches ?? source.CopyPagePatches(),
        canonicalSha256 ?? source.CanonicalSha256);

static Id65AuthoringManifest CopyManifest(
    Id65AuthoringManifest source,
    string? profileId = null,
    IEnumerable<Id65TextureIntent>? textures = null,
    Id65MusicIntent? music = null) =>
    new(
        profileId ?? source.ProfileId,
        source.LockedBase,
        source.Sectors,
        source.Vertices,
        source.RenderFaces,
        source.Collision,
        source.Occlusion,
        textures ?? source.Textures,
        source.Mobys,
        source.Spawn,
        music ?? source.Music,
        source.Totals,
        source.Exit,
        source.Save);

static IReadOnlyDictionary<string, FileState> SnapshotDirectory(string path) =>
    Directory.GetFiles(path)
        .Order(StringComparer.Ordinal)
        .ToDictionary(
            file => Path.GetFileName(file),
            file => new FileState(new FileInfo(file).Length, File.GetLastWriteTimeUtc(file), HashFile(file)),
            StringComparer.Ordinal);

static bool SnapshotsEqual(
    IReadOnlyDictionary<string, FileState> left,
    IReadOnlyDictionary<string, FileState> right) =>
    left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out FileState? value) && value == pair.Value);

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexStringLower(SHA256.HashData(stream));
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(long ByteLength, DateTime LastWriteUtc, string Sha256);
