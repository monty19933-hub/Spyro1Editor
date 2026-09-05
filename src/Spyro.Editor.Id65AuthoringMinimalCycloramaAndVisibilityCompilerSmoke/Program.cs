using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "id65-authoring-aggregate-workspace",
    "_research",
    "unusedlevel65blank",
    "v1",
    "locked-base",
    "unusedlevel65blank-locked-base-v1.bin");
Require(File.Exists(lockedImagePath),
    "Missing exact schema-8 fixture: " + lockedImagePath);

FileState lockedBefore = SnapshotFile(lockedImagePath);
IReadOnlyList<string> directoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
Id65AuthoringEditorAggregateBoundSource boundSource =
    Id65AuthoringEditorAggregateBoundSource.Capture(lockedImagePath);
Id65AuthoringCompositeLockedSource lockedSource = boundSource.LockedSource;
UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3Template =
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
        .BuildStaticPlan(lockedImagePath);
Id65V2NativeTextureWitness textureWitness =
    Id65V2NativeTextureCompositionCompiler
        .DerivePinnedT66WitnessFromLockedBase(lockedImagePath);
Id65MobyDependencyBundleDescriptor grassBundle =
    Id65AuthoringMobyDependencyBundleCompiler
        .CreateArtisansGrassBundleFromLockedBase(lockedImagePath);
Id65AuthoringEditorDocument document =
    Id65AuthoringEditorDocument
        .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
            Id65RemotePadMobyKind.GrassT107,
            Id65RemotePadGrassT107Catalog.CenterPositionId,
            [Id65AuthoringTerrainSetCatalog.Tile1Id]);

Require(
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler.ProfileId ==
        "id65-authoring-schema8-minimal-cyclorama-and-sky-visibility-in-memory-p1" &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler.SchemaVersion == 1 &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler.SkyPartCount == 1 &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .VisibilityGroupCount == 16 &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .VisibilityDirectoryOcclusionRelativeOffset == 0x720 &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .VisibilityDirectoryByteLength == 0x254 &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .VisibilityGroupDataOffset == 0x44 &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .ExpectedVisibilityDirectoryRow80Offset == 0x10A488 &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .ExpectedOwnedRangeCount == 2 &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .ExpectedOwnedByteCount == 34_616 &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .ExpectedChangedByteCount == 32_294 &&
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .ExpectedUnchangedOwnedByteCount == 2_322,
    "The schema-8 profile, layout, or byte accounting pin changed.");

Id65CompiledAuthoringSingleSectorSubstrate schema7 =
    Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
        document,
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle));
Id65CompiledAuthoringMinimalCycloramaAndVisibility first =
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .CompileFromSingleSector(schema7);
Id65CompiledAuthoringMinimalCycloramaAndVisibility repeat =
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .CompileFromSingleSector(schema7);
Id65CompiledAuthoringMinimalCycloramaAndVisibility direct =
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler.Compile(new(
        Id65AuthoringEditorDocument.Parse(document.SerializeUtf8()),
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle));

Require(
    first.ProfileId ==
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler.ProfileId &&
    first.SchemaVersion ==
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler.SchemaVersion &&
    first.Schema7BaselineProfileId ==
        Id65AuthoringSingleSectorSubstrateCompiler.ProfileId &&
    first.DocumentCanonicalSha256 == document.CanonicalSha256 &&
    first.ActiveTerrainUnitIds.SequenceEqual(
        [Id65AuthoringTerrainSetCatalog.Tile1Id]) &&
    first.MobyKind == Id65RemotePadMobyKind.GrassT107 &&
    first.MobyPositionId ==
        Id65RemotePadGrassT107Catalog.CenterPositionId &&
    first.Schema7BaselineRow80Sha256 == schema7.OutputRow80Sha256 &&
    first.Schema7BaselineRow80Sha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .ExpectedSchema7Tile1GrassCenterRow80Sha256 &&
    first.Schema7BaselineModelSha256 == schema7.OutputModelSha256 &&
    first.Schema7DeterministicPlanSha256 == schema7.DeterministicPlanSha256 &&
    first.OutputRow80Sha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .ExpectedOutputRow80Sha256 &&
    first.CycloramaModelOffset ==
        Id65AuthoringBlankCycloramaCompiler
            .OneTerrainCycloramaModelOffset &&
    first.CycloramaRow80Offset ==
        Id65AuthoringBlankCycloramaCompiler
            .OneTerrainCycloramaRow80Offset &&
    first.CycloramaWadOffset ==
        Id65AuthoringBlankCycloramaCompiler
            .OneTerrainCycloramaWadOffset &&
    first.VisibilityDirectoryRow80Offset == 0x10A488 &&
    first.VisibilityDirectoryByteLength == 0x254,
    "The exact focused schema-7 to schema-8 physical binding changed.");
Require(
    first.OutputRow80Sha256 == repeat.OutputRow80Sha256 &&
    first.OutputModelSha256 == repeat.OutputModelSha256 &&
    first.OutputCycloramaSha256 == repeat.OutputCycloramaSha256 &&
    first.OutputVisibilityDirectorySha256 ==
        repeat.OutputVisibilityDirectorySha256 &&
    first.OutputOcclusionSha256 == repeat.OutputOcclusionSha256 &&
    first.OwnedRangeMapSha256 == repeat.OwnedRangeMapSha256 &&
    first.TransactionSha256 == repeat.TransactionSha256 &&
    first.ProtectedComplementSha256 ==
        repeat.ProtectedComplementSha256 &&
    first.DeterministicPlanSha256 == repeat.DeterministicPlanSha256 &&
    first.CopySchema7BaselineRow80().SequenceEqual(
        repeat.CopySchema7BaselineRow80()) &&
    first.CopyOutputRow80().SequenceEqual(repeat.CopyOutputRow80()) &&
    first.OutputRow80Sha256 == direct.OutputRow80Sha256 &&
    first.OutputModelSha256 == direct.OutputModelSha256 &&
    first.DeterministicPlanSha256 == direct.DeterministicPlanSha256 &&
    first.CopyOutputRow80().SequenceEqual(direct.CopyOutputRow80()),
    "The schema-8 compiler is not deterministic across entry paths.");
Require(
    first.PureInMemory &&
    first.ExactTile1GrassCenterSchema7BaselineRequired &&
    !first.CallerSchema7AfterimageAccepted &&
    first.ProducesSchema8AtomicPhysicalStage &&
    first.ExactlyTwoFullOwnedRanges &&
    first.CycloramaAndVisibilityWrittenAtomically &&
    first.FixedCycloramaCapacityPreserved &&
    first.DownstreamComponentOffsetsPreserved &&
    first.StandaloneSkyReparseVerified &&
    first.CodeOwnedMinimalGeometryVerified &&
    first.CodeOwnedSkyPartVisibilityVerified &&
    first.EveryVisibilityGroupContainsOnlyPartZero &&
    first.NoVisibilityReferenceExceedsPartCount &&
    first.OcclusionPrefixPreservedExactly &&
    first.EnvironmentPreservedExactly &&
    first.CollisionPreservedExactly &&
    first.TexturePreservedExactly &&
    first.SpawnAndMobyBytesPreservedExactly &&
    first.ExactInverseVerified && first.EveryChangedByteOwned &&
    first.ProtectedComplementVerified && first.ContainsOutputRow80Bytes &&
    !first.WritesFileSystem && !first.WritesDiscImage && !first.WritesCue &&
    !first.AppIntegrated && !first.UiIntegrated &&
    !first.DocumentPersistenceIntegrated && !first.MaterializerIntegrated &&
    !first.CreateBinEnabled && !first.NormalCreateBinEnabled &&
    !first.RuntimeCandidateAuthorized && !first.RuntimeProven &&
    !first.RuntimeVisibilityProven && !first.ReleaseIntegrated &&
    !first.PromotionAuthorized && !first.Publishable,
    "The schema-8 physical stage opened an integration/runtime gate.");

VerifySky(first);
VerifyVisibility(first);
VerifyProtectedComponents(schema7, first);
VerifyLedger(first);
VerifyDefensiveCopies(first);
VerifyVisibilityTamperGuards();

byte[] baselineRow = first.CopySchema7BaselineRow80();
byte[] outputRow = first.CopyOutputRow80();
byte[] forward =
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .ApplySchema7StageTransactional(first, baselineRow, reverse: false);
byte[] rollback =
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .ApplySchema7StageTransactional(first, forward, reverse: true);
Require(forward.SequenceEqual(outputRow) && rollback.SequenceEqual(baselineRow),
    "The schema-8 atomic transaction did not round-trip exactly.");
byte[] forgedPreimage = baselineRow.ToArray();
forgedPreimage[0x400] ^= 1;
Reject("forged schema-7 preimage", () =>
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .ApplySchema7StageTransactional(first, forgedPreimage, reverse: false));
byte[] forgedAfterimage = outputRow.ToArray();
forgedAfterimage[0x400] ^= 1;
Reject("forged schema-8 afterimage", () =>
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .ApplySchema7StageTransactional(first, forgedAfterimage, reverse: true));

Reject("null request", () =>
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler.Compile(null!));
Reject("null schema-7 baseline", () =>
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .CompileFromSingleSector(null!));
Reject("wrong Moby state", () =>
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler.Compile(new(
        Id65AuthoringEditorDocument
            .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                Id65RemotePadMobyKind.None,
                Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
                [Id65AuthoringTerrainSetCatalog.Tile1Id]),
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle)));
Reject("wrong terrain state", () =>
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler.Compile(new(
        Id65AuthoringEditorDocument
            .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                Id65RemotePadMobyKind.GrassT107,
                Id65RemotePadGrassT107Catalog.CenterPositionId,
                [Id65AuthoringTerrainSetCatalog.Tile2Id]),
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle)));

Require(SnapshotFile(lockedImagePath) == lockedBefore,
    "The pure schema-8 compiler changed the locked fixture file.");
Require(SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!)
        .SequenceEqual(directoryBefore),
    "The pure schema-8 compiler changed the locked fixture directory.");

Console.WriteLine(
    "ID65_AUTHORING_MINIMAL_CYCLORAMA_AND_VISIBILITY_COMPILER_SMOKE_OK " +
    $"baselineRowSha={first.Schema7BaselineRow80Sha256} " +
    $"outputRowSha={first.OutputRow80Sha256} " +
    $"outputModelSha={first.OutputModelSha256} " +
    $"cycloramaSha={first.OutputCycloramaSha256} " +
    $"geometrySha={first.OutputCycloramaGeometrySha256} " +
    $"visibilitySha={first.OutputVisibilityDirectorySha256} " +
    $"occlusionSha={first.OutputOcclusionSha256} " +
    $"owned={first.OwnedByteCount} changed={first.ChangedByteCount} " +
    $"unchangedOwned={first.UnchangedOwnedByteCount} " +
    $"ranges={first.OwnedRanges.Count} " +
    $"environmentSha={first.EnvironmentSha256} " +
    $"collisionSha={first.CollisionSha256} " +
    "groups=16 members=[0] direct=1 inverse=1 tamperGuards=6 " +
    "writes=0 runtime=0 release=0");
return;

static void VerifySky(
    Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled)
{
    byte[] output = compiled.CopyOutputCyclorama();
    Spyro1SkyBlockLayout reparsed =
        Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(output);
    Require(
        compiled.BaselineSkyLayout.PartCount == 35 &&
        compiled.BaselineSkyLayout.VertexCount == 1915 &&
        compiled.BaselineSkyLayout.ColorCount == 726 &&
        compiled.BaselineSkyLayout.PolygonCount == 2808 &&
        compiled.OutputSkyLayout.PartCount == 1 &&
        compiled.OutputSkyLayout.VertexCount == 3 &&
        compiled.OutputSkyLayout.ColorCount == 3 &&
        compiled.OutputSkyLayout.PolygonCount == 1 &&
        compiled.OutputSkyLayout.PaletteWordCount == 4 &&
        compiled.OutputSkyLayout.TrailingBytes == 0x849C &&
        compiled.OutputSkyLayout.BackgroundColor == "#263447/0x00" &&
        compiled.OutputSkyLayout.Sha256 == reparsed.Sha256 &&
        compiled.OutputSkyLayout.GeometrySha256 == reparsed.GeometrySha256 &&
        string.Equals(
            Hash(output),
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                .ExpectedOutputCycloramaSha256,
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            reparsed.GeometrySha256,
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                .ExpectedOutputCycloramaGeometrySha256,
            StringComparison.OrdinalIgnoreCase),
        "The schema-8 one-part sky failed exact reparse.");
}

static void VerifyVisibility(
    Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled)
{
    byte[] baselineRow = compiled.CopySchema7BaselineRow80();
    byte[] outputRow = compiled.CopyOutputRow80();
    byte[] baselineDirectory = compiled.CopyBaselineVisibilityDirectory();
    byte[] outputDirectory = compiled.CopyOutputVisibilityDirectory();
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .RequireCodeOwnedSkyVisibilityDirectory(outputDirectory, 1);
    Require(
        !baselineDirectory.SequenceEqual(outputDirectory) &&
        Hash(baselineDirectory) ==
            compiled.BaselineVisibilityDirectorySha256 &&
        Hash(outputDirectory) == compiled.OutputVisibilityDirectorySha256 &&
        compiled.OutputVisibilityDirectorySha256 ==
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                .ExpectedOutputVisibilityDirectorySha256 &&
        Hash(outputRow.AsSpan(
            compiled.OcclusionRow80Offset,
            Id65AuthoringBlankCycloramaCompiler
                .ExpectedVisibilityOcclusionByteLength)) ==
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                .ExpectedOutputOcclusionSha256 &&
        compiled.OutputOcclusionSha256 ==
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                .ExpectedOutputOcclusionSha256 &&
        baselineRow.AsSpan(
            compiled.OcclusionRow80Offset,
            0x720).SequenceEqual(outputRow.AsSpan(
                compiled.OcclusionRow80Offset,
                0x720)) &&
        Hash(outputRow.AsSpan(compiled.OcclusionRow80Offset, 0x720)) ==
            compiled.OcclusionPrefixSha256,
        "The schema-8 sky-part directory or protected occlusion prefix changed.");
}

static void VerifyProtectedComponents(
    Id65CompiledAuthoringSingleSectorSubstrate schema7,
    Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled)
{
    byte[] before = compiled.CopySchema7BaselineRow80();
    byte[] after = compiled.CopyOutputRow80();
    foreach (Id65ModelComponentRelocation component in
             schema7.ComponentRelocations)
    {
        int offset = checked(
            schema7.ModelRow80Offset + component.OutputRelativeOffset);
        ReadOnlySpan<byte> beforeComponent = before.AsSpan(
            offset,
            component.OutputByteLength);
        ReadOnlySpan<byte> afterComponent = after.AsSpan(
            offset,
            component.OutputByteLength);
        if (component.StableId == "component.cyclorama")
            Require(!beforeComponent.SequenceEqual(afterComponent),
                "The schema-8 cyclorama did not change.");
        else if (component.StableId == "component.occlusion")
            Require(
                beforeComponent.Slice(0, 0x720).SequenceEqual(
                    afterComponent.Slice(0, 0x720)) &&
                !beforeComponent.Slice(0x720).SequenceEqual(
                    afterComponent.Slice(0x720)),
                "The schema-8 occlusion replacement escaped its tail.");
        else
            Require(beforeComponent.SequenceEqual(afterComponent),
                "A protected schema-7 component changed: " +
                component.StableId);
    }
    Id65ModelComponentRelocation collision =
        schema7.ComponentRelocations.Single(item =>
            item.StableId == "component.collision");
    Id65ModelComponentRelocation texture =
        schema7.ComponentRelocations.Single(item =>
            item.StableId == "component.texture");
    Require(
        compiled.EnvironmentSha256 ==
            schema7.Environment.OutputEnvironmentSha256 &&
        compiled.CollisionSha256 == collision.OutputSha256 &&
        compiled.CollisionSha256 == schema7.CollisionSha256 &&
        compiled.TextureSha256 == texture.OutputSha256 &&
        compiled.TextureSha256 == schema7.OutputTextureComponentSha256,
        "Environment, collision, or T66 texture was not preserved.");
}

static void VerifyLedger(
    Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled)
{
    byte[] baseline = compiled.CopySchema7BaselineRow80();
    byte[] output = compiled.CopyOutputRow80();
    Require(
        compiled.OwnedRanges.Count == 2 &&
        compiled.InverseRanges.Count == 2 &&
        compiled.OwnedRanges[0].OwnerId ==
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                .VisibilityOwnerId &&
        compiled.OwnedRanges[0].DataRelativeOffset ==
            compiled.VisibilityDirectoryRow80Offset &&
        compiled.OwnedRanges[0].ByteLength == 0x254 &&
        compiled.OwnedRanges[1].OwnerId ==
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                .CycloramaOwnerId &&
        compiled.OwnedRanges[1].DataRelativeOffset ==
            compiled.CycloramaRow80Offset &&
        compiled.OwnedRanges[1].ByteLength == 0x84E4 &&
        compiled.OwnedByteCount == 34_616 &&
        compiled.ChangedByteCount == 32_294 &&
        compiled.UnchangedOwnedByteCount == 2_322,
        "The schema-8 exact two-range accounting changed.");
    int changed = 0;
    int owned = 0;
    for (int index = 0; index < compiled.OwnedRanges.Count; index++)
    {
        Id65AuthoringCompositeOwnedRange range = compiled.OwnedRanges[index];
        Id65AuthoringCompositeOwnedRange inverse =
            compiled.InverseRanges[compiled.InverseRanges.Count - 1 - index];
        Require(
            baseline.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(range.CopyBefore()) &&
            output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(range.CopyAfter()) &&
            inverse.DataRelativeOffset == range.DataRelativeOffset &&
            inverse.CopyBefore().SequenceEqual(range.CopyAfter()) &&
            inverse.CopyAfter().SequenceEqual(range.CopyBefore()),
            "A schema-8 full owned range changed.");
        owned += range.ByteLength;
        changed += CountChangedBytes(range.CopyBefore(), range.CopyAfter());
    }
    Require(owned == compiled.OwnedByteCount &&
        changed == compiled.ChangedByteCount &&
        changed == CountChangedBytes(baseline, output),
        "The schema-8 owned and changed byte counts diverged.");
}

static void VerifyDefensiveCopies(
    Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled)
{
    byte[] baseline = compiled.CopySchema7BaselineRow80();
    byte[] output = compiled.CopyOutputRow80();
    byte[] sky = compiled.CopyOutputCyclorama();
    byte[] visibility = compiled.CopyOutputVisibilityDirectory();
    baseline[0] ^= 1;
    output[0] ^= 1;
    sky[0] ^= 1;
    visibility[0] ^= 1;
    Require(
        Hash(compiled.CopySchema7BaselineRow80()) ==
            compiled.Schema7BaselineRow80Sha256 &&
        Hash(compiled.CopyOutputRow80()) == compiled.OutputRow80Sha256 &&
        string.Equals(
            Hash(compiled.CopyOutputCyclorama()),
            compiled.OutputCycloramaSha256,
            StringComparison.OrdinalIgnoreCase) &&
        Hash(compiled.CopyOutputVisibilityDirectory()) ==
            compiled.OutputVisibilityDirectorySha256,
        "A schema-8 result exposed mutable backing bytes.");
}

static void VerifyVisibilityTamperGuards()
{
    byte[] exact =
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .BuildCodeOwnedSkyVisibilityDirectory();
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler
        .RequireCodeOwnedSkyVisibilityDirectory(exact, 1);

    byte[] badCount = exact.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(badCount.AsSpan(0, 4), 15);
    Reject("visibility group count", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .RequireCodeOwnedSkyVisibilityDirectory(badCount, 1));
    byte[] badPointer = exact.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(badPointer.AsSpan(4, 4), 0x46);
    Reject("visibility pointer", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .RequireCodeOwnedSkyVisibilityDirectory(badPointer, 1));
    byte[] stalePart = exact.ToArray();
    stalePart[0x44] = 1;
    Reject("visibility stale part", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .RequireCodeOwnedSkyVisibilityDirectory(stalePart, 1));
    byte[] missingTerminator = exact.ToArray();
    missingTerminator[0x45] = 0;
    Reject("visibility terminator", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .RequireCodeOwnedSkyVisibilityDirectory(missingTerminator, 1));
    byte[] badPadding = exact.ToArray();
    badPadding[0x100] = 0;
    Reject("visibility padding", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .RequireCodeOwnedSkyVisibilityDirectory(badPadding, 1));
    Reject("visibility wrong sky part count", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .RequireCodeOwnedSkyVisibilityDirectory(exact, 35));
}

static int CountChangedBytes(
    ReadOnlySpan<byte> before,
    ReadOnlySpan<byte> after)
{
    Require(before.Length == after.Length,
        "Changed-byte inputs differ in length.");
    int changed = 0;
    for (int index = 0; index < before.Length; index++)
        if (before[index] != after[index]) changed++;
    return changed;
}

static void Reject(string label, Action action)
{
    try
    {
        action();
        throw new InvalidOperationException(
            label + " unexpectedly passed its guard.");
    }
    catch (Exception ex) when (ex is ArgumentNullException or
        InvalidDataException)
    {
    }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static FileState SnapshotFile(string path)
{
    FileInfo info = new(path);
    return new(
        info.Length,
        info.LastWriteTimeUtc.Ticks,
        Hash(File.ReadAllBytes(path)));
}

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories)
        .Select(item => Path.GetRelativePath(path, item))
        .OrderBy(item => item, StringComparer.Ordinal)
        .ToArray();

static string FindRepositoryRoot(string? explicitRoot)
{
    if (!string.IsNullOrWhiteSpace(explicitRoot))
        return Path.GetFullPath(explicitRoot);
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(
                current.FullName,
                "src",
                "Spyro.Editor.Core",
                "Spyro.Editor.Core.csproj")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException(
        "Could not locate the repository root.");
}

internal sealed record FileState(
    long Length,
    long LastWriteTicks,
    string Sha256);
