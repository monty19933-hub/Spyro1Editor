using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// One immutable, statically verified placement choice for the resident Town
/// Square T21 Red Gem. Coordinates are exposed in editor and raw on-disc units.
/// </summary>
public sealed class Id65RemotePadRedGemT21Position
{
    internal Id65RemotePadRedGemT21Position(
        int ordinal,
        string positionId,
        string label,
        int editorX,
        int editorY,
        int editorZ,
        int rawX,
        int rawY,
        int rawZ,
        string outputRowSha256,
        bool isDefault)
    {
        Ordinal = ordinal;
        PositionId = positionId;
        Label = label;
        EditorX = editorX;
        EditorY = editorY;
        EditorZ = editorZ;
        RawX = rawX;
        RawY = rawY;
        RawZ = rawZ;
        OutputRowSha256 = outputRowSha256;
        IsDefault = isDefault;
    }

    public int Ordinal { get; }
    public string PositionId { get; }
    public string Label { get; }
    public int EditorX { get; }
    public int EditorY { get; }
    public int EditorZ { get; }
    public int RawX { get; }
    public int RawY { get; }
    public int RawZ { get; }
    public string OutputRowSha256 { get; }
    public bool IsDefault { get; }

    public override string ToString() => Label;
}

/// <summary>
/// Pure, fail-closed catalog for a prospective fresh-ID65 T21 Red Gem placement.
/// The exact Town Square/ID65-resident row is reused in place and only its XYZ
/// bytes may change. Actor class 0x0053 is global/overlay resident and rootless.
/// This catalog does not authorize a candidate, runtime acceptance, Create BIN,
/// materialization, or release publication.
/// </summary>
public static class Id65RemotePadRedGemT21Catalog
{
    public const string ProfileId = "id65-authoring-t21-red-gem-remote-pad-grid-v1";
    public const int SchemaVersion = 1;

    public const string PlacementId = "placement.remote-pad.red-gem.t21";
    public const string AtomicGroupId =
        "atomic.id65-remote-pad-red-gem-t21-one-at-a-time";
    public const string BundleId =
        "bundle.townsquare-red-gem.actor-0053-resident-rootless";
    public const string RowTemplateId = "row.townsquare.red-gem.t21";

    public const string SourceLevelKey = "townsquare";
    public const string SourceLevelName = "Town Square";
    public const string MobyTypeName = "Red Gem";
    public const int TreasureValue = 1;
    public const string RewardOwnerSemantics = "stationary-loose-gem";
    public const int SourceTrueIndex = 21;
    public const int TargetTrueIndex = 21;
    public const int SourceObjectCount = 107;
    public const int RowByteLength = 0x58;
    public const int RowCoordinateOffset = 0x0C;
    public const int RowCoordinateByteLength = 0x0C;
    public const int ActorClassOffset = 0x36;
    public const ushort ActorClass = 0x0053;

    public const string RetailSourceImageSha256 =
        "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    public const int RetailSourceDataWadEntry = 16;
    public const long RetailSourceDataWadOffset = 0x119E000;
    public const long RetailSourceObjectTableWadOffset = 0x136E170;
    public const long RetailSourceRowWadOffset = 0x136E8A8;

    public const string LockedId65BaseImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string LockedId65DataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    public const int LockedId65DataWadEntry = 80;
    public const long LockedId65DataWadOffset = 0x6936800;
    public const long LockedId65ObjectTableWadOffset = 0x6B06970;
    public const long LockedId65ResidentRowWadOffset = 0x6B070A8;

    public const string SourceRowSha256 =
        "004efc909b3973a370d141e3f7200d8ea188226f52f2cf1bdbb22ba40c45e0bc";
    public const string SourceRowExactHex =
        "6055000000000000000000005CE801005CC1010000200000000000000000008000000000000000000000000000000000000000000000530000007D0000000000002000FF000000040000FF0000000001180040FF7F100000";
    public const int SourceRawX = 125_020;
    public const int SourceRawY = 115_036;
    public const int SourceRawZ = 8_192;

    public const string RowAllocationSemantics = "reuse-resident-exact";
    public const string ActorAllocationSemantics = "reuse-resident-exact";
    public const string ActorDependencySemantics = "resident-rootless";
    public const string MutableFieldSemantics = "xyz-only";
    public const bool RequiresActorPackageInstall = false;
    public const bool RequiresActorRoot = false;
    public const bool RootlessResidentActor = true;

    public const string SupportHandle = "collision.remote-pad.t13995";
    public const int SupportCollisionTrueIndex = 13_995;
    public const string SupportCollisionPackedHex = "10011C701001380000020000";
    public const int SupportCollisionNormal = -50_176;

    public const string NorthWestPositionId = "remote-pad-grid.x368-y336";
    public const string NorthPositionId = "remote-pad-grid.x384-y336";
    public const string NorthEastPositionId = "remote-pad-grid.x400-y336";
    public const string WestPositionId = "remote-pad-grid.x368-y352";
    public const string CenterPositionId = "remote-pad-grid.x384-y352";
    public const string EastPositionId = "remote-pad-grid.x400-y352";
    public const string SouthWestPositionId = "remote-pad-grid.x368-y368";
    public const string SouthPositionId = "remote-pad-grid.x384-y368";
    public const string SouthEastPositionId = "remote-pad-grid.x400-y368";
    public const string DefaultPositionId = CenterPositionId;

    public const string SelectionSemantics = "one-at-a-time";
    public const int MaximumSimultaneousPlacementCount = 1;
    public const bool AllowsMultipleSimultaneousPlacements = false;

    public const bool RuntimeAcceptancePassed = false;
    public const bool RuntimeCandidateAuthorized = false;
    public const bool NormalCreateBinAuthorized = false;
    public const bool ReleasePublicationAuthorized = false;

    public const int SupportVertexAEditorX = 272;
    public const int SupportVertexAEditorY = 272;
    public const int SupportVertexAEditorZ = 512;
    public const int SupportVertexCEditorX = 384;
    public const int SupportVertexCEditorY = 496;
    public const int SupportVertexCEditorZ = 512;
    public const int SupportVertexBEditorX = 496;
    public const int SupportVertexBEditorY = 272;
    public const int SupportVertexBEditorZ = 512;

    public const string ExpectedCanonicalSha256 =
        "af1eed3bd76c353ef444e6062256fbbff70203fdc6214d33f9d0d1853f498995";

    private const int RawUnitsPerEditorUnit = 16;

    private static readonly byte[] SourceRowBytes = Convert.FromHexString(SourceRowExactHex);
    private static readonly Id65RemotePadRedGemT21Position[] PositionArray =
    [
        new(0, NorthWestPositionId, "Top left", 368, 336, 512, 5_888, 5_376, 8_192,
            "85a73516d6e1291480883c09c81f74f06d8713a81df75428c22f2ba8a2d981bf", false),
        new(1, NorthPositionId, "Top center", 384, 336, 512, 6_144, 5_376, 8_192,
            "89037427732f22f211a6e104e8bf755c056ed01cb8d5064bf500a8cfee3908b6", false),
        new(2, NorthEastPositionId, "Top right", 400, 336, 512, 6_400, 5_376, 8_192,
            "8ec0b330cffdbc7ed968e780732f0a679974e487ab951e1bbdb8cf8c6ca03c86", false),
        new(3, WestPositionId, "Middle left", 368, 352, 512, 5_888, 5_632, 8_192,
            "ea415c39d3aa59f09a0c0f7c7d1b3f453ed4fc9918964ae62173f0b5216c4d77", false),
        new(4, CenterPositionId, "Center", 384, 352, 512, 6_144, 5_632, 8_192,
            "33588f555826c076076b0f0a0b7b1e489804009cc51d420bc1b9615c7aebfc61", true),
        new(5, EastPositionId, "Middle right", 400, 352, 512, 6_400, 5_632, 8_192,
            "e1f9fb20e309dccefe0b3bf8bc401fd9c5e3bf4f3f916947f73a412c7ee5cf6e", false),
        new(6, SouthWestPositionId, "Bottom left", 368, 368, 512, 5_888, 5_888, 8_192,
            "567baab06e6983fbdf370ab7032a3b744287c0ff80ec146e6817506c87796518", false),
        new(7, SouthPositionId, "Bottom center", 384, 368, 512, 6_144, 5_888, 8_192,
            "7900bca9dcc4e8b4e287dba429a74d633ec4d65596d130413e9b9fe74da90246", false),
        new(8, SouthEastPositionId, "Bottom right", 400, 368, 512, 6_400, 5_888, 8_192,
            "2cad5a9cbb22c3da10262773c24241e53bbd2e08c60ad42a007dffb1d138c1e7", false)
    ];

    private static readonly ReadOnlyCollection<Id65RemotePadRedGemT21Position> PositionView =
        Array.AsReadOnly(PositionArray);
    private static readonly ReadOnlyDictionary<string, Id65RemotePadRedGemT21Position> PositionById =
        new(PositionArray.ToDictionary(item => item.PositionId, StringComparer.Ordinal));

    public static IReadOnlyList<Id65RemotePadRedGemT21Position> Positions => PositionView;
    public static Id65RemotePadRedGemT21Position DefaultPosition =>
        GetRequiredPosition(DefaultPositionId);
    public static string CanonicalJson { get; } = BuildCanonicalJson();
    public static string CanonicalSha256 { get; } = Hash(Encoding.UTF8.GetBytes(CanonicalJson));

    static Id65RemotePadRedGemT21Catalog()
    {
        ValidateFrozenCatalog();
    }

    /// <summary>
    /// Resolves only an exact, case-sensitive position ID. Unknown and blank IDs
    /// are rejected instead of receiving a fallback or nearest-position match.
    /// </summary>
    public static Id65RemotePadRedGemT21Position GetRequiredPosition(string positionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        if (!PositionById.TryGetValue(positionId, out Id65RemotePadRedGemT21Position? position))
        {
            throw new KeyNotFoundException(
                $"Unknown remote-pad T21 Red Gem position ID '{positionId}'.");
        }

        return position;
    }

    /// <summary>
    /// Returns a fresh exact row for one catalog position. The source row is
    /// cloned and only the three little-endian XYZ words are overwritten.
    /// </summary>
    public static byte[] CreateExactRowBytes(string positionId)
    {
        Id65RemotePadRedGemT21Position position = GetRequiredPosition(positionId);
        byte[] row = SourceRowBytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(RowCoordinateOffset, 4), position.RawX);
        BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(RowCoordinateOffset + 4, 4), position.RawY);
        BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(RowCoordinateOffset + 8, 4), position.RawZ);
        return row;
    }

    /// <summary>
    /// Exact static containment check against repaired T13995 in its locked A,C,B
    /// order. Boundary points and points on any other Z plane are rejected.
    /// </summary>
    public static bool IsStrictlyInsideSupportTriangle(int editorX, int editorY, int editorZ)
    {
        if (editorZ != SupportVertexAEditorZ)
        {
            return false;
        }

        long edgeAc = Cross(
            SupportVertexAEditorX, SupportVertexAEditorY,
            SupportVertexCEditorX, SupportVertexCEditorY,
            editorX, editorY);
        long edgeCb = Cross(
            SupportVertexCEditorX, SupportVertexCEditorY,
            SupportVertexBEditorX, SupportVertexBEditorY,
            editorX, editorY);
        long edgeBa = Cross(
            SupportVertexBEditorX, SupportVertexBEditorY,
            SupportVertexAEditorX, SupportVertexAEditorY,
            editorX, editorY);

        return edgeAc < 0 && edgeCb < 0 && edgeBa < 0;
    }

    /// <summary>
    /// Bridges the pure catalog to the internal intent vocabulary without
    /// integrating it into any document, compiler, materializer, or candidate.
    /// </summary>
    internal static Id65MobyPlacementIntent CreateRemotePadT21Placement(string positionId)
    {
        Id65RemotePadRedGemT21Position position = GetRequiredPosition(positionId);
        return new(
            PlacementId,
            AtomicGroupId,
            BundleId,
            RowTemplateId,
            Id65MobyRowAllocationPolicy.ReuseResidentExact,
            TargetTrueIndex,
            Id65MobyCoordinatePolicy.AuthorExact,
            new Id65AuthoringPoint(position.RawX, position.RawY, position.RawZ),
            null,
            SupportHandle);
    }

    private static void ValidateFrozenCatalog()
    {
        if (PositionArray.Length != 9 || PositionView.Count != 9 || PositionById.Count != 9 ||
            PositionArray.Select(item => item.PositionId).Distinct(StringComparer.Ordinal).Count() != 9 ||
            PositionArray.Select(item => item.Label).Distinct(StringComparer.Ordinal).Count() != 9 ||
            PositionArray.Select(item => (item.EditorX, item.EditorY, item.EditorZ)).Distinct().Count() != 9 ||
            PositionArray.Select(item => item.OutputRowSha256).Distinct(StringComparer.Ordinal).Count() != 9 ||
            PositionArray.Count(item => item.IsDefault) != 1 ||
            DefaultPosition.PositionId != DefaultPositionId ||
            MaximumSimultaneousPlacementCount != 1 || AllowsMultipleSimultaneousPlacements ||
            RequiresActorPackageInstall || RequiresActorRoot || !RootlessResidentActor ||
            RuntimeAcceptancePassed || RuntimeCandidateAuthorized || NormalCreateBinAuthorized ||
            ReleasePublicationAuthorized)
        {
            throw new InvalidDataException(
                "The remote-pad T21 Red Gem catalog cardinality, rootless reuse, default, or fail-closed policy changed.");
        }

        if (RetailSourceRowWadOffset !=
                RetailSourceObjectTableWadOffset + (SourceTrueIndex * RowByteLength) ||
            LockedId65ResidentRowWadOffset !=
                LockedId65ObjectTableWadOffset + (TargetTrueIndex * RowByteLength) ||
            SourceTrueIndex != TargetTrueIndex || SourceObjectCount != 107)
        {
            throw new InvalidDataException("The exact Town Square/ID65 resident T21 row identity changed.");
        }

        if (SourceRowBytes.Length != RowByteLength ||
            !string.Equals(Hash(SourceRowBytes), SourceRowSha256, StringComparison.Ordinal) ||
            BinaryPrimitives.ReadInt32LittleEndian(SourceRowBytes.AsSpan(RowCoordinateOffset, 4)) !=
                SourceRawX ||
            BinaryPrimitives.ReadInt32LittleEndian(SourceRowBytes.AsSpan(RowCoordinateOffset + 4, 4)) !=
                SourceRawY ||
            BinaryPrimitives.ReadInt32LittleEndian(SourceRowBytes.AsSpan(RowCoordinateOffset + 8, 4)) !=
                SourceRawZ ||
            BinaryPrimitives.ReadUInt16LittleEndian(SourceRowBytes.AsSpan(ActorClassOffset, 2)) !=
                ActorClass)
        {
            throw new InvalidDataException("The exact resident Town Square T21 Red Gem source row changed.");
        }

        for (int index = 0; index < PositionArray.Length; index++)
        {
            Id65RemotePadRedGemT21Position position = PositionArray[index];
            if (position.Ordinal != index ||
                position.RawX != checked(position.EditorX * RawUnitsPerEditorUnit) ||
                position.RawY != checked(position.EditorY * RawUnitsPerEditorUnit) ||
                position.RawZ != checked(position.EditorZ * RawUnitsPerEditorUnit) ||
                !IsStrictlyInsideSupportTriangle(position.EditorX, position.EditorY, position.EditorZ))
            {
                throw new InvalidDataException(
                    $"Remote-pad T21 Red Gem position '{position.PositionId}' changed or escaped T13995.");
            }

            byte[] exactRow = CreateExactRowBytes(position.PositionId);
            if (!string.Equals(Hash(exactRow), position.OutputRowSha256, StringComparison.Ordinal) ||
                !OnlyCoordinateBytesMayDiffer(SourceRowBytes, exactRow) ||
                BinaryPrimitives.ReadUInt16LittleEndian(exactRow.AsSpan(ActorClassOffset, 2)) !=
                    ActorClass)
            {
                throw new InvalidDataException(
                    $"Remote-pad T21 Red Gem row changed outside exact XYZ for '{position.PositionId}'.");
            }
        }

        Id65MobyPlacementIntent centerIntent = CreateRemotePadT21Placement(DefaultPositionId);
        if (centerIntent.Id != PlacementId || centerIntent.AtomicGroupId != AtomicGroupId ||
            centerIntent.BundleId != BundleId || centerIntent.RowTemplateId != RowTemplateId ||
            centerIntent.RowPolicy != Id65MobyRowAllocationPolicy.ReuseResidentExact ||
            centerIntent.TargetTrueIndex != TargetTrueIndex ||
            centerIntent.CoordinatePolicy != Id65MobyCoordinatePolicy.AuthorExact ||
            centerIntent.CoordinatesOrDelta != new Id65AuthoringPoint(6_144, 5_632, 8_192) ||
            centerIntent.YawByte.HasValue || centerIntent.SupportHandle != SupportHandle)
        {
            throw new InvalidDataException("The exact default resident T21 Red Gem intent changed.");
        }

        if (!string.Equals(CanonicalSha256, ExpectedCanonicalSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Remote-pad T21 Red Gem catalog canonical SHA changed: actual {CanonicalSha256}, " +
                $"expected {ExpectedCanonicalSha256}.");
        }
    }

    private static bool OnlyCoordinateBytesMayDiffer(ReadOnlySpan<byte> source, ReadOnlySpan<byte> output)
    {
        if (source.Length != RowByteLength || output.Length != RowByteLength)
        {
            return false;
        }

        return source[..RowCoordinateOffset].SequenceEqual(output[..RowCoordinateOffset]) &&
            source[(RowCoordinateOffset + RowCoordinateByteLength)..]
                .SequenceEqual(output[(RowCoordinateOffset + RowCoordinateByteLength)..]);
    }

    private static string BuildCanonicalJson()
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter json = new(stream, new JsonWriterOptions { Indented = false }))
        {
            json.WriteStartObject();
            json.WriteString("profileId", ProfileId);
            json.WriteNumber("schemaVersion", SchemaVersion);
            json.WriteString("placementId", PlacementId);
            json.WriteString("atomicGroupId", AtomicGroupId);
            json.WriteString("bundleId", BundleId);
            json.WriteString("rowTemplateId", RowTemplateId);
            json.WriteString("sourceLevelKey", SourceLevelKey);
            json.WriteString("sourceLevelName", SourceLevelName);
            json.WriteString("mobyTypeName", MobyTypeName);
            json.WriteNumber("treasureValue", TreasureValue);
            json.WriteString("rewardOwnerSemantics", RewardOwnerSemantics);
            json.WriteNumber("sourceTrueIndex", SourceTrueIndex);
            json.WriteNumber("targetTrueIndex", TargetTrueIndex);
            json.WriteNumber("sourceObjectCount", SourceObjectCount);
            json.WriteNumber("rowByteLength", RowByteLength);
            json.WriteNumber("rowCoordinateOffset", RowCoordinateOffset);
            json.WriteNumber("rowCoordinateByteLength", RowCoordinateByteLength);
            json.WriteNumber("actorClassOffset", ActorClassOffset);
            json.WriteNumber("actorClass", ActorClass);
            json.WriteString("retailSourceImageSha256", RetailSourceImageSha256);
            json.WriteNumber("retailSourceDataWadEntry", RetailSourceDataWadEntry);
            json.WriteNumber("retailSourceDataWadOffset", RetailSourceDataWadOffset);
            json.WriteNumber("retailSourceObjectTableWadOffset", RetailSourceObjectTableWadOffset);
            json.WriteNumber("retailSourceRowWadOffset", RetailSourceRowWadOffset);
            json.WriteString("lockedId65BaseImageSha256", LockedId65BaseImageSha256);
            json.WriteString("lockedId65DataSha256", LockedId65DataSha256);
            json.WriteNumber("lockedId65DataWadEntry", LockedId65DataWadEntry);
            json.WriteNumber("lockedId65DataWadOffset", LockedId65DataWadOffset);
            json.WriteNumber("lockedId65ObjectTableWadOffset", LockedId65ObjectTableWadOffset);
            json.WriteNumber("lockedId65ResidentRowWadOffset", LockedId65ResidentRowWadOffset);
            json.WriteString("sourceRowSha256", SourceRowSha256);
            json.WriteNumber("sourceRawX", SourceRawX);
            json.WriteNumber("sourceRawY", SourceRawY);
            json.WriteNumber("sourceRawZ", SourceRawZ);
            json.WriteString("rowAllocationSemantics", RowAllocationSemantics);
            json.WriteString("actorAllocationSemantics", ActorAllocationSemantics);
            json.WriteString("actorDependencySemantics", ActorDependencySemantics);
            json.WriteString("mutableFieldSemantics", MutableFieldSemantics);
            json.WriteBoolean("requiresActorPackageInstall", RequiresActorPackageInstall);
            json.WriteBoolean("requiresActorRoot", RequiresActorRoot);
            json.WriteBoolean("rootlessResidentActor", RootlessResidentActor);
            json.WriteString("supportHandle", SupportHandle);
            json.WriteNumber("supportCollisionTrueIndex", SupportCollisionTrueIndex);
            json.WriteString("supportCollisionPackedHex", SupportCollisionPackedHex);
            json.WriteNumber("supportCollisionNormal", SupportCollisionNormal);
            json.WriteString("defaultPositionId", DefaultPositionId);
            json.WriteString("selectionSemantics", SelectionSemantics);
            json.WriteNumber("maximumSimultaneousPlacementCount", MaximumSimultaneousPlacementCount);
            json.WriteBoolean("allowsMultipleSimultaneousPlacements", AllowsMultipleSimultaneousPlacements);
            json.WriteBoolean("runtimeAcceptancePassed", RuntimeAcceptancePassed);
            json.WriteBoolean("runtimeCandidateAuthorized", RuntimeCandidateAuthorized);
            json.WriteBoolean("normalCreateBinAuthorized", NormalCreateBinAuthorized);
            json.WriteBoolean("releasePublicationAuthorized", ReleasePublicationAuthorized);
            json.WriteStartArray("supportTriangleEditorVerticesAcb");
            WritePoint(json, SupportVertexAEditorX, SupportVertexAEditorY, SupportVertexAEditorZ);
            WritePoint(json, SupportVertexCEditorX, SupportVertexCEditorY, SupportVertexCEditorZ);
            WritePoint(json, SupportVertexBEditorX, SupportVertexBEditorY, SupportVertexBEditorZ);
            json.WriteEndArray();
            json.WriteStartArray("positions");
            foreach (Id65RemotePadRedGemT21Position position in PositionArray)
            {
                json.WriteStartObject();
                json.WriteNumber("ordinal", position.Ordinal);
                json.WriteString("positionId", position.PositionId);
                json.WriteString("label", position.Label);
                json.WriteNumber("editorX", position.EditorX);
                json.WriteNumber("editorY", position.EditorY);
                json.WriteNumber("editorZ", position.EditorZ);
                json.WriteNumber("rawX", position.RawX);
                json.WriteNumber("rawY", position.RawY);
                json.WriteNumber("rawZ", position.RawZ);
                json.WriteString("outputRowSha256", position.OutputRowSha256);
                json.WriteBoolean("isDefault", position.IsDefault);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WritePoint(Utf8JsonWriter json, int x, int y, int z)
    {
        json.WriteStartObject();
        json.WriteNumber("x", x);
        json.WriteNumber("y", y);
        json.WriteNumber("z", z);
        json.WriteEndObject();
    }

    private static long Cross(int ax, int ay, int bx, int by, int px, int py) =>
        ((long)bx - ax) * ((long)py - ay) - ((long)by - ay) * ((long)px - ax);

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
