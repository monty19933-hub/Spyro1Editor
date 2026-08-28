using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;

const string ExactSourceRowHex =
    "6055000000000000000000005CE801005CC1010000200000000000000000008000000000000000000000000000000000000000000000530000007D0000000000002000FF000000040000FF0000000001180040FF7F100000";
const string ExactSourceRowSha256 =
    "004efc909b3973a370d141e3f7200d8ea188226f52f2cf1bdbb22ba40c45e0bc";

string[] expectedPositionIds =
[
    Id65RemotePadRedGemT21Catalog.NorthWestPositionId,
    Id65RemotePadRedGemT21Catalog.NorthPositionId,
    Id65RemotePadRedGemT21Catalog.NorthEastPositionId,
    Id65RemotePadRedGemT21Catalog.WestPositionId,
    Id65RemotePadRedGemT21Catalog.CenterPositionId,
    Id65RemotePadRedGemT21Catalog.EastPositionId,
    Id65RemotePadRedGemT21Catalog.SouthWestPositionId,
    Id65RemotePadRedGemT21Catalog.SouthPositionId,
    Id65RemotePadRedGemT21Catalog.SouthEastPositionId
];
string[] expectedHashes =
[
    "85a73516d6e1291480883c09c81f74f06d8713a81df75428c22f2ba8a2d981bf",
    "89037427732f22f211a6e104e8bf755c056ed01cb8d5064bf500a8cfee3908b6",
    "8ec0b330cffdbc7ed968e780732f0a679974e487ab951e1bbdb8cf8c6ca03c86",
    "ea415c39d3aa59f09a0c0f7c7d1b3f453ed4fc9918964ae62173f0b5216c4d77",
    "33588f555826c076076b0f0a0b7b1e489804009cc51d420bc1b9615c7aebfc61",
    "e1f9fb20e309dccefe0b3bf8bc401fd9c5e3bf4f3f916947f73a412c7ee5cf6e",
    "567baab06e6983fbdf370ab7032a3b744287c0ff80ec146e6817506c87796518",
    "7900bca9dcc4e8b4e287dba429a74d633ec4d65596d130413e9b9fe74da90246",
    "2cad5a9cbb22c3da10262773c24241e53bbd2e08c60ad42a007dffb1d138c1e7"
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

byte[] sourceRow = Convert.FromHexString(ExactSourceRowHex);
Require(sourceRow.Length == 0x58 && Hash(sourceRow) == ExactSourceRowSha256 &&
        ExactSourceRowSha256 == Id65RemotePadRedGemT21Catalog.SourceRowSha256 &&
        ExactSourceRowHex == Id65RemotePadRedGemT21Catalog.SourceRowExactHex,
    "The independent exact Town Square T21 row witness changed.");
Require(BinaryPrimitives.ReadInt32LittleEndian(sourceRow.AsSpan(0x0C, 4)) == 125_020 &&
        BinaryPrimitives.ReadInt32LittleEndian(sourceRow.AsSpan(0x10, 4)) == 115_036 &&
        BinaryPrimitives.ReadInt32LittleEndian(sourceRow.AsSpan(0x14, 4)) == 8_192 &&
        BinaryPrimitives.ReadUInt16LittleEndian(sourceRow.AsSpan(0x36, 2)) == 0x0053,
    "The source T21 XYZ or fixed actor class 0x0053 changed.");

Require(Id65RemotePadRedGemT21Catalog.SourceLevelKey == "townsquare" &&
        Id65RemotePadRedGemT21Catalog.SourceLevelName == "Town Square" &&
        Id65RemotePadRedGemT21Catalog.MobyTypeName == "Red Gem" &&
        Id65RemotePadRedGemT21Catalog.TreasureValue == 1 &&
        Id65RemotePadRedGemT21Catalog.RewardOwnerSemantics == "stationary-loose-gem" &&
        Id65RemotePadRedGemT21Catalog.SourceTrueIndex == 21 &&
        Id65RemotePadRedGemT21Catalog.TargetTrueIndex == 21 &&
        Id65RemotePadRedGemT21Catalog.SourceObjectCount == 107 &&
        Id65RemotePadRedGemT21Catalog.RowByteLength == 0x58 &&
        Id65RemotePadRedGemT21Catalog.RowCoordinateOffset == 0x0C &&
        Id65RemotePadRedGemT21Catalog.RowCoordinateByteLength == 0x0C &&
        Id65RemotePadRedGemT21Catalog.ActorClassOffset == 0x36 &&
        Id65RemotePadRedGemT21Catalog.ActorClass == 0x0053,
    "The exact Town Square T21 resident row identity changed.");
Require(Id65RemotePadRedGemT21Catalog.RetailSourceImageSha256 ==
            "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37" &&
        Id65RemotePadRedGemT21Catalog.RetailSourceDataWadEntry == 16 &&
        Id65RemotePadRedGemT21Catalog.RetailSourceDataWadOffset == 0x119E000 &&
        Id65RemotePadRedGemT21Catalog.RetailSourceObjectTableWadOffset == 0x136E170 &&
        Id65RemotePadRedGemT21Catalog.RetailSourceRowWadOffset == 0x136E8A8 &&
        Id65RemotePadRedGemT21Catalog.RetailSourceRowWadOffset ==
            Id65RemotePadRedGemT21Catalog.RetailSourceObjectTableWadOffset + (21 * 0x58),
    "The clean retail Town Square source-disc identity or row offset changed.");
Require(Id65RemotePadRedGemT21Catalog.LockedId65BaseImageSha256 ==
            "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8" &&
        Id65RemotePadRedGemT21Catalog.LockedId65DataSha256 ==
            "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0" &&
        Id65RemotePadRedGemT21Catalog.LockedId65DataWadEntry == 80 &&
        Id65RemotePadRedGemT21Catalog.LockedId65DataWadOffset == 0x6936800 &&
        Id65RemotePadRedGemT21Catalog.LockedId65ObjectTableWadOffset == 0x6B06970 &&
        Id65RemotePadRedGemT21Catalog.LockedId65ResidentRowWadOffset == 0x6B070A8 &&
        Id65RemotePadRedGemT21Catalog.LockedId65ResidentRowWadOffset ==
            Id65RemotePadRedGemT21Catalog.LockedId65ObjectTableWadOffset + (21 * 0x58),
    "The locked ID65 resident T21 row identity changed.");

IReadOnlyList<Id65RemotePadRedGemT21Position> positions =
    Id65RemotePadRedGemT21Catalog.Positions;
Require(positions.Count == 9 &&
        positions.Select(item => item.PositionId).SequenceEqual(expectedPositionIds) &&
        positions.Select(item => item.Label).SequenceEqual(expectedDisplayLabels) &&
        positions.Select(item => item.ToString()).SequenceEqual(expectedDisplayLabels) &&
        positions.Select(item => item.Ordinal).SequenceEqual(Enumerable.Range(0, 9)),
    "The exact nine-entry Red Gem remote-pad position order changed.");
Require(positions.Select(item => item.PositionId).Distinct(StringComparer.Ordinal).Count() == 9 &&
        positions.Select(item => item.Label).Distinct(StringComparer.Ordinal).Count() == 9 &&
        positions.Select(item => (item.EditorX, item.EditorY, item.EditorZ)).Distinct().Count() == 9 &&
        positions.Select(item => (item.RawX, item.RawY, item.RawZ)).Distinct().Count() == 9 &&
        positions.Select(item => item.OutputRowSha256).Distinct(StringComparer.Ordinal).Count() == 9,
    "Red Gem position IDs, coordinates, or output-row hashes are not unique.");

for (int index = 0; index < positions.Count; index++)
{
    Id65RemotePadRedGemT21Position position = positions[index];
    (int X, int Y, int Z) expectedEditor = expectedEditorCoordinates[index];
    Require((position.EditorX, position.EditorY, position.EditorZ) == expectedEditor &&
            (position.RawX, position.RawY, position.RawZ) ==
                (expectedEditor.X * 16, expectedEditor.Y * 16, expectedEditor.Z * 16) &&
            position.OutputRowSha256 == expectedHashes[index],
        $"Red Gem position '{position.PositionId}' changed coordinates or output-row hash.");

    byte[] independentlyDerived = sourceRow.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(independentlyDerived.AsSpan(0x0C, 4), position.RawX);
    BinaryPrimitives.WriteInt32LittleEndian(independentlyDerived.AsSpan(0x10, 4), position.RawY);
    BinaryPrimitives.WriteInt32LittleEndian(independentlyDerived.AsSpan(0x14, 4), position.RawZ);
    byte[] catalogRow = Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(position.PositionId);
    Require(catalogRow.SequenceEqual(independentlyDerived) &&
            Hash(catalogRow) == expectedHashes[index] &&
            catalogRow.AsSpan(0, 0x0C).SequenceEqual(sourceRow.AsSpan(0, 0x0C)) &&
            catalogRow.AsSpan(0x18).SequenceEqual(sourceRow.AsSpan(0x18)) &&
            BinaryPrimitives.ReadUInt16LittleEndian(catalogRow.AsSpan(0x36, 2)) == 0x0053,
        $"Red Gem row '{position.PositionId}' changed outside exact XYZ or lost actor class 0x0053.");
    Require(IndependentStrictContainment(position.EditorX, position.EditorY, position.EditorZ) &&
            Id65RemotePadRedGemT21Catalog.IsStrictlyInsideSupportTriangle(
                position.EditorX, position.EditorY, position.EditorZ),
        $"Red Gem position '{position.PositionId}' escaped repaired T13995.");
}

Id65RemotePadRedGemT21Position center = Id65RemotePadRedGemT21Catalog.DefaultPosition;
Require(center.PositionId == Id65RemotePadRedGemT21Catalog.DefaultPositionId &&
        center.PositionId == Id65RemotePadRedGemT21Catalog.CenterPositionId && center.IsDefault &&
        positions.Count(item => item.IsDefault) == 1 &&
        ReferenceEquals(center,
            Id65RemotePadRedGemT21Catalog.GetRequiredPosition(
                Id65RemotePadRedGemT21Catalog.CenterPositionId)),
    "The Red Gem remote-pad default is not the exact center entry.");

byte[] firstCenterRow = Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(center.PositionId);
firstCenterRow[0] ^= 0xFF;
byte[] secondCenterRow = Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(center.PositionId);
Require(firstCenterRow[0] != secondCenterRow[0] && secondCenterRow[0] == sourceRow[0],
    "Exact row creation leaked mutable shared storage.");

ExpectReject<ArgumentException>(
    "blank position ID",
    () => Id65RemotePadRedGemT21Catalog.GetRequiredPosition(" "));
ExpectReject<KeyNotFoundException>(
    "unknown position ID",
    () => Id65RemotePadRedGemT21Catalog.GetRequiredPosition("remote-pad-grid.x999-y999"));
ExpectReject<KeyNotFoundException>(
    "case-folded position ID",
    () => Id65RemotePadRedGemT21Catalog.GetRequiredPosition(
        Id65RemotePadRedGemT21Catalog.CenterPositionId.ToUpperInvariant()));
ExpectReject<KeyNotFoundException>(
    "unknown row position ID",
    () => Id65RemotePadRedGemT21Catalog.CreateExactRowBytes("remote-pad-grid.x999-y999"));

Require(!Id65RemotePadRedGemT21Catalog.IsStrictlyInsideSupportTriangle(272, 272, 512) &&
        !Id65RemotePadRedGemT21Catalog.IsStrictlyInsideSupportTriangle(384, 496, 512) &&
        !Id65RemotePadRedGemT21Catalog.IsStrictlyInsideSupportTriangle(496, 272, 512) &&
        !Id65RemotePadRedGemT21Catalog.IsStrictlyInsideSupportTriangle(384, 352, 511) &&
        !Id65RemotePadRedGemT21Catalog.IsStrictlyInsideSupportTriangle(384, 352, 513),
    "Strict T13995 containment accepted an edge vertex or another Z plane.");

Require(Id65RemotePadRedGemT21Catalog.RowAllocationSemantics == "reuse-resident-exact" &&
        Id65RemotePadRedGemT21Catalog.ActorAllocationSemantics == "reuse-resident-exact" &&
        Id65RemotePadRedGemT21Catalog.ActorDependencySemantics == "resident-rootless" &&
        Id65RemotePadRedGemT21Catalog.MutableFieldSemantics == "xyz-only" &&
        !Id65RemotePadRedGemT21Catalog.RequiresActorPackageInstall &&
        !Id65RemotePadRedGemT21Catalog.RequiresActorRoot &&
        Id65RemotePadRedGemT21Catalog.RootlessResidentActor,
    "T21 lost exact resident-row reuse or rootless actor semantics.");
Require(Id65RemotePadRedGemT21Catalog.SelectionSemantics == "one-at-a-time" &&
        Id65RemotePadRedGemT21Catalog.MaximumSimultaneousPlacementCount == 1 &&
        !Id65RemotePadRedGemT21Catalog.AllowsMultipleSimultaneousPlacements,
    "The Red Gem catalog no longer declares exactly-one placement semantics.");
Require(!Id65RemotePadRedGemT21Catalog.RuntimeAcceptancePassed &&
        !Id65RemotePadRedGemT21Catalog.RuntimeCandidateAuthorized &&
        !Id65RemotePadRedGemT21Catalog.NormalCreateBinAuthorized &&
        !Id65RemotePadRedGemT21Catalog.ReleasePublicationAuthorized,
    "A Red Gem runtime, candidate, Create BIN, or release gate opened without evidence.");
Require(Id65RemotePadRedGemT21Catalog.SupportHandle == "collision.remote-pad.t13995" &&
        Id65RemotePadRedGemT21Catalog.SupportCollisionTrueIndex == 13_995 &&
        Id65RemotePadRedGemT21Catalog.SupportCollisionPackedHex ==
            "10011C701001380000020000" &&
        Id65RemotePadRedGemT21Catalog.SupportCollisionNormal == -50_176,
    "The repaired T13995 support identity changed.");
Require(Hash(Encoding.UTF8.GetBytes(Id65RemotePadRedGemT21Catalog.CanonicalJson)) ==
            Id65RemotePadRedGemT21Catalog.CanonicalSha256 &&
        Id65RemotePadRedGemT21Catalog.CanonicalSha256 ==
            Id65RemotePadRedGemT21Catalog.ExpectedCanonicalSha256,
    "The frozen Red Gem catalog canonical SHA is not an exact readback.");

ICollection<Id65RemotePadRedGemT21Position> immutableView =
    (ICollection<Id65RemotePadRedGemT21Position>)positions;
ExpectReject<NotSupportedException>(
    "immutable position collection",
    () => immutableView.Add(center));

VerifyInternalExactIntentFactory(center);
VerifyPureCatalogSurface();

Console.WriteLine(
    "PASS Spyro.Editor.Id65RemotePadRedGemT21CatalogSmoke: locked Town Square/ID65 T21 source row, " +
    "exact nine-entry XYZ-only output rows and hashes, fixed rootless actor class 0x0053, " +
    "ReuseResidentExact intent, repaired-T13995 strict containment, immutable exposure, " +
    "canonical SHA, and fail-closed runtime/candidate/Create BIN/release gates passed.");

static void VerifyInternalExactIntentFactory(Id65RemotePadRedGemT21Position center)
{
    MethodInfo factory = typeof(Id65RemotePadRedGemT21Catalog).GetMethod(
        "CreateRemotePadT21Placement",
        BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Missing internal exact T21 placement factory.");
    object intent = factory.Invoke(null, [center.PositionId]) ??
        throw new InvalidOperationException("Internal exact T21 placement factory returned null.");
    Type intentType = intent.GetType();
    object coordinates = ReadProperty<object>(intentType, intent, "CoordinatesOrDelta");
    Type pointType = coordinates.GetType();
    object? yaw = intentType.GetProperty("YawByte", BindingFlags.Instance | BindingFlags.Public)?
        .GetValue(intent);

    Require(ReadProperty<string>(intentType, intent, "Id") ==
                Id65RemotePadRedGemT21Catalog.PlacementId &&
            ReadProperty<string>(intentType, intent, "AtomicGroupId") ==
                Id65RemotePadRedGemT21Catalog.AtomicGroupId &&
            ReadProperty<string>(intentType, intent, "BundleId") ==
                Id65RemotePadRedGemT21Catalog.BundleId &&
            ReadProperty<string>(intentType, intent, "RowTemplateId") ==
                Id65RemotePadRedGemT21Catalog.RowTemplateId &&
            ReadProperty<object>(intentType, intent, "RowPolicy").ToString() ==
                "ReuseResidentExact" &&
            ReadProperty<int>(intentType, intent, "TargetTrueIndex") == 21 &&
            ReadProperty<object>(intentType, intent, "CoordinatePolicy").ToString() ==
                "AuthorExact" &&
            ReadProperty<int>(pointType, coordinates, "X") == center.RawX &&
            ReadProperty<int>(pointType, coordinates, "Y") == center.RawY &&
            ReadProperty<int>(pointType, coordinates, "Z") == center.RawZ &&
            yaw is null &&
            ReadProperty<string>(intentType, intent, "SupportHandle") ==
                Id65RemotePadRedGemT21Catalog.SupportHandle,
        "The internal placement factory changed its exact resident T21 intent.");

    try
    {
        factory.Invoke(null, ["remote-pad-grid.x999-y999"]);
        throw new InvalidOperationException(
            "Internal exact T21 placement factory accepted an unknown position ID.");
    }
    catch (TargetInvocationException exception)
        when (exception.InnerException is KeyNotFoundException)
    {
    }
}

static void VerifyPureCatalogSurface()
{
    string[] forbiddenMethodFragments = ["Compile", "Materialize", "Publish", "CreateBin", "Export"];
    string[] publicMethods = typeof(Id65RemotePadRedGemT21Catalog)
        .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Select(method => method.Name)
        .ToArray();
    Require(!publicMethods.Any(method => forbiddenMethodFragments.Any(fragment =>
                method.Contains(fragment, StringComparison.OrdinalIgnoreCase))),
        "The prospective Red Gem catalog gained candidate, compiler, materializer, or release behavior.");
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
