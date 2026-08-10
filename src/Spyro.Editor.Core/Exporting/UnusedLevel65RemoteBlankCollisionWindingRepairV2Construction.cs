using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65RemoteBlankCollisionWindingRepairV2Range(
    string Kind,
    int DataRelativeOffset,
    int ModelRelativeOffset,
    long WadOffset,
    int RawSectorLba,
    int RawSectorUserDataOffset,
    byte[] Before,
    byte[] After);

internal sealed record UnusedLevel65RemoteBlankCollisionWindingRepairV2Proof(
    string FailedV1ProfileId,
    string FailedV1OutputDataSha256,
    string FailedV1OutputModelSha256,
    int CollisionTriangleIndex,
    int TriangleDataRelativeOffset,
    int TriangleModelRelativeOffset,
    long TriangleWadOffset,
    int OnlyAffectedRawSectorLba,
    string FailedV1TriangleHex,
    string RepairedTriangleHex,
    IReadOnlyList<UnusedLevel65RemoteBlankPoint> FailedV1Points,
    IReadOnlyList<UnusedLevel65RemoteBlankPoint> RepairedPoints,
    long FailedV1NormalZ,
    long RepairedNormalZ,
    int ChangedByteCount,
    int RepairRangeCount,
    string RepairDiffManifestSha256,
    IReadOnlyList<UnusedLevel65RemoteBlankCollisionWindingRepairV2Range> RepairRanges,
    bool ExactFailedV1OutputConsumed,
    bool FourByteAllowlistComplete,
    bool ByteInverseVerified,
    bool PointSetIdentical,
    bool AcbOrderVerified,
    bool CollisionTreeIdentical,
    bool CollisionBlocksIdentical,
    bool CollisionAssignmentIdentical,
    bool OcclusionIdentical,
    bool SpawnIdentical,
    bool ProtectedDataIdentical,
    bool FailedV1ArtifactMutationPossible,
    bool RequiresDuckStationRuntimeProof,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal sealed record UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan(
    UnusedLevel65RemoteBlankStaticPlan FailedV1Plan,
    IReadOnlyList<UnusedLevel65RemoteBlankComponentProof> Components,
    UnusedLevel65RemoteBlankCollisionProof Collision,
    IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> StructuralPatches,
    IReadOnlyList<UnusedLevel65RemoteBlankDiffRange> DiffRanges,
    int ChangedDataByteCount,
    string DiffManifestSha256,
    string DeterministicPlanSha256,
    byte[] SourceData,
    byte[] FailedV1OutputData,
    byte[] OutputData,
    string OutputDataSha256,
    string OutputModelSha256,
    UnusedLevel65RemoteBlankCollisionWindingRepairV2Proof Repair)
{
    public string ProfileId => UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ProfileId;
    public string SourceImagePath => FailedV1Plan.SourceImagePath;
    public string SourceImageSha256 => FailedV1Plan.SourceImageSha256;
    public string ExecutableSha256 => FailedV1Plan.ExecutableSha256;
    public int WadLba => FailedV1Plan.WadLba;
    public int WadByteLength => FailedV1Plan.WadByteLength;
    public int TargetWadEntry => FailedV1Plan.TargetWadEntry;
    public long DataWadOffset => FailedV1Plan.DataWadOffset;
    public int DataByteLength => FailedV1Plan.DataByteLength;
    public string SourceDataSha256 => FailedV1Plan.SourceDataSha256;
    public long ModelWadOffset => FailedV1Plan.ModelWadOffset;
    public int ModelByteLength => FailedV1Plan.ModelByteLength;
    public string SourceModelSha256 => FailedV1Plan.SourceModelSha256;
    public int SourceUsedModelByteLength => FailedV1Plan.SourceUsedModelByteLength;
    public int OutputUsedModelByteLength => FailedV1Plan.OutputUsedModelByteLength;
    public int SourceZeroTailByteCount => FailedV1Plan.SourceZeroTailByteCount;
    public int OutputZeroTailByteCount => FailedV1Plan.OutputZeroTailByteCount;
    public UnusedLevel65RemoteBlankSceneProof Scene => FailedV1Plan.Scene;
    public UnusedLevel65RemoteBlankOcclusionProof Occlusion => FailedV1Plan.Occlusion;
    public UnusedLevel65RemoteBlankSpawnProof Spawn => FailedV1Plan.Spawn;
    public UnusedLevel65RemoteBlankIsolationProof Isolation => FailedV1Plan.Isolation;
    public bool PatchPreimagesVerified => true;
    public bool PatchAllowlistComplete => true;
    public bool ByteInverseVerified => Repair.ByteInverseVerified;
    public bool ProtectedSubfilesPreserved => FailedV1Plan.ProtectedSubfilesPreserved && Repair.ProtectedDataIdentical;
    public bool InheritedObjectRowsPreserved => FailedV1Plan.InheritedObjectRowsPreserved && Repair.ProtectedDataIdentical;
    public bool RetailWadEntriesExcluded => FailedV1Plan.RetailWadEntriesExcluded;
    public bool ExecutableExcluded => FailedV1Plan.ExecutableExcluded;
    public bool SourceImageMutationPossible => false;
    public bool DisposableRuntimeCandidateAuthorized => false;
    public bool PromotionAuthorized => false;
    public bool NormalCreateBinEnabled => false;
}

/// <summary>
/// Static v2 discriminator layered over the exact failed remote-blank v1
/// output. It changes only the two packed delta pairs needed to reorder the
/// T13995 collision vertices from A,B,C to A,C,B. The failed v1 plan and all
/// non-triangle construction state remain immutable and independently pinned.
/// </summary>
internal static class UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction
{
    public const string ProfileId =
        "unused-level-65-remote-blank-collision-winding-repair-sector216-static-clean-usa-v2";
    public const string ExpectedSourceImageSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceImageSha256;
    public const string ExpectedSourceDataSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceDataSha256;
    public const string ExpectedSourceModelSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceModelSha256;
    public const string ExpectedFailedV1OutputDataSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputDataSha256;
    public const string ExpectedFailedV1OutputModelSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputModelSha256;
    public const string ExpectedOutputDataSha256 =
        "a21e3ace8f16e981dc1d75c4d2210e96ba2833200bd4e3054b2a1c0bb540ade9";
    public const string ExpectedOutputModelSha256 =
        "2ea39a0c19f51f16abfd3cae73d13df01191e16d70f31644ae8c14c977581b79";
    public const string ExpectedOutputTreeSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputTreeSha256;
    public const string ExpectedOutputBlocksSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputBlocksSha256;
    public const string ExpectedOutputCollisionComponentSha256 =
        "5eedf23796ba8c9664b9e002cb46a6f5c9f9e37780ba5ab58d6d018ec1a147a2";
    public const string ExpectedNewSectorSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedNewSectorSha256;
    public const string ExpectedDiffManifestSha256 =
        "4e4f3c4433212d860963946c3bc8cdb6cfa931beb57f7c5e53d2720462b67f21";
    public const string ExpectedRepairDiffManifestSha256 =
        "0fae1b1035fa0bae3faf82e1b4d172a39c51d1cd5ce425a49321e3f01cf66e25";
    public const string ExpectedDeterministicPlanSha256 =
        "279bd11d25eefb5f97a981db489d40a0d7eaa938079b8f6fe4a80523fa64b00f";
    public const int ExpectedChangedDataByteCount = 484_336;
    public const int ExpectedDiffRangeCount = 55_386;

    public const int WadLba = UnusedLevel65RemoteBlankIsolationConstruction.WadLba;
    public const int WadByteLength = UnusedLevel65RemoteBlankIsolationConstruction.WadByteLength;
    public const long DataWadOffset = UnusedLevel65RemoteBlankIsolationConstruction.DataWadOffset;
    public const int DataByteLength = UnusedLevel65RemoteBlankIsolationConstruction.DataByteLength;
    public const long ModelWadOffset = UnusedLevel65RemoteBlankIsolationConstruction.ModelWadOffset;
    public const int ModelByteLength = UnusedLevel65RemoteBlankIsolationConstruction.ModelByteLength;
    public const int ModelDataRelativeOffset = UnusedLevel65RemoteBlankIsolationConstruction.ModelDataRelativeOffset;
    public const int AuthoredRawX = UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawX;
    public const int AuthoredRawY = UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawY;
    public const int ReusedCollisionTriangleIndex = 13_995;
    public const int TriangleDataRelativeOffset = 0x151A3C;
    public const int TriangleModelRelativeOffset = 0x7323C;
    public const long TriangleWadOffset = DataWadOffset + TriangleDataRelativeOffset;
    public const int OnlyAffectedRawSectorLba = 54_581;
    public const int RepairChangedByteCount = 4;
    public const long FailedV1NormalZ = 50_176;
    public const long RepairedNormalZ = -50_176;

    private const int LogicalSectorByteLength = 2048;
    private const int CollisionTreeInComponentOffset = 4 + 0x1C;
    private const int CollisionTreeByteLength = 0x6A60;
    private const int CollisionBlocksInComponentOffset = 4 + 0x6A7C;
    private const int CollisionBlocksByteLength = 0x17984;
    private const int CollisionAssignmentsInComponentOffset = 4 + 0x58480;
    private const int ObjectRecordByteLength = 0x58;
    private static readonly byte[] FailedV1Triangle =
        Convert.FromHexString("100138381001007000020000");
    private static readonly byte[] RepairedTriangle =
        Convert.FromHexString("10011C701001380000020000");
    private static readonly UnusedLevel65RemoteBlankPoint[] ExpectedFailedV1Points =
    [
        new(272, 272, 512),
        new(496, 272, 512),
        new(384, 496, 512)
    ];
    private static readonly UnusedLevel65RemoteBlankPoint[] ExpectedRepairedPoints =
    [
        new(272, 272, 512),
        new(384, 496, 512),
        new(496, 272, 512)
    ];

    public static UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan BuildStaticPlan(
        string sourceImagePath)
    {
        UnusedLevel65RemoteBlankStaticPlan failedV1 =
            UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(sourceImagePath);
        RequireExactFailedV1(failedV1);

        byte[] failedV1Output = failedV1.OutputData.ToArray();
        byte[] failedV1Model = failedV1Output.AsSpan(ModelDataRelativeOffset, ModelByteLength).ToArray();
        UnusedLevel65RemoteBlankComponentProof collisionComponent =
            failedV1.Components.Single(component => component.Name == "collision");
        int computedTriangleModelOffset = checked(
            collisionComponent.OutputRelativeOffset + 4 + 0x1E400 + (ReusedCollisionTriangleIndex * 12));
        int computedTriangleDataOffset = checked(ModelDataRelativeOffset + computedTriangleModelOffset);
        if (computedTriangleModelOffset != TriangleModelRelativeOffset ||
            computedTriangleDataOffset != TriangleDataRelativeOffset ||
            TriangleWadOffset != 0x6A8823C ||
            WadLba + checked((int)(TriangleWadOffset / LogicalSectorByteLength)) != OnlyAffectedRawSectorLba)
        {
            throw new InvalidDataException("The exact v1 T13995 physical/logical ownership moved.");
        }
        if (!failedV1Output.AsSpan(TriangleDataRelativeOffset, FailedV1Triangle.Length)
                .SequenceEqual(FailedV1Triangle))
        {
            throw new InvalidDataException("The exact failed-v1 T13995 A,B,C bytes changed.");
        }

        UnusedLevel65RemoteBlankPoint[] failedPoints = DecodeCollisionTriangle(FailedV1Triangle);
        UnusedLevel65RemoteBlankPoint[] repairedPoints = DecodeCollisionTriangle(RepairedTriangle);
        if (!failedPoints.SequenceEqual(ExpectedFailedV1Points) ||
            !repairedPoints.SequenceEqual(ExpectedRepairedPoints) ||
            !SamePointSet(failedPoints, repairedPoints) ||
            CollisionNormalZ(failedPoints) != FailedV1NormalZ ||
            CollisionNormalZ(repairedPoints) != RepairedNormalZ)
        {
            throw new InvalidDataException("The A,B,C to A,C,B winding discriminator failed decoded geometry proof.");
        }

        UnusedLevel65RemoteBlankCollisionWindingRepairV2Range[] repairRanges = BuildRepairRanges();
        byte[] repairedOutput = ApplyRepairTransactional(failedV1Output, repairRanges, reverse: false);
        byte[] repairedModel = repairedOutput.AsSpan(ModelDataRelativeOffset, ModelByteLength).ToArray();
        RequireHash(Hash(repairedOutput), ExpectedOutputDataSha256, "repaired row-80 data");
        RequireHash(Hash(repairedModel), ExpectedOutputModelSha256, "repaired model");
        if (CountChangedBytes(failedV1Output, repairedOutput) != RepairChangedByteCount ||
            !repairedOutput.AsSpan(TriangleDataRelativeOffset, RepairedTriangle.Length)
                .SequenceEqual(RepairedTriangle))
        {
            throw new InvalidDataException("The v2 repair escaped its exact four-byte T13995 allowlist.");
        }
        byte[] failedV1RoundTrip = ApplyRepairTransactional(repairedOutput, repairRanges, reverse: true);
        if (!failedV1RoundTrip.SequenceEqual(failedV1Output))
            throw new InvalidDataException("The v2 repair is not byte-invertible to exact failed v1 output.");

        VerifyUnchangedConstructionState(failedV1, failedV1Model, repairedModel, collisionComponent);
        IReadOnlyList<UnusedLevel65RemoteBlankComponentProof> components = failedV1.Components
            .Select(component => component.Name == "collision"
                ? component with { OutputSha256 = ExpectedOutputCollisionComponentSha256 }
                : component)
            .ToArray();
        UnusedLevel65RemoteBlankCollisionProof collision = failedV1.Collision with
        {
            OutputTriangleHex = Convert.ToHexString(RepairedTriangle),
            UpwardWinding = false
        };
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> structuralPatches =
            failedV1.StructuralPatches.Select(patch => patch.Kind == "id65-model"
                ? patch with { After = repairedModel }
                : patch).ToArray();
        ValidateStructuralPatches(structuralPatches);
        byte[] composed = ApplyTransactionalCore(
            failedV1.SourceData,
            structuralPatches,
            reverse: false);
        byte[] sourceRoundTrip = ApplyTransactionalCore(
            composed,
            structuralPatches,
            reverse: true);
        if (!composed.SequenceEqual(repairedOutput) || !sourceRoundTrip.SequenceEqual(failedV1.SourceData))
            throw new InvalidDataException("The complete v2 plan is not byte-invertible to the locked base.");

        IReadOnlyList<UnusedLevel65RemoteBlankDiffRange> diffRanges =
            BuildDiffRanges(failedV1.SourceData, repairedOutput, structuralPatches);
        int changedDataBytes = diffRanges.Sum(range => range.ByteLength);
        string diffManifest = HashDiffManifest(diffRanges);
        string repairManifest = HashRepairManifest(repairRanges);
        if (changedDataBytes != ExpectedChangedDataByteCount ||
            diffRanges.Count != ExpectedDiffRangeCount ||
            diffManifest != ExpectedDiffManifestSha256 ||
            repairManifest != ExpectedRepairDiffManifestSha256)
        {
            throw new InvalidDataException("The complete or repair-only v2 diff boundary changed.");
        }

        string deterministicIdentity = HashDeterministicIdentity(
            failedV1,
            diffManifest,
            repairManifest);
        RequireHash(
            deterministicIdentity,
            ExpectedDeterministicPlanSha256,
            "v2 deterministic plan identity");
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Proof repair = new(
            failedV1.ProfileId,
            failedV1.OutputDataSha256,
            failedV1.OutputModelSha256,
            ReusedCollisionTriangleIndex,
            TriangleDataRelativeOffset,
            TriangleModelRelativeOffset,
            TriangleWadOffset,
            OnlyAffectedRawSectorLba,
            Convert.ToHexString(FailedV1Triangle),
            Convert.ToHexString(RepairedTriangle),
            failedPoints,
            repairedPoints,
            FailedV1NormalZ,
            RepairedNormalZ,
            RepairChangedByteCount,
            repairRanges.Length,
            repairManifest,
            repairRanges,
            ExactFailedV1OutputConsumed: true,
            FourByteAllowlistComplete: true,
            ByteInverseVerified: true,
            PointSetIdentical: true,
            AcbOrderVerified: true,
            CollisionTreeIdentical: true,
            CollisionBlocksIdentical: true,
            CollisionAssignmentIdentical: true,
            OcclusionIdentical: true,
            SpawnIdentical: true,
            ProtectedDataIdentical: true,
            FailedV1ArtifactMutationPossible: false,
            RequiresDuckStationRuntimeProof: true,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);

        return new(
            failedV1,
            components,
            collision,
            structuralPatches,
            diffRanges,
            changedDataBytes,
            diffManifest,
            deterministicIdentity,
            failedV1.SourceData.ToArray(),
            failedV1Output,
            repairedOutput,
            ExpectedOutputDataSha256,
            ExpectedOutputModelSha256,
            repair);
    }

    internal static byte[] ApplyTransactional(
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan plan,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(input);
        byte[] output = ApplyTransactionalCore(input, plan.StructuralPatches, reverse);
        RequireHash(
            Hash(output),
            reverse ? ExpectedSourceDataSha256 : ExpectedOutputDataSha256,
            reverse ? "reversed locked row-80 data" : "applied repaired row-80 data");
        return output;
    }

    internal static byte[] ApplyRepairTransactional(
        byte[] input,
        IReadOnlyList<UnusedLevel65RemoteBlankCollisionWindingRepairV2Range> ranges,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(ranges);
        ValidateRepairRanges(ranges);
        RequireHash(
            Hash(input),
            reverse ? ExpectedOutputDataSha256 : ExpectedFailedV1OutputDataSha256,
            reverse ? "v2 repair afterimage" : "failed-v1 repair preimage");
        foreach (UnusedLevel65RemoteBlankCollisionWindingRepairV2Range range in ranges)
        {
            byte[] expected = reverse ? range.After : range.Before;
            if (!input.AsSpan(range.DataRelativeOffset, expected.Length).SequenceEqual(expected))
                throw new InvalidDataException($"Repair range {range.Kind} lost its exact preimage.");
        }
        byte[] output = input.ToArray();
        foreach (UnusedLevel65RemoteBlankCollisionWindingRepairV2Range range in ranges)
            (reverse ? range.Before : range.After).CopyTo(output, range.DataRelativeOffset);
        RequireHash(
            Hash(output),
            reverse ? ExpectedFailedV1OutputDataSha256 : ExpectedOutputDataSha256,
            reverse ? "recovered failed-v1 output" : "repaired v2 output");
        return output;
    }

    private static void RequireExactFailedV1(UnusedLevel65RemoteBlankStaticPlan plan)
    {
        if (plan.ProfileId != UnusedLevel65RemoteBlankIsolationConstruction.ProfileId ||
            plan.SourceImageSha256 != ExpectedSourceImageSha256 ||
            plan.SourceDataSha256 != ExpectedSourceDataSha256 ||
            plan.OutputDataSha256 != ExpectedFailedV1OutputDataSha256 ||
            plan.SourceModelSha256 != ExpectedSourceModelSha256 ||
            plan.OutputModelSha256 != ExpectedFailedV1OutputModelSha256 ||
            plan.Collision.OutputTriangleHex != Convert.ToHexString(FailedV1Triangle) ||
            !plan.Collision.UpwardWinding ||
            !plan.PatchPreimagesVerified ||
            !plan.PatchAllowlistComplete ||
            !plan.ByteInverseVerified ||
            !plan.ProtectedSubfilesPreserved ||
            !plan.InheritedObjectRowsPreserved ||
            !plan.RetailWadEntriesExcluded ||
            !plan.ExecutableExcluded ||
            plan.SourceImageMutationPossible ||
            plan.DisposableRuntimeCandidateAuthorized ||
            plan.PromotionAuthorized ||
            plan.NormalCreateBinEnabled)
        {
            throw new InvalidDataException("The exact frozen failed-v1 in-memory construction changed.");
        }
    }

    private static UnusedLevel65RemoteBlankCollisionWindingRepairV2Range[] BuildRepairRanges() =>
    [
        new(
            "id65-t13995-winding-acb-byte-pair-1",
            TriangleDataRelativeOffset + 2,
            TriangleModelRelativeOffset + 2,
            TriangleWadOffset + 2,
            OnlyAffectedRawSectorLba,
            checked((int)((TriangleWadOffset + 2) % LogicalSectorByteLength)),
            FailedV1Triangle.AsSpan(2, 2).ToArray(),
            RepairedTriangle.AsSpan(2, 2).ToArray()),
        new(
            "id65-t13995-winding-acb-byte-pair-2",
            TriangleDataRelativeOffset + 6,
            TriangleModelRelativeOffset + 6,
            TriangleWadOffset + 6,
            OnlyAffectedRawSectorLba,
            checked((int)((TriangleWadOffset + 6) % LogicalSectorByteLength)),
            FailedV1Triangle.AsSpan(6, 2).ToArray(),
            RepairedTriangle.AsSpan(6, 2).ToArray())
    ];

    private static void VerifyUnchangedConstructionState(
        UnusedLevel65RemoteBlankStaticPlan failedV1,
        byte[] failedV1Model,
        byte[] repairedModel,
        UnusedLevel65RemoteBlankComponentProof collisionComponent)
    {
        int collisionOffset = collisionComponent.OutputRelativeOffset;
        RequireHash(
            Hash(repairedModel.AsSpan(
                collisionOffset + CollisionTreeInComponentOffset,
                CollisionTreeByteLength)),
            ExpectedOutputTreeSha256,
            "repaired collision tree");
        RequireHash(
            Hash(repairedModel.AsSpan(
                collisionOffset + CollisionBlocksInComponentOffset,
                CollisionBlocksByteLength)),
            ExpectedOutputBlocksSha256,
            "repaired collision blocks");
        RequireHash(
            Hash(repairedModel.AsSpan(collisionOffset, collisionComponent.ByteLength)),
            ExpectedOutputCollisionComponentSha256,
            "repaired collision component");
        int assignmentOffset = checked(
            collisionOffset + CollisionAssignmentsInComponentOffset + ReusedCollisionTriangleIndex);
        if (failedV1Model[assignmentOffset] != 0 || repairedModel[assignmentOffset] != 0)
            throw new InvalidDataException("The v2 discriminator changed T13995 assignment zero.");
        if (CountChangedBytes(failedV1Model, repairedModel) != RepairChangedByteCount)
            throw new InvalidDataException("The repaired model changed bytes outside T13995 winding.");
        foreach (UnusedLevel65RemoteBlankComponentProof component in failedV1.Components)
        {
            ReadOnlySpan<byte> before = failedV1Model.AsSpan(component.OutputRelativeOffset, component.ByteLength);
            ReadOnlySpan<byte> after = repairedModel.AsSpan(component.OutputRelativeOffset, component.ByteLength);
            if (component.Name == "collision")
            {
                if (CountChangedBytes(before, after) != RepairChangedByteCount)
                    throw new InvalidDataException("The collision component lost its four-byte repair boundary.");
            }
            else if (!before.SequenceEqual(after))
            {
                throw new InvalidDataException($"The v2 repair changed the inherited {component.Name} component.");
            }
        }
        if (!failedV1.Spawn.AtomicXyOnly ||
            !failedV1.Scene.AllInheritedSectorPayloadsPreserved ||
            !failedV1.Scene.ExactHpLpPairing ||
            !failedV1.Occlusion.GroupZeroInheritedOrderPreserved ||
            !failedV1.Occlusion.OtherGroupsPreserved ||
            !failedV1.Collision.NativeCellSequencesPreserved ||
            !failedV1.Collision.NativeOrderingPreserved)
        {
            throw new InvalidDataException("The failed-v1 preserved construction proof weakened.");
        }
        byte[] repairedPlayer = repairedModel.Length > 0
            ? failedV1.OutputData.AsSpan(
                UnusedLevel65RemoteBlankIsolationConstruction.PlayerAnchorDataRelativeOffset,
                ObjectRecordByteLength).ToArray()
            : throw new InvalidDataException("The repaired model is unexpectedly empty.");
        if (Hash(repairedPlayer) != UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputPlayerAnchorSha256)
            throw new InvalidDataException("The exact v1 spawn anchor identity changed before repair.");
    }

    private static byte[] ApplyTransactionalCore(
        byte[] input,
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> patches,
        bool reverse)
    {
        if (input.Length != DataByteLength)
            throw new InvalidDataException("The v2 transactional row-80 buffer has the wrong length.");
        ValidateStructuralPatches(patches);
        foreach (UnusedLevel65RemoteBlankStructuralPatch patch in patches)
        {
            byte[] expected = reverse ? patch.After : patch.Before;
            if (!input.AsSpan(patch.DataRelativeOffset, expected.Length).SequenceEqual(expected))
                throw new InvalidDataException($"The v2 {patch.Kind} transactional preimage changed.");
        }
        byte[] output = input.ToArray();
        foreach (UnusedLevel65RemoteBlankStructuralPatch patch in patches)
            (reverse ? patch.Before : patch.After).CopyTo(output, patch.DataRelativeOffset);
        return output;
    }

    private static void ValidateStructuralPatches(
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> patches)
    {
        if (patches.Count != 3 ||
            patches.Any(patch => patch.Before.Length != patch.After.Length ||
                patch.DataRelativeOffset < 0 ||
                patch.DataRelativeOffset + (long)patch.Before.Length > DataByteLength ||
                patch.WadOffset != DataWadOffset + patch.DataRelativeOffset))
        {
            throw new InvalidDataException("The v2 complete structural patch set is invalid.");
        }
        UnusedLevel65RemoteBlankStructuralPatch[] ordered =
            patches.OrderBy(patch => patch.DataRelativeOffset).ToArray();
        if (ordered[0].Kind != "id65-model" ||
            ordered[0].DataRelativeOffset != ModelDataRelativeOffset ||
            ordered[0].Before.Length != ModelByteLength ||
            ordered[1].Kind != "id65-landing-xy" ||
            ordered[2].Kind != "id65-t92-player-anchor-xy")
        {
            throw new InvalidDataException("The v2 complete model/landing/T92 allowlist changed.");
        }
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].DataRelativeOffset + ordered[index - 1].Before.Length >
                ordered[index].DataRelativeOffset)
            {
                throw new InvalidDataException("The v2 complete structural patches overlap.");
            }
        }
    }

    private static void ValidateRepairRanges(
        IReadOnlyList<UnusedLevel65RemoteBlankCollisionWindingRepairV2Range> ranges)
    {
        if (ranges.Count != 2 || ranges.Sum(range => range.Before.Length) != RepairChangedByteCount ||
            ranges.Any(range => range.Before.Length != 2 || range.After.Length != 2 ||
                range.WadOffset != DataWadOffset + range.DataRelativeOffset ||
                range.ModelRelativeOffset != range.DataRelativeOffset - ModelDataRelativeOffset ||
                range.RawSectorLba != OnlyAffectedRawSectorLba ||
                range.RawSectorUserDataOffset != range.WadOffset % LogicalSectorByteLength))
        {
            throw new InvalidDataException("The exact two-range/four-byte v2 repair allowlist changed.");
        }
        if (ranges[0].DataRelativeOffset != TriangleDataRelativeOffset + 2 ||
            ranges[1].DataRelativeOffset != TriangleDataRelativeOffset + 6)
        {
            throw new InvalidDataException("The exact packed T13995 delta-byte offsets changed.");
        }
    }

    private static IReadOnlyList<UnusedLevel65RemoteBlankDiffRange> BuildDiffRanges(
        byte[] before,
        byte[] after,
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> patches)
    {
        List<UnusedLevel65RemoteBlankDiffRange> ranges = [];
        int index = 0;
        while (index < before.Length)
        {
            if (before[index] == after[index])
            {
                index++;
                continue;
            }
            int start = index;
            while (index < before.Length && before[index] != after[index])
                index++;
            int length = index - start;
            UnusedLevel65RemoteBlankStructuralPatch patch = patches.Single(item =>
                start >= item.DataRelativeOffset &&
                start + length <= item.DataRelativeOffset + item.Before.Length);
            ranges.Add(new(
                patch.Kind,
                start,
                DataWadOffset + start,
                length,
                Hash(before.AsSpan(start, length)),
                Hash(after.AsSpan(start, length))));
        }
        return ranges;
    }

    private static string HashDiffManifest(IReadOnlyList<UnusedLevel65RemoteBlankDiffRange> ranges)
    {
        StringBuilder text = new();
        foreach (UnusedLevel65RemoteBlankDiffRange range in ranges)
        {
            text.Append(range.Kind).Append('|')
                .Append(range.DataRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.WadOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.ByteLength.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(range.BeforeSha256).Append('|').Append(range.AfterSha256).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashRepairManifest(
        IReadOnlyList<UnusedLevel65RemoteBlankCollisionWindingRepairV2Range> ranges)
    {
        StringBuilder text = new();
        foreach (UnusedLevel65RemoteBlankCollisionWindingRepairV2Range range in ranges)
        {
            text.Append(range.Kind).Append('|')
                .Append(range.DataRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.WadOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.Before.Length.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(Hash(range.Before)).Append('|').Append(Hash(range.After)).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashDeterministicIdentity(
        UnusedLevel65RemoteBlankStaticPlan failedV1,
        string diffManifest,
        string repairManifest)
    {
        string identity = string.Join('\n',
        [
            ProfileId,
            failedV1.ProfileId,
            ExpectedSourceImageSha256,
            ExpectedFailedV1OutputDataSha256,
            ExpectedOutputDataSha256,
            ExpectedFailedV1OutputModelSha256,
            ExpectedOutputModelSha256,
            diffManifest,
            repairManifest,
            Convert.ToHexString(FailedV1Triangle),
            Convert.ToHexString(RepairedTriangle),
            FailedV1NormalZ.ToString(CultureInfo.InvariantCulture),
            RepairedNormalZ.ToString(CultureInfo.InvariantCulture),
            TriangleDataRelativeOffset.ToString("X8", CultureInfo.InvariantCulture),
            TriangleModelRelativeOffset.ToString("X8", CultureInfo.InvariantCulture),
            OnlyAffectedRawSectorLba.ToString(CultureInfo.InvariantCulture),
            ExpectedOutputTreeSha256,
            ExpectedOutputBlocksSha256,
            ExpectedNewSectorSha256,
            UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputPlayerAnchorSha256
        ]);
        return Hash(Encoding.UTF8.GetBytes(identity));
    }

    private static UnusedLevel65RemoteBlankPoint[] DecodeCollisionTriangle(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 12)
            throw new InvalidDataException("A collision triangle must be exactly 12 bytes.");
        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes[..4]);
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        UnusedLevel65RemoteBlankPoint first = new(
            (int)(xWord & 0x3FFF),
            (int)(yWord & 0x3FFF),
            (int)(zWord & 0x3FFF));
        return
        [
            first,
            new(
                first.X + Signed9((int)((xWord >> 14) & 0x1FF)),
                first.Y + Signed9((int)((yWord >> 14) & 0x1FF)),
                first.Z + (int)((zWord >> 16) & 0xFF)),
            new(
                first.X + Signed9((int)((xWord >> 23) & 0x1FF)),
                first.Y + Signed9((int)((yWord >> 23) & 0x1FF)),
                first.Z + (int)((zWord >> 24) & 0xFF))
        ];
    }

    private static int Signed9(int value) => (value & 0x100) == 0 ? value : value - 0x200;

    private static long CollisionNormalZ(IReadOnlyList<UnusedLevel65RemoteBlankPoint> points) =>
        ((long)points[1].X - points[0].X) * ((long)points[2].Y - points[0].Y) -
        ((long)points[1].Y - points[0].Y) * ((long)points[2].X - points[0].X);

    private static bool SamePointSet(
        IReadOnlyList<UnusedLevel65RemoteBlankPoint> first,
        IReadOnlyList<UnusedLevel65RemoteBlankPoint> second) =>
        first.OrderBy(point => point.X).ThenBy(point => point.Y).ThenBy(point => point.Z)
            .SequenceEqual(second.OrderBy(point => point.X).ThenBy(point => point.Y).ThenBy(point => point.Z));

    private static int CountChangedBytes(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("Changed-byte comparison requires equal lengths.");
        int changed = 0;
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index])
                changed++;
        }
        return changed;
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}, expected {expected}.");
    }
}
