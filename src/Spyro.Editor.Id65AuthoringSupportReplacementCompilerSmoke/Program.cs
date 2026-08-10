using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string foundationImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin");
Require(File.Exists(lockedImagePath), $"Missing exact locked display-name image: {lockedImagePath}");
Require(File.Exists(foundationImagePath), $"Missing exact foundation T66 witness image: {foundationImagePath}");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState foundationBefore = SnapshotFile(foundationImagePath);
IReadOnlyList<string> lockedDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
IReadOnlyList<string> foundationDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!);

byte[] directRow80 = ReadRootFile(lockedImagePath, "WAD.WAD", 0x6936800, 0x2E2000);
UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2 =
    UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.BuildStaticPlan(lockedImagePath);
Require(directRow80.SequenceEqual(v2.SourceData),
    "The support smoke direct row 80 differs from the exact locked v2 proof source.");
UnusedLevel65NativeTextureStaticPlan oldTextureProof =
    UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(foundationImagePath);
Id65V2NativeTextureWitness textureWitness =
    Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(oldTextureProof);
Id65AuthoringSupportLockedSource lockedSource =
    Id65AuthoringSupportReplacementCompiler.CaptureLockedSource(directRow80, textureWitness);
IReadOnlyList<Id65V2NativeTexturePackedRow> completeTextureRows = lockedSource.CopyTextureRows();
IReadOnlyList<Id65V2NativeTexturePagePatch> completePagePatches = lockedSource.CopyPagePatches();
Require(lockedSource.TextureProjectionSha256 ==
            "899bc97cdbf7a3f9da2fa5132d25665fb6cf6f2596d7b49d68733ca0e52212a7" &&
        lockedSource.TextureProjectionSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedTextureProjectionSha256 &&
        completeTextureRows.Count == 67 &&
        completeTextureRows.Select(item => item.TextureId).SequenceEqual(Enumerable.Range(0, 67)) &&
        completePagePatches.Count == 7_949 &&
        completePagePatches.Sum(item => item.ByteLength) == 402_194,
    "The frozen complete T66 projection identity, cardinality, or page coverage changed.");
Id65V2NativeTexturePackedRow completeT1 = completeTextureRows.Single(item => item.TextureId == 1);
Require(!completeT1.Synthetic && completeT1.DonorWadEntry == 80 && completeT1.DonorTextureId == 1 &&
        completeT1.LowSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedLockedT1LowRowSha256 &&
        completeT1.HighSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedLockedT1HighRowSha256,
    "The explicitly materialized locked T1 projection row changed.");

Id65AuthoringSupportManifest empty = Id65AuthoringSupportReplacementCompiler.CreateEmptyManifest();
Id65AuthoringSupportManifest core = Id65AuthoringSupportReplacementCompiler.AddCoreSupport(empty);
Id65AuthoringSupportManifest full = Id65AuthoringSupportReplacementCompiler.AddEnemyBay(core);
Id65AuthoringSupportManifest coreFromFull = Id65AuthoringSupportReplacementCompiler.RemoveEnemyBay(full);
Id65AuthoringSupportManifest emptyFromCore = Id65AuthoringSupportReplacementCompiler.RemoveCore(core);
Require(core.CanonicalJson == coreFromFull.CanonicalJson &&
        core.CanonicalSha256 == coreFromFull.CanonicalSha256 &&
        empty.CanonicalJson == emptyFromCore.CanonicalJson &&
        empty.CanonicalSha256 == emptyFromCore.CanonicalSha256,
    "Support manifest add/remove factories do not return exact canonical states.");
Require(empty.CanonicalSha256 == "fc162fc5db6afa30952ca749ac4f60ce98cee6d9bce1ff8aa3f7ed3ac856182f" &&
        core.CanonicalSha256 == "aaac3076e2246aeb481e25241fcef9d564aa8c338bef07b0bdf83af11726c270" &&
        full.CanonicalSha256 == "4ccb922f43a9ed2551ce700f56270b6c1207adf1c49eb26984b96e0bdacdd0fb" &&
        empty.CanonicalSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedEmptyManifestSha256 &&
        core.CanonicalSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedCoreManifestSha256 &&
        full.CanonicalSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedFullManifestSha256 &&
        empty.AuthoredTranslationWorld == 752 && empty.AuthoredTranslationRaw == 12_032 &&
        core.AuthoredTranslationWorld == 752 && core.AuthoredTranslationRaw == 12_032 &&
        full.AuthoredTranslationWorld == 752 && full.AuthoredTranslationRaw == 12_032,
    "A frozen support manifest identity or authored translation unit changed.");

Id65CompiledSupportReplacement addCore = Compile(empty, core);
Id65CompiledSupportReplacement addBay = Compile(core, full);
Id65CompiledSupportReplacement removeBay = Compile(full, core);
Id65CompiledSupportReplacement removeCore = Compile(core, empty);
Id65CompiledSupportReplacement addCoreRepeat = Compile(
    Id65AuthoringSupportReplacementCompiler.CreateEmptyManifest(),
    Id65AuthoringSupportReplacementCompiler.AddCoreSupport(
        Id65AuthoringSupportReplacementCompiler.CreateEmptyManifest()));

Require(addCore.CopyOutputRow80().SequenceEqual(addBay.CopySourceRow80()) &&
        addBay.CopyOutputRow80().SequenceEqual(removeBay.CopySourceRow80()) &&
        removeBay.CopyOutputRow80().SequenceEqual(removeCore.CopySourceRow80()) &&
        removeCore.CopyOutputRow80().SequenceEqual(addCore.CopySourceRow80()),
    "The exact Empty/Core4/Full8 state chain is not byte-continuous in both directions.");

Require(addCore.Transition == Id65SupportTransitionKind.AddCoreSupport &&
        addBay.Transition == Id65SupportTransitionKind.AddEnemyBay &&
        removeBay.Transition == Id65SupportTransitionKind.RemoveEnemyBay &&
        removeCore.Transition == Id65SupportTransitionKind.RemoveCore,
    "The exact support transition graph changed.");
Require(addCore.SourceRow80Sha256 == Id65AuthoringSupportReplacementCompiler.ExpectedEmptyRow80Sha256 &&
        addCore.OutputRow80Sha256 == Id65AuthoringSupportReplacementCompiler.ExpectedCoreRow80Sha256 &&
        addBay.SourceRow80Sha256 == Id65AuthoringSupportReplacementCompiler.ExpectedCoreRow80Sha256 &&
        addBay.OutputRow80Sha256 == Id65AuthoringSupportReplacementCompiler.ExpectedFullRow80Sha256 &&
        removeBay.SourceRow80Sha256 == Id65AuthoringSupportReplacementCompiler.ExpectedFullRow80Sha256 &&
        removeBay.OutputRow80Sha256 == Id65AuthoringSupportReplacementCompiler.ExpectedCoreRow80Sha256 &&
        removeCore.SourceRow80Sha256 == Id65AuthoringSupportReplacementCompiler.ExpectedCoreRow80Sha256 &&
        removeCore.OutputRow80Sha256 == Id65AuthoringSupportReplacementCompiler.ExpectedEmptyRow80Sha256,
    "A frozen support row-80 state identity changed.");
Require(addCore.SourceModelSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedEmptyModelSha256 &&
        addCore.OutputModelSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedCoreModelSha256 &&
        addBay.OutputModelSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedFullModelSha256 &&
        removeBay.OutputModelSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedCoreModelSha256 &&
        removeCore.OutputModelSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedEmptyModelSha256,
    "A frozen support model identity changed.");

Require(addCore.OutputState.EnvironmentSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedCoreEnvironmentSha256 &&
        addCore.OutputState.CollisionSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedCoreCollisionSha256 &&
        addCore.OutputState.CollisionTreeSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedCoreTreeSha256 &&
        addCore.OutputState.CollisionBlocksSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedCoreBlocksSha256 &&
        addCore.OutputState.OcclusionSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedCoreOcclusionSha256 &&
        addBay.OutputState.EnvironmentSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedFullEnvironmentSha256 &&
        addBay.OutputState.CollisionSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedFullCollisionSha256 &&
        addBay.OutputState.CollisionTreeSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedFullTreeSha256 &&
        addBay.OutputState.CollisionBlocksSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedFullBlocksSha256 &&
        addBay.OutputState.OcclusionSha256 ==
            Id65AuthoringSupportReplacementCompiler.ExpectedFullOcclusionSha256,
    "A support environment/collision/occlusion component pin changed.");

Require(addCore.OutputState.SectorCount == 4 && addBay.OutputState.SectorCount == 8 &&
        addCore.OutputState.ActiveCollisionCellCount == 16 &&
        addBay.OutputState.ActiveCollisionCellCount == 32 &&
        addCore.OutputState.CollisionBlocksUsedBytes == 0x82 &&
        addBay.OutputState.CollisionBlocksUsedBytes == 0x10E &&
        addCore.OutputState.InheritedActiveLeavesCleared &&
        addBay.OutputState.InheritedActiveLeavesCleared &&
        addCore.OutputState.ExactHpLpPairing && addBay.OutputState.ExactHpLpPairing &&
        addCore.OutputState.ExactSeams && addBay.OutputState.ExactSeams &&
        addCore.OutputState.ProtectedComponentsPreserved &&
        addBay.OutputState.ProtectedComponentsPreserved &&
        !addCore.OutputState.RuntimeAccepted && !addBay.OutputState.RuntimeAccepted,
    "The support semantic readback changed.");
Require(core.Sectors.Count == 4 && full.Sectors.Count == 8 &&
        core.Sectors.All(item => item.Vertices.Count == 5 && item.Faces.Count == 4) &&
        full.Sectors.All(item => item.Vertices.Count == 5 && item.Faces.Count == 4) &&
        core.Seams.Count == 4 && full.Seams.Count == 10 &&
        core.CollisionCells.Count == 16 && full.CollisionCells.Count == 32 &&
        core.CollisionCells[^1].BlockWordOffset == 62 &&
        full.CollisionCells[^1].BlockWordOffset == 132 &&
        core.CollisionCells.All(item => IsStrictlyDescending(item.TriangleIndexes)) &&
        full.CollisionCells.All(item => IsStrictlyDescending(item.TriangleIndexes)) &&
        full.Sectors.SelectMany(item => item.Faces).Select(item => item.CollisionTriangleIndex)
            .SequenceEqual(new[]
            {
                13_995, 19_807, 19_806, 19_805,
                19_804, 19_803, 19_802, 19_801,
                19_800, 19_799, 19_798, 19_797,
                19_796, 19_795, 19_794, 19_793,
                19_792, 19_791, 19_790, 19_789,
                19_788, 19_787, 19_786, 19_785,
                19_784, 19_783, 19_782, 19_781,
                19_780, 19_779, 19_778, 19_777
            }),
    "The exact center-fan sector/seam/collision-row ledger changed.");

foreach (Id65CompiledSupportReplacement compiled in new[] { addCore, addBay, removeBay, removeCore })
{
    FrozenSmokeTransitionPins frozen = FrozenPinsFor(compiled.Transition);
    Require(compiled.ChangedByteCount == frozen.ChangedByteCount &&
            compiled.DiffRangeCount == frozen.DiffRangeCount &&
            compiled.DiffManifestSha256 == frozen.DiffManifestSha256 &&
            compiled.OwnedRangeMapSha256 == frozen.OwnedRangeMapSha256 &&
            compiled.RelocationMapSha256 == frozen.RelocationMapSha256 &&
            compiled.RebaseMapSha256 == frozen.RebaseMapSha256 &&
            compiled.TransactionSha256 == frozen.TransactionSha256 &&
            compiled.DeterministicPlanSha256 == frozen.DeterministicPlanSha256,
        $"The independent frozen {compiled.Transition} transition pins changed.");
    Require(compiled.OwnedRanges.Count == compiled.DiffRangeCount &&
            compiled.OwnedRanges.Sum(item => item.ByteLength) == compiled.ChangedByteCount &&
            compiled.DiffRanges.Sum(item => item.ByteLength) == compiled.ChangedByteCount &&
            compiled.OwnedRanges.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() ==
                compiled.OwnedRanges.Count &&
            compiled.Relocations.Count == 9 &&
            compiled.Relocations.Where(item => item.ContentsPreserved).Select(item => item.StableId)
                .SequenceEqual(new[]
                {
                    "component.texture", "component.special-surface", "component.cyclorama",
                    "component.portal-table", "component.particles", "component.sound"
                }) &&
            compiled.HandleRebases.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() ==
                compiled.HandleRebases.Count,
        "A support owned diff, relocation, or stable-handle map changed.");
    foreach ((string stableId, string kind, Id65LogicalAddress address) in new[]
             {
                 (Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId,
                     "texture-record", new Id65LogicalAddress("texture-record", 66, 0)),
                 ("texture.locked.record25", "texture-record", new Id65LogicalAddress("texture-record", 25, 0)),
                 ("spawn.remote-pad", "landing-record", new Id65LogicalAddress("landing-record", 0, 0)),
                 ("moby.player-anchor.t92", "moby-row", new Id65LogicalAddress("moby-row", 92, 0)),
                 ("music.slot35", "music-slot", new Id65LogicalAddress("music-slot", 35, 0)),
                 ("totals.slot65", "totals-slot", new Id65LogicalAddress("level-slot", 65, 0)),
                 ("exit.slot65", "exit-slot", new Id65LogicalAddress("level-slot", 65, 0)),
                 ("save.slot65", "save-slot", new Id65LogicalAddress("level-slot", 65, 0))
             })
    {
        Id65StableHandleRebase rebase = compiled.HandleRebases.Single(item => item.StableId == stableId);
        Require(rebase.Kind == kind && rebase.Source == address && rebase.Output == address,
            $"Preserved stable handle `{stableId}` changed.");
    }
    byte[] applied = Id65AuthoringSupportReplacementCompiler.ApplyTransactional(
        compiled, compiled.CopySourceRow80(), reverse: false);
    byte[] rolledBack = Id65AuthoringSupportReplacementCompiler.ApplyTransactional(
        compiled, applied, reverse: true);
    Require(applied.SequenceEqual(compiled.CopyOutputRow80()) &&
            rolledBack.SequenceEqual(compiled.CopySourceRow80()),
        "A support transition failed exact forward/rollback application.");
}

Id65CompiledSupportReplacement addCoreInverse =
    Id65AuthoringSupportReplacementCompiler.Invert(addCore);
Id65CompiledSupportReplacement addBayInverse =
    Id65AuthoringSupportReplacementCompiler.Invert(addBay);
Id65CompiledSupportReplacement addCoreTwice =
    Id65AuthoringSupportReplacementCompiler.Invert(addCoreInverse);
Id65CompiledSupportReplacement addBayTwice =
    Id65AuthoringSupportReplacementCompiler.Invert(addBayInverse);
Require(Equivalent(addCoreInverse, removeCore) && Equivalent(addBayInverse, removeBay) &&
        Equivalent(addCoreTwice, addCore) && Equivalent(addBayTwice, addBay) &&
        Equivalent(addCoreRepeat, addCore),
    "A support transition inverse, double inverse, or repeated compile changed.");

byte[] defensiveRow = addBay.CopyOutputRow80();
defensiveRow[0] ^= 1;
byte[] defensiveModel = addBay.CopyOutputModel();
defensiveModel[0] ^= 1;
byte[] defensiveBefore = addBay.OwnedRanges[0].CopyBefore();
defensiveBefore[0] ^= 1;
Require(Hash(addBay.CopyOutputRow80()) == addBay.OutputRow80Sha256 &&
        Hash(addBay.CopyOutputModel()) == addBay.OutputModelSha256 &&
        Hash(addBay.OwnedRanges[0].CopyBefore()) == addBay.OwnedRanges[0].BeforeSha256,
    "A support state or ownership byte array escaped defensive copying.");

int rejectionCount = 0;
void Reject(string label, Action action)
{
    bool rejected = false;
    try
    {
        action();
    }
    catch (Exception)
    {
        rejected = true;
    }
    Require(rejected, $"Atomic support rejection `{label}` was not observed.");
    rejectionCount++;
}

byte[] wrongRow80 = directRow80.ToArray();
wrongRow80[0] ^= 1;
Reject("wrong locked row80", () =>
    Id65AuthoringSupportReplacementCompiler.CaptureLockedSource(wrongRow80, textureWitness));
byte[] compiledAfterimage = addCore.CopyOutputRow80();
Reject("compiled afterimage as locked source", () =>
    Id65AuthoringSupportReplacementCompiler.CaptureLockedSource(compiledAfterimage, textureWitness));

IReadOnlyList<Id65V2NativeTexturePackedRow> textureRows = textureWitness.CopyPackedRows();
IReadOnlyList<Id65V2NativeTexturePagePatch> pagePatches = textureWitness.CopyPagePatches();
Id65V2NativeTextureWitness missingRowWitness = CopyWitness(
    textureWitness,
    packedRows: textureRows.Skip(1).ToArray());
Reject("missing T66 row", () =>
    Id65AuthoringSupportReplacementCompiler.CaptureLockedSource(directRow80, missingRowWitness));
Id65AuthoringSupportLockedSource forgedProjection = new(
    directRow80,
    textureRows,
    pagePatches,
    Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256);
Reject("stored projection missing materialized locked T1", () =>
    Id65AuthoringSupportReplacementCompiler.CompileTransition(forgedProjection, empty, core));
Id65V2NativeTexturePackedRow lockedT1 = completeTextureRows.Single(item => item.TextureId == 1);
byte[] badT1Low = lockedT1.CopyLow();
badT1Low[0] ^= 1;
Id65V2NativeTexturePackedRow badT1 = new(
    lockedT1.TextureId,
    lockedT1.DonorWadEntry,
    lockedT1.DonorTextureId,
    lockedT1.Synthetic,
    badT1Low,
    lockedT1.CopyHigh(),
    Hash(badT1Low),
    lockedT1.HighSha256);
Id65AuthoringSupportLockedSource tamperedT1Projection = new(
    directRow80,
    completeTextureRows.Select(item => item.TextureId == 1 ? badT1 : item),
    pagePatches,
    Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256);
Reject("materialized locked T1 tamper", () =>
    Id65AuthoringSupportReplacementCompiler.CompileTransition(tamperedT1Projection, empty, core));
byte[] badPageAfter = pagePatches[0].CopyAfter();
badPageAfter[0] ^= 1;
Id65V2NativeTexturePagePatch badPage = new(
    pagePatches[0].RelativeOffset,
    pagePatches[0].CopyBefore(),
    badPageAfter,
    pagePatches[0].BeforeSha256,
    Hash(badPageAfter),
    pagePatches[0].Owner);
Id65V2NativeTextureWitness pageTamper = CopyWitness(
    textureWitness,
    pagePatches: [badPage, .. pagePatches.Skip(1)]);
Reject("T66 page tamper", () =>
    Id65AuthoringSupportReplacementCompiler.CaptureLockedSource(directRow80, pageTamper));

Reject("invalid Empty to Full8 edge", () => Compile(empty, full));
Reject("invalid self edge", () => Compile(core, core));
Reject("wrong manifest canonical sha", () => Compile(
    CopyManifest(empty, declaredCanonicalSha256: new string('0', 64)), core));
Reject("wrong manifest canonical json", () => Compile(
    CopyManifest(empty, declaredCanonicalJson: "{}"), core));
Reject("sector order", () => Compile(
    core,
    CopyManifest(full, sectors: full.Sectors.Reverse().ToArray())));

Id65SupportSectorIntent coreSector0 = core.Sectors[0];
Id65SupportSectorIntent badCenter = CopySector(
    coreSector0,
    center: coreSector0.Center with { X = coreSector0.Center.X + 1 });
Reject("sector center geometry", () => Compile(empty,
    CopyManifest(core, sectors: [badCenter, .. core.Sectors.Skip(1)])));
Id65SupportSectorIntent badHeader = CopySector(
    coreSector0,
    exactHeaderHex: "FF" + coreSector0.ExactHeaderHex[2..]);
Reject("sector header", () => Compile(empty,
    CopyManifest(core, sectors: [badHeader, .. core.Sectors.Skip(1)])));
Id65SupportVertexIntent badVertex = coreSector0.Vertices[0] with
    { Point = coreSector0.Vertices[0].Point with { X = -1 } };
Reject("negative vertex", () => Compile(empty,
    CopyManifest(core, sectors:
    [
        CopySector(coreSector0, vertices: [badVertex, .. coreSector0.Vertices.Skip(1)]),
        .. core.Sectors.Skip(1)
    ])));
Id65SupportFaceIntent coreFace0 = coreSector0.Faces[0];
Id65SupportFaceIntent badNormal = CopyFace(coreFace0, collisionNormalZ: 115_200);
Reject("collision winding normal", () => Compile(empty,
    CopyManifest(core, sectors:
    [
        CopySector(coreSector0, faces: [badNormal, .. coreSector0.Faces.Skip(1)]),
        .. core.Sectors.Skip(1)
    ])));
Id65SupportFaceIntent badPoint = CopyFace(
    coreFace0,
    collisionPoints:
    [
        coreFace0.CollisionPoints[0],
        coreFace0.CollisionPoints[1] with { X = coreFace0.CollisionPoints[1].X + 512 },
        coreFace0.CollisionPoints[2]
    ]);
Reject("signed9 collision point", () => Compile(empty,
    CopyManifest(core, sectors:
    [
        CopySector(coreSector0, faces: [badPoint, .. coreSector0.Faces.Skip(1)]),
        .. core.Sectors.Skip(1)
    ])));
Id65SupportFaceIntent badTexture = CopyFace(
    coreFace0,
    highDetailHex: coreFace0.HighDetailHex.Replace("42", "41", StringComparison.Ordinal));
Reject("T66 face binding", () => Compile(empty,
    CopyManifest(core, sectors:
    [
        CopySector(coreSector0, faces: [badTexture, .. coreSector0.Faces.Skip(1)]),
        .. core.Sectors.Skip(1)
    ])));
Id65SupportSeamIntent badSeam = core.Seams[0] with { TjunctionFree = false };
Reject("seam T-junction", () => Compile(empty,
    CopyManifest(core, seams: [badSeam, .. core.Seams.Skip(1)])));
Id65SupportCollisionCellIntent coreCell0 = core.CollisionCells[0];
Id65SupportCollisionCellIntent badTree = new(
    coreCell0.Cell,
    coreCell0.TreePointerByteOffset + 2,
    coreCell0.BlockWordOffset,
    coreCell0.TriangleIndexes);
Reject("collision tree pointer", () => Compile(empty,
    CopyManifest(core, collisionCells: [badTree, .. core.CollisionCells.Skip(1)])));
Id65SupportCollisionCellIntent badBlock = new(
    coreCell0.Cell,
    coreCell0.TreePointerByteOffset,
    coreCell0.BlockWordOffset,
    coreCell0.TriangleIndexes.Reverse());
Reject("collision descending block", () => Compile(empty,
    CopyManifest(core, collisionCells: [badBlock, .. core.CollisionCells.Skip(1)])));
Reject("incomplete intersected cells", () => Compile(empty,
    CopyManifest(core, collisionCells: core.CollisionCells.Skip(1).ToArray())));
Reject("collision row allowlist", () => Compile(empty,
    CopyManifest(core, sectors:
    [
        CopySector(coreSector0, faces:
        [
            CopyFace(coreFace0, collisionTriangleIndex: 12_345),
            .. coreSector0.Faces.Skip(1)
        ]),
        .. core.Sectors.Skip(1)
    ])));
Reject("occlusion group", () => Compile(empty,
    CopyManifest(core, occlusionGroup0Sectors: [0, 1, 2])));
Reject("occlusion tail hash", () => Compile(empty,
    CopyManifest(core, occlusionOpaqueTailSha256: new string('0', 64))));

void CapacityReject(string label, Id65AuthoringSupportCompilerLimits limits) =>
    Reject(label, () => Compile(empty, core, limits));
CapacityReject("model capacity", new(ModelByteCapacity: 0x947FF));
CapacityReject("used-model capacity", new(MaximumOutputUsedModelBytes: 0x94633));
CapacityReject("sector count capacity", new(MaximumSceneSectorCount: 216));
CapacityReject("sector bytes capacity", new(MaximumSectorByteLength: 0xC7));
CapacityReject("sector face capacity", new(MaximumSectorFaceCount: 3));
CapacityReject("collision count capacity", new(MaximumCollisionTriangleCount: 19_807));
CapacityReject("collision tree capacity", new(CollisionTreeCapacityBytes: 0x6A5F));
CapacityReject("collision block capacity", new(CollisionBlockCapacityBytes: 0x17983));
CapacityReject("texture capacity", new(MaximumNativeTextureId: 65));

byte[] badForward = addCore.CopySourceRow80();
badForward[0] ^= 1;
Reject("forward preimage", () => Id65AuthoringSupportReplacementCompiler.ApplyTransactional(
    addCore, badForward, reverse: false));
byte[] badReverse = addCore.CopyOutputRow80();
badReverse[0xDE800 + 0x3030] ^= 1;
Reject("inverse preimage", () => Id65AuthoringSupportReplacementCompiler.ApplyTransactional(
    addCore, badReverse, reverse: true));
byte[] unownedOutput = addCore.CopyOutputRow80();
unownedOutput[0] ^= 1;
Reject("unowned mutation", () => Id65AuthoringSupportReplacementCompiler.ValidateDiffOwnershipForSmoke(
    addCore.CopySourceRow80(), unownedOutput, addCore.OwnedRanges));
byte[] protectedModel = addCore.CopyOutputModel();
protectedModel[core.Layout.SpecialSurfaceOffset] ^= 1;
byte[] protectedRow = addCore.CopyOutputRow80();
protectedModel.CopyTo(protectedRow, 0xDE800);
Reject("protected component mutation", () => Id65AuthoringSupportReplacementCompiler.ValidateStateForSmoke(
    core, directRow80.AsSpan(0xDE800, 0x94800), protectedModel, protectedRow));
byte[] tailModel = addCore.CopyOutputModel();
tailModel[core.Layout.UsedModelBytes] = 1;
byte[] tailRow = addCore.CopyOutputRow80();
tailModel.CopyTo(tailRow, 0xDE800);
Reject("nonzero model tail", () => Id65AuthoringSupportReplacementCompiler.ValidateStateForSmoke(
    core, directRow80.AsSpan(0xDE800, 0x94800), tailModel, tailRow));

Require(rejectionCount == 38, $"Support rejection matrix ran {rejectionCount}, expected 38.");
Require(typeof(Id65AuthoringSupportReplacementCompiler)
            .GetMethod(nameof(Id65AuthoringSupportReplacementCompiler.CompileTransition))!
            .GetParameters()
            .All(parameter => parameter.ParameterType != typeof(Id65CompiledSupportReplacement) &&
                !parameter.ParameterType.Name.Contains("StaticPlan", StringComparison.Ordinal) &&
                !parameter.ParameterType.Name.Contains("Compiled", StringComparison.Ordinal) &&
                !parameter.ParameterType.Name.Contains("Path", StringComparison.Ordinal)),
    "The support compile API accepts a compiled afterimage, slice plan, or path.");
foreach (Id65CompiledSupportReplacement compiled in new[] { addCore, addBay, removeBay, removeCore })
{
    Require(compiled.BuildsBothStatesFromExactLockedSource && !compiled.AcceptsCompiledAfterimages &&
            !compiled.AcceptsSlicePlans && compiled.DirectRelocationsAndRebases &&
            compiled.ExactInverseVerified && compiled.DeterministicReadbackRequired &&
            compiled.PostV2RetirementDiscriminatorOnly &&
            compiled.FourOrEightSectorsBelowNativeObservedMinimum64 && !compiled.RuntimeAccepted &&
            !compiled.WritesFileSystem && !compiled.WritesDiscImage && !compiled.WritesCue &&
            !compiled.RetiredPublisherCalled && !compiled.AppIntegrated && !compiled.CreateBinEnabled &&
            !compiled.NormalCreateBinEnabled && !compiled.RuntimeCandidateAuthorized &&
            !compiled.ReleaseIntegrated && !compiled.PromotionAuthorized && !compiled.Publishable,
        "A support compiler no-afterimage/no-writer/no-runtime safety boundary changed.");
}
Require(!lockedSource.ContainsPath && !lockedSource.ContainsCompiledAfterimage &&
        !lockedSource.ContainsSlicePlan && !lockedSource.WritesFileSystem,
    "The support locked source gained path, afterimage, plan, or writer state.");
Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(foundationImagePath) == foundationBefore &&
        SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!).SequenceEqual(lockedDirectoryBefore) &&
        SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!).SequenceEqual(foundationDirectoryBefore),
    "The pure support compiler changed or published beside an input artifact.");

Console.WriteLine("ID65 authored support replacement pins:");
Console.WriteLine($"  projection={lockedSource.TextureProjectionSha256}");
Console.WriteLine($"  manifests empty={empty.CanonicalSha256}; core={core.CanonicalSha256}; full={full.CanonicalSha256}");
Print("addCore", addCore);
Print("addBay", addBay);
Print("removeBay", removeBay);
Print("removeCore", removeCore);
Console.WriteLine(
    "PASS Id65AuthoringSupportReplacementCompilerSmoke: exact locked-source Empty<->Core4<->Full8 " +
    "center-fan/T66/collision-tree/occlusion support replacement, direct relocations/rebases, exact double " +
    $"inverse, defensive copies, {rejectionCount} rejects, and no filesystem/BIN/CUE/App/CreateBIN/runtime/release/promotion.");

Id65CompiledSupportReplacement Compile(
    Id65AuthoringSupportManifest source,
    Id65AuthoringSupportManifest output,
    Id65AuthoringSupportCompilerLimits? limits = null) =>
    Id65AuthoringSupportReplacementCompiler.CompileTransition(
        lockedSource, source, output, limits);

static void Print(string label, Id65CompiledSupportReplacement value)
{
    Console.WriteLine(
        $"  {label}: changed={value.ChangedByteCount}; ranges={value.DiffRangeCount}; diff={value.DiffManifestSha256}");
    Console.WriteLine(
        $"    owned={value.OwnedRangeMapSha256}; reloc={value.RelocationMapSha256}; rebase={value.RebaseMapSha256}");
    Console.WriteLine($"    tx={value.TransactionSha256}; plan={value.DeterministicPlanSha256}");
}

static bool Equivalent(Id65CompiledSupportReplacement left, Id65CompiledSupportReplacement right) =>
    left.Transition == right.Transition &&
    left.SourceManifest.CanonicalSha256 == right.SourceManifest.CanonicalSha256 &&
    left.OutputManifest.CanonicalSha256 == right.OutputManifest.CanonicalSha256 &&
    left.SourceRow80Sha256 == right.SourceRow80Sha256 &&
    left.OutputRow80Sha256 == right.OutputRow80Sha256 &&
    left.SourceModelSha256 == right.SourceModelSha256 &&
    left.OutputModelSha256 == right.OutputModelSha256 &&
    left.ChangedByteCount == right.ChangedByteCount && left.DiffRangeCount == right.DiffRangeCount &&
    left.DiffManifestSha256 == right.DiffManifestSha256 &&
    left.OwnedRangeMapSha256 == right.OwnedRangeMapSha256 &&
    left.RelocationMapSha256 == right.RelocationMapSha256 &&
    left.RebaseMapSha256 == right.RebaseMapSha256 &&
    left.TransactionSha256 == right.TransactionSha256 &&
    left.DeterministicPlanSha256 == right.DeterministicPlanSha256 &&
    left.Capacity == right.Capacity && left.SourceState == right.SourceState &&
    left.OutputState == right.OutputState && left.DiffRanges.SequenceEqual(right.DiffRanges) &&
    left.Relocations.SequenceEqual(right.Relocations) &&
    left.HandleRebases.SequenceEqual(right.HandleRebases) &&
    left.OwnedRanges.Select(OwnedTuple).SequenceEqual(right.OwnedRanges.Select(OwnedTuple)) &&
    left.CopySourceRow80().SequenceEqual(right.CopySourceRow80()) &&
    left.CopyOutputRow80().SequenceEqual(right.CopyOutputRow80());

static FrozenSmokeTransitionPins FrozenPinsFor(Id65SupportTransitionKind transition) =>
    transition switch
    {
        Id65SupportTransitionKind.AddCoreSupport => new(
            550_741,
            27_683,
            "1461d1e4b3e1a4fee36ecd241189a5b32cc8e2b2e0ab54b91bc58d8924fd3619",
            "ed6f76359de3278e6ebed1b7efea6df093d51a06bc54a004bf18b85837a86917",
            "c18302c4c6c14ae00022dd6db8827d1021205231a0bb18bdfa742b7844020cf5",
            "c8f83a8f7318221169989b9b9f188666163bc0a7daf697cda993bf155be954ab",
            "c1e427bc481ed287aa216477bab99214f38f38673e9d5cec403f17c40eac981a",
            "dbac770db883f551fc7f37bbc38eeb13b1d5ed1ff5a385e58077981536637d3e"),
        Id65SupportTransitionKind.AddEnemyBay => new(
            225_506,
            25_424,
            "793281faee27ae95b6f144db5912b9f8c6b0f2091ca2ab1d4fcae19c95bf5c8a",
            "a995e2b2245a8a554b3c5f79b857a83bf80b58ea997c7b9c4129a3ed1e6ac50f",
            "897759c9bd28e97f5e2673e6f264c6195adb5290cd04edde64d11ca91fbac7ee",
            "04e9f8c23ccdf1d32745402437c9b431e202750484183bf8036748a3ebde55eb",
            "98032389fd159d1bb4a48473f9a87dfc7ebf70c2a811626b0557da533a17fa1e",
            "9010c618ee6d20c81ba8a1a8b6110c2a2d9ce9bb7df661502925c94e9100bb92"),
        Id65SupportTransitionKind.RemoveEnemyBay => new(
            225_506,
            25_424,
            "e00cb4d235f36f75bc5b9c4decf971aed2bca0250b9f0e9c328c613b9290dda1",
            "42279113c8679676937641a6e89569b7c08fdad8228305bfd0cdc9e4c465ee01",
            "42b0cdc4f1b552cde62bacee88a5d37157fd9b47e92cd07a87db165830d805b4",
            "4b2010a89ee6af2abd354253602870fb968ad790c0b3f82cb6ea5f92709d0529",
            "29ad57ab6559ac861c9ef3aa21c1f8f50d4160290be6723680ec02a5c70a0b74",
            "f51a21927eb701a134399681a793eb1cdd097d8180c549c8c9506e6c2932db60"),
        Id65SupportTransitionKind.RemoveCore => new(
            550_741,
            27_683,
            "e9ec44809fa10ca97bbcd31b5aef7c1b91db491de347751a476b922874a9a5cc",
            "b3a4428d1ac6dacbbfd93be569dffabdf00ddd29ac0b917cd52d2b18a14fe5bf",
            "84258e4c0a181fafd9c16ebabd6896ae60db2f4e9094cdc9cea4c3ecf97145d4",
            "4554e8cf2fb31da501ad27912b7ea431558d7b98944f4ad3d3be12ded48a7e7b",
            "0364dc4e7984445a582abf3f99c4462fa2aef3beb9d9625dbdb0105210e0006b",
            "051c65f5af2062a8a0fdcb2d20abf9446ee9db0879ac7fccaa44bbc2556db606"),
        _ => throw new InvalidDataException("Unknown support transition kind.")
    };

static (string, string, int, int, string, string) OwnedTuple(Id65SupportOwnedRange item) =>
    (item.StableId, item.OwnerId, item.DataRelativeOffset, item.ByteLength,
        item.BeforeSha256, item.AfterSha256);

static bool IsStrictlyDescending(IReadOnlyList<int> values)
{
    for (int index = 1; index < values.Count; index++)
        if (values[index - 1] <= values[index])
            return false;
    return true;
}

static Id65AuthoringSupportManifest CopyManifest(
    Id65AuthoringSupportManifest source,
    IReadOnlyList<Id65SupportSectorIntent>? sectors = null,
    IReadOnlyList<Id65SupportSeamIntent>? seams = null,
    IReadOnlyList<Id65SupportCollisionCellIntent>? collisionCells = null,
    IReadOnlyList<int>? occlusionGroup0Sectors = null,
    string? occlusionOpaqueTailSha256 = null,
    string? declaredCanonicalJson = null,
    string? declaredCanonicalSha256 = null) =>
    new(
        source.ProfileId,
        source.Kind,
        source.LockedSubstrateSectorCount,
        source.RetiresLockedSector217,
        source.ClearsInheritedCollisionLookups,
        source.CollisionTriangleCount,
        source.CollisionAssignment,
        source.AuthoredTranslationWorld,
        source.TextureWitnessSha256,
        source.TextureIntentId,
        sectors ?? source.Sectors,
        seams ?? source.Seams,
        collisionCells ?? source.CollisionCells,
        occlusionGroup0Sectors ?? source.OcclusionGroup0Sectors,
        source.CollisionTreeSha256,
        source.CollisionBlocksSha256,
        source.CollisionComponentSha256,
        source.CollisionBlocksUsedBytes,
        source.OcclusionSha256,
        occlusionOpaqueTailSha256 ?? source.OcclusionOpaqueTailSha256,
        source.Layout,
        declaredCanonicalJson,
        declaredCanonicalSha256);

static Id65SupportSectorIntent CopySector(
    Id65SupportSectorIntent source,
    Id65AuthoringPoint? center = null,
    string? exactHeaderHex = null,
    IReadOnlyList<Id65SupportVertexIntent>? vertices = null,
    IReadOnlyList<Id65SupportFaceIntent>? faces = null) =>
    new(
        source.Id,
        source.SectorIndex,
        center ?? source.Center,
        source.Origin,
        source.BoundsMinimum,
        source.BoundsMaximum,
        source.Radius,
        exactHeaderHex ?? source.ExactHeaderHex,
        source.ExactSectorSha256,
        vertices ?? source.Vertices,
        faces ?? source.Faces);

static Id65SupportFaceIntent CopyFace(
    Id65SupportFaceIntent source,
    string? highDetailHex = null,
    int? collisionTriangleIndex = null,
    IReadOnlyList<Id65AuthoringPoint>? collisionPoints = null,
    long? collisionNormalZ = null) =>
    new(
        source.Id,
        source.Direction,
        source.LowDetailHandle,
        source.HighDetailHandle,
        source.HpLpPairId,
        source.LowDetailHex,
        highDetailHex ?? source.HighDetailHex,
        source.CollisionHandle,
        collisionTriangleIndex ?? source.CollisionTriangleIndex,
        collisionPoints ?? source.CollisionPoints,
        source.RenderNormalZ,
        collisionNormalZ ?? source.CollisionNormalZ,
        source.CollisionCells);

static Id65V2NativeTextureWitness CopyWitness(
    Id65V2NativeTextureWitness source,
    IReadOnlyList<Id65V2NativeTexturePackedRow>? packedRows = null,
    IReadOnlyList<Id65V2NativeTexturePagePatch>? pagePatches = null) =>
    new(
        source.ProfileId,
        source.DonorManifestId,
        source.DonorCompleteRecordSha256,
        source.SourceTexturePagesSha256,
        source.OutputTexturePagesSha256,
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
        source.CanonicalSha256);

static byte[] ReadRootFile(
    string imagePath,
    string exactName,
    long relativeOffset,
    int byteLength)
{
    DiscLayout layout = DiscImage.DetectLayout(imagePath);
    using FileStream image = File.OpenRead(imagePath);
    DiscFileRecord file = DiscImage.FindRootFileRecord(
        image,
        layout,
        name => string.Equals(name, exactName, StringComparison.OrdinalIgnoreCase));
    if (relativeOffset < 0 || relativeOffset + byteLength > file.Size)
        throw new InvalidDataException("A support smoke source read escaped its root file.");
    return DiscImage.ReadFileBytes(image, layout, file.Lba, relativeOffset, byteLength);
}

static FileState SnapshotFile(string path) =>
    new(new FileInfo(path).Length, File.GetLastWriteTimeUtc(path), HashFile(path));

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.GetFileSystemEntries(path)
        .Select(entry => $"{Path.GetFileName(entry)}|{new FileInfo(entry).Length}|{File.GetLastWriteTimeUtc(entry):O}")
        .Order(StringComparer.Ordinal)
        .ToArray();

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexStringLower(SHA256.HashData(stream));
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(long ByteLength, DateTime LastWriteUtc, string Sha256);

internal sealed record FrozenSmokeTransitionPins(
    int ChangedByteCount,
    int DiffRangeCount,
    string DiffManifestSha256,
    string OwnedRangeMapSha256,
    string RelocationMapSha256,
    string RebaseMapSha256,
    string TransactionSha256,
    string DeterministicPlanSha256);
