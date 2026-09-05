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
    "Missing exact collapsed-cyclorama fixture: " + lockedImagePath);

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
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.ProfileId ==
        "id65-authoring-schema7-collapsed-cyclorama-geometry-transitional-probe-p2" &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.SchemaVersion == 1 &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .CycloramaByteLength == 0x84E4 &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ExpectedPartCount == 35 &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ExpectedVertexCount == 1_915 &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ExpectedColorCount == 726 &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ExpectedPolygonCount == 2_808 &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ExpectedCandidateCollapsedVertexWordCount == 1_880 &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ExpectedChangedVertexWordCount == 1_879 &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ExpectedChangedLogicalByteCount == 7_367 &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ExpectedPolygonChangedByteCount == 0 &&
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ExpectedOcclusionTailChangedByteCount == 0,
    "The collapsed-cyclorama transitional profile constants changed.");

Id65CompiledAuthoringSingleSectorSubstrate schema7 =
    Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
        document,
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle));
Id65CompiledAuthoringCollapsedCycloramaGeometryProbe first =
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .CompileFromSingleSector(schema7);
Id65CompiledAuthoringCollapsedCycloramaGeometryProbe repeat =
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .CompileFromSingleSector(schema7);
Id65CompiledAuthoringCollapsedCycloramaGeometryProbe direct =
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Compile(new(
        Id65AuthoringEditorDocument.Parse(document.SerializeUtf8()),
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle));

Require(
    first.ProfileId ==
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.ProfileId &&
    first.SchemaVersion ==
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.SchemaVersion &&
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
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
            .ExpectedSchema7Tile1GrassCenterRow80Sha256 &&
    first.Schema7BaselineModelSha256 == schema7.OutputModelSha256 &&
    first.Schema7DeterministicPlanSha256 == schema7.DeterministicPlanSha256 &&
    first.CycloramaModelOffset ==
        Id65AuthoringBlankCycloramaCompiler
            .OneTerrainCycloramaModelOffset &&
    first.CycloramaRow80Offset ==
        Id65AuthoringBlankCycloramaCompiler
            .OneTerrainCycloramaRow80Offset &&
    first.CycloramaWadOffset ==
        Id65AuthoringBlankCycloramaCompiler
            .OneTerrainCycloramaWadOffset &&
    first.CycloramaByteLength == 0x84E4,
    "The exact schema-7 p1 preimage binding changed.");

Require(
    first.OutputRow80Sha256 ==
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
            .ExpectedOutputRow80Sha256 &&
    first.BaselineCycloramaSha256 ==
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
            .ExpectedSchema7CycloramaSha256 &&
    first.OutputCycloramaSha256 ==
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
            .ExpectedOutputCycloramaSha256 &&
    first.OutputGeometrySha256 ==
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
            .ExpectedOutputGeometrySha256 &&
    first.CandidateCollapsedVertexWordCount == 1_880 &&
    first.ChangedVertexWordCount == 1_879 &&
    first.ChangedLogicalByteCount == 7_367 &&
    first.ChangedByteCount == 7_367 &&
    first.PolygonChangedByteCount == 0 &&
    first.OcclusionChangedByteCount == 0 &&
    first.OcclusionTailChangedByteCount == 0,
    "The exact collapsed-cyclorama output identity or diff census changed.");

Require(
    first.OutputRow80Sha256 == repeat.OutputRow80Sha256 &&
    first.OutputModelSha256 == repeat.OutputModelSha256 &&
    first.OutputCycloramaSha256 == repeat.OutputCycloramaSha256 &&
    first.OutputGeometrySha256 == repeat.OutputGeometrySha256 &&
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
    "Direct and precompiled collapsed-cyclorama paths differ.");

Require(
    first.PureInMemory && first.TransitionalProbeOnly &&
    !first.FullyOriginalCyclorama &&
    first.ExactTile1GrassCenterSchema7BaselineRequired &&
    !first.CallerSchema7AfterimageAccepted &&
    first.FixedCycloramaCapacityPreserved &&
    first.AllThirtyFivePartPointersPreservedExactly &&
    first.AllThirtyFivePartHeadersPreservedExactly &&
    first.PartCountsPreservedExactly &&
    first.ColorTablesPreservedExactly &&
    first.PolygonTablesPreservedExactly &&
    first.FirstVertexWordsPreservedExactly &&
    first.RemainingVertexWordsCollapsedPerPart &&
    first.DownstreamComponentOffsetsPreserved &&
    first.EnvironmentPreservedExactly &&
    first.EntireOcclusionComponentPreservedExactly &&
    first.SkyVisibilityDirectoryPreservedExactly &&
    first.OcclusionOpaqueTailPreservedExactly &&
    first.CollisionPreservedExactly && first.TexturePreservedExactly &&
    first.SpawnAndMobyBytesPreservedExactly &&
    first.ExactInverseVerified && first.EveryChangedByteOwned &&
    first.OwnedRangesContainOnlyChangedBytes &&
    first.ProtectedComplementVerified && first.ContainsOutputRow80Bytes &&
    !first.WritesFileSystem && !first.WritesDiscImage && !first.WritesCue &&
    !first.AppIntegrated && !first.UiIntegrated &&
    !first.DocumentPersistenceIntegrated && !first.MaterializerIntegrated &&
    !first.CreateBinEnabled && !first.NormalCreateBinEnabled &&
    !first.RuntimeCandidateAuthorized && !first.RuntimeProven &&
    !first.ReleaseIntegrated && !first.PromotionAuthorized &&
    !first.Publishable,
    "The transitional probe opened an authoring/runtime/release gate.");

VerifyIndependentSemanticReadback(schema7, first);
VerifyOwnedChangedBytes(first);
VerifyDefensiveCopies(first);

byte[] baselineRow = first.CopySchema7BaselineRow80();
byte[] outputRow = first.CopyOutputRow80();
byte[] forward =
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ApplySchema7StageTransactional(first, baselineRow, reverse: false);
byte[] rollback =
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ApplySchema7StageTransactional(first, forward, reverse: true);
Require(forward.SequenceEqual(outputRow) && rollback.SequenceEqual(baselineRow),
    "The collapsed-cyclorama transaction did not round-trip exactly.");

byte[] forgedPreimage = baselineRow.ToArray();
forgedPreimage[first.CycloramaRow80Offset + 1] ^= 1;
Reject("hostile schema-7 preimage", () =>
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ApplySchema7StageTransactional(
            first,
            forgedPreimage,
            reverse: false));
byte[] forgedAfterimage = outputRow.ToArray();
forgedAfterimage[first.OwnedRanges[0].DataRelativeOffset] ^= 1;
Reject("hostile probe afterimage", () =>
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ApplySchema7StageTransactional(
            first,
            forgedAfterimage,
            reverse: true));
byte[] forgedProtectedAfterimage = outputRow.ToArray();
forgedProtectedAfterimage[schema7.Occlusion.Row80Offset] ^= 1;
Reject("hostile protected afterimage", () =>
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .ApplySchema7StageTransactional(
            first,
            forgedProtectedAfterimage,
            reverse: true));

Reject("null request", () =>
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Compile(null!));
Reject("null precompiled schema-7", () =>
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
        .CompileFromSingleSector(null!));
Reject("wrong Moby preimage", () =>
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Compile(new(
        Id65AuthoringEditorDocument
            .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                Id65RemotePadMobyKind.None,
                Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
                [Id65AuthoringTerrainSetCatalog.Tile1Id]),
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle)));
Reject("wrong terrain preimage", () =>
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Compile(new(
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
    "The pure collapsed-cyclorama compiler changed the locked fixture file.");
Require(SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!)
        .SequenceEqual(directoryBefore),
    "The pure collapsed-cyclorama compiler changed the fixture directory.");

Console.WriteLine(
    "ID65_AUTHORING_COLLAPSED_CYCLORAMA_GEOMETRY_PROBE_COMPILER_SMOKE_OK " +
    $"baselineRowSha={first.Schema7BaselineRow80Sha256} " +
    $"outputRowSha={first.OutputRow80Sha256} " +
    $"outputModelSha={first.OutputModelSha256} " +
    $"outputCycloramaSha={first.OutputCycloramaSha256} " +
    $"geometrySha={first.OutputGeometrySha256} " +
    $"candidateWords={first.CandidateCollapsedVertexWordCount} " +
    $"changedWords={first.ChangedVertexWordCount} " +
    $"changedBytes={first.ChangedLogicalByteCount} " +
    $"ranges={first.OwnedRanges.Count} " +
    $"environmentSha={first.EnvironmentSha256} " +
    $"occlusionSha={first.OcclusionSha256} " +
    $"occlusionTailSha={first.OcclusionTailSha256} " +
    $"collisionSha={first.CollisionSha256} " +
    "parts=35 vertices=1915 colors=726 polygons=2808 " +
    "polygonChanges=0 occlusionChanges=0 occlusionTailChanges=0 " +
    "direct=1 inverse=1 protected=1 transitional=1 original=0 " +
    "writes=0 runtime=0 release=0");
return;

static void VerifyIndependentSemanticReadback(
    Id65CompiledAuthoringSingleSectorSubstrate schema7,
    Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled)
{
    byte[] baselineRow = compiled.CopySchema7BaselineRow80();
    byte[] outputRow = compiled.CopyOutputRow80();
    byte[] baseline = compiled.CopySchema7BaselineCyclorama();
    byte[] output = compiled.CopyOutputCyclorama();
    Spyro1SkyBlockLayout before =
        Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(baseline);
    Spyro1SkyBlockLayout after =
        Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(output);

    Require(
        before.PartCount == 35 && after.PartCount == 35 &&
        before.VertexCount == 1_915 && after.VertexCount == 1_915 &&
        before.ColorCount == 726 && after.ColorCount == 726 &&
        before.PolygonCount == 2_808 && after.PolygonCount == 2_808 &&
        before.PaletteWordCount == after.PaletteWordCount &&
        before.BackgroundColor == after.BackgroundColor &&
        before.BackgroundColorOffset == after.BackgroundColorOffset &&
        before.TrailingBytes == after.TrailingBytes &&
        Hash(baseline) ==
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                .ExpectedSchema7CycloramaSha256 &&
        Hash(output) ==
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                .ExpectedOutputCycloramaSha256 &&
        after.GeometrySha256.Equals(
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                .ExpectedOutputGeometrySha256,
            StringComparison.OrdinalIgnoreCase),
        "Independent standalone sky reparse changed structure or identity.");

    bool[] mutableVertexBytes = new bool[output.Length];
    int candidateWords = 0;
    int changedWords = 0;
    int polygonChanges = 0;
    for (int partIndex = 0; partIndex < before.Parts.Count; partIndex++)
    {
        Spyro1SkyPartLayout beforePart = before.Parts[partIndex];
        Spyro1SkyPartLayout afterPart = after.Parts[partIndex];
        Require(beforePart == afterPart,
            "Independent part pointer/header/count readback changed.");
        ReadOnlySpan<byte> first = output.AsSpan(
            beforePart.VertexTableOffset,
            4);
        Require(baseline.AsSpan(beforePart.VertexTableOffset, 4)
                .SequenceEqual(first),
            "A first vertex word changed.");
        for (int vertex = 1; vertex < beforePart.VertexCount; vertex++)
        {
            candidateWords++;
            int offset = beforePart.VertexTableOffset + vertex * 4;
            for (int index = 0; index < 4; index++)
                mutableVertexBytes[offset + index] = true;
            Require(output.AsSpan(offset, 4).SequenceEqual(first),
                "An output vertex did not collapse to its part's first word.");
            if (!baseline.AsSpan(offset, 4).SequenceEqual(first))
                changedWords++;
        }
        polygonChanges += CountChangedBytes(
            baseline.AsSpan(
                beforePart.PolygonTableOffset,
                beforePart.PolygonCount * 8),
            output.AsSpan(
                beforePart.PolygonTableOffset,
                beforePart.PolygonCount * 8));
    }
    int changedBytes = 0;
    for (int index = 0; index < baseline.Length; index++)
    {
        if (baseline[index] == output[index]) continue;
        changedBytes++;
        Require(mutableVertexBytes[index],
            "A changed cyclorama byte is not a non-first vertex byte.");
    }
    Require(candidateWords == 1_880 && changedWords == 1_879 &&
        changedBytes == 7_367 && polygonChanges == 0,
        "Independent collapsed vertex/polygon census changed.");

    for (int index = 0; index < baselineRow.Length; index++)
    {
        bool insideSky = index >= compiled.CycloramaRow80Offset &&
            index < compiled.CycloramaRow80Offset +
                compiled.CycloramaByteLength;
        if (!insideSky)
            Require(baselineRow[index] == outputRow[index],
                "Independent protected row complement changed.");
    }
    ReadOnlySpan<byte> beforeOcclusion = baselineRow.AsSpan(
        schema7.Occlusion.Row80Offset,
        schema7.Occlusion.ComponentByteLength);
    ReadOnlySpan<byte> afterOcclusion = outputRow.AsSpan(
        schema7.Occlusion.Row80Offset,
        schema7.Occlusion.ComponentByteLength);
    Require(beforeOcclusion.SequenceEqual(afterOcclusion) &&
        CountChangedBytes(beforeOcclusion, afterOcclusion) == 0 &&
        beforeOcclusion.Slice(
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailOffset,
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailByteLength)
        .SequenceEqual(afterOcclusion.Slice(
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailOffset,
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailByteLength)),
        "The complete occlusion component or sky-visibility tail changed.");
}

static void VerifyOwnedChangedBytes(
    Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled)
{
    byte[] baseline = compiled.CopySchema7BaselineRow80();
    byte[] output = compiled.CopyOutputRow80();
    bool[] owned = new bool[baseline.Length];
    int ownedBytes = 0;
    Require(compiled.OwnedRanges.Count > 0 &&
        compiled.OwnedRanges.Count == compiled.InverseRanges.Count,
        "The collapsed-cyclorama forward/inverse ledger count changed.");
    for (int index = 0; index < compiled.OwnedRanges.Count; index++)
    {
        Id65AuthoringCompositeOwnedRange range = compiled.OwnedRanges[index];
        Id65AuthoringCompositeOwnedRange inverse =
            compiled.InverseRanges[compiled.InverseRanges.Count - 1 - index];
        byte[] before = range.CopyBefore();
        byte[] after = range.CopyAfter();
        Require(
            range.OwnerId ==
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.OwnerId &&
            range.ByteLength is >= 1 and <= 4 &&
            range.DataRelativeOffset >= compiled.CycloramaRow80Offset &&
            range.DataRelativeOffset + range.ByteLength <=
                compiled.CycloramaRow80Offset + compiled.CycloramaByteLength &&
            before.Where((value, byteIndex) =>
                value == after[byteIndex]).Any() == false &&
            baseline.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(before) &&
            output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(after) &&
            inverse.DataRelativeOffset == range.DataRelativeOffset &&
            inverse.CopyBefore().SequenceEqual(after) &&
            inverse.CopyAfter().SequenceEqual(before),
            "An exact changed-byte forward/inverse range changed.");
        for (int byteIndex = 0; byteIndex < range.ByteLength; byteIndex++)
        {
            int absolute = range.DataRelativeOffset + byteIndex;
            Require(!owned[absolute], "Owned changed-byte ranges overlap.");
            owned[absolute] = true;
            ownedBytes++;
        }
    }
    for (int index = 0; index < baseline.Length; index++)
    {
        Require((baseline[index] != output[index]) == owned[index],
            "The smoke ledger does not exactly cover changed bytes.");
    }
    Require(ownedBytes == 7_367,
        "The owned changed-byte total is not 7,367.");
}

static void VerifyDefensiveCopies(
    Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled)
{
    byte[] baseline = compiled.CopySchema7BaselineRow80();
    byte[] output = compiled.CopyOutputRow80();
    byte[] sky = compiled.CopyOutputCyclorama();
    baseline[0] ^= 1;
    output[0] ^= 1;
    sky[0] ^= 1;
    Require(
        Hash(compiled.CopySchema7BaselineRow80()) ==
            compiled.Schema7BaselineRow80Sha256 &&
        Hash(compiled.CopyOutputRow80()) == compiled.OutputRow80Sha256 &&
        Hash(compiled.CopyOutputCyclorama()) ==
            compiled.OutputCycloramaSha256,
        "The compiled probe exposed mutable backing bytes.");
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
