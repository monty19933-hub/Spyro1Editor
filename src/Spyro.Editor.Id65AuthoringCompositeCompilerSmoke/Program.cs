using System.Buffers.Binary;
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
Require(File.Exists(foundationImagePath), $"Missing exact foundation donor image: {foundationImagePath}");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState foundationBefore = SnapshotFile(foundationImagePath);
IReadOnlyList<string> lockedDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
IReadOnlyList<string> foundationDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!);

UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2 =
    UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.BuildStaticPlan(lockedImagePath);
byte[] directRow80 = ReadRootFile(lockedImagePath, "WAD.WAD", 0x6936800, 0x2E2000);
byte[] targetOverlay = ReadRootFile(lockedImagePath, "WAD.WAD", 0x6927000, 0xF800);
byte[] globalExecutable = ReadRootFileWhere(
    lockedImagePath,
    name => name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase),
    0,
    0x66000);
Require(directRow80.SequenceEqual(v2.SourceData),
    "The direct WAD row-80 source differs from the locked display-name proof source.");

byte[] sourceInput = directRow80.ToArray();
byte[] overlayInput = targetOverlay.ToArray();
byte[] executableInput = globalExecutable.ToArray();
Id65AuthoringCompositeLockedSource source =
    Id65AuthoringCompositeCompiler.CaptureLockedSource(sourceInput, overlayInput, executableInput);
sourceInput[0] ^= 1;
overlayInput[0] ^= 1;
executableInput[0] ^= 1;

UnusedLevel65NativeTextureStaticPlan oldTextureProof =
    UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(foundationImagePath);
Id65V2NativeTextureWitness textureWitness =
    Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(oldTextureProof);
UnusedLevel65MobyDependencyBundleContract mobyContract =
    UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(foundationImagePath);
byte[] package = ReadRootFile(foundationImagePath, "WAD.WAD", 0x9C1D20, 0x174);
Id65MobyDependencyBundleDescriptor bundle =
    Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundle(mobyContract, package);
Id65MobyPlacementIntent t88 =
    Id65AuthoringMobyDependencyBundleCompiler.CreateZeroEggT88Placement();
Id65MobyPlacementIntent t107 =
    Id65AuthoringMobyDependencyBundleCompiler.CreateT107Placement();
Id65TerrainTriangleIntent triangle =
    Id65AuthoringTerrainMutationCompiler.CreateConcreteTwoTileWitness();
Id65AuthoringCompositeManifest manifest =
    Id65AuthoringCompositeCompiler.CreateManifest(triangle, textureWitness, bundle, [t107, t88]);
Id65AuthoringCompositeManifest reorderedManifest =
    Id65AuthoringCompositeCompiler.CreateManifest(triangle, textureWitness, bundle, [t88, t107]);
Require(manifest.CanonicalJson == reorderedManifest.CanonicalJson &&
        manifest.CanonicalSha256 == reorderedManifest.CanonicalSha256,
    "The composite manifest depends on caller placement order.");

Id65CompiledAuthoringComposite first = Compile(manifest, [t107, t88]);
Id65CompiledAuthoringComposite repeat = Compile(reorderedManifest, [t88, t107]);
Id65AuthoringCompositeManifest noTerrainManifest =
    Id65AuthoringCompositeCompiler.CreateManifest(null, textureWitness, bundle, [t88, t107]);
Id65CompiledAuthoringComposite noTerrain = Compile(noTerrainManifest, [t88, t107]);

Require(first.ProfileId == Id65AuthoringCompositeCompiler.ProfileId &&
        first.ManifestSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedManifestSha256 &&
        first.SourceRow80Sha256 == Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
        first.SourceModelSha256 == Id65AuthoringCompositeCompiler.ExpectedLockedModelSha256 &&
        first.OutputModelSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedModelSha256 &&
        first.OutputRow80Sha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedOutputRow80Sha256 &&
        first.ChangedByteCount == Id65AuthoringCompositeCompiler.ExpectedCombinedChangedByteCount &&
        first.DiffRangeCount == Id65AuthoringCompositeCompiler.ExpectedCombinedDiffRangeCount &&
        first.DiffManifestSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedDiffManifestSha256 &&
        first.OwnedRangeMapSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedOwnedRangeMapSha256 &&
        first.RelocationMapSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedRelocationMapSha256 &&
        first.RebaseMapSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedRebaseMapSha256 &&
        first.MobyAssetRelocationMapSha256 ==
            Id65AuthoringCompositeCompiler.ExpectedCombinedMobyAssetRelocationMapSha256 &&
        first.TransactionSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedTransactionSha256 &&
        first.DeterministicPlanSha256 ==
            Id65AuthoringCompositeCompiler.ExpectedCombinedDeterministicPlanSha256 &&
        first.DiffRanges.Count == first.DiffRangeCount &&
        first.DiffRanges.Sum(item => item.ByteLength) == first.ChangedByteCount,
    "The concrete locked-source composite witness changed.");
Require(first.TargetOverlaySha256 == Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 &&
        first.GlobalExecutableSha256 == Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256,
    "The protected overlay or global executable identity changed.");
Require(noTerrain.OutputModelSha256 ==
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256 &&
        !noTerrain.Terrain.OptionalTrianglePresent && noTerrain.Terrain.OptionalTriangle is null &&
        noTerrain.Capacity.OutputUsedModelBytes == 0x94634,
    "Optional terrain was not rebuilt cleanly from locked source when absent.");

Require(first.OwnedRanges.Count == 7_961 &&
        first.OwnedRanges.Count(item => item.StableId.StartsWith("texture.page.", StringComparison.Ordinal)) == 7_949 &&
        first.OwnedRanges.Count(item => item.StableId == "model.final") == 1 &&
        first.OwnedRanges.Count(item => item.OwnerId == "composite.v2-spawn") == 2 &&
        first.OwnedRanges.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() ==
            first.OwnedRanges.Count,
    "The complete nonoverlapping composite ledger count/identity changed.");
Require(first.OwnedRanges.Where(item =>
            item.StableId is "asset.root37" or "asset.actor-id37" or "asset.package.01f5" or
                "scene.object-count" or "placement.t88" or "placement.t107" or
                "scene.fixup-count" or "scene.fixup.t107-properties" or "asset.properties.grass")
        .Select(item => (item.DataRelativeOffset, item.ByteLength))
        .SequenceEqual(new[]
        {
            (0xE4, 4),
            (0x19A, 2),
            (0x1CFA44, 0x174),
            (0x1D016C, 4),
            (0x1D1FB0, 0x58),
            (0x1D2638, 0x58),
            (0x1D7F28, 4),
            (0x1D8130, 4),
            (0x1D8138, 8)
        }),
    "The exact nine-range Moby ledger changed.");

Require(first.Relocations.Count == 9 &&
        first.Relocations[0].SourceRelativeOffset == 0 &&
        first.Relocations[0].OutputRelativeOffset == 0 &&
        first.Relocations[^1].SourceRelativeOffset + first.Relocations[^1].SourceByteLength == 0x94508 &&
        first.Relocations[^1].OutputRelativeOffset + first.Relocations[^1].OutputByteLength == 0x94664 &&
        first.Relocations.Where(item => item.ContentsPreserved).Select(item => item.StableId)
            .SequenceEqual(new[]
            {
                "component.special-surface",
                "component.cyclorama",
                "component.portal-table",
                "component.particles",
                "component.sound"
            }),
    "The exact direct model relocation/preservation map changed.");
Require(first.HandleRebases.Count == 31 &&
        first.HandleRebases.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() == 31 &&
        first.HandleRebases.Single(item => item.StableId == "terrain.remote-pad.face.0:lp").Output ==
            new Id65LogicalAddress("scene-sector", 216, 0) &&
        first.HandleRebases.Single(item => item.StableId == "terrain.remote-pad.face.0:hp").Output ==
            new Id65LogicalAddress("scene-sector", 216, 0) &&
        first.HandleRebases.Single(item => item.StableId == "occlusion.remote-pad.group0").Output ==
            new Id65LogicalAddress("occlusion-group", 0, 216) &&
        first.HandleRebases.Single(item => item.StableId == "texture.locked.record25").Source ==
            new Id65LogicalAddress("texture-record", 25, 0) &&
        first.HandleRebases.Single(item => item.StableId == "placement.zero-egg.t88").Source is null &&
        first.HandleRebases.Single(item => item.StableId == "moby.retired-thief.t88").Output is null &&
        first.HandleRebases.Single(item => item.StableId == "asset.root.actor-01f5").Source ==
            new Id65LogicalAddress("actor-root.artisans", 22, 0x01F5) &&
        first.HandleRebases.Single(item => item.StableId ==
            Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPackageAssetId).Source ==
            new Id65LogicalAddress("data.artisans", 0x1C1520, 0) &&
        first.HandleRebases.Single(item => item.StableId ==
            Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPropertiesAssetId).Source ==
            new Id65LogicalAddress("scene.artisans", 0x11ACC, 0),
    "The exact direct stable-handle map or donor provenance changed.");
Require(first.MobyAssetRelocations.Count == 5 &&
        first.MobyAssetRelocations.Take(3).All(item => item.Deduplicated) &&
        first.MobyAssetRelocations.Skip(3).All(item => !item.Deduplicated) &&
        first.MobyAssetRelocations.Count(item => item.Kind == "moby-row-template") == 2,
    "The direct Moby asset relocation/dedupe readback changed.");

Id65AuthoringCompositeTerrainReadback terrainReadback = first.Terrain;
Require(terrainReadback.MandatoryNegativeWindingV2 && terrainReadback.OptionalTrianglePresent &&
        terrainReadback.SectorIndex == 216 && terrainReadback.LowDetailVertexCount == 4 &&
        terrainReadback.LowDetailFaceCount == 2 && terrainReadback.HighDetailVertexCount == 4 &&
        terrainReadback.HighDetailFaceCount == 2 && terrainReadback.BaseCollisionTriangleIndex == 13_995 &&
        terrainReadback.BaseCollisionTriangleHex == "10011C701001380000020000" &&
        terrainReadback.BaseCollisionNormalZ == -50_176 &&
        terrainReadback.EnvironmentSha256 ==
            "c41f70eacc4c507401743dfe7d8fe20de2443b62de78682a16211306b3e4c617" &&
        terrainReadback.SectorSha256 ==
            "8537b07f283bdbfebf3816048269e0a93454456749e710a94f7f5ead000c4111" &&
        terrainReadback.CollisionSha256 == Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileCollisionSha256 &&
        terrainReadback.CollisionTreeSha256 == Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileTreeSha256 &&
        terrainReadback.CollisionBlocksSha256 == Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileBlocksSha256 &&
        terrainReadback.CollisionBlocksUsedBytes == 0x17962 && terrainReadback.OcclusionGroupIndex == 0 &&
        terrainReadback.OcclusionContainsSector && terrainReadback.OptionalTriangle is not null &&
        terrainReadback.OptionalTriangle.TriangleId == "tile.remote-pad.1" &&
        terrainReadback.OptionalTriangle.AddedVertexHandle == "vertex.remote-pad.d" &&
        terrainReadback.OptionalTriangle.CollisionTriangleIndex == 19_808 &&
        terrainReadback.OptionalTriangle.TargetCellSequence.SequenceEqual([19_808, 13_995]),
    "The direct terrain semantic readback changed.");

Id65AuthoringCompositeTextureReadback texture = first.Texture;
Require(texture.WitnessSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 &&
        texture.SourceTextureCount == 66 && texture.OutputTextureCount == 67 &&
        texture.PrivateTextureId == 66 && texture.DonorWadEntry == 70 && texture.DonorTextureId == 12 &&
        texture.PrivateLowRowSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedPrivateLowRowSha256 &&
        texture.PrivateHighRowSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedPrivateHighRowSha256 &&
        texture.OutputTextureComponentBytes == 0x3030 &&
        texture.OutputTextureComponentSha256 ==
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTextureComponentSha256 &&
        texture.OutputTexturePagesSha256 ==
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256 &&
        texture.RenderFaceId == "terrain.remote-pad.face.0" && texture.SectorIndex == 216 &&
        texture.OutputLowFaceModelOffset == 0x2B510 && texture.OutputHighFaceModelOffset == 0x2B548 &&
        texture.OutputHighTextureWordModelOffset == 0x2B550 &&
        texture.OutputLowFaceHex == "0082100000821000" &&
        texture.OutputHighFaceHex == "000001020101000242024001080A1000" &&
        texture.TemplateTextureId == 25 && texture.OutputTextureId == 66 &&
        texture.AddedTerrainFacePreservedT25,
    "The direct private-T66 semantic readback changed.");
Require(first.MobyPlacements.Select(item => item.TargetTrueIndex).SequenceEqual(new[] { 88, 107 }) &&
        first.MobyPlacements.Single(item => item.TargetTrueIndex == 88) is var grass88 &&
        grass88.RawX == 96_850 && grass88.RawY == 141_732 && grass88.RawZ == 12_248 &&
        grass88.ActorId == 0x01F5 && grass88.PropertiesSceneOffset == 0x8138 &&
        grass88.RowSha256 == "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f" &&
        first.MobyPlacements.Single(item => item.TargetTrueIndex == 107) is var grass107 &&
        grass107.RawX == 124_384 && grass107.RawY == 102_304 && grass107.RawZ == 8_192 &&
        grass107.ActorId == 0x01F5 && grass107.PropertiesSceneOffset == 0x8138 &&
        grass107.RowSha256 == "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb",
    "The exact T88/T107 semantic placement readback changed.");
byte[] semanticOutput = first.CopyOutputRow80();
Require(Hash(first.CopySourceRow80().AsSpan(0x1D5AFC, 0x134)) ==
            "6213aceb752cb139ac669b392a3faf7441888306b363fb4cf3e2c63f45715136" &&
        first.CopySourceRow80().AsSpan(0x1D5AFC, 0x134)
            .SequenceEqual(semanticOutput.AsSpan(0x1D5AFC, 0x134)) &&
        CountFixup(semanticOutput, 0x1FB0) == 1 && CountFixup(semanticOutput, 0x5AFC) == 1 &&
        CountFixup(semanticOutput, 0x2638) == 1,
    "The T88/private/T107 fixup or retired-thief private-block preservation readback changed.");

Id65AuthoringCompositeCapacityReadback capacity = first.Capacity;
Require(capacity.ModelByteCapacity == 0x94800 && capacity.SourceUsedModelBytes == 0x94508 &&
        capacity.OutputUsedModelBytes == 0x94664 && capacity.OutputZeroTailBytes == 0x19C &&
        capacity.TextureCountBefore == 66 && capacity.TextureCountAfter == 67 &&
        capacity.HighestTextureId == 66 && capacity.SectorCount == 217 &&
        capacity.LowDetailVertexCount == 4 && capacity.LowDetailFaceCount == 2 &&
        capacity.HighDetailVertexCount == 4 && capacity.HighDetailFaceCount == 2 &&
        capacity.CollisionTriangleCount == 19_809 && capacity.CollisionBlocksUsedBytes == 0x17962 &&
        capacity.CollisionBlocksCapacityBytes == 0x17984 &&
        capacity.ObjectCountBefore == 107 && capacity.ObjectCountAfter == 108 &&
        capacity.ActorRootCountBefore == 37 && capacity.ActorRootCountAfter == 38 &&
        capacity.ActorTailBytesRemaining == 0x448 && capacity.FixupCountBefore == 0x81 &&
        capacity.FixupCountAfter == 0x82 && capacity.SceneTailBytesRemaining == 0x6C0,
    "The complete composite capacity readback changed.");

Require(Equivalent(first, repeat), "Repeated/reordered composite compilation was not deterministic.");
Require(Id65AuthoringCompositeCompiler.InvertRelocations(
            Id65AuthoringCompositeCompiler.InvertRelocations(first.Relocations)).SequenceEqual(first.Relocations) &&
        Id65AuthoringCompositeCompiler.InvertHandleRebases(
            Id65AuthoringCompositeCompiler.InvertHandleRebases(first.HandleRebases)).SequenceEqual(first.HandleRebases),
    "A direct relocation/rebase double inverse changed.");
byte[] lockedSource = first.CopySourceRow80();
byte[] output = Id65AuthoringCompositeCompiler.ApplyTransactional(first, lockedSource, reverse: false);
byte[] exactRollback = Id65AuthoringCompositeCompiler.ApplyTransactional(first, output, reverse: true);
Id65AuthoringCompositeRollbackPlan rollbackPlan =
    Id65AuthoringCompositeCompiler.CreateRollbackPlan(first);
byte[] plannedRollback = Id65AuthoringCompositeCompiler.ApplyRollback(rollbackPlan, output);
Require(output.SequenceEqual(first.CopyOutputRow80()) && exactRollback.SequenceEqual(lockedSource) &&
        plannedRollback.SequenceEqual(lockedSource) && rollbackPlan.RollbackOnly &&
        !rollbackPlan.AcceptedAsCompileInput && rollbackPlan.ExpectedAfterimageSha256 == first.OutputRow80Sha256 &&
        rollbackPlan.LockedSourceSha256 == first.SourceRow80Sha256,
    "The exact forward/rollback-only composite transaction changed.");

byte[] defensiveOutput = first.CopyOutputRow80();
defensiveOutput[0] ^= 1;
byte[] defensiveModel = first.CopyOutputModel();
defensiveModel[0] ^= 1;
byte[] defensiveOwnedBefore = first.OwnedRanges[0].CopyBefore();
defensiveOwnedBefore[0] ^= 1;
Require(Hash(first.CopyOutputRow80()) == first.OutputRow80Sha256 &&
        Hash(first.CopyOutputModel()) == first.OutputModelSha256 &&
        Hash(first.OwnedRanges[0].CopyBefore()) == first.OwnedRanges[0].BeforeSha256,
    "A compiled output or ownership byte array escaped defensive copying.");

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
    Require(rejected, $"Atomic rejection `{label}` was not observed.");
    rejectionCount++;
}

void RejectText(string label, Action action, string requiredText)
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
    Require(rejected, $"Atomic rejection `{label}` did not mention `{requiredText}`.");
    rejectionCount++;
}

byte[] wrongSource = directRow80.ToArray();
wrongSource[0] ^= 1;
Reject("locked row80", () => Id65AuthoringCompositeCompiler.CaptureLockedSource(
    wrongSource, targetOverlay, globalExecutable));
byte[] wrongOverlay = targetOverlay.ToArray();
wrongOverlay[0] ^= 1;
Reject("target overlay", () => Id65AuthoringCompositeCompiler.CaptureLockedSource(
    directRow80, wrongOverlay, globalExecutable));
byte[] wrongExecutable = globalExecutable.ToArray();
wrongExecutable[0] ^= 1;
Reject("global executable", () => Id65AuthoringCompositeCompiler.CaptureLockedSource(
    directRow80, targetOverlay, wrongExecutable));
Reject("compiled afterimage as locked source", () => Id65AuthoringCompositeCompiler.CaptureLockedSource(
    first.CopyOutputRow80(), targetOverlay, globalExecutable));

Reject("terrain intent", () => Compile(
    Id65AuthoringCompositeCompiler.CreateManifest(
        triangle with { CollisionB = triangle.CollisionC }, textureWitness, bundle, [t88, t107]),
    [t88, t107]));
Reject("manifest/input mismatch", () => Compile(manifest, [t107]));
Reject("placement id", () => Compile(
    Id65AuthoringCompositeCompiler.CreateManifest(
        triangle, textureWitness, bundle, [t88 with { Id = "placement.forged.t88" }, t107]),
    [t88 with { Id = "placement.forged.t88" }, t107]));
Reject("placement atomic group", () => Compile(
    Id65AuthoringCompositeCompiler.CreateManifest(
        triangle, textureWitness, bundle, [t88 with { AtomicGroupId = "atomic.forged" }, t107]),
    [t88 with { AtomicGroupId = "atomic.forged" }, t107]));

Reject("texture canonical", () => Compile(
    Id65AuthoringCompositeCompiler.CreateManifest(
        triangle, CopyWitness(textureWitness, canonicalSha256: new string('0', 64)), bundle, [t88, t107]),
    [t88, t107],
    witness: CopyWitness(textureWitness, canonicalSha256: new string('0', 64))));
IReadOnlyList<Id65V2NativeTexturePagePatch> pagePatches = textureWitness.CopyPagePatches();
byte[] badPage = pagePatches[0].CopyAfter();
badPage[0] ^= 1;
Id65V2NativeTexturePagePatch badPagePatch = new(
    pagePatches[0].RelativeOffset,
    pagePatches[0].CopyBefore(),
    badPage,
    pagePatches[0].BeforeSha256,
    Hash(badPage),
    pagePatches[0].Owner);
Id65V2NativeTextureWitness pageTamper = CopyWitness(
    textureWitness, pagePatches: [badPagePatch, .. pagePatches.Skip(1)]);
Reject("texture page bytes", () => Compile(
    Id65AuthoringCompositeCompiler.CreateManifest(triangle, pageTamper, bundle, [t88, t107]),
    [t88, t107], witness: pageTamper));
IReadOnlyList<Id65V2NativeTexturePackedRow> packedRows = textureWitness.CopyPackedRows();
Id65V2NativeTexturePackedRow synthetic = packedRows.Single(item => item.Synthetic);
byte[] badLow = synthetic.CopyLow();
badLow[0] ^= 1;
Id65V2NativeTexturePackedRow badSynthetic = new(
    synthetic.TextureId,
    synthetic.DonorWadEntry,
    synthetic.DonorTextureId,
    synthetic.Synthetic,
    badLow,
    synthetic.CopyHigh(),
    Hash(badLow),
    synthetic.HighSha256);
Id65V2NativeTextureWitness rowTamper = CopyWitness(
    textureWitness, packedRows: [.. packedRows.Where(item => !item.Synthetic), badSynthetic]);
Reject("texture row bytes", () => Compile(
    Id65AuthoringCompositeCompiler.CreateManifest(triangle, rowTamper, bundle, [t88, t107]),
    [t88, t107], witness: rowTamper));

Id65MobyDependencyBundleDescriptor badBundle = CopyBundle(bundle, canonicalSha256: new string('0', 64));
Reject("Moby descriptor canonical", () => Compile(
    Id65AuthoringCompositeCompiler.CreateManifest(triangle, textureWitness, badBundle, [t88, t107]),
    [t88, t107], suppliedBundle: badBundle));
Id65MobyDependencyBundleDescriptor dragonScus = new(
    bundle.Id,
    bundle.SchemaVersion,
    bundle.FoundationContractSha256,
    bundle.Rows,
    bundle.Properties,
    bundle.ActorPackages,
    bundle.ActorRoots,
    bundle.Fixups,
    [
        .. bundle.ExternalDependencies,
        new(
            "dragon-save.scus-slot35",
            Id65MobyExternalDependencyKind.GlobalExecutable,
            new Id65LogicalAddress("scus", 0x5FC37, 0),
            1,
            new string('0', 64),
            Id65MobyExternalDependencyPolicy.RequiresFutureCompositeCompiler)
    ],
    bundle.BehaviorClosure,
    bundle.RuntimeDependencies,
    bundle.CanonicalJson,
    bundle.CanonicalSha256);
RejectText("Moby SCUS dependency", () => Compile(
        Id65AuthoringCompositeCompiler.CreateManifest(triangle, textureWitness, dragonScus, [t88, t107]),
        [t88, t107], suppliedBundle: dragonScus),
    "SCUS/global executable");

void CapacityReject(string label, Id65AuthoringCompositeCompilerLimits limits) =>
    Reject(label, () => Compile(manifest, [t88, t107], limits));
CapacityReject("model capacity", new(ModelByteCapacity: 0x947FF));
CapacityReject("used-model capacity", new(MaximumOutputUsedModelBytes: 0x94663));
CapacityReject("texture id capacity", new(MaximumNativeTextureId: 65));
CapacityReject("object count capacity", new(MaximumObjectCount: 107));
CapacityReject("actor root capacity", new(MaximumActorRootCount: 37));
CapacityReject("actor tail capacity", new(ActorSubfileEnd: 0x1CFBB7));
CapacityReject("scene tail capacity", new(SceneSubfileEnd: 0x813F));
CapacityReject("collision tree capacity", new(CollisionTreeCapacityBytes: 0x6A5F));
CapacityReject("collision blocks capacity", new(CollisionBlocksCapacityBytes: 0x17983));

byte[] badPreimage = first.CopySourceRow80();
badPreimage[0x800] ^= 1;
Reject("forward preimage", () => Id65AuthoringCompositeCompiler.ApplyTransactional(
    first, badPreimage, reverse: false));
byte[] badAfterimage = first.CopyOutputRow80();
badAfterimage[0xDE800 + 0x2B550] ^= 1;
Reject("inverse afterimage", () => Id65AuthoringCompositeCompiler.ApplyTransactional(
    first, badAfterimage, reverse: true));
Reject("rollback afterimage", () => Id65AuthoringCompositeCompiler.ApplyRollback(
    rollbackPlan, badAfterimage));
Reject("cross-output afterimage", () => Id65AuthoringCompositeCompiler.ApplyTransactional(
    first, noTerrain.CopyOutputRow80(), reverse: true));
Reject("missing diff owner", () => Id65AuthoringCompositeCompiler.ValidateDiffOwnershipForSmoke(
    first.CopySourceRow80(),
    first.CopyOutputRow80(),
    first.OwnedRanges.Where(item => item.StableId != "model.final").ToArray()));

Require(rejectionCount == 27, $"The composite rejection matrix ran {rejectionCount}, expected 27.");
Require(typeof(Id65AuthoringCompositeCompileRequest).GetProperties().All(property =>
            property.PropertyType != typeof(Id65CompiledAuthoringComposite) &&
            property.PropertyType != typeof(Id65AuthoringCompositeRollbackPlan) &&
            !property.PropertyType.Name.Contains("StaticPlan", StringComparison.Ordinal) &&
            !property.PropertyType.Name.Contains("Compiled", StringComparison.Ordinal)),
    "The compile request shape accepts a compiled afterimage, rollback plan, or slice plan.");
Require(first.BuildsDirectlyFromLockedSource && first.SinglePassCompositeRebuildOwned &&
        !first.AcceptsCompiledSliceAfterimages && !first.AcceptsSlicePlans &&
        !first.CrossSliceCompositionSupported && first.AppliesOneNonOverlappingLedger &&
        first.OwnsV2CollisionWinding && first.OwnsOptionalTerrainTriangle &&
        first.OwnsPrivateT66Texture && first.OwnsMobyDependencyBundles &&
        first.DirectRelocationsAndRebases && first.ExactInverseVerified &&
        first.DeterministicReadbackRequired && first.ProtectedRetailEntriesPreserved &&
        first.TargetOverlayPreservedExact && first.GlobalExecutablePreservedExact &&
        first.ExecutableMutationExcluded && first.InverseIsRollbackOnly &&
        !first.InverseIsAuthoringInput && !first.WritesFileSystem && !first.WritesDiscImage &&
        !first.WritesCue && !first.RetiredPublisherCalled && !first.AppIntegrated &&
        !first.CreateBinEnabled && !first.NormalCreateBinEnabled &&
        !first.RuntimeCandidateAuthorized && !first.ReleaseIntegrated &&
        !first.PromotionAuthorized && !first.Publishable && rollbackPlan.RollbackOnly &&
        !rollbackPlan.AcceptedAsCompileInput && !rollbackPlan.WritesFileSystem &&
        !rollbackPlan.WritesDiscImage && !rollbackPlan.WritesCue &&
        !rollbackPlan.RetiredPublisherCalled && !rollbackPlan.AppIntegrated &&
        !rollbackPlan.CreateBinEnabled && !rollbackPlan.NormalCreateBinEnabled &&
        !rollbackPlan.RuntimeCandidateAuthorized && !rollbackPlan.ReleaseIntegrated &&
        !rollbackPlan.PromotionAuthorized && !rollbackPlan.Publishable,
    "A direct/single-pass/no-afterimage/no-writer/no-publication safety boundary changed.");
Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(foundationImagePath) == foundationBefore &&
        SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!).SequenceEqual(lockedDirectoryBefore) &&
        SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!).SequenceEqual(foundationDirectoryBefore),
    "The pure composite compiler changed or published beside an input artifact.");

Console.WriteLine("ID65 single-pass composite pins:");
Console.WriteLine($"  manifest={first.ManifestSha256}");
Console.WriteLine($"  row80={first.OutputRow80Sha256}; model={first.OutputModelSha256}");
Console.WriteLine($"  changed={first.ChangedByteCount}; ranges={first.DiffRangeCount}; diff={first.DiffManifestSha256}");
Console.WriteLine($"  owned={first.OwnedRangeMapSha256}; relocations={first.RelocationMapSha256}");
Console.WriteLine($"  rebases={first.RebaseMapSha256}; mobyAssets={first.MobyAssetRelocationMapSha256}");
Console.WriteLine($"  transaction={first.TransactionSha256}; plan={first.DeterministicPlanSha256}");
Console.WriteLine(
    "PASS Id65AuthoringCompositeCompilerSmoke: direct locked-source negative-winding v2 + optional terrain + " +
    "private T66 + deduplicated T88/T107 Moby bundles, one complete ledger, maximal owned diffs, exact rollback, " +
    $"defensive readback, {rejectionCount} rejects, and no filesystem/BIN/CUE/App/CreateBIN/runtime/release/promotion.");

Id65CompiledAuthoringComposite Compile(
    Id65AuthoringCompositeManifest selectedManifest,
    IReadOnlyList<Id65MobyPlacementIntent> placements,
    Id65AuthoringCompositeCompilerLimits? limits = null,
    Id65V2NativeTextureWitness? witness = null,
    Id65MobyDependencyBundleDescriptor? suppliedBundle = null) =>
    Id65AuthoringCompositeCompiler.Compile(new(
        source,
        selectedManifest,
        witness ?? textureWitness,
        [suppliedBundle ?? bundle],
        placements,
        limits));

static bool Equivalent(Id65CompiledAuthoringComposite left, Id65CompiledAuthoringComposite right) =>
    left.ManifestSha256 == right.ManifestSha256 &&
    left.SourceRow80Sha256 == right.SourceRow80Sha256 &&
    left.OutputRow80Sha256 == right.OutputRow80Sha256 &&
    left.SourceModelSha256 == right.SourceModelSha256 &&
    left.OutputModelSha256 == right.OutputModelSha256 &&
    left.ChangedByteCount == right.ChangedByteCount &&
    left.DiffRangeCount == right.DiffRangeCount &&
    left.DiffManifestSha256 == right.DiffManifestSha256 &&
    left.OwnedRangeMapSha256 == right.OwnedRangeMapSha256 &&
    left.RelocationMapSha256 == right.RelocationMapSha256 &&
    left.RebaseMapSha256 == right.RebaseMapSha256 &&
    left.MobyAssetRelocationMapSha256 == right.MobyAssetRelocationMapSha256 &&
    left.TransactionSha256 == right.TransactionSha256 &&
    left.DeterministicPlanSha256 == right.DeterministicPlanSha256 &&
    left.OwnedRanges.Select(ToOwnedTuple).SequenceEqual(right.OwnedRanges.Select(ToOwnedTuple)) &&
    left.DiffRanges.SequenceEqual(right.DiffRanges) &&
    left.Relocations.SequenceEqual(right.Relocations) &&
    left.HandleRebases.SequenceEqual(right.HandleRebases) &&
    left.MobyAssetRelocations.SequenceEqual(right.MobyAssetRelocations) &&
    left.MobyPlacements.SequenceEqual(right.MobyPlacements) &&
    TerrainEquivalent(left.Terrain, right.Terrain) &&
    left.Texture == right.Texture && left.Capacity == right.Capacity &&
    left.CopyOutputRow80().SequenceEqual(right.CopyOutputRow80());

static bool TerrainEquivalent(
    Id65AuthoringCompositeTerrainReadback left,
    Id65AuthoringCompositeTerrainReadback right) =>
    left.MandatoryNegativeWindingV2 == right.MandatoryNegativeWindingV2 &&
    left.OptionalTrianglePresent == right.OptionalTrianglePresent &&
    left.SectorIndex == right.SectorIndex &&
    left.LowDetailVertexCount == right.LowDetailVertexCount &&
    left.LowDetailFaceCount == right.LowDetailFaceCount &&
    left.HighDetailVertexCount == right.HighDetailVertexCount &&
    left.HighDetailFaceCount == right.HighDetailFaceCount &&
    left.BaseCollisionTriangleIndex == right.BaseCollisionTriangleIndex &&
    left.BaseCollisionTriangleHex == right.BaseCollisionTriangleHex &&
    left.BaseCollisionNormalZ == right.BaseCollisionNormalZ &&
    left.EnvironmentSha256 == right.EnvironmentSha256 &&
    left.SectorSha256 == right.SectorSha256 &&
    left.CollisionSha256 == right.CollisionSha256 &&
    left.CollisionTreeSha256 == right.CollisionTreeSha256 &&
    left.CollisionBlocksSha256 == right.CollisionBlocksSha256 &&
    left.CollisionBlocksUsedBytes == right.CollisionBlocksUsedBytes &&
    left.OcclusionGroupIndex == right.OcclusionGroupIndex &&
    left.OcclusionContainsSector == right.OcclusionContainsSector &&
    TriangleEquivalent(left.OptionalTriangle, right.OptionalTriangle);

static bool TriangleEquivalent(Id65TerrainTriangleReadback? left, Id65TerrainTriangleReadback? right)
{
    if (left is null || right is null)
        return left is null && right is null;
    return left.TriangleId == right.TriangleId &&
        left.AddedVertexHandle == right.AddedVertexHandle &&
        left.PresentInSource == right.PresentInSource &&
        left.PresentInOutput == right.PresentInOutput &&
        left.SectorIndex == right.SectorIndex &&
        left.LowDetailVertexIndex == right.LowDetailVertexIndex &&
        left.HighDetailVertexIndex == right.HighDetailVertexIndex &&
        left.LowDetailFaceIndex == right.LowDetailFaceIndex &&
        left.HighDetailFaceIndex == right.HighDetailFaceIndex &&
        left.CollisionTriangleIndex == right.CollisionTriangleIndex &&
        left.AddedVertexWordHex == right.AddedVertexWordHex &&
        left.LowDetailFaceHex == right.LowDetailFaceHex &&
        left.HighDetailFaceHex == right.HighDetailFaceHex &&
        left.CollisionTriangleHex == right.CollisionTriangleHex &&
        left.RenderNormalZ == right.RenderNormalZ &&
        left.CollisionNormalZ == right.CollisionNormalZ &&
        left.CollisionCell == right.CollisionCell &&
        left.TargetCellSequence.SequenceEqual(right.TargetCellSequence) &&
        left.OcclusionGroupIndex == right.OcclusionGroupIndex;
}

static (string, string, int, int, string, string) ToOwnedTuple(Id65AuthoringCompositeOwnedRange range) =>
    (range.StableId, range.OwnerId, range.DataRelativeOffset, range.ByteLength,
        range.BeforeSha256, range.AfterSha256);

static int CountFixup(byte[] row80, int fieldOffset)
{
    int count = BinaryPrimitives.ReadInt32LittleEndian(row80.AsSpan(0x1D7F28, 4));
    int occurrences = 0;
    for (int index = 0; index < count; index++)
        if (BinaryPrimitives.ReadInt32LittleEndian(row80.AsSpan(0x1D7F2C + (index * 4), 4)) == fieldOffset)
            occurrences++;
    return occurrences;
}

static Id65V2NativeTextureWitness CopyWitness(
    Id65V2NativeTextureWitness source,
    IReadOnlyList<Id65V2NativeTexturePackedRow>? packedRows = null,
    IReadOnlyList<Id65V2NativeTexturePagePatch>? pagePatches = null,
    string? canonicalSha256 = null) =>
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
        canonicalSha256 ?? source.CanonicalSha256);

static Id65MobyDependencyBundleDescriptor CopyBundle(
    Id65MobyDependencyBundleDescriptor source,
    string? canonicalSha256 = null) =>
    new(
        source.Id,
        source.SchemaVersion,
        source.FoundationContractSha256,
        source.Rows,
        source.Properties,
        source.ActorPackages,
        source.ActorRoots,
        source.Fixups,
        source.ExternalDependencies,
        source.BehaviorClosure,
        source.RuntimeDependencies,
        source.CanonicalJson,
        canonicalSha256 ?? source.CanonicalSha256);

static byte[] ReadRootFile(
    string imagePath,
    string exactName,
    long relativeOffset,
    int byteLength) =>
    ReadRootFileWhere(
        imagePath,
        name => string.Equals(name, exactName, StringComparison.OrdinalIgnoreCase),
        relativeOffset,
        byteLength);

static byte[] ReadRootFileWhere(
    string imagePath,
    Predicate<string> predicate,
    long relativeOffset,
    int byteLength)
{
    DiscLayout layout = DiscImage.DetectLayout(imagePath);
    using FileStream image = File.OpenRead(imagePath);
    DiscFileRecord file = DiscImage.FindRootFileRecord(image, layout, predicate);
    if (relativeOffset < 0 || relativeOffset + byteLength > file.Size)
        throw new InvalidDataException("A composite smoke source read escaped its root file.");
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
