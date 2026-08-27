using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// One immutable, statically verified placement choice for the remote-pad T107
/// Grass catalog. Coordinates are exposed in both editor and raw on-disc units.
/// </summary>
public sealed class Id65RemotePadGrassT107Position
{
    internal Id65RemotePadGrassT107Position(
        int ordinal,
        string positionId,
        string label,
        int editorX,
        int editorY,
        int editorZ,
        int rawX,
        int rawY,
        int rawZ,
        string rowSha256,
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
        RowSha256 = rowSha256;
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
    public string RowSha256 { get; }
    public bool IsDefault { get; }

    public override string ToString() => Label;
}

/// <summary>
/// Fail-closed Phase D1 catalog for selecting exactly one Artisans Grass actor
/// at one of nine statically supported locations on repaired collision T13995.
/// This catalog is not runtime, normal Create BIN, or release authorization.
/// </summary>
public static class Id65RemotePadGrassT107Catalog
{
    public const string ProfileId = "id65-authoring-t107-remote-pad-grid-v1";
    public const int SchemaVersion = 1;

    public const string PlacementId = "placement.remote-pad.grass.t107";
    public const string AtomicGroupId = "atomic.id65-remote-pad-grass-t107-one-at-a-time";
    public const string SupportHandle = "collision.remote-pad.t13995";
    public const int SupportCollisionTrueIndex = 13_995;
    public const string SupportCollisionPackedHex = "10011C701001380000020000";
    public const int SupportCollisionNormal = -50_176;

    public const string BundleId = "bundle.artisans-grass.actor-01f5";
    public const string RowTemplateId = "row.artisans-grass.t121";
    public const string BundleCanonicalSha256 =
        "22e43e5a192b4486cf0d30f6d42bcbc92249a19b8998e02dc13e98b3c672ca91";
    public const int TargetTrueIndex = 107;
    public const ushort ActorId = 0x01F5;
    public const byte YawByte = 0x04;

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
        "6aaf8b410603dc6b8963b2cae9d576cdec8d99ce9c68af9be57ef70647e49615";

    private const int RawUnitsPerEditorUnit = 16;
    private const int T107RowByteLength = 0x58;
    private const int RawCoordinatesOffset = 0x0C;
    private const int ActorIdOffset = 0x36;
    private const int YawOffset = 0x47;
    private const string CenterT107RowExactHex =
        "388100000000000000000000001800000016000000200000000000000000000000000000000000000000000000000000000000000000F50100007F0000000000002000FF000000040000FF0C00000000200010FF7F100000";

    private static readonly Id65RemotePadGrassT107Position[] PositionArray =
    [
        new(0, NorthWestPositionId, "Top left", 368, 336, 512, 5_888, 5_376, 8_192,
            "dd876093374806841f95a92334518364ba70cae5e41328bdc8e28c2a97d11390", false),
        new(1, NorthPositionId, "Top center", 384, 336, 512, 6_144, 5_376, 8_192,
            "57bbf2acd69210df586aebdecf21bf023a6626d7aa8026ff9900add0fe2f4d41", false),
        new(2, NorthEastPositionId, "Top right", 400, 336, 512, 6_400, 5_376, 8_192,
            "ce1531f69087055dc109337a55afac974cc52fb1a12175c0e2c0a79c58ad8e6d", false),
        new(3, WestPositionId, "Middle left", 368, 352, 512, 5_888, 5_632, 8_192,
            "61abb5ea66457cc487498d5dbdc93f7cfd0e9956b29bcdeac4563a18967229f7", false),
        new(4, CenterPositionId, "Center", 384, 352, 512, 6_144, 5_632, 8_192,
            "04f64ddc0b0a8e30c128b0cb1487c5e0a0cded4689e7f18becdbb4723bbc48a2", true),
        new(5, EastPositionId, "Middle right", 400, 352, 512, 6_400, 5_632, 8_192,
            "fc28f4464d42aa0d116b2560cd4d5af0165b7f0cd6e33ef6c0f3b3cdddc25d50", false),
        new(6, SouthWestPositionId, "Bottom left", 368, 368, 512, 5_888, 5_888, 8_192,
            "bc73d9ec6e3d35c41b5b3044f2d3fa25550c8ed6407aac05b1e3386eaa843a4a", false),
        new(7, SouthPositionId, "Bottom center", 384, 368, 512, 6_144, 5_888, 8_192,
            "8de6f59f6b369824fdda9ae378ecbe0931bddd3f511a3392ad2a0f65bdcca5e5", false),
        new(8, SouthEastPositionId, "Bottom right", 400, 368, 512, 6_400, 5_888, 8_192,
            "d49fcb12e028a843e3802084a03228486d451e6786a209f4ca08b5f8a9987451", false)
    ];

    private static readonly ReadOnlyCollection<Id65RemotePadGrassT107Position> PositionView =
        Array.AsReadOnly(PositionArray);
    private static readonly ReadOnlyDictionary<string, Id65RemotePadGrassT107Position> PositionById =
        new(PositionArray.ToDictionary(item => item.PositionId, StringComparer.Ordinal));

    public static IReadOnlyList<Id65RemotePadGrassT107Position> Positions => PositionView;
    public static Id65RemotePadGrassT107Position DefaultPosition =>
        GetRequiredPosition(DefaultPositionId);
    public static string CanonicalJson { get; } = BuildCanonicalJson();
    public static string CanonicalSha256 { get; } = Hash(Encoding.UTF8.GetBytes(CanonicalJson));

    static Id65RemotePadGrassT107Catalog()
    {
        ValidateFrozenCatalog();
    }

    /// <summary>
    /// Resolves only an exact, case-sensitive position ID. Unknown and blank IDs
    /// are rejected instead of receiving a fallback or nearest-position match.
    /// </summary>
    public static Id65RemotePadGrassT107Position GetRequiredPosition(string positionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        if (!PositionById.TryGetValue(positionId, out Id65RemotePadGrassT107Position? position))
        {
            throw new KeyNotFoundException(
                $"Unknown remote-pad T107 Grass position ID '{positionId}'.");
        }

        return position;
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
    /// Bridges the public catalog to the existing internal compiler intent model.
    /// The legacy compiler still rejects this new placement identity until a later
    /// integration phase explicitly adopts it.
    /// </summary>
    internal static Id65MobyPlacementIntent CreateRemotePadT107Placement(string positionId)
    {
        Id65RemotePadGrassT107Position position = GetRequiredPosition(positionId);
        return new(
            PlacementId,
            AtomicGroupId,
            BundleId,
            RowTemplateId,
            Id65MobyRowAllocationPolicy.AppendContiguous,
            TargetTrueIndex,
            Id65MobyCoordinatePolicy.AuthorExact,
            new Id65AuthoringPoint(position.RawX, position.RawY, position.RawZ),
            YawByte,
            SupportHandle);
    }

    private static void ValidateFrozenCatalog()
    {
        if (PositionArray.Length != 9 || PositionView.Count != 9 || PositionById.Count != 9 ||
            PositionArray.Select(item => item.PositionId).Distinct(StringComparer.Ordinal).Count() != 9 ||
            PositionArray.Select(item => item.Label).Distinct(StringComparer.Ordinal).Count() != 9 ||
            PositionArray.Select(item => (item.EditorX, item.EditorY, item.EditorZ)).Distinct().Count() != 9 ||
            PositionArray.Select(item => item.RowSha256).Distinct(StringComparer.Ordinal).Count() != 9 ||
            PositionArray.Count(item => item.IsDefault) != 1 ||
            DefaultPosition.PositionId != DefaultPositionId ||
            MaximumSimultaneousPlacementCount != 1 || AllowsMultipleSimultaneousPlacements ||
            RuntimeAcceptancePassed || RuntimeCandidateAuthorized || NormalCreateBinAuthorized ||
            ReleasePublicationAuthorized)
        {
            throw new InvalidDataException(
                "The remote-pad T107 Grass catalog cardinality, uniqueness, default, or fail-closed policy changed.");
        }

        byte[] centerRow = Convert.FromHexString(CenterT107RowExactHex);
        if (centerRow.Length != T107RowByteLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(centerRow.AsSpan(ActorIdOffset, 2)) != ActorId ||
            centerRow[YawOffset] != YawByte)
        {
            throw new InvalidDataException("The exact T107 Grass row template changed.");
        }

        for (int index = 0; index < PositionArray.Length; index++)
        {
            Id65RemotePadGrassT107Position position = PositionArray[index];
            if (position.Ordinal != index ||
                position.RawX != checked(position.EditorX * RawUnitsPerEditorUnit) ||
                position.RawY != checked(position.EditorY * RawUnitsPerEditorUnit) ||
                position.RawZ != checked(position.EditorZ * RawUnitsPerEditorUnit) ||
                !IsStrictlyInsideSupportTriangle(position.EditorX, position.EditorY, position.EditorZ))
            {
                throw new InvalidDataException(
                    $"Remote-pad T107 Grass position '{position.PositionId}' changed or escaped T13995.");
            }

            byte[] exactRow = centerRow.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(exactRow.AsSpan(RawCoordinatesOffset, 4), position.RawX);
            BinaryPrimitives.WriteInt32LittleEndian(exactRow.AsSpan(RawCoordinatesOffset + 4, 4), position.RawY);
            BinaryPrimitives.WriteInt32LittleEndian(exactRow.AsSpan(RawCoordinatesOffset + 8, 4), position.RawZ);
            if (!string.Equals(Hash(exactRow), position.RowSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Remote-pad T107 Grass row hash changed for '{position.PositionId}'.");
            }
        }

        Id65MobyPlacementIntent centerIntent = CreateRemotePadT107Placement(DefaultPositionId);
        if (centerIntent.Id != PlacementId || centerIntent.AtomicGroupId != AtomicGroupId ||
            centerIntent.BundleId != BundleId || centerIntent.RowTemplateId != RowTemplateId ||
            centerIntent.RowPolicy != Id65MobyRowAllocationPolicy.AppendContiguous ||
            centerIntent.TargetTrueIndex != TargetTrueIndex ||
            centerIntent.CoordinatePolicy != Id65MobyCoordinatePolicy.AuthorExact ||
            centerIntent.CoordinatesOrDelta != new Id65AuthoringPoint(6_144, 5_632, 8_192) ||
            centerIntent.YawByte != YawByte || centerIntent.SupportHandle != SupportHandle)
        {
            throw new InvalidDataException("The exact default remote-pad T107 Grass intent changed.");
        }

        if (!string.Equals(CanonicalSha256, ExpectedCanonicalSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Remote-pad T107 Grass catalog canonical SHA changed: actual {CanonicalSha256}, " +
                $"expected {ExpectedCanonicalSha256}.");
        }
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
            json.WriteString("supportHandle", SupportHandle);
            json.WriteNumber("supportCollisionTrueIndex", SupportCollisionTrueIndex);
            json.WriteString("supportCollisionPackedHex", SupportCollisionPackedHex);
            json.WriteNumber("supportCollisionNormal", SupportCollisionNormal);
            json.WriteString("bundleId", BundleId);
            json.WriteString("rowTemplateId", RowTemplateId);
            json.WriteString("bundleCanonicalSha256", BundleCanonicalSha256);
            json.WriteNumber("targetTrueIndex", TargetTrueIndex);
            json.WriteNumber("actorId", ActorId);
            json.WriteNumber("yawByte", YawByte);
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
            foreach (Id65RemotePadGrassT107Position position in PositionArray)
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
                json.WriteString("rowSha256", position.RowSha256);
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
