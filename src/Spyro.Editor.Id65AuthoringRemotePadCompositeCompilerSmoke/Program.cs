using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

const int Row80ByteLength = 0x2E2000;
const int T107Offset = 0x1D2638;
const int T107Length = 0x58;

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
    "Missing exact locked schema-3 smoke image: " + lockedImagePath);

FileState lockedBefore = SnapshotFile(lockedImagePath);
IReadOnlyList<string> directoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);

Id65AuthoringEditorAggregateBoundSource boundSource =
    Id65AuthoringEditorAggregateBoundSource.Capture(lockedImagePath);
Id65AuthoringCompositeLockedSource lockedSource = boundSource.LockedSource;
Id65V2NativeTextureWitness textureWitness =
    Id65V2NativeTextureCompositionCompiler
        .DerivePinnedT66WitnessFromLockedBase(lockedImagePath);
Id65MobyDependencyBundleDescriptor grassBundle =
    Id65AuthoringMobyDependencyBundleCompiler
        .CreateArtisansGrassBundleFromLockedBase(lockedImagePath);

Require(
    lockedSource.Row80Sha256 ==
        Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
    lockedSource.TargetOverlaySha256 ==
        Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 &&
    lockedSource.GlobalExecutableSha256 ==
        Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 &&
    textureWitness.CanonicalSha256 ==
        Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 &&
    grassBundle.CanonicalSha256 ==
        Id65RemotePadGrassT107Catalog.BundleCanonicalSha256,
    "The exact locked source, T66 witness, or Grass bundle input pin changed.");

Dictionary<string, Id65CompiledAuthoringRemotePadComposite> compiledByPosition =
    new(StringComparer.Ordinal);
HashSet<string> outputHashes = new(StringComparer.Ordinal);
HashSet<string> deterministicHashes = new(StringComparer.Ordinal);
string? baselineOutputSha256 = null;
string? baselinePlanSha256 = null;
string? protectedComplementSha256 = null;
byte[]? referenceOutput = null;

foreach (Id65RemotePadGrassT107Position position in
         Id65RemotePadGrassT107Catalog.Positions)
{
    Id65AuthoringEditorDocument document =
        Id65AuthoringEditorDocument.CreateRemotePadGridExact(
            position.PositionId,
            includeOptionalTerrain: true,
            placementIds: [Id65RemotePadGrassT107Catalog.PlacementId],
            mandatoryBaseFaceTextureId:
                Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId);
    Id65CompiledAuthoringRemotePadComposite first = Compile(document);
    Id65CompiledAuthoringRemotePadComposite repeat = Compile(
        Id65AuthoringEditorDocument.Parse(document.SerializeUtf8()));
    Id65AuthoringRemotePadCompositeFrozenPin frozen =
        Id65AuthoringRemotePadCompositeCompiler.GetRequiredFrozenPin(
            position.PositionId);

    byte[] source = first.CopySourceRow80();
    byte[] output = first.CopyOutputRow80();
    byte[] repeatedOutput = repeat.CopyOutputRow80();
    Require(
        first.ProfileId == Id65AuthoringRemotePadCompositeCompiler.ProfileId &&
        first.SchemaVersion == Id65AuthoringRemotePadCompositeCompiler.SchemaVersion &&
        first.DocumentCanonicalSha256 == document.CanonicalSha256 &&
        first.CatalogCanonicalSha256 ==
            Id65RemotePadGrassT107Catalog.ExpectedCanonicalSha256 &&
        first.SourceRow80Sha256 ==
            Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
        first.OutputModelSha256 ==
            Id65AuthoringRemotePadCompositeCompiler.ExpectedOutputModelSha256 &&
        first.TargetOverlaySha256 ==
            Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 &&
        first.GlobalExecutableSha256 ==
            Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 &&
        first.OutputRow80Sha256 == repeat.OutputRow80Sha256 &&
        first.OwnedRangeMapSha256 == repeat.OwnedRangeMapSha256 &&
        first.TransactionSha256 == repeat.TransactionSha256 &&
        first.ProtectedComplementSha256 ==
            repeat.ProtectedComplementSha256 &&
        first.DeterministicPlanSha256 ==
            repeat.DeterministicPlanSha256 &&
        first.BaselineOutputRow80Sha256 ==
            Id65AuthoringRemotePadCompositeCompiler
                .ExpectedBaselineOutputRow80Sha256 &&
        first.BaselineDeterministicPlanSha256 ==
            Id65AuthoringRemotePadCompositeCompiler
                .ExpectedBaselineDeterministicPlanSha256 &&
        first.ChangedByteCount ==
            Id65AuthoringRemotePadCompositeCompiler.ExpectedChangedByteCount &&
        first.ProtectedComplementSha256 ==
            Id65AuthoringRemotePadCompositeCompiler
                .ExpectedProtectedComplementSha256 &&
        first.OutputRow80Sha256 == frozen.OutputRow80Sha256 &&
        first.OwnedRangeMapSha256 == frozen.OwnedRangeMapSha256 &&
        first.TransactionSha256 == frozen.TransactionSha256 &&
        first.DeterministicPlanSha256 == frozen.DeterministicPlanSha256 &&
        output.SequenceEqual(repeatedOutput),
        "Position " + position.PositionId +
        " did not compile deterministically from canonical inputs.");

    Require(
        first.Placement.PositionId == position.PositionId &&
        first.Placement.PositionOrdinal == position.Ordinal &&
        first.Placement.EditorX == position.EditorX &&
        first.Placement.EditorY == position.EditorY &&
        first.Placement.EditorZ == position.EditorZ &&
        first.Placement.RawX == position.RawX &&
        first.Placement.RawY == position.RawY &&
        first.Placement.RawZ == position.RawZ &&
        first.Placement.TargetTrueIndex ==
            Id65RemotePadGrassT107Catalog.TargetTrueIndex &&
        first.Placement.ActorId ==
            Id65RemotePadGrassT107Catalog.ActorId &&
        first.Placement.YawByte ==
            Id65RemotePadGrassT107Catalog.YawByte &&
        first.Placement.SupportHandle ==
            Id65RemotePadGrassT107Catalog.SupportHandle &&
        first.Placement.SupportCollisionTrueIndex ==
            Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex &&
        first.Placement.RowSha256 == position.RowSha256 &&
        Hash(output.AsSpan(T107Offset, T107Length)) ==
            position.RowSha256,
        "Position " + position.PositionId +
        " lost catalog coordinate, actor, support, or row identity.");

    Id65AuthoringCompositeOwnedRange[] grassRanges =
        first.OwnedRanges.Where(item =>
            item.StableId is
                "asset.root37" or
                "asset.actor-id37" or
                "asset.package.01f5" or
                "scene.object-count" or
                "placement.remote-pad.t107" or
                "scene.fixup-count" or
                "scene.fixup.t107-properties" or
                "asset.properties.grass").ToArray();
    Id65AuthoringCompositeOwnedRange t107Range =
        first.OwnedRanges.Single(item =>
            item.StableId == "placement.remote-pad.t107");
    Require(
        first.OwnedRanges.Count ==
            Id65AuthoringRemotePadCompositeCompiler.ExpectedOwnedRangeCount &&
        first.InverseRanges.Count ==
            Id65AuthoringRemotePadCompositeCompiler.ExpectedOwnedRangeCount &&
        first.OwnedRanges.Count(item => item.StableId.StartsWith(
            "texture.page.", StringComparison.Ordinal)) ==
                Id65AuthoringRemotePadCompositeCompiler
                    .ExpectedTextureOwnedRangeCount &&
        first.OwnedRanges.Count(item => item.StableId == "model.final") == 1 &&
        first.OwnedRanges.Count(item => item.OwnerId == "composite.v3-spawn") == 2 &&
        !first.OwnedRanges.Any(item => item.StableId == "placement.t88") &&
        grassRanges.Length ==
            Id65AuthoringRemotePadCompositeCompiler.ExpectedGrassOwnedRangeCount &&
        grassRanges.Select(item =>
                (item.DataRelativeOffset, item.ByteLength))
            .SequenceEqual(new[]
            {
                (0xE4, 4),
                (0x19A, 2),
                (0x1CFA44, 0x174),
                (0x1D016C, 4),
                (0x1D2638, 0x58),
                (0x1D7F28, 4),
                (0x1D8130, 4),
                (0x1D8138, 8)
            }) &&
        t107Range.OwnerId ==
            Id65RemotePadGrassT107Catalog.PlacementId &&
        t107Range.DataRelativeOffset == T107Offset &&
        t107Range.ByteLength == T107Length &&
        t107Range.AfterSha256 == position.RowSha256 &&
        first.OwnedRanges.Select(item => item.StableId)
            .Distinct(StringComparer.Ordinal).Count() ==
                first.OwnedRanges.Count,
        "Position " + position.PositionId +
        " lost its exact 7,960-range ownership partition.");

    VerifyOrderedNonoverlapAndInverse(first);
    VerifyEveryChangedByteIsOwned(source, output, first.OwnedRanges);
    byte[] applied =
        Id65AuthoringRemotePadCompositeCompiler.ApplyTransactional(
            first,
            source.ToArray(),
            reverse: false);
    byte[] rolledBack =
        Id65AuthoringRemotePadCompositeCompiler.ApplyTransactional(
            first,
            applied,
            reverse: true);
    Require(
        applied.SequenceEqual(output) &&
        rolledBack.SequenceEqual(source),
        "Position " + position.PositionId +
        " failed exact forward/inverse application.");

    byte[] mutatedSourceInput = source.ToArray();
    mutatedSourceInput[0x400] ^= 1;
    Reject(
        "position " + position.PositionId + " forged forward preimage",
        () => Id65AuthoringRemotePadCompositeCompiler.ApplyTransactional(
            first,
            mutatedSourceInput,
            reverse: false));
    byte[] mutatedAfterimage = output.ToArray();
    mutatedAfterimage[T107Offset + 0x0C] ^= 1;
    Reject(
        "position " + position.PositionId + " forged rollback afterimage",
        () => Id65AuthoringRemotePadCompositeCompiler.ApplyTransactional(
            first,
            mutatedAfterimage,
            reverse: true));

    byte[] defensiveOutput = first.CopyOutputRow80();
    defensiveOutput[0] ^= 1;
    Require(
        Hash(first.CopyOutputRow80()) == first.OutputRow80Sha256,
        "Position " + position.PositionId +
        " leaked mutable output bytes.");
    byte[] defensiveRange = t107Range.CopyAfter();
    defensiveRange[0] ^= 1;
    Require(
        Hash(t107Range.CopyAfter()) == t107Range.AfterSha256,
        "Position " + position.PositionId +
        " leaked mutable owned-range bytes.");

    baselineOutputSha256 ??= first.BaselineOutputRow80Sha256;
    baselinePlanSha256 ??= first.BaselineDeterministicPlanSha256;
    protectedComplementSha256 ??= first.ProtectedComplementSha256;
    Require(
        first.BaselineOutputRow80Sha256 == baselineOutputSha256 &&
        first.BaselineDeterministicPlanSha256 == baselinePlanSha256 &&
        first.ProtectedComplementSha256 == protectedComplementSha256,
        "The frozen baseline or protected complement varied by catalog position.");

    if (referenceOutput is null)
    {
        referenceOutput = output.ToArray();
    }
    else
    {
        Require(
            EqualOutsideSpan(
                referenceOutput,
                output,
                T107Offset,
                T107Length),
            "Position " + position.PositionId +
            " changed bytes outside the exact owned T107 row versus another grid position.");
    }

    Require(outputHashes.Add(first.OutputRow80Sha256),
        "Two grid positions produced the same output row-80 hash.");
    Require(deterministicHashes.Add(first.DeterministicPlanSha256),
        "Two grid positions produced the same deterministic-plan hash.");
    compiledByPosition.Add(position.PositionId, first);
}

Require(
    compiledByPosition.Count == 9 &&
    outputHashes.Count == 9 &&
    deterministicHashes.Count == 9,
    "The nine-position deterministic output set changed.");

Id65CompiledAuthoringRemotePadComposite firstPosition =
    compiledByPosition[Id65RemotePadGrassT107Catalog.Positions[0].PositionId];
Id65CompiledAuthoringRemotePadComposite lastPosition =
    compiledByPosition[Id65RemotePadGrassT107Catalog.Positions[^1].PositionId];
Reject(
    "cross-position rollback afterimage",
    () => Id65AuthoringRemotePadCompositeCompiler.ApplyTransactional(
        firstPosition,
        lastPosition.CopyOutputRow80(),
        reverse: true));

int matrixStates = 0;
int matrixSucceeded = 0;
int matrixRejected = 0;
foreach (bool terrainOn in new[] { false, true })
foreach (bool t88On in new[] { false, true })
foreach (bool t107On in new[] { false, true })
foreach (int mandatoryTextureId in new[] { 25, 66 })
foreach (Id65RemotePadGrassT107Position position in
         Id65RemotePadGrassT107Catalog.Positions)
{
    matrixStates++;
    List<string> placements = [];
    if (t88On)
        placements.Add(Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId);
    if (t107On)
        placements.Add(Id65RemotePadGrassT107Catalog.PlacementId);
    Id65AuthoringEditorDocument matrixDocument =
        Id65AuthoringEditorDocument.CreateRemotePadGridExact(
            position.PositionId,
            includeOptionalTerrain: terrainOn,
            placementIds: placements,
            mandatoryBaseFaceTextureId: mandatoryTextureId);
    bool supported = terrainOn && !t88On && t107On && mandatoryTextureId == 66;
    if (supported)
    {
        Id65CompiledAuthoringRemotePadComposite matrixCompiled =
            Compile(matrixDocument);
        Require(
            compiledByPosition.TryGetValue(
                position.PositionId,
                out Id65CompiledAuthoringRemotePadComposite? existing) &&
            existing.DocumentCanonicalSha256 == matrixDocument.CanonicalSha256 &&
            matrixCompiled.OutputRow80Sha256 == existing.OutputRow80Sha256 &&
            matrixCompiled.OwnedRangeMapSha256 == existing.OwnedRangeMapSha256 &&
            matrixCompiled.TransactionSha256 == existing.TransactionSha256 &&
            matrixCompiled.DeterministicPlanSha256 ==
                existing.DeterministicPlanSha256,
            "The supported schema-3 matrix state was not compiled exactly.");
        matrixSucceeded++;
        continue;
    }

    try
    {
        _ = Compile(matrixDocument);
    }
    catch (InvalidDataException)
    {
        matrixRejected++;
        continue;
    }
    throw new InvalidOperationException(
        "Unsupported schema-3 matrix state compiled: terrain=" + terrainOn +
        ", t88=" + t88On +
        ", t107=" + t107On +
        ", texture=" + mandatoryTextureId +
        ", position=" + position.PositionId + ".");
}
Require(
    matrixStates == 144 &&
    matrixSucceeded == 9 &&
    matrixRejected == 135,
    "The full schema-3 state matrix changed from 9 supported / 135 rejected.");

string center = Id65RemotePadGrassT107Catalog.CenterPositionId;
RejectDocument(
    "legacy schema-1 T66",
    Id65AuthoringEditorDocument.CreateExact(
        includeOptionalTerrain: true,
        placementIds:
        [Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId],
        mandatoryBaseFaceTextureId: 66));
RejectDocument(
    "legacy schema-2 T25",
    Id65AuthoringEditorDocument.CreateExact(
        includeOptionalTerrain: true,
        placementIds:
        [Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId],
        mandatoryBaseFaceTextureId: 25));

Id65AuthoringEditorDocument forgedDocument =
    Id65AuthoringEditorDocument.CreateRemotePadGridExact(
        center,
        includeOptionalTerrain: true,
        placementIds: [Id65RemotePadGrassT107Catalog.PlacementId],
        mandatoryBaseFaceTextureId: 66);
FieldInfo canonicalField = typeof(Id65AuthoringEditorDocument).GetField(
    "_canonicalUtf8",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException(
        "Missing canonical document field for forged-identity smoke.");
byte[] forgedCanonical =
    (byte[])(canonicalField.GetValue(forgedDocument)
        ?? throw new InvalidOperationException(
            "Missing canonical document bytes for forged-identity smoke."));
forgedCanonical[0] ^= 1;
RejectDocument("forged document canonical identity", forgedDocument);

Id65AuthoringEditorDocument validCenterDocument =
    Id65AuthoringEditorDocument.CreateRemotePadGridExact(
        center,
        includeOptionalTerrain: true,
        placementIds: [Id65RemotePadGrassT107Catalog.PlacementId],
        mandatoryBaseFaceTextureId: 66);
byte[] forgedRow = lockedSource.CopyRow80();
forgedRow[0x400] ^= 1;
Id65AuthoringCompositeLockedSource forgedSource = new(
    forgedRow,
    lockedSource.CopyTargetOverlay(),
    lockedSource.CopyGlobalExecutable());
Reject(
    "forged locked-source identity",
    () => Compile(validCenterDocument, source: forgedSource));

Id65V2NativeTextureWitness forgedWitness = new(
    "forged.texture-witness",
    textureWitness.DonorManifestId,
    textureWitness.DonorCompleteRecordSha256,
    textureWitness.SourceTexturePagesSha256,
    textureWitness.OutputTexturePagesSha256,
    textureWitness.SourceTextureComponentSha256,
    textureWitness.OutputTextureComponentSha256,
    textureWitness.SourceTextureCount,
    textureWitness.OutputTextureCount,
    textureWitness.PrivateTextureId,
    textureWitness.DonorWadEntry,
    textureWitness.DonorTextureId,
    textureWitness.MaterialTemplateTextureId,
    textureWitness.CopyPackedRows(),
    textureWitness.CopyPagePatches(),
    textureWitness.CanonicalSha256);
Reject(
    "forged T66 witness identity",
    () => Compile(validCenterDocument, texture: forgedWitness));

Id65MobyDependencyBundleDescriptor forgedBundle = new(
    "forged.grass-bundle",
    grassBundle.SchemaVersion,
    grassBundle.FoundationContractSha256,
    grassBundle.Rows,
    grassBundle.Properties,
    grassBundle.ActorPackages,
    grassBundle.ActorRoots,
    grassBundle.Fixups,
    grassBundle.ExternalDependencies,
    grassBundle.BehaviorClosure,
    grassBundle.RuntimeDependencies,
    grassBundle.CanonicalJson,
    grassBundle.CanonicalSha256);
Reject(
    "forged Grass bundle identity",
    () => Compile(validCenterDocument, bundle: forgedBundle));
Reject(
    "insufficient model capacity",
    () => Compile(
        validCenterDocument,
        limits: new Id65AuthoringCompositeCompilerLimits(
            ModelByteCapacity: 0x947FF)));

FileState lockedAfter = SnapshotFile(lockedImagePath);
IReadOnlyList<string> directoryAfter =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
Require(
    lockedBefore == lockedAfter &&
    directoryBefore.SequenceEqual(directoryAfter),
    "The in-memory schema-3 compiler smoke changed its locked fixture or directory.");

Console.WriteLine(
    "ID65_AUTHORING_REMOTE_PAD_COMPOSITE_COMPILER_SMOKE_OK " +
    "positions=9 " +
    "owned_ranges=7960 " +
    "grass_ranges=8 " +
    "schema=3 " +
    "terrain=on " +
    "texture=t66 " +
    "t107=on " +
    "t88=off " +
    "output_hashes=" + outputHashes.Count + " " +
    "deterministic_hashes=" + deterministicHashes.Count + " " +
    "matrix_states=" + matrixStates + " " +
    "matrix_supported=" + matrixSucceeded + " " +
    "matrix_rejected=" + matrixRejected + " " +
    "frozen_result_contract=true " +
    "filesystem_writes=false " +
    "aggregate_plan=false " +
    "materializer=false " +
    "app=false " +
    "create_bin=false " +
    "runtime=false " +
    "release=false");

Id65CompiledAuthoringRemotePadComposite Compile(
    Id65AuthoringEditorDocument document,
    Id65AuthoringCompositeLockedSource? source = null,
    Id65V2NativeTextureWitness? texture = null,
    Id65MobyDependencyBundleDescriptor? bundle = null,
    Id65AuthoringCompositeCompilerLimits? limits = null) =>
    Id65AuthoringRemotePadCompositeCompiler.Compile(
        new(
            document,
            source ?? lockedSource,
            texture ?? textureWitness,
            bundle ?? grassBundle,
            limits));

void RejectDocument(
    string label,
    Id65AuthoringEditorDocument document) =>
    Reject(label, () => Compile(document));

static void VerifyOrderedNonoverlapAndInverse(
    Id65CompiledAuthoringRemotePadComposite compiled)
{
    for (int index = 0; index < compiled.OwnedRanges.Count; index++)
    {
        Id65AuthoringCompositeOwnedRange forward =
            compiled.OwnedRanges[index];
        Id65AuthoringCompositeOwnedRange inverse =
            compiled.InverseRanges[compiled.InverseRanges.Count - 1 - index];
        Require(
            forward.DataRelativeOffset >= 0 &&
            forward.ByteLength > 0 &&
            forward.DataRelativeOffset + (long)forward.ByteLength <=
                Row80ByteLength &&
            (index == 0 ||
                compiled.OwnedRanges[index - 1].DataRelativeOffset +
                    compiled.OwnedRanges[index - 1].ByteLength <=
                        forward.DataRelativeOffset),
            "Owned ranges are out of bounds, unordered, or overlapping.");
        Require(
            inverse.StableId == forward.StableId &&
            inverse.OwnerId == forward.OwnerId &&
            inverse.DataRelativeOffset == forward.DataRelativeOffset &&
            inverse.ByteLength == forward.ByteLength &&
            inverse.BeforeSha256 == forward.AfterSha256 &&
            inverse.AfterSha256 == forward.BeforeSha256,
            "The inverse ownership range is not the exact reverse mapping.");
    }
}

static void VerifyEveryChangedByteIsOwned(
    byte[] source,
    byte[] output,
    IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
{
    Require(
        source.Length == Row80ByteLength &&
        output.Length == Row80ByteLength,
        "Changed-byte ownership input length changed.");
    int rangeIndex = 0;
    for (int offset = 0; offset < source.Length; offset++)
    {
        while (rangeIndex < ranges.Count &&
               ranges[rangeIndex].DataRelativeOffset +
                    ranges[rangeIndex].ByteLength <= offset)
        {
            rangeIndex++;
        }
        bool owned =
            rangeIndex < ranges.Count &&
            ranges[rangeIndex].DataRelativeOffset <= offset &&
            offset <
                ranges[rangeIndex].DataRelativeOffset +
                    ranges[rangeIndex].ByteLength;
        if (source[offset] != output[offset] && !owned)
        {
            throw new InvalidOperationException(
                "Changed byte 0x" + offset.ToString("X") +
                " escaped the ownership ledger.");
        }
    }
}

static bool EqualOutsideSpan(
    byte[] left,
    byte[] right,
    int offset,
    int length)
{
    if (left.Length != right.Length ||
        offset < 0 ||
        length < 0 ||
        offset + (long)length > left.Length)
    {
        return false;
    }
    return left.AsSpan(0, offset).SequenceEqual(right.AsSpan(0, offset)) &&
        left.AsSpan(offset + length).SequenceEqual(
            right.AsSpan(offset + length));
}

static void Reject(string label, Action action)
{
    try
    {
        action();
    }
    catch (Exception exception) when (exception is
        InvalidDataException or
        ArgumentException or
        KeyNotFoundException)
    {
        return;
    }
    throw new InvalidOperationException(
        "Expected fail-closed rejection: " + label + ".");
}

static string FindRepositoryRoot(string? explicitRoot)
{
    if (!string.IsNullOrWhiteSpace(explicitRoot))
    {
        string full = Path.GetFullPath(explicitRoot);
        if (Directory.Exists(Path.Combine(full, ".git")))
            return full;
        throw new DirectoryNotFoundException(
            "Explicit repository root is invalid: " + full);
    }

    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException(
        "Could not locate the repository root.");
}

static FileState SnapshotFile(string path)
{
    FileInfo file = new(path);
    return new(
        file.Length,
        file.LastWriteTimeUtc.Ticks,
        Hash(File.ReadAllBytes(path)));
}

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.EnumerateFileSystemEntries(path)
        .Select(Path.GetFileName)
        .Where(name => name is not null)
        .Select(name => name!)
        .Order(StringComparer.Ordinal)
        .ToArray();

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(
    long ByteLength,
    long LastWriteUtcTicks,
    string Sha256);
