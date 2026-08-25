using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Range(
    string Kind,
    int DataRelativeOffset,
    int ModelRelativeOffset,
    long WadOffset,
    int RawSectorLba,
    int RawSectorUserDataOffset,
    long BinPhysicalOffset,
    byte Before,
    byte After);

internal sealed record UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Proof(
    string SupersededV2ProfileId,
    string SupersededV2OutputDataSha256,
    string SupersededV2OutputModelSha256,
    string SupersededV2SectorSha256,
    int SectorIndex,
    int HeaderDataRelativeOffset,
    int HeaderModelRelativeOffset,
    long HeaderWadOffset,
    int OnlyAffectedRawSectorLba,
    int RawSectorUserDataOffset,
    long BinPhysicalOffset,
    byte SupersededHpVertexWordOffset,
    byte RepairedHpVertexWordOffset,
    int ChangedByteCount,
    int RepairRangeCount,
    string RepairDiffManifestSha256,
    IReadOnlyList<UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Range> RepairRanges,
    bool ExactSupersededV2OutputConsumed,
    bool OneByteAllowlistComplete,
    bool ByteInverseVerified,
    bool ReservedByteZeroVerified,
    bool HpTableOffsetDerivedFromLowDetailCounts,
    bool ExactSectorTableEndVerified,
    bool FailedV1Rejected,
    bool SupersededV2Rejected,
    bool HistoricalArtifactsMutationPossible,
    bool RequiresDuckStationRuntimeProof,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal sealed record UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan(
    UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan SupersededV2Plan,
    IReadOnlyList<UnusedLevel65RemoteBlankComponentProof> Components,
    UnusedLevel65RemoteBlankSceneProof Scene,
    UnusedLevel65RemoteBlankCollisionProof Collision,
    IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> StructuralPatches,
    IReadOnlyList<UnusedLevel65RemoteBlankDiffRange> DiffRanges,
    int ChangedDataByteCount,
    string DiffManifestSha256,
    string DeterministicPlanSha256,
    byte[] SourceData,
    byte[] FailedV1OutputData,
    byte[] SupersededV2OutputData,
    byte[] OutputData,
    string OutputDataSha256,
    string OutputModelSha256,
    UnusedLevel65RemoteBlankCollisionWindingRepairV2Proof Repair,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Proof EnvironmentHeaderRepair)
{
    public string ProfileId => UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ProfileId;
    public string SourceImagePath => SupersededV2Plan.SourceImagePath;
    public string SourceImageSha256 => SupersededV2Plan.SourceImageSha256;
    public string ExecutableSha256 => SupersededV2Plan.ExecutableSha256;
    public int WadLba => SupersededV2Plan.WadLba;
    public int WadByteLength => SupersededV2Plan.WadByteLength;
    public int TargetWadEntry => SupersededV2Plan.FailedV1Plan.TargetWadEntry;
    public long DataWadOffset => SupersededV2Plan.DataWadOffset;
    public int DataByteLength => SupersededV2Plan.DataByteLength;
    public string SourceDataSha256 => SupersededV2Plan.SourceDataSha256;
    public long ModelWadOffset => SupersededV2Plan.ModelWadOffset;
    public int ModelByteLength => SupersededV2Plan.ModelByteLength;
    public string SourceModelSha256 => SupersededV2Plan.SourceModelSha256;
    public int SourceUsedModelByteLength => SupersededV2Plan.SourceUsedModelByteLength;
    public int OutputUsedModelByteLength => SupersededV2Plan.OutputUsedModelByteLength;
    public int SourceZeroTailByteCount => SupersededV2Plan.SourceZeroTailByteCount;
    public int OutputZeroTailByteCount => SupersededV2Plan.OutputZeroTailByteCount;
    public UnusedLevel65RemoteBlankOcclusionProof Occlusion => SupersededV2Plan.Occlusion;
    public UnusedLevel65RemoteBlankSpawnProof Spawn => SupersededV2Plan.Spawn;
    public UnusedLevel65RemoteBlankIsolationProof Isolation => SupersededV2Plan.Isolation;
    public bool PatchPreimagesVerified => true;
    public bool PatchAllowlistComplete => true;
    public bool ByteInverseVerified => Repair.ByteInverseVerified && EnvironmentHeaderRepair.ByteInverseVerified;
    public bool ProtectedSubfilesPreserved =>
        SupersededV2Plan.ProtectedSubfilesPreserved && EnvironmentHeaderRepair.OneByteAllowlistComplete;
    public bool InheritedObjectRowsPreserved => SupersededV2Plan.InheritedObjectRowsPreserved;
    public bool RetailWadEntriesExcluded => SupersededV2Plan.RetailWadEntriesExcluded;
    public bool ExecutableExcluded => SupersededV2Plan.ExecutableExcluded;
    public bool SourceImageMutationPossible => false;
    public bool DisposableRuntimeCandidateAuthorized => false;
    public bool PromotionAuthorized => false;
    public bool NormalCreateBinEnabled => false;
}

/// <summary>
/// Static v3 discriminator layered over the exact historical v2 bytes. It
/// changes only sector 216 header byte +0x17 from the superseded value 0x09 to
/// the independently derived LP-table word count 0x08. Failed v1 and v2 remain
/// immutable forensic preimages and are never rewritten in place.
/// </summary>
internal static class UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
{
    public const string ProfileId =
        "unused-level-65-remote-blank-environment-header-repair-sector216-static-clean-usa-v3";
    public const string ExpectedSourceImageSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceImageSha256;
    public const string ExpectedSourceDataSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceDataSha256;
    public const string ExpectedSourceModelSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceModelSha256;
    public const string ExpectedSupersededV2OutputDataSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputDataSha256;
    public const string ExpectedSupersededV2OutputModelSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputModelSha256;
    public const string ExpectedSupersededV2SectorSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedNewSectorSha256;
    public const string ExpectedOutputDataSha256 =
        "a759fba359722aedde2e5af4559f8bfe94a83cdeb263c364ebab5a8af7b92dcb";
    public const string ExpectedOutputModelSha256 =
        "00c88deb64eed8d829b504422e1dd21b1d6b4449c75126bdb2c0821d077c17b1";
    public const string ExpectedOutputEnvironmentSha256 =
        "c4097bfdc54cf9a29864f5195e52c44151478d67e515fdb27f7c4c4f24d1ff06";
    public const string ExpectedNewSectorSha256 =
        "7d71ebc9558c10896b83d1d8a79e6224cd6a48630b7a335d25b46935a572759e";
    public const string ExpectedOutputTreeSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputTreeSha256;
    public const string ExpectedOutputBlocksSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputBlocksSha256;
    public const string ExpectedOutputCollisionComponentSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputCollisionComponentSha256;
    public const string ExpectedFailedV1OutputDataSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedFailedV1OutputDataSha256;
    public const string ExpectedFailedV1OutputModelSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedFailedV1OutputModelSha256;
    public const string ExpectedCollisionWindingRepairDiffManifestSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedRepairDiffManifestSha256;
    public const string ExpectedEnvironmentHeaderRepairDiffManifestSha256 =
        "fe85cd987e9d39c3ca10b944371c76fd7f7247e11db15e9eb6baed0d1925d875";
    public const string ExpectedDiffManifestSha256 =
        "c37a00572bd023b9457d2a24944a4864c639c8e160958af135ac4c4f2e0b6a78";
    public const string ExpectedDeterministicPlanSha256 =
        "ea78191299f6f59c41a3bd09858b9ad6bc18dc5c783baca6455f6c5de67ae9a4";
    public const int ExpectedChangedDataByteCount = 484_336;
    public const int ExpectedDiffRangeCount = 55_386;

    public const int WadLba = UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.WadLba;
    public const int WadByteLength = UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.WadByteLength;
    public const long DataWadOffset = UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.DataWadOffset;
    public const int DataByteLength = UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.DataByteLength;
    public const long ModelWadOffset = UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ModelWadOffset;
    public const int ModelByteLength = UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ModelByteLength;
    public const int ModelDataRelativeOffset =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ModelDataRelativeOffset;
    public const int TriangleDataRelativeOffset =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.TriangleDataRelativeOffset;
    public const int AuthoredRawX =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.AuthoredRawX;
    public const int AuthoredRawY =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.AuthoredRawY;
    public const int SectorIndex = 216;
    public const int HeaderModelRelativeOffset = 0x2B437;
    public const int HeaderDataRelativeOffset = 0x109C37;
    public const long HeaderWadOffset = 0x6A40437;
    public const int OnlyAffectedRawSectorLba = 54_437;
    public const int RawSectorUserDataOffset = 0x437;
    public const long BinPhysicalOffset = 0x7A1B03F;
    public const int RepairChangedByteCount = 1;
    public const byte SupersededHpVertexWordOffset = 0x09;
    public const byte RepairedHpVertexWordOffset = 0x08;
    private const int LogicalSectorByteLength = 2048;

    internal static UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan BuildStaticPlan(
        string sourceImagePath)
    {
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2 =
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.BuildStaticPlan(sourceImagePath);
        RequireExactSupersededV2(v2);

        if (HeaderDataRelativeOffset != ModelDataRelativeOffset + HeaderModelRelativeOffset ||
            HeaderWadOffset != DataWadOffset + HeaderDataRelativeOffset ||
            WadLba + checked((int)(HeaderWadOffset / LogicalSectorByteLength)) != OnlyAffectedRawSectorLba ||
            HeaderWadOffset % LogicalSectorByteLength != RawSectorUserDataOffset ||
            RawSectorUserDataOffset >= LogicalSectorByteLength ||
            BinPhysicalOffset != checked(
                ((long)OnlyAffectedRawSectorLba * 2352L) + 24L + RawSectorUserDataOffset))
        {
            throw new InvalidDataException("The exact environment-header logical/physical ownership moved.");
        }

        byte[] output = ApplyHeaderRepairTransactional(v2.OutputData, reverse: false);
        byte[] model = output.AsSpan(ModelDataRelativeOffset, ModelByteLength).ToArray();
        RequireHash(Hash(output), ExpectedOutputDataSha256, "v3 row-80 data");
        RequireHash(Hash(model), ExpectedOutputModelSha256, "v3 model");

        UnusedLevel65RemoteBlankSceneProof scene = v2.Scene;
        int environmentRelativeOffset = v2.Components.Single(component => component.Name == "environment")
            .OutputRelativeOffset;
        byte[] environment = model.AsSpan(environmentRelativeOffset, scene.OutputEnvironmentByteLength).ToArray();
        RequireHash(Hash(environment), ExpectedOutputEnvironmentSha256, "v3 environment");
        ValidateEveryEnvironmentSectorHeader(environment);
        int sectorRelativeOffset = checked(4 + ReadInt32(environment, 8 + (SectorIndex * 4)));
        byte[] sector = environment.AsSpan(sectorRelativeOffset, scene.NewSectorByteLength).ToArray();
        Id65EnvironmentSectorHeaderReadback header =
            Id65EnvironmentSectorHeaderContract.ReadExactSector(sector, "v3 remote sector 216");
        if (header.LowDetailVertexCount != 3 || header.LowDetailColorCount != 3 ||
            header.LowDetailFaceCount != 1 || header.HighDetailVertexCount != 3 ||
            header.HighDetailColorCount != 3 || header.HighDetailFaceCount != 1 ||
            header.HighDetailVertexWordOffset != RepairedHpVertexWordOffset)
        {
            throw new InvalidDataException("The repaired v3 remote sector failed the literal 3/3/1 -> 0x08 oracle.");
        }
        RequireHash(Hash(sector), ExpectedNewSectorSha256, "v3 remote sector 216");

        IReadOnlyList<UnusedLevel65RemoteBlankComponentProof> components = v2.Components
            .Select(component => component.Name == "environment"
                ? component with { OutputSha256 = ExpectedOutputEnvironmentSha256, ContentsPreserved = false }
                : component)
            .ToArray();
        scene = scene with
        {
            NewSectorHex = Convert.ToHexString(sector),
            NewSectorSha256 = ExpectedNewSectorSha256
        };
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> structuralPatches = v2.StructuralPatches
            .Select(patch => patch.Kind == "id65-model" ? patch with { After = model } : patch)
            .ToArray();
        ValidateStructuralPatches(structuralPatches);
        byte[] applied = ApplyTransactionalCore(v2.SourceData, structuralPatches, reverse: false);
        byte[] roundTrip = ApplyTransactionalCore(applied, structuralPatches, reverse: true);
        if (!applied.SequenceEqual(output) || !roundTrip.SequenceEqual(v2.SourceData))
            throw new InvalidDataException("The complete v3 plan is not byte-invertible to the locked base.");

        IReadOnlyList<UnusedLevel65RemoteBlankDiffRange> diffRanges =
            BuildDiffRanges(v2.SourceData, output, structuralPatches);
        int changedDataBytes = diffRanges.Sum(range => range.ByteLength);
        string diffManifest = HashDiffManifest(diffRanges);
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Range[] repairRanges = BuildRepairRanges();
        string repairManifest = HashRepairManifest(repairRanges);
        RequireDerivedPin(changedDataBytes, ExpectedChangedDataByteCount, "v3 changed-data byte count");
        RequireDerivedPin(diffRanges.Count, ExpectedDiffRangeCount, "v3 diff-range count");
        RequireDerivedPin(diffManifest, ExpectedDiffManifestSha256, "v3 complete diff manifest");
        RequireDerivedPin(
            repairManifest,
            ExpectedEnvironmentHeaderRepairDiffManifestSha256,
            "v3 repair diff manifest");

        string deterministicIdentity = HashDeterministicIdentity(
            v2,
            diffManifest,
            repairManifest);
        RequireDerivedPin(
            deterministicIdentity,
            ExpectedDeterministicPlanSha256,
            "v3 deterministic plan identity");

        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Proof proof = new(
            v2.ProfileId,
            v2.OutputDataSha256,
            v2.OutputModelSha256,
            v2.Scene.NewSectorSha256,
            SectorIndex,
            HeaderDataRelativeOffset,
            HeaderModelRelativeOffset,
            HeaderWadOffset,
            OnlyAffectedRawSectorLba,
            RawSectorUserDataOffset,
            BinPhysicalOffset,
            SupersededHpVertexWordOffset,
            RepairedHpVertexWordOffset,
            RepairChangedByteCount,
            repairRanges.Length,
            repairManifest,
            repairRanges,
            ExactSupersededV2OutputConsumed: true,
            OneByteAllowlistComplete: true,
            ByteInverseVerified: true,
            ReservedByteZeroVerified: true,
            HpTableOffsetDerivedFromLowDetailCounts: true,
            ExactSectorTableEndVerified: true,
            FailedV1Rejected: true,
            SupersededV2Rejected: true,
            HistoricalArtifactsMutationPossible: false,
            RequiresDuckStationRuntimeProof: true,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);

        return new(
            v2,
            components,
            scene,
            v2.Collision,
            structuralPatches,
            diffRanges,
            changedDataBytes,
            diffManifest,
            deterministicIdentity,
            v2.SourceData.ToArray(),
            v2.FailedV1OutputData.ToArray(),
            v2.OutputData.ToArray(),
            output,
            ExpectedOutputDataSha256,
            ExpectedOutputModelSha256,
            v2.Repair,
            proof);
    }

    internal static byte[] ApplyTransactional(
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan plan,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(input);
        byte[] output = ApplyTransactionalCore(input, plan.StructuralPatches, reverse);
        RequireHash(
            Hash(output),
            reverse ? ExpectedSourceDataSha256 : ExpectedOutputDataSha256,
            reverse ? "reversed locked row-80 data" : "applied v3 row-80 data");
        return output;
    }

    internal static byte[] ApplyHeaderRepairTransactional(byte[] input, bool reverse)
    {
        ArgumentNullException.ThrowIfNull(input);
        string expectedInputHash = reverse ? ExpectedOutputDataSha256 : ExpectedSupersededV2OutputDataSha256;
        RequireHash(Hash(input), expectedInputHash, reverse ? "v3 header afterimage" : "superseded-v2 header preimage");
        byte expected = reverse ? RepairedHpVertexWordOffset : SupersededHpVertexWordOffset;
        if (input.Length != DataByteLength || input[HeaderDataRelativeOffset] != expected)
            throw new InvalidDataException("The v3 environment-header repair lost its exact one-byte preimage.");
        byte[] output = input.ToArray();
        output[HeaderDataRelativeOffset] = reverse ? SupersededHpVertexWordOffset : RepairedHpVertexWordOffset;
        RequireHash(
            Hash(output),
            reverse ? ExpectedSupersededV2OutputDataSha256 : ExpectedOutputDataSha256,
            reverse ? "recovered superseded-v2 row-80 data" : "repaired v3 row-80 data");
        return output;
    }

    private static void RequireExactSupersededV2(
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan plan)
    {
        if (plan.ProfileId != UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ProfileId ||
            plan.SourceImageSha256 != ExpectedSourceImageSha256 ||
            plan.SourceDataSha256 != ExpectedSourceDataSha256 ||
            plan.OutputDataSha256 != ExpectedSupersededV2OutputDataSha256 ||
            plan.SourceModelSha256 != ExpectedSourceModelSha256 ||
            plan.OutputModelSha256 != ExpectedSupersededV2OutputModelSha256 ||
            plan.Scene.NewSectorSha256 != ExpectedSupersededV2SectorSha256 ||
            plan.OutputData[HeaderDataRelativeOffset] != SupersededHpVertexWordOffset ||
            !plan.ByteInverseVerified || plan.DisposableRuntimeCandidateAuthorized ||
            plan.PromotionAuthorized || plan.NormalCreateBinEnabled)
        {
            throw new InvalidDataException("The exact frozen superseded-v2 in-memory construction changed.");
        }
    }

    private static UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Range[] BuildRepairRanges() =>
    [
        new(
            "id65-sector216-hp-table-word-offset",
            HeaderDataRelativeOffset,
            HeaderModelRelativeOffset,
            HeaderWadOffset,
            OnlyAffectedRawSectorLba,
            RawSectorUserDataOffset,
            BinPhysicalOffset,
            SupersededHpVertexWordOffset,
            RepairedHpVertexWordOffset)
    ];

    private static byte[] ApplyTransactionalCore(
        byte[] input,
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> patches,
        bool reverse)
    {
        if (input.Length != DataByteLength)
            throw new InvalidDataException("The v3 transactional row-80 buffer has the wrong length.");
        ValidateStructuralPatches(patches);
        foreach (UnusedLevel65RemoteBlankStructuralPatch patch in patches)
        {
            byte[] expected = reverse ? patch.After : patch.Before;
            if (!input.AsSpan(patch.DataRelativeOffset, expected.Length).SequenceEqual(expected))
                throw new InvalidDataException($"The v3 {patch.Kind} transactional preimage changed.");
        }
        byte[] output = input.ToArray();
        foreach (UnusedLevel65RemoteBlankStructuralPatch patch in patches)
            (reverse ? patch.Before : patch.After).CopyTo(output, patch.DataRelativeOffset);
        return output;
    }

    private static void ValidateStructuralPatches(
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> patches)
    {
        if (patches.Count != 3 || patches.Any(patch =>
                patch.Before.Length != patch.After.Length ||
                patch.DataRelativeOffset < 0 ||
                patch.DataRelativeOffset + (long)patch.Before.Length > DataByteLength ||
                patch.WadOffset != DataWadOffset + patch.DataRelativeOffset))
        {
            throw new InvalidDataException("The v3 complete structural patch set is invalid.");
        }
        UnusedLevel65RemoteBlankStructuralPatch[] ordered =
            patches.OrderBy(patch => patch.DataRelativeOffset).ToArray();
        if (ordered[0].Kind != "id65-model" ||
            ordered[0].DataRelativeOffset != ModelDataRelativeOffset ||
            ordered[0].Before.Length != ModelByteLength ||
            ordered[1].Kind != "id65-landing-xy" ||
            ordered[2].Kind != "id65-t92-player-anchor-xy")
        {
            throw new InvalidDataException("The v3 complete model/landing/T92 allowlist changed.");
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
        IReadOnlyList<UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Range> ranges)
    {
        StringBuilder text = new();
        foreach (UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Range range in ranges)
        {
            text.Append(range.Kind).Append('|')
                .Append(range.DataRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.ModelRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.WadOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.RawSectorLba.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(range.RawSectorUserDataOffset.ToString("X3", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.BinPhysicalOffset.ToString("X", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.Before.ToString("X2", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.After.ToString("X2", CultureInfo.InvariantCulture)).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashDeterministicIdentity(
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2,
        string diffManifest,
        string repairManifest)
    {
        string identity = string.Join("\n",
        [
            ProfileId,
            v2.ProfileId,
            ExpectedSourceImageSha256,
            ExpectedSupersededV2OutputDataSha256,
            ExpectedOutputDataSha256,
            ExpectedSupersededV2OutputModelSha256,
            ExpectedOutputModelSha256,
            ExpectedSupersededV2SectorSha256,
            ExpectedNewSectorSha256,
            ExpectedOutputEnvironmentSha256,
            diffManifest,
            repairManifest,
            HeaderDataRelativeOffset.ToString("X8", CultureInfo.InvariantCulture),
            HeaderModelRelativeOffset.ToString("X8", CultureInfo.InvariantCulture),
            HeaderWadOffset.ToString("X8", CultureInfo.InvariantCulture),
            OnlyAffectedRawSectorLba.ToString(CultureInfo.InvariantCulture),
            RawSectorUserDataOffset.ToString("X3", CultureInfo.InvariantCulture),
            BinPhysicalOffset.ToString("X", CultureInfo.InvariantCulture),
            SupersededHpVertexWordOffset.ToString("X2", CultureInfo.InvariantCulture),
            RepairedHpVertexWordOffset.ToString("X2", CultureInfo.InvariantCulture)
        ]);
        return Hash(Encoding.UTF8.GetBytes(identity));
    }

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("The v3 environment integer read is out of range.");
        return System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static void ValidateEveryEnvironmentSectorHeader(byte[] environment)
    {
        if (ReadInt32(environment, 0) != environment.Length || ReadInt32(environment, 4) != 217)
            throw new InvalidDataException("The v3 environment length/count header changed.");
        const int sectorCount = 217;
        int pointerTableEnd = checked(8 + (sectorCount * 4));
        for (int sectorIndex = 0; sectorIndex < sectorCount; sectorIndex++)
        {
            int sectorOffset = checked(4 + ReadInt32(environment, 8 + (sectorIndex * 4)));
            int nextOffset = sectorIndex + 1 < sectorCount
                ? checked(4 + ReadInt32(environment, 8 + ((sectorIndex + 1) * 4)))
                : environment.Length;
            if (sectorOffset < pointerTableEnd || nextOffset <= sectorOffset || nextOffset > environment.Length)
                throw new InvalidDataException($"The v3 environment sector {sectorIndex} pointer span changed.");
            int pointerSpan = nextOffset - sectorOffset;
            _ = Id65EnvironmentSectorHeaderContract.ReadPointerSpan(
                environment.AsSpan(sectorOffset, pointerSpan),
                pointerSpan,
                requireExactEnd: false,
                $"v3 environment sector {sectorIndex}");
        }
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void RequireDerivedPin(int actual, int expected, string label)
    {
        if (actual != expected)
            throw new InvalidDataException($"The {label} is {actual}, expected {expected}.");
    }

    private static void RequireDerivedPin(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"The {label} is {actual}, expected {expected}.");
    }
}
