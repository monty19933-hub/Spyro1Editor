using System.Security.Cryptography;
using System.Reflection;
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
Require(File.Exists(lockedImagePath), $"Missing exact locked display-name BIN: {lockedImagePath}");
Require(File.Exists(foundationImagePath), $"Missing exact foundation BIN: {foundationImagePath}");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState foundationBefore = SnapshotFile(foundationImagePath);
IReadOnlyList<string> lockedDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
IReadOnlyList<string> foundationDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!);

Id65RewardCensusSourceSnapshot snapshot = Id65RewardCensusContract.ReadLockedSource(lockedImagePath);
Id65RewardCensus census = Id65RewardCensusContract.Inspect(snapshot);
Id65AuthoringSupportManifest empty = Id65AuthoringSupportReplacementCompiler.CreateEmptyManifest();
Id65AuthoringSupportManifest core = Id65AuthoringSupportReplacementCompiler.AddCoreSupport(empty);
Id65AuthoringSupportManifest full = Id65AuthoringSupportReplacementCompiler.AddEnemyBay(core);
Id65RewardSupportLayout layout = Id65RewardSupportLayoutContract.Build(census, core, full);

UnusedLevel65NativeTextureStaticPlan oldTextureProof =
    UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(foundationImagePath);
Id65V2NativeTextureWitness textureWitness =
    Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(oldTextureProof);
UnusedLevel65MobyDependencyBundleContract grassContract =
    UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(foundationImagePath);
byte[] grassPackage = ReadWad(foundationImagePath, 0x9C1D20, 0x174);
Id65MobyDependencyBundleDescriptor grassBundle =
    Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundle(grassContract, grassPackage);

Id65RewardStagingLockedSource locked = Id65AuthoringRewardStagingCompiler.CaptureLockedSource(
    snapshot.Id65Data.Span,
    snapshot.Id65Overlay.Span,
    snapshot.Executable.Span,
    textureWitness);
Id65RewardStagingManifest coreManifest = Id65AuthoringRewardStagingCompiler.CreateCore4Manifest(
    census, layout, core, full, grassBundle);
Id65RewardStagingManifest fullReservations =
    Id65AuthoringRewardStagingCompiler.CreateFull8ReservationsOnlyManifest(
        census, layout, core, full, grassBundle);

Id65RewardStagingCompileRequest request = new(
    locked,
    coreManifest,
    census,
    layout,
    core,
    full,
    grassBundle);
Id65CompiledRewardStaging compiled = Id65AuthoringRewardStagingCompiler.Compile(request);
Id65CompiledRewardStaging repeat = Id65AuthoringRewardStagingCompiler.Compile(request with
{
    Manifest = Id65AuthoringRewardStagingCompiler.CreateCore4Manifest(
        census, layout, core, full, grassBundle)
});

Console.WriteLine($"  manifest={compiled.ManifestSha256}");
Console.WriteLine($"  grass-full={compiled.DescriptorFullFieldSha256}");
Console.WriteLine($"  output={compiled.OutputRow80Sha256}");
Console.WriteLine($"  changed={compiled.ChangedByteCount}; diff-ranges={compiled.DiffRangeCount}");
Console.WriteLine($"  diff={compiled.DiffManifestSha256}");
Console.WriteLine($"  owned={compiled.OwnedRangeMapSha256}");
Console.WriteLine($"  inverse={compiled.InverseRangeMapSha256}");
Console.WriteLine($"  transaction={compiled.TransactionSha256}");
Console.WriteLine($"  complement={compiled.ProtectedComplementSha256}");
Console.WriteLine($"  plan={compiled.DeterministicPlanSha256}");
Console.WriteLine($"  outside-model={compiled.Structure.OutsideModelRowSha256}");
Console.WriteLine(
    $"  outside-texture-pages-and-model={compiled.Structure.OutsideTexturePagesAndModelRowSha256}");

Require(compiled.OwnedLeafCount == 9_313 && compiled.GuardedByteCount == 1_027_212 &&
        compiled.OwnedRanges.Count == compiled.InverseRanges.Count &&
        compiled.Stationary.OwnerCount == 71 && compiled.Stationary.RewardTotal == 171 &&
        compiled.Dragons.Count == 4 && compiled.Dragons.Sum(item => item.OwnedByteCount) == 15_420 &&
        compiled.Dragons.Sum(item => item.ChangedByteCount) == 9_445 &&
        compiled.Dragons.Sum(item => item.DiffRangeCount) == 3_855 &&
        compiled.Dragons.Count(item => !item.RunToEndpointInsideNamedPrecinct) == 1 &&
        compiled.Dragons.Single(item => !item.RunToEndpointInsideNamedPrecinct).Id == "dragon-b" &&
        compiled.Dragons.All(item => item.RunToEndpointInsideCore4) &&
        compiled.ZeroEgg.ObjectCountAfter == 107 && compiled.ZeroEgg.FixupCountAfter == 0x81 &&
        compiled.Structure.ModelSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedCoreModelSha256 &&
        compiled.Structure.CoreSectorCount == 4 && compiled.Structure.ActiveCollisionCellCount == 16 &&
        compiled.Structure.CollisionBlocksUsedBytes == 0x82 && compiled.Structure.TextureCount == 67 &&
        compiled.Structure.HighestTextureId == 66,
    "The direct Core4 reward-staging semantic readback changed.");
Require(compiled.Structure.OutsideModelRowSha256 ==
            Id65AuthoringRewardStagingCompiler.ExpectedOutsideModelRowSha256 &&
        compiled.Structure.OutsideTexturePagesAndModelRowSha256 ==
            Id65AuthoringRewardStagingCompiler.ExpectedOutsideTexturePagesAndModelRowSha256,
    "An outside-model or protected nontexture/model projection changed meaning or identity.");
Require(Equivalent(compiled, repeat), "Repeated locked-source compilation was not deterministic.");

byte[] applied = Id65AuthoringRewardStagingCompiler.ApplyTransactional(
    compiled, compiled.CopySourceRow80(), reverse: false);
byte[] reversed = Id65AuthoringRewardStagingCompiler.ApplyTransactional(
    compiled, applied, reverse: true);
Require(applied.SequenceEqual(compiled.CopyOutputRow80()) &&
        reversed.SequenceEqual(compiled.CopySourceRow80()),
    "The direct reward-staging transaction did not round-trip exactly.");
Id65AuthoringRewardStagingCompiler.ValidateOwnershipForSmoke(
    compiled.CopySourceRow80(), compiled.CopyOutputRow80(), compiled.OwnedRanges);

Require(!compiled.AcceptsCompiledSupportAfterimage && !compiled.AcceptsCompiledCompositeAfterimage &&
        !compiled.AcceptsCompiledMobyAfterimage && !compiled.CallsRetiredPublisher &&
        !compiled.WriterAuthorized && !compiled.WritesFileSystem && !compiled.WritesDiscImage &&
        !compiled.WritesBin && !compiled.WritesCue && !compiled.EnemyRoutesProven &&
        !compiled.ChestChildBehaviorProven && !compiled.RuntimeProven &&
        !compiled.RuntimeCandidateAuthorized && !compiled.AllDragonRoutesContainedByNamedPrecincts &&
        !compiled.TreasureTotal200Proven && !compiled.SavePersistenceProven && !compiled.SaveAuthorized &&
        !compiled.MusicLongPlayProven && !compiled.ExitDestination65Proven &&
        !compiled.PromotionAuthorized && !compiled.AppIntegrated && !compiled.CreateBinEnabled &&
        !compiled.NormalCreateBinEnabled && !compiled.ReleaseAuthorized && !compiled.Publishable &&
        compiled.Full8Excluded && compiled.ExecutableMutationExcluded &&
        !compiled.AfterimageStackingAuthorized,
    "A reward-staging writer/runtime/totals/save/music/exit/promotion/App/CreateBIN gate opened.");
Require(fullReservations.Kind == Id65RewardStagingProfileKind.Full8ReservationsOnly &&
        fullReservations.MobileReservations.Count == 10 &&
        fullReservations.MobileReservations.Sum(item => item.RewardValue) == 29 &&
        !fullReservations.AuthorMobileEnemies,
    "The fail-closed Full8 reservation boundary changed.");

int rejectionCount = 0;
void Reject(string label, Action action, string requiredText)
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

byte[] wrongLockedRow = snapshot.Id65Data.ToArray();
wrongLockedRow[0x400] ^= 1;
Reject("wrong locked row", () => Id65AuthoringRewardStagingCompiler.CaptureLockedSource(
    wrongLockedRow, snapshot.Id65Overlay.Span, snapshot.Executable.Span, textureWitness), "SHA-256");
byte[] wrongOverlay = snapshot.Id65Overlay.ToArray();
wrongOverlay[0] ^= 1;
Reject("wrong overlay", () => Id65AuthoringRewardStagingCompiler.CaptureLockedSource(
    snapshot.Id65Data.Span, wrongOverlay, snapshot.Executable.Span, textureWitness), "SHA-256");
byte[] wrongExecutable = snapshot.Executable.ToArray();
wrongExecutable[0] ^= 1;
Reject("wrong executable", () => Id65AuthoringRewardStagingCompiler.CaptureLockedSource(
    snapshot.Id65Data.Span, snapshot.Id65Overlay.Span, wrongExecutable, textureWitness), "SHA-256");
Reject("compiled afterimage as locked source", () => Id65AuthoringRewardStagingCompiler.CaptureLockedSource(
    compiled.CopyOutputRow80(), snapshot.Id65Overlay.Span, snapshot.Executable.Span, textureWitness), "SHA-256");
Id65V2NativeTextureWitness forgedWitness = CopyWitness(
    textureWitness, canonicalSha256: new string('0', 64));
Reject("forged T66 witness", () => Id65AuthoringRewardStagingCompiler.CaptureLockedSource(
    snapshot.Id65Data.Span, snapshot.Id65Overlay.Span, snapshot.Executable.Span, forgedWitness), "witness");

Reject("Full8 reservations compiled", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Manifest = fullReservations }), "not a compilable authoring profile");
Reject("mobile authoring request", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { AuthorMobileEnemies = true }), "Mobile enemy authoring is fail-closed");
Reject("manifest mobile authoring", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Manifest = CopyManifest(coreManifest, authorMobileEnemies: true) }), "fail-closed boundary");
Reject("object capacity drift", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Capacity = new Id65RewardStagingCapacity(MaximumObjectCount: 106) }), "capacity");
Reject("root capacity drift", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Capacity = new Id65RewardStagingCapacity(MaximumActorRootCount: 39) }), "capacity");
Reject("track capacity drift", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Capacity = new Id65RewardStagingCapacity(CameraTrackEnd: 0x2E2000) }), "capacity");

Id65RewardStagingManifest forgedCanonical = CopyManifest(
    coreManifest, declaredCanonicalSha256: new string('0', 64));
Reject("forged manifest canonical hash", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Manifest = forgedCanonical }), "canonical reward-staging manifest");
Id65RewardStagingStationaryIntent[] movedStationary = coreManifest.Stationary.ToArray();
movedStationary[0] = movedStationary[0] with
{
    Point = movedStationary[0].Point with { X = movedStationary[0].Point.X + 1 }
};
Reject("stationary coordinate drift", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Manifest = CopyManifest(coreManifest, stationary: movedStationary) }),
    "descriptor values differ");
Id65RewardStagingStationaryIntent[] wrongRetirement = coreManifest.Stationary.ToArray();
wrongRetirement[0] = wrongRetirement[0] with
{
    RetirementByteIndex = wrongRetirement[0].RetirementByteIndex + 1
};
Reject("retirement address drift", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Manifest = CopyManifest(coreManifest, stationary: wrongRetirement) }),
    "descriptor values differ");
Id65RewardStagingDragonIntent[] movedDragon = coreManifest.Dragons.ToArray();
movedDragon[0] = movedDragon[0] with
{
    Translation = movedDragon[0].Translation with { X = movedDragon[0].Translation.X + 1 }
};
Reject("dragon uniform delta drift", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Manifest = CopyManifest(coreManifest, dragons: movedDragon) }),
    "descriptor values differ");
Id65RewardStagingDragonIntent[] wrongTrack = coreManifest.Dragons.ToArray();
wrongTrack[0] = wrongTrack[0] with { CameraTrackRelativeOffset = wrongTrack[0].CameraTrackRelativeOffset + 0x18 };
Reject("dragon track identity drift", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Manifest = CopyManifest(coreManifest, dragons: wrongTrack) }),
    "descriptor values differ");
Id65RewardStagingMobileReservation[] openedReservation = coreManifest.MobileReservations.ToArray();
openedReservation[0] = openedReservation[0] with { RoutePinned = true };
Reject("mobile route opened", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Manifest = CopyManifest(coreManifest, mobileReservations: openedReservation) }),
    "fail-closed boundary");

Reject("census total drift", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Census = census with { RewardTotal = 199 } }), "census identity/count");
Reject("layout hash drift", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { Layout = CopyLayout(layout, new string('0', 64)) }), "deterministic readback hash");
Reject("wrong Core support manifest", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { CoreSupportManifest = full }), "manifest identity changed");
Id65MobyActorPackageAsset forgedPackage = grassBundle.ActorPackages.Single() with { Alignment = 8 };
Reject("Grass package alignment drift", () => Id65AuthoringRewardStagingCompiler.Compile(
    request with { GrassBundle = CopyBundle(grassBundle, actorPackages: [forgedPackage]) }),
    "Grass row/property/package/root/fixup");

byte[] tamperedPreimage = compiled.CopySourceRow80();
tamperedPreimage[0x400] ^= 1;
Reject("transaction preimage tamper", () => Id65AuthoringRewardStagingCompiler.ApplyTransactional(
    compiled, tamperedPreimage, reverse: false), "locked-source preimage");
byte[] tamperedAfterimage = compiled.CopyOutputRow80();
tamperedAfterimage[0x400] ^= 1;
Reject("transaction afterimage tamper", () => Id65AuthoringRewardStagingCompiler.ApplyTransactional(
    compiled, tamperedAfterimage, reverse: true), "afterimage");
Reject("forward apply wrong direction", () => Id65AuthoringRewardStagingCompiler.ApplyTransactional(
    compiled, compiled.CopyOutputRow80(), reverse: false), "locked-source preimage");
Reject("reverse apply wrong direction", () => Id65AuthoringRewardStagingCompiler.ApplyTransactional(
    compiled, compiled.CopySourceRow80(), reverse: true), "afterimage");
byte[] unownedMutation = compiled.CopyOutputRow80();
unownedMutation[0x400] ^= 1;
Reject("protected complement mutation", () => Id65AuthoringRewardStagingCompiler.ValidateOwnershipForSmoke(
    compiled.CopySourceRow80(), unownedMutation, compiled.OwnedRanges), "diff manifest");
Id65RewardStagingOwnedRange firstOwned = compiled.OwnedRanges[0];
byte[] guardedMutation = compiled.CopyOutputRow80();
guardedMutation[firstOwned.DataRelativeOffset] ^= 1;
Reject("guarded afterimage mutation", () => Id65AuthoringRewardStagingCompiler.ValidateOwnershipForSmoke(
    compiled.CopySourceRow80(), guardedMutation, compiled.OwnedRanges), "declared before/afterimage");
Id65RewardStagingOwnedRange shiftedOwned = new(
    firstOwned.StableId,
    firstOwned.OwnerId,
    firstOwned.DataRelativeOffset + 1,
    firstOwned.CopyBefore(),
    firstOwned.CopyAfter());
Reject("owned range offset drift", () => Id65AuthoringRewardStagingCompiler.ValidateOwnershipForSmoke(
    compiled.CopySourceRow80(), compiled.CopyOutputRow80(),
    [shiftedOwned, .. compiled.OwnedRanges.Skip(1)]), "diff manifest");
Id65RewardStagingOwnedRange secondOwned = compiled.OwnedRanges[1];
Id65RewardStagingOwnedRange duplicateStableId = new(
    firstOwned.StableId,
    secondOwned.OwnerId,
    secondOwned.DataRelativeOffset,
    secondOwned.CopyBefore(),
    secondOwned.CopyAfter());
Reject("owned stable-id duplicate", () => Id65AuthoringRewardStagingCompiler.ValidateOwnershipForSmoke(
    compiled.CopySourceRow80(), compiled.CopyOutputRow80(),
    [firstOwned, duplicateStableId, .. compiled.OwnedRanges.Skip(2)]), "ownership ledger changed");
byte[] sourceForOverlap = compiled.CopySourceRow80();
Id65RewardStagingOwnedRange overlappingSecond = new(
    secondOwned.StableId,
    secondOwned.OwnerId,
    firstOwned.DataRelativeOffset,
    sourceForOverlap.AsSpan(firstOwned.DataRelativeOffset, secondOwned.ByteLength).ToArray(),
    secondOwned.CopyAfter());
Reject("owned range overlap", () => Id65AuthoringRewardStagingCompiler.ValidateOwnershipForSmoke(
    compiled.CopySourceRow80(), compiled.CopyOutputRow80(),
    [firstOwned, overlappingSecond, .. compiled.OwnedRanges.Skip(2)]), "overlap");
byte[] forgedBefore = firstOwned.CopyBefore();
forgedBefore[0] ^= 1;
Id65RewardStagingOwnedRange wrongOwnedPreimage = new(
    firstOwned.StableId,
    firstOwned.OwnerId,
    firstOwned.DataRelativeOffset,
    forgedBefore,
    firstOwned.CopyAfter());
Reject("owned range preimage tamper", () => Id65AuthoringRewardStagingCompiler.ValidateOwnershipForSmoke(
    compiled.CopySourceRow80(), compiled.CopyOutputRow80(),
    [wrongOwnedPreimage, .. compiled.OwnedRanges.Skip(1)]), "locked preimage/output");

byte[] defensiveOutput = compiled.CopyOutputRow80();
defensiveOutput[0] ^= 1;
byte[] defensiveBefore = compiled.OwnedRanges[0].CopyBefore();
defensiveBefore[0] ^= 1;
Require(Hash(compiled.CopyOutputRow80()) == compiled.OutputRow80Sha256 &&
        Hash(compiled.OwnedRanges[0].CopyBefore()) == compiled.OwnedRanges[0].BeforeSha256,
    "A compiled row or owned-range buffer escaped defensive copying.");

PropertyInfo[] requestProperties = typeof(Id65RewardStagingCompileRequest).GetProperties();
MethodInfo[] compilerMethods = typeof(Id65AuthoringRewardStagingCompiler)
    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
MethodInfo[] compileMethods = compilerMethods.Where(method => method.Name == "Compile").ToArray();
Require(requestProperties.All(property =>
            !property.PropertyType.Name.StartsWith("Id65Compiled", StringComparison.Ordinal) &&
            property.PropertyType != typeof(string) &&
            !typeof(Stream).IsAssignableFrom(property.PropertyType) &&
            !property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)) &&
        compileMethods.Length == 1 &&
        compileMethods[0].ReturnType == typeof(Id65CompiledRewardStaging) &&
        compileMethods[0].GetParameters().Length == 1 &&
        compileMethods[0].GetParameters()[0].ParameterType == typeof(Id65RewardStagingCompileRequest) &&
        !compileMethods[0].GetParameters()[0].Name!.Contains("Path", StringComparison.OrdinalIgnoreCase) &&
        compilerMethods.All(method =>
            !method.Name.Contains("Publish", StringComparison.OrdinalIgnoreCase) &&
            !method.Name.Contains("WriteBin", StringComparison.OrdinalIgnoreCase) &&
            !method.Name.Contains("WriteCue", StringComparison.OrdinalIgnoreCase) &&
            !method.Name.Contains("CreateBin", StringComparison.OrdinalIgnoreCase)) &&
        !locked.ContainsPath && !locked.ContainsCompiledSupportAfterimage &&
        !locked.ContainsCompiledCompositeAfterimage && !locked.ContainsCompiledMobyAfterimage &&
        !locked.ContainsPublisher && !coreManifest.ContainsCompiledSupportAfterimage &&
        !coreManifest.ContainsCompiledCompositeAfterimage && !coreManifest.ContainsCompiledMobyAfterimage &&
        !coreManifest.ContainsPublisher,
    "A compiled-afterimage/path/stream/publisher writer surface entered the request or compiler API.");

Require(rejectionCount == 31, $"The frozen atomic rejection matrix has {rejectionCount} cases, expected 31.");

Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(foundationImagePath) == foundationBefore &&
        SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!).SequenceEqual(lockedDirectoryBefore) &&
        SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!).SequenceEqual(foundationDirectoryBefore),
    "The static reward-staging smoke mutated a source file or directory.");

Console.WriteLine(
    "PASS Id65AuthoringRewardStagingCompilerSmoke identity pass: one exact locked-source Core4/T66/" +
    "ZeroEgg/71-owner/four-dragon transaction, 9,313 leaves, exact inverse/complement, Full8 reservations only, " +
    $"Dragon B precinct-route claim rejected, {rejectionCount} atomic rejects, and all " +
    "runtime/totals/save/music/exit/promotion/App/CreateBIN gates false.");

static bool Equivalent(Id65CompiledRewardStaging left, Id65CompiledRewardStaging right) =>
    left.ManifestSha256 == right.ManifestSha256 &&
    left.SourceRow80Sha256 == right.SourceRow80Sha256 &&
    left.OutputRow80Sha256 == right.OutputRow80Sha256 &&
    left.ChangedByteCount == right.ChangedByteCount &&
    left.DiffRangeCount == right.DiffRangeCount &&
    left.DiffManifestSha256 == right.DiffManifestSha256 &&
    left.OwnedRangeMapSha256 == right.OwnedRangeMapSha256 &&
    left.InverseRangeMapSha256 == right.InverseRangeMapSha256 &&
    left.TransactionSha256 == right.TransactionSha256 &&
    left.ProtectedComplementSha256 == right.ProtectedComplementSha256 &&
    left.DeterministicPlanSha256 == right.DeterministicPlanSha256 &&
    left.CopyOutputRow80().SequenceEqual(right.CopyOutputRow80());

static Id65RewardStagingManifest CopyManifest(
    Id65RewardStagingManifest source,
    IEnumerable<Id65RewardStagingStationaryIntent>? stationary = null,
    IEnumerable<Id65RewardStagingDragonIntent>? dragons = null,
    IEnumerable<Id65RewardStagingMobileReservation>? mobileReservations = null,
    bool? authorMobileEnemies = null,
    string? declaredCanonicalJson = null,
    string? declaredCanonicalSha256 = null) =>
    new(
        source.Kind,
        source.CensusSha256,
        source.LayoutSha256,
        source.SupportManifestSha256,
        source.GrassBundleSha256,
        stationary ?? source.Stationary,
        dragons ?? source.Dragons,
        mobileReservations ?? source.MobileReservations,
        authorMobileEnemies ?? source.AuthorMobileEnemies,
        declaredCanonicalJson,
        declaredCanonicalSha256);

static Id65RewardSupportLayout CopyLayout(
    Id65RewardSupportLayout source,
    string deterministicLayoutSha256) =>
    new(
        source.ProfileId,
        source.Source,
        source.Profiles,
        source.CoreEnvelope,
        source.FullEnvelope,
        source.EnemyBayEnvelope,
        source.LandingApron,
        source.StationarySlots,
        source.StationaryPlacements,
        source.DragonPrecincts,
        source.DragonSeparations,
        source.EnemyReservations,
        source.ZeroEgg,
        source.RuntimeGates,
        source.StaticSpacing,
        source.RejectedClaims,
        deterministicLayoutSha256);

static Id65MobyDependencyBundleDescriptor CopyBundle(
    Id65MobyDependencyBundleDescriptor source,
    IEnumerable<Id65MobyActorPackageAsset>? actorPackages = null) =>
    new(
        source.Id,
        source.SchemaVersion,
        source.FoundationContractSha256,
        source.Rows,
        source.Properties,
        actorPackages ?? source.ActorPackages,
        source.ActorRoots,
        source.Fixups,
        source.ExternalDependencies,
        source.BehaviorClosure,
        source.RuntimeDependencies,
        source.CanonicalJson,
        source.CanonicalSha256);

static Id65V2NativeTextureWitness CopyWitness(
    Id65V2NativeTextureWitness source,
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
        source.CopyPackedRows(),
        source.CopyPagePatches(),
        canonicalSha256 ?? source.CanonicalSha256);

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
