using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string baseImagePath = Path.Combine(
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
Require(File.Exists(baseImagePath), $"Missing exact locked display-name BIN: {baseImagePath}");
Require(File.Exists(foundationImagePath), $"Missing exact Moby foundation BIN: {foundationImagePath}");

FileState baseBefore = SnapshotFile(baseImagePath);
FileState foundationBefore = SnapshotFile(foundationImagePath);
IReadOnlyList<string> baseDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(baseImagePath)!);
IReadOnlyList<string> foundationDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!);

UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2 =
    UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.BuildStaticPlan(baseImagePath);
UnusedLevel65MobyDependencyBundleContract contract =
    UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(foundationImagePath);
byte[] packageBytes = ReadWad(foundationImagePath, 0x9C1D20, 0x174);
Id65MobyDependencyBundleDescriptor bundle =
    Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundle(contract, packageBytes);
Id65MobyDependencyBundleDescriptor repeatedBundle =
    Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundle(contract, packageBytes);
Require(bundle.CanonicalJson == repeatedBundle.CanonicalJson &&
        bundle.CanonicalSha256 == repeatedBundle.CanonicalSha256,
    "The exact Artisans Grass bundle descriptor is not deterministic.");

Id65AuthoringManifest minimal = Id65AuthoringModelCompiler.CreateMinimalV2EquivalentManifest();
Id65MobyPlacementIntent t107 =
    Id65AuthoringMobyDependencyBundleCompiler.CreateT107Placement();
Id65MobyPlacementIntent t88 =
    Id65AuthoringMobyDependencyBundleCompiler.CreateZeroEggT88Placement();
Require(t107.AtomicGroupId == t88.AtomicGroupId &&
        t107.AtomicGroupId == Id65AuthoringMobyDependencyBundleCompiler.AtomicPlacementGroupId,
    "T107 and T88 do not share the exact combined atomic group.");

Id65AuthoringManifest appendManifest = CreateScenarioManifest(
    minimal,
    Id65AuthoringMobyDependencyBundleCompiler.AppendManifestProfileId,
    [new(
        "moby.artisans-grass.t107",
        Id65AuthoringIntentDisposition.AuthorExact,
        107,
        124_384,
        102_304,
        8_192,
        bundle.Id)]);
Id65AuthoringManifest zeroEggManifest = CreateScenarioManifest(
    minimal,
    Id65AuthoringMobyDependencyBundleCompiler.ZeroEggManifestProfileId,
    [new(
        "moby.zero-egg.grass.t88",
        Id65AuthoringIntentDisposition.AuthorExact,
        88,
        96_850,
        141_732,
        12_248,
        bundle.Id)]);
Id65AuthoringManifest combinedManifest = CreateScenarioManifest(
    minimal,
    Id65AuthoringMobyDependencyBundleCompiler.CombinedManifestProfileId,
    [
        new(
            "moby.zero-egg.grass.t88",
            Id65AuthoringIntentDisposition.AuthorExact,
            88,
            96_850,
            141_732,
            12_248,
            bundle.Id),
        new(
            "moby.artisans-grass.t107",
            Id65AuthoringIntentDisposition.AuthorExact,
            107,
            124_384,
            102_304,
            8_192,
            bundle.Id)
    ]);

Id65CompiledMobyDependencyBundles append = Compile(appendManifest, [t107]);
Id65CompiledMobyDependencyBundles zeroEgg = Compile(zeroEggManifest, [t88]);
Id65CompiledMobyDependencyBundles combined = Compile(combinedManifest, [t107, t88]);
Id65CompiledMobyDependencyBundles combinedReordered = Compile(combinedManifest, [t88, t107]);

Require(append.ScenarioId == Id65AuthoringMobyDependencyBundleCompiler.AppendScenarioId &&
        zeroEgg.ScenarioId == Id65AuthoringMobyDependencyBundleCompiler.ZeroEggScenarioId &&
        combined.ScenarioId == Id65AuthoringMobyDependencyBundleCompiler.CombinedScenarioId,
    "Scenario classification changed.");
Require(combined.OutputDataSha256 == combinedReordered.OutputDataSha256 &&
        combined.DiffManifestSha256 == combinedReordered.DiffManifestSha256 &&
        combined.OwnedRangeMapSha256 == combinedReordered.OwnedRangeMapSha256 &&
        combined.AssetRelocationMapSha256 == combinedReordered.AssetRelocationMapSha256 &&
        combined.CombinedTransactionSha256 == combinedReordered.CombinedTransactionSha256 &&
        combined.DeterministicPlanSha256 == combinedReordered.DeterministicPlanSha256,
    "Combined compilation depends on caller placement order.");

Require(combined.OutputDataSha256 ==
            Id65AuthoringMobyDependencyBundleCompiler.ExpectedCombinedOutputDataSha256 &&
        combined.ChangedDataByteCount ==
            Id65AuthoringMobyDependencyBundleCompiler.ExpectedCombinedChangedDataByteCount &&
        combined.DiffRangeCount ==
            Id65AuthoringMobyDependencyBundleCompiler.ExpectedCombinedDiffRangeCount &&
        combined.MobyChangedByteCountOverV2 ==
            Id65AuthoringMobyDependencyBundleCompiler.ExpectedCombinedMobyChangedBytesOverV2,
    "The independent combined witness pins changed.");
Require(append.MobyChangedByteCountOverV2 == 293 &&
        zeroEgg.MobyChangedByteCountOverV2 == 276 &&
        combined.MobyChangedByteCountOverV2 == 302,
    "T107, ZeroEgg, or deduplicated combined Moby changed-byte counts changed.");
Require(append.OwnedRanges.Count == 8 && append.OwnedRanges.Sum(item => item.ByteLength) == 486 &&
        zeroEgg.OwnedRanges.Count == 5 && zeroEgg.OwnedRanges.Sum(item => item.ByteLength) == 474 &&
        combined.OwnedRanges.Count == 9 && combined.OwnedRanges.Sum(item => item.ByteLength) == 574,
    "The exact guarded Moby ownership envelopes changed.");
Require(combined.OwnedRanges.Select(item => (item.DataRelativeOffset, item.ByteLength)).SequenceEqual(
        new[]
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
    "The combined root/package/rows/fixup/properties allowlist changed.");
Require(combined.AssetRelocations.Count == 5 &&
        combined.AssetRelocations.Take(3).All(item => item.Deduplicated) &&
        combined.AssetRelocations.Count(item => item.Kind == "actor-root") == 1 &&
        combined.AssetRelocations.Count(item => item.Kind == "actor-package") == 1 &&
        combined.AssetRelocations.Count(item => item.Kind == "scene-properties") == 1 &&
        combined.AssetRelocations.Count(item => item.Kind == "moby-row") == 2,
    "Combined compilation did not deduplicate root 37, package, and properties exactly once.");

VerifyCapacity(append, objectCountAfter: 108, fixupCountAfter: 0x82);
VerifyCapacity(zeroEgg, objectCountAfter: 107, fixupCountAfter: 0x81);
VerifyCapacity(combined, objectCountAfter: 108, fixupCountAfter: 0x82);
VerifyPlacement(append.Placements.Single(), 107, 124_384, 102_304, 8_192,
    "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb");
VerifyPlacement(zeroEgg.Placements.Single(), 88, 96_850, 141_732, 12_248,
    "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f");
Require(combined.Placements.Select(item => item.TargetTrueIndex).SequenceEqual(new[] { 88, 107 }),
    "Combined placement readback order changed.");

byte[] combinedOutput = combined.CopyOutputData();
Require(Hash(combinedOutput.AsSpan(0, 0x200)) ==
            "6157dc52dbf53f744378aa5577209751dc75eb397401504ba23a28f1d5dabd71" &&
        Hash(combinedOutput.AsSpan(0x173000, 0x5D000)) ==
            "e5e0f898a2d9487d56da9b28c1fb53706df219380ce5831f79334b06ebd1118f" &&
        Hash(combinedOutput.AsSpan(0x1D0000, 0x8800)) ==
            "170dcef6eba922addeddae47449150f905e94fe5de9d8b3af2834d165b13a0bf" &&
        Hash(combinedOutput.AsSpan(0x1D0170, 108 * 0x58)) ==
            "bf4c7d4f3f2f2f89c20da6a7d63c9033f4f109fad28a58c74212b95b83783104" &&
        Hash(combinedOutput.AsSpan(0xDE800, 0x94800)) ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputModelSha256,
    "The combined header/actor/scene/object-table/model structural readback changed.");
Require(BinaryPrimitives.ReadUInt32LittleEndian(combinedOutput.AsSpan(0xE4, 4)) == 0x1CFA44 &&
        BinaryPrimitives.ReadUInt16LittleEndian(combinedOutput.AsSpan(0x19A, 2)) == 0x01F5 &&
        BinaryPrimitives.ReadUInt32LittleEndian(combinedOutput.AsSpan(0x1D016C, 4)) == 108 &&
        BinaryPrimitives.ReadUInt32LittleEndian(combinedOutput.AsSpan(0x1D7F28, 4)) == 0x82 &&
        BinaryPrimitives.ReadUInt32LittleEndian(combinedOutput.AsSpan(0x1D8130, 4)) == 0x2638,
    "The combined root/count/fixup byte readback changed.");

byte[] source = combined.CopySourceData();
byte[] applied = Id65AuthoringMobyDependencyBundleCompiler.ApplyTransactional(combined, source, reverse: false);
byte[] reversed = Id65AuthoringMobyDependencyBundleCompiler.ApplyTransactional(combined, applied, reverse: true);
Require(applied.SequenceEqual(combinedOutput) && reversed.SequenceEqual(source),
    "Combined transaction did not round-trip exactly.");
ExpectReject(
    "prior winding-v2 afterimage input",
    () => Id65AuthoringMobyDependencyBundleCompiler.ApplyTransactional(
        combined,
        v2.OutputData.ToArray(),
        reverse: false),
    "locked-source preimage");
byte[] tamperedSource = source.ToArray();
tamperedSource[0x1CFA44] ^= 1;
ExpectReject(
    "tampered locked source",
    () => Id65AuthoringMobyDependencyBundleCompiler.ApplyTransactional(combined, tamperedSource, reverse: false),
    "locked-source preimage");
byte[] tamperedOutput = applied.ToArray();
tamperedOutput[0x1D2638] ^= 1;
ExpectReject(
    "tampered combined afterimage",
    () => Id65AuthoringMobyDependencyBundleCompiler.ApplyTransactional(combined, tamperedOutput, reverse: true),
    "afterimage");
ExpectReject(
    "cross-scenario afterimage",
    () => Id65AuthoringMobyDependencyBundleCompiler.ApplyTransactional(
        append,
        zeroEgg.CopyOutputData(),
        reverse: true),
    "afterimage");

byte[] tamperedPackage = packageBytes.ToArray();
tamperedPackage[0] ^= 1;
ExpectReject(
    "tampered donor package",
    () => Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundle(contract, tamperedPackage),
    "foundation contract");
ExpectReject(
    "forged descriptor canonical SHA",
    () => CompileWithBundle(appendManifest, [t107], CopyBundle(
        bundle,
        canonicalSha256: new string('0', 64))),
    "full-field");
ExpectReject(
    "removed exact executable dependency",
    () => CompileWithBundle(appendManifest, [t107], CopyBundle(
        bundle,
        externalDependencies: bundle.ExternalDependencies.Take(1).ToArray())),
    "external/runtime dependency");
Id65MobyRowTemplate rowWithForgedSpan = bundle.Rows.Single() with
{
    MutablePlacementFields = [new Id65MobyFieldSpan(0x0C, 12, "raw-xyz")]
};
ExpectReject(
    "forged row mutable spans",
    () => CompileWithBundle(appendManifest, [t107], CopyBundle(bundle, rows: [rowWithForgedSpan])),
    "exact passive");
ExpectReject(
    "forged package alignment",
    () => CompileWithBundle(appendManifest, [t107], CopyBundle(
        bundle,
        actorPackages: [bundle.ActorPackages.Single() with { Alignment = 8 }])),
    "exact passive");
ExpectReject(
    "forged fixup policy",
    () => CompileWithBundle(appendManifest, [t107], CopyBundle(
        bundle,
        fixups: [bundle.Fixups.Single() with { RegisterInternalPropertiesFields = true }])),
    "exact passive");
UnusedLevel65MobyDependencyRequirement[] forgedRuntimeDependencies = bundle.RuntimeDependencies.ToArray();
forgedRuntimeDependencies[0] = forgedRuntimeDependencies[0] with { Evidence = "forged" };
ExpectReject(
    "forged runtime dependency evidence",
    () => CompileWithBundle(appendManifest, [t107], CopyBundle(
        bundle,
        runtimeDependencies: forgedRuntimeDependencies)),
    "full-field");
ExpectReject(
    "wrong T88 coordinate policy",
    () => Compile(zeroEggManifest, [t88 with
    {
        CoordinatePolicy = Id65MobyCoordinatePolicy.AuthorExact,
        CoordinatesOrDelta = new Id65AuthoringPoint(96_850, 141_732, 12_248)
    }]),
    "preserve destination");
ExpectReject(
    "forged T107 placement ID",
    () => Compile(appendManifest, [t107 with { Id = "placement.forged.t107" }]),
    "Grass T107");
ExpectReject(
    "forged T88 atomic group",
    () => Compile(zeroEggManifest, [t88 with { AtomicGroupId = "atomic.forged" }]),
    "ZeroEgg T88");
ExpectReject(
    "non-contiguous append",
    () => Compile(appendManifest, [t107 with { TargetTrueIndex = 108 }]),
    "only exact T88");
ExpectReject(
    "duplicate target",
    () => Compile(combinedManifest, [t107, t107 with { Id = "placement.artisans-grass.duplicate" }]),
    "targets");
ExpectReject(
    "manifest/placement mismatch",
    () => Compile(appendManifest, [t88]),
    "profile");

ExpectReject(
    "object capacity",
    () => Compile(appendManifest, [t107], new Id65MobyBundleCompilerLimits(MaximumObjectCount: 107)),
    "object-row capacity");
ExpectReject(
    "root capacity",
    () => Compile(appendManifest, [t107], new Id65MobyBundleCompilerLimits(MaximumActorRootCount: 37)),
    "actor-root capacity");
ExpectReject(
    "actor tail capacity",
    () => Compile(appendManifest, [t107], new Id65MobyBundleCompilerLimits(ActorSubfileEnd: 0x1CFBB7)),
    "actor-tail capacity");
ExpectReject(
    "scene tail capacity",
    () => Compile(appendManifest, [t107], new Id65MobyBundleCompilerLimits(SceneSubfileEnd: 0x813F)),
    "scene-tail capacity");

Id65MobyDependencyBundleDescriptor dragonSave = new(
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
ExpectReject(
    "DragonSave requires SCUS",
    () => Id65AuthoringMobyDependencyBundleCompiler.Compile(new(
        appendManifest,
        v2,
        [dragonSave],
        [t107])),
    "SCUS/global executable");

Require(combined.BuildsFromLockedSourceInOneTransaction &&
        !combined.AcceptsPriorSliceAfterimage && !combined.CrossSliceCompositionSupported &&
        combined.DescriptorCanFeedFutureCompositeCompiler &&
        combined.V2StructuralPatchesComposedAtomically && combined.ExactInverseVerified &&
        combined.DeterministicReadbackVerified && combined.RootPackageAndPropertiesDeduplicated &&
        combined.ReplacedRowPrivateDataPreserved && combined.GlobalExecutableExcluded &&
        !combined.WritesFileSystem && !combined.WritesDiscImage && !combined.WritesCue &&
        !combined.RetiredPublisherCalled && !combined.AppIntegrated &&
        !combined.NormalCreateBinEnabled && !combined.ReleaseIntegrated &&
        !combined.RuntimeCandidateAuthorized && !combined.PromotionAuthorized && !combined.Publishable,
    "A pure compiler or fail-closed publication boundary weakened.");
Require(SnapshotFile(baseImagePath) == baseBefore && SnapshotFile(foundationImagePath) == foundationBefore &&
        SnapshotDirectory(Path.GetDirectoryName(baseImagePath)!).SequenceEqual(baseDirectoryBefore) &&
        SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!).SequenceEqual(foundationDirectoryBefore),
    "The pure Moby compiler changed an input file or source directory.");

Console.WriteLine("ID65 authoring Moby compiler frozen pins:");
Console.WriteLine($"  descriptor={bundle.CanonicalSha256}");
PrintPins("append", append);
PrintPins("zeroEgg", zeroEgg);
PrintPins("combined", combined);
Console.WriteLine(
    "PASS Id65AuthoringMobyDependencyBundleCompilerSmoke: exact winding-v2 + T107 append, " +
    "ZeroEgg T88 alone, combined root37/package/properties dedupe with only T107 new fixup, " +
    "SCUS-bound DragonSave rejection, locked-source one-transaction exact inverse/readback, " +
    "and no filesystem/writer/CUE/App/CreateBIN/release/promotion gates passed.");

Id65CompiledMobyDependencyBundles Compile(
    Id65AuthoringManifest manifest,
    IReadOnlyList<Id65MobyPlacementIntent> placements,
    Id65MobyBundleCompilerLimits? limits = null) =>
    Id65AuthoringMobyDependencyBundleCompiler.Compile(new(
        manifest,
        v2,
        [bundle],
        placements,
        limits));

Id65CompiledMobyDependencyBundles CompileWithBundle(
    Id65AuthoringManifest manifest,
    IReadOnlyList<Id65MobyPlacementIntent> placements,
    Id65MobyDependencyBundleDescriptor suppliedBundle) =>
    Id65AuthoringMobyDependencyBundleCompiler.Compile(new(
        manifest,
        v2,
        [suppliedBundle],
        placements));

static void PrintPins(string label, Id65CompiledMobyDependencyBundles compiled)
{
    Console.WriteLine(
        $"  {label}: manifest={compiled.ManifestSha256}; output={compiled.OutputDataSha256}; " +
        $"changed={compiled.ChangedDataByteCount}; ranges={compiled.DiffRangeCount}; " +
        $"diff={compiled.DiffManifestSha256}; mobyOverV2={compiled.MobyChangedByteCountOverV2}; " +
        $"owned={compiled.OwnedRangeMapSha256}; relocations={compiled.AssetRelocationMapSha256}; " +
        $"transaction={compiled.CombinedTransactionSha256}; plan={compiled.DeterministicPlanSha256}");
}

static Id65AuthoringManifest CreateScenarioManifest(
    Id65AuthoringManifest minimal,
    string profileId,
    IReadOnlyList<Id65MobyIntent> authoredMobys) =>
    new(
        profileId,
        minimal.LockedBase,
        minimal.Sectors,
        minimal.Vertices,
        minimal.RenderFaces,
        minimal.Collision,
        minimal.Occlusion,
        minimal.Textures,
        [minimal.Mobys.Single(), .. authoredMobys],
        minimal.Spawn,
        minimal.Music,
        minimal.Totals,
        minimal.Exit,
        minimal.Save);

static Id65MobyDependencyBundleDescriptor CopyBundle(
    Id65MobyDependencyBundleDescriptor source,
    string? foundationContractSha256 = null,
    IReadOnlyList<Id65MobyRowTemplate>? rows = null,
    IReadOnlyList<Id65MobyPropertiesAsset>? properties = null,
    IReadOnlyList<Id65MobyActorPackageAsset>? actorPackages = null,
    IReadOnlyList<Id65MobyActorRootRequirement>? actorRoots = null,
    IReadOnlyList<Id65MobySceneFixupPolicy>? fixups = null,
    IReadOnlyList<Id65MobyExternalDependencyIntent>? externalDependencies = null,
    UnusedLevel65MobyBehaviorClosureManifest? behaviorClosure = null,
    IReadOnlyList<UnusedLevel65MobyDependencyRequirement>? runtimeDependencies = null,
    string? canonicalJson = null,
    string? canonicalSha256 = null) =>
    new(
        source.Id,
        source.SchemaVersion,
        foundationContractSha256 ?? source.FoundationContractSha256,
        rows ?? source.Rows,
        properties ?? source.Properties,
        actorPackages ?? source.ActorPackages,
        actorRoots ?? source.ActorRoots,
        fixups ?? source.Fixups,
        externalDependencies ?? source.ExternalDependencies,
        behaviorClosure ?? source.BehaviorClosure,
        runtimeDependencies ?? source.RuntimeDependencies,
        canonicalJson ?? source.CanonicalJson,
        canonicalSha256 ?? source.CanonicalSha256);

static void VerifyCapacity(
    Id65CompiledMobyDependencyBundles compiled,
    int objectCountAfter,
    int fixupCountAfter)
{
    Id65MobyBundleCapacityReadback capacity = compiled.Capacity;
    Require(capacity.ObjectCountBefore == 107 && capacity.ObjectCountAfter == objectCountAfter &&
            capacity.ActorRootCountBefore == 37 && capacity.ActorRootCountAfter == 38 &&
            capacity.ActorTailBytesRemaining == 0x448 &&
            capacity.FixupCountBefore == 0x81 && capacity.FixupCountAfter == fixupCountAfter &&
            capacity.SceneTailBytesRemaining == 0x6C0,
        $"Capacity readback changed for {compiled.ScenarioId}.");
}

static void VerifyPlacement(
    Id65MobyPlacementReadback placement,
    int trueIndex,
    int rawX,
    int rawY,
    int rawZ,
    string rowSha256)
{
    Require(placement.TargetTrueIndex == trueIndex &&
            placement.RawX == rawX && placement.RawY == rawY && placement.RawZ == rawZ &&
            placement.ActorId == 0x01F5 && placement.PropertiesSceneOffset == 0x8138 &&
            placement.RowSha256 == rowSha256,
        $"Placement T{trueIndex} readback changed.");
}

static byte[] ReadWad(string imagePath, long wadOffset, int byteLength)
{
    DiscLayout layout = DiscImage.DetectLayout(imagePath);
    using FileStream image = File.OpenRead(imagePath);
    DiscFileRecord wad = DiscImage.FindRootFileRecord(
        image,
        layout,
        name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
    Require(wad.Lba == 37 && wad.Size == 0x6C18800, "Foundation WAD.WAD layout changed.");
    return DiscImage.ReadFileBytes(image, layout, wad.Lba, wadOffset, byteLength);
}

static void ExpectReject(string label, Action action, string requiredText)
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
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

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

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(long ByteLength, DateTime LastWriteUtc, string Sha256);
