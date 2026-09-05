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
    "Missing exact minimal-cyclorama fixture: " + lockedImagePath);

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
    Id65AuthoringMinimalCycloramaGeometryCompiler.ProfileId ==
        "id65-authoring-schema7-single-sector-minimal-cyclorama-in-memory-p2" &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.SchemaVersion == 1 &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.CycloramaByteLength ==
        0x84E4 &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.MinimalUsedByteLength == 72 &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.MinimalTrailingByteLength ==
        0x849C &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.MinimalPartCount == 1 &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.MinimalVertexCount == 3 &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.MinimalColorCount == 3 &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.MinimalPolygonCount == 1 &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.BackgroundCommandByte == 0 &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.PartColorCommandByte == 0x30 &&
    Id65AuthoringMinimalCycloramaGeometryCompiler.ExpectedChangedByteCount ==
        31_760,
    "The minimal-cyclorama profile or fixed geometry layout changed.");

Id65CompiledAuthoringSingleSectorSubstrate schema7 =
    Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
        document,
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle));
Id65CompiledAuthoringMinimalCycloramaGeometry first =
    Id65AuthoringMinimalCycloramaGeometryCompiler
        .CompileFromSingleSector(schema7);
Id65CompiledAuthoringMinimalCycloramaGeometry repeat =
    Id65AuthoringMinimalCycloramaGeometryCompiler
        .CompileFromSingleSector(schema7);
Id65CompiledAuthoringMinimalCycloramaGeometry direct =
    Id65AuthoringMinimalCycloramaGeometryCompiler.Compile(new(
        Id65AuthoringEditorDocument.Parse(document.SerializeUtf8()),
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle));

Require(
    first.ProfileId ==
        Id65AuthoringMinimalCycloramaGeometryCompiler.ProfileId &&
    first.SchemaVersion ==
        Id65AuthoringMinimalCycloramaGeometryCompiler.SchemaVersion &&
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
        Id65AuthoringMinimalCycloramaGeometryCompiler
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
    "The exact Tile1 / Grass T107 / Center schema-7 binding changed.");
Require(
    first.OutputRow80Sha256 == repeat.OutputRow80Sha256 &&
    first.OutputModelSha256 == repeat.OutputModelSha256 &&
    first.OutputCycloramaSha256 == repeat.OutputCycloramaSha256 &&
    first.CodeOwnedGeometrySha256 == repeat.CodeOwnedGeometrySha256 &&
    first.ZeroTrailingCapacitySha256 ==
        repeat.ZeroTrailingCapacitySha256 &&
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
    "The minimal-cyclorama stage is not deterministic across entry paths.");
Require(
    first.PureInMemory &&
    first.ExactTile1GrassCenterSchema7BaselineRequired &&
    !first.CallerSchema7AfterimageAccepted &&
    first.FixedCycloramaCapacityPreserved &&
    first.DownstreamComponentOffsetsPreserved &&
    first.StandaloneSkyReparseVerified &&
    first.CodeOwnedMinimalGeometryVerified &&
    first.DegenerateZeroGeometryEncoded &&
    first.StructuralExperimentOnly &&
    first.PreservesSchema7SkyPartOcclusionDirectoryExactly &&
    !first.SkyPartOcclusionDirectoryRewritten &&
    !first.SkyPartOcclusionCompatibilityVerified &&
    first.RequiresSkyPartOcclusionRewriteBeforeRuntime &&
    !first.RuntimeVisibilityProven &&
    first.EnvironmentPreservedExactly &&
    first.OcclusionPreservedExactly &&
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
    !first.ReleaseIntegrated && !first.PromotionAuthorized &&
    !first.Publishable,
    "The pure minimal-cyclorama stage opened an integration/runtime gate.");

VerifySky(first);
VerifyProtectedComponents(schema7, first);
VerifyLedger(first);
VerifyDefensiveCopies(first);

byte[] baselineRow = first.CopySchema7BaselineRow80();
byte[] outputRow = first.CopyOutputRow80();
byte[] forward =
    Id65AuthoringMinimalCycloramaGeometryCompiler
        .ApplySchema7StageTransactional(first, baselineRow, reverse: false);
byte[] rollback =
    Id65AuthoringMinimalCycloramaGeometryCompiler
        .ApplySchema7StageTransactional(first, forward, reverse: true);
Require(forward.SequenceEqual(outputRow) && rollback.SequenceEqual(baselineRow),
    "The minimal-cyclorama stage transaction did not round-trip exactly.");
byte[] forgedPreimage = baselineRow.ToArray();
forgedPreimage[0x400] ^= 1;
Reject("forged schema-7 preimage", () =>
    Id65AuthoringMinimalCycloramaGeometryCompiler
        .ApplySchema7StageTransactional(first, forgedPreimage, reverse: false));
byte[] forgedAfterimage = outputRow.ToArray();
forgedAfterimage[0x400] ^= 1;
Reject("forged minimal-cyclorama afterimage", () =>
    Id65AuthoringMinimalCycloramaGeometryCompiler
        .ApplySchema7StageTransactional(first, forgedAfterimage, reverse: true));

Reject("null request", () =>
    Id65AuthoringMinimalCycloramaGeometryCompiler.Compile(null!));
Reject("null schema-7 baseline", () =>
    Id65AuthoringMinimalCycloramaGeometryCompiler
        .CompileFromSingleSector(null!));
Reject("wrong Moby state", () =>
    Id65AuthoringMinimalCycloramaGeometryCompiler.Compile(new(
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
    Id65AuthoringMinimalCycloramaGeometryCompiler.Compile(new(
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
    "The pure minimal-cyclorama compiler changed the locked fixture file.");
Require(SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!)
        .SequenceEqual(directoryBefore),
    "The pure minimal-cyclorama compiler changed the locked fixture directory.");

Console.WriteLine(
    "ID65_AUTHORING_MINIMAL_CYCLORAMA_GEOMETRY_COMPILER_SMOKE_OK " +
    $"baselineRowSha={first.Schema7BaselineRow80Sha256} " +
    $"outputRowSha={first.OutputRow80Sha256} " +
    $"outputModelSha={first.OutputModelSha256} " +
    $"outputCycloramaSha={first.OutputCycloramaSha256} " +
    $"geometrySha={first.CodeOwnedGeometrySha256} " +
    $"zeroTailSha={first.ZeroTrailingCapacitySha256} " +
    $"changed={first.ChangedByteCount} ranges={first.OwnedRanges.Count} " +
    $"environmentSha={first.EnvironmentSha256} " +
    $"occlusionSha={first.OcclusionSha256} " +
    $"collisionSha={first.CollisionSha256} " +
    "parts=1 vertices=3 colors=3 polygons=1 trailing=33948 " +
    "direct=1 inverse=1 protected=1 skyOcclusionCompatible=0 " +
    "writes=0 runtime=0 release=0");
return;

static void VerifySky(
    Id65CompiledAuthoringMinimalCycloramaGeometry compiled)
{
    byte[] baseline = compiled.CopySchema7BaselineCyclorama();
    byte[] output = compiled.CopyOutputCyclorama();
    Spyro1SkyBlockLayout reparsed =
        Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(output);
    Require(
        compiled.BaselineLayout.PartCount == 35 &&
        compiled.BaselineLayout.VertexCount == 1915 &&
        compiled.BaselineLayout.ColorCount == 726 &&
        compiled.BaselineLayout.PolygonCount == 2808 &&
        compiled.OutputLayout.PartCount == 1 &&
        compiled.OutputLayout.VertexCount == 3 &&
        compiled.OutputLayout.ColorCount == 3 &&
        compiled.OutputLayout.PolygonCount == 1 &&
        compiled.OutputLayout.PaletteWordCount == 4 &&
        compiled.OutputLayout.TrailingBytes == 0x849C &&
        compiled.OutputLayout.BackgroundColor == "#263447/0x00" &&
        string.Equals(
            compiled.OutputCycloramaSha256,
            Id65AuthoringMinimalCycloramaGeometryCompiler
                .ExpectedOutputCycloramaSha256,
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            compiled.CodeOwnedGeometrySha256,
            Id65AuthoringMinimalCycloramaGeometryCompiler
                .ExpectedOutputGeometrySha256,
            StringComparison.OrdinalIgnoreCase) &&
        compiled.OutputLayout.Sha256 == reparsed.Sha256 &&
        compiled.OutputLayout.GeometrySha256 == reparsed.GeometrySha256 &&
        string.Equals(compiled.OutputCycloramaSha256, Hash(output),
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(compiled.BaselineCycloramaSha256, Hash(baseline),
            StringComparison.OrdinalIgnoreCase) &&
        !baseline.SequenceEqual(output) &&
        output.AsSpan(40, 12).IndexOfAnyExcept((byte)0) < 0 &&
        output.AsSpan(64, 8).IndexOfAnyExcept((byte)0) < 0 &&
        output.AsSpan(72).IndexOfAnyExcept((byte)0) < 0 &&
        Hash(output.AsSpan(72)) == compiled.ZeroTrailingCapacitySha256,
        "The code-owned one-part degenerate sky failed exact reparse.");
}

static void VerifyProtectedComponents(
    Id65CompiledAuthoringSingleSectorSubstrate schema7,
    Id65CompiledAuthoringMinimalCycloramaGeometry compiled)
{
    byte[] before = compiled.CopySchema7BaselineRow80();
    byte[] after = compiled.CopyOutputRow80();
    foreach (Id65ModelComponentRelocation component in
             schema7.ComponentRelocations)
    {
        int offset = checked(
            schema7.ModelRow80Offset + component.OutputRelativeOffset);
        bool equal = before.AsSpan(offset, component.OutputByteLength)
            .SequenceEqual(after.AsSpan(offset, component.OutputByteLength));
        Require(
            equal == (component.StableId != "component.cyclorama"),
            "An unexpected schema-7 component changed: " +
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
            Id65AuthoringMinimalCycloramaGeometryCompiler
                .ExpectedSchema7Tile1EnvironmentSha256 &&
        compiled.EnvironmentSha256 ==
            schema7.Environment.OutputEnvironmentSha256 &&
        compiled.OcclusionSha256 ==
            Id65AuthoringSingleSectorSubstrateCompiler
                .ExpectedOutputOcclusionSha256 &&
        compiled.OcclusionSha256 == schema7.Occlusion.OutputOcclusionSha256 &&
        compiled.CollisionSha256 ==
            Id65AuthoringMinimalCycloramaGeometryCompiler
                .ExpectedSchema7Tile1CollisionSha256 &&
        compiled.CollisionSha256 == collision.OutputSha256 &&
        compiled.TextureSha256 == schema7.OutputTextureComponentSha256 &&
        compiled.TextureSha256 == texture.OutputSha256,
        "Environment, occlusion, collision, or T66 texture was not preserved.");
}

static void VerifyLedger(
    Id65CompiledAuthoringMinimalCycloramaGeometry compiled)
{
    byte[] baseline = compiled.CopySchema7BaselineRow80();
    byte[] output = compiled.CopyOutputRow80();
    Require(compiled.OwnedRanges.Count > 0 &&
        compiled.OwnedRanges.Count == compiled.InverseRanges.Count &&
        compiled.OwnedRanges.All(item =>
            item.OwnerId ==
                Id65AuthoringMinimalCycloramaGeometryCompiler.OwnerId &&
            item.DataRelativeOffset >= compiled.CycloramaRow80Offset &&
            item.DataRelativeOffset + item.ByteLength <=
                compiled.CycloramaRow80Offset + compiled.CycloramaByteLength),
        "The minimal-cyclorama ledger escaped its exact component.");
    int changed = 0;
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
            "A minimal-cyclorama forward/inverse range changed.");
        changed += CountChangedBytes(range.CopyBefore(), range.CopyAfter());
    }
    Require(changed == compiled.ChangedByteCount &&
        changed == CountChangedBytes(baseline, output),
        "The minimal-cyclorama changed-byte ownership count changed.");
}

static void VerifyDefensiveCopies(
    Id65CompiledAuthoringMinimalCycloramaGeometry compiled)
{
    byte[] baseline = compiled.CopySchema7BaselineRow80();
    byte[] output = compiled.CopyOutputRow80();
    byte[] cyclorama = compiled.CopyOutputCyclorama();
    baseline[0] ^= 1;
    output[0] ^= 1;
    cyclorama[0] ^= 1;
    Require(
        Hash(compiled.CopySchema7BaselineRow80()) ==
            compiled.Schema7BaselineRow80Sha256 &&
        Hash(compiled.CopyOutputRow80()) == compiled.OutputRow80Sha256 &&
        string.Equals(
            Hash(compiled.CopyOutputCyclorama()),
            compiled.OutputCycloramaSha256,
            StringComparison.OrdinalIgnoreCase),
        "A minimal-cyclorama result exposed mutable backing bytes.");
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
