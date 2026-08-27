using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;

const string CenterT107RowExactHex =
    "388100000000000000000000001800000016000000200000000000000000000000000000000000000000000000000000000000000000F50100007F0000000000002000FF000000040000FF0C00000000200010FF7F100000";

string[] expectedPositionIds =
[
    Id65RemotePadGrassT107Catalog.NorthWestPositionId,
    Id65RemotePadGrassT107Catalog.NorthPositionId,
    Id65RemotePadGrassT107Catalog.NorthEastPositionId,
    Id65RemotePadGrassT107Catalog.WestPositionId,
    Id65RemotePadGrassT107Catalog.CenterPositionId,
    Id65RemotePadGrassT107Catalog.EastPositionId,
    Id65RemotePadGrassT107Catalog.SouthWestPositionId,
    Id65RemotePadGrassT107Catalog.SouthPositionId,
    Id65RemotePadGrassT107Catalog.SouthEastPositionId
];
string[] expectedHashes =
[
    "dd876093374806841f95a92334518364ba70cae5e41328bdc8e28c2a97d11390",
    "57bbf2acd69210df586aebdecf21bf023a6626d7aa8026ff9900add0fe2f4d41",
    "ce1531f69087055dc109337a55afac974cc52fb1a12175c0e2c0a79c58ad8e6d",
    "61abb5ea66457cc487498d5dbdc93f7cfd0e9956b29bcdeac4563a18967229f7",
    "04f64ddc0b0a8e30c128b0cb1487c5e0a0cded4689e7f18becdbb4723bbc48a2",
    "fc28f4464d42aa0d116b2560cd4d5af0165b7f0cd6e33ef6c0f3b3cdddc25d50",
    "bc73d9ec6e3d35c41b5b3044f2d3fa25550c8ed6407aac05b1e3386eaa843a4a",
    "8de6f59f6b369824fdda9ae378ecbe0931bddd3f511a3392ad2a0f65bdcca5e5",
    "d49fcb12e028a843e3802084a03228486d451e6786a209f4ca08b5f8a9987451"
];
string[] expectedDisplayLabels =
[
    "Top left",
    "Top center",
    "Top right",
    "Middle left",
    "Center",
    "Middle right",
    "Bottom left",
    "Bottom center",
    "Bottom right"
];
(int X, int Y, int Z)[] expectedEditorCoordinates =
[
    (368, 336, 512),
    (384, 336, 512),
    (400, 336, 512),
    (368, 352, 512),
    (384, 352, 512),
    (400, 352, 512),
    (368, 368, 512),
    (384, 368, 512),
    (400, 368, 512)
];

IReadOnlyList<Id65RemotePadGrassT107Position> positions =
    Id65RemotePadGrassT107Catalog.Positions;
Require(positions.Count == 9 &&
        positions.Select(item => item.PositionId).SequenceEqual(expectedPositionIds) &&
        positions.Select(item => item.Label).SequenceEqual(expectedDisplayLabels) &&
        positions.Select(item => item.ToString()).SequenceEqual(expectedDisplayLabels) &&
        positions.Select(item => item.Ordinal).SequenceEqual(Enumerable.Range(0, 9)),
    "The exact nine-entry remote-pad position order changed.");
Require(positions.Select(item => item.PositionId).Distinct(StringComparer.Ordinal).Count() == 9 &&
        positions.Select(item => item.Label).Distinct(StringComparer.Ordinal).Count() == 9 &&
        positions.Select(item => (item.EditorX, item.EditorY, item.EditorZ)).Distinct().Count() == 9 &&
        positions.Select(item => (item.RawX, item.RawY, item.RawZ)).Distinct().Count() == 9 &&
        positions.Select(item => item.RowSha256).Distinct(StringComparer.Ordinal).Count() == 9,
    "Remote-pad position IDs, coordinates, or row hashes are not unique.");

byte[] centerRow = Convert.FromHexString(CenterT107RowExactHex);
Require(centerRow.Length == 0x58 &&
        BinaryPrimitives.ReadUInt16LittleEndian(centerRow.AsSpan(0x36, 2)) ==
            Id65RemotePadGrassT107Catalog.ActorId &&
        centerRow[0x47] == Id65RemotePadGrassT107Catalog.YawByte,
    "The independent exact T107 row witness changed actor or yaw.");
for (int index = 0; index < positions.Count; index++)
{
    Id65RemotePadGrassT107Position position = positions[index];
    (int X, int Y, int Z) expectedEditor = expectedEditorCoordinates[index];
    Require((position.EditorX, position.EditorY, position.EditorZ) == expectedEditor &&
            (position.RawX, position.RawY, position.RawZ) ==
                (expectedEditor.X * 16, expectedEditor.Y * 16, expectedEditor.Z * 16) &&
            position.RowSha256 == expectedHashes[index],
        $"Remote-pad position '{position.PositionId}' changed coordinates or its pinned row hash.");

    byte[] exactRow = centerRow.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(exactRow.AsSpan(0x0C, 4), position.RawX);
    BinaryPrimitives.WriteInt32LittleEndian(exactRow.AsSpan(0x10, 4), position.RawY);
    BinaryPrimitives.WriteInt32LittleEndian(exactRow.AsSpan(0x14, 4), position.RawZ);
    Require(Hash(exactRow) == position.RowSha256,
        $"Independent T107 row derivation failed for '{position.PositionId}'.");
    Require(IndependentStrictContainment(position.EditorX, position.EditorY, position.EditorZ) &&
            Id65RemotePadGrassT107Catalog.IsStrictlyInsideSupportTriangle(
                position.EditorX, position.EditorY, position.EditorZ),
        $"Remote-pad position '{position.PositionId}' escaped repaired T13995.");
}

Id65RemotePadGrassT107Position center = Id65RemotePadGrassT107Catalog.DefaultPosition;
Require(center.PositionId == Id65RemotePadGrassT107Catalog.DefaultPositionId &&
        center.PositionId == Id65RemotePadGrassT107Catalog.CenterPositionId && center.IsDefault &&
        positions.Count(item => item.IsDefault) == 1 &&
        ReferenceEquals(center,
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                Id65RemotePadGrassT107Catalog.CenterPositionId)),
    "The remote-pad default is not the exact center catalog entry.");

ExpectReject<ArgumentException>(
    "blank position ID",
    () => Id65RemotePadGrassT107Catalog.GetRequiredPosition(" "));
ExpectReject<KeyNotFoundException>(
    "unknown position ID",
    () => Id65RemotePadGrassT107Catalog.GetRequiredPosition(
        "remote-pad-grid.x999-y999"));
ExpectReject<KeyNotFoundException>(
    "case-folded position ID",
    () => Id65RemotePadGrassT107Catalog.GetRequiredPosition(
        Id65RemotePadGrassT107Catalog.CenterPositionId.ToUpperInvariant()));

Require(!Id65RemotePadGrassT107Catalog.IsStrictlyInsideSupportTriangle(272, 272, 512) &&
        !Id65RemotePadGrassT107Catalog.IsStrictlyInsideSupportTriangle(384, 496, 512) &&
        !Id65RemotePadGrassT107Catalog.IsStrictlyInsideSupportTriangle(496, 272, 512) &&
        !Id65RemotePadGrassT107Catalog.IsStrictlyInsideSupportTriangle(384, 352, 511) &&
        !Id65RemotePadGrassT107Catalog.IsStrictlyInsideSupportTriangle(384, 352, 513),
    "Strict T13995 containment accepted an edge vertex or another Z plane.");

Require(Id65RemotePadGrassT107Catalog.SelectionSemantics == "one-at-a-time" &&
        Id65RemotePadGrassT107Catalog.MaximumSimultaneousPlacementCount == 1 &&
        !Id65RemotePadGrassT107Catalog.AllowsMultipleSimultaneousPlacements,
    "The remote-pad catalog no longer declares exactly-one placement semantics.");
Require(!Id65RemotePadGrassT107Catalog.RuntimeAcceptancePassed &&
        !Id65RemotePadGrassT107Catalog.RuntimeCandidateAuthorized &&
        !Id65RemotePadGrassT107Catalog.NormalCreateBinAuthorized &&
        !Id65RemotePadGrassT107Catalog.ReleasePublicationAuthorized,
    "A runtime, Create BIN, or release gate opened without evidence.");

Require(Id65RemotePadGrassT107Catalog.PlacementId == "placement.remote-pad.grass.t107" &&
        Id65RemotePadGrassT107Catalog.SupportHandle == "collision.remote-pad.t13995" &&
        Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex == 13_995 &&
        Id65RemotePadGrassT107Catalog.SupportCollisionPackedHex ==
            "10011C701001380000020000" &&
        Id65RemotePadGrassT107Catalog.SupportCollisionNormal == -50_176 &&
        Id65RemotePadGrassT107Catalog.TargetTrueIndex == 107 &&
        Id65RemotePadGrassT107Catalog.ActorId == 0x01F5 &&
        Id65RemotePadGrassT107Catalog.YawByte == 0x04,
    "The exact placement, support, target, actor, or yaw identity changed.");
Require(Hash(Encoding.UTF8.GetBytes(Id65RemotePadGrassT107Catalog.CanonicalJson)) ==
            Id65RemotePadGrassT107Catalog.CanonicalSha256 &&
        Id65RemotePadGrassT107Catalog.CanonicalSha256 ==
            Id65RemotePadGrassT107Catalog.ExpectedCanonicalSha256,
    "The frozen catalog canonical SHA is not an exact readback.");

ICollection<Id65RemotePadGrassT107Position> immutableView =
    (ICollection<Id65RemotePadGrassT107Position>)positions;
ExpectReject<NotSupportedException>(
    "immutable position collection",
    () => immutableView.Add(center));

VerifyInternalExactIntentFactory(center);

Console.WriteLine(
    "PASS Spyro.Editor.Id65RemotePadGrassT107CatalogSmoke: exact nine-entry T107 Grass order, " +
    "unique editor/raw coordinates and row hashes, repaired-T13995 strict containment, Center default, " +
    "strict lookup rejection, one-at-a-time semantics, internal exact intent, immutable exposure, " +
    "canonical SHA, and fail-closed runtime/Create BIN/release gates passed.");

static void VerifyInternalExactIntentFactory(Id65RemotePadGrassT107Position center)
{
    MethodInfo factory = typeof(Id65RemotePadGrassT107Catalog).GetMethod(
        "CreateRemotePadT107Placement",
        BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Missing internal exact placement factory.");
    object intent = factory.Invoke(null, [center.PositionId]) ??
        throw new InvalidOperationException("Internal exact placement factory returned null.");
    Type intentType = intent.GetType();
    object coordinates = ReadProperty<object>(intentType, intent, "CoordinatesOrDelta");
    Type pointType = coordinates.GetType();

    Require(ReadProperty<string>(intentType, intent, "Id") ==
                Id65RemotePadGrassT107Catalog.PlacementId &&
            ReadProperty<string>(intentType, intent, "AtomicGroupId") ==
                Id65RemotePadGrassT107Catalog.AtomicGroupId &&
            ReadProperty<string>(intentType, intent, "BundleId") ==
                Id65RemotePadGrassT107Catalog.BundleId &&
            ReadProperty<string>(intentType, intent, "RowTemplateId") ==
                Id65RemotePadGrassT107Catalog.RowTemplateId &&
            ReadProperty<object>(intentType, intent, "RowPolicy").ToString() == "AppendContiguous" &&
            ReadProperty<int>(intentType, intent, "TargetTrueIndex") == 107 &&
            ReadProperty<object>(intentType, intent, "CoordinatePolicy").ToString() == "AuthorExact" &&
            ReadProperty<int>(pointType, coordinates, "X") == center.RawX &&
            ReadProperty<int>(pointType, coordinates, "Y") == center.RawY &&
            ReadProperty<int>(pointType, coordinates, "Z") == center.RawZ &&
            ReadProperty<byte?>(intentType, intent, "YawByte") == 0x04 &&
            ReadProperty<string>(intentType, intent, "SupportHandle") ==
                Id65RemotePadGrassT107Catalog.SupportHandle,
        "The internal exact placement factory changed its T107 intent.");

    try
    {
        factory.Invoke(null, ["remote-pad-grid.x999-y999"]);
        throw new InvalidOperationException(
            "Internal exact placement factory accepted an unknown position ID.");
    }
    catch (TargetInvocationException exception)
        when (exception.InnerException is KeyNotFoundException)
    {
    }
}

static T ReadProperty<T>(Type type, object instance, string propertyName)
{
    object? value = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?
        .GetValue(instance);
    return value is T typed
        ? typed
        : throw new InvalidOperationException(
            $"Missing or unexpected internal property '{type.FullName}.{propertyName}'.");
}

static bool IndependentStrictContainment(int editorX, int editorY, int editorZ)
{
    if (editorZ != 512)
    {
        return false;
    }

    long edgeAc = Cross(272, 272, 384, 496, editorX, editorY);
    long edgeCb = Cross(384, 496, 496, 272, editorX, editorY);
    long edgeBa = Cross(496, 272, 272, 272, editorX, editorY);
    return edgeAc < 0 && edgeCb < 0 && edgeBa < 0;
}

static long Cross(int ax, int ay, int bx, int by, int px, int py) =>
    ((long)bx - ax) * ((long)py - ay) - ((long)by - ay) * ((long)px - ax);

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static void ExpectReject<TException>(string name, Action action)
    where TException : Exception
{
    try
    {
        action();
        throw new InvalidOperationException($"Expected rejection for {name}.");
    }
    catch (TException)
    {
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
